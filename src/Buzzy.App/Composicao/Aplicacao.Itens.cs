using System.Diagnostics;
using System.Windows.Threading;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// A parte da raiz de composição do tamagotchi adulto (DEC-028, passo T8): as janelas dos itens
/// (<see cref="GerenteDosItens"/>), os gestos sobre elas, num árbitro próprio (<see cref="GestosDosItens"/>), e o
/// temporizador da onda (<see cref="TemporizadorDaOnda"/>). A configuração do aplicativo liga a chave do tamagotchi desde o
/// passo T9; com ela desligada no núcleo, nenhum efeito novo chega aqui e nenhuma janela de item é criada.
/// </summary>
internal sealed partial class Aplicacao
{
    private readonly GestosDosItens _gestosDosItens = new();

    /// <summary>M5 do arraste do item em curso, só com <c>--diagnostico</c>: ms de cada movimento até a janela no lugar.</summary>
    private readonly List<double> _latenciasDoArrasteDoItem = [];

    private GerenteDosItens? _itens;
    private TemporizadorDaOnda? _onda;

    /// <summary>Logo depois de criar a janela do personagem: o gerente das janelas dos itens e o temporizador da onda.</summary>
    private void IniciarItens()
    {
        _itens = new GerenteDosItens(CriarJanelaDoItem, () => _personagem?.Hwnd ?? 0, ItemParadoNoChao);
        _itens.Ponteiro += AoPonteiroDoItem;
        _onda = new TemporizadorDaOnda(AoDispararOnda);
    }

    /// <summary>A janela de um item, criada escondida, no tamanho do item do núcleo.</summary>
    private IJanelaDoItem CriarJanelaDoItem(int id)
    {
        var janela = new JanelaDoItem(id, _nucleo?.Configuracao.TamanhoDoItem ?? SpriteDoItem.TamanhoLogico);
        janela.CriarSemMostrar();
        return janela;
    }

    private bool ItemParadoNoChao(int id) => _nucleo?.Estado.Itens.PorId(id) is { Situacao: SituacaoDoItem.NoChao };

    /// <summary>
    /// Ponteiro sobre uma janela de item, ou capturado num gesto começado num item: o árbitro dos itens decide o gesto,
    /// a captura fica só na janela do item do gesto (no topo, durante ele; crítica, L17), e o núcleo aplica cada evento
    /// no mesmo tratamento da mensagem — um movimento do arraste vira o lugar da janela antes de a mensagem acabar.
    /// </summary>
    private void AoPonteiroDoItem(int id, EventoDePonteiro evento)
    {
        if (_encerrando || _nucleo is null || _itens is null) return;
        long recebido = Stopwatch.GetTimestamp();
        GestoDoItem gesto = LigacaoDosItens.ReceberPonteiro(_gestosDosItens, _itens, id, evento);

        bool moveu = false;
        foreach (Evento e in gesto.Eventos)
        {
            if (e is ItemDragStart) _latenciasDoArrasteDoItem.Clear();
            if (e is ContextMenu) Diagnostico.Evento("ITEM", ("menuPedido", id));
            int? solto = e switch { ItemDragEnd f => f.Id, ItemRelease r => r.Id, _ => null };
            bool existia = solto is { } s && _nucleo.Estado.Itens.PorId(s) is not null;
            Enviar(e, "ponteiro no item");
            moveu |= e is ItemDragMove;
            if (solto is { } idSolto && existia) RegistrarSoltura(e, idSolto);
        }

        if (moveu && Diagnostico.Ligado)
            _latenciasDoArrasteDoItem.Add(Stopwatch.GetElapsedTime(recebido).TotalMilliseconds);
    }

    /// <summary>
    /// Só com <c>--diagnostico</c>: o fim do gesto sobre um item. "Sobre" é a regra do núcleo (crítica, C14:
    /// <see cref="Maquina.SobreOPersonagem"/> com o retângulo do item já preso na área útil); "usado", se ele usou o item
    /// (a janela some). Com o resumo do arraste do item (M5), como o do personagem.
    /// </summary>
    private void RegistrarSoltura(Evento fim, int id)
    {
        double[] ms = [.. _latenciasDoArrasteDoItem.Order()];
        _latenciasDoArrasteDoItem.Clear();
        if (!Diagnostico.Ligado || _nucleo is null) return;
        ItemNoMundo? item = _nucleo.Estado.Itens.PorId(id);
        bool usado = item is null && fim is ItemDragEnd;
        bool sobre = usado || (item is not null && _nucleo.Estado.Lugar is { } lugar
            && Maquina.SobreOPersonagem(item.Lugar.Retangulo, lugar.Retangulo, _nucleo.Configuracao.MargemDoAlvo));
        var campos = new List<(string, object?)>
        {
            ("solto", id),
            ("fim", fim.GetType().Name),
            ("sobre", sobre ? "sim" : "nao"),
            ("usado", usado ? "sim" : "nao"),
            ("movimentos", ms.Length),
        };
        if (ms.Length > 0)
        {
            double p95 = ms[Math.Min(ms.Length - 1, (int)Math.Ceiling(ms.Length * 0.95) - 1)];
            campos.Add(("m5MediaMs", Math.Round(ms.Average(), 3)));
            campos.Add(("m5P95Ms", Math.Round(p95, 3)));
            campos.Add(("m5MaxMs", Math.Round(ms[^1], 3)));
        }
        Diagnostico.Evento("ITEM", [.. campos]);
    }

