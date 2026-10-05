namespace Buzzy.Visual.Pixel;

/// <summary>
/// Ícone da bandeja: a cabeça do Buzzy, com o chapéu de palha, desenhada à mão em 16 × 16 pixels (o tamanho do ícone
/// pequeno a 100%). Reduzir o sprite de 64 pixels apagaria olhos e boca; por isso o ícone tem
/// desenho próprio, com a mesma paleta.
/// </summary>
public static class Icone
{
    public const int Lado = 16;

    private static readonly Carimbo Desenho = new(
        "......KKKK......",
        "....KKhhHhKK....",
        "...KhjhhhhjhK...",
        "...KffffffffK...",
        ".KKhhhhhhhhhhKK.",
        "KjhhhhhhhhhhhhjK",
        ".KKKKKKKKKKKKKK.",
        ".KoKpccccccpKoK.",
        "KooKcWuccWucKooK",
        "KooKcuuccuucKooK",
        ".KKKcccoocccKKK.",
        "...KcKccccKcK...",
        "....KcKKKKcK....",
        ".....KsccsK.....",
        "......KKKK......",
        "................");

    public static Tela Desenhar()
    {
        var tela = new Tela(Lado, Lado);
        tela.Carimbar(Desenho, 0, 0);
        return tela;
    }
}
