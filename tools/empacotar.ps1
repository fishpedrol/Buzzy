<#
.SYNOPSIS
    Gera o pacote pessoal do Buzzy (o ZIP portátil ou o .exe único) e o confere rodando de uma pasta temporária
    (DEC-040, item 9; DEC-042; Q-10).

.DESCRIPTION
    Uso pessoal e de testes, sem distribuição (Q-10; DEC-042, item 6): nada é publicado nem enviado. Passos:
      1. confere que nenhum Buzzy está aberto e que não está elevado;
      2. publica src\Buzzy.App em Release, numa pasta temporária:
         -Formato zip (padrão): dependente do framework (exige o .NET 10 Desktop Runtime);
         -Formato exe: o .exe único autocontido para Windows x64 (-p:BuzzyExeUnico=true; F9-P10), sem exigir runtime;
         -Edicao publica: a edição pública do download, sem as drogas ilícitas (-p:BuzzyEdicao=publica; DEC-044, item 2);
         -Edicao completa (padrão): a de sempre, com os treze itens, para a página separada do site;
      3. o portão de APIs: no zip, roda na pasta publicada; no exe, roda dentro do publish duas vezes (a pasta
         autocontida, com a procedência do runtime, e o pacote por dentro, com o host contra o singlefilehost.exe do SDK),
         e o script exige os dois APROVADO;
      4. recusa qualquer arquivo fora da lista esperada (os .pdb ficam fora);
      5. gera resultados\Buzzy-<versão>[-completo].zip ou resultados\Buzzy-<versão>[-completo]-win-x64.exe (o sufixo só na
         edição completa), e o SHA-256 dele;
      6. põe o pacote numa pasta temporária (o ZIP extraído; o .exe copiado, como um download), abre o Buzzy.exe de lá com
         o perfil de teste "pacote" (sem instalar nem elevar), espera a janela, fecha por WM_CLOSE e exige saída limpa
         (código 0); a linha INICIO do log tem de dizer a edição pedida (edicao=publica ou edicao=completa). O .exe vai com o nome do download e roda duas vezes: a primeira faz o runtime extrair as DLLs
         nativas do WPF em %TEMP%\.net\Buzzy-<versão>-win-x64\<id>, a segunda tem de reaproveitá-las sem gravar de novo;
         o script mede o caminho, os arquivos e o tamanho, e apaga só as pastas <id> que esta execução criou;
      7. confere que a pasta temporária não mudou (o Buzzy não grava ao lado do executável) e que os arquivos e o registro
         reais do usuário ficaram iguais, vistos só por fora.
    Abre uma janela do Buzzy por alguns segundos (duas vezes no exe): avise quem usa o computador antes. Códigos de
    saída: 0 tudo certo; 1 alguma conferência falhou; 2 pré-condição não atendida (nada foi aberto).
