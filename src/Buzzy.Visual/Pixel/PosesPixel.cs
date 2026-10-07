namespace Buzzy.Visual.Pixel;

// Uma pose-chave por estado ou gesto. Ângulos em graus na tela: 0 = baixo, 90 = direita,
// -90 = esquerda, 180 = cima.
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
        // Cipó na borda de cima: segura com a mão B e balança; o quadro 2 é o do meio.
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
        // Esconderijo: o "espiando" com a borda na última linha do quadro (a borda da tela). Só
        // aparecem chapéu, cabeça e mãos; nas laterais, girado.
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

    // Gestos da onda, com o nome do enum Gesto do núcleo em minúsculas e na mesma ordem. As seis
    // primeiras são provisórias, reaproveitando poses (tremedeira = parado deslocado 1 px, a
    // apresentação alterna). As duas últimas são da paranoia, com desenho próprio. Ficam fora de
    // Todas pra não mudar a folha nativa nem as prévias.
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
            // Joelhos dobrados, aponta o teto; o braço sobe pela frente da orelha com o punho à
            // direita da ponta da aba (o dedo não encosta nela), a outra mão aperta o peito. De frente
            // não dá pra inclinar a cabeça pra trás, então o pescoço estica 1 px.
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
            // Agachado, cabeça encolhida, segura a aba com os punhos; cotovelos pra fora, antebraços
            // ao lado dos olhos sem cobrir o rosto. (Mãos abertas acima da aba pareciam orelhas.)
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

    // Quadros extras dos clipes: a pose-chave com poucos ângulos mudados, pro corpo não pular entre
    // quadros. Ordem e tempos ficam no manifesto. Fora de Todas pela mesma razão dos gestos.
    public static readonly IReadOnlyList<PosePixel> DosClipes = CriarQuadrosDosClipes();

    private static PosePixel[] CriarQuadrosDosClipes()
    {
        PosePixel Chave(string nome) => Todas.First(p => p.Nome == nome);
        PosePixel cocando = Chave("cocando"), espreguicando = Chave("espreguicando"), olhando = Chave("olhando"), espiando = Chave("espiando"),
            brincando = Chave("brincando"), caindo = Chave("caindo"), reagindo = Chave("reagindo"), andando = Chave("andando-2");
        // Explorando a borda: de perfil, mão aberta na frente dos olhos olhando pra fora; no 2º quadro
        // a mão sobe um pouco, ajeitando a aba.
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
            // Olhar a janela: de perfil, braços soltos. Um quadro só, porque parado e sem gesto o
            // relógio fica desligado.
            espia with { Nome = "andando-2-olha1", Estado = "curiosidade: olha a janela", BracoB = new(-6, 0), MaoB = Mao.Fechada, BracoA = new(6, 0) },
        ];
    }

    // Procura em todas as listas, inclusive as de uso.
    public static PosePixel? PorNome(string nome)
        => Todas.FirstOrDefault(p => p.Nome == nome)
           ?? DosGestos.FirstOrDefault(p => p.Nome == nome)
           ?? DosClipes.FirstOrDefault(p => p.Nome == nome)
           ?? UsosPixel.Poses.FirstOrDefault(p => p.Nome == nome);

    // De frente: mão B no cipó acima e ao lado do chapéu, A solta, cauda pra cima equilibrando.
    // Tronco e cipó inclinam juntos.
    private static PosePixel Cipo(string nome, double tronco, double deslocamentoDoCipo, Membro pernaA, Membro pernaB) => new()
    {
        Nome = nome,
        Estado = "HANGING",
        QuadrilX = 30 - tronco * 0.25,
        QuadrilY = 50,
        Tronco = tronco,
        // Ombros encolhidos: a cabeça desce e o chapéu sai do caminho da mão.
        CabecaDescida = 6,
        BracoA = new(-60 + tronco, -40 + tronco),
        // Segue pouco o tronco, pra mão ficar fora da aba.
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
