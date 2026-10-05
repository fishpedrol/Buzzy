namespace Buzzy.Visual.Pixel;

/// <summary>
/// Ícones do menu nativo (DEC-027 e DEC-028), gerados do próprio desenho, para o submenu da emoção
/// dominante e o dos itens. O rosto é o recorte exato da célula de assets/identidade/pixel/previa/expressoes.png
/// (crítica, C29): o parado com a cara, de (12, 0), com 40 × 32 pixels, pela mesma <see cref="Tela.Recortada"/>
/// que a prévia usa. O item é o desenho do chão, de 24 × 24. O menu não amplia o bitmap: a ampliação
/// inteira pelo DPI (<see cref="Fator"/>) é feita aqui, por vizinho mais próximo, sem suavização.
/// </summary>
public static class IconesDoMenu
{
    /// <summary>Canto de cima à esquerda do recorte do rosto no quadro do parado (o mesmo de expressoes.png).</summary>
    public const int XDoRosto = 12, YDoRosto = 0;

    /// <summary>Tamanho do rosto, em pixels de arte: 40 × 32, a célula de expressoes.png.</summary>
    public const int LarguraDoRosto = 40, AlturaDoRosto = 32;

    /// <summary>Lado do ícone do item, em pixels de arte: o desenho do chão.</summary>
    public const int LadoDoItem = ItensPixel.Lado;

    private static readonly PosePixel Parado = PosesPixel.Todas.First(p => p.Nome == "parado");

    /// <summary>O rosto da <paramref name="expressao"/> (qualquer chave de <see cref="Rostos.Expressoes"/>): o recorte do parado.</summary>
    public static Tela Rosto(string expressao)
    {
        if (expressao is null || !Rostos.Expressoes.ContainsKey(expressao))
            throw new ArgumentException($"Cara desconhecida: '{expressao}'.", nameof(expressao));
        return BonecoPixel.Desenhar(Parado, expressao).Recortada(XDoRosto, YDoRosto, LarguraDoRosto, AlturaDoRosto);
    }

    /// <summary>O ícone do item: o desenho do chão (<see cref="ItensPixel.Desenhar"/>).</summary>
    public static Tela Item(string item) => ItensPixel.Desenhar(item);

    /// <summary>A ampliação inteira do ícone no DPI do monitor: 1× até 191, 2× de 192 a 287, 3× em 288...</summary>
    public static int Fator(int dpi) => Math.Max(1, dpi / 96);

    /// <summary>
    /// Os pixels da tela ampliados <paramref name="fator"/> vezes por vizinho mais próximo, linha a linha
    /// de cima para baixo, em 0xAARRGGBB: na memória (little-endian), os bytes B, G, R e A do DIB de 32
    /// bits. Com o alfa só 0 ou 255, já é pré-multiplicado: o transparente é 0.
    /// </summary>
    public static uint[] Ampliar(Tela tela, int fator)
    {
        ArgumentNullException.ThrowIfNull(tela);
        ArgumentOutOfRangeException.ThrowIfLessThan(fator, 1);
        uint[] origem = tela.ParaArgb();
        int largura = tela.Largura * fator, altura = tela.Altura * fator;
        var saida = new uint[largura * altura];
        for (int y = 0; y < altura; y++)
            for (int x = 0; x < largura; x++)
                saida[y * largura + x] = origem[y / fator * tela.Largura + x / fator];
        return saida;
    }

    /// <summary>
    /// As linhas em ordem invertida, para o DIB de altura positiva, que guarda a última linha da imagem
    /// primeiro (crítica, C30). Devolve um vetor novo.
    /// </summary>
    public static uint[] DeBaixoParaCima(uint[] pixels, int largura)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfLessThan(largura, 1);
        if (pixels.Length % largura != 0) throw new ArgumentException("O tamanho não fecha linhas inteiras.", nameof(pixels));
        int linhas = pixels.Length / largura;
        var saida = new uint[pixels.Length];
        for (int y = 0; y < linhas; y++)
            Array.Copy(pixels, (linhas - 1 - y) * largura, saida, y * largura, largura);
        return saida;
    }
}
