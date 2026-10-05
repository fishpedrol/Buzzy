using System.IO;
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
    private static readonly string Src = Path.Combine(Caminhos.Raiz, "src");

    private static IEnumerable<string> FontesDoProduto() => Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories)
        .Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"));

    private static string SemComentarios(string codigo) => Regex.Replace(codigo, @"//.*|/\*[\s\S]*?\*/", "");

    private static string Relativo(string f) => Path.GetRelativePath(Src, f);

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
        var declaracao = new Regex(@"\[(?:DllImport|LibraryImport)\(\s*""([^""]+)""([^\]]*)\][\s\S]*?(?:extern|partial)\s+[\w<>\[\]?]+\s+(\w+)\s*\(");
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
        // Nenhuma outra forma de chamar código nativo por nome.
        string[] outros = [.. FontesDoProduto().Where(f => Regex.IsMatch(SemComentarios(File.ReadAllText(f)), @"\bdelegate\s+unmanaged\b|\bfunction pointer\b|\bUnmanagedCallersOnly\b|\bDllImportResolver\b")).Select(Relativo)];
        Afirmar.Sequencia([], outros, "ponteiros de função nativos e resolvedores de DLL");
    }

    // L16: os argumentos dos dois ganchos fixos: o evento único, fora de contexto, sem módulo, sem processo, pulando o
    // próprio; e as três constantes com os valores do Windows.
    [Teste]
    public void SetWinEventHook_ArgumentosFixos()
    {
        string codigo = SemComentarios(File.ReadAllText(Path.Combine(Src, "Buzzy.App", "Plataforma", "ObservadorDeTelaCheia.cs")));
        string[] chamadas = [.. Regex.Matches(codigo, @"Nativo\.SetWinEventHook\(([^;]*)\);").Select(m => Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim())];
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
        var reflexao = new Regex(@"\bdynamic\b|\.GetMethod\s*\(|(typeof\([^)]*\)|GetType\(\))\.Get(Property|Field|Member|Methods|Properties|Fields)\s*\(|\.GetField\s*\(|\bType\.GetType\s*\(|\bActivator\.CreateInstance\b|\bMethodInfo\b|\bInvokeMember\b|\bAssembly\.Load");
        string[] quem = [.. FontesDoProduto().Where(f => reflexao.IsMatch(SemComentarios(File.ReadAllText(f)))).Select(Relativo)];
        Afirmar.Sequencia([], quem, "reflexão por texto ou dynamic");
    }
}
