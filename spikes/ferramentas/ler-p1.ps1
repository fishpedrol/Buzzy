<#
    ler-p1.ps1 — resume o teste de clique a partir do log do protótipo.

    Uso: .\ler-p1.ps1 [-CliqueiTodasAsQuatro]

    Alfa 0 não pode ter linha de clique (atravessou); alfa 1, 128 e 255 têm que ter.
    Pegadinha: sem linha pro alfa 0 só prova algo se a faixa foi clicada mesmo. Por isso o
    switch, e por isso existe a sonda automática.
#>

[CmdletBinding()]
param(
    # Só quando as quatro faixas foram clicadas de fato.
    [switch] $CliqueiTodasAsQuatro
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$log = Join-Path $raiz 'resultados\p1.log'
if (-not (Test-Path $log)) { throw "Sem log de P1 em $log. Rode antes: .\sonda-p1.ps1 -ManterAberto" }

$linhas = Get-Content $log -Encoding UTF8

$recebidos = @{}
foreach ($l in ($linhas | Where-Object { $_ -match 'P1\|CLIQUE\|' })) {
    $campos = (($l -replace '.*P1\|CLIQUE\|', '') -split '\|')
    $nome = $campos[0]
    if ($recebidos.ContainsKey($nome)) { $recebidos[$nome]++ } else { $recebidos[$nome] = 1 }
}

$esperado = [ordered]@{
    'alfa0'   = $false   # não pode chegar na nossa janela
    'alfa1'   = $true
    'alfa128' = $true
    'alfa255' = $true
}

Write-Host ""
Write-Host "=== P1: cliques físicos que CHEGARAM à janela do Buzzy ===" -ForegroundColor Green

$tabela = foreach ($nome in $esperado.Keys) {
    $qtd = if ($recebidos.ContainsKey($nome)) { $recebidos[$nome] } else { 0 }
    $chegou = ($qtd -gt 0)
    $deveriaChegar = $esperado[$nome]

    $veredito =
        if ($chegou -eq $deveriaChegar) { 'COMO ESPERADO' }
        elseif ($deveriaChegar -and -not $chegou) { 'SEM DADO (ninguém clicou?)' }
        else { 'DIVERGENTE' }

    [pscustomobject]@{
        Faixa            = $nome
        Cliques          = $qtd
        ChegouAoBuzzy    = if ($chegou) { 'sim' } else { 'nao' }
        Esperado         = if ($deveriaChegar) { 'chegar' } else { 'atravessar' }
        Veredito         = $veredito
    }
}

$tabela | Format-Table -AutoSize

$roubos = @($linhas | Where-Object { $_ -match 'Foco é nosso\? SIM' })
Write-Host ("Cliques que roubaram o foco: " + $roubos.Count) `
    -ForegroundColor $(if ($roubos.Count -eq 0) { 'Green' } else { 'Red' })

$sonda = @($linhas | Where-Object { $_ -match 'atravessou:' })
if ($sonda.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Sonda automatizada registrada no mesmo log ===" -ForegroundColor Green
    $sonda | ForEach-Object { Write-Host ("  " + $_.Trim()) }
}

Write-Host ""
$alfa0Silenciosa = -not $recebidos.ContainsKey('alfa0')
$opacasChegaram = @('alfa1', 'alfa128', 'alfa255' | Where-Object { $recebidos.ContainsKey($_) }).Count

if ($CliqueiTodasAsQuatro) {
    if ($alfa0Silenciosa -and $opacasChegaram -eq 3) {
        Write-Host "P1: CONFIRMADO fisicamente. A faixa alfa 0 deixou o clique passar e as" -ForegroundColor Green
        Write-Host "    faixas alfa 1, 128 e 255 receberam o clique." -ForegroundColor Green
    } else {
        Write-Host "P1: o resultado físico NÃO fecha. Faixas que faltam ou divergem acima." -ForegroundColor Red
        if (-not $alfa0Silenciosa) {
            Write-Host "    A faixa alfa 0 recebeu clique: isso REPROVA o requisito central." -ForegroundColor Red
        }
    }
} else {
    Write-Host "P1: leitura parcial. $opacasChegaram de 3 faixas opacas receberam clique;" -ForegroundColor Yellow
    Write-Host "    a faixa alfa 0 está $(if ($alfa0Silenciosa) { 'silenciosa (bom sinal)' } else { 'com clique registrado (PROBLEMA)' })." -ForegroundColor Yellow
    Write-Host "    Para fechar P1, clique nas quatro faixas e rode:" -ForegroundColor Yellow
    Write-Host "      .\ler-p1.ps1 -CliqueiTodasAsQuatro" -ForegroundColor Yellow
}
Write-Host ""
