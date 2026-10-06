namespace Buzzy.Core.Personagem;

/// <summary>
/// De onde vem um evento, da maior para a menor prioridade (ARCHITECTURE.md 2.3): ação direta
/// do usuário sobre o personagem; menu, bandeja e painel de energia; sistema; relógio e
/// movimento; comportamento autônomo; troca de expressão.
/// </summary>
public enum Origem
{
    Expressao = 0,
    Autonomo = 1,
    Relogio = 2,
    Sistema = 3,
    ComandoDoUsuario = 4,
    AcaoDireta = 5,
}

/// <summary>
/// Evento normalizado que o núcleo consome (ARCHITECTURE.md 2.6, tabela de eventos). Os nomes
/// são os da especificação. Coordenadas em pixels físicos do desktop virtual (DEC-008).
/// </summary>
public abstract record Evento
{
    public abstract Origem Origem { get; }
}

// Gestos derivados pela arbitragem de input (ARCHITECTURE.md 2.7). Na Fase 2 os testes os
// entregam prontos; o reconhecedor de gestos é da Fase 3.

/// <summary><c>PRESS</c>: botão esquerdo pressionado sobre pixel opaco, com o cursor em <paramref name="Cursor"/>.</summary>
public sealed record Press(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>CLICK</c>: soltou dentro do retângulo de arraste.</summary>
public sealed record Click : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>DOUBLE_CLICK</c>: segundo clique dentro do tempo e do retângulo do sistema.</summary>
public sealed record DoubleClick : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>DRAG_START</c>: o cursor saiu do retângulo de arraste.</summary>
public sealed record DragStart : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>DRAG_MOVE</c>: posição mais recente do cursor durante o arraste.</summary>
public sealed record DragMove(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>DRAG_END</c>: soltou o botão depois de arrastar.</summary>
public sealed record DragEnd(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>DRAG_CANCEL</c>: a captura foi perdida (Alt+Tab, UAC, outra captura).</summary>
public sealed record DragCancel : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>CONTEXT_MENU</c>: botão direito solto sobre o personagem.</summary>
public sealed record ContextMenu(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Painel de energia (Fase 8).

/// <summary><c>ENERGY_PANEL_OPEN</c>: pedido explícito de abrir o painel (menu "Energia").</summary>
public sealed record EnergyPanelOpen : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>ENERGY_SELECTED</c>: o usuário escolheu um nível no painel aberto.</summary>
public sealed record EnergySelected(NivelDeEnergia Nivel) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>ENERGY_PANEL_CLOSE</c>: o painel fechou (Esc, botão de fechar ou perda de foco).</summary>
public sealed record EnergyPanelClose : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Bandeja e menu.

/// <summary><c>CMD_HIDE</c>.</summary>
public sealed record CmdHide : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_SHOW</c>.</summary>
public sealed record CmdShow : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_PAUSE_AUTONOMY</c>.</summary>
public sealed record CmdPauseAutonomy : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_RESUME_AUTONOMY</c>.</summary>
public sealed record CmdResumeAutonomy : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_OPEN_SETTINGS</c> (Fase 8).</summary>
public sealed record CmdOpenSettings : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_RESET_POSITION</c>: volta à posição inicial.</summary>
public sealed record CmdResetPosition : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_EXIT</c>.</summary>
public sealed record CmdExit : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_DOMINANT_EMOTION</c> (DEC-027): a emoção dominante escolhida no menu, uma das 14 caras de humor
/// (<see cref="Expressoes.DeHumor"/>), ou nula para "Automática". Um valor fora das 14 é ignorado.
/// </summary>
public sealed record CmdSetDominantEmotion(Expressao? Emocao) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_ADULT_CONTENT</c> (DEC-033): "Conteúdo adulto" no menu, ligado ou desligado. Desligar tira do mundo os itens
/// adultos, acaba as ondas de substância e o uso de um item adulto; ligar só grava a escolha.
/// </summary>
public sealed record CmdSetAdultContent(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>CMD_SET_ADULT_ITEM: altera a disponibilidade de um item adulto nas Configurações (DEC-041).</summary>
public sealed record CmdSetAdultItemEnabled(Item Item, bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_FULLSCREEN_MODE</c> (DEC-034): "Desviar da tela cheia" no menu, o modo de tela cheia (Q-09) ligado ou
/// desligado. Grava a escolha nas preferências. Desligar desfaz o efeito temporário, como as preferências; ligar com ele à
/// vista num monitor já ocupado o tira de lá.
/// </summary>
public sealed record CmdSetFullscreenMode(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_ENERGY</c> (Fase 8; DEC-038, item 2): a energia escolhida na janela de configurações, sem o painel. Muda só a
/// preferência e a grava; a próxima decisão da agenda já usa o perfil novo.
/// </summary>
public sealed record CmdSetEnergy(NivelDeEnergia Nivel) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_ALWAYS_ON_TOP</c> (Fase 8; DEC-038, item 8): "Sempre no topo" nas configurações. Muda a preferência, a grava e
/// pede à raiz que a aplique (<see cref="AplicarSempreNoTopo"/>).
/// </summary>
public sealed record CmdSetAlwaysOnTop(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SET_SCALE</c> (Fase 8; DEC-038, item 9): o tamanho do Buzzy nas configurações. Só muda a preferência e a grava:
/// vale na próxima abertura.
/// </summary>
public sealed record CmdSetScale(EscalaDoPersonagem Escala) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary>
/// <c>CMD_SUMMON_ITEM</c> (DEC-028): o usuário invocou um item pelo menu. Ele aparece ao lado do personagem, acima do
/// chão, e cai. Escondido, antes da carga, fora do enum ou com o tamagotchi desligado, é ignorado.
/// </summary>
public sealed record CmdSummonItem(Item Item) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

/// <summary><c>CMD_CLEAR_ITEMS</c> (DEC-028): "Recolher itens" do menu; todos os itens somem, inclusive o da mão.</summary>
public sealed record CmdClearItems : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Gestos sobre a janela de um item (DEC-028), derivados por um árbitro de gestos próprio da janela: Press, DragStart,
// DragMove, DragEnd e, para Click, DoubleClick ou DragCancel, ItemRelease. O botão direito no item é o ContextMenu de
// sempre. Com o tamagotchi desligado, todos são ignorados.

/// <summary><c>ITEM_PRESS</c>: botão esquerdo pressionado sobre um pixel opaco do item, com o cursor em <paramref name="Cursor"/>.</summary>
public sealed record ItemPress(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>ITEM_DRAG_START</c>: o cursor saiu do retângulo de arraste com o item seguro.</summary>
public sealed record ItemDragStart(int Id) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>ITEM_DRAG_MOVE</c>: posição mais recente do cursor durante o arraste do item.</summary>
public sealed record ItemDragMove(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary><c>ITEM_DRAG_END</c>: soltou o item depois de arrastar; sobre o personagem, num estado que aceita, ele o usa.</summary>
public sealed record ItemDragEnd(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

/// <summary>
/// <c>ITEM_RELEASE</c>: o gesto sobre o item acabou sem arraste até um lugar (clique, clique duplo ou captura perdida):
/// o item cai de onde está e nunca é usado.
/// </summary>
public sealed record ItemRelease(int Id) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Sistema.

/// <summary>
/// Configurações e topologia carregadas (primeira linha da tabela de transições). A posição salva e as
/// preferências vêm do settings.json (Fase 5, <see cref="Persistencia.EsquemaDeConfiguracoes"/>). A
/// posição, quando há, é restaurada pela cascata da partida (<see cref="Posicionador.Restaurar"/>); nula, o
/// personagem começa na posição inicial.
/// </summary>
public sealed record Loaded(Topologia Topologia, PosicaoDoPersonagem? PosicaoSalva, Preferencias Preferencias) : Evento
{
    public override Origem Origem => Origem.Sistema;

    /// <summary>
    /// A borda do esconderijo gravada com a posição (DEC-025; esquema v3, DEC-029, item 11): a acomodação da carga o
    /// devolve escondido no mesmo lado. Só vale com <see cref="PosicaoSalva"/> e com o esconderijo pelo clique duplo
    /// ligado na configuração; fora do enum, nenhum.
    /// </summary>
    public LadoDoEsconderijo Esconderijo { get; init; }

    /// <summary>
    /// A marca "preso pelo usuário" gravada com a posição (DEC-024; esquema v3): agarrado na carga, ele continua preso
    /// onde o usuário o deixou. Só vale com <see cref="PosicaoSalva"/>; longe da parede e do cipó, a acomodação a apaga.
    /// </summary>
    public bool PresoPeloUsuario { get; init; }
}

/// <summary>
/// <c>TOPOLOGY_CHANGED</c>: nova leitura dos monitores, já agrupada pelo adaptador. Com o monitor do personagem só transladado
/// ou igual, o estado continua; senão, ele revalida a posição (DEC-030, Maquina.MudarTopologia).
/// </summary>
public sealed record TopologyChanged(Topologia Topologia) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>SESSION_LOCKED</c>.</summary>
public sealed record SessionLocked : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>SESSION_UNLOCKED</c>.</summary>
public sealed record SessionUnlocked : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>SUSPENDING</c>.</summary>
public sealed record Suspending : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>RESUMED</c>.</summary>
public sealed record Resumed : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>SESSION_ENDING</c>.</summary>
public sealed record SessionEnding : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary>
/// <c>FULLSCREEN_TARGETS_CHANGED</c> (DEC-013): só as chaves dos monitores cobertos pela janela
/// ativa em tela cheia, sem identidade nem conteúdo de outra janela (invariante 13).
/// </summary>
public sealed record FullscreenTargetsChanged(MonitoresOcupados Ocupados) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

/// <summary><c>SETTINGS_CHANGED</c>.</summary>
public sealed record SettingsChanged(Preferencias Preferencias) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

// Relógio e movimento.

/// <summary><c>TICK</c>: um passo fixo do relógio lógico (1/60 s por padrão).</summary>
public sealed record Tick : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>
/// Sinal do módulo de movimento (parede, passagem, contato com o chão). A partir da Fase 4 sai
/// do próprio passo físico; na Fase 2 só os testes o injetam.
/// </summary>
public sealed record MovementSignal(SinalDeMovimento Sinal) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>
/// <c>AUTONOMY_TIMER</c>: disparo do temporizador único da agenda autônoma. A geração evita que
/// um disparo antigo, que chegou depois de cancelado, seja tomado pelo atual.
/// </summary>
public sealed record AutonomyTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Autonomo;
}

/// <summary>
/// <c>ITEM_EFFECT_TIMER</c> (DEC-028): disparo do temporizador único da onda de um item, que a máquina agendou com
/// <see cref="AgendarOnda"/>. A geração evita que um disparo antigo seja tomado pelo atual. Tem a prioridade do
/// relógio: não é descartado com o usuário no controle e não encerra um gesto. Com o tamagotchi desligado, é ignorado.
/// </summary>
public sealed record ItemEffectTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>
/// FOREGROUND_MONITOR_CHANGED (Fase 7; DEC-037, item 2): a chave opaca do monitor da janela em primeiro plano, confirmada
/// pelo adaptador depois da carência e só quando muda. Nada da janela vem junto. Abaixo do sistema: não encerra um gesto.
/// </summary>
public sealed record ForegroundMonitorChanged(string Chave) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>
/// ACTIVE_WINDOW_SPAN (Fase 7; DEC-037, item 2): a resposta ao pedido do vão (<see cref="PedirVaoDaJanelaAtiva"/>), com a
/// mesma geração; nulo sem janela ou sem trecho no monitor.
/// </summary>
public sealed record ActiveWindowSpan(long Geracao, VaoDaJanela? Vao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>CURIOSITY_TIMER (Fase 7; DEC-037, item 3): o disparo único da curiosidade, com a geração do agendamento.</summary>
public sealed record CuriosityTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

/// <summary>
/// Troca de expressão pedida pela personalidade. Vale em qualquer estado e nunca muda estado de
/// comportamento nem posição (invariante 6).
/// </summary>
public sealed record ExpressionChange(Expressao Expressao) : Evento
{
    public override Origem Origem => Origem.Expressao;
}

/// <summary>Conjunto imutável, ordenado e sem repetição de chaves de monitor, com igualdade por valor.</summary>
public sealed class MonitoresOcupados : IEquatable<MonitoresOcupados>
{
    public static readonly MonitoresOcupados Nenhum = new([]);

    private readonly string[] _chaves;

    public MonitoresOcupados(IEnumerable<string> chaves)
    {
        ArgumentNullException.ThrowIfNull(chaves);
        _chaves = [.. chaves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (_chaves.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Chave de monitor vazia.", nameof(chaves));
    }

    public IReadOnlyList<string> Chaves => _chaves;

    public bool Vazio => _chaves.Length == 0;

    public bool Contem(string chave) => Array.BinarySearch(_chaves, chave, StringComparer.Ordinal) >= 0;

    public bool Equals(MonitoresOcupados? outro) => outro is not null && _chaves.AsSpan().SequenceEqual(outro._chaves);

    public override bool Equals(object? obj) => Equals(obj as MonitoresOcupados);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (string chave in _chaves) hash.Add(chave, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(",", _chaves);
}

/// <summary>
/// Preferências que o núcleo usa. Desde a Fase 5 são guardadas no settings.json
/// (<see cref="Persistencia.EsquemaDeConfiguracoes"/>); a Fase 8 acrescenta as demais.
/// </summary>
/// <param name="Energia">Nível de energia; padrão Média (DEC-014).</param>
/// <param name="ModoTelaCheia">Modo automático de tela cheia (Q-09); padrão ligado.</param>
/// <param name="AtravessarMonitores">
/// Se o personagem pode passar sozinho de um monitor para outro (Q-05); padrão ligado. É a escolha do
/// usuário; até a travessia entrar no núcleo, só é guardada e reproduzida.
/// </param>
public sealed record Preferencias(NivelDeEnergia Energia, bool ModoTelaCheia, bool AtravessarMonitores = true)
{
    public static readonly Preferencias Padrao = new(NivelDeEnergia.Media, true, true);

    /// <summary>
    /// A emoção dominante (DEC-027): uma das 14 caras de humor (<see cref="Expressoes.DeHumor"/>), que vira a cara
    /// de base e a mais sorteada nas trocas de expressão; nula, "Automática", como antes. Só muda as caras: nunca as
    /// ações, os pesos da agenda nem a física. Fica fora do construtor posicional, e o padrão é a automática.
    /// </summary>
    public Expressao? EmocaoDominante { get; init; }

    /// <summary>
    /// A chave geral do conteúdo adulto (DEC-033): desligada por padrão; desligada, nenhum item adulto nem efeito de
    /// substância aparece ou acontece. Fica separada das escolhas individuais dos itens adultos.
    /// </summary>
    public bool ConteudoAdulto { get; init; }

    /// <summary>
    /// Itens adultos permitidos individualmente (DEC-041). O padrão habilita vodka, cerveja, cigarro e baseado. As caixas
    /// continuam configuráveis com a chave geral desligada, mas isso não libera o conteúdo enquanto ela estiver desligada.
    /// </summary>
    public ConjuntoDeItens ItensAdultosHabilitados { get; init; } = ItensAdultosPadrao;

    /// <summary>Seleção inicial de conteúdo adulto, conforme o pedido do usuário de 2026-10-05.</summary>
    public static ConjuntoDeItens ItensAdultosPadrao => ConjuntoDeItens.Vazio
        .Com(Item.Vodka).Com(Item.Cerveja).Com(Item.Cigarro).Com(Item.Baseado);

    /// <summary>Os nove itens adultos (DEC-033 e DEC-041), todos marcados: o que valia antes da DEC-041.</summary>
    public static ConjuntoDeItens TodosOsItensAdultos
    {
        get
        {
            ConjuntoDeItens todos = ConjuntoDeItens.Vazio;
            foreach (Item item in TabelaDoTamagotchi.Itens)
                if (TabelaDoTamagotchi.Adulto(item)) todos = todos.Com(item);
            return todos;
        }
    }

    /// <summary>Remove itens não adultos de um conjunto recebido de arquivo ou preferência externa.</summary>
    public static ConjuntoDeItens NormalizarItensAdultos(ConjuntoDeItens itens)
    {
        ConjuntoDeItens normalizados = ConjuntoDeItens.Vazio;
        foreach (Item item in TabelaDoTamagotchi.Itens)
            if (TabelaDoTamagotchi.Adulto(item) && itens.Contem(item)) normalizados = normalizados.Com(item);
        return normalizados;
    }

    /// <summary>
    /// Se o personagem e os itens ficam sempre no topo (Q-03; DEC-038, item 8). Ligado por padrão; desligado, a ordem Z só
    /// muda por pedido do usuário (mostrar pela bandeja ou pelo menu). Quem aplica é a raiz; o núcleo só guarda e grava.
    /// Fica fora do construtor posicional.
    /// </summary>
    public bool SempreNoTopo { get; init; } = true;

    /// <summary>
    /// O tamanho do personagem (DEC-038, item 9), que vale na próxima abertura: a raiz cria o núcleo e as janelas com ele.
    /// O núcleo só guarda e grava. Fica fora do construtor posicional; o padrão é a Média, o tamanho de antes.
    /// </summary>
    public EscalaDoPersonagem Escala { get; init; } = EscalaDoPersonagem.Media;
}
