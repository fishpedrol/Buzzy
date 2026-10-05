using System.IO;
using Buzzy.Core.Persistencia;

namespace Buzzy.App.Plataforma;

/// <summary>Estado de um dos arquivos de configurações no disco (<see cref="ArquivoDeConfiguracoes"/>).</summary>
internal enum EstadoDoArquivo
{
    /// <summary>O arquivo, ou a pasta dele, não existe.</summary>
    Ausente,

    /// <summary>Existe, mas não deu para abrir nem ler: preso por outro processo sem compartilhar, sem permissão, é uma pasta.</summary>
    Inacessivel,

    /// <summary>Lido, com a estrutura inválida (<see cref="SituacaoDaLeitura.Ilegivel"/>), inclusive grande demais.</summary>
    Ilegivel,

    /// <summary>Lido na versão atual do esquema.</summary>
    Valido,

    /// <summary>Lido, de uma versão futura do esquema: vale o que se conhece, e nada é gravado por cima dele.</summary>
    VersaoFutura,
}

/// <summary>De onde vieram as configurações de uma leitura (<see cref="ArquivoDeConfiguracoes.Ler"/>).</summary>
internal enum OrigemDasConfiguracoes
{
    /// <summary>Do settings.json.</summary>
    Principal,

    /// <summary>Do settings.json.bak, a última cópia boa, porque o principal faltou ou não serviu.</summary>
    Reserva,

    /// <summary>Nenhum dos dois serviu: as configurações padrão.</summary>
    Padroes,
}

/// <summary>
/// Etapas de <see cref="ArquivoDeConfiguracoes.Gravar"/>, na ordem em que acontecem. O gancho dos testes é chamado
/// depois de cada uma; se ele lança, simula uma queda logo depois dela.
/// </summary>
internal enum EtapaDaGravacao
{
    /// <summary>O temporário foi criado, vazio, e está aberto.</summary>
    TemporarioAberto,

    /// <summary>O conteúdo novo foi escrito no temporário.</summary>
    TemporarioEscrito,

    /// <summary>O temporário foi descarregado no disco (<c>Flush(true)</c>) e fechado.</summary>
    TemporarioDescarregado,

    /// <summary>O principal atual foi conferido.</summary>
    PrincipalConferido,

    /// <summary>O temporário virou o principal.</summary>
    Substituido,
}

/// <summary>Resultado de <see cref="ArquivoDeConfiguracoes.Ler"/>.</summary>
/// <param name="Configuracoes">O que vale: o lido do principal ou da reserva, campo a campo, ou as configurações padrão.</param>
/// <param name="Origem">De qual arquivo vieram as configurações.</param>
/// <param name="Principal">Estado do settings.json.</param>
/// <param name="Reserva">Estado do settings.json.bak; nulo quando o principal serviu e a reserva nem foi consultada.</param>
/// <param name="GravacaoBloqueada">
/// Se nada mais será gravado nesta execução: o arquivo usado é de versão futura, ou o principal estava inacessível.
/// </param>
/// <param name="Versao">O <c>schemaVersion</c> do arquivo usado; nulo com as configurações padrão.</param>
/// <param name="Avisos">Quantos avisos a leitura do arquivo usado gerou (campo ignorado, repetido, fora da faixa ou do tipo errado).</param>
/// <param name="TentativasNoPrincipal">
/// Quantas vezes o settings.json foi aberto, ou tentado: 1 quando foi lido, ou visto ausente, de primeira; até
/// <see cref="ArquivoDeConfiguracoes.TentativasDeLeitura"/> quando estava preso por outro processo.
/// </param>
internal sealed record LeituraDoArquivo(
    ConfiguracoesSalvas Configuracoes,
    OrigemDasConfiguracoes Origem,
    EstadoDoArquivo Principal,
    EstadoDoArquivo? Reserva,
    bool GravacaoBloqueada,
    int? Versao,
    int Avisos,
    int TentativasNoPrincipal);

/// <summary>Resultado de <see cref="ArquivoDeConfiguracoes.Gravar"/>.</summary>
/// <param name="Gravou">Se o principal passou a ter o conteúdo novo.</param>
/// <param name="Bytes">Tamanho do conteúdo novo; 0 quando a gravação já estava bloqueada.</param>
/// <param name="PrincipalAntes">Estado do principal na última conferência; nulo se nenhuma tentativa chegou a conferi-lo.</param>
/// <param name="CopiaDeDiagnostico">Se um principal ilegível foi guardado como settings.corrupt.json.</param>
/// <param name="Tentativas">Quantas tentativas foram feitas; 0 quando a gravação já estava bloqueada.</param>
/// <param name="Erro">
/// Por que não gravou: <c>"bloqueada"</c>, <c>"versaoFutura"</c>, <c>"principalInacessivel"</c> ou o tipo e o código da
/// exceção de E/S da última tentativa. Nunca leva um caminho, que contém o nome do usuário (SECURITY.md 6).
/// </param>
internal sealed record ResultadoDaGravacao(
    bool Gravou,
    int Bytes,
    EstadoDoArquivo? PrincipalAntes,
    bool CopiaDeDiagnostico,
    int Tentativas,
    string? Erro);

