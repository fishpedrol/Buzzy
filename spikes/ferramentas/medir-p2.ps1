<#
    medir-p2.ps1 — mede CPU, acordadas, memória e GPU de um processo do protótipo.

    Uso:
      .\medir-p2.ps1 -Modo p2-repouso -Minutos 60 -IntervaloSegundos 5
      .\medir-p2.ps1 -Modo p2-anim10  -Minutos 10 -IntervaloSegundos 1
      .\medir-p2.ps1 -Modo p2-anim60  -Minutos 10 -IntervaloSegundos 1

    Segue o PID, não o nome: pode ter outra instância aberta e o contador por nome misturaria.
      M1  CPU       -> delta de TotalProcessorTime sobre o tempo de parede
      M2  acordadas -> soma das trocas de contexto das threads do processo
      M3  memória   -> PrivateMemorySize64 e WorkingSet64
      M4  GPU       -> soma de UtilizationPercentage das engines do PID
    A resolução do timer é do sistema todo, então mede antes, durante e depois pra saber
    quem mexeu. Usa CIM porque os nomes de contador vêm traduzidos e quebram o Get-Counter
    em Windows em português.
#>

[CmdletBinding()]
param(
    [ValidateSet('p2-repouso', 'p2-anim10', 'p2-anim60', 'p2-anim60comp')]
    [string] $Modo = 'p2-repouso',

    [int] $Minutos = 60,
    [int] $IntervaloSegundos = 5,

    # Canto do monitor secundário, longe de onde se trabalha.
    [int] $X = -1900,
    [int] $Y = 40,

    # Fora das estatísticas: a partida do processo puxa CPU de verdade.
    [int] $AquecimentoSegundos = 30
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$exe = Join-Path $raiz 'BuzzySpike\bin\Release\net10.0-windows\BuzzySpike.exe'
$destino = Join-Path $raiz "resultados\$Modo.medicao.txt"

if (-not (Test-Path $exe)) { throw "Protótipo não compilado em $exe" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Timer {
    [DllImport("ntdll.dll")]
    public static extern int NtQueryTimerResolution(out uint min, out uint max, out uint atual);
    public static double? AtualMs() {
        uint a, b, c;
        if (NtQueryTimerResolution(out a, out b, out c) != 0) return null;
        return c / 10000.0;
    }

    // A janela do protótipo é uma tool window, e Process.CloseMainWindow() não a alcança.
    // Enviar WM_CLOSE ao HWND que o próprio protótipo registrou no log é determinístico e
    // garante que o handler de fechamento rode e grave o resumo (contagem de OnRender e
    // quadros por segundo alcançados).
    public const int WM_CLOSE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
'@

function Resolucao { [Timer]::AtualMs() }

function Estatistica($valores, $rotulo, $unidade) {
    if (-not $valores -or $valores.Count -eq 0) {
        return "$rotulo : NÃO MEDIDA"
    }
    $ord = @($valores | Sort-Object)
    $media = ($valores | Measure-Object -Average).Average
    $p95 = $ord[[Math]::Min([int]([Math]::Ceiling($ord.Count * 0.95)) - 1, $ord.Count - 1)]
    return ("{0} : média {1:0.000} {4} | p95 {2:0.000} {4} | máx {3:0.000} {4}" -f `
        $rotulo, $media, $p95, $ord[-1], $unidade)
}

$nucleos = [Environment]::ProcessorCount
$resAntes = Resolucao

Write-Host "=== P2 / $Modo ===" -ForegroundColor Cyan
Write-Host "Duração: $Minutos min | intervalo: $IntervaloSegundos s | aquecimento descartado: $AquecimentoSegundos s" -ForegroundColor DarkGray
Write-Host "Resolução do timer global ANTES de abrir o protótipo: $resAntes ms" -ForegroundColor DarkGray

$proc = Start-Process -FilePath $exe -ArgumentList '--modo', $Modo, '--x', $X, '--y', $Y -PassThru
$pidAlvo = $proc.Id
Write-Host "Protótipo aberto: PID $pidAlvo em ($X,$Y)" -ForegroundColor DarkGray

Start-Sleep -Seconds $AquecimentoSegundos
$proc.Refresh()
if ($proc.HasExited) { throw "O protótipo encerrou durante o aquecimento." }

$cpuInicialAquecimento = $proc.TotalProcessorTime
Write-Host ("CPU consumida na partida e aquecimento: {0:0.000} s" -f $cpuInicialAquecimento.TotalSeconds) -ForegroundColor DarkGray

$consultaThread = "SELECT IDProcess, ContextSwitchesPersec FROM Win32_PerfFormattedData_PerfProc_Thread WHERE IDProcess = $pidAlvo"
$consultaGpu = "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE 'pid_${pidAlvo}_%'"

$gpuDisponivel = $true
try { $null = Get-CimInstance -Query $consultaGpu -ErrorAction Stop } catch { $gpuDisponivel = $false }

$amostrasCpu = [System.Collections.Generic.List[double]]::new()
$amostrasCpuMaquina = [System.Collections.Generic.List[double]]::new()
$amostrasPriv = [System.Collections.Generic.List[double]]::new()
$amostrasWs = [System.Collections.Generic.List[double]]::new()
$amostrasGpu = [System.Collections.Generic.List[double]]::new()
$amostrasTrocas = [System.Collections.Generic.List[double]]::new()
$resolucoes = [System.Collections.Generic.List[double]]::new()

$privInicial = $proc.PrivateMemorySize64 / 1MB
$cpuAnterior = $proc.TotalProcessorTime
$marcaAnterior = Get-Date
$fim = $marcaAnterior.AddMinutes($Minutos)

while ((Get-Date) -lt $fim) {
    Start-Sleep -Seconds $IntervaloSegundos

    $proc.Refresh()
    if ($proc.HasExited) {
        Write-Warning "O protótipo encerrou antes do fim da medição."
        break
    }

    $agora = Get-Date
    $cpuAgora = $proc.TotalProcessorTime
    $segundos = ($agora - $marcaAnterior).TotalSeconds
    if ($segundos -le 0) { continue }

    # M1: % de um núcleo e % da máquina.
    $usoNucleo = (($cpuAgora - $cpuAnterior).TotalSeconds / $segundos) * 100.0
    $amostrasCpu.Add($usoNucleo)
    $amostrasCpuMaquina.Add($usoNucleo / $nucleos)

    # M3
    $amostrasPriv.Add($proc.PrivateMemorySize64 / 1MB)
    $amostrasWs.Add($proc.WorkingSet64 / 1MB)

    # M2
    try {
        $soma = (Get-CimInstance -Query $consultaThread -ErrorAction Stop |
                 Measure-Object -Property ContextSwitchesPersec -Sum).Sum
        if ($null -eq $soma) { $soma = 0 }
        $amostrasTrocas.Add([double]$soma)
    } catch { }

    # M4
    if ($gpuDisponivel) {
        try {
            $somaGpu = (Get-CimInstance -Query $consultaGpu -ErrorAction Stop |
                        Measure-Object -Property UtilizationPercentage -Sum).Sum
            if ($null -eq $somaGpu) { $somaGpu = 0 }
            $amostrasGpu.Add([double]$somaGpu)
        } catch { }
    }

    $r = Resolucao
    if ($null -ne $r) { $resolucoes.Add($r) }

    $cpuAnterior = $cpuAgora
    $marcaAnterior = $agora
}

$proc.Refresh()
$privFinal = if ($proc.HasExited) { $null } else { $proc.PrivateMemorySize64 / 1MB }
$cpuTotal = if ($proc.HasExited) { $null } else { $proc.TotalProcessorTime.TotalSeconds }
$resDurante = Resolucao

# Fecha pela janela (HWND do log) pro protótipo gravar o resumo dele: OnRender e qps.
$logApp = Join-Path $raiz "resultados\$Modo.log"
$fechouLimpo = $false

if (-not $proc.HasExited -and (Test-Path $logApp)) {
    $linhaHwnd = Get-Content $logApp -Encoding UTF8 |
                 Where-Object { $_ -match 'SONDA\|HWND\|' } |
                 Select-Object -Last 1
    if ($linhaHwnd) {
        $hwndApp = [IntPtr][int64]($linhaHwnd -replace '.*SONDA\|HWND\|', '')
        $null = [Timer]::PostMessage($hwndApp, [Timer]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
        $fechouLimpo = $proc.WaitForExit(10000)
    }
}

if (-not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    $fechouLimpo = $proc.WaitForExit(10000)
}
if (-not $proc.HasExited) {
    Stop-Process -Id $pidAlvo -Force
    $fechouLimpo = $false
}

Start-Sleep -Seconds 3
$resDepois = Resolucao

# ------------------------------------------------------------------ relatório
$gpuInfo = (Get-CimInstance Win32_VideoController | Select-Object -First 1)
$osInfo = Get-CimInstance Win32_OperatingSystem
$cpuInfo = Get-CimInstance Win32_Processor | Select-Object -First 1

$linhas = @()
$linhas += "================================================================"
$linhas += "P2 — medição de $Modo"
$linhas += "Data/hora            : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$linhas += "Ferramenta           : medir-p2.ps1 (CIM + System.Diagnostics.Process)"
$linhas += "Máquina              : $($cpuInfo.Name.Trim()), $nucleos processadores lógicos"
$linhas += "Memória da máquina   : $([math]::Round($osInfo.TotalVisibleMemorySize/1024/1024,1)) GB"
$linhas += "GPU                  : $($gpuInfo.Name), driver $($gpuInfo.DriverVersion)"
$linhas += "Resolução de tela    : $($gpuInfo.CurrentHorizontalResolution)x$($gpuInfo.CurrentVerticalResolution) @ $($gpuInfo.CurrentRefreshRate) Hz"
$linhas += "SO                   : $($osInfo.Caption) build $($osInfo.BuildNumber)"
$linhas += "Runtime do protótipo : .NET 10 (WPF), x64, Release"
$linhas += "PID medido           : $pidAlvo"
$linhas += "Janela em            : ($X,$Y), 200x200 px, monitor secundário"
$linhas += "Duração amostrada    : $Minutos min | intervalo $IntervaloSegundos s | $($amostrasCpu.Count) amostras"
$linhas += "Aquecimento descartado: $AquecimentoSegundos s (CPU de partida: $([math]::Round($cpuInicialAquecimento.TotalSeconds,3)) s)"
$linhas += "----------------------------------------------------------------"
$linhas += "M1 CPU"
$linhas += "   " + (Estatistica $amostrasCpu        'percentual de UM núcleo      ' '%')
$linhas += "   " + (Estatistica $amostrasCpuMaquina 'percentual da máquina inteira' '%')
if ($null -ne $cpuTotal) { $linhas += "   CPU acumulada do processo ao fim: $([math]::Round($cpuTotal,3)) s" }
$linhas += "M2 acordadas por segundo (proxy: trocas de contexto das threads do processo)"
$linhas += "   " + (Estatistica $amostrasTrocas 'trocas de contexto por segundo' '/s')
$linhas += "M3 memória"
$linhas += "   " + (Estatistica $amostrasPriv 'memória privada' 'MB')
$linhas += "   " + (Estatistica $amostrasWs   'working set    ' 'MB')
$linhas += "   memória privada no início da amostragem: $([math]::Round($privInicial,2)) MB"
if ($null -ne $privFinal) { $linhas += "   memória privada no fim da amostragem   : $([math]::Round($privFinal,2)) MB" }
$linhas += "M4 GPU"
if ($gpuDisponivel) {
    $linhas += "   " + (Estatistica $amostrasGpu 'utilização somada das engines do processo' '%')
} else {
    $linhas += "   NÃO MEDIDA: a classe de contadores de GPU não respondeu nesta máquina."
}
$linhas += "----------------------------------------------------------------"
$linhas += "Resolução do timer global (do sistema inteiro, não só deste processo)"
$linhas += "   antes de abrir o protótipo : $resAntes ms"
$linhas += "   " + (Estatistica $resolucoes 'durante a medição         ' 'ms')
$linhas += "   ao terminar a medição      : $resDurante ms"
$linhas += "   depois de fechar o protótipo: $resDepois ms"
if ($resAntes -eq $resDepois -and $resAntes -eq $resDurante) {
    $linhas += "   Leitura: a resolução não mudou ao abrir nem ao fechar o protótipo, então"
    $linhas += "   nada nesta medição é atribuível ao Buzzy. O valor de base desta máquina já"
    $linhas += "   vem elevado por outro processo; o critério de DEC-011 é sobre quem a muda."
} else {
    $linhas += "   Leitura: houve variação. Investigar a atribuição antes de concluir."
}
$linhas += "----------------------------------------------------------------"
$linhas += if ($fechouLimpo) {
    "Encerramento: o protótipo aceitou WM_CLOSE e gravou o resumo interno em $Modo.log
   (passagens de OnRender e quadros por segundo alcançados)."
} else {
    "Encerramento: o processo NÃO fechou pela janela e foi terminado à força.
   O resumo interno pode faltar em $Modo.log; tratar as contagens internas como não medidas."
}
$linhas += "================================================================"

$linhas | Set-Content -Path $destino -Encoding UTF8
$linhas | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Relatório salvo em: $destino" -ForegroundColor Green
