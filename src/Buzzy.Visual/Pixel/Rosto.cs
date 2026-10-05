namespace Buzzy.Visual.Pixel;

/// <summary>
/// Um rosto: olhos, sobrancelhas, boca, topete e bochechas, para as vistas de frente e de perfil.
/// Com <see cref="Corado"/>, as bochechas ganham rubor de frente, na cor <see cref="Rubor"/>
/// (a <see cref="Cor.Bochecha"/> se nula); <see cref="RuborGrande"/> troca os 4 pixels por uma
/// mancha de 3 × 2 em cada bochecha, que aparece também de perfil. Com <see cref="Gota"/>, uma gota de
/// suor fica na têmpora, de frente e de perfil (<see cref="BonecoPixel.CantoDaGota"/>).
/// </summary>
public sealed record Rosto(
    string OlhoE,
    string OlhoD,
    string Sobrancelhas,
    string Boca,
    Topete Topete,
    string OlhoPerfil,
    string BocaPerfil,
    bool Corado = false,
    Cor? Rubor = null,
    bool RuborGrande = false,
    bool Gota = false);

/// <summary>
/// Estado do tufo de pelo do alto da cabeça e do chapéu, que reagem à emoção. <see cref="Torto"/>
/// é o chapéu do bêbado, inclinado, com o tufo normal.
/// </summary>
public enum Topete
{
    Normal,
    Ericado,
    Caido,
    Torto,
}

/// <summary>
/// Carimbos do rosto na escala nativa (1 pixel = 2 DIP a 100%): as 14 expressões de humor de
/// docs/IDENTIDADE_VISUAL.md, as 8 caras de efeito do tamagotchi e as 7 caras passageiras das poses
/// de uso (DEC-028). Os olhos grandes, castanhos e com dois brilhos seguem as pranchas.
/// </summary>
public static class Rostos
{
    /// <summary>As 14 caras de humor, na ordem de expressoes.png e do enum <c>Expressao</c> do núcleo.</summary>
    public static readonly IReadOnlyList<string> DeHumor =
        ["neutro", "feliz", "rindo", "curioso", "surpreso", "assustado", "sonolento", "bocejando", "dormindo", "travesso", "entediado", "pensativo", "empolgado", "determinado"];

    /// <summary>
    /// As 8 caras de efeito do tamagotchi, na ordem em que o núcleo as acrescenta no fim do enum
    /// <c>Expressao</c>; a chave é o nome do valor em minúsculas. A "paranoico" (onda Paranoico, adicional
    /// de 2026-10-01) entrou por último, depois de "viajando".
    /// </summary>
    public static readonly IReadOnlyList<string> DeEfeito = ["bebado", "enjoado", "chapado", "eletrico", "apaixonado", "tonto", "viajando", "paranoico"];

    /// <summary>Caras das poses de uso (morder, mastigar, engolir, tragar...): só existem aqui, não no núcleo.</summary>
    public static readonly IReadOnlyList<string> Passageiras = ["mordendo", "mastigando", "engolindo", "tragando", "soltando", "fungando", "tossindo"];

