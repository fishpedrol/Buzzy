using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P4 (DEC-037, item 8): a reação ao clique por regra, sem sorteio, com a capacidade
/// <c>Personalidade</c>. Clicado descansando, um susto; o segundo clique seguido desde a última decisão, empolgada; os outros
/// pela energia (Baixa preguiçosa, Média a de antes, Alta empolgada); escondido ou preso, a de antes. A duração vem do perfil;
/// a variante só vale em REACTING e aparece no retrato (<c>reacao=</c>). Sem a capacidade, tudo como antes.
/// </summary>
internal static class ReacoesTestes
{
    private static readonly ConfiguracaoDoNucleo ComPersonalidade = new() { Personalidade = true };

    private static Cenario Parado(NivelDeEnergia energia, ConfiguracaoDoNucleo? cfg = null)
        => new Cenario(cfg ?? ComPersonalidade).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, new Preferencias(energia, true)));

    private static Cenario Clicar(this Cenario c) => c.Aplicar(new Press(Cenario.MeioDoPersonagem(c.Atual, c.Config)), new Click());

    /// <summary>Passos até a reação acabar e ele voltar a IDLE, sem decisão da agenda no meio.</summary>
    private static Cenario AteOFim(this Cenario c)
    {
        for (int i = 0; i < 200 && c.Atual.Estado != Estado.Idle; i++) c.Passos(1);
        return c.Esta(Estado.Idle, "a reação acabou");
    }

    private static void Reagiu(Cenario c, VarianteDaReacao variante, Expressao cara, int passos, string contexto)
    {
        c.Esta(Estado.Reacting, contexto);
        Afirmar.Igual((variante, cara, passos, Sinal.FoiClicado), (c.Atual.Reacao, c.Atual.Expressao, c.Atual.PassosRestantes, c.Atual.Sinal), contexto);
        Afirmar.Igual(variante, c.Retrato.Reacao, $"{contexto}: o retrato leva a variante");
        string sufixo = variante == VarianteDaReacao.Padrao ? "" : $" reacao={variante.ToString().ToLowerInvariant()}";
        Afirmar.Verdadeiro(variante == VarianteDaReacao.Padrao ? !c.Retrato.Descrever().Contains("reacao=") : c.Retrato.Descrever().EndsWith(sufixo, StringComparison.Ordinal), $"{contexto}: {c.Retrato.Descrever()}");
    }

    [Teste]
    public static void SemPersonalidade_ComoAntes()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            Cenario c = Parado(energia, new ConfiguracaoDoNucleo()).Clicar();
            Reagiu(c, VarianteDaReacao.Padrao, Expressao.Feliz, new ConfiguracaoDoNucleo().PassosDaReacao, $"{energia}, sem personalidade");
            Afirmar.Igual(0, c.Atual.CliquesSeguidos, "sem personalidade, nada é contado");
            c.AteOFim();
            Reagiu(Parado(energia, new ConfiguracaoDoNucleo()).AplicarCom(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(1)).Clicar(),
                VarianteDaReacao.Padrao, Expressao.Feliz, 36, $"{energia}, sem personalidade, descansando");
        }
    }

    [Teste]
    public static void CliqueComum_PelaEnergia()
    {
        Reagiu(Parado(NivelDeEnergia.Baixa).Clicar(), VarianteDaReacao.Preguica, Expressao.Sonolento, 30, "Baixa: preguiçosa");
        Reagiu(Parado(NivelDeEnergia.Media).Clicar(), VarianteDaReacao.Padrao, Expressao.Feliz, 36, "Média: a de antes");
        Reagiu(Parado(NivelDeEnergia.Alta).Clicar(), VarianteDaReacao.Empolgada, Expressao.Empolgado, 48, "Alta: empolgada");
        Cenario c = Parado(NivelDeEnergia.Media).Clicar();
        Afirmar.Verdadeiro(c.Transicoes.Any(t => t.Regra == "CLICK: reação de antes (energia Média)"), "a regra vai à transição");

        // Fora de REACTING, a variante acaba: o retrato volta à linha de antes.
        Cenario preguicosa = Parado(NivelDeEnergia.Baixa).Clicar().AteOFim();
        Afirmar.Igual(VarianteDaReacao.Padrao, preguicosa.Atual.Reacao, "a variante acabou com a reação");
        Afirmar.Falso(preguicosa.Retrato.Descrever().Contains("reacao="), preguicosa.Retrato.Descrever());
    }

    [Teste]
    public static void Descansando_Susto_EmQualquerEnergia()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            Cenario c = Parado(energia).AplicarCom(ComPersonalidade with { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(1)).Esta(Estado.Resting);
            int passos = PerfilDeEnergia.Padrao(energia).PassosDaReacao;
            Reagiu(c.Clicar(), VarianteDaReacao.Susto, Expressao.Surpreso, passos, $"{energia}: descansando, susto");
            Afirmar.Verdadeiro(c.Transicoes.Any(t => t.Regra == "CLICK: susto (descansava)"), "a regra do susto");
        }
    }

    [Teste]
    public static void SegundoCliqueSeguido_Empolgada_DecisaoEArrasteZeram()
    {
        Cenario c = Parado(NivelDeEnergia.Media).Clicar();
        Reagiu(c, VarianteDaReacao.Padrao, Expressao.Feliz, 36, "o primeiro clique");
        c.AteOFim();
        Afirmar.Igual((VarianteDaReacao.Padrao, ""), (c.Atual.Reacao, c.Retrato.Descrever().Contains("reacao=") ? "sufixo" : ""), "fora de REACTING, a variante acaba");
        Reagiu(c.Clicar(), VarianteDaReacao.Empolgada, Expressao.Empolgado, 36, "o segundo clique seguido");
        c.AteOFim();
        Reagiu(c.Clicar(), VarianteDaReacao.Empolgada, Expressao.Empolgado, 36, "o terceiro também");
        c.AteOFim();

        // Uma decisão da agenda no meio zera a contagem (só a parada: nenhuma ação permitida).
        c.AplicarCom(ComPersonalidade with { Acoes = AcoesAutonomas.Nenhuma }, new AutonomyTimer(c.Atual.Geracao));
        Afirmar.Igual(0, c.Atual.CliquesSeguidos, "a decisão zera");
        Reagiu(c.Clicar(), VarianteDaReacao.Padrao, Expressao.Feliz, 36, "depois de uma decisão, a de antes");
        c.AteOFim();

        // O arraste também zera.
        c.Aplicar(new Press(Cenario.MeioDoPersonagem(c.Atual, c.Config)), new DragStart());
        Afirmar.Igual(0, c.Atual.CliquesSeguidos, "o arraste zera");
        c.Aplicar(new DragEnd(Cenario.MeioDoPersonagem(c.Atual, c.Config)));
        for (int i = 0; i < 200 && c.Atual.Estado != Estado.Idle; i++) c.Passos(1);
        Reagiu(c.Esta(Estado.Idle).Clicar(), VarianteDaReacao.Padrao, Expressao.Feliz, 36, "depois do arraste, a de antes");
    }

    // Escondido (DEC-025) ou preso pelo usuário (DEC-024): a reação de antes, com a duração de antes, em qualquer energia.
    [Teste]
    public static void EscondidoOuPreso_ComoAntes()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Personalidade = true };
        var sim = new SimuladorDeTempo(cfg, 4, TopologiasDeExemplo.UmMonitor, new Preferencias(NivelDeEnergia.Baixa, true));
        PontoPx a = sim.Estado.Lugar!.Ancora;
        var p = new PontoPx(a.X, a.Y - 20);
        sim.Aplicar(new Press(p));
        sim.Aplicar(new Click());
        sim.Aplicar(new Press(p));
        sim.Aplicar(new DoubleClick());
        sim.Esta(Estado.Peeking, "escondido");
        sim.Avancar(TimeSpan.FromSeconds(2));
        PontoPx escondido = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(escondido.X, escondido.Y - 20)));
        sim.Aplicar(new Click());
        sim.Esta(Estado.Reacting);
        Afirmar.Igual((VarianteDaReacao.Padrao, Expressao.Feliz, cfg.PassosDaReacao), (sim.Estado.Reacao, sim.Estado.Expressao, sim.Estado.PassosRestantes), "escondido, na Baixa: a de antes");

        var preso = new SimuladorDeTempo(cfg, 4, TopologiasDeExemplo.UmMonitor, new Preferencias(NivelDeEnergia.Alta, true)).Semeado(s => s with { PresoPeloUsuario = true });
        PontoPx b = preso.Estado.Lugar!.Ancora;
        preso.Aplicar(new Press(new PontoPx(b.X, b.Y - 20)));
        preso.Aplicar(new Click());
        Afirmar.Igual((VarianteDaReacao.Padrao, Expressao.Feliz, cfg.PassosDaReacao), (preso.Estado.Reacao, preso.Estado.Expressao, preso.Estado.PassosRestantes), "preso, na Alta: a de antes");
    }

    // Por regra, sem sorteio: os geradores da paranoia e da personalidade não andam, e o principal anda igual ao de antes (o
    // reagendamento da agenda depois da acomodação, que já existia).
    [Teste]
    public static void NenhumSorteio()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta })
        {
            Cenario com = Parado(energia), sem = Parado(energia, new ConfiguracaoDoNucleo());
            (Aleatorio Paranoia, Aleatorio Personalidade) antes = (com.Atual.AleatorioDaParanoia, com.Atual.AleatorioDaPersonalidade);
            com.Clicar().AteOFim().Clicar().AteOFim();
            sem.Clicar().AteOFim().Clicar().AteOFim();
            Afirmar.Igual(antes, (com.Atual.AleatorioDaParanoia, com.Atual.AleatorioDaPersonalidade), $"{energia}: os geradores próprios parados");
            Afirmar.Igual(sem.Atual.Aleatorio, com.Atual.Aleatorio, $"{energia}: o principal como sem a personalidade");
        }
    }

    // Invariante 34 (reescrito pela DEC-037): durante REACTING, sem TOPOLOGY_CHANGED, a âncora e o monitor não mudam; e a
    // variante não muda o que vem depois: com e sem a personalidade, o mesmo lugar no fim da reação e depois da acomodação.
    [Teste]
    public static void Invariante34_AVarianteNaoMudaOLugar()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Alta })
        {
            Cenario com = Parado(energia).Clicar(), sem = Parado(energia, new ConfiguracaoDoNucleo()).Clicar();
            Posicionamento inicio = Afirmar.NaoNulo(com.Atual.Lugar);
            while (com.Atual.Estado == Estado.Reacting)
            {
                com.Passos(1);
                if (com.Atual.Estado == Estado.Reacting) Afirmar.Igual((inicio.Ancora, inicio.Monitor.Chave), (com.Ancora, com.Atual.Lugar!.Monitor.Chave), $"{energia}: parado na reação");
            }
            sem.AteOFim();
            com.AteOFim();
            Afirmar.Igual(sem.Atual.Lugar, com.Atual.Lugar, $"{energia}: o mesmo lugar depois da acomodação");
        }
    }
}
