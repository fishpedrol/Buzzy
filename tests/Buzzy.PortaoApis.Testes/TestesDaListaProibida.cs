using System.Text.RegularExpressions;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>A tabela única de regras: conteúdo mínimo exigido, integridade e normalização de nomes.</summary>
public sealed class TestesDaListaProibida
{
    // Lista mínima de SECURITY.md 3.2 pedida para a Fase 1, com a categoria de cada item.
    private static readonly (string Nome, Categoria Categoria)[] FuncoesMinimas =
    [
        ("SetWindowsHookEx", Categoria.InputGlobal), ("RegisterRawInputDevices", Categoria.InputGlobal),
        ("GetAsyncKeyState", Categoria.InputGlobal), ("GetKeyboardState", Categoria.InputGlobal),
        ("GetKeyState", Categoria.InputGlobal), ("RegisterHotKey", Categoria.InputGlobal),
        ("SendInput", Categoria.InjetarInput), ("mouse_event", Categoria.InjetarInput), ("keybd_event", Categoria.InjetarInput),
        ("BitBlt", Categoria.CapturaDeTela), ("StretchBlt", Categoria.CapturaDeTela), ("PrintWindow", Categoria.CapturaDeTela),
        ("CreateDC", Categoria.CapturaDeTela),
        ("CreateProcess", Categoria.Processos), ("CreateProcessAsUser", Categoria.Processos), ("ShellExecute", Categoria.Processos),
        ("ShellExecuteEx", Categoria.Processos), ("WinExec", Categoria.Processos),
        ("URLDownloadToFile", Categoria.Rede),
        ("EnumWindows", Categoria.LerOutrosAplicativos), ("EnumChildWindows", Categoria.LerOutrosAplicativos),
        ("EnumThreadWindows", Categoria.LerOutrosAplicativos), ("FindWindow", Categoria.LerOutrosAplicativos),
        ("FindWindowEx", Categoria.LerOutrosAplicativos), ("GetWindowText", Categoria.LerOutrosAplicativos),
        ("GetWindowTextLength", Categoria.LerOutrosAplicativos), ("GetClassName", Categoria.LerOutrosAplicativos),
        ("OpenClipboard", Categoria.LerOutrosAplicativos), ("GetClipboardData", Categoria.LerOutrosAplicativos),
        ("OpenProcess", Categoria.LerOutrosAplicativos), ("ReadProcessMemory", Categoria.LerOutrosAplicativos),
        ("EnumProcesses", Categoria.LerOutrosAplicativos), ("CreateToolhelp32Snapshot", Categoria.LerOutrosAplicativos),
        ("QueryFullProcessImageName", Categoria.LerOutrosAplicativos), ("GetModuleFileNameEx", Categoria.LerOutrosAplicativos),
        ("GetForegroundWindow", Categoria.LerOutrosAplicativos), ("WindowFromPoint", Categoria.LerOutrosAplicativos),
        ("SetWinEventHook", Categoria.LerOutrosAplicativos),
        ("RegSetValueEx", Categoria.PersistenciaEscondida), ("RegCreateKeyEx", Categoria.PersistenciaEscondida),
        ("CreateService", Categoria.PersistenciaEscondida), ("OpenSCManager", Categoria.PersistenciaEscondida),
        ("LoadLibrary", Categoria.CodigoDinamico), ("LoadLibraryEx", Categoria.CodigoDinamico), ("GetProcAddress", Categoria.CodigoDinamico),
    ];

    private static readonly string[] ModulosMinimos = ["ws2_32", "wsock32", "winhttp", "wininet"];

