using Buzzy.Core;

namespace Buzzy.App.Composicao;

// Posição da janela de configurações, em px físicos: centrada na área útil do
// monitor do personagem; se cobrir o sprite, vai pra metade oposta. A janela não é
// topmost, então o sprite cobriria os controles se ela abrisse em cima dele.
internal static class LugarDasConfiguracoes
{
    // Usa a topologia atual: o último monitor do personagem pela chave ou, se ele
    // foi desconectado com o Buzzy escondido, o mais perto dos pés guardados.
    internal static MonitorDoDesktop Monitor(Topologia atual, MonitorDoDesktop ultimo, RetanguloPx personagem)
        => atual.PorChave(ultimo.Chave) ?? atual.MonitorMaisProximo(new PontoPx(personagem.Esquerda + (personagem.Largura / 2), personagem.Base - 1));

    // Em DIP: a área útil inteira; o resto rola.
    internal static double AlturaMaximaDip(MonitorDoDesktop monitor) => monitor.AreaUtil.Altura * 96.0 / monitor.Dpi;

    internal static RetanguloPx Calcular(RetanguloPx area, RetanguloPx? personagem, TamanhoPx janela)
    {
        int w = janela.Largura, h = janela.Altura;
        int x = area.Esquerda + ((area.Largura - w) / 2), y = area.Topo + ((area.Altura - h) / 2);
        var centro = new RetanguloPx(x, y, x + w, y + h);
        if (personagem is { } p && centro.Intersecta(p))
        {
            bool spriteAEsquerda = p.Esquerda + (p.Largura / 2) < area.Esquerda + (area.Largura / 2);
            int metade = area.Largura / 2;
            x = spriteAEsquerda ? area.Esquerda + metade + ((metade - w) / 2) : area.Esquerda + ((metade - w) / 2);
            centro = new RetanguloPx(x, y, x + w, y + h);
        }
        return AncoragemDoPainel.Preso(centro, area);
    }
}
