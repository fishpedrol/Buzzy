using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Interface pra trocar a JanelaDoItem por uma falsa nos testes. Nada aqui ativa a janela.
internal interface IJanelaDoItem
{
    // 0 antes de criada.
    nint Hwnd { get; }

    // Com a captura do mouse de um gesto em curso sobre o item.
    bool Capturando { get; }

    // Já normalizado: px físicos, relógio monotônico em ms.
    event Action<EventoDePonteiro>? Ponteiro;

    void DefinirSprite(BitmapSource sprite);

    // Px físicos, sem ativar nem mudar a ordem Z.
    void AplicarRetangulo(RetanguloPx retangulo);

    // Nulo se o Windows não informar.
    RetanguloPx? RetanguloReal();

    void Mostrar();

    void Esconder();

    void ColocarAbaixoDe(nint hwnd);

    // Topo do grupo "sempre no topo".
    void TrazerParaFrente();

    void AplicarSempreNoTopo(bool ligado);

    void Capturar();

    // Não conta como captura perdida.
    void SoltarCaptura();

    void Fechar();
}

// Uma janela por item do tamagotchi, comandada pelos efeitos do núcleo. Id
// desconhecido é ignorado (só vai pro log com desconhecido=sim). Num lote, a raiz
// pula um MoverItem que tenha outro do mesmo Id mais adiante.
//
// Ordem Z só muda por evento, nunca por timer: o item fica logo abaixo do personagem;
// durante o gesto sobre ele vai pro topo, pra não sumir atrás do personagem bem na hora
// de ser solto em cima dele, e volta no fim. ReafirmarLugares mexe só no lugar.
// Só na thread da interface.
internal sealed class GerenteDosItens
{
    private sealed class Registro(IJanelaDoItem janela, Item item, Action<EventoDePonteiro> ouvinte)
    {
        internal IJanelaDoItem Janela { get; } = janela;

        internal Action<EventoDePonteiro> Ouvinte { get; } = ouvinte;

        internal Item Item { get; set; } = item;

        // 0 sem sprite.
        internal int Dpi { get; set; }

        internal bool Visivel { get; set; }

        // Último lugar aplicado; nulo antes do primeiro.
        internal Posicionamento? Lugar { get; set; }

        // Nulo enquanto o item não está parado no chão.
        internal RetanguloPx? UltimoPouso { get; set; }
    }

    private readonly Func<int, IJanelaDoItem> _criar;
    private readonly Func<nint> _hwndDoPersonagem;
    private readonly Func<int, bool> _emRepouso;
    private readonly SortedDictionary<int, Registro> _janelas = [];

    // criar devolve a janela ainda escondida. emRepouso nulo = nunca está parado.
    internal GerenteDosItens(Func<int, IJanelaDoItem> criar, Func<nint> hwndDoPersonagem, Func<int, bool>? emRepouso = null)
    {
        ArgumentNullException.ThrowIfNull(criar);
        ArgumentNullException.ThrowIfNull(hwndDoPersonagem);
        _criar = criar;
        _hwndDoPersonagem = hwndDoPersonagem;
        _emRepouso = emRepouso ?? (_ => false);
    }

    internal event Action<int, EventoDePonteiro>? Ponteiro;

    // Inclui as escondidas.
    internal int Quantas => _janelas.Count;

    internal int Visiveis => _janelas.Values.Count(r => r.Visivel);

    // Um MoverItem pode ser pulado se há outro do mesmo Id adiante, antes de um
    // mostrar/esconder/remover desse Id: o último leva ao mesmo lugar final.
    internal static bool MovimentoPosterior(IReadOnlyList<Efeito> lote, int i)
    {
        ArgumentNullException.ThrowIfNull(lote);
        if (lote[i] is not MoverItem mover) return false;
        for (int j = i + 1; j < lote.Count; j++)
        {
            switch (lote[j])
            {
                case MoverItem outro when outro.Id == mover.Id:
                    return true;
                case MostrarItem m when m.Id == mover.Id:
                case EsconderItem e when e.Id == mover.Id:
                case RemoverItem r when r.Id == mover.Id:
                    return false;
            }
        }
        return false;
    }

    // Em LiberarCapturaDoItem só cuida da janela; esquecer o gesto no árbitro é com a raiz.
    internal void Executar(Efeito efeito)
    {
        switch (efeito)
        {
            case MostrarItem m:
                Mostrar(m);
                break;
            case MoverItem m:
                Mover(m);
                break;
            case EsconderItem e:
                Esconder(e.Id);
                break;
            case RemoverItem r:
                Remover(r);
                break;
            case LiberarCapturaDoItem l:
                // O núcleo encerrou o gesto sozinho: solta o mouse (sem contar como captura
                // perdida) e volta pra baixo do personagem.
                if (!_janelas.ContainsKey(l.Id)) break;
                TerminarGesto(l.Id);
                Diagnostico.Evento("ITEM", ("capturaLiberada", l.Id));
                break;
            default:
                throw new ArgumentException($"Não é efeito de janela de item: {efeito}.", nameof(efeito));
        }
    }

