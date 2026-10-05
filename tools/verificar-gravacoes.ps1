<#
.SYNOPSIS
    Analisa um registro do Process Monitor e confere que o Buzzy só grava na pasta de dados e no valor Run\Buzzy
    (SECURITY.md 8, item 4; DEC-040, item 10). [MANUAL]: a captura exige administrador; a análise, não.

.DESCRIPTION
    Como capturar (uma vez, com um UAC):
      1. Baixe o Process Monitor da Sysinternals (site da Microsoft) e abra-o como administrador.
      2. Filtro: "Process Name" "is" "Buzzy.exe" -> Include. Limpe a tela (Ctrl+X) e ligue a captura (Ctrl+E).
      3. Abra o Buzzy de verdade (sem perfil de teste), use-o alguns minutos: arraste, esconda e mostre, abra o
         menu, mude a energia, abra as Configurações, marque e desmarque "Iniciar com o Windows", e saia pelo menu.
      4. Pare a captura (Ctrl+E) e salve: File -> Save -> "Events displayed using current filter", formato CSV.
      5. Rode: powershell -NoProfile -File tools\verificar-gravacoes.ps1 -Csv <arquivo.csv>
    O que conta como gravação: WriteFile, SetRenameInformationFile, SetDispositionInformationFile(Ex),
    SetEndOfFileInformationFile, SetAllocationInformationFile, SetBasicInformationFile, CreateFile que cria ou abre
    para escrita, e RegSetValue, RegDeleteValue, RegCreateKey, RegDeleteKey e RegSetInfoKey com resultado SUCCESS.
    Cada uma cai num grupo:
      - do Buzzy: a pasta de dados (%LOCALAPPDATA%\Buzzy) e o valor HKCU\...\CurrentVersion\Run\Buzzy;
      - do Windows em nome do processo: caches de shader do driver de vídeo, o prefetch, o MuiCache, o estado da
        área de notificação e afins, que qualquer aplicativo de janela provoca e que o código do Buzzy não pede;
      - inesperada: o resto. Com alguma inesperada, sai com o código 1, e o caminho vai para a revisão.
    O CSV fica onde você o salvou; nada é enviado. Códigos: 0 só gravações esperadas; 1 alguma inesperada; 2 uso.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Csv,
    [string] $PastaLocal = [Environment]::GetFolderPath('LocalApplicationData')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Csv)) { Write-Host "Arquivo não encontrado: $Csv" -ForegroundColor Red; exit 2 }

$operacoesDeArquivo = @('WriteFile', 'SetRenameInformationFile', 'SetDispositionInformationFile', 'SetDispositionInformationEx',
    'SetEndOfFileInformationFile', 'SetAllocationInformationFile', 'SetBasicInformationFile')
$operacoesDeRegistro = @('RegSetValue', 'RegDeleteValue', 'RegCreateKey', 'RegDeleteKey', 'RegSetInfoKey')

