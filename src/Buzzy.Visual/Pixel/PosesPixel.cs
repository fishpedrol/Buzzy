namespace Buzzy.Visual.Pixel;

/// <summary>
/// Poses-chave da pixel art, uma por estado ou gesto de ARCHITECTURE.md 2.6 (as mesmas da
/// identidade anterior). Ângulos dos membros em graus na tela: 0 para baixo, 90 para a direita,
/// −90 para a esquerda, 180 para cima.
/// </summary>
public static class PosesPixel
{
    public static readonly IReadOnlyList<PosePixel> Todas =
    [
        new PosePixel
        {
            Nome = "parado",
            Estado = "IDLE",
            BracoA = new(-24, -8),
            BracoB = new(24, 8),
        },
        new PosePixel
        {
            Nome = "reagindo",
            Estado = "REACTING",
            QuadrilY = 49,
            BracoA = new(-128, -158),
            BracoB = new(128, 158),
            PernaA = new(-18, 12),
            PernaB = new(14, -4),
            Cauda = Cauda.Alta,
            Expressao = "rindo",
        },
        new PosePixel
        {
            Nome = "caindo",
            Estado = "FALLING",
            QuadrilY = 49,
            BracoA = new(-145, -170),
            BracoB = new(145, 170),
            PernaA = new(-22, -8),
            PernaB = new(22, 8),
            Cauda = Cauda.Alta,
            Expressao = "assustado",
        },
        new PosePixel
        {
            Nome = "pousando",
            Estado = "LANDING",
            QuadrilY = 51.5,
            BracoA = new(-70, -50),
            BracoB = new(70, 50),
            PernaA = new(-42, 22),
            PernaB = new(42, -22),
            Cauda = Cauda.Caida,
            Expressao = "surpreso",
        },
        new PosePixel
        {
            Nome = "pendurado",
            Estado = "HANGING",
            QuadrilY = 40,
            CabecaDescida = 11,
            BracoA = new(-150, -165),
            BracoB = new(150, 165),
            MaoA = Mao.Fechada,
            MaoB = Mao.Fechada,
            PernaA = new(-8, 0),
            PernaB = new(18, 34),
            Cauda = Cauda.Caida,
            Expressao = "feliz",
        },
        // Cipó da borda de cima (DEC-024): segura o cipó com a mão B e balança; o quadro 2 é o do meio.
        Cipo("cipo-1", tronco: 9, deslocamentoDoCipo: 5.5, pernaA: new(-2, 10), pernaB: new(24, 44)),
        Cipo("cipo-2", tronco: 0, deslocamentoDoCipo: 0, pernaA: new(-8, 0), pernaB: new(18, 34)),
        Cipo("cipo-3", tronco: -9, deslocamentoDoCipo: -5.5, pernaA: new(-18, -8), pernaB: new(10, 22)),
        new PosePixel
        {
            Nome = "sentado",
            Estado = "RESTING",
            QuadrilY = 56,
            BracoA = new(-20, 22),
            BracoB = new(20, -22),
            PernaA = new(-58, 44),
            PernaB = new(58, -44),
            Expressao = "neutro",
        },
        new PosePixel
        {
            Nome = "dormindo",
            Estado = "RESTING",
            QuadrilY = 56,
            Tronco = 4,
            Cabeca = 16,
            CabecaDescida = 2,
            BracoA = new(-24, 62),
            BracoB = new(24, -62),
            PernaA = new(-58, 44),
            PernaB = new(58, -44),
            Cauda = Cauda.Caida,
            Expressao = "dormindo",
        },
        new PosePixel
        {
            Nome = "segurado",
            Estado = "PRESSED/DRAGGING",
            QuadrilY = 49,
            BracoA = new(-12, -2),
            BracoB = new(12, 2),
            PernaA = new(-8, -2),
            PernaB = new(8, 2),
            Cauda = Cauda.Caida,
            Expressao = "surpreso",
        },
        new PosePixel
        {
            Nome = "olhando",
            Estado = "gesto: olhar ao redor",
            Cabeca = -10,
            BracoA = new(-24, -8),
            BracoB = new(160, -95),
            Expressao = "curioso",
        },
        new PosePixel
        {
            Nome = "cocando",
            Estado = "gesto: coçar-se",
            Cabeca = 8,
            BracoA = new(-24, -8),
            BracoB = new(168, -120),
            Expressao = "pensativo",
        },
        new PosePixel
        {
            Nome = "espreguicando",
            Estado = "gesto: espreguiçar-se",
            QuadrilY = 48.5,
            BracoA = new(-146, -160),
            BracoB = new(146, 160),
            Cauda = Cauda.Alta,
            Expressao = "bocejando",
        },
        new PosePixel
        {
            Nome = "espiando",
            Estado = "gesto: espiar",
            QuadrilY = 78,
            Borda = 58,
            BracoA = new(-80, 150),
            BracoB = new(80, -150),
            MaoA = Mao.Fechada,
            MaoB = Mao.Fechada,
            Cauda = Cauda.Alta,
            Expressao = "curioso",
        },
        // Esconderijo (DEC-025): o "espiando" com a borda na última linha do quadro, onde fica a borda
        // da tela. Só o chapéu, a cabeça e as mãos, que seguram a borda; nas laterais, girado.
        new PosePixel
        {
            Nome = "escondido",
            Estado = "PEEKING",
            QuadrilY = 84,
            Borda = 64,
            BracoA = new(-80, 150),
            BracoB = new(80, -150),
            MaoA = Mao.Fechada,
            MaoB = Mao.Fechada,
            Cauda = Cauda.Alta,
            Expressao = "curioso",
        },
        new PosePixel
        {
            Nome = "brincando",
            Estado = "gesto: brincar",
            QuadrilY = 48,
            Cabeca = -8,
            BracoA = new(-58, -150),
            BracoB = new(62, 24),
            PernaA = new(-4, 0),
            PernaB = new(44, -24),
            Cauda = Cauda.Alta,
            Expressao = "travesso",
        },
        Andando("andando-1", perto: new(28, 8), longe: new(-28, -12), bracoPerto: new(-26, -14), bracoLonge: new(24, 34), y: 48.5),
        Andando("andando-2", perto: new(4, 0), longe: new(-14, -62), bracoPerto: new(-6, 0), bracoLonge: new(6, 12), y: 47.5),
        Andando("andando-3", perto: new(-28, -12), longe: new(28, 8), bracoPerto: new(24, 34), bracoLonge: new(-26, -14), y: 48.5),
        Andando("andando-4", perto: new(-14, -62), longe: new(4, 0), bracoPerto: new(6, 12), bracoLonge: new(-6, 0), y: 47.5),
        new PosePixel
        {
            Nome = "impulso",
            Estado = "JUMPING (antecipação)",
            Vista = Vista.Perfil,
            QuadrilX = 30,
            QuadrilY = 52.5,
            Tronco = 26,
            BracoA = new(-58, -30),
            BracoB = new(-66, -40),
            PernaA = new(58, -18),
            PernaB = new(66, -14),
            Cauda = Cauda.PerfilAlta,
            Expressao = "determinado",
        },
        new PosePixel
        {
            Nome = "no-ar",
            Estado = "JUMPING (extensão)",
            Vista = Vista.Perfil,
            QuadrilX = 29,
            QuadrilY = 49,
            Tronco = 12,
            BracoA = new(128, 112),
            BracoB = new(138, 120),
            PernaA = new(-28, -44),
            PernaB = new(-14, -30),
            Cauda = Cauda.PerfilAlta,
            Expressao = "empolgado",
        },
        new PosePixel
        {
            Nome = "escalando-1",
            Estado = "CLIMBING",
            Vista = Vista.Perfil,
            QuadrilX = 42,
            QuadrilY = 47,
            Tronco = 4,
            BracoA = new(128, 150),
            BracoB = new(158, 176),
            MaoA = Mao.Fechada,
            MaoB = Mao.Fechada,
            PernaA = new(64, 8),
            PernaB = new(112, 22),
            Cauda = Cauda.PerfilCaida,
            Expressao = "determinado",
        },
        new PosePixel
        {
            Nome = "escalando-2",
            Estado = "CLIMBING",
            Vista = Vista.Perfil,
            QuadrilX = 42,
            QuadrilY = 46,
            Tronco = 4,
            BracoA = new(158, 176),
            BracoB = new(128, 150),
            MaoA = Mao.Fechada,
            MaoB = Mao.Fechada,
            PernaA = new(112, 22),
            PernaB = new(64, 8),
            Cauda = Cauda.PerfilCaida,
            Expressao = "determinado",
        },
    ];

