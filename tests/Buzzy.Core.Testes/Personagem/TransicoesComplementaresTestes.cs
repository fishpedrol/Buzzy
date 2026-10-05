using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Complementos da tabela "Transições principais" de ARCHITECTURE.md 2.6 (revisão do gate da
/// Fase 2, itens A a F e I a P): linhas que <see cref="TransicoesTestes"/> só exercita a partir de
/// IDLE, asserções que o perfil padrão tornava sempre verdadeiras e comportamentos do núcleo que
/// ainda não estavam fixados. Os comentários "Linha:" citam a tabela pelas colunas "De | Evento",
/// porque a numeração do arquivo muda; "Item" cita a revisão e "Rn" cita as regras do núcleo já
/// implementadas em Maquina.cs. Tempos e posições esperados são literais calculados à mão, como
/// pede <see cref="TopologiasDeExemplo"/>.
/// </summary>
internal static class TransicoesComplementaresTestes
{
    private static readonly ConfiguracaoDoNucleo Padrao = new();
    private static readonly ConfiguracaoDoNucleo ComPainel = new() { PainelDeEnergiaDisponivel = true };
    private static readonly ConfiguracaoDoNucleo ComQueda = new() { QuedaFisica = true };

    /// <summary>
    /// Perfis com decisões de 200 a 900 ms e descansos de 300 a 900 ms, bem abaixo do intervalo de
    /// acomodação padrão (3 s): com eles, "atraso igual ao intervalo" só passa se o piso de R11 for
    /// aplicado. Com o perfil padrão (sorteios de 3 s para cima), as asserções "≥ intervalo" de
    /// TransicoesTestes passam mesmo sem piso.
    /// </summary>
    private static readonly ConfiguracaoDoNucleo Curto = new() { Perfil = PerfilCurto };

    private static readonly TimeSpan TresSegundos = TimeSpan.FromSeconds(3);

    /// <summary>Faixas do perfil padrão Média (DEC-014): decisão de 8 a 20 s e descanso de 30 a 90 s, sem sobreposição.</summary>
    private static readonly (TimeSpan Minimo, TimeSpan Maximo) DecisaoDeMedia = (TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(20));

    private static readonly (TimeSpan Minimo, TimeSpan Maximo) DescansoDeMedia = (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90));

    private static readonly Estado[] AutonomosEFisicos =
        [Estado.Idle, Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Resting, Estado.Falling, Estado.Landing];

    /// <summary>Estados visíveis além de IDLE, de onde as linhas "qualquer estado visível" ainda não eram exercitadas.</summary>
    private static readonly Estado[] VisiveisAlemDeIdle =
        [Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Falling, Estado.Landing, Estado.Resting, Estado.Reacting];

    /// <summary>Posição inicial em <see cref="TopologiasDeExemplo.UmMonitor"/>: 85% da largura, no chão.</summary>
    private static readonly PosicaoDoPersonagem PosicaoInicial = new(TopologiasDeExemplo.Display1, 0.85, 1.0, new PontoPx(1632, 1032));

    // ---------------------------------------------------------------- A: intervalo de acomodação (R11)

    // Item A, R11. Linha: SETTLING | com apoio | IDLE | Autonomia retomada depois de um intervalo de acomodação.
    [Teste]
    public static void Acomodacao_DragEndComPerfilCurto_AgendaExatamenteNoIntervalo()
    {
        Cenario c = Cenario.Em(Estado.Dragging, Curto).Aplicar(new DragEnd(new PontoPx(1000, 1000)));
        c.Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "o sorteio (200 a 900 ms) fica abaixo do piso: vale o intervalo de acomodação");
    }

    // Item A, R11. Linha: qualquer estado visível com painel aberto | ENERGY_PANEL_CLOSE | permanece | Fecha o painel e retoma a agenda após intervalo de acomodação.
    [Teste]
    public static void Acomodacao_EnergyPanelCloseComPerfilCurto_AgendaExatamenteNoIntervalo()
    {
        ConfiguracaoDoNucleo cfg = Curto with { PainelDeEnergiaDisponivel = true };
        Cenario c = Cenario.Parado(cfg).Aplicar(new EnergyPanelOpen());
        Afirmar.Falso(c.Atual.DecisaoAgendada, "aberto: sem agenda");
        c.Aplicar(new EnergyPanelClose()).Esta(Estado.Idle).SemTransicao();
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "fechar o painel retoma a agenda só depois do intervalo de acomodação");
    }

    // Item A, R11. Linha: qualquer estado visível | CMD_PAUSE_AUTONOMY, CMD_RESUME_AUTONOMY | permanece | Retomar agenda a próxima decisão após o intervalo de acomodação.
    [Teste]
    public static void Acomodacao_CmdResumeAutonomyComPerfilCurto_AgendaNoIntervaloEmCadaEstadoQueDecide()
    {
        foreach (Estado origem in new[] { Estado.Idle, Estado.Resting, Estado.Climbing, Estado.Hanging })
        {
            Cenario c = Cenario.Em(origem, Curto);
            c.Aplicar(new CmdPauseAutonomy()).Esta(origem).SemTransicao().Efeito<CancelarDecisao>();
            c.Aplicar(new CmdResumeAutonomy()).Esta(origem).SemTransicao();
            Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, $"{origem}: retomar agenda no intervalo de acomodação, não no sorteio curto");
        }
    }

    // Item A, R11: o piso vale para toda decisão agendada, não só depois de uma interação.
    [Teste]
    public static void Acomodacao_TodaDecisaoComPerfilCurto_RespeitaOPisoEmIdleRestingClimbingEHanging()
    {
        Cenario c = Cenario.Parado(Curto);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "IDLE depois da carga");

        c.AplicarCom(Curto with { Acoes = AcoesAutonomas.TrocarExpressao }, new AutonomyTimer(c.Atual.Geracao)).Esta(Estado.Idle);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "IDLE depois de trocar a expressão");

        c.AplicarCom(Curto with { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(c.Atual.Geracao)).Percorreu(Estado.Idle, Estado.Resting);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "RESTING: o descanso sorteado (300 a 900 ms) também tem piso");

        c.Decidir().Percorreu(Estado.Resting, Estado.Idle);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "IDLE depois de acordar");

        c.AplicarCom(Curto with { Acoes = AcoesAutonomas.Escalar }, new AutonomyTimer(c.Atual.Geracao)).Percorreu(Estado.Idle, Estado.Climbing);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "CLIMBING");

        c.Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior)).Percorreu(Estado.Climbing, Estado.Hanging);
        Afirmar.Igual(TresSegundos, c.Efeito<AgendarDecisao>().Atraso, "HANGING");
    }

    // Item A: o piso não encurta nem fixa um sorteio maior; com o perfil padrão (Média), vale o
    // sorteio. A faixa sozinha aceitaria um atraso fixo (no mínimo do perfil, por exemplo): por isso
    // as sementes também precisam dar atrasos diferentes.
    [Teste]
    public static void Acomodacao_PisoNaoSubstituiUmSorteioMaiorDoPerfil()
    {
        var distintos = new HashSet<TimeSpan>();
        for (ulong semente = 1; semente <= 20; semente++)
        {
            Cenario c = Cenario.Parado(semente: semente).Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragEnd(new PontoPx(900, 1000)));
            TimeSpan atraso = c.Efeito<AgendarDecisao>().Atraso;
            NaFaixa(DecisaoDeMedia, atraso, $"semente {semente}: decisão de Média");
            distintos.Add(atraso);
        }
        Afirmar.Verdadeiro(distintos.Count >= 10, $"o atraso é sorteado na faixa: {distintos.Count} valores distintos em 20 sementes ({string.Join(", ", distintos.Order())})");
    }

    // ---------------------------------------------------------------- B: clique duplo e menu "Energia" (linha PRESSED (segundo clique), IDLE, REACTING | DOUBLE_CLICK ou menu "Energia")

    // Item B. Linha: PRESSED (segundo clique), IDLE, REACTING | DOUBLE_CLICK ou menu "Energia" | SETTLING se vier de PRESSED; senão permanece | Antes da Fase 8, o segundo clique só produz reação não verbal.
    [Teste]
    public static void Idle_DoubleClickSemPainel_SoReacaoNaoVerbalSemTransicaoNemRelogio()
    {
        Cenario c = Cenario.Parado().Aplicar(new DoubleClick());
        c.Esta(Estado.Idle).SemTransicao().SemEfeito<AbrirPainelDeEnergia>().SemEfeito<LigarRelogio>();
        Afirmar.Igual(Sinal.FoiClicadoDuasVezes, c.Retrato.Sinal, "reação não verbal");
        Afirmar.Igual(Expressao.Rindo, c.Retrato.Expressao, "a reação é a expressão");
        Afirmar.Falso(c.Atual.RelogioAtivo, "a expressão não liga o relógio");
        Afirmar.Falso(c.Atual.PainelAberto);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "não se move");
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "a agenda continua");
        Afirmar.Igual(0, c.Efeitos.Count, "nenhum efeito");
    }

    // Item B. Linha: PRESSED (segundo clique), IDLE, REACTING | DOUBLE_CLICK ou menu "Energia", a partir de REACTING, com o painel (Fase 8): permanece e abre o painel, que pausa a autonomia.
    [Teste]
    public static void Reacting_DoubleClickComPainel_AbreOPainelESemAgendaNemDepoisDaReacao()
    {
        Cenario c = Cenario.Em(Estado.Reacting, ComPainel).Aplicar(new DoubleClick());
        c.Esta(Estado.Reacting).SemTransicao().Efeito<AbrirPainelDeEnergia>();
        Afirmar.Verdadeiro(c.Atual.PainelAberto);
        // REACTING nunca tem agenda: a pausa da autonomia só aparece no fim da reação, abaixo. Aqui,
        // o clique duplo não encurta nem reinicia a reação.
        Afirmar.Igual(ComPainel.PassosDaReacao, c.Atual.PassosRestantes, "a reação continua de onde estava");

        c.Passos(ComPainel.PassosDaReacao - 1).Esta(Estado.Reacting, "a reação dura o mesmo com o painel aberto");
        c.Passos(1).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle).SemEfeito<AgendarDecisao>();
        Afirmar.Verdadeiro(c.Atual.PainelAberto, "o fim da reação não fecha o painel");
        Afirmar.Falso(c.Atual.DecisaoAgendada, "a agenda continua cancelada enquanto o painel está aberto");
    }

    // Item B. Linha: PRESSED (segundo clique), IDLE, REACTING | DOUBLE_CLICK ou menu "Energia", pelo menu (ENERGY_PANEL_OPEN): abre em IDLE e REACTING; em PRESSED e DRAGGING é ignorado.
    [Teste]
    public static void EnergyPanelOpen_AbreEmIdleEReactingEEIgnoradoEmPressedEDragging()
    {
        Cenario parado = Cenario.Parado(ComPainel).Aplicar(new EnergyPanelOpen());
        parado.Esta(Estado.Idle).SemTransicao().Efeito<AbrirPainelDeEnergia>();
        parado.Efeito<CancelarDecisao>();
        Afirmar.Verdadeiro(parado.Atual.PainelAberto);
        Afirmar.Falso(parado.Atual.DecisaoAgendada, "o painel pausa a autonomia");
        Afirmar.Igual(Cenario.AncoraInicial, parado.Ancora, "invariante 8");

        Cenario reagindo = Cenario.Em(Estado.Reacting, ComPainel).Aplicar(new EnergyPanelOpen());
        reagindo.Esta(Estado.Reacting).SemTransicao().Efeito<AbrirPainelDeEnergia>();
        Afirmar.Verdadeiro(reagindo.Atual.PainelAberto);

        foreach (Estado gesto in new[] { Estado.Pressed, Estado.Dragging })
            Ignorado(Cenario.Em(gesto, ComPainel), new EnergyPanelOpen(), $"{gesto} + ENERGY_PANEL_OPEN");

        // Antes da Fase 8 o painel não existe: o pedido não abre nada.
        Ignorado(Cenario.Parado(), new EnergyPanelOpen(), "IDLE sem painel disponível");
    }

    // ---------------------------------------------------------------- C: RESTING e CLIMBING (linha RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_*)

    // Item C. Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* | conforme a linha correspondente. DOUBLE_CLICK só vale em PRESSED, IDLE e REACTING; a arbitragem sempre emite PRESS antes dele.
    [Teste]
    public static void RestingEClimbing_DoubleClick_EhIgnorado()
    {
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
            foreach (ConfiguracaoDoNucleo cfg in new[] { Padrao, ComPainel })
                Ignorado(Cenario.Em(origem, cfg), new DoubleClick(), $"{origem} + DOUBLE_CLICK (painel disponível: {cfg.PainelDeEnergiaDisponivel})");
    }

    // Item C. Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* + linha HIDDEN(...) | CMD_SHOW: visível, CMD_SHOW não muda nada.
    [Teste]
    public static void RestingEClimbing_CmdShowVisivel_NaoMudaNada()
    {
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
            Ignorado(Cenario.Em(origem), new CmdShow(), $"{origem} + CMD_SHOW");
    }

    // Item C. Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* + linha qualquer estado visível | CMD_PAUSE_AUTONOMY, CMD_RESUME_AUTONOMY | permanece. O piso do intervalo de acomodação está fixado com o perfil curto (item A); aqui, com o perfil padrão, a faixa sorteada mostra qual agenda voltou: o descanso em RESTING, a decisão em CLIMBING.
    [Teste]
    public static void RestingEClimbing_PausarERetomar_PermanecemEReagendam()
    {
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
        {
            Cenario c = Cenario.Em(origem);
            Ignorado(c, new CmdResumeAutonomy(), $"{origem}: retomar sem estar pausada");
            c.Aplicar(new CmdPauseAutonomy()).Esta(origem).SemTransicao().Efeito<CancelarDecisao>();
            Afirmar.Verdadeiro(c.Retrato.AutonomiaPausada, $"{origem}: pausada");
            c.Aplicar(new CmdResumeAutonomy()).Esta(origem).SemTransicao();
            Afirmar.Falso(c.Retrato.AutonomiaPausada, $"{origem}: retomada");
            NaFaixa(origem == Estado.Resting ? DescansoDeMedia : DecisaoDeMedia, c.Efeito<AgendarDecisao>().Atraso,
                $"{origem}: {(origem == Estado.Resting ? "descanso" : "decisão")} de Média");
            Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, $"{origem}: volta a decidir");
        }
    }

    // Item C. Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* + CMD_OPEN_SETTINGS (Fase 8): só com a janela de configurações disponível, e sem mudar o estado.
    [Teste]
    public static void RestingEClimbing_CmdOpenSettings_SoComConfiguracoesDisponiveis()
    {
        var comConfiguracoes = new ConfiguracaoDoNucleo { ConfiguracoesDisponiveis = true };
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
        {
            Ignorado(Cenario.Em(origem), new CmdOpenSettings(), $"{origem}: sem configurações disponíveis");

            Cenario c = Cenario.Em(origem, comConfiguracoes).Aplicar(new CmdOpenSettings());
            c.Esta(origem).SemTransicao();
            Afirmar.Sequencia(["AbrirConfiguracoes"], c.Efeitos.Select(e => e.GetType().Name), $"{origem}: só abre as configurações");
        }
    }

    // Item C. Linha: RESTING, CLIMBING | PRESS, DOUBLE_CLICK, CMD_* + CMD_RESET_POSITION: volta à posição inicial por SETTLING e grava.
    [Teste]
    public static void RestingEClimbing_CmdResetPosition_VaiParaAPosicaoInicialPorSettlingEGrava()
    {
        foreach (Estado origem in new[] { Estado.Resting, Estado.Climbing })
        {
            Cenario c = EmDepoisDeSoltar(origem, new PontoPx(300, 1000));
            Afirmar.Igual(new PontoPx(300, 1032), c.Ancora, $"{origem}: começa longe da posição inicial");
            c.Aplicar(new CmdResetPosition()).Percorreu(origem, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: posição inicial");
            Afirmar.Igual(Cenario.AncoraInicial, c.Efeito<MoverJanela>().Destino.Ancora, $"{origem}: a janela vai junto");
            MesmaPosicao(PosicaoInicial, c.Efeito<GravarPosicao>().Posicao, $"{origem}: grava a posição inicial");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: parado, sem relógio");
            Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, $"{origem}: volta a decidir");
        }
    }

    // ---------------------------------------------------------------- D: contato com o chão (linhas FALLING, LANDING | contato com o chão e JUMPING, FALLING | contato com o chão)

    // Item D. Linha: FALLING, LANDING | contato com o chão | LANDING, depois IDLE. Em LANDING um contato repetido não reinicia o pouso.
    [Teste]
    public static void Landing_ContatoComOChao_EhIgnoradoEOPousoTemOMesmoTotalDePassos()
    {
        Afirmar.Igual(12, PassosAteIdle(Cenario.Em(Estado.Landing)), "pouso sem contato repetido: 12 passos");

        Cenario c = Cenario.Em(Estado.Landing).Passos(5).Esta(Estado.Landing);
        int restantes = c.Atual.PassosRestantes;
        Afirmar.Igual(7, restantes, "5 dos 12 passos do pouso já correram");
        c.Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao)).Esta(Estado.Landing).SemTransicao();
        Afirmar.Igual(restantes, c.Atual.PassosRestantes, "o contato repetido não reinicia o pouso");
        Afirmar.Igual(Sinal.Nenhum, c.Retrato.Sinal, "não pousa de novo");
        Afirmar.Igual(0, c.Efeitos.Count, "nenhum efeito");
        Afirmar.Igual(7, PassosAteIdle(c), "o total continua 12 passos");
    }

    // Item D. Linha: JUMPING, FALLING | contato com o chão | LANDING, depois IDLE | Vale com o painel aberto ou fechado; o painel não altera a física.
    [Teste]
    public static void Jumping_ContatoComOChaoComPainelAberto_PousaEFicaParadoSemAgenda()
    {
        Cenario c = Cenario.Em(Estado.Jumping, ComPainel).Aplicar(new EnergyPanelOpen());
        Afirmar.Verdadeiro(c.Atual.PainelAberto, "o painel abre no meio do pulo");
        c.Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao)).Percorreu(Estado.Jumping, Estado.Landing);
        Afirmar.Igual(Sinal.Pousou, c.Retrato.Sinal);

        c.Passos(ComPainel.PassosDoPouso - 1).Esta(Estado.Landing, "o pouso dura o mesmo com o painel aberto");
        c.Passos(1).Percorreu(Estado.Landing, Estado.Idle).SemEfeito<AgendarDecisao>();
        Afirmar.Verdadeiro(c.Atual.PainelAberto, "o painel continua aberto");
        Afirmar.Falso(c.Atual.DecisaoAgendada, "com o painel aberto, a autonomia continua pausada");
        Afirmar.Falso(c.Atual.RelogioAtivo, "parado, sem relógio");
    }

    // ---------------------------------------------------------------- E: escolha ponderada pela energia (linha IDLE | AUTONOMY_TIMER)

    // Item E. Linha: IDLE | AUTONOMY_TIMER | WALKING, CLIMBING, JUMPING, RESTING ou permanece com um gesto curto | Escolha ponderada pela personalidade e pelo nível de energia, com semente.
    // Com 400 sementes por nível, as contagens de hoje são: descanso 156/70/21, andar 68/121/115,
    // escalar 23/54/74 e pular 0/25/60 (Baixa/Média/Alta). Média e Alta ficam praticamente empatadas
    // em andar, então andar só compara Baixa com as outras duas.
    [Teste]
    public static void Idle_AutonomyTimer_EscolhaPonderadaPeloNivelDeEnergia()
    {
        const int Sementes = 400;
        var contagem = new Dictionary<NivelDeEnergia, Dictionary<string, int>>();
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            var porDestino = new Dictionary<string, int>(StringComparer.Ordinal);
            for (ulong semente = 1; semente <= Sementes; semente++)
            {
                // A segunda execução só difere da primeira se a escolha usar uma fonte escondida de
                // aleatoriedade (Random.Shared, relógio, estado estático): é o que "com semente"
                // (invariante 7) exclui.
                (string destino, string retrato) = PrimeiraDecisao(nivel, semente);
                (string outroDestino, string outroRetrato) = PrimeiraDecisao(nivel, semente);
                Afirmar.Igual(destino, outroDestino, $"{nivel}, semente {semente}: mesma semente e mesmo nível, mesmo destino");
                Afirmar.Igual(retrato, outroRetrato, $"{nivel}, semente {semente}: mesmo retrato");
                porDestino[destino] = porDestino.GetValueOrDefault(destino) + 1;
            }
            contagem[nivel] = porDestino;
        }

        int Quantos(NivelDeEnergia nivel, string destino) => contagem[nivel].GetValueOrDefault(destino);
        string Resumo(NivelDeEnergia nivel) => string.Join(", ", contagem[nivel].OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
            Console.WriteLine($"         {nivel}, {Sementes} sementes: {Resumo(nivel)}");

        Afirmar.Igual(0, Quantos(NivelDeEnergia.Baixa, "Jumping"), $"Baixa nunca pula ({Resumo(NivelDeEnergia.Baixa)})");
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            foreach (string destino in new[] { "Walking", "Climbing", "Resting", "Gesto", "Expressao" })
                Afirmar.Verdadeiro(Quantos(nivel, destino) > 0, $"{nivel} alcança {destino} ({Resumo(nivel)})");
            // Toda decisão faz alguma coisa: a troca de expressão sorteia uma diferente da atual.
            Afirmar.Igual(0, Quantos(nivel, "Nada"), $"{nivel}: nenhuma decisão sem efeito ({Resumo(nivel)})");
        }
        Afirmar.Verdadeiro(Quantos(NivelDeEnergia.Media, "Jumping") > 0 && Quantos(NivelDeEnergia.Alta, "Jumping") > 0, "Média e Alta pulam");

        int descansoBaixa = Quantos(NivelDeEnergia.Baixa, "Resting"), descansoMedia = Quantos(NivelDeEnergia.Media, "Resting"), descansoAlta = Quantos(NivelDeEnergia.Alta, "Resting");
        Afirmar.Verdadeiro(descansoBaixa > descansoMedia && descansoMedia > descansoAlta, $"descanso cai com a energia: Baixa {descansoBaixa}, Média {descansoMedia}, Alta {descansoAlta}");
        int andarBaixa = Quantos(NivelDeEnergia.Baixa, "Walking"), andarMedia = Quantos(NivelDeEnergia.Media, "Walking"), andarAlta = Quantos(NivelDeEnergia.Alta, "Walking");
        Afirmar.Verdadeiro(andarBaixa < andarMedia && andarBaixa < andarAlta, $"andar sobe de Baixa para Média e Alta: Baixa {andarBaixa}, Média {andarMedia}, Alta {andarAlta}");
        int escalarBaixa = Quantos(NivelDeEnergia.Baixa, "Climbing"), escalarMedia = Quantos(NivelDeEnergia.Media, "Climbing"), escalarAlta = Quantos(NivelDeEnergia.Alta, "Climbing");
        Afirmar.Verdadeiro(escalarBaixa < escalarMedia && escalarMedia < escalarAlta, $"escalar sobe com a energia: Baixa {escalarBaixa}, Média {escalarMedia}, Alta {escalarAlta}");
        int pularMedia = Quantos(NivelDeEnergia.Media, "Jumping"), pularAlta = Quantos(NivelDeEnergia.Alta, "Jumping");
        Afirmar.Verdadeiro(pularMedia < pularAlta, $"pular sobe de Média para Alta: Média {pularMedia}, Alta {pularAlta}");
    }

    // ---------------------------------------------------------------- F: painel e pausa em outros estados visíveis (linhas qualquer estado visível com painel aberto | ENERGY_SELECTED(nivel) e ENERGY_PANEL_CLOSE; qualquer estado visível | CMD_PAUSE_AUTONOMY, CMD_RESUME_AUTONOMY)

    // Item F. Linha: qualquer estado visível com painel aberto | ENERGY_SELECTED(nivel) | permanece | "o novo nível afeta as próximas decisões autônomas". Linha: ... | ENERGY_PANEL_CLOSE | permanece | "retoma a agenda após intervalo de acomodação". Com os perfis marcados, o atraso agendado ao fechar mostra que a agenda já usa o perfil Alta: 4 s de decisão em CLIMBING e HANGING, 11 s de descanso em RESTING.
    [Teste]
    public static void VisiveisAlemDeIdle_AbrirEscolherEFecharOPainel_Permanecem()
    {
        ConfiguracaoDoNucleo cfg = ComPainel with { Perfil = PerfilMarcado };
        foreach (Estado origem in VisiveisAlemDeIdle)
        {
            Cenario c = Cenario.Em(origem, cfg);
            PontoPx ancora = c.Ancora;
            c.Aplicar(new EnergyPanelOpen()).Esta(origem).SemTransicao().Efeito<AbrirPainelDeEnergia>();
            Afirmar.Verdadeiro(c.Atual.PainelAberto, $"{origem}: painel aberto");

            c.Aplicar(new EnergySelected(NivelDeEnergia.Alta)).Esta(origem).SemTransicao();
            Afirmar.Igual(NivelDeEnergia.Alta, c.Efeito<GravarPreferencias>().Preferencias.Energia, $"{origem}: grava a preferência");
            Afirmar.Igual(NivelDeEnergia.Alta, c.Retrato.Energia, $"{origem}: nível novo");
            // Escolher o nível que já está não muda nada nem grava de novo.
            Ignorado(c, new EnergySelected(NivelDeEnergia.Alta), $"{origem}: o mesmo nível de novo");

            c.Aplicar(new EnergyPanelClose()).Esta(origem).SemTransicao();
            Afirmar.Falso(c.Atual.PainelAberto, $"{origem}: painel fechado");
            Afirmar.Igual(ancora, c.Ancora, $"{origem}: o painel não move o personagem (invariante 8)");
            bool decide = Maquina.DecideNoEstado(origem);
            Afirmar.Igual(decide, c.Tem<AgendarDecisao>(), $"{origem}: fechar retoma a agenda só nos estados que decidem");
            Afirmar.Igual(decide, c.Atual.DecisaoAgendada, $"{origem}: agenda pendente");
            if (decide)
            {
                TimeSpan esperado = origem == Estado.Resting ? MarcaDeDescanso(NivelDeEnergia.Alta) : MarcaDeDecisao(NivelDeEnergia.Alta);
                Afirmar.Igual(esperado, c.Efeito<AgendarDecisao>().Atraso, $"{origem}: {(origem == Estado.Resting ? "descanso" : "decisão")} do perfil Alta, o nível novo");
            }
        }
    }

    // Item F. Linha: qualquer estado visível | CMD_PAUSE_AUTONOMY, CMD_RESUME_AUTONOMY | permanece. Com o perfil padrão (Média), a faixa do atraso mostra qual agenda voltou: descanso (30 a 90 s) em RESTING, decisão (8 a 20 s) em CLIMBING e HANGING; o piso está fixado no item A.
    [Teste]
    public static void VisiveisAlemDeIdle_PausarERetomar_Permanecem()
    {
        foreach (Estado origem in VisiveisAlemDeIdle)
        {
            Cenario c = Cenario.Em(origem);
            bool decide = Maquina.DecideNoEstado(origem);
            bool relogio = c.Atual.RelogioAtivo;
            PontoPx ancora = c.Ancora;

            c.Aplicar(new CmdPauseAutonomy()).Esta(origem).SemTransicao();
            Afirmar.Verdadeiro(c.Retrato.AutonomiaPausada, $"{origem}: pausada");
            Afirmar.Igual(decide, c.Tem<CancelarDecisao>(), $"{origem}: pausar cancela a agenda só onde havia uma");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: nada agendado pausado");

            c.Aplicar(new CmdResumeAutonomy()).Esta(origem).SemTransicao();
            Afirmar.Falso(c.Retrato.AutonomiaPausada, $"{origem}: retomada");
            Afirmar.Igual(decide, c.Tem<AgendarDecisao>(), $"{origem}: retomar agenda só nos estados que decidem");
            if (decide)
            {
                NaFaixa(origem == Estado.Resting ? DescansoDeMedia : DecisaoDeMedia, c.Efeito<AgendarDecisao>().Atraso,
                    $"{origem}: {(origem == Estado.Resting ? "descanso" : "decisão")} de Média");
            }
            Afirmar.Igual(relogio, c.Atual.RelogioAtivo, $"{origem}: pausar e retomar não mexem no relógio do movimento");
            Afirmar.Igual(ancora, c.Ancora, $"{origem}: nem na posição");
        }
    }

    // Item F e R11. Linhas: qualquer estado visível com painel aberto | ENERGY_SELECTED(nivel) e ... | ENERGY_PANEL_CLOSE: o nível novo afeta as próximas decisões; fechar o painel agenda na faixa do perfil Alta, com o piso do intervalo de acomodação.
    [Teste]
    public static void Idle_EnergiaAltaEFecharOPainel_AgendaNaFaixaDoPerfilAltaComPiso()
    {
        // Piso de 5 s: acima do mínimo de Alta (3 s) e abaixo do de Média (8 s). Faixa esperada: 5 a 10 s.
        ConfiguracaoDoNucleo cfg = ComPainel with { IntervaloDeAcomodacao = TimeSpan.FromSeconds(5) };
        bool viuOPiso = false, viuAbaixoDeMedia = false;
        for (ulong semente = 1; semente <= 60; semente++)
        {
            Cenario c = Cenario.Parado(cfg, semente: semente).Aplicar(new EnergyPanelOpen(), new EnergySelected(NivelDeEnergia.Alta), new EnergyPanelClose());
            TimeSpan atraso = c.Efeito<AgendarDecisao>().Atraso;
            Afirmar.Verdadeiro(atraso >= TimeSpan.FromSeconds(5) && atraso <= TimeSpan.FromSeconds(10), $"semente {semente}: {atraso} entre 5 e 10 s");
            viuOPiso |= atraso == TimeSpan.FromSeconds(5);
            viuAbaixoDeMedia |= atraso < TimeSpan.FromSeconds(8);
        }
        Afirmar.Verdadeiro(viuOPiso, "algum sorteio de Alta abaixo de 5 s virou o piso");
        Afirmar.Verdadeiro(viuAbaixoDeMedia, "algum atraso abaixo do mínimo de Média: o perfil usado é o de Alta");
    }

    // ---------------------------------------------------------------- I: relógio em RESTING (linha RESTING | AUTONOMY_TIMER)

    // Item I. Linha: RESTING | AUTONOMY_TIMER | IDLE | Acorda e volta a decidir. Em IDLE sem gesto não há animação: o relógio continua desligado (DEC-011).
    [Teste]
    public static void Resting_AcordaEmIdleSemGesto_ORelogioContinuaDesligado()
    {
        Cenario c = Cenario.Em(Estado.Resting);
        Afirmar.Falso(c.Atual.RelogioAtivo, "descansando, sem relógio");
        c.Decidir().Percorreu(Estado.Resting, Estado.Idle).SemEfeito<LigarRelogio>();
        Afirmar.Igual(Gesto.Nenhum, c.Retrato.Gesto);
        Afirmar.Falso(c.Atual.RelogioAtivo, "acordado e parado, sem gesto: o relógio continua desligado");
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "volta a decidir");

        // O relógio só liga quando há o que animar: um gesto curto na próxima decisão.
        c.AplicarCom(Padrao with { Acoes = AcoesAutonomas.Gesto }, new AutonomyTimer(c.Atual.Geracao)).Esta(Estado.Idle).Efeito<LigarRelogio>();
        Afirmar.Diferente(Gesto.Nenhum, c.Retrato.Gesto);
    }

    // Item I. Linha: RESTING | AUTONOMY_TIMER ("o relógio só é religado neste momento"): TICK e troca de expressão em RESTING não ligam o relógio.
    [Teste]
    public static void Resting_TickEExpressionChange_NaoLigamORelogio()
    {
        Cenario c = Cenario.Em(Estado.Resting);
        for (int i = 0; i < 3; i++)
        {
            c.Aplicar(new Tick()).Esta(Estado.Resting).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"TICK {i + 1} em RESTING: nenhum efeito");
            Afirmar.Falso(c.Atual.RelogioAtivo, "TICK atrasado não liga o relógio");
        }
        c.Aplicar(new ExpressionChange(Expressao.Dormindo)).Esta(Estado.Resting).SemTransicao();
        Afirmar.Igual(Expressao.Dormindo, c.Retrato.Expressao);
        Afirmar.Igual(0, c.Efeitos.Count, "trocar a expressão em RESTING: nenhum efeito");
        Afirmar.Falso(c.Atual.RelogioAtivo, "a expressão não liga o relógio");
        Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, "o temporizador de acordar continua pendente");
    }

    // ---------------------------------------------------------------- J: TOPOLOGY_CHANGED sem estado visível (linha BOOTING, HIDDEN, EXITING | TOPOLOGY_CHANGED)

    // Item J. Linha: BOOTING, HIDDEN, EXITING | TOPOLOGY_CHANGED | sem troca de estado | Só atualiza a topologia em cache. BOOTING valida ao terminar de carregar.
    [Teste]
    public static void Booting_TopologyChangedEDepoisLoaded_ValidaComATopologiaDoLoaded()
    {
        Cenario c = new Cenario().Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo));
        c.Esta(Estado.Booting).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "BOOTING só guarda a topologia");
        c.Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao)).Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "chão da UmMonitor (1032), não o da BarraNoTopo (1080)");
        Afirmar.Verdadeiro(Afirmar.NaoNulo(c.Atual.Topologia).MesmaConfiguracao(TopologiasDeExemplo.UmMonitor), "a topologia em cache passa a ser a do Loaded");

        Cenario inverso = new Cenario().Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor), new Loaded(TopologiasDeExemplo.BarraNoTopo, null, Preferencias.Padrao));
        Afirmar.Igual(new PontoPx(1632, 1080), inverso.Ancora, "chão da BarraNoTopo (1080)");
    }

    // Item J. Linha: BOOTING, HIDDEN, EXITING | TOPOLOGY_CHANGED: HIDDEN por sessão, suspensão ou tela cheia só atualiza o cache; valida ao reaparecer.
    [Teste]
    public static void EscondidoPorSessaoSuspensaoOuTelaCheia_TopologyChanged_SoOCacheEValidaAoReaparecer()
    {
        (string Nome, Evento Esconder, MotivoDoOcultamento Motivo, Evento Reaparecer)[] casos =
        [
            ("sessão", new SessionLocked(), MotivoDoOcultamento.PorSessao, new SessionUnlocked()),
            ("suspensão", new Suspending(), MotivoDoOcultamento.PorSuspensao, new Resumed()),
            ("tela cheia", Ocupados(TopologiasDeExemplo.Display1), MotivoDoOcultamento.PorTelaCheia, Ocupados()),
        ];
        foreach ((string nome, Evento esconder, MotivoDoOcultamento motivo, Evento reaparecer) in casos)
        {
            foreach (Evento mostrar in new[] { reaparecer, new CmdShow() })
            {
                string contexto = $"{nome}, reaparece por {mostrar.GetType().Name}";
                Cenario c = Cenario.Parado().Aplicar(esconder).EstaEscondido(motivo);
                Posicionamento? lugar = c.Atual.Lugar;
                c.Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo)).EstaEscondido(motivo).SemTransicao();
                Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: escondido, só o cache");
                Afirmar.Igual(lugar, c.Atual.Lugar, $"{contexto}: o lugar não muda escondido");
                Afirmar.Verdadeiro(Afirmar.NaoNulo(c.Atual.Topologia).MesmaConfiguracao(TopologiasDeExemplo.BarraNoTopo), $"{contexto}: cache atualizado");

                c.Aplicar(mostrar).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
                Afirmar.Igual(new PontoPx(1632, 1080), c.Ancora, $"{contexto}: validado contra a topologia nova ao reaparecer");
            }
        }
    }

    // ---------------------------------------------------------------- K: SESSION_LOCKED e SUSPENDING (linhas qualquer, exceto EXITING | CMD_HIDE, SESSION_LOCKED e SUSPENDING)

    // Item K. Linha: qualquer, exceto EXITING | SESSION_LOCKED | HIDDEN(POR_SESSAO) | Idem. Linha: ... | SUSPENDING | HIDDEN(POR_SUSPENSAO) | Idem. "Idem" = fecha o painel, encerra captura e arraste, grava a posição (linha de CMD_HIDE). Em DRAGGING, o cursor está fora da área útil: esconder encerra o arraste onde ele está, como um cancelamento (ARCHITECTURE.md 2.7), e grava o ponto validado, não o de antes do arraste nem o do cursor.
    [Teste]
    public static void Qualquer_SessionLockedESuspending_EscondemComOsEfeitosDoCmdHide()
    {
        // Cursor em (2500,300), pegada (0,-32): âncora (2500,332), além da direita do único monitor.
        // Validada sem queda física: x preso em 1920 − 64 = 1856 e os pés no chão (1032).
        var cursorForaDaTela = new PontoPx(2500, 300);
        var arrasteValidado = new PosicaoDoPersonagem(TopologiasDeExemplo.Display1, 1856 / 1920.0, 1.0, new PontoPx(1856, 1032));
        foreach ((Evento evento, MotivoDoOcultamento motivo) in EventosDoSistemaQueEscondem())
        {
            foreach (Estado origem in AutonomosEFisicos.Concat([Estado.Pressed, Estado.Dragging, Estado.Reacting, Estado.Booting]))
            {
                string contexto = $"{origem} + {evento.GetType().Name}";
                Cenario c = Cenario.Em(origem);
                if (origem == Estado.Dragging)
                {
                    c.Aplicar(new DragMove(cursorForaDaTela));
                    Afirmar.Igual(new PontoPx(2500, 332), c.Ancora, $"{contexto}: preso ao cursor, sem validar");
                }
                PosicaoDoPersonagem? posicao = origem == Estado.Dragging ? arrasteValidado : c.Atual.Posicao;
                c.Aplicar(evento).EstaEscondido(motivo).Percorreu(origem, Estado.Hidden);
                Afirmar.Falso(c.Atual.RelogioAtivo, $"{contexto}: sem relógio escondido");
                Afirmar.Falso(c.Atual.DecisaoAgendada, $"{contexto}: sem agenda escondido");
                Afirmar.Igual(0, c.Atual.PassosRestantes, $"{contexto}: nada pendente");
                if (origem is Estado.Pressed or Estado.Dragging)
                    Afirmar.Igual(typeof(LiberarCaptura), c.Efeitos[0].GetType(), $"{contexto}: encerra a captura primeiro");
                else
                    c.SemEfeito<LiberarCaptura>();
                if (origem == Estado.Booting)
                {
                    c.SemEfeito<EsconderJanela>().SemEfeito<GravarPosicao>();
                }
                else
                {
                    c.Efeito<EsconderJanela>();
                    MesmaPosicao(Afirmar.NaoNulo(posicao), c.Efeito<GravarPosicao>().Posicao, $"{contexto}: grava a posição");
                    MesmaPosicao(Afirmar.NaoNulo(posicao), c.Atual.Posicao, $"{contexto}: a posição do personagem escondido é a gravada");
                }
                if (origem == Estado.Dragging)
                {
                    // Ao reaparecer, está onde o arraste parou, validado.
                    c.Aplicar(motivo == MotivoDoOcultamento.PorSessao ? new SessionUnlocked() : new Resumed()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
                    Afirmar.Igual(new PontoPx(1856, 1032), c.Ancora, $"{contexto}: reaparece no ponto validado do arraste");
                }
            }

            // Painel aberto (também em PRESSED, aberto antes do pressionar): esconder o fecha.
            foreach (Estado origem in AutonomosEFisicos.Concat([Estado.Pressed, Estado.Reacting]))
            {
                string contexto = $"{origem} com painel + {evento.GetType().Name}";
                Cenario c = origem == Estado.Pressed
                    ? Cenario.Parado(ComPainel).Aplicar(new EnergyPanelOpen(), new Press(Cenario.PontoOpaco))
                    : Cenario.Em(origem, ComPainel).Aplicar(new EnergyPanelOpen());
                Afirmar.Verdadeiro(c.Atual.PainelAberto, $"{contexto}: aberto antes");
                c.Aplicar(evento).EstaEscondido(motivo).Efeito<FecharPainelDeEnergia>();
                Afirmar.Falso(c.Atual.PainelAberto, $"{contexto}: fechado escondido");
            }
        }
    }

    // Item K e R2. Linhas: qualquer, exceto EXITING | CMD_HIDE, SESSION_LOCKED e SUSPENDING, a partir de HIDDEN: um motivo de precedência maior (usuário 4 > sessão 3 > suspensão 2 > tela cheia 1) substitui o atual, com uma transição HIDDEN -> HIDDEN; um menor ou igual não.
    [Teste]
    public static void Escondido_CadaMotivoContraCadaPedidoDeEsconder_SegueAPrecedencia()
    {
        (string Nome, Func<Cenario> Montar, MotivoDoOcultamento Motivo)[] escondidos =
        [
            ("usuário", () => Cenario.Parado().Aplicar(new CmdHide()), MotivoDoOcultamento.PorUsuario),
            ("sessão", () => Cenario.Parado().Aplicar(new SessionLocked()), MotivoDoOcultamento.PorSessao),
            ("suspensão", () => Cenario.Parado().Aplicar(new Suspending()), MotivoDoOcultamento.PorSuspensao),
            ("tela cheia", () => Cenario.Parado().Aplicar(Ocupados(TopologiasDeExemplo.Display1)), MotivoDoOcultamento.PorTelaCheia),
        ];
        // Resultado esperado, linha a linha: [motivo atual][pedido] -> motivo final.
        (string Atual, Evento Pedido, MotivoDoOcultamento Final)[] tabela =
        [
            ("usuário", new CmdHide(), MotivoDoOcultamento.PorUsuario),
            ("usuário", new SessionLocked(), MotivoDoOcultamento.PorUsuario),
            ("usuário", new Suspending(), MotivoDoOcultamento.PorUsuario),
            ("sessão", new CmdHide(), MotivoDoOcultamento.PorUsuario),
            ("sessão", new SessionLocked(), MotivoDoOcultamento.PorSessao),
            ("sessão", new Suspending(), MotivoDoOcultamento.PorSessao),
            ("suspensão", new CmdHide(), MotivoDoOcultamento.PorUsuario),
            ("suspensão", new SessionLocked(), MotivoDoOcultamento.PorSessao),
            ("suspensão", new Suspending(), MotivoDoOcultamento.PorSuspensao),
            ("tela cheia", new CmdHide(), MotivoDoOcultamento.PorUsuario),
            ("tela cheia", new SessionLocked(), MotivoDoOcultamento.PorSessao),
            ("tela cheia", new Suspending(), MotivoDoOcultamento.PorSuspensao),
        ];
        foreach ((string atual, Evento pedido, MotivoDoOcultamento final) in tabela)
        {
            (_, Func<Cenario> montar, MotivoDoOcultamento motivoAtual) = escondidos.Single(e => e.Nome == atual);
            string contexto = $"HIDDEN({atual}) + {pedido.GetType().Name}";
            Cenario c = montar().EstaEscondido(motivoAtual);
            c.Aplicar(pedido).EstaEscondido(final);
            if (final != motivoAtual)
                c.Percorreu(Estado.Hidden, Estado.Hidden);
            else
                c.SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: já escondido, nada de janela, relógio, agenda ou gravação");
        }
    }

    // Item K e R3. Linhas: qualquer, exceto EXITING | CMD_HIDE, SESSION_LOCKED e SUSPENDING, e HIDDEN(POR_TELA_CHEIA) | CMD_HIDE, sobre HIDDEN(POR_TELA_CHEIA): o motivo maior assume, a posição volta a ser a de antes da tela cheia, o retorno some e o personagem não reaparece.
    [Teste]
    public static void EscondidoPorTelaCheia_SubstituidoPorMotivoMaior_VoltaAPosicaoDeAntesSemReaparecer()
    {
        foreach ((Evento evento, MotivoDoOcultamento motivo) in EventosDoSistemaQueEscondem().Append(((Evento)new CmdHide(), MotivoDoOcultamento.PorUsuario)))
        {
            string contexto = evento.GetType().Name;
            // {1} leva ao DISPLAY2; {1,2} esconde. A posição é a do DISPLAY2; o retorno, a do DISPLAY1.
            Cenario c = Cenario.Parado(topologia: TopologiasDeExemplo.LadoALado)
                .Aplicar(Ocupados(TopologiasDeExemplo.Display1), Ocupados(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Display2))
                .EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
            Afirmar.Igual(TopologiasDeExemplo.Display2, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, $"{contexto}: escondido a partir do monitor temporário");
            MesmaPosicao(PosicaoInicial, c.Atual.RetornoDaTelaCheia, $"{contexto}: retorno guardado");

            c.Aplicar(evento).EstaEscondido(motivo).Percorreu(Estado.Hidden, Estado.Hidden);
            Afirmar.Nulo(c.Atual.RetornoDaTelaCheia, $"{contexto}: o retorno não sobra para o próximo episódio");
            MesmaPosicao(PosicaoInicial, c.Atual.Posicao, $"{contexto}: a posição volta a ser a de antes da tela cheia");
            Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: sem reaparecer nem gravar");
        }
    }

    // ---------------------------------------------------------------- L: reaparecer contra a topologia nova (linhas HIDDEN(POR_SESSAO) | SESSION_UNLOCKED ou CMD_SHOW e HIDDEN(POR_SUSPENSAO) | RESUMED ou CMD_SHOW)

    // Item L. Linha: HIDDEN(POR_SESSAO) | SESSION_UNLOCKED ou CMD_SHOW | SETTLING | Revalida a posição contra a topologia atual. Linha: HIDDEN(POR_SUSPENSAO) | RESUMED ou CMD_SHOW | SETTLING | Idem.
    [Teste]
    public static void EscondidoPorSessaoOuSuspensao_TopologiaNova_RevalidaAoReaparecer()
    {
        foreach ((Evento esconder, MotivoDoOcultamento motivo) in EventosDoSistemaQueEscondem())
        {
            Evento reaparecer = motivo == MotivoDoOcultamento.PorSessao ? new SessionUnlocked() : new Resumed();
            foreach (Evento mostrar in new[] { reaparecer, new CmdShow() })
            {
                string contexto = $"{esconder.GetType().Name}, depois {mostrar.GetType().Name}";

                // O monitor do personagem mudou (barra de tarefas no topo): mesma posição relativa, chão novo.
                Cenario barra = Cenario.Parado().Aplicar(esconder, new TopologyChanged(TopologiasDeExemplo.BarraNoTopo));
                barra.EstaEscondido(motivo).SemTransicao();
                barra.Aplicar(mostrar).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
                Afirmar.Igual(new PontoPx(1632, 1080), barra.Ancora, $"{contexto}: 85% da largura, no chão novo");

                // O monitor do personagem sumiu: regra de reserva (ARCHITECTURE.md 2.8), o monitor mais
                // próximo da última âncora, na mesma posição relativa. No meio de três monitores, a
                // 75%: o DISPLAY3 fica a 480 px e o DISPLAY1 (principal) a 1441 px.
                var salva = new PosicaoDoPersonagem(TopologiasDeExemplo.Display2, 0.75, 1.0, new PontoPx(3360, 1032));
                Cenario c = new Cenario().Aplicar(new Loaded(Cenario.TresEmLinha, salva, Preferencias.Padrao));
                Afirmar.Igual(new PontoPx(3360, 1032), c.Ancora, $"{contexto}: começa no DISPLAY2");
                c.Aplicar(esconder, new TopologyChanged(Cenario.TresEmLinhaSemODoMeio)).EstaEscondido(motivo).SemTransicao();
                c.Aplicar(mostrar).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
                Afirmar.Igual(TopologiasDeExemplo.Display3, c.Retrato.ChaveMonitor, $"{contexto}: monitor mais próximo, não o principal");
                Afirmar.Igual(new PontoPx(3840 + 1440, 1032), c.Ancora, $"{contexto}: 75% da área útil do DISPLAY3");
                Afirmar.Igual(TopologiasDeExemplo.Display3, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, $"{contexto}: a posição passa a ser do monitor novo");
            }
        }
    }

    // ---------------------------------------------------------------- M: gravar antes de encerrar (linha qualquer | CMD_EXIT, SESSION_ENDING)

    // Item M. Linha: qualquer | CMD_EXIT, SESSION_ENDING | EXITING | Grava configurações e encerra.
    [Teste]
    public static void Qualquer_CmdExitOuSessionEnding_GravaAPosicaoLogoAntesDeEncerrar()
    {
        foreach (Evento sair in new Evento[] { new CmdExit(), new SessionEnding() })
        {
            foreach (Estado origem in AutonomosEFisicos.Concat([Estado.Pressed, Estado.Dragging, Estado.Reacting, Estado.Hidden]))
            {
                string contexto = $"{origem} + {sair.GetType().Name}";
                Cenario c = Cenario.Em(origem);
                PosicaoDoPersonagem posicao = Afirmar.NaoNulo(c.Atual.Posicao, contexto);
                c.Aplicar(sair).Percorreu(origem, Estado.Exiting);
                string[] tipos = [.. c.Efeitos.Select(e => e.GetType().Name)];
                Afirmar.Igual("Encerrar", tipos[^1], $"{contexto}: Encerrar é o último efeito");
                Afirmar.Igual("GravarPosicao", tipos.Length >= 2 ? tipos[^2] : "", $"{contexto}: grava a posição logo antes de encerrar em [{string.Join(", ", tipos)}]");
                MesmaPosicao(posicao, c.Efeito<GravarPosicao>().Posicao, $"{contexto}: a posição do personagem");
            }

            // BOOTING não tem posição: só encerra.
            Cenario booting = new Cenario().Aplicar(sair).Percorreu(Estado.Booting, Estado.Exiting);
            Afirmar.Sequencia(["Encerrar"], booting.Efeitos.Select(e => e.GetType().Name), $"BOOTING + {sair.GetType().Name}");
        }
    }

    // Item M. Linha: qualquer | CMD_EXIT, SESSION_ENDING, a partir de DRAGGING: o arraste interrompido vale como cancelamento; grava a posição validada (presa na área útil).
    [Teste]
    public static void Dragging_CmdExitOuSessionEnding_GravaAPosicaoValidadaDoArraste()
    {
        foreach (Evento sair in new Evento[] { new CmdExit(), new SessionEnding() })
        {
            string contexto = sair.GetType().Name;
            // Cursor fora da tela: a âncora (2500,332) fica além da direita do único monitor.
            Cenario c = Cenario.Em(Estado.Dragging).Aplicar(new DragMove(new PontoPx(2500, 300)));
            Afirmar.Igual(new PontoPx(2500, 332), c.Ancora, $"{contexto}: preso ao cursor, sem validar");
            c.Aplicar(sair).Percorreu(Estado.Dragging, Estado.Exiting);
            Afirmar.Igual(typeof(LiberarCaptura), c.Efeitos[0].GetType(), $"{contexto}: solta a captura primeiro");
            // Sem queda física: x preso em 1920 − 64 = 1856 e os pés no chão (1032).
            var validada = new PosicaoDoPersonagem(TopologiasDeExemplo.Display1, 1856 / 1920.0, 1.0, new PontoPx(1856, 1032));
            MesmaPosicao(validada, c.Efeito<GravarPosicao>().Posicao, $"{contexto}: posição validada");

            // Com queda física, a validação não põe no chão: fica onde foi solto, preso na área útil.
            Cenario q = Cenario.Em(Estado.Dragging, ComQueda).Aplicar(new DragMove(new PontoPx(2500, 300)), sair);
            var noAr = new PosicaoDoPersonagem(TopologiasDeExemplo.Display1, 1856 / 1920.0, 332 / 1032.0, new PontoPx(1856, 332));
            MesmaPosicao(noAr, q.Efeito<GravarPosicao>().Posicao, $"{contexto} com queda física: posição validada");
        }
    }

    // Item M. Linha: qualquer | CMD_EXIT, SESSION_ENDING: EXITING é final; sair de novo não produz efeito.
    [Teste]
    public static void Exiting_CmdExitESessionEnding_NaoProduzemEfeitos()
    {
        foreach (Evento sair in new Evento[] { new CmdExit(), new SessionEnding() })
        {
            Cenario c = Cenario.Em(Estado.Exiting);
            Ignorado(c, sair, $"EXITING + {sair.GetType().Name}");
            c.Esta(Estado.Exiting);
        }
    }

    // ---------------------------------------------------------------- N: arraste a partir de estados em movimento (linha qualquer autônomo ou físico | PRESS sobre pixel opaco)

    // Item N. Linha: qualquer autônomo ou físico | PRESS | PRESSED | "Movimento autônomo congela no quadro atual. Vale também no meio de um pulo ou queda." Linha: PRESSED | DRAG_START | DRAGGING | Plano autônomo descartado. Linha: DRAGGING | DRAG_END | SETTLING. Em CLIMBING e HANGING havia uma decisão pendente, que o PRESS cancela; nos outros, não havia agenda a cancelar. Passos e gesto pendentes que o PRESS descarta estão em PressEArraste_NaoHerdamPassosRestantesNemGesto, a partir de LANDING, REACTING e de um gesto em IDLE, os únicos estados que os têm.
    [Teste]
    public static void EmMovimento_PressArrastaESolta_AcomodaSemRelogioNemAgendaNoArraste()
    {
        foreach (Estado origem in new[] { Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Falling })
        {
            Cenario c = Cenario.Em(origem);
            bool decidia = Maquina.DecideNoEstado(origem);
            Afirmar.Verdadeiro(c.Atual.RelogioAtivo, $"{origem}: em movimento, o relógio corre");
            Afirmar.Igual(decidia, c.Atual.DecisaoAgendada, $"{origem}: decisão pendente só em CLIMBING e HANGING");
            c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(origem, Estado.Pressed).Efeito<DesligarRelogio>();
            Afirmar.Igual(decidia, c.Tem<CancelarDecisao>(), $"{origem}: o PRESS cancela a decisão pendente");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: nada agendado com o botão pressionado");
            Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, $"{origem}: congela no lugar");
            c.Aplicar(new DragStart()).Percorreu(Estado.Pressed, Estado.Dragging);
            c.Aplicar(new DragMove(new PontoPx(700, 1000))).Esta(Estado.Dragging).SemTransicao();
            Afirmar.Sequencia(["MoverJanela"], c.Efeitos.Select(e => e.GetType().Name), $"{origem}: arrastar só move a janela");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: sem relógio no arraste");
            Afirmar.Falso(c.Atual.DecisaoAgendada, $"{origem}: sem agenda no arraste");

            c.Aplicar(new DragEnd(new PontoPx(700, 1000))).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
            Afirmar.Igual(new PontoPx(700, 1032), c.Ancora, $"{origem}: onde foi solto");
            Afirmar.Falso(c.Atual.RelogioAtivo, $"{origem}: parado, sem relógio");
            Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, $"{origem}: volta a decidir");
        }
    }

    // Item N. Linha: qualquer autônomo ou físico | PRESS sobre pixel opaco, a partir de FALLING com queda física: solto no ar, volta a cair.
    [Teste]
    public static void Falling_ArrastadoESoltoNoAr_VoltaACairComQuedaFisica()
    {
        Cenario c = Cenario.Em(Estado.Falling, ComQueda);
        c.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(new PontoPx(1000, 300)));
        c.Aplicar(new DragEnd(new PontoPx(1000, 300))).Percorreu(Estado.Dragging, Estado.Settling, Estado.Falling);
        Afirmar.Igual(new PontoPx(1000, 332), c.Ancora, "a queda começa de onde foi solto");
        Afirmar.Verdadeiro(c.Atual.RelogioAtivo, "a queda precisa do relógio");
        Afirmar.Falso(c.Atual.DecisaoAgendada, "caindo não decide");
        Afirmar.Igual(0, c.Atual.PassosRestantes);

        c.Aplicar(new DragEnd(new PontoPx(1000, 1000))).Esta(Estado.Falling).SemTransicao();
        c.Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao)).Percorreu(Estado.Falling, Estado.Landing);

        // Solto no chão, com queda física: apoio, IDLE.
        Cenario chao = Cenario.Em(Estado.Falling, ComQueda).Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragEnd(new PontoPx(1000, 1000)));
        chao.Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
    }

    // Item N. Linha: qualquer autônomo ou físico | PRESS sobre pixel opaco (e REACTING, DEC-004): PRESS descarta o resto do pouso, da reação e do gesto; o arraste não os herda.
    [Teste]
    public static void PressEArraste_NaoHerdamPassosRestantesNemGesto()
    {
        foreach (Estado origem in new[] { Estado.Landing, Estado.Reacting })
        {
            Cenario c = Cenario.Em(origem);
            Afirmar.Verdadeiro(c.Atual.PassosRestantes > 0, $"{origem}: há passos pendentes");
            c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(origem, Estado.Pressed);
            Afirmar.Igual(0, c.Atual.PassosRestantes, $"{origem}: PRESS descarta os passos pendentes");
            c.Aplicar(new DragStart(), new DragEnd(new PontoPx(1000, 1000))).Esta(Estado.Idle);
            Afirmar.Igual(0, c.Atual.PassosRestantes, $"{origem}: nada herdado depois de soltar");
            c.Passos(3).Esta(Estado.Idle, $"{origem}: TICK atrasado não retoma o pouso nem a reação");
        }

        Cenario g = Cenario.Parado(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Gesto }).Decidir();
        Afirmar.Diferente(Gesto.Nenhum, g.Retrato.Gesto, "gesto em curso");
        g.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Idle, Estado.Pressed);
        Afirmar.Igual(Gesto.Nenhum, g.Retrato.Gesto, "PRESS encerra o gesto (invariante 15)");
        Afirmar.Igual(0, g.Atual.PassosDoGesto);
        g.Aplicar(new DragStart(), new DragEnd(new PontoPx(1000, 1000))).Esta(Estado.Idle);
        Afirmar.Igual(Gesto.Nenhum, g.Retrato.Gesto, "o gesto não volta depois de soltar");
        Afirmar.Falso(g.Atual.RelogioAtivo, "sem gesto, sem relógio");
    }

    // ---------------------------------------------------------------- O: direção e ramo calmo (linhas WALKING | parede, passagem ou fim do chão; CLIMBING | topo da área útil...; HANGING | deslocamento autônomo...)

    // Item O. Linha: WALKING | parede, passagem ou fim do chão | IDLE, CLIMBING, FALLING ou WALKING: voltar a andar na parede inverte a direção; os outros destinos não viram. Os dois ramos precisam acontecer, e a inversão, a partir das duas direções.
    [Teste]
    public static void Walking_Parede_VoltarAAndarInverteADirecao()
    {
        var inversoes = new HashSet<Direcao>();
        var semVirar = new HashSet<Estado>();
        for (ulong semente = 1; semente <= 60; semente++)
        {
            Cenario c = AndandoComSemente(semente, Padrao);
            Direcao antes = c.Retrato.Direcao;
            c.Aplicar(new MovementSignal(SinalDeMovimento.Parede));
            if (c.Atual.Estado == Estado.Walking)
            {
                c.Percorreu(Estado.Walking, Estado.Walking);
                Afirmar.Diferente(antes, c.Retrato.Direcao, $"semente {semente}: volta a andar para o outro lado");
                inversoes.Add(antes);
            }
            else
            {
                Afirmar.Igual(antes, c.Retrato.Direcao, $"semente {semente}: {c.Atual.Estado} não vira");
                semVirar.Add(c.Atual.Estado);
            }
        }
        Afirmar.Igual(2, inversoes.Count, "inverteu partindo das duas direções");
        Afirmar.Sequencia([Estado.Idle, Estado.Climbing], semVirar.Order(), "parar e escalar aconteceram, sem virar");
    }

    // Item O. Linha: HANGING | deslocamento autônomo, AUTONOMY_TIMER ou passagem compatível | HANGING, CLIMBING, JUMPING ou FALLING: continuar pendurado no fim da borda inverte a direção, cada vez que acontece. O personagem chega pendurado virado para a direita; a partir da esquerda, é a volta seguinte. Os dois ramos precisam acontecer.
    [Teste]
    public static void Hanging_FimDaBorda_ContinuarPenduradoInverteADirecao()
    {
        var partidas = new Dictionary<Direcao, int>();
        var semVirar = new HashSet<Estado>();
        for (ulong semente = 1; semente <= 120; semente++)
        {
            Cenario c = EscalandoComSemente(semente, Padrao).Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior)).Esta(Estado.Hanging);
            // Fim da borda até sair de HANGING (no máximo 10 vezes).
            for (int volta = 1; volta <= 10 && c.Atual.Estado == Estado.Hanging; volta++)
            {
                Direcao antes = c.Retrato.Direcao;
                c.Aplicar(new MovementSignal(SinalDeMovimento.FimDaBorda));
                if (c.Atual.Estado == Estado.Hanging)
                {
                    c.Percorreu(Estado.Hanging, Estado.Hanging);
                    Afirmar.Diferente(antes, c.Retrato.Direcao, $"semente {semente}, volta {volta}: segue pela borda no outro sentido");
                    partidas[antes] = partidas.GetValueOrDefault(antes) + 1;
                }
                else
                {
                    Afirmar.Igual(antes, c.Retrato.Direcao, $"semente {semente}, volta {volta}: {c.Atual.Estado} não vira");
                    semVirar.Add(c.Atual.Estado);
                }
            }
        }
        string resumo = string.Join(", ", partidas.Select(p => $"a partir de {p.Key}: {p.Value}"));
        Afirmar.Verdadeiro(partidas.GetValueOrDefault(Direcao.Direita) > 0 && partidas.GetValueOrDefault(Direcao.Esquerda) > 0, $"inverteu partindo das duas direções ({resumo})");
        Afirmar.Sequencia([Estado.Climbing, Estado.Falling], semVirar.Order(), "voltar à parede e cair aconteceram, sem virar");
    }

    // Item O. Linhas: WALKING | parede...; CLIMBING | topo da área útil...; HANGING | deslocamento autônomo... com a autonomia pausada ou o painel aberto: o movimento termina no destino mais calmo (parede -> IDLE, topo -> IDLE, fim da borda -> CLIMBING), sem sortear.
    [Teste]
    public static void SinaisDeMovimento_ComAutonomiaPausadaOuPainelAberto_FicamComODestinoCalmo()
    {
        for (ulong semente = 1; semente <= 40; semente++)
        {
            foreach (bool painel in new[] { false, true })
            {
                Evento calmo = painel ? new EnergyPanelOpen() : new CmdPauseAutonomy();
                string contexto = $"semente {semente}, {(painel ? "painel aberto" : "autonomia pausada")}";

                Cenario parede = AndandoComSemente(semente, ComPainel).Aplicar(calmo);
                Aleatorio sorteio = parede.Atual.Aleatorio;
                Direcao direcao = parede.Retrato.Direcao;
                parede.Aplicar(new MovementSignal(SinalDeMovimento.Parede)).Percorreu(Estado.Walking, Estado.Idle);
                Afirmar.Igual(sorteio, parede.Atual.Aleatorio, $"{contexto}: parede sem sortear");
                Afirmar.Igual(direcao, parede.Retrato.Direcao, $"{contexto}: parado, não vira");
                Afirmar.Falso(parede.Atual.DecisaoAgendada, $"{contexto}: parado, mas sem agenda");

                Cenario topo = EscalandoComSemente(semente, ComPainel).Aplicar(calmo);
                sorteio = topo.Atual.Aleatorio;
                topo.Aplicar(new MovementSignal(SinalDeMovimento.TopoDaParede)).Percorreu(Estado.Climbing, Estado.Idle);
                Afirmar.Igual(sorteio, topo.Atual.Aleatorio, $"{contexto}: topo sem sortear");

                Cenario borda = EscalandoComSemente(semente, ComPainel).Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior), calmo);
                sorteio = borda.Atual.Aleatorio;
                borda.Aplicar(new MovementSignal(SinalDeMovimento.FimDaBorda)).Percorreu(Estado.Hanging, Estado.Climbing);
                Afirmar.Igual(sorteio, borda.Atual.Aleatorio, $"{contexto}: fim da borda sem sortear");
            }
        }
    }

    // ---------------------------------------------------------------- P: CLICK depois de mudar a topologia (R10)

    // Item P, R10. Linha: PRESSED, DRAGGING | TOPOLOGY_CHANGED | sem troca de estado | Só atualiza a topologia em cache; a validação acontece ao soltar. O CLICK valida na hora se o monitor do personagem sumiu.
    [Teste]
    public static void Pressed_MonitorRemovidoEClick_ValidaNaHoraNoMonitorMaisProximo()
    {
        var salva = new PosicaoDoPersonagem(TopologiasDeExemplo.Display2, 0.75, 1.0, new PontoPx(3360, 1032));
        Cenario c = new Cenario().Aplicar(new Loaded(Cenario.TresEmLinha, salva, Preferencias.Padrao));
        Afirmar.Igual(new PontoPx(3360, 1032), c.Ancora, "no DISPLAY2, o do meio");
        c.Aplicar(new Press(new PontoPx(3360, 1000))).Esta(Estado.Pressed);
        c.Aplicar(new TopologyChanged(Cenario.TresEmLinhaSemODoMeio)).Esta(Estado.Pressed).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "com o botão pressionado, só o cache");
        Afirmar.Igual(TopologiasDeExemplo.Display2, c.Retrato.ChaveMonitor, "ainda não validou");

        c.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual(Sinal.FoiClicado, c.Retrato.Sinal);
        // A âncora (3360,1032) fica a 480 px do DISPLAY3 e a 1441 px do DISPLAY1: vai ao DISPLAY3, na mesma posição
        // relativa, como ele iria parado (ARCHITECTURE.md 2.8; revisão do bloco P6-P9, achado 3): 3840 + 0,75 · 1920 = 5280.
        // Antes, a âncora era presa na borda dele (3904), como se o usuário a tivesse soltado ali.
        Afirmar.Igual(TopologiasDeExemplo.Display3, c.Retrato.ChaveMonitor, "validado no monitor mais próximo");
        Afirmar.Igual(new PontoPx(5280, 1032), c.Ancora, "na mesma posição relativa do DISPLAY3");
        Afirmar.Igual(TopologiasDeExemplo.Display3, c.Efeito<MoverJanela>().Destino.Monitor.Chave, "a janela vai para lá já no clique");
        Afirmar.Igual(TopologiasDeExemplo.Display3, Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, "a posição acompanha");

        c.Passos(Padrao.PassosDaReacao).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(5280, 1032), c.Ancora, "a reação termina onde foi validada");
    }

    // Item P, R10: o monitor mudou (barra de tarefas no topo) valida já no CLICK; sem mudança no monitor do personagem, a reação começa no mesmo lugar.
    [Teste]
    public static void Pressed_TopologiaMudaEClick_ValidaSoSeOMonitorDoPersonagemMudou()
    {
        Cenario mudou = Cenario.Em(Estado.Pressed).Aplicar(new TopologyChanged(TopologiasDeExemplo.BarraNoTopo));
        mudou.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual(new PontoPx(1632, 1080), mudou.Ancora, "o monitor mudou: validado no chão novo");
        mudou.Efeito<MoverJanela>();

        // Acrescentar um monitor à direita não muda o DISPLAY1.
        Cenario igual = Cenario.Em(Estado.Pressed).Aplicar(new TopologyChanged(TopologiasDeExemplo.LadoALado));
        igual.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting).SemEfeito<MoverJanela>();
        Afirmar.Igual(Cenario.AncoraInicial, igual.Ancora, "o monitor do personagem não mudou: reage no mesmo lugar");
    }

    // ---------------------------------------------------------------- auxiliares

    private static PerfilDeEnergia PerfilCurto(NivelDeEnergia nivel) => PerfilDeEnergia.Padrao(nivel) with
    {
        DecisaoMinima = TimeSpan.FromMilliseconds(200),
        DecisaoMaxima = TimeSpan.FromMilliseconds(900),
        DescansoMinimo = TimeSpan.FromMilliseconds(300),
        DescansoMaximo = TimeSpan.FromMilliseconds(900),
    };

    /// <summary>
    /// Perfis com atrasos fixos, diferentes por nível e por faixa (decisão ou descanso), todos acima
    /// do intervalo de acomodação: o atraso agendado diz qual perfil e qual faixa a agenda usou.
    /// Pesos e gestos são os do perfil padrão.
    /// </summary>
    private static PerfilDeEnergia PerfilMarcado(NivelDeEnergia nivel) => PerfilDeEnergia.Padrao(nivel) with
    {
        DecisaoMinima = MarcaDeDecisao(nivel),
        DecisaoMaxima = MarcaDeDecisao(nivel),
        DescansoMinimo = MarcaDeDescanso(nivel),
        DescansoMaximo = MarcaDeDescanso(nivel),
    };

    /// <summary>Decisão fixa de <see cref="PerfilMarcado"/>: 21 s em Baixa, 9 s em Média, 4 s em Alta.</summary>
    private static TimeSpan MarcaDeDecisao(NivelDeEnergia nivel) => TimeSpan.FromSeconds(nivel switch { NivelDeEnergia.Baixa => 21, NivelDeEnergia.Media => 9, _ => 4 });

    /// <summary>Descanso fixo de <see cref="PerfilMarcado"/>: 61 s em Baixa, 31 s em Média, 11 s em Alta.</summary>
    private static TimeSpan MarcaDeDescanso(NivelDeEnergia nivel) => TimeSpan.FromSeconds(nivel switch { NivelDeEnergia.Baixa => 61, NivelDeEnergia.Media => 31, _ => 11 });

    private static void NaFaixa((TimeSpan Minimo, TimeSpan Maximo) faixa, TimeSpan atraso, string contexto)
        => Afirmar.Verdadeiro(atraso >= faixa.Minimo && atraso <= faixa.Maximo, $"{contexto}: {atraso} entre {faixa.Minimo} e {faixa.Maximo}");

    private static (Evento Evento, MotivoDoOcultamento Motivo)[] EventosDoSistemaQueEscondem() =>
    [
        (new SessionLocked(), MotivoDoOcultamento.PorSessao),
        (new Suspending(), MotivoDoOcultamento.PorSuspensao),
    ];

    private static FullscreenTargetsChanged Ocupados(params string[] chaves) => new(new MonitoresOcupados(chaves));

    /// <summary>Aplica o evento e confere que nada mudou: sem transição, sem efeito e com o mesmo estado, fora o sinal pontual.</summary>
    private static void Ignorado(Cenario c, Evento evento, string contexto)
    {
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(evento).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, $"{contexto}: sem efeitos (houve: {string.Join(", ", c.Efeitos.Select(e => e.GetType().Name))})");
        Afirmar.Igual(antes with { Sinal = Sinal.Nenhum }, c.Atual, $"{contexto}: estado inalterado");
    }

    private static void MesmaPosicao(PosicaoDoPersonagem esperada, PosicaoDoPersonagem? obtida, string contexto)
    {
        PosicaoDoPersonagem p = Afirmar.NaoNulo(obtida, contexto);
        Afirmar.Igual(esperada.ChaveMonitor, p.ChaveMonitor, $"{contexto}: monitor");
        Afirmar.Igual(esperada.AncoraAbsoluta, p.AncoraAbsoluta, $"{contexto}: âncora");
        Afirmar.Aproximado(esperada.FracaoX, p.FracaoX, 1e-9, $"{contexto}: fração X");
        Afirmar.Aproximado(esperada.FracaoY, p.FracaoY, 1e-9, $"{contexto}: fração Y");
    }

    /// <summary>Solta o personagem com o cursor em <paramref name="solto"/> e leva ao estado pedido pela agenda, como <see cref="Cenario.Em"/>.</summary>
    private static Cenario EmDepoisDeSoltar(Estado alvo, PontoPx solto)
    {
        Cenario c = Cenario.Parado().Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragEnd(solto)).Esta(Estado.Idle);
        AcoesAutonomas acao = alvo switch
        {
            Estado.Resting => AcoesAutonomas.Descansar,
            Estado.Climbing => AcoesAutonomas.Escalar,
            _ => throw new ArgumentException($"{alvo} não é montado por este auxiliar.", nameof(alvo)),
        };
        c.AplicarCom(Padrao with { Acoes = acao }, new AutonomyTimer(c.Atual.Geracao));
        return c.Esta(alvo);
    }

    /// <summary>Passos de relógio até IDLE (no máximo 1000).</summary>
    private static int PassosAteIdle(Cenario c)
    {
        for (int i = 1; i <= 1000; i++)
        {
            if (c.Aplicar(new Tick()).Atual.Estado == Estado.Idle) return i;
        }
        Afirmar.Falhar($"não chegou a IDLE em 1000 passos ({c.Atual.Estado})");
        return -1;
    }

    /// <summary>Primeira decisão autônoma depois da carga: o destino (ou "Gesto", "Expressao") e o retrato resultante.</summary>
    private static (string Destino, string Retrato) PrimeiraDecisao(NivelDeEnergia nivel, ulong semente)
    {
        Cenario c = new Cenario(Padrao, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, new Preferencias(nivel, true))).Decidir();
        EstadoDoNucleo s = c.Atual;
        string destino = s.Estado != Estado.Idle ? s.Estado.ToString()
            : s.Gesto != Gesto.Nenhum ? "Gesto"
            : s.Expressao != Expressao.Neutro ? "Expressao"
            : "Nada";
        return (destino, s.Retrato().Descrever() + " | " + string.Join("; ", c.Transicoes));
    }

    private static Cenario AndandoComSemente(ulong semente, ConfiguracaoDoNucleo cfg)
    {
        Cenario c = new Cenario(cfg, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao));
        return c.Esta(Estado.Walking);
    }

    private static Cenario EscalandoComSemente(ulong semente, ConfiguracaoDoNucleo cfg)
    {
        Cenario c = new Cenario(cfg, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Escalar }, new AutonomyTimer(c.Atual.Geracao));
        return c.Esta(Estado.Climbing);
    }
}
