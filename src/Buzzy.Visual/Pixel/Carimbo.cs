namespace Buzzy.Visual.Pixel;

/// <summary>
/// Pequena imagem desenhada pixel a pixel em texto: cada caractere é uma cor da legenda e
/// <c>.</c> deixa o pixel de baixo como está. Usada para os detalhes que a geometria não resolve
/// bem em poucos pixels: olhos, sobrancelhas, nariz, bocas, dedos e os itens do tamagotchi.
/// </summary>
public sealed class Carimbo
{
    // Pares da legenda padrão, na ordem em que entraram. Vem antes de Legenda: os campos estáticos
    // são iniciados na ordem do texto.
    private static readonly (char Letra, Cor Cor)[] ParesDaLegenda =
    [
        ('K', Cor.Contorno),
        ('e', Cor.PeloEscuro),
        ('p', Cor.Pelo),
        ('c', Cor.Creme),
        ('s', Cor.CremeSombra),
        ('C', Cor.CremeClaro),
        ('o', Cor.Pessego),
        ('O', Cor.PessegoEscuro),
        ('i', Cor.Iris),
        ('I', Cor.IrisClara),
        ('u', Cor.Pupila),
        ('W', Cor.Branco),
        ('b', Cor.Boca),
        ('l', Cor.Lingua),
        ('r', Cor.Bochecha),
        ('v', Cor.Sobrancelha),
        ('h', Cor.Palha),
        ('H', Cor.PalhaClara),
        ('j', Cor.PalhaEscura),
        ('f', Cor.Faixa),
        ('F', Cor.FaixaEscura),
        // Tamagotchi (DEC-028).
        ('J', Cor.Cipo),
        ('L', Cor.CipoEscuro),
        ('y', Cor.Banana),
        ('Y', Cor.BananaClara),
        ('n', Cor.BananaEscura),
        ('a', Cor.Agua),
        ('A', Cor.AguaClara),
        ('d', Cor.AguaEscura),
        ('g', Cor.Vidro),
        ('G', Cor.VidroSombra),
        ('m', Cor.Metal),
        ('M', Cor.MetalClaro),
        ('E', Cor.MetalEscuro),
        ('w', Cor.Papel),
        ('x', Cor.PapelSombra),
        ('t', Cor.Filtro),
        ('T', Cor.FiltroEscuro),
        ('q', Cor.Brasa),
        ('Q', Cor.BrasaClara),
        ('Z', Cor.Fumaca),
        ('B', Cor.Cerveja),
        ('D', Cor.CervejaEscura),
        ('k', Cor.Cafe),
        ('N', Cor.Neon),
        ('P', Cor.NeonEscuro),
        ('R', Cor.Rosa),
        ('S', Cor.RosaEscura),
        ('V', Cor.Lilas),
        ('X', Cor.LilasEscuro),
        ('1', Cor.Coracao),
        ('2', Cor.Estrela),
        ('3', Cor.EstrelaEscura),
        ('5', Cor.EscleraVermelha),
        ('6', Cor.OlhoVermelho),
        ('7', Cor.Enjoo),
        ('8', Cor.BochechaForte),
        ('z', Cor.Cinza),
    ];

    /// <summary>Legenda padrão dos carimbos.</summary>
    public static readonly IReadOnlyDictionary<char, Cor> Legenda = MontarLegenda(ParesDaLegenda);

    private readonly Cor?[] _pixels;

    public Carimbo(params string[] linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        if (linhas.Length == 0) throw new ArgumentException("Carimbo vazio.", nameof(linhas));
        Largura = linhas.Max(l => l.Length);
        Altura = linhas.Length;
        _pixels = new Cor?[Largura * Altura];
        for (int y = 0; y < Altura; y++)
        {
            for (int x = 0; x < linhas[y].Length; x++)
            {
                char c = linhas[y][x];
                if (c == '.' || c == ' ') continue;
                _pixels[y * Largura + x] = Legenda.TryGetValue(c, out Cor cor)
                    ? cor
                    : throw new ArgumentException($"Caractere '{c}' fora da legenda na linha {y}.", nameof(linhas));
            }
        }
    }

    private Carimbo(int largura, int altura, Cor?[] pixels)
    {
        Largura = largura;
        Altura = altura;
        _pixels = pixels;
    }

    public int Largura { get; }

    public int Altura { get; }

    public Cor? this[int x, int y] => x >= 0 && y >= 0 && x < Largura && y < Altura ? _pixels[y * Largura + x] : null;

    /// <summary>
    /// Monta uma legenda com <c>Add</c>: uma letra repetida lança <see cref="ArgumentException"/>
    /// em vez de trocar a cor em silêncio, como faria um inicializador por indexador.
    /// </summary>
    public static IReadOnlyDictionary<char, Cor> MontarLegenda(IEnumerable<(char Letra, Cor Cor)> pares)
    {
        ArgumentNullException.ThrowIfNull(pares);
        var legenda = new Dictionary<char, Cor>();
        foreach ((char letra, Cor cor) in pares) legenda.Add(letra, cor);
        return legenda;
    }

    /// <summary>
    /// O carimbo girado 90°, sem perda, com a regra de <see cref="Tela.Girada"/>:
    /// <paramref name="horario"/> leva a linha de baixo para a coluna da esquerda; anti-horário,
    /// a linha de cima para a coluna da esquerda, isto é, (x, y) vai para (y, Largura − 1 − x).
    /// Largura e altura trocam de lugar.
    /// </summary>
    public Carimbo Girado(bool horario)
    {
        int largura = Altura, altura = Largura;
        var pixels = new Cor?[largura * altura];
        for (int y = 0; y < altura; y++)
            for (int x = 0; x < largura; x++)
                pixels[y * largura + x] = horario ? this[y, Altura - 1 - x] : this[Largura - 1 - y, x];
        return new Carimbo(largura, altura, pixels);
    }
}
