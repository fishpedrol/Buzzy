// WebClient, WebRequest e HttpWebRequest são obsoletos; a amostra precisa deles assim mesmo.
#pragma warning disable SYSLIB0014

using System.Runtime.InteropServices;

namespace Buzzy.PortaoApis.Testes.Amostras;

// Amostras (fixtures) que o portão PRECISA acusar. NUNCA são executadas: nenhum teste chama
// estes métodos. O portão só lê os metadados deste assembly compilado. As assinaturas nativas
// usam nint de propósito, sem marshalling de texto, porque nada aqui chega a ser chamado.

/// <summary>Um P/Invoke para cada função nativa da lista mínima, com grafias variadas de módulo e nome.</summary>
internal static class PInvokesProibidos
{
    // Input global
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
    internal static extern nint InstalarHook(int tipo, nint procedimento, nint modulo, uint thread);

    [DllImport("user32.dll")]
    internal static extern int RegisterRawInputDevices(nint dispositivos, uint quantidade, uint tamanho);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int tecla);

    [DllImport("user32.dll")]
    internal static extern int GetKeyboardState(nint estado);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int tecla);

    [DllImport("user32.dll")]
    internal static extern int RegisterHotKey(nint janela, int id, uint modificadores, uint tecla);

    // Injetar input: nome do método diferente do ponto de entrada, módulo em maiúsculas e sem extensão.
    [DllImport("user32.dll", EntryPoint = "SendInput")]
    internal static extern uint EnviarEntrada(uint quantidade, nint entradas, int tamanho);

    [DllImport("USER32.DLL")]
    internal static extern void mouse_event(uint flags, uint dx, uint dy, uint dados, nint extra);

    [DllImport("user32")]
    internal static extern void keybd_event(byte tecla, byte varredura, uint flags, nint extra);

    // Captura de tela
    [DllImport("gdi32.dll")]
    internal static extern int BitBlt(nint destino, int x, int y, int largura, int altura, nint origem, int x1, int y1, uint operacao);

    [DllImport("gdi32.dll")]
    internal static extern int StretchBlt(nint destino, int x, int y, int largura, int altura, nint origem, int x1, int y1, int l1, int a1, uint operacao);

    [DllImport("user32.dll")]
    internal static extern int PrintWindow(nint janela, nint dc, uint flags);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW")]
    internal static extern nint CriarDc(nint driver, nint dispositivo, nint porta, nint modo);

    // Processos
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW")]
    internal static extern int CriarProcesso(nint aplicativo, nint linha, nint a1, nint a2, int herdar, uint flags, nint ambiente, nint pasta, nint inicio, nint info);

    [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW")]
    internal static extern int CriarProcessoComoUsuario(nint token, nint aplicativo, nint linha, nint a1, nint a2, int herdar, uint flags, nint ambiente, nint pasta, nint inicio, nint info);

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteW")]
    internal static extern nint ExecutarNoShell(nint janela, nint operacao, nint arquivo, nint parametros, nint pasta, int mostrar);

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW")]
    internal static extern int ExecutarNoShellEx(nint informacao);

    [DllImport("kernel32.dll")]
    internal static extern uint WinExec(nint linha, uint mostrar);

    // Rede: módulos inteiros, com qualquer função, e URLDownloadToFile.
    [DllImport("ws2_32.dll")]
    internal static extern int connect(nint soquete, nint endereco, int tamanho);

    [DllImport("WSOCK32.DLL", EntryPoint = "send")]
    internal static extern int Enviar(nint soquete, nint dados, int tamanho, int flags);

    [DllImport("winhttp.dll")]
    internal static extern nint WinHttpOpen(nint agente, uint acesso, nint proxy, nint excecoes, uint flags);

    [DllImport("wininet.dll", EntryPoint = "InternetOpenW")]
    internal static extern nint AbrirInternet(nint agente, uint acesso, nint proxy, nint excecoes, uint flags);

    [DllImport("urlmon.dll", EntryPoint = "URLDownloadToFileW")]
    internal static extern int Baixar(nint chamador, nint url, nint arquivo, uint reservado, nint retorno);

    // Ler outros aplicativos
    [DllImport("user32.dll")]
    internal static extern int EnumWindows(nint retorno, nint parametro);

    [DllImport("user32.dll")]
    internal static extern int EnumChildWindows(nint pai, nint retorno, nint parametro);

    [DllImport("user32.dll")]
    internal static extern int EnumThreadWindows(uint thread, nint retorno, nint parametro);

    [DllImport("user32.dll", EntryPoint = "FindWindowW")]
    internal static extern nint ProcurarJanela(nint classe, nint titulo);

    [DllImport("user32.dll", EntryPoint = "FindWindowExW")]
    internal static extern nint ProcurarJanelaEx(nint pai, nint depoisDe, nint classe, nint titulo);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW")]
    internal static extern int LerTitulo(nint janela, nint texto, int maximo);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    internal static extern int MedirTitulo(nint janela);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW")]
    internal static extern int LerClasse(nint janela, nint texto, int maximo);

    [DllImport("user32.dll")]
    internal static extern int OpenClipboard(nint dono);

    [DllImport("kernel32.dll")]
    internal static extern nint OpenProcess(uint acesso, int herdar, uint processo);

    [DllImport("kernel32.dll")]
    internal static extern int ReadProcessMemory(nint processo, nint endereco, nint destino, nint tamanho, nint lidos);

    [DllImport("psapi.dll")]
    internal static extern int EnumProcesses(nint processos, uint tamanho, nint usados);

    [DllImport("kernel32.dll")]
    internal static extern nint CreateToolhelp32Snapshot(uint flags, uint processo);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW")]
    internal static extern int LerCaminhoDoProcesso(nint processo, uint flags, nint texto, nint tamanho);

    [DllImport("psapi.dll", EntryPoint = "GetModuleFileNameExW")]
    internal static extern uint LerModuloDeOutroProcesso(nint processo, nint modulo, nint texto, uint tamanho);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern nint WindowFromPoint(long ponto);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(uint minimo, uint maximo, nint modulo, nint retorno, uint processo, uint thread, uint flags);

    // Persistência escondida
    [DllImport("advapi32.dll", EntryPoint = "RegSetValueExW")]
    internal static extern int GravarValor(nint chave, nint nome, uint reservado, uint tipo, nint dados, uint tamanho);

    [DllImport("advapi32.dll", EntryPoint = "RegCreateKeyExW")]
    internal static extern int CriarChave(nint chave, nint subchave, uint reservado, nint classe, uint opcoes, uint acesso, nint seguranca, nint resultado, nint disposicao);

    [DllImport("advapi32.dll", EntryPoint = "CreateServiceW")]
    internal static extern nint CriarServico(nint gerenciador, nint nome, nint exibicao, uint acesso, uint tipo, uint inicio, uint erro, nint binario, nint grupo, nint marca, nint dependencias, nint conta, nint senha);

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW")]
    internal static extern nint AbrirGerenciadorDeServicos(nint maquina, nint banco, uint acesso);

    // Código dinâmico. LoadLibraryExW e GetProcAddress estão na lista de permissões do apphost,
    // mas num assembly gerenciado continuam proibidas: a permissão nunca se estende às DLLs.
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW")]
    internal static extern nint CarregarBiblioteca(nint caminho);

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW")]
    internal static extern nint CarregarBibliotecaEx(nint caminho, nint arquivo, uint flags);

    [DllImport("kernel32.dll")]
    internal static extern nint GetProcAddress(nint modulo, nint nome);

    // Importação por ordinal: o ponto de entrada não pode ser conferido com a lista.
    [DllImport("user32.dll", EntryPoint = "#2000")]
    internal static extern int PorOrdinal();
}

