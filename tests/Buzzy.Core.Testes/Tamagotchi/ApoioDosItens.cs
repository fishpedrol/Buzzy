using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// O que os testes dos itens, do uso e da onda (DEC-028, passo T5) usam em comum: as configurações com o tamagotchi
/// ligado e os gestos sobre um item, sempre pela própria máquina.
/// </summary>
internal static class ApoioDosItens
{
    public static readonly TamanhoDip Sprite = new(128, 128);

    /// <summary>Sem a física (o personagem só se move pelos sinais de movimento), com o tamagotchi ligado.</summary>
    public static ConfiguracaoDoNucleo SemFisica() => new() { Tamagotchi = true };

    /// <summary>
    /// Uma chance de paranoia que nunca sai (0 em 1: o sorteio acontece, mas não sai). Os testes que misturam uma droga
    /// sintética com outra substância sem falar da paranoia a usam, para não dependerem de a semente não sortear a paranoia;
    /// ela tem os testes dela (ParanoiaTestes).
    /// </summary>
    public static readonly Chance NuncaParanoia = new(0, 1);

    /// <summary>
    /// A configuração do aplicativo, com a física, e o tamagotchi ligado de forma explícita: o aplicativo o liga desde o
    /// passo T9, e estes testes não dependem de a configuração dele continuar assim.
    /// </summary>
    public static ConfiguracaoDoNucleo ComFisica() => ConfiguracaoDoNucleo.DoAplicativo(Sprite) with { Tamagotchi = true };

    /// <summary>Invoca o item e deixa cair até parar no chão; devolve o item parado.</summary>
    public static ItemNoMundo InvocarEAssentar(Cenario c, Item item)
    {
        c.Aplicar(new CmdSummonItem(item));
        int id = c.Atual.ProximoIdDeItem - 1;
        for (int i = 0; i < 600 && Afirmar.NaoNulo(c.Atual.Itens.PorId(id), $"o item {id}").Situacao == SituacaoDoItem.Caindo; i++) c.Aplicar(new Tick());
        ItemNoMundo parado = Afirmar.NaoNulo(c.Atual.Itens.PorId(id), $"o item {id}");
        Afirmar.Igual(SituacaoDoItem.NoChao, parado.Situacao, $"o item {id} parou no chão");
        return parado;
    }

    /// <summary>Invoca o item e deixa cair até parar no chão, pelo relógio do simulador; devolve o item parado.</summary>
    public static ItemNoMundo InvocarEAssentar(SimuladorDeTempo sim, Item item)
    {
        sim.Aplicar(new CmdSummonItem(item));
        int id = sim.Estado.ProximoIdDeItem - 1;
        for (int i = 0; i < 600 && Afirmar.NaoNulo(sim.Estado.Itens.PorId(id), $"o item {id}").Situacao == SituacaoDoItem.Caindo; i++) sim.Passos(1);
        ItemNoMundo parado = Afirmar.NaoNulo(sim.Estado.Itens.PorId(id), $"o item {id}");
        Afirmar.Igual(SituacaoDoItem.NoChao, parado.Situacao, $"o item {id} parou no chão");
        return parado;
    }

    /// <summary>Os eventos do gesto inteiro sobre um item: pega 10 px acima da âncora, arrasta e solta com a âncora em <paramref name="ancoraDoItem"/>.</summary>
    public static Evento[] Arraste(ItemNoMundo item, PontoPx ancoraDoItem)
    {
        var cursor = new PontoPx(ancoraDoItem.X, ancoraDoItem.Y - 10);
        return
        [
            new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)),
            new ItemDragStart(item.Id), new ItemDragMove(item.Id, cursor), new ItemDragEnd(item.Id, cursor),
        ];
    }

    /// <summary>Arrasta o item até o meio do personagem e solta, pelo simulador.</summary>
    public static void SoltarSobreEle(SimuladorDeTempo sim, int id)
    {
        ItemNoMundo item = Afirmar.NaoNulo(sim.Estado.Itens.PorId(id), $"o item {id}");
        foreach (Evento e in Arraste(item, Cenario.MeioDoPersonagem(sim.Estado, sim.Nucleo.Configuracao))) sim.Aplicar(e);
    }

    /// <summary>Os efeitos das janelas dos itens, na ordem.</summary>
    public static IEnumerable<Efeito> DosItens(IEnumerable<Efeito> efeitos)
        => efeitos.Where(e => e is MostrarItem or MoverItem or EsconderItem or RemoverItem or LiberarCapturaDoItem);

    /// <summary>Solta o personagem, pela mão do usuário, com a âncora em <paramref name="destino"/> (pega 30 px acima da âncora).</summary>
    public static void SoltarPersonagemEm(SimuladorDeTempo sim, PontoPx destino)
    {
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        var pegar = new PontoPx(l.Ancora.X, l.Ancora.Y - 30);
        var cursor = new PontoPx(destino.X, destino.Y - 30);
        foreach (Evento e in new Evento[] { new Press(pegar), new DragStart(), new DragMove(cursor), new DragEnd(cursor) }) sim.Aplicar(e);
    }

    /// <summary>As superfícies do personagem no monitor dele.</summary>
    public static Superficies Sup(EstadoDoNucleo s)
    {
        Posicionamento l = Afirmar.NaoNulo(s.Lugar, "lugar");
        return Superficies.Do(Afirmar.NaoNulo(s.Topologia, "topologia"), l.Monitor, l.Tamanho);
    }
}
