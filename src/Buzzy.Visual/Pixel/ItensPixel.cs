namespace Buzzy.Visual.Pixel;

// Cada verbo tem as suas poses de uso.
public enum Verbo
{
    Comer,
    Beber,
    Fumar,
    Cheirar,
    Engolir,
    Inalar,
}

// Em pixels do carimbo. Pega = centro da mão (a palma vai por cima); pode ficar fora do desenho
// quando o item vai na ponta dos dedos ou em cima da palma. Ponta = pixel que encosta no rosto
// (boca da garrafa, filtro, mordida...); nula se a variante não vai ao rosto.
public sealed record ItemNaMao(Carimbo Desenho, (int X, int Y) Pega, (int X, int Y)? Ponta)
{
    // 90° anti-horário: a linha de cima (boca da garrafa, filtro) vai pra coluna da esquerda, rumo à boca.
    public ItemNaMao Girado()
    {
        int largura = Desenho.Largura;
        return new(Desenho.Girado(horario: false), (Pega.Y, largura - 1 - Pega.X), Ponta is { } p ? (p.Y, largura - 1 - p.X) : null);
    }
}

// Itens do tamagotchi na mesma densidade do boneco (1 px = 2 DIP): o desenho do chão em 24x24
// (também é o ícone do menu) e os carimbos na mão. Genéricos, de desenho animado: sem texto,
// marca nem folha de maconha. Chave = nome do enum Item do núcleo em minúsculas, na ordem do menu.
public static class ItensPixel
{
    // Em pixels de arte.
    public const int Lado = 24;

    // 24 px de arte x 2 DIP.
    public const double TamanhoLogicoDip = 48;

    private sealed record Definicao(Verbo Verbo, Carimbo Chao, (string Nome, ItemNaMao Mao)[] NaMao);

