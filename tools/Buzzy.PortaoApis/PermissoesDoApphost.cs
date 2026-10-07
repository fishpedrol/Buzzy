namespace Buzzy.PortaoApis;

// Modulo normalizado; Funcao exata, com o sufixo A/W que o lançador usa. O Motivo vai pro relatório.
internal sealed record PermissaoDoApphost(string Modulo, string Funcao, string Motivo);

// Exceções explícitas pro Buzzy.exe. Ele não é código nosso: é o apphost genérico que o SDK
// copia do Microsoft.NETCore.App.Host.win-x64, só com o nome do Buzzy.dll, subsistema e recursos.
// Ele acha o runtime, carrega o hostfxr.dll e passa a execução pro Buzzy.dll.
//
// O apphost do SDK 10.0.401 (host 10.0.12) importa SHELL32 (1), ADVAPI32 (6), KERNEL32 (58),
// USER32 (1) e o CRT, sem ordinal nem carga atrasada. Só as quatro abaixo batem com a lista
// proibida; os nomes nos motivos saíram das strings do próprio apphost.
//
// Vale só pro <aplicativo>.exe nativo (sem cabeçalho CLI) da pasta de binários, nunca pras DLLs
// ou um Buzzy.exe gerenciado. Casa módulo e função exatos, sem variantes A/W. Se um SDK novo
// trouxer outra importação proibida, o portão reprova até alguém revisar e acrescentar aqui.
internal static class PermissoesDoApphost
{
    public static readonly IReadOnlyList<PermissaoDoApphost> Entradas =
    [
        new("kernel32", "LoadLibraryExW",
            "o lançador carrega hostfxr.dll da instalação do .NET que encontrou (caminho relativo ao aplicativo, DOTNET_ROOT, registro InstallLocation ou pasta padrão), e comctl32.dll e kernel32.dll para funções opcionais"),
        new("kernel32", "LoadLibraryA",
            "variante ANSI usada pelo lançador; o único nome de DLL em ANSI nas cadeias do apphost é ntdll.dll, junto de RtlGetVersion (versão do Windows)"),
        new("kernel32", "GetProcAddress",
            "o lançador obtém os pontos de entrada de hostfxr (hostfxr_main_bundle_startupinfo, hostfxr_main_startupinfo, hostfxr_main, hostfxr_set_error_writer) e funções opcionais do sistema (TaskDialogIndirect, GetTempPath2W, IsWow64Process2, RtlGetVersion)"),
        new("shell32", "ShellExecuteW",
            "quando falta o runtime, o lançador pergunta se o usuário quer baixar o .NET e, só com a resposta sim, abre a página de download (https://aka.ms/dotnet-core-applaunch) no navegador"),
    ];

    public static PermissaoDoApphost? Procurar(string modulo, string funcao)
    {
        string moduloNormalizado = ListaProibida.NormalizarModulo(modulo);
        return Entradas.FirstOrDefault(p =>
            string.Equals(p.Modulo, moduloNormalizado, StringComparison.Ordinal)
            && string.Equals(p.Funcao, funcao, StringComparison.Ordinal));
    }
}
