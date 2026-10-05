using System.Globalization;
using System.IO;
using Buzzy.App.Plataforma;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// O que se vê de fora dos arquivos REAIS de configuração do usuário, na pasta do Buzzy (<c>%LOCALAPPDATA%\Buzzy</c>): o
/// principal, a reserva, o temporário e a cópia de diagnóstico, cada um ausente ou com o tamanho e as datas de criação e
/// de escrita. Só metadados da pasta: o conteúdo nunca é aberto nem lido (SECURITY.md 5). Toda execução que abre o Buzzy
/// (a integração inteira, a verificação de tela, que compila este arquivo, e a medição de desempenho, que faz o mesmo em
/// PowerShell) tira uma foto antes e outra depois: os Buzzy abertos usam um perfil de teste e nunca podem mudá-los
/// (revisão de segurança do bloco P6-P9, achado 8).
/// </summary>
internal static class ArquivosReais
{
    /// <summary>
    /// Os nomes dos arquivos de configuração (os de <c>ArquivoDeConfiguracoes.Nomes</c>, que a verificação de tela não
    /// compila; um teste confere que são os mesmos).
    /// </summary>
    internal static readonly IReadOnlyList<string> Nomes = ["settings.json", "settings.json.bak", "settings.json.tmp", "settings.corrupt.json"];

    /// <summary>
    /// A foto, na pasta real do Buzzy (sem a pasta local do usuário, um texto que diz isso), mais a do registro real do início
    /// com o Windows (<see cref="RegistroReal"/>; DEC-038, item 12), também só por fora.
    /// </summary>
    internal static string Foto() => (PastaDeDados.DoBuzzy() is { } pasta ? Foto(pasta) : "sem a pasta local do usuário") + "; " + RegistroReal.Foto();

    /// <summary>A foto dos arquivos de configuração em <paramref name="pasta"/> (os testes desta classe usam uma pasta temporária).</summary>
    internal static string Foto(string pasta)
        => string.Join("; ", Nomes.Select(nome =>
        {
            var info = new FileInfo(Path.Combine(pasta, nome));
            return info.Exists
                ? string.Create(CultureInfo.InvariantCulture, $"{nome}: {info.Length} bytes, criado {info.CreationTimeUtc:O}, escrito {info.LastWriteTimeUtc:O}")
                : $"{nome}: ausente";
        }));
}
