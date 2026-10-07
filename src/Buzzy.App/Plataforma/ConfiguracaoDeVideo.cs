using System.Runtime.InteropServices;

namespace Buzzy.App.Plataforma;

// Erro só leva nome da função + código do Windows (ou tipo + HResult), nada que
// identifique a máquina. Alvo sem nome ou caminho fica de fora e conta em CaminhosSemNome.
internal sealed record ConsultaDeVideo(IReadOnlyList<AlvoAtivo> Alvos, string? Erro, int CaminhosSemNome);

// Lê (nunca muda) a configuração de vídeo pra montar a chave estável do monitor.
// O caminho do dispositivo só sai daqui dentro de AlvoAtivo, e só ChavesDeMonitor usa.
// Roda na thread da UI, como o resto da topologia.
internal static class ConfiguracaoDeVideo
{
    // Pra quando a configuração muda entre medir e ler (ERROR_INSUFFICIENT_BUFFER).
    private const int Tentativas = 3;

    // Sanidade nos tamanhos que o Windows devolve, antes de alocar.
    private const uint MaximoDeCaminhos = 1024;

    private const uint MaximoDeModos = 2048;

    // Não lança por falha do Windows (sessão remota ou sem console nega a consulta):
    // vira Erro e a topologia segue com cache ou reserva.
    internal static ConsultaDeVideo Consultar()
    {
        try
        {
            for (int tentativa = 1; tentativa <= Tentativas; tentativa++)
            {
                int r = Win32.GetDisplayConfigBufferSizes(Win32.QDC_ONLY_ACTIVE_PATHS, out uint caminhos, out uint modos);
                if (r != Win32.ERROR_SUCCESS) return Falha($"GetDisplayConfigBufferSizes {r}");
                if (caminhos > MaximoDeCaminhos || modos > MaximoDeModos) return Falha("GetDisplayConfigBufferSizes fora dos limites");

                // Mínimo 1: vetor vazio iria pro Windows como ponteiro nulo.
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
            return Falha($"{e.GetType().Name} 0x{e.HResult:X8}");
        }
    }

    private static ConsultaDeVideo Falha(string motivo) => new([], motivo, 0);

    // Nome GDI lido uma vez por fonte (serve pros clones). Alvo indisponível é monitor
    // que acabou de sair e o Windows ainda não tirou: pula sem contar como falha.
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
