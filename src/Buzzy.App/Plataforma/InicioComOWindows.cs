using System.Runtime.InteropServices;
using Buzzy.App.Composicao;

namespace Buzzy.App.Plataforma;

// Único lugar do Buzzy que grava/apaga no registro (RegSetValueExW e RegDeleteValueW
// só aqui), e só o valor "Buzzy" da chave Run do usuário, quando ele liga nas configurações.
// - Ligar: abre a Run só se existir (nunca cria) e grava o caminho do exe entre aspas, REG_SZ.
// - Desligar: relê e só apaga se apontar pra esta cópia.
// - StartupApproved\Run só é lida: nunca religamos o que foi desligado no Windows.
// Chave, nome e tipo são constantes e o caminho do exe nunca vai pro log.
// Construtor privado: só DaExecucao cria, e perfil de teste nunca chega aqui.
internal sealed class InicioComOWindows : IInicioComOWindows
{
    private const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ChaveDaAprovacao = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string NomeDoValor = "Buzzy";
    private const int REG_SZ = 1;
    private const int KEY_QUERY_VALUE = 0x0001;
    private const int KEY_SET_VALUE = 0x0002;
    private const int RRF_RT_REG_SZ = 0x0002;
    private const int RRF_RT_REG_BINARY = 0x0008;
    private const int RRF_NOEXPAND = 0x10000000;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_FILE_NOT_FOUND = 2;
    private static readonly nint HKEY_CURRENT_USER = unchecked((nint)0x80000001);

    private readonly string? _caminho;

    private InicioComOWindows(string? caminho) => _caminho = caminho;

    public int? UltimoErro { get; private set; }

    public ModoDoInicio Modo => ModoDoInicio.Registro;

    // Falha fechado: perfil de teste válido -> simulado em memória; perfil inválido,
    // persistência desligada ou sem pasta -> indisponível. Registro de verdade só sem perfil.
    // Nada é lido aqui: a partida nunca toca o registro.
    internal static IInicioComOWindows DaExecucao(string? perfilDeTeste, bool persistenciaDesligada, string? pastaDoBuzzy, string? caminhoDoExecutavel)
    {
        if (perfilDeTeste is not null)
            return PastaDeDados.NomeDePerfilValido(perfilDeTeste) && !persistenciaDesligada ? new InicioSimulado(caminhoDoExecutavel) : new InicioIndisponivel();
        if (persistenciaDesligada || pastaDoBuzzy is null) return new InicioIndisponivel();
        return new InicioComOWindows(caminhoDoExecutavel);
    }

    public EstadoDoInicio Ler()
    {
        UltimoErro = null;
        int erro = LerTexto(out string? valor, out bool grandeDemais);
        if (erro is not (ERROR_SUCCESS or ERROR_FILE_NOT_FOUND))
        {
            UltimoErro = erro;
            return EstadoDoInicio.Indisponivel;
        }
        // Sem a chave Run, ligar sempre falharia (a gente não cria a chave).
        if (erro == ERROR_FILE_NOT_FOUND && ChaveRunAusente()) return EstadoDoInicio.Indisponivel;
        // Maior que qualquer caminho aceito: não é desta cópia, mas o Windows roda, então é outra cópia.
        if (grandeDemais) return RegrasDoInicio.CaminhoValido(_caminho) ? EstadoDoInicio.OutroCaminho : EstadoDoInicio.Indisponivel;
        byte[]? aprovacao = LerAprovacao();
        return RegrasDoInicio.Avaliar(true, erro == ERROR_FILE_NOT_FOUND ? null : valor, aprovacao, _caminho);
    }

    public ResultadoDoInicio Ligar()
    {
        UltimoErro = null;
        if (!RegrasDoInicio.CaminhoValido(_caminho)) return ResultadoDoInicio.Indisponivel;
        int erro = Nativo.RegOpenKeyExW(HKEY_CURRENT_USER, ChaveRun, 0, KEY_SET_VALUE, out nint chave);
        if (erro != ERROR_SUCCESS)
        {
            UltimoErro = erro;
            return ResultadoDoInicio.Erro;
        }
        try
        {
            string dado = RegrasDoInicio.DadoDoRun(_caminho!);
            byte[] bytes = [.. System.Text.Encoding.Unicode.GetBytes(dado), 0, 0];
            erro = Nativo.RegSetValueExW(chave, NomeDoValor, 0, REG_SZ, bytes, bytes.Length);
            UltimoErro = erro == ERROR_SUCCESS ? null : erro;
            return erro == ERROR_SUCCESS ? ResultadoDoInicio.Ok : ResultadoDoInicio.Erro;
        }
        finally
        {
            _ = Nativo.RegCloseKey(chave);
        }
    }

