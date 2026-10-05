using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// TOPOLOGY_CHANGED com o app aberto (DEC-030, passo P8 da Fase 5), pelo que aconteceu com o monitor do personagem:
/// <list type="bullet">
/// <item>A e T, a geometria dele não mudou: só outros monitores mudaram, ele só foi transladado no desktop virtual (troca de
/// principal, rearranjo) ou só a chave mudou. O estado continua, com a âncora, a posição fina e a janela transladadas
/// juntas; o relógio e a agenda não mudam (invariante 19). USING é tratado como REACTING: continua.</item>
/// <item>B, a geometria mudou (resolução, escala, orientação ou área útil): SETTLING na mesma posição relativa, com o texto
/// de sempre (referência 05).</item>
/// <item>C, ele sumiu: SETTLING no sobrevivente mais próximo do pixel dos pés medido nas coordenadas antigas.</item>
/// </list>
/// As posições guardadas (a do personagem e o retorno da tela cheia) acompanham a topologia em qualquer estado, sem mover a
/// janela, e o clique depois de o monitor mudar com o botão pressionado valida a partir delas. Os itens do tamagotchi seguem
/// a mesma regra. As âncoras esperadas são calculadas à mão.
/// </summary>
internal static class MudancaDeTopologiaTestes
{
    private static readonly ConfiguracaoDoNucleo Padrao = new();

    private static readonly ConfiguracaoDoNucleo DoApp = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    private static readonly Estado[] QueRevalidam =
        [Estado.Idle, Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Jumping, Estado.Falling, Estado.Landing, Estado.Resting, Estado.Reacting, Estado.Using];

    // ---------------------------------------------------------------- classes A e T: continua

