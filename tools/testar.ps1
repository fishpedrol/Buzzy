<#
    testar.ps1 — build e verificações automáticas do Buzzy.

    Sem -Integracao, NADA abre na tela:
      1. compila a solução (o portão de APIs proibidas roda depois do build do app);
      2. roda as suítes de teste que não abrem janelas;
      3. roda o portão de APIs proibidas de novo, explicitamente, com relatório;
      4. audita pacotes NuGet vulneráveis (o repositório não usa nenhum pacote).

    Com -Integracao, roda também os testes que iniciam o Buzzy.exe na tela. Avise o usuário
    antes: janelas aparecem e somem. A verificação com cliques e teclas sintéticos fica à
    parte e só age na tela com a opção explícita:
      tests\Buzzy.Verificacao\bin\<Configuracao>\net10.0-windows\Buzzy.Verificacao.exe --injetar-input-na-tela

    Cada etapa que falha, inclusive por executável ausente, é registrada e o script segue
    para a próxima; no fim, o código de saída é 1 se alguma etapa falhou.

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
        # Um erro dentro da etapa a reprova, mas não aborta o script.
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

# Roda um executável compilado. Ausente (build falhou, caminho mudou), a etapa falha com
# código 1 em vez de abortar o script.
function Executar([string] $exe, [string[]] $argumentos = @()) {
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        Write-Host "Executável não encontrado: $exe" -ForegroundColor Red
        $global:LASTEXITCODE = 1
        return
    }
    # O Windows PowerShell 5.1 transforma linhas do stderr de programas nativos em erros quando a
    # saída é redirecionada; aqui só o código de saída decide.
    $ErrorActionPreference = 'Continue'
    & $exe @argumentos
}

$binApp = Join-Path $raiz "src\Buzzy.App\bin\$Configuracao\net10.0-windows"
$argsIntegracao = @()
if ($Integracao) { $argsIntegracao = @('--integracao') }

# O dotnet procura o global.json (SDK fixado) a partir da pasta atual: roda da raiz do
# repositório e devolve a pasta de quem chamou no fim.
Push-Location $raiz
try {
    Etapa 'build da solução (inclui o portão de APIs no build do app)' {
        dotnet build (Join-Path $raiz 'Buzzy.slnx') -c $Configuracao -nologo
    }

    Etapa 'testes do núcleo (Buzzy.Core.Testes)' {
        Executar (Join-Path $raiz "tests\Buzzy.Core.Testes\bin\$Configuracao\net10.0\Buzzy.Core.Testes.exe")
    }

    # O projeto compila para net10.0-windows (usa WPF e Windows Forms nas amostras).
    Etapa 'testes do portão de APIs (Buzzy.PortaoApis.Testes)' {
        Executar (Join-Path $raiz "tests\Buzzy.PortaoApis.Testes\bin\$Configuracao\net10.0-windows\Buzzy.PortaoApis.Testes.exe")
    }

    Etapa ('testes do aplicativo (Buzzy.App.Testes)' + $(if ($Integracao) { ', com integração na tela' } else { ', sem janelas' })) {
        Executar (Join-Path $raiz "tests\Buzzy.App.Testes\bin\$Configuracao\net10.0-windows\Buzzy.App.Testes.exe") $argsIntegracao
    }

    # As mesmas fontes que o portão do build (Buzzy.App.csproj) confere: aplicativo, núcleo e pixel art.
    Etapa 'portão de APIs proibidas (relatório)' {
        Executar (Join-Path $raiz "tools\Buzzy.PortaoApis\bin\$Configuracao\net10.0\Buzzy.PortaoApis.exe") @(
            '--binarios', $binApp,
            '--fonte', (Join-Path $raiz 'src\Buzzy.App'),
            '--fonte', (Join-Path $raiz 'src\Buzzy.Core'),
            '--fonte', (Join-Path $raiz 'src\Buzzy.Visual'),
            '--manifesto', (Join-Path $raiz 'src\Buzzy.App\app.manifest'))
    }

    Etapa 'auditoria de pacotes vulneráveis' {
        # stderr descartado (ver Executar); só a saída JSON e o código de saída importam.
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
        # Propriedade ausente no JSON vira $null, e @($null).Count é 1: os nulos ficam de fora.
        $pacotes = 0
        foreach ($p in @($dados.projects)) { foreach ($f in @($p.frameworks)) { $pacotes += @(@($f.topLevelPackages) + @($f.transitivePackages) | Where-Object { $_ }).Count } }
        Write-Host "Projetos: $(@($dados.projects).Count); pacotes NuGet referenciados: $pacotes; vulneráveis: $($vulneraveis.Count)"
        # DEC-040, item 8: o runtime é a dependência real do Buzzy; fica registrado a cada bateria, para conferir contra a
        # página de suporte do .NET no gate de cada fase. Com pacotes e sem rede, a consulta não aconteceu: aviso.
        Write-Host "SDK: $(dotnet --version); runtimes de desktop: $((dotnet --list-runtimes | Select-String 'WindowsDesktop') -join '; ')"
        if ($pacotes -gt 0 -and -not (Test-Connection -TargetName api.nuget.org -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
            Write-Host "AVISO: há pacotes e o api.nuget.org não respondeu; a auditoria pode não ter consultado nada." -ForegroundColor Yellow
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
