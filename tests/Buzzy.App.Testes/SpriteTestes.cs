using System.IO;
using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Sprite provisório: tamanho físico por DPI (critério 8 da Fase 1, na parte que dá para
/// verificar sem mudar a escala do Windows) e regra de alfa de P1 (só 0 ou 255).
/// </summary>
internal sealed class SpriteTestes
{
    private static readonly int[] Dpis = [96, 120, 144, 168, 192, 240, 288];

    private static int[] Pixels(BitmapSource bmp)
    {
        int[] p = new int[bmp.PixelWidth * bmp.PixelHeight];
        bmp.CopyPixels(p, bmp.PixelWidth * 4, 0);
        return p;
    }

    private static byte Alfa(int pixel) => (byte)((uint)pixel >> 24);

    [Teste]
    public void TamanhoFisicoAcompanhaODpiDoMonitor()
    {
        foreach (int dpi in Dpis)
        {
            BitmapSource bmp = SpriteProvisorio.Renderizar(dpi);
            TamanhoPx esperado = SpriteProvisorio.TamanhoLogico.ParaPixels(dpi);
            Afirmar.Igual(esperado.Largura, bmp.PixelWidth, $"largura em {dpi} DPI");
            Afirmar.Igual(esperado.Altura, bmp.PixelHeight, $"altura em {dpi} DPI");
            Afirmar.Aproximado(dpi, bmp.DpiX, 0.01, $"DPI do bitmap em {dpi} DPI");
            Afirmar.Verdadeiro(bmp.IsFrozen, "o sprite é congelado");
        }
    }

    [Teste]
    public void TodoPixelTemAlfaZeroOuDuzentosECinquentaECinco()
    {
        foreach (int dpi in Dpis)
        {
            int[] pixels = Pixels(SpriteProvisorio.Renderizar(dpi));
            int intermediarios = pixels.Count(p => Alfa(p) is not 0 and not 255);
            Afirmar.Igual(0, intermediarios, $"pixels com alfa entre 1 e 254 em {dpi} DPI");
            Afirmar.Verdadeiro(pixels.Any(p => Alfa(p) == 255), $"há pixels opacos em {dpi} DPI");
            Afirmar.Verdadeiro(pixels.Any(p => Alfa(p) == 0), $"há pixels transparentes em {dpi} DPI");
        }
    }

    [Teste]
    public void OsQuatroCantosSaoTransparentesParaOCliqueAtravessar()
    {
        foreach (int dpi in Dpis)
        {
            BitmapSource bmp = SpriteProvisorio.Renderizar(dpi);
            int[] p = Pixels(bmp);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            int bloco = Math.Max(3, w / 20);
            foreach ((int x0, int y0) in new[] { (0, 0), (w - bloco, 0), (0, h - bloco), (w - bloco, h - bloco) })
            {
                for (int y = y0; y < y0 + bloco; y++)
                    for (int x = x0; x < x0 + bloco; x++)
                        Afirmar.Igual((byte)0, Alfa(p[y * w + x]), $"canto ({x},{y}) em {dpi} DPI");
            }
        }
    }

    [Teste]
    public void OsPesTocamABordaDeBaixoPertoDoCentro()
    {
        // A âncora é o centro da base: a última linha do sprite precisa ter pixels opacos
        // (os pés), e todos perto do centro.
        foreach (int dpi in Dpis)
        {
            BitmapSource bmp = SpriteProvisorio.Renderizar(dpi);
            int[] p = Pixels(bmp);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            List<int> colunasOpacas = [.. Enumerable.Range(0, w).Where(x => Alfa(p[(h - 2) * w + x]) == 255)];
            Afirmar.Verdadeiro(colunasOpacas.Count > 0, $"pés na penúltima linha em {dpi} DPI");
            double meio = w / 2.0;
            Afirmar.Verdadeiro(colunasOpacas.All(x => Math.Abs(x - meio) < w * 0.3), $"pés perto do centro em {dpi} DPI");
        }
    }

    [Teste]
    public void PontosDeTesteBatemComOsPixels()
    {
        foreach (int dpi in Dpis)
        {
            BitmapSource bmp = SpriteProvisorio.Renderizar(dpi);
            (PontoPx opaco, PontoPx transparente) = SpriteProvisorio.PontosDeTeste(bmp);
            int[] p = Pixels(bmp);
            Afirmar.Igual((byte)255, Alfa(p[opaco.Y * bmp.PixelWidth + opaco.X]), $"ponto opaco em {dpi} DPI");
            Afirmar.Igual((byte)0, Alfa(p[transparente.Y * bmp.PixelWidth + transparente.X]), $"ponto transparente em {dpi} DPI");
        }
    }

    [Teste]
    public void RenderizacaoEDeterministica()
    {
        Afirmar.Sequencia(Pixels(SpriteProvisorio.Renderizar(144)), Pixels(SpriteProvisorio.Renderizar(144)));
    }

    [Teste]
    public void LimiarizacaoForcaZeroOuOpacoEDesfazAPreMultiplicacao()
    {
        int[] pixels =
        [
            0x00000000,                        // transparente continua transparente
            unchecked((int)0x7F7F0000),        // alfa 127 vira transparente
            unchecked((int)0x80400000),        // alfa 128, vermelho pré-multiplicado 0x40 -> 0x7F
            unchecked((int)0xFF123456),        // opaco não muda
            unchecked((int)0x01010101),        // alfa 1 (o caso de P1) vira transparente
        ];
        SpriteProvisorio.Limiarizar(pixels);
        Afirmar.Igual(0, pixels[0]);
        Afirmar.Igual(0, pixels[1]);
        Afirmar.Igual(unchecked((int)0xFF7F0000), pixels[2]);
        Afirmar.Igual(unchecked((int)0xFF123456), pixels[3]);
        Afirmar.Igual(0, pixels[4]);
    }

    [Teste]
    public void IconeDaBandejaEhUmPngDoTamanhoPedido()
    {
        foreach (int lado in new[] { 16, 20, 24, 32, 48 })
        {
            byte[] png = SpriteProvisorio.IconePng(lado);
            Afirmar.Verdadeiro(png.Length > 8 && png[0] == 0x89 && png[1] == (byte)'P', $"assinatura PNG em {lado} px");
            using var memoria = new MemoryStream(png);
            BitmapFrame quadro = new PngBitmapDecoder(memoria, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Afirmar.Igual(lado, quadro.PixelWidth, $"largura do ícone {lado}");
            Afirmar.Igual(lado, quadro.PixelHeight, $"altura do ícone {lado}");
        }
    }
}
