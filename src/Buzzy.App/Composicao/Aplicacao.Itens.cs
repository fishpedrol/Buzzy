using System.Diagnostics;
using System.Windows.Threading;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Tamagotchi adulto: janelas dos itens, gestos sobre elas (árbitro próprio) e o timer
// da onda. Com o tamagotchi desligado no núcleo, nenhum desses efeitos chega e nenhuma
// janela de item é criada.
internal sealed partial class Aplicacao
{
    private readonly GestosDosItens _gestosDosItens = new();

    // Só com --diagnostico: ms de cada movimento do arraste até a janela no lugar.
    private readonly List<double> _latenciasDoArrasteDoItem = [];

    private GerenteDosItens? _itens;
    private TemporizadorDaOnda? _onda;

    // Chamar logo depois de criar a janela do personagem.
    private void IniciarItens()
    {
        _itens = new GerenteDosItens(CriarJanelaDoItem, () => _personagem?.Hwnd ?? 0, ItemParadoNoChao);
        _itens.Ponteiro += AoPonteiroDoItem;
        _onda = new TemporizadorDaOnda(AoDispararOnda);
    }

    private IJanelaDoItem CriarJanelaDoItem(int id)
    {
        var janela = new JanelaDoItem(id, _nucleo?.Configuracao.TamanhoDoItem ?? SpriteDoItem.TamanhoLogico);
        janela.CriarSemMostrar();
        return janela;
    }

    private bool ItemParadoNoChao(int id) => _nucleo?.Estado.Itens.PorId(id) is { Situacao: SituacaoDoItem.NoChao };

    // O núcleo processa cada evento dentro da mesma mensagem: no arraste, a janela já
    // está no lugar novo antes de a mensagem terminar.
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

    // Só com --diagnostico. "sobre" usa a mesma regra do núcleo (Maquina.SobreOPersonagem);
    // "usado" quando o personagem usou o item e a janela sumiu.
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

    private void ExecutarEfeitoDoTamagotchi(Efeito efeito)
    {
        // Gesto encerrado pelo núcleo: o resumo do arraste não sai.
        if (efeito is LiberarCapturaDoItem) _latenciasDoArrasteDoItem.Clear();
        LigacaoDosItens.Executar(efeito, _gestosDosItens, _itens, _onda);
    }

    private void AoDispararOnda(long geracao)
    {
        Diagnostico.Evento("ONDA", ("disparada", "sim"), ("geracao", geracao));
        Enviar(new ItemEffectTimer(geracao), $"onda geração {geracao}");
    }

    private void EncerrarItens()
    {
        _onda?.Parar();
        _gestosDosItens.Reiniciar();
        _latenciasDoArrasteDoItem.Clear();
        _itens?.FecharTodas();
    }
}

// Separado da Aplicacao pra testar sem ela e sem janela de verdade.
// Só na thread da interface.
internal static class LigacaoDosItens
{
    // A captura acompanha o gesto: a janela onde ele acabou (ou de onde passou pra
    // outro item) solta e volta pra baixo do personagem; a do botão pressionado
    // captura e vai pro topo.
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

