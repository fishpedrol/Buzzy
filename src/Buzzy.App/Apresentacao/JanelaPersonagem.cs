using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// Janela do personagem (ARCHITECTURE.md 2.13.1): sem borda, do tamanho do sprite, com
/// transparência por pixel (janela layered do WPF, confirmada em P1), sempre no topo, fora da
/// barra de tarefas e do Alt+Tab (Q-03) e que NÃO ativa ao ser clicada (DEC-009, P3).
///
/// Adaptador do ponteiro (Fase 3): converte as mensagens de mouse que o Windows entrega a esta
/// janela em eventos de ponteiro do núcleo, em pixels físicos do desktop virtual, e segura a
/// captura do mouse só enquanto a raiz de composição pede (um gesto começado no personagem).
/// O Windows só entrega aqui cliques em pixels com alfa diferente de 0; fora de um gesto, nada
/// do mouse de outros aplicativos chega (SECURITY.md 3.1).
/// </summary>
internal sealed class JanelaPersonagem : Window
{
    // Pixel a pixel pelo DPI atual da janela (EncaixeDeDpi, passo P14).
    private readonly Image _imagem = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        SnapsToDevicePixels = true,
    };

    private bool _capturando;

    // Verdadeiro só durante o ReleaseCapture pedido pela raiz. ReleaseCapture manda
    // WM_CAPTURECHANGED de forma síncrona; sem esta marca, o fim normal do gesto pareceria uma
    // captura perdida (a mesma lição do protótipo P3).
    private bool _soltandoPorNos;

    /// <summary>O tamanho lógico da janela, o do passo de escala em vigor (DEC-038, item 9), fixo durante a execução.</summary>
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
            // O WPF também avisa quando só reavaliou o DPI, sem mudança (observado logo depois
            // de mostrar a janela); isso não é mudança de topologia.
            int antes = (int)Math.Round(e.OldDpi.PixelsPerInchX);
            int depois = (int)Math.Round(e.NewDpi.PixelsPerInchX);
            EncaixeDeDpi.AjustarPixelAPixel(_imagem, e.NewDpi.PixelsPerInchX);
            if (antes != depois) DpiMudou?.Invoke(depois);
        };
    }

    internal nint Hwnd { get; private set; }

    /// <summary>Evento de ponteiro já normalizado (pixels físicos, relógio monotônico em ms).</summary>
    internal event Action<EventoDePonteiro>? Ponteiro;

    /// <summary>O Windows mudou o DPI da janela (troca de escala ou de monitor).</summary>
    internal event Action<int>? DpiMudou;

    /// <summary>O Windows tentou minimizar a janela (Q-03: minimizar esconde).</summary>
    internal event Action? Minimizada;

    /// <summary>Se a janela está com a captura do mouse de um gesto em curso.</summary>
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

    /// <summary>Posiciona e dimensiona a janela em pixels físicos, sem ativar nem mudar a ordem Z.</summary>
    internal void AplicarRetangulo(RetanguloPx r)
        => Win32.SetWindowPos(Hwnd, 0, r.Esquerda, r.Topo, r.Largura, r.Altura, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    /// <summary>Onde a janela está de fato, em pixels físicos; nulo se o Windows não informar.</summary>
    internal RetanguloPx? RetanguloReal()
        => Hwnd != 0 && Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    /// <summary>Um peer vazio: a imagem fica fora das árvores de controle e de conteúdo do leitor de tela (DEC-038, item 7).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PeerVazio(this);

    /// <summary>O tamanho lógico da janela (o passo de escala em vigor).</summary>
    internal TamanhoDip Tamanho => _tamanho;

    /// <summary>Se a janela está no grupo "sempre no topo" (Q-03; DEC-038, item 8).</summary>
    internal bool SempreNoTopo => Topmost;

    /// <summary>Liga ou desliga o "sempre no topo", uma vez, por evento (a partida ou o comando do usuário).</summary>
    internal void AplicarSempreNoTopo(bool ligado) => Topmost = ligado;

    /// <summary>
    /// Recoloca a janela no topo, uma vez, sem ativar: com o "sempre no topo" ligado, no topo do grupo topmost; desligado, acima
    /// das janelas comuns, sem ficar topmost. Só é chamado por ação explícita do usuário (mostrar, abrir o painel) e no fim da
    /// tela cheia com o topo ligado, nunca por timer (SECURITY.md 2).
    /// </summary>
    internal void AoTopoDaFaixa()
    {
        if (Topmost) Win32.SetWindowPos(Hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        else Win32.AoTopoDaFaixaComum(Hwnd);
    }

    /// <summary>Captura o mouse para o gesto em curso (ARCHITECTURE.md 2.7, passo 1 do ciclo de arraste).</summary>
    internal void Capturar()
    {
        if (_capturando || Hwnd == 0) return;
        Win32.SetCapture(Hwnd);
        _capturando = true;
    }

    /// <summary>Solta a captura do gesto, sem que isso conte como captura perdida.</summary>
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
                // A janela vai mudar de DPI (outro monitor ou outra escala): o tamanho do sprite no DPI novo (EncaixeDeDpi).
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
                // "Cancelar modos, como a captura do mouse": o DefWindowProc solta a captura em nome
                // da janela. Feito aqui de forma explícita, para não depender do tratamento do WPF;
                // o WM_CAPTURECHANGED que vem em seguida encerra o gesto como captura perdida.
                if (_capturando) Win32.ReleaseCapture();
                break;

            case Win32.WM_CAPTURECHANGED:
                // Ponto único de término de um gesto interrompido (ARCHITECTURE.md 2.13.3): Alt+Tab,
                // tecla Windows, UAC ou outra janela ficou com o mouse. Só o fato é registrado,
                // nunca qual janela é a nova dona (SECURITY.md 6).
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

    /// <summary>
    /// Métricas de gesto no DPI atual da janela, que é o do monitor em que o personagem foi
    /// pressionado (ARCHITECTURE.md 2.7: retângulo de arraste "lido para o DPI do monitor").
    /// </summary>
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

    /// <summary>
    /// Coordenadas da mensagem (cliente, com sinal) em pixels físicos do desktop virtual. A janela
    /// só se move nesta mesma thread, então ela está onde estava quando a mensagem foi gerada.
    /// </summary>
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
