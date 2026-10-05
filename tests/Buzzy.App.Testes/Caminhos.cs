using System.IO;

namespace Buzzy.App.Testes;

/// <summary>Caminhos do repositório, a partir da pasta do executável de testes.</summary>
internal static class Caminhos
{
    private static readonly Lazy<string> ConfiguracaoDeduzida = new(() => DeduzirConfiguracao(AppContext.BaseDirectory));

    internal static string Raiz { get; } = LocalizarRaiz();

    /// <summary>
    /// Configuração (Release ou Debug) deste executável de testes, deduzida da pasta
    /// <c>bin\&lt;configuração&gt;\</c> em que ele está. O Buzzy.exe testado é sempre o da
    /// mesma configuração, para um build Debug nunca testar um Buzzy Release antigo (e vice-versa).
    /// </summary>
    internal static string Configuracao => ConfiguracaoDeduzida.Value;

    internal static string ExeDoBuzzy()
        => Path.Combine(Raiz, "src", "Buzzy.App", "bin", Configuracao, "net10.0-windows", "Buzzy.exe");

    /// <summary>
    /// Nome da pasta logo abaixo de <c>bin</c> no caminho dado (<c>...\bin\Release\net10.0-windows\</c>
    /// dá <c>Release</c>, com ou sem uma pasta de runtime a mais no fim).
    /// </summary>
    internal static string DeduzirConfiguracao(string pastaDoExecutavel)
    {
        var dir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(pastaDoExecutavel));
        while (dir.Parent is { } pai)
        {
            if (string.Equals(pai.Name, "bin", StringComparison.OrdinalIgnoreCase)) return dir.Name;
            dir = pai;
        }
        throw new InvalidOperationException(
            $"Não deu para deduzir a configuração (Release ou Debug) da pasta {pastaDoExecutavel}: esperado ...\\bin\\<configuração>\\<framework>\\.");
    }

    private static string LocalizarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Buzzy.Build.props"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Raiz do repositório (Buzzy.Build.props) não encontrada.");
    }
}
