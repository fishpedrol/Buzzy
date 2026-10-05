using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes.Persistencia;

/// <summary>
/// A borda do esconderijo (DEC-025) e a marca "preso pelo usuário" (DEC-024) gravadas com a posição (esquema v3; DEC-029,
/// item 11): todo GravarPosicao as leva, como estão no estado; a carga as recebe com a posição salva e a acomodação da
/// partida o devolve escondido na mesma borda, ou preso onde o usuário o deixou. Cada partida seguinte é montada pelo
/// caminho do arquivo: o GravarPosicao da saída é escrito e lido pelo esquema, e as configurações lidas viram a carga.
/// Com a configuração do aplicativo (física, agarrar e esconderijo pelo clique duplo).
/// </summary>
internal static class PosturaSalvaTestes
{
    private static readonly ConfiguracaoDoNucleo DoApp = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    private static Superficies Sup(SimuladorDeTempo sim)
    {
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        return Superficies.Do(Afirmar.NaoNulo(sim.Estado.Topologia, "topologia"), l.Monitor, l.Tamanho);
    }

    /// <summary>Arrasta o personagem pelo corpo e solta com a âncora em <paramref name="alvo"/>.</summary>
    private static void ArrastarPara(SimuladorDeTempo sim, PontoPx alvo)
    {
        PontoPx a = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new DragStart());
        var cursor = new PontoPx(alvo.X, alvo.Y - 30);
        sim.Aplicar(new DragMove(cursor));
        sim.Aplicar(new DragEnd(cursor));
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

    /// <summary>Sai pelo menu e devolve o GravarPosicao da saída (um só).</summary>
    private static GravarPosicao SairEGravar(SimuladorDeTempo sim)
    {
        var gravadas = new List<GravarPosicao>();
        sim.AoResultado = (_, _, r) => gravadas.AddRange(r.Efeitos.OfType<GravarPosicao>());
        sim.Aplicar(new CmdExit());
        sim.AoResultado = null;
        sim.Esta(Estado.Exiting, "saiu");
        Afirmar.Igual(1, gravadas.Count, "a saída grava a posição uma vez");
        return gravadas[0];
    }

    /// <summary>
    /// A partida seguinte pelo caminho do arquivo: o que a saída gravou passa pelo esquema (escrito e lido), volta igual,
    /// e as configurações lidas viram a carga (<see cref="ConfiguracoesSalvas.ParaACarga"/>).
    /// </summary>
    private static SimuladorDeTempo Reabrir(GravarPosicao gravada, Topologia topologia, string caso, ulong semente = 11)
    {
        var salvas = new ConfiguracoesSalvas(gravada.Posicao, Preferencias.Padrao) { Esconderijo = gravada.Esconderijo, PresoPeloUsuario = gravada.PresoPeloUsuario };
        LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(EsquemaDeConfiguracoes.Escrever(salvas));
        Afirmar.Igual(SituacaoDaLeitura.Valida, lida.Situacao, $"{caso}: o arquivo é válido");
        Afirmar.Sequencia([], lida.Avisos, $"{caso}: sem aviso");
        Afirmar.Igual(salvas, lida.Configuracoes, $"{caso}: o arquivo devolve a posição com o esconderijo e o preso");
        return new SimuladorDeTempo(DoApp, semente, lida.Configuracoes.ParaACarga(topologia));
    }

