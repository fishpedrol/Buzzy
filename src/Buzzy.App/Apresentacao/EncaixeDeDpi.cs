using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Buzzy.Core;

namespace Buzzy.App.Apresentacao;

// Troca de DPI comum às janelas do personagem e dos itens:
// - no WM_GETDPISCALEDSIZE responde o tamanho do sprite no DPI novo; o escalado
//   linear do Windows aplicaria a escala duas vezes;
// - a imagem é dimensionada em DIP pra cada pixel do bitmap cair num pixel da
//   janela, mesmo antes do sprite novo chegar ou entre monitores de escala diferente.
// O WM_DPICHANGED fica com o WPF, que aplica o retângulo sugerido.
internal static class EncaixeDeDpi
{
    // wParam traz o DPI novo; lParam aponta um SIZE. DPI inválido: devolve falso
    // sem escrever e o Windows decide.
    internal static bool ResponderTamanhoEscalado(nint wParam, nint lParam, TamanhoDip tamanho)
    {
        int dpiNovo = (int)(long)wParam;
        if (dpiNovo <= 0 || lParam == 0) return false;
        TamanhoPx px = tamanho.ParaPixels(dpiNovo);
        Marshal.WriteInt32(lParam, 0, px.Largura);
        Marshal.WriteInt32(lParam, 4, px.Altura);
        return true;
    }

    // DIP = pixels × 96 / DPI.
    internal static (double Largura, double Altura) TamanhoPixelAPixel(int larguraPx, int alturaPx, double dpiDaJanela)
    {
        if (dpiDaJanela <= 0) throw new ArgumentOutOfRangeException(nameof(dpiDaJanela), dpiDaJanela, "O DPI é positivo.");
        return (larguraPx * 96.0 / dpiDaJanela, alturaPx * 96.0 / dpiDaJanela);
    }

    // A imagem usa Stretch.Fill. Sem bitmap ou com DPI inválido, nada muda.
    internal static void AjustarPixelAPixel(Image imagem, double dpiDaJanela)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        if (imagem.Source is not BitmapSource bitmap || dpiDaJanela <= 0) return;
        (imagem.Width, imagem.Height) = TamanhoPixelAPixel(bitmap.PixelWidth, bitmap.PixelHeight, dpiDaJanela);
    }
}
