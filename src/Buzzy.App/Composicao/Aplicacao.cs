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

// Opções da linha de comando.
// --pausado: começa sem movimento autônomo (as verificações de tela precisam dele parado).
// --semente N: semente fixa da agenda, pra reproduzir um comportamento. Sem ela, vem do relógio.
// --perfil-de-teste NOME: dados em %LOCALAPPDATA%\Buzzy\testes\NOME, pros testes nunca
//   tocarem as configurações reais. O log de diagnóstico continua na raiz da pasta do Buzzy.
// PersistenciaDesligada: --perfil-de-teste sem nome ou com nome inválido. Falha fechada:
//   não lê nem grava nada, em vez de cair na pasta real.
// --sem-tela-cheia: não liga o observador do primeiro plano, pra não depender do que
//   estiver em tela cheia na máquina.
internal sealed record OpcoesDaAplicacao(bool MovimentoPausado, ulong? Semente, string? PerfilDeTeste = null, bool PersistenciaDesligada = false, bool SemTelaCheia = false)
{
    internal static readonly OpcoesDaAplicacao Padrao = new(false, null);
}

// Raiz de composição: liga o núcleo puro (personagem, posicionamento, gestos) às janelas,
// captura do mouse, bandeja, menu e mensagens do Windows.
//
// Parado, nada roda em timer periódico; todo timer é de disparo único (releitura dos
// monitores, bandeja, agenda autônoma, onda, gravação, tela cheia). O relógio de passo
// fixo só corre quando o núcleo pede e anda com CompositionTarget.Rendering: a janela
// se move no máximo uma vez por quadro. O arraste não usa relógio: cada movimento do
// mouse já vira posição dentro da mesma mensagem.
//
// Itens, gestos sobre eles e a onda ficam em Aplicacao.Itens.cs.
internal sealed partial class Aplicacao
{
    // Motivo nas linhas POSICAO|reaplicada e ITEM|reaplicado.
    private const string MotivoDaReafirmacaoTardia = "reafirmação tardia";

    private const int TentativasDaBandeja = 3;

    // Maior atraso que o relógio recupera de uma vez (15 passos a 60 Hz).
    private static readonly TimeSpan AtrasoMaximoDoRelogio = TimeSpan.FromMilliseconds(250);

    private readonly Application _app;
    private readonly InstanciaUnica _instancia;
    private readonly OpcoesDaAplicacao _opcoes;

    private readonly ArbitroDeEventosDoSistema _eventosDoSistema;

    private readonly AgendaDaReleitura _releitura;

    // Origem do relógio monotônico da _releitura.
    private readonly long _origemDoRelogio = Stopwatch.GetTimestamp();

    private readonly DispatcherTimer _repetirBandeja;
    private readonly Dictionary<Evento, string> _motivosDoNucleo = new(ReferenceEqualityComparer.Instance);
    private readonly ArbitroDeGestos _arbitro = new();

    // Só com --diagnostico: ms de cada movimento do arraste até a janela no lugar.
    private readonly List<double> _latenciasDoArraste = [];

    private JanelaDeServico? _servico;
    private JanelaPersonagem? _personagem;

    // Nulos se a assinatura do observador falhar; aí o modo tela cheia não age.
    private ObservadorDeTelaCheia? _observadorDeTelaCheia;
    private AgendaDaTelaCheia? _telaCheia;
    private Bandeja? _bandeja;
    private Nucleo? _nucleo;
    private Topologia _topologia = null!;

    // Criada no começo de Iniciar, antes de qualquer efeito do núcleo.
    private AgendaDeGravacao? _gravacao;

    // Descarrega uma vez só, sem reentrar.
    private bool _descarregouNoErro;

    // Sempre atualizada junto com _topologia. Guarda o nome GDI de cada chave, que só vai
    // pro log. A leitura da barra recriada não passa por aqui (só escolhe o tamanho do ícone).
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

    // Movida no arraste sem registrar a posição no log.
    private bool _posicaoSemRegistro;

    // Inscrito no CompositionTarget.Rendering.
    private bool _relogioLigado;

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
        // Limitação aceita do WPF: ele encerra no WM_QUERYENDSESSION se ninguém cancelar; se
        // outro app cancelar o desligamento depois, o Buzzy já fechou.
        _app.SessionEnding += (_, _) => Enviar(new SessionEnding(), "fim de sessão");
        _app.DispatcherUnhandledException += (_, e) =>
        {
            // Só tipo e código: a mensagem pode trazer um caminho com o nome do usuário.
            Diagnostico.Evento("ERRO", ("tipo", e.Exception.GetType().Name), ("hresult", $"0x{e.Exception.HResult:X8}"));
            DescarregarNoErro();
            _bandeja?.Remover();
        };

