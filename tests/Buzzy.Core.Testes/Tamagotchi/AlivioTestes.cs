using System.Globalization;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// O alívio (pedido do usuário de 2026-10-01; decisão do coordenador para a DEC-028): comer e beber algo sem álcool acalma
/// a onda de desenho animado aos poucos, um passo por item. A banana, o café e o energético, com uma onda de substância na
/// frente, aliviam um passo e não começam a onda deles; sem onda, ou com uma onda leve na frente, fazem o de sempre (a
/// combinação 4.5). A água alivia um passo qualquer onda da frente. Um passo: na subida ou no pico acima do nível 1, um
/// nível abaixo, na mesma fase e com o temporizador em curso; no nível 1, a queda, com a duração cheia e a cara dela (sem
/// queda, o fim); na queda, o fim. A onda de fundo nunca é tocada, e volta quando a da frente acaba. Os itens de
/// substância ficam como estão. A bala saiu do alívio no mesmo dia: é droga sintética (palavras do usuário), começa o
/// eufórico e é acalmada pela comida e pela bebida como as outras substâncias. O esperado vem da decisão e da transcrição
/// das tabelas (<see cref="TabelasDoDesenho"/>), à parte do núcleo. Tudo é de desenho animado: comer e beber água
/// "acalmam" a onda.
/// </summary>
internal static class AlivioTestes
{
    /// <summary>
    /// Sem a física e com a paranoia de fora (<see cref="ApoioDosItens.NuncaParanoia"/>): nos casos daqui que misturam uma
    /// droga sintética com outra substância (o que sorteia a paranoia), o teste fala só do alívio e da combinação; a
    /// paranoia tem os testes dela (<see cref="ParanoiaTestes"/>).
    /// </summary>
    private static ConfiguracaoDoNucleo SemParanoia() => SemFisica() with { ChanceDaParanoia = NuncaParanoia };

    /// <summary>
    /// O que um item solto sobre ele fez: o estado antes do gesto (com a onda semeada e o temporizador dela agendado), o
    /// estado logo antes do soltar (o ITEM_DRAG_END), o resultado do soltar e o estado no fim do uso.
    /// </summary>
    private sealed record Usado(EstadoDoNucleo Antes, EstadoDoNucleo AntesDoSoltar, Resultado Soltar, EstadoDoNucleo DepoisDoUso);

    /// <summary>
    /// A bancada de cada caso: parado no chão, com a autonomia pausada (nada autônomo acontece) e o item já invocado e
    /// assentado no chão, ao lado dele. Devolve o simulador e o Id do item.
    /// </summary>
    private static (SimuladorDeTempo Sim, int Id) Bancada(Item item)
    {
        var sim = new SimuladorDeTempo(SemFisica(), 7, TopologiasDeExemplo.UmMonitor);
        sim.Aplicar(new CmdPauseAutonomy());
        return (sim, InvocarEAssentar(sim, item).Id);
    }

