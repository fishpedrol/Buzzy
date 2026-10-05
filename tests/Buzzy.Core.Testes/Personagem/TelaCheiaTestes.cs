using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Modo de tela cheia (DEC-013, DEC-020, Q-09; ARCHITECTURE.md 2.6: as linhas da tabela com
/// FULLSCREEN_TARGETS_CHANGED, as de HIDDEN(POR_TELA_CHEIA), a de SETTINGS_CHANGED que desliga o
/// modo e os invariantes 14 e 16): origens diferentes de IDLE e gestos do usuário (itens G e H da
/// revisão do gate da Fase 2) e cenários concretos das regras R1 e R3 a R12 do núcleo (item Q).
/// Os comentários citam cada linha da tabela pelas colunas "De | Evento", porque a numeração do
/// arquivo muda. Em <see cref="TopologiasDeExemplo.LadoALado"/> o personagem começa no DISPLAY1 em
/// (1632,1032) e, transferido, vai para a mesma posição relativa no DISPLAY2, (3552,1032); em
/// <see cref="TopologiasDeExemplo.UmMonitor"/> não há monitor livre e ele se esconde; em
/// <see cref="Cenario.TresEmLinha"/> há mais de um monitor livre.
/// </summary>
internal static class TelaCheiaTestes
{
    private const string Display1 = TopologiasDeExemplo.Display1;
    private const string Display2 = TopologiasDeExemplo.Display2;
    private const string Display3 = TopologiasDeExemplo.Display3;

    /// <summary>Chave que nenhuma topologia de exemplo tem.</summary>
    private const string Desconhecido = @"\\.\DISPLAY9";

    private static readonly ConfiguracaoDoNucleo ComPainel = new() { PainelDeEnergiaDisponivel = true };

    /// <summary>A posição inicial no DISPLAY1 (85% da largura, no chão): o retorno temporário dos cenários.</summary>
    private static readonly PosicaoDoPersonagem PosicaoInicial = new(Display1, 0.85, 1.0, new PontoPx(1632, 1032));

    private static readonly PontoPx NoDisplay2 = new(1920 + 1632, 1032);

    /// <summary>Um ponto sobre o corpo do personagem transferido para o DISPLAY2.</summary>
    private static readonly PontoPx CorpoNoDisplay2 = new(3552, 1000);

    private static readonly SettingsChanged DesligarOModo = new(new Preferencias(NivelDeEnergia.Media, false));

    private static readonly Estado[] VisiveisAlemDeIdle =
        [Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Falling, Estado.Landing, Estado.Resting, Estado.Reacting];

    // ---------------------------------------------------------------- G: origens diferentes de IDLE

    // Item G. Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED(monitoresOcupados) | SETTLING no monitor livre | transfere sem ativar; guarda a posição anterior só em memória.
    [Teste]
    public static void VisiveisAlemDeIdle_TelaCheiaNoMonitorDoPersonagem_TransferemParaOLivre()
    {
        foreach (Estado origem in VisiveisAlemDeIdle)
        {
            Cenario c = Cenario.Em(origem, topologia: TopologiasDeExemplo.LadoALado);
            c.Aplicar(Ocupados(Display1)).Percorreu(origem, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, $"{origem}: foi para o monitor livre");
            Afirmar.Igual(NoDisplay2, c.Ancora, $"{origem}: mesma posição relativa");
            Afirmar.Igual(NoDisplay2, c.Efeito<MoverJanela>().Destino.Ancora, $"{origem}: a janela vai junto");
            c.SemEfeito<MostrarJanela>().SemEfeito<GravarPosicao>();
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{origem}: retorno guardado só em memória");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: parado, sem relógio");
            // A reação ou o pouso interrompidos não continuam: TICKs atrasados não mudam nada.
            c.Passos(3).Esta(Estado.Idle, $"{origem}: TICK atrasado não retoma o que foi interrompido").SemTransicao();

            // R12: o mesmo conjunto de novo não age.
            c.Aplicar(Ocupados(Display1)).Esta(Estado.Idle).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: conjunto repetido, nenhum efeito");

            // Linha: visível ou HIDDEN(POR_TELA_CHEIA) com retorno temporário guardado | FULLSCREEN_TARGETS_CHANGED(vazio): restaura a posição anterior e limpa o retorno.
            c.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: de volta ao DISPLAY1");
            Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{origem}: retorno limpo");
        }
    }

    // Item G. Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED, sem monitor livre: fecha o painel e oculta com HIDDEN(POR_TELA_CHEIA). Linha: ... HIDDEN(POR_TELA_CHEIA) com retorno temporário guardado | FULLSCREEN_TARGETS_CHANGED(vazio): o fim da tela cheia o traz de volta.
    [Teste]
    public static void VisiveisAlemDeIdle_TelaCheiaSemMonitorLivre_EscondemEFechamOPainel()
    {
        foreach (Estado origem in VisiveisAlemDeIdle)
        {
            Cenario c = Cenario.Em(origem, ComPainel).Aplicar(new EnergyPanelOpen());
            Afirmar.Verdadeiro(c.Atual.PainelAberto, $"{origem}: painel aberto antes");
            c.Aplicar(Ocupados(Display1)).Percorreu(origem, Estado.Hidden).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
            Afirmar.Igual(typeof(FecharPainelDeEnergia), c.Efeitos[0].GetType(), $"{origem}: fecha o painel primeiro");
            c.Efeito<EsconderJanela>();
            c.SemEfeito<GravarPosicao>();
            Afirmar.Falso(c.Atual.PainelAberto, $"{origem}: painel fechado");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: sem relógio escondido");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: sem agenda escondido");
            Afirmar.Igual(0, c.Atual.PassosRestantes, $"{origem}: reação ou pouso descartados");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{origem}: retorno guardado");

            // R12: o mesmo conjunto de novo não age.
            c.Aplicar(Ocupados(Display1)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: conjunto repetido, nenhum efeito");

            c.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: reaparece na posição de antes");
            Afirmar.Falso(c.Atual.PainelAberto, $"{origem}: o painel não reabre sozinho");
            Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{origem}: retorno limpo");
        }
    }

    // Item G e R12. Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED, "uma vez por mudança": com o personagem de volta ao monitor ocupado por escolha do usuário, o mesmo conjunto não age em nenhum estado; um conjunto diferente (inclusive com chave desconhecida) volta a agir.
    [Teste]
    public static void VisiveisAlemDeIdle_MesmoConjuntoComOPersonagemNoMonitorOcupado_NaoAge()
    {
        foreach (Estado origem in VisiveisAlemDeIdle)
        {
            Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(Display1));
            Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, $"{origem}: transferido");
            // O usuário arrasta de volta ao DISPLAY1, ainda ocupado: a escolha dele vale.
            c.Aplicar(new Press(CorpoNoDisplay2), new DragStart(), new DragMove(Cenario.PontoOpaco), new DragEnd(Cenario.PontoOpaco));
            Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, $"{origem}: de volta ao monitor ocupado");
            Levar(c, origem);

            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(Ocupados(Display1)).Esta(origem).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: conjunto repetido, nenhum efeito");
            Afirmar.Igual(antes with { Sinal = Sinal.Nenhum }, c.Atual, $"{origem}: nada muda");

            c.Aplicar(Ocupados(Display1, Desconhecido)).Percorreu(origem, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, $"{origem}: conjunto diferente volta a agir");
        }
    }

    // Item G e R5. Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED: "transfere instantaneamente para um monitor livre" — o mais próximo da âncora, não o primeiro da lista nem o principal. Vale também quando o modo age na carga (R6) e ao reaparecer pelo sistema (R5).
    [Teste]
    public static void VariosMonitoresLivres_TransfereParaOMaisProximo_NaoParaOPrimeiroDaListaNemOPrincipal()
    {
        // No meio, a 85%: (3552,1032). O DISPLAY3 fica a 288 px; o DISPLAY1, primeiro da lista e principal, a 1633 px.
        var noMeio = new PosicaoDoPersonagem(Display2, 0.85, 1.0, new PontoPx(3552, 1032));
        var noDisplay3 = new PontoPx(3840 + 1632, 1032);

        Cenario visivel = CarregadoEm(noMeio).Aplicar(Ocupados(Display2)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display3, visivel.Retrato.ChaveMonitor, "visível: o livre mais próximo");
        Afirmar.Igual(noDisplay3, visivel.Ancora, "visível: 85% da área útil do DISPLAY3");
        MesmaPosicao(noMeio, visivel.Atual.RetornoDaTelaCheia, "visível: retorno no DISPLAY2");

        // No DISPLAY3, a 50%: (4800,1032). O DISPLAY2 fica a 961 px; o DISPLAY1, primeiro da lista, a 2881 px.
        var aDireita = new PosicaoDoPersonagem(Display3, 0.5, 1.0, new PontoPx(4800, 1032));
        Cenario direita = CarregadoEm(aDireita).Aplicar(Ocupados(Display3)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display2, direita.Retrato.ChaveMonitor, "da direita: o vizinho, não o primeiro da lista");
        Afirmar.Igual(new PontoPx(1920 + 960, 1032), direita.Ancora, "50% da área útil do DISPLAY2");

        // Na carga, com o DISPLAY2 ocupado antes dela (R6).
        Cenario carga = new Cenario().Aplicar(Ocupados(Display2), new Loaded(Cenario.TresEmLinha, noMeio, Preferencias.Padrao));
        carga.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(noDisplay3, carga.Ancora, "carga: o livre mais próximo");
        MesmaPosicao(noMeio, carga.Atual.RetornoDaTelaCheia, "carga: retorno no DISPLAY2");

        // Ao reaparecer pelo sistema com o DISPLAY2 ocupado em cache (R5).
        foreach ((Evento esconder, MotivoDoOcultamento motivo, Evento reaparecer) in PedidosDoSistema())
        {
            string contexto = reaparecer.GetType().Name;
            Cenario s = CarregadoEm(noMeio).Aplicar(esconder, Ocupados(Display2)).EstaEscondido(motivo);
            s.Aplicar(reaparecer).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(noDisplay3, s.Ancora, $"{contexto}: o livre mais próximo");
            MesmaPosicao(noMeio, s.Atual.RetornoDaTelaCheia, $"{contexto}: retorno no DISPLAY2");
        }
    }

    // Item G. Linha: HIDDEN(POR_TELA_CHEIA) | FULLSCREEN_TARGETS_CHANGED com monitor livre | SETTLING | Reaparece no monitor livre; mantém o retorno temporário. O livre escolhido é o mais próximo da posição de antes da tela cheia (o retorno), não da posição temporária de onde ele se escondeu.
    [Teste]
    public static void EscondidoPorTelaCheia_VariosLivres_ReapareceNoMaisProximoDoRetorno()
    {
        // Sem posição salva: começa no DISPLAY1, (1632,1032). {1} leva ao DISPLAY2; {1,2}, ao DISPLAY3; {1,2,3} esconde.
        Cenario c = new Cenario().Aplicar(new Loaded(Cenario.TresEmLinha, null, Preferencias.Padrao));
        c.Aplicar(Ocupados(Display1)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(3552, 1032), c.Ancora, "{1}: no DISPLAY2");
        c.Aplicar(Ocupados(Display1, Display2)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(5472, 1032), c.Ancora, "{1,2}: no DISPLAY3");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "a segunda transferência não troca o retorno (\"se ainda não houver uma guardada\")");
        c.Aplicar(Ocupados(Display1, Display2, Display3)).Percorreu(Estado.Idle, Estado.Hidden).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual(Display3, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, "escondido a partir do DISPLAY3");

        // {2}: livres o DISPLAY1 e o DISPLAY3. Da posição temporária, o mais próximo seria o DISPLAY3; do retorno, é o DISPLAY1.
        c.Aplicar(Ocupados(Display2)).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        c.Efeito<MostrarJanela>();
        Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, "o livre mais próximo do retorno");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "na posição relativa do retorno");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "mantém o retorno temporário");
    }

    // Item G. Linha: visível ou HIDDEN(POR_TELA_CHEIA) com retorno temporário guardado | FULLSCREEN_TARGETS_CHANGED(vazio) | SETTLING | Restaura a posição anterior validada pela seção 2.8. Com o monitor do retorno desconectado, vale a regra de reserva: o monitor mais próximo da âncora de antes, na mesma posição relativa.
    [Teste]
    public static void FimDaTelaCheia_MonitorDoRetornoRemovido_RestauraNoMonitorMaisProximo()
    {
        // Começa no DISPLAY2 (o do meio), a 85%: (3552,1032). Da âncora de antes, o DISPLAY3 fica a
        // 288 px e o DISPLAY1 (principal) a 1633 px.
        var noMeio = new PosicaoDoPersonagem(Display2, 0.85, 1.0, new PontoPx(3552, 1032));
        var noDisplay3 = new PontoPx(3840 + 1632, 1032);

        // Visível: {2,3} ocupados, vai para o DISPLAY1; o DISPLAY2 é desconectado; o fim da tela
        // cheia leva ao DISPLAY3, não ao monitor atual nem ao principal.
        Cenario visivel = CarregadoEm(noMeio);
        visivel.Aplicar(Ocupados(Display2, Display3)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(1632, 1032), visivel.Ancora, "transferido para o único livre, o DISPLAY1");
        // O DISPLAY1, onde ele está, não mudou: ele continua parado, sem revalidar (DEC-030, classe A).
        visivel.Aplicar(new TopologyChanged(Cenario.TresEmLinhaSemODoMeio)).Percorreu(Estado.Idle, Estado.Idle);
        MesmaPosicao(noMeio, visivel.Atual.RetornoDaTelaCheia, "o retorno sobrevive à mudança de topologia");
        visivel.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display3, visivel.Retrato.ChaveMonitor, "visível: monitor mais próximo da âncora de antes");
        Afirmar.Igual(noDisplay3, visivel.Ancora, "visível: 85% da área útil do DISPLAY3");
        Afirmar.Nulo(visivel.Atual.RetornoDaTelaCheia, "visível: retorno limpo");

        // Escondido por tela cheia: o mesmo destino ao reaparecer.
        Cenario escondido = CarregadoEm(noMeio);
        escondido.Aplicar(Ocupados(Display1, Display2, Display3)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        escondido.Aplicar(new TopologyChanged(Cenario.TresEmLinhaSemODoMeio)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia).SemTransicao();
        escondido.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display3, escondido.Retrato.ChaveMonitor, "escondido: monitor mais próximo da âncora de antes");
        Afirmar.Igual(noDisplay3, escondido.Ancora, "escondido: 85% da área útil do DISPLAY3");
    }

    // ---------------------------------------------------------------- H: gestos do usuário

    // Item H. Linha: PRESSED, DRAGGING | FULLSCREEN_TARGETS_CHANGED | sem troca de estado | Só atualiza os monitores ocupados em cache. O gesto do usuário nunca é interrompido; ao soltar, SETTLING respeita o ponto escolhido pelo usuário, mesmo que seja um monitor ocupado.
    [Teste]
    public static void PressedEDragging_TelaCheia_SoAtualizamOCacheEOMesmoConjuntoDepoisDeSoltarNaoAge()
    {
        foreach (Estado origem in new[] { Estado.Pressed, Estado.Dragging })
        {
            Cenario c = Cenario.Em(origem, topologia: TopologiasDeExemplo.LadoALado);
            c.Aplicar(Ocupados(Display1)).Esta(origem).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: nenhum efeito");
            Afirmar.Igual(new MonitoresOcupados([Display1]), c.Atual.Ocupados, $"{origem}: cache atualizado");
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: não se move");

            c.Aplicar(Ocupados(Display1, Display2)).Esta(origem).SemTransicao();
            Afirmar.Igual(new MonitoresOcupados([Display1, Display2]), c.Atual.Ocupados, $"{origem}: cache atualizado de novo");

            // Solta no mesmo lugar, sobre o monitor ocupado.
            if (origem == Estado.Pressed)
                c.Aplicar(new Click()).Passos(36).Esta(Estado.Idle);
            else
                c.Aplicar(new DragEnd(Cenario.PontoOpaco)).Esta(Estado.Idle);
            Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, $"{origem}: fica no ponto do usuário, mesmo ocupado");

            c.Aplicar(Ocupados(Display1, Display2)).Esta(Estado.Idle).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: reenviar o mesmo conjunto depois de soltar não muda nada");
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: continua onde o usuário deixou");
        }
    }

    // Itens H e Q9, R9. Linha: PRESSED, DRAGGING | FULLSCREEN_TARGETS_CHANGED: ao soltar, o retorno temporário é descartado. DRAG_END e DRAG_CANCEL em DRAGGING sempre descartam; CLICK, DOUBLE_CLICK e DRAG_CANCEL em PRESSED só descartam se a tela cheia mudou durante o gesto.
    [Teste]
    public static void Q9_FimDoGesto_DescartaORetornoSoQuandoAEscolhaEhDoUsuario()
    {
        (string Nome, Evento[] Gesto, bool SempreDescarta)[] saidas =
        [
            ("CLICK em PRESSED", [new Click()], false),
            ("DOUBLE_CLICK em PRESSED", [new DoubleClick()], false),
            ("DRAG_CANCEL em PRESSED", [new DragCancel()], false),
            ("DRAG_END em DRAGGING", [new DragStart(), new DragEnd(CorpoNoDisplay2)], true),
            ("DRAG_CANCEL em DRAGGING", [new DragStart(), new DragCancel()], true),
        ];
        foreach ((string nome, Evento[] gesto, bool sempre) in saidas)
        {
            foreach (bool mudouNoGesto in new[] { false, true })
            {
                string contexto = $"{nome}, tela cheia {(mudouNoGesto ? "mudou" : "não mudou")} durante o gesto";
                Cenario c = Transferido();
                c.Aplicar(new Press(CorpoNoDisplay2)).Esta(Estado.Pressed);
                if (mudouNoGesto) c.Aplicar(Ocupados(Display1, Display2)).Esta(Estado.Pressed).SemTransicao();
                c.Aplicar(gesto);
                if (c.Atual.Estado == Estado.Reacting) c.Passos(36);
                c.Esta(Estado.Idle, $"{contexto}: parado depois do gesto");
                Afirmar.Igual(NoDisplay2, c.Ancora, $"{contexto}: fica no DISPLAY2");

                bool descartado = sempre || mudouNoGesto;
                if (descartado)
                {
                    Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno descartado");
                    // Invariante 14: o fim da tela cheia não move o personagem.
                    c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
                    Afirmar.Igual(NoDisplay2, c.Ancora, $"{contexto}: o fim da tela cheia não move");
                }
                else
                {
                    MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno mantido");
                    c.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
                    Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{contexto}: o fim da tela cheia restaura o DISPLAY1");
                }
            }
        }
    }

    // Item Q9, R9: o caso da revisão passo a passo. PRESSED no DISPLAY2, {1,2} durante o PRESSED, CLICK e 36 passos: fica no DISPLAY2, ocupado, sem retorno.
    [Teste]
    public static void Q9_CliqueComTelaCheiaMudandoNoGesto_FicaNoMonitorOcupadoSemRetorno()
    {
        Cenario c = Transferido();
        c.Aplicar(new Press(CorpoNoDisplay2), Ocupados(Display1, Display2)).Esta(Estado.Pressed);
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "o gesto ainda não terminou: retorno guardado");
        c.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "o clique terminou o gesto com a tela cheia mudada: o ponto do usuário vale");
        c.Passos(35).Esta(Estado.Reacting);
        c.Passos(1).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, "fica no DISPLAY2, ocupado");
        Afirmar.Igual(NoDisplay2, c.Ancora);
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia);

        // Variante sem tela cheia no gesto: o retorno fica.
        Cenario sem = Transferido().Aplicar(new Press(CorpoNoDisplay2), new Click()).Passos(36).Esta(Estado.Idle);
        MesmaPosicao(PosicaoInicial, sem.Atual.RetornoDaTelaCheia, "sem mudança no gesto, o clique não escolhe posição");
    }

    // Itens H e Q9, R9. Linha: PRESSED, DRAGGING | FULLSCREEN_TARGETS_CHANGED ("um clique, clique duplo ou cancelamento em PRESSED só o descarta se a tela cheia mudou durante o gesto"): a mudança vale só para o gesto em que aconteceu. Um gesto interrompido por CMD_HIDE com a tela cheia mudada não passa a marca para o gesto seguinte.
    [Teste]
    public static void Q9_MudancaNumGestoInterrompido_NaoValeParaOGestoSeguinte()
    {
        (string Nome, Evento Saida)[] saidas = [("CLICK", new Click()), ("DOUBLE_CLICK", new DoubleClick()), ("DRAG_CANCEL em PRESSED", new DragCancel())];
        foreach ((string nome, Evento saida) in saidas)
        {
            // Primeiro gesto: a tela cheia muda com o botão pressionado; CMD_HIDE interrompe o gesto e CMD_SHOW o traz de volta.
            Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado);
            c.Aplicar(new Press(Cenario.PontoOpaco), Ocupados(Display1)).Esta(Estado.Pressed);
            c.Aplicar(new CmdHide(), new CmdShow()).Esta(Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{nome}: de volta ao DISPLAY1");

            // Episódio novo: {} e depois {1} transferem para o DISPLAY2, com o retorno no DISPLAY1.
            c.Aplicar(Ocupados(), Ocupados(Display1));
            Afirmar.Igual(NoDisplay2, c.Ancora, $"{nome}: transferido");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{nome}: retorno guardado");

            // Segundo gesto, sem mudança de tela cheia: o retorno fica.
            c.Aplicar(new Press(CorpoNoDisplay2), saida);
            if (c.Atual.Estado == Estado.Reacting) c.Passos(36);
            c.Esta(Estado.Idle, $"{nome}: parado depois do gesto");
            Afirmar.Igual(NoDisplay2, c.Ancora, $"{nome}: fica no DISPLAY2");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{nome}: a mudança do gesto anterior não descarta o retorno");
            c.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{nome}: o fim da tela cheia restaura o DISPLAY1");
        }
    }

    // Item Q9, R7. Linha: HIDDEN por outro motivo, durante um episódio de tela cheia | CMD_SHOW, e invariante 14: CMD_SHOW com o personagem visível também é escolha manual; descarta o retorno e o fim da tela cheia não o move.
    [Teste]
    public static void Q9_CmdShowVisivel_DescartaORetornoEOFimDaTelaCheiaNaoMove()
    {
        Cenario c = Transferido();
        c.Aplicar(new CmdShow()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "visível: nada a mostrar");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "mostrar manualmente descarta o retorno");
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(NoDisplay2, c.Ancora, "o fim da tela cheia não move");
    }

    // ---------------------------------------------------------------- Q: cenários concretos

    // Item Q1, R8. Linhas: qualquer, exceto EXITING | CMD_HIDE, SESSION_LOCKED, SUSPENDING e qualquer | CMD_EXIT, SESSION_ENDING: esconder ou sair depois da transferência grava a posição de antes (DISPLAY1), nunca a temporária (DISPLAY2).
    [Teste]
    public static void Q1_EsconderOuSairDepoisDaTransferencia_GravaAPosicaoDeAntes()
    {
        Evento[] eventos = [new CmdExit(), new SessionEnding(), new CmdHide(), new SessionLocked(), new Suspending()];
        foreach (Evento evento in eventos)
        {
            string contexto = evento.GetType().Name;
            Cenario c = Transferido().Aplicar(evento);
            MesmaPosicao(PosicaoInicial, c.Efeito<GravarPosicao>().Posicao, $"{contexto}: grava o DISPLAY1");

            // Também com o botão pressionado sobre o DISPLAY2 (o gesto interrompido não escolhe posição).
            Cenario p = Transferido().Aplicar(new Press(CorpoNoDisplay2), evento);
            p.Efeito<LiberarCaptura>();
            MesmaPosicao(PosicaoInicial, p.Efeito<GravarPosicao>().Posicao, $"{contexto} em PRESSED: grava o DISPLAY1");
        }

        // Escondido pela tela cheia a partir do DISPLAY2: sair grava a posição de antes.
        foreach (Evento sair in new Evento[] { new CmdExit(), new SessionEnding() })
        {
            Cenario h = Transferido().Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
            h.Aplicar(sair).Esta(Estado.Exiting);
            MesmaPosicao(PosicaoInicial, h.Efeito<GravarPosicao>().Posicao, $"HIDDEN(POR_TELA_CHEIA) + {sair.GetType().Name}: grava o DISPLAY1");
        }
    }

    // Item Q1, R8 e R9 a partir de DRAGGING. Linhas: qualquer, exceto EXITING | CMD_HIDE, SESSION_LOCKED, SUSPENDING ("encerra captura e arraste") e qualquer | CMD_EXIT, SESSION_ENDING. Esconder ou sair no meio do arraste encerra o gesto onde ele está, como um cancelamento (ARCHITECTURE.md 2.7): o ponto do cursor, validado, é a escolha do usuário, e "um arraste sempre descarta o retorno" (DEC-020). Grava esse ponto, não o retorno, e o fim da tela cheia não o move.
    [Teste]
    public static void Q1_EsconderOuSairNoMeioDoArrasteComRetorno_GravaOPontoValidadoEDescartaORetorno()
    {
        // Pegada (0,-32). Cursor em (700,1000): âncora (700,1032), no chão do DISPLAY1. Cursor em
        // (2800,700): âncora (2800,732), no ar sobre o DISPLAY2; sem queda física, a validação a põe
        // no chão, (2800,1032), a 880/1920 da área útil do DISPLAY2.
        (PontoPx Cursor, PosicaoDoPersonagem Validada)[] arrastes =
        [
            (new PontoPx(700, 1000), new PosicaoDoPersonagem(Display1, 700 / 1920.0, 1.0, new PontoPx(700, 1032))),
            (new PontoPx(2800, 700), new PosicaoDoPersonagem(Display2, 880 / 1920.0, 1.0, new PontoPx(2800, 1032))),
        ];
        (Evento Evento, Evento? Reaparecer)[] eventos =
        [
            (new CmdHide(), new CmdShow()),
            (new SessionLocked(), new SessionUnlocked()),
            (new Suspending(), new Resumed()),
            (new CmdExit(), null),
            (new SessionEnding(), null),
        ];
        foreach ((PontoPx cursor, PosicaoDoPersonagem validada) in arrastes)
        {
            foreach ((Evento evento, Evento? reaparecer) in eventos)
            {
                string contexto = $"{evento.GetType().Name} arrastando até {cursor}";
                Cenario c = Transferido().Aplicar(new Press(CorpoNoDisplay2), new DragStart(), new DragMove(cursor)).Esta(Estado.Dragging);
                MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno guardado antes");
                c.Aplicar(evento);
                Afirmar.Igual(typeof(LiberarCaptura), c.Efeitos[0].GetType(), $"{contexto}: solta a captura primeiro");
                MesmaPosicao(validada, c.Efeito<GravarPosicao>().Posicao, $"{contexto}: grava o ponto validado do arraste, não o retorno");
                Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{contexto}: o arraste descarta o retorno");
                if (reaparecer is null) continue;

                // Escondido e sem retorno, o fim da tela cheia não faz nada; ao reaparecer, está onde o arraste parou.
                c.Aplicar(Ocupados()).Esta(Estado.Hidden).SemTransicao();
                Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: o fim da tela cheia não faz nada");
                c.Aplicar(reaparecer).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
                Afirmar.Igual(validada.AncoraAbsoluta, c.Ancora, $"{contexto}: reaparece onde o arraste parou");
            }
        }
    }

    // Regressão do defeito achado pela cobertura do gate da Fase 2 e corrigido em 2026-09-30
    // (Maquina.MudarPreferencias, FimDoGestoDoUsuario e Esconder). Antes, SETTINGS_CHANGED que
    // desligava o modo com o retorno guardado apagava o retorno sem restaurá-lo com o personagem
    // escondido por outro motivo (usuário, sessão ou suspensão) ou com o botão pressionado, e a
    // posição temporária do DISPLAY2 virava a do usuário. A regra é a linha "qualquer, com retorno
    // temporário guardado | SETTINGS_CHANGED que desliga o modo de tela cheia" (escondido por outro
    // motivo, a posição anterior volta a valer sem reaparecer; um clique, clique duplo ou
    // cancelamento em PRESSED leva de volta à posição anterior; esconder no meio do gesto grava e
    // guarda a posição anterior), o invariante 16, R8 e DEC-020, item 2. Sequência mínima:
    // LadoALado; Loaded; FullscreenTargetsChanged ocupados=\\.\DISPLAY1; CmdHide; SettingsChanged
    // energia=Media telaCheia=nao; CmdExit -> GravarPosicao \\.\DISPLAY1;0.85;1;1632;1032.
    [Teste]
    public static void Q1_DesligarOModoComRetornoGuardado_NaoTornaAPosicaoTemporariaDefinitiva()
    {
        Evento[] reacaoInteira = [new Click(), .. Enumerable.Repeat<Evento>(new Tick(), 36)];
        (string Nome, Evento[] Antes, Evento[] Depois, Evento? Reaparecer)[] casos =
        [
            ("CMD_HIDE e depois desliga", [new CmdHide()], [], new CmdShow()),
            ("SESSION_LOCKED e depois desliga", [new SessionLocked()], [], new SessionUnlocked()),
            ("SUSPENDING e depois desliga", [new Suspending()], [], new Resumed()),
            ("PRESS, desliga, CLICK e fim da reação", [new Press(CorpoNoDisplay2)], reacaoInteira, null),
            ("PRESS, desliga, DOUBLE_CLICK", [new Press(CorpoNoDisplay2)], [new DoubleClick()], null),
            ("PRESS, desliga, DRAG_CANCEL", [new Press(CorpoNoDisplay2)], [new DragCancel()], null),
            ("PRESS, desliga, CMD_HIDE", [new Press(CorpoNoDisplay2)], [new CmdHide()], new CmdShow()),
        ];
        var defeitos = new List<string>();
        foreach ((string nome, Evento[] antes, Evento[] depois, Evento? reaparecer) in casos)
        {
            Cenario Montar() => Transferido().Aplicar(antes).Aplicar(DesligarOModo).Aplicar(depois);
            var problemas = new List<string>();
            void Conferir(string oQue, PosicaoDoPersonagem? obtida)
            {
                if (Diferenca(PosicaoInicial, obtida) is { } diferenca) problemas.Add($"{oQue} {diferenca}");
            }

            Cenario c = Montar();
            if (c.Tem<GravarPosicao>()) Conferir("gravou", c.Efeito<GravarPosicao>().Posicao);
            Conferir("posição", c.Atual.Posicao);
            if (c.Atual.RetornoDaTelaCheia is { } sobra) problemas.Add($"retorno sobrou fora do gesto: {Gravacao.DescreverPosicao(sobra)}");
            if (c.Atual.Estado.Visivel() && c.Ancora != Cenario.AncoraInicial) problemas.Add($"parado em {c.Ancora}");
            Conferir("CMD_EXIT gravou", Montar().Aplicar(new CmdExit()).Efeito<GravarPosicao>().Posicao);
            if (reaparecer is not null)
            {
                Cenario r = Montar().Aplicar(reaparecer);
                if (!r.Atual.Estado.Visivel() || r.Ancora != Cenario.AncoraInicial) problemas.Add($"{reaparecer.GetType().Name} o mostrou em {r.Retrato.ChaveMonitor} {r.Ancora}");
            }
            if (problemas.Count > 0) defeitos.Add($"{nome}: {string.Join("; ", problemas)}");
        }
        if (defeitos.Count > 0)
        {
            Afirmar.Falhar("Regressão (Maquina.MudarPreferencias): desligar o modo com o retorno guardado, escondido por outro motivo ou com o botão pressionado, "
                + "torna definitiva a posição temporária do DISPLAY2; esperado o DISPLAY1 de antes da tela cheia (linha SETTINGS_CHANGED que desliga o modo, "
                + "invariante 16, R8, DEC-020). Casos: " + string.Join(" | ", defeitos));
        }
    }

    // Linha: qualquer, com retorno temporário guardado | SETTINGS_CHANGED que desliga o modo de tela cheia, nos ramos que já seguem a tabela: "visível ou HIDDEN(POR_TELA_CHEIA): o personagem volta à posição anterior validada"; em PRESSED e DRAGGING "o gesto não é interrompido" e "um arraste escolhe a posição". Mudar as preferências sem desligar o modo não desfaz nada.
    [Teste]
    public static void DesligarOModo_VisivelOuEscondidoPelaTelaCheia_VoltaAPosicaoAnterior()
    {
        // Visível, transferido para o DISPLAY2: volta ao DISPLAY1, ainda ocupado.
        Cenario visivel = Transferido().Aplicar(DesligarOModo).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, visivel.Ancora, "visível: volta à posição de antes");
        Afirmar.Igual(Cenario.AncoraInicial, visivel.Efeito<MoverJanela>().Destino.Ancora, "visível: a janela vai junto");
        visivel.SemEfeito<GravarPosicao>();
        MesmaPosicao(PosicaoInicial, visivel.Atual.Posicao, "visível: a posição de antes volta a valer");
        Afirmar.Nulo(visivel.Atual.RetornoDaTelaCheia, "visível: sem retorno");
        Afirmar.Falso(visivel.Atual.Preferencias.ModoTelaCheia, "modo desligado");

        // Escondido pela tela cheia depois de transferido: reaparece na posição de antes, não na temporária.
        Cenario escondido = Transferido().Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        escondido.Aplicar(DesligarOModo).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        escondido.Efeito<MostrarJanela>();
        Afirmar.Igual(Cenario.AncoraInicial, escondido.Ancora, "HIDDEN(POR_TELA_CHEIA): reaparece no DISPLAY1");
        Afirmar.Nulo(escondido.Atual.RetornoDaTelaCheia, "HIDDEN(POR_TELA_CHEIA): sem retorno");

        // Um monitor só: escondido pela tela cheia, reaparece nele mesmo com a tela cheia ainda lá.
        Cenario um = Cenario.Parado().Aplicar(Ocupados(Display1)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        um.Aplicar(DesligarOModo).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        um.Efeito<MostrarJanela>();
        Afirmar.Igual(Cenario.AncoraInicial, um.Ancora, "um monitor: reaparece na posição de antes");
        Afirmar.Nulo(um.Atual.RetornoDaTelaCheia, "um monitor: sem retorno");

        // PRESSED e DRAGGING: desligar não interrompe o gesto; soltar depois de arrastar escolhe a posição.
        Cenario pressionado = Transferido().Aplicar(new Press(CorpoNoDisplay2), DesligarOModo).Esta(Estado.Pressed).SemTransicao();
        Afirmar.Igual(NoDisplay2, pressionado.Ancora, "PRESSED: o gesto continua onde estava");
        Cenario arraste = Transferido().Aplicar(new Press(CorpoNoDisplay2), new DragStart(), DesligarOModo).Esta(Estado.Dragging).SemTransicao();
        arraste.Aplicar(new DragMove(new PontoPx(2800, 1000)), new DragEnd(new PontoPx(2800, 1000))).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(2800, 1032), arraste.Ancora, "DRAGGING: o arraste escolhe a posição");
        Afirmar.Igual(new PontoPx(2800, 1032), arraste.Efeito<GravarPosicao>().Posicao.AncoraAbsoluta, "DRAGGING: grava o ponto solto");
        Afirmar.Nulo(arraste.Atual.RetornoDaTelaCheia, "DRAGGING: sem retorno depois de soltar");

        // Só a energia muda: o modo continua ligado, o retorno fica e o fim da tela cheia ainda restaura.
        Cenario energia = Transferido().Aplicar(new SettingsChanged(new Preferencias(NivelDeEnergia.Alta, true))).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(NoDisplay2, energia.Ancora, "trocar a energia não move");
        MesmaPosicao(PosicaoInicial, energia.Atual.RetornoDaTelaCheia, "trocar a energia não desfaz a tela cheia");
        energia.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, energia.Ancora, "o fim da tela cheia ainda restaura");
    }

    // Linhas de FULLSCREEN_TARGETS_CHANGED com o modo desligado (Q-09; DEC-013: "com o modo desligado, só o cache"): não transfere, não esconde nem restaura, com um ou dois monitores, desligado na carga ou depois.
    [Teste]
    public static void ModoDesligado_TelaCheiaSoAtualizaOCache()
    {
        var desligado = new Preferencias(NivelDeEnergia.Media, false);
        (string Nome, Cenario Cenario)[] casos =
        [
            ("LadoALado, desligado na carga", new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, null, desligado))),
            ("UmMonitor, desligado na carga", new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, desligado))),
            ("LadoALado, desligado depois", Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(new SettingsChanged(desligado))),
        ];
        foreach ((string nome, Cenario c) in casos)
        {
            c.Esta(Estado.Idle);
            foreach (string[] chaves in new[] { new[] { Display1 }, new[] { Display1, Display2 }, Array.Empty<string>() })
            {
                string contexto = $"{nome}, ocupados {{{string.Join(",", chaves)}}}";
                c.Aplicar(Ocupados(chaves)).Esta(Estado.Idle).SemTransicao();
                Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: nenhum efeito");
                Afirmar.Igual(new MonitoresOcupados(chaves), c.Atual.Ocupados, $"{contexto}: cache atualizado");
                Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{contexto}: fica no DISPLAY1");
                Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{contexto}: nada guardado");
            }
        }
    }

    // Item Q2, R7. Linha: HIDDEN(POR_USUARIO) | CMD_SHOW no meio de um episódio (HIDDEN por outro motivo, durante um episódio de tela cheia | CMD_SHOW): reaparece onde estava (DISPLAY2, o 1 continua ocupado), descarta o retorno, e o fim da tela cheia não o move.
    [Teste]
    public static void Q2_CmdShowDepoisDeCmdHideNoEpisodio_ApareceNoMonitorTemporarioEDescartaORetorno()
    {
        Cenario c = Transferido().Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        MesmaPosicao(PosicaoInicial, c.Efeito<GravarPosicao>().Posicao, "R8: CMD_HIDE grava o DISPLAY1");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "o retorno continua guardado escondido");

        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, "reaparece onde estava; o DISPLAY1 continua ocupado");
        Afirmar.Igual(NoDisplay2, c.Ancora);
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "mostrar manualmente descarta o retorno");

        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "invariante 14: o fim da tela cheia não move");
        Afirmar.Igual(NoDisplay2, c.Ancora);
    }

    // Item Q3, R3. Linha: HIDDEN(POR_TELA_CHEIA) | CMD_HIDE | HIDDEN(POR_USUARIO): vira ocultação do usuário; a posição volta a ser a de antes; o fim da tela cheia não o mostra; CMD_SHOW o mostra no DISPLAY1.
    [Teste]
    public static void Q3_CmdHideSobreOcultacaoDaTelaCheia_ReapareceNaPosicaoDeAntesSoPeloUsuario()
    {
        Cenario c = Transferido().Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual(Display2, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, "escondido a partir do DISPLAY2");

        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario).Percorreu(Estado.Hidden, Estado.Hidden);
        MesmaPosicao(PosicaoInicial, c.Atual.Posicao, "a posição passa a ser a de antes da tela cheia");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "sem retorno");

        c.Aplicar(Ocupados()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "o fim da tela cheia não desfaz a ocultação do usuário");

        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, "reaparece no DISPLAY1");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "na posição de antes");
    }

    // Linha: HIDDEN(POR_TELA_CHEIA) | CMD_SHOW | SETTLING | "aparece na posição anterior validada, descarta o retorno temporário e o modo não o oculta de novo até a próxima mudança de tela cheia". Escondido a partir do monitor temporário, aparece na posição de antes da tela cheia, não na temporária.
    [Teste]
    public static void EscondidoPorTelaCheiaDepoisDeTransferido_CmdShow_ApareceNaPosicaoAnterior()
    {
        Cenario c = Transferido().Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual(Display2, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, "escondido a partir do DISPLAY2");

        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        c.Efeito<MostrarJanela>();
        Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, "na posição anterior, no DISPLAY1, mesmo ocupado");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora);
        MesmaPosicao(PosicaoInicial, c.Atual.Posicao, "a posição anterior volta a valer");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "o retorno é descartado");

        c.Aplicar(Ocupados(Display1, Display2)).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "o mesmo conjunto não o esconde de novo");
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "sem retorno, o fim da tela cheia não o move");
    }

    // Item Q4, R4. Linha: HIDDEN por outro motivo, com retorno temporário guardado | FULLSCREEN_TARGETS_CHANGED(vazio) | sem troca de estado: o fim da tela cheia não o mostra, mas a posição de antes volta a valer e o retorno some.
    [Teste]
    public static void Q4_FimDaTelaCheiaEscondidoPeloUsuario_GuardaAPosicaoDeAntesParaQuandoReaparecer()
    {
        Cenario c = Transferido().Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        c.Aplicar(Ocupados()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "continua escondido, sem efeitos");
        MesmaPosicao(PosicaoInicial, c.Atual.Posicao, "a posição volta a ser a de antes");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "o retorno não sobra");

        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, "reaparece no DISPLAY1");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora);

        // Um episódio novo só no DISPLAY2 não o afeta, e o fim dele também não o move.
        c.Aplicar(Ocupados(Display2)).Esta(Estado.Idle).SemTransicao();
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "continua no DISPLAY1");
    }

    // Item Q4, R4 só vale para o conjunto vazio. Escondido por outro motivo, um conjunto não vazio só atualiza o cache: o episódio continua, com o retorno e a posição temporária; CMD_SHOW (R7) o mostra onde estava.
    [Teste]
    public static void Q4_EscondidoPeloUsuario_ConjuntoNaoVazio_SoAtualizaOCache()
    {
        Cenario c = Transferido().Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        c.Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "escondido pelo usuário: nenhum efeito");
        Afirmar.Igual(new MonitoresOcupados([Display1, Display2]), c.Atual.Ocupados, "cache atualizado");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "o episódio continua: o retorno fica");
        Afirmar.Igual(NoDisplay2, Afirmar.NaoNulo(c.Atual.Posicao).AncoraAbsoluta, "a posição continua a temporária");

        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(NoDisplay2, c.Ancora, "R7: reaparece onde estava, mesmo com o DISPLAY2 ocupado");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "R7: o retorno é descartado");
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(NoDisplay2, c.Ancora, "invariante 14: o fim da tela cheia não o move");
    }

    // Item Q5, R5. Linhas: HIDDEN(POR_SESSAO) | SESSION_UNLOCKED e HIDDEN(POR_SUSPENSAO) | RESUMED com o monitor ainda ocupado (cache): não mostram o personagem sobre a tela cheia; ele volta a HIDDEN(POR_TELA_CHEIA) sem a janela piscar, e o fim da tela cheia ainda o traz de volta.
    [Teste]
    public static void Q5_RetomarOuDesbloquearComOUnicoMonitorOcupado_VoltaAEsconderPelaTelaCheia()
    {
        foreach ((Evento esconder, MotivoDoOcultamento motivo, Evento reaparecer) in PedidosDoSistema())
        {
            string contexto = $"{esconder.GetType().Name}/{reaparecer.GetType().Name}";
            Cenario c = Cenario.Parado().Aplicar(Ocupados(Display1)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
            c.Aplicar(esconder).EstaEscondido(motivo).Percorreu(Estado.Hidden, Estado.Hidden);

            c.Aplicar(reaparecer).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
            c.Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle, Estado.Hidden);
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: a janela nem aparece");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno guardado de novo");

            c.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{contexto}: o fim da tela cheia o traz de volta");
        }
    }

    // Item Q5, R5 e R7. Em LadoALado com {1} ocupado: reaparecer pelo sistema age como FULLSCREEN_TARGETS_CHANGED e leva ao DISPLAY2, guardando o retorno; CMD_SHOW (ação do usuário) não reaplica o modo e, no meio de um episódio, descarta o retorno.
    [Teste]
    public static void Q5_RetomarOuDesbloquearComOMonitorOcupado_ReapareceNoLivre()
    {
        foreach ((Evento esconder, MotivoDoOcultamento motivo, Evento reaparecer) in PedidosDoSistema())
        {
            string contexto = $"{esconder.GetType().Name}/{reaparecer.GetType().Name}";

            // A tela cheia começa com o personagem escondido pelo sistema: só o cache.
            Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(esconder).EstaEscondido(motivo);
            c.Aplicar(Ocupados(Display1)).EstaEscondido(motivo).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: escondido por outro motivo, só o cache");
            c.Aplicar(reaparecer).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, $"{contexto}: reaparece no DISPLAY2");
            Afirmar.Igual(NoDisplay2, c.Ancora, $"{contexto}: mesma posição relativa");
            Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name), $"{contexto}: a janela aparece uma vez, já no livre");
            Afirmar.Igual(Display2, c.Efeito<MoverJanela>().Destino.Monitor.Chave, $"{contexto}: a janela vai direto ao DISPLAY2");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno guardado");

            // Escondido primeiro pela tela cheia ({1,2}), depois pelo sistema (R3); só o 1 continua ocupado.
            Cenario t = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(Display1, Display2), esconder).EstaEscondido(motivo);
            t.Aplicar(Ocupados(Display1)).EstaEscondido(motivo).SemTransicao();
            t.Aplicar(reaparecer).Esta(Estado.Idle);
            Afirmar.Igual(NoDisplay2, t.Ancora, $"{contexto}, depois da tela cheia: reaparece no DISPLAY2");
            MesmaPosicao(PosicaoInicial, t.Atual.RetornoDaTelaCheia, $"{contexto}, depois da tela cheia: retorno guardado");

            // Escondido pelo sistema já transferido, com o monitor dele livre: reaparece onde estava e o episódio continua.
            Cenario s = Transferido().Aplicar(esconder).EstaEscondido(motivo);
            s.Aplicar(reaparecer).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(NoDisplay2, s.Ancora, $"{contexto}, já transferido: reaparece no DISPLAY2");
            MesmaPosicao(PosicaoInicial, s.Atual.RetornoDaTelaCheia, $"{contexto}, já transferido: reaparecer pelo sistema não é escolha do usuário");
            s.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, s.Ancora, $"{contexto}, já transferido: o fim da tela cheia restaura o DISPLAY1");

            // CMD_SHOW é do usuário: não reaplica o modo e aparece no DISPLAY1 mesmo ocupado.
            Cenario m = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(esconder, Ocupados(Display1), new CmdShow());
            m.Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Display1, m.Retrato.ChaveMonitor, $"{contexto}: CMD_SHOW não reaplica o modo");
            Afirmar.Igual(Cenario.AncoraInicial, m.Ancora, $"{contexto}: CMD_SHOW, no DISPLAY1 ocupado");

            // R7: escondido pelo sistema no meio de um episódio, CMD_SHOW o mostra onde estava e descarta o retorno.
            Cenario e = Transferido().Aplicar(esconder).EstaEscondido(motivo);
            e.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(NoDisplay2, e.Ancora, $"{contexto}: CMD_SHOW no meio do episódio, onde estava");
            Afirmar.Nulo(e.Atual.RetornoDaTelaCheia, $"{contexto}: CMD_SHOW descarta o retorno");
            e.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
            Afirmar.Igual(NoDisplay2, e.Ancora, $"{contexto}: depois do CMD_SHOW, o fim da tela cheia não o move");
        }
    }

    // Item Q6, R6. Linha: BOOTING | configurações e topologia carregadas | SETTLING. Tela cheia avisada antes da carga fica em cache; a carga já sai do monitor ocupado.
    [Teste]
    public static void Q6_TelaCheiaAvisadaAntesDaCarga_ApareceJaNoMonitorLivre()
    {
        Cenario c = new Cenario().Aplicar(Ocupados(Display1));
        c.Esta(Estado.Booting).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "BOOTING: só o cache");
        Afirmar.Igual(new MonitoresOcupados([Display1]), c.Atual.Ocupados, "cache atualizado");

        c.Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, null, Preferencias.Padrao))
            .Percorreu(Estado.Booting, Estado.Settling, Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display2, c.Retrato.ChaveMonitor, "aparece no DISPLAY2");
        Afirmar.Igual(NoDisplay2, c.Ancora);
        Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name), "a janela aparece uma vez, já no livre");
        Afirmar.Igual(Display2, c.Efeito<MoverJanela>().Destino.Monitor.Chave);
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "retorno no DISPLAY1");

        c.Aplicar(Ocupados()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "o fim da tela cheia o leva ao DISPLAY1");

        // Um monitor só, ocupado antes da carga: nem chega a aparecer.
        Cenario um = new Cenario().Aplicar(Ocupados(Display1), new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        um.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle, Estado.Hidden).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual(0, um.Efeitos.Count, "um monitor ocupado: a janela não aparece");
        um.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        um.Efeito<MostrarJanela>();

        // Com o modo desligado na carga, o cache não age.
        Cenario desligado = new Cenario().Aplicar(Ocupados(Display1), new Loaded(TopologiasDeExemplo.LadoALado, null, new Preferencias(NivelDeEnergia.Media, false)));
        desligado.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display1, desligado.Retrato.ChaveMonitor, "modo desligado: fica no DISPLAY1");
        Afirmar.Nulo(desligado.Atual.RetornoDaTelaCheia);
    }

    // Item Q7, R6 (defeito antigo). Pedidos anteriores à carga não mostram o personagem com os padrões; ele aparece só com o Loaded, na posição e com as preferências do Loaded, e só a primeira carga vale.
    [Teste]
    public static void Q7_PedidosAntesDaCarga_SoApareceComOLoadedEComAsPreferenciasDele()
    {
        Cenario c = new Cenario().Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor));
        c.Esta(Estado.Booting).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "BOOTING: só o cache");

        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario).Percorreu(Estado.Booting, Estado.Hidden);
        Afirmar.Igual(0, c.Efeitos.Count, "antes da carga: nada a esconder nem a gravar");

        c.Aplicar(new CmdShow()).Esta(Estado.Hidden).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "CMD_SHOW antes da carga não mostra com os padrões");
        Afirmar.Nulo(c.Atual.Lugar, "ainda sem lugar");

        var salva = new PosicaoDoPersonagem(Display1, 0.25, 1.0, new PontoPx(480, 1032));
        c.Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, salva, new Preferencias(NivelDeEnergia.Alta, false)))
            .Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, "aparece na posição salva, a 25%");
        Afirmar.Igual(NivelDeEnergia.Alta, c.Retrato.Energia, "energia do Loaded");
        Afirmar.Falso(c.Atual.Preferencias.ModoTelaCheia, "modo de tela cheia do Loaded");
        Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name));
        FaixaDeAltaAbaixoDeMedia(c.Efeito<AgendarDecisao>().Atraso, "a agenda usa o perfil Alta");

        // Só a primeira carga vale.
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, null, new Preferencias(NivelDeEnergia.Baixa, true))).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "carga repetida: nenhum efeito");
        Afirmar.Igual(antes with { Sinal = Sinal.Nenhum }, c.Atual, "carga repetida ignorada");

        // Também com o personagem escondido depois da carga: a segunda carga não troca posição,
        // topologia nem preferências, e CMD_SHOW o traz de volta a 25%, com energia Alta.
        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        EstadoDoNucleo escondido = c.Atual;
        var outra = new PosicaoDoPersonagem(Display2, 0.5, 1.0, new PontoPx(2880, 1032));
        c.Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, outra, new Preferencias(NivelDeEnergia.Baixa, true))).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Afirmar.Igual(escondido with { Sinal = Sinal.Nenhum }, c.Atual, "carga repetida com o personagem escondido: ignorada");
        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, "reaparece na posição da primeira carga");
        Afirmar.Igual(NivelDeEnergia.Alta, c.Retrato.Energia, "com a energia da primeira carga");
    }

    // Item Q7, R6. Linha: BOOTING | configurações e topologia carregadas ("pedidos anteriores a ela ... ficam guardados e o personagem só aparece com a carga"): um pedido de esconder anterior à carga continua valendo depois dela. A carga guarda a posição e as preferências sem mostrar nem agendar; só o evento que desfaz aquele motivo mostra o personagem, e os outros não (invariante 10).
    [Teste]
    public static void Q7_EsconderAntesDaCarga_ContinuaValendoDepoisDela()
    {
        var salva = new PosicaoDoPersonagem(Display1, 0.25, 1.0, new PontoPx(480, 1032));
        var alta = new Preferencias(NivelDeEnergia.Alta, false);
        (Evento Esconder, MotivoDoOcultamento Motivo, Evento Mostrar, Evento[] NaoMostram)[] casos =
        [
            (new CmdHide(), MotivoDoOcultamento.PorUsuario, new CmdShow(), [new SessionUnlocked(), new Resumed()]),
            (new SessionLocked(), MotivoDoOcultamento.PorSessao, new SessionUnlocked(), [new Resumed()]),
            (new Suspending(), MotivoDoOcultamento.PorSuspensao, new Resumed(), [new SessionUnlocked()]),
        ];
        foreach ((Evento esconder, MotivoDoOcultamento motivo, Evento mostrar, Evento[] naoMostram) in casos)
        {
            string contexto = $"{esconder.GetType().Name} antes da carga";
            Cenario c = new Cenario().Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor), esconder).EstaEscondido(motivo);
            c.Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, salva, alta)).EstaEscondido(motivo).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: a carga não mostra nem agenda");
            Afirmar.Verdadeiro(c.Atual.Carregado, $"{contexto}: carregado");
            Afirmar.Igual(NivelDeEnergia.Alta, c.Retrato.Energia, $"{contexto}: preferências da carga");
            MesmaPosicao(salva, c.Atual.Posicao, $"{contexto}: posição da carga guardada");

            foreach (Evento outro in naoMostram)
            {
                c.Aplicar(outro).EstaEscondido(motivo).SemTransicao();
                Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: {outro.GetType().Name} não desfaz outro motivo");
            }

            c.Aplicar(mostrar).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, $"{contexto}: aparece na posição da carga");
            Afirmar.Sequencia(["MoverJanela", "MostrarJanela", "AgendarDecisao"], c.Efeitos.Select(e => e.GetType().Name), $"{contexto}: aparece uma vez");
            FaixaDeAltaAbaixoDeMedia(c.Efeito<AgendarDecisao>().Atraso, $"{contexto}: a agenda usa o perfil Alta da carga");
        }

        // Com a tela cheia avisada antes da carga, o pedido do usuário continua valendo: a carga não
        // aplica o modo a um personagem escondido (nem o transfere nem troca o motivo), e CMD_SHOW
        // não o reaplica.
        foreach (Topologia topologia in new[] { TopologiasDeExemplo.LadoALado, TopologiasDeExemplo.UmMonitor })
        {
            string contexto = $"CMD_HIDE e tela cheia antes da carga em {topologia.Monitores.Count} monitor(es)";
            Cenario c = new Cenario().Aplicar(new CmdHide(), Ocupados(Display1));
            c.Aplicar(new Loaded(topologia, null, Preferencias.Padrao)).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: a carga não mostra");
            Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{contexto}: nada guardado");
            c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{contexto}: CMD_SHOW o mostra no DISPLAY1, mesmo ocupado");
        }

        // Escondido pela sessão antes da carga, com o DISPLAY1 ocupado: o desbloqueio aplica o modo (R5).
        Cenario lado = new Cenario().Aplicar(new SessionLocked(), Ocupados(Display1), new Loaded(TopologiasDeExemplo.LadoALado, null, Preferencias.Padrao));
        lado.EstaEscondido(MotivoDoOcultamento.PorSessao).SemTransicao();
        lado.Aplicar(new SessionUnlocked()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(NoDisplay2, lado.Ancora, "desbloqueio com o DISPLAY1 ocupado: no livre");
        MesmaPosicao(PosicaoInicial, lado.Atual.RetornoDaTelaCheia, "desbloqueio: retorno no DISPLAY1");
        Cenario um = new Cenario().Aplicar(new SessionLocked(), Ocupados(Display1), new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        um.Aplicar(new SessionUnlocked()).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual(0, um.Efeitos.Count, "desbloqueio sem monitor livre: a janela nem aparece");
        um.Aplicar(Ocupados()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, um.Ancora, "o fim da tela cheia o mostra");
    }

    // Item Q8, R12. Linha: qualquer estado visível, exceto PRESSED, DRAGGING e EXITING | FULLSCREEN_TARGETS_CHANGED ("uma vez por mudança") e linha HIDDEN(POR_TELA_CHEIA) | CMD_SHOW ("o modo não o oculta de novo até a próxima mudança").
    [Teste]
    public static void Q8_MesmoConjuntoNaoAgeDuasVezes()
    {
        Cenario c = Transferido();
        c.Aplicar(Ocupados(Display1)).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "o mesmo conjunto repetido não age");

        // Depois de CMD_SHOW em HIDDEN(POR_TELA_CHEIA), o mesmo conjunto não esconde de novo...
        Cenario h = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(Display1, Display2)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        h.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, h.Ancora, "aparece na posição de antes, no DISPLAY1 ocupado");
        h.Aplicar(Ocupados(Display1, Display2)).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(0, h.Efeitos.Count, "o mesmo conjunto não o esconde de novo");
        // ... e um conjunto diferente volta a agir.
        h.Aplicar(Ocupados(Display1)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Display2, h.Retrato.ChaveMonitor, "conjunto diferente: transfere");

        // Com um monitor só: CMD_SHOW, o mesmo conjunto não age, um diferente (com chave desconhecida) esconde de novo.
        Cenario u = Cenario.Parado().Aplicar(Ocupados(Display1), new CmdShow()).Esta(Estado.Idle);
        u.Aplicar(Ocupados(Display1)).Esta(Estado.Idle).SemTransicao();
        u.Aplicar(Ocupados(Display1, Desconhecido)).Percorreu(Estado.Idle, Estado.Hidden).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
    }

    // Linha: qualquer, exceto BOOTING, PRESSED e DRAGGING | CMD_RESET_POSITION durante um episódio de tela cheia: voltar à posição inicial é escolha do usuário; grava a posição inicial, descarta o retorno, e o fim da tela cheia não desfaz o reset (visível ou escondido pelo usuário).
    [Teste]
    public static void CmdResetPositionDuranteOEpisodio_DescartaORetorno()
    {
        // Solto em (600,1032) no DISPLAY1; {1} o leva ao DISPLAY2 com o retorno em (600,1032), longe da posição inicial.
        Cenario Montar()
        {
            Cenario m = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragEnd(new PontoPx(600, 1000)));
            m.Aplicar(Ocupados(Display1));
            Afirmar.Igual(Display2, m.Retrato.ChaveMonitor, "transferido");
            Afirmar.Igual(new PontoPx(600, 1032), Afirmar.NaoNulo(m.Atual.RetornoDaTelaCheia, "retorno").AncoraAbsoluta, "retorno onde foi solto");
            return m;
        }

        Cenario c = Montar().Aplicar(new CmdResetPosition()).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "visível: posição inicial, no DISPLAY1 ocupado");
        MesmaPosicao(PosicaoInicial, c.Efeito<GravarPosicao>().Posicao, "visível: grava a posição inicial");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "visível: o reset descarta o retorno");
        c.Aplicar(Ocupados()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "visível: o fim da tela cheia não desfaz o reset");

        Cenario h = Montar().Aplicar(new CmdHide(), new CmdResetPosition()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        MesmaPosicao(PosicaoInicial, h.Efeito<GravarPosicao>().Posicao, "escondido: grava a posição inicial sem mostrar");
        Afirmar.Nulo(h.Atual.RetornoDaTelaCheia, "escondido: o reset descarta o retorno");
        h.Aplicar(Ocupados()).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        h.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, h.Ancora, "escondido: reaparece na posição inicial, não no retorno");
    }

    // Item Q10, R1. Linha: qualquer estado visível com painel aberto | ENERGY_SELECTED(nivel): só aceita BAIXA, MEDIA ou ALTA; SETTINGS_CHANGED e a carga com nível desconhecido usam Média (SECURITY.md 7). A agenda e a decisão seguinte funcionam com o perfil Média.
    [Teste]
    public static void Q10_NivelDeEnergiaForaDoEnum_EhIgnoradoOuViraMedia()
    {
        foreach (NivelDeEnergia invalido in new[] { (NivelDeEnergia)99, (NivelDeEnergia)3, (NivelDeEnergia)(-1) })
        {
            string contexto = $"nível {(int)invalido}";

            // ENERGY_SELECTED fora do enum, com o painel aberto: ignorado, sem gravar.
            Cenario painel = Cenario.Parado(ComPainel).Aplicar(new EnergyPanelOpen());
            painel.Aplicar(new EnergySelected(invalido)).Esta(Estado.Idle).SemTransicao();
            Afirmar.Igual(0, painel.Efeitos.Count, $"{contexto}: ENERGY_SELECTED ignorado, sem GravarPreferencias");
            Afirmar.Igual(NivelDeEnergia.Media, painel.Retrato.Energia, $"{contexto}: a energia não muda");
            painel.Aplicar(new EnergyPanelClose());
            FaixaDeMedia(painel.Efeito<AgendarDecisao>().Atraso, $"{contexto}: agenda depois do painel");
            DescansarAgora(painel, $"{contexto}: decisão depois do painel");

            // SETTINGS_CHANGED fora do enum: Média (mesmo vindo de Alta).
            Cenario config = Cenario.Parado().Aplicar(new SettingsChanged(new Preferencias(NivelDeEnergia.Alta, true)), new SettingsChanged(new Preferencias(invalido, true)));
            Afirmar.Igual(NivelDeEnergia.Media, config.Retrato.Energia, $"{contexto}: SETTINGS_CHANGED vira Média");
            config.Aplicar(new CmdPauseAutonomy(), new CmdResumeAutonomy());
            FaixaDeMedia(config.Efeito<AgendarDecisao>().Atraso, $"{contexto}: agenda depois de SETTINGS_CHANGED");
            DescansarAgora(config, $"{contexto}: decisão depois de SETTINGS_CHANGED");

            // Carga fora do enum: Média.
            Cenario carga = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, new Preferencias(invalido, true)));
            carga.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
            Afirmar.Igual(NivelDeEnergia.Media, carga.Retrato.Energia, $"{contexto}: Loaded vira Média");
            FaixaDeMedia(carga.Efeito<AgendarDecisao>().Atraso, $"{contexto}: agenda depois da carga");
            DescansarAgora(carga, $"{contexto}: decisão depois da carga");
        }
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>Carregado em <see cref="Cenario.TresEmLinha"/> na posição salva dada, parado.</summary>
    private static Cenario CarregadoEm(PosicaoDoPersonagem salva)
        => new Cenario().Aplicar(new Loaded(Cenario.TresEmLinha, salva, Preferencias.Padrao)).Esta(Estado.Idle);

    /// <summary>LadoALado, parado e transferido pela tela cheia no DISPLAY1: no DISPLAY2, com o retorno no DISPLAY1.</summary>
    // ---------------------------------------------------------------- DEC-034: "Desviar da tela cheia" no menu

    // Desligar pelo menu faz o mesmo que pelas preferências (linha SETTINGS_CHANGED que desliga o modo) e grava a escolha; a
    // transição para o mesmo estado registra o comando. O mesmo valor de novo, ou antes da carga, é ignorado.
    [Teste]
    public static void MenuDesliga_VoltaAPosicaoAnteriorEGravaAEscolha()
    {
        Cenario c = Transferido().Aplicar(new CmdSetFullscreenMode(false));
        Afirmar.Igual("CMD_SET_FULLSCREEN_MODE: desligado", c.Transicoes[0].Regra);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "volta à posição de antes");
        Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, "sem retorno");
        Afirmar.Falso(c.Atual.Preferencias.ModoTelaCheia, "modo desligado");
        Afirmar.Falso(c.Efeito<GravarPreferencias>().Preferencias.ModoTelaCheia, "grava o modo desligado");
        c.SemEfeito<GravarPosicao>();

        c.Aplicar(new CmdSetFullscreenMode(false)).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "o mesmo valor de novo: nada");

        Resultado antes = Maquina.Aplicar(EstadoDoNucleo.Inicial(1), new CmdSetFullscreenMode(false), new ConfiguracaoDoNucleo());
        Afirmar.Igual(0, antes.Efeitos.Count, "antes da carga: nenhum efeito");
        Afirmar.Verdadeiro(antes.Estado.Preferencias.ModoTelaCheia, "antes da carga: o modo continua ligado");
    }

    // Ligar pelo menu com ele num monitor já ocupado (o modo desligado só guardava o cache): ele sai de lá na hora, como se a
    // tela cheia tivesse acabado de começar, e o fim dela o traz de volta. Num monitor só, esconde pela tela cheia; num gesto
    // do usuário, nada muda (invariante 14).
    [Teste]
    public static void MenuLiga_ComEleNoMonitorOcupado_SaiDeLa()
    {
        var desligado = new Preferencias(NivelDeEnergia.Media, false);
        Cenario c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, null, desligado), Ocupados(Display1));
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "com o modo desligado, fica");
        c.Aplicar(new CmdSetFullscreenMode(true));
        Afirmar.Igual("CMD_SET_FULLSCREEN_MODE: ligado", c.Transicoes[0].Regra);
        Afirmar.Igual(NoDisplay2, c.Ancora, "foi para o monitor livre");
        Afirmar.Verdadeiro(c.Efeito<GravarPreferencias>().Preferencias.ModoTelaCheia, "grava o modo ligado");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "retorno guardado");
        c.Aplicar(Ocupados());
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "o fim da tela cheia o traz de volta");

        Cenario um = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, desligado), Ocupados(Display1));
        um.Aplicar(new CmdSetFullscreenMode(true)).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);

        Cenario gesto = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.LadoALado, null, desligado), Ocupados(Display1), new Press(Cenario.PontoOpaco));
        gesto.Aplicar(new CmdSetFullscreenMode(true)).Esta(Estado.Pressed);
        Afirmar.Igual(Cenario.AncoraInicial, gesto.Ancora, "o gesto continua onde estava");
        Afirmar.Verdadeiro(gesto.Atual.Preferencias.ModoTelaCheia, "mas o modo ligou");
    }

    private static Cenario Transferido()
    {
        Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado).Aplicar(Ocupados(Display1));
        Afirmar.Igual(NoDisplay2, c.Ancora, "transferido para o DISPLAY2");
        MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, "retorno no DISPLAY1");
        return c;
    }

    private static (Evento Esconder, MotivoDoOcultamento Motivo, Evento Reaparecer)[] PedidosDoSistema() =>
    [
        (new Suspending(), MotivoDoOcultamento.PorSuspensao, new Resumed()),
        (new SessionLocked(), MotivoDoOcultamento.PorSessao, new SessionUnlocked()),
    ];

    /// <summary>Leva um cenário parado em IDLE ao estado pedido pelo caminho de <see cref="Cenario.Em"/>, onde o personagem estiver.</summary>
    private static void Levar(Cenario c, Estado alvo)
    {
        c.Esta(Estado.Idle);
        var noCorpo = new PontoPx(c.Ancora.X, c.Ancora.Y - 32);
        void Decidir(AcoesAutonomas acao) => c.AplicarCom(c.Config with { Acoes = acao }, new AutonomyTimer(c.Atual.Geracao));
        switch (alvo)
        {
            case Estado.Walking: Decidir(AcoesAutonomas.Andar); break;
            case Estado.Climbing: Decidir(AcoesAutonomas.Escalar); break;
            case Estado.Jumping: Decidir(AcoesAutonomas.Pular); break;
            case Estado.Resting: Decidir(AcoesAutonomas.Descansar); break;
            case Estado.Hanging:
                Decidir(AcoesAutonomas.Escalar);
                c.Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior));
                break;
            case Estado.Falling:
                Decidir(AcoesAutonomas.Andar);
                c.Aplicar(new MovementSignal(SinalDeMovimento.FimDoChao));
                break;
            case Estado.Landing:
                Decidir(AcoesAutonomas.Andar);
                c.Aplicar(new MovementSignal(SinalDeMovimento.FimDoChao), new MovementSignal(SinalDeMovimento.ContatoComOChao));
                break;
            case Estado.Reacting:
                c.Aplicar(new Press(noCorpo), new Click());
                break;
            default:
                throw new ArgumentException($"{alvo} não é montado por este auxiliar.", nameof(alvo));
        }
        c.Esta(alvo, $"levado a {alvo}");
    }

    /// <summary>
    /// Dispara a agenda só com "descansar" permitido e confere que a decisão aconteceu com o perfil
    /// Média: IDLE -> RESTING, com o próximo despertar na faixa de descanso de Média (30 a 90 s).
    /// </summary>
    private static void DescansarAgora(Cenario c, string contexto)
    {
        c.AplicarCom(c.Config with { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(c.Atual.Geracao)).Percorreu(Estado.Idle, Estado.Resting);
        TimeSpan atraso = c.Efeito<AgendarDecisao>().Atraso;
        Afirmar.Verdadeiro(atraso >= TimeSpan.FromSeconds(30) && atraso <= TimeSpan.FromSeconds(90), $"{contexto}: descanso de {atraso} na faixa de Média (30 a 90 s)");
    }

    private static FullscreenTargetsChanged Ocupados(params string[] chaves) => new(new MonitoresOcupados(chaves));

    private static void FaixaDeMedia(TimeSpan atraso, string contexto)
        => Afirmar.Verdadeiro(atraso >= TimeSpan.FromSeconds(8) && atraso <= TimeSpan.FromSeconds(20), $"{contexto}: {atraso} na faixa de Média (8 a 20 s)");

    /// <summary>Atraso da faixa de decisão de Alta (3 a 10 s) abaixo do mínimo de Média (8 s): só o perfil Alta o produz.</summary>
    private static void FaixaDeAltaAbaixoDeMedia(TimeSpan atraso, string contexto)
        => Afirmar.Verdadeiro(atraso >= TimeSpan.FromSeconds(3) && atraso < TimeSpan.FromSeconds(8), $"{contexto}: {atraso} entre 3 e 8 s");

    private static void MesmaPosicao(PosicaoDoPersonagem esperada, PosicaoDoPersonagem? obtida, string contexto)
    {
        PosicaoDoPersonagem p = Afirmar.NaoNulo(obtida, contexto);
        Afirmar.Igual(esperada.ChaveMonitor, p.ChaveMonitor, $"{contexto}: monitor");
        Afirmar.Igual(esperada.AncoraAbsoluta, p.AncoraAbsoluta, $"{contexto}: âncora");
        Afirmar.Aproximado(esperada.FracaoX, p.FracaoX, 1e-9, $"{contexto}: fração X");
        Afirmar.Aproximado(esperada.FracaoY, p.FracaoY, 1e-9, $"{contexto}: fração Y");
    }

    /// <summary>Como <see cref="MesmaPosicao"/>, sem lançar: nulo se as posições batem, ou a diferença descrita.</summary>
    private static string? Diferenca(PosicaoDoPersonagem esperada, PosicaoDoPersonagem? obtida)
    {
        if (obtida is null) return "nula";
        bool igual = obtida.ChaveMonitor == esperada.ChaveMonitor && obtida.AncoraAbsoluta == esperada.AncoraAbsoluta
            && Math.Abs(obtida.FracaoX - esperada.FracaoX) <= 1e-9 && Math.Abs(obtida.FracaoY - esperada.FracaoY) <= 1e-9;
        return igual ? null : $"{Gravacao.DescreverPosicao(obtida)} (esperado {Gravacao.DescreverPosicao(esperada)})";
    }
}
