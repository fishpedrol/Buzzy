using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Buzzy.Core;

namespace Buzzy.Verificacao;

internal sealed record Resultado(string Criterio, string Situacao, string Detalhe);

/// <summary>
/// Contagem final da verificação. Só <see cref="Falhas"/> reprova a execução. SIMULADO nunca
/// conta como OK e, como os itens não exercitados, fica pendente de verificação manual real.
/// </summary>
internal sealed record Sumario(int Ok, int NaoAplicavel, IReadOnlyList<string> Simulados, int Falhas, IReadOnlyList<string> NaoExercitados);

/// <summary>
/// Verificação sintética dos critérios [MANUAL] 1 a 7 e 10 da Fase 1 (TODO.md), mais o
/// critério 6 repetido no monitor secundário [HW]. Abre um receptor (o "aplicativo do
/// usuário"), dá o foco a ele, abre o Buzzy por cima e confere pelos logs do receptor e do
/// Buzzy. Todo input é SINTÉTICO (SendInput).
///
/// Situação de cada resultado:
/// - OK: exercitado com input SINTÉTICO e confirmado pelos logs e pelo estado das janelas;
/// - SIMULADO: dependeu de uma notificação da bandeja postada pela ferramenta (PostMessage) no
///   lugar da Shell; nunca vale como OK e fica pendente de verificação manual real;
/// - N/A: não se aplica a esta máquina (por exemplo, um monitor só);
/// - FALHOU, INCONCLUSIVO (a condição do teste não se estabeleceu, por exemplo o dono do
///   ponto mudou antes do clique) e INVÁLIDA (interferência humana): contam como falha.
/// </summary>
internal sealed partial class Verificacao
{
    private const string Ok = "OK";
    private const string NaoAplicavel = "N/A";
    private const string Simulado = "SIMULADO";
    private const string Falhou = "FALHOU";
    private const string Inconclusivo = "INCONCLUSIVO";
    private const string Invalida = "INVÁLIDA";

    private const int MensagemDaBandeja = 0x8000 + 1; // WM_APP + 1, como Plataforma/Bandeja.cs
    private const uint IdDoIcone = 1;                    // Bandeja.IdDoIcone
    private const int NIN_SELECT = 0x0400;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int WM_RBUTTONUP = 0x0205;
    private const ushort VK_E = 0x45, VK_S = 0x53, VK_ESCAPE = 0x1B;

    private static readonly Regex ReTexto = new(@"RECEPTOR\|TEXTO\|(?<t>.*)\|ativa=(?<a>\w+)\|foco=(?<f>\w+)$", RegexOptions.Compiled);
    private static readonly Regex ReClique = new(@"RECEPTOR\|CLIQUE\|(?<b>\w+)\|(?<x>-?\d+),(?<y>-?\d+)\|", RegexOptions.Compiled);

    private readonly string _exeBuzzy;
    private readonly string _exeProprio;
    private readonly Relatorio _rel;
    private readonly LogArquivo _logReceptor;
    private readonly Injetor _inj;
    private readonly List<Resultado> _resultados = [];
    private readonly List<string> _naoExercitados = [];
    private readonly StringBuilder _textoEsperado = new();
    private readonly HashSet<int> _filhos = [];

    private Process? _receptor;
    private Process? _buzzy;
    private DateTime _inicioBuzzy;
    private nint _hReceptor;
    private nint _hBuzzy;
    private nint _hServico;
    private Nativo.POINT _alvoReceptor;
    private int _marcaSessaoReceptor;
    private int _marcaFoco;
    private long _inicioLogBuzzy;
    private Nativo.RECT _retanguloOriginal;
    private Nativo.POINT _deslocOpaco;
    private Nativo.POINT _deslocTransparente;
    private RetanguloPx _esperado;
    private Nativo.RECT _areaUtilPrincipal;
    private Nativo.RECT _telaPrincipal;
    private int _dpiPrincipal;
    private bool _buzzyEncerrado;
    private bool _houveInterferencia;

    /// <summary>Prefixo dos rótulos, para distinguir a regressão da Fase 1 dentro da verificação de outra fase.</summary>
    private string _prefixo = "";
    private string _coberturaEsconderPelaBandeja = "não executado";
    private string _coberturaRestaurarPelaBandeja = "não executado";

    /// <summary>O que aconteceu com um menu do Buzzy que a verificação tentou operar.</summary>
    private sealed record MenuOperado(bool Abriu, EventoBuzzy? Fechado, bool DonoEmPrimeiroPlano, bool TeclaEnviada, string Detalhe, bool Recusado = false);

    /// <param name="ultimoInputDoUsuario">Hora do último input do usuário antes de começar (fim da espera de ociosidade).</param>
    internal Verificacao(string exeBuzzy, string exeProprio, string pastaResultados, Relatorio rel, uint ultimoInputDoUsuario)
    {
        _exeBuzzy = exeBuzzy;
        _exeProprio = exeProprio;
        _rel = rel;
        _logReceptor = new LogArquivo(Path.Combine(pastaResultados, "verificacao-receptor.log"));
        _inj = new Injetor(() => Programa.Cancelado, ultimoInputDoUsuario);
    }

    internal IReadOnlyList<Resultado> Resultados => _resultados;

    internal Sumario Executar()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            AbrirBuzzy();

            _marcaFoco = _logReceptor.Contar();
            Digitar("F1-inicio;");
            C5CliqueEmPixelTransparente();
            Digitar("F2-transparente;");
            C6CliqueNoSprite();
            Digitar("F3-sprite;");
            C6NoMonitorSecundario();
            FecharPeriodoDeFoco("do início até o primeiro menu");

            C3EsconderPeloMenuDoPersonagem("C3a");
            Digitar("F5-menu;");
            C4SegundaInstanciaRevelaAExistente();
            Digitar("F6-segunda;");
            C3EsconderPeloMenuDaBandeja();
            Digitar("F7-menubandeja;");
            C3MostrarPelaBandeja();
            Digitar("F8-bandeja;");
            C10SairPeloMenu();
            Digitar("F9-fim;");

