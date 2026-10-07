using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Posição do painel de energia, em px físicos: ao lado do personagem, do lado com
// mais espaço, centrado na vertical; escondido embaixo abre acima, escondido em cima
// abre abaixo. Nunca sobre o sprite; se não couber em lugar nenhum, canto superior esquerdo.
internal static class AncoragemDoPainel
{
    // painel já no DPI do monitor; area é a área útil do monitor da âncora.
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

    // Empurra pra dentro sem mudar o tamanho; se for maior que a área, encosta à esquerda e no topo.
    internal static RetanguloPx Preso(RetanguloPx r, RetanguloPx area)
    {
        int x = Math.Max(area.Esquerda, Math.Min(r.Esquerda, area.Direita - r.Largura));
        int y = Math.Max(area.Topo, Math.Min(r.Topo, area.Base - r.Altura));
        return new RetanguloPx(x, y, x + r.Largura, y + r.Altura);
    }
}
