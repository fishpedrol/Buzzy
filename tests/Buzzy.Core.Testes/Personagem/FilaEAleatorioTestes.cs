using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>Fila com prioridade (ARCHITECTURE.md 2.3), gerador com semente e perfis de energia.</summary>
internal static class FilaEAleatorioTestes
{
    [Teste]
    public static void FilaAplicaPorPrioridadeEPorOrdemDeChegadaNaMesmaPrioridade()
    {
        var nucleo = new Nucleo(new ConfiguracaoDoNucleo(), 3);
        nucleo.Enfileirar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        nucleo.Processar();

        var aplicados = new List<string>();
        nucleo.Enfileirar(new ExpressionChange(Expressao.Feliz));
        nucleo.Enfileirar(new Tick());
        nucleo.Enfileirar(new TopologyChanged(TopologiasDeExemplo.UmMonitor));
        nucleo.Enfileirar(new CmdPauseAutonomy());
        nucleo.Enfileirar(new Press(Cenario.PontoOpaco));
        nucleo.Enfileirar(new CmdResumeAutonomy());
        nucleo.Processar((e, _) => aplicados.Add(e.GetType().Name));

        Afirmar.Sequencia(
            ["Press", "CmdPauseAutonomy", "CmdResumeAutonomy", "TopologyChanged", "Tick", "ExpressionChange"],
            aplicados,
            "ação direta, depois comandos na ordem de chegada, sistema, relógio e expressão");
        Afirmar.Igual(Estado.Pressed, nucleo.Retrato.Estado);
    }

