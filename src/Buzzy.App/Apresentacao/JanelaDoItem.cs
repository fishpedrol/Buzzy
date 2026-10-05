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

/// <summary>
/// Janela de um item do tamagotchi (DEC-028; D8 do desenho do app; crítica, C11 e L18), uma por item, com a receita da
/// <see cref="JanelaPersonagem"/> (ARCHITECTURE.md 2.13.1): sem borda, do tamanho do item (48 × 48 DIP), com transparência
/// por pixel (janela layered do WPF), sempre no topo, fora da barra de tarefas e do Alt+Tab, e que NUNCA é ativada nem tira
/// o foco do aplicativo em uso (WS_EX_NOACTIVATE, MA_NOACTIVATE, ShowActivated falso). O Windows só entrega a ela cliques
/// em pixels com alfa diferente de 0: só o desenho do item recebe clique, como no personagem.
///
/// Adaptador do ponteiro: as mensagens de mouse que o Windows entrega a esta janela viram eventos de ponteiro em pixels
/// físicos do desktop virtual, e a captura do mouse só existe enquanto a raiz pede (um gesto começado no item). Fora de um
/// gesto, nada do mouse de outros aplicativos chega (SECURITY.md 3.1).
///
/// O tratamento do DPI é o da janela do personagem, em <see cref="EncaixeDeDpi"/> (passo P14), com o tamanho do item.
/// </summary>
internal sealed class JanelaDoItem : Window, IJanelaDoItem
{
    /// <summary>Um peer vazio, como o do personagem (DEC-038, item 7).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PeerVazio(this);

    // Pixel a pixel pelo DPI atual da janela (EncaixeDeDpi, passo P14).
    private readonly Image _imagem = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        SnapsToDevicePixels = true,
    };

    private readonly TamanhoDip _tamanho;
    private bool _capturando;

    // Verdadeiro só durante o ReleaseCapture pedido pela raiz: o WM_CAPTURECHANGED síncrono do fim normal do gesto não
    // é uma captura perdida (a mesma lição do protótipo P3).
    private bool _soltandoPorNos;

    // Só a raiz fecha a janela de um item (remover ou sair); outro pedido de fechamento é recusado.
    private bool _fechandoPorNos;

    /// <param name="id">O Id do item no núcleo.</param>
    /// <param name="tamanho">O tamanho lógico do item (o de <c>ConfiguracaoDoNucleo.TamanhoDoItem</c>).</param>
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

    /// <summary>O Id do item no núcleo.</summary>
    internal int Id { get; }

    public nint Hwnd { get; private set; }

    /// <summary>Evento de ponteiro já normalizado (pixels físicos, relógio monotônico em ms).</summary>
    public event Action<EventoDePonteiro>? Ponteiro;

    /// <summary>Se a janela está com a captura do mouse de um gesto em curso sobre o item.</summary>
    public bool Capturando => _capturando;

    internal BitmapSource? Sprite => _imagem.Source as BitmapSource;

    /// <summary>Cria a janela, ainda escondida, para ter o HWND antes de mostrar.</summary>
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

    /// <summary>Posiciona e dimensiona a janela em pixels físicos, sem ativar nem mudar a ordem Z.</summary>
    public void AplicarRetangulo(RetanguloPx r)
        => Win32.SetWindowPos(Hwnd, 0, r.Esquerda, r.Topo, r.Largura, r.Altura, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    /// <summary>Mostra sem ativar (ShowActivated é falso).</summary>
    public void Mostrar() => Show();

    public void Esconder() => Hide();

    /// <summary>Logo abaixo de <paramref name="hwnd"/> (o personagem) na ordem Z, sem mover, redimensionar nem ativar.</summary>
    public void ColocarAbaixoDe(nint hwnd)
        => Win32.SetWindowPos(Hwnd, hwnd, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    /// <summary>
    /// No topo, sem ativar: só no gesto sobre o item, por evento (L17). Com o "sempre no topo" desligado (DEC-038, item 8),
    /// acima das janelas comuns, sem ficar topmost.
    /// </summary>
    public void TrazerParaFrente()
    {
        if (Topmost) Win32.SetWindowPos(Hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        else Win32.AoTopoDaFaixaComum(Hwnd);
    }

    /// <summary>Liga ou desliga o "sempre no topo" do item, junto com o do personagem (DEC-038, item 8).</summary>
    public void AplicarSempreNoTopo(bool ligado) => Topmost = ligado;

    /// <summary>Onde a janela está de fato, em pixels físicos; nulo se o Windows não informar.</summary>
    public RetanguloPx? RetanguloReal()
        => Hwnd != 0 && Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    /// <summary>Captura o mouse para o gesto em curso sobre o item.</summary>
    public void Capturar()
    {
        if (_capturando || Hwnd == 0) return;
        Win32.SetCapture(Hwnd);
        _capturando = true;
    }

    /// <summary>Solta a captura do gesto, sem que isso conte como captura perdida.</summary>
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

    /// <summary>Fecha de vez (o item saiu, ou o Buzzy está saindo).</summary>
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
                // A janela vai mudar de DPI: o tamanho do item no DPI novo (EncaixeDeDpi, o mesmo da janela do personagem).
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
                // O botão direito solto no item abre o menu do Buzzy, pela arbitragem (crítica, C15).
                Diagnostico.Evento("ITEM", ("clique", Id), ("botao", "direito"), ("cliente", Cliente(lParam)));
                Ponteiro?.Invoke(new PonteiroSolto(NaTela(hwnd, lParam), BotaoDoPonteiro.Direito, Environment.TickCount64));
                tratado = true;
                return 0;

            case Win32.WM_CONTEXTMENU:
                // O menu sai do botão direito solto, pela arbitragem; a janela nunca tem foco de teclado.
                tratado = true;
                return 0;

            case Win32.WM_CANCELMODE:
                // "Cancelar modos, como a captura do mouse": o WM_CAPTURECHANGED que vem em seguida encerra o gesto.
                if (_capturando) Win32.ReleaseCapture();
                break;

            case Win32.WM_CAPTURECHANGED:
                // Gesto interrompido (Alt+Tab, tecla Windows, UAC, ou outra janela ficou com o mouse). Só o fato vale,
                // nunca qual janela é a nova dona (SECURITY.md 6).
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

    /// <summary>Métricas de gesto no DPI atual da janela, que é o do monitor em que o item foi pressionado.</summary>
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

    /// <summary>Coordenadas da mensagem (cliente, com sinal) em pixels físicos do desktop virtual.</summary>
    private static PontoPx NaTela(nint hwnd, nint lParam)
    {
        var p = new Win32.POINT { X = Win32.XComSinal(lParam), Y = Win32.YComSinal(lParam) };
        Win32.ClientToScreen(hwnd, ref p);
        return new PontoPx(p.X, p.Y);
    }

    private void AoMudarEstado(object? remetente, EventArgs e)
    {
        // O Windows pode minimizar uma janela ao desconectar um monitor (crítica, L18; Fase 5, P12): o item volta ao
        // normal na hora, sem esconder o personagem.
        if (WindowState != WindowState.Minimized) return;
        WindowState = WindowState.Normal;
        Diagnostico.Evento("ITEM", ("minimizado", Id), ("restaurado", "sim"));
    }
}
