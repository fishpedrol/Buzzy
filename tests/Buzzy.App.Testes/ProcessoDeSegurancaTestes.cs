using System.IO;
using System.Text.RegularExpressions;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 9, passo F9-P4 (DEC-040, item 8; SECURITY.md 8, itens 1, 2 e 7): o processo de segurança guardado por testes.
/// Nenhum pacote NuGet e o restore travado; o portão no build do app, com as três fontes e o manifesto; as propriedades de
/// segurança do build comum, importado por src, tests e tools; e nenhum segredo no repositório.
/// </summary>
internal sealed class ProcessoDeSegurancaTestes
{
    private static IEnumerable<string> ArquivosDoRepositorio(params string[] padroes)
        => padroes.SelectMany(p => Directory.GetFiles(Caminhos.Raiz, p, SearchOption.AllDirectories))
            .Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin|\.git|\.vs|resultados)[\\/]"));

    [Teste]
    public void Dependencias_NenhumPacoteNuGet_ELockfilesSoDeProjetos()
    {
        string[] comPacote = [.. ArquivosDoRepositorio("*.csproj", "*.props", "*.targets")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"<(PackageReference|PackageVersion|GlobalPackageReference)\b"))
            .Select(f => Path.GetRelativePath(Caminhos.Raiz, f))];
        Afirmar.Sequencia([], comPacote, "nenhum PackageReference (DEC-016, item 2)");
        string[] lockfiles = [.. ArquivosDoRepositorio("packages.lock.json")];
        Afirmar.Verdadeiro(lockfiles.Length >= 5, $"os lockfiles: {lockfiles.Length}");
        foreach (string f in lockfiles)
        {
            string[] tipos = [.. Regex.Matches(File.ReadAllText(f), @"""type"":\s*""(\w+)""").Select(m => m.Groups[1].Value).Distinct()];
            Afirmar.Verdadeiro(tipos.All(t => t == "Project"), $"{Path.GetRelativePath(Caminhos.Raiz, f)}: só projetos ({string.Join(", ", tipos)})");
        }
        string props = File.ReadAllText(Path.Combine(Caminhos.Raiz, "Buzzy.Build.props"));
        foreach (string p in new[] { "<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", "<RestoreLockedMode>true</RestoreLockedMode>", "<NuGetAudit>true</NuGetAudit>", "<NuGetAuditMode>all</NuGetAuditMode>", "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", "<Nullable>enable</Nullable>" })
            Afirmar.Contem(p, props);
        foreach (string pasta in new[] { "src", "tests", "tools" })
            Afirmar.Contem("Buzzy.Build.props", File.ReadAllText(Path.Combine(Caminhos.Raiz, pasta, "Directory.Build.props")));
    }

    [Teste]
    public void Portao_NoBuildDoApp_ComAsTresFontesEOManifesto()
    {
        string csproj = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Buzzy.App.csproj"));
        Match alvo = Regex.Match(csproj, @"<Target Name=""PortaoDeApisProibidas"" AfterTargets=""Build""[\s\S]*?</Target>");
        Afirmar.Verdadeiro(alvo.Success, "o alvo do portão roda depois do build");
        foreach (string trecho in new[] { "--binarios", @"--fonte &quot;$(MSBuildThisFileDirectory).&quot;", @"..\Buzzy.Core", @"..\Buzzy.Visual", @"--manifesto &quot;$(MSBuildThisFileDirectory)app.manifest&quot;" })
            Afirmar.Contem(trecho, alvo.Value);
        string testar = File.ReadAllText(Path.Combine(Caminhos.Raiz, "tools", "testar.ps1"));
        Afirmar.Contem("portão de APIs proibidas", testar);
        Afirmar.Contem("auditoria de pacotes vulneráveis", testar);
    }

    // SECURITY.md 8, item 7: nenhum segredo versionado. Extensões de chave e certificado e os padrões comuns de chaves
    // privadas e tokens, em todo arquivo de texto do repositório (fora de bin, obj e .git).
    [Teste]
    public void Repositorio_SemSegredos()
    {
        string[] chaves = [.. ArquivosDoRepositorio("*.pfx", "*.p12", "*.snk", "*.pem", "*.key", ".env").Select(f => Path.GetRelativePath(Caminhos.Raiz, f))];
        Afirmar.Sequencia([], chaves, "arquivos de chave ou certificado");
        var segredo = new Regex(@"-----BEGIN (RSA |EC |OPENSSH |DSA |)PRIVATE KEY-----|\bghp_[A-Za-z0-9]{30,}|\bgithub_pat_[A-Za-z0-9_]{30,}|\bAKIA[0-9A-Z]{16}\b|\bsk-[A-Za-z0-9]{32,}|\bxox[bpoa]-[A-Za-z0-9-]{10,}|\bAIza[0-9A-Za-z_-]{35}\b");
        string[] extensoes = [".cs", ".ps1", ".md", ".json", ".props", ".csproj", ".targets", ".xml", ".resx", ".manifest", ".txt", ".yml", ".yaml", ".config", ".slnx"];
        int lidos = 0;
        foreach (string f in ArquivosDoRepositorio("*").Where(f => extensoes.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            if (Path.GetFileName(f) == "ProcessoDeSegurancaTestes.cs") continue; // os padrões ficam aqui
            lidos++;
            Match m = segredo.Match(File.ReadAllText(f));
            Afirmar.Falso(m.Success, $"{Path.GetRelativePath(Caminhos.Raiz, f)}: parece um segredo ({(m.Success ? m.Value[..Math.Min(12, m.Value.Length)] : "")}…)");
        }
        Afirmar.Verdadeiro(lidos > 200, $"os arquivos de texto lidos: {lidos}");
        string gitignore = File.ReadAllText(Path.Combine(Caminhos.Raiz, ".gitignore"));
        foreach (string p in new[] { "*.pfx", "*.snk", "*.pem", ".env" }) Afirmar.Contem(p, gitignore);
    }
}
