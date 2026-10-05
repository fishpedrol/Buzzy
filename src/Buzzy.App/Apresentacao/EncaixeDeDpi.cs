using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Buzzy.Core;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// O que as janelas do personagem e dos itens fazem igual na troca de DPI (Fase 5, passo P14; protótipo P6), num lugar só:
/// <list type="bullet">
/// <item>no <c>WM_GETDPISCALEDSIZE</c>, o tamanho certo é o do sprite no DPI novo, e não o pedido escalado linearmente pelo
/// Windows, que já é o do DPI novo e ficaria com a escala aplicada duas vezes (<see cref="ResponderTamanhoEscalado"/>);</item>
/// <item>a imagem tem o tamanho, em DIP, que põe cada pixel do bitmap num pixel da janela pelo DPI atual dela
/// (<see cref="AjustarPixelAPixel"/>): no instante em que a janela já trocou de DPI e o sprite do DPI novo ainda não chegou,
/// ou com ela montada entre monitores de escala diferente, a pixel art não é redimensionada pela metade.</item>
/// </list>
/// O <c>WM_DPICHANGED</c> continua com o WPF, que aplica o retângulo sugerido (ARCHITECTURE.md 2.13.3).
/// </summary>
internal static class EncaixeDeDpi
{
    /// <summary>
    /// Responde o <c>WM_GETDPISCALEDSIZE</c>: escreve em <paramref name="lParam"/> (um SIZE) o <paramref name="tamanho"/> no
    /// DPI novo, que vem em <paramref name="wParam"/>. Devolve falso, sem escrever, com um DPI inválido: o Windows decide.
    /// </summary>
    internal static bool ResponderTamanhoEscalado(nint wParam, nint lParam, TamanhoDip tamanho)
    {
        int dpiNovo = (int)(long)wParam;
        if (dpiNovo <= 0 || lParam == 0) return false;
        TamanhoPx px = tamanho.ParaPixels(dpiNovo);
        Marshal.WriteInt32(lParam, 0, px.Largura);
        Marshal.WriteInt32(lParam, 4, px.Altura);
        return true;
    }

    /// <summary>O tamanho, em DIP, que leva um bitmap de <paramref name="larguraPx"/> × <paramref name="alturaPx"/> pixel a pixel
    /// numa janela de <paramref name="dpiDaJanela"/>: pixels × 96 / DPI.</summary>
    internal static (double Largura, double Altura) TamanhoPixelAPixel(int larguraPx, int alturaPx, double dpiDaJanela)
    {
        if (dpiDaJanela <= 0) throw new ArgumentOutOfRangeException(nameof(dpiDaJanela), dpiDaJanela, "O DPI é positivo.");
        return (larguraPx * 96.0 / dpiDaJanela, alturaPx * 96.0 / dpiDaJanela);
    }

    /// <summary>
    /// Dimensiona a <paramref name="imagem"/> (com <c>Stretch.Fill</c>) para o bitmap dela preencher a janela pixel a pixel,
    /// pelo <paramref name="dpiDaJanela"/>. Sem bitmap ou com um DPI inválido, nada muda.
    /// </summary>
    internal static void AjustarPixelAPixel(Image imagem, double dpiDaJanela)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        if (imagem.Source is not BitmapSource bitmap || dpiDaJanela <= 0) return;
        (imagem.Width, imagem.Height) = TamanhoPixelAPixel(bitmap.PixelWidth, bitmap.PixelHeight, dpiDaJanela);
    }
}
