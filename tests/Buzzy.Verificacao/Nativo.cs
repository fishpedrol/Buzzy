using System.Runtime.InteropServices;

namespace Buzzy.Verificacao;

/// <summary>
/// Chamadas ao Windows da ferramenta de verificação (fora do produto). Compara identificadores
/// de janela, confere a qual processo uma janela pertence e lê geometria das janelas do próprio
/// teste e dos monitores. A barra de tarefas é localizada pela classe pública
/// <c>Shell_TrayWnd</c> só para confirmar que um clique cai nela. Não lê título, texto ou
/// conteúdo de outros aplicativos; do "último input" do Windows lê só a hora, nunca a tecla.
/// </summary>
internal static class Nativo
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Largura => Right - Left;
        public int Altura => Bottom - Top;

        /// <summary>Se <paramref name="outro"/> cabe inteiro neste retângulo.</summary>
        public bool Contem(RECT outro) => outro.Left >= Left && outro.Top >= Top && outro.Right <= Right && outro.Bottom <= Bottom;

        /// <summary>Se o ponto está dentro (semiaberto, como o RECT do Windows).</summary>
        public bool Contem(POINT p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

        public POINT Centro => new((Left + Right) / 2, (Top + Bottom) / 2);

        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public MOUSEINPUT mi;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    /// <summary>Identifica um ícone da bandeja pela janela que o registrou e pelo número dele.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NOTIFYICONIDENTIFIER
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public Guid guidItem;
    }

    /// <summary>Um monitor lido por EnumDisplayMonitors, com o DPI efetivo.</summary>
    internal sealed record MonitorLido(nint Handle, MONITORINFOEX Info, int Dpi)
    {
        internal bool Principal => (Info.dwFlags & MONITORINFOF_PRIMARY) != 0;
    }

    internal delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint dwData);

    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TOPMOST = 0x00000008L;
    internal const long WS_EX_TRANSPARENT = 0x00000020L;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_APPWINDOW = 0x00040000L;
    internal const long WS_EX_LAYERED = 0x00080000L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;

    internal const int WM_CLOSE = 0x0010;
    internal const int WM_CANCELMODE = 0x001F;
    internal const uint GA_ROOT = 2;
    internal const uint MONITOR_DEFAULTTONULL = 0;
    internal const uint MONITORINFOF_PRIMARY = 1;
    internal const int MDT_EFFECTIVE_DPI = 0;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.</summary>
    internal const nint ContextoPerMonitorV2 = -4;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT r);

    [DllImport("user32.dll")]
    internal static extern nint WindowFromPoint(POINT p);

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint hWnd, uint flags);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    internal static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AreDpiAwarenessContextsEqual(nint contextoA, nint contextoB);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("shell32.dll")]
    internal static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    // ---- tamagotchi (DEC-028): ordem Z das janelas do Buzzy e objetos GDI e USER dele ----------

    internal const uint GW_HWNDNEXT = 2;
    internal const uint GR_GDIOBJECTS = 0;
    internal const uint GR_USEROBJECTS = 1;

    /// <summary>A janela seguinte na ordem Z: só para conferir a ordem das janelas do Buzzy aberto pela verificação.</summary>
    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hWnd, uint uCmd);

    /// <summary>Quantos objetos GDI ou USER um processo tem abertos: só para o Buzzy que a verificação abriu.</summary>
    [DllImport("user32.dll")]
    internal static extern uint GetGuiResources(nint hProcess, uint uiFlags);

    /// <summary>
    /// Quantas janelas há descendo a ordem Z de <paramref name="de"/> até <paramref name="ate"/> (1 = logo abaixo); -1 se
    /// <paramref name="ate"/> não está abaixo de <paramref name="de"/>.
    /// </summary>
    internal static int PosicoesAbaixo(nint de, nint ate)
    {
        nint h = de;
        for (int i = 1; i <= 1000; i++)
        {
            h = GetWindow(h, GW_HWNDNEXT);
            if (h == 0) return -1;
            if (h == ate) return i;
        }
        return -1;
    }

    internal const uint SPI_GETMOUSECLICKLOCK = 0x101E;
    internal const uint SPI_GETMOUSECLICKLOCKTIME = 0x2008;

    /// <summary>
    /// Só LEITURA de parâmetros do sistema (ClickLock ligado e seu tempo). A verificação nunca
    /// grava configuração global do Windows (AGENTS.md).
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint acao, uint parametro, out int valor, uint winIni);

    /// <summary>Lê um parâmetro inteiro ou booleano do sistema; nulo se o Windows não informar.</summary>
    internal static int? LerParametroDoSistema(uint acao) => SystemParametersInfo(acao, 0, out int valor, 0) ? valor : null;

    internal static nint Raiz(nint h) => h == 0 ? 0 : GetAncestor(h, GA_ROOT);

    internal static nint DonoDoPonto(int x, int y) => Raiz(WindowFromPoint(new POINT(x, y)));

    internal static RECT Retangulo(nint h)
    {
        GetWindowRect(h, out RECT r);
        return r;
    }

    internal static POINT Cursor()
    {
        GetCursorPos(out POINT p);
        return p;
    }

    internal static uint PidDe(nint h)
    {
        if (h == 0) return 0;
        GetWindowThreadProcessId(h, out uint pid);
        return pid;
    }

    /// <summary>Se a thread atual está em Per-Monitor V2, como o Buzzy (coordenadas físicas).</summary>
    internal static bool ThreadEmPerMonitorV2()
        => AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), ContextoPerMonitorV2);

    /// <summary>
    /// Hora (GetTickCount, 32 bits) do último input do sistema, de qualquer dispositivo, injetado
    /// ou não; nulo se o Windows não informar. Não diz qual tecla nem qual botão.
    /// </summary>
    internal static uint? UltimoInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? info.dwTime : null;
    }

    /// <summary>
    /// Milissegundos desde o último input. A diferença é lida com sinal, o que aguenta a volta
    /// do contador de 32 bits; um carimbo "no futuro" (o input chegou entre as duas leituras)
    /// vale 0, e não 49 dias de ociosidade.
    /// </summary>
    internal static uint OciosoMs()
    {
        if (UltimoInput() is not { } ultimo) return 0;
        int diferenca = unchecked((int)((uint)Environment.TickCount - ultimo));
        return diferenca < 0 ? 0 : (uint)diferenca;
    }

    /// <summary>MAKELPARAM(baixo, alto): cada metade com 16 bits, preservando o sinal (coordenadas negativas).</summary>
    internal static nint MakeLParam(int baixo, int alto)
        => unchecked((nint)(int)(((uint)(ushort)(short)alto << 16) | (ushort)(short)baixo));

    /// <summary>
    /// Retângulo do ícone da bandeja registrado pela janela <paramref name="hwnd"/> com o número
    /// <paramref name="id"/>, lido agora pela Shell; nulo se ela não informar.
    /// </summary>
    internal static RECT? RetanguloDoIcone(nint hwnd, uint id)
    {
        var identificador = new NOTIFYICONIDENTIFIER { cbSize = Marshal.SizeOf<NOTIFYICONIDENTIFIER>(), hWnd = hwnd, uID = id };
        return Shell_NotifyIconGetRect(ref identificador, out RECT r) == 0 ? r : null;
    }

    /// <summary>Todos os monitores do desktop, por EnumDisplayMonitors, na ordem em que o Windows os entrega.</summary>
    internal static List<MonitorLido> Monitores()
    {
        var handles = new List<nint>();
        MonitorEnumProc coletar = (h, _, _, _) =>
        {
            handles.Add(h);
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, coletar, 0)) throw new InvalidOperationException("EnumDisplayMonitors falhou.");
        GC.KeepAlive(coletar);

        var lidos = new List<MonitorLido>(handles.Count);
        foreach (nint h in handles)
        {
            if (Ler(h) is { } m) lidos.Add(m);
        }
        return lidos;
    }

    /// <summary>O monitor que contém o ponto, ou nulo num vão entre monitores.</summary>
    internal static MonitorLido? Monitor(POINT p)
    {
        nint h = MonitorFromPoint(p, MONITOR_DEFAULTTONULL);
        return h == 0 ? null : Ler(h);
    }

    private static MonitorLido? Ler(nint h)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(h, ref mi)) return null;
        int dpi = GetDpiForMonitor(h, MDT_EFFECTIVE_DPI, out uint dx, out _) == 0 ? (int)dx : 96;
        return new MonitorLido(h, mi, dpi);
    }
}
