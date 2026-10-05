<#
    medir-desempenho.ps1 — linha de base de desempenho do Buzzy em repouso (Fase 1).

    Critério 11 da Fase 1 (TODO.md): "parado por 10 min, o consumo de CPU, memória e
    acordadas fica registrado como linha de base". Mede UM processo, identificado por PID
    e nunca por nome, com as métricas de DEC-011:

      M1  CPU do processo        delta de TotalProcessorTime sobre o tempo de parede, em %
                                 de um núcleo e % da máquina; média, p95, máximo e total
      M2  acordadas por segundo  trocas de contexto somadas das threads do processo
      M3  memória                memória privada e working set
      M4  GPU por processo       UtilizationPercentage das engines de GPU do PID
      M6  tempo até 1º quadro    informado pelo próprio app (BUZZY|PRIMEIRO_QUADRO)

    Também registra:
      - a resolução do timer global antes de abrir, durante e depois de fechar o Buzzy.
        Ela vale para o sistema inteiro; comparar os três momentos é o que permite atribuir
        uma mudança ao Buzzy (critério de DEC-011);
      - os processos filhos do Buzzy a cada amostra (Win32_Process com ParentProcessId =
        PID; esperado nenhum, SECURITY.md 3.2);
      - instantâneos das conexões de rede do PID (SECURITY.md 8, item 3; esperado nenhuma).

    Método: o protocolo aceito de P2 (spikes/ferramentas/medir-p2.ps1), com classes CIM,
    cujos nomes não são traduzidos e por isso funcionam em Windows em português, ao
    contrário dos nomes de contador de Get-Counter. Duas diferenças, ambas de fidelidade:
      - M2 lê o contador acumulado de trocas de cada thread (NtQuerySystemInformation). É
        o mesmo dado do "Context Switch Delta" do Process Explorer, citado em DEC-011, e da
        classe CIM de threads usada em P2. A consulta CIM de threads levou de 7 a 9 s nesta
        máquina (medido em 2026-09-29); em P2 ela esticou o "intervalo de 1 s" para cerca de
        6,7 s (88 a 90 amostras em 10 min). A consulta CIM fica como alternativa, usada só
        se a leitura nativa falhar.
      - o filtro de GPU confere o PID exato: em WQL, "_" é curinga, e 'pid_123_%' também
        pegaria as engines do PID 1234.

    A Fase 1 só precisa REGISTRAR a linha de base. As metas de repouso de Q-08 (DEC-011)
    aparecem no fim do relatório como referência informativa, não como veredito.

    Contrato com o aplicativo (Buzzy.exe --diagnostico --perfil-de-teste desempenho):
      - grava %LOCALAPPDATA%\Buzzy\diagnostico.log acrescentando; o script só lê o que for
        escrito depois de abrir o app;
      - com o perfil de teste, os dados do app (posição, preferências) ficam em
        %LOCALAPPDATA%\Buzzy\testes\desempenho, nunca nas configurações reais do usuário; essa
        pasta é apagada antes de abrir, para toda medição partir da posição inicial;
      - linhas "[hh:mm:ss.fff] BUZZY|CHAVE|campo=valor|...": INICIO (pid), JANELA (hwnd em
        decimal), PRIMEIRO_QUADRO (ms desde o início do processo), FIM (codigo);
      - instância única: se já houver processo Buzzy, o script aborta ANTES de abrir, e
        nunca encerra processo que não tenha aberto;
      - encerramento limpo por WM_CLOSE ao HWND de BUZZY|JANELA. A janela é tool window, e
        Process.CloseMainWindow não a alcança.
    Tempos limite: 30 s da abertura até as linhas de partida no log (sem BUZZY|JANELA o
    script desiste e encerra o processo que abriu; sem BUZZY|PRIMEIRO_QUADRO a medição
    segue e só M6 fica sem valor) e 15 s de WM_CLOSE até a saída (depois disso, registra a
    falha e só então encerra à força o processo que ele mesmo abriu).

    ATENÇÃO: o script ABRE o Buzzy na tela e o deixa parado durante toda a medição (cerca
    de Minutos + 1 min). Não interaja com o Buzzy nem passe o mouse sobre ele: isso acorda
    o processo e entra na medição. Enquanto mede, o script pede ao Windows que não
    desligue a tela nem suspenda a máquina (SetThreadExecutionState); o pedido é do
    PowerShell, não do Buzzy, e termina com a medição.

    A partir da Fase 4 o Buzzy se move sozinho. O modo padrão, -Modo repouso, abre o app com
    --pausado (movimento pausado, como pelo menu "Pausar movimento") e mede a linha de base.
    -Modo autonomia deixa a agenda ligada: o personagem anda, escala e pula pela tela durante a
    medição, e o relatório dá o custo médio desse comportamento.

    -Modo onda (tamagotchi, DEC-028; crítica de integração, V13) abre pausado, como o repouso, e
    antes do aquecimento deixa uma vodka em uso: o botão direito no personagem abre o menu, as
    teclas I e V invocam a vodka, e ela é arrastada até ele. Tudo por mensagens POSTADAS às
    janelas do próprio Buzzy aberto aqui (WM_RBUTTONDOWN/UP, WM_CHAR ao dono do menu,
    WM_LBUTTONDOWN, WM_MOUSEMOVE e WM_LBUTTONUP à janela do item), com o PID de cada janela
    conferido antes de cada mensagem, como nos testes de integração: nada passa pela fila de
    input do Windows, o cursor não se move e nenhum outro aplicativo recebe nada. O menu toma o
    primeiro plano por um instante, como sempre (DEC-016). A medição é o repouso pausado com a
    onda de bebedeira em curso: os disparos únicos do temporizador da onda só trocam a cara, e o
    relógio fica desligado. Uma vodka dura cerca de 5 min e 20 s (subida, dois níveis de pico e a
    queda); o relatório mostra a linha do tempo da onda, a CPU com ela e depois dela, e se o
    relógio ligou durante a janela medida.

    Uso, na raiz do repositório, depois de compilar o Release de src/Buzzy.App:
      .\tools\medir-desempenho.ps1
      .\tools\medir-desempenho.ps1 -Minutos 60 -IntervaloSegundos 5
      .\tools\medir-desempenho.ps1 -Modo autonomia -Semente 7
      .\tools\medir-desempenho.ps1 -Modo onda
      .\tools\medir-desempenho.ps1 -Exe C:\caminho\Buzzy.exe -Destino C:\temp\medicao.txt

    Código de saída: 0 medição completa e encerramento limpo; 1 medição incompleta ou
    encerramento com falha (o relatório diz o motivo); 2 abortado antes de abrir o app.
#>

[CmdletBinding()]
param(
    # Duração da janela medida, depois do aquecimento. Q-08 avalia a CPU em 10 min.
    [ValidateRange(1, 1440)]
    [int] $Minutos = 10,

    # Intervalo entre amostras. DEC-011 prescreve 1 s para M1.
    [ValidateRange(1, 3600)]
    [int] $IntervaloSegundos = 1,

    # Descartado das estatísticas, contado a partir do primeiro quadro: a partida é o
    # único trecho em que o app gasta CPU de verdade.
    [ValidateRange(0, 3600)]
    [int] $AquecimentoSegundos = 30,

    # Executável medido. Padrão: o Release de src/Buzzy.App.
    [string] $Exe,

    # Arquivo do relatório. Padrão: resultados\desempenho-AAAAMMDD-HHMMSS.txt na raiz do
    # repositório; a pasta é criada se faltar.
    [string] $Destino,

    # O que medir. 'repouso' (padrão) abre o Buzzy com o movimento pausado (--pausado): é a
    # linha de base de repouso (Fase 1, critério 11), que a Fase 4 não pode piorar.
    # 'autonomia' abre com a agenda autônoma ligada (Fase 4): o personagem anda, escala, pula
    # e descansa sozinho, e a medição dá o custo médio desse comportamento.
    # 'onda' abre pausado e, antes do aquecimento, deixa uma vodka em uso por mensagens postadas
    # às janelas do próprio Buzzy (tamagotchi, DEC-028; crítica, V13): repouso com a onda ativa.
    [ValidateSet('repouso', 'autonomia', 'onda')]
    [string] $Modo = 'repouso',

    # Semente da agenda autônoma (--semente), para repetir a mesma sequência de ações.
    # Negativa: o app usa a própria semente.
    [long] $Semente = -1
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ constantes do protocolo
$limitePartidaSegundos = 30        # abrir -> BUZZY|JANELA e BUZZY|PRIMEIRO_QUADRO no log
$limiteSaidaSegundos = 15          # WM_CLOSE -> processo encerrado
$limiteForcadoSegundos = 5         # encerramento forçado -> processo encerrado
$leituraTimerSegundos = 3          # resolução do timer lida antes de abrir e depois de fechar
$intervaloRedeSegundos = 10        # instantâneos de conexões de rede do PID
$intervaloProgressoSegundos = 60   # linha de progresso no console
$trocasDeUmTimer300ms = 1000.0 / 300.0   # um timer de 300 ms acorda a thread 3,33 vezes/s

# Perfil de teste (Fase 5): os dados do Buzzy medido ficam em %LOCALAPPDATA%\Buzzy\testes\desempenho; a
# medição nunca lê nem grava a posição e as preferências reais do usuário. O log continua na pasta do Buzzy.
# A pasta do perfil é apagada antes de abrir (LimparPerfilDeTeste): toda medição parte da posição inicial.
$perfilDeTeste = 'desempenho'
$argumentosDoBuzzy = '--diagnostico --perfil-de-teste ' + $perfilDeTeste
if ($Modo -eq 'repouso' -or $Modo -eq 'onda') { $argumentosDoBuzzy += ' --pausado' }
if ($Semente -ge 0) { $argumentosDoBuzzy += (' --semente {0}' -f $Semente) }
$tituloDaMedicao = if ($Modo -eq 'repouso') {
    'linha de base de desempenho em repouso, movimento pausado (Fase 1, critério 11)'
} elseif ($Modo -eq 'onda') {
    'repouso pausado com a onda de uma vodka em curso (tamagotchi, DEC-028; crítica, V13)'
} else {
    'desempenho com a agenda autônoma ligada (Fase 4): anda, escala, pula e descansa sozinho'
}

$cultura = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
$invariante = [Globalization.CultureInfo]::InvariantCulture
$nucleos = [Environment]::ProcessorCount
$inicioScript = [DateTime]::Now

# ------------------------------------------------------------------ caminhos
$raiz = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $raiz 'src\Buzzy.App\bin\Release\net10.0-windows\Buzzy.exe' }
if (-not $Destino) { $Destino = Join-Path $raiz ('resultados\desempenho-{0}.txt' -f $inicioScript.ToString('yyyyMMdd-HHmmss')) }
# Caminho relativo vale a partir da pasta atual do PowerShell, não da pasta do processo.
$Exe = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Exe)
$Destino = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destino)
$logDiag = Join-Path (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Buzzy') 'diagnostico.log'
# Ao passar de 1 MB, o Buzzy move o log para esta cópia e recomeça (Diagnostico.cs).
$logDiagCopia = Join-Path (Split-Path -Parent $logDiag) 'diagnostico.1.log'
$nomesProcesso = @('Buzzy', [IO.Path]::GetFileNameWithoutExtension($Exe)) | Select-Object -Unique

# Os arquivos reais de configuração do usuário (os quatro nomes de ArquivoDeConfiguracoes), vistos só por fora: existência,
# tamanho e datas, nunca o conteúdo (SECURITY.md 5). A medição usa o perfil de teste e nunca pode mudá-los: a foto de antes
# de abrir e a do fim são comparadas (revisão de segurança do bloco P6-P9, achado 8; a mesma de ArquivosReais.cs).
$pastaDoBuzzyReal = Split-Path -Parent $logDiag
function FotoDosArquivosReais {
    $arquivos = (@('settings.json', 'settings.json.bak', 'settings.json.tmp', 'settings.corrupt.json') | ForEach-Object {
        $info = New-Object IO.FileInfo ([IO.Path]::Combine($pastaDoBuzzyReal, $_))
        if ($info.Exists) { '{0}: {1} bytes, criado {2}, escrito {3}' -f $_, $info.Length, $info.CreationTimeUtc.ToString('o'), $info.LastWriteTimeUtc.ToString('o') }
        else { '{0}: ausente' -f $_ }
    }) -join '; '
    # O registro real do início com o Windows (DEC-038, item 12), só por fora: se o valor Buzzy existe e de que tipo, nas
    # chaves Run e StartupApproved\Run do usuário. Os dados nunca são lidos; nada é gravado.
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

function Abortar([string] $motivo) {
    Write-Host "ABORTADO: $motivo" -ForegroundColor Red
    Write-Host 'Nada foi aberto nem encerrado.' -ForegroundColor Red
    exit 2
}

function ProcessosBuzzyAbertos {
    @(Get-Process -Name $nomesProcesso -ErrorAction SilentlyContinue | Sort-Object Id -Unique)
}

# Apaga a pasta do perfil de teste (<pastaLocal>\Buzzy\testes\<perfil>, com a pasta local do usuário), se existir,
# com tudo o que houver dentro, como a limpeza dos testes (PerfilDeTeste.Limpar): só com nenhum Buzzy aberto, e
# sem seguir junção nem link simbólico no caminho até ela, para nunca apagar nada fora da pasta do Buzzy. Sem a
# posição gravada pela medição anterior, duas medições com a mesma semente partem do mesmo lugar e continuam
# comparáveis. Devolve o que fez, para o relatório; se não puder apagar, aborta antes de abrir.
function LimparPerfilDeTeste([string] $pastaLocal, [string] $perfil) {
    if ($perfil -cnotmatch '\A[a-z0-9][a-z0-9-]{0,31}\z') { Abortar ('nome de perfil de teste inválido: {0}' -f $perfil) }
    if ([string]::IsNullOrEmpty($pastaLocal) -or $pastaLocal -notmatch '\A([A-Za-z]:\\|\\\\)') { return 'sem a pasta local do usuário; nada a apagar' }
    $buzzy = [IO.Path]::Combine($pastaLocal, 'Buzzy')
    $testes = [IO.Path]::Combine($buzzy, 'testes')
    $pasta = [IO.Path]::Combine($testes, $perfil)
    # Cada trecho que existe é conferido antes de tudo, também sem a pasta do perfil: com a pasta do Buzzy ou a dos testes
    # como junção, o Buzzy medido gravaria do outro lado (revisão de segurança do bloco P6-P9, achado 5).
    foreach ($trecho in @($buzzy, $testes, $pasta)) {
        try { $atributos = [IO.File]::GetAttributes($trecho) } catch [IO.FileNotFoundException], [IO.DirectoryNotFoundException] { continue }
        if (($atributos -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Abortar ('{0} é uma junção ou um link: a medição não apaga nada fora da pasta do Buzzy.' -f $trecho)
        }
    }
    if (-not [IO.Directory]::Exists($pasta)) { return 'sem pasta de uma medição anterior' }
    try {
        [IO.Directory]::Delete($pasta, $true)
    } catch {
        Abortar ('não foi possível apagar a pasta do perfil de teste {0}: {1}' -f $pasta, $_.Exception.Message)
    }
    'pasta da medição anterior apagada'
}

# ------------------------------------------------------------------ verificações antes de abrir
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    Abortar "executável não encontrado em $Exe. Compile o Release de src/Buzzy.App ou passe -Exe."
}
$abertos = @(ProcessosBuzzyAbertos)
if ($abertos.Count -gt 0) {
    Abortar ('já há processo Buzzy aberto (PID {0}). Com instância única, abrir de novo só traria a janela existente, e a medição pegaria o processo errado. Feche o Buzzy pelo menu da bandeja e rode de novo.' -f (($abertos | ForEach-Object { $_.Id }) -join ', '))
}
if (Test-Path -LiteralPath $Destino -PathType Container) { Abortar "-Destino aponta para uma pasta: $Destino" }
try {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destino))
} catch {
    Abortar ('não foi possível criar a pasta do relatório de {0}: {1}' -f $Destino, $_.Exception.Message)
}
$fotoAntes = FotoDosArquivosReais

