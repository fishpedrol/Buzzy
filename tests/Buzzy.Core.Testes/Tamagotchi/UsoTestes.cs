using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// O uso de um item (DEC-028, passo T5; estado USING, desenho do núcleo 4.6, com a crítica de integração): dura os passos
/// do verbo, com o relógio ligado e nada autônomo; um PRESS o segura na hora; esconder, topologia, tela cheia e sair só
/// acabam o uso, e a onda continua; no fim, a acomodação o devolve ao mesmo apoio: chão, parede ou cipó (agarrado, preso
/// se já estava) e esconderijo. Tudo é de desenho animado.
/// </summary>
internal static class UsoTestes
{
    // Cada um dos 13 itens: o uso dura exatamente os passos do verbo (a soma dos quadros da arte), com o retrato mostrando o
    // uso e o passo, a cara de quem usa e o relógio ligado; o AUTONOMY_TIMER é descartado pelo Nucleo (grupo usuário); no
    // último passo, SETTLING e IDLE, sem relógio, com a agenda de volta e a cara de base (a da fase da onda; na água, sem
    // onda e sem emoção dominante, a de quem usou fica, como depois de uma reação).
    [Teste]
    public static void Usar_DuraOsPassosDoItem()
    {
        foreach (Item item in TabelaDoTamagotchi.Itens)
        {
            DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
            Cenario c = Cenario.Parado(SemFisica());
            ItemNoMundo it = InvocarEAssentar(c, item);
            c.SoltarSobreEle(it.Id).Esta(Estado.Using, $"{item}: usando");
            Afirmar.Igual(new Uso(item, dados.Verbo, dados.PassosDoUso, ApoioDoUso.Chao), c.Retrato.Uso, $"{item}: o uso no retrato");
            Afirmar.Igual((0, dados.CaraDurante), (c.Retrato.PassoDoUso, c.Atual.Expressao), $"{item}: o primeiro passo, com a cara de quem usa");
            Afirmar.Verdadeiro(c.Atual.RelogioAtivo && c.Tem<LigarRelogio>(), $"{item}: o relógio liga");
            Afirmar.Verdadeiro(c.Efeitos.Contains(new RemoverItem(it.Id, MotivoDaRemocao.Usado)), $"{item}: a janela do item fecha");
            Afirmar.Igual(dados.Onda is not null, c.Tem<AgendarOnda>(), $"{item}: a onda começa no soltar (a água não tem onda)");
            Afirmar.Falso(new Nucleo(c.Config, c.Atual).Enfileirar(new AutonomyTimer(c.Atual.Geracao)), $"{item}: o Nucleo descarta a agenda em USING");

            for (int passo = 1; passo < dados.PassosDoUso; passo++)
            {
                c.Aplicar(new Tick()).SemTransicao();
                Afirmar.Igual(passo, c.Retrato.PassoDoUso, $"{item}: passo {passo}");
                Afirmar.Igual(dados.CaraDurante, c.Atual.Expressao, $"{item}: passo {passo}, a cara de quem usa");
            }
            c.Aplicar(new Tick()).Percorreu(Estado.Using, Estado.Settling, Estado.Idle);
            Afirmar.Igual($"USING: fim do uso de {item}", c.Transicoes[0].Regra, $"{item}: a regra do fim");
            Afirmar.Nulo(c.Atual.Uso, $"{item}: o uso acabou");
            Afirmar.Verdadeiro(!c.Atual.RelogioAtivo && c.Tem<DesligarRelogio>() && c.Tem<AgendarDecisao>(), $"{item}: sem relógio, com a agenda");
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{item}: no mesmo lugar");
            Expressao esperada = dados.Onda is { } onda ? TabelaDoTamagotchi.DaOnda(onda).Cara(FaseDaOnda.Subida) : dados.CaraDurante;
            Afirmar.Igual(esperada, c.Atual.Expressao, $"{item}: a cara de base no fim");
        }
    }

    // DEC-024 com DEC-028: preso pelo usuário na parede, ele usa o item ali e volta grudado e preso, no mesmo lugar.
    [Teste]
    public static void Usar_NaParedePresa_VoltaGrudadaEPresa()
    {
        var sim = new SimuladorDeTempo(ComFisica(), 6, TopologiasDeExemplo.UmMonitor);
        Superficies sup = Sup(sim.Estado);
        SoltarPersonagemEm(sim, new PontoPx(sup.Direita - 20, 600));
        sim.Esta(Estado.Climbing, "grudado na parede");
        Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, "preso e agarrado");
        PontoPx naParede = sim.Estado.Lugar!.Ancora;

