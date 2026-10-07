using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.Core;
using Buzzy.Visual.Animacao;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

// Sprite do Buzzy: quadros de 64 × 64 px de arte (com o chapéu de palha) ampliados
// por vizinho mais próximo até o tamanho físico no DPI (2×, 3×, 4× exatos em 100%,
// 150%, 200%). Só alfa exatamente 0 deixa o clique passar; a arte já é 0 ou 255 e
// a ampliação sem suavização mantém isso.
internal static class SpriteProvisorio
{
    // Escala Média. A escala em vigor vem das configurações e entra na chave do cache;
    // nada aqui guarda o tamanho escolhido.
    internal static readonly TamanhoDip TamanhoLogico = Buzzy.Core.Personagem.ConfiguracaoDoNucleo.TamanhoDoPersonagem(Buzzy.Core.Personagem.EscalaDoPersonagem.Media);

    // Mesmas escalas (X, Y) que a validação do manifesto usa.
    internal static readonly (double X, double Y) EscalaAchatada = Deformacoes.Achatado;

    internal static readonly (double X, double Y) EscalaEsticada = Deformacoes.Esticado;

    private static readonly Lazy<Tela> Parado = new(() => BonecoPixel.Desenhar(PosesPixel.Todas.First(p => p.Nome == "parado")));

    // 16 MiB de pixels: cabem 256 quadros a 100% (64 KiB), 64 a 200%, 28 a 300%.
    internal const long OrcamentoDoCache = 16L * 1024 * 1024;

    // Chave: o quadro inteiro, o DPI e o tamanho lógico. Só na thread da interface.
    private static readonly CacheDeQuadros<(QuadroDoSprite Quadro, int Dpi, TamanhoDip Tamanho)> Cache = new(OrcamentoDoCache);

    internal static int QuadrosEmCache => Cache.Quantos;

    internal static long BytesEmCache => Cache.Bytes;

    internal static long QuadrosDescartados => Cache.Descartados;

    // Só os que não estavam no cache; o log SPRITE sai a cada um.
    internal static long QuadrosRenderizados { get; private set; }

    // Quadro parado, congelado, alfa só 0 ou 255.
    internal static BitmapSource Renderizar(int dpi) => Renderizar(dpi, TamanhoLogico);

    internal static BitmapSource Renderizar(int dpi, TamanhoDip logico)
    {
        TamanhoPx tamanho = logico.ParaPixels(dpi);
        return Bitmap(Parado.Value, tamanho.Largura, tamanho.Altura, dpi);
    }

    // Mesmo tamanho lógico em todas as poses, pra janela e âncora (centro da base) não mudarem.
    internal static BitmapSource Renderizar(QuadroDoSprite quadro, int dpi) => Renderizar(quadro, dpi, TamanhoLogico);

    internal static BitmapSource Renderizar(QuadroDoSprite quadro, int dpi, TamanhoDip logico)
    {
        if (Cache.TentarObter((quadro, dpi, logico), out BitmapSource? pronto)) return pronto;
        Tela tela = Compor(quadro);
        TamanhoPx tamanho = logico.ParaPixels(dpi);
        BitmapSource bmp = Bitmap(tela, tamanho.Largura, tamanho.Altura, dpi);
        QuadrosRenderizados++;
        Cache.Guardar((quadro, dpi, logico), bmp);
        return bmp;
    }

    // Em pixels de arte (64 × 64), antes da ampliação. Pose de uso usa a própria cara.
    // A sobreposição da onda vai por cima de tudo, mas o modificador de pose só onde
    // a pose aceita (no chão, nunca no uso). Espelho, giro e deformação por último.
    internal static Tela Compor(QuadroDoSprite quadro)
    {
        PosePixel pose = PosesPixel.PorNome(quadro.Pose)
            ?? throw new ArgumentException($"Pose desconhecida: {quadro.Pose}.", nameof(quadro));
        string? expressao = UsosPixel.EhDeUso(pose) ? null : quadro.Expressao;
        if (EfeitosPixel.Modificavel(pose)) pose = EfeitosPixel.Modificar(pose, quadro.Efeito, quadro.Fase);
        Tela tela = BonecoPixel.Desenhar(pose, expressao, quadro.Item, quadro.Efeito, quadro.Fase);
        if (quadro.Espelhado) tela = tela.Espelhada();
        if (quadro.Giro == Giro.MeiaVolta) tela = tela.Girada(horario: true).Girada(horario: true);
        else if (quadro.Giro != Giro.Nenhum) tela = tela.Girada(horario: quadro.Giro == Giro.Horario);
        return quadro.Deformacao switch
        {
            Deformacao.Achatado => tela.Deformada(EscalaAchatada.X, EscalaAchatada.Y),
            Deformacao.Esticado => tela.Deformada(EscalaEsticada.X, EscalaEsticada.Y),
            _ => tela,
        };
    }

