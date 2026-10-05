using System.Runtime.InteropServices;
using Buzzy.App.Composicao;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

/// <summary>
/// O observador de tela cheia (DEC-013 e DEC-034; SECURITY.md 3.1), o único lugar do Buzzy que olha para uma janela de outro
/// aplicativo, e só para a geometria da janela em primeiro plano:
/// <list type="bullet">
/// <item>assina, fora do processo e sem o próprio processo, a troca da janela em primeiro plano no sistema todo
/// (<c>EVENT_SYSTEM_FOREGROUND</c>) e a mudança de geometria só na thread da janela em primeiro plano
/// (<c>EVENT_OBJECT_LOCATIONCHANGE</c>), reassinada quando a thread muda: nenhum evento de input, nenhuma geometria de outras
/// janelas, e o cursor, que também gera esse evento, é descartado na hora;</item>
/// <item>na avaliação (<see cref="Ler"/>), lê só o retângulo da janela em primeiro plano e o estado do shell. A thread dela
/// serve para reassinar a geometria e para reconhecer o próprio Buzzy; o processo nunca é pedido. Nada de título, classe,
/// nome ou caminho de processo, texto, pixels ou conteúdo; nenhuma janela ou processo enumerado;</item>
/// <item>o identificador e o retângulo da janela não ficam guardados nem vão ao log: a leitura vira, na hora, a lista dos
/// monitores ocupados (<see cref="AgendaDaTelaCheia"/>). O log só conta eventos.</item>
/// </list>
/// As funções da lista proibida que ele usa (<c>SetWinEventHook</c>, <c>GetForegroundWindow</c> e
/// <c>GetWindowThreadProcessId</c>) só são permitidas neste tipo e neste arquivo, pela regra de usos restritos do portão de
/// APIs. Os eventos chegam fora de contexto, na thread da interface, durante a leitura de mensagens: o tratamento só conta e
/// sinaliza, e o resto fica para a avaliação, num disparo do Dispatcher. Só na thread da interface.
/// </summary>
internal sealed class ObservadorDeTelaCheia : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0;
    private const int CHILDID_SELF = 0;

    // O delegado fica num campo enquanto houver gancho: sem ele, o coletor apagaria o ponteiro que o Windows chama.
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

    /// <summary>Um evento que pode ter mudado a tela cheia: "primeiro plano" ou "geometria".</summary>
    internal event Action<string>? Sinal;

    /// <summary>Trocas de primeiro plano recebidas desde o começo (para o log; medição do protótipo P7).</summary>
    internal long EventosDePrimeiroPlano { get; private set; }

    /// <summary>
    /// Eventos de geometria recebidos da thread em primeiro plano desde o começo, antes do filtro, inclusive os do cursor
    /// (para o log; a taxa que o protótipo P7 precisava medir).
    /// </summary>
    internal long EventosDeGeometria { get; private set; }

    /// <summary>Se a troca de primeiro plano está assinada.</summary>
    internal bool Ligado => _ganchoDePrimeiroPlano != 0;

    /// <summary>Assina a troca de primeiro plano no sistema todo. Devolve se conseguiu; sem ela, o modo não age.</summary>
    internal bool Iniciar()
    {
        if (_ganchoDePrimeiroPlano == 0)
        {
            _ganchoDePrimeiroPlano = Nativo.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _aoEvento, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }
        return Ligado;
    }

    /// <summary>
    /// A janela em primeiro plano agora: o retângulo dela e o estado do shell. Se a thread dela mudou, a geometria passa a
    /// ser assinada nela. A janela do próprio Buzzy (o menu) só é reconhecida, sem retângulo.
    /// </summary>
    internal LeituraDoPrimeiroPlano Ler()
    {
        nint janela = Nativo.GetForegroundWindow();
        if (janela == 0) return new LeituraDoPrimeiroPlano(null, Shell(), DoBuzzy: false);
        // Só a thread: o ponteiro do processo vai nulo, e o processo nunca é lido.
        uint thread = Nativo.GetWindowThreadProcessId(janela, 0);
        if (thread == _threadDaInterface) return new LeituraDoPrimeiroPlano(null, null, DoBuzzy: true);
        AssinarGeometria(thread);
        RetanguloPx? retangulo = Win32.GetWindowRect(janela, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;
        return new LeituraDoPrimeiroPlano(retangulo, Shell(), DoBuzzy: false);
    }

    /// <summary>Tira os dois ganchos. Depois disso, nenhum evento chega.</summary>
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
        // Só a própria janela em primeiro plano: o cursor, o cursor de texto e as outras janelas da thread ficam de fora.
        if (idObjeto != OBJID_WINDOW || idFilho != CHILDID_SELF || janela == 0 || janela != Nativo.GetForegroundWindow()) return;
        Sinal?.Invoke("geometria");
    }

    private static EstadoDoShell? Shell()
        => Nativo.SHQueryUserNotificationState(out int estado) == 0 && Enum.IsDefined((EstadoDoShell)estado) ? (EstadoDoShell)estado : null;

    /// <summary>As declarações do observador, aqui e não em <see cref="Win32"/>: o portão só as permite neste tipo.</summary>
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

        /// <summary>Sempre com <paramref name="processo"/> nulo: só a thread.</summary>
        [DllImport("user32.dll", ExactSpelling = true)]
        internal static extern uint GetWindowThreadProcessId(nint janela, nint processo);

        [DllImport("shell32.dll", ExactSpelling = true)]
        internal static extern int SHQueryUserNotificationState(out int estado);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern uint GetCurrentThreadId();
    }
}
