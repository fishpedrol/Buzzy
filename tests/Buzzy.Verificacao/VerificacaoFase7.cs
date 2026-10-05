using System.Diagnostics;
using System.Globalization;
using System.IO;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética da Fase 7 (passo F7-P11; DEC-037), na máquina em que roda, com o receptor desta ferramenta no papel
/// do aplicativo em uso (outro processo, em primeiro plano o tempo todo):
/// <list type="bullet">
/// <item>C1, chegar perto: com o receptor no monitor dele por bastante tempo, o Buzzy pede o vão, anda até o lado do
/// receptor e fica olhando, ao lado dele, nos pés do chão, sem cobrir a janela, sem tirar o foco;</item>
/// <item>C2, o flagra: um clique SINTÉTICO enquanto ele olha dá a reação de flagra;</item>
/// <item>C3, ir ver: com o receptor levado ao outro monitor, ele vai até lá, uma vez (só com um vizinho de mesmo chão);</item>
/// <item>C4, a dispensa: arrastado e solto, ele fica, mesmo com o receptor levado de volta;</item>
/// <item>C5, a contenção no log: nenhuma linha por troca de foco, nada da janela (título nem identificador) e a linha do fim
/// com só as contagens, inclusive a dos vãos pedidos.</item>
/// </list>
/// Todo input é SINTÉTICO. A energia é a padrão (Média): chegar perto leva cerca de 90 s; a agenda segue sorteando o resto.
/// </summary>
internal sealed partial class Verificacao
{
    private const ulong SementeDaFase7 = 7;

