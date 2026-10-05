using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Agarrar onde o usuário solta (DEC-024, pedido do usuário): solto perto da borda de cima, agarra
/// o cipó; solto junto a uma lateral, fica grudado na parede; perto do chão, cai como antes. Posto
/// lá pelo usuário, só sai quando o usuário o tira: a agenda só o faz passear pela mesma superfície.
/// </summary>
internal static class AgarrarTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

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

    private static SimuladorDeTempo Novo(ulong semente = 3, Topologia? topologia = null)
        => new(MovimentoTestes.Fase4(), semente, topologia ?? TopologiasDeExemplo.UmMonitor);

    [Teste]
    public static void SoltoPertoDaBordaDeCima_AgarraOCipoParadoPresoESemRelogio()
    {
        SimuladorDeTempo sim = Novo();
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(900, sup.Teto + 60));
        sim.Esta(Estado.Hanging, "agarrou o cipó");
        Afirmar.Igual(new PontoPx(900, sup.Teto), sim.Estado.Lugar!.Ancora, "subiu até a borda de cima, onde o cipó se prende");
        Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado, "parado, agarrado");
        Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario, "posto lá pelo usuário");
        Afirmar.Falso(sim.RelogioLigado, "agarrado, sem relógio");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra.Contains("agarra o cipó", StringComparison.Ordinal)), "regra do cipó");
        MovimentoTestes.ConferirApoio(sim.Estado, "no cipó");
    }

    [Teste]
    public static void SoltoJuntoAUmaLateral_FicaGrudadoNaParedeOlhandoParaEla()
    {
        foreach (bool direita in new[] { true, false })
        {
            SimuladorDeTempo sim = Novo();
            Superficies sup = Sup(sim);
            int x = direita ? sup.Direita - 30 : sup.Esquerda + 30;
            ArrastarPara(sim, new PontoPx(x, 600));
            string lado = direita ? "direita" : "esquerda";
            sim.Esta(Estado.Climbing, $"{lado}: grudou na parede");
            Afirmar.Igual(new PontoPx(direita ? sup.Direita : sup.Esquerda, 600), sim.Estado.Lugar!.Ancora, $"{lado}: encostado na lateral, na altura em que foi solto");
            Afirmar.Igual(direita ? Direcao.Direita : Direcao.Esquerda, sim.Estado.Direcao, $"{lado}: olha para a parede");
            Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado && sim.Estado.PresoPeloUsuario, $"{lado}: parado e preso");
            Afirmar.Falso(sim.RelogioLigado, $"{lado}: sem relógio");
            MovimentoTestes.ConferirApoio(sim.Estado, $"na parede da {lado}");
        }
    }

    [Teste]
    public static void SoltoPertoDoChaoOuLongeDasBordas_CaiComoAntes()
    {
        (string Caso, Func<Superficies, PontoPx> Alvo)[] casos =
        [
            ("junto à lateral, mas a 20 DIP do chão", s => new PontoPx(s.Direita - 10, s.Chao - 20)),
            ("no meio da tela, longe da borda de cima", s => new PontoPx(900, s.Teto + 400)),
        ];
        foreach ((string caso, Func<Superficies, PontoPx> alvo) in casos)
        {
            SimuladorDeTempo sim = Novo();
            ArrastarPara(sim, alvo(Sup(sim)));
            sim.Esta(Estado.Falling, caso);
            Afirmar.Falso(sim.Estado.PresoPeloUsuario, $"{caso}: não fica preso");
        }
    }

    [Teste]
    public static void PresoNaParede_SoSaiQuandoOUsuarioTira()
    {
        SimuladorDeTempo sim = Novo(semente: 5);
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(sup.Direita - 20, 500));
        int x = sup.Direita;
        var alturas = new HashSet<int>();
        int paradas = 0;
        sim.AoAplicar = (antes, e, depois) =>
        {
            Afirmar.Igual(Estado.Climbing, depois.Estado, $"{e}: continua na parede");
            Afirmar.Igual(x, depois.Lugar!.Ancora.X, $"{e}: encostado na mesma lateral");
            Afirmar.Verdadeiro(depois.Lugar.Ancora.Y >= sup.Teto && depois.Lugar.Ancora.Y <= sup.Chao - 32, $"{e}: entre o topo e a folga do chão ({depois.Lugar.Ancora.Y})");
            alturas.Add(depois.Lugar.Ancora.Y);
            if (!antes.Movimento.Agarrado && depois.Movimento.Agarrado) paradas++;
            MovimentoTestes.ConferirApoio(depois, $"{e}");
        };
        sim.Avancar(TimeSpan.FromMinutes(10));
        Afirmar.Verdadeiro(alturas.Count > 10 && paradas >= 2, $"passeou pela parede e parou de novo ({alturas.Count} alturas, {paradas} paradas)");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra.Contains("passeia pela parede", StringComparison.Ordinal)), "a agenda o fez passear");

        // O usuário o tira de lá: no chão, volta a ser um macaquinho livre.
        sim.AoAplicar = null;
        ArrastarPara(sim, new PontoPx(900, sup.Chao));
        sim.Esta(Estado.Idle, "solto no chão");
        Afirmar.Falso(sim.Estado.PresoPeloUsuario, "não está mais preso");
    }

    [Teste]
    public static void PresoNoCipo_SoPasseiaPelaBordaDeCimaEDaMeiaVoltaNasQuinas()
    {
        SimuladorDeTempo sim = Novo(semente: 8);
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(sup.Esquerda + 150, sup.Teto + 30));
        var posicoes = new HashSet<int>();
        sim.AoAplicar = (_, e, depois) =>
        {
            Afirmar.Igual(Estado.Hanging, depois.Estado, $"{e}: continua no cipó");
            Afirmar.Igual(sup.Teto, depois.Lugar!.Ancora.Y, $"{e}: na borda de cima");
            posicoes.Add(depois.Lugar.Ancora.X);
            MovimentoTestes.ConferirApoio(depois, $"{e}");
        };
        sim.Avancar(TimeSpan.FromMinutes(15));
        Afirmar.Verdadeiro(posicoes.Count > 10, $"passeou pela borda ({posicoes.Count} posições)");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra.Contains("passeia pela borda", StringComparison.Ordinal)), "a agenda o fez passear");
    }

    [Teste]
    public static void Preso_CliqueNaoTiraEPausadoNaoSeMexe()
    {
        SimuladorDeTempo sim = Novo();
        Superficies sup = Sup(sim);
        ArrastarPara(sim, new PontoPx(1200, sup.Teto + 40));
        sim.Esta(Estado.Hanging);
        PontoPx noCipo = sim.Estado.Lugar!.Ancora;

        // Um clique: reage e volta ao cipó, ainda preso.
        sim.Aplicar(new Press(new PontoPx(noCipo.X, noCipo.Y - 40)));
        sim.Aplicar(new Click());
        sim.Esta(Estado.Reacting);
        sim.Avancar(TimeSpan.FromSeconds(2), s => s.Estado != Estado.Reacting);
        sim.Esta(Estado.Hanging, "depois do clique, de volta ao cipó");
        Afirmar.Igual(noCipo, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
        Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario, "o clique não o tira de lá");

        // Pausado: nada se move, sem relógio.
        sim.Aplicar(new CmdPauseAutonomy());
        sim.Avancar(TimeSpan.FromMinutes(5));
        sim.Esta(Estado.Hanging);
        Afirmar.Igual(noCipo, sim.Estado.Lugar!.Ancora, "pausado, fica parado");
        Afirmar.Falso(sim.RelogioLigado, "pausado e agarrado, sem relógio");
    }

    [Teste]
    public static void CliqueNaEscaladaAutonoma_VoltaAAgarrarEmVezDeCairENaoFicaPreso()
    {
        for (ulong semente = 1; semente <= 40; semente++)
        {
            var sim = new SimuladorDeTempo(MovimentoTestes.Fase4(AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.UmMonitor);
            sim.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Climbing && s.Lugar!.Ancora.Y < 700 && !s.Movimento.Foguete);
            if (sim.Estado.Estado != Estado.Climbing) continue;
            PontoPx naParede = sim.Estado.Lugar!.Ancora;
            sim.Aplicar(new Press(new PontoPx(naParede.X, naParede.Y - 40)));
            sim.Aplicar(new Click());
            sim.Avancar(TimeSpan.FromSeconds(2), s => s.Estado != Estado.Reacting);
            sim.Esta(Estado.Climbing, $"semente {semente}: depois do clique, agarrado de novo à parede");
            Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado, $"semente {semente}: parado");
            Afirmar.Falso(sim.Estado.PresoPeloUsuario, $"semente {semente}: não foi o usuário que o pôs lá");
            // A agenda volta a movê-lo: sobe, desce, salta ou se solta.
            sim.Avancar(TimeSpan.FromMinutes(1), s => !(s.Estado == Estado.Climbing && s.Movimento.Agarrado));
            Afirmar.Falso(sim.Estado.Estado == Estado.Climbing && sim.Estado.Movimento.Agarrado, $"semente {semente}: a agenda o tira da parada");
            return;
        }
        Afirmar.Falhar("nenhuma semente chegou a escalar abaixo de y = 700");
    }

    // DEC-022 com DEC-024: agarrado sem ter sido posto pelo usuário (depois da reação a um clique no meio de uma escalada),
    // com a autonomia pausada ele não fica esperando uma agenda que não vem: desce até o chão pela mesma parede, como quem
    // escala. Vale pausar depois do clique e clicar já pausado (a acomodação o agarra e a calma o faz descer).
    [Teste]
    public static void AgarradoSemEstarPreso_PausadoDesceAteOChao()
    {
        foreach (bool pausaAntes in new[] { false, true })
        {
            string caso = pausaAntes ? "clique já pausado" : "pausa depois do clique";
            bool achou = false;
            for (ulong semente = 1; semente <= 40 && !achou; semente++)
            {
                var sim = new SimuladorDeTempo(MovimentoTestes.Fase4(AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.UmMonitor);
                sim.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Climbing && s.Lugar!.Ancora.Y < 700 && !s.Movimento.Foguete);
                if (sim.Estado.Estado != Estado.Climbing) continue;
                achou = true;
                Superficies sup = Sup(sim);
                PontoPx naParede = sim.Estado.Lugar!.Ancora;
                if (pausaAntes)
                {
                    // Pausado no meio da subida, ele já começa a descer; o clique o pega no passo seguinte.
                    sim.Aplicar(new CmdPauseAutonomy());
                    sim.Passos(1);
                    naParede = sim.Estado.Lugar!.Ancora;
                }
                sim.Aplicar(new Press(new PontoPx(naParede.X, naParede.Y - 40)));
                sim.Aplicar(new Click());
                sim.Avancar(TimeSpan.FromSeconds(2), s => s.Estado != Estado.Reacting);
                sim.Esta(Estado.Climbing, $"{caso}: depois do clique, de volta à parede");
                Afirmar.Falso(sim.Estado.PresoPeloUsuario, $"{caso}: não foi o usuário que o pôs lá");
                if (!pausaAntes) sim.Aplicar(new CmdPauseAutonomy());
                sim.Avancar(TimeSpan.FromMinutes(1));
                sim.Esta(Estado.Idle, $"{caso}: pausado, desceu");
                Afirmar.Igual(new PontoPx(naParede.X, sup.Chao), sim.Estado.Lugar!.Ancora, $"{caso}: pela mesma parede, até o chão");
                Afirmar.Falso(sim.RelogioLigado, $"{caso}: parado no chão, sem relógio");
            }
            Afirmar.Verdadeiro(achou, $"{caso}: alguma semente chegou a escalar abaixo de y = 700");
        }
    }
}
