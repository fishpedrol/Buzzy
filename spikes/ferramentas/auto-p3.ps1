<#
    auto-p3.ps1 — gestos SINTÉTICOS para os cenários de P3 que o teste humano não cobriu.

    Ferramenta de teste fora do produto (SECURITY.md 3.2 permite injeção só aqui).
    Todo evento carrega a marca de injetado; o resultado é rotulado como sintético.

    Cenários, nesta ordem:
      B7  clique curto (move 1 px, abaixo do limiar de 4 px) -> deve virar CLIQUE
      B2  arraste de 144 px -> deve virar ARRASTE sem tirar o foco do Bloco de Notas
      B6  ClickLock: ligado SÓ EM MEMÓRIA (fWinIni = 0, nada gravado no perfil) e
          restaurado no fim, mesmo com erro
      B4  Alt+Tab no meio do arraste (por último, porque troca a janela ativa)

    Texto: entre os gestos, digita marcadores no Bloco de Notas, SOMENTE se ele estiver
    em primeiro plano naquele instante. O script não lê o Bloco de Notas (SECURITY.md
    proíbe); quem confere se o texto entrou é a pessoa, olhando a janela.

    Segurança operacional: se houver qualquer processo do Bloco de Notas aberto, o script
    aborta. Ele nunca encerra documentos do usuário à força.
#>

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$exe = Join-Path $raiz 'BuzzySpike\bin\Release\net10.0-windows\BuzzySpike.exe'
$log = Join-Path $raiz 'resultados\p3.log'
if (-not (Test-Path $exe)) { throw "Protótipo não compilado em $exe" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class G
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    // INPUT em x64 tem 40 bytes: type em 0, union a partir de 8.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public MOUSEINPUT mi;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    const uint MOVE = 0x0001, LDOWN = 0x0002, LUP = 0x0004, ABS = 0x8000, VDESK = 0x4000;
    const uint KUP = 0x0002, KUNICODE = 0x0004;

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetCapture();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] public static extern bool SpiSet(uint a, uint u, IntPtr pv, uint f);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] public static extern bool SpiGet(uint a, uint u, ref int pv, uint f);

    public static int Tamanho() { return Marshal.SizeOf(typeof(INPUT)); }

    static INPUT M(int x, int y, uint f)
    {
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        var i = new INPUT { type = 0 };
        i.mi.dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
        i.mi.dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
        i.mi.dwFlags = f | ABS | VDESK;
        return i;
    }

    public static uint Mover(int x, int y) { return SendInput(1, new[] { M(x, y, MOVE) }, Tamanho()); }
    public static uint Descer(int x, int y) { return SendInput(2, new[] { M(x, y, MOVE), M(x, y, LDOWN) }, Tamanho()); }
    public static uint Subir(int x, int y) { return SendInput(2, new[] { M(x, y, MOVE), M(x, y, LUP) }, Tamanho()); }

    static INPUT K(ushort vk, ushort scan, uint f) { var i = new INPUT { type = 1 }; i.ki.wVk = vk; i.ki.wScan = scan; i.ki.dwFlags = f; return i; }

    public static uint AltTab()
    {
        return SendInput(4, new[] { K(0x12, 0, 0), K(0x09, 0, 0), K(0x09, 0, KUP), K(0x12, 0, KUP) }, Tamanho());
    }

    public static uint Digitar(string s)
    {
        var lista = new System.Collections.Generic.List<INPUT>();
        foreach (char c in s) { lista.Add(K(0, c, KUNICODE)); lista.Add(K(0, c, KUNICODE | KUP)); }
        return SendInput((uint)lista.Count, lista.ToArray(), Tamanho());
    }

    public static uint PidDe(IntPtr h) { uint p; GetWindowThreadProcessId(h, out p); return p; }
}
'@

$null = [G]::SetProcessDpiAwarenessContext([IntPtr](-4))
if ([G]::Tamanho() -ne 40) { throw "INPUT com $([G]::Tamanho()) bytes; esperado 40. Abortando antes de injetar." }

$SPI_GETMOUSECLICKLOCK = 0x101E
$SPI_SETMOUSECLICKLOCK = 0x101F
$clickLockOriginal = 0
[void][G]::SpiGet($SPI_GETMOUSECLICKLOCK, 0, [ref]$clickLockOriginal, 0)