    private static readonly (string Tipo, Categoria Categoria)[] TiposMinimos =
    [
        ("System.Diagnostics.ProcessStartInfo", Categoria.Processos),
        ("System.Net.WebClient", Categoria.Rede), ("System.Net.WebRequest", Categoria.Rede), ("System.Net.HttpWebRequest", Categoria.Rede),
        ("System.Windows.Clipboard", Categoria.LerOutrosAplicativos), ("System.Windows.Forms.Clipboard", Categoria.LerOutrosAplicativos),
        ("System.Windows.Automation.AutomationElement", Categoria.LerOutrosAplicativos),
        ("Microsoft.Win32.Registry", Categoria.PersistenciaEscondida), ("Microsoft.Win32.RegistryKey", Categoria.PersistenciaEscondida),
        ("System.Runtime.InteropServices.NativeLibrary", Categoria.CodigoDinamico),
    ];

    private static readonly (string Namespace, Categoria Categoria)[] NamespacesMinimos =
    [
        ("Windows.Graphics.Capture", Categoria.CapturaDeTela),
        ("System.Net.Http", Categoria.Rede), ("System.Net.Sockets", Categoria.Rede), ("System.Net.WebSockets", Categoria.Rede),
        ("System.Reflection.Emit", Categoria.CodigoDinamico),
    ];

    private static readonly (string Tipo, string Membro, Categoria Categoria)[] MembrosMinimos =
    [
        ("System.Drawing.Graphics", "CopyFromScreen", Categoria.CapturaDeTela),
        ("System.Diagnostics.Process", "Start", Categoria.Processos),
        ("System.Diagnostics.Process", "GetProcesses", Categoria.LerOutrosAplicativos),
        ("System.Diagnostics.Process", "GetProcessesByName", Categoria.LerOutrosAplicativos),
        ("System.Diagnostics.Process", "GetProcessById", Categoria.LerOutrosAplicativos),
        ("System.Reflection.Assembly", "Load", Categoria.CodigoDinamico),
        ("System.Reflection.Assembly", "LoadFrom", Categoria.CodigoDinamico),
        ("System.Reflection.Assembly", "LoadFile", Categoria.CodigoDinamico),
        ("System.Reflection.Assembly", "UnsafeLoadFrom", Categoria.CodigoDinamico),
        ("System.Runtime.Loader.AssemblyLoadContext", "LoadFromAssemblyPath", Categoria.CodigoDinamico),
        ("System.Runtime.Loader.AssemblyLoadContext", "LoadFromStream", Categoria.CodigoDinamico),
        ("System.Runtime.InteropServices.Marshal", "GetDelegateForFunctionPointer", Categoria.CodigoDinamico),
    ];

