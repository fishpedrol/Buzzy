using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A agenda de gravação da raiz (Fase 5, passo P7; DEC-029, item 9; ARCHITECTURE.md 2.12), sem janela: um agendador falso
/// no lugar do DispatcherTimer (cada agendamento é um disparo único, que o teste dispara ou vê cancelado) e uma pasta
/// temporária própria, apagada no fim. Nenhum teste lê nem grava em %LOCALAPPDATA%\Buzzy. O agendador de verdade
/// (<see cref="AgendaDeGravacao.AgendarNoDispatcher"/>) tem um teste à parte, com o laço de mensagens desta thread.
/// </summary>
internal sealed class AgendaDeGravacaoTestes : IDisposable
{
    private static readonly PosicaoDoPersonagem NoChao = new(@"mon:0123456789abcdef", 0.85, 1, new PontoPx(1632, 1032)) { TelaDoMonitor = new RetanguloPx(0, 0, 1920, 1080) };
    private static readonly PosicaoDoPersonagem NaParede = new(@"mon:0123456789abcdef", 0.966667, 0.5, new PontoPx(1856, 516)) { TelaDoMonitor = new RetanguloPx(0, 0, 1920, 1080) };

    private readonly string _raiz = Directory.CreateTempSubdirectory("buzzy-agenda-").FullName;
    private readonly List<(string Campo, object? Valor)[]> _log = [];
    private readonly AgendadorFalso _agendador = new();
    private bool _gesto;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"         limpeza: a pasta temporária {_raiz} ficou para o sistema apagar ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// Um agendador de disparos únicos para os testes: cada agendamento fica pendente até o teste dispará-lo, ou até ser
    /// cancelado. Guarda a espera de cada agendamento, na ordem.
    /// </summary>
    private sealed class AgendadorFalso
    {
        private sealed class Agendamento(TimeSpan espera, Action acao)
        {
            internal TimeSpan Espera { get; } = espera;
            internal Action Acao { get; } = acao;
            internal bool Encerrado { get; set; }
        }

        private readonly List<Agendamento> _todos = [];

        internal Action Agendar(TimeSpan espera, Action acao)
        {
            var a = new Agendamento(espera, acao);
            _todos.Add(a);
            return () => a.Encerrado = true;
        }

        /// <summary>As esperas de todos os agendamentos, na ordem.</summary>
        internal IReadOnlyList<TimeSpan> Esperas => [.. _todos.Select(a => a.Espera)];

        /// <summary>Quantos disparos estão pendentes (nem disparados nem cancelados).</summary>
        internal int Pendentes => _todos.Count(a => !a.Encerrado);

        /// <summary>A espera do único disparo pendente; nula sem nenhum.</summary>
        internal TimeSpan? Pendente
        {
            get
            {
                Agendamento[] vivos = [.. _todos.Where(a => !a.Encerrado)];
                Afirmar.Verdadeiro(vivos.Length <= 1, $"no máximo um disparo pendente (há {vivos.Length})");
                return vivos.Length == 1 ? vivos[0].Espera : null;
            }
        }

        /// <summary>Dispara o único pendente, como o temporizador faria.</summary>
        internal void Disparar()
        {
            Agendamento[] vivos = [.. _todos.Where(a => !a.Encerrado)];
            Afirmar.Igual(1, vivos.Length, "um disparo pendente para disparar");
            vivos[0].Encerrado = true;
            vivos[0].Acao();
        }
    }

    // ------------------------------------------------------------------ apoio

    private string NovaPasta() => Path.Combine(_raiz, $"p{Directory.GetDirectories(_raiz).Length + Directory.GetFiles(_raiz).Length}-{Guid.NewGuid():N}");

    private AgendaDeGravacao Nova(ArquivoDeConfiguracoes? arquivo, bool persistenciaDesligada = false)
        => AgendaDeGravacao.NaPartida(arquivo, persistenciaDesligada, perfil: true, () => _gesto, _agendador.Agendar, _log.Add);

    private static ConfiguracoesSalvas? NoDisco(string pasta)
    {
        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        if (!File.Exists(principal)) return null;
        LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(File.ReadAllBytes(principal));
        Afirmar.Igual(SituacaoDaLeitura.Valida, lida.Situacao, "o principal gravado é válido");
        return lida.Configuracoes;
    }

