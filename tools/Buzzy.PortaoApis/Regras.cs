namespace Buzzy.PortaoApis;

/// <summary>
/// Capacidades proibidas, na ordem da tabela de SECURITY.md 3.2. <see cref="Manifesto"/> não
/// vem de 3.2: cobre SECURITY.md 8, item 5 (sem elevação) e ARCHITECTURE.md 2.4 (Per-Monitor V2).
/// </summary>
internal enum Categoria
{
    InputGlobal,
    InjetarInput,
    CapturaDeTela,
    Processos,
    Rede,
    LerOutrosAplicativos,
    PersistenciaEscondida,
    CodigoDinamico,
    Manifesto,

    /// <summary>
    /// Alterar a configuração global do Windows (AGENTS.md; regra dura do projeto): só o usuário muda vídeo, energia e
    /// sessão. Fica no fim do enum para não renumerar as outras (revisão de segurança do bloco P6-P9, achado 6).
    /// </summary>
    ConfiguracaoGlobal,
}

internal static class Categorias
{
    /// <summary>Nome da linha correspondente em SECURITY.md 3.2.</summary>
    public static string Nome(this Categoria categoria) => categoria switch
    {
        Categoria.InputGlobal => "Input global",
        Categoria.InjetarInput => "Injetar input",
        Categoria.CapturaDeTela => "Captura de tela",
        Categoria.Processos => "Processos",
        Categoria.Rede => "Rede",
        Categoria.LerOutrosAplicativos => "Ler outros aplicativos",
        Categoria.PersistenciaEscondida => "Persistência escondida",
        Categoria.CodigoDinamico => "Código dinâmico",
        Categoria.Manifesto => "Manifesto",
        Categoria.ConfiguracaoGlobal => "Alterar configuração global",
        _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, null),
    };
}

/// <summary>O que uma regra da lista proibida reconhece nos binários.</summary>
internal enum TipoDeRegra
{
    /// <summary>
    /// Função nativa pelo nome, num P/Invoke ou na tabela de importação de um PE. Aceita o nome
    /// exato e os sufixos A e W, sem diferenciar maiúsculas, em qualquer módulo.
    /// </summary>
    FuncaoNativa,

    /// <summary>Qualquer função de um módulo nativo, sem diferenciar maiúsculas nem a extensão .dll.</summary>
    ModuloNativo,

    /// <summary>Referência a um tipo gerenciado pelo nome completo.</summary>
    TipoGerenciado,

    /// <summary>Referência a qualquer tipo de um namespace ou dos namespaces abaixo dele.</summary>
    NamespaceGerenciado,

    /// <summary>Referência a um membro de um tipo gerenciado, em qualquer sobrecarga.</summary>
    MembroGerenciado,

    /// <summary>
    /// Método de interface COM pelo nome: declarado numa interface do próprio assembly (como
    /// fica uma interface [ComImport] escrita em C#) ou referenciado em outro assembly.
    /// </summary>
    MetodoCom,
}

/// <summary>Uma entrada da lista proibida.</summary>
/// <param name="Tipo">O que a regra reconhece nos binários.</param>
/// <param name="Alvo">
/// Nome da função, do módulo sem extensão, do tipo completo, do namespace ou do método COM.
/// Em <see cref="TipoDeRegra.MembroGerenciado"/>, o tipo que declara o membro.
/// </param>
/// <param name="Membro">Só em <see cref="TipoDeRegra.MembroGerenciado"/>: o nome do membro.</param>
/// <param name="Categoria">Linha de SECURITY.md 3.2.</param>
/// <param name="Motivo">Por que a API dá a capacidade proibida; aparece no relatório.</param>
/// <param name="PadroesNaFonte">
/// Palavras inteiras, ou sequências de palavras separadas por ponto, procuradas no código-fonte
/// sem diferenciar maiúsculas. Lista vazia: a regra só vale para os binários, e o comentário da
/// regra explica por quê.
/// </param>
internal sealed record Regra(
    TipoDeRegra Tipo,
    string Alvo,
    string? Membro,
    Categoria Categoria,
    string Motivo,
    IReadOnlyList<string> PadroesNaFonte)
{
    /// <summary>Nome legível: <c>SendInput</c>, <c>ws2_32</c>, <c>System.Diagnostics.Process.Start</c>.</summary>
    public string Descricao => Membro is null ? Alvo : $"{Alvo}.{Membro}";
}