    // itens e onda podem ser nulos antes de criados; aí a parte deles não faz nada.
    internal static void Executar(Efeito efeito, GestosDosItens gestos, GerenteDosItens? itens, TemporizadorDaOnda? onda)
    {
        ArgumentNullException.ThrowIfNull(gestos);
        switch (efeito)
        {
            case MostrarItem or MoverItem or EsconderItem:
                itens?.Executar(efeito);
                break;

            case RemoverItem remover:
                // Item removido no meio do próprio gesto: o núcleo já solta antes, isto é só defesa.
                if (gestos.ItemEmGesto == remover.Id) gestos.Reiniciar();
                itens?.Executar(remover);
                break;

            case LiberarCapturaDoItem liberar:
                // O núcleo encerrou o gesto (esconder, minimizar, bloquear, sair, recolher, pegar
                // outro item): esquece sem gerar ITEM_RELEASE, e um soltar que chegue depois não vira nada.
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

    // Linha PARANOIA do log (só com --diagnostico). Fica aqui, fora do núcleo, pra não
    // mudar a linha canônica nem as reproduções gravadas. Teve sorteio se o gerador da
    // paranoia andou (só ele usa, um passo por sorteio, uma vez por episódio). Nulo sem sorteio.
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

// Eventos vão pra fila do núcleo na ordem. ItemEmGesto: o do botão esquerdo, ou nulo.
internal readonly record struct GestoDoItem(IReadOnlyList<Evento> Eventos, bool Capturar, int? ItemEmGesto);

// Um segundo ArbitroDeGestos só pros itens, com as mesmas regras do personagem (limiar
// de arraste, clique duplo, ClickLock, captura perdida). Cada gesto vira evento do item
// onde o botão esquerdo desceu; Click, DoubleClick e DragCancel viram ITEM_RELEASE (o
// item cai de onde está). Botão direito abre o mesmo menu do personagem. Botão
// pressionado em outro item sem soltar o anterior larga o anterior antes de pegar o novo.
// Só na thread da interface.
internal sealed class GestosDosItens
{
    private readonly ArbitroDeGestos _arbitro = new();

    internal int? ItemEmGesto { get; private set; }

    internal GestoDoItem Receber(int id, EventoDePonteiro evento)
    {
        Arbitragem arbitragem = _arbitro.Receber(evento);
        var eventos = new List<Evento>(arbitragem.Gestos.Count);
        int? doGesto = ItemEmGesto;
        foreach (Evento gesto in arbitragem.Gestos)
        {
            // O gesto pertence ao item onde começou, mesmo que a mensagem venha por outra
            // janela. O cancelamento que vem antes de um Press é do gesto anterior.
            if (gesto is Press) doGesto = id;
            eventos.Add(Traduzir(gesto, doGesto ?? id));
        }
        ItemEmGesto = arbitragem.Capturar ? doGesto ?? id : null;
        return new GestoDoItem(eventos, arbitragem.Capturar, ItemEmGesto);
    }

    // Esquece o gesto sem emitir nada; um soltar que chegue depois não vira ITEM_RELEASE.
    internal void Reiniciar()
    {
        _arbitro.Reiniciar();
        ItemEmGesto = null;
    }

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

// Timer de disparo único da onda (também usado pela curiosidade). Agendar substitui o
// pendente; o disparo entrega a geração agendada. Depois de Parar, nada mais é agendado.
// Só na thread da interface.
internal sealed class TemporizadorDaOnda
{
    private readonly DispatcherTimer _temporizador = new(DispatcherPriority.Background);
    private readonly Action<long> _disparar;
    private bool _parado;

    internal TemporizadorDaOnda(Action<long> disparar)
    {
        ArgumentNullException.ThrowIfNull(disparar);
        _disparar = disparar;
        _temporizador.Tick += AoDisparar;
    }

    internal bool Pendente => GeracaoPendente is not null;

    internal long? GeracaoPendente { get; private set; }

    // Só ligado entre o agendamento e o disparo; parado, nunca.
    internal bool Ligado => _temporizador.IsEnabled;

    // Mínimo de 1 ms.
    internal void Agendar(TimeSpan atraso, long geracao)
    {
        if (_parado) return;
        _temporizador.Stop();
        _temporizador.Interval = atraso > TimeSpan.Zero ? atraso : TimeSpan.FromMilliseconds(1);
        GeracaoPendente = geracao;
        _temporizador.Start();
    }

    // Devolve se havia um pendente.
    internal bool Cancelar()
    {
        bool havia = Pendente;
        _temporizador.Stop();
        GeracaoPendente = null;
        return havia;
    }

    internal void Parar()
    {
        _parado = true;
        Cancelar();
    }

    private void AoDisparar(object? remetente, EventArgs e)
    {
        // Sem o Stop, o DispatcherTimer continua disparando a cada intervalo.
        _temporizador.Stop();
        if (GeracaoPendente is not { } geracao) return;
        GeracaoPendente = null;
        _disparar(geracao);
    }
}
