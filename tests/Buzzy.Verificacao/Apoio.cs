using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Buzzy.App.Plataforma;
using Buzzy.App.Testes.Integracao;

namespace Buzzy.Verificacao;

/// <summary>Arquivo de log acrescentado linha a linha por outro processo de teste.</summary>
internal sealed class LogArquivo(string caminho)
{
    internal string Caminho => caminho;

    internal List<string> Linhas()
    {
        if (!File.Exists(caminho)) return [];
        using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<string>();
        string? l;
        while ((l = sr.ReadLine()) is not null) linhas.Add(l);
        return linhas;
    }

    internal int Contar() => Linhas().Count;

    internal List<string> Desde(int marca) => [.. Linhas().Skip(marca)];

    internal string? EsperarLinha(int marca, string contem, int limiteMs)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            string? achada = Desde(marca).FirstOrDefault(l => l.Contains(contem, StringComparison.Ordinal));
            if (achada is not null) return achada;
            if (DateTime.UtcNow > fim) return null;
            Thread.Sleep(100);
        }
    }
}

/// <summary>Uma linha <c>BUZZY|CHAVE|campo=valor</c> do log de diagnóstico do Buzzy.</summary>
internal sealed record EventoBuzzy(string Chave, IReadOnlyDictionary<string, string> Campos)
{
    private static readonly Regex RePonto = new(@"(-?\d+),(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex ReRetangulo = new(@"\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", RegexOptions.Compiled);

    internal string this[string campo] => Campos.TryGetValue(campo, out string? v) ? v : "";

    /// <summary>
    /// Instante da linha, do prefixo <c>[hh:mm:ss.fff]</c>: o tempo desde o início do processo do Buzzy que a gravou.
    /// Serve para medir durações entre linhas do mesmo Buzzy; nulo sem o prefixo.
    /// </summary>
    internal TimeSpan? Instante { get; init; }

    internal static EventoBuzzy? Ler(string linha)
    {
        int i = linha.IndexOf("BUZZY|", StringComparison.Ordinal);
        if (i < 0) return null;
        string[] partes = linha[(i + 6)..].Split('|');
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string parte in partes.Skip(1))
        {
            int igual = parte.IndexOf('=', StringComparison.Ordinal);
            if (igual > 0) campos[parte[..igual]] = parte[(igual + 1)..];
        }
        int fim = linha.IndexOf(']', StringComparison.Ordinal);
        TimeSpan? instante = linha.StartsWith('[') && fim > 1
            && TimeSpan.TryParseExact(linha.AsSpan(1, fim - 1), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out TimeSpan t) ? t : null;
        return new EventoBuzzy(partes[0], campos) { Instante = instante };
    }

