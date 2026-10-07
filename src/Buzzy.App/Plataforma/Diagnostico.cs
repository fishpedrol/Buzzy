using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Buzzy.App.Plataforma;

// Log de diagnóstico, desligado por padrão: só com --diagnostico, e só em
// %LOCALAPPDATA%\Buzzy\diagnostico.log. Passou de 1 MB, vira diagnostico.1.log (uma cópia).
// Falha fechado: log que é link simbólico/junção não é usado, e se não der pra girar
// para de gravar em 2x o limite. Link físico passa batido (risco aceito).
//
// Só fatos do próprio Buzzy, nunca de outro aplicativo.
// Formato: [hh:mm:ss.fff] BUZZY|CHAVE|campo=valor|..., lido pelos testes e pela medição.
internal static class Diagnostico
{
    private const long LimiteBytes = 1024 * 1024;
    private static readonly object Trava = new();
    private static readonly Stopwatch Relogio = Stopwatch.StartNew();
    private static string? _arquivo;
    private static long _limite = LimiteBytes;

    internal static bool Ligado => _arquivo is not null;

    internal static string? Arquivo => _arquivo;

    // Vazia se o Windows não der a pasta local. O log fica na raiz dela mesmo com
    // perfil de teste.
    internal static string PastaDeDados() => Plataforma.PastaDeDados.DoBuzzy() ?? "";

    internal static void Ligar() => Ligar(PastaDeDados(), LimiteBytes);

    // Os testes passam pasta temporária e limite pequeno.
    internal static void Ligar(string pasta, long limite)
    {
        _arquivo = null;
        _limite = limite;
        if (pasta.Length == 0) return; // nunca num caminho relativo à pasta atual

        try
        {
            Directory.CreateDirectory(pasta);
            string arquivo = Path.Combine(pasta, "diagnostico.log");
            // Link ou junção levaria a gravação pra fora da pasta.
            if (File.Exists(arquivo) && (File.GetAttributes(arquivo) & FileAttributes.ReparsePoint) != 0) return;
            _arquivo = arquivo;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _arquivo = null;
        }
    }

    // Só pros testes; o app liga uma vez e não desliga.
    internal static void Desligar()
    {
        lock (Trava)
        {
            _arquivo = null;
            _limite = LimiteBytes;
        }
    }

    // Evento("JANELA", ("hwnd", 1234)) -> BUZZY|JANELA|hwnd=1234
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
            // Se não der pra renomear (aberto por outro processo), grava assim mesmo e tenta
            // girar na próxima. Passou de 2x o limite, descarta a linha: o log nunca cresce sem teto.
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

            // A segunda instância (que só pede pra esta aparecer) pode estar gravando no
            // mesmo arquivo: umas tentativas curtas e depois desiste da linha.
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
                    return; // diagnóstico nunca derruba o app
                }
            }
        }
    }
}
