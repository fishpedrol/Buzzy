using System.Security.Cryptography;
using System.Text;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

// NomeGdi é o \\.\DISPLAYn da fonte. O caminho do dispositivo identifica o hardware:
// só serve pra calcular a chave e nunca vai pro log, pro arquivo nem pro ToString.
internal readonly record struct AlvoAtivo(string NomeGdi, string CaminhoDoDispositivo)
{
    public override string ToString() => $"AlvoAtivo {{ NomeGdi = {NomeGdi} }}";
}

internal enum OrigemDaChave
{
    // Caminho do dispositivo lido agora: a chave estável.
    Caminho,

    // Consulta falhou ou não trouxe esse monitor: reaproveita a da última consulta boa,
    // mesmo nome GDI e tela do mesmo tamanho (pode ter mudado de lugar).
    Cache,

    // "gdi:" + nome GDI, quando nada mais serve.
    Reserva,
}

internal readonly record struct ChaveAtribuida(string NomeGdi, string Chave, OrigemDaChave Origem);

// Tela do monitor na última consulta boa.
internal readonly record struct ChaveConhecida(string Chave, RetanguloPx Tela);

// Chave estável do monitor, lógica pura pra testar sem hardware.
//
// "mon:" + 16 hex = 8 primeiros bytes do SHA-256 do caminho do dispositivo em maiúsculas.
// Tamanho fixo, ASCII sem espaço, ; , | ou =, e não expõe o id do hardware.
// Sem caminho, usa o cache (mesmo nome GDI, tela do mesmo tamanho, já que trocar o
// principal ou rearranjar move as telas sem trocar monitor); sem cache, "gdi:" + nome.
// Pro núcleo a chave é opaca, só compara por igualdade.
internal static class ChavesDeMonitor
{
    internal const string PrefixoEstavel = "mon:";
    internal const string PrefixoDeReserva = "gdi:";

    private const int BytesDaChave = 8;

    // NUNCA mudar entre versões: as chaves gravadas deixariam de valer
    // (valores fixados em ChavesDeMonitorTestes).
    internal static string DoCaminho(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        byte[] resumo = SHA256.HashData(Encoding.UTF8.GetBytes(caminho.ToUpperInvariant()));
        return PrefixoEstavel + Convert.ToHexStringLower(resumo, 0, BytesDaChave);
    }

    internal static string DeReserva(string nomeGdi)
    {
        ArgumentNullException.ThrowIfNull(nomeGdi);
        return PrefixoDeReserva + nomeGdi;
    }

    // Nome GDI -> chave. Em clone (uma fonte, vários alvos) vale o menor caminho em
    // ordinal, pra não depender da ordem que o Windows devolve.
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

    // Ordem: caminho (mapa nulo = consulta falhou), cache com tela do mesmo tamanho, reserva.
    // Só o tamanho, não a posição: exigir a mesma tela jogava tudo na reserva justo com a
    // consulta negada (sessão bloqueada/remota) e o personagem ia pro monitor errado.
    // Tamanho diferente = o nome GDI pode ter ido pra outro monitor. DPI não conta.
    // Nenhuma chave se repete: as do caminho vão primeiro e um cache repetido cai na reserva.
    internal static IReadOnlyList<ChaveAtribuida> Atribuir(
        IReadOnlyList<(string NomeGdi, RetanguloPx Tela)> enumerados,
        IReadOnlyDictionary<string, string>? mapa,
        IReadOnlyDictionary<string, ChaveConhecida> cache)
    {
        ArgumentNullException.ThrowIfNull(enumerados);
        ArgumentNullException.ThrowIfNull(cache);
        var chaves = new ChaveAtribuida?[enumerados.Count];
        var usadas = new HashSet<string>(StringComparer.Ordinal);

        // 1. Do caminho.
        for (int i = 0; i < enumerados.Count; i++)
        {
            string nome = enumerados[i].NomeGdi;
            if (mapa is not null && mapa.TryGetValue(nome, out string? chave) && usadas.Add(chave))
                chaves[i] = new ChaveAtribuida(nome, chave, OrigemDaChave.Caminho);
        }

        // 2. Do cache, 3. reserva.
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

    // Depois de uma consulta boa. Mantém também as que vieram do cache (monitor que a
    // consulta não trouxe dessa vez); senão a próxima consulta negada o jogava na reserva.
    // Reserva nunca entra no cache.
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

    private static bool MesmoTamanho(RetanguloPx a, RetanguloPx b) => a.Largura == b.Largura && a.Altura == b.Altura;

    private static ChaveAtribuida Reserva(string nome, HashSet<string> usadas)
    {
        string chave = DeReserva(nome);
        usadas.Add(chave);
        return new ChaveAtribuida(nome, chave, OrigemDaChave.Reserva);
    }
}
