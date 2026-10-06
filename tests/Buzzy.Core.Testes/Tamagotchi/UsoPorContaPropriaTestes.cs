using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// O uso por conta própria das seis drogas ilícitas (DEC-045, pedido do usuário de 2026-10-06: "quero que seja configurável
/// na versão completa o uso de drogas por livre arbítrio ... para todas drogas"): uma escolha por droga, todas desligadas
/// por padrão (inclusive o baseado, que antes era sempre); só as seis e só as da edição; exige também o item marcado e a
/// chave adulta; a frequência total é a de antes, com a droga sorteada entre as marcadas que podem; uma droga cuja onda está
/// na frente não entra, e nenhuma com a paranoia na frente. O esperado vem da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class UsoPorContaPropriaTestes
{
    private static readonly Item[] Seis = [Item.Baseado, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala];

    private static ConjuntoDeItens Conjunto(params Item[] itens) => itens.Aggregate(ConjuntoDeItens.Vazio, (c, i) => c.Com(i));

    /// <summary>Só o uso por conta própria na agenda: toda decisão em que ele pode usar é usar.</summary>
    private static ConfiguracaoDoNucleo SoOUso() => SemFisica() with { Acoes = AcoesAutonomas.UsarPorContaPropria, ChanceDaParanoia = NuncaParanoia };

    /// <summary>O cenário parado com as preferências completas e a seleção por conta própria dada.</summary>
    private static Cenario Com(ConfiguracaoDoNucleo cfg, params Item[] porContaPropria)
    {
        Cenario c = Cenario.Parado(cfg);
        c.Aplicar(new SettingsChanged(c.Atual.Preferencias with { ItensPorContaPropria = Conjunto(porContaPropria) }));
        return c;
    }

    [Teste]
    public static void Padrao_NenhumaPorContaPropria_EAsSeisIlicitas()
    {
        Afirmar.Igual(ConjuntoDeItens.Vazio, Preferencias.Padrao.ItensPorContaPropria, "tudo desligado por padrão, inclusive o baseado");
        Afirmar.Igual(Conjunto(Seis), TabelaDoTamagotchi.Ilicitos, "as seis drogas ilícitas");
        Afirmar.Igual(Conjunto(Item.Baseado), Preferencias.NormalizarPorContaPropria(Conjunto(Item.Baseado, Item.Vodka, Item.Banana, Item.Cigarro)),
            "normalizar deixa só as ilícitas");
    }

    // O comando grava a escolha numa transição para o mesmo estado; repetir não faz nada; um item que não é ilícito, um fora
    // do enum e o comando antes da carga são ignorados; na edição pública, nenhuma.
    [Teste]
    public static void Comando_GravaAEscolha_SoDasIlicitasDaEdicao()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.Aplicar(new CmdSetSelfUseItem(Item.Cocaina, true));
        Afirmar.Verdadeiro(c.Atual.Preferencias.ItensPorContaPropria.Contem(Item.Cocaina), "a cocaína marcada");
        Afirmar.Verdadeiro(c.Efeito<GravarPreferencias>().Preferencias.ItensPorContaPropria.Contem(Item.Cocaina), "a escolha gravada");
        Afirmar.Igual((Estado.Idle, Estado.Idle, "CMD_SET_SELF_USE_ITEM: preferência atualizada"), (c.Transicoes[0].De, c.Transicoes[0].Para, c.Transicoes[0].Regra), "no mesmo estado");
        c.Aplicar(new CmdSetSelfUseItem(Item.Cocaina, true)).SemTransicao().SemEfeito<GravarPreferencias>();
        c.Aplicar(new CmdSetSelfUseItem(Item.Cocaina, false));
        Afirmar.Falso(c.Atual.Preferencias.ItensPorContaPropria.Contem(Item.Cocaina), "desmarcada");

        EstadoDoNucleo antes = c.Atual;
        foreach (Evento e in new Evento[] { new CmdSetSelfUseItem(Item.Vodka, true), new CmdSetSelfUseItem(Item.Cigarro, true), new CmdSetSelfUseItem(Item.Banana, true), new CmdSetSelfUseItem((Item)99, true) })
        {
            c.Aplicar(e);
            Afirmar.Igual((antes, 0), (c.Atual, c.Efeitos.Count), $"{e}: ignorado");
        }

        Resultado antesDaCarga = Maquina.Aplicar(EstadoDoNucleo.Inicial(1), new CmdSetSelfUseItem(Item.Md, true), SemFisica());
        Afirmar.Igual((Preferencias.Padrao, 0), (antesDaCarga.Estado.Preferencias, antesDaCarga.Efeitos.Count), "antes da carga: ignorado");

        Cenario publica = Cenario.Parado(SemFisica() with { ItensDaEdicao = TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica) });
        EstadoDoNucleo antesDaPublica = publica.Atual;
        foreach (Item item in Seis)
        {
            publica.Aplicar(new CmdSetSelfUseItem(item, true));
            Afirmar.Igual((antesDaPublica, 0), (publica.Atual, publica.Efeitos.Count), $"pública, {item}: ignorado");
        }
    }

    // A carga e as preferências trocadas tiram o que não é ilícito ou não é da edição.
    [Teste]
    public static void Sanear_SoIlicitasDaEdicao()
    {
        Cenario c = Cenario.Parado(SemFisica());
        c.Aplicar(new SettingsChanged(c.Atual.Preferencias with { ItensPorContaPropria = Conjunto(Item.Vodka, Item.Md, Item.Banana) }));
        Afirmar.Igual(Conjunto(Item.Md), c.Atual.Preferencias.ItensPorContaPropria, "só o MD fica");

        Cenario p = Cenario.Parado(SemFisica() with { ItensDaEdicao = TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica) });
        p.Aplicar(new SettingsChanged(p.Atual.Preferencias with { ItensPorContaPropria = Conjunto(Seis) }));
        Afirmar.Igual(ConjuntoDeItens.Vazio, p.Atual.Preferencias.ItensPorContaPropria, "na pública, nenhuma");
    }

    // Nada marcado: a decisão não acha opção (nem o baseado, que antes era sempre).
    [Teste]
    public static void SemNadaMarcado_NaoUsaNada()
        => Com(SoOUso()).Decidir().SemTransicao().Esta(Estado.Idle, "nada por conta própria");

    // Cada uma das seis, sozinha: usa a dela, sem item no mundo, com o uso da tabela, a regra "por conta própria" e só o
    // sorteio da agenda no gerador principal (com uma só, nada a sortear entre elas).
    [Teste]
    public static void CadaUma_UsaADela_SemItemNoMundo()
    {
        foreach (Item item in Seis)
        {
            Cenario c = Com(SoOUso(), item);
            EstadoDoNucleo antes = c.Atual;
            DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
            c.Decidir().Percorreu(Estado.Idle, Estado.Using);
            Afirmar.Igual($"IDLE + AUTONOMY_TIMER: {dados.Verbo} {item} por conta própria", c.Transicoes[0].Regra, $"{item}: a regra");
            Afirmar.Igual(new Uso(item, dados.Verbo, dados.PassosDoUso, ApoioDoUso.Chao), c.Retrato.Uso, $"{item}: o uso da tabela, no chão");
            Afirmar.Igual(ItensNoMundo.Nenhum, c.Atual.Itens, $"{item}: nenhum item no mundo");
            Afirmar.Igual(antes.Aleatorio.Sortear().Proximo, c.Atual.Aleatorio, $"{item}: só o sorteio da agenda");
        }
    }

    // Marcada por conta própria mas desmarcada nos itens adultos, ou com a chave adulta desligada: não usa.
    [Teste]
    public static void ExigeOItemMarcadoEAChaveAdulta()
    {
        Cenario semItem = Com(SoOUso(), Item.Cocaina).Aplicar(new CmdSetAdultItemEnabled(Item.Cocaina, false));
        semItem.Decidir().SemTransicao().Esta(Estado.Idle, "a cocaína desmarcada nos itens adultos");
        Cenario semChave = Com(SoOUso(), Item.Cocaina).Aplicar(new CmdSetAdultContent(false));
        semChave.Decidir().SemTransicao().Esta(Estado.Idle, "a chave adulta desligada");
    }

    // Com várias marcadas, a droga é sorteada entre elas: em muitas sementes, todas aparecem, e nenhuma fora delas.
    [Teste]
    public static void VariasMarcadas_SorteiaEntreElas()
    {
        var vistas = new HashSet<Item>();
        for (ulong semente = 1; semente <= 200; semente++)
        {
            var c = new Cenario(SoOUso(), semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null,
                Cenario.PreferenciasDosCenarios with { ItensPorContaPropria = Conjunto(Item.Md, Item.Cogumelo, Item.Bala) }));
            c.Decidir().Esta(Estado.Using);
            vistas.Add(c.Atual.Uso!.Item);
        }
        Afirmar.Igual(Conjunto(Item.Md, Item.Cogumelo, Item.Bala), vistas.Aggregate(ConjuntoDeItens.Vazio, (s, i) => s.Com(i)), "as três aparecem, e só elas");
    }

    // A frequência total não depende de quantas estão marcadas: o peso da ação é um só.
    [Teste]
    public static void Frequencia_AMesmaComUmaOuSeis()
    {
        static int Usos(params Item[] marcadas)
        {
            int usos = 0;
            for (ulong semente = 1; semente <= 300; semente++)
            {
                var c = new Cenario(SemFisica() with { Acoes = AcoesAutonomas.Todas | AcoesAutonomas.UsarPorContaPropria, ChanceDaParanoia = NuncaParanoia }, semente).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null,
                    Cenario.PreferenciasDosCenarios with { ItensPorContaPropria = Conjunto(marcadas) }));
                c.Decidir();
                if (c.Atual.Estado == Estado.Using) usos++;
            }
            return usos;
        }
        Afirmar.Igual(Usos(Item.Baseado), Usos(Seis), "a mesma quantidade de decisões de uso, com uma ou com as seis");
        Afirmar.Verdadeiro(Usos(Item.Baseado) > 0, "e alguma decisão de uso houve");
    }

    // Uma droga cuja onda está na frente fica de fora (o MD depois do MD), mas outra marcada entra; com a paranoia na frente,
    // nenhuma.
    [Teste]
    public static void NaoEmendaAMesmaOnda_ENadaComParanoia()
    {
        Cenario c = Com(SoOUso(), Item.Md);
        c.Decidir().Esta(Estado.Using);
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle);
        Onda? ondaDoMd = TabelaDoTamagotchi.DoItem(Item.Md).Onda;
        Afirmar.Igual(ondaDoMd, c.Atual.Onda?.Tipo, "a onda do MD na frente");
        c.Decidir().SemTransicao().Esta(Estado.Idle, "não emenda o MD");
        c.Aplicar(new CmdSetSelfUseItem(Item.Cogumelo, true));
        c.Decidir().Esta(Estado.Using);
        Afirmar.Igual(Item.Cogumelo, c.Atual.Uso!.Item, "o cogumelo, de outra onda, entra");

        Cenario p = Com(SoOUso() with { ChanceDaParanoia = new Chance(1, 1) }, Seis);
        foreach (Item item in new[] { Item.Bala, Item.Vodka })
        {
            p.SoltarSobreEle(InvocarEAssentar(p, item).Id).Esta(Estado.Using, $"usando {item}");
            p.Passos(p.Atual.PassosRestantes);
            for (int i = 0; i < 200 && p.Atual.Gesto != Gesto.Nenhum; i++) p.Aplicar(new Tick());
        }
        Afirmar.Igual(Onda.Paranoico, p.Atual.Onda?.Tipo, "paranoico depois de uma mistura com sintética");
        p.Decidir().SemTransicao().Esta(Estado.Idle, "paranoico, nenhuma");
    }
}
