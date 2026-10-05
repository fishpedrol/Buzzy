using System.Diagnostics;
using System.Windows.Threading;
using Buzzy.App.Plataforma;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// Quando a raiz grava o settings.json (Fase 5, passo P7; DEC-029, item 9; ARCHITECTURE.md 2.12), pela política do núcleo
/// (<see cref="PoliticaDeGravacao"/>), com o arquivo desta execução (<see cref="ArquivoDeConfiguracoes"/>):
/// <list type="bullet">
/// <item>a partida lê o arquivo uma vez (<see cref="NaPartida"/>), protegida contra qualquer exceção: em falha, a
/// persistência fica desligada nesta execução, e o log leva só o tipo e o código;</item>
/// <item>cada efeito GravarPosicao ou GravarPreferencias vira um pedido (<see cref="Pedir(Efeito, Evento)"/>) com o
/// conteúdo desejado; com o mesmo conteúdo do disco, nada é gravado nem agendado;</item>
/// <item>com atraso: um disparo único 2 s depois do último pedido, reiniciado a cada pedido. Se o disparo chega durante um
/// gesto do usuário, ele não rearma a espera, o que seria periódico com o ClickLock (L5 da crítica): o fim do gesto
/// grava (<see cref="ConferirFimDoGesto"/>);</item>
/// <item>na hora, quando o evento que gerou o pedido é de suspensão, fim de sessão, saída ou bloqueio, com as tentativas
/// do caminho imediato; e ao descarregar (<see cref="Descarregar"/>), no encerramento e no erro não tratado;</item>
/// <item>depois de uma falha de E/S, novas tentativas únicas em 2, 10 e 60 s; depois delas, só no próximo pedido;</item>
/// <item>um defeito (exceção que não é de E/S) desliga a gravação nesta execução, sem derrubar o Buzzy.</item>
/// </list>
/// Nada é periódico: só eventos do usuário ou do sistema geram pedidos (invariante 18), e todo agendamento é um disparo
/// único, parado no encerramento (<see cref="Parar"/>). A E/S é síncrona, na thread da interface: o arquivo tem menos de
/// 1 KiB, e o tempo vai para o log. As linhas CONFIG levam só enums, contagens e tempos (DEC-029, item 12): nunca a pasta,
/// valores do arquivo nem o texto automático dos registros, que imprime a chave e a tela. Só na thread da interface.
/// </summary>
internal sealed class AgendaDeGravacao
{
    private readonly ArquivoDeConfiguracoes? _arquivo;
    private readonly Func<bool> _gestoDoUsuarioEmCurso;
    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Action<(string Campo, object? Valor)[]> _registrar;

    /// <summary>O que se sabe estar no disco, normalizado; nulo quando não se sabe, e então a próxima necessidade grava.</summary>
    private ConfiguracoesSalvas? _noDisco;

    /// <summary>Cancela o disparo único pendente; nulo sem nenhum.</summary>
    private Action? _cancelar;

    /// <summary>Gravações com atraso que falharam seguidas: escolhem a espera da nova tentativa.</summary>
    private int _falhas;

    /// <summary>
    /// Se já houve algum pedido nesta execução. Sem nenhum, nada está pendente, mesmo sem saber o que o disco tem (a
    /// partida não grava nada sozinha: um descarregar sem pedido não recria o principal).
    /// </summary>
    private bool _houvePedido;

    private bool _parada;
    private bool _desligadaPorDefeito;

    private AgendaDeGravacao(ArquivoDeConfiguracoes? arquivo, LeituraDoArquivo? leitura, Func<bool> gestoDoUsuarioEmCurso,
        Func<TimeSpan, Action, Action> agendarUmaVez, Action<(string Campo, object? Valor)[]> registrar)
    {
        _arquivo = arquivo;
        _gestoDoUsuarioEmCurso = gestoDoUsuarioEmCurso;
        _agendarUmaVez = agendarUmaVez;
        _registrar = registrar;
        Lidas = leitura?.Configuracoes ?? ConfiguracoesSalvas.Padrao;
        Desejadas = Lidas;
        // Só um principal válido, lido agora e na versão atual, é o que se sabe estar no disco. Vindo da reserva ou dos
        // padrões, a primeira necessidade grava e recria o principal (e guarda um ilegível como cópia de diagnóstico); de uma
        // versão anterior, o primeiro pedido grava na versão atual, mesmo com o mesmo conteúdo (a migração no disco, DEC-038,
        // item 3), e a anterior vira a reserva. Sem pedido, nada é gravado: a partida continua sem gravar.
        _noDisco = leitura is { Origem: OrigemDasConfiguracoes.Principal, Principal: EstadoDoArquivo.Valido } && !(leitura.Versao < EsquemaDeConfiguracoes.VersaoAtual)
            ? EsquemaDeConfiguracoes.Normalizar(leitura.Configuracoes)
            : null;
    }

