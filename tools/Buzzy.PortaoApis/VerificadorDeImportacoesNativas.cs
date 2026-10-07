namespace Buzzy.PortaoApis;

internal sealed record AnaliseDeImportacoes(IReadOnlyList<Violacao> Violacoes, IReadOnlyList<Permitida> Permitidas);

internal enum ListaDePermissoes
{
    // DLLs do produto e qualquer outro PE.
    Nenhuma,

    // Apphost do build dependente do framework.
    Apphost,

    HostDeArquivoUnico,
}

// Exceções só valem pro apphost ou pro host de arquivo único (que também nomeia ordinais).
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
                    // Ordinal não dá pra conferir pelo nome; só passa com permissão nomeada (host de arquivo único).
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
