using System.Diagnostics;
using System.IO;
using System.Text;
using Buzzy.Visual.Animacao;

namespace Buzzy.ValidadorDeClipes;

// Problemas saem no formato de erro do MSBuild (ARQUIVO: error BUZZY6: ...) pra cair na lista de erros.
// Saída: 0 ok, 1 com problemas, 2 erro de uso ou leitura.
internal static class Programa
{
    private const string Uso = "Uso: Buzzy.ValidadorDeClipes --manifesto CAMINHO";

    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length != 2 || args[0] != "--manifesto")
        {
            Console.Error.WriteLine(Uso);
            return 2;
        }
        string caminho = Path.GetFullPath(args[1]);
        string texto;
        try
        {
            texto = File.ReadAllText(caminho, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{caminho}: error BUZZY6: não foi possível ler o manifesto de clipes: {e.Message}");
            return 2;
        }

        var relogio = Stopwatch.StartNew();
        ManifestoDeClipes manifesto;
        try
        {
            manifesto = ManifestoDeClipes.Ler(texto);
        }
        catch (FormatException e)
        {
            Console.Error.WriteLine($"{caminho}: error BUZZY6: manifesto de clipes inválido: {e.Message}");
            return 1;
        }
        IReadOnlyList<string> problemas = global::Buzzy.Visual.Animacao.ValidadorDeClipes.Validar(manifesto);
        foreach (string problema in problemas)
            Console.Error.WriteLine($"{caminho}: error BUZZY6: {problema}");
        if (problemas.Count > 0) return 1;
        Console.WriteLine($"Manifesto de clipes aprovado: {manifesto.Clipes.Count} clipes, {manifesto.Clipes.Sum(c => c.Quadros.Count)} quadros, "
            + $"todas as {Situacoes.Todas.Count} situações, alfa só 0 ou 255 ({relogio.ElapsedMilliseconds} ms).");
        return 0;
    }
}
