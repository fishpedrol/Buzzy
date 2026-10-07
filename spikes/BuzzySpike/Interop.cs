using System.Runtime.InteropServices;

namespace BuzzySpike;

// Sem hook global, injeção de input, captura de tela, rede ou leitura de janela alheia.
// O teste de foco só compara HWNDs, não lê nada do outro app.
internal static class Interop
{
    // ---- Estilos de janela -------------------------------------------------
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const long WS_EX_TOPMOST = 0x00000008L;
    internal const long WS_EX_TRANSPARENT = 0x00000020L;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_LAYERED = 0x00080000L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;

    // ---- Mensagens --------------------------------------------------------
    internal const int WM_MOUSEMOVE = 0x0200;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_MOUSEACTIVATE = 0x0021;
    internal const int WM_CAPTURECHANGED = 0x0215;
    internal const int WM_DPICHANGED = 0x02E0;
    internal const int WM_DISPLAYCHANGE = 0x007E;

    internal const int MA_NOACTIVATE = 3;

    // ---- Métricas do sistema ----------------------------------------------
    internal const int SM_CXDRAG = 68;
    internal const int SM_CYDRAG = 69;

    // ---- SetWindowPos -----------------------------------------------------
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
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
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
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

    // Não troque por LibraryImport como o analisador sugere: o marshalling gerado não entende o
    // ByValTStr de MONITORINFOEX e GetMonitorInfo volta retângulos zerados, sem erro nenhum.

    internal const uint MONITORINFOF_PRIMARY = 0x00000001;
    internal const uint MONITOR_DEFAULTTONULL = 0;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern nint SetCapture(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    internal static extern nint GetCapture();

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern nint WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hWnd);

    // Unidades de 100 ns. Só leitura: serve pra provar que ninguém mexeu na resolução do timer.
    [DllImport("ntdll.dll")]
    internal static extern int NtQueryTimerResolution(out uint minimo, out uint maximo, out uint atual);

    // Tem que manter o sinal: monitor à esquerda do primário dá x negativo (aqui o secundário
    // começa em x = -1920).
    internal static int XComSinal(nint lParam) => unchecked((short)(long)lParam);

    internal static int YComSinal(nint lParam) => unchecked((short)((long)lParam >> 16));

    internal static string DescreverEstiloEstendido(long ex)
    {
        List<string> bits = [];
        if ((ex & WS_EX_LAYERED) != 0) bits.Add("WS_EX_LAYERED");
        if ((ex & WS_EX_NOACTIVATE) != 0) bits.Add("WS_EX_NOACTIVATE");
        if ((ex & WS_EX_TOOLWINDOW) != 0) bits.Add("WS_EX_TOOLWINDOW");
        if ((ex & WS_EX_TOPMOST) != 0) bits.Add("WS_EX_TOPMOST");
        if ((ex & WS_EX_TRANSPARENT) != 0) bits.Add("WS_EX_TRANSPARENT");
        return bits.Count == 0 ? "(nenhum dos bits observados)" : string.Join(" | ", bits);
    }
}
