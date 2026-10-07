using System.Security.Cryptography;

namespace Buzzy.PortaoApis;

// Build autocontido: binário que não é do Buzzy só passa com mesmo nome e SHA-256 de um arquivo
// dos pacotes de runtime da Microsoft (runtimes/<rid>/lib ou native, na pasta do NuGet). Assim nada
// de terceiros entra e runtime alterado reprova (BZP006), sem ter que ler código da Microsoft.
internal sealed class ProcedenciaDoRuntime
{
    private readonly Dictionary<string, HashSet<string>> _hashesPorNome = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Pastas { get; }
    public int Arquivos { get; private set; }

    private ProcedenciaDoRuntime(IReadOnlyList<string> pastas) => Pastas = pastas;

    // Pasta sem runtimes/*/lib nem runtimes/*/native é erro de uso.
    public static ProcedenciaDoRuntime Indexar(IReadOnlyList<string> pastas)
    {
        ArgumentNullException.ThrowIfNull(pastas);
        // O MSBuild pode passar o mesmo pacote duas vezes.
        string[] distintas = [.. pastas.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)];
        var procedencia = new ProcedenciaDoRuntime(distintas);
        foreach (string pasta in distintas)
        {
            string runtimes = Path.Combine(pasta, "runtimes");
            if (!Directory.Exists(runtimes)) throw new ErroDeUso($"--runtime {pasta}: sem a pasta runtimes; não é um pacote de runtime.");
            int lidos = 0;
            foreach (string rid in Directory.EnumerateDirectories(runtimes))
            {
                foreach (string sub in new[] { "lib", "native" })
                {
                    string raiz = Path.Combine(rid, sub);
                    if (!Directory.Exists(raiz)) continue;
                    foreach (string arquivo in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories))
                    {
                        procedencia.Acrescentar(Path.GetFileName(arquivo), Hash(File.ReadAllBytes(arquivo)));
                        lidos++;
                    }
                }
            }
            if (lidos == 0) throw new ErroDeUso($"--runtime {pasta}: nenhum arquivo em runtimes/*/lib nem runtimes/*/native.");
        }
        return procedencia;
    }

    public bool EhDoRuntime(string nome, ReadOnlySpan<byte> conteudo)
        => _hashesPorNome.TryGetValue(Path.GetFileName(nome), out HashSet<string>? hashes) && hashes.Contains(Hash(conteudo));

    public static string Hash(ReadOnlySpan<byte> conteudo) => Convert.ToHexString(SHA256.HashData(conteudo));

    private void Acrescentar(string nome, string hash)
    {
        if (!_hashesPorNome.TryGetValue(nome, out HashSet<string>? hashes))
            _hashesPorNome[nome] = hashes = new HashSet<string>(StringComparer.Ordinal);
        if (hashes.Add(hash)) Arquivos++;
    }
}
