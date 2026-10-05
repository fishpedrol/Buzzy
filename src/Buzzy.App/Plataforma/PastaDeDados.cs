using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Pastas em que o Buzzy grava (SECURITY.md 3.1 e 5): tudo fica em <c>%LOCALAPPDATA%\Buzzy</c>, obtida pela
/// consulta de pasta conhecida do Windows, nunca por variável de ambiente nem por caminho relativo.
///
/// Os perfis de teste (<c>--perfil-de-teste NOME</c>, Fase 5) ficam em <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c>:
/// os testes e as ferramentas que abrem o Buzzy nunca leem nem gravam as configurações reais do usuário. O nome
/// é validado de forma que o caminho nunca saia dessa pasta: sem separador, sem ponto, sem nome reservado do
/// Windows; um nome inválido não tem pasta, e quem pediu o perfil desliga a persistência (falha fechada).
/// </summary>
internal static class PastaDeDados
{
    /// <summary>Nome da pasta do Buzzy dentro da pasta local do usuário.</summary>
    internal const string NomeDaPasta = "Buzzy";

    /// <summary>Nome da pasta dos perfis de teste dentro da pasta do Buzzy.</summary>
    internal const string NomeDaPastaDeTestes = "testes";

    /// <summary>Comprimento máximo do nome de um perfil de teste.</summary>
    internal const int ComprimentoMaximoDoPerfil = 32;

    /// <summary>
    /// Nomes de dispositivo que o Windows reserva em qualquer pasta (CON, PRN, AUX, NUL, COM0–COM9, LPT0–LPT9):
    /// uma pasta com um deles não é uma pasta comum.
    /// </summary>
    private static readonly string[] NomesReservados =
    [
        "con", "prn", "aux", "nul",
        "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    /// <summary>
    /// <c>%LOCALAPPDATA%\Buzzy</c>, pela pasta conhecida do Windows, sem exigir que ela exista; nulo se o Windows
    /// não informar a pasta local do usuário. A pasta não é criada aqui: só na primeira gravação.
    /// </summary>
    internal static string? DoBuzzy()
        => DoBuzzy(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));

    /// <summary>
    /// A pasta do Buzzy dentro de <paramref name="pastaLocal"/>; nulo se ela vier vazia ou não for um caminho
    /// completo. Sem isso, <c>Path.Combine("", "Buzzy")</c> daria o caminho relativo <c>Buzzy</c>, que cairia na
    /// pasta atual do processo.
    /// </summary>
    internal static string? DoBuzzy(string? pastaLocal)
        => string.IsNullOrEmpty(pastaLocal) || !Path.IsPathFullyQualified(pastaLocal) ? null : Path.Combine(pastaLocal, NomeDaPasta);

    /// <summary>
    /// Se <paramref name="nome"/> serve de perfil de teste: de 1 a 32 caracteres, só letras minúsculas de a a z,
    /// algarismos de 0 a 9 e hífen, sem começar por hífen, e fora dos nomes reservados do Windows. Letra acentuada,
    /// maiúscula, ponto, espaço, barra e fim de linha ficam de fora.
    /// </summary>
    internal static bool NomeDePerfilValido([NotNullWhen(true)] string? nome)
    {
        if (string.IsNullOrEmpty(nome) || nome.Length > ComprimentoMaximoDoPerfil) return false;
        for (int i = 0; i < nome.Length; i++)
        {
            char c = nome[i];
            bool permitido = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') || (c == '-' && i > 0);
            if (!permitido) return false;
        }
        return !NomesReservados.Contains(nome, StringComparer.Ordinal);
    }

    /// <summary>
    /// <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c>; nulo se o nome for inválido (<see cref="NomeDePerfilValido"/>) ou se
    /// não houver pasta do Buzzy. Pelo nome validado, o caminho é sempre uma subpasta direta de <c>testes</c>.
    /// </summary>
    internal static string? DoPerfilDeTeste(string? nome) => DoPerfilDeTeste(nome, DoBuzzy());

    /// <summary>
    /// Pasta das configurações nesta execução, pelas opções da linha de comando (Fase 5), na pasta do Buzzy do
    /// Windows (<see cref="DoBuzzy()"/>). Ver <see cref="DasConfiguracoes(string?, bool, string?)"/>.
    /// </summary>
    internal static string? DasConfiguracoes(string? perfilDeTeste, bool persistenciaDesligada)
        => DasConfiguracoes(perfilDeTeste, persistenciaDesligada, DoBuzzy());

    /// <summary>
    /// Pasta das configurações nesta execução, com a falha fechada numa regra só: com a persistência desligada,
    /// nenhuma; com um perfil de teste, a pasta dele dentro de <paramref name="pastaDoBuzzy"/>, e nenhuma se o nome
    /// for inválido; sem perfil, a própria <paramref name="pastaDoBuzzy"/>; sem ela, nenhuma. Com um perfil pedido,
    /// o resultado nunca é a pasta real: um perfil sem pasta desliga a persistência, em vez de cair nela. Nula, nada
    /// é lido nem gravado como configuração.
    /// </summary>
    internal static string? DasConfiguracoes(string? perfilDeTeste, bool persistenciaDesligada, string? pastaDoBuzzy)
    {
        if (persistenciaDesligada) return null;
        return perfilDeTeste is null ? pastaDoBuzzy : DoPerfilDeTeste(perfilDeTeste, pastaDoBuzzy);
    }

    private static string? DoPerfilDeTeste(string? nome, string? pastaDoBuzzy)
        => NomeDePerfilValido(nome) && pastaDoBuzzy is not null ? Path.Combine(pastaDoBuzzy, NomeDaPastaDeTestes, nome) : null;
}