/// <summary>
/// O settings.json no disco (Fase 5; ARCHITECTURE.md 2.12; SECURITY.md 5 e 7): lê sem efeito colateral e grava de
/// forma atômica. O formato e a validação são do núcleo (<see cref="EsquemaDeConfiguracoes"/>); aqui ficam só os
/// arquivos. E/S síncrona: o arquivo tem menos de 1 KiB.
///
/// São quatro arquivos, todos na mesma pasta, e nenhum outro:
/// <list type="bullet">
/// <item><c>settings.json</c>, o principal;</item>
/// <item><c>settings.json.bak</c>, a reserva: o principal anterior, válido quando foi substituído;</item>
/// <item><c>settings.json.tmp</c>, o temporário de uma gravação, criado do zero a cada uma e nunca lido;</item>
/// <item><c>settings.corrupt.json</c>, a cópia de diagnóstico: o último principal ilegível substituído, uma só, nunca lida.</item>
/// </list>
///
/// A leitura (<see cref="Ler"/>) usa o principal; se ele faltar ou não servir, a reserva; senão, as configurações
/// padrão. Um arquivo de versão futura, ou um principal inacessível, bloqueia a gravação nesta execução, para não
/// apagar o que não se conhece ou não se conseguiu ler.
///
/// A gravação (<see cref="Gravar"/>) escreve o temporário sem buffer e o descarrega no disco, confere o principal
/// atual e troca tudo de uma vez: sem principal, o temporário vira o principal; com um válido,
/// <see cref="File.Replace(string, string, string?, bool)"/> guarda o anterior como reserva; com um ilegível, guarda-o
/// como cópia de diagnóstico e deixa a reserva como está. Uma queda em qualquer ponto deixa o principal anterior, o
/// novo ou só a reserva: o Buzzy nunca deixa um principal presente e ilegível.
/// </summary>
internal sealed class ArquivoDeConfiguracoes
{
    internal const string NomePrincipal = "settings.json";
    internal const string NomeReserva = "settings.json.bak";
    internal const string NomeTemporario = "settings.json.tmp";
    internal const string NomeIlegivel = "settings.corrupt.json";

    /// <summary>Tentativas de abrir e ler um arquivo preso na leitura da partida; depois delas, ele é inacessível.</summary>
    internal const int TentativasDeLeitura = 3;

    /// <summary>Pausa entre as tentativas de leitura.</summary>
    internal static readonly TimeSpan PausaEntreLeituras = TimeSpan.FromMilliseconds(100);

    /// <summary>Os únicos nomes que o Buzzy cria ou lê na pasta.</summary>
    internal static readonly IReadOnlyList<string> Nomes = [NomePrincipal, NomeReserva, NomeTemporario, NomeIlegivel];

    private readonly Action<EtapaDaGravacao>? _aoConcluirEtapa;
    private readonly string _principal;
    private readonly string _reserva;
    private readonly string _temporario;
    private readonly string _ilegivel;
    private bool _gravacaoBloqueada;

    /// <param name="pasta">Pasta dos arquivos, em caminho completo. Só é criada na primeira gravação.</param>
    /// <param name="aoConcluirEtapa">Só para testes: chamado depois de cada etapa da gravação; se lançar, simula uma queda ali.</param>
    internal ArquivoDeConfiguracoes(string pasta, Action<EtapaDaGravacao>? aoConcluirEtapa = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(pasta);
        if (!Path.IsPathFullyQualified(pasta))
            throw new ArgumentException("A pasta das configurações precisa ser um caminho completo.", nameof(pasta));
        Pasta = pasta;
        _aoConcluirEtapa = aoConcluirEtapa;
        _principal = Path.Combine(pasta, NomePrincipal);
        _reserva = Path.Combine(pasta, NomeReserva);
        _temporario = Path.Combine(pasta, NomeTemporario);
        _ilegivel = Path.Combine(pasta, NomeIlegivel);
    }

    internal string Pasta { get; }

    /// <summary>Se nada mais é gravado nesta execução (versão futura ou principal inacessível); não volta a falso.</summary>
    internal bool GravacaoBloqueada => _gravacaoBloqueada;