    // Só preenchimento: o contorno vem do Contornar (no chão) e da linha interna do boneco (na mão).
    // Luz de cima e da esquerda. Retocados olhando as prévias a 8x, 2x e 1x. Itens do mesmo verbo
    // têm as mesmas variantes na mão, na ordem da animação.
    private static readonly (string Chave, Definicao Definicao)[] Tabela =
    [
        // Banana deitada, curva para cima, com o cabinho à direita.
        ("banana", new(Verbo.Comer,
            new Carimbo(
                "................kk",
                "...............nyk",
                "k..............nyn",
                "kY............nYyn",
                ".yYY.........nYyyn",
                ".nyYYY......nYyyyn",
                "..nyyYYYYYYYYyyyn.",
                "...nnyyyyyyyyyynn.",
                ".....nnnnnnnnnn..."),
            Maos.Banana)),
        // Garrafinha de plástico, gordinha, de tampa azul, com o rótulo branco e uma gota.
        ("agua", new(Verbo.Beber,
            new Carimbo(
                "..ddddd..",
                "..dAddd..",
                "..ddddd..",
                "...ggg...",
                "..gAaag..",
                ".gAaaaaG.",
                "gWaaaaaaG",
                "wwwwwwwwx",
                "wwwwdwwwx",
                "wwwdddwwx",
                "wwwdddwwx",
                "gWaaaaaaG",
                "gAaaaaaaG",
                "gaaaaaadG",
                ".GGGGGGG."),
            Maos.Agua)),
        // Garrafa alta de vidro, tampa de metal e rótulo vermelho liso (sem letras).
        ("vodka", new(Verbo.Beber,
            new Carimbo(
                "...mm...",
                "...MmE..",
                "...EE...",
                "...gG...",
                "...gG...",
                "...gG...",
                "..gWgG..",
                ".gWggGG.",
                "gWggggGG",
                "gWgggggG",
                "ffffffFF",
                "fWffffFF",
                "fWffffFF",
                "ffffffFF",
                "gWgggggG",
                "gWgggggG",
                "gggggggG",
                "gggggggG",
                ".GGGGGG."),
            Maos.Vodka)),
        // Caneca de chope com espuma transbordando e alça em D.
        ("cerveja", new(Verbo.Beber,
            new Carimbo(
                "...WW.WWW.......",
                ".WWWWWWWWWW.....",
                "WWWWWWWWWWWx....",
                "xWWWWWWWWxxx....",
                "gBWBBBBBBBDG....",
                "gBYBBBBBBBDGGGG.",
                "gBYBBBBBBBDG..GG",
                "gBYBBBBBBBDG...G",
                "gBYBBBBBBBDG...G",
                "gBYBBBBBBBDG...G",
                "gBYBBBBBBBDG..GG",
                "gBBBBBBBBBDGGGG.",
                "gBBBBBBBBBDG....",
                "gDDDDDDDDDDG....",
                "ggggggggggGG....",
                ".GGGGGGGGGG....."),
            Maos.Cerveja)),
        // Cone de papel, piteira à esquerda, pontinhos verdes e ponta acesa com cinza e brasa. Sem
        // folha. (Apagado, parecia uma cunha de papel.)
        ("baseado", new(Verbo.Fumar,
            new Carimbo(
                ".................z..",
                "...............WWzzq",
                "..........WWWWwJwzQq",
                "....WWWWWwwwJwwwwzqq",
                "CCCWwwwwwwwwwwLwwzz.",
                "cccwwwwJwwwwwwwwxz..",
                "sssxxxxxxxxxxxxx...."),
            Maos.Baseado)),
        // Filtro laranja, papel branco, cinza e brasa. 6 linhas = 8 px com contorno; mais fino ficava
        // difícil de agarrar com o mouse.
        ("cigarro", new(Verbo.Fumar,
            new Carimbo(
                ".tTtwwwwwwwwwwwwzz.",
                "tTtTwwwwwwwwwwwwzzq",
                "tTttwwwwwwwwwwwwzQq",
                "tTtTwwwwwwwwwwwwzQq",
                "TTTTxxxxxxxxxxxxzzq",
                ".TTTxxxxxxxxxxxxzz."),
            Maos.Cigarro)),
        // Espelhinho deitado em perspectiva, moldura dourada, brilho de espelho e duas carreiras
        // curtas. (Com vidro liso e carreiras de ponta a ponta parecia cartão ou livro azul.)
        ("cocaina", new(Verbo.Cheirar,
            new Carimbo(
                "......2222222222223",
                ".....2dAdddddadddd3",
                "....2dAdWWWWWaddd3.",
                "...2dAdddddddaddd3.",
                "..2dAddWWWWWadddd3.",
                ".2dAddddddddaddd3..",
                "2333333333333333..."),
            Maos.Cocaina)),
        // Comprimido lilás com um coração em relevo.
        ("md", new(Verbo.Engolir,
            new Carimbo(
                "....VVVV....",
                "..VVVVVVVV..",
                ".VVVVVVVVVV.",
                "VVVWWVVWWVVV",
                "VVVWWWWWWVVV",
                "VVVVWWWWVVVV",
                "XVVVVWWVVVVX",
                "XXVVVVVVVVXX",
                ".XXXXXXXXXX.",
                "...XXXXXX..."),
            Maos.Md)),
        // Frasco fino com válvula e, ao lado, lenço dobrado em triângulo com barra azul. (Em retângulo
        // listrado parecia pilha de toalhas.)
        ("lancaperfume", new(Verbo.Inalar,
            new Carimbo(
                ".E................",
                "EmE...............",
                "mMmE..............",
                "mmmE..............",
                ".gG...............",
                "gWgG..............",
                "gWYG..............",
                "gWYG..............",
                "gWYG.......W......",
                "gWYG......WWx.....",
                "gWYG.....WWdWx....",
                "gWYG....WWdWdWx...",
                "gWYG...WWdWWWdWx..",
                "GGGG..dddddddddddd"),
            Maos.Lancaperfume)),
        // Xícara branca com café, alça à direita e pires.
        ("cafe", new(Verbo.Beber,
            new Carimbo(
                "..WWWWWWWWW....",
                ".WkkkkkkkkkW...",
                ".WWkkkkkkkWWx..",
                ".WWWWWWWWWWxWW.",
                "..WWWWWWWWWx..W",
                "..WWWWWWWWxx.xW",
                "...xWWWWWxxWW..",
                "WWWWWWWWWWWWWWx",
                ".xxxxxxxxxxxxx."),
            Maos.Cafe)),
        // Lata fina verde-neon com raio amarelo. (Escura com raio verde lembrava uma marca conhecida
        // e tinha a cor do pelo: sumia no corpo e na boca parecia barba.)
        ("energetico", new(Verbo.Beber,
            new Carimbo(
                ".MMmmE.",
                "MmmmmmE",
                "EEEEEEE",
                "NNNN3NP",
                "NNN33NP",
                "NN332NP",
                "N3222NP",
                "N22222P",
                "NN223NP",
                "N223NNP",
                "N23NNNP",
                "23NNNNP",
                "NNNNNNP",
                "NNNNNNP",
                "NNNNNPP",
                "EEEEEEE",
                ".mmmmE."),
            Maos.Energetico)),
        // Cogumelo de desenho animado: chapéu vermelho de bolinhas brancas e pé creme.
        ("cogumelo", new(Verbo.Comer,
            new Carimbo(
                "....111111....",
                "..11WW111111..",
                ".1WWWW1111WW1.",
                "11WWW1111WWW1f",
                "11111111WWW11f",
                "f111WW1111111f",
                ".FFFFFFFFFFFF.",
                "....ccccCs....",
                "....cCccss....",
                "....cCccss....",
                "...ccCcccss...",
                "...cccccsss...",
                "....ssssss...."),
            Maos.Cogumelo)),
        // Bala embrulhada em papel rosa listrado, torcido nas pontas.
        ("bala", new(Verbo.Engolir,
            new Carimbo(
                "RR....RRRR....RR",
                "RRR..RRWRRR..RRR",
                "RRRRRRWRRWRRRRRS",
                "RRRRRWRRWRRWRRSS",
                "SSSSRWRRWRRSSSSS",
                "SSS..SRWRRS..SSS",
                "SS....SSSS....SS"),
            Maos.Bala)),
    ];

