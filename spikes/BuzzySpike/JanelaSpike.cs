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

// Conta os OnRender: em repouso o WPF só chama quando invalida, então é uma prova de graça de
// que nada redesenha. Assinar CompositionTarget.Rendering não serve, porque já força quadros.
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

// A mesma janela pra todos os modos, assim a medição de desempenho vale pra janela que os
// testes de clique e arraste usam.
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

    // Fonte da verdade da posição, em px físicos do desktop virtual.
    private int _posX;
    private int _posY;

    // ---- gesto ----
    private bool _pressionado;
    private bool _arrastando;
    private int _agarreX, _agarreY;      // coordenadas de cliente
    private int _limiarX, _limiarY;
    private readonly List<double> _latenciasMs = [];
    private int _movimentos;
    private nint _foregroundNoPressionar;

    // Ligado só durante o nosso ReleaseCapture. Ele manda WM_CAPTURECHANGED na hora, e sem
    // isso o gesto encerrava duas vezes e não dava pra separar soltar normal de perda real
    // de captura (Alt+Tab).
    private bool _soltandoPorNos;

    // ---- animação ----
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

        // Do tamanho do sprite, transparente por pixel, sem ativar e fora da barra de tarefas.
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

        // Não ativa no clique e fica fora da barra de tarefas e do Alt+Tab.
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

        // Compensa o DPI pra cada pixel da figura cair num pixel físico da tela (1:1).
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

        // Linhas pra máquina, lidas por ferramentas/sonda-p1.ps1.
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

    // Só compara HWND; não lê título nem conteúdo da janela alheia.
    private bool RoubamosOFoco() => Interop.GetForegroundWindow() == _hwnd;

    // ------------------------------------------------------------------ mensagens

    private nint Gancho(nint hwnd, int msg, nint wParam, nint lParam, ref bool tratado)
    {
        switch (msg)
        {
            case Interop.WM_MOUSEACTIVATE:
                // O app de baixo continua com o foco.
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
                // Único ponto onde o gesto termina.
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

        // Qual faixa recebeu o clique: é o resultado do teste de clique.
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

        // Loga só de vez em quando; logar tudo distorceria a medição.
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

        // Só é clique se soltar DENTRO do limiar. Num gesto rápido o soltar pode chegar fora
        // sem nenhum WM_MOUSEMOVE antes; aí é arraste e a janela vai pra onde soltou.
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

    // ------------------------------------------------------------------ animação

    private void IniciarAnimacao(int fps)
    {
        _fpsPedido = fps;
        double intervaloMs = 1000.0 / fps;

        // Timer comum do Windows, sem timeBeginPeriod de propósito: o Buzzy nunca mexe na
        // resolução global. Se não der 60 qps por causa disso, o número medido é o resultado.
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

    // 60 qps pelo relógio do compositor em vez do timer. DispatcherTimer pedindo 60 entregou
    // ~39, mesmo com o timer global em 1 ms por outro processo (desde o Windows 10 2004 isso
    // não vale pra quem não pediu).
    // Custo: o compositor dispara mais que a troca de quadro (~85/s num monitor de 180 Hz).
    // Zerando a referência a cada quadro deu ~40 qps; com acumulador talvez chegue a 60, não medi.
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

        // Tolerância de 0,5 ms: sem ela, evento um pouco adiantado empurra o quadro pro ciclo seguinte.
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