/// <summary>
/// A lista proibida: tabela única com todas as regras do portão, agrupadas pelas categorias de
/// SECURITY.md 3.2. Cada regra traz o motivo. Entradas marcadas "(além da lista mínima)" foram
/// acrescentadas por darem a mesma capacidade por outro nome; nenhuma é usada pelo produto.
///
/// Exceções deliberadas, que NÃO estão aqui e portanto são permitidas:
/// - System.Diagnostics.Process como tipo, Process.GetCurrentProcess e os membros de leitura do
///   próprio processo (Id, StartTime, WorkingSet64 e afins): o Buzzy pode medir a si mesmo.
///   Só Start, GetProcesses, GetProcessesByName e GetProcessById são proibidos.
/// - O namespace System.Windows.Automation como um todo: o WPF usa AutomationProperties e
///   System.Windows.Automation.Peers para a acessibilidade das próprias janelas. Só o lado
///   cliente, que lê outros processos (AutomationElement, Automation), é proibido.
/// - Microsoft.Win32 como namespace (SystemEvents avisa mudanças de tela e sessão). Só Registry
///   e RegistryKey são proibidos.
/// - Leitura nativa do registro (RegOpenKeyEx, RegQueryValueEx, RegGetValue): SECURITY.md 3.2
///   proíbe criar persistência, não ler. O acesso gerenciado ao registro é proibido inteiro,
///   porque Registry e RegistryKey servem para as duas coisas.
/// - Funções de janela e monitor que o adaptador de plataforma usa sobre as próprias janelas e a
///   topologia (SetWindowPos, GetWindowLongPtr, MonitorFromPoint, GetMonitorInfo,
///   EnumDisplayMonitors, Shell_NotifyIcon, SetForegroundWindow para o menu da bandeja e afins),
///   conforme SECURITY.md 3.1.
///
/// O Buzzy.exe (apphost) tem uma lista de permissões própria, em <see cref="PermissoesDoApphost"/>. O observador de tela
/// cheia (DEC-013 e DEC-034) usa três funções desta lista por uma permissão restrita a um tipo e a um arquivo, em
/// <see cref="UsosRestritos"/>; no resto do produto, elas continuam proibidas.
/// </summary>
internal static class ListaProibida
{
    public static readonly IReadOnlyList<Regra> Regras =
    [
        // ---- Input global ---------------------------------------------------------------
        Funcao("SetWindowsHookEx", Categoria.InputGlobal, "instala hook de teclado ou mouse que observa o sistema todo"),
        Funcao("SetWindowsHook", Categoria.InputGlobal, "forma antiga de SetWindowsHookEx, ainda exportada pelo user32 (além da lista mínima)"),
        Funcao("RegisterRawInputDevices", Categoria.InputGlobal, "registra input bruto; com RIDEV_INPUTSINK recebe teclado e mouse fora do foco"),
        Funcao("GetAsyncKeyState", Categoria.InputGlobal, "lê o estado de qualquer tecla do sistema, mesmo sem foco"),
        Funcao("GetKeyboardState", Categoria.InputGlobal, "lê o estado do teclado; serve para observar teclas fora do foco"),
        Funcao("GetKeyState", Categoria.InputGlobal, "lê o estado de teclas; o Buzzy só usa o input entregue às próprias janelas"),
        Funcao("RegisterHotKey", Categoria.InputGlobal, "atalho global de teclado; exige decisão aprovada"),

        // ---- Injetar input --------------------------------------------------------------
        // SendInput é permitido só em ferramentas de teste fora do executável (spikes/ e tools/
        // de teste), nunca no produto.
        Funcao("SendInput", Categoria.InjetarInput, "injeta teclado e mouse sintéticos"),
        Funcao("mouse_event", Categoria.InjetarInput, "injeta mouse sintético (antecessora de SendInput)"),
        Funcao("keybd_event", Categoria.InjetarInput, "injeta teclado sintético (antecessora de SendInput)"),
        Funcao("SetCursorPos", Categoria.InjetarInput, "move o cursor do usuário (além da lista mínima)"),
        Funcao("SetPhysicalCursorPos", Categoria.InjetarInput, "move o cursor do usuário em pixels físicos (além da lista mínima)"),
        Funcao("BlockInput", Categoria.InjetarInput, "bloqueia teclado e mouse do sistema todo (além da lista mínima)"),
        Funcao("InjectTouchInput", Categoria.InjetarInput, "injeta toque sintético (além da lista mínima)"),
        Funcao("InjectSyntheticPointerInput", Categoria.InjetarInput, "injeta ponteiro sintético (além da lista mínima)"),

        // ---- Captura de tela ------------------------------------------------------------
        Funcao("BitBlt", Categoria.CapturaDeTela, "copia pixels de um DC; sobre o desktop ou outra janela, captura a tela"),
        Funcao("StretchBlt", Categoria.CapturaDeTela, "copia pixels de um DC com escala; sobre o desktop ou outra janela, captura a tela"),
        Funcao("PrintWindow", Categoria.CapturaDeTela, "fotografa o conteúdo de uma janela"),
        Funcao("CreateDC", Categoria.CapturaDeTela, "cria DC do monitor ou do desktop, de onde se leem os pixels da tela"),
        // DEC-037, item 12: os pixels da tela também saem por um DC obtido sem CreateDC.
        Funcao("GetDC", Categoria.CapturaDeTela, "obtém o DC de uma janela ou da tela inteira, de onde se leem os pixels (além da lista mínima)"),
        Funcao("GetWindowDC", Categoria.CapturaDeTela, "obtém o DC de uma janela inteira, inclusive de outro aplicativo (além da lista mínima)"),
        Funcao("GetDCEx", Categoria.CapturaDeTela, "obtém o DC de uma janela ou da tela com opções (além da lista mínima)"),
        Funcao("GetPixel", Categoria.CapturaDeTela, "lê a cor de um pixel de um DC (além da lista mínima)"),
        Funcao("GetDIBits", Categoria.CapturaDeTela, "copia os pixels de um bitmap de um DC (além da lista mínima)"),
        Membro("System.Drawing.Graphics", "CopyFromScreen", Categoria.CapturaDeTela, "copia pixels da tela para uma imagem",
            fonte: ["CopyFromScreen"]),
        Namespace("Windows.Graphics.Capture", Categoria.CapturaDeTela, "Windows Graphics Capture: captura janelas e monitores",
            fonte: ["Windows.Graphics.Capture", "GraphicsCaptureItem", "GraphicsCaptureSession", "GraphicsCapturePicker",
                    "Direct3D11CaptureFramePool", "IGraphicsCaptureItemInterop"]),
        MetodoCom("DuplicateOutput", Categoria.CapturaDeTela, "Desktop Duplication (IDXGIOutput1::DuplicateOutput): copia a imagem do monitor"),
        MetodoCom("DuplicateOutput1", Categoria.CapturaDeTela, "Desktop Duplication (IDXGIOutput5::DuplicateOutput1) (além da lista mínima)"),
        MetodoCom("CreateForWindow", Categoria.CapturaDeTela, "IGraphicsCaptureItemInterop: cria item de captura de uma janela (além da lista mínima)"),
        MetodoCom("CreateForMonitor", Categoria.CapturaDeTela, "IGraphicsCaptureItemInterop: cria item de captura de um monitor (além da lista mínima)"),

        // ---- Processos ------------------------------------------------------------------
        Funcao("CreateProcess", Categoria.Processos, "inicia outro processo"),
        Funcao("CreateProcessAsUser", Categoria.Processos, "inicia outro processo com outro token"),
        Funcao("CreateProcessWithLogon", Categoria.Processos, "inicia outro processo com credenciais (além da lista mínima)"),
        Funcao("CreateProcessWithToken", Categoria.Processos, "inicia outro processo com outro token (além da lista mínima)"),
        Funcao("ShellExecute", Categoria.Processos, "abre programa, documento ou URL pelo shell"),
        Funcao("ShellExecuteEx", Categoria.Processos, "abre programa, documento ou URL pelo shell"),
        Funcao("WinExec", Categoria.Processos, "inicia outro processo (API antiga)"),
        // Qualificado na fonte: "Start" sozinho casaria com DispatcherTimer.Start e afins.
        Membro("System.Diagnostics.Process", "Start", Categoria.Processos, "inicia outro processo",
            fonte: ["Process.Start"]),
        Tipo("System.Diagnostics.ProcessStartInfo", Categoria.Processos, "descreve um processo a iniciar"),

        // ---- Rede -----------------------------------------------------------------------
        // O MVP não tem rede (SECURITY.md 1 e 6). Módulos inteiros: qualquer função deles.
        Modulo("ws2_32", Categoria.Rede, "Winsock: sockets"),
        Modulo("wsock32", Categoria.Rede, "Winsock antigo: sockets"),
        Modulo("mswsock", Categoria.Rede, "extensões do Winsock (além da lista mínima)"),
        Modulo("winhttp", Categoria.Rede, "WinHTTP: cliente HTTP"),
        Modulo("wininet", Categoria.Rede, "WinINet: cliente HTTP e FTP"),
        Modulo("httpapi", Categoria.Rede, "HTTP Server API: servidor HTTP (além da lista mínima)"),
        Modulo("websocket", Categoria.Rede, "WebSocket Protocol Component API (além da lista mínima)"),
        Funcao("URLDownloadToFile", Categoria.Rede, "baixa um arquivo da rede (urlmon)"),
        Funcao("URLDownloadToCacheFile", Categoria.Rede, "baixa um arquivo da rede (urlmon) (além da lista mínima)"),
        Funcao("URLOpenStream", Categoria.Rede, "lê conteúdo da rede (urlmon) (além da lista mínima)"),
        Funcao("URLOpenBlockingStream", Categoria.Rede, "lê conteúdo da rede (urlmon) (além da lista mínima)"),
        Funcao("URLOpenPullStream", Categoria.Rede, "lê conteúdo da rede (urlmon) (além da lista mínima)"),
        // A lista de nomes simples também pega o uso sem o namespace escrito, pelo global using
        // implícito do SDK (que fica em obj/ e não é lido).
        Namespace("System.Net.Http", Categoria.Rede, "cliente HTTP (HttpClient e afins)",
            fonte: ["System.Net.Http", "HttpClient", "HttpClientHandler", "SocketsHttpHandler", "HttpRequestMessage", "HttpResponseMessage"]),
        Namespace("System.Net.Sockets", Categoria.Rede, "sockets TCP e UDP",
            fonte: ["System.Net.Sockets", "Socket", "TcpClient", "TcpListener", "UdpClient"]),
        Namespace("System.Net.WebSockets", Categoria.Rede, "conexões WebSocket",
            fonte: ["System.Net.WebSockets", "ClientWebSocket", "WebSocket"]),
        Namespace("System.Net.NetworkInformation", Categoria.Rede, "ping e leitura dos adaptadores de rede e seus endereços (SECURITY.md 6) (além da lista mínima)",
            fonte: ["System.Net.NetworkInformation", "NetworkInterface"]),
        Namespace("System.Net.Mail", Categoria.Rede, "envio de e-mail por SMTP (além da lista mínima)",
            fonte: ["System.Net.Mail", "SmtpClient"]),
        Namespace("System.Net.Quic", Categoria.Rede, "conexões QUIC (além da lista mínima)",
            fonte: ["System.Net.Quic", "QuicConnection", "QuicListener"]),
        Tipo("System.Net.WebClient", Categoria.Rede, "cliente HTTP e FTP"),
        Tipo("System.Net.WebRequest", Categoria.Rede, "requisição de rede"),
        Tipo("System.Net.HttpWebRequest", Categoria.Rede, "requisição HTTP"),
        Tipo("System.Net.FtpWebRequest", Categoria.Rede, "requisição FTP (além da lista mínima)"),
        Tipo("System.Net.HttpListener", Categoria.Rede, "servidor HTTP (além da lista mínima)"),
        Tipo("System.Net.Dns", Categoria.Rede, "consulta DNS pela rede (além da lista mínima)"),

        // ---- Ler outros aplicativos -----------------------------------------------------
        Funcao("EnumWindows", Categoria.LerOutrosAplicativos, "enumera as janelas de todos os aplicativos"),
        Funcao("EnumChildWindows", Categoria.LerOutrosAplicativos, "enumera janelas filhas, inclusive de outros aplicativos"),
        Funcao("EnumThreadWindows", Categoria.LerOutrosAplicativos, "enumera as janelas de uma thread, inclusive de outros aplicativos"),
        Funcao("EnumDesktopWindows", Categoria.LerOutrosAplicativos, "enumera as janelas de uma área de trabalho (além da lista mínima)"),
        Funcao("FindWindow", Categoria.LerOutrosAplicativos, "procura janelas de outros aplicativos por classe ou título"),
        Funcao("FindWindowEx", Categoria.LerOutrosAplicativos, "procura janelas de outros aplicativos por classe ou título"),
        Funcao("GetWindowText", Categoria.LerOutrosAplicativos, "lê o título de uma janela"),
        Funcao("GetWindowTextLength", Categoria.LerOutrosAplicativos, "mede o título de uma janela"),
        Funcao("InternalGetWindowText", Categoria.LerOutrosAplicativos, "lê o título de uma janela (além da lista mínima)"),
        Funcao("GetClassName", Categoria.LerOutrosAplicativos, "lê a classe de uma janela"),
        Funcao("OpenClipboard", Categoria.LerOutrosAplicativos, "abre a área de transferência"),
        Funcao("GetClipboardData", Categoria.LerOutrosAplicativos, "lê a área de transferência"),
        Funcao("OleGetClipboard", Categoria.LerOutrosAplicativos, "lê a área de transferência por OLE (além da lista mínima)"),
        Funcao("AddClipboardFormatListener", Categoria.LerOutrosAplicativos, "observa mudanças na área de transferência (além da lista mínima)"),
        Funcao("SetClipboardViewer", Categoria.LerOutrosAplicativos, "observa mudanças na área de transferência (além da lista mínima)"),
        Funcao("OpenProcess", Categoria.LerOutrosAplicativos, "abre outro processo"),
        Funcao("ReadProcessMemory", Categoria.LerOutrosAplicativos, "lê a memória de outro processo"),
        Funcao("EnumProcesses", Categoria.LerOutrosAplicativos, "lista os processos do sistema"),
        Funcao("K32EnumProcesses", Categoria.LerOutrosAplicativos, "EnumProcesses exportada pelo kernel32 (além da lista mínima)"),
        Funcao("CreateToolhelp32Snapshot", Categoria.LerOutrosAplicativos, "tira retrato dos processos, threads e módulos do sistema"),
        Funcao("Process32First", Categoria.LerOutrosAplicativos, "percorre o retrato de processos (além da lista mínima)"),
        Funcao("Process32Next", Categoria.LerOutrosAplicativos, "percorre o retrato de processos (além da lista mínima)"),
        Funcao("QueryFullProcessImageName", Categoria.LerOutrosAplicativos, "lê o caminho do executável de um processo"),
        Funcao("GetModuleFileNameEx", Categoria.LerOutrosAplicativos, "lê o caminho de um módulo de outro processo"),
        Funcao("K32GetModuleFileNameEx", Categoria.LerOutrosAplicativos, "GetModuleFileNameEx exportada pelo kernel32 (além da lista mínima)"),
        // As três abaixo marcadas com DEC-034 só valem no observador de tela cheia, pela permissão restrita de UsosRestritos
        // (SECURITY.md 3.1 e 8, item 1); em qualquer outro tipo ou arquivo, reprovam.
        Funcao("GetForegroundWindow", Categoria.LerOutrosAplicativos, "identifica a janela de outro aplicativo em primeiro plano; só no observador de tela cheia (DEC-013, DEC-034)"),
        Funcao("GetWindowThreadProcessId", Categoria.LerOutrosAplicativos, "identifica a thread e o processo donos de uma janela, inclusive de outro aplicativo; só no observador de tela cheia, sem o processo (DEC-034) (além da lista mínima)"),
        Funcao("WindowFromPoint", Categoria.LerOutrosAplicativos, "identifica a janela de outro aplicativo sob um ponto"),
        Funcao("SetWinEventHook", Categoria.LerOutrosAplicativos, "observa eventos de janelas de outros aplicativos; só no observador de tela cheia, restrito aos eventos e filtros da DEC-013 (DEC-034)"),
        // DEC-037, item 12: identidade, hierarquia e processo de janelas de outros aplicativos, por outros caminhos.
        Funcao("RealGetWindowClass", Categoria.LerOutrosAplicativos, "lê a classe de uma janela (além da lista mínima)"),
        Funcao("GetWindowModuleFileName", Categoria.LerOutrosAplicativos, "lê o módulo dono de uma janela (além da lista mínima)"),
        Funcao("GetWindowInfo", Categoria.LerOutrosAplicativos, "lê estilos, classe e geometria de uma janela (além da lista mínima)"),
        Funcao("GetGUIThreadInfo", Categoria.LerOutrosAplicativos, "lê o foco, o caret e as janelas ativas de uma thread de outro aplicativo (além da lista mínima)"),
        Funcao("GetTitleBarInfo", Categoria.LerOutrosAplicativos, "lê a barra de título de uma janela (além da lista mínima)"),
        Funcao("GetWindowPlacement", Categoria.LerOutrosAplicativos, "lê o estado e a posição normal de uma janela (além da lista mínima)"),
        Funcao("DwmGetWindowAttribute", Categoria.LerOutrosAplicativos, "lê atributos do compositor de uma janela, inclusive de outro aplicativo (além da lista mínima)"),
        Funcao("OpenThread", Categoria.LerOutrosAplicativos, "abre uma thread, inclusive de outro processo (além da lista mínima)"),
        Funcao("GetProcessIdOfThread", Categoria.LerOutrosAplicativos, "identifica o processo de uma thread (além da lista mínima)"),
        Funcao("WTSEnumerateProcesses", Categoria.LerOutrosAplicativos, "lista os processos da sessão (além da lista mínima)"),
        Funcao("NtQuerySystemInformation", Categoria.LerOutrosAplicativos, "lê processos e threads do sistema (além da lista mínima)"),
        Funcao("ChildWindowFromPoint", Categoria.LerOutrosAplicativos, "identifica a janela filha sob um ponto (além da lista mínima)"),
        Funcao("ChildWindowFromPointEx", Categoria.LerOutrosAplicativos, "identifica a janela filha sob um ponto (além da lista mínima)"),
        Funcao("RealChildWindowFromPoint", Categoria.LerOutrosAplicativos, "identifica a janela filha sob um ponto (além da lista mínima)"),
        Funcao("WindowFromPhysicalPoint", Categoria.LerOutrosAplicativos, "identifica a janela de outro aplicativo sob um ponto físico (além da lista mínima)"),
        Funcao("GetAncestor", Categoria.LerOutrosAplicativos, "navega até a janela mãe ou dona, inclusive de outro aplicativo (além da lista mínima)"),
        Funcao("GetWindow", Categoria.LerOutrosAplicativos, "navega pela ordem Z e pelas donas até janelas de outros aplicativos (além da lista mínima)"),
        Funcao("GetTopWindow", Categoria.LerOutrosAplicativos, "dá a janela do topo da ordem Z, inclusive de outro aplicativo (além da lista mínima)"),
        Funcao("GetLastActivePopup", Categoria.LerOutrosAplicativos, "dá o último popup ativo de outro aplicativo (além da lista mínima)"),
        Funcao("GetShellWindow", Categoria.LerOutrosAplicativos, "dá a janela do shell (além da lista mínima)"),
        Funcao("AccessibleObjectFromWindow", Categoria.LerOutrosAplicativos, "lê a interface de outro aplicativo por MSAA (além da lista mínima)"),
        Funcao("AccessibleObjectFromPoint", Categoria.LerOutrosAplicativos, "lê a interface de outro aplicativo por MSAA (além da lista mínima)"),
        Tipo("System.Windows.Clipboard", Categoria.LerOutrosAplicativos, "lê a área de transferência (WPF)", fonte: ["Clipboard"]),
        Tipo("System.Windows.Forms.Clipboard", Categoria.LerOutrosAplicativos, "lê a área de transferência (Windows Forms)", fonte: ["Clipboard"]),
        Tipo("System.Windows.Automation.AutomationElement", Categoria.LerOutrosAplicativos, "UI Automation do lado cliente: lê a interface de outros processos"),
        // Só nos binários: na fonte, "Automation" é também parte do namespace
        // System.Windows.Automation, que o WPF usa para a própria acessibilidade.
        Tipo("System.Windows.Automation.Automation", Categoria.LerOutrosAplicativos, "UI Automation do lado cliente: observa foco e eventos de outros processos (além da lista mínima)",
            fonte: []),
        Membro("System.Diagnostics.Process", "GetProcesses", Categoria.LerOutrosAplicativos, "lista os processos do sistema",
            fonte: ["GetProcesses"]),
        Membro("System.Diagnostics.Process", "GetProcessesByName", Categoria.LerOutrosAplicativos, "procura processos pelo nome",
            fonte: ["GetProcessesByName"]),
        Membro("System.Diagnostics.Process", "GetProcessById", Categoria.LerOutrosAplicativos, "abre outro processo pelo identificador",
            fonte: ["GetProcessById"]),

        // ---- Persistência escondida -----------------------------------------------------
        // A chave Run de Q-04 (iniciar com o Windows, opcional e ligada pelo usuário): RegSetValueExW e RegDeleteValueW
        // são usos restritos do adaptador do início (UsosRestritos; DEC-038, item 11); no resto do produto, reprovam.
        Funcao("RegSetValueEx", Categoria.PersistenciaEscondida, "grava valor no registro, como a chave Run"),
        Funcao("RegSetValue", Categoria.PersistenciaEscondida, "grava valor no registro (API antiga) (além da lista mínima)"),
        Funcao("RegSetKeyValue", Categoria.PersistenciaEscondida, "grava valor no registro (além da lista mínima)"),
        Funcao("RegCreateKeyEx", Categoria.PersistenciaEscondida, "cria chave no registro"),
        Funcao("RegCreateKey", Categoria.PersistenciaEscondida, "cria chave no registro (API antiga) (além da lista mínima)"),
        // DEC-038, item 11: apagar ou gravar no registro por outros nomes, e a pasta Inicializar.
        Funcao("RegDeleteValue", Categoria.PersistenciaEscondida, "apaga valor do registro, como o da chave Run (além da lista mínima)"),
        Funcao("RegDeleteKey", Categoria.PersistenciaEscondida, "apaga chave do registro (além da lista mínima)"),
        Funcao("RegDeleteKeyEx", Categoria.PersistenciaEscondida, "apaga chave do registro (além da lista mínima)"),
        Funcao("RegDeleteKeyValue", Categoria.PersistenciaEscondida, "apaga valor do registro (além da lista mínima)"),
        Funcao("RegDeleteTree", Categoria.PersistenciaEscondida, "apaga uma árvore do registro (além da lista mínima)"),
        Funcao("SHSetValue", Categoria.PersistenciaEscondida, "grava valor no registro pelo shell (além da lista mínima)"),
        Funcao("SHRegSetUSValue", Categoria.PersistenciaEscondida, "grava valor no registro pelo shell (além da lista mínima)"),
        Membro("System.Environment+SpecialFolder", "Startup", Categoria.PersistenciaEscondida, "a pasta Inicializar, onde um atalho abre o programa no logon (além da lista mínima)",
            fonte: ["SpecialFolder.Startup", "SpecialFolder.CommonStartup"]),
        Funcao("CreateService", Categoria.PersistenciaEscondida, "instala um serviço do Windows"),
        Funcao("OpenSCManager", Categoria.PersistenciaEscondida, "abre o gerenciador de serviços para instalar ou alterar serviços"),
        Tipo("Microsoft.Win32.Registry", Categoria.PersistenciaEscondida, "acesso ao registro, inclusive a chave Run"),
        Tipo("Microsoft.Win32.RegistryKey", Categoria.PersistenciaEscondida, "acesso ao registro, inclusive a chave Run"),

        // ---- Código dinâmico ------------------------------------------------------------
        Funcao("LoadLibrary", Categoria.CodigoDinamico, "carrega DLL por caminho em tempo de execução"),
        Funcao("LoadLibraryEx", Categoria.CodigoDinamico, "carrega DLL por caminho em tempo de execução"),
        Funcao("LoadPackagedLibrary", Categoria.CodigoDinamico, "carrega DLL em tempo de execução (além da lista mínima)"),
        Funcao("LdrLoadDll", Categoria.CodigoDinamico, "carrega DLL pelo ntdll, por baixo de LoadLibrary (além da lista mínima)"),
        Funcao("GetProcAddress", Categoria.CodigoDinamico, "obtém função por nome em tempo de execução, escondendo a chamada do portão"),
        Funcao("LdrGetProcedureAddress", Categoria.CodigoDinamico, "obtém função pelo ntdll, por baixo de GetProcAddress (além da lista mínima)"),
        // Qualificados na fonte: "Load", "LoadFrom" e "LoadFile" sozinhos são nomes comuns.
        Membro("System.Reflection.Assembly", "Load", Categoria.CodigoDinamico, "carrega assembly em tempo de execução",
            fonte: ["Assembly.Load"]),
        Membro("System.Reflection.Assembly", "LoadFrom", Categoria.CodigoDinamico, "carrega assembly de um caminho",
            fonte: ["Assembly.LoadFrom"]),
        Membro("System.Reflection.Assembly", "LoadFile", Categoria.CodigoDinamico, "carrega assembly de um caminho",
            fonte: ["Assembly.LoadFile"]),
        Membro("System.Reflection.Assembly", "UnsafeLoadFrom", Categoria.CodigoDinamico, "carrega assembly de um caminho",
            fonte: ["UnsafeLoadFrom"]),
        // Só nos binários: o uso comum é AppDomain.CurrentDomain.Load, sem nome distintivo.
        Membro("System.AppDomain", "Load", Categoria.CodigoDinamico, "carrega assembly em tempo de execução, como Assembly.Load (além da lista mínima)",
            fonte: []),
        Membro("System.Runtime.Loader.AssemblyLoadContext", "LoadFromAssemblyPath", Categoria.CodigoDinamico, "carrega assembly de um caminho",
            fonte: ["LoadFromAssemblyPath"]),
        Membro("System.Runtime.Loader.AssemblyLoadContext", "LoadFromStream", Categoria.CodigoDinamico, "carrega assembly de bytes arbitrários",
            fonte: ["LoadFromStream"]),
        Membro("System.Runtime.Loader.AssemblyLoadContext", "LoadFromNativeImagePath", Categoria.CodigoDinamico, "carrega assembly de um caminho (além da lista mínima)",
            fonte: ["LoadFromNativeImagePath"]),
        Membro("System.Runtime.Loader.AssemblyLoadContext", "LoadUnmanagedDllFromPath", Categoria.CodigoDinamico, "carrega DLL nativa de um caminho (além da lista mínima)",
            fonte: ["LoadUnmanagedDllFromPath"]),
        Tipo("System.Runtime.InteropServices.NativeLibrary", Categoria.CodigoDinamico, "carrega DLL nativa e obtém funções em tempo de execução"),
        Membro("System.Runtime.InteropServices.Marshal", "GetDelegateForFunctionPointer", Categoria.CodigoDinamico, "chama ponteiro de função obtido em tempo de execução",
            fonte: ["GetDelegateForFunctionPointer"]),
        Namespace("System.Reflection.Emit", Categoria.CodigoDinamico, "gera código em tempo de execução",
            fonte: ["System.Reflection.Emit", "DynamicMethod", "ILGenerator", "AssemblyBuilder", "PersistedAssemblyBuilder"]),

        // ---- Alterar configuração global --------------------------------------------------
        // A chave estável do monitor (DEC-030) só LÊ a configuração de vídeo (GetDisplayConfigBufferSizes, QueryDisplayConfig e
        // DisplayConfigGetDeviceInfo, permitidas); estas a mudam para o sistema todo (revisão de segurança do bloco P6-P9).
        Funcao("SetDisplayConfig", Categoria.ConfiguracaoGlobal, "muda a topologia, a resolução, a orientação ou o modo de vídeo do sistema todo"),
        Funcao("DisplayConfigSetDeviceInfo", Categoria.ConfiguracaoGlobal, "muda propriedades de um alvo ou de uma fonte de vídeo (escala, HDR) para o sistema todo"),
        Funcao("ChangeDisplaySettings", Categoria.ConfiguracaoGlobal, "muda o modo de vídeo do monitor principal para o sistema todo"),
        Funcao("ChangeDisplaySettingsEx", Categoria.ConfiguracaoGlobal, "muda o modo de vídeo ou a posição de um monitor para o sistema todo"),
        // ---- Fase 9 (DEC-040, item 6): famílias que a auditoria achou fora da lista ------------------------
        // Input global sem hook: a posição do cursor, a ociosidade e o estado de input de outra thread. O Buzzy só lê o
        // cursor nas mensagens entregues às próprias janelas (SECURITY.md 3.1).
        Funcao("GetCursorPos", Categoria.InputGlobal, "lê a posição do cursor do sistema, fora das mensagens das próprias janelas"),
        Funcao("GetPhysicalCursorPos", Categoria.InputGlobal, "lê a posição física do cursor do sistema"),
        Funcao("GetCursorInfo", Categoria.InputGlobal, "lê o cursor do sistema (posição e forma)"),
        Funcao("GetLastInputInfo", Categoria.InputGlobal, "lê a ociosidade do usuário no sistema todo"),
        Funcao("GetMouseMovePointsEx", Categoria.InputGlobal, "lê o histórico de movimento do mouse do sistema"),
        Funcao("AttachThreadInput", Categoria.InputGlobal, "compartilha o estado de input com a thread de outro aplicativo"),

        // Ler outros aplicativos: shell hook, acessibilidade (MSAA e UIA por COM), metadados do clipboard, identidade de
        // processos, do usuário e da máquina, e observação de arquivos do usuário (SECURITY.md 6).
        Funcao("RegisterShellHookWindow", Categoria.LerOutrosAplicativos, "recebe o HWND de toda janela criada ou ativada no sistema"),
        Funcao("AccessibleObjectFromEvent", Categoria.LerOutrosAplicativos, "lê nome e valor do objeto de um evento de outro aplicativo"),
        Funcao("AccessibleChildren", Categoria.LerOutrosAplicativos, "percorre a árvore de acessibilidade de outro aplicativo"),
        Funcao("WindowFromAccessibleObject", Categoria.LerOutrosAplicativos, "obtém a janela de um objeto de acessibilidade de outro aplicativo"),
        MetodoCom("ElementFromHandle", Categoria.LerOutrosAplicativos, "UIA cliente por COM: o elemento de uma janela de outro aplicativo"),
        MetodoCom("ElementFromPoint", Categoria.LerOutrosAplicativos, "UIA cliente por COM: o elemento sob um ponto da tela"),
        MetodoCom("GetFocusedElement", Categoria.LerOutrosAplicativos, "UIA cliente por COM: o elemento com o foco, de qualquer aplicativo"),
        Funcao("GetClipboardSequenceNumber", Categoria.LerOutrosAplicativos, "observa quando o clipboard muda"),
        Funcao("IsClipboardFormatAvailable", Categoria.LerOutrosAplicativos, "lê que tipo de conteúdo está no clipboard"),
        Funcao("EnumClipboardFormats", Categoria.LerOutrosAplicativos, "lista os formatos do conteúdo do clipboard"),
        Funcao("GetClipboardOwner", Categoria.LerOutrosAplicativos, "obtém a janela de quem pôs o conteúdo no clipboard"),
        Funcao("GetOpenClipboardWindow", Categoria.LerOutrosAplicativos, "obtém a janela que está usando o clipboard"),
        Funcao("GetProcessImageFileName", Categoria.LerOutrosAplicativos, "lê o executável de outro processo"),
        Funcao("GetModuleBaseName", Categoria.LerOutrosAplicativos, "lê o nome de um módulo de outro processo"),
        Funcao("NtQueryInformationProcess", Categoria.LerOutrosAplicativos, "lê informações de outro processo (linha de comando, pai)"),
        Funcao("WTSQuerySessionInformation", Categoria.LerOutrosAplicativos, "lê o usuário, o cliente e o estado da sessão"),
        Funcao("GetUserName", Categoria.LerOutrosAplicativos, "lê o nome do usuário (SECURITY.md 6: identificador)"),
        Funcao("GetUserNameEx", Categoria.LerOutrosAplicativos, "lê o nome do usuário em outros formatos (SECURITY.md 6)"),
        Funcao("GetComputerName", Categoria.LerOutrosAplicativos, "lê o nome da máquina (SECURITY.md 6: identificador)"),
        Funcao("GetComputerNameEx", Categoria.LerOutrosAplicativos, "lê o nome da máquina e do domínio (SECURITY.md 6)"),
        Funcao("ReadDirectoryChanges", Categoria.LerOutrosAplicativos, "observa mudanças numa pasta (arquivos do usuário)"),
        Funcao("ReadDirectoryChangesEx", Categoria.LerOutrosAplicativos, "observa mudanças numa pasta, com detalhes (arquivos do usuário)"),
        Funcao("SHChangeNotifyRegister", Categoria.LerOutrosAplicativos, "observa mudanças do shell nos arquivos do usuário"),
        Tipo("System.IO.FileSystemWatcher", Categoria.LerOutrosAplicativos, "observa mudanças em arquivos e pastas do usuário"),
        Tipo("System.Windows.DataObject", Categoria.LerOutrosAplicativos, "recebe conteúdo de outro aplicativo por arrastar e soltar"),
        Tipo("System.Windows.DragDrop", Categoria.LerOutrosAplicativos, "recebe conteúdo de outro aplicativo por arrastar e soltar"),
        Membro("System.Environment", "UserName", Categoria.LerOutrosAplicativos, "lê o nome do usuário (SECURITY.md 6)", fonte: ["Environment.UserName"]),
        Membro("System.Environment", "MachineName", Categoria.LerOutrosAplicativos, "lê o nome da máquina (SECURITY.md 6)", fonte: ["Environment.MachineName"]),
        Membro("System.Environment", "UserDomainName", Categoria.LerOutrosAplicativos, "lê o domínio do usuário (SECURITY.md 6)", fonte: ["Environment.UserDomainName"]),

        // Processos e código dinâmico por COM tardio, reflexão por texto, VB e WMI, e as funções de processo do CRT e do ntdll.
        Membro("System.Type", "GetTypeFromProgID", Categoria.CodigoDinamico, "COM tardio por nome (WScript.Shell, Shell.Application, Schedule.Service)", fonte: ["GetTypeFromProgID"]),
        Membro("System.Type", "GetTypeFromCLSID", Categoria.CodigoDinamico, "COM tardio por identificador de classe", fonte: ["GetTypeFromCLSID"]),
        Membro("System.Type", "InvokeMember", Categoria.CodigoDinamico, "chama um membro pelo nome em texto, fora do que o portão rastreia", fonte: ["InvokeMember"]),
        Membro("System.Runtime.InteropServices.Marshal", "BindToMoniker", Categoria.CodigoDinamico, "obtém um objeto COM por moniker", fonte: ["BindToMoniker"]),
        Membro("System.Runtime.InteropServices.Marshal", "GetObjectForIUnknown", Categoria.CodigoDinamico, "embrulha um ponteiro COM arbitrário", fonte: ["GetObjectForIUnknown"]),
        Funcao("CoCreateInstance", Categoria.CodigoDinamico, "cria um objeto COM qualquer por P/Invoke"),
        Funcao("CoCreateInstanceEx", Categoria.CodigoDinamico, "cria um objeto COM qualquer, inclusive remoto"),
        Funcao("CoGetObject", Categoria.CodigoDinamico, "obtém um objeto COM por nome de exibição"),
        Namespace("System.Management", Categoria.Processos, "WMI: cria processos (Win32_Process.Create) e grava no registro (StdRegProv)",
            fonte: ["System.Management", "ManagementObject", "ManagementClass", "ManagementObjectSearcher"]),
        Namespace("Microsoft.VisualBasic", Categoria.Processos, "Interaction.Shell e CreateObject executam processos e criam COM tardio",
            fonte: ["Microsoft.VisualBasic", "Interaction.Shell", "Interaction.CreateObject"]),
        Funcao("_wsystem", Categoria.Processos, "o system do CRT: executa um comando"),
        Funcao("_popen", Categoria.Processos, "o popen do CRT: executa um comando com um pipe"),
        Funcao("_wpopen", Categoria.Processos, "o popen do CRT, em UTF-16"),
        Funcao("_wspawnv", Categoria.Processos, "o spawn do CRT: cria um processo"),
        Funcao("_wspawnvp", Categoria.Processos, "o spawn do CRT, pelo PATH"),
        Funcao("_wexecv", Categoria.Processos, "o exec do CRT: troca o processo"),
        Funcao("_wexecvp", Categoria.Processos, "o exec do CRT, pelo PATH"),
        Funcao("CreateProcessInternal", Categoria.Processos, "a criação de processo interna do kernelbase"),
        Funcao("NtCreateUserProcess", Categoria.Processos, "a criação de processo do ntdll"),
        Funcao("RtlCreateUserProcess", Categoria.Processos, "a criação de processo do ntdll"),

        // Alterar configuração global além do vídeo: entrada, aparência, energia, sessão e relógio.
        Funcao("SystemParametersInfo", Categoria.ConfiguracaoGlobal, "com SPI_SET*, muda mouse, papel de parede, área útil e outros parâmetros do sistema todo"),
        Funcao("SetSysColors", Categoria.ConfiguracaoGlobal, "muda as cores do sistema todo"),
        Funcao("SetDoubleClickTime", Categoria.ConfiguracaoGlobal, "muda o tempo do clique duplo do sistema todo (o Buzzy só o lê)"),
        Funcao("SwapMouseButton", Categoria.ConfiguracaoGlobal, "troca os botões do mouse do sistema todo"),
        Funcao("SetSystemCursor", Categoria.ConfiguracaoGlobal, "troca o cursor do sistema todo"),
        Funcao("timeBeginPeriod", Categoria.ConfiguracaoGlobal, "muda a resolução global do timer (Q-08 mede que ela não muda)"),
        Funcao("NtSetTimerResolution", Categoria.ConfiguracaoGlobal, "muda a resolução global do timer pelo ntdll"),
        Funcao("SetThreadExecutionState", Categoria.ConfiguracaoGlobal, "impede o sistema de dormir ou desligar a tela"),
        Funcao("PowerSetActiveScheme", Categoria.ConfiguracaoGlobal, "troca o plano de energia do sistema"),
        Funcao("SetSuspendState", Categoria.ConfiguracaoGlobal, "suspende ou hiberna o sistema"),
        Funcao("ExitWindowsEx", Categoria.ConfiguracaoGlobal, "encerra a sessão ou desliga o sistema"),
        Funcao("InitiateSystemShutdown", Categoria.ConfiguracaoGlobal, "desliga ou reinicia o sistema"),
        Funcao("InitiateSystemShutdownEx", Categoria.ConfiguracaoGlobal, "desliga ou reinicia o sistema, com motivo"),
        Funcao("LockWorkStation", Categoria.ConfiguracaoGlobal, "bloqueia a sessão do usuário"),
        Funcao("SetSystemTime", Categoria.ConfiguracaoGlobal, "muda o relógio do sistema"),
        Funcao("SetLocalTime", Categoria.ConfiguracaoGlobal, "muda o relógio local do sistema"),

        // Rede por módulos e tipos que o WPF e o .NET expõem fora de System.Net.Http e dos sockets.
        Modulo("urlmon", Categoria.Rede, "baixa por URL (URLDownloadToFile e afins)"),
        Modulo("iphlpapi", Categoria.Rede, "tabelas de rede e conexões do sistema"),
        Modulo("dnsapi", Categoria.Rede, "consulta DNS"),
        Modulo("netapi32", Categoria.Rede, "funções de rede do Windows (compartilhamentos, domínio)"),
        Modulo("mpr", Categoria.Rede, "conecta unidades e compartilhamentos de rede (WNetAddConnection)"),
        Tipo("System.Windows.Controls.WebBrowser", Categoria.Rede, "navega na web dentro do aplicativo"),
        Tipo("System.Windows.Navigation.NavigationWindow", Categoria.Rede, "navega para URIs, inclusive da web"),
        Tipo("System.IO.Pipes.NamedPipeClientStream", Categoria.Rede, "conecta a um pipe nomeado, inclusive de outra máquina"),
        Namespace("System.Net.Security", Categoria.Rede, "TLS e autenticação de rede", fonte: ["System.Net.Security", "SslStream", "NegotiateStream"]),
        Namespace("Windows.Web", Categoria.Rede, "HTTP do Windows Runtime", fonte: ["Windows.Web"]),
        Namespace("Windows.Networking", Categoria.Rede, "sockets e conectividade do Windows Runtime", fonte: ["Windows.Networking"]),

        // Persistência e registro: as outras formas de gravar, copiar, carregar ou apagar chaves e valores.
        Funcao("RegSaveKey", Categoria.PersistenciaEscondida, "salva uma chave do registro num arquivo"),
        Funcao("RegSaveKeyEx", Categoria.PersistenciaEscondida, "salva uma chave do registro num arquivo"),
        Funcao("RegRestoreKey", Categoria.PersistenciaEscondida, "sobrescreve uma chave do registro a partir de um arquivo"),
        Funcao("RegLoadKey", Categoria.PersistenciaEscondida, "carrega uma colmeia do registro"),
        Funcao("RegReplaceKey", Categoria.PersistenciaEscondida, "troca uma colmeia do registro"),
        Funcao("RegCopyTree", Categoria.PersistenciaEscondida, "copia uma árvore do registro"),
        Funcao("RegRenameKey", Categoria.PersistenciaEscondida, "renomeia uma chave do registro"),
        Funcao("RegSetKeySecurity", Categoria.PersistenciaEscondida, "muda a segurança de uma chave do registro"),
        Funcao("NtSetValueKey", Categoria.PersistenciaEscondida, "grava um valor do registro pelo ntdll"),
        Funcao("ZwSetValueKey", Categoria.PersistenciaEscondida, "grava um valor do registro pelo ntdll"),
        Funcao("SHRegSetValue", Categoria.PersistenciaEscondida, "grava um valor do registro pelo shlwapi"),
        Funcao("SHRegWriteUSValue", Categoria.PersistenciaEscondida, "grava um valor do registro pelo shlwapi"),
        Funcao("SHDeleteKey", Categoria.PersistenciaEscondida, "apaga uma chave do registro pelo shlwapi"),
        Funcao("SHDeleteValue", Categoria.PersistenciaEscondida, "apaga um valor do registro pelo shlwapi"),
        Funcao("SHDeleteEmptyKey", Categoria.PersistenciaEscondida, "apaga uma chave do registro pelo shlwapi"),
        Funcao("SHCopyKey", Categoria.PersistenciaEscondida, "copia uma chave do registro pelo shlwapi"),
        Funcao("SHRegDeleteUSValue", Categoria.PersistenciaEscondida, "apaga um valor do registro pelo shlwapi"),
        Funcao("ChangeServiceConfig", Categoria.PersistenciaEscondida, "muda a configuração de um serviço (início automático)"),
    ];

