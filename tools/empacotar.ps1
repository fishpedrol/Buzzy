<#
.SYNOPSIS
    Gera o ZIP portátil pessoal do Buzzy e o confere rodando da pasta extraída (DEC-040, item 9; Q-10).

.DESCRIPTION
    Uso pessoal e de testes, sem distribuição (Q-10): nada é publicado nem enviado. Passos:
      1. confere que nenhum Buzzy está aberto;
      2. publica src\Buzzy.App em Release, dependente do framework (exige o .NET 10 Desktop Runtime), numa pasta temporária;
      3. roda o portão de APIs na pasta publicada, com as três fontes e o manifesto, e exige APROVADO;
      4. recusa qualquer arquivo fora da lista esperada (o Buzzy e as duas bibliotecas dele, o deps e o runtimeconfig;
         os .pdb ficam fora do ZIP);
      5. gera resultados\Buzzy-<versão>.zip e o SHA-256 dele;
      6. extrai o ZIP numa pasta temporária, abre o Buzzy.exe de lá com o perfil de teste "zip" (sem instalar nem elevar),
         espera a janela, fecha por WM_CLOSE e exige saída limpa (código 0);
      7. confere que a pasta extraída não mudou (o Buzzy não grava ao lado do executável) e que os arquivos e o registro
         reais do usuário ficaram iguais, vistos só por fora.
    Abre uma janela do Buzzy por alguns segundos: avise quem usa o computador antes. Códigos de saída: 0 tudo certo;
    1 alguma conferência falhou; 2 pré-condição não atendida (nada foi aberto).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$nomesProcesso = @('Buzzy')
$perfilDeTeste = 'zip'
$pastaLocal = [Environment]::GetFolderPath('LocalApplicationData')
$pastaDoBuzzyReal = [IO.Path]::Combine($pastaLocal, 'Buzzy')
$logDiag = [IO.Path]::Combine($pastaDoBuzzyReal, 'diagnostico.log')
$esperados = @('Buzzy.exe', 'Buzzy.dll', 'Buzzy.Core.dll', 'Buzzy.Visual.dll', 'Buzzy.deps.json', 'Buzzy.runtimeconfig.json')

function Abortar([string] $motivo) {
    Write-Host "ABORTADO: $motivo" -ForegroundColor Red
    Write-Host 'Nada foi aberto nem encerrado.' -ForegroundColor Red
    exit 2
}

function Falhar([string] $motivo) {
    Write-Host "FALHA: $motivo" -ForegroundColor Red
    $script:falhas++
}

function ProcessosBuzzyAbertos {
    @(Get-Process -Name $nomesProcesso -ErrorAction SilentlyContinue | Sort-Object Id -Unique)
}

# Os arquivos reais de configuração e o registro do início com o Windows, só por fora (metadados e existência), como a
# medição (tools\medir-desempenho.ps1) e a integração (ArquivosReais.cs): nada é lido por dentro nem gravado.
function FotoDosArquivosReais {
    $arquivos = (@('settings.json', 'settings.json.bak', 'settings.json.tmp', 'settings.corrupt.json') | ForEach-Object {
        $info = New-Object IO.FileInfo ([IO.Path]::Combine($pastaDoBuzzyReal, $_))
        if ($info.Exists) { '{0}: {1} bytes, criado {2}, escrito {3}' -f $_, $info.Length, $info.CreationTimeUtc.ToString('o'), $info.LastWriteTimeUtc.ToString('o') }
        else { '{0}: ausente' -f $_ }
    }) -join '; '
    $registro = (@('Software\Microsoft\Windows\CurrentVersion\Run', 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run') | ForEach-Object {
        $chave = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($_)
        if ($null -eq $chave) { '{0}: chave ausente' -f $_ }
        else {
            try { if ($chave.GetValueNames() -contains 'Buzzy') { '{0}\Buzzy: {1}' -f $_, $chave.GetValueKind('Buzzy') } else { '{0}\Buzzy: ausente' -f $_ } }
            finally { $chave.Dispose() }
        }
    }) -join '; '
    '{0}; registro: {1}' -f $arquivos, $registro
}

