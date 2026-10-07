namespace Buzzy.Visual.Pixel;

// O perfil olha pra direita; a esquerda é o espelho.
public enum Vista
{
    Frente,
    Perfil,
}

public enum Mao
{
    Aberta,
    Fechada,

    // Punho com o indicador pra cima (a paranoia aponta pro teto).
    Apontando,
}

public enum Cauda
{
    Espiral,
    Alta,
    Caida,
    Perfil,
    PerfilAlta,
    PerfilCaida,
}

// Graus absolutos na tela: 0 = baixo, 90 = direita, -90 = esquerda, 180 = cima.
// Superior = braço/coxa; Inferior = antebraço/canela.
public readonly record struct Membro(double Superior, double Inferior);

// O centro da mão cai na Pega do carimbo do item e a palma é desenhada por cima.
// VarianteDoItem diz se está em pé, no gole, mordido etc.
public enum Segura
{
    Nada,

    // Inclusive o espelhinho, que antes ia entre as duas mãos.
    MaoB,

    // Lança-perfume: "frasco" na mão A, "lenco" na B.
    Inalar,
}

// O braço que segura dobra até a Ponta do item cair no pixel da boca ou do nariz, seja qual for
// o tamanho do item.
public enum PontaNoRosto
{
    Nenhuma,

    // Beber, comer, tragar, engolir.
    Boca,

    // Cheirar, inalar.
    Nariz,
}

// X, Y = canto de cima à esquerda, no quadro 64x64 sem espelho.
public readonly record struct ItemColocado(ItemNaMao Item, int X, int Y)
{
    // Centro do pixel da ponta; nulo se a variante não tem ponta.
    public (double X, double Y)? Ponta => Item.Ponta is { } p ? (X + p.X + 0.5, Y + p.Y + 0.5) : null;
}

// Quadro 64x64 sem espelho; o pixel (x, y) tem centro em (x + 0,5; y + 0,5). Mãos = centro da
// palma, cabeça = centro do círculo, boca/nariz = centro dos carimbos. Espelhado: x vira 64 - x.
public readonly record struct PontosDoEsqueleto(
    (double X, double Y) MaoA,
    (double X, double Y) MaoB,
    (double X, double Y) Cabeca,
    (double X, double Y) Boca,
    (double X, double Y) Nariz);

// Grade nativa 64x64 (1 px = 2 DIP a 100%), com o quadril como raiz. De frente, A é a esquerda
// da tela e B a direita; de perfil, A é o lado de trás (mais escuro) e B o da frente.
public sealed record PosePixel
{
    public required string Nome { get; init; }

    public string Estado { get; init; } = "";

    public Vista Vista { get; init; } = Vista.Frente;

    public double QuadrilX { get; init; } = 32;

    public double QuadrilY { get; init; } = 48.5;

    // Graus; positivo leva o pescoço pra direita.
    public double Tronco { get; init; }

    // Relativa ao tronco.
    public double Cabeca { get; init; }

    // Pixels que a cabeça afunda nos ombros (encolher, pendurar).
    public double CabecaDescida { get; init; }

    public Membro BracoA { get; init; } = new(-10, -4);

    public Membro BracoB { get; init; } = new(10, 4);

    public Membro PernaA { get; init; } = new(-5, -2);

    public Membro PernaB { get; init; } = new(5, 2);

    public Mao MaoA { get; init; } = Mao.Aberta;

    public Mao MaoB { get; init; } = Mao.Aberta;

    public Cauda Cauda { get; init; } = Cauda.Espiral;

    public string Expressao { get; init; } = "neutro";

    // De frente, pés virados pra fora; de perfil, pra frente. Sem pé, a canela termina redonda.
    public bool PesNoChao { get; init; } = true;

    // Linha (em px do quadro) atrás da qual o corpo se esconde, como ao espiar: o que está abaixo
    // some, e as mãos que agarram a borda ficam na frente da cabeça.
    public double? Borda { get; init; }

    // Se houver, um cipó desce do topo até a mão B. O valor é o deslocamento horizontal (px) de
    // onde ele sai do topo em relação à mão; negativo inclina pra esquerda. Único desenho que
    // encosta na borda do quadro (a de cima, onde prende na borda da tela).
    public double? Cipo { get; init; }

    // Só nas poses de uso, de frente.
    public Segura Segura { get; init; } = Segura.Nada;

    // Nula ou inexistente no item vale a primeira. Itens do mesmo verbo têm as mesmas variantes.
    public string? VarianteDoItem { get; init; }

    public PontaNoRosto PontaNo { get; init; } = PontaNoRosto.Nenhuma;

    // Desenhado depois da cabeça, na frente do rosto.
    public bool BracoANaFrente { get; init; }

    public bool BracoBNaFrente { get; init; }

    // Ex.: a fumaça de quem solta a tragada.
    public EfeitoVisual EfeitoDaPose { get; init; } = EfeitoVisual.Nenhum;

    // 0 a EfeitosPixel.Fases - 1.
    public int FaseDoEfeito { get; init; }
}

// Formas simples sem meio-tom, sombra de 1 px embaixo e à direita, linhas internas entre partes
// sobrepostas, contorno escuro por fora e carimbos feitos à mão pro rosto.
public static class BonecoPixel
{
    public const int Lado = 64;

    // Esqueleto, em pixels nativos.
    private const double TroncoAltura = 18;
    private const double PescocoACabeca = 8.2;
    private const double RaioCabeca = 11.2;
    private const double BracoSuperior = 10.8;
    private const double Antebraco = 9.6;
    private const double Coxa = 5.6;
    private const double Canela = 5.2;

