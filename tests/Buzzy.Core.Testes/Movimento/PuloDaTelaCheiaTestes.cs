using Buzzy.Core.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Movimento.MovimentoTestes;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// DEC-035 — o pulo da tela cheia (pedido do usuário de 2026-10-03: "ele ta literalmente teleportando [...] se desse pra
/// fazer ele pulando pro outro monitor e se agarrando num cipo"). Com a capacidade e o movimento ligados, a troca de monitor do
/// modo de tela cheia (DEC-013, DEC-034) é um arco: na ida, da posição dele ao cipó do monitor livre, perto da lateral voltada
/// para a partida; no fim da tela cheia, de volta à posição de antes dela. O sprite fica na união das áreas úteis em todos os
/// passos; sem um arco assim, a troca é direta, como antes. O relógio é virtual (<see cref="SimuladorDeTempo"/>); nada abre
/// janela.
/// </summary>
internal static class PuloDaTelaCheiaTestes
{
    /// <summary>A Fase 4 sem ações autônomas (ele só se move pela tela cheia), com o pulo ligado ou não.</summary>
    private static ConfiguracaoDoNucleo Cfg(bool pulo = true) => Fase4(AcoesAutonomas.Nenhuma) with { PuloDaTelaCheia = pulo };

    private static FullscreenTargetsChanged Ocupado(params string[] chaves) => new(new MonitoresOcupados(chaves));

    private static bool Pulando(EstadoDoNucleo s) => s.Estado == Estado.Jumping && s.Movimento.Travessia is { Pulo: not PuloDaTelaCheia.Nenhum };

    /// <summary>
    /// Um simulador com ele no principal: na posição inicial (85% da largura, no chão) ou na fração
    /// (<paramref name="fracaoX"/>, <paramref name="fracaoY"/>) da área útil, acomodado pela carga (no chão, na parede ou no cipó).
    /// </summary>
    private static SimuladorDeTempo NoPrincipal(Topologia? topologia = null, double? fracaoX = null, double fracaoY = 1)
    {
        topologia ??= TopologiasDeExemplo.SecundarioAEsquerda;
        PosicaoDoPersonagem? salva = fracaoX is { } fx
            ? Posicionador.Descrever(Posicionador.NoMonitor(topologia.PorChave(TopologiasDeExemplo.Display1)!, fx, fracaoY, Cfg().Tamanho))
            : null;
        var sim = new SimuladorDeTempo(Cfg(), 1, topologia, posicaoSalva: salva);
        Afirmar.Igual(TopologiasDeExemplo.Display1, sim.Estado.Lugar?.Monitor.Chave, "começa no principal");
        if (fracaoY >= 1) Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parado no chão");
        return sim;
    }

    /// <summary>
    /// Avança passo a passo enquanto ele pula, conferindo em cada passo o sprite na união das áreas úteis e que nenhum passo
    /// salta mais que <paramref name="passoMaximo"/> px (nada de teleporte), com o monitor do lugar sendo o da âncora; devolve
    /// as âncoras do voo.
    /// </summary>
    private static List<PontoPx> Voar(SimuladorDeTempo sim, string onde, int passoMaximo = 120)
    {
        var ancoras = new List<PontoPx> { sim.Estado.Lugar!.Ancora };
        sim.AoAplicar = (_, e, depois) =>
        {
            TravessiaTestes.ConferirApoioNaTravessia(depois, $"{onde}: {e}");
            if (depois.Lugar is not { } l) return;
            Afirmar.Igual(depois.Topologia!.MonitorMaisProximo(Posicionador.PixelDosPes(l.Ancora)).Chave, l.Monitor.Chave, $"{onde}: o monitor da âncora {l.Ancora} em {e}");
            PontoPx a = ancoras[^1];
            Afirmar.Verdadeiro(Math.Abs(l.Ancora.X - a.X) <= passoMaximo && Math.Abs(l.Ancora.Y - a.Y) <= passoMaximo,
                $"{onde}: passo de ({l.Ancora.X - a.X}, {l.Ancora.Y - a.Y}) px em {e}");
            ancoras.Add(l.Ancora);
        };
        sim.Avancar(TimeSpan.FromSeconds(3), s => !Pulando(s));
        sim.AoAplicar = null;
        Afirmar.Falso(Pulando(sim.Estado), $"{onde}: o pulo terminou");
        return ancoras;
    }