        // Configurações primeiro: arquivo escolhido pela regra da pasta (perfil de teste,
        // persistência desligada ou pasta do Buzzy), lido uma vez, sem derrubar a partida.
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

        // O tamanho vem do arquivo e fica fixo até fechar o Buzzy.
        _escalaEmVigor = _gravacao.Lidas.Preferencias.Escala;
        TamanhoDip tamanhoDoPersonagem = ConfiguracaoDoNucleo.TamanhoDoPersonagem(_escalaEmVigor);
        Diagnostico.Evento("ESCALA", ("passo", _escalaEmVigor), ("dip", tamanhoDoPersonagem.Largura));
        _personagem = new JanelaPersonagem(tamanhoDoPersonagem);
        // Registro de verdade só sem perfil de teste; a partida nunca lê o registro.
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

        // Cria sem mostrar: os efeitos do núcleo põem a janela no lugar (px físicos) antes do Show.
        new WindowInteropHelper(_personagem).EnsureHandle();
        Diagnostico.Evento("JANELA", ("hwnd", _personagem.Hwnd));
        IniciarItens();
        AplicarSempreNoTopo(_gravacao.Lidas.Preferencias.SempreNoTopo, "partida");

        ulong semente = _opcoes.Semente ?? unchecked((ulong)Environment.TickCount64);
        _nucleo = new Nucleo(ConfiguracaoDoNucleo.DoAplicativo(_escalaEmVigor, EdicaoDoBuild.Atual), semente);
        Diagnostico.Evento("NUCLEO", ("semente", semente), ("pausado", _opcoes.MovimentoPausado ? "sim" : "nao"));
        // A carga leva o que foi lido: posição salva (com a tela do monitor de então, pro núcleo
        // restaurar em cascata), esconderijo, marca de preso e preferências.
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