    // ------------------------------------------------------------------ olhos de frente (7 × 8)
    // Olho da esquerda da tela; o da direita usa o mesmo desenho (o brilho fica do mesmo lado nos
    // dois, como a luz das pranchas).
    public static readonly IReadOnlyDictionary<string, Carimbo> Olhos = new Dictionary<string, Carimbo>
    {
        ["aberto"] = new(
            ".KKKKK.",
            "KKWWWKK",
            "KWiiiiK",
            "KWWuiiK",
            "KiuuuiK",
            "KiiuuIK",
            "KWiiIWK",
            ".KKKKK."),
        ["lado"] = new(
            ".KKKKK.",
            "KKWWWKK",
            "KWWiiiK",
            "KWiWuiK",
            "KWiuuuK",
            "KWIuuiK",
            "KWWiiIK",
            ".KKKKK."),
        ["cima"] = new(
            ".KKKKK.",
            "KKiiiKK",
            "KiWuuiK",
            "KiuuuiK",
            "KIiiiIK",
            "KWWWWWK",
            "KWWWWWK",
            ".KKKKK."),
        ["arregalado"] = new(
            ".KKKKK.",
            "KWWWWWK",
            "KWWiiWK",
            "KWiWuiK",
            "KWiuuiK",
            "KWWiiWK",
            "KWWWWWK",
            ".KKKKK."),
        ["estrela"] = new(
            ".KKKKK.",
            "KKWWWKK",
            "KWiWiiK",
            "KWWWWiK",
            "KiiWiuK",
            "KiiuuIK",
            "KWiiIWK",
            ".KKKKK."),
        ["feliz"] = new(
            ".......",
            ".......",
            "..KKK..",
            ".K...K.",
            "K.....K",
            ".......",
            ".......",
            "......."),
        ["fechado"] = new(
            ".......",
            ".......",
            ".......",
            ".......",
            "K.....K",
            ".KKKKK.",
            ".......",
            "......."),
        ["sonolento"] = new(
            ".......",
            ".......",
            ".......",
            ".KKKKK.",
            "KpppppK",
            "KiuuuiK",
            "KWiiIWK",
            ".KKKKK."),
        ["entediado"] = new(
            ".......",
            ".......",
            ".......",
            ".KKKKK.",
            "KpppppK",
            "KWiuuiK",
            "KWWiiWK",
            ".KKKKK."),
        ["piscada"] = new(
            ".......",
            ".......",
            ".......",
            "K.....K",
            ".KKKKK.",
            ".......",
            ".......",
            "......."),

        // Caras de efeito do tamagotchi (DEC-028), de desenho animado.
        ["semicerrado-vermelho"] = new(
            ".......",
            ".......",
            ".......",
            ".KKKKK.",
            "KpppppK",
            "K5iuu5K",
            "K65556K",
            ".KKKKK."),
        ["bebado-e"] = new(
            ".......",
            ".......",
            ".KKKKK.",
            "KpppppK",
            "KWiuuiK",
            "KWiuIiK",
            "K5WiiWK",
            ".KKKKK."),
        ["bebado-d"] = new(
            ".......",
            ".......",
            ".......",
            ".......",
            "KKKKKKK",
            "KiuuiWK",
            "K5iIW5K",
            ".KKKKK."),
        ["espiral"] = new(
            ".KKKKK.",
            "KWWWWWK",
            "KKKKKWK",
            "KWWWKWK",
            "KWKWKWK",
            "KWKKKWK",
            "KWWWWWK",
            ".KKKKK."),
        ["arco-iris"] = new(
            ".KKKKK.",
            "KRRRRRK",
            "KRWNNRK",
            "KRNaNRK",
            "KRNuNRK",
            "KRNNNRK",
            "KRRRRRK",
            ".KKKKK."),
        ["pontinho"] = new(
            ".KKKKK.",
            "KWWWWWK",
            "KWWWWWK",
            "KWWuWWK",
            "KWWWWWK",
            "KWWWW5K",
            "KWWWWWK",
            ".KKKKK."),
        ["coracao"] = new(
            ".......",
            ".11.11.",
            "1111111",
            "1W11111",
            "1111111",
            ".11111.",
            "..1F1..",
            "...F..."),
        ["apertado-e"] = new(
            ".......",
            ".......",
            ".KK....",
            "...KK..",
            ".....KK",
            "...KK..",
            ".KK....",
            "......."),
        ["apertado-d"] = new(
            ".......",
            ".......",
            "....KK.",
            "..KK...",
            "KK.....",
            "..KK...",
            "....KK.",
            "......."),

        // Paranoico ("os cara tá no teto"): arregalado, sem íris, com a pupila pequena colada no alto,
        // olhando para cima (o "pontinho" do elétrico olha para a frente; o "cima" do pensativo tem a íris
        // grande).
        ["arregalado-cima"] = new(
            ".KKKKK.",
            "KWWuuWK",
            "KWWuuWK",
            "KWWWWWK",
            "KWWWWWK",
            "KWWWWWK",
            "KWWWWWK",
            ".KKKKK."),
    };