    internal static Nativo.POINT Ponto(string texto)
    {
        Match m = RePonto.Match(texto);
        if (!m.Success) throw new FormatException($"Ponto ilegível: {texto}");
        return new Nativo.POINT(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    internal static Nativo.RECT? Retangulo(string texto)
    {
        Match m = ReRetangulo.Match(texto);
        if (!m.Success) return null;
        int N(int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        return new Nativo.RECT { Left = N(1), Top = N(2), Right = N(3), Bottom = N(4) };
    }
}

/// <summary>Leitura do log de diagnóstico do Buzzy a partir de um deslocamento em bytes.</summary>
internal static class LogDoBuzzy
{
    internal static string Arquivo { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buzzy", "diagnostico.log");

    /// <summary>
    /// A cópia que o Buzzy faz do log ao passar de 1 MB (Diagnostico.cs): o arquivo vira <c>diagnostico.1.log</c> e o
    /// <see cref="Arquivo"/> recomeça vazio.
    /// </summary>
    internal static string ArquivoRotacionado { get; } = Path.Combine(Path.GetDirectoryName(Arquivo)!, "diagnostico.1.log");

    /// <summary>
    /// A marca do fim atual do log, para ler depois só o que vier dali (<see cref="Desde"/>). É um número opaco
    /// (<see cref="LeituraDoLog"/>, a mesma leitura dos testes de integração, compilada junto): reconhece a rotação do log
    /// pelo arquivo, e não pelo tamanho.
    /// </summary>
    internal static long Marca() => LeituraDoLog.Marcar(Arquivo);

    /// <summary>
    /// Os eventos escritos desde a <paramref name="marca"/> (uma <see cref="Marca"/> lida antes). Se o log foi rotacionado
    /// depois dela, o trecho da marca em diante está na cópia <see cref="ArquivoRotacionado"/> e vem antes do arquivo atual
    /// inteiro, mesmo que o arquivo novo já tenha passado do deslocamento da marca: uma verificação longa, com marcas
    /// antigas como a da abertura do Buzzy, atravessa a rotação sem perder linhas.
    /// </summary>
    internal static List<EventoBuzzy> Desde(long marca)
    {
        var eventos = new List<EventoBuzzy>();
        foreach (string linha in LeituraDoLog.LinhasDesde(Arquivo, ArquivoRotacionado, marca))
        {
            if (EventoBuzzy.Ler(linha) is { } e) eventos.Add(e);
        }
        return eventos;
    }

    /// <summary>
    /// Último evento que satisfaz a condição, desde <paramref name="inicio"/>, esperando até o
    /// limite; nulo se não aparecer. <paramref name="desistir"/>, se dado, encerra a espera antes
    /// (por exemplo, quando o processo do Buzzy já saiu).
    /// </summary>
    internal static EventoBuzzy? Esperar(long inicio, Func<EventoBuzzy, bool> condicao, int limiteMs, Func<bool>? desistir = null)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoBuzzy? e = Desde(inicio).LastOrDefault(condicao);
            if (e is not null) return e;
            if (DateTime.UtcNow > fim || desistir?.Invoke() == true) return null;
            Thread.Sleep(100);
        }
    }
}

/// <summary>
/// Isolamento da verificação (Fase 5): todo Buzzy aberto por ela usa o perfil de teste <c>verificacao</c>
/// (<c>--perfil-de-teste</c>), com os dados em <c>%LOCALAPPDATA%\Buzzy\testes\verificacao</c>, nunca nas
/// configurações reais do usuário. A pasta do perfil é apagada antes de abrir cada primeira instância, para o
/// Buzzy partir da posição inicial que a verificação calcula (<c>Posicionador.Inicial</c>); o log de
/// diagnóstico continua na raiz da pasta do Buzzy. O caminho e a limpeza não são escritos aqui: vêm do
/// próprio Buzzy (<see cref="PastaDeDados"/>) e dos testes de integração (<see cref="PerfilDeTeste"/>), cujos
/// fontes o projeto compila junto (Buzzy.Verificacao.csproj), com os testes deles.
/// </summary>
internal static class PerfilDaVerificacao
{
    internal const string Nome = "verificacao";

    /// <summary>
    /// Um Buzzy.exe com o log de diagnóstico, o perfil da verificação, sem o observador de tela cheia (as fases conferem o
    /// lugar inicial, que não pode depender do que estiver em tela cheia na máquina; DEC-034) e os argumentos dados.
    /// </summary>
    internal static ProcessStartInfo Descrever(string exeBuzzy, params string[] argumentos)
    {
        var psi = new ProcessStartInfo(exeBuzzy) { UseShellExecute = false };
        psi.ArgumentList.Add("--diagnostico");
        psi.ArgumentList.Add("--perfil-de-teste");
        psi.ArgumentList.Add(Nome);
        psi.ArgumentList.Add("--sem-tela-cheia");
        foreach (string argumento in argumentos) psi.ArgumentList.Add(argumento);
        return psi;
    }

    /// <summary>
    /// Liga, num descritor de <see cref="Descrever"/>, o observador da janela em primeiro plano (tira <c>--sem-tela-cheia</c>):
    /// a medição do custo dele (<c>--fase custo</c>; protótipo P7). O perfil de teste continua.
    /// </summary>
    internal static void LigarOObservador(ProcessStartInfo psi) => psi.ArgumentList.Remove("--sem-tela-cheia");

    /// <summary>
    /// Apaga a pasta do perfil (<see cref="PastaDeDados.DoPerfilDeTeste"/>), se existir, com tudo o que houver
    /// dentro, pela mesma limpeza dos testes de integração (<see cref="PerfilDeTeste.Limpar(string)"/>). Só com
    /// nenhum Buzzy aberto. Não segue junção nem link simbólico no caminho até ela: nunca apaga nada fora da pasta
    /// do Buzzy.
    /// </summary>
    internal static void Limpar()
    {
        try
        {
            PerfilDeTeste.Limpar(Nome);
        }
        catch (InvalidOperationException e)
        {
            throw new FalhaDeVerificacao($"{e.Message} Nada foi iniciado.");
        }
    }

    /// <summary>
    /// Grava, na pasta do perfil (nunca a real), as preferências com a chave adulta ligada e os nove itens adultos marcados,
    /// sem posição: desde a DEC-041 o padrão é a chave desligada e só quatro marcados, e a verificação do tamagotchi usa
    /// todos os itens. Só com nenhum Buzzy aberto, logo depois de <see cref="Limpar"/>.
    /// </summary>
    internal static void ComOsNoveAdultos()
    {
        string pasta = PastaDeDados.DoPerfilDeTeste(Nome) ?? throw new FalhaDeVerificacao("O Windows não informou a pasta local do usuário. Nada foi iniciado.");
        Directory.CreateDirectory(pasta);
        var preferencias = Buzzy.Core.Personagem.Preferencias.Padrao with
        {
            ConteudoAdulto = true,
            ItensAdultosHabilitados = Buzzy.Core.Personagem.Preferencias.TodosOsItensAdultos,
        };
        File.WriteAllBytes(Path.Combine(pasta, "settings.json"),
            Buzzy.Core.Persistencia.EsquemaDeConfiguracoes.Escrever(new Buzzy.Core.Persistencia.ConfiguracoesSalvas(null, preferencias)));
    }
}

/// <summary>Processos filhos de um processo, para conferir que o Buzzy não cria nenhum.</summary>
internal static class Processos
{
    /// <summary>
    /// Encerra à força um processo que a própria verificação abriu, sem tocar em mais nenhum
    /// (nem nos filhos dele), e devolve o que aconteceu, para o relatório.
    /// </summary>
    internal static string EncerrarAForca(Process processo, string nome)
    {
        int pid = processo.Id;
        try
        {
            processo.Kill(entireProcessTree: false);
            processo.WaitForExit(5000);
            return $"o processo {pid} ({nome}) não encerrou no tempo e foi encerrado À FORÇA pela verificação, que o abriu";
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return processo.HasExited
                ? $"o processo {pid} ({nome}) encerrou sozinho durante a limpeza"
                : $"o processo {pid} ({nome}) não pôde ser encerrado: {e.Message}";
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint h, ref PROCESSENTRY32W e);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint h, ref PROCESSENTRY32W e);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint h);

    /// <summary>
    /// PIDs dos processos cujo pai registrado é <paramref name="pid"/> e que foram criados a
    /// partir de <paramref name="inicioDoPai"/> (Process.StartTime do Buzzy). O Windows guarda só
    /// o número do pai, que pode ser de um processo antigo, já encerrado, cujo PID o Buzzy
    /// reaproveitou; a hora de criação separa os filhos verdadeiros. Funciona também depois de o
    /// Buzzy sair (filhos que sobraram). Hora de criação ilegível conta, por segurança.
    /// </summary>
    internal static List<int> Filhos(int pid, DateTime inicioDoPai)
    {
        var candidatos = new List<int>();
        nint instantaneo = CreateToolhelp32Snapshot(0x00000002, 0);
        if (instantaneo == -1) throw new FalhaDeVerificacao("CreateToolhelp32Snapshot falhou: não deu para conferir processos filhos.");
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (bool ok = Process32First(instantaneo, ref e); ok; ok = Process32Next(instantaneo, ref e))
            {
                if (e.th32ParentProcessID == (uint)pid && e.th32ProcessID != (uint)pid) candidatos.Add((int)e.th32ProcessID);
            }
        }
        finally
        {
            CloseHandle(instantaneo);
        }
        return [.. candidatos.Where(c => CriadoAPartirDe(c, inicioDoPai))];
    }

    private static bool CriadoAPartirDe(int pid, DateTime referencia)
    {
        try
        {
            using Process processo = Process.GetProcessById(pid);
            return processo.StartTime >= referencia;
        }
        catch (ArgumentException)
        {
            return false; // já encerrou entre o instantâneo e agora: não é um filho vivo
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return true; // hora de criação ilegível: conta, por segurança
        }
    }
}

/// <summary>Saída simultânea no console e no relatório.</summary>
internal sealed class Relatorio : IDisposable
{
    private readonly StreamWriter _arquivo;

    internal Relatorio(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        _arquivo = new StreamWriter(caminho, append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }

    internal void Linha(string texto)
    {
        Console.WriteLine(texto);
        _arquivo.WriteLine(texto);
    }

    public void Dispose() => _arquivo.Dispose();
}