    private static GravarPosicao Gravar(PosicaoDoPersonagem p, LadoDoEsconderijo borda = LadoDoEsconderijo.Nenhum, bool preso = false)
        => new(p) { Esconderijo = borda, PresoPeloUsuario = preso };

    private IEnumerable<string> Linhas(string campo) => _log.Where(c => c.Any(x => x.Campo == campo)).Select(c => string.Join("|", c.Select(x => $"{x.Campo}={x.Valor}")));

    private string? Valor((string Campo, object? Valor)[] linha, string campo) => linha.FirstOrDefault(x => x.Campo == campo).Valor?.ToString();

    // ------------------------------------------------------------------ partida

    // A partida lê o arquivo desta execução uma vez: as configurações lidas (posição, postura e preferências) viram a
    // carga e o que se deseja no disco. Sem arquivo (persistência desligada ou sem pasta), as padrão, e nada é lido.
    [Teste]
    public void NaPartida_LeUmaVezOArquivoDaExecucao_ESemArquivoUsaOsPadroes()
    {
        string pasta = NovaPasta();
        var salvas = new ConfiguracoesSalvas(NaParede, Preferencias.Padrao with { EmocaoDominante = Expressao.Feliz, AtravessarMonitores = false })
        {
            Esconderijo = LadoDoEsconderijo.Direita,
            PresoPeloUsuario = true,
        };
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(salvas).Gravou, "arquivo preparado");

        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        Afirmar.Igual(salvas, agenda.Lidas, "o que a partida leu");
        Afirmar.Igual(salvas, agenda.Desejadas, "o desejado começa com o lido");
        Afirmar.Verdadeiro(agenda.Ligada, "gravação ligada");
        Afirmar.Falso(agenda.Pendente, "o disco já tem o lido: nada pendente");
        string partida = Afirmar.NaoNulo(Linhas("lido").SingleOrDefault(), "uma linha CONFIG na partida");
        Afirmar.Igual("lido=principal|principal=Valido|reserva=-|versao=atual|avisos=0|tentativas=1|gravacao=liberada|pasta=perfil", partida, "a linha da partida");

        Loaded carga = agenda.Lidas.ParaACarga(TopologiaDeUmMonitor());
        Afirmar.Igual((NaParede, LadoDoEsconderijo.Direita, true), (carga.PosicaoSalva, carga.Esconderijo, carga.PresoPeloUsuario), "a carga leva a posição com a tela e a postura");
        Afirmar.Igual(salvas.Preferencias, carga.Preferencias, "e as preferências lidas, com a emoção e a travessia");

