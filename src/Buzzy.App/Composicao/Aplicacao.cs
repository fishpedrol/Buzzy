using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>Opções da linha de comando que a raiz de composição usa.</summary>
/// <param name="MovimentoPausado">
/// <c>--pausado</c>: começa com o movimento autônomo pausado, como o item "Pausar movimento" do
/// menu. Usado pelas verificações de tela, que precisam do personagem parado no lugar inicial.
/// </param>
/// <param name="Semente">
/// <c>--semente N</c>: semente fixa da agenda autônoma, para reproduzir uma sequência de
/// comportamento (diagnóstico e testes). Sem ela, a semente vem do relógio do sistema.
/// </param>
/// <param name="PerfilDeTeste">
/// <c>--perfil-de-teste NOME</c>, validado na leitura da linha de comando (<see cref="PastaDeDados.NomeDePerfilValido"/>)
/// e de novo na escolha da pasta (<see cref="PastaDeDados.DasConfiguracoes(string?, bool)"/>): os dados do Buzzy
/// ficam em <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c>, para os testes e as ferramentas que o abrem nunca lerem
/// nem gravarem as configurações reais do usuário. Nulo sem a opção. O log de diagnóstico continua na raiz da
/// pasta do Buzzy.
/// </param>
/// <param name="PersistenciaDesligada">
/// <c>--perfil-de-teste</c> veio sem nome, com um nome inválido ou escrito de outro jeito: nesta execução nada
/// é lido nem gravado como configuração (falha fechada), em vez de cair na pasta real do usuário.
/// </param>
/// <param name="SemTelaCheia">
/// <c>--sem-tela-cheia</c>: o observador da janela em primeiro plano não liga (DEC-034). Os testes e a verificação de
/// tela, que conferem o lugar inicial, usam para não depender do que estiver em tela cheia na máquina. A chave do menu
/// continua valendo; só nenhum monitor fica ocupado.
/// </param>
internal sealed record OpcoesDaAplicacao(bool MovimentoPausado, ulong? Semente, string? PerfilDeTeste = null, bool PersistenciaDesligada = false, bool SemTelaCheia = false)
{
    internal static readonly OpcoesDaAplicacao Padrao = new(false, null);
}

/// <summary>
/// Raiz de composição das Fases 1 a 4 (DEC-007): liga o núcleo puro do personagem, do
/// posicionamento e da arbitragem de input ao adaptador de plataforma (janelas, captura do
/// mouse, bandeja, menu e mensagens do Windows) e escolhe o quadro do sprite pelo retrato.
///
/// Ocioso por eventos (DEC-011): em repouso, nada roda em timer periódico. Os timers são de uma
/// vez só — a releitura da topologia depois das mensagens do Windows (300 ms depois da última, com
/// teto de 1 s desde a primeira e até três novas tentativas se a leitura vier incoerente) e a
/// conferência tardia do lugar das janelas, 1,5 s depois de cada releitura publicada (Fase 5,
/// passo P9; <see cref="AgendaDaReleitura"/>), novas tentativas de pôr o ícone na
/// bandeja, a próxima decisão da agenda autônoma, o fim de cada fase da onda de um item do
/// tamagotchi (DEC-028), a gravação do settings.json com atraso, com as novas tentativas dela
/// (Fase 5, passo P7; <see cref="AgendaDeGravacao"/>), e a avaliação da tela cheia depois de uma troca de primeiro plano
/// (DEC-034; <see cref="AgendaDaTelaCheia"/>). O relógio de passo fixo só corre enquanto o
/// núcleo pede (reação, pouso, gesto curto e movimento) e anda junto com os quadros do
/// compositor do WPF (<see cref="CompositionTarget.Rendering"/>): a janela se move no máximo uma
/// vez por quadro, com a posição mais recente. O arraste não usa relógio: cada movimento do
/// mouse vira posição da janela no mesmo tratamento da mensagem.
///
/// As janelas dos itens, o árbitro dos gestos sobre elas e o temporizador da onda ficam em
/// Aplicacao.Itens.cs; aqui ficam só os ganchos.
/// </summary>
internal sealed partial class Aplicacao
{
    /// <summary>O motivo da conferência tardia nas linhas <c>POSICAO|reaplicada</c> e <c>ITEM|reaplicado</c>.</summary>
    private const string MotivoDaReafirmacaoTardia = "reafirmação tardia";

    private const int TentativasDaBandeja = 3;

    /// <summary>Maior atraso que o relógio de passo fixo recupera de uma vez (15 passos a 60 Hz).</summary>
    private static readonly TimeSpan AtrasoMaximoDoRelogio = TimeSpan.FromMilliseconds(250);

    private readonly Application _app;
    private readonly InstanciaUnica _instancia;
    private readonly OpcoesDaAplicacao _opcoes;

    /// <summary>Ordena eventos de sessão com a releitura da topologia (Fase 5, passo P10).</summary>
    private readonly ArbitroDeEventosDoSistema _eventosDoSistema;

    /// <summary>
    /// A releitura da topologia depois das mensagens do Windows (Fase 5, passo P9; crítica, C11): o agrupamento com teto, as
    /// novas tentativas e a conferência tardia, em disparos únicos na prioridade normal do Dispatcher.
    /// </summary>
    private readonly AgendaDaReleitura _releitura;

    /// <summary>A origem do relógio monotônico da <see cref="_releitura"/>.</summary>
    private readonly long _origemDoRelogio = Stopwatch.GetTimestamp();

    private readonly DispatcherTimer _repetirBandeja;
    private readonly Dictionary<Evento, string> _motivosDoNucleo = new(ReferenceEqualityComparer.Instance);
    private readonly ArbitroDeGestos _arbitro = new();

    /// <summary>M5 do arraste em curso (DEC-011), só com <c>--diagnostico</c>: ms de cada movimento até a janela no lugar.</summary>
    private readonly List<double> _latenciasDoArraste = [];

    private JanelaDeServico? _servico;
    private JanelaPersonagem? _personagem;

    /// <summary>
    /// O modo de tela cheia (DEC-013 e DEC-034): o observador da janela em primeiro plano e a agenda que avalia o que ele lê.
    /// Nulos se a assinatura falhar: aí o modo não age.
    /// </summary>
    private ObservadorDeTelaCheia? _observadorDeTelaCheia;
    private AgendaDaTelaCheia? _telaCheia;
    private Bandeja? _bandeja;
    private Nucleo? _nucleo;
    private Topologia _topologia = null!;

    /// <summary>
    /// A gravação do settings.json desta execução (Fase 5, passo P7; DEC-029): a leitura da partida, o atraso, a gravação
    /// na hora e a do encerramento. Criada no começo de <see cref="Iniciar"/>, antes de qualquer efeito do núcleo.
    /// </summary>
    private AgendaDeGravacao? _gravacao;

    /// <summary>Se o erro não tratado já descarregou a gravação (uma vez só, sem reentrar).</summary>
    private bool _descarregouNoErro;

    /// <summary>
    /// A última leitura coerente da topologia, a mesma de <see cref="_topologia"/>, atualizada junto com ela em todo caminho
    /// que a entrega ao núcleo (a partida, a releitura agrupada e o mostrar): guarda o nome GDI de cada chave estável, que só
    /// vai para o log (DEC-030). A leitura da barra recriada só escolhe o tamanho do ícone (passo P9).
    /// </summary>
    private LeituraDaTopologia? _leitura;

    private Posicionamento _posicionamento = null!;
    private DispatcherTimer? _decisaoAutonoma;
    private EventHandler? _aoDispararDecisao;
    private TimeSpan _tempoAcumulado;
    private long _ultimaMarcacaoRelogio;
    private long _passosDoRelogio;
    private int _dpiDoSprite;
    private int _dpiDoIcone;
    private int _tentativasDaBandeja;
    private bool _visivel;
    private bool _primeiroQuadroRegistrado;
    private bool _encerrando;
    private bool _processandoNucleo;

    /// <summary>A janela foi movida durante o arraste sem registrar a posição (diagnóstico).</summary>
    private bool _posicaoSemRegistro;

    /// <summary>Se o relógio de passo fixo está ligado (inscrito nos quadros do compositor).</summary>
    private bool _relogioLigado;

    /// <summary>Quadro do sprite na janela e o estado em que ele começou a ser contado.</summary>
    private QuadroDoSprite? _quadroAtual;
    private Estado _estadoDoQuadro = Estado.Booting;
    private int _quiquesDoQuadro;
    private long _passoDeEntradaNoEstado;

