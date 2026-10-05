namespace Buzzy.PortaoApis;

/// <summary>Importação nativa do lançador que coincide com a lista proibida e é permitida no apphost.</summary>
/// <param name="Modulo">Módulo normalizado (<see cref="ListaProibida.NormalizarModulo"/>).</param>
/// <param name="Funcao">Nome exato da função importada, com o sufixo A ou W observado.</param>
/// <param name="Motivo">O que o lançador faz com ela; aparece no relatório.</param>
internal sealed record PermissaoDoApphost(string Modulo, string Funcao, string Motivo);

/// <summary>
/// Lista de permissões EXPLÍCITA do Buzzy.exe.
///
/// O Buzzy.exe não é código do Buzzy (SECURITY.md 4): é o apphost, o lançador nativo genérico
/// que o SDK do .NET copia do pacote Microsoft.NETCore.App.Host.win-x64
/// (runtimes/win-x64/native/apphost.exe), gravando nele só o nome do Buzzy.dll, o subsistema e
/// os recursos Win32 do aplicativo. Ele acha o runtime instalado, carrega hostfxr.dll e entrega
/// a execução ao Buzzy.dll, onde fica o código do produto.
///
/// Levantamento de 2026-09-29, feito lendo a tabela de importação com o próprio portão:
/// o apphost do SDK 10.0.401 (pacote de host 10.0.12), tanto em
/// spikes/BuzzySpike/bin/Release/net10.0-windows/BuzzySpike.exe quanto no apphost.exe do pacote,
/// importa as mesmas funções: SHELL32 (1), ADVAPI32 (6), KERNEL32 (58), USER32 (1) e o runtime
/// C (api-ms-win-crt-*), sem importação por ordinal nem com carga atrasada. Só as quatro abaixo
/// coincidem com a lista proibida. Os nomes citados nos motivos foram vistos nas cadeias de texto
/// do próprio apphost.
///
/// Regras de uso:
/// - vale só para o arquivo &lt;aplicativo&gt;.exe da pasta de binários, e só quando ele é
///   nativo (sem cabeçalho CLI); nunca para Buzzy.dll, Buzzy.*.dll ou um Buzzy.exe gerenciado;
/// - casa módulo e função exatos, sem as variantes A/W que a lista proibida aceita;
/// - cada uso aparece no relatório como "permitida no apphost";
/// - se um SDK novo trouxer outra importação que coincida com a lista proibida, o portão reprova
///   o build até alguém revisar o lançador e acrescentar a entrada aqui, com o motivo.
/// </summary>
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

    /// <summary>Permissão para esta importação exata, ou nulo.</summary>
    public static PermissaoDoApphost? Procurar(string modulo, string funcao)
    {
        string moduloNormalizado = ListaProibida.NormalizarModulo(modulo);
        return Entradas.FirstOrDefault(p =>
            string.Equals(p.Modulo, moduloNormalizado, StringComparison.Ordinal)
            && string.Equals(p.Funcao, funcao, StringComparison.Ordinal));
    }
}
