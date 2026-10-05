using System.Globalization;
using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes;

/// <summary>Validação e consultas da topologia do desktop virtual (Topologia.cs).</summary>
internal static class TopologiaTestes
{
    private static readonly MonitorDoDesktop Principal1 = Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96);
    private static readonly MonitorDoDesktop Secundario2 = Secundario(Display2, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96);

    // ---------------------------------------------------------------- validação

    [Teste]
    public static void TopologiaSemMonitoresLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([]), "lista vazia");
        Afirmar.Lanca<ArgumentNullException>(() => _ = new Topologia(null!), "lista nula");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1, null!]), "monitor nulo");
    }

    [Teste]
    public static void TopologiaPrecisaDeExatamenteUmPrincipal()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Secundario2]), "nenhum principal");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1, Secundario2 with { Principal = true }]), "dois principais");
    }

    [Teste]
    public static void ChaveRepetidaOuVaziaLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1, Secundario2 with { Chave = Display1 }]), "chave repetida");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { Chave = "" }]), "chave vazia");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { Chave = null! }]), "chave nula");
    }

    [Teste]
    public static void TelaVaziaLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { Tela = Ret(0, 0, 0, 1080) }]), "largura zero");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { Tela = Ret(0, 0, 1920, -5) }]), "altura negativa");
    }

    [Teste]
    public static void AreaUtilVaziaOuForaDaTelaLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { AreaUtil = Ret(0, 0, 1920, 1081) }]), "um pixel abaixo da tela");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { AreaUtil = Ret(-1, 0, 1920, 1032) }]), "um pixel à esquerda da tela");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1, Secundario2 with { AreaUtil = Ret(0, 0, 1920, 1032) }]), "área útil na tela de outro monitor");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { AreaUtil = Ret(100, 100, 100, 100) }]), "área útil vazia");
    }

    [Teste]
    public static void DpiInvalidoLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1 with { Dpi = 0 }]), "DPI zero");
        Afirmar.Lanca<ArgumentException>(() => _ = new Topologia([Principal1, Secundario2 with { Dpi = -96 }]), "DPI negativo");
    }

    [Teste]
    public static void TopologiasDeExemploSaoCoerentes()
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string nome, Topologia t) in Todas)
        {
            Afirmar.Verdadeiro(nomes.Add(nome), $"{nome}: nome repetido");
            Afirmar.Igual(new PontoPx(0, 0), new PontoPx(t.Principal.Tela.Esquerda, t.Principal.Tela.Topo), $"{nome}: origem do principal");
            for (int i = 0; i < t.Monitores.Count; i++)
                for (int j = i + 1; j < t.Monitores.Count; j++)
                    Afirmar.Falso(t.Monitores[i].Tela.Intersecta(t.Monitores[j].Tela), $"{nome}: {t.Monitores[i].Chave} sobrepõe {t.Monitores[j].Chave}");
        }
        Afirmar.Igual(17, nomes.Count, "quantidade de exemplos");
    }

    // ---------------------------------------------------------------- lista e principal

    [Teste]
    public static void MonitoresFicamNaOrdemRecebida()
    {
        Afirmar.Sequencia([Display2, Display1, Display3], TresMonitores.Monitores.Select(m => m.Chave));
        Afirmar.Sequencia([Display3, Display1, Display2], Invertida(TresMonitores).Monitores.Select(m => m.Chave));
    }

    [Teste]
    public static void TopologiaCopiaAListaRecebida()
    {
        var origem = new List<MonitorDoDesktop> { Principal1, Secundario2 };
        var topologia = new Topologia(origem);
        origem.Clear();
        Afirmar.Igual(2, topologia.Monitores.Count);
    }

    [Teste]
    public static void MonitoresNaoPodeSerAlteradaPorFora()
    {
        // Topologia é imutável (Topologia.cs): a lista exposta não pode aceitar alteração nem
        // por conversão de tipo, senão ImpressaoDigital, calculada na construção, deixaria de
        // descrever os monitores.
        var topologia = new Topologia([Principal1, Secundario2]);
        if (topologia.Monitores is ICollection<MonitorDoDesktop> colecao)
            Afirmar.Verdadeiro(colecao.IsReadOnly, "Monitores expõe uma coleção alterável");
    }

    [Teste]
    public static void PrincipalEOMonitorMarcadoMesmoForaDoInicioDaLista()
    {
        Afirmar.Igual(Display1, PrincipalADireita.Principal.Chave, "principal é o segundo da lista");
        Afirmar.Igual(Display1, TresMonitores.Principal.Chave, "principal no meio da lista");
        foreach ((string nome, Topologia t) in Todas)
        {
            Afirmar.Verdadeiro(t.Principal.Principal, $"{nome}: marcado como principal");
            Afirmar.Verdadeiro(t.Monitores.Contains(t.Principal), $"{nome}: principal pertence à lista");
        }
    }

    [Teste]
    public static void PorChaveAchaCadaMonitorOuDevolveNulo()
    {
        Topologia t = SecundarioAEsquerda;
        Afirmar.Igual(Ret(0, 0, 1920, 1080), Afirmar.NaoNulo(t.PorChave(Display1)).Tela, "principal");
        Afirmar.Igual(Ret(-1920, 0, 0, 1080), Afirmar.NaoNulo(t.PorChave(Display2)).Tela, "secundário");
        Afirmar.Nulo(t.PorChave(Display3), "chave ausente");
        Afirmar.Nulo(t.PorChave(""), "chave vazia");
    }

    // ---------------------------------------------------------------- monitor que contém

    [Teste]
    public static void MonitorQueContemRespeitaAsBordasSemiabertas()
    {
        Topologia t = LadoALado;
        Afirmar.Igual(Display1, Chave(t.MonitorQueContem(new PontoPx(0, 0))), "origem");
        Afirmar.Igual(Display1, Chave(t.MonitorQueContem(new PontoPx(1919, 500))), "última coluna do principal");
        Afirmar.Igual(Display2, Chave(t.MonitorQueContem(new PontoPx(1920, 500))), "primeira coluna do secundário");
        Afirmar.Igual(Display2, Chave(t.MonitorQueContem(new PontoPx(3839, 1079))), "último pixel do secundário");
        Afirmar.Nulo(t.MonitorQueContem(new PontoPx(3840, 500)), "depois do último monitor");
        Afirmar.Nulo(t.MonitorQueContem(new PontoPx(-1, 500)), "antes do primeiro monitor");
        Afirmar.Nulo(t.MonitorQueContem(new PontoPx(500, 1080)), "abaixo de todos");
    }

    [Teste]
    public static void MonitorQueContemComCoordenadasNegativas()
    {
        Topologia esquerda = SecundarioAEsquerda;
        Afirmar.Igual(Display2, Chave(esquerda.MonitorQueContem(new PontoPx(-1, 0))), "x = -1 é do secundário");
        Afirmar.Igual(Display1, Chave(esquerda.MonitorQueContem(new PontoPx(0, 0))), "x = 0 é do principal");
        Afirmar.Igual(Display2, Chave(esquerda.MonitorQueContem(new PontoPx(-1920, 1079))), "canto inferior esquerdo do secundário");
        Afirmar.Nulo(esquerda.MonitorQueContem(new PontoPx(-1921, 0)), "à esquerda do secundário");

        Topologia acima = EmpilhadoSecundarioAcima;
        Afirmar.Igual(Display2, Chave(acima.MonitorQueContem(new PontoPx(0, -1))), "y = -1 é do secundário");
        Afirmar.Igual(Display2, Chave(acima.MonitorQueContem(new PontoPx(-320, -1440))), "canto superior esquerdo do secundário");
        Afirmar.Igual(Display2, Chave(acima.MonitorQueContem(new PontoPx(2239, -1))), "canto inferior direito do secundário");
        Afirmar.Nulo(acima.MonitorQueContem(new PontoPx(2240, -1)), "à direita do secundário");
        Afirmar.Nulo(acima.MonitorQueContem(new PontoPx(-1, 0)), "à esquerda do principal, abaixo do secundário");
    }

    [Teste]
    public static void MonitorQueContemDevolveNuloNosVaos()
    {
        Afirmar.Nulo(VaoEntreMonitores.MonitorQueContem(new PontoPx(1920, 500)), "primeira coluna do vão");
        Afirmar.Nulo(VaoEntreMonitores.MonitorQueContem(new PontoPx(2119, 500)), "última coluna do vão");
        Afirmar.Igual(Display2, Chave(VaoEntreMonitores.MonitorQueContem(new PontoPx(2120, 500))), "primeira coluna depois do vão");
        Afirmar.Nulo(VaoEntreMonitores.MonitorQueContem(new PontoPx(2500, 50)), "acima do secundário");
        Afirmar.Nulo(QuinaComQuina.MonitorQueContem(new PontoPx(1920, 1079)), "à direita da quina");
        Afirmar.Nulo(QuinaComQuina.MonitorQueContem(new PontoPx(1919, 1080)), "abaixo da quina");
        Afirmar.Nulo(EmL.MonitorQueContem(new PontoPx(2000, 1160)), "canto interno do L");
        Afirmar.Nulo(DegrauDesalinhado.MonitorQueContem(new PontoPx(2500, 399)), "acima do segundo degrau");
        Afirmar.Nulo(Retrato.MonitorQueContem(new PontoPx(1919, -1)), "acima do principal, à esquerda do retrato");
    }

    // ---------------------------------------------------------------- monitor mais próximo

    [Teste]
    public static void MonitorMaisProximoDevolveOQueContem()
    {
        Afirmar.Igual(Display2, LadoALado.MonitorMaisProximo(new PontoPx(1920, 0)).Chave);
        Afirmar.Igual(Display2, SecundarioAEsquerda.MonitorMaisProximo(new PontoPx(-1, 1079)).Chave);
    }

    [Teste]
    public static void MonitorMaisProximoNumVao()
    {
        Topologia t = VaoEntreMonitores;
        // Última coluna do principal: 1919; primeira do secundário: 2120.
        Afirmar.Igual(Display1, t.MonitorMaisProximo(new PontoPx(1950, 600)).Chave, "31 px do principal, 170 do secundário");
        Afirmar.Igual(Display1, t.MonitorMaisProximo(new PontoPx(2019, 600)).Chave, "100 px do principal, 101 do secundário");
        Afirmar.Igual(Display2, t.MonitorMaisProximo(new PontoPx(2020, 600)).Chave, "101 px do principal, 100 do secundário");
        Afirmar.Igual(Display2, t.MonitorMaisProximo(new PontoPx(2500, 50)).Chave, "50 px acima do secundário");
        Afirmar.Igual(Display2, EmL.MonitorMaisProximo(new PontoPx(2500, 1500)).Chave, "canto interno do L: 421 px do secundário à direita, 581 do de baixo");
        Afirmar.Igual(Display1, SecundarioAEsquerda.MonitorMaisProximo(new PontoPx(100_000, -100_000)).Chave, "muito longe, acima e à direita");
        Afirmar.Igual(Display2, SecundarioAEsquerda.MonitorMaisProximo(new PontoPx(-5000, 500)).Chave, "muito à esquerda");
    }

    [Teste]
    public static void MonitorMaisProximoEmEmpateEscolheOPrincipal()
    {
        // Na quina, (1920,1079) fica a 1 px do principal e a 1 px do secundário.
        Afirmar.Igual(Display1, QuinaComQuina.MonitorMaisProximo(new PontoPx(1920, 1079)).Chave, "principal primeiro na lista");
        Afirmar.Igual(Display1, Invertida(QuinaComQuina).MonitorMaisProximo(new PontoPx(1920, 1079)).Chave, "principal por último na lista");
        Afirmar.Igual(Display1, Invertida(QuinaComQuina).MonitorMaisProximo(new PontoPx(1919, 1080)).Chave, "abaixo da quina");

        // Vão de 99 px entre um secundário à esquerda (última coluna -100) e o principal:
        // x = -50 fica a 50 px de cada um.
        var vao = new Topologia([
            Secundario(Display2, Ret(-1000, 0, -99, 1080), Ret(-1000, 0, -99, 1032), 96),
            Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        ]);
        Afirmar.Igual(Display1, vao.MonitorMaisProximo(new PontoPx(-50, 500)).Chave, "empate no meio do vão");
        Afirmar.Igual(Display2, vao.MonitorMaisProximo(new PontoPx(-51, 500)).Chave, "um pixel mais perto do secundário");
    }

    [Teste]
    public static void MonitorMaisProximoEmEmpateSemPrincipalSegueAOrdemDaLista()
    {
        // (2000,1160), no canto interno do L, fica a 81 px do monitor da direita e do de
        // baixo, e a 81·√2 px do principal.
        Afirmar.Igual(Display2, EmL.MonitorMaisProximo(new PontoPx(2000, 1160)).Chave, "ordem 1, 2, 3");
        Afirmar.Igual(Display3, Invertida(EmL).MonitorMaisProximo(new PontoPx(2000, 1160)).Chave, "ordem 3, 2, 1");
    }

    // ---------------------------------------------------------------- impressão digital

    [Teste]
    public static void ImpressaoDigitalIgualParaAMesmaConfiguracaoEmOutraOrdem()
    {
        Topologia t = TresMonitores;
        Afirmar.Igual(t.ImpressaoDigital, Invertida(t).ImpressaoDigital, "ordem inversa");
        Afirmar.Igual(t.ImpressaoDigital, new Topologia([t.Monitores[2], t.Monitores[0], t.Monitores[1]]).ImpressaoDigital, "outra permutação");
        Afirmar.Igual(t.ImpressaoDigital, TresMonitores.ImpressaoDigital, "outra instância");
        Afirmar.Verdadeiro(t.MesmaConfiguracao(Invertida(t)), "MesmaConfiguracao");
    }

    [Teste]
    public static void ImpressaoDigitalMudaComCadaCampoDaConfiguracao()
    {
        Topologia t = LadoALado;
        var variacoes = new (string Caso, Topologia Topologia)[]
        {
            ("área útil (barra no topo)", ComMonitor(t, Display1, m => m with { AreaUtil = Ret(0, 48, 1920, 1080) })),
            ("DPI", ComMonitor(t, Display2, m => m with { Dpi = 144 })),
            ("retângulo da tela", ComMonitor(t, Display2, m => m with { Tela = Ret(1920, 0, 3840, 1200) })),
            ("retângulo inteiro deslocado", ComMonitor(t, Display2, m => m with { Tela = Ret(1920, 100, 3840, 1180), AreaUtil = Ret(1920, 100, 3840, 1132) })),
            ("chave", ComMonitor(t, Display2, m => m with { Chave = Display3 })),
            ("principal", new Topologia(t.Monitores.Select(m => m with { Principal = !m.Principal }))),
            ("monitor a menos", SemMonitor(t, Display2)),
            ("monitor a mais", new Topologia([.. t.Monitores, Secundario(Display3, Ret(0, 1080, 1920, 2160), Ret(0, 1080, 1920, 2112), 96)])),
        };

        foreach ((string caso, Topologia outra) in variacoes)
        {
            Afirmar.Diferente(t.ImpressaoDigital, outra.ImpressaoDigital, caso);
            Afirmar.Falso(t.MesmaConfiguracao(outra), $"{caso}: MesmaConfiguracao");
        }
    }

    [Teste]
    public static void ImpressaoDigitalNaoDependeDaCulturaAtual()
    {
        // A impressão é um resumo canônico (Topologia.cs): a mesma configuração precisa dar a
        // mesma impressão em qualquer cultura, inclusive nas que escrevem o sinal de menos
        // como U+2212 (sv-SE, fi-FI e nb-NO com ICU no .NET 10).
        Topologia invariante = Cultura.Com(CultureInfo.InvariantCulture, () => SecundarioAEsquerda);
        Topologia outra = Cultura.Com(Cultura.ComMenosTipografico(), () => SecundarioAEsquerda);

        Afirmar.Igual(invariante.ImpressaoDigital, outra.ImpressaoDigital);
        Afirmar.Verdadeiro(invariante.MesmaConfiguracao(outra), "MesmaConfiguracao");
    }

    [Teste]
    public static void MesmaConfiguracaoComNuloLanca()
    {
        Afirmar.Lanca<ArgumentNullException>(() => LadoALado.MesmaConfiguracao(null!));
    }

    // ---------------------------------------------------------------- monitor

    [Teste]
    public static void OrientacaoSegueAsProporcoesDaTela()
    {
        Afirmar.Igual(Orientacao.Retrato, Afirmar.NaoNulo(Retrato.PorChave(Display2)).Orientacao, "1080x1920");
        Afirmar.Igual(Orientacao.Paisagem, Afirmar.NaoNulo(Retrato.PorChave(Display1)).Orientacao, "1920x1080");
        Afirmar.Igual(Orientacao.Paisagem, (Principal1 with { Tela = Ret(0, 0, 1200, 1200), AreaUtil = Ret(0, 0, 1200, 1152) }).Orientacao, "quadrado conta como paisagem");
    }

    [Teste]
    public static void EscalaEDpiSobre96()
    {
        int[] dpis = [96, 120, 144, 168, 192, 240, 288];
        double[] escalas = [1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];
        for (int i = 0; i < dpis.Length; i++)
            Afirmar.Igual(escalas[i], (Principal1 with { Dpi = dpis[i] }).Escala, $"{dpis[i]} DPI");
    }

    // ---------------------------------------------------------------- auxiliares

    private static string? Chave(MonitorDoDesktop? monitor) => monitor?.Chave;
}
