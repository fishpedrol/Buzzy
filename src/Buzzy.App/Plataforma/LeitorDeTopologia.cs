using System.Runtime.InteropServices;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

/// <summary>Um monitor como o Windows o enumera, antes de ganhar a chave: o nome GDI liga-o à configuração de vídeo.</summary>
internal readonly record struct MonitorEnumerado(string NomeGdi, RetanguloPx Tela, RetanguloPx AreaUtil, int Dpi, bool Principal);

/// <summary>
/// Uma leitura coerente da topologia, com as chaves estáveis (DEC-030) e o que só o adaptador conhece: o nome GDI de
/// cada chave, para o log, e de onde cada chave veio. Classe, e não registro, de propósito: o texto de um registro
/// imprimiria tudo, e o log só leva campos escolhidos.
/// </summary>
internal sealed class LeituraDaTopologia
{
    private readonly Dictionary<string, string> _nomeGdiPorChave;

    internal LeituraDaTopologia(Topologia topologia, IReadOnlyList<ChaveAtribuida> chaves, string? erroDaConsulta, int caminhosSemNome,
        int monitoresIgnorados = 0, string? motivoDoIgnorado = null)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(chaves);
        Topologia = topologia;
        Chaves = chaves;
        ErroDaConsulta = erroDaConsulta;
        CaminhosSemNome = caminhosSemNome;
        MonitoresIgnorados = monitoresIgnorados;
        MotivoDoIgnorado = monitoresIgnorados > 0 ? motivoDoIgnorado : null;
        _nomeGdiPorChave = chaves.ToDictionary(c => c.Chave, c => c.NomeGdi, StringComparer.Ordinal);
    }

    /// <summary>
    /// Monitores enumerados que não puderam ser lidos por inteiro e ficaram de fora: só numa leitura parcial, o último
    /// recurso (<see cref="LeitorDeTopologia.LerDetalhado"/>); 0 na leitura de sempre.
    /// </summary>
    internal int MonitoresIgnorados { get; }

    /// <summary>A falha do primeiro monitor que ficou de fora (função e código, nunca um nome); nulo sem nenhum.</summary>
    internal string? MotivoDoIgnorado { get; }

    internal Topologia Topologia { get; }

    /// <summary>A chave de cada monitor, com o nome GDI e a origem, na ordem de <see cref="Topologia.Monitores"/>.</summary>
    internal IReadOnlyList<ChaveAtribuida> Chaves { get; }

    /// <summary>Nulo se a consulta da configuração de vídeo deu certo; senão, o motivo (função e código).</summary>
    internal string? ErroDaConsulta { get; }

    /// <summary>Alvos ativos cujo nome ou caminho não pôde ser lido numa consulta que deu certo.</summary>
    internal int CaminhosSemNome { get; }

    internal int ChavesDoCache => Chaves.Count(c => c.Origem == OrigemDaChave.Cache);

    internal int ChavesDeReserva => Chaves.Count(c => c.Origem == OrigemDaChave.Reserva);

    /// <summary>O nome GDI (<c>\\.\DISPLAYn</c>) do monitor da chave nesta leitura, ou <c>-</c> se a chave não é dela.</summary>
    internal string NomeGdi(string chave) => _nomeGdiPorChave.TryGetValue(chave, out string? nome) ? nome : "-";
}

/// <summary>
/// Lê a topologia real dos monitores e a converte para o modelo puro do núcleo, em pixels
/// físicos do desktop virtual (DEC-008). Só lê geometria, área útil e DPI dos monitores e, para
/// a chave estável de cada um, a configuração de vídeo (<see cref="ConfiguracaoDeVideo"/>, DEC-030).
/// </summary>
internal static class LeitorDeTopologia
{
    private static readonly object Trava = new();