    public ResultadoDoInicio Desligar()
    {
        UltimoErro = null;
        if (!RegrasDoInicio.CaminhoValido(_caminho)) return ResultadoDoInicio.Indisponivel;
        int lido = LerTexto(out string? valor, out _);
        if (lido == ERROR_FILE_NOT_FOUND) return ResultadoDoInicio.Ok;
        if (lido != ERROR_SUCCESS)
        {
            UltimoErro = lido;
            return ResultadoDoInicio.Erro;
        }
        if (!RegrasDoInicio.DestaCopia(valor, _caminho!)) return ResultadoDoInicio.NaoEDestaCopia;
        int erro = Nativo.RegOpenKeyExW(HKEY_CURRENT_USER, ChaveRun, 0, KEY_SET_VALUE, out nint chave);
        if (erro != ERROR_SUCCESS)
        {
            UltimoErro = erro;
            return ResultadoDoInicio.Erro;
        }
        try
        {
            erro = Nativo.RegDeleteValueW(chave, NomeDoValor);
            UltimoErro = erro is ERROR_SUCCESS or ERROR_FILE_NOT_FOUND ? null : erro;
            return erro is ERROR_SUCCESS or ERROR_FILE_NOT_FOUND ? ResultadoDoInicio.Ok : ResultadoDoInicio.Erro;
        }
        finally
        {
            _ = Nativo.RegCloseKey(chave);
        }
    }

    // REG_SZ sem expandir variáveis. Grande demais nem é lido, só marca grandeDemais.
    private static int LerTexto(out string? valor, out bool grandeDemais)
    {
        valor = null;
        grandeDemais = false;
        int tamanho = 0;
        int erro = Nativo.RegGetValueW(HKEY_CURRENT_USER, ChaveRun, NomeDoValor, RRF_RT_REG_SZ | RRF_NOEXPAND, out _, null, ref tamanho);
        if (erro != ERROR_SUCCESS) return erro;
        if (tamanho > 4 * RegrasDoInicio.CaminhoMaximo)
        {
            grandeDemais = true;
            return ERROR_SUCCESS;
        }
        var dados = new byte[tamanho];
        erro = Nativo.RegGetValueW(HKEY_CURRENT_USER, ChaveRun, NomeDoValor, RRF_RT_REG_SZ | RRF_NOEXPAND, out _, dados, ref tamanho);
        if (erro != ERROR_SUCCESS) return erro;
        valor = System.Text.Encoding.Unicode.GetString(dados, 0, Math.Max(0, tamanho - 2)).TrimEnd('\0');
        return ERROR_SUCCESS;
    }

    // Abre só pra consulta, nada é criado.
    private static bool ChaveRunAusente()
    {
        int erro = Nativo.RegOpenKeyExW(HKEY_CURRENT_USER, ChaveRun, 0, KEY_QUERY_VALUE, out nint chave);
        if (erro == ERROR_SUCCESS) _ = Nativo.RegCloseKey(chave);
        return erro == ERROR_FILE_NOT_FOUND;
    }

    // StartupApproved (REG_BINARY); nulo se faltar ou der qualquer erro.
    private static byte[]? LerAprovacao()
    {
        int tamanho = 0;
        if (Nativo.RegGetValueW(HKEY_CURRENT_USER, ChaveDaAprovacao, NomeDoValor, RRF_RT_REG_BINARY, out _, null, ref tamanho) != ERROR_SUCCESS || tamanho is <= 0 or > 64) return null;
        var dados = new byte[tamanho];
        return Nativo.RegGetValueW(HKEY_CURRENT_USER, ChaveDaAprovacao, NomeDoValor, RRF_RT_REG_BINARY, out _, dados, ref tamanho) == ERROR_SUCCESS ? dados[..tamanho] : null;
    }

    private static class Nativo
    {
        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int RegOpenKeyExW(nint chave, string subchave, int opcoes, int acesso, out nint resultado);

        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int RegGetValueW(nint chave, string subchave, string valor, int flags, out int tipo, byte[]? dados, ref int tamanho);

        [DllImport("advapi32.dll", EntryPoint = "RegSetValueExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int RegSetValueExW(nint chave, string nome, int reservado, int tipo, byte[] dados, int tamanho);

        [DllImport("advapi32.dll", EntryPoint = "RegDeleteValueW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int RegDeleteValueW(nint chave, string nome);

        [DllImport("advapi32.dll", EntryPoint = "RegCloseKey", ExactSpelling = true)]
        internal static extern int RegCloseKey(nint chave);
    }
}
