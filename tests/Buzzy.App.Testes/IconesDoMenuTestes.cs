using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Ícones do menu nativo (DEC-027 e DEC-028, passo A4; crítica, C29 e C30): o rosto de cada emoção é o
/// recorte exato da célula de expressoes.png — (12, 0) com 40 × 32 pixels do parado, pela mesma
/// <see cref="Tela.Recortada"/> que a prévia usa —, o ícone de item é o desenho do chão de 24 × 24, e a
/// ampliação inteira por DPI é por vizinho mais próximo, em BGRA com alfa só 0 ou 255, com as linhas
/// invertidas para o DIB de baixo para cima.
/// </summary>
internal sealed class IconesDoMenuTestes
{
    // Geometria da grade de expressoes.png em tools/Buzzy.Identidade/PreviaPixel.cs: escala 8, 7 colunas,
    // margem de 10 e rótulo de 18 pixels, fundo claro.
    private const int Escala = 8, Colunas = 7, Margem = 10, Rotulo = 18;
    private const uint FundoDaPrevia = 0xFFF1EEE8;

    private static readonly string[] Humor =
        ["neutro", "feliz", "rindo", "curioso", "surpreso", "assustado", "sonolento", "bocejando", "dormindo", "travesso", "entediado", "pensativo", "empolgado", "determinado"];

    private static uint[] Pixels(BitmapSource bmp, out int largura)
    {
        var bgra = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
        largura = bgra.PixelWidth;
        int[] p = new int[bgra.PixelWidth * bgra.PixelHeight];
        bgra.CopyPixels(p, bgra.PixelWidth * 4, 0);
        return [.. p.Select(v => unchecked((uint)v))];
    }

    [Teste]
    public void RostoEhACelulaDeExpressoesPng()
    {
        string caminho = Path.Combine(Caminhos.Raiz, "assets", "identidade", "pixel", "previa", "expressoes.png");
        using FileStream arquivo = File.OpenRead(caminho);
        BitmapFrame png = new PngBitmapDecoder(arquivo, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        uint[] previa = Pixels(png, out int largura);
        Afirmar.Igual(Humor.Length, Rostos.DeHumor.Count);
        for (int i = 0; i < Humor.Length; i++)
        {
            Tela rosto = IconesDoMenu.Rosto(Humor[i]);
            Afirmar.Igual((40, 32), (rosto.Largura, rosto.Altura), Humor[i]);
            int x0 = Margem + i % Colunas * (rosto.Largura * Escala + Margem);
            int y0 = Margem + i / Colunas * (rosto.Altura * Escala + Margem + Rotulo);
            uint[] argb = rosto.ParaArgb();
            for (int y = 0; y < rosto.Altura * Escala; y++)
            {
                for (int x = 0; x < rosto.Largura * Escala; x++)
                {
                    uint esperado = argb[y / Escala * rosto.Largura + x / Escala];
                    if (esperado >> 24 == 0) esperado = FundoDaPrevia;
                    uint obtido = previa[(y0 + y) * largura + x0 + x];
                    if (obtido != esperado)
                        Afirmar.Falhar($"{Humor[i]}: pixel ({x / Escala},{y / Escala}) do ícone difere da célula da prévia: {obtido:X8} contra {esperado:X8} (prévia desatualizada ou recorte diferente)");
                }
            }
        }
    }

    [Teste]
    public void RostoEhORecorteDoParadoParaQualquerCara()
    {
        PosePixel parado = PosesPixel.Todas[0];
        foreach (string cara in Rostos.Expressoes.Keys)
        {
            Tela icone = IconesDoMenu.Rosto(cara), recorte = BonecoPixel.Desenhar(parado, cara).Recortada(12, 0, 40, 32);
            Afirmar.Igual((IconesDoMenu.LarguraDoRosto, IconesDoMenu.AlturaDoRosto), (icone.Largura, icone.Altura), cara);
            Afirmar.Sequencia(recorte.ParaArgb(), icone.ParaArgb(), cara);
        }
        Afirmar.Lanca<ArgumentException>(() => IconesDoMenu.Rosto("acelerado"), "cara desconhecida");
    }

    [Teste]
    public void RecortadaCopiaPixelAPixelEOForaFicaTransparente()
    {
        var t = new Tela(4, 3);
        t[0, 0] = Cor.Faixa;
        t[3, 2] = Cor.Branco;
        t[1, 1] = Cor.Pelo;
        Tela r = t.Recortada(1, 1, 4, 3);
        Afirmar.Igual((4, 3), (r.Largura, r.Altura));
        Afirmar.Igual(Cor.Pelo, r[0, 0]);
        Afirmar.Igual(Cor.Branco, r[2, 1]);
        Afirmar.Igual(Cor.Nada, r[3, 2], "fora da tela de origem, transparente");
        Afirmar.Igual(Cor.Faixa, t.Recortada(-1, -1, 2, 2)[1, 1], "recorte que começa fora");
        r[0, 0] = Cor.Neon;
        Afirmar.Igual(Cor.Pelo, t[1, 1], "o recorte é uma tela nova");
    }

    [Teste]
    public void ItemEhODesenhoDoChao()
    {
        foreach (string item in ItensPixel.Todos)
        {
            Tela icone = IconesDoMenu.Item(item);
            Afirmar.Igual((IconesDoMenu.LadoDoItem, IconesDoMenu.LadoDoItem), (icone.Largura, icone.Altura), item);
            Afirmar.Sequencia(ItensPixel.Desenhar(item).ParaArgb(), icone.ParaArgb(), item);
        }
    }

    [Teste]
    public void FatorPorDpi()
    {
        foreach ((int dpi, int fator) in new[] { (72, 1), (96, 1), (120, 1), (144, 1), (168, 1), (191, 1), (192, 2), (240, 2), (287, 2), (288, 3), (384, 4) })
            Afirmar.Igual(fator, IconesDoMenu.Fator(dpi), $"{dpi} DPI");
    }

    [Teste]
    public void AmpliarPorVizinhoMaisProximoComAlfaBinario()
    {
        foreach (Tela t in new[] { IconesDoMenu.Rosto("neutro"), IconesDoMenu.Item("vodka") })
        {
            uint[] origem = t.ParaArgb();
            for (int fator = 1; fator <= 4; fator++)
            {
                uint[] grande = IconesDoMenu.Ampliar(t, fator);
                int w = t.Largura * fator;
                Afirmar.Igual(w * t.Altura * fator, grande.Length, $"fator {fator}: tamanho");
                for (int y = 0; y < t.Altura * fator; y++)
                    for (int x = 0; x < w; x++)
                        if (grande[y * w + x] != origem[y / fator * t.Largura + x / fator])
                            Afirmar.Falhar($"fator {fator}: pixel ({x},{y}) não é o de ({x / fator},{y / fator})");
                Afirmar.Igual(0, grande.Count(p => (p >> 24) is not 0 and not 255), $"fator {fator}: alfa só 0 ou 255");
                Afirmar.Verdadeiro(grande.Where(p => p >> 24 == 0).All(p => p == 0), $"fator {fator}: o transparente é zero, já pré-multiplicado");
            }
        }
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => IconesDoMenu.Ampliar(new Tela(2, 2), 0), "fator zero");
    }

