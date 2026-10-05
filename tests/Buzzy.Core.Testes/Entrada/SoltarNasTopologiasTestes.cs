using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Entrada;

/// <summary>
/// Validação ao soltar sobre as topologias de exemplo (Fase 3, critério 2 e "Testes
/// automatizados"): arrastar o personagem para uma grade de pontos que cobre todos os monitores,
/// os vãos entre eles e o lado de fora, e soltar. A conta do resultado esperado é feita aqui,
/// de forma independente do núcleo, a partir das regras de ARCHITECTURE.md 2.7 (passos 4 e 5).
/// </summary>
internal static class SoltarNasTopologiasTestes
{
    /// <summary>Pegada: o cursor segura o personagem 40 px acima dos pés.</summary>
    private const int AlturaDaPegada = 40;

    private const int Colunas = 29;
    private const int Linhas = 17;

    [Teste]
    public static void SoltarEmQualquerPonto_DeixaAAncoraNaAreaUtilDoMonitorCertoComOsPesNoChao()
    {
        var config = new ConfiguracaoDoNucleo { QuedaFisica = false };
        int casos = 0, emVao = 0;
        foreach ((string nome, Topologia topologia) in TopologiasDeExemplo.Todas)
        {
            int esquerda = topologia.Monitores.Min(m => m.Tela.Esquerda) - 400;
            int topo = topologia.Monitores.Min(m => m.Tela.Topo) - 400;
            int direita = topologia.Monitores.Max(m => m.Tela.Direita) + 400;
            int baseY = topologia.Monitores.Max(m => m.Tela.Base) + 400;

            for (int i = 0; i < Colunas; i++)
            {
                for (int j = 0; j < Linhas; j++)
                {
                    var alvo = new PontoPx(esquerda + (direita - esquerda) * i / (Colunas - 1), topo + (baseY - topo) * j / (Linhas - 1));
                    Cenario c = Cenario.Parado(config, topologia);
                    PontoPx pes = c.Ancora;
                    var pegar = new PontoPx(pes.X, pes.Y - AlturaDaPegada);
                    c.Aplicar(new Press(pegar), new DragStart(), new DragMove(alvo));

                    // Durante o arraste, a âncora segue o cursor sem limite (invariante 2).
                    var desejada = new PontoPx(alvo.X, alvo.Y + AlturaDaPegada);
                    Afirmar.Igual(desejada, c.Ancora, $"{nome}: âncora durante o arraste até {alvo}");

                    c.Aplicar(new DragEnd(alvo)).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
                    Posicionamento lugar = Afirmar.NaoNulo(c.Atual.Lugar, "lugar depois de soltar");
                    c.Efeito<GravarPosicao>();

                    (MonitorDoDesktop esperado, bool vao) = MonitorEsperado(topologia, desejada);
                    if (vao) emVao++;
                    string onde = $"{nome}: soltar em {alvo} (âncora pedida {desejada})";
                    Afirmar.Igual(esperado.Chave, lugar.Monitor.Chave, $"{onde}: monitor");

                    RetanguloPx area = esperado.AreaUtil;
                    TamanhoPx tamanho = config.Tamanho.ParaPixels(esperado.Dpi);
                    int metade = tamanho.Largura / 2;
                    int xEsperado = Math.Clamp(desejada.X, area.Esquerda + metade, area.Direita - (tamanho.Largura - metade));
                    Afirmar.Igual(new PontoPx(xEsperado, area.Base), lugar.Ancora, $"{onde}: âncora presa na área útil, com os pés no chão");
                    Afirmar.Verdadeiro(area.Contem(lugar.Retangulo), $"{onde}: sprite {lugar.Retangulo} inteiro na área útil {area}");
                    Afirmar.Igual(tamanho, lugar.Tamanho, $"{onde}: tamanho físico no DPI do monitor escolhido");
                    casos++;
                }
            }
        }
        Console.WriteLine($"         {casos} soltares em {TopologiasDeExemplo.Todas.Count} topologias; {emVao} com a âncora num vão ou fora de todos os monitores");
        Afirmar.Verdadeiro(emVao > 100, $"a grade cobriu vãos e o lado de fora ({emVao})");
    }

    [Teste]
    public static void CancelarOArraste_DeixaOPersonagemOndeEstavaNaValidacaoNormal()
    {
        // ARCHITECTURE.md 2.7, passo 6: não volta ao ponto de origem.
        var config = new ConfiguracaoDoNucleo { QuedaFisica = false };
        Topologia topologia = TopologiasDeExemplo.SecundarioAEsquerda;
        Cenario c = Cenario.Parado(config, topologia);
        PontoPx pes = c.Ancora;
        c.Aplicar(new Press(new PontoPx(pes.X, pes.Y - AlturaDaPegada)), new DragStart(), new DragMove(new PontoPx(-700, 400)));
        c.Aplicar(new DragCancel()).Percorreu(Estado.Dragging, Estado.Settling, Estado.Idle);
        Afirmar.Igual(@"\\.\DISPLAY2", c.Atual.Lugar!.Monitor.Chave, "fica no monitor para onde foi arrastado");
        Afirmar.Igual(new PontoPx(-700, 1032), c.Ancora, "no mesmo x, com os pés no chão do secundário");
    }

    /// <summary>
    /// Monitor da âncora pela regra de <c>SETTLING</c>: o que contém o pixel logo acima dela; num
    /// vão ou fora de todos, o de tela mais próxima (empate: o principal, depois a ordem da lista).
    /// </summary>
    private static (MonitorDoDesktop Monitor, bool Vao) MonitorEsperado(Topologia topologia, PontoPx ancora)
    {
        var acima = new PontoPx(ancora.X, ancora.Y - 1);
        foreach (MonitorDoDesktop m in topologia.Monitores)
        {
            RetanguloPx t = m.Tela;
            if (acima.X >= t.Esquerda && acima.X < t.Direita && acima.Y >= t.Topo && acima.Y < t.Base) return (m, false);
        }

        MonitorDoDesktop? melhor = null;
        long melhorDistancia = long.MaxValue;
        foreach (MonitorDoDesktop m in topologia.Monitores)
        {
            RetanguloPx t = m.Tela;
            long dx = acima.X < t.Esquerda ? t.Esquerda - (long)acima.X : acima.X > t.Direita - 1 ? acima.X - (t.Direita - 1L) : 0;
            long dy = acima.Y < t.Topo ? t.Topo - (long)acima.Y : acima.Y > t.Base - 1 ? acima.Y - (t.Base - 1L) : 0;
            long d = dx * dx + dy * dy;
            if (d < melhorDistancia || (d == melhorDistancia && m.Principal && melhor is { Principal: false }))
            {
                melhor = m;
                melhorDistancia = d;
            }
        }
        return (melhor!, true);
    }
}