    // Preso pelo usuário na parede (dos dois lados) ou no cipó: a saída grava a marca, e a partida seguinte o deixa
    // agarrado no mesmo lugar, ainda preso. Pausado, quem não estivesse preso desceria da parede ou se soltaria do cipó
    // (regra da calma): ficar lá, minutos depois, é a prova de que continua preso.
    [Teste]
    public static void PresoNaParedeOuNoCipo_SaiEReabre_ContinuaPresoNoMesmoLugar()
    {
        (string Caso, Func<Superficies, PontoPx> Alvo, Estado Estado)[] casos =
        [
            ("na parede da direita", s => new PontoPx(s.Direita - 30, 600), Estado.Climbing),
            ("na parede da esquerda", s => new PontoPx(s.Esquerda + 30, 480), Estado.Climbing),
            ("no cipó", s => new PontoPx(900, s.Teto + 40), Estado.Hanging),
        ];
        foreach ((string caso, Func<Superficies, PontoPx> alvo, Estado estado) in casos)
        {
            var sim = new SimuladorDeTempo(DoApp, 3, UmMonitor);
            ArrastarPara(sim, alvo(Sup(sim)));
            sim.Esta(estado, $"{caso}: agarrou");
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario, $"{caso}: posto lá pelo usuário");
            PontoPx onde = sim.Estado.Lugar!.Ancora;

            GravarPosicao gravada = SairEGravar(sim);
            Afirmar.Verdadeiro(gravada.PresoPeloUsuario, $"{caso}: a saída grava a marca de preso");
            Afirmar.Igual(LadoDoEsconderijo.Nenhum, gravada.Esconderijo, $"{caso}: sem esconderijo");

            SimuladorDeTempo reaberto = Reabrir(gravada, UmMonitor, caso);
            reaberto.Esta(estado, $"{caso}: reaberto, agarrado de novo");
            Afirmar.Igual(onde, reaberto.Estado.Lugar!.Ancora, $"{caso}: no mesmo lugar");
            Afirmar.Verdadeiro(reaberto.Estado.Movimento.Agarrado && reaberto.Estado.PresoPeloUsuario, $"{caso}: parado e ainda preso");
            Afirmar.Falso(reaberto.RelogioLigado, $"{caso}: agarrado, sem relógio");

            reaberto.Aplicar(new CmdPauseAutonomy());
            reaberto.Avancar(TimeSpan.FromMinutes(5));
            reaberto.Esta(estado, $"{caso}: pausado, continua lá");
            Afirmar.Igual(onde, reaberto.Estado.Lugar!.Ancora, $"{caso}: pausado, não se mexe");
        }
    }

    // Com a agenda ligada, o preso reaberto só passeia pela mesma superfície (DEC-024): nunca desce ao chão nem se solta.
    [Teste]
    public static void PresoReaberto_ComAAgenda_SoPasseiaPelaMesmaParede()
    {
        var sim = new SimuladorDeTempo(DoApp, 5, UmMonitor);
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(sup.Direita - 20, 500));
        SimuladorDeTempo reaberto = Reabrir(SairEGravar(sim), UmMonitor, "parede", semente: 5);
        int alturas = 0;
        reaberto.AoAplicar = (_, e, depois) =>
        {
            Afirmar.Igual(Estado.Climbing, depois.Estado, $"{e}: continua na parede");
            Afirmar.Igual(sup.Direita, depois.Lugar!.Ancora.X, $"{e}: encostado na mesma lateral");
            Afirmar.Verdadeiro(depois.PresoPeloUsuario, $"{e}: continua preso");
            alturas++;
        };
        reaberto.Avancar(TimeSpan.FromMinutes(10));
        Afirmar.Verdadeiro(alturas > 10 && reaberto.Transicoes.Any(t => t.Regra.Contains("passeia pela parede", StringComparison.Ordinal)),
            $"a agenda o fez passear pela parede ({alturas} eventos)");
    }

    // Escondido atrás da borda de baixo ou de uma lateral (DEC-025): a saída grava a borda, e a partida seguinte o devolve
    // escondido nela, no mesmo lugar, só trocando a cara. Outro clique duplo o tira de lá como sempre: na borda de baixo,
    // fica de pé; na lateral, volta a grudar na parede, preso pelo usuário.
    [Teste]
    public static void Escondido_SaiEReabre_VoltaEscondidoNaMesmaBorda()
    {
        (string Caso, LadoDoEsconderijo Lado, Func<Superficies, PontoPx>? Parede, Estado Saida)[] casos =
        [
            ("na borda de baixo", LadoDoEsconderijo.Baixo, null, Estado.Idle),
            ("atrás da lateral direita", LadoDoEsconderijo.Direita, s => new PontoPx(s.Direita - 20, 560), Estado.Climbing),
            ("atrás da lateral esquerda", LadoDoEsconderijo.Esquerda, s => new PontoPx(s.Esquerda + 20, 420), Estado.Climbing),
        ];
        foreach ((string caso, LadoDoEsconderijo lado, Func<Superficies, PontoPx>? parede, Estado saida) in casos)
        {
            var sim = new SimuladorDeTempo(DoApp, 4, UmMonitor);
            if (parede is not null) ArrastarPara(sim, parede(Sup(sim)));
            CliqueDuplo(sim);
            sim.Esta(Estado.Peeking, $"{caso}: escondido");
            Afirmar.Igual(lado, sim.Estado.Esconderijo, $"{caso}: a borda");
            PontoPx onde = sim.Estado.Lugar!.Ancora;

            GravarPosicao gravada = SairEGravar(sim);
            Afirmar.Igual(lado, gravada.Esconderijo, $"{caso}: a saída grava a borda");

            SimuladorDeTempo reaberto = Reabrir(gravada, UmMonitor, caso);
            reaberto.Esta(Estado.Peeking, $"{caso}: reaberto, escondido");
            Afirmar.Igual(lado, reaberto.Estado.Esconderijo, $"{caso}: na mesma borda");
            Afirmar.Igual(onde, reaberto.Estado.Lugar!.Ancora, $"{caso}: no mesmo lugar");
            Afirmar.Verdadeiro(reaberto.Transicoes.Any(t => t.Regra == $"SETTLING: escondido atrás da borda ({lado})"), $"{caso}: a regra da acomodação");

            // Minutos com a agenda ligada: nada o tira de lá.
            reaberto.AoAplicar = (_, e, depois) => Afirmar.Igual(Estado.Peeking, depois.Estado, $"{caso}, {e}: continua escondido");
            reaberto.Avancar(TimeSpan.FromMinutes(3));
            reaberto.AoAplicar = null;

            CliqueDuplo(reaberto);
            reaberto.Esta(saida, $"{caso}: o clique duplo o tira do esconderijo");
            Afirmar.Igual(LadoDoEsconderijo.Nenhum, reaberto.Estado.Esconderijo, $"{caso}: fora do esconderijo");
            if (saida == Estado.Climbing)
                Afirmar.Verdadeiro(reaberto.Estado.Movimento.Agarrado && reaberto.Estado.PresoPeloUsuario, $"{caso}: grudado na parede e preso");
        }
    }

    // Escondido na borda e depois pela bandeja: a ocultação da bandeja não vai para o arquivo (a partida mostra o
    // personagem), mas a borda vai, e ele volta escondido nela. Um pedido de esconder anterior à carga continua valendo, e
    // ele reaparece no esconderijo.
    [Teste]
    public static void EscondidoNaBordaEPelaBandeja_VoltaNaBorda_EAOcultacaoAntesDaCargaContinua()
    {
        var sim = new SimuladorDeTempo(DoApp, 4, UmMonitor);
        CliqueDuplo(sim);
        var gravadas = new List<GravarPosicao>();
        sim.AoResultado = (_, _, r) => gravadas.AddRange(r.Efeitos.OfType<GravarPosicao>());
        sim.Aplicar(new CmdHide());
        sim.AoResultado = null;
        Afirmar.Igual(LadoDoEsconderijo.Baixo, Afirmar.NaoNulo(gravadas.SingleOrDefault(), "esconder pela bandeja grava").Esconderijo, "com a borda");
        GravarPosicao gravada = SairEGravar(sim);
        Afirmar.Igual(LadoDoEsconderijo.Baixo, gravada.Esconderijo, "escondido pela bandeja, a saída grava a borda");

        SimuladorDeTempo reaberto = Reabrir(gravada, UmMonitor, "bandeja");
        reaberto.Esta(Estado.Peeking, "a partida mostra o personagem, escondido na borda");

        // A sessão bloqueada antes da carga: ele continua escondido pela sessão, com a borda guardada, e reaparece nela.
        var salvas = new ConfiguracoesSalvas(gravada.Posicao, Preferencias.Padrao) { Esconderijo = gravada.Esconderijo };
        var nucleo = new Nucleo(DoApp, 9);
        nucleo.Enfileirar(new SessionLocked());
        nucleo.Enfileirar(salvas.ParaACarga(UmMonitor));
        nucleo.Processar();
        Afirmar.Igual((Estado.Hidden, MotivoDoOcultamento.PorSessao, LadoDoEsconderijo.Baixo), (nucleo.Estado.Estado, nucleo.Estado.Motivo, nucleo.Estado.Esconderijo),
            "bloqueada antes da carga: continua escondida pela sessão, com a borda");
        nucleo.Enfileirar(new SessionUnlocked());
        nucleo.Processar();
        Afirmar.Igual(Estado.Peeking, nucleo.Estado.Estado, "desbloqueada, reaparece escondido na borda");
    }

    // A borda e a marca só valem com a posição salva e quando fazem sentido: sem posição, começa na posição inicial, de
    // pé; uma borda fora do enum vale nenhuma; sem o esconderijo pelo clique duplo na configuração, a borda é ignorada
    // (ele não teria como sair de lá); preso, mas restaurado longe da parede e do cipó, a acomodação apaga a marca.
    [Teste]
    public static void Carga_BordaEMarca_SoComAPosicaoEQuandoFazemSentido()
    {
        PosicaoDoPersonagem noChao = Posicionador.Descrever(Posicionador.NoMonitor(UmMonitor.Principal, 0.5, 1, new TamanhoDip(128, 128)));

        var semPosicao = new SimuladorDeTempo(DoApp, 1, new Loaded(UmMonitor, null, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Baixo, PresoPeloUsuario = true });
        semPosicao.Esta(Estado.Idle, "sem posição salva, de pé na posição inicial");
        Afirmar.Igual((LadoDoEsconderijo.Nenhum, false), (semPosicao.Estado.Esconderijo, semPosicao.Estado.PresoPeloUsuario), "sem posição, a borda e a marca são ignoradas");
        Afirmar.Igual(Posicionador.Inicial(UmMonitor, new TamanhoDip(128, 128)).Ancora, semPosicao.Estado.Lugar!.Ancora, "na posição inicial");

        var foraDoEnum = new SimuladorDeTempo(DoApp, 1, new Loaded(UmMonitor, noChao, Preferencias.Padrao) { Esconderijo = (LadoDoEsconderijo)7 });
        foraDoEnum.Esta(Estado.Idle, "borda fora do enum: nenhuma");
        Afirmar.Igual(LadoDoEsconderijo.Nenhum, foraDoEnum.Estado.Esconderijo);

        var semOModo = new SimuladorDeTempo(DoApp with { EsconderijoNoCliqueDuplo = false }, 1, new Loaded(UmMonitor, noChao, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Baixo });
        semOModo.Esta(Estado.Idle, "sem o esconderijo pelo clique duplo, a borda é ignorada");
        Afirmar.Igual(LadoDoEsconderijo.Nenhum, semOModo.Estado.Esconderijo);

        var presoNoChao = new SimuladorDeTempo(DoApp, 1, new Loaded(UmMonitor, noChao, Preferencias.Padrao) { PresoPeloUsuario = true });
        presoNoChao.Esta(Estado.Idle, "preso, mas no chão: de pé");
        Afirmar.Falso(presoNoChao.Estado.PresoPeloUsuario, "longe da parede e do cipó, a marca se apaga");

        var comBorda = new SimuladorDeTempo(DoApp, 1, new Loaded(UmMonitor, noChao, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Baixo });
        comBorda.Esta(Estado.Peeking, "com a posição e o modo, escondido");
    }

    // Só a postura do estado vai para o arquivo, a cada gravação: soltar na parede grava preso; soltar no chão, não;
    // escondido, a borda; a redefinição da posição grava a postura que o personagem tem depois dela.
    [Teste]
    public static void GravarPosicao_LevaAPosturaDoEstadoDepoisDoEvento()
    {
        var sim = new SimuladorDeTempo(DoApp, 3, UmMonitor);
        var gravadas = new List<(Evento Evento, GravarPosicao Gravada, EstadoDoNucleo Depois)>();
        sim.AoResultado = (_, e, r) => gravadas.AddRange(r.Efeitos.OfType<GravarPosicao>().Select(g => (e, g, r.Estado)));
        Superficies sup = Sup(sim);

        ArrastarPara(sim, new PontoPx(sup.Direita - 30, 600));
        ArrastarPara(sim, new PontoPx(700, sup.Chao));
        CliqueDuplo(sim);
        sim.Aplicar(new CmdResetPosition());
        sim.Aplicar(new CmdExit());

        Afirmar.Sequencia(["DragEnd preso=True esconderijo=Nenhum", "DragEnd preso=False esconderijo=Nenhum", "CmdResetPosition preso=False esconderijo=Baixo", "CmdExit preso=False esconderijo=Baixo"],
            gravadas.Select(g => $"{g.Evento.GetType().Name} preso={g.Gravada.PresoPeloUsuario} esconderijo={g.Gravada.Esconderijo}"), "a postura de cada gravação");
        foreach ((Evento e, GravarPosicao g, EstadoDoNucleo depois) in gravadas)
            Afirmar.Igual((depois.Esconderijo, depois.PresoPeloUsuario), (g.Esconderijo, g.PresoPeloUsuario), $"{e.GetType().Name}: a postura do estado depois do evento");
    }
}