#>
[CmdletBinding()]
param(
    [ValidateSet('zip', 'exe')]
    [string] $Formato = 'zip',

    [ValidateSet('completa', 'publica')]
    [string] $Edicao = 'completa'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
# Qualquer Buzzy, com qualquer nome de arquivo: Buzzy.exe, o .exe único Buzzy-<versão>-win-x64.exe ou uma cópia renomeada
# (o mutex de instância única cobre esta; revisão adversarial do F9-P10).
$nomesProcesso = @('Buzzy*')
$perfilDeTeste = 'pacote'
$pastaLocal = [Environment]::GetFolderPath('LocalApplicationData')
$pastaDoBuzzyReal = [IO.Path]::Combine($pastaLocal, 'Buzzy')
$logDiag = [IO.Path]::Combine($pastaDoBuzzyReal, 'diagnostico.log')
$esperados = if ($Formato -eq 'exe') { @('Buzzy.exe') } else { @('Buzzy.exe', 'Buzzy.dll', 'Buzzy.Core.dll', 'Buzzy.Visual.dll', 'Buzzy.deps.json', 'Buzzy.runtimeconfig.json') }
# O runtime do .NET extrai as DLLs nativas do .exe único em %TEMP%\.net\<nome do arquivo .exe, sem a extensão>\<id do
# pacote> (DOTNET_BUNDLE_EXTRACT_BASE_DIR, que o Buzzy não define; DEC-042, item 9). Definida no passo 6, com o nome real.
$extracaoDoRuntime = $null
$extracoesAntes = $null


function Abortar([string] $motivo) {
    Write-Host "ABORTADO: $motivo" -ForegroundColor Red
    Write-Host 'Nada foi aberto nem encerrado.' -ForegroundColor Red
    exit 2
}

function Falhar([string] $motivo) {
    Write-Host "FALHA: $motivo" -ForegroundColor Red
    $script:falhas++
}

# As pastas de extração (os ids de pacote) que existem agora em $extracaoDoRuntime; nenhuma no formato zip.
function PastasDeExtracao {
    if ($null -eq $extracaoDoRuntime -or -not [IO.Directory]::Exists($extracaoDoRuntime)) { return @() }
    @(Get-ChildItem -LiteralPath $extracaoDoRuntime -Directory -Force | ForEach-Object { $_.Name })
}

function ProcessosBuzzyAbertos {
    $abertos = @(Get-Process -Name $nomesProcesso -ErrorAction SilentlyContinue | Sort-Object Id -Unique)
    # O mutex de instância única do usuário (InstanciaUnica): só aberto para saber se existe, e fechado na hora.
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex = $null
    if ([Threading.Mutex]::TryOpenExisting(('Local\Buzzy.Instancia.' + $sid), [ref]$mutex)) {
        $mutex.Dispose()
        if ($abertos.Count -eq 0) { $abertos = @('instância do Buzzy (mutex) com outro nome de processo') }
    }
    $abertos
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
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Abortar 'rode sem elevação: o pacote é conferido como o usuário comum o usaria.' }

$temporaria = Join-Path ([IO.Path]::GetTempPath()) ('buzzy-pacote-' + [Guid]::NewGuid().ToString('N'))
$publicado = Join-Path $temporaria 'publicado'
$extraido = Join-Path $temporaria 'extraido'
New-Item -ItemType Directory -Path $publicado, $extraido | Out-Null

try {
    # ---- 2 e 3. Publicar e o portão ------------------------------------------------------------------------------------
    $portao = Join-Path $raiz 'tools\Buzzy.PortaoApis\bin\Release\net10.0\Buzzy.PortaoApis.dll'
    # No PowerShell 5.1, o stderr de um executável nativo com 2>&1 e 'Stop' vira erro terminal: só aqui, 'Continue'.
    if ($Formato -eq 'exe') {
        Write-Host '== publicar (Release, .exe único autocontido, win-x64; o portão roda dentro do publish) =='
        $ErrorActionPreference = 'Continue'
        $saidaPublish = & dotnet publish (Join-Path $raiz 'src\Buzzy.App\Buzzy.App.csproj') -c Release -o $publicado -p:BuzzyExeUnico=true "-p:BuzzyEdicao=$Edicao" -nologo -v n 2>&1 | Out-String
        $codigoDoPublish = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        $resumos = @($saidaPublish -split "`r?`n" | Where-Object { $_ -match 'Resumo: ' } | ForEach-Object { $_.Trim() })
        $doPacote = @($saidaPublish -split "`r?`n" | Where-Object { $_ -match 'pacote \d+\.\d+ com \d+ arquivo' } | ForEach-Object { $_.Trim() })
        $resumos + $doPacote | ForEach-Object { Write-Host ('   ' + $_) }
        if ($codigoDoPublish -ne 0) { Write-Host $saidaPublish; Abortar 'dotnet publish falhou (ou o portão reprovou).' }
        if (@($resumos | Where-Object { $_ -match 'APROVADO' }).Count -ne 2 -or @($resumos | Where-Object { $_ -notmatch 'APROVADO' }).Count -ne 0 -or $doPacote.Count -ne 1) {
            Write-Host $saidaPublish; Falhar 'o publish não mostrou os dois portões APROVADO (a pasta autocontida e o pacote).'
        }
    } else {
        Write-Host '== publicar (Release, dependente do framework, win-x64) =='
        & dotnet publish (Join-Path $raiz 'src\Buzzy.App\Buzzy.App.csproj') -c Release -o $publicado --self-contained false "-p:BuzzyEdicao=$Edicao" -nologo -v q
        if ($LASTEXITCODE -ne 0) { Abortar 'dotnet publish falhou.' }

        Write-Host '== portão de APIs na pasta publicada =='
        $ErrorActionPreference = 'Continue'
        $saidaPortao = & dotnet $portao --binarios $publicado --fonte (Join-Path $raiz 'src\Buzzy.App') --fonte (Join-Path $raiz 'src\Buzzy.Core') `
            --fonte (Join-Path $raiz 'src\Buzzy.Visual') --manifesto (Join-Path $raiz 'src\Buzzy.App\app.manifest') 2>&1 | Out-String
        $codigoDoPortao = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        $resumo = ($saidaPortao -split "`r?`n" | Where-Object { $_ -match 'Resumo:' }) -join ' '
        Write-Host $resumo
        if ($codigoDoPortao -ne 0 -or $resumo -notmatch 'APROVADO') { Write-Host $saidaPortao; Falhar 'o portão não aprovou a pasta publicada.' }
    }

    # ---- 4. Só os arquivos esperados -----------------------------------------------------------------------------------
    $presentes = @(Get-ChildItem -LiteralPath $publicado -Recurse -File | ForEach-Object { $_.FullName.Substring($publicado.Length + 1) })
    $inesperados = @($presentes | Where-Object { $esperados -notcontains $_ -and $_ -notmatch '\A[^\\]+\.pdb\z' })
    $faltando = @($esperados | Where-Object { $presentes -notcontains $_ })
    if ($inesperados.Count -gt 0) { Falhar ('arquivos fora da lista esperada: {0}' -f ($inesperados -join ', ')) }
    if ($faltando.Count -gt 0) { Falhar ('faltam arquivos: {0}' -f ($faltando -join ', ')) }
    if ($script:falhas -gt 0) { exit 1 }

    # ---- 5. O pacote e o SHA-256 ---------------------------------------------------------------------------------------
    $versao = (Get-Item (Join-Path $publicado 'Buzzy.exe')).VersionInfo.ProductVersion -replace '\+.*$', ''
    # A pública leva o nome de sempre; a completa, o sufixo (DEC-044, item 2). Os dois começam com "Buzzy", como pede a regra
    # do início com o Windows (DEC-038, item 10), e extraem o runtime em pastas separadas.
    $sufixo = if ($Edicao -eq 'completa') { '-completo' } else { '' }
    $resultados = Join-Path $raiz 'resultados'
    New-Item -ItemType Directory -Force -Path $resultados | Out-Null
    if ($Formato -eq 'exe') {
        $pacote = Join-Path $resultados ('Buzzy-{0}{1}-win-x64.exe' -f $versao, $sufixo)
        Copy-Item -LiteralPath (Join-Path $publicado 'Buzzy.exe') -Destination $pacote -Force
    } else {
        $pacote = Join-Path $resultados ('Buzzy-{0}{1}.zip' -f $versao, $sufixo)
        if (Test-Path -LiteralPath $pacote) { Remove-Item -LiteralPath $pacote -Force }
        Compress-Archive -Path ($esperados | ForEach-Object { Join-Path $publicado $_ }) -DestinationPath $pacote
    }
    $hash = (Get-FileHash -LiteralPath $pacote -Algorithm SHA256).Hash
    Set-Content -LiteralPath ($pacote + '.sha256') -Value ('{0}  {1}' -f $hash, (Split-Path -Leaf $pacote)) -Encoding ascii
    Write-Host ('== {0}: {1:N0} bytes, SHA-256 {2} ==' -f (Split-Path -Leaf $pacote), (Get-Item $pacote).Length, $hash)
    if ($Formato -eq 'exe') {
        # DEC-044, item 4: a licença e os avisos de terceiros da Microsoft acompanham cada release do .exe.
        if ($saidaPublish -notmatch 'Microsoft\.NETCore\.App\.Host\.win-x64\\(\d+\.\d+\.\d+)\\') { Falhar 'a versão do runtime não apareceu no publish (avisos de terceiros).' }
        else {
            & (Join-Path $PSScriptRoot 'avisos-de-terceiros.ps1') -VersaoDoRuntime $Matches[1] -Destino (Join-Path $resultados 'THIRD-PARTY-NOTICES.txt')
            Copy-Item -LiteralPath (Join-Path $raiz 'LICENSE') -Destination (Join-Path $resultados 'LICENSE.txt') -Force
            Write-Host '   LICENSE.txt copiado'
        }
    }

    # ---- 6. Rodar da pasta temporária ----------------------------------------------------------------------------------
    # O .exe vai com o nome do download, porque o runtime extrai numa pasta com o nome do arquivo (revisão adversarial).
    $nomeDoExe = if ($Formato -eq 'exe') { Split-Path -Leaf $pacote } else { 'Buzzy.exe' }
    if ($Formato -eq 'exe') { Copy-Item -LiteralPath $pacote -Destination (Join-Path $extraido $nomeDoExe) }
    else { Expand-Archive -LiteralPath $pacote -DestinationPath $extraido }
    if ($Formato -eq 'exe') { $extracaoDoRuntime = [IO.Path]::Combine([IO.Path]::GetTempPath(), '.net', [IO.Path]::GetFileNameWithoutExtension($nomeDoExe)) }
    $hashAntes = HashDaPasta $extraido
    $extracoesAntes = @(PastasDeExtracao)
    $fotoAntes = FotoDosArquivosReais
    if (@(ProcessosBuzzyAbertos).Count -gt 0) { Abortar 'um Buzzy abriu durante o empacotamento.' }
    Write-Host ('   ' + (LimparPerfilDeTeste $pastaLocal $perfilDeTeste))
    if (@(ProcessosBuzzyAbertos).Count -gt 0) { Abortar 'um Buzzy abriu durante a limpeza do perfil.' }
    $rodadas = if ($Formato -eq 'exe') { 2 } else { 1 }
    $extracaoNova = $null
    $fotoDaExtracao = $null
    foreach ($rodada in 1..$rodadas) {
        if (@(ProcessosBuzzyAbertos).Count -gt 0) {
            # Depois da primeira rodada, algo já foi aberto: não é "nada foi aberto" (código 2), é uma falha.
            if ($rodada -eq 1) { Abortar 'um Buzzy abriu antes da rodada 1.' }
            Falhar ('um Buzzy abriu antes da rodada {0}.' -f $rodada)
            break
        }
        $marca = 0L
        if ([IO.File]::Exists($logDiag)) { $marca = (Get-Item -LiteralPath $logDiag).Length }
        $info = New-Object Diagnostics.ProcessStartInfo (Join-Path $extraido $nomeDoExe)
        $info.Arguments = '--diagnostico --perfil-de-teste ' + $perfilDeTeste + ' --pausado --sem-tela-cheia'
        $info.UseShellExecute = $false
        $info.WorkingDirectory = $extraido
        Write-Host ('== abrir o Buzzy da pasta temporária (perfil de teste "{0}"; rodada {1} de {2}) ==' -f $perfilDeTeste, $rodada, $rodadas)
        $proc = [Diagnostics.Process]::Start($info)
        $hwnd = [IntPtr]::Zero
        $limite = [DateTime]::Now.AddSeconds(30)
        while ([DateTime]::Now -lt $limite -and $hwnd -eq [IntPtr]::Zero -and -not $proc.HasExited) {
            Start-Sleep -Milliseconds 250
            $linhas = LerLogDesde $marca
            $inicio = $linhas | Where-Object { $_ -match ('BUZZY\|INICIO\|pid={0}\|' -f $proc.Id) } | Select-Object -First 1
            $janela = $linhas | Where-Object { $_ -match 'BUZZY\|JANELA\|.*hwnd=(\d+)' } | Select-Object -First 1
            if ($inicio -and $inicio -notmatch ('\|edicao={0}(\||$)' -f $Edicao)) {
                Falhar ('a partida não é da edição {0}: {1}' -f $Edicao, $inicio)
                break
            }
            if ($inicio -and $janela -and ($janela -match 'hwnd=(\d+)')) {
                $candidato = [IntPtr][int64]$Matches[1]
                $pid2 = [uint32]0
                [void][BuzzyEmpacotar.Nativo]::GetWindowThreadProcessId($candidato, [ref]$pid2)
                if ($pid2 -eq $proc.Id) { $hwnd = $candidato }
            }
        }
        if ($hwnd -eq [IntPtr]::Zero) {
            Falhar ('a janela do Buzzy não apareceu no log em 30 s (rodada {0}).' -f $rodada)
            if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit(5000) | Out-Null }
            break
        }
        Start-Sleep -Milliseconds 800
        [void][BuzzyEmpacotar.Nativo]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        if (-not $proc.WaitForExit(15000)) { Falhar 'o Buzzy não saiu em 15 s depois do WM_CLOSE.'; $proc.Kill(); $proc.WaitForExit(5000) | Out-Null; break }
        elseif ($proc.ExitCode -ne 0) { Falhar ('o Buzzy saiu com o código {0} (rodada {1}).' -f $proc.ExitCode, $rodada); break }
        Write-Host ('   abriu da pasta temporária (pid {0}, sem instalar nem elevar) e saiu limpo (código 0)' -f $proc.Id)

        if ($Formato -eq 'exe') {
            # A extração do runtime: uma pasta nova na primeira rodada; a mesma, sem gravar nada, na segunda.
            $novas = @(PastasDeExtracao | Where-Object { $extracoesAntes -notcontains $_ })
            if ($rodada -eq 1) {
                if ($novas.Count -ne 1) {
                    # Sem pasta nova, a extração não foi medida (sobra de uma execução anterior, ou outro lugar): falha.
                    Falhar ('extração do runtime: {0} pasta(s) nova(s) em {1}, esperada 1 (antes já havia: {2}); apague a sobra e rode de novo.' -f $novas.Count, $extracaoDoRuntime, ($extracoesAntes -join ', '))
                    break
                } else {
                    $extracaoNova = Join-Path $extracaoDoRuntime $novas[0]
                    $arquivos = @(Get-ChildItem -LiteralPath $extracaoNova -Recurse -File -Force)
                    $fotoDaExtracao = ($arquivos | Sort-Object FullName | ForEach-Object { '{0}|{1}|{2}' -f $_.FullName, $_.Length, $_.LastWriteTimeUtc.Ticks }) -join ';'
                    Write-Host ('   extração do runtime: {0} — {1} arquivo(s), {2:N0} bytes: {3}' -f $extracaoNova, $arquivos.Count, ($arquivos | Measure-Object Length -Sum).Sum, (($arquivos | ForEach-Object { $_.Name }) -join ', '))
                }
            } elseif ($null -ne $extracaoNova) {
                $arquivos = @(Get-ChildItem -LiteralPath $extracaoNova -Recurse -File -Force)
                $foto = ($arquivos | Sort-Object FullName | ForEach-Object { '{0}|{1}|{2}' -f $_.FullName, $_.Length, $_.LastWriteTimeUtc.Ticks }) -join ';'
                if ($novas.Count -ne 1 -or $foto -ne $fotoDaExtracao) { Falhar 'a segunda rodada não reaproveitou a extração da primeira (gravou de novo ou em outro lugar).' }
                else { Write-Host '   segunda rodada: reaproveitou a extração, sem gravar nada nela' }
            }
        }
    }

    # ---- 7. Nada gravado ao lado do executável nem nos arquivos reais --------------------------------------------------
    if ((HashDaPasta $extraido) -ne $hashAntes) { Falhar 'a pasta temporária mudou: o Buzzy gravou ao lado do executável.' }
    else { Write-Host '   a pasta temporária ficou igual (nada gravado ao lado do executável)' }
    $fotoDepois = FotoDosArquivosReais
    if ($fotoDepois -ne $fotoAntes) {
        Write-Host ('FALHA: os arquivos reais do usuário mudaram, vistos só por fora. Antes: {0}. Depois: {1}.' -f $fotoAntes, $fotoDepois) -ForegroundColor Red
        exit 1
    }
    Write-Host '   arquivos e registro reais do usuário intocados, vistos só por fora'
} finally {
    if (Test-Path -LiteralPath $temporaria) { Remove-Item -LiteralPath $temporaria -Recurse -Force -ErrorAction SilentlyContinue }
    # Só as pastas de extração que esta execução criou (também quando uma rodada falhou), direto em
    # %TEMP%\.net\<nome do exe> e sem junção nem link no caminho.
    if ($null -ne $extracaoDoRuntime -and $null -ne $extracoesAntes) {
        foreach ($nova in @(PastasDeExtracao | Where-Object { $extracoesAntes -notcontains $_ })) {
            $extracaoNova = Join-Path $extracaoDoRuntime $nova
            $semLink = @($extracaoDoRuntime, $extracaoNova) | Where-Object { (([IO.File]::GetAttributes($_)) -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
            if (@($semLink).Count -eq 0 -and (Split-Path -Parent $extracaoNova) -eq $extracaoDoRuntime) {
                Remove-Item -LiteralPath $extracaoNova -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host ('   pasta de extração desta execução apagada: {0}' -f $extracaoNova)
            }
        }
    }
}

if ($script:falhas -gt 0) { exit 1 }
Write-Host ('Pacote {0} gerado e conferido.' -f $Formato) -ForegroundColor Green
exit 0
