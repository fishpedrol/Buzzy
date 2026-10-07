using System.Runtime.InteropServices;

namespace SondaP3;

// Só compara HWNDs e lê geometria das janelas do teste; nada de título, texto ou pixels alheios.
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

    // Em x64 são 40 bytes: type no 0 e a união a partir do 8. Tamanho errado e o Windows
    // recusa todos os eventos sem avisar.
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

    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TOPMOST = 0x00000008L;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_LAYERED = 0x00080000L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;

    internal const int WM_CLOSE = 0x0010;
    internal const uint GA_ROOT = 2;
    internal const uint MONITOR_DEFAULTTONULL = 0;
    internal const uint MONITORINFOF_PRIMARY = 1;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;

    internal const int SM_CXDRAG = 68;
    internal const int SM_CYDRAG = 69;
    internal const int SM_CXDOUBLECLK = 36;
    internal const int SM_CYDOUBLECLK = 37;
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    internal const uint SPI_GETMOUSECLICKLOCK = 0x101E;
    internal const uint SPI_SETMOUSECLICKLOCK = 0x101F;
    internal const uint SPI_GETMOUSECLICKLOCKTIME = 0x2008;

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT r);

    [DllImport("user32.dll")]
    internal static extern nint WindowFromPoint(POINT p);

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint hWnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(nint value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SpiGetInt(uint action, uint uiParam, ref int pvParam, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SpiSetValor(uint action, uint uiParam, nint pvParam, uint winIni);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);

    internal static nint Raiz(nint h) => h == 0 ? 0 : GetAncestor(h, GA_ROOT);

    internal static RECT Retangulo(nint h)
    {
        if (!GetWindowRect(h, out RECT r))
            throw new FalhaDeTeste($"GetWindowRect falhou para a janela {h}.");
        return r;
    }

    internal static POINT Cursor()
    {
        GetCursorPos(out POINT p);
        return p;
    }

    internal static bool ClickLock()
    {
        int v = 0;
        SpiGetInt(SPI_GETMOUSECLICKLOCK, 0, ref v, 0);
        return v != 0;
    }

    // Só em memória: fWinIni = 0 não grava no perfil nem avisa as outras janelas.
    internal static void DefinirClickLock(bool ligado)
        => SpiSetValor(SPI_SETMOUSECLICKLOCK, 0, ligado ? 1 : 0, 0);

    internal static int TempoClickLockMs()
    {
        int v = 0;
        SpiGetInt(SPI_GETMOUSECLICKLOCKTIME, 0, ref v, 0);
        return v;
    }

    // Último input de qualquer dispositivo, injetado incluso, em ms do GetTickCount (32 bits).
    // Diz só QUANDO, nunca qual tecla ou botão. Null se falhar.
    internal static uint? UltimoInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? info.dwTime : null;
    }

    // Mesmo relógio do GetLastInputInfo.
    internal static uint Agora() => unchecked((uint)Environment.TickCount);

    // Com sinal e certo na volta do contador de 32 bits. Sem sinal, um input 1 ms "no futuro"
    // viraria 49 dias de ociosidade.
    internal static int MsDepoisDe(uint instante, uint referencia) => unchecked((int)(instante - referencia));

    internal static MONITORINFOEX? Monitor(POINT p)
    {
        nint h = MonitorFromPoint(p, MONITOR_DEFAULTTONULL);
        if (h == 0) return null;
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(h, ref mi) ? mi : null;
    }
}
