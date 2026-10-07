using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Buzzy.App.Plataforma;

// Tudo em %LOCALAPPDATA%\Buzzy, pela pasta conhecida do Windows: nunca por variável
// de ambiente nem caminho relativo.
//
// Perfis de teste (--perfil-de-teste NOME) ficam em ...\Buzzy\testes\NOME, pra testes e
// ferramentas nunca tocarem as configurações reais. O nome é validado pra o caminho não
// escapar dessa pasta; nome inválido não tem pasta e a persistência desliga.
internal static class PastaDeDados
{
    internal const string NomeDaPasta = "Buzzy";

    internal const string NomeDaPastaDeTestes = "testes";

    internal const int ComprimentoMaximoDoPerfil = 32;

    // Nomes de dispositivo que o Windows reserva em qualquer pasta.
    private static readonly string[] NomesReservados =
    [
        "con", "prn", "aux", "nul",
        "com0", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt0", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    // DoNotVerify: não exige que exista. Nulo sem pasta local. Só é criada na primeira gravação.
    internal static string? DoBuzzy()
        => DoBuzzy(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));

    // Sem essa checagem, Path.Combine("", "Buzzy") daria "Buzzy" relativo à pasta atual.
    internal static string? DoBuzzy(string? pastaLocal)
        => string.IsNullOrEmpty(pastaLocal) || !Path.IsPathFullyQualified(pastaLocal) ? null : Path.Combine(pastaLocal, NomeDaPasta);

    // 1 a 32 chars, só a-z, 0-9 e hífen (não no começo), e nada de nome reservado.
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

    // Com o nome validado, é sempre subpasta direta de testes.
    internal static string? DoPerfilDeTeste(string? nome) => DoPerfilDeTeste(nome, DoBuzzy());

    internal static string? DasConfiguracoes(string? perfilDeTeste, bool persistenciaDesligada)
        => DasConfiguracoes(perfilDeTeste, persistenciaDesligada, DoBuzzy());

    // Com perfil pedido, nunca devolve a pasta real: perfil sem pasta vira nulo (sem
    // persistência) em vez de cair nela.
    internal static string? DasConfiguracoes(string? perfilDeTeste, bool persistenciaDesligada, string? pastaDoBuzzy)
    {
        if (persistenciaDesligada) return null;
        return perfilDeTeste is null ? pastaDoBuzzy : DoPerfilDeTeste(perfilDeTeste, pastaDoBuzzy);
    }

    private static string? DoPerfilDeTeste(string? nome, string? pastaDoBuzzy)
        => NomeDePerfilValido(nome) && pastaDoBuzzy is not null ? Path.Combine(pastaDoBuzzy, NomeDaPastaDeTestes, nome) : null;
}
