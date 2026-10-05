using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// A paranoia (pedidos do usuário de 2026-10-01: "caso o macaco use muitas coisas ele fica paranoico, como o meme 'os cara
/// tá no teto' mas de forma engraçada"; "só quero que ele fique paranoico se misturar substâncias com alguma droga
/// sintética, como bala, md, coca e lança; usando álcool e maconha não"; "quero que a chance dele ficar paranoico seja de 1
/// em 8"; e a escolha dele às 23:03, "uma vez por mistura"; decisão do coordenador para a DEC-028). De desenho animado: ele
/// acha que tem alguém no teto. As drogas sintéticas são regra de jogo, dado da tabela: a bala, o MD, a cocaína e o
/// lança-perfume. Cada item de substância entra na carga do episódio, depois da combinação de sempre (quantos, se houve
/// sintética e quais itens distintos); a comida e a bebida sem álcool não entram. O uso que fecha a mistura com sintética
/// (pelo menos uma sintética e pelo menos dois itens de substância distintos, contando o atual), sem a paranoia na frente,
/// faz o único sorteio do episódio, com a chance da configuração (1 em 8), num gerador próprio da paranoia, guardado no
/// estado e semeado da semente do núcleo; o principal nunca. Saindo, ela vai para a frente, na subida do nível 1, e a frente
/// anterior vai para o fundo. Saindo ou não, o episódio não sorteia mais: a chance de ele ficar paranoico num episódio é a
/// da configuração, por mais substâncias que ele use. Sem mistura com sintética, nunca há sorteio. Com ela na frente, mais
/// uma substância sobe um nível e recomeça a fase, sem sorteio. A carga volta a zero, tudo junto (com o sorteio feito), no
/// fim de todo evento sem onda de substância na frente nem no fundo, e o episódio seguinte sorteia de novo. A onda tem
/// subida de 1 s (assustado), pico de 40 s por nível (paranoico) e queda de 15 s (sonolento); no pico, ele nunca escala,
/// pula nem descansa. O uso que começa a paranoia, se termina com ele em IDLE sem gesto, acaba com ele olhando pro teto, por
/// 90 passos e sem sorteio. Os testes de regra usam a chance de 1 em 1, para o sorteio virar paranoia, ou a de 0 em 1, para
/// ele nunca sair; os da chance real, a de 1 em 8 do aplicativo. O esperado vem da decisão, escrito aqui à parte do núcleo,
/// com os números da tabela.
/// </summary>
internal static class ParanoiaTestes
{
    // ---------------------------------------------------------------- apoio

    /// <summary>A chance que sempre sai (1 em 1): nos testes de regra, cada sorteio vira paranoia.</summary>
    private static readonly Chance Sempre = new(1, 1);

    /// <summary>As drogas sintéticas da decisão, escritas aqui à parte do núcleo.</summary>
    private static readonly Item[] Sinteticas = [Item.Bala, Item.Md, Item.Cocaina, Item.LancaPerfume];

    /// <summary>As outras substâncias da decisão: álcool, maconha, cigarro e cogumelo.</summary>
    private static readonly Item[] OutrasSubstancias = [Item.Vodka, Item.Cerveja, Item.Baseado, Item.Cigarro, Item.Cogumelo];

    private static readonly EstadoDaOnda ParanoiaComecando = new(Onda.Paranoico, FaseDaOnda.Subida, 1, 1);

    private static readonly EstadoDaOnda BebadoNaSubida2 = new(Onda.Bebado, FaseDaOnda.Subida, 2, 2);

    /// <summary>Um uso: o estado logo antes do soltar (o ITEM_DRAG_END) e o resultado do soltar.</summary>
    private sealed record Usado(EstadoDoNucleo AntesDoSoltar, Resultado Soltar)
    {
        public EstadoDoNucleo Depois => Soltar.Estado;

        public string Regra => Soltar.Transicoes.Single().Regra;
    }

    /// <summary>Parado no chão, sem a física e com a autonomia pausada, com a chance dada: só o que os testes mandam acontece.</summary>
    private static Cenario Pausado(Chance chance) => Cenario.Parado(SemFisica() with { ChanceDaParanoia = chance }).Aplicar(new CmdPauseAutonomy());

    /// <summary>
    /// Invoca o item, deixa cair, solta sobre ele e deixa o uso acabar; devolve o estado logo antes do soltar e o resultado
    /// do soltar. Confere, em todo uso, que o soltar não sorteia no gerador principal: a paranoia nunca o consome.
    /// </summary>
    private static Usado Usar(Cenario c, Item item)
    {
        ItemNoMundo it = InvocarEAssentar(c, item);
        Evento[] gesto = Arraste(it, Cenario.MeioDoPersonagem(c.Atual, c.Config));
        c.Aplicar(gesto[..^1]);
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(gesto[^1]).Esta(Estado.Using, $"usando {item}");
        Resultado soltar = Afirmar.NaoNulo(c.Ultimo, "o resultado do soltar");
        Afirmar.Igual(antes.Aleatorio, soltar.Estado.Aleatorio, $"{item}: o soltar não sorteia no gerador principal");
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        return new Usado(antes, soltar);
    }

    /// <summary>O disparo da onda na geração agendada; devolve o resultado.</summary>
    private static Resultado Disparar(Cenario c) => Afirmar.NaoNulo(c.Aplicar(new ItemEffectTimer(c.Atual.GeracaoDaOnda)).Ultimo, "o resultado do disparo");

