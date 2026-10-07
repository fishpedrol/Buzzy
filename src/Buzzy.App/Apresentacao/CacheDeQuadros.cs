using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.Imaging;

namespace Buzzy.App.Apresentacao;

// Cache LRU de quadros renderizados, limitado por bytes de pixels: uso, itens e
// sobreposições do tamagotchi multiplicam os quadros e sem limite a memória explode.
// Quadro maior que o orçamento inteiro não entra. Só na thread da interface.
internal sealed class CacheDeQuadros<TChave>
    where TChave : notnull
{
    private readonly record struct Entrada(TChave Chave, BitmapSource Quadro, long Bytes);

    private readonly Dictionary<TChave, LinkedListNode<Entrada>> _porChave = [];

    // Mais recente primeiro, mais antigo no fim.
    private readonly LinkedList<Entrada> _ordem = new();

    internal CacheDeQuadros(long orcamentoBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orcamentoBytes);
        Orcamento = orcamentoBytes;
    }

    internal long Orcamento { get; }

    internal int Quantos => _porChave.Count;

    internal long Bytes { get; private set; }

    internal long Descartados { get; private set; }

    // Não conta como uso.
    internal bool Contem(TChave chave) => _porChave.ContainsKey(chave);

    // Achou: o quadro vira o mais recente.
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

    // Substitui o da mesma chave e descarta os mais antigos até caber. Se o quadro
    // sozinho passa do orçamento, devolve falso (o antigo da chave já saiu).
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

    // largura × altura × bytes por pixel (4 no sprite, Pbgra32).
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