    // ------------------------------------------------------------------ olhos de perfil (5 × 8)
    public static readonly IReadOnlyDictionary<string, Carimbo> OlhosPerfil = new Dictionary<string, Carimbo>
    {
        ["aberto"] = new(
            ".KKK.",
            "KKWWK",
            "KWiiK",
            "KWuiK",
            "KiuuK",
            "KiuIK",
            "KWiWK",
            ".KKK."),
        ["feliz"] = new(
            ".....",
            ".....",
            ".KKK.",
            "K...K",
            ".....",
            ".....",
            ".....",
            "....."),
        ["arregalado"] = new(
            ".KKK.",
            "KWWWK",
            "KWiWK",
            "KWuiK",
            "KWuiK",
            "KWiWK",
            "KWWWK",
            ".KKK."),
        ["concentrado"] = new(
            ".....",
            ".KKKK",
            "KKWWK",
            "KWuiK",
            "KiuuK",
            "KiuIK",
            "KWiWK",
            ".KKK."),

        // Caras de efeito do tamagotchi (DEC-028).
        ["semicerrado-vermelho"] = new(
            ".....",
            ".....",
            ".....",
            ".KKK.",
            "KpppK",
            "K5iuK",
            "K655K",
            ".KKK."),
        ["semicerrado"] = new(
            ".....",
            ".....",
            ".....",
            ".KKK.",
            "KpppK",
            "KWiuK",
            "KWiIK",
            ".KKK."),
        // Tonto de perfil: olho revirado. Uma espiral em 3 × 6 pixels lê como algarismo (2, 5, 0, 8).
        ["revirado"] = new(
            ".KKK.",
            "KiuIK",
            "KWiWK",
            "KWWWK",
            "KWWWK",
            "KWWWK",
            "KWWWK",
            ".KKK."),
        ["pontinho"] = new(
            ".KKK.",
            "KWWWK",
            "KWWWK",
            "KWuWK",
            "KWWWK",
            "KWWWK",
            "KWWWK",
            ".KKK."),
        ["arco-iris"] = new(
            ".KKK.",
            "KRRRK",
            "KNNRK",
            "KauRK",
            "KauRK",
            "KNNRK",
            "KRRRK",
            ".KKK."),
        ["coracao"] = new(
            ".....",
            "11.11",
            "11111",
            "1W111",
            "11111",
            ".111.",
            "..1..",
            "....."),
        // Paranoico: a pupila colada no alto, para a frente (o "revirado" do tonto tem a íris no alto).
        ["arregalado-cima"] = new(
            ".KKK.",
            "KWuuK",
            "KWuuK",
            "KWWWK",
            "KWWWK",
            "KWWWK",
            "KWWWK",
            ".KKK."),
    };

    // ------------------------------------------------------------------ sobrancelhas (pares, 17 × 3)
    // Desenhadas sobre a máscara do rosto, acima dos olhos, como um só carimbo.
    public static readonly IReadOnlyDictionary<string, Carimbo> Sobrancelhas = new Dictionary<string, Carimbo>
    {
        ["neutras"] = new(
            "..vvv.....vvv..",
            ".v...........v.",
            "..............."),
        ["erguidas"] = new(
            ".vvvv.....vvvv.",
            "v.............v",
            "..............."),
        ["uma-erguida"] = new(
            "..........vvv..",
            "..vvv....v...v.",
            ".v............."),
        ["preocupadas"] = new(
            "....v.....v....",
            "..vv.......vv..",
            ".v...........v."),
        ["bravas"] = new(
            ".v...........v.",
            "..vv.......vv..",
            "....v.....v...."),
        ["caidas"] = new(
            "...............",
            "..vvv.....vvv..",
            ".v...........v."),
        ["retas"] = new(
            "...............",
            ".vvvv.....vvvv.",
            "..............."),
        ["nenhuma"] = new("..............."),
        // Paranoico: preocupadas (a ponta de dentro mais alta) e erguidas (a linha de baixo fica livre).
        ["aflitas"] = new(
            "....vv...vv....",
            "..vv.......vv..",
            "..............."),
    };

