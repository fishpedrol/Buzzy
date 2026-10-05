namespace Buzzy.Visual.Pixel;

/// <summary>Sobreposições de desenho animado por cima do boneco (DEC-028).</summary>
public enum EfeitoVisual
{
    Nenhum,
    Fumaca,
    Bolhas,
    Brilhos,
    Estrelinhas,
    Coracoes,
    Cores,
    Poeira,
    Borrifo,

    /// <summary>Gotas de suor saltando da cabeça: a onda Paranoico (adicional de 2026-10-01).</summary>
    Suor,
}

/// <summary>
/// Sobreposições de efeito do tamagotchi (DEC-028) em <see cref="Fases"/> fases, desenhadas por
/// <see cref="BonecoPixel.Desenhar"/> depois do corpo e antes do contorno: carimbos pequenos em volta
/// da cabeça (ou entre as mãos, no borrifo), com uma linha de contorno onde passam por cima do corpo.
/// Nunca cobrem olhos, sobrancelhas, nariz, rubor e boca (<see cref="AreaDoRosto"/>) e ficam a 2 pixels
/// das bordas do quadro, para o contorno caber. A fase 0 é a parada: sem relógio, vale sempre ela
/// (DEC-011); com o relógio ligado, a apresentação troca a fase a cada 12 passos.
/// <para>
/// A apresentação escolhe a sobreposição pela onda do núcleo (crítica, L12): Bebado → <see cref="EfeitoVisual.Bolhas"/>;
/// Chapado → <see cref="EfeitoVisual.Fumaca"/>; Eletrico → <see cref="EfeitoVisual.Brilhos"/>; Tonto →
/// <see cref="EfeitoVisual.Estrelinhas"/>; Euforico → <see cref="EfeitoVisual.Coracoes"/>; Viajando →
/// <see cref="EfeitoVisual.Cores"/>; Satisfeito, Alegre, Relaxado e Ligado → nenhuma; Paranoico (adicional de
/// 2026-10-01) → <see cref="EfeitoVisual.Suor"/>. <see cref="EfeitoVisual.Poeira"/>
/// e <see cref="EfeitoVisual.Borrifo"/> são das poses de uso (cheirar e inalar); a poeira também é o espirro e
/// a fumaça, a tosse. Os seis gestos da onda (L11) têm poses provisórias em <see cref="PosesPixel.DosGestos"/>:
/// a dança é o "brincando", a gargalhada o "reagindo", a tremedeira o parado deslocado 1 pixel, e o soluço, a
/// tosse e o espirro o parado com a cara e o efeito do gesto, com a sobreposição da onda por cima. Os dois da
/// paranoia, "olharproteto" e "agachar", têm desenho próprio.
/// </para>
/// </summary>
public static class EfeitosPixel
{
    /// <summary>Quantas fases cada sobreposição tem.</summary>
    public const int Fases = 3;

    /// <summary>Distância mínima de um carimbo de efeito às bordas do quadro: o contorno fica a 1 pixel.</summary>
    private const int Margem = 2;

    /// <summary>Os efeitos que desenham alguma coisa, na ordem do enum.</summary>
    public static readonly IReadOnlyList<EfeitoVisual> Todos =
    [
        EfeitoVisual.Fumaca, EfeitoVisual.Bolhas, EfeitoVisual.Brilhos, EfeitoVisual.Estrelinhas,
        EfeitoVisual.Coracoes, EfeitoVisual.Cores, EfeitoVisual.Poeira, EfeitoVisual.Borrifo,
        EfeitoVisual.Suor,
    ];

