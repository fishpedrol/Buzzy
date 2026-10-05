using System.Globalization;
using Buzzy.Testes;

namespace Buzzy.Core.Testes;

/// <summary>Retângulos, pontos e tamanhos em pixels físicos (Geometria.cs).</summary>
internal static class GeometriaTestes
{
    // Monitor 1920x1080 na origem: último pixel incluído em (1919,1079).
    private static readonly RetanguloPx Tela = new(0, 0, 1920, 1080);

    // Secundário da máquina do usuário, à esquerda do principal: último pixel em (-1,1079).
    private static readonly RetanguloPx TelaNegativa = new(-1920, 0, 0, 1080);

    [Teste]
    public static void MedidasDoRetangulo()
    {
        var r = new RetanguloPx(-1920, -360, 640, 1080);
        Afirmar.Igual(2560, r.Largura, "largura");
        Afirmar.Igual(1440, r.Altura, "altura");
        Afirmar.Igual(new TamanhoPx(2560, 1440), r.Tamanho, "tamanho");
        Afirmar.Falso(r.Vazio, "não vazio");
        Afirmar.Verdadeiro(new RetanguloPx(5, 5, 5, 10).Vazio, "largura zero");
        Afirmar.Verdadeiro(new RetanguloPx(5, 5, 10, 5).Vazio, "altura zero");
        Afirmar.Verdadeiro(new RetanguloPx(10, 10, 5, 20).Vazio, "invertido");
        Afirmar.Igual(new RetanguloPx(-100, 50, 100, 250),
            RetanguloPx.DePosicaoETamanho(new PontoPx(-100, 50), new TamanhoPx(200, 200)), "de posição e tamanho");
    }

    [Teste]
    public static void ContemPontoIncluiEsquerdaETopoEExcluiDireitaEBase()
    {
        Afirmar.Verdadeiro(Tela.Contem(new PontoPx(0, 0)), "canto superior esquerdo");
        Afirmar.Verdadeiro(Tela.Contem(new PontoPx(1919, 1079)), "último pixel");
        Afirmar.Verdadeiro(Tela.Contem(new PontoPx(960, 540)), "meio");
        Afirmar.Falso(Tela.Contem(new PontoPx(1920, 500)), "coluna da borda direita (exclusiva)");
        Afirmar.Falso(Tela.Contem(new PontoPx(500, 1080)), "linha da borda inferior (exclusiva)");
        Afirmar.Falso(Tela.Contem(new PontoPx(-1, 500)), "coluna à esquerda da tela");
        Afirmar.Falso(Tela.Contem(new PontoPx(500, -1)), "linha acima da tela");
    }

    [Teste]
    public static void ContemPontoComCoordenadasNegativas()
    {
        Afirmar.Verdadeiro(TelaNegativa.Contem(new PontoPx(-1920, 0)), "canto superior esquerdo");
        Afirmar.Verdadeiro(TelaNegativa.Contem(new PontoPx(-1, 1079)), "último pixel");
        Afirmar.Falso(TelaNegativa.Contem(new PontoPx(0, 0)), "x = 0 já é do principal");
        Afirmar.Falso(TelaNegativa.Contem(new PontoPx(-1921, 0)), "à esquerda");
    }

    [Teste]
    public static void RetanguloVazioNaoContemPonto()
    {
        var vazio = new RetanguloPx(10, 10, 10, 20);
        Afirmar.Falso(vazio.Contem(new PontoPx(10, 10)));
        Afirmar.Falso(vazio.Contem(new PontoPx(10, 15)));
    }

    [Teste]
    public static void ContemRetanguloAceitaEncostarNasBordas()
    {
        Afirmar.Verdadeiro(Tela.Contem(Tela), "o próprio retângulo");
        Afirmar.Verdadeiro(Tela.Contem(new RetanguloPx(100, 100, 300, 300)), "no meio");
        Afirmar.Verdadeiro(Tela.Contem(new RetanguloPx(1720, 880, 1920, 1080)), "encostado no canto inferior direito");
        Afirmar.Verdadeiro(Tela.Contem(new RetanguloPx(0, 0, 1, 1)), "um pixel no canto");
        Afirmar.Verdadeiro(TelaNegativa.Contem(new RetanguloPx(-200, 832, 0, 1032)), "encostado na borda x = 0");
    }

