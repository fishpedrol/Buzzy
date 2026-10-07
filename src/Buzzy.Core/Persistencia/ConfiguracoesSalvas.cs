using Buzzy.Core.Personagem;

namespace Buzzy.Core.Persistencia;

// Conteúdo do settings.json, sem o formato: EsquemaDeConfiguracoes converte bytes e o adaptador
// lê e grava o arquivo. Posicao é nula na primeira execução ou se a gravada era inválida.
public sealed record ConfiguracoesSalvas(PosicaoDoPersonagem? Posicao, Preferencias Preferencias)
{
    // Vale sem arquivo ou com arquivo ilegível.
    public static readonly ConfiguracoesSalvas Padrao = new(null, Preferencias.Padrao);

    // Esconderijo e PresoPeloUsuario só existem junto com a posição: sem ela, Normalizar zera os
    // dois. Ficam fora do construtor posicional.
    public LadoDoEsconderijo Esconderijo { get; init; }

    public bool PresoPeloUsuario { get; init; }

    public Loaded ParaACarga(Topologia topologia)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return new Loaded(topologia, Posicao, Preferencias) { Esconderijo = Esconderijo, PresoPeloUsuario = PresoPeloUsuario };
    }
}

public enum SituacaoDaLeitura
{
    // Lida campo a campo; campo ruim fica no padrão, com aviso.
    Valida,

    // schemaVersion maior que a atual: lê o que conhece, mas não grava por cima nesta execução
    // pra não apagar os campos que não conhece.
    VersaoFutura,

    // Tamanho, UTF-8, JSON, raiz ou schemaVersion inválidos: valem os padrões.
    Ilegivel,
}

// Avisos: um por campo ignorado, repetido, fora da faixa ou de tipo errado. Só nome de campo do
// esquema e o motivo, nunca valor do arquivo nem nome de campo desconhecido (não vaza dado pro log).
// MotivoIlegivel: a primeira checagem que falhou ("tamanho", "utf8", "json", "raiz", "schemaVersion").
public sealed record LeituraDasConfiguracoes(
    SituacaoDaLeitura Situacao,
    int? Versao,
    ConfiguracoesSalvas Configuracoes,
    IReadOnlyList<string> Avisos,
    string? MotivoIlegivel);
