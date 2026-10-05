using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// Onde o painel compacto de energia abre (Fase 8; DEC-038, item 4): ao lado do personagem, do lado com mais espaço, com uma
/// folga, centrado na vertical com o sprite; no esconderijo de baixo, acima, e no de cima, abaixo; sempre inteiro na área útil
/// do monitor da âncora. Nunca sobre o sprite, a não ser que não caiba em lugar nenhum: aí, no canto de cima e da esquerda da
/// área útil. Tudo em pixels físicos. Função pura.
/// </summary>
internal static class AncoragemDoPainel
{
    /// <param name="personagem">O retângulo da janela do personagem.</param>
    /// <param name="painel">O tamanho do painel no DPI do monitor.</param>
    /// <param name="area">A área útil do monitor da âncora.</param>
    /// <param name="folga">A folga entre o painel e o sprite.</param>
    /// <param name="esconderijo">A borda do esconderijo, se ele estiver escondido.</param>
    internal static RetanguloPx Calcular(RetanguloPx personagem, TamanhoPx painel, RetanguloPx area, int folga, LadoDoEsconderijo esconderijo = LadoDoEsconderijo.Nenhum)
    {
        int w = painel.Largura, h = painel.Altura;
        int meioX = personagem.Esquerda + (personagem.Largura / 2) - (w / 2);
        int meioY = personagem.Topo + (personagem.Altura / 2) - (h / 2);
        var direita = new PontoPx(personagem.Direita + folga, meioY);
        var esquerda = new PontoPx(personagem.Esquerda - folga - w, meioY);
        var acima = new PontoPx(meioX, personagem.Topo - folga - h);
        var abaixo = new PontoPx(meioX, personagem.Base + folga);
        bool maisADireita = area.Direita - personagem.Direita >= personagem.Esquerda - area.Esquerda;
        PontoPx[] candidatos = esconderijo switch
        {
            LadoDoEsconderijo.Baixo => [acima, maisADireita ? direita : esquerda, maisADireita ? esquerda : direita, abaixo],
            LadoDoEsconderijo.Cima => [abaixo, maisADireita ? direita : esquerda, maisADireita ? esquerda : direita, acima],
            _ => [maisADireita ? direita : esquerda, maisADireita ? esquerda : direita, acima, abaixo],
        };
        foreach (PontoPx p in candidatos)
        {
            RetanguloPx r = Preso(new RetanguloPx(p.X, p.Y, p.X + w, p.Y + h), area);
            if (area.Contem(r) && !r.Intersecta(personagem)) return r;
        }
        return new RetanguloPx(area.Esquerda, area.Topo, area.Esquerda + w, area.Topo + h);
    }

    /// <summary>O retângulo deslocado para dentro da área, sem mudar o tamanho (maior que ela, encostado à esquerda e ao topo).</summary>
    internal static RetanguloPx Preso(RetanguloPx r, RetanguloPx area)
    {
        int x = Math.Max(area.Esquerda, Math.Min(r.Esquerda, area.Direita - r.Largura));
        int y = Math.Max(area.Topo, Math.Min(r.Topo, area.Base - r.Altura));
        return new RetanguloPx(x, y, x + r.Largura, y + r.Altura);
    }
}
