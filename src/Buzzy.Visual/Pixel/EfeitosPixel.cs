namespace Buzzy.Visual.Pixel;

// Sobreposições de desenho animado por cima do boneco.
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

    // Gotas saltando da cabeça (paranoia).
    Suor,
}

// Carimbos pequenos em volta da cabeça (ou entre as mãos, no borrifo), desenhados depois do corpo
// e antes do contorno. Nunca cobrem olhos, sobrancelhas, nariz, rubor e boca, e ficam a 2 px da
// borda pro contorno caber. A fase 0 é a parada (sem relógio fica nela); com relógio, troca a cada
// 12 passos.
// Onda -> efeito: Bebado = Bolhas, Chapado = Fumaca, Eletrico = Brilhos, Tonto = Estrelinhas,
// Euforico = Coracoes, Viajando = Cores, Paranoico = Suor; Satisfeito, Alegre, Relaxado e Ligado,
// nenhum. Poeira e Borrifo são das poses de uso (cheirar, inalar); a poeira também serve pro
// espirro e a fumaça pra tosse.
public static class EfeitosPixel
{
    public const int Fases = 3;

    // Distância mínima à borda do quadro; o contorno fica a 1 px.
    private const int Margem = 2;

    // Só os que desenham algo, na ordem do enum.
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

    // A gota grande é a mesma da têmpora da cara paranoica.
    private static readonly Carimbo SuorG = BonecoPixel.GotaDeSuor;
    private static readonly Carimbo SuorP = new(
        ".A.",
        "AWa",
        "Aaa");

    // Pro viajando.
    private static Carimbo Losango(char letra) => new(
        $"..{letra}..",
        $".{letra}{letra}{letra}.",
        $"{letra}{letra}W{letra}{letra}",
        $".{letra}{letra}{letra}.",
        $"..{letra}..");

    private static readonly Carimbo Rosa = Losango('R'), Lilas = Losango('V'), Verde = Losango('N'), Azul = Losango('a');

    // Pixels que os efeitos nunca cobrem (olhos, sobrancelhas, nariz, rubor, boca), no quadro sem
    // espelho. É máscara dos traços e não um retângulo, pra fumaça e bolhas poderem sair do canto da boca.
    public static IReadOnlySet<(int X, int Y)> AreaDoRosto(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return AreaDoRosto(pose, pose.Expressao);
    }