# ------------------------------------------------------------------ interop
if (-not ('BuzzyFerramentas.MedicaoDesempenho' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BuzzyFerramentas
{
    // Monitor do desktop virtual em pixels físicos (ARCHITECTURE.md 2.4).
    public sealed class MonitorLido
    {
        public string Dispositivo;
        public bool Principal;
        public int X, Y, Largura, Altura;
        public int UtilX, UtilY, UtilLargura, UtilAltura;
        public int Dpi;   // 0 se não foi possível ler
        public int Hz;    // 0 ou 1 se não foi possível ler
    }

    // Trocas de contexto somadas das threads de um processo, pela variação do contador
    // acumulado de cada thread entre duas leituras. Uma thread que termina entre duas
    // leituras perde as trocas daquele intervalo; uma que nasce entra com todas as suas.
    public sealed class ContadorDeTrocas
    {
        private readonly long _pid;
        private Dictionary<string, uint> _anterior;

        public ContadorDeTrocas(int pid) { _pid = pid; }

        // Threads do processo na última leitura.
        public int Threads { get; private set; }

        // Trocas desde a leitura anterior. A primeira leitura só guarda a referência e
        // devolve 0. Devolve -1 se o processo não está na lista do sistema.
        public long Ler()
        {
            Dictionary<string, uint> atual = MedicaoDesempenho.LerTrocasPorThread(_pid);
            if (atual == null) return -1;
            long soma = 0;
            if (_anterior != null)
            {
                foreach (KeyValuePair<string, uint> par in atual)
                {
                    uint antes;
                    soma += _anterior.TryGetValue(par.Key, out antes) ? unchecked(par.Value - antes) : par.Value;
                }
            }
            _anterior = atual;
            Threads = atual.Count;
            return soma;
        }
    }

    public static class MedicaoDesempenho
    {
        // ------------------------------------------------------ resolução do timer global
        [DllImport("ntdll.dll")]
        private static extern int NtQueryTimerResolution(out uint maisGrossa, out uint maisFina, out uint atual);

        // Resolução atual do timer global, em ms, ou NaN se a consulta falhar.
        public static double ResolucaoAtualMs()
        {
            uint grossa, fina, atual;
            if (NtQueryTimerResolution(out grossa, out fina, out atual) != 0) return double.NaN;
            return atual / 10000.0;
        }

        // Faixa da máquina, em ms: { mais fina, mais grossa }, ou null se a consulta falhar.
        public static double[] FaixaDaResolucaoMs()
        {
            uint grossa, fina, atual;
            if (NtQueryTimerResolution(out grossa, out fina, out atual) != 0) return null;
            return new double[] { fina / 10000.0, grossa / 10000.0 };
        }

        // ------------------------------------------------------ janela do Buzzy
        public const int WM_CLOSE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // PID dono da janela, ou 0 se o handle não é de uma janela.
        public static int PidDaJanela(IntPtr hWnd)
        {
            uint pid;
            if (GetWindowThreadProcessId(hWnd, out pid) == 0) return 0;
            return unchecked((int)pid);
        }

        // ------------------------------------------------------ mensagens postadas às janelas do Buzzy (modo onda)
        public const int WM_CHAR = 0x0102;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int MK_LBUTTON = 0x0001;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        // Retângulo da janela em pixels físicos { esquerda, topo, direita, base }, ou null: a thread fica em
        // Per-Monitor V2 só durante a leitura (o PowerShell não tem consciência de DPI e, fora de 100%, leria
        // coordenadas virtualizadas). O contexto anterior da thread é restaurado.
        public static int[] RetanguloFisico(IntPtr hWnd)
        {
            IntPtr anterior = IntPtr.Zero;
            try { anterior = SetThreadDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { }
            try
            {
                RECT r;
                if (!GetWindowRect(hWnd, out r)) return null;
                return new int[] { r.Left, r.Top, r.Right, r.Bottom };
            }
            finally
            {
                if (anterior != IntPtr.Zero) SetThreadDpiAwarenessContext(anterior);
            }
        }

        // Posta uma mensagem de mouse a uma janela, com o ponto de tela (pixels físicos) convertido para coordenadas
        // de cliente pela posição ATUAL dela (as janelas do Buzzy não têm borda: o cliente é a janela inteira). Não é
        // input: nada passa pela fila de input do Windows nem move o cursor.
        public static bool PostarMouse(IntPtr hWnd, int mensagem, int botoes, int x, int y)
        {
            int[] r = RetanguloFisico(hWnd);
            if (r == null) return false;
            int cx = x - r[0], cy = y - r[1];
            return PostMessage(hWnd, mensagem, new IntPtr(botoes), new IntPtr(((cy & 0xFFFF) << 16) | (cx & 0xFFFF)));
        }

        // Posta um caractere (WM_CHAR) a uma janela, como a tecla de acesso de um item do menu ao dono dele.
        public static bool PostarCaractere(IntPtr hWnd, char c)
        {
            return PostMessage(hWnd, WM_CHAR, new IntPtr(c), IntPtr.Zero);
        }

        // ------------------------------------------------------ tela e sistema acordados
        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint estado);

        // Liga ou desliga, nesta thread, o pedido de manter a tela ligada e a máquina acordada.
        public static bool ManterAcordado(bool ligar)
        {
            const uint continuo = 0x80000000, sistema = 0x00000001, tela = 0x00000002;
            return SetThreadExecutionState(ligar ? (continuo | sistema | tela) : continuo) != 0;
        }

        // ------------------------------------------------------ energia
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS estado);

        // { alimentação (0 bateria, 1 tomada, 255 desconhecida), BatteryFlag (128 sem
        //   bateria, 255 desconhecido), carga em %, economia de bateria (1 ligada) }, ou null.
        public static int[] LerEnergia()
        {
            SYSTEM_POWER_STATUS s;
            if (!GetSystemPowerStatus(out s)) return null;
            return new int[] { s.ACLineStatus, s.BatteryFlag, s.BatteryLifePercent, s.SystemStatusFlag };
        }

        // ------------------------------------------------------ monitores
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        // Lê os monitores com a thread em Per-Monitor V2, para obter pixels físicos mesmo com
        // o PowerShell sem consciência de DPI. O contexto anterior da thread é restaurado.
        public static MonitorLido[] LerMonitores()
        {
            var lidos = new List<MonitorLido>();
            IntPtr contextoAnterior = IntPtr.Zero;
            try { contextoAnterior = SetThreadDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { }
            try
            {
                MonitorEnumProc aoEncontrar = delegate (IntPtr hMonitor, IntPtr hdc, IntPtr retangulo, IntPtr dados)
                {
                    var info = new MONITORINFOEX();
                    info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (!GetMonitorInfo(hMonitor, ref info)) return true;

                    var m = new MonitorLido();
                    m.Dispositivo = info.szDevice;
                    m.Principal = (info.dwFlags & 1) != 0;
                    m.X = info.rcMonitor.Left;
                    m.Y = info.rcMonitor.Top;
                    m.Largura = info.rcMonitor.Right - info.rcMonitor.Left;
                    m.Altura = info.rcMonitor.Bottom - info.rcMonitor.Top;
                    m.UtilX = info.rcWork.Left;
                    m.UtilY = info.rcWork.Top;
                    m.UtilLargura = info.rcWork.Right - info.rcWork.Left;
                    m.UtilAltura = info.rcWork.Bottom - info.rcWork.Top;

                    uint dpiX, dpiY;
                    try { if (GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY) == 0) m.Dpi = (int)dpiX; }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }

                    var modo = new DEVMODE();
                    modo.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                    if (EnumDisplaySettings(info.szDevice, -1, ref modo)) m.Hz = (int)modo.dmDisplayFrequency;

                    lidos.Add(m);
                    return true;
                };
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, aoEncontrar, IntPtr.Zero);
                GC.KeepAlive(aoEncontrar);
            }
            finally
            {
                if (contextoAnterior != IntPtr.Zero) SetThreadDpiAwarenessContext(contextoAnterior);
            }
            return lidos.ToArray();
        }

        // ------------------------------------------------------ trocas de contexto (M2)
        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int classe, IntPtr buffer, int tamanho, out int necessario);

        private const int SystemProcessInformation = 5;
        private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

        // Deslocamentos em processo de 64 bits: os de SYSTEM_PROCESS_INFORMATION e
        // SYSTEM_THREAD_INFORMATION, os mesmos que o .NET usa em System.Diagnostics.Process.
        private const int ProcProximo = 0x00, ProcThreads = 0x04, ProcId = 0x50, ProcTamanho = 0x100;
        private const int ThrCriacao = 0x10, ThrId = 0x30, ThrTrocas = 0x40, ThrTamanho = 0x50;

        // Trocas acumuladas de cada thread do processo, com chave "id@criação" (o id de thread
        // pode ser reaproveitado). Null se o processo não aparece na lista do sistema.
        internal static Dictionary<string, uint> LerTrocasPorThread(long pid)
        {
            if (IntPtr.Size != 8)
                throw new PlatformNotSupportedException("a leitura nativa de threads exige PowerShell de 64 bits");

            int tamanho = 1 << 20;
            for (int tentativa = 0; tentativa < 8; tentativa++)
            {
                IntPtr buffer = Marshal.AllocHGlobal(tamanho);
                try
                {
                    int necessario;
                    int status = NtQuerySystemInformation(SystemProcessInformation, buffer, tamanho, out necessario);
                    if (status == StatusInfoLengthMismatch)
                    {
                        tamanho = Math.Max(tamanho * 2, necessario + (1 << 16));
                        continue;
                    }
                    if (status != 0)
                        throw new InvalidOperationException("NtQuerySystemInformation devolveu 0x" + status.ToString("X8", CultureInfo.InvariantCulture));
                    return Extrair(buffer, tamanho, pid);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            throw new InvalidOperationException("NtQuerySystemInformation: a lista de processos não coube no buffer");
        }

        private static Dictionary<string, uint> Extrair(IntPtr buffer, int tamanho, long pid)
        {
            long inicio = buffer.ToInt64();
            long deslocamento = 0;
            while (deslocamento + ProcTamanho <= tamanho)
            {
                IntPtr processo = new IntPtr(inicio + deslocamento);
                int proximo = Marshal.ReadInt32(processo, ProcProximo);
                if (Marshal.ReadIntPtr(processo, ProcId).ToInt64() == pid)
                {
                    int n = Marshal.ReadInt32(processo, ProcThreads);
                    var trocas = new Dictionary<string, uint>(Math.Max(n, 0));
                    for (int i = 0; i < n; i++)
                    {
                        long t = deslocamento + ProcTamanho + (long)i * ThrTamanho;
                        if (t + ThrTamanho > tamanho) break;
                        IntPtr thread = new IntPtr(inicio + t);
                        string chave = Marshal.ReadIntPtr(thread, ThrId).ToInt64().ToString(CultureInfo.InvariantCulture)
                            + "@" + Marshal.ReadInt64(thread, ThrCriacao).ToString(CultureInfo.InvariantCulture);
                        trocas[chave] = unchecked((uint)Marshal.ReadInt32(thread, ThrTrocas));
                    }
                    return trocas;
                }
                if (proximo <= 0) break;
                deslocamento += proximo;
            }
            return null;
        }
    }
}
'@
}
# Uma sessão do PowerShell que já carregou uma versão anterior das ferramentas não carrega a nova (o tipo é o mesmo).
if ($Modo -eq 'onda' -and $null -eq [BuzzyFerramentas.MedicaoDesempenho].GetMethod('PostarMouse')) {
    Abortar 'esta sessão do PowerShell carregou uma versão anterior das ferramentas de medição, sem as mensagens do modo onda. Rode o script num PowerShell novo (powershell -NoProfile -File tools\medir-desempenho.ps1 -Modo onda).'
}

# ------------------------------------------------------------------ estado da medição
$amostrasCpu = New-Object System.Collections.Generic.List[double]          # % de um núcleo
$amostrasCpuMaquina = New-Object System.Collections.Generic.List[double]   # % da máquina
$intervalosReais = New-Object System.Collections.Generic.List[double]      # s entre amostras
$amostrasTrocas = New-Object System.Collections.Generic.List[double]       # trocas/s
$amostrasPriv = New-Object System.Collections.Generic.List[double]         # MB
$amostrasWs = New-Object System.Collections.Generic.List[double]           # MB
$temposMemoria = New-Object System.Collections.Generic.List[double]        # s desde o início da janela
$amostrasGpu = New-Object System.Collections.Generic.List[double]          # % somado das engines
$gpuSomaPorTipo = @{}
$gpuMaxPorTipo = @{}
$resAntes = New-Object System.Collections.Generic.List[double]             # ms
$resPartida = New-Object System.Collections.Generic.List[double]
$resMedicao = New-Object System.Collections.Generic.List[double]
$resDepois = New-Object System.Collections.Generic.List[double]
$eventos = New-Object System.Collections.Generic.List[object]
$linhasLog = New-Object System.Collections.Generic.List[string]
$avisos = New-Object System.Collections.Generic.List[string]
$linhasAmbiente = New-Object System.Collections.Generic.List[string]
$filhosVistos = @{}
$filhosVivosDepois = New-Object System.Collections.Generic.List[string]
$conexoesVistas = New-Object System.Collections.Generic.List[string]
$encerramento = @{
    Tentativas = 0; Hwnd = [IntPtr]::Zero; HwndValido = $false; WmCloseEnviado = $false
    Saiu = $false; SegundosAteSair = $null; Forcado = $false; ErroForcado = $null
    JaTinhaSaido = $false; CodigoSaida = $null; HoraSaida = $null
}
# Modo onda (tamagotchi, DEC-028; crítica, V13)
$preparacaoOnda = New-Object System.Collections.Generic.List[string]   # o que a preparação fez, para o relatório
$temposCpu = New-Object System.Collections.Generic.List[double]         # s desde o início da janela, um por amostra de M1