    private static readonly Dictionary<string, Regra> FuncoesPorVariante = IndexarFuncoes();
    private static readonly Dictionary<string, Regra> ModulosPorNome = Indexar(TipoDeRegra.ModuloNativo, r => r.Alvo.ToLowerInvariant());
    private static readonly Dictionary<string, Regra> TiposPorNome = Indexar(TipoDeRegra.TipoGerenciado, r => r.Alvo);
    private static readonly Dictionary<string, Regra> MembrosPorNome = Indexar(TipoDeRegra.MembroGerenciado, r => $"{r.Alvo}::{r.Membro}");
    private static readonly Dictionary<string, Regra> MetodosComPorNome = Indexar(TipoDeRegra.MetodoCom, r => r.Alvo);
    private static readonly Regra[] Namespaces = [.. Regras.Where(r => r.Tipo == TipoDeRegra.NamespaceGerenciado)];

    /// <summary>
    /// Regra que proíbe uma função nativa: primeiro pelo nome da função (ignorando maiúsculas e
    /// os sufixos A e W), depois pelo módulo (ignorando maiúsculas, caminho e extensão .dll).
    /// </summary>
    public static Regra? ProcurarNativa(string modulo, string funcao)
    {
        ArgumentNullException.ThrowIfNull(modulo);
        ArgumentNullException.ThrowIfNull(funcao);
        if (FuncoesPorVariante.TryGetValue(funcao.Trim().ToLowerInvariant(), out Regra? porFuncao)) return porFuncao;
        return ModulosPorNome.GetValueOrDefault(NormalizarModulo(modulo));
    }