    internal void ComecarGesto(int id)
    {
        if (!_janelas.TryGetValue(id, out Registro? r)) return;
        r.Janela.Capturar();
        r.Janela.TrazerParaFrente();
    }

    internal void TerminarGesto(int id)
    {
        if (!_janelas.TryGetValue(id, out Registro? r)) return;
        r.Janela.SoltarCaptura();
        if (r.Visivel) ColocarAbaixoDoPersonagem(r);
    }

    internal void AplicarSempreNoTopo(bool ligado)
    {
        SempreNoTopo = ligado;
        foreach (Registro r in _janelas.Values) r.Janela.AplicarSempreNoTopo(ligado);
    }

    // As janelas criadas depois herdam.
    internal bool SempreNoTopo { get; private set; } = true;

    // Depois de o personagem subir pro topo: as janelas voltam pra logo abaixo dele,
    // menos a do gesto em curso, que fica por cima.
    internal void ReordenarAbaixoDoPersonagem()
    {
        foreach (Registro r in _janelas.Values.Where(r => r.Visivel && !r.Janela.Capturando)) ColocarAbaixoDoPersonagem(r);
        foreach (Registro r in _janelas.Values.Where(r => r.Visivel && r.Janela.Capturando)) r.Janela.TrazerParaFrente();
    }

    // Depois de reler os monitores: janela fora do lugar do núcleo (o Windows devolveu
    // ao monitor reconectado ou moveu ao trocar o DPI) volta pro lugar. Não mexe na
    // ordem Z, porque a conferência tardia vem de um timer e ordem Z só muda por evento.
    // A janela do gesto em curso fica onde o cursor pôs.
    internal IReadOnlyList<int> ReafirmarLugares(string motivo)
    {
        List<int>? reaplicados = null;
        foreach ((int id, Registro r) in _janelas)
        {
            if (!r.Visivel || r.Janela.Capturando || r.Lugar is not { } lugar) continue;
            RetanguloPx? real = r.Janela.RetanguloReal();
            if (real == lugar.Retangulo) continue;
            r.Janela.AplicarRetangulo(lugar.Retangulo);
            (reaplicados ??= []).Add(id);
            Diagnostico.Evento("ITEM", ("reaplicado", id), ("motivo", motivo), ("real", real), ("nucleo", lugar.Retangulo));
        }
        return reaplicados ?? [];
    }

    // No fim de cada processamento: uma linha ITEM|movido=Id|parado=sim por pouso.
    // O pouso vem do núcleo, não do movimento da janela: se o último passo no ar já
    // arredonda pro chão, a janela não se move no pouso e a linha tem que sair igual.
    internal IReadOnlyList<int> RegistrarPousos()
    {
        List<int>? pousaram = null;
        foreach ((int id, Registro r) in _janelas)
        {
            if (!r.Visivel || r.Lugar is not { } lugar) continue;
            if (!_emRepouso(id))
            {
                r.UltimoPouso = null;
                continue;
            }
            if (r.UltimoPouso == lugar.Retangulo) continue;
            r.UltimoPouso = lugar.Retangulo;
            (pousaram ??= []).Add(id);
            if (!Diagnostico.Ligado) continue;
            Diagnostico.Evento("ITEM",
                ("movido", id),
                ("parado", "sim"),
                ("monitor", lugar.Monitor.Chave),
                ("retangulo", lugar.Retangulo),
                ("dpi", r.Dpi),
                ("pontoOpaco", PontosNaTela(r, lugar.Retangulo).Opaco));
        }
        return pousaram ?? [];
    }

    internal void FecharTodas()
    {
        if (_janelas.Count == 0) return;
        // Copia antes: fechar janela no WPF processa mensagens, e algo pode mexer na coleção no meio do loop.
        Registro[] todas = [.. _janelas.Values];
        _janelas.Clear();
        foreach (Registro r in todas) Fechar(r);
        int quantas = todas.Length;
        Diagnostico.Evento("ITEM", ("fechadas", quantas));
    }

