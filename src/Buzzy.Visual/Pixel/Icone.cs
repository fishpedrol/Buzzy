namespace Buzzy.Visual.Pixel;

// Ícone da bandeja: cabeça com chapéu de palha feita à mão em 16x16 (ícone pequeno a 100%).
// Reduzir o sprite de 64 px apagaria olhos e boca, por isso tem desenho próprio.
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
