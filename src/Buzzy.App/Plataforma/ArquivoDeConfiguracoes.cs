using System.IO;
using Buzzy.Core.Persistencia;

namespace Buzzy.App.Plataforma;

internal enum EstadoDoArquivo
{
    // O arquivo ou a pasta não existe.
    Ausente,

    // Existe mas não abre: preso por outro processo, sem permissão, ou é uma pasta.
    Inacessivel,

    // Estrutura inválida, inclusive grande demais.
    Ilegivel,

    Valido,

    // Esquema mais novo: vale o que se conhece e nada é gravado por cima.
    VersaoFutura,
}

internal enum OrigemDasConfiguracoes
{
    Principal,

    // settings.json.bak, a última cópia boa.
    Reserva,

    Padroes,
}

// Na ordem em que acontecem. O gancho dos testes roda depois de cada uma;
// se ele lança, simula uma queda ali.
internal enum EtapaDaGravacao
{
    TemporarioAberto,
    TemporarioEscrito,

    // Flush(true) feito e arquivo fechado.
    TemporarioDescarregado,

    PrincipalConferido,
    Substituido,
}

// Reserva é nulo quando o principal serviu e a reserva nem foi lida.
// GravacaoBloqueada: versão futura ou principal inacessível, nada mais é gravado nesta execução.
// TentativasNoPrincipal: 1 de primeira, até TentativasDeLeitura se estava preso.
internal sealed record LeituraDoArquivo(
    ConfiguracoesSalvas Configuracoes,
    OrigemDasConfiguracoes Origem,
    EstadoDoArquivo Principal,
    EstadoDoArquivo? Reserva,
    bool GravacaoBloqueada,
    int? Versao,
    int Avisos,
    int TentativasNoPrincipal);

// Bytes e Tentativas ficam 0 quando a gravação já estava bloqueada.
// Erro: "bloqueada", "versaoFutura", "principalInacessivel" ou tipo + HResult da
// última exceção. Nunca o caminho, que tem o nome do usuário do Windows.
internal sealed record ResultadoDaGravacao(
    bool Gravou,
    int Bytes,
    EstadoDoArquivo? PrincipalAntes,
    bool CopiaDeDiagnostico,
    int Tentativas,
    string? Erro);

// Arquivos do settings.json; formato e validação ficam no EsquemaDeConfiguracoes.
// E/S síncrona de propósito, o arquivo tem menos de 1 KiB.
//
// Quatro arquivos na mesma pasta, e só eles:
//   settings.json          principal
//   settings.json.bak      reserva: o principal anterior, que era válido
//   settings.json.tmp      temporário da gravação, recriado a cada vez, nunca lido
//   settings.corrupt.json  último principal ilegível substituído, nunca lido
//
// Gravação atômica: temporário sem buffer + Flush(true), depois troca. Principal válido
// vira reserva via File.Replace; ilegível vira o .corrupt e a reserva fica como está.
// Uma queda em qualquer ponto nunca deixa um principal presente e ilegível.
internal sealed class ArquivoDeConfiguracoes
{
    internal const string NomePrincipal = "settings.json";
    internal const string NomeReserva = "settings.json.bak";
    internal const string NomeTemporario = "settings.json.tmp";
    internal const string NomeIlegivel = "settings.corrupt.json";

    // Depois disso um arquivo preso conta como inacessível.
    internal const int TentativasDeLeitura = 3;

    internal static readonly TimeSpan PausaEntreLeituras = TimeSpan.FromMilliseconds(100);

    // Os únicos nomes que o Buzzy cria ou lê na pasta.
    internal static readonly IReadOnlyList<string> Nomes = [NomePrincipal, NomeReserva, NomeTemporario, NomeIlegivel];

    private readonly Action<EtapaDaGravacao>? _aoConcluirEtapa;
    private readonly string _principal;
    private readonly string _reserva;
    private readonly string _temporario;
    private readonly string _ilegivel;
    private bool _gravacaoBloqueada;

    // A pasta só é criada na primeira gravação. aoConcluirEtapa é só pros testes.
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

    // Uma vez bloqueada, não volta a falso.
    internal bool GravacaoBloqueada => _gravacaoBloqueada;

    // Único jeito do app criar o arquivo, pra o isolamento dos perfis de teste ficar num
    // lugar só. Nulo (nada lido nem gravado) com persistência desligada, perfil inválido
    // ou sem pasta local.
    internal static ArquivoDeConfiguracoes? DaExecucao(string? perfilDeTeste, bool persistenciaDesligada)
        => PastaDeDados.DasConfiguracoes(perfilDeTeste, persistenciaDesligada) is { } pasta ? new ArquivoDeConfiguracoes(pasta) : null;

    // Principal, senão reserva, senão padrões. Não cria, altera nem apaga nada e
    // abre compartilhado pra não travar outro processo.
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

    // Só erro de E/S ou permissão é tentado de novo; outra exceção é bug e propaga.
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

                // Confere de novo: o principal pode ter mudado desde a leitura.
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
                        // A reserva continua sendo o último bom; o ilegível vai pro .corrupt.
                        File.Replace(_temporario, _principal, _ilegivel, ignoreMetadataErrors: true);
                        break;
                    case EstadoDoArquivo.VersaoFutura:
                        // Uma versão mais nova gravou depois da leitura: para de gravar.
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

        // Temporário de gravação abortada não serve pra nada.
        ApagarTemporario();
        return new ResultadoDaGravacao(false, dados.Length, principalAntes, CopiaDeDiagnostico: false, tentativas, erro);
    }

    // Apaga e cria com CreateNew em vez de abrir por cima: se o .tmp fosse um link
    // (físico ou simbólico), abrir por cima gravaria no alvo, fora da pasta.
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
            // A próxima gravação recria; ninguém lê o .tmp.
        }
    }

    private void Concluir(EtapaDaGravacao etapa) => _aoConcluirEtapa?.Invoke(etapa);

    private LeituraDoArquivo Usar(ArquivoLido lido, OrigemDasConfiguracoes origem, ArquivoLido principal, EstadoDoArquivo? reserva)
    {
        // Versão futura: usa o que se conhece, mas não grava por cima do resto.
        if (lido.Estado == EstadoDoArquivo.VersaoFutura) _gravacaoBloqueada = true;
        LeituraDasConfiguracoes conteudo = lido.Conteudo!;
        return new LeituraDoArquivo(conteudo.Configuracoes, origem, principal.Estado, reserva, _gravacaoBloqueada, conteudo.Versao, conteudo.Avisos.Count,
            principal.Tentativas);
    }

    private readonly record struct ArquivoLido(EstadoDoArquivo Estado, LeituraDasConfiguracoes? Conteudo, int Tentativas);

    // Grande demais conta como ilegível sem nem ler o conteúdo.
    private static ArquivoLido LerUm(string caminho, int tentativas)
    {
        for (int tentativa = 1; ; tentativa++)
        {
            try
            {
                using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fluxo.Length > EsquemaDeConfiguracoes.TamanhoMaximoEmBytes) return new ArquivoLido(EstadoDoArquivo.Ilegivel, null, tentativa);

                // +1 byte pro esquema recusar um arquivo que cresceu depois do Length.
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
