using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Esconderijo (DEC-025, pedido do usuário): o clique duplo esconde o personagem atrás da borda mais
/// próxima (a de baixo, que é a barra de tarefas, ou uma lateral), só com a cabeça e as mãos para
/// fora; outro clique duplo o tira de lá. Escondido, nada autônomo o move: só troca a cara.
/// </summary>
internal static class EsconderijoTestes
{
    private static readonly ConfiguracaoDoNucleo DoApp = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    private static Superficies Sup(SimuladorDeTempo sim)
    {
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        return Superficies.Do(Afirmar.NaoNulo(sim.Estado.Topologia, "topologia"), l.Monitor, l.Tamanho);
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

    private static void ArrastarPara(SimuladorDeTempo sim, PontoPx alvo)
    {
        PontoPx a = sim.Estado.Lugar!.Ancora;
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new DragStart());
        var cursor = new PontoPx(alvo.X, alvo.Y - 30);
        sim.Aplicar(new DragMove(cursor));
        sim.Aplicar(new DragEnd(cursor));
    }

    [Teste]
    public static void NoChao_CliqueDuploEscondeAtrasDaBordaDeBaixoEOutroTiraDeLa()
    {
        var sim = new SimuladorDeTempo(DoApp, 4, TopologiasDeExemplo.UmMonitor);
        PontoPx noChao = sim.Estado.Lugar!.Ancora;
        CliqueDuplo(sim);
        sim.Esta(Estado.Peeking, "escondido");
        Afirmar.Igual(LadoDoEsconderijo.Baixo, sim.Estado.Esconderijo, "atrás da borda de baixo, a barra de tarefas");
        Afirmar.Igual(noChao, sim.Estado.Lugar!.Ancora, "no mesmo lugar: só a pose esconde o corpo");
        Afirmar.Falso(sim.RelogioLigado, "escondido, sem relógio");

        // Dez minutos com a agenda ligada: nada o tira de lá; ele só troca a cara.
        var caras = new HashSet<Expressao>();
        sim.AoAplicar = (_, e, depois) =>
        {
            Afirmar.Igual(Estado.Peeking, depois.Estado, $"{e}: continua escondido");
            Afirmar.Igual(noChao, depois.Lugar!.Ancora, $"{e}: não se move");
            caras.Add(depois.Expressao);
        };
        sim.Avancar(TimeSpan.FromMinutes(10));
        sim.AoAplicar = null;
        Afirmar.Verdadeiro(caras.Count >= 3, $"espiou com várias caras ({string.Join(", ", caras)})");

        CliqueDuplo(sim);
        sim.Esta(Estado.Idle, "saiu do esconderijo, de pé no chão");
        Afirmar.Igual(LadoDoEsconderijo.Nenhum, sim.Estado.Esconderijo);
        Afirmar.Igual(noChao, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
    }

    [Teste]
    public static void NaParede_CliqueDuploEscondeAtrasDaLateralEVoltaAGrudarNela()
    {
        foreach (bool direita in new[] { true, false })
        {
            var sim = new SimuladorDeTempo(DoApp, 6, TopologiasDeExemplo.UmMonitor);
            Superficies sup = Sup(sim);
            ArrastarPara(sim, new PontoPx(direita ? sup.Direita - 20 : sup.Esquerda + 20, 600));
            sim.Esta(Estado.Climbing, "grudado na parede");
            string lado = direita ? "direita" : "esquerda";

            CliqueDuplo(sim);
            sim.Esta(Estado.Peeking, $"{lado}: escondido");
            Afirmar.Igual(direita ? LadoDoEsconderijo.Direita : LadoDoEsconderijo.Esquerda, sim.Estado.Esconderijo, $"{lado}: atrás da lateral");
            Afirmar.Igual(new PontoPx(direita ? sup.Direita : sup.Esquerda, 600), sim.Estado.Lugar!.Ancora, $"{lado}: encostado nela, na mesma altura");

            CliqueDuplo(sim);
            sim.Esta(Estado.Climbing, $"{lado}: de volta à parede");
            Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado && sim.Estado.PresoPeloUsuario, $"{lado}: grudado e preso, como o usuário deixou");
        }
    }

    // No cipó (pedido do usuário de 2026-10-03: "queria que desse pra esconder ele em cima tambem, quando ele esta no cipo,
    // dando 2 clicks"): o clique duplo o esconde atrás da borda de cima, no mesmo lugar, e não na barra de tarefas; dez
    // minutos depois, continua lá, só trocando de cara; outro clique duplo o devolve ao cipó, agarrado e preso. Gravado
    // escondido em cima, ele reabre lá.
    [Teste]
    public static void NoCipo_CliqueDuploEscondeAtrasDaBordaDeCimaEVoltaAoCipo()
    {
        var sim = new SimuladorDeTempo(DoApp, 6, TopologiasDeExemplo.UmMonitor);
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(960, sup.Teto + 10));
        sim.Esta(Estado.Hanging, "no cipó");
        PontoPx noCipo = sim.Estado.Lugar!.Ancora;
        Afirmar.Igual(new PontoPx(960, sup.Teto), noCipo, "pendurado no meio da borda de cima");

