<#
.SYNOPSIS
    Gera o THIRD-PARTY-NOTICES.txt do .exe único do Buzzy (DEC-044, item 4), a partir dos pacotes que o build usou.

.DESCRIPTION
    O .exe autocontido redistribui o runtime do .NET e o WPF. Pela documentação da Microsoft (dotnet/core,
    license-information-windows.md), o host de arquivo único, o CoreCLR, Microsoft.DiaSymReader.Native,
    PresentationNative_cor3, vcruntime140_cor3 e wpfgfx_cor3 ficam sob a .NET Library License; o D3DCompiler_47_cor3, sob a
    Windows SDK License; o resto, sob MIT. Cada distribuição tem de levar a licença e os avisos de terceiros
    (dotnet/runtime, docs/project/licensing-assets.md). Este script junta, sem resumir: a licença e os avisos dos dois
    pacotes de runtime (no cache do NuGet), a licença e os avisos da instalação do .NET (de onde vem o pacote Host) e os
    avisos do WPF (tools\avisos\wpf-THIRD-PARTY-NOTICES.txt, do commit do runtime). Falha se faltar alguma fonte.
    Só lê arquivos e grava o destino; não abre o Buzzy nem acessa a rede.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('\A\d+\.\d+\.\d+\z')] [string] $VersaoDoRuntime,
    [Parameter(Mandatory)] [string] $Destino
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$dotnet = Join-Path $env:ProgramFiles 'dotnet'

$fontes = @(
    @{ Titulo = "Microsoft.NETCore.App.Runtime.win-x64 $VersaoDoRuntime — licença"; Caminho = Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$VersaoDoRuntime\LICENSE.TXT" },
    @{ Titulo = "Microsoft.NETCore.App.Runtime.win-x64 $VersaoDoRuntime — avisos de terceiros"; Caminho = Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$VersaoDoRuntime\THIRD-PARTY-NOTICES.TXT" },
    @{ Titulo = "Microsoft.WindowsDesktop.App.Runtime.win-x64 $VersaoDoRuntime — licença"; Caminho = Join-Path $nuget "microsoft.windowsdesktop.app.runtime.win-x64\$VersaoDoRuntime\LICENSE" },
    @{ Titulo = 'WPF — avisos de terceiros (dotnet/dotnet@95017c7, src/wpf/THIRD-PARTY-NOTICES.TXT)'; Caminho = Join-Path $raiz 'tools\avisos\wpf-THIRD-PARTY-NOTICES.txt' },
    @{ Titulo = 'Instalação do .NET (origem do host de arquivo único, Microsoft.NETCore.App.Host.win-x64) — licença'; Caminho = Join-Path $dotnet 'LICENSE.txt' },
    @{ Titulo = 'Instalação do .NET — avisos de terceiros'; Caminho = Join-Path $dotnet 'ThirdPartyNotices.txt' }
)
$faltando = @($fontes | Where-Object { -not (Test-Path -LiteralPath $_.Caminho -PathType Leaf) } | ForEach-Object { $_.Caminho })
if ($faltando.Count -gt 0) { throw ('fontes dos avisos ausentes: {0}' -f ($faltando -join '; ')) }

$sb = New-Object Text.StringBuilder
$cabecalho = @"
Buzzy — avisos de terceiros
===========================

O executável do Buzzy (Buzzy-<versão>[-completo]-win-x64.exe) é um arquivo único autocontido: inclui o runtime do .NET
$VersaoDoRuntime e o WPF, da Microsoft, para rodar sem nada instalado. O código e a arte do Buzzy estão sob a licença do
arquivo LICENSE (todos os direitos reservados); os componentes da Microsoft continuam sob as licenças deles:

  - .NET Library License (https://dotnet.microsoft.com/dotnet_library_license.htm): o host de arquivo único, o CoreCLR e
    os runtimes incluídos num binário publicado como arquivo único, Microsoft.DiaSymReader.Native, PresentationNative_cor3,
    vcruntime140_cor3 e wpfgfx_cor3;
  - Windows SDK License (https://learn.microsoft.com/legal/windows-sdk/license): D3DCompiler_47_cor3;
  - MIT: o resto do .NET e do WPF.

Fonte da classificação: https://github.com/dotnet/core/blob/main/license-information-windows.md e
https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md. Abaixo, sem alteração, as licenças e os
avisos de terceiros que acompanham esses componentes.

"@
[void]$sb.Append($cabecalho)
foreach ($f in $fontes) {
    $linha = '=' * 118
    [void]$sb.AppendLine($linha).AppendLine($f.Titulo).AppendLine($linha).AppendLine()
    [void]$sb.AppendLine(([IO.File]::ReadAllText($f.Caminho)).TrimEnd()).AppendLine().AppendLine()
}
[IO.File]::WriteAllText($Destino, ($sb.ToString() -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding $false))
Write-Host ('   {0}: {1:N0} bytes, de {2} fontes' -f (Split-Path -Leaf $Destino), (Get-Item -LiteralPath $Destino).Length, $fontes.Count)
