using Buzzy.PortaoApis.Testes.Amostras;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>
/// Verificação dos assemblies gerenciados: as amostras compiladas neste projeto (saída real do
/// compilador C#) e assemblies sintéticos para os casos que o compilador não produz aqui.
/// </summary>
public sealed class TestesDoVerificadorDeAssembly : IDisposable
{
    // O que o portão precisa achar nas amostras proibidas, exatamente como aparece nos metadados.
    private static readonly string[] PInvokesEsperados =
    [
        "user32.dll!SetWindowsHookExW", "user32.dll!RegisterRawInputDevices", "user32.dll!GetAsyncKeyState",
        "user32.dll!GetKeyboardState", "user32.dll!GetKeyState", "user32.dll!RegisterHotKey",
        "user32.dll!SendInput", "USER32.DLL!mouse_event", "user32!keybd_event",
        "gdi32.dll!BitBlt", "gdi32.dll!StretchBlt", "user32.dll!PrintWindow", "gdi32.dll!CreateDCW",
        "kernel32.dll!CreateProcessW", "advapi32.dll!CreateProcessAsUserW", "shell32.dll!ShellExecuteW",
        "shell32.dll!ShellExecuteExW", "kernel32.dll!WinExec",
        "ws2_32.dll!connect", "WSOCK32.DLL!send", "winhttp.dll!WinHttpOpen", "wininet.dll!InternetOpenW", "urlmon.dll!URLDownloadToFileW",
        "user32.dll!EnumWindows", "user32.dll!EnumChildWindows", "user32.dll!EnumThreadWindows", "user32.dll!FindWindowW",
        "user32.dll!FindWindowExW", "user32.dll!GetWindowTextW", "user32.dll!GetWindowTextLengthW", "user32.dll!GetClassNameW",
        "user32.dll!OpenClipboard", "user32.dll!GetClipboardData", "kernel32.dll!OpenProcess", "kernel32.dll!ReadProcessMemory",
        "psapi.dll!EnumProcesses", "kernel32.dll!CreateToolhelp32Snapshot", "kernel32.dll!QueryFullProcessImageNameW",
        "psapi.dll!GetModuleFileNameExW", "user32.dll!GetForegroundWindow", "user32.dll!WindowFromPoint", "user32.dll!SetWinEventHook",
        "advapi32.dll!RegSetValueExW", "advapi32.dll!RegCreateKeyExW", "advapi32.dll!CreateServiceW", "advapi32.dll!OpenSCManagerW",
        "kernel32.dll!LoadLibraryW", "kernel32.dll!LoadLibraryExW", "kernel32.dll!GetProcAddress",
        "user32.dll!#2000",
    ];

    private static readonly string[] ReferenciasEsperadas =
    [
        "System.Diagnostics.Process.Start", "System.Diagnostics.ProcessStartInfo",
        "System.Net.Http.HttpClient", "System.Net.Sockets.Socket", "System.Net.Sockets.SocketType", "System.Net.Sockets.ProtocolType",
        "System.Net.WebSockets.ClientWebSocket", "System.Net.WebClient", "System.Net.WebRequest", "System.Net.HttpWebRequest",
        "System.Windows.Clipboard", "System.Windows.Forms.Clipboard", "System.Windows.Automation.AutomationElement",
        "System.Diagnostics.Process.GetProcesses", "System.Diagnostics.Process.GetProcessesByName", "System.Diagnostics.Process.GetProcessById",
        "System.Drawing.Graphics.CopyFromScreen",
        "Microsoft.Win32.Registry", "Microsoft.Win32.RegistryKey",
        "System.Reflection.Assembly.Load", "System.Reflection.Assembly.LoadFrom", "System.Reflection.Assembly.LoadFile",
        "System.Reflection.Assembly.UnsafeLoadFrom",
        "System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath", "System.Runtime.Loader.AssemblyLoadContext.LoadFromStream",
        "System.Runtime.InteropServices.NativeLibrary", "System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer",
        "System.Reflection.Emit.AssemblyBuilder", "System.Reflection.Emit.AssemblyBuilderAccess",
        "Buzzy.PortaoApis.Testes.Amostras.ISaidaDxgiAmostra.DuplicateOutput",
        "System.Reflection.Assembly.get_Location", "System.Reflection.Assembly.get_CodeBase", "System.Reflection.Assembly.get_EscapedCodeBase",
        "System.Reflection.Assembly.GetFile", "System.Reflection.Assembly.GetFiles",
        "System.Reflection.AssemblyName.get_CodeBase", "System.Reflection.AssemblyName.get_EscapedCodeBase",
        "System.Runtime.InteropServices.Marshal.GetHINSTANCE", "System.Reflection.Module.get_Name", "System.Reflection.Module.get_FullyQualifiedName",
    ];

