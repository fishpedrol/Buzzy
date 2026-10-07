using System.Diagnostics;
using System.Windows.Threading;
using Buzzy.App.Plataforma;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Quando gravar o settings.json, seguindo a PoliticaDeGravacao do núcleo.
// - Lê uma vez na partida; qualquer exceção desliga a persistência nesta execução.
// - Pedido com o mesmo conteúdo do disco não grava nem agenda.
// - Com atraso: 2 s depois do último pedido. Se o disparo cai no meio de um gesto,
//   não rearma (com ClickLock viraria periódico); o fim do gesto grava.
// - Na hora em suspensão, fim de sessão, saída ou bloqueio, e ao descarregar.
// - Falha de E/S: novas tentativas em 2, 10 e 60 s; depois, só no próximo pedido.
// - Exceção que não é de E/S desliga a gravação nesta execução, sem derrubar o Buzzy.
// E/S síncrona na thread da interface: o arquivo tem menos de 1 KiB. As linhas CONFIG
// levam só enums, contagens e tempos, nunca a pasta nem valores do arquivo (o ToString
// automático dos records imprimiria a chave e a tela).
internal sealed class AgendaDeGravacao
{
    private readonly ArquivoDeConfiguracoes? _arquivo;
    private readonly Func<bool> _gestoDoUsuarioEmCurso;
    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Action<(string Campo, object? Valor)[]> _registrar;

    // Normalizado. Nulo quando não se sabe o que tem no disco: aí o próximo pedido grava.
    private ConfiguracoesSalvas? _noDisco;

    private Action? _cancelar;

    // Falhas seguidas; escolhe a espera da nova tentativa.
    private int _falhas;

    // Sem nenhum pedido, nada está pendente: a partida nunca grava sozinha, e
    // descarregar sem pedido não recria o principal.
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
        // Só um principal válido e na versão atual conta como "no disco". Vindo da reserva ou
        // dos padrões, o primeiro pedido recria o principal (o ilegível vira cópia de
        // diagnóstico). De versão anterior, o primeiro pedido grava mesmo sem mudança, pra
        // migrar o arquivo, e o antigo vira a reserva.
        _noDisco = leitura is { Origem: OrigemDasConfiguracoes.Principal, Principal: EstadoDoArquivo.Valido } && !(leitura.Versao < EsquemaDeConfiguracoes.VersaoAtual)
            ? EsquemaDeConfiguracoes.Normalizar(leitura.Configuracoes)
            : null;
    }

    // Viram a carga do núcleo. Sem arquivo ou com leitura falha, os padrões.
    internal ConfiguracoesSalvas Lidas { get; }

    // O lido mais o que os pedidos trouxeram depois.
    internal ConfiguracoesSalvas Desejadas { get; private set; }

    // Bloqueada = versão futura ou principal inacessível.
    internal bool Ligada => _arquivo is { GravacaoBloqueada: false } && !_desligadaPorDefeito;

    internal bool Pendente => Ligada && _houvePedido && EsquemaDeConfiguracoes.Normalizar(Desejadas) != _noDisco;

    internal bool EsperandoOGesto { get; private set; }

    private string Situacao => _arquivo is null || _desligadaPorDefeito ? "desligada" : _arquivo.GravacaoBloqueada ? "bloqueada" : "ligada";

    // Uma instância de arquivo por execução, porque o bloqueio não volta atrás. Arquivo
    // nulo = persistência desligada. Qualquer exceção na leitura (até um link plantado
    // apontando pra algo que não é arquivo) desliga a persistência sem derrubar a partida.
    // persistenciaDesligada e perfil só servem pro log (a pasta nunca vai pra ele).
    // ler: só pra testes.
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

    // Grava na hora se a política diz que o evento é imediato; senão, com atraso.
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

        // Pedido novo zera a espera do gesto e as novas tentativas.
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

    // Funciona mesmo depois de Parar: o erro não tratado descarrega o que der.
    // Sem pendente, não toca no disco.
    internal bool Descarregar(string motivo)
    {
        CancelarDisparo();
        EsperandoOGesto = false;
        return Pendente && GravarAgora(PoliticaDeGravacao.TentativasImediatas, motivo);
    }

    // Chamado depois de cada processamento do núcleo.
    internal void ConferirFimDoGesto()
    {
        if (!EsperandoOGesto || _gestoDoUsuarioEmCurso()) return;
        EsperandoOGesto = false;
        if (!_parada && Pendente) GravarAgora(1, "fimDoGesto");
    }

    // Nada mais é agendado, mas Descarregar ainda grava.
    internal void Parar()
    {
        _parada = true;
        EsperandoOGesto = false;
        CancelarDisparo();
    }

    internal static Action AgendarNoDispatcher(TimeSpan espera, Action acao)
        => DisparoUnico.NoDispatcher(espera, acao, DispatcherPriority.Background);

    // A versão é um valor vindo do arquivo, então vai pro log só como atual/anterior/futura,
    // nunca o número.
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

    // Do erro vai só o tipo e o código, nunca a mensagem.
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

    // Falha de E/S agenda nova tentativa (2, 10, 60 s) e depois desiste até o próximo
    // pedido. Qualquer outra exceção desliga a gravação, sem lançar.
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
            // Gravar nunca derruba o Buzzy, e um defeito não se repete a cada pedido.
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

    // Substitui o pendente.
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
            // Não rearma durante o gesto; ConferirFimDoGesto grava quando ele acabar.
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