    /// <summary>
    /// As configurações lidas na partida, que viram a carga do núcleo (<see cref="ConfiguracoesSalvas.ParaACarga"/>): a
    /// posição salva com a tela do monitor da época, a postura e as preferências (a emoção dominante e a travessia
    /// inclusive). Sem arquivo, ou com a leitura falha, as padrão.
    /// </summary>
    internal ConfiguracoesSalvas Lidas { get; }

    /// <summary>O conteúdo que se deseja no disco: o lido, com o que os pedidos trouxeram depois.</summary>
    internal ConfiguracoesSalvas Desejadas { get; private set; }

    /// <summary>Se a gravação está ligada nesta execução: com arquivo, sem bloqueio (versão futura, principal inacessível) e sem um defeito.</summary>
    internal bool Ligada => _arquivo is { GravacaoBloqueada: false } && !_desligadaPorDefeito;

    /// <summary>
    /// Se há um pedido cujo conteúdo não se sabe estar no disco (só com a gravação ligada): o desejado difere do que foi
    /// lido de um principal válido ou gravado depois. Vindo da reserva ou dos padrões, o primeiro pedido grava.
    /// </summary>
    internal bool Pendente => Ligada && _houvePedido && EsquemaDeConfiguracoes.Normalizar(Desejadas) != _noDisco;

    /// <summary>Se o disparo chegou durante um gesto do usuário e a gravação espera o fim dele.</summary>
    internal bool EsperandoOGesto { get; private set; }

    private string Situacao => _arquivo is null || _desligadaPorDefeito ? "desligada" : _arquivo.GravacaoBloqueada ? "bloqueada" : "ligada";

    /// <summary>
    /// A agenda desta execução, com a leitura da partida: o arquivo que a regra da pasta escolheu
    /// (<see cref="ArquivoDeConfiguracoes.DaExecucao"/>, uma instância por execução, porque o bloqueio não volta atrás),
    /// lido uma vez. Sem arquivo, a persistência está desligada (pela linha de comando ou sem a pasta). Qualquer exceção da
    /// leitura, também a que não é de E/S (um link plantado para algo que não é arquivo, um defeito), desliga a
    /// persistência nesta execução, sem derrubar a partida; o log leva só o tipo e o código.
    /// </summary>
    /// <param name="arquivo">O arquivo desta execução; nulo, sem persistência.</param>
    /// <param name="persistenciaDesligada">Se a linha de comando desligou a persistência (só para o motivo no log).</param>
    /// <param name="perfil">Se a pasta é a de um perfil de teste (só para o log: a pasta nunca vai para ele).</param>
    /// <param name="gestoDoUsuarioEmCurso">Se o usuário está no meio de um gesto (botão pressionado, arraste, item na mão).</param>
    /// <param name="agendarUmaVez">Agenda um disparo único e devolve o que o cancela (<see cref="AgendarNoDispatcher"/>).</param>
    /// <param name="registrar">Recebe os campos de cada linha CONFIG; nulo, o log de diagnóstico.</param>
    /// <param name="ler">Só para testes: a leitura no lugar de <see cref="ArquivoDeConfiguracoes.Ler"/>.</param>
    internal static AgendaDeGravacao NaPartida(ArquivoDeConfiguracoes? arquivo, bool persistenciaDesligada, bool perfil, Func<bool> gestoDoUsuarioEmCurso,
        Func<TimeSpan, Action, Action> agendarUmaVez, Action<(string Campo, object? Valor)[]>? registrar = null, Func<ArquivoDeConfiguracoes, LeituraDoArquivo>? ler = null)
    {
        ArgumentNullException.ThrowIfNull(gestoDoUsuarioEmCurso);
        ArgumentNullException.ThrowIfNull(agendarUmaVez);
        registrar ??= campos => Diagnostico.Evento("CONFIG", campos);
        if (arquivo is null)
        {
            registrar([("lido", "desligado"), ("motivo", persistenciaDesligada ? "opcao" : "pasta")]);
            return new AgendaDeGravacao(null, null, gestoDoUsuarioEmCurso, agendarUmaVez, registrar);
        }

        LeituraDoArquivo leitura;
        try
        {
            leitura = ler is null ? arquivo.Ler() : ler(arquivo);
        }
        catch (Exception e)
        {
            registrar([("lido", "desligado"), ("motivo", "erro"), ("erro", Erro(e))]);
            return new AgendaDeGravacao(null, null, gestoDoUsuarioEmCurso, agendarUmaVez, registrar);
        }
        registrar(CamposDaLeitura(leitura, perfil));
        return new AgendaDeGravacao(arquivo, leitura, gestoDoUsuarioEmCurso, agendarUmaVez, registrar);
    }