    /// <summary>Os atrasos agendados para a onda no resultado.</summary>
    private static TimeSpan[] Agendados(Resultado r) => [.. r.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso)];

    /// <summary>O gerador depois de exatamente um sorteio: um passo.</summary>
    private static Aleatorio UmPasso(Aleatorio gerador) => gerador.Sortear().Proximo;

    /// <summary>A carga esperada do episódio, ainda sem o sorteio: quantas substâncias, se houve sintética e os itens distintos.</summary>
    private static CargaDaParanoia Carga(int substancias, bool sintetica, params Item[] distintas)
        => new(substancias, sintetica, distintas.Aggregate(ConjuntoDeItens.Vazio, (conjunto, item) => conjunto.Com(item)), Sorteada: false);

    /// <summary>
    /// A carga esperada de um episódio que já fez o sorteio da paranoia, o único dele: uma mistura com sintética, com as
    /// substâncias e os itens distintos dados.
    /// </summary>
    private static CargaDaParanoia CargaSorteada(int substancias, params Item[] distintas) => Carga(substancias, true, distintas) with { Sorteada = true };

    /// <summary>
    /// Dispara o temporizador da onda até não sobrar onda nenhuma, na frente ou no fundo (só de substância: o episódio não
    /// tem comida nem bebida sem álcool). Enquanto sobra uma, a carga do episódio fica como estava, com o sorteio feito; no
    /// disparo que acaba a última, o episódio acaba, e a carga volta a zero, tudo junto, com o sorteio.
    /// </summary>
    private static void AcabarOEpisodio(Cenario c, string caso)
    {
        CargaDaParanoia doEpisodio = c.Atual.Carga;
        for (int disparos = 0; c.Atual.Onda is not null; disparos++)
        {
            Afirmar.Verdadeiro(disparos < 40, $"{caso}: as ondas acabam");
            Disparar(c);
            if (c.Atual.Onda is not null) Afirmar.Igual(doEpisodio, c.Atual.Carga, $"{caso}: com onda de substância, a carga do episódio fica, com o sorteio feito");
        }
        Afirmar.Igual(((EstadoDaOnda?)null, CargaDaParanoia.Nenhuma), (c.Atual.OndaDeFundo, c.Atual.Carga),
            $"{caso}: sem onda, o episódio acabou e a carga voltou a zero no mesmo disparo, tudo junto, com o sorteio");
    }

    private static TabelasDoDesenho.ItemEsperado Esperado(Item item) => TabelasDoDesenho.ItensEsperados().Single(i => i.Item == item);

    /// <summary>Um uso sem sorteio da paranoia: ela não começa, o gerador dela fica o mesmo e a regra do soltar não fala dela.</summary>
    private static void ConferirSemSorteio(Usado u, string caso)
    {
        Afirmar.Igual(u.AntesDoSoltar.AleatorioDaParanoia, u.Depois.AleatorioDaParanoia, $"{caso}: sem sorteio, o gerador da paranoia fica o mesmo");
        Afirmar.Falso(u.Depois.Onda?.Tipo == Onda.Paranoico, $"{caso}: sem paranoia ({u.Depois.Onda})");
        Afirmar.Falso(u.Regra.Contains("paranoia", StringComparison.Ordinal), $"{caso}: a regra de sempre ({u.Regra})");
        Afirmar.Falso(u.Depois.Uso!.ComecouAParanoia, $"{caso}: o uso não começou a paranoia");
    }

    /// <summary>
    /// O caminho mais curto até a paranoia, com a chance de 1 em 1: a bala (o eufórico na subida do nível 1, sem sorteio:
    /// uma sintética sozinha) e a vodka, que fecha a mistura: o bêbado vai para a frente da combinação (precedência igual) e
    /// o sorteio sai; a paranoia toma a frente e o bêbado fica no fundo. Devolve o uso da vodka.
    /// </summary>
    private static Usado AteAParanoia(Cenario c)
    {
        Afirmar.Igual(Sempre, c.Config.ChanceDaParanoia, "o caminho curto até a paranoia pede a chance de 1 em 1");
        Usar(c, Item.Bala);
        Usado vodka = Usar(c, Item.Vodka);
        Afirmar.Igual((ParanoiaComecando, BebadoNaSubida2), (vodka.Depois.Onda, vodka.Depois.OndaDeFundo), "a bala e a vodka: a paranoia na frente, o bêbado no fundo");
        return vodka;
    }

    // ---------------------------------------------------------------- sem mistura com sintética, nunca

    // "Usando álcool e maconha não" (pedido do usuário de 2026-10-01, 18:50): mesmo com a chance em 1 em 1, a vodka, a
    // cerveja e o baseado, e também o cigarro e o cogumelo (as outras substâncias da decisão), sozinhos e repetidos, ou
    // misturados entre si, nunca sorteiam nem trazem a paranoia: o gerador da paranoia fica o mesmo a cada uso, a regra do
    // soltar é a de sempre, e a carga só conta (as substâncias, nenhuma sintética, os itens distintos).
    [Teste]
    public static void SemSintetica_NuncaSorteiaNemDaParanoia()
    {
        foreach (Item item in OutrasSubstancias)
        {
            Cenario sozinho = Pausado(Sempre);
            for (int vez = 1; vez <= 3; vez++)
            {
                Usado u = Usar(sozinho, item);
                ConferirSemSorteio(u, $"{item} sozinho, {vez}ª vez");
                Afirmar.Igual(Carga(vez, false, item), u.Depois.Carga, $"{item} sozinho, {vez}ª vez: a carga");
            }
        }

        Cenario misturado = Pausado(Sempre);
        Item[] mistura = [.. OutrasSubstancias, Item.Vodka, Item.Baseado, Item.Cerveja];
        for (int i = 0; i < mistura.Length; i++)
        {
            Usado u = Usar(misturado, mistura[i]);
            ConferirSemSorteio(u, $"a mistura sem sintética, {i + 1}º uso ({mistura[i]})");
            Afirmar.Igual(Carga(i + 1, false, [.. mistura.Take(i + 1).Distinct()]), u.Depois.Carga, $"{i + 1}º uso ({mistura[i]}): a carga conta, sem sintética");
        }
        Afirmar.Igual(8, misturado.Retrato.Carga, "o retrato leva a carga");
    }

    // Uma droga sintética sozinha, repetida, não é mistura: cada uma das quatro, quatro vezes seguidas, com a chance em 1 em
    // 1, nunca sorteia. A comida e a bebida sem álcool no meio não contam como outra substância: a bala, a banana, a bala, a
    // água e a bala continuam sendo uma sintética sozinha.
    [Teste]
    public static void SinteticaSozinhaRepetida_NaoEhMistura()
    {
        foreach (Item sintetica in Sinteticas)
        {
            Cenario c = Pausado(Sempre);
            for (int vez = 1; vez <= 4; vez++)
            {
                Usado u = Usar(c, sintetica);
                ConferirSemSorteio(u, $"{sintetica} sozinha, {vez}ª vez");
                Afirmar.Igual(Carga(vez, true, sintetica), u.Depois.Carga, $"{sintetica}, {vez}ª vez: a carga, com a sintética e um item só");
            }
        }

        Cenario comComida = Pausado(Sempre);
        int balas = 0;
        foreach (Item item in new[] { Item.Bala, Item.Banana, Item.Bala, Item.Agua, Item.Bala })
        {
            if (item == Item.Bala) balas++;
            Usado u = Usar(comComida, item);
            ConferirSemSorteio(u, $"a bala com a comida e a água no meio ({item})");
            Afirmar.Igual(Carga(balas, true, Item.Bala), u.Depois.Carga, $"{item}: a comida e a água não entram na carga");
        }
    }

    // ---------------------------------------------------------------- a mistura com sintética: o sorteio

    // A mistura com sintética: pelo menos uma droga sintética e pelo menos dois itens de substância distintos no episódio,
    // contando o item atual. O sorteio vem no uso que fecha a mistura, depois da combinação de sempre, com um passo do
    // gerador próprio da paranoia (o principal, nunca); o primeiro item não sorteia. Com 1 em 1, a paranoia começa na
    // frente, na subida do nível 1 (o disparo de 1 s), e a frente da combinação vai para o fundo (a de fundo anterior sai).
    // Vale com a sintética antes ou depois, e com duas sintéticas. O retrato leva a carga.
    [Teste]
    public static void MisturaComSintetica_SorteiaNoUsoQueFechaAMistura()
    {
        (Item Primeiro, Item Segundo, EstadoDaOnda FundoDaParanoia, string Uso)[] casos =
        [
            // A vodka (o bêbado no nível 2) e o MD: o eufórico, de precedência igual, vai para a frente da combinação, e o
            // bêbado, para o fundo; a paranoia toma a frente, o eufórico fica no fundo e o bêbado sai (só cabem duas).
            (Item.Vodka, Item.Md, new(Onda.Euforico, FaseDaOnda.Subida, 2, 2), "Engolir Md"),
            // O MD antes e a vodka depois: o bêbado é a frente da combinação e fica no fundo da paranoia.
            (Item.Md, Item.Vodka, BebadoNaSubida2, "Beber Vodka"),
            // Duas sintéticas: a cocaína (o elétrico) e o lança-perfume (o tonto, a frente da combinação).
            (Item.Cocaina, Item.LancaPerfume, new(Onda.Tonto, FaseDaOnda.Subida, 2, 2), "Inalar LancaPerfume"),
        ];
        foreach ((Item primeiro, Item segundo, EstadoDaOnda fundo, string uso) in casos)
        {
            string caso = $"{primeiro} e {segundo}";
            Cenario c = Pausado(Sempre);
            ConferirSemSorteio(Usar(c, primeiro), $"{caso}: o primeiro item");
            Usado u = Usar(c, segundo);
            Afirmar.Igual(UmPasso(u.AntesDoSoltar.AleatorioDaParanoia), u.Depois.AleatorioDaParanoia, $"{caso}: um sorteio, um passo do gerador da paranoia");
            Afirmar.Igual((ParanoiaComecando, fundo), (u.Depois.Onda, u.Depois.OndaDeFundo), $"{caso}: a paranoia na frente, a frente da combinação no fundo");
            Afirmar.Igual($"ITEM_DRAG_END sobre o personagem: {uso}; a paranoia começa: Paranoico/Subida/1", u.Regra, $"{caso}: a regra diz que a paranoia começou");
            Afirmar.Sequencia([TimeSpan.FromSeconds(1)], Agendados(u.Soltar), $"{caso}: a subida da paranoia, 1 s");
            Afirmar.Verdadeiro(u.Depois.Uso!.ComecouAParanoia, $"{caso}: este uso começou a paranoia");
            Afirmar.Igual(Esperado(segundo).CaraDurante, u.Depois.Expressao, $"{caso}: durante o uso, a cara de quem usa");
            Afirmar.Igual(CargaSorteada(2, primeiro, segundo), u.Depois.Carga, $"{caso}: a carga do episódio, com o sorteio feito");
        }

        Cenario retrato = Pausado(Sempre);
        Usar(retrato, Item.Vodka);
        Usado md = Usar(retrato, Item.Md);
        Afirmar.Contem(" onda=Paranoico/Subida/1 fundo=Euforico/Subida/2 carga=2 uso=Md/Engolir/0de90/Chao", md.Depois.Retrato().Descrever(), "a linha do retrato no soltar");
        Afirmar.Igual(2, md.Depois.Retrato().Carga, "o retrato leva a carga");
    }

    /// <summary>
    /// A frente depois de dois itens de substância seguidos, sem disparo no meio, pela transcrição (4.5): do mesmo tipo, os
    /// níveis somam (na subida, até 3); de precedência maior ou igual, a onda do segundo; menor, a do primeiro.
    /// </summary>
    private static EstadoDaOnda FrenteDaCombinacao(Item primeiro, Item segundo)
    {
        TabelasDoDesenho.ItemEsperado a = Esperado(primeiro), b = Esperado(segundo);
        Onda ondaA = Afirmar.NaoNulo(a.Onda, $"{primeiro} tem onda"), ondaB = Afirmar.NaoNulo(b.Onda, $"{segundo} tem onda");
        if (ondaA == ondaB)
        {
            int nivel = Math.Min(3, a.Intensidade + b.Intensidade);
            return new EstadoDaOnda(ondaA, FaseDaOnda.Subida, nivel, nivel);
        }
        return TabelasDoDesenho.Esperada(ondaB).Precedencia >= TabelasDoDesenho.Esperada(ondaA).Precedencia
            ? new EstadoDaOnda(ondaB, FaseDaOnda.Subida, b.Intensidade, b.Intensidade)
            : new EstadoDaOnda(ondaA, FaseDaOnda.Subida, a.Intensidade, a.Intensidade);
    }

    // Qualquer uma das quatro sintéticas basta, com qualquer uma das outras cinco substâncias, antes ou depois dela; e duas
    // sintéticas diferentes também (a bala e o MD, que começam a mesma onda: contam os itens, não as ondas). Com 1 em 1, o
    // segundo uso começa a paranoia, e o fundo dela é a frente da combinação.
    [Teste]
    public static void QualquerSinteticaBasta_EmQualquerOrdem()
    {
        var pares = new List<(Item Primeiro, Item Segundo)>();
        foreach (Item sintetica in Sinteticas)
        {
            foreach (Item outra in OutrasSubstancias)
            {
                pares.Add((outra, sintetica));
                pares.Add((sintetica, outra));
            }
        }
        pares.Add((Item.Bala, Item.Md));
        foreach ((Item primeiro, Item segundo) in pares)
        {
            string caso = $"{primeiro} e {segundo}";
            Cenario c = Pausado(Sempre);
            ConferirSemSorteio(Usar(c, primeiro), $"{caso}: o primeiro item");
            Usado u = Usar(c, segundo);
            Afirmar.Igual((ParanoiaComecando, FrenteDaCombinacao(primeiro, segundo)), (u.Depois.Onda, u.Depois.OndaDeFundo), $"{caso}: a paranoia, com a frente da combinação no fundo");
            Afirmar.Igual($"ITEM_DRAG_END sobre o personagem: {Esperado(segundo).Verbo} {segundo}; a paranoia começa: Paranoico/Subida/1", u.Regra, $"{caso}: a regra");
            Afirmar.Igual(UmPasso(u.AntesDoSoltar.AleatorioDaParanoia), u.Depois.AleatorioDaParanoia, $"{caso}: um sorteio");
        }
        Afirmar.Igual(41, pares.Count, "4 sintéticas × 5 outras × 2 ordens, e as duas sintéticas");
    }

    // ---------------------------------------------------------------- com a paranoia na frente: sobe, sem sorteio

    // Com a paranoia na frente, qualquer substância sobe um nível (até 3), o pior acompanha e a fase recomeça, sem sorteio
    // (o gerador da paranoia fica o mesmo): na subida, continua subida (1 s); no pico, recomeça o pico (40 s); no nível 3,
    // fica no 3 e o pico recomeça assim mesmo, com um disparo novo; na queda, volta ao pico, no nível 2. Vale para outra
    // substância qualquer, para a mesma sintética de novo e para uma sintética nova. A substância combina antes com o fundo,
    // como sempre (a cerveja e a vodka somam no bêbado; a bala e o lança-perfume, mais fracos que a paranoia, são
    // absorvidos), e a carga continua contando.
    [Teste]
    public static void ComAParanoiaNaFrente_QualquerSubstanciaSobeUmNivelSemSorteio()
    {
        Cenario c = Pausado(Sempre);
        AteAParanoia(c);
        var bebadoNoTeto = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 3, 3);

        Usado naSubida = Usar(c, Item.Cerveja);
        ConferirSubida(naSubida, new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Subida, 2, 2), "Beber Cerveja; a paranoia sobe: Paranoico/Subida/1 -> Paranoico/Subida/2", TimeSpan.FromSeconds(1), "na subida");
        Afirmar.Igual(bebadoNoTeto, naSubida.Depois.OndaDeFundo, "a cerveja somou no bêbado do fundo");
        Afirmar.Igual(CargaSorteada(3, Item.Bala, Item.Vodka, Item.Cerveja), naSubida.Depois.Carga, "a carga continua contando, com o sorteio feito");

        Disparar(c);
        Afirmar.Igual(new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 2, 2), c.Atual.Onda, "o pico do nível 2");
        Usado noPico = Usar(c, Item.Bala);
        ConferirSubida(noPico, new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 3, 3), "Engolir Bala; a paranoia sobe: Paranoico/Pico/2 -> Paranoico/Pico/3", TimeSpan.FromSeconds(40), "no pico, a mesma sintética de novo");
        Afirmar.Igual(bebadoNoTeto, noPico.Depois.OndaDeFundo, "a bala, absorvida pela paranoia, não mexe no fundo");
        Usado noTeto = Usar(c, Item.Vodka);
        ConferirSubida(noTeto, new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 3, 3), "Beber Vodka; a paranoia sobe: Paranoico/Pico/3 -> Paranoico/Pico/3", TimeSpan.FromSeconds(40), "no nível 3");
        Afirmar.Igual(noPico.Depois.GeracaoDaOnda + 1, noTeto.Depois.GeracaoDaOnda, "no nível 3, o pico recomeça com um disparo novo");

        while (c.Atual.Onda!.Fase != FaseDaOnda.Queda) Disparar(c);
        Afirmar.Igual(new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Queda, 1, 3), c.Atual.Onda, "a queda, com o pior 3");
        Usado naQueda = Usar(c, Item.LancaPerfume);
        ConferirSubida(naQueda, new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 2, 3), "Inalar LancaPerfume; a paranoia sobe: Paranoico/Queda/1 -> Paranoico/Pico/2", TimeSpan.FromSeconds(40), "na queda, uma sintética nova");
        Afirmar.Igual((bebadoNoTeto, CargaSorteada(6, Item.Bala, Item.Vodka, Item.Cerveja, Item.LancaPerfume)), (naQueda.Depois.OndaDeFundo, naQueda.Depois.Carga),
            "o lança-perfume foi absorvido, e a carga conta tudo");
    }

    /// <summary>A paranoia na frente subiu: a onda esperada, sem sorteio, a regra, o temporizador recomeçado e o uso que não a começou.</summary>
    private static void ConferirSubida(Usado u, EstadoDaOnda esperada, string regra, TimeSpan atraso, string caso)
    {
        Afirmar.Igual(esperada, u.Depois.Onda, $"{caso}: a paranoia sobe");
        Afirmar.Igual(u.AntesDoSoltar.AleatorioDaParanoia, u.Depois.AleatorioDaParanoia, $"{caso}: sem sorteio, o gerador da paranoia fica o mesmo");
        Afirmar.Igual($"ITEM_DRAG_END sobre o personagem: {regra}", u.Regra, $"{caso}: a regra");
        Afirmar.Sequencia([atraso], Agendados(u.Soltar), $"{caso}: a fase recomeça");
        Afirmar.Falso(u.Depois.Uso!.ComecouAParanoia, $"{caso}: subir não é começar");
    }

    // ---------------------------------------------------------------- um sorteio só por episódio
    //
    // "Quero que a chance dele ficar paranoico seja de 1 em 8" (pedido do usuário de 2026-10-01, 19:00), e a escolha dele às
    // 23:03, "uma vez por mistura": quando ele mistura uma droga sintética com outra substância, o uso que fecha a mistura faz
    // o único sorteio do episódio, de 1 em 8; usar mais coisas na mesma leva não aumenta a chance. (Com um sorteio a cada
    // substância, a chance crescia com o que ele usasse: três sorteios, perto de 33%; seis, perto de 55%.)

    // A paranoia acaba e o episódio continua (o bêbado de fundo volta, e é de substância), mas o sorteio dele já foi feito:
    // com 1 em 1, a vodka outra vez e uma sintética nova (o MD) não sorteiam, e a paranoia não volta. Acabadas as ondas, o
    // episódio acaba e a carga volta a zero, com o sorteio; o episódio seguinte (a cerveja e a cocaína) sorteia de novo, no
    // uso que fecha a mistura, e a paranoia começa outra vez, com o olhar pro teto no fim do uso.
    [Teste]
    public static void UmSorteioPorEpisodio_AParanoiaAcabadaNaoVoltaNoMesmoEpisodio()
    {
        Cenario c = Pausado(Sempre);
        AteAParanoia(c);
        while (c.Atual.Onda!.Tipo == Onda.Paranoico) Disparar(c);
        Afirmar.Igual((BebadoNaSubida2, (EstadoDaOnda?)null, CargaSorteada(2, Item.Bala, Item.Vodka)), (c.Atual.Onda, c.Atual.OndaDeFundo, c.Atual.Carga),
            "o bêbado voltou, com a carga do episódio, que já sorteou");
        foreach (Item item in new[] { Item.Vodka, Item.Md })
            ConferirSemSorteio(Usar(c, item), $"{item} no mesmo episódio, que já sorteou");
        Afirmar.Igual(CargaSorteada(4, Item.Bala, Item.Vodka, Item.Md), c.Atual.Carga, "a carga continua contando, com o sorteio feito");

        AcabarOEpisodio(c, "o episódio da bala e da vodka");
        ConferirSemSorteio(Usar(c, Item.Cerveja), "a cerveja abre o episódio seguinte");
        Usado cocaina = Usar(c, Item.Cocaina);
        Afirmar.Igual(UmPasso(cocaina.AntesDoSoltar.AleatorioDaParanoia), cocaina.Depois.AleatorioDaParanoia, "o episódio seguinte sorteia de novo, no uso que fecha a mistura");
        Afirmar.Igual((Onda.Paranoico, CargaSorteada(2, Item.Cerveja, Item.Cocaina)), (cocaina.Depois.Onda?.Tipo, cocaina.Depois.Carga), "e a paranoia começa outra vez");
        Afirmar.Verdadeiro(cocaina.Depois.Uso!.ComecouAParanoia, "este uso a começou");
        Afirmar.Igual(Gesto.OlharProTeto, c.Atual.Gesto, "e ele olha pro teto de novo no fim do uso");
    }

    // O sorteio que não sai também é o único do episódio: com a chance que nunca sai (0 em 1), a vodka e o MD fecham a mistura
    // e sorteiam (um passo do gerador da paranoia), sem paranoia e sem texto na regra do soltar; depois, a cocaína, o
    // lança-perfume, a bala e a cerveja, no mesmo episódio, não sorteiam mais. Acabadas as ondas, a carga volta a zero, com o
    // sorteio, e o episódio seguinte (a vodka e o MD de novo) sorteia outra vez.
    [Teste]
    public static void SorteioQueNaoSai_OEpisodioNaoSorteiaMais()
    {
        Cenario c = Pausado(NuncaParanoia);
        ConferirSemSorteio(Usar(c, Item.Vodka), "a vodka, sozinha");
        Usado md = Usar(c, Item.Md);
        Afirmar.Igual(UmPasso(md.AntesDoSoltar.AleatorioDaParanoia), md.Depois.AleatorioDaParanoia, "o MD fecha a mistura e sorteia: um passo do gerador da paranoia");
        Afirmar.Falso(md.Depois.Onda?.Tipo == Onda.Paranoico || md.Depois.Uso!.ComecouAParanoia, "o sorteio não saiu: sem paranoia");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Engolir Md", md.Regra, "o sorteio que não sai não deixa texto na regra do soltar");
        Afirmar.Igual(CargaSorteada(2, Item.Vodka, Item.Md), md.Depois.Carga, "a carga guarda que o episódio já sorteou");
        foreach (Item item in new[] { Item.Cocaina, Item.LancaPerfume, Item.Bala, Item.Cerveja })
            ConferirSemSorteio(Usar(c, item), $"{item}, no mesmo episódio, depois do sorteio");
        Afirmar.Igual(CargaSorteada(6, Item.Vodka, Item.Md, Item.Cocaina, Item.LancaPerfume, Item.Bala, Item.Cerveja), c.Atual.Carga, "a carga conta tudo, com o sorteio feito");

        AcabarOEpisodio(c, "o episódio da vodka e do MD");
        ConferirSemSorteio(Usar(c, Item.Vodka), "a vodka abre o episódio seguinte");
        Usado deNovo = Usar(c, Item.Md);
        Afirmar.Igual(UmPasso(deNovo.AntesDoSoltar.AleatorioDaParanoia), deNovo.Depois.AleatorioDaParanoia, "o episódio seguinte sorteia outra vez");
        Afirmar.Igual(CargaSorteada(2, Item.Vodka, Item.Md), deNovo.Depois.Carga, "com a carga nova");
    }

    // ---------------------------------------------------------------- o episódio: a carga zera tudo junto

    // O episódio acaba quando não sobra onda de substância, nem na frente nem no fundo: a carga volta a zero com tudo junto
    // (as substâncias, a sintética, os itens distintos e o sorteio feito; o sorteio, nos testes de um sorteio por episódio,
    // por AcabarOEpisodio), e o episódio seguinte começa do nada, sem o anterior contar para a mistura nem para o sorteio.
    // Com 1 em 1: o MD sozinho até o fim do eufórico, e depois a vodka e a cerveja não sorteiam; a água acaba o bêbado da
    // vodka, e o MD seguinte não sorteia; e o cigarro absorvido pelo ligado do café (uma onda leve) não deixa carga, e o MD
    // seguinte também não sorteia. Semeada, uma onda leve na frente com uma de substância no fundo: a carga fica, porque a
    // regra olha as duas; a cerveja seguinte fecha a mistura com o MD do episódio e sorteia; e, se o episódio já tinha
    // sorteado, o sorteio feito fica com o resto, e a cerveja não sorteia de novo.
    [Teste]
    public static void Carga_ZeraTudoJuntoQuandoNaoSobraOndaDeSubstancia()
    {
        Cenario md = Pausado(Sempre);
        Usar(md, Item.Md);
        int disparos = 0;
        while (md.Atual.Onda is not null)
        {
            Afirmar.Igual(Carga(1, true, Item.Md), md.Atual.Carga, "com o eufórico, a carga do MD fica");
            Afirmar.Verdadeiro(disparos++ < 10, "o eufórico acaba");
            Disparar(md);
        }
        Afirmar.Igual(CargaDaParanoia.Nenhuma, md.Atual.Carga, "o eufórico acabou: a carga volta a zero, tudo junto, no mesmo disparo");
        Afirmar.Falso(md.Retrato.Descrever().Contains("carga=", StringComparison.Ordinal), $"nem na linha: {md.Retrato.Descrever()}");
        foreach (Item item in new[] { Item.Vodka, Item.Cerveja })
            ConferirSemSorteio(Usar(md, item), $"{item} depois do episódio do MD");
        Afirmar.Igual(Carga(2, false, Item.Vodka, Item.Cerveja), md.Atual.Carga, "o episódio novo, sem sintética");

        Cenario agua = Pausado(Sempre);
        Usar(agua, Item.Vodka);
        for (int vez = 0; vez < 3; vez++) Usar(agua, Item.Agua);
        Afirmar.Igual(((EstadoDaOnda?)null, CargaDaParanoia.Nenhuma), (agua.Atual.Onda, agua.Atual.Carga), "três águas acabaram o bêbado da vodka: a carga volta a zero");
        ConferirSemSorteio(Usar(agua, Item.Md), "o MD depois do episódio da vodka");

        Cenario absorvido = Pausado(Sempre);
        Usar(absorvido, Item.Cafe);
        Usado cigarro = Usar(absorvido, Item.Cigarro);
        Afirmar.Igual((new EstadoDaOnda(Onda.Ligado, FaseDaOnda.Subida, 1, 1), CargaDaParanoia.Nenhuma), (cigarro.Depois.Onda, cigarro.Depois.Carga),
            "o cigarro absorvido pelo ligado (leve): sem onda de substância, a carga volta a zero no próprio soltar");
        Usado mdDepois = Usar(absorvido, Item.Md);
        ConferirSemSorteio(mdDepois, "o MD depois do cigarro absorvido");
        Afirmar.Igual(Carga(1, true, Item.Md), mdDepois.Depois.Carga, "o cigarro não conta");

        SimuladorDeTempo semeado = ComALeveNaFrente(Carga(1, true, Item.Md), "o MD no episódio");
        Aleatorio antesDaCerveja = semeado.Estado.AleatorioDaParanoia;
        SoltarSobreEle(semeado, InvocarEAssentar(semeado, Item.Cerveja).Id);
        Afirmar.Igual((ParanoiaComecando, CargaSorteada(2, Item.Md, Item.Cerveja), UmPasso(antesDaCerveja)), (semeado.Estado.Onda, semeado.Estado.Carga, semeado.Estado.AleatorioDaParanoia),
            "e a cerveja fecha a mistura com o MD do episódio e faz o sorteio dele");

        SimuladorDeTempo jaSorteado = ComALeveNaFrente(CargaSorteada(2, Item.Md, Item.Vodka), "o episódio que já sorteou");
        Aleatorio semSorteio = jaSorteado.Estado.AleatorioDaParanoia;
        SoltarSobreEle(jaSorteado, InvocarEAssentar(jaSorteado, Item.Cerveja).Id);
        Afirmar.Igual((Onda.Bebado, CargaSorteada(3, Item.Md, Item.Vodka, Item.Cerveja), semSorteio), (jaSorteado.Estado.Onda?.Tipo, jaSorteado.Estado.Carga, jaSorteado.Estado.AleatorioDaParanoia),
            "o sorteio feito ficou com o episódio, e a cerveja não sorteia de novo");
    }

    /// <summary>
    /// Semeada, sem a física e com a chance de 1 em 1: uma onda leve na frente (o alegre na queda), o bêbado no fundo e a
    /// carga dada. O primeiro disparo não vale (a semeadura não agendou nada), e o fim dele agenda a queda da leve; o
    /// segundo a acaba, e o bêbado volta. A carga fica, inteira, nos dois: a regra olha a frente e o fundo. Devolve o
    /// simulador, com o bêbado na frente.
    /// </summary>
    private static SimuladorDeTempo ComALeveNaFrente(CargaDaParanoia carga, string caso)
    {
        var sim = new SimuladorDeTempo(SemFisica() with { ChanceDaParanoia = Sempre }, 7, TopologiasDeExemplo.UmMonitor);
        sim.Aplicar(new CmdPauseAutonomy());
        sim = sim.Semeado(s => s with
        {
            Onda = new EstadoDaOnda(Onda.Alegre, FaseDaOnda.Queda, 1, 1),
            OndaDeFundo = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2),
            Carga = carga,
        });
        sim.Aplicar(new ItemEffectTimer(sim.Estado.GeracaoDaOnda));
        Afirmar.Igual(carga, sim.Estado.Carga, $"{caso}: a leve na frente, o bêbado no fundo: a carga fica");
        sim.Aplicar(new ItemEffectTimer(sim.Estado.GeracaoDaOnda));
        Afirmar.Igual((new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2), carga), (sim.Estado.Onda, sim.Estado.Carga), $"{caso}: a leve acabou e o bêbado voltou: a carga continua");
        return sim;
    }

    // A carga guarda os itens distintos num conjunto com igualdade por valor (um por item do enum): a ordem e a repetição
    // não importam, e um valor fora do enum não entra. A carga sem nada é a de partida.
    [Teste]
    public static void ConjuntoDeItens_ComIgualdadePorValor()
    {
        ConjuntoDeItens a = ConjuntoDeItens.Vazio.Com(Item.Md).Com(Item.Vodka).Com(Item.Md);
        ConjuntoDeItens b = ConjuntoDeItens.Vazio.Com(Item.Vodka).Com(Item.Md);
        Afirmar.Igual(a, b, "a ordem e a repetição não importam");
        Afirmar.Igual(2, a.Quantidade, "dois itens");
        Afirmar.Verdadeiro(a.Contem(Item.Md) && a.Contem(Item.Vodka) && !a.Contem(Item.Bala), "contém o MD e a vodka, e não a bala");
        Afirmar.Sequencia([Item.Vodka, Item.Md], a.Itens, "os itens, na ordem do enum");
        Afirmar.Igual("Vodka,Md", a.ToString(), "como texto");
        Afirmar.Igual(0, ConjuntoDeItens.Vazio.Quantidade, "o vazio");
        Afirmar.Igual(default, ConjuntoDeItens.Vazio, "o vazio é o valor padrão");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => ConjuntoDeItens.Vazio.Com((Item)13), "um item fora do enum não entra");
        Afirmar.Igual(new CargaDaParanoia(0, false, ConjuntoDeItens.Vazio, Sorteada: false), CargaDaParanoia.Nenhuma, "a carga sem nada, sem o sorteio");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, EstadoDoNucleo.Inicial(3).Carga, "é a de partida");
        Afirmar.Igual((true, false, false), (Carga(2, true, Item.Md, Item.Vodka).MisturaComSintetica, Carga(2, true, Item.Md).MisturaComSintetica, Carga(2, false, Item.Vodka, Item.Cerveja).MisturaComSintetica),
            "mistura com sintética: uma sintética e dois itens distintos");
        Afirmar.Igual(CargaSorteada(3, Item.Md, Item.Vodka), CargaSorteada(2, Item.Md, Item.Vodka).Com(TabelaDoTamagotchi.DoItem(Item.Md)),
            "mais uma substância não desfaz o sorteio do episódio");
    }

    // ---------------------------------------------------------------- o gerador próprio e a chance real

    // O gerador próprio da paranoia (decisão do coordenador): guardado no estado e semeado da semente do núcleo (semente × 41
    // + 13), à parte do principal, que continua o de sempre: os sorteios da paranoia não mudam a agenda, as caras nem as
    // referências gravadas. A chance da configuração é a do pedido do usuário, 1 em 8.
    [Teste]
    public static void GeradorProprio_SemeadoDaSementeDoNucleo()
    {
        foreach (ulong semente in new ulong[] { 0, 1, 7, 2028, 2048, ulong.MaxValue })
        {
            EstadoDoNucleo s = EstadoDoNucleo.Inicial(semente);
            Afirmar.Igual(new Aleatorio(semente), s.Aleatorio, $"semente {semente}: o gerador principal, como sempre");
            Afirmar.Igual(new Aleatorio(unchecked((semente * 41) + 13)), s.AleatorioDaParanoia, $"semente {semente}: o da paranoia, semente × 41 + 13");
            Afirmar.Igual(s, new Nucleo(new ConfiguracaoDoNucleo(), semente).Estado, $"semente {semente}: o núcleo parte desse estado");
        }
        Afirmar.Igual(new Chance(1, 8), new ConfiguracaoDoNucleo().ChanceDaParanoia, "a chance padrão, 1 em 8");
    }

    /// <summary>Os itens com o uso de 1 passo: as simulações longas da chance real andam rápido.</summary>
    private static DadosDoItem UsoDeUmPasso(Item item) => TabelaDoTamagotchi.DoItem(item) with { PassosDoUso = 1 };

    /// <summary>
    /// Invoca o item e o solta sobre ele ainda no ar, e o uso de 1 passo acaba no passo seguinte; devolve o estado logo antes
    /// do soltar e o resultado do soltar.
    /// </summary>
    private static Usado UsarRapido(Cenario c, Item item)
    {
        c.Aplicar(new CmdSummonItem(item));
        ItemNoMundo it = c.Atual.Itens.Todos[^1];
        Evento[] gesto = Arraste(it, Cenario.MeioDoPersonagem(c.Atual, c.Config));
        c.Aplicar(gesto[..^1]);
        EstadoDoNucleo antes = c.Atual;
        c.Aplicar(gesto[^1]).Esta(Estado.Using, $"usando {item}");
        Resultado soltar = Afirmar.NaoNulo(c.Ultimo, "o soltar");
        c.Aplicar(new Tick()).Esta(Estado.Idle, $"fim do uso de {item}");
        return new Usado(antes, soltar);
    }

    /// <summary>
    /// A configuração das simulações da chance real: a do núcleo com o tamagotchi, sem a física, com o uso encurtado e a
    /// chance da paranoia do aplicativo (<see cref="ConfiguracaoDoNucleo.DoAplicativo"/>), a que o usuário vê.
    /// </summary>
    private static ConfiguracaoDoNucleo DaChanceReal()
        => SemFisica() with { TabelaDeItens = UsoDeUmPasso, ChanceDaParanoia = ConfiguracaoDoNucleo.DoAplicativo(Sprite).ChanceDaParanoia };

    /// <summary>As substâncias de depois do sorteio, em rodízio: as nove da transcrição.</summary>
    private static readonly Item[] Substancias = [.. TabelasDoDesenho.ItensEsperados().Where(i => !i.Alivio).Select(i => i.Item)];

    /// <summary>
    /// Um episódio de mistura com sintética, com o uso encurtado: os dois primeiros itens (o segundo fecha a mistura e faz o
    /// sorteio do episódio) e mais as substâncias de <paramref name="depois"/>, que não sorteiam (com a paranoia na frente, a
    /// sobem). Confere em cada uso que o gerador principal não muda no soltar, que o da paranoia dá exatamente um passo no
    /// episódio, no uso que fecha a mistura, e que só esse uso pode começar a paranoia. Devolve se ele ficou paranoico.
    /// </summary>
    private static bool EpisodioDeMistura(Cenario c, Item primeiro, Item fecha, IEnumerable<Item> depois, string caso)
    {
        Aleatorio inicio = c.Atual.AleatorioDaParanoia;
        ConferirSemSorteio(UsarRapido(c, primeiro), $"{caso}: {primeiro}, sozinho");
        Usado sorteio = UsarRapido(c, fecha);
        Afirmar.Igual(sorteio.AntesDoSoltar.Aleatorio, sorteio.Depois.Aleatorio, $"{caso}: {fecha}: o gerador principal não muda");
        Afirmar.Igual(UmPasso(inicio), sorteio.Depois.AleatorioDaParanoia, $"{caso}: {fecha} fecha a mistura e sorteia, um passo do gerador da paranoia");
        bool paranoico = sorteio.Depois.Onda?.Tipo == Onda.Paranoico;
        Afirmar.Igual(paranoico, sorteio.Depois.Uso!.ComecouAParanoia, $"{caso}: o uso começou a paranoia se e só se ela saiu");
        foreach (Item item in depois)
        {
            Usado u = UsarRapido(c, item);
            Afirmar.Igual((u.AntesDoSoltar.Aleatorio, u.AntesDoSoltar.AleatorioDaParanoia), (u.Depois.Aleatorio, u.Depois.AleatorioDaParanoia),
                $"{caso}: {item}, depois do sorteio do episódio: nenhum gerador muda");
            Afirmar.Falso(u.Depois.Uso!.ComecouAParanoia, $"{caso}: {item} não começa a paranoia");
            Afirmar.Igual(paranoico, u.Depois.Onda?.Tipo == Onda.Paranoico, $"{caso}: {item}: a paranoia continua como o sorteio a deixou");
        }
        return paranoico;
    }

    /// <summary>
    /// A simulação da chance real por episódio, num núcleo só (<see cref="DaChanceReal"/>): cada episódio tem
    /// <paramref name="substancias"/> usos de substância (2 ou mais): o MD sozinho, a vodka, que fecha a mistura com
    /// sintética e faz o sorteio, e mais as da transcrição, em rodízio (<see cref="EpisodioDeMistura"/>); depois, os disparos
    /// da onda até não sobrar nenhuma, e o episódio acaba, com a carga de volta a zero (<see cref="AcabarOEpisodio"/>).
    /// Devolve se cada episódio o deixou paranoico e o estado final.
    /// </summary>
    private static (List<bool> Paranoicos, EstadoDoNucleo Final) SimularEpisodios(ulong semente, int episodios, int substancias)
    {
        Cenario c = Cenario.Parado(DaChanceReal(), semente: semente).Aplicar(new CmdPauseAutonomy());
        var paranoicos = new List<bool>(episodios);
        int depois = substancias - 2;
        for (int e = 0; e < episodios; e++)
        {
            string caso = $"semente {semente}, {substancias} substâncias, episódio {e + 1}";
            paranoicos.Add(EpisodioDeMistura(c, Item.Md, Item.Vodka, Enumerable.Range(depois * e, depois).Select(k => Substancias[k % Substancias.Length]), caso));
            AcabarOEpisodio(c, caso);
        }
        return (paranoicos, c.Atual);
    }

    /// <summary>Quantos episódios (ou sementes) cada conferência da chance real conta.</summary>
    private const int EpisodiosDaChance = 4000;

    /// <summary>
    /// A faixa aceita de episódios paranoicos em <see cref="EpisodiosDaChance"/>, com a chance de 1 em 8: 500 ± 4 desvios-padrão
    /// da binomial (σ = √(4000 · 1/8 · 7/8) ≈ 20,9), de 417 a 583. Um gerador honesto só cai fora por azar uma vez em mais de
    /// 15 mil; com um sorteio a cada substância depois da mistura, 4 substâncias por episódio dariam perto de 1320 (três
    /// sorteios, 1 − (7/8)³ ≈ 33%) e 8 dariam perto de 2430 (sete sorteios, 1 − (7/8)⁷ ≈ 61%), longe da faixa.
    /// </summary>
    private static bool PertoDeUmEmOito(int paranoicos) => paranoicos is >= 417 and <= 583;

    // A chance real, 1 em 8 por episódio (pedido do usuário de 2026-10-01, 19:00, "quero que a chance dele ficar paranoico
    // seja de 1 em 8", e a escolha dele às 23:03, "uma vez por mistura": usar mais coisas na mesma leva não aumenta a chance),
    // com a chance do aplicativo, em simulações longas (SimularEpisodios): em 4000 episódios de mistura com sintética, de 2,
    // de 4 e de 8 substâncias, cada tamanho com a sua semente, ele fica paranoico perto de 500 vezes em cada um, dentro da
    // tolerância explícita (PertoDeUmEmOito).
    [Teste]
    public static void ChanceReal_UmEmOitoPorEpisodio_ComDuasQuatroEOitoSubstancias()
    {
        foreach (int substancias in new[] { 2, 4, 8 })
        {
            ulong semente = 20261000 + (ulong)substancias;
            (List<bool> paranoicos, _) = SimularEpisodios(semente, EpisodiosDaChance, substancias);
            int ficou = paranoicos.Count(p => p);
            Console.WriteLine($"         {substancias} substâncias por episódio (semente {semente}): paranoico em {ficou} de {EpisodiosDaChance} episódios ({100.0 * ficou / EpisodiosDaChance:0.00}%; 1/8 = 12,50%)");
            Afirmar.Verdadeiro(PertoDeUmEmOito(ficou), $"{substancias} substâncias por episódio: {ficou} de {EpisodiosDaChance}, fora de 500 ± 4σ (417 a 583)");
        }
    }

    // A mesma semente dá o mesmo resultado, episódio a episódio e no estado final (o retrato, os dois geradores e a carga);
    // outra semente, outra sequência. E, com um sorteio por episódio, o tamanho do episódio não muda nada no sorteio: com a
    // mesma semente, os episódios de 2 e de 8 substâncias deixam ele paranoico exatamente nos mesmos episódios, e o gerador da
    // paranoia termina no mesmo lugar, um passo por episódio (as substâncias a mais não sorteiam).
    [Teste]
    public static void ChanceReal_MesmaSementeMesmoResultado_EOTamanhoDoEpisodioNaoMudaOSorteio()
    {
        const int Episodios = 500;
        const ulong Semente = 20261001;
        (List<bool> oito, EstadoDoNucleo final) = SimularEpisodios(Semente, Episodios, 8);
        (List<bool> deNovo, EstadoDoNucleo finalDeNovo) = SimularEpisodios(Semente, Episodios, 8);
        Afirmar.Sequencia(oito, deNovo, "a mesma semente, os mesmos episódios");
        Afirmar.Igual((final.Retrato().Descrever(), final.Aleatorio, final.AleatorioDaParanoia, final.Carga), (finalDeNovo.Retrato().Descrever(), finalDeNovo.Aleatorio, finalDeNovo.AleatorioDaParanoia, finalDeNovo.Carga),
            "e o mesmo estado final: o retrato, os dois geradores e a carga");
        (List<bool> outra, _) = SimularEpisodios(Semente + 1, Episodios, 8);
        Afirmar.Falso(outra.SequenceEqual(oito), "outra semente, outra sequência");

        (List<bool> dois, EstadoDoNucleo finalComDois) = SimularEpisodios(Semente, Episodios, 2);
        Afirmar.Sequencia(oito, dois, "com 2 ou com 8 substâncias, paranoico nos mesmos episódios: só o uso que fecha a mistura sorteia");
        Aleatorio passos = new(EstadoDoNucleo.SementeDaParanoia(Semente));
        for (int e = 0; e < Episodios; e++) passos = UmPasso(passos);
        Afirmar.Igual((passos, passos), (final.AleatorioDaParanoia, finalComDois.AleatorioDaParanoia), "o gerador da paranoia andou um passo por episódio, com 2 ou com 8 substâncias");
    }

    // De semente em semente: cada execução do aplicativo tem a sua (do relógio, sem --semente), e o primeiro episódio de
    // mistura de cada uma também o deixa paranoico perto de 1 em 8. Em 4000 sementes seguidas, num núcleo novo para cada uma,
    // um episódio de 8 substâncias: a vodka, o MD (que fecha a mistura e faz o primeiro sorteio do gerador da paranoia) e mais
    // seis, a cocaína, a cerveja, o lança-perfume, o baseado, a bala e o cogumelo, que não sorteiam. Ele fica paranoico perto
    // de 500 vezes, com a mesma tolerância (PertoDeUmEmOito), e o gerador da paranoia só deu o primeiro passo.
    [Teste]
    public static void ChanceReal_UmEmOitoPorEpisodio_DeSementeEmSemente()
    {
        ConfiguracaoDoNucleo cfg = DaChanceReal();
        Item[] depois = [Item.Cocaina, Item.Cerveja, Item.LancaPerfume, Item.Baseado, Item.Bala, Item.Cogumelo];
        int ficou = 0;
        for (ulong semente = 1; semente <= EpisodiosDaChance; semente++)
        {
            Cenario c = Cenario.Parado(cfg, semente: semente).Aplicar(new CmdPauseAutonomy());
            if (EpisodioDeMistura(c, Item.Vodka, Item.Md, depois, $"semente {semente}")) ficou++;
            Afirmar.Igual(UmPasso(new Aleatorio(unchecked((semente * 41) + 13))), c.Atual.AleatorioDaParanoia, $"semente {semente}: só o primeiro passo do gerador da paranoia");
        }
        Console.WriteLine($"         ficou paranoico no primeiro episódio, de 8 substâncias, de {ficou} de {EpisodiosDaChance} sementes ({100.0 * ficou / EpisodiosDaChance:0.00}%)");
        Afirmar.Verdadeiro(PertoDeUmEmOito(ficou), $"perto de 1 em 8: {ficou} de {EpisodiosDaChance}, fora de 500 ± 4σ (417 a 583)");
    }

    // ---------------------------------------------------------------- a onda: fases, durações, caras e perfis

    // As fases e as durações, pelos itens: a subida de 1 s; um pico de 40 s por nível; a queda de 15 s × 100, 125 ou 150%,
    // pelo pior nível (15, 18,75 e 22,5 s); e o fim, com a frente anterior de volta (o bêbado, na subida de 8 s). Nos níveis 2
    // e 3, a paranoia sobe na subida, com uma e duas cervejas (que somam também no bêbado do fundo).
    [Teste]
    public static void FasesEDuracoes_PelosItens()
    {
        foreach (int nivel in new[] { 1, 2, 3 })
        {
            Cenario c = Pausado(Sempre);
            Usado inicio = AteAParanoia(c);
            var atrasos = new List<TimeSpan>(Agendados(inicio.Soltar));
            for (int n = 2; n <= nivel; n++) atrasos = [.. Agendados(Usar(c, Item.Cerveja).Soltar)];
            Afirmar.Igual(new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Subida, nivel, nivel), c.Atual.Onda, $"nível {nivel}: na subida");
            EstadoDaOnda bebado = nivel == 1 ? BebadoNaSubida2 : new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 3, 3);
            var fases = new List<EstadoDaOnda?>();
            while (c.Atual.Onda!.Tipo == Onda.Paranoico)
            {
                Afirmar.Verdadeiro(fases.Count < 10, $"nível {nivel}: a paranoia acaba");
                Resultado r = Disparar(c);
                fases.Add(r.Estado.Onda);
                atrasos.AddRange(Agendados(r));
            }
            TimeSpan queda = TimeSpan.FromMilliseconds(15000 * (100 + 25 * (nivel - 1)) / 100);
            TimeSpan[] esperados = [TimeSpan.FromSeconds(1), .. Enumerable.Repeat(TimeSpan.FromSeconds(40), nivel), queda, TimeSpan.FromSeconds(8)];
            Afirmar.Sequencia(esperados, atrasos, $"nível {nivel}: subida, {nivel} níveis do pico, a queda e a subida do bêbado que voltou");
            EstadoDaOnda?[] fasesEsperadas =
            [
                .. Enumerable.Range(0, nivel).Select(k => (EstadoDaOnda?)new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, nivel - k, nivel)),
                new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Queda, 1, nivel),
                bebado,
            ];
            Afirmar.Sequencia(fasesEsperadas, fases, $"nível {nivel}: as fases");
            Afirmar.Igual(2 + nivel, fases.Count, $"nível {nivel}: 2 + nível disparos (invariante 25)");
        }
        DadosDaOnda d = TabelaDoTamagotchi.DaOnda(Onda.Paranoico);
        Afirmar.Igual(TimeSpan.FromSeconds(143.5), d.Duracao(FaseDaOnda.Subida, 3) + 3 * d.Duracao(FaseDaOnda.Pico, 3) + d.Duracao(FaseDaOnda.Queda, 3), "o episódio mais longo: 1 + 3 × 40 + 22,5 s");
    }

    // As caras: assustado na subida, paranoico no pico, sonolento na queda, cada uma na hora em que a fase começa, com a
    // cara livre; no fim, a de base (aqui, a da subida do bêbado que voltou). Durante o uso, a cara de quem usa espera o fim.
    [Teste]
    public static void Caras_DeCadaFase()
    {
        DadosDaOnda d = TabelaDoTamagotchi.DaOnda(Onda.Paranoico);
        Afirmar.Igual((Expressao.Assustado, Expressao.Paranoico, Expressao.Sonolento), (d.Cara(FaseDaOnda.Subida), d.Cara(FaseDaOnda.Pico), d.Cara(FaseDaOnda.Queda)), "as caras da tabela");

        Cenario c = Pausado(Sempre);
        AteAParanoia(c);
        Afirmar.Igual(Expressao.Assustado, c.Atual.Expressao, "no fim do uso, ainda na subida: assustado");
        Afirmar.Igual(Expressao.Paranoico, Disparar(c).Estado.Expressao, "no pico: paranoico");
        Afirmar.Igual(Expressao.Sonolento, Disparar(c).Estado.Expressao, "na queda: sonolento");
        Afirmar.Igual(Expressao.Feliz, Disparar(c).Estado.Expressao, "no fim, a da subida do bêbado que voltou");

        Cenario usando = Pausado(Sempre);
        Usar(usando, Item.Bala);
        ItemNoMundo vodka = InvocarEAssentar(usando, Item.Vodka);
        usando.SoltarSobreEle(vodka.Id);
        Afirmar.Igual(Onda.Paranoico, usando.Atual.Onda?.Tipo, "a paranoia começou no soltar");
        Disparar(usando);
        Afirmar.Igual((FaseDaOnda.Pico, Expressao.Determinado), (usando.Atual.Onda!.Fase, usando.Atual.Expressao), "o pico chega durante o uso: a cara de quem bebe a vodka continua");
        usando.Passos(usando.Atual.PassosRestantes);
        Afirmar.Igual(Expressao.Paranoico, usando.Atual.Expressao, "no fim do uso, a do pico");
    }

    // O perfil no pico e na queda (tabela da decisão), pelo PerfilEfetivo e pela FisicaEfetiva, contado à mão na energia
    // média (decisão 8–20 s, descanso 30–90 s, pesos 4/2/1/3/3/2, pulo 50–100, foguete 30, parede 15–35 s, pendurado 3–8 s;
    // andar 90, escalar 110 e pendurar 80 DIP/s). No pico, igual nos três níveis e na subida: decide mais vezes, descansa
    // pouco, anda devagar (70%), gesticula muito, e escalar, pular, descansar e o foguete vão a zero. Na queda, o cansaço.
    [Teste]
    public static void Perfil_NoPicoENaQueda()
    {
        ConfiguracaoDoNucleo cfg = SemFisica();
        EstadoDoNucleo inicial = EstadoDoNucleo.Inicial(1);
        foreach (EstadoDaOnda pico in new EstadoDaOnda[] { new(Onda.Paranoico, FaseDaOnda.Subida, 1, 1), new(Onda.Paranoico, FaseDaOnda.Pico, 1, 1), new(Onda.Paranoico, FaseDaOnda.Pico, 2, 2), new(Onda.Paranoico, FaseDaOnda.Pico, 3, 3) })
        {
            string caso = $"{pico.Fase} {pico.Nivel}";
            EstadoDoNucleo s = inicial with { Onda = pico };
            PerfilDeEnergia p = Maquina.PerfilEfetivo(s, cfg);
            Afirmar.Igual((TimeSpan.FromMilliseconds(4800), TimeSpan.FromSeconds(12)), (p.DecisaoMinima, p.DecisaoMaxima), $"{caso}: decisão a 60%");
            Afirmar.Igual((TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(27)), (p.DescansoMinimo, p.DescansoMaximo), $"{caso}: descanso a 30%");
            Afirmar.Igual((2, 0, 0, 0, 9, 3), (p.PesoAndar, p.PesoEscalar, p.PesoPular, p.PesoDescansar, p.PesoGesto, p.PesoTrocarExpressao), $"{caso}: andar 50%, escalar, pular e descansar 0, gesto 300%, troca 150%");
            Afirmar.Igual((50, 100, 0), (p.AlturaDoPuloMinima, p.AlturaDoPuloMaxima, p.ChanceDoFoguete), $"{caso}: pulo a 100% e foguete 0");
            Afirmar.Igual((TimeSpan.FromMilliseconds(21428), TimeSpan.FromSeconds(50)), (p.TempoNaParedeMinimo, p.TempoNaParedeMaximo), $"{caso}: parede × 100/70");
            Afirmar.Igual((TimeSpan.FromMilliseconds(4285), TimeSpan.FromMilliseconds(11428)), (p.TempoPenduradoMinimo, p.TempoPenduradoMaximo), $"{caso}: pendurado × 100/70");
            ParametrosDeMovimento f = Maquina.FisicaEfetiva(s, cfg);
            Afirmar.Igual(cfg.Fisica with { VelocidadeAndando = 63, VelocidadeEscalando = 77, VelocidadePendurado = 56 }, f, $"{caso}: só as três velocidades, a 70%");
        }

        EstadoDoNucleo naQueda = inicial with { Onda = new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Queda, 1, 2) };
        PerfilDeEnergia q = Maquina.PerfilEfetivo(naQueda, cfg);
        Afirmar.Igual((TimeSpan.FromMilliseconds(9600), TimeSpan.FromSeconds(24)), (q.DecisaoMinima, q.DecisaoMaxima), "queda: decisão a 120%");
        Afirmar.Igual((TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(135)), (q.DescansoMinimo, q.DescansoMaximo), "queda: descanso a 150%");
        Afirmar.Igual((3, 1, 1, 5, 3, 2), (q.PesoAndar, q.PesoEscalar, q.PesoPular, q.PesoDescansar, q.PesoGesto, q.PesoTrocarExpressao), "queda: andar 80%, escalar e pular 50%, descansar 150%, gesto e troca 100%");
        Afirmar.Igual((50, 100, 30), (q.AlturaDoPuloMinima, q.AlturaDoPuloMaxima, q.ChanceDoFoguete), "queda: pulo a 100% e o foguete do perfil");
        Afirmar.Igual((TimeSpan.FromMilliseconds(17647), TimeSpan.FromMilliseconds(41176)), (q.TempoNaParedeMinimo, q.TempoNaParedeMaximo), "queda: parede × 100/85");
        Afirmar.Igual((TimeSpan.FromMilliseconds(3529), TimeSpan.FromMilliseconds(9411)), (q.TempoPenduradoMinimo, q.TempoPenduradoMaximo), "queda: pendurado × 100/85");
        Afirmar.Igual(cfg.Fisica with { VelocidadeAndando = 76.5, VelocidadeEscalando = 93.5, VelocidadePendurado = 68 }, Maquina.FisicaEfetiva(naQueda, cfg), "queda: as três velocidades a 85%, sem cambaleio");
        Afirmar.Igual(1.0, Maquina.FatorDoCambaleio(24, TabelaDoTamagotchi.DaOnda(Onda.Paranoico).Perfil(FaseDaOnda.Pico, 3).Cambaleio), "anda reto: sem cambaleio");
    }

    /// <summary>Uma tabela com a paranoia no pico por duas horas: ela fica no pico durante toda a simulação.</summary>
    private static DadosDaOnda PicoLongo(Onda onda) => TabelaDoTamagotchi.DaOnda(onda) with { NivelDoPico = TimeSpan.FromHours(2) };

    // No pico, com a física do aplicativo e a agenda livre, com todas as ações, por meia hora em cada uma de 6 sementes: ele
    // nunca escala, pula nem descansa por conta própria (os pesos vão a zero, até no fim da caminhada junto a uma parede),
    // mas anda; as trocas de cara só sorteiam as caras do pico (a paranoica, a mais pesada, é a mais sorteada); e os gestos
    // só os do pico, todos, com o olhar pro teto como o mais sorteado.
    [Teste]
    public static void NoPico_NuncaEscalaPulaNemDescansa_ECarasEGestosDaFase()
    {
        ConfiguracaoDoNucleo cfg = ComFisica() with { TabelaDeOndas = PicoLongo, Acoes = AcoesAutonomas.Todas };
        // As caras e os gestos do pico, pela decisão (à parte da tabela do núcleo).
        Expressao[] carasDoPico = [Expressao.Paranoico, Expressao.Assustado, Expressao.Surpreso];
        Gesto[] gestosDoPico = [Gesto.OlharProTeto, Gesto.Agachar, Gesto.Tremedeira, Gesto.OlharAoRedor, Gesto.Espiar];
        var caras = new Dictionary<Expressao, int>();
        var gestos = new Dictionary<Gesto, int>();
        long passosAndando = 0, decisoes = 0;
        for (ulong semente = 1; semente <= 6; semente++)
        {
            var sim = new SimuladorDeTempo(cfg, semente, TopologiasDeExemplo.UmMonitor);
            sim = sim.Semeado(s => s with { Onda = new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 1, 1), Expressao = Expressao.Paranoico, Carga = CargaSorteada(2, Item.Bala, Item.Vodka) });
            sim.Aplicar(new ItemEffectTimer(sim.Estado.GeracaoDaOnda));
            sim.AoResultado = (antes, e, r) =>
            {
                string onde = $"semente {semente}, {e} em {antes.Estado}";
                Afirmar.Igual(Onda.Paranoico, r.Estado.Onda?.Tipo, $"{onde}: no pico da paranoia");
                foreach (Transicao t in r.Transicoes)
                    Afirmar.Falso(t.Para is Estado.Climbing or Estado.Jumping or Estado.Resting or Estado.Hanging, $"{onde}: {t}");
                if (e is Tick && r.Estado.Estado == Estado.Walking) passosAndando++;
                if (e is not AutonomyTimer || antes.Estado != Estado.Idle) return;
                // Parado, a agenda escolhe andar (uma transição), um gesto (uma transição para o mesmo estado, com o gesto) ou
                // trocar a cara (nenhuma transição; a cara pode repetir).
                decisoes++;
                if (r.Estado.Gesto != Gesto.Nenhum) gestos[r.Estado.Gesto] = gestos.GetValueOrDefault(r.Estado.Gesto) + 1;
                else if (r.Transicoes.Count == 0) caras[r.Estado.Expressao] = caras.GetValueOrDefault(r.Estado.Expressao) + 1;
            };
            sim.Avancar(TimeSpan.FromMinutes(30));
        }
        string Contagem<T>(Dictionary<T, int> vezes) where T : notnull => string.Join(", ", vezes.OrderByDescending(v => v.Value).Select(v => $"{v.Key}={v.Value}"));
        Console.WriteLine($"         {decisoes} decisões em 3 horas no pico; {passosAndando} passos andando; caras: {Contagem(caras)}; gestos: {Contagem(gestos)}");
        Afirmar.Verdadeiro(passosAndando > 1000, $"ele anda: {passosAndando} passos");
        Afirmar.Sequencia(carasDoPico.Order(), caras.Keys.Order(), "as caras do pico, e só elas");
        Afirmar.Igual(Expressao.Paranoico, caras.MaxBy(c => c.Value).Key, "a paranoica é a mais sorteada");
        Afirmar.Sequencia(gestosDoPico.Order(), gestos.Keys.Order(), "os gestos do pico, todos, e só eles");
        Afirmar.Igual(Gesto.OlharProTeto, gestos.MaxBy(g => g.Value).Key, "olhar pro teto é o mais sorteado");
    }

    // ---------------------------------------------------------------- o gesto do começo e o usuário

    // O uso que começa a paranoia acaba, no chão, com ele em IDLE: na hora, olha pro teto, por 90 passos, sem sorteio (os
    // dois geradores ficam os mesmos, e a agenda espera o fim do gesto), com uma transição que diz por quê. O relógio corre no
    // gesto; no 90º passo ele acaba e a agenda volta. Vale também com a autonomia pausada (não é decisão da agenda). O uso
    // seguinte, que só sobe a paranoia, não traz gesto.
    [Teste]
    public static void GestoImediato_OlhaProTetoNoFimDoUsoQueAComecou()
    {
        foreach (bool pausado in new[] { false, true })
        {
            string caso = pausado ? "pausado" : "com a agenda livre";
            Cenario c = Cenario.Parado(SemFisica() with { Acoes = AcoesAutonomas.Nenhuma, ChanceDaParanoia = Sempre });
            if (pausado) c.Aplicar(new CmdPauseAutonomy());
            Usar(c, Item.Bala);
            ItemNoMundo vodka = InvocarEAssentar(c, Item.Vodka);
            c.SoltarSobreEle(vodka.Id);
            Afirmar.Igual(Onda.Paranoico, c.Atual.Onda?.Tipo, $"{caso}: a paranoia começou no soltar");
            c.Passos(c.Atual.PassosRestantes - 1);
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(new Tick());
            Afirmar.Sequencia(
                [new Transicao(Estado.Using, Estado.Settling, "USING: fim do uso de Vodka"), new Transicao(Estado.Settling, Estado.Idle, "SETTLING com apoio"),
                 new Transicao(Estado.Idle, Estado.Idle, "IDLE: a paranoia começou, gesto OlharProTeto")],
                c.Transicoes, $"{caso}: o fim do uso e o gesto");
            Afirmar.Igual((Gesto.OlharProTeto, 90), (c.Atual.Gesto, c.Atual.PassosDoGesto), $"{caso}: olha pro teto por 90 passos");
            Afirmar.Igual((antes.Aleatorio, antes.AleatorioDaParanoia), (c.Atual.Aleatorio, c.Atual.AleatorioDaParanoia), $"{caso}: sem sorteio");
            Afirmar.Verdadeiro(c.Atual.RelogioAtivo && !c.Tem<DesligarRelogio>() && !c.Tem<AgendarDecisao>(), $"{caso}: o relógio continua e a agenda espera o gesto");
            for (int passo = 1; passo < 90; passo++)
            {
                c.Aplicar(new Tick()).SemTransicao();
                Afirmar.Igual(Gesto.OlharProTeto, c.Atual.Gesto, $"{caso}: passo {passo} do gesto");
            }
            c.Aplicar(new Tick());
            Afirmar.Igual(Gesto.Nenhum, c.Atual.Gesto, $"{caso}: no 90º passo, o gesto acaba");
            Afirmar.Verdadeiro(c.Tem<DesligarRelogio>() && c.Tem<AgendarDecisao>() != pausado, $"{caso}: o relógio desliga e a agenda volta (pausado, não)");

            Usar(c, Item.Cerveja);
            Afirmar.Igual(Gesto.Nenhum, c.Atual.Gesto, $"{caso}: o uso que só sobe a paranoia não traz gesto");
        }
    }

    // O usuário prevalece: um PRESS no meio do uso que começa a paranoia o segura no mesmo evento (o uso acaba, a paranoia
    // continua, sem mexer no temporizador dela), e o clique depois não traz o olhar pro teto (o uso não foi até o fim). Um
    // PRESS no meio do olhar pro teto acaba o gesto na hora.
    [Teste]
    public static void PressNoMeio_ContinuaPrioritario()
    {
        Cenario c = Pausado(Sempre);
        Usar(c, Item.Bala);
        ItemNoMundo vodka = InvocarEAssentar(c, Item.Vodka);
        c.SoltarSobreEle(vodka.Id).Passos(30);
        Afirmar.Igual(ParanoiaComecando, c.Atual.Onda, "a paranoia começou no soltar");
        long geracao = c.Atual.GeracaoDaOnda;
        c.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Using, Estado.Pressed);
        Afirmar.Nulo(c.Atual.Uso, "o uso acabou");
        Afirmar.Igual((ParanoiaComecando, BebadoNaSubida2, CargaSorteada(2, Item.Bala, Item.Vodka), geracao), (c.Atual.Onda, c.Atual.OndaDeFundo, c.Atual.Carga, c.Atual.GeracaoDaOnda),
            "a paranoia continua, com o mesmo disparo");
        Afirmar.Falso(c.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem mexer no temporizador dela");
        c.Aplicar(new Click()).Esta(Estado.Reacting);
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, "fim da reação");
        Afirmar.Igual(Gesto.Nenhum, c.Atual.Gesto, "sem o olhar pro teto: o uso não foi até o fim");

        Cenario olhando = Pausado(Sempre);
        AteAParanoia(olhando);
        Afirmar.Igual(Gesto.OlharProTeto, olhando.Atual.Gesto, "olhando pro teto");
        olhando.Passos(10);
        olhando.Aplicar(new Press(Cenario.PontoOpaco)).Percorreu(Estado.Idle, Estado.Pressed);
        Afirmar.Igual(Gesto.Nenhum, olhando.Atual.Gesto, "o PRESS acaba o gesto na hora");
    }

    // DEC-024: preso pelo usuário na parede ou no cipó, ele usa a bala e a vodka ali; a paranoia começa, e ele volta
    // agarrado e preso, sem o olhar pro teto (só em IDLE). Com a agenda livre e a paranoia correndo (subida, pico, queda e
    // o fundo de volta), por cinco minutos, ele continua preso no mesmo apoio: nunca cai, nunca vai ao chão.
    [Teste]
    public static void Preso_ContinuaPreso()
    {
        foreach (bool noCipo in new[] { false, true })
        {
            string caso = noCipo ? "no cipó" : "na parede";
            Estado apoio = noCipo ? Estado.Hanging : Estado.Climbing;
            var sim = new SimuladorDeTempo(ComFisica() with { ChanceDaParanoia = Sempre }, 6, TopologiasDeExemplo.UmMonitor);
            Superficies sup = Sup(sim.Estado);
            SoltarPersonagemEm(sim, noCipo ? new PontoPx(960, sup.Teto + 12) : new PontoPx(sup.Direita - 20, 600));
            sim.Esta(apoio, $"{caso}: posto lá");
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario, $"{caso}: preso");
            foreach (Item item in new[] { Item.Bala, Item.Vodka })
            {
                SoltarSobreEle(sim, InvocarEAssentar(sim, item).Id);
                sim.Esta(Estado.Using, $"{caso}: usando {item}");
                sim.Passos(sim.Estado.PassosRestantes);
                sim.Esta(apoio, $"{caso}: de volta depois de {item}");
            }
            Afirmar.Igual(Onda.Paranoico, sim.Estado.Onda?.Tipo, $"{caso}: a paranoia começou");
            Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, $"{caso}: agarrado e preso");
            Afirmar.Igual(Gesto.Nenhum, sim.Estado.Gesto, $"{caso}: sem o olhar pro teto fora de IDLE");
            sim.AoResultado = (_, e, r) =>
            {
                Afirmar.Igual(apoio, r.Estado.Estado, $"{caso}: {e} o tirou do apoio");
                Afirmar.Verdadeiro(r.Estado.PresoPeloUsuario, $"{caso}: {e} o soltou");
            };
            sim.Avancar(TimeSpan.FromMinutes(5));
            Afirmar.Igual(Onda.Bebado, sim.Estado.Onda?.Tipo, $"{caso}: a paranoia acabou, e o bêbado voltou");
        }
    }

    // DEC-025: escondido atrás da borda de baixo, ele usa a bala e a vodka ali; a paranoia começa e ele continua escondido na
    // mesma borda, sem o olhar pro teto. Com a agenda livre, por três minutos, ele só espia, com as caras da fase da
    // paranoia enquanto ela dura.
    [Teste]
    public static void Escondido_ContinuaEscondido()
    {
        var sim = new SimuladorDeTempo(ComFisica() with { ChanceDaParanoia = Sempre }, 4, TopologiasDeExemplo.UmMonitor);
        Posicionamento l = sim.Estado.Lugar!;
        var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 20);
        foreach (Evento e in new Evento[] { new Press(corpo), new Click(), new Press(corpo), new DoubleClick() }) sim.Aplicar(e);
        sim.Esta(Estado.Peeking, "escondido");
        LadoDoEsconderijo lado = sim.Estado.Esconderijo;
        PontoPx escondido = sim.Estado.Lugar!.Ancora;
        foreach (Item item in new[] { Item.Bala, Item.Vodka })
        {
            SoltarSobreEle(sim, InvocarEAssentar(sim, item).Id);
            sim.Esta(Estado.Using, $"usando {item} escondido");
            sim.Passos(sim.Estado.PassosRestantes);
            sim.Esta(Estado.Peeking, $"de volta ao esconderijo depois de {item}");
        }
        Afirmar.Igual(Onda.Paranoico, sim.Estado.Onda?.Tipo, "a paranoia começou");
        Afirmar.Igual((lado, escondido, Gesto.Nenhum), (sim.Estado.Esconderijo, sim.Estado.Lugar!.Ancora, sim.Estado.Gesto), "na mesma borda, no mesmo lugar, sem gesto");
        int espiadas = 0;
        sim.AoResultado = (_, e, r) =>
        {
            Afirmar.Igual(Estado.Peeking, r.Estado.Estado, $"{e} o tirou do esconderijo");
            if (e is AutonomyTimer && r.Estado.Onda is { Tipo: Onda.Paranoico, Fase: FaseDaOnda.Pico })
            {
                espiadas++;
                Afirmar.Verdadeiro(r.Estado.Expressao is Expressao.Paranoico or Expressao.Assustado or Expressao.Surpreso, $"espiando com a cara {r.Estado.Expressao}, que não é do pico");
            }
        };
        sim.Avancar(TimeSpan.FromMinutes(3));
        Afirmar.Verdadeiro(espiadas > 0, "espiou no pico da paranoia");
    }

    // ---------------------------------------------------------------- o alívio e o fim

    // O alívio acalma a paranoia um passo, como as outras ondas de substância: com ela no pico do nível 2 (a vodka a subiu),
    // a banana a baixa ao nível 1 sem mexer no temporizador; a água leva o pico do nível 1 à queda (15 s × 125%, pelo pior
    // 2); o café acaba a queda, e a frente anterior (o bêbado, somado pela vodka) volta do fundo, com a fase recomeçada. A
    // carga não muda: a comida e a bebida sem álcool não entram nela, e o bêbado continua de substância.
    [Teste]
    public static void Alivio_AcalmaUmPasso()
    {
        Cenario c = Pausado(Sempre);
        AteAParanoia(c);
        Usar(c, Item.Vodka);
        Disparar(c);
        CargaDaParanoia carga = CargaSorteada(3, Item.Bala, Item.Vodka);
        Afirmar.Igual((new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 2, 2), carga), (c.Atual.Onda, c.Atual.Carga), "o pico do nível 2");
        long geracao = c.Atual.GeracaoDaOnda;

        Usado banana = Usar(c, Item.Banana);
        Afirmar.Igual((new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Pico, 1, 2), carga), (banana.Depois.Onda, banana.Depois.Carga), "a banana: nível 1");
        Afirmar.Igual(geracao, banana.Depois.GeracaoDaOnda, "sem mexer no temporizador");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Comer Banana; alivia Paranoico/Pico/2 -> Paranoico/Pico/1", banana.Regra, "a regra");

        Usado agua = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Queda, 1, 2), agua.Depois.Onda, "a água: a queda");
        Afirmar.Sequencia([TimeSpan.FromSeconds(18.75)], Agendados(agua.Soltar), "15 s × 125%");
        Afirmar.Igual(Expressao.Sonolento, c.Atual.Expressao, "no fim do uso, a cara da queda");

        Usado cafe = Usar(c, Item.Cafe);
        var bebado = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 3, 3);
        Afirmar.Igual((bebado, (EstadoDaOnda?)null, carga), (cafe.Depois.Onda, cafe.Depois.OndaDeFundo, cafe.Depois.Carga), "o café acaba a paranoia; o bêbado volta, e a carga fica");
        Afirmar.Sequencia([TimeSpan.FromSeconds(8)], Agendados(cafe.Soltar), "a subida do bêbado de novo");
        Afirmar.Igual("ITEM_DRAG_END sobre o personagem: Beber Cafe; alivia Paranoico/Queda/1 -> fim; a de fundo volta: Bebado/Subida/3", cafe.Regra, "a regra");
        foreach (Usado u in new[] { banana, agua, cafe })
            Afirmar.Igual(u.AntesDoSoltar.AleatorioDaParanoia, u.Depois.AleatorioDaParanoia, $"{u.Depois.Uso!.Item}: o alívio não sorteia a paranoia");
    }

    // Quando a paranoia acaba pelo temporizador, a frente anterior volta do fundo, na fase em que estava, com a duração
    // cheia e a cara dela; a regra do disparo diz isso.
    [Teste]
    public static void FimDaParanoia_AFrenteAnteriorVoltaDoFundo()
    {
        Cenario c = Pausado(Sempre);
        AteAParanoia(c);
        Disparar(c);
        Disparar(c);
        Afirmar.Igual((new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Queda, 1, 1), BebadoNaSubida2), (c.Atual.Onda, c.Atual.OndaDeFundo), "a queda, com o bêbado no fundo");
        Resultado fim = Disparar(c);
        Afirmar.Igual((BebadoNaSubida2, (EstadoDaOnda?)null), (fim.Estado.Onda, fim.Estado.OndaDeFundo), "o bêbado volta à frente");
        Afirmar.Igual("ITEM_EFFECT_TIMER: onda Paranoico/Queda/1 -> fim; a de fundo volta: Bebado/Subida/2", fim.Transicoes.Single().Regra, "a regra");
        Afirmar.Sequencia([TimeSpan.FromSeconds(8)], Agendados(fim), "com a subida inteira");
        Afirmar.Igual(Expressao.Feliz, fim.Estado.Expressao, "e a cara da subida do bêbado");
    }

    // ---------------------------------------------------------------- o retrato e a chave

    // O retrato leva a carga (quantas substâncias), e a linha canônica só a mostra acima de 0, logo depois da onda de fundo:
    // sem ela, a linha é a de antes. Com o tamagotchi desligado, os itens nem chegam, e a carga fica a de partida.
    [Teste]
    public static void Retrato_CargaSoApareceAcimaDeZero()
    {
        Cenario c = Pausado(Sempre);
        string semCarga = c.Retrato.Descrever();
        Afirmar.Falso(semCarga.Contains("carga=", StringComparison.Ordinal), semCarga);
        Usar(c, Item.Vodka);
        Afirmar.Igual(1, c.Retrato.Carga, "o retrato leva a carga");
        Afirmar.Verdadeiro(c.Retrato.Descrever().EndsWith(" sinal=Nenhum onda=Bebado/Subida/2 carga=1", StringComparison.Ordinal), c.Retrato.Descrever());
        EstadoDoNucleo comFundo = c.Atual with { OndaDeFundo = new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Pico, 1, 1) };
        Afirmar.Contem(" onda=Bebado/Subida/2 fundo=Tonto/Pico/1 carga=1", comFundo.Retrato().Descrever(), "logo depois do fundo");
        Afirmar.Igual(semCarga, (c.Atual with { Onda = null, Carga = CargaDaParanoia.Nenhuma, Expressao = Expressao.Neutro }).Retrato().Descrever(), "sem a onda e sem a carga, a linha de antes");

        Cenario desligado = Cenario.Parado(new ConfiguracaoDoNucleo()).Aplicar(new CmdSummonItem(Item.Vodka));
        Afirmar.Igual((0, CargaDaParanoia.Nenhuma), (desligado.Atual.Itens.Quantidade, desligado.Atual.Carga), "com a chave desligada, nem item nem carga");
    }
}