    [Teste]
    public void ContemTodasAsFuncoesNativasDaListaMinima()
    {
        foreach ((string nome, Categoria categoria) in FuncoesMinimas)
        {
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa("qualquer.dll", nome), nome);
            Afirmar.Igual(TipoDeRegra.FuncaoNativa, regra.Tipo, nome);
            Afirmar.Igual(categoria, regra.Categoria, nome);
        }
    }

    // DEC-037, item 12: as funções que leriam pixels, identidade, hierarquia ou processo de janelas de outros aplicativos por
    // outros caminhos, acrescentadas junto com a curiosidade, que amplia o código que lida com a janela ativa.
    [Teste]
    public void ContemAsFuncoesAcrescentadasPelaCuriosidade()
    {
        string[] captura = ["GetDC", "GetWindowDC", "GetDCEx", "GetPixel", "GetDIBits"];
        string[] leitura =
        [
            "RealGetWindowClass", "GetWindowModuleFileName", "GetWindowInfo", "GetGUIThreadInfo", "GetTitleBarInfo", "GetWindowPlacement",
            "DwmGetWindowAttribute", "OpenThread", "GetProcessIdOfThread", "WTSEnumerateProcesses", "NtQuerySystemInformation",
            "ChildWindowFromPoint", "ChildWindowFromPointEx", "RealChildWindowFromPoint", "WindowFromPhysicalPoint", "GetAncestor",
            "GetWindow", "GetTopWindow", "GetLastActivePopup", "GetShellWindow",
        ];
        foreach ((string nome, Categoria categoria) in captura.Select(n => (n, Categoria.CapturaDeTela)).Concat(leitura.Select(n => (n, Categoria.LerOutrosAplicativos))))
        {
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa("user32.dll", nome), nome);
            Afirmar.Igual((TipoDeRegra.FuncaoNativa, categoria), (regra.Tipo, regra.Categoria), nome);
        }
        // As vizinhas que o adaptador usa nas próprias janelas continuam fora da lista.
        foreach (string permitida in new[] { "GetWindowRect", "GetWindowLongPtr", "MonitorFromWindow" })
            Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", permitida), permitida);
    }

    [Teste]
    public void ContemOsModulosDeRedeEOsItensGerenciadosDaListaMinima()
    {
        foreach (string modulo in ModulosMinimos)
            Afirmar.Igual(Categoria.Rede, Afirmar.NaoNulo(ListaProibida.ProcurarNativa(modulo, "FuncaoQualquer"), modulo).Categoria, modulo);

        foreach ((string tipo, Categoria categoria) in TiposMinimos)
        {
            int ponto = tipo.LastIndexOf('.');
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarTipo(tipo[..ponto], tipo[(ponto + 1)..]), tipo);
            Afirmar.Igual(categoria, regra.Categoria, tipo);
        }

        foreach ((string nomeDoNamespace, Categoria categoria) in NamespacesMinimos)
        {
            Afirmar.Igual(categoria, Afirmar.NaoNulo(ListaProibida.ProcurarTipo(nomeDoNamespace, "TipoQualquer"), nomeDoNamespace).Categoria);
            Afirmar.Igual(categoria, Afirmar.NaoNulo(ListaProibida.ProcurarTipo(nomeDoNamespace + ".Sub", "TipoQualquer"), nomeDoNamespace + ".Sub").Categoria);
        }

        foreach ((string tipo, string membro, Categoria categoria) in MembrosMinimos)
            Afirmar.Igual(categoria, Afirmar.NaoNulo(ListaProibida.ProcurarMembro(tipo, membro), $"{tipo}.{membro}").Categoria);

        Afirmar.Igual(Categoria.CapturaDeTela, Afirmar.NaoNulo(ListaProibida.ProcurarMetodoCom("DuplicateOutput")).Categoria);
    }

    [Teste]
    public void CadaRegraTemMotivoEAsOitoCategoriasEstaoCobertas()
    {
        foreach (Regra regra in ListaProibida.Regras)
        {
            Afirmar.Verdadeiro(regra.Motivo.Length > 10, $"regra {regra.Descricao} sem motivo");
            Afirmar.Diferente(Categoria.Manifesto, regra.Categoria, regra.Descricao);
            Afirmar.Igual(regra.Tipo == TipoDeRegra.MembroGerenciado, regra.Membro is not null, regra.Descricao);
        }

        Categoria[] categorias = [.. Enum.GetValues<Categoria>().Where(c => c != Categoria.Manifesto)];
        foreach (Categoria categoria in categorias)
            Afirmar.Verdadeiro(ListaProibida.Regras.Any(r => r.Categoria == categoria), $"nenhuma regra para {categoria.Nome()}");
        Afirmar.Igual(10, categorias.Length);
        Afirmar.Igual("Arquivo único", Categoria.ArquivoUnico.Nome());
    }

    [Teste]
    public void ConfiguracaoDeVideo_LerEhPermitido_MudarEhProibido()
    {
        // Revisão de segurança do bloco P6-P9, achado 6: o P6 trouxe a família DisplayConfig para a chave estável do monitor,
        // só de leitura. "Nenhuma configuração global alterada" deixa de depender só de revisão: as funções que mudam o vídeo
        // do sistema todo reprovam o build, em qualquer grafia.
        foreach (string funcao in new[] { "SetDisplayConfig", "DisplayConfigSetDeviceInfo", "ChangeDisplaySettings", "ChangeDisplaySettingsA", "ChangeDisplaySettingsW",
            "ChangeDisplaySettingsEx", "ChangeDisplaySettingsExA", "ChangeDisplaySettingsExW", "setdisplayconfig" })
        {
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa("user32.dll", funcao), funcao);
            Afirmar.Igual(Categoria.ConfiguracaoGlobal, regra.Categoria, funcao);
        }
        foreach (string funcao in new[] { "GetDisplayConfigBufferSizes", "QueryDisplayConfig", "DisplayConfigGetDeviceInfo" })
            Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", funcao), funcao);
        Afirmar.Igual("Alterar configuração global", Categoria.ConfiguracaoGlobal.Nome());
    }

    [Teste]
    public void PadroesDeFonteSaoPalavrasOuSequenciasComPonto()
    {
        var formato = new Regex(@"^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)*$", RegexOptions.CultureInvariant);
        var categoriaPorPadrao = new Dictionary<string, Categoria>(StringComparer.OrdinalIgnoreCase);
        foreach (Regra regra in ListaProibida.Regras)
        {
            foreach (string padrao in regra.PadroesNaFonte)
            {
                Afirmar.Verdadeiro(formato.IsMatch(padrao), $"padrão inválido \"{padrao}\" em {regra.Descricao}");
                // Um padrão repetido em duas regras só é aceitável na mesma categoria (Clipboard).
                if (categoriaPorPadrao.TryGetValue(padrao, out Categoria anterior))
                    Afirmar.Igual(anterior, regra.Categoria, $"padrão \"{padrao}\" em categorias diferentes");
                categoriaPorPadrao[padrao] = regra.Categoria;
            }
        }
    }

    [Teste]
    public void FuncaoNativaIgnoraMaiusculasESufixosAeW()
    {
        Afirmar.Igual("SendInput", ListaProibida.ProcurarNativa("user32.dll", "SendInput")?.Alvo);
        Afirmar.Igual("SendInput", ListaProibida.ProcurarNativa("user32.dll", "SENDINPUT")?.Alvo);
        Afirmar.Igual("SendInput", ListaProibida.ProcurarNativa("user32.dll", "sendinputw")?.Alvo);
        Afirmar.Igual("CreateDC", ListaProibida.ProcurarNativa("gdi32.dll", "CreateDCW")?.Alvo);
        Afirmar.Igual("ShellExecuteEx", ListaProibida.ProcurarNativa("shell32.dll", "ShellExecuteExW")?.Alvo);
        Afirmar.Igual("ShellExecute", ListaProibida.ProcurarNativa("shell32.dll", "ShellExecuteA")?.Alvo);
        Afirmar.Igual("GetWindowTextLength", ListaProibida.ProcurarNativa("user32.dll", "GetWindowTextLengthW")?.Alvo);
        Afirmar.Igual("PrintWindow", ListaProibida.ProcurarNativa("user32.dll", "PrintWindow")?.Alvo);
        Afirmar.Igual("URLDownloadToFile", ListaProibida.ProcurarNativa("urlmon.dll", "URLDownloadToFileW")?.Alvo);

        // Só as variantes: cortar letras finais daria falso positivo.
        Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", "PrintWindo"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", "SendInputX"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", "SendInputWW"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", "SendInpu"));
    }

    // Fase 9, F9-P2 (DEC-040, item 6): as famílias que a auditoria achou fora da lista, cada uma na categoria certa.
    [Teste]
    public void ContemAsFamiliasAcrescentadasNaFase9()
    {
        (string Funcao, Categoria Categoria)[] funcoes =
        [
            ("GetCursorPos", Categoria.InputGlobal), ("GetLastInputInfo", Categoria.InputGlobal), ("AttachThreadInput", Categoria.InputGlobal),
            ("GetMouseMovePointsEx", Categoria.InputGlobal), ("GetPhysicalCursorPos", Categoria.InputGlobal), ("GetCursorInfo", Categoria.InputGlobal),
            ("RegisterShellHookWindow", Categoria.LerOutrosAplicativos), ("AccessibleObjectFromEvent", Categoria.LerOutrosAplicativos),
            ("GetClipboardSequenceNumber", Categoria.LerOutrosAplicativos), ("GetClipboardOwner", Categoria.LerOutrosAplicativos),
            ("GetProcessImageFileNameW", Categoria.LerOutrosAplicativos), ("NtQueryInformationProcess", Categoria.LerOutrosAplicativos),
            ("GetUserNameW", Categoria.LerOutrosAplicativos), ("GetComputerNameExW", Categoria.LerOutrosAplicativos),
            ("ReadDirectoryChangesW", Categoria.LerOutrosAplicativos), ("ReadDirectoryChangesExW", Categoria.LerOutrosAplicativos),
            ("SHChangeNotifyRegister", Categoria.LerOutrosAplicativos), ("WTSQuerySessionInformationW", Categoria.LerOutrosAplicativos),
            ("CoCreateInstance", Categoria.CodigoDinamico), ("CoGetObject", Categoria.CodigoDinamico),
            ("_wsystem", Categoria.Processos), ("_wpopen", Categoria.Processos), ("CreateProcessInternalW", Categoria.Processos), ("NtCreateUserProcess", Categoria.Processos),
            ("SystemParametersInfoW", Categoria.ConfiguracaoGlobal), ("SetDoubleClickTime", Categoria.ConfiguracaoGlobal), ("timeBeginPeriod", Categoria.ConfiguracaoGlobal),
            ("NtSetTimerResolution", Categoria.ConfiguracaoGlobal), ("SetThreadExecutionState", Categoria.ConfiguracaoGlobal), ("ExitWindowsEx", Categoria.ConfiguracaoGlobal),
            ("LockWorkStation", Categoria.ConfiguracaoGlobal), ("SetSystemTime", Categoria.ConfiguracaoGlobal), ("SwapMouseButton", Categoria.ConfiguracaoGlobal),
            ("RegRestoreKeyW", Categoria.PersistenciaEscondida), ("RegCopyTreeW", Categoria.PersistenciaEscondida), ("RegRenameKey", Categoria.PersistenciaEscondida),
            ("NtSetValueKey", Categoria.PersistenciaEscondida), ("SHDeleteKeyW", Categoria.PersistenciaEscondida), ("SHRegSetValue", Categoria.PersistenciaEscondida),
            ("ChangeServiceConfigW", Categoria.PersistenciaEscondida),
        ];
        foreach ((string funcao, Categoria categoria) in funcoes)
            Afirmar.Igual(categoria, ListaProibida.ProcurarNativa("qualquer.dll", funcao)?.Categoria, funcao);
        foreach (string modulo in new[] { "iphlpapi", "dnsapi", "netapi32", "mpr", "urlmon" })
            Afirmar.Igual(Categoria.Rede, ListaProibida.ProcurarNativa(modulo + ".dll", "QualquerFuncao")?.Categoria, modulo);
        foreach ((string ns, string nome, Categoria categoria) in new[]
        {
            ("System.IO", "FileSystemWatcher", Categoria.LerOutrosAplicativos), ("System.Windows", "DataObject", Categoria.LerOutrosAplicativos),
            ("System.Windows", "DragDrop", Categoria.LerOutrosAplicativos), ("System.Windows.Controls", "WebBrowser", Categoria.Rede),
            ("System.Windows.Navigation", "NavigationWindow", Categoria.Rede), ("System.IO.Pipes", "NamedPipeClientStream", Categoria.Rede),
            ("System.Net.Security", "SslStream", Categoria.Rede), ("System.Management", "ManagementClass", Categoria.Processos),
            ("Microsoft.VisualBasic", "Interaction", Categoria.Processos), ("Windows.Web.Http", "HttpClient", Categoria.Rede),
        })
            Afirmar.Igual(categoria, ListaProibida.ProcurarTipo(ns, nome)?.Categoria, $"{ns}.{nome}");
        foreach ((string tipo, string membro, Categoria categoria) in new[]
        {
            ("System.Environment", "get_UserName", Categoria.LerOutrosAplicativos), ("System.Environment", "get_MachineName", Categoria.LerOutrosAplicativos),
            ("System.Type", "GetTypeFromProgID", Categoria.CodigoDinamico), ("System.Type", "InvokeMember", Categoria.CodigoDinamico),
            ("System.Runtime.InteropServices.Marshal", "BindToMoniker", Categoria.CodigoDinamico),
        })
            Afirmar.Igual(categoria, ListaProibida.ProcurarMembro(tipo, membro)?.Categoria, $"{tipo}.{membro}");
        foreach (string metodo in new[] { "ElementFromHandle", "ElementFromPoint" })
            Afirmar.Igual(Categoria.LerOutrosAplicativos, ListaProibida.ProcurarMetodoCom(metodo)?.Categoria, metodo);
        // O FocusManager do WPF não é UIA de outro aplicativo: nada com esse nome é proibido.
        Afirmar.Nulo(ListaProibida.ProcurarMetodoCom("GetFocusedElement"));
        Afirmar.Nulo(ListaProibida.ProcurarMembro("System.Windows.Input.FocusManager", "GetFocusedElement"));
    }

    [Teste]
    public void ModuloIgnoraMaiusculasCaminhoEExtensao()
    {
        string[] grafias = ["ws2_32", "WS2_32.DLL", "ws2_32.dll", @"C:\Windows\System32\ws2_32.dll", "ws2_32.", "C:/Windows/System32/Ws2_32.Dll"];
        foreach (string modulo in grafias)
            Afirmar.Igual("ws2_32", ListaProibida.ProcurarNativa(modulo, "connect")?.Alvo, modulo);

        Afirmar.Igual("ws2_32", ListaProibida.NormalizarModulo(@"C:\Windows\System32\WS2_32.DLL"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("ws2_32x.dll", "connect"));
        // Desde a DEC-040, item 6, o urlmon inteiro é proibido (antes, só as funções de download).
        Afirmar.Igual("urlmon", ListaProibida.ProcurarNativa("urlmon.dll", "CoInternetParseUrl")?.Alvo, "o urlmon inteiro");
        Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", "SetWindowPos"));
    }

    [Teste]
    public void UsosRestritosApontamParaFuncoesProibidasEParaOObservador()
    {
        // DEC-034 e DEC-038: a permissão só faz sentido para uma função que a lista proíbe; o observador usa o nome exato da
        // regra, e o adaptador do início, a variante W exata da entrada no binário; e o arquivo dela existe e declara o tipo.
        Afirmar.Sequencia<string>(["SetWinEventHook", "GetForegroundWindow", "GetWindowThreadProcessId", "RegSetValueExW", "RegDeleteValueW"], UsosRestritos.Entradas.Select(u => u.Funcao));
        foreach (UsoRestrito uso in UsosRestritos.Entradas)
        {
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa(uso.Modulo, uso.Funcao), uso.Funcao);
            bool doInicio = uso.Tipo == "Buzzy.App.Plataforma.InicioComOWindows";
            Afirmar.Igual(doInicio ? uso.Funcao[..^1] : uso.Funcao, regra.Alvo, uso.Funcao);
            Afirmar.Igual(doInicio ? Categoria.PersistenciaEscondida : Categoria.LerOutrosAplicativos, regra.Categoria, uso.Funcao);
            Afirmar.Igual(doInicio ? "advapi32" : "user32", uso.Modulo, uso.Funcao);
            Afirmar.Igual("Buzzy", uso.Assembly, uso.Funcao);
            Afirmar.Verdadeiro(uso.Motivo.Contains(doInicio ? "DEC-038" : "DEC-034", StringComparison.Ordinal), uso.Funcao);

            string arquivo = Path.Combine(Repositorio.Raiz(), uso.Arquivo);
            Afirmar.Verdadeiro(File.Exists(arquivo), $"arquivo do uso restrito não existe: {arquivo}");
            int ponto = uso.Tipo.LastIndexOf('.');
            string texto = File.ReadAllText(arquivo);
            Afirmar.Contem($"namespace {uso.Tipo[..ponto]};", texto, uso.Arquivo);
            Afirmar.Contem($"class {uso.Tipo[(ponto + 1)..]}", texto, uso.Arquivo);
        }
    }

    // DEC-038, item 11: apagar ou gravar no registro por outros nomes e a pasta Inicializar ficam proibidos; a chave Run
    // continua proibida no resto do produto (RegSetValueEx, RegCreateKeyEx, Registry).
    [Teste]
    public void ContemAsFuncoesDaPersistenciaAcrescentadasNaFase8()
    {
        foreach (string nome in new[] { "RegDeleteValue", "RegDeleteKey", "RegDeleteKeyEx", "RegDeleteKeyValue", "RegDeleteTree", "SHSetValue", "SHRegSetUSValue", "RegSetValueEx", "RegCreateKeyEx" })
        {
            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa("advapi32.dll", nome), nome);
            Afirmar.Igual((TipoDeRegra.FuncaoNativa, Categoria.PersistenciaEscondida), (regra.Tipo, regra.Categoria), nome);
            Afirmar.Igual(regra, ListaProibida.ProcurarNativa("advapi32.dll", nome + "W"), $"{nome}W, a mesma regra");
        }
        Regra startup = Afirmar.NaoNulo(ListaProibida.Regras.FirstOrDefault(r => r.PadroesNaFonte.Contains("SpecialFolder.Startup")), "a pasta Inicializar");
        Afirmar.Igual(Categoria.PersistenciaEscondida, startup.Categoria);
        Afirmar.Verdadeiro(startup.PadroesNaFonte.Contains("SpecialFolder.CommonStartup"), "e a de todos os usuários");
    }

    // DEC-038, item 11: o uso restrito do início vale no binário só para a entrada W exata, no tipo (ou aninhado) e no
    // assembly certos; na fonte, para a família da função, só no arquivo do adaptador.
    [Teste]
    public void UsoRestritoDoInicio_SoAEntradaW_NoTipoENoArquivoDele()
    {
        const string tipo = "Buzzy.App.Plataforma.InicioComOWindows";
        foreach (string funcao in new[] { "RegSetValueExW", "RegDeleteValueW" })
        {
            Afirmar.NaoNulo(UsosRestritos.NoBinario("Buzzy", "advapi32.dll", funcao, tipo), $"{funcao} no tipo");
            Afirmar.NaoNulo(UsosRestritos.NoBinario("Buzzy", "ADVAPI32", funcao, tipo + "+Nativo"), $"{funcao} no aninhado");
            Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy", "advapi32.dll", funcao[..^1] + "A", tipo), $"{funcao}: a variante A");
            Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy", "advapi32.dll", funcao[..^1], tipo), $"{funcao}: sem sufixo");
            Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy", "advapi32.dll", funcao, "Buzzy.App.Plataforma.Win32"), $"{funcao}: outro tipo");
            Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy.Visual", "advapi32.dll", funcao, tipo), $"{funcao}: outro assembly");
            Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy", "kernel32.dll", funcao, tipo), $"{funcao}: outro módulo");

            Regra regra = Afirmar.NaoNulo(ListaProibida.ProcurarNativa("advapi32.dll", funcao));
            Afirmar.NaoNulo(UsosRestritos.NaFonte(@"C:\repo\src\Buzzy.App\Plataforma\InicioComOWindows.cs", regra), $"{funcao}: no arquivo do adaptador");
            Afirmar.NaoNulo(UsosRestritos.NaFonte("C:/repo/SRC/buzzy.app/plataforma/iniciocomowindows.cs", regra), $"{funcao}: sem diferenciar maiúsculas nem a barra");
            Afirmar.Nulo(UsosRestritos.NaFonte(@"C:\repo\src\Buzzy.App\Plataforma\Win32.cs", regra), $"{funcao}: em outro arquivo");
            Afirmar.Nulo(UsosRestritos.NaFonte(@"C:\repo\src\Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs", regra), $"{funcao}: no arquivo do observador");
        }
        // As outras funções do registro continuam reprovando no arquivo do adaptador.
        foreach (string outra in new[] { "RegCreateKeyEx", "RegDeleteKey", "RegSetKeyValue", "SHSetValue" })
            Afirmar.Nulo(UsosRestritos.NaFonte(@"C:\repo\src\Buzzy.App\Plataforma\InicioComOWindows.cs", Afirmar.NaoNulo(ListaProibida.ProcurarNativa("advapi32.dll", outra))), $"{outra} no arquivo do adaptador");
        Afirmar.Nulo(UsosRestritos.NoBinario("Buzzy", "advapi32.dll", "RegCreateKeyExW", tipo), "RegCreateKeyExW no tipo do adaptador");
    }

    [Teste]
    public void ExcecoesDocumentadasNaoSaoProibidas()
    {
        // Funções do adaptador de plataforma.
        string[] doAdaptador =
        [
            "SetWindowPos", "GetWindowLongPtrW", "SetWindowLongPtrW", "MonitorFromPoint", "MonitorFromWindow",
            "GetMonitorInfoW", "EnumDisplayMonitors", "SetForegroundWindow", "GetWindowRect", "SetCapture", "ReleaseCapture",
            "GetSystemMetrics", "GetDpiForWindow", "CreateWindowExW", "DefWindowProcW", "RegisterWindowMessageW",
        ];
        foreach (string funcao in doAdaptador)
            Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", funcao), funcao);
        // Configuração de vídeo, só leitura, para a chave estável do monitor (DEC-030; ARCHITECTURE.md 2.13.3).
        foreach (string funcao in new[] { "GetDisplayConfigBufferSizes", "QueryDisplayConfig", "DisplayConfigGetDeviceInfo" })
            Afirmar.Nulo(ListaProibida.ProcurarNativa("user32.dll", funcao), funcao);
        Afirmar.Nulo(ListaProibida.ProcurarNativa("shell32.dll", "Shell_NotifyIconW"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("shcore.dll", "GetDpiForMonitor"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("kernel32.dll", "GetModuleFileNameW"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("kernel32.dll", "GetCurrentProcess"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("advapi32.dll", "RegOpenKeyExW"));
        Afirmar.Nulo(ListaProibida.ProcurarNativa("advapi32.dll", "RegGetValueW"));

        // Process: o tipo e a leitura do próprio processo.
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Diagnostics", "Process"));
        string[] doProprioProcesso = ["GetCurrentProcess", "get_Id", "get_StartTime", "get_WorkingSet64", "Kill", "Dispose"];
        foreach (string membro in doProprioProcesso)
            Afirmar.Nulo(ListaProibida.ProcurarMembro("System.Diagnostics.Process", membro), membro);

        // Acessibilidade do WPF e eventos de sistema.
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Windows.Automation", "AutomationProperties"));
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Windows.Automation.Peers", "UIElementAutomationPeer"));
        Afirmar.Nulo(ListaProibida.ProcurarTipo("Microsoft.Win32", "SystemEvents"));
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Reflection", "Assembly"));
        Afirmar.Nulo(ListaProibida.ProcurarMembro("System.Reflection.Assembly", "GetExecutingAssembly"));
        Afirmar.Nulo(ListaProibida.ProcurarMembro("System.Runtime.InteropServices.Marshal", "SizeOf"));

        // Namespace parecido não conta: System.Net.HttpListener não é System.Net.Http.
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Net.HttpExtras", "Qualquer"));
        Afirmar.Nulo(ListaProibida.ProcurarTipo("System.Net", "IPAddress"));
    }
}
