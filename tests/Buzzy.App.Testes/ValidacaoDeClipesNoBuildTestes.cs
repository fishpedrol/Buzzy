using System.Diagnostics;
using System.IO;
using System.Text;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 6, passo F6-P3 (DEC-036, item 4; critérios 2 e 3): a ferramenta que o build do app roda depois de compilar,
/// executada de verdade sobre o manifesto do app e sobre cópias quebradas numa pasta temporária. O manifesto do app passa
/// (código 0); um sem o clipe de uma situação, com uma pose ou uma cara que a arte não tem, ou fora do formato reprova
/// (código 1), com a linha de erro do MSBuild (<c>error BUZZY6</c>) que o build mostra. E o csproj do app chama a
/// ferramenta depois do build, com o manifesto embutido.
/// </summary>
internal sealed class ValidacaoDeClipesNoBuildTestes
{
    private static string Manifesto => Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Apresentacao", "clipes.json");

    /// <summary>A ferramenta compilada na mesma configuração destes testes.</summary>
    private static string Ferramenta
    {
        get
        {
            string configuracao = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
            string dll = Path.Combine(Caminhos.Raiz, "tools", "Buzzy.ValidadorDeClipes", "bin", configuracao, "net10.0-windows", "Buzzy.ValidadorDeClipes.dll");
            Afirmar.Verdadeiro(File.Exists(dll), $"a ferramenta compilada em {dll}");
            return dll;
        }
    }

    private static (int Codigo, string Saida) Rodar(string manifesto)
    {
        var inicio = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        inicio.ArgumentList.Add(Ferramenta);
        inicio.ArgumentList.Add("--manifesto");
        inicio.ArgumentList.Add(manifesto);
        using Process p = Process.Start(inicio)!;
        Task<string> erro = p.StandardError.ReadToEndAsync();
        string saida = p.StandardOutput.ReadToEnd();
        Afirmar.Verdadeiro(p.WaitForExit(60_000), "a ferramenta terminou em 60 s");
        return (p.ExitCode, saida + erro.Result);
    }

    [Teste]
    public void OManifestoDoAppPassa_ECopiasQuebradasReprovamComOErroDoBuild()
    {
        (int codigo, string saida) = Rodar(Manifesto);
        Afirmar.Igual(0, codigo, $"o do app passa: {saida}");
        Afirmar.Contem("Manifesto de clipes aprovado", saida);

        string pasta = Path.Combine(Path.GetTempPath(), "buzzy-clipes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            string original = File.ReadAllText(Manifesto);
            (string Caso, string Texto, string Erro)[] quebrados =
            [
                ("sem o clipe da queda", string.Join('\n', original.Split('\n').Where(l => !l.Contains("\"situacao\": \"caindo\"", StringComparison.Ordinal))), "situação \"caindo\" sem clipe"),
                ("pose ausente", original.Replace("\"pose\": \"reagindo\"", "\"pose\": \"voando\"", StringComparison.Ordinal), "a pose \"voando\" não existe na arte"),
                ("cara ausente", original.Replace("\"cara\": \"surpreso\"", "\"cara\": \"zangado\"", StringComparison.Ordinal), "a cara \"zangado\" não existe na arte"),
                ("fora do formato", original.Replace("\"versao\": 1", "\"versao\": 9", StringComparison.Ordinal), "manifesto de clipes inválido"),
            ];
            foreach ((string caso, string texto, string erroEsperado) in quebrados)
            {
                Afirmar.Diferente(original, texto, $"{caso}: a cópia mudou");
                string arquivo = Path.Combine(pasta, $"{caso.Replace(' ', '-')}.json");
                File.WriteAllText(arquivo, texto);
                (int c, string s) = Rodar(arquivo);
                Afirmar.Igual(1, c, $"{caso}: reprova ({s})");
                Afirmar.Contem("error BUZZY6", s);
                Afirmar.Contem(erroEsperado, s);
            }
            Afirmar.Igual(2, Rodar(Path.Combine(pasta, "nao-existe.json")).Codigo, "arquivo ausente: erro de leitura");
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }

        // O csproj do app roda a ferramenta depois do build, sobre o manifesto que ele embute.
        string csproj = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Buzzy.App.csproj"));
        Afirmar.Contem("<Target Name=\"ValidacaoDoManifestoDeClipes\" AfterTargets=\"Build\"", csproj);
        Afirmar.Contem("--manifesto &quot;$(MSBuildThisFileDirectory)Apresentacao\\clipes.json&quot;", csproj);
        Afirmar.Contem("<EmbeddedResource Include=\"Apresentacao\\clipes.json\"", csproj);
    }
}