    // Carimbos: só o preenchimento; o contorno vem do boneco. Luz de cima e da esquerda.
    private static readonly Carimbo FumacaP = new(
        ".ZZ.",
        "ZZZx",
        ".xx.");
    private static readonly Carimbo FumacaM = new(
        ".ZZZ.",
        "ZZZZZ",
        "ZZZZx",
        ".xxx.");
    private static readonly Carimbo FumacaG = new(
        "..ZZ..",
        ".ZZZZ.",
        "ZZZZZZ",
        "ZZZZZx",
        ".xxxx.");
    private static readonly Carimbo Bolha = new(
        ".AA.",
        "AWAa",
        "AAaa",
        ".aa.");
    private static readonly Carimbo BolhaP = new(
        ".A.",
        "AWa",
        ".a.");
    private static readonly Carimbo Brilho = new(
        "..2..",
        "..2..",
        "22W22",
        "..2..",
        "..2..");
    private static readonly Carimbo BrilhoP = new(
        ".2.",
        "2W2",
        ".2.");
    private static readonly Carimbo Estrela = new(
        "..2..",
        ".222.",
        "22222",
        ".232.",
        ".3.3.");
    private static readonly Carimbo Coracao = new(
        "11.11",
        "1W11f",
        ".11f.",
        "..f..");
    private static readonly Carimbo Poeira = new(
        ".WWW.",
        "WWWWW",
        "WWWWx",
        ".xxx.");
    private static readonly Carimbo PoeiraP = new(
        ".W.",
        "WWx",
        ".x.");
    private static readonly Carimbo Gota = new(
        ".A.",
        "AAa",
        ".a.");
    private static readonly Carimbo GotaP = new(
        "A.",
        "Aa");

    // Gotas de suor (paranoia): a grande é a mesma da têmpora da cara paranoica; a pequena, a ponta e um corpo de 3.
    private static readonly Carimbo SuorG = BonecoPixel.GotaDeSuor;
    private static readonly Carimbo SuorP = new(
        ".A.",
        "AWa",
        "Aaa");

    /// <summary>Um losango de cor, para o viajando.</summary>
    private static Carimbo Losango(char letra) => new(
        $"..{letra}..",
        $".{letra}{letra}{letra}.",
        $"{letra}{letra}W{letra}{letra}",
        $".{letra}{letra}{letra}.",
        $"..{letra}..");

    private static readonly Carimbo Rosa = Losango('R'), Lilas = Losango('V'), Verde = Losango('N'), Azul = Losango('a');

    /// <summary>
    /// Os pixels do rosto que as sobreposições nunca cobrem, na vista da pose e com a cara da própria pose, em
    /// pixels do quadro sem espelho: os carimbos dos olhos, das sobrancelhas, do nariz, do rubor e da boca. É uma
    /// máscara só dos traços, não um retângulo: assim a fumaça e as bolhas podem sair do canto da boca (revisão da
    /// arte, achado 7).
    /// </summary>
    public static IReadOnlySet<(int X, int Y)> AreaDoRosto(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return AreaDoRosto(pose, pose.Expressao);
    }

