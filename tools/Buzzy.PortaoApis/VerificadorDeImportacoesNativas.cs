namespace Buzzy.PortaoApis;

/// <summary>Resultado da conferência das importações nativas de um arquivo.</summary>
internal sealed record AnaliseDeImportacoes(IReadOnlyList<Violacao> Violacoes, IReadOnlyList<Permitida> Permitidas);

/// <summary>Qual lista de permissões vale para um PE nativo.</summary>
internal enum ListaDePermissoes
{
    /// <summary>Nenhuma: as DLLs do produto e qualquer outro PE.</summary>
    Nenhuma,

    /// <summary>O apphost do build dependente do framework (<see cref="PermissoesDoApphost"/>).</summary>
    Apphost,

    /// <summary>O host de um pacote de arquivo único (<see cref="PermissoesDoHostDeArquivoUnico"/>).</summary>
    HostDeArquivoUnico,
}

/// <summary>
/// Confere as importações nativas de um PE com a lista proibida. Para o apphost, e só para ele, aplica a lista de
/// permissões explícita de <see cref="PermissoesDoApphost"/>; para o host de um pacote de arquivo único, a de
/// <see cref="PermissoesDoHostDeArquivoUnico"/>, que também nomeia importações por ordinal.
/// </summary>
internal static class VerificadorDeImportacoesNativas
{
    public static AnaliseDeImportacoes Avaliar(string arquivo, IEnumerable<ImportacaoNativa> importacoes, bool ehApphost)
        => Avaliar(arquivo, importacoes, ehApphost ? ListaDePermissoes.Apphost : ListaDePermissoes.Nenhuma);

    public static AnaliseDeImportacoes Avaliar(string arquivo, IEnumerable<ImportacaoNativa> importacoes, ListaDePermissoes lista)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(importacoes);

        var violacoes = new List<Violacao>();
        var permitidas = new List<Permitida>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PermissaoDoApphost? Permissao(ImportacaoNativa i) => lista switch
        {
            ListaDePermissoes.Apphost => PermissoesDoApphost.Procurar(i.Modulo, i.Funcao),
            ListaDePermissoes.HostDeArquivoUnico => PermissoesDoHostDeArquivoUnico.Procurar(i.Modulo, i.Funcao),
            _ => null,
        };

        foreach (ImportacaoNativa importacao in importacoes)
        {
            string api = importacao.ToString();
            if (!vistas.Add(api)) continue;
            string origem = importacao.CargaAtrasada ? "importação nativa com carga atrasada" : "importação nativa";

            Regra? regra = ListaProibida.ProcurarNativa(importacao.Modulo, importacao.Funcao);
            if (regra is null)
            {
                if (importacao.PorOrdinal)
                {
                    // Por ordinal, a função não pode ser conferida pelo nome; só uma permissão nomeada (o host de arquivo
                    // único, com o nome tirado da tabela de exportação do Windows) a aceita.
                    if (Permissao(importacao) is { } nomeada)
                        permitidas.Add(new Permitida(arquivo, api, Categoria.CodigoDinamico, nomeada));
                    else
                        violacoes.Add(new Violacao(arquivo, 0, 0, Codigos.ImportacaoNativa, Categoria.CodigoDinamico, api,
                            $"{origem} por ordinal: a função não pode ser conferida com a lista proibida", null));
                }
                continue;
            }

            PermissaoDoApphost? permissao = Permissao(importacao);
            if (permissao is not null)
                permitidas.Add(new Permitida(arquivo, api, regra.Categoria, permissao));
            else
                violacoes.Add(new Violacao(arquivo, 0, 0, Codigos.ImportacaoNativa, regra.Categoria, api,
                    $"{origem}: {regra.Motivo}", regra));
        }

        return new AnaliseDeImportacoes(violacoes, permitidas);
    }
}
