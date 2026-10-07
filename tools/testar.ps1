<#
    Build e testes do Buzzy. Sem -Integracao nada abre na tela: compila, roda as suítes
    sem janela, o portão de APIs proibidas (com relatório) e a auditoria do NuGet.
    -Integracao também roda os testes que abrem o Buzzy.exe (janelas aparecem e somem).
    A verificação que clica e tecla na tela é à parte:
      tests\Buzzy.Verificacao\bin\<Configuracao>\net10.0-windows\Buzzy.Verificacao.exe --injetar-input-na-tela

    Etapa que falha não para o script; sai com 1 se alguma falhou.

    Uso:
      .\tools\testar.ps1 [-Configuracao Release|Debug]
      .\tools\testar.ps1 -Integracao
#>

[CmdletBinding()]
param(
    [switch] $Integracao,
    [ValidateSet('Release', 'Debug')]
    [string] $Configuracao = 'Release'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

$falhas = New-Object System.Collections.Generic.List[string]

function Etapa([string] $nome, [scriptblock] $bloco) {
    Write-Host ""
    Write-Host "== $nome ==" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    try {
        & $bloco
    } catch {
        # Erro reprova a etapa, mas não aborta o script.
        Write-Host "Erro: $($_.Exception.Message)" -ForegroundColor Red
        $global:LASTEXITCODE = 1
    }
    if ($global:LASTEXITCODE -ne 0) {
        $falhas.Add("$nome (código $global:LASTEXITCODE)")
        Write-Host "FALHOU: $nome (código $global:LASTEXITCODE)" -ForegroundColor Red
    } else {
        Write-Host "OK: $nome" -ForegroundColor Green
    }
}

# Exe ausente (build falhou, caminho mudou) reprova a etapa em vez de abortar.
function Executar([string] $exe, [string[]] $argumentos = @()) {
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        Write-Host "Executável não encontrado: $exe" -ForegroundColor Red
        $global:LASTEXITCODE = 1
        return
    }
    # O PS 5.1 transforma stderr de nativo em erro quando a saída é redirecionada;
    # aqui só o código de saída decide.
    $ErrorActionPreference = 'Continue'
    & $exe @argumentos
}

$binApp = Join-Path $raiz "src\Buzzy.App\bin\$Configuracao\net10.0-windows"
$argsIntegracao = @()
if ($Integracao) { $argsIntegracao = @('--integracao') }

# O dotnet acha o global.json (SDK fixado) pela pasta atual, então roda da raiz.
Push-Location $raiz
try {
    Etapa 'build da solução (inclui o portão de APIs no build do app)' {
        dotnet build (Join-Path $raiz 'Buzzy.slnx') -c $Configuracao -nologo
    }

    Etapa 'testes do núcleo (Buzzy.Core.Testes)' {
        Executar (Join-Path $raiz "tests\Buzzy.Core.Testes\bin\$Configuracao\net10.0\Buzzy.Core.Testes.exe")
    }

    # net10.0-windows porque as amostras usam WPF e Windows Forms.
    Etapa 'testes do portão de APIs (Buzzy.PortaoApis.Testes)' {
        Executar (Join-Path $raiz "tests\Buzzy.PortaoApis.Testes\bin\$Configuracao\net10.0-windows\Buzzy.PortaoApis.Testes.exe")
    }

    Etapa ('testes do aplicativo (Buzzy.App.Testes)' + $(if ($Integracao) { ', com integração na tela' } else { ', sem janelas' })) {
        Executar (Join-Path $raiz "tests\Buzzy.App.Testes\bin\$Configuracao\net10.0-windows\Buzzy.App.Testes.exe") $argsIntegracao
    }

    # Mesmas fontes que o portão do build confere: app, núcleo e pixel art.
    Etapa 'portão de APIs proibidas (relatório)' {
        Executar (Join-Path $raiz "tools\Buzzy.PortaoApis\bin\$Configuracao\net10.0\Buzzy.PortaoApis.exe") @(
            '--binarios', $binApp,
            '--fonte', (Join-Path $raiz 'src\Buzzy.App'),
            '--fonte', (Join-Path $raiz 'src\Buzzy.Core'),
            '--fonte', (Join-Path $raiz 'src\Buzzy.Visual'),
            '--manifesto', (Join-Path $raiz 'src\Buzzy.App\app.manifest'))
    }

    Etapa 'auditoria de pacotes vulneráveis' {
        # stderr descartado (ver Executar); só o JSON e o código de saída importam.
        $ErrorActionPreference = 'Continue'
        $json = dotnet list (Join-Path $raiz 'Buzzy.slnx') package --vulnerable --include-transitive --format json 2>$null | Out-String
        if ($LASTEXITCODE -ne 0) { Write-Host "dotnet list package falhou" -ForegroundColor Red; return }
        if ([string]::IsNullOrWhiteSpace($json)) { Write-Host "dotnet list package não devolveu nada" -ForegroundColor Red; $global:LASTEXITCODE = 1; return }
        $dados = $json | ConvertFrom-Json
        $vulneraveis = @()
        foreach ($p in @($dados.projects)) {
            foreach ($f in @($p.frameworks)) {
                foreach ($pacote in @($f.topLevelPackages) + @($f.transitivePackages)) {
                    if ($pacote -and $pacote.vulnerabilities) { $vulneraveis += "$($p.path): $($pacote.id) $($pacote.resolvedVersion)" }
                }
            }
        }
        # Propriedade ausente vira $null e @($null).Count é 1, então filtra os nulos.
        $pacotes = 0
        foreach ($p in @($dados.projects)) { foreach ($f in @($p.frameworks)) { $pacotes += @(@($f.topLevelPackages) + @($f.transitivePackages) | Where-Object { $_ }).Count } }
        Write-Host "Projetos: $(@($dados.projects).Count); pacotes NuGet referenciados: $pacotes; vulneráveis: $($vulneraveis.Count)"
        # O runtime é a dependência real do Buzzy; registra a versão pra conferir com a
        # página de suporte do .NET.
        Write-Host "SDK: $(dotnet --version); runtimes de desktop: $((dotnet --list-runtimes | Select-String 'WindowsDesktop') -join '; ')"
        if ($pacotes -gt 0) {
            # Sem rede a auditoria pode não ter consultado nada; só avisa. Dns funciona no PS 5.1 e no 7.
            $alcanca = $true
            try { [void][System.Net.Dns]::GetHostAddresses('api.nuget.org') } catch { $alcanca = $false }
            if (-not $alcanca) { Write-Host "AVISO: há pacotes e o api.nuget.org não resolve; a auditoria pode não ter consultado nada." -ForegroundColor Yellow }
        }
        if ($vulneraveis.Count -gt 0) { $vulneraveis | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }; $global:LASTEXITCODE = 1 }
    }
} finally {
    Pop-Location
}

Write-Host ""
if ($falhas.Count -eq 0) {
    Write-Host "Tudo verde." -ForegroundColor Green
    exit 0
}
Write-Host "Falhas:" -ForegroundColor Red
$falhas | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