    /// <summary>Regra que proíbe um tipo gerenciado de nível superior, pelo nome ou pelo namespace.</summary>
    public static Regra? ProcurarTipo(string nomeDoNamespace, string nome)
    {
        ArgumentNullException.ThrowIfNull(nomeDoNamespace);
        ArgumentNullException.ThrowIfNull(nome);
        if (TiposPorNome.TryGetValue(Juntar(nomeDoNamespace, nome), out Regra? porTipo)) return porTipo;
        foreach (Regra r in Namespaces)
        {
            if (string.Equals(nomeDoNamespace, r.Alvo, StringComparison.Ordinal)
                || nomeDoNamespace.StartsWith(r.Alvo + ".", StringComparison.Ordinal))
                return r;
        }
        return null;
    }

    /// <summary>Regra que proíbe um membro, dado o nome completo do tipo que o declara.</summary>
    public static Regra? ProcurarMembro(string tipo, string membro)
        => MembrosPorNome.GetValueOrDefault($"{tipo}::{membro}");

    /// <summary>Regra que proíbe um método de interface COM pelo nome.</summary>
    public static Regra? ProcurarMetodoCom(string metodo) => MetodosComPorNome.GetValueOrDefault(metodo);

    /// <summary>
    /// Nome de módulo comparável: sem caminho, sem ponto final, sem extensão .dll, em minúsculas.
    /// "C:\Windows\System32\WS2_32.DLL", "ws2_32.dll" e "ws2_32" viram "ws2_32".
    /// </summary>
    public static string NormalizarModulo(string modulo)
    {
        ArgumentNullException.ThrowIfNull(modulo);
        string nome = modulo.Trim();
        int barra = nome.LastIndexOfAny(['\\', '/']);
        if (barra >= 0) nome = nome[(barra + 1)..];
        nome = nome.TrimEnd('.');
        if (nome.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) nome = nome[..^4];
        return nome.ToLowerInvariant();
    }

