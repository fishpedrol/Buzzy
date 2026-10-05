using System.Runtime.InteropServices;
using Buzzy.App.Plataforma;
using Buzzy.App.Testes.Integracao;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Os bitmaps dos ícones do menu (DEC-027; crítica, C30 e seção 4): DIB de 32 bits sem DC, com altura positiva (a
/// primeira linha na memória é a última da imagem), preenchido com os pixels do rosto, e todo bitmap criado apagado no
/// Dispose. Só GDI na memória do processo de testes: nenhuma janela abre.
/// </summary>
internal sealed class BitmapsDoMenuTestes
{
    [Teste]
    public void EstruturaDeLeituraDoDibTemOTamanhoDoWindows()
        => Afirmar.Igual(104, Marshal.SizeOf<NativoTeste.DIBSECTION>(), "DIBSECTION em x64");

    [Teste]
    public void Criar_DibDe32BitsDeBaixoParaCima_ComOsPixelsDoRosto()
    {
        const int Fator = 2;
        Tela rosto = IconesDoMenu.Rosto("feliz");
        int largura = rosto.Largura * Fator, altura = rosto.Altura * Fator;
        uint[] imagem = IconesDoMenu.Ampliar(rosto, Fator);
        Afirmar.Verdadeiro(imagem.Any(p => p >> 24 == 0) && imagem.Any(p => p >> 24 == 255), "o rosto tem transparente e opaco");

        using var bitmaps = new BitmapsDoMenu();
        nint bitmap = bitmaps.Criar(imagem, largura, altura);
        Afirmar.Diferente((nint)0, bitmap, "bitmap criado");

        // GetObject informa a altura sem o sinal: a orientação é conferida abaixo, pelo que o Windows enxerga.
        NativoTeste.DIBSECTION dib = NativoTeste.LerDib(bitmap);
        Afirmar.Igual((80, 64, 32, 1), (dib.dsBmih.biWidth, Math.Abs(dib.dsBmih.biHeight), (int)dib.dsBmih.biBitCount, (int)dib.dsBmih.biPlanes),
            "80 × 64 a 2×, 32 bits");
        Afirmar.Igual(Win32.BI_RGB, dib.dsBmih.biCompression, "BI_RGB");
        Afirmar.Igual(largura * 4, dib.dsBm.bmWidthBytes, "linhas de 4 bytes por pixel, sem enchimento");

        // Na memória, de baixo para cima: a linha y é a linha (altura − 1 − y) da imagem, em BGRA, com o alfa.
        int[] memoria = new int[largura * altura];
        Marshal.Copy(dib.dsBm.bmBits, memoria, 0, memoria.Length);
        for (int y = 0; y < altura; y++)
        {
            for (int x = 0; x < largura; x++)
            {
                uint esperado = imagem[(altura - 1 - y) * largura + x], obtido = unchecked((uint)memoria[y * largura + x]);
                if (obtido != esperado)
                    Afirmar.Falhar($"pixel ({x},{y}) da memória do DIB: {obtido:X8}; esperado o ({x},{altura - 1 - y}) da imagem, {esperado:X8}");
            }
        }

        // E o Windows enxerga o rosto em pé: lido de cima para baixo pelo GDI, a linha y é a linha y da imagem. Só a
        // cor é comparada; o alfa já foi conferido na memória.
        uint[] vista = NativoTeste.ImagemDoBitmap(bitmap, largura, altura);
        for (int i = 0; i < vista.Length; i++)
        {
            if ((vista[i] & 0x00FFFFFF) != (imagem[i] & 0x00FFFFFF))
                Afirmar.Falhar($"o Windows enxerga o pixel ({i % largura},{i / largura}) como {vista[i] & 0x00FFFFFF:X6}, e a imagem tem {imagem[i] & 0x00FFFFFF:X6}: o rosto ficou de cabeça para baixo?");
        }
    }

    [Teste]
    public void Dispose_ApagaTodosOsBitmaps_CriadosIgualApagados_EOGdiVoltaAoPatamar()
    {
        uint[] neutro = IconesDoMenu.Ampliar(IconesDoMenu.Rosto("neutro"), 1);
        using (var aquecimento = new BitmapsDoMenu()) aquecimento.Criar(neutro, 40, 32);
        uint antes = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS);

        var bitmaps = new BitmapsDoMenu();
        var criados = new List<nint>();
        for (int i = 0; i < 30; i++) criados.Add(bitmaps.Criar(neutro, 40, 32));
        Afirmar.Verdadeiro(criados.All(b => b != 0) && criados.Distinct().Count() == 30, "30 bitmaps diferentes");
        Afirmar.Igual((30, 0, 0), (bitmaps.Criados, bitmaps.Apagados, bitmaps.Falhas));
        Afirmar.Igual(antes + 30, NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), "cada bitmap é um objeto GDI do processo");

        bitmaps.Dispose();
        Afirmar.Igual((30, 30), (bitmaps.Criados, bitmaps.Apagados), "criados = apagados");
        Afirmar.Igual(antes, NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), "o GDI volta ao patamar");
        Afirmar.Falso(criados.Any(NativoTeste.ObjetoGdiVivo), "nenhum dos handles continua vivo");

        bitmaps.Dispose();
        Afirmar.Igual((30, 30), (bitmaps.Criados, bitmaps.Apagados), "um segundo Dispose não apaga de novo");
        Afirmar.Lanca<ObjectDisposedException>(() => bitmaps.Criar(neutro, 40, 32), "depois do Dispose, nada é criado");
    }

    [Teste]
    public void FalhaDoCreateDibSection_DevolveZeroSemExcecao_EOItemFicaSemIcone()
    {
        using var bitmaps = new BitmapsDoMenu();
        Afirmar.Igual((nint)0, bitmaps.Criar([], 1, 0), "altura zero: o Windows recusa o DIB");
        Afirmar.Igual((0, 0, 1), (bitmaps.Criados, bitmaps.Apagados, bitmaps.Falhas), "nada criado, uma falha contada");

        // Erros de quem chama, não do Windows.
        Afirmar.Lanca<ArgumentException>(() => bitmaps.Criar([1, 2, 3], 2, 2), "pixels que não fecham a imagem");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => bitmaps.Criar([], 1, -1), "altura negativa (DIB de cima para baixo) não é aceita");
        Afirmar.Igual((0, 1), (bitmaps.Criados, bitmaps.Falhas));
    }
}
