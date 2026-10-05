using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// O cache de quadros do personagem (crítica, C28 e F14): limitado por bytes de pixels, 16 MiB no sprite, descartando o
/// quadro usado há mais tempo (LRU); e o sprite guarda cada quadro pelo quadro inteiro e pelo DPI, sem confundir dois
/// quadros que só diferem num campo.
/// </summary>
internal sealed class CacheDeQuadrosTestes
{
    private const long KiB = 1024;
    private const long MiB = 1024 * KiB;

    /// <summary>Um bitmap vazio de <paramref name="lado"/> × <paramref name="lado"/>, no formato do sprite (4 bytes por pixel).</summary>
    private static BitmapSource Quadro(int lado)
    {
        var b = new WriteableBitmap(lado, lado, 96, 96, PixelFormats.Pbgra32, null);
        b.Freeze();
        return b;
    }

    [Teste]
    public void OsBytesSaoOsDosPixels()
    {
        Afirmar.Igual(64 * KiB, CacheDeQuadros<int>.BytesDe(Quadro(128)), "100%: 128 × 128 × 4");
        Afirmar.Igual(256 * KiB, CacheDeQuadros<int>.BytesDe(Quadro(256)), "200%: 256 × 256 × 4");
        Afirmar.Igual(576 * KiB, CacheDeQuadros<int>.BytesDe(Quadro(384)), "300%: 384 × 384 × 4");
        Afirmar.Igual(16 * MiB, SpriteProvisorio.OrcamentoDoCache, "o orçamento do sprite (crítica, C28)");
    }

    [Teste]
    public void QuinhentosQuadrosCabemEmDezesseisMegasEOsMaisAntigosSaem()
    {
        var cache = new CacheDeQuadros<int>(16 * MiB);
        BitmapSource cem = Quadro(128);
        for (int i = 0; i < 500; i++)
        {
            Afirmar.Verdadeiro(cache.Guardar(i, cem), $"quadro {i} guardado");
            Afirmar.Verdadeiro(cache.Bytes <= 16 * MiB, $"depois do quadro {i}: {cache.Bytes} bytes");
            Afirmar.Igual(cache.Quantos * 64 * KiB, cache.Bytes, $"depois do quadro {i}: os bytes são a soma dos quadros");
        }
        Afirmar.Igual((256, 16 * MiB, 244L), (cache.Quantos, cache.Bytes, cache.Descartados), "cabem 256 quadros a 100%; saíram 244");
        for (int i = 0; i < 500; i++)
            Afirmar.Igual(i >= 244, cache.Contem(i), $"quadro {i}: ficam só os 256 mais recentes");

        // A 200%, cabem 64.
        var grande = new CacheDeQuadros<int>(16 * MiB);
        BitmapSource duzentos = Quadro(256);
        for (int i = 0; i < 100; i++) grande.Guardar(i, duzentos);
        Afirmar.Igual((64, 16 * MiB, 36L), (grande.Quantos, grande.Bytes, grande.Descartados), "cabem 64 quadros a 200%");
    }

    [Teste]
    public void UsarUmQuadroOFazFicarMaisTempo()
    {
        var cache = new CacheDeQuadros<string>(3 * 4 * KiB);
        BitmapSource q = Quadro(32); // 4 KiB
        cache.Guardar("A", q);
        cache.Guardar("B", q);
        cache.Guardar("C", q);
        Afirmar.Verdadeiro(cache.TentarObter("A", out BitmapSource? a) && ReferenceEquals(q, a), "A está no cache");
        cache.Guardar("D", q);
        Afirmar.Igual((true, false, true, true), (cache.Contem("A"), cache.Contem("B"), cache.Contem("C"), cache.Contem("D")), "sai o usado há mais tempo, B; A foi usado há pouco");
        cache.Guardar("E", q);
        Afirmar.Igual((true, false, false, true, true), (cache.Contem("A"), cache.Contem("B"), cache.Contem("C"), cache.Contem("D"), cache.Contem("E")), "depois, C");
        Afirmar.Igual(2L, cache.Descartados, "dois descartados");
        Afirmar.Falso(cache.TentarObter("B", out BitmapSource? b), "B não está mais");
        Afirmar.Nulo(b, "sem quadro");
        // Conferir sem usar (Contem) não renova: A continua o mais antigo depois de D e E.
        cache.Guardar("F", q);
        Afirmar.Igual((false, true, true, true), (cache.Contem("A"), cache.Contem("D"), cache.Contem("E"), cache.Contem("F")), "A sai antes de D e E");
    }

    [Teste]
    public void GuardarDeNovoTrocaOQuadroSemContarDuasVezes()
    {
        var cache = new CacheDeQuadros<string>(64 * KiB);
        BitmapSource pequeno = Quadro(32), maior = Quadro(64);
        cache.Guardar("A", pequeno);
        cache.Guardar("A", maior);
        Afirmar.Igual((1, 16 * KiB, 0L), (cache.Quantos, cache.Bytes, cache.Descartados), "um quadro só, com os bytes do novo");
        Afirmar.Verdadeiro(cache.TentarObter("A", out BitmapSource? a) && ReferenceEquals(maior, a), "o quadro novo");
    }

