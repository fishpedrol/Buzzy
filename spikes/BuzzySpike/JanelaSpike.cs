using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BuzzySpike;

internal enum Modo
{
    P1,
    P3,
    P3Margem,
    P2Repouso,
    P2Anim10,
    P2Anim60,
    P2Anim60Comp,
    Receptor,
}

/// <summary>
/// Elemento que conta as próprias passagens de desenho. Em repouso, o WPF só chama
/// OnRender quando o elemento é invalidado, então o contador é uma prova interna e de
/// custo zero de que nada está sendo redesenhado. Não serve para isso um assinante de
/// CompositionTarget.Rendering, porque assinar já força quadros contínuos.
/// </summary>
internal sealed class SuperficieContada : FrameworkElement
{
    private BitmapSource? _fonte;

    internal long Renders { get; private set; }

    internal BitmapSource? Fonte
    {
        get => _fonte;
        set { _fonte = value; InvalidateVisual(); }
    }

    protected override Size MeasureOverride(Size disponivel)
        => _fonte is null ? new Size(0, 0) : new Size(_fonte.PixelWidth, _fonte.PixelHeight);

    protected override void OnRender(DrawingContext dc)
    {
        Renders++;
        if (_fonte is not null)
            dc.DrawImage(_fonte, new Rect(0, 0, _fonte.PixelWidth, _fonte.PixelHeight));
    }
}

/// <summary>
/// Janela única dos protótipos P1, P3 e P2. O modo escolhe o comportamento; a janela em si
/// é sempre a mesma, para que a medição de P2 valha para a mesma janela que P1 e P3 testam.
/// </summary>
internal sealed class JanelaSpike : Window
{
    private readonly Modo _modo;
    private readonly int? _xPedido;
    private readonly int? _yPedido;

    private readonly SuperficieContada _superficie = new();
    private BitmapSource _figuraParada = null!;
    private BitmapSource _figuraComMargem = null!;
    private IReadOnlyList<BitmapSource> _quadros = [];

    private nint _hwnd;
    private uint _dpi = 96;

    // Posição física autoritativa da janela, em pixels do desktop virtual.
    private int _posX;
    private int _posY;

    // ---- estado do gesto (P3) ----
    private bool _pressionado;
    private bool _arrastando;
    private int _agarreX, _agarreY;      // ponto de agarre, em coordenadas de cliente
    private int _limiarX, _limiarY;
    private readonly List<double> _latenciasMs = [];
    private int _movimentos;
    private nint _foregroundNoPressionar;

    // Verdadeiro só durante o ReleaseCapture do próprio SOLTAR. ReleaseCapture envia
    // WM_CAPTURECHANGED de forma síncrona; sem esta marca, o gesto era encerrado duas vezes
    // (uma como "captura perdida", outra como "soltou"), e o log não distinguia uma perda
    // de captura real, como a de Alt+Tab, da liberação normal ao soltar.
    private bool _soltandoPorNos;

    // ---- animação (P2) ----
    private DispatcherTimer? _timer;
    private int _quadroAtual;
    private long _ticks;
    private readonly Stopwatch _relogioAnimacao = new();
    private int _fpsPedido;
    private bool _usandoCompositor;
    private long _eventosCompositor;
    private double _alvoQuadroMs;
    private long _marcaUltimoQuadro;