$script:posLog = [int64]0
$script:bytesLogAntes = [int64]0
$script:logRecriado = $false
$script:erroLeituraLog = $null
$script:verificacoesFilhos = 0
$script:falhasFilhos = 0
$script:maxFilhosSimultaneos = 0
$script:filhosDepoisVerificados = $false
$script:verificacoesRede = 0
$script:falhasRede = 0
$script:falhasTrocas = 0
$script:falhasGpu = 0
$script:amostrasGpuComInstancias = 0
$script:depoisColetado = $false
$script:resFimMedicao = $null
$script:duracaoMedida = 0.0
$script:cpuSegundosJanela = $null
$script:cpuMediaPeriodo = [double]::NaN
$script:cpuPartidaAquecimento = $null
$script:cpuFimJanela = $null
$script:cpuTotalProcesso = $null
$script:threadsUltimaLeitura = $null
$script:indiceInicioJanela = -1     # modo onda: índice em $eventos no começo da janela medida
$script:indiceUsoDaOnda = -1        # modo onda: índice em $eventos do ITEM_DRAG_END que começou o uso da vodka
$script:tFimDaOnda = $null          # modo onda: s desde o início da janela até a amostra em que a onda acabou no log
$script:eventosVistosOnda = 0

# ------------------------------------------------------------------ utilidades
# Tudo o que o bloco finally usa (encerrar, coletar depois, relatório) evita cmdlets:
# depois de Ctrl+C o PowerShell ainda roda o finally, mas cmdlets podem ser interrompidos.

function Tentar([scriptblock] $bloco) { try { & $bloco } catch { } }

function Num($valor, [string] $formato = '0.000') {
    if ($null -eq $valor) { return 'n/d' }
    $d = [double]$valor
    if ([double]::IsNaN($d) -or [double]::IsInfinity($d)) { return 'n/d' }
    return $d.ToString($formato, $cultura)
}

function Duracao([double] $segundos) {
    $ts = [TimeSpan]::FromSeconds([Math]::Max(0.0, $segundos))
    if ($ts.TotalHours -ge 1) { return ('{0}:{1}' -f [int][Math]::Floor($ts.TotalHours), $ts.ToString('mm\:ss')) }
    return $ts.ToString('mm\:ss')
}

function Ordenar($lista) {
    # Cópia ordenada; a lista original fica na ordem das amostras.
    $copia = [double[]]@($lista)
    [Array]::Sort($copia)
    return ,$copia
}

function Media($valores) {
    if ($null -eq $valores -or $valores.Count -eq 0) { return [double]::NaN }
    $soma = 0.0
    foreach ($v in $valores) { $soma += $v }
    return $soma / $valores.Count
}

function Percentil($ordenados, [double] $fracao) {
    # Posto mais próximo, como em medir-p2.ps1.
    $n = $ordenados.Count
    if ($n -eq 0) { return [double]::NaN }
    $i = [int][Math]::Ceiling($n * $fracao) - 1
    if ($i -lt 0) { $i = 0 }
    if ($i -gt $n - 1) { $i = $n - 1 }
    return $ordenados[$i]
}

function Resumo($lista, [string] $unidade, [string] $formato = '0.000') {
    if ($null -eq $lista -or $lista.Count -eq 0) { return 'NÃO MEDIDA' }
    $ord = Ordenar $lista
    return ('média {0} {3} | p95 {1} {3} | máx {2} {3}' -f (Num (Media $ord) $formato), (Num (Percentil $ord 0.95) $formato), (Num $ord[$ord.Count - 1] $formato), $unidade)
}

function ResumoMemoria($lista) {
    if ($null -eq $lista -or $lista.Count -eq 0) { return 'NÃO MEDIDA' }
    $ord = Ordenar $lista
    return ('início {0} MB | fim {1} MB | mín {2} MB | máx {3} MB | média {4} MB' -f (Num $lista[0] '0.00'), (Num $lista[$lista.Count - 1] '0.00'), (Num $ord[0] '0.00'), (Num $ord[$ord.Count - 1] '0.00'), (Num (Media $ord) '0.00'))
}

function MediasPorTerco($lista) {
    $n = $lista.Count
    if ($n -lt 3) { return $null }
    $t = [int][Math]::Floor($n / 3)
    $limites = @(0, $t, (2 * $t), $n)
    $medias = New-Object double[] 3
    for ($j = 0; $j -lt 3; $j++) {
        $soma = 0.0
        for ($i = $limites[$j]; $i -lt $limites[$j + 1]; $i++) { $soma += $lista[$i] }
        $medias[$j] = $soma / ($limites[$j + 1] - $limites[$j])
    }
    return ,$medias
}

function Tendencia($tempos, $valores) {
    # Inclinação por mínimos quadrados, em unidades por hora.
    $n = $valores.Count
    if ($n -lt 3 -or $tempos.Count -ne $n) { return [double]::NaN }
    $mt = Media $tempos
    $mv = Media $valores
    $num = 0.0
    $den = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $dt = $tempos[$i] - $mt
        $num += $dt * ($valores[$i] - $mv)
        $den += $dt * $dt
    }
    if ($den -le 0) { return [double]::NaN }
    return $num / $den * 3600.0
}

function LerResolucao {
    $v = [BuzzyFerramentas.MedicaoDesempenho]::ResolucaoAtualMs()
    if ([double]::IsNaN($v)) { return $null }
    return $v
}

function ResumoResolucao($lista) {
    if ($null -eq $lista -or $lista.Count -eq 0) { return 'não medida' }
    $ord = Ordenar $lista
    $distintos = New-Object System.Collections.Generic.List[string]
    foreach ($v in $ord) {
        $texto = Num $v
        if (-not $distintos.Contains($texto)) { $distintos.Add($texto) }
    }
    $valores = if ($distintos.Count -le 6) { $distintos -join '; ' } else { ($distintos.GetRange(0, 6) -join '; ') + '; ...' }
    return ('mín {0} ms | máx {1} ms | leituras: {2} | valores: {3}' -f (Num $ord[0]), (Num $ord[$ord.Count - 1]), $ord.Count, $valores)
}

function Referencia([bool] $dentro) { if ($dentro) { 'dentro da referência' } else { 'ACIMA da referência' } }

function LerNumero([string] $texto) {
    # Número de um campo do log, em cultura invariável; aceita vírgula por tolerância.
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    $valor = 0.0
    $limpo = $texto.Trim().Replace(',', '.')
    if ([double]::TryParse($limpo, [Globalization.NumberStyles]::Float, $invariante, [ref]$valor)) { return $valor }
    return $null
}

function LerHwnd([string] $texto) {
    # HWND de um campo do log: decimal pelo contrato; aceita 0x... por tolerância.
    if ([string]::IsNullOrWhiteSpace($texto)) { return [IntPtr]::Zero }
    $limpo = $texto.Trim()
    try {
        if ($limpo.StartsWith('0x', [StringComparison]::OrdinalIgnoreCase)) {
            return [IntPtr][Convert]::ToInt64($limpo.Substring(2), 16)
        }
        return [IntPtr][int64]::Parse($limpo, [Globalization.NumberStyles]::Integer, $invariante)
    } catch {
        return [IntPtr]::Zero
    }
}

function DescreverMonitor($m) {
    $principal = if ($m.Principal) { ' [principal]' } else { '' }
    $dpi = if ($m.Dpi -gt 0) { '{0} dpi ({1}%)' -f $m.Dpi, [Math]::Round($m.Dpi * 100.0 / 96.0) } else { 'dpi não lido' }
    $hz = if ($m.Hz -gt 1) { '{0} Hz' -f $m.Hz } else { 'frequência não lida' }
    $retrato = if ($m.Altura -gt $m.Largura) { ', retrato' } else { '' }
    return ('{0}{1}: tela ({2},{3}) {4}x{5}, área útil ({6},{7}) {8}x{9}, {10}, {11}{12}' -f $m.Dispositivo, $principal, $m.X, $m.Y, $m.Largura, $m.Altura, $m.UtilX, $m.UtilY, $m.UtilLargura, $m.UtilAltura, $dpi, $hz, $retrato)
}

function DescreverEnergia($e) {
    if ($null -eq $e -or $e.Count -lt 4) { return 'não lida' }
    $partes = New-Object System.Collections.Generic.List[string]
    switch ([int]$e[0]) {
        0 { $partes.Add('na bateria') }
        1 { $partes.Add('na tomada') }
        default { $partes.Add('alimentação desconhecida') }
    }
    if ([int]$e[1] -eq 128) { $partes.Add('sem bateria') }
    elseif ([int]$e[1] -ne 255 -and [int]$e[2] -le 100) { $partes.Add(('bateria em {0}%' -f $e[2])) }
    if ([int]$e[3] -eq 1) { $partes.Add('economia de bateria LIGADA') }
    return ($partes -join ', ')
}

function LerGit([string[]] $argumentos) {
    # git é opcional: sem ele, o relatório só perde a referência do commit.
    $ErrorActionPreference = 'Continue'
    try {
        $saida = @(& git -C $raiz @argumentos 2>$null)
        if ($LASTEXITCODE -ne 0) { return [pscustomobject]@{ Ok = $false; Linhas = @() } }
        return [pscustomobject]@{ Ok = $true; Linhas = $saida }
    } catch {
        return [pscustomobject]@{ Ok = $false; Linhas = @() }
    }
}

# ------------------------------------------------------------------ log de diagnóstico
function ConverterLinhaBuzzy([string] $linha) {
    # "[hh:mm:ss.fff] BUZZY|CHAVE|campo=valor|campo=valor" -> objeto; outras linhas -> $null.
    $i = $linha.IndexOf('BUZZY|', [StringComparison]::Ordinal)
    if ($i -lt 0) { return $null }
    $partes = $linha.Substring($i).Split('|')
    if ($partes.Count -lt 2 -or [string]::IsNullOrWhiteSpace($partes[1])) { return $null }
    $campos = @{}
    for ($indice = 2; $indice -lt $partes.Count; $indice++) {
        $par = $partes[$indice]
        $igual = $par.IndexOf('=')
        if ($igual -gt 0) { $campos[$par.Substring(0, $igual).Trim()] = $par.Substring($igual + 1).Trim() }
    }
    $carimbo = if ($linha -match '^\s*\[([^\]]+)\]') { $matches[1] } else { '' }
    return [pscustomobject]@{ Chave = $partes[1].Trim(); Campos = $campos; Carimbo = $carimbo; Linha = $linha }
}

function LerLinhasNovas([IO.FileStream] $fluxo, [int64] $desde) {
    # Acrescenta as linhas completas do fluxo a partir de $desde e devolve quantos bytes consumiu. Só avança até a
    # última quebra de linha: uma linha que o app ainda está escrevendo é lida inteira na próxima vez.
    $pendentes = [int]($fluxo.Length - $desde)
    if ($pendentes -le 0) { return [int64]0 }
    $bytes = New-Object byte[] $pendentes
    [void]$fluxo.Seek($desde, [IO.SeekOrigin]::Begin)
    $lidos = 0
    while ($lidos -lt $pendentes) {
        $n = $fluxo.Read($bytes, $lidos, $pendentes - $lidos)
        if ($n -le 0) { break }
        $lidos += $n
    }
    if ($lidos -le 0) { return [int64]0 }
    $ultimaQuebra = [Array]::LastIndexOf($bytes, [byte]10, $lidos - 1)
    if ($ultimaQuebra -lt 0) { return [int64]0 }
    $texto = [Text.Encoding]::UTF8.GetString($bytes, 0, $ultimaQuebra + 1)
    foreach ($bruta in $texto.Split([char]10)) {
        $linha = $bruta.TrimEnd([char]13).TrimStart([char]0xFEFF)
        if ($linha.Length -eq 0) { continue }
        $linhasLog.Add($linha)
        $evento = ConverterLinhaBuzzy $linha
        if ($null -ne $evento) { $eventos.Add($evento) }
    }
    return [int64]($ultimaQuebra + 1)
}