$cursor0 = New-Object G+POINT
[void][G]::GetCursorPos([ref]$cursor0)

$relatorio = [System.Collections.Generic.List[string]]::new()
function Nota([string] $t) { $relatorio.Add($t); Write-Host $t }

$proc = $null
$hSpike = [IntPtr]::Zero

try {
    # ------------------------------------------------ Bloco de Notas em primeiro plano
    $notepadAberto = @(Get-Process -Name Notepad -ErrorAction SilentlyContinue)
    if ($notepadAberto.Count -gt 0) {
        $avisoNotepad = "ABORTADO: o Bloco de Notas já está aberto. Salve e feche manualmente todas as janelas e abas antes de rodar este teste; o script não fecha documentos do usuário."
        Nota $avisoNotepad
        throw $avisoNotepad
    }
    Start-Process notepad
    $pidsBloco = @()
    $hBloco = [IntPtr]::Zero
    $lim = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $lim) {
        $p = @(Get-Process -Name Notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
        if ($p.Count -gt 0) { $hBloco = $p[0].MainWindowHandle; break }
        Start-Sleep -Milliseconds 300
    }
    if ($hBloco -eq [IntPtr]::Zero) { throw "Bloco de Notas não encontrado; abortando antes de injetar." }
    Start-Sleep -Seconds 2
    $pidsBloco = @(Get-Process -Name Notepad -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })

    function BlocoNaFrente { return ($pidsBloco -contains [int][G]::PidDe([G]::GetForegroundWindow())) }

    function DigitarSeSeguro([string] $marca) {
        if (BlocoNaFrente) {
            $n = [G]::Digitar($marca)
            Nota ("   texto '" + $marca.Trim() + "': Bloco de Notas estava na frente; " + $n + " eventos de tecla aceitos")
        } else {
            $fgPid = [G]::PidDe([G]::GetForegroundWindow())
            $fgNome = try { (Get-Process -Id $fgPid).ProcessName } catch { '?' }
            Nota ("   texto '" + $marca.Trim() + "': NÃO digitado — janela da frente era " + $fgNome + " (segurança)")
        }
        Start-Sleep -Milliseconds 300
    }

    # ------------------------------------------------ protótipo sobre o Bloco de Notas
    $r = New-Object G+RECT
    [void][G]::GetWindowRect($hBloco, [ref]$r)
    $X = [int](($r.Left + $r.Right) / 2 - 100)
    $Y = [int](($r.Top + $r.Bottom) / 2 - 100)

    $linhasAntes = if (Test-Path $log) { @(Get-Content $log -Encoding UTF8).Count } else { 0 }
    $proc = Start-Process -FilePath $exe -ArgumentList '--modo','p3','--x',$X,'--y',$Y -PassThru
    Start-Sleep -Seconds 3

    $novas = @(Get-Content $log -Encoding UTF8 | Select-Object -Skip $linhasAntes)
    $hSpike = [IntPtr][int64](($novas | Where-Object { $_ -match 'SONDA\|HWND\|' } | Select-Object -First 1) -replace '.*SONDA\|HWND\|','')

    # Ponto de agarre: coluna de rótulos, opaca, em coordenada local (20,100).
    $gx = $X + 20; $gy = $Y + 100

    Nota ""
    Nota "=== P3 sintético (SendInput) — Bloco de Notas em ($($r.Left),$($r.Top)), Buzzy em ($X,$Y) ==="
    Nota ("   Bloco de Notas na frente ao abrir o Buzzy: " + (BlocoNaFrente))
    DigitarSeSeguro "1-antes "

    # ------------------------------------------------ B7: clique curto
    $marcaB7 = @(Get-Content $log -Encoding UTF8).Count
    [void][G]::Descer($gx, $gy); Start-Sleep -Milliseconds 60
    [void][G]::Mover($gx + 1, $gy); Start-Sleep -Milliseconds 60
    [void][G]::Subir($gx + 1, $gy); Start-Sleep -Milliseconds 500
    $b7 = @(Get-Content $log -Encoding UTF8 | Select-Object -Skip $marcaB7)
    $b7Clique = @($b7 | Where-Object { $_ -match 'como CLIQUE' }).Count
    $b7Arraste = @($b7 | Where-Object { $_ -match 'como ARRASTE' }).Count
    Nota ("B7 clique curto (1 px): CLIQUE=" + $b7Clique + " ARRASTE=" + $b7Arraste + " -> " + $(if ($b7Clique -eq 1 -and $b7Arraste -eq 0) { 'OK' } else { 'DIVERGENTE' }))
    DigitarSeSeguro "2-depois-do-clique "

    # ------------------------------------------------ B2: arraste simples
    $marcaB2 = @(Get-Content $log -Encoding UTF8).Count
    $cx = $gx; $cy = $gy
    [void][G]::Descer($cx, $cy); Start-Sleep -Milliseconds 60
    for ($i = 1; $i -le 12; $i++) { $cx += 12; [void][G]::Mover($cx, $cy); Start-Sleep -Milliseconds 30 }
    [void][G]::Subir($cx, $cy); Start-Sleep -Milliseconds 500
    $b2 = @(Get-Content $log -Encoding UTF8 | Select-Object -Skip $marcaB2)
    $b2Arraste = @($b2 | Where-Object { $_ -match 'como ARRASTE' }).Count
    $b2Roubo = @($b2 | Where-Object { $_ -match 'PROBLEMA' }).Count
    Nota ("B2 arraste 144 px: ARRASTE=" + $b2Arraste + " roubos de foco=" + $b2Roubo + " Bloco na frente depois=" + (BlocoNaFrente) + " -> " + $(if ($b2Arraste -eq 1 -and $b2Roubo -eq 0 -and (BlocoNaFrente)) { 'OK' } else { 'DIVERGENTE' }))
    DigitarSeSeguro "3-depois-do-arraste "

    # ------------------------------------------------ B6: ClickLock
    [void][G]::SpiSet($SPI_SETMOUSECLICKLOCK, 0, [IntPtr]1, 0)
    $clAgora = 0; [void][G]::SpiGet($SPI_GETMOUSECLICKLOCK, 0, [ref]$clAgora, 0)
    Nota ("B6 ClickLock ligado só em memória: " + ($clAgora -ne 0))

    $marcaB6 = @(Get-Content $log -Encoding UTF8).Count
    $rb = New-Object G+RECT; [void][G]::GetWindowRect($hSpike, [ref]$rb); $cx = $rb.Left + 20; $cy = $rb.Top + 100
    [void][G]::Descer($cx, $cy)
    Start-Sleep -Milliseconds 1700          # segura parado além do tempo de trava (1200 ms)
    [void][G]::Subir($cx, $cy)              # com a trava, este "soltar" deve ser engolido
    Start-Sleep -Milliseconds 400
    for ($i = 1; $i -le 10; $i++) { $cx -= 12; [void][G]::Mover($cx, $cy); Start-Sleep -Milliseconds 40 }
    Start-Sleep -Milliseconds 300
    [void][G]::Descer($cx, $cy); Start-Sleep -Milliseconds 60   # clique que libera a trava
    [void][G]::Subir($cx, $cy); Start-Sleep -Milliseconds 600

    [void][G]::SpiSet($SPI_SETMOUSECLICKLOCK, 0, [IntPtr]$clickLockOriginal, 0)

    # Critério: com a trava, o "soltar" depois de segurar 1,7 s é engolido, então o MESMO
    # gesto continua nos movimentos seguintes e só termina no clique de liberação.
    # Sem a trava, apareceria um SOLTAR classificado como CLIQUE logo após segurar, e os
    # movimentos seguintes seriam só passagem do cursor, sem ARRASTE.
    # (GetCapture não serve de prova aqui: chamado de outro processo, retorna sempre 0.)
    $b6 = @(Get-Content $log -Encoding UTF8 | Select-Object -Skip $marcaB6)
    $b6Movs = @($b6 | Where-Object { $_ -match 'Movimentos aplicados: (\d+)' } | ForEach-Object { [int]$matches[1] })
    $b6MaxMov = if ($b6Movs.Count) { ($b6Movs | Measure-Object -Maximum).Maximum } else { 0 }
    $b6Arrastou = @($b6 | Where-Object { $_ -match 'ARRASTE iniciado' }).Count -gt 0
    $b6Pressionares = @($b6 | Where-Object { $_ -match 'PRESSIONAR:' }).Count
    $b6CliqueSolto = @($b6 | Where-Object { $_ -match 'como CLIQUE' }).Count
    $b6Roubo = @($b6 | Where-Object { $_ -match 'PROBLEMA' }).Count
    $travou = $b6Arrastou -and $b6Pressionares -eq 1 -and $b6CliqueSolto -eq 0 -and $b6MaxMov -gt 0
    Nota ("B6 ClickLock: um só gesto=" + ($b6Pressionares -eq 1) + " soltar engolido (sem CLIQUE)=" + ($b6CliqueSolto -eq 0) + " arrastou depois de soltar=" + $b6Arrastou + " movimentos=" + $b6MaxMov + " roubos=" + $b6Roubo + " -> " + $(if ($travou -and $b6Roubo -eq 0) { 'OK (a trava segurou o arraste)' } else { 'DIVERGENTE' }))
    DigitarSeSeguro "4-depois-do-clicklock "

    # ------------------------------------------------ B4: Alt+Tab no meio do arraste
    $rb = New-Object G+RECT; [void][G]::GetWindowRect($hSpike, [ref]$rb); $cx = $rb.Left + 20; $cy = $rb.Top + 100
    $marcaB4 = @(Get-Content $log -Encoding UTF8).Count
    [void][G]::Descer($cx, $cy); Start-Sleep -Milliseconds 60
    for ($i = 1; $i -le 6; $i++) { $cx += 12; [void][G]::Mover($cx, $cy); Start-Sleep -Milliseconds 30 }
    $nAlt = [G]::AltTab(); Start-Sleep -Milliseconds 700
    for ($i = 1; $i -le 4; $i++) { $cx += 12; [void][G]::Mover($cx, $cy); Start-Sleep -Milliseconds 30 }
    [void][G]::Subir($cx, $cy); Start-Sleep -Milliseconds 600

    $b4 = @(Get-Content $log -Encoding UTF8 | Select-Object -Skip $marcaB4)
    $b4Fim = @($b4 | Where-Object { $_ -match 'Motivo do término' }).Count -gt 0
    $b4Roubo = @($b4 | Where-Object { $_ -match 'PROBLEMA' }).Count
    $capturaDepois = [G]::GetCapture()
    $fgPid = [G]::PidDe([G]::GetForegroundWindow())
    $fgNome = try { (Get-Process -Id $fgPid).ProcessName } catch { '?' }
    $buzzyNaFrente = ([G]::GetForegroundWindow() -eq $hSpike)
    Nota ("B4 Alt+Tab no meio (" + $nAlt + "/4 teclas aceitas): gesto encerrado=" + $b4Fim + " captura presa=" + ($capturaDepois -ne [IntPtr]::Zero) + " Buzzy na frente=" + $buzzyNaFrente + " (frente agora: " + $fgNome + ") roubos=" + $b4Roubo + " -> " + $(if ($b4Fim -and $capturaDepois -eq [IntPtr]::Zero -and -not $buzzyNaFrente -and $b4Roubo -eq 0) { 'OK' } else { 'DIVERGENTE' }))
}
finally {
    # Restaurações que valem mesmo com erro
    [void][G]::SpiSet($SPI_SETMOUSECLICKLOCK, 0, [IntPtr]$clickLockOriginal, 0)
    $clFinal = 0; [void][G]::SpiGet($SPI_GETMOUSECLICKLOCK, 0, [ref]$clFinal, 0)
    Nota ("ClickLock restaurado para o original (" + ($clickLockOriginal -ne 0) + "): agora=" + ($clFinal -ne 0))
    [void][G]::SetCursorPos($cursor0.X, $cursor0.Y)

    if ($hSpike -ne [IntPtr]::Zero) { [void][G]::PostMessage($hSpike, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 800
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }

    $cab = "---- Sonda de gestos sintéticos (SendInput), $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ----"
    try { Add-Content -Path $log -Value (@($cab) + ($relatorio | ForEach-Object { "     " + $_ })) -Encoding UTF8 } catch { }
}

Write-Host ""
Write-Host "Pronto. O Bloco de Notas ficou aberto: confira se aparecem os marcadores 1 a 4." -ForegroundColor Yellow