    /// <summary>
    /// Um efeito do núcleo que pede gravação, vindo de <paramref name="evento"/>: o GravarPosicao troca a posição e a
    /// postura (a borda do esconderijo e a marca de preso) do desejado; o GravarPreferencias, as preferências. Grava na
    /// hora se a política diz que o evento é imediato (<see cref="PoliticaDeGravacao.Imediata"/>); senão, com atraso.
    /// </summary>
    internal void Pedir(Efeito efeito, Evento evento)
    {
        ArgumentNullException.ThrowIfNull(efeito);
        ArgumentNullException.ThrowIfNull(evento);
        (ConfiguracoesSalvas novas, string tipo) = efeito switch
        {
            GravarPosicao g => (Desejadas with { Posicao = g.Posicao, Esconderijo = g.Esconderijo, PresoPeloUsuario = g.PresoPeloUsuario }, "posicao"),
            GravarPreferencias p => (Desejadas with { Preferencias = p.Preferencias }, "preferencias"),
            _ => throw new ArgumentException($"Efeito sem gravação: {efeito.GetType().Name}.", nameof(efeito)),
        };
        string nome = evento.GetType().Name;
        Desejadas = novas;
        _houvePedido = true;
        _registrar([("pedido", tipo), ("evento", nome), ("imediata", PoliticaDeGravacao.Imediata(evento) ? "sim" : "nao"), ("gravacao", Situacao)]);
        if (!Ligada) return;

        // Um pedido novo recomeça: a espera pelo fim do gesto e as novas tentativas ficam para trás.
        EsperandoOGesto = false;
        _falhas = 0;
        if (!Pendente)
        {
            CancelarDisparo();
            _registrar([("gravado", "sem mudanca"), ("motivo", nome)]);
            return;
        }
        if (PoliticaDeGravacao.Imediata(evento))
            Descarregar(nome);
        else
            Armar(PoliticaDeGravacao.Atraso, "atraso");
    }

    /// <summary>
    /// Grava agora o pendente, com as tentativas do caminho imediato, e cancela o disparo; sem pendente, não toca no disco.
    /// Vale também depois de <see cref="Parar"/>: o encerramento descarrega e depois para, e o erro não tratado descarrega
    /// o que der. Devolve se gravou.
    /// </summary>
    internal bool Descarregar(string motivo)
    {
        CancelarDisparo();
        EsperandoOGesto = false;
        return Pendente && GravarAgora(PoliticaDeGravacao.TentativasImediatas, motivo);
    }

    /// <summary>
    /// Depois de cada processamento do núcleo: se um disparo chegou durante o gesto do usuário e o gesto acabou, grava o
    /// pendente agora (L5 da crítica), em vez de rearmar a espera a cada 2 s durante o gesto.
    /// </summary>
    internal void ConferirFimDoGesto()
    {
        if (!EsperandoOGesto || _gestoDoUsuarioEmCurso()) return;
        EsperandoOGesto = false;
        if (!_parada && Pendente) GravarAgora(1, "fimDoGesto");
    }

    /// <summary>Encerramento: cancela o disparo pendente, e nada mais é agendado (um descarregar ainda grava).</summary>
    internal void Parar()
    {
        _parada = true;
        EsperandoOGesto = false;
        CancelarDisparo();
    }

    /// <summary>
    /// O agendador do aplicativo: um <see cref="DispatcherTimer"/> de disparo único, na prioridade de fundo, que para antes
    /// de chamar a ação (sem o Stop, dispararia a cada intervalo). Devolve o que o cancela; cancelado, ou já disparado, não
    /// dispara mais.
    /// </summary>
    internal static Action AgendarNoDispatcher(TimeSpan espera, Action acao)
        => DisparoUnico.NoDispatcher(espera, acao, DispatcherPriority.Background);

