using Buzzy.Core;

namespace Buzzy.App.Composicao;

/// <summary>
/// Onde a janela de configurações abre (Fase 8; DEC-038, item 5): centrada na área útil do último monitor do personagem (com ele
/// escondido, sem o sprite na conta); se o centro cobrir o sprite, na metade oposta a ele; sempre presa na área útil. A janela
/// nunca é topmost, então ficar longe do sprite é o que evita que ele cubra os controles. Função pura, em pixels físicos.
/// </summary>
internal static class LugarDasConfiguracoes
{
    /// <summary>
    /// O monitor onde as janelas do Buzzy (painel e configurações) abrem, pela topologia ATUAL: o último monitor do personagem,
    /// pela chave, com a área útil de agora; se ele saiu (desconectado com o Buzzy escondido), o mais próximo dos pés guardados.
    /// Nunca uma área de um monitor que não existe mais.
    /// </summary>
    internal static MonitorDoDesktop Monitor(Topologia atual, MonitorDoDesktop ultimo, RetanguloPx personagem)
        => atual.PorChave(ultimo.Chave) ?? atual.MonitorMaisProximo(new PontoPx(personagem.Esquerda + (personagem.Largura / 2), personagem.Base - 1));

    /// <summary>A altura máxima da janela, em DIP do monitor: a área útil inteira (o resto rola).</summary>
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