    private void Mostrar(MostrarItem m)
    {
        bool criada = false;
        if (!_janelas.TryGetValue(m.Id, out Registro? r))
        {
            IJanelaDoItem janela = _criar(m.Id);
            // Herda o "sempre no topo" em vigor.
            if (!SempreNoTopo) janela.AplicarSempreNoTopo(false);
            int id = m.Id;
            Action<EventoDePonteiro> ouvinte = e => Ponteiro?.Invoke(id, e);
            janela.Ponteiro += ouvinte;
            r = new Registro(janela, m.Item, ouvinte);
            _janelas[m.Id] = r;
            criada = true;
        }
        DesenharSePreciso(r, m.Item, m.Lugar.Monitor.Dpi);
        r.Janela.AplicarRetangulo(m.Lugar.Retangulo);
        r.Lugar = m.Lugar;
        if (!r.Visivel)
        {
            r.Janela.Mostrar();
            r.Visivel = true;
            // O WPF pode reaplicar a posição inicial no Show, então aplica de novo.
            r.Janela.AplicarRetangulo(m.Lugar.Retangulo);
        }
        if (r.Janela.Capturando) r.Janela.TrazerParaFrente();
        else ColocarAbaixoDoPersonagem(r);

        if (!Diagnostico.Ligado) return;
        RetanguloPx ret = m.Lugar.Retangulo;
        (string opaco, string transparente) = PontosNaTela(r, ret);
        Diagnostico.Evento("ITEM",
            ("mostrado", m.Id),
            ("item", r.Item),
            ("criada", criada ? "sim" : "nao"),
            ("hwnd", r.Janela.Hwnd),
            ("monitor", m.Lugar.Monitor.Chave),
            ("retangulo", ret),
            ("dpi", r.Dpi),
            ("opaco", SpriteDoItem.LimitesOpacos(r.Item, r.Dpi).Deslocado(ret.Esquerda, ret.Topo)),
            ("pontoOpaco", opaco),
            ("pontoTransparente", transparente));
    }

    // Pontos pras verificações clicarem no item. Diagnóstico nunca derruba o app:
    // se der erro, vira "indisponível".
    private static (string Opaco, string Transparente) PontosNaTela(Registro r, RetanguloPx ret)
    {
        try
        {
            (PontoPx opaco, PontoPx transparente) = SpriteDoItem.PontosDeTeste(r.Item, r.Dpi);
            return ($"{ret.Esquerda + opaco.X},{ret.Topo + opaco.Y}", $"{ret.Esquerda + transparente.X},{ret.Topo + transparente.Y}");
        }
        catch (InvalidOperationException e)
        {
            Diagnostico.Evento("ERRO", ("etapa", "pontos de teste do item"), ("tipo", e.GetType().Name));
            return ("indisponível", "indisponível");
        }
    }

    private void Mover(MoverItem m)
    {
        if (!_janelas.TryGetValue(m.Id, out Registro? r))
        {
            Diagnostico.Evento("ITEM", ("movido", m.Id), ("desconhecido", "sim"));
            return;
        }
        DesenharSePreciso(r, r.Item, m.Lugar.Monitor.Dpi);
        r.Janela.AplicarRetangulo(m.Lugar.Retangulo);
        // Na queda e no arraste muda a cada quadro; o log só leva o lugar quando para (RegistrarPousos).
        r.Lugar = m.Lugar;
    }

    private void Esconder(int id)
    {
        if (!_janelas.TryGetValue(id, out Registro? r))
        {
            Diagnostico.Evento("ITEM", ("escondido", id), ("desconhecido", "sim"));
            return;
        }
        if (!r.Visivel) return;
        r.Janela.Esconder();
        r.Visivel = false;
        Diagnostico.Evento("ITEM", ("escondido", id));
    }

    private void Remover(RemoverItem remover)
    {
        if (!_janelas.Remove(remover.Id, out Registro? r))
        {
            Diagnostico.Evento("ITEM", ("removido", remover.Id), ("motivo", remover.Motivo), ("desconhecido", "sim"));
            return;
        }
        Fechar(r);
        Diagnostico.Evento("ITEM", ("removido", remover.Id), ("motivo", remover.Motivo));
    }

    // Desliga o ouvinte antes, pra nada da janela chegar à raiz durante o fechamento.
    private static void Fechar(Registro r)
    {
        r.Janela.Ponteiro -= r.Ouvinte;
        if (r.Janela.Capturando) r.Janela.SoltarCaptura();
        r.Janela.Fechar();
        r.Visivel = false;
    }

    private void DesenharSePreciso(Registro r, Item item, int dpi)
    {
        if (r.Dpi == dpi && r.Item == item) return;
        r.Janela.DefinirSprite(SpriteDoItem.Renderizar(item, dpi));
        r.Item = item;
        r.Dpi = dpi;
    }

    private void ColocarAbaixoDoPersonagem(Registro r)
    {
        nint personagem = _hwndDoPersonagem();
        if (personagem != 0) r.Janela.ColocarAbaixoDe(personagem);
    }
}
