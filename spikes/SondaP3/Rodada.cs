using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SondaP3;

internal sealed record Veredito(string Cenario, string Resultado, string Detalhe);

// Abre o receptor, dá o foco a ele, abre o protótipo por cima e roda os cenários, digitando
// um marcador no receptor depois de cada gesto. A prova de que o foco ficou lá é o receptor
// receber cada marcador inteiro, com a janela ativa e a caixa focada.
// Input que não for da sonda invalida a rodada.
internal sealed class Rodada
{
    private readonly int _numero;
    private readonly string _exe;
    private readonly Relatorio _rel;
    private readonly Injetor _inj;
    private readonly LogArquivo _logSpike;
    private readonly LogArquivo _logReceptor;
    private readonly List<Veredito> _vereditos = [];
    private readonly StringBuilder _textoEsperado = new();

    private Process? _procReceptor;
    private Process? _procSpike;
    private nint _hReceptor;
    private nint _hSpike;
    private Nativo.POINT _alvoReceptor;
    private int _marcaSessaoReceptor;
    private int _marcaFoco = -1;
    private int _tempoClickLockMs;

    // Com interferência a limpeza não mexe no cursor.
    private bool _interferencia;

    // O receptor só loga [A-Za-z0-9;-] e troca o resto por '?', então os marcadores usam só
    // esse alfabeto e não há escape a desfazer.
    private static readonly Regex ReTexto = new(@"RECEPTOR\|TEXTO\|(?<t>.*)\|ativa=(?<a>\w+)\|foco=(?<f>\w+)$", RegexOptions.Compiled);

    internal Rodada(int numero, string exe, string resultados, Relatorio rel, uint ultimoInputAntes)
    {
        _numero = numero;
        _exe = exe;
        _rel = rel;
        _inj = new Injetor(() => Programa.Cancelado, ultimoInputAntes);
        _logSpike = new LogArquivo(Path.Combine(resultados, "p3.log"));
        _logReceptor = new LogArquivo(Path.Combine(resultados, "receptor.log"));
    }

    internal IReadOnlyList<Veredito> Vereditos => _vereditos;

    internal bool Executar()
    {
        bool clickLockOriginal = Nativo.ClickLock();
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        _tempoClickLockMs = Nativo.TempoClickLockMs();

        _rel.Linha("");
        _rel.Linha($"---- Rodada {_numero} — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ----");

        try
        {
            AbrirReceptor();
            AtivarReceptor("início");
            AbrirSpike();

            _marcaFoco = _logReceptor.Contar();
            Digitar("M1-inicio;");
            CenarioB7();
            Digitar("M2-clique;");
            CenarioB2();
            Digitar("M3-arraste;");
            CenarioB3();
            Digitar("M4-rapido;");
            CenarioB6Controle(clickLockOriginal);
            Digitar("M5-controle;");
            CenarioB6(clickLockOriginal);
            Digitar("M6-clicklock;");
            if (CenarioB5Ida(out Nativo.RECT origem))
            {
                Digitar("M7-monitor2;");
                CenarioB5Volta(origem);
                Digitar("M8-volta;");
            }
            FecharPeriodoDeFoco("do início até antes do Alt+Tab rápido");

            CenarioB4("B4a Alt+Tab rápido (4 teclas num lote)", segurado: false);

            // B4b é extra (Alt segurado, como gente faz). Se não der pra devolver o foco ao
            // receptor sem clicar em outra janela, fica INCONCLUSIVO, não falha.
            bool reativou;
            try
            {
                AtivarReceptor("reativação antes de B4b", permitirAltTab: true);
                reativou = true;
            }
            catch (FalhaDeTeste e)
            {
                Registrar("B4b Alt+Tab segurado", "INCONCLUSIVO", "não executado: " + e.Message);
                reativou = false;
            }

            if (reativou)
            {
                _marcaFoco = _logReceptor.Contar();
                Digitar("M9-reativado;");
                FecharPeriodoDeFoco("da reativação até antes do Alt+Tab segurado");
                CenarioB4("B4b Alt+Tab segurado (Alt por 500 ms, seletor visível)", segurado: true);
            }
        }
        catch (Interferencia e)
        {
            _interferencia = true;
            Registrar("EXECUÇÃO", "INVÁLIDA", "interferência humana detectada: " + e.Message);
        }
        catch (Exception e)
        {
            Registrar("EXECUÇÃO", "FALHOU", $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            Limpeza(clickLockOriginal, cursorOriginal);
        }

        VerificarReceptorAoFinal();
        return Resumir();
    }

    // ------------------------------------------------------------------ abertura

    private Process Iniciar(string modo, int x, int y)
    {
        var psi = new ProcessStartInfo(_exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_exe)!,
        };
        psi.ArgumentList.Add("--modo");
        psi.ArgumentList.Add(modo);
        psi.ArgumentList.Add("--x");
        psi.ArgumentList.Add(x.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--y");
        psi.ArgumentList.Add(y.ToString(CultureInfo.InvariantCulture));
        return Process.Start(psi) ?? throw new FalhaDeTeste($"Não foi possível iniciar o modo {modo}.");
    }

    private static nint Hwnd(string linha) => (nint)long.Parse(linha[(linha.IndexOf("SONDA|HWND|", StringComparison.Ordinal) + 11)..].Trim(), CultureInfo.InvariantCulture);

    private void AbrirReceptor()
    {
        // No monitor principal, longe das bordas; o protótipo abre dentro dele.
        _marcaSessaoReceptor = _logReceptor.Contar();
        _procReceptor = Iniciar("receptor", 200, 150);

        string linhaHwnd = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "SONDA|HWND|", 15000)
            ?? throw new FalhaDeTeste("O receptor não registrou o HWND em 15 s.");
        _hReceptor = Hwnd(linhaHwnd);

        string linhaAlvo = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "SONDA|ALVO|", 5000)
            ?? throw new FalhaDeTeste("O receptor não registrou o ponto de ativação.");
        string[] partes = linhaAlvo[(linhaAlvo.IndexOf("SONDA|ALVO|", StringComparison.Ordinal) + 11)..].Split('|');
        _alvoReceptor = new Nativo.POINT(int.Parse(partes[0], CultureInfo.InvariantCulture), int.Parse(partes[1], CultureInfo.InvariantCulture));

