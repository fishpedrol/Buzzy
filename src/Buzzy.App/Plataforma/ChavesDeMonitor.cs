using System.Security.Cryptography;
using System.Text;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Um alvo ativo da configuração de vídeo: o nome GDI da fonte (<c>\\.\DISPLAYn</c>) e o caminho do dispositivo do
/// monitor ligado a ela. O caminho identifica o hardware: só serve para calcular a chave (<see cref="ChavesDeMonitor.DoCaminho"/>)
/// e nunca vai para o log, o arquivo nem o texto deste registro.
/// </summary>
internal readonly record struct AlvoAtivo(string NomeGdi, string CaminhoDoDispositivo)
{
    public override string ToString() => $"AlvoAtivo {{ NomeGdi = {NomeGdi} }}";
}

/// <summary>De onde veio a chave de um monitor numa leitura da topologia.</summary>
internal enum OrigemDaChave
{
    /// <summary>Do caminho do dispositivo, lido nesta consulta: a chave estável.</summary>
    Caminho,

    /// <summary>
    /// De uma consulta boa anterior, para o mesmo nome GDI com uma tela do mesmo tamanho (transladada ou não): desta vez a
    /// consulta falhou, ou não trouxe o caminho desse monitor.
    /// </summary>
    Cache,

    /// <summary>Do nome GDI (<c>gdi:</c>): sem caminho nem cache que valham.</summary>
    Reserva,
}

/// <summary>A chave dada a um monitor enumerado, com o nome GDI dele e a origem.</summary>
internal readonly record struct ChaveAtribuida(string NomeGdi, string Chave, OrigemDaChave Origem);

/// <summary>A chave de um nome GDI numa leitura com a consulta boa e a tela do monitor naquela leitura.</summary>
internal readonly record struct ChaveConhecida(string Chave, RetanguloPx Tela);

/// <summary>
/// Chave estável do monitor (DEC-030, ARCHITECTURE.md 2.4), em lógica pura, testável sem hardware.
///
/// A chave é <c>mon:</c> seguido de 16 dígitos hexadecimais, os 8 primeiros bytes do SHA-256 do caminho do
/// dispositivo do monitor em maiúsculas. É tão estável quanto o caminho, tem tamanho fixo, só usa ASCII sem espaço,
/// <c>;</c>, <c>,</c>, <c>|</c> ou <c>=</c>, e não grava nem registra o identificador do hardware. Quando o caminho não
/// pode ser lido, vale a chave da última consulta boa para o mesmo nome GDI com uma tela do mesmo tamanho, mesmo
/// transladada (uma troca de principal ou um rearranjo move as telas sem trocar os monitores); sem ela, a reserva
/// <c>gdi:</c> seguida do nome GDI. Os prefixos separam os dois espaços de chaves. Para o núcleo a chave continua opaca:
/// ele só a compara por igualdade.
/// </summary>
internal static class ChavesDeMonitor
{
    internal const string PrefixoEstavel = "mon:";
    internal const string PrefixoDeReserva = "gdi:";

    /// <summary>Bytes do resumo que entram na chave.</summary>
    private const int BytesDaChave = 8;

    /// <summary>
    /// A chave estável de um caminho de dispositivo. Esta função nunca pode mudar entre versões: as chaves gravadas
    /// deixariam de valer (os valores estão fixados em ChavesDeMonitorTestes).
    /// </summary>
    internal static string DoCaminho(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        byte[] resumo = SHA256.HashData(Encoding.UTF8.GetBytes(caminho.ToUpperInvariant()));
        return PrefixoEstavel + Convert.ToHexStringLower(resumo, 0, BytesDaChave);
    }

    /// <summary>A chave de reserva, pelo nome GDI.</summary>
    internal static string DeReserva(string nomeGdi)
    {
        ArgumentNullException.ThrowIfNull(nomeGdi);
        return PrefixoDeReserva + nomeGdi;
    }