    /// <summary>
    /// O arquivo de configurações desta execução, na pasta que <see cref="PastaDeDados.DasConfiguracoes(string?, bool)"/>
    /// escolhe pelas opções da linha de comando; nulo sem pasta (persistência desligada, perfil de teste inválido ou
    /// sem a pasta local do usuário), e então nada é lido nem gravado. É a única forma de o aplicativo criar o
    /// arquivo: o isolamento dos testes e a falha fechada ficam numa regra só, testada.
    /// </summary>
    internal static ArquivoDeConfiguracoes? DaExecucao(string? perfilDeTeste, bool persistenciaDesligada)
        => PastaDeDados.DasConfiguracoes(perfilDeTeste, persistenciaDesligada) is { } pasta ? new ArquivoDeConfiguracoes(pasta) : null;

    /// <summary>
    /// Lê as configurações: principal, depois reserva, depois padrões. Não cria, não altera e não
    /// apaga nenhum arquivo nem a pasta, e não impede outro processo de usá-los. O temporário e a cópia de
    /// diagnóstico nunca são lidos. Um arquivo preso é tentado <see cref="TentativasDeLeitura"/> vezes.
    /// </summary>
    internal LeituraDoArquivo Ler()
    {
        ArquivoLido principal = LerUm(_principal, TentativasDeLeitura);
        if (principal.Estado is EstadoDoArquivo.Valido or EstadoDoArquivo.VersaoFutura)
            return Usar(principal, OrigemDasConfiguracoes.Principal, principal, reserva: null);

        // Não se grava por cima do que não se conseguiu ler.
        if (principal.Estado == EstadoDoArquivo.Inacessivel) _gravacaoBloqueada = true;

        ArquivoLido reserva = LerUm(_reserva, TentativasDeLeitura);
        if (reserva.Estado is EstadoDoArquivo.Valido or EstadoDoArquivo.VersaoFutura)
            return Usar(reserva, OrigemDasConfiguracoes.Reserva, principal, reserva.Estado);

        return new LeituraDoArquivo(ConfiguracoesSalvas.Padrao, OrigemDasConfiguracoes.Padroes, principal.Estado, reserva.Estado,
            _gravacaoBloqueada, Versao: null, Avisos: 0, principal.Tentativas);
    }