    /// <summary>
    /// Nome GDI → chave e tela, da última leitura coerente com a consulta boa (<see cref="ChavesDeMonitor.NovoCache"/>).
    /// Segura a chave de um monitor quando a consulta falha (sessão remota, bloqueada ou sem acesso ao console), enquanto
    /// o nome GDI for o mesmo e a tela tiver o mesmo tamanho, mesmo transladada. Só vive nesta execução.
    /// </summary>
    private static IReadOnlyDictionary<string, ChaveConhecida> _cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Topologia atual, ou nulo se o Windows devolver algo incoerente (por exemplo, no meio de
    /// uma troca de modo de vídeo, sem monitor principal). Quem chama mantém a anterior.
    /// </summary>
    internal static Topologia? Ler(out string? erro) => LerDetalhado(out erro)?.Topologia;

    /// <summary>
    /// A leitura completa: a topologia com as chaves estáveis, o nome GDI de cada chave e o resultado da consulta da
    /// configuração de vídeo. Nula, com o motivo em <paramref name="erro"/>, quando a leitura é incoerente: a
    /// enumeração falhou, um monitor não pôde ser lido por inteiro (informação ou DPI) ou a topologia não fecha. Uma
    /// falha só da consulta da configuração de vídeo nunca torna a leitura nula: as chaves vêm do cache ou da reserva.
    /// </summary>
    /// <param name="erro">O motivo de uma leitura incoerente; nulo se ela saiu.</param>
    /// <param name="parcial">
    /// O último recurso, depois das novas tentativas (na partida e na última releitura de uma rajada): um monitor que não
    /// pôde ser lido por inteiro fica de fora e é contado (<see cref="LeituraDaTopologia.MonitoresIgnorados"/>), em vez de
    /// tornar a leitura inteira incoerente. Com uma falha persistente, o Buzzy não partia e, em execução, nenhuma releitura
    /// saía: com o monitor do personagem desconectado, ele ficava fora da tela (revisão de correção do bloco P6-P9, achado
    /// 7). Uma falha passageira continua só adiando a releitura (L3 da crítica).
    /// </param>
    internal static LeituraDaTopologia? LerDetalhado(out string? erro, bool parcial = false)
    {
        ConsultaDeVideo consulta = ConfiguracaoDeVideo.Consultar();
        List<MonitorEnumerado>? enumerados = Enumerar(parcial, out int ignorados, out string? motivoDoIgnorado, out erro);
        if (enumerados is null) return null;
        lock (Trava)
        {
            LeituraDaTopologia? leitura = Montar(enumerados, consulta, _cache, out IReadOnlyDictionary<string, ChaveConhecida> cacheDepois, out erro, ignorados, motivoDoIgnorado);
            _cache = cacheDepois;
            return leitura;
        }
    }

    /// <summary>
    /// Junta o que cada monitor enumerado deu (<see cref="Descrever"/>), na ordem do Windows. Sem falha, todos. Com uma falha,
    /// a leitura é incoerente (nula, com o motivo da primeira, L3), a não ser na leitura <paramref name="parcial"/>, em que os
    /// monitores que falharam ficam de fora e são contados em <paramref name="ignorados"/>, com o motivo do primeiro.
    /// </summary>
    internal static List<MonitorEnumerado>? Juntar(IReadOnlyList<(MonitorEnumerado? Monitor, string? Falha)> lidos, bool parcial, out int ignorados, out string? erro)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        var monitores = new List<MonitorEnumerado>(lidos.Count);
        ignorados = 0;
        erro = null;
        foreach ((MonitorEnumerado? monitor, string? falha) in lidos)
        {
            if (monitor is { } lido)
            {
                monitores.Add(lido);
                continue;
            }
            erro ??= falha ?? "monitor ilegível";
            if (!parcial)
            {
                ignorados = 0;
                return null;
            }
            ignorados++;
        }
        return monitores;
    }

