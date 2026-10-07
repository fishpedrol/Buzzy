using System.Runtime.InteropServices;

namespace Buzzy.App.Plataforma;

// Todas as chamadas ao Windows num lugar só, menos as do ObservadorDeTelaCheia, que o
// portão de APIs só libera lá. O portão inspeciona as declarações no binário a cada build.
//
// De propósito: nada de hook, injeção de input, captura de tela, rede, processo ou
// leitura de título/texto de janela alheia. A única geometria alheia é o retângulo da
// janela em primeiro plano (via observador, com o GetWindowRect daqui).
//
// DllImport e não LibraryImport: o marshalling gerado não trata ByValTStr de
// MONITORINFOEX/NOTIFYICONDATA e o GetMonitorInfo volta zeros sem erro.
internal static class Win32
{
    // ---- Estilos -------------------------------------------------------------------
    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;
    internal const int WS_POPUP = unchecked((int)0x80000000);

    // ---- Mensagens -----------------------------------------------------------------
    internal const int WM_NULL = 0x0000;
    internal const int WM_SETTINGCHANGE = 0x001A;
    internal const int WM_CANCELMODE = 0x001F;
    internal const int WM_MOUSEACTIVATE = 0x0021;
    internal const int WM_DISPLAYCHANGE = 0x007E;
    internal const int WM_CONTEXTMENU = 0x007B;
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;
    internal const int WM_POWERBROADCAST = 0x0218;
    internal const int WM_MOUSEMOVE = 0x0200;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_LBUTTONDBLCLK = 0x0203;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_CAPTURECHANGED = 0x0215;
    internal const int WM_GETDPISCALEDSIZE = 0x02E4;
    internal const int WM_APP = 0x8000;
    internal const int MA_NOACTIVATE = 3;
    internal const int MK_LBUTTON = 0x0001;
    internal const int SPI_SETWORKAREA = 0x002F;

    // ---- Sessão e energia -----------------------------------------------------------
    internal const uint NOTIFY_FOR_THIS_SESSION = 0;
    internal const int WTS_SESSION_LOCK = 0x7;
    internal const int WTS_SESSION_UNLOCK = 0x8;
    internal const int PBT_APMSUSPEND = 0x0004;
    internal const int PBT_APMRESUMESUSPEND = 0x0007;
    internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;

    // ---- SetWindowPos --------------------------------------------------------------
    internal static readonly nint HWND_TOPMOST = -1;
    internal static readonly nint HWND_NOTOPMOST = -2;

    // Truque pra subir acima das janelas comuns sem ativar e sem ficar topmost: entra no
    // grupo topmost e sai logo em seguida. Só sob pedido, nunca por timer.
    internal static void AoTopoDaFaixaComum(nint hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    // ---- Monitores -----------------------------------------------------------------
    internal const uint MONITORINFOF_PRIMARY = 0x00000001;
    internal const int MDT_EFFECTIVE_DPI = 0;

    // ---- Menu ----------------------------------------------------------------------
    internal const uint TPM_LEFTALIGN = 0x0000;
    internal const uint TPM_TOPALIGN = 0x0000;
    internal const uint TPM_BOTTOMALIGN = 0x0020;
    internal const uint TPM_RIGHTBUTTON = 0x0002;
    internal const uint TPM_NONOTIFY = 0x0080;
    internal const uint TPM_RETURNCMD = 0x0100;

    // ---- Bandeja -------------------------------------------------------------------
    internal const uint NIM_ADD = 0x00000000;
    internal const uint NIM_MODIFY = 0x00000001;
    internal const uint NIM_DELETE = 0x00000002;
    internal const uint NIM_SETFOCUS = 0x00000003;
    internal const uint NIM_SETVERSION = 0x00000004;
    internal const uint NIF_MESSAGE = 0x00000001;
    internal const uint NIF_ICON = 0x00000002;
    internal const uint NIF_TIP = 0x00000004;
    internal const uint NIF_SHOWTIP = 0x00000080;
    internal const uint NOTIFYICON_VERSION_4 = 4;
    internal const int NIN_SELECT = 0x0400;
    internal const int NIN_KEYSELECT = 0x0401;

    // ---- Métricas ------------------------------------------------------------------
    internal const int SM_CXDOUBLECLK = 36;
    internal const int SM_CYDOUBLECLK = 37;
    internal const int SM_CXSMICON = 49;
    internal const int SM_CXDRAG = 68;
    internal const int SM_CYDRAG = 69;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NOTIFYICONIDENTIFIER
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public Guid guidItem;
    }

