using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;

namespace Buzzy.App.Apresentacao;

// Uma janela por item do tamagotchi, com a mesma receita da JanelaPersonagem: sem
// borda, 48 × 48 DIP, transparência por pixel, sempre no topo, fora da barra e do
// Alt+Tab, e NUNCA ativada (WS_EX_NOACTIVATE, MA_NOACTIVATE, ShowActivated falso).
// O Windows só entrega clique em pixel com alfa > 0, então só o desenho é clicável.
// O mouse vira eventos de ponteiro em pixels físicos do desktop virtual; a captura
// só existe durante um gesto começado no item, então fora disso nada de outros apps chega.
internal sealed class JanelaDoItem : Window, IJanelaDoItem
{
    // Peer vazio, como o do personagem.
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PeerVazio(this);

    // Pixel a pixel pelo DPI atual da janela (ver EncaixeDeDpi).
    private readonly Image _imagem = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        SnapsToDevicePixels = true,
    };

    private readonly TamanhoDip _tamanho;
    private bool _capturando;

    // Ligado só durante o nosso ReleaseCapture: o WM_CAPTURECHANGED síncrono do fim
    // normal do gesto não é captura perdida.
    private bool _soltandoPorNos;

    // Só a raiz fecha (remover ou sair); qualquer outro fechamento é recusado.
    private bool _fechandoPorNos;

    // tamanho é o lógico, o de ConfiguracaoDoNucleo.TamanhoDoItem.
    internal JanelaDoItem(int id, TamanhoDip tamanho)
    {
        Id = id;
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
        DpiChanged += (_, e) => EncaixeDeDpi.AjustarPixelAPixel(_imagem, e.NewDpi.PixelsPerInchX);
    }

    internal int Id { get; }

    public nint Hwnd { get; private set; }

    // Pixels físicos, relógio monotônico em ms.
    public event Action<EventoDePonteiro>? Ponteiro;

    public bool Capturando => _capturando;

    internal BitmapSource? Sprite => _imagem.Source as BitmapSource;

    // Pra ter o HWND antes de mostrar.
    internal void CriarSemMostrar() => new WindowInteropHelper(this).EnsureHandle();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Hwnd = new WindowInteropHelper(this).Handle;

        // Não ativar ao ser clicada e ficar fora da barra de tarefas e do Alt+Tab.
        nint estilo = Win32.GetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE, (nint)((long)estilo | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));

        HwndSource.FromHwnd(Hwnd)?.AddHook(Gancho);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_fechandoPorNos) e.Cancel = true;
        base.OnClosing(e);
    }

    public void DefinirSprite(BitmapSource sprite)
    {
        _imagem.Source = sprite;
        EncaixeDeDpi.AjustarPixelAPixel(_imagem, VisualTreeHelper.GetDpi(this).PixelsPerInchX);
    }

    // Pixels físicos, sem ativar nem mudar a ordem Z.
    public void AplicarRetangulo(RetanguloPx r)
        => Win32.SetWindowPos(Hwnd, 0, r.Esquerda, r.Topo, r.Largura, r.Altura, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    // Não ativa: ShowActivated é falso.
    public void Mostrar() => Show();

    public void Esconder() => Hide();

    // Logo abaixo do personagem na ordem Z.
    public void ColocarAbaixoDe(nint hwnd)
        => Win32.SetWindowPos(Hwnd, hwnd, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    // Só no gesto sobre o item, nunca periódico. Com "sempre no topo" desligado,
    // sobe acima das janelas comuns sem virar topmost.
    public void TrazerParaFrente()
    {
        if (Topmost) Win32.SetWindowPos(Hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        else Win32.AoTopoDaFaixaComum(Hwnd);
    }

    // Acompanha o do personagem.
    public void AplicarSempreNoTopo(bool ligado) => Topmost = ligado;

    // Pixels físicos; nulo se o Windows não informar.
    public RetanguloPx? RetanguloReal()
        => Hwnd != 0 && Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    public void Capturar()
    {
        if (_capturando || Hwnd == 0) return;
        Win32.SetCapture(Hwnd);
        _capturando = true;
    }

    // Não conta como captura perdida.
    public void SoltarCaptura()
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

    // O item saiu ou o Buzzy está saindo.
    public void Fechar()
    {
        _fechandoPorNos = true;
        Close();
    }

    private nint Gancho(nint hwnd, int msg, nint wParam, nint lParam, ref bool tratado)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEACTIVATE:
                // O clique chega ao item, mas o aplicativo em uso continua com o foco.
                tratado = true;
                return Win32.MA_NOACTIVATE;

            case Win32.WM_GETDPISCALEDSIZE:
                // Vai mudar de DPI: responde o tamanho do item no DPI novo.
                if (!EncaixeDeDpi.ResponderTamanhoEscalado(wParam, lParam, _tamanho)) break;
                tratado = true;
                return 1;

            case Win32.WM_LBUTTONDOWN:
            case Win32.WM_LBUTTONDBLCLK:
                // Quem decide o clique duplo é a arbitragem, pelas regras do sistema.
                Diagnostico.Evento("ITEM", ("clique", Id), ("botao", "esquerdo"), ("cliente", Cliente(lParam)));
                Ponteiro?.Invoke(new PonteiroPressionado(NaTela(hwnd, lParam), BotaoDoPonteiro.Esquerdo, Environment.TickCount64, Metricas()));
                tratado = true;
                return 0;

            case Win32.WM_MOUSEMOVE:
                // Fora de um gesto, passar o mouse sobre o item não interessa a ninguém.
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
                // Direito solto no item abre o menu do Buzzy, pela arbitragem.
                Diagnostico.Evento("ITEM", ("clique", Id), ("botao", "direito"), ("cliente", Cliente(lParam)));
                Ponteiro?.Invoke(new PonteiroSolto(NaTela(hwnd, lParam), BotaoDoPonteiro.Direito, Environment.TickCount64));
                tratado = true;
                return 0;

            case Win32.WM_CONTEXTMENU:
                // O menu sai do botão direito solto, pela arbitragem; a janela nunca tem foco de teclado.
                tratado = true;
                return 0;

            case Win32.WM_CANCELMODE:
                // Solta a captura; o WM_CAPTURECHANGED que vem em seguida encerra o gesto.
                if (_capturando) Win32.ReleaseCapture();
                break;

            case Win32.WM_CAPTURECHANGED:
                // Gesto interrompido (Alt+Tab, tecla Windows, UAC, outra janela pegou o mouse).
                // Por privacidade, não olha qual janela é a nova dona.
                if (_capturando && !_soltandoPorNos && lParam != hwnd)
                {
                    _capturando = false;
                    Diagnostico.Evento("ITEM", ("capturaPerdida", Id));
                    Ponteiro?.Invoke(new CapturaPerdida(Environment.TickCount64));
                }
                break;
        }
        return 0;
    }

    private static string Cliente(nint lParam) => $"{Win32.XComSinal(lParam)},{Win32.YComSinal(lParam)}";

    // No DPI atual da janela, que é o do monitor onde o item foi pressionado.
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

    // Cliente com sinal -> pixels físicos do desktop virtual.
    private static PontoPx NaTela(nint hwnd, nint lParam)
    {
        var p = new Win32.POINT { X = Win32.XComSinal(lParam), Y = Win32.YComSinal(lParam) };
        Win32.ClientToScreen(hwnd, ref p);
        return new PontoPx(p.X, p.Y);
    }

    private void AoMudarEstado(object? remetente, EventArgs e)
    {
        // O Windows pode minimizar a janela ao desconectar um monitor: volta ao normal
        // na hora, sem esconder o personagem.
        if (WindowState != WindowState.Minimized) return;
        WindowState = WindowState.Normal;
        Diagnostico.Evento("ITEM", ("minimizado", Id), ("restaurado", "sim"));
    }
}