        _log.Clear();
        AgendaDeGravacao desligada = Nova(null, persistenciaDesligada: true);
        Afirmar.Igual(ConfiguracoesSalvas.Padrao, desligada.Lidas, "sem arquivo, os padrões");
        Afirmar.Falso(desligada.Ligada, "sem arquivo, desligada");
        Afirmar.Sequencia(["lido=desligado|motivo=opcao"], Linhas("lido"), "persistência desligada pela linha de comando");
        _log.Clear();
        _ = Nova(null);
        Afirmar.Sequencia(["lido=desligado|motivo=pasta"], Linhas("lido"), "sem a pasta");
    }

    // A leitura da partida não derruba o Buzzy: qualquer exceção, mesmo a que não é de E/S, desliga a persistência nesta
    // execução (nada é gravado depois), e o log leva só o tipo e o código, nunca a mensagem.
    [Teste]
    public void NaPartida_ExcecaoNaLeitura_DesligaAPersistenciaSoComTipoECodigo()
    {
        string pasta = NovaPasta();
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        AgendaDeGravacao agenda = AgendaDeGravacao.NaPartida(arquivo, persistenciaDesligada: false, perfil: true, () => false, _agendador.Agendar, _log.Add,
            ler: _ => throw new NotSupportedException($"caminho {pasta} não suportado"));
        Afirmar.Igual(ConfiguracoesSalvas.Padrao, agenda.Lidas, "os padrões");
        Afirmar.Falso(agenda.Ligada, "desligada nesta execução");
        string linha = Afirmar.NaoNulo(Linhas("lido").SingleOrDefault(), "a linha da partida");
        Afirmar.Igual($"lido=desligado|motivo=erro|erro=NotSupportedException 0x{new NotSupportedException().HResult:X8}", linha, "só o tipo e o código");

        agenda.Pedir(Gravar(NoChao), new CmdExit());
        Afirmar.Igual(0, _agendador.Pendentes, "nada agendado");
        Afirmar.Falso(Directory.Exists(pasta), "nada gravado: a pasta nem foi criada");
    }

    // ------------------------------------------------------------------ quando gravar

    // Com atraso: nada vai ao disco antes do disparo de 2 s; cada pedido reinicia a espera (um disparo pendente só), e o
    // disparo grava o último pedido. Gravado, nada fica pendente nem agendado.
    [Teste]
    public void ComAtraso_SoGravaNoDisparo_EReiniciaACadaPedido()
    {
        string pasta = NovaPasta();
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        agenda.Pedir(Gravar(NoChao), new DragEnd(default));
        Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.Atraso, _agendador.Pendente, "um disparo de 2 s");
        Afirmar.Nulo(NoDisco(pasta), "nada no disco antes do disparo");

        agenda.Pedir(Gravar(NaParede, preso: true), new DragEnd(default));
        Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.Atraso, _agendador.Pendente, "o pedido novo reinicia a espera: ainda um disparo só");
        Afirmar.Sequencia([PoliticaDeGravacao.Atraso, PoliticaDeGravacao.Atraso], _agendador.Esperas, "dois agendamentos, o primeiro cancelado");

        _agendador.Disparar();
        Afirmar.Igual(new ConfiguracoesSalvas(NaParede, Preferencias.Padrao) { PresoPeloUsuario = true }, NoDisco(pasta), "o disparo grava o último pedido, com a postura");
        Afirmar.Falso(agenda.Pendente, "gravado, nada pendente");
        Afirmar.Igual(0, _agendador.Pendentes, "e nada agendado");
        string gravado = Afirmar.NaoNulo(Linhas("gravado").LastOrDefault(), "a linha da gravação");
        Afirmar.Contem("gravado=sim|motivo=atraso|", gravado);
    }

    // Na hora: SUSPENDING, SESSION_ENDING, CMD_EXIT e SESSION_LOCKED gravam no próprio pedido, e o disparo pendente é
    // cancelado. Os outros eventos esperam o atraso.
    [Teste]
    public void Imediato_GravaNaHora_ECancelaODisparo()
    {
        foreach (Evento evento in new Evento[] { new CmdExit(), new SessionEnding(), new Suspending(), new SessionLocked() })
        {
            string pasta = NovaPasta();
            AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
            agenda.Pedir(Gravar(NoChao), new CmdHide());
            Afirmar.Igual(1, _agendador.Pendentes, $"{evento.GetType().Name}: esconder pela bandeja agenda");
            agenda.Pedir(Gravar(NaParede, LadoDoEsconderijo.Direita), evento);
            Afirmar.Igual(new ConfiguracoesSalvas(NaParede, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Direita }, NoDisco(pasta), $"{evento.GetType().Name}: gravado na hora");
            Afirmar.Igual(0, _agendador.Pendentes, $"{evento.GetType().Name}: o disparo pendente foi cancelado");
            Afirmar.Contem($"gravado=sim|motivo={evento.GetType().Name}|", Linhas("gravado").Last());
        }
    }

    // Sem gravação inútil: o que já está no disco (lido na partida ou gravado depois) não é gravado de novo, nem agendado.
    // As preferências vêm só do GravarPreferencias: o GravarPosicao troca a posição e a postura e mantém as preferências.
    [Teste]
    public void IgualAoDisco_NaoGrava_EOPedidoSoTrocaOQueOEfeitoTraz()
    {
        string pasta = NovaPasta();
        var lidas = new ConfiguracoesSalvas(NoChao, Preferencias.Padrao with { EmocaoDominante = Expressao.Curioso });
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(lidas).Gravou, "arquivo preparado");
        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        DateTime antes = File.GetLastWriteTimeUtc(principal);
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));

        agenda.Pedir(Gravar(NoChao), new CmdExit());
        Afirmar.Igual(0, _agendador.Pendentes, "nada agendado");
        Afirmar.Igual(antes, File.GetLastWriteTimeUtc(principal), "o arquivo não foi regravado");
        Afirmar.Contem("gravado=sem mudanca|motivo=CmdExit", Linhas("gravado").Last());
        Afirmar.Igual(lidas, agenda.Desejadas, "a posição do efeito, com as preferências lidas");

        agenda.Pedir(new GravarPreferencias(lidas.Preferencias with { EmocaoDominante = Expressao.Feliz }), new CmdSetDominantEmotion(Expressao.Feliz));
        Afirmar.Igual(lidas with { Preferencias = lidas.Preferencias with { EmocaoDominante = Expressao.Feliz } }, agenda.Desejadas, "as preferências do efeito, com a mesma posição");
        _agendador.Disparar();
        agenda.Pedir(Gravar(NoChao), new DragEnd(default));
        Afirmar.Igual(0, _agendador.Pendentes, "depois de gravado, o mesmo conteúdo não é agendado");
        Afirmar.Igual<Expressao?>(Expressao.Feliz, NoDisco(pasta)?.Preferencias.EmocaoDominante, "a emoção foi para o disco");

        Afirmar.Lanca<ArgumentException>(() => agenda.Pedir(new Encerrar(), new CmdExit()), "outro efeito");
    }

    // L5 da crítica: com o gesto do usuário em curso (o botão pressionado, um arraste, um item na mão), o disparo não
    // grava nem rearma a espera a cada 2 s (seria periódico com o ClickLock): fica esperando, e o fim do gesto grava. Um
    // pedido novo no meio recomeça o atraso.
    [Teste]
    public void DisparoDuranteOGesto_EsperaOFim_SemRearmar()
    {
        string pasta = NovaPasta();
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        agenda.Pedir(Gravar(NoChao), new DragEnd(default));
        _gesto = true;
        _agendador.Disparar();
        Afirmar.Nulo(NoDisco(pasta), "com o gesto em curso, não grava");
        Afirmar.Igual(0, _agendador.Pendentes, "e não rearma: nenhum disparo periódico");
        Afirmar.Verdadeiro(agenda.EsperandoOGesto, "espera o fim do gesto");
        agenda.ConferirFimDoGesto();
        Afirmar.Nulo(NoDisco(pasta), "o gesto continua: nada");

        _gesto = false;
        agenda.ConferirFimDoGesto();
        Afirmar.Igual(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao), NoDisco(pasta), "o fim do gesto grava");
        Afirmar.Falso(agenda.EsperandoOGesto, "não espera mais");
        Afirmar.Contem("gravado=sim|motivo=fimDoGesto|", Linhas("gravado").Last());
        agenda.ConferirFimDoGesto();
        Afirmar.Igual(1, Linhas("gravado").Count(), "conferir de novo não grava de novo");

        // Um pedido no meio da espera recomeça o atraso.
        agenda.Pedir(Gravar(NaParede), new DragEnd(default));
        _gesto = true;
        _agendador.Disparar();
        Afirmar.Verdadeiro(agenda.EsperandoOGesto, "de novo esperando o fim do gesto");
        agenda.Pedir(Gravar(NaParede, preso: true), new DragCancel());
        Afirmar.Falso(agenda.EsperandoOGesto, "o pedido novo recomeça");
        Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.Atraso, _agendador.Pendente, "com o atraso de sempre");
    }

    // Falha de E/S (o principal preso por outro processo): novas tentativas únicas em 2, 10 e 60 s; depois, desiste até o
    // próximo pedido. O erro vai para o log só com o tipo e o código, e a espera seguinte, em ms.
    [Teste]
    public void Falhas_NovasTentativasEm2_10E60s_EDesiste()
    {
        string pasta = NovaPasta();
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao)).Gravou, "arquivo preparado");
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        agenda.Pedir(Gravar(NaParede, preso: true), new DragEnd(default));

        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        using (new FileStream(principal, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            foreach (TimeSpan espera in PoliticaDeGravacao.EsperasDeNovaTentativa)
            {
                _agendador.Disparar();
                Afirmar.Igual<TimeSpan?>(espera, _agendador.Pendente, $"nova tentativa em {espera.TotalSeconds} s");
                Afirmar.Contem($"novaTentativaMs={(long)espera.TotalMilliseconds}", Linhas("gravado").Last());
            }
            _agendador.Disparar();
            Afirmar.Igual(0, _agendador.Pendentes, "depois da terceira nova tentativa, desiste");
            Afirmar.Verdadeiro(agenda.Pendente, "o pedido continua pendente");
            string falha = Linhas("gravado").Last();
            Afirmar.Contem("gravado=nao|motivo=novaTentativa|", falha);
            Afirmar.Contem("erro=principalInacessivel", falha);
            Afirmar.Contem("novaTentativaMs=-", falha);
        }
        Afirmar.Igual(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao), NoDisco(pasta), "o principal ficou como estava");

        agenda.Pedir(Gravar(NaParede, preso: true), new DragEnd(default));
        _agendador.Disparar();
        Afirmar.Igual(new ConfiguracoesSalvas(NaParede, Preferencias.Padrao) { PresoPeloUsuario = true }, NoDisco(pasta), "o próximo pedido grava");
    }

    // Um pedido novo zera as falhas (revisão de correção do bloco P6-P9, achado 5, mutação P7c): a série 2, 10 e 60 s
    // recomeça do primeiro passo, em vez de continuar de onde a série do pedido anterior parou.
    [Teste]
    public void Falhas_UmPedidoNovoRecomecaASerieDeNovasTentativas()
    {
        string pasta = NovaPasta();
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao)).Gravou, "arquivo preparado");
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        using (new FileStream(principal, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            agenda.Pedir(Gravar(NaParede, preso: true), new DragEnd(default));
            _agendador.Disparar();
            _agendador.Disparar();
            Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.EsperasDeNovaTentativa[1], _agendador.Pendente, "a série andou: a segunda espera");
            agenda.Pedir(Gravar(NoChao, LadoDoEsconderijo.Baixo), new DragEnd(default));
            Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.Atraso, _agendador.Pendente, "o pedido novo, com o atraso de sempre");
            _agendador.Disparar();
            Afirmar.Igual<TimeSpan?>(PoliticaDeGravacao.EsperasDeNovaTentativa[0], _agendador.Pendente, "a série recomeça: a primeira espera, e não a terceira");
        }
        _agendador.Disparar();
        Afirmar.Igual(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Baixo }, NoDisco(pasta), "liberado, grava o último pedido");
    }

    // A versão do arquivo é um valor lido dele, que qualquer um edita: a linha CONFIG da partida, que só leva enums,
    // contagens e tempos (DEC-029, item 12), a leva como atual, anterior ou futura (revisão de segurança do bloco P6-P9,
    // achado 2).
    [Teste]
    public void LinhaConfigDaPartida_AVersaoDoArquivoVaiComoAtualAnteriorOuFutura()
    {
        int atual = EsquemaDeConfiguracoes.VersaoAtual;
        foreach ((int? versao, string esperado) in new (int?, string)[]
        {
            (null, "-"), (1, "anterior"), (atual - 1, "anterior"), (atual, "atual"), (atual + 1, "futura"), (int.MaxValue, "futura"), (0, "anterior"), (-7, "anterior"),
        })
        {
            var lida = new LeituraDoArquivo(ConfiguracoesSalvas.Padrao, OrigemDasConfiguracoes.Principal, EstadoDoArquivo.Valido, null, false, versao, 0, 1);
            Afirmar.Igual(esperado, AgendaDeGravacao.CamposDaLeitura(lida, perfil: true).Single(c => c.Campo == "versao").Valor?.ToString(), $"versão {versao}");
        }
    }

    // A migração no disco (DEC-038, item 3; DEC-029, item 9): um principal válido de versão anterior não conta como "já no
    // disco". Sem pedido, nada é gravado nem fica pendente; o primeiro pedido, mesmo com o conteúdo igual, grava na versão
    // atual, e a anterior vira a reserva, byte a byte. Na versão atual, o mesmo conteúdo não grava.
    [Teste]
    public void VersaoAnterior_SemPedidoNaoGrava_EOPrimeiroPedidoGravaNaAtual()
    {
        string pasta = NovaPasta();
        Directory.CreateDirectory(pasta);
        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal), reserva = Path.Combine(pasta, ArquivoDeConfiguracoes.NomeReserva);
        var conteudo = new ConfiguracoesSalvas(NoChao, Preferencias.Padrao);
        byte[] v4 = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(EsquemaDeConfiguracoes.Escrever(conteudo))
            .Replace("\"schemaVersion\": 5", "\"schemaVersion\": 4", StringComparison.Ordinal)
            .Replace(",\n    \"sempreNoTopo\": true,\n    \"escala\": \"media\"", "", StringComparison.Ordinal));
        File.WriteAllBytes(principal, v4);
        Afirmar.Igual((int?)4, EsquemaDeConfiguracoes.Ler(v4).Versao, "a amostra é v4");

        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        Afirmar.Igual(conteudo, agenda.Lidas, "lida com os padrões nos campos novos");
        Afirmar.Falso(agenda.Pendente, "sem pedido, nada pendente");
        Afirmar.Falso(agenda.Descarregar("encerrar"), "sem pedido, descarregar não grava");
        Afirmar.Sequencia(v4, File.ReadAllBytes(principal), "o v4 continua no disco");

        agenda.Pedir(Gravar(NoChao), new CmdExit());
        Afirmar.Verdadeiro(agenda.Pendente || File.ReadAllBytes(principal).Length != v4.Length, "o pedido com o mesmo conteúdo migra");
        agenda.Descarregar("encerrar");
        LeituraDasConfiguracoes migrado = EsquemaDeConfiguracoes.Ler(File.ReadAllBytes(principal));
        Afirmar.Igual(((int?)EsquemaDeConfiguracoes.VersaoAtual, conteudo), (migrado.Versao, migrado.Configuracoes), "gravado na versão atual, com os mesmos valores");
        Afirmar.Sequencia(v4, File.ReadAllBytes(reserva), "o v4 virou a reserva");

        // Na versão atual, o mesmo conteúdo não grava.
        AgendaDeGravacao depois = Nova(new ArquivoDeConfiguracoes(pasta));
        depois.Pedir(Gravar(NoChao), new CmdExit());
        Afirmar.Falso(depois.Pendente, "v5 com o mesmo conteúdo: nada a gravar");
    }

    // Descarregar (encerrar, erro não tratado): grava o pendente na hora, com as tentativas do caminho imediato, e cancela
    // o disparo; sem pendente, não toca no disco. Depois de parar, nada mais é agendado, mas descarregar ainda grava.
    [Teste]
    public void Descarregar_GravaOPendente_ESemPendenteNaoToca_EPararNaoAgendaMais()
    {
        string pasta = NovaPasta();
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        Afirmar.Falso(agenda.Descarregar("encerrar"), "nada pendente: nada gravado");
        Afirmar.Falso(Directory.Exists(pasta), "a pasta nem foi criada");

        agenda.Pedir(Gravar(NoChao, LadoDoEsconderijo.Baixo), new CmdHide());
        Afirmar.Verdadeiro(agenda.Descarregar("encerrar"), "descarregou");
        Afirmar.Igual(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao) { Esconderijo = LadoDoEsconderijo.Baixo }, NoDisco(pasta), "o pendente foi para o disco");
        Afirmar.Igual(0, _agendador.Pendentes, "o disparo foi cancelado");
        Afirmar.Contem("gravado=sim|motivo=encerrar|", Linhas("gravado").Last());
        DateTime gravadoEm = File.GetLastWriteTimeUtc(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal));
        Afirmar.Falso(agenda.Descarregar("encerrar"), "sem pendente, de novo nada");
        Afirmar.Igual(gravadoEm, File.GetLastWriteTimeUtc(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal)), "o arquivo não foi tocado");

        agenda.Parar();
        agenda.Pedir(Gravar(NaParede), new DragEnd(default));
        Afirmar.Igual(0, _agendador.Pendentes, "parada: nada agendado");
        Afirmar.Verdadeiro(agenda.Descarregar("erro"), "parada, descarregar ainda grava");
        Afirmar.Igual(NaParede, NoDisco(pasta)?.Posicao, "gravado");
    }

    // Vindo da reserva (o principal ilegível), o que está no disco não é o que vale: o primeiro pedido grava, mesmo com o
    // conteúdo da reserva, e recria o principal, guardando o ilegível como a cópia de diagnóstico. Sem pedido, nada.
    [Teste]
    public void DaReserva_OPrimeiroPedidoGravaERecriaOPrincipal()
    {
        string pasta = NovaPasta();
        var b = new ConfiguracoesSalvas(NaParede, Preferencias.Padrao) { PresoPeloUsuario = true };
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(b).Gravou, "B gravado");
        File.Move(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal), Path.Combine(pasta, ArquivoDeConfiguracoes.NomeReserva));
        File.WriteAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal), "{ lixo");

        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        Afirmar.Igual(b, agenda.Lidas, "vale a reserva");
        Afirmar.Contem("lido=reserva|principal=Ilegivel|reserva=Valido", Linhas("lido").Single());
        Afirmar.Falso(agenda.Descarregar("encerrar"), "sem pedido, nada é gravado");
        Afirmar.Igual("{ lixo", File.ReadAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal)), "o ilegível continua lá");

        agenda.Pedir(Gravar(NaParede, preso: true), new CmdExit());
        Afirmar.Igual(b, NoDisco(pasta), "o principal recriado com o mesmo conteúdo");
        Afirmar.Igual("{ lixo", File.ReadAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomeIlegivel)), "o ilegível virou a cópia de diagnóstico");
        Afirmar.Contem("copiaDeDiagnostico=sim", Linhas("gravado").Last());
    }

    // Versão futura no disco: a gravação fica bloqueada nesta execução; nada é agendado nem gravado por cima.
    [Teste]
    public void VersaoFutura_NadaAgendadoNemGravado()
    {
        string pasta = NovaPasta();
        Directory.CreateDirectory(pasta);
        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        byte[] futura = """{"schemaVersion": 6, "preferencias": {"energia": "alta"}}"""u8.ToArray();
        File.WriteAllBytes(principal, futura);
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        Afirmar.Falso(agenda.Ligada, "bloqueada");
        Afirmar.Contem("gravacao=bloqueada", Linhas("lido").Single());
        agenda.Pedir(Gravar(NoChao), new DragEnd(default));
        agenda.Pedir(Gravar(NoChao), new CmdExit());
        Afirmar.Falso(agenda.Descarregar("encerrar"), "nada gravado");
        Afirmar.Igual(0, _agendador.Pendentes, "nada agendado");
        Afirmar.Sequencia(futura, File.ReadAllBytes(principal), "o arquivo da versão mais nova fica como estava");
    }

    // Um defeito na gravação (exceção que não é de E/S) não derruba o Buzzy: a gravação fica desligada nesta execução, o
    // log leva só o tipo e o código, e nada mais é agendado.
    [Teste]
    public void DefeitoNaGravacao_DesligaSemLancar()
    {
        string pasta = NovaPasta();
        var arquivo = new ArquivoDeConfiguracoes(pasta, etapa => { if (etapa == EtapaDaGravacao.TemporarioEscrito) throw new FormatException("defeito simulado"); });
        AgendaDeGravacao agenda = Nova(arquivo);
        agenda.Pedir(Gravar(NoChao), new DragEnd(default));
        _agendador.Disparar();
        Afirmar.Falso(agenda.Ligada, "desligada depois do defeito");
        Afirmar.Igual($"gravado=nao|motivo=atraso|erro=FormatException 0x{new FormatException().HResult:X8}|gravacao=desligada", Linhas("gravado").Last(), "só o tipo e o código");
        agenda.Pedir(Gravar(NaParede), new CmdExit());
        Afirmar.Igual(0, _agendador.Pendentes, "nada agendado");
        Afirmar.Falso(agenda.Descarregar("erro"), "nada gravado");
    }

    // Os campos das linhas CONFIG são só enums, contagens e tempos (DEC-029, item 12): nenhum valor tem a pasta, a chave do
    // monitor, a tela, as frações, a âncora nem a emoção, nem o texto automático dos registros.
    [Teste]
    public void LinhasConfig_SoEnumsContagensETempos()
    {
        string pasta = NovaPasta();
        Afirmar.Verdadeiro(new ArquivoDeConfiguracoes(pasta).Gravar(new ConfiguracoesSalvas(NoChao, Preferencias.Padrao)).Gravou, "arquivo preparado");
        AgendaDeGravacao agenda = Nova(new ArquivoDeConfiguracoes(pasta));
        agenda.Pedir(Gravar(NaParede, LadoDoEsconderijo.Esquerda, preso: true), new DragEnd(default));
        _agendador.Disparar();
        agenda.Pedir(new GravarPreferencias(Preferencias.Padrao with { EmocaoDominante = Expressao.Travesso }), new CmdSetDominantEmotion(Expressao.Travesso));
        agenda.Pedir(Gravar(NoChao), new CmdExit());
        agenda.Descarregar("encerrar");

        // As frações vão com 6 casas: o tempo (ms) tem 3, e não pode coincidir com elas.
        string[] proibidos = [pasta, Path.GetFileName(pasta), "mon:", "0123456789abcdef", "1920", "1632", "1856", "0.966667", "Travesso", "travesso", "Esquerda", "esquerda", "ConfiguracoesSalvas", "Posicao"];
        Afirmar.Verdadeiro(_log.Count >= 6, $"linhas CONFIG registradas: {_log.Count}");
        foreach ((string campo, object? valor) in _log.SelectMany(l => l))
        {
            string texto = valor?.ToString() ?? "";
            foreach (string proibido in proibidos)
                Afirmar.Falso(texto.Contains(proibido, StringComparison.Ordinal), $"{campo}={texto} contém \"{proibido}\"");
            Afirmar.Verdadeiro(valor is null or string or int or long or double or bool or Enum, $"{campo}: só valores simples, nunca um registro ({valor?.GetType().Name})");
        }
    }

    // O agendador de verdade: um DispatcherTimer de disparo único, que dispara uma vez só e para; cancelado, não dispara.
    // Roda o laço de mensagens desta thread por um tempo curto, sem janela.
    [Teste]
    public void AgendarNoDispatcher_DisparaUmaVezSo_ECanceladoNaoDispara()
    {
        int disparos = 0;
        Action cancelar = AgendaDeGravacao.AgendarNoDispatcher(TimeSpan.FromMilliseconds(30), () => disparos++);
        Afirmar.Verdadeiro(BombearAte(() => disparos > 0, TimeSpan.FromSeconds(3)), "disparou");
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Igual(1, disparos, "uma vez só: o temporizador para no próprio disparo");
        cancelar();
        Afirmar.Igual(1, disparos, "cancelar depois do disparo não faz nada");

        int outros = 0;
        Action cancelarOutro = AgendaDeGravacao.AgendarNoDispatcher(TimeSpan.FromMilliseconds(30), () => outros++);
        cancelarOutro();
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Igual(0, outros, "cancelado, não dispara");
    }

    private static Topologia TopologiaDeUmMonitor()
        => new([new MonitorDoDesktop("mon:0123456789abcdef", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true)]);

    /// <summary>Roda o laço de mensagens desta thread por <paramref name="duracao"/>.</summary>
    private static void Bombear(TimeSpan duracao)
    {
        var quadro = new DispatcherFrame();
        var fim = new DispatcherTimer(DispatcherPriority.Normal) { Interval = duracao };
        fim.Tick += (_, _) =>
        {
            fim.Stop();
            quadro.Continue = false;
        };
        fim.Start();
        Dispatcher.PushFrame(quadro);
    }

    private static bool BombearAte(Func<bool> condicao, TimeSpan limite)
    {
        var relogio = Stopwatch.StartNew();
        while (!condicao())
        {
            if (relogio.Elapsed > limite) return false;
            Bombear(TimeSpan.FromMilliseconds(20));
        }
        return true;
    }
}