    internal delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint dwData);

    // ---- user32: janelas do próprio Buzzy ------------------------------------------

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

    // Em px físicos.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int RegisterWindowMessage(string lpString);

    // ---- WTS: notificações de bloqueio e desbloqueio da sessão atual ----------------

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSRegisterSessionNotification(nint hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSUnRegisterSessionNotification(nint hWnd);

    // ---- user32: captura do mouse no gesto começado no personagem ----

    // Só durante um gesto que começou no personagem. Como a janela não está em primeiro
    // plano, o Windows só entrega o mouse com botão pressionado: nada de input fora disso.
    [DllImport("user32.dll")]
    internal static extern nint SetCapture(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    // Em ms.
    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    // ---- user32: menu nativo ---------------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hWnd, nint lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint hMenu);

    // Usado ao encerrar com o menu aberto.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EndMenu();

    // ---- Menu com ícones: submenus e itens com bitmap ------------------------
    //
    // hbmpItem é um DIB 32 bits só na nossa memória (CreateDIBSection sem DC +
    // Marshal.Copy): nenhum pixel vem da tela. DestroyMenu não apaga hbmpItem; quem
    // apaga é o BitmapsDoMenu, depois.

    internal const uint MIIM_STATE = 0x00000001;
    internal const uint MIIM_ID = 0x00000002;
    internal const uint MIIM_SUBMENU = 0x00000004;
    internal const uint MIIM_STRING = 0x00000040;
    internal const uint MIIM_BITMAP = 0x00000080;
    internal const uint MIIM_FTYPE = 0x00000100;
    internal const uint MFT_STRING = 0x00000000;
    internal const uint MFT_RADIOCHECK = 0x00000200;
    internal const uint MFT_SEPARATOR = 0x00000800;
    internal const uint MFS_ENABLED = 0x00000000;
    internal const uint MFS_GRAYED = 0x00000003;
    internal const uint MFS_CHECKED = 0x00000008;
    internal const uint BI_RGB = 0;
    internal const uint DIB_RGB_COLORS = 0;

    // 80 bytes em x64.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MENUITEMINFO
    {
        public int cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public nint hSubMenu;
        public nint hbmpChecked;
        public nint hbmpUnchecked;
        public nint dwItemData;
        public string? dwTypeData;
        public uint cch;
        public nint hbmpItem;
    }

    // 40 bytes. Com 32 bpp e BI_RGB não tem tabela de cores.
    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("user32.dll", EntryPoint = "InsertMenuItemW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InsertMenuItem(nint hMenu, uint item, [MarshalAs(UnmanagedType.Bool)] bool porPosicao, [In] ref MENUITEMINFO mii);

    // Com hdc = 0 e DIB_RGB_COLORS nenhum DC é usado.
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateDIBSection(nint hdc, [In] ref BITMAPINFOHEADER cabecalho, uint uso, out nint bits, nint secao, uint deslocamento);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint objeto);

    // ---- user32: monitores -----------------------------------------------------------

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    // ---- user32: configuração de vídeo, só pra chave estável do monitor ----
    //
    // Só leitura. O caminho do dispositivo vira hash opaco em ChavesDeMonitor e nunca é
    // gravado nem logado; o nome amigável vem no pacote mas nunca é lido. Essas funções
    // devolvem o erro direto (0 = sucesso), sem GetLastError. Tamanho de struct errado
    // e o Windows recusa: os tamanhos são conferidos em PlataformaTestes.

    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    internal const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    internal const int ERROR_SUCCESS = 0;
    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;

    // 8 bytes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    // 8 bytes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    // 20 bytes. A fonte é o dispositivo GDI (\\.\DISPLAYn).
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    // 48 bytes. O alvo é o monitor ligado à fonte.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public int outputTechnology;
        public int rotation;
        public int scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public int scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    // 72 bytes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    // 64 bytes (a união alinha em 8 e começa no byte 16). Só reserva espaço; os modos
    // nunca são lidos.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public int infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
    }

    // 20 bytes; size é o tamanho do pacote inteiro.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    // 84 bytes. Mesmo nome GDI do MONITORINFOEX.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    // 420 bytes. Só monitorDevicePath é usado (pro hash da chave); EDID e nome amigável
    // só ocupam espaço e nunca são lidos.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public int outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    // Com QDC_ONLY_ACTIVE_PATHS o último argumento é sempre 0.
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    // ---- user32 e shell32: ícone da bandeja --------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint CreateIconFromResourceEx(byte[] presbits, int dwResSize, [MarshalAs(UnmanagedType.Bool)] bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint hIcon);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll")]
    internal static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    // ---- auxiliares ------------------------------------------------------------------

    // Com sinal: monitor à esquerda do principal dá x negativo.
    internal static int XComSinal(nint valor) => unchecked((short)(long)valor);

    internal static int YComSinal(nint valor) => unchecked((short)((long)valor >> 16));

    internal static int LoWord(nint valor) => unchecked((ushort)(long)valor);

    internal static int HiWord(nint valor) => unchecked((ushort)((long)valor >> 16));
}
