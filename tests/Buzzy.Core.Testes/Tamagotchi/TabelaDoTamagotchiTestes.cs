using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// Tipos e tabelas do tamagotchi adulto (DEC-028, passo T3), ainda sem comportamento: os itens na ordem do menu, o verbo
/// e a duração do uso de cada um, as ondas com fases, perfis, gestos e caras (desenho do núcleo, tabelas 4.1 a 4.4, com a
/// paranoia no fim), as oito caras de efeito e os oito gestos novos no fim dos enums, e a chave Tamagotchi desligada. O
/// esperado vem de <see cref="TabelasDoDesenho"/>, transcrito do desenho à parte do código.
/// </summary>
internal static class TabelaDoTamagotchiTestes
{
    // Os 13 itens, na ordem da resposta do usuário e do menu, e a chave da arte de cada um: o nome em minúsculas.
    [Teste]
    public static void Itens_NaOrdemDoMenuComAsChavesDaArte()
    {
        string[] chaves = ["banana", "agua", "vodka", "cerveja", "baseado", "cigarro", "cocaina", "md", "lancaperfume", "cafe", "energetico", "cogumelo", "bala"];
        Afirmar.Sequencia(chaves, TabelaDoTamagotchi.Itens.Select(i => i.ToString().ToLowerInvariant()), "os itens do menu e as chaves da arte");
        Afirmar.Sequencia(Enum.GetValues<Item>(), TabelaDoTamagotchi.Itens, "a lista do menu cobre o enum inteiro, na ordem dele");
        Afirmar.Sequencia(TabelasDoDesenho.ItensEsperados().Select(i => i.Item), TabelaDoTamagotchi.Itens, "a ordem da tabela 4.1");
        foreach (int fora in new[] { -1, 13, 99 })
            Afirmar.Lanca<ArgumentOutOfRangeException>(() => TabelaDoTamagotchi.DoItem((Item)fora), $"item {fora}, fora do enum");
    }

    // Tabela 4.1 com os verbos e as durações do coordenador: o uso dura os passos do verbo (a sequência de quadros da
    // arte soma o mesmo), a cara durante o uso é de humor, só a água não começa onda (soma 0; os outros, 1 ou 2), e cada item
    // é de alívio ou de substância, e droga sintética ou não, como na transcrição.
    [Teste]
    public static void TodoItemTemOVerboOsPassosEACaraDaTabela()
    {
        foreach (TabelasDoDesenho.ItemEsperado esperado in TabelasDoDesenho.ItensEsperados())
        {
            DadosDoItem d = TabelaDoTamagotchi.DoItem(esperado.Item);
            string onde = esperado.Item.ToString();
            Afirmar.Igual(esperado.Item, d.Item, onde);
            Afirmar.Igual(esperado.Verbo, d.Verbo, $"{onde}: verbo");
            Afirmar.Igual(TabelasDoDesenho.PassosPorVerbo[esperado.Verbo], d.PassosDoUso, $"{onde}: o uso dura os passos do verbo");
            Afirmar.Igual(esperado.CaraDurante, d.CaraDurante, $"{onde}: cara durante o uso");
            Afirmar.Verdadeiro(Expressoes.EhDeHumor(d.CaraDurante), $"{onde}: a cara durante o uso é de humor");
            Afirmar.Igual(esperado.Onda, d.Onda, $"{onde}: onda");
            Afirmar.Igual(esperado.Intensidade, d.Intensidade, $"{onde}: intensidade");
            Afirmar.Igual(d.Onda is null, d.Intensidade == 0, $"{onde}: sem onda, intensidade 0; com onda, 1 ou 2");
            Afirmar.Verdadeiro(d.Intensidade is >= 0 and <= 2, $"{onde}: intensidade de 0 a 2");
            Afirmar.Igual(esperado.Alivio, d.Alivio, $"{onde}: de alívio (ou de substância)");
            Afirmar.Igual(esperado.Sintetica, d.Sintetica, $"{onde}: droga sintética (ou não)");
        }
        Afirmar.Sequencia([Item.Agua], TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.DoItem(i).Onda is null), "só a água não começa onda");

