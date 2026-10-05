using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Uma verificação por linha da tabela "Transições principais" de ARCHITECTURE.md 2.6
/// (critério 1 da Fase 2). O nome de cada teste cita a linha; os comentários "Linha:" trazem o
/// texto da tabela. Linhas cujo gatilho vem do movimento (parede, contato com o chão) usam o
/// sinal que o módulo de movimento emitirá a partir da Fase 4.
/// </summary>
internal static class TransicoesTestes
{
    private static readonly ConfiguracaoDoNucleo Padrao = new();
    private static readonly ConfiguracaoDoNucleo ComQueda = new() { QuedaFisica = true };
    private static readonly ConfiguracaoDoNucleo ComPainel = new() { PainelDeEnergiaDisponivel = true };

    private static readonly Estado[] AutonomosEFisicos =
        [Estado.Idle, Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Resting, Estado.Falling, Estado.Landing];

    // Linha: BOOTING | configurações e topologia carregadas | SETTLING | Posição restaurada pela seção 2.8.
    [Teste]
    public static void Booting_Carregado_VaiParaSettlingEDepoisIdleNoChaoDoPrincipal()
    {
        Cenario c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "âncora inicial no chão, a 85% da largura");
        Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name), "move antes de mostrar; depois agenda a autonomia");
    }

    [Teste]
    public static void Booting_CarregadoComPosicaoSalva_RestauraPelaPosicaoRelativa()
    {
        var salva = new PosicaoDoPersonagem(TopologiasDeExemplo.Display2, 0.25, 1.0, new PontoPx(2400, 1032));
        Cenario c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, salva, Preferencias.Padrao));
        c.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TopologiasDeExemplo.Display2, c.Retrato.ChaveMonitor);
        Afirmar.Igual(new PontoPx(1920 + 480, 1032), c.Ancora, "25% da área útil do secundário, no chão");
    }

    // Linha: qualquer autônomo ou físico | PRESS sobre pixel opaco | PRESSED | Movimento autônomo congela no quadro atual. Vale também no meio de um pulo ou queda.
    [Teste]
    public static void AutonomoOuFisico_Press_VaiParaPressedECongelaNoLugar()
    {
        foreach (Estado origem in AutonomosEFisicos)
        {
            Cenario c = Cenario.Em(origem);
            PontoPx antes = c.Ancora;
            c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(origem, Estado.Pressed);
            Afirmar.Igual(antes, c.Ancora, $"{origem}: congela no lugar");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: relógio parado em PRESSED");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: agenda suspensa em PRESSED");
        }
    }

    // DEC-004: nenhuma reação bloqueia a ação direta; um segundo PRESS durante a reação vale.
    [Teste]
    public static void Reacting_Press_VaiParaPressed()
    {
        Cenario.Em(Estado.Reacting).Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Reacting, Estado.Pressed);
    }

    // Linha: PRESSED | DRAG_START | DRAGGING | Plano autônomo descartado.
    [Teste]
    public static void Pressed_DragStart_VaiParaDragging()
    {
        Cenario.Em(Estado.Pressed).Aplicar(new DragStart()).Percorreu(Estado.Pressed, Estado.Dragging);
    }

    // Linha: PRESSED | CLICK | REACTING | Reação curta. Depois, SETTLING decide o próximo estado.
    [Teste]
    public static void Pressed_Click_VaiParaReactingEDepoisSettling()
    {
        Cenario c = Cenario.Em(Estado.Pressed).Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual(Sinal.FoiClicado, c.Retrato.Sinal);
        Afirmar.Verdadeiro(c.Atual.RelogioAtivo, "a reação é animada: o relógio corre");

        c.Passos(Padrao.PassosDaReacao - 1).Esta(Estado.Reacting, "a reação ainda não acabou");
        c.Passos(1).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle);
        Afirmar.Falso(c.Atual.RelogioAtivo, "parado de novo, sem relógio");
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "a autonomia volta depois da reação");
    }

    // Linha: DRAGGING | DRAG_MOVE(p) | DRAGGING | Posição = cursor menos o deslocamento da pegada.
    [Teste]
    public static void Dragging_DragMove_PosicaoEhOCursorMenosAPegada()
    {
        Cenario c = Cenario.Em(Estado.Dragging);
        // Pegada: PontoOpaco − âncora = (0, −32).
        c.Aplicar(new DragMove(new PontoPx(900, 500))).Esta(Estado.Dragging).SemTransicao();
        Afirmar.Igual(new PontoPx(900, 532), c.Ancora, "cursor − pegada, sem prender na área útil");
        Afirmar.Igual(new RetanguloPx(836, 404, 964, 532), c.Efeito<MoverJanela>().Destino.Retangulo);
    }

    // Linha: DRAGGING | DRAG_END ou DRAG_CANCEL | SETTLING | Validação da seção 2.7.
    [Teste]
    public static void Dragging_DragEnd_VaiParaSettlingEGravaAPosicao()
    {
        Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragMove(new PontoPx(400, 1000)));
        c.Aplicar(new DragEnd(new PontoPx(400, 1000))).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(400, 1032), c.Ancora, "soltou com os pés no chão");
        c.Efeito<GravarPosicao>();
    }

    [Teste]
    public static void Dragging_DragCancel_VaiParaSettlingFicandoOndeEstava()
    {
        Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragMove(new PontoPx(700, 1000)));
        c.Aplicar(new DragCancel()).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(700, 1032), c.Ancora, "não volta ao ponto de origem");
    }

    // Complemento da seção 2.7: captura perdida antes do limiar também valida e retoma.
    [Teste]
    public static void Pressed_DragCancel_VaiParaSettling()
    {
        Cenario.Em(Estado.Pressed).Aplicar(new DragCancel()).Percorreu(Estado.Pressed, Estado.Settling, Estado.Idle);
    }

    // Linha: SETTLING | com apoio | IDLE | Autonomia retomada depois de um intervalo de acomodação.
    [Teste]
    public static void Settling_ComApoio_VaiParaIdleEAgendaDepoisDaAcomodacao()
    {
        Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragEnd(new PontoPx(1000, 1000)));
        c.Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        AgendarDecisao agenda = c.Efeito<AgendarDecisao>();
        Afirmar.Verdadeiro(agenda.Atraso >= Padrao.IntervaloDeAcomodacao, $"atraso {agenda.Atraso} ≥ intervalo de acomodação");
    }

    // Linha: SETTLING | sem apoio | FALLING | Cai até o chão do monitor.
    [Teste]
    public static void Settling_SemApoio_VaiParaFallingComQuedaFisica()
    {
        Cenario c = Cenario.Em(Estado.Dragging, ComQueda).Aplicar(new DragEnd(new PontoPx(1000, 300)));
        c.Percorreu(Estado.Dragging, Estado.Settling, Estado.Falling);
        Afirmar.Igual(new PontoPx(1000, 332), c.Ancora, "começa a queda de onde foi solto");
        Afirmar.Verdadeiro(c.Atual.RelogioAtivo, "a queda precisa do relógio");
    }

    // TODO.md, Fase 3: antes da queda animada da Fase 4, soltar sem apoio põe o personagem no chão.
    [Teste]
    public static void Settling_SemApoioSemQuedaFisica_PrendeNoChao()
    {
        Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragEnd(new PontoPx(1000, 300)));
        c.Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(1000, 1032), c.Ancora);
    }

    // Linha: PRESSED (segundo clique), IDLE, REACTING | DOUBLE_CLICK ou menu "Energia" | SETTLING se vier de PRESSED; senão permanece.
    [Teste]
    public static void Pressed_DoubleClick_VaiParaSettlingComReacaoAntesDaFase8()
    {
        Cenario c = Cenario.Em(Estado.Pressed).Aplicar(new DoubleClick());
        c.Percorreu(Estado.Pressed, Estado.Settling, Estado.Idle).SemEfeito<AbrirPainelDeEnergia>();
        Afirmar.Igual(Sinal.FoiClicadoDuasVezes, c.Retrato.Sinal, "antes da Fase 8, só reação não verbal");
    }

    [Teste]
    public static void Pressed_DoubleClick_AbrePainelQuandoDisponivel()
    {
        Cenario c = Cenario.Em(Estado.Pressed, ComPainel).Aplicar(new DoubleClick());
        c.Percorreu(Estado.Pressed, Estado.Settling, Estado.Idle).Efeito<AbrirPainelDeEnergia>();
        Afirmar.Verdadeiro(c.Atual.PainelAberto);
        Afirmar.Falso(c.Atual.DecisaoAgendada, "painel aberto pausa a autonomia");
    }

    [Teste]
    public static void IdleEReacting_DoubleClick_Permanecem()
    {
        Cenario.Em(Estado.Idle, ComPainel).Aplicar(new DoubleClick()).Esta(Estado.Idle).SemTransicao().Efeito<AbrirPainelDeEnergia>();
        Cenario.Em(Estado.Reacting).Aplicar(new DoubleClick()).Esta(Estado.Reacting).SemTransicao();
    }

    // Linha: qualquer estado visível com painel aberto | ENERGY_SELECTED(nivel) | permanece | Atualiza a mesma preferência persistida.
    [Teste]
    public static void PainelAberto_EnergySelected_PermaneceEGravaAPreferencia()
    {
        Cenario c = Cenario.Em(Estado.Idle, ComPainel).Aplicar(new EnergyPanelOpen());
        c.Aplicar(new EnergySelected(NivelDeEnergia.Alta)).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(NivelDeEnergia.Alta, c.Retrato.Energia);
        Afirmar.Igual(NivelDeEnergia.Alta, c.Efeito<GravarPreferencias>().Preferencias.Energia);

        // Sem painel aberto, a escolha não chega ao núcleo por este caminho.
        Cenario sem = Cenario.Em(Estado.Idle, ComPainel).Aplicar(new EnergySelected(NivelDeEnergia.Baixa));
        Afirmar.Igual(NivelDeEnergia.Media, sem.Retrato.Energia);
    }

    // Linha: qualquer estado visível com painel aberto | ENERGY_PANEL_CLOSE | permanece | Fecha o painel e retoma a agenda após intervalo de acomodação.
    [Teste]
    public static void PainelAberto_EnergyPanelClose_PermaneceERetomaAAgenda()
    {
        Cenario c = Cenario.Em(Estado.Idle, ComPainel).Aplicar(new EnergyPanelOpen());
        Afirmar.Falso(c.Atual.DecisaoAgendada, "aberto: sem agenda");
        c.Aplicar(new EnergyPanelClose()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Falso(c.Atual.PainelAberto);
        Afirmar.Verdadeiro(c.Efeito<AgendarDecisao>().Atraso >= ComPainel.IntervaloDeAcomodacao);
    }

    // Linha: FALLING, LANDING | contato com o chão | LANDING, depois IDLE | O painel não altera a física.
    [Teste]
    public static void Falling_ContatoComOChao_VaiParaLandingEDepoisIdle()
    {
        Cenario c = Cenario.Em(Estado.Falling).Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao));
        c.Percorreu(Estado.Falling, Estado.Landing);
        Afirmar.Igual(Sinal.Pousou, c.Retrato.Sinal);
        c.Passos(Padrao.PassosDoPouso).Percorreu(Estado.Landing, Estado.Idle);
        Afirmar.Falso(c.Atual.RelogioAtivo);
    }

    [Teste]
    public static void Landing_ComPainelAberto_TerminaAFisicaSemRetomarAAgenda()
    {
        Cenario c = Cenario.Em(Estado.Falling, ComPainel).Aplicar(new EnergyPanelOpen(), new MovementSignal(SinalDeMovimento.ContatoComOChao));
        c.Esta(Estado.Landing).Passos(ComPainel.PassosDoPouso).Esta(Estado.Idle);
        Afirmar.Verdadeiro(c.Atual.PainelAberto);
        Afirmar.Falso(c.Atual.DecisaoAgendada, "painel aberto: a autonomia continua pausada");
    }

    // Linha: IDLE | AUTONOMY_TIMER | WALKING, CLIMBING, JUMPING, RESTING ou permanece com um gesto curto.
    [Teste]
    public static void Idle_AutonomyTimer_EscolheCadaDestinoPermitido()
    {
        (AcoesAutonomas Acao, Estado Destino)[] casos =
        [
            (AcoesAutonomas.Andar, Estado.Walking),
            (AcoesAutonomas.Escalar, Estado.Climbing),
            (AcoesAutonomas.Pular, Estado.Jumping),
            (AcoesAutonomas.Descansar, Estado.Resting),
        ];
        foreach ((AcoesAutonomas acao, Estado destino) in casos)
            Cenario.Parado(new ConfiguracaoDoNucleo { Acoes = acao }).Decidir().Percorreu(Estado.Idle, destino);

        Cenario g = Cenario.Parado(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Gesto }).Decidir();
        g.Esta(Estado.Idle);
        Afirmar.Diferente(Gesto.Nenhum, g.Retrato.Gesto, "permanece com um gesto curto");
        Afirmar.Verdadeiro(g.Atual.RelogioAtivo, "o gesto é animado");
    }

    [Teste]
    public static void Idle_AutonomyTimer_NadaComPainelAbertoOuAutonomiaPausadaOuDisparoAntigo()
    {
        Cenario pausada = Cenario.Parado();
        long geracao = pausada.Atual.Geracao;
        pausada.Aplicar(new CmdPauseAutonomy(), new AutonomyTimer(geracao)).Esta(Estado.Idle).SemTransicao();

        Cenario painel = Cenario.Parado(ComPainel);
        long g2 = painel.Atual.Geracao;
        painel.Aplicar(new EnergyPanelOpen(), new AutonomyTimer(g2)).Esta(Estado.Idle).SemTransicao();

        Cenario antigo = Cenario.Parado(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Andar });
        antigo.Aplicar(new AutonomyTimer(antigo.Atual.Geracao - 1)).Esta(Estado.Idle).SemTransicao();
    }

    // Linha: WALKING | parede, passagem ou fim do chão | IDLE, CLIMBING, FALLING ou WALKING | Conforme a superfície.
    [Teste]
    public static void Walking_Superficies_LevamAosDestinosDaTabela()
    {
        var destinosNaParede = new HashSet<Estado>();
        for (ulong semente = 1; semente <= 60; semente++)
        {
            Cenario c = AndandoComSemente(semente).Aplicar(new MovementSignal(SinalDeMovimento.Parede));
            Afirmar.Verdadeiro(c.Atual.Estado is Estado.Idle or Estado.Climbing or Estado.Walking, $"parede → {c.Atual.Estado}");
            destinosNaParede.Add(c.Atual.Estado);
        }
        Afirmar.Igual(3, destinosNaParede.Count, $"parede alcança IDLE, CLIMBING e WALKING ({string.Join(",", destinosNaParede)})");

        Cenario.Em(Estado.Walking).Aplicar(new MovementSignal(SinalDeMovimento.Passagem)).Percorreu(Estado.Walking, Estado.Walking);
        Cenario.Em(Estado.Walking).Aplicar(new MovementSignal(SinalDeMovimento.FimDoChao)).Percorreu(Estado.Walking, Estado.Falling);
    }

    // Linha: CLIMBING | topo da área útil, fim da parede ou AUTONOMY_TIMER | IDLE, WALKING, JUMPING ou FALLING.
    [Teste]
    public static void Climbing_TopoFimDaParedeETemporizador_LevamAosDestinosDaTabela()
    {
        var noTopo = new HashSet<Estado>();
        var peloTemporizador = new HashSet<Estado>();
        for (ulong semente = 1; semente <= 80; semente++)
        {
            Cenario c = EscalandoComSemente(semente).Aplicar(new MovementSignal(SinalDeMovimento.TopoDaParede));
            Afirmar.Verdadeiro(c.Atual.Estado is Estado.Idle or Estado.Walking or Estado.Jumping or Estado.Falling, $"topo → {c.Atual.Estado}");
            noTopo.Add(c.Atual.Estado);

            Cenario t = EscalandoComSemente(semente).Decidir();
            Afirmar.Verdadeiro(t.Atual.Estado is Estado.Jumping or Estado.Falling, $"temporizador → {t.Atual.Estado}");
            peloTemporizador.Add(t.Atual.Estado);
        }
        Afirmar.Igual(4, noTopo.Count, $"topo alcança os quatro destinos ({string.Join(",", noTopo)})");
        Afirmar.Igual(2, peloTemporizador.Count, $"o temporizador salta ou solta ({string.Join(",", peloTemporizador)})");
        Cenario.Em(Estado.Climbing).Aplicar(new MovementSignal(SinalDeMovimento.FimDaParede)).Percorreu(Estado.Climbing, Estado.Idle);
    }

    // Linha: CLIMBING | alcança borda superior apoiável | HANGING.
    [Teste]
    public static void Climbing_BordaSuperior_VaiParaHanging()
    {
        Cenario.Em(Estado.Climbing).Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior)).Percorreu(Estado.Climbing, Estado.Hanging);
    }

    // Linha: HANGING | deslocamento autônomo, AUTONOMY_TIMER ou passagem compatível | HANGING, CLIMBING, JUMPING ou FALLING.
    [Teste]
    public static void Hanging_TemporizadorEPassagem_LevamAosDestinosDaTabela()
    {
        var peloTemporizador = new HashSet<Estado>();
        var naPassagem = new HashSet<Estado>();
        for (ulong semente = 1; semente <= 80; semente++)
        {
            Cenario t = EscalandoComSemente(semente).Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior)).Decidir();
            Afirmar.Verdadeiro(t.Atual.Estado is Estado.Hanging or Estado.Climbing or Estado.Jumping or Estado.Falling, $"temporizador → {t.Atual.Estado}");
            peloTemporizador.Add(t.Atual.Estado);

            Cenario p = EscalandoComSemente(semente).Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior), new MovementSignal(SinalDeMovimento.FimDaBorda));
            Afirmar.Verdadeiro(p.Atual.Estado is Estado.Hanging or Estado.Climbing or Estado.Falling, $"passagem → {p.Atual.Estado}");
            naPassagem.Add(p.Atual.Estado);
        }
        Afirmar.Igual(4, peloTemporizador.Count, $"temporizador alcança os quatro destinos ({string.Join(",", peloTemporizador)})");
        Afirmar.Igual(3, naPassagem.Count, $"passagem alcança três destinos ({string.Join(",", naPassagem)})");
    }

    // Linha: RESTING | AUTONOMY_TIMER | IDLE | Acorda e volta a decidir. O relógio só é religado neste momento.
    [Teste]
    public static void Resting_AutonomyTimer_AcordaParaIdle()
    {
        Cenario c = Cenario.Em(Estado.Resting);
        Afirmar.Falso(c.Atual.RelogioAtivo, "descansando, o relógio fica parado");
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "um temporizador único até acordar");
        c.Decidir().Percorreu(Estado.Resting, Estado.Idle);
        Afirmar.Igual(Sinal.Acordou, c.Retrato.Sinal);
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "acordado, volta a decidir");
    }

    // Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED | SETTLING no monitor livre, HIDDEN(POR_TELA_CHEIA) ou estado atual.
    [Teste]
    public static void Visivel_TelaCheiaNoMonitorDoPersonagem_TransfereParaOLivreOuEsconde()
    {
        Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado);
        c.Aplicar(Ocupados(TopologiasDeExemplo.Display1)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TopologiasDeExemplo.Display2, c.Retrato.ChaveMonitor, "foi para o monitor livre");
        Afirmar.Igual(new PontoPx(1920 + 1632, 1032), c.Ancora, "mesma posição relativa");
        c.SemEfeito<GravarPosicao>();

        Cenario cheio = Cenario.Parado();
        cheio.Aplicar(Ocupados(TopologiasDeExemplo.Display1)).Percorreu(Estado.Idle, Estado.Hidden);
        cheio.EstaEscondido(MotivoDoOcultamento.PorTelaCheia);

        Cenario outro = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado);
        outro.Aplicar(Ocupados(TopologiasDeExemplo.Display2)).Esta(Estado.Idle).SemTransicao();
    }

    // Linha: PRESSED, DRAGGING | FULLSCREEN_TARGETS_CHANGED | sem troca de estado | O gesto do usuário nunca é interrompido.
    [Teste]
    public static void PressedEDragging_TelaCheia_NaoInterrompemEOSoltarDescartaORetorno()
    {
        Cenario p = Cenario.Em(Estado.Pressed);
        p.Aplicar(Ocupados(TopologiasDeExemplo.Display1)).Esta(Estado.Pressed).SemTransicao();

        // Transferido pela tela cheia, depois arrastado de volta ao monitor ocupado.
        Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(TopologiasDeExemplo.Display1));
        Afirmar.NaoNulo(c.Atual.RetornoDaTelaCheia, "retorno temporário guardado");
        PontoPx noSecundario = c.Ancora;
        c.Aplicar(new Press(new PontoPx(noSecundario.X, noSecundario.Y - 30)), new DragStart(), new DragMove(new PontoPx(1000, 1000)));
        c.Aplicar(Ocupados(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Display2)).Esta(Estado.Dragging).SemTransicao();
        c.Aplicar(new DragEnd(new PontoPx(1000, 1000))).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TopologiasDeExemplo.Display1, c.Retrato.ChaveMonitor, "respeita o ponto escolhido, mesmo ocupado");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "soltar descarta o retorno temporário");

        // Invariante 14: depois do arraste, o fim da tela cheia não move o personagem.
        PontoPx escolhido = c.Ancora;
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(escolhido, c.Ancora);
    }

    // Linha: HIDDEN(POR_TELA_CHEIA) | FULLSCREEN_TARGETS_CHANGED com monitor livre | SETTLING | Reaparece no monitor livre; mantém o retorno temporário.
    [Teste]
    public static void EscondidoPorTelaCheia_MonitorLivre_ReapareceNeleMantendoORetorno()
    {
        Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado)
            .Aplicar(Ocupados(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Display2))
            .EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        c.Aplicar(Ocupados(TopologiasDeExemplo.Display1)).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TopologiasDeExemplo.Display2, c.Retrato.ChaveMonitor);
        Afirmar.NaoNulo(c.Atual.RetornoDaTelaCheia, "mantém o retorno temporário");
    }

    // Linha: visível ou HIDDEN(POR_TELA_CHEIA) com retorno temporário guardado | FULLSCREEN_TARGETS_CHANGED(vazio) | SETTLING | Restaura a posição anterior.
    [Teste]
    public static void FimDaTelaCheia_RestauraAPosicaoAnteriorELimpaORetorno()
    {
        Cenario visivel = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(TopologiasDeExemplo.Display1));
        visivel.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, visivel.Ancora, "de volta à posição anterior");
        Afirmar.Nulo(visivel.Atual.RetornoDaTelaCheia);

        Cenario escondido = Cenario.Parado().Aplicar(Ocupados(TopologiasDeExemplo.Display1));
        escondido.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, escondido.Ancora);
    }

    // Linha: HIDDEN(POR_TELA_CHEIA) | CMD_SHOW | SETTLING | Aparece na posição anterior validada e descarta o retorno temporário.
    [Teste]
    public static void EscondidoPorTelaCheia_CmdShow_ApareceEDescartaORetorno()
    {
        Cenario c = Cenario.Parado().Aplicar(Ocupados(TopologiasDeExemplo.Display1));
        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora);
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia);
    }

    // Linha: HIDDEN(POR_TELA_CHEIA) | CMD_HIDE | HIDDEN(POR_USUARIO) | O fim da tela cheia não o faz reaparecer.
    [Teste]
    public static void EscondidoPorTelaCheia_CmdHide_ViraOcultacaoDoUsuario()
    {
        Cenario c = Cenario.Parado().Aplicar(Ocupados(TopologiasDeExemplo.Display1));
        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        c.Aplicar(Ocupados()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
    }

    // Linha: qualquer estado visível | CMD_PAUSE_AUTONOMY, CMD_RESUME_AUTONOMY | permanece | Retomar agenda a próxima decisão após o intervalo de acomodação.
    [Teste]
    public static void Visivel_PausarERetomar_PermaneceEControlaAAgenda()
    {
        Cenario c = Cenario.Parado();
        c.Aplicar(new CmdPauseAutonomy()).Esta(Estado.Idle).SemTransicao().Efeito<CancelarDecisao>();
        Afirmar.Verdadeiro(c.Retrato.AutonomiaPausada);
        c.Aplicar(new CmdResumeAutonomy()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Verdadeiro(c.Efeito<AgendarDecisao>().Atraso >= Padrao.IntervaloDeAcomodacao);
    }

    // Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* | conforme a linha correspondente | Nenhum estado autônomo bloqueia interação do usuário.
    [Teste]
    public static void RestingEClimbing_AcoesDoUsuario_SeguemAsPropriasLinhas()
    {
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
        {
            Cenario.Em(origem).Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(origem, Estado.Pressed);
            Cenario.Em(origem).Aplicar(new CmdHide()).Percorreu(origem, Estado.Hidden);
            Cenario.Em(origem).Aplicar(new CmdExit()).Percorreu(origem, Estado.Exiting);
            Cenario.Em(origem).Aplicar(new CmdPauseAutonomy()).Esta(origem);
        }
    }

    // Linha: JUMPING, FALLING | contato com o chão | LANDING, depois IDLE.
    [Teste]
    public static void Jumping_ContatoComOChao_VaiParaLandingEDepoisIdle()
    {
        Cenario c = Cenario.Em(Estado.Jumping).Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao));
        c.Percorreu(Estado.Jumping, Estado.Landing).Passos(Padrao.PassosDoPouso).Percorreu(Estado.Landing, Estado.Idle);
    }

    // Linha: estados autônomos, físicos e REACTING | TOPOLOGY_CHANGED | SETTLING | Revalida a posição.
    [Teste]
    public static void AutonomosFisicosEReacting_TopologyChanged_Revalidam()
    {
        foreach (Estado origem in AutonomosEFisicos.Append(Estado.Reacting))
        {
            Cenario c = Cenario.Em(origem);
            c.Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo));
            Afirmar.Igual(Estado.Settling, c.Transicoes[0].Para, $"{origem}: passa por SETTLING");
            Afirmar.Igual(Estado.Idle, c.Atual.Estado, $"{origem}: termina parado");
            MonitorDoDesktop m = TopologiasDeExemplo.BarraNoTopo.Principal;
            Afirmar.Igual(m.AreaUtil.Base, c.Ancora.Y, $"{origem}: pés no novo chão");
        }
    }

    [Teste]
    public static void TopologyChanged_MesmaConfiguracao_NaoRevalida()
    {
        Cenario.Parado().Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor)).Esta(Estado.Idle).SemTransicao();
    }

    // Linha: PRESSED, DRAGGING | TOPOLOGY_CHANGED | sem troca de estado | A validação acontece ao soltar.
    [Teste]
    public static void PressedEDragging_TopologyChanged_SoAtualizamOCache()
    {
        Cenario.Em(Estado.Pressed).Aplicar(new TopologyChanged(TopologiasDeExemplo.LadoALado)).Esta(Estado.Pressed).SemTransicao();

        Cenario d = Cenario.Em(Estado.Dragging).Aplicar(new TopologyChanged(TopologiasDeExemplo.LadoALado));
        d.Esta(Estado.Dragging).SemTransicao();
        d.Aplicar(new DragEnd(new PontoPx(2500, 900)));
        Afirmar.Igual(TopologiasDeExemplo.Display2, d.Retrato.ChaveMonitor, "a validação ao soltar usa a topologia nova");
    }

    // Linha: BOOTING, HIDDEN, EXITING | TOPOLOGY_CHANGED | sem troca de estado | HIDDEN valida ao reaparecer.
    [Teste]
    public static void BootingHiddenExiting_TopologyChanged_SoAtualizamOCache()
    {
        new Cenario().Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor)).Esta(Estado.Booting).SemTransicao();
        Cenario.Em(Estado.Exiting).Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo)).Esta(Estado.Exiting).SemTransicao();

        Cenario h = Cenario.Em(Estado.Hidden).Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo));
        h.EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        h.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TopologiasDeExemplo.BarraNoTopo.Principal.AreaUtil.Base, h.Ancora.Y, "valida ao reaparecer");
    }

    // Linha: qualquer, exceto EXITING | CMD_HIDE | HIDDEN(POR_USUARIO) | Fecha o painel, encerra captura e arraste, grava a posição.
    [Teste]
    public static void Qualquer_CmdHide_EscondePeloUsuario()
    {
        foreach (Estado origem in AutonomosEFisicos.Concat([Estado.Pressed, Estado.Dragging, Estado.Reacting, Estado.Booting]))
        {
            Cenario c = Cenario.Em(origem).Aplicar(new CmdHide());
            c.EstaEscondido(MotivoDoOcultamento.PorUsuario);
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: sem relógio escondido");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: sem agenda escondido");
            if (origem is Estado.Pressed or Estado.Dragging) c.Efeito<LiberarCaptura>();
            if (origem != Estado.Booting)
            {
                c.Efeito<EsconderJanela>();
                c.Efeito<GravarPosicao>();
            }
        }

        Cenario comPainel = Cenario.Em(Estado.Idle, ComPainel).Aplicar(new EnergyPanelOpen(), new CmdHide());
        comPainel.Efeito<FecharPainelDeEnergia>();
        Afirmar.Falso(comPainel.Atual.PainelAberto);
    }

    // Linha: qualquer, exceto EXITING | SESSION_LOCKED | HIDDEN(POR_SESSAO) | Se já estava em HIDDEN(POR_USUARIO), o motivo do usuário é preservado.
    [Teste]
    public static void Qualquer_SessionLocked_EscondePorSessaoPreservandoOUsuario()
    {
        Cenario.Parado().Aplicar(new SessionLocked()).EstaEscondido(MotivoDoOcultamento.PorSessao);
        Cenario.Em(Estado.Dragging).Aplicar(new SessionLocked()).EstaEscondido(MotivoDoOcultamento.PorSessao).Efeito<LiberarCaptura>();
        Cenario.Em(Estado.Hidden).Aplicar(new SessionLocked()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
    }

    // Linha: qualquer, exceto EXITING | SUSPENDING | HIDDEN(POR_SUSPENSAO) | Idem, com a mesma preservação.
    [Teste]
    public static void Qualquer_Suspending_EscondePorSuspensaoPreservandoOUsuario()
    {
        Cenario.Parado().Aplicar(new Suspending()).EstaEscondido(MotivoDoOcultamento.PorSuspensao);
        Cenario.Em(Estado.Hidden).Aplicar(new Suspending()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        // Esclarecimento: com a sessão bloqueada, suspender não troca o motivo; RESUMED não mostra
        // o personagem sobre uma sessão ainda bloqueada.
        Cenario.Parado().Aplicar(new SessionLocked(), new Suspending(), new Resumed()).EstaEscondido(MotivoDoOcultamento.PorSessao);
    }

    // Linha: HIDDEN(POR_USUARIO) | CMD_SHOW | SETTLING | Só o usuário desfaz o que o usuário pediu.
    [Teste]
    public static void EscondidoPeloUsuario_CmdShow_Reaparece()
    {
        Cenario c = Cenario.Em(Estado.Hidden).Aplicar(new CmdShow());
        c.Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name));
    }

    // Linha: HIDDEN(POR_SESSAO) | SESSION_UNLOCKED ou CMD_SHOW | SETTLING.
    [Teste]
    public static void EscondidoPorSessao_UnlockOuShow_Reaparece()
    {
        Cenario.Parado().Aplicar(new SessionLocked(), new SessionUnlocked()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Cenario.Parado().Aplicar(new SessionLocked(), new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
    }

    // Linha: HIDDEN(POR_SUSPENSAO) | RESUMED ou CMD_SHOW | SETTLING.
    [Teste]
    public static void EscondidoPorSuspensao_ResumedOuShow_Reaparece()
    {
        Cenario.Parado().Aplicar(new Suspending(), new Resumed()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Cenario.Parado().Aplicar(new Suspending(), new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
    }

    // Linha: HIDDEN(POR_USUARIO) | SESSION_UNLOCKED, RESUMED | HIDDEN(POR_USUARIO) | Um evento do sistema não desfaz uma ação direta do usuário.
    [Teste]
    public static void EscondidoPeloUsuario_UnlockEResumed_NaoReaparece()
    {
        Cenario.Em(Estado.Hidden).Aplicar(new SessionUnlocked()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Cenario.Em(Estado.Hidden).Aplicar(new Resumed()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Cenario.Em(Estado.Hidden).Aplicar(new SessionLocked(), new SessionUnlocked()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
    }

    // Linha: qualquer | CMD_EXIT, SESSION_ENDING | EXITING | Grava configurações e encerra.
    [Teste]
    public static void Qualquer_CmdExitOuSessionEnding_Encerra()
    {
        foreach (Estado origem in AutonomosEFisicos.Concat([Estado.Pressed, Estado.Dragging, Estado.Reacting, Estado.Hidden, Estado.Booting]))
        {
            Cenario c = Cenario.Em(origem).Aplicar(new CmdExit()).Esta(Estado.Exiting);
            Afirmar.Igual(typeof(Encerrar), c.Efeitos[^1].GetType(), $"{origem}: Encerrar é o último efeito");
            Afirmar.Falso(c.Atual.RelogioAtivo || c.Atual.DecisaoAgendada, $"{origem}: nada agendado ao sair");
            Cenario.Em(origem).Aplicar(new SessionEnding()).Esta(Estado.Exiting).Efeito<Encerrar>();
        }
        // EXITING é final: nada mais muda.
        Cenario.Em(Estado.Exiting).Aplicar(new CmdShow(), new Press(Cenario.PontoOpaco), new Tick()).Esta(Estado.Exiting).SemTransicao();
    }

    // ---------------------------------------------------------------- complementos

    [Teste]
    public static void ContextMenu_AbreOMenuSemMudarOEstado()
    {
        Cenario c = Cenario.Parado().Aplicar(new ContextMenu(new PontoPx(1600, 990)));
        c.Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(new PontoPx(1600, 990), c.Efeito<AbrirMenu>().Ponto);
        Cenario.Em(Estado.Dragging).Aplicar(new ContextMenu(new PontoPx(1, 1))).SemEfeito<AbrirMenu>();
    }

    [Teste]
    public static void CmdResetPosition_VoltaAPosicaoInicialEGrava()
    {
        Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragEnd(new PontoPx(300, 1000)));
        c.Aplicar(new CmdResetPosition()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora);
        c.Efeito<GravarPosicao>();
    }

    private static Cenario AndandoComSemente(ulong semente)
    {
        var cfg = new ConfiguracaoDoNucleo();
        var c = new Cenario(cfg, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao));
        return c.Esta(Estado.Walking);
    }

    private static Cenario EscalandoComSemente(ulong semente)
    {
        var cfg = new ConfiguracaoDoNucleo();
        var c = new Cenario(cfg, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Escalar }, new AutonomyTimer(c.Atual.Geracao));
        return c.Esta(Estado.Climbing);
    }

    private static FullscreenTargetsChanged Ocupados(params string[] chaves) => new(new MonitoresOcupados(chaves));
}