    // Cada gesto é aplicado dentro da mesma mensagem: no arraste, a janela já está no
    // lugar novo antes de a mensagem terminar.
    private void AoPonteiro(EventoDePonteiro evento)
    {
        if (_encerrando || _nucleo is null || _personagem is null) return;
        long recebido = Stopwatch.GetTimestamp();
        Arbitragem arbitragem = _arbitro.Receber(evento);

        // Captura no botão esquerdo pressionado, solta no fim do gesto.
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

    // Só com --diagnostico: resumo da latência do arraste, sem uma linha por movimento.
    // A posição final já foi pro log no fim do processamento do gesto.
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
        // Explorer reiniciou ou o DPI do principal mudou (a Shell manda a mesma mensagem): o
        // ícone volta na hora. Esta leitura só escolhe o tamanho do ícone; a topologia vai pro
        // núcleo pela releitura agrupada, e a da raiz só muda junto com a dele.
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

    // WM_DISPLAYCHANGE, WM_SETTINGCHANGE (SPI_SETWORKAREA), WM_DPICHANGED do personagem ou
    // TaskbarCreated. Vai pro log só o tipo (serve pra calibrar o agrupamento). O
    // WM_DPICHANGED da própria janela além do limite é ignorado sem avisar o árbitro.
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

    // Eventos de sessão (WTS) e energia. Na suspensão, grava o pendente na hora: com o
    // Buzzy escondido o núcleo não pede gravação, e o atraso de 2 s só venceria depois
    // de acordar, ou nunca.
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

    // Leitura incoerente (troca de modo em andamento) mantém a anterior; a agenda tenta de
    // novo. Devolve se publicou.
    private bool RelerAgrupada(PedidoDeReleitura pedido)
    {
        if (_encerrando) return false;
        string motivos = pedido.Motivos;

        // A última tentativa é parcial: deixa de fora o monitor que não dá pra ler inteiro,
        // em vez de ficar pra sempre com uma topologia que não existe mais.
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
        // Jogo que troca o modo de vídeo muda a tela do monitor, então reavalia a tela cheia.
        _telaCheia?.Sinalizar("topologia");
        return true;
    }

    // A janela já voltou ao normal sozinha quando isto roda.
    // Já escondido: nunca vira CMD_HIDE, senão a ocultação da sessão/suspensão viraria
    // ocultação do usuário e o desbloqueio não mostraria mais o Buzzy.
    // Minimizado pelo usuário: esconde. Pelo sistema (troca de monitores): não esconde;
    // a releitura reafirma o lugar, e se não houver uma pendente, pede.
    private void AoMinimizarPersonagem()
    {
        if (_encerrando || _personagem is null) return;
        bool pendente = _releitura.ReleituraPendente;
        if (!_visivel)
        {
            Diagnostico.Evento("MINIMIZADO", ("janela", "personagem"), ("escondido", "sim"), ("releituraPendente", pendente ? "sim" : "nao"));
            // Voltou ao normal visível, mas o WPF acha que está escondida e um Hide() sozinho não
            // faz nada. Show() + Hide() esconde de novo e deixa o WPF em dia.
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

    // Com "Minimizar janelas quando um monitor for desconectado", o Windows minimiza as
    // janelas do monitor que sai. Conta como do sistema se há releitura pendente ou se a
    // leitura de agora difere da publicada ou é incoerente (nula).
    internal static bool MinimizadaPeloSistema(bool releituraPendente, Topologia publicada, Topologia? lidaAgora)
    {
        ArgumentNullException.ThrowIfNull(publicada);
        return releituraPendente || lidaAgora is null || !lidaAgora.MesmaConfiguracao(publicada);
    }

    // Monitores podem ter mudado com o Buzzy escondido, então relê antes de mostrar.
    // Não passa pela agenda: não arma a conferência tardia, não reafirma o lugar (o
    // CMD_SHOW faz isso) e não solta os eventos retidos no árbitro. Incoerente, fica a
    // anterior, sem nova tentativa.
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

    // 1,5 s depois da releitura: com "Lembrar locais das janelas", o Windows pode devolver
    // uma janela ao monitor reconectado depois que a gente já releu.
    private void ReafirmarDepoisDaReleitura()
    {
        if (_encerrando) return;
        ReafirmarLugares(MotivoDaReafirmacaoTardia);
    }

    // Pra linha TOPOLOGIA. A chave é um resumo opaco: nem o caminho do dispositivo nem o
    // nome do monitor vão pro log.
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

    // Só o lugar: a ordem Z nunca é reafirmada por timer.
    private void ReafirmarLugares(string motivo)
    {
        ReafirmarLugarDaJanela(motivo);
        if (_visivel && !_encerrando) _itens?.ReafirmarLugares(motivo);
    }

    // Quem manda na posição é o núcleo: se o Windows moveu a janela (troca de monitor ou
    // DPI) ou o sprite ficou em outro DPI, reaplica. Nunca no meio de um gesto; com o
    // botão pressionado, a validação é ao soltar.
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

    // Até 5 leituras com 200 ms entre elas; no fim, uma parcial que deixa de fora o
    // monitor ilegível (senão um monitor com defeito impedia o Buzzy de abrir).
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
        // Logar cada passo pesaria no movimento; a posição vai pro log quando ele para.
        if (EmMovimentoOuArraste()) _posicaoSemRegistro = true;
        else RegistrarPosicao(p);
    }

    private bool EmMovimentoOuArraste()
        => _nucleo?.Estado.Estado is { } e && (e == Estado.Dragging || e.EmMovimento());

    // Só com --diagnostico. Os pontos de teste (opaco e transparente, em coordenadas de
    // tela) são do quadro "parado" e servem pras verificações clicarem no personagem.
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
        // O personagem acabou de subir; os itens voltam pra logo abaixo dele.
        _itens?.ReordenarAbaixoDoPersonagem();
        if (jaEstavaVisivel) RegistrarVisibilidade(true, motivo);
    }

    private void ExibirMenuDoDesktop(PontoPx ponto, string origem, bool peloTeclado)
    {
        if (_encerrando) return;
        // A escolha usa o estado de quando o menu abriu, que é o texto que a pessoa leu; o
        // laço modal do menu continua despachando operações enquanto ele está aberto.
        bool visivelAoAbrir = _visivel;
        ModeloDoMenu modelo = MenuNativo.ModeloAoAbrir(_nucleo, visivelAoAbrir, SystemParameters.HighContrast);
        bool pausadoAoAbrir = modelo.MovimentoPausado;
        // O Windows não escala o bitmap de item de menu, então usamos o DPI do monitor onde ele abre.
        int dpi = MenuNativo.DpiAoAbrir(_topologia, ponto);
        Diagnostico.Evento("MENU", ("aberto", origem), ("ponto", ponto), ("peloTeclado", peloTeclado ? "sim" : "nao"));
        // A curiosidade espera o menu fechar e sai depois do comando. O comando roda com o
        // dono do menu ainda vivo.
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
                // Uma das 14 caras ou "Automática" (nula). Vai pro settings.json com atraso.
                Enviar(new CmdSetDominantEmotion(escolha.Emocao), "menu");
                break;
            case ComandoDoMenu.Item when escolha.Item is { } item:
                // Nasce ao lado do personagem e cai; o núcleo decide onde.
                Enviar(new CmdSummonItem(item), "menu");
                break;
            case ComandoDoMenu.RecolherItens:
                Enviar(new CmdClearItems(), "menu");
                break;
            case ComandoDoMenu.ConteudoAdulto:
                // Inverte o que estava marcado quando o menu abriu; desligar tira o conteúdo adulto da tela.
                Enviar(new CmdSetAdultContent(!modelo.ConteudoAdulto), "menu");
                break;
            case ComandoDoMenu.ModoTelaCheia:
                // "Desviar da tela cheia", invertendo o que estava marcado. Desligado, ele volta pra onde
                // estava antes; ligado com ele num monitor ocupado, ele sai de lá.
                Enviar(new CmdSetFullscreenMode(!modelo.ModoTelaCheia), "menu");
                break;
            case ComandoDoMenu.Energia:
                // O núcleo pausa a autonomia e pede o painel (AbrirPainelDeEnergia).
                Enviar(new EnergyPanelOpen(), "menu");
                break;
            case ComandoDoMenu.Configuracoes:
                Enviar(new CmdOpenSettings(), "menu");
                break;
            case ComandoDoMenu.Nenhum when peloTeclado:
                // Menu da bandeja aberto e cancelado pelo teclado: o foco volta pra área de
                // notificação (ver NIM_SETFOCUS). Com o mouse não, porque o clique fora pode ter
                // ativado outro app.
                _bandeja?.DevolverFoco();
                break;
        }
    }