        foreach (VerboDeUso verbo in Enum.GetValues<VerboDeUso>())
        {
            Afirmar.Igual(TabelasDoDesenho.PassosPorVerbo[verbo], TabelaDoTamagotchi.PassosDoUso(verbo), $"passos de {verbo}");
            Afirmar.Verdadeiro(TabelaDoTamagotchi.PassosDoUso(verbo) is >= 60 and <= 240, $"{verbo}: de 1 a 4 segundos");
        }
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => TabelaDoTamagotchi.PassosDoUso((VerboDeUso)6), "verbo fora do enum");
    }

    // Tabelas 4.2 a 4.4: para cada onda, a precedência, os tempos, as caras de cada fase, a classe (de substância ou leve)
    // e os perfis de cada nível do pico e da queda, campo a campo, com os gestos e as caras sorteados em cada fase.
    [Teste]
    public static void TodaOndaSegueAsTabelasDoDesenho()
    {
        Afirmar.Sequencia(Enum.GetValues<Onda>(), TabelasDoDesenho.OndasEsperadas().Select(o => o.Onda), "uma linha por onda, na ordem do enum");
        foreach (TabelasDoDesenho.OndaEsperada esperada in TabelasDoDesenho.OndasEsperadas())
        {
            DadosDaOnda d = TabelaDoTamagotchi.DaOnda(esperada.Onda);
            string onde = esperada.Onda.ToString();
            Afirmar.Igual(esperada.Onda, d.Onda, onde);
            Afirmar.Igual(esperada.Precedencia, d.Precedencia, $"{onde}: precedência");
            Afirmar.Igual(esperada.Subida, d.Subida, $"{onde}: subida");
            Afirmar.Igual(esperada.NivelDoPico, d.NivelDoPico, $"{onde}: cada nível do pico");
            Afirmar.Igual(esperada.QuedaBase ?? TimeSpan.Zero, d.QuedaBase, $"{onde}: queda base");
            Afirmar.Igual(esperada.CaraDaSubida, d.CaraDaSubida, $"{onde}: cara na subida");
            Afirmar.Igual(esperada.CaraDoPico, d.CaraDoPico, $"{onde}: cara no pico");
            Afirmar.Igual(esperada.CaraDaQueda, d.CaraDaQueda, $"{onde}: cara na queda");
            Afirmar.Igual(esperada.DeSubstancia, d.DeSubstancia, $"{onde}: de substância (ou leve)");
            Afirmar.Igual(3, d.PicoPorNivel.Count, $"{onde}: três níveis no pico");
            for (int nivel = 1; nivel <= 3; nivel++)
            {
                Afirmar.Igual(esperada.PicoPorNivel[nivel - 1], TabelasDoDesenho.Descrever(d.PicoPorNivel[nivel - 1]), $"{onde}: perfil do pico no nível {nivel}");
                Afirmar.Igual(esperada.PicoPorNivel[nivel - 1], TabelasDoDesenho.Descrever(d.Perfil(FaseDaOnda.Pico, nivel)), $"{onde}: Perfil(Pico, {nivel})");
                Afirmar.Igual(esperada.Subir, TabelasDoDesenho.Descrever(d.Perfil(FaseDaOnda.Subida, nivel)), $"{onde}: a subida usa o nível 1, com a cara da subida");
            }
            Afirmar.Igual(esperada.Queda, d.Queda is null ? null : TabelasDoDesenho.Descrever(d.Queda), $"{onde}: perfil da queda");
            if (d.Queda is not null)
            {
                foreach (int nivel in new[] { 1, 2, 3 })
                    Afirmar.Igual(esperada.Queda, TabelasDoDesenho.Descrever(d.Perfil(FaseDaOnda.Queda, nivel)), $"{onde}: a queda não depende do nível");
            }
            else
            {
                Afirmar.Lanca<InvalidOperationException>(() => d.Perfil(FaseDaOnda.Queda, 1), $"{onde}: sem queda");
            }
        }
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => TabelaDoTamagotchi.DaOnda((Onda)11), "onda fora do enum");
    }

    // O alívio (pedido do usuário de 2026-10-01, decisão do coordenador para a DEC-028), com as listas da decisão: a comida
    // e a bebida sem álcool (banana, água, café e energético) são de alívio, e os outros nove itens, de substância, ficam
    // como estão. A bala saiu do alívio no mesmo dia: é droga sintética (palavras do usuário, "como bala, md, coca e lança").
    // As ondas do bêbado, do chapado, do elétrico, do eufórico, do tonto, do viajando e do relaxado são de substância, e as do
    // satisfeito, do alegre e do ligado, leves. A paranoia (outro pedido do mesmo dia) também é de substância: a comida e a
    // bebida sem álcool a acalmam um passo, como as outras. A classificação do núcleo e a da transcrição são exatamente
    // essas, e são coerentes: a onda própria de um item de alívio é leve (a água não tem onda), e a de um item de substância,
    // de substância.
    [Teste]
    public static void Alivio_AClassificacaoEhExatamenteADaDecisao()
    {
        Item[] deAlivio = [Item.Banana, Item.Agua, Item.Cafe, Item.Energetico];
        Item[] deSubstancia = [Item.Vodka, Item.Cerveja, Item.Cigarro, Item.Baseado, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala];
        Onda[] ondasDeSubstancia = [Onda.Bebado, Onda.Chapado, Onda.Eletrico, Onda.Euforico, Onda.Tonto, Onda.Viajando, Onda.Relaxado, Onda.Paranoico];
        Onda[] ondasLeves = [Onda.Satisfeito, Onda.Alegre, Onda.Ligado];
        Afirmar.Sequencia(Enum.GetValues<Item>(), deAlivio.Concat(deSubstancia).Order(), "a decisão classifica os 13 itens, cada um uma vez");
        Afirmar.Sequencia(Enum.GetValues<Onda>(), ondasDeSubstancia.Concat(ondasLeves).Order(), "e as 11 ondas, cada uma uma vez");

        Afirmar.Sequencia(deAlivio.Order(), TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.DoItem(i).Alivio).Order(), "os itens de alívio do núcleo");
        Afirmar.Sequencia(ondasDeSubstancia.Order(), Enum.GetValues<Onda>().Where(o => TabelaDoTamagotchi.DaOnda(o).DeSubstancia).Order(), "as ondas de substância do núcleo");
        Afirmar.Sequencia(deAlivio.Order(), TabelasDoDesenho.ItensEsperados().Where(i => i.Alivio).Select(i => i.Item).Order(), "os itens de alívio da transcrição");
        Afirmar.Sequencia(ondasDeSubstancia.Order(), TabelasDoDesenho.OndasEsperadas().Where(o => o.DeSubstancia).Select(o => o.Onda).Order(), "as ondas de substância da transcrição");

        foreach (Item item in TabelaDoTamagotchi.Itens)
        {
            DadosDoItem d = TabelaDoTamagotchi.DoItem(item);
            if (d.Onda is { } onda)
                Afirmar.Igual(!d.Alivio, TabelaDoTamagotchi.DaOnda(onda).DeSubstancia, $"{item}: a onda própria ({onda}) é leve no item de alívio e de substância no outro");
            else
                Afirmar.Igual((Item.Agua, true), (item, d.Alivio), "só a água não tem onda, e ela é de alívio");
        }
    }

    // As drogas sintéticas (pedido do usuário de 2026-10-01, 18:50: "só quero que ele fique paranoico se misturar substâncias
    // com alguma droga sintética, como bala, md, coca e lança; usando álcool e maconha não"; decisão do coordenador), com as
    // listas literais da decisão: a bala, o MD, a cocaína e o lança-perfume são sintéticas; a vodka, a cerveja, o baseado, o
    // cigarro e o cogumelo são as outras substâncias; a banana, a água, o café e o energético são de alívio. É regra de jogo,
    // de desenho animado, e dado da tabela, por item. A do núcleo e a da transcrição são essas; nenhuma sintética é de
    // alívio, e a onda de cada uma é de substância (a comida e a bebida sem álcool a acalmam).
    [Teste]
    public static void Sinteticas_AClassificacaoEhExatamenteADaDecisao()
    {
        Item[] sinteticas = [Item.Bala, Item.Md, Item.Cocaina, Item.LancaPerfume];
        Item[] outrasSubstancias = [Item.Vodka, Item.Cerveja, Item.Baseado, Item.Cigarro, Item.Cogumelo];
        Item[] deAlivio = [Item.Banana, Item.Agua, Item.Cafe, Item.Energetico];
        Afirmar.Sequencia(Enum.GetValues<Item>(), sinteticas.Concat(outrasSubstancias).Concat(deAlivio).Order(), "a decisão classifica os 13 itens, cada um uma vez");

        Afirmar.Sequencia(sinteticas.Order(), TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.DoItem(i).Sintetica).Order(), "as sintéticas do núcleo");
        Afirmar.Sequencia(outrasSubstancias.Order(), TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.DoItem(i) is { Alivio: false, Sintetica: false }).Order(), "as outras substâncias do núcleo");
        Afirmar.Sequencia(deAlivio.Order(), TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.DoItem(i).Alivio).Order(), "os itens de alívio do núcleo");
        Afirmar.Sequencia(sinteticas.Order(), TabelasDoDesenho.ItensEsperados().Where(i => i.Sintetica).Select(i => i.Item).Order(), "as sintéticas da transcrição");
        Afirmar.Sequencia(outrasSubstancias.Order(), TabelasDoDesenho.ItensEsperados().Where(i => !i.Alivio && !i.Sintetica).Select(i => i.Item).Order(), "as outras substâncias da transcrição");

        foreach (Item item in sinteticas)
        {
            DadosDoItem d = TabelaDoTamagotchi.DoItem(item);
            Afirmar.Falso(d.Alivio, $"{item}: droga sintética nunca é de alívio");
            Onda onda = Afirmar.NaoNulo(d.Onda, $"{item}: começa uma onda");
            Afirmar.Verdadeiro(TabelaDoTamagotchi.DaOnda(onda).DeSubstancia, $"{item}: a onda dela ({onda}) é de substância");
        }
    }

    // A bala é droga sintética (palavras do usuário de 2026-10-01): deixou de ser doce de alívio e começa o eufórico, com
    // intensidade 1 (o MD continua com 2), engolida, com a cara feliz durante o uso (a arte da bala não muda). A onda alegre,
    // que era dela, fica sem item, mas continua no enum, no mesmo lugar, e na tabela (sem mexer em ordinais).
    [Teste]
    public static void Bala_EhDrogaSinteticaComOEuforicoNoNivel1()
    {
        DadosDoItem bala = TabelaDoTamagotchi.DoItem(Item.Bala);
        Afirmar.Igual((VerboDeUso.Engolir, 90, Expressao.Feliz), (bala.Verbo, bala.PassosDoUso, bala.CaraDurante), "a bala é engolida, como antes, com a cara feliz");
        Afirmar.Igual(((Onda?)Onda.Euforico, 1, false, true), (bala.Onda, bala.Intensidade, bala.Alivio, bala.Sintetica), "a bala: o eufórico no nível 1, droga sintética, fora do alívio");
        DadosDoItem md = TabelaDoTamagotchi.DoItem(Item.Md);
        Afirmar.Igual(((Onda?)Onda.Euforico, 2, false, true), (md.Onda, md.Intensidade, md.Alivio, md.Sintetica), "o MD continua com o eufórico no nível 2");
        Afirmar.Falso(TabelaDoTamagotchi.Itens.Any(i => TabelaDoTamagotchi.DoItem(i).Onda == Onda.Alegre), "nenhum item começa o alegre");
        Afirmar.Igual(1, (int)Onda.Alegre, "o alegre continua no enum, no mesmo lugar");
        Afirmar.Igual(Onda.Alegre, TabelaDoTamagotchi.DaOnda(Onda.Alegre).Onda, "e na tabela");
    }

    // A cara de cada fase (a queda sem cara própria fica com a do pico) e as durações: a subida e cada nível do pico são
    // fixos; a queda é a base × 100, 125 ou 150% pelo pior nível atingido; nunca menos de 1 s (um disparo único).
    [Teste]
    public static void CarasEDuracoesDeCadaFase()
    {
        foreach (TabelasDoDesenho.OndaEsperada e in TabelasDoDesenho.OndasEsperadas())
        {
            DadosDaOnda d = TabelaDoTamagotchi.DaOnda(e.Onda);
            string onde = e.Onda.ToString();
            Afirmar.Igual(e.CaraDaSubida, d.Cara(FaseDaOnda.Subida), $"{onde}: cara da subida");
            Afirmar.Igual(e.CaraDoPico, d.Cara(FaseDaOnda.Pico), $"{onde}: cara do pico");
            for (int pior = 1; pior <= 3; pior++)
            {
                Afirmar.Igual(e.Subida, d.Duracao(FaseDaOnda.Subida, pior), $"{onde}: subida com o pior nível {pior}");
                Afirmar.Igual(e.NivelDoPico, d.Duracao(FaseDaOnda.Pico, pior), $"{onde}: nível do pico com o pior nível {pior}");
                if (e.QuedaBase is not null)
                    Afirmar.Igual(e.Atrasos(pior)[^1], d.Duracao(FaseDaOnda.Queda, pior), $"{onde}: queda com o pior nível {pior}");
            }
            if (e.QuedaBase is { } baseDaQueda)
            {
                Afirmar.Igual(e.CaraDaQueda ?? e.CaraDoPico, d.Cara(FaseDaOnda.Queda), $"{onde}: cara da queda");
                Afirmar.Igual(baseDaQueda * 1.5, d.Duracao(FaseDaOnda.Queda, 3), $"{onde}: queda no pior nível 3 = 150%");
            }
            else
            {
                Afirmar.Lanca<InvalidOperationException>(() => d.Duracao(FaseDaOnda.Queda, 1), $"{onde}: sem queda");
            }
            foreach (int fora in new[] { 0, 4 })
            {
                Afirmar.Lanca<ArgumentOutOfRangeException>(() => d.Perfil(FaseDaOnda.Pico, fora), $"{onde}: nível {fora}");
                Afirmar.Lanca<ArgumentOutOfRangeException>(() => d.Duracao(FaseDaOnda.Pico, fora), $"{onde}: pior nível {fora}");
            }
        }
        DadosDaOnda curta = TabelaDoTamagotchi.DaOnda(Onda.Tonto) with { Subida = TimeSpan.FromMilliseconds(200), QuedaBase = TimeSpan.FromMilliseconds(400) };
        Afirmar.Igual(TimeSpan.FromSeconds(1), curta.Duracao(FaseDaOnda.Subida, 1), "nunca menos de 1 s");
        Afirmar.Igual(TimeSpan.FromSeconds(1), curta.Duracao(FaseDaOnda.Queda, 2), "nem na queda");
    }

    // Conjuntos válidos: precedência de 1 a 3 nas ondas dos itens e 4 só na paranoia, velocidade de 50 a 200% (D10),
    // cambaleio e percentuais não negativos, gestos e caras com peso positivo; os gestos novos e as caras de efeito só
    // aparecem nas tabelas das ondas, e cada um deles aparece em alguma.
    [Teste]
    public static void TodaOndaTemConjuntosValidos()
    {
        var gestosVistos = new HashSet<Gesto>();
        var carasVistas = new HashSet<Expressao>();
        foreach (Onda onda in Enum.GetValues<Onda>())
        {
            DadosDaOnda d = TabelaDoTamagotchi.DaOnda(onda);
            Afirmar.Verdadeiro(onda == Onda.Paranoico ? d.Precedencia == 4 : d.Precedencia is >= 1 and <= 3, $"{onda}: precedência de 1 a 3 (a paranoia, 4)");
            Afirmar.Verdadeiro(d.Subida >= TimeSpan.FromSeconds(1) && d.NivelDoPico >= TimeSpan.FromSeconds(1), $"{onda}: fases de 1 s ou mais");
            var perfis = new List<PerfilDaOnda>(d.PicoPorNivel) { d.Perfil(FaseDaOnda.Subida, 1) };
            if (d.Queda is { } queda) perfis.Add(queda);
            foreach (PerfilDaOnda p in perfis)
            {
                Afirmar.Verdadeiro(p.Velocidade is >= 50 and <= 200, $"{onda}: velocidade {p.Velocidade} entre 50 e 200%");
                Afirmar.Verdadeiro(new[] { p.Intervalo, p.Descanso, p.Andar, p.Escalar, p.Pular, p.Descansar, p.Gesticular, p.TrocarCara, p.Cambaleio, p.AlturaDoPulo }.All(v => v >= 0), $"{onda}: percentuais não negativos");
                Afirmar.Verdadeiro(p.Intervalo > 0 && p.Descanso > 0 && p.AlturaDoPulo > 0, $"{onda}: intervalos e pulo positivos");
                Afirmar.Verdadeiro(p.ChanceDoFoguete is null or (>= 0 and <= 100), $"{onda}: foguete de 0 a 100");
                Afirmar.Verdadeiro(p.Gestos.Count > 0 && p.Gestos.All(g => g.Peso > 0 && g.Gesto != Gesto.Nenhum && Enum.IsDefined(g.Gesto)), $"{onda}: gestos válidos");
                Afirmar.Verdadeiro(p.Caras.Count > 0 && p.Caras.All(c => c.Peso > 0 && Enum.IsDefined(c.Cara)), $"{onda}: caras válidas");
                gestosVistos.UnionWith(p.Gestos.Select(g => g.Gesto));
                carasVistas.UnionWith(p.Caras.Select(c => c.Cara));
            }
            carasVistas.Add(d.CaraDoPico);
            if (d.CaraDaQueda is { } caraDaQueda) carasVistas.Add(caraDaQueda);
            Afirmar.Verdadeiro(Expressoes.EhDeHumor(d.CaraDaSubida), $"{onda}: a subida mostra uma cara de humor");
        }
        Afirmar.Verdadeiro(new[] { Gesto.Soluco, Gesto.Danca, Gesto.Gargalhada, Gesto.Espirro, Gesto.Tosse, Gesto.Tremedeira, Gesto.OlharProTeto, Gesto.Agachar }.All(gestosVistos.Contains), "cada gesto novo aparece em alguma onda");
        Afirmar.Verdadeiro(Expressoes.DeEfeito.All(carasVistas.Contains), "cada cara de efeito aparece em alguma onda");
        Afirmar.Verdadeiro(TabelaDoTamagotchi.Itens.All(i => Expressoes.EhDeHumor(TabelaDoTamagotchi.DoItem(i).CaraDurante)), "nenhum item mostra cara de efeito durante o uso");
    }

    // O maior episódio de uma onda sozinha (pior nível 3: subida, três níveis do pico e a queda a 150%) cabe em 10 minutos
    // (o do viajando é o maior: 20 + 420 + 90 = 530 s), e cada item usado sozinho dura o da coluna "duração sozinho" de 4.1.
    [Teste]
    public static void EpisodioMaisLongoCabeEmDezMinutos()
    {
        TimeSpan maior = TimeSpan.Zero;
        foreach (Onda onda in Enum.GetValues<Onda>())
        {
            DadosDaOnda d = TabelaDoTamagotchi.DaOnda(onda);
            TimeSpan episodio = d.Duracao(FaseDaOnda.Subida, 3) + 3 * d.Duracao(FaseDaOnda.Pico, 3) + (d.Queda is null ? TimeSpan.Zero : d.Duracao(FaseDaOnda.Queda, 3));
            Afirmar.Verdadeiro(episodio <= TimeSpan.FromMinutes(10), $"{onda}: {episodio} no pior caso");
            if (episodio > maior) maior = episodio;
        }
        Afirmar.Igual(TimeSpan.FromSeconds(530), maior, "o maior episódio");
        Afirmar.Igual(TimeSpan.FromSeconds(530), TabelasDoDesenho.Esperada(Onda.Viajando).Atrasos(3).Aggregate(TimeSpan.Zero, (a, b) => a + b), "é o do viajando no nível 3");

        foreach (TabelasDoDesenho.ItemEsperado item in TabelasDoDesenho.ItensEsperados())
        {
            DadosDoItem d = TabelaDoTamagotchi.DoItem(item.Item);
            if (d.Onda is not { } onda)
            {
                Afirmar.Nulo(item.DuracaoSozinho, $"{item.Item}: sem onda");
                continue;
            }
            DadosDaOnda o = TabelaDoTamagotchi.DaOnda(onda);
            int n = d.Intensidade;
            TimeSpan sozinho = o.Duracao(FaseDaOnda.Subida, n) + n * o.Duracao(FaseDaOnda.Pico, n) + (o.Queda is null ? TimeSpan.Zero : o.Duracao(FaseDaOnda.Queda, n));
            Afirmar.Igual(item.DuracaoSozinho, sozinho, $"{item.Item}: duração sozinho");
        }
    }

    // As oito caras de efeito e os oito gestos novos ficam no fim dos enums, na ordem do coordenador (a cara e os dois
    // gestos da paranoia por último), com as chaves da arte em minúsculas; as 14 caras de humor e os gestos de sempre não
    // mudam de valor, e o sorteio automático de gesto (de Espiar a Brincar) não alcança os novos.
    [Teste]
    public static void CarasDeEfeitoEGestosNovos_NoFimDosEnums()
    {
        Expressao[] deEfeito = [Expressao.Bebado, Expressao.Enjoado, Expressao.Chapado, Expressao.Eletrico, Expressao.Apaixonado, Expressao.Tonto, Expressao.Viajando, Expressao.Paranoico];
        Afirmar.Sequencia(deEfeito, Expressoes.DeEfeito, "as caras de efeito");
        Afirmar.Sequencia(Enumerable.Range(14, 8), deEfeito.Select(e => (int)e), "no fim do enum, depois das 14 de humor");
        Afirmar.Sequencia(["bebado", "enjoado", "chapado", "eletrico", "apaixonado", "tonto", "viajando", "paranoico"], deEfeito.Select(e => e.ToString().ToLowerInvariant()), "chaves da arte");
        Afirmar.Sequencia(Enum.GetValues<Expressao>(), Expressoes.DeHumor.Concat(Expressoes.DeEfeito), "de humor e de efeito cobrem o enum inteiro");
        Afirmar.Verdadeiro(deEfeito.All(e => !Expressoes.EhDeHumor(e)), "nenhuma de efeito é de humor (nem pode ser a emoção dominante)");
        Afirmar.Igual(13, (int)Expressao.Determinado, "as 14 de humor não mudam de valor");
        Afirmar.Igual(21, (int)Expressao.Paranoico, "a da paranoia depois de Viajando");

        Gesto[] novos = [Gesto.Soluco, Gesto.Danca, Gesto.Gargalhada, Gesto.Espirro, Gesto.Tosse, Gesto.Tremedeira, Gesto.OlharProTeto, Gesto.Agachar];
        Afirmar.Sequencia(Enumerable.Range(6, 8), novos.Select(g => (int)g), "gestos novos no fim do enum");
        Afirmar.Sequencia(["soluco", "danca", "gargalhada", "espirro", "tosse", "tremedeira", "olharproteto", "agachar"], novos.Select(g => g.ToString().ToLowerInvariant()), "poses dos gestos novos");
        Afirmar.Sequencia([0, 1, 2, 3, 4, 5], new[] { Gesto.Nenhum, Gesto.Espiar, Gesto.OlharAoRedor, Gesto.Cocar, Gesto.Espreguicar, Gesto.Brincar }.Select(g => (int)g), "os gestos de sempre não mudam de valor");
        Afirmar.Igual(14, Enum.GetValues<Gesto>().Length, "catorze gestos com o nenhum");
        Afirmar.Igual(10, (int)Onda.Paranoico, "a paranoia no fim do enum Onda");
    }

    // A paranoia (pedido do usuário de 2026-10-01; decisão do coordenador para a DEC-028) na tabela: nenhum item a começa
    // (ela vem da carga), a precedência 4 é maior que a de toda onda de item, é de substância (o alívio a acalma) e tem
    // queda; no pico, igual nos três níveis, ele nunca escala, pula, descansa nem dispara o foguete, e os gestos mais
    // pesados são os da paranoia (olhar pro teto e agachar).
    [Teste]
    public static void Paranoia_NenhumItemAComecaEAPrecedenciaEhAMaior()
    {
        DadosDaOnda d = TabelaDoTamagotchi.DaOnda(Onda.Paranoico);
        Afirmar.Falso(TabelaDoTamagotchi.Itens.Any(i => TabelaDoTamagotchi.DoItem(i).Onda == Onda.Paranoico), "nenhum item começa a paranoia");
        Afirmar.Verdadeiro(Enum.GetValues<Onda>().Where(o => o != Onda.Paranoico).All(o => TabelaDoTamagotchi.DaOnda(o).Precedencia < d.Precedencia), "a precedência da paranoia é maior que a de todas");
        Afirmar.Verdadeiro(d.DeSubstancia && d.Queda is not null, "de substância e com queda");
        foreach (int nivel in new[] { 1, 2, 3 })
        {
            PerfilDaOnda p = d.Perfil(FaseDaOnda.Pico, nivel);
            Afirmar.Igual((0, 0, 0, (int?)0), (p.Escalar, p.Pular, p.Descansar, p.ChanceDoFoguete), $"pico {nivel}: sem escalar, pular, descansar nem foguete");
            Afirmar.Igual((Gesto.OlharProTeto, Gesto.Agachar), (p.Gestos[0].Gesto, p.Gestos[1].Gesto), $"pico {nivel}: os gestos mais pesados são os da paranoia");
            Afirmar.Igual(Expressao.Paranoico, p.Caras.MaxBy(c => c.Peso).Cara, $"pico {nivel}: a cara mais sorteada é a paranoica");
        }
    }

    // A chave do tamagotchi fica desligada por padrão e ligada no aplicativo desde a entrega do app e da arte (D15, passo
    // T9); os pedidos já entregues continuam (o esconderijo no clique duplo, DEC-025). O item tem 48×48 DIP (24×24 px de
    // arte), no máximo 6 ficam na tela, e o alvo do soltar é o sprite encolhido 20% de cada lado; as tabelas são as do
    // núcleo, trocáveis nos testes, e a chance da paranoia é 1 em 8. A queda do item usa a gravidade do personagem, com os
    // parâmetros próprios do desenho.
    [Teste]
    public static void Configuracao_ChaveLigadaNoAplicativoEParametrosDoItem()
    {
        var padrao = new ConfiguracaoDoNucleo();
        Afirmar.Falso(padrao.Tamagotchi, "desligada por padrão");
        ConfiguracaoDoNucleo app = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));
        Afirmar.Verdadeiro(app.Tamagotchi, "ligada no aplicativo desde o passo T9: o app e a arte estão prontos");
        Afirmar.Verdadeiro(app.EsconderijoNoCliqueDuplo && app.Movimento && app.QuedaFisica, "o aplicativo continua com o esconderijo, a física e a queda");
        Afirmar.Igual(new TamanhoDip(48, 48), padrao.TamanhoDoItem, "tamanho do item");
        Afirmar.Igual(6, padrao.MaximoDeItens, "máximo de itens");
        Afirmar.Igual(20, padrao.MargemDoAlvo, "margem do alvo, em % de cada lado");
        foreach (Item item in TabelaDoTamagotchi.Itens) Afirmar.Igual(TabelaDoTamagotchi.DoItem(item), padrao.TabelaDeItens(item), $"tabela de itens: {item}");
        foreach (Onda onda in Enum.GetValues<Onda>()) Afirmar.Igual(TabelaDoTamagotchi.DaOnda(onda), padrao.TabelaDeOndas(onda), $"tabela de ondas: {onda}");
        // A chance da paranoia (pedido do usuário de 2026-10-01, 19:00: "quero que a chance dele ficar paranoico seja de 1 em
        // 8"), a mesma no aplicativo; os testes podem trocá-la.
        Afirmar.Igual((1, 8), (padrao.ChanceDaParanoia.Vezes, padrao.ChanceDaParanoia.Em), "a chance da paranoia: 1 em 8");
        Afirmar.Igual(padrao.ChanceDaParanoia, app.ChanceDaParanoia, "a mesma no aplicativo");
        Afirmar.Igual("1 em 8", padrao.ChanceDaParanoia.ToString(), "escrita como na decisão");

        ParametrosDeMovimento f = padrao.Fisica;
        Afirmar.Igual(140.0, f.AlturaDaQuedaDoItem, "o item nasce 140 DIP acima e cai");
        Afirmar.Igual(8.0, f.FolgaDoItem, "folga entre o item e o personagem");
        Afirmar.Igual(0.35, f.RestituicaoDoItem, "quique do item");
        Afirmar.Igual(300.0, f.ImpactoMinimoDoItem, "impacto mínimo para quicar");
        Afirmar.Igual(1, f.QuiquesDoItem, "um quique só");
    }
}
