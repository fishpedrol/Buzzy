using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P7 (DEC-026 e DEC-037): a curiosidade no núcleo, num relógio virtual (<see cref="SimuladorDeTempo"/>,
/// que entrega o disparo dela e responde ao pedido do vão como o adaptador). Com o foco 30 s noutro monitor, vai ver, uma
/// vez por foco; sem caminho, olha de longe; nunca para um monitor ocupado. Com o foco no monitor dele pelo bastante tempo
/// do perfil, chega perto do trecho da janela, olha e perde o interesse do jeito da energia. Soltar, mostrar e redefinir
/// ligam a dispensa, que nada apaga antes do disparo dela; pegar na ida a encerra. Sem a capacidade, nada muda.
/// </summary>
internal static class CuriosidadeTestes
{
    private const string A = TopologiasDeExemplo.Display1, B = TopologiasDeExemplo.Display2;

    /// <summary>O aplicativo com a curiosidade, sem nenhuma ação da agenda: o que se move é só ela.</summary>
    private static ConfiguracaoDoNucleo Curiosa(AcoesAutonomas acoes = AcoesAutonomas.Nenhuma)
        => ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Acoes = acoes, Curiosidade = true, Personalidade = true };

    private static SimuladorDeTempo Sim(Topologia topologia, NivelDeEnergia energia = NivelDeEnergia.Media, ConfiguracaoDoNucleo? cfg = null, ulong semente = 5)
        => new(cfg ?? Curiosa(), semente, topologia, new Preferencias(energia, true)) { ResponderVao = _ => new VaoDaJanela(6, 10) };

    private static string Monitor(SimuladorDeTempo sim) => sim.Estado.Lugar!.Monitor.Chave;

    private static bool Regra(SimuladorDeTempo sim, string inicio) => sim.Transicoes.Any(t => t.Regra.StartsWith(inicio, StringComparison.Ordinal));

    private static void Arrastar(SimuladorDeTempo sim, PontoPx alvo)
    {
        PontoPx a = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new DragStart());
        var cursor = new PontoPx(alvo.X, alvo.Y - 30);
        sim.Aplicar(new DragMove(cursor));
        sim.Aplicar(new DragEnd(cursor));
    }

    [Teste]
    public static void Desligada_OsEventosSaoDescartados()
    {
        ConfiguracaoDoNucleo cfg = Curiosa() with { Curiosidade = false };
        var sim = Sim(TopologiasDeExemplo.LadoALado, cfg: cfg);
        EstadoDoNucleo antes = sim.Estado;
        var efeitos = new List<Efeito>();
        sim.AoResultado = (_, _, r) => efeitos.AddRange(r.Efeitos);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Aplicar(new CuriosityTimer(1));
        sim.Aplicar(new ActiveWindowSpan(1, new VaoDaJanela(0, 4)));
        Afirmar.Igual(antes, sim.Estado, "o estado não muda");
        Afirmar.Igual(0, efeitos.Count, "nenhum efeito");
        sim.Avancar(TimeSpan.FromMinutes(10));
        Afirmar.Igual((A, (string?)null, false), (Monitor(sim), sim.Estado.FocoDoPrimeiroPlano, sim.Transicoes.Any(t => t.Regra.Contains("CURIOSIDADE"))), "nada da curiosidade");
    }

    [Teste]
    public static void FocoNoutroMonitor_VaiVerAos30s_ChegaPertoEOlha()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        Afirmar.Igual((EstagioDaCuriosidade.Aguardando, (double?)28000), (sim.Estado.Curiosidade, sim.CuriosidadeVenceEmMs - sim.AgoraMs), "aguarda os 28 s que faltam (2 s de carência no adaptador)");
        sim.Avancar(TimeSpan.FromSeconds(27.9));
        Afirmar.Igual((A, Estado.Idle), (Monitor(sim), sim.Estado.Estado), "antes dos 30 s, nada");
        sim.Avancar(TimeSpan.FromSeconds(0.2));
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "no disparo, em IDLE livre, sai andando");
        Afirmar.Verdadeiro(Regra(sim, "IDLE + CURIOSIDADE: vai ver o outro monitor"), "a regra da ida");
        Afirmar.Igual(Direcao.Direita, sim.Estado.Direcao, "para o lado do foco");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
        Afirmar.Igual((B, EstagioDaCuriosidade.Olhando, true), (Monitor(sim), sim.Estado.Curiosidade, sim.Estado.IdaFeitaNesteFoco), "atravessou, chegou perto e olha");
        Afirmar.Igual(1, sim.VaosPedidos.Count, "um vão por aproximação");
        Afirmar.Igual(B, sim.VaosPedidos[0].Chave, "no monitor do foco");
        Retrato r = sim.Estado.Retrato();
        Afirmar.Igual((Estado.Idle, Expressao.Curioso, true), (r.Estado, r.Expressao, r.Olhando), "parado, curioso, olhando");
        Afirmar.Verdadeiro(r.Descrever().EndsWith(" olhando=sim", StringComparison.Ordinal), r.Descrever());
    }

    [Teste]
    public static void UmaIdaPorFoco_PegarNaIdaEncerra()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Avancar(TimeSpan.FromSeconds(28.5));
        sim.Esta(Estado.Walking, "indo ver");
        // Pego no meio da ida e solto no mesmo monitor: a ida deste foco acabou, e a dispensa começa.
        Arrastar(sim, new PontoPx(400, 1032));
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado == Estado.Idle);
        Afirmar.Igual((true, true), (sim.Estado.IdaFeitaNesteFoco, sim.Estado.DispensaAtiva), "a ida acabou, dispensado");
        sim.Avancar(TimeSpan.FromMinutes(30));
        Afirmar.Igual(A, Monitor(sim), "a mesma ida não recomeça, nem depois da dispensa");
        Afirmar.Falso(sim.Estado.DispensaAtiva, "a dispensa venceu");
        // Um foco novo é outra ida.
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar!.Monitor.Chave == B);
        Afirmar.Igual(B, Monitor(sim), "o foco novo o leva");
    }

    [Teste]
    public static void SemCaminho_OlhaDeLonge_ENaoAnda()
    {
        var sim = Sim(TopologiasDeExemplo.EmpilhadoSecundarioAcima);
        PontoPx antes = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Avancar(TimeSpan.FromSeconds(29));
        Afirmar.Verdadeiro(Regra(sim, "IDLE + CURIOSIDADE: olha de longe (sem caminho até o foco)"), "olha de longe");
        Afirmar.Igual((Expressao.Curioso, true, EstagioDaCuriosidade.Satisfeita), (sim.Estado.Expressao, sim.Estado.IdaFeitaNesteFoco, sim.Estado.Curiosidade), "curioso, a ida conta como feita");
        sim.Avancar(TimeSpan.FromHours(1));
        Afirmar.Igual((A, antes), (Monitor(sim), sim.Estado.Lugar!.Ancora), "não andou");
        Afirmar.Igual(null, sim.CuriosidadeVenceEmMs, "e não fica acordando à toa: espera um foco novo");
    }

    [Teste]
    public static void NuncaParaUmMonitorOcupado()
    {
        foreach (bool modo in new[] { true, false })
        {
            var sim = Sim(TopologiasDeExemplo.LadoALado);
            if (!modo) sim.Aplicar(new CmdSetFullscreenMode(false));
            sim.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([B])));
            sim.Aplicar(new ForegroundMonitorChanged(B));
            sim.Avancar(TimeSpan.FromMinutes(20));
            Afirmar.Igual(A, Monitor(sim), $"modo {(modo ? "ligado" : "desligado")}: não vai ao ocupado");
            Afirmar.Falso(Regra(sim, "IDLE + CURIOSIDADE: vai ver"), "nem começa a ida");
            Afirmar.Igual(((double?)null, EstagioDaCuriosidade.Satisfeita), (sim.CuriosidadeVenceEmMs, sim.Estado.Curiosidade), "e espera um foco novo, sem disparo periódico");
        }
    }

    [Teste]
    public static void FocoNoMonitorDele_ChegaPertoDepoisDoTempoDoPerfil_EPerdeOInteresseDoJeitoDaEnergia()
    {
        foreach ((NivelDeEnergia energia, double segundos) in new[] { (NivelDeEnergia.Baixa, 180.0), (NivelDeEnergia.Media, 90.0), (NivelDeEnergia.Alta, 45.0) })
        {
            var sim = Sim(TopologiasDeExemplo.UmMonitor, energia);
            sim.Aplicar(new ForegroundMonitorChanged(A));
            sim.Avancar(TimeSpan.FromSeconds(segundos - 2.5));
            Afirmar.Igual(0, sim.VaosPedidos.Count, $"{energia}: antes de {segundos} s, nada");
            sim.Avancar(TimeSpan.FromSeconds(1));
            Afirmar.Igual(1, sim.VaosPedidos.Count, $"{energia}: aos {segundos} s (menos a carência), pede o vão");
            sim.Avancar(TimeSpan.FromMinutes(1), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
            sim.Esta(Estado.Idle, $"{energia}: olhando");
            // O trecho 6 a 10 de 16: 720 a 1200 px; ele fica ao lado, a 64 + 16 px da borda, do lado mais perto: parte de
            // 1632 (a posição inicial), e a direita, 1280, fica mais perto que a esquerda, 640.
            Afirmar.Igual((1280, Direcao.Esquerda), (sim.Estado.Lugar!.Ancora.X, sim.Estado.Direcao), $"{energia}: ao lado do trecho, virado para ele");

            sim.Avancar(TimeSpan.FromMinutes(10), s => s.Curiosidade != EstagioDaCuriosidade.Olhando);
            Afirmar.Verdadeiro(Regra(sim, "IDLE + CURIOSIDADE: perdeu o interesse"), $"{energia}: perdeu o interesse");
            switch (energia)
            {
                case NivelDeEnergia.Baixa:
                    sim.Esta(Estado.Resting, "Baixa: senta");
                    break;
                case NivelDeEnergia.Media:
                    Afirmar.Igual((Gesto.Espreguicar, Expressao.Entediado), (sim.Estado.Gesto, sim.Estado.Expressao), "Média: espreguiça, entediado");
                    break;
                default:
                    Afirmar.Igual(Gesto.Brincar, sim.Estado.Gesto, "Alta: brinca");
                    break;
            }
            Afirmar.Igual(EstagioDaCuriosidade.Satisfeita, sim.Estado.Curiosidade, $"{energia}: satisfeita");
            double intervalo = PerfilDeEnergia.Padrao(energia).IntervaloEntreCuriosidades.TotalMilliseconds;
            Afirmar.Igual(intervalo, sim.CuriosidadeVenceEmMs!.Value - sim.AgoraMs, $"{energia}: espera o intervalo do perfil");
            sim.Avancar(TimeSpan.FromMilliseconds(intervalo + 5000));
            Afirmar.Igual(2, sim.VaosPedidos.Count, $"{energia}: com o foco ainda ali, chega perto de novo");
        }
    }

    [Teste]
    public static void JanelaMaximizada_NoCantoDoLadoOndeEsta()
    {
        foreach ((int x, int esperado, Direcao lado) in new[] { (300, 64, Direcao.Direita), (1500, 1856, Direcao.Esquerda) })
        {
            var sim = Sim(TopologiasDeExemplo.UmMonitor);
            sim.ResponderVao = _ => new VaoDaJanela(0, 16);
            Arrastar(sim, new PontoPx(x, 1032));
            sim.Avancar(TimeSpan.FromMinutes(9));
            sim.Aplicar(new ForegroundMonitorChanged(A));
            sim.Avancar(TimeSpan.FromMinutes(3), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
            Afirmar.Igual((esperado, lado), (sim.Estado.Lugar!.Ancora.X, sim.Estado.Direcao), $"saindo de {x}: no canto, virado para dentro");
        }
    }

    [Teste]
    public static void PontoDeOlhar_FuncaoPura()
    {
        var area = new RetanguloPx(0, 0, 1920, 1032);
        var sup = new Superficies(64, 1856, 1032, 0, false, false);
        // Trecho 6 a 10 (720 a 1200), sprite de 128, folga 16: 640 à esquerda, 1280 à direita.
        Afirmar.Igual((640.0, Direcao.Direita), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(6, 10), 100, 128, 16), "mais perto da esquerda");
        Afirmar.Igual((1280.0, Direcao.Esquerda), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(6, 10), 1700, 128, 16), "mais perto da direita");
        Afirmar.Igual((1280.0, Direcao.Esquerda), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(0, 10), 100, 128, 16), "sem lugar à esquerda, à direita");
        Afirmar.Igual((640.0, Direcao.Direita), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(6, 16), 1800, 128, 16), "sem lugar à direita, à esquerda");
        Afirmar.Igual((64.0, Direcao.Direita), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(0, 16), 900, 128, 16), "maximizada, desse lado: o canto");
        Afirmar.Igual((1856.0, Direcao.Esquerda), Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(0, 16), 1000, 128, 16), "maximizada, do outro: o outro canto");
        foreach (int i in Enumerable.Range(0, 16))
            foreach (int f in Enumerable.Range(i + 1, 16 - i))
                foreach (double x in new[] { 64.0, 960, 1856 })
                {
                    (double p, _) = Maquina.PontoDeOlhar(area, sup, new VaoDaJanela(i, f), x, 128, 16);
                    Afirmar.Verdadeiro(p >= sup.Esquerda && p <= sup.Direita, $"({i},{f}) de {x}: {p} dentro do chão");
                }
    }

    [Teste]
    public static void SemVao_OuSemResposta_TerminaOEpisodio()
    {
        var sim = Sim(TopologiasDeExemplo.UmMonitor);
        sim.ResponderVao = _ => null;
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Avancar(TimeSpan.FromSeconds(89));
        Afirmar.Igual((1, EstagioDaCuriosidade.Satisfeita), (sim.VaosPedidos.Count, sim.Estado.Curiosidade), "vão nulo: satisfeita, sem andar");
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parado");

        var mudo = Sim(TopologiasDeExemplo.UmMonitor);
        mudo.ResponderAoVao = false;
        mudo.Aplicar(new ForegroundMonitorChanged(A));
        mudo.Avancar(TimeSpan.FromSeconds(88.5));
        Afirmar.Igual(EstagioDaCuriosidade.PedindoVao, mudo.Estado.Curiosidade, "esperando o vão");
        mudo.Avancar(TimeSpan.FromSeconds(2.1));
        Afirmar.Igual(EstagioDaCuriosidade.Satisfeita, mudo.Estado.Curiosidade, "a guarda de 2 s venceu");
        // Uma resposta atrasada, da geração velha, é ignorada.
        mudo.Aplicar(new ActiveWindowSpan(mudo.VaosPedidos[0].Geracao, new VaoDaJanela(6, 10)));
        Afirmar.Igual((EstagioDaCuriosidade.Satisfeita, Estado.Idle), (mudo.Estado.Curiosidade, mudo.Estado.Estado), "a resposta atrasada não o move");
    }

    // A dispensa (DEC-037, item 6): soltar a liga; troca de foco, pausa, sessão e ocultação não a apagam; a curiosidade não o
    // move até o disparo dela.
    [Teste]
    public static void Dispensa_NadaAApagaAntesDoDisparo()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Avancar(TimeSpan.FromSeconds(10));
        Arrastar(sim, new PontoPx(500, 1032));
        Afirmar.Verdadeiro(sim.Estado.DispensaAtiva, "soltar dispensa");
        Afirmar.Verdadeiro(Regra(sim, "CURIOSIDADE: dispensada (DRAG_END)"), "a regra");
        double vence = sim.CuriosidadeVenceEmMs!.Value;
        Afirmar.Igual(PerfilDeEnergia.Media.EsperaDepoisDoUsuario.TotalMilliseconds, vence - sim.AgoraMs, "a espera do perfil");
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Aplicar(new CmdPauseAutonomy());
        sim.Aplicar(new CmdResumeAutonomy());
        sim.Aplicar(new SessionLocked());
        sim.Aplicar(new SessionUnlocked());
        Afirmar.Igual((true, vence), (sim.Estado.DispensaAtiva, sim.CuriosidadeVenceEmMs!.Value), "nada apagou nem adiou a dispensa");
        sim.Avancar(TimeSpan.FromMilliseconds(vence - sim.AgoraMs - 100));
        Afirmar.Igual((A, true), (Monitor(sim), sim.Estado.DispensaAtiva), "até o disparo, não se move");
        sim.Avancar(TimeSpan.FromSeconds(1));
        Afirmar.Falso(sim.Estado.DispensaAtiva, "a dispensa venceu");
        Afirmar.Igual(EstagioDaCuriosidade.Aguardando, sim.Estado.Curiosidade, "e a contagem recomeça");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar!.Monitor.Chave == B);
        Afirmar.Igual(B, Monitor(sim), "o foco que mudou durante a dispensa é outro: ele vai ver");

        foreach (Evento comando in new Evento[] { new CmdShow(), new CmdResetPosition() })
        {
            var outro = Sim(TopologiasDeExemplo.LadoALado);
            outro.Aplicar(new ForegroundMonitorChanged(B));
            outro.Aplicar(comando);
            Afirmar.Verdadeiro(outro.Estado.DispensaAtiva, $"{comando.GetType().Name} dispensa");
            outro.Avancar(TimeSpan.FromMinutes(7.9));
            Afirmar.Igual(A, Monitor(outro), $"{comando.GetType().Name}: parado durante a dispensa");
        }
    }

    // Prioridade do usuário: pausado, escondido pela bandeja, com o painel aberto ou com o menu, a curiosidade espera.
    [Teste]
    public static void SoComPodeSerCurioso()
    {
        ConfiguracaoDoNucleo comPainel = Curiosa() with { PainelDeEnergiaDisponivel = true };
        foreach ((string caso, Evento[] antes) in new (string, Evento[])[]
        {
            ("pausado", [new CmdPauseAutonomy()]),
            ("escondido", [new CmdHide()]),
            ("painel", [new EnergyPanelOpen()]),
        })
        {
            var sim = Sim(TopologiasDeExemplo.LadoALado, cfg: comPainel);
            foreach (Evento e in antes) sim.Aplicar(e);
            if (caso == "painel") Afirmar.Verdadeiro(sim.Estado.PainelAberto, "o painel abriu");
            sim.Aplicar(new ForegroundMonitorChanged(B));
            sim.Avancar(TimeSpan.FromMinutes(5));
            Afirmar.Igual(A, Monitor(sim), $"{caso}: não vai");
            Afirmar.Igual(EstagioDaCuriosidade.IrVer, sim.Estado.Curiosidade, $"{caso}: fica maduro, esperando");
        }
    }

    // O clique enquanto olha é um flagra (DEC-037, item 8), e ele volta a olhar.
    [Teste]
    public static void Olhando_CliqueEFlagra_EVoltaAOlhar()
    {
        var sim = Sim(TopologiasDeExemplo.UmMonitor);
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Avancar(TimeSpan.FromMinutes(3), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
        PontoPx a = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new Click());
        Afirmar.Igual((Estado.Reacting, VarianteDaReacao.Flagra, Expressao.Surpreso), (sim.Estado.Estado, sim.Estado.Reacao, sim.Estado.Expressao), "flagra");
        sim.Avancar(TimeSpan.FromSeconds(3), s => s.Estado == Estado.Idle);
        Retrato r = sim.Estado.Retrato();
        Afirmar.Igual((EstagioDaCuriosidade.Olhando, true, Expressao.Curioso, a), (sim.Estado.Curiosidade, r.Olhando, r.Expressao, sim.Estado.Lugar!.Ancora), "volta a olhar, no mesmo lugar");
    }

    // Uma troca de foco no meio do episódio não o para: o episódio termina, e a decisão é revista com o foco novo.
    [Teste]
    public static void FocoNovoNoEpisodio_RevistoNoFim()
    {
        var sim = Sim(new Topologia([.. TopologiasDeExemplo.LadoALado.Monitores]));
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Avancar(TimeSpan.FromMinutes(3), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        Afirmar.Igual(EstagioDaCuriosidade.Olhando, sim.Estado.Curiosidade, "continua olhando");
        sim.Avancar(TimeSpan.FromMinutes(10), s => s.Curiosidade != EstagioDaCuriosidade.Olhando);
        Afirmar.Igual(EstagioDaCuriosidade.Aguardando, sim.Estado.Curiosidade, "no fim, aguarda o foco novo, não o intervalo");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar!.Monitor.Chave == B);
        Afirmar.Igual(B, Monitor(sim), "e vai ver");
    }

    // Nada guarda o vão, um retângulo ou um instante no estado (DEC-037, item 2; invariante 33).
    [Teste]
    public static void Estado_SemVaoRetanguloNemInstante()
    {
        foreach (System.Reflection.PropertyInfo p in typeof(EstadoDoNucleo).GetProperties())
        {
            Type t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            Afirmar.Falso(t == typeof(VaoDaJanela) || t == typeof(DateTime) || t == typeof(DateTimeOffset), $"{p.Name}: {t.Name}");
        }
    }

    // O mesmo foco de novo não recomeça a contagem; um disparo de outra geração é ignorado.
    [Teste]
    public static void FocoRepetido_EDisparoVelho_NaoMudamNada()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        double vence = sim.CuriosidadeVenceEmMs!.Value;
        long geracao = sim.Estado.GeracaoDaCuriosidade;
        sim.Avancar(TimeSpan.FromSeconds(20));
        sim.Aplicar(new ForegroundMonitorChanged(B));
        Afirmar.Igual((vence, geracao), (sim.CuriosidadeVenceEmMs!.Value, sim.Estado.GeracaoDaCuriosidade), "o mesmo foco não recomeça");
        EstadoDoNucleo antes = sim.Estado;
        sim.Aplicar(new CuriosityTimer(geracao - 1));
        Afirmar.Igual(antes, sim.Estado, "o disparo velho é ignorado");
        sim.Avancar(TimeSpan.FromSeconds(8.5));
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "aos 28 s do primeiro, vai");
    }

    // O monitor dele ocupado (com o modo de tela cheia desligado, ele fica lá): a curiosidade espera, mesmo madura.
    [Teste]
    public static void MonitorDeleOcupado_Espera()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado, cfg: Curiosa(AcoesAutonomas.Gesto));
        sim.Aplicar(new CmdSetFullscreenMode(false));
        sim.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([A])));
        sim.Aplicar(new ForegroundMonitorChanged(B));
        sim.Avancar(TimeSpan.FromMinutes(10));
        Afirmar.Igual((A, EstagioDaCuriosidade.IrVer), (Monitor(sim), sim.Estado.Curiosidade), "madura, esperando");
        sim.Aplicar(new FullscreenTargetsChanged(MonitoresOcupados.Nenhum));
        sim.Avancar(TimeSpan.FromMinutes(5), s => s.Lugar!.Monitor.Chave == B);
        Afirmar.Igual(B, Monitor(sim), "livre, vai na próxima decisão");
    }

    // Sem vizinho direto (três em linha), vai pelo lado que aproxima e replaneja no monitor do meio.
    [Teste]
    public static void TresEmLinha_PeloLadoQueAproxima_ReplanejaNoMeio()
    {
        var sim = Sim(Cenario.TresEmLinha);
        sim.Aplicar(new ForegroundMonitorChanged(TopologiasDeExemplo.Display3));
        sim.Avancar(TimeSpan.FromMinutes(5), s => s.Curiosidade == EstagioDaCuriosidade.Olhando);
        Afirmar.Igual((TopologiasDeExemplo.Display3, EstagioDaCuriosidade.Olhando), (Monitor(sim), sim.Estado.Curiosidade), "chegou ao terceiro e olha");
        Afirmar.Verdadeiro(Regra(sim, "CURIOSIDADE: no meio do caminho (segue)"), "replanejou no do meio");
    }

    // As rodadas acabam: com só "ficar olhando" nos pesos e duas rodadas, perde o interesse na segunda decisão.
    [Teste]
    public static void Rodadas_Acabam()
    {
        ConfiguracaoDoNucleo cfg = Curiosa() with { Perfil = n => PerfilDeEnergia.Padrao(n) with { PesosOlhando = [1, 0, 0, 0], RodadasOlhandoMinimo = 2, RodadasOlhandoMaximo = 2 } };
        var sim = Sim(TopologiasDeExemplo.UmMonitor, cfg: cfg);
        sim.Aplicar(new ForegroundMonitorChanged(A));
        sim.Avancar(TimeSpan.FromMinutes(10), s => s.Curiosidade == EstagioDaCuriosidade.Satisfeita);
        Afirmar.Igual(1, sim.Transicoes.Count(t => t.Regra == "IDLE + CURIOSIDADE: continua olhando"), "uma rodada olhando");
        Afirmar.Verdadeiro(Regra(sim, "IDLE + CURIOSIDADE: perdeu o interesse"), "e perdeu o interesse na segunda");
    }

    // Clicar ou redefinir a posição no meio da ida a encerra: a mesma ida não recomeça.
    [Teste]
    public static void CliqueOuRedefinirNaIda_Encerram()
    {
        foreach (string caso in new[] { "clique", "redefinir" })
        {
            var sim = Sim(TopologiasDeExemplo.LadoALado);
            sim.Aplicar(new ForegroundMonitorChanged(B));
            sim.Avancar(TimeSpan.FromSeconds(28.5));
            sim.Esta(Estado.Walking, $"{caso}: indo ver");
            if (caso == "clique")
            {
                PontoPx a = sim.Estado.Lugar!.Ancora;
                sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
                sim.Aplicar(new Click());
            }
            else
            {
                sim.Aplicar(new CmdResetPosition());
            }
            Afirmar.Verdadeiro(sim.Estado.IdaFeitaNesteFoco, $"{caso}: a ida acabou");
            sim.Avancar(TimeSpan.FromHours(1));
            Afirmar.Igual(A, Monitor(sim), $"{caso}: não recomeça");
        }
    }

    // Saindo, o disparo pendente é cancelado.
    [Teste]
    public static void Saindo_CancelaODisparo()
    {
        var sim = Sim(TopologiasDeExemplo.LadoALado);
        sim.Aplicar(new ForegroundMonitorChanged(B));
        var efeitos = new List<Efeito>();
        sim.AoResultado = (_, _, r) => efeitos.AddRange(r.Efeitos);
        sim.Aplicar(new CmdExit());
        Afirmar.Igual((1, false, (double?)null), (efeitos.OfType<CancelarCuriosidade>().Count(), sim.Estado.CuriosidadeAgendada, sim.CuriosidadeVenceEmMs), "cancelado");
    }
}