    // Trechos que não podem aparecer em nenhuma violação: as amostras permitidas.
    private static readonly string[] Permitidos =
    [
        "SetWindowPos", "GetWindowLongPtrW", "MonitorFromPoint", "GetMonitorInfoW", "EnumDisplayMonitors", "Shell_NotifyIconW",
        "SetForegroundWindow", "GetWindowRect", "SetCapture", "ReleaseCapture", "GetDpiForMonitor", "GetModuleHandleW",
        "GetCurrentProcessId", "RegOpenKeyExW", "NtQueryTimerResolution",
        "GetCurrentProcess", "get_Id", "get_StartTime", "get_WorkingSet64", "Stopwatch", "DispatcherTimer", "GetExecutingAssembly",
        "GetLoadContext", "System.AppDomain", "SizeOf", "GetLastPInvokeError", "SystemEvents", "AutomationProperties",
        "System.Windows.Forms.Screen",
    ];

    private readonly PastaTemporaria _pasta = new();

    public void Dispose() => _pasta.Dispose();

    private static AnaliseDeAssembly AnalisarAmostras() => VerificadorDeAssembly.Verificar(typeof(PInvokesProibidos).Assembly.Location);

    [Teste]
    public void AmostrasCompiladasGeramExatamenteAsViolacoesEsperadas()
    {
        AnaliseDeAssembly analise = AnalisarAmostras();
        var obtidas = analise.Violacoes.Select(v => v.Api).ToHashSet(StringComparer.Ordinal);
        var esperadas = PInvokesEsperados.Concat(ReferenciasEsperadas).ToHashSet(StringComparer.Ordinal);

        List<string> faltando = [.. esperadas.Except(obtidas).Order(StringComparer.Ordinal)];
        List<string> sobrando = [.. obtidas.Except(esperadas).Order(StringComparer.Ordinal)];
        Afirmar.Verdadeiro(faltando.Count == 0 && sobrando.Count == 0,
            $"faltando: [{string.Join(", ", faltando)}]; sobrando: [{string.Join(", ", sobrando)}]");
        Afirmar.Igual(analise.Violacoes.Count, obtidas.Count, "violação repetida");
    }

    [Teste]
    public void AmostrasPermitidasNaoSaoAcusadas()
    {
        AnaliseDeAssembly analise = AnalisarAmostras();
        foreach (string permitido in Permitidos)
        {
            Violacao? indevida = analise.Violacoes.FirstOrDefault(v => v.Api.Contains(permitido, StringComparison.Ordinal));
            Afirmar.Nulo(indevida, $"{permitido} foi acusado");
        }
        Afirmar.Verdadeiro(analise.PInvokes >= PInvokesEsperados.Length + 15, $"P/Invokes contados: {analise.PInvokes}");
    }

