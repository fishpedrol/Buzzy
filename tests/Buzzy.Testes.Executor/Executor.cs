using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Buzzy.Testes;

/// <summary>
/// Descobre e executa os métodos marcados com <see cref="TesteAttribute"/> de um assembly.
///
/// Opções de linha de comando:
///   --integracao   inclui os testes marcados com [Integracao] (abrem janelas na tela)
///   --filtro TEXTO só executa testes cujo nome completo contém TEXTO
///   --lista        só lista os testes encontrados
///
/// Código de saída: 0 se todos os executados passaram; 1 se algum falhou; 2 se nenhum
/// teste foi executado ou a linha de comando é inválida.
/// </summary>
public static class Executor
{
    private sealed record Caso(string Nome, MethodInfo Metodo, bool Integracao);

    public static int Executar(Assembly assembly, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        bool incluirIntegracao = false;
        bool soListar = false;
        string? filtro = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--integracao": incluirIntegracao = true; break;
                case "--lista": soListar = true; break;
                case "--filtro" when i + 1 < args.Length: filtro = args[++i]; break;
                default:
                    Console.Error.WriteLine($"Argumento não reconhecido: {args[i]}");
                    return 2;
            }
        }

        List<Caso> casos = Descobrir(assembly);
        if (filtro is not null)
            casos = [.. casos.Where(c => c.Nome.Contains(filtro, StringComparison.OrdinalIgnoreCase))];

        if (soListar)
        {
            foreach (Caso c in casos) Console.WriteLine((c.Integracao ? "[integração] " : "") + c.Nome);
            return casos.Count > 0 ? 0 : 2;
        }

        Console.WriteLine($"== {assembly.GetName().Name}: {casos.Count} teste(s) encontrados{(incluirIntegracao ? ", integração incluída" : "")} ==");

        int passou = 0, falhou = 0, ignorado = 0;
        var falhas = new List<string>();
        var total = Stopwatch.StartNew();

        foreach (Caso caso in casos)
        {
            if (caso.Integracao && !incluirIntegracao)
            {
                ignorado++;
                continue;
            }

            var relogio = Stopwatch.StartNew();
            string? erro = Rodar(caso.Metodo);
            relogio.Stop();

            if (erro is null)
            {
                passou++;
                Console.WriteLine($"[OK]     {caso.Nome} ({relogio.ElapsedMilliseconds} ms)");
            }
            else
            {
                falhou++;
                falhas.Add($"{caso.Nome}: {erro}");
                Console.WriteLine($"[FALHOU] {caso.Nome} ({relogio.ElapsedMilliseconds} ms)");
                Console.WriteLine("         " + erro.Replace("\n", "\n         ", StringComparison.Ordinal));
            }
        }

        Console.WriteLine($"== {passou} passaram, {falhou} falharam, {ignorado} de integração não executados; {total.Elapsed.TotalSeconds:0.00} s ==");
        if (falhas.Count > 0)
        {
            Console.WriteLine("Falhas:");
            foreach (string f in falhas) Console.WriteLine("  - " + f.Split('\n')[0]);
        }

        if (falhou > 0) return 1;
        return passou > 0 ? 0 : 2;
    }

    private static List<Caso> Descobrir(Assembly assembly)
    {
        var casos = new List<Caso>();
        foreach (Type tipo in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            bool classeIntegracao = tipo.GetCustomAttribute<IntegracaoAttribute>() is not null;
            foreach (MethodInfo m in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttribute<TesteAttribute>() is not null)
                         .OrderBy(m => m.MetadataToken))
            {
                if (m.GetParameters().Length != 0 || m.ReturnType != typeof(void))
                    throw new InvalidOperationException($"O teste {tipo.Name}.{m.Name} precisa ser void e sem parâmetros.");
                bool integracao = classeIntegracao || m.GetCustomAttribute<IntegracaoAttribute>() is not null;
                casos.Add(new Caso($"{tipo.Name}.{m.Name}", m, integracao));
            }
        }
        return casos;
    }

    private static string? Rodar(MethodInfo metodo)
    {
        object? instancia = null;
        try
        {
            if (!metodo.IsStatic)
                instancia = Activator.CreateInstance(metodo.DeclaringType!, nonPublic: true);
            metodo.Invoke(instancia, null);
            return null;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            return Descrever(e.InnerException);
        }
        catch (Exception e)
        {
            return Descrever(e);
        }
        finally
        {
            (instancia as IDisposable)?.Dispose();
        }
    }

    private static string Descrever(Exception e) => e is FalhaDeAfirmacao
        ? e.Message + LocalDaFalha(e)
        : $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}";

    /// <summary>Primeira linha da pilha fora do próprio executor: onde a afirmação falhou.</summary>
    private static string LocalDaFalha(Exception e)
    {
        string? linha = e.StackTrace?
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => !l.Contains("Buzzy.Testes.Afirmar", StringComparison.Ordinal) && l.Length > 0);
        return linha is null ? "" : $"\n{linha}";
    }
}
