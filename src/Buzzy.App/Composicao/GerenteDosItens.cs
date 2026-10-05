using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// Uma janela de item, como o gerente a vê: a de verdade é <see cref="JanelaDoItem"/>; os testes usam uma falsa. Todas as
/// operações agem só na janela do próprio Buzzy, sem ativá-la.
/// </summary>
internal interface IJanelaDoItem
{
    /// <summary>O HWND da janela (0 antes de criada).</summary>
    nint Hwnd { get; }

    /// <summary>Se a janela está com a captura do mouse de um gesto em curso sobre o item.</summary>
    bool Capturando { get; }

    /// <summary>Evento de ponteiro já normalizado (pixels físicos, relógio monotônico em ms).</summary>
    event Action<EventoDePonteiro>? Ponteiro;

    void DefinirSprite(BitmapSource sprite);

    /// <summary>Posiciona e dimensiona em pixels físicos, sem ativar nem mudar a ordem Z.</summary>
    void AplicarRetangulo(RetanguloPx retangulo);

    /// <summary>Onde a janela está de fato, em pixels físicos; nulo se o Windows não informar.</summary>
    RetanguloPx? RetanguloReal();

    /// <summary>Mostra sem ativar.</summary>
    void Mostrar();

    void Esconder();

    /// <summary>Põe a janela logo abaixo de <paramref name="hwnd"/> na ordem Z, sem ativar.</summary>
    void ColocarAbaixoDe(nint hwnd);

    /// <summary>Põe a janela no topo do grupo "sempre no topo", sem ativar.</summary>
    void TrazerParaFrente();

    /// <summary>Liga ou desliga o "sempre no topo" (DEC-038, item 8).</summary>
    void AplicarSempreNoTopo(bool ligado);

    void Capturar();

    /// <summary>Solta a captura do gesto sem que isso conte como captura perdida.</summary>
    void SoltarCaptura();

    /// <summary>Fecha de vez.</summary>
    void Fechar();
}

/// <summary>
/// As janelas dos itens do tamagotchi (DEC-028; crítica, C19 e L17), uma por item, pelas ordens do núcleo:
/// <see cref="MostrarItem"/> cria a janela (com o sprite do item no DPI do monitor) ou mostra de novo a escondida;
/// <see cref="MoverItem"/> leva ao lugar, com o sprite redesenhado quando o DPI muda; <see cref="EsconderItem"/> esconde;
/// <see cref="RemoverItem"/> fecha; <see cref="LiberarCapturaDoItem"/> solta o mouse do gesto sobre o item. O que chega
/// para um Id que o gerente não conhece é ignorado. Num lote, a raiz pula um MOVER_ITEM que tenha outro do mesmo Id
/// adiante (<see cref="MovimentoPosterior"/>); nenhum outro efeito é pulado.
///
/// Ordem Z, sempre por evento, nunca por timer (SECURITY.md 2): o item fica logo abaixo do personagem; no gesto sobre ele,
/// vai para o topo (<see cref="ComecarGesto"/>), para não sumir atrás do personagem justamente quando vai ser solto sobre
/// ele, e volta para baixo no fim (<see cref="TerminarGesto"/>). Quando o personagem reaparece no topo,
/// <see cref="ReordenarAbaixoDoPersonagem"/> reafirma a ordem. Depois de uma releitura da topologia e na conferência tardia
/// dela, <see cref="ReafirmarLugares"/> reafirma só o lugar, sem mexer na ordem Z (Fase 5, passo P9; revisão do bloco
/// P6-P9). No encerramento, <see cref="FecharTodas"/>.
///
/// Diagnóstico (só com <c>--diagnostico</c>, sem dado pessoal): linhas ITEM de mostrado (com os pontos de teste), movido
/// (uma vez por pouso no chão, por <see cref="RegistrarPousos"/>), escondido, removido (com o motivo), reaplicado (com o
/// motivo, depois de uma releitura da topologia), captura liberada e janelas fechadas no encerramento; um Id desconhecido
/// leva <c>desconhecido=sim</c>. Só na thread da interface.
/// </summary>
internal sealed class GerenteDosItens
{
    private sealed class Registro(IJanelaDoItem janela, Item item, Action<EventoDePonteiro> ouvinte)
    {
        internal IJanelaDoItem Janela { get; } = janela;