    // A ida: o principal fica ocupado, e ele pula do chão ao cipó do monitor da esquerda, a 200 DIP da lateral direita dele,
    // pendurado e agarrado; o arco só sobe (o ponto mais alto é a chegada), a âncora troca de monitor uma vez só, e o voo dura
    // entre o tempo mínimo e o máximo. A posição de antes fica guardada só como retorno.
    [Teste]
    public static void Ida_PulaDoChaoAoCipoDoMonitorLivre()
    {
        SimuladorDeTempo sim = NoPrincipal();
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(Pulando(sim.Estado), "pulando");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pula para o cipó do monitor livre"), "a regra do pulo");
        Afirmar.Falso(sim.Transicoes.Any(t => t.Regra.Contains("transfere para o monitor livre", StringComparison.Ordinal)), "sem a troca direta");
        Afirmar.Igual(Direcao.Esquerda, sim.Estado.Direcao, "virado para o lado do pulo");
        Afirmar.Igual(antes, sim.Estado.RetornoDaTelaCheia, "a posição de antes, guardada");

        double inicio = sim.AgoraMs;
        List<PontoPx> voo = Voar(sim, "ida");
        double duracao = (sim.AgoraMs - inicio) / 1000;
        Afirmar.Verdadeiro(duracao is >= 0.6 and <= 1.25, $"o voo dura {duracao:0.00} s");
        for (int i = 1; i < voo.Count; i++) Afirmar.Verdadeiro(voo[i].Y <= voo[i - 1].Y, $"o arco só sobe ({voo[i - 1].Y} → {voo[i].Y})");

        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Superficies sup = Superficies.Do(sim.Estado.Topologia!, l.Monitor, l.Tamanho);
        Afirmar.Igual(Estado.Hanging, sim.Estado.Estado, "pendurado no cipó");
        Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado, "agarrado");
        Afirmar.Igual((TopologiasDeExemplo.Display2, sup.Direita - 200, sup.Teto), (l.Monitor.Chave, l.Ancora.X, l.Ancora.Y), "no cipó, a 200 DIP da lateral");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "JUMPING: fim do pulo da tela cheia, agarra o cipó do monitor livre"), "a regra da chegada");
        Afirmar.Igual(antes, sim.Estado.RetornoDaTelaCheia, "o retorno continua guardado");
    }

    // Com o livre à direita, a borda entre os dois em x = 1920: do meio do principal, a âncora troca de monitor nela, e ele
    // agarra o cipó a 200 DIP da lateral esquerda do livre, virado para a direita.
    [Teste]
    public static void Ida_ParaADireita()
    {
        SimuladorDeTempo sim = NoPrincipal(TopologiasDeExemplo.LadoALado, fracaoX: 0.5);
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Igual(Direcao.Direita, sim.Estado.Direcao, "virado para a direita");
        Voar(sim, "ida para a direita");
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Superficies sup = Superficies.Do(sim.Estado.Topologia!, l.Monitor, l.Tamanho);
        Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2, sup.Esquerda + 200, sup.Teto), (sim.Estado.Estado, l.Monitor.Chave, l.Ancora.X, l.Ancora.Y), "no cipó do livre");
    }

    // A volta: a tela cheia acaba, e ele pula do cipó de volta ao lugar de antes, no chão do principal; o arco só desce (o ponto
    // mais alto é a partida). No fim, o retorno acaba.
    [Teste]
    public static void Volta_PulaDoCipoDeVoltaAoLugarDeAntes()
    {
        SimuladorDeTempo sim = NoPrincipal();
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Voar(sim, "ida");
        sim.Aplicar(Ocupado());
        Afirmar.Verdadeiro(sim.Estado.Movimento.Travessia is { Pulo: PuloDaTelaCheia.Volta }, "pulando de volta");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED(vazio): pula de volta à posição anterior"), "a regra da volta");
        Afirmar.Igual(Direcao.Direita, sim.Estado.Direcao, "virado para o lado da volta");

        List<PontoPx> voo = Voar(sim, "volta");
        for (int i = 1; i < voo.Count; i++) Afirmar.Verdadeiro(voo[i].Y >= voo[i - 1].Y, $"o arco só desce ({voo[i - 1].Y} → {voo[i].Y})");
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parado no chão");
        Afirmar.Igual(antes.AncoraAbsoluta, sim.Estado.Lugar!.Ancora, "no lugar de antes");
        Afirmar.Igual(TopologiasDeExemplo.Display1, sim.Estado.Lugar.Monitor.Chave, "no principal");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "o retorno acabou");
    }

    // Do cipó de um monitor ao do outro (ele estava pendurado quando a tela cheia começou), o arco não cabe por cima, e ele
    // balança: desce abaixo da reta e sobe de novo até o cipó.
    [Teste]
    public static void DeCipoACipo_Balanca()
    {
        Topologia t = TopologiasDeExemplo.SecundarioAEsquerda;
        MonitorDoDesktop principal = t.PorChave(TopologiasDeExemplo.Display1)!, outro = t.PorChave(TopologiasDeExemplo.Display2)!;
        var cfg = new ParametrosDeMovimento();
        TamanhoDip sprite = new(128, 128);
        Superficies daqui = Superficies.Do(t, principal, sprite.ParaPixels(96)), dela = Superficies.Do(t, outro, sprite.ParaPixels(96));
        Travessia balanco = Afirmar.NaoNulo(Passagens.PlanejarPuloDaTelaCheia(t, principal, 600, daqui.Teto, outro, dela.Direita - 200, dela.Teto, sprite, cfg, 60, PuloDaTelaCheia.Ida), "o balanço");
        Afirmar.Verdadeiro(balanco.G < 0, $"desce abaixo da reta (G = {balanco.G:0})");
        (double _, double meio) = Passagens.PosicaoNoSalto(balanco, balanco.PassosTotais / 2, 60);
        Afirmar.Verdadeiro(meio > daqui.Teto + 100, $"no meio, {meio - daqui.Teto:0} px abaixo do cipó");

        // Do chão ao cipó, a chegada é o ponto mais alto: a velocidade vertical zera nela.
        Travessia subida = Afirmar.NaoNulo(Passagens.PlanejarPuloDaTelaCheia(t, principal, 600, daqui.Chao, outro, dela.Direita - 200, dela.Teto, sprite, cfg, 60, PuloDaTelaCheia.Ida), "a subida");
        double tempo = (double)subida.PassosTotais / 60;
        Afirmar.Verdadeiro(Math.Abs(subida.VY0 + subida.G * tempo) < 1e-6, "chega ao cipó parando de subir");
    }

    // A duração vai pela distância, entre o tempo mínimo (0,6 s) e o máximo (1,2 s), em passos inteiros.
    [Teste]
    public static void Duracao_EntreOMinimoEOMaximo()
    {
        Topologia t = TopologiasDeExemplo.LadoALado;
        MonitorDoDesktop um = t.PorChave(TopologiasDeExemplo.Display1)!, dois = t.PorChave(TopologiasDeExemplo.Display2)!;
        var cfg = new ParametrosDeMovimento();
        TamanhoDip sprite = new(128, 128);
        int Passos(double x0, MonitorDoDesktop destino, double x1)
            => Afirmar.NaoNulo(Passagens.PlanejarPuloDaTelaCheia(t, um, x0, 1032, destino, x1, 1032, sprite, cfg, 60, PuloDaTelaCheia.Volta), $"de {x0} a {x1}").PassosTotais;
        Afirmar.Igual(36, Passos(900, um, 1000), "curto: o mínimo");
        Afirmar.Igual(72, Passos(100, dois, 3700), "de ponta a ponta: o máximo");
        Afirmar.Igual(60, Passos(400, dois, 2800), "2400 px a 2400 DIP/s: 1 s");
    }

    // Sem um arco em que o sprite fique na união das áreas úteis (um vão entre os monitores), a troca é direta, como antes.
    // Com a capacidade desligada, também.
    [Teste]
    public static void SemArcoQueCaibaOuComOPuloDesligado_TrocaDireta()
    {
        SimuladorDeTempo vao = NoPrincipal(TopologiasDeExemplo.VaoEntreMonitores);
        vao.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Falso(Pulando(vao.Estado), "com o vão, não pula");
        Afirmar.Verdadeiro(vao.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: transfere para o monitor livre"), "troca direta");
        Afirmar.Igual(TopologiasDeExemplo.Display2, vao.Estado.Lugar?.Monitor.Chave, "no monitor livre");

        var desligado = new SimuladorDeTempo(Cfg(pulo: false), 1, TopologiasDeExemplo.SecundarioAEsquerda);
        desligado.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Falso(Pulando(desligado.Estado), "com o pulo desligado, não pula");
        Afirmar.Igual(TopologiasDeExemplo.Display2, desligado.Estado.Lugar?.Monitor.Chave, "no monitor livre");
    }

    // A tela cheia acaba no meio da ida: ele volta dali mesmo, num pulo, ao lugar de antes.
    [Teste]
    public static void TelaCheiaAcabaNoMeioDaIda_VoltaDali()
    {
        SimuladorDeTempo sim = NoPrincipal();
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        sim.Passos(15);
        Afirmar.Verdadeiro(sim.Estado.Movimento.Travessia is { Pulo: PuloDaTelaCheia.Ida }, "no meio da ida");
        sim.Aplicar(Ocupado());
        Afirmar.Verdadeiro(sim.Estado.Movimento.Travessia is { Pulo: PuloDaTelaCheia.Volta }, "volta dali");
        Voar(sim, "volta");
        Afirmar.Igual(antes.AncoraAbsoluta, sim.Estado.Lugar!.Ancora, "no lugar de antes");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "o retorno acabou");
    }

    // A volta é interrompida pela tela cheia de novo no destino (o pingue-pongue entre monitores): ele sai dali para o monitor
    // livre, com o mesmo retorno. E, com os dois ocupados no meio da ida, ele some até a tela cheia acabar, e reaparece no lugar
    // de antes.
    [Teste]
    public static void DestinoOcupadoNoMeioDoPulo()
    {
        SimuladorDeTempo sim = NoPrincipal();
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Voar(sim, "ida");
        sim.Aplicar(Ocupado());
        sim.Passos(2);
        Afirmar.Igual(TopologiasDeExemplo.Display2, sim.Estado.Lugar?.Monitor.Chave, "a volta, ainda sobre o livre");
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(sim.Estado.Movimento.Travessia is { Pulo: PuloDaTelaCheia.Ida, ChaveDestino: TopologiasDeExemplo.Display2 }, "sai de novo para o livre");
        Afirmar.Igual(antes, sim.Estado.RetornoDaTelaCheia, "com o mesmo retorno");
        Voar(sim, "ida de novo");
        Afirmar.Igual(TopologiasDeExemplo.Display2, sim.Estado.Lugar?.Monitor.Chave, "no livre");

        SimuladorDeTempo dois = NoPrincipal();
        dois.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        dois.Passos(10);
        dois.Aplicar(Ocupado(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Display2));
        Afirmar.Igual((Estado.Hidden, MotivoDoOcultamento.PorTelaCheia), (dois.Estado.Estado, dois.Estado.Motivo), "sem monitor livre, some");
        dois.Aplicar(Ocupado());
        Afirmar.Igual(antes.AncoraAbsoluta, dois.Estado.Lugar!.Ancora, "reaparece no lugar de antes");
    }

    // Esconder ou sair no meio do pulo dá o pulo por terminado: a gravação leva a posição escolhida pelo usuário (na ida, a de
    // antes da tela cheia; na volta, também), nunca um ponto no ar. Escondido no meio da ida, ele reaparece no cipó do livre.
    [Teste]
    public static void EsconderOuSairNoMeioDoPulo_GravaOLugarDeAntes()
    {
        foreach (bool naVolta in new[] { false, true })
        foreach (Evento evento in new Evento[] { new CmdHide(), new CmdExit() })
        {
            string onde = $"{(naVolta ? "volta" : "ida")}, {evento}";
            SimuladorDeTempo sim = NoPrincipal();
            PosicaoDoPersonagem antes = sim.Estado.Posicao!;
            sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
            if (naVolta)
            {
                Voar(sim, onde);
                sim.Aplicar(Ocupado());
            }
            sim.Passos(10);
            Afirmar.Verdadeiro(Pulando(sim.Estado), $"{onde}: no meio do pulo");
            var gravadas = new List<PosicaoDoPersonagem>();
            sim.AoResultado = (_, _, r) => gravadas.AddRange(r.Efeitos.OfType<GravarPosicao>().Select(g => g.Posicao));
            sim.Aplicar(evento);
            Afirmar.Igual(1, gravadas.Count, $"{onde}: uma gravação");
            Afirmar.Igual(antes.AncoraAbsoluta, gravadas[0].AncoraAbsoluta, $"{onde}: grava o lugar de antes");
            // Na volta, o pulo vale como terminado: o lugar de antes passa a ser a posição, sem retorno sobrando; na ida, o retorno
            // continua guardado para o fim da tela cheia.
            if (naVolta) Afirmar.Igual((antes.AncoraAbsoluta, (PosicaoDoPersonagem?)null), (sim.Estado.Posicao!.AncoraAbsoluta, sim.Estado.RetornoDaTelaCheia), $"{onde}: sem retorno sobrando");
            else Afirmar.Igual(antes, sim.Estado.RetornoDaTelaCheia, $"{onde}: o retorno continua");
            if (evento is CmdHide && !naVolta)
            {
                sim.Aplicar(new CmdShow());
                Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2), (sim.Estado.Estado, sim.Estado.Lugar?.Monitor.Chave), $"{onde}: reaparece no cipó do livre");
            }
        }
    }

    // Pegar no ar a volta: o usuário assume, e o retorno acaba (o fim da tela cheia não o move mais). Uma mudança de topologia
    // no meio da volta a termina no lugar de antes.
    [Teste]
    public static void PegarNoArOuTopologiaNaVolta()
    {
        SimuladorDeTempo sim = NoPrincipal();
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Voar(sim, "ida");
        sim.Aplicar(Ocupado());
        sim.Passos(10);
        sim.Aplicar(new Press(sim.Estado.Lugar!.Ancora));
        Afirmar.Igual(Estado.Pressed, sim.Estado.Estado, "pegou no ar");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "o retorno acabou");

        SimuladorDeTempo topo = NoPrincipal();
        PosicaoDoPersonagem antes = topo.Estado.Posicao!;
        topo.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Voar(topo, "ida");
        topo.Aplicar(Ocupado());
        topo.Passos(10);
        Topologia transladada = new([.. TopologiasDeExemplo.SecundarioAEsquerda.Monitores.Select(m => m with { AreaUtil = m.AreaUtil with { Base = m.AreaUtil.Base - 8 } })]);
        topo.Aplicar(new TopologyChanged(transladada));
        Afirmar.Verdadeiro(topo.Transicoes.Any(t => t.Regra == "TOPOLOGY_CHANGED: pulo da tela cheia interrompido, de volta à posição anterior"), "a regra");
        Afirmar.Igual(TopologiasDeExemplo.Display1, topo.Estado.Lugar?.Monitor.Chave, "no principal");
        Afirmar.Nulo(topo.Estado.RetornoDaTelaCheia, "o retorno acabou");
        Afirmar.Igual(antes.FracaoX, topo.Estado.Posicao!.FracaoX, "na mesma posição relativa de antes");
    }

    // Uma mudança de topologia no meio da ida, ainda por cima do monitor ocupado, desfaz o pulo, como a travessia; ali ele
    // sai de novo para o livre, num pulo.
    [Teste]
    public static void TopologiaNoMeioDaIda_SaiDeNovoDoMonitorOcupado()
    {
        SimuladorDeTempo sim = NoPrincipal();
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        sim.Passos(5);
        Afirmar.Igual(TopologiasDeExemplo.Display1, sim.Estado.Lugar?.Monitor.Chave, "ainda por cima do ocupado");
        Topologia mudada = new([.. TopologiasDeExemplo.SecundarioAEsquerda.Monitores.Select(m => m with { AreaUtil = m.AreaUtil with { Base = m.AreaUtil.Base - 8 } })]);
        sim.Aplicar(new TopologyChanged(mudada));
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "TOPOLOGY_CHANGED: pulo da tela cheia interrompido sobre o monitor ocupado: pula para o cipó do monitor livre"), "sai de novo");
        Voar(sim, "ida de novo");
        Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2), (sim.Estado.Estado, sim.Estado.Lugar?.Monitor.Chave), "no cipó do livre");
    }

    // Desligar o modo pelo menu com ele no cipó do livre: volta num pulo ao lugar de antes.
    [Teste]
    public static void ModoDesligadoPeloMenu_VoltaNumPulo()
    {
        SimuladorDeTempo sim = NoPrincipal();
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Voar(sim, "ida");
        sim.Aplicar(new CmdSetFullscreenMode(false));
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "SETTINGS_CHANGED: modo de tela cheia desligado, pula de volta"), "a regra");
        Voar(sim, "volta");
        Afirmar.Igual(antes.AncoraAbsoluta, sim.Estado.Lugar!.Ancora, "no lugar de antes");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "o retorno acabou");
    }

    // ---------------------------------------------------------------- o pulinho (item 6)

    // Perto da borda que encosta no livre (na posição inicial, a 224 px dela), só um pulinho baixo e curto para o outro lado:
    // pousa no chão do livre, a 96 DIP da borda; no fim da tela cheia, das duas pontas perto da mesma borda, outro pulinho de
    // volta.
    [Teste]
    public static void NaBordaDaPorta_DaUmPulinho_EVoltaNoutro()
    {
        SimuladorDeTempo sim = NoPrincipal(TopologiasDeExemplo.LadoALado);
        PosicaoDoPersonagem antes = sim.Estado.Posicao!;
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pulinho para o monitor livre"), "a regra do pulinho");
        double inicio = sim.AgoraMs;
        List<PontoPx> voo = Voar(sim, "pulinho");
        double duracao = (sim.AgoraMs - inicio) / 1000;
        Afirmar.Verdadeiro(duracao is >= 0.35 and < 0.6, $"curto: {duracao:0.00} s");
        int topo = voo.Min(a => a.Y);
        Afirmar.Verdadeiro(topo >= 1032 - 72, $"baixo: subiu {1032 - topo} px");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "JUMPING: fim do pulinho da tela cheia, no chão do monitor livre"), "pousa");
        Afirmar.Igual(Estado.Landing, sim.Estado.Estado, "pousando");
        Afirmar.Igual((TopologiasDeExemplo.Display2, 1920 + 64 + 96, 1032), (sim.Estado.Lugar!.Monitor.Chave, sim.Estado.Lugar.Ancora.X, sim.Estado.Lugar.Ancora.Y), "no chão do livre, a 96 DIP da borda");
        sim.Avancar(TimeSpan.FromSeconds(1), s => s.Estado == Estado.Idle);
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parado depois do pouso");

        sim.Aplicar(Ocupado());
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED(vazio): pula de volta à posição anterior (pulinho)"), "volta noutro pulinho");
        Voar(sim, "pulinho de volta");
        Afirmar.Igual(antes.AncoraAbsoluta, sim.Estado.Lugar!.Ancora, "no lugar de antes");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "o retorno acabou");
    }

    // O pulinho na mesma superfície: na parede da borda, ele passa para a parede do livre, na mesma altura; no cipó perto da
    // borda, para o cipó do livre.
    [Teste]
    public static void PulinhoNaParedeENoCipo_MesmaSuperficieDoOutroLado()
    {
        SimuladorDeTempo parede = NoPrincipal(TopologiasDeExemplo.LadoALado, fracaoX: 1, fracaoY: 0.5);
        Afirmar.Igual(Estado.Climbing, parede.Estado.Estado, "na parede da borda");
        int altura = parede.Estado.Lugar!.Ancora.Y;
        parede.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(parede.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pulinho para o monitor livre"), "pulinho da parede");
        Voar(parede, "pulinho da parede");
        Afirmar.Igual((Estado.Climbing, TopologiasDeExemplo.Display2, 1920 + 64, altura), (parede.Estado.Estado, parede.Estado.Lugar!.Monitor.Chave, parede.Estado.Lugar.Ancora.X, parede.Estado.Lugar.Ancora.Y), "na parede do livre, na mesma altura");

        SimuladorDeTempo cipo = NoPrincipal(TopologiasDeExemplo.LadoALado, fracaoX: 0.95, fracaoY: 0);
        Afirmar.Igual(Estado.Hanging, cipo.Estado.Estado, "no cipó, perto da borda");
        cipo.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(cipo.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pulinho para o monitor livre"), "pulinho do cipó");
        Voar(cipo, "pulinho do cipó");
        Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2, 1920 + 64 + 96), (cipo.Estado.Estado, cipo.Estado.Lugar!.Monitor.Chave, cipo.Estado.Lugar.Ancora.X), "no cipó do livre");
    }

    // Perto da beirada sem monitor do lado (a direita do principal, com o livre à esquerda), o pulo grande até o cipó; e no
    // meio, também.
    [Teste]
    public static void NaBeiradaSemVizinhoOuNoMeio_PulaProCipo()
    {
        foreach (double fracao in new[] { 1.0, 0.5 })
        {
            SimuladorDeTempo sim = NoPrincipal(fracaoX: fracao);
            sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
            Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pula para o cipó do monitor livre"), $"em {fracao}: o pulo ao cipó");
            Voar(sim, $"em {fracao}");
            Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2), (sim.Estado.Estado, sim.Estado.Lugar?.Monitor.Chave), $"em {fracao}: no cipó do livre");
        }
    }

    // O livre sem barra de tarefas tem o chão 48 px mais baixo: o pulinho do chão pousa no chão dele, não na altura de onde
    // saiu. Num degrau de 400 px, o pulinho baixo não passa pela quina, e ele dá o pulo grande até o cipó.
    [Teste]
    public static void ChaoDiferente_PulinhoPousaNoChaoDoLivre_DegrauGrandeVaiAoCipo()
    {
        Topologia semBarra = new([
            TopologiasDeExemplo.Principal(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Ret(0, 0, 1920, 1080), TopologiasDeExemplo.Ret(0, 0, 1920, 1032), 96),
            TopologiasDeExemplo.Secundario(TopologiasDeExemplo.Display2, TopologiasDeExemplo.Ret(1920, 0, 3840, 1080), TopologiasDeExemplo.Ret(1920, 0, 3840, 1080), 96),
        ]);
        SimuladorDeTempo sim = NoPrincipal(semBarra);
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pulinho para o monitor livre"), "pulinho");
        Voar(sim, "pulinho sem barra");
        Afirmar.Igual((Estado.Landing, TopologiasDeExemplo.Display2, 1080), (sim.Estado.Estado, sim.Estado.Lugar!.Monitor.Chave, sim.Estado.Lugar.Ancora.Y), "no chão do livre");

        SimuladorDeTempo degrau = NoPrincipal(TopologiasDeExemplo.DegrauDesalinhado);
        degrau.Aplicar(Ocupado(TopologiasDeExemplo.Display1));
        Afirmar.Verdadeiro(degrau.Transicoes.Any(t => t.Regra == "FULLSCREEN_TARGETS_CHANGED: pula para o cipó do monitor livre"), "o pulo grande");
        Voar(degrau, "degrau");
        Afirmar.Igual((Estado.Hanging, TopologiasDeExemplo.Display2), (degrau.Estado.Estado, degrau.Estado.Lugar?.Monitor.Chave), "no cipó do livre");
    }
}
