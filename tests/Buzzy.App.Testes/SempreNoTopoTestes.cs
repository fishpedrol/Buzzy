using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P7 (DEC-038, item 8; critério 8): o "sempre no topo" desligável. O personagem segue a preferência, e
/// <c>AoTopoDaFaixa</c> desligado nunca o deixa topmost; as janelas dos itens seguem o personagem, inclusive as criadas
/// depois. Sem mostrar janelas.
/// </summary>
internal sealed class SempreNoTopoTestes
{
    [Teste]
    public void Personagem_SegueAPreferencia()
    {
        var janela = new JanelaPersonagem();
        Afirmar.Verdadeiro(janela.SempreNoTopo, "ligado por padrão");
        janela.AplicarSempreNoTopo(false);
        Afirmar.Falso(janela.SempreNoTopo || janela.Topmost, "desligado");
        janela.AoTopoDaFaixa();
        Afirmar.Falso(janela.Topmost, "subir à frente não o deixa topmost");
        janela.AplicarSempreNoTopo(true);
        Afirmar.Verdadeiro(janela.Topmost, "religado");
        janela.Close();
    }

    [Teste]
    public void Itens_SeguemOPersonagem_InclusiveOsNovos()
    {
        var falsas = new List<JanelaDoItemFalsa>();
        var gerente = new GerenteDosItens(id =>
        {
            var j = new JanelaDoItemFalsa(id);
            falsas.Add(j);
            return j;
        }, () => 77);
        var monitor = new MonitorDoDesktop("m1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1040), 96, true);
        var lugar = new Posicionamento(monitor, new PontoPx(500, 1032), new TamanhoPx(48, 48), new RetanguloPx(476, 984, 524, 1032));
        gerente.Executar(new MostrarItem(1, Item.Banana, lugar));
        Afirmar.Falso(falsas[0].Chamadas.Any(c => c.StartsWith("topo", StringComparison.Ordinal)), "com o padrão, nada a aplicar");
        gerente.AplicarSempreNoTopo(false);
        Afirmar.Verdadeiro(falsas[0].Chamadas.Contains("topo nao"), "a janela que existe desliga");
        gerente.Executar(new MostrarItem(2, Item.Cerveja, lugar));
        Afirmar.Verdadeiro(falsas[1].Chamadas.Contains("topo nao"), "a nova nasce desligada");
        gerente.AplicarSempreNoTopo(true);
        Afirmar.Verdadeiro(falsas.All(f => f.Chamadas.Contains("topo sim")), "religar vale para todas");
    }
}
