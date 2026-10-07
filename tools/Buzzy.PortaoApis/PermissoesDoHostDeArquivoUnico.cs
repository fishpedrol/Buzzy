namespace Buzzy.PortaoApis;

// Exceções explícitas pro Buzzy.exe de arquivo único. Ali ele é o singlefilehost.exe: lançador,
// hostfxr, hostpolicy e CoreCLR ligados estaticamente num PE só. No build normal essas mesmas
// importações estão nas DLLs do runtime instalado, que o portão nem lê. ProcedenciaDoHost garante
// que o host é byte a byte o da Microsoft; aqui fica o motivo de cada importação proibida dele.
//
// SDK 10.0.401, host 10.0.12: 441 funções importadas, 33 batem com a lista proibida. Quatro são
// as do apphost; LoadLibraryExA, CreateProcessW e os 27 ordinais do OLEAUT32 vêm do coreclr.dll
// (nomes dos ordinais tirados da tabela de exportação do oleaut32.dll). Só vale pro .exe de um
// pacote (--pacote), com módulo e função/ordinal exatos. Importação nova reprova até ser revisada.
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

    // funcao pode ser "#ordinal".
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
