using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P3 (DEC-037, itens 7 e 14): as capacidades novas, desligadas por padrão; o gerador próprio da
/// personalidade, semeado à parte e intocado enquanto elas estão desligadas; e os campos novos do perfil de energia,
/// ordenados pela energia (a Baixa mais lenta e mais calma, a Alta mais rápida e mais agitada), sem física (invariante 12).
/// </summary>
internal static class PersonalidadeDaFase7Testes
{
    private static readonly PerfilDeEnergia[] Perfis = [PerfilDeEnergia.Baixa, PerfilDeEnergia.Media, PerfilDeEnergia.Alta];

    [Teste]
    public static void Capacidades_DesligadasPorPadrao()
    {
        var cfg = new ConfiguracaoDoNucleo();
        Afirmar.Igual((false, false), (cfg.Curiosidade, cfg.Personalidade), "a curiosidade e a personalidade");
        Afirmar.Igual((30.0, 2.0, 16), (cfg.LimiarDeOutroMonitor.TotalSeconds, cfg.EsperaPeloVao.TotalSeconds, cfg.FolgaDoOlhar), "os parâmetros fixos");
    }

    [Teste]
    public static void Gerador_SemeadoAParte_DosOutrosDois()
    {
        const ulong semente = 2026;
        EstadoDoNucleo e = EstadoDoNucleo.Inicial(semente);
        Afirmar.Igual(unchecked((semente * 47) + 19), EstadoDoNucleo.SementeDaPersonalidade(semente), "semente × 47 + 19");
        Afirmar.Igual(new Aleatorio(EstadoDoNucleo.SementeDaPersonalidade(semente)), e.AleatorioDaPersonalidade, "o gerador do estado inicial");
        Afirmar.Igual(unchecked((ulong.MaxValue * 47) + 19), EstadoDoNucleo.SementeDaPersonalidade(ulong.MaxValue), "sem estouro verificado");
        Afirmar.Falso(e.AleatorioDaPersonalidade.Equals(e.Aleatorio) || e.AleatorioDaPersonalidade.Equals(e.AleatorioDaParanoia), "diferente do principal e do da paranoia");
    }

