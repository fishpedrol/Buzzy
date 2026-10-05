using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P8 (DEC-038, item 9): o tamanho é dado, não estado global. A janela do personagem recebe o tamanho no
/// construtor, e o sprite é renderizado no tamanho pedido, com o tamanho na chave do cache (dois tamanhos não se misturam).
/// Os itens continuam com 48 DIP.
/// </summary>
internal sealed class EscalaNaTelaTestes
{
    [Teste]
    public void Janela_ESprite_NoTamanhoPedido()
    {
        foreach (EscalaDoPersonagem escala in Enum.GetValues<EscalaDoPersonagem>())
        {
            TamanhoDip tamanho = ConfiguracaoDoNucleo.TamanhoDoPersonagem(escala);
            var janela = new JanelaPersonagem(tamanho);
            Afirmar.Igual(((double)tamanho.Largura, (double)tamanho.Altura, tamanho), (janela.Width, janela.Height, janela.Tamanho), $"{escala}: a janela");
            janela.Close();
            var quadro = new QuadroDoSprite("parado", false, "neutro");
            foreach (int dpi in new[] { 96, 144, 192 })
            {
                var bmp = SpriteProvisorio.Renderizar(quadro, dpi, tamanho);
                TamanhoPx esperado = tamanho.ParaPixels(dpi);
                Afirmar.Igual((esperado.Largura, esperado.Altura), (bmp.PixelWidth, bmp.PixelHeight), $"{escala} a {dpi} DPI: o sprite");
            }
        }
        // O mesmo quadro e DPI em dois tamanhos: dois bitmaps, cada um no seu tamanho (o tamanho está na chave do cache).
        var q = new QuadroDoSprite("parado", false, "feliz");
        Afirmar.Igual((128, 192), (SpriteProvisorio.Renderizar(q, 96, new TamanhoDip(128, 128)).PixelWidth, SpriteProvisorio.Renderizar(q, 96, new TamanhoDip(192, 192)).PixelWidth), "o cache não mistura tamanhos");
        Afirmar.Igual(new TamanhoDip(48, 48), SpriteDoItem.TamanhoLogico, "o item em 48 DIP");
        Afirmar.Igual(new TamanhoDip(128, 128), SpriteProvisorio.TamanhoLogico, "o padrão é o Médio");
    }
}