    [Teste]
    public static void ContemRetanguloRecusaUmPixelParaForaEmCadaBorda()
    {
        Afirmar.Falso(Tela.Contem(new RetanguloPx(-1, 100, 199, 300)), "esquerda");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(100, -1, 300, 199)), "topo");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(1721, 100, 1921, 300)), "direita");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(100, 881, 300, 1081)), "base");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(-10, -10, 1930, 1090)), "maior que a tela");
        Afirmar.Falso(TelaNegativa.Contem(new RetanguloPx(-199, 832, 1, 1032)), "passa de x = 0");
    }

    [Teste]
    public static void RetanguloVazioNaoCabeNemContemNada()
    {
        Afirmar.Falso(Tela.Contem(new RetanguloPx(100, 100, 100, 300)), "largura zero dentro da tela");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(100, 100, 300, 100)), "altura zero dentro da tela");
        Afirmar.Falso(Tela.Contem(new RetanguloPx(300, 300, 100, 100)), "invertido");
        Afirmar.Falso(new RetanguloPx(0, 0, 0, 0).Contem(new RetanguloPx(0, 0, 1, 1)), "vazio não contém");
    }

    [Teste]
    public static void IntersectaQuandoHaAoMenosUmPixelEmComum()
    {
        var direita = new RetanguloPx(1919, 0, 3840, 1080);
        Afirmar.Verdadeiro(Tela.Intersecta(direita), "uma coluna em comum");
        Afirmar.Verdadeiro(direita.Intersecta(Tela), "simétrico");
        Afirmar.Verdadeiro(Tela.Intersecta(new RetanguloPx(100, 100, 200, 200)), "contido");
        Afirmar.Verdadeiro(new RetanguloPx(100, 100, 200, 200).Intersecta(Tela), "contém");
        Afirmar.Verdadeiro(Tela.Intersecta(new RetanguloPx(-100, -100, 1, 1)), "só o pixel (0,0)");
        Afirmar.Verdadeiro(TelaNegativa.Intersecta(new RetanguloPx(-1, 500, 10, 600)), "coordenadas negativas");
    }

    [Teste]
    public static void MonitoresQueSoEncostamNaoSeIntersectam()
    {
        Afirmar.Falso(Tela.Intersecta(new RetanguloPx(1920, 0, 3840, 1080)), "vizinho à direita");
        Afirmar.Falso(Tela.Intersecta(TelaNegativa), "vizinho à esquerda, em x negativo");
        Afirmar.Falso(Tela.Intersecta(new RetanguloPx(0, -1080, 1920, 0)), "vizinho acima");
        Afirmar.Falso(Tela.Intersecta(new RetanguloPx(0, 1080, 1920, 2160)), "vizinho abaixo");
        Afirmar.Falso(Tela.Intersecta(new RetanguloPx(1920, 1080, 3840, 2160)), "só a quina");
        Afirmar.Falso(Tela.Intersecta(new RetanguloPx(2120, 100, 3400, 1124)), "separado por um vão");
    }

    [Teste]
    public static void RetanguloVazioNuncaIntersecta()
    {
        var vazio = new RetanguloPx(100, 100, 100, 300);
        Afirmar.Falso(Tela.Intersecta(vazio), "vazio dentro da tela");
        Afirmar.Falso(vazio.Intersecta(Tela), "simétrico");
        Afirmar.Falso(vazio.Intersecta(vazio), "consigo mesmo");
    }

    [Teste]
    public static void DeslocadoMoveSemMudarOTamanho()
    {
        var r = new RetanguloPx(10, 20, 30, 60);
        Afirmar.Igual(new RetanguloPx(-5, 25, 15, 65), r.Deslocado(-15, 5));
        Afirmar.Igual(new RetanguloPx(-1910, 20, -1890, 60), r.Deslocado(-1920, 0));
        Afirmar.Igual(r, r.Deslocado(0, 0));
    }

    [Teste]
    public static void CentroArredondaParaMenosInfinito()
    {
        Afirmar.Igual(new PontoPx(960, 540), Tela.Centro, "tela par");
        Afirmar.Igual(new PontoPx(-960, 540), TelaNegativa.Centro, "tela em x negativo");
        Afirmar.Igual(new PontoPx(2, 1), new RetanguloPx(0, 0, 5, 3).Centro, "ímpar: 2,5 e 1,5 descem para 2 e 1");
        Afirmar.Igual(new PontoPx(-3, -2), new RetanguloPx(-5, -3, 0, 0).Centro, "ímpar negativo: -2,5 e -1,5 descem para -3 e -2");
        Afirmar.Igual(new PontoPx(7, -4), new RetanguloPx(7, -4, 8, -3).Centro, "um pixel");
    }

    [Teste]
    public static void DistanciaAoQuadradoEZeroDentro()
    {
        Afirmar.Igual(0L, Tela.DistanciaAoQuadrado(new PontoPx(960, 540)), "meio");
        Afirmar.Igual(0L, Tela.DistanciaAoQuadrado(new PontoPx(0, 0)), "primeiro pixel");
        Afirmar.Igual(0L, Tela.DistanciaAoQuadrado(new PontoPx(1919, 1079)), "último pixel");
    }

    [Teste]
    public static void DistanciaAoQuadradoEmCadaLadoUsaOUltimoPixelIncluido()
    {
        Afirmar.Igual(100L, Tela.DistanciaAoQuadrado(new PontoPx(-10, 500)), "esquerda");
        Afirmar.Igual(49L, Tela.DistanciaAoQuadrado(new PontoPx(500, -7)), "acima");
        Afirmar.Igual(1L, Tela.DistanciaAoQuadrado(new PontoPx(1920, 500)), "borda direita exclusiva fica a 1 px");
        Afirmar.Igual(100L, Tela.DistanciaAoQuadrado(new PontoPx(1929, 500)), "direita");
        Afirmar.Igual(1L, Tela.DistanciaAoQuadrado(new PontoPx(500, 1080)), "borda inferior exclusiva fica a 1 px");
        Afirmar.Igual(9L, Tela.DistanciaAoQuadrado(new PontoPx(500, 1082)), "abaixo");
    }

    [Teste]
    public static void DistanciaAoQuadradoNosCantos()
    {
        // Triângulo 3-4-5 a partir de cada canto: 3² + 4² = 25.
        Afirmar.Igual(25L, Tela.DistanciaAoQuadrado(new PontoPx(-3, -4)), "superior esquerdo");
        Afirmar.Igual(25L, Tela.DistanciaAoQuadrado(new PontoPx(1922, -4)), "superior direito");
        Afirmar.Igual(25L, Tela.DistanciaAoQuadrado(new PontoPx(-3, 1083)), "inferior esquerdo");
        Afirmar.Igual(25L, Tela.DistanciaAoQuadrado(new PontoPx(1922, 1083)), "inferior direito");
    }

    [Teste]
    public static void DistanciaAoQuadradoComCoordenadasNegativas()
    {
        Afirmar.Igual(36L, TelaNegativa.DistanciaAoQuadrado(new PontoPx(5, 500)), "à direita de x = -1");
        Afirmar.Igual(29L, TelaNegativa.DistanciaAoQuadrado(new PontoPx(-1925, -2)), "canto superior esquerdo: 5² + 2²");
        Afirmar.Igual(0L, TelaNegativa.DistanciaAoQuadrado(new PontoPx(-1, 1079)), "último pixel");
    }

    [Teste]
    public static void DistanciaAoQuadradoNaoTransbordaInt()
    {
        // 100000² + 100000² = 2·10¹⁰, acima de int.MaxValue.
        Afirmar.Igual(20_000_000_000L, new RetanguloPx(0, 0, 1, 1).DistanciaAoQuadrado(new PontoPx(100_000, 100_000)));
    }

    [Teste]
    public static void DistanciaAoRetanguloVazioLanca()
    {
        Afirmar.Lanca<InvalidOperationException>(() => new RetanguloPx(0, 0, 0, 10).DistanciaAoQuadrado(new PontoPx(0, 0)));
    }

    [Teste]
    public static void ParaPixelsEmCadaEscalaComum()
    {
        // Sprite par (200x200) e ímpar (101x151) em 100%, 125%, 150%, 175%, 200%, 250% e 300%.
        var par = new TamanhoDip(200, 200);
        var impar = new TamanhoDip(101, 151);

        Afirmar.Igual(new TamanhoPx(200, 200), par.ParaPixels(96), "par a 96");
        Afirmar.Igual(new TamanhoPx(250, 250), par.ParaPixels(120), "par a 120");
        Afirmar.Igual(new TamanhoPx(300, 300), par.ParaPixels(144), "par a 144");
        Afirmar.Igual(new TamanhoPx(350, 350), par.ParaPixels(168), "par a 168");
        Afirmar.Igual(new TamanhoPx(400, 400), par.ParaPixels(192), "par a 192");
        Afirmar.Igual(new TamanhoPx(500, 500), par.ParaPixels(240), "par a 240");
        Afirmar.Igual(new TamanhoPx(600, 600), par.ParaPixels(288), "par a 288");

        Afirmar.Igual(new TamanhoPx(101, 151), impar.ParaPixels(96), "ímpar a 96");
        Afirmar.Igual(new TamanhoPx(126, 189), impar.ParaPixels(120), "ímpar a 120: 126,25 e 188,75");
        Afirmar.Igual(new TamanhoPx(152, 227), impar.ParaPixels(144), "ímpar a 144: 151,5 e 226,5");
        Afirmar.Igual(new TamanhoPx(177, 264), impar.ParaPixels(168), "ímpar a 168: 176,75 e 264,25");
        Afirmar.Igual(new TamanhoPx(202, 302), impar.ParaPixels(192), "ímpar a 192");
        Afirmar.Igual(new TamanhoPx(253, 378), impar.ParaPixels(240), "ímpar a 240: 252,5 e 377,5");
        Afirmar.Igual(new TamanhoPx(303, 453), impar.ParaPixels(288), "ímpar a 288");
    }

    [Teste]
    public static void ParaPixelsArredondaAMetadeParaLongeDeZero()
    {
        // Todas as contas dão metade exata; o arredondamento bancário (metade para o par)
        // daria um valor menor em módulo em cada um destes casos.
        Afirmar.Igual(new TamanhoPx(3, 13), new TamanhoDip(2, 10).ParaPixels(120), "2,5 e 12,5");
        Afirmar.Igual(new TamanhoPx(5, 11), new TamanhoDip(3, 7).ParaPixels(144), "4,5 e 10,5");
        Afirmar.Igual(new TamanhoPx(11, 25), new TamanhoDip(6, 14).ParaPixels(168), "10,5 e 24,5");
        Afirmar.Igual(new TamanhoPx(3, 13), new TamanhoDip(1, 5).ParaPixels(240), "2,5 e 12,5");
        Afirmar.Igual(new TamanhoPx(-5, -11), new TamanhoDip(-3, -7).ParaPixels(144), "-4,5 e -10,5 vão para longe de zero");
        Afirmar.Igual(new TamanhoPx(0, 0), new TamanhoDip(0, 0).ParaPixels(192), "zero");
    }

    [Teste]
    public static void ParaPixelsComDpiInvalidoLanca()
    {
        var tamanho = new TamanhoDip(200, 200);
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => tamanho.ParaPixels(0), "DPI zero");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => tamanho.ParaPixels(-96), "DPI negativo");
    }

    [Teste]
    public static void TextoDosTiposDeGeometria()
    {
        // O texto é para exibição e usa a cultura atual; o formato é conferido na invariante.
        string[] textos = Cultura.Com(CultureInfo.InvariantCulture, () => new[]
        {
            new PontoPx(-1920, 0).ToString(),
            new TamanhoPx(200, 150).ToString(),
            new TamanhoDip(200, 150).ToString(),
            new RetanguloPx(-1920, 0, 0, 1032).ToString(),
        });
        Afirmar.Sequencia(["(-1920,0)", "200x150", "200x150 DIP", "(-1920,0)-(0,1032)"], textos);
    }
}
