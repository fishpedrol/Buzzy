using System.Windows;

namespace BuzzySpike;

internal static class Programa
{
    private const string Uso = """
        BuzzySpike — protótipos descartáveis da Etapa 0B do Buzzy.

        Uso: BuzzySpike.exe --modo <modo> [--x N] [--y N]

        Modos:
          p1            P1: figura de teste parada, registra qual faixa recebeu cada clique.
          p3            P3: arraste com captura do mouse, sem ativar a janela.
          p3-margem     P3, recuo: igual, mas com margem alfa 1 durante o gesto.
          p2-repouso    P2: janela parada, sem timer nenhum. Para medir repouso.
          p2-anim10     P2: animação a 10 quadros por segundo.
          p2-anim60     P2: animação a 60 quadros por segundo, com DispatcherTimer.
          p2-anim60comp P2: animação a 60 quadros por segundo, pelo compositor do WPF.
          receptor      P3: janela comum com caixa de texto que faz o papel do aplicativo
                        do usuário; registra só o que ela mesma recebe (ferramentas/SondaP3).

        --x e --y posicionam a janela em pixels físicos do desktop virtual.
        Sem eles, a janela vai para o centro da área útil do monitor primário.
        """;

    [STAThread]
    internal static int Main(string[] argumentos)
    {
        Modo? modo = null;
        int? x = null;
        int? y = null;

        for (int i = 0; i < argumentos.Length; i++)
        {
            switch (argumentos[i])
            {
                case "--modo" when i + 1 < argumentos.Length:
                    modo = Interpretar(argumentos[++i]);
                    if (modo is null)
                    {
                        Reclamar($"Modo desconhecido: {argumentos[i]}");
                        return 2;
                    }
                    break;

                case "--x" when i + 1 < argumentos.Length:
                    x = int.Parse(argumentos[++i]);
                    break;

                case "--y" when i + 1 < argumentos.Length:
                    y = int.Parse(argumentos[++i]);
                    break;

                case "--ajuda" or "-h" or "--help":
                    Reclamar(Uso);
                    return 0;

                default:
                    Reclamar($"Argumento não reconhecido: {argumentos[i]}\n\n{Uso}");
                    return 2;
            }
        }

        if (modo is null)
        {
            Reclamar($"Falta --modo.\n\n{Uso}");
            return 2;
        }

        Diagnostico.Iniciar(NomeDoModo(modo.Value));

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Window janela = modo == Modo.Receptor
            ? new JanelaReceptor(x, y)
            : new JanelaSpike(modo.Value, x, y);
        return app.Run(janela);
    }

    private static Modo? Interpretar(string texto) => texto.ToLowerInvariant() switch
    {
        "p1" => Modo.P1,
        "p3" => Modo.P3,
        "p3-margem" => Modo.P3Margem,
        "p2-repouso" => Modo.P2Repouso,
        "p2-anim10" => Modo.P2Anim10,
        "p2-anim60" => Modo.P2Anim60,
        "p2-anim60comp" => Modo.P2Anim60Comp,
        "receptor" => Modo.Receptor,
        _ => null,
    };

    private static string NomeDoModo(Modo modo) => modo switch
    {
        Modo.P1 => "p1",
        Modo.P3 => "p3",
        Modo.P3Margem => "p3-margem",
        Modo.P2Repouso => "p2-repouso",
        Modo.P2Anim10 => "p2-anim10",
        Modo.P2Anim60 => "p2-anim60",
        Modo.P2Anim60Comp => "p2-anim60comp",
        Modo.Receptor => "receptor",
        _ => "desconhecido",
    };

    // O protótipo é WinExe e não tem console próprio. Erros de linha de comando aparecem
    // numa caixa de diálogo, que é o único jeito de serem vistos quando o processo é
    // iniciado pelo Explorer.
    private static void Reclamar(string texto)
        => MessageBox.Show(texto, "BuzzySpike", MessageBoxButton.OK, MessageBoxImage.Information);
}
