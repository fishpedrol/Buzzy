using System.Text;
using System.Text.RegularExpressions;

namespace SondaP3;

// Lê só os logs do próprio spike (protótipo e receptor), nunca outro app.
internal sealed class LogArquivo(string caminho)
{
    internal string Caminho => caminho;

    internal List<string> Linhas()
    {
        if (!File.Exists(caminho)) return [];
        using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<string>();
        string? l;
        while ((l = sr.ReadLine()) is not null) linhas.Add(l);
        return linhas;
    }

    internal int Contar() => Linhas().Count;

    internal List<string> Desde(int marca) => [.. Linhas().Skip(marca)];

    internal string? EsperarLinha(int marca, string contem, int limiteMs)
    {
        string? achada = null;
        EsperarAte(marca, ls => (achada = ls.FirstOrDefault(l => l.Contains(contem, StringComparison.Ordinal))) is not null, limiteMs);
        return achada;
    }

    internal bool EsperarAte(int marca, Func<List<string>, bool> condicao, int limiteMs)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            if (condicao(Desde(marca))) return true;
            if (DateTime.UtcNow > fim) return false;
            Thread.Sleep(100);
        }
    }
}

// Montado a partir das linhas que o protótipo logou.
internal sealed record AnaliseGesto(
    int Pressionares,
    int ArrastesIniciados,
    int ArrastesDetectadosNoSoltar,
    int SoltarArraste,
    int SoltarClique,
    int CapturasPerdidas,
    int CapturasLiberadasAoSoltar,
    IReadOnlyList<string> Motivos,
    Nativo.POINT? PosicaoFinal,
    bool CapturaZeroNoFim,
    int Problemas,
    int FocoMudou,
    bool PerdaAntesDeSoltar,
    string? MonitorFinal,
    string? Latencia)
{
    internal int Fins => Motivos.Count;

    private static readonly Regex RePosicao = new(@"Posição final\s+: \((-?\d+),(-?\d+)\)", RegexOptions.Compiled);
    private static readonly Regex ReCapturaZero = new(@"Captura agora\s+: 0 ", RegexOptions.Compiled);

    internal static AnaliseGesto De(List<string> ls)
    {
        int IndiceDe(string trecho) => ls.FindIndex(l => l.Contains(trecho, StringComparison.Ordinal));

        var posicoes = ls.Select(l => RePosicao.Match(l)).Where(m => m.Success).ToList();
        Nativo.POINT? posFinal = posicoes.Count == 0
            ? null
            : new Nativo.POINT(int.Parse(posicoes[^1].Groups[1].Value), int.Parse(posicoes[^1].Groups[2].Value));

        var capturas = ls.Where(l => l.Contains("Captura agora", StringComparison.Ordinal)).ToList();
        int iPerda = IndiceDe("WM_CAPTURECHANGED: captura perdida");
        int iSoltar = IndiceDe("] SOLTAR em cliente");

        return new AnaliseGesto(
            Pressionares: ls.Count(l => l.Contains("] PRESSIONAR:", StringComparison.Ordinal)),
            ArrastesIniciados: ls.Count(l => l.Contains("ARRASTE iniciado", StringComparison.Ordinal)),
            ArrastesDetectadosNoSoltar: ls.Count(l => l.Contains("ARRASTE detectado no soltar", StringComparison.Ordinal)),
            SoltarArraste: ls.Count(l => l.Contains("] SOLTAR em cliente", StringComparison.Ordinal) && l.Contains("como ARRASTE", StringComparison.Ordinal)),
            SoltarClique: ls.Count(l => l.Contains("] SOLTAR em cliente", StringComparison.Ordinal) && l.Contains("como CLIQUE", StringComparison.Ordinal)),
            CapturasPerdidas: ls.Count(l => l.Contains("WM_CAPTURECHANGED: captura perdida", StringComparison.Ordinal)),
            CapturasLiberadasAoSoltar: ls.Count(l => l.Contains("WM_CAPTURECHANGED: captura liberada por nós", StringComparison.Ordinal)),
            Motivos: [.. ls.Where(l => l.Contains("Motivo do término", StringComparison.Ordinal)).Select(l => l.Split(':', 2)[1].Trim())],
            PosicaoFinal: posFinal,
            CapturaZeroNoFim: capturas.Count > 0 && capturas.All(l => ReCapturaZero.IsMatch(l)),
            Problemas: ls.Count(l => l.Contains("PROBLEMA", StringComparison.Ordinal)),
            FocoMudou: ls.Count(l => l.Contains("Foco mudou no gesto?: SIM", StringComparison.Ordinal)),
            PerdaAntesDeSoltar: iPerda >= 0 && (iSoltar < 0 || iPerda < iSoltar),
            MonitorFinal: ls.LastOrDefault(l => l.Contains("Monitor final", StringComparison.Ordinal))?.Split(':', 2)[1].Trim(),
            Latencia: ls.LastOrDefault(l => l.Contains("M5 latência", StringComparison.Ordinal))?.Trim());
    }

    internal string Resumo() =>
        $"pressionar={Pressionares} arraste={ArrastesIniciados}+{ArrastesDetectadosNoSoltar}(no soltar) "
        + $"soltar[ARRASTE={SoltarArraste} CLIQUE={SoltarClique}] captura[perdida={CapturasPerdidas} liberada={CapturasLiberadasAoSoltar}] "
        + $"fins={Fins} ({string.Join("; ", Motivos)}) captura zero no fim={CapturaZeroNoFim} problemas={Problemas}";
}

// Escreve no console e em spikes/resultados/ ao mesmo tempo.
internal sealed class Relatorio : IDisposable
{
    private readonly StreamWriter _arquivo;

    internal Relatorio(string caminho)
    {
        _arquivo = new StreamWriter(caminho, append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }

    internal void Linha(string texto)
    {
        Console.WriteLine(texto);
        _arquivo.WriteLine(texto);
    }

    public void Dispose() => _arquivo.Dispose();
}