    /// <summary>
    /// Poses dos gestos da onda do tamagotchi (DEC-028; crítica, L11), com os nomes que o núcleo gera do enum
    /// <c>Gesto</c> em minúsculas, na ordem dele. As seis primeiras são provisórias: a dança é o "brincando", a
    /// gargalhada é o "reagindo", a tremedeira é o parado deslocado 1 pixel (a apresentação alterna as duas) e o
    /// soluço, a tosse e o espirro são o parado com a cara e o efeito do gesto (a fumaça da tosse sai da
    /// boca; o espirro solta a poeira do nariz). As duas últimas são da paranoia (adicional de 2026-10-01, "os cara
    /// tá no teto"), desenhadas para ela, com a cara "paranoico": o "olharproteto" aponta para o teto e o "agachar"
    /// segura o chapéu com as duas mãos. Ficam fora de <see cref="Todas"/>, como as poses de uso: a folha nativa e
    /// as prévias das poses não mudam. <see cref="PorNome"/> acha as três listas.
    /// </summary>
    public static readonly IReadOnlyList<PosePixel> DosGestos = CriarGestos();

    private static PosePixel[] CriarGestos()
    {
        PosePixel parado = Todas.First(p => p.Nome == "parado");
        PosePixel brincando = Todas.First(p => p.Nome == "brincando");
        PosePixel reagindo = Todas.First(p => p.Nome == "reagindo");
        return
        [
            parado with { Nome = "soluco", Estado = "gesto: soluçar", Expressao = "surpreso" },
            brincando with { Nome = "danca", Estado = "gesto: dançar" },
            reagindo with { Nome = "gargalhada", Estado = "gesto: gargalhar" },
            parado with { Nome = "espirro", Estado = "gesto: espirrar", Expressao = "tossindo", EfeitoDaPose = EfeitoVisual.Poeira },
            parado with { Nome = "tosse", Estado = "gesto: tossir", Expressao = "tossindo", EfeitoDaPose = EfeitoVisual.Fumaca },
            parado with { Nome = "tremedeira", Estado = "gesto: tremer", Expressao = "eletrico", QuadrilX = parado.QuadrilX + 1 },
            // Paranoia: de joelhos dobrados, olha para cima e aponta o teto com o indicador, reto para cima; o braço sobe
            // pela frente da orelha, longe do rosto, com o punho à direita da ponta da aba (o dedo não encosta nela), e a
            // outra mão aperta o peito. De frente, o boneco não inclina a cabeça para trás: o pescoço estica 1 pixel,
            // esticando-se para ver o teto (revisão da paranoia, achados 4 e 5).
            parado with
            {
                Nome = "olharproteto",
                Estado = "gesto: olhar pro teto",
                QuadrilY = 49.1,
                CabecaDescida = -1,
                PernaA = new(-20, 8),
                PernaB = new(20, -8),
                BracoA = new(-25, 120),
                MaoA = Mao.Fechada,
                BracoB = new(130, 145),
                MaoB = Mao.Apontando,
                BracoBNaFrente = true,
                Cauda = Cauda.Alta,
                Expressao = "paranoico",
            },
            // Paranoia: agachado, com a cabeça encolhida entre os ombros, segura a aba com os dois punhos e espia para
            // cima; os cotovelos abrem para fora e os antebraços sobem ao lado dos olhos, sem cobrir o rosto. (Abertas e
            // de dedos para cima, acima da aba, as mãos pareciam orelhas.)
            parado with
            {
                Nome = "agachar",
                Estado = "gesto: agachar",
                QuadrilY = 52.5,
                CabecaDescida = 3,
                PernaA = new(-70, 15),
                PernaB = new(70, -15),
                BracoA = new(-135, 174),
                BracoB = new(135, -174),
                MaoA = Mao.Fechada,
                MaoB = Mao.Fechada,
                BracoANaFrente = true,
                BracoBNaFrente = true,
                Expressao = "paranoico",
            },
        ];
    }

