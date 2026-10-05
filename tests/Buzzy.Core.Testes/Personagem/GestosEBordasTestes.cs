using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P5 (DEC-037, itens 7 e 9): com a personalidade, o tipo do gesto vem dos pesos da energia (a Baixa
/// prefere coçar-se e espreguiçar-se; a Alta, brincar, olhar ao redor e espiar; a Média uniforme, como antes), no gerador da
/// personalidade, sem mudar o principal; e uma caminhada em cada N vai à lateral mais próxima e lá espia para fora, sem
/// atravessar nem escalar. Horas de relógio virtual (<see cref="SimuladorDeTempo"/>).
/// </summary>
internal static class GestosEBordasTestes
{
    private static readonly Gesto[] Calmos = [Gesto.Cocar, Gesto.Espreguicar];

    /// <summary>Os gestos da agenda em <paramref name="horas"/>, só com o gesto permitido; e o gerador principal no fim.</summary>
    private static (List<Gesto> Gestos, Aleatorio Principal, List<double> Instantes) Gestos(NivelDeEnergia energia, bool personalidade, int horas = 4)
    {
        var cfg = new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Gesto, Personalidade = personalidade };
        var sim = new SimuladorDeTempo(cfg, 2026, TopologiasDeExemplo.UmMonitor, new Preferencias(energia, true));
        var gestos = new List<Gesto>();
        var instantes = new List<double>();
        sim.AoAplicar = (antes, e, depois) =>
        {
            if (e is AutonomyTimer && antes.Gesto == Gesto.Nenhum && depois.Gesto != Gesto.Nenhum)
            {
                gestos.Add(depois.Gesto);
                instantes.Add(sim.AgoraMs);
            }
        };
        sim.Avancar(TimeSpan.FromHours(horas));
        return (gestos, sim.Estado.Aleatorio, instantes);
    }

    private static double Fracao(List<Gesto> g, Gesto[] grupo) => (double)g.Count(grupo.Contains) / g.Count;

    [Teste]
    public static void Gestos_PelaEnergia_SemMudarOPrincipal()
    {
        var resumo = new List<string>();
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            (List<Gesto> com, Aleatorio principalCom, List<double> instantesCom) = Gestos(energia, personalidade: true);
            (List<Gesto> sem, Aleatorio principalSem, List<double> instantesSem) = Gestos(energia, personalidade: false);
            Afirmar.Verdadeiro(com.Count >= 150, $"{energia}: {com.Count} gestos");
            // O principal anda igual: os mesmos instantes, as mesmas durações, o mesmo gerador no fim.
            Afirmar.Igual(principalSem, principalCom, $"{energia}: o gerador principal");
            Afirmar.Sequencia(instantesSem, instantesCom, $"{energia}: os mesmos instantes");
            Afirmar.Verdadeiro(Fracao(sem, Calmos) is > 0.32 and < 0.48, $"{energia}, sem personalidade: uniforme ({Fracao(sem, Calmos):P0} calmos)");
            resumo.Add($"{energia} {Fracao(com, Calmos):P0}");
            foreach (Gesto g in new[] { Gesto.Espiar, Gesto.OlharAoRedor, Gesto.Cocar, Gesto.Espreguicar, Gesto.Brincar })
                Afirmar.Verdadeiro(com.Contains(g), $"{energia}: o gesto {g} ainda aparece");
            if (energia == NivelDeEnergia.Media) Afirmar.Sequencia(sem, com, "a Média uniforme: os mesmos gestos de antes");
            else
            {
                // Cada gesto é um sorteio ponderado no gerador da personalidade, em sequência.
                IReadOnlyList<int> pesos = PerfilDeEnergia.Padrao(energia).PesosDosGestos!;
                Aleatorio p = new(EstadoDoNucleo.SementeDaPersonalidade(2026));
                var esperados = new List<Gesto>();
                foreach (Gesto _ in com)
                {
                    (int i, p) = p.Ponderado([.. pesos]);
                    esperados.Add((Gesto)((int)Gesto.Espiar + i));
                }
                Afirmar.Sequencia(esperados, com, $"{energia}: os gestos saem do gerador da personalidade");
            }
        }
        Console.WriteLine($"         calmos (coçar e espreguiçar): {string.Join(", ", resumo)}");
        // Pesos: Baixa 7 de 11 (64%); Alta 2 de 12 (17%).
        Afirmar.Verdadeiro(Fracao(Gestos(NivelDeEnergia.Baixa, true).Gestos, Calmos) is > 0.55 and < 0.73, $"Baixa: {string.Join(", ", resumo)}");
        Afirmar.Verdadeiro(Fracao(Gestos(NivelDeEnergia.Alta, true).Gestos, Calmos) is > 0.10 and < 0.25, $"Alta: {string.Join(", ", resumo)}");
    }

    /// <summary>As caminhadas em <paramref name="horas"/>, só com andar: quantas, quantas exploraram a borda e o que viu em cada uma.</summary>
    private static (int Caminhadas, int Exploracoes) Explorar(NivelDeEnergia energia, Topologia topologia, int horas = 3)
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Acoes = AcoesAutonomas.Andar, Personalidade = true };
        var sim = new SimuladorDeTempo(cfg, 11, topologia, new Preferencias(energia, true));
        int caminhadas = 0, exploracoes = 0;
        string? monitorDaExploracao = null;
        sim.AoResultado = (antes, e, r) =>
        {
            foreach (Transicao t in r.Transicoes)
            {
                if (t.Regra == "IDLE + AUTONOMY_TIMER: andar") caminhadas++;
                if (t.Regra == "IDLE + AUTONOMY_TIMER: explorar a borda")
                {
                    caminhadas++;
                    exploracoes++;
                    monitorDaExploracao = antes.Lugar!.Monitor.Chave;
                }
            }
            EstadoDoNucleo s = sim.Estado;
            if (monitorDaExploracao is null) return;
            Afirmar.Igual(monitorDaExploracao, s.Lugar!.Monitor.Chave, $"{energia}: explorando, nunca atravessa");
            Afirmar.Falso(s.Estado is Estado.Climbing or Estado.Jumping, $"{energia}: explorando, nunca escala nem salta ({s.Estado})");
            if (r.Transicoes.Any(t => t.Regra == "WALKING: borda explorada (espia para fora)"))
            {
                Superficies sup = Superficies.Do(s.Topologia!, s.Lugar.Monitor, s.Lugar.Tamanho);
                int x = s.Lugar.Ancora.X;
                Afirmar.Verdadeiro(x == sup.Esquerda || x == sup.Direita, $"{energia}: na lateral ({x}; {sup.Esquerda} a {sup.Direita})");
                Direcao fora = x == sup.Direita ? Direcao.Direita : Direcao.Esquerda;
                Afirmar.Igual((Estado.Idle, Gesto.Espiar, fora, true), (s.Estado, s.Gesto, s.Direcao, s.Retrato().NaBorda), $"{energia}: espia para fora");
                Afirmar.Verdadeiro(s.Retrato().Descrever().EndsWith(" borda=sim", StringComparison.Ordinal), s.Retrato().Descrever());
                monitorDaExploracao = null;
            }
        };
        sim.Avancar(TimeSpan.FromHours(horas));
        return (caminhadas, exploracoes);
    }

    [Teste]
    public static void ExplorarBorda_UmEmN_NaLateral_SemAtravessar()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            int umEm = PerfilDeEnergia.Padrao(energia).ExplorarBordaUmEm;
            (int caminhadas, int exploracoes) = Explorar(energia, TopologiasDeExemplo.LadoALado);
            double fracao = (double)exploracoes / caminhadas;
            Console.WriteLine($"         {energia}: {exploracoes} de {caminhadas} caminhadas exploraram a borda ({fracao:P0}; 1 em {umEm})");
            Afirmar.Verdadeiro(caminhadas >= 100, $"{energia}: {caminhadas} caminhadas");
            Afirmar.Verdadeiro(Math.Abs(fracao - (1.0 / umEm)) < 0.08, $"{energia}: {fracao:P0}, perto de 1 em {umEm}");
        }
    }

    // A espiada na borda acaba com o gesto: o retrato volta à linha de antes, e o próximo gesto no mesmo lugar não é na borda.
    [Teste]
    public static void EspiadaNaBorda_AcabaComOGesto()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Acoes = AcoesAutonomas.Andar, Personalidade = true };
        var sim = new SimuladorDeTempo(cfg, 11, TopologiasDeExemplo.UmMonitor, new Preferencias(NivelDeEnergia.Alta, true));
        sim.Avancar(TimeSpan.FromHours(1), s => s.Retrato().NaBorda);
        Afirmar.Verdadeiro(sim.Estado.Retrato().NaBorda, "chegou a espiar na borda");
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Gesto == Gesto.Nenhum);
        Afirmar.Igual((Gesto.Nenhum, false, false), (sim.Estado.Gesto, sim.Estado.Movimento.ExplorandoBorda, sim.Estado.Retrato().NaBorda), "o fim do gesto acaba a espiada");
    }

    // Pausada no meio da exploração, a caminhada para em IDLE sem espiar: o retrato não fica na borda.
    [Teste]
    public static void PausaNaExploracao_NaoFicaNaBorda()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Acoes = AcoesAutonomas.Andar, Personalidade = true };
        var sim = new SimuladorDeTempo(cfg, 11, TopologiasDeExemplo.UmMonitor, new Preferencias(NivelDeEnergia.Alta, true));
        sim.Avancar(TimeSpan.FromHours(1), s => s.Estado == Estado.Walking && s.Movimento.ExplorandoBorda && s.Movimento.Restante > 200);
        Afirmar.Verdadeiro(sim.Estado.Movimento.ExplorandoBorda, "explorando");
        sim.Aplicar(new CmdPauseAutonomy());
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado == Estado.Idle);
        sim.Esta(Estado.Idle, "parou");
        Afirmar.Igual((Gesto.Nenhum, false), (sim.Estado.Gesto, sim.Estado.Retrato().NaBorda), "parado sem espiar, fora da borda");
    }

    // Sem a personalidade, nenhuma caminhada explora a borda.
    [Teste]
    public static void SemPersonalidade_NenhumaExploracao()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Acoes = AcoesAutonomas.Andar, Personalidade = false };
        var sim = new SimuladorDeTempo(cfg, 11, TopologiasDeExemplo.UmMonitor);
        sim.AoAplicar = (_, e, depois) => Afirmar.Falso(depois.Movimento.ExplorandoBorda, $"{e}: sem personalidade");
        sim.Avancar(TimeSpan.FromHours(1));
    }
}
