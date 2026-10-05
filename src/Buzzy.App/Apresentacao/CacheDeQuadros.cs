using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.Imaging;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// Cache dos quadros já renderizados, limitado por bytes de pixels (crítica, C28 e F14): o uso, os itens e as
/// sobreposições da onda do tamagotchi multiplicam os quadros possíveis, e um cache sem limite cresceria sem fim
/// (meta de memória de Q-08). Guarda até <see cref="Orcamento"/> bytes; para caber um quadro novo, descarta os usados
/// há mais tempo (LRU). Um quadro maior que o orçamento inteiro não é guardado. Só na thread da interface.
/// </summary>
internal sealed class CacheDeQuadros<TChave>
    where TChave : notnull
{
    private readonly record struct Entrada(TChave Chave, BitmapSource Quadro, long Bytes);

    private readonly Dictionary<TChave, LinkedListNode<Entrada>> _porChave = [];

    /// <summary>Do usado há menos tempo (primeiro) ao usado há mais tempo (último).</summary>
    private readonly LinkedList<Entrada> _ordem = new();

    internal CacheDeQuadros(long orcamentoBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orcamentoBytes);
        Orcamento = orcamentoBytes;
    }

    /// <summary>Quantos bytes de pixels o cache guarda, no máximo.</summary>
    internal long Orcamento { get; }

    /// <summary>Quantos quadros estão no cache.</summary>
    internal int Quantos => _porChave.Count;

    /// <summary>Quantos bytes de pixels os quadros do cache ocupam; nunca mais que <see cref="Orcamento"/>.</summary>
    internal long Bytes { get; private set; }

    /// <summary>Quantos quadros já foram descartados para caber outros.</summary>
    internal long Descartados { get; private set; }

    /// <summary>Se o quadro está no cache, sem contar como uso.</summary>
    internal bool Contem(TChave chave) => _porChave.ContainsKey(chave);

    /// <summary>O quadro guardado com a chave, que passa a ser o usado há menos tempo.</summary>
    internal bool TentarObter(TChave chave, [MaybeNullWhen(false)] out BitmapSource quadro)
    {
        if (!_porChave.TryGetValue(chave, out LinkedListNode<Entrada>? no))
        {
            quadro = null;
            return false;
        }
        _ordem.Remove(no);
        _ordem.AddFirst(no);
        quadro = no.Value.Quadro;
        return true;
    }

    /// <summary>
    /// Guarda o quadro como o usado há menos tempo, no lugar do que já tivesse a mesma chave, descartando os usados há
    /// mais tempo até caber. Falso, sem mexer no cache além de tirar o quadro antigo da chave, se o quadro sozinho passa
    /// do orçamento.
    /// </summary>
    internal bool Guardar(TChave chave, BitmapSource quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);
        Remover(chave);
        long bytes = BytesDe(quadro);
        if (bytes > Orcamento) return false;
        while (Bytes + bytes > Orcamento && _ordem.Last is { } maisAntigo)
        {
            Remover(maisAntigo.Value.Chave);
            Descartados++;
        }
        _porChave[chave] = _ordem.AddFirst(new Entrada(chave, quadro, bytes));
        Bytes += bytes;
        return true;
    }

    /// <summary>Bytes de pixels de um quadro: largura × altura × bytes por pixel (4 no sprite, Pbgra32).</summary>
    internal static long BytesDe(BitmapSource quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);
        return (long)quadro.PixelWidth * quadro.PixelHeight * ((quadro.Format.BitsPerPixel + 7) / 8);
    }

    private void Remover(TChave chave)
    {
        if (!_porChave.Remove(chave, out LinkedListNode<Entrada>? no)) return;
        _ordem.Remove(no);
        Bytes -= no.Value.Bytes;
    }
}
