<#
    sonda-p3.ps1 — prepara o teste de arraste e confere o que dá sem uma pessoa.

    Uso:
      .\sonda-p3.ps1                 # captura simples
      .\sonda-p3.ps1 -Margem         # plano B: margem alfa 1 durante o gesto
      .\sonda-p3.ps1 -SemBlocoDeNotas

    Parte A (sozinha): A1 a janela aparece sem tirar o foco do Bloco de Notas; A2 ela vai
    do primário ao secundário (x negativo) por SetWindowPos + SWP_NOACTIVATE sem mexer no
    foco; A3 tem WS_EX_NOACTIVATE e WS_EX_LAYERED.
    Parte B (pessoa): o arraste de verdade. O script só imprime os passos e deixa aberto.
    A2 não é arraste: separa o movimento/foco da captura do mouse. Só a Parte B vale.
#>

[CmdletBinding()]
param(
    [switch] $Margem,
    [switch] $SemBlocoDeNotas
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$exe = Join-Path $raiz 'BuzzySpike\bin\Release\net10.0-windows\BuzzySpike.exe'
$modo = if ($Margem) { 'p3-margem' } else { 'p3' }
$log = Join-Path $raiz "resultados\$modo.log"

if (-not (Test-Path $exe)) { throw "Protótipo não compilado em $exe" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class P3
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const int GWL_EXSTYLE = -20;

    public static uint PidDe(IntPtr h) { uint p; GetWindowThreadProcessId(h, out p); return p; }
}
'@

$null = [P3]::SetProcessDpiAwarenessContext([IntPtr](-4))

function NomeDaJanela([IntPtr] $h) {
    if ($h -eq [IntPtr]::Zero) { return 'nenhuma' }
    $pidJanela = [P3]::PidDe($h)
    $nome = try { (Get-Process -Id $pidJanela -ErrorAction Stop).ProcessName } catch { '?' }
    return "$nome (pid $pidJanela, hwnd $h)"
}

# ---------------------------------------------------------------- Bloco de Notas
$hBloco = [IntPtr]::Zero
$pidBloco = $null

if (-not $SemBlocoDeNotas) {
    Write-Host "Abrindo o Bloco de Notas..." -ForegroundColor Cyan
    Start-Process notepad
    $limite = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $limite) {
        $procs = @(Get-Process -Name 'Notepad' -ErrorAction SilentlyContinue |
                   Where-Object { $_.MainWindowHandle -ne 0 })
        if ($procs.Count -gt 0) { $pidBloco = $procs[0].Id; $hBloco = $procs[0].MainWindowHandle; break }
        Start-Sleep -Milliseconds 300
    }
    if ($hBloco -eq [IntPtr]::Zero) { Write-Warning "Não localizei a janela do Bloco de Notas." }
    Start-Sleep -Seconds 2
}

$focoAntes = [P3]::GetForegroundWindow()
Write-Host ("Janela em primeiro plano ANTES de abrir o protótipo: " + (NomeDaJanela $focoAntes)) -ForegroundColor DarkGray

