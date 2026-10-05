using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.Visual;

namespace Buzzy.Identidade;

/// <summary>
/// Gera as prévias da identidade visual e confere as fontes.
/// Uso: dotnet run --project tools/Buzzy.Identidade -c Release
///   (padrão)     pixel art (DEC-018): assets/identidade/pixel/ — folhas nativas (poses e itens do
///                tamagotchi, DEC-028) e prévias ampliadas, inclusive as animações de uso, as
///                sobreposições de efeito, os gestos da onda, a paranoia a 8× e os ícones do menu; confere que nenhuma
///                pose (as de estado e as dos gestos), quadro de uso (com cada item) ou efeito encosta
///                na borda do quadro de 64 × 64, que nenhuma cara deixa preenchimento na borda de cima
///                ou dos lados (o contorno seria cortado) e que cada item pousa na última linha da grade
///                de 24 × 24, com 1 pixel livre no topo e nas laterais.
///   --vetorial   direção vetorial arquivada (DEC-017, substituída): assets/identidade/arquivo-vetorial/.
/// Código de saída: 0 sem problemas; 1 com pose, quadro de uso, efeito ou item na borda/fora do quadro
/// ou parte ausente.
/// </summary>
internal static class Programa
{
    private static readonly Color Claro = Color.FromRgb(0xF4, 0xF1, 0xEC);
    private static readonly Color Escuro = Color.FromRgb(0x1E, 0x1E, 0x24);
    private static readonly Color Tinta = Color.FromRgb(0x21, 0x1B, 0x42);
    private static readonly Color TintaClara = Color.FromRgb(0xE8, 0xE4, 0xF4);

    [STAThread]
    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string raiz = LocalizarRaiz();
        if (!args.Contains("--vetorial"))
        {
            int naBorda = PreviaPixel.Gerar(raiz, Buzzy.Visual.Pixel.PosesPixel.Todas);
            Console.WriteLine(naBorda == 0
                ? "Pixel art: nenhuma pose, quadro de uso, efeito nem item encostado na borda do quadro."
                : $"Pixel art: {naBorda} pose(s), quadro(s) de uso, efeito(s) ou item(ns) na borda do quadro.");
            return naBorda == 0 ? 0 : 1;
        }
        string pasta = Path.Combine(raiz, "assets", "identidade", "arquivo-vetorial");
        string previa = Path.Combine(pasta, "previa");
        Directory.CreateDirectory(previa);

        Boneco boneco;
        using (FileStream svg = File.OpenRead(Path.Combine(pasta, "buzzy-partes.svg")))
        using (FileStream json = File.OpenRead(Path.Combine(pasta, "buzzy-poses.json")))
        {
            boneco = new Boneco(BibliotecaDePartes.Ler(svg), DefinicaoDoBoneco.Ler(json));
        }

        Console.WriteLine($"Partes: {boneco.Partes.Nomes.Count}; poses: {boneco.Definicao.Poses.Count}; expressões: {boneco.Definicao.Expressoes.Count}");
        int problemas = Conferir(boneco);

        List<string> poses = [.. boneco.Definicao.Poses.Keys];
        List<string> expressoes = [.. boneco.Definicao.Expressoes.Keys];

        Salvar(GradeDePoses(boneco, poses, Claro, Tinta, 2), Path.Combine(previa, "poses-claro.png"));
        Salvar(GradeDePoses(boneco, poses, Escuro, TintaClara, 2), Path.Combine(previa, "poses-escuro.png"));
        Salvar(GradeDeExpressoes(boneco, expressoes), Path.Combine(previa, "expressoes.png"));
        Salvar(GradeDeSilhuetas(boneco, poses), Path.Combine(previa, "silhuetas.png"));
        Salvar(TamanhoReal(boneco, poses), Path.Combine(previa, "tamanho-real.png"));
        Salvar(FolhaDeModelo(boneco, expressoes), Path.Combine(previa, "folha-de-modelo.png"));