    // Ícone da bandeja: cabeça de 16 × 16 ampliada sem suavização.
    internal static byte[] IconePng(int ladoPx)
    {
        if (ladoPx <= 0) throw new ArgumentOutOfRangeException(nameof(ladoPx));
        BitmapSource icone = Bitmap(Icone.Desenhar(), ladoPx, ladoPx, 96);

        var codificador = new PngBitmapEncoder();
        codificador.Frames.Add(BitmapFrame.Create(icone));
        using var memoria = new MemoryStream();
        codificador.Save(memoria);
        return memoria.ToArray();
    }

    // Um pixel opaco (barriga) e um transparente (perto do canto), conferidos no bitmap.
    internal static (PontoPx Opaco, PontoPx Transparente) PontosDeTeste(BitmapSource bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        int largura = bmp.PixelWidth, altura = bmp.PixelHeight;
        int[] pixels = new int[largura * altura];
        bmp.CopyPixels(pixels, largura * 4, 0);

        byte Alfa(int x, int y) => (byte)((uint)pixels[y * largura + x] >> 24);

        var opaco = new PontoPx(largura / 2, (int)(altura * 0.62));
        var transparente = new PontoPx(Math.Max(1, largura / 32), Math.Max(1, altura / 32));

        if (Alfa(opaco.X, opaco.Y) != 255)
            throw new InvalidOperationException($"O ponto opaco de teste {opaco} não está opaco no sprite.");
        if (Alfa(transparente.X, transparente.Y) != 0)
            throw new InvalidOperationException($"O ponto transparente de teste {transparente} não está transparente no sprite.");
        return (opaco, transparente);
    }

    // Pbgra32: alfa < 128 vira tudo zero; o resto vira 255, desfazendo a pré-multiplicação.
    internal static void Limiarizar(int[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        for (int i = 0; i < pixels.Length; i++)
        {
            uint p = (uint)pixels[i];
            uint a = p >> 24;
            if (a == 255) continue;
            if (a < 128)
            {
                pixels[i] = 0;
                continue;
            }
            uint r = Math.Min(255u, ((p >> 16) & 0xFF) * 255 / a);
            uint g = Math.Min(255u, ((p >> 8) & 0xFF) * 255 / a);
            uint b = Math.Min(255u, (p & 0xFF) * 255 / a);
            pixels[i] = unchecked((int)(0xFF000000u | (r << 16) | (g << 8) | b));
        }
    }

    // Vizinho mais próximo, congelado, alfa só 0 ou 255. O SpriteDoItem usa também.
    internal static BitmapSource Bitmap(Tela tela, int largura, int altura, int dpi)
    {
        uint[] origem = tela.ParaArgb();
        int[] pixels = new int[largura * altura];
        for (int y = 0; y < altura; y++)
        {
            int sy = y * tela.Altura / altura;
            for (int x = 0; x < largura; x++)
            {
                int sx = x * tela.Largura / largura;
                pixels[y * largura + x] = unchecked((int)origem[sy * tela.Largura + sx]);
            }
        }
        // Alfa só 0 ou 255: em Pbgra32, a cor pré-multiplicada é a própria cor.
        Limiarizar(pixels);

        var bmp = new WriteableBitmap(largura, altura, dpi, dpi, PixelFormats.Pbgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, largura, altura), pixels, largura * 4, 0);
        bmp.Freeze();
        return bmp;
    }
}