    /// <summary>
    /// Converte o que o Windows informou de um monitor. Uma falha ao ler a informação ou o DPI, ou um monitor sem nome
    /// GDI, devolve nulo com o motivo, e a leitura inteira passa a ser incoerente: a topologia nunca sai sem um monitor
    /// nem com uma escala inventada (crítica da Fase 5, L3). Quem lê tenta de novo, e a anterior continua valendo; só a
    /// leitura parcial, o último recurso, deixa o monitor de fora (<see cref="Juntar"/>).
    /// </summary>
    internal static MonitorEnumerado? Descrever(bool infoLida, int erroDaInfo, Win32.MONITORINFOEX info, int resultadoDoDpi, uint dpi, out string? falha)
    {
        if (!infoLida)
        {
            falha = $"GetMonitorInfo falhou (erro {erroDaInfo})";
            return null;
        }
        if (resultadoDoDpi != 0 || dpi == 0 || dpi > int.MaxValue)
        {
            falha = $"GetDpiForMonitor falhou (0x{resultadoDoDpi:X8}, dpi {dpi})";
            return null;
        }
        if (string.IsNullOrWhiteSpace(info.szDevice))
        {
            falha = "monitor sem nome GDI";
            return null;
        }
        falha = null;
        return new MonitorEnumerado(info.szDevice, Retangulo(info.rcMonitor), Retangulo(info.rcWork), (int)dpi, (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0);
    }

    /// <summary>
    /// Monta a leitura com as chaves (<see cref="ChavesDeMonitor.Atribuir"/>), sem tocar no Windows. O cache só é
    /// trocado, em <paramref name="cacheDepois"/>, por uma leitura coerente e inteira com a consulta boa
    /// (<see cref="ChavesDeMonitor.NovoCache"/>); senão, volta o mesmo <paramref name="cache"/>: numa leitura parcial
    /// (<paramref name="ignorados"/> maior que zero), a chave do monitor que ficou de fora continua lá para quando ele
    /// voltar a ser lido. Nome GDI repetido torna a leitura incoerente, como antes, quando o nome era a chave.
    /// </summary>
    internal static LeituraDaTopologia? Montar(
        IReadOnlyList<MonitorEnumerado> enumerados,
        ConsultaDeVideo consulta,
        IReadOnlyDictionary<string, ChaveConhecida> cache,
        out IReadOnlyDictionary<string, ChaveConhecida> cacheDepois,
        out string? erro,
        int ignorados = 0,
        string? motivoDoIgnorado = null)
    {
        ArgumentNullException.ThrowIfNull(enumerados);
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(cache);
        cacheDepois = cache;

        if (enumerados.Select(m => m.NomeGdi).Distinct(StringComparer.OrdinalIgnoreCase).Count() != enumerados.Count)
        {
            erro = "Nome GDI de monitor repetido.";
            return null;
        }

        (string NomeGdi, RetanguloPx Tela)[] nomesETelas = [.. enumerados.Select(m => (m.NomeGdi, m.Tela))];
        IReadOnlyDictionary<string, string>? mapa = consulta.Erro is null ? ChavesDeMonitor.Mapear(consulta.Alvos) : null;
        IReadOnlyList<ChaveAtribuida> chaves = ChavesDeMonitor.Atribuir(nomesETelas, mapa, cache);

        Topologia topologia;
        try
        {
            topologia = new Topologia(enumerados.Select((m, i) => new MonitorDoDesktop(chaves[i].Chave, m.Tela, m.AreaUtil, m.Dpi, m.Principal)));
        }
        catch (ArgumentException e)
        {
            erro = e.Message;
            return null;
        }

        if (consulta.Erro is null && ignorados == 0) cacheDepois = ChavesDeMonitor.NovoCache(nomesETelas, chaves);
        erro = null;
        return new LeituraDaTopologia(topologia, chaves, consulta.Erro, consulta.CaminhosSemNome, ignorados, motivoDoIgnorado);
    }

    internal static RetanguloPx Retangulo(Win32.RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);

    /// <summary>
    /// Os monitores na ordem do Windows; nulo, com o motivo, se a enumeração falhou ou se um monitor falhou fora da leitura
    /// <paramref name="parcial"/> (<see cref="Juntar"/>).
    /// </summary>
    private static List<MonitorEnumerado>? Enumerar(bool parcial, out int ignorados, out string? motivoDoIgnorado, out string? erro)
    {
        var lidos = new List<(MonitorEnumerado? Monitor, string? Falha)>();

        bool Visitar(nint hMonitor, nint hdc, nint lprc, nint dado)
        {
            var mi = new Win32.MONITORINFOEX { cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>() };
            bool infoLida = Win32.GetMonitorInfo(hMonitor, ref mi);
            int erroDaInfo = infoLida ? 0 : Marshal.GetLastWin32Error();
            uint dpi = 0;
            int resultadoDoDpi = infoLida ? Win32.GetDpiForMonitor(hMonitor, Win32.MDT_EFFECTIVE_DPI, out dpi, out _) : 0;
            MonitorEnumerado? monitor = Descrever(infoLida, erroDaInfo, mi, resultadoDoDpi, dpi, out string? falha);
            lidos.Add((monitor, falha));
            return true;
        }

        Win32.MonitorEnumProc callback = Visitar;
        bool ok = Win32.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);

        motivoDoIgnorado = null;
        if (!ok)
        {
            ignorados = 0;
            erro = "EnumDisplayMonitors falhou";
            return null;
        }
        List<MonitorEnumerado>? monitores = Juntar(lidos, parcial, out ignorados, out string? falhaDoPrimeiro);
        if (monitores is null)
        {
            erro = falhaDoPrimeiro;
            return null;
        }
        motivoDoIgnorado = falhaDoPrimeiro;
        erro = null;
        return monitores;
    }
}