    /// <summary>
    /// Grava as configurações (normalizadas pelo esquema) pelo protocolo atômico descrito no resumo da classe, em até
    /// <paramref name="tentativas"/> tentativas, com a pausa da política entre elas. Uma falha de E/S ou de
    /// permissão é tentada de novo; qualquer outra exceção é defeito e propaga. Com a gravação bloqueada, nada é
    /// tocado.
    /// </summary>
    internal ResultadoDaGravacao Gravar(ConfiguracoesSalvas configuracoes, int tentativas = 1)
    {
        ArgumentNullException.ThrowIfNull(configuracoes);
        ArgumentOutOfRangeException.ThrowIfLessThan(tentativas, 1);
        if (_gravacaoBloqueada)
            return new ResultadoDaGravacao(false, 0, PrincipalAntes: null, CopiaDeDiagnostico: false, Tentativas: 0, "bloqueada");

        byte[] dados = EsquemaDeConfiguracoes.Escrever(configuracoes);
        EstadoDoArquivo? principalAntes = null;
        string? erro = null;
        for (int tentativa = 1; tentativa <= tentativas; tentativa++)
        {
            if (tentativa > 1) Thread.Sleep(PoliticaDeGravacao.PausaEntreTentativasImediatas);
            try
            {
                Directory.CreateDirectory(Pasta);
                EscreverTemporario(dados);

                // Conferido agora, e não na partida: o principal pode ter mudado desde a leitura.
                principalAntes = LerUm(_principal, tentativas: 1).Estado;
                Concluir(EtapaDaGravacao.PrincipalConferido);
                switch (principalAntes)
                {
                    case EstadoDoArquivo.Ausente:
                        File.Move(_temporario, _principal, overwrite: false);
                        break;
                    case EstadoDoArquivo.Valido:
                        File.Replace(_temporario, _principal, _reserva, ignoreMetadataErrors: true);
                        break;
                    case EstadoDoArquivo.Ilegivel:
                        // A reserva continua sendo o último arquivo bom; o ilegível vira a única cópia de diagnóstico.
                        File.Replace(_temporario, _principal, _ilegivel, ignoreMetadataErrors: true);
                        break;
                    case EstadoDoArquivo.VersaoFutura:
                        // Uma versão mais nova gravou depois da leitura: nada mais é gravado nesta execução.
                        _gravacaoBloqueada = true;
                        ApagarTemporario();
                        return new ResultadoDaGravacao(false, dados.Length, principalAntes, CopiaDeDiagnostico: false, tentativa, "versaoFutura");
                    default:
                        erro = "principalInacessivel";
                        continue;
                }
                Concluir(EtapaDaGravacao.Substituido);
                return new ResultadoDaGravacao(true, dados.Length, principalAntes, principalAntes == EstadoDoArquivo.Ilegivel, tentativa, Erro: null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                erro = $"{e.GetType().Name} 0x{e.HResult:X8}";
            }
        }

        // O temporário de uma gravação que desistiu não serve para nada: nunca é lido, e a próxima o recria.
        ApagarTemporario();
        return new ResultadoDaGravacao(false, dados.Length, principalAntes, CopiaDeDiagnostico: false, tentativas, erro);
    }

    /// <summary>
    /// Temporário escrito sem buffer e descarregado no disco antes de virar o principal. Um temporário que sobrou de
    /// uma gravação que caiu é apagado antes, e o novo é criado do zero (<see cref="FileMode.CreateNew"/>): se esse
    /// nome fosse um link, físico ou simbólico, para outro arquivo, abri-lo por cima truncaria e gravaria o outro
    /// arquivo, fora da pasta; apagar remove só o link.
    /// </summary>
    private void EscreverTemporario(byte[] dados)
    {
        File.Delete(_temporario);
        using (var fluxo = new FileStream(_temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 1))
        {
            Concluir(EtapaDaGravacao.TemporarioAberto);
            fluxo.Write(dados);
            Concluir(EtapaDaGravacao.TemporarioEscrito);
            fluxo.Flush(flushToDisk: true);
        }
        Concluir(EtapaDaGravacao.TemporarioDescarregado);
    }

    private void ApagarTemporario()
    {
        try
        {
            File.Delete(_temporario);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Fica para a próxima gravação, que o recria; o temporário nunca é lido.
        }
    }

    private void Concluir(EtapaDaGravacao etapa) => _aoConcluirEtapa?.Invoke(etapa);

    private LeituraDoArquivo Usar(ArquivoLido lido, OrigemDasConfiguracoes origem, ArquivoLido principal, EstadoDoArquivo? reserva)
    {
        // Versão futura: vale o que se conhece, e nada é gravado por cima, para não apagar o que não se conhece.
        if (lido.Estado == EstadoDoArquivo.VersaoFutura) _gravacaoBloqueada = true;
        LeituraDasConfiguracoes conteudo = lido.Conteudo!;
        return new LeituraDoArquivo(conteudo.Configuracoes, origem, principal.Estado, reserva, _gravacaoBloqueada, conteudo.Versao, conteudo.Avisos.Count,
            principal.Tentativas);
    }

    /// <summary>Estado de um arquivo, quantas vezes ele foi aberto (ou tentado) e, quando lido, o que o esquema achou dele.</summary>
    private readonly record struct ArquivoLido(EstadoDoArquivo Estado, LeituraDasConfiguracoes? Conteudo, int Tentativas);

    /// <summary>
    /// Avalia um arquivo sem alterá-lo nem impedir outro processo de usá-lo: ausente; grande demais (ilegível, sem ler
    /// o conteúdo); ou o que o esquema disser dos bytes. Preso ou sem permissão, tenta de novo até
    /// <paramref name="tentativas"/> vezes, com <see cref="PausaEntreLeituras"/> entre elas, e então é inacessível.
    /// </summary>
    private static ArquivoLido LerUm(string caminho, int tentativas)
    {
        for (int tentativa = 1; ; tentativa++)
        {
            try
            {
                using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fluxo.Length > EsquemaDeConfiguracoes.TamanhoMaximoEmBytes) return new ArquivoLido(EstadoDoArquivo.Ilegivel, null, tentativa);

                // Um byte além do limite, para o esquema recusar um arquivo que cresceu desde a consulta do tamanho.
                LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(LerNoMaximo(fluxo, EsquemaDeConfiguracoes.TamanhoMaximoEmBytes + 1));
                EstadoDoArquivo estado = lida.Situacao switch
                {
                    SituacaoDaLeitura.Valida => EstadoDoArquivo.Valido,
                    SituacaoDaLeitura.VersaoFutura => EstadoDoArquivo.VersaoFutura,
                    _ => EstadoDoArquivo.Ilegivel,
                };
                return new ArquivoLido(estado, lida, tentativa);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return new ArquivoLido(EstadoDoArquivo.Ausente, null, tentativa);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (tentativa >= tentativas) return new ArquivoLido(EstadoDoArquivo.Inacessivel, null, tentativa);
                Thread.Sleep(PausaEntreLeituras);
            }
        }
    }

    private static ReadOnlyMemory<byte> LerNoMaximo(Stream fluxo, int limite)
    {
        byte[] dados = new byte[limite];
        int total = 0;
        int lidos;
        while (total < limite && (lidos = fluxo.Read(dados, total, limite - total)) > 0) total += lidos;
        return dados.AsMemory(0, total);
    }
}
