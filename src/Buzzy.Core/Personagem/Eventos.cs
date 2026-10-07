namespace Buzzy.Core.Personagem;

// Prioridade, da maior pra menor: ação direta sobre o personagem; menu,
// bandeja e painel; sistema; relógio e movimento; autônomo; troca de expressão.
public enum Origem
{
    Expressao = 0,
    Autonomo = 1,
    Relogio = 2,
    Sistema = 3,
    ComandoDoUsuario = 4,
    AcaoDireta = 5,
}

// Evento normalizado que o núcleo consome. Coordenadas em px físicos do
// desktop virtual.
public abstract record Evento
{
    public abstract Origem Origem { get; }
}

// Gestos já derivados pelo árbitro de input.

// Botão esquerdo sobre pixel opaco.
public sealed record Press(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Soltou dentro do retângulo de arraste.
public sealed record Click : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Tempo e retângulo de clique duplo do sistema.
public sealed record DoubleClick : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// O cursor saiu do retângulo de arraste.
public sealed record DragStart : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

public sealed record DragMove(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

public sealed record DragEnd(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Captura perdida (Alt+Tab, UAC, outra captura).
public sealed record DragCancel : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Botão direito solto sobre o personagem.
public sealed record ContextMenu(PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Painel de energia.

// Pedido explícito pelo menu "Energia".
public sealed record EnergyPanelOpen : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record EnergySelected(NivelDeEnergia Nivel) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Esc, botão de fechar ou perda de foco.
public sealed record EnergyPanelClose : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Bandeja e menu.

public sealed record CmdHide : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdShow : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdPauseAutonomy : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdResumeAutonomy : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdOpenSettings : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Volta à posição inicial.
public sealed record CmdResetPosition : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdExit : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Uma das 14 caras de humor (Expressoes.DeHumor), ou nula pra "Automática".
// Valor fora das 14 é ignorado.
public sealed record CmdSetDominantEmotion(Expressao? Emocao) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Desligar tira os itens adultos do mundo e acaba as ondas de substância e o
// uso de item adulto; ligar só grava a escolha.
public sealed record CmdSetAdultContent(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Uso por conta própria de uma das seis drogas ilícitas. Item fora da
// edição é ignorado.
public sealed record CmdSetSelfUseItem(Item Item, bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

public sealed record CmdSetAdultItemEnabled(Item Item, bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// "Desviar da tela cheia". Desligar desfaz o efeito temporário; ligar com ele
// visível num monitor já ocupado tira ele de lá.
public sealed record CmdSetFullscreenMode(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Só vale pras próximas decisões; uma travessia em curso termina.
public sealed record CmdSetCrossMonitors(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Energia escolhida nas configurações, sem o painel. A próxima decisão da
// agenda já usa o perfil novo.
public sealed record CmdSetEnergy(NivelDeEnergia Nivel) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Grava e pede pra raiz aplicar (AplicarSempreNoTopo).
public sealed record CmdSetAlwaysOnTop(bool Ligado) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Só grava: vale na próxima abertura.
public sealed record CmdSetScale(EscalaDoPersonagem Escala) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Aparece ao lado do personagem, acima do chão, e cai. Ignorado se escondido,
// antes da carga, fora do enum ou com o tamagotchi desligado.
public sealed record CmdSummonItem(Item Item) : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// "Recolher itens": somem todos, inclusive o da mão.
public sealed record CmdClearItems : Evento
{
    public override Origem Origem => Origem.ComandoDoUsuario;
}

// Gestos sobre a janela de um item, com árbitro próprio: Press, DragStart,
// DragMove, DragEnd e, pra Click, DoubleClick ou DragCancel, ItemRelease. O botão direito no item é o ContextMenu de
// sempre. Com o tamagotchi desligado, todos são ignorados.

// Botão esquerdo sobre pixel opaco do item.
public sealed record ItemPress(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

public sealed record ItemDragStart(int Id) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

public sealed record ItemDragMove(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Solto sobre o personagem, num estado que aceita, ele usa o item.
public sealed record ItemDragEnd(int Id, PontoPx Cursor) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Gesto acabou sem arrastar até um lugar (clique, clique duplo ou captura
// perdida): o item cai de onde está e nunca é usado.
public sealed record ItemRelease(int Id) : Evento
{
    public override Origem Origem => Origem.AcaoDireta;
}

// Sistema.

// Configurações e topologia carregadas. Posição e preferências vêm do
// settings.json; a posição, se houver, passa pela cascata de
// Posicionador.Restaurar. Nula, começa na posição inicial.
public sealed record Loaded(Topologia Topologia, PosicaoDoPersonagem? PosicaoSalva, Preferencias Preferencias) : Evento
{
    public override Origem Origem => Origem.Sistema;

    // Volta escondido do mesmo lado. Só vale com PosicaoSalva e com o
    // esconderijo do clique duplo ligado; fora do enum, nenhum.
    public LadoDoEsconderijo Esconderijo { get; init; }

    // Agarrado na carga, continua preso onde o usuário deixou. Só vale com
    // PosicaoSalva; longe da parede e do cipó, a acomodação apaga.
    public bool PresoPeloUsuario { get; init; }
}

// Monitores já agrupados pelo adaptador. Se o monitor do personagem só
// transladou ou ficou igual, segue o estado; senão revalida a posição
// (Maquina.MudarTopologia).
public sealed record TopologyChanged(Topologia Topologia) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record SessionLocked : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record SessionUnlocked : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record Suspending : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record Resumed : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record SessionEnding : Evento
{
    public override Origem Origem => Origem.Sistema;
}

// Só as chaves dos monitores cobertos pela janela ativa em tela cheia; nada
// sobre a identidade ou o conteúdo da janela.
public sealed record FullscreenTargetsChanged(MonitoresOcupados Ocupados) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

public sealed record SettingsChanged(Preferencias Preferencias) : Evento
{
    public override Origem Origem => Origem.Sistema;
}

// Relógio e movimento.

// Um passo fixo (1/60 s por padrão).
public sealed record Tick : Evento
{
    public override Origem Origem => Origem.Relogio;
}

// Parede, passagem, contato com o chão. Normalmente sai do passo físico;
// os testes também injetam.
public sealed record MovementSignal(SinalDeMovimento Sinal) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

// A geração impede que um disparo velho, chegado depois de cancelado, seja
// tomado pelo atual.
public sealed record AutonomyTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Autonomo;
}

// Timer da onda (AgendarOnda), com geração pelo mesmo motivo. Tem prioridade
// de relógio: não é descartado com o usuário no controle nem encerra gesto.
// Com o tamagotchi desligado, é ignorado.
public sealed record ItemEffectTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

// Chave opaca do monitor da janela em primeiro plano, depois da carência e
// só quando muda. Nada da janela vem junto. Abaixo do sistema: não encerra gesto.
public sealed record ForegroundMonitorChanged(string Chave) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

// Resposta a PedirVaoDaJanelaAtiva, com a mesma geração. Nulo sem janela ou
// sem trecho no monitor.
public sealed record ActiveWindowSpan(long Geracao, VaoDaJanela? Vao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

public sealed record CuriosityTimer(long Geracao) : Evento
{
    public override Origem Origem => Origem.Relogio;
}

// Vale em qualquer estado e nunca muda estado de comportamento nem posição.
public sealed record ExpressionChange(Expressao Expressao) : Evento
{
    public override Origem Origem => Origem.Expressao;
}

// Imutável, ordenado, sem repetição, com igualdade por valor.
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

// Preferências gravadas no settings.json.
// AtravessarMonitores: o Padrao é desligado (sem dois monitores ele parecia
// se comportar mal), mas o default do parâmetro segue true pra quem constrói
// na mão.
public sealed record Preferencias(NivelDeEnergia Energia, bool ModoTelaCheia, bool AtravessarMonitores = true)
{
    public static readonly Preferencias Padrao = new(NivelDeEnergia.Media, true, false);

    // Uma das 14 caras de humor: vira a cara de base e a mais sorteada. Nula
    // = "Automática". Só mexe nas caras, nunca em ações, pesos ou física.
    public Expressao? EmocaoDominante { get; init; }

    // Chave geral, desligada por padrão: desligada, nenhum item adulto nem
    // efeito de substância acontece. Separada das escolhas por item.
    public bool ConteudoAdulto { get; init; }

    // Dá pra mexer nas caixas com a chave geral desligada, mas isso não libera
    // nada enquanto ela estiver desligada.
    public ConjuntoDeItens ItensAdultosHabilitados { get; init; } = ItensAdultosPadrao;

    // Vodka, cerveja e cigarro; o baseado começa desmarcado.
    public static ConjuntoDeItens ItensAdultosPadrao => ConjuntoDeItens.Vazio
        .Com(Item.Vodka).Com(Item.Cerveja).Com(Item.Cigarro);

    // Os nove adultos marcados.
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

    // Só as seis ilícitas, e cada uma também precisa estar marcada nos itens
    // adultos com a chave geral ligada. Nenhuma por padrão, nem o baseado.
    public ConjuntoDeItens ItensPorContaPropria { get; init; } = ConjuntoDeItens.Vazio;

    // Filtra o que veio de arquivo ou de fora: só as seis ilícitas.
    public static ConjuntoDeItens NormalizarPorContaPropria(ConjuntoDeItens itens)
    {
        ConjuntoDeItens normalizados = ConjuntoDeItens.Vazio;
        foreach (Item item in TabelaDoTamagotchi.Itens)
            if (TabelaDoTamagotchi.Ilicitos.Contem(item) && itens.Contem(item)) normalizados = normalizados.Com(item);
        return normalizados;
    }

    // Filtra o que veio de arquivo ou de fora: só itens adultos.
    public static ConjuntoDeItens NormalizarItensAdultos(ConjuntoDeItens itens)
    {
        ConjuntoDeItens normalizados = ConjuntoDeItens.Vazio;
        foreach (Item item in TabelaDoTamagotchi.Itens)
            if (TabelaDoTamagotchi.Adulto(item) && itens.Contem(item)) normalizados = normalizados.Com(item);
        return normalizados;
    }

    // Personagem e itens. Desligado, a ordem Z só muda a pedido do usuário
    // (mostrar pela bandeja ou menu). Quem aplica é a raiz; aqui só guarda.
    public bool SempreNoTopo { get; init; } = true;

    // Vale na próxima abertura: a raiz cria o núcleo e as janelas com ela.
    public EscalaDoPersonagem Escala { get; init; } = EscalaDoPersonagem.Media;
}
