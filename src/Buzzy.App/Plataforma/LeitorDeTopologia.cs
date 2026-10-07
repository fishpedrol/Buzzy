using System.Runtime.InteropServices;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

// Monitor como o Windows enumera, antes da chave. O nome GDI liga com a config de vídeo.
internal readonly record struct MonitorEnumerado(string NomeGdi, RetanguloPx Tela, RetanguloPx AreaUtil, int Dpi, bool Principal);

// Classe e não record de propósito: o ToString de record imprimiria tudo, e o log só
// leva campos escolhidos.
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

    // Só diferente de 0 na leitura parcial (último recurso).
    internal int MonitoresIgnorados { get; }

    // Função e código, nunca um nome.
    internal string? MotivoDoIgnorado { get; }

    internal Topologia Topologia { get; }

    // Mesma ordem de Topologia.Monitores.
    internal IReadOnlyList<ChaveAtribuida> Chaves { get; }

    internal string? ErroDaConsulta { get; }

    internal int CaminhosSemNome { get; }

    internal int ChavesDoCache => Chaves.Count(c => c.Origem == OrigemDaChave.Cache);

    internal int ChavesDeReserva => Chaves.Count(c => c.Origem == OrigemDaChave.Reserva);

    // "-" se a chave não é desta leitura.
    internal string NomeGdi(string chave) => _nomeGdiPorChave.TryGetValue(chave, out string? nome) ? nome : "-";
}

// Lê os monitores do Windows pro modelo do núcleo, em px físicos do desktop virtual.
// Só geometria, área útil, DPI e a config de vídeo pra chave estável.
internal static class LeitorDeTopologia
{
    private static readonly object Trava = new();

    // Nome GDI -> chave + tela da última leitura boa. Segura a chave quando a consulta
    // falha (sessão remota/bloqueada). Só em memória.
    private static IReadOnlyDictionary<string, ChaveConhecida> _cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase);

    // Nulo se o Windows devolver algo incoerente (ex.: no meio de uma troca de modo, sem
    // principal). Quem chama mantém a anterior.
    internal static Topologia? Ler(out string? erro) => LerDetalhado(out erro)?.Topologia;

    // Nula quando incoerente: enumeração falhou, monitor sem info/DPI ou topologia não fecha.
    // Falha só na config de vídeo não anula: as chaves vêm do cache ou da reserva.
    // parcial é o último recurso depois das retentativas: o monitor ilegível fica de fora
    // em vez de anular tudo. Sem isso, uma falha persistente impedia a partida e deixava o
    // personagem fora da tela quando o monitor dele saía.
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

    // Uma falha anula tudo (com o motivo da primeira), exceto em parcial, que pula e conta.
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

    // Falha de info/DPI ou sem nome GDI devolve nulo: nunca sai topologia faltando
    // monitor nem com escala inventada.
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

    // Sem tocar no Windows. O cache só é trocado com leitura inteira e consulta boa; numa
    // parcial ele fica igual, pra chave de quem ficou de fora estar lá quando voltar.
    // Nome GDI repetido = incoerente.
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