    [Teste]
    public void ViolacoesDasAmostrasTemCategoriaCodigoEOrigem()
    {
        AnaliseDeAssembly analise = AnalisarAmostras();
        string caminho = typeof(PInvokesProibidos).Assembly.Location;

        Violacao envio = analise.Violacoes.Single(v => v.Api == "user32.dll!SendInput");
        Afirmar.Igual(Categoria.InjetarInput, envio.Categoria);
        Afirmar.Igual(Codigos.PInvoke, envio.Codigo);
        Afirmar.Igual(caminho, envio.Arquivo);
        Afirmar.Igual(0, envio.Linha);
        Afirmar.Contem("PInvokesProibidos.EnviarEntrada", envio.Detalhe);

        Violacao ordinal = analise.Violacoes.Single(v => v.Api == "user32.dll!#2000");
        Afirmar.Igual(Categoria.CodigoDinamico, ordinal.Categoria);
        Afirmar.Nulo(ordinal.Regra);
        Afirmar.Contem("ordinal", ordinal.Detalhe);

        Afirmar.Igual(Categoria.Rede, analise.Violacoes.Single(v => v.Api == "WSOCK32.DLL!send").Categoria);
        Afirmar.Igual(Categoria.Processos, analise.Violacoes.Single(v => v.Api == "System.Diagnostics.ProcessStartInfo").Categoria);
        Afirmar.Igual(Codigos.ReferenciaGerenciada, analise.Violacoes.Single(v => v.Api == "System.Diagnostics.Process.Start").Codigo);
        Afirmar.Igual(Categoria.CapturaDeTela, analise.Violacoes.Single(v => v.Api.EndsWith(".DuplicateOutput", StringComparison.Ordinal)).Categoria);

        // As importações da lista de permissões do apphost continuam proibidas num assembly.
        Afirmar.Igual(Categoria.CodigoDinamico, analise.Violacoes.Single(v => v.Api == "kernel32.dll!LoadLibraryExW").Categoria);
        Afirmar.Igual(Categoria.Processos, analise.Violacoes.Single(v => v.Api == "shell32.dll!ShellExecuteW").Categoria);
    }