    internal Aplicacao(Application app, InstanciaUnica instancia, OpcoesDaAplicacao? opcoes = null)
    {
        _app = app;
        _instancia = instancia;
        _opcoes = opcoes ?? OpcoesDaAplicacao.Padrao;
        _eventosDoSistema = new(evento => Enviar(evento, $"evento do sistema {evento.GetType().Name}"));
        _releitura = new AgendaDaReleitura(
            () => Stopwatch.GetElapsedTime(_origemDoRelogio),
            (espera, acao) => DisparoUnico.NoDispatcher(espera, acao, DispatcherPriority.Normal),
            RelerAgrupada,
            ReafirmarDepoisDaReleitura);
        _repetirBandeja = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(2) };
        _repetirBandeja.Tick += (_, _) =>
        {
            _repetirBandeja.Stop();
            AdicionarIconeNaBandeja();
        };
    }

    internal void Iniciar()
    {
        // Limitação aceita do System.Windows.Application: o WPF encerra na pergunta de fim de
        // sessão (WM_QUERYENDSESSION) se ninguém cancelar; um desligamento cancelado depois por
        // outro aplicativo deixa o Buzzy fechado (DECISIONS.md).
        _app.SessionEnding += (_, _) => Enviar(new SessionEnding(), "fim de sessão");
        _app.DispatcherUnhandledException += (_, e) =>
        {
            // Só o tipo e o código: a mensagem de uma exceção do sistema pode trazer um caminho, com o nome do usuário (SECURITY.md 6).
            Diagnostico.Evento("ERRO", ("tipo", e.Exception.GetType().Name), ("hresult", $"0x{e.Exception.HResult:X8}"));
            DescarregarNoErro();
            _bandeja?.Remover();
        };

        // 1. As configurações (crítica, C18): o arquivo desta execução, escolhido só pela regra da pasta (perfil de teste,
        // persistência desligada ou a pasta do Buzzy), uma instância por execução, lido uma vez e sem derrubar a partida.
        _gravacao = AgendaDeGravacao.NaPartida(
            ArquivoDeConfiguracoes.DaExecucao(_opcoes.PerfilDeTeste, _opcoes.PersistenciaDesligada),
            _opcoes.PersistenciaDesligada,
            perfil: _opcoes.PerfilDeTeste is not null,
            GestoDoUsuarioEmCurso,
            AgendaDeGravacao.AgendarNoDispatcher);

        LeituraDaTopologia? leitura = LerTopologiaNaPartida();
        if (leitura is null)
        {
            _app.Shutdown(CodigosDeSaida.TopologiaIlegivel);
            return;
        }
        Topologia topologia = leitura.Topologia;
        _leitura = leitura;
        _topologia = topologia;
        Diagnostico.Evento("TOPOLOGIA", [("motivo", "início"), ("impressao", topologia.ImpressaoDigital), .. CamposDasChaves(leitura)]);

        _servico = new JanelaDeServico();
        _servico.BandejaAcionada += AoAcionarBandeja;
        _servico.BarraDeTarefasRecriada += AoRecriarBarra;
        _servico.TopologiaPodeTerMudado += motivo => AoPossivelMudancaDeTopologia(motivo);
        _servico.EventoDoSistema += AoEventoDoSistema;
        Diagnostico.Evento("SERVICO", ("hwnd", _servico.Hwnd));

        // O tamanho do personagem é o do passo lido do arquivo, fixo nesta execução (DEC-038, item 9).
        _escalaEmVigor = _gravacao.Lidas.Preferencias.Escala;
        TamanhoDip tamanhoDoPersonagem = ConfiguracaoDoNucleo.TamanhoDoPersonagem(_escalaEmVigor);
        Diagnostico.Evento("ESCALA", ("passo", _escalaEmVigor), ("dip", tamanhoDoPersonagem.Largura));
        _personagem = new JanelaPersonagem(tamanhoDoPersonagem);
        // A porta do início com o Windows (DEC-038, item 12): o registro só sem perfil de teste; a partida nunca o lê.
        _inicio = InicioComOWindows.DaExecucao(_opcoes.PerfilDeTeste, _opcoes.PersistenciaDesligada,
            PastaDeDados.DasConfiguracoes(_opcoes.PerfilDeTeste, _opcoes.PersistenciaDesligada), Environment.ProcessPath);
        _personagem.Ponteiro += AoPonteiro;
        _personagem.DpiMudou += dpi => AoPossivelMudancaDeTopologia($"WM_DPICHANGED {dpi}", daPropriaJanela: true);
        _personagem.Minimizada += () => Adiar(AoMinimizarPersonagem);
        _personagem.Closing += (_, e) =>
        {
            if (_encerrando) return;
            e.Cancel = true;
            Adiar(() => Enviar(new CmdExit(), "fechamento da janela"));
        };
        _personagem.ContentRendered += AoPrimeiroQuadro;

        // Cria a janela sem mostrá-la. Loaded escolhe o primeiro posicionamento e os efeitos
        // do núcleo a colocam em pixels físicos antes de torná-la visível.
        new WindowInteropHelper(_personagem).EnsureHandle();
        Diagnostico.Evento("JANELA", ("hwnd", _personagem.Hwnd));
        IniciarItens();
        // O "sempre no topo" lido do arquivo, antes de mostrar (DEC-038, item 8).
        AplicarSempreNoTopo(_gravacao.Lidas.Preferencias.SempreNoTopo, "partida");

        ulong semente = _opcoes.Semente ?? unchecked((ulong)Environment.TickCount64);
        _nucleo = new Nucleo(ConfiguracaoDoNucleo.DoAplicativo(_escalaEmVigor, EdicaoDoBuild.Atual), semente);
        Diagnostico.Evento("NUCLEO", ("semente", semente), ("pausado", _opcoes.MovimentoPausado ? "sim" : "nao"));
        // A carga leva o que a partida leu (DEC-029 e DEC-030): a posição salva, com a tela do monitor da época, que o
        // núcleo restaura pela cascata; a borda do esconderijo e a marca de preso; e as preferências, com a emoção
        // dominante (DEC-027) e a travessia. Sem arquivo, as padrão.
        Enviar(_gravacao.Lidas.ParaACarga(topologia), "início");
        if (_opcoes.MovimentoPausado) Enviar(new CmdPauseAutonomy(), "linha de comando --pausado");

        _dpiDoIcone = topologia.Principal.Dpi;
        _bandeja = new Bandeja(_servico.Hwnd, CriarIcone(_dpiDoIcone));
        AdicionarIconeNaBandeja();
        IniciarTelaCheia();

        _instancia.EscutarPedidos(() => Adiar(() =>
        {
            Diagnostico.Evento("INSTANCIA", ("papel", "primeira"), ("pedido", "mostrar"));
            MostrarPorComando("segunda instância");
        }));
    }

    // ------------------------------------------------------------------ eventos

    /// <summary>
    /// Ponteiro sobre o personagem ou capturado num gesto começado nele (ARCHITECTURE.md 2.7): a
    /// arbitragem decide o gesto e a captura, e o núcleo aplica cada gesto no mesmo tratamento da
    /// mensagem — um movimento de arraste vira posição da janela antes de a mensagem acabar.
    /// </summary>
    private void AoPonteiro(EventoDePonteiro evento)
    {
        if (_encerrando || _nucleo is null || _personagem is null) return;
        long recebido = Stopwatch.GetTimestamp();
        Arbitragem arbitragem = _arbitro.Receber(evento);

        // A captura acompanha o gesto: pega no botão esquerdo pressionado e solta no fim dele.
        if (arbitragem.Capturar) _personagem.Capturar();
        else _personagem.SoltarCaptura();

        bool moveu = false;
        foreach (Evento gesto in arbitragem.Gestos)
        {
            if (gesto is DragStart) _latenciasDoArraste.Clear();
            Enviar(gesto, "ponteiro");
            moveu |= gesto is DragMove;
            if (gesto is DragEnd or DragCancel) RegistrarFimDoArraste(gesto);
        }

        if (moveu && Diagnostico.Ligado)
            _latenciasDoArraste.Add(Stopwatch.GetElapsedTime(recebido).TotalMilliseconds);
    }

    /// <summary>
    /// Só com <c>--diagnostico</c>: resumo do arraste e M5 (DEC-011), sem registrar cada movimento.
    /// A posição validada ao soltar já foi registrada no fim do processamento do gesto.
    /// </summary>
    private void RegistrarFimDoArraste(Evento fim)
    {
        if (!Diagnostico.Ligado) return;
        double[] ms = [.. _latenciasDoArraste.Order()];
        _latenciasDoArraste.Clear();
        if (ms.Length == 0)
        {
            Diagnostico.Evento("ARRASTE", ("fim", fim.GetType().Name), ("movimentos", 0));
            return;
        }
        double p95 = ms[Math.Min(ms.Length - 1, (int)Math.Ceiling(ms.Length * 0.95) - 1)];
        Diagnostico.Evento("ARRASTE",
            ("fim", fim.GetType().Name),
            ("movimentos", ms.Length),
            ("m5MediaMs", Math.Round(ms.Average(), 3)),
            ("m5P95Ms", Math.Round(p95, 3)),
            ("m5MaxMs", Math.Round(ms[^1], 3)));
    }

    private void AoPrimeiroQuadro(object? remetente, EventArgs e)
    {
        if (_primeiroQuadroRegistrado) return;
        _primeiroQuadroRegistrado = true;
        using Process atual = Process.GetCurrentProcess();
        double ms = (DateTime.Now - atual.StartTime).TotalMilliseconds;
        Diagnostico.Evento("PRIMEIRO_QUADRO", ("ms", Math.Round(ms)));
    }

    private void AoAcionarBandeja(AcaoNaBandeja acao, PontoPx? ancora)
    {
        PontoPx ponto = ancora ?? PontoDoIcone();
        Diagnostico.Evento("BANDEJA", ("acao", acao), ("ancora", ponto));
        if (acao == AcaoNaBandeja.Selecionar)
            Adiar(() => MostrarPorComando("bandeja"));
        else
            Adiar(() => ExibirMenuDoDesktop(ponto, "bandeja", peloTeclado: acao == AcaoNaBandeja.MenuPeloTeclado));
    }

    private void AoRecriarBarra()
    {
        // A barra de tarefas reinicia (o Explorer recomeçou) ou o DPI do monitor principal muda (a Shell difunde a mesma
        // mensagem): o ícone volta na hora, no tamanho do DPI do principal lido agora. A topologia, que pode ter mudado com a
        // barra (a área útil, o DPI), vai ao núcleo pela releitura agrupada, como as outras mensagens (Fase 5, passo P9;
        // crítica, C12): esta leitura só escolhe o tamanho do ícone, e a topologia da raiz só muda junto com a do núcleo.
        Diagnostico.Evento("BANDEJA", ("barraDeTarefasRecriada", "sim"));
        if (_bandeja is not null)
        {
            if (LeitorDeTopologia.Ler(out _) is { } atual) _dpiDoIcone = atual.Principal.Dpi;
            _bandeja.TrocarIcone(CriarIcone(_dpiDoIcone), aplicar: false);
            _tentativasDaBandeja = 0;
            if (!_bandeja.Recriar()) AgendarNovaTentativaDaBandeja();
            _servico!.NotificacoesVersao4 = _bandeja.Versao4;
        }
        AoPossivelMudancaDeTopologia("TaskbarCreated");
    }

    /// <summary>
    /// Uma mensagem do Windows que pode ter mudado a topologia (WM_DISPLAYCHANGE, WM_SETTINGCHANGE com SPI_SETWORKAREA, o
    /// WM_DPICHANGED da janela do personagem ou a TaskbarCreated): vai crua ao log, só com o tipo (<c>MENSAGEM|tipo=</c>,
    /// para calibrar o agrupamento no protótipo P5), e pede a releitura agrupada (crítica, C11). No passo P10, o árbitro do
    /// sistema recebe o sinal entre as duas coisas. O WM_DPICHANGED da própria janela (<paramref name="daPropriaJanela"/>,
    /// passo P14) passado do limite de rodadas seguidas da agenda é ignorado, sem avisar o árbitro (<c>ignorada=sim</c>).
    /// </summary>
    private void AoPossivelMudancaDeTopologia(string motivo, bool daPropriaJanela = false)
    {
        if (_encerrando) return;
        if (daPropriaJanela && _releitura.IgnoraAPropriaJanela)
        {
            Diagnostico.Evento("MENSAGEM", ("tipo", motivo), ("ignorada", "sim"));
            return;
        }
        Diagnostico.Evento("MENSAGEM", ("tipo", motivo));
        TimeSpan esperaMinima = _eventosDoSistema.SinalizarMudancaDeTopologia();
        _releitura.Agendar(motivo, esperaMinima, daPropriaJanela);
    }

    /// <summary>
    /// O adaptador da janela de serviço mapeia WTS e energia para eventos do núcleo. Registra a mensagem primeiro; o árbitro
    /// entrega bloqueio/suspensão imediatamente e segura desbloqueio/retomada até uma topologia coerente ser publicada.
    /// Depois de a SUSPENDING entrar no núcleo, o pendente vai ao disco na hora (passo P11): escondido pelo usuário ou pela
    /// sessão, o núcleo não pede gravação, e o atraso de 2 s só cairia depois de acordar, ou nunca.
    /// </summary>
    private void AoEventoDoSistema(string motivo, Evento evento)
    {
        if (_encerrando) return;
        Diagnostico.Evento("MENSAGEM", ("tipo", motivo));
        TimeSpan esperaMinima = _eventosDoSistema.Sinalizar(evento);
        if (evento is Suspending)
            _gravacao?.Descarregar(nameof(Suspending));
        if (evento is SessionUnlocked or Resumed)
            _releitura.Agendar(motivo, esperaMinima);
    }

    /// <summary>
    /// A releitura agrupada (<see cref="AgendaDaReleitura"/>). Coerente, a topologia vai ao núcleo (TOPOLOGY_CHANGED), que
    /// revalida o personagem e reacomoda os itens (DEC-030); depois, o lugar das janelas é reafirmado (sem mexer na ordem Z), e
    /// o ícone muda se o DPI do principal mudou. Incoerente (troca de modo em andamento), a anterior continua valendo, e a
    /// agenda tenta de novo algumas vezes, com esperas crescentes. Devolve se publicou.
    /// </summary>
    private bool RelerAgrupada(PedidoDeReleitura pedido)
    {
        if (_encerrando) return false;
        string motivos = pedido.Motivos;

        // A última tentativa de uma rajada é parcial: um monitor que não pode ser lido por inteiro fica de fora, em vez de
        // manter para sempre uma topologia que já não existe (revisão de correção do bloco P6-P9, achado 7).
        LeituraDaTopologia? leitura = LeitorDeTopologia.LerDetalhado(out string? erro, parcial: !pedido.NovaTentativaSeFalhar);
        if (leitura is null)
        {
            Diagnostico.Evento("TOPOLOGIA", ("motivo", motivos), ("erro", erro), ("mantida", "anterior"), ("novaTentativa", pedido.NovaTentativaSeFalhar));
            _eventosDoSistema.TopologiaRelida(publicada: false);
            return false;
        }

        Topologia nova = leitura.Topologia;
        bool mudou = !nova.MesmaConfiguracao(_topologia);
        _leitura = leitura;
        _topologia = nova;
        Enviar(new TopologyChanged(nova), motivos);
        ReafirmarLugares(motivos);

        if (nova.Principal.Dpi != _dpiDoIcone && _bandeja is not null)
        {
            _dpiDoIcone = nova.Principal.Dpi;
            _bandeja.TrocarIcone(CriarIcone(_dpiDoIcone), aplicar: true);
        }

        Diagnostico.Evento("TOPOLOGIA",
            [("motivo", motivos), ("mudou", mudou ? "sim" : "nao"), ("visivel", _visivel), ("impressao", nova.ImpressaoDigital), .. CamposDasChaves(leitura)]);
        _eventosDoSistema.TopologiaRelida(publicada: true);
        // Os monitores ocupados dependem das telas: uma troca de modo de vídeo do jogo muda a tela do monitor dele (DEC-034).
        _telaCheia?.Sinalizar("topologia");
        return true;
    }

    /// <summary>
    /// A janela do personagem foi minimizada, e ela mesma já voltou ao normal (Fase 5, passo P12; DEC-031, adendo). Já
    /// escondido, por qualquer motivo, nunca vira CMD_HIDE: pela precedência, trocaria a ocultação da sessão ou da suspensão
    /// pela do usuário, e o desbloqueio não o mostraria mais; a janela só volta a ficar fora da vista. À vista, do usuário
    /// (<see cref="MinimizadaPeloSistema"/> falso), esconde, como sempre (Q-03). Do sistema, numa troca de monitores, não
    /// esconde: a releitura pendente reafirma o lugar do personagem e dos itens; sem ela (o monitor saiu antes de a mensagem
    /// chegar), a minimização pede uma, como uma mensagem de topologia.
    /// </summary>
    private void AoMinimizarPersonagem()
    {
        if (_encerrando || _personagem is null) return;
        bool pendente = _releitura.ReleituraPendente;
        if (!_visivel)
        {
            Diagnostico.Evento("MINIMIZADO", ("janela", "personagem"), ("escondido", "sim"), ("releituraPendente", pendente ? "sim" : "nao"));
            // Mostrada minimizada por fora, a janela voltou ao normal à vista, mas o WPF ainda a tem por escondida, e um Hide()
            // sozinho não faria nada: Show() e Hide() no mesmo tratamento a escondem de novo, com o WPF em dia.
            _personagem.Show();
            _personagem.Hide();
            return;
        }
        Topologia? agora = pendente ? _topologia : LeitorDeTopologia.LerDetalhado(out _)?.Topologia;
        bool peloSistema = MinimizadaPeloSistema(pendente, _topologia, agora);
        Diagnostico.Evento("MINIMIZADO", ("janela", "personagem"), ("escondido", "nao"), ("pelo", peloSistema ? "sistema" : "usuario"), ("releituraPendente", pendente ? "sim" : "nao"));
        if (!peloSistema)
        {
            Enviar(new CmdHide(), "minimizado pelo Windows");
            return;
        }
        if (!pendente) AoPossivelMudancaDeTopologia("WM_SIZE SIZE_MINIMIZED");
    }

    /// <summary>
    /// Se a minimização da janela do personagem foi do sistema, e não do usuário (Fase 5, passo P12; DEC-031, adendo): com
    /// "Minimizar janelas quando um monitor for desconectado", o Windows minimiza as janelas do monitor que sai. É do sistema
    /// com uma releitura da topologia pendente na agenda, com a leitura de agora diferente da publicada ou incoerente (nula:
    /// a troca de modo em andamento). Só sem nada disso a minimização é do usuário e esconde (Q-03). Função pura.
    /// </summary>
    internal static bool MinimizadaPeloSistema(bool releituraPendente, Topologia publicada, Topologia? lidaAgora)
    {
        ArgumentNullException.ThrowIfNull(publicada);
        return releituraPendente || lidaAgora is null || !lidaAgora.MesmaConfiguracao(publicada);
    }

    /// <summary>
    /// A releitura imediata do mostrar (passo P11): o núcleo usa a topologia que conhece, então uma mudança ocorrida com o
    /// personagem escondido é validada antes de a janela reaparecer. Ao contrário da agrupada (<see cref="RelerAgrupada"/>),
    /// não passa pela agenda: não arma a conferência tardia nem reafirma o lugar, que o CMD_SHOW aplica em seguida, e não
    /// libera o desbloqueio ou a retomada retidos pelo árbitro, que esperam a releitura das mensagens do Windows (DEC-031).
    /// Incoerente, a topologia anterior continua valendo, sem nova tentativa: a mensagem que vier relê pela agenda.
    /// </summary>
    private void RelerAntesDeMostrar(string motivo)
    {
        string motivos = $"revalidar antes de mostrar: {motivo}";
        LeituraDaTopologia? leitura = LeitorDeTopologia.LerDetalhado(out string? erro);
        if (leitura is null)
        {
            Diagnostico.Evento("TOPOLOGIA", ("motivo", motivos), ("imediata", "sim"), ("erro", erro), ("mantida", "anterior"));
            return;
        }

        Topologia nova = leitura.Topologia;
        bool mudou = !nova.MesmaConfiguracao(_topologia);
        _leitura = leitura;
        _topologia = nova;
        Enviar(new TopologyChanged(nova), motivos);
        Diagnostico.Evento("TOPOLOGIA",
            [("motivo", motivos), ("imediata", "sim"), ("mudou", mudou ? "sim" : "nao"), ("visivel", _visivel), ("impressao", nova.ImpressaoDigital), .. CamposDasChaves(leitura)]);
        _telaCheia?.Sinalizar("topologia");
    }

    /// <summary>
    /// A conferência tardia, 1,5 s depois da última releitura publicada (D14 do desenho dos monitores): com "Lembrar
    /// locais das janelas", o Windows pode devolver uma janela ao monitor reconectado depois da releitura, e o lugar é o
    /// que o núcleo decidiu. Um disparo por rajada, nunca periódico.
    /// </summary>
    private void ReafirmarDepoisDaReleitura()
    {
        if (_encerrando) return;
        ReafirmarLugares(MotivoDaReafirmacaoTardia);
    }

    /// <summary>
    /// As chaves de uma leitura, para a linha TOPOLOGIA do log (DEC-030): <c>chaves=chave=nomeGdi;…</c> na ordem do
    /// Windows, <c>consulta=ok</c> ou o motivo da falha (função e código), e as contagens de chaves do cache, da reserva
    /// e de alvos sem nome. A chave é o resumo opaco; nem o caminho do dispositivo nem o nome do monitor vão ao log. Numa
    /// leitura parcial, também quantos monitores ficaram de fora e a falha do primeiro (função e código).
    /// </summary>
    private static (string Campo, object? Valor)[] CamposDasChaves(LeituraDaTopologia leitura) =>
    [
        ("chaves", string.Join(";", leitura.Chaves.Select(c => $"{c.Chave}={c.NomeGdi}"))),
        ("consulta", leitura.ErroDaConsulta ?? "ok"),
        ("cache", leitura.ChavesDoCache),
        ("reserva", leitura.ChavesDeReserva),
        ("semNome", leitura.CaminhosSemNome),
        .. leitura.MonitoresIgnorados > 0 ? [("ignorados", leitura.MonitoresIgnorados), ("falhaDoIgnorado", leitura.MotivoDoIgnorado)] : Array.Empty<(string, object?)>(),
    ];

    // ------------------------------------------------------------------ ações

    /// <summary>
    /// Depois de uma releitura publicada e na conferência tardia dela: o lugar da janela do personagem
    /// (<see cref="ReafirmarLugarDaJanela"/>) e, com ele à vista, o das janelas dos itens
    /// (<see cref="GerenteDosItens.ReafirmarLugares"/>). Só o lugar: as duas movem as janelas sem mudar a ordem Z, que nunca
    /// é reafirmada por timer (SECURITY.md 2) e, nos itens, só muda nos eventos da DEC-028, item 22.
    /// </summary>
    private void ReafirmarLugares(string motivo)
    {
        ReafirmarLugarDaJanela(motivo);
        if (_visivel && !_encerrando) _itens?.ReafirmarLugares(motivo);
    }

    /// <summary>
    /// Depois de reler a topologia: se a janela não está onde o núcleo a pôs (o Windows a
    /// reposicionou ao trocar monitor ou DPI, ou outro agente a moveu) ou o sprite ficou em outro
    /// DPI, reaplica o lugar do núcleo, que é quem decide a posição. Nunca no meio de um gesto do
    /// usuário (ARCHITECTURE.md 2.8: com o botão pressionado, a validação é ao soltar).
    /// </summary>
    private void ReafirmarLugarDaJanela(string motivo)
    {
        if (_personagem is null || _nucleo is null || !_visivel || _encerrando) return;
        if (_nucleo.Estado.Estado is Estado.Pressed or Estado.Dragging) return;
        if (_nucleo.Estado.Lugar is not { } lugar) return;
        RetanguloPx? real = _personagem.RetanguloReal();
        if (real == lugar.Retangulo && lugar.Monitor.Dpi == _dpiDoSprite) return;
        Diagnostico.Evento("POSICAO", ("reaplicada", "sim"), ("motivo", motivo), ("real", real), ("nucleo", lugar.Retangulo));
        _posicionamento = lugar;
        AplicarNaJanela(lugar);
    }

    /// <summary>
    /// A topologia da partida: até cinco leituras, 200 ms entre elas; depois, uma leitura parcial, o último recurso, que
    /// deixa de fora um monitor que não pode ser lido por inteiro (revisão de correção do bloco P6-P9, achado 7): com uma
    /// falha persistente de um monitor, o Buzzy não partia. Nula se nem ela sai.
    /// </summary>
    private static LeituraDaTopologia? LerTopologiaNaPartida()
    {
        string? erro = null;
        for (int tentativa = 1; tentativa <= 5; tentativa++)
        {
            LeituraDaTopologia? leitura = LeitorDeTopologia.LerDetalhado(out erro);
            if (leitura is not null) return leitura;
            Thread.Sleep(200);
        }
        if (LeitorDeTopologia.LerDetalhado(out string? erroDaParcial, parcial: true) is { } parcial) return parcial;
        Diagnostico.Evento("ERRO", ("etapa", "topologia inicial"), ("mensagem", erro), ("parcial", erroDaParcial));
        return null;
    }

    private void AplicarNaJanela(Posicionamento p)
    {
        if (_personagem is null) return;
        if (p.Monitor.Dpi != _dpiDoSprite || _personagem.Sprite is null)
        {
            QuadroDoSprite quadro = _quadroAtual ?? new QuadroDoSprite("parado", false, null);
            _personagem.DefinirSprite(SpriteProvisorio.Renderizar(quadro, p.Monitor.Dpi, _personagem.Tamanho));
            _quadroAtual = quadro;
            _dpiDoSprite = p.Monitor.Dpi;
        }
        _personagem.AplicarRetangulo(p.Retangulo);
        // Durante o arraste e o movimento, registrar cada passo pesaria no próprio movimento: a
        // posição é registrada quando o personagem para (fim de ProcessarFilaDoNucleo).
        if (EmMovimentoOuArraste()) _posicaoSemRegistro = true;
        else RegistrarPosicao(p);
    }

    private bool EmMovimentoOuArraste()
        => _nucleo?.Estado.Estado is { } e && (e == Estado.Dragging || e.EmMovimento());

    /// <summary>
    /// Só com <c>--diagnostico</c>: registra a posição e dois pontos de teste do sprite em pé (um
    /// opaco e um transparente, em coordenadas de tela), usados pelas verificações de tela para
    /// clicar no personagem parado. Os pontos são sempre os do quadro "parado" no DPI do monitor.
    /// </summary>
    private void RegistrarPosicao(Posicionamento p)
    {
        _posicaoSemRegistro = false;
        if (!Diagnostico.Ligado || _personagem is null) return;
        string opaco = "indisponível", transparente = "indisponível";
        try
        {
            (PontoPx o, PontoPx t) = SpriteProvisorio.PontosDeTeste(SpriteProvisorio.Renderizar(p.Monitor.Dpi, _personagem.Tamanho));
            opaco = $"{p.Retangulo.Esquerda + o.X},{p.Retangulo.Topo + o.Y}";
            transparente = $"{p.Retangulo.Esquerda + t.X},{p.Retangulo.Topo + t.Y}";
        }
        catch (InvalidOperationException e)
        {
            Diagnostico.Evento("ERRO", ("etapa", "pontos de teste do sprite"), ("mensagem", e.Message));
        }
        Diagnostico.Evento("POSICAO",
            ("monitor", p.Monitor.Chave),
            ("gdi", _leitura?.NomeGdi(p.Monitor.Chave) ?? "-"),
            ("retangulo", p.Retangulo),
            ("ancora", p.Ancora),
            ("dpi", p.Monitor.Dpi),
            ("areaUtil", p.Monitor.AreaUtil),
            ("pontoOpaco", opaco),
            ("pontoTransparente", transparente));
    }

    private void MostrarPorComando(string motivo)
    {
        if (_encerrando || _personagem is null) return;
        bool jaEstavaVisivel = _visivel;
        RelerAntesDeMostrar(motivo);
        Enviar(new CmdShow(), motivo);
        if (_encerrando || !_visivel) return;

        _personagem.AoTopoDaFaixa();
        // Os itens à vista voltam para logo abaixo do personagem, que acabou de ir para o topo (L17).
        _itens?.ReordenarAbaixoDoPersonagem();
        if (jaEstavaVisivel) RegistrarVisibilidade(true, motivo);
    }

    private void ExibirMenuDoDesktop(PontoPx ponto, string origem, bool peloTeclado)
    {
        if (_encerrando) return;
        // A decisão usa o estado do momento em que o menu abriu, que é o texto que o
        // usuário leu no item. O laço modal do menu continua despachando operações.
        // A marca de rádio fica na emoção dominante atual (DEC-027); em alto contraste, o menu fica só com texto. O
        // submenu "Itens" só existe com a chave do tamagotchi, e "Recolher itens" só vale com itens na tela (DEC-028).
        bool visivelAoAbrir = _visivel;
        ModeloDoMenu modelo = MenuNativo.ModeloAoAbrir(_nucleo, visivelAoAbrir, SystemParameters.HighContrast);
        bool pausadoAoAbrir = modelo.MovimentoPausado;
        // Os ícones são ampliados pelo DPI do monitor onde o menu abre: o Windows não amplia o bitmap de um item.
        int dpi = MenuNativo.DpiAoAbrir(_topologia, ponto);
        Diagnostico.Evento("MENU", ("aberto", origem), ("ponto", ponto), ("peloTeclado", peloTeclado ? "sim" : "nao"));
        // Com o menu aberto, o disparo da curiosidade espera (DEC-037, item 6); sai depois do comando escolhido. O comando roda
        // com o dono do menu ainda vivo (DEC-038, item 4).
        _menuAberto = true;
        try
        {
            MenuNativo.Mostrar(ponto, modelo, dpi, abrirParaCima: origem == "bandeja", escolha =>
            {
                _menuAberto = false;
                if (!_encerrando) ExecutarEscolhaDoMenu(escolha, modelo, visivelAoAbrir, pausadoAoAbrir, peloTeclado);
            });
        }
        finally
        {
            _menuAberto = false;
            Adiar(EntregarCuriosidadeAdiada);
        }
    }

    private void ExecutarEscolhaDoMenu(EscolhaDoMenu escolha, ModeloDoMenu modelo, bool visivelAoAbrir, bool pausadoAoAbrir, bool peloTeclado)
    {
        switch (escolha.Comando)
        {
            case ComandoDoMenu.AlternarVisibilidade when visivelAoAbrir:
                Enviar(new CmdHide(), "menu");
                break;
            case ComandoDoMenu.AlternarVisibilidade:
                MostrarPorComando("menu");
                break;
            case ComandoDoMenu.AlternarMovimento when pausadoAoAbrir:
                Enviar(new CmdResumeAutonomy(), "menu");
                break;
            case ComandoDoMenu.AlternarMovimento:
                Enviar(new CmdPauseAutonomy(), "menu");
                break;
            case ComandoDoMenu.Sair:
                Enviar(new CmdExit(), "menu");
                break;
            case ComandoDoMenu.Emocao:
                // Uma das 14 caras de humor ou "Automática" (nula). O núcleo grava a preferência (GravarPreferencias), e
                // ela vai para o settings.json com atraso: sobrevive a reabrir o app (Fase 5, passo P7).
                Enviar(new CmdSetDominantEmotion(escolha.Emocao), "menu");
                break;
            case ComandoDoMenu.Item when escolha.Item is { } item:
                // Um item do tamagotchi (DEC-028): nasce ao lado do personagem e cai (o núcleo decide onde).
                Enviar(new CmdSummonItem(item), "menu");
                break;
            case ComandoDoMenu.RecolherItens:
                Enviar(new CmdClearItems(), "menu");
                break;
            case ComandoDoMenu.ConteudoAdulto:
                // Inverte o que o usuário leu ao abrir (DEC-033): desligar tira o que é adulto da tela; a escolha vai para o
                // settings.json com atraso, como a emoção.
                Enviar(new CmdSetAdultContent(!modelo.ConteudoAdulto), "menu");
                break;
            case ComandoDoMenu.ModoTelaCheia:
                // "Desviar da tela cheia" (DEC-034): inverte o que o usuário leu ao abrir. Desligado, ele volta para onde estava
                // antes da tela cheia; ligado com ele num monitor ocupado, sai de lá. A escolha vai para o settings.json com
                // atraso, como a emoção.
                Enviar(new CmdSetFullscreenMode(!modelo.ModoTelaCheia), "menu");
                break;
            case ComandoDoMenu.Energia:
                // O painel compacto (DEC-038, item 4): o núcleo pausa a autonomia e pede a janela (AbrirPainelDeEnergia).
                Enviar(new EnergyPanelOpen(), "menu");
                break;
            case ComandoDoMenu.Configuracoes:
                Enviar(new CmdOpenSettings(), "menu");
                break;
            case ComandoDoMenu.Nenhum when peloTeclado:
                // Quem abriu o menu da bandeja pelo teclado e o cancelou volta para a área de
                // notificação (documentação do NIM_SETFOCUS). Depois de um cancelamento com o
                // mouse, não: o clique fora pode ter ativado outro aplicativo.
                _bandeja?.DevolverFoco();
                break;
        }
    }

    private void Enviar(Evento evento, string motivo)
    {
        if (!Enfileirar(evento, motivo)) return;
        ProcessarFilaDoNucleo();
    }

    /// <summary>Põe o evento na fila do núcleo sem processar (lote de passos do relógio).</summary>
    private bool Enfileirar(Evento evento, string motivo)
    {
        if (_encerrando) return false;
        if (_nucleo is null) throw new InvalidOperationException("O núcleo ainda não foi criado.");

        if (!_nucleo.Enfileirar(evento))
        {
            Diagnostico.Evento("NUCLEO", ("evento", evento.GetType().Name), ("descartado", "autônomo sob controle do usuário"));
            return false;
        }

        _motivosDoNucleo[evento] = motivo;
        return true;
    }

    private void ProcessarFilaDoNucleo()
    {
        if (_processandoNucleo || _nucleo is null) return;
        _processandoNucleo = true;
        try
        {
            while (_nucleo.Pendentes > 0 && !_encerrando)
            {
                Evento[] eventosDoLote = [.. _motivosDoNucleo.Keys];
                var efeitos = new List<(Evento Evento, Efeito Efeito, string Motivo)>();
                long descartadosAntes = _nucleo.Descartados;
                // O estado antes de cada evento do lote, para a linha do sorteio da paranoia (só com --diagnostico).
                EstadoDoNucleo anterior = _nucleo.Estado;
                Chance chanceDaParanoia = _nucleo.Configuracao.ChanceDaParanoia;
                try
                {
                    _nucleo.Processar((eventoAplicado, resultado) =>
                    {
                        string motivoEvento = _motivosDoNucleo.GetValueOrDefault(eventoAplicado, "sem motivo");
                        foreach (Transicao transicao in resultado.Transicoes)
                        {
                            Diagnostico.Evento("NUCLEO",
                                ("evento", eventoAplicado.GetType().Name),
                                ("motivo", motivoEvento),
                                ("de", transicao.De),
                                ("para", transicao.Para),
                                ("regra", transicao.Regra));
                        }
                        // O sorteio da paranoia, saindo ou não, numa linha à parte da regra (LigacaoDosItens.SorteioDaParanoia).
                        if (Diagnostico.Ligado && LigacaoDosItens.SorteioDaParanoia(anterior, resultado.Estado, chanceDaParanoia) is { } sorteio)
                            Diagnostico.Evento("PARANOIA", sorteio);
                        anterior = resultado.Estado;
                        foreach (Efeito efeito in resultado.Efeitos)
                            efeitos.Add((eventoAplicado, efeito, motivoEvento));
                    });
                }
                finally
                {
                    foreach (Evento eventoDoLote in eventosDoLote)
                        _motivosDoNucleo.Remove(eventoDoLote);
                }

                long descartados = _nucleo.Descartados - descartadosAntes;
                if (descartados > 0)
                    Diagnostico.Evento("NUCLEO", ("autonomosDescartados", descartados));

                Efeito[]? soEfeitos = null;
                for (int i = 0; i < efeitos.Count; i++)
                {
                    if (_encerrando) break;
                    (Evento evento, Efeito efeito, string motivoEvento) = efeitos[i];
                    // Num lote (vários passos do relógio no mesmo quadro), só a última posição
                    // antes do próximo mostrar/esconder vai para a janela: uma movimentação por quadro.
                    if (efeito is MoverJanela && MovimentacaoPosterior(efeitos, i)) continue;
                    // O mesmo para cada janela de item: pula um movimento só com outro do mesmo item adiante (C19).
                    if (efeito is MoverItem && GerenteDosItens.MovimentoPosterior(soEfeitos ??= [.. efeitos.Select(e => e.Efeito)], i)) continue;
                    ExecutarEfeito(evento, efeito, motivoEvento);
                }
            }
        }
        finally
        {
            _processandoNucleo = false;
        }
        AtualizarSprite();
        if (_posicaoSemRegistro && _visivel && !_encerrando && !EmMovimentoOuArraste()) RegistrarPosicao(_posicionamento);
        // Só com --diagnostico: o item que acabou de parar no chão ganha a linha ITEM|movido…|parado=sim, mesmo quando o
        // passo do pouso não moveu a janela dele.
        if (Diagnostico.Ligado && !_encerrando) _itens?.RegistrarPousos();
        // O disparo da gravação que chegou no meio de um gesto do usuário grava quando o gesto acaba (L5 da crítica).
        if (!_encerrando) _gravacao?.ConferirFimDoGesto();
    }

    /// <summary>
    /// Se o usuário está no meio de um gesto (o botão pressionado, um arraste ou um item na mão, DEC-028): a gravação com
    /// atraso espera o fim dele, para a E/S não cair no meio do gesto.
    /// </summary>
    private bool GestoDoUsuarioEmCurso()
        => _nucleo?.Estado is { } s && (s.Estado is Estado.Pressed or Estado.Dragging || s.Atento);

    /// <summary>Se há outra <see cref="MoverJanela"/> depois de <paramref name="i"/>, sem mostrar/esconder no meio.</summary>
    private static bool MovimentacaoPosterior(List<(Evento Evento, Efeito Efeito, string Motivo)> efeitos, int i)
    {
        for (int j = i + 1; j < efeitos.Count; j++)
        {
            switch (efeitos[j].Efeito)
            {
                case MoverJanela:
                    return true;
                case MostrarJanela or EsconderJanela:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Poses provisórias da Fase 4: escolhe o quadro pelo retrato do núcleo e só troca o sprite
    /// quando o quadro ou o DPI mudam (ARCHITECTURE.md 2.10: redesenho só quando o quadro muda).
    /// </summary>
    private void AtualizarSprite()
    {
        if (_personagem is null || _nucleo is null || _encerrando || _posicionamento is null) return;
        Retrato retrato = _nucleo.Retrato;
        if (!retrato.Estado.Visivel()) return;
        EstadoDoMovimento movimento = _nucleo.Estado.Movimento;
        // A contagem da pose recomeça a cada troca de estado e a cada quique de borracha, que
        // continua em JUMPING (toon force, DEC-023).
        if (retrato.Estado != _estadoDoQuadro || movimento.Quiques != _quiquesDoQuadro)
        {
            _estadoDoQuadro = retrato.Estado;
            _quiquesDoQuadro = movimento.Quiques;
            _passoDeEntradaNoEstado = _nucleo.Estado.Passos;
        }
        int dpi = _posicionamento.Monitor.Dpi;
        var dinamica = new Dinamica(movimento.VY * 96.0 / dpi, movimento.Quiques, movimento.Foguete, movimento.Agarrado, _nucleo.Estado.Esconderijo);
        QuadroDoSprite quadro = PoseDoPersonagem.Escolher(retrato, _nucleo.Estado.Passos - _passoDeEntradaNoEstado, dinamica);
        if (quadro == _quadroAtual && dpi == _dpiDoSprite && _personagem.Sprite is not null) return;
        // Uma linha por quadro desenhado (fora do cache). Com o cache cheio, um quadro novo descarta outro, e a contagem
        // de quadros no cache não muda: o que conta é o desenho.
        long desenhadosAntes = SpriteProvisorio.QuadrosRenderizados;
        _personagem.DefinirSprite(SpriteProvisorio.Renderizar(quadro, dpi, _personagem.Tamanho));
        if (SpriteProvisorio.QuadrosRenderizados != desenhadosAntes)
        {
            Diagnostico.Evento("SPRITE",
                ("quadrosEmCache", SpriteProvisorio.QuadrosEmCache),
                ("bytesEmCache", SpriteProvisorio.BytesEmCache),
                ("descartados", SpriteProvisorio.QuadrosDescartados),
                ("pose", quadro.Pose),
                ("expressao", quadro.Expressao ?? "-"),
                ("item", quadro.Item ?? "-"),
                ("efeito", quadro.Efeito),
                ("fase", quadro.Fase),
                ("deformacao", quadro.Deformacao),
                ("dpi", dpi));
        }
        _quadroAtual = quadro;
        _dpiDoSprite = dpi;
    }

    /// <summary>
    /// Memória do processo no log de diagnóstico, só quando o movimento para (nunca por timer):
    /// o heap gerenciado, o comprometido pelo GC, o conjunto de trabalho e os quadros em cache, com os bytes deles
    /// (limitados a 16 MiB: crítica, C28), e as janelas de item vivas (DEC-028).
    /// </summary>
    private void RegistrarMemoria(string quando)
    {
        if (!Diagnostico.Ligado) return;
        GCMemoryInfo gc = GC.GetGCMemoryInfo();
        const double MB = 1024 * 1024;
        Diagnostico.Evento("MEMORIA",
            ("quando", quando),
            ("heapMB", Math.Round(gc.HeapSizeBytes / MB, 1)),
            ("comprometidaGcMB", Math.Round(gc.TotalCommittedBytes / MB, 1)),
            ("conjuntoMB", Math.Round(Environment.WorkingSet / MB, 1)),
            ("quadrosEmCache", SpriteProvisorio.QuadrosEmCache),
            ("bytesEmCache", SpriteProvisorio.BytesEmCache),
            ("janelasDeItens", _itens?.Quantas ?? 0),
            ("gc0", GC.CollectionCount(0)), ("gc1", GC.CollectionCount(1)), ("gc2", GC.CollectionCount(2)));
    }

    private void ExecutarEfeito(Evento evento, Efeito efeito, string motivo)
    {
        switch (efeito)
        {
            case MoverJanela mover:
                _posicionamento = mover.Destino;
                AplicarNaJanela(mover.Destino);
                break;

            case MostrarJanela:
                if (_personagem is null || _visivel) break;
                _personagem.Show();
                _visivel = true;
                // O WPF pode reaplicar a posição inicial ao mostrar a janela. Confirma o
                // retângulo físico depois de Show(), como fazia a composição da Fase 1.
                AplicarNaJanela(_posicionamento);
                // O personagem reapareceu no topo: os itens à vista ficam logo abaixo dele (L17).
                _itens?.ReordenarAbaixoDoPersonagem();
                RegistrarVisibilidade(true, motivo);
                break;

            case EsconderJanela:
                if (_personagem is null || !_visivel) break;
                _personagem.Hide();
                _visivel = false;
                RegistrarVisibilidade(false, motivo);
                break;

            case LigarRelogio:
                if (!_relogioLigado) Diagnostico.Evento("RELOGIO", ("ligado", "sim"), ("evento", evento.GetType().Name));
                IniciarRelogio();
                break;

            case DesligarRelogio:
                if (_relogioLigado)
                {
                    Diagnostico.Evento("RELOGIO", ("ligado", "nao"), ("evento", evento.GetType().Name));
                    RegistrarMemoria("relógio desligado");
                }
                PararRelogio();
                break;

            case AgendarDecisao agendar:
                Diagnostico.Evento("AGENDA", ("atrasoMs", (long)agendar.Atraso.TotalMilliseconds), ("geracao", agendar.Geracao));
                AgendarTemporizadorDeDecisao(agendar);
                break;

            case CancelarDecisao:
                Diagnostico.Evento("AGENDA", ("cancelada", "sim"));
                CancelarDecisaoAutonoma();
                break;

            case LiberarCaptura:
                // O núcleo encerrou o gesto por conta própria (esconder ou sair no meio dele): a
                // arbitragem esquece o gesto e a janela solta o mouse, sem gerar DRAG_CANCEL.
                _arbitro.Reiniciar();
                _latenciasDoArraste.Clear();
                _personagem?.SoltarCaptura();
                break;

            case AbrirMenu pedidoMenu:
                // O laço modal do menu roda depois do processamento, não dentro dele: eventos que
                // chegam com o menu aberto (relógio, agenda, bandeja) são aplicados na hora.
                Adiar(() => ExibirMenuDoDesktop(pedidoMenu.Ponto, "personagem", peloTeclado: false));
                break;

            case AgendarCuriosidade or CancelarCuriosidade or PedirVaoDaJanelaAtiva:
                // A curiosidade (DEC-037; Aplicacao.Curiosidade.cs).
                ExecutarEfeitoDaCuriosidade(efeito);
                break;

            case MostrarItem or MoverItem or EsconderItem or RemoverItem or LiberarCapturaDoItem or AgendarOnda or CancelarOnda:
                // Tamagotchi (DEC-028): as janelas dos itens e o temporizador da onda (Aplicacao.Itens.cs).
                ExecutarEfeitoDoTamagotchi(efeito);
                break;

            case Encerrar:
                EncerrarAplicacao(motivo);
                break;

            case GravarPreferencias preferencias:
                // As janelas abertas acompanham o que o núcleo gravou (DEC-038); depois, a gravação de sempre.
                AtualizarJanelasDasPreferencias(preferencias.Preferencias);
                _gravacao?.Pedir(efeito, evento);
                break;

            case GravarPosicao:
                // O settings.json (Fase 5, passo P7; DEC-029): com atraso, ou na hora quando o evento é de suspensão, fim
                // de sessão, saída ou bloqueio. A posição, a postura e as preferências vêm do efeito, nunca do estado.
                _gravacao?.Pedir(efeito, evento);
                break;

            case AbrirPainelDeEnergia:
                AbrirPainelDeEnergiaPeloNucleo();
                break;

            case FecharPainelDeEnergia:
                FecharPainelDeEnergiaPeloNucleo();
                break;

            case AbrirConfiguracoes:
                AbrirConfiguracoesPeloNucleo();
                break;

            case AplicarSempreNoTopo topo:
                AplicarSempreNoTopo(topo.Ligado, "comando");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(efeito), efeito, "Efeito do núcleo sem adaptador.");
        }
    }

    private void RegistrarVisibilidade(bool visivel, string motivo)
        => Diagnostico.Evento("VISIVEL", ("visivel", visivel ? "sim" : "nao"), ("motivo", motivo));

    /// <summary>
    /// Liga o relógio de passo fixo: inscreve-se nos quadros do compositor do WPF, que só existem
    /// enquanto alguém está inscrito. Sem nada se mexendo, não há inscrição nem quadros (DEC-011).
    /// </summary>
    private void IniciarRelogio()
    {
        if (_relogioLigado) return;
        _relogioLigado = true;
        _tempoAcumulado = TimeSpan.Zero;
        _ultimaMarcacaoRelogio = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += AoQuadroDoCompositor;
    }

    private void PararRelogio()
    {
        if (!_relogioLigado) return;
        _relogioLigado = false;
        CompositionTarget.Rendering -= AoQuadroDoCompositor;
        _tempoAcumulado = TimeSpan.Zero;
    }

    /// <summary>
    /// Um quadro do compositor: aplica os passos fixos acumulados desde o anterior num lote só, e a
    /// janela vai para a posição do último passo.
    /// </summary>
    private void AoQuadroDoCompositor(object? remetente, EventArgs e)
    {
        if (_nucleo is null || !_relogioLigado || _encerrando) return;
        long agora = Stopwatch.GetTimestamp();
        _tempoAcumulado += Stopwatch.GetElapsedTime(_ultimaMarcacaoRelogio, agora);
        _ultimaMarcacaoRelogio = agora;

        TimeSpan passo = TimeSpan.FromSeconds(1d / _nucleo.Configuracao.PassosPorSegundo);
        // Depois de uma parada da thread da interface (chamada lenta ao Windows, depurador), os
        // passos atrasados não saem todos de uma vez: o relógio lógico perde o excesso, em vez de
        // aplicar uma rajada de movimentos. O passo continua fixo (ARCHITECTURE.md 2.9).
        if (_tempoAcumulado > AtrasoMaximoDoRelogio)
        {
            long descartados = (long)((_tempoAcumulado - AtrasoMaximoDoRelogio) / passo);
            _tempoAcumulado = AtrasoMaximoDoRelogio;
            Diagnostico.Evento("RELOGIO", ("atraso", "limitado"), ("passosDescartados", descartados));
        }
        bool algum = false;
        while (_tempoAcumulado >= passo)
        {
            _tempoAcumulado -= passo;
            algum |= Enfileirar(new Tick(), $"relógio passo {++_passosDoRelogio}");
        }
        if (algum) ProcessarFilaDoNucleo();
    }

    private void AgendarTemporizadorDeDecisao(AgendarDecisao agendamento)
    {
        CancelarDecisaoAutonoma();
        var temporizador = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = agendamento.Atraso > TimeSpan.Zero ? agendamento.Atraso : TimeSpan.FromMilliseconds(1),
        };
        EventHandler aoDisparar = null!;
        aoDisparar = (_, _) =>
        {
            temporizador.Stop();
            temporizador.Tick -= aoDisparar;
            if (ReferenceEquals(_decisaoAutonoma, temporizador))
            {
                _decisaoAutonoma = null;
                _aoDispararDecisao = null;
            }
            Diagnostico.Evento("AGENDA", ("disparo", agendamento.Geracao));
            Enviar(new AutonomyTimer(agendamento.Geracao), $"agenda autônoma geração {agendamento.Geracao}");
        };
        _decisaoAutonoma = temporizador;
        _aoDispararDecisao = aoDisparar;
        temporizador.Tick += aoDisparar;
        temporizador.Start();
    }

    private void CancelarDecisaoAutonoma()
    {
        DispatcherTimer? temporizador = _decisaoAutonoma;
        EventHandler? aoDisparar = _aoDispararDecisao;
        _decisaoAutonoma = null;
        _aoDispararDecisao = null;
        if (temporizador is null) return;
        temporizador.Stop();
        if (aoDisparar is not null) temporizador.Tick -= aoDisparar;
    }

    private void EncerrarAplicacao(string motivo)
    {
        if (_encerrando) return;
        _encerrando = true;
        Diagnostico.Evento("ENCERRANDO", ("motivo", motivo));
        _eventosDoSistema.Parar();

        // O pendente vai ao disco antes de desmontar qualquer coisa, e a agenda para (crítica, C18): nenhum disparo da
        // gravação sobra depois do encerramento.
        _gravacao?.Descarregar("encerrar");
        _gravacao?.Parar();

        // Um menu aberto nesta thread (por exemplo, fim de sessão com o menu na tela) é
        // fechado antes de destruir as janelas.
        Win32.EndMenu();
        _personagem?.SoltarCaptura();
        // A releitura agendada e a conferência tardia (disparos únicos) não saem depois do encerramento.
        _releitura.Parar();
        PararTelaCheia();
        _repetirBandeja.Stop();
        PararRelogio();
        CancelarDecisaoAutonoma();
        // O núcleo não manda efeito de janela de item ao sair: a raiz fecha todas e para o temporizador da onda.
        EncerrarItens();
        FecharConfiguracoes();
        _bandeja?.Dispose();
        _servico?.Dispose();
        _personagem?.Close();
        _app.Shutdown(CodigosDeSaida.Normal);
    }

    // ------------------------------------------------------------------ tela cheia (DEC-013, DEC-034)

    /// <summary>
    /// Liga o modo de tela cheia (DEC-034): o observador assina a troca de primeiro plano, e a primeira avaliação sai na hora,
    /// para um jogo que já estava em tela cheia na partida. O núcleo recebe os monitores ocupados mesmo com o modo
    /// desligado, só para o cache: ligar o modo pelo menu age na hora. Sem a assinatura, o modo não age, e o log diz.
    /// </summary>
    private void IniciarTelaCheia()
    {
        if (_opcoes.SemTelaCheia)
        {
            Diagnostico.Evento("TELA_CHEIA", ("observador", "desligado"), ("motivo", "--sem-tela-cheia"));
            return;
        }
        var observador = new ObservadorDeTelaCheia();
        if (!observador.Iniciar())
        {
            Diagnostico.Evento("TELA_CHEIA", ("observador", "falhou"));
            return;
        }
        _observadorDeTelaCheia = observador;
        _telaCheia = new AgendaDaTelaCheia(observador.Ler, () => _topologia,
            (espera, acao) => DisparoUnico.NoDispatcher(espera, acao, DispatcherPriority.Normal), PublicarTelaCheia, IniciarCuriosidade());
        observador.Sinal += motivo => _telaCheia?.Sinalizar(motivo);
        Diagnostico.Evento("TELA_CHEIA", ("observador", "ligado"));
        _telaCheia.AvaliarAgora("início");
    }

    /// <summary>
    /// Uma mudança dos monitores ocupados vai ao núcleo (FULLSCREEN_TARGETS_CHANGED). No log, só as chaves opacas, os
    /// motivos, o sinal do shell, quantos monitores a janela cobria antes dele e as contagens de eventos: nunca a janela, o
    /// retângulo dela ou de quem ela é (SECURITY.md 3.1). O fim da tela cheia devolve o personagem ao topo do grupo "sempre no
    /// topo", uma vez: a janela em tela cheia, ativada depois dele, pode ter ficado por cima, e ele voltaria escondido atrás
    /// dela, como no relato do usuário de 2026-10-03.
    /// </summary>
    private void PublicarTelaCheia(MudancaDaTelaCheia mudanca)
    {
        if (_encerrando || _nucleo is null) return;
        Diagnostico.Evento("TELA_CHEIA",
            ("ocupados", mudanca.Ocupados.Vazio ? "-" : string.Join(";", mudanca.Ocupados.Chaves)),
            ("motivo", mudanca.Motivos),
            ("shell", mudanca.Shell?.ToString() ?? "falhou"),
            ("candidatos", mudanca.Candidatos),
            ("eventosPrimeiroPlano", _observadorDeTelaCheia?.EventosDePrimeiroPlano ?? 0),
            ("eventosGeometria", _observadorDeTelaCheia?.EventosDeGeometria ?? 0));
        Enviar(new FullscreenTargetsChanged(mudanca.Ocupados), $"tela cheia: {mudanca.Motivos}");
        // Com o "sempre no topo" desligado, o fim da tela cheia não reordena (DEC-038, item 8; DEC-034, item 7).
        if (!mudanca.Ocupados.Vazio || _encerrando || !_visivel || _personagem is null || !_personagem.SempreNoTopo) return;
        _personagem.AoTopoDaFaixa();
        _itens?.ReordenarAbaixoDoPersonagem();
    }

    /// <summary>
    /// O encerramento: nenhuma avaliação sai depois, os ganchos saem, e o log leva as contagens finais (P7). O observador
    /// continua referenciado até o fim do processo: um evento que já estava na fila ainda chama o delegado dele.
    /// </summary>
    private void PararTelaCheia()
    {
        _telaCheia?.Parar();
        PararCuriosidade();
        if (_observadorDeTelaCheia is not { Ligado: true } observador) return;
        // DEC-037, item 11: no fim, só quantos vãos o núcleo pediu (limitado pelas aproximações do próprio Buzzy).
        Diagnostico.Evento("TELA_CHEIA", ("fim", "sim"), ("eventosPrimeiroPlano", observador.EventosDePrimeiroPlano), ("eventosGeometria", observador.EventosDeGeometria),
            ("vaosPedidos", _telaCheia?.VaosPedidos ?? 0));
        observador.Dispose();
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>
    /// Erro não tratado: grava o que der do pendente, uma vez só, sem lançar e sem reentrar (o erro pode ter vindo da
    /// própria gravação). O log leva só o tipo e o código.
    /// </summary>
    private void DescarregarNoErro()
    {
        if (_descarregouNoErro) return;
        _descarregouNoErro = true;
        try
        {
            _gravacao?.Descarregar("erro");
        }
        catch (Exception e)
        {
            Diagnostico.Evento("CONFIG", ("descarregado", "nao"), ("motivo", "erro"), ("erro", $"{e.GetType().Name} 0x{e.HResult:X8}"));
        }
    }

    /// <summary>Executa depois de a mensagem atual terminar, na thread da interface.</summary>
    private void Adiar(Action acao) => _app.Dispatcher.BeginInvoke(acao);

    private void AdicionarIconeNaBandeja()
    {
        if (_bandeja is null || _encerrando) return;
        if (_bandeja.Adicionar())
        {
            _servico!.NotificacoesVersao4 = _bandeja.Versao4;
            return;
        }
        AgendarNovaTentativaDaBandeja();
    }

    private void AgendarNovaTentativaDaBandeja()
    {
        if (++_tentativasDaBandeja > TentativasDaBandeja) return;
        _repetirBandeja.Stop();
        _repetirBandeja.Start();
    }

    /// <summary>Âncora de reserva para o menu quando a notificação não traz coordenadas.</summary>
    private PontoPx PontoDoIcone()
    {
        if (_bandeja?.Retangulo() is { } r) return r.Centro;
        RetanguloPx area = _topologia.Principal.AreaUtil;
        return new PontoPx(area.Direita - 1, area.Base - 1);
    }

    private static nint CriarIcone(int dpiPrincipal)
    {
        int lado = Win32.GetSystemMetricsForDpi(Win32.SM_CXSMICON, (uint)dpiPrincipal);
        if (lado <= 0) lado = 16;
        byte[] png = SpriteProvisorio.IconePng(lado);
        nint icone = Win32.CreateIconFromResourceEx(png, png.Length, true, 0x00030000, lado, lado, 0);
        Diagnostico.Evento("ICONE", ("lado", lado), ("dpi", dpiPrincipal), ("criado", icone != 0));
        return icone;
    }
}