    internal Sumario ExecutarFase7()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        Nativo.RECT? receptorOriginal = null;
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            receptorOriginal = Nativo.Retangulo(_hReceptor);
            AtivarReceptor("início");
            Topologia topologia = TopologiaLida();
            _prefixo = "Fase 7 — ";
            AbrirBuzzyCurioso(SementeDaFase7);
            try
            {
                bool olhando = F7ChegarPerto();
                Digitar("F7-perto;");
                if (olhando) F7Flagra();
                else Registrar("C2 — flagra (DEC-037, item 8)", Inconclusivo, "sem o olhar do C1");
                bool foi = F7IrVer(topologia);
                Digitar("F7-ida;");
                F7Dispensa(topologia, foi);
            }
            finally
            {
                FecharBuzzy("Fase 7");
            }
            F7ContencaoNoLog();
            _prefixo = "";
            _inj.ConferirUltimoInput();
        }
        catch (Interferencia e)
        {
            _houveInterferencia = true;
            _prefixo = "";
            Registrar("EXECUÇÃO", Invalida, "interferência humana detectada: " + e.Message + " Os resultados desta execução não valem.");
        }
        catch (Exception e)
        {
            _prefixo = "";
            Registrar("EXECUÇÃO", Falhou, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _prefixo = "";
            if (receptorOriginal is { } ro && _hReceptor != 0) MoverReceptor(ro.Left, ro.Top);
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        _rel.Linha("   Cobertura da Fase 7: chegar perto, o flagra, ir ver (com um vizinho de mesmo chão), a dispensa e a contenção no log, com a energia Média; as outras energias e os detalhes ficam com os testes automatizados (CuriosidadeTestes, EnergiaDaFase7Testes).");
        return Resumir();
    }

    /// <summary>Abre o Buzzy com a autonomia, a semente e o observador da janela ativa ligados (sem <c>--sem-tela-cheia</c>).</summary>
    private void AbrirBuzzyCurioso(ulong semente)
    {
        _inicioLogBuzzy = LogDoBuzzy.Marca();
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy, "--semente", semente.ToString(CultureInfo.InvariantCulture));
        PerfilDaVerificacao.LigarOObservador(psi);
        ExigirNenhumBuzzyAberto();
        PerfilDaVerificacao.Limpar();
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não iniciou.");
        _inicioBuzzy = _buzzy.StartTime;
        _buzzyEncerrado = false;
        _hBuzzy = JanelaDoBuzzy(Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy")["hwnd"], "JANELA");
        _hServico = JanelaDoBuzzy(Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço")["hwnd"], "SERVICO");
        Esperar(e => e.Chave == "TELA_CHEIA" && e["observador"] == "ligado", 5000, "observador ligado");
        _rel.Linha($"   Buzzy aberto com a autonomia, a semente {semente} e o observador: pid {_buzzy.Id}");
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois de abrir o Buzzy");
    }

    /// <summary>Move o receptor (desta ferramenta) sem ativá-lo nem mudar a ordem Z; o ponto de ativação acompanha.</summary>
    private void MoverReceptor(int x, int y)
    {
        Nativo.RECT antes = Nativo.Retangulo(_hReceptor);
        Nativo.SetWindowPos(_hReceptor, 0, x, y, 0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOZORDER | Nativo.SWP_NOACTIVATE);
        Thread.Sleep(200);
        Nativo.RECT depois = Nativo.Retangulo(_hReceptor);
        _alvoReceptor = new Nativo.POINT(_alvoReceptor.X + depois.Left - antes.Left, _alvoReceptor.Y + depois.Top - antes.Top);
    }

    private static bool Regra(EventoBuzzy e, string inicio) => e.Chave == "NUCLEO" && e["regra"].StartsWith(inicio, StringComparison.Ordinal);

    // ------------------------------------------------------------------ C1: chegar perto e olhar

    private bool F7ChegarPerto()
    {
        const string Criterio = "C1 — chegar perto (DEC-037, item 5): com o aplicativo em uso no monitor dele, pede o vão uma vez, anda até o lado da janela e fica olhando, no chão, sem cobri-la, e o foco fica no aplicativo";
        int marcaR = _logReceptor.Contar();
        long inicio = _inicioLogBuzzy;
        EventoBuzzy? olha = LogDoBuzzy.Esperar(inicio, e => e.Chave == "NUCLEO" && e["regra"] == "CURIOSIDADE: olha a janela", 300000, () => _buzzy is { HasExited: true });
        if (olha is null)
        {
            int pedidos = LogDoBuzzy.Desde(inicio).Count(e => Regra(e, "CURIOSIDADE: pede o vão"));
            Registrar(Criterio, Falhou, $"nenhum olhar em 300 s; vãos pedidos={pedidos}");
            return false;
        }
        Thread.Sleep(400);
        Nativo.RECT b = Nativo.Retangulo(_hBuzzy), j = Nativo.Retangulo(_hReceptor);
        Topologia t = TopologiaLida();
        MonitorDoDesktop? m = t.Monitores.FirstOrDefault(x => x.AreaUtil.Contem(new RetanguloPx(b.Left, b.Top, b.Right, b.Bottom)));
        double escala = (m?.Dpi ?? 96) / 96.0;
        int meio = (b.Left + b.Right) / 2;
        // Fora do trecho da janela (com o arredondamento para fora, até 1/16 da área útil a mais) e encostado nela pela folga.
        double folga = 16 * escala, parte = (m?.AreaUtil.Largura ?? 1920) / 16.0;
        double ateAJanela = meio < (j.Left + j.Right) / 2 ? j.Left - (meio + (b.Right - b.Left) / 2.0) : (meio - (b.Right - b.Left) / 2.0) - j.Right;
        bool aoLado = ateAJanela >= folga - 12 && ateAJanela <= folga + parte + 12;
        bool noChao = m is not null && b.Bottom == m.AreaUtil.Base;
        int pedidosDoVao = LogDoBuzzy.Desde(inicio).Count(e => Regra(e, "CURIOSIDADE: pede o vão"));
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        bool ok = aoLado && noChao && pedidosDoVao >= 1 && frente && !desativou;
        Registrar(Criterio, ok, $"olhou aos {(olha.Instante?.TotalSeconds ?? 0):0} s; Buzzy {b}, janela {j}; distância até a janela {ateAJanela:0} px (folga {folga:0}, mais até {parte:0} do arredondamento); no chão={noChao}; vãos pedidos={pedidosDoVao}; receptor na frente={frente}; desativado={desativou}");
        return ok;
    }

    // ------------------------------------------------------------------ C2: o flagra

    private void F7Flagra()
    {
        const string Criterio = "C2 — flagra (DEC-037, item 8): clicado enquanto olha, ele reage com o flagra, sem sair do lugar";
        int marcaR = _logReceptor.Contar();
        long marca = LogDoBuzzy.Marca();
        Nativo.RECT antes = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT p = PontoDoCorpo();
        _inj.CliqueEsquerdo(p.X, p.Y, _hBuzzy);
        EventoBuzzy? flagra = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["regra"] == "CLICK: flagra (olhava a janela)", 3000);
        EventoBuzzy? fim = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["regra"] == "REACTING: fim da reação", 5000);
        Thread.Sleep(300);
        Nativo.RECT depois = Nativo.Retangulo(_hBuzzy);
        bool mesmoLugar = depois.Left == antes.Left && depois.Bottom == antes.Bottom;
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois do clique");
        (bool frente, _) = FocoNoReceptor(marcaR);
        Registrar(Criterio, flagra is not null && fim is not null && mesmoLugar,
            $"flagra={flagra is not null}; fim da reação={fim is not null}; no mesmo lugar={mesmoLugar} ({antes} → {depois}); receptor na frente depois={frente}");
    }

    // ------------------------------------------------------------------ C3: ir ver

    private bool F7IrVer(Topologia topologia)
    {
        const string Criterio = "C3 — ir ver (DEC-037, item 4): com o aplicativo em uso no outro monitor, ele vai até lá, pela travessia, e o foco continua no aplicativo";
        Nativo.RECT b = Nativo.Retangulo(_hBuzzy);
        var rb = new RetanguloPx(b.Left, b.Top, b.Right, b.Bottom);
        MonitorDoDesktop? dele = topologia.Monitores.FirstOrDefault(m => m.AreaUtil.Contem(rb));
        MonitorDoDesktop? outro = dele is null ? null : topologia.Monitores.FirstOrDefault(m => m.Chave != dele.Chave
            && (Passagens.PortaPlana(topologia, dele, -1, SpriteDoApp)?.ChaveVizinho == m.Chave || Passagens.PortaPlana(topologia, dele, +1, SpriteDoApp)?.ChaveVizinho == m.Chave));
        if (dele is null || outro is null)
        {
            Registrar(Criterio, NaoAplicavel, $"sem vizinho de mesmo chão ({topologia.Monitores.Count} monitor(es))");
            return false;
        }
        int marcaR = _logReceptor.Contar();
        long marca = LogDoBuzzy.Marca();
        Nativo.RECT j = Nativo.Retangulo(_hReceptor);
        MoverReceptor(outro.AreaUtil.Esquerda + (outro.AreaUtil.Largura - (j.Right - j.Left)) / 2, outro.AreaUtil.Base - (j.Bottom - j.Top));
        _rel.Linha($"   C3: receptor levado ao {outro.Chave} ({Nativo.Retangulo(_hReceptor)}); o Buzzy está no {dele.Chave}");
        EventoBuzzy? vai = LogDoBuzzy.Esperar(marca, e => Regra(e, "IDLE + CURIOSIDADE: vai ver o outro monitor"), 300000, () => _buzzy is { HasExited: true });
        bool chegou = vai is not null && EsperarAte(() =>
        {
            Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
            return outro.AreaUtil.Contem(new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom));
        }, 60000);
        int idas = LogDoBuzzy.Desde(marca).Count(e => Regra(e, "IDLE + CURIOSIDADE: vai ver o outro monitor"));
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        Registrar(Criterio, vai is not null && chegou && idas == 1 && frente && !desativou,
            $"decidiu ir={vai is not null}; chegou ao {outro.Chave}={chegou}; idas={idas}; janela final {Nativo.Retangulo(_hBuzzy)}; receptor na frente={frente}; desativado={desativou}");
        return chegou;
    }

    // ------------------------------------------------------------------ C4: a dispensa

    private void F7Dispensa(Topologia topologia, bool foi)
    {
        const string Criterio = "C4 — dispensa (DEC-037, item 6): arrastado e solto, ele fica onde foi solto, mesmo com o aplicativo em uso levado a outro monitor";
        Nativo.RECT b = Nativo.Retangulo(_hBuzzy);
        var rb = new RetanguloPx(b.Left, b.Top, b.Right, b.Bottom);
        MonitorDoDesktop? dele = topologia.Monitores.FirstOrDefault(m => m.AreaUtil.Contem(rb)) ?? topologia.Principal;
        long marca = LogDoBuzzy.Marca();
        var alvo = new PontoPx(dele.AreaUtil.Esquerda + dele.AreaUtil.Largura / 3, dele.AreaUtil.Base);
        bool solto = ArrastarAncoraPara(alvo, "DRAG_END");
        EventoBuzzy? dispensa = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["regra"] == "CURIOSIDADE: dispensada (DRAG_END)", 3000);
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois do arraste");
        MonitorDoDesktop? outro = topologia.Monitores.FirstOrDefault(m => m.Chave != dele.Chave);
        if (outro is not null)
        {
            Nativo.RECT j = Nativo.Retangulo(_hReceptor);
            MoverReceptor(outro.AreaUtil.Esquerda + (outro.AreaUtil.Largura - (j.Right - j.Left)) / 2, outro.AreaUtil.Base - (j.Bottom - j.Top));
        }
        Thread.Sleep(45000);
        int idas = LogDoBuzzy.Desde(marca).Count(e => Regra(e, "IDLE + CURIOSIDADE"));
        Nativo.RECT depois = Nativo.Retangulo(_hBuzzy);
        bool ficou = dele.AreaUtil.Contem(new RetanguloPx(depois.Left, depois.Top, depois.Right, depois.Bottom));
        Registrar(Criterio, solto && dispensa is not null && idas == 0 && ficou,
            $"solto={solto}; dispensa={dispensa is not null}; ações da curiosidade em 45 s={idas}; ficou no {dele.Chave}={ficou}; aplicativo levado ao {outro?.Chave ?? "nenhum outro"}; ida anterior={foi}");
    }

    // ------------------------------------------------------------------ C5: a contenção no log

    private void F7ContencaoNoLog()
    {
        const string Criterio = "C5 — contenção no log (DEC-037, itens 2 e 11): nenhuma linha por troca de foco, nada da janela (título, identificador) e o fim só com contagens, inclusive os vãos pedidos";
        List<EventoBuzzy> linhas = LogDoBuzzy.Desde(_inicioLogBuzzy);
        int doFoco = linhas.Count(e => e.Chave == "NUCLEO" && e["evento"] == "ForegroundMonitorChanged");
        string hwnd = ((long)_hReceptor).ToString(CultureInfo.InvariantCulture);
        string[] texto = File.ReadAllLines(LogDoBuzzy.Arquivo);
        bool vazou = texto.Any(l => l.Contains("Receptor de teste", StringComparison.OrdinalIgnoreCase) || l.Contains($"={hwnd}", StringComparison.Ordinal) || l.Contains("Buzzy.Verificacao", StringComparison.Ordinal));
        EventoBuzzy? fim = linhas.LastOrDefault(e => e.Chave == "TELA_CHEIA" && e["fim"] == "sim");
        string[] permitidos = ["fim", "eventosPrimeiroPlano", "eventosGeometria", "vaosPedidos"];
        bool fimLimpo = fim is not null && fim.Campos.Keys.All(permitidos.Contains) && long.TryParse(fim["vaosPedidos"], out long vaos) && vaos >= 1;
        Registrar(Criterio, doFoco == 0 && !vazou && fimLimpo, $"linhas do NUCLEO por troca de foco={doFoco}; título ou identificador do aplicativo no log={vazou}; linha do fim: {(fim is null ? "nenhuma" : string.Join(", ", fim.Campos.Select(c => $"{c.Key}={c.Value}")))}");
    }
}
