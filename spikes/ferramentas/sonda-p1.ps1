<#
    sonda-p1.ps1 — põe o protótipo sobre o Bloco de Notas e pergunta ao Windows
    (WindowFromPoint) quem receberia o clique em cada faixa. É o mesmo teste de acerto do
    clique de verdade, e em janela layered ele olha o alfa do pixel.

    Uso:
      .\sonda-p1.ps1                  # abre Bloco de Notas + protótipo, sonda e fecha
      .\sonda-p1.ps1 -ManterAberto    # deixa aberto pros cliques na mão
      .\sonda-p1.ps1 -SemBlocoDeNotas -X 700 -Y 400

    Não injeta input, não tira print e não lê janela alheia. Não substitui o clique físico:
    falta uma pessoa clicar em cada faixa e conferir no log.
#>

[CmdletBinding()]
param(
    [int] $X = 700,
    [int] $Y = 400,
    [switch] $SemBlocoDeNotas,
    [switch] $ManterAberto
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$exe = Join-Path $raiz 'BuzzySpike\bin\Release\net10.0-windows\BuzzySpike.exe'
$log = Join-Path $raiz 'resultados\p1.log'

if (-not (Test-Path $exe)) {
    throw "Protótipo não compilado. Rode: dotnet build -c Release em $raiz\BuzzySpike"
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Sonda
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT p);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public static IntPtr JanelaEm(int x, int y)
    {
        POINT p; p.X = x; p.Y = y;
        return WindowFromPoint(p);
    }

    public static uint PidDe(IntPtr h)
    {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        return pid;
    }
}
'@

# Px físicos, como o protótipo.
$null = [Sonda]::SetProcessDpiAwarenessContext([IntPtr](-4))

# ---------------------------------------------------------------- Bloco de Notas
$pidBloco = $null
$retBloco = $null

if (-not $SemBlocoDeNotas) {
    Write-Host "Abrindo o Bloco de Notas..." -ForegroundColor Cyan
    Start-Process notepad

    $limite = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $limite) {
        $p = @(Get-Process -Name 'Notepad' -ErrorAction SilentlyContinue |
               Where-Object { $_.MainWindowHandle -ne 0 })
        if ($p.Count -gt 0) {
            $pidBloco = $p[0].Id
            $h = $p[0].MainWindowHandle
            $r = New-Object Sonda+RECT
            if ([Sonda]::GetWindowRect($h, [ref]$r)) { $retBloco = $r }
            break
        }
        Start-Sleep -Milliseconds 300
    }

    if ($retBloco) {
        Write-Host ("Bloco de Notas: pid $pidBloco, janela ($($retBloco.Left),$($retBloco.Top))-($($retBloco.Right),$($retBloco.Bottom))") -ForegroundColor DarkGray
        # Centraliza no Bloco de Notas pra ter janela de outro processo embaixo de cada faixa.
        $X = [int](($retBloco.Left + $retBloco.Right) / 2 - 100)
        $Y = [int](($retBloco.Top + $retBloco.Bottom) / 2 - 100)
    } else {
        Write-Warning "Não localizei a janela do Bloco de Notas. Usando ($X,$Y) e relatando o que estiver embaixo."
    }
}

# ---------------------------------------------------------------- protótipo
if (Test-Path $log) { Remove-Item $log -Force }

Write-Host "Abrindo o protótipo P1 em ($X,$Y)..." -ForegroundColor Cyan
$proc = Start-Process -FilePath $exe -ArgumentList '--modo', 'p1', '--x', $X, '--y', $Y -PassThru

$limite = (Get-Date).AddSeconds(20)
while (-not (Test-Path $log) -and (Get-Date) -lt $limite) { Start-Sleep -Milliseconds 200 }
if (-not (Test-Path $log)) { throw "O protótipo não escreveu o log em 20 s." }
Start-Sleep -Milliseconds 900

$linhas = Get-Content $log -Encoding UTF8
$hwndSpike = [IntPtr][int64](($linhas | Where-Object { $_ -match 'SONDA\|HWND\|' } | Select-Object -First 1) -replace '.*SONDA\|HWND\|', '')

$bandas = @()
foreach ($l in ($linhas | Where-Object { $_ -match 'SONDA\|BANDA\|' })) {
    $campos = (($l -replace '.*SONDA\|BANDA\|', '') -split '\|')
    $bandas += [pscustomobject]@{
        Nome  = $campos[0]
        Alfa  = [int]$campos[1]
        TelaX = [int]$campos[2]
        TelaY = [int]$campos[3]
    }
}

if ($bandas.Count -eq 0) { throw "O log não trouxe as faixas da figura." }

Write-Host ""
Write-Host "HWND do protótipo: $hwndSpike" -ForegroundColor DarkGray
Write-Host ""

