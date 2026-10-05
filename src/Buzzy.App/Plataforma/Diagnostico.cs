using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Log de diagnóstico, DESLIGADO por padrão. Só grava com <c>--diagnostico</c> na linha de
/// comando, e só em <c>%LOCALAPPDATA%\Buzzy\diagnostico.log</c> (SECURITY.md 5: logs ficam na
/// pasta do Buzzy e têm tamanho limitado). Ao passar de 1 MB, o arquivo vira
/// <c>diagnostico.1.log</c> (uma cópia só) e recomeça. O limite falha fechado (DEC-040, item 4): um log que
/// é link (ponto de nova análise) não é usado, e um que não consegue girar para de gravar ao passar de duas
/// vezes o limite.
///
/// Registra apenas fatos do próprio Buzzy: suas janelas, sua posição, os monitores, o ícone
/// da bandeja, os cliques que chegaram às suas janelas e os comandos do seu menu. Nunca
/// registra nada de outro aplicativo.
///
/// Linhas legíveis por máquina: <c>[hh:mm:ss.fff] BUZZY|CHAVE|campo=valor|campo=valor</c>,
/// usadas pelos testes de integração e pelo script de medição.
/// </summary>
internal static class Diagnostico
{
    private const long LimiteBytes = 1024 * 1024;
    private static readonly object Trava = new();
    private static readonly Stopwatch Relogio = Stopwatch.StartNew();
    private static string? _arquivo;
    private static long _limite = LimiteBytes;

    internal static bool Ligado => _arquivo is not null;

    internal static string? Arquivo => _arquivo;

    /// <summary>
    /// Pasta de dados do Buzzy, pela consulta de pasta conhecida do Windows (<see cref="Plataforma.PastaDeDados.DoBuzzy()"/>);
    /// vazia se o Windows não informar a pasta local do usuário. O log fica sempre na raiz dessa pasta, também com
    /// um perfil de teste.
    /// </summary>
    internal static string PastaDeDados() => Plataforma.PastaDeDados.DoBuzzy() ?? "";

    internal static void Ligar() => Ligar(PastaDeDados(), LimiteBytes);

    /// <summary>
    /// Liga o log na <paramref name="pasta"/>, com o <paramref name="limite"/> de bytes antes de girar (os testes usam
    /// uma pasta temporária e um limite pequeno). Sem pasta ou com o log sendo um link, fica desligado.
    /// </summary>
    internal static void Ligar(string pasta, long limite)
    {
        _arquivo = null;
        _limite = limite;
        if (pasta.Length == 0) return; // Sem a pasta local do usuário, sem log: nunca num caminho relativo à pasta atual.

        try
        {
            Directory.CreateDirectory(pasta);
            string arquivo = Path.Combine(pasta, "diagnostico.log");
            // Um log que é link (simbólico ou junção) apontaria a gravação para fora da pasta: não é usado.
            if (File.Exists(arquivo) && (File.GetAttributes(arquivo) & FileAttributes.ReparsePoint) != 0) return;
            _arquivo = arquivo;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _arquivo = null;
        }
    }

    /// <summary>Desliga o log (só os testes; o aplicativo liga uma vez e não desliga).</summary>
    internal static void Desligar()
    {
        lock (Trava)
        {
            _arquivo = null;
            _limite = LimiteBytes;
        }
    }

    /// <summary>Registra um evento: <c>Evento("JANELA", ("hwnd", 1234))</c> vira <c>BUZZY|JANELA|hwnd=1234</c>.</summary>
    internal static void Evento(string chave, params (string Campo, object? Valor)[] campos)
    {
        if (_arquivo is null) return;
        var sb = new StringBuilder("BUZZY|").Append(chave);
        foreach ((string campo, object? valor) in campos)
        {
            sb.Append('|').Append(campo).Append('=');
            sb.Append(Convert.ToString(valor, CultureInfo.InvariantCulture)?.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' '));
        }
        Escrever(sb.ToString());
    }

    private static void Escrever(string texto)
    {
        string? arquivo = _arquivo;
        if (arquivo is null) return;

        string carimbo = Relogio.Elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        byte[] bytes = new UTF8Encoding(false).GetBytes($"[{carimbo}] {texto}{Environment.NewLine}");
        lock (Trava)
        {
            // Rotação à parte: se não der para renomear agora (arquivo aberto por outro processo), a linha é gravada
            // mesmo assim e a rotação fica para a próxima vez, até duas vezes o limite; daí em diante, a linha se perde
            // (falha fechada: o log nunca cresce sem teto).
            try
            {
                var info = new FileInfo(arquivo);
                if (info.Exists && info.Length > _limite)
                {
                    try
                    {
                        File.Move(arquivo, Path.Combine(info.DirectoryName!, "diagnostico.1.log"), overwrite: true);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        if (info.Length > 2 * _limite) return;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return;
            }

            // Outra instância (a segunda abertura, que só pede para esta aparecer) pode estar
            // gravando no mesmo arquivo: poucas tentativas curtas antes de desistir da linha.
            for (int tentativa = 0; tentativa < 4; tentativa++)
            {
                try
                {
                    using var fluxo = new FileStream(arquivo, FileMode.Append, FileAccess.Write, FileShare.Read);
                    fluxo.Write(bytes);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(2);
                }
                catch (UnauthorizedAccessException)
                {
                    return; // Diagnóstico nunca derruba o aplicativo.
                }
            }
        }
    }
}