    [Teste]
    public void CadaRegraDaListaEhReconhecidaNumAssemblySintetico()
    {
        var sintetico = new AssemblySintetico("Sintetico.Todas");
        foreach (Regra regra in ListaProibida.Regras)
        {
            switch (regra.Tipo)
            {
                case TipoDeRegra.FuncaoNativa:
                    sintetico.DeclararPInvoke("modulo_qualquer.dll", regra.Alvo + "W");
                    break;
                case TipoDeRegra.ModuloNativo:
                    sintetico.DeclararPInvoke(regra.Alvo.ToUpperInvariant() + ".DLL", "FuncaoQualquer");
                    break;
                case TipoDeRegra.TipoGerenciado:
                    int ponto = regra.Alvo.LastIndexOf('.');
                    sintetico.ReferenciarTipo(regra.Alvo[..ponto], regra.Alvo[(ponto + 1)..]);
                    break;
                case TipoDeRegra.NamespaceGerenciado:
                    sintetico.ReferenciarTipo(regra.Alvo + ".Interno", "TipoQualquer");
                    break;
                case TipoDeRegra.MembroGerenciado:
                    int p = regra.Alvo.LastIndexOf('.');
                    sintetico.ReferenciarMembro(regra.Alvo[..p], regra.Alvo[(p + 1)..], regra.Membro!);
                    break;
                case TipoDeRegra.MetodoCom:
                    sintetico.DeclararMetodoCom(regra.Alvo);
                    break;
            }
        }
        string caminho = Path.Combine(_pasta.Caminho, "Sintetico.Todas.dll");
        sintetico.Gravar(caminho);

        AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho);
        foreach (Regra regra in ListaProibida.Regras)
            Afirmar.Verdadeiro(analise.Violacoes.Any(v => ReferenceEquals(v.Regra, regra)), $"regra não reconhecida: {regra.Descricao}");
    }

    [Teste]
    public void AssemblySinteticoLimpoNaoTemViolacoes()
    {
        var sintetico = new AssemblySintetico("Sintetico.Limpo");
        sintetico.ReferenciarMembro("System", "Console", "WriteLine");
        sintetico.ReferenciarMembro("System.Windows.Threading", "DispatcherTimer", "Start");
        sintetico.ReferenciarMembro("System.Diagnostics", "Process", "GetCurrentProcess");
        sintetico.ReferenciarMembro("Sintetico.Outro", "Carregador", "Load");
        sintetico.ReferenciarMembro("Sintetico.Outro", "Carregador", "LoadFromStream");
        sintetico.DeclararPInvoke("user32.dll", "SetWindowPos");
        sintetico.DeclararPInvoke("shell32.dll", "Shell_NotifyIconW");
        string caminho = Path.Combine(_pasta.Caminho, "Sintetico.Limpo.dll");
        sintetico.Gravar(caminho);

        AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho);
        Afirmar.Igual(0, analise.Violacoes.Count, string.Join("; ", analise.Violacoes.Select(v => v.Api)));
        Afirmar.Igual(2, analise.PInvokes);
    }

    [Teste]
    public void NamespaceWinRtDeCapturaGrafiasEstranhasEOrdinais()
    {
        var sintetico = new AssemblySintetico("Sintetico.Casos");
        sintetico.ReferenciarTipo("Windows.Graphics.Capture", "GraphicsCaptureItem");
        sintetico.DeclararPInvoke("USER32.DLL", "sendinputw");
        sintetico.DeclararPInvoke(@"C:\Windows\System32\WinInet.dll", "HttpOpenRequestW");
        sintetico.DeclararPInvoke("ws2_32.dll", "#23");
        sintetico.DeclararPInvoke("user32.dll", "#10");
        string caminho = Path.Combine(_pasta.Caminho, "Sintetico.Casos.dll");
        sintetico.Gravar(caminho);

        List<Violacao> violacoes = [.. VerificadorDeAssembly.Verificar(caminho).Violacoes];
        Afirmar.Igual(Categoria.CapturaDeTela, violacoes.Single(v => v.Api == "Windows.Graphics.Capture.GraphicsCaptureItem").Categoria);
        Afirmar.Igual("SendInput", violacoes.Single(v => v.Api == "USER32.DLL!sendinputw").Regra?.Alvo);
        Afirmar.Igual("wininet", violacoes.Single(v => v.Api.EndsWith("!HttpOpenRequestW", StringComparison.Ordinal)).Regra?.Alvo);
        // Ordinal de módulo de rede: vale a regra do módulo. Ordinal de outro módulo: não verificável.
        Afirmar.Igual(Categoria.Rede, violacoes.Single(v => v.Api == "ws2_32.dll!#23").Categoria);
        Violacao ordinal = violacoes.Single(v => v.Api == "user32.dll!#10");
        Afirmar.Igual(Categoria.CodigoDinamico, ordinal.Categoria);
        Afirmar.Nulo(ordinal.Regra);
        Afirmar.Igual(5, violacoes.Count);
    }

    [Teste]
    public void TipoAninhadoEMetodoComReferenciado()
    {
        var sintetico = new AssemblySintetico("Sintetico.Aninhados");
        sintetico.ReferenciarTipoAninhado(sintetico.ReferenciarTipo("System.Net.Sockets", "Socket"), "Interno");
        // Nome igual ao de um tipo proibido, mas aninhado num tipo qualquer: não é Microsoft.Win32.Registry.
        sintetico.ReferenciarTipoAninhado(sintetico.ReferenciarTipo("Sintetico.Outro", "Externo"), "Registry");
        sintetico.ReferenciarMembro("Vortice.DXGI", "IDXGIOutput1", "DuplicateOutput");
        string caminho = Path.Combine(_pasta.Caminho, "Sintetico.Aninhados.dll");
        sintetico.Gravar(caminho);

        List<string> apis = [.. VerificadorDeAssembly.Verificar(caminho).Violacoes.Select(v => v.Api).Order(StringComparer.Ordinal)];
        Afirmar.Sequencia<string>(["System.Net.Sockets.Socket", "Vortice.DXGI.IDXGIOutput1.DuplicateOutput"], apis);
    }

    [Teste]
    public void UsoRestritoSoValeNoTipoENoAssemblyDoObservador()
    {
        // DEC-034: as três funções do observador de tela cheia, declaradas no tipo dele (ou num aninhado) do Buzzy, são uso
        // restrito; no mesmo tipo, WindowFromPoint, a variante W e outro módulo continuam violação.
        foreach (string? aninhado in new[] { null, "Nativo" })
        {
            var sintetico = new AssemblySintetico("Buzzy", "Buzzy.App.Plataforma", "ObservadorDeTelaCheia", aninhado);
            sintetico.DeclararPInvoke("user32.dll", "SetWinEventHook");
            sintetico.DeclararPInvoke("USER32.DLL", "GetForegroundWindow");
            sintetico.DeclararPInvoke("user32", "GetWindowThreadProcessId");
            sintetico.DeclararPInvoke("user32.dll", "WindowFromPoint");
            sintetico.DeclararPInvoke("user32.dll", "SetWinEventHookW");
            sintetico.DeclararPInvoke("kernel32.dll", "GetForegroundWindow");
            string caminho = Path.Combine(_pasta.Caminho, $"Observador{aninhado}.dll");
            sintetico.Gravar(caminho);

            AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho);
            Afirmar.Sequencia<string>(["USER32.DLL!GetForegroundWindow", "user32!GetWindowThreadProcessId", "user32.dll!SetWinEventHook"],
                analise.UsosRestritos.Select(u => u.Api).Order(StringComparer.Ordinal), $"aninhado: {aninhado}");
            Afirmar.Sequencia<string>(["kernel32.dll!GetForegroundWindow", "user32.dll!SetWinEventHookW", "user32.dll!WindowFromPoint"],
                analise.Violacoes.Select(v => v.Api).Order(StringComparer.Ordinal), $"aninhado: {aninhado}");
            string tipo = aninhado is null ? "Buzzy.App.Plataforma.ObservadorDeTelaCheia." : "Buzzy.App.Plataforma.ObservadorDeTelaCheia+Nativo.";
            foreach (UsoRestritoVisto uso in analise.UsosRestritos)
            {
                Afirmar.Verdadeiro(uso.Onde.StartsWith(tipo, StringComparison.Ordinal), uso.Onde);
                Afirmar.Igual(Categoria.LerOutrosAplicativos, uso.Categoria, uso.Api);
                Afirmar.Igual(caminho, uso.Arquivo);
            }
        }
    }

    [Teste]
    public void FuncoesDoObservadorForaDoLugarDeleSaoViolacao()
    {
        (string Assembly, string Namespace, string Tipo, string? Aninhado)[] lugares =
        [
            ("Buzzy", "Buzzy.App.Plataforma", "Win32", null),
            ("Buzzy", "Buzzy.App.Plataforma", "ObservadorDeTelaCheiaFalso", null),
            ("Buzzy", "Buzzy.App", "ObservadorDeTelaCheia", null),
            ("Buzzy.Core", "Buzzy.App.Plataforma", "ObservadorDeTelaCheia", null),
            ("Outro", "Buzzy.App.Plataforma", "ObservadorDeTelaCheia", "Nativo"),
        ];
        int n = 0;
        foreach ((string assembly, string nomeDoNamespace, string tipo, string? aninhado) in lugares)
        {
            var sintetico = new AssemblySintetico(assembly, nomeDoNamespace, tipo, aninhado);
            sintetico.DeclararPInvoke("user32.dll", "SetWinEventHook");
            sintetico.DeclararPInvoke("user32.dll", "GetForegroundWindow");
            sintetico.DeclararPInvoke("user32.dll", "GetWindowThreadProcessId");
            string caminho = Path.Combine(_pasta.Caminho, $"Lugar{++n}.dll");
            sintetico.Gravar(caminho);

            AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho);
            string lugar = $"{assembly}: {nomeDoNamespace}.{tipo}{(aninhado is null ? "" : "+" + aninhado)}";
            Afirmar.Igual(0, analise.UsosRestritos.Count, lugar);
            Afirmar.Sequencia<string>(["user32.dll!GetForegroundWindow", "user32.dll!GetWindowThreadProcessId", "user32.dll!SetWinEventHook"],
                analise.Violacoes.Select(v => v.Api).Order(StringComparer.Ordinal), lugar);
        }
    }

    [Teste]
    public void ArquivoNativoNaoEhAssemblyGerenciado()
    {
        string apphost = Repositorio.ApphostReal();
        Afirmar.Lanca<BadImageFormatException>(() => VerificadorDeAssembly.Verificar(apphost));
    }
}
