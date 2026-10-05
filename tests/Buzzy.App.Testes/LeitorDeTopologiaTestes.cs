using System.Text.RegularExpressions;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Leitura da topologia com a chave estável (Fase 5, passo P6; DEC-030). Sem hardware: a conversão de cada monitor
/// enumerado, em que uma falha torna a leitura inteira incoerente, e a montagem da topologia com as chaves e o cache.
/// Com a máquina, só leitura, sem mudar nada no sistema: a topologia real tem chaves opacas, únicas e estáveis entre
/// leituras.
/// </summary>
internal sealed class LeitorDeTopologiaTestes
{
    private const string CaminhoA = @"\\?\DISPLAY#AAA0001#1&11111111&0&UID1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string CaminhoB = @"\\?\DISPLAY#BBB0002#1&22222222&0&UID2#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private static readonly MonitorEnumerado Principal =
        new(@"\\.\DISPLAY1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true);

    private static readonly MonitorEnumerado AEsquerda =
        new(@"\\.\DISPLAY2", new RetanguloPx(-1920, 0, 0, 1080), new RetanguloPx(-1920, 0, 0, 1080), 144, false);

    private static readonly IReadOnlyDictionary<string, ChaveConhecida> SemCache = new Dictionary<string, ChaveConhecida>();

    // ------------------------------------------------------------------ cada monitor enumerado (crítica da Fase 5, L3)

    [Teste]
    public void Descrever_MonitorLidoPorInteiro()
    {
        Win32.MONITORINFOEX info = Info(@"\\.\DISPLAY2", -1920, 0, 0, 1080, trabalho: (-1920, 0, 0, 1040), principal: false);
        MonitorEnumerado m = Afirmar.NaoNulo(LeitorDeTopologia.Descrever(true, 0, info, 0, 144, out string? falha));
        Afirmar.Nulo(falha);
        Afirmar.Igual(new MonitorEnumerado(@"\\.\DISPLAY2", new RetanguloPx(-1920, 0, 0, 1080), new RetanguloPx(-1920, 0, 0, 1040), 144, false), m);
        Afirmar.Verdadeiro(Afirmar.NaoNulo(LeitorDeTopologia.Descrever(true, 0, Info(@"\\.\DISPLAY1", 0, 0, 1920, 1080, principal: true), 0, 96, out _)).Principal);
    }

    [Teste]
    public void Descrever_FalhaDaInformacaoDoMonitor_LeituraIncoerente()
    {
        // Antes, o monitor era pulado e a topologia saía sem ele: com "monitor sumido não volta", o personagem
        // migraria de monitor para sempre por uma falha passageira.
        Afirmar.Nulo(LeitorDeTopologia.Descrever(false, 1461, Info(@"\\.\DISPLAY1", 0, 0, 1920, 1080, principal: true), 0, 96, out string? falha));
        Afirmar.Contem("GetMonitorInfo", falha);
        Afirmar.Contem("1461", falha);
    }

    [Teste]
    public void Descrever_FalhaDoDpi_LeituraIncoerente()
    {
        // Antes, uma falha virava 96 DPI sem aviso: o tamanho do personagem e a escala do monitor sairiam errados.
        Win32.MONITORINFOEX info = Info(@"\\.\DISPLAY1", 0, 0, 1920, 1080, principal: true);
        Afirmar.Nulo(LeitorDeTopologia.Descrever(true, 0, info, unchecked((int)0x80070057), 0, out string? falha), "HRESULT de erro");
        Afirmar.Contem("GetDpiForMonitor", falha);
        Afirmar.Contem("0x80070057", falha);
        Afirmar.Nulo(LeitorDeTopologia.Descrever(true, 0, info, 0, 0, out falha), "sucesso com DPI zero");
        Afirmar.Contem("GetDpiForMonitor", falha);
    }

    [Teste]
    public void Descrever_MonitorSemNomeGdi_LeituraIncoerente()
    {
        // Sem o nome GDI não há como ligar o monitor ao caminho do dispositivo nem dar a reserva; antes, a chave vazia
        // já tornava a topologia incoerente.
        Afirmar.Nulo(LeitorDeTopologia.Descrever(true, 0, Info("", 0, 0, 1920, 1080, principal: true), 0, 96, out string? falha));
        Afirmar.Contem("nome GDI", falha);
    }

    // ------------------------------------------------------------------ leitura parcial, o último recurso (revisão do bloco P6-P9)

    [Teste]
    public void Juntar_UmMonitorQueFalha_TornaALeituraIncoerente_SoNaLeituraParcialEleFicaDeFora()
    {
        // L3: uma falha de um monitor torna a leitura inteira incoerente (a anterior continua valendo e a releitura tenta de
        // novo). Revisão de correção do bloco P6-P9, achado 7: com a falha persistente, o Buzzy não partia (5 tentativas) e,
        // em execução, nenhuma releitura saía: com o monitor do personagem desconectado, ele ficava fora da tela. Depois das
        // novas tentativas, a leitura parcial deixa de fora só o monitor que falhou, e conta.
        (MonitorEnumerado? Monitor, string? Falha)[] lidos =
        [
            (Principal, null),
            (null, "GetDpiForMonitor falhou (0x80070057, dpi 0)"),
            (AEsquerda, null),
        ];
        Afirmar.Nulo(LeitorDeTopologia.Juntar(lidos, parcial: false, out int ignorados, out string? erro), "estrita: incoerente");
        Afirmar.Igual((0, "GetDpiForMonitor falhou (0x80070057, dpi 0)"), (ignorados, erro));

        List<MonitorEnumerado> parcial = Afirmar.NaoNulo(LeitorDeTopologia.Juntar(lidos, parcial: true, out ignorados, out erro), "parcial");
        Afirmar.Sequencia([Principal, AEsquerda], parcial, "os que foram lidos por inteiro, na ordem");
        Afirmar.Igual((1, (string?)"GetDpiForMonitor falhou (0x80070057, dpi 0)"), (ignorados, erro), "o que ficou de fora, e por quê");

        List<MonitorEnumerado> todos = Afirmar.NaoNulo(LeitorDeTopologia.Juntar([(Principal, null), (AEsquerda, null)], parcial: true, out ignorados, out erro));
        Afirmar.Igual((2, 0, (string?)null), (todos.Count, ignorados, erro), "sem falha, a parcial é igual à estrita");
    }

    [Teste]
    public void Montar_LeituraParcial_LevaOsIgnorados_ENaoTrocaOCache()
    {
        // Com um monitor de fora, o cache não é trocado: a chave dele continua lá para quando ele voltar a ser lido.
        string chaveA = ChavesDeMonitor.DoCaminho(CaminhoA), chaveB = ChavesDeMonitor.DoCaminho(CaminhoB);
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\\.\DISPLAY1"] = new(chaveA, Principal.Tela),
            [@"\\.\DISPLAY2"] = new(chaveB, AEsquerda.Tela),
        };
        var consulta = new ConsultaDeVideo([new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA), new AlvoAtivo(@"\\.\DISPLAY2", CaminhoB)], Erro: null, CaminhosSemNome: 0);
        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal], consulta, cache, out var cacheDepois, out string? erro, ignorados: 1, motivoDoIgnorado: "monitor sem nome GDI"), erro);
        Afirmar.Sequencia([chaveA], leitura.Topologia.Monitores.Select(m => m.Chave), "só o que foi lido");
        Afirmar.Igual((1, (string?)"monitor sem nome GDI"), (leitura.MonitoresIgnorados, leitura.MotivoDoIgnorado));
        Afirmar.Verdadeiro(ReferenceEquals(cache, cacheDepois), "a leitura parcial não troca o cache");

        LeituraDaTopologia inteira = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal, AEsquerda], consulta, cache, out _, out erro), erro);
        Afirmar.Igual((0, (string?)null), (inteira.MonitoresIgnorados, inteira.MotivoDoIgnorado), "a estrita não ignora nada");
    }

    // ------------------------------------------------------------------ montagem da topologia com as chaves

    [Teste]
    public void Montar_ConsultaBoa_ChavesPeloCaminhoENomeGdiAoLado()
    {
        var consulta = new ConsultaDeVideo([new AlvoAtivo(@"\\.\DISPLAY2", CaminhoB), new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA)], Erro: null, CaminhosSemNome: 0);
        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal, AEsquerda], consulta, SemCache, out var cacheDepois, out string? erro), erro);

        string chaveA = ChavesDeMonitor.DoCaminho(CaminhoA), chaveB = ChavesDeMonitor.DoCaminho(CaminhoB);
        Afirmar.Sequencia([chaveA, chaveB], leitura.Topologia.Monitores.Select(m => m.Chave), "na ordem do Windows");
        Afirmar.Igual(new MonitorDoDesktop(chaveB, AEsquerda.Tela, AEsquerda.AreaUtil, 144, false), leitura.Topologia.Monitores[1], "o resto do monitor como enumerado");
        Afirmar.Igual(chaveA, leitura.Topologia.Principal.Chave);
        Afirmar.Igual(@"\\.\DISPLAY1", leitura.NomeGdi(chaveA));
        Afirmar.Igual(@"\\.\DISPLAY2", leitura.NomeGdi(chaveB));
        Afirmar.Igual("-", leitura.NomeGdi("mon:0000000000000000"), "chave de outra leitura");
        Afirmar.Sequencia([OrigemDaChave.Caminho, OrigemDaChave.Caminho], leitura.Chaves.Select(c => c.Origem));
        Afirmar.Igual((0, 0), (leitura.ChavesDoCache, leitura.ChavesDeReserva));
        Afirmar.Nulo(leitura.ErroDaConsulta);

        Afirmar.Igual(2, cacheDepois.Count, "o cache passa a ser o desta leitura");
        Afirmar.Igual(new ChaveConhecida(chaveB, AEsquerda.Tela), cacheDepois[@"\\.\DISPLAY2"]);
    }

    [Teste]
    public void Montar_ConsultaQueFalhou_UsaOCacheENaoOTroca()
    {
        // Sessão remota ou bloqueada pode negar a consulta (DEC-030): a leitura sai mesmo assim, com as chaves da
        // última consulta boa para o mesmo nome GDI e a mesma tela, e o cache fica como estava.
        string chaveA = ChavesDeMonitor.DoCaminho(CaminhoA);
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY1"] = new(chaveA, Principal.Tela) };
        var negada = new ConsultaDeVideo([], Erro: "QueryDisplayConfig 5", CaminhosSemNome: 0);

        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal, AEsquerda], negada, cache, out var cacheDepois, out string? erro), erro);
        Afirmar.Sequencia([chaveA, @"gdi:\\.\DISPLAY2"], leitura.Topologia.Monitores.Select(m => m.Chave));
        Afirmar.Igual((1, 1), (leitura.ChavesDoCache, leitura.ChavesDeReserva));
        Afirmar.Igual("QueryDisplayConfig 5", leitura.ErroDaConsulta);
        Afirmar.Verdadeiro(ReferenceEquals(cache, cacheDepois), "a consulta que falhou não troca o cache");
    }

    [Teste]
    public void Montar_CaminhoSemNome_SoAqueleMonitorVaiAoCache_EContinuaNele()
    {
        // A consulta deu certo, mas o caminho de um alvo não pôde ser lido: os outros continuam pelo caminho, e esse fica
        // com a chave do cache. O cache novo guarda as duas: a próxima consulta negada ainda acha a chave dele.
        string chaveA = ChavesDeMonitor.DoCaminho(CaminhoA), chaveB = ChavesDeMonitor.DoCaminho(CaminhoB);
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY2"] = new(chaveB, AEsquerda.Tela) };
        var consulta = new ConsultaDeVideo([new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA)], Erro: null, CaminhosSemNome: 1);

        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal, AEsquerda], consulta, cache, out var cacheDepois, out string? erro), erro);
        Afirmar.Sequencia([chaveA, chaveB], leitura.Topologia.Monitores.Select(m => m.Chave));
        Afirmar.Sequencia([OrigemDaChave.Caminho, OrigemDaChave.Cache], leitura.Chaves.Select(c => c.Origem));
        Afirmar.Igual(1, leitura.CaminhosSemNome);
        Afirmar.Sequencia([@"\\.\DISPLAY1", @"\\.\DISPLAY2"], cacheDepois.Keys.Order(StringComparer.Ordinal), "o cache novo guarda as duas");

        var negada = new ConsultaDeVideo([], Erro: "QueryDisplayConfig 5", CaminhosSemNome: 0);
        LeituraDaTopologia depois = Afirmar.NaoNulo(LeitorDeTopologia.Montar([Principal, AEsquerda], negada, cacheDepois, out _, out erro), erro);
        Afirmar.Sequencia([chaveA, chaveB], depois.Topologia.Monitores.Select(m => m.Chave), "a consulta negada logo depois acha as duas chaves");
    }

    [Teste]
    public void Montar_ConsultaNegadaNaTrocaDePrincipal_OPersonagemFicaNoMesmoMonitorFisico()
    {
        // Revisão de correção do bloco P6-P9, achado 1 (teste R5): S2, a consulta da configuração de vídeo é negada (sessão
        // bloqueada ou remota) e o DISPLAY2 vira o principal. As duas telas andam 1920 px. O personagem, a 85% do DISPLAY1,
        // continua no DISPLAY1 físico, que foi para (1920,0)-(3840,1080): (3552,1032). Exigir a mesma tela no cache punha
        // as duas chaves na reserva, e o apelido por retângulo do núcleo o deixava em (1632,1032), já no DISPLAY2.
        var boa = new ConsultaDeVideo([new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA), new AlvoAtivo(@"\\.\DISPLAY2", CaminhoB)], Erro: null, CaminhosSemNome: 0);
        LeituraDaTopologia antes = Afirmar.NaoNulo(LeitorDeTopologia.Montar(
            [Enumerado(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true), Enumerado(@"\\.\DISPLAY2", -1920, 0, 0, 1080, false)], boa, SemCache, out var cache, out string? erro), erro);
        var negada = new ConsultaDeVideo([], Erro: "QueryDisplayConfig 5", CaminhosSemNome: 0);
        LeituraDaTopologia depois = Afirmar.NaoNulo(LeitorDeTopologia.Montar(
            [Enumerado(@"\\.\DISPLAY1", 1920, 0, 3840, 1080, false), Enumerado(@"\\.\DISPLAY2", 0, 0, 1920, 1080, true)], negada, cache, out _, out erro), erro);
        Afirmar.Sequencia(antes.Topologia.Monitores.Select(m => m.Chave), depois.Topologia.Monitores.Select(m => m.Chave), "as mesmas chaves, pelo cache");

        EstadoDoNucleo s = CarregarParado(antes.Topologia);
        s = Maquina.Aplicar(s, new TopologyChanged(depois.Topologia), Configuracao).Estado;
        Afirmar.Igual((@"\\.\DISPLAY1", new PontoPx(3552, 1032)), (depois.NomeGdi(s.Lugar!.Monitor.Chave), s.Lugar.Ancora), "monitor físico e âncora");
    }

    [Teste]
    public void Montar_ConsultaNegadaComOPrincipalDesconectado_OSobreviventeMedidoNasCoordenadasAntigas()
    {
        // [2][1*][3], a consulta negada, e o 1 sai: o 2 assume e vai para (0,0), e o 3 vai para 3840. Com as chaves do 2 e do
        // 3 seguras pelo cache, o núcleo acha o sobrevivente medido nas coordenadas antigas (R15c): o 3, a 85% dele.
        const string CaminhoC = @"\\?\DISPLAY#CCC0003#1&33333333&0&UID3#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var boa = new ConsultaDeVideo(
            [new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA), new AlvoAtivo(@"\\.\DISPLAY2", CaminhoB), new AlvoAtivo(@"\\.\DISPLAY3", CaminhoC)], Erro: null, CaminhosSemNome: 0);
        LeituraDaTopologia antes = Afirmar.NaoNulo(LeitorDeTopologia.Montar(
            [Enumerado(@"\\.\DISPLAY2", -1920, 0, 0, 1080, false), Enumerado(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true), Enumerado(@"\\.\DISPLAY3", 1920, 0, 3840, 1080, false)],
            boa, SemCache, out var cache, out string? erro), erro);
        var negada = new ConsultaDeVideo([], Erro: "QueryDisplayConfig 5", CaminhosSemNome: 0);
        LeituraDaTopologia depois = Afirmar.NaoNulo(LeitorDeTopologia.Montar(
            [Enumerado(@"\\.\DISPLAY2", 0, 0, 1920, 1080, true), Enumerado(@"\\.\DISPLAY3", 3840, 0, 5760, 1080, false)], negada, cache, out _, out erro), erro);

        EstadoDoNucleo s = CarregarParado(antes.Topologia);
        s = Maquina.Aplicar(s, new TopologyChanged(depois.Topologia), Configuracao).Estado;
        Afirmar.Igual((@"\\.\DISPLAY3", new PontoPx(5472, 1032)), (depois.NomeGdi(s.Lugar!.Monitor.Chave), s.Lugar.Ancora), "no 3, nas coordenadas novas");
    }

    [Teste]
    public void Montar_TopologiaIncoerente_NulaSemTrocarOCache()
    {
        var consulta = new ConsultaDeVideo([new AlvoAtivo(@"\\.\DISPLAY1", CaminhoA)], Erro: null, CaminhosSemNome: 0);
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase);

        Afirmar.Nulo(LeitorDeTopologia.Montar([AEsquerda], consulta, cache, out var depoisSemPrincipal, out string? erro), "sem monitor principal");
        Afirmar.Contem("principal", erro);
        Afirmar.Verdadeiro(ReferenceEquals(cache, depoisSemPrincipal), "leitura incoerente não troca o cache");

        Afirmar.Nulo(LeitorDeTopologia.Montar([Principal, Principal with { Principal = false }], consulta, cache, out var depoisRepetido, out erro), "nome GDI repetido");
        Afirmar.Verdadeiro(ReferenceEquals(cache, depoisRepetido));
        Afirmar.Nulo(LeitorDeTopologia.Montar([], consulta, cache, out _, out erro), "nenhum monitor");
    }

    // ------------------------------------------------------------------ a máquina (só leitura)

    [Teste]
    public void LeituraReal_ChavesOpacasUnicasEEstaveisEntreLeituras()
    {
        // Só lê: enumera os monitores e consulta a configuração de vídeo, sem mudar nada no sistema e sem abrir janela.
        LeituraDaTopologia primeira = Afirmar.NaoNulo(LeitorDeTopologia.LerDetalhado(out string? erro), erro);
        LeituraDaTopologia segunda = Afirmar.NaoNulo(LeitorDeTopologia.LerDetalhado(out erro), erro);
        Topologia t = primeira.Topologia;

        Afirmar.Sequencia(t.Monitores.Select(m => m.Chave), segunda.Topologia.Monitores.Select(m => m.Chave), "a mesma chave nas duas leituras");
        Afirmar.Igual(t.ImpressaoDigital, segunda.Topologia.ImpressaoDigital, "a mesma topologia nas duas leituras");
        Afirmar.Igual(t.ImpressaoDigital, Afirmar.NaoNulo(LeitorDeTopologia.Ler(out erro), erro).ImpressaoDigital, "Ler devolve a topologia de LerDetalhado");
        Afirmar.Igual(t.Monitores.Count, t.Monitores.Select(m => m.Chave).Distinct(StringComparer.Ordinal).Count(), "chaves únicas");
        foreach (MonitorDoDesktop m in t.Monitores)
            Afirmar.Verdadeiro(Regex.IsMatch(primeira.NomeGdi(m.Chave), @"^\\\\\.\\DISPLAY\d+$"), $"cada chave tem o nome GDI ao lado: {primeira.NomeGdi(m.Chave)}");

        if (primeira.ErroDaConsulta is null)
        {
            // A consulta deu certo: todas as chaves são o resumo opaco do caminho, iguais às calculadas de novo aqui.
            foreach (MonitorDoDesktop m in t.Monitores)
                Afirmar.Verdadeiro(Regex.IsMatch(m.Chave, "^mon:[0-9a-f]{16}$"), $"chave estável e opaca: {m.Chave}");
            IReadOnlyDictionary<string, string> mapa = ChavesDeMonitor.Mapear(ConfiguracaoDeVideo.Consultar().Alvos);
            foreach (MonitorDoDesktop m in t.Monitores)
                Afirmar.Igual(mapa[primeira.NomeGdi(m.Chave)], m.Chave, $"a chave de {primeira.NomeGdi(m.Chave)} vem do caminho do dispositivo dele");
            Afirmar.Igual((0, 0), (primeira.ChavesDoCache, primeira.ChavesDeReserva), "nenhuma chave do cache nem da reserva");
        }
        else
        {
            // Só uma recusa documentada do Windows vale aqui (sessão remota ou sem acesso ao console): um erro de
            // parâmetro seria defeito nas estruturas, e as chaves cairiam todas na reserva sem ninguém notar.
            Afirmar.Verdadeiro(primeira.ErroDaConsulta.EndsWith($" {Win32.ERROR_ACCESS_DENIED}", StringComparison.Ordinal),
                $"a consulta da configuração de vídeo só pode falhar por acesso negado: {primeira.ErroDaConsulta}");
            Console.WriteLine($"         consulta negada nesta sessão ({primeira.ErroDaConsulta}): chaves do cache ou da reserva");
        }
        Console.WriteLine($"         {t.Monitores.Count} monitor(es): {string.Join(", ", t.Monitores.Select(m => $"{m.Chave} ({primeira.NomeGdi(m.Chave)})"))}");
    }

    // ------------------------------------------------------------------ apoio

    private static readonly ConfiguracaoDoNucleo Configuracao = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    /// <summary>Um monitor de 1920 px de largura a 96 DPI, com a barra de 48 px embaixo.</summary>
    private static MonitorEnumerado Enumerado(string nome, int esquerda, int topo, int direita, int baixo, bool principal)
        => new(nome, new RetanguloPx(esquerda, topo, direita, baixo), new RetanguloPx(esquerda, topo, direita, baixo - 48), 96, principal);

    /// <summary>O núcleo carregado na topologia, sem posição salva (a 85% do principal, no chão) e parado.</summary>
    private static EstadoDoNucleo CarregarParado(Topologia topologia)
    {
        EstadoDoNucleo s = Maquina.Aplicar(EstadoDoNucleo.Inicial(7), new Loaded(topologia, null, Preferencias.Padrao), Configuracao).Estado;
        Afirmar.Igual((Estado.Idle, new PontoPx(1632, 1032)), (s.Estado, s.Lugar!.Ancora), "premissa: parado a 85% do principal");
        return s;
    }

    private static Win32.MONITORINFOEX Info(string nome, int esquerda, int topo, int direita, int baixo, (int E, int T, int D, int B)? trabalho = null, bool principal = false)
    {
        (int e, int t, int d, int b) = trabalho ?? (esquerda, topo, direita, baixo);
        return new Win32.MONITORINFOEX
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFOEX>(),
            rcMonitor = new Win32.RECT { Left = esquerda, Top = topo, Right = direita, Bottom = baixo },
            rcWork = new Win32.RECT { Left = e, Top = t, Right = d, Bottom = b },
            dwFlags = principal ? Win32.MONITORINFOF_PRIMARY : 0,
            szDevice = nome,
        };
    }
}
