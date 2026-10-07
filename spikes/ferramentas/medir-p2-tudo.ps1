<#
    medir-p2-tudo.ps1 — roda toda a sequência de medição de desempenho (~92 min).

    Uso: .\medir-p2-tudo.ps1 [-MinutosRepouso 60] [-MinutosAnimacao 10]

    Ordem:
      1. repouso, sem timer nenhum         (amostra a cada 5 s)
      2. animação a 10 qps                 (amostra a cada 1 s)
      3. animação a 60 qps, DispatcherTimer
      4. animação a 60 qps, pelo compositor do WPF
    O 4 existe porque o DispatcherTimer pedindo 60 entregou ~39; medir os dois separa
    "60 pedidos" de "60 entregues".

    Apaga os p2-* anteriores pra evidência ser só desta rodada; não mexe nos outros testes.
#>

[CmdletBinding()]
param(
    [int] $MinutosRepouso = 60,
    [int] $MinutosAnimacao = 10
)

$ErrorActionPreference = 'Continue'

$aqui = Split-Path -Parent $PSCommandPath
$resultados = Join-Path (Split-Path -Parent $aqui) 'resultados'
$trilha = Join-Path $resultados 'p2-sequencia.txt'

Get-ChildItem $resultados -Filter 'p2-*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

function Anotar([string] $texto) {
    $linha = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $texto"
    Add-Content -Path $trilha -Value $linha -Encoding UTF8
    Write-Host $linha
}

$etapas = @(
    @{ Modo = 'p2-repouso';    Minutos = $MinutosRepouso;  Intervalo = 5 },
    @{ Modo = 'p2-anim10';     Minutos = $MinutosAnimacao; Intervalo = 1 },
    @{ Modo = 'p2-anim60';     Minutos = $MinutosAnimacao; Intervalo = 1 },
    @{ Modo = 'p2-anim60comp'; Minutos = $MinutosAnimacao; Intervalo = 1 }
)

Anotar "INICIO da sequência de P2. Etapas: $($etapas.Count)."

foreach ($e in $etapas) {
    Anotar "INICIO $($e.Modo): $($e.Minutos) min, intervalo $($e.Intervalo) s."
    try {
        & (Join-Path $aqui 'medir-p2.ps1') `
            -Modo $e.Modo -Minutos $e.Minutos -IntervaloSegundos $e.Intervalo |
            Out-File -FilePath (Join-Path $resultados "$($e.Modo).saida.txt") -Encoding UTF8
        Anotar "FIM $($e.Modo): relatório gravado."
    }
    catch {
        Anotar "ERRO em $($e.Modo): $($_.Exception.Message)"
    }

    # Deixa a máquina assentar pra próxima etapa não herdar atividade da anterior.
    Start-Sleep -Seconds 20
}

Anotar "FIM da sequência de P2."
