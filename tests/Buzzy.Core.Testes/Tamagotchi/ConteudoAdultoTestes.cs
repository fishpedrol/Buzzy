using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// A chave "Conteúdo adulto" (pedido do usuário de 2026-10-02: "todas as coisas adultas do buzzy tivesse uma opcao de
/// desligar e ligar, quando desligasse iria sumir essas opcoes, deixe ativado por padrao"; DEC-033). Adulto é todo item que
/// não é de alívio (a vodka, a cerveja, o baseado, o cigarro, a cocaína, o MD, o lança-perfume, o cogumelo e a bala), as
/// ondas de substância, a paranoia e o baseado por conta própria; a banana, a água, o café e o energético continuam. Ligada
/// por padrão, gravada nas preferências. Desligada: os itens adultos não nascem, os que estão no mundo saem (o da mão solta
/// a captura antes), as ondas de substância acabam (uma leve no fundo volta à frente), o uso de um item adulto termina na
/// hora e ele não fuma por conta própria. O esperado vem do pedido e da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class ConteudoAdultoTestes
{
    /// <summary>Os itens adultos, pela decisão: todos menos a banana, a água, o café e o energético.</summary>
    private static readonly Item[] Adultos =
        [Item.Vodka, Item.Cerveja, Item.Baseado, Item.Cigarro, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala];

    private static readonly Item[] Livres = [Item.Banana, Item.Agua, Item.Cafe, Item.Energetico];

    private static Cenario Desligado(ConfiguracaoDoNucleo? cfg = null) => Cenario.Parado(cfg ?? SemFisica()).Aplicar(new CmdSetAdultContent(false));

    // ---------------------------------------------------------------- a preferência

    // Desligada por padrão desde a DEC-041 (decisão do usuário de 2026-10-05, que substitui o padrão ligado da DEC-033); os
    // itens adultos são os nove da decisão, e os quatro de alívio ficam.
    [Teste]
    public static void Padrao_Desligado_EOsItensAdultosSaoOsNoveDaDecisao()
    {
        Afirmar.Falso(Preferencias.Padrao.ConteudoAdulto, "desligado por padrão");
        Afirmar.Falso(new Preferencias(NivelDeEnergia.Baixa, false).ConteudoAdulto, "desligado também numa preferência nova");
        Afirmar.Sequencia(Adultos, TabelaDoTamagotchi.Itens.Where(TabelaDoTamagotchi.Adulto), "os adultos, na ordem do menu");
        Afirmar.Sequencia(Livres, TabelaDoTamagotchi.Itens.Where(i => !TabelaDoTamagotchi.Adulto(i)), "os que ficam, na ordem do menu");
    }

    // O comando grava a preferência (um GravarPreferencias, e mais nada) e registra a escolha numa transição para o mesmo
    // estado; repetir não faz nada; religar grava de novo.
    [Teste]
    public static void Desligar_GravaAPreferencia_RepetirNaoFazNada_ReligarGrava()
    {
        Cenario c = Cenario.Parado(SemFisica()).Aplicar(new CmdSetAdultContent(false));
        Afirmar.Falso(c.Atual.Preferencias.ConteudoAdulto, "desligado");
        Afirmar.Falso(c.Efeito<GravarPreferencias>().Preferencias.ConteudoAdulto, "a preferência gravada");
        Afirmar.Sequencia(["CMD_SET_ADULT_CONTENT: desligado"], c.Transicoes.Select(t => t.Regra), "a escolha registrada");
        Afirmar.Igual((Estado.Idle, Estado.Idle), (c.Transicoes[0].De, c.Transicoes[0].Para), "no mesmo estado");

        c.Aplicar(new CmdSetAdultContent(false)).SemTransicao().SemEfeito<GravarPreferencias>();
        c.Aplicar(new CmdSetAdultContent(true));
        Afirmar.Verdadeiro(c.Atual.Preferencias.ConteudoAdulto && c.Efeito<GravarPreferencias>().Preferencias.ConteudoAdulto, "religado e gravado");
        Afirmar.Sequencia(["CMD_SET_ADULT_CONTENT: ligado"], c.Transicoes.Select(t => t.Regra), "a escolha registrada");
    }

    // Antes da carga, o comando é ignorado, como a emoção dominante: quem decide é a carga.
    [Teste]
    public static void AntesDaCarga_Ignorado()
    {
        Resultado r = Maquina.Aplicar(EstadoDoNucleo.Inicial(1), new CmdSetAdultContent(true), SemFisica());
        Afirmar.Falso(r.Estado.Preferencias.ConteudoAdulto, "continua como o padrão, desligado");
        Afirmar.Igual(0, r.Efeitos.Count, "nenhum efeito");
    }

    // A carga traz a preferência gravada; desligada, vale desde a partida.
    [Teste]
    public static void Carga_DesligadoNoArquivo_ValeDesdeAPartida()
    {
        var prefs = Preferencias.Padrao with { ConteudoAdulto = false };
        Cenario c = new Cenario(SemFisica()).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, prefs));
        Afirmar.Falso(c.Atual.Preferencias.ConteudoAdulto, "desligado pela carga");
        c.Aplicar(new CmdSummonItem(Item.Vodka));
        Afirmar.Igual(0, c.Atual.Itens.Quantidade, "a vodka não nasce");
    }

    // ---------------------------------------------------------------- desligado: os itens

    // Desligado, um item adulto do menu não nasce (nada no mundo, nenhum Id gasto, nenhuma janela); os de alívio nascem.
    [Teste]
    public static void Desligado_ItemAdultoNaoNasce_ItemLivreNasce()
    {
        foreach (Item item in Adultos)
        {
            Cenario c = Desligado();
            int id = c.Atual.ProximoIdDeItem;
            c.Aplicar(new CmdSummonItem(item));
            Afirmar.Igual(0, c.Atual.Itens.Quantidade, $"{item}: não nasce");
            Afirmar.Igual(id, c.Atual.ProximoIdDeItem, $"{item}: nenhum Id gasto");
            Afirmar.Falso(c.Tem<MostrarItem>(), $"{item}: nenhuma janela");
        }
        foreach (Item item in Livres)
        {
            Cenario c = Desligado();
            c.Aplicar(new CmdSummonItem(item));
            Afirmar.Igual(item, Afirmar.NaoNulo(c.Atual.Itens.Todos.SingleOrDefault(), $"{item} nasce").Item, $"{item}: nasce");
        }
    }

    // Desligar tira do mundo os itens adultos, com a janela removida como recolhida, e deixa os outros onde estão.
    [Teste]
    public static void Desligar_TiraOsItensAdultosDoMundo_EDeixaOsOutros()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo vodka = InvocarEAssentar(c, Item.Vodka);
        ItemNoMundo banana = InvocarEAssentar(c, Item.Banana);
        ItemNoMundo bala = InvocarEAssentar(c, Item.Bala);

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Sequencia([banana.Id], c.Atual.Itens.Todos.Select(i => i.Id), "só a banana fica");
        Afirmar.Igual(banana, c.Atual.Itens.PorId(banana.Id), "a banana, no mesmo lugar");
        Afirmar.Sequencia(
            [new RemoverItem(vodka.Id, MotivoDaRemocao.Recolhido), new RemoverItem(bala.Id, MotivoDaRemocao.Recolhido)],
            c.Efeitos.OfType<RemoverItem>(), "as janelas da vodka e da bala saem");
        Afirmar.Falso(c.Tem<LiberarCapturaDoItem>(), "nada na mão");
    }

    // Com um item adulto na mão do usuário, desligar solta a captura antes e o tira do mundo; ele deixa de estar atento.
    [Teste]
    public static void Desligar_ComItemAdultoNaMao_SoltaACapturaETira()
    {
        Cenario c = Cenario.Parado(SemFisica());
        ItemNoMundo cigarro = InvocarEAssentar(c, Item.Cigarro);
        c.Aplicar(new ItemPress(cigarro.Id, new PontoPx(cigarro.Lugar.Ancora.X, cigarro.Lugar.Ancora.Y - 10)));
        Afirmar.Verdadeiro(c.Atual.Atento, "segurando o cigarro");

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Igual(0, c.Atual.Itens.Quantidade, "o cigarro saiu");
        Afirmar.Falso(c.Atual.Atento, "não está mais atento");
        int captura = c.Efeitos.ToList().FindIndex(e => e is LiberarCapturaDoItem l && l.Id == cigarro.Id);
        int remocao = c.Efeitos.ToList().FindIndex(e => e is RemoverItem r && r.Id == cigarro.Id);
        Afirmar.Verdadeiro(captura >= 0 && remocao > captura, "solta a captura antes de remover a janela");
    }

    // ---------------------------------------------------------------- desligado: as ondas e o uso

    // Desligar acaba a onda de substância da frente: sem onda, a carga zerada, o temporizador cancelado e a cara de base.
    [Teste]
    public static void Desligar_AcabaAOndaDeSubstancia()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.SoltarSobreEle(InvocarEAssentar(c, Item.Vodka).Id);
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, "fim do uso");
        Afirmar.Igual(Onda.Bebado, c.Atual.Onda?.Tipo, "bêbado");

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Nulo(c.Atual.Onda, "sem onda");
        Afirmar.Nulo(c.Atual.OndaDeFundo, "nada no fundo");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, c.Atual.Carga, "a carga do episódio zerada");
        Afirmar.Verdadeiro(c.Tem<CancelarOnda>(), "o temporizador da onda cancelado");
        Afirmar.Igual(Expressao.Neutro, c.Atual.Expressao, "a cara de base");
    }

    // Uma onda leve no fundo (o café, Ligado) volta à frente quando a de substância (a vodka, Bebado) sai, como no fim dela.
    [Teste]
    public static void Desligar_OndaLeveNoFundo_VoltaAFrente()
    {
        Cenario c = Cenario.Parado(SemFisica());
        foreach (Item item in new[] { Item.Cafe, Item.Vodka })
        {
            c.SoltarSobreEle(InvocarEAssentar(c, item).Id);
            c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        }
        Afirmar.Igual((Onda.Bebado, Onda.Ligado), (c.Atual.Onda?.Tipo, c.Atual.OndaDeFundo?.Tipo), "bêbado na frente, ligado no fundo");

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Igual(Onda.Ligado, c.Atual.Onda?.Tipo, "o ligado volta à frente");
        Afirmar.Nulo(c.Atual.OndaDeFundo, "nada no fundo");
        Afirmar.Verdadeiro(c.Tem<AgendarOnda>(), "o temporizador recomeça com a fase dele");
    }

    // Com duas ondas de substância (a vodka no fundo, o MD na frente), as duas saem: a de fundo não volta à frente.
    [Teste]
    public static void Desligar_DuasOndasDeSubstancia_AsDuasSaem()
    {
        Cenario c = Cenario.Parado(SemFisica() with { ChanceDaParanoia = NuncaParanoia });
        foreach (Item item in new[] { Item.Vodka, Item.Md })
        {
            c.SoltarSobreEle(InvocarEAssentar(c, item).Id);
            c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        }
        Afirmar.Igual((Onda.Euforico, Onda.Bebado), (c.Atual.Onda?.Tipo, c.Atual.OndaDeFundo?.Tipo), "eufórico na frente, bêbado no fundo");

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Nulo(c.Atual.Onda, "sem onda na frente");
        Afirmar.Nulo(c.Atual.OndaDeFundo, "nem no fundo");
        Afirmar.Igual(CargaDaParanoia.Nenhuma, c.Atual.Carga, "a carga zerada");
    }

    // Uma onda leve na frente fica como está.
    [Teste]
    public static void Desligar_OndaLeve_Fica()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.SoltarSobreEle(InvocarEAssentar(c, Item.Energetico).Id);
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, "fim do uso");
        EstadoDaOnda? antes = c.Atual.Onda;
        Afirmar.Igual(Onda.Ligado, antes?.Tipo, "ligado");

        c.Aplicar(new CmdSetAdultContent(false)).SemEfeito<CancelarOnda>();
        Afirmar.Igual(antes, c.Atual.Onda, "a mesma onda");
    }

    // Usando um item adulto, desligar termina o uso na hora, no mesmo lugar, sem a onda dele.
    [Teste]
    public static void Desligar_UsandoItemAdulto_OUsoTermina()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.SoltarSobreEle(InvocarEAssentar(c, Item.Cogumelo).Id).Esta(Estado.Using, "usando o cogumelo");

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Igual(Estado.Idle, c.Atual.Estado, "parado de novo");
        Afirmar.Nulo(c.Atual.Uso, "o uso acabou");
        Afirmar.Nulo(c.Atual.Onda, "sem a onda do cogumelo");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "no mesmo lugar");
    }

    // Usando um item de alívio, desligar não interrompe nada.
    [Teste]
    public static void Desligar_UsandoItemLivre_Continua()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.SoltarSobreEle(InvocarEAssentar(c, Item.Banana).Id).Esta(Estado.Using, "comendo a banana");
        int restantes = c.Atual.PassosRestantes;

        c.Aplicar(new CmdSetAdultContent(false));
        Afirmar.Igual((Estado.Using, Item.Banana, restantes), (c.Atual.Estado, c.Atual.Uso?.Item, c.Atual.PassosRestantes), "continua comendo");
    }

    // Desligado, ele não fuma por conta própria: a agenda nunca escolhe o baseado. Religado, volta a poder.
    [Teste]
    public static void Desligado_NaoFumaPorContaPropria()
    {
        ConfiguracaoDoNucleo soOBaseado = SemFisica() with { Acoes = AcoesAutonomas.FumarBaseado };
        Cenario c = Desligado(soOBaseado);
        for (int i = 0; i < 20; i++)
        {
            c.Decidir();
            Afirmar.Diferente(Estado.Using, c.Atual.Estado, $"decisão {i}: não fuma");
        }
        c.Aplicar(new CmdSetAdultContent(true)).Decidir().Esta(Estado.Using, "religado, fuma");
    }

    // SETTINGS_CHANGED desligando a chave faz o mesmo que o comando do menu.
    [Teste]
    public static void PreferenciasDesligandoAChave_FazemOMesmo()
    {
        Cenario c = Cenario.Parado(SemFisica());
        InvocarEAssentar(c, Item.Md);
        c.Aplicar(new SettingsChanged(Preferencias.Padrao with { ConteudoAdulto = false }));
        Afirmar.Igual(0, c.Atual.Itens.Quantidade, "o MD saiu");
        Afirmar.Falso(c.Atual.Preferencias.ConteudoAdulto, "desligado");
    }

    // ---------------------------------------------------------------- a gravação de referência

    // O comando e a preferência vão à gravação dos testes de referência e voltam iguais; ligada, a preferência não aparece,
    // e as referências gravadas antes da chave continuam iguais.
    [Teste]
    public static void Gravacao_ComandoEPreferencia_IdaEVolta()
    {
        string Nome(Topologia _) => "um";
        Topologia Topo(string _) => TopologiasDeExemplo.UmMonitor;
        EstadoDoNucleo s = EstadoDoNucleo.Inicial(1);
        foreach (bool ligado in new[] { false, true })
        {
            string linha = Gravacao.Escrever(new CmdSetAdultContent(ligado), Nome);
            Afirmar.Igual($"CmdSetAdultContent ligado={(ligado ? "sim" : "nao")}", linha, "a linha do comando");
            Afirmar.Igual(new CmdSetAdultContent(ligado), Gravacao.Ler(linha, Topo, s).Single(), "lida de volta");
        }
        string desligada = Gravacao.Escrever(new SettingsChanged(PreferenciasDeTeste.Completas with { ConteudoAdulto = false }), Nome);
        Afirmar.Igual("SettingsChanged energia=Media telaCheia=sim adulto=nao", desligada, "desligada aparece");
        Afirmar.Falso(((SettingsChanged)Gravacao.Ler(desligada, Topo, s).Single()).Preferencias.ConteudoAdulto, "lida de volta");
        Afirmar.Igual("SettingsChanged energia=Media telaCheia=sim", Gravacao.Escrever(new SettingsChanged(PreferenciasDeTeste.Completas), Nome), "ligada não aparece");
        // DEC-041: a seleção só aparece fora dos nove, e volta igual.
        string soMd = Gravacao.Escrever(new SettingsChanged(PreferenciasDeTeste.Completas with { ItensAdultosHabilitados = ConjuntoDeItens.Vazio.Com(Item.Md) }), Nome);
        Afirmar.Igual("SettingsChanged energia=Media telaCheia=sim adultos=Md", soMd, "a seleção fora dos nove aparece");
        Afirmar.Igual(ConjuntoDeItens.Vazio.Com(Item.Md), ((SettingsChanged)Gravacao.Ler(soMd, Topo, s).Single()).Preferencias.ItensAdultosHabilitados, "lida de volta");
        string nenhum = Gravacao.Escrever(new SettingsChanged(PreferenciasDeTeste.Completas with { ItensAdultosHabilitados = ConjuntoDeItens.Vazio }), Nome);
        Afirmar.Igual("SettingsChanged energia=Media telaCheia=sim adultos=nenhum", nenhum, "nenhum");
        Afirmar.Igual(ConjuntoDeItens.Vazio, ((SettingsChanged)Gravacao.Ler(nenhum, Topo, s).Single()).Preferencias.ItensAdultosHabilitados, "nenhum lido de volta");
        string comando = Gravacao.Escrever(new CmdSetAdultItemEnabled(Item.Bala, false), Nome);
        Afirmar.Igual(new CmdSetAdultItemEnabled(Item.Bala, false), Gravacao.Ler(comando, Topo, s).Single(), $"o comando do item, ida e volta ({comando})");
        string religar = Gravacao.Escrever(new CmdSetAdultItemEnabled(Item.Md, true), Nome);
        Afirmar.Igual(new CmdSetAdultItemEnabled(Item.Md, true), Gravacao.Ler(religar, Topo, s).Single(), $"religar, ida e volta ({religar})");
    }
}