        CliqueDuplo(sim);
        sim.Esta(Estado.Peeking, "escondido");
        Afirmar.Igual(LadoDoEsconderijo.Cima, sim.Estado.Esconderijo, "atrás da borda de cima");
        Afirmar.Igual(noCipo, sim.Estado.Lugar!.Ancora, "no mesmo lugar, com o topo do sprite na borda");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "DOUBLE_CLICK: esconde-se atrás da borda (Cima)"), "a regra");

        sim.AoAplicar = (_, e, depois) =>
        {
            Afirmar.Igual(Estado.Peeking, depois.Estado, $"{e}: continua escondido");
            Afirmar.Igual(noCipo, depois.Lugar!.Ancora, $"{e}: não se move");
        };
        sim.Avancar(TimeSpan.FromMinutes(10));
        sim.AoAplicar = null;

        var gravadas = new List<GravarPosicao>();
        sim.AoResultado = (_, _, r) => gravadas.AddRange(r.Efeitos.OfType<GravarPosicao>());
        sim.Aplicar(new CmdExit());
        GravarPosicao gravada = Afirmar.NaoNulo(gravadas.SingleOrDefault(), "uma gravação na saída");
        Afirmar.Igual(LadoDoEsconderijo.Cima, gravada.Esconderijo, "grava a borda de cima");
        var reaberto = new SimuladorDeTempo(DoApp, 6, new Loaded(TopologiasDeExemplo.UmMonitor, gravada.Posicao, Preferencias.Padrao) { Esconderijo = gravada.Esconderijo, PresoPeloUsuario = gravada.PresoPeloUsuario });
        reaberto.Esta(Estado.Peeking, "reabre escondido");
        Afirmar.Igual((LadoDoEsconderijo.Cima, noCipo), (reaberto.Estado.Esconderijo, reaberto.Estado.Lugar!.Ancora), "em cima, no mesmo lugar");

        CliqueDuplo(reaberto);
        reaberto.Esta(Estado.Hanging, "de volta ao cipó");
        Afirmar.Igual(noCipo, reaberto.Estado.Lugar!.Ancora, "no mesmo lugar");
        Afirmar.Verdadeiro(reaberto.Estado.Movimento.Agarrado && reaberto.Estado.PresoPeloUsuario, "agarrado e preso, como o usuário deixou");
    }

    [Teste]
    public static void Escondido_CliqueSimplesReageEContinuaEscondido()
    {
        var sim = new SimuladorDeTempo(DoApp, 4, TopologiasDeExemplo.UmMonitor);
        CliqueDuplo(sim);
        PontoPx escondido = sim.Estado.Lugar!.Ancora;
        sim.Avancar(TimeSpan.FromSeconds(2));
        sim.Aplicar(new Press(new PontoPx(escondido.X, escondido.Y - 20)));
        sim.Aplicar(new Click());
        sim.Esta(Estado.Reacting, "reage ao clique");
        sim.Avancar(TimeSpan.FromSeconds(2), s => s.Estado != Estado.Reacting);
        sim.Esta(Estado.Peeking, "e volta a espiar");
        Afirmar.Igual(LadoDoEsconderijo.Baixo, sim.Estado.Esconderijo);
        Afirmar.Igual(escondido, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
    }

    [Teste]
    public static void Escondido_ArrastarTiraDoEsconderijo_EEsconderEMostrarPelaBandejaNaoTira()
    {
        var sim = new SimuladorDeTempo(DoApp, 4, TopologiasDeExemplo.UmMonitor);
        CliqueDuplo(sim);

        // Esconder e mostrar pela bandeja: volta ao esconderijo.
        sim.Aplicar(new CmdHide());
        sim.Esta(Estado.Hidden);
        sim.Aplicar(new CmdShow());
        sim.Esta(Estado.Peeking, "mostrado de novo, continua escondido na borda");
        Afirmar.Igual(LadoDoEsconderijo.Baixo, sim.Estado.Esconderijo);

        // Arrastar: sai do esconderijo e fica onde foi solto.
        ArrastarPara(sim, new PontoPx(700, Sup(sim).Chao));
        sim.Esta(Estado.Idle, "arrastado para o chão, fica de pé");
        Afirmar.Igual(LadoDoEsconderijo.Nenhum, sim.Estado.Esconderijo, "o arraste tirou do esconderijo");
    }

    [Teste]
    public static void SemOModo_CliqueDuploContinuaComoNaFase2()
    {
        var sim = new SimuladorDeTempo(DoApp with { EsconderijoNoCliqueDuplo = false, PainelDeEnergiaDisponivel = false }, 4, TopologiasDeExemplo.UmMonitor);
        CliqueDuplo(sim);
        sim.Esta(Estado.Idle, "sem o modo, o clique duplo só reage");
        Afirmar.Igual(Expressao.Rindo, sim.Estado.Expressao);
        Afirmar.Igual(LadoDoEsconderijo.Nenhum, sim.Estado.Esconderijo);
    }
}
