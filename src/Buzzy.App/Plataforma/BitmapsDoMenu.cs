using System.Runtime.InteropServices;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Os bitmaps dos ícones de uma abertura do menu nativo (DEC-027). Cada um é um DIB de 32 bits criado por
/// <c>CreateDIBSection</c> sem DC (<c>hdc = 0</c>, <c>BI_RGB</c>), com altura positiva — o DIB guarda a última linha da
/// imagem primeiro, por isso as linhas entram invertidas (<see cref="IconesDoMenu.DeBaixoParaCima"/>; crítica, C30) —
/// e preenchido por <see cref="Marshal.Copy(int[], int, nint, int)"/>: os pixels vêm do próprio desenho, nunca de um DC.
///
/// O menu não apaga o bitmap de um item (<c>DestroyMenu</c> não apaga <c>hbmpItem</c>): quem cria apaga, no
/// <see cref="Dispose"/>, que vem depois do <c>DestroyMenu</c>. <see cref="Criados"/> e <see cref="Apagados"/> vão para o
/// log do menu e precisam terminar iguais. Uma falha do Windows não derruba o menu: o item fica sem ícone, e o log leva
/// só o código do erro.
/// </summary>
internal sealed class BitmapsDoMenu : IDisposable
{
    private readonly List<nint> _vivos = [];
    private bool _descartado;

    /// <summary>Bitmaps criados nesta abertura do menu.</summary>
    internal int Criados { get; private set; }

    /// <summary>Bitmaps apagados por <see cref="Dispose"/>.</summary>
    internal int Apagados { get; private set; }

    /// <summary>Ícones que o Windows recusou criar: os itens deles ficam só com texto.</summary>
    internal int Falhas { get; private set; }

    /// <summary>
    /// Cria o bitmap de um ícone a partir dos pixels em 0xAARRGGBB, linha a linha de cima para baixo, com o alfa só 0 ou
    /// 255 (o transparente vale 0, já pré-multiplicado, como o menu espera num bitmap de 32 bits). Devolve 0 se o
    /// Windows recusar.
    /// </summary>
    internal nint Criar(uint[] pixelsDeCimaParaBaixo, int largura, int altura)
    {
        ArgumentNullException.ThrowIfNull(pixelsDeCimaParaBaixo);
        ArgumentOutOfRangeException.ThrowIfNegative(largura);
        ArgumentOutOfRangeException.ThrowIfNegative(altura);
        ObjectDisposedException.ThrowIf(_descartado, this);
        if (pixelsDeCimaParaBaixo.Length != (long)largura * altura)
            throw new ArgumentException($"São {pixelsDeCimaParaBaixo.Length} pixels para uma imagem de {largura} × {altura}.", nameof(pixelsDeCimaParaBaixo));

        var cabecalho = new Win32.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
            biWidth = largura,
            biHeight = altura, // positiva: de baixo para cima
            biPlanes = 1,
            biBitCount = 32,
            biCompression = Win32.BI_RGB,
        };
        nint bitmap = Win32.CreateDIBSection(0, ref cabecalho, Win32.DIB_RGB_COLORS, out nint bits, 0, 0);
        if (bitmap == 0)
        {
            Falhou(Marshal.GetLastPInvokeError());
            return 0;
        }
        _vivos.Add(bitmap);
        Criados++;
        if (bits == 0)
        {
            Falhou(0); // criado sem memória de pixels: apagado no Dispose, como os outros
            return 0;
        }

        uint[] linhas = IconesDoMenu.DeBaixoParaCima(pixelsDeCimaParaBaixo, largura);
        int[] dados = new int[linhas.Length];
        Buffer.BlockCopy(linhas, 0, dados, 0, linhas.Length * sizeof(uint));
        Marshal.Copy(dados, 0, bits, dados.Length);
        return bitmap;
    }

    /// <summary>Apaga todos os bitmaps criados. Chame depois do <c>DestroyMenu</c>: o menu não os apaga.</summary>
    public void Dispose()
    {
        foreach (nint bitmap in _vivos)
        {
            if (Win32.DeleteObject(bitmap)) Apagados++;
        }
        _vivos.Clear();
        _descartado = true;
    }

    private void Falhou(int codigo)
    {
        Falhas++;
        Diagnostico.Evento("MENU", ("icone", "falhou"), ("codigo", codigo));
    }
}