    // ------------------------------------------------------------------ bocas de frente (9 × 4)
    public static readonly IReadOnlyDictionary<string, Carimbo> Bocas = new Dictionary<string, Carimbo>
    {
        ["sorriso"] = new(
            ".K.....K.",
            "..KKKKK..",
            ".........",
            "........."),
        ["firme"] = new(
            ".........",
            "..KKKKK..",
            ".........",
            "........."),
        ["aberta"] = new(
            ".KKKKKKK.",
            ".KbbbbbK.",
            "..KblbK..",
            "...KKK..."),
        ["risada"] = new(
            "KKKKKKKKK",
            "KWbbbbbWK",
            ".KbblbbK.",
            "..KKKKK.."),
        ["canto"] = new(
            ".........",
            "......K..",
            "..KKKK...",
            "........."),
        ["o"] = new(
            "...KKK...",
            "..KbbbK..",
            "..KbbbK..",
            "...KKK..."),
        ["ondulada"] = new(
            ".........",
            "..K.K.K..",
            ".K.K.K.K.",
            "........."),
        ["reta"] = new(
            ".........",
            "..KKKKK..",
            ".........",
            "........."),
        ["reta-pequena"] = new(
            ".........",
            "...KKK...",
            ".........",
            "........."),
        ["lingua"] = new(
            ".K.....K.",
            "..KKKKK..",
            "....KlK..",
            ".....K..."),
        ["bocejo"] = new(
            "..KKKKK..",
            ".KbbbbbK.",
            ".KbblbbK.",
            "..KKKKK.."),

        // Caras de efeito do tamagotchi (DEC-028).
        ["bobo"] = new(
            "K.......K",
            ".KbbbbbK.",
            "..KKlKK..",
            "........."),
        ["dentes"] = new(
            "KKKKKKKKK",
            "KWWWWWWWK",
            "KWKWKWKWK",
            ".KKKKKKK."),
        ["torta"] = new(
            ".........",
            ".K.......",
            "..KK...K.",
            "....KKK.."),
        // Paranoico: pequena e tensa, os dentes cerrados (os "dentes" do elétrico são um sorriso largo).
        ["tensa"] = new(
            ".........",
            "..KKKKK..",
            "..KWKWK..",
            "..KKKKK.."),
    };

    // ------------------------------------------------------------------ bocas de perfil (5 × 4)
    public static readonly IReadOnlyDictionary<string, Carimbo> BocasPerfil = new Dictionary<string, Carimbo>
    {
        ["sorriso"] = new(
            "....K",
            "KKKK.",
            ".....",
            "....."),
        ["aberta"] = new(
            ".KKKK",
            "KbbbK",
            ".KlK.",
            "..K.."),
        ["o"] = new(
            "..KK.",
            ".KbbK",
            "..KK.",
            "....."),
        ["reta"] = new(
            ".....",
            ".KKKK",
            ".....",
            "....."),
        // Paranoico: os dentes cerrados, de lado.
        ["tensa"] = new(
            ".....",
            "..KKK",
            "..KWK",
            "..KKK"),
    };

