namespace Buzzy.Visual.Pixel;

/// <summary>Como o Buzzy usa um item do tamagotchi (DEC-028): cada verbo tem as suas poses de uso.</summary>
public enum Verbo
{
    Comer,
    Beber,
    Fumar,
    Cheirar,
    Engolir,
    Inalar,
}

/// <summary>
/// Carimbo de um item na mão, numa variante (em pé, no gole, mordido...). Coordenadas em pixels do
/// carimbo. <see cref="Pega"/> é onde fica o centro da mão que o segura: a palma é desenhada por cima
/// desse ponto, que fica fora do desenho quando o item vai na ponta dos dedos (a pílula, a bala) ou em
/// cima da palma (o espelho à vista). <see cref="Ponta"/> é o pixel que encosta no rosto nas poses de uso que levam o item até lá
/// (a boca da garrafa, o filtro, a mordida, o meio do lenço, a borda de cima do espelho); nula quando a
/// variante não vai ao rosto (a casca, o frasco).
/// </summary>
public sealed record ItemNaMao(Carimbo Desenho, (int X, int Y) Pega, (int X, int Y)? Ponta)
{
    /// <summary>
    /// Girado 90° no sentido anti-horário, sem perda: (x, y) vai para (y, Largura − 1 − x), e o que
    /// estava na linha de cima (a boca da garrafa, o filtro) vai para a coluna da esquerda, rumo à boca.
    /// </summary>
    public ItemNaMao Girado()
    {
        int largura = Desenho.Largura;
        return new(Desenho.Girado(horario: false), (Pega.Y, largura - 1 - Pega.X), Ponta is { } p ? (p.Y, largura - 1 - p.X) : null);
    }
}

/// <summary>
/// Os 13 itens do tamagotchi (DEC-028) em pixel art, na mesma densidade do boneco (1 pixel = 2 DIP):
/// o desenho do chão, que também é o ícone do menu, numa grade de 24 × 24, e o carimbo de cada item
/// na mão, nas variantes que as poses de uso pedem. Genéricos e de desenho animado: sem texto, sem
/// marca e sem folha de maconha. As chaves são os nomes do enum <c>Item</c> do núcleo em minúsculas,
/// na ordem do menu.
/// </summary>
public static class ItensPixel
{
    /// <summary>Lado da grade do item, em pixels de arte.</summary>
    public const int Lado = 24;

    /// <summary>Lado da janela do item a 100%: 24 pixels de arte × 2 DIP.</summary>
    public const double TamanhoLogicoDip = 48;

    private sealed record Definicao(Verbo Verbo, Carimbo Chao, (string Nome, ItemNaMao Mao)[] NaMao);

    // Os carimbos são só o preenchimento, sem contorno externo: o contorno de 1 pixel vem do
    // Contornar (no chão) e da linha interna do boneco (na mão). Luz de cima e da esquerda: realce em
    // cima e à esquerda, sombra embaixo e à direita. Desenhados e retocados olhando as prévias a 8×,
    // 2× e 1× (assets/identidade/pixel/previa/). Os itens do mesmo verbo têm as mesmas variantes na
    // mão, na ordem em que a animação as usa.
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
        // Cone de papel com a piteira de papelão à esquerda, pontinhos verdes e a ponta larga acesa, com
        // cinza e brasa. Sem folha. (Apagado, o cone branco lia como uma cunha de papel: revisão da arte,
        // achado 8.)
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
        // Cigarro reto: filtro laranja, papel branco, cinza e brasa na ponta. Com 6 linhas, para ter 8
        // pixels de altura com o contorno: mais fino, ficava difícil de agarrar (crítica, L14).
        ("cigarro", new(Verbo.Fumar,
            new Carimbo(
                ".tTtwwwwwwwwwwwwzz.",
                "tTtTwwwwwwwwwwwwzzq",
                "tTttwwwwwwwwwwwwzQq",
                "tTtTwwwwwwwwwwwwzQq",
                "TTTTxxxxxxxxxxxxzzq",
                ".TTTxxxxxxxxxxxxzz."),
            Maos.Cigarro)),
        // Espelhinho deitado, em perspectiva, com moldura dourada, o vidro com brilho de espelho (duas
        // faixas claras) e duas carreiras brancas curtas. (Com o vidro liso e as carreiras de ponta a
        // ponta, lia como um cartão ou um livro azul: revisão da arte, achado 8.)
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
        // Frasco fino de vidro com válvula de metal e, ao lado, um lenço dobrado em triângulo, com a barra
        // azul. (Dobrado em retângulo, com listras, lia como uma pilha de toalhas: revisão da arte, achado 8.)
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
        // Lata fina, genérica, verde-neon com um raio amarelo e as bordas de metal. (Escura, com o raio
        // verde, lembrava a paleta de uma marca conhecida e tinha as cores do pelo: some no corpo e, na
        // boca, parecia uma barba. Revisão da arte, achado 12.)
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

    /// <summary>As chaves dos 13 itens, na ordem do menu (a do enum <c>Item</c> do núcleo).</summary>
    public static readonly IReadOnlyList<string> Todos = [.. Tabela.Select(t => t.Chave)];

    /// <summary>Os itens de um verbo, na ordem do menu.</summary>
    public static IReadOnlyList<string> DoVerbo(Verbo verbo) => [.. Tabela.Where(t => t.Definicao.Verbo == verbo).Select(t => t.Chave)];

