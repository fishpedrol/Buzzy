using System.Runtime.InteropServices;

namespace Buzzy.App.Plataforma;

/// <summary>
/// As declarações de chamadas ao Windows do Buzzy, num lugar só (adaptador de plataforma,
/// DEC-007), menos as do observador de tela cheia, que ficam nele (<see cref="ObservadorDeTelaCheia"/>):
/// o portão de APIs proibidas (SECURITY.md 3.2 e 8) só as permite naquele tipo, e inspeciona
/// todas as declarações no binário a cada build.
///
/// Limites respeitados de propósito: nenhum hook, nenhuma injeção de input, nenhuma captura
/// de tela, nenhuma rede, nenhum processo, nenhuma leitura de título, texto ou identidade de
/// janelas de outros aplicativos. A única geometria de outro aplicativo lida é o retângulo da
/// janela em primeiro plano, pelo observador de tela cheia, com <c>GetWindowRect</c> daqui (DEC-034). As funções abaixo só agem sobre janelas do
/// próprio Buzzy, sobre a topologia dos monitores (e a configuração de vídeo deles, lida só
/// para a chave estável do monitor), sobre o ícone da bandeja e sobre o menu do Buzzy, com os
/// bitmaps dos ícones dele criados na memória do próprio processo.
///
/// DllImport, e não LibraryImport, de propósito: o marshalling gerado do LibraryImport não
/// trata o campo ByValTStr de MONITORINFOEX nem os campos de texto de NOTIFYICONDATA, e
/// GetMonitorInfo passa a devolver zeros sem erro (defeito já visto no protótipo P1).
/// </summary>
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

    /// <summary>
    /// Põe a janela acima de todas as janelas comuns, sem ativar e sem deixá-la topmost (DEC-038, item 8): entra no grupo
    /// topmost e sai dele logo em seguida, ficando no topo da faixa comum. Uma vez, por pedido; nunca por timer.
    /// </summary>
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

    /// <summary>Retângulo de uma janela do próprio Buzzy, em pixels físicos.</summary>
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

    // ---- user32: captura do mouse no gesto começado no personagem (ARCHITECTURE.md 2.7) ----

    /// <summary>
    /// Captura o mouse para a janela do personagem durante um gesto que começou nela. Como a janela
    /// não é a de primeiro plano, o Windows só entrega o mouse a ela enquanto um botão estiver
    /// pressionado; ela nunca lê input fora desse gesto (SECURITY.md 3.1).
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern nint SetCapture(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    /// <summary>Tempo de clique duplo do sistema, em milissegundos (ARCHITECTURE.md 2.7, regra 4).</summary>
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

    /// <summary>Fecha o menu ativo desta thread, se houver (usado ao encerrar com o menu aberto).</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EndMenu();

    // ---- Menu com ícones (DEC-027): submenus e itens com bitmap ------------------------
    //
    // Os itens entram por InsertMenuItemW, numa posição explícita. O ícone de um item (hbmpItem) é um bitmap
    // de 32 bits criado só na memória do próprio Buzzy (CreateDIBSection sem DC, preenchido por Marshal.Copy):
    // nenhum pixel é lido da tela nem de outra janela. DestroyMenu não apaga hbmpItem; quem cria o bitmap o
    // apaga com DeleteObject depois do DestroyMenu (BitmapsDoMenu).

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

    /// <summary>MENUITEMINFOW: 80 bytes em x64.</summary>
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

    /// <summary>BITMAPINFOHEADER: 40 bytes. Com 32 bits por pixel e BI_RGB, o DIB não tem tabela de cores.</summary>
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

    /// <summary>Bitmap de 32 bits na memória do próprio processo; com <c>hdc = 0</c> e DIB_RGB_COLORS, nenhum DC é usado.</summary>
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

    // ---- user32: configuração de vídeo, só para a chave estável do monitor (DEC-030) ----
    //
    // Leitura, nunca mudança: os caminhos ativos (fonte GDI e alvo de cada um) e, de cada alvo, o caminho do
    // dispositivo, que vira um resumo opaco em ChavesDeMonitor e nunca é gravado nem registrado. O nome amigável
    // do monitor vem junto no mesmo pacote, mas nunca é lido. Estas funções devolvem o código de erro do Windows
    // direto (0 = sucesso), sem o último erro da thread. Os tamanhos são conferidos em PlataformaTestes: com um
    // tamanho errado, o Windows recusa o pedido.

    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    internal const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    internal const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    internal const int ERROR_SUCCESS = 0;
    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;

    /// <summary>LUID: 8 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    /// <summary>DISPLAYCONFIG_RATIONAL: 8 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    /// <summary>DISPLAYCONFIG_PATH_SOURCE_INFO: 20 bytes. A fonte é o dispositivo GDI (<c>\\.\DISPLAYn</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    /// <summary>DISPLAYCONFIG_PATH_TARGET_INFO: 48 bytes. O alvo é o monitor ligado à fonte.</summary>
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

    /// <summary>DISPLAYCONFIG_PATH_INFO: 72 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    /// <summary>
    /// DISPLAYCONFIG_MODE_INFO: 64 bytes (a união dos modos é alinhada a 8 e começa no byte 16). O Buzzy só
    /// precisa reservar o espaço que o Windows preenche: os modos nunca são lidos.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public int infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
    }

    /// <summary>DISPLAYCONFIG_DEVICE_INFO_HEADER: 20 bytes; <c>size</c> é o tamanho do pacote inteiro.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    /// <summary>DISPLAYCONFIG_SOURCE_DEVICE_NAME: 84 bytes; o nome GDI da fonte, o mesmo de MONITORINFOEX.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    /// <summary>
    /// DISPLAYCONFIG_TARGET_DEVICE_NAME: 420 bytes. Do alvo, o Buzzy só usa <c>monitorDevicePath</c>, e só para o
    /// resumo da chave (ConfiguracaoDeVideo). Os códigos do EDID e o nome amigável ocupam o espaço do pacote e
    /// nunca são lidos (SECURITY.md 6).
    /// </summary>
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

    /// <summary>Com <see cref="QDC_ONLY_ACTIVE_PATHS"/>, o último argumento é sempre 0.</summary>
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

    /// <summary>
    /// Extrai as coordenadas preservando o sinal (ARCHITECTURE.md 2.13.3): um monitor à
    /// esquerda do principal produz x negativo.
    /// </summary>
    internal static int XComSinal(nint valor) => unchecked((short)(long)valor);

    internal static int YComSinal(nint valor) => unchecked((short)((long)valor >> 16));

    internal static int LoWord(nint valor) => unchecked((ushort)(long)valor);

    internal static int HiWord(nint valor) => unchecked((ushort)((long)valor >> 16));
}
