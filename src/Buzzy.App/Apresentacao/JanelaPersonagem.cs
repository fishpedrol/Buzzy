using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;

namespace Buzzy.App.Apresentacao;

// Janela do personagem: sem borda, do tamanho do sprite, transparência por pixel
// (layered do WPF), sempre no topo, fora da barra e do Alt+Tab, e NÃO ativa no clique.
// Converte o mouse em eventos de ponteiro do núcleo, em pixels físicos do desktop
// virtual, e só segura a captura durante um gesto começado no personagem. O Windows
// só entrega clique em pixel com alfa > 0; fora de um gesto, nada de outros apps chega.
internal sealed class JanelaPersonagem : Window
{
    // Pixel a pixel pelo DPI atual da janela (ver EncaixeDeDpi).
    private readonly Image _imagem = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        SnapsToDevicePixels = true,
    };

    private bool _capturando;

    // Ligado só durante o nosso ReleaseCapture, que manda WM_CAPTURECHANGED de forma
    // síncrona; sem isso o fim normal do gesto pareceria captura perdida.
    private bool _soltandoPorNos;

    // Tamanho lógico da escala em vigor; não muda durante a execução.
    private readonly TamanhoDip _tamanho;

    internal JanelaPersonagem() : this(SpriteProvisorio.TamanhoLogico)
    {
    }

    internal JanelaPersonagem(TamanhoDip tamanho)
    {
        _tamanho = tamanho;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Focusable = false;
        Title = "Buzzy";
        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeToContent = SizeToContent.Manual;
        Width = tamanho.Largura;
        Height = tamanho.Altura;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        RenderOptions.SetBitmapScalingMode(_imagem, BitmapScalingMode.NearestNeighbor);
        Content = _imagem;

        StateChanged += AoMudarEstado;
        DpiChanged += (_, e) =>
        {
            // O WPF também avisa quando só reavaliou o DPI sem mudar (logo depois de
            // mostrar a janela); isso não é mudança de topologia.
            int antes = (int)Math.Round(e.OldDpi.PixelsPerInchX);
            int depois = (int)Math.Round(e.NewDpi.PixelsPerInchX);
            EncaixeDeDpi.AjustarPixelAPixel(_imagem, e.NewDpi.PixelsPerInchX);
            if (antes != depois) DpiMudou?.Invoke(depois);
        };
    }

    internal nint Hwnd { get; private set; }

    // Pixels físicos, relógio monotônico em ms.
    internal event Action<EventoDePonteiro>? Ponteiro;

    // Troca de escala ou de monitor.
    internal event Action<int>? DpiMudou;

    // O Windows tentou minimizar; pra nós, minimizar é esconder.
    internal event Action? Minimizada;

    internal bool Capturando => _capturando;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Hwnd = new WindowInteropHelper(this).Handle;

        // Não ativar ao ser clicada e ficar fora da barra de tarefas e do Alt+Tab.
        nint estilo = Win32.GetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE, (nint)((long)estilo | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));

        HwndSource.FromHwnd(Hwnd)?.AddHook(Gancho);
    }

    internal void DefinirSprite(BitmapSource sprite)
    {
        _imagem.Source = sprite;
        EncaixeDeDpi.AjustarPixelAPixel(_imagem, VisualTreeHelper.GetDpi(this).PixelsPerInchX);
    }

    internal BitmapSource? Sprite => _imagem.Source as BitmapSource;

    // Pixels físicos, sem ativar nem mudar a ordem Z.
    internal void AplicarRetangulo(RetanguloPx r)
        => Win32.SetWindowPos(Hwnd, 0, r.Esquerda, r.Topo, r.Largura, r.Altura, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    // Pixels físicos; nulo se o Windows não informar.
    internal RetanguloPx? RetanguloReal()
        => Hwnd != 0 && Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    // Peer vazio: a imagem fica fora das árvores do leitor de tela.
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PeerVazio(this);

    internal TamanhoDip Tamanho => _tamanho;

    internal bool SempreNoTopo => Topmost;

    // Só por evento (partida ou comando), nunca periódico.
    internal void AplicarSempreNoTopo(bool ligado) => Topmost = ligado;

    // Sobe uma vez, sem ativar: topo do grupo topmost se o "sempre no topo" estiver
    // ligado, senão acima das janelas comuns. Só por ação explícita (mostrar, abrir o
    // painel) ou no fim da tela cheia; nunca por timer, pra não brigar por foco.
    internal void AoTopoDaFaixa()
    {
        if (Topmost) Win32.SetWindowPos(Hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        else Win32.AoTopoDaFaixaComum(Hwnd);
    }

    internal void Capturar()
    {
        if (_capturando || Hwnd == 0) return;
        Win32.SetCapture(Hwnd);
        _capturando = true;
    }

    // Não conta como captura perdida.
    internal void SoltarCaptura()
    {
        if (!_capturando) return;
        _capturando = false;
        _soltandoPorNos = true;
        try
        {
            Win32.ReleaseCapture();
        }
        finally
        {
            _soltandoPorNos = false;
        }
    }

    private nint Gancho(nint hwnd, int msg, nint wParam, nint lParam, ref bool tratado)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEACTIVATE:
                // O clique chega ao Buzzy, mas o aplicativo em uso continua com o foco.
                tratado = true;
                return Win32.MA_NOACTIVATE;

            case Win32.WM_GETDPISCALEDSIZE:
                // Vai mudar de DPI (outro monitor ou escala): responde o tamanho do sprite no DPI novo.
                if (!EncaixeDeDpi.ResponderTamanhoEscalado(wParam, lParam, _tamanho)) break;
                tratado = true;
                return 1;

            case Win32.WM_LBUTTONDOWN:
            case Win32.WM_LBUTTONDBLCLK:
                // O segundo botão pressionado de um clique duplo pode chegar como WM_LBUTTONDBLCLK;
                // quem decide o clique duplo é a arbitragem, pelas regras do sistema.
                Diagnostico.Evento("CLIQUE", ("botao", "esquerdo"), ("cliente", Cliente(lParam)));
                Ponteiro?.Invoke(new PonteiroPressionado(NaTela(hwnd, lParam), BotaoDoPonteiro.Esquerdo, Environment.TickCount64, Metricas()));
                tratado = true;
                return 0;

            case Win32.WM_MOUSEMOVE:
                // Fora de um gesto, passar o mouse sobre o personagem não interessa ao núcleo.
                if (!_capturando) break;
                Ponteiro?.Invoke(new PonteiroMovido(NaTela(hwnd, lParam), ((long)wParam & Win32.MK_LBUTTON) != 0, Environment.TickCount64));
                tratado = true;
                return 0;

            case Win32.WM_LBUTTONUP:
                Ponteiro?.Invoke(new PonteiroSolto(NaTela(hwnd, lParam), BotaoDoPonteiro.Esquerdo, Environment.TickCount64));
                tratado = true;
                return 0;

            case Win32.WM_RBUTTONDOWN:
                Ponteiro?.Invoke(new PonteiroPressionado(NaTela(hwnd, lParam), BotaoDoPonteiro.Direito, Environment.TickCount64, MetricasDeGesto.Padrao));
                tratado = true;
                return 0;

            case Win32.WM_RBUTTONUP:
                Diagnostico.Evento("CLIQUE", ("botao", "direito"), ("cliente", Cliente(lParam)));
                Ponteiro?.Invoke(new PonteiroSolto(NaTela(hwnd, lParam), BotaoDoPonteiro.Direito, Environment.TickCount64));
                tratado = true;
                return 0;

            case Win32.WM_CONTEXTMENU:
                // O menu sai do botão direito solto, pela arbitragem; a janela nunca tem foco de teclado.
                tratado = true;
                return 0;

            case Win32.WM_CANCELMODE:
                // O DefWindowProc já soltaria a captura; soltamos aqui pra não depender do WPF.
                // O WM_CAPTURECHANGED seguinte encerra o gesto como captura perdida.
                if (_capturando) Win32.ReleaseCapture();
                break;

            case Win32.WM_CAPTURECHANGED:
                // Único ponto de fim de gesto interrompido: Alt+Tab, tecla Windows, UAC ou outra
                // janela pegou o mouse. Por privacidade, não registra qual janela é a nova dona.
                if (_capturando && !_soltandoPorNos && lParam != hwnd)
                {
                    _capturando = false;
                    Diagnostico.Evento("CAPTURA", ("perdida", "sim"));
                    Ponteiro?.Invoke(new CapturaPerdida(Environment.TickCount64));
                }
                break;
        }
        return 0;
    }

    // No DPI atual da janela, que é o do monitor onde o personagem foi pressionado.
    private MetricasDeGesto Metricas()
    {
        uint dpi = (uint)Math.Max(1, Math.Round(VisualTreeHelper.GetDpi(this).PixelsPerInchX));
        return new MetricasDeGesto(
            Win32.GetSystemMetricsForDpi(Win32.SM_CXDRAG, dpi),
            Win32.GetSystemMetricsForDpi(Win32.SM_CYDRAG, dpi),
            Win32.GetSystemMetricsForDpi(Win32.SM_CXDOUBLECLK, dpi),
            Win32.GetSystemMetricsForDpi(Win32.SM_CYDOUBLECLK, dpi),
            (int)Math.Min(Win32.GetDoubleClickTime(), int.MaxValue));
    }

    // Cliente com sinal -> pixels físicos. A janela só se move nesta thread, então
    // ainda está onde estava quando a mensagem foi gerada.
    private static PontoPx NaTela(nint hwnd, nint lParam)
    {
        var p = new Win32.POINT { X = Win32.XComSinal(lParam), Y = Win32.YComSinal(lParam) };
        Win32.ClientToScreen(hwnd, ref p);
        return new PontoPx(p.X, p.Y);
    }

    private static string Cliente(nint lParam) => $"{Win32.XComSinal(lParam)},{Win32.YComSinal(lParam)}";

    private void AoMudarEstado(object? remetente, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        WindowState = WindowState.Normal;
        Minimizada?.Invoke();
    }
}
