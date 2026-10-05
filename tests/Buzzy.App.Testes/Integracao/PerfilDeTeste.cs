using System.IO;
using Buzzy.App.Plataforma;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Perfil de teste do Buzzy aberto pelos testes (<c>--perfil-de-teste NOME</c>, Fase 5): os dados dele ficam em
/// <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c> (<see cref="PastaDeDados.DoPerfilDeTeste"/>), nunca nas configurações reais
/// do usuário. Antes de abrir o Buzzy, o teste apaga a pasta do perfil, para ele partir sem posição salva; a limpeza
/// nunca apaga nada fora dela. A verificação de tela (Buzzy.Verificacao) compila este arquivo e usa a mesma limpeza.
/// </summary>
internal static class PerfilDeTeste
{
    /// <summary>Perfil dos testes de integração.</summary>
    internal const string Integracao = "integracao";

    /// <summary>A pasta do perfil dentro de <paramref name="pastaDoBuzzy"/>: <c>pastaDoBuzzy\testes\perfil</c>.</summary>
    internal static string Pasta(string perfil, string pastaDoBuzzy)
    {
        if (!PastaDeDados.NomeDePerfilValido(perfil))
            throw new ArgumentException($"Perfil de teste inválido: \"{perfil}\".", nameof(perfil));
        return Path.Combine(pastaDoBuzzy, PastaDeDados.NomeDaPastaDeTestes, perfil);
    }

    /// <summary>Apaga a pasta do perfil em <c>%LOCALAPPDATA%\Buzzy\testes</c>, se existir; devolve se apagou.</summary>
    internal static bool Limpar(string perfil)
        => Limpar(perfil, PastaDeDados.DoBuzzy() ?? throw new InvalidOperationException("O Windows não informou a pasta local do usuário."));

    /// <summary>
    /// Apaga <c>pastaDoBuzzy\testes\perfil</c>, se existir, com tudo o que houver dentro; devolve se apagou. Recusa um
    /// nome de perfil inválido (<see cref="PastaDeDados.NomeDePerfilValido"/>) e um caminho com junção ou link simbólico
    /// até a pasta do perfil: a limpeza nunca segue para fora da pasta do Buzzy. Cada trecho que existe é conferido antes
    /// de tudo, também quando a pasta do perfil ainda não existe: com a pasta do Buzzy ou a dos testes como junção, o Buzzy
    /// de teste e o próprio teste gravariam do outro lado (revisão de segurança do bloco P6-P9, achado 5).
    /// </summary>
    internal static bool Limpar(string perfil, string pastaDoBuzzy)
    {
        string pasta = Pasta(perfil, pastaDoBuzzy);
        string testes = Path.GetDirectoryName(pasta)!;
        foreach (string trecho in new[] { pastaDoBuzzy, testes, pasta })
        {
            if (Atributos(trecho) is { } atributos && (atributos & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"{trecho} é uma junção ou um link: a limpeza do perfil de teste não segue para fora da pasta do Buzzy.");
        }
        if (!Directory.Exists(pasta)) return false;
        Directory.Delete(pasta, recursive: true);
        return true;
    }

    /// <summary>Os atributos do próprio trecho (de uma junção, os dela, mesmo sem o destino); nulo se ele não existe.</summary>
    private static FileAttributes? Atributos(string trecho)
    {
        try
        {
            return File.GetAttributes(trecho);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