    [Teste]
    public static void EventoAutonomoComOUsuarioNoControleEhDescartadoNaoEnfileirado()
    {
        var nucleo = new Nucleo(new ConfiguracaoDoNucleo(), 3);
        nucleo.Enfileirar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        nucleo.Processar();
        long geracao = nucleo.Estado.Geracao;
        // Na mesma leva, o PRESS passaria na frente da carga (prioridade maior) e seria ignorado
        // em BOOTING; por isso a carga é processada antes.
        nucleo.Enfileirar(new Press(Cenario.PontoOpaco));
        nucleo.Processar();
        Afirmar.Igual(Estado.Pressed, nucleo.Retrato.Estado);

        Afirmar.Falso(nucleo.Enfileirar(new AutonomyTimer(geracao)), "descartado na chegada");
        Afirmar.Igual(0, nucleo.Pendentes);
        Afirmar.Igual(1L, nucleo.Descartados);

        // Chegou com o personagem livre, mas um PRESS da mesma leva passou na frente.
        var outro = new Nucleo(new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Andar }, 3);
        outro.Enfileirar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        outro.Processar();
        Afirmar.Verdadeiro(outro.Enfileirar(new AutonomyTimer(outro.Estado.Geracao)), "enfileirado com o personagem livre");
        outro.Enfileirar(new Press(Cenario.PontoOpaco));
        outro.Processar();
        Afirmar.Igual(Estado.Pressed, outro.Retrato.Estado, "o PRESS venceu; a decisão não aconteceu");
        Afirmar.Igual(1L, outro.Descartados, "descartado ao sair da fila");
    }

    // DEC-028: o disparo da onda de um item tem a prioridade do relógio: com o usuário segurando o personagem (PRESSED), ele
    // entra na fila e é aplicado. USING é do grupo do usuário: a agenda (autônoma) é descartada na chegada e, se um item
    // solto sobre ele passou na frente na mesma leva, ao sair da fila.
    [Teste]
    public static void ItemEffectTimerNaoEhDescartadoEmPressed_AutonomyTimerEhDescartadoEmUsing()
    {
        Cenario usando = Cenario.Em(Estado.Using);
        Afirmar.Igual(GrupoDoEstado.Usuario, usando.Atual.Estado.Grupo(), "USING é do grupo do usuário");
        Afirmar.Verdadeiro(Estado.Using.AceitaPressionar(), "e aceita PRESS");
        var nucleo = new Nucleo(usando.Config, usando.Atual);
        Afirmar.Falso(nucleo.Enfileirar(new AutonomyTimer(usando.Atual.Geracao)), "em USING, a agenda é descartada na chegada");
        Afirmar.Igual((0, 1L), (nucleo.Pendentes, nucleo.Descartados), "nada na fila, um descartado");

        Cenario pressionado = Cenario.Em(Estado.Using).Aplicar(new Press(Cenario.PontoOpaco)).Esta(Estado.Pressed);
        EstadoDaOnda onda = Afirmar.NaoNulo(pressionado.Atual.Onda, "com a onda da banana");
        var comOnda = new Nucleo(pressionado.Config, pressionado.Atual);
        Afirmar.Verdadeiro(comOnda.Enfileirar(new ItemEffectTimer(pressionado.Atual.GeracaoDaOnda)), "em PRESSED, o disparo da onda entra na fila");
        comOnda.Processar();
        Afirmar.Igual(Estado.Pressed, comOnda.Retrato.Estado, "continua segurado");
        Afirmar.Diferente(onda, comOnda.Estado.Onda, "e a onda avançou");
        Afirmar.Igual(0L, comOnda.Descartados, "sem descarte");

        // Na mesma leva: a agenda chegou com ele livre, mas o item solto sobre ele (ação direta) passou na frente.
        Cenario segurando = Cenario.Parado(new ConfiguracaoDoNucleo { Tamagotchi = true });
        segurando.Aplicar(new CmdSummonItem(Item.Agua));
        while (segurando.Atual.Itens.AlgumCaindo) segurando.Aplicar(new Tick());
        ItemNoMundo item = segurando.Atual.Itens.Todos.Single();
        PontoPx meio = Cenario.MeioDoPersonagem(segurando.Atual, segurando.Config);
        segurando.Aplicar(new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10)), new ItemDragStart(item.Id),
            new ItemDragMove(item.Id, new PontoPx(meio.X, meio.Y - 10)));
        var leva = new Nucleo(segurando.Config, segurando.Atual);
        Afirmar.Verdadeiro(leva.Enfileirar(new AutonomyTimer(segurando.Atual.Geracao)), "enfileirado com o personagem livre");
        leva.Enfileirar(new ItemDragEnd(item.Id, new PontoPx(meio.X, meio.Y - 10)));
        leva.Processar();
        Afirmar.Igual(Estado.Using, leva.Retrato.Estado, "o item solto venceu");
        Afirmar.Igual(1L, leva.Descartados, "a agenda foi descartada ao sair da fila");
    }

    [Teste]
    public static void AleatorioEhDeterministicoEFicaNaFaixa()
    {
        var a = new Aleatorio(42);
        var b = new Aleatorio(42);
        for (int i = 0; i < 1000; i++)
        {
            (int va, Aleatorio pa) = a.Entre(-3, 5);
            (int vb, Aleatorio pb) = b.Entre(-3, 5);
            Afirmar.Igual(va, vb, "mesma semente, mesmo valor");
            Afirmar.Verdadeiro(va is >= -3 and <= 5, $"na faixa: {va}");
            a = pa;
            b = pb;
        }
        Afirmar.Diferente(new Aleatorio(1).Sortear().Valor, new Aleatorio(2).Sortear().Valor, "sementes diferentes, valores diferentes");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => new Aleatorio(1).Entre(5, 4));
    }

    // Uma chance de "Vezes em Em" (a da paranoia, 1 em 8, pedido do usuário de 2026-10-01): um sorteio é exatamente um passo
    // do gerador, um inteiro uniforme de 1 a Em que sai se for até Vezes, mesmo com a chance certa (1 em 1) ou nula (0 em 1).
    // Em 80 mil sorteios de 1 em 8, sai perto de 1/8 (a tolerância de 0,5 ponto é mais de 4 desvios-padrão da binomial,
    // σ ≈ 0,12 ponto). Uma chance fora da faixa não existe.
    [Teste]
    public static void ChanceEhUmPassoDoGerador()
    {
        var a = new Aleatorio(5);
        int saiu = 0;
        for (int i = 0; i < 80000; i++)
        {
            (bool sorteada, Aleatorio proximo) = a.Sortear(new Chance(1, 8));
            (int valor, Aleatorio umPasso) = a.Entre(1, 8);
            Afirmar.Igual((valor == 1, umPasso), (sorteada, proximo), "um passo: um inteiro de 1 a 8, que sai se for 1");
            if (sorteada) saiu++;
            a = proximo;
        }
        Afirmar.Aproximado(0.125, saiu / 80000.0, 0.005, "perto de 1/8");
        var b = new Aleatorio(77);
        Afirmar.Igual((true, b.Sortear().Proximo), b.Sortear(new Chance(1, 1)), "1 em 1 sempre sai, num passo");
        Afirmar.Igual((false, b.Sortear().Proximo), b.Sortear(new Chance(0, 1)), "0 em 1 nunca sai, num passo também");
        Afirmar.Igual((3, 4), (new Chance(3, 4).Vezes, new Chance(3, 4).Em), "as partes");
        Afirmar.Igual("1 em 8", new Chance(1, 8).ToString(), "escrita como na decisão");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => _ = new Chance(1, 0), "em zero");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => _ = new Chance(-1, 8), "vezes negativas");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => _ = new Chance(9, 8), "mais vezes que o total");
    }

    [Teste]
    public static void PonderadoRespeitaOsPesos()
    {
        var a = new Aleatorio(9);
        var contagem = new int[3];
        for (int i = 0; i < 30000; i++)
        {
            (int indice, Aleatorio proximo) = a.Ponderado([1, 0, 3]);
            contagem[indice]++;
            a = proximo;
        }
        Afirmar.Igual(0, contagem[1], "peso zero nunca sai");
        double proporcao = contagem[2] / (double)contagem[0];
        Afirmar.Aproximado(3.0, proporcao, 0.25, "peso 3 sai três vezes mais que peso 1");
        Afirmar.Lanca<ArgumentException>(() => a.Ponderado([0, 0]));
    }

    [Teste]
    public static void PerfisDeEnergiaSaoOrdenadosDeBaixaParaAlta()
    {
        PerfilDeEnergia baixa = PerfilDeEnergia.Padrao(NivelDeEnergia.Baixa);
        PerfilDeEnergia media = PerfilDeEnergia.Padrao(NivelDeEnergia.Media);
        PerfilDeEnergia alta = PerfilDeEnergia.Padrao(NivelDeEnergia.Alta);

        // DEC-014: Baixa com mais pausas e menos ações; Alta com mais ações e mais longas.
        Afirmar.Verdadeiro(baixa.DecisaoMinima > media.DecisaoMinima && media.DecisaoMinima > alta.DecisaoMinima, "intervalo mínimo entre decisões diminui");
        Afirmar.Verdadeiro(baixa.DecisaoMaxima > media.DecisaoMaxima && media.DecisaoMaxima > alta.DecisaoMaxima, "intervalo máximo diminui");
        Afirmar.Verdadeiro(baixa.PesoDescansar > media.PesoDescansar && media.PesoDescansar > alta.PesoDescansar, "descansa menos com mais energia");
        Afirmar.Verdadeiro(baixa.PesoAndar < media.PesoAndar && media.PesoAndar < alta.PesoAndar, "anda mais com mais energia");
        Afirmar.Verdadeiro(baixa.PassosDoGestoMaximo < alta.PassosDoGestoMaximo, "gestos mais longos em Alta");
        Afirmar.Igual(NivelDeEnergia.Media, Preferencias.Padrao.Energia, "Média é o padrão");
    }

    [Teste]
    public static void EnergiaMudaSoOsTemposDaAgendaNuncaAFisicaNemOApoio()
    {
        // Invariante 12 (parte do núcleo que já existe): a mesma sequência em Baixa e em Alta
        // passa pelos mesmos estados de validação, com o mesmo lugar ao soltar.
        foreach (NivelDeEnergia nivel in Enum.GetValues<NivelDeEnergia>())
        {
            Cenario c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, new Preferencias(nivel, true)));
            c.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragEnd(new PontoPx(500, 200)));
            Afirmar.Igual(new PontoPx(500, 1032), c.Ancora, $"{nivel}: mesmo lugar ao soltar");
            Afirmar.Igual(Estado.Idle, c.Atual.Estado, $"{nivel}: mesmo estado");
        }
    }
}