    /// <summary>
    /// A partir da bancada, semeia a onda da frente e a de fundo, como se itens as tivessem começado (com a cara da fase da
    /// frente), e o temporizador dela (o primeiro disparo, sem nada agendado, só agenda); solta o item sobre ele e deixa o
    /// uso acabar.
    /// </summary>
    private static Usado Usar(SimuladorDeTempo bancada, int id, EstadoDaOnda? frente, EstadoDaOnda? fundo)
    {
        SimuladorDeTempo sim = bancada.Semeado(s => s with { Onda = frente, OndaDeFundo = fundo, Expressao = frente is { } f ? CaraEsperada(f) : s.Expressao });
        sim.Aplicar(new ItemEffectTimer(sim.Estado.GeracaoDaOnda));
        EstadoDoNucleo antes = sim.Estado;
        Resultado? soltar = null;
        EstadoDoNucleo? antesDoSoltar = null;
        sim.AoResultado = (anterior, e, r) =>
        {
            if (e is not ItemDragEnd) return;
            soltar = r;
            antesDoSoltar = anterior;
        };
        SoltarSobreEle(sim, id);
        sim.AoResultado = null;
        Resultado resultado = Afirmar.NaoNulo(soltar, "o resultado do soltar");
        Afirmar.Igual(Estado.Using, resultado.Estado.Estado, "solto sobre ele, ele usa o item");
        sim.Passos(sim.Estado.PassosRestantes);
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "o uso acabou");
        return new Usado(antes, Afirmar.NaoNulo(antesDoSoltar, "o estado logo antes do soltar"), resultado, sim.Estado);
    }

    // ---------------------------------------------------------------- o que a decisão diz, escrito aqui à parte

    /// <summary>
    /// Um passo do alívio, pela decisão: na queda, o fim (nulo); na subida ou no pico acima do nível 1, um nível abaixo, na
    /// mesma fase e com o mesmo pior; no nível 1, a queda (nível 1, o mesmo pior), ou o fim, se a onda não tem queda.
    /// </summary>
    private static EstadoDaOnda? UmPassoAbaixo(EstadoDaOnda onda)
    {
        if (onda.Fase == FaseDaOnda.Queda) return null;
        if (onda.Nivel > 1) return onda with { Nivel = onda.Nivel - 1 };
        return TabelasDoDesenho.Esperada(onda.Tipo).QuedaBase is null ? null : onda with { Fase = FaseDaOnda.Queda, Nivel = 1 };
    }

    /// <summary>A duração cheia da fase, pela transcrição: a subida, um nível do pico, ou a queda pelo pior nível (base × 100, 125 ou 150%).</summary>
    private static TimeSpan DuracaoCheia(EstadoDaOnda onda)
    {
        TabelasDoDesenho.OndaEsperada e = TabelasDoDesenho.Esperada(onda.Tipo);
        return onda.Fase switch
        {
            FaseDaOnda.Subida => e.Subida,
            FaseDaOnda.Pico => e.NivelDoPico,
            _ => TimeSpan.FromTicks(Afirmar.NaoNulo(e.QuedaBase, $"{onda.Tipo} tem queda").Ticks * (100 + 25 * (onda.Pior - 1)) / 100),
        };
    }

    /// <summary>A cara de cada fase, pela transcrição (4.2): a queda sem cara própria fica com a do pico.</summary>
    private static Expressao CaraEsperada(EstadoDaOnda onda)
    {
        TabelasDoDesenho.OndaEsperada e = TabelasDoDesenho.Esperada(onda.Tipo);
        return onda.Fase switch
        {
            FaseDaOnda.Subida => e.CaraDaSubida,
            FaseDaOnda.Pico => e.CaraDoPico,
            _ => e.CaraDaQueda ?? e.CaraDoPico,
        };
    }

    /// <summary>Todas as fases e níveis de uma onda: a subida e o pico nos níveis 1 a 3, com cada pior possível, e a queda com o pior de 1 a 3, se houver.</summary>
    private static IEnumerable<EstadoDaOnda> FasesENiveis(Onda tipo)
    {
        foreach (FaseDaOnda fase in new[] { FaseDaOnda.Subida, FaseDaOnda.Pico })
        {
            for (int nivel = 1; nivel <= 3; nivel++)
            {
                for (int pior = nivel; pior <= 3; pior++) yield return new EstadoDaOnda(tipo, fase, nivel, pior);
            }
        }
        if (TabelasDoDesenho.Esperada(tipo).QuedaBase is not null)
        {
            for (int pior = 1; pior <= 3; pior++) yield return new EstadoDaOnda(tipo, FaseDaOnda.Queda, 1, pior);
        }
    }

    private static string Descrever(EstadoDaOnda? onda) => onda is null ? "nenhuma" : string.Create(CultureInfo.InvariantCulture, $"{onda.Tipo}/{onda.Fase}/{onda.Nivel}");

    private static TabelasDoDesenho.ItemEsperado Esperado(Item item) => TabelasDoDesenho.ItensEsperados().Single(i => i.Item == item);

    /// <summary>A regra do soltar com o alívio: "ITEM_DRAG_END sobre o personagem: Comer Banana; alivia Bebado/Pico/2 -> Bebado/Pico/1".</summary>
    private static string RegraDoAlivio(Item item, EstadoDaOnda frente, EstadoDaOnda? fundo)
    {
        string fim = UmPassoAbaixo(frente) is { } passo ? Descrever(passo) : fundo is null ? "fim" : $"fim; a de fundo volta: {Descrever(fundo)}";
        return $"ITEM_DRAG_END sobre o personagem: {Esperado(item).Verbo} {item}; alivia {Descrever(frente)} -> {fim}";
    }

    /// <summary>
    /// Confere um alívio pela regra da decisão: a frente um passo abaixo (ou, no fim dela, a de fundo de volta), a de fundo
    /// intacta, nenhuma onda do item, nenhum sorteio (o gerador logo depois do soltar é o de logo antes), o temporizador (em
    /// curso quando só o nível cai; recomeçado com a duração cheia na queda ou na volta da de fundo; cancelado sem onda), a
    /// transição, a cara de quem usa durante o uso e, no fim dele, a cara da fase (sem onda, a de quem usou fica, como
    /// depois de uma reação).
    /// </summary>
    private static void ConferirAlivio(Item item, Usado u, string caso)
    {
        EstadoDaOnda frente = Afirmar.NaoNulo(u.Antes.Onda, $"{caso}: com onda na frente");
        EstadoDaOnda? fundo = u.Antes.OndaDeFundo;
        EstadoDoNucleo depois = u.Soltar.Estado;
        EstadoDaOnda? passo = UmPassoAbaixo(frente);
        EstadoDaOnda? novaFrente = passo ?? fundo;
        EstadoDaOnda? novoFundo = passo is null ? null : fundo;

        Afirmar.Igual((novaFrente, novoFundo), (depois.Onda, depois.OndaDeFundo), $"{caso}: um passo abaixo, com a de fundo intacta (ou de volta, se a da frente acabou)");
        Afirmar.Igual(u.AntesDoSoltar.Aleatorio, depois.Aleatorio, $"{caso}: o alívio não sorteia nada (o gerador fica o mesmo)");
        if (Esperado(item).Onda is { } propria && fundo?.Tipo != propria)
            Afirmar.Falso(depois.Onda?.Tipo == propria || depois.OndaDeFundo?.Tipo == propria, $"{caso}: a onda do item ({propria}) não começa");
        if (novaFrente is not null) Afirmar.Verdadeiro(novaFrente.Nivel <= frente.Nivel || novaFrente == fundo, $"{caso}: o alívio nunca sobe o nível");

        AgendarOnda[] agendas = [.. u.Soltar.Efeitos.OfType<AgendarOnda>()];
        int cancelamentos = u.Soltar.Efeitos.OfType<CancelarOnda>().Count();
        if (passo is not null && passo.Fase == frente.Fase)
        {
            Afirmar.Igual((0, 0), (agendas.Length, cancelamentos), $"{caso}: só o nível cai, e o temporizador em curso continua");
            Afirmar.Verdadeiro(depois.OndaAgendada && depois.GeracaoDaOnda == u.Antes.GeracaoDaOnda, $"{caso}: o mesmo disparo pendente, na mesma geração");
        }
        else if (novaFrente is not null)
        {
            Afirmar.Sequencia([new AgendarOnda(DuracaoCheia(novaFrente), u.Antes.GeracaoDaOnda + 1)], agendas, $"{caso}: a fase nova recomeça com a duração cheia");
            Afirmar.Igual(0, cancelamentos, $"{caso}: sem cancelar");
        }
        else
        {
            Afirmar.Igual((0, 1), (agendas.Length, cancelamentos), $"{caso}: sem onda, o temporizador é cancelado");
            Afirmar.Falso(depois.OndaAgendada, $"{caso}: sem disparo pendente");
        }

        Afirmar.Sequencia([new Transicao(Estado.Idle, Estado.Using, RegraDoAlivio(item, frente, fundo))], u.Soltar.Transicoes, $"{caso}: a transição diz o que o alívio fez");
        Afirmar.Igual(Esperado(item).CaraDurante, depois.Expressao, $"{caso}: durante o uso, a cara de quem usa");
        Afirmar.Igual((novaFrente, novoFundo), (u.DepoisDoUso.Onda, u.DepoisDoUso.OndaDeFundo), $"{caso}: o fim do uso não mexe na onda");
        Afirmar.Igual(novaFrente is { } f ? CaraEsperada(f) : Esperado(item).CaraDurante, u.DepoisDoUso.Expressao, $"{caso}: no fim do uso, a cara da fase");
    }

    // ---------------------------------------------------------------- comida e bebida contra as ondas de substância

    // Exemplos à mão, com os números da tabela: a banana no pico do bêbado no nível 2 só baixa o nível (o disparo em curso
    // continua); a água no pico do nível 1, com o pior 3, leva à queda de 135 s (90 × 150%); o café na subida do chapado no
    // nível 1 leva à queda de 90 s; o energético no pico do relaxado, que não tem queda, acaba a onda; a banana na queda do
    // tonto acaba a onda, e o bêbado de fundo volta no pico do nível 2, com 100 s; e o café com o próprio ligado no fundo
    // alivia o bêbado da frente sem somar no fundo.
    [Teste]
    public static void Exemplos_UmPassoPorItem()
    {
        var bebado2 = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2);
        var ligadoNoFundo = new EstadoDaOnda(Onda.Ligado, FaseDaOnda.Pico, 1, 1);
        (Item Item, EstadoDaOnda Frente, EstadoDaOnda? Fundo, EstadoDaOnda? Frente2, EstadoDaOnda? Fundo2, TimeSpan? Agendada, string Regra)[] exemplos =
        [
            (Item.Banana, bebado2, null, new(Onda.Bebado, FaseDaOnda.Pico, 1, 2), null, null,
                "ITEM_DRAG_END sobre o personagem: Comer Banana; alivia Bebado/Pico/2 -> Bebado/Pico/1"),
            (Item.Agua, new(Onda.Bebado, FaseDaOnda.Pico, 1, 3), null, new(Onda.Bebado, FaseDaOnda.Queda, 1, 3), null, TimeSpan.FromSeconds(135),
                "ITEM_DRAG_END sobre o personagem: Beber Agua; alivia Bebado/Pico/1 -> Bebado/Queda/1"),
            (Item.Cafe, new(Onda.Chapado, FaseDaOnda.Subida, 1, 1), null, new(Onda.Chapado, FaseDaOnda.Queda, 1, 1), null, TimeSpan.FromSeconds(90),
                "ITEM_DRAG_END sobre o personagem: Beber Cafe; alivia Chapado/Subida/1 -> Chapado/Queda/1"),
            (Item.Energetico, new(Onda.Relaxado, FaseDaOnda.Pico, 1, 1), null, null, null, null,
                "ITEM_DRAG_END sobre o personagem: Beber Energetico; alivia Relaxado/Pico/1 -> fim"),
            (Item.Banana, new(Onda.Tonto, FaseDaOnda.Queda, 1, 2), bebado2, bebado2, null, TimeSpan.FromSeconds(100),
                "ITEM_DRAG_END sobre o personagem: Comer Banana; alivia Tonto/Queda/1 -> fim; a de fundo volta: Bebado/Pico/2"),
            (Item.Cafe, new(Onda.Bebado, FaseDaOnda.Subida, 3, 3), ligadoNoFundo, new(Onda.Bebado, FaseDaOnda.Subida, 2, 3), ligadoNoFundo, null,
                "ITEM_DRAG_END sobre o personagem: Beber Cafe; alivia Bebado/Subida/3 -> Bebado/Subida/2"),
        ];
        foreach ((Item item, EstadoDaOnda frente, EstadoDaOnda? fundo, EstadoDaOnda? frente2, EstadoDaOnda? fundo2, TimeSpan? agendada, string regra) in exemplos)
        {
            (SimuladorDeTempo bancada, int id) = Bancada(item);
            Usado u = Usar(bancada, id, frente, fundo);
            string caso = $"{item} com {Descrever(frente)} (fundo {Descrever(fundo)})";
            Afirmar.Igual((frente2, fundo2), (u.Soltar.Estado.Onda, u.Soltar.Estado.OndaDeFundo), $"{caso}: a onda depois");
            TimeSpan[] atrasos = agendada is { } a ? [a] : [];
            Afirmar.Sequencia(atrasos, u.Soltar.Efeitos.OfType<AgendarOnda>().Select(x => x.Atraso), $"{caso}: o temporizador");
            Afirmar.Igual(frente2 is null, u.Soltar.Efeitos.OfType<CancelarOnda>().Any(), $"{caso}: sem onda, cancelado");
            Afirmar.Igual(regra, u.Soltar.Transicoes.Single().Regra, $"{caso}: a regra");
        }
    }

    // Cada item de alívio com onda própria (banana, café e energético) contra cada onda de substância, em cada fase e nível
    // (a subida e o pico nos níveis 1 a 3, com cada pior, e a queda), sem onda de fundo, com outra onda de substância no
    // fundo e com a própria onda leve do item no fundo: um passo, pela regra da decisão (ConferirAlivio). O eufórico, que
    // agora vem também da bala, está entre as ondas de substância.
    [Teste]
    public static void ComidaEBebida_ContraCadaOndaDeSubstancia_UmPasso()
    {
        int casos = 0;
        foreach (Item item in new[] { Item.Banana, Item.Cafe, Item.Energetico })
        {
            (SimuladorDeTempo bancada, int id) = Bancada(item);
            Onda propria = Afirmar.NaoNulo(Esperado(item).Onda, $"{item} tem onda própria");
            foreach (Onda tipo in TabelasDoDesenho.OndasEsperadas().Where(o => o.DeSubstancia).Select(o => o.Onda))
            {
                EstadoDaOnda outraDeSubstancia = tipo == Onda.Bebado ? new(Onda.Chapado, FaseDaOnda.Pico, 2, 2) : new(Onda.Bebado, FaseDaOnda.Queda, 1, 3);
                foreach (EstadoDaOnda frente in FasesENiveis(tipo))
                {
                    foreach (EstadoDaOnda? fundo in new[] { null, outraDeSubstancia, new EstadoDaOnda(propria, FaseDaOnda.Subida, 1, 1) })
                    {
                        ConferirAlivio(item, Usar(bancada, id, frente, fundo), $"{item} com {Descrever(frente)} (pior {frente.Pior}) e fundo {Descrever(fundo)}");
                        casos++;
                    }
                }
            }
        }
        Console.WriteLine($"         {casos} usos de comida e bebida com uma onda de substância na frente");
    }

    // ---------------------------------------------------------------- a água

    // A água alivia um passo qualquer onda da frente, de substância ou leve, em cada fase e nível, sem onda de fundo, com uma
    // de substância e com uma leve no fundo (ConferirAlivio). É o refresco de antes com uma diferença só: na subida do nível
    // 1, a onda vai para a queda, em vez de acabar (sem queda, acaba, como antes). Sem onda, a água não faz nada: nem onda,
    // nem temporizador, nem "alivia" na regra.
    [Teste]
    public static void Agua_AliviaUmPassoQualquerOnda()
    {
        (SimuladorDeTempo bancada, int id) = Bancada(Item.Agua);
        int casos = 0;
        foreach (Onda tipo in Enum.GetValues<Onda>())
        {
            EstadoDaOnda deSubstancia = tipo == Onda.Bebado ? new(Onda.Tonto, FaseDaOnda.Queda, 1, 2) : new(Onda.Bebado, FaseDaOnda.Pico, 2, 3);
            EstadoDaOnda leve = tipo == Onda.Alegre ? new(Onda.Ligado, FaseDaOnda.Pico, 1, 2) : new(Onda.Alegre, FaseDaOnda.Subida, 1, 1);
            foreach (EstadoDaOnda frente in FasesENiveis(tipo))
            {
                foreach (EstadoDaOnda? fundo in new[] { null, deSubstancia, leve })
                {
                    ConferirAlivio(Item.Agua, Usar(bancada, id, frente, fundo), $"água com {Descrever(frente)} (pior {frente.Pior}) e fundo {Descrever(fundo)}");
                    casos++;
                }
            }
        }
        Console.WriteLine($"         {casos} usos da água com onda na frente");

        foreach (Onda tipo in new[] { Onda.Alegre, Onda.Ligado, Onda.Bebado, Onda.Tonto })
        {
            Usado u = Usar(bancada, id, new EstadoDaOnda(tipo, FaseDaOnda.Subida, 1, 1), null);
            Afirmar.Igual(new EstadoDaOnda(tipo, FaseDaOnda.Queda, 1, 1), u.Soltar.Estado.Onda, $"{tipo} na subida do nível 1: a água a leva à queda (antes, acabava)");
        }
        foreach (Onda tipo in new[] { Onda.Satisfeito, Onda.Relaxado })
            Afirmar.Nulo(Usar(bancada, id, new EstadoDaOnda(tipo, FaseDaOnda.Subida, 1, 1), null).Soltar.Estado.Onda, $"{tipo}, sem queda: na subida do nível 1, acaba, como antes");

        Usado semOnda = Usar(bancada, id, null, null);
        Afirmar.Igual(((EstadoDaOnda?)null, (EstadoDaOnda?)null), (semOnda.Soltar.Estado.Onda, semOnda.Soltar.Estado.OndaDeFundo), "sem onda, a água não faz nada");
        Afirmar.Falso(semOnda.Soltar.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "nem no temporizador");
        Afirmar.Igual(semOnda.AntesDoSoltar.Aleatorio, semOnda.Soltar.Estado.Aleatorio, "nem sorteia");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Beber Agua", semOnda.Soltar.Transicoes.Single().Regra, "e a regra não fala de alívio");
    }

    // ---------------------------------------------------------------- com onda leve na frente, ou sem onda: como antes

    /// <summary>
    /// A combinação de antes do alívio (4.5), escrita aqui à parte do núcleo, para um item com onda própria: sem onda, a do
    /// item começa na subida, no nível da intensidade; do mesmo tipo da da frente, os níveis somam até 3, o pior acompanha e
    /// a fase recomeça (a subida continua subida; o pico e a queda viram pico); do mesmo tipo da de fundo, a de fundo soma do
    /// mesmo jeito e continua congelada; de precedência maior ou igual, a do item vai para a frente, na subida, e a da frente
    /// vai para o fundo; de precedência menor, é absorvida. Devolve também se o temporizador recomeça (a frente mudou).
    /// </summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo, bool Recomeca) ComoAntes(EstadoDaOnda? frente, EstadoDaOnda? fundo, Onda tipo, int intensidade)
    {
        EstadoDaOnda Somada(EstadoDaOnda o)
        {
            int nivel = Math.Min(3, o.Nivel + intensidade);
            return new EstadoDaOnda(o.Tipo, o.Fase == FaseDaOnda.Subida ? FaseDaOnda.Subida : FaseDaOnda.Pico, nivel, Math.Max(o.Pior, nivel));
        }
        var nova = new EstadoDaOnda(tipo, FaseDaOnda.Subida, intensidade, intensidade);
        if (frente is null) return (nova, fundo, true);
        if (frente.Tipo == tipo) return (Somada(frente), fundo, true);
        if (fundo?.Tipo == tipo) return (frente, Somada(fundo), false);
        if (TabelasDoDesenho.Esperada(tipo).Precedencia >= TabelasDoDesenho.Esperada(frente.Tipo).Precedencia) return (nova, frente, true);
        return (frente, fundo, false);
    }

    // A banana, o café e o energético, sem onda ou com uma onda leve na frente (o satisfeito, o alegre ou o ligado, em cada
    // fase e nível), fazem exatamente o de antes do alívio: a combinação 4.5 (ComoAntes), com o temporizador recomeçado na
    // duração cheia só quando a frente muda, a regra do soltar sem "alivia", nenhum sorteio no soltar e, no fim do uso, a
    // cara da fase.
    // Com a onda leve na frente: sem onda de fundo, com uma de substância no fundo e com a própria onda do item no fundo
    // (quando a da frente é outra). A água sem onda, que também é como antes, fica em Agua_AliviaUmPassoQualquerOnda.
    [Teste]
    public static void ComidaEBebida_ComOndaLeveNaFrenteOuSemOnda_ComoAntes()
    {
        var desfechos = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Item item in new[] { Item.Banana, Item.Cafe, Item.Energetico })
        {
            (SimuladorDeTempo bancada, int id) = Bancada(item);
            TabelasDoDesenho.ItemEsperado e = Esperado(item);
            Onda propria = Afirmar.NaoNulo(e.Onda, $"{item} tem onda própria");
            var casos = new List<(EstadoDaOnda? Frente, EstadoDaOnda? Fundo)> { (null, null) };
            foreach (Onda leve in TabelasDoDesenho.OndasEsperadas().Where(o => !o.DeSubstancia).Select(o => o.Onda))
            {
                foreach (EstadoDaOnda frente in FasesENiveis(leve))
                {
                    casos.Add((frente, null));
                    casos.Add((frente, new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2)));
                    if (leve != propria) casos.Add((frente, new EstadoDaOnda(propria, FaseDaOnda.Pico, 1, 2)));
                }
            }
            foreach ((EstadoDaOnda? frente, EstadoDaOnda? fundo) in casos)
            {
                string caso = $"{item} com {Descrever(frente)} (pior {frente?.Pior}) e fundo {Descrever(fundo)}";
                Usado u = Usar(bancada, id, frente, fundo);
                (EstadoDaOnda? f, EstadoDaOnda? b, bool recomeca) = ComoAntes(frente, fundo, propria, e.Intensidade);
                EstadoDaOnda novaFrente = Afirmar.NaoNulo(f, $"{caso}: com onda própria, sempre fica uma onda");
                Afirmar.Igual((f, b), (u.Soltar.Estado.Onda, u.Soltar.Estado.OndaDeFundo), $"{caso}: a combinação de antes");
                AgendarOnda[] agendas = [.. u.Soltar.Efeitos.OfType<AgendarOnda>()];
                Afirmar.Falso(u.Soltar.Efeitos.OfType<CancelarOnda>().Any(), $"{caso}: nada é cancelado");
                if (recomeca)
                    Afirmar.Sequencia([new AgendarOnda(DuracaoCheia(novaFrente), u.Antes.GeracaoDaOnda + 1)], agendas, $"{caso}: a frente nova recomeça o temporizador");
                else
                    Afirmar.Verdadeiro(agendas.Length == 0 && u.Soltar.Estado.GeracaoDaOnda == u.Antes.GeracaoDaOnda, $"{caso}: a frente não mudou, nem o temporizador");
                Afirmar.Igual($"ITEM_DRAG_END sobre o personagem: {e.Verbo} {item}", u.Soltar.Transicoes.Single().Regra, $"{caso}: a regra de sempre, sem alívio");
                Afirmar.Igual(u.AntesDoSoltar.Aleatorio, u.Soltar.Estado.Aleatorio, $"{caso}: como antes, o soltar não sorteia nada");
                Afirmar.Igual(CaraEsperada(novaFrente), u.DepoisDoUso.Expressao, $"{caso}: no fim do uso, a cara da fase");
                string desfecho = frente is null ? "começou" : !recomeca ? (b != fundo ? "somou no fundo" : "absorvida") : novaFrente.Tipo == frente.Tipo ? "somou na frente" : "foi para a frente";
                desfechos[desfecho] = desfechos.GetValueOrDefault(desfecho) + 1;
            }
        }
        Console.WriteLine($"         {string.Join(", ", desfechos.Select(d => $"{d.Key}={d.Value}"))}");
        Afirmar.Sequencia(["absorvida", "começou", "foi para a frente", "somou na frente", "somou no fundo"], desfechos.Keys, "todos os desfechos da combinação aconteceram");
    }

    // ---------------------------------------------------------------- aos poucos, com itens de verdade

    /// <summary>Invoca o item, solta sobre ele e deixa o uso acabar; devolve o resultado do soltar.</summary>
    private static Resultado Usar(Cenario c, Item item)
    {
        ItemNoMundo it = InvocarEAssentar(c, item);
        c.SoltarSobreEle(it.Id).Esta(Estado.Using, $"usando {item}");
        Resultado soltar = Afirmar.NaoNulo(c.Ultimo, "o resultado do soltar");
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        return soltar;
    }

    /// <summary>O disparo da onda na geração agendada; devolve o resultado.</summary>
    private static Resultado Disparar(Cenario c) => Afirmar.NaoNulo(c.Aplicar(new ItemEffectTimer(c.Atual.GeracaoDaOnda)).Ultimo, "o resultado do disparo");

    // "Aos poucos", com itens de verdade e sem o temporizador disparar no meio: a vodka e a cerveja levam o bêbado à subida
    // do nível 3; a banana e a água baixam o nível, com o mesmo disparo pendente; o café o leva à queda, com 135 s (90 ×
    // 150%, pelo pior nível 3); e o energético o acaba, cancelando o temporizador. Um item, um passo: nível 3, 2, 1, a queda
    // e o fim. Nenhum deles começa a onda dele, e, sem onda, a cara volta à de base (aqui, a emoção dominante).
    [Teste]
    public static void VariosItensSeguidos_LevamAOndaAteOFimAosPoucos()
    {
        Cenario c = Cenario.Parado(SemFisica()).Aplicar(new CmdPauseAutonomy(), new CmdSetDominantEmotion(Expressao.Pensativo));
        Usar(c, Item.Vodka);
        Usar(c, Item.Cerveja);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 3, 3), c.Atual.Onda, "a vodka e a cerveja: o bêbado na subida do nível 3");
        long geracao = c.Atual.GeracaoDaOnda;

        Resultado banana = Usar(c, Item.Banana);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 3), banana.Estado.Onda, "a banana: nível 2");
        Resultado agua = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 1, 3), agua.Estado.Onda, "a água: nível 1");
        foreach (Resultado r in new[] { banana, agua })
            Afirmar.Falso(r.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "só o nível caiu: o disparo da subida continua pendente");
        Afirmar.Verdadeiro(c.Atual.OndaAgendada && c.Atual.GeracaoDaOnda == geracao, "o mesmo disparo, na mesma geração");

        Resultado cafe = Usar(c, Item.Cafe);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Queda, 1, 3), cafe.Estado.Onda, "o café: a queda");
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(135), geracao + 1)], cafe.Efeitos.OfType<AgendarOnda>(), "a queda inteira, pelo pior nível 3");
        Afirmar.Igual(Expressao.Enjoado, c.Atual.Expressao, "no fim do uso, a cara da queda do bêbado");

        Resultado energetico = Usar(c, Item.Energetico);
        Afirmar.Nulo(energetico.Estado.Onda, "o energético: o fim");
        Afirmar.Verdadeiro(energetico.Efeitos.OfType<CancelarOnda>().Count() == 1 && !c.Atual.OndaAgendada, "e o temporizador é cancelado");
        Afirmar.Igual(Expressao.Pensativo, c.Atual.Expressao, "sem onda, a cara volta à de base, a dominante");
        foreach (Resultado r in new[] { banana, agua, cafe, energetico })
            Afirmar.Nulo(r.Estado.OndaDeFundo, "nenhuma onda própria foi para o fundo");
        Afirmar.Sequencia(
            ["Comer Banana; alivia Bebado/Subida/3 -> Bebado/Subida/2", "Beber Agua; alivia Bebado/Subida/2 -> Bebado/Subida/1",
             "Beber Cafe; alivia Bebado/Subida/1 -> Bebado/Queda/1", "Beber Energetico; alivia Bebado/Queda/1 -> fim"],
            new[] { banana, agua, cafe, energetico }.Select(r => r.Transicoes.Single().Regra["ITEM_DRAG_END sobre o personagem: ".Length..]), "as regras");
    }

    // Com o temporizador no meio: o baseado (o chapado na subida do nível 2) e o disparo (o pico do nível 2, 110 s). A banana
    // baixa ao nível 1 sem reagendar, e o disparo que estava pendente leva o pico do nível 1 à queda, de 112,5 s (90 × 125%,
    // pelo pior nível 2): o nível 2 acabou mais cedo, e nada se alongou. A água acaba a queda.
    [Teste]
    public static void ComOTemporizadorNoMeio_ODisparoPendenteContinua()
    {
        Cenario c = Cenario.Parado(SemFisica()).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Baseado);
        Resultado pico = Disparar(c);
        Afirmar.Igual(new EstadoDaOnda(Onda.Chapado, FaseDaOnda.Pico, 2, 2), pico.Estado.Onda, "o chapado no pico do nível 2");
        long geracao = c.Atual.GeracaoDaOnda;
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(110), geracao)], pico.Efeitos.OfType<AgendarOnda>(), "110 s no nível 2");

        Resultado banana = Usar(c, Item.Banana);
        Afirmar.Igual(new EstadoDaOnda(Onda.Chapado, FaseDaOnda.Pico, 1, 2), banana.Estado.Onda, "a banana: nível 1");
        Afirmar.Falso(banana.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem reagendar");
        Resultado queda = Disparar(c);
        Afirmar.Igual(geracao, queda.Estado.GeracaoDaOnda - 1, "o disparo pendente era o do nível 2");
        Afirmar.Igual("ITEM_EFFECT_TIMER: onda Chapado/Pico/1 -> Chapado/Queda/1", queda.Transicoes.Single().Regra, "e leva à queda");
        Afirmar.Sequencia([TimeSpan.FromSeconds(112.5)], queda.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "de 112,5 s");

        Resultado agua = Usar(c, Item.Agua);
        Afirmar.Nulo(agua.Estado.Onda, "a água acaba a queda");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Beber Agua; alivia Chapado/Queda/1 -> fim", agua.Transicoes.Single().Regra, "a regra");
    }

    // Com onda de fundo: a vodka e o disparo (o bêbado no pico do nível 2); o lança-perfume (o tonto na subida do nível 2)
    // vai para a frente, e o bêbado fica no fundo, congelado (a mistura com a droga sintética sorteia a paranoia, que aqui
    // fica de fora: SemParanoia). A banana e a água aliviam só o tonto, e o bêbado de fundo fica intacto; o café acaba o
    // tonto, e o bêbado volta à frente no pico do nível 2, com a duração cheia (100 s) e a cara do pico, sem o ligado do café;
    // o energético o baixa ao nível 1, sem reagendar.
    [Teste]
    public static void ComOndaDeFundo_ADeFundoFicaIntactaEVoltaQuandoAFrenteAcaba()
    {
        Cenario c = Cenario.Parado(SemParanoia()).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Vodka);
        Disparar(c);
        var bebado = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2);
        Usar(c, Item.LancaPerfume);
        Afirmar.Igual((new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 2, 2), bebado), (c.Atual.Onda, c.Atual.OndaDeFundo), "o tonto na frente, o bêbado no fundo");

        Resultado banana = Usar(c, Item.Banana);
        Afirmar.Igual((new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 1, 2), bebado), (banana.Estado.Onda, banana.Estado.OndaDeFundo), "a banana alivia o tonto; o bêbado fica intacto");
        Resultado agua = Usar(c, Item.Agua);
        Afirmar.Igual((new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Queda, 1, 2), bebado), (agua.Estado.Onda, agua.Estado.OndaDeFundo), "a água leva o tonto à queda; o bêbado continua intacto");
        Afirmar.Sequencia([TimeSpan.FromSeconds(12.5)], agua.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "a queda do tonto, 10 × 125%");

        Resultado cafe = Usar(c, Item.Cafe);
        Afirmar.Igual((bebado, (EstadoDaOnda?)null), (cafe.Estado.Onda, cafe.Estado.OndaDeFundo), "o café acaba o tonto, e o bêbado volta, sem o ligado do café");
        Afirmar.Sequencia([TimeSpan.FromSeconds(100)], cafe.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "com a duração cheia do nível do pico");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Beber Cafe; alivia Tonto/Queda/1 -> fim; a de fundo volta: Bebado/Pico/2", cafe.Transicoes.Single().Regra, "a regra");
        Afirmar.Igual(Expressao.Bebado, c.Atual.Expressao, "no fim do uso, a cara do pico do bêbado");

        Resultado energetico = Usar(c, Item.Energetico);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 1, 2), energetico.Estado.Onda, "o energético baixa o bêbado ao nível 1");
        Afirmar.Falso(energetico.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem reagendar");
    }

    // A bala é droga sintética (pedido do usuário de 2026-10-01): de substância, ela nunca alivia nada; começa o eufórico no
    // nível 1 (subida de 15 s), com a regra do soltar de sempre, sem "alivia"; e é acalmada pela comida e pela bebida sem
    // álcool como as outras substâncias: a banana leva a subida do nível 1 à queda (120 s) e o café a acaba. Por cima de uma
    // onda de substância (o bêbado da vodka), ela não alivia: combina como substância, e o eufórico, de precedência igual,
    // vai para a frente, com o bêbado no fundo. Por cima de uma onda leve (o ligado do café), o eufórico, mais forte, também
    // vai para a frente (antes, o alegre da bala era absorvido). O soltar não sorteia no gerador principal.
    [Teste]
    public static void Bala_EhSubstancia_AComidaEABebidaAAcalmam()
    {
        var euforico = new EstadoDaOnda(Onda.Euforico, FaseDaOnda.Subida, 1, 1);
        Cenario sozinha = Cenario.Parado(SemParanoia()).Aplicar(new CmdPauseAutonomy());
        Resultado bala = Usar(sozinha, Item.Bala);
        Afirmar.Igual((euforico, (EstadoDaOnda?)null), (bala.Estado.Onda, bala.Estado.OndaDeFundo), "a bala sozinha: o eufórico no nível 1");
        Afirmar.Sequencia([TimeSpan.FromSeconds(15)], bala.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "a subida do eufórico, 15 s");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Engolir Bala", bala.Transicoes.Single().Regra, "a regra de sempre, sem alívio");
        Resultado banana = Usar(sozinha, Item.Banana);
        Afirmar.Igual(new EstadoDaOnda(Onda.Euforico, FaseDaOnda.Queda, 1, 1), banana.Estado.Onda, "a banana acalma o eufórico da bala: a queda");
        Afirmar.Sequencia([TimeSpan.FromSeconds(120)], banana.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "a queda inteira, 120 s");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Comer Banana; alivia Euforico/Subida/1 -> Euforico/Queda/1", banana.Transicoes.Single().Regra, "a regra do alívio");
        Resultado cafe = Usar(sozinha, Item.Cafe);
        Afirmar.Nulo(cafe.Estado.Onda, "o café acaba o eufórico da bala");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Beber Cafe; alivia Euforico/Queda/1 -> fim", cafe.Transicoes.Single().Regra, "a regra do fim");

        Cenario comBebado = Cenario.Parado(SemParanoia()).Aplicar(new CmdPauseAutonomy());
        Usar(comBebado, Item.Vodka);
        ItemNoMundo outraBala = InvocarEAssentar(comBebado, Item.Bala);
        Aleatorio gerador = comBebado.Atual.Aleatorio;
        comBebado.SoltarSobreEle(outraBala.Id).Esta(Estado.Using, "usando a bala");
        Resultado porCima = Afirmar.NaoNulo(comBebado.Ultimo, "o soltar");
        Afirmar.Igual((euforico, new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2)), (porCima.Estado.Onda, porCima.Estado.OndaDeFundo),
            "por cima do bêbado, a bala não alivia: o eufórico vai para a frente e o bêbado fica no fundo");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Engolir Bala", porCima.Transicoes.Single().Regra, "sem alívio");
        Afirmar.Igual(gerador, porCima.Estado.Aleatorio, "o soltar não sorteia no gerador principal");

        Cenario comLigado = Cenario.Parado(SemParanoia()).Aplicar(new CmdPauseAutonomy());
        Usar(comLigado, Item.Cafe);
        Resultado sobreOLigado = Usar(comLigado, Item.Bala);
        Afirmar.Igual((euforico, new EstadoDaOnda(Onda.Ligado, FaseDaOnda.Subida, 1, 1)), (sobreOLigado.Estado.Onda, sobreOLigado.Estado.OndaDeFundo),
            "por cima do ligado (leve), o eufórico, mais forte, vai para a frente");
    }
}
