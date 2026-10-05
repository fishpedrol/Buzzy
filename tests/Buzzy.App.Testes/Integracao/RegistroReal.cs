using System.Globalization;
using System.Runtime.InteropServices;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// O que se vê de fora do registro REAL do usuário que o início com o Windows usaria (Q-04; DEC-038, item 12): o valor
/// <c>Buzzy</c> da chave Run e o de <c>StartupApproved\Run</c>, cada um ausente ou com o tipo e o tamanho, e a data da última
/// escrita das duas chaves. Os dados nunca são lidos: <c>RegQueryValueExW</c> vai com o buffer nulo e só devolve o tipo e o
/// tamanho. Só leitura (<c>KEY_QUERY_VALUE</c>); é o único arquivo de <c>tests/</c> e <c>tools/</c> que toca o registro
/// (<c>IsolamentoTestes</c>). Entra na foto dos arquivos reais (<see cref="ArquivosReais.Foto()"/>): uma escrita de outro
/// programa nessas chaves durante os testes também muda a foto, o que falha do lado seguro.
/// </summary>
internal static class RegistroReal
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Aprovacao = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string Valor = "Buzzy";
    private static readonly nint HKEY_CURRENT_USER = unchecked((nint)0x80000001);
    private const int KEY_QUERY_VALUE = 0x0001;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_FILE_NOT_FOUND = 2;

    /// <summary>A foto das duas chaves e dos dois valores.</summary>
    internal static string Foto() => $"registro: {Chave("Run", Run)}; {Chave("StartupApproved\\Run", Aprovacao)}";

    private static string Chave(string nome, string caminho)
    {
        int erro = RegOpenKeyExW(HKEY_CURRENT_USER, caminho, 0, KEY_QUERY_VALUE, out nint chave);
        if (erro == ERROR_FILE_NOT_FOUND) return $"{nome}: chave ausente";
        if (erro != ERROR_SUCCESS) return $"{nome}: erro {erro} ao abrir";
        try
        {
            string escrita = RegQueryInfoKeyW(chave, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, out long arquivoDeTempo) == ERROR_SUCCESS
                ? DateTime.FromFileTimeUtc(arquivoDeTempo).ToString("O", CultureInfo.InvariantCulture)
                : "desconhecida";
            int tamanho = 0;
            int consulta = RegQueryValueExW(chave, Valor, 0, out int tipo, 0, ref tamanho);
            string valor = consulta switch
            {
                ERROR_SUCCESS => string.Create(CultureInfo.InvariantCulture, $"tipo {tipo}, {tamanho} bytes"),
                ERROR_FILE_NOT_FOUND => "ausente",
                _ => $"erro {consulta}",
            };
            return $"{nome}\\{Valor}: {valor}, chave escrita {escrita}";
        }
        finally
        {
            _ = RegCloseKey(chave);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyExW(nint chave, string subchave, int opcoes, int acesso, out nint resultado);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueExW(nint chave, string valor, nint reservado, out int tipo, nint dados, ref int tamanho);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryInfoKeyW(nint chave, nint classe, nint tamanhoDaClasse, nint reservado, nint subchaves, nint maiorSubchave,
        nint maiorClasse, nint valores, nint maiorNome, nint maiorValor, nint descritor, out long ultimaEscrita);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegCloseKey(nint chave);
}
