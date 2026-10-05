using System.Runtime.InteropServices;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Resultado de uma consulta da configuração de vídeo: os alvos ativos (nome GDI da fonte e caminho do dispositivo
/// do monitor) e, se a consulta inteira falhou, o motivo, só com o nome da função e o código do Windows, ou o tipo e o
/// código de uma exceção (SECURITY.md 6). Um alvo cujo nome ou caminho não pôde ser lido fica de fora e conta em
/// <see cref="CaminhosSemNome"/>, sem invalidar os outros.
/// </summary>
internal sealed record ConsultaDeVideo(IReadOnlyList<AlvoAtivo> Alvos, string? Erro, int CaminhosSemNome);

/// <summary>
/// Leitura da configuração de vídeo para a chave estável do monitor (DEC-030, ARCHITECTURE.md 2.13.3): os caminhos
/// ativos, o nome GDI da fonte de cada um e o caminho do dispositivo do alvo. Só lê, nunca muda a configuração. O
/// caminho sai daqui só dentro de <see cref="AlvoAtivo"/>, e só <see cref="ChavesDeMonitor"/> o usa, para o resumo.
/// Na thread da interface, como o resto da leitura da topologia.
/// </summary>
internal static class ConfiguracaoDeVideo
{
    /// <summary>Tentativas completas quando a configuração muda entre medir e ler (ERROR_INSUFFICIENT_BUFFER).</summary>
    private const int Tentativas = 3;

    /// <summary>Limites de sanidade dos tamanhos que o Windows informa, antes de reservar memória.</summary>
    private const uint MaximoDeCaminhos = 1024;

    private const uint MaximoDeModos = 2048;

    /// <summary>
    /// Os alvos ativos agora. Nunca lança por causa do Windows: uma falha vira <see cref="ConsultaDeVideo.Erro"/>, e a
    /// leitura da topologia segue com o cache ou a reserva (a consulta pode ser negada numa sessão remota ou sem acesso
    /// ao console).
    /// </summary>
    internal static ConsultaDeVideo Consultar()
    {
        try
        {
            for (int tentativa = 1; tentativa <= Tentativas; tentativa++)
            {
                int r = Win32.GetDisplayConfigBufferSizes(Win32.QDC_ONLY_ACTIVE_PATHS, out uint caminhos, out uint modos);
                if (r != Win32.ERROR_SUCCESS) return Falha($"GetDisplayConfigBufferSizes {r}");
                if (caminhos > MaximoDeCaminhos || modos > MaximoDeModos) return Falha("GetDisplayConfigBufferSizes fora dos limites");

                // Pelo menos um elemento em cada vetor: um vetor vazio iria ao Windows como ponteiro nulo.
                var vetorDeCaminhos = new Win32.DISPLAYCONFIG_PATH_INFO[Math.Max(caminhos, 1)];
                var vetorDeModos = new Win32.DISPLAYCONFIG_MODE_INFO[Math.Max(modos, 1)];
                uint lidos = (uint)vetorDeCaminhos.Length, modosLidos = (uint)vetorDeModos.Length;
                r = Win32.QueryDisplayConfig(Win32.QDC_ONLY_ACTIVE_PATHS, ref lidos, vetorDeCaminhos, ref modosLidos, vetorDeModos, 0);
                if (r == Win32.ERROR_INSUFFICIENT_BUFFER) continue;
                if (r != Win32.ERROR_SUCCESS) return Falha($"QueryDisplayConfig {r}");
                return Alvos(vetorDeCaminhos.AsSpan(0, (int)Math.Min(lidos, (uint)vetorDeCaminhos.Length)));
            }
            return Falha($"QueryDisplayConfig {Win32.ERROR_INSUFFICIENT_BUFFER}");
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Só o tipo e o código (SECURITY.md 6).
            return Falha($"{e.GetType().Name} 0x{e.HResult:X8}");
        }
    }

    private static ConsultaDeVideo Falha(string motivo) => new([], motivo, 0);

    /// <summary>
    /// De cada caminho ativo com o alvo disponível: o nome GDI da fonte (lido uma vez por fonte, o que serve aos
    /// clones) e o caminho do dispositivo do alvo. Um alvo marcado como indisponível é o de um monitor que acabou de
    /// sair, e o Windows ainda não tirou o caminho; fica de fora sem contar como falha.
    /// </summary>
    private static ConsultaDeVideo Alvos(ReadOnlySpan<Win32.DISPLAYCONFIG_PATH_INFO> caminhos)
    {
        var fontes = new Dictionary<(uint, int, uint), string?>();
        var alvos = new List<AlvoAtivo>(caminhos.Length);
        int semNome = 0;
        foreach (ref readonly Win32.DISPLAYCONFIG_PATH_INFO c in caminhos)
        {
            if ((c.flags & Win32.DISPLAYCONFIG_PATH_ACTIVE) == 0 || c.targetInfo.targetAvailable == 0) continue;
            (uint, int, uint) fonte = (c.sourceInfo.adapterId.LowPart, c.sourceInfo.adapterId.HighPart, c.sourceInfo.id);
            if (!fontes.TryGetValue(fonte, out string? nomeGdi))
                fontes[fonte] = nomeGdi = NomeDaFonte(c.sourceInfo.adapterId, c.sourceInfo.id);
            string? caminho = nomeGdi is null ? null : CaminhoDoAlvo(c.targetInfo.adapterId, c.targetInfo.id);
            if (nomeGdi is null || caminho is null)
            {
                semNome++;
                continue;
            }
            alvos.Add(new AlvoAtivo(nomeGdi, caminho));
        }
        return new ConsultaDeVideo(alvos, null, semNome);
    }

    private static string? NomeDaFonte(Win32.LUID adaptador, uint id)
    {
        var pacote = new Win32.DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            header = Cabecalho(Win32.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, Marshal.SizeOf<Win32.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(), adaptador, id),
        };
        return Win32.DisplayConfigGetDeviceInfo(ref pacote) == Win32.ERROR_SUCCESS && !string.IsNullOrWhiteSpace(pacote.viewGdiDeviceName)
            ? pacote.viewGdiDeviceName
            : null;
    }

    /// <summary>Só o caminho do dispositivo; o resto do pacote do alvo não é lido.</summary>
    private static string? CaminhoDoAlvo(Win32.LUID adaptador, uint id)
    {
        var pacote = new Win32.DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = Cabecalho(Win32.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, Marshal.SizeOf<Win32.DISPLAYCONFIG_TARGET_DEVICE_NAME>(), adaptador, id),
        };
        return Win32.DisplayConfigGetDeviceInfo(ref pacote) == Win32.ERROR_SUCCESS && !string.IsNullOrWhiteSpace(pacote.monitorDevicePath)
            ? pacote.monitorDevicePath
            : null;
    }

    private static Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER Cabecalho(int tipo, int tamanho, Win32.LUID adaptador, uint id)
        => new() { type = tipo, size = (uint)tamanho, adapterId = adaptador, id = id };
}
