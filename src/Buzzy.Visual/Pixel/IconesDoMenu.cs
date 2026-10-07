namespace Buzzy.Visual.Pixel;

// Ícones dos submenus de emoção e de itens, gerados do próprio desenho. O rosto é o mesmo recorte
// da célula de assets/identidade/pixel/previa/expressoes.png; o item é o desenho do chão. O menu
// não amplia o bitmap, então a ampliação inteira por DPI é feita aqui, por vizinho mais próximo.
public static class IconesDoMenu
{
    // Recorte no quadro do parado, igual ao de expressoes.png. Em pixels de arte.
    public const int XDoRosto = 12, YDoRosto = 0;

    public const int LarguraDoRosto = 40, AlturaDoRosto = 32;

    public const int LadoDoItem = ItensPixel.Lado;

    private static readonly PosePixel Parado = PosesPixel.Todas.First(p => p.Nome == "parado");

    public static Tela Rosto(string expressao)
    {
        if (expressao is null || !Rostos.Expressoes.ContainsKey(expressao))
            throw new ArgumentException($"Cara desconhecida: '{expressao}'.", nameof(expressao));
        return BonecoPixel.Desenhar(Parado, expressao).Recortada(XDoRosto, YDoRosto, LarguraDoRosto, AlturaDoRosto);
    }

    public static Tela Item(string item) => ItensPixel.Desenhar(item);

    // 1x até 191 DPI, 2x de 192 a 287, 3x a partir de 288...
    public static int Fator(int dpi) => Math.Max(1, dpi / 96);

    // De cima pra baixo, em 0xAARRGGBB (na memória little-endian: B, G, R, A, como o DIB de 32
    // bits). Alfa é só 0 ou 255, então já está pré-multiplicado.
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

    // DIB com altura positiva guarda a última linha primeiro. Devolve um vetor novo.
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