    /// <summary>Os efeitos novos do núcleo (DEC-028): janelas dos itens, captura do item e temporizador da onda.</summary>
    private void ExecutarEfeitoDoTamagotchi(Efeito efeito)
    {
        // O gesto sobre o item acabou por conta do núcleo: o resumo do arraste dele (M5) não sai.
        if (efeito is LiberarCapturaDoItem) _latenciasDoArrasteDoItem.Clear();
        LigacaoDosItens.Executar(efeito, _gestosDosItens, _itens, _onda);
    }

    /// <summary>O temporizador da onda disparou: ITEM_EFFECT_TIMER com a geração agendada.</summary>
    private void AoDispararOnda(long geracao)
    {
        Diagnostico.Evento("ONDA", ("disparada", "sim"), ("geracao", geracao));
        Enviar(new ItemEffectTimer(geracao), $"onda geração {geracao}");
    }

    /// <summary>Encerramento: o temporizador da onda para, o gesto sobre um item é esquecido e todas as janelas dos itens fecham.</summary>
    private void EncerrarItens()
    {
        _onda?.Parar();
        _gestosDosItens.Reiniciar();
        _latenciasDoArrasteDoItem.Clear();
        _itens?.FecharTodas();
    }
}

/// <summary>
/// A ligação da raiz com os itens do tamagotchi (DEC-028), separada do resto da raiz para ser testada sem ela e sem janela
/// de verdade: o que um evento de ponteiro numa janela de item faz com a captura e a ordem Z (<see cref="ReceberPonteiro"/>)
/// e o que cada efeito novo do núcleo faz com o árbitro dos itens, as janelas e o temporizador da onda
/// (<see cref="Executar"/>). Só na thread da interface.
/// </summary>
internal static class LigacaoDosItens
{
    /// <summary>
    /// Um evento de ponteiro na janela do item <paramref name="id"/>: o árbitro dos itens decide o gesto, e a captura
    /// acompanha o gesto. A janela do item em que ele acabou (ou de onde ele passou para outro item) solta o mouse sem virar
    /// captura perdida e volta para baixo do personagem; a do botão pressionado captura e vai ao topo (crítica, L17).
    /// Devolve o gesto, com os eventos para a fila do núcleo.
    /// </summary>
    internal static GestoDoItem ReceberPonteiro(GestosDosItens gestos, GerenteDosItens itens, int id, EventoDePonteiro evento)
    {
        ArgumentNullException.ThrowIfNull(gestos);
        ArgumentNullException.ThrowIfNull(itens);
        int? antes = gestos.ItemEmGesto;
        GestoDoItem gesto = gestos.Receber(id, evento);
        if (antes is { } anterior && anterior != gesto.ItemEmGesto) itens.TerminarGesto(anterior);
        if (gesto.ItemEmGesto is { } atual && (atual != antes || evento is PonteiroPressionado)) itens.ComecarGesto(atual);
        return gesto;
    }