        internal Action<EventoDePonteiro> Ouvinte { get; } = ouvinte;

        internal Item Item { get; set; } = item;

        /// <summary>O DPI do sprite na janela; 0 sem sprite.</summary>
        internal int Dpi { get; set; }

        internal bool Visivel { get; set; }

        /// <summary>O último lugar aplicado à janela (mostrar ou mover); nulo antes do primeiro.</summary>
        internal Posicionamento? Lugar { get; set; }

        /// <summary>O retângulo do último pouso registrado; nulo enquanto o item não está parado no chão.</summary>
        internal RetanguloPx? UltimoPouso { get; set; }
    }

    private readonly Func<int, IJanelaDoItem> _criar;
    private readonly Func<nint> _hwndDoPersonagem;
    private readonly Func<int, bool> _emRepouso;
    private readonly SortedDictionary<int, Registro> _janelas = [];

    /// <param name="criar">Cria a janela do item de um Id, ainda escondida.</param>
    /// <param name="hwndDoPersonagem">O HWND da janela do personagem (0 sem ela): o item fica logo abaixo dela.</param>
    /// <param name="emRepouso">Se o item do Id está parado no chão, para <see cref="RegistrarPousos"/> (nulo: nunca está).</param>
    internal GerenteDosItens(Func<int, IJanelaDoItem> criar, Func<nint> hwndDoPersonagem, Func<int, bool>? emRepouso = null)
    {
        ArgumentNullException.ThrowIfNull(criar);
        ArgumentNullException.ThrowIfNull(hwndDoPersonagem);
        _criar = criar;
        _hwndDoPersonagem = hwndDoPersonagem;
        _emRepouso = emRepouso ?? (_ => false);
    }

    /// <summary>Um evento de ponteiro de uma janela de item, com o Id dela.</summary>
    internal event Action<int, EventoDePonteiro>? Ponteiro;

    /// <summary>Quantas janelas de item existem (à vista ou escondidas).</summary>
    internal int Quantas => _janelas.Count;

    /// <summary>Quantas estão à vista.</summary>
    internal int Visiveis => _janelas.Values.Count(r => r.Visivel);

    /// <summary>
    /// Se o efeito <paramref name="i"/> do lote é um <see cref="MoverItem"/> com outro do mesmo Id adiante, antes de um
    /// mostrar, esconder ou remover desse Id (crítica, C19): pode ser pulado, porque o último leva a janela ao mesmo
    /// lugar final. Efeitos de outros itens e da janela do personagem não contam; nenhum outro efeito é pulado.
    /// </summary>
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

