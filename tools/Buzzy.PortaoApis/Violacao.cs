namespace Buzzy.PortaoApis;

internal static class Codigos
{
    public const string ErroDeUso = "BZP000";
    public const string PInvoke = "BZP001";
    public const string ReferenciaGerenciada = "BZP002";
    public const string ImportacaoNativa = "BZP003";
    public const string CodigoFonte = "BZP004";
    public const string Manifesto = "BZP005";
    public const string BinarioDeTerceiro = "BZP006";

    // Host do pacote que não é o singlefilehost.exe da Microsoft.
    public const string HostDeArquivoUnico = "BZP007";

    // runtimeconfig.json ou deps.json fora do revisado: o runtime carregaria código de fora.
    public const string ConfiguracaoDoRuntime = "BZP008";
}

// Linha e Coluna começam em 1; 0 em binário. Regra é nula pra ordinal e manifesto.
internal sealed record Violacao(
    string Arquivo,
    int Linha,
    int Coluna,
    string Codigo,
    Categoria Categoria,
    string Api,
    string Detalhe,
    Regra? Regra);

// Importação proibida do host que está na lista de permissões.
internal sealed record Permitida(string Arquivo, string Api, Categoria Categoria, PermissaoDoApphost Permissao);