    /// <summary>
    /// Os quadros extras dos clipes da Fase 6 (DEC-036, item 5): o segundo (ou o primeiro) quadro de cada animação nova,
    /// feito da pose-chave com poucos ângulos mudados, para o corpo não pular de um quadro para o outro. Ficam fora de
    /// <see cref="Todas"/>, como os gestos da onda: a folha nativa não muda. O manifesto de clipes do app diz a ordem e os
    /// tempos; <see cref="PorNome"/> acha estes também.
    /// </summary>
    public static readonly IReadOnlyList<PosePixel> DosClipes = CriarQuadrosDosClipes();

    private static PosePixel[] CriarQuadrosDosClipes()
    {
        PosePixel Chave(string nome) => Todas.First(p => p.Nome == nome);
        PosePixel cocando = Chave("cocando"), espreguicando = Chave("espreguicando"), olhando = Chave("olhando"), espiando = Chave("espiando"),
            brincando = Chave("brincando"), caindo = Chave("caindo"), reagindo = Chave("reagindo"), andando = Chave("andando-2");
        // Explorar a borda (Fase 7; DEC-037, item 9): de perfil, parado, a mão aberta à frente dos olhos, olhando para fora
        // da lateral; no segundo quadro, a mão sobe um pouco, ajeitando a aba.
        PosePixel espia = andando with
        {
            Nome = "andando-2-espia1",
            Estado = "gesto: espiar na borda",
            BracoA = new(-6, 0),
            BracoB = new(85, 165),
            MaoB = Mao.Aberta,
            PernaA = new(-4, 0),
            PernaB = new(4, 0),
            QuadrilY = 48.5,
        };
        return
        [
            // Coçar: a mão B desce e sobe pela cabeça, que acompanha.
            cocando with { Nome = "cocando-2", Estado = "gesto: coçar-se (2)", Cabeca = 12, BracoB = new(176, -100) },
            // Espreguiçar: os braços sobem pelos lados, ainda sonolento; no fim, na ponta dos pés.
            espreguicando with { Nome = "espreguicando-1", Estado = "gesto: espreguiçar-se (subindo)", BracoA = new(-100, -140), BracoB = new(100, 140), Expressao = "sonolento" },
            espreguicando with { Nome = "espreguicando-2", Estado = "gesto: espreguiçar-se (na ponta dos pés)", QuadrilY = 47.5 },
            // Olhar ao redor: a outra mão faz a aba nos olhos, e a cabeça vira para o outro lado.
            olhando with { Nome = "olhando-2", Estado = "gesto: olhar ao redor (2)", Cabeca = 10, BracoA = new(-160, 95), BracoB = new(24, 8) },
            // Espiar: a cabeça sobe 2 pixels acima da borda e desce de novo.
            espiando with { Nome = "espiando-2", Estado = "gesto: espiar (2)", QuadrilY = 76 },
            // Brincar: troca o braço e a perna levantados.
            brincando with { Nome = "brincando-2", Estado = "gesto: brincar (2)", Cabeca = 8, BracoA = new(-62, -24), BracoB = new(58, 150), PernaA = new(-44, 24), PernaB = new(4, 0) },
            // Cair: os braços debatem e as pernas chutam.
            caindo with { Nome = "caindo-2", Estado = "FALLING (2)", BracoA = new(-120, -150), BracoB = new(120, 150), PernaA = new(-8, 10), PernaB = new(30, 16) },
            // Reagir: um pulinho, com os braços mais altos.
            reagindo with { Nome = "reagindo-2", Estado = "REACTING (2)", QuadrilY = 48, BracoA = new(-118, -146), BracoB = new(118, 146), PernaA = new(-10, -4), PernaB = new(10, 4) },
            espia,
            espia with { Nome = "andando-2-espia2", Estado = "gesto: espiar na borda (2)", BracoB = new(80, 172) },
            // Olhar a janela (Fase 7; DEC-037, item 5): de perfil, parado, os braços soltos. Um quadro só: parado e sem gesto,
            // o relógio fica desligado (invariante 29).
            espia with { Nome = "andando-2-olha1", Estado = "curiosidade: olha a janela", BracoB = new(-6, 0), MaoB = Mao.Fechada, BracoA = new(6, 0) },
        ];
    }

