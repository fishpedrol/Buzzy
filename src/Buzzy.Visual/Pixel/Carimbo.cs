namespace Buzzy.Visual.Pixel;

// Imagem pixel a pixel em texto: cada caractere é uma cor da legenda e '.' deixa o pixel de baixo.
// Pros detalhes que a geometria não resolve em poucos pixels (olhos, boca, dedos, itens).
public sealed class Carimbo
{
    // Tem que vir antes de Legenda: campos estáticos iniciam na ordem do texto.
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
        // Tamagotchi.
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

    // Usa Add de propósito: letra repetida lança em vez de trocar a cor calada, como faria o indexador.
    public static IReadOnlyDictionary<char, Cor> MontarLegenda(IEnumerable<(char Letra, Cor Cor)> pares)
    {
        ArgumentNullException.ThrowIfNull(pares);
        var legenda = new Dictionary<char, Cor>();
        foreach ((char letra, Cor cor) in pares) legenda.Add(letra, cor);
        return legenda;
    }

    // 90° sem perda, mesma regra de Tela.Girada. Horário: linha de baixo vira a coluna da esquerda.
    // Anti-horário: (x, y) vai pra (y, Largura - 1 - x).
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
