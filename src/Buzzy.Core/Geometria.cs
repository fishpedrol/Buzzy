namespace Buzzy.Core;

/// <summary>Ponto em pixels físicos do desktop virtual (DEC-008). Aceita valores negativos.</summary>
public readonly record struct PontoPx(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}

/// <summary>Tamanho em pixels físicos.</summary>
public readonly record struct TamanhoPx(int Largura, int Altura)
{
    public override string ToString() => $"{Largura}x{Altura}";
}

/// <summary>
/// Tamanho lógico em DIPs (1 DIP = 1 px a 96 DPI). O tamanho físico depende do DPI do
/// monitor em que está a âncora (ARCHITECTURE.md 2.4).
/// </summary>
public readonly record struct TamanhoDip(int Largura, int Altura)
{
    /// <summary>Tamanho físico no DPI dado, arredondando a metade para longe de zero.</summary>
    public TamanhoPx ParaPixels(int dpi)
    {
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI precisa ser positivo.");
        return new TamanhoPx(Escalar(Largura, dpi), Escalar(Altura, dpi));
    }

    private static int Escalar(int dip, int dpi) => (int)Math.Round(dip * dpi / 96.0, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Largura}x{Altura} DIP";
}

/// <summary>
/// Retângulo em pixels físicos, semiaberto: contém os pontos com
/// Esquerda ≤ X &lt; Direita e Topo ≤ Y &lt; Base, como o RECT do Windows.
/// </summary>
public readonly record struct RetanguloPx(int Esquerda, int Topo, int Direita, int Base)
{
    public int Largura => Direita - Esquerda;
    public int Altura => Base - Topo;
    public bool Vazio => Largura <= 0 || Altura <= 0;
    public TamanhoPx Tamanho => new(Largura, Altura);

    /// <summary>Centro, arredondado para baixo (em direção a menos infinito) em cada eixo.</summary>
    public PontoPx Centro => new(Esquerda + (int)Math.Floor(Largura / 2.0), Topo + (int)Math.Floor(Altura / 2.0));

    public static RetanguloPx DePosicaoETamanho(PontoPx canto, TamanhoPx tamanho)
        => new(canto.X, canto.Y, canto.X + tamanho.Largura, canto.Y + tamanho.Altura);

    public bool Contem(PontoPx p) => p.X >= Esquerda && p.X < Direita && p.Y >= Topo && p.Y < Base;

    /// <summary>Verdadeiro se <paramref name="outro"/> cabe inteiro neste retângulo. Um retângulo vazio não cabe.</summary>
    public bool Contem(RetanguloPx outro)
        => !outro.Vazio && outro.Esquerda >= Esquerda && outro.Direita <= Direita && outro.Topo >= Topo && outro.Base <= Base;

    public bool Intersecta(RetanguloPx outro)
        => !Vazio && !outro.Vazio && outro.Esquerda < Direita && Esquerda < outro.Direita && outro.Topo < Base && Topo < outro.Base;

    public RetanguloPx Deslocado(int dx, int dy) => new(Esquerda + dx, Topo + dy, Direita + dx, Base + dy);

    /// <summary>
    /// Quadrado da distância do ponto ao pixel mais próximo do retângulo; zero se o ponto
    /// está dentro. Usa o último pixel incluído (Direita − 1, Base − 1) como limite.
    /// </summary>
    public long DistanciaAoQuadrado(PontoPx p)
    {
        if (Vazio) throw new InvalidOperationException("Distância a um retângulo vazio não é definida.");
        long dx = p.X < Esquerda ? Esquerda - (long)p.X : p.X > Direita - 1 ? (long)p.X - (Direita - 1) : 0;
        long dy = p.Y < Topo ? Topo - (long)p.Y : p.Y > Base - 1 ? (long)p.Y - (Base - 1) : 0;
        return dx * dx + dy * dy;
    }

    public override string ToString() => $"({Esquerda},{Topo})-({Direita},{Base})";
}
