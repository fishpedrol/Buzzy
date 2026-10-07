<#
    auto-p1.ps1 — clica via SendInput em cada faixa da figura de teste, com o protótipo
    por cima do Bloco de Notas, e diz quais cliques chegaram ao Buzzy.

    Uso: .\auto-p1.ps1 [-SemBlocoDeNotas]

    Ferramenta de teste; SendInput nunca vai pro produto. O evento sai marcado como injetado,
    mas o teste de acerto da janela layered só depende do alfa do pixel. O cursor se move de
    verdade e volta pro lugar no fim.
#>

[CmdletBinding()]
param([switch] $SemBlocoDeNotas)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$exe = Join-Path $raiz 'BuzzySpike\bin\Release\net10.0-windows\BuzzySpike.exe'
$log = Join-Path $raiz 'resultados\p1.log'
if (-not (Test-Path $exe)) { throw "Protótipo não compilado em $exe" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Inj
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
        public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo;
    }
    // MOUSEINPUT em x64 = 32 bytes; INPUT = 40 bytes. Nada de padding extra, senão
    // Marshal.SizeOf(INPUT) fica maior que o cbSize esperado e SendInput rejeita tudo.

    public const uint INPUT_MOUSE = 0;
    public const uint MOVE = 0x0001, LEFTDOWN = 0x0002, LEFTUP = 0x0004, ABSOLUTE = 0x8000, VIRTUALDESK = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint n, INPUT[] p, int cb);

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);

    static double Norm(int v, int origin, int span) { return (double)(v - origin) * 65535.0 / (span - 1); }

    public static int TamanhoInput() { return Marshal.SizeOf(typeof(INPUT)); }

    public static uint ClickAbs(int x, int y)
    {
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
        int vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        int ax = (int)Math.Round(Norm(x, vx, vw));
        int ay = (int)Math.Round(Norm(y, vy, vh));

        var move = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = MOVE | ABSOLUTE | VIRTUALDESK } };
        var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = LEFTDOWN | ABSOLUTE | VIRTUALDESK } };
        var up   = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = LEFTUP | ABSOLUTE | VIRTUALDESK } };
        // Retorna quantos eventos o Windows aceitou (esperado: 3). 0 = rejeitado.
        return SendInput(3, new[] { move, down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static uint PidEm(int x, int y) { POINT p; p.X = x; p.Y = y; uint id; GetWindowThreadProcessId(WindowFromPoint(p), out id); return id; }
    public static IntPtr JanelaEm(int x, int y) { POINT p; p.X = x; p.Y = y; return WindowFromPoint(p); }
}
'@

$null = [Inj]::SetProcessDpiAwarenessContext([IntPtr](-4))

# Abrir o Bloco de Notas por baixo
$pidBloco = $null; $rBloco = $null
if (-not $SemBlocoDeNotas) {
    Start-Process notepad
    $lim = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $lim) {
        $p = @(Get-Process -Name Notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
        if ($p.Count -gt 0) { $pidBloco = $p[0].Id; break }
        Start-Sleep -Milliseconds 300
    }
    Start-Sleep -Seconds 2
}

# Protótipo no centro do monitor primário
if (Test-Path $log) { Remove-Item $log -Force }
$X = 760; $Y = 380
$proc = Start-Process -FilePath $exe -ArgumentList '--modo','p1','--x',$X,'--y',$Y -PassThru
$lim = (Get-Date).AddSeconds(20)
while (-not (Test-Path $log) -and (Get-Date) -lt $lim) { Start-Sleep -Milliseconds 200 }
Start-Sleep -Milliseconds 900

$linhas = Get-Content $log -Encoding UTF8
$hSpike = [IntPtr][int64](($linhas | Where-Object { $_ -match 'SONDA\|HWND\|' } | Select-Object -First 1) -replace '.*SONDA\|HWND\|','')
$bandas = foreach ($l in ($linhas | Where-Object { $_ -match 'SONDA\|BANDA\|' })) {
    $c = (($l -replace '.*SONDA\|BANDA\|','') -split '\|')
    [pscustomobject]@{ Nome=$c[0]; Alfa=[int]$c[1]; X=[int]$c[2]; Y=[int]$c[3] }
}

Write-Host ""
Write-Host "ATENCAO: o cursor vai se mover sozinho. Nao toque no mouse." -ForegroundColor Yellow
for ($i = 5; $i -ge 1; $i--) { Write-Host "  clicando em $i..." -ForegroundColor DarkGray; Start-Sleep -Seconds 1 }

$cursor0 = New-Object Inj+POINT
[void][Inj]::GetCursorPos([ref]$cursor0)

Write-Host ("Tamanho da struct INPUT (deve ser 40 em x64): " + [Inj]::TamanhoInput()) -ForegroundColor DarkGray
$aceitosTotal = 0
foreach ($b in $bandas) {
    $aceitos = [Inj]::ClickAbs($b.X, $b.Y)
    $aceitosTotal += $aceitos
    if ($aceitos -ne 3) { Write-Host ("  AVISO: faixa " + $b.Nome + " so aceitou " + $aceitos + "/3 eventos") -ForegroundColor Red }
    Start-Sleep -Milliseconds 500
}
Write-Host ("Eventos de input aceitos pelo Windows: " + $aceitosTotal + " de " + ($bandas.Count * 3)) -ForegroundColor DarkGray

[void][Inj]::SetCursorPos($cursor0.X, $cursor0.Y)
Start-Sleep -Milliseconds 400

# Quais cliques chegaram ao Buzzy e onde caiu o de alfa 0
$linhas = Get-Content $log -Encoding UTF8
$recebidos = @{}
foreach ($l in ($linhas | Where-Object { $_ -match 'P1\|CLIQUE\|' })) {
    $nome = (($l -replace '.*P1\|CLIQUE\|','') -split '\|')[0]
    if ($recebidos.ContainsKey($nome)) { $recebidos[$nome]++ } else { $recebidos[$nome] = 1 }
}

Write-Host ""
Write-Host "=== P1: clique sintetico (SendInput) em cada faixa ===" -ForegroundColor Green
$tabela = foreach ($b in $bandas) {
    $chegou = $recebidos.ContainsKey($b.Nome)
    $hitPid = [Inj]::PidEm($b.X, $b.Y)
    $hitNome = try { (Get-Process -Id $hitPid).ProcessName } catch { '?' }
    $ehNossa = ([Inj]::JanelaEm($b.X, $b.Y) -eq $hSpike)
    $esperadoChegar = ($b.Alfa -ne 0)
    $ok = ($chegou -eq $esperadoChegar)
    [pscustomobject]@{
        Faixa = $b.Nome; Alfa = $b.Alfa
        ChegouAoBuzzy = if ($chegou) { 'sim' } else { 'nao' }
        HitTestAtual = if ($ehNossa) { 'BuzzySpike' } else { "$hitNome" }
        Esperado = if ($esperadoChegar) { 'chegar' } else { 'atravessar' }
        Veredito = if ($ok) { 'OK' } else { 'DIVERGENTE' }
    }
}
$tabela | Format-Table -AutoSize

$roubos = @($linhas | Where-Object { $_ -match 'Foco é nosso\? SIM' }).Count
Write-Host ("Cliques sinteticos que roubaram foco: " + $roubos) -ForegroundColor $(if ($roubos -eq 0) { 'Green' } else { 'Red' })

$rel = @("---- Sonda de clique sintetico (SendInput), $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ----",
         "     Ferramenta de teste fora do produto (SECURITY.md 3.2 permite). Input carrega marca de injetado.")
foreach ($r in $tabela) { $rel += ("     faixa {0,-8} alfa {1,-3} chegou={2} hit={3} {4}" -f $r.Faixa,$r.Alfa,$r.ChegouAoBuzzy,$r.HitTestAtual,$r.Veredito) }
Add-Content -Path $log -Value $rel -Encoding UTF8

# Fecha o protótipo pelo HWND
Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class K{[DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);}'
[void][K]::PostMessage($hSpike, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
Start-Sleep -Milliseconds 800
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }

$faltam = @($bandas | Where-Object { $_.Alfa -ne 0 -and -not $recebidos.ContainsKey($_.Nome) })
$alfa0Silenciosa = -not $recebidos.ContainsKey('alfa0')
Write-Host ""
if ($faltam.Count -eq 0 -and $alfa0Silenciosa -and $roubos -eq 0) {
    Write-Host "P1 (sintetico): as tres faixas opacas receberam o clique, a alfa 0 atravessou," -ForegroundColor Green
    Write-Host "e nenhum clique roubou o foco. Consistente com a sonda WindowFromPoint." -ForegroundColor Green
} else {
    Write-Host "P1 (sintetico): resultado nao fecha; ver a tabela." -ForegroundColor Red
}
Write-Host "Log: $log" -ForegroundColor DarkGray