    /// <summary>
    /// Um efeito novo do núcleo (DEC-028): as janelas dos itens (<paramref name="itens"/>), o fim do gesto sobre um item
    /// e o temporizador da onda (<paramref name="onda"/>). Sem as janelas ou o temporizador (antes de criados), a parte
    /// deles não faz nada. Um efeito que não é do tamagotchi lança.
    /// </summary>
    internal static void Executar(Efeito efeito, GestosDosItens gestos, GerenteDosItens? itens, TemporizadorDaOnda? onda)
    {
        ArgumentNullException.ThrowIfNull(gestos);
        switch (efeito)
        {
            case MostrarItem or MoverItem or EsconderItem:
                itens?.Executar(efeito);
                break;

            case RemoverItem remover:
                // Um item que sai no meio do próprio gesto (o núcleo solta a captura antes; aqui, por defesa): o árbitro esquece.
                if (gestos.ItemEmGesto == remover.Id) gestos.Reiniciar();
                itens?.Executar(remover);
                break;

            case LiberarCapturaDoItem liberar:
                // O núcleo encerrou por conta própria o gesto sobre o item (esconder, minimizar, bloquear a sessão, sair,
                // recolher ou pegar outro no meio dele): o árbitro dos itens esquece o gesto, sem gerar ITEM_RELEASE, e a
                // janela solta o mouse. Um soltar que chegue depois não vira nada.
                if (gestos.ItemEmGesto == liberar.Id) gestos.Reiniciar();
                itens?.Executar(liberar);
                break;

            case AgendarOnda agendar:
                Diagnostico.Evento("ONDA", ("agendada", "sim"), ("atrasoMs", (long)agendar.Atraso.TotalMilliseconds), ("geracao", agendar.Geracao));
                onda?.Agendar(agendar.Atraso, agendar.Geracao);
                break;

            case CancelarOnda:
                Diagnostico.Evento("ONDA", ("cancelada", "sim"), ("pendente", onda?.Pendente == true ? "sim" : "nao"));
                onda?.Cancelar();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(efeito), efeito, "Efeito do tamagotchi sem adaptador.");
        }
    }

    /// <summary>
    /// A linha de diagnóstico do sorteio da paranoia (pedidos do usuário de 2026-10-01; DEC-028), só com
    /// <c>--diagnostico</c>: o núcleo não escreve o sorteio que não sai na regra do soltar, para a linha canônica e as
    /// reproduções gravadas não mudarem, e a raiz o registra à parte. Houve sorteio no evento se o gerador da paranoia mudou:
    /// só o sorteio dela o usa, um passo cada, no uso que fecha a mistura com droga sintética, uma vez por episódio; e saiu
    /// se esse uso começou a paranoia. Devolve os campos da linha <c>PARANOIA</c> (o item do uso, a chance, se saiu e a carga
    /// do episódio: as substâncias e os itens distintos), ou nulo sem sorteio.
    /// </summary>
    internal static (string Campo, object? Valor)[]? SorteioDaParanoia(EstadoDoNucleo antes, EstadoDoNucleo depois, Chance chance)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(depois);
        ArgumentNullException.ThrowIfNull(chance);
        if (antes.AleatorioDaParanoia == depois.AleatorioDaParanoia) return null;
        return
        [
            ("item", depois.Uso?.Item.ToString() ?? "-"),
            ("chance", chance.ToString()),
            ("saiu", depois.Uso?.ComecouAParanoia == true ? "sim" : "nao"),
            ("substancias", depois.Carga.Substancias),
            ("distintas", depois.Carga.Distintas.ToString()),
        ];
    }
}

/// <summary>
/// O resultado de um evento de ponteiro sobre uma janela de item.
/// </summary>
/// <param name="Eventos">Os eventos para a fila do núcleo, na ordem.</param>
/// <param name="Capturar">Se a janela do item do gesto fica com a captura do mouse depois deste evento.</param>
/// <param name="ItemEmGesto">O item do gesto do botão esquerdo em curso depois deste evento, ou nulo.</param>
internal readonly record struct GestoDoItem(IReadOnlyList<Evento> Eventos, bool Capturar, int? ItemEmGesto);

/// <summary>
/// Os gestos sobre as janelas dos itens do tamagotchi (DEC-028; desenho do núcleo, 2.4; D9 do desenho do app): uma
/// segunda instância de <see cref="ArbitroDeGestos"/>, só para os itens, com as mesmas regras do personagem (limiar de
/// arraste do sistema, clique duplo, ClickLock, captura perdida). Cada gesto vira o evento do item em que o botão
/// esquerdo foi pressionado: Press → ITEM_PRESS, DragStart → ITEM_DRAG_START, DragMove → ITEM_DRAG_MOVE, DragEnd →
/// ITEM_DRAG_END, e Click, DoubleClick ou DragCancel → ITEM_RELEASE (o item cai de onde está). O botão direito solto num
/// item é o CONTEXT_MENU de sempre: abre o mesmo menu do personagem (crítica, C15). Um botão pressionado noutro item sem o
/// soltar do anterior (mensagens postadas) larga o anterior antes de pegar o novo, nessa ordem. Só na thread da interface.
/// </summary>
internal sealed class GestosDosItens
{
    private readonly ArbitroDeGestos _arbitro = new();

    /// <summary>O item do gesto do botão esquerdo em curso, ou nulo.</summary>
    internal int? ItemEmGesto { get; private set; }

