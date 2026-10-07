using System.Runtime.InteropServices;
using Buzzy.App.Composicao;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

// Único lugar do Buzzy que olha janela de outro app, e só a geometria da janela em
// primeiro plano. Privacidade:
// - Ganchos out-of-context, pulando o próprio processo: EVENT_SYSTEM_FOREGROUND no
//   sistema todo e EVENT_OBJECT_LOCATIONCHANGE só na thread em primeiro plano. Nada de
//   input; o cursor (que também gera LOCATIONCHANGE) é descartado na hora.
// - Lê só o retângulo e o estado do shell. A thread serve pra reassinar e reconhecer o
//   Buzzy; processo, título, classe, texto e pixels nunca são lidos.
// - hwnd e retângulo não ficam guardados nem vão pro log; viram na hora a lista de
//   monitores ocupados.
// SetWinEventHook, GetForegroundWindow e GetWindowThreadProcessId só são liberadas
// neste arquivo. Os eventos chegam na thread da UI durante o pump: só conta e sinaliza,
// o resto fica pra avaliação num disparo do Dispatcher.
internal sealed class ObservadorDeTelaCheia : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0;
    private const int CHILDID_SELF = 0;

    // Delegado em campo pro GC não coletar o ponteiro que o Windows chama.
    private readonly Nativo.ProcedimentoDeEvento _aoEvento;
    private readonly uint _threadDaInterface;
    private nint _ganchoDePrimeiroPlano;
    private nint _ganchoDeGeometria;
    private uint _threadDaGeometria;

    internal ObservadorDeTelaCheia()
    {
        _aoEvento = AoEvento;
        _threadDaInterface = Nativo.GetCurrentThreadId();
    }

    // "primeiro plano" ou "geometria".
    internal event Action<string>? Sinal;

    // Contadores pro log.
    internal long EventosDePrimeiroPlano { get; private set; }

    // Antes do filtro, inclusive os do cursor: é a taxa que interessa medir.
    internal long EventosDeGeometria { get; private set; }

    internal bool Ligado => _ganchoDePrimeiroPlano != 0;

    // Se falhar, o modo tela cheia simplesmente não age.
    internal bool Iniciar()
    {
        if (_ganchoDePrimeiroPlano == 0)
        {
            _ganchoDePrimeiroPlano = Nativo.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _aoEvento, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }
        return Ligado;
    }

    // Reassina a geometria se a thread mudou. Janela do próprio Buzzy (o menu) volta sem retângulo.
    internal LeituraDoPrimeiroPlano Ler()
    {
        nint janela = Nativo.GetForegroundWindow();
        if (janela == 0) return new LeituraDoPrimeiroPlano(null, Shell(), DoBuzzy: false);
        // Ponteiro do processo nulo: só a thread, o processo nunca é lido.
        uint thread = Nativo.GetWindowThreadProcessId(janela, 0);
        if (thread == _threadDaInterface) return new LeituraDoPrimeiroPlano(null, null, DoBuzzy: true);
        AssinarGeometria(thread);
        RetanguloPx? retangulo = Win32.GetWindowRect(janela, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;
        return new LeituraDoPrimeiroPlano(retangulo, Shell(), DoBuzzy: false);
    }

    public void Dispose()
    {
        AssinarGeometria(0);
        if (_ganchoDePrimeiroPlano != 0) Nativo.UnhookWinEvent(_ganchoDePrimeiroPlano);
        _ganchoDePrimeiroPlano = 0;
    }

    private void AssinarGeometria(uint thread)
    {
        if (thread == _threadDaGeometria && (_ganchoDeGeometria != 0 || thread == 0)) return;
        if (_ganchoDeGeometria != 0) Nativo.UnhookWinEvent(_ganchoDeGeometria);
        _ganchoDeGeometria = thread == 0 || !Ligado ? 0
            : Nativo.SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, 0, _aoEvento, 0, thread,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _threadDaGeometria = _ganchoDeGeometria != 0 ? thread : 0;
    }

    private void AoEvento(nint gancho, uint evento, nint janela, int idObjeto, int idFilho, uint thread, uint tempo)
    {
        if (evento == EVENT_SYSTEM_FOREGROUND)
        {
            EventosDePrimeiroPlano++;
            Sinal?.Invoke("primeiro plano");
            return;
        }
        if (evento != EVENT_OBJECT_LOCATIONCHANGE) return;
        EventosDeGeometria++;
        // Só a janela em primeiro plano em si; cursor, caret e outras janelas da thread ficam de fora.
        if (idObjeto != OBJID_WINDOW || idFilho != CHILDID_SELF || janela == 0 || janela != Nativo.GetForegroundWindow()) return;
        Sinal?.Invoke("geometria");
    }

    private static EstadoDoShell? Shell()
        => Nativo.SHQueryUserNotificationState(out int estado) == 0 && Enum.IsDefined((EstadoDoShell)estado) ? (EstadoDoShell)estado : null;

    // Aqui e não no Win32: o portão de APIs só libera essas funções neste tipo.
    private static class Nativo
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void ProcedimentoDeEvento(nint gancho, uint evento, nint janela, int idObjeto, int idFilho, uint thread, uint tempo);

        [DllImport("user32.dll", ExactSpelling = true)]
        internal static extern nint SetWinEventHook(uint eventoMinimo, uint eventoMaximo, nint modulo, ProcedimentoDeEvento procedimento, uint processo, uint thread, uint opcoes);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWinEvent(nint gancho);

        [DllImport("user32.dll", ExactSpelling = true)]
        internal static extern nint GetForegroundWindow();

        // processo sempre nulo: só a thread.
        [DllImport("user32.dll", ExactSpelling = true)]
        internal static extern uint GetWindowThreadProcessId(nint janela, nint processo);

        [DllImport("shell32.dll", ExactSpelling = true)]
        internal static extern int SHQueryUserNotificationState(out int estado);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern uint GetCurrentThreadId();
    }
}