function LerRestoDaCopia([int64] $desde) {
    # Depois de uma rotação (o Buzzy passou de 1 MB e o log virou diagnostico.1.log), o trecho ainda não lido do
    # arquivo anterior está na cópia, a partir do mesmo deslocamento. Uma cópia menor que ele (de outra rotação) não
    # tem nada depois dele: a leitura não traz linha nenhuma.
    if (-not [IO.File]::Exists($logDiagCopia)) { return }
    $copia = $null
    try {
        $copia = [IO.FileStream]::new($logDiagCopia, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        [void](LerLinhasNovas $copia $desde)
    } finally {
        if ($null -ne $copia) { $copia.Dispose() }
    }
}

function AtualizarLog {
    # Lê as linhas completas acrescentadas ao log desde a última leitura. Abre com compartilhamento
    # total para não atrapalhar a escrita do app.
    if (-not [IO.File]::Exists($logDiag)) { return }
    $fluxo = $null
    try {
        $fluxo = [IO.FileStream]::new($logDiag, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        if ($fluxo.Length -lt $script:posLog) {
            # O arquivo encolheu depois de aberto o app: o Buzzy o rotacionou ou ele foi recriado ou truncado. O
            # que faltava ler do arquivo anterior vem da cópia da rotação; depois, o arquivo novo, do começo.
            $script:logRecriado = $true
            LerRestoDaCopia $script:posLog
            $script:posLog = [int64]0
        }
        $script:posLog += (LerLinhasNovas $fluxo $script:posLog)
        $script:erroLeituraLog = $null
    } catch {
        $script:erroLeituraLog = $_.Exception.Message
    } finally {
        if ($null -ne $fluxo) { $fluxo.Dispose() }
    }
}

function PrimeiroEvento([string] $chave) {
    foreach ($e in $eventos) { if ($e.Chave -eq $chave) { return $e } }
    return $null
}

function UltimoEvento([string] $chave) {
    for ($i = $eventos.Count - 1; $i -ge 0; $i--) { if ($eventos[$i].Chave -eq $chave) { return $eventos[$i] } }
    return $null
}

function RuntimeDoLog {
    foreach ($e in $eventos) {
        foreach ($nome in @('runtime', 'framework', 'dotnet', 'clr')) {
            if ($e.Campos.ContainsKey($nome) -and $e.Campos[$nome]) {
                return ('{0} (campo {1} de BUZZY|{2})' -f $e.Campos[$nome], $nome, $e.Chave)
            }
        }
    }
    foreach ($linha in $linhasLog) {
        if ($linha -match '\.NET\s+\d+(\.\d+)+[^\s|]*') { return ('{0} (texto do log)' -f $matches[0]) }
    }
    return $null
}

function UltimasLinhasDoLog([int] $quantas) {
    $inicio = [Math]::Max(0, $linhasLog.Count - $quantas)
    for ($i = $inicio; $i -lt $linhasLog.Count; $i++) { $avisos.Add('   log: ' + $linhasLog[$i]) }
}

# ------------------------------------------------------------------ amostragem
function VerificarFilhos {
    try {
        $lista = @(Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $pidAlvo" -ErrorAction Stop)
    } catch {
        $script:falhasFilhos++
        return
    }
    $script:verificacoesFilhos++
    $agora = 0
    foreach ($f in $lista) {
        # Um órfão antigo, cujo pai morto tinha este mesmo PID, também aparece na consulta;
        # só conta processo criado depois do Buzzy.
        if ($null -ne $f.CreationDate -and $f.CreationDate -lt $inicioProcesso) { continue }
        $agora++
        $criacao = if ($null -ne $f.CreationDate) { $f.CreationDate.Ticks } else { 0 }
        $chave = '{0}|{1}' -f $f.ProcessId, $criacao
        if (-not $filhosVistos.ContainsKey($chave)) {
            $filhosVistos[$chave] = '{0} (PID {1}), visto às {2}' -f $f.Name, $f.ProcessId, [DateTime]::Now.ToString('HH:mm:ss')
        }
    }
    if ($agora -gt $script:maxFilhosSimultaneos) { $script:maxFilhosSimultaneos = $agora }
}

function VerificarRede {
    # Instantâneo: pega conexão que esteja aberta no momento da consulta.
    try {
        $errosTcp = $null
        $errosUdp = $null
        $tcp = @(Get-NetTCPConnection -OwningProcess $pidAlvo -ErrorAction SilentlyContinue -ErrorVariable errosTcp)
        $udp = @(Get-NetUDPEndpoint -OwningProcess $pidAlvo -ErrorAction SilentlyContinue -ErrorVariable errosUdp)
        # "Nenhum objeto encontrado" vem como erro ObjectNotFound: é o resultado esperado.
        foreach ($erro in @($errosTcp) + @($errosUdp)) {
            if ($null -ne $erro -and $erro.CategoryInfo.Category -ne 'ObjectNotFound') { throw $erro.Exception }
        }
    } catch {
        $script:falhasRede++
        return
    }
    $script:verificacoesRede++
    foreach ($c in $tcp) {
        $d = 'TCP {0}:{1} -> {2}:{3} ({4})' -f $c.LocalAddress, $c.LocalPort, $c.RemoteAddress, $c.RemotePort, $c.State
        if (-not $conexoesVistas.Contains($d)) { $conexoesVistas.Add($d) }
    }
    foreach ($u in $udp) {
        $d = 'UDP {0}:{1}' -f $u.LocalAddress, $u.LocalPort
        if (-not $conexoesVistas.Contains($d)) { $conexoesVistas.Add($d) }
    }
}

function LerGpu {
    try {
        $todas = @(Get-CimInstance -Query $consultaGpu -ErrorAction Stop)
    } catch {
        $script:falhasGpu++
        return
    }
    # O LIKE do WQL trata "_" como curinga; aqui fica só o PID exato.
    $prefixo = 'pid_{0}_' -f $pidAlvo
    $total = 0.0
    $porTipo = @{}
    $instancias = 0
    foreach ($engine in $todas) {
        if (-not $engine.Name.StartsWith($prefixo, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $instancias++
        $valor = [double]$engine.UtilizationPercentage
        $total += $valor
        $tipo = if ($engine.Name -match 'engtype_(.+)$') { $matches[1] } else { 'desconhecido' }
        if ($porTipo.ContainsKey($tipo)) { $porTipo[$tipo] += $valor } else { $porTipo[$tipo] = $valor }
    }
    if ($instancias -gt 0) { $script:amostrasGpuComInstancias++ }
    $amostrasGpu.Add($total)
    foreach ($tipo in @($porTipo.Keys)) {
        if ($gpuSomaPorTipo.ContainsKey($tipo)) { $gpuSomaPorTipo[$tipo] += $porTipo[$tipo] } else { $gpuSomaPorTipo[$tipo] = $porTipo[$tipo] }
        if (-not $gpuMaxPorTipo.ContainsKey($tipo) -or $porTipo[$tipo] -gt $gpuMaxPorTipo[$tipo]) { $gpuMaxPorTipo[$tipo] = $porTipo[$tipo] }
    }
}

function LerTrocasCim {
    # Alternativa de P2, usada só se a leitura nativa falhar: cada consulta leva segundos.
    try {
        $soma = 0.0
        foreach ($t in @(Get-CimInstance -Query $consultaThread -ErrorAction Stop)) { $soma += [double]$t.ContextSwitchesPersec }
        $amostrasTrocas.Add($soma)
    } catch {
        $script:falhasTrocas++
    }
}

function MostrarProgresso([double] $t) {
    $cpuMedia = if ($t -gt 0) { ($cpuAnterior - $cpuInicioJanela).TotalSeconds / $t * 100.0 } else { [double]::NaN }
    $priv = if ($amostrasPriv.Count -gt 0) { $amostrasPriv[$amostrasPriv.Count - 1] } else { [double]::NaN }
    Write-Host ('   [{0} de {1}] {2} amostras | CPU média {3}% de um núcleo | trocas/s média {4} | privada {5} MB | filhos {6}' -f (Duracao $t), (Duracao ($Minutos * 60)), $amostrasCpu.Count, (Num $cpuMedia), (Num (Media $amostrasTrocas) '0.00'), (Num $priv '0.00'), $filhosVistos.Count) -ForegroundColor DarkGray
}

# ------------------------------------------------------------------ modo onda (tamagotchi, DEC-028; crítica, V13)
# Mensagens POSTADAS às janelas do próprio Buzzy aberto aqui, com o PID de cada janela conferido imediatamente antes de
# cada mensagem, como nos testes de integração (ItensIntegracaoTestes): nada passa pela fila de input do Windows, o
# cursor não se move e nenhum outro aplicativo recebe nada.

function EsperarEvento([int] $desde, [string] $chave, [hashtable] $campos, [string[]] $comCampos, [double] $limiteSegundos, [string] $oque) {
    # Índice em $eventos do primeiro evento a partir de $desde com a chave, os valores e os campos pedidos.
    $relogio = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        AtualizarLog
        for ($i = $desde; $i -lt $eventos.Count; $i++) {
            $e = $eventos[$i]
            if ($e.Chave -ne $chave) { continue }
            $confere = $true
            foreach ($nome in $campos.Keys) { if ($e.Campos[$nome] -ne [string]$campos[$nome]) { $confere = $false; break } }
            foreach ($nome in $comCampos) { if (-not $e.Campos.ContainsKey($nome)) { $confere = $false; break } }
            if ($confere) { return $i }
        }
        if ($proc.HasExited) { throw ('o Buzzy encerrou (código {0}) antes de registrar: {1}' -f $proc.ExitCode, $oque) }
        if ($relogio.Elapsed.TotalSeconds -ge $limiteSegundos) { throw ('tempo limite de {0} s esgotado esperando no log: {1}' -f $limiteSegundos, $oque) }
        Start-Sleep -Milliseconds 50
    }
}

function ExigirJanelaDoBuzzy([IntPtr] $hwnd, [string] $oque) {
    if ($hwnd -eq [IntPtr]::Zero -or [BuzzyFerramentas.MedicaoDesempenho]::PidDaJanela($hwnd) -ne $pidAlvo) {
        throw ('a janela {0} ({1}) não é do Buzzy aberto por este script (PID {2}); nada foi enviado a ela' -f $hwnd.ToInt64(), $oque, $pidAlvo)
    }
}

function PostarMouse([IntPtr] $hwnd, [int] $mensagem, [int] $botoes, [int[]] $ponto, [string] $oque) {
    ExigirJanelaDoBuzzy $hwnd $oque
    if (-not [BuzzyFerramentas.MedicaoDesempenho]::PostarMouse($hwnd, $mensagem, $botoes, $ponto[0], $ponto[1])) { throw ('PostMessage falhou: {0}' -f $oque) }
}

function LerPonto([string] $texto) {
    # "x,y" ou "(x,y)", com sinais, em pixels físicos.
    if ($texto -match '^\(?(-?\d+),(-?\d+)\)?$') { return [int[]]@([int]$matches[1], [int]$matches[2]) }
    throw ('ponto ilegível no log: {0}' -f $texto)
}

function LerRetangulo([string] $texto) {
    # "(esquerda,topo)-(direita,base)", com sinais, em pixels físicos.
    if ($texto -match '^\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)$') { return [int[]]@([int]$matches[1], [int]$matches[2], [int]$matches[3], [int]$matches[4]) }
    throw ('retângulo ilegível no log: {0}' -f $texto)
}

function Centro([int[]] $r) { return [int[]]@([int][Math]::Floor(($r[0] + $r[2]) / 2.0), [int][Math]::Floor(($r[1] + $r[3]) / 2.0)) }

function PrepararOnda {
    # 1. O ponto opaco do personagem parado, registrado pelo app (BUZZY|POSICAO), e a janela dele (BUZZY|JANELA).
    $M = [BuzzyFerramentas.MedicaoDesempenho]
    $hwndPersonagem = LerHwnd $eJanela.Campos['hwnd']
    $iPosicao = EsperarEvento 0 'POSICAO' @{} @('pontoOpaco') 10 'BUZZY|POSICAO com o ponto opaco'
    $opaco = LerPonto $eventos[$iPosicao].Campos['pontoOpaco']

    # 2. O menu pelo botão direito no personagem; as teclas de acesso I (Itens) e V (Vodka), como WM_CHAR ao dono do
    # menu, cuja fila o laço modal do menu lê.
    $marca = $eventos.Count
    PostarMouse $hwndPersonagem $M::WM_RBUTTONDOWN 0 $opaco 'botão direito no personagem'
    PostarMouse $hwndPersonagem $M::WM_RBUTTONUP 0 $opaco 'botão direito no personagem'
    $iMenu = EsperarEvento $marca 'MENU' @{ exibindo = 'sim' } @('dono') 5 'BUZZY|MENU|exibindo=sim'
    $dono = LerHwnd $eventos[$iMenu].Campos['dono']
    Start-Sleep -Milliseconds 200
    foreach ($tecla in @('i', 'v')) {
        ExigirJanelaDoBuzzy $dono 'dono do menu'
        if (-not $M::PostarCaractere($dono, [char]$tecla)) { throw ("PostMessage WM_CHAR '{0}' ao dono do menu falhou" -f $tecla) }
        Start-Sleep -Milliseconds 150
    }
    $iFechado = EsperarEvento $marca 'MENU' @{} @('fechado') 5 'BUZZY|MENU|fechado'
    $fechado = $eventos[$iFechado]
    if ($fechado.Campos['fechado'] -ne 'Item' -or $fechado.Campos['argumento'] -ne 'Vodka') {
        throw ('o menu fechou com {0} {1}, não com a vodka' -f $fechado.Campos['fechado'], $fechado.Campos['argumento'])
    }
    $preparacaoOnda.Add(('menu pelo botão direito no personagem (mensagem postada), teclas I e V (WM_CHAR ao dono do menu): fechado={0}, argumento={1}; o dono do menu teve o primeiro plano={2}' -f $fechado.Campos['fechado'], $fechado.Campos['argumento'], $fechado.Campos['donoEmPrimeiroPlano']))

    # 3. A janela da vodka aparece, cai e para no chão: o relógio, ligado só pela queda, desliga.
    $iMostrado = EsperarEvento $marca 'ITEM' @{ item = 'Vodka' } @('mostrado', 'hwnd', 'retangulo', 'pontoOpaco') 5 'BUZZY|ITEM|mostrado da vodka'
    $mostrado = $eventos[$iMostrado]
    $id = $mostrado.Campos['mostrado']
    $hwndItem = LerHwnd $mostrado.Campos['hwnd']
    ExigirJanelaDoBuzzy $hwndItem 'janela da vodka'
    $retNascimento = LerRetangulo $mostrado.Campos['retangulo']
    $opacoNascimento = LerPonto $mostrado.Campos['pontoOpaco']
    [void](EsperarEvento $iMostrado 'RELOGIO' @{ ligado = 'nao' } @() 10 'a vodka parar no chão (BUZZY|RELOGIO|ligado=nao)')
    Start-Sleep -Milliseconds 300
    $retItem = $M::RetanguloFisico($hwndItem)
    $retPersonagem = $M::RetanguloFisico($hwndPersonagem)
    if ($null -eq $retItem -or $null -eq $retPersonagem) { throw 'não deu para ler o retângulo da janela da vodka ou do personagem' }
    $preparacaoOnda.Add(('vodka (Id {0}) invocada ao lado dele: nasceu em {1} e parou no chão em ({2},{3})-({4},{5})' -f $id, $mostrado.Campos['retangulo'], $retItem[0], $retItem[1], $retItem[2], $retItem[3]))

    # 4. O arraste até ele: o botão no ponto opaco da vodka e o soltar deslocado do mesmo tanto, para o centro dela
    # ficar no centro dele; oito movimentos com o botão, cada um convertido pela posição atual da janela do item.
    $de = [int[]]@(($retItem[0] + $opacoNascimento[0] - $retNascimento[0]), ($retItem[1] + $opacoNascimento[1] - $retNascimento[1]))
    $centroItem = Centro $retItem
    $centroPersonagem = Centro $retPersonagem
    $ate = [int[]]@(($de[0] + $centroPersonagem[0] - $centroItem[0]), ($de[1] + $centroPersonagem[1] - $centroItem[1]))
    $marca = $eventos.Count
    PostarMouse $hwndItem $M::WM_LBUTTONDOWN $M::MK_LBUTTON $de 'botão pressionado na vodka'
    [void](EsperarEvento $marca 'ITEM' @{ clique = $id } @() 5 'o botão pressionado chegar à vodka (BUZZY|ITEM|clique)')
    Start-Sleep -Milliseconds 80
    for ($passo = 1; $passo -le 8; $passo++) {
        $ponto = [int[]]@(($de[0] + [int][Math]::Truncate(($ate[0] - $de[0]) * $passo / 8.0)), ($de[1] + [int][Math]::Truncate(($ate[1] - $de[1]) * $passo / 8.0)))
        PostarMouse $hwndItem $M::WM_MOUSEMOVE $M::MK_LBUTTON $ponto 'arraste da vodka'
        Start-Sleep -Milliseconds 60
    }
    PostarMouse $hwndItem $M::WM_LBUTTONUP 0 $ate 'soltar a vodka sobre ele'
    $iUso = EsperarEvento $marca 'NUCLEO' @{ evento = 'ItemDragEnd'; para = 'Using' } @() 5 'a vodka solta sobre ele (BUZZY|NUCLEO ItemDragEnd -> Using)'
    $script:indiceUsoDaOnda = $iUso
    $iFim = EsperarEvento $iUso 'NUCLEO' @{ de = 'Using' } @() 10 'o fim do uso (BUZZY|NUCLEO de=Using)'
    $iRelogio = EsperarEvento $iFim 'RELOGIO' @{ ligado = 'nao' } @() 10 'o relógio desligar depois do uso'
    $iOnda = EsperarEvento $iUso 'ONDA' @{ agendada = 'sim' } @() 5 'a onda agendada (BUZZY|ONDA|agendada=sim)'
    $preparacaoOnda.Add(('arraste até ele por mensagens postadas à janela da vodka: {0} ({1}); fim do uso: {2} -> {3}; relógio desligado às {4}; onda agendada com {5} ms (geração {6})' -f $eventos[$iUso].Campos['regra'], $eventos[$iUso].Carimbo, $eventos[$iFim].Campos['de'], $eventos[$iFim].Campos['para'], $eventos[$iRelogio].Carimbo, $eventos[$iOnda].Campos['atrasoMs'], $eventos[$iOnda].Campos['geracao']))
}

function VerificarFimDaOnda([double] $t) {
    # A onda acaba no disparo que leva a "-> fim"; anota a amostra em que a linha apareceu.
    for ($i = [Math]::Max($script:eventosVistosOnda, $script:indiceUsoDaOnda); $i -lt $eventos.Count; $i++) {
        $e = $eventos[$i]
        if ($e.Chave -eq 'NUCLEO' -and $e.Campos['evento'] -eq 'ItemEffectTimer' -and $e.Campos['regra'] -match '-> fim') {
            $script:tFimDaOnda = $t
            break
        }
    }
    $script:eventosVistosOnda = $eventos.Count
}

function ResumoDaOnda {
    # Na janela medida: os disparos da onda, as linhas de relógio ligado e as amostras de CPU com a onda e depois dela.
    $disparos = 0
    $relogioLigado = 0
    if ($script:indiceInicioJanela -ge 0) {
        for ($i = $script:indiceInicioJanela; $i -lt $eventos.Count; $i++) {
            $e = $eventos[$i]
            if ($e.Chave -eq 'ONDA' -and $e.Campos['disparada'] -eq 'sim') { $disparos++ }
            if ($e.Chave -eq 'RELOGIO' -and $e.Campos['ligado'] -eq 'sim') { $relogioLigado++ }
        }
    }
    $com = New-Object System.Collections.Generic.List[double]
    $sem = New-Object System.Collections.Generic.List[double]
    for ($i = 0; $i -lt $amostrasCpu.Count -and $i -lt $temposCpu.Count; $i++) {
        if ($null -eq $script:tFimDaOnda -or $temposCpu[$i] -le $script:tFimDaOnda) { $com.Add($amostrasCpu[$i]) } else { $sem.Add($amostrasCpu[$i]) }
    }
    return [pscustomobject]@{ Disparos = $disparos; RelogioLigado = $relogioLigado; ComOnda = $com; SemOnda = $sem }
}

# ------------------------------------------------------------------ encerramento e depois
function Encerrar {
    # Pode ser chamada de novo (pelo finally) se a primeira chamada for interrompida.
    if ($null -eq $proc) { return }
    if (-not $proc.HasExited) {
        $encerramento.Tentativas++
        $ultima = UltimoEvento 'JANELA'
        $hwnd = if ($null -ne $ultima) { LerHwnd $ultima.Campos['hwnd'] } else { [IntPtr]::Zero }
        $encerramento.Hwnd = $hwnd
        # WM_CLOSE só para uma janela que ainda existe e que é do processo aberto aqui.
        if ($hwnd -ne [IntPtr]::Zero -and [BuzzyFerramentas.MedicaoDesempenho]::IsWindow($hwnd) -and
            [BuzzyFerramentas.MedicaoDesempenho]::PidDaJanela($hwnd) -eq $pidAlvo) {
            $encerramento.HwndValido = $true
            $relogio = [Diagnostics.Stopwatch]::StartNew()
            if ([BuzzyFerramentas.MedicaoDesempenho]::PostMessage($hwnd, [BuzzyFerramentas.MedicaoDesempenho]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)) {
                $encerramento.WmCloseEnviado = $true
                if ($proc.WaitForExit($limiteSaidaSegundos * 1000)) {
                    $encerramento.Saiu = $true
                    $encerramento.SegundosAteSair = $relogio.Elapsed.TotalSeconds
                }
            }
        }
        if (-not $proc.HasExited) {
            # Só o processo que este script abriu, pelo handle guardado desde a abertura;
            # nunca por nome, para não atingir outra instância do Buzzy.
            $encerramento.Forcado = $true
            try { $proc.Kill() } catch { $encerramento.ErroForcado = $_.Exception.Message }
            [void]$proc.WaitForExit($limiteForcadoSegundos * 1000)
        }
    } elseif ($encerramento.Tentativas -eq 0) {
        $encerramento.JaTinhaSaido = $true
    }
    if ($proc.HasExited -and $null -eq $encerramento.CodigoSaida) {
        try { $encerramento.CodigoSaida = $proc.ExitCode } catch { }
        try { $encerramento.HoraSaida = $proc.ExitTime } catch { }
        try { $script:cpuTotalProcesso = $proc.TotalProcessorTime.TotalSeconds } catch { }
    }
}

function ColetarDepois {
    if ($script:depoisColetado -or $null -eq $proc) { return }
    # Últimas linhas do log, incluindo BUZZY|FIM.
    AtualizarLog
    # Filhos ainda vivos depois da saída (critério 10 da Fase 1).
    try {
        foreach ($f in @(Get-CimInstance -ClassName Win32_Process -Filter "ParentProcessId = $pidAlvo" -ErrorAction Stop)) {
            if ($null -ne $f.CreationDate -and $f.CreationDate -lt $inicioProcesso) { continue }
            if ($null -ne $f.CreationDate -and $null -ne $encerramento.HoraSaida -and $f.CreationDate -gt $encerramento.HoraSaida) { continue }
            $filhosVivosDepois.Add(('{0} (PID {1})' -f $f.Name, $f.ProcessId))
        }
        $script:filhosDepoisVerificados = $true
    } catch { }
    # Resolução do timer depois de fechar.
    $fim = [DateTime]::UtcNow.AddSeconds($leituraTimerSegundos)
    while ([DateTime]::UtcNow -lt $fim) {
        $r = LerResolucao
        if ($null -ne $r) { $resDepois.Add($r) }
        [Threading.Thread]::Sleep(250)
    }
    $script:depoisColetado = $true
}

function FecharJanela {
    # Totais da janela medida até a última amostra; vale também para medição interrompida.
    $script:duracaoMedida = $tAnterior
    if ($tAnterior -gt 0) {
        $script:cpuSegundosJanela = ($cpuAnterior - $cpuInicioJanela).TotalSeconds
        $script:cpuMediaPeriodo = $script:cpuSegundosJanela / $tAnterior * 100.0
    }
}

function AtribuicaoTimer {
    # Compara antes, durante e depois. Devolve @{ Codigo; Texto }.
    $durante = New-Object System.Collections.Generic.List[double]
    foreach ($v in $resPartida) { $durante.Add($v) }
    foreach ($v in $resMedicao) { $durante.Add($v) }
    if ($null -ne $script:resFimMedicao) { $durante.Add($script:resFimMedicao) }
    if ($resAntes.Count -eq 0 -or $durante.Count -eq 0 -or $resDepois.Count -eq 0) {
        return @{ Codigo = 'incompleta'; Texto = @('Leitura: faltam leituras de antes, durante ou depois; a atribuição não pode ser feita.') }
    }
    $fora = New-Object System.Collections.Generic.List[double]
    foreach ($v in $resAntes) { $fora.Add($v) }
    foreach ($v in $resDepois) { $fora.Add($v) }
    $oDurante = Ordenar $durante
    $oFora = Ordenar $fora
    $minDurante = $oDurante[0]
    $maxDurante = $oDurante[$oDurante.Count - 1]
    $minFora = $oFora[0]
    $maxFora = $oFora[$oFora.Count - 1]
    $tolerancia = 0.0005
    if (($maxDurante - $minDurante) -lt $tolerancia -and ($maxFora - $minFora) -lt $tolerancia -and [Math]::Abs($minDurante - $minFora) -lt $tolerancia) {
        $texto = @('Leitura: a resolução não mudou ao abrir, durante nem depois de fechar o Buzzy; nada nesta medição é atribuível a ele.')
        if ($null -ne $faixaTimer -and $faixaTimer.Count -ge 2 -and $minFora -lt ([double]$faixaTimer[1] - $tolerancia)) {
            $texto += ('O valor de base desta máquina ({0} ms) já vem mais fino que o padrão ({1} ms) por outro processo; o critério de DEC-011 é sobre quem a muda.' -f (Num $minFora), (Num ([double]$faixaTimer[1])))
        }
        return @{ Codigo = 'sem mudança'; Texto = $texto }
    }
    if ($minDurante -lt ($minFora - $tolerancia)) {
        return @{ Codigo = 'possível'; Texto = @(
            ('Leitura: a resolução ficou mais fina com o Buzzy aberto (mín {0} ms) do que antes e depois (mín {1} ms).' -f (Num $minDurante), (Num $minFora)),
            'Isso é compatível com o Buzzy pedindo resolução maior, mas outro processo pode ter pedido no mesmo período.',
            'Confirmar antes de concluir: repetir a medição e conferir "powercfg /energy" num prompt elevado (lista quem pede resolução de timer).') }
    }
    return @{ Codigo = 'variação'; Texto = @(
        'Leitura: a resolução variou, mas a variação não coincide com abrir e fechar o Buzzy (o mínimo com ele aberto não é mais fino que o de fora).',
        'Provavelmente outro processo; investigar a atribuição antes de concluir.') }
}

# ------------------------------------------------------------------ relatório
function EscreverRelatorio {
    $L = New-Object System.Collections.Generic.List[string]
    $separador = '----------------------------------------------------------------'
    $completa = $medicaoConcluida -and $encerramento.Saiu -and -not $encerramento.Forcado

    $L.Add('================================================================')
    $L.Add('Buzzy — ' + $tituloDaMedicao)
    $L.Add(('Data/hora            : {0} (início da execução)' -f $inicioScript.ToString('yyyy-MM-dd HH:mm:ss')))
    $L.Add(('Modo                 : {0} (argumentos do Buzzy: {1})' -f $Modo, $argumentosDoBuzzy))
    $L.Add('Ferramenta           : tools/medir-desempenho.ps1 (CIM + System.Diagnostics.Process + NtQuerySystemInformation, por PID)')
    if ($completa) {
        $L.Add('Resultado            : medição COMPLETA, encerramento limpo')
    } elseif ($medicaoConcluida) {
        $L.Add('Resultado            : medição completa, mas o encerramento FALHOU (ver Encerramento)')
    } else {
        $L.Add(('Resultado            : medição INCOMPLETA: {0}' -f $falha))
    }
    foreach ($aviso in $avisos) { $L.Add('AVISO: ' + $aviso) }

    $L.Add($separador)
    $L.Add('Ambiente')
    foreach ($linha in $linhasAmbiente) { $L.Add('   ' + $linha) }
    $runtime = RuntimeDoLog
    $L.Add(('   Runtime do Buzzy    : {0}' -f $(if ($runtime) { $runtime } else { 'não informado pelo log' })))

    $L.Add($separador)
    $L.Add('Protocolo')
    $L.Add(('   PID medido          : {0} (processo aberto por este script, com --diagnostico)' -f $pidAlvo))
    $L.Add(('   Log de diagnóstico  : {0} (lido a partir do byte {1})' -f $logDiag, $script:bytesLogAntes))
    $contadoDe = if ($Modo -eq 'onda') { 'contados do fim da preparação da onda, que vem logo depois do primeiro quadro' } else { 'contados do primeiro quadro' }
    $L.Add(('   Aquecimento         : {0} s descartados, {1} (CPU de partida e aquecimento: {2} s)' -f $AquecimentoSegundos, $contadoDe, (Num $script:cpuPartidaAquecimento)))
    $ordIntervalos = Ordenar $intervalosReais
    $maxIntervalo = if ($ordIntervalos.Count -gt 0) { $ordIntervalos[$ordIntervalos.Count - 1] } else { [double]::NaN }
    $L.Add(('   Janela medida       : {0} min pedidos, {1} min amostrados | intervalo pedido {2} s | {3} amostras | intervalo real médio {4} s, máximo {5} s' -f $Minutos, (Num ($script:duracaoMedida / 60.0) '0.00'), $IntervaloSegundos, $amostrasCpu.Count, (Num (Media $intervalosReais)), (Num $maxIntervalo)))

    $L.Add($separador)
    $L.Add('M6 tempo até o primeiro quadro')
    if ($null -ne $m6) {
        $L.Add(('   informado pelo app  : {0} ms (BUZZY|PRIMEIRO_QUADRO, do início do processo ao primeiro quadro)' -f (Num $m6 '0.#')))
    } elseif ($null -ne $eQuadro) {
        $L.Add('   NÃO MEDIDO: a linha BUZZY|PRIMEIRO_QUADRO não trouxe ms numérico: ' + $eQuadro.Linha)
    } else {
        $L.Add('   NÃO MEDIDO: a linha BUZZY|PRIMEIRO_QUADRO não apareceu no log')
    }
    if ($null -ne $quadroVistoMs) {
        $L.Add(('   visto pelo script   : {0} ms entre pedir a abertura e a linha aparecer no log (inclui até 100 ms de espera de leitura; só referência)' -f (Num $quadroVistoMs '0')))
    }
    if ($null -ne $janelaVistaMs) {
        $L.Add(('   BUZZY|JANELA vista  : {0} ms depois de pedir a abertura' -f (Num $janelaVistaMs '0')))
    }

    $L.Add('M1 CPU (delta de TotalProcessorTime sobre o tempo de parede de cada amostra)')
    $L.Add('   percentual de UM núcleo       : ' + (Resumo $amostrasCpu '%'))
    $L.Add(('   percentual da máquina inteira : {0} ({1} processadores lógicos)' -f (Resumo $amostrasCpuMaquina '%'), $nucleos))
    if ($null -ne $script:cpuSegundosJanela -and $script:duracaoMedida -gt 0) {
        $L.Add(('   CPU no período medido         : {0} s em {1} s = {2}% de um núcleo (média exata do período)' -f (Num $script:cpuSegundosJanela), (Num $script:duracaoMedida '0.0'), (Num $script:cpuMediaPeriodo)))
    }
    if ($null -ne $script:cpuFimJanela) { $L.Add(('   CPU acumulada ao fim da medição: {0} s (inclui partida e aquecimento)' -f (Num $script:cpuFimJanela))) }
    if ($null -ne $script:cpuTotalProcesso) { $L.Add(('   CPU acumulada até a saída      : {0} s' -f (Num $script:cpuTotalProcesso))) }

    $L.Add('M2 acordadas por segundo (proxy de DEC-011: trocas de contexto somadas das threads do processo)')
    if ($metodoTrocas -eq 'nativo') {
        $L.Add(('   método: variação do contador de trocas de cada thread (NtQuerySystemInformation), o dado do "Context Switch Delta" do Process Explorer; {0} threads na última leitura' -f $script:threadsUltimaLeitura))
    } elseif ($metodoTrocas -eq 'cim') {
        $L.Add(('   método: contador formatado da classe CIM Win32_PerfFormattedData_PerfProc_Thread, como em P2, porque a leitura nativa falhou ({0}). Cada consulta leva segundos: o intervalo efetivo de M2 é maior que o pedido.' -f $motivoTrocasNativo))
    }
    $L.Add('   trocas de contexto por segundo : ' + (Resumo $amostrasTrocas '/s'))
    $medianaTrocas = Percentil (Ordenar $amostrasTrocas) 0.5
    if ($amostrasTrocas.Count -gt 0) { $L.Add(('   mediana                        : {0} /s ({1} leituras, {2} falhas)' -f (Num $medianaTrocas), $amostrasTrocas.Count, $script:falhasTrocas)) }

    $L.Add('M3 memória')
    $L.Add('   memória privada : ' + (ResumoMemoria $amostrasPriv))
    $L.Add('   working set     : ' + (ResumoMemoria $amostrasWs))
    $variacaoPriv = [double]::NaN
    $variacaoPrivPct = [double]::NaN
    $tercos = MediasPorTerco $amostrasPriv
    if ($amostrasPriv.Count -ge 2) {
        $variacaoPriv = $amostrasPriv[$amostrasPriv.Count - 1] - $amostrasPriv[0]
        if ($amostrasPriv[0] -gt 0) { $variacaoPrivPct = $variacaoPriv / $amostrasPriv[0] * 100.0 }
        $textoTercos = if ($null -ne $tercos) { '{0} -> {1} -> {2} MB' -f (Num $tercos[0] '0.00'), (Num $tercos[1] '0.00'), (Num $tercos[2] '0.00') } else { 'n/d' }
        $L.Add(('   privada, variação do início ao fim: {0} MB ({1}%) | tendência por mínimos quadrados: {2} MB/h | médias por terço: {3}' -f (Num $variacaoPriv '+0.00;-0.00;0.00'), (Num $variacaoPrivPct '+0.0;-0.0;0.0'), (Num (Tendencia $temposMemoria $amostrasPriv) '+0.00;-0.00;0.00'), $textoTercos))
    }

    $L.Add('M4 GPU (contadores GPU Engine do PID, classe CIM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine)')
    if (-not $gpuDisponivel) {
        $L.Add(('   NÃO DISPONÍVEL: a classe de contadores de GPU não respondeu nesta máquina ({0})' -f $motivoGpu))
    } elseif ($amostrasGpu.Count -eq 0) {
        $L.Add(('   NÃO MEDIDA: nenhuma consulta de GPU deu certo ({0} falhas)' -f $script:falhasGpu))
    } else {
        $L.Add('   soma das engines do processo : ' + (Resumo $amostrasGpu '%'))
        $tipos = [string[]]@($gpuSomaPorTipo.Keys)
        [Array]::Sort($tipos)
        foreach ($tipo in $tipos) {
            $L.Add(('   engtype_{0} : média {1} % | máx {2} %' -f $tipo, (Num ($gpuSomaPorTipo[$tipo] / $amostrasGpu.Count)), (Num $gpuMaxPorTipo[$tipo])))
        }
        if ($script:amostrasGpuComInstancias -eq 0) {
            $L.Add('   nenhuma engine de GPU apareceu para este PID: o processo não usou a GPU durante a medição (0%).')
        }
        if ($script:falhasGpu -gt 0) { $L.Add(('   {0} consultas de GPU falharam e ficaram fora da média' -f $script:falhasGpu)) }
    }

    if ($Modo -eq 'onda') {
        $L.Add($separador)
        $L.Add('Onda da vodka (modo onda; tamagotchi, DEC-028; crítica de integração, V13)')
        $L.Add('   preparação, antes do aquecimento, por mensagens postadas às janelas do próprio Buzzy (PID conferido; sem SendInput, sem mover o cursor):')
        if ($preparacaoOnda.Count -eq 0) { $L.Add('   NÃO PREPARADA: a medição parou antes (ver Resultado)') }
        foreach ($linha in $preparacaoOnda) { $L.Add('   - ' + $linha) }
        $L.Add('   linha do tempo da onda no log (carimbo = tempo desde o início do processo do Buzzy):')
        if ($script:indiceUsoDaOnda -ge 0) {
            for ($i = $script:indiceUsoDaOnda; $i -lt $eventos.Count; $i++) {
                $e = $eventos[$i]
                if ($e.Chave -eq 'ONDA' -or ($e.Chave -eq 'NUCLEO' -and $e.Campos['evento'] -eq 'ItemEffectTimer')) {
                    $L.Add(('     [{0}] {1}' -f $e.Carimbo, $e.Linha.Substring($e.Linha.IndexOf('BUZZY|', [StringComparison]::Ordinal))))
                }
            }
        }
        $resumoOnda = ResumoDaOnda
        $L.Add(('   na janela medida: {0} disparo(s) único(s) da onda; {1} linha(s) RELOGIO|ligado=sim (esperado 0: pausado, a onda só troca a cara)' -f $resumoOnda.Disparos, $resumoOnda.RelogioLigado))
        if ($null -ne $script:tFimDaOnda) {
            $L.Add(('   a onda acabou aos {0} da janela medida (amostra em que a linha "-> fim" apareceu no log)' -f (Duracao $script:tFimDaOnda)))
        } elseif ($preparacaoOnda.Count -gt 0) {
            $L.Add('   a onda continuava em curso no fim da janela medida')
        }
        $L.Add('   CPU com a onda ativa         : ' + (Resumo $resumoOnda.ComOnda '%'))
        $L.Add('   CPU depois do fim da onda    : ' + (Resumo $resumoOnda.SemOnda '%'))
    }

    $L.Add($separador)
    $L.Add('Processos filhos (Win32_Process com ParentProcessId = PID, criados depois do Buzzy; esperado 0)')
    $L.Add(('   verificações: {0} (falhas: {1}) | filhos distintos vistos: {2} | máximo ao mesmo tempo: {3} -> {4}' -f $script:verificacoesFilhos, $script:falhasFilhos, $filhosVistos.Count, $script:maxFilhosSimultaneos, $(if ($filhosVistos.Count -eq 0) { 'nenhum filho' } else { 'HÁ FILHOS' })))
    foreach ($descricao in $filhosVistos.Values) { $L.Add('   filho: ' + $descricao) }
    if ($script:filhosDepoisVerificados) {
        $L.Add(('   filhos ainda vivos depois da saída: {0}' -f $filhosVivosDepois.Count))
        foreach ($descricao in $filhosVivosDepois) { $L.Add('   vivo depois da saída: ' + $descricao) }
    } else {
        $L.Add('   filhos ainda vivos depois da saída: não verificado')
    }
    $L.Add(('Rede (SECURITY.md 8, item 3; instantâneos a cada {0} s; esperado nenhuma conexão)' -f $intervaloRedeSegundos))
    $L.Add(('   verificações: {0} (falhas: {1}) | conexões TCP e pontos UDP vistos: {2} -> {3}' -f $script:verificacoesRede, $script:falhasRede, $conexoesVistas.Count, $(if ($conexoesVistas.Count -eq 0) { 'nenhuma' } else { 'HÁ CONEXÕES' })))
    foreach ($descricao in $conexoesVistas) { $L.Add('   ' + $descricao) }

    $L.Add($separador)
    $faixa = if ($null -ne $faixaTimer -and $faixaTimer.Count -ge 2) { '{0} a {1} ms' -f (Num ([double]$faixaTimer[0])), (Num ([double]$faixaTimer[1])) } else { 'não lida' }
    $L.Add(('Resolução do timer global (do sistema inteiro, não só deste processo; faixa da máquina: {0})' -f $faixa))
    $L.Add('   antes de abrir o Buzzy   : ' + (ResumoResolucao $resAntes))
    $L.Add('   partida e aquecimento    : ' + (ResumoResolucao $resPartida))
    $L.Add('   durante a medição        : ' + (ResumoResolucao $resMedicao))
    $L.Add(('   ao terminar a medição    : {0}' -f $(if ($null -ne $script:resFimMedicao) { (Num $script:resFimMedicao) + ' ms' } else { 'não medida' })))
    $L.Add('   depois de fechar o Buzzy : ' + (ResumoResolucao $resDepois))
    foreach ($linha in $atribuicao.Texto) { $L.Add('   ' + $linha) }

    $L.Add($separador)
    $L.Add('Encerramento')
    if ($encerramento.JaTinhaSaido) {
        $L.Add('   o processo já tinha saído sozinho antes do encerramento; WM_CLOSE não foi enviado')
    } elseif ($encerramento.HwndValido) {
        $L.Add(('   WM_CLOSE por PostMessage ao HWND {0} (0x{1}), janela do PID medido: {2}' -f $encerramento.Hwnd.ToInt64(), $encerramento.Hwnd.ToInt64().ToString('X'), $(if ($encerramento.WmCloseEnviado) { 'enviado' } else { 'PostMessage FALHOU' })))
    } else {
        $L.Add(('   WM_CLOSE NÃO enviado: sem HWND válido do PID medido na última linha BUZZY|JANELA (hwnd lido: {0})' -f $encerramento.Hwnd.ToInt64()))
    }
    if ($encerramento.Saiu) {
        $L.Add(('   saída em {0} s depois de WM_CLOSE (tempo limite {1} s) -> encerramento limpo' -f (Num $encerramento.SegundosAteSair '0.00'), $limiteSaidaSegundos))
    } elseif ($encerramento.Forcado) {
        $L.Add(('   FALHA: o processo não saiu em {0} s depois de WM_CLOSE; o script encerrou à força o processo que ele mesmo abriu (PID {1}){2}' -f $limiteSaidaSegundos, $pidAlvo, $(if ($encerramento.ErroForcado) { ', com erro: ' + $encerramento.ErroForcado } else { '' })))
    }
    $eFim = UltimoEvento 'FIM'
    $codigoFim = if ($null -ne $eFim) { $eFim.Campos['codigo'] } else { $null }
    $textoCodigo = if ($null -ne $encerramento.CodigoSaida) { [string]$encerramento.CodigoSaida } else { 'não lido' }
    $textoFim = if ($null -ne $eFim) { 'BUZZY|FIM|codigo={0}' -f $codigoFim } else { 'linha BUZZY|FIM ausente' }
    $coincidem = ($null -ne $eFim -and $null -ne $encerramento.CodigoSaida -and [string]$encerramento.CodigoSaida -eq [string]$codigoFim)
    $L.Add(('   código de saída do processo: {0} | {1}{2}' -f $textoCodigo, $textoFim, $(if ($coincidem) { ' -> coincidem' } elseif ($null -ne $eFim -and $null -ne $encerramento.CodigoSaida) { ' -> DIVERGEM' } else { '' })))

    $L.Add($separador)
    $L.Add('Log de diagnóstico desta execução')
    $contagem = New-Object System.Collections.Specialized.OrderedDictionary
    foreach ($e in $eventos) { if ($contagem.Contains($e.Chave)) { $contagem[$e.Chave] = [int]$contagem[$e.Chave] + 1 } else { $contagem.Add($e.Chave, 1) } }
    $chaves = foreach ($k in $contagem.Keys) { '{0} ({1})' -f $k, $contagem[$k] }
    $L.Add(('   {0} linhas; chaves BUZZY: {1}' -f $linhasLog.Count, $(if ($contagem.Count -gt 0) { $chaves -join ', ' } else { 'nenhuma' })))
    $eInicio = PrimeiroEvento 'INICIO'
    if ($null -ne $eInicio) {
        $nomesCampos = [string[]]@($eInicio.Campos.Keys)
        [Array]::Sort($nomesCampos)
        $campos = foreach ($k in $nomesCampos) { '{0}={1}' -f $k, $eInicio.Campos[$k] }
        $L.Add('   campos de BUZZY|INICIO: ' + ($campos -join ', '))
    }
    $problemas = 0
    foreach ($linha in $linhasLog) {
        if ($problemas -ge 10) { break }
        if ($linha -match 'BUZZY\|(ERRO|FALHA|EXCECAO|EXCEÇÃO|AVISO)' -or $linha -match 'Exception|Exceção') {
            $L.Add('   linha de erro/aviso: ' + $linha)
            $problemas++
        }
    }
    if ($script:logRecriado) { $L.Add('   o arquivo de log encolheu durante a execução (recriado ou truncado); a leitura recomeçou do início') }
    if ($script:erroLeituraLog) { $L.Add('   último erro de leitura do log: ' + $script:erroLeituraLog) }

    $L.Add($separador)
    $L.Add('Comparação informativa com as metas de repouso de Q-08 (DEC-011)')
    $L.Add('   A Fase 1 só precisa REGISTRAR esta linha de base (TODO.md, Fase 1, critério 11). As metas')
    $L.Add('   abaixo são referência para as fases seguintes, verificadas na Fase 11; nada aqui aprova ou')
    $L.Add('   reprova a Fase 1.')
    if ($Modo -eq 'autonomia') {
        $L.Add('   Modo autonomia: o personagem se move durante parte da janela, então as metas de REPOUSO')
        $L.Add('   não se aplicam diretamente; a comparação fica só como ordem de grandeza.')
    }
    if ($Modo -eq 'onda') {
        # V13 da crítica de integração: repouso pausado com a onda de uma vodka, CPU média até 0,1% e relógio desligado.
        $resumoOnda = ResumoDaOnda
        $L.Add(('   Modo onda (V13): repouso pausado com a onda de uma vodka; uma vodka dura cerca de 5 min e 20 s, e o resto da janela é o repouso de sempre.'))
        $L.Add(('   - relógio desligado durante a janela medida : {0} linha(s) RELOGIO|ligado=sim -> {1}' -f $resumoOnda.RelogioLigado, (Referencia ($resumoOnda.RelogioLigado -eq 0 -and $preparacaoOnda.Count -gt 0))))
        $L.Add(('   - CPU média com a onda ativa até 0,1%       : {0}% (média das amostras de 1 s) -> {1}' -f (Num (Media $resumoOnda.ComOnda)), (Referencia ($resumoOnda.ComOnda.Count -gt 0 -and (Media $resumoOnda.ComOnda) -le 0.1))))
    }
    if ($amostrasCpu.Count -gt 0) {
        $janelaCurta = if ($script:duracaoMedida -lt 599) { ' (janela menor que os 10 min de Q-08: só indicativo)' } else { '' }
        $L.Add(('   - CPU média até 0,1% de um núcleo em 10 min : {0}% em {1} min -> {2}{3}' -f (Num $script:cpuMediaPeriodo), (Num ($script:duracaoMedida / 60.0) '0.0'), (Referencia ($script:cpuMediaPeriodo -le 0.1)), $janelaCurta))
        $p95Cpu = Percentil (Ordenar $amostrasCpu) 0.95
        $notaIntervalo = if ($IntervaloSegundos -ne 1) { (' (amostras de {0} s; Q-08 pressupõe 1 s, DEC-011 M1)' -f $IntervaloSegundos) } else { '' }
        $L.Add(('   - CPU p95 até 1% de um núcleo               : {0}% -> {1}{2}' -f (Num $p95Cpu), (Referencia ($p95Cpu -le 1.0)), $notaIntervalo))
    } else {
        $L.Add('   - CPU: não medida')
    }
    $textoTimer = switch ($atribuicao.Codigo) {
        'sem mudança' { 'nenhuma mudança observada -> dentro da referência' }
        'possível' { 'POSSÍVEL mudança atribuível ao Buzzy -> investigar (ver Leitura acima)' }
        'variação' { 'variação que não coincide com abrir e fechar o Buzzy -> investigar a atribuição' }
        default { 'leituras incompletas -> sem comparação' }
    }
    $L.Add('   - resolução global do timer sem mudança atribuível ao Buzzy : ' + $textoTimer)
    if ($amostrasPriv.Count -ge 2) {
        $sobe = ($null -ne $tercos -and $tercos[0] -lt $tercos[1] -and $tercos[1] -lt $tercos[2])
        $L.Add(('   - memória estável : privada {0} MB ({1}%) do início ao fim; {2}. Q-08 compara 1 h e 8 h (até +10%, sem tendência contínua): numa janela curta isto é só indício.' -f (Num $variacaoPriv '+0.00;-0.00;0.00'), (Num $variacaoPrivPct '+0.0;-0.0;0.0'), $(if ($sobe) { 'a média sobe nos três terços da janela (possível tendência; observar na medição longa)' } else { 'sem subida contínua entre os terços da janela' })))
    } else {
        $L.Add('   - memória: não medida')
    }
    if ($amostrasTrocas.Count -gt 0) {
        $L.Add(('   - nenhuma atividade periódica até 300 ms (indício por M2) : mediana {0} trocas/s; um timer de 300 ms daria pelo menos {1}/s o tempo todo -> {2}. A prova é com WPR ou Process Explorer (DEC-011, M2).' -f (Num $medianaTrocas '0.00'), (Num $trocasDeUmTimer300ms '0.00'), $(if ($medianaTrocas -lt $trocasDeUmTimer300ms) { 'nenhum sinal de timer contínuo' } else { 'COMPATÍVEL com atividade periódica; investigar' })))
    }
    $L.Add(('   - primeiro quadro (M6) : {0}; Q-08 ainda não tem limite (propor depois de haver executável real)' -f $(if ($null -ne $m6) { (Num $m6 '0.#') + ' ms' } else { 'não medido' })))
    $L.Add('================================================================')

    $gravou = $false
    try {
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destino))
        [IO.File]::WriteAllLines($Destino, $L.ToArray(), [Text.UTF8Encoding]::new($true))
        $gravou = $true
    } catch {
        $L.Add(('NÃO FOI POSSÍVEL GRAVAR O RELATÓRIO em {0}: {1}' -f $Destino, $_.Exception.Message))
    }
    foreach ($linha in $L) { try { Write-Host $linha } catch { } }
    if ($gravou) { try { Write-Host ''; Write-Host "Relatório salvo em: $Destino" -ForegroundColor Green } catch { } }
}

# ------------------------------------------------------------------ ambiente (antes de abrir)
Write-Host ''
Write-Host ('=== Buzzy — {0} ===' -f $tituloDaMedicao) -ForegroundColor Cyan
Write-Host ('Executável: {0}' -f $Exe) -ForegroundColor DarkGray
Write-Host ('Medição: {0} min, amostra a cada {1} s, aquecimento de {2} s descartado; duração total de cerca de {3} min.' -f $Minutos, $IntervaloSegundos, $AquecimentoSegundos, [Math]::Ceiling($Minutos + ($AquecimentoSegundos + 45) / 60.0)) -ForegroundColor DarkGray
Write-Host ('Relatório: {0}' -f $Destino) -ForegroundColor DarkGray
Write-Host 'Coletando o ambiente...' -ForegroundColor DarkGray

$so = Tentar { Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop }
$versaoWindows = Tentar { Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop }
if ($null -ne $so) {
    $build = if ($null -ne $versaoWindows -and $null -ne $versaoWindows.UBR) { '{0}.{1}' -f $so.BuildNumber, $versaoWindows.UBR } else { $so.BuildNumber }
    $edicao = if ($null -ne $versaoWindows -and $versaoWindows.DisplayVersion) { ' ' + $versaoWindows.DisplayVersion } else { '' }
    $linhasAmbiente.Add(('SO                  : {0}{1}, build {2}, {3}' -f $so.Caption, $edicao, $build, $so.OSArchitecture))
    $linhasAmbiente.Add(('Memória da máquina  : {0} GB' -f (Num ($so.TotalVisibleMemorySize / 1MB) '0.0')))
} else {
    $linhasAmbiente.Add('SO                  : não lido')
}
$processador = Tentar { Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1 }
if ($null -ne $processador) {
    $linhasAmbiente.Add(('Processador         : {0}, {1} núcleos, {2} processadores lógicos' -f $processador.Name.Trim(), $processador.NumberOfCores, $nucleos))
} else {
    $linhasAmbiente.Add(('Processador         : não lido, {0} processadores lógicos' -f $nucleos))
}
$placas = @(Tentar { Get-CimInstance -ClassName Win32_VideoController -ErrorAction Stop })
if ($placas.Count -eq 0) { $linhasAmbiente.Add('GPU                 : não lida') }
foreach ($placa in $placas) { $linhasAmbiente.Add(('GPU                 : {0}, driver {1}' -f $placa.Name, $placa.DriverVersion)) }
$monitores = @(Tentar { [BuzzyFerramentas.MedicaoDesempenho]::LerMonitores() })
$linhasAmbiente.Add(('Monitores           : {0}' -f $(if ($monitores.Count -gt 0) { $monitores.Count } else { 'não lidos' })))
foreach ($monitor in $monitores) { $linhasAmbiente.Add('   ' + (DescreverMonitor $monitor)) }
$energia = Tentar { [BuzzyFerramentas.MedicaoDesempenho]::LerEnergia() }
$plano = Tentar { (Get-CimInstance -Namespace 'root\cimv2\power' -ClassName Win32_PowerPlan -Filter 'IsActive = TRUE' -ErrorAction Stop | Select-Object -First 1).ElementName }
$linhasAmbiente.Add(('Energia             : {0}{1}' -f (DescreverEnergia $energia), $(if ($plano) { '; plano ' + $plano } else { '' })))
$faixaTimer = Tentar { [BuzzyFerramentas.MedicaoDesempenho]::FaixaDaResolucaoMs() }
$infoExe = Get-Item -LiteralPath $Exe
$hashExe = Tentar { (Get-FileHash -LiteralPath $Exe -Algorithm SHA256 -ErrorAction Stop).Hash }
$versaoArquivo = if ($infoExe.VersionInfo.FileVersion) { $infoExe.VersionInfo.FileVersion } else { 'não informada' }
$linhasAmbiente.Add(('Executável          : {0}' -f $Exe))
$linhasAmbiente.Add(('   {0} bytes, modificado em {1}, versão do arquivo {2}' -f $infoExe.Length, $infoExe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), $versaoArquivo))
$linhasAmbiente.Add(('   SHA-256 {0}' -f $(if ($hashExe) { $hashExe } else { 'não calculado' })))
$git = LerGit @('rev-parse', '--short', 'HEAD')
if ($git.Ok -and $git.Linhas.Count -gt 0) {
    $estadoGit = LerGit @('status', '--porcelain')
    $alteracoes = if ($estadoGit.Ok) { '{0} caminho(s) com alteração local' -f $estadoGit.Linhas.Count } else { 'estado local não lido' }
    $linhasAmbiente.Add(('Repositório         : commit {0} ({1})' -f $git.Linhas[0], $alteracoes))
} else {
    $linhasAmbiente.Add('Repositório         : commit não lido (git indisponível)')
}

# ------------------------------------------------------------------ resolução antes de abrir
Write-Host ('Lendo a resolução do timer global por {0} s ANTES de abrir o Buzzy. Ctrl+C agora cancela sem abrir nada.' -f $leituraTimerSegundos) -ForegroundColor Yellow
$fimLeitura = [DateTime]::UtcNow.AddSeconds($leituraTimerSegundos)
while ([DateTime]::UtcNow -lt $fimLeitura) {
    $r = LerResolucao
    if ($null -ne $r) { $resAntes.Add($r) }
    Start-Sleep -Milliseconds 250
}

# Instância única: confere de novo logo antes de abrir.
$abertos = @(ProcessosBuzzyAbertos)
if ($abertos.Count -gt 0) {
    Abortar ('um processo Buzzy apareceu antes da abertura (PID {0}). Feche-o pelo menu da bandeja e rode de novo.' -f (($abertos | ForEach-Object { $_.Id }) -join ', '))
}

# Sem nenhum Buzzy aberto, ninguém usa a pasta do perfil de teste: apagada, o Buzzy medido parte da posição inicial.
$pastaLocalDoUsuario = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData, [Environment+SpecialFolderOption]::DoNotVerify)
$limpezaDoPerfil = LimparPerfilDeTeste $pastaLocalDoUsuario $perfilDeTeste
Write-Host ('Perfil de teste {0}: {1}.' -f $perfilDeTeste, $limpezaDoPerfil) -ForegroundColor DarkGray
$linhasAmbiente.Add(('Perfil de teste     : {0}, em %LOCALAPPDATA%\Buzzy\testes ({1})' -f $perfilDeTeste, $limpezaDoPerfil))
$abertos = @(ProcessosBuzzyAbertos)
if ($abertos.Count -gt 0) {
    Abortar ('um processo Buzzy apareceu durante a limpeza do perfil de teste (PID {0}). Feche-o pelo menu da bandeja e rode de novo.' -f (($abertos | ForEach-Object { $_.Id }) -join ', '))
}

