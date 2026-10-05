using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Fase 4 — movimento e superfícies (TODO.md): trajetórias com passo fixo, colisão contra as
/// superfícies das topologias de exemplo, apoio, pulo, queda, escalada, pendurar e pausa. O
/// relógio é virtual (<see cref="SimuladorDeTempo"/>); nada abre janela.
/// </summary>
internal static class MovimentoTestes
{
    /// <summary>Configuração da Fase 4: física ligada, queda animada, todas as ações.</summary>
    internal static ConfiguracaoDoNucleo Fase4(AcoesAutonomas acoes = AcoesAutonomas.Todas)
        => new() { Movimento = true, QuedaFisica = true, Acoes = acoes };

    private static Superficies Sup(EstadoDoNucleo s)
    {
        Posicionamento l = Afirmar.NaoNulo(s.Lugar, "lugar");
        return Superficies.Do(Afirmar.NaoNulo(s.Topologia, "topologia"), l.Monitor, l.Tamanho);
    }

    /// <summary>
    /// Critério 2: fora de JUMPING e FALLING (e dos estados do usuário), o personagem tem apoio —
    /// chão, lateral da área útil (toda lateral é escalável com a toon force, DEC-023) ou borda
    /// superior — e o sprite fica inteiro na área útil.
    /// </summary>
    internal static void ConferirApoio(EstadoDoNucleo s, string onde)
    {
        if (s.Lugar is not { } l || s.Topologia is null || !s.Estado.Visivel()) return;
        Superficies sup = Superficies.Do(s.Topologia, l.Monitor, l.Tamanho);
        switch (s.Estado)
        {
            case Estado.Idle or Estado.Walking or Estado.Landing or Estado.Resting:
                Afirmar.Igual(sup.Chao, l.Ancora.Y, $"{onde}: {s.Estado} com os pés no chão");
                break;
            case Estado.Climbing:
                bool naLateral = l.Ancora.X == sup.Esquerda || l.Ancora.X == sup.Direita;
                Afirmar.Verdadeiro(naLateral, $"{onde}: CLIMBING encostado numa lateral da área útil (âncora {l.Ancora}, limites {sup})");
                Afirmar.Verdadeiro(l.Ancora.Y >= sup.Teto && l.Ancora.Y <= sup.Chao, $"{onde}: CLIMBING na altura da parede ({l.Ancora.Y})");
                break;
            case Estado.Hanging:
                Afirmar.Igual(sup.Teto, l.Ancora.Y, $"{onde}: HANGING pendurado na borda superior");
                break;
        }
        // O sprite fica inteiro na área útil quando cabe nela; numa área útil menor que o sprite,
        // os pés ficam no chão e ele fica centralizado (Posicionador.PrenderNaAreaUtil).
        RetanguloPx area = l.Monitor.AreaUtil;
        bool cabe = l.Tamanho.Largura <= area.Largura && l.Tamanho.Altura <= area.Altura;
        // No meio de uma travessia em curso (Fase 5, passo P13: andando, ou no voo do salto de degrau), o sprite fica montado
        // entre dois monitores, inteiro na união das áreas úteis (invariante 21).
        bool emTravessia = s.Movimento.Travessia is { } tr && (s.Estado == Estado.Jumping || (s.Estado == Estado.Walking && tr.Tipo == TipoDeTravessia.Andando));
        if (emTravessia)
            Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(s.Topologia, l.Retangulo), $"{onde}: {s.Estado} atravessando com o sprite {l.Retangulo} na união das áreas úteis");
        else if (cabe && s.Estado.Grupo() is GrupoDoEstado.Autonomo or GrupoDoEstado.Fisico)
            Afirmar.Verdadeiro(area.Contem(l.Retangulo), $"{onde}: {s.Estado} com o sprite {l.Retangulo} inteiro na área útil {area}");
    }

    [Teste]
    public static void Caminhada_AndaSoPeloChaoEmPassosFixosEParaNoFimDoPercurso()
    {
        var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Andar), 11, TopologiasDeExemplo.UmMonitor);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Walking);
        sim.Esta(Estado.Walking);
        int passoEsperado = 0;
        PontoPx anterior = sim.Estado.Lugar!.Ancora;
        int passos = 0;
        double andado = 0;
        while (sim.Estado.Estado == Estado.Walking && passos < 3600)
        {
            sim.Passos(1);
            passos++;
            PontoPx agora = sim.Estado.Lugar!.Ancora;
            ConferirApoio(sim.Estado, $"passo {passos}");
            int dx = Math.Abs(agora.X - anterior.X);
            // 90 DIP/s a 96 DPI e 60 passos/s = 1,5 px por passo: 1 ou 2 px, nunca mais.
            Afirmar.Verdadeiro(dx <= 2, $"passo {passos}: avanço de {dx} px, maior que o passo esperado");
            passoEsperado += dx;
            andado += dx;
            anterior = agora;
        }
        Afirmar.Diferente(Estado.Walking, sim.Estado.Estado, "a caminhada termina");
        Afirmar.Verdadeiro(andado > 0, "andou");
    }

    [Teste]
    public static void MonitorMaisEstreitoQueOSprite_AsLateraisFicamOndeAValidacaoOPoe()
    {
        // 240 px de largura a 240 DPI: o sprite de 128 DIP tem 320 px e não cabe. A validação o centraliza
        // (Posicionador.PrenderNaAreaUtil: 0 + (240 - 320) / 2 + 160 = 120), e as duas laterais ficam ali também; senão,
        // ele começaria a escalar no meio, fora da parede, e a física o puxaria 40 px no passo seguinte.
        var estreito = new Topologia([TopologiasDeExemplo.Principal("E", TopologiasDeExemplo.Ret(0, 0, 240, 1732), TopologiasDeExemplo.Ret(0, 0, 240, 1612), 240)]);
        TamanhoPx sprite = new TamanhoDip(128, 128).ParaPixels(240);
        Superficies sup = Superficies.Do(estreito, estreito.Principal, sprite);
        int x = Posicionador.PrenderNaAreaUtil(new PontoPx(0, 1612), sprite, estreito.Principal.AreaUtil).X;
        Afirmar.Igual((120, 120, 120), (x, sup.Esquerda, sup.Direita), "as laterais e a validação no mesmo x");

        var c = new Personagem.Cenario(Fase4()).Aplicar(new Loaded(estreito, null, Preferencias.Padrao));
        c.AplicarCom(Fase4(AcoesAutonomas.Escalar), new AutonomyTimer(c.Atual.Geracao)).Esta(Estado.Climbing);
        ConferirApoio(c.Atual, "decidiu escalar no monitor estreito");
    }

    [Teste]
    public static void Passagem_NaoEhAtravessadaNaFase4MasSeEscaladaComToonForce()
    {
        // Secundário à esquerda: a lateral esquerda do principal (x = 0) é passagem. A travessia é
        // da Fase 5; com a toon force (DEC-023) ele sobe por ela como por uma parede.
        Topologia t = TopologiasDeExemplo.SecundarioAEsquerda;
        Superficies sup = Superficies.Do(t, t.Principal, new TamanhoDip(128, 128).ParaPixels(96));
        Afirmar.Verdadeiro(sup.PassagemEsquerda, "a lateral encostada no secundário é passagem");
        Afirmar.Falso(sup.PassagemDireita, "a lateral livre é parede");

        int subiuNaPassagem = 0;
        for (ulong semente = 1; semente <= 40; semente++)
        {
            var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Andar | AcoesAutonomas.Escalar), semente, t);
            bool subiu = false;
            sim.AoAplicar = (_, e, depois) =>
            {
                if (depois.Lugar is { } l)
                {
                    Afirmar.Igual(TopologiasDeExemplo.Display1, l.Monitor.Chave, $"semente {semente}: continua no principal (travessia é da Fase 5)");
                    if (depois.Estado == Estado.Climbing && l.Ancora.X == sup.Esquerda) subiu = true;
                }
                ConferirApoio(depois, $"semente {semente}, {e}");
            };
            sim.Avancar(TimeSpan.FromMinutes(4));
            if (subiu) subiuNaPassagem++;
        }
        Afirmar.Verdadeiro(subiuNaPassagem > 0, $"subiu pela lateral encostada no outro monitor em {subiuNaPassagem} de 40 sementes");
    }

    // ---------------------------------------------------------------- toon force (DEC-023)

    /// <summary>Solta o personagem com a âncora <paramref name="altura"/> DIP acima do chão, no meio da área útil.</summary>
    private static SimuladorDeTempo SoltarDoAlto(int altura, AcoesAutonomas acoes = AcoesAutonomas.Nenhuma, ulong semente = 3, bool pausado = false)
    {
        var sim = new SimuladorDeTempo(Fase4(acoes), semente, TopologiasDeExemplo.UmMonitor);
        if (pausado) sim.Aplicar(new CmdPauseAutonomy());
        RetanguloPx area = sim.Estado.Lugar!.Monitor.AreaUtil;
        PontoPx a = sim.Estado.Lugar.Ancora;
        var pegada = new PontoPx(a.X, a.Y - 30);
        var alvo = new PontoPx(area.Esquerda + area.Largura / 2, area.Base - altura - 30);
        sim.Aplicar(new Press(pegada));
        sim.Aplicar(new DragStart());
        sim.Aplicar(new DragMove(alvo));
        sim.Aplicar(new DragEnd(alvo));
        sim.Esta(Estado.Falling, "solto no ar, cai");
        return sim;
    }

    [Teste]
    public static void ToonForce_QuedaAltaQuicaComoBorrachaRindoEDepoisPousa()
    {
        SimuladorDeTempo sim = SoltarDoAlto(600);
        Superficies sup = Sup(sim.Estado);
        var alturas = new List<int>();
        int topoDoQuique = int.MaxValue, passos = 0;
        while (sim.Estado.Estado is Estado.Falling or Estado.Jumping && passos++ < 2000)
        {
            Estado antes = sim.Estado.Estado;
            int quiquesAntes = sim.Estado.Movimento.Quiques;
            sim.Passos(1);
            ConferirApoio(sim.Estado, $"passo {passos}");
            if (sim.Estado.Movimento.Quiques > quiquesAntes)
            {
                Afirmar.Igual(Estado.Jumping, sim.Estado.Estado, "o quique é um pulo");
                Afirmar.Igual(sup.Chao, sim.Estado.Lugar!.Ancora.Y, "quica a partir do chão");
                Afirmar.Igual(Expressao.Rindo, sim.Estado.Expressao, "quica rindo, como o Luffy");
                if (quiquesAntes > 0) alturas.Add(sup.Chao - topoDoQuique);
                topoDoQuique = int.MaxValue;
            }
            if (sim.Estado.Estado == Estado.Jumping) topoDoQuique = Math.Min(topoDoQuique, sim.Estado.Lugar!.Ancora.Y);
            if (antes == Estado.Jumping && sim.Estado.Estado == Estado.Landing && topoDoQuique != int.MaxValue) alturas.Add(sup.Chao - topoDoQuique);
        }
        var quiques = sim.Transicoes.Where(t => t.Regra.Contains("quique de borracha", StringComparison.Ordinal)).ToList();
        Afirmar.Igual(2, quiques.Count, $"quica duas vezes ({string.Join(", ", sim.Transicoes.TakeLast(8))})");
        Afirmar.Igual(Estado.Falling, quiques[0].De, "o primeiro quique vem da queda");
        Afirmar.Igual(2, alturas.Count, $"alturas dos quiques: {string.Join(", ", alturas)}");
        Afirmar.Verdadeiro(alturas[0] > 40 && alturas[1] < alturas[0], $"cada quique mais baixo que o anterior: {string.Join(", ", alturas)} px");
        sim.Esta(Estado.Landing, "depois dos quiques, pousa");
        Afirmar.Igual(0, sim.Estado.Movimento.Quiques, "a contagem zera ao pousar");
        sim.Passos(60);
        sim.Esta(Estado.Idle);
        Afirmar.Falso(sim.RelogioLigado, "parado, sem relógio");
    }

    [Teste]
    public static void ToonForce_QuedaBaixaOuComAutonomiaPausadaNaoQuica()
    {
        foreach ((int altura, bool pausado, string caso) in new[] { (40, false, "queda de 40 DIP"), (600, true, "autonomia pausada") })
        {
            SimuladorDeTempo sim = SoltarDoAlto(altura, pausado: pausado);
            sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Landing);
            sim.Esta(Estado.Landing, caso);
            Afirmar.Falso(sim.Transicoes.Any(t => t.Para == Estado.Jumping), $"{caso}: pousa sem quicar");
        }
    }

    [Teste]
    public static void ToonForce_PressionarNoMeioDoQuiqueSeguraNaHora()
    {
        SimuladorDeTempo sim = SoltarDoAlto(600);
        sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Jumping && s.Movimento.Quiques == 1);
        sim.Passos(10);
        sim.Esta(Estado.Jumping, "no meio do primeiro quique");
        PontoPx noAr = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(noAr.X, noAr.Y - 40)));
        sim.Esta(Estado.Pressed);
        Afirmar.Falso(sim.RelogioLigado, "segurado: nada se move");
        sim.Avancar(TimeSpan.FromSeconds(2));
        Afirmar.Igual(noAr, sim.Estado.Lugar!.Ancora, "continua onde foi segurado");
    }

    [Teste]
    public static void ToonForce_FogueteDisparaParedeAcimaEPendura()
    {
        // Energia Alta: metade das subidas a partir do chão são foguetes.
        int foguetes = 0;
        for (ulong semente = 1; semente <= 60 && foguetes < 3; semente++)
        {
            var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.UmMonitor, new Preferencias(NivelDeEnergia.Alta, true));
            sim.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Climbing);
            if (sim.Estado.Estado != Estado.Climbing || !sim.Estado.Movimento.Foguete) continue;
            foguetes++;
            Superficies sup = Sup(sim.Estado);
            int passos = 0;
            while (sim.Estado.Estado == Estado.Climbing && passos++ < 600)
            {
                sim.Passos(1);
                ConferirApoio(sim.Estado, $"semente {semente}, foguete, passo {passos}");
            }
            sim.Esta(Estado.Hanging, $"semente {semente}: o foguete termina pendurado na borda superior");
            // 1000 DIP/s a 96 DPI e 60 passos/s: 16,7 px por passo.
            int maximo = (int)Math.Ceiling((sup.Chao - sup.Teto) / (1000.0 / 60)) + 2;
            Afirmar.Verdadeiro(passos <= maximo, $"semente {semente}: subiu em {passos} passos (no máximo {maximo}; escalando levaria {(sup.Chao - sup.Teto) / (110.0 / 60):0})");
        }
        Afirmar.Verdadeiro(foguetes >= 3, $"foguetes vistos: {foguetes}");
    }

    [Teste]
    public static void ToonForce_ChanceDoFogueteSegueAEnergiaComAMesmaVelocidade()
    {
        var chance = new Dictionary<NivelDeEnergia, double>();
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            int subidas = 0, foguetes = 0;
            for (ulong semente = 1; semente <= 200; semente++)
            {
                var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.UmMonitor, new Preferencias(nivel, true));
                sim.Avancar(TimeSpan.FromMinutes(3), s => s.Estado == Estado.Climbing);
                if (sim.Estado.Estado != Estado.Climbing) continue;
                subidas++;
                if (sim.Estado.Movimento.Foguete) foguetes++;
            }
            chance[nivel] = subidas == 0 ? 0 : (double)foguetes / subidas;
        }
        string texto = string.Join("; ", chance.Select(c => $"{c.Key}: {c.Value:P0}"));
        Console.WriteLine("         foguetes por subida: " + texto);
        Afirmar.Verdadeiro(chance[NivelDeEnergia.Baixa] < chance[NivelDeEnergia.Media] && chance[NivelDeEnergia.Media] < chance[NivelDeEnergia.Alta], texto);
        Afirmar.Igual(1000.0, new ConfiguracaoDoNucleo().Fisica.VelocidadeDoFoguete, "a velocidade do foguete não depende da energia");
    }

    [Teste]
    public static void Escalada_SobePenduraAtravessaOTetoEVoltaAoChaoSemFicarPreso()
    {
        // Critério 7: subir até a borda superior, ficar pendurado, percorrer a borda e voltar ou se soltar.
        int completas = 0;
        for (ulong semente = 1; semente <= 40; semente++)
        {
            var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.UmMonitor);
            var estados = new List<Estado>();
            sim.AoAplicar = (_, e, depois) =>
            {
                ConferirApoio(depois, $"semente {semente}, {e}");
                if (estados.Count == 0 || estados[^1] != depois.Estado) estados.Add(depois.Estado);
            };
            sim.Avancar(TimeSpan.FromMinutes(4));
            bool subiuEPendurou = estados.IndexOf(Estado.Climbing) is int i && i >= 0 && estados.IndexOf(Estado.Hanging) > i;
            if (subiuEPendurou && estados.LastIndexOf(Estado.Idle) > estados.IndexOf(Estado.Hanging)) completas++;
            // Nunca preso: o personagem sempre volta a IDLE (ou descansa) dentro do tempo simulado.
            Afirmar.Verdadeiro(estados.Count(e => e == Estado.Idle) >= 2, $"semente {semente}: voltou ao chão ({string.Join(" > ", estados)})");
        }
        Afirmar.Verdadeiro(completas >= 10, $"escalada, pendurar e volta ao chão completos em {completas} de 40 sementes");
    }

    [Teste]
    public static void Pulo_TrajetoriaBalisticaDeterministicaQuePousaNoChao()
    {
        var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Pular), 5, TopologiasDeExemplo.UmMonitor);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Jumping);
        sim.Esta(Estado.Jumping);
        Superficies sup = Sup(sim.Estado);
        int menorY = int.MaxValue;
        int passos = 0;
        while (sim.Estado.Estado == Estado.Jumping && passos < 600)
        {
            sim.Passos(1);
            passos++;
            menorY = Math.Min(menorY, sim.Estado.Lugar!.Ancora.Y);
            ConferirApoio(sim.Estado, $"passo {passos}");
        }
        Afirmar.Igual(Estado.Landing, sim.Estado.Estado, "pousou");
        Afirmar.Igual(sup.Chao, sim.Estado.Lugar!.Ancora.Y, "pousou no chão");
        Afirmar.Verdadeiro(menorY < sup.Chao - 20, $"subiu antes de cair (menor y {menorY}, chão {sup.Chao})");
        sim.Passos(100);
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "LANDING termina em IDLE");
        Afirmar.Falso(sim.RelogioLigado, "parado, sem relógio");
    }

    [Teste]
    public static void PressionarNoMeioDoPuloOuDaQueda_SeguraNaHoraECaiDeOndeFoiSolto()
    {
        // Critério 3.
        var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Pular), 5, TopologiasDeExemplo.UmMonitor);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Jumping);
        sim.Passos(8);
        sim.Esta(Estado.Jumping);
        PontoPx noAr = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(noAr.X, noAr.Y - 40)));
        sim.Esta(Estado.Pressed);
        Afirmar.Igual(noAr, sim.Estado.Lugar!.Ancora, "segurado na hora, no mesmo ponto");
        Afirmar.Falso(sim.RelogioLigado, "segurado: sem relógio, nada se move");
        sim.Avancar(TimeSpan.FromSeconds(5));
        Afirmar.Igual(noAr, sim.Estado.Lugar!.Ancora, "continua segurado");

        // Clique: reage no ar e depois cai do ponto em que foi segurado.
        sim.Aplicar(new Click());
        sim.Esta(Estado.Reacting);
        sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Falling);
        sim.Esta(Estado.Falling);
        sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Idle);
        sim.Esta(Estado.Idle);
        Afirmar.Igual(Sup(sim.Estado).Chao, sim.Estado.Lugar!.Ancora.Y, "caiu até o chão");
    }

    [Teste]
    public static void MesmaSementeRepeteOsMesmosMovimentos()
    {
        // Critério 1.
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            string Rodar(ulong semente)
            {
                var sim = new SimuladorDeTempo(Fase4(), semente, TopologiasDeExemplo.UmMonitor, new Preferencias(nivel, true));
                var trilha = new List<string>();
                sim.AoAplicar = (_, _, s) => trilha.Add($"{s.Estado}{s.Lugar?.Ancora}");
                sim.Avancar(TimeSpan.FromMinutes(3));
                return string.Join(";", trilha);
            }
            Afirmar.Igual(Rodar(99), Rodar(99), $"{nivel}: mesma semente, mesma trajetória");
            Afirmar.Diferente(Rodar(99), Rodar(100), $"{nivel}: sementes diferentes, trajetórias diferentes");
        }
    }

    [Teste]
    public static void Energia_BaixaFazMenosEMenoresAcoesQueMediaEAltaMaisEMaisLongas()
    {
        // Critério 6: mesmas regras físicas; muda a frequência e o tamanho das ações.
        var resumo = new Dictionary<NivelDeEnergia, (double Acoes, double Andado, double TempoEmMovimento)>();
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            double acoes = 0, andado = 0, movimento = 0;
            for (ulong semente = 1; semente <= 30; semente++)
            {
                var sim = new SimuladorDeTempo(Fase4(), semente, TopologiasDeExemplo.UmMonitor, new Preferencias(nivel, true));
                sim.AoAplicar = (antes, e, depois) =>
                {
                    ConferirApoio(depois, $"{nivel}, semente {semente}");
                    if (antes.Lugar is { } a && depois.Lugar is { } d && depois.Estado == Estado.Walking) andado += Math.Abs(d.Ancora.X - a.Ancora.X);
                    if (e is Tick && depois.Estado.EmMovimento()) movimento += 1.0 / 60;
                };
                sim.Avancar(TimeSpan.FromMinutes(10));
                acoes += sim.Transicoes.Count(t => t.De == Estado.Idle && t.Regra.StartsWith("IDLE + AUTONOMY_TIMER", StringComparison.Ordinal));
            }
            resumo[nivel] = (acoes / 30, andado / 30, movimento / 30);
        }
        string texto = string.Join("; ", resumo.Select(k => $"{k.Key}: {k.Value.Acoes:0.0} ações, {k.Value.Andado:0} px andados, {k.Value.TempoEmMovimento:0} s em movimento por 10 min"));
        Console.WriteLine("         " + texto);
        Afirmar.Verdadeiro(resumo[NivelDeEnergia.Baixa].Acoes < resumo[NivelDeEnergia.Media].Acoes && resumo[NivelDeEnergia.Media].Acoes < resumo[NivelDeEnergia.Alta].Acoes, $"ações: {texto}");
        Afirmar.Verdadeiro(resumo[NivelDeEnergia.Baixa].TempoEmMovimento < resumo[NivelDeEnergia.Media].TempoEmMovimento
            && resumo[NivelDeEnergia.Media].TempoEmMovimento < resumo[NivelDeEnergia.Alta].TempoEmMovimento, $"tempo em movimento: {texto}");
    }

    [Teste]
    public static void Pausar_ParaACaminhadaDesceDaParedeESoltaDoTeto()
    {
        // Autonomia pausada (ARCHITECTURE.md 2.6): nada autônomo começa; o que está em curso termina num lugar estável.
        foreach (Estado alvo in new[] { Estado.Walking, Estado.Climbing, Estado.Hanging })
        {
            AcoesAutonomas acoes = alvo == Estado.Walking ? AcoesAutonomas.Andar : AcoesAutonomas.Escalar;
            bool achou = false;
            for (ulong semente = 1; semente <= 40 && !achou; semente++)
            {
                var sim = new SimuladorDeTempo(Fase4(acoes), semente, TopologiasDeExemplo.UmMonitor);
                sim.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == alvo);
                if (sim.Estado.Estado != alvo) continue;
                achou = true;
                sim.Aplicar(new CmdPauseAutonomy());
                sim.AoAplicar = (_, e, depois) => ConferirApoio(depois, $"{alvo} pausado, {e}");
                sim.Avancar(TimeSpan.FromMinutes(1));
                sim.Esta(Estado.Idle, $"{alvo} pausado termina parado no chão");
                Afirmar.Falso(sim.RelogioLigado, $"{alvo}: sem relógio depois de parar");
                Afirmar.Falso(sim.Estado.DecisaoAgendada, $"{alvo}: sem agenda com a autonomia pausada");
            }
            Afirmar.Verdadeiro(achou, $"alguma semente chegou a {alvo}");
        }
    }

    [Teste]
    public static void ApoioValeEmMilharesDePassosComInteracoesEMudancasDeTopologia()
    {
        // Critério 2 como propriedade: simulações longas, com o usuário pressionando, arrastando e
        // soltando, e com mudanças de topologia, conferindo o apoio depois de cada evento.
        var mestre = new Random(20261001);
        long eventos = 0;
        for (int n = 0; n < 60; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia t = gerador.NovaTopologia();
            var sim = new SimuladorDeTempo(Fase4(), (ulong)semente, t, new Preferencias((NivelDeEnergia)rnd.Next(3), true));
            sim.AoAplicar = (_, e, depois) =>
            {
                eventos++;
                ConferirApoio(depois, $"sequência {n} (semente {semente}), {e}");
            };
            for (int k = 0; k < 40; k++)
            {
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 30)));
                if (sim.Estado.Lugar is not { } l) continue;
                switch (rnd.Next(6))
                {
                    case 0:
                        var pegar = new PontoPx(l.Ancora.X, l.Ancora.Y - 30);
                        sim.Aplicar(new Press(pegar));
                        sim.Aplicar(new DragStart());
                        PontoPx alvo = gerador.Ponto(t);
                        sim.Aplicar(new DragMove(alvo));
                        sim.Aplicar(new DragEnd(alvo));
                        break;
                    case 1:
                        sim.Aplicar(new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)));
                        sim.Aplicar(new Click());
                        break;
                    case 2:
                        t = rnd.Next(3) == 0 ? gerador.NovaTopologia() : gerador.Mudar(t);
                        sim.Aplicar(new TopologyChanged(t));
                        break;
                    case 3:
                        sim.Aplicar(rnd.Next(2) == 0 ? new CmdPauseAutonomy() : new CmdResumeAutonomy());
                        break;
                }
            }
        }
        Console.WriteLine($"         {eventos} eventos conferidos em 60 simulações longas");
    }
}

internal static class ExtensoesDoSimulador
{
    public static SimuladorDeTempo Esta(this SimuladorDeTempo sim, Estado esperado, string? mensagem = null)
    {
        Afirmar.Igual(esperado, sim.Estado.Estado, mensagem ?? $"estado (últimas transições: {string.Join(", ", sim.Transicoes.TakeLast(4))})");
        return sim;
    }
}
