namespace Buzzy.PortaoApis;

/// <summary>
/// Lista de permissões EXPLÍCITA do Buzzy.exe de arquivo único (F9-P10, DEC-042; revisão da DEC-016).
///
/// No .exe único autocontido, o Buzzy.exe é o singlefilehost.exe do pacote Microsoft.NETCore.App.Host.win-x64: o
/// lançador (o mesmo código do apphost), o hostfxr, o hostpolicy e o runtime CoreCLR, ligados estaticamente num só
/// PE. No build dependente do framework, essas mesmas importações já existem, só que nas DLLs do runtime instalado
/// (coreclr.dll, hostfxr.dll, hostpolicy.dll), que o portão nunca leu. A <see cref="ProcedenciaDoHost"/> exige que o
/// host seja byte a byte o do pacote da Microsoft (menos a posição do pacote, o nome do aplicativo e os recursos); esta
/// lista registra, com o motivo, cada importação dele que coincide com a lista proibida.
///
/// Levantamento de 2026-10-05, lendo as tabelas de importação (SDK 10.0.401, host 10.0.12): o singlefilehost importa
/// 441 funções; 33 coincidem com a lista proibida. As do lançador são as quatro de <see cref="PermissoesDoApphost"/>;
/// LoadLibraryExA, CreateProcessW e as 27 do OLEAUT32 por ordinal estão todas no coreclr.dll do pacote
/// Microsoft.NETCore.App.Runtime.win-x64 10.0.12 (os nomes dos ordinais saem da tabela de exportação do oleaut32.dll do
/// Windows). Regras de uso: só para o &lt;aplicativo&gt;.exe de um pacote de arquivo único (--pacote), nunca para as DLLs;
/// módulo e função (ou ordinal) exatos; uma importação nova de um SDK novo reprova até alguém revisar e acrescentar aqui.
/// </summary>
internal static class PermissoesDoHostDeArquivoUnico
{
    private const string ComInterop =
        "o runtime CoreCLR embutido (a mesma importação do coreclr.dll do runtime instalado) usa para a interoperabilidade COM: cadeias BSTR, VARIANT, SAFEARRAY, IErrorInfo e bibliotecas de tipos, que o WPF usa na acessibilidade (UIA) e nos serviços de texto; importada por ordinal";

    public static readonly IReadOnlyList<PermissaoDoApphost> Entradas =
    [
        .. PermissoesDoApphost.Entradas,
        new("kernel32", "LoadLibraryExA",
            "o runtime CoreCLR embutido carrega DLLs do sistema por nome ANSI (a mesma importação do coreclr.dll do runtime instalado); o código do Buzzy não a usa, e o portão proíbe carregar DLL no IL do produto"),
        new("kernel32", "CreateProcessW",
            "o runtime CoreCLR embutido só inicia o createdump.exe para gravar um despejo de memória numa falha quando o despejo foi ligado por variável de ambiente (DOTNET_DbgEnableMiniDump); o createdump.exe não vai no pacote, e o código do Buzzy não cria processos"),
        Ordinal(2, "SysAllocString"), Ordinal(4, "SysAllocStringLen"), Ordinal(6, "SysFreeString"), Ordinal(7, "SysStringLen"),
        Ordinal(8, "VariantInit"), Ordinal(9, "VariantClear"), Ordinal(12, "VariantChangeType"), Ordinal(16, "SafeArrayDestroy"),
        Ordinal(17, "SafeArrayGetDim"), Ordinal(18, "SafeArrayGetElemsize"), Ordinal(20, "SafeArrayGetLBound"), Ordinal(26, "SafeArrayPutElement"),
        Ordinal(37, "SafeArrayAllocData"), Ordinal(41, "SafeArrayAllocDescriptorEx"), Ordinal(44, "SafeArraySetRecordInfo"), Ordinal(77, "SafeArrayGetVartype"),
        Ordinal(149, "SysStringByteLen"), Ordinal(150, "SysAllocStringByteLen"), Ordinal(162, "LoadRegTypeLib"), Ordinal(164, "QueryPathOfRegTypeLib"),
        Ordinal(183, "LoadTypeLibEx"), Ordinal(200, "GetErrorInfo"), Ordinal(201, "SetErrorInfo"), Ordinal(202, "CreateErrorInfo"),
        Ordinal(228, "VarCyFromDec"), Ordinal(323, "GetRecordInfoFromTypeInfo"), Ordinal(411, "SafeArrayCreateVector"),
    ];

    /// <summary>Permissão para esta importação exata (função ou "#ordinal"), ou nulo.</summary>
    public static PermissaoDoApphost? Procurar(string modulo, string funcao)
    {
        string moduloNormalizado = ListaProibida.NormalizarModulo(modulo);
        return Entradas.FirstOrDefault(p =>
            string.Equals(p.Modulo, moduloNormalizado, StringComparison.Ordinal)
            && string.Equals(p.Funcao, funcao, StringComparison.Ordinal));
    }

    private static PermissaoDoApphost Ordinal(int ordinal, string nome)
        => new("oleaut32", "#" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), $"{nome}: {ComInterop}");
}