    private static readonly Dictionary<string, Definicao> PorChave = Tabela.ToDictionary(t => t.Chave, t => t.Definicao, StringComparer.Ordinal);

    // Na ordem do menu, que é a do enum Item do núcleo.
    public static readonly IReadOnlyList<string> Todos = [.. Tabela.Select(t => t.Chave)];

    public static IReadOnlyList<string> DoVerbo(Verbo verbo) => [.. Tabela.Where(t => t.Definicao.Verbo == verbo).Select(t => t.Chave)];

    public static Verbo VerboDe(string item) => Achar(item).Verbo;

    // Centrado na horizontal, contorno de baixo na última linha (pousa como os pés), 1 px livre
    // em cima e dos lados.
    public static Tela Desenhar(string item)
    {
        Carimbo chao = Achar(item).Chao;
        var tela = new Tela(Lado, Lado);
        tela.Carimbar(chao, (Lado - chao.Largura) / 2, Lado - 1 - chao.Altura);
        tela.Contornar(Cor.Contorno);
        return tela;
    }

    // Na ordem da animação; a primeira é a padrão.
    public static IReadOnlyList<string> VariantesNaMao(string item) => [.. Achar(item).NaMao.Select(v => v.Nome)];

    public static ItemNaMao NaMao(string item, string? variante = null)
    {
        (string Nome, ItemNaMao Mao)[] variantes = Achar(item).NaMao;
        if (variante is null) return variantes[0].Mao;
        foreach ((string nome, ItemNaMao mao) in variantes)
            if (string.Equals(nome, variante, StringComparison.Ordinal)) return mao;
        throw new ArgumentException($"Variante desconhecida do item '{item}': '{variante}'.", nameof(variante));
    }

