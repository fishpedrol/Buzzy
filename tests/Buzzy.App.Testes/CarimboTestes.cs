using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Base da arte do tamagotchi (DEC-028, passo A1): a paleta só cresce no fim, sem mudar o byte nem
/// o ARGB das cores antigas; a legenda dos carimbos recusa letra repetida em vez de trocar a cor em
/// silêncio (crítica, F10); e o carimbo gira 90° sem perda, com a mesma regra de
/// <see cref="Tela.Girada"/>.
/// </summary>
internal sealed class CarimboTestes
{
    /// <summary>As 28 cores de antes do tamagotchi, com o byte e o ARGB gravados.</summary>
    private static readonly (Cor Cor, byte Byte, uint Argb)[] CoresAntigas =
    [
        (Cor.Nada, 0, 0x00000000),
        (Cor.Contorno, 1, 0xFF121830),
        (Cor.PeloEscuro, 2, 0xFF1B2748),
        (Cor.Pelo, 3, 0xFF283A5F),
        (Cor.PeloClaro, 4, 0xFF3B5486),
        (Cor.Creme, 5, 0xFFFDD5A6),
        (Cor.CremeSombra, 6, 0xFFE6B083),
        (Cor.CremeClaro, 7, 0xFFFFE9CC),
        (Cor.Pessego, 8, 0xFFF6996D),
        (Cor.PessegoEscuro, 9, 0xFFD2704A),
        (Cor.Iris, 10, 0xFF6A3419),
        (Cor.IrisClara, 11, 0xFF9A5236),
        (Cor.Pupila, 12, 0xFF140A06),
        (Cor.Branco, 13, 0xFFFFFFFF),
        (Cor.Boca, 14, 0xFF5A1D22),
        (Cor.Lingua, 15, 0xFFE8727A),
        (Cor.Bochecha, 16, 0xFFF4A987),
        (Cor.Sobrancelha, 17, 0xFF2E2230),
        (Cor.Palha, 18, 0xFFEFB262),
        (Cor.PalhaClara, 19, 0xFFFCCB7E),
        (Cor.PalhaEscura, 20, 0xFFC27F45),
        (Cor.Faixa, 21, 0xFFB83A37),
        (Cor.FaixaEscura, 22, 0xFF862A2B),
        (Cor.Cipo, 23, 0xFF6B8A34),
        (Cor.CipoEscuro, 24, 0xFF46601F),
        (Cor.CipoClaro, 25, 0xFF93B24F),
        (Cor.Folha, 26, 0xFF4EA24A),
        (Cor.FolhaEscura, 27, 0xFF2E6B2E),
    ];

    [Teste]
    public void CoresAntigasMantemByteEArgb()
    {
        foreach ((Cor cor, byte valor, uint argb) in CoresAntigas)
        {
            Afirmar.Igual(valor, (byte)cor, $"{cor}: o byte não muda");
            Afirmar.Igual(argb, Paleta.Argb(cor), $"{cor}: o ARGB não muda");
        }
    }

    [Teste]
    public void CoresNovasFicamNoFimOpacasEDistintas()
    {
        Cor[] todas = Enum.GetValues<Cor>();
        Afirmar.Verdadeiro(todas.Length > CoresAntigas.Length, "o tamagotchi acrescenta cores");
        foreach (Cor nova in todas.Where(c => !CoresAntigas.Any(a => a.Cor == c)))
        {
            Afirmar.Verdadeiro((byte)nova > (byte)Cor.FolhaEscura, $"{nova}: cor nova só depois da última antiga");
            Afirmar.Igual(0xFFu, Paleta.Argb(nova) >> 24, $"{nova}: opaca");
        }
        uint[] valores = [.. todas.Select(Paleta.Argb)];
        Afirmar.Igual(valores.Length, valores.Distinct().Count(), "duas cores com o mesmo ARGB");
    }

    [Teste]
    public void LegendaComLetraRepetidaLanca()
    {
        Afirmar.Lanca<ArgumentException>(() => Carimbo.MontarLegenda([('K', Cor.Contorno), ('W', Cor.Branco), ('K', Cor.Pelo)]), "letra repetida");
        IReadOnlyDictionary<char, Cor> legenda = Carimbo.MontarLegenda([('K', Cor.Contorno), ('W', Cor.Branco)]);
        Afirmar.Igual(2, legenda.Count, "letras distintas entram");
        Afirmar.Igual(Cor.Branco, legenda['W']);
    }

    [Teste]
    public void LegendaPadraoTemUmaLetraPorCorESemPontoNemEspaco()
    {
        IReadOnlyDictionary<char, Cor> legenda = Carimbo.Legenda;
        Afirmar.Falso(legenda.ContainsKey('.') || legenda.ContainsKey(' '), "'.' e ' ' são transparentes");
        Afirmar.Falso(legenda.Values.Contains(Cor.Nada), "a legenda não pinta transparente");
        Afirmar.Igual(legenda.Count, legenda.Values.Distinct().Count(), "duas letras para a mesma cor");
        // Letras dos carimbos antigos continuam com a mesma cor.
        foreach ((char letra, Cor cor) in new[] { ('K', Cor.Contorno), ('W', Cor.Branco), ('c', Cor.Creme), ('f', Cor.Faixa), ('h', Cor.Palha), ('v', Cor.Sobrancelha) })
            Afirmar.Igual(cor, legenda[letra], $"letra '{letra}'");
    }

    [Teste]
    public void CarimboGiradoEhOMesmoQueGirarATela()
    {
        // Não quadrado e sem simetria, para pegar troca de eixo ou de sentido.
        var carimbo = new Carimbo(
            "KW.",
            "fF.",
            "..e",
            "ph.");
        const int n = 7;
        foreach (bool horario in new[] { true, false })
        {
            var antes = new Tela(n, n);
            antes.Carimbar(carimbo, 0, 0);
            Tela esperada = antes.Girada(horario);

            Carimbo girado = carimbo.Girado(horario);
            Afirmar.Igual((carimbo.Altura, carimbo.Largura), (girado.Largura, girado.Altura), $"horário={horario}: troca largura e altura");
            var obtida = new Tela(n, n);
            // Girar a tela leva o canto (0, 0) do carimbo para o canto de cima à direita (horário) ou de baixo à esquerda.
            obtida.Carimbar(girado, horario ? n - carimbo.Altura : 0, horario ? 0 : n - carimbo.Largura);
            Afirmar.Sequencia(esperada.ParaArgb(), obtida.ParaArgb(), $"horário={horario}");
        }
    }

    [Teste]
    public void QuatroGirosOuIdaEVoltaDevolvemOCarimbo()
    {
        var carimbo = new Carimbo("KWW.", ".fFe", "p..h");
        Carimbo quatro = carimbo.Girado(true).Girado(true).Girado(true).Girado(true);
        Carimbo idaEVolta = carimbo.Girado(false).Girado(true);
        foreach (Carimbo c in new[] { quatro, idaEVolta })
        {
            Afirmar.Igual((carimbo.Largura, carimbo.Altura), (c.Largura, c.Altura));
            for (int y = 0; y < carimbo.Altura; y++)
                for (int x = 0; x < carimbo.Largura; x++)
                    Afirmar.Igual(carimbo[x, y], c[x, y], $"pixel ({x},{y})");
        }
    }
}