    public static string Juntar(string nomeDoNamespace, string nome) => nomeDoNamespace.Length == 0 ? nome : $"{nomeDoNamespace}.{nome}";

    // O nome exato e os sufixos A e W de cada função. Comparar variantes, em vez de cortar o
    // último caractere, evita falso positivo: "PrintWindow" termina em W e não é variante de nada.
    private static Dictionary<string, Regra> IndexarFuncoes()
    {
        var indice = new Dictionary<string, Regra>(StringComparer.Ordinal);
        foreach (Regra r in Regras.Where(r => r.Tipo == TipoDeRegra.FuncaoNativa))
        {
            string nome = r.Alvo.ToLowerInvariant();
            string[] variantes = [nome, nome + "a", nome + "w"];
            foreach (string variante in variantes)
            {
                if (!indice.TryAdd(variante, r) && !ReferenceEquals(indice[variante], r))
                    throw new InvalidOperationException($"Lista proibida: a variante \"{variante}\" aparece em duas regras.");
            }
        }
        return indice;
    }

    private static Dictionary<string, Regra> Indexar(TipoDeRegra tipo, Func<Regra, string> chave)
    {
        var indice = new Dictionary<string, Regra>(StringComparer.Ordinal);
        foreach (Regra r in Regras.Where(r => r.Tipo == tipo))
        {
            if (!indice.TryAdd(chave(r), r))
                throw new InvalidOperationException($"Lista proibida: \"{chave(r)}\" aparece em duas regras.");
        }
        return indice;
    }