    /// <summary>
    /// A pose pelo nome: uma das poses de estado e gesto (<see cref="Todas"/>), dos gestos da onda
    /// (<see cref="DosGestos"/>), dos clipes (<see cref="DosClipes"/>) ou de uso (<see cref="UsosPixel.Poses"/>); nula se
    /// não houver.
    /// </summary>
    public static PosePixel? PorNome(string nome)
        => Todas.FirstOrDefault(p => p.Nome == nome)
           ?? DosGestos.FirstOrDefault(p => p.Nome == nome)
           ?? DosClipes.FirstOrDefault(p => p.Nome == nome)
           ?? UsosPixel.Poses.FirstOrDefault(p => p.Nome == nome);

    /// <summary>
    /// Pendurado no cipó (DEC-024), de frente: a mão B segura o cipó acima e ao lado do chapéu, a A
    /// balança solta, a cauda sobe para equilibrar. O tronco e o cipó inclinam juntos no balanço.
    /// </summary>
    private static PosePixel Cipo(string nome, double tronco, double deslocamentoDoCipo, Membro pernaA, Membro pernaB) => new()
    {
        Nome = nome,
        Estado = "HANGING",
        QuadrilX = 30 - tronco * 0.25,
        QuadrilY = 50,
        Tronco = tronco,
        // Ombros encolhidos, como quem se pendura: a cabeça desce e o chapéu sai do caminho da mão.
        CabecaDescida = 6,
        BracoA = new(-60 + tronco, -40 + tronco),
        // O braço do cipó segue pouco a inclinação do tronco, para a mão ficar fora da aba do chapéu.
        BracoB = new(144 + tronco * 0.3, 150 + tronco * 0.3),
        MaoA = Mao.Aberta,
        MaoB = Mao.Fechada,
        PernaA = pernaA,
        PernaB = pernaB,
        PesNoChao = false,
        Cauda = Cauda.Alta,
        Expressao = "rindo",
        Cipo = deslocamentoDoCipo,
    };

    private static PosePixel Andando(string nome, Membro perto, Membro longe, Membro bracoPerto, Membro bracoLonge, double y) => new()
    {
        Nome = nome,
        Estado = "WALKING",
        Vista = Vista.Perfil,
        QuadrilX = 30,
        QuadrilY = y,
        Tronco = 5,
        BracoA = bracoLonge,
        BracoB = bracoPerto,
        PernaA = longe,
        PernaB = perto,
        Cauda = Cauda.Perfil,
    };
}