    [Teste]
    public void UmQuadroMaiorQueOOrcamentoNaoEntraNemTiraOsOutros()
    {
        var cache = new CacheDeQuadros<string>(8 * KiB);
        cache.Guardar("A", Quadro(32));
        Afirmar.Falso(cache.Guardar("B", Quadro(64)), "16 KiB não cabem em 8 KiB");
        Afirmar.Igual((1, 4 * KiB, 0L, true, false), (cache.Quantos, cache.Bytes, cache.Descartados, cache.Contem("A"), cache.Contem("B")), "o cache fica como estava");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => new CacheDeQuadros<int>(0), "orçamento zero");
    }

    [Teste]
    public void OSpriteGuardaCadaQuadroPeloQuadroInteiroEPeloDpi()
    {
        var baseDoTeste = new QuadroDoSprite("parado", false, "tonto") { Efeito = EfeitoVisual.Coracoes, Fase = 1 };
        BitmapSource primeiro = SpriteProvisorio.Renderizar(baseDoTeste, 96);
        long antes = SpriteProvisorio.QuadrosRenderizados;
        Afirmar.Verdadeiro(ReferenceEquals(primeiro, SpriteProvisorio.Renderizar(baseDoTeste, 96)), "o mesmo quadro vem do cache");
        Afirmar.Igual(antes, SpriteProvisorio.QuadrosRenderizados, "nada desenhado de novo");

        // Um DPI que nenhum outro teste usa: o quadro é desenhado agora, uma vez.
        BitmapSource emOutroDpi = SpriteProvisorio.Renderizar(baseDoTeste, 101);
        Afirmar.Igual(antes + 1, SpriteProvisorio.QuadrosRenderizados, "outro DPI é outro quadro");
        Afirmar.Diferente(primeiro.PixelWidth, emOutroDpi.PixelWidth, "do tamanho do outro DPI");

        // Cada campo do quadro entra na chave: mudar um só dá outro bitmap, com outros pixels.
        var usando = new QuadroDoSprite("bebendo-2", false, null) { Item = "vodka" };
        (string Campo, QuadroDoSprite De, QuadroDoSprite Para)[] variacoes =
        [
            ("pose", baseDoTeste, baseDoTeste with { Pose = "sentado" }),
            ("espelho", baseDoTeste, baseDoTeste with { Espelhado = true }),
            ("cara", baseDoTeste, baseDoTeste with { Expressao = "bebado" }),
            ("efeito", baseDoTeste, baseDoTeste with { Efeito = EfeitoVisual.Estrelinhas }),
            ("fase", baseDoTeste, baseDoTeste with { Fase = 2 }),
            ("deformação", baseDoTeste, baseDoTeste with { Deformacao = Deformacao.Achatado }),
            ("giro", baseDoTeste, baseDoTeste with { Giro = Giro.Horario }),
            ("item", usando, usando with { Item = "cerveja" }),
            ("sem item", usando, usando with { Item = null }),
        ];
        foreach ((string campo, QuadroDoSprite de, QuadroDoSprite para) in variacoes)
        {
            BitmapSource a = SpriteProvisorio.Renderizar(de, 96), b = SpriteProvisorio.Renderizar(para, 96);
            Afirmar.Falso(ReferenceEquals(a, b), $"{campo}: outro quadro no cache");
            Afirmar.Verdadeiro(Pixels(a).Zip(Pixels(b)).Any(p => p.First != p.Second), $"{campo}: outros pixels");
        }
    }

    private static int[] Pixels(BitmapSource bmp)
    {
        int[] p = new int[bmp.PixelWidth * bmp.PixelHeight];
        bmp.CopyPixels(p, bmp.PixelWidth * 4, 0);
        return p;
    }

    [Teste]
    public void OCacheDoSpriteFicaNoOrcamento()
    {
        // 300 quadros diferentes a 96 DPI (64 KiB cada) passam do orçamento: o cache fica nos 256 mais recentes.
        long descartadosAntes = SpriteProvisorio.QuadrosDescartados;
        string[] caras = ["neutro", "feliz", "rindo", "curioso", "surpreso"];
        int n = 0;
        foreach (string pose in new[] { "andando-1", "andando-2", "andando-3", "andando-4", "parado" })
            foreach (string cara in caras)
                foreach (EfeitoVisual efeito in EfeitosPixel.Todos.Take(4))
                    for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                    {
                        SpriteProvisorio.Renderizar(new QuadroDoSprite(pose, true, cara) { Efeito = efeito, Fase = fase }, 96);
                        n++;
                        Afirmar.Verdadeiro(SpriteProvisorio.BytesEmCache <= SpriteProvisorio.OrcamentoDoCache, $"depois de {n} quadros: {SpriteProvisorio.BytesEmCache} bytes");
                    }
        Afirmar.Igual(300, n, "quadros desenhados");
        Afirmar.Verdadeiro(SpriteProvisorio.QuadrosEmCache <= 256, $"{SpriteProvisorio.QuadrosEmCache} quadros de até 64 KiB no cache");
        Afirmar.Verdadeiro(SpriteProvisorio.QuadrosDescartados >= descartadosAntes + 300 - 256, $"descartados: {SpriteProvisorio.QuadrosDescartados - descartadosAntes}");
    }
}
