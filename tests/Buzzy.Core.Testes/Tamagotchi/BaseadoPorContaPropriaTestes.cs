using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10: "uma funcionalidade que o macaco fume maconha à
/// vontade quando ele quiser"; decisão do coordenador, adendo da DEC-028), de desenho animado. É uma ação autônoma nova,
/// <see cref="AcoesAutonomas.FumarBaseado"/>, no fim do enum, fora de <see cref="AcoesAutonomas.Todas"/>, que o aplicativo
/// liga e que só existe com a chave do tamagotchi ligada. Só em IDLE no chão, sem estar escondido, com a autonomia livre e
/// sem item na mão do usuário; o peso é zero com a onda Chapado ou a paranoia na frente, para ele não emendar. Ele "tira do
/// chapéu" o baseado, sem item no mundo, e o usa no chão com o uso do baseado da tabela (fumar, 210 passos), pelo mesmo
/// caminho do baseado que o usuário solta nele: a onda Chapado 2 pela combinação de sempre, o alívio, a carga e o sorteio da
/// paranoia. Não é droga sintética: sozinho, ou com álcool, cigarro e cogumelo, nunca sorteia; num episódio com sintética
/// que ainda não sorteou, fecha a mistura e faz o sorteio único dele; e, como conta na carga, uma bala dada com ele chapado
/// do baseado dele também fecha a mistura. O clique o interrompe, como todo uso. O peso é o mesmo
/// nos três níveis, calibrado por simulação do núcleo para cerca de um baseado a cada 4 minutos de tempo elegível na energia
/// Média. O esperado vem da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class BaseadoPorContaPropriaTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    /// <summary>A chance que sempre sai (1 em 1); a que nunca sai (0 em 1) é a de <see cref="ApoioDosItens.NuncaParanoia"/>.</summary>
    private static readonly Chance Sempre = new(1, 1);

    /// <summary>O uso do baseado da tabela, como a decisão o descreve: fumar, 210 passos, no chão.</summary>
    private static readonly Uso UsoDoBaseado = new(Item.Baseado, VerboDeUso.Fumar, 210, ApoioDoUso.Chao);

    /// <summary>A onda que o baseado começa sem onda na frente: Chapado, na subida do nível 2.</summary>
    private static readonly EstadoDaOnda ChapadoNaSubida2 = new(Onda.Chapado, FaseDaOnda.Subida, 2, 2);

    private const string RegraDoBaseado = "IDLE + AUTONOMY_TIMER: Fumar Baseado por conta própria";

    /// <summary>A configuração com só o baseado por conta própria na agenda: toda decisão em que ele pode fumar é fumar.</summary>
    private static ConfiguracaoDoNucleo SoOBaseado(ConfiguracaoDoNucleo cfg) => cfg with { Acoes = AcoesAutonomas.FumarBaseado };

    /// <summary>Invoca o item, deixa cair, solta sobre ele, deixa o uso acabar e, se ele olha pro teto, o gesto acabar.</summary>
    private static void UsarEAcabar(Cenario c, Item item)
    {
        ItemNoMundo it = InvocarEAssentar(c, item);
        c.SoltarSobreEle(it.Id).Esta(Estado.Using, $"usando {item}");
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        for (int i = 0; i < 200 && c.Atual.Gesto != Gesto.Nenhum; i++) c.Aplicar(new Tick());
    }

    /// <summary>Aplica os eventos a partir do estado dado, sem o cenário; devolve o último resultado.</summary>
    private static Resultado Aplicar(EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, IEnumerable<Evento> eventos)
    {
        Resultado? r = null;
        foreach (Evento e in eventos)
        {
            r = Maquina.Aplicar(s, e, cfg);
            s = r.Estado;
        }
        return r ?? throw new ArgumentException("Nenhum evento.", nameof(eventos));
    }

    // ---------------------------------------------------------------- a ação e a configuração

    // A ação nova vai no fim do enum, sem mudar os valores das outras nem o de Todas (que não a inclui, para toda
    // configuração de antes continuar igual); o aplicativo a liga, com a chave do tamagotchi; o peso é o mesmo nos três
    // níveis (1).
    [Teste]
    public static void AcaoNova_NoFimDoEnum_ForaDeTodas_LigadaNoAplicativo()
    {
        Afirmar.Sequencia([0, 1, 2, 4, 8, 16, 32, 63, 64, 128], Enum.GetValues<AcoesAutonomas>().Select(a => (int)a), "os valores de sempre, a nova e, depois dela, a de ir ao outro monitor (Fase 5, passo P13)");
        Afirmar.Igual(64, (int)AcoesAutonomas.FumarBaseado, "FumarBaseado no bit seguinte");
        Afirmar.Igual(63, (int)AcoesAutonomas.Todas, "Todas continua as seis ações de sempre");
        ConfiguracaoDoNucleo app = ConfiguracaoDoNucleo.DoAplicativo(Sprite);
        Afirmar.Igual(AcoesAutonomas.Todas | AcoesAutonomas.FumarBaseado | AcoesAutonomas.IrAoOutroMonitor, app.Acoes, "o aplicativo liga a ação nova (e a de ir ao outro monitor, do passo P13)");
        Afirmar.Verdadeiro(app.Tamagotchi, "com a chave do tamagotchi");
        Afirmar.Igual(AcoesAutonomas.Todas, new ConfiguracaoDoNucleo().Acoes, "a configuração padrão do núcleo continua com as de sempre");
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
            Afirmar.Igual(1, PerfilDeEnergia.Padrao(nivel).PesoFumarBaseado, $"{nivel}: o peso calibrado");
    }

    // ---------------------------------------------------------------- o uso

    // Em IDLE no chão, a agenda o faz fumar: USING com o uso do baseado da tabela, no chão, a cara de quem fuma, o relógio
    // ligado e nada autônomo; nenhum item nasce, sai ou gasta Id, e nenhuma janela de item; a onda do baseado (Chapado 2) pela
    // combinação de sempre; a carga com uma substância, sem sintética e sem sorteio; o gerador principal anda um passo só (o
    // sorteio da agenda), e o da paranoia, nenhum.
    [Teste]
    public static void FumaPorContaPropria_OUsoDoBaseadoDaTabela_SemItemNoMundo()
    {
        DadosDoItem dados = TabelaDoTamagotchi.DoItem(Item.Baseado);
        Afirmar.Igual((VerboDeUso.Fumar, 210), (dados.Verbo, dados.PassosDoUso), "o baseado da tabela: fumar, 210 passos");
        Cenario c = Cenario.Parado(SoOBaseado(SemFisica()));
        EstadoDoNucleo antes = c.Atual;
        c.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual(RegraDoBaseado, c.Transicoes[0].Regra, "a regra");
        Afirmar.Igual(UsoDoBaseado, c.Retrato.Uso, "o uso no retrato");
        Afirmar.Falso(c.Atual.Uso!.ComecouAParanoia, "sem paranoia");
        Afirmar.Igual((0, 210, dados.CaraDurante), (c.Retrato.PassoDoUso, c.Atual.PassosRestantes, c.Atual.Expressao), "o primeiro passo, com a cara de quem fuma");
        Afirmar.Igual(ItensNoMundo.Nenhum, c.Atual.Itens, "nenhum item no mundo");
        Afirmar.Igual(antes.ProximoIdDeItem, c.Atual.ProximoIdDeItem, "nenhum Id gasto");
        Afirmar.Falso(c.Efeitos.Any(e => e is MostrarItem or MoverItem or EsconderItem or RemoverItem or LiberarCapturaDoItem), "nenhuma janela de item");
        Afirmar.Igual(ChapadoNaSubida2, c.Atual.Onda, "a onda do baseado");
        Afirmar.Igual(TabelaDoTamagotchi.DaOnda(Onda.Chapado).Subida, c.Efeito<AgendarOnda>().Atraso, "o temporizador da subida do chapado");
        Afirmar.Verdadeiro(c.Atual.RelogioAtivo && c.Tem<LigarRelogio>(), "o relógio liga");
        Afirmar.Falso(c.Atual.DecisaoAgendada || c.Tem<AgendarDecisao>(), "nada autônomo em USING");
        Afirmar.Igual(new CargaDaParanoia(1, false, ConjuntoDeItens.Vazio.Com(Item.Baseado), false), c.Atual.Carga, "uma substância, sem sintética e sem sorteio");
        Afirmar.Igual(antes.Aleatorio.Sortear().Proximo, c.Atual.Aleatorio, "o gerador principal: só o sorteio da agenda");
        Afirmar.Igual(antes.AleatorioDaParanoia, c.Atual.AleatorioDaParanoia, "o gerador da paranoia não anda");
        Afirmar.Falso(new Nucleo(c.Config, c.Atual).Enfileirar(new AutonomyTimer(c.Atual.Geracao)), "o Nucleo descarta a agenda em USING");
    }

    // O uso dura os 210 passos do fumar, com o retrato mostrando o passo e a cara de quem fuma; no último, SETTLING e IDLE no
    // mesmo lugar, sem relógio, com a agenda de volta e a cara da fase da onda.
    [Teste]
    public static void FumaPorContaPropria_DuraOsPassosEVoltaAoMesmoLugar()
    {
        Cenario c = Cenario.Parado(SoOBaseado(SemFisica())).Decidir().Esta(Estado.Using);
        for (int passo = 1; passo < 210; passo++)
        {
            c.Aplicar(new Tick()).SemTransicao();
            Afirmar.Igual(passo, c.Retrato.PassoDoUso, $"passo {passo}");
            Afirmar.Igual(Expressao.Pensativo, c.Atual.Expressao, $"passo {passo}: a cara de quem fuma");
        }
        c.Aplicar(new Tick()).Percorreu(Estado.Using, Estado.Settling, Estado.Idle);
        Afirmar.Igual("USING: fim do uso de Baseado", c.Transicoes[0].Regra, "a regra do fim");
        Afirmar.Nulo(c.Atual.Uso, "o uso acabou");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "no mesmo lugar");
        Afirmar.Verdadeiro(!c.Atual.RelogioAtivo && c.Tem<DesligarRelogio>() && c.Tem<AgendarDecisao>(), "sem relógio, com a agenda");
        Afirmar.Igual(TabelaDoTamagotchi.DaOnda(Onda.Chapado).Cara(FaseDaOnda.Subida), c.Atual.Expressao, "a cara da subida do chapado");
        Afirmar.Igual(Gesto.Nenhum, c.Atual.Gesto, "sem gesto");
    }

    // Com a física do aplicativo: ele fuma no chão, sem sair do lugar, e volta a IDLE no mesmo lugar, com os pés no chão.
    [Teste]
    public static void ComAFisica_FumaNoChaoEVoltaAoMesmoLugar()
    {
        var sim = new SimuladorDeTempo(SoOBaseado(ComFisica()), 3, TopologiasDeExemplo.UmMonitor);
        PontoPx noChao = sim.Estado.Lugar!.Ancora;
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Using);
        sim.Esta(Estado.Using, "fumou na primeira decisão");
        Afirmar.Igual(UsoDoBaseado, sim.Estado.Uso, "o uso do baseado, no chão");
        int passos = 0;
        while (sim.Estado.Estado == Estado.Using && passos < 400)
        {
            Afirmar.Igual(noChao, sim.Estado.Lugar!.Ancora, $"passo {passos}: sem sair do lugar");
            passos += sim.Passos(1);
        }
        Afirmar.Igual(210, passos, "os 210 passos do fumar");
        sim.Esta(Estado.Idle, "de volta a IDLE");
        Afirmar.Igual(noChao, sim.Estado.Lugar!.Ancora, "no mesmo lugar");
        MovimentoTestes.ConferirApoio(sim.Estado, "depois do baseado");
    }

    // ---------------------------------------------------------------- quando ele pode

    // Não emenda: com a onda Chapado na frente (do próprio baseado, até a queda) ou com a paranoia, o peso é zero e, com só o
    // baseado na agenda, a decisão não acha opção: nada acontece. Com outra onda na frente (o bêbado), ele fuma; o Chapado no
    // fundo não impede.
    [Teste]
    public static void NaoFumaChapadoNemParanoico()
    {
        ConfiguracaoDoNucleo cfg = SoOBaseado(SemFisica() with { ChanceDaParanoia = Sempre });

        // Chapado na frente, em todas as fases: a subida e o pico do nível 2, o pico do nível 1 e a queda.
        Cenario c = Cenario.Parado(cfg).Decidir().Esta(Estado.Using);
        c.Passos(210).Esta(Estado.Idle);
        var fases = new List<EstadoDaOnda>();
        while (c.Atual.Onda is { } fase)
        {
            fases.Add(fase);
            c.Decidir().SemTransicao().Esta(Estado.Idle, $"{fase}: não fuma de novo");
            Afirmar.Nulo(c.Atual.Uso, $"{fase}: sem uso");
            c.Aplicar(new ItemEffectTimer(c.Atual.GeracaoDaOnda));
        }
        EstadoDaOnda[] esperadas =
            [ChapadoNaSubida2, new(Onda.Chapado, FaseDaOnda.Pico, 2, 2), new(Onda.Chapado, FaseDaOnda.Pico, 1, 2), new(Onda.Chapado, FaseDaOnda.Queda, 1, 2)];
        Afirmar.Sequencia(esperadas, fases, "as fases do chapado, até ele acabar");
        c.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual(RegraDoBaseado, c.Transicoes[0].Regra, "sem onda, fuma de novo");

        // A paranoia na frente: a bala e a vodka fecham a mistura com sintética, e o sorteio (1 em 1) sai.
        Cenario p = Cenario.Parado(cfg);
        UsarEAcabar(p, Item.Bala);
        UsarEAcabar(p, Item.Vodka);
        Afirmar.Igual(Onda.Paranoico, p.Atual.Onda?.Tipo, "paranoico");
        p.Decidir().SemTransicao().Esta(Estado.Idle, "paranoico, não fuma");

        // Outra onda na frente (a vodka, sem sintética): fuma; o chapado vai para a frente, e o bêbado, para o fundo.
        Cenario b = Cenario.Parado(cfg);
        UsarEAcabar(b, Item.Vodka);
        Afirmar.Igual(Onda.Bebado, b.Atual.Onda?.Tipo, "bêbado");
        b.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual((Onda.Chapado, Onda.Bebado), (b.Atual.Onda!.Tipo, b.Atual.OndaDeFundo!.Tipo), "o chapado na frente, o bêbado no fundo");

        // O chapado no fundo não impede: a vodka depois do baseado vai para a frente (mesma precedência).
        Cenario f = Cenario.Parado(cfg).Decidir().Esta(Estado.Using);
        f.Passos(210);
        UsarEAcabar(f, Item.Vodka);
        Afirmar.Igual((Onda.Bebado, Onda.Chapado), (f.Atual.Onda!.Tipo, f.Atual.OndaDeFundo!.Tipo), "o bêbado na frente, o chapado no fundo");
        f.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual(new EstadoDaOnda(Onda.Chapado, FaseDaOnda.Subida, 3, 3), f.Atual.OndaDeFundo, "o baseado soma no chapado do fundo, que continua congelado");
    }

    // Só em IDLE no chão, visível, sem estar escondido, com a autonomia livre e sem item na mão, e só com a chave ligada: no
    // ar (sem a física, o pouso pelos sinais deixa o IDLE fora do chão), escondido na borda, pausado, com um item na mão ou
    // com a chave desligada, a agenda nunca o leva a fumar.
    [Teste]
    public static void SoEmIdleNoChao_ComAChaveLigada()
    {
        // No ar: sem a física, solto no alto, cai e pousa pelos sinais, e o IDLE fica onde ele estava.
        Cenario noAr = Cenario.Parado(SoOBaseado(SemFisica() with { QuedaFisica = true }));
        var alto = new PontoPx(960, 500);
        noAr.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(alto), new DragEnd(alto)).Esta(Estado.Falling);
        noAr.Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao)).Esta(Estado.Landing);
        noAr.Passos(12).Esta(Estado.Idle, "pousou pelos sinais");
        Afirmar.Verdadeiro(noAr.Ancora.Y < 1032, $"IDLE fora do chão ({noAr.Ancora})");
        noAr.Decidir().SemTransicao().Esta(Estado.Idle, "no ar, não fuma");

        // Escondido na borda: a agenda só troca a cara.
        Cenario escondido = Cenario.Parado(SoOBaseado(SemFisica() with { EsconderijoNoCliqueDuplo = true }));
        escondido.Aplicar(new DoubleClick()).Esta(Estado.Peeking);
        for (int i = 0; i < 20; i++)
        {
            escondido.Decidir();
            Afirmar.Igual(Estado.Peeking, escondido.Atual.Estado, $"decisão {i}: escondido, não fuma");
        }

        // Pausado: nenhuma decisão.
        Cenario pausado = Cenario.Parado(SoOBaseado(SemFisica()));
        long geracao = pausado.Atual.Geracao;
        pausado.Aplicar(new CmdPauseAutonomy(), new AutonomyTimer(geracao)).SemTransicao().Esta(Estado.Idle, "pausado, não fuma");

        // Com um item na mão do usuário: atento, sem decisão.
        Cenario atento = Cenario.Parado(SoOBaseado(SemFisica()));
        ItemNoMundo banana = InvocarEAssentar(atento, Item.Banana);
        long antesDePegar = atento.Atual.Geracao;
        atento.Aplicar(new ItemPress(banana.Id, new PontoPx(banana.Lugar.Ancora.X, banana.Lugar.Ancora.Y - 10)), new AutonomyTimer(antesDePegar));
        atento.SemTransicao().Esta(Estado.Idle, "com a banana na mão do usuário, não fuma");

        // A chave desligada: o peso é zero, e a decisão não acha opção.
        Cenario desligada = Cenario.Parado(SoOBaseado(new ConfiguracaoDoNucleo()));
        EstadoDoNucleo antes = desligada.Atual;
        desligada.Decidir().SemTransicao().Esta(Estado.Idle, "sem a chave, não fuma");
        Afirmar.Igual((CargaDaParanoia.Nenhuma, (EstadoDaOnda?)null), (desligada.Atual.Carga, desligada.Atual.Onda), "nada do tamagotchi");
        Afirmar.Igual(antes.AleatorioDaParanoia, desligada.Atual.AleatorioDaParanoia, "nem o gerador da paranoia");
    }

    // Com a chave desligada, ligar a ação não muda nada: a configuração do aplicativo sem a chave, com e sem a ação, dá o mesmo
    // registro, evento a evento, em dez minutos de agenda livre com interações sorteadas, nos três níveis.
    [Teste]
    public static void ChaveDesligada_AAcaoNaoMudaNada()
    {
        ConfiguracaoDoNucleo comAAcao = ConfiguracaoDoNucleo.DoAplicativo(Sprite) with { Tamagotchi = false };
        ConfiguracaoDoNucleo semAAcao = comAAcao with { Acoes = AcoesAutonomas.Todas };
        Afirmar.Verdadeiro((comAAcao.Acoes & AcoesAutonomas.FumarBaseado) != 0, "a ação ligada");
        var mestre = new Random(20261002);
        long eventos = 0, decisoes = 0;
        for (int n = 0; n < 12; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var preferencias = new Preferencias((NivelDeEnergia)(n % 3), true);
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            var com = new SimuladorDeTempo(comAAcao, (ulong)semente, topologia, preferencias);
            var sem = new SimuladorDeTempo(semAAcao, (ulong)semente, topologia, preferencias);
            var a = new List<(Evento Evento, Resultado Resultado)>();
            var b = new List<(Evento Evento, Resultado Resultado)>();
            com.AoResultado = (_, e, r) => a.Add((e, r));
            sem.AoResultado = (_, e, r) => b.Add((e, r));
            while (com.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(5, 60));
                com.Avancar(espera);
                sem.Avancar(espera);
                foreach (Evento e in OndaTestes.Interacao(rnd, sem.Estado))
                {
                    com.Aplicar(e);
                    sem.Aplicar(e);
                }
            }
            Afirmar.Igual(b.Count, a.Count, $"semente {semente}: o mesmo número de eventos");
            for (int i = 0; i < a.Count; i++)
            {
                string onde = $"semente {semente}, {i}º evento ({a[i].Evento})";
                Afirmar.Igual(b[i].Evento, a[i].Evento, $"{onde}: o mesmo evento");
                Afirmar.Sequencia(b[i].Resultado.Transicoes, a[i].Resultado.Transicoes, $"{onde}: as mesmas transições");
                Afirmar.Sequencia(b[i].Resultado.Efeitos, a[i].Resultado.Efeitos, $"{onde}: os mesmos efeitos");
                Afirmar.Igual(b[i].Resultado.Estado, a[i].Resultado.Estado, $"{onde}: o mesmo estado");
                if (a[i].Evento is AutonomyTimer && a[i].Resultado.Estado.Estado != Estado.Using) decisoes++;
            }
            eventos += a.Count;
            Afirmar.Falso(a.Any(x => x.Resultado.Estado.Estado == Estado.Using), $"semente {semente}: nunca usa nada");
        }
        Console.WriteLine($"         {eventos} eventos iguais em 12 sementes de 10 minutos, {decisoes} disparos da agenda");
        Afirmar.Verdadeiro(decisoes >= 200, "a agenda decidiu bastante");
    }

    // ---------------------------------------------------------------- a prioridade do usuário

    // O clique o interrompe na hora (PRESS em USING leva a PRESSED no mesmo evento), como em todo uso, e a onda continua;
    // o menu abre sem interromper; esconder, a tela cheia e sair acabam o uso, e a onda continua (só sair a cancela).
    [Teste]
    public static void OUsuarioPrevalece_OCliqueInterrompe()
    {
        ConfiguracaoDoNucleo cfg = SoOBaseado(SemFisica());
        Cenario c = Cenario.Parado(cfg).Decidir().Esta(Estado.Using);
        c.Passos(40);
        c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Using, Estado.Pressed);
        Afirmar.Nulo(c.Atual.Uso, "o uso acabou");
        Afirmar.Igual(ChapadoNaSubida2, c.Atual.Onda, "a onda continua");
        Afirmar.Verdadeiro(c.Atual.OndaAgendada, "com o temporizador dela");
        c.Aplicar(new Click()).Esta(Estado.Reacting, "o clique vira reação");

        Cenario menu = Cenario.Parado(cfg).Decidir();
        menu.Aplicar(new ContextMenu(Cenario.PontoOpaco)).SemTransicao().Esta(Estado.Using, "o menu abre sem interromper");
        menu.Efeito<AbrirMenu>();

        Cenario oculto = Cenario.Parado(cfg).Decidir();
        oculto.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        Afirmar.Igual((null, ChapadoNaSubida2), (oculto.Atual.Uso, oculto.Atual.Onda), "esconder acaba o uso, e a onda continua");

        Cenario telaCheia = Cenario.Parado(cfg).Decidir();
        telaCheia.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([TopologiasDeExemplo.Display1]))).EstaEscondido(MotivoDoOcultamento.PorTelaCheia);
        Afirmar.Igual((null, ChapadoNaSubida2), (telaCheia.Atual.Uso, telaCheia.Atual.Onda), "a tela cheia acaba o uso, e a onda continua");

        Cenario saindo = Cenario.Parado(cfg).Decidir();
        saindo.Aplicar(new CmdExit()).Esta(Estado.Exiting);
        saindo.Efeito<CancelarOnda>();
    }

    // ---------------------------------------------------------------- a paranoia

    // Não é droga sintética: sozinho, repetido, ou com álcool, cigarro e cogumelo, nunca sorteia (o gerador da paranoia não
    // anda), e entra na carga como substância.
    [Teste]
    public static void SemSintetica_NuncaSorteia()
    {
        ConfiguracaoDoNucleo cfg = SoOBaseado(SemFisica() with { ChanceDaParanoia = Sempre });
        foreach (Item[] antes in new Item[][] { [], [Item.Vodka], [Item.Cerveja], [Item.Cigarro], [Item.Cogumelo], [Item.Vodka, Item.Cigarro, Item.Cogumelo] })
        {
            Cenario c = Cenario.Parado(cfg);
            foreach (Item item in antes) UsarEAcabar(c, item);
            EstadoDoNucleo s = c.Atual;
            c.Decidir().Percorreu(Estado.Idle, Estado.Using);
            string onde = antes.Length == 0 ? "sozinho" : $"depois de {string.Join(", ", antes)}";
            Afirmar.Igual(RegraDoBaseado, c.Transicoes[0].Regra, $"{onde}: sem paranoia na regra");
            Afirmar.Igual(s.AleatorioDaParanoia, c.Atual.AleatorioDaParanoia, $"{onde}: sem sorteio");
            Afirmar.Igual(s.Carga.Substancias + 1, c.Atual.Carga.Substancias, $"{onde}: mais uma substância");
            Afirmar.Falso(c.Atual.Carga.Sintetica || c.Atual.Carga.Sorteada, $"{onde}: sem sintética e sem sorteio");
            Afirmar.Diferente(Onda.Paranoico, c.Atual.Onda?.Tipo, $"{onde}: sem paranoia");
        }
    }

    // Num episódio com droga sintética que ainda não sorteou (a bala sozinha), o baseado fecha a mistura e faz o sorteio
    // único do episódio, num passo do gerador da paranoia: com 1 em 1, ela começa (na regra, e no fim do uso ele olha pro
    // teto); com 0 em 1, não, e o episódio fica sorteado. Num episódio já sorteado (a bala e o MD), o baseado não sorteia.
    [Teste]
    public static void ComSintetica_FechaAMisturaESorteiaUmaVez()
    {
        Cenario sai = Cenario.Parado(SoOBaseado(SemFisica() with { ChanceDaParanoia = Sempre }));
        UsarEAcabar(sai, Item.Bala);
        Afirmar.Verdadeiro(sai.Atual.Carga is { Sintetica: true, Sorteada: false } && !sai.Atual.Carga.MisturaComSintetica, "a bala sozinha: sintética, sem mistura");
        EstadoDoNucleo antes = sai.Atual;
        sai.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual($"{RegraDoBaseado}; a paranoia começa: Paranoico/Subida/1", sai.Transicoes[0].Regra, "o baseado fecha a mistura, e o sorteio sai");
        Afirmar.Igual(antes.AleatorioDaParanoia.Sortear().Proximo, sai.Atual.AleatorioDaParanoia, "um passo do gerador da paranoia");
        Afirmar.Igual(antes.Aleatorio.Sortear().Proximo, sai.Atual.Aleatorio, "o principal: só o sorteio da agenda");
        Afirmar.Verdadeiro(sai.Atual.Uso!.ComecouAParanoia && sai.Atual.Carga.Sorteada, "começou a paranoia, e o episódio sorteou");
        Afirmar.Igual((Onda.Paranoico, Onda.Chapado), (sai.Atual.Onda!.Tipo, sai.Atual.OndaDeFundo!.Tipo), "a paranoia na frente, e a frente depois da combinação (o chapado) no fundo");
        sai.Passos(210).Esta(Estado.Idle);
        Afirmar.Igual((Gesto.OlharProTeto, Maquina.PassosDoOlharProTeto), (sai.Atual.Gesto, sai.Atual.PassosDoGesto), "no fim do uso, olha pro teto");

        Cenario naoSai = Cenario.Parado(SoOBaseado(SemFisica() with { ChanceDaParanoia = NuncaParanoia }));
        UsarEAcabar(naoSai, Item.Bala);
        EstadoDoNucleo antesDoNao = naoSai.Atual;
        naoSai.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual(RegraDoBaseado, naoSai.Transicoes[0].Regra, "o sorteio não sai: nada na regra");
        Afirmar.Igual(antesDoNao.AleatorioDaParanoia.Sortear().Proximo, naoSai.Atual.AleatorioDaParanoia, "mas o gerador da paranoia anda um passo");
        Afirmar.Verdadeiro(naoSai.Atual.Carga is { Sorteada: true, MisturaComSintetica: true } && !naoSai.Atual.Uso!.ComecouAParanoia, "o episódio sorteou, sem paranoia");

        Cenario sorteado = Cenario.Parado(SoOBaseado(SemFisica() with { ChanceDaParanoia = NuncaParanoia }));
        UsarEAcabar(sorteado, Item.Bala);
        UsarEAcabar(sorteado, Item.Md);
        Afirmar.Verdadeiro(sorteado.Atual.Carga.Sorteada, "a bala e o MD: o episódio já sorteou");
        EstadoDoNucleo jaSorteado = sorteado.Atual;
        sorteado.Decidir().Percorreu(Estado.Idle, Estado.Using);
        Afirmar.Igual(jaSorteado.AleatorioDaParanoia, sorteado.Atual.AleatorioDaParanoia, "episódio já sorteado: o baseado não sorteia");
        Afirmar.Igual(3, sorteado.Atual.Carga.Substancias, "mais uma substância na carga");
    }

    // O outro lado da mesma regra (revisão do baseado por conta própria, achado 1): o baseado que ele fuma sozinho conta como
    // substância no episódio, então uma bala só, dada pelo usuário com ele chapado desse baseado, fecha a mistura com droga
    // sintética e faz o sorteio único do episódio (com 1 em 1, a paranoia começa; no aplicativo, 1 em 8), como depois de um
    // baseado solto pelo usuário. Sem o baseado dele, a mesma bala sozinha não sorteia. É a regra do coordenador; fica para o
    // usuário confirmar.
    [Teste]
    public static void BalaDoUsuario_ComEleChapadoDoBaseadoDele_FechaAMisturaESorteia()
    {
        ConfiguracaoDoNucleo cfg = SoOBaseado(SemFisica() with { ChanceDaParanoia = Sempre });
        Cenario c = Cenario.Parado(cfg).Decidir().Esta(Estado.Using);
        c.Passos(210).Esta(Estado.Idle, "fumou sozinho");
        Afirmar.Verdadeiro(c.Atual.Carga is { Substancias: 1, Sintetica: false, Sorteada: false } && c.Atual.Onda?.Tipo == Onda.Chapado, "chapado do baseado dele, sem sintética e sem sorteio");
        EstadoDoNucleo antes = c.Atual;
        ItemNoMundo bala = InvocarEAssentar(c, Item.Bala);
        c.SoltarSobreEle(bala.Id).Esta(Estado.Using, "usando a bala");
        Afirmar.Contem("a paranoia começa: Paranoico/Subida/1", c.Transicoes[0].Regra, "a bala fecha a mistura com o baseado dele, e o sorteio sai");
        Afirmar.Igual(antes.AleatorioDaParanoia.Sortear().Proximo, c.Atual.AleatorioDaParanoia, "um passo do gerador da paranoia");
        Afirmar.Verdadeiro(c.Atual.Carga is { MisturaComSintetica: true, Sorteada: true } && c.Atual.Onda?.Tipo == Onda.Paranoico, "o episódio sorteou, e ele ficou paranoico");

        Cenario semOBaseado = Cenario.Parado(cfg);
        EstadoDoNucleo antesDaBala = semOBaseado.Atual;
        UsarEAcabar(semOBaseado, Item.Bala);
        Afirmar.Igual(antesDaBala.AleatorioDaParanoia, semOBaseado.Atual.AleatorioDaParanoia, "a bala sozinha, sem o baseado dele: sem sorteio");
        Afirmar.Diferente(Onda.Paranoico, semOBaseado.Atual.Onda?.Tipo, "nem paranoia");
    }

    // O mesmo caminho do baseado que o usuário solta nele: a partir do mesmo estado, depois de vários começos de episódio, com
    // as três chances, o baseado por conta própria e o baseado solto pelo usuário dão o mesmo uso, a mesma cara, as mesmas
    // ondas, a mesma carga, o mesmo gerador da paranoia e o mesmo texto da combinação, do alívio e da paranoia na regra. Só
    // mudam a origem (a agenda, que sorteia no gerador principal, ou o gesto, que tira o item do mundo).
    [Teste]
    public static void OMesmoCaminhoDoBaseadoSoltoPeloUsuario()
    {
        Item[][] comecos =
        [
            [], [Item.Vodka], [Item.Cerveja, Item.Cigarro], [Item.Cogumelo], [Item.Bala], [Item.Md], [Item.Cocaina], [Item.LancaPerfume],
            [Item.Bala, Item.Md], [Item.Banana], [Item.Cafe], [Item.Energetico], [Item.Vodka, Item.Bala], [Item.Cigarro, Item.Agua],
        ];
        int comparados = 0;
        foreach (Chance chance in new[] { Sempre, NuncaParanoia, new Chance(1, 8) })
        {
            foreach (Item[] comeco in comecos)
            {
                ConfiguracaoDoNucleo cfg = SoOBaseado(SemFisica() with { ChanceDaParanoia = chance });
                Cenario c = Cenario.Parado(cfg, semente: (ulong)(17 + comparados));
                foreach (Item item in comeco) UsarEAcabar(c, item);
                EstadoDoNucleo s = c.Atual;
                if (s.Onda?.Tipo is Onda.Chapado or Onda.Paranoico) continue;
                string onde = $"chance {chance}, depois de [{string.Join(", ", comeco)}]";

                Resultado sozinho = Maquina.Aplicar(s, new AutonomyTimer(s.Geracao), cfg);
                Afirmar.Igual(Estado.Using, sozinho.Estado.Estado, $"{onde}: fumou por conta própria");

                // O usuário invoca um baseado, deixa cair e o solta nele.
                EstadoDoNucleo comItem = Aplicar(s, cfg, [new CmdSummonItem(Item.Baseado)]).Estado;
                for (int i = 0; i < 600 && comItem.Itens.AlgumCaindo; i++) comItem = Maquina.Aplicar(comItem, new Tick(), cfg).Estado;
                ItemNoMundo baseado = comItem.Itens.Todos.Single();
                Resultado solto = Aplicar(comItem, cfg, Arraste(baseado, Cenario.MeioDoPersonagem(comItem, cfg)));
                Afirmar.Igual(Estado.Using, solto.Estado.Estado, $"{onde}: usou o baseado solto");

                EstadoDoNucleo a = sozinho.Estado, b = solto.Estado;
                Afirmar.Igual(b.Uso, a.Uso, $"{onde}: o mesmo uso");
                Afirmar.Igual((b.PassosRestantes, b.Expressao), (a.PassosRestantes, a.Expressao), $"{onde}: os mesmos passos e a mesma cara");
                Afirmar.Igual((b.Onda, b.OndaDeFundo), (a.Onda, a.OndaDeFundo), $"{onde}: as mesmas ondas");
                Afirmar.Igual(b.Carga, a.Carga, $"{onde}: a mesma carga");
                Afirmar.Igual(b.AleatorioDaParanoia, a.AleatorioDaParanoia, $"{onde}: o mesmo gerador da paranoia");
                Afirmar.Igual((b.GeracaoDaOnda, b.OndaAgendada), (a.GeracaoDaOnda, a.OndaAgendada), $"{onde}: o mesmo temporizador da onda");
                Afirmar.Igual(
                    solto.Transicoes.Single().Regra["ITEM_DRAG_END sobre o personagem: Fumar Baseado".Length..],
                    sozinho.Transicoes.Single().Regra[RegraDoBaseado.Length..],
                    $"{onde}: o mesmo texto da combinação e da paranoia");
                Afirmar.Igual(ItensNoMundo.Nenhum, a.Itens, $"{onde}: por conta própria, nenhum item");
                comparados++;
            }
        }
        Console.WriteLine($"         {comparados} comparações do baseado por conta própria com o solto pelo usuário");
        Afirmar.Verdadeiro(comparados >= 30, "comparações suficientes");
    }

    // ---------------------------------------------------------------- o peso

    // A calibração do peso (1, nos três níveis), pela simulação do núcleo, só com a autonomia, na configuração do aplicativo:
    // em 100 horas na energia Média (10 sementes de 10 horas), ele fuma por conta própria cerca de uma vez a cada 4 minutos
    // de tempo elegível (IDLE no chão, sem Chapado nem paranoia na frente): entre 3,2 e 4,8 minutos, cerca de quatro desvios
    // padrão em volta da medição longa (3,9 minutos em 1000 horas). Na Baixa, menos vezes por minuto elegível; na Alta, mais.
    // Toda vez, ele estava em IDLE no chão, sem Chapado nem paranoia na frente, e nenhum item nasceu.
    [Teste]
    public static void Peso_CercaDeUmBaseadoACadaQuatroMinutosElegiveis_NaMedia()
    {
        (long fumou, double elegivelMin, double totalMin) Medir(NivelDeEnergia nivel, int sementes, double horas)
        {
            ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(Sprite);
            long fumou = 0;
            double elegivel = 0, total = 0;
            for (int n = 1; n <= sementes; n++)
            {
                var sim = new SimuladorDeTempo(cfg, (ulong)(20261100 + n), TopologiasDeExemplo.UmMonitor, new Preferencias(nivel, true));
                double ultimo = sim.AgoraMs;
                EstadoDoNucleo atual = sim.Estado;
                sim.AoResultado = (antes, e, r) =>
                {
                    if (!r.Transicoes.Any(t => t.Regra.StartsWith(RegraDoBaseado, StringComparison.Ordinal))) return;
                    fumou++;
                    Afirmar.Verdadeiro(e is AutonomyTimer && Elegivel(antes), $"{nivel}, semente {n}: fumou fora de IDLE no chão ou com Chapado ou paranoia na frente ({antes.Estado}, onda {antes.Onda})");
                    Afirmar.Igual(antes.Itens, r.Estado.Itens, $"{nivel}, semente {n}: nenhum item nasce nem sai");
                };
                sim.AoAplicar = (_, _, depois) =>
                {
                    if (Elegivel(atual)) elegivel += sim.AgoraMs - ultimo;
                    ultimo = sim.AgoraMs;
                    atual = depois;
                };
                sim.Avancar(TimeSpan.FromHours(horas));
                if (Elegivel(atual)) elegivel += sim.AgoraMs - ultimo;
                total += sim.AgoraMs;
            }
            return (fumou, elegivel / 60000, total / 60000);
        }

        (long fumouNaMedia, double elegivelNaMedia, double totalNaMedia) = Medir(NivelDeEnergia.Media, 10, 10);
        double porMinutoNaMedia = elegivelNaMedia / fumouNaMedia;
        (long fumouNaBaixa, double elegivelNaBaixa, _) = Medir(NivelDeEnergia.Baixa, 5, 6);
        (long fumouNaAlta, double elegivelNaAlta, _) = Medir(NivelDeEnergia.Alta, 5, 6);
        Console.WriteLine($"         Média: {fumouNaMedia} baseados em {totalNaMedia / 60:0} h, 1 a cada {porMinutoNaMedia:0.00} min elegíveis e {totalNaMedia / fumouNaMedia:0.00} min no total; "
            + $"Baixa: 1 a cada {elegivelNaBaixa / fumouNaBaixa:0.00} min elegíveis; Alta: 1 a cada {elegivelNaAlta / fumouNaAlta:0.00} min elegíveis");
        Afirmar.Verdadeiro(porMinutoNaMedia is >= 3.2 and <= 4.8, $"na Média, um baseado a cada {porMinutoNaMedia:0.00} min elegíveis, fora de 3,2 a 4,8");
        Afirmar.Verdadeiro(elegivelNaBaixa / fumouNaBaixa > porMinutoNaMedia && porMinutoNaMedia > elegivelNaAlta / fumouNaAlta,
            "a energia muda a frequência como em toda ação: menos vezes na Baixa, mais na Alta");
    }

    /// <summary>
    /// O tempo elegível da decisão, escrito aqui à parte do núcleo: IDLE no chão (a âncora na borda de baixo da área útil), sem
    /// estar escondido, com a autonomia livre, sem item na mão do usuário e sem a onda Chapado ou a paranoia na frente.
    /// </summary>
    private static bool Elegivel(EstadoDoNucleo s)
        => s.Estado == Estado.Idle && s.Esconderijo == LadoDoEsconderijo.Nenhum && !s.AutonomiaPausada && !s.PainelAberto && s.Itens.NaMao is null
            && s.Lugar is { } l && l.Ancora.Y == l.Monitor.AreaUtil.Base && s.Onda?.Tipo is not (Onda.Chapado or Onda.Paranoico);
}