        _ = _logReceptor.EsperarLinha(_marcaSessaoReceptor, "RECEPTOR|PRONTO", 5000)
            ?? throw new FalhaDeTeste("O receptor não ficou pronto.");

        _rel.Linha($"   receptor: pid {_procReceptor.Id}, hwnd {_hReceptor}, retângulo {Nativo.Retangulo(_hReceptor)}, ponto de ativação {_alvoReceptor}");
    }

    // Nunca clica em outra janela. Se o ponto de ativação é do receptor, clica nele; senão,
    // só com permitirAltTab e se outra janela o cobre (a que o Alt+Tab do B4a trouxe), manda um
    // Alt+Tab de volta, como uma pessoa faria. O primeiro plano é conferido depois aqui e de
    // novo em Digitar, antes de qualquer tecla.
    private void AtivarReceptor(string motivo, bool permitirAltTab = false)
    {
        // Sobe na ordem Z sem ativar. O Windows pode recusar passar da janela em primeiro plano,
        // por isso a conferência abaixo.
        Nativo.SetWindowPos(_hReceptor, 0, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
        Thread.Sleep(250);

        if (Nativo.GetForegroundWindow() == _hReceptor)
        {
            _rel.Linha($"   receptor já em primeiro plano ({motivo}); nenhuma ação de ativação necessária");
            return;
        }

        if (Nativo.Raiz(Nativo.WindowFromPoint(_alvoReceptor)) == _hReceptor)
        {
            _inj.EsperarIntervaloDeCliqueDuplo();
            GarantirAlvo(_alvoReceptor, _hReceptor, $"ativar o receptor ({motivo})");
            _inj.Descer(_alvoReceptor.X, _alvoReceptor.Y);
            Pausa(50);
            _inj.Subir(_alvoReceptor.X, _alvoReceptor.Y);
            if (!EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 3000))
                throw new FalhaDeTeste($"O receptor não ficou em primeiro plano depois do clique de ativação ({motivo}).");
            Pausa(300);
            _rel.Linha($"   receptor ativado por clique sintético no próprio receptor ({motivo})");
            return;
        }

        if (!permitirAltTab)
            throw new FalhaDeTeste($"ativar o receptor ({motivo}): o ponto {_alvoReceptor} está coberto por outra janela. Nada foi clicado.");

        // Confere de novo logo antes: se o receptor já estiver na frente, o Alt+Tab levaria o
        // foco embora pro app do usuário.
        nint frente = Nativo.GetForegroundWindow();
        nint donoDoPonto = Nativo.Raiz(Nativo.WindowFromPoint(_alvoReceptor));
        if (frente == _hReceptor)
        {
            _rel.Linha($"   receptor passou ao primeiro plano sem ação da sonda ({motivo}); Alt+Tab não enviado");
            return;
        }
        if (frente == 0 || donoDoPonto == 0 || donoDoPonto == _hReceptor)
        {
            throw new FalhaDeTeste(
                $"ativar o receptor ({motivo}): condições do Alt+Tab de volta não confirmadas (frente: {Quem(frente)}; "
                + $"o ponto {_alvoReceptor} pertence a: {Quem(donoDoPonto)}). Nada foi enviado.");
        }

        _inj.AltTabRapido();

        if (!EsperarAte(ReceptorNaFrente, 3000))
            throw new FalhaDeTeste($"ativar o receptor ({motivo}): o Alt+Tab de volta não trouxe o receptor ao primeiro plano (frente: {Quem(Nativo.GetForegroundWindow())}).");
        Pausa(300);
        _rel.Linha($"   receptor reativado por Alt+Tab sintético de volta ({motivo}); o ponto de ativação estava coberto por outra janela");
    }

    private void AbrirSpike()
    {
        Nativo.RECT rr = Nativo.Retangulo(_hReceptor);
        int x = rr.Left + 500;
        int y = rr.Top + 180;

        nint frenteAntes = Nativo.GetForegroundWindow();
        int marcaReceptor = _logReceptor.Contar();
        int marcaSpike = _logSpike.Contar();

        _procSpike = Iniciar("p3", x, y);
        string linhaHwnd = _logSpike.EsperarLinha(marcaSpike, "SONDA|HWND|", 15000)
            ?? throw new FalhaDeTeste("O protótipo não registrou o HWND em 15 s.");
        _hSpike = Hwnd(linhaHwnd);
        _ = _logSpike.EsperarLinha(marcaSpike, "P3 pronto", 5000)
            ?? throw new FalhaDeTeste("O protótipo não ficou pronto.");
        Pausa(700);

        nint frenteDepois = Nativo.GetForegroundWindow();
        bool receptorDesativou = _logReceptor.Desde(marcaReceptor).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        bool a1 = frenteAntes == _hReceptor && frenteDepois == _hReceptor && !receptorDesativou;
        Registrar("A1 protótipo aparece sem roubar o foco", a1,
            $"frente antes={Quem(frenteAntes)} depois={Quem(frenteDepois)}; receptor desativou={receptorDesativou}");

        long ex = Nativo.GetWindowLongPtr(_hSpike, Nativo.GWL_EXSTYLE);
        bool noActivate = (ex & Nativo.WS_EX_NOACTIVATE) != 0;
        bool layered = (ex & Nativo.WS_EX_LAYERED) != 0;
        bool tool = (ex & Nativo.WS_EX_TOOLWINDOW) != 0;
        bool topmost = (ex & Nativo.WS_EX_TOPMOST) != 0;
        Registrar("A3 estilos da janela", noActivate && layered && tool && topmost,
            $"0x{ex:X8}: NOACTIVATE={noActivate} LAYERED={layered} TOOLWINDOW={tool} TOPMOST={topmost}");

        _rel.Linha($"   protótipo: pid {_procSpike.Id}, hwnd {_hSpike}, retângulo {Nativo.Retangulo(_hSpike)}");
    }

    // ------------------------------------------------------------------ texto e foco

    private (string Texto, bool TodosComFoco, int Eventos) TextoDesde(int marca)
    {
        var sb = new StringBuilder();
        bool todos = true;
        int n = 0;
        foreach (string l in _logReceptor.Desde(marca))
        {
            Match m = ReTexto.Match(l);
            if (!m.Success) continue;
            n++;
            sb.Append(m.Groups["t"].Value);
            todos &= m.Groups["a"].Value == "True" && m.Groups["f"].Value == "True";
        }
        return (sb.ToString(), todos, n);
    }

    // O que o receptor loga sem máscara.
    private static bool CaractereDeMarcador(char c) => char.IsAsciiLetterOrDigit(c) || c is ';' or '-';

    // Só digita com o receptor na frente, e confere no log dele que tudo chegou, em ordem, com
    // foco. SendInput aceitar não quer dizer que chegou.
    private void Digitar(string marcador)
    {
        // Fora do alfabeto o caractere viraria '?' no log e nunca bateria.
        if (!marcador.All(CaractereDeMarcador))
            throw new FalhaDeTeste($"marcador '{marcador}' fora do alfabeto [A-Za-z0-9;-] que o receptor registra; nada foi digitado.");

        if (Nativo.GetForegroundWindow() != _hReceptor)
        {
            Registrar($"texto '{marcador}'", "FALHOU", $"não digitado: a janela da frente era {Quem(Nativo.GetForegroundWindow())}, não o receptor");
            throw new FalhaDeTeste($"Receptor fora do primeiro plano antes de '{marcador}'; nada foi digitado.");
        }

        int marca = _logReceptor.Contar();
        _inj.Digitar(marcador);
        _textoEsperado.Append(marcador);

        EsperarAte(() => TextoDesde(marca).Texto.Length >= marcador.Length, 3000);
        Pausa(150);
        var (texto, todosComFoco, eventos) = TextoDesde(marca);
        bool ok = texto == marcador && todosComFoco;
        Registrar($"texto '{marcador}' entregue ao receptor", ok,
            $"recebido '{texto}' em {eventos} eventos; todos com janela ativa e foco na caixa={todosComFoco}");
    }

    private void FecharPeriodoDeFoco(string nome)
    {
        List<string> ls = _logReceptor.Desde(_marcaFoco);
        int desativacoes = ls.Count(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        int perdasTeclado = ls.Count(l => l.Contains("RECEPTOR|FOCO_TECLADO|nao", StringComparison.Ordinal));
        int cliques = ls.Count(l => l.Contains("RECEPTOR|CLIQUE|", StringComparison.Ordinal));
        Registrar($"foco mantido no receptor ({nome})", desativacoes == 0 && perdasTeclado == 0 && cliques == 0,
            $"desativações={desativacoes} perdas de foco de teclado={perdasTeclado} cliques que caíram no receptor={cliques} ({ls.Count} linhas do receptor no período)");
    }

    // ------------------------------------------------------------------ gestos

    private static Nativo.POINT Agarre(Nativo.RECT r) => new(r.Left + 20, r.Top + 100);

    private static bool Deslocou(Nativo.RECT de, Nativo.RECT para, int dx, int dy)
        => para.Left == de.Left + dx && para.Top == de.Top + dy && para.Largura == de.Largura && para.Altura == de.Altura;

    private bool ReceptorNaFrente() => Nativo.GetForegroundWindow() == _hReceptor;

    private static void GarantirAlvo(Nativo.POINT p, nint esperado, string oque)
    {
        nint dono = Nativo.Raiz(Nativo.WindowFromPoint(p));
        if (dono != esperado)
            throw new FalhaDeTeste($"{oque}: o ponto {p} pertence à janela {dono}, não à esperada {esperado}. Nada foi clicado.");
    }

    private AnaliseGesto Gesto(string nome, Nativo.POINT agarre, Action acao, int fins)
    {
        _inj.EsperarIntervaloDeCliqueDuplo();
        GarantirAlvo(agarre, _hSpike, nome);
        int marca = _logSpike.Contar();
        acao();
        _logSpike.EsperarAte(marca, ls => ls.Count(l => l.Contains("Motivo do término", StringComparison.Ordinal)) >= fins, 4000);
        Pausa(250);
        return AnaliseGesto.De(_logSpike.Desde(marca));
    }

    private void CenarioB7()
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        AnaliseGesto a = Gesto("B7", g, () =>
        {
            _inj.Descer(g.X, g.Y); Pausa(60);
            _inj.Mover(g.X + 1, g.Y); Pausa(60);
            _inj.Subir(g.X + 1, g.Y); Pausa(400);
        }, fins: 1);
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool ok = a.Pressionares == 1 && a.SoltarClique == 1 && a.SoltarArraste == 0
            && a.ArrastesIniciados == 0 && a.ArrastesDetectadosNoSoltar == 0 && a.CapturasPerdidas == 0
            && a.Fins == 1 && a.CapturaZeroNoFim && a.Problemas == 0 && Deslocou(r0, r1, 0, 0) && frente;
        Registrar("B7 clique curto (1 px, abaixo do limiar) vira CLIQUE", ok,
            $"{a.Resumo()}; janela {r0} -> {r1}; receptor na frente={frente}");
    }

    private void CenarioB2()
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        AnaliseGesto a = Gesto("B2", g, () =>
        {
            _inj.Descer(g.X, g.Y); Pausa(60);
            for (int i = 1; i <= 12; i++) { _inj.Mover(g.X + 12 * i, g.Y); Pausa(30); }
            _inj.Subir(g.X + 144, g.Y); Pausa(400);
        }, fins: 1);
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool seguiu = Deslocou(r0, r1, 144, 0);
        bool posLog = a.PosicaoFinal is { } p && p.X == r1.Left && p.Y == r1.Top;
        bool ok = a.Pressionares == 1 && a.ArrastesIniciados == 1 && a.SoltarArraste == 1 && a.SoltarClique == 0
            && a.CapturasPerdidas == 0 && a.Fins == 1 && a.Motivos[0] == "soltou depois de arrastar"
            && a.CapturaZeroNoFim && a.Problemas == 0 && seguiu && posLog && frente;
        Registrar("B2 arraste de 144 px acompanha o cursor e não rouba o foco", ok,
            $"{a.Resumo()}; janela {r0} -> {r1} (esperado +144,0: {seguiu}); receptor na frente={frente}; {a.Latencia}");
    }

    private void CenarioB3()
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        const int dx = -320, dy = 40;
        AnaliseGesto a = Gesto("B3", g, () =>
        {
            _inj.Descer(g.X, g.Y); Pausa(60);
            // Salto de 320 px e soltar no mesmo lote: solta fora da janela antes dela alcançar o cursor.
            _inj.Subir(g.X + dx, g.Y + dy); Pausa(500);
        }, fins: 1);
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool seguiu = Deslocou(r0, r1, dx, dy);
        bool soltouFora = g.X + dx < r0.Left || g.X + dx >= r0.Right;
        bool ok = a.Pressionares == 1 && (a.ArrastesIniciados + a.ArrastesDetectadosNoSoltar) == 1
            && a.SoltarArraste == 1 && a.SoltarClique == 0 && a.CapturasPerdidas == 0 && a.Fins == 1
            && a.CapturaZeroNoFim && a.Problemas == 0 && seguiu && soltouFora && frente;
        string caminho = a.ArrastesDetectadosNoSoltar == 1 ? "o soltar chegou sem movimento intermediário" : "o movimento chegou antes do soltar";
        Registrar("B3 arraste rápido, soltando fora da janela", ok,
            $"{a.Resumo()}; {caminho}; janela {r0} -> {r1} (esperado {dx},{dy}: {seguiu}); soltou fora do retângulo anterior={soltouFora}; receptor na frente={frente}");
    }

    private void CenarioB6Controle(bool clickLockOriginal)
    {
        // Mesmo gesto do B6 com ClickLock desligado: o soltar chega (vira CLIQUE) e nada arrasta depois.
        if (Nativo.ClickLock()) Nativo.DefinirClickLock(false);
        try
        {
            bool desligado = !Nativo.ClickLock();
            int segurar = _tempoClickLockMs + 600;
            Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
            Nativo.POINT g = Agarre(r0);
            AnaliseGesto a = Gesto("B6-controle", g, () =>
            {
                _inj.Descer(g.X, g.Y); Pausa(segurar);
                _inj.Subir(g.X, g.Y); Pausa(400);
                for (int i = 1; i <= 10; i++) { _inj.Mover(g.X - 12 * i, g.Y); Pausa(40); }
                Pausa(300);
                _inj.Mover(g.X, g.Y); Pausa(200);
            }, fins: 1);
            Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
            bool frente = ReceptorNaFrente();
            bool ok = desligado && a.Pressionares == 1 && a.SoltarClique == 1 && a.SoltarArraste == 0
                && a.ArrastesIniciados == 0 && a.ArrastesDetectadosNoSoltar == 0 && a.Fins == 1
                && a.CapturaZeroNoFim && a.Problemas == 0 && Deslocou(r0, r1, 0, 0) && frente;
            Registrar($"B6-controle sem ClickLock: segurar {segurar} ms e soltar vira CLIQUE; nada arrasta depois", ok,
                $"ClickLock desligado={desligado}; {a.Resumo()}; janela {r0} -> {r1}; receptor na frente={frente}");
        }
        finally
        {
            Nativo.DefinirClickLock(clickLockOriginal);
        }
    }

    private void CenarioB6(bool clickLockOriginal)
    {
        int segurar = _tempoClickLockMs + 600;
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        bool ligou;
        AnaliseGesto a;

        Nativo.DefinirClickLock(true);
        try
        {
            ligou = Nativo.ClickLock();
            if (!ligou)
            {
                Registrar("B6 ClickLock", "INCONCLUSIVO", "não foi possível ligar o ClickLock em memória");
                return;
            }

            a = Gesto("B6", g, () =>
            {
                _inj.Descer(g.X, g.Y); Pausa(segurar);
                _inj.Subir(g.X, g.Y);            // a trava deve engolir este soltar
                Pausa(400);
                for (int i = 1; i <= 10; i++) { _inj.Mover(g.X - 12 * i, g.Y); Pausa(40); }
                Pausa(300);

                // Clique que libera a trava. Se a trava segurou o arraste, a janela veio junto e
                // o ponto está sobre ela; senão, volta pra cima da janela parada.
                var liberar = new Nativo.POINT(g.X - 120, g.Y);
                if (Nativo.Raiz(Nativo.WindowFromPoint(liberar)) != _hSpike)
                {
                    GarantirAlvo(g, _hSpike, "B6: liberação sobre a janela parada");
                    _inj.Mover(g.X, g.Y);
                    liberar = g;
                }
                _inj.Descer(liberar.X, liberar.Y); Pausa(60);
                _inj.Subir(liberar.X, liberar.Y); Pausa(600);
            }, fins: 1);
        }
        finally
        {
            Nativo.DefinirClickLock(clickLockOriginal);
        }

        bool restaurado = Nativo.ClickLock() == clickLockOriginal;
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool seguiu = Deslocou(r0, r1, -120, 0);
        bool ok = a.Pressionares == 1 && a.SoltarClique == 0 && a.ArrastesIniciados == 1 && a.SoltarArraste == 1
            && a.CapturasPerdidas == 0 && a.Fins == 1 && a.CapturaZeroNoFim && a.Problemas == 0
            && seguiu && frente && restaurado;
        Registrar($"B6 ClickLock (em memória): o soltar após {segurar} ms é engolido e o mesmo gesto segue até o clique de liberação", ok,
            $"{a.Resumo()}; janela {r0} -> {r1} (esperado -120,0: {seguiu}); receptor na frente={frente}; ClickLock restaurado ao original ({clickLockOriginal})={restaurado}");
    }

    private static Nativo.MONITORINFOEX? MonitorSecundario()
    {
        Nativo.MONITORINFOEX? principal = Nativo.Monitor(new Nativo.POINT(0, 0));
        if (principal is not { } p) return null;
        int cy = (p.rcMonitor.Top + p.rcMonitor.Bottom) / 2;
        int cx = (p.rcMonitor.Left + p.rcMonitor.Right) / 2;
        // Esquerda primeiro: coordenada negativa é o caso que interessa.
        Nativo.POINT[] candidatos =
        [
            new(p.rcMonitor.Left - 50, cy),
            new(p.rcMonitor.Right + 50, cy),
            new(cx, p.rcMonitor.Top - 50),
            new(cx, p.rcMonitor.Bottom + 50),
        ];
        foreach (Nativo.POINT c in candidatos)
        {
            if (Nativo.Monitor(c) is { } m && (m.dwFlags & Nativo.MONITORINFOF_PRIMARY) == 0) return m;
        }
        return null;
    }

    // Passos de 16 px, conferindo que a janela acompanha. O agarre fica 20 px dentro da borda,
    // então se ela parar o cursor ainda está em cima dela e o botão é solto ali, nunca sobre
    // janela de outro app.
    private void ArrastarEmPassos(Nativo.POINT de, Nativo.POINT para)
    {
        const int passo = 16;
        _inj.Descer(de.X, de.Y); Pausa(60);
        int x = de.X, y = de.Y;
        while (Math.Abs(para.X - x) > passo || Math.Abs(para.Y - y) > passo)
        {
            x += Math.Clamp(para.X - x, -passo, passo);
            y += Math.Clamp(para.Y - y, -passo, passo);
            _inj.Mover(x, y);
            int cx = x, cy = y;
            bool acompanhou = EsperarAte(() =>
            {
                Nativo.RECT r = Nativo.Retangulo(_hSpike);
                return r.Left == cx - 20 && r.Top == cy - 100;
            }, 250);
            if (!acompanhou)
            {
                _inj.Subir(x, y);
                throw new FalhaDeTeste($"A janela parou de acompanhar o cursor em ({x},{y}); botão solto sobre o protótipo e arraste abortado.");
            }
            Pausa(8);
        }
        _inj.Subir(para.X, para.Y); Pausa(500);
    }

    private bool CenarioB5Ida(out Nativo.RECT origem)
    {
        origem = Nativo.Retangulo(_hSpike);
        if (MonitorSecundario() is not { } sec)
        {
            Registrar("B5 arraste entre monitores", "N/A", "só há um monitor; sem hardware para B5");
            return false;
        }

        Nativo.RECT r0 = origem;
        Nativo.POINT g = Agarre(r0);
        var destino = new Nativo.POINT((sec.rcWork.Left + sec.rcWork.Right) / 2, Math.Clamp(g.Y, sec.rcWork.Top + 120, sec.rcWork.Bottom - 120));
        AnaliseGesto a = Gesto("B5 ida", g, () => ArrastarEmPassos(g, destino), fins: 1);
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool seguiu = Deslocou(r0, r1, destino.X - g.X, destino.Y - g.Y);
        bool noSecundario = a.MonitorFinal?.StartsWith(sec.szDevice, StringComparison.Ordinal) == true;
        bool ok = a.Pressionares == 1 && a.ArrastesIniciados == 1 && a.SoltarArraste == 1 && a.CapturasPerdidas == 0
            && a.Fins == 1 && a.CapturaZeroNoFim && a.Problemas == 0 && seguiu && noSecundario && frente;
        Registrar($"B5 arraste até o monitor {sec.szDevice} {sec.rcMonitor}", ok,
            $"{a.Resumo()}; janela {r0} -> {r1} (seguiu o cursor: {seguiu}); monitor final: {a.MonitorFinal}; receptor na frente={frente}");
        return true;
    }

    private void CenarioB5Volta(Nativo.RECT origem)
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        Nativo.POINT destino = Agarre(origem);
        AnaliseGesto a = Gesto("B5 volta", g, () => ArrastarEmPassos(g, destino), fins: 1);
        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        bool frente = ReceptorNaFrente();
        bool voltou = Deslocou(origem, r1, 0, 0);
        bool ok = a.Pressionares == 1 && a.ArrastesIniciados == 1 && a.SoltarArraste == 1 && a.CapturasPerdidas == 0
            && a.Fins == 1 && a.CapturaZeroNoFim && a.Problemas == 0 && voltou && frente;
        Registrar("B5 volta ao monitor principal", ok,
            $"{a.Resumo()}; janela {r0} -> {r1} (de volta à origem {origem}: {voltou}); receptor na frente={frente}");
    }

    private void CenarioB4(string nome, bool segurado)
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        nint frenteAntes = Nativo.GetForegroundWindow();
        int marcaReceptor = _logReceptor.Contar();

        AnaliseGesto a = Gesto(nome, g, () =>
        {
            _inj.Descer(g.X, g.Y); Pausa(60);
            for (int i = 1; i <= 6; i++) { _inj.Mover(g.X + 12 * i, g.Y); Pausa(30); }
            if (segurado) _inj.AltTabSegurado(500); else _inj.AltTabRapido();
            Pausa(700);
            for (int i = 7; i <= 10; i++) { _inj.Mover(g.X + 12 * i, g.Y); Pausa(30); }
            _inj.Subir(g.X + 120, g.Y); Pausa(600);
        }, fins: 1);

        Nativo.RECT r1 = Nativo.Retangulo(_hSpike);
        nint frenteDepois = Nativo.GetForegroundWindow();
        bool receptorDesativou = _logReceptor.Desde(marcaReceptor).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        bool altTabTeveEfeito = frenteAntes == _hReceptor && frenteDepois != _hReceptor && receptorDesativou;
        bool perdeu = a.PerdaAntesDeSoltar;
        int dxEsperado = perdeu ? 72 : 120;
        bool posicaoCoerente = Deslocou(r0, r1, dxEsperado, 0);
        bool ok = a.Pressionares == 1 && a.Fins == 1 && a.CapturaZeroNoFim && a.Problemas == 0
            && frenteDepois != _hSpike && posicaoCoerente;
        string como = perdeu
            ? "a captura foi perdida no Alt+Tab e o gesto terminou como cancelamento, onde estava"
            : "a captura foi mantida e o gesto terminou ao soltar";
        string resultado = !altTabTeveEfeito ? "INCONCLUSIVO" : ok ? "OK" : "FALHOU";
        Registrar($"{nome} no meio do arraste", resultado,
            $"{como}; {a.Resumo()}; janela {r0} -> {r1} (esperado +{dxEsperado},0: {posicaoCoerente}); "
            + $"frente antes={Quem(frenteAntes)} depois={Quem(frenteDepois)}; Alt+Tab teve efeito={altTabTeveEfeito}; protótipo na frente={frenteDepois == _hSpike}");

        CliqueDepois($"{nome.Split(' ')[0]}: clique normal logo depois (o protótipo não ficou travado)");
    }

    private void CliqueDepois(string nome)
    {
        Nativo.RECT r0 = Nativo.Retangulo(_hSpike);
        Nativo.POINT g = Agarre(r0);
        AnaliseGesto a = Gesto(nome, g, () =>
        {
            _inj.Descer(g.X, g.Y); Pausa(60);
            _inj.Subir(g.X, g.Y); Pausa(400);
        }, fins: 1);
        bool protoNaFrente = Nativo.GetForegroundWindow() == _hSpike;
        bool ok = a.Pressionares == 1 && a.SoltarClique == 1 && a.ArrastesIniciados == 0 && a.Fins == 1
            && a.CapturaZeroNoFim && a.Problemas == 0 && !protoNaFrente;
        Registrar(nome, ok, $"{a.Resumo()}; protótipo na frente={protoNaFrente}");
    }

    // ------------------------------------------------------------------ fim

    // Solta o que ficou abaixado, devolve o ClickLock e fecha os processos. O cursor só volta
    // pro lugar se ninguém mais mexeu; depois de interferência o mouse é do usuário.
    private void Limpeza(bool clickLockOriginal, Nativo.POINT cursorOriginal)
    {
        // Antes de soltar as pendências, porque a própria limpeza injeta eventos.
        bool usuarioAssumiu = _interferencia || UsuarioUsouOComputador("depois da última injeção");

        List<string> pendencias = _inj.SoltarPendencias();
        if (pendencias.Count > 0) _rel.Linha("   limpeza de input: " + string.Join("; ", pendencias));

        Nativo.DefinirClickLock(clickLockOriginal);
        bool clickLock = Nativo.ClickLock() == clickLockOriginal;

        // De novo, logo antes de mover o cursor.
        usuarioAssumiu = usuarioAssumiu || UsuarioUsouOComputador("durante a limpeza");
        Nativo.POINT c;
        string resultadoCursor, detalheCursor;
        if (usuarioAssumiu)
        {
            c = Nativo.Cursor();
            resultadoCursor = "PULADO";
            string porque = _interferencia
                ? "houve interferência: o usuário assumiu o mouse ou o teclado"
                : "GetLastInputInfo falhou e não há como saber se alguém está usando o mouse";
            detalheCursor = $"não reposicionado de propósito ({porque}); original={cursorOriginal} agora={c}";
        }
        else
        {
            Nativo.SetCursorPos(cursorOriginal.X, cursorOriginal.Y);
            Thread.Sleep(80);
            c = Nativo.Cursor();
            bool voltou = Math.Abs(c.X - cursorOriginal.X) <= 1 && Math.Abs(c.Y - cursorOriginal.Y) <= 1;
            resultadoCursor = voltou ? "OK" : "FALHOU";
            detalheCursor = $"original={cursorOriginal} agora={c}";
        }

        string spike = Fechar(_procSpike, _hSpike);
        string receptor = Fechar(_procReceptor, _hReceptor);

        Registrar("limpeza: ClickLock no valor original", clickLock, $"original={clickLockOriginal} agora={Nativo.ClickLock()}");
        Registrar("limpeza: cursor de volta à posição original", resultadoCursor, detalheCursor);
        Registrar("limpeza: processos de teste encerrados", spike.StartsWith("encerrado", StringComparison.Ordinal) && receptor.StartsWith("encerrado", StringComparison.Ordinal),
            $"protótipo: {spike}; receptor: {receptor}");
    }

    // Se houve input alheio, marca a rodada INVÁLIDA (uma vez) e devolve true. Se o
    // GetLastInputInfo falhar, devolve true sem invalidar: na dúvida, não mexe no mouse.
    private bool UsuarioUsouOComputador(string quando)
    {
        int? depois = _inj.InputDepoisDaSondaMs();
        if (depois is null) return true;
        if (depois <= Injetor.ToleranciaInputMs) return false;
        if (!_interferencia)
        {
            _interferencia = true;
            Registrar("EXECUÇÃO", "INVÁLIDA",
                $"interferência humana detectada {quando}: houve input do mouse ou do teclado {depois} ms depois do último evento "
                + "injetado pela sonda; os resultados registrados por último podem ter sido afetados");
        }
        return true;
    }

    private static string Fechar(Process? proc, nint hwnd)
    {
        if (proc is null) return "encerrado (não chegou a abrir)";
        try
        {
            if (!proc.HasExited && hwnd != 0 && Nativo.IsWindow(hwnd))
                Nativo.PostMessage(hwnd, Nativo.WM_CLOSE, 0, 0);
            if (proc.WaitForExit(5000)) return $"encerrado por WM_CLOSE (pid {proc.Id}, código {proc.ExitCode})";
            proc.Kill();
            return proc.WaitForExit(3000) ? $"encerrado À FORÇA depois de 5 s (pid {proc.Id})" : $"NÃO ENCERROU (pid {proc.Id})";
        }
        catch (Exception e)
        {
            return $"erro ao encerrar: {e.Message}";
        }
    }

    private void VerificarReceptorAoFinal()
    {
        if (_procReceptor is null) return;
        _logReceptor.EsperarLinha(_marcaSessaoReceptor, "RECEPTOR|CONTEUDO_FINAL|", 3000);
        List<string> ls = _logReceptor.Desde(_marcaSessaoReceptor);
        var (texto, todosComFoco, eventos) = TextoDesde(_marcaSessaoReceptor);
        string esperado = _textoEsperado.ToString();
        string? linhaFinal = ls.LastOrDefault(l => l.Contains("RECEPTOR|CONTEUDO_FINAL|", StringComparison.Ordinal));
        string? conteudo = linhaFinal is null ? null : linhaFinal[(linhaFinal.IndexOf("RECEPTOR|CONTEUDO_FINAL|", StringComparison.Ordinal) + 24)..];
        Registrar("receptor: todo o texto chegou, na ordem, só com foco", texto == esperado && todosComFoco && conteudo == esperado,
            $"esperado '{esperado}'; recebido '{texto}' em {eventos} eventos; conteúdo final da caixa '{conteudo}'; todos com foco={todosComFoco}");
    }

    internal bool Invalidada => _vereditos.Any(v => v.Resultado == "INVÁLIDA");

    private bool Resumir()
    {
        int ok = _vereditos.Count(v => v.Resultado == "OK");
        int na = _vereditos.Count(v => v.Resultado == "N/A");
        int ruins = _vereditos.Count - ok - na;
        _rel.Linha($"   Rodada {_numero}: {ok} OK, {na} N/A, {ruins} não OK. Maior deriva do cursor tolerada: {_inj.MaiorDeriva} px.");
        return ruins == 0;
    }

    private void Registrar(string cenario, bool ok, string detalhe) => Registrar(cenario, ok ? "OK" : "FALHOU", detalhe);

    private void Registrar(string cenario, string resultado, string detalhe)
    {
        _vereditos.Add(new Veredito(cenario, resultado, detalhe));
        _rel.Linha($"   [{resultado}] {cenario}");
        _rel.Linha($"          {detalhe}");
    }

    private string Quem(nint h)
    {
        if (h == 0) return "nenhuma";
        if (h == _hReceptor) return "receptor";
        if (h == _hSpike) return "PROTÓTIPO";
        return "outra janela (não identificada de propósito)";
    }

    private static void Pausa(int ms)
    {
        if (Programa.Cancelado) throw new FalhaDeTeste("Execução cancelada.");
        Thread.Sleep(ms);
    }

    private static bool EsperarAte(Func<bool> condicao, int limiteMs)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (!condicao())
        {
            if (DateTime.UtcNow > fim) return false;
            Thread.Sleep(50);
        }
        return true;
    }
}