# Protótipo por cima do Bloco de Notas, se ele existir.
$X = 700; $Y = 400
if ($hBloco -ne [IntPtr]::Zero) {
    $r = New-Object P3+RECT
    if ([P3]::GetWindowRect($hBloco, [ref]$r)) {
        $X = [int](($r.Left + $r.Right) / 2 - 100)
        $Y = [int](($r.Top + $r.Bottom) / 2 - 100)
        Write-Host "Bloco de Notas em ($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom))" -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------- protótipo
if (Test-Path $log) { Remove-Item $log -Force }

Write-Host "Abrindo o protótipo $modo em ($X,$Y)..." -ForegroundColor Cyan
$proc = Start-Process -FilePath $exe -ArgumentList '--modo', $modo, '--x', $X, '--y', $Y -PassThru

$limite = (Get-Date).AddSeconds(20)
while (-not (Test-Path $log) -and (Get-Date) -lt $limite) { Start-Sleep -Milliseconds 200 }
if (-not (Test-Path $log)) { throw "O protótipo não escreveu o log em 20 s." }
Start-Sleep -Milliseconds 1200

$linhas = Get-Content $log -Encoding UTF8
$hSpike = [IntPtr][int64](($linhas | Where-Object { $_ -match 'SONDA\|HWND\|' } | Select-Object -First 1) -replace '.*SONDA\|HWND\|', '')

$relatorio = @()
$relatorio += "---- Sonda automatizada de P3, $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ----"
$relatorio += "     Parte A: sem injecao de input. O arraste real fica para a Parte B, fisica."

# ---------------------------------------------------------------- A1: foco ao aparecer
$focoDepois = [P3]::GetForegroundWindow()
$a1 = ($focoDepois -ne $hSpike)

Write-Host ""
Write-Host "=== A1: a janela aparece sem roubar o foco ===" -ForegroundColor Green
Write-Host ("  antes  : " + (NomeDaJanela $focoAntes))
Write-Host ("  depois : " + (NomeDaJanela $focoDepois))
Write-Host ("  foco continua fora do protótipo: " + $(if ($a1) { 'SIM (correto)' } else { 'NÃO (PROBLEMA)' })) `
    -ForegroundColor $(if ($a1) { 'Green' } else { 'Red' })
if ($focoAntes -eq $focoDepois) {
    Write-Host "  o foco nem mudou de janela" -ForegroundColor Green
}

$relatorio += "     A1 foco ao aparecer: antes=$(NomeDaJanela $focoAntes) depois=$(NomeDaJanela $focoDepois)"
$relatorio += "        foco fora do prototipo: $(if ($a1) { 'SIM (correto)' } else { 'NAO (PROBLEMA)' })"
$relatorio += "        foco inalterado pela abertura: $(if ($focoAntes -eq $focoDepois) { 'SIM' } else { 'NAO' })"

# ---------------------------------------------------------------- A3: estilos
$ex = [int64][P3]::GetWindowLongPtr($hSpike, [P3]::GWL_EXSTYLE)
$temNoActivate = (($ex -band 0x08000000) -ne 0)
$temLayered = (($ex -band 0x00080000) -ne 0)

Write-Host ""
Write-Host "=== A3: estilos da janela ===" -ForegroundColor Green
Write-Host ("  estilo estendido : 0x{0:X8}" -f $ex)
Write-Host ("  WS_EX_NOACTIVATE : " + $(if ($temNoActivate) { 'SIM' } else { 'NÃO' }))
Write-Host ("  WS_EX_LAYERED    : " + $(if ($temLayered) { 'SIM' } else { 'NÃO' }))
$relatorio += ("     A3 estilo estendido 0x{0:X8} | NOACTIVATE={1} | LAYERED={2}" -f $ex, $temNoActivate, $temLayered)

# ---------------------------------------------------------------- A2: travessia entre monitores
Write-Host ""
Write-Host "=== A2: travessia programática do primário para o secundário ===" -ForegroundColor Green
Write-Host "  (SetWindowPos com SWP_NOACTIVATE; isto NÃO é o arraste, é o mecanismo de movimento)" -ForegroundColor DarkGray

$rSpike = New-Object P3+RECT
[void][P3]::GetWindowRect($hSpike, [ref]$rSpike)
$yFixo = $rSpike.Top
$origemX = $rSpike.Left

$passos = @(1200, 800, 400, 100, -100, -400, -900, -1400, -1800)
$falhasFoco = 0
$linhasA2 = @()

foreach ($px in $passos) {
    [void][P3]::SetWindowPos($hSpike, [IntPtr]::Zero, $px, $yFixo, 0, 0,
        ([P3]::SWP_NOSIZE -bor [P3]::SWP_NOZORDER -bor [P3]::SWP_NOACTIVATE))
    Start-Sleep -Milliseconds 350

    $f = [P3]::GetForegroundWindow()
    $ok = ($f -ne $hSpike)
    if (-not $ok) { $falhasFoco++ }

    $rAgora = New-Object P3+RECT
    [void][P3]::GetWindowRect($hSpike, [ref]$rAgora)
    $monitor = if ($rAgora.Left -lt 0) { 'secundário (x negativo)' } else { 'primário' }

    $linha = ("  x={0,6} -> janela em ({1},{2}) no {3}; foco fora do protótipo: {4}" -f `
        $px, $rAgora.Left, $rAgora.Top, $monitor, $(if ($ok) { 'sim' } else { 'NÃO' }))
    Write-Host $linha -ForegroundColor $(if ($ok) { 'Gray' } else { 'Red' })
    $linhasA2 += ("     A2 x=$px -> ($($rAgora.Left),$($rAgora.Top)) $monitor foco_ok=$ok")
}