    /// <summary>
    /// Os campos da linha CONFIG da partida: de onde vieram as configurações, os estados dos arquivos e contagens. A versão do
    /// arquivo é um valor lido dele: vai como <c>atual</c>, <c>anterior</c> ou <c>futura</c>, comparada com a do esquema, e
    /// nunca o número (DEC-029, item 12; revisão de segurança do bloco P6-P9, achado 2).
    /// </summary>
    internal static (string Campo, object? Valor)[] CamposDaLeitura(LeituraDoArquivo lida, bool perfil)
    {
        ArgumentNullException.ThrowIfNull(lida);
        return
        [
            ("lido", lida.Origem switch { OrigemDasConfiguracoes.Principal => "principal", OrigemDasConfiguracoes.Reserva => "reserva", _ => "padroes" }),
            ("principal", lida.Principal.ToString()),
            ("reserva", lida.Reserva?.ToString() ?? "-"),
            ("versao", lida.Versao switch
            {
                null => "-",
                EsquemaDeConfiguracoes.VersaoAtual => "atual",
                > EsquemaDeConfiguracoes.VersaoAtual => "futura",
                _ => "anterior",
            }),
            ("avisos", lida.Avisos),
            ("tentativas", lida.TentativasNoPrincipal),
            ("gravacao", lida.GravacaoBloqueada ? "bloqueada" : "liberada"),
            ("pasta", perfil ? "perfil" : "padrao"),
        ];
    }

    /// <summary>
    /// Os campos da linha CONFIG de uma gravação: se gravou, o motivo, o tamanho, o tempo, o estado do principal antes, se
    /// guardou a cópia de diagnóstico, as tentativas, o erro (o tipo e o código, nunca a mensagem) e a espera da nova
    /// tentativa, em ms.
    /// </summary>
    internal static (string Campo, object? Valor)[] CamposDaGravacao(ResultadoDaGravacao r, string motivo, double ms, TimeSpan? novaTentativa)
    {
        ArgumentNullException.ThrowIfNull(r);
        return
        [
            ("gravado", r.Gravou ? "sim" : "nao"),
            ("motivo", motivo),
            ("bytes", r.Bytes),
            ("ms", Math.Round(ms, 3)),
            ("principalAntes", r.PrincipalAntes?.ToString() ?? "-"),
            ("copiaDeDiagnostico", r.CopiaDeDiagnostico ? "sim" : "nao"),
            ("tentativas", r.Tentativas),
            ("erro", r.Erro ?? "-"),
            ("novaTentativaMs", novaTentativa is { } espera ? (long)espera.TotalMilliseconds : "-"),
        ];
    }

    private static string Erro(Exception e) => $"{e.GetType().Name} 0x{e.HResult:X8}";

    /// <summary>
    /// Grava o desejado em até <paramref name="tentativas"/> tentativas. Gravou: é o que está no disco. Falhou por E/S, com
    /// a gravação ainda ligada: uma nova tentativa única, na espera da vez (2, 10 e 60 s), e depois desiste até o próximo
    /// pedido. Um defeito desliga a gravação nesta execução, sem lançar.
    /// </summary>
    private bool GravarAgora(int tentativas, string motivo)
    {
        ConfiguracoesSalvas alvo = Desejadas;
        long inicio = Stopwatch.GetTimestamp();
        ResultadoDaGravacao r;
        try
        {
            r = _arquivo!.Gravar(alvo, tentativas);
        }
        catch (Exception e)
        {
            // A gravação nunca derruba o Buzzy; um defeito não se repete a cada pedido.
            _desligadaPorDefeito = true;
            CancelarDisparo();
            _registrar([("gravado", "nao"), ("motivo", motivo), ("erro", Erro(e)), ("gravacao", "desligada")]);
            return false;
        }
        double ms = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

        TimeSpan? novaTentativa = null;
        if (r.Gravou)
        {
            _noDisco = EsquemaDeConfiguracoes.Normalizar(alvo);
            _falhas = 0;
        }
        else if (Ligada && !_parada && _falhas < PoliticaDeGravacao.EsperasDeNovaTentativa.Count)
        {
            novaTentativa = PoliticaDeGravacao.EsperasDeNovaTentativa[_falhas++];
            Armar(novaTentativa.Value, "novaTentativa");
        }
        _registrar(CamposDaGravacao(r, motivo, ms, novaTentativa));
        return r.Gravou;
    }

    /// <summary>Um disparo único depois de <paramref name="espera"/>, no lugar do pendente; parada, nada é agendado.</summary>
    private void Armar(TimeSpan espera, string motivo)
    {
        CancelarDisparo();
        if (_parada) return;
        _cancelar = _agendarUmaVez(espera, () => AoDisparar(motivo));
    }

    private void AoDisparar(string motivo)
    {
        _cancelar = null;
        if (_parada || !Pendente) return;
        if (_gestoDoUsuarioEmCurso())
        {
            // L5: durante o gesto, não rearma: o fim dele grava (ConferirFimDoGesto).
            EsperandoOGesto = true;
            _registrar([("adiado", "gesto"), ("motivo", motivo)]);
            return;
        }
        GravarAgora(1, motivo);
    }

    private void CancelarDisparo()
    {
        Action? cancelar = _cancelar;
        _cancelar = null;
        cancelar?.Invoke();
    }
}
