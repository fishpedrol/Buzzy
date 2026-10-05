using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// Os itens do tamagotchi adulto (DEC-028, passo T5; desenho do núcleo, 4.6 e 4.8, com a crítica de integração), atrás da
/// chave Tamagotchi: nascer ao lado dele e cair quicando uma vez, o sétimo item, segurar (atento), arrastar, soltar fora ou
/// sobre ele, esconder, topologia, tela cheia, recolher e a regra escrita de "sobre o personagem". Tudo é de desenho animado.
/// </summary>
internal static class ItensTestes
{
    // ---------------------------------------------------------------- regras puras

    // C14 e L10 da crítica: "sobre o personagem" é o retângulo do item, já preso na área útil, cruzar o do sprite dele
    // encolhido 20% de cada lado (arredondado ao pixel, metade para cima): num sprite de 128 px, o miolo de 76 px, de
    // (1594,930) a (1670,1006) com a âncora em (1632,1032). Retângulos semiabertos: encostar não é cruzar.
    [Teste]
    public static void SobreOPersonagem_TabelaDaRegra()
    {
        var personagem = new RetanguloPx(1568, 904, 1696, 1032);
        RetanguloPx Item(int x, int y) => new(x - 24, y - 48, x + 24, y);
        (string Caso, RetanguloPx Item, bool Sobre)[] casos =
        [
            ("no meio", Item(1632, 992), true),
            ("encostado no miolo pela esquerda (direita do item = 1594)", Item(1570, 992), false),
            ("um pixel dentro do miolo pela esquerda", Item(1571, 992), true),
            ("encostado no miolo pela direita (esquerda do item = 1670)", Item(1694, 992), false),
            ("um pixel dentro pela direita", Item(1693, 992), true),
            ("encostado em cima (base do item = 930)", Item(1632, 930), false),
            ("um pixel dentro em cima", Item(1632, 931), true),
            ("encostado embaixo (topo do item = 1006)", Item(1632, 1054), false),
            ("um pixel dentro embaixo", Item(1632, 1053), true),
            ("no canto transparente do sprite, fora do miolo", Item(1580, 925), false),
            ("longe", Item(500, 300), false),
        ];
        foreach ((string caso, RetanguloPx item, bool sobre) in casos)
            Afirmar.Igual(sobre, Maquina.SobreOPersonagem(item, personagem, 20), caso);

        // A margem arredonda ao pixel: 20% de 128 = 25,6, que vira 26; de 100 px, 20 cheios; 0% é o sprite inteiro.
        Afirmar.Verdadeiro(Maquina.SobreOPersonagem(new RetanguloPx(1500, 900, 1569, 960), personagem, 0), "0%: o sprite inteiro (um pixel em comum)");
        Afirmar.Falso(Maquina.SobreOPersonagem(new RetanguloPx(1500, 900, 1568, 960), personagem, 0), "0%: encostar não é cruzar");
        var cem = new RetanguloPx(0, 0, 100, 100);
        Afirmar.Verdadeiro(Maquina.SobreOPersonagem(new RetanguloPx(0, 0, 21, 21), cem, 20), "100 px a 20%: o miolo começa em 20");
        Afirmar.Falso(Maquina.SobreOPersonagem(new RetanguloPx(0, 0, 20, 20), cem, 20), "e não em 19");
        Afirmar.Falso(Maquina.SobreOPersonagem(personagem, personagem, 50), "50%: o miolo fica vazio, nada está sobre ele");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => Maquina.SobreOPersonagem(personagem, personagem, -1), "margem negativa");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => Maquina.SobreOPersonagem(personagem, personagem, 51), "margem acima de 50%");
        Afirmar.Igual(20, new ConfiguracaoDoNucleo().MargemDoAlvo, "a margem do núcleo é 20%");
    }

    // Tabela 4.6: os estados que aceitam um item solto sobre ele.
    [Teste]
    public static void AceitaItem_TabelaDeEstados()
    {
        Estado[] aceitam = [Estado.Idle, Estado.Walking, Estado.Climbing, Estado.Hanging, Estado.Resting, Estado.Reacting, Estado.Landing, Estado.Peeking];
        foreach (Estado e in Enum.GetValues<Estado>())
            Afirmar.Igual(aceitam.Contains(e), Maquina.AceitaItem(e), $"{e}");
    }

    // ---------------------------------------------------------------- nascer e cair

    // 4.8: o item nasce ao lado dele, do lado para onde ele olha (a direita, na partida): 1632 + 64 (meio personagem) + 8
    // (folga) + 24 (meio item) = 1728, e 140 DIP acima do chão; a janela aparece e o relógio liga. Cai com a gravidade do
    // personagem, quica uma vez (de leve) e para no chão; o relógio só corre durante a queda, e a janela segue o item.
    [Teste]
    public static void Invocar_ApareceDoLadoQueEleOlhaCaiQuicaUmaVezEPara()
    {
        Cenario c = Cenario.Parado(SemFisica());
        Afirmar.Igual(Direcao.Direita, c.Atual.Direcao, "olha para a direita");
        c.Aplicar(new CmdSummonItem(Item.Banana)).SemTransicao();
        ItemNoMundo item = c.Atual.Itens.Todos.Single();
        Afirmar.Igual((1, Item.Banana, SituacaoDoItem.Caindo), (item.Id, item.Item, item.Situacao), "o primeiro item, caindo");
        Afirmar.Igual(new PontoPx(1632 + 64 + 8 + 24, 1032 - 140), item.Lugar.Ancora, "ao lado, 140 DIP acima do chão");
        Afirmar.Igual(new RetanguloPx(1704, 844, 1752, 892), item.Lugar.Retangulo, "48 × 48 DIP a 96 DPI");
        Afirmar.Sequencia([new MostrarItem(1, Item.Banana, item.Lugar), new LigarRelogio()], c.Efeitos, "a janela aparece e o relógio liga");
        Afirmar.Igual(Expressao.Empolgado, c.Atual.Expressao, "parado e sem onda, fica empolgado");
        Afirmar.Igual(2, c.Atual.ProximoIdDeItem, "o próximo Id");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "ele não se move");

        int passos = 0, quiques = 0;
        double vyAnterior = 0;
        int yMaisAlto = int.MaxValue;
        while (c.Atual.Itens.Todos.Single().Situacao == SituacaoDoItem.Caindo)
        {
            Afirmar.Verdadeiro(passos++ < 300, "a queda acaba");
            ItemNoMundo antes = c.Atual.Itens.Todos.Single();
            c.Aplicar(new Tick()).SemTransicao();
            ItemNoMundo agora = c.Atual.Itens.Todos.Single();
            if (agora.VY < 0 && vyAnterior >= 0) quiques++;
            if (quiques > 0) yMaisAlto = Math.Min(yMaisAlto, agora.Lugar.Ancora.Y);
            vyAnterior = agora.VY;
            Afirmar.Igual(1728, agora.Lugar.Ancora.X, $"passo {passos}: cai reto");
            Afirmar.Verdadeiro(agora.Lugar.Ancora.Y <= 1032, $"passo {passos}: nunca passa do chão");
            EsperarSoAJanelaEORelogio(c, antes, agora, $"passo {passos}");
        }
        ItemNoMundo parado = c.Atual.Itens.Todos.Single();
        Afirmar.Igual(new PontoPx(1728, 1032), parado.Lugar.Ancora, "parado no chão");
        Afirmar.Igual(1, quiques, "quicou uma vez");
        Afirmar.Verdadeiro(yMaisAlto is < 1032 and > 1032 - 30, $"um quique leve ({1032 - yMaisAlto} px)");
        Afirmar.Verdadeiro(passos is > 20 and < 60, $"a queda e o quique levam menos de um segundo ({passos} passos)");
        Afirmar.Falso(c.Atual.RelogioAtivo, "parado, o relógio desliga");
        Afirmar.Verdadeiro(c.Tem<DesligarRelogio>(), "no passo em que para");
        c.Esta(Estado.Idle);
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "ele continua no lugar");
    }

    /// <summary>Num passo da queda, só a janela do item se move (se mudou de lugar) e, no último, o relógio desliga.</summary>
    private static void EsperarSoAJanelaEORelogio(Cenario c, ItemNoMundo antes, ItemNoMundo agora, string onde)
    {
        var esperado = new List<Efeito>();
        if (antes.Lugar != agora.Lugar) esperado.Add(new MoverItem(agora.Id, agora.Lugar));
        if (agora.Situacao != SituacaoDoItem.Caindo) esperado.Add(new DesligarRelogio());
        Afirmar.Sequencia(esperado, c.Efeitos, $"{onde}: efeitos");
    }

    // 4.8: sem lugar do lado para onde ele olha (fora da área útil ou sobre outro item), nasce do outro lado, e depois mais
    // longe; olhando para a esquerda, a esquerda vem primeiro.
    [Teste]
    public static void Invocar_SemEspacoDoLado_UsaOOutro()
    {
        // Encostado na lateral direita (âncora em 1920 − 64 = 1856), olhando para a direita: o lado direito (1952) passa
        // da área útil do item (até 1920 − 24 = 1896); nasce à esquerda (1856 − 96 = 1760).
        Cenario c = Cenario.Parado(SemFisica()).Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(new PontoPx(1950, 1000)), new DragEnd(new PontoPx(1950, 1000)));
        Afirmar.Igual(new PontoPx(1856, 1032), c.Ancora, "encostado na lateral direita");
        Afirmar.Igual(Direcao.Direita, c.Atual.Direcao, "olhando para a direita");
        Afirmar.Igual(1760, InvocarEAssentar(c, Item.Agua).Lugar.Ancora.X, "do outro lado");
        // O lado esquerdo já tem um item: o seguinte vai uma largura de item e uma folga mais longe, à esquerda (1760 − 56).
        Afirmar.Igual(1704, InvocarEAssentar(c, Item.Cafe).Lugar.Ancora.X, "mais longe, sem cruzar o primeiro");
        Afirmar.Igual(1648, InvocarEAssentar(c, Item.Bala).Lugar.Ancora.X, "e mais longe ainda");
        // Os três lugares (1760, 1704, 1648) ocupados e o lado direito fora: o primeiro candidato, preso entre as laterais.
        Afirmar.Igual(1896, InvocarEAssentar(c, Item.Md).Lugar.Ancora.X, "sem nenhum lugar livre, o primeiro, preso na lateral");

        // Olhando para a esquerda, no meio da tela, a esquerda vem primeiro.
        Cenario esquerda = OlhandoParaAEsquerda();
        Afirmar.Igual(esquerda.Ancora.X - 96, InvocarEAssentar(esquerda, Item.Banana).Lugar.Ancora.X, "olhando para a esquerda, nasce à esquerda");
        Afirmar.Igual(esquerda.Ancora.X + 96, InvocarEAssentar(esquerda, Item.Banana).Lugar.Ancora.X, "o segundo, do outro lado");
    }

    /// <summary>Parado no meio da tela, olhando para a esquerda: a agenda o fez andar para a esquerda, e o usuário o pôs no chão.</summary>
    private static Cenario OlhandoParaAEsquerda()
    {
        for (ulong semente = 1; semente < 50; semente++)
        {
            var c = new Cenario(SemFisica(), semente);
            c.Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
            c.AplicarCom(SemFisica() with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao));
            if (c.Atual.Direcao != Direcao.Esquerda) continue;
            c.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(new PontoPx(960, 1000)), new DragEnd(new PontoPx(960, 1000)));
            c.Esta(Estado.Idle);
            Afirmar.Igual(Direcao.Esquerda, c.Atual.Direcao, "continua olhando para a esquerda");
            return c;
        }
        Afirmar.Falhar("nenhuma semente fez o personagem olhar para a esquerda");
        return null!;
    }

    // D17: com 6 itens, o sétimo tira o de menor Id que não está na mão do usuário (RemoverItem Substituido); Ids nunca se
    // repetem.
    [Teste]
    public static void Invocar_SetimoTiraOMaisAntigoForaDaMao()
    {
        Cenario c = Cenario.Parado(SemFisica());
        for (int i = 0; i < 6; i++) InvocarEAssentar(c, (Item)i);
        Afirmar.Sequencia([1, 2, 3, 4, 5, 6], c.Atual.Itens.Todos.Select(i => i.Id), "seis itens");
        ItemNoMundo primeiro = c.Atual.Itens.PorId(1)!;
        c.Aplicar(new ItemPress(1, new PontoPx(primeiro.Lugar.Ancora.X, primeiro.Lugar.Ancora.Y - 10)));
        Afirmar.Igual(SituacaoDoItem.Segurado, c.Atual.Itens.PorId(1)!.Situacao, "o primeiro na mão");

        c.Aplicar(new CmdSummonItem(Item.Vodka));
        Afirmar.Sequencia([1, 3, 4, 5, 6, 7], c.Atual.Itens.Todos.Select(i => i.Id), "o sétimo tirou o 2, o mais antigo fora da mão");
        Afirmar.Igual(new RemoverItem(2, MotivoDaRemocao.Substituido), DosItens(c.Efeitos).First(), "a janela do 2 fecha primeiro");
        Afirmar.Verdadeiro(c.Efeitos.OfType<MostrarItem>().Single().Id == 7, "e a do 7 aparece");

        c.Aplicar(new ItemRelease(1), new CmdSummonItem(Item.Cerveja));
        Afirmar.Sequencia([3, 4, 5, 6, 7, 8], c.Atual.Itens.Todos.Select(i => i.Id), "solto, o 1 é o mais antigo e sai");
        Afirmar.Igual(new RemoverItem(1, MotivoDaRemocao.Substituido), DosItens(c.Efeitos).First(), "substituído");
        Afirmar.Igual(9, c.Atual.ProximoIdDeItem, "os Ids nunca se repetem");
    }

    // Escondido, antes da carga, com o tamagotchi desligado ou com um item fora do enum, a invocação não faz nada.
    [Teste]
    public static void Invocar_EscondidoSemOModoOuForaDoEnum_EhIgnorado()
    {
        void Ignorado(Cenario c, Evento e, string caso)
        {
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(e).SemTransicao();
            Afirmar.Sequencia([], c.Efeitos, $"{caso}: sem efeito");
            Afirmar.Igual(antes with { Sinal = Sinal.Nenhum }, c.Atual, $"{caso}: nada muda");
        }
        Ignorado(Cenario.Parado(SemFisica()).Aplicar(new CmdHide()), new CmdSummonItem(Item.Banana), "escondido");
        Ignorado(new Cenario(SemFisica()), new CmdSummonItem(Item.Banana), "antes da carga");
        Ignorado(Cenario.Parado(new ConfiguracaoDoNucleo()), new CmdSummonItem(Item.Banana), "tamagotchi desligado");
        foreach (int fora in new[] { 13, 99, -1 })
            Ignorado(Cenario.Parado(SemFisica()), new CmdSummonItem((Item)fora), $"item {fora}, fora do enum");
    }

    // L15: com o tamagotchi desligado, os eventos dos itens e o disparo da onda são descartados antes de tudo, sem nem
    // encerrar um gesto curto em curso (ligado, um evento de item, de prioridade maior que a do relógio, o encerra:
    // invariante 15).
    [Teste]
    public static void ChaveDesligada_EventosDosItensDescartadosAntesDeEncerrarOGesto()
    {
        Evento[] doTamagotchi = [new CmdSummonItem(Item.Banana), new CmdClearItems(), new ItemPress(1, Cenario.PontoOpaco), new ItemDragStart(1),
            new ItemDragMove(1, Cenario.PontoOpaco), new ItemDragEnd(1, Cenario.PontoOpaco), new ItemRelease(1), new ItemEffectTimer(0)];
        foreach (Evento e in doTamagotchi)
        {
            Cenario c = Cenario.Parado(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Gesto }).Decidir();
            Afirmar.Diferente(Gesto.Nenhum, c.Atual.Gesto, "um gesto em curso");
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(e).SemTransicao();
            Afirmar.Sequencia([], c.Efeitos, $"{e.GetType().Name}, desligado: sem efeito");
            Afirmar.Igual(antes, c.Atual, $"{e.GetType().Name}, desligado: o gesto continua e nada muda");

            if (e.Origem < Origem.Sistema) continue;
            Cenario ligado = Cenario.Parado(SemFisica() with { Acoes = AcoesAutonomas.Gesto }).Decidir();
            ligado.Aplicar(e);
            Afirmar.Igual(Gesto.Nenhum, ligado.Atual.Gesto, $"{e.GetType().Name}, ligado: encerra o gesto (invariante 15)");
        }
    }

    // ---------------------------------------------------------------- segurar e arrastar

    // C18 da crítica: segurar um item deixa o personagem atento. Andando, ele para; descansando, acorda; na parede e no
    // cipó, fica agarrado, e o foguete apaga (L7), inclusive preso pelo usuário no meio de um passeio (DEC-024); sem onda,
    // olha curioso. A agenda pausa até o item sair da mão: nenhum AgendarDecisao com ele seguro, por minutos; solto, ela
    // volta depois do intervalo de acomodação.
    [Teste]
    public static void SegurarItem_FicaAtentoSemAgenda()
    {
        foreach (string situacao in new[] { "andando", "descansando", "na parede", "no cipó", "no foguete", "preso passeando na parede", "preso passeando no cipó" })
        {
            SimuladorDeTempo sim = Chegar(situacao);
            Estado antes = sim.Estado.Estado;
            ItemNoMundo item = sim.Estado.Itens.Todos.Single();
            var aplicados = new List<Resultado>();
            sim.AoResultado = (_, _, r) => aplicados.Add(r);
            sim.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)));
            EstadoDoNucleo s = sim.Estado;
            Afirmar.Verdadeiro(s.Atento, $"{situacao}: atento");
            Afirmar.Igual(SituacaoDoItem.Segurado, s.Itens.PorId(item.Id)!.Situacao, $"{situacao}: o item na mão");
            Afirmar.Falso(s.DecisaoAgendada, $"{situacao}: a agenda pausa");
            Afirmar.Falso(aplicados[^1].Efeitos.OfType<AgendarDecisao>().Any(), $"{situacao}: sem agendamento");
            Afirmar.Igual(Expressao.Curioso, s.Expressao, $"{situacao}: olha o item, curioso");
            switch (situacao)
            {
                case "andando":
                    Afirmar.Igual(Estado.Idle, s.Estado, "andando: para");
                    Afirmar.Igual("ITEM_PRESS: para e olha o item", aplicados[^1].Transicoes.Single().Regra, "a regra");
                    break;
                case "descansando":
                    Afirmar.Igual((Estado.Idle, Sinal.Acordou), (s.Estado, s.Sinal), "descansando: acorda");
                    break;
                default:
                    Afirmar.Igual(antes, s.Estado, $"{situacao}: continua no mesmo apoio");
                    Afirmar.Verdadeiro(s.Movimento.Agarrado && !s.Movimento.Foguete, $"{situacao}: agarrado, sem foguete");
                    Afirmar.Falso(s.RelogioAtivo, $"{situacao}: parado, sem relógio");
                    break;
            }
            Posicionamento lugar = s.Lugar!;
            sim.Avancar(TimeSpan.FromMinutes(3));
            Afirmar.Igual(lugar, sim.Estado.Lugar, $"{situacao}: três minutos depois, no mesmo lugar");
            Afirmar.Falso(aplicados.Any(r => r.Efeitos.OfType<AgendarDecisao>().Any()), $"{situacao}: nenhuma decisão autônoma com o item na mão");

            aplicados.Clear();
            sim.Aplicar(new ItemRelease(item.Id));
            Afirmar.Falso(sim.Estado.Atento, $"{situacao}: solto, deixa de estar atento");
            AgendarDecisao agenda = aplicados[^1].Efeitos.OfType<AgendarDecisao>().Single();
            Afirmar.Verdadeiro(agenda.Atraso >= sim.Nucleo.Configuracao.IntervaloDeAcomodacao, $"{situacao}: a agenda volta depois do intervalo de acomodação ({agenda.Atraso})");
        }
    }

    /// <summary>Com a física do aplicativo e um item invocado, a agenda leva o personagem à situação dada.</summary>
    private static SimuladorDeTempo Chegar(string situacao)
    {
        if (situacao.StartsWith("preso passeando", StringComparison.Ordinal)) return ChegarPresoPasseando(noCipo: situacao.EndsWith("cipó", StringComparison.Ordinal));
        (AcoesAutonomas acoes, Func<EstadoDoNucleo, bool> condicao) = situacao switch
        {
            "andando" => (AcoesAutonomas.Andar, (Func<EstadoDoNucleo, bool>)(s => s.Estado == Estado.Walking && !s.Itens.AlgumCaindo)),
            "descansando" => (AcoesAutonomas.Descansar, s => s.Estado == Estado.Resting && !s.Itens.AlgumCaindo),
            "na parede" => (AcoesAutonomas.Escalar, s => s.Estado == Estado.Climbing && !s.Movimento.Agarrado && !s.Movimento.Foguete && s.Movimento.SentidoVertical < 0),
            "no cipó" => (AcoesAutonomas.Escalar, s => s.Estado == Estado.Hanging && !s.Movimento.Agarrado),
            _ => (AcoesAutonomas.Escalar, s => s.Estado == Estado.Climbing && s.Movimento.Foguete),
        };
        for (ulong semente = 1; semente <= 80; semente++)
        {
            var sim = new SimuladorDeTempo(ComFisica() with { Acoes = acoes }, semente, TopologiasDeExemplo.UmMonitor);
            sim.Aplicar(new CmdSummonItem(Item.Banana));
            sim.Avancar(TimeSpan.FromMinutes(5), condicao);
            if (condicao(sim.Estado) && ItemVisivel(sim.Estado)) return sim;
        }
        Afirmar.Falhar($"nenhuma semente chegou a {situacao}");
        return null!;
    }

    // DEC-022 com o atento (achado 4 da revisão adversarial): com a autonomia pausada ou o painel aberto, quem está na
    // parede ou no cipó sem estar preso desce até o chão ou se solta, mesmo se o atento o deixou agarrado. Pausar logo
    // depois de soltar o item, ou pausar com o item ainda na mão (o atento o mantém onde está até o item sair da mão), dá
    // o mesmo fim: os pés no chão, sem relógio. Preso pelo usuário, ele continua agarrado (DEC-024).
    [Teste]
    public static void Calma_DepoisDoAtento_DesceDaParedeOuSeSoltaDoCipo()
    {
        foreach (string caso in new[] { "pausa depois de soltar", "pausa com o item na mão", "painel depois de soltar" })
        {
            foreach (bool noCipo in new[] { false, true })
            {
                string onde = $"{caso}, {(noCipo ? "no cipó" : "na parede")}";
                ConfiguracaoDoNucleo cfg = ComFisica() with { PainelDeEnergiaDisponivel = true };
                SimuladorDeTempo sim = NoAltoPorContaPropria(cfg, noCipo);
                Superficies sup = Sup(sim.Estado);
                ItemNoMundo item = sim.Estado.Itens.Todos.Single();
                PontoPx noAlto = sim.Estado.Lugar!.Ancora;
                Evento calma = caso.StartsWith("painel", StringComparison.Ordinal) ? new EnergyPanelOpen() : new CmdPauseAutonomy();
                sim.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)));
                Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado, $"{onde}: atento, agarrado");
                if (caso == "pausa com o item na mão")
                {
                    sim.Aplicar(calma);
                    sim.Avancar(TimeSpan.FromMinutes(1));
                    Afirmar.Verdadeiro(sim.Estado.Movimento.Agarrado && sim.Estado.Lugar!.Ancora == noAlto, $"{onde}: com o item na mão, continua agarrado onde estava");
                    Afirmar.Falso(sim.RelogioLigado, $"{onde}: parado, sem relógio");
                    sim.Aplicar(new ItemRelease(item.Id));
                }
                else
                {
                    sim.Aplicar(new ItemRelease(item.Id));
                    sim.Aplicar(calma);
                }
                sim.Avancar(TimeSpan.FromMinutes(1));
                sim.Esta(Estado.Idle, $"{onde}: desceu ou se soltou e está parado");
                Afirmar.Igual(sup.Chao, sim.Estado.Lugar!.Ancora.Y, $"{onde}: com os pés no chão");
                if (!noCipo) Afirmar.Igual(noAlto.X, sim.Estado.Lugar.Ancora.X, $"{onde}: desceu pela mesma parede");
                Afirmar.Falso(sim.RelogioLigado || sim.Estado.PresoPeloUsuario, $"{onde}: sem relógio e sem estar preso");
            }
        }

        // Preso pelo usuário (DEC-024): o atento e a pausa não o tiram de lá.
        var preso = new SimuladorDeTempo(ComFisica(), 6, TopologiasDeExemplo.UmMonitor);
        ItemNoMundo banana = InvocarEAssentar(preso, Item.Banana);
        SoltarPersonagemEm(preso, new PontoPx(Sup(preso.Estado).Direita - 20, 600));
        preso.Esta(Estado.Climbing, "preso na parede");
        PontoPx naParede = preso.Estado.Lugar!.Ancora;
        foreach (Evento e in new Evento[] { new ItemPress(banana.Id, new PontoPx(banana.Lugar.Ancora.X, banana.Lugar.Ancora.Y - 10)), new ItemRelease(banana.Id), new CmdPauseAutonomy() })
            preso.Aplicar(e);
        preso.Avancar(TimeSpan.FromMinutes(5));
        preso.Esta(Estado.Climbing, "preso: continua na parede");
        Afirmar.Verdadeiro(preso.Estado.Movimento.Agarrado && preso.Estado.PresoPeloUsuario && preso.Estado.Lugar!.Ancora == naParede, "preso: agarrado no mesmo lugar");
    }

    /// <summary>
    /// Com a física, um item parado no chão e a agenda livre, ele escala por conta própria até bem acima do chão (na parede)
    /// ou fica pendurado no cipó, sem estar preso e sem estar agarrado.
    /// </summary>
    private static SimuladorDeTempo NoAltoPorContaPropria(ConfiguracaoDoNucleo cfg, bool noCipo)
    {
        bool NoAlto(EstadoDoNucleo s) => !s.PresoPeloUsuario && !s.Movimento.Agarrado && !s.Movimento.Foguete && !s.Itens.AlgumCaindo && s.Lugar is { } l
            && (noCipo ? s.Estado == Estado.Hanging : s.Estado == Estado.Climbing && Sup(s).Chao - l.Ancora.Y > 300);
        for (ulong semente = 1; semente <= 80; semente++)
        {
            var sim = new SimuladorDeTempo(cfg with { Acoes = AcoesAutonomas.Escalar }, semente, TopologiasDeExemplo.UmMonitor);
            InvocarEAssentar(sim, Item.Cafe);
            sim.Avancar(TimeSpan.FromMinutes(5), NoAlto);
            if (NoAlto(sim.Estado) && ItemVisivel(sim.Estado)) return sim;
        }
        Afirmar.Falhar($"nenhuma semente o levou {(noCipo ? "ao cipó" : "ao alto da parede")}");
        return null!;
    }

    /// <summary>
    /// Com a física do aplicativo e um item parado no chão, o usuário põe o personagem na parede ou no cipó (preso, DEC-024),
    /// e a agenda o faz passear pela mesma superfície: devolve o simulador no meio do passeio, sem estar agarrado.
    /// </summary>
    private static SimuladorDeTempo ChegarPresoPasseando(bool noCipo)
    {
        Estado esperado = noCipo ? Estado.Hanging : Estado.Climbing;
        bool Passeando(EstadoDoNucleo s) => s.Estado == esperado && s.PresoPeloUsuario && !s.Movimento.Agarrado;
        for (ulong semente = 1; semente <= 40; semente++)
        {
            var sim = new SimuladorDeTempo(ComFisica(), semente, TopologiasDeExemplo.UmMonitor);
            InvocarEAssentar(sim, Item.Banana);
            Superficies sup = Sup(sim.Estado);
            SoltarPersonagemEm(sim, noCipo ? new PontoPx(960, sup.Teto + 12) : new PontoPx(sup.Direita - 20, 600));
            if (sim.Estado.Estado != esperado || !sim.Estado.PresoPeloUsuario) continue;
            sim.Avancar(TimeSpan.FromMinutes(5), Passeando);
            if (Passeando(sim.Estado) && ItemVisivel(sim.Estado)) return sim;
        }
        Afirmar.Falhar($"nenhuma semente pôs o preso a passear {(noCipo ? "no cipó" : "na parede")}");
        return null!;
    }

    private static bool ItemVisivel(EstadoDoNucleo s) => s.Itens.Todos.All(i => Maquina.ItemVisivel(s, i));

    // Um PRESS noutro item com um ainda na mão (dois ponteiros, ou uma captura perdida sem aviso): o da mão é largado de
    // onde está, com a captura solta antes de tudo (L6), e o novo fica na mão; o personagem continua atento.
    [Teste]
    public static void PegarOutroItem_LargaOPrimeiroComACapturaSolta()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo primeiro = InvocarEAssentar(c, Item.Banana);
        ItemNoMundo segundo = InvocarEAssentar(c, Item.Agua);
        c.Aplicar(new ItemPress(primeiro.Id, new PontoPx(primeiro.Lugar.Ancora.X, primeiro.Lugar.Ancora.Y - 10)), new ItemDragStart(primeiro.Id),
            new ItemDragMove(primeiro.Id, new PontoPx(800, 290)));
        c.Aplicar(new ItemPress(segundo.Id, new PontoPx(segundo.Lugar.Ancora.X, segundo.Lugar.Ancora.Y - 10)));
        Afirmar.Igual(new LiberarCapturaDoItem(primeiro.Id), c.Efeitos[0], "solta a captura do primeiro antes de tudo");
        ItemNoMundo largado = c.Atual.Itens.PorId(primeiro.Id)!;
        Afirmar.Igual((SituacaoDoItem.Caindo, new PontoPx(800, 300)), (largado.Situacao, largado.Lugar.Ancora), "o primeiro cai de onde estava");
        Afirmar.Igual(segundo.Id, c.Atual.Itens.NaMao?.Id, "o segundo na mão");
        Afirmar.Verdadeiro(c.Atual.Atento && !c.Atual.DecisaoAgendada, "continua atento, sem agenda");
    }

    // Invariante 2 para o item: durante o arraste, a âncora dele é o cursor menos a pegada, sem física nem limite (até
    // fora da área útil e noutro monitor, que passa a ser o do item, com o tamanho do DPI dele). A janela segue.
    [Teste]
    public static void ArrastarItem_EhOCursorMenosAPegada()
    {
        Cenario c = Cenario.Parado(SemFisica(), TopologiasDeExemplo.EscalasMistas);
        ItemNoMundo item = InvocarEAssentar(c, Item.Cafe);
        var pegar = new PontoPx(item.Lugar.Ancora.X + 5, item.Lugar.Ancora.Y - 17);
        c.Aplicar(new ItemPress(item.Id, pegar), new ItemDragStart(item.Id));
        Afirmar.Igual(new PontoPx(5, -17), c.Atual.Itens.PorId(item.Id)!.Pegada, "a pegada");
        Afirmar.Igual(SituacaoDoItem.Arrastado, c.Atual.Itens.PorId(item.Id)!.Situacao, "arrastado");
        foreach (PontoPx cursor in new[] { new PontoPx(1000, 500), new PontoPx(3000, 900), new PontoPx(-2000, -600), new PontoPx(1000, 5000) })
        {
            c.Aplicar(new ItemDragMove(item.Id, cursor));
            ItemNoMundo agora = c.Atual.Itens.PorId(item.Id)!;
            var ancora = new PontoPx(cursor.X - 5, cursor.Y + 17);
            Afirmar.Igual(ancora, agora.Lugar.Ancora, $"cursor {cursor}: âncora = cursor − pegada");
            MonitorDoDesktop m = Maquina.MonitorDaAncora(c.Atual.Topologia!, ancora);
            Afirmar.Igual(m.Chave, agora.Lugar.Monitor.Chave, $"cursor {cursor}: o monitor da âncora");
            Afirmar.Igual(new TamanhoDip(48, 48).ParaPixels(m.Dpi), agora.Lugar.Tamanho, $"cursor {cursor}: o tamanho no DPI dele");
            Afirmar.Sequencia([new MoverItem(item.Id, agora.Lugar)], c.Efeitos, $"cursor {cursor}: a janela segue");
        }
        // Um arraste de outro Id, ou antes do DragStart, não move nada.
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(new ItemDragMove(item.Id + 1, new PontoPx(10, 10)));
        Afirmar.Igual(antes, c.Atual, "outro Id: nada muda");
    }

    // Solto longe dele (ou num estado qualquer, longe), o item é preso na área útil e cai de onde foi solto; no chão, fica.
    // A agenda volta depois do intervalo de acomodação.
    [Teste]
    public static void SoltarFora_CaiDeOndeFoiSolto()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo item = InvocarEAssentar(c, Item.Banana);
        c.ArrastarItem(item.Id, new PontoPx(500, 300)).SemTransicao();
        ItemNoMundo solto = c.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual((SituacaoDoItem.Caindo, new PontoPx(500, 300)), (solto.Situacao, solto.Lugar.Ancora), "cai de onde foi solto");
        Afirmar.Verdadeiro(c.Atual.RelogioAtivo && c.Tem<AgendarDecisao>(), "o relógio corre e a agenda volta");
        Afirmar.Verdadeiro(c.Efeito<AgendarDecisao>().Atraso >= c.Config.IntervaloDeAcomodacao, "depois do intervalo de acomodação");
        while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
        Afirmar.Igual(new PontoPx(500, 1032), c.Atual.Itens.PorId(item.Id)!.Lugar.Ancora, "no chão, na mesma coluna");
        c.Esta(Estado.Idle);

        // Solto fora da área útil, é preso nela; solto no chão, fica.
        c.ArrastarItem(item.Id, new PontoPx(-300, -800));
        Afirmar.Igual(new PontoPx(24, 48), c.Atual.Itens.PorId(item.Id)!.Lugar.Ancora, "preso no canto de cima, à esquerda");
        while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
        c.ArrastarItem(item.Id, new PontoPx(800, 1200));
        Afirmar.Igual((SituacaoDoItem.NoChao, new PontoPx(800, 1032)), (c.Atual.Itens.PorId(item.Id)!.Situacao, c.Atual.Itens.PorId(item.Id)!.Lugar.Ancora), "solto abaixo do chão, fica no chão");
        Afirmar.Falso(c.Atual.RelogioAtivo, "no chão, sem relógio");
    }

    // Tabela 4.6, um caso por estado alcançável com um item na mão: aceito, o item sai (RemoverItem Usado) e ele vai a
    // USING; recusado (pulando, caindo, pressionado, usando outro), o item cai de onde foi solto.
    [Teste]
    public static void SoltarSobreOPersonagem_TabelaDeEstados()
    {
        void Aceito(Cenario c, string caso)
        {
            ItemNoMundo item = c.Atual.Itens.NaMao ?? c.Atual.Itens.Todos[^1];
            Estado de = c.Atual.Estado;
            if (c.Atual.Itens.NaMao is null) c.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)));
            if (c.Atual.Itens.NaMao is { Situacao: SituacaoDoItem.Segurado }) c.Aplicar(new ItemDragStart(item.Id));
            PontoPx meio = Cenario.MeioDoPersonagem(c.Atual, c.Config);
            var cursor = new PontoPx(meio.X, meio.Y - 10);
            c.Aplicar(new ItemDragMove(item.Id, cursor), new ItemDragEnd(item.Id, cursor));
            c.Esta(Estado.Using, caso);
            Afirmar.Nulo(c.Atual.Itens.PorId(item.Id), $"{caso}: o item saiu");
            Afirmar.Verdadeiro(c.Efeitos.Contains(new RemoverItem(item.Id, MotivoDaRemocao.Usado)), $"{caso}: RemoverItem Usado");
            Afirmar.Igual(new Transicao(de, Estado.Using, $"ITEM_DRAG_END sobre o personagem: {c.Config.TabelaDeItens(item.Item).Verbo} {item.Item}"), c.Transicoes[^1], $"{caso}: a transição");
        }
        void Recusado(Cenario c, string caso)
        {
            ItemNoMundo item = Afirmar.NaoNulo(c.Atual.Itens.NaMao, $"{caso}: um item na mão");
            Estado de = c.Atual.Estado;
            PontoPx meio = Cenario.MeioDoPersonagem(c.Atual, c.Config);
            var cursor = new PontoPx(meio.X + item.Pegada.X, meio.Y + item.Pegada.Y);
            if (item.Situacao == SituacaoDoItem.Segurado) c.Aplicar(new ItemDragStart(item.Id));
            c.Aplicar(new ItemDragMove(item.Id, cursor), new ItemDragEnd(item.Id, cursor));
            Afirmar.Igual(de, c.Atual.Estado, $"{caso}: recusado, continua em {de}");
            ItemNoMundo depois = Afirmar.NaoNulo(c.Atual.Itens.PorId(item.Id), $"{caso}: o item continua");
            Afirmar.Igual(SituacaoDoItem.Caindo, depois.Situacao, $"{caso}: e cai de onde foi solto");
            Afirmar.Igual(meio, depois.Lugar.Ancora, $"{caso}: do meio dele");
        }
        ItemNoMundo Pegar(Cenario c)
        {
            ItemNoMundo item = c.Atual.Itens.Todos.First(i => !i.NaMao);
            c.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)));
            return item;
        }

        // Parado, no chão.
        Cenario parado = Cenario.Parado(SemFisica());
        InvocarEAssentar(parado, Item.Banana);
        Aceito(parado, "IDLE");
        Afirmar.Igual(ApoioDoUso.Chao, parado.Atual.Uso!.Apoio, "IDLE: no chão");

        // Reagindo ao clique: o item pego antes, o clique no personagem, e o item solto nele durante a reação.
        Cenario reagindo = Cenario.Parado(SemFisica());
        InvocarEAssentar(reagindo, Item.Banana);
        Pegar(reagindo);
        reagindo.Aplicar(new Press(Cenario.PontoOpaco), new Click()).Esta(Estado.Reacting);
        Aceito(reagindo, "REACTING (a reação é cortada)");

        // Pousando: o item na mão, o personagem cai (sinais de movimento) e o item é solto no pouso.
        Cenario pousando = Cenario.Parado(SemFisica() with { QuedaFisica = true, Acoes = AcoesAutonomas.Andar });
        InvocarEAssentar(pousando, Item.Cafe);
        pousando.Decidir().Esta(Estado.Walking);
        Pegar(pousando);
        pousando.Esta(Estado.Idle, "segurar um item o fez parar");
        // Sem a física, a queda só existe pelos sinais: pressionado e solto no alto, ele cai; o contato o faz pousar.
        pousando.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(new PontoPx(1000, 400)), new DragEnd(new PontoPx(1000, 400))).Esta(Estado.Falling);
        pousando.Aplicar(new MovementSignal(SinalDeMovimento.ContatoComOChao)).Esta(Estado.Landing);
        Aceito(pousando, "LANDING (o pouso é cortado)");

        // Escondido na borda (DEC-025).
        Cenario escondido = Cenario.Parado(SemFisica() with { EsconderijoNoCliqueDuplo = true });
        InvocarEAssentar(escondido, Item.Vodka);
        escondido.Aplicar(new Press(Cenario.PontoOpaco), new Click(), new Press(Cenario.PontoOpaco), new DoubleClick()).Esta(Estado.Peeking);
        Aceito(escondido, "PEEKING");
        Afirmar.Igual(ApoioDoUso.Esconderijo, escondido.Atual.Uso!.Apoio, "PEEKING: no esconderijo");

        // Na parede e no cipó, sem a física: os sinais de movimento o levam até lá (no chão: o apoio é o chão).
        foreach (Estado alvo in new[] { Estado.Climbing, Estado.Hanging })
        {
            Cenario c = Cenario.Em(alvo, SemFisica());
            InvocarEAssentar(c, Item.Agua);
            Pegar(c);
            c.Esta(alvo, "segurar não o tira da parede nem do cipó");
            Aceito(c, alvo.ToString());
        }

        // Recusados: caindo, pulando, pressionado, arrastado e usando outro item.
        foreach (Estado alvo in new[] { Estado.Falling, Estado.Jumping })
        {
            Cenario c = Cenario.Em(alvo, SemFisica() with { QuedaFisica = true });
            InvocarEAssentar(c, Item.Agua);
            Pegar(c);
            Recusado(c, alvo.ToString());
        }
        Cenario pressionado = Cenario.Parado(SemFisica());
        InvocarEAssentar(pressionado, Item.Agua);
        Pegar(pressionado);
        pressionado.Aplicar(new Press(Cenario.PontoOpaco)).Esta(Estado.Pressed);
        Recusado(pressionado, "PRESSED");
        pressionado.Aplicar(new DragStart()).Esta(Estado.Dragging);
        Pegar(pressionado);
        Recusado(pressionado, "DRAGGING");

        Cenario usando = Cenario.Em(Estado.Using);
        InvocarEAssentar(usando, Item.Cerveja);
        Pegar(usando);
        Recusado(usando, "USING");
        Afirmar.Igual(Item.Banana, usando.Atual.Uso!.Item, "USING: continua usando o primeiro");
    }

    // Um clique no item, um clique duplo ou a captura perdida (ITEM_RELEASE), mesmo em cima dele, nunca usa o item: ele cai
    // de onde está.
    [Teste]
    public static void ReleaseSobreOPersonagem_NaoUsa()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo item = InvocarEAssentar(c, Item.Vodka);
        PontoPx meio = Cenario.MeioDoPersonagem(c.Atual, c.Config);
        c.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)), new ItemDragStart(item.Id),
            new ItemDragMove(item.Id, new PontoPx(meio.X, meio.Y - 10)), new ItemRelease(item.Id));
        c.Esta(Estado.Idle, "não usou");
        ItemNoMundo solto = c.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual((SituacaoDoItem.Caindo, meio), (solto.Situacao, solto.Lugar.Ancora), "cai de cima dele");
        Afirmar.Falso(c.Atual.Atento, "deixou de estar atento");

        // Clique: pegar e largar no mesmo lugar, no chão: fica.
        while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
        ItemNoMundo noChao = c.Atual.Itens.PorId(item.Id)!;
        c.Aplicar(new ItemPress(item.Id, new PontoPx(noChao.Lugar.Ancora.X, noChao.Lugar.Ancora.Y - 5)), new ItemRelease(item.Id));
        ItemNoMundo clicado = c.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual((SituacaoDoItem.NoChao, noChao.Lugar), (clicado.Situacao, clicado.Lugar), "o clique no item não o tira do lugar");
        Afirmar.Sequencia([], DosItens(c.Efeitos), "nem mexe na janela");
        // Um release de outro Id não faz nada.
        c.Aplicar(new ItemPress(item.Id, new PontoPx(noChao.Lugar.Ancora.X, noChao.Lugar.Ancora.Y - 5)), new ItemRelease(item.Id + 5));
        Afirmar.Verdadeiro(c.Atual.Atento, "o release de outro Id não solta o item");
    }

    // Robustez (achado 8 da revisão adversarial): um ITEM_DRAG_END sem o ITEM_DRAG_START antes (o árbitro manda os dois,
    // mas uma falha da raiz não pode deixar o personagem atento e sem agenda para sempre) larga o item de onde ele está,
    // como o ITEM_RELEASE: ele nunca é usado, mesmo com o cursor sobre o personagem, sai da mão sem captura a soltar (o
    // gesto acabou), e a agenda volta depois do intervalo de acomodação. O de outro Id não faz nada.
    [Teste]
    public static void DragEndSemDragStart_LargaOItem()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo item = InvocarEAssentar(c, Item.Banana);
        PontoPx meio = Cenario.MeioDoPersonagem(c.Atual, c.Config);
        c.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)));
        Afirmar.Verdadeiro(c.Atual.Atento && !c.Atual.DecisaoAgendada, "atento, sem agenda");
        EstadoDoNucleo segurando = c.Atual;
        c.Aplicar(new ItemDragEnd(item.Id + 1, meio));
        Afirmar.Igual(segurando with { Sinal = Sinal.Nenhum }, c.Atual, "o fim do gesto de outro Id não faz nada");

        c.Aplicar(new ItemDragEnd(item.Id, meio)).SemTransicao();
        c.Esta(Estado.Idle, "não usou");
        ItemNoMundo largado = Afirmar.NaoNulo(c.Atual.Itens.PorId(item.Id), "o item continua");
        Afirmar.Igual((SituacaoDoItem.NoChao, item.Lugar), (largado.Situacao, largado.Lugar), "largado de onde estava, no chão");
        Afirmar.Falso(c.Atual.Atento, "deixou de estar atento");
        Afirmar.Falso(c.Tem<LiberarCapturaDoItem>(), "o gesto acabou: não há captura a soltar");
        Afirmar.Verdadeiro(c.Efeito<AgendarDecisao>().Atraso >= c.Config.IntervaloDeAcomodacao, "a agenda volta depois do intervalo de acomodação");
    }

    // ---------------------------------------------------------------- esconder, topologia, tela cheia, recolher

    // D18 e 4.8: ao esconder, o item da mão solta a captura (antes de tudo) e fica no chão, na coluna em que estava, os que
    // caem vão direto ao chão, e todas as janelas somem; ao mostrar, voltam no chão, sem relógio.
    [Teste]
    public static void EsconderEMostrar_ItensSomemEVoltam()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo parado = InvocarEAssentar(c, Item.Banana);
        ItemNoMundo naMao = InvocarEAssentar(c, Item.Agua);
        c.Aplicar(new ItemPress(naMao.Id, new PontoPx(naMao.Lugar.Ancora.X, naMao.Lugar.Ancora.Y - 10)), new ItemDragStart(naMao.Id), new ItemDragMove(naMao.Id, new PontoPx(700, 200)));
        c.Aplicar(new CmdSummonItem(Item.Vodka));
        int caindo = c.Atual.ProximoIdDeItem - 1;
        Afirmar.Verdadeiro(c.Atual.Itens.PorId(caindo)!.Situacao == SituacaoDoItem.Caindo && c.Atual.RelogioAtivo, "um caindo, com o relógio");

        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        Afirmar.Igual(new LiberarCapturaDoItem(naMao.Id), c.Efeitos[0], "solta a captura do item antes de tudo");
        Afirmar.Sequencia([new LiberarCapturaDoItem(naMao.Id), new EsconderItem(parado.Id), new EsconderItem(naMao.Id), new EsconderItem(caindo)],
            DosItens(c.Efeitos), "todas as janelas somem");
        // A janela dele vem antes das dos itens (desenho 3.7): a raiz põe as dos itens abaixo dela na ordem Z (L17).
        Afirmar.Verdadeiro(Antes<EsconderJanela, EsconderItem>(c.Efeitos), $"a janela dele some antes das dos itens: {string.Join(", ", c.Efeitos)}");
        Afirmar.Verdadeiro(c.Atual.Itens.Todos.All(i => i.Situacao == SituacaoDoItem.NoChao && i.Lugar.Ancora.Y == 1032), "todos no chão");
        Afirmar.Igual(new PontoPx(700, 1032), c.Atual.Itens.PorId(naMao.Id)!.Lugar.Ancora, "o da mão, na coluna em que estava");
        Afirmar.Igual(parado.Lugar, c.Atual.Itens.PorId(parado.Id)!.Lugar, "o parado não muda");
        Afirmar.Igual(1536, c.Atual.Itens.PorId(caindo)!.Lugar.Ancora.X, "o que caía, na mesma coluna");
        Afirmar.Falso(c.Atual.RelogioAtivo || c.Atual.Atento, "sem relógio, sem item na mão");

        c.Aplicar(new CmdShow()).Esta(Estado.Idle);
        Afirmar.Sequencia(c.Atual.Itens.Todos.Select(i => (Efeito)new MostrarItem(i.Id, i.Item, i.Lugar)), DosItens(c.Efeitos), "as janelas voltam, no chão");
        Afirmar.Verdadeiro(Antes<MoverJanela, MostrarItem>(c.Efeitos) && Antes<MostrarJanela, MostrarItem>(c.Efeitos), $"a janela dele aparece antes das dos itens: {string.Join(", ", c.Efeitos)}");
        Afirmar.Falso(c.Atual.RelogioAtivo, "sem relógio");
    }

    /// <summary>Se há efeitos dos dois tipos e o primeiro <typeparamref name="TA"/> vem antes do primeiro <typeparamref name="TB"/>.</summary>
    private static bool Antes<TA, TB>(IReadOnlyList<Efeito> efeitos) where TA : Efeito where TB : Efeito
    {
        int a = -1, b = -1;
        for (int i = 0; i < efeitos.Count; i++)
        {
            if (a < 0 && efeitos[i] is TA) a = i;
            if (b < 0 && efeitos[i] is TB) b = i;
        }
        return a >= 0 && b >= 0 && a < b;
    }

    // 4.8: depois de uma mudança de topologia, os itens fora da mão são reacomodados como o personagem; sem o monitor deles,
    // vão para o mais próximo, e no chão continuam no chão.
    [Teste]
    public static void TopologiaMuda_ItensReacomodados()
    {
        Cenario c = Cenario.Parado(SemFisica(), TopologiasDeExemplo.LadoALado);
        ItemNoMundo item = InvocarEAssentar(c, Item.Cogumelo);
        c.ArrastarItem(item.Id, new PontoPx(2900, 1032));
        Afirmar.Igual((TopologiasDeExemplo.Display2, new PontoPx(2900, 1032)), (c.Atual.Itens.PorId(item.Id)!.Lugar.Monitor.Chave, c.Atual.Itens.PorId(item.Id)!.Lugar.Ancora), "no chão do monitor 2");
        ItemNoMundo outro = InvocarEAssentar(c, Item.Banana);

        // A barra do monitor 1 sobe para 980: o item no chão continua no chão (a posição relativa).
        Topologia barraMaisAlta = TopologiasDeExemplo.ComMonitor(TopologiasDeExemplo.LadoALado, TopologiasDeExemplo.Display1,
            m => m with { AreaUtil = TopologiasDeExemplo.Ret(0, 0, 1920, 980) });
        c.Aplicar(new TopologyChanged(barraMaisAlta));
        Afirmar.Igual(new PontoPx(outro.Lugar.Ancora.X, 980), c.Atual.Itens.PorId(outro.Id)!.Lugar.Ancora, "no chão novo");
        Afirmar.Igual(SituacaoDoItem.NoChao, c.Atual.Itens.PorId(outro.Id)!.Situacao, "parado");
        Afirmar.Verdadeiro(c.Efeitos.Contains(new MoverItem(outro.Id, c.Atual.Itens.PorId(outro.Id)!.Lugar)), "a janela acompanha");
        Afirmar.Igual(new PontoPx(2900, 1032), c.Atual.Itens.PorId(item.Id)!.Lugar.Ancora, "o do outro monitor não muda");

        // O monitor 2 sai: o item vai para o 1, na mesma posição relativa, no chão.
        c.Aplicar(new TopologyChanged(TopologiasDeExemplo.SemMonitor(barraMaisAlta, TopologiasDeExemplo.Display2)));
        ItemNoMundo movido = c.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual(TopologiasDeExemplo.Display1, movido.Lugar.Monitor.Chave, "no monitor que sobrou");
        Afirmar.Igual((980, SituacaoDoItem.NoChao), (movido.Lugar.Ancora.Y, movido.Situacao), "no chão");
        Afirmar.Verdadeiro(movido.Lugar.Monitor.AreaUtil.Contem(movido.Lugar.Retangulo), "inteiro na área útil");
        Afirmar.Falso(c.Atual.RelogioAtivo, "sem relógio");
    }

    // L4, L5 e D18: um item fora da mão num monitor ocupado pela tela cheia (com o modo ligado) perde a janela, e, se caía,
    // vai direto ao chão (o relógio não corre por ele); o item na mão sempre aparece, mesmo sobre o monitor ocupado; solto
    // lá no ar, some e vai ao chão. Quando a tela cheia acaba, as janelas voltam.
    [Teste]
    public static void TelaCheia_ItemNoMonitorOcupadoSemJanela()
    {
        Cenario c = Cenario.Parado(SemFisica(), TopologiasDeExemplo.LadoALado);
        ItemNoMundo item = InvocarEAssentar(c, Item.Energetico);
        c.ArrastarItem(item.Id, new PontoPx(2500, 400));
        Afirmar.Verdadeiro(c.Atual.Itens.PorId(item.Id)!.Situacao == SituacaoDoItem.Caindo && c.Atual.RelogioAtivo, "caindo no monitor 2");

        c.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([TopologiasDeExemplo.Display2])));
        c.Esta(Estado.Idle, "o personagem, no monitor 1, não se move");
        ItemNoMundo escondido = c.Atual.Itens.PorId(item.Id)!;
        Afirmar.Igual((SituacaoDoItem.NoChao, new PontoPx(2500, 1032)), (escondido.Situacao, escondido.Lugar.Ancora), "foi direto ao chão (L5)");
        Afirmar.Sequencia([new EsconderItem(item.Id)], DosItens(c.Efeitos), "a janela some");
        Afirmar.Falso(c.Atual.RelogioAtivo, "o relógio não corre por um item que não se vê");

        // Na mão, o item aparece mesmo sobre o monitor ocupado (L4); solto no ar lá, some e vai ao chão.
        ItemNoMundo outro = InvocarEAssentar(c, Item.Agua);
        c.Aplicar(new ItemPress(outro.Id, new PontoPx(outro.Lugar.Ancora.X, outro.Lugar.Ancora.Y - 10)), new ItemDragStart(outro.Id), new ItemDragMove(outro.Id, new PontoPx(3000, 300)));
        Afirmar.Sequencia([new MoverItem(outro.Id, c.Atual.Itens.PorId(outro.Id)!.Lugar)], DosItens(c.Efeitos), "na mão, sobre o monitor ocupado, continua à vista");
        Afirmar.Verdadeiro(Maquina.ItemVisivel(c.Atual, c.Atual.Itens.PorId(outro.Id)!), "visível na mão");
        c.Aplicar(new ItemDragEnd(outro.Id, new PontoPx(3000, 300)));
        Afirmar.Igual((SituacaoDoItem.NoChao, new PontoPx(3000, 1032)), (c.Atual.Itens.PorId(outro.Id)!.Situacao, c.Atual.Itens.PorId(outro.Id)!.Lugar.Ancora), "solto no ar sobre o ocupado: no chão");
        Afirmar.Sequencia([new EsconderItem(outro.Id)], DosItens(c.Efeitos), "e some");
        Afirmar.Falso(c.Atual.RelogioAtivo, "sem relógio");

        // Um item no ocupado não pode ser pego (sem janela).
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(new ItemPress(item.Id, new PontoPx(2500, 1020)));
        Afirmar.Igual(antes, c.Atual, "o item sem janela não é pego");

        c.Aplicar(new FullscreenTargetsChanged(MonitoresOcupados.Nenhum));
        Afirmar.Sequencia([new MostrarItem(item.Id, Item.Energetico, c.Atual.Itens.PorId(item.Id)!.Lugar), new MostrarItem(outro.Id, Item.Agua, c.Atual.Itens.PorId(outro.Id)!.Lugar)],
            DosItens(c.Efeitos), "as janelas voltam");

        // Com o modo desligado, a tela cheia não esconde nada.
        Cenario semModo = Cenario.Parado(SemFisica(), TopologiasDeExemplo.LadoALado).Aplicar(new SettingsChanged(new Preferencias(NivelDeEnergia.Media, false)));
        ItemNoMundo noDois = InvocarEAssentar(semModo, Item.Bala);
        semModo.ArrastarItem(noDois.Id, new PontoPx(2500, 1032));
        semModo.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados([TopologiasDeExemplo.Display2])));
        Afirmar.Sequencia([], DosItens(semModo.Efeitos), "com o modo desligado, a janela fica");
    }

    // L6: "Recolher itens" tira todos, e o da mão solta a captura antes; ele deixa de estar atento e a agenda volta.
    [Teste]
    public static void RecolherItens_TodosSomem()
    {
        Cenario c = Cenario.Parado(SemFisica());
        foreach (Item i in new[] { Item.Banana, Item.Baseado, Item.Cigarro }) InvocarEAssentar(c, i);
        ItemNoMundo naMao = c.Atual.Itens.PorId(2)!;
        c.Aplicar(new ItemPress(2, new PontoPx(naMao.Lugar.Ancora.X, naMao.Lugar.Ancora.Y - 10)));
        Afirmar.Verdadeiro(c.Atual.Atento && !c.Atual.DecisaoAgendada, "atento, sem agenda");
        c.Aplicar(new CmdClearItems()).SemTransicao();
        Afirmar.Sequencia([new LiberarCapturaDoItem(2), new RemoverItem(1, MotivoDaRemocao.Recolhido), new RemoverItem(2, MotivoDaRemocao.Recolhido), new RemoverItem(3, MotivoDaRemocao.Recolhido)],
            DosItens(c.Efeitos), "solta a captura e fecha as três janelas");
        Afirmar.Igual(new LiberarCapturaDoItem(2), c.Efeitos[0], "a captura antes de tudo");
        Afirmar.Igual(0, c.Atual.Itens.Quantidade, "nenhum item sobra");
        Afirmar.Falso(c.Atual.Atento, "deixou de estar atento");
        Afirmar.Verdadeiro(c.Efeito<AgendarDecisao>().Atraso >= c.Config.IntervaloDeAcomodacao, "a agenda volta");
        Afirmar.Igual(4, c.Atual.ProximoIdDeItem, "os Ids continuam");
        c.Aplicar(new CmdClearItems()).SemTransicao();
        Afirmar.Sequencia([], c.Efeitos, "sem itens, nada");
    }

    // D17 e invariante 23: nada autônomo, do relógio ou do sistema invoca um item: meia hora de agenda livre, com interações
    // sorteadas e o tamagotchi ligado, sem nenhum item e sem nenhum efeito de janela de item.
    [Teste]
    public static void SemComando_NenhumItemEm30Min()
    {
        var rnd = new Random(2031);
        var sim = new SimuladorDeTempo(ComFisica(), 77, TopologiasDeExemplo.UmMonitor);
        long eventos = 0;
        sim.AoResultado = (_, e, r) =>
        {
            eventos++;
            Afirmar.Igual(0, r.Estado.Itens.Quantidade, $"{e}: nenhum item");
            Afirmar.Sequencia([], DosItens(r.Efeitos), $"{e}: nenhuma janela de item");
        };
        while (sim.AgoraMs < 30 * 60 * 1000)
        {
            sim.Avancar(TimeSpan.FromSeconds(rnd.Next(5, 90)));
            if (sim.Estado.Lugar is { } l && sim.Estado.Estado != Estado.Hidden)
            {
                var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 20);
                var alvo = new PontoPx(rnd.Next(0, 1920), rnd.Next(60, 1032));
                Evento[] interacao = rnd.Next(5) switch
                {
                    0 => [new Press(corpo), new Click()],
                    1 => [new Press(corpo), new DragStart(), new DragMove(alvo), new DragEnd(alvo)],
                    2 => [new CmdHide(), new CmdShow()],
                    _ => [],
                };
                foreach (Evento e in interacao) sim.Aplicar(e);
            }
        }
        Afirmar.Igual(1, sim.Estado.ProximoIdDeItem, "nenhum Id usado");
        Afirmar.Verdadeiro(eventos > 1000, $"a simulação andou ({eventos} eventos)");
    }

    // ---------------------------------------------------------------- gravação e retrato

    // As reproduções: cada evento dos itens é escrito e lido de volta igual; os efeitos das janelas têm a linha canônica; e o
    // retrato mostra o uso, a onda de fundo e os itens só quando há.
    [Teste]
    public static void Gravacao_EventosEfeitosERetrato()
    {
        Topologia umMonitor = TopologiasDeExemplo.UmMonitor;
        EstadoDoNucleo inicial = EstadoDoNucleo.Inicial(1);
        (Evento Evento, string Linha)[] eventos =
        [
            (new CmdSummonItem(Item.LancaPerfume), "CmdSummonItem item=LancaPerfume"),
            (new CmdClearItems(), "CmdClearItems"),
            (new ItemPress(3, new PontoPx(-5, 7)), "ItemPress id=3 x=-5 y=7"),
            (new ItemDragStart(3), "ItemDragStart id=3"),
            (new ItemDragMove(3, new PontoPx(100, 200)), "ItemDragMove id=3 x=100 y=200"),
            (new ItemDragEnd(3, new PontoPx(101, 202)), "ItemDragEnd id=3 x=101 y=202"),
            (new ItemRelease(3), "ItemRelease id=3"),
        ];
        foreach ((Evento e, string linha) in eventos)
        {
            Afirmar.Igual(linha, Gravacao.Escrever(e, _ => "UmMonitor"), $"escrita de {e}");
            Afirmar.Igual(e, Gravacao.Ler(linha, _ => umMonitor, inicial).Single(), $"leitura de {linha}");
        }
        Afirmar.Lanca<FormatException>(() => Gravacao.Ler("ItemPress id=3", _ => umMonitor, inicial), "ItemPress sem o cursor");
        Afirmar.Lanca<FormatException>(() => Gravacao.Ler("ItemRelease", _ => umMonitor, inicial), "ItemRelease sem o Id");
        // O item também pela lista fechada do que a gravação escreve (achado 9 da revisão adversarial): o nome exato, ou o
        // número de um valor fora do enum; nem listas, nem o número de um item com nome, nem o nome com outra caixa.
        Afirmar.Igual(new CmdSummonItem((Item)99), Gravacao.Ler("CmdSummonItem item=99", _ => umMonitor, inicial).Single(), "fora do enum, pelo número");
        foreach (string invalido in new[] { "Banana,Agua", "1", "+2", "Lancaperfume" })
            Afirmar.Lanca<FormatException>(() => Gravacao.Ler($"CmdSummonItem item={invalido}", _ => umMonitor, inicial), $"item={invalido}");

        MonitorDoDesktop m = umMonitor.Principal;
        var lugar = new Posicionamento(m, new PontoPx(1728, 1032), new TamanhoPx(48, 48), new RetanguloPx(1704, 984, 1752, 1032));
        Afirmar.Igual(@"MostrarItem id=1 item=Banana monitor=\\.\DISPLAY1 ancora=(1728,1032) retangulo=(1704,984)-(1752,1032)", Gravacao.DescreverEfeito(new MostrarItem(1, Item.Banana, lugar)), "mostrar");
        Afirmar.Igual(@"MoverItem id=1 monitor=\\.\DISPLAY1 ancora=(1728,1032)", Gravacao.DescreverEfeito(new MoverItem(1, lugar)), "mover");
        Afirmar.Igual("EsconderItem id=1", Gravacao.DescreverEfeito(new EsconderItem(1)), "esconder");
        Afirmar.Igual("RemoverItem id=1 motivo=Usado", Gravacao.DescreverEfeito(new RemoverItem(1, MotivoDaRemocao.Usado)), "remover");
        Afirmar.Igual("LiberarCapturaDoItem id=1", Gravacao.DescreverEfeito(new LiberarCapturaDoItem(1)), "liberar a captura");

        // O retrato: sem itens, sem uso e sem onda de fundo, a linha de antes.
        Cenario c = Cenario.Parado(SemFisica());
        string linhaDeAntes = c.Retrato.Descrever();
        Afirmar.Falso(linhaDeAntes.Contains("itens=", StringComparison.Ordinal) || linhaDeAntes.Contains("uso=", StringComparison.Ordinal) || linhaDeAntes.Contains("fundo=", StringComparison.Ordinal), linhaDeAntes);
        InvocarEAssentar(c, Item.Banana);
        Afirmar.Verdadeiro(c.Retrato.Descrever().EndsWith(" sinal=Nenhum itens=[1:Banana:NoChao:(1728,1032)]", StringComparison.Ordinal), c.Retrato.Descrever());
        Afirmar.Igual(c.Atual.Itens, c.Retrato.Itens, "o retrato leva os itens");
        c.SoltarSobreEle(1);
        Afirmar.Verdadeiro(c.Retrato.Descrever().EndsWith(" sinal=Nenhum onda=Satisfeito/Subida/1 uso=Banana/Comer/0de150/Chao", StringComparison.Ordinal), c.Retrato.Descrever());
        Afirmar.Igual((new Uso(Item.Banana, VerboDeUso.Comer, 150, ApoioDoUso.Chao), 0), (c.Retrato.Uso, c.Retrato.PassoDoUso), "o retrato leva o uso");
        c.Passos(37);
        Afirmar.Igual(37, c.Retrato.PassoDoUso, "o passo do uso");
        Afirmar.Contem(" uso=Banana/Comer/37de150/Chao", c.Retrato.Descrever(), "na linha");
        EstadoDoNucleo comFundo = c.Atual with { OndaDeFundo = new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 2, 2), Preferencias = c.Atual.Preferencias with { EmocaoDominante = Expressao.Feliz } };
        Afirmar.Verdadeiro(comFundo.Retrato().Descrever().EndsWith(" onda=Satisfeito/Subida/1 fundo=Tonto/Subida/2 uso=Banana/Comer/37de150/Chao emocao=Feliz", StringComparison.Ordinal), comFundo.Retrato().Descrever());
        Afirmar.Igual(comFundo.OndaDeFundo, comFundo.Retrato().OndaDeFundo, "o retrato leva a onda de fundo");
    }
}