    /// <summary>
    /// Nome GDI → chave estável, um por fonte. Num clone (uma fonte com vários alvos) vale o menor caminho em
    /// maiúsculas, por comparação ordinal: a escolha não depende da ordem do Windows, e quem sai do clone deixa a
    /// chave com um dos monitores. Alvos sem nome GDI ou sem caminho não entram.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Mapear(IEnumerable<AlvoAtivo> alvos)
    {
        ArgumentNullException.ThrowIfNull(alvos);
        var menorCaminho = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (AlvoAtivo alvo in alvos)
        {
            if (string.IsNullOrWhiteSpace(alvo.NomeGdi) || string.IsNullOrWhiteSpace(alvo.CaminhoDoDispositivo)) continue;
            string caminho = alvo.CaminhoDoDispositivo.ToUpperInvariant();
            if (!menorCaminho.TryGetValue(alvo.NomeGdi, out string? atual) || string.CompareOrdinal(caminho, atual) < 0)
                menorCaminho[alvo.NomeGdi] = caminho;
        }
        return menorCaminho.ToDictionary(p => p.Key, p => DoCaminho(p.Value), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A chave de cada monitor enumerado, na mesma ordem:
    /// <list type="number">
    /// <item>a do caminho, se o nome GDI está no <paramref name="mapa"/> (nulo quando a consulta falhou);</item>
    /// <item>senão, a do <paramref name="cache"/>, se o nome GDI está lá com uma tela do mesmo tamanho, transladada ou não.
    /// A troca de principal e o rearranjo movem as telas sem trocar os monitores: exigir a mesma tela punha todos na
    /// reserva justamente quando a consulta é negada (sessão bloqueada ou remota), e o apelido por retângulo do núcleo
    /// levava o personagem ao monitor que passou a ocupar a tela antiga. Com outro tamanho, o nome GDI pode ter ido para
    /// outro monitor. O DPI não conta: é a escala que o usuário escolhe, e um monitor que só mudou de escala é o mesmo;</item>
    /// <item>senão, a reserva <c>gdi:</c>.</item>
    /// </list>
    /// Nenhuma chave se repete. As do caminho são da consulta atual e são distribuídas primeiro; uma chave do cache
    /// que repetiria outra já dada vai para a reserva. Como os nomes GDI de uma enumeração são únicos, as reservas
    /// também são.
    /// </summary>
    internal static IReadOnlyList<ChaveAtribuida> Atribuir(
        IReadOnlyList<(string NomeGdi, RetanguloPx Tela)> enumerados,
        IReadOnlyDictionary<string, string>? mapa,
        IReadOnlyDictionary<string, ChaveConhecida> cache)
    {
        ArgumentNullException.ThrowIfNull(enumerados);
        ArgumentNullException.ThrowIfNull(cache);
        var chaves = new ChaveAtribuida?[enumerados.Count];
        var usadas = new HashSet<string>(StringComparer.Ordinal);

        // 1. Do caminho, lidas agora.
        for (int i = 0; i < enumerados.Count; i++)
        {
            string nome = enumerados[i].NomeGdi;
            if (mapa is not null && mapa.TryGetValue(nome, out string? chave) && usadas.Add(chave))
                chaves[i] = new ChaveAtribuida(nome, chave, OrigemDaChave.Caminho);
        }

        // 2. Do cache, com uma tela do mesmo tamanho, e 3. a reserva.
        for (int i = 0; i < enumerados.Count; i++)
        {
            if (chaves[i] is not null) continue;
            (string nome, RetanguloPx tela) = enumerados[i];
            chaves[i] = cache.TryGetValue(nome, out ChaveConhecida conhecida) && MesmoTamanho(conhecida.Tela, tela) && usadas.Add(conhecida.Chave)
                ? new ChaveAtribuida(nome, conhecida.Chave, OrigemDaChave.Cache)
                : Reserva(nome, usadas);
        }
        return [.. chaves.Select(c => c!.Value)];
    }

    /// <summary>
    /// O cache depois de uma leitura com a consulta boa: as chaves lidas do caminho e as que continuaram pelo cache (o
    /// monitor ainda enumerado cujo caminho a consulta não trouxe desta vez, por estar marcado como indisponível ou por
    /// uma falha ao ler o nome), com a tela de cada monitor naquela leitura. Sem isso, uma consulta boa sem um alvo
    /// apagava a chave dele, e a próxima consulta negada o punha na reserva. A reserva nunca entra.
    /// </summary>
    internal static IReadOnlyDictionary<string, ChaveConhecida> NovoCache(
        IReadOnlyList<(string NomeGdi, RetanguloPx Tela)> enumerados, IReadOnlyList<ChaveAtribuida> chaves)
    {
        ArgumentNullException.ThrowIfNull(enumerados);
        ArgumentNullException.ThrowIfNull(chaves);
        if (enumerados.Count != chaves.Count)
            throw new ArgumentException("Uma chave por monitor enumerado.", nameof(chaves));
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < chaves.Count; i++)
        {
            if (chaves[i].Origem is OrigemDaChave.Caminho or OrigemDaChave.Cache)
                cache[enumerados[i].NomeGdi] = new ChaveConhecida(chaves[i].Chave, enumerados[i].Tela);
        }
        return cache;
    }

    /// <summary>Se as duas telas têm a mesma largura e a mesma altura, em qualquer lugar do desktop virtual.</summary>
    private static bool MesmoTamanho(RetanguloPx a, RetanguloPx b) => a.Largura == b.Largura && a.Altura == b.Altura;

    private static ChaveAtribuida Reserva(string nome, HashSet<string> usadas)
    {
        string chave = DeReserva(nome);
        usadas.Add(chave);
        return new ChaveAtribuida(nome, chave, OrigemDaChave.Reserva);
    }
}