        ItemNoMundo item = InvocarEAssentar(sim, Item.Cafe);
        SoltarSobreEle(sim, item.Id);
        sim.Esta(Estado.Using, "usando na parede");
        Afirmar.Igual(ApoioDoUso.Parede, sim.Estado.Uso!.Apoio, "o apoio é a parede");
        TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
        Afirmar.Sequencia([Estado.Settling, Estado.Climbing], fim.Select(t => t.Para), "volta à parede");
        Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, "grudado e preso");
        Afirmar.Igual(naParede, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
        Afirmar.Falso(sim.Estado.RelogioAtivo, "parado, sem relógio");
    }

    // DEC-024 com DEC-028: no cipó, ele usa o item ali e volta agarrado ao cipó, preso, no mesmo lugar.
    [Teste]
    public static void Usar_NoCipo_VoltaAoCipo()
    {
        var sim = new SimuladorDeTempo(ComFisica(), 6, TopologiasDeExemplo.UmMonitor);
        Superficies sup = Sup(sim.Estado);
        SoltarPersonagemEm(sim, new PontoPx(960, sup.Teto + 12));
        sim.Esta(Estado.Hanging, "agarrado ao cipó");
        PontoPx noCipo = sim.Estado.Lugar!.Ancora;

        ItemNoMundo item = InvocarEAssentar(sim, Item.Cerveja);
        SoltarSobreEle(sim, item.Id);
        sim.Esta(Estado.Using, "usando no cipó");
        Afirmar.Igual(ApoioDoUso.Cipo, sim.Estado.Uso!.Apoio, "o apoio é o cipó");
        TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
        Afirmar.Sequencia([Estado.Settling, Estado.Hanging], fim.Select(t => t.Para), "volta ao cipó");
        Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, "agarrado e preso");
        Afirmar.Igual(noCipo, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
    }

    // Na quina de cima, ao alcance da parede e do cipó, quem usava o item na parede volta para a parede (a acomodação, sem
    // o apoio do uso, escolheria o cipó, mais perto).
    [Teste]
    public static void Usar_NaQuinaDaParede_VoltaParaAParede()
    {
        for (ulong semente = 1; semente <= 40; semente++)
        {
            var sim = new SimuladorDeTempo(ComFisica() with { Acoes = AcoesAutonomas.Nenhuma }, semente, TopologiasDeExemplo.UmMonitor);
            Superficies sup = Sup(sim.Estado);
            // Na parede, um pouco abaixo do alcance do cipó: o passeio de quem está preso pode levá-lo até a quina.
            SoltarPersonagemEm(sim, new PontoPx(sup.Direita - 5, sup.Teto + 97));
            if (sim.Estado.Estado != Estado.Climbing) continue;
            sim.Avancar(TimeSpan.FromMinutes(20), s => s.Estado == Estado.Climbing && s.Movimento.Agarrado && s.Lugar!.Ancora.Y == sup.Teto);
            if (sim.Estado.Lugar!.Ancora.Y != sup.Teto || !sim.Estado.Movimento.Agarrado) continue;

            sim.Esta(Estado.Climbing, "na quina, ainda na parede");
            ItemNoMundo item = InvocarEAssentar(sim, Item.Bala);
            SoltarSobreEle(sim, item.Id);
            Afirmar.Igual(ApoioDoUso.Parede, sim.Estado.Uso!.Apoio, "na quina, o apoio é a parede");
            TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
            Afirmar.Sequencia([Estado.Settling, Estado.Climbing], fim.Select(t => t.Para), "volta para a parede, e não para o cipó");
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, "grudado e preso");
            Afirmar.Igual(new PontoPx(sup.Direita, sup.Teto), sim.Estado.Lugar!.Ancora, "na quina");
            return;
        }
        Afirmar.Falhar("nenhuma semente levou o preso até a quina");
    }

    // Tabela 4.6 e invariante 24, perto do chão: escalando por conta própria (sem estar preso), a menos da altura mínima
    // para agarrar (32 DIP) do chão, subindo ou descendo, ele fica atento ao item (agarrado ali) e o usa na parede; no
    // fim do uso, volta agarrado à parede, no mesmo lugar e sem ficar preso, em vez de cair. A altura mínima vale para
    // quem é solto ali, não para quem já estava na parede (achado 1 da revisão adversarial).
    [Teste]
    public static void Usar_NaParedePertoDoChao_VoltaAParede()
    {
        foreach (int sentido in new[] { -1, 1 })
        {
            string caso = sentido < 0 ? "subindo" : "descendo";
            bool achou = false;
            for (ulong semente = 1; semente <= 120 && !achou; semente++)
            {
                var sim = new SimuladorDeTempo(ComFisica() with { Acoes = AcoesAutonomas.Escalar }, semente, TopologiasDeExemplo.UmMonitor);
                ItemNoMundo item = InvocarEAssentar(sim, Item.Cafe);
                Superficies sup = Sup(sim.Estado);
                double minimo = sim.Nucleo.Configuracao.Fisica.AlturaMinimaParaAgarrar;
                bool PertoDoChao(EstadoDoNucleo s) => s.Estado == Estado.Climbing && !s.Movimento.Agarrado && !s.Movimento.Foguete && !s.PresoPeloUsuario
                    && s.Movimento.SentidoVertical == sentido && s.Lugar is { } l && sup.Chao - l.Ancora.Y > 0 && sup.Chao - l.Ancora.Y < minimo;
                sim.Avancar(TimeSpan.FromMinutes(5), PertoDoChao);
                if (!PertoDoChao(sim.Estado)) continue;
                achou = true;
                PontoPx naParede = sim.Estado.Lugar!.Ancora;

                SoltarSobreEle(sim, item.Id);
                sim.Esta(Estado.Using, $"{caso}: usando na parede, {sup.Chao - naParede.Y} px acima do chão");
                Afirmar.Igual(ApoioDoUso.Parede, sim.Estado.Uso!.Apoio, $"{caso}: o apoio é a parede");
                TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
                Afirmar.Sequencia([Estado.Settling, Estado.Climbing], fim.Select(t => t.Para), $"{caso}: volta à parede, sem cair");
                Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado && !sim.Estado.PresoPeloUsuario, $"{caso}: agarrado, e não preso (não foi o usuário que o pôs lá)");
                Afirmar.Igual(naParede, sim.Estado.Lugar!.Ancora, $"{caso}: no mesmo lugar");
                Afirmar.Falso(sim.RelogioLigado, $"{caso}: parado, sem relógio");
            }
            Afirmar.Verdadeiro(achou, $"{caso}: alguma semente o deixou escalando perto do chão");
        }
    }

    // Tabela 4.6 e invariante 24, no alto por conta própria (sem estar preso: ele escalou sozinho): bem acima do chão, na
    // parede, ou pendurado no cipó, ele fica atento ao item (agarrado ali) e o usa no mesmo apoio; no fim do uso, volta a
    // ele, agarrado, no mesmo lugar e sem ficar preso. A simulação longa com itens (OndaTestes) só chega a esses usos por
    // sorte; estes são os casos determinísticos (o de perto do chão fica em Usar_NaParedePertoDoChao_VoltaAParede).
    [Teste]
    public static void Usar_NoAltoPorContaPropria_VoltaAoMesmoApoio()
    {
        var achados = new List<string>();
        foreach (bool noCipo in new[] { false, true })
        {
            string caso = noCipo ? "no cipó" : "na parede";
            Estado apoio = noCipo ? Estado.Hanging : Estado.Climbing;
            bool NoAlto(EstadoDoNucleo s) => s.Estado == apoio && !s.PresoPeloUsuario && !s.Movimento.Agarrado && !s.Movimento.Foguete
                && !s.Itens.AlgumCaindo && s.Lugar is { } l && Sup(s).Chao - l.Ancora.Y > 300;
            bool achou = false;
            for (ulong semente = 1; semente <= 80 && !achou; semente++)
            {
                var sim = new SimuladorDeTempo(ComFisica() with { Acoes = AcoesAutonomas.Escalar }, semente, TopologiasDeExemplo.UmMonitor);
                ItemNoMundo item = InvocarEAssentar(sim, Item.Banana);
                sim.Avancar(TimeSpan.FromMinutes(5), NoAlto);
                if (!NoAlto(sim.Estado)) continue;
                achou = true;
                PontoPx noAltoDoApoio = sim.Estado.Lugar!.Ancora;
                achados.Add($"{caso}, semente {semente}, {Sup(sim.Estado).Chao - noAltoDoApoio.Y} px acima do chão");

                SoltarSobreEle(sim, item.Id);
                sim.Esta(Estado.Using, $"{caso}: usando");
                Afirmar.Igual(noCipo ? ApoioDoUso.Cipo : ApoioDoUso.Parede, sim.Estado.Uso!.Apoio, $"{caso}: o apoio do uso");
                TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
                Afirmar.Sequencia([Estado.Settling, apoio], fim.Select(t => t.Para), $"{caso}: volta ao mesmo apoio");
                Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado && !sim.Estado.PresoPeloUsuario, $"{caso}: agarrado, e não preso (não foi o usuário que o pôs lá)");
                Afirmar.Igual(noAltoDoApoio, sim.Estado.Lugar!.Ancora, $"{caso}: no mesmo lugar");
                Afirmar.Falso(sim.RelogioLigado, $"{caso}: parado, sem relógio");
            }
            Afirmar.Verdadeiro(achou, $"{caso}: alguma semente o levou ao alto por conta própria");
        }
        Console.WriteLine($"         {string.Join("; ", achados)}");
    }

    // Tabela 4.6 com a física, o caso que a simulação longa não alcança (pendência do T5): reagindo a um clique no meio de
    // uma queda, longe das laterais e da borda de cima (toon force: ele flutua enquanto reage), ele aceita o item solto
    // sobre ele; o apoio, pela geometria, é o chão, e o uso acontece ali, parado no ar. No fim, a acomodação decide: sem
    // apoio e sem nada para agarrar, ele cai, pousa e fica parado no chão, na mesma coluna, sem ficar preso. (Pegar um
    // item com ele reagindo pede dois ponteiros; o núcleo não depende disso.)
    [Teste]
    public static void Usar_ReagindoNoAr_TerminaPelaAcomodacao()
    {
        var sim = new SimuladorDeTempo(ComFisica() with { Acoes = AcoesAutonomas.Nenhuma }, 5, TopologiasDeExemplo.UmMonitor);
        ItemNoMundo item = InvocarEAssentar(sim, Item.Banana);
        Superficies sup = Sup(sim.Estado);
        SoltarPersonagemEm(sim, new PontoPx(960, 500));
        sim.Esta(Estado.Falling, "solto no ar, longe das bordas, cai");
        sim.Passos(5);
        PontoPx noAr = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(noAr.X, noAr.Y - 20)));
        sim.Aplicar(new Click());
        sim.Esta(Estado.Reacting, "reage no ar");
        Afirmar.Verdadeiro(noAr.Y < sup.Chao - 300, $"bem acima do chão ({noAr})");

        SoltarSobreEle(sim, item.Id);
        sim.Esta(Estado.Using, "usa no ar, cortando a reação");
        Afirmar.Igual(ApoioDoUso.Chao, sim.Estado.Uso!.Apoio, "o apoio, pela geometria, é o chão");
        Afirmar.Igual(noAr, sim.Estado.Lugar!.Ancora, "parado onde reagia");
        TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
        Afirmar.Sequencia([Estado.Settling, Estado.Falling], fim.Select(t => t.Para), "no fim, a acomodação: sem apoio, cai");
        sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Idle);
        sim.Esta(Estado.Idle, "pousou");
        Afirmar.Igual(new PontoPx(noAr.X, sup.Chao), sim.Estado.Lugar!.Ancora, "no chão, na mesma coluna");
        Afirmar.Falso(sim.Estado.PresoPeloUsuario || sim.RelogioLigado, "sem ficar preso, e sem relógio");
    }

    // DEC-025 com DEC-028: escondido atrás da borda de baixo ou de uma lateral, ele usa o item ali e continua escondido, na
    // mesma borda.
    [Teste]
    public static void Usar_Escondido_VoltaAoEsconderijo()
    {
        foreach (bool lateral in new[] { false, true })
        {
            var sim = new SimuladorDeTempo(ComFisica(), 4, TopologiasDeExemplo.UmMonitor);
            if (lateral) SoltarPersonagemEm(sim, new PontoPx(Sup(sim.Estado).Direita - 20, 600));
            Posicionamento l = sim.Estado.Lugar!;
            var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 20);
            foreach (Evento e in new Evento[] { new Press(corpo), new Click(), new Press(corpo), new DoubleClick() }) sim.Aplicar(e);
            sim.Esta(Estado.Peeking, "escondido");
            LadoDoEsconderijo lado = sim.Estado.Esconderijo;
            Afirmar.Igual(lateral ? LadoDoEsconderijo.Direita : LadoDoEsconderijo.Baixo, lado, "a borda");
            PontoPx escondido = sim.Estado.Lugar!.Ancora;

            ItemNoMundo item = InvocarEAssentar(sim, Item.Cigarro);
            SoltarSobreEle(sim, item.Id);
            sim.Esta(Estado.Using, $"usando escondido ({lado})");
            Afirmar.Igual(ApoioDoUso.Esconderijo, sim.Estado.Uso!.Apoio, "o apoio é o esconderijo");
            TerminarOUso(sim, out IReadOnlyList<Transicao> fim);
            Afirmar.Sequencia([Estado.Settling, Estado.Peeking], fim.Select(t => t.Para), $"{lado}: volta ao esconderijo");
            Afirmar.Igual((lado, escondido), (sim.Estado.Esconderijo, sim.Estado.Lugar!.Ancora), $"{lado}: na mesma borda, no mesmo lugar");
        }
    }

    /// <summary>Passos do relógio até o uso acabar; devolve as transições do último passo.</summary>
    private static void TerminarOUso(SimuladorDeTempo sim, out IReadOnlyList<Transicao> fim)
    {
        int passos = sim.Estado.PassosRestantes;
        IReadOnlyList<Transicao> ultimas = [];
        sim.AoResultado = (_, _, r) => ultimas = r.Transicoes;
        for (int i = 0; i < passos; i++)
        {
            Afirmar.Igual(Estado.Using, sim.Estado.Estado, $"passo {i + 1} de {passos}: ainda usando");
            Afirmar.Igual(1, sim.Passos(1), "o relógio corre no uso");
        }
        sim.AoResultado = null;
        Afirmar.Diferente(Estado.Using, sim.Estado.Estado, "o uso acabou no último passo");
        Afirmar.Igual(Estado.Using, ultimas[0].De, "pela regra do fim do uso");
        fim = ultimas;
    }

    // Invariante 24 e C16: em USING, um PRESS o segura no mesmo evento; o uso acaba, mas a onda continua, sem cancelar nem
    // reagendar o temporizador dela.
    [Teste]
    public static void Usar_PressNoMeio_SeguraNaHoraEOndaContinua()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo item = InvocarEAssentar(c, Item.Vodka);
        c.SoltarSobreEle(item.Id).Esta(Estado.Using);
        c.Passos(40);
        EstadoDaOnda? onda = c.Atual.Onda;
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2), onda, "a onda do bêbado começou no soltar");
        c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Using, Estado.Pressed);
        Afirmar.Nulo(c.Atual.Uso, "o uso acabou");
        Afirmar.Igual(onda, c.Atual.Onda, "a onda continua");
        Afirmar.Falso(c.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "o temporizador da onda não muda");
        Afirmar.Verdadeiro(c.Atual.OndaAgendada, "e continua pendente");
        c.Aplicar(new Click()).Esta(Estado.Reacting, "o clique vira reação, como sempre");
    }

    // C16: esconder (pela bandeja, pelo bloqueio ou pela suspensão), mudar a topologia, a tela cheia no monitor dele,
    // redefinir a posição e sair acabam o uso; a onda continua (saindo, o temporizador dela é cancelado, como sempre).
    [Teste]
    public static void Usar_EsconderOuTopologiaNoMeio_EncerraSemPerderAOnda()
    {
        (string Caso, Evento Evento, Estado Destino)[] casos =
        [
            ("CMD_HIDE", new CmdHide(), Estado.Hidden),
            ("SESSION_LOCKED", new SessionLocked(), Estado.Hidden),
            ("SUSPENDING", new Suspending(), Estado.Hidden),
            ("TOPOLOGY_CHANGED", new TopologyChanged(TopologiasDeExemplo.ComMonitor(TopologiasDeExemplo.LadoALado, TopologiasDeExemplo.Display1,
                m => m with { AreaUtil = TopologiasDeExemplo.Ret(0, 0, 1920, 1000) })), Estado.Idle),
            ("FULLSCREEN_TARGETS_CHANGED", new FullscreenTargetsChanged(new MonitoresOcupados([TopologiasDeExemplo.Display1])), Estado.Idle),
            ("CMD_RESET_POSITION", new CmdResetPosition(), Estado.Idle),
            ("CMD_EXIT", new CmdExit(), Estado.Exiting),
        ];
        foreach ((string caso, Evento evento, Estado destino) in casos)
        {
            Cenario c = Cenario.Parado(SemFisica(), TopologiasDeExemplo.LadoALado);
            ItemNoMundo item = InvocarEAssentar(c, Item.Baseado);
            c.SoltarSobreEle(item.Id).Esta(Estado.Using);
            c.Passos(10);
            EstadoDaOnda? onda = c.Atual.Onda;
            c.Aplicar(evento).Esta(destino, caso);
            Afirmar.Nulo(c.Atual.Uso, $"{caso}: o uso acabou");
            Afirmar.Igual(onda, c.Atual.Onda, $"{caso}: a onda continua");
            Afirmar.Igual(destino == Estado.Exiting, c.Tem<CancelarOnda>(), $"{caso}: só a saída cancela o temporizador da onda");
            Afirmar.Falso(c.Tem<AgendarOnda>(), $"{caso}: sem reagendar a onda");
        }
    }

    // Tabela 4.6: durante o uso, outro item solto sobre ele é recusado e cai de onde foi solto; o uso continua.
    [Teste]
    public static void Usar_SegundoItemDuranteOUso_Cai()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo primeiro = InvocarEAssentar(c, Item.Energetico);
        ItemNoMundo segundo = InvocarEAssentar(c, Item.Cogumelo);
        c.SoltarSobreEle(primeiro.Id).Esta(Estado.Using);
        int restantes = c.Atual.PassosRestantes;
        c.SoltarSobreEle(segundo.Id).Esta(Estado.Using, "o segundo é recusado");
        Afirmar.Igual(Item.Energetico, c.Atual.Uso!.Item, "continua usando o primeiro");
        Afirmar.Igual(restantes, c.Atual.PassosRestantes, "sem perder passo");
        ItemNoMundo caindo = c.Atual.Itens.PorId(segundo.Id)!;
        Afirmar.Igual((SituacaoDoItem.Caindo, Cenario.MeioDoPersonagem(c.Atual, c.Config)), (caindo.Situacao, caindo.Lugar.Ancora), "cai de onde foi solto");
        Afirmar.Igual(new EstadoDaOnda(Onda.Ligado, FaseDaOnda.Subida, 2, 2), c.Atual.Onda, "a onda é só a do primeiro");
    }

    // A cara de quem usa fica até o fim do uso (CaraLivre): nem a fase da onda nem a emoção dominante escolhida no meio a
    // trocam; no fim, entra a de base (a da fase da onda, que tem precedência).
    [Teste]
    public static void Usar_ACaraDeQuemUsaFicaAteOFim()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo item = InvocarEAssentar(c, Item.LancaPerfume);
        c.SoltarSobreEle(item.Id).Esta(Estado.Using);
        Afirmar.Igual(Expressao.Surpreso, c.Atual.Expressao, "a cara de quem inala");
        c.Aplicar(new ItemEffectTimer(c.Atual.GeracaoDaOnda));
        Afirmar.Igual(new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Pico, 2, 2), c.Atual.Onda, "a subida do tonto (1 s) acaba durante o uso");
        Afirmar.Igual(Expressao.Surpreso, c.Atual.Expressao, "e a cara de quem usa continua");
        c.Aplicar(new CmdSetDominantEmotion(Expressao.Travesso));
        Afirmar.Igual(Expressao.Surpreso, c.Atual.Expressao, "a emoção escolhida no meio também espera");
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle);
        Afirmar.Igual(Expressao.Tonto, c.Atual.Expressao, "no fim, a cara do pico do tonto");
    }
}