    // Do centro do punho até a ponta do indicador, na Mao.Apontando.
    private const double PontaDoDedo = 5.6;

    // expressao nula = cara da pose. O item só aparece em poses que seguram, de frente, com o braço
    // ajustado a ele. O efeito vai por cima do da própria pose (fase 0 = parado); aplicar
    // EfeitosPixel.Modificar na pose é com quem chama.
    public static Tela Desenhar(PosePixel pose, string? expressao = null, string? item = null, EfeitoVisual efeito = EfeitoVisual.Nenhum, int fase = 0)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Rosto rosto = Rostos.Expressoes[expressao ?? pose.Expressao];
        pose = AjustadaAoItem(pose, item);
        var tela = new Tela(Lado, Lado);
        var e = new Esqueleto(pose);

        // O cipó vem antes do corpo: a mão que o segura fica por cima dele.
        if (pose.Cipo is { } deslocamento) DesenharCipo(tela, MaoDoBraco(e.OmbroB, pose.BracoB, pose.MaoB), deslocamento);

        if (pose.Vista == Vista.Frente)
        {
            DesenharCauda(tela, e, pose.Cauda, longe: false);
            DesenharPerna(tela, e.QuadrilA, pose.PernaA, pose, ladoDaTela: -1, longe: false);
            DesenharPerna(tela, e.QuadrilB, pose.PernaB, pose, ladoDaTela: 1, longe: false);
            DesenharTronco(tela, e, pose.Vista);
            if (pose.Borda is null)
            {
                // O item vem logo antes da palma, que cobre a pega; o braço que vai à boca ou ao
                // nariz é desenhado depois da cabeça, com o item na frente do rosto.
                ItensDaPose itens = Colocar(pose, e, item);
                if (!pose.BracoANaFrente) DesenharBraco(tela, e.OmbroA, pose.BracoA, pose.MaoA, longe: false, itens.MaoA);
                if (!pose.BracoBNaFrente) DesenharBraco(tela, e.OmbroB, pose.BracoB, pose.MaoB, longe: false, itens.MaoB);
                DesenharCabecaDeFrente(tela, e, rosto);
                if (pose.BracoANaFrente) DesenharBraco(tela, e.OmbroA, pose.BracoA, pose.MaoA, longe: false, itens.MaoA);
                if (pose.BracoBNaFrente) DesenharBraco(tela, e.OmbroB, pose.BracoB, pose.MaoB, longe: false, itens.MaoB);
            }
            else
            {
                DesenharCabecaDeFrente(tela, e, rosto);
                DesenharBraco(tela, e.OmbroA, pose.BracoA, pose.MaoA, longe: false);
                DesenharBraco(tela, e.OmbroB, pose.BracoB, pose.MaoB, longe: false);
            }
        }
        else
        {
            DesenharBraco(tela, e.OmbroA, pose.BracoA, pose.MaoA, longe: true);
            DesenharPerna(tela, e.QuadrilA, pose.PernaA, pose, ladoDaTela: 1, longe: true);
            DesenharCauda(tela, e, pose.Cauda, longe: false);
            DesenharTronco(tela, e, pose.Vista);
            DesenharPerna(tela, e.QuadrilB, pose.PernaB, pose, ladoDaTela: 1, longe: false);
            DesenharBraco(tela, e.OmbroB, pose.BracoB, pose.MaoB, longe: false);
            DesenharCabecaDePerfil(tela, e, rosto);
        }

        // A gota de suor vai por cima dos braços: precisa ficar à vista (no agachar os antebraços
        // passam pelas têmporas).
        if (rosto.Gota) DesenharGota(tela, e, pose.Vista);

        // Efeitos (o da pose e o pedido) por cima do corpo, mas fora dos olhos, boca e gota; o
        // contorno final fecha.
        if (pose.EfeitoDaPose != EfeitoVisual.Nenhum || efeito != EfeitoVisual.Nenhum)
        {
            PontosDoEsqueleto pontos = Pontos(pose, e);
            bool cipo = pose.Cipo is not null;
            EfeitosPixel.Desenhar(tela, pontos, pose.Vista, pose.EfeitoDaPose, pose.FaseDoEfeito, cipo, rosto.Gota);
            EfeitosPixel.Desenhar(tela, pontos, pose.Vista, efeito, fase, cipo, rosto.Gota);
        }

        if (pose.Borda is { } borda)
        {
            // O que fica atrás da borda some; o contorno depois fecha a linha da borda.
            for (int y = (int)Math.Ceiling(borda); y < Lado; y++)
                for (int x = 0; x < Lado; x++)
                    tela[x, y] = Cor.Nada;
        }

