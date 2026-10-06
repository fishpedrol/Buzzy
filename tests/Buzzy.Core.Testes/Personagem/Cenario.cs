using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Monta o núcleo num estado conhecido, sempre pela própria máquina (nunca escrevendo o estado
/// à mão), e aplica eventos guardando o último resultado. Topologia padrão: um monitor
/// 1920×1080 com área útil (0,0)-(1920,1032); a âncora inicial fica a 85% da largura, no chão.
/// </summary>
internal sealed class Cenario
{
    /// <summary>Âncora inicial em <see cref="TopologiasDeExemplo.UmMonitor"/>: 85% de 1920 = 1632, no chão (1032).</summary>
    public static readonly PontoPx AncoraInicial = new(1632, 1032);

    /// <summary>Um ponto sobre o corpo do sprite, acima da âncora.</summary>
    public static readonly PontoPx PontoOpaco = new(1632, 1000);

    /// <summary>
    /// Três monitores 1920x1080 a 96 DPI em linha, com a barra de 48 px embaixo e o principal à
    /// esquerda. Do monitor do meio, o da direita fica mais perto que o principal: os cenários que
    /// distinguem "o monitor mais próximo" de "o principal" e de "o primeiro da lista" usam esta.
    /// <code>
    /// [ 1* ][ 2  ][ 3  ]
    /// </code>
    /// </summary>
    public static Topologia TresEmLinha => new([
        TopologiasDeExemplo.Principal(TopologiasDeExemplo.Display1, TopologiasDeExemplo.Ret(0, 0, 1920, 1080), TopologiasDeExemplo.Ret(0, 0, 1920, 1032), 96),
        TopologiasDeExemplo.Secundario(TopologiasDeExemplo.Display2, TopologiasDeExemplo.Ret(1920, 0, 3840, 1080), TopologiasDeExemplo.Ret(1920, 0, 3840, 1032), 96),
        TopologiasDeExemplo.Secundario(TopologiasDeExemplo.Display3, TopologiasDeExemplo.Ret(3840, 0, 5760, 1080), TopologiasDeExemplo.Ret(3840, 0, 5760, 1032), 96),
    ]);

    /// <summary><see cref="TresEmLinha"/> depois de desconectar o DISPLAY2: [ 1* ]      [ 3  ].</summary>
    public static Topologia TresEmLinhaSemODoMeio => TopologiasDeExemplo.SemMonitor(TresEmLinha, TopologiasDeExemplo.Display2);

    public Cenario(ConfiguracaoDoNucleo? config = null, ulong semente = 7)
    {
        Config = config ?? new ConfiguracaoDoNucleo();
        Atual = EstadoDoNucleo.Inicial(semente);
    }

    public ConfiguracaoDoNucleo Config { get; }

    public EstadoDoNucleo Atual { get; private set; }

    public Resultado? Ultimo { get; private set; }

    public Retrato Retrato => Atual.Retrato();

    public PontoPx Ancora => Afirmar.NaoNulo(Atual.Lugar, "o personagem tem lugar").Ancora;

    public IReadOnlyList<Transicao> Transicoes => Ultimo?.Transicoes ?? [];

    public IReadOnlyList<Efeito> Efeitos => Ultimo?.Efeitos ?? [];

    /// <summary>Aplica os eventos em ordem; o último resultado fica em <see cref="Ultimo"/>.</summary>
    public Cenario Aplicar(params Evento[] eventos) => AplicarCom(Config, eventos);

    /// <summary>Aplica com outra configuração (por exemplo, para a agenda escolher uma ação só).</summary>
    public Cenario AplicarCom(ConfiguracaoDoNucleo config, params Evento[] eventos)
    {
        foreach (Evento e in eventos)
        {
            Ultimo = Maquina.Aplicar(Atual, e, config);
            Atual = Ultimo.Estado;
        }
        return this;
    }

    /// <summary>Dispara o temporizador da agenda na geração agendada.</summary>
    public Cenario Decidir() => Aplicar(new AutonomyTimer(Atual.Geracao));

    public Cenario Passos(int quantos)
    {
        for (int i = 0; i < quantos; i++) Aplicar(new Tick());
        return this;
    }

    /// <summary>
    /// As preferências dos cenários: as padrão com o tamagotchi completo, o conteúdo adulto e os nove itens ligados. Os
    /// cenários exercitam a mecânica; o padrão do arquivo (adulto desligado, quatro itens marcados; DEC-041) tem os
    /// testes dele em ConteudoAdultoTestes.
    /// </summary>
    public static readonly Preferencias PreferenciasDosCenarios = PreferenciasDeTeste.Completas;

    /// <summary>Núcleo carregado e parado no chão.</summary>
    public static Cenario Parado(ConfiguracaoDoNucleo? config = null, Topologia? topologia = null, ulong semente = 7)
        => new Cenario(config, semente).Aplicar(new Loaded(topologia ?? TopologiasDeExemplo.UmMonitor, null, PreferenciasDosCenarios));

    /// <summary>
    /// Leva o núcleo ao estado pedido pelo caminho da tabela. Para os estados autônomos, a
    /// agenda decide com uma configuração que só permite a ação desejada.
    /// </summary>
    public static Cenario Em(Estado alvo, ConfiguracaoDoNucleo? config = null, Topologia? topologia = null)
    {
        ConfiguracaoDoNucleo cfg = config ?? new ConfiguracaoDoNucleo();
        if (alvo == Estado.Booting) return new Cenario(cfg);
        // USING só existe com o tamagotchi (DEC-028): o cenário o liga.
        if (alvo == Estado.Using) cfg = cfg with { Tamagotchi = true };

        Cenario c = Parado(cfg, topologia);
        switch (alvo)
        {
            case Estado.Idle:
                break;
            case Estado.Pressed:
                c.Aplicar(new Press(PontoOpaco));
                break;
            case Estado.Dragging:
                c.Aplicar(new Press(PontoOpaco), new DragStart());
                break;
            case Estado.Reacting:
                c.Aplicar(new Press(PontoOpaco), new Click());
                break;
            case Estado.Hidden:
                c.Aplicar(new CmdHide());
                break;
            case Estado.Exiting:
                c.Aplicar(new CmdExit());
                break;
            case Estado.Walking:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao));
                break;
            case Estado.Climbing:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Escalar }, new AutonomyTimer(c.Atual.Geracao));
                break;
            case Estado.Jumping:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Pular }, new AutonomyTimer(c.Atual.Geracao));
                break;
            case Estado.Resting:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(c.Atual.Geracao));
                break;
            case Estado.Hanging:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Escalar }, new AutonomyTimer(c.Atual.Geracao))
                    .Aplicar(new MovementSignal(SinalDeMovimento.BordaSuperior));
                break;
            case Estado.Falling:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao))
                    .Aplicar(new MovementSignal(SinalDeMovimento.FimDoChao));
                break;
            case Estado.Landing:
                c.AplicarCom(cfg with { Acoes = AcoesAutonomas.Andar }, new AutonomyTimer(c.Atual.Geracao))
                    .Aplicar(new MovementSignal(SinalDeMovimento.FimDoChao), new MovementSignal(SinalDeMovimento.ContatoComOChao));
                break;
            case Estado.Using:
                // Invoca uma banana, deixa cair até o chão, pega, arrasta e solta no meio do personagem.
                c.Aplicar(new CmdSummonItem(Item.Banana));
                while (c.Atual.Itens.AlgumCaindo) c.Aplicar(new Tick());
                c.SoltarSobreEle(c.Atual.Itens.Todos[^1].Id);
                break;
            default:
                throw new ArgumentException($"{alvo} não é um estado estável para montar cenário.", nameof(alvo));
        }
        Afirmar.Igual(alvo, c.Atual.Estado, $"o cenário chegou a {alvo}");
        return c;
    }

    // ---------------------------------------------------------------- itens (DEC-028)

    /// <summary>
    /// Onde a âncora de um item fica bem no meio do personagem: o centro do item no centro do sprite dele. Solto ali, o
    /// item está sobre ele (<see cref="Maquina.SobreOPersonagem"/>).
    /// </summary>
    public static PontoPx MeioDoPersonagem(EstadoDoNucleo s, ConfiguracaoDoNucleo config)
    {
        Posicionamento l = Afirmar.NaoNulo(s.Lugar, "o personagem tem lugar");
        int alturaDoItem = config.TamanhoDoItem.ParaPixels(l.Monitor.Dpi).Altura;
        return new PontoPx(l.Ancora.X, l.Retangulo.Topo + l.Tamanho.Altura / 2 + alturaDoItem / 2);
    }

    /// <summary>
    /// O gesto inteiro sobre um item, pela janela dele: pega 10 px acima da âncora, arrasta e solta com a âncora do item
    /// em <paramref name="ancoraDoItem"/> (o cursor é ela mais a pegada).
    /// </summary>
    public Cenario ArrastarItem(int id, PontoPx ancoraDoItem)
    {
        ItemNoMundo item = Afirmar.NaoNulo(Atual.Itens.PorId(id), $"o item {id} existe");
        var pegada = new PontoPx(0, -10);
        var cursor = new PontoPx(ancoraDoItem.X + pegada.X, ancoraDoItem.Y + pegada.Y);
        return Aplicar(
            new ItemPress(id, new PontoPx(item.Lugar.Ancora.X + pegada.X, item.Lugar.Ancora.Y + pegada.Y)),
            new ItemDragStart(id), new ItemDragMove(id, cursor), new ItemDragEnd(id, cursor));
    }

    /// <summary>Arrasta o item até o meio do personagem e solta.</summary>
    public Cenario SoltarSobreEle(int id) => ArrastarItem(id, MeioDoPersonagem(Atual, Config));

    // ---------------------------------------------------------------- afirmações

    public Cenario Esta(Estado esperado, string? mensagem = null)
    {
        Afirmar.Igual(esperado, Atual.Estado, mensagem ?? $"estado depois de {Descrever(Transicoes)}");
        return this;
    }

    public Cenario EstaEscondido(MotivoDoOcultamento motivo)
    {
        Afirmar.Igual(Estado.Hidden, Atual.Estado, "escondido");
        Afirmar.Igual(motivo, Atual.Motivo, "motivo do ocultamento");
        return this;
    }

    /// <summary>Confere as transições do último evento, em ordem.</summary>
    public Cenario Percorreu(params Estado[] caminho)
    {
        Afirmar.Verdadeiro(caminho.Length >= 2, "o caminho tem origem e destino");
        var esperado = new List<string>();
        for (int i = 1; i < caminho.Length; i++) esperado.Add($"{caminho[i - 1]}->{caminho[i]}");
        Afirmar.Sequencia(esperado, Transicoes.Select(t => $"{t.De}->{t.Para}"), "transições");
        return this;
    }

    public Cenario SemTransicao()
    {
        Afirmar.Igual(0, Transicoes.Count, $"nenhuma transição (houve: {Descrever(Transicoes)})");
        return this;
    }

    public T Efeito<T>() where T : Efeito
    {
        T[] achados = [.. Efeitos.OfType<T>()];
        Afirmar.Igual(1, achados.Length, $"um efeito {typeof(T).Name} em [{string.Join(", ", Efeitos.Select(e => e.GetType().Name))}]");
        return achados[0];
    }

    public bool Tem<T>() where T : Efeito => Efeitos.OfType<T>().Any();

    public Cenario SemEfeito<T>() where T : Efeito
    {
        Afirmar.Falso(Tem<T>(), $"sem efeito {typeof(T).Name} em [{string.Join(", ", Efeitos.Select(e => e.GetType().Name))}]");
        return this;
    }

    private static string Descrever(IEnumerable<Transicao> transicoes) => string.Join(", ", transicoes.Select(t => $"{t.De}->{t.Para}"));
}