    /// <summary>Um evento de ponteiro que chegou à janela do item <paramref name="id"/>.</summary>
    internal GestoDoItem Receber(int id, EventoDePonteiro evento)
    {
        Arbitragem arbitragem = _arbitro.Receber(evento);
        var eventos = new List<Evento>(arbitragem.Gestos.Count);
        int? doGesto = ItemEmGesto;
        foreach (Evento gesto in arbitragem.Gestos)
        {
            // O botão pressionado começa o gesto no item da janela; o resto do gesto é do item em que ele começou, mesmo
            // que a mensagem chegue por outra janela. O cancelamento que antecede um Press é do gesto anterior.
            if (gesto is Press) doGesto = id;
            eventos.Add(Traduzir(gesto, doGesto ?? id));
        }
        ItemEmGesto = arbitragem.Capturar ? doGesto ?? id : null;
        return new GestoDoItem(eventos, arbitragem.Capturar, ItemEmGesto);
    }

    /// <summary>
    /// Esquece o gesto em curso sem emitir nada (LIBERAR_CAPTURA_DO_ITEM): o núcleo já o encerrou, ao esconder, sair ou
    /// recolher os itens no meio dele, e a janela solta a captura. Um soltar que chegue depois não vira ITEM_RELEASE.
    /// </summary>
    internal void Reiniciar()
    {
        _arbitro.Reiniciar();
        ItemEmGesto = null;
    }

    /// <summary>Um gesto do árbitro como evento do item <paramref name="id"/>; o CONTEXT_MENU fica como está.</summary>
    internal static Evento Traduzir(Evento gesto, int id) => gesto switch
    {
        Press p => new ItemPress(id, p.Cursor),
        DragStart => new ItemDragStart(id),
        DragMove m => new ItemDragMove(id, m.Cursor),
        DragEnd f => new ItemDragEnd(id, f.Cursor),
        Click or DoubleClick or DragCancel => new ItemRelease(id),
        ContextMenu => gesto,
        _ => throw new ArgumentException($"Gesto desconhecido para um item: {gesto}.", nameof(gesto)),
    };
}

/// <summary>
/// O temporizador da onda do tamagotchi (DEC-028; crítica, C2): um segundo DispatcherTimer, ao lado do da agenda
/// autônoma, só de disparo único. <see cref="Agendar"/> (efeito AGENDAR_ONDA) substitui o pendente; o disparo para o
/// temporizador antes de avisar, para nunca virar periódico (DEC-011), e entrega a geração agendada, que a raiz manda ao
/// núcleo como ITEM_EFFECT_TIMER. <see cref="Cancelar"/> (CANCELAR_ONDA) e <see cref="Parar"/> (encerramento) não deixam
/// disparar; depois de parado, nada mais é agendado. Só na thread da interface.
/// </summary>
internal sealed class TemporizadorDaOnda
{
    private readonly DispatcherTimer _temporizador = new(DispatcherPriority.Background);
    private readonly Action<long> _disparar;
    private bool _parado;

    /// <param name="disparar">Chamado uma vez por agendamento, com a geração dele.</param>
    internal TemporizadorDaOnda(Action<long> disparar)
    {
        ArgumentNullException.ThrowIfNull(disparar);
        _disparar = disparar;
        _temporizador.Tick += AoDisparar;
    }

    /// <summary>Se há um disparo agendado.</summary>
    internal bool Pendente => GeracaoPendente is not null;

    /// <summary>A geração do disparo agendado, ou nula.</summary>
    internal long? GeracaoPendente { get; private set; }

    /// <summary>Se o DispatcherTimer está ligado: só entre um agendamento e o disparo dele (nunca em repouso).</summary>
    internal bool Ligado => _temporizador.IsEnabled;

    /// <summary>Um disparo único depois de <paramref name="atraso"/> (no mínimo 1 ms), no lugar do que estiver pendente.</summary>
    internal void Agendar(TimeSpan atraso, long geracao)
    {
        if (_parado) return;
        _temporizador.Stop();
        _temporizador.Interval = atraso > TimeSpan.Zero ? atraso : TimeSpan.FromMilliseconds(1);
        GeracaoPendente = geracao;
        _temporizador.Start();
    }

    /// <summary>Cancela o disparo pendente; devolve se havia um.</summary>
    internal bool Cancelar()
    {
        bool havia = Pendente;
        _temporizador.Stop();
        GeracaoPendente = null;
        return havia;
    }

    /// <summary>Encerramento: cancela o pendente e ignora os agendamentos seguintes.</summary>
    internal void Parar()
    {
        _parado = true;
        Cancelar();
    }

    private void AoDisparar(object? remetente, EventArgs e)
    {
        // Disparo único: o DispatcherTimer continuaria disparando a cada intervalo.
        _temporizador.Stop();
        if (GeracaoPendente is not { } geracao) return;
        GeracaoPendente = null;
        _disparar(geracao);
    }
}
