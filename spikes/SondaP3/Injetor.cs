using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SondaP3;

internal sealed class FalhaDeTeste(string mensagem) : Exception(mensagem);

/// <summary>
/// Alguém usou o mouse ou o teclado durante o teste: os resultados deixam de valer e, na
/// limpeza, a sonda não devolve o cursor à posição original (o usuário assumiu o mouse).
/// </summary>
internal sealed class Interferencia(string mensagem) : Exception(mensagem);

/// <summary>
/// Injeção de input SINTÉTICO por SendInput. Todo evento sai com a marca de injetado do
/// Windows e é rotulado como sintético nos resultados; não é gesto humano.
///
/// Salvaguardas:
/// - antes de cada injeção, de mouse ou de teclado, confere por GetLastInputInfo que não houve
///   input depois do último evento injetado pela sonda (tolerância de <see cref="ToleranciaInputMs"/>
///   ms para o Windows registrar o próprio evento injetado). GetLastInputInfo diz só QUANDO
///   houve o último input, de qualquer dispositivo; nenhuma tecla é lida. Se houve, alguém
///   usou o mouse ou o teclado e o teste aborta com <see cref="Interferencia"/>;
/// - confere o retorno de SendInput e falha se o Windows recusar qualquer evento;
/// - depois de cada movimento, confere que o cursor está onde o harness o pôs; se não
///   estiver, alguém mexeu no mouse e o teste aborta;
/// - acompanha, pelos eventos que o SendInput de fato aceitou (inclusive num lote aceito pela
///   metade), o botão esquerdo e cada tecla que ficaram abaixados, para a limpeza soltá-los.
/// </summary>
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

    /// <summary>
    /// Folga, em ms, entre o fim do SendInput e o instante que o Windows registra como último
    /// input para o evento injetado (o relógio de GetTickCount anda em passos de ~16 ms).
    /// </summary>
    internal const int ToleranciaInputMs = 50;

    private Nativo.POINT _esperado;
    private bool _temEsperado;
    private long _ultimoSoltar;
    private readonly Func<bool> _cancelado;

    /// <summary>
    /// Instante (relógio de GetTickCount) do último evento injetado pela sonda; antes da
    /// primeira injeção, o do último input do sistema quando a espera de ociosidade terminou.
    /// Input posterior a ele, além da tolerância, não é da sonda.
    /// </summary>
    private uint _ultimoEventoDaSonda;

    /// <summary>Teclas que a sonda abaixou e ainda não soltou, na ordem em que foram abaixadas.</summary>
    private readonly List<Nativo.KEYBDINPUT> _teclasAbaixadas = [];

    /// <param name="cancelado">consultado antes de cada injeção (Ctrl+C no console).</param>
    /// <param name="ultimoInputAntes">instante do último input do sistema quando a espera de
    /// ociosidade terminou; até a primeira injeção, qualquer input depois dele é interferência.</param>
    internal Injetor(Func<bool> cancelado, uint ultimoInputAntes)
    {
        _cancelado = cancelado;
        _ultimoEventoDaSonda = ultimoInputAntes;
    }

    /// <summary>O botão esquerdo ficou abaixado por um evento que o SendInput aceitou.</summary>
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

    /// <summary>
    /// Quantos ms depois do último evento da sonda aconteceu o último input do sistema. Até a
    /// tolerância (ou negativo), ninguém mais usou o mouse nem o teclado. null se
    /// GetLastInputInfo falhar.
    /// </summary>
    internal int? InputDepoisDaSondaMs()
        => Nativo.UltimoInput() is uint ultimo ? Nativo.MsDepoisDe(ultimo, _ultimoEventoDaSonda) : null;

    /// <summary>
    /// Antes de cada injeção: falha com <see cref="Interferencia"/> se houve input do mouse ou
    /// do teclado depois do último evento da sonda. Sem como conferir, não injeta.
    /// </summary>
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
        // A limpeza só solta o que a sonda deixou abaixado; roda mesmo depois de cancelamento
        // ou de interferência, por isso não passa pelas conferências.
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

    /// <summary>
    /// Atualiza o que ficou abaixado a partir só dos eventos aceitos. SendInput insere os
    /// eventos em ordem e devolve quantos inseriu; num lote aceito pela metade, valem
    /// exatamente os primeiros <paramref name="aceitos"/>.
    /// </summary>
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

    /// <summary>Maior distância, em px, entre o cursor e a posição injetada, sem contar interferência.</summary>
    internal int MaiorDeriva { get; private set; }

    /// <summary>
    /// Falha se o cursor estiver a mais de 3 px de onde o harness o deixou por último.
    /// Até 3 px é deriva do sensor do mouse parado; não se acumula, porque cada evento
    /// injetado usa coordenadas absolutas. Tolera até 150 ms para o Windows aplicar o
    /// movimento injetado antes de concluir que houve interferência.
    /// </summary>
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

    /// <summary>
    /// Move até (x, y) e solta no MESMO lote atômico de SendInput. Com um salto grande, o
    /// botão é solto antes de a janela alcançar o cursor, ou seja, fora do retângulo que
    /// ela ocupava: é o cenário "arrastar rápido e soltar fora".
    /// </summary>
    internal void Subir(int x, int y)
    {
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE), Mouse(x, y, MOUSEEVENTF_LEFTUP)], "subir");
        _ultimoSoltar = Stopwatch.GetTimestamp();
        Posto(x, y);
    }

    /// <summary>
    /// Espera o intervalo de clique duplo do sistema desde o último soltar. Sem isso, um
    /// pressionar logo depois de um clique no mesmo ponto viraria WM_LBUTTONDBLCLK.
    /// </summary>
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

    /// <summary>Alt+Tab rápido: as quatro teclas num lote só, sem o seletor aparecer.</summary>
    internal void AltTabRapido()
    {
        Enviar([
            Tecla(VK_MENU, SCAN_ALT, 0),
            Tecla(VK_TAB, SCAN_TAB, 0),
            Tecla(VK_TAB, SCAN_TAB, KEYEVENTF_KEYUP),
            Tecla(VK_MENU, SCAN_ALT, KEYEVENTF_KEYUP),
        ], "alt+tab rápido");
    }

    /// <summary>
    /// Alt+Tab como uma pessoa faz: Alt segurado, o seletor aparece, depois solta. O Alt fica
    /// abaixado entre os dois lotes; se o segundo não sair (interferência, cancelamento ou
    /// recusa do SendInput), a limpeza o solta.
    /// </summary>
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

    /// <summary>
    /// Limpeza: solta cada tecla e o botão esquerdo que a sonda deixou abaixados por causa de
    /// um erro, de cancelamento ou de interferência no meio do gesto. Nunca move o cursor: o
    /// botão é solto onde o cursor estiver, porque depois de interferência o mouse é do usuário.
    /// </summary>
    internal List<string> SoltarPendencias()
    {
        var feito = new List<string>();

        // Na ordem inversa à de abaixar, como uma pessoa soltando um atalho (o Alt por último).
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
            // Só MOUSEEVENTF_LEFTUP: sem MOUSEEVENTF_MOVE, sem coordenadas absolutas e com
            // dx = dy = 0, o botão é solto onde o cursor estiver e o cursor não sai do lugar.
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