    private static Regra Funcao(string nome, Categoria categoria, string motivo)
        => new(TipoDeRegra.FuncaoNativa, nome, null, categoria, motivo, [nome, nome + "A", nome + "W"]);

    private static Regra Modulo(string nome, Categoria categoria, string motivo)
        => new(TipoDeRegra.ModuloNativo, nome, null, categoria, motivo, [nome]);

    private static Regra Tipo(string nomeCompleto, Categoria categoria, string motivo, IReadOnlyList<string>? fonte = null)
        => new(TipoDeRegra.TipoGerenciado, nomeCompleto, null, categoria, motivo, fonte ?? [nomeCompleto[(nomeCompleto.LastIndexOf('.') + 1)..]]);

    private static Regra Namespace(string nome, Categoria categoria, string motivo, IReadOnlyList<string> fonte)
        => new(TipoDeRegra.NamespaceGerenciado, nome, null, categoria, motivo, fonte);

    private static Regra Membro(string tipo, string membro, Categoria categoria, string motivo, IReadOnlyList<string> fonte)
        => new(TipoDeRegra.MembroGerenciado, tipo, membro, categoria, motivo, fonte);

    private static Regra MetodoCom(string nome, Categoria categoria, string motivo)
        => new(TipoDeRegra.MetodoCom, nome, null, categoria, motivo, [nome]);
}
