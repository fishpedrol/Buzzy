using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Buzzy.Verificacao;

/// <summary>A verificação não pôde continuar; a mensagem diz o que faltou.</summary>
internal class FalhaDeVerificacao(string mensagem) : Exception(mensagem);

/// <summary>
/// Um clique foi recusado ANTES de sair: o dono do ponto (ou outra condição exigida) mudou
/// entre a última conferência e o clique. Nenhum botão foi pressionado.
/// </summary>
internal sealed class CliqueRecusado(string mensagem) : FalhaDeVerificacao(mensagem);

/// <summary>
/// Houve input que não veio da ferramenta (mouse, teclado ou outro dispositivo) durante a
/// verificação: alguém está usando o computador, e os resultados deixam de valer.
/// </summary>
internal sealed class Interferencia(string mensagem) : Exception(mensagem);

/// <summary>
/// Input SINTÉTICO por SendInput (marca de injetado do Windows; não é gesto humano).
///
/// Antes de cada injeção:
/// - interferência pelo mouse: o cursor não pode estar a mais de 3 px de onde a ferramenta o pôs;
/// - interferência por qualquer dispositivo, teclado inclusive, sem ler teclas: a HORA do último
///   input do Windows (GetLastInputInfo, que o input injetado também atualiza) não pode ser mais
///   de 50 ms posterior ao último evento injetado por esta ferramenta;
/// - clique: o dono do ponto (janela raiz) é reconferido depois da espera de clique duplo e
///   depois do movimento do cursor; só então saem o pressionar e o soltar, num lote só;
/// - tecla ou texto: a condição dada por quem chama (por exemplo, "a janela da frente é do
///   Buzzy") é conferida imediatamente antes do envio; se falhar, nada sai.
/// Confere o retorno de SendInput e, se o Windows aceitar só parte de um lote, guarda o que ficou
/// pressionado (botão ou tecla) para a limpeza soltar.
/// </summary>
internal sealed class Injetor
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>Até 3 px é deriva do sensor de um mouse parado; mais que isso é alguém mexendo nele.</summary>
    private const int DesvioMaximoDoCursorPx = 3;

    /// <summary>Folga entre a hora do último evento injetado e a hora que o Windows registra para ele.</summary>
    private const int ToleranciaDoUltimoInputMs = 50;

    private readonly Func<bool> _cancelado;
    private readonly HashSet<(ushort Vk, ushort Scan, bool Unicode)> _teclasAbaixadas = [];
    private Nativo.POINT _esperado;
    private bool _temEsperado;
    private long _ultimoSoltar;
    private uint _ultimoInjetado;

    /// <param name="cancelado">Consultado antes de cada injeção (Ctrl+C no console).</param>
    /// <param name="ultimoInputAntesDeComecar">
    /// Hora (GetLastInputInfo) do último input do usuário, lida ao fim da espera de ociosidade:
    /// qualquer input posterior que não seja desta ferramenta é interferência.
    /// </param>
    internal Injetor(Func<bool> cancelado, uint ultimoInputAntesDeComecar)
    {
        _cancelado = cancelado;
        _ultimoInjetado = ultimoInputAntesDeComecar;
    }

    internal bool BotaoEsquerdoAbaixado { get; private set; }
    internal bool BotaoDireitoAbaixado { get; private set; }

    /// <summary>Maior distância, em px, entre o cursor e a posição injetada, sem contar interferência.</summary>
    internal int MaiorDeriva { get; private set; }

    internal static int TamanhoInput => Marshal.SizeOf<Nativo.INPUT>();

    /// <summary>
    /// Clique esquerdo em (x, y), só se o ponto pertencer a <paramref name="donoEsperado"/> (janela
    /// raiz) e <paramref name="outraCondicao"/>, se dada, não devolver um motivo. Ver <see cref="Clicar"/>.
    /// </summary>
    internal void CliqueEsquerdo(int x, int y, nint donoEsperado, Func<string?>? outraCondicao = null)
        => Clicar(x, y, donoEsperado, outraCondicao, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, "esquerdo");

    /// <summary>Clique direito, com as mesmas conferências de <see cref="CliqueEsquerdo"/>.</summary>
    internal void CliqueDireito(int x, int y, nint donoEsperado, Func<string?>? outraCondicao = null)
        => Clicar(x, y, donoEsperado, outraCondicao, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, "direito");

    /// <summary>
    /// Pressiona e solta uma tecla virtual (por exemplo, a letra de acesso de um item de menu),
    /// só se <paramref name="condicao"/> for verdadeira imediatamente antes do envio. Devolve
    /// falso, sem enviar nada, se não for.
    /// </summary>
    internal bool TeclaVirtual(ushort vk, string oque, Func<bool> condicao)
        => Enviar([Tecla(vk, 0, 0), Tecla(vk, 0, KEYEVENTF_KEYUP)], oque, condicao);

    /// <summary>
    /// Combinação de teclas num lote só (por exemplo, Alt+Tab): pressiona na ordem dada e solta na
    /// ordem inversa, só se <paramref name="condicao"/> for verdadeira imediatamente antes.
    /// </summary>
    internal bool Combinacao(ushort[] teclas, string oque, Func<bool> condicao)
    {
        var lote = new List<Nativo.INPUT>(teclas.Length * 2);
        foreach (ushort vk in teclas) lote.Add(Tecla(vk, 0, 0));
        foreach (ushort vk in teclas.Reverse()) lote.Add(Tecla(vk, 0, KEYEVENTF_KEYUP));
        return Enviar([.. lote], oque, condicao);
    }

    // ------------------------------------------------------------------ arraste (Fase 3)

    /// <summary>
    /// Pressiona o botão esquerdo em (x, y) e o MANTÉM pressionado, com as mesmas conferências do
    /// clique: espera do clique duplo, dono do ponto antes e depois de mover o cursor.
    /// </summary>
    /// <param name="esperarCliqueDuplo">
    /// Falso só quando o ponto está longe do último clique (mais que o retângulo de clique duplo),
    /// por exemplo para pegar o personagem no meio de uma queda: a espera o deixaria pousar antes.
    /// </param>
    internal void Pressionar(int x, int y, nint donoEsperado, Func<string?>? outraCondicao = null, bool esperarCliqueDuplo = true)
    {
        if (donoEsperado == 0) throw new ArgumentException("O dono esperado do ponto não pode ser nulo.", nameof(donoEsperado));
        if (BotaoEsquerdoAbaixado) throw new FalhaDeVerificacao("pressionar com o botão esquerdo já pressionado");
        ConferirCursor();
        if (esperarCliqueDuplo) EsperarIntervaloDeCliqueDuplo();
        ExigirDono(x, y, donoEsperado, outraCondicao, "esquerdo (pressionar)", "depois da espera de clique duplo");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], "mover para pressionar");
        Posto(x, y);
        ExigirDono(x, y, donoEsperado, outraCondicao, "esquerdo (pressionar)", "depois de mover o cursor");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN)], "pressionar esquerdo");
    }

    /// <summary>
    /// Move o cursor com o botão esquerdo pressionado (o Buzzy tem a captura do gesto). Sem
    /// conferência de dono: durante o arraste, o ponto é qualquer lugar da tela.
    /// </summary>
    internal void MoverSegurando(int x, int y)
    {
        if (!BotaoEsquerdoAbaixado) throw new FalhaDeVerificacao("mover segurando sem o botão esquerdo pressionado");
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], "mover segurando");
        Posto(x, y);
    }

    /// <summary>Solta o botão esquerdo em (x, y), com o movimento até lá no mesmo evento.</summary>
    internal void SoltarEsquerdo(int x, int y)
    {
        if (!BotaoEsquerdoAbaixado) throw new FalhaDeVerificacao("soltar sem o botão esquerdo pressionado");
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTUP)], "soltar esquerdo");
        Posto(x, y);
        _ultimoSoltar = Stopwatch.GetTimestamp();
    }

    /// <summary>Move o cursor sem botão pressionado (por exemplo, depois de um gesto, para ver se algo ficou preso ao cursor).</summary>
    internal void MoverSemBotao(int x, int y)
    {
        if (BotaoEsquerdoAbaixado || BotaoDireitoAbaixado) throw new FalhaDeVerificacao("mover sem botão com um botão pressionado");
        ConferirCursor();
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], "mover sem botão");
        Posto(x, y);
    }

    /// <summary>
    /// Clique duplo esquerdo em (x, y): os dois cliques num lote só, dentro do tempo de clique
    /// duplo do sistema, com as conferências de <see cref="CliqueEsquerdo"/>.
    /// </summary>
    internal void CliqueDuploEsquerdo(int x, int y, nint donoEsperado, Func<string?>? outraCondicao = null)
    {
        if (donoEsperado == 0) throw new ArgumentException("O dono esperado do ponto não pode ser nulo.", nameof(donoEsperado));
        ConferirCursor();
        EsperarIntervaloDeCliqueDuplo();
        ExigirDono(x, y, donoEsperado, outraCondicao, "duplo", "depois da espera de clique duplo");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], "mover para o clique duplo");
        Posto(x, y);
        ExigirDono(x, y, donoEsperado, outraCondicao, "duplo", "depois de mover o cursor");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN), Mouse(x, y, MOUSEEVENTF_LEFTUP), Mouse(x, y, MOUSEEVENTF_LEFTDOWN), Mouse(x, y, MOUSEEVENTF_LEFTUP)], "clique duplo esquerdo");
        _ultimoSoltar = Stopwatch.GetTimestamp();
    }

    /// <summary>Digita texto como caracteres Unicode, com a mesma regra de condição de <see cref="TeclaVirtual"/>.</summary>
    internal bool Digitar(string texto, Func<bool> condicao)
    {
        var lote = new List<Nativo.INPUT>(texto.Length * 2);
        foreach (char c in texto)
        {
            lote.Add(Tecla(0, c, KEYEVENTF_UNICODE));
            lote.Add(Tecla(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }
        return Enviar([.. lote], "digitar", condicao);
    }

    /// <summary>
    /// Interferência por qualquer dispositivo, sem ler teclas: o Windows guarda só a HORA do
    /// último input, que o input injetado também atualiza. Se ela for mais de 50 ms posterior
    /// ao último evento desta ferramenta, alguém usou o mouse, o teclado ou outro dispositivo.
    /// </summary>
    internal void ConferirUltimoInput()
    {
        uint ultimo = Nativo.UltimoInput()
            ?? throw new FalhaDeVerificacao("GetLastInputInfo falhou: sem ele não há como detectar interferência pelo teclado. Nada mais foi injetado.");
        int adiante = unchecked((int)(ultimo - _ultimoInjetado));
        if (adiante > ToleranciaDoUltimoInputMs)
            throw new Interferencia($"houve input que não veio da ferramenta {adiante} ms depois do último evento injetado (mouse, teclado ou outro dispositivo; as teclas não são lidas).");
    }

    /// <summary>
    /// Limpeza: solta botões e teclas que ficaram pressionados (erro no meio de um gesto ou lote
    /// aceito pela metade). Não move o cursor: depois de uma interferência, o mouse é do usuário.
    /// </summary>
    internal List<string> SoltarPendencias()
    {
        var feito = new List<string>();
        if (BotaoEsquerdoAbaixado) Soltar([BotaoSemMover(MOUSEEVENTF_LEFTUP)], "botão esquerdo", feito);
        if (BotaoDireitoAbaixado) Soltar([BotaoSemMover(MOUSEEVENTF_RIGHTUP)], "botão direito", feito);
        foreach ((ushort vk, ushort scan, bool unicode) in _teclasAbaixadas.ToList())
        {
            string nome = unicode ? "caractere Unicode" : $"tecla virtual 0x{vk:X2}";
            Soltar([Tecla(vk, scan, KEYEVENTF_KEYUP | (unicode ? KEYEVENTF_UNICODE : 0))], nome, feito);
        }
        BotaoEsquerdoAbaixado = false;
        BotaoDireitoAbaixado = false;
        _teclasAbaixadas.Clear();
        return feito;
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Espera o intervalo de clique duplo, reconfere o dono do ponto, move o cursor, reconfere de
    /// novo e só então envia pressionar e soltar num lote só (atômico: nenhum outro input entra no
    /// meio, e o pressionar leva o cursor ao mesmo ponto). Se o dono mudou, ou
    /// <paramref name="outraCondicao"/> devolveu um motivo, lança <see cref="CliqueRecusado"/> sem
    /// pressionar nada.
    /// </summary>
    private void Clicar(int x, int y, nint donoEsperado, Func<string?>? outraCondicao, uint descer, uint subir, string botao)
    {
        if (donoEsperado == 0) throw new ArgumentException("O dono esperado do ponto não pode ser nulo.", nameof(donoEsperado));
        ConferirCursor();
        EsperarIntervaloDeCliqueDuplo();
        ExigirDono(x, y, donoEsperado, outraCondicao, botao, "depois da espera de clique duplo");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE)], $"mover para o clique {botao}");
        Posto(x, y);
        ExigirDono(x, y, donoEsperado, outraCondicao, botao, "depois de mover o cursor");
        Enviar([Mouse(x, y, MOUSEEVENTF_MOVE | descer), Mouse(x, y, subir)], $"clique {botao}");
        _ultimoSoltar = Stopwatch.GetTimestamp();
    }

    private static void ExigirDono(int x, int y, nint donoEsperado, Func<string?>? outraCondicao, string botao, string quando)
    {
        if (Nativo.DonoDoPonto(x, y) != donoEsperado)
            throw new CliqueRecusado($"clique {botao} em ({x},{y}) recusado {quando}: o ponto deixou de pertencer à janela esperada. Nada foi clicado.");
        if (outraCondicao?.Invoke() is { } motivo)
            throw new CliqueRecusado($"clique {botao} em ({x},{y}) recusado {quando}: {motivo}. Nada foi clicado.");
    }

    /// <summary>
    /// Envia um lote. Fora da limpeza, confere imediatamente antes o cancelamento, a
    /// interferência e a condição (se houver); com a condição falsa, devolve falso sem enviar.
    /// </summary>
    private bool Enviar(Nativo.INPUT[] lote, string oque, Func<bool>? condicao = null)
    {
        if (_cancelado()) throw new FalhaDeVerificacao("Execução cancelada.");
        ConferirUltimoInput();
        if (condicao is not null && !condicao()) return false;

        uint antes = unchecked((uint)Environment.TickCount);
        uint aceitos = Nativo.SendInput((uint)lote.Length, lote, TamanhoInput);
        int erro = Marshal.GetLastWin32Error();
        Contabilizar(lote, aceitos);
        if (aceitos > 0) MarcarUltimoInjetado(antes);
        if (aceitos != lote.Length)
            throw new FalhaDeVerificacao($"SendInput aceitou {aceitos} de {lote.Length} eventos em '{oque}' (erro {erro}).");
        return true;
    }

    /// <summary>Limpeza: envia sem conferências (precisa soltar o que ficou pressionado de qualquer jeito).</summary>
    private void Soltar(Nativo.INPUT[] lote, string nome, List<string> feito)
    {
        uint aceitos = Nativo.SendInput((uint)lote.Length, lote, TamanhoInput);
        int erro = Marshal.GetLastWin32Error();
        feito.Add(aceitos == lote.Length ? $"{nome} solto" : $"falha ao soltar {nome} (erro {erro})");
    }

    /// <summary>Atualiza o que está pressionado a partir dos eventos que o Windows aceitou (os primeiros <paramref name="aceitos"/>).</summary>
    private void Contabilizar(Nativo.INPUT[] lote, uint aceitos)
    {
        for (int i = 0; i < lote.Length && i < aceitos; i++)
        {
            Nativo.INPUT e = lote[i];
            if (e.type == INPUT_MOUSE)
            {
                uint f = e.mi.dwFlags;
                if ((f & MOUSEEVENTF_LEFTDOWN) != 0) BotaoEsquerdoAbaixado = true;
                if ((f & MOUSEEVENTF_LEFTUP) != 0) BotaoEsquerdoAbaixado = false;
                if ((f & MOUSEEVENTF_RIGHTDOWN) != 0) BotaoDireitoAbaixado = true;
                if ((f & MOUSEEVENTF_RIGHTUP) != 0) BotaoDireitoAbaixado = false;
            }
            else if (e.type == INPUT_KEYBOARD)
            {
                var tecla = (e.ki.wVk, e.ki.wScan, (e.ki.dwFlags & KEYEVENTF_UNICODE) != 0);
                if ((e.ki.dwFlags & KEYEVENTF_KEYUP) != 0) _teclasAbaixadas.Remove(tecla);
                else _teclasAbaixadas.Add(tecla);
            }
        }
    }

    /// <summary>
    /// Guarda a hora do lote que acabou de sair. O Windows carimba o "último input" ao processar
    /// os eventos, logo depois do SendInput: espera esse carimbo (até 100 ms) para usá-lo como
    /// referência, e nunca uma hora anterior ao fim do envio.
    /// </summary>
    private void MarcarUltimoInjetado(uint antes)
    {
        uint referencia = unchecked((uint)Environment.TickCount);
        long inicio = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(inicio).TotalMilliseconds < 100)
        {
            if (Nativo.UltimoInput() is { } ultimo && unchecked((int)(ultimo - antes)) >= 0)
            {
                if (unchecked((int)(ultimo - referencia)) > 0) referencia = ultimo;
                break;
            }
            Thread.Sleep(1);
        }
        _ultimoInjetado = referencia;
    }

    /// <summary>
    /// Falha com <see cref="Interferencia"/> se o cursor estiver a mais de 3 px de onde a
    /// ferramenta o deixou por último. Tolera até 150 ms para o Windows aplicar o movimento.
    /// </summary>
    private void ConferirCursor()
    {
        if (!_temEsperado) return;
        long inicio = Stopwatch.GetTimestamp();
        while (true)
        {
            Nativo.POINT p = Nativo.Cursor();
            int desvio = Math.Max(Math.Abs(p.X - _esperado.X), Math.Abs(p.Y - _esperado.Y));
            if (desvio <= DesvioMaximoDoCursorPx)
            {
                MaiorDeriva = Math.Max(MaiorDeriva, desvio);
                return;
            }
            if (Stopwatch.GetElapsedTime(inicio).TotalMilliseconds > 150)
                throw new Interferencia($"cursor em {p}, esperado {_esperado}: alguém mexeu no mouse durante a verificação.");
            Thread.Sleep(5);
        }
    }

    private void Posto(int x, int y)
    {
        _esperado = new Nativo.POINT(x, y);
        _temEsperado = true;
        ConferirCursor();
    }

    /// <summary>
    /// Sem esta espera, um pressionar logo depois de um clique no mesmo ponto viraria clique duplo. Quem clica num alvo que
    /// se move (o personagem escalando) chama antes, para escolher o ponto só depois da espera.
    /// </summary>
    internal void EsperarIntervaloDeCliqueDuplo()
    {
        if (_ultimoSoltar == 0) return;
        double decorrido = Stopwatch.GetElapsedTime(_ultimoSoltar).TotalMilliseconds;
        double minimo = Nativo.GetDoubleClickTime() + 250;
        if (decorrido < minimo) Thread.Sleep((int)(minimo - decorrido));
    }

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

    /// <summary>Evento de botão sem movimento (deslocamento relativo zero): age onde o cursor estiver.</summary>
    private static Nativo.INPUT BotaoSemMover(uint flags)
    {
        var i = new Nativo.INPUT { type = INPUT_MOUSE };
        i.mi.dwFlags = flags;
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
}
