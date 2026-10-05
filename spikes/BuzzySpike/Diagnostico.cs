using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BuzzySpike;

/// <summary>
/// Registro de evidência dos protótipos. Escreve num arquivo de texto dentro de
/// spikes/resultados/ e em nenhum outro lugar. Não usa rede nem pasta de dados do usuário.
/// </summary>
internal static class Diagnostico
{
    private static readonly object _trava = new();
    private static string _arquivo = "";
    private static readonly Stopwatch _cronometro = Stopwatch.StartNew();

    internal static string Arquivo => _arquivo;

    internal static void Iniciar(string modo)
    {
        // A pasta de resultados fica ao lado do projeto do protótipo, subindo a partir de
        // bin/<config>/<tfm>/. Se o caminho esperado não existir, cai para a pasta do binário.
        string baseDir = AppContext.BaseDirectory;
        string? candidato = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "resultados"));
        string destino = Directory.Exists(candidato) ? candidato : baseDir;

        _arquivo = Path.Combine(destino, $"{modo}.log");

        var cab = new StringBuilder();
        cab.AppendLine("================================================================");
        cab.AppendLine($"Sessão      : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        cab.AppendLine($"Modo        : {modo}");
        cab.AppendLine($"PID         : {Environment.ProcessId}");
        cab.AppendLine($"Runtime     : {RuntimeInformation.FrameworkDescription}");
        cab.AppendLine($"Arquitetura : {RuntimeInformation.ProcessArchitecture} em {RuntimeInformation.OSArchitecture}");
        cab.AppendLine($"SO          : {RuntimeInformation.OSDescription}");
        cab.AppendLine($"Limiar arraste do sistema (SM_CXDRAG x SM_CYDRAG): "
            + $"{Interop.GetSystemMetrics(Interop.SM_CXDRAG)} x {Interop.GetSystemMetrics(Interop.SM_CYDRAG)} px");

        if (Interop.NtQueryTimerResolution(out uint min, out uint max, out uint atual) == 0)
        {
            cab.AppendLine($"Resolução do timer global: atual {atual / 10000.0:0.000} ms "
                + $"(faixa {max / 10000.0:0.000} a {min / 10000.0:0.000} ms)");
        }
        else
        {
            cab.AppendLine("Resolução do timer global: NÃO MEDIDA (consulta falhou)");
        }

        cab.AppendLine("================================================================");

        lock (_trava)
        {
            File.AppendAllText(_arquivo, cab.ToString(), Encoding.UTF8);
        }
    }

    internal static void Linha(string texto)
    {
        string carimbo = _cronometro.Elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        lock (_trava)
        {
            File.AppendAllText(_arquivo, $"[{carimbo}] {texto}{Environment.NewLine}", Encoding.UTF8);
        }
    }

    internal static void Bloco(string titulo, IEnumerable<string> linhas)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"---- {titulo} ----");
        foreach (string l in linhas) sb.AppendLine("     " + l);
        lock (_trava)
        {
            File.AppendAllText(_arquivo, sb.ToString(), Encoding.UTF8);
        }
    }

    /// <summary>Resolução do timer global, em milissegundos, ou null se a consulta falhar.</summary>
    internal static double? ResolucaoTimerMs()
        => Interop.NtQueryTimerResolution(out _, out _, out uint atual) == 0 ? atual / 10000.0 : null;
}
