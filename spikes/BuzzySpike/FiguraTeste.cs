using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BuzzySpike;

// Interior em pixels locais da figura, com o alfa exato.
internal sealed record Banda(string Nome, byte Alfa, Interop.RECT Interior)
{
    // Onde o teste clica.
    public Interop.POINT Centro => new(
        (Interior.Left + Interior.Right) / 2,
        (Interior.Top + Interior.Bottom) / 2);
}

// Faixas com alfa 0, 1, 255 e 128, pra ver que só alfa exatamente 0 deixa o clique passar.
// Gerada em código pra o alfa ficar exato, sem compressão nem perfil de cor no caminho.
internal static class FiguraTeste
{
    internal const int Largura = 200;
    internal const int Altura = 200;

    private const int ColunaRotulo = 40;   // faixa opaca à esquerda, sempre clicável
    private const int AlturaBanda = 50;
    private const int Contorno = 2;        // moldura opaca, para a faixa ser localizável na tela

    // Pbgra32 lido como Int32 little-endian é 0xAARRGGBB, com o RGB já multiplicado pelo alfa.
    private const int PixelAlfa0 = unchecked((int)0x00000000);   // A=0
    private const int PixelAlfa1 = unchecked((int)0x01010101);   // A=1,   branco pré-multiplicado
    private const int PixelAlfa128 = unchecked((int)0x80000080); // A=128, azul pré-multiplicado

    private static readonly (string Nome, byte Alfa, Color CorContorno)[] _definicao =
    [
        ("alfa0",   0,   Color.FromRgb(0xE0, 0x40, 0x40)),  // vermelho
        ("alfa1",   1,   Color.FromRgb(0xE0, 0xA0, 0x40)),  // laranja
        ("alfa255", 255, Color.FromRgb(0x20, 0x80, 0x50)),  // verde
        ("alfa128", 128, Color.FromRgb(0x40, 0x70, 0xE0)),  // azul
    ];

    internal static IReadOnlyList<Banda> Bandas { get; } = Construir();

    private static List<Banda> Construir()
    {
        var lista = new List<Banda>();
        for (int i = 0; i < _definicao.Length; i++)
        {
            int topo = i * AlturaBanda;
            lista.Add(new Banda(
                _definicao[i].Nome,
                _definicao[i].Alfa,
                new Interop.RECT
                {
                    Left = ColunaRotulo + Contorno,
                    Top = topo + Contorno,
                    Right = Largura - Contorno,
                    Bottom = topo + AlturaBanda - Contorno,
                }));
        }
        return lista;
    }

    // Desenha molduras e rótulos primeiro e só depois sobrescreve o interior com o alfa exato,
    // pro antialiasing do WPF não sujar o que é medido.
    // margemAlfa1: troca alfa 0 por 1 em tudo. É o plano B do arraste se a captura simples
    // falhar; a janela toda passa a pegar clique enquanto isso estiver ligado.
    internal static BitmapSource Criar(bool margemAlfa1 = false)
    {
        int[] pixels = Rasterizar(DesenharMolduras());

        for (int i = 0; i < Bandas.Count; i++)
        {
            Banda b = Bandas[i];
            int? valor = b.Alfa switch
            {
                0 => PixelAlfa0,
                1 => PixelAlfa1,
                128 => PixelAlfa128,
                _ => null,   // alfa 255 já está desenhado como preenchimento opaco
            };
            if (valor is null) continue;

            for (int y = b.Interior.Top; y < b.Interior.Bottom; y++)
                for (int x = b.Interior.Left; x < b.Interior.Right; x++)
                    pixels[y * Largura + x] = valor.Value;
        }

        if (margemAlfa1)
        {
            for (int i = 0; i < pixels.Length; i++)
                if ((pixels[i] & unchecked((int)0xFF000000)) == 0)
                    pixels[i] = PixelAlfa1;
        }

        return DoArranjo(pixels, Largura, Altura);
    }

    private static DrawingVisual DesenharMolduras()
    {
        var visual = new DrawingVisual();
        using DrawingContext dc = visual.RenderOpen();

        // Coluna opaca de controle: clique aqui é sempre da janela.
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x28)),
            null,
            new Rect(0, 0, ColunaRotulo, Altura));

        var tipo = new Typeface("Segoe UI");
        var tintaRotulo = Brushes.White;

        for (int i = 0; i < _definicao.Length; i++)
        {
            (string nome, byte alfa, Color cor) = _definicao[i];
            int topo = i * AlturaBanda;
            Banda banda = Bandas[i];

            if (alfa == 255)
            {
                dc.DrawRectangle(
                    new SolidColorBrush(Color.FromRgb(0x3C, 0xB3, 0x71)),
                    null,
                    new Rect(banda.Interior.Left, banda.Interior.Top,
                             banda.Interior.Largura, banda.Interior.Altura));
            }

            // Moldura pra achar a faixa na tela mesmo com o interior invisível.
            var caneta = new Pen(new SolidColorBrush(cor), Contorno);
            dc.DrawRectangle(null, caneta, new Rect(
                ColunaRotulo + Contorno / 2.0,
                topo + Contorno / 2.0,
                Largura - ColunaRotulo - Contorno,
                AlturaBanda - Contorno));

            var texto = new FormattedText(
                alfa.ToString(CultureInfo.InvariantCulture),
                CultureInfo.GetCultureInfo("pt-BR"),
                FlowDirection.LeftToRight,
                tipo,
                14,
                tintaRotulo,
                1.0);
            dc.DrawText(texto, new Point(4, topo + (AlturaBanda - texto.Height) / 2));

            dc.DrawRectangle(new SolidColorBrush(cor), null,
                new Rect(ColunaRotulo - 5, topo + 6, 4, AlturaBanda - 12));
        }

        return visual;
    }

    private static int[] Rasterizar(Visual visual)
    {
        var alvo = new RenderTargetBitmap(Largura, Altura, 96, 96, PixelFormats.Pbgra32);
        alvo.Render(visual);

        int[] pixels = new int[Largura * Altura];
        alvo.CopyPixels(pixels, Largura * 4, 0);
        return pixels;
    }

    private static BitmapSource DoArranjo(int[] pixels, int largura, int altura)
    {
        var bmp = new WriteableBitmap(largura, altura, 96, 96, PixelFormats.Pbgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, largura, altura), pixels, largura * 4, 0);
        bmp.Freeze();
        return bmp;
    }

    // Disco girando sobre alfa 0. Cada quadro é diferente pra forçar o WPF a recompor de verdade,
    // em vez de reaproveitar a mesma imagem.
    internal static IReadOnlyList<BitmapSource> CriarQuadrosAnimacao(int quantidade = 8)
    {
        var quadros = new List<BitmapSource>(quantidade);
        const double raioOrbita = 56;
        const double raioDisco = 26;

        for (int i = 0; i < quantidade; i++)
        {
            double angulo = 2 * Math.PI * i / quantidade;
            double cx = Largura / 2.0 + raioOrbita * Math.Cos(angulo);
            double cy = Altura / 2.0 + raioOrbita * Math.Sin(angulo);

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                byte tom = (byte)(80 + 175 * i / Math.Max(1, quantidade - 1));
                dc.DrawEllipse(
                    new SolidColorBrush(Color.FromRgb(tom, 0x70, (byte)(255 - tom))),
                    new Pen(Brushes.White, 2),
                    new Point(cx, cy), raioDisco, raioDisco);
            }

            quadros.Add(DoArranjo(Rasterizar(visual), Largura, Altura));
        }

        return quadros;
    }
}
