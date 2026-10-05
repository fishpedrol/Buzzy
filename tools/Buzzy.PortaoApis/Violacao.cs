namespace Buzzy.PortaoApis;

/// <summary>Códigos das linhas de erro, no formato que o MSBuild reconhece.</summary>
internal static class Codigos
{
    public const string ErroDeUso = "BZP000";
    public const string PInvoke = "BZP001";
    public const string ReferenciaGerenciada = "BZP002";
    public const string ImportacaoNativa = "BZP003";
    public const string CodigoFonte = "BZP004";
    public const string Manifesto = "BZP005";
    public const string BinarioDeTerceiro = "BZP006";
}

/// <summary>Uma capacidade proibida encontrada.</summary>
/// <param name="Arquivo">Caminho completo do arquivo onde foi encontrada.</param>
/// <param name="Linha">Linha, a partir de 1; 0 quando o achado não tem linha (binários).</param>
/// <param name="Coluna">Coluna, a partir de 1; 0 quando não tem linha.</param>
/// <param name="Codigo">Um dos <see cref="Codigos"/>.</param>
/// <param name="Categoria">Linha de SECURITY.md 3.2, ou <see cref="Categoria.Manifesto"/>.</param>
/// <param name="Api">O que foi encontrado, como aparece no arquivo: <c>user32.dll!SendInput</c>.</param>
/// <param name="Detalhe">Onde e por quê, em uma frase.</param>
/// <param name="Regra">Regra da lista proibida que acusou; nulo para ordinal e manifesto.</param>
internal sealed record Violacao(
    string Arquivo,
    int Linha,
    int Coluna,
    string Codigo,
    Categoria Categoria,
    string Api,
    string Detalhe,
    Regra? Regra);

/// <summary>Importação do apphost que coincide com a lista proibida e está na lista de permissões.</summary>
internal sealed record Permitida(string Arquivo, string Api, Categoria Categoria, PermissaoDoApphost Permissao);