    internal JanelaSpike(Modo modo, int? x, int? y)
    {
        _modo = modo;
        _xPedido = x;
        _yPedido = y;

        // Janela do tamanho do sprite, transparente por pixel, que não ativa e não
        // aparece na barra de tarefas. ARCHITECTURE.md 2.13.1 e decisão Q-03.
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Title = "BuzzySpike";
        WindowStartupLocation = WindowStartupLocation.Manual;

        RenderOptions.SetBitmapScalingMode(_superficie, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(_superficie, EdgeMode.Aliased);
        Content = _superficie;

        Loaded += AoCarregar;
        Closed += AoFechar;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        _dpi = Interop.GetDpiForWindow(_hwnd);
        if (_dpi == 0) _dpi = 96;

        // Não ativar ao ser clicada, e ficar fora da barra de tarefas e do Alt+Tab.
        nint ex = Interop.GetWindowLongPtr(_hwnd, Interop.GWL_EXSTYLE);
        Interop.SetWindowLongPtr(_hwnd, Interop.GWL_EXSTYLE,
            (nint)((long)ex | Interop.WS_EX_NOACTIVATE | Interop.WS_EX_TOOLWINDOW));

        HwndSource.FromHwnd(_hwnd)?.AddHook(Gancho);

        _limiarX = Interop.GetSystemMetrics(Interop.SM_CXDRAG);
        _limiarY = Interop.GetSystemMetrics(Interop.SM_CYDRAG);
    }

    private void AoCarregar(object? remetente, RoutedEventArgs e)
    {
        _figuraParada = FiguraTeste.Criar();
        _figuraComMargem = FiguraTeste.Criar(margemAlfa1: true);

        // A janela tem exatamente o tamanho da figura em pixels físicos. Em 96 DPI a conta
        // é neutra; num monitor com escala diferente, a escala compensa para manter 1:1,
        // porque P1 exige que cada pixel da figura caia num pixel da tela.
        double fator = 96.0 / _dpi;
        _superficie.LayoutTransform = new ScaleTransform(fator, fator);

        Width = FiguraTeste.Largura * fator;
        Height = FiguraTeste.Altura * fator;

        _superficie.Fonte = _modo is Modo.P2Anim10 or Modo.P2Anim60 or Modo.P2Anim60Comp
            ? (_quadros = FiguraTeste.CriarQuadrosAnimacao())[0]
            : _figuraParada;

        PosicionarInicial();
        RegistrarAbertura();

        switch (_modo)
        {
            case Modo.P2Anim10:
                IniciarAnimacao(10);
                break;
            case Modo.P2Anim60:
                IniciarAnimacao(60);
                break;
            case Modo.P2Anim60Comp:
                IniciarAnimacaoPeloCompositor(60);
                break;
        }
    }

    // ------------------------------------------------------------------ posição

    private void PosicionarInicial()
    {
        int largura = FiguraTeste.Largura;
        int altura = FiguraTeste.Altura;

        if (_xPedido is int px && _yPedido is int py)
        {
            _posX = px;
            _posY = py;
        }
        else
        {
            // Centro da área útil do monitor primário.
            var mi = new Interop.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Interop.MONITORINFOEX>() };
            nint mon = Interop.MonitorFromPoint(new Interop.POINT(0, 0), Interop.MONITOR_DEFAULTTONEAREST);
            if (Interop.GetMonitorInfo(mon, ref mi))
            {
                _posX = mi.rcWork.Left + (mi.rcWork.Largura - largura) / 2;
                _posY = mi.rcWork.Top + (mi.rcWork.Altura - altura) / 2;
            }
        }

        Interop.SetWindowPos(_hwnd, 0, _posX, _posY, largura, altura,
            Interop.SWP_NOZORDER | Interop.SWP_NOACTIVATE);
    }

    private void MoverPara(int x, int y)
    {
        _posX = x;
        _posY = y;
        Interop.SetWindowPos(_hwnd, 0, x, y, 0, 0,
            Interop.SWP_NOSIZE | Interop.SWP_NOZORDER | Interop.SWP_NOACTIVATE);
    }

    // ------------------------------------------------------------------ registro