    [Teste]
    public static void OutroMonitorMuda_NenhumEstadoQueRevalidaEhInterrompido()
    {
        // UmMonitor -> LadoALado: o DISPLAY1 fica igual, só aparece um monitor à direita.
        foreach (Estado origem in QueRevalidam)
        {
            Cenario c = Cenario.Em(origem);
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(new TopologyChanged(LadoALado)).Percorreu(origem, origem);
            Afirmar.Igual("TOPOLOGY_CHANGED: o monitor do personagem não mudou", c.Transicoes[0].Regra, $"{origem}: regra");
            Afirmar.Igual(0, c.Efeitos.Count, $"{origem}: nenhum efeito, nem de janela, relógio ou agenda ({string.Join(", ", c.Efeitos.Select(e => e.GetType().Name))})");
            Afirmar.Igual(antes.Lugar!.Ancora, c.Ancora, $"{origem}: no mesmo lugar");
            Afirmar.Igual(Monitor(LadoALado, Display1), c.Atual.Lugar!.Monitor, $"{origem}: o monitor da topologia nova");
            Afirmar.Igual((antes.Geracao, antes.DecisaoAgendada, antes.RelogioAtivo, antes.PassosRestantes, antes.Uso),
                (c.Atual.Geracao, c.Atual.DecisaoAgendada, c.Atual.RelogioAtivo, c.Atual.PassosRestantes, c.Atual.Uso), $"{origem}: a agenda, o relógio, a reação e o uso seguem");
        }

        // A reação e o uso continuam até o fim deles.
        Cenario reagindo = Cenario.Em(Estado.Reacting).Aplicar(new TopologyChanged(LadoALado));
        reagindo.Passos(Padrao.PassosDaReacao - 1).Esta(Estado.Reacting, "a reação continua");
        reagindo.Passos(1).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle);
        Cenario usando = Cenario.Em(Estado.Using).Aplicar(new TopologyChanged(LadoALado));
        int restantes = usando.Atual.PassosRestantes;
        usando.Passos(restantes - 1).Esta(Estado.Using, "o uso continua (USING é tratado como REACTING)");
        usando.Passos(1).Percorreu(Estado.Using, Estado.Settling, Estado.Idle);
    }

    [Teste]
    public static void TrocaDePrincipal_TransladaSemInterromper()
    {
        // R15a: S2, andando no DISPLAY2 a 25%, (-1440,1032). O DISPLAY2 vira principal e vai para (0,0): (+1920, 0).
        Cenario c = Andando(NoMonitor(SecundarioAEsquerda, Display2, 0.25));
        double x = c.Atual.Movimento.X;
        c.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2))).Percorreu(Estado.Walking, Estado.Walking);
        Afirmar.Igual("TOPOLOGY_CHANGED: o monitor do personagem foi transladado (1920,0)", c.Transicoes[0].Regra, "regra");
        Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, "a âncora anda junto");
        MoverJanela mover = c.Efeito<MoverJanela>();
        Afirmar.Igual((Display2, Ret(416, 904, 544, 1032)), (mover.Destino.Monitor.Chave, mover.Destino.Retangulo), "a janela vai para as coordenadas novas");
        Afirmar.Igual(x + 1920, c.Atual.Movimento.X, "a posição fina também");
        PosicaoDoPersonagem p = Afirmar.NaoNulo(c.Atual.Posicao);
        Afirmar.Igual((Display2, new PontoPx(480, 1032), (RetanguloPx?)Ret(0, 0, 1920, 1080)), (p.ChaveMonitor, p.AncoraAbsoluta, p.TelaDoMonitor), "a posição descreve o lugar novo");
        Afirmar.Aproximado(0.25, p.FracaoX, 1e-12, "a mesma fração");
        c.SemEfeito<AgendarDecisao>().SemEfeito<CancelarDecisao>();

        // R15e: S3 (secundário acima, y negativo), andando no DISPLAY1 a 85%. O DISPLAY2 vira principal: (+320, +1440).
        Cenario s3 = Andando(NoMonitor(EmpilhadoSecundarioAcima, Display1, 0.85));
        s3.Aplicar(new TopologyChanged(Rebaseada(EmpilhadoSecundarioAcima, Display2))).Percorreu(Estado.Walking, Estado.Walking);
        Afirmar.Igual("TOPOLOGY_CHANGED: o monitor do personagem foi transladado (320,1440)", s3.Transicoes[0].Regra, "S3: regra");
        Afirmar.Igual(new PontoPx(1952, 2472), s3.Ancora, "S3: (1632,1032) + (320,1440)");
    }

    [Teste]
    public static void ChaveNovaComAMesmaTela_ContinuaComAChaveNova()
    {
        // A consulta de vídeo voltou e as chaves passaram de gdi: a mon:, com as mesmas telas: é o mesmo monitor.
        Cenario c = Andando(NoMonitor(SecundarioAEsquerda, Display2, 0.25));
        c.Aplicar(new TopologyChanged(ComChavesRenomeadas(SecundarioAEsquerda, Renomear))).Percorreu(Estado.Walking, Estado.Walking);
        Afirmar.Igual("TOPOLOGY_CHANGED: o monitor do personagem mudou de chave", c.Transicoes[0].Regra, "regra");
        Afirmar.Igual((Renomear(Display2), new PontoPx(-1440, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "no mesmo lugar, com a chave nova");
        Afirmar.Igual(Renomear(Display2), Afirmar.NaoNulo(c.Atual.Posicao).ChaveMonitor, "a posição também");
        Afirmar.Igual(c.Atual.Lugar, c.Efeito<MoverJanela>().Destino, "a janela recebe o lugar com o monitor novo, no mesmo retângulo");
    }

    [Teste]
    public static void TrocaDePrincipalNoMeioDaCaminhada_TrajetoriaIgualATransladada()
    {
        // Duas simulações com a mesma semente; numa, o principal troca no meio da caminhada. Daí em diante, os estados são
        // os mesmos e as âncoras diferem exatamente pela translação, inclusive nas decisões e nas paredes seguintes.
        var d = new PontoPx(1920, 0);
        for (ulong semente = 1; semente < 60; semente++)
        {
            var normal = new SimuladorDeTempo(MovimentoTestes.Fase4(), semente, SecundarioAEsquerda);
            var trocada = new SimuladorDeTempo(MovimentoTestes.Fase4(), semente, SecundarioAEsquerda);
            normal.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Walking);
            trocada.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Walking);
            if (normal.Estado.Estado != Estado.Walking) continue;
            normal.Passos(20);
            trocada.Passos(20);
            Afirmar.Igual(normal.Estado.Lugar!.Ancora, trocada.Estado.Lugar!.Ancora, $"semente {semente}: iguais antes da troca");

            trocada.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2)));
            trocada.Esta(Estado.Walking, $"semente {semente}: continua andando");
            Afirmar.Igual(Mais(normal.Estado.Lugar!.Ancora, d), trocada.Estado.Lugar!.Ancora, $"semente {semente}: transladado");

            var esperado = new List<string>();
            var obtido = new List<string>();
            normal.AoAplicar = (_, e, s) => esperado.Add($"{e.GetType().Name} {s.Estado} {Ancora(s, new PontoPx(0, 0))} {s.Direcao}");
            trocada.AoAplicar = (_, e, s) => obtido.Add($"{e.GetType().Name} {s.Estado} {Ancora(s, d)} {s.Direcao}");
            normal.Avancar(TimeSpan.FromMinutes(3));
            trocada.Avancar(TimeSpan.FromMinutes(3));
            Afirmar.Sequencia(esperado, obtido, $"semente {semente}: a mesma trajetória, transladada");
            string[] estados = [.. esperado.Select(l => l.Split(' ')[1]).Distinct()];
            Console.WriteLine($"         semente {semente}: {esperado.Count} eventos iguais depois da troca, nos estados {string.Join(", ", estados)}");
            Afirmar.Verdadeiro(esperado.Count > 500 && estados.Length >= 3, $"semente {semente}: {esperado.Count} eventos, {estados.Length} estados");
            return;
        }
        Afirmar.Falhar("nenhuma semente andou em 2 minutos");
    }

    // ---------------------------------------------------------------- classe B: a geometria mudou

    [Teste]
    public static void MonitorDoPersonagemMudouDeGeometria_SettlingNaMesmaPosicaoRelativa()
    {
        // A 85% e no chão de UmMonitor, (1632,1032); parado e andando.
        MonitorDoDesktop m = Monitor(UmMonitor, Display1);
        (string Caso, Topologia Nova, PontoPx Ancora, int Lado)[] casos =
        [
            ("barra no topo", BarraNoTopo, new PontoPx(1632, 1080), 128),
            ("barra à esquerda: 62 + 0,85 · 1858", BarraAEsquerda, new PontoPx(1641, 1080), 128),
            ("barra à direita: 0,85 · 1858", BarraADireita, new PontoPx(1579, 1080), 128),
            ("barra oculta", BarraOculta, new PontoPx(1632, 1080), 128),
            ("2560x1440: 0,85 · 2560", new Topologia([m with { Tela = Ret(0, 0, 2560, 1440), AreaUtil = Ret(0, 0, 2560, 1392) }]), new PontoPx(2176, 1392), 128),
            ("144 DPI (R15h): 192 px", new Topologia([m with { Dpi = 144, AreaUtil = Ret(0, 0, 1920, 1008) }]), new PontoPx(1632, 1008), 192),
            ("girado: 0,85 · 1080", new Topologia([m with { Tela = Ret(0, 0, 1080, 1920), AreaUtil = Ret(0, 0, 1080, 1872) }]), new PontoPx(918, 1872), 128),
        ];
        foreach ((string caso, Topologia nova, PontoPx ancora, int lado) in casos)
        {
            foreach (Estado origem in new[] { Estado.Idle, Estado.Walking })
            {
                Cenario c = Cenario.Em(origem);
                c.Aplicar(new TopologyChanged(nova)).Percorreu(origem, Estado.Settling, Estado.Idle);
                Afirmar.Igual("TOPOLOGY_CHANGED", c.Transicoes[0].Regra, $"{caso}, {origem}: o texto de sempre (referência 05)");
                Afirmar.Igual(ancora, c.Ancora, $"{caso}, {origem}: âncora");
                Afirmar.Igual(new TamanhoPx(lado, lado), c.Retrato.Tamanho, $"{caso}, {origem}: tamanho aparente de 128 DIP");
            }
        }
    }

    // ---------------------------------------------------------------- classe C: o monitor sumiu

    [Teste]
    public static void Desconectado_VaiAoSobreviventeMedidoNasCoordenadasAntigas()
    {
        const string Desconectado = "TOPOLOGY_CHANGED: o monitor do personagem foi desconectado";

        // R15b: S2, no principal a 85%. Ele é desconectado e o DISPLAY2 assume em (0,0): a 85% dele.
        Cenario b = Cenario.Parado(topologia: SecundarioAEsquerda);
        b.Aplicar(new TopologyChanged(SemOPrincipal(SecundarioAEsquerda, Display1, Display2))).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(Desconectado, b.Transicoes[0].Regra, "R15b: regra");
        Afirmar.Igual((Display2, new PontoPx(1632, 1032)), (b.Retrato.ChaveMonitor, b.Ancora), "R15b: no DISPLAY2, a 85%");
        Afirmar.Igual(Display2, Afirmar.NaoNulo(b.Atual.Posicao).ChaveMonitor, "R15b: a posição passa a ser dele e não volta sozinha");

        // R15c: [2][1*][3], no 1 a 85%. Promovendo o 2, o sobrevivente medido nas coordenadas antigas é o 3, a 288 px (o 2
        // fica a 1633 px): 85% do 3, que foi para 3840. Promovendo o 3, a 85% dele em (0,0).
        foreach ((string promovido, PontoPx esperada) in new[] { (Display2, new PontoPx(5472, 1032)), (Display3, new PontoPx(1632, 1032)) })
        {
            Cenario c = Cenario.Parado(topologia: TresEmLinhaComPrincipalNoMeio);
            c.Aplicar(new TopologyChanged(SemOPrincipal(TresEmLinhaComPrincipalNoMeio, Display1, promovido))).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
            Afirmar.Igual(Desconectado, c.Transicoes[0].Regra, $"R15c, promove {promovido}: regra");
            Afirmar.Igual((Display3, esperada), (c.Retrato.ChaveMonitor, c.Ancora), $"R15c, promove {promovido}: no 3");
        }

        // R15d: S3, pendurado no cipó do DISPLAY2, que está todo em y negativo, em (1000,-1312): fx = 1320/2560, fy = 128/1392.
        // O DISPLAY2 sai: no DISPLAY1, x = 0,515625 · 1920 = 990 e y = 95, preso a 128; perto da borda de cima, agarra o cipó
        // de novo (DEC-024), sem ter sido o usuário que o pôs ali.
        Topologia s3 = EmpilhadoSecundarioAcima;
        var sim = new SimuladorDeTempo(DoApp, 3, s3, posicaoSalva: DescreverNo(s3, Display2, 1320 / 2560.0, 128 / 1392.0));
        sim.Esta(Estado.Hanging, "R15d: carregado no cipó");
        Afirmar.Igual(new PontoPx(1000, -1312), sim.Estado.Lugar!.Ancora, "R15d: no cipó do DISPLAY2");
        int transicoes = sim.Transicoes.Count;
        sim.Aplicar(new TopologyChanged(SemMonitor(s3, Display2)));
        Afirmar.Sequencia(["Hanging->Settling", "Settling->Hanging"], sim.Transicoes.Skip(transicoes).Select(t => $"{t.De}->{t.Para}"), "R15d: transições");
        Afirmar.Igual(Desconectado, sim.Transicoes[transicoes].Regra, "R15d: regra");
        Afirmar.Igual((Display1, new PontoPx(990, 128)), (sim.Estado.Lugar!.Monitor.Chave, sim.Estado.Lugar.Ancora), "R15d: no cipó do DISPLAY1");
        Afirmar.Falso(sim.Estado.PresoPeloUsuario, "R15d: agarrado, não preso");
    }

    // ---------------------------------------------------------------- posições guardadas, em qualquer estado

    [Teste]
    public static void Escondido_APosicaoAcompanhaSemMoverAJanela()
    {
        // D11: escondido no DISPLAY1 de S2, o principal troca. A janela não se move; a posição vai a (3552,1032), e ele
        // reaparece lá, no mesmo monitor físico.
        Cenario c = Cenario.Parado(topologia: SecundarioAEsquerda).Aplicar(new CmdHide());
        c.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2))).EstaEscondido(MotivoDoOcultamento.PorUsuario).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "escondido, nenhum efeito");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "o lugar da janela não muda");
        PosicaoDoPersonagem p = Afirmar.NaoNulo(c.Atual.Posicao);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032), (RetanguloPx?)Ret(1920, 0, 3840, 1080)), (p.ChaveMonitor, p.AncoraAbsoluta, p.TelaDoMonitor), "a posição acompanha");
        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "reaparece no DISPLAY1, nas coordenadas novas");
    }

    [Teste]
    public static void Escondido_MonitorSaiEVoltaAntesDeMostrar_ReapareceNele()
    {
        // Escondido no DISPLAY2 de S2, a 25%. O DISPLAY2 sai e volta antes de ele reaparecer: ele continua ligado à chave.
        Cenario c = NoMonitor(SecundarioAEsquerda, Display2, 0.25).Aplicar(new CmdHide());
        c.Aplicar(new TopologyChanged(SemMonitor(SecundarioAEsquerda, Display2))).SemTransicao();
        PosicaoDoPersonagem semEle = Afirmar.NaoNulo(c.Atual.Posicao);
        Afirmar.Igual((Display2, new PontoPx(-1440, 1032), (RetanguloPx?)Ret(-1920, 0, 0, 1080)), (semEle.ChaveMonitor, semEle.AncoraAbsoluta, semEle.TelaDoMonitor), "sem ele, a chave, a âncora e a tela ficam");
        c.Aplicar(new TopologyChanged(SecundarioAEsquerda)).SemTransicao();
        c.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display2, new PontoPx(-1440, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "reaparece no DISPLAY2, que voltou");

        // Sem ele voltar, reaparece no mais próximo, na mesma posição relativa.
        Cenario sem = NoMonitor(SecundarioAEsquerda, Display2, 0.25).Aplicar(new CmdHide(), new TopologyChanged(SemMonitor(SecundarioAEsquerda, Display2)));
        sem.Aplicar(new CmdShow()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display1, new PontoPx(480, 1032)), (sem.Retrato.ChaveMonitor, sem.Ancora), "no DISPLAY1, a 25%");
    }

    [Teste]
    public static void EscondidoPelaSessao_PrincipalDesconectado_DesbloqueiaNoSobreviventeCerto()
    {
        // S12 no núcleo: bloqueado no principal de [2][1*][3]; o 1 sai e o 2 assume (R15c); ao desbloquear, o 3 a 85%.
        Cenario c = Cenario.Parado(topologia: TresEmLinhaComPrincipalNoMeio).Aplicar(new SessionLocked());
        c.Aplicar(new TopologyChanged(SemOPrincipal(TresEmLinhaComPrincipalNoMeio, Display1, Display2))).EstaEscondido(MotivoDoOcultamento.PorSessao).SemTransicao();
        c.Aplicar(new SessionUnlocked()).Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display3, new PontoPx(5472, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "no 3, nas coordenadas novas");
    }

    [Teste]
    public static void RetornoDaTelaCheia_AcompanhaATrocaDePrincipal()
    {
        // S2: a tela cheia no DISPLAY1 o leva ao DISPLAY2 a 85%, (-288,1032), guardando o retorno (1632,1032). O principal
        // troca: ele, no DISPLAY2, só foi transladado e continua; o retorno vai a (3552,1032). No fim da tela cheia, volta lá.
        Cenario c = Cenario.Parado(topologia: SecundarioAEsquerda);
        c.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([Display1]))).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual(new PontoPx(-288, 1032), c.Ancora, "transferido para o DISPLAY2");
        c.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2))).Percorreu(Estado.Idle, Estado.Idle);
        Afirmar.Igual(new PontoPx(1632, 1032), c.Ancora, "transladado com o DISPLAY2");
        PosicaoDoPersonagem retorno = Afirmar.NaoNulo(c.Atual.RetornoDaTelaCheia);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032), (RetanguloPx?)Ret(1920, 0, 3840, 1080)), (retorno.ChaveMonitor, retorno.AncoraAbsoluta, retorno.TelaDoMonitor), "o retorno acompanha");
        c.Aplicar(new FullscreenTargetsChanged(MonitoresOcupados.Nenhum)).Percorreu(Estado.Idle, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "volta ao DISPLAY1, nas coordenadas novas");
    }

    // ---------------------------------------------------------------- CLICK depois de o monitor mudar no gesto

    [Teste]
    public static void Pressionado_TrocaDePrincipalEClique_ValidaNoLugarTransladado()
    {
        // Com o botão pressionado, a topologia só vai para o cache e a janela não se move; a posição acompanha. O CLICK valida
        // a partir dela: (3552,1032), no mesmo DISPLAY1. Validar a âncora de antes, (1632,1032), o levaria ao DISPLAY2.
        Cenario c = Cenario.Em(Estado.Pressed, topologia: SecundarioAEsquerda);
        c.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2))).Esta(Estado.Pressed).SemTransicao();
        Afirmar.Igual(0, c.Efeitos.Count, "o gesto não é interrompido nem a janela movida");
        Afirmar.Igual(new PontoPx(3552, 1032), Afirmar.NaoNulo(c.Atual.Posicao).AncoraAbsoluta, "a posição acompanha");

        c.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "reage no DISPLAY1, transladado");
        Afirmar.Igual(new PontoPx(3552, 1032), c.Efeito<MoverJanela>().Destino.Ancora, "a janela vai junto");
        c.Passos(Padrao.PassosDaReacao).Percorreu(Estado.Reacting, Estado.Settling, Estado.Idle);
        Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "e termina lá");
    }

    [Teste]
    public static void Pressionado_MonitorMudouDeGeometriaEClique_AsFracoesDescrevemOLugar()
    {
        // O DISPLAY1 fica mais largo (2560x1080) com o botão pressionado. O CLICK valida a partir da posição que acompanhou a
        // topologia: a mesma fração, 0,85 · 2560 = 2176, como sem o gesto. As frações gravadas descrevem o lugar validado: a
        // partida seguinte o restaura onde ele estava, e não noutro ponto (pendência da DEC-030 resolvida aqui).
        Topologia maisLargo = ComMonitor(UmMonitor, Display1, m => m with { Tela = Ret(0, 0, 2560, 1080), AreaUtil = Ret(0, 0, 2560, 1032) });
        Cenario c = Cenario.Em(Estado.Pressed);
        c.Aplicar(new TopologyChanged(maisLargo)).Esta(Estado.Pressed).SemTransicao();
        c.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual(new PontoPx(2176, 1032), c.Ancora, "a mesma posição relativa na área útil nova");

        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        PosicaoDoPersonagem gravada = c.Efeito<GravarPosicao>().Posicao;
        Afirmar.Igual((Display1, new PontoPx(2176, 1032), (RetanguloPx?)Ret(0, 0, 2560, 1080)), (gravada.ChaveMonitor, gravada.AncoraAbsoluta, gravada.TelaDoMonitor), "grava onde está, com a tela nova");
        (Posicionamento restaurado, _, OrigemDaRestauracao origem) = Posicionador.Restaurar(maisLargo, gravada, c.Config.Tamanho);
        Afirmar.Igual((OrigemDaRestauracao.PelaChave, new PontoPx(2176, 1032)), (origem, restaurado.Ancora), "a partida seguinte volta ao mesmo lugar");
    }

    // ---------------------------------------------------------------- as outras saídas do gesto (revisão do bloco P6-P9)

    [Teste]
    public static void Pressionado_TrocaDePrincipal_TodasAsSaidasFicamNoMesmoMonitorFisico()
    {
        // Revisão de correção do bloco P6-P9, achado 2 (testes R1 e R2): como o CLICK (R14), toda saída de PRESSED parte do
        // lugar que a posição acompanhada descreve. S2, pressionado no DISPLAY1 a 85%; o DISPLAY2 vira o principal e o DISPLAY1
        // vai para (1920,0)-(3840,1080): ele fica no DISPLAY1, em (3552,1032). Partir da âncora de antes do gesto,
        // (1632,1032), o levava ao DISPLAY2 (com o esconderijo, escondido atrás da borda do monitor errado).
        Topologia trocada = Rebaseada(SecundarioAEsquerda, Display2);
        (string Caso, ConfiguracaoDoNucleo Config, Evento[] Eventos, Estado Fim)[] casos =
        [
            ("CLICK", Padrao, [new Click()], Estado.Reacting),
            ("DRAG_CANCEL em PRESSED", Padrao, [new DragCancel()], Estado.Idle),
            ("DOUBLE_CLICK sem o esconderijo", Padrao, [new DoubleClick()], Estado.Idle),
            ("DOUBLE_CLICK com o esconderijo (DEC-025)", DoApp, [new DoubleClick()], Estado.Peeking),
            ("DRAG_START e DRAG_CANCEL sem movimento", Padrao, [new DragStart(), new DragCancel()], Estado.Idle),
            ("DRAG_START e CMD_HIDE sem movimento", Padrao, [new DragStart(), new CmdHide()], Estado.Hidden),
        ];
        foreach ((string caso, ConfiguracaoDoNucleo cfg, Evento[] eventos, Estado fim) in casos)
        {
            Cenario c = Cenario.Em(Estado.Pressed, cfg, SecundarioAEsquerda);
            c.Aplicar(new TopologyChanged(trocada)).Esta(Estado.Pressed).SemTransicao();
            Afirmar.Igual(0, c.Efeitos.Count, $"{caso}: o gesto não é interrompido nem a janela movida");
            c.Aplicar(eventos).Esta(fim, $"{caso}: estado");
            PosicaoDoPersonagem p = Afirmar.NaoNulo(c.Atual.Posicao);
            Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (p.ChaveMonitor, p.AncoraAbsoluta), $"{caso}: a posição, no DISPLAY1 físico");
            if (fim != Estado.Hidden)
                Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), $"{caso}: o lugar, no DISPLAY1 físico");
            else
                Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Efeito<GravarPosicao>().Posicao.ChaveMonitor, c.Efeito<GravarPosicao>().Posicao.AncoraAbsoluta),
                    $"{caso}: a posição gravada (settings.json)");
        }
    }

    [Teste]
    public static void Pressionado_MonitorMudouOuSumiu_TodasAsSaidasVaoAondeEleIriaParado()
    {
        // Achados 2 e 3 da revisão (teste R4): com a geometria do monitor mudada (classe B) ou com ele desconectado (classe
        // C, R15b e R15c), a saída do gesto vai à mesma posição relativa que ele teria parado ou escondido, e não à âncora
        // transladada presa na borda do sobrevivente: R15b, (1632,1032) no DISPLAY2, e não (1856,1032).
        Topologia maisLargo = ComMonitor(SecundarioAEsquerda, Display1, m => m with { Tela = Ret(0, 0, 2560, 1080), AreaUtil = Ret(0, 0, 2560, 1032) });
        (string Caso, Topologia Antes, Topologia Depois, string Chave, PontoPx Ancora)[] casos =
        [
            ("classe B, 2560 de largura", SecundarioAEsquerda, maisLargo, Display1, new PontoPx(2176, 1032)),
            ("R15b", SecundarioAEsquerda, SemOPrincipal(SecundarioAEsquerda, Display1, Display2), Display2, new PontoPx(1632, 1032)),
            ("R15c, promove o 2", TresEmLinhaComPrincipalNoMeio, SemOPrincipal(TresEmLinhaComPrincipalNoMeio, Display1, Display2), Display3, new PontoPx(5472, 1032)),
            ("R15c, promove o 3", TresEmLinhaComPrincipalNoMeio, SemOPrincipal(TresEmLinhaComPrincipalNoMeio, Display1, Display3), Display3, new PontoPx(1632, 1032)),
        ];
        foreach ((string caso, Topologia antes, Topologia depois, string chave, PontoPx ancora) in casos)
        {
            Cenario parado = Cenario.Parado(topologia: antes).Aplicar(new TopologyChanged(depois));
            Afirmar.Igual((chave, ancora), (parado.Retrato.ChaveMonitor, parado.Ancora), $"{caso}: premissa, parado");
            foreach (Evento[] saida in new Evento[][] { [new Click()], [new DragCancel()], [new DoubleClick()], [new DragStart(), new DragCancel()] })
            {
                string nome = string.Join("+", saida.Select(e => e.GetType().Name));
                Cenario c = Cenario.Em(Estado.Pressed, topologia: antes).Aplicar(new TopologyChanged(depois)).Aplicar(saida);
                Afirmar.Igual((chave, ancora), (c.Retrato.ChaveMonitor, c.Ancora), $"{caso}, {nome}: onde ele iria parado");
                Afirmar.Igual((chave, ancora), (c.Atual.Posicao!.ChaveMonitor, c.Atual.Posicao.AncoraAbsoluta), $"{caso}, {nome}: a posição descreve o lugar");
            }
        }
    }

    [Teste]
    public static void Arrastando_TrocaDePrincipal_OLugarAndaComOMonitor_EAsSaidasFicamNele()
    {
        // Achado 2 da revisão (teste R3): no arraste, a janela segue o cursor, e numa troca de principal o Windows leva a janela
        // e o cursor junto com o monitor físico. O lugar do arraste anda com o monitor em que está: soltar, esconder ou sair
        // antes do próximo DRAG_MOVE fica no mesmo monitor físico, e a posição gravada (settings.json) é a dele.
        Topologia trocada = Rebaseada(SecundarioAEsquerda, Display2);
        foreach (Evento saida in new Evento[] { new DragCancel(), new CmdHide(), new SessionLocked(), new CmdExit() })
        {
            string caso = saida.GetType().Name;
            Cenario c = Cenario.Em(Estado.Dragging, topologia: SecundarioAEsquerda);
            c.Aplicar(new TopologyChanged(trocada)).Esta(Estado.Dragging).SemTransicao();
            Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), $"{caso}: o lugar do arraste anda junto");
            Afirmar.Igual(c.Atual.Lugar, c.Efeito<MoverJanela>().Destino, $"{caso}: a janela vai para onde o Windows já a levou");
            c.Aplicar(saida);
            GravarPosicao gravada = c.Efeito<GravarPosicao>();
            Afirmar.Igual((Display1, new PontoPx(3552, 1032)), (gravada.Posicao.ChaveMonitor, gravada.Posicao.AncoraAbsoluta), $"{caso}: grava no DISPLAY1 físico");
        }

        // O movimento seguinte continua sendo o cursor menos a pegada (invariante 2), já nas coordenadas novas.
        Cenario m = Cenario.Em(Estado.Dragging, topologia: SecundarioAEsquerda).Aplicar(new TopologyChanged(trocada));
        m.Aplicar(new DragMove(new PontoPx(Cenario.PontoOpaco.X + 1920 + 10, Cenario.PontoOpaco.Y)));
        Afirmar.Igual(new PontoPx(3562, 1032), m.Ancora, "cursor menos a pegada");
    }

    [Teste]
    public static void Arrastando_ChaveNovaComAMesmaTela_OLugarFica_MesmoComOOutroMonitorRearranjado()
    {
        // O monitor do arraste trocou de chave com a mesma tela (a reserva gdi: que vira mon:) e o outro foi levado para a
        // direita na mesma releitura: pelo apelido por retângulo, é o mesmo monitor, que não andou. Sem o apelido, o lugar
        // andaria com o sobrevivente (+3840) e iria parar no outro monitor.
        Topologia nova = new([
            Principal("mon:1", Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
            Secundario(Display2, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96),
        ]);
        Cenario c = Cenario.Em(Estado.Dragging, topologia: SecundarioAEsquerda).Aplicar(new TopologyChanged(nova));
        Afirmar.Igual(("mon:1", new PontoPx(1632, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "no mesmo monitor, com a chave nova, no mesmo lugar");
        c.Aplicar(new DragCancel());
        Afirmar.Igual(("mon:1", new PontoPx(1632, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "e solto lá");
    }

    [Teste]
    public static void Arrastando_OMonitorSumiu_OLugarAndaComOSobreviventeMedidoNasCoordenadasAntigas()
    {
        // S2, arrastando no principal, que é desconectado: o DISPLAY2 assume e vai para (0,0), +1920. O lugar do arraste anda com
        // o sobrevivente mais próximo medido nas coordenadas antigas (D8), como a âncora acompanhada: (3552,1032), à direita
        // dele; soltar sem mover valida ali, presa na borda direita do DISPLAY2 (1920 − 64). Sem andar, o ponto antigo
        // (1632,1032) cairia no meio do DISPLAY2.
        Cenario c = Cenario.Em(Estado.Dragging, topologia: SecundarioAEsquerda).Aplicar(new TopologyChanged(SemOPrincipal(SecundarioAEsquerda, Display1, Display2)));
        Afirmar.Igual(new PontoPx(3552, 1032), c.Ancora, "o lugar do arraste anda com o sobrevivente");
        c.Aplicar(new DragCancel());
        Afirmar.Igual((Display2, new PontoPx(1856, 1032)), (c.Retrato.ChaveMonitor, c.Ancora), "solto e validado no DISPLAY2");
    }

    [Teste]
    public static void ItemNaMao_TrocaDePrincipal_AndaComOMonitor_ESoltoFicaNele()
    {
        // Achado 6 da revisão (sonda 5): o item na mão segue o cursor; numa troca de principal, anda com o monitor em que está,
        // como o arraste do personagem. Largado (ITEM_RELEASE) logo depois, fica no mesmo monitor físico, e não do outro lado.
        ConfiguracaoDoNucleo comItens = Padrao with { Tamagotchi = true };
        var d = new PontoPx(1920, 0);
        Topologia trocada = Rebaseada(SecundarioAEsquerda, Display2);
        foreach (bool arrastando in new[] { false, true })
        {
            string caso = arrastando ? "arrastado" : "segurado";
            Cenario c = Cenario.Parado(comItens, SecundarioAEsquerda).Aplicar(new CmdSummonItem(Item.Banana));
            while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
            ItemNoMundo item = c.Atual.Itens.Todos[0];
            var cursor = new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10);
            c.Aplicar(new ItemPress(item.Id, cursor));
            if (arrastando) c.Aplicar(new ItemDragStart(item.Id), new ItemDragMove(item.Id, cursor));
            c.Aplicar(new TopologyChanged(trocada));
            ItemNoMundo naMao = c.Atual.Itens.PorId(item.Id)!;
            Afirmar.Verdadeiro(naMao.NaMao, $"{caso}: continua na mão");
            Afirmar.Igual((Display1, Mais(item.Lugar.Ancora, d), item.Lugar.Ancora.Y + d.Y), (naMao.Lugar.Monitor.Chave, naMao.Lugar.Ancora, (int)Math.Round(naMao.Y)),
                $"{caso}: na mão, anda com o monitor");
            Afirmar.Verdadeiro(c.Efeitos.Contains(new MoverItem(item.Id, naMao.Lugar)), $"{caso}: a janela do item acompanha");
            c.Aplicar(new ItemRelease(item.Id));
            ItemNoMundo solto = c.Atual.Itens.PorId(item.Id)!;
            Afirmar.Igual((Display1, Mais(item.Lugar.Ancora, d), SituacaoDoItem.NoChao), (solto.Lugar.Monitor.Chave, solto.Lugar.Ancora, solto.Situacao), $"{caso}: largado no mesmo monitor físico");
            Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, $"{caso}: o personagem também está no DISPLAY1");
        }
    }

    [Teste]
    public static void GestoAtravessandoMudancas_ASaidaVaiAondeEleIriaParado()
    {
        // Propriedade: pressionado num ponto aleatório de topologias aleatórias, com uma mudança no meio do gesto (que muitas
        // vezes conserva monitores: troca de principal, conexão, desconexão e chave nova), toda saída de PRESSED termina no
        // mesmo lugar que um personagem parado no mesmo ponto teria depois da mesma mudança. Uma mudança só: parado, a
        // translação reescreve as frações pelo lugar (ContinuarNoMonitor), e uma segunda mudança de geometria pode então
        // arredondar 1 px diferente das frações guardadas no gesto, sem que nenhum dos dois esteja errado.
        var mestre = new Random(20261001);
        int casos = 0, mudouOMonitor = 0;
        for (int n = 0; n < 1500; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia t = gerador.NovaTopologia();
            MonitorDoDesktop m = t.Monitores[rnd.Next(t.Monitores.Count)];
            PosicaoDoPersonagem salva = Posicionador.Descrever(Posicionador.NoMonitor(m, gerador.Fracao(), 1, Padrao.Tamanho));
            Cenario parado = new Cenario().Aplicar(new Loaded(t, salva, Preferencias.Padrao));
            Cenario pressionado = new Cenario().Aplicar(new Loaded(t, salva, Preferencias.Padrao));
            PontoPx a = pressionado.Ancora;
            pressionado.Aplicar(new Press(new PontoPx(a.X, a.Y - 20)));
            Topologia atual = gerador.MudarComIdentidade(t);
            parado.Aplicar(new TopologyChanged(atual));
            pressionado.Aplicar(new TopologyChanged(atual));
            if (!Equals(atual.PorChave(pressionado.Atual.Lugar!.Monitor.Chave), pressionado.Atual.Lugar.Monitor)) mudouOMonitor++;
            Evento[] saida = rnd.Next(4) switch
            {
                0 => [new Click()],
                1 => [new DragCancel()],
                2 => [new DoubleClick()],
                _ => [new DragStart(), new DragCancel()],
            };
            pressionado.Aplicar(saida);
            string onde = $"semente {semente}, {string.Join("+", saida.Select(e => e.GetType().Name))}";
            Afirmar.Igual((parado.Retrato.ChaveMonitor, parado.Ancora), (pressionado.Retrato.ChaveMonitor, pressionado.Ancora), $"{onde}: onde ele iria parado");
            casos++;
        }
        Console.WriteLine($"         {casos} gestos atravessando mudanças de topologia, {mudouOMonitor} com o monitor do gesto mudado ou sumido");
        Afirmar.Verdadeiro(mudouOMonitor > 300, $"o gerador exercitou {mudouOMonitor} gestos com o monitor mudado");
    }

    // ---------------------------------------------------------------- esconderijo e preso (DEC-024, DEC-025)

    [Teste]
    public static void EsconderijoEPreso_ContinuamQuandoOMonitorSoTransladou()
    {
        var d = new PontoPx(1920, 0);
        Topologia trocada = Rebaseada(SecundarioAEsquerda, Display2);

        // Escondido atrás da barra, no DISPLAY1 de S2.
        var escondido = new SimuladorDeTempo(DoApp, 4, SecundarioAEsquerda);
        CliqueDuplo(escondido);
        escondido.Esta(Estado.Peeking, "escondido");
        AfirmarContinua(escondido, trocada, d, "escondido");
        Afirmar.Igual(LadoDoEsconderijo.Baixo, escondido.Estado.Esconderijo, "escondido: na mesma borda");

        // Preso pelo usuário na parede direita e no cipó do DISPLAY1.
        foreach ((string caso, PontoPx alvo, Estado estado) in new[] { ("na parede", new PontoPx(1836, 600), Estado.Climbing), ("no cipó", new PontoPx(1000, 140), Estado.Hanging) })
        {
            var sim = new SimuladorDeTempo(DoApp, 6, SecundarioAEsquerda);
            ArrastarPara(sim, alvo);
            sim.Esta(estado, $"{caso}: agarrado");
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, $"{caso}: preso pelo usuário");
            AfirmarContinua(sim, trocada, d, caso);
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, $"{caso}: continua preso e agarrado");
        }
    }

    // ---------------------------------------------------------------- itens (DEC-028)

    [Teste]
    public static void Itens_SeguemAMesmaRegraDoPersonagem()
    {
        ConfiguracaoDoNucleo comItens = Padrao with { Tamagotchi = true };

        // Troca de principal em S2: um item no chão e outro caindo, no DISPLAY1, andam juntos; o que cai continua caindo,
        // com a mesma velocidade, e pousa na coluna transladada.
        Cenario c = Cenario.Parado(comItens, SecundarioAEsquerda).Aplicar(new CmdSummonItem(Item.Banana));
        while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
        c.Aplicar(new CmdSummonItem(Item.Agua)).Passos(5);
        ItemNoMundo noChao = c.Atual.Itens.Todos[0], caindo = c.Atual.Itens.Todos[1];
        Afirmar.Igual((SituacaoDoItem.NoChao, SituacaoDoItem.Caindo), (noChao.Situacao, caindo.Situacao), "um no chão e outro caindo");
        c.Aplicar(new TopologyChanged(Rebaseada(SecundarioAEsquerda, Display2)));
        ItemNoMundo noChaoDepois = c.Atual.Itens.PorId(noChao.Id)!, caindoDepois = c.Atual.Itens.PorId(caindo.Id)!;
        Afirmar.Igual((SituacaoDoItem.NoChao, Mais(noChao.Lugar.Ancora, new PontoPx(1920, 0))), (noChaoDepois.Situacao, noChaoDepois.Lugar.Ancora), "no chão: transladado");
        Afirmar.Igual((SituacaoDoItem.Caindo, Mais(caindo.Lugar.Ancora, new PontoPx(1920, 0)), caindo.VY, caindo.Y), (caindoDepois.Situacao, caindoDepois.Lugar.Ancora, caindoDepois.VY, caindoDepois.Y),
            "caindo: transladado, com a mesma velocidade");
        Afirmar.Igual(Display1, caindoDepois.Lugar.Monitor.Chave, "no mesmo monitor");
        Afirmar.Verdadeiro(c.Efeitos.Contains(new MoverItem(noChao.Id, noChaoDepois.Lugar)) && c.Efeitos.Contains(new MoverItem(caindo.Id, caindoDepois.Lugar)), "as janelas acompanham");
        while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
        Afirmar.Igual(new PontoPx(caindoDepois.Lugar.Ancora.X, 1032), c.Atual.Itens.PorId(caindo.Id)!.Lugar.Ancora, "pousa na coluna transladada");

        // O principal de [2][1*][3] sai e o 2 assume: o item no chão do 1 vai ao 3, medido nas coordenadas antigas, a 85%.
        Cenario t = Cenario.Parado(comItens, TresEmLinhaComPrincipalNoMeio).Aplicar(new CmdSummonItem(Item.Banana));
        while (t.Atual.Itens.AlgumCaindo) t.Aplicar(new Tick());
        ItemNoMundo item = t.Atual.Itens.Todos[0];
        double fx = item.Posicao.FracaoX;
        t.Aplicar(new TopologyChanged(SemOPrincipal(TresEmLinhaComPrincipalNoMeio, Display1, Display2)));
        ItemNoMundo movido = t.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual(Display3, movido.Lugar.Monitor.Chave, "no 3, o sobrevivente mais próximo nas coordenadas antigas");
        Afirmar.Igual(new PontoPx(3840 + (int)Math.Round(fx * 1920, MidpointRounding.AwayFromZero), 1032), movido.Lugar.Ancora, "na mesma posição relativa, no chão");
    }

    // ---------------------------------------------------------------- propriedade com a física do aplicativo

    [Teste]
    public static void ComAFisicaDoAplicativo_InvariantesDezenoveEVinteEmMilharesDePassos()
    {
        // Simulações longas com a configuração do aplicativo (física, toon force, agarrar, esconderijo e itens), com o usuário
        // arrastando para qualquer ponto (perto das bordas, agarra e fica preso), clicando, escondendo pelo clique duplo e
        // invocando itens, e com mudanças de topologia que muitas vezes conservam o monitor do personagem. A cada evento, o
        // apoio (critério 2 da Fase 4). A cada TOPOLOGY_CHANGED com outra configuração:
        // - invariante 19: num estado que revalida, com a geometria do monitor dele conservada (no máximo transladada), o estado
        //   continua, com uma transição para ele mesmo, e a âncora e a posição fina andam exatamente pela translação; senão,
        //   SETTLING;
        // - invariante 20: depois, visível e fora de PRESSED e DRAGGING, o monitor dele é da topologia nova e, quando o sprite
        //   cabe, a âncora está na área útil dele.
        var mestre = new Random(20261009);
        int mudancas = 0, transladados = 0, revalidados = 0;
        var continuaram = new HashSet<Estado>();
        for (int n = 0; n < 80; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia t = gerador.NovaTopologia();
            var sim = new SimuladorDeTempo(DoApp, (ulong)semente, t);
            string Onde(Evento e) => $"simulação {n} (semente {semente}), {e}";
            sim.AoAplicar = (_, e, depois) => MovimentoTestes.ConferirApoio(depois, Onde(e));
            for (int k = 0; k < 60; k++)
            {
                sim.Avancar(TimeSpan.FromMilliseconds(rnd.Next(50, 8000)));
                if (sim.Estado.Lugar is not { } l) continue;
                var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 20);
                switch (rnd.Next(10))
                {
                    case 0 or 1:
                        PontoPx alvo = gerador.Ponto(t);
                        sim.Aplicar(new Press(corpo));
                        sim.Aplicar(new DragStart());
                        sim.Aplicar(new DragMove(new PontoPx(alvo.X, alvo.Y - 20)));
                        sim.Aplicar(new DragEnd(new PontoPx(alvo.X, alvo.Y - 20)));
                        break;
                    case 2:
                        sim.Aplicar(new Press(corpo));
                        sim.Aplicar(new Click());
                        if (rnd.Next(2) == 0)
                        {
                            sim.Aplicar(new Press(corpo));
                            sim.Aplicar(new DoubleClick());
                        }
                        break;
                    case 3:
                        sim.Aplicar(new CmdSummonItem((Item)rnd.Next(13)));
                        break;
                    default:
                        EstadoDoNucleo antes = sim.Estado;
                        int transicoes = sim.Transicoes.Count;
                        Topologia nova = gerador.MudarComIdentidade(t);
                        var evento = new TopologyChanged(nova);
                        sim.Aplicar(evento);
                        if (!t.MesmaConfiguracao(nova))
                        {
                            mudancas++;
                            (bool continuou, bool andou) = ConferirDezenoveEVinte(t, nova, antes, sim.Estado, [.. sim.Transicoes.Skip(transicoes)], Onde(evento));
                            if (continuou) continuaram.Add(antes.Estado);
                            if (andou) transladados++;
                            if (!continuou && antes.Estado.Visivel() && antes.Estado is not (Estado.Pressed or Estado.Dragging)) revalidados++;
                        }
                        t = nova;
                        break;
                }
            }
        }
        Console.WriteLine($"         {mudancas} mudanças de topologia: continuou em {string.Join(", ", continuaram.Order())} ({transladados} transladados), {revalidados} revalidações");
        foreach (Estado e in new[] { Estado.Idle, Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Falling, Estado.Peeking })
            Afirmar.Verdadeiro(continuaram.Contains(e), $"o gerador exercitou {e} numa mudança que conserva o monitor");
        Afirmar.Verdadeiro(transladados > 50 && revalidados > 50, $"transladados {transladados}, revalidações {revalidados}");
    }

    /// <summary>Invariantes 19 e 20 para um TOPOLOGY_CHANGED, com regras escritas aqui; devolve se continuou e se andou.</summary>
    private static (bool Continuou, bool Andou) ConferirDezenoveEVinte(Topologia velha, Topologia nova, EstadoDoNucleo antes, EstadoDoNucleo depois, List<Transicao> transicoes, string onde)
    {
        (bool, bool) resultado = (false, false);
        bool revalida = antes.Estado is Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Falling
            or Estado.Landing or Estado.Resting or Estado.Peeking or Estado.Reacting or Estado.Using;
        if (revalida && antes.Lugar is { } lugar)
        {
            MonitorDoDesktop m = lugar.Monitor;
            MonitorDoDesktop? n = nova.PorChave(m.Chave) ?? nova.Monitores.FirstOrDefault(x => x.Tela == m.Tela && velha.PorChave(x.Chave) is null);
            int dx = n is null ? 0 : n.Tela.Esquerda - m.Tela.Esquerda, dy = n is null ? 0 : n.Tela.Topo - m.Tela.Topo;
            // A travessia em curso (Fase 5, passo P13; C15 da crítica) fica fora do "continua": ela se desfaz e vai a SETTLING.
            bool emTravessia = antes.Movimento.Travessia is { } tr && (antes.Estado == Estado.Jumping || (antes.Estado == Estado.Walking && tr.Tipo == TipoDeTravessia.Andando));
            if (!emTravessia && n is not null && n.Tela == m.Tela.Deslocado(dx, dy) && n.AreaUtil == m.AreaUtil.Deslocado(dx, dy) && n.Dpi == m.Dpi)
            {
                Afirmar.Sequencia([$"{antes.Estado}->{antes.Estado}"], transicoes.Select(x => $"{x.De}->{x.Para}"), $"invariante 19: {onde}: continua");
                Afirmar.Igual(new PontoPx(lugar.Ancora.X + dx, lugar.Ancora.Y + dy), depois.Lugar!.Ancora, $"invariante 19: {onde}: a âncora anda ({dx},{dy})");
                if (antes.Estado.EmMovimento())
                    Afirmar.Igual((antes.Movimento.X + dx, antes.Movimento.Y + dy, antes.Movimento.VX, antes.Movimento.VY), (depois.Movimento.X, depois.Movimento.Y, depois.Movimento.VX, depois.Movimento.VY),
                        $"invariante 19: {onde}: a posição fina anda junto e a velocidade fica");
                Afirmar.Igual((antes.Esconderijo, antes.PresoPeloUsuario, antes.Uso), (depois.Esconderijo, depois.PresoPeloUsuario, depois.Uso), $"invariante 19: {onde}: o esconderijo, o preso e o uso ficam");
                resultado = (true, dx != 0 || dy != 0);
            }
            else
            {
                Afirmar.Igual(Estado.Settling, transicoes[0].Para, $"{onde}: a geometria mudou ou o monitor sumiu: revalida");
            }
        }
        if (depois.Estado.Visivel() && depois.Estado is not (Estado.Pressed or Estado.Dragging) && depois.Lugar is { } final)
        {
            RetanguloPx area = final.Monitor.AreaUtil;
            bool cabe = final.Tamanho.Largura <= area.Largura && final.Tamanho.Altura <= area.Altura;
            Afirmar.Verdadeiro(nova.Monitores.Contains(final.Monitor), $"invariante 20: {onde}: o monitor {final.Monitor.Chave} é da topologia nova");
            Afirmar.Verdadeiro(!cabe || (final.Ancora.X >= area.Esquerda && final.Ancora.X < area.Direita && final.Ancora.Y > area.Topo && final.Ancora.Y <= area.Base),
                $"invariante 20: {onde}: a âncora {final.Ancora} na área útil {area}");
        }
        return resultado;
    }

    // ---------------------------------------------------------------- auxiliares

    private static string Renomear(string chave) => "mon:" + chave[^1];

    private static MonitorDoDesktop Monitor(Topologia t, string chave) => Afirmar.NaoNulo(t.PorChave(chave), chave);

    private static PosicaoDoPersonagem DescreverNo(Topologia t, string chave, double fx, double fy)
        => Posicionador.Descrever(Posicionador.NoMonitor(Monitor(t, chave), fx, fy, new TamanhoDip(128, 128)));

    /// <summary>Carregado no chão do monitor da chave, na fração dada.</summary>
    private static Cenario NoMonitor(Topologia t, string chave, double fx)
        => new Cenario().Aplicar(new Loaded(t, DescreverNo(t, chave, fx, 1), Preferencias.Padrao));

    /// <summary>A agenda decide andar (sem a física, o estado muda e a âncora fica).</summary>
    private static Cenario Andando(Cenario c)
        => c.AplicarCom(c.Config with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao)).Esta(Estado.Walking);

    private static PontoPx Mais(PontoPx a, PontoPx b) => new(a.X + b.X, a.Y + b.Y);

    private static string Ancora(EstadoDoNucleo s, PontoPx menos) => s.Lugar is { } l ? $"({l.Ancora.X - menos.X},{l.Ancora.Y - menos.Y})" : "-";

    /// <summary>
    /// A troca de principal translada o monitor do personagem: o estado continua, com uma transição para ele mesmo, a âncora
    /// e a janela transladadas, e sem mexer no relógio nem na agenda.
    /// </summary>
    private static void AfirmarContinua(SimuladorDeTempo sim, Topologia nova, PontoPx d, string caso)
    {
        EstadoDoNucleo antes = sim.Estado;
        int transicoes = sim.Transicoes.Count;
        sim.Aplicar(new TopologyChanged(nova));
        Afirmar.Sequencia([$"{antes.Estado}->{antes.Estado}"], sim.Transicoes.Skip(transicoes).Select(t => $"{t.De}->{t.Para}"), $"{caso}: continua");
        Afirmar.Igual(Mais(antes.Lugar!.Ancora, d), sim.Estado.Lugar!.Ancora, $"{caso}: transladado");
        Afirmar.Igual((antes.Geracao, antes.DecisaoAgendada, antes.RelogioAtivo), (sim.Estado.Geracao, sim.Estado.DecisaoAgendada, sim.Estado.RelogioAtivo), $"{caso}: a agenda e o relógio seguem");
    }

    /// <summary>O clique duplo como o árbitro o entrega: PRESS, CLICK, PRESS e DOUBLE_CLICK.</summary>
    private static void CliqueDuplo(SimuladorDeTempo sim)
    {
        PontoPx a = sim.Estado.Lugar!.Ancora;
        var p = new PontoPx(a.X, a.Y - 20);
        sim.Aplicar(new Press(p));
        sim.Aplicar(new Click());
        sim.Aplicar(new Press(p));
        sim.Aplicar(new DoubleClick());
    }

    /// <summary>Arrasta pelo corpo e solta com a âncora em <paramref name="alvo"/>.</summary>
    private static void ArrastarPara(SimuladorDeTempo sim, PontoPx alvo)
    {
        PontoPx a = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new DragStart());
        var cursor = new PontoPx(alvo.X, alvo.Y - 30);
        sim.Aplicar(new DragMove(cursor));
        sim.Aplicar(new DragEnd(cursor));
    }
}