    /// <summary>Como o item é usado.</summary>
    public static Verbo VerboDe(string item) => Achar(item).Verbo;

    /// <summary>
    /// O item no chão, numa tela nova de 24 × 24: carimbo centrado na horizontal, com o contorno de
    /// baixo na última linha (o item pousa como os pés) e 1 pixel livre no topo e nas laterais.
    /// </summary>
    public static Tela Desenhar(string item)
    {
        Carimbo chao = Achar(item).Chao;
        var tela = new Tela(Lado, Lado);
        tela.Carimbar(chao, (Lado - chao.Largura) / 2, Lado - 1 - chao.Altura);
        tela.Contornar(Cor.Contorno);
        return tela;
    }

    /// <summary>Os nomes das variantes do item na mão, na ordem em que a animação as usa; a primeira é a padrão.</summary>
    public static IReadOnlyList<string> VariantesNaMao(string item) => [.. Achar(item).NaMao.Select(v => v.Nome)];

    /// <summary>O item na mão; sem variante, a primeira de <see cref="VariantesNaMao"/>.</summary>
    public static ItemNaMao NaMao(string item, string? variante = null)
    {
        (string Nome, ItemNaMao Mao)[] variantes = Achar(item).NaMao;
        if (variante is null) return variantes[0].Mao;
        foreach ((string nome, ItemNaMao mao) in variantes)
            if (string.Equals(nome, variante, StringComparison.Ordinal)) return mao;
        throw new ArgumentException($"Variante desconhecida do item '{item}': '{variante}'.", nameof(variante));
    }

    /// <summary>
    /// Pontos de teste em pixels de arte do desenho do chão: um opaco no corpo do item (o mais perto
    /// do centro dos pixels do corpo) e um transparente, no canto de cima à esquerda.
    /// </summary>
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

    /// <summary>
    /// Os itens na mão, nas variantes que as poses de uso pedem, na ordem em que a animação as usa. A
    /// pega fica onde a palma cobre o item; a ponta, no pixel que encosta na boca ou no nariz.
    /// </summary>
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

        // Desenhado com a piteira em cima e a brasa embaixo; na mão fica ao contrário, com a brasa para
        // cima, e na tragada deita com a piteira na boca.
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

        // Comprido o bastante para o papel aparecer dos dois lados dos dedos.
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

        // O espelhinho numa mão só (revisão da arte, achado 1: seguro pelas duas mãos na altura da
        // barriga, parecia um biquíni e uma sunga). "cheia": deitado em perspectiva, na palma, como uma
        // bandeja, à vista ao lado do peito (a pega fica abaixo do desenho, para a mão não o cortar ao
        // meio). "meia" e "vazia": seguro pela ponta da direita, com o meio da borda de cima no nariz; a
        // carreira que sobra some na segunda fungada.
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

        // Na ponta dos dedos: a pega fica abaixo do comprimido, para a palma não o cobrir. Na boca, o mesmo.
        private static readonly ItemNaMao Comprimido = new(new Carimbo(
            ".VVV.",
            "VWVWV",
            "VVWVV",
            "XVVVX",
            ".XXX."), (2, 8), (2, 2));

        internal static readonly (string, ItemNaMao)[] Md = [("normal", Comprimido), ("na-boca", Comprimido)];

        // A bala também vai na ponta dos dedos: embrulhada na mão; na boca, já sem o papel (embrulhada no
        // rosto, a bala lia como uma gravata-borboleta: revisão da arte, achado 8).
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

        // O frasco vai na mão A; o lenço, na B, até o nariz: a ponta é o meio do lenço.
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

        /// <summary>
        /// As duas variantes de uma bebida: "normal", em pé, segura pela <paramref name="pega"/>, à vista, e
        /// "gole", com a <paramref name="ponta"/> na boca: a garrafa e a lata deitam para a boca
        /// (<paramref name="girar"/>), a caneca e a xícara continuam em pé.
        /// </summary>
        private static (string, ItemNaMao)[] Bebida(Carimbo emPe, (int X, int Y) pega, (int X, int Y) ponta, bool girar)
        {
            var gole = new ItemNaMao(emPe, pega, ponta);
            return [("normal", gole with { Ponta = null }), ("gole", girar ? gole.Girado() : gole)];
        }

        /// <summary>
        /// As duas variantes de um fumo desenhado com o filtro em cima: "aceso", de cabeça para baixo, com
        /// a brasa para cima e o filtro entre os dedos (<paramref name="pegaAceso"/>), e "tragando", deitado
        /// com o filtro na boca, a palma longe o bastante para o filtro aparecer (<paramref name="pegaTragando"/>)
        /// e a última linha (a brasa) trocada por <paramref name="brasaClara"/>. As pegas são do desenho com o
        /// filtro em cima.
        /// </summary>
        private static (string, ItemNaMao)[] Fumo(string[] filtroEmCima, string brasaClara, (int X, int Y) pegaAceso, (int X, int Y) pegaTragando)
        {
            int altura = filtroEmCima.Length;
            var aceso = new ItemNaMao(new Carimbo([.. filtroEmCima.Reverse()]), (pegaAceso.X, altura - 1 - pegaAceso.Y), null);
            var tragando = new ItemNaMao(new Carimbo([.. filtroEmCima[..^1], brasaClara]), pegaTragando, (pegaTragando.X, 0)).Girado();
            return [("aceso", aceso), ("tragando", tragando)];
        }
    }
}
