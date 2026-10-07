<#
    ler-p3.ps1 — resume o teste de arraste a partir do log do protótipo.

    Uso: .\ler-p3.ps1 [-Modo p3|p3-margem]

    Procura, em ordem de importância: gesto que terminou com o foco na nossa janela (reprova),
    clique x arraste pelo limiar do sistema, captura terminando por um caminho só, latência
    entre receber o movimento e aplicar a posição, e x negativo (passou pro outro monitor).
#>

[CmdletBinding()]
param(
    [ValidateSet('p3', 'p3-margem')]
    [string] $Modo = 'p3'
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$log = Join-Path $raiz "resultados\$Modo.log"
if (-not (Test-Path $log)) { throw "Sem log de $Modo em $log. Rode antes: .\sonda-p3.ps1" }

$linhas = Get-Content $log -Encoding UTF8

$pressionares = @($linhas | Where-Object { $_ -match '^\[.*\] PRESSIONAR:' })
$arrastesIniciados = @($linhas | Where-Object { $_ -match 'ARRASTE iniciado' })
$soltares = @($linhas | Where-Object { $_ -match '^\[.*\] SOLTAR em cliente' })
$classifArraste = @($soltares | Where-Object { $_ -match 'como ARRASTE' })
$classifClique = @($soltares | Where-Object { $_ -match 'como CLIQUE' })
$capturasPerdidas = @($linhas | Where-Object { $_ -match 'WM_CAPTURECHANGED' })
$roubosDeFoco = @($linhas | Where-Object { $_ -match 'PROBLEMA' })
$latencias = @($linhas | Where-Object { $_ -match 'M5 latência' })
$fins = @($linhas | Where-Object { $_ -match 'Motivo do término' })
$posicoes = @($linhas | Where-Object { $_ -match 'Posição final\s+: \(-' })
$capturaOk = @($linhas | Where-Object { $_ -match 'PRESSIONAR: captura = \d+ \(nossa: True\)' })

Write-Host ""
Write-Host "=== P3 / $Modo — resumo dos gestos físicos ===" -ForegroundColor Green
Write-Host ""
Write-Host ("  gestos iniciados (mouse down)      : " + $pressionares.Count)
Write-Host ("  captura do mouse obtida por nós    : " + $capturaOk.Count + " de " + $pressionares.Count)
Write-Host ("  passaram do limiar (viraram arraste): " + $arrastesIniciados.Count)
Write-Host ("  gestos encerrados (mouse up)       : " + $soltares.Count)
Write-Host ("     classificados como ARRASTE      : " + $classifArraste.Count)
Write-Host ("     classificados como CLIQUE       : " + $classifClique.Count)
Write-Host ("  perdas de captura (WM_CAPTURECHANGED): " + $capturasPerdidas.Count)
Write-Host ("  posições finais em x negativo (outro monitor): " + $posicoes.Count)
Write-Host ""

if ($roubosDeFoco.Count -eq 0) {
    Write-Host "  FOCO: nenhum gesto deixou o foco na janela do Buzzy." -ForegroundColor Green
} else {
    Write-Host ("  FOCO: " + $roubosDeFoco.Count + " ocorrência(s) marcada(s) como PROBLEMA:") -ForegroundColor Red
    $roubosDeFoco | ForEach-Object { Write-Host ("    " + $_.Trim()) -ForegroundColor Red }
}

if ($latencias.Count -gt 0) {
    Write-Host ""
    Write-Host "  M5 — latência entre receber o movimento e aplicar a posição:" -ForegroundColor Green
    $latencias | ForEach-Object { Write-Host ("    " + ($_ -replace '^\s*', '').Trim()) }
}

if ($fins.Count -gt 0) {
    Write-Host ""
    Write-Host "  Motivos de término dos gestos:" -ForegroundColor Green
    $fins | ForEach-Object { Write-Host ("    " + $_.Trim()) }
}

$sondaA = @($linhas | Where-Object { $_ -match '^\s+A[123] ' })
if ($sondaA.Count -gt 0) {
    Write-Host ""
    Write-Host "  Parte A (automatizada) registrada no mesmo log:" -ForegroundColor Green
    $sondaA | ForEach-Object { Write-Host ("    " + $_.Trim()) }
}

Write-Host ""
if ($pressionares.Count -eq 0) {
    Write-Host "P3: SEM DADO FÍSICO. Nenhum gesto foi feito ainda." -ForegroundColor Yellow
    Write-Host "    Rode .\sonda-p3.ps1 e execute os passos B1 a B7." -ForegroundColor Yellow
} elseif ($roubosDeFoco.Count -gt 0) {
    Write-Host "P3: REPROVADO no estado atual. Houve troca de foco durante o gesto." -ForegroundColor Red
    Write-Host "    Conforme TODO.md, qualquer alternativa que roube foco não conta como aprovação." -ForegroundColor Red
} elseif ($arrastesIniciados.Count -eq 0) {
    Write-Host "P3: parcial. Houve clique, mas nenhum arraste passou do limiar do sistema." -ForegroundColor Yellow
} else {
    Write-Host "P3: os gestos registrados até agora se comportaram como a arquitetura previa." -ForegroundColor Green
    Write-Host "    Confira se os sete passos B1 a B7 foram todos executados antes de declarar" -ForegroundColor Yellow
    Write-Host "    P3 aprovado: este resumo conta gestos, não sabe quais cenários você cobriu." -ForegroundColor Yellow
}
Write-Host ""