            // Input humano depois da última injeção também invalida a execução.
            _inj.ConferirUltimoInput();
        }
        catch (Interferencia e)
        {
            _houveInterferencia = true;
            Registrar("EXECUÇÃO", Invalida, "interferência humana detectada: " + e.Message + " Os resultados desta execução não valem.");
        }
        catch (Exception e)
        {
            Registrar("EXECUÇÃO", Falhou, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        RegistrarCobertura();
        return Resumir();
    }

    // ------------------------------------------------------------------ preparação

    private void CalcularPosicoes()
    {
        List<Nativo.MonitorLido> monitores = Nativo.Monitores();
        Nativo.MonitorLido principal = monitores.FirstOrDefault(m => m.Principal)
            ?? throw new FalhaDeVerificacao("EnumDisplayMonitors não informou o monitor principal.");
        _telaPrincipal = principal.Info.rcMonitor;
        _areaUtilPrincipal = principal.Info.rcWork;
        _dpiPrincipal = principal.Dpi;

        // Posição esperada calculada de forma independente do aplicativo: monitores lidos por
        // esta ferramenta e a regra pública do núcleo.
        var monitor = new MonitorDoDesktop(principal.Info.szDevice, R(_telaPrincipal), R(_areaUtilPrincipal), _dpiPrincipal, true);
        _esperado = Posicionador.Inicial(new Topologia([monitor]), new TamanhoDip(128, 128)).Retangulo;
        _rel.Linha($"   {monitores.Count} monitor(es) por EnumDisplayMonitors; principal {principal.Info.szDevice}: tela {_telaPrincipal}, área útil {_areaUtilPrincipal}, {_dpiPrincipal} DPI; Buzzy esperado em {_esperado}");
    }

    private static RetanguloPx R(Nativo.RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);

    private void AbrirReceptor()
    {
        const int largura = 900, altura = 520;
        int x = Math.Clamp(_esperado.Esquerda - 500, _areaUtilPrincipal.Left + 8, _areaUtilPrincipal.Right - largura - 8);
        int y = _areaUtilPrincipal.Bottom - altura;

        _marcaSessaoReceptor = _logReceptor.Contar();
        var psi = new ProcessStartInfo(_exeProprio) { UseShellExecute = false };
        foreach (string a in new[] { "--receptor", _logReceptor.Caminho, $"{x}", $"{y}", $"{largura}", $"{altura}" }) psi.ArgumentList.Add(a);
        _receptor = Process.Start(psi) ?? throw new FalhaDeVerificacao("O receptor não iniciou.");

        string hwnd = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "SONDA|HWND|", 15000) ?? throw new FalhaDeVerificacao("O receptor não registrou o HWND.");
        var hReceptor = (nint)long.Parse(hwnd[(hwnd.IndexOf("SONDA|HWND|", StringComparison.Ordinal) + 11)..], CultureInfo.InvariantCulture);
        if (hReceptor == 0 || Nativo.PidDe(hReceptor) != (uint)_receptor.Id)
            throw new FalhaDeVerificacao("O HWND registrado pelo receptor não é do processo que a verificação abriu; nenhuma mensagem foi enviada a ele.");
        _hReceptor = hReceptor;
        string alvo = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "SONDA|ALVO|", 5000) ?? throw new FalhaDeVerificacao("O receptor não registrou o ponto de ativação.");
        string[] p = alvo[(alvo.IndexOf("SONDA|ALVO|", StringComparison.Ordinal) + 11)..].Split('|');
        _alvoReceptor = new Nativo.POINT(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture));
        _ = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "RECEPTOR|PRONTO", 5000) ?? throw new FalhaDeVerificacao("O receptor não ficou pronto.");

        Nativo.RECT rr = Nativo.Retangulo(_hReceptor);
        bool cobre = rr.Left <= _esperado.Esquerda && rr.Right >= _esperado.Direita && rr.Top <= _esperado.Topo && rr.Bottom >= _esperado.Base;
        _rel.Linha($"   receptor: pid {_receptor.Id}, hwnd {_hReceptor}, retângulo {rr}, ponto de ativação {_alvoReceptor}; fica embaixo do Buzzy: {cobre}");
        if (!cobre) throw new FalhaDeVerificacao("O receptor não ficou embaixo da posição esperada do Buzzy.");
    }

    private void AtivarReceptor(string motivo)
    {
        Nativo.SetWindowPos(_hReceptor, 0, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
        Thread.Sleep(250);
        if (Nativo.GetForegroundWindow() == _hReceptor) return;

        if (Nativo.DonoDoPonto(_alvoReceptor.X, _alvoReceptor.Y) != _hReceptor)
            throw new FalhaDeVerificacao($"ativar o receptor ({motivo}): o ponto {_alvoReceptor} está coberto por outra janela. Nada foi clicado.");
        _inj.CliqueEsquerdo(_alvoReceptor.X, _alvoReceptor.Y, _hReceptor);
        if (!EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 3000))
            throw new FalhaDeVerificacao($"O receptor não ficou em primeiro plano ({motivo}).");
        Thread.Sleep(300);
        _rel.Linha($"   receptor ativado por clique SINTÉTICO nele mesmo ({motivo})");
    }

    /// <param name="semente">
    /// <c>--semente</c> do Buzzy (verificação do tamagotchi: os sorteios de cara que a onda faz mesmo pausado se repetem);
    /// nula, a do relógio, como nas Fases 1, 3 e 4.
    /// </param>
    /// <param name="comOsNoveAdultos">
    /// Grava no perfil, depois de apagá-lo, a chave adulta ligada e os nove itens marcados (a verificação do tamagotchi usa
    /// todos; desde a DEC-041 o padrão tem a chave desligada).
    /// </param>
    private void AbrirBuzzy(ulong? semente = null, bool comOsNoveAdultos = false)
    {
        nint frenteAntes = Nativo.GetForegroundWindow();
        int marcaReceptor = _logReceptor.Contar();
        _inicioLogBuzzy = LogDoBuzzy.Marca();

        // A partir da Fase 4 o personagem anda sozinho: a verificação o quer parado no lugar inicial.
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy,
            semente is { } s ? ["--pausado", "--semente", s.ToString(CultureInfo.InvariantCulture)] : ["--pausado"]);
        ExigirNenhumBuzzyAberto();
        // Sem nenhum Buzzy aberto, ninguém usa a pasta do perfil: apagada, o Buzzy parte da posição inicial esperada.
        PerfilDaVerificacao.Limpar();
        if (comOsNoveAdultos) PerfilDaVerificacao.ComOsNoveAdultos();
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não iniciou.");
        _inicioBuzzy = _buzzy.StartTime;

        EventoBuzzy janela = Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy");
        _hBuzzy = JanelaDoBuzzy(janela["hwnd"], "JANELA");
        EventoBuzzy servico = Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço");
        _hServico = JanelaDoBuzzy(servico["hwnd"], "SERVICO");
        EventoBuzzy posicao = Esperar(e => e.Chave == "POSICAO", 5000, "posição");
        EventoBuzzy bandeja = Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 5000, "bandeja");
        Thread.Sleep(700);
        AmostrarFilhos();

        nint frenteDepois = Nativo.GetForegroundWindow();
        bool desativou = _logReceptor.Desde(marcaReceptor).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        Registrar("Fase 1 — o Buzzy aparece sem tirar o foco do aplicativo em uso", frenteAntes == _hReceptor && frenteDepois == _hReceptor && !desativou,
            $"frente antes={Quem(frenteAntes)} depois={Quem(frenteDepois)}; receptor desativado={desativou}");

        _retanguloOriginal = Nativo.Retangulo(_hBuzzy);
        bool visivel = Nativo.IsWindowVisible(_hBuzzy);
        bool igualEsperado = _retanguloOriginal.Left == _esperado.Esquerda && _retanguloOriginal.Top == _esperado.Topo
            && _retanguloOriginal.Right == _esperado.Direita && _retanguloOriginal.Bottom == _esperado.Base;
        bool naAreaUtil = _areaUtilPrincipal.Contem(_retanguloOriginal);
        Registrar("Critério 1 — o app inicia e mostra o sprite sobre a área útil do monitor principal", visivel && igualEsperado && naAreaUtil
            && _retanguloOriginal.Bottom == _areaUtilPrincipal.Bottom,
            $"visível={visivel}; janela {_retanguloOriginal} (esperado {_esperado}: {igualEsperado}); dentro da área útil {_areaUtilPrincipal}={naAreaUtil}; pés no chão={_retanguloOriginal.Bottom == _areaUtilPrincipal.Bottom}");

        long ex = (long)Nativo.GetWindowLongPtr(_hBuzzy, Nativo.GWL_EXSTYLE);
        bool topmost = (ex & Nativo.WS_EX_TOPMOST) != 0;
        Registrar("Critério 2 — sempre no topo por padrão e ícone na bandeja", topmost && bandeja["adicionado"] == "True",
            $"WS_EX_TOPMOST={topmost}; bandeja adicionado={bandeja["adicionado"]} versão 4={bandeja["versao4"]} retângulo do ícone={bandeja["retangulo"]}");

        bool tool = (ex & Nativo.WS_EX_TOOLWINDOW) != 0, app = (ex & Nativo.WS_EX_APPWINDOW) != 0;
        Registrar("Critério 7 — fora da barra de tarefas e do Alt+Tab (verificado pelos estilos, sem inspeção visual)", tool && !app,
            $"estilo estendido 0x{ex:X8}: WS_EX_TOOLWINDOW={tool} WS_EX_APPWINDOW={app}; pela regra documentada do Windows, janela de ferramenta não tem botão na barra nem entrada no Alt+Tab");

        Nativo.POINT opaco = EventoBuzzy.Ponto(posicao["pontoOpaco"]);
        Nativo.POINT transparente = EventoBuzzy.Ponto(posicao["pontoTransparente"]);
        _deslocOpaco = new Nativo.POINT(opaco.X - _retanguloOriginal.Left, opaco.Y - _retanguloOriginal.Top);
        _deslocTransparente = new Nativo.POINT(transparente.X - _retanguloOriginal.Left, transparente.Y - _retanguloOriginal.Top);
        _rel.Linha($"   Buzzy: pid {_buzzy.Id}, hwnd {_hBuzzy}, serviço {_hServico}, janela {_retanguloOriginal}, ponto opaco {opaco}, ponto transparente {transparente}");
    }

    private static void ExigirNenhumBuzzyAberto()
    {
        Process[] abertos = Process.GetProcessesByName("Buzzy");
        try
        {
            if (abertos.Length > 0)
                throw new FalhaDeVerificacao($"já há Buzzy aberto (pids {string.Join(", ", abertos.Select(p => p.Id))}); a verificação não mexe em processos que não abriu. Nada foi iniciado.");
            if (Buzzy.App.Testes.Integracao.InstanciaDoBuzzy.Aberta())
                throw new FalhaDeVerificacao("já há um Buzzy aberto nesta sessão (o mutex de instância única existe; talvez com outro nome de arquivo); a verificação não mexe em processos que não abriu. Nada foi iniciado.");
        }
        finally
        {
            foreach (Process p in abertos) p.Dispose();
        }
    }

    private static int ContarBuzzys()
    {
        Process[] abertos = Process.GetProcessesByName("Buzzy");
        foreach (Process p in abertos) p.Dispose();
        return abertos.Length;
    }

    /// <summary>Lê o HWND registrado no log e confere que a janela é do Buzzy aberto pela verificação; sem isso, nenhuma mensagem vai para ela.</summary>
    private nint JanelaDoBuzzy(string texto, string chave)
    {
        if (!long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out long valor) || valor == 0)
            throw new FalhaDeVerificacao($"HWND de {chave} ilegível no log do Buzzy: '{texto}'.");
        var hwnd = (nint)valor;
        if (Nativo.PidDe(hwnd) != (uint)_buzzy!.Id)
            throw new FalhaDeVerificacao($"o HWND de {chave} ({hwnd}) registrado no log não pertence ao Buzzy aberto pela verificação (pid {_buzzy.Id}); nenhuma mensagem foi enviada a ele.");
        return hwnd;
    }

    // ------------------------------------------------------------------ cenários

    private Nativo.POINT PontoOpaco()
    {
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        return new Nativo.POINT(r.Left + _deslocOpaco.X, r.Top + _deslocOpaco.Y);
    }

    private void C5CliqueEmPixelTransparente()
    {
        const string Criterio = "Critério 5 — clique em pixel transparente dentro do retângulo da janela chega ao aplicativo de baixo";
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        var p = new Nativo.POINT(r.Left + _deslocTransparente.X, r.Top + _deslocTransparente.Y);
        nint dono = Nativo.DonoDoPonto(p.X, p.Y);
        long marcaB = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();

        if (dono != _hReceptor)
        {
            Registrar(Criterio, Falhou, $"o teste de acerto do Windows no ponto {p} (dentro do retângulo {r}) aponta {Quem(dono)}, não o receptor; nada foi clicado");
            return;
        }

        try
        {
            _inj.CliqueEsquerdo(p.X, p.Y, _hReceptor);
        }
        catch (CliqueRecusado e)
        {
            Registrar(Criterio, Inconclusivo, e.Message);
            return;
        }
        Thread.Sleep(600);
        bool buzzyRecebeu = LogDoBuzzy.Desde(marcaB).Any(e => e.Chave == "CLIQUE");
        var cliques = _logReceptor.Desde(marcaR).Select(l => ReClique.Match(l)).Where(m => m.Success).ToList();
        bool receptorRecebeu = cliques.Any(m => Math.Abs(int.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture) - p.X) <= 1
            && Math.Abs(int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) - p.Y) <= 1);
        bool frente = Nativo.GetForegroundWindow() == _hReceptor;
        Registrar(Criterio, receptorRecebeu && !buzzyRecebeu && frente,
            $"ponto {p} dentro da janela {r}; teste de acerto: {Quem(dono)}; receptor registrou o clique={receptorRecebeu}; Buzzy registrou clique={buzzyRecebeu}; receptor na frente={frente}");
    }

    /// <param name="condicaoNoMomento">
    /// Conferida pelo injetor junto com o dono do ponto, imediatamente antes do clique; um motivo
    /// não nulo recusa o clique (resultado INCONCLUSIVO).
    /// </param>
    private void C6CliqueNoSprite(string rotulo = "Critério 6 — clicar no sprite não tira o foco do aplicativo ativo", Func<string?>? condicaoNoMomento = null)
    {
        Nativo.POINT p = PontoOpaco();
        nint dono = Nativo.DonoDoPonto(p.X, p.Y);
        if (dono != _hBuzzy)
        {
            Registrar(rotulo, Falhou, $"o teste de acerto no ponto opaco {p} aponta {Quem(dono)}, não o Buzzy; nada foi clicado");
            return;
        }
        long marcaB = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();
        try
        {
            _inj.CliqueEsquerdo(p.X, p.Y, _hBuzzy, condicaoNoMomento);
        }
        catch (CliqueRecusado e)
        {
            Registrar(rotulo, Inconclusivo, e.Message);
            return;
        }
        Thread.Sleep(600);
        bool buzzyRecebeu = LogDoBuzzy.Desde(marcaB).Any(e => e.Chave == "CLIQUE" && e["botao"] == "esquerdo");
        List<string> r = _logReceptor.Desde(marcaR);
        bool desativou = r.Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        bool clicouReceptor = r.Any(l => l.Contains("RECEPTOR|CLIQUE|", StringComparison.Ordinal));
        bool frente = Nativo.GetForegroundWindow() == _hReceptor;
        Registrar(rotulo, buzzyRecebeu && !desativou && !clicouReceptor && frente,
            $"ponto {p}; Buzzy recebeu o clique={buzzyRecebeu}; receptor desativado={desativou}; clique caiu no receptor={clicouReceptor}; receptor na frente={frente}");
    }

    /// <summary>
    /// Critério 6 [HW]: o mesmo clique no sprite com a janela no monitor secundário. Monitores
    /// enumerados por EnumDisplayMonitors. Imediatamente antes do clique, exige que o ponto esteja
    /// no secundário (MonitorFromPoint, pelo szDevice) e que a janela inteira caiba nele; senão,
    /// INCONCLUSIVO com o motivo (por exemplo, o Buzzy se reacomodou depois de mudança de DPI).
    /// </summary>
    private void C6NoMonitorSecundario()
    {
        List<Nativo.MonitorLido> monitores = Nativo.Monitores();
        if (monitores.FirstOrDefault(m => !m.Principal) is not { } sec)
        {
            Registrar("Critério 6 [HW] — repetido no segundo monitor", NaoAplicavel, $"EnumDisplayMonitors encontrou {monitores.Count} monitor(es), nenhum secundário");
            return;
        }

        string rotulo = $"Critério 6 [HW] — clicar no sprite no monitor {sec.Info.szDevice} {sec.Info.rcMonitor} não tira o foco";
        Nativo.RECT area = sec.Info.rcWork;
        int x = (area.Left + area.Right) / 2 - _retanguloOriginal.Largura / 2;
        int y = area.Bottom - _retanguloOriginal.Altura;
        long marca = LogDoBuzzy.Marca();
        // A Fase 1 não tem arraste: a janela é levada ao outro monitor pela ferramenta de teste.
        Nativo.SetWindowPos(_hBuzzy, 0, x, y, 0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOZORDER | Nativo.SWP_NOACTIVATE);
        // Mais que o agrupamento de 300 ms do Buzzy: uma reacomodação por mudança de DPI aparece antes da conferência.
        Thread.Sleep(800);
        _rel.Linha($"   {monitores.Count} monitores por EnumDisplayMonitors; janela do Buzzy levada pela ferramenta a {sec.Info.szDevice} ({sec.Dpi} DPI): {Nativo.Retangulo(_hBuzzy)}");

        string? motivo = ForaDoSecundario(sec);
        if (motivo is not null)
            Registrar(rotulo, Inconclusivo, motivo + DescreverReacomodacao(marca) + "; nada foi clicado");
        else
        {
            C6CliqueNoSprite(rotulo, () => ForaDoSecundario(sec));
            Digitar("F4-monitor2;");
        }

        Nativo.SetWindowPos(_hBuzzy, 0, _retanguloOriginal.Left, _retanguloOriginal.Top, 0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOZORDER | Nativo.SWP_NOACTIVATE);
        Thread.Sleep(300);
    }

    /// <summary>Nulo se a janela do Buzzy está inteira no monitor secundário, do mesmo tamanho, com o ponto opaco nele; senão, o motivo.</summary>
    private string? ForaDoSecundario(Nativo.MonitorLido sec)
    {
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        if (r.Largura != _retanguloOriginal.Largura || r.Altura != _retanguloOriginal.Altura)
            return $"a janela do Buzzy mudou de tamanho ({r}; antes {_retanguloOriginal}), provavelmente por DPI diferente: o ponto opaco calculado no principal não vale";
        Nativo.POINT p = PontoOpaco();
        string? noPonto = Nativo.Monitor(p)?.Info.szDevice;
        if (!string.Equals(noPonto, sec.Info.szDevice, StringComparison.Ordinal))
            return $"o ponto opaco {p} está no monitor {noPonto ?? "nenhum"}, não em {sec.Info.szDevice} (o Buzzy pode ter se reacomodado)";
        if (!sec.Info.rcMonitor.Contem(r))
            return $"a janela do Buzzy {r} não está inteira dentro de {sec.Info.szDevice} {sec.Info.rcMonitor}";
        return null;
    }

    /// <summary>
    /// A chave do monitor de nome GDI <paramref name="nomeGdi"/>, pela última linha TOPOLOGIA deste Buzzy, com o campo
    /// <c>chaves=chave=nomeGdi;…</c> (chave estável, DEC-030); nula sem ela. A linha POSICAO traz o nome GDI ao lado da
    /// chave (<c>gdi=</c>); a linha ITEM, só a chave.
    /// </summary>
    private string? ChaveDoMonitorNoLog(string nomeGdi)
    {
        EventoBuzzy? topologia = LogDoBuzzy.Desde(_inicioLogBuzzy).LastOrDefault(e => e.Chave == "TOPOLOGIA" && e.Campos.ContainsKey("chaves"));
        if (topologia is null) return null;
        foreach (string par in topologia["chaves"].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int igual = par.IndexOf('=', StringComparison.Ordinal);
            if (igual > 0 && string.Equals(par[(igual + 1)..], nomeGdi, StringComparison.OrdinalIgnoreCase)) return par[..igual];
        }
        return null;
    }

    private static string DescreverReacomodacao(long marca)
    {
        List<EventoBuzzy> ev = [.. LogDoBuzzy.Desde(marca).Where(e => e.Chave is "TOPOLOGIA" or "POSICAO")];
        return ev.Count == 0
            ? ""
            : $"; depois do movimento o Buzzy registrou {string.Join(", ", ev.Select(e => e.Chave + (e["motivo"].Length > 0 ? $"({e["motivo"]})" : "")))}: ele se reacomodou (por exemplo, depois de mudança de DPI)";
    }

    // ------------------------------------------------------------------ menus

    /// <summary>
    /// Com o menu do Buzzy aberto depois da <paramref name="marca"/>, escolhe o item pela tecla de
    /// acesso, só se o Buzzy registrou o dono do menu em primeiro plano e a janela da frente é do
    /// processo do Buzzy (conferido de novo pelo injetor imediatamente antes da tecla). Sem isso,
    /// fecha o menu sem teclado (WM_CANCELMODE ao dono) e registra que o teclado não pôde ser usado.
    /// </summary>
    private MenuOperado OperarMenu(long marca, ushort tecla, string nomeDaTecla)
    {
        EventoBuzzy? exibindo = LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000);
        if (exibindo is null) return new MenuOperado(false, null, false, false, "o Buzzy não registrou MENU|exibindo");
        nint dono = long.TryParse(exibindo["dono"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long d) ? (nint)d : 0;
        bool donoNaFrente = exibindo["donoEmPrimeiroPlano"] == "True";
        Thread.Sleep(400);

        var detalhe = new StringBuilder();
        bool enviada = false;
        if (donoNaFrente)
        {
            enviada = _inj.TeclaVirtual(tecla, $"tecla de acesso {nomeDaTecla}", FrenteEhDoBuzzy);
            if (!enviada) detalhe.Append("a janela da frente não era do Buzzy no momento da tecla: nada foi enviado; ");
        }
        else detalhe.Append("o Buzzy registrou donoEmPrimeiroPlano=False: o menu não recebe teclado nem fecha ao clicar fora; ");

        EventoBuzzy? fechado = enviada ? EsperarFechamento(marca, 3000) : null;
        if (fechado is null)
        {
            detalhe.Append(enviada ? "o menu não fechou com a tecla; " : "o teclado não pôde ser usado; ");
            fechado = CancelarMenu(marca, dono, detalhe);
        }
        return new MenuOperado(true, fechado, donoNaFrente, enviada, detalhe.ToString().TrimEnd(' ', ';'));
    }

    private static EventoBuzzy? EsperarFechamento(long marca, int limiteMs)
        => LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), limiteMs);

    /// <summary>
    /// Fecha um menu do Buzzy sem teclado: WM_CANCELMODE postado ao dono do menu, só se o HWND for
    /// do processo do Buzzy. Esc só como último recurso, e só com a janela da frente do Buzzy.
    /// </summary>
    private EventoBuzzy? CancelarMenu(long marca, nint dono, StringBuilder detalhe)
    {
        if (dono != 0 && _buzzy is { HasExited: false } && Nativo.PidDe(dono) == (uint)_buzzy.Id)
        {
            Nativo.PostMessage(dono, Nativo.WM_CANCELMODE, 0, 0);
            detalhe.Append("menu cancelado por WM_CANCELMODE ao dono; ");
            if (EsperarFechamento(marca, 3000) is { } f) return f;
            detalhe.Append("o menu não fechou com WM_CANCELMODE; ");
        }
        else detalhe.Append("o dono do menu não é (mais) do Buzzy: WM_CANCELMODE não enviado; ");

        if (_inj.TeclaVirtual(VK_ESCAPE, "Esc para fechar o menu do Buzzy", FrenteEhDoBuzzy))
        {
            detalhe.Append("Esc enviado com a janela da frente do Buzzy; ");
            return EsperarFechamento(marca, 2000);
        }
        detalhe.Append("Esc não enviado (a janela da frente não era do Buzzy); ");
        return null;
    }

    private bool FrenteEhDoBuzzy()
    {
        nint frente = Nativo.GetForegroundWindow();
        return frente != 0 && _buzzy is { HasExited: false } && Nativo.PidDe(frente) == (uint)_buzzy.Id;
    }

    private static string DescreverMenu(MenuOperado m)
        => $"menu aberto={m.Abriu}; dono do menu em primeiro plano={m.DonoEmPrimeiroPlano}; tecla enviada={m.TeclaEnviada}; fechado com={m.Fechado?["fechado"] ?? "não fechou"}"
           + (m.Detalhe.Length > 0 ? "; " + m.Detalhe : "");

    /// <summary>Abre o menu pelo botão direito no sprite e escolhe um item pela tecla de acesso (ver <see cref="OperarMenu"/>).</summary>
    private MenuOperado MenuDoPersonagem(ushort tecla, string nomeDaTecla)
    {
        Nativo.POINT p = PontoOpaco();
        if (Nativo.DonoDoPonto(p.X, p.Y) != _hBuzzy)
            throw new FalhaDeVerificacao($"o ponto opaco {p} não pertence ao Buzzy; nada foi clicado");
        long marca = LogDoBuzzy.Marca();
        try
        {
            _inj.CliqueDireito(p.X, p.Y, _hBuzzy);
        }
        catch (CliqueRecusado e)
        {
            return new MenuOperado(false, null, false, false, e.Message, Recusado: true);
        }
        if (LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e["aberto"] == "personagem", 3000) is null)
            return new MenuOperado(false, null, false, false, "o menu não abriu em 3 s");
        return OperarMenu(marca, tecla, nomeDaTecla);
    }

    /// <summary>
    /// Critério 3 pelo personagem. O botão direito no próprio Buzzy dá a ele o último input, como o
    /// gesto de uma pessoa: se mesmo assim o dono do menu não receber o primeiro plano (teclado
    /// inutilizável), o resultado é FALHOU, porque uma pessoa teria o mesmo problema.
    /// </summary>
    private void C3EsconderPeloMenuDoPersonagem(string rotulo)
    {
        long marca = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();
        MenuOperado m = MenuDoPersonagem(VK_E, "E (Esconder)");
        EventoBuzzy? escondeu = LogDoBuzzy.Esperar(marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao" && e["motivo"] == "menu", m.TeclaEnviada ? 3000 : 300);
        bool invisivel = !Nativo.IsWindowVisible(_hBuzzy);
        bool voltou = EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 2000);
        bool textoIndevido = _logReceptor.Desde(marcaR).Any(l => l.Contains("RECEPTOR|TEXTO|", StringComparison.Ordinal));

        string situacao = m.Recusado ? Inconclusivo
            : m.Abriu && m.TeclaEnviada && m.Fechado?["fechado"] == "AlternarVisibilidade" && escondeu is not null && invisivel && !textoIndevido ? Ok
            : Falhou;
        Registrar($"Critério 3 ({rotulo}) — botão direito no personagem abre o menu e \"Esconder\" esconde", situacao,
            $"{DescreverMenu(m)}; escondido={invisivel}; a tecla foi parar no receptor={textoIndevido}");
        Registrar($"Menu ({rotulo}) — depois de fechar, o foco volta ao aplicativo que estava em uso", voltou,
            $"frente depois do menu: {Quem(Nativo.GetForegroundWindow())}");
        if (!voltou) AtivarReceptor($"depois do menu {rotulo}");
        AmostrarFilhos();
    }

    private void C4SegundaInstanciaRevelaAExistente()
    {
        long marca = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();
        bool escondidoAntes = !Nativo.IsWindowVisible(_hBuzzy);

        // O mesmo perfil do Buzzy aberto, sem limpar: a pasta é dele.
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy);
        Process segunda = Process.Start(psi) ?? throw new FalhaDeVerificacao("A segunda instância não iniciou.");
        bool saiu = false;
        int codigo = -1;
        string encerramento = "";
        try
        {
            saiu = segunda.WaitForExit(8000);
            if (saiu) codigo = segunda.ExitCode;
        }
        finally
        {
            // Foi a verificação que abriu: se não saiu no tempo, é encerrada aqui mesmo, e registrado.
            if (!segunda.HasExited)
            {
                encerramento = Processos.EncerrarAForca(segunda, "segunda instância");
                _rel.Linha("   " + encerramento);
            }
            segunda.Dispose();
        }

        EventoBuzzy? pedido = LogDoBuzzy.Esperar(marca, e => e.Chave == "INSTANCIA" && e["papel"] == "segunda", 3000);
        EventoBuzzy? mostrou = LogDoBuzzy.Esperar(marca, e => e.Chave == "VISIVEL" && e["visivel"] == "sim" && e["motivo"] == "segunda instância", 3000);
        Thread.Sleep(300);
        bool visivel = Nativo.IsWindowVisible(_hBuzzy);
        int instancias = ContarBuzzys();
        bool frente = Nativo.GetForegroundWindow() == _hReceptor;
        bool desativou = _logReceptor.Desde(marcaR).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));

        Registrar("Critério 4 — abrir o app de novo revela a janela existente sem criar outra instância",
            escondidoAntes && saiu && codigo == 0 && pedido?["pedidoEntregue"] == "True" && mostrou is not null && visivel && instancias == 1,
            $"escondido antes={escondidoAntes}; segunda instância saiu sozinha={saiu} código={codigo} ({DescreverCodigo(codigo)})"
            + (encerramento.Length > 0 ? "; " + encerramento : "")
            + $"; pedido entregue={pedido?["pedidoEntregue"] ?? "?"}; reapareceu={visivel}; processos Buzzy={instancias}");
        Registrar("Critério 4 — reaparecer não tira o foco do aplicativo em uso", frente && !desativou,
            $"receptor na frente={frente}; receptor desativado={desativou}");
        if (!frente) AtivarReceptor("depois da segunda instância");
        AmostrarFilhos();
    }

    // ------------------------------------------------------------------ bandeja

    /// <summary>
    /// Critério 3 pela bandeja, parte "esconder": com o Buzzy visível, o menu da bandeja e o item
    /// "Esconder" pela tecla de acesso. Tenta o clique real no ícone só nas condições seguras de
    /// <see cref="TentarCliqueRealNoIcone"/>; no Windows 11, com o ícone na área de ícones
    /// ocultos, usa o caminho SIMULADO de <see cref="MenuDaBandejaSimulado"/>.
    /// </summary>
    private void C3EsconderPeloMenuDaBandeja()
    {
        const string Criterio = "Critério 3 — o menu da bandeja abre e \"Esconder\" esconde o Buzzy";
        if (!Nativo.IsWindowVisible(_hBuzzy))
        {
            Registrar(Criterio, Inconclusivo, "o Buzzy não estava visível antes do menu da bandeja (a segunda instância não o revelou); nada foi clicado nem postado");
            _coberturaEsconderPelaBandeja = $"{Inconclusivo} (pré-condição não atendida)";
            return;
        }

        long marca = LogDoBuzzy.Marca();
        bool simulado = !TentarCliqueRealNoIcone(direito: true, marca, e => e.Chave == "MENU" && e["aberto"] == "bandeja", "abrir o menu", out string semCliqueReal);
        MenuOperado m;
        string caminho;
        if (!simulado)
        {
            m = OperarMenu(marca, VK_E, "E (Esconder)");
            caminho = "clique direito SINTÉTICO no ícone real da bandeja; notificação enviada pela própria Shell";
        }
        else
        {
            _rel.Linha($"   sem clique real no ícone da bandeja: {semCliqueReal}; usando a notificação SIMULADA");
            if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("antes do menu da bandeja simulado");
            marca = LogDoBuzzy.Marca();
            m = MenuDaBandejaSimulado(marca, VK_E, "E (Esconder)");
            caminho = "notificação WM_CONTEXTMENU da bandeja SIMULADA (PostMessage à janela de serviço), logo depois de um clique SINTÉTICO no personagem para o Buzzy ter o último input";
        }

        EventoBuzzy? escondeu = LogDoBuzzy.Esperar(marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao" && e["motivo"] == "menu", m.TeclaEnviada ? 3000 : 300);
        bool invisivel = !Nativo.IsWindowVisible(_hBuzzy);
        bool funcionou = m.Abriu && m.TeclaEnviada && m.Fechado?["fechado"] == "AlternarVisibilidade" && escondeu is not null && invisivel;
        // Na simulação, o direito de primeiro plano vem do clique no personagem, não da Shell:
        // teclado inutilizável ali é INCONCLUSIVO, não prova de defeito.
        string situacao = funcionou ? (simulado ? Simulado : Ok)
            : m.Recusado || (m.Abriu && !m.TeclaEnviada && simulado) ? Inconclusivo
            : Falhou;
        Registrar(Criterio, situacao, $"{caminho}; {DescreverMenu(m)}; escondido={invisivel}");
        _coberturaEsconderPelaBandeja = $"{situacao} ({(simulado ? "notificação SIMULADA" : "clique real no ícone")})";

        bool voltou = EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 2000);
        if (simulado)
        {
            bool escolheuItem = m.Fechado?["fechado"] is "AlternarVisibilidade" or "Sair";
            string sit = voltou ? Simulado : escolheuItem ? Falhou : Inconclusivo;
            Registrar("Menu da bandeja — depois de fechar, o foco volta ao aplicativo que estava em uso", sit,
                $"notificação SIMULADA; frente depois do menu: {Quem(Nativo.GetForegroundWindow())}; menu fechado com {m.Fechado?["fechado"] ?? "nada"}"
                + (escolheuItem ? "" : " (cancelado pela ferramenta, sem escolha: nesse caso o Buzzy devolve o foco à área de notificação, NIM_SETFOCUS)"));
        }
        else
        {
            _rel.Linha($"   foco depois do menu da bandeja real: {Quem(Nativo.GetForegroundWindow())} (o clique na barra de tarefas tira o foco do receptor; não avaliado)");
        }
        if (!voltou) AtivarReceptor("depois do menu da bandeja");

        if (Nativo.IsWindowVisible(_hBuzzy))
        {
            _rel.Linha("   o Buzzy continua visível: escondendo pelo menu do personagem para a restauração pela bandeja começar com ele escondido");
            C3EsconderPeloMenuDoPersonagem("C3b, preparação da restauração pela bandeja");
        }
        AmostrarFilhos();
    }

    /// <summary>
    /// Caminho SIMULADO do menu da bandeja: clique SINTÉTICO no ponto opaco do personagem (o Buzzy
    /// passa a ser o processo que recebeu o último input e, pela regra do SetForegroundWindow, pode
    /// dar primeiro plano ao dono do menu) e, logo em seguida, a notificação WM_CONTEXTMENU que a
    /// Shell mandaria, postada à janela de serviço. Depois, como em <see cref="OperarMenu"/>.
    /// </summary>
    private MenuOperado MenuDaBandejaSimulado(long marca, ushort tecla, string nomeDaTecla)
    {
        Nativo.POINT p = PontoOpaco();
        if (Nativo.DonoDoPonto(p.X, p.Y) != _hBuzzy)
            return new MenuOperado(false, null, false, false, $"o ponto opaco {p} não pertence ao Buzzy; nada foi clicado nem postado", Recusado: true);
        try
        {
            _inj.CliqueEsquerdo(p.X, p.Y, _hBuzzy);
        }
        catch (CliqueRecusado e)
        {
            return new MenuOperado(false, null, false, false, e.Message + " Notificação não postada.", Recusado: true);
        }
        // A notificação só sai depois que o clique chegou ao Buzzy (registrado no log dele).
        if (LogDoBuzzy.Esperar(marca, e => e.Chave == "CLIQUE" && e["botao"] == "esquerdo", 1500) is null)
            return new MenuOperado(false, null, false, false, "o clique no personagem não chegou ao Buzzy; notificação não postada");
        // Na versão 4, o botão direito do mouse chega como WM_RBUTTONUP seguido de WM_CONTEXTMENU;
        // sem o primeiro, o Buzzy trata o menu como aberto pelo teclado (e devolve o foco à
        // área de notificação se ele for cancelado).
        PostarNotificacaoDaBandeja(WM_RBUTTONUP);
        PostarNotificacaoDaBandeja(WM_CONTEXTMENU);
        if (LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e["aberto"] == "bandeja", 3000) is null)
            return new MenuOperado(false, null, false, false, "o menu não abriu em 3 s depois da notificação simulada");
        return OperarMenu(marca, tecla, nomeDaTecla);
    }

    /// <summary>
    /// Posta à janela de serviço do Buzzy a notificação da bandeja na versão 4, como a Shell faria:
    /// lParam = MAKELPARAM(evento, id do ícone), wParam = MAKELPARAM(x, y) da âncora.
    /// </summary>
    private void PostarNotificacaoDaBandeja(int evento)
    {
        if (_buzzy is not { HasExited: false } || Nativo.PidDe(_hServico) != (uint)_buzzy.Id)
            throw new FalhaDeVerificacao("a janela de serviço não é mais do Buzzy aberto pela verificação; nada foi postado");
        Nativo.POINT ancora = AncoraDaBandeja();
        if (!Nativo.PostMessage(_hServico, MensagemDaBandeja, Nativo.MakeLParam(ancora.X, ancora.Y), Nativo.MakeLParam(evento, (int)IdDoIcone)))
            throw new FalhaDeVerificacao("PostMessage da notificação simulada da bandeja falhou");
    }

    /// <summary>Âncora da notificação simulada: o centro do ícone, se a Shell informar; senão, perto do canto inferior direito da área útil.</summary>
    private Nativo.POINT AncoraDaBandeja()
        => Nativo.RetanguloDoIcone(_hServico, IdDoIcone) is { } r && r.Largura > 0 && r.Altura > 0
            ? r.Centro
            : new Nativo.POINT(_areaUtilPrincipal.Right - 40, _areaUtilPrincipal.Bottom - 1);

    /// <summary>
    /// Clique real (SINTÉTICO) no ícone da bandeja, só quando for seguro:
    /// (a) o retângulo do ícone é relido agora pela Shell (Shell_NotifyIconGetRect com a janela de
    ///     serviço do Buzzy e o id 1), e de novo pelo injetor imediatamente antes do clique;
    /// (b) o dono do centro (WindowFromPoint + GA_ROOT) é exatamente FindWindow("Shell_TrayWnd").
    /// Nenhum outro teste de acerto autoriza clicar na barra de tarefas. Se o efeito esperado não
    /// aparecer em 3 s, envia Esc só se a janela da frente for do mesmo processo da barra de
    /// tarefas (para fechar o que o clique abriu); senão, nada. Verdadeiro só se o efeito apareceu.
    /// </summary>
    private bool TentarCliqueRealNoIcone(bool direito, long marca, Func<EventoBuzzy, bool> efeito, string oQue, out string motivo)
    {
        nint barra = Nativo.FindWindow("Shell_TrayWnd", null);
        if (barra == 0)
        {
            motivo = "a barra de tarefas (Shell_TrayWnd) não foi encontrada";
            return false;
        }
        if (Nativo.RetanguloDoIcone(_hServico, IdDoIcone) is not { } icone)
        {
            motivo = "a Shell não informou o retângulo do ícone (Shell_NotifyIconGetRect)";
            return false;
        }
        if (icone.Largura <= 0 || icone.Altura <= 0 || icone.Largura > 128 || icone.Altura > 128)
        {
            motivo = $"retângulo do ícone implausível: {icone}";
            return false;
        }
        Nativo.POINT centro = icone.Centro;
        if (Nativo.DonoDoPonto(centro.X, centro.Y) != barra)
        {
            motivo = $"o centro {centro} do retângulo do ícone {icone} não pertence à barra de tarefas (no Windows 11, o ícone costuma ficar na área de ícones ocultos); nada foi clicado";
            return false;
        }

        string? IconeAindaNoLugar() => Nativo.RetanguloDoIcone(_hServico, IdDoIcone) is { } r && r.Contem(centro) ? null : "o ícone mudou de lugar ou sumiu";
        try
        {
            if (direito) _inj.CliqueDireito(centro.X, centro.Y, barra, IconeAindaNoLugar);
            else _inj.CliqueEsquerdo(centro.X, centro.Y, barra, IconeAindaNoLugar);
        }
        catch (CliqueRecusado e)
        {
            motivo = e.Message;
            return false;
        }

        if (LogDoBuzzy.Esperar(marca, efeito, 3000) is not null)
        {
            motivo = "";
            return true;
        }

        uint pidDaBarra = Nativo.PidDe(barra);
        bool esc = pidDaBarra != 0 && _inj.TeclaVirtual(VK_ESCAPE, "Esc para fechar o que o clique abriu na barra de tarefas",
            () => Nativo.PidDe(Nativo.GetForegroundWindow()) == pidDaBarra);
        motivo = $"o clique {(direito ? "direito" : "esquerdo")} real em {centro} não fez o Buzzy {oQue} em 3 s; "
            + (esc ? "Esc enviado (a janela da frente era do processo da barra de tarefas)" : "nenhuma tecla enviada (a janela da frente não era do processo da barra de tarefas)");
        return false;
    }

    /// <summary>
    /// Critério 3 pela bandeja, parte "restaurar": com o Buzzy escondido, a seleção do ícone
    /// (NIN_SELECT) o mostra. Clique esquerdo real no ícone só nas condições seguras; senão, a
    /// notificação SIMULADA.
    /// </summary>
    private void C3MostrarPelaBandeja()
    {
        const string Criterio = "Critério 3 — a bandeja restaura o Buzzy escondido";
        if (Nativo.IsWindowVisible(_hBuzzy))
        {
            Registrar(Criterio, Inconclusivo, "o Buzzy não estava escondido antes da restauração (nem o menu da bandeja nem o do personagem o esconderam); nada foi clicado nem postado");
            _coberturaRestaurarPelaBandeja = $"{Inconclusivo} (pré-condição não atendida)";
            return;
        }
        long marca = LogDoBuzzy.Marca();
        static bool Mostrou(EventoBuzzy e) => e.Chave == "VISIVEL" && e["visivel"] == "sim" && e["motivo"] == "bandeja";

        if (TentarCliqueRealNoIcone(direito: false, marca, Mostrou, "reaparecer", out string semCliqueReal))
        {
            bool visivel = Nativo.IsWindowVisible(_hBuzzy);
            Registrar(Criterio, visivel,
                $"clique esquerdo SINTÉTICO no ícone real da bandeja; NIN_SELECT enviado pela própria Shell; escondido antes=True; visível depois={visivel}");
            _coberturaRestaurarPelaBandeja = $"{(visivel ? Ok : Falhou)} (clique real no ícone)";
        }
        else
        {
            _rel.Linha($"   sem clique real no ícone da bandeja: {semCliqueReal}; usando a notificação SIMULADA");
            if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("antes da restauração simulada pela bandeja");
            marca = LogDoBuzzy.Marca();
            int marcaR = _logReceptor.Contar();
            PostarNotificacaoDaBandeja(NIN_SELECT);
            bool registrou = LogDoBuzzy.Esperar(marca, Mostrou, 3000) is not null;
            Thread.Sleep(300);
            bool visivel = Nativo.IsWindowVisible(_hBuzzy);
            string situacao = registrou && visivel ? Simulado : Falhou;
            Registrar(Criterio, situacao,
                $"notificação NIN_SELECT da bandeja SIMULADA (PostMessage à janela de serviço); escondido antes=True; registrou VISIVEL por bandeja={registrou}; visível depois={visivel}");
            _coberturaRestaurarPelaBandeja = $"{situacao} (notificação SIMULADA)";

            bool frente = Nativo.GetForegroundWindow() == _hReceptor;
            bool desativou = _logReceptor.Desde(marcaR).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
            Registrar("Critério 3 — reaparecer pela bandeja não tira o foco do aplicativo em uso", frente && !desativou ? Simulado : Falhou,
                $"notificação SIMULADA; receptor na frente={frente}; receptor desativado={desativou}");
        }

        Thread.Sleep(300);
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois da bandeja");
        AmostrarFilhos();
    }

    private void C10SairPeloMenu()
    {
        const string Criterio = "Critério 3/10 — \"Sair\" no menu do personagem encerra o processo, sem processos filhos";
        if (!Nativo.IsWindowVisible(_hBuzzy))
        {
            Registrar(Criterio, Inconclusivo, "o Buzzy estava escondido (a restauração pela bandeja falhou): sem personagem na tela, \"Sair\" pelo menu dele não pôde ser exercitado; nada foi clicado");
            return;
        }
        long marca = LogDoBuzzy.Marca();
        AmostrarFilhos();
        MenuOperado m = MenuDoPersonagem(VK_S, "S (Sair)");
        bool saiu = _buzzy!.WaitForExit(5000);
        int codigo = saiu ? _buzzy.ExitCode : -1;
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
        bool removeuIcone = ev.Any(e => e.Chave == "BANDEJA" && e["removido"] == "True");
        bool fim = ev.Any(e => e.Chave == "FIM" && e["codigo"] == "0");
        _buzzyEncerrado = saiu;
        // Amostra depois do encerramento: um filho que sobrasse ao Buzzy apareceria aqui.
        Thread.Sleep(300);
        List<int> filhosDepois = Processos.Filhos(_buzzy.Id, _inicioBuzzy);
        bool voltou = EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 2000);

        string situacao = m.Recusado ? Inconclusivo
            : m.Abriu && m.TeclaEnviada && m.Fechado?["fechado"] == "Sair" && saiu && codigo == 0 && removeuIcone && fim && _filhos.Count == 0 && filhosDepois.Count == 0 ? Ok
            : Falhou;
        Registrar(Criterio, situacao,
            $"{DescreverMenu(m)}; processo saiu={saiu} código={codigo}; ícone removido={removeuIcone}; FIM registrado={fim}; "
            + $"processos filhos criados depois do início do Buzzy: vistos durante a execução={_filhos.Count}, depois do encerramento={filhosDepois.Count}");
        Registrar("Menu (sair) — depois de fechar, o foco volta ao aplicativo que estava em uso", voltou,
            $"frente depois: {Quem(Nativo.GetForegroundWindow())}");
        if (!voltou) AtivarReceptor("depois de sair");
    }

    // ------------------------------------------------------------------ texto, foco e limpeza

    private void Digitar(string marcador)
    {
        if (!marcador.All(JanelaReceptor.Permitido))
            throw new ArgumentException($"o marcador '{marcador}' tem caractere fora de [A-Za-z0-9;-], que o receptor mascara", nameof(marcador));
        int marca = _logReceptor.Contar();
        // A condição é conferida pelo injetor imediatamente antes do envio.
        if (!_inj.Digitar(marcador, () => Nativo.GetForegroundWindow() == _hReceptor))
        {
            Registrar($"texto '{marcador}'", Falhou, $"não digitado: a janela da frente era {Quem(Nativo.GetForegroundWindow())}");
            throw new FalhaDeVerificacao($"Receptor fora do primeiro plano antes de '{marcador}'; nada foi digitado.");
        }
        _textoEsperado.Append(marcador);
        EsperarAte(() => TextoDesde(marca).Texto.Length >= marcador.Length, 3000);
        Thread.Sleep(150);
        var (texto, comFoco, n) = TextoDesde(marca);
        Registrar($"texto '{marcador}' chegou ao aplicativo em uso", texto == marcador && comFoco,
            $"recebido '{texto}' em {n} eventos; todos com janela ativa e foco na caixa={comFoco}");
    }

    /// <summary>Texto recebido pelo receptor desde a marca, já mascarado por ele ([A-Za-z0-9;-]; o resto vira '?').</summary>
    private (string Texto, bool ComFoco, int Eventos) TextoDesde(int marca)
    {
        var sb = new StringBuilder();
        bool todos = true;
        int n = 0;
        foreach (string l in _logReceptor.Desde(marca))
        {
            Match m = ReTexto.Match(l);
            if (!m.Success) continue;
            n++;
            sb.Append(m.Groups["t"].Value);
            todos &= m.Groups["a"].Value == "True" && m.Groups["f"].Value == "True";
        }
        return (sb.ToString(), todos, n);
    }

    private void FecharPeriodoDeFoco(string nome)
    {
        List<string> ls = _logReceptor.Desde(_marcaFoco);
        int desativacoes = ls.Count(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        int perdas = ls.Count(l => l.Contains("RECEPTOR|FOCO_TECLADO|nao", StringComparison.Ordinal));
        Registrar($"foco mantido no aplicativo em uso ({nome})", desativacoes == 0 && perdas == 0,
            $"desativações={desativacoes} perdas de foco de teclado={perdas} em {ls.Count} linhas do receptor");
    }

    /// <summary>Filhos do Buzzy criados depois do início dele (Process.StartTime), acumulados durante a execução.</summary>
    private void AmostrarFilhos()
    {
        if (_buzzy is { HasExited: false }) foreach (int f in Processos.Filhos(_buzzy.Id, _inicioBuzzy)) _filhos.Add(f);
    }

    /// <summary>
    /// Solta botões e teclas pendentes, fecha os processos que a verificação abriu (WM_CLOSE; à
    /// força só se não fecharem) e restaura o cursor. Depois de interferência (ou Ctrl+C), o
    /// cursor NÃO é reposicionado: o usuário assumiu o mouse.
    /// </summary>
    private void Limpeza(Nativo.POINT cursorOriginal)
    {
        List<string> pendencias = _inj.SoltarPendencias();
        if (pendencias.Count > 0) _rel.Linha("   limpeza de input: " + string.Join("; ", pendencias));

        string buzzy = "não aberto";
        bool buzzyOk = true;
        if (_buzzy is not null)
        {
            if (!_buzzy.HasExited)
            {
                if (_hBuzzy != 0) Nativo.PostMessage(_hBuzzy, Nativo.WM_CLOSE, 0, 0);
                if (_buzzy.WaitForExit(_hBuzzy != 0 ? 5000 : 1000))
                    buzzy = $"encerrado por WM_CLOSE na limpeza (código {_buzzy.ExitCode})";
                else
                {
                    buzzy = Processos.EncerrarAForca(_buzzy, "Buzzy aberto pela verificação");
                    buzzyOk = false;
                }
            }
            else buzzy = _buzzyEncerrado ? $"encerrado pelo menu (código {_buzzy.ExitCode})" : $"encerrou (código {_buzzy.ExitCode})";
        }

        string receptor = "não aberto";
        bool receptorOk = true;
        if (_receptor is not null)
        {
            if (_receptor.HasExited) receptor = $"já tinha encerrado (código {_receptor.ExitCode})";
            else
            {
                if (_hReceptor != 0) Nativo.PostMessage(_hReceptor, Nativo.WM_CLOSE, 0, 0);
                if (_receptor.WaitForExit(_hReceptor != 0 ? 5000 : 1000)) receptor = "encerrado por WM_CLOSE";
                else
                {
                    receptor = Processos.EncerrarAForca(_receptor, "receptor");
                    receptorOk = false;
                }
            }
        }

        string cursor;
        bool cursorOk;
        if (_houveInterferencia || Programa.Cancelado)
        {
            cursor = $"não reposicionado de propósito: o usuário assumiu o mouse (cursor em {Nativo.Cursor()}, original {cursorOriginal})";
            cursorOk = true;
        }
        else
        {
            Nativo.SetCursorPos(cursorOriginal.X, cursorOriginal.Y);
            Thread.Sleep(80);
            Nativo.POINT c = Nativo.Cursor();
            cursorOk = Math.Abs(c.X - cursorOriginal.X) <= 1 && Math.Abs(c.Y - cursorOriginal.Y) <= 1;
            cursor = $"restaurado em {c} (original {cursorOriginal})";
        }
        Registrar("limpeza: processos de teste encerrados sem força e cursor restaurado (salvo depois de interferência)", buzzyOk && receptorOk && cursorOk,
            $"Buzzy: {buzzy}; receptor: {receptor}; cursor {cursor}");
    }

    private void VerificarReceptorAoFinal()
    {
        if (_receptor is null) return;
        _logReceptor.EsperarLinha(_marcaSessaoReceptor, "RECEPTOR|CONTEUDO_FINAL|", 3000);
        var (texto, comFoco, n) = TextoDesde(_marcaSessaoReceptor);
        string esperado = _textoEsperado.ToString();
        string? linha = _logReceptor.Desde(_marcaSessaoReceptor).LastOrDefault(l => l.Contains("RECEPTOR|CONTEUDO_FINAL|", StringComparison.Ordinal));
        string? conteudo = linha?[(linha.IndexOf("RECEPTOR|CONTEUDO_FINAL|", StringComparison.Ordinal) + 24)..];
        Registrar("aplicativo em uso: recebeu todo o texto, na ordem, só com foco", texto == esperado && comFoco && conteudo == esperado,
            $"esperado '{esperado}'; recebido '{texto}' em {n} eventos; conteúdo final '{conteudo}' (o receptor mascara como '?' o que estiver fora de [A-Za-z0-9;-])");
    }

    /// <summary>O que o critério 3 cobriu pela bandeja nesta execução, e o que ficou sem exercitar.</summary>
    private void RegistrarCobertura()
    {
        _rel.Linha("   Cobertura do critério 3 pela bandeja:");
        _rel.Linha($"     - \"Esconder\" pelo menu da bandeja: {_coberturaEsconderPelaBandeja}");
        _rel.Linha($"     - restaurar pela seleção do ícone (NIN_SELECT): {_coberturaRestaurarPelaBandeja}");
        _rel.Linha("     - \"Mostrar\" pelo item do menu da bandeja: não exercitado (na simulação, com o Buzzy escondido, não há personagem para o clique que dá ao Buzzy o direito de primeiro plano)");
        _rel.Linha("     - \"Sair\" pelo menu da bandeja: não exercitado; \"Sair\" foi exercitado pelo menu do personagem, que é o mesmo menu (MenuNativo.Mostrar)");
        _naoExercitados.Add("\"Mostrar\" pelo item do menu da bandeja");
        _naoExercitados.Add("\"Sair\" pelo menu da bandeja (coberto só pelo menu do personagem, que é o mesmo menu)");
    }

    private Sumario Resumir()
    {
        int ok = _resultados.Count(r => r.Situacao == Ok);
        int na = _resultados.Count(r => r.Situacao == NaoAplicavel);
        List<string> simulados = [.. _resultados.Where(r => r.Situacao == Simulado).Select(r => r.Criterio)];
        int falhas = _resultados.Count - ok - na - simulados.Count;
        _rel.Linha($"   {ok} OK, {na} N/A, {simulados.Count} SIMULADO, {falhas} falha(s) (FALHOU, INCONCLUSIVO ou INVÁLIDA). Maior deriva do cursor tolerada: {_inj.MaiorDeriva} px.");
        return new Sumario(ok, na, simulados, falhas, _naoExercitados);
    }

    private EventoBuzzy Esperar(Func<EventoBuzzy, bool> condicao, int limiteMs, string oQue)
    {
        EventoBuzzy? e = LogDoBuzzy.Esperar(_inicioLogBuzzy, condicao, limiteMs, () => _buzzy is { HasExited: true });
        if (e is not null) return e;
        if (_buzzy is { HasExited: true })
            throw new FalhaDeVerificacao($"O Buzzy encerrou (código {_buzzy.ExitCode}: {DescreverCodigo(_buzzy.ExitCode)}) antes de registrar: {oQue}.");
        throw new FalhaDeVerificacao($"O Buzzy não registrou: {oQue}.");
    }

    /// <summary>Significado dos códigos de saída do Buzzy.exe (src/Buzzy.App/CodigosDeSaida.cs).</summary>
    private static string DescreverCodigo(int codigo) => codigo switch
    {
        -1 => "não saiu",
        0 => "saída normal; logo na partida, quer dizer que outra instância já estava aberta",
        3 => "topologia dos monitores ilegível na partida",
        5 => "recusou rodar elevado",
        6 => "instância única indisponível, ou a primeira instância não respondeu",
        _ => "código não documentado",
    };

    private void Registrar(string criterio, bool ok, string detalhe) => Registrar(criterio, ok ? Ok : Falhou, detalhe);

    private void Registrar(string criterio, string situacao, string detalhe)
    {
        _resultados.Add(new Resultado(_prefixo + criterio, situacao, detalhe));
        _rel.Linha($"   [{situacao}] {_prefixo}{criterio}");
        _rel.Linha($"          {detalhe}");
    }

    private string Quem(nint h)
    {
        if (h == 0) return "nenhuma";
        if (h == _hReceptor) return "receptor";
        if (h == _hBuzzy) return "BUZZY";
        if (_buzzy is { HasExited: false } && Nativo.PidDe(h) == (uint)_buzzy.Id) return "outra janela do Buzzy";
        return "outra janela (não identificada de propósito)";
    }

    private static bool EsperarAte(Func<bool> condicao, int limiteMs)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (!condicao())
        {
            if (DateTime.UtcNow > fim) return false;
            Thread.Sleep(50);
        }
        return true;
    }
}
