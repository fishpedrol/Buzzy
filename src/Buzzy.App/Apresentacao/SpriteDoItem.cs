using System.Windows.Media.Imaging;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

// Sprite da janela de um item: o desenho de chão da arte (24 × 24 px) ampliado por
// vizinho mais próximo até o tamanho do item no DPI (2×, 3×, 4× exatos em 100%, 150%,
// 200%). Alfa só 0 ou 255, então só os pixels opacos recebem clique.
internal static class SpriteDoItem
{
    // 48 × 48 DIP, da mesma fonte que dá o retângulo da janela.
    internal static readonly TamanhoDip TamanhoLogico = new ConfiguracaoDoNucleo().TamanhoDoItem;

    // 4 MiB. 13 itens por DPI, de 9 KiB (100%) a 81 KiB (300%) cada: cabem vários DPIs.
    internal const long OrcamentoDoCache = 4L * 1024 * 1024;

    private static readonly CacheDeQuadros<(Item Item, int Dpi)> Cache = new(OrcamentoDoCache);

    internal static long BytesEmCache => Cache.Bytes;

    // Conta só os que não estavam no cache.
    internal static long QuadrosRenderizados { get; private set; }

    // Congelado e vindo do cache quando dá.
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

    // Menor retângulo com os pixels opacos, em px da janela: onde o item recebe clique.
    // Lido do próprio bitmap, que é o que o Windows usa pra decidir o clique.
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

    // Onde as verificações clicam, em px da janela: um opaco no centro do pixel de arte
    // de ItensPixel.PontosDeTeste e um transparente no canto. Conferidos no bitmap.
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
