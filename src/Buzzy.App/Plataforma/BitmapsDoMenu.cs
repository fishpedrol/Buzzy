using System.Runtime.InteropServices;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Plataforma;

// Bitmaps dos ícones de uma abertura do menu nativo: DIB 32 bits via CreateDIBSection
// sem DC, altura positiva. O DIB guarda a última linha primeiro, por isso as linhas
// entram invertidas. Os pixels vêm do nosso desenho, nunca de um DC.
//
// DestroyMenu não apaga hbmpItem: quem cria apaga, no Dispose depois do DestroyMenu.
// Criados e Apagados vão pro log e têm que bater. Se o Windows falhar, o item só
// fica sem ícone.
internal sealed class BitmapsDoMenu : IDisposable
{
    private readonly List<nint> _vivos = [];
    private bool _descartado;

    internal int Criados { get; private set; }

    internal int Apagados { get; private set; }

    // Ícones que o Windows recusou; esses itens ficam só com texto.
    internal int Falhas { get; private set; }

    // Pixels 0xAARRGGBB de cima pra baixo, alfa só 0 ou 255: o transparente é 0, então
    // já está pré-multiplicado como o menu espera. Devolve 0 se o Windows recusar.
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
            biHeight = altura, // positiva = de baixo pra cima
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
            Falhou(0); // sem memória de pixels; o Dispose apaga igual
            return 0;
        }

        uint[] linhas = IconesDoMenu.DeBaixoParaCima(pixelsDeCimaParaBaixo, largura);
        int[] dados = new int[linhas.Length];
        Buffer.BlockCopy(linhas, 0, dados, 0, linhas.Length * sizeof(uint));
        Marshal.Copy(dados, 0, bits, dados.Length);
        return bitmap;
    }

    // Chamar depois do DestroyMenu.
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