    private void Enviar(Evento evento, string motivo)
    {
        if (!Enfileirar(evento, motivo)) return;
        ProcessarFilaDoNucleo();
    }

    // Sem processar, pra juntar os passos do relógio num lote.
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
                // Estado antes de cada evento, pra linha PARANOIA (só com --diagnostico).
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
                    // Vários passos no mesmo quadro: só a última posição antes do próximo
                    // mostrar/esconder vai pra janela. Um movimento por quadro.
                    if (efeito is MoverJanela && MovimentacaoPosterior(efeitos, i)) continue;
                    // Mesma coisa pra cada janela de item.
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
        if (Diagnostico.Ligado && !_encerrando) _itens?.RegistrarPousos();
        // Gravação que disparou no meio de um gesto sai agora, se o gesto acabou.
        if (!_encerrando) _gravacao?.ConferirFimDoGesto();
    }

    // Botão pressionado, arraste ou item na mão. A gravação com atraso espera, pra E/S
    // não cair no meio do gesto.
    private bool GestoDoUsuarioEmCurso()
        => _nucleo?.Estado is { } s && (s.Estado is Estado.Pressed or Estado.Dragging || s.Atento);

    // Outro MoverJanela adiante, sem mostrar/esconder no meio.
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

    // Escolhe o quadro pelo retrato do núcleo; só redesenha quando o quadro ou o DPI mudam.
    private void AtualizarSprite()
    {
        if (_personagem is null || _nucleo is null || _encerrando || _posicionamento is null) return;
        Retrato retrato = _nucleo.Retrato;
        if (!retrato.Estado.Visivel()) return;
        EstadoDoMovimento movimento = _nucleo.Estado.Movimento;
        // A contagem da pose recomeça a cada troca de estado e a cada quique de borracha,
        // que continua em JUMPING (toon force).
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
        // Uma linha por quadro desenhado de verdade. Com o cache cheio a contagem do cache
        // não muda (um entra, outro sai), então compara os desenhados.
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

    // Só quando o movimento para, nunca por timer. O cache de quadros é limitado a 16 MiB.
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
                // O WPF pode reaplicar a posição inicial no Show(), então aplica de novo.
                AplicarNaJanela(_posicionamento);
                // Reapareceu no topo: os itens ficam logo abaixo.
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
                // O núcleo encerrou o gesto (esconder ou sair no meio): esquece e solta o mouse
                // sem gerar DRAG_CANCEL.
                _arbitro.Reiniciar();
                _latenciasDoArraste.Clear();
                _personagem?.SoltarCaptura();
                break;

            case AbrirMenu pedidoMenu:
                // O laço modal do menu roda depois do processamento, não dentro: eventos que chegam
                // com o menu aberto (relógio, agenda, bandeja) são aplicados na hora.
                Adiar(() => ExibirMenuDoDesktop(pedidoMenu.Ponto, "personagem", peloTeclado: false));
                break;

            case AgendarCuriosidade or CancelarCuriosidade or PedirVaoDaJanelaAtiva:
                // Aplicacao.Curiosidade.cs
                ExecutarEfeitoDaCuriosidade(efeito);
                break;

            case MostrarItem or MoverItem or EsconderItem or RemoverItem or LiberarCapturaDoItem or AgendarOnda or CancelarOnda:
                // Aplicacao.Itens.cs
                ExecutarEfeitoDoTamagotchi(efeito);
                break;

            case Encerrar:
                EncerrarAplicacao(motivo);
                break;

            case GravarPreferencias preferencias:
                // Primeiro as janelas abertas acompanham o que o núcleo gravou; depois, o disco.
                AtualizarJanelasDasPreferencias(preferencias.Preferencias);
                _gravacao?.Pedir(efeito, evento);
                break;

            case GravarPosicao:
                // Os valores gravados vêm do efeito, nunca do estado atual.
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

    // O WPF só gera quadros enquanto alguém está inscrito no Rendering; sem nada se
    // mexendo, não há inscrição nem quadros.
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

    // Aplica num lote só os passos acumulados desde o último quadro.
    private void AoQuadroDoCompositor(object? remetente, EventArgs e)
    {
        if (_nucleo is null || !_relogioLigado || _encerrando) return;
        long agora = Stopwatch.GetTimestamp();
        _tempoAcumulado += Stopwatch.GetElapsedTime(_ultimaMarcacaoRelogio, agora);
        _ultimaMarcacaoRelogio = agora;

        TimeSpan passo = TimeSpan.FromSeconds(1d / _nucleo.Configuracao.PassosPorSegundo);
        // Depois de a thread travar (chamada lenta ao Windows, depurador), descarta o excesso
        // em vez de soltar uma rajada de passos. O passo continua fixo.
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

        // Grava o pendente antes de desmontar qualquer coisa e para a agenda: nenhum disparo
        // de gravação sobra depois daqui.
        _gravacao?.Descarregar("encerrar");
        _gravacao?.Parar();

        // Fecha um menu aberto (ex.: fim de sessão com o menu na tela) antes de destruir as janelas.
        Win32.EndMenu();
        _personagem?.SoltarCaptura();
        _releitura.Parar();
        PararTelaCheia();
        _repetirBandeja.Stop();
        PararRelogio();
        CancelarDecisaoAutonoma();
        // O núcleo não manda efeito de item ao sair; as janelas fecham por aqui.
        EncerrarItens();
        FecharConfiguracoes();
        _bandeja?.Dispose();
        _servico?.Dispose();
        _personagem?.Close();
        _app.Shutdown(CodigosDeSaida.Normal);
    }

    // ------------------------------------------------------------------ tela cheia

    // Avalia logo na partida, pra pegar um jogo que já estava em tela cheia. O núcleo
    // recebe os monitores ocupados mesmo com o modo desligado, pra ligar pelo menu agir na hora.
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

    // No log, nada da janela (nem retângulo, nem de quem é): só chaves opacas e contagens.
    // No fim da tela cheia, sobe o personagem de novo: a janela em tela cheia foi ativada
    // depois dele e pode ter ficado por cima, deixando-o escondido atrás dela.
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
        // Sem "sempre no topo", não reordena.
        if (!mudanca.Ocupados.Vazio || _encerrando || !_visivel || _personagem is null || !_personagem.SempreNoTopo) return;
        _personagem.AoTopoDaFaixa();
        _itens?.ReordenarAbaixoDoPersonagem();
    }

    // O observador continua referenciado até o fim do processo: um evento que já estava
    // na fila ainda chama o delegado dele.
    private void PararTelaCheia()
    {
        _telaCheia?.Parar();
        PararCuriosidade();
        if (_observadorDeTelaCheia is not { Ligado: true } observador) return;
        // Dos vãos, só a contagem vai pro log.
        Diagnostico.Evento("TELA_CHEIA", ("fim", "sim"), ("eventosPrimeiroPlano", observador.EventosDePrimeiroPlano), ("eventosGeometria", observador.EventosDeGeometria),
            ("vaosPedidos", _telaCheia?.VaosPedidos ?? 0));
        observador.Dispose();
    }

    // ------------------------------------------------------------------ apoio

    // Uma vez só, sem lançar e sem reentrar: o erro pode ter vindo da própria gravação.
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

    // Roda depois de a mensagem atual terminar.
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

    // Pro menu, quando a notificação não traz coordenadas.
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
