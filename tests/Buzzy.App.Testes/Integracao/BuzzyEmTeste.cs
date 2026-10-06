using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Testes.Integracao;

/// <summary>Uma linha <c>BUZZY|CHAVE|campo=valor</c> do log de diagnóstico.</summary>
internal sealed record EventoDoLog(string Chave, IReadOnlyDictionary<string, string> Campos, string Linha)
{
    internal string this[string campo] => Campos.TryGetValue(campo, out string? v) ? v : "";

    /// <summary>
    /// Instante da linha, lido do prefixo <c>[hh:mm:ss.fff]</c>: o tempo desde o início do processo que a gravou. Serve
    /// para medir durações entre linhas do mesmo Buzzy. Lança <see cref="FormatException"/> sem o prefixo.
    /// </summary>
    internal TimeSpan Instante => InstanteDa(Linha);

    internal static TimeSpan InstanteDa(string linha)
    {
        int fim = linha.IndexOf(']', StringComparison.Ordinal);
        if (linha.StartsWith('[') && fim > 1
            && TimeSpan.TryParseExact(linha.AsSpan(1, fim - 1), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out TimeSpan instante))
            return instante;
        throw new FormatException($"Instante ilegível: {linha}");
    }

    internal static EventoDoLog? Ler(string linha)
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
        return new EventoDoLog(partes[0], campos, linha);
    }

    private static readonly System.Text.RegularExpressions.Regex ReRetangulo =
        new(@"\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RePonto =
        new(@"(-?\d+),(-?\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Lê o formato de <see cref="RetanguloPx.ToString"/>: <c>(e,t)-(d,b)</c>, com sinais.</summary>
    internal static RetanguloPx Retangulo(string texto)
    {
        var m = ReRetangulo.Match(texto);
        if (!m.Success) throw new FormatException($"Retângulo ilegível: {texto}");
        int N(int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        return new RetanguloPx(N(1), N(2), N(3), N(4));
    }

    /// <summary>Lê <c>x,y</c> ou <c>(x,y)</c>, com sinais.</summary>
    internal static PontoPx Ponto(string texto)
    {
        var m = RePonto.Match(texto);
        if (!m.Success) throw new FormatException($"Ponto ilegível: {texto}");
        return new PontoPx(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }
}

/// <summary>Resultado de abrir uma segunda instância do Buzzy.</summary>
/// <param name="SaiuSozinha">Se o processo saiu dentro do tempo, sem o teste encerrá-lo.</param>
/// <param name="Codigo">Código de saída, quando saiu sozinha.</param>
/// <param name="Encerramento">O que o teste precisou fazer porque ela não saiu no tempo; nulo se nada.</param>
internal sealed record SegundaInstancia(bool SaiuSozinha, int? Codigo, string? Encerramento);

/// <summary>
/// Um Buzzy.exe iniciado pelo teste com <c>--diagnostico</c> e um perfil de teste
/// (<c>--perfil-de-teste</c>, padrão <see cref="PerfilDeTeste.Integracao"/>): os dados dele ficam em
/// <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c>, nunca nas configurações reais do usuário. Garante que
/// não havia outro Buzzy aberto (e nunca encerra um que não abriu), apaga a pasta do perfil antes
/// de iniciar, confere que as janelas registradas no log são deste processo antes de mandar
/// qualquer mensagem a elas e lê só o trecho do log escrito depois de iniciar. Se a partida falhar
/// em qualquer ponto, fecha o que abriu.
/// </summary>
internal sealed class BuzzyEmTeste : IDisposable
{
    private readonly long _inicioDoLog;

    internal static string ArquivoDeLog { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buzzy", "diagnostico.log");

    private BuzzyEmTeste(Process processo, long inicioDoLog)
    {
        Processo = processo;
        _inicioDoLog = inicioDoLog;
    }

    internal Process Processo { get; }

    /// <summary>Hora de criação do processo, para separar os filhos dele de PIDs reaproveitados.</summary>
    internal DateTime Inicio { get; private set; }

    /// <summary>Janela do personagem, já conferida como deste processo (0 até lá).</summary>
    internal nint Janela { get; private set; }

    /// <summary>Janela de serviço, já conferida como deste processo (0 até lá).</summary>
    internal nint Servico { get; private set; }

    internal static void ExigirNenhumBuzzyAberto()
    {
        Process[] abertos = Process.GetProcessesByName("Buzzy");
        try
        {
            if (abertos.Length > 0)
                throw new InvalidOperationException($"Já há Buzzy aberto (pids {string.Join(", ", abertos.Select(p => p.Id))}); o teste não encerra processos que não abriu.");
            if (InstanciaDoBuzzy.Aberta())
                throw new InvalidOperationException("Já há um Buzzy aberto nesta sessão (o mutex de instância única existe; talvez com outro nome de arquivo); o teste não encerra processos que não abriu.");
        }
        finally
        {
            foreach (Process p in abertos) p.Dispose();
        }
    }

    /// <summary>
    /// O Buzzy recusa rodar elevado: mostra uma caixa de aviso na tela e sai com código 5. Um
    /// teste elevado passaria a elevação ao Buzzy que abre; por isso ele nem chega a abrir.
    /// </summary>
    internal static void ExigirTesteSemElevacao()
    {
        if (Environment.IsPrivilegedProcess)
            throw new InvalidOperationException("O teste está rodando como administrador: o Buzzy aberto por ele herdaria a elevação, recusaria rodar e mostraria uma caixa de aviso. Rode os testes sem elevação.");
    }

    /// <summary>
    /// A marca do fim atual do log, para ler depois só o que vier dali (<see cref="EventosDesde(long)"/>). É um número
    /// opaco (<see cref="LeituraDoLog"/>): reconhece a rotação do log pelo arquivo, e não pelo tamanho.
    /// </summary>
    internal static long MarcaDoLog() => LeituraDoLog.Marcar(ArquivoDeLog);

    /// <summary>
    /// Descreve, sem iniciar, um Buzzy.exe com <c>--diagnostico</c> e o perfil de teste dado.
    /// Com <paramref name="pausado"/>, o movimento autônomo começa pausado: o personagem fica no
    /// lugar inicial, como os testes de gesto e de janela esperam. Sem <paramref name="telaCheia"/>, vai
    /// <c>--sem-tela-cheia</c>: com um jogo ou vídeo em tela cheia no principal durante a bateria (2026-10-03), o modo
    /// levava o Buzzy de teste ao outro monitor, e os testes do lugar inicial falhavam (DEC-034).
    /// </summary>
    internal static ProcessStartInfo DescreverProcesso(bool pausado = true, ulong? semente = null, string perfil = PerfilDeTeste.Integracao, bool telaCheia = false)
    {
        var psi = new ProcessStartInfo(Caminhos.ExeDoBuzzy()) { UseShellExecute = false };
        psi.ArgumentList.Add("--diagnostico");
        psi.ArgumentList.Add("--perfil-de-teste");
        psi.ArgumentList.Add(perfil);
        if (!telaCheia) psi.ArgumentList.Add("--sem-tela-cheia");
        if (pausado) psi.ArgumentList.Add("--pausado");
        if (semente is { } s)
        {
            psi.ArgumentList.Add("--semente");
            psi.ArgumentList.Add(s.ToString(CultureInfo.InvariantCulture));
        }
        return psi;
    }

    /// <summary>
    /// Inicia um Buzzy.exe com <c>--diagnostico</c> e o perfil de teste, sem conferências e sem
    /// limpar a pasta do perfil (a segunda instância usa isto, com o primeiro Buzzy aberto).
    /// </summary>
    internal static Process IniciarProcesso(bool pausado = true, ulong? semente = null, string perfil = PerfilDeTeste.Integracao, bool telaCheia = false)
        => Process.Start(DescreverProcesso(pausado, semente, perfil, telaCheia)) ?? throw new InvalidOperationException("Buzzy.exe não iniciou.");

    /// <summary>
    /// Inicia o Buzzy do teste. Com <paramref name="limpar"/>, apaga antes a pasta do perfil (só a
    /// de <paramref name="perfil"/>, em <c>%LOCALAPPDATA%\Buzzy\testes</c>), para ele partir sem
    /// posição salva; sem, parte do que a execução anterior com o mesmo perfil deixou. Com <paramref name="telaCheia"/>,
    /// o observador de tela cheia liga, como no uso real (<see cref="DescreverProcesso"/>).
    /// </summary>
    /// <summary>O caminho do Buzzy.exe em teste, para conferir que ele não vai ao log (DEC-040, item 5).</summary>
    internal static string CaminhoDoExecutavel => Caminhos.ExeDoBuzzy();

    /// <summary>
    /// As preferências dos testes do tamagotchi: a chave adulta ligada e os nove itens adultos marcados. Desde a DEC-041, o
    /// padrão é a chave desligada e só três marcados; estes testes exercitam todos os itens, então partem destas.
    /// </summary>
    internal static readonly Preferencias PreferenciasComOsNoveAdultos
        = Preferencias.Padrao with { ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos };

    internal static BuzzyEmTeste Iniciar(bool pausado = true, ulong? semente = null, string perfil = PerfilDeTeste.Integracao, bool limpar = true, bool telaCheia = false,
        bool comOsNoveAdultos = false)
    {
        ExigirTesteSemElevacao();
        string exe = Caminhos.ExeDoBuzzy();
        if (!File.Exists(exe))
            throw new InvalidOperationException($"Buzzy.exe não compilado em {exe} (configuração {Caminhos.Configuracao}, a mesma deste executável de testes).");

        // O trecho do log é marcado ANTES de iniciar: as primeiras linhas do Buzzy podem ser
        // gravadas antes de o Process.Start voltar.
        long inicioDoLog = MarcaDoLog();
        ExigirNenhumBuzzyAberto();

        // Sem nenhum Buzzy aberto, ninguém usa a pasta do perfil.
        if (limpar) PerfilDeTeste.Limpar(perfil);
        // O arquivo das preferências, só na pasta do perfil de teste (nunca a real), sem posição: ele parte do lugar inicial.
        if (comOsNoveAdultos)
        {
            string pasta = PerfilDeTeste.Pasta(perfil, PastaDeDados.DoBuzzy() ?? throw new InvalidOperationException("O Windows não informou a pasta local do usuário."));
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal),
                EsquemaDeConfiguracoes.Escrever(new ConfiguracoesSalvas(null, PreferenciasComOsNoveAdultos)));
        }
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        var b = new BuzzyEmTeste(IniciarProcesso(pausado, semente, perfil, telaCheia), inicioDoLog);
        try
        {
            b.Inicio = b.Processo.StartTime;
            EventoDoLog janela = b.Esperar(e => e.Chave == "JANELA", 15000, "janela do personagem");
            b.Janela = b.JanelaDesteProcesso(janela["hwnd"], "JANELA");
            EventoDoLog servico = b.Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço");
            b.Servico = b.JanelaDesteProcesso(servico["hwnd"], "SERVICO");
            b.Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 5000, "ícone da bandeja");
            return b;
        }
        catch
        {
            b.Dispose(); // WM_CLOSE (se a janela já foi conferida), espera e, se preciso, encerra só este processo
            throw;
        }
    }

    /// <summary>
    /// Abre uma segunda instância, que deve só pedir para a primeira aparecer e sair (código 0;
    /// 6 se não conseguir falar com a primeira). Se não sair no tempo, é encerrada — foi o teste
    /// que a abriu — e o fato volta em <see cref="SegundaInstancia.Encerramento"/>.
    /// </summary>
    internal static SegundaInstancia AbrirSegundaInstancia(int limiteMs = 8000)
    {
        Process segunda = IniciarProcesso();
        bool saiu = false;
        int? codigo = null;
        string? encerramento = null;
        try
        {
            saiu = segunda.WaitForExit(limiteMs);
            if (saiu) codigo = segunda.ExitCode;
        }
        finally
        {
            if (!segunda.HasExited)
            {
                encerramento = EncerrarAForca(segunda, "segunda instância");
                Console.WriteLine("         " + encerramento);
            }
            segunda.Dispose();
        }
        return new SegundaInstancia(saiu, codigo, encerramento);
    }

    /// <summary>Significado dos códigos de saída do Buzzy.exe (CodigosDeSaida.cs), para as mensagens de falha.</summary>
    internal static string DescreverCodigo(int? codigo) => codigo switch
    {
        null => "não saiu",
        CodigosDeSaida.Normal => "saída normal; logo na partida, quer dizer que outra instância já estava aberta",
        CodigosDeSaida.TopologiaIlegivel => "topologia dos monitores ilegível na partida",
        CodigosDeSaida.Elevado => "recusou rodar elevado",
        CodigosDeSaida.InstanciaUnicaIndisponivel => "instância única indisponível, ou a primeira instância não respondeu",
        _ => "código não documentado",
    };

    /// <summary>Eventos do log escritos desde o início deste Buzzy (inclui os de outras instâncias do mesmo período).</summary>
    internal List<EventoDoLog> Eventos() => EventosDesde(_inicioDoLog);

    /// <summary>
    /// A cópia que o Buzzy faz do log ao passar de 1 MB (Diagnostico.cs): o arquivo vira <c>diagnostico.1.log</c> e o
    /// <see cref="ArquivoDeLog"/> recomeça vazio.
    /// </summary>
    internal static string ArquivoRotacionado { get; } = Path.Combine(Path.GetDirectoryName(ArquivoDeLog)!, "diagnostico.1.log");

    /// <summary>
    /// Os eventos escritos desde a <paramref name="marca"/> (uma <see cref="MarcaDoLog"/> lida antes). Se o log foi
    /// rotacionado depois dela, o trecho da marca em diante está na cópia <see cref="ArquivoRotacionado"/> e vem antes do
    /// arquivo atual inteiro, mesmo que o arquivo novo já tenha passado do deslocamento da marca (<see cref="LeituraDoLog"/>):
    /// um teste não perde as linhas escritas entre a marca e a rotação.
    /// </summary>
    internal static List<EventoDoLog> EventosDesde(long marca) => EventosDesde(ArquivoDeLog, ArquivoRotacionado, marca);

    /// <summary>Como <see cref="EventosDesde(long)"/>, com os dois arquivos dados (os testes da leitura usam arquivos temporários).</summary>
    internal static List<EventoDoLog> EventosDesde(string atual, string rotacionado, long marca)
    {
        var eventos = new List<EventoDoLog>();
        foreach (string linha in LeituraDoLog.LinhasDesde(atual, rotacionado, marca))
        {
            if (EventoDoLog.Ler(linha) is { } e) eventos.Add(e);
        }
        return eventos;
    }

    internal EventoDoLog Esperar(Func<EventoDoLog, bool> condicao, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoDoLog? achado = Eventos().LastOrDefault(condicao);
            if (achado is not null) return achado;
            if (Processo.HasExited)
                throw new InvalidOperationException($"O Buzzy encerrou (código {Processo.ExitCode}: {DescreverCodigo(Processo.ExitCode)}) antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim)
                throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(100);
        }
    }

    internal RetanguloPx RetanguloDaJanela()
    {
        NativoTeste.GetWindowRect(Janela, out NativoTeste.RECT r);
        return new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>
    /// Posta uma mensagem de mouse à janela do personagem deste Buzzy, com o ponto de tela
    /// convertido para coordenadas de cliente pela posição ATUAL da janela (sem borda: o cliente é
    /// a janela inteira). Não é input: nada passa pela fila de input do Windows nem por outro
    /// aplicativo; o resultado é rotulado como mensagem postada, não como gesto.
    /// </summary>
    internal void PostarMouse(int mensagem, nint wParam, PontoPx tela) => PostarMouse(Janela, mensagem, wParam, tela);

    /// <summary>
    /// Posta uma mensagem de mouse a uma janela DESTE Buzzy (a do personagem ou a de um item do tamagotchi), com o PID
    /// conferido imediatamente antes e o ponto de tela convertido para coordenadas de cliente pela posição ATUAL dela
    /// (sem borda: o cliente é a janela inteira). Como <see cref="PostarMouse(int, nint, PontoPx)"/>, não é input.
    /// </summary>
    internal void PostarMouse(nint janela, int mensagem, nint wParam, PontoPx tela)
    {
        ExigirDesteProcesso(janela, "mensagem de mouse");
        NativoTeste.GetWindowRect(janela, out NativoTeste.RECT r);
        int x = tela.X - r.Left, y = tela.Y - r.Top;
        nint lParam = (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));
        if (!NativoTeste.PostMessage(janela, mensagem, wParam, lParam))
            throw new InvalidOperationException($"PostMessage 0x{mensagem:X4} à janela {janela} do Buzzy falhou.");
    }

    /// <summary>Posta um caractere (WM_CHAR) a uma janela deste Buzzy, como o dono de um menu aberto, com o PID conferido antes.</summary>
    internal void PostarChar(nint janela, char c)
    {
        ExigirDesteProcesso(janela, $"WM_CHAR '{c}'");
        if (!NativoTeste.PostMessage(janela, NativoTeste.WM_CHAR, c, 0))
            throw new InvalidOperationException($"PostMessage WM_CHAR '{c}' à janela {janela} do Buzzy falhou.");
    }

    // As únicas portas dos testes para mexer nas janelas do Buzzy (SECURITY.md 3.2: o PID conferido antes de cada mensagem).
    // Um teste de fonte (IsolamentoTestes.TestesDoAplicativo_SoMexemNasJanelasDoBuzzyComOPidConferido) proíbe chamar o
    // PostMessage, o SendMessageTimeout, o SetWindowPos e o ShowWindow de NativoTeste fora daqui: se o Buzzy cair no meio de
    // um teste e o HWND for reaproveitado por outro programa, nada vai para a janela dele.

    /// <summary>
    /// Posta uma mensagem a uma janela DESTE Buzzy (a de serviço, a do personagem, a de um item ou a dona de um menu), com o
    /// PID conferido imediatamente antes. Devolve se o Windows aceitou.
    /// </summary>
    internal bool Postar(nint janela, int mensagem, nint wParam, nint lParam, string oQue)
    {
        ExigirDesteProcesso(janela, oQue);
        return NativoTeste.PostMessage(janela, mensagem, wParam, lParam);
    }

    /// <summary>
    /// Envia, sem esperar um Buzzy travado (SendMessageTimeout, 2 s), uma mensagem do sistema que leva ponteiro e não pode ser
    /// postada a outro processo (WM_SETTINGCHANGE), a uma janela deste Buzzy, com o PID conferido imediatamente antes.
    /// Devolve o que o SendMessageTimeout devolveu (0 se falhou).
    /// </summary>
    internal nint Enviar(nint janela, int mensagem, nint wParam, nint lParam, string oQue)
    {
        ExigirDesteProcesso(janela, oQue);
        return NativoTeste.SendMessageTimeout(janela, mensagem, wParam, lParam, NativoTeste.SMTO_ABORTIFHUNG, 2000, out _);
    }

    /// <summary>
    /// Move ou reordena "por fora", como outro agente faria, uma janela DESTE Buzzy (SetWindowPos, sempre sem ativar e sem
    /// mudar o tamanho), com o PID conferido imediatamente antes. Devolve se o Windows aceitou.
    /// </summary>
    internal bool MoverPorFora(nint janela, nint depoisDe, int x, int y, uint flags, string oQue)
    {
        ExigirDesteProcesso(janela, oQue);
        return NativoTeste.SetWindowPos(janela, depoisDe, x, y, 0, 0, flags | NativoTeste.SWP_NOSIZE | NativoTeste.SWP_NOACTIVATE);
    }

    /// <summary>Minimiza "por fora", sem ativar (SW_SHOWMINNOACTIVE), uma janela deste Buzzy, com o PID conferido imediatamente antes.</summary>
    internal void MinimizarPorFora(nint janela, string oQue)
    {
        ExigirDesteProcesso(janela, oQue);
        NativoTeste.ShowWindow(janela, NativoTeste.SW_SHOWMINNOACTIVE);
    }

    /// <summary>Lê um HWND registrado no log (de um item, do dono de um menu) e confere que a janela é deste processo.</summary>
    internal nint JanelaDoLog(string texto, string chave) => JanelaDesteProcesso(texto, chave);

    /// <summary>Se a janela em primeiro plano é deste Buzzy: só o PID dela é lido.</summary>
    internal bool FrenteEhDesteBuzzy()
    {
        nint frente = NativoTeste.GetForegroundWindow();
        return frente != 0 && NativoTeste.PidDe(frente) == (uint)Processo.Id;
    }

    private void ExigirDesteProcesso(nint janela, string oQue)
    {
        if (janela == 0 || NativoTeste.PidDe(janela) != (uint)Processo.Id)
            throw new InvalidOperationException($"A janela {janela} não é do Buzzy aberto pelo teste (pid {Processo.Id}); {oQue} não enviada.");
    }

    /// <summary>Espera a janela do personagem chegar ao retângulo dado.</summary>
    internal void EsperarRetangulo(RetanguloPx esperado, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        RetanguloPx atual;
        while ((atual = RetanguloDaJanela()) != esperado)
        {
            if (Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando {oQue}: janela em {atual}, esperado {esperado}.");
            Thread.Sleep(20);
        }
    }

    /// <summary>Fecha pelo WM_CLOSE da janela do personagem, com o PID conferido antes, e devolve o código de saída.</summary>
    internal int FecharPorWmClose(int limiteMs = 5000)
    {
        Postar(Janela, NativoTeste.WM_CLOSE, 0, 0, "WM_CLOSE");
        if (!Processo.WaitForExit(limiteMs))
            throw new TimeoutException("O Buzzy não encerrou depois de WM_CLOSE.");
        return Processo.ExitCode;
    }

    /// <summary>
    /// Fecha o Buzzy que ESTE teste abriu: WM_CLOSE à janela do personagem, só se ela já foi
    /// conferida como deste processo e o PID dela ainda é o dele; espera; se preciso, encerra só este
    /// processo. Não lança, para não esconder a falha que levou até aqui.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (!Processo.HasExited)
            {
                bool postou = Janela != 0 && NativoTeste.PidDe(Janela) == (uint)Processo.Id && Postar(Janela, NativoTeste.WM_CLOSE, 0, 0, "WM_CLOSE");
                if (!Processo.WaitForExit(postou ? 5000 : 1000))
                    Console.WriteLine("         limpeza: " + EncerrarAForca(Processo, "Buzzy aberto pelo teste"));
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            Console.WriteLine($"         limpeza: não deu para conferir ou encerrar o Buzzy aberto pelo teste: {e.Message}");
        }
        finally
        {
            Processo.Dispose();
        }
    }

    /// <summary>Encerra à força um processo que o próprio teste abriu, sem tocar em mais nenhum (nem nos filhos).</summary>
    internal static string EncerrarAForca(Process processo, string nome)
    {
        int pid = processo.Id;
        try
        {
            processo.Kill(entireProcessTree: false);
            processo.WaitForExit(5000);
            return $"o processo {pid} ({nome}) não encerrou no tempo e foi encerrado à força pelo teste que o abriu";
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return processo.HasExited
                ? $"o processo {pid} ({nome}) encerrou sozinho durante a limpeza"
                : $"o processo {pid} ({nome}) não pôde ser encerrado: {e.Message}";
        }
    }

    /// <summary>Lê o HWND registrado no log e confere que a janela é deste processo; sem isso, nenhuma mensagem vai para ela.</summary>
    private nint JanelaDesteProcesso(string texto, string chave)
    {
        if (!long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out long valor) || valor == 0)
            throw new InvalidOperationException($"HWND de {chave} ilegível no log: '{texto}'.");
        var hwnd = (nint)valor;
        uint pid = NativoTeste.PidDe(hwnd);
        if (pid != (uint)Processo.Id)
            throw new InvalidOperationException($"O HWND de {chave} ({hwnd}) registrado no log não pertence ao Buzzy aberto pelo teste (pid {Processo.Id}); nenhuma mensagem foi enviada a ele.");
        return hwnd;
    }
}
