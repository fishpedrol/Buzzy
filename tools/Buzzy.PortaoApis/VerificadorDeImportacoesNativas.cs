namespace Buzzy.PortaoApis;

/// <summary>Resultado da conferência das importações nativas de um arquivo.</summary>
internal sealed record AnaliseDeImportacoes(IReadOnlyList<Violacao> Violacoes, IReadOnlyList<Permitida> Permitidas);

/// <summary>
/// Confere as importações nativas de um PE com a lista proibida. Para o apphost, e só para ele,
/// aplica a lista de permissões explícita de <see cref="PermissoesDoApphost"/>.
/// </summary>
internal static class VerificadorDeImportacoesNativas
{
    public static AnaliseDeImportacoes Avaliar(string arquivo, IEnumerable<ImportacaoNativa> importacoes, bool ehApphost)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(importacoes);

        var violacoes = new List<Violacao>();
        var permitidas = new List<Permitida>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ImportacaoNativa importacao in importacoes)
        {
            string api = importacao.ToString();
            if (!vistas.Add(api)) continue;
            string origem = importacao.CargaAtrasada ? "importação nativa com carga atrasada" : "importação nativa";

            Regra? regra = ListaProibida.ProcurarNativa(importacao.Modulo, importacao.Funcao);
            if (regra is null)
            {
                if (importacao.PorOrdinal)
                    violacoes.Add(new Violacao(arquivo, 0, 0, Codigos.ImportacaoNativa, Categoria.CodigoDinamico, api,
                        $"{origem} por ordinal: a função não pode ser conferida com a lista proibida", null));
                continue;
            }

            PermissaoDoApphost? permissao = ehApphost ? PermissoesDoApphost.Procurar(importacao.Modulo, importacao.Funcao) : null;
            if (permissao is not null)
                permitidas.Add(new Permitida(arquivo, api, regra.Categoria, permissao));
            else
                violacoes.Add(new Violacao(arquivo, 0, 0, Codigos.ImportacaoNativa, regra.Categoria, api,
                    $"{origem}: {regra.Motivo}", regra));
        }

        return new AnaliseDeImportacoes(violacoes, permitidas);
    }
}