    // No desenho do chão: um pixel opaco perto do centro do corpo e um transparente no canto (0, 0).
    public static ((int X, int Y) Opaco, (int X, int Y) Transparente) PontosDeTeste(string item)
    {
        Tela t = Desenhar(item);
        var corpo = new List<(int X, int Y)>();
        for (int y = 0; y < t.Altura; y++)
            for (int x = 0; x < t.Largura; x++)
                if (t.Opaco(x, y) && t[x, y] != Cor.Contorno) corpo.Add((x, y));
        double cx = corpo.Average(p => p.X), cy = corpo.Average(p => p.Y);
        (int X, int Y) opaco = corpo.MinBy(p => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
        return (opaco, (0, 0));
    }

    private static Definicao Achar(string item)
        => item is not null && PorChave.TryGetValue(item, out Definicao? d)
            ? d
            : throw new ArgumentException($"Item desconhecido: '{item}'. Os itens são: {string.Join(", ", Todos)}.", nameof(item));

    // Variantes na ordem da animação. Pega onde a palma cobre; ponta no pixel da boca ou do nariz.
    private static class Maos
    {
        // Banana descascada em cima, segura pela casca: a polpa sobe e a casca abre em abas.
        internal static readonly (string, ItemNaMao)[] Banana =
        [
            ("aberto", new(new Carimbo(
                "...WC...",
                "..WWCc..",
                "..WWCc..",
                "..WWCc..",
                "..WWCc..",
                "y.WWCc.n",
                "yyYWCynn",
                ".yYyyyn.",
                "..yYyn..",
                "..yYyn..",
                "..yYyn..",
                "..yyyn..",
                "...yn...",
                "...nk..."), (3, 10), (3, 0))),
            ("mordido", new(new Carimbo(
                "........",
                "........",
                "........",
                "..W..c..",
                "..WWCc..",
                "y.WWCc.n",
                "yyYWCynn",
                ".yYyyyn.",
                "..yYyn..",
                "..yYyn..",
                "..yYyn..",
                "..yyyn..",
                "...yn...",
                "...nk..."), (3, 10), (3, 4))),
            ("resto", new(new Carimbo(
                "........",
                "........",
                "........",
                "........",
                "........",
                "y......n",
                "yy.YY.nn",
                ".yYyyyn.",
                "..yYyn..",
                "..yYyn..",
                "..yYyn..",
                "..yyyn..",
                "...yn...",
                "...nk..."), (3, 10), null)),
        ];

        // Cogumelo seguro pelo pé, com o chapéu de bolinhas para cima.
        internal static readonly (string, ItemNaMao)[] Cogumelo =
        [
            ("aberto", new(new Carimbo(
                "...1111..",
                ".11WW111.",
                "1WWWW111f",
                "11WW111Wf",
                "f11111WWf",
                ".ff111ff.",
                "..FFFFF..",
                "...cCs...",
                "...cCs...",
                "...cCs...",
                "...css..."), (4, 10), (4, 0))),
            ("mordido", new(new Carimbo(
                ".........",
                ".....111.",
                "....W111f",
                "..WW111Wf",
                "f11111WWf",
                ".ff111ff.",
                "..FFFFF..",
                "...cCs...",
                "...cCs...",
                "...cCs...",
                "...css..."), (4, 10), (4, 2))),
            ("resto", new(new Carimbo(
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "...FFF...",
                "...cCs...",
                "...cCs...",
                "...cCs...",
                "...css..."), (4, 10), null)),
        ];

        internal static readonly (string, ItemNaMao)[] Agua = Bebida(new Carimbo(
            "..ddd..",
            "..dAd..",
            "..ddd..",
            "..gAG..",
            ".gAaaG.",
            "gAaaaaG",
            "wwwwwwx",
            "wwwdwwx",
            "wwdddwx",
            "gWaaaaG",
            "gAaaaaG",
            "gaaaadG",
            ".GGGGG."), pega: (3, 10), ponta: (3, 0), girar: true);

        internal static readonly (string, ItemNaMao)[] Vodka = Bebida(new Carimbo(
            "..mE..",
            "..ME..",
            "..gG..",
            "..gG..",
            "..gG..",
            ".gWgG.",
            "gWgggG",
            "ffffFF",
            "ffffFF",
            "ffffFF",
            "gWgggG",
            "gWgggG",
            "gggggG",
            ".GGGG."), pega: (2, 11), ponta: (2, 0), girar: true);

        // Segura pela alça; no gole, a caneca continua em pé, com a espuma na boca.
        internal static readonly (string, ItemNaMao)[] Cerveja = Bebida(new Carimbo(
            "..WW.WW...",
            ".WWWWWWW..",
            "WWWWWWWWx.",
            "xWWWWWWxx.",
            "gYBBBBDGGG",
            "gYBBBBDG.G",
            "gYBBBBDG.G",
            "gYBBBBDG.G",
            "gYBBBBDGGG",
            "gBBBBBDG..",
            "gDDDDDDG..",
            ".GGGGGG..."), pega: (9, 6), ponta: (3, 0), girar: false);

        // Segura pela alça; no gole, a xícara continua em pé.
        internal static readonly (string, ItemNaMao)[] Cafe = Bebida(new Carimbo(
            ".WWWWW...",
            "WkkkkkW..",
            "WWWWWWxWW",
            "WWWWWWx.W",
            ".WWWWxWW.",
            "..xxx...."), pega: (8, 3), ponta: (3, 0), girar: false);

        internal static readonly (string, ItemNaMao)[] Energetico = Bebida(new Carimbo(
            ".MmmE.",
            "EmmmmE",
            "NNN32P",
            "NN322P",
            "N322NP",
            "N2222P",
            "NN22NP",
            "N22NNP",
            "23NNNP",
            "EmmmmE",
            ".EEEE."), pega: (2, 7), ponta: (2, 0), girar: true);

        // Desenhado com a piteira em cima; na mão inverte (brasa pra cima) e na tragada deita com a
        // piteira na boca.
        internal static readonly (string, ItemNaMao)[] Baseado = Fumo(
            [
                ".C..",
                ".c..",
                ".w..",
                ".ww.",
                ".wJ.",
                "wwww",
                "wJww",
                "wwwL",
                "wwww",
                "xwJx",
                ".xx.",
                ".qq.",
            ], brasaClara: ".QQ.", pegaAceso: (1, 2), pegaTragando: (1, 4));

        // Comprido pro papel aparecer dos dois lados dos dedos.
        internal static readonly (string, ItemNaMao)[] Cigarro = Fumo(
            [
                "tT",
                "tt",
                "Tt",
                "ww",
                "ww",
                "ww",
                "ww",
                "ww",
                "ww",
                "ww",
                "ww",
                "wx",
                "zz",
                "qq",
            ], brasaClara: "QQ", pegaAceso: (0, 2), pegaTragando: (0, 6));

        // Numa mão só (com as duas na barriga parecia biquíni e sunga). "cheia": na palma como
        // bandeja, com a pega abaixo do desenho pra mão não cortar ele ao meio. "meia"/"vazia": pela
        // ponta direita, borda de cima no nariz; a carreira que sobra some na segunda fungada.
        internal static readonly (string, ItemNaMao)[] Cocaina =
        [
            ("cheia", new(new Carimbo(
                "..2222222223",
                ".2dAWWWWdad3",
                "2dAddWWWWd3.",
                "2333333333.."), (5, 6), null)),
            ("meia", new(new Carimbo(
                "..2222222223",
                ".2dAWWWWdad3",
                "2dAdddddad3.",
                "2333333333.."), (10, 2), (6, 0))),
            ("vazia", new(new Carimbo(
                "..2222222223",
                ".2dAdddddad3",
                "2dAdddddad3.",
                "2333333333.."), (10, 2), (6, 0))),
        ];

        // Ponta dos dedos: pega abaixo do comprimido pra palma não cobrir. Na boca, igual.
        private static readonly ItemNaMao Comprimido = new(new Carimbo(
            ".VVV.",
            "VWVWV",
            "VVWVV",
            "XVVVX",
            ".XXX."), (2, 8), (2, 2));

        internal static readonly (string, ItemNaMao)[] Md = [("normal", Comprimido), ("na-boca", Comprimido)];

        // Ponta dos dedos também: embrulhada na mão, sem papel na boca (embrulhada no rosto parecia
        // gravata-borboleta).
        internal static readonly (string, ItemNaMao)[] Bala =
        [
            ("normal", new(new Carimbo(
                "RR.RRR.RR",
                "RRRRWRRRS",
                "RRRWRRRSS",
                "SS.SSS.SS"), (4, 7), (4, 1))),
            ("na-boca", new(new Carimbo(
                ".RRR.",
                "RWRRS",
                "RRRRS",
                "RRRSS",
                ".SSS."), (2, 8), (2, 2))),
        ];

        // Frasco na mão A; lenço na B, até o nariz (a ponta é o meio do lenço).
        internal static readonly (string, ItemNaMao)[] Lancaperfume =
        [
            ("frasco", new(new Carimbo(
                ".mE.",
                "EmmE",
                ".Mm.",
                ".mE.",
                ".gG.",
                "gWgG",
                "gWYG",
                "gWYG",
                "gWYG",
                "gWYG",
                "gWYG",
                "gYYG",
                ".GG."), (1, 10), null)),
            ("lenco", new(new Carimbo(
                ".WWWWWW.",
                "WWWWWWWx",
                "dddddddd",
                "WWWWWWWx",
                "dddddddd",
                "WWWWWWWx",
                ".xxxxxx."), (6, 5), (3, 3))),
        ];

        // "normal" em pé, à vista; "gole" com a ponta na boca. Garrafa e lata deitam (girar), caneca
        // e xícara ficam em pé.
        private static (string, ItemNaMao)[] Bebida(Carimbo emPe, (int X, int Y) pega, (int X, int Y) ponta, bool girar)
        {
            var gole = new ItemNaMao(emPe, pega, ponta);
            return [("normal", gole with { Ponta = null }), ("gole", girar ? gole.Girado() : gole)];
        }

        // Recebe o desenho com o filtro em cima (as pegas são nesse referencial). "aceso": invertido,
        // brasa pra cima, filtro entre os dedos. "tragando": deitado com o filtro na boca, a palma longe
        // o bastante pro filtro aparecer e a última linha trocada pela brasa acesa.
        private static (string, ItemNaMao)[] Fumo(string[] filtroEmCima, string brasaClara, (int X, int Y) pegaAceso, (int X, int Y) pegaTragando)
        {
            int altura = filtroEmCima.Length;
            var aceso = new ItemNaMao(new Carimbo([.. filtroEmCima.Reverse()]), (pegaAceso.X, altura - 1 - pegaAceso.Y), null);
            var tragando = new ItemNaMao(new Carimbo([.. filtroEmCima[..^1], brasaClara]), pegaTragando, (pegaTragando.X, 0)).Girado();
            return [("aceso", aceso), ("tragando", tragando)];
        }
    }
}
