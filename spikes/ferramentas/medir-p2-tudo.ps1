<#
    medir-p2-tudo.ps1 — roda a sequência completa de medição de P2.

    Ordem, conforme o pedido da Etapa 0B:
      1. repouso, 60 min, sem timer nenhum          (intervalo de amostragem 5 s)
      2. animação a 10 quadros/s, 10 min            (intervalo 1 s, como em DEC-011 M1)
      3. animação a 60 quadros/s, 10 min, DispatcherTimer
      4. animação a 60 quadros/s, 10 min, pelo compositor do WPF

    O item 4 existe porque a medição curta mostrou DispatcherTimer entregando cerca de
    39 quadros por segundo quando se pediam 60; a causa não foi isolada.
    Medir os dois caminhos separa "60 pedidos" de "60 entregues".

    Duração total aproximada: 92 minutos, contando os aquecimentos descartados.

    Este script apaga os logs e relatórios anteriores de P2 para que a evidência final
    venha só desta rodada. Não toca nos resultados de P1 nem de P3.
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

    # Deixa a máquina assentar entre etapas, para a próxima não herdar atividade da anterior.
    Start-Sleep -Seconds 20
}

Anotar "FIM da sequência de P2."