    private void RegistrarAbertura()
    {
        long ex = Interop.GetWindowLongPtr(_hwnd, Interop.GWL_EXSTYLE);
        Interop.GetWindowRect(_hwnd, out Interop.RECT r);

        Diagnostico.Bloco("Janela", [
            $"HWND                : {_hwnd} (0x{_hwnd:X})",
            $"Estilo estendido    : 0x{ex:X8} -> {Interop.DescreverEstiloEstendido(ex)}",
            $"WS_EX_LAYERED       : {((ex & Interop.WS_EX_LAYERED) != 0 ? "SIM (WPF usou o caminho de janela layered)" : "NÃO")}",
            $"WS_EX_NOACTIVATE    : {((ex & Interop.WS_EX_NOACTIVATE) != 0 ? "SIM" : "NÃO")}",
            $"Retângulo físico    : {r} ({r.Largura}x{r.Altura} px)",
            $"DPI da janela       : {_dpi} ({_dpi * 100 / 96}%)",
            $"Figura              : {FiguraTeste.Largura}x{FiguraTeste.Altura} px, gerada em código, Pbgra32",
            $"Monitor da janela   : {DescreverMonitorDaJanela()}",
        ]);

        // Linhas legíveis por máquina, consumidas por ferramentas/sonda-p1.ps1.
        Diagnostico.Linha($"SONDA|HWND|{_hwnd}");
        Diagnostico.Linha($"SONDA|RECT|{r.Left}|{r.Top}|{r.Right}|{r.Bottom}");
        foreach (Banda b in FiguraTeste.Bandas)
        {
            Interop.POINT c = b.Centro;
            Diagnostico.Linha($"SONDA|BANDA|{b.Nome}|{b.Alfa}|{_posX + c.X}|{_posY + c.Y}");
        }

        Diagnostico.Bloco("Faixas da figura (centro em coordenadas de tela)", [
            .. FiguraTeste.Bandas.Select(b =>
                $"{b.Nome,-8} alfa={b.Alfa,-3} interior local {b.Interior} -> clique em ({_posX + b.Centro.X},{_posY + b.Centro.Y})")
        ]);

        if (_modo == Modo.P1)
        {
            Diagnostico.Linha("P1 pronto. Clique em cada faixa. Cada clique que CHEGAR a esta janela vira uma linha abaixo.");
            Diagnostico.Linha("P1: a ausência de linha para a faixa alfa 0 é o resultado esperado, porque o clique deve ir para o aplicativo de baixo.");
        }
        else if (_modo is Modo.P3 or Modo.P3Margem)
        {
            Diagnostico.Linha($"P3 pronto ({(_modo == Modo.P3Margem ? "com margem alfa 1 durante o gesto" : "captura simples")}). "
                + $"Limiar de arraste do sistema: {_limiarX}x{_limiarY} px.");
        }
    }

    private string DescreverMonitorDaJanela()
    {
        nint mon = Interop.MonitorFromWindow(_hwnd, Interop.MONITOR_DEFAULTTONEAREST);
        var mi = new Interop.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Interop.MONITORINFOEX>() };
        if (!Interop.GetMonitorInfo(mon, ref mi)) return "não foi possível ler";