# O log é acrescentado: só interessa o que vier depois deste ponto.
$script:posLog = if ([IO.File]::Exists($logDiag)) { [IO.FileInfo]::new($logDiag).Length } else { [int64]0 }
$script:bytesLogAntes = $script:posLog

# ------------------------------------------------------------------ abrir, aquecer, medir
$proc = $null
$pidAlvo = 0
$inicioProcesso = [DateTime]::MinValue
$falha = $null
$medicaoConcluida = $false
$etapa = 'partida'
$acordadoPedido = $false
$eJanela = $null
$eQuadro = $null
$m6 = $null
$janelaVistaMs = $null
$quadroVistoMs = $null
$gpuDisponivel = $false
$motivoGpu = $null
$consultaGpu = $null
$consultaThread = $null
$contadorTrocas = $null
$metodoTrocas = $null
$motivoTrocasNativo = $null
$cpuInicioJanela = [TimeSpan]::Zero
$cpuAnterior = [TimeSpan]::Zero
$tAnterior = 0.0
$atribuicao = @{ Codigo = 'incompleta'; Texto = @('Leitura: sem leituras suficientes.') }

try {
    $infoPartida = New-Object Diagnostics.ProcessStartInfo
    $infoPartida.FileName = $Exe
    $infoPartida.Arguments = $argumentosDoBuzzy
    $infoPartida.WorkingDirectory = Split-Path -Parent $Exe
    $infoPartida.UseShellExecute = $false

    Write-Host 'Abrindo o Buzzy na tela agora.' -ForegroundColor Yellow
    $relogioPartida = [Diagnostics.Stopwatch]::StartNew()
    $proc = [Diagnostics.Process]::Start($infoPartida)
    $pidAlvo = $proc.Id
    $inicioProcesso = $proc.StartTime
    $acordadoPedido = [BuzzyFerramentas.MedicaoDesempenho]::ManterAcordado($true)
    if (-not $acordadoPedido) { $avisos.Add('o Windows recusou o pedido de manter a tela ligada; se a tela desligar ou a máquina suspender, a medição fica comprometida') }
    Write-Host ('Buzzy aberto: PID {0}. Esperando BUZZY|JANELA e BUZZY|PRIMEIRO_QUADRO no log (até {1} s)...' -f $pidAlvo, $limitePartidaSegundos) -ForegroundColor DarkGray

    while ($true) {
        AtualizarLog
        if ($null -eq $eJanela) {
            $eJanela = PrimeiroEvento 'JANELA'
            if ($null -ne $eJanela) { $janelaVistaMs = $relogioPartida.Elapsed.TotalMilliseconds }
        }
        if ($null -eq $eQuadro) {
            $eQuadro = PrimeiroEvento 'PRIMEIRO_QUADRO'
            if ($null -ne $eQuadro) { $quadroVistoMs = $relogioPartida.Elapsed.TotalMilliseconds }
        }
        if ($null -ne $eJanela -and $null -ne $eQuadro) { break }
        $faltando = @()
        if ($null -eq $eJanela) { $faltando += 'BUZZY|JANELA' }
        if ($null -eq $eQuadro) { $faltando += 'BUZZY|PRIMEIRO_QUADRO' }
        if ($proc.HasExited) {
            UltimasLinhasDoLog 5
            throw ('o Buzzy encerrou sozinho durante a partida, com código {0}, antes de registrar {1}' -f $proc.ExitCode, ($faltando -join ' e '))
        }
        if ($relogioPartida.Elapsed.TotalSeconds -ge $limitePartidaSegundos) {
            if ($null -ne $eJanela) {
                # Sem o primeiro quadro ainda dá para medir e fechar pela janela; só M6 fica sem valor.
                $avisos.Add(('BUZZY|PRIMEIRO_QUADRO não apareceu em {0} s; a medição seguiu sem M6' -f $limitePartidaSegundos))
                break
            }
            UltimasLinhasDoLog 5
            $detalhe = if ($script:erroLeituraLog) { '; último erro de leitura do log: ' + $script:erroLeituraLog } elseif (-not [IO.File]::Exists($logDiag)) { '; o arquivo de log não existe' } else { '' }
            throw ('tempo limite de {0} s esgotado esperando {1} em {2}{3}' -f $limitePartidaSegundos, ($faltando -join ' e '), $logDiag, $detalhe)
        }
        $r = LerResolucao
        if ($null -ne $r) { $resPartida.Add($r) }
        Start-Sleep -Milliseconds 100
    }

    # A janela registrada precisa ser do processo aberto aqui: é para ela que vai o WM_CLOSE.
    $hwndJanela = LerHwnd $eJanela.Campos['hwnd']
    if ($hwndJanela -eq [IntPtr]::Zero) { throw ('a linha BUZZY|JANELA não trouxe hwnd válido: {0}' -f $eJanela.Linha) }
    $donoJanela = [BuzzyFerramentas.MedicaoDesempenho]::PidDaJanela($hwndJanela)
    if ($donoJanela -ne $pidAlvo) {
        throw ('o HWND {0} de BUZZY|JANELA pertence ao PID {1}, não ao processo aberto ({2}); a medição seria do processo errado' -f $hwndJanela.ToInt64(), $donoJanela, $pidAlvo)
    }
    $inicios = @($eventos | Where-Object { $_.Chave -eq 'INICIO' })
    if ($inicios.Count -eq 0) {
        $avisos.Add('o log não tem a linha BUZZY|INICIO; o PID foi conferido pelo dono da janela')
    } elseif (@($inicios | Where-Object { $_.Campos['pid'] -eq [string]$pidAlvo }).Count -eq 0) {
        $avisos.Add(('BUZZY|INICIO trouxe pid={0}, mas o processo aberto é {1}; o PID foi conferido pelo dono da janela' -f (($inicios | ForEach-Object { $_.Campos['pid'] }) -join ','), $pidAlvo))
    }
    if ($null -ne $eQuadro) { $m6 = LerNumero $eQuadro.Campos['ms'] }
    Write-Host ('Janela do Buzzy: HWND {0}. Primeiro quadro: {1} ms (informado pelo app).' -f $hwndJanela.ToInt64(), (Num $m6 '0.#')) -ForegroundColor DarkGray

    # ---------------------------------------------------------------- preparação da onda (modo onda)
    if ($Modo -eq 'onda') {
        $etapa = 'preparação da onda'
        Write-Host 'Modo onda: uma vodka pelo menu e arrastada até ele, por mensagens postadas às janelas do Buzzy (o cursor não se move; o menu toma o primeiro plano por um instante)...' -ForegroundColor Yellow
        PrepararOnda
        foreach ($linha in $preparacaoOnda) { Write-Host ('   ' + $linha) -ForegroundColor DarkGray }
    }

    # ---------------------------------------------------------------- aquecimento
    $etapa = 'aquecimento'
    Write-Host ('Aquecimento: {0} s descartados. A partir de agora, não interaja com o Buzzy nem passe o mouse sobre ele.' -f $AquecimentoSegundos) -ForegroundColor Yellow
    $relogioAquecimento = [Diagnostics.Stopwatch]::StartNew()
    while ($relogioAquecimento.Elapsed.TotalSeconds -lt $AquecimentoSegundos) {
        $resta = $AquecimentoSegundos - $relogioAquecimento.Elapsed.TotalSeconds
        Start-Sleep -Milliseconds ([int][Math]::Max(50.0, [Math]::Min(1000.0, $resta * 1000.0)))
        if ($proc.HasExited) { UltimasLinhasDoLog 5; throw ('o Buzzy encerrou sozinho durante o aquecimento, com código {0}' -f $proc.ExitCode) }
        VerificarFilhos
        $r = LerResolucao
        if ($null -ne $r) { $resPartida.Add($r) }
        AtualizarLog
    }
    $script:cpuPartidaAquecimento = $proc.TotalProcessorTime.TotalSeconds
    if ($null -eq $eQuadro) {
        $eQuadro = PrimeiroEvento 'PRIMEIRO_QUADRO'
        if ($null -ne $eQuadro) {
            $m6 = LerNumero $eQuadro.Campos['ms']
            $avisos.Add('BUZZY|PRIMEIRO_QUADRO chegou depois do tempo limite de partida, durante o aquecimento')
        }
    }

    # ---------------------------------------------------------------- preparação
    # As primeiras consultas CIM e a carga do módulo de rede custam mais; ficam fora da janela.
    $etapa = 'preparação da medição'
    $consultaGpu = "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE 'pid_${pidAlvo}_%'"
    try {
        $null = Get-CimInstance -Query $consultaGpu -ErrorAction Stop
        $gpuDisponivel = $true
    } catch {
        $motivoGpu = $_.Exception.Message
    }
    try {
        $contadorTrocas = New-Object BuzzyFerramentas.ContadorDeTrocas $pidAlvo
        if ($contadorTrocas.Ler() -lt 0) { throw 'o processo não apareceu na lista do sistema' }
        $metodoTrocas = 'nativo'
    } catch {
        $contadorTrocas = $null
        $metodoTrocas = 'cim'
        $motivoTrocasNativo = $_.Exception.Message
        $consultaThread = "SELECT IDProcess, ContextSwitchesPersec FROM Win32_PerfFormattedData_PerfProc_Thread WHERE IDProcess = $pidAlvo"
        $avisos.Add(('M2 pela consulta CIM de P2, porque a leitura nativa falhou ({0}); cada consulta leva segundos e alonga o intervalo das amostras' -f $motivoTrocasNativo))
        # A primeira consulta só aquece o provedor CIM; não entra nas amostras nem nas falhas.
        LerTrocasCim
        $amostrasTrocas.Clear()
        $script:falhasTrocas = 0
    }
    VerificarRede

    # ---------------------------------------------------------------- medição
    $etapa = 'medição'
    Write-Host ('Medindo por {0} min, amostra a cada {1} s. Progresso a cada {2} s:' -f $Minutos, $IntervaloSegundos, $intervaloProgressoSegundos) -ForegroundColor Yellow
    $duracaoAlvo = $Minutos * 60.0
    AtualizarLog
    $script:indiceInicioJanela = $eventos.Count
    $script:eventosVistosOnda = $eventos.Count
    $relogioMedicao = [Diagnostics.Stopwatch]::StartNew()
    $cpuInicioJanela = $proc.TotalProcessorTime
    $cpuAnterior = $cpuInicioJanela
    $tAnterior = 0.0
    $proc.Refresh()
    $amostrasPriv.Add($proc.PrivateMemorySize64 / 1MB)
    $amostrasWs.Add($proc.WorkingSet64 / 1MB)
    $temposMemoria.Add(0.0)
    if ($metodoTrocas -eq 'nativo') { [void]$contadorTrocas.Ler() }
    $tTrocasAnterior = $relogioMedicao.Elapsed.TotalSeconds
    $ultimaRede = 0.0
    $ultimoProgresso = 0.0
    $k = 0

    while ($true) {
        # Agenda fixa: a amostra k cai em k * intervalo. Se uma rodada atrasar, pula para a
        # próxima marca em vez de amostrar em rajada.
        $k = [Math]::Max($k + 1, [int][Math]::Floor($relogioMedicao.Elapsed.TotalSeconds / $IntervaloSegundos) + 1)
        $marca = $k * $IntervaloSegundos
        if ($marca -gt $duracaoAlvo + 0.000001) { break }
        $espera = ($marca - $relogioMedicao.Elapsed.TotalSeconds) * 1000.0
        if ($espera -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Ceiling($espera)) }

        if ($proc.HasExited) {
            UltimasLinhasDoLog 5
            $falha = 'o Buzzy encerrou sozinho durante a medição, com código {0}, aos {1} da janela' -f $proc.ExitCode, (Duracao $tAnterior)
            break
        }

        # M1
        $t = $relogioMedicao.Elapsed.TotalSeconds
        $cpu = $proc.TotalProcessorTime
        $dt = $t - $tAnterior
        if ($dt -le 0) { continue }
        $usoNucleo = ($cpu - $cpuAnterior).TotalSeconds / $dt * 100.0
        $amostrasCpu.Add($usoNucleo)
        $temposCpu.Add($t)
        $amostrasCpuMaquina.Add($usoNucleo / $nucleos)
        $intervalosReais.Add($dt)
        $cpuAnterior = $cpu
        $tAnterior = $t

        # M3
        $proc.Refresh()
        $amostrasPriv.Add($proc.PrivateMemorySize64 / 1MB)
        $amostrasWs.Add($proc.WorkingSet64 / 1MB)
        $temposMemoria.Add($t)

        # M2
        if ($metodoTrocas -eq 'nativo') {
            try {
                $tTrocas = $relogioMedicao.Elapsed.TotalSeconds
                $trocas = $contadorTrocas.Ler()
                if ($trocas -ge 0 -and $tTrocas -gt $tTrocasAnterior) {
                    $amostrasTrocas.Add($trocas / ($tTrocas - $tTrocasAnterior))
                    $script:threadsUltimaLeitura = $contadorTrocas.Threads
                } else {
                    $script:falhasTrocas++
                }
                $tTrocasAnterior = $tTrocas
            } catch {
                $script:falhasTrocas++
            }
        } else {
            LerTrocasCim
        }

        # M4
        if ($gpuDisponivel) { LerGpu }

        # Timer global, filhos, rede e log
        $r = LerResolucao
        if ($null -ne $r) { $resMedicao.Add($r) }
        VerificarFilhos
        if ($t - $ultimaRede -ge $intervaloRedeSegundos) {
            VerificarRede
            $ultimaRede = $t
        }
        AtualizarLog
        if ($Modo -eq 'onda' -and $null -eq $script:tFimDaOnda) { VerificarFimDaOnda $t }

        if ($t - $ultimoProgresso -ge $intervaloProgressoSegundos) {
            MostrarProgresso $t
            $ultimoProgresso = $t
        }
    }

    FecharJanela
    if (-not $proc.HasExited) {
        try { $script:cpuFimJanela = $proc.TotalProcessorTime.TotalSeconds } catch { }
        VerificarRede
    }
    $script:resFimMedicao = LerResolucao
    if ($null -eq $falha) { $medicaoConcluida = $true }
    if ($intervalosReais.Count -gt 0 -and (Media $intervalosReais) -gt 1.2 * $IntervaloSegundos) {
        $avisos.Add(('a amostragem ficou mais lenta que o pedido: intervalo real médio de {0} s para {1} s pedidos' -f (Num (Media $intervalosReais)), $IntervaloSegundos))
    }

    # ---------------------------------------------------------------- encerramento
    $etapa = 'encerramento'
    Write-Host 'Medição terminada. Fechando o Buzzy com WM_CLOSE...' -ForegroundColor DarkGray
    Encerrar
    ColetarDepois
}
catch {
    $falha = '{0} (etapa: {1})' -f $_.Exception.Message, $etapa
}
finally {
    if ($acordadoPedido) { try { [void][BuzzyFerramentas.MedicaoDesempenho]::ManterAcordado($false) } catch { } }
    if ($null -ne $proc) {
        if (-not $medicaoConcluida -and $null -eq $falha) {
            $falha = "interrompida durante a etapa '$etapa' (Ctrl+C ou erro inesperado)"
        }
        try { Encerrar } catch { }
        try { ColetarDepois } catch { }
        try { FecharJanela } catch { }
        try { $atribuicao = AtribuicaoTimer } catch { }
        try {
            EscreverRelatorio
        } catch {
            try { Write-Host ('Falha ao montar o relatório: {0}' -f $_.Exception.Message) -ForegroundColor Red } catch { }
        }
    } elseif ($null -ne $falha) {
        try { Write-Host ('ABORTADO: o Buzzy não chegou a abrir: {0}' -f $falha) -ForegroundColor Red } catch { }
    }
}

