using System.Windows.Media.Imaging;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// O sprite da janela de um item do tamagotchi (DEC-028; crítica, C11 e C27): o desenho do chão da pixel art
/// (<see cref="ItensPixel.Desenhar"/>, 24 × 24 pixels de arte) ampliado por vizinho mais próximo até o tamanho do item
/// no DPI do monitor (2×, 3× e 4× exatos em 100%, 150% e 200%), sem achatar nem esticar. Alfa só 0 ou 255: só os pixels
/// opacos recebem clique, como no personagem (ARCHITECTURE.md 2.13.7, item 7). Os desenhos ficam num cache limitado,
/// por item e DPI; os limites opacos e os pontos de teste vão para o log de diagnóstico.
/// </summary>
internal static class SpriteDoItem
{
    /// <summary>
    /// Tamanho lógico do item: o do núcleo do aplicativo (<see cref="ConfiguracaoDoNucleo.TamanhoDoItem"/>, 48 × 48 DIP),
    /// a mesma fonte que dá o retângulo da janela.
    /// </summary>
    internal static readonly TamanhoDip TamanhoLogico = new ConfiguracaoDoNucleo().TamanhoDoItem;

    /// <summary>
    /// Orçamento do cache (crítica, C28): 4 MiB. São 13 itens por DPI; a 100% cada desenho tem 9 KiB e, a 300%, 81 KiB:
    /// os 13 cabem em vários DPIs ao mesmo tempo.
    /// </summary>
    internal const long OrcamentoDoCache = 4L * 1024 * 1024;

    private static readonly CacheDeQuadros<(Item Item, int Dpi)> Cache = new(OrcamentoDoCache);

    /// <summary>Quantos bytes de pixels os desenhos do cache ocupam.</summary>
    internal static long BytesEmCache => Cache.Bytes;

    /// <summary>Quantos desenhos já foram feitos, por não estarem no cache.</summary>
    internal static long QuadrosRenderizados { get; private set; }

    /// <summary>O item no DPI do monitor (tamanho físico = <see cref="TamanhoLogico"/> no DPI dado), congelado e do cache.</summary>
    internal static BitmapSource Renderizar(Item item, int dpi)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item fora do enum.");
        if (Cache.TentarObter((item, dpi), out BitmapSource? pronto)) return pronto;
        TamanhoPx tamanho = TamanhoLogico.ParaPixels(dpi);
        BitmapSource bmp = SpriteProvisorio.Bitmap(ItensPixel.Desenhar(PoseDoPersonagem.NomeDoItem(item)), tamanho.Largura, tamanho.Altura, dpi);
        QuadrosRenderizados++;
        Cache.Guardar((item, dpi), bmp);
        return bmp;
    }

    /// <summary>
    /// O menor retângulo com os pixels opacos do sprite, em coordenadas da janela (pixels físicos): onde o item recebe
    /// clique. Lido dos pixels do próprio bitmap, que é o que o Windows usa para decidir o clique.
    /// </summary>
    internal static RetanguloPx LimitesOpacos(Item item, int dpi)
    {
        BitmapSource bmp = Renderizar(item, dpi);
        int largura = bmp.PixelWidth, altura = bmp.PixelHeight;
        int[] pixels = new int[largura * altura];
        bmp.CopyPixels(pixels, largura * 4, 0);
        int e = largura, t = altura, d = 0, b = 0;
        for (int y = 0; y < altura; y++)
        {
            for (int x = 0; x < largura; x++)
            {
                if ((uint)pixels[y * largura + x] >> 24 == 0) continue;
                e = Math.Min(e, x);
                t = Math.Min(t, y);
                d = Math.Max(d, x + 1);
                b = Math.Max(b, y + 1);
            }
        }
        return d == 0 ? default : new RetanguloPx(e, t, d, b);
    }

    /// <summary>
    /// Pontos de teste em coordenadas da janela (pixels físicos), para as verificações clicarem no item: um opaco, no
    /// corpo do desenho (o de <see cref="ItensPixel.PontosDeTeste"/>, no centro do pixel de arte ampliado), e um
    /// transparente, no canto de cima à esquerda. Conferidos contra os pixels do bitmap.
    /// </summary>
    internal static (PontoPx Opaco, PontoPx Transparente) PontosDeTeste(Item item, int dpi)
    {
        BitmapSource bmp = Renderizar(item, dpi);
        int largura = bmp.PixelWidth, altura = bmp.PixelHeight;
        ((int X, int Y) arte, _) = ItensPixel.PontosDeTeste(PoseDoPersonagem.NomeDoItem(item));
        var opaco = new PontoPx((2 * arte.X + 1) * largura / (2 * ItensPixel.Lado), (2 * arte.Y + 1) * altura / (2 * ItensPixel.Lado));
        var transparente = new PontoPx(0, 0);

        int[] pixels = new int[largura * altura];
        bmp.CopyPixels(pixels, largura * 4, 0);
        if ((uint)pixels[opaco.Y * largura + opaco.X] >> 24 != 255)
            throw new InvalidOperationException($"O ponto opaco de teste {opaco} do item {item} não está opaco a {dpi} DPI.");
        if ((uint)pixels[transparente.Y * largura + transparente.X] >> 24 != 0)
            throw new InvalidOperationException($"O ponto transparente de teste {transparente} do item {item} não está transparente a {dpi} DPI.");
        return (opaco, transparente);
    }
}