    // Com tudo o que o aplicativo liga hoje, e a personalidade e a curiosidade desligadas, horas de relógio virtual não
    // consomem o gerador novo: as reproduções e os testes de semente fixa não podem mudar por causa dele.
    [Teste]
    public static void Desligadas_OGeradorNaoAnda()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Personalidade = false, Curiosidade = false };
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            var sim = new SimuladorDeTempo(cfg, 7, TopologiasDeExemplo.LadoALado, new Preferencias(energia, true));
            Aleatorio inicial = sim.Estado.AleatorioDaPersonalidade;
            sim.Avancar(TimeSpan.FromHours(2));
            Afirmar.Verdadeiro(sim.Transicoes.Count > 100, $"{energia}: ele fez coisas ({sim.Transicoes.Count})");
            Afirmar.Igual(inicial, sim.Estado.AleatorioDaPersonalidade, $"{energia}: o gerador da personalidade não andou");
        }
    }

    [Teste]
    public static void Perfis_OrdenadosPelaEnergia()
    {
        static void Crescente<T>(Func<PerfilDeEnergia, T> campo, string nome) where T : IComparable<T>
        {
            T b = campo(PerfilDeEnergia.Baixa), m = campo(PerfilDeEnergia.Media), a = campo(PerfilDeEnergia.Alta);
            Afirmar.Verdadeiro(b.CompareTo(m) < 0 && m.CompareTo(a) < 0, $"{nome} cresce com a energia: {b}, {m}, {a}");
        }
        static void Decrescente<T>(Func<PerfilDeEnergia, T> campo, string nome) where T : IComparable<T>
        {
            T b = campo(PerfilDeEnergia.Baixa), m = campo(PerfilDeEnergia.Media), a = campo(PerfilDeEnergia.Alta);
            Afirmar.Verdadeiro(b.CompareTo(m) > 0 && m.CompareTo(a) > 0, $"{nome} diminui com a energia: {b}, {m}, {a}");
        }
        // Mais energia: chega perto mais cedo, olha menos, volta mais cedo, fica dispensado menos tempo, reage mais longo e
        // explora a borda mais vezes.
        Decrescente(p => p.TempoParaAproximar, "o tempo para chegar perto");
        Decrescente(p => p.RodadasOlhandoMaximo, "o máximo de rodadas olhando");
        Decrescente(p => p.IntervaloEntreCuriosidades, "o intervalo entre curiosidades");
        Decrescente(p => p.EsperaDepoisDoUsuario, "a dispensa depois do usuário");
        Decrescente(p => p.ExplorarBordaUmEm, "o 1 em N de explorar a borda");
        Crescente(p => p.PassosDaReacao, "a reação ao clique");
        Afirmar.Igual((180.0, 90.0, 45.0), (PerfilDeEnergia.Baixa.TempoParaAproximar.TotalSeconds, PerfilDeEnergia.Media.TempoParaAproximar.TotalSeconds, PerfilDeEnergia.Alta.TempoParaAproximar.TotalSeconds), "DEC-037, item 5");
        Afirmar.Igual((30, 36, 48), (PerfilDeEnergia.Baixa.PassosDaReacao, PerfilDeEnergia.Media.PassosDaReacao, PerfilDeEnergia.Alta.PassosDaReacao), "DEC-037, item 8");

        foreach (PerfilDeEnergia p in Perfis)
        {
            Afirmar.Verdadeiro(p.RodadasOlhandoMinimo >= 1 && p.RodadasOlhandoMinimo <= p.RodadasOlhandoMaximo, $"{p.Nivel}: rodadas {p.RodadasOlhandoMinimo} a {p.RodadasOlhandoMaximo}");
            Afirmar.Igual(4, p.PesosOlhando.Count, $"{p.Nivel}: quatro pesos do olhar");
            Afirmar.Verdadeiro(p.PesosOlhando.All(x => x > 0), $"{p.Nivel}: todo peso do olhar positivo");
            Afirmar.Verdadeiro(p.ExplorarBordaUmEm >= 2, $"{p.Nivel}: explorar a borda é a exceção");
        }
        // Ficar olhando pesa mais na Baixa; perder o interesse, mais na Alta.
        double Fracao(PerfilDeEnergia p, int i) => (double)p.PesosOlhando[i] / p.PesosOlhando.Sum();
        Afirmar.Verdadeiro(Fracao(PerfilDeEnergia.Baixa, 0) > Fracao(PerfilDeEnergia.Media, 0) && Fracao(PerfilDeEnergia.Media, 0) > Fracao(PerfilDeEnergia.Alta, 0), "ficar olhando diminui com a energia");
        Afirmar.Verdadeiro(Fracao(PerfilDeEnergia.Baixa, 3) < Fracao(PerfilDeEnergia.Media, 3) && Fracao(PerfilDeEnergia.Media, 3) < Fracao(PerfilDeEnergia.Alta, 3), "perder o interesse cresce com a energia");
    }

    // Os pesos dos gestos (DEC-037, item 9): a Média uniforme (nulo, o sorteio de antes); a Baixa prefere coçar-se e
    // espreguiçar-se; a Alta, brincar, olhar ao redor e espiar; nenhum gesto some.
    [Teste]
    public static void PesosDosGestos_CalmosNaBaixa_AgitadosNaAlta()
    {
        Afirmar.Nulo(PerfilDeEnergia.Media.PesosDosGestos, "a Média uniforme");
        Gesto[] daAgenda = [Gesto.Espiar, Gesto.OlharAoRedor, Gesto.Cocar, Gesto.Espreguicar, Gesto.Brincar];
        Afirmar.Igual(daAgenda.Length, (int)Gesto.Brincar, "os gestos da agenda vão de Espiar a Brincar, na ordem do enum");
        IReadOnlyList<int> baixa = Afirmar.NaoNulo(PerfilDeEnergia.Baixa.PesosDosGestos), alta = Afirmar.NaoNulo(PerfilDeEnergia.Alta.PesosDosGestos);
        Afirmar.Igual((5, 5), (baixa.Count, alta.Count), "um peso por gesto da agenda");
        Afirmar.Verdadeiro(baixa.All(x => x > 0) && alta.All(x => x > 0), "nenhum gesto some");
        int Peso(IReadOnlyList<int> p, Gesto g) => p[(int)g - 1];
        double Calmos(IReadOnlyList<int> p) => (double)(Peso(p, Gesto.Cocar) + Peso(p, Gesto.Espreguicar)) / p.Sum();
        Afirmar.Verdadeiro(Calmos(baixa) > 0.5 && Calmos(alta) < 0.25, $"coçar e espreguiçar: Baixa {Calmos(baixa):P0}, Alta {Calmos(alta):P0} (uniforme 40%)");
    }

    // Invariante 12: a energia muda frequências e tempos, nunca a física.
    [Teste]
    public static void Perfis_SemFisica()
    {
        Afirmar.Falso(typeof(PerfilDeEnergia).GetProperties().Any(p => p.PropertyType == typeof(ParametrosDeMovimento)), "o perfil não carrega física");
    }
}
