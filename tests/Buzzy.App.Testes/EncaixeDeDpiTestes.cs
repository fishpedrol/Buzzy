using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O tratamento de DPI comum às janelas do personagem e dos itens (Fase 5, passo P14; protótipo P6), sem janela: a resposta
/// do WM_GETDPISCALEDSIZE, o tamanho que põe o bitmap pixel a pixel e, conferido na fonte, que as duas janelas usam o mesmo
/// código. A troca de escala real, com monitores de DPI diferente, é [HW] (S5 e S10).
/// </summary>
internal sealed class EncaixeDeDpiTestes
{
    // O WM_GETDPISCALEDSIZE pede o tamanho no DPI novo: o do sprite nesse DPI (128 DIP a 144 DPI são 192 px), escrito no SIZE do
    // lParam; com um DPI inválido, nada é escrito, e o Windows decide.
    [Teste]
    public void TamanhoEscalado_OSpriteNoDpiNovo_NadaComDpiInvalido()
    {
        nint size = Marshal.AllocHGlobal(8);
        try
        {
            foreach ((int dpi, TamanhoDip tamanho, int largura, int altura) in new[] { (144, new TamanhoDip(128, 128), 192, 192), (192, new TamanhoDip(48, 48), 96, 96), (96, new TamanhoDip(128, 64), 128, 64) })
            {
                Afirmar.Verdadeiro(EncaixeDeDpi.ResponderTamanhoEscalado(dpi, size, tamanho), $"{dpi} DPI: respondeu");
                Afirmar.Igual((largura, altura), (Marshal.ReadInt32(size, 0), Marshal.ReadInt32(size, 4)), $"{dpi} DPI: o tamanho no DPI novo");
            }
            Marshal.WriteInt32(size, 0, 7);
            Marshal.WriteInt32(size, 4, 7);
            foreach (int invalido in new[] { 0, -96 })
            {
                Afirmar.Falso(EncaixeDeDpi.ResponderTamanhoEscalado(invalido, size, new TamanhoDip(128, 128)), $"{invalido}: não responde");
                Afirmar.Igual((7, 7), (Marshal.ReadInt32(size, 0), Marshal.ReadInt32(size, 4)), $"{invalido}: nada escrito");
            }
            Afirmar.Falso(EncaixeDeDpi.ResponderTamanhoEscalado(144, 0, new TamanhoDip(128, 128)), "sem o SIZE, não responde");
        }
        finally
        {
            Marshal.FreeHGlobal(size);
        }
    }

    // Pixel a pixel: o tamanho em DIP é pixels × 96 / DPI da janela. O sprite de 192 px feito para 144 DPI, numa janela que
    // ainda está a 96, fica com 192 DIP (192 px), e não 128 (o bitmap encolhido pela metade do caminho); a 144, 128 DIP.
    [Teste]
    public void PixelAPixel_PeloDpiDaJanela_ENaoPeloDoBitmap()
    {
        Afirmar.Igual((128.0, 128.0), EncaixeDeDpi.TamanhoPixelAPixel(128, 128, 96), "96 DPI");
        Afirmar.Igual((128.0, 128.0), EncaixeDeDpi.TamanhoPixelAPixel(192, 192, 144), "144 DPI");
        Afirmar.Igual((192.0, 192.0), EncaixeDeDpi.TamanhoPixelAPixel(192, 192, 96), "o bitmap de 144 numa janela a 96");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => EncaixeDeDpi.TamanhoPixelAPixel(128, 128, 0), "DPI zero");

        var imagem = new Image();
        EncaixeDeDpi.AjustarPixelAPixel(imagem, 96);
        Afirmar.Verdadeiro(double.IsNaN(imagem.Width), "sem bitmap, nada muda");
        imagem.Source = new WriteableBitmap(192, 192, 144, 144, PixelFormats.Pbgra32, null);
        EncaixeDeDpi.AjustarPixelAPixel(imagem, 96);
        Afirmar.Igual((192.0, 192.0), (imagem.Width, imagem.Height), "na janela a 96, o bitmap de 144 inteiro");
        EncaixeDeDpi.AjustarPixelAPixel(imagem, 144);
        Afirmar.Igual((128.0, 128.0), (imagem.Width, imagem.Height), "na janela a 144");
    }

    // As duas janelas usam o mesmo tratamento (a crítica, P14: a do item era uma cópia da do personagem): a imagem esticada ao
    // tamanho pixel a pixel, ajustado ao definir o sprite e na troca de DPI, e o WM_GETDPISCALEDSIZE respondido pelo auxiliar.
    [Teste]
    public void AsDuasJanelas_UsamOMesmoEncaixe_ConferidoNaFonte()
    {
        foreach (string arquivo in new[] { "JanelaPersonagem.cs", "JanelaDoItem.cs" })
        {
            string fonte = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Apresentacao", arquivo));
            Afirmar.Contem("Stretch = Stretch.Fill,", fonte);
            Afirmar.Verdadeiro(fonte.Split("EncaixeDeDpi.AjustarPixelAPixel(_imagem,").Length - 1 == 2, $"{arquivo}: ajusta ao definir o sprite e na troca de DPI");
            Afirmar.Contem("EncaixeDeDpi.ResponderTamanhoEscalado(wParam, lParam,", fonte);
            Afirmar.Falso(fonte.Contains("Marshal.WriteInt32", StringComparison.Ordinal), $"{arquivo}: sem a cópia do gancho");
        }
    }
}