    /// <summary>
    /// Executa um efeito das janelas dos itens: mostrar, mover, esconder, remover ou soltar a captura do gesto sobre o item
    /// (LIBERAR_CAPTURA_DO_ITEM: a parte da janela; esquecer o gesto no árbitro é da raiz).
    /// </summary>
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
                // O núcleo encerrou o gesto sobre o item por conta própria: a janela solta o mouse, sem que isso conte como
                // captura perdida, e volta para baixo do personagem.
                if (!_janelas.ContainsKey(l.Id)) break;
                TerminarGesto(l.Id);
                Diagnostico.Evento("ITEM", ("capturaLiberada", l.Id));
                break;
            default:
                throw new ArgumentException($"Não é efeito de janela de item: {efeito}.", nameof(efeito));
        }
    }

    /// <summary>Começo de um gesto sobre o item: a janela captura o mouse e vai para o topo (L17).</summary>
    internal void ComecarGesto(int id)
    {
        if (!_janelas.TryGetValue(id, out Registro? r)) return;
        r.Janela.Capturar();
        r.Janela.TrazerParaFrente();
    }

    /// <summary>Fim do gesto sobre o item: a janela solta o mouse, sem que isso conte como captura perdida, e volta para baixo do personagem.</summary>
    internal void TerminarGesto(int id)
    {
        if (!_janelas.TryGetValue(id, out Registro? r)) return;
        r.Janela.SoltarCaptura();
        if (r.Visivel) ColocarAbaixoDoPersonagem(r);
    }

    /// <summary>
    /// O personagem acabou de ir para o topo (mostrar): as janelas à vista voltam para logo abaixo dele; a do gesto em
    /// curso, se houver, fica por cima dele.
    /// </summary>
    /// <summary>O "sempre no topo" das janelas dos itens, junto com o do personagem (DEC-038, item 8); vale também para as próximas.</summary>
    internal void AplicarSempreNoTopo(bool ligado)
    {
        SempreNoTopo = ligado;
        foreach (Registro r in _janelas.Values) r.Janela.AplicarSempreNoTopo(ligado);
    }

    /// <summary>Se as janelas dos itens ficam sempre no topo; as criadas depois herdam.</summary>
    internal bool SempreNoTopo { get; private set; } = true;

    internal void ReordenarAbaixoDoPersonagem()
    {
        foreach (Registro r in _janelas.Values.Where(r => r.Visivel && !r.Janela.Capturando)) ColocarAbaixoDoPersonagem(r);
        foreach (Registro r in _janelas.Values.Where(r => r.Visivel && r.Janela.Capturando)) r.Janela.TrazerParaFrente();
    }

    /// <summary>
    /// Depois de uma releitura da topologia publicada e na conferência tardia dela (Fase 5, passo P9; D14 do desenho dos
    /// monitores): cada janela à vista, fora de um gesto, que não está onde o núcleo a pôs (o Windows a levou de volta a um
    /// monitor reconectado, ou a moveu ao trocar o DPI) volta ao último lugar aplicado, que é o do núcleo, sem mudar a ordem Z
    /// (<see cref="IJanelaDoItem.AplicarRetangulo"/>). A ordem Z não é reafirmada aqui: ela só muda quando o item aparece, no
    /// gesto sobre ele e quando o personagem reaparece (DEC-028, item 22; SECURITY.md 2), e a conferência tardia é um
    /// temporizador (revisão do bloco P6-P9). A janela do gesto em curso fica onde o cursor a pôs. Uma linha
    /// <c>ITEM|reaplicado=Id</c> por janela reaplicada, com o motivo, o retângulo em que ela estava e o do núcleo (só com
    /// <c>--diagnostico</c>). Devolve os Ids reaplicados, em ordem.
    /// </summary>
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

    /// <summary>
    /// No fim de cada processamento do núcleo: cada janela à vista cujo item acabou de parar no chão, ou foi levado a outro
    /// lugar sem sair dele, ganha uma linha <c>ITEM|movido=Id|parado=sim</c> (só com <c>--diagnostico</c>), com o lugar em que
    /// a janela está. O pouso é o fato do núcleo, e não um movimento da janela: quando o último passo no ar já arredonda
    /// para o chão, o passo do pouso não move a janela, e a linha sai mesmo assim (revisão de correção, achado 5). Uma vez
    /// por pouso; janelas escondidas ficam para quando forem mostradas. Devolve os Ids registrados agora, em ordem.
    /// </summary>
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

    /// <summary>Encerramento: fecha todas as janelas, cada uma uma vez, soltando antes a captura de um gesto em curso.</summary>
    internal void FecharTodas()
    {
        if (_janelas.Count == 0) return;
        // Uma cópia: fechar uma janela do WPF processa mensagens, e nada pode mexer na coleção no meio da volta.
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
            // A janela nova herda o "sempre no topo" em vigor (DEC-038, item 8).
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
            // O WPF pode reaplicar a posição inicial ao mostrar a janela: o lugar é confirmado depois, como no personagem.
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

    /// <summary>
    /// Os pontos de teste do item na tela, para as verificações clicarem nele. O diagnóstico nunca derruba o aplicativo:
    /// um ponto que não confere vira "indisponível", com o tipo do erro.
    /// </summary>
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
        // Na queda e no arraste, o item se move a cada quadro: o lugar só vai para o log quando ele para, como a POSICAO do
        // personagem, pelo RegistrarPousos no fim do processamento.
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

    /// <summary>Fecha a janela sem que nada dela chegue à raiz: deixa de ouvi-la e solta a captura antes.</summary>
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
