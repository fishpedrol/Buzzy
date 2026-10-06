using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes.Apoio;

/// <summary>Caminhos do repositório usados pelos testes. Tudo aqui é só leitura.</summary>
internal static class Repositorio
{
    /// <summary>Raiz do repositório: a primeira pasta acima dos binários com Buzzy.Build.props e global.json.</summary>
    public static string Raiz()
    {
        for (DirectoryInfo? pasta = new(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            if (File.Exists(Path.Combine(pasta.FullName, "Buzzy.Build.props")) && File.Exists(Path.Combine(pasta.FullName, "global.json")))
                return pasta.FullName;
        }
        Afirmar.Falhar($"Raiz do repositório não encontrada acima de {AppContext.BaseDirectory}.");
        return "";
    }

    public static string PastaDoSpike => Path.Combine(Raiz(), "spikes", "BuzzySpike");

    public static string BinariosDoSpike => Path.Combine(PastaDoSpike, "bin", "Release", "net10.0-windows");

    /// <summary>
    /// Um apphost real gerado pelo SDK: o executável do protótipo. Falha com mensagem clara se
    /// ele não existir, em vez de pular o teste.
    /// </summary>
    public static string ApphostReal()
    {
        string caminho = Path.Combine(BinariosDoSpike, "BuzzySpike.exe");
        if (!File.Exists(caminho))
        {
            Afirmar.Falhar(
                $"Apphost real não encontrado: {caminho}. Este teste lê o lançador que o SDK gera; " +
                "compile o protótipo antes (dotnet build spikes/BuzzySpike -c Release), sem executá-lo.");
        }
        return caminho;
    }

    /// <summary>
    /// O singlefilehost.exe do pacote de host do .NET instalado, na versão do runtime que roda os testes (F9-P10): a pasta
    /// do runtime é dotnet\shared\Microsoft.NETCore.App\&lt;versão&gt;, e o host fica em dotnet\packs. Falha com mensagem
    /// clara se ele não existir.
    /// </summary>
    public static string HostDeArquivoUnicoReal()
    {
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string dotnet = Path.GetFullPath(Path.Combine(runtime, "..", "..", ".."));
        string caminho = Path.Combine(dotnet, "packs", "Microsoft.NETCore.App.Host.win-x64", Path.GetFileName(runtime), "runtimes", "win-x64", "native", "singlefilehost.exe");
        if (!File.Exists(caminho))
            Afirmar.Falhar($"Host de arquivo único não encontrado: {caminho}. Ele vem com o SDK do .NET (pacote Microsoft.NETCore.App.Host.win-x64).");
        return caminho;
    }
}

/// <summary>Pasta temporária apagada no fim do teste.</summary>
internal sealed class PastaTemporaria : IDisposable
{
    public PastaTemporaria() => Caminho = Directory.CreateTempSubdirectory("buzzy-portao-").FullName;

    public string Caminho { get; }

    public string Subpasta(string relativo)
    {
        string caminho = Path.Combine(Caminho, relativo);
        Directory.CreateDirectory(caminho);
        return caminho;
    }

    public string Escrever(string relativo, string conteudo)
    {
        string caminho = Path.Combine(Caminho, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Caminho, recursive: true);
        }
        catch (IOException)
        {
            // Pasta temporária presa por outro processo: o sistema limpa depois.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
