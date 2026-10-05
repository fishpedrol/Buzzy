using System.Collections;

namespace Buzzy.Testes;

/// <summary>Uma afirmação de teste que não se confirmou.</summary>
public sealed class FalhaDeAfirmacao(string mensagem) : Exception(mensagem);

/// <summary>Afirmações usadas pelos testes. Cada uma lança <see cref="FalhaDeAfirmacao"/>.</summary>
public static class Afirmar
{
    public static void Verdadeiro(bool condicao, string? mensagem = null)
    {
        if (!condicao) Falhar(mensagem ?? "esperado verdadeiro, obtido falso");
    }

    public static void Falso(bool condicao, string? mensagem = null)
    {
        if (condicao) Falhar(mensagem ?? "esperado falso, obtido verdadeiro");
    }

    public static void Igual<T>(T esperado, T obtido, string? mensagem = null)
    {
        if (!EqualityComparer<T>.Default.Equals(esperado, obtido))
            Falhar($"{Prefixo(mensagem)}esperado <{Descrever(esperado)}>, obtido <{Descrever(obtido)}>");
    }

    public static void Diferente<T>(T naoEsperado, T obtido, string? mensagem = null)
    {
        if (EqualityComparer<T>.Default.Equals(naoEsperado, obtido))
            Falhar($"{Prefixo(mensagem)}não esperava <{Descrever(obtido)}>");
    }

    public static void Aproximado(double esperado, double obtido, double tolerancia, string? mensagem = null)
    {
        if (double.IsNaN(obtido) || Math.Abs(esperado - obtido) > tolerancia)
            Falhar($"{Prefixo(mensagem)}esperado {esperado} ± {tolerancia}, obtido {obtido}");
    }

    public static void Nulo(object? valor, string? mensagem = null)
    {
        if (valor is not null) Falhar($"{Prefixo(mensagem)}esperado nulo, obtido <{Descrever(valor)}>");
    }

    public static T NaoNulo<T>(T? valor, string? mensagem = null) where T : class
    {
        if (valor is null) Falhar($"{Prefixo(mensagem)}esperado um valor, obtido nulo");
        return valor!;
    }

    public static T NaoNulo<T>(T? valor, string? mensagem = null) where T : struct
    {
        if (valor is null) Falhar($"{Prefixo(mensagem)}esperado um valor, obtido nulo");
        return valor!.Value;
    }

    public static void Contem(string trecho, string? texto, string? mensagem = null)
    {
        if (texto is null || !texto.Contains(trecho, StringComparison.Ordinal))
            Falhar($"{Prefixo(mensagem)}esperado conter \"{trecho}\", obtido \"{texto}\"");
    }

    public static void Sequencia<T>(IEnumerable<T> esperado, IEnumerable<T> obtido, string? mensagem = null)
    {
        List<T> e = [.. esperado];
        List<T> o = [.. obtido];
        if (!e.SequenceEqual(o))
            Falhar($"{Prefixo(mensagem)}esperado [{string.Join(", ", e.Select(x => Descrever(x)))}], obtido [{string.Join(", ", o.Select(x => Descrever(x)))}]");
    }

    public static TExcecao Lanca<TExcecao>(Action acao, string? mensagem = null) where TExcecao : Exception
    {
        try
        {
            acao();
        }
        catch (TExcecao e)
        {
            return e;
        }
        catch (Exception e)
        {
            Falhar($"{Prefixo(mensagem)}esperado {typeof(TExcecao).Name}, obtido {e.GetType().Name}: {e.Message}");
        }
        Falhar($"{Prefixo(mensagem)}esperado {typeof(TExcecao).Name}, nada foi lançado");
        return null!;
    }

    public static void Falhar(string mensagem) => throw new FalhaDeAfirmacao(mensagem);

    private static string Prefixo(string? mensagem) => mensagem is null ? "" : mensagem + ": ";

    private static string Descrever(object? valor) => valor switch
    {
        null => "nulo",
        string s => $"\"{s}\"",
        IEnumerable e and not string => "[" + string.Join(", ", e.Cast<object?>().Select(Descrever)) + "]",
        _ => valor.ToString() ?? "",
    };
}
