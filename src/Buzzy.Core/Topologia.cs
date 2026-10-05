using System.Globalization;
using System.Text;

namespace Buzzy.Core;

public enum Orientacao
{
    Paisagem,
    Retrato,
}

/// <summary>
/// Um monitor do desktop virtual, em pixels físicos (DEC-008).
/// </summary>
/// <param name="Chave">
/// Identificador opaco do monitor. No app é a chave estável da Fase 5 (DEC-030): <c>mon:</c> e um
/// resumo do caminho do dispositivo, ou, sem ele, a reserva <c>gdi:</c> e o nome GDI. O núcleo só a
/// compara por igualdade; nenhuma decisão depende do texto, e as topologias de exemplo dos testes
/// usam o nome GDI (<c>\\.\DISPLAYn</c>).
/// </param>
/// <param name="Tela">Retângulo do monitor inteiro.</param>
/// <param name="AreaUtil">Retângulo sem a barra de tarefas e outras barras reservadas.</param>
/// <param name="Dpi">DPI efetivo do monitor (96 = escala de 100%).</param>
/// <param name="Principal">Se é o monitor principal, que tem a origem (0,0).</param>
public sealed record MonitorDoDesktop(string Chave, RetanguloPx Tela, RetanguloPx AreaUtil, int Dpi, bool Principal)
{
    public double Escala => Dpi / 96.0;

    public Orientacao Orientacao => Tela.Altura > Tela.Largura ? Orientacao.Retrato : Orientacao.Paisagem;

    public override string ToString()
        => $"{Chave}{(Principal ? " [principal]" : "")} tela {Tela} útil {AreaUtil} dpi {Dpi}";
}

/// <summary>
/// Topologia imutável do desktop virtual. Nada aqui presume monitores lado a lado,
/// alinhados, com a mesma resolução ou com o principal à esquerda (ARCHITECTURE.md 2.4).
/// </summary>
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

    /// <summary>Monitores na ordem recebida.</summary>
    public IReadOnlyList<MonitorDoDesktop> Monitores { get; }

    public MonitorDoDesktop Principal { get; }

    /// <summary>
    /// Resumo canônico de chaves, retângulos, áreas úteis, DPI e monitor principal, em ordem
    /// de chave. Impressões diferentes significam mudança de configuração (ARCHITECTURE.md 2.4).
    /// </summary>
    public string ImpressaoDigital { get; }

    public bool MesmaConfiguracao(Topologia outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        return string.Equals(ImpressaoDigital, outra.ImpressaoDigital, StringComparison.Ordinal);
    }

    public MonitorDoDesktop? PorChave(string chave) => Monitores.FirstOrDefault(m => string.Equals(m.Chave, chave, StringComparison.Ordinal));

    /// <summary>Monitor cuja tela contém o ponto, ou nulo se o ponto cai num vão entre monitores.</summary>
    public MonitorDoDesktop? MonitorQueContem(PontoPx p) => Monitores.FirstOrDefault(m => m.Tela.Contem(p));

    /// <summary>
    /// Monitor que contém o ponto ou, num vão, o de tela mais próxima. Em empate de
    /// distância, vence o principal e depois a ordem da lista.
    /// </summary>
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

    // Mesmo formato de RetanguloPx.ToString, mas sempre na cultura invariante: o ToString usa
    // a cultura atual, e culturas como sv-SE escrevem números negativos com U+2212.
    private static string Canonico(RetanguloPx r)
        => string.Create(CultureInfo.InvariantCulture, $"({r.Esquerda},{r.Topo})-({r.Direita},{r.Base})");

    public override string ToString() => string.Join("; ", Monitores);
}
