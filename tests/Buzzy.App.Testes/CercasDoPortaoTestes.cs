using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 9, passo F9-P2 (DEC-040, item 6): as cercas de fonte que completam o portão de APIs. A lista fechada dos P/Invoke
/// do produto (qualquer API nativa nova exige mudar este teste, de propósito); os argumentos do <c>SetWinEventHook</c>
/// fixos; o registro aberto só no adaptador do início; as ações sobre janelas só com HWNDs do próprio Buzzy; nenhum
/// endereço de rede nem UNC; e nada de reflexão por texto nem <c>dynamic</c>.
/// </summary>
internal sealed class CercasDoPortaoTestes
{
    private static readonly string Src = FonteDoProduto.Src;

    private static IEnumerable<string> FontesDoProduto() => FonteDoProduto.Arquivos();

    private static string SemComentarios(string codigo) => FonteDoProduto.SemComentarios(codigo);

    private static string Relativo(string f) => FonteDoProduto.Relativo(f);

    /// <summary>Os métodos P/Invoke dos três assemblies do produto, pelos metadados compilados (módulo!entrada).</summary>
    private static List<string> PInvokeNosMetadados()
    {
        var vistos = new List<string>();
        foreach (Assembly a in new[] { typeof(CodigosDeSaida).Assembly, typeof(Buzzy.Core.Topologia).Assembly, typeof(Buzzy.Visual.Animacao.ManifestoDeClipes).Assembly })
        {
            foreach (Type t in a.GetTypes())
            {
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if ((m.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
                    DllImportAttribute? d = m.GetCustomAttribute<DllImportAttribute>();
                    vistos.Add($"{d?.Value.ToLowerInvariant() ?? "?"}!{(string.IsNullOrEmpty(d?.EntryPoint) ? m.Name : d.EntryPoint)}");
                }
            }
        }
        return vistos;
    }

    // Todas as declarações P/Invoke do produto, por arquivo, módulo e ponto de entrada. Uma API nativa nova (ou uma que
    // muda de arquivo) reprova aqui e pede a revisão de segurança: o portão proíbe por lista, e esta cerca fecha o resto.
    private static readonly string[] PInvokeEsperados =
    [
        @"Buzzy.App\Plataforma\InicioComOWindows.cs|advapi32.dll!RegCloseKey",
        @"Buzzy.App\Plataforma\InicioComOWindows.cs|advapi32.dll!RegDeleteValueW",
        @"Buzzy.App\Plataforma\InicioComOWindows.cs|advapi32.dll!RegGetValueW",
        @"Buzzy.App\Plataforma\InicioComOWindows.cs|advapi32.dll!RegOpenKeyExW",
        @"Buzzy.App\Plataforma\InicioComOWindows.cs|advapi32.dll!RegSetValueExW",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|kernel32.dll!GetCurrentThreadId",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|shell32.dll!SHQueryUserNotificationState",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|user32.dll!GetForegroundWindow",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|user32.dll!GetWindowThreadProcessId",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|user32.dll!SetWinEventHook",
        @"Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs|user32.dll!UnhookWinEvent",
        @"Buzzy.App\Plataforma\Win32.cs|gdi32.dll!CreateDIBSection",
        @"Buzzy.App\Plataforma\Win32.cs|gdi32.dll!DeleteObject",
        @"Buzzy.App\Plataforma\Win32.cs|shcore.dll!GetDpiForMonitor",
        @"Buzzy.App\Plataforma\Win32.cs|shell32.dll!Shell_NotifyIconGetRect",
        @"Buzzy.App\Plataforma\Win32.cs|shell32.dll!Shell_NotifyIconW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!ClientToScreen",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!CreateIconFromResourceEx",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!CreatePopupMenu",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!DestroyIcon",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!DestroyMenu",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!DisplayConfigGetDeviceInfo",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!DisplayConfigGetDeviceInfo",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!EndMenu",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!EnumDisplayMonitors",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetDisplayConfigBufferSizes",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetDoubleClickTime",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetMonitorInfoW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetSystemMetricsForDpi",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetWindowLongPtrW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!GetWindowRect",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!InsertMenuItemW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!PostMessageW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!QueryDisplayConfig",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!RegisterWindowMessageW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!ReleaseCapture",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!SetCapture",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!SetForegroundWindow",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!SetWindowLongPtrW",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!SetWindowPos",
        @"Buzzy.App\Plataforma\Win32.cs|user32.dll!TrackPopupMenuEx",
        @"Buzzy.App\Plataforma\Win32.cs|wtsapi32.dll!WTSRegisterSessionNotification",
        @"Buzzy.App\Plataforma\Win32.cs|wtsapi32.dll!WTSUnRegisterSessionNotification",
    ];

    [Teste]
    public void PInvoke_ListaFechada()
    {
        // Qualquer grafia do atributo (com o sufixo Attribute, com o nome qualificado, junto de outros atributos).
        var declaracao = new Regex(@"\[[^\]]*?\b(?:DllImport|LibraryImport)(?:Attribute)?\s*\(\s*""([^""]+)""([^\]]*)\][\s\S]*?(?:extern|partial)\s+[\w<>\[\]?]+\s+(\w+)\s*\(");
        var vistos = new List<string>();
        foreach (string f in FontesDoProduto())
        {
            foreach (Match m in declaracao.Matches(SemComentarios(File.ReadAllText(f))))
            {
                Match ponto = Regex.Match(m.Groups[2].Value, @"EntryPoint\s*=\s*""([^""]+)""");
                vistos.Add($"{Relativo(f)}|{m.Groups[1].Value.ToLowerInvariant()}!{(ponto.Success ? ponto.Groups[1].Value : m.Groups[3].Value)}");
            }
        }
        Afirmar.Sequencia(PInvokeEsperados, vistos.Order(StringComparer.Ordinal), "os P/Invoke do produto (uma API nova pede revisão de segurança)");
        // Os metadados compilados fecham a mesma lista, qualquer que seja a grafia na fonte.
        Afirmar.Sequencia(PInvokeEsperados.Select(e => e[(e.IndexOf('|') + 1)..]).Order(StringComparer.Ordinal), PInvokeNosMetadados().Order(StringComparer.Ordinal), "os P/Invoke nos metadados do App, do Core e do Visual");
        // Nenhuma outra forma de chamar código nativo: ponteiros de função, resolvedores de DLL e COM declarado à mão.
        string[] outros = [.. FontesDoProduto().Where(f => Regex.IsMatch(SemComentarios(File.ReadAllText(f)), @"\bdelegate\s*\*|\bUnmanagedCallersOnly\b|\bDllImportResolver\b|\bcalli\b|\bComImport\b|\bCoClass\b|\bGeneratedComInterface\b|\bComWrappers\b|\bComVisible\s*\(\s*true")).Select(Relativo)];
        Afirmar.Sequencia([], outros, "ponteiros de função, resolvedores de DLL e COM declarado à mão");
    }

    // DEC-043: o Buzzy roda no Windows 10 (versão 1607, build 14393) e no Windows 11. A versão do Windows em que cada
    // P/Invoke do produto entrou, pela documentação da Microsoft; nenhuma pode ser mais nova que o mínimo, e a mais nova
    // é a que o define (GetSystemMetricsForDpi, do 1607, o mesmo mínimo do .NET 10). Uma API nova entra aqui junto com a
    // lista acima. Windows 2000 = 5.0, XP = 5.1, Vista = 6.0, 7 = 6.1, 8.1 = 6.3, 10 1607 = 10.0.14393.
    private static readonly Version WindowsMinimo = new(10, 0, 14393);

    private static readonly Dictionary<string, (Version Desde, string Windows)> VersaoMinimaDoWindows = new(StringComparer.Ordinal)
    {
        ["advapi32.dll!RegCloseKey"] = (new(5, 0), "Windows 2000"),
        ["advapi32.dll!RegDeleteValueW"] = (new(5, 0), "Windows 2000"),
        ["advapi32.dll!RegGetValueW"] = (new(6, 0), "Windows Vista"),
        ["advapi32.dll!RegOpenKeyExW"] = (new(5, 0), "Windows 2000"),
        ["advapi32.dll!RegSetValueExW"] = (new(5, 0), "Windows 2000"),
        ["gdi32.dll!CreateDIBSection"] = (new(5, 0), "Windows 2000"),
        ["gdi32.dll!DeleteObject"] = (new(5, 0), "Windows 2000"),
        ["kernel32.dll!GetCurrentThreadId"] = (new(5, 0), "Windows 2000"),
        ["shcore.dll!GetDpiForMonitor"] = (new(6, 3), "Windows 8.1"),
        ["shell32.dll!SHQueryUserNotificationState"] = (new(6, 0), "Windows Vista"),
        ["shell32.dll!Shell_NotifyIconGetRect"] = (new(6, 1), "Windows 7"),
        ["shell32.dll!Shell_NotifyIconW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!ClientToScreen"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!CreateIconFromResourceEx"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!CreatePopupMenu"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!DestroyIcon"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!DestroyMenu"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!DisplayConfigGetDeviceInfo"] = (new(6, 1), "Windows 7"),
        ["user32.dll!EndMenu"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!EnumDisplayMonitors"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetDisplayConfigBufferSizes"] = (new(6, 1), "Windows 7"),
        ["user32.dll!GetDoubleClickTime"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetForegroundWindow"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetMonitorInfoW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetSystemMetricsForDpi"] = (new(10, 0, 14393), "Windows 10 1607"),
        ["user32.dll!GetWindowLongPtrW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetWindowRect"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!GetWindowThreadProcessId"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!InsertMenuItemW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!PostMessageW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!QueryDisplayConfig"] = (new(6, 1), "Windows 7"),
        ["user32.dll!RegisterWindowMessageW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!ReleaseCapture"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!SetCapture"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!SetForegroundWindow"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!SetWinEventHook"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!SetWindowLongPtrW"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!SetWindowPos"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!TrackPopupMenuEx"] = (new(5, 0), "Windows 2000"),
        ["user32.dll!UnhookWinEvent"] = (new(5, 0), "Windows 2000"),
        ["wtsapi32.dll!WTSRegisterSessionNotification"] = (new(5, 1), "Windows XP"),
        ["wtsapi32.dll!WTSUnRegisterSessionNotification"] = (new(5, 1), "Windows XP"),
    };

    [Teste]
    public void PInvoke_VersaoMinimaDoWindows()
    {
        string[] doProduto = [.. PInvokeEsperados.Select(e => e[(e.IndexOf('|') + 1)..]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(doProduto, VersaoMinimaDoWindows.Keys.Order(StringComparer.Ordinal), "cada P/Invoke do produto tem a versão do Windows em que entrou (DEC-043)");
        foreach ((string api, (Version desde, string windows)) in VersaoMinimaDoWindows)
            Afirmar.Verdadeiro(desde <= WindowsMinimo, $"{api} exige {windows} ({desde}), mais novo que o mínimo do Buzzy, o Windows 10 1607 ({WindowsMinimo})");
        Afirmar.Igual(WindowsMinimo, VersaoMinimaDoWindows.Values.Max(v => v.Desde), "o mínimo declarado é o da função mais nova");
        // O manifesto declara o Windows 10 e 11 (um GUID para os dois; o portão também exige).
        Afirmar.Contem("<supportedOS Id=\"{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}\" />", File.ReadAllText(Path.Combine(Src, "Buzzy.App", "app.manifest")));
    }

    // L16: os argumentos dos dois ganchos fixos: o evento único, fora de contexto, sem módulo, sem processo, pulando o
    // próprio; e as três constantes com os valores do Windows.
    [Teste]
    public void SetWinEventHook_ArgumentosFixos()
    {
        string codigo = SemComentarios(File.ReadAllText(Path.Combine(Src, "Buzzy.App", "Plataforma", "ObservadorDeTelaCheia.cs")));
        string[] chamadas = [.. Regex.Matches(codigo, @"\bNativo\s*\.\s*SetWinEventHook\s*\(([^;]*)\);").Select(m => Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim())];
        // A palavra aparece exatamente três vezes: a declaração e as duas chamadas (nenhuma outra forma de chamar).
        Afirmar.Igual(3, Regex.Matches(codigo, @"\bSetWinEventHook\b").Count, "a declaração e as duas chamadas");
        Afirmar.Sequencia(
        [
            "EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _aoEvento, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS",
            "EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, 0, _aoEvento, 0, thread, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS",
        ], chamadas, "as duas assinaturas");
        foreach (string constante in new[] { "EVENT_SYSTEM_FOREGROUND = 0x0003;", "EVENT_OBJECT_LOCATIONCHANGE = 0x800B;", "WINEVENT_OUTOFCONTEXT = 0x0000;", "WINEVENT_SKIPOWNPROCESS = 0x0002;" })
            Afirmar.Contem(constante, codigo);
    }

    // L23: o registro só é aberto, e só com acesso de escrita, no adaptador do início.
    [Teste]
    public void Registro_AbertoSoNoAdaptadorDoInicio()
    {
        var abertura = new Regex(@"\bRegOpenKey|\bRegCreateKey|\bKEY_SET_VALUE\b|\bKEY_WRITE\b|\bKEY_ALL_ACCESS\b|\bKEY_CREATE_SUB_KEY\b|\bHKEY_(CURRENT_USER|LOCAL_MACHINE|USERS|CLASSES_ROOT)\b");
        string[] quem = [.. FontesDoProduto().Where(f => abertura.IsMatch(SemComentarios(File.ReadAllText(f)))).Select(Relativo)];
        Afirmar.Sequencia([@"Buzzy.App\Plataforma\InicioComOWindows.cs"], quem, "quem abre o registro");
    }

    // L26: as ações sobre janelas só recebem HWNDs do próprio Buzzy; nenhum HWND_BROADCAST.
    [Teste]
    public void AcoesSobreJanelas_SoComHwndsProprios()
    {
        string[] permitidos = ["Hwnd", "hwnd", "dono.Handle", "ajudante.Handle", "painel.Hwnd", "janela.Hwnd"];
        var acao = new Regex(@"Win32\.(SetWindowPos|PostMessage|SetWindowLongPtr|SetForegroundWindow|AoTopoDaFaixaComum)\(\s*([^,)]+)");
        int vistas = 0;
        foreach (string f in FontesDoProduto())
        {
            string codigo = SemComentarios(File.ReadAllText(f));
            Afirmar.Falso(Regex.IsMatch(codigo, @"HWND_BROADCAST|\(nint\)\s*0xFFFF\b|\(IntPtr\)\s*0xFFFF\b"), $"{Relativo(f)}: HWND_BROADCAST");
            foreach (Match m in acao.Matches(codigo))
            {
                vistas++;
                Afirmar.Verdadeiro(permitidos.Contains(m.Groups[2].Value.Trim()), $"{Relativo(f)}: Win32.{m.Groups[1].Value}({m.Groups[2].Value}): HWND que não é do Buzzy");
            }
        }
        Afirmar.Verdadeiro(vistas >= 10, $"as ações vistas: {vistas}");
    }

    // L22: nenhum endereço de rede nem caminho UNC no produto; nenhum URI montado nem imagem por URI.
    [Teste]
    public void SemEnderecosDeRede()
    {
        // O removedor de comentários não pode engolir um endereço dentro de um texto.
        Afirmar.Contem("https://exemplo", SemComentarios("var u = \"https://exemplo\"; // comentário"));
        Afirmar.Contem(@"\\servidor", SemComentarios(@"var p = @""\\servidor\x""; /* c */"));
        foreach (string f in FontesDoProduto())
        {
            string codigo = SemComentarios(File.ReadAllText(f));
            // O namespace do SVG é um identificador, não um endereço consultado (a arte é lida da memória).
            codigo = codigo.Replace("\"http://www.w3.org/2000/svg\"", "", StringComparison.Ordinal);
            Afirmar.Falso(Regex.IsMatch(codigo, @"(?i)\b(https?|ftp|wss?|file)://|""\\\\\\\\[A-Za-z0-9]|@""\\\\[A-Za-z0-9]"), $"{Relativo(f)}: endereço de rede ou UNC");
            Afirmar.Falso(Regex.IsMatch(codigo, @"\bnew\s+Uri\s*\(|\bUriSource\b|\bNavigateUri\b|\bBaseUri\b"), $"{Relativo(f)}: URI montado");
        }
    }

    // L19: nada de reflexão por texto nem de dynamic no produto.
    [Teste]
    public void SemReflexaoPorTexto()
    {
        // (o GetProperty do JsonElement, que lê um campo de JSON, não é reflexão: só o de um Type conta)
        var reflexao = new Regex(@"\bdynamic\b|\.(GetMethods?|GetRuntimeMethods?|GetDeclaredMethods?|GetMembers?|GetRuntimeProperties|GetFields?|GetConstructors?)\s*\(|(typeof\([^)]*\)|GetType\(\))\.Get(Property|Properties)\s*\(|\.GetType\s*\(\s*[""$@]|\bType\.GetType\s*\(|\bActivator\.CreateInstance\b|\bMethod(Info|Base)\b|\bInvokeMember\b|\bAssembly\.Load|\bExpression\.Lambda\b");
        string[] quem = [.. FontesDoProduto().Where(f => reflexao.IsMatch(SemComentarios(File.ReadAllText(f)))).Select(Relativo)];
        Afirmar.Sequencia([], quem, "reflexão por texto ou dynamic");
    }
}
