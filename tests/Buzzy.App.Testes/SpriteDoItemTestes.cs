using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// O sprite da janela de um item do tamagotchi (DEC-028, passo T8), sem janela: o desenho do chão da arte
/// (<see cref="ItensPixel.Desenhar"/>) ampliado sem suavização até o tamanho do item no DPI do monitor, com alfa só 0 ou
/// 255 (só os pixels opacos recebem clique, como no personagem), o cache por item e DPI e os limites opacos.
/// </summary>
internal sealed class SpriteDoItemTestes
{
    private static readonly int[] Dpis = [96, 120, 144, 168, 192, 240, 288, 336, 384, 480];

    private static int[] Pixels(BitmapSource bmp)
    {
        int[] p = new int[bmp.PixelWidth * bmp.PixelHeight];
        bmp.CopyPixels(p, bmp.PixelWidth * 4, 0);
        return p;
    }

    private static byte Alfa(int pixel) => (byte)((uint)pixel >> 24);

    [Teste]
    public void TamanhoLogicoEhODoNucleoDoAplicativoEODaArte()
    {
        // Uma fonte só para a janela, o sprite e o núcleo: 24 pixels de arte, 2 DIP cada.
        Afirmar.Igual(ConfiguracaoDoNucleo.DoAplicativo(SpriteProvisorio.TamanhoLogico).TamanhoDoItem, SpriteDoItem.TamanhoLogico, "o tamanho do núcleo");
        Afirmar.Igual(new TamanhoDip(2 * ItensPixel.Lado, 2 * ItensPixel.Lado), SpriteDoItem.TamanhoLogico, "24 × 24 pixels de arte a 2 DIP");
        Afirmar.Igual((int)ItensPixel.TamanhoLogicoDip, SpriteDoItem.TamanhoLogico.Largura, "o tamanho lógico da arte");
    }

    [Teste]
    public void CadaItemEmCadaDpi_TamanhoFisico_AlfaZeroOuDuzentosECinquentaECinco_CantoTransparente()
    {
        foreach (Item item in Enum.GetValues<Item>())
        {
            foreach (int dpi in Dpis)
            {
                BitmapSource bmp = SpriteDoItem.Renderizar(item, dpi);
                TamanhoPx esperado = SpriteDoItem.TamanhoLogico.ParaPixels(dpi);
                string onde = $"{item} a {dpi} DPI";
                Afirmar.Igual((esperado.Largura, esperado.Altura), (bmp.PixelWidth, bmp.PixelHeight), $"{onde}: tamanho físico");
                Afirmar.Aproximado(dpi, bmp.DpiX, 0.01, $"{onde}: DPI do bitmap (1 pixel do bitmap = 1 pixel da tela)");
                Afirmar.Verdadeiro(bmp.IsFrozen, $"{onde}: congelado");
                int[] p = Pixels(bmp);
                Afirmar.Igual(0, p.Count(x => Alfa(x) is not 0 and not 255), $"{onde}: pixels com alfa entre 1 e 254");
                Afirmar.Verdadeiro(p.Any(x => Alfa(x) == 255), $"{onde}: há pixels opacos");
                Afirmar.Igual((byte)0, Alfa(p[0]), $"{onde}: o canto de cima à esquerda deixa o clique passar");
            }
        }
    }

    [Teste]
    public void A96E192Dpi_EhODesenhoDoChaoAmpliadoPixelAPixel()
    {
        // A 96 DPI, 2×; a 192, 4×: cada pixel de arte vira um bloco inteiro (vizinho mais próximo), como o ícone do menu.
        foreach (Item item in Enum.GetValues<Item>())
        {
            Tela desenho = ItensPixel.Desenhar(item.ToString().ToLowerInvariant());
            foreach ((int dpi, int fator) in new[] { (96, 2), (192, 4) })
            {
                uint[] esperado = IconesDoMenu.Ampliar(desenho, fator);
                uint[] obtido = [.. Pixels(SpriteDoItem.Renderizar(item, dpi)).Select(x => unchecked((uint)x))];
                Afirmar.Sequencia(esperado, obtido, $"{item} a {dpi} DPI");
            }
        }
    }

    [Teste]
    public void CachePorItemEDpi_UmDesenhoSoPorPar()
    {
        BitmapSource a = SpriteDoItem.Renderizar(Item.Cerveja, 144);
        long desenhados = SpriteDoItem.QuadrosRenderizados;
        Afirmar.Verdadeiro(ReferenceEquals(a, SpriteDoItem.Renderizar(Item.Cerveja, 144)), "o mesmo item no mesmo DPI vem do cache");
        Afirmar.Igual(desenhados, SpriteDoItem.QuadrosRenderizados, "sem desenhar de novo");
        Afirmar.Falso(ReferenceEquals(a, SpriteDoItem.Renderizar(Item.Cerveja, 168)), "outro DPI é outro desenho");
        Afirmar.Falso(ReferenceEquals(a, SpriteDoItem.Renderizar(Item.Vodka, 144)), "outro item é outro desenho");
        Afirmar.Verdadeiro(SpriteDoItem.BytesEmCache <= SpriteDoItem.OrcamentoDoCache, "o cache fica no orçamento");
    }

    [Teste]
    public void LimitesOpacosEPontosDeTeste_BatemComOsPixelsDoBitmap()
    {
        foreach (Item item in Enum.GetValues<Item>())
        {
            foreach (int dpi in Dpis)
            {
                BitmapSource bmp = SpriteDoItem.Renderizar(item, dpi);
                int[] p = Pixels(bmp);
                int w = bmp.PixelWidth, h = bmp.PixelHeight;
                int e = w, t = h, d = 0, b = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (Alfa(p[y * w + x]) != 255) continue;
                        e = Math.Min(e, x);
                        t = Math.Min(t, y);
                        d = Math.Max(d, x + 1);
                        b = Math.Max(b, y + 1);
                    }
                }
                string onde = $"{item} a {dpi} DPI";
                RetanguloPx limites = SpriteDoItem.LimitesOpacos(item, dpi);
                Afirmar.Igual(new RetanguloPx(e, t, d, b), limites, $"{onde}: os limites opacos são os dos pixels do bitmap");
                Afirmar.Igual(h, limites.Base, $"{onde}: o item pousa na última linha, como os pés");

                (PontoPx opaco, PontoPx transparente) = SpriteDoItem.PontosDeTeste(item, dpi);
                Afirmar.Igual((byte)255, Alfa(p[opaco.Y * w + opaco.X]), $"{onde}: o ponto opaco {opaco} está opaco");
                Afirmar.Verdadeiro(limites.Contem(opaco), $"{onde}: o ponto opaco fica dentro dos limites");
                Afirmar.Igual((byte)0, Alfa(p[transparente.Y * w + transparente.X]), $"{onde}: o ponto transparente {transparente} está transparente");
            }

            // A 96 DPI, os limites são os da arte em dobro.
            (int Esquerda, int Topo, int Direita, int Base) arte = ItensPixel.Desenhar(item.ToString().ToLowerInvariant()).Limites()!.Value;
            Afirmar.Igual(new RetanguloPx(2 * arte.Esquerda, 2 * arte.Topo, 2 * arte.Direita, 2 * arte.Base), SpriteDoItem.LimitesOpacos(item, 96), $"{item}: a arte em dobro");
        }
    }

    [Teste]
    public void ItemForaDoEnumLanca()
        => Afirmar.Lanca<ArgumentOutOfRangeException>(() => SpriteDoItem.Renderizar((Item)13, 96), "um item fora do enum não tem desenho");
}
