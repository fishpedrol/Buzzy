using System.Globalization;

namespace Buzzy.Core.Personagem;

// SplitMix64 guardado como valor no estado: mesma semente + mesmos eventos
// = mesmas escolhas. Não é criptográfico e não precisa ser.
public readonly record struct Aleatorio(ulong Estado)
{
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

    // Inteiro em [minimo, maximo], inclusive. O viés do resto (64 bits sobre
    // faixas de até 2³¹) fica abaixo de 2⁻³², então tanto faz.
    public (int Valor, Aleatorio Proximo) Entre(int minimo, int maximo)
    {
        if (maximo < minimo) throw new ArgumentOutOfRangeException(nameof(maximo), maximo, $"Máximo menor que o mínimo {minimo}.");
        (ulong v, Aleatorio proximo) = Sortear();
        ulong faixa = (ulong)((long)maximo - minimo) + 1;
        return ((int)((long)minimo + (long)(v % faixa)), proximo);
    }

    // Peso negativo conta como zero.
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

    // Sorteia em ms inteiros.
    public (TimeSpan Valor, Aleatorio Proximo) Duracao(TimeSpan minimo, TimeSpan maximo)
    {
        (int ms, Aleatorio proximo) = Entre((int)minimo.TotalMilliseconds, (int)maximo.TotalMilliseconds);
        return (TimeSpan.FromMilliseconds(ms), proximo);
    }

    // Sempre gasta um passo do gerador, mesmo com chance certa (1 em 1)
    // ou nula (0 em 1), pra não mudar a sequência dos sorteios seguintes.
    public (bool Saiu, Aleatorio Proximo) Sortear(Chance chance)
    {
        ArgumentNullException.ThrowIfNull(chance);
        (int valor, Aleatorio proximo) = Entre(1, chance.Em);
        return (valor <= chance.Vezes, proximo);
    }
}

// "Vezes em Em", tipo "1 em 8". Vezes = 0 é nunca; Vezes = Em é sempre.
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

    public int Vezes { get; }

    public int Em { get; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Vezes} em {Em}");
}