if ($null -eq $proc) { exit 2 }
$fotoDepois = FotoDosArquivosReais
if ($fotoDepois -ne $fotoAntes) {
    Write-Host ('FALHA: os arquivos reais do usuário (%LOCALAPPDATA%\Buzzy) mudaram durante a medição, vistos só por fora. Antes: {0}. Depois: {1}.' -f $fotoAntes, $fotoDepois) -ForegroundColor Red
    exit 1
}
if ($null -ne $falha -or -not $encerramento.Saiu -or $encerramento.Forcado) { exit 1 }
# DEC-040, item 8: o veredito de segurança entra no código de saída. Conexão vista, processo filho (durante ou depois da
# saída), nenhuma verificação de rede feita ou mais de 10% delas falhando reprovam a medição (SECURITY.md 8, item 3).
$vereditos = New-Object System.Collections.Generic.List[string]
if ($conexoesVistas.Count -gt 0) { $vereditos.Add(('{0} conexão(ões) do PID' -f $conexoesVistas.Count)) }
if ($filhosVistos.Count -gt 0) { $vereditos.Add(('{0} processo(s) filho(s)' -f $filhosVistos.Count)) }
if ($filhosVivosDepois.Count -gt 0) { $vereditos.Add(('{0} filho(s) vivo(s) depois da saída' -f $filhosVivosDepois.Count)) }
if ($script:verificacoesRede -eq 0) { $vereditos.Add('nenhuma verificação de rede feita') }
elseif ($script:falhasRede -gt ($script:verificacoesRede / 10)) { $vereditos.Add(('{0} de {1} verificações de rede falharam' -f $script:falhasRede, $script:verificacoesRede)) }
if ($vereditos.Count -gt 0) {
    Write-Host ('FALHA DE SEGURANÇA: {0}.' -f ($vereditos -join '; ')) -ForegroundColor Red
    exit 1
}
exit 0
