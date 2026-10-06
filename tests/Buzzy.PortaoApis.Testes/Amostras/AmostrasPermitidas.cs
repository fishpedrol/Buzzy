using System.Runtime.InteropServices;

namespace Buzzy.PortaoApis.Testes.Amostras;

// Amostras (fixtures) que o portão NÃO pode acusar: o que o adaptador de plataforma e a medição
// do próprio processo precisam usar. NUNCA são executadas.

[StructLayout(LayoutKind.Sequential)]
internal struct PontoNativo
{
    public int X;
    public int Y;
}

/// <summary>Funções de janela, monitor, bandeja e do próprio processo, permitidas (SECURITY.md 3.1).</summary>
internal static class PInvokesPermitidos
{
    [DllImport("user32.dll")]
    internal static extern int SetWindowPos(nint janela, nint depoisDe, int x, int y, int largura, int altura, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint LerEstilo(nint janela, int indice);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(PontoNativo ponto, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    internal static extern int LerMonitor(nint monitor, nint informacao);

    [DllImport("user32.dll")]
    internal static extern int EnumDisplayMonitors(nint dc, nint recorte, nint retorno, nint dado);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    internal static extern int NotificarIcone(uint mensagem, nint dados);

    [DllImport("user32.dll")]
    internal static extern int SetForegroundWindow(nint janela);

    [DllImport("user32.dll")]
    internal static extern int GetWindowRect(nint janela, nint retangulo);

    [DllImport("user32.dll")]
    internal static extern nint SetCapture(nint janela);

    [DllImport("user32.dll")]
    internal static extern int ReleaseCapture();

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int tipo, nint x, nint y);

    [DllImport("kernel32.dll")]
    internal static extern nint GetModuleHandleW(nint nome);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentProcessId();

    // Ler o registro não é proibido (SECURITY.md 3.2 proíbe criar persistência); gravar é.
    [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW")]
    internal static extern int AbrirChave(nint chave, nint subchave, uint opcoes, uint acesso, nint resultado);

    [DllImport("ntdll.dll")]
    internal static extern int NtQueryTimerResolution(nint minimo, nint maximo, nint atual);
}

/// <summary>Referências gerenciadas permitidas, parecidas com as proibidas.</summary>
internal static class ReferenciasPermitidas
{
    internal static void ProprioProcesso()
    {
        using var eu = System.Diagnostics.Process.GetCurrentProcess();
        _ = eu.Id;
        _ = eu.StartTime;
        _ = eu.WorkingSet64;
        _ = eu.PrivateMemorySize64;
        _ = eu.TotalProcessorTime;
        _ = Environment.ProcessId;
    }

    internal static void OutrosStart()
    {
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        relogio.Stop();
        var temporizador = new System.Windows.Threading.DispatcherTimer();
        temporizador.Start();
        temporizador.Stop();
    }

    internal static void Diversas()
    {
        _ = System.Reflection.Assembly.GetExecutingAssembly().GetName();
        // No lugar de Assembly.Location (vazio num executável de arquivo único; F9-P10): o caminho e a pasta do executável.
        _ = Environment.ProcessPath;
        _ = AppContext.BaseDirectory;
        _ = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(ReferenciasPermitidas).Assembly);
        _ = AppDomain.CurrentDomain.BaseDirectory;
        _ = Marshal.SizeOf<PontoNativo>();
        _ = Marshal.GetLastPInvokeError();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => { };
        System.Windows.Automation.AutomationProperties.SetName(new System.Windows.Controls.Button(), "Buzzy");
        _ = System.Windows.Forms.Screen.AllScreens;
    }
}