        uint dx = 96, dy = 96;
        Interop.GetDpiForMonitor(mon, Interop.MDT_EFFECTIVE_DPI, out dx, out dy);
        bool primario = (mi.dwFlags & Interop.MONITORINFOF_PRIMARY) != 0;
        return $"{mi.szDevice} {(primario ? "[primário]" : "[secundário]")} tela {mi.rcMonitor} útil {mi.rcWork} dpi {dx}x{dy}";
    }

    /// <summary>
    /// Verdadeiro quando a janela em primeiro plano é a nossa, ou seja, quando o protótipo
    /// roubou o foco. Só compara identificadores; não lê título nem conteúdo de janela
    /// alheia, o que SECURITY.md 3.2 proíbe.
    /// </summary>
    private bool RoubamosOFoco() => Interop.GetForegroundWindow() == _hwnd;

    // ------------------------------------------------------------------ mensagens

    private nint Gancho(nint hwnd, int msg, nint wParam, nint lParam, ref bool tratado)
    {
        switch (msg)
        {
            case Interop.WM_MOUSEACTIVATE:
                // Não ativar por clique: o aplicativo de baixo mantém o foco.
                tratado = true;
                return Interop.MA_NOACTIVATE;

            case Interop.WM_LBUTTONDOWN:
                TratarPressionar(lParam);
                break;

            case Interop.WM_MOUSEMOVE:
                if (_pressionado) TratarMover(lParam);
                break;

            case Interop.WM_LBUTTONUP:
                if (_pressionado) TratarSoltar(lParam);
                break;

            case Interop.WM_CAPTURECHANGED:
                // Ponto único de término do gesto, conforme ARCHITECTURE.md 2.7.
                if (_soltandoPorNos)
                {
                    Diagnostico.Linha($"WM_CAPTURECHANGED: captura liberada por nós ao soltar (esperado), nova dona {lParam}.");
                }
                else if (_pressionado)
                {
                    Diagnostico.Linha($"WM_CAPTURECHANGED: captura perdida para {lParam} antes de soltar. Encerrando o gesto.");
                    EncerrarGesto("captura perdida");
                }
                break;

            case Interop.WM_DPICHANGED:
                _dpi = (uint)(Interop.XComSinal(wParam));
                Diagnostico.Linha($"WM_DPICHANGED: novo DPI {_dpi}. Monitor: {DescreverMonitorDaJanela()}");
                break;

            case Interop.WM_DISPLAYCHANGE:
                Diagnostico.Linha($"WM_DISPLAYCHANGE recebido. Monitor: {DescreverMonitorDaJanela()}");
                break;
        }

        return 0;
    }

    private void TratarPressionar(nint lParam)
    {
        int cx = Interop.XComSinal(lParam);
        int cy = Interop.YComSinal(lParam);

        // Qual faixa da figura recebeu o clique. Este registro é o resultado de P1.
        Banda? faixa = FiguraTeste.Bandas.FirstOrDefault(b =>
            cx >= b.Interior.Left && cx < b.Interior.Right &&
            cy >= b.Interior.Top && cy < b.Interior.Bottom);

        string onde = faixa is null
            ? "moldura ou coluna de rótulos (opaca)"
            : $"faixa {faixa.Nome} (alfa {faixa.Alfa})";

        Diagnostico.Linha($"CLIQUE recebido pela janela do Buzzy em cliente ({cx},{cy}) -> {onde}. "
            + $"Foco é nosso? {(RoubamosOFoco() ? "SIM (PROBLEMA)" : "não")}");

        if (faixa is not null)
            Diagnostico.Linha($"P1|CLIQUE|{faixa.Nome}|{faixa.Alfa}");

        if (_modo is not (Modo.P3 or Modo.P3Margem)) return;

        _pressionado = true;
        _arrastando = false;
        _agarreX = cx;
        _agarreY = cy;
        _latenciasMs.Clear();
        _movimentos = 0;
        _foregroundNoPressionar = Interop.GetForegroundWindow();

        Interop.SetCapture(_hwnd);

        if (_modo == Modo.P3Margem)
            _superficie.Fonte = _figuraComMargem;

        Diagnostico.Linha($"PRESSIONAR: captura = {Interop.GetCapture()} (nossa: {Interop.GetCapture() == _hwnd}). "
            + $"Janela em primeiro plano antes do gesto: {_foregroundNoPressionar}; é nossa? {_foregroundNoPressionar == _hwnd}");
    }

    private void TratarMover(nint lParam)
    {
        long inicio = Stopwatch.GetTimestamp();

        int cx = Interop.XComSinal(lParam);
        int cy = Interop.YComSinal(lParam);

        int dx = cx - _agarreX;
        int dy = cy - _agarreY;

        if (!_arrastando)
        {
            if (Math.Abs(dx) < _limiarX && Math.Abs(dy) < _limiarY) return;
            _arrastando = true;
            Diagnostico.Linha($"ARRASTE iniciado: passou o limiar do sistema ({_limiarX}x{_limiarY} px) com delta ({dx},{dy}).");
        }

        MoverPara(_posX + dx, _posY + dy);
        _movimentos++;

        double ms = (Stopwatch.GetTimestamp() - inicio) * 1000.0 / Stopwatch.Frequency;
        _latenciasMs.Add(ms);

        // Amostragem esparsa do log: registrar todo movimento distorceria a medição.
        if (_movimentos % 60 == 0)
        {
            Diagnostico.Linha($"ARRASTE em curso: {_movimentos} movimentos. Cliente ({cx},{cy}) — "
                + $"negativo aqui é esperado fora da janela. Posição física ({_posX},{_posY}). "
                + $"Foco é nosso? {(RoubamosOFoco() ? "SIM (PROBLEMA)" : "não")}");
        }
    }

    private void TratarSoltar(nint lParam)
    {
        int cx = Interop.XComSinal(lParam);
        int cy = Interop.YComSinal(lParam);

        // ARCHITECTURE.md 2.7, passo 3: só é clique se o botão for solto DENTRO do
        // retângulo de arraste. Num gesto muito rápido, o soltar pode chegar fora dele sem
        // nenhum WM_MOUSEMOVE intermediário; nesse caso o gesto é arraste e a janela vai
        // para o ponto em que o botão foi solto.
        if (!_arrastando)
        {
            int dx = cx - _agarreX;
            int dy = cy - _agarreY;
            if (Math.Abs(dx) >= _limiarX || Math.Abs(dy) >= _limiarY)
            {
                _arrastando = true;
                Diagnostico.Linha($"ARRASTE detectado no soltar: o botão foi solto fora do limiar com delta ({dx},{dy}), sem movimento intermediário.");
                MoverPara(_posX + dx, _posY + dy);
                _movimentos++;
            }
        }

        bool eraArraste = _arrastando;

        Diagnostico.Linha($"SOLTAR em cliente ({cx},{cy}). Gesto classificado como "
            + $"{(eraArraste ? "ARRASTE" : "CLIQUE")} pelo limiar do sistema.");

        _soltandoPorNos = true;
        try { Interop.ReleaseCapture(); }
        finally { _soltandoPorNos = false; }
        EncerrarGesto(eraArraste ? "soltou depois de arrastar" : "soltou sem passar do limiar");
    }

    private void EncerrarGesto(string motivo)
    {
        _pressionado = false;
        _arrastando = false;

        if (_modo == Modo.P3Margem)
            _superficie.Fonte = _figuraParada;

        var linhas = new List<string>
        {
            $"Motivo do término   : {motivo}",
            $"Movimentos aplicados: {_movimentos}",
            $"Posição final       : ({_posX},{_posY})",
            $"Monitor final       : {DescreverMonitorDaJanela()}",
            $"Captura agora       : {Interop.GetCapture()} (deve ser 0)",
            $"Foco é nosso agora? : {(RoubamosOFoco() ? "SIM (PROBLEMA: a janela roubou o foco)" : "não (correto)")}",
            $"Foco mudou no gesto?: {(Interop.GetForegroundWindow() == _foregroundNoPressionar ? "não (correto)" : "SIM — conferir se foi o usuário com Alt+Tab")}",
        };

        if (_latenciasMs.Count > 0)
        {
            var ordenadas = _latenciasMs.OrderBy(v => v).ToList();
            double media = _latenciasMs.Average();
            double p95 = ordenadas[(int)(ordenadas.Count * 0.95).AtMost(ordenadas.Count - 1)];
            linhas.Add($"M5 latência do arraste (recebimento da mensagem até aplicar a posição): "
                + $"média {media:0.000} ms, p95 {p95:0.000} ms, máx {ordenadas[^1]:0.000} ms, n={ordenadas.Count}");
        }

        Diagnostico.Bloco("Fim do gesto", linhas);
    }

    // ------------------------------------------------------------------ animação P2

    private void IniciarAnimacao(int fps)
    {
        _fpsPedido = fps;
        double intervaloMs = 1000.0 / fps;

        // DispatcherTimer usa o timer comum do Windows. De propósito, nada aqui chama
        // timeBeginPeriod: DEC-011 exige que o Buzzy nunca eleve a resolução global do
        // timer. Se 60 qps não for alcançado por isso, o número medido é o resultado.
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(intervaloMs),
        };
        _timer.Tick += (_, _) =>
        {
            _ticks++;
            _quadroAtual = (_quadroAtual + 1) % _quadros.Count;
            _superficie.Fonte = _quadros[_quadroAtual];
        };

        _relogioAnimacao.Restart();
        _timer.Start();

        Diagnostico.Bloco("Animação P2", [
            $"Quadros por segundo pedidos: {fps}",
            $"Intervalo do timer         : {intervaloMs:0.000} ms",
            $"Quadros distintos no ciclo : {_quadros.Count}",
            $"Resolução do timer global ao iniciar: {Diagnostico.ResolucaoTimerMs()?.ToString("0.000") ?? "não medida"} ms",
            "Nenhuma chamada a timeBeginPeriod: DEC-011 proíbe elevar a resolução global.",
        ]);
    }

    /// <summary>
    /// Segunda via para 60 quadros por segundo, usando o relógio do compositor do WPF em
    /// vez do timer do Windows. Existe porque, medido, DispatcherTimer pedindo 60 qps
    /// entregou ~39, mesmo com a resolução global do timer já em 1 ms por outro processo.
    /// A causa não foi isolada: desde o Windows 10 2004, a resolução pedida por outro
    /// processo não vale para os timers de quem não a pediu.
    ///
    /// Custo deste caminho: o evento do compositor dispara mais vezes do que o quadro é
    /// trocado. Medido nesta máquina: ~85 eventos por segundo, com monitor de 180 Hz. O
    /// disparo abaixo zera a referência a cada quadro e por isso entregou ~40 qps; um
    /// disparo por acumulador poderia chegar perto de 60, mas não foi medido.
    /// </summary>
    private void IniciarAnimacaoPeloCompositor(int fps)
    {
        _fpsPedido = fps;
        _usandoCompositor = true;
        _alvoQuadroMs = 1000.0 / fps;

        _relogioAnimacao.Restart();
        _marcaUltimoQuadro = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += AoComporQuadro;

        Diagnostico.Bloco("Animação P2 pelo compositor", [
            $"Quadros por segundo pedidos: {fps}",
            $"Intervalo alvo             : {_alvoQuadroMs:0.000} ms",
            $"Quadros distintos no ciclo : {_quadros.Count}",
            $"Resolução do timer global ao iniciar: {Diagnostico.ResolucaoTimerMs()?.ToString("0.000") ?? "não medida"} ms",
            "Relógio: CompositionTarget.Rendering, sem timeBeginPeriod e sem DispatcherTimer.",
        ]);
    }

    private void AoComporQuadro(object? remetente, EventArgs e)
    {
        _eventosCompositor++;

        long agora = Stopwatch.GetTimestamp();
        double decorridoMs = (agora - _marcaUltimoQuadro) * 1000.0 / Stopwatch.Frequency;

        // Meia janela de tolerância: sem ela, um evento que chega pouco antes do alvo
        // atrasaria o quadro para o ciclo seguinte e derrubaria a taxa alcançada.
        if (decorridoMs + 0.5 < _alvoQuadroMs) return;

        _marcaUltimoQuadro = agora;
        _ticks++;
        _quadroAtual = (_quadroAtual + 1) % _quadros.Count;
        _superficie.Fonte = _quadros[_quadroAtual];
    }

    // ------------------------------------------------------------------ fim

    private void AoFechar(object? remetente, EventArgs e)
    {
        _timer?.Stop();
        if (_usandoCompositor) CompositionTarget.Rendering -= AoComporQuadro;

        var linhas = new List<string>
        {
            $"Passagens de desenho (OnRender) no total: {_superficie.Renders}",
            $"Resolução do timer global ao encerrar   : {Diagnostico.ResolucaoTimerMs()?.ToString("0.000") ?? "não medida"} ms",
        };

        if (_relogioAnimacao.IsRunning || _ticks > 0)
        {
            _relogioAnimacao.Stop();
            double s = _relogioAnimacao.Elapsed.TotalSeconds;
            linhas.Add($"Animação: {_ticks} quadros trocados em {s:0.0} s -> {(s > 0 ? _ticks / s : 0):0.00} quadros/s "
                + $"alcançados contra {_fpsPedido} pedidos");

            if (_usandoCompositor)
            {
                linhas.Add($"Eventos do compositor recebidos: {_eventosCompositor} -> "
                    + $"{(s > 0 ? _eventosCompositor / s : 0):0.00} por segundo. "
                    + "O compositor dispara mais vezes do que o quadro é trocado; a diferença é "
                    + "custo deste caminho.");
            }
        }

        if (_modo == Modo.P2Repouso)
        {
            linhas.Add(_superficie.Renders <= 2
                ? "Repouso: o WPF desenhou apenas a passagem inicial. Nenhum redesenho periódico partiu do aplicativo."
                : $"Repouso: houve {_superficie.Renders} passagens de desenho. Investigar antes de reportar o número.");
        }

        Diagnostico.Bloco("Encerramento", linhas);
    }
}

internal static class Extensoes
{
    internal static int AtMost(this double valor, int teto) => Math.Min((int)valor, teto);
}
