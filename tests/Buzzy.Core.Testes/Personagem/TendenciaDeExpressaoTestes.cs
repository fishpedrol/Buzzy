using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 6, passo F6-P5 (DEC-036, item 6; Q-23): a tendência de expressão por energia. Na troca de cara automática, a
/// energia Baixa prefere as caras calmas, e a Alta, as agitadas; a Média continua uniforme, com o sorteio de antes. A troca
/// nunca repete a cara atual. Horas de relógio virtual só com a troca de cara (<see cref="SimuladorDeTempo"/>).
/// </summary>
internal static class TendenciaDeExpressaoTestes
{
    private static readonly Expressao[] Calmas = [Expressao.Sonolento, Expressao.Bocejando, Expressao.Entediado, Expressao.Pensativo, Expressao.Dormindo];

    private static readonly Expressao[] Agitadas = [Expressao.Empolgado, Expressao.Rindo, Expressao.Travesso, Expressao.Determinado, Expressao.Surpreso];

    /// <summary>As caras que a troca automática sorteou em <paramref name="horas"/> de relógio virtual, na energia dada.</summary>
    private static List<Expressao> Trocas(NivelDeEnergia energia, int horas = 6)
    {
        var cfg = new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.TrocarExpressao };
        var sim = new SimuladorDeTempo(cfg, 2026, TopologiasDeExemplo.UmMonitor, new Preferencias(energia, true));
        var trocas = new List<Expressao>();
        sim.AoAplicar = (antes, e, depois) =>
        {
            if (e is AutonomyTimer && depois.Expressao != antes.Expressao)
            {
                Afirmar.Verdadeiro(Expressoes.DeHumor.Contains(depois.Expressao), $"{energia}: uma cara de humor ({depois.Expressao})");
                trocas.Add(depois.Expressao);
            }
            if (e is AutonomyTimer && antes.DecisaoAgendada && depois.Expressao == antes.Expressao && antes.Estado == Estado.Idle)
                Afirmar.Falso(true, $"{energia}: a troca repetiu a cara {antes.Expressao}");
        };
        sim.Avancar(TimeSpan.FromHours(horas));
        return trocas;
    }

    private static double Fracao(List<Expressao> trocas, Expressao[] grupo) => (double)trocas.Count(grupo.Contains) / trocas.Count;

    [Teste]
    public static void Baixa_PrefereAsCalmas_Alta_AsAgitadas_Media_Uniforme()
    {
        List<Expressao> baixa = Trocas(NivelDeEnergia.Baixa), media = Trocas(NivelDeEnergia.Media), alta = Trocas(NivelDeEnergia.Alta);
        foreach ((string nome, List<Expressao> t) in new[] { ("Baixa", baixa), ("Média", media), ("Alta", alta) })
            Afirmar.Verdadeiro(t.Count >= 300, $"{nome}: {t.Count} trocas em 6 horas");
        string resumo = $"calmas: Baixa {Fracao(baixa, Calmas):P0}, Média {Fracao(media, Calmas):P0}, Alta {Fracao(alta, Calmas):P0}; "
            + $"agitadas: Baixa {Fracao(baixa, Agitadas):P0}, Média {Fracao(media, Agitadas):P0}, Alta {Fracao(alta, Agitadas):P0}";
        Console.WriteLine($"         {resumo}");
        // Pesos: na Baixa, as calmas somam 18 de 34 (53%); na Alta, 5 de 31 (16%); na Média, 5 de 14 (36%).
        Afirmar.Verdadeiro(Fracao(baixa, Calmas) is > 0.45 and < 0.62, $"Baixa, calmas: {resumo}");
        Afirmar.Verdadeiro(Fracao(alta, Calmas) < 0.24, $"Alta, calmas: {resumo}");
        Afirmar.Verdadeiro(Fracao(alta, Agitadas) is > 0.50 and < 0.68, $"Alta, agitadas: {resumo}");
        Afirmar.Verdadeiro(Fracao(media, Calmas) is > 0.28 and < 0.44, $"Média, calmas, uniforme: {resumo}");
        Afirmar.Verdadeiro(Fracao(baixa, Agitadas) < Fracao(media, Agitadas) && Fracao(media, Agitadas) < Fracao(alta, Agitadas), $"as agitadas crescem com a energia: {resumo}");
        // Toda cara de humor ainda aparece nas três, por mais rara que seja.
        foreach ((string nome, List<Expressao> t) in new[] { ("Baixa", baixa), ("Média", media), ("Alta", alta) })
            foreach (Expressao cara in Expressoes.DeHumor)
                Afirmar.Verdadeiro(t.Contains(cara), $"{nome}: a cara {cara} aparece");
    }

    // Os pesos têm uma entrada por cara de humor, nenhuma negativa, e a Média não tem tendência (o sorteio uniforme de antes).
    [Teste]
    public static void Pesos_UmPorCaraDeHumor_EAMediaSemTendencia()
    {
        Afirmar.Nulo(PerfilDeEnergia.Media.PesosDasCaras, "a Média, uniforme");
        foreach (PerfilDeEnergia p in new[] { PerfilDeEnergia.Baixa, PerfilDeEnergia.Alta })
        {
            IReadOnlyList<int> pesos = Afirmar.NaoNulo(p.PesosDasCaras, $"{p.Nivel}: com tendência");
            Afirmar.Igual(Expressoes.DeHumor.Count, pesos.Count, $"{p.Nivel}: um peso por cara");
            Afirmar.Verdadeiro(pesos.All(x => x >= 1), $"{p.Nivel}: toda cara pode sair");
        }
    }
}