        Console.WriteLine(problemas == 0 ? "Fontes conferidas: nenhuma pose fora do quadro, nenhuma parte ausente." : $"{problemas} problema(s) nas fontes.");
        return problemas == 0 ? 0 : 1;
    }

    private static int Conferir(Boneco boneco)
    {
        int problemas = 0;
        Quadro q = boneco.Definicao.Quadro;
        foreach (string pose in boneco.Definicao.Poses.Keys)
        {
            foreach (string expressao in boneco.Definicao.Expressoes.Keys)
            {
                try
                {
                    Rect r = boneco.Limites(pose, expressao);
                    const double folga = 0.5;
                    if (r.Left < -folga || r.Top < -folga || r.Right > q.Largura + folga || r.Bottom > q.Altura + folga)
                    {
                        // Uma linha por pose com a expressão padrão basta para o relatório.
                        if (expressao == boneco.Definicao.Poses[pose].Expressao)
                        {
                            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                                $"  FORA DO QUADRO: pose '{pose}' ocupa ({r.Left:0.0},{r.Top:0.0})-({r.Right:0.0},{r.Bottom:0.0})"));
                            problemas++;
                        }
                    }
                }
                catch (KeyNotFoundException e)
                {
                    Console.WriteLine($"  PARTE AUSENTE em '{pose}'/'{expressao}': {e.Message}");
                    problemas++;
                }
            }
        }
        return problemas;
    }

    // ------------------------------------------------------------------ grades

    private static BitmapSource GradeDePoses(Boneco boneco, List<string> poses, Color fundo, Color tinta, int escala)
    {
        const int colunas = 6, margem = 16, rotulo = 30, titulo = 56;
        int cel = (int)(128 * escala);
        int linhas = (poses.Count + colunas - 1) / colunas;
        int w = colunas * cel + (colunas + 1) * margem;
        int h = titulo + linhas * (cel + rotulo) + (linhas + 1) * margem;
        return Desenhar(w, h, fundo, dc =>
        {
            Texto(dc, "Buzzy — poses-chave (borda dura, como no aplicativo)", margem, 14, 22, tinta, true);
            for (int i = 0; i < poses.Count; i++)
            {
                int x = margem + (i % colunas) * (cel + margem);
                int y = titulo + margem + (i / colunas) * (cel + rotulo + margem);
                Pose p = boneco.Definicao.Poses[poses[i]];
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(40, tinta.R, tinta.G, tinta.B)), 1), new Rect(x + 0.5, y + 0.5, cel - 1, cel - 1));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, tinta.R, tinta.G, tinta.B)), 1), new Point(x, y + cel - 0.5), new Point(x + cel, y + cel - 0.5));
                if (p.Parede is double parede)
                {
                    double px = x + (boneco.Definicao.Quadro.AncoraX + parede) * escala;
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, tinta.R, tinta.G, tinta.B)), null, new Rect(px, y, x + cel - px, cel));
                }
                dc.DrawImage(boneco.Renderizar(poses[i], 96.0 * escala), new Rect(x, y, cel, cel));
                Texto(dc, $"{poses[i]}  ·  {p.Estado}", x, y + cel + 6, 13, tinta, false);
            }
        });
    }

    private static BitmapSource GradeDeExpressoes(Boneco boneco, List<string> expressoes)
    {
        const int colunas = 7, margem = 16, rotulo = 28, titulo = 56;
        const double escala = 3;
        var recorte = new Rect(22, 0, 84, 76);
        int cw = (int)(recorte.Width * escala), ch = (int)(recorte.Height * escala);
        int linhas = (expressoes.Count + colunas - 1) / colunas;
        int w = colunas * cw + (colunas + 1) * margem;
        int h = titulo + linhas * (ch + rotulo) + (linhas + 1) * margem;
        return Desenhar(w, h, Claro, dc =>
        {
            Texto(dc, "Buzzy — expressões (camadas do rosto e do topete)", margem, 14, 22, Tinta, true);
            for (int i = 0; i < expressoes.Count; i++)
            {
                int x = margem + (i % colunas) * (cw + margem);
                int y = titulo + margem + (i / colunas) * (ch + rotulo + margem);
                BitmapSource inteiro = boneco.Renderizar("parado", 96.0 * escala, expressoes[i]);
                var corte = new CroppedBitmap(inteiro, new Int32Rect((int)(recorte.X * escala), (int)(recorte.Y * escala), cw, ch));
                dc.DrawImage(corte, new Rect(x, y, cw, ch));
                Texto(dc, expressoes[i], x, y + ch + 6, 14, Tinta, false);
            }
        });
    }

    private static BitmapSource GradeDeSilhuetas(Boneco boneco, List<string> poses)
    {
        const int colunas = 10, margem = 12, rotulo = 22, titulo = 52, cel = 128;
        int linhas = (poses.Count + colunas - 1) / colunas;
        int w = colunas * cel + (colunas + 1) * margem;
        int h = titulo + linhas * (cel + rotulo) + (linhas + 1) * margem;
        return Desenhar(w, h, Claro, dc =>
        {
            Texto(dc, "Buzzy — teste de silhueta (uma cor só, tamanho real)", margem, 14, 20, Tinta, true);
            for (int i = 0; i < poses.Count; i++)
            {
                int x = margem + (i % colunas) * (cel + margem);
                int y = titulo + margem + (i / colunas) * (cel + rotulo + margem);
                dc.DrawImage(Silhueta(boneco.Renderizar(poses[i], 96)), new Rect(x, y, cel, cel));
                Texto(dc, poses[i], x, y + cel + 4, 11, Tinta, false);
            }
        });
    }

    private static BitmapSource TamanhoReal(Boneco boneco, List<string> poses)
    {
        const int margem = 8, cel = 128, titulo = 44;
        int w = poses.Count * (cel + margem) + margem;
        int h = titulo + 2 * (cel + margem) + margem;
        return Desenhar(w, h, Claro, dc =>
        {
            Texto(dc, "Buzzy — tamanho real (128 × 128 px a 100%), em fundo claro e escuro", margem, 12, 18, Tinta, true);
            dc.DrawRectangle(new SolidColorBrush(Escuro), null, new Rect(0, titulo + cel + margem * 1.5, w, cel + margem));
            for (int i = 0; i < poses.Count; i++)
            {
                int x = margem + i * (cel + margem);
                BitmapSource b = boneco.Renderizar(poses[i], 96);
                dc.DrawImage(b, new Rect(x, titulo, cel, cel));
                dc.DrawImage(b, new Rect(x, titulo + cel + margem * 2, cel, cel));
            }
        });
    }

    private static BitmapSource FolhaDeModelo(Boneco boneco, List<string> expressoes)
    {
        const int w = 1600, h = 1000, m = 28;
        (string Nome, string Hex)[] paleta =
        [
            ("pelo", "#574AA0"), ("pelo-sombra", "#43397E"), ("contorno", "#211B42"), ("rosto", "#F6DFC6"),
            ("rosto-sombra", "#E4C3A2"), ("orelha", "#EFC0A8"), ("menta", "#3CCFA3"), ("menta-escura", "#22A882"),
            ("boca", "#6B2344"), ("lingua", "#E8768F"),
        ];
        return Desenhar(w, h, Claro, dc =>
        {
            Texto(dc, "Buzzy — folha de modelo", m, 20, 34, Tinta, true);
            Texto(dc, "Sagui-acrobata violeta-índigo · topete de três tufos · cauda em espiral com ponta menta · sem roupa nem acessório", m, 64, 16, Tinta, false);

            // Pose principal grande.
            dc.DrawImage(boneco.Renderizar("parado", 96 * 4), new Rect(m, 100, 512, 512));
            Texto(dc, "parado (IDLE), 4×", m, 616, 14, Tinta, false);

            // Paleta.
            for (int i = 0; i < paleta.Length; i++)
            {
                double x = 580 + (i % 5) * 196, y = 110 + (i / 5) * 70;
                Color c = (Color)ColorConverter.ConvertFromString(paleta[i].Hex);
                dc.DrawRoundedRectangle(new SolidColorBrush(c), new Pen(new SolidColorBrush(Tinta), 1.5), new Rect(x, y, 44, 44), 8, 8);
                Texto(dc, paleta[i].Nome, x + 54, y + 4, 14, Tinta, true);
                Texto(dc, paleta[i].Hex, x + 54, y + 24, 13, Tinta, false);
            }

            // Ciclo de caminhada e poses de ação em 2×.
            string[] acao = ["andando-1", "andando-2", "andando-3", "andando-4", "no-ar"];
            for (int i = 0; i < acao.Length; i++)
            {
                double x = 580 + i * 200, y = 270;
                dc.DrawImage(boneco.Renderizar(acao[i], 96 * 1.5), new Rect(x, y, 192, 192));
                Texto(dc, acao[i], x, y + 196, 13, Tinta, false);
            }
            string[] acao2 = ["pendurado", "escalando-1", "caindo", "sentado", "segurado"];
            for (int i = 0; i < acao2.Length; i++)
            {
                double x = 580 + i * 200, y = 490;
                dc.DrawImage(boneco.Renderizar(acao2[i], 96 * 1.5), new Rect(x, y, 192, 192));
                Texto(dc, acao2[i], x, y + 196, 13, Tinta, false);
            }

            // Expressões (recorte da cabeça).
            var recorte = new Rect(22, 0, 84, 76);
            const double e = 1.3;
            int cw = (int)(recorte.Width * e), ch = (int)(recorte.Height * e);
            for (int i = 0; i < expressoes.Count; i++)
            {
                double x = m + i * (cw + 3), y = 745;
                BitmapSource inteiro = boneco.Renderizar("parado", 96 * e, expressoes[i]);
                dc.DrawImage(new CroppedBitmap(inteiro, new Int32Rect((int)(recorte.X * e), (int)(recorte.Y * e), cw, ch)), new Rect(x, y, cw, ch));
                Texto(dc, expressoes[i], x, y + ch + 4, 11, Tinta, false);
            }

            // Silhuetas.
            string[] sil = ["parado", "andando-1", "pendurado", "escalando-1", "no-ar", "sentado", "reagindo", "segurado"];
            for (int i = 0; i < sil.Length; i++)
            {
                double x = m + i * 104, y = 880;
                dc.DrawImage(Silhueta(boneco.Renderizar(sil[i], 96 * 0.75)), new Rect(x, y, 96, 96));
            }
            Texto(dc, "teste de silhueta", m + sil.Length * 104 + 10, 920, 14, Tinta, false);
        });
    }

    // ------------------------------------------------------------------ apoio

    private static BitmapSource Silhueta(BitmapSource origem)
    {
        int w = origem.PixelWidth, h = origem.PixelHeight;
        int[] p = new int[w * h];
        origem.CopyPixels(p, w * 4, 0);
        int cor = unchecked((int)(0xFF000000u | ((uint)Tinta.R << 16) | ((uint)Tinta.G << 8) | Tinta.B));
        for (int i = 0; i < p.Length; i++) if (((uint)p[i] >> 24) != 0) p[i] = cor;
        var bmp = new WriteableBitmap(w, h, origem.DpiX, origem.DpiY, PixelFormats.Pbgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, w, h), p, w * 4, 0);
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource Desenhar(int w, int h, Color fundo, Action<DrawingContext> desenhar)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(fundo), null, new Rect(0, 0, w, h));
            desenhar(dc);
        }
        var alvo = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        alvo.Render(visual);
        alvo.Freeze();
        return alvo;
    }

    private static void Texto(DrawingContext dc, string texto, double x, double y, double tamanho, Color cor, bool negrito)
    {
        var ft = new FormattedText(texto, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, negrito ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            tamanho, new SolidColorBrush(cor), 1.0);
        dc.DrawText(ft, new Point(x, y));
    }

    private static void Salvar(BitmapSource bmp, string caminho)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bmp));
        using FileStream f = File.Create(caminho);
        png.Save(f);
        Console.WriteLine($"  {Path.GetFileName(caminho)}: {bmp.PixelWidth}×{bmp.PixelHeight}");
    }

    private static string LocalizarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Buzzy.Build.props"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
