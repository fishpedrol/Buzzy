using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SondaP3;

internal sealed class FalhaDeTeste(string mensagem) : Exception(mensagem);

// Alguém mexeu no mouse ou teclado: o resultado não vale e a limpeza não devolve o cursor,
// porque o mouse agora é do usuário.
internal sealed class Interferencia(string mensagem) : Exception(mensagem);

// SendInput com salvaguardas:
// - antes de cada injeção, GetLastInputInfo não pode mostrar input depois do nosso último evento
//   (com ToleranciaInputMs de folga). Ele só diz QUANDO houve input, não lê tecla nenhuma;
// - falha se o SendInput recusar qualquer evento;
// - depois de mover, o cursor tem que estar onde pusemos, senão alguém mexeu;
// - guarda botão e teclas abaixados pelos eventos realmente aceitos (mesmo lote pela metade)
//   pra limpeza soltar.
internal sealed class Injetor
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_MENU = 0x12;
    private const ushort SCAN_TAB = 0x0F;
    private const ushort SCAN_ALT = 0x38;

    // Folga entre o SendInput e o horário que o Windows grava pro evento (GetTickCount anda
    // de ~16 em ~16 ms).
    internal const int ToleranciaInputMs = 50;

    private Nativo.POINT _esperado;
    private bool _temEsperado;
    private long _ultimoSoltar;
    private readonly Func<bool> _cancelado;

    // Relógio do GetTickCount. Antes da primeira injeção, é o último input do sistema no fim da
    // espera de ociosidade. Input depois disso (além da tolerância) não é nosso.
    private uint _ultimoEventoDaSonda;

    // Na ordem em que foram abaixadas.
    private readonly List<Nativo.KEYBDINPUT> _teclasAbaixadas = [];

    // cancelado é consultado antes de cada injeção (Ctrl+C).
    internal Injetor(Func<bool> cancelado, uint ultimoInputAntes)
    {
        _cancelado = cancelado;
        _ultimoEventoDaSonda = ultimoInputAntes;
    }

    internal bool BotaoAbaixado { get; private set; }

    internal static int TamanhoInput => Marshal.SizeOf<Nativo.INPUT>();

    private static Nativo.INPUT Mouse(int x, int y, uint flags)
    {
        int vx = Nativo.GetSystemMetrics(Nativo.SM_XVIRTUALSCREEN);
        int vy = Nativo.GetSystemMetrics(Nativo.SM_YVIRTUALSCREEN);
        int vw = Nativo.GetSystemMetrics(Nativo.SM_CXVIRTUALSCREEN);
        int vh = Nativo.GetSystemMetrics(Nativo.SM_CYVIRTUALSCREEN);

        var i = new Nativo.INPUT { type = INPUT_MOUSE };
        i.mi.dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
        i.mi.dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
        i.mi.dwFlags = flags | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
        return i;
    }

    private static Nativo.INPUT Tecla(ushort vk, ushort scan, uint flags)
    {
        var i = new Nativo.INPUT { type = INPUT_KEYBOARD };
        i.ki.wVk = vk;
        i.ki.wScan = scan;
        i.ki.dwFlags = flags;
        return i;
    }

    // Até a tolerância (ou negativo), ninguém mais mexeu. Null se GetLastInputInfo falhar.
    internal int? InputDepoisDaSondaMs()
        => Nativo.UltimoInput() is uint ultimo ? Nativo.MsDepoisDe(ultimo, _ultimoEventoDaSonda) : null;

    // Sem como conferir, não injeta.
    private void ConferirInputAlheio(string oque)
    {
        int? depois = InputDepoisDaSondaMs();
        if (depois is null)
            throw new FalhaDeTeste($"GetLastInputInfo falhou antes de '{oque}': sem como detectar interferência, nada foi injetado.");
        if (depois > ToleranciaInputMs)
        {
            throw new Interferencia(
                $"houve input do mouse ou do teclado {depois} ms depois do último evento injetado pela sonda "
                + $"(tolerância {ToleranciaInputMs} ms), antes de '{oque}': alguém usou o computador durante o teste.");
        }
    }

    private void Enviar(Nativo.INPUT[] lote, string oque, bool limpeza = false)
    {
        // A limpeza roda mesmo depois de cancelamento ou interferência, então pula as conferências.
        if (!limpeza)
        {
            if (_cancelado()) throw new FalhaDeTeste("Execução cancelada.");
            ConferirInputAlheio(oque);
        }
        uint aceitos = Nativo.SendInput((uint)lote.Length, lote, TamanhoInput);
        int erro = Marshal.GetLastWin32Error();
        if (aceitos > 0) _ultimoEventoDaSonda = Nativo.Agora();
        RegistrarAceitos(lote, (int)Math.Min(aceitos, (uint)lote.Length));
        if (aceitos != lote.Length)
        {
            throw new FalhaDeTeste(
                $"SendInput aceitou {aceitos} de {lote.Length} eventos em '{oque}' (erro {erro}).");
        }
    }

    // SendInput insere em ordem e devolve quantos entraram, então valem os primeiros "aceitos".
    private void RegistrarAceitos(Nativo.INPUT[] lote, int aceitos)
    {
        for (int i = 0; i < aceitos; i++)
        {
            Nativo.INPUT evento = lote[i];
            if (evento.type == INPUT_MOUSE)
            {
                if ((evento.mi.dwFlags & MOUSEEVENTF_LEFTDOWN) != 0) BotaoAbaixado = true;
                if ((evento.mi.dwFlags & MOUSEEVENTF_LEFTUP) != 0) BotaoAbaixado = false;
            }
            else if (evento.type == INPUT_KEYBOARD)
            {
                Nativo.KEYBDINPUT k = evento.ki;
                int j = _teclasAbaixadas.FindIndex(t => MesmaTecla(t, k));
                if ((k.dwFlags & KEYEVENTF_KEYUP) != 0)
                {
                    if (j >= 0) _teclasAbaixadas.RemoveAt(j);
                }
                else if (j < 0)
                {
                    _teclasAbaixadas.Add(k);
                }
            }
        }
    }

    private static bool MesmaTecla(Nativo.KEYBDINPUT a, Nativo.KEYBDINPUT b)
        => a.wVk == b.wVk && a.wScan == b.wScan
            && (a.dwFlags & KEYEVENTF_UNICODE) == (b.dwFlags & KEYEVENTF_UNICODE);

    private static string NomeDaTecla(Nativo.KEYBDINPUT t)
        => (t.dwFlags & KEYEVENTF_UNICODE) != 0
            ? $"o caractere U+{t.wScan:X4}"
            : t.wVk switch
            {
                VK_MENU => "a tecla Alt",
                VK_TAB => "a tecla Tab",
                _ => $"a tecla VK 0x{t.wVk:X2}",
            };

    // Em px, sem contar interferência.
    internal int MaiorDeriva { get; private set; }

    // Até 3 px é deriva do sensor com o mouse parado, e não acumula porque cada evento é
    // absoluto. Espera até 150 ms o Windows aplicar o movimento antes de acusar interferência.
    internal void ConferirCursor()
    {
        if (!_temEsperado) return;
        long inicio = Stopwatch.GetTimestamp();
        while (true)
        {
            Nativo.POINT p = Nativo.Cursor();
            int desvio = Math.Max(Math.Abs(p.X - _esperado.X), Math.Abs(p.Y - _esperado.Y));
            if (desvio <= 3)
            {
                MaiorDeriva = Math.Max(MaiorDeriva, desvio);
                return;
            }
            if (Stopwatch.GetElapsedTime(inicio).TotalMilliseconds > 150)
            {
                throw new Interferencia(
                    $"cursor em {p}, esperado {_esperado}: alguém mexeu no mouse durante o teste.");
            }
            Thread.Sleep(5);
        }
    }

    private void Posto(int x, int y)
    {
        _esperado = new Nativo.POINT(x, y);
        _temEsperado = true;
        ConferirCursor();
    }

    internal void Mover(int x, int y)
    {
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], "mover");
        Posto(x, y);
    }

    internal void Descer(int x, int y)
    {
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE), Mouse(x, y, MOUSEEVENTF_LEFTDOWN)], "descer");
        Posto(x, y);
    }

    // Move e solta no MESMO lote. Num salto grande o botão sobe antes da janela alcançar o
    // cursor, fora do retângulo dela: o caso "arrastar rápido e soltar fora".
    internal void Subir(int x, int y)
    {
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE), Mouse(x, y, MOUSEEVENTF_LEFTUP)], "subir");
        _ultimoSoltar = Stopwatch.GetTimestamp();
        Posto(x, y);
    }

    // Senão um pressionar logo depois de um clique no mesmo ponto vira WM_LBUTTONDBLCLK.
    internal void EsperarIntervaloDeCliqueDuplo()
    {
        if (_ultimoSoltar == 0) return;
        double decorridoMs = Stopwatch.GetElapsedTime(_ultimoSoltar).TotalMilliseconds;
        double minimoMs = Nativo.GetDoubleClickTime() + 250;
        if (decorridoMs < minimoMs) Thread.Sleep((int)(minimoMs - decorridoMs));
    }

    internal void Digitar(string texto)
    {
        var lote = new List<Nativo.INPUT>(texto.Length * 2);
        foreach (char c in texto)
        {
            lote.Add(Tecla(0, c, KEYEVENTF_UNICODE));
            lote.Add(Tecla(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }
        Enviar([.. lote], "digitar");
    }

    // As quatro teclas num lote só, rápido demais pro seletor aparecer.
    internal void AltTabRapido()
    {
        Enviar([
            Tecla(VK_MENU, SCAN_ALT, 0),
            Tecla(VK_TAB, SCAN_TAB, 0),
            Tecla(VK_TAB, SCAN_TAB, KEYEVENTF_KEYUP),
            Tecla(VK_MENU, SCAN_ALT, KEYEVENTF_KEYUP),
        ], "alt+tab rápido");
    }

    // Como gente faz: segura Alt, o seletor aparece, solta. Se o segundo lote não sair, a
    // limpeza solta o Alt.
    internal void AltTabSegurado(int segurarMs)
    {
        Enviar([
            Tecla(VK_MENU, SCAN_ALT, 0),
            Tecla(VK_TAB, SCAN_TAB, 0),
            Tecla(VK_TAB, SCAN_TAB, KEYEVENTF_KEYUP),
        ], "alt+tab segurado (início)");
        Thread.Sleep(segurarMs);
        Enviar([Tecla(VK_MENU, SCAN_ALT, KEYEVENTF_KEYUP)], "alt+tab segurado (fim)");
    }

    // Solta o que ficou abaixado depois de erro, cancelamento ou interferência. Nunca move o
    // cursor: depois de interferência o mouse é do usuário.
    internal List<string> SoltarPendencias()
    {
        var feito = new List<string>();

        // Ordem inversa, como quem solta um atalho (Alt por último).
        Nativo.KEYBDINPUT[] pendentes = [.. _teclasAbaixadas];
        for (int i = pendentes.Length - 1; i >= 0; i--)
        {
            Nativo.KEYBDINPUT t = pendentes[i];
            string nome = NomeDaTecla(t);
            try
            {
                Enviar([Tecla(t.wVk, t.wScan, (t.dwFlags & KEYEVENTF_UNICODE) | KEYEVENTF_KEYUP)], $"limpeza: soltar {nome}", limpeza: true);
                feito.Add($"soltou {nome}");
            }
            catch (Exception e)
            {
                feito.Add($"falha ao soltar {nome}: {e.Message}");
            }
        }
        _teclasAbaixadas.Clear();

        if (BotaoAbaixado)
        {
            // Só LEFTUP, sem MOVE nem ABSOLUTE e dx = dy = 0: solta onde o cursor está, sem movê-lo.
            var soltar = new Nativo.INPUT { type = INPUT_MOUSE };
            soltar.mi.dwFlags = MOUSEEVENTF_LEFTUP;
            Nativo.POINT p = Nativo.Cursor();
            try
            {
                Enviar([soltar], "limpeza: soltar o botão esquerdo", limpeza: true);
                feito.Add($"soltou o botão esquerdo onde o cursor estava, {p}, sem movê-lo");
            }
            catch (Exception e)
            {
                feito.Add("falha ao soltar o botão esquerdo: " + e.Message);
            }
            BotaoAbaixado = false;
        }
        return feito;
    }
}