/// <summary>P/Invoke gerado pelo LibraryImport, como o analisador do SDK sugere usar.</summary>
internal static partial class LibraryImportProibido
{
    [LibraryImport("user32.dll")]
    internal static partial nint GetClipboardData(uint formato);
}

/// <summary>Interface COM declarada em C#, como uma Desktop Duplication escrita à mão (IDXGIOutput1).</summary>
[ComImport]
[Guid("00cddea8-939b-4b83-a340-a685226666cc")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISaidaDxgiAmostra
{
    void DuplicateOutput(nint dispositivo, out nint duplicacao);
}

/// <summary>Referências gerenciadas proibidas, uma ou mais por categoria de SECURITY.md 3.2.</summary>
internal static class ReferenciasProibidas
{
    internal static void Processos()
    {
        _ = System.Diagnostics.Process.Start("amostra.exe");
        _ = new System.Diagnostics.ProcessStartInfo("amostra.exe");
    }

    internal static void Rede()
    {
        using var cliente = new System.Net.Http.HttpClient();
        using var soquete = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        using var websocket = new System.Net.WebSockets.ClientWebSocket();
        using var web = new System.Net.WebClient();
        var pedido = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://exemplo.invalid/");
        _ = pedido;
    }

    internal static void LerOutrosAplicativos()
    {
        _ = System.Windows.Clipboard.GetText();
        _ = System.Windows.Forms.Clipboard.GetText();
        _ = System.Windows.Automation.AutomationElement.RootElement;
        _ = System.Diagnostics.Process.GetProcesses();
        _ = System.Diagnostics.Process.GetProcessesByName("amostra");
        _ = System.Diagnostics.Process.GetProcessById(4);
    }

    internal static void CapturaDeTela()
    {
        using var grafico = System.Drawing.Graphics.FromHwnd(nint.Zero);
        grafico.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(1, 1));
    }

    internal static void PersistenciaEscondida()
    {
        using Microsoft.Win32.RegistryKey? chave = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\BuzzyAmostra");
    }

    internal static void CodigoDinamico()
    {
        _ = System.Reflection.Assembly.Load(Array.Empty<byte>());
        _ = System.Reflection.Assembly.LoadFrom("amostra.dll");
        _ = System.Reflection.Assembly.LoadFile(@"C:\amostra.dll");
        _ = System.Reflection.Assembly.UnsafeLoadFrom("amostra.dll");
        _ = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(@"C:\amostra.dll");
        _ = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(Stream.Null);
        _ = System.Runtime.InteropServices.NativeLibrary.Load("amostra.dll");
        _ = Marshal.GetDelegateForFunctionPointer<Action>(nint.Zero);
        _ = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName("Amostra"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
    }
}
