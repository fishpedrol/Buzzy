using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Buzzy.App.Plataforma;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 9, passo F9-P1 (DEC-040, itens 1 a 4): a escrita em disco do produto só em dois arquivos; elevado, nada antes da
/// recusa; o P/Invoke só no System32; e o log de diagnóstico com o limite em falha fechada.
/// </summary>
internal sealed class CercasDeEscritaTestes
{
    private static IEnumerable<string> FontesDoProduto() => Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !Regex.IsMatch(f, @"[\/](obj|bin)[\/]"));

    private static string SemComentarios(string codigo) => Regex.Replace(codigo, @"//.*|/\*[\s\S]*?\*/", "");

    // Item 1: File, Directory, FileStream, FileInfo, StreamWriter, caminhos temporários, armazenamento isolado e diálogos
    // de salvar só nos dois arquivos que gravam (as configurações e o log); o único Save do produto vai para a memória.
    [Teste]
    public void Escrita_SoNosDoisArquivosQueGravam()
    {
        var es = new Regex(@"\b(File|Directory|FileInfo|DirectoryInfo|FileStream|StreamWriter|BinaryWriter|FileSystemInfo|IsolatedStorage\w*|SaveFileDialog|FileMode|FileAccess)\b|Path\.GetTemp|\bSafeFileHandle\b|CreateFile\w*\(");
        string[] quem = [.. FontesDoProduto().Where(f => es.IsMatch(SemComentarios(File.ReadAllText(f)))).Select(f => Path.GetRelativePath(Path.Combine(Caminhos.Raiz, "src"), f)).Order(StringComparer.Ordinal)];
        Afirmar.Sequencia([@"Buzzy.App\Plataforma\ArquivoDeConfiguracoes.cs", @"Buzzy.App\Plataforma\Diagnostico.cs"], quem, "quem usa as APIs de arquivo");
        var salvar = new Regex(@"\.Save\(([^)]*)\)");
        foreach (string f in FontesDoProduto())
        {
            foreach (Match m in salvar.Matches(SemComentarios(File.ReadAllText(f))))
                Afirmar.Igual("memoria", m.Groups[1].Value.Trim(), $"{Path.GetFileName(f)}: o Save vai para a memória");
        }
    }

    // Item 2: a recusa de rodar elevado vem antes do log, das opções, da instância única e da aplicação.
    [Teste]
    public void Elevado_RecusaAntesDeTudo()
    {
        string codigo = SemComentarios(File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Programa.cs")));
        int main = codigo.IndexOf("internal static int Main(", StringComparison.Ordinal);
        int recusa = codigo.IndexOf("if (Environment.IsPrivilegedProcess)", main, StringComparison.Ordinal);
        Afirmar.Verdadeiro(main > 0 && recusa > main, "a recusa está no Main");
        string antes = codigo[main..recusa];
        Afirmar.Igual("internalstaticintMain(string[]argumentos){", Regex.Replace(antes, @"\s", ""), "nada entre o Main e a recusa");
        int fimDoBloco = codigo.IndexOf('}', recusa);
        string bloco = codigo[recusa..fimDoBloco];
        Afirmar.Contem("return CodigosDeSaida.Elevado;", bloco);
        Afirmar.Falso(Regex.IsMatch(bloco, @"Diagnostico\.|File\.|Directory\."), $"o bloco não toca o disco: {bloco}");
        foreach (string depois in new[] { "Diagnostico.Ligar(", "LerOpcoes(", "InstanciaUnica.Obter(", "new Application" })
        {
            int i = codigo.IndexOf(depois, main, StringComparison.Ordinal);
            Afirmar.Verdadeiro(i < 0 || i > fimDoBloco, $"{depois} depois da recusa");
        }
        Afirmar.Igual(5, CodigosDeSaida.Elevado, "o código 5");
    }

    // Item 3: o P/Invoke do produto só procura DLLs no System32, e todas as DLLs dele são do sistema.
    [Teste]
    public void PInvoke_SoNoSystem32()
    {
        Assembly app = typeof(CodigosDeSaida).Assembly;
        Afirmar.Igual(DllImportSearchPath.System32, app.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>()?.Paths, "o atributo no assembly do app");
        string[] dlls = [.. FontesDoProduto().SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?:DllImport|LibraryImport)\(""([^""]+)""").Select(m => m.Groups[1].Value.ToLowerInvariant())).Distinct().Order(StringComparer.Ordinal)];
        foreach (string dll in dlls)
            Afirmar.Verdadeiro(File.Exists(Path.Combine(Environment.SystemDirectory, dll)), $"{dll} existe no System32");
        Afirmar.Verdadeiro(dlls.Length >= 5, $"as DLLs: {string.Join(", ", dlls)}");
    }

    // Item 4: o log gira ao passar do limite (uma cópia só); sem conseguir girar, para de gravar ao passar de duas vezes o
    // limite; e um log que é link não é usado.
    [Teste]
    public void Log_GiraNoLimite_EFalhaFechado()
    {
        string pasta = Directory.CreateTempSubdirectory("buzzy-log-").FullName;
        try
        {
            Diagnostico.Ligar(pasta, 2000);
            for (int i = 0; i < 200; i++) Diagnostico.Evento("TESTE", ("linha", i), ("enchimento", new string('x', 40)));
            string log = Path.Combine(pasta, "diagnostico.log"), copia = Path.Combine(pasta, "diagnostico.1.log");
            Afirmar.Verdadeiro(File.Exists(copia), "girou");
            Afirmar.Verdadeiro(new FileInfo(log).Length <= 2000 + 200 && new FileInfo(copia).Length <= 2000 + 200, $"cada um perto do limite: {new FileInfo(log).Length} e {new FileInfo(copia).Length}");
            Afirmar.Sequencia(["diagnostico.1.log", "diagnostico.log"], Directory.GetFiles(pasta).Select(Path.GetFileName).Order(StringComparer.Ordinal).Cast<string>(), "dois arquivos no máximo");

            // A cópia presa (aberta sem compartilhar a exclusão): a rotação falha, e o log para em duas vezes o limite.
            using (new FileStream(copia, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (int i = 0; i < 400; i++) Diagnostico.Evento("TESTE", ("linha", i), ("enchimento", new string('y', 40)));
                long tamanho = new FileInfo(log).Length;
                Afirmar.Verdadeiro(tamanho <= 2 * 2000 + 200, $"sem girar, para em duas vezes o limite: {tamanho}");
            }

            // Um log que é link simbólico (com o modo de desenvolvedor) não é usado.
            Diagnostico.Desligar();
            File.Delete(log);
            string deFora = Path.Combine(pasta, "..", $"de-fora-{Guid.NewGuid():N}.txt");
            File.WriteAllText(deFora, "fora");
            try
            {
                File.CreateSymbolicLink(log, deFora);
                Diagnostico.Ligar(pasta, 2000);
                Afirmar.Falso(Diagnostico.Ligado, "o log que é link fica desligado");
                Diagnostico.Evento("TESTE", ("linha", 1));
                Afirmar.Igual("fora", File.ReadAllText(deFora), "nada gravado através do link");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"         link simbólico: sem permissão para criar ({e.GetType().Name}); só a rotação foi conferida");
            }
            finally
            {
                File.Delete(deFora);
            }
        }
        finally
        {
            Diagnostico.Desligar();
            Directory.Delete(pasta, recursive: true);
        }
    }
}
