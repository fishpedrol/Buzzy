using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.Core;
using Buzzy.Visual.Animacao;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// Sprite estático do Buzzy até a animação da Fase 6: o quadro "parado" da pixel art
/// (docs/IDENTIDADE_VISUAL.md, DEC-018 e DEC-019), com o chapéu de palha. O quadro tem 64 × 64
/// pixels de arte e é ampliado por vizinho mais próximo até o tamanho físico do DPI do monitor
/// (2×, 3× e 4× exatos em 100%, 150% e 200%).
///
/// Regra de P1 (ARCHITECTURE.md 2.13.7, item 7): só alfa exatamente 0 deixa o clique passar. A pixel
/// art já tem só alfa 0 ou 255, e a ampliação sem suavização mantém isso.
/// </summary>
internal static class SpriteProvisorio
{
    /// <summary>
    /// Tamanho lógico padrão do personagem: o passo Médio da escala (DEC-038, item 9). O tamanho em vigor vem das
    /// configurações na partida e entra em cada renderização (a chave do cache o leva); nada aqui guarda o tamanho escolhido.
    /// </summary>
    internal static readonly TamanhoDip TamanhoLogico = Buzzy.Core.Personagem.ConfiguracaoDoNucleo.TamanhoDoPersonagem(Buzzy.Core.Personagem.EscalaDoPersonagem.Media);

    /// <summary>Escalas horizontal e vertical do corpo achatado no impacto (toon force, DEC-023), as da validação do manifesto.</summary>
    internal static readonly (double X, double Y) EscalaAchatada = Deformacoes.Achatado;

    /// <summary>Escalas horizontal e vertical do corpo esticado pela velocidade (toon force, DEC-023), as da validação do manifesto.</summary>
    internal static readonly (double X, double Y) EscalaEsticada = Deformacoes.Esticado;

    private static readonly Lazy<Tela> Parado = new(() => BonecoPixel.Desenhar(PosesPixel.Todas.First(p => p.Nome == "parado")));

    /// <summary>
    /// Orçamento do cache de quadros (crítica, C28): 16 MiB de pixels. A 100% cada quadro tem 64 KiB, e cabem 256; a
    /// 200%, 256 KiB, e cabem 64; a 300%, 576 KiB, e cabem 28.
    /// </summary>
    internal const long OrcamentoDoCache = 16L * 1024 * 1024;

    /// <summary>
    /// Quadros já renderizados, pelo quadro inteiro (pose, espelho, cara, item, efeito, fase, deformação e giro) e pelo
    /// DPI, limitados por <see cref="OrcamentoDoCache"/> (só na thread da interface).
    /// </summary>
    private static readonly CacheDeQuadros<(QuadroDoSprite Quadro, int Dpi, TamanhoDip Tamanho)> Cache = new(OrcamentoDoCache);

    /// <summary>Quantos quadros estão no cache (diagnóstico de memória).</summary>
    internal static int QuadrosEmCache => Cache.Quantos;

    /// <summary>Quantos bytes de pixels os quadros do cache ocupam (diagnóstico de memória).</summary>
    internal static long BytesEmCache => Cache.Bytes;

    /// <summary>Quantos quadros o cache já descartou para caber no orçamento.</summary>
    internal static long QuadrosDescartados => Cache.Descartados;

    /// <summary>Quantos quadros já foram desenhados, por não estarem no cache (o log SPRITE sai a cada um).</summary>
    internal static long QuadrosRenderizados { get; private set; }

    /// <summary>
    /// Renderiza o sprite no DPI do monitor (tamanho físico = <see cref="TamanhoLogico"/> no DPI
    /// dado), com alfa só 0 ou 255. Congelado.
    /// </summary>
    internal static BitmapSource Renderizar(int dpi) => Renderizar(dpi, TamanhoLogico);

    /// <summary>O quadro parado no DPI e no tamanho lógico dados (a escala em vigor, DEC-038).</summary>
    internal static BitmapSource Renderizar(int dpi, TamanhoDip logico)
    {
        TamanhoPx tamanho = logico.ParaPixels(dpi);
        return Bitmap(Parado.Value, tamanho.Largura, tamanho.Altura, dpi);
    }

    /// <summary>
    /// O quadro pedido (pose provisória da Fase 4, ou de uso, gesto e onda do tamagotchi) no DPI do monitor, renderizado
    /// e guardado no cache limitado: enquanto estiver lá, não é desenhado de novo. Mesmo tamanho lógico em todas as
    /// poses: a janela e a âncora (centro da base) não mudam.
    /// </summary>
    internal static BitmapSource Renderizar(QuadroDoSprite quadro, int dpi) => Renderizar(quadro, dpi, TamanhoLogico);

    /// <summary>O mesmo, no tamanho lógico dado (a escala em vigor, DEC-038): o tamanho faz parte da chave do cache.</summary>
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

    /// <summary>
    /// O quadro pedido em pixels de arte (64 × 64), antes da ampliação pelo DPI. A pose vem de
    /// <see cref="PosesPixel.PorNome"/>, que acha também as poses de uso e as dos gestos da onda (DEC-028). Nas poses de
    /// uso, a cara é a da própria pose (crítica, C10). A sobreposição da onda entra por cima de qualquer pose, e o
    /// modificador de pose dela só onde a pose o aceita (<see cref="EfeitosPixel.Modificavel"/>: no chão, nunca no uso).
    /// Espelho, giro e deformação vêm depois, como antes.
    /// </summary>
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

    /// <summary>PNG do ícone da bandeja (cabeça desenhada em 16 × 16), ampliado sem suavização.</summary>
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

    /// <summary>
    /// Pontos de teste em coordenadas locais do bitmap: um pixel opaco (na barriga) e um
    /// transparente (perto do canto superior esquerdo). Conferidos contra os pixels reais.
    /// </summary>
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

    /// <summary>
    /// Força cada pixel Pbgra32 a alfa 0 (tudo zero) ou 255 (cor desfeita da pré-multiplicação).
    /// </summary>
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

    /// <summary>
    /// Amplia a tela por vizinho mais próximo até largura × altura e gera um bitmap congelado, com alfa só 0 ou 255. Serve
    /// também ao sprite das janelas dos itens (<see cref="SpriteDoItem"/>).
    /// </summary>
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