# A limpeza dos testes (PerfilDeTeste.Limpar): só a pasta testes\<perfil>, sem seguir junção nem link no caminho.
function LimparPerfilDeTeste([string] $pastaLocal, [string] $perfil) {
    if ($perfil -cnotmatch '\A[a-z0-9][a-z0-9-]{0,31}\z') { Abortar ('nome de perfil de teste inválido: {0}' -f $perfil) }
    if ([string]::IsNullOrEmpty($pastaLocal) -or $pastaLocal -notmatch '\A([A-Za-z]:\\|\\\\)') { return 'sem a pasta local do usuário; nada a apagar' }
    $buzzy = [IO.Path]::Combine($pastaLocal, 'Buzzy')
    $testes = [IO.Path]::Combine($buzzy, 'testes')
    $pasta = [IO.Path]::Combine($testes, $perfil)
    foreach ($trecho in @($buzzy, $testes, $pasta)) {
        try { $atributos = [IO.File]::GetAttributes($trecho) } catch [IO.FileNotFoundException], [IO.DirectoryNotFoundException] { continue }
        if (($atributos -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Abortar ('{0} é uma junção ou um link: nada é apagado fora da pasta do Buzzy.' -f $trecho)
        }
    }
    if (-not [IO.Directory]::Exists($pasta)) { return 'sem pasta de uma execução anterior' }
    try { [IO.Directory]::Delete($pasta, $true) } catch { Abortar ('não foi possível apagar a pasta do perfil de teste {0}: {1}' -f $pasta, $_.Exception.Message) }
    'pasta da execução anterior apagada'
}

# O hash de cada arquivo de uma pasta, em ordem: a pasta extraída antes e depois de rodar o Buzzy.
function HashDaPasta([string] $pasta) {
    (Get-ChildItem -LiteralPath $pasta -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        '{0}={1}' -f $_.FullName.Substring($pasta.Length), (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join ';'
}

function LerLogDesde([int64] $desde) {
    if (-not [IO.File]::Exists($logDiag)) { return @() }
    $fluxo = [IO.File]::Open($logDiag, 'Open', 'Read', 'ReadWrite, Delete')
    try {
        if ($fluxo.Length -lt $desde) { $desde = 0 }
        [void] $fluxo.Seek($desde, 'Begin')
        $leitor = New-Object IO.StreamReader($fluxo, [Text.Encoding]::UTF8)
        @($leitor.ReadToEnd() -split "`r?`n" | Where-Object { $_ -match 'BUZZY\|' })
    } finally { $fluxo.Dispose() }
}

Add-Type -Namespace BuzzyEmpacotar -Name Nativo -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
'@

$script:falhas = 0

# ---- 1. Pré-condições ------------------------------------------------------------------------------------------------
if (@(ProcessosBuzzyAbertos).Count -gt 0) { Abortar 'há um Buzzy aberto; feche-o antes (o empacotamento não mexe em processos que não abriu).' }
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Abortar 'rode sem elevação: o ZIP é conferido como o usuário comum o usaria.' }

$temporaria = Join-Path ([IO.Path]::GetTempPath()) ('buzzy-zip-' + [Guid]::NewGuid().ToString('N'))
$publicado = Join-Path $temporaria 'publicado'
$extraido = Join-Path $temporaria 'extraido'
New-Item -ItemType Directory -Path $publicado, $extraido | Out-Null

try {
    # ---- 2. Publicar ---------------------------------------------------------------------------------------------------
    Write-Host '== publicar (Release, dependente do framework, win-x64) =='
    & dotnet publish (Join-Path $raiz 'src\Buzzy.App\Buzzy.App.csproj') -c Release -o $publicado --self-contained false -nologo -v q
    if ($LASTEXITCODE -ne 0) { Abortar 'dotnet publish falhou.' }

    # ---- 3. Portão na pasta publicada ----------------------------------------------------------------------------------
    Write-Host '== portão de APIs na pasta publicada =='
    $portao = Join-Path $raiz 'tools\Buzzy.PortaoApis\bin\Release\net10.0\Buzzy.PortaoApis.dll'
    $saidaPortao = & dotnet $portao --binarios $publicado --fonte (Join-Path $raiz 'src\Buzzy.App') --fonte (Join-Path $raiz 'src\Buzzy.Core') `
        --fonte (Join-Path $raiz 'src\Buzzy.Visual') --manifesto (Join-Path $raiz 'src\Buzzy.App\app.manifest') 2>&1 | Out-String
    $resumo = ($saidaPortao -split "`r?`n" | Where-Object { $_ -match 'Resumo:' }) -join ' '
    Write-Host $resumo
    if ($LASTEXITCODE -ne 0 -or $resumo -notmatch 'APROVADO') { Write-Host $saidaPortao; Falhar 'o portão não aprovou a pasta publicada.' }

    # ---- 4. Só os arquivos esperados -----------------------------------------------------------------------------------
    $presentes = @(Get-ChildItem -LiteralPath $publicado -Recurse -File | ForEach-Object { $_.FullName.Substring($publicado.Length + 1) })
    $inesperados = @($presentes | Where-Object { $esperados -notcontains $_ -and $_ -notmatch '\A[^\\]+\.pdb\z' })
    $faltando = @($esperados | Where-Object { $presentes -notcontains $_ })
    if ($inesperados.Count -gt 0) { Falhar ('arquivos fora da lista esperada: {0}' -f ($inesperados -join ', ')) }
    if ($faltando.Count -gt 0) { Falhar ('faltam arquivos: {0}' -f ($faltando -join ', ')) }
    if ($script:falhas -gt 0) { exit 1 }

    # ---- 5. O ZIP e o SHA-256 ------------------------------------------------------------------------------------------
    $versao = (Get-Item (Join-Path $publicado 'Buzzy.dll')).VersionInfo.ProductVersion -replace '\+.*$', ''
    $resultados = Join-Path $raiz 'resultados'
    New-Item -ItemType Directory -Force -Path $resultados | Out-Null
    $zip = Join-Path $resultados ('Buzzy-{0}.zip' -f $versao)
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path ($esperados | ForEach-Object { Join-Path $publicado $_ }) -DestinationPath $zip
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    Set-Content -LiteralPath ($zip + '.sha256') -Value ('{0}  {1}' -f $hash, (Split-Path -Leaf $zip)) -Encoding ascii
    Write-Host ('== {0}: {1:N0} bytes, SHA-256 {2} ==' -f (Split-Path -Leaf $zip), (Get-Item $zip).Length, $hash)

    # ---- 6. Rodar da pasta extraída ------------------------------------------------------------------------------------
    Expand-Archive -LiteralPath $zip -DestinationPath $extraido
    $hashAntes = HashDaPasta $extraido
    $fotoAntes = FotoDosArquivosReais
    if (@(ProcessosBuzzyAbertos).Count -gt 0) { Abortar 'um Buzzy abriu durante o empacotamento.' }
    Write-Host ('   ' + (LimparPerfilDeTeste $pastaLocal $perfilDeTeste))
    if (@(ProcessosBuzzyAbertos).Count -gt 0) { Abortar 'um Buzzy abriu durante a limpeza do perfil.' }
    $marca = 0L
    if ([IO.File]::Exists($logDiag)) { $marca = (Get-Item -LiteralPath $logDiag).Length }
    $info = New-Object Diagnostics.ProcessStartInfo (Join-Path $extraido 'Buzzy.exe')
    $info.Arguments = '--diagnostico --perfil-de-teste ' + $perfilDeTeste + ' --pausado --sem-tela-cheia'
    $info.UseShellExecute = $false
    $info.WorkingDirectory = $extraido
    Write-Host '== abrir o Buzzy da pasta extraída (perfil de teste "zip") =='
    $proc = [Diagnostics.Process]::Start($info)
    $hwnd = [IntPtr]::Zero
    $limite = [DateTime]::Now.AddSeconds(20)
    while ([DateTime]::Now -lt $limite -and $hwnd -eq [IntPtr]::Zero -and -not $proc.HasExited) {
        Start-Sleep -Milliseconds 250
        $linhas = LerLogDesde $marca
        $inicio = $linhas | Where-Object { $_ -match ('BUZZY\|INICIO\|pid={0}\|' -f $proc.Id) } | Select-Object -First 1
        $janela = $linhas | Where-Object { $_ -match 'BUZZY\|JANELA\|.*hwnd=(\d+)' } | Select-Object -First 1
        if ($inicio -and $janela -and ($janela -match 'hwnd=(\d+)')) {
            $candidato = [IntPtr][int64]$Matches[1]
            $pid2 = [uint32]0
            [void][BuzzyEmpacotar.Nativo]::GetWindowThreadProcessId($candidato, [ref]$pid2)
            if ($pid2 -eq $proc.Id) { $hwnd = $candidato }
        }
    }
    if ($hwnd -eq [IntPtr]::Zero) {
        Falhar 'a janela do Buzzy da pasta extraída não apareceu no log em 20 s.'
        if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit(5000) | Out-Null }
    } else {
        Start-Sleep -Milliseconds 800
        [void][BuzzyEmpacotar.Nativo]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        if (-not $proc.WaitForExit(15000)) { Falhar 'o Buzzy não saiu em 15 s depois do WM_CLOSE.'; $proc.Kill(); $proc.WaitForExit(5000) | Out-Null }
        elseif ($proc.ExitCode -ne 0) { Falhar ('o Buzzy saiu com o código {0}.' -f $proc.ExitCode) }
        else { Write-Host ('   abriu da pasta extraída (pid {0}, sem instalar nem elevar) e saiu limpo (código 0)' -f $proc.Id) }
    }

    # ---- 7. Nada gravado ao lado do executável nem nos arquivos reais --------------------------------------------------
    if ((HashDaPasta $extraido) -ne $hashAntes) { Falhar 'a pasta extraída mudou: o Buzzy gravou ao lado do executável.' }
    else { Write-Host '   a pasta extraída ficou igual (nada gravado ao lado do executável)' }
    $fotoDepois = FotoDosArquivosReais
    if ($fotoDepois -ne $fotoAntes) {
        Write-Host ('FALHA: os arquivos reais do usuário mudaram, vistos só por fora. Antes: {0}. Depois: {1}.' -f $fotoAntes, $fotoDepois) -ForegroundColor Red
        exit 1
    }
    Write-Host '   arquivos e registro reais do usuário intocados, vistos só por fora'
} finally {
    if (Test-Path -LiteralPath $temporaria) { Remove-Item -LiteralPath $temporaria -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($script:falhas -gt 0) { exit 1 }
Write-Host 'ZIP portátil gerado e conferido.' -ForegroundColor Green
exit 0