    /// <summary>
    /// Todas as caras pela chave: as 14 de humor (docs/IDENTIDADE_VISUAL.md, seção 6), as 8 de
    /// efeito e as 7 passageiras, nesta ordem.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Rosto> Expressoes = new Dictionary<string, Rosto>
    {
        ["neutro"] = new("aberto", "aberto", "neutras", "sorriso", Topete.Normal, "aberto", "sorriso"),
        ["feliz"] = new("feliz", "feliz", "neutras", "aberta", Topete.Normal, "feliz", "aberta", Corado: true),
        ["rindo"] = new("feliz", "feliz", "erguidas", "risada", Topete.Ericado, "feliz", "aberta", Corado: true),
        ["curioso"] = new("lado", "lado", "uma-erguida", "canto", Topete.Normal, "aberto", "reta"),
        ["surpreso"] = new("arregalado", "arregalado", "erguidas", "o", Topete.Ericado, "arregalado", "o"),
        ["assustado"] = new("arregalado", "arregalado", "preocupadas", "ondulada", Topete.Ericado, "arregalado", "o"),
        ["sonolento"] = new("sonolento", "sonolento", "caidas", "reta", Topete.Caido, "aberto", "reta"),
        ["bocejando"] = new("fechado", "fechado", "caidas", "bocejo", Topete.Caido, "feliz", "o"),
        ["dormindo"] = new("fechado", "fechado", "nenhuma", "reta-pequena", Topete.Caido, "feliz", "reta"),
        ["travesso"] = new("aberto", "piscada", "uma-erguida", "lingua", Topete.Normal, "aberto", "sorriso"),
        ["entediado"] = new("entediado", "entediado", "retas", "reta", Topete.Caido, "aberto", "reta"),
        ["pensativo"] = new("cima", "cima", "uma-erguida", "canto", Topete.Normal, "aberto", "reta"),
        ["empolgado"] = new("estrela", "estrela", "erguidas", "aberta", Topete.Ericado, "aberto", "aberta", Corado: true),
        ["determinado"] = new("aberto", "aberto", "bravas", "firme", Topete.Normal, "concentrado", "reta"),

        // Caras de efeito (DEC-028): o núcleo as escolhe durante as ondas dos itens.
        ["bebado"] = new("bebado-e", "bebado-d", "uma-erguida", "torta", Topete.Torto, "semicerrado", "sorriso", Corado: true, Rubor: Cor.BochechaForte, RuborGrande: true),
        ["enjoado"] = new("apertado-e", "apertado-d", "preocupadas", "ondulada", Topete.Caido, "semicerrado", "reta", Corado: true, Rubor: Cor.Enjoo, RuborGrande: true),
        ["chapado"] = new("semicerrado-vermelho", "semicerrado-vermelho", "caidas", "bobo", Topete.Caido, "semicerrado-vermelho", "sorriso"),
        ["eletrico"] = new("pontinho", "pontinho", "erguidas", "dentes", Topete.Ericado, "pontinho", "aberta"),
        ["apaixonado"] = new("coracao", "coracao", "neutras", "sorriso", Topete.Normal, "coracao", "sorriso", Corado: true),
        ["tonto"] = new("espiral", "espiral", "preocupadas", "ondulada", Topete.Ericado, "revirado", "o"),
        ["viajando"] = new("arco-iris", "arco-iris", "erguidas", "aberta", Topete.Ericado, "arco-iris", "aberta"),
        // Paranoico ("os cara tá no teto"): medo de algo lá em cima, com o chapéu eriçado.
        ["paranoico"] = new("arregalado-cima", "arregalado-cima", "aflitas", "tensa", Topete.Ericado, "arregalado-cima", "tensa", Gota: true),

        // Caras passageiras das poses de uso (DEC-028): a pose escolhe, o núcleo não as conhece.
        ["mordendo"] = new("fechado", "fechado", "erguidas", "risada", Topete.Ericado, "feliz", "aberta"),
        ["mastigando"] = new("feliz", "feliz", "neutras", "ondulada", Topete.Normal, "feliz", "reta", Corado: true),
        ["engolindo"] = new("fechado", "fechado", "caidas", "firme", Topete.Normal, "feliz", "reta"),
        ["tragando"] = new("sonolento", "sonolento", "caidas", "firme", Topete.Normal, "aberto", "reta"),
        ["soltando"] = new("sonolento", "sonolento", "caidas", "o", Topete.Caido, "aberto", "o"),
        ["fungando"] = new("apertado-e", "apertado-d", "bravas", "reta-pequena", Topete.Normal, "concentrado", "reta"),
        ["tossindo"] = new("apertado-e", "apertado-d", "preocupadas", "o", Topete.Ericado, "concentrado", "o"),
    };
}
