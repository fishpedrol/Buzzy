using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Buzzy.App.Plataforma;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Chamadas ao Windows usadas SÓ pelos testes, fora do produto. Agem sobre as janelas do
/// Buzzy aberto pelo próprio teste, conferem a qual processo uma janela pertence, consultam o
/// contexto de DPI do próprio processo de testes e listam processos filhos do Buzzy. Também
/// contam os objetos GDI e USER do processo de testes e do Buzzy que ele abriu, e leem os
/// bitmaps que o próprio processo de testes criou.
/// </summary>
internal static class NativoTeste
{
    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TOPMOST = 0x00000008L;
    internal const long WS_EX_TRANSPARENT = 0x00000020L;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_APPWINDOW = 0x00040000L;
    internal const long WS_EX_LAYERED = 0x00080000L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;

    internal const int WM_CLOSE = 0x0010;
    internal const int WM_SETTINGCHANGE = 0x001A;
    internal const int WM_CANCELMODE = 0x001F;
    internal const int WM_DISPLAYCHANGE = 0x007E;
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;
    internal const int WM_POWERBROADCAST = 0x0218;
    internal const int WM_MOUSEMOVE = 0x0200;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int MK_LBUTTON = 0x0001;
    internal const int SPI_SETWORKAREA = 0x002F;
    internal const int WTS_SESSION_LOCK = 0x7;
    internal const int WTS_SESSION_UNLOCK = 0x8;
    internal const int PBT_APMSUSPEND = 0x0004;
    internal const int PBT_APMRESUMESUSPEND = 0x0007;
    internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    internal const int SW_SHOWMINNOACTIVE = 7;

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.</summary>
    internal const nint ContextoPerMonitorV2 = -4;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out RECT r);

    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>
    /// Mensagens do sistema que levam ponteiro, como WM_SETTINGCHANGE, não podem ser postadas
    /// a outro processo (PostMessage falha com ERROR_MESSAGE_SYNC_ONLY, 1159): o Windows as
    /// entrega por envio síncrono, e o teste faz o mesmo.
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static extern nint SendMessageTimeout(nint hWnd, int msg, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint resultado);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>O topo do grupo "sempre no topo", como "inserir depois de" no SetWindowPos.</summary>
    internal const nint HWND_TOPMOST = -1;

    /// <summary>Só para mover a janela do Buzzy aberto pelo próprio teste, "por fora" do núcleo.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    internal static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AreDpiAwarenessContextsEqual(nint contextoA, nint contextoB);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    // ---- objetos GDI e USER (bitmaps do menu, DEC-027) ------------------------------------

    internal const uint GR_GDIOBJECTS = 0;
    internal const uint GR_USEROBJECTS = 1;

    /// <summary>Quantos objetos GDI ou USER o processo tem abertos: só para o próprio processo de testes e o Buzzy que ele abriu.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetGuiResources(nint hProcess, uint uiFlags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    /// <summary>DIBSECTION: 104 bytes em x64. <c>dsBm.bmBits</c> aponta para os pixels do DIB, na memória deste processo.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DIBSECTION
    {
        public BITMAP dsBm;
        public Win32.BITMAPINFOHEADER dsBmih;
        public uint dsBitfields0;
        public uint dsBitfields1;
        public uint dsBitfields2;
        public nint dshSection;
        public uint dsOffset;
    }

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(nint objeto, int tamanho, out DIBSECTION dib);

    /// <summary>Lê o cabeçalho e o endereço dos pixels de um bitmap criado por CreateDIBSection neste processo.</summary>
    internal static DIBSECTION LerDib(nint bitmap)
    {
        int lidos = GetObject(bitmap, Marshal.SizeOf<DIBSECTION>(), out DIBSECTION dib);
        if (lidos != Marshal.SizeOf<DIBSECTION>())
            throw new InvalidOperationException($"GetObject devolveu {lidos} bytes: {bitmap} não é uma seção DIB viva.");
        return dib;
    }

    /// <summary>Se o handle ainda é um objeto GDI vivo (depois de DeleteObject, GetObject devolve 0).</summary>
    internal static bool ObjetoGdiVivo(nint objeto) => GetObject(objeto, Marshal.SizeOf<DIBSECTION>(), out _) != 0;

    /// <summary>BITMAPINFO de 32 bits, com espaço para as máscaras de cor que GetDIBits pode escrever.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO32
    {
        public Win32.BITMAPINFOHEADER bmiHeader;
        public uint mascara0;
        public uint mascara1;
        public uint mascara2;
        public uint mascara3;
    }

    /// <summary>DC de memória, sem ligação com a tela: só o formato que GetDIBits pede.</summary>
    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint bitmap, uint primeiraLinha, uint linhas, [Out] int[] pixels, ref BITMAPINFO32 info, uint uso);

    /// <summary>
    /// A imagem que o Windows enxerga num bitmap criado por este processo, pedida de cima para baixo em 32 bits: confere a
    /// orientação pelo que o GDI desenharia, não pelo cabeçalho (GetObject informa a altura sem o sinal).
    /// </summary>
    internal static uint[] ImagemDoBitmap(nint bitmap, int largura, int altura)
    {
        nint dc = CreateCompatibleDC(0);
        if (dc == 0) throw new InvalidOperationException("CreateCompatibleDC falhou.");
        try
        {
            var info = new BITMAPINFO32
            {
                bmiHeader = new Win32.BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                    biWidth = largura,
                    biHeight = -altura, // pedido de cima para baixo
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = Win32.BI_RGB,
                },
            };
            int[] pixels = new int[largura * altura];
            int lidas = GetDIBits(dc, bitmap, 0, (uint)altura, pixels, ref info, Win32.DIB_RGB_COLORS);
            if (lidas != altura) throw new InvalidOperationException($"GetDIBits leu {lidas} de {altura} linhas.");
            return [.. pixels.Select(p => unchecked((uint)p))];
        }
        finally
        {
            DeleteDC(dc);
        }
    }

    internal static uint ObjetosDesteProcesso(uint tipo)
    {
        using Process atual = Process.GetCurrentProcess();
        return GetGuiResources(atual.Handle, tipo);
    }

    // ---- leitura de um menu montado pelo próprio processo de testes ----------------------

    /// <summary>MENUITEMINFOW para leitura: o texto vem num buffer alocado pelo teste.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MENUITEMINFO_LEITURA
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
        public nint dwTypeData;
        public uint cch;
        public nint hbmpItem;
    }

    [DllImport("user32.dll")]
    internal static extern int GetMenuItemCount(nint hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsMenu(nint hMenu);

    [DllImport("user32.dll", EntryPoint = "GetMenuItemInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMenuItemInfo(nint hMenu, uint item, [MarshalAs(UnmanagedType.Bool)] bool porPosicao, ref MENUITEMINFO_LEITURA mii);

    /// <summary>Um item lido de volta do HMENU: tipo (MFT_*), estado (MFS_*), id, submenu, bitmap e texto.</summary>
    internal sealed record ItemDoMenuLido(uint Tipo, uint Estado, uint Id, nint Submenu, nint Bitmap, string Texto);

    /// <summary>Lê o item da posição dada de um menu criado por este processo.</summary>
    internal static ItemDoMenuLido LerItemDoMenu(nint menu, uint posicao)
    {
        const uint MIIM_STATE = 0x1, MIIM_ID = 0x2, MIIM_SUBMENU = 0x4, MIIM_STRING = 0x40, MIIM_BITMAP = 0x80, MIIM_FTYPE = 0x100;
        var mii = new MENUITEMINFO_LEITURA
        {
            cbSize = Marshal.SizeOf<MENUITEMINFO_LEITURA>(),
            fMask = MIIM_STATE | MIIM_ID | MIIM_SUBMENU | MIIM_STRING | MIIM_BITMAP | MIIM_FTYPE,
        };
        if (!GetMenuItemInfo(menu, posicao, true, ref mii))
            throw new InvalidOperationException($"GetMenuItemInfoW falhou na posição {posicao} (erro {Marshal.GetLastPInvokeError()}).");

        string texto = "";
        if (mii.cch > 0)
        {
            // Com dwTypeData nulo, cch volta com o tamanho do texto; a segunda chamada o copia, com o terminador.
            uint tamanho = mii.cch + 1;
            nint buffer = Marshal.AllocHGlobal((int)tamanho * sizeof(char));
            try
            {
                var leitura = new MENUITEMINFO_LEITURA { cbSize = mii.cbSize, fMask = MIIM_STRING, dwTypeData = buffer, cch = tamanho };
                if (!GetMenuItemInfo(menu, posicao, true, ref leitura))
                    throw new InvalidOperationException($"GetMenuItemInfoW (texto) falhou na posição {posicao}.");
                texto = Marshal.PtrToStringUni(buffer) ?? "";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        return new ItemDoMenuLido(mii.fType, mii.fState, mii.wID, mii.hSubMenu, mii.hbmpItem, texto);
    }

    // ---- ordem Z e primeiro plano (itens do tamagotchi, DEC-028) -------------------------

    internal const int WM_CHAR = 0x0102;
    internal const int WM_CONTEXTMENU = 0x007B;
    internal const uint GW_HWNDNEXT = 2;

    /// <summary>A janela seguinte na ordem Z (GW_HWNDNEXT): só para conferir a ordem das janelas do Buzzy aberto pelo teste.</summary>
    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hWnd, uint uCmd);

    /// <summary>
    /// A janela em primeiro plano, só para conferir que nenhuma janela do Buzzy aberto pelo teste a tomou (o produto não
    /// usa esta função); o teste compara só o PID dela, sem ler nada da janela.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    /// <summary>
    /// Quantas janelas há descendo a ordem Z de <paramref name="de"/> até <paramref name="ate"/> (1 = logo abaixo), só entre
    /// janelas do processo de <paramref name="de"/>; -1 se <paramref name="ate"/> não está abaixo dela ou se uma janela de
    /// outro processo aparece antes (o percurso para nela: de outros aplicativos, só o PID de uma janela é lido).
    /// </summary>
    internal static int PosicoesAbaixo(nint de, nint ate)
    {
        uint pid = PidDe(de);
        nint h = de;
        for (int i = 1; i <= 1000; i++)
        {
            h = GetWindow(h, GW_HWNDNEXT);
            if (h == 0) return -1;
            if (h == ate) return i;
            if (PidDe(h) != pid) return -1;
        }
        return -1;
    }

    // ---- janela intrusa na ordem Z (itens do tamagotchi ao mostrar de novo) -------------------

    private const uint WS_POPUP = 0x80000000;
    private const uint SWP_NOMOVE = 0x0002;

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint estiloEstendido, string classe, string? titulo, uint estilo, int x, int y, int largura, int altura,
        nint pai, nint menu, nint instancia, nint parametro);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    /// <summary>
    /// Uma janela ESCONDIDA do próprio processo de testes (classe STATIC do sistema, nunca mostrada nem ativada), posta no
    /// topo do grupo "sempre no topo": faz o papel de outra janela "sempre no topo" que passa à frente do Buzzy enquanto
    /// ele está escondido (um player de vídeo, a barra de tarefas). Uma janela escondida também tem lugar na ordem Z. Tem de
    /// ser destruída na mesma thread que a criou.
    /// </summary>
    internal sealed class JanelaIntrusa : IDisposable
    {
        internal JanelaIntrusa()
        {
            Hwnd = CreateWindowEx((uint)(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE), "STATIC", null, WS_POPUP, 0, 0, 1, 1, 0, 0, 0, 0);
            if (Hwnd == 0) throw new InvalidOperationException($"CreateWindowExW falhou (erro {Marshal.GetLastPInvokeError()}).");
        }

        internal nint Hwnd { get; private set; }

        /// <summary>No topo do grupo "sempre no topo", sem mostrar nem ativar.</summary>
        internal void PorNoTopo()
        {
            if (!SetWindowPos(Hwnd, -1, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE))
                throw new InvalidOperationException("SetWindowPos da janela intrusa falhou.");
        }

        public void Dispose()
        {
            if (Hwnd == 0) return;
            DestroyWindow(Hwnd);
            Hwnd = 0;
        }
    }

    internal const uint GW_HWNDPREV = 3;

    /// <summary>
    /// Se <paramref name="alto"/> está acima de <paramref name="baixo"/> na ordem Z, subindo de <paramref name="baixo"/> por
    /// GW_HWNDPREV, no máximo 5000 passos: só handles são comparados com <paramref name="alto"/>, e nada de janela alguma é
    /// lido (nem o PID, nem título, classe ou retângulo). As duas janelas são deste teste ou do Buzzy que ele abriu; outras
    /// podem estar no meio, porque outros aplicativos também têm janelas "sempre no topo". Diferente de
    /// <see cref="PosicoesAbaixo"/>, que para na primeira janela de outro processo depois de ler o PID dela (SECURITY.md
    /// 3.2, "Ferramentas de teste": o texto dessa seção ainda descreve só a varredura que para; revisão de segurança do bloco
    /// P6-P9, achado 3). Só os testes da janela intrusa a usam.
    /// </summary>
    internal static bool EstaAcima(nint alto, nint baixo)
    {
        nint h = baixo;
        for (int i = 0; i < 5000; i++)
        {
            h = GetWindow(h, GW_HWNDPREV);
            if (h == 0) return false;
            if (h == alto) return true;
        }
        return false;
    }

    // ---- captura do mouse (fim do gesto num item do tamagotchi) ---------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO gui);

    /// <summary>
    /// A janela com a captura do mouse na thread que criou <paramref name="janela"/> (0 = nenhuma), só para conferir que
    /// uma janela do Buzzy aberto pelo teste soltou o mouse. Lê só a thread dessa janela; lança se o Windows não informar.
    /// </summary>
    internal static nint CapturaNaThreadDe(nint janela)
    {
        uint thread = GetWindowThreadProcessId(janela, out _);
        if (thread == 0) throw new InvalidOperationException($"A janela {janela} não existe: sem thread para consultar.");
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(thread, ref info))
            throw new InvalidOperationException($"GetGUIThreadInfo falhou (erro {Marshal.GetLastPInvokeError()}).");
        return info.hwndCapture;
    }

    /// <summary>MAKELPARAM(baixo, alto), com o sinal de cada metade preservado (coordenadas negativas).</summary>
    internal static nint MakeLParam(int baixo, int alto)
        => unchecked((nint)(int)(((uint)(ushort)(short)alto << 16) | (ushort)(short)baixo));

    internal static long EstiloEstendido(nint hwnd) => (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);

    /// <summary>PID do processo dono da janela; 0 se a janela não existe.</summary>
    internal static uint PidDe(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    /// <summary>
    /// Se a thread atual está em Per-Monitor V2, como o Buzzy. Fora dele, o Windows entrega a
    /// este processo coordenadas virtualizadas (escaladas) nos monitores com escala diferente
    /// de 100%, e elas não batem com o log do Buzzy, que está em pixels físicos. Só consulta o
    /// contexto de DPI: não abre janela.
    /// </summary>
    internal static bool ThreadEmPerMonitorV2()
        => AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), ContextoPerMonitorV2);

    /// <summary>
    /// PIDs dos processos cujo pai registrado é <paramref name="pid"/> e que foram criados a
    /// partir de <paramref name="inicioDoPai"/>. O Windows guarda só o número do pai, e esse
    /// número pode ser de um processo antigo, já encerrado, que o Buzzy reaproveitou; a hora de
    /// criação separa os filhos verdadeiros. Um processo cuja hora de criação não dá para ler
    /// entra na lista, por segurança.
    /// </summary>
    internal static List<int> Filhos(int pid, DateTime inicioDoPai)
    {
        const uint TH32CS_SNAPPROCESS = 0x00000002;
        var candidatos = new List<int>();
        nint instantaneo = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (instantaneo == -1) throw new InvalidOperationException("CreateToolhelp32Snapshot falhou.");
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (bool ok = Process32First(instantaneo, ref e); ok; ok = Process32Next(instantaneo, ref e))
            {
                if (e.th32ParentProcessID == (uint)pid && e.th32ProcessID != (uint)pid) candidatos.Add((int)e.th32ProcessID);
            }
        }
        finally
        {
            CloseHandle(instantaneo);
        }
        return [.. candidatos.Where(c => CriadoAPartirDe(c, inicioDoPai))];
    }

    private static bool CriadoAPartirDe(int pid, DateTime referencia)
    {
        try
        {
            using Process processo = Process.GetProcessById(pid);
            return processo.StartTime >= referencia;
        }
        catch (ArgumentException)
        {
            return false; // já encerrou entre o instantâneo e agora: não é um filho vivo
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return true; // hora de criação ilegível: conta, por segurança
        }
    }
}