# ---------------------------------------------------------------- sondagem
$resultados = foreach ($b in $bandas) {
    $h = [Sonda]::JanelaEm($b.TelaX, $b.TelaY)
    $pidAlvo = [Sonda]::PidDe($h)
    $nomeProc = try { (Get-Process -Id $pidAlvo -ErrorAction Stop).ProcessName } catch { '?' }
    $ehNossa = ($h -eq $hwndSpike)

    # Só a faixa alfa 0 deve deixar o clique passar.
    $esperadoNossa = ($b.Alfa -ne 0)
    $veredito = if ($ehNossa -eq $esperadoNossa) { 'COMO ESPERADO' } else { 'DIVERGENTE' }

    [pscustomobject]@{
        Faixa          = $b.Nome
        Alfa           = $b.Alfa
        Ponto          = "$($b.TelaX),$($b.TelaY)"
        JanelaAtingida = if ($ehNossa) { 'BuzzySpike' } else { "$nomeProc (pid $pidAlvo)" }
        Atravessou     = if ($ehNossa) { 'nao' } else { 'SIM' }
        Veredito       = $veredito
    }
}

Write-Host "=== P1: teste de acerto do Windows em cada faixa (WindowFromPoint) ===" -ForegroundColor Green
$resultados | Format-Table -AutoSize

$divergentes = @($resultados | Where-Object { $_.Veredito -eq 'DIVERGENTE' })
if ($divergentes.Count -eq 0) {
    Write-Host "Sonda: todas as faixas se comportaram como a arquitetura previa." -ForegroundColor Green
    Write-Host "  alfa 0            -> o clique atravessa e chega ao aplicativo de baixo" -ForegroundColor Green
    Write-Host "  alfa 1, 128 e 255 -> o clique pertence a janela do Buzzy" -ForegroundColor Green
} else {
    Write-Host "Sonda: $($divergentes.Count) faixa(s) divergiram do previsto. Ver a tabela acima." -ForegroundColor Red
}

# Grava a sondagem no log do protótipo.
$relatorio = @()
$relatorio += "---- Sonda automatizada (WindowFromPoint), $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ----"
$relatorio += "     Sem injecao de input e sem captura de tela. HWND do prototipo: $hwndSpike"
if ($pidBloco) { $relatorio += "     Bloco de Notas aberto pela sonda: pid $pidBloco" }
if ($retBloco) { $relatorio += "     Janela do Bloco de Notas: ($($retBloco.Left),$($retBloco.Top))-($($retBloco.Right),$($retBloco.Bottom))" }
foreach ($r in $resultados) {
    $relatorio += ("     faixa {0,-8} alfa {1,-3} ponto ({2}) -> {3} | atravessou: {4} | {5}" -f `
        $r.Faixa, $r.Alfa, $r.Ponto, $r.JanelaAtingida, $r.Atravessou, $r.Veredito)
}
Add-Content -Path $log -Value $relatorio -Encoding UTF8

# ---------------------------------------------------------------- passo físico
Write-Host ""
Write-Host "=== Passo que exige uma pessoa: o clique fisico ===" -ForegroundColor Yellow
Write-Host "A janela do prototipo esta em ($X,$Y), tamanho 200x200 px." -ForegroundColor Yellow
Write-Host "Sao quatro molduras coloridas, cada uma rotulada com o alfa do seu interior:" -ForegroundColor Yellow
Write-Host ""
foreach ($b in $bandas) {
    $obs = switch ($b.Alfa) {
        0   { 'moldura VERMELHA - interior invisivel; o clique DEVE ir para o Bloco de Notas' }
        1   { 'moldura LARANJA  - interior quase invisivel; o clique DEVE ficar no Buzzy' }
        255 { 'preenchimento VERDE - visivel; o clique DEVE ficar no Buzzy' }
        128 { 'moldura AZUL     - interior meio transparente; o clique DEVE ficar no Buzzy' }
    }
    Write-Host ("  alfa {0,-3} clique em ({1},{2})  {3}" -f $b.Alfa, $b.TelaX, $b.TelaY, $obs)
}
Write-Host ""
Write-Host "1. Clique UMA vez no centro de cada uma das quatro faixas." -ForegroundColor Yellow
Write-Host "2. Confira se o cursor de texto do Bloco de Notas continua piscando e se da para digitar." -ForegroundColor Yellow
Write-Host "3. Rode: .\ler-p1.ps1" -ForegroundColor Yellow
Write-Host ""

if ($ManterAberto) {
    Write-Host "Prototipo aberto no PID $($proc.Id). Encerre depois com: Stop-Process -Id $($proc.Id)" -ForegroundColor DarkGray
} else {
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 700 }
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Write-Host "Prototipo encerrado. Log: $log" -ForegroundColor DarkGray
}