        tela.Contornar(Cor.Contorno);
        return tela;
    }

    // Com item na mão, as mãos desenhadas são as de Pontos(AjustadaAoItem(pose, item)).
    public static PontosDoEsqueleto Pontos(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return Pontos(pose, new Esqueleto(pose));
    }

    private static PontosDoEsqueleto Pontos(PosePixel pose, Esqueleto e)
    {
        (double cx, double cy) = e.Cabeca;
        int ex = (int)Math.Round(cx), ey = (int)Math.Round(cy);
        // Os carimbos do rosto partem do centro arredondado; boca e nariz são o centro deles.
        bool frente = pose.Vista == Vista.Frente;
        return new(
            MaoDoBraco(e.OmbroA, pose.BracoA, pose.MaoA),
            MaoDoBraco(e.OmbroB, pose.BracoB, pose.MaoB),
            (cx, cy),
            frente ? (ex - 0.5, ey + 7.5) : (ex + 9.5, ey + 6),
            frente ? (ex, ey + 5) : (ex + 12, ey + 3));
    }

    // Dobra o braço B pra ponta do item cair na boca ou no nariz, seja qual for o tamanho do item.
    // Sem item, sem PontaNo ou com variante sem ponta, devolve a pose igual.
    public static PosePixel AjustadaAoItem(PosePixel pose, string? item)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (item is null || pose.Segura == Segura.Nada || pose.PontaNo == PontaNoRosto.Nenhuma || pose.Vista != Vista.Frente) return pose;
        (ItemNaMao? _, ItemNaMao? naMaoB) = Variantes(pose, item);
        if (naMaoB?.Ponta is not { } ponta) return pose;
        PontosDoEsqueleto pontos = Pontos(pose);
        (double X, double Y) alvo = pose.PontaNo == PontaNoRosto.Boca ? pontos.Boca : pontos.Nariz;
        (int X, int Y) pixel = ((int)Math.Floor(alvo.X), (int)Math.Floor(alvo.Y));
        // O centro da mão no pixel da pega, com a ponta no pixel do alvo.
        (double X, double Y) mao = (pixel.X + naMaoB.Pega.X - ponta.X + 0.5, pixel.Y + naMaoB.Pega.Y - ponta.Y + 0.5);
        return Alcancar(pose with { MaoB = Mao.Fechada }, bracoB: true, mao);
    }

    // Mão A e depois B, só os que existem; ajusta a pose ao item antes, como Desenhar.
    public static IReadOnlyList<ItemColocado> ItensColocados(PosePixel pose, string item)
    {
        ArgumentNullException.ThrowIfNull(pose);
        PosePixel ajustada = AjustadaAoItem(pose, item);
        ItensDaPose itens = Colocar(ajustada, new Esqueleto(ajustada), item);
        return [.. new[] { itens.MaoA, itens.MaoB }.Where(i => i is not null).Select(i => i!.Value)];
    }

    // IK de dois ossos. Das duas dobras possíveis, fica a com o cotovelo mais perto do que a pose já
    // tem. Fora do alcance, a mão vai o mais longe que der na mesma direção.
    public static PosePixel Alcancar(PosePixel pose, bool bracoB, (double X, double Y) mao)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var e = new Esqueleto(pose);
        (double X, double Y) ombro = bracoB ? e.OmbroB : e.OmbroA;
        double a = BracoSuperior, b = Antebraco + ((bracoB ? pose.MaoB : pose.MaoA) == Mao.Aberta ? 1.9 : 1.2);
        (double X, double Y) dica = Somar(ombro, Escalar(Direcao((bracoB ? pose.BracoB : pose.BracoA).Superior), a));
        double dx = mao.X - ombro.X, dy = mao.Y - ombro.Y;
        double d = Math.Clamp(Math.Sqrt(dx * dx + dy * dy), Math.Abs(a - b) + 1e-6, a + b - 1e-6);
        double rumo = Math.Atan2(dx, dy), abertura = Math.Acos(Math.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1));
        double Distancia(double sup) => Math.Pow(ombro.X + a * Math.Sin(sup) - dica.X, 2) + Math.Pow(ombro.Y + a * Math.Cos(sup) - dica.Y, 2);
        double superior = Distancia(rumo + abertura) <= Distancia(rumo - abertura) ? rumo + abertura : rumo - abertura;
        (double X, double Y) cotovelo = (ombro.X + a * Math.Sin(superior), ombro.Y + a * Math.Cos(superior));
        (double X, double Y) alvo = (ombro.X + d * Math.Sin(rumo), ombro.Y + d * Math.Cos(rumo));
        double inferior = Math.Atan2(alvo.X - cotovelo.X, alvo.Y - cotovelo.Y);
        var membro = new Membro(Graus(superior), Graus(inferior));
        return bracoB ? pose with { BracoB = membro } : pose with { BracoA = membro };
    }

    private static double Graus(double radianos)
    {
        double g = radianos * 180 / Math.PI;
        return g > 180 ? g - 360 : g <= -180 ? g + 360 : g;
    }

    private readonly record struct ItensDaPose(ItemColocado? MaoA, ItemColocado? MaoB);

    // A variante pedida só vale se o item tiver; senão, a primeira.
    private static (ItemNaMao? MaoA, ItemNaMao? MaoB) Variantes(PosePixel pose, string? item)
    {
        if (item is null || pose.Segura == Segura.Nada) return (null, null);
        IReadOnlyList<string> variantes = ItensPixel.VariantesNaMao(item);
        string variante = pose.VarianteDoItem is { } v && variantes.Contains(v) ? v : variantes[0];
        return pose.Segura switch
        {
            Segura.MaoB => (null, ItensPixel.NaMao(item, variante)),
            Segura.Inalar => (
                variantes.Contains("frasco") ? ItensPixel.NaMao(item, "frasco") : null,
                variantes.Contains("lenco") ? ItensPixel.NaMao(item, "lenco") : ItensPixel.NaMao(item, variante)),
            _ => throw new ArgumentOutOfRangeException(nameof(pose), pose.Segura, "Modo de segurar desconhecido."),
        };
    }

    // A pega de cada item no pixel do centro da mão.
    private static ItensDaPose Colocar(PosePixel pose, Esqueleto e, string? item)
    {
        (ItemNaMao? a, ItemNaMao? b) = Variantes(pose, item);
        return new(
            a is null ? null : NaPega(a, MaoDoBraco(e.OmbroA, pose.BracoA, pose.MaoA)),
            b is null ? null : NaPega(b, MaoDoBraco(e.OmbroB, pose.BracoB, pose.MaoB)));
    }

    private static ItemColocado NaPega(ItemNaMao item, (double X, double Y) centro)
        => new(item, (int)Math.Floor(centro.X) - item.Pega.X, (int)Math.Floor(centro.Y) - item.Pega.Y);

    // Com linha de contorno onde encosta no corpo já desenhado.
    private static void Carimbar(Tela tela, ItemColocado item) => tela.Carimbar(item.Item.Desenho, item.X, item.Y, linhaInterna: Cor.Contorno);

    // ------------------------------------------------------------------ esqueleto

    private sealed class Esqueleto
    {
        internal Esqueleto(PosePixel pose)
        {
            double t = pose.Tronco * Math.PI / 180;
            Cima = (Math.Sin(t), -Math.Cos(t));
            Direita = (Math.Cos(t), Math.Sin(t));
            Quadril = (pose.QuadrilX, pose.QuadrilY);
            Pescoco = Somar(Quadril, Escalar(Cima, TroncoAltura));
            double c = (pose.Tronco + pose.Cabeca) * Math.PI / 180;
            (double X, double Y) cimaDaCabeca = (Math.Sin(c), -Math.Cos(c));
            Cabeca = Somar(Pescoco, Escalar(cimaDaCabeca, PescocoACabeca - pose.CabecaDescida));
            bool frente = pose.Vista == Vista.Frente;
            double ombro = frente ? 5.5 : 1.2, quadril = frente ? 3.6 : 0.8;
            (double X, double Y) baseDoOmbro = Somar(Pescoco, Escalar(Cima, -3.2));
            OmbroA = Somar(baseDoOmbro, Escalar(Direita, -ombro));
            OmbroB = Somar(baseDoOmbro, Escalar(Direita, frente ? ombro : ombro + 0.6));
            QuadrilA = Somar(Quadril, Escalar(Direita, -quadril));
            QuadrilB = Somar(Quadril, Escalar(Direita, quadril));
        }

        internal (double X, double Y) Cima { get; }
        internal (double X, double Y) Direita { get; }
        internal (double X, double Y) Quadril { get; }
        internal (double X, double Y) Pescoco { get; }
        internal (double X, double Y) Cabeca { get; }
        internal (double X, double Y) OmbroA { get; }
        internal (double X, double Y) OmbroB { get; }
        internal (double X, double Y) QuadrilA { get; }
        internal (double X, double Y) QuadrilB { get; }

        // Referencial do tronco: x pra direita dele, y pra cima.
        internal (double X, double Y) NoTronco(double x, double y) => Somar(Somar(Quadril, Escalar(Direita, x)), Escalar(Cima, y));
    }

    // ------------------------------------------------------------------ partes

    private static Mascara Nova() => new(Lado, Lado);

    private static void DesenharTronco(Tela tela, Esqueleto e, Vista vista)
    {
        bool frente = vista == Vista.Frente;
        double graus = Math.Atan2(e.Direita.Y, e.Direita.X) * 180 / Math.PI;
        // Tronco em forma de pera, como nas pranchas: peito estreito e barriga mais larga.
        (double X, double Y) peito = e.NoTronco(frente ? 0 : 0.3, 13.2), ventre = e.NoTronco(0, 6.8);
        Mascara corpo = Nova()
            .Elipse(peito.X, peito.Y, frente ? 5.0 : 4.6, 7.2, graus)
            .Elipse(ventre.X, ventre.Y, frente ? 6.2 : 5.7, 7.6, graus);
        tela.Pintar(corpo, Cor.Pelo, Cor.PeloEscuro, Cor.PeloClaro, Cor.PeloEscuro);

        (double X, double Y) centroDaBarriga = frente ? e.NoTronco(0, 8.2) : e.NoTronco(2.4, 8.2);
        Mascara barriga = Nova().Elipse(centroDaBarriga.X, centroDaBarriga.Y, frente ? 3.9 : 3.0, 7.6, graus).Intersectar(corpo);
        tela.Pintar(barriga, Cor.Creme, Cor.CremeSombra);
    }

    // Tem que bater com DesenharBraco.
    private static (double X, double Y) MaoDoBraco((double X, double Y) ombro, Membro membro, Mao mao)
    {
        (double X, double Y) cotovelo = Somar(ombro, Escalar(Direcao(membro.Superior), BracoSuperior));
        (double X, double Y) pulso = Somar(cotovelo, Escalar(Direcao(membro.Inferior), Antebraco));
        return Somar(pulso, Escalar(Direcao(membro.Inferior), mao == Mao.Aberta ? 1.9 : 1.2));
    }

    // Do topo (x = mão + deslocamento) até a mão, com curva leve e duas folhas. Começa um pouco
    // acima do quadro pra encostar na borda de cima, onde prende na borda da tela.
    private static void DesenharCipo(Tela tela, (double X, double Y) mao, double deslocamento)
    {
        (double X, double Y) topo = (mao.X + deslocamento, -2);
        // Curva de Bézier quadrática: o meio do cipó cede para o lado de fora do balanço.
        (double X, double Y) meio = ((topo.X + mao.X) / 2 + (deslocamento >= 0 ? -1.2 : 1.2), (topo.Y + mao.Y) / 2);
        Mascara cipo = Nova();
        (double X, double Y) anterior = topo;
        const int Segmentos = 8;
        for (int i = 1; i <= Segmentos; i++)
        {
            double t = i / (double)Segmentos;
            (double X, double Y) ponto = Bezier(topo, meio, mao, t);
            cipo.Capsula(anterior.X, anterior.Y, ponto.X, ponto.Y, 1.25, 1.25);
            anterior = ponto;
        }
        tela.Pintar(cipo, Cor.Cipo, Cor.CipoEscuro, Cor.CipoClaro);

        // Duas folhas, alternando os lados, a um terço e a dois terços do caminho.
        Mascara folhas = Nova();
        foreach ((double t, int lado) in new[] { (0.30, -1), (0.62, 1) })
        {
            (double X, double Y) p = Bezier(topo, meio, mao, t);
            folhas.Elipse(p.X + lado * 2.4, p.Y + 0.4, 2.3, 1.3, lado * 35);
        }
        tela.Pintar(folhas, Cor.Folha, Cor.FolhaEscura, null, Cor.CipoEscuro);
    }

    private static (double X, double Y) Bezier((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, double t)
    {
        double u = 1 - t;
        return (u * u * a.X + 2 * u * t * b.X + t * t * c.X, u * u * a.Y + 2 * u * t * b.Y + t * t * c.Y);
    }

    private static void DesenharBraco(Tela tela, (double X, double Y) ombro, Membro membro, Mao mao, bool longe, ItemColocado? item = null)
    {
        (double X, double Y) cotovelo = Somar(ombro, Escalar(Direcao(membro.Superior), BracoSuperior));
        (double X, double Y) pulso = Somar(cotovelo, Escalar(Direcao(membro.Inferior), Antebraco));
        Mascara braco = Nova()
            .Capsula(ombro.X, ombro.Y, cotovelo.X, cotovelo.Y, 1.9, 1.7)
            .Capsula(cotovelo.X, cotovelo.Y, pulso.X, pulso.Y, 1.7, 1.5);
        Cor pelo = longe ? Cor.PeloEscuro : Cor.Pelo, sombra = longe ? Cor.Contorno : Cor.PeloEscuro;
        tela.Pintar(braco, pelo, sombra, null, Cor.Contorno);

        (double X, double Y) dir = Direcao(membro.Inferior);
        (double X, double Y) centroDaMao = Somar(pulso, Escalar(dir, mao == Mao.Aberta ? 1.9 : 1.2));
        // O item fica entre o antebraço e a palma: os dedos o envolvem na pega.
        if (item is { } colocado) Carimbar(tela, colocado);
        Mascara palma = mao == Mao.Aberta
            ? Nova().Elipse(centroDaMao.X, centroDaMao.Y, 2.2, 2.9, -membro.Inferior)
            : Nova().Circulo(centroDaMao.X, centroDaMao.Y, 2.2);
        // Indicador: traço de 1 px reto pra cima, do meio do punho, pintado junto com a mão (o
        // contorno envolve os dois) e sem sombra, que fino assim o pintaria inteiro. Seguindo o
        // antebraço inclinado ele saía curto pela beira e parecia um polegar.
        Mascara? dedo = null;
        if (mao == Mao.Apontando)
        {
            double x = Math.Floor(centroDaMao.X) + 0.5;
            dedo = Nova().Capsula(x, centroDaMao.Y - 1.4, x, centroDaMao.Y - PontaDoDedo, 0.7, 0.7).Subtrair(palma);
            palma.Unir(dedo);
        }
        tela.Pintar(palma, longe ? Cor.CremeSombra : Cor.Creme, longe ? Cor.PessegoEscuro : Cor.CremeSombra, null, Cor.Contorno);
        if (dedo is not null) tela.Preencher(dedo, longe ? Cor.CremeSombra : Cor.Creme);
        if (mao == Mao.Aberta && !longe)
        {
            // Dedos: dois riscos de sombra na ponta da mão, na direção do antebraço.
            (double X, double Y) ponta = Somar(centroDaMao, Escalar(dir, 1.6));
            (double X, double Y) lado = (-dir.Y, dir.X);
            foreach (double k in new[] { -0.8, 0.8 })
            {
                (double X, double Y) q = Somar(ponta, Escalar(lado, k));
                int qx = (int)Math.Floor(q.X), qy = (int)Math.Floor(q.Y);
                if (palma[qx, qy]) tela[qx, qy] = Cor.CremeSombra;
            }
        }
    }

    private static void DesenharPerna(Tela tela, (double X, double Y) quadril, Membro membro, PosePixel pose, int ladoDaTela, bool longe)
    {
        (double X, double Y) joelho = Somar(quadril, Escalar(Direcao(membro.Superior), Coxa));
        (double X, double Y) tornozelo = Somar(joelho, Escalar(Direcao(membro.Inferior), Canela));
        Mascara perna = Nova()
            .Capsula(quadril.X, quadril.Y, joelho.X, joelho.Y, 2.6, 2.4)
            .Capsula(joelho.X, joelho.Y, tornozelo.X, tornozelo.Y, 2.4, 2.1);
        Cor pelo = longe ? Cor.PeloEscuro : Cor.Pelo, sombra = longe ? Cor.Contorno : Cor.PeloEscuro;
        tela.Pintar(perna, pelo, sombra, null, longe ? Cor.Contorno : Cor.PeloEscuro);

        if (!pose.PesNoChao) return;
        (double X, double Y) centroDoPe = pose.Vista == Vista.Frente
            ? (tornozelo.X + 1.3 * ladoDaTela, tornozelo.Y + 1.7)
            : (tornozelo.X + 2.3, tornozelo.Y + 1.6);
        Mascara pe = Nova().Elipse(centroDoPe.X, centroDoPe.Y, pose.Vista == Vista.Frente ? 3.7 : 4.0, 1.8);
        tela.Pintar(pe, longe ? Cor.CremeSombra : Cor.Creme, longe ? Cor.PessegoEscuro : Cor.CremeSombra, null, Cor.Contorno);
        if (!longe)
        {
            // Um risco de dedo na ponta do pé.
            int dedoX = (int)Math.Floor(pose.Vista == Vista.Frente ? centroDoPe.X + 2.2 * ladoDaTela : centroDoPe.X + 2.4);
            int dedoY = (int)Math.Floor(centroDoPe.Y);
            if (pe[dedoX, dedoY]) tela[dedoX, dedoY] = Cor.CremeSombra;
        }
    }

    private static void DesenharCauda(Tela tela, Esqueleto e, Cauda forma, bool longe)
    {
        // Caminhos no referencial do tronco (x para a direita, y para cima), a partir da base.
        (double X, double Y)[] local = forma switch
        {
            Cauda.Espiral => [(3, 2), (10, -1.5), (17, -0.5), (19, 5.5), (21, 11.5), (17, 15), (15.3, 11.5), (14.4, 9), (17.4, 8), (18.2, 10.6)],
            Cauda.Alta => [(2.5, 1.5), (9, 1), (12, 8), (11, 16), (10, 22), (14.5, 25), (16, 21), (17, 18.5), (14, 17.5), (13.5, 20)],
            Cauda.Caida => [(2.5, 1.5), (8, 0), (11, -5), (11, -10), (11, -14), (7, -15), (6.5, -12), (6.2, -10), (8.5, -9.5), (9, -11.5)],
            Cauda.Perfil => [(-4.5, 2), (-11, -1), (-17, 0.5), (-18.5, 6), (-20, 11.5), (-15.5, 14.5), (-13.8, 11), (-12.8, 8.6), (-15.6, 7.8), (-16.4, 10.2)],
            Cauda.PerfilAlta => [(-4.5, 2), (-11, 3), (-15, 9), (-14, 15), (-13, 20), (-17.5, 22.5), (-18.5, 19), (-19, 16.5), (-16, 16), (-15.5, 18.2)],
            Cauda.PerfilCaida => [(-4.5, 2), (-10, 1), (-14, -4), (-14.5, -9), (-15, -13), (-11, -14.5), (-10.5, -11.5), (-10.2, -9.5), (-12.5, -9), (-13, -11)],
            _ => throw new ArgumentOutOfRangeException(nameof(forma), forma, "Cauda desconhecida."),
        };
        List<(double X, double Y)> caminho = [.. local.Select(p => e.NoTronco(p.X, p.Y))];
        Mascara pelo = Nova().Traco(caminho, 1.4, 1.5, 0, 0.72);
        Mascara ponta = Nova().Traco(caminho, 1.6, 1.9, 0.66, 1);
        pelo.Subtrair(ponta);
        tela.Pintar(pelo, longe ? Cor.PeloEscuro : Cor.Pelo, longe ? Cor.Contorno : Cor.PeloEscuro, null, Cor.Contorno);
        tela.Pintar(ponta, Cor.Creme, Cor.CremeSombra, null, Cor.PeloEscuro);
    }

    private static void DesenharCabecaDeFrente(Tela tela, Esqueleto e, Rosto rosto)
    {
        (double cx, double cy) = e.Cabeca;

        // Orelhas redondas, cor de pêssego com a borda de pelo, atrás da cabeça.
        foreach (int lado in new[] { -1, 1 })
        {
            Mascara orelha = Nova().Circulo(cx + 11.4 * lado, cy + 1.2, 4.7);
            tela.Pintar(orelha, Cor.Pelo, Cor.PeloEscuro, null, Cor.Contorno);
            Mascara dentro = Nova().Circulo(cx + 11.8 * lado, cy + 1.3, 3.5);
            tela.Pintar(dentro, Cor.Pessego, Cor.PessegoEscuro);
        }

        Mascara cabeca = Nova().Circulo(cx, cy, RaioCabeca).Unir(Tufo(cx, cy, rosto.Topete, perfil: false))
            .Poligono((cx - 8.6, cy + 5.4), (cx - 12.6, cy + 8.8), (cx - 8.0, cy + 8.4))
            .Poligono((cx + 8.6, cy + 5.4), (cx + 12.6, cy + 8.8), (cx + 8.0, cy + 8.4));
        tela.Pintar(cabeca, Cor.Pelo, Cor.PeloEscuro, Cor.PeloClaro, Cor.PeloEscuro);

        // Máscara do rosto: dois lobos sobre os olhos (o bico de pelo desce entre eles), bochechas
        // largas e focinho, como nas pranchas.
        Mascara mascara = Nova()
            .Elipse(cx - 5.0, cy - 1.3, 5.2, 6.0)
            .Elipse(cx + 5.0, cy - 1.3, 5.2, 6.0)
            .Elipse(cx, cy + 2.6, 9.7, 4.6)
            .Elipse(cx, cy + 5, 7.4, 5.1)
            .Intersectar(Nova().Circulo(cx, cy, RaioCabeca - 0.6));
        tela.Pintar(mascara, Cor.Creme, Cor.CremeSombra);

        int ex = (int)Math.Round(cx), ey = (int)Math.Round(cy);
        tela.Carimbar(Rostos.Olhos[rosto.OlhoE], ex - 9, ey - 4);
        tela.Carimbar(Rostos.Olhos[rosto.OlhoD], ex + 2, ey - 4);
        tela.Carimbar(Rostos.Sobrancelhas[rosto.Sobrancelhas], ex - 8, ey - 7);
        if (rosto.Corado)
        {
            Cor rubor = rosto.Rubor ?? Cor.Bochecha;
            if (rosto.RuborGrande)
            {
                // Mancha grande (bêbado, enjoado): 3x2 px por bochecha, toda sobre o creme (o
                // corado normal encosta no pelo da borda do rosto).
                for (int y = ey + 4; y <= ey + 5; y++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        tela[ex - 8 + k, y] = rubor;
                        tela[ex + 5 + k, y] = rubor;
                    }
                }
            }
            else
            {
                tela[ex - 9, ey + 5] = rubor;
                tela[ex - 8, ey + 5] = rubor;
                tela[ex + 7, ey + 5] = rubor;
                tela[ex + 8, ey + 5] = rubor;
            }
        }
        // Nariz pequeno cor de pêssego, como nas pranchas.
        tela[ex - 1, ey + 4] = Cor.Pessego;
        tela[ex, ey + 4] = Cor.Pessego;
        tela[ex - 1, ey + 5] = Cor.PessegoEscuro;
        tela[ex, ey + 5] = Cor.PessegoEscuro;
        tela.Carimbar(Rostos.Bocas[rosto.Boca], ex - 5, ey + 6);
        DesenharChapeu(tela, cx, cy, rosto.Topete, perfil: false);
    }

    private static void DesenharCabecaDePerfil(Tela tela, Esqueleto e, Rosto rosto)
    {
        (double cx, double cy) = e.Cabeca;
        Mascara cabeca = Nova().Circulo(cx, cy, RaioCabeca - 0.3).Unir(Tufo(cx, cy, rosto.Topete, perfil: true))
            .Poligono((cx - 3.5, cy + 8.0), (cx - 7.8, cy + 11.4), (cx - 2.2, cy + 10.4));
        tela.Pintar(cabeca, Cor.Pelo, Cor.PeloEscuro, Cor.PeloClaro, Cor.PeloEscuro);

        Mascara mascara = Nova()
            .Circulo(cx + 4.4, cy - 0.9, 6.1)
            .Elipse(cx + 6.6, cy + 4.3, 5.9, 4.3);
        tela.Pintar(mascara, Cor.Creme, Cor.CremeSombra, null, Cor.PeloEscuro);

        // Orelha grande na parte de trás da cabeça, por cima dela.
        Mascara orelha = Nova().Circulo(cx - 5.6, cy + 0.6, 4.5);
        tela.Pintar(orelha, Cor.Pelo, Cor.PeloEscuro, null, Cor.PeloEscuro);
        Mascara dentro = Nova().Circulo(cx - 5.6, cy + 1.0, 2.6);
        tela.Pintar(dentro, Cor.Pessego, Cor.PessegoEscuro);

        int ex = (int)Math.Round(cx), ey = (int)Math.Round(cy);
        if (rosto.Corado && rosto.RuborGrande)
        {
            // De perfil só a mancha grande aparece (bêbado e enjoado precisam se ler andando).
            Cor rubor = rosto.Rubor ?? Cor.Bochecha;
            for (int y = ey + 3; y <= ey + 4; y++)
                for (int x = ex + 3; x <= ex + 5; x++)
                    tela[x, y] = rubor;
        }
        tela.Carimbar(Rostos.OlhosPerfil[rosto.OlhoPerfil], ex + 3, ey - 5);
        tela[ex + 3, ey - 7] = Cor.Sobrancelha;
        tela[ex + 4, ey - 8] = Cor.Sobrancelha;
        tela[ex + 5, ey - 8] = Cor.Sobrancelha;
        tela[ex + 11, ey + 2] = Cor.Pessego;
        tela[ex + 12, ey + 2] = Cor.Pessego;
        tela[ex + 11, ey + 3] = Cor.PessegoEscuro;
        tela.Carimbar(Rostos.BocasPerfil[rosto.BocaPerfil], ex + 7, ey + 5);
        DesenharChapeu(tela, cx, cy, rosto.Topete, perfil: true);
    }

    // Chapéu de palha com faixa vermelha. Reage junto com o tufo: salta no susto e na risada, desce
    // no sono e entorta na bebedeira (Torto: gira tudo 12° anti-horário em volta do centro da aba,
    // que desce meio pixel e vai um pra frente).
    private static void DesenharChapeu(Tela tela, double cx, double cy, Topete topete, bool perfil)
    {
        bool torto = topete == Topete.Torto;
        double dy = topete switch { Topete.Ericado => -2.2, Topete.Caido => 1.0, Topete.Torto => 0.5, _ => 0 };
        double dx = torto ? 1.0 : 0;
        double abaY = cy - 9.3 + dy, copaY = cy - 12.6 + dy;
        double abaX = (perfil ? cx + 0.8 : cx) + dx, copaX = (perfil ? cx - 0.8 : cx) + dx;
        double abaRx = perfil ? 15.4 : 17.0, abaRy = perfil ? 2.2 : 2.8;
        double copaRx = perfil ? 8.8 : 9.4, copaRy = 5.5;

        // Sem giro os pontos ficam idênticos, pra não mexer nos outros topetes.
        double graus = torto ? -12 : 0;
        (double X, double Y) G(double x, double y) => torto ? Girar(x, y, abaX, abaY, graus) : (x, y);

        // A copa não pode tocar a linha 0, senão o contorno não cabe. Só o eriçado no alto da
        // escalada chegava lá: desce o chapéu inteiro, pixel a pixel, até a copa começar na linha 1.
        for (int descida = 0; descida < 4 && PrimeiraLinha(Nova().Elipse(G(copaX, copaY).X, G(copaX, copaY).Y, copaRx, copaRy, graus)) < 1; descida++)
        {
            abaY += 1;
            copaY += 1;
        }
        // As faixas horizontais viram polígonos girados; inclinadas, precisam passar das bordas do quadro.
        double esquerda = torto ? -Lado : 0, direita = torto ? 2 * Lado : Lado, topo = torto ? -Lado : 0;
        (double X, double Y) centroDaCopa = G(copaX, copaY);

        Mascara aba = Nova().Elipse(abaX, abaY, abaRx, abaRy, graus);
        // Sombra da aba na testa e no pelo logo abaixo dela.
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
                if (!aba[x, y] && aba[x, y - 1])
                    tela[x, y] = tela[x, y] switch
                    {
                        Cor.Creme or Cor.CremeClaro => Cor.CremeSombra,
                        Cor.Pelo or Cor.PeloClaro => Cor.PeloEscuro,
                        var c => c,
                    };
        tela.Pintar(aba, Cor.Palha, Cor.PalhaEscura, Cor.PalhaClara, Cor.Contorno);

        Mascara acimaDaAba = Nova().Poligono(G(esquerda, topo), G(direita, topo), G(direita, abaY + 0.4), G(esquerda, abaY + 0.4));
        Mascara copa = Nova().Elipse(centroDaCopa.X, centroDaCopa.Y, copaRx, copaRy, graus).Intersectar(acimaDaAba);
        tela.Pintar(copa, Cor.Palha, Cor.PalhaEscura, Cor.PalhaClara, Cor.PalhaEscura);
        // Trama da palha: pontos escuros em diagonal, fora das bordas.
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
                if (copa[x, y] && copa[x - 1, y] && copa[x + 1, y] && copa[x, y - 1] && tela[x, y] == Cor.Palha && (x + 2 * y) % 5 == 0)
                    tela[x, y] = Cor.PalhaEscura;

        Mascara faixa = Nova().Elipse(centroDaCopa.X, centroDaCopa.Y, copaRx, copaRy, graus)
            .Intersectar(Nova().Poligono(G(esquerda, abaY - 3.0), G(direita, abaY - 3.0), G(direita, abaY + 0.4), G(esquerda, abaY + 0.4)));
        tela.Pintar(faixa, Cor.Faixa, Cor.FaixaEscura);

        // A borda da frente da aba passa por cima da base da copa.
        Mascara labio = Nova().Elipse(abaX, abaY, abaRx, abaRy, graus)
            .Subtrair(Nova().Poligono(G(esquerda, topo), G(direita, topo), G(direita, abaY + 0.2), G(esquerda, abaY + 0.2)));
        tela.Pintar(labio, Cor.Palha, Cor.PalhaEscura, null, Cor.PalhaEscura);
    }

    // Gota de suor da paranoia: só o preenchimento, ponta pra cima e brilho do lado da luz. O
    // contorno vem da linha interna e do contorno final.
    internal static readonly Carimbo GotaDeSuor = new(
        ".A.",
        "AAa",
        "WAa",
        "Aaa");

    // A partir do centro arredondado da cabeça. De frente, na têmpora esquerda entre aba e orelha
    // (a mão que aponta é a B, do outro lado); de perfil, atrás do olho, sob a aba.
    internal static (int X, int Y) CantoDaGota(Vista vista, int ex, int ey)
        => vista == Vista.Frente ? (ex - 13, ey - 6) : (ex - 3, ey - 8);

    private static void DesenharGota(Tela tela, Esqueleto e, Vista vista)
    {
        (double cx, double cy) = e.Cabeca;
        (int x, int y) = CantoDaGota(vista, (int)Math.Round(cx), (int)Math.Round(cy));
        tela.Carimbar(GotaDeSuor, x, y, linhaInterna: Cor.Contorno);
    }

    // int.MaxValue se vazia.
    private static int PrimeiraLinha(Mascara m)
    {
        for (int y = 0; y < m.Altura; y++)
            for (int x = 0; x < m.Largura; x++)
                if (m[x, y]) return y;
        return int.MaxValue;
    }

    // Graus no sentido horário da tela, igual a Mascara.Elipse.
    private static (double X, double Y) Girar(double x, double y, double px, double py, double graus)
    {
        double a = graus * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double rx = x - px, ry = y - py;
        return (px + rx * cos - ry * sin, py + rx * sin + ry * cos);
    }

    // Tufo bagunçado no alto da cabeça, por baixo do chapéu.
    private static Mascara Tufo(double cx, double cy, Topete topete, bool perfil)
    {
        double s = perfil ? -1 : 1;
        (double X, double Y)[] pontos = topete switch
        {
            Topete.Ericado => [(-6.5, -8.5), (-7.5, -14.5), (-3.8, -11.5), (-2.2, -16.5), (0.6, -12), (3.6, -16), (4.4, -11.2), (8, -13.2), (6.4, -8.2)],
            Topete.Caido => [(-7, -8.5), (-9.5, -11.5), (-4.5, -11.2), (-4.4, -13.2), (-0.5, -11.8), (1.5, -13.6), (3.2, -11.2), (6.8, -11.2), (6.6, -8)],
            // Normal e Torto: no bêbado só o chapéu entorta, o tufo fica como está.
            _ => [(-6.5, -8.5), (-8, -12.8), (-3.8, -11), (-2.5, -14.5), (0.4, -11.4), (3.2, -14), (4, -10.8), (7.4, -11.6), (6.4, -8)],
        };
        return Nova().Poligono([.. pontos.Select(p => (cx + p.X * s, cy + p.Y))]);
    }

    // ------------------------------------------------------------------ apoio

    private static (double X, double Y) Direcao(double graus)
    {
        double a = graus * Math.PI / 180;
        return (Math.Sin(a), Math.Cos(a));
    }

    private static (double X, double Y) Somar((double X, double Y) a, (double X, double Y) b) => (a.X + b.X, a.Y + b.Y);

    private static (double X, double Y) Escalar((double X, double Y) a, double k) => (a.X * k, a.Y * k);
}