    [Teste]
    public void AmpliarFicaEmBgraNaMemoria()
    {
        var t = new Tela(1, 1);
        t[0, 0] = Cor.Faixa;
        byte[] bytes = BitConverter.GetBytes(IconesDoMenu.Ampliar(t, 1)[0]);
        Afirmar.Verdadeiro(BitConverter.IsLittleEndian, "x64 é little-endian");
        // #B83A37 opaco: B = 0x37, G = 0x3A, R = 0xB8, A = 0xFF, nesta ordem na memória do DIB de 32 bits.
        Afirmar.Sequencia(new byte[] { 0x37, 0x3A, 0xB8, 0xFF }, bytes);
    }

    [Teste]
    public void DeBaixoParaCimaInverteAsLinhas()
    {
        uint[] imagem = [1, 2, 3, 4, 5, 6];
        Afirmar.Sequencia(new uint[] { 5, 6, 3, 4, 1, 2 }, IconesDoMenu.DeBaixoParaCima(imagem, largura: 2), "3 linhas de 2");
        Afirmar.Sequencia(new uint[] { 1, 2, 3, 4, 5, 6 }, imagem, "não mexe na entrada");
        uint[] icone = IconesDoMenu.Ampliar(IconesDoMenu.Rosto("feliz"), 2);
        int largura = IconesDoMenu.LarguraDoRosto * 2;
        uint[] invertido = IconesDoMenu.DeBaixoParaCima(icone, largura);
        Afirmar.Sequencia(icone.Skip(icone.Length - largura), invertido.Take(largura), "a primeira linha do DIB é a última da imagem");
        Afirmar.Sequencia(icone, IconesDoMenu.DeBaixoParaCima(invertido, largura), "duas vezes volta ao original");
        Afirmar.Lanca<ArgumentException>(() => IconesDoMenu.DeBaixoParaCima([1, 2, 3], 2), "tamanho que não fecha linhas");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => IconesDoMenu.DeBaixoParaCima([1, 2], 0), "largura zero");
    }
}
