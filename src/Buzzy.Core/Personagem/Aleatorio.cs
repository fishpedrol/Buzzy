using System.Globalization;

namespace Buzzy.Core.Personagem;

/// <summary>
/// Gerador pseudoaleatório determinístico (SplitMix64) guardado como valor no estado do núcleo:
/// com a mesma semente e a mesma sequência de eventos, as escolhas se repetem (invariante 7).
/// Não é criptográfico e não precisa ser.
/// </summary>
public readonly record struct Aleatorio(ulong Estado)
{
    /// <summary>Próximo valor de 64 bits e o gerador avançado.</summary>
    public (ulong Valor, Aleatorio Proximo) Sortear()
    {
        unchecked
        {
            ulong z = Estado + 0x9E3779B97F4A7C15UL;
            ulong r = z;
            r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
            r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
            return (r ^ (r >> 31), new Aleatorio(z));
        }
    }

    /// <summary>
    /// Inteiro uniforme em [<paramref name="minimo"/>, <paramref name="maximo"/>]. O viés do resto
    /// com 64 bits sobre faixas de até 2³¹ é menor que 2⁻³², irrelevante para comportamento.
    /// </summary>
    public (int Valor, Aleatorio Proximo) Entre(int minimo, int maximo)
    {
        if (maximo < minimo) throw new ArgumentOutOfRangeException(nameof(maximo), maximo, $"Máximo menor que o mínimo {minimo}.");
        (ulong v, Aleatorio proximo) = Sortear();
        ulong faixa = (ulong)((long)maximo - minimo) + 1;
        return ((int)((long)minimo + (long)(v % faixa)), proximo);
    }

    /// <summary>Índice escolhido com probabilidade proporcional ao peso; pesos negativos contam como zero.</summary>
    public (int Indice, Aleatorio Proximo) Ponderado(IReadOnlyList<int> pesos)
    {
        ArgumentNullException.ThrowIfNull(pesos);
        long total = pesos.Sum(p => (long)Math.Max(p, 0));
        if (total <= 0) throw new ArgumentException("Nenhum peso positivo.", nameof(pesos));
        (ulong v, Aleatorio proximo) = Sortear();
        long alvo = (long)(v % (ulong)total);
        for (int i = 0; i < pesos.Count; i++)
        {
            long peso = Math.Max(pesos[i], 0);
            if (alvo < peso) return (i, proximo);
            alvo -= peso;
        }
        throw new InvalidOperationException("Inalcançável: o alvo é menor que a soma dos pesos.");
    }

    /// <summary>Duração uniforme entre dois limites, em milissegundos inteiros.</summary>
    public (TimeSpan Valor, Aleatorio Proximo) Duracao(TimeSpan minimo, TimeSpan maximo)
    {
        (int ms, Aleatorio proximo) = Entre((int)minimo.TotalMilliseconds, (int)maximo.TotalMilliseconds);
        return (TimeSpan.FromMilliseconds(ms), proximo);
    }

    /// <summary>
    /// Um sorteio com a chance dada, num passo só do gerador: um inteiro uniforme de 1 a <see cref="Chance.Em"/>, que sai se
    /// for até <see cref="Chance.Vezes"/>. O passo acontece mesmo com a chance certa (1 em 1) ou nula (0 em 1).
    /// </summary>
    public (bool Saiu, Aleatorio Proximo) Sortear(Chance chance)
    {
        ArgumentNullException.ThrowIfNull(chance);
        (int valor, Aleatorio proximo) = Entre(1, chance.Em);
        return (valor <= chance.Vezes, proximo);
    }
}

/// <summary>
/// Uma chance de <see cref="Vezes"/> em <see cref="Em"/>, como "1 em 8", sorteada por <see cref="Aleatorio.Sortear(Chance)"/>.
/// <see cref="Em"/> é pelo menos 1, e <see cref="Vezes"/> vai de 0 (nunca) a <see cref="Em"/> (sempre).
/// </summary>
public sealed record Chance
{
    public Chance(int vezes, int em)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(em, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(vezes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vezes, em);
        Vezes = vezes;
        Em = em;
    }

    /// <summary>Em quantas, de cada <see cref="Em"/>, sai.</summary>
    public int Vezes { get; }

    /// <summary>De quantas.</summary>
    public int Em { get; }

    /// <summary>"1 em 8", na cultura invariante.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Vezes} em {Em}");
}