# Volta pra cima do Bloco de Notas pra Parte B.
[void][P3]::SetWindowPos($hSpike, [IntPtr]::Zero, $X, $Y, 0, 0,
    ([P3]::SWP_NOSIZE -bor [P3]::SWP_NOZORDER -bor [P3]::SWP_NOACTIVATE))

Write-Host ""
if ($falhasFoco -eq 0) {
    Write-Host "  A2: a janela atravessou os dois monitores, incluindo coordenadas negativas," -ForegroundColor Green
    Write-Host "      sem nenhuma troca de foco para o protótipo." -ForegroundColor Green
} else {
    Write-Host "  A2: em $falhasFoco passo(s) o protótipo ficou em primeiro plano. Ver acima." -ForegroundColor Red
}

$relatorio += $linhasA2
$relatorio += "     A2 resumo: passos=$($passos.Count) falhas_de_foco=$falhasFoco"
Add-Content -Path $log -Value $relatorio -Encoding UTF8

# ---------------------------------------------------------------- Parte B
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host " PARTE B — o arraste físico, que só uma pessoa pode fazer" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "A janela do protótipo está em ($X,$Y), sobre o Bloco de Notas." -ForegroundColor Yellow
Write-Host "Agarre a janela pela COLUNA ESCURA da esquerda ou pela faixa VERDE:" -ForegroundColor Yellow
Write-Host "essas são as áreas opacas, que recebem o clique." -ForegroundColor Yellow
Write-Host ""
Write-Host "Faça, nesta ordem, e sem pressa:" -ForegroundColor Yellow
Write-Host "  B1. Clique no Bloco de Notas e digite algo. Deixe o cursor de texto piscando." -ForegroundColor Yellow
Write-Host "  B2. Arraste o Buzzy devagar por alguns centímetros e solte. Digite de novo no" -ForegroundColor Yellow
Write-Host "      Bloco de Notas SEM clicar nele antes: o texto tem de entrar." -ForegroundColor Yellow
Write-Host "  B3. Arraste bem RÁPIDO, saindo fora da janela, e solte o botão FORA dela." -ForegroundColor Yellow
Write-Host "  B4. Comece um arraste e, no meio dele, aperte Alt+Tab. Depois solte o botão." -ForegroundColor Yellow
Write-Host "  B5. Arraste do monitor primário para o secundário (o da esquerda, x negativo)" -ForegroundColor Yellow
Write-Host "      e solte lá. Depois traga de volta." -ForegroundColor Yellow
Write-Host "  B6. Ligue o ClickLock do Windows (Configurações > Bluetooth e dispositivos >" -ForegroundColor Yellow
Write-Host "      Mouse > Configurações adicionais do mouse > Ativar ClickLock) e repita B2." -ForegroundColor Yellow
Write-Host "  B7. Dê um clique curto, sem mover: tem de virar CLIQUE, não arraste." -ForegroundColor Yellow
Write-Host ""
Write-Host "Depois de tudo, rode:  .\ler-p3.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "Protótipo aberto no PID $($proc.Id). Para encerrar: Stop-Process -Id $($proc.Id)" -ForegroundColor DarkGray
Write-Host "Log: $log" -ForegroundColor DarkGray
