using System.Globalization;
using System.Text;

namespace Buzzy.Core;

public enum Orientacao
{
    Paisagem,
    Retrato,
}

// Monitor em pixels físicos. Chave é opaca e só comparada por igualdade: no app é "mon:" + resumo
// do caminho do dispositivo (ou "gdi:" + nome GDI como reserva); os testes usam \\.\DISPLAYn.
// AreaUtil exclui a barra de tarefas. Dpi 96 = 100%. O principal tem a origem (0,0).
public sealed record MonitorDoDesktop(string Chave, RetanguloPx Tela, RetanguloPx AreaUtil, int Dpi, bool Principal)
{
    public double Escala => Dpi / 96.0;

    public Orientacao Orientacao => Tela.Altura > Tela.Largura ? Orientacao.Retrato : Orientacao.Paisagem;

    public override string ToString()
        => $"{Chave}{(Principal ? " [principal]" : "")} tela {Tela} útil {AreaUtil} dpi {Dpi}";
}

// Imutável. Não presume monitores lado a lado, alinhados, de mesma resolução
// nem com o principal à esquerda.
public sealed class Topologia
{
    public Topologia(IEnumerable<MonitorDoDesktop> monitores)
    {
        ArgumentNullException.ThrowIfNull(monitores);
        List<MonitorDoDesktop> lista = [.. monitores];

        if (lista.Count == 0)
            throw new ArgumentException("A topologia precisa de pelo menos um monitor.", nameof(monitores));
        if (lista.Any(m => m is null))
            throw new ArgumentException("Monitor nulo na topologia.", nameof(monitores));

        int principais = lista.Count(m => m.Principal);
        if (principais != 1)
            throw new ArgumentException($"A topologia precisa de exatamente um monitor principal; há {principais}.", nameof(monitores));

        var chaves = new HashSet<string>(StringComparer.Ordinal);
        foreach (MonitorDoDesktop m in lista)
        {
            if (string.IsNullOrEmpty(m.Chave))
                throw new ArgumentException("Monitor sem chave.", nameof(monitores));
            if (!chaves.Add(m.Chave))
                throw new ArgumentException($"Chave de monitor repetida: {m.Chave}.", nameof(monitores));
            if (m.Tela.Vazio)
                throw new ArgumentException($"Monitor {m.Chave} com tela vazia: {m.Tela}.", nameof(monitores));
            if (m.AreaUtil.Vazio || !m.Tela.Contem(m.AreaUtil))
                throw new ArgumentException($"Monitor {m.Chave} com área útil {m.AreaUtil} vazia ou fora da tela {m.Tela}.", nameof(monitores));
            if (m.Dpi <= 0)
                throw new ArgumentException($"Monitor {m.Chave} com DPI inválido: {m.Dpi}.", nameof(monitores));
        }

        Monitores = lista.AsReadOnly();
        Principal = lista.Single(m => m.Principal);
        ImpressaoDigital = CalcularImpressao(lista);
    }

    // Na ordem recebida.
    public IReadOnlyList<MonitorDoDesktop> Monitores { get; }

    public MonitorDoDesktop Principal { get; }

    // Resumo canônico (chaves, retângulos, áreas úteis, DPI, principal) em ordem de chave.
    // Se mudou, a configuração de monitores mudou.
    public string ImpressaoDigital { get; }

    public bool MesmaConfiguracao(Topologia outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        return string.Equals(ImpressaoDigital, outra.ImpressaoDigital, StringComparison.Ordinal);
    }

    public MonitorDoDesktop? PorChave(string chave) => Monitores.FirstOrDefault(m => string.Equals(m.Chave, chave, StringComparison.Ordinal));

    // Nulo se o ponto cai num vão entre monitores.
    public MonitorDoDesktop? MonitorQueContem(PontoPx p) => Monitores.FirstOrDefault(m => m.Tela.Contem(p));

    // Num vão, pega a tela mais próxima. Empate: principal primeiro, depois a ordem da lista.
    public MonitorDoDesktop MonitorMaisProximo(PontoPx p)
    {
        MonitorDoDesktop? contem = MonitorQueContem(p);
        if (contem is not null) return contem;

        MonitorDoDesktop melhor = Monitores[0];
        long melhorDistancia = melhor.Tela.DistanciaAoQuadrado(p);
        foreach (MonitorDoDesktop m in Monitores.Skip(1))
        {
            long d = m.Tela.DistanciaAoQuadrado(p);
            if (d < melhorDistancia || (d == melhorDistancia && m.Principal && !melhor.Principal))
            {
                melhor = m;
                melhorDistancia = d;
            }
        }
        return melhor;
    }

    private static string CalcularImpressao(List<MonitorDoDesktop> monitores)
    {
        var sb = new StringBuilder();
        foreach (MonitorDoDesktop m in monitores.OrderBy(m => m.Chave, StringComparer.Ordinal))
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(CultureInfo.InvariantCulture, $"{m.Chave}|{Canonico(m.Tela)}|{Canonico(m.AreaUtil)}|{m.Dpi}|{(m.Principal ? "P" : "-")}");
        }
        return sb.ToString();
    }

    // Igual ao RetanguloPx.ToString, mas invariante: sv-SE, por exemplo, escreve o sinal
    // de negativo como U+2212.
    private static string Canonico(RetanguloPx r)
        => string.Create(CultureInfo.InvariantCulture, $"({r.Esquerda},{r.Topo})-({r.Direita},{r.Base})");

    public override string ToString() => string.Join("; ", Monitores);
}