    /// <summary>
    /// A área do rosto com a cara <paramref name="expressao"/>: a de sempre e, na cara com gota de suor
    /// (<see cref="Rosto.Gota"/>, a paranoica), também a gota com o contorno dela. Para as outras caras, é a mesma
    /// área em qualquer cara.
    /// </summary>
    public static IReadOnlySet<(int X, int Y)> AreaDoRosto(PosePixel pose, string expressao)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(expressao);
        bool gota = Rostos.Expressoes.TryGetValue(expressao, out Rosto? rosto) && rosto.Gota;
        return AreaDoRosto(BonecoPixel.Pontos(pose), pose.Vista, gota);
    }

    private static HashSet<(int X, int Y)> AreaDoRosto(PontosDoEsqueleto p, Vista vista, bool gota)
    {
        // Os carimbos do rosto partem do centro arredondado da cabeça (BonecoPixel.DesenharCabeca...).
        int ex = (int)Math.Round(p.Cabeca.X), ey = (int)Math.Round(p.Cabeca.Y);
        var area = new HashSet<(int X, int Y)>();
        void Retangulo(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    area.Add((x, y));
        }
        if (vista == Vista.Frente)
        {
            Retangulo(ex - 9, ey - 4, ex - 3, ey + 3);   // olho da esquerda (7 × 8)
            Retangulo(ex + 2, ey - 4, ex + 8, ey + 3);   // olho da direita
            Retangulo(ex - 8, ey - 7, ex + 6, ey - 5);   // sobrancelhas (15 × 3)
            Retangulo(ex - 9, ey + 4, ex + 8, ey + 5);   // nariz e bochechas (o rubor pequeno e o grande)
            Retangulo(ex - 5, ey + 6, ex + 3, ey + 9);   // boca (9 × 4)
        }
        else
        {
            Retangulo(ex + 3, ey - 8, ex + 7, ey + 2);   // sobrancelha e olho (5 × 8)
            Retangulo(ex + 3, ey + 3, ex + 5, ey + 4);   // rubor
            Retangulo(ex + 11, ey + 2, ex + 12, ey + 3); // nariz
            // Boca (5 × 4) com 1 pixel em volta: de perfil, a boca aberta passa do focinho e ganha contorno.
            Retangulo(ex + 6, ey + 4, ex + 12, ey + 9);
        }
        if (gota)
        {
            // A gota de suor da cara paranoica, com o contorno em volta.
            (int gx, int gy) = BonecoPixel.CantoDaGota(vista, ex, ey);
            Retangulo(gx - 1, gy - 1, gx + BonecoPixel.GotaDeSuor.Largura, gy + BonecoPixel.GotaDeSuor.Altura);
        }
        return area;
    }

    /// <summary>
    /// Desenha a sobreposição na <paramref name="fase"/> (qualquer inteiro; vale o resto por
    /// <see cref="Fases"/>). <see cref="EfeitoVisual.Nenhum"/> não desenha nada. Com <paramref name="cipo"/>
    /// (a pose pendurada no cipó), nenhum carimbo cobre o cipó, as folhas ou a mão que o segura (a gota de suor que
    /// cairia ali salta do outro lado da cabeça). Com <paramref name="gota"/> (a cara tem gota de suor), nenhum cobre a gota.
    /// </summary>
    internal static void Desenhar(Tela tela, PontosDoEsqueleto pontos, Vista vista, EfeitoVisual efeito, int fase, bool cipo = false, bool gota = false)
    {
        if (efeito == EfeitoVisual.Nenhum) return;
        int f = (fase % Fases + Fases) % Fases;
        HashSet<(int X, int Y)> rosto = AreaDoRosto(pontos, vista, gota);
        int ex = (int)Math.Round(pontos.Cabeca.X), ey = (int)Math.Round(pontos.Cabeca.Y);
        (double X, double Y)? maoNoCipo = cipo ? pontos.MaoB : null;
        foreach ((Carimbo c, double x, double y) in Carimbos(efeito, f, ex, ey, pontos, vista))
        {
            // Pendurado no cipó, a gota de suor que cairia sobre ele ou sobre a mão que o segura salta do outro lado da
            // cabeça, no espelho da posição dela: o suor continua com 2 ou 3 gotas (revisão da paranoia, achado 6). Nos
            // outros efeitos, o carimbo só some, como antes.
            if (!Carimbar(tela, c, x, y, rosto, maoNoCipo) && efeito == EfeitoVisual.Suor)
                Carimbar(tela, c, 2 * ex - x - 1, y, rosto, maoNoCipo);
        }
    }

    /// <summary>Raio, em pixels, em volta do centro da mão que segura o cipó, onde nenhum carimbo entra.</summary>
    private const double RaioDaMaoNoCipo = 4;

    /// <summary>Os carimbos de cada efeito e fase, com o centro de cada um.</summary>
    private static IEnumerable<(Carimbo Carimbo, double X, double Y)> Carimbos(EfeitoVisual efeito, int fase, int ex, int ey, PontosDoEsqueleto p, Vista vista)
    {
        bool frente = vista == Vista.Frente;
        switch (efeito)
        {
            case EfeitoVisual.Fumaca:
                // Fumaça que sai da boca e sobe, crescendo: de frente, do canto da boca para a direita, por
                // fora da bochecha (do lado da orelha, lia como vapor de raiva); de perfil, da boca para a frente.
                (Carimbo, double, double)[][] fumaca = frente
                    ?
                    [
                        [(FumacaP, ex + 7, ey + 8), (FumacaM, ex + 13, ey + 9)],
                        [(FumacaP, ex + 7, ey + 9), (FumacaM, ex + 14, ey + 7), (FumacaG, ex + 18, ey + 1)],
                        [(FumacaM, ex + 15, ey + 9), (FumacaG, ex + 19, ey + 2), (FumacaM, ex + 20, ey - 6)],
                    ]
                    :
                    [
                        [(FumacaP, ex + 12, ey + 7), (FumacaM, ex + 15, ey + 1)],
                        [(FumacaM, ex + 13, ey + 5), (FumacaG, ex + 17, ey - 3)],
                        [(FumacaP, ex + 12, ey + 8), (FumacaG, ex + 16, ey + 1), (FumacaM, ex + 19, ey - 7)],
                    ];
                return fumaca[fase];
            case EfeitoVisual.Bolhas:
                // Bolhas de soluço que saem da boca e sobem: de frente, do canto da boca (do lado da orelha,
                // liam como gotas de suor); de perfil, da boca para a frente.
                (Carimbo, double, double)[][] bolhas = frente
                    ?
                    [
                        [(BolhaP, ex + 7, ey + 8), (Bolha, ex + 13, ey + 7)],
                        [(BolhaP, ex + 7, ey + 9), (Bolha, ex + 12, ey + 9), (BolhaP, ex + 17, ey + 4)],
                        [(Bolha, ex + 14, ey + 8), (BolhaP, ex + 18, ey + 2), (Bolha, ex + 19, ey - 5)],
                    ]
                    :
                    [
                        [(Bolha, ex + 13, ey + 6), (BolhaP, ex + 16, ey - 1)],
                        [(BolhaP, ex + 12, ey + 8), (Bolha, ex + 15, ey + 1), (BolhaP, ex + 18, ey - 6)],
                        [(Bolha, ex + 14, ey + 4), (BolhaP, ex + 17, ey - 4)],
                    ];
                return bolhas[fase];
            case EfeitoVisual.Brilhos:
                (Carimbo, double, double)[][] brilhos =
                [
                    [(Brilho, ex - 17, ey + 3), (BrilhoP, ex + 18, ey - 5)],
                    [(Brilho, ex + 18, ey - 5), (BrilhoP, ex - 16, ey - 9)],
                    [(Brilho, ex - 16, ey - 9), (BrilhoP, ex + 17, ey + 4)],
                ];
                return brilhos[fase];
            case EfeitoVisual.Estrelinhas:
                // Estrelinhas girando em volta da cabeça, na altura da aba do chapéu.
                (Carimbo, double, double)[][] estrelas =
                [
                    [(Estrela, ex - 18, ey - 7), (Estrela, ex + 17, ey - 3)],
                    [(Estrela, ex - 15, ey - 2), (Estrela, ex + 18, ey - 8)],
                    [(Estrela, ex - 18, ey - 3), (Estrela, ex + 15, ey - 1)],
                ];
                return estrelas[fase];
            case EfeitoVisual.Coracoes:
                (Carimbo, double, double)[][] coracoes =
                [
                    [(Coracao, ex - 17, ey - 2), (Coracao, ex + 17, ey - 8)],
                    [(Coracao, ex + 18, ey + 1), (Coracao, ex - 16, ey - 9)],
                    [(Coracao, ex - 18, ey + 3), (Coracao, ex + 16, ey - 12)],
                ];
                return coracoes[fase];
            case EfeitoVisual.Cores:
                (Carimbo, double, double)[][] cores =
                [
                    [(Rosa, ex - 16, ey - 8), (Lilas, ex + 16, ey - 8), (Verde, ex - 17, ey + 3)],
                    [(Verde, ex - 17, ey + 1), (Lilas, ex + 17, ey + 1), (Rosa, ex + 15, ey - 11), (Azul, ex - 15, ey - 10)],
                    [(Rosa, ex + 15, ey - 12), (Lilas, ex - 15, ey - 12), (Verde, ex + 17, ey + 5)],
                ];
                return cores[fase];
            case EfeitoVisual.Poeira:
                // Nuvenzinhas que saem do nariz, subindo: de frente, dos dois lados; de perfil, à frente do
                // focinho (atrás da cabeça, liam como saindo da nuca).
                (Carimbo, double, double)[][] poeira = frente
                    ?
                    [
                        [(Poeira, ex - 13, ey + 6), (PoeiraP, ex + 12, ey + 7)],
                        [(PoeiraP, ex - 13, ey + 3), (Poeira, ex + 13, ey + 4)],
                        [(Poeira, ex - 14, ey + 1), (PoeiraP, ex + 13, ey + 1)],
                    ]
                    :
                    [
                        [(Poeira, ex + 16, ey + 3), (PoeiraP, ex + 15, ey + 7)],
                        [(PoeiraP, ex + 16, ey + 1), (Poeira, ex + 17, ey + 5)],
                        [(Poeira, ex + 17, ey), (PoeiraP, ex + 18, ey + 5)],
                    ];
                return poeira[fase];
            case EfeitoVisual.Borrifo:
                // Gotinhas do frasco (mão A, a válvula 9 pixels acima) para o lenço (mão B).
                (double X, double Y) de = (p.MaoA.X + 2, p.MaoA.Y - 9), ate = (p.MaoB.X - 3, p.MaoB.Y - 5);
                (double X, double Y) Em(double k) => (de.X + (ate.X - de.X) * k, de.Y + (ate.Y - de.Y) * k);
                double[][] trechos = [[0.3, 0.7], [0.5, 0.15], [0.85, 0.45]];
                return trechos[fase].Select((k, i) => (i == 0 ? Gota : GotaP, Em(k).X, Em(k).Y));
            case EfeitoVisual.Suor:
                // Gotas de suor saltando da cabeça (paranoia): de frente, da têmpora da esquerda, para fora, e por cima
                // da ponta direita da aba, longe do braço que aponta para o teto e da gota da têmpora; de perfil, da
                // nuca e por cima da aba, à frente. Cada gota sobe e cai num arco, e na fase 2 uma nova sai da têmpora.
                // A da direita cai sobre a ponta da aba, e não para fora dela, onde pousava no dedo que aponta para o
                // teto (revisão da paranoia, achado 3).
                (Carimbo, double, double)[][] suor = frente
                    ?
                    [
                        [(SuorG, ex - 17, ey - 6), (SuorG, ex + 12, ey - 15)],
                        [(SuorP, ex - 19, ey - 10), (SuorG, ex + 14, ey - 16)],
                        [(SuorP, ex - 21, ey - 5), (SuorP, ex + 16, ey - 15), (SuorG, ex - 17, ey - 1)],
                    ]
                    :
                    [
                        [(SuorG, ex - 15, ey - 5), (SuorG, ex + 13, ey - 15)],
                        [(SuorP, ex - 18, ey - 9), (SuorG, ex + 15, ey - 16)],
                        [(SuorP, ex - 20, ey - 4), (SuorP, ex + 18, ey - 13), (SuorG, ex - 14, ey - 1)],
                    ];
                return suor[fase];
            default:
                throw new ArgumentOutOfRangeException(nameof(efeito), efeito, "Efeito desconhecido.");
        }
    }

    /// <summary>
    /// Carimba centrado em (<paramref name="cx"/>, <paramref name="cy"/>), puxado para dentro do quadro,
    /// sem tocar a área do rosto, com uma linha de contorno onde passa por cima do que já está desenhado.
    /// Pendurado no cipó (<paramref name="maoNoCipo"/> é o centro da mão que o segura), um carimbo que
    /// cairia sobre o cipó, as folhas ou essa mão (ou encostado neles) não é desenhado: a mão agarrada ao
    /// cipó fica sempre à vista (revisão da arte, achado 7). Devolve falso só nesse caso.
    /// </summary>
    private static bool Carimbar(Tela tela, Carimbo c, double cx, double cy, HashSet<(int X, int Y)> rosto, (double X, double Y)? maoNoCipo)
    {
        int lado = tela.Largura;
        int x0 = Math.Clamp((int)Math.Floor(cx - c.Largura / 2.0 + 0.5), Margem, lado - Margem - c.Largura);
        int y0 = Math.Clamp((int)Math.Floor(cy - c.Altura / 2.0 + 0.5), Margem, tela.Altura - Margem - c.Altura);
        if (maoNoCipo is { } mao)
        {
            for (int y = y0 - 1; y <= y0 + c.Altura; y++)
            {
                for (int x = x0 - 1; x <= x0 + c.Largura; x++)
                {
                    bool naMao = (x + 0.5 - mao.X) * (x + 0.5 - mao.X) + (y + 0.5 - mao.Y) * (y + 0.5 - mao.Y) <= RaioDaMaoNoCipo * RaioDaMaoNoCipo;
                    if (naMao || tela[x, y] is Cor.Cipo or Cor.CipoEscuro or Cor.CipoClaro or Cor.Folha or Cor.FolhaEscura) return false;
                }
            }
        }
        bool NoRosto(int x, int y) => rosto.Contains((x, y));
        // O carimbo guarda 1 pixel de folga do rosto: o contorno dele não entra na área protegida.
        bool PertoDoRosto(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (rosto.Contains((x + dx, y + dy))) return true;
            return false;
        }
        bool NoCarimbo(int x, int y) => c[x - x0, y - y0] is not null && !PertoDoRosto(x, y);
        var linha = new List<(int X, int Y)>();
        for (int y = y0 - 1; y <= y0 + c.Altura; y++)
            for (int x = x0 - 1; x <= x0 + c.Largura; x++)
                if (!NoCarimbo(x, y) && !NoRosto(x, y) && tela.Opaco(x, y) && (NoCarimbo(x - 1, y) || NoCarimbo(x + 1, y) || NoCarimbo(x, y - 1) || NoCarimbo(x, y + 1)))
                    linha.Add((x, y));
        foreach ((int x, int y) in linha) tela[x, y] = Cor.Contorno;
        for (int y = y0; y < y0 + c.Altura; y++)
            for (int x = x0; x < x0 + c.Largura; x++)
                if (NoCarimbo(x, y)) tela[x, y] = c[x - x0, y - y0]!.Value;
        return true;
    }

    /// <summary>
    /// O modificador de pose do efeito, sem pose nova: o bêbado balança o tronco e a cabeça, o chapado
    /// abaixa a cabeça, o tonto a gira, o elétrico treme de lado e o apaixonado balança, os dois com a cauda erguida,
    /// quem viaja balança a cabeça e o paranoico dá um tremidinho de 1 pixel, sem mexer na cauda. Vale no chão: parado,
    /// andando, descansando e nos gestos; na parede, no cipó (que já balança), no ar, segurado, no esconderijo, no
    /// espiar e nas poses de uso, a pose fica como está. <see cref="EfeitoVisual.Poeira"/> e
    /// <see cref="EfeitoVisual.Borrifo"/> não mudam a pose.
    /// </summary>
    public static PosePixel Modificar(PosePixel pose, EfeitoVisual efeito, int fase)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!Modificavel(pose)) return pose;
        int f = (fase % Fases + Fases) % Fases;
        Cauda alta = pose.Vista == Vista.Frente ? Cauda.Alta : Cauda.PerfilAlta;
        // Cada balanço passa um terço do tempo de cada lado e um no meio (revisão da arte, achado 10: com
        // +6, −6, +6, o bêbado ficava dois terços do tempo do mesmo lado, mancando). A zonzeira do tonto
        // dá uma volta (esquerda, direita, cabeça baixa) sem saltar mais de 10° de uma fase para a outra.
        return efeito switch
        {
            EfeitoVisual.Bolhas => pose with { Tronco = pose.Tronco + (1 - f) * 6, Cabeca = pose.Cabeca + (1 - f) * 4 },
            EfeitoVisual.Fumaca => pose with { CabecaDescida = pose.CabecaDescida + 1.5 },
            EfeitoVisual.Estrelinhas => pose with { Cabeca = pose.Cabeca + f switch { 0 => -5, 1 => 5, _ => 0 }, CabecaDescida = pose.CabecaDescida + (f == 2 ? 1 : 0) },
            EfeitoVisual.Brilhos => pose with { QuadrilX = pose.QuadrilX + f switch { 0 => 0, 1 => 1, _ => -1 }, Cauda = alta },
            EfeitoVisual.Coracoes => pose with { Tronco = pose.Tronco + f switch { 0 => -3, 1 => 0, _ => 3 }, Cauda = alta },
            EfeitoVisual.Cores => pose with { Cabeca = pose.Cabeca + f switch { 0 => -4, 1 => 4, _ => 0 } },
            // Paranoia: o corpo inteiro treme 1 pixel de lado (a fase 0, a parada, fica no lugar).
            EfeitoVisual.Suor => pose with { QuadrilX = pose.QuadrilX + f switch { 0 => 0, 1 => -1, _ => 1 } },
            _ => pose,
        };
    }

    /// <summary>
    /// Se os modificadores valem na pose: no chão, parado, andando, descansando e nos gestos, fora do
    /// esconderijo, do espiar e do cipó; nunca nas poses de uso.
    /// </summary>
    public static bool Modificavel(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        bool noChao = pose.Estado is "IDLE" or "WALKING" or "RESTING" || pose.Estado.StartsWith("gesto:", StringComparison.Ordinal);
        return noChao && pose.Borda is null && pose.Cipo is null && pose.Segura == Segura.Nada && !UsosPixel.EhDeUso(pose);
    }
}