$pastaDoBuzzy = [IO.Path]::Combine($PastaLocal, 'Buzzy').TrimEnd('\') + '\'
$valorRun = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Buzzy'

# Gravações que o Windows ou o driver fazem em nome de qualquer aplicativo de janela (não vêm do código do Buzzy).
$doWindows = @(
    '\D3DSCache\', '\NVIDIA\DXCache\', '\NVIDIA\GLCache\', '\NVIDIA Corporation\NV_Cache\', '\AMD\DxCache\', '\AMD\DxcCache\',
    '\Intel\ShaderCache\', '\Windows\Prefetch\', '\Microsoft\Windows\Caches\', '\Windows\ServiceProfiles\',
    'HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache',
    'HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify',
    'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\', 'HKCU\Software\Microsoft\Direct3D\',
    'HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\', 'HKCU\Software\Microsoft\CTF\'
)

function Normalizar([string] $caminho) {
    $c = $caminho
    $c = $c -replace '^HKEY_CURRENT_USER', 'HKCU'
    $c = $c -replace '^HKU\\S-1-5-21-[0-9-]+(_Classes)?', { if ($_.Groups[1].Value) { 'HKCU\Software\Classes' } else { 'HKCU' } }
    $c
}

function Grupo([string] $caminho) {
    $c = Normalizar $caminho
    if ($c.StartsWith($pastaDoBuzzy, [StringComparison]::OrdinalIgnoreCase) -or $c.Equals($pastaDoBuzzy.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) { return 'Buzzy' }
    if ($c.Equals($valorRun, [StringComparison]::OrdinalIgnoreCase)) { return 'Buzzy' }
    foreach ($trecho in $doWindows) { if ($c.IndexOf($trecho, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return 'Windows' } }
    'Inesperada'
}

function EhGravacao($linha) {
    if ($linha.Result -ne 'SUCCESS') { return $false }
    if ($operacoesDeArquivo -contains $linha.Operation -or $operacoesDeRegistro -contains $linha.Operation) { return $true }
    if ($linha.Operation -eq 'CreateFile') {
        # Cria, sobrescreve ou abre para escrita (o Detail do Process Monitor diz a disposição e o acesso).
        return ($linha.Detail -match 'Disposition: (Create|OverwriteIf|Overwrite|Supersede|OpenIf)' -and $linha.Detail -match 'Desired Access:[^,]*(Write|Append|Delete)') `
            -or $linha.Detail -match 'Desired Access:[^,]*Generic Write'
    }
    $false
}

$linhas = @(Import-Csv -LiteralPath $Csv)
if ($linhas.Count -gt 0 -and -not ($linhas[0].PSObject.Properties.Name -contains 'Operation' -and $linhas[0].PSObject.Properties.Name -contains 'Path')) {
    Write-Host 'O CSV não tem as colunas Operation e Path do Process Monitor.' -ForegroundColor Red; exit 2
}
$processos = @($linhas | Where-Object { $_.'Process Name' } | ForEach-Object { $_.'Process Name' } | Sort-Object -Unique)
if ($processos.Count -gt 0 -and ($processos | Where-Object { $_ -ne 'Buzzy.exe' })) {
    Write-Host ('AVISO: o CSV tem outros processos ({0}); só as linhas do Buzzy.exe contam.' -f (($processos | Where-Object { $_ -ne 'Buzzy.exe' }) -join ', ')) -ForegroundColor Yellow
}
$gravacoes = @($linhas | Where-Object { (-not $_.'Process Name' -or $_.'Process Name' -eq 'Buzzy.exe') -and (EhGravacao $_) })
$porGrupo = @{ Buzzy = New-Object System.Collections.Generic.List[string]; Windows = New-Object System.Collections.Generic.List[string]; Inesperada = New-Object System.Collections.Generic.List[string] }
foreach ($g in $gravacoes) {
    $descricao = '{0} {1}' -f $g.Operation, $g.Path
    $lista = $porGrupo[(Grupo $g.Path)]
    if (-not $lista.Contains($descricao)) { $lista.Add($descricao) }
}

Write-Host ('Linhas do CSV: {0}; gravações do Buzzy.exe: {1}' -f $linhas.Count, $gravacoes.Count)
foreach ($nome in @('Buzzy', 'Windows', 'Inesperada')) {
    $titulo = @{ Buzzy = 'Do Buzzy (pasta de dados e Run\Buzzy)'; Windows = 'Do Windows em nome do processo'; Inesperada = 'INESPERADAS' }[$nome]
    Write-Host ('{0}: {1}' -f $titulo, $porGrupo[$nome].Count)
    foreach ($d in $porGrupo[$nome]) { Write-Host ('   ' + $d) }
}
if ($porGrupo['Inesperada'].Count -gt 0) {
    Write-Host 'FALHA: há gravações fora da pasta de dados e do Run\Buzzy; revise os caminhos acima.' -ForegroundColor Red
    exit 1
}
Write-Host 'OK: o Buzzy só gravou na pasta de dados e no valor Run\Buzzy.' -ForegroundColor Green
exit 0