    // Só muda pra cara com gota de suor, que protege também a gota e o contorno dela.
    public static IReadOnlySet<(int X, int Y)> AreaDoRosto(PosePixel pose, string expressao)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(expressao);
        bool gota = Rostos.Expressoes.TryGetValue(expressao, out Rosto? rosto) && rosto.Gota;
        return AreaDoRosto(BonecoPixel.Pontos(pose), pose.Vista, gota);
    }

    private static HashSet<(int X, int Y)> AreaDoRosto(PontosDoEsqueleto p, Vista vista, bool gota)
    {
        // Mesma origem dos carimbos do rosto em BonecoPixel: centro arredondado da cabeça.
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
            Retangulo(ex - 9, ey - 4, ex - 3, ey + 3);   // olho esquerdo
            Retangulo(ex + 2, ey - 4, ex + 8, ey + 3);   // olho direito
            Retangulo(ex - 8, ey - 7, ex + 6, ey - 5);   // sobrancelhas
            Retangulo(ex - 9, ey + 4, ex + 8, ey + 5);   // nariz e rubor (pequeno e grande)
            Retangulo(ex - 5, ey + 6, ex + 3, ey + 9);   // boca
        }
        else
        {
            Retangulo(ex + 3, ey - 8, ex + 7, ey + 2);   // sobrancelha e olho
            Retangulo(ex + 3, ey + 3, ex + 5, ey + 4);   // rubor
            Retangulo(ex + 11, ey + 2, ex + 12, ey + 3); // nariz
            // Boca com 1 px de folga: de perfil, a boca aberta passa do focinho e ganha contorno.
            Retangulo(ex + 6, ey + 4, ex + 12, ey + 9);
        }
        if (gota)
        {
            (int gx, int gy) = BonecoPixel.CantoDaGota(vista, ex, ey);
            Retangulo(gx - 1, gy - 1, gx + BonecoPixel.GotaDeSuor.Largura, gy + BonecoPixel.GotaDeSuor.Altura);
        }
        return area;
    }

    // fase pode ser qualquer inteiro (usa o resto). No cipó, nada cobre o cipó, as folhas ou a mão
    // que segura. Com gota, nada cobre a gota de suor do rosto.
    internal static void Desenhar(Tela tela, PontosDoEsqueleto pontos, Vista vista, EfeitoVisual efeito, int fase, bool cipo = false, bool gota = false)
    {
        if (efeito == EfeitoVisual.Nenhum) return;
        int f = (fase % Fases + Fases) % Fases;
        HashSet<(int X, int Y)> rosto = AreaDoRosto(pontos, vista, gota);
        int ex = (int)Math.Round(pontos.Cabeca.X), ey = (int)Math.Round(pontos.Cabeca.Y);
        (double X, double Y)? maoNoCipo = cipo ? pontos.MaoB : null;
        foreach ((Carimbo c, double x, double y) in Carimbos(efeito, f, ex, ey, pontos, vista))
        {
            // No cipó, a gota de suor que cairia em cima dele vai pro lado espelhado da cabeça, pra
            // continuar com 2 ou 3 gotas. Nos outros efeitos o carimbo só some.
            if (!Carimbar(tela, c, x, y, rosto, maoNoCipo) && efeito == EfeitoVisual.Suor)
                Carimbar(tela, c, 2 * ex - x - 1, y, rosto, maoNoCipo);
        }
    }

    // Px em volta do centro da mão no cipó onde nenhum carimbo entra.
    private const double RaioDaMaoNoCipo = 4;

    // X, Y = centro de cada carimbo.
    private static IEnumerable<(Carimbo Carimbo, double X, double Y)> Carimbos(EfeitoVisual efeito, int fase, int ex, int ey, PontosDoEsqueleto p, Vista vista)
    {
        bool frente = vista == Vista.Frente;
        switch (efeito)
        {
            case EfeitoVisual.Fumaca:
                // Sai da boca e sobe crescendo. De frente, pela direita por fora da bochecha (perto da
                // orelha parecia vapor de raiva); de perfil, pra frente.
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
                // Bolhas de soluço saindo do canto da boca (perto da orelha pareciam suor).
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
                // Girando na altura da aba do chapéu.
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
                // Nuvenzinhas subindo do nariz. De perfil, na frente do focinho (atrás parecia sair da nuca).
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
                // Do frasco (mão A, válvula 9 px acima) pro lenço (mão B).
                (double X, double Y) de = (p.MaoA.X + 2, p.MaoA.Y - 9), ate = (p.MaoB.X - 3, p.MaoB.Y - 5);
                (double X, double Y) Em(double k) => (de.X + (ate.X - de.X) * k, de.Y + (ate.Y - de.Y) * k);
                double[][] trechos = [[0.3, 0.7], [0.5, 0.15], [0.85, 0.45]];
                return trechos[fase].Select((k, i) => (i == 0 ? Gota : GotaP, Em(k).X, Em(k).Y));
            case EfeitoVisual.Suor:
                // De frente: da têmpora esquerda pra fora e por cima da ponta direita da aba (longe do
                // dedo que aponta pro teto; mais pra fora ela pousava nele). De perfil: da nuca e por cima
                // da aba. Cada gota faz um arco; na fase 2 sai uma nova da têmpora.
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

    // Centrado em (cx, cy), puxado pra dentro do quadro, fora do rosto, com contorno onde passa por
    // cima do desenho. No cipó, se encostar no cipó, nas folhas ou na mão, não desenha e devolve
    // falso: a mão agarrada tem que ficar à vista.
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
        // 1 px de folga do rosto pro contorno não invadir a área protegida.
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

    // Mexe na pose sem criar pose nova: bêbado balança tronco e cabeça, chapado baixa a cabeça, tonto
    // gira, elétrico treme de lado e apaixonado balança (os dois com a cauda erguida), viajando balança
    // a cabeça, paranoico treme 1 px. Só vale no chão (ver Modificavel).
    public static PosePixel Modificar(PosePixel pose, EfeitoVisual efeito, int fase)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!Modificavel(pose)) return pose;
        int f = (fase % Fases + Fases) % Fases;
        Cauda alta = pose.Vista == Vista.Frente ? Cauda.Alta : Cauda.PerfilAlta;
        // Balanço: um terço de cada lado e um no meio (com +6, -6, +6 o bêbado parecia mancar).
        // O tonto dá a volta (esquerda, direita, cabeça baixa) sem pular mais de 10° por fase.
        return efeito switch
        {
            EfeitoVisual.Bolhas => pose with { Tronco = pose.Tronco + (1 - f) * 6, Cabeca = pose.Cabeca + (1 - f) * 4 },
            EfeitoVisual.Fumaca => pose with { CabecaDescida = pose.CabecaDescida + 1.5 },
            EfeitoVisual.Estrelinhas => pose with { Cabeca = pose.Cabeca + f switch { 0 => -5, 1 => 5, _ => 0 }, CabecaDescida = pose.CabecaDescida + (f == 2 ? 1 : 0) },
            EfeitoVisual.Brilhos => pose with { QuadrilX = pose.QuadrilX + f switch { 0 => 0, 1 => 1, _ => -1 }, Cauda = alta },
            EfeitoVisual.Coracoes => pose with { Tronco = pose.Tronco + f switch { 0 => -3, 1 => 0, _ => 3 }, Cauda = alta },
            EfeitoVisual.Cores => pose with { Cabeca = pose.Cabeca + f switch { 0 => -4, 1 => 4, _ => 0 } },
            // Treme 1 px de lado; a fase 0 fica no lugar.
            EfeitoVisual.Suor => pose with { QuadrilX = pose.QuadrilX + f switch { 0 => 0, 1 => -1, _ => 1 } },
            _ => pose,
        };
    }

    // No chão (parado, andando, descansando, gestos), fora do esconderijo, do espiar e do cipó (que já
    // balança). Nunca nas poses de uso.
    public static bool Modificavel(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        bool noChao = pose.Estado is "IDLE" or "WALKING" or "RESTING" || pose.Estado.StartsWith("gesto:", StringComparison.Ordinal);
        return noChao && pose.Borda is null && pose.Cipo is null && pose.Segura == Segura.Nada && !UsosPixel.EhDeUso(pose);
    }
}
