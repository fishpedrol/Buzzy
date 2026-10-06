using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// A seleção individual dos itens adultos (DEC-041, pedido do usuário de 2026-10-05): o padrão (a chave geral desligada;
/// vodka, cerveja e cigarro marcados), o comando de cada item, a invocação recusada de um item desmarcado e o que
/// desmarcar faz no mundo, no uso, nas ondas e na mistura da paranoia, sem sortear nada de novo e sem mexer nos outros
/// itens. O esperado vem da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class ItensAdultosIndividuaisTestes
{
    private static readonly Chance Sempre = new(1, 1);

    private static ConjuntoDeItens Conjunto(params Item[] itens) => itens.Aggregate(ConjuntoDeItens.Vazio, (c, i) => c.Com(i));

    [Teste]
    public static void Padrao_ChaveDesligada_TresMarcados_ENoveAdultos()
    {
        Afirmar.Falso(Preferencias.Padrao.ConteudoAdulto, "a chave geral desligada por padrão");
        Afirmar.Igual(Conjunto(Item.Vodka, Item.Cerveja, Item.Cigarro), Preferencias.Padrao.ItensAdultosHabilitados, "os três marcados, sem o baseado");
        Afirmar.Igual(Conjunto(Item.Vodka, Item.Cerveja, Item.Baseado, Item.Cigarro, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala),
            Preferencias.TodosOsItensAdultos, "os nove adultos");
        Afirmar.Igual(Conjunto(Item.Vodka), Preferencias.NormalizarItensAdultos(Conjunto(Item.Vodka, Item.Banana, Item.Agua)), "normalizar tira os de alívio");
    }

    // O comando grava a escolha (um GravarPreferencias) numa transição para o mesmo estado; repetir não faz nada; um item de
    // alívio, um fora do enum e o comando antes da carga são ignorados; com a chave geral desligada, a escolha também vale.
    [Teste]
    public static void Comando_GravaAEscolha_EIgnoraOQueNaoEhAdulto()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.Aplicar(new CmdSetAdultItemEnabled(Item.Md, false));
        Afirmar.Falso(c.Atual.Preferencias.ItensAdultosHabilitados.Contem(Item.Md), "o MD desmarcado");
        Afirmar.Falso(c.Efeito<GravarPreferencias>().Preferencias.ItensAdultosHabilitados.Contem(Item.Md), "a escolha gravada");
        Afirmar.Igual((Estado.Idle, Estado.Idle), (c.Transicoes[0].De, c.Transicoes[0].Para), "no mesmo estado");
        c.Aplicar(new CmdSetAdultItemEnabled(Item.Md, false)).SemTransicao().SemEfeito<GravarPreferencias>();

        EstadoDoNucleo antes = c.Atual;
        foreach (Evento e in new Evento[] { new CmdSetAdultItemEnabled(Item.Banana, false), new CmdSetAdultItemEnabled(Item.Agua, true), new CmdSetAdultItemEnabled(Item.Cafe, true), new CmdSetAdultItemEnabled((Item)99, false), new CmdSetAdultItemEnabled((Item)(-1), true) })
        {
            c.Aplicar(e);
            Afirmar.Igual((antes, 0), (c.Atual, c.Efeitos.Count), $"{e}: ignorado");
        }

        Resultado antesDaCarga = Maquina.Aplicar(EstadoDoNucleo.Inicial(1), new CmdSetAdultItemEnabled(Item.Vodka, false), SemFisica());
        Afirmar.Igual((Preferencias.Padrao, 0), (antesDaCarga.Estado.Preferencias, antesDaCarga.Efeitos.Count), "antes da carga: ignorado");

        Cenario desligado = Cenario.Parado(SemFisica()).Aplicar(new CmdSetAdultContent(false));
        desligado.Aplicar(new CmdSetAdultItemEnabled(Item.Bala, false));
        Afirmar.Falso(desligado.Atual.Preferencias.ItensAdultosHabilitados.Contem(Item.Bala), "com a chave geral desligada, a escolha também vale");
        desligado.Aplicar(new CmdSummonItem(Item.Vodka));
        Afirmar.Igual(0, desligado.Atual.Itens.Quantidade, "mas nada adulto nasce com a chave desligada");
    }

    // Desmarcado, o item não nasce; os outros adultos e os de alívio nascem; remarcar só grava, sem criar item nem uso.
    [Teste]
    public static void Desmarcado_NaoNasce_EReMarcarSoGrava()
    {
        Cenario c = Cenario.Parado(SemFisica()).Aplicar(new CmdSetAdultItemEnabled(Item.Cocaina, false));
        c.Aplicar(new CmdSummonItem(Item.Cocaina));
        Afirmar.Igual(0, c.Atual.Itens.Quantidade, "a cocaína desmarcada não nasce");
        c.Aplicar(new CmdSummonItem(Item.Cerveja));
        c.Aplicar(new CmdSummonItem(Item.Banana));
        Afirmar.Igual(2, c.Atual.Itens.Quantidade, "a cerveja e a banana nascem");

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Cocaina, true));
        Afirmar.Igual((2, Estado.Idle), (c.Atual.Itens.Quantidade, c.Atual.Estado), "remarcar não cria item nem uso");
        Afirmar.Sequencia([typeof(GravarPreferencias)], c.Efeitos.Select(e => e.GetType()), "só grava");
    }

    // Desmarcar recolhe só as instâncias daquele item; a da mão do usuário solta a captura antes.
    [Teste]
    public static void Desmarcar_RecolheSoAsInstanciasDoItem_ESoltaACaptura()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo vodka = InvocarEAssentar(c, Item.Vodka);
        ItemNoMundo cerveja = InvocarEAssentar(c, Item.Cerveja);
        ItemNoMundo outraVodka = InvocarEAssentar(c, Item.Vodka);
        c.Aplicar(new ItemPress(outraVodka.Id, new PontoPx(outraVodka.Lugar.Ancora.X, outraVodka.Lugar.Ancora.Y - 10)));

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Vodka, false));
        Afirmar.Sequencia([cerveja.Id], c.Atual.Itens.Todos.Select(i => i.Id), "só a cerveja fica");
        Afirmar.Igual(2, c.Efeitos.OfType<RemoverItem>().Count(r => r.Motivo == MotivoDaRemocao.Recolhido && (r.Id == vodka.Id || r.Id == outraVodka.Id)), "as duas vodkas recolhidas");
        int captura = c.Efeitos.ToList().FindIndex(e => e is LiberarCapturaDoItem l && l.Id == outraVodka.Id);
        int remocao = c.Efeitos.ToList().FindIndex(e => e is RemoverItem r && r.Id == outraVodka.Id);
        Afirmar.Verdadeiro(captura >= 0 && remocao > captura, "solta a captura antes de remover a janela");
        Afirmar.Falso(c.Atual.Atento, "não está mais atento");
    }

    // Desmarcar o item em uso termina o uso no mesmo lugar; desmarcar outro item não mexe no uso.
    [Teste]
    public static void Desmarcar_OItemEmUso_TerminaSoEsseUso()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.SoltarSobreEle(InvocarEAssentar(c, Item.Cigarro).Id).Esta(Estado.Using, "fumando o cigarro");
        int restantes = c.Atual.PassosRestantes;
        c.Aplicar(new CmdSetAdultItemEnabled(Item.Bala, false));
        Afirmar.Igual((Estado.Using, Item.Cigarro, restantes), (c.Atual.Estado, c.Atual.Uso?.Item, c.Atual.PassosRestantes), "outro item desmarcado: o uso continua");

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Cigarro, false));
        Afirmar.Igual(Estado.Idle, c.Atual.Estado, "parado de novo");
        Afirmar.Nulo(c.Atual.Uso, "o uso do cigarro terminou");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "no mesmo lugar");
    }

    // A onda perde só a parte do item desmarcado: com vodka e cerveja (as duas do bêbado), desmarcar a vodka deixa o bêbado
    // que a cerveja sustenta; desmarcar a cerveja acaba a onda. Nada é sorteado no gerador da paranoia.
    [Teste]
    public static void Desmarcar_TiraSoAParteDoItemDaOnda()
    {
        Cenario c = Cenario.Parado(SemFisica() with { ChanceDaParanoia = NuncaParanoia });
        foreach (Item item in new[] { Item.Vodka, Item.Vodka, Item.Cerveja })
        {
            c.SoltarSobreEle(InvocarEAssentar(c, item).Id);
            c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        }
        EstadoDaOnda bebado = Afirmar.NaoNulo(c.Atual.Onda, "o bêbado");
        Afirmar.Igual(Onda.Bebado, bebado.Tipo, "bêbado");
        Aleatorio paranoia = c.Atual.AleatorioDaParanoia;

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Vodka, false));
        EstadoDaOnda soACerveja = Afirmar.NaoNulo(c.Atual.Onda, "o bêbado continua com a cerveja");
        Afirmar.Igual(Onda.Bebado, soACerveja.Tipo, "ainda o bêbado");
        Afirmar.Verdadeiro(soACerveja.Nivel <= 1 && soACerveja.Nivel <= bebado.Nivel, $"o nível cai ao que a cerveja sustenta: {bebado.Nivel} → {soACerveja.Nivel}");
        Afirmar.Igual((1, Conjunto(Item.Cerveja)), (c.Atual.Carga.Substancias, c.Atual.Carga.Distintas), "a carga sem as duas vodkas");

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Cerveja, false));
        Afirmar.Nulo(c.Atual.Onda, "sem item que a sustente, a onda acaba");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, c.Atual.Carga, "a carga zerada");
        Afirmar.Igual(paranoia, c.Atual.AleatorioDaParanoia, "nada sorteado na limpeza");
    }

    // A paranoia sustentada pela mistura com sintética acaba quando a sintética é desmarcada, e nenhum sorteio novo acontece.
    // Como só cabem duas ondas, a paranoia já tinha tirado o bêbado da vodka; sem o MD, o eufórico também sai, e o episódio
    // acaba (a carga zera, como no fim de toda onda de substância).
    [Teste]
    public static void Desmarcar_ASintetica_AcabaAParanoia_SemSortearDeNovo()
    {
        Cenario c = Cenario.Parado(SemFisica() with { ChanceDaParanoia = Sempre });
        foreach (Item item in new[] { Item.Vodka, Item.Md })
        {
            c.SoltarSobreEle(InvocarEAssentar(c, item).Id);
            c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        }
        Afirmar.Igual((Onda.Paranoico, Onda.Euforico), (c.Atual.Onda?.Tipo, c.Atual.OndaDeFundo?.Tipo), "a mistura com sintética deixou paranoico, com o eufórico no fundo");
        Aleatorio paranoia = c.Atual.AleatorioDaParanoia;

        c.Aplicar(new CmdSetAdultItemEnabled(Item.Md, false));
        Afirmar.Nulo(c.Atual.Onda, "sem a sintética, a paranoia acaba");
        Afirmar.Nulo(c.Atual.OndaDeFundo, "e o eufórico do MD também");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, c.Atual.Carga, "sem onda de substância, o episódio acaba");
        Afirmar.Igual(paranoia, c.Atual.AleatorioDaParanoia, "nenhum sorteio novo");
    }

    // A carga sem um item (DEC-041, item 3): tira as vezes dele, os distintos e a sintética, e o sorteio feito fica feito.
    [Teste]
    public static void Carga_SemUmItem_TiraSoEle_EOSorteioFica()
    {
        CargaDaParanoia carga = CargaDaParanoia.Nenhuma;
        foreach (Item item in new[] { Item.Vodka, Item.Md, Item.Vodka, Item.Cerveja })
            carga = carga.Com(TabelaDoTamagotchi.DoItem(item));
        carga = carga with { Sorteada = true };
        CargaDaParanoia semMd = carga.Sem(Item.Md);
        Afirmar.Igual((3, false, Conjunto(Item.Vodka, Item.Cerveja), true), (semMd.Substancias, semMd.Sintetica, semMd.Distintas, semMd.Sorteada), "sem o MD: três, sem sintética, o sorteio feito");
        Afirmar.Igual(3, semMd.PorItem.Total, "a proveniência acompanha");
        Afirmar.Igual((1, Conjunto(Item.Cerveja)), (semMd.Sem(Item.Vodka).Substancias, semMd.Sem(Item.Vodka).Distintas), "sem a vodka também: só a cerveja");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, semMd.Sem(Item.Vodka).Sem(Item.Cerveja), "sem nenhum: a carga vazia");
        Afirmar.Igual(carga, carga.Sem(Item.Bala), "um item que não estava: nada muda");
    }

    // Sem o baseado marcado, ele não fuma por conta própria, mesmo com a agenda só para isso.
    [Teste]
    public static void SemOBaseado_NaoFumaPorContaPropria()
    {
        ConfiguracaoDoNucleo soOBaseado = SemFisica() with { Acoes = AcoesAutonomas.UsarPorContaPropria };
        Cenario com = Cenario.Parado(soOBaseado);
        com.Decidir();
        Afirmar.Igual(Estado.Using, com.Atual.Estado, "com o baseado marcado, fuma");

        Cenario sem = Cenario.Parado(soOBaseado).Aplicar(new CmdSetAdultItemEnabled(Item.Baseado, false));
        sem.Decidir();
        Afirmar.Igual(Estado.Idle, sem.Atual.Estado, "desmarcado, não fuma");
    }

    // A proveniência da carga bate com ela em uso real: o total é o número de substâncias, e os itens contados são os distintos.
    [Teste]
    public static void Proveniencia_BateComACarga()
    {
        Cenario c = Cenario.Parado(SemFisica() with { ChanceDaParanoia = NuncaParanoia });
        foreach (Item item in new[] { Item.Vodka, Item.Cerveja, Item.Vodka, Item.Bala })
        {
            c.SoltarSobreEle(InvocarEAssentar(c, item).Id);
            c.Passos(c.Atual.PassosRestantes);
            CargaDaParanoia carga = c.Atual.Carga;
            Afirmar.Igual(carga.Substancias, carga.PorItem.Total, $"depois de {item}: o total");
            Afirmar.Verdadeiro(Preferencias.TodosOsItensAdultos.Itens.All(i => (carga.PorItem.Contagem(i) > 0) == carga.Distintas.Contem(i)), $"depois de {item}: os contados são os distintos");
        }
        Afirmar.Igual(2, c.Atual.Carga.PorItem.Contagem(Item.Vodka), "duas vodkas");
    }
}
