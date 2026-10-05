using System.Diagnostics;
using System.Globalization;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética dos critérios [MANUAL] da Fase 3 — input e arraste (TODO.md). Abre o
/// mesmo receptor da Fase 1 (o "aplicativo do usuário", com o foco), o Buzzy por cima, e exercita
/// clique, clique duplo, arraste, soltar no ar, arraste até outro monitor, menu pelo botão
/// direito e Alt+Tab e tecla Windows no meio do arraste. Todo input é SINTÉTICO (SendInput); a
/// janela de UAC e as escalas diferentes ficam pendentes de verificação manual real ou [HW].
/// A partida repete, como regressão, os critérios 1, 2 e 7 da Fase 1, e o fim, o critério 10.
/// </summary>
internal sealed partial class Verificacao
{
    private const ushort VK_TAB = 0x09, VK_MENU = 0x12, VK_LWIN = 0x5B;

    /// <summary>Movimento dentro do limiar de arraste: 2 px, abaixo do menor SM_CXDRAG possível (4 a 96 DPI).</summary>
    private const int DentroDoLimiarPx = 2;

    private string _coberturaClickLock = "não executado";
    private string _coberturaTeclaWindows = "não executado";

    internal Sumario ExecutarFase3()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            _prefixo = "Fase 1 (regressão) — ";
            AbrirBuzzy();
            _prefixo = "Fase 3 — ";

            _marcaFoco = _logReceptor.Contar();
            Digitar("G1-inicio;");
            F3Clique();
            Digitar("G2-clique;");
            F3CliqueDuplo();
            Digitar("G3-duplo;");
            F3ArrasteAcompanhaOCursor();
            Digitar("G4-arraste;");
            F3SoltarNoAr();
            Digitar("G5-ar;");
            F3ArrasteAteOutroMonitor();
            Digitar("G6-monitor2;");
            // O menu dá o primeiro plano ao dono dele de propósito: o período sem nenhuma perda de
            // foco (cliques e arrastes) fecha antes.
            FecharPeriodoDeFoco("do início até o menu pelo botão direito: cliques e arrastes");
            F3BotaoDireitoAbreOMenu();
            Digitar("G7-menu;");

            F3ClickLock();
            F3AltTabNoMeioDoArraste();
            Digitar("G8-alttab;");
            F3TeclaWindowsNoMeioDoArraste();
            Digitar("G9-windows;");

            _prefixo = "Fase 1 (regressão) — ";
            C10SairPeloMenu();
            _prefixo = "";
            Digitar("G10-fim;");

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
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        RegistrarCoberturaFase3();
        return Resumir();
    }

    // ------------------------------------------------------------------ cenários

    /// <summary>Critérios 3 e 6: soltar sem passar do limiar é clique; o personagem não se move nem rouba o foco.</summary>
    private void F3Clique()
    {
        Nativo.POINT p = ExigirPontoOpaco("clique");
        Nativo.RECT antes = Nativo.Retangulo(_hBuzzy);
        long marca = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();

        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        _inj.MoverSegurando(p.X + DentroDoLimiarPx, p.Y - DentroDoLimiarPx / 2);
        _inj.SoltarEsquerdo(p.X + DentroDoLimiarPx, p.Y - DentroDoLimiarPx / 2);

        EventoBuzzy? clique = Nucleo(marca, "Click", "Pressed", "Reacting", 3000);
        bool arrastou = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "NUCLEO" && e["evento"] == "DragStart");
        bool parado = Nativo.Retangulo(_hBuzzy).Equals(antes);
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        Registrar("critério 3 — soltar sem passar do limiar de arraste gera clique", clique is not null && !arrastou && parado,
            $"movimento de {DentroDoLimiarPx} px com o botão pressionado; CLICK→Reacting={clique is not null}; DRAG_START={arrastou}; janela parada={parado}");
        Registrar("critério 6 — clicar no personagem não tira o foco do aplicativo em uso", frente && !desativou,
            $"receptor na frente={frente}; receptor desativado={desativou}");
    }

    /// <summary>Critério 9: dois cliques dentro do tempo de clique duplo do Windows viram DOUBLE_CLICK.</summary>
    private void F3CliqueDuplo()
    {
        Nativo.POINT p = ExigirPontoOpaco("clique duplo");
        long marca = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();
        _inj.CliqueDuploEsquerdo(p.X, p.Y, _hBuzzy);

        EventoBuzzy? duplo = Nucleo(marca, "DoubleClick", "Pressed", "Settling", 3000);
        EventoBuzzy? primeiro = Nucleo(marca, "Click", "Pressed", "Reacting", 500);
        // DEC-025: o clique duplo esconde o personagem atrás da borda de baixo; outro o tira de lá.
        EventoBuzzy? escondeu = Nucleo(marca, "DoubleClick", "Settling", "Peeking", 3000);
        long marcaSaida = LogDoBuzzy.Marca();
        Nativo.POINT cabeca = PontoDoCorpo();
        _inj.CliqueDuploEsquerdo(cabeca.X, cabeca.Y, _hBuzzy);
        EventoBuzzy? saiu = Nucleo(marcaSaida, "DoubleClick", "Settling", "Idle", 3000);
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        Registrar("critério 9 — clique duplo é reconhecido dentro do intervalo do Windows", duplo is not null && primeiro is not null && frente && !desativou,
            $"GetDoubleClickTime={Nativo.GetDoubleClickTime()} ms; primeiro clique na hora (CLICK→Reacting)={primeiro is not null}; "
            + $"DOUBLE_CLICK de PRESSED={duplo is not null}; sem painel; receptor na frente={frente}; desativado={desativou}");
        Registrar("DEC-025 — clique duplo esconde atrás da borda de baixo e outro clique duplo tira de lá", escondeu is not null && saiu is not null,
            $"escondido (Settling→Peeking)={escondeu is not null}; clique duplo na cabeça em {cabeca}; saiu do esconderijo (Settling→Idle)={saiu is not null}");
    }

    /// <summary>
    /// Critérios 1, 3, 6 e 8: arraste longo, em zigue-zague no monitor principal. Confere, por
    /// amostragem, que a janela chega ao cursor menos a pegada logo depois de cada movimento; que
    /// nada autônomo acontece durante o gesto; o foco; e mede CPU e M5.
    /// </summary>
    private void F3ArrasteAcompanhaOCursor()
    {
        const int Movimentos = 240;
        const int PassoPx = 12;
        const int IntervaloMs = 16;
        const int Amostragem = 12;

        Nativo.POINT p = ExigirPontoOpaco("arraste");
        Nativo.RECT inicio = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT pegada = new(p.X - (inicio.Left + inicio.Largura / 2), p.Y - inicio.Bottom);
        long marca = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();

        // Faixa horizontal livre na área útil do principal, a partir do ponto de pressão.
        int minimo = _areaUtilPrincipal.Left + inicio.Largura, maximo = _areaUtilPrincipal.Right - inicio.Largura;
        _buzzy!.Refresh();
        TimeSpan cpuAntes = _buzzy.TotalProcessorTime;
        var relogio = Stopwatch.StartNew();

        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        int x = p.X, direcao = -1, amostras = 0, atrasadas = 0;
        double maiorEsperaMs = 0;
        for (int i = 1; i <= Movimentos; i++)
        {
            int proximo = x + direcao * PassoPx;
            if (proximo < minimo || proximo > maximo) { direcao = -direcao; proximo = x + direcao * PassoPx; }
            x = proximo;
            _inj.MoverSegurando(x, p.Y);
            if (i % Amostragem == 0)
            {
                // A janela precisa estar no cursor menos a pegada: sem física, sem suavização.
                Nativo.RECT esperado = SpriteEm(new Nativo.POINT(x - pegada.X, p.Y - pegada.Y), inicio);
                double? espera = EsperarRetangulo(esperado, 100);
                amostras++;
                if (espera is null) atrasadas++;
                else maiorEsperaMs = Math.Max(maiorEsperaMs, espera.Value);
            }
            else
            {
                Thread.Sleep(IntervaloMs);
            }
        }
        _inj.SoltarEsquerdo(x, p.Y);
        relogio.Stop();
        EventoBuzzy? fim = Nucleo(marca, "DragEnd", "Settling", "Idle", 3000);
        Thread.Sleep(300);
        _buzzy.Refresh();
        double cpuPct = (_buzzy.TotalProcessorTime - cpuAntes).TotalMilliseconds / relogio.Elapsed.TotalMilliseconds * 100;

        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
        bool comecou = ev.Any(e => e.Chave == "NUCLEO" && e["evento"] == "DragStart" && e["de"] == "Pressed" && e["para"] == "Dragging");
        // Entre o DRAG_START e o DRAG_END, nenhuma transição: não anda, não pula, não foge, não escala.
        List<EventoBuzzy> transicoes = [.. ev.Where(e => e.Chave == "NUCLEO" && e.Campos.ContainsKey("para"))];
        int iStart = transicoes.FindIndex(e => e["evento"] == "DragStart");
        int iEnd = transicoes.FindIndex(e => e["evento"] == "DragEnd");
        List<EventoBuzzy> meio = iStart >= 0 && iEnd > iStart ? transicoes.GetRange(iStart + 1, iEnd - iStart - 1) : [];
        EventoBuzzy? arraste = ev.LastOrDefault(e => e.Chave == "ARRASTE");
        double m5p95 = arraste is null ? double.NaN : double.Parse(arraste["m5P95Ms"], CultureInfo.InvariantCulture);
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);

        Registrar("critério 1 — durante o arraste o personagem acompanha o cursor e não anda, pula, foge nem escala",
            comecou && fim is not null && atrasadas == 0 && meio.Count == 0,
            $"{Movimentos} movimentos de {PassoPx} px a cada ~{IntervaloMs} ms; {amostras} amostras da janela no cursor menos a pegada, atrasadas além de 100 ms={atrasadas}, maior espera={maiorEsperaMs:0.0} ms (medida de outro processo, inclui a leitura); "
            + $"transições entre DRAG_START e DRAG_END={meio.Count}{(meio.Count > 0 ? " (" + string.Join(", ", meio.Select(e => e["evento"] + ":" + e["de"] + "→" + e["para"])) + ")" : "")}");
        Registrar("critério 3 — passar do limiar gera arraste", comecou, $"DRAG_START de PRESSED para DRAGGING={comecou}");
        Registrar("critério 6 — arrastar não tira o foco do aplicativo em uso", frente && !desativou, $"receptor na frente={frente}; receptor desativado={desativou}");
        Registrar("critério 8 — CPU durante o arraste medida e registrada", arraste is not null && !double.IsNaN(m5p95),
            $"CPU do Buzzy durante {relogio.Elapsed.TotalSeconds:0.0} s de arraste: {cpuPct:0.00}% de um núcleo; M5 pelo próprio app: {arraste?["movimentos"] ?? "?"} movimentos, média {arraste?["m5MediaMs"] ?? "?"} ms, p95 {arraste?["m5P95Ms"] ?? "?"} ms, máx {arraste?["m5MaxMs"] ?? "?"} ms "
            + $"(Q-08: p95 até 16,7 ms → {(m5p95 <= 16.7 ? "dentro" : "ACIMA")})");
    }

    /// <summary>
    /// Critério 2: soltar no ar valida a posição, com os pés no chão da área útil. Desde a Fase 4, o
    /// personagem sem apoio cai animado até o chão (pausado, sem quique; DEC-022 e DEC-023).
    /// </summary>
    private void F3SoltarNoAr()
    {
        Nativo.POINT p = ExigirPontoOpaco("soltar no ar");
        Nativo.RECT inicio = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT pegada = new(p.X - (inicio.Left + inicio.Largura / 2), p.Y - inicio.Bottom);
        long marca = LogDoBuzzy.Marca();

        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        int y = p.Y;
        for (int i = 0; i < 20; i++)
        {
            y -= 15;
            _inj.MoverSegurando(p.X, y);
            Thread.Sleep(16);
        }
        Nativo.RECT noAr = SpriteEm(new Nativo.POINT(p.X - pegada.X, y - pegada.Y), inicio);
        bool subiu = EsperarRetangulo(noAr, 500) is not null;
        _inj.SoltarEsquerdo(p.X, y);
        // Fase 4: sem apoio, DRAG_END leva a SETTLING e FALLING; a queda termina em LANDING e IDLE.
        EventoBuzzy? caiu = Nucleo(marca, "DragEnd", "Settling", "Falling", 3000);
        EventoBuzzy? pousou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["de"] == "Landing" && e["para"] == "Idle", 5000);
        Nativo.RECT esperado = SpriteEm(new Nativo.POINT(p.X - pegada.X, _areaUtilPrincipal.Bottom), inicio);
        bool noChao = EsperarRetangulo(esperado, 3000) is not null;
        Nativo.RECT agora = Nativo.Retangulo(_hBuzzy);
        Registrar("critério 2 — soltar no ar deixa a âncora na área útil, com os pés no chão (desde a Fase 4, depois de cair)",
            subiu && caiu is not null && pousou is not null && noChao && _areaUtilPrincipal.Contem(agora),
            $"subiu {p.Y - y} px com o cursor={subiu}; DRAG_END→Falling={caiu is not null}; Landing→Idle={pousou is not null}; janela {agora} (esperado {esperado}); dentro da área útil {_areaUtilPrincipal}={_areaUtilPrincipal.Contem(agora)}");
    }

    /// <summary>
    /// Critério 5 [HW]: arrastar até o monitor secundário (x negativo nesta máquina) e de volta.
    /// Escalas diferentes exigem hardware que não está aqui: ficam pendentes.
    /// </summary>
    private void F3ArrasteAteOutroMonitor()
    {
        List<Nativo.MonitorLido> monitores = Nativo.Monitores();
        if (monitores.FirstOrDefault(m => !m.Principal) is not { } sec)
        {
            Registrar("critério 5 [HW] — arrastar para outro monitor", NaoAplicavel, $"{monitores.Count} monitor(es): nenhum secundário");
            return;
        }
        Nativo.RECT origem = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT p = ExigirPontoOpaco("arraste até o secundário");
        Nativo.POINT pegada = new(p.X - (origem.Left + origem.Largura / 2), p.Y - origem.Bottom);
        Nativo.RECT area = sec.Info.rcWork;
        var destino = new Nativo.POINT((area.Left + area.Right) / 2 + pegada.X, area.Bottom + pegada.Y);

        long marca = LogDoBuzzy.Marca();
        ArrastarEmPassos(p, destino, 30);
        EventoBuzzy? fim = Nucleo(marca, "DragEnd", "Settling", "Idle", 3000);
        Thread.Sleep(200);
        Nativo.RECT noSec = Nativo.Retangulo(_hBuzzy);
        EventoBuzzy? posicao = LogDoBuzzy.Desde(marca).LastOrDefault(e => e.Chave == "POSICAO");
        bool dentro = area.Contem(noSec) && noSec.Bottom == area.Bottom;
        bool mesmoTamanho = noSec.Largura == origem.Largura && noSec.Altura == origem.Altura;
        // Desde a chave estável (Fase 5, DEC-030), o campo monitor é um resumo opaco; o nome GDI vem ao lado, em gdi.
        Registrar($"critério 5 [HW] — arrastar até {sec.Info.szDevice} {sec.Info.rcMonitor}, com coordenadas negativas, e soltar lá",
            fim is not null && dentro && posicao?["gdi"] == sec.Info.szDevice,
            $"janela {noSec}; na área útil {area} com os pés no chão={dentro}; monitor registrado={posicao?["gdi"] ?? "nenhum"} (chave {posicao?["monitor"] ?? "-"}); "
            + $"DPI {sec.Dpi} (principal {_dpiPrincipal}); mesmo tamanho={mesmoTamanho}");
        if (sec.Dpi == _dpiPrincipal)
        {
            Registrar("critério 5 [HW] — arrastar entre monitores de escalas diferentes", NaoAplicavel,
                $"os dois monitores desta máquina estão em {sec.Dpi} DPI; escalas mistas continuam PENDENTES de hardware (P6)");
            _naoExercitados.Add("critério 5 da Fase 3 com escalas diferentes [HW]");
        }

        // De volta ao lugar de origem, para os cenários seguintes.
        long marcaVolta = LogDoBuzzy.Marca();
        Nativo.POINT q = ExigirPontoOpaco("arraste de volta");
        var volta = new Nativo.POINT(origem.Left + origem.Largura / 2 + pegada.X, origem.Bottom + pegada.Y);
        ArrastarEmPassos(q, volta, 30);
        bool voltou = Nucleo(marcaVolta, "DragEnd", "Settling", "Idle", 3000) is not null && EsperarRetangulo(origem, 1000) is not null;
        Registrar("critério 5 [HW] — arrastar de volta ao monitor principal", voltou, $"janela {Nativo.Retangulo(_hBuzzy)} (origem {origem})");
    }

    /// <summary>Critério 9: o botão direito solicita o menu de contexto; Esc o fecha e o foco volta.</summary>
    private void F3BotaoDireitoAbreOMenu()
    {
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuDoPersonagem(VK_ESCAPE, "Esc (fechar sem escolher)");
        bool voltou = EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 2000);
        bool visivel = Nativo.IsWindowVisible(_hBuzzy);
        string situacao = m.Recusado ? Inconclusivo : m.Abriu && m.Fechado?["fechado"] == "Nenhum" && visivel ? Ok : Falhou;
        Registrar("critério 9 — botão direito no personagem solicita o menu de contexto", situacao,
            $"{DescreverMenu(m)}; continua visível={visivel}; eventos do menu no log={LogDoBuzzy.Desde(marca).Count(e => e.Chave == "MENU")}");
        Registrar("menu pelo botão direito — depois de fechar, o foco volta ao aplicativo em uso", voltou, $"frente depois: {Quem(Nativo.GetForegroundWindow())}");
        if (!voltou) AtivarReceptor("depois do menu pelo botão direito");
    }

    /// <summary>
    /// Critério 7: só exercitado se o ClickLock JÁ estiver ligado. A verificação lê a configuração,
    /// mas nunca a altera (AGENTS.md: não alterar configurações globais do Windows para testes).
    /// </summary>
    private void F3ClickLock()
    {
        const string Criterio = "critério 7 — com ClickLock ligado, o arraste funciona sem tempo limite";
        int? ligado = Nativo.LerParametroDoSistema(Nativo.SPI_GETMOUSECLICKLOCK);
        int? tempo = Nativo.LerParametroDoSistema(Nativo.SPI_GETMOUSECLICKLOCKTIME);
        if (ligado is null || tempo is null)
        {
            Registrar(Criterio, NaoAplicavel, "o Windows não informou o estado do ClickLock");
            _coberturaClickLock = "não lido";
            _naoExercitados.Add("critério 7 da Fase 3 (ClickLock)");
            return;
        }
        if (ligado == 0)
        {
            Registrar(Criterio, NaoAplicavel,
                "ClickLock desligado nesta máquina e NÃO ligado de propósito (configuração global); o árbitro não tem regra de tempo (teste automático SegurarPorMuitoTempo_NaoTemLimiteNemParaCliqueNemParaArraste)");
            _coberturaClickLock = "desligado na máquina; não alterado";
            _naoExercitados.Add("critério 7 da Fase 3 (ClickLock ligado pelo usuário)");
            return;
        }

        // ClickLock ligado pelo próprio usuário: segurar além do tempo dele trava o botão; o soltar
        // físico é engolido; o arraste segue sem botão até o clique de liberação.
        Nativo.POINT p = ExigirPontoOpaco("ClickLock");
        Nativo.RECT inicio = Nativo.Retangulo(_hBuzzy);
        long marca = LogDoBuzzy.Marca();
        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        Thread.Sleep(tempo.Value + 400);
        _inj.SoltarEsquerdo(p.X, p.Y);
        for (int i = 1; i <= 10; i++)
        {
            _inj.MoverSemBotao(p.X - i * 12, p.Y);
            Thread.Sleep(20);
        }
        bool arrastando = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "NUCLEO" && e["evento"] == "DragStart");
        _inj.CliqueEsquerdo(p.X - 120, p.Y, _hBuzzy);
        EventoBuzzy? fim = Nucleo(marca, "DragEnd", "Settling", "Idle", 3000);
        bool moveu = !Nativo.Retangulo(_hBuzzy).Equals(inicio);
        Registrar(Criterio, arrastando && fim is not null && moveu,
            $"ClickLock ligado pelo usuário (tempo {tempo} ms); arrastou depois do soltar engolido={arrastando}; terminou no clique de liberação={fim is not null}; janela movida={moveu}");
        _coberturaClickLock = "ligado na máquina; exercitado";
    }

    /// <summary>
    /// Critério 4: Alt+Tab no meio do arraste. Qualquer desfecho que não prenda o personagem é
    /// aceito — a captura perdida encerra o gesto (DRAG_CANCEL) ou ela continua até soltar (P3,
    /// B4a) —; depois disso, mover sem botão não mexe nele e um clique volta a funcionar.
    /// </summary>
    private void F3AltTabNoMeioDoArraste()
    {
        const string Criterio = "critério 4 — Alt+Tab no meio do arraste encerra o gesto sem travar o personagem";
        Nativo.POINT p = ExigirPontoOpaco("Alt+Tab");
        long marca = LogDoBuzzy.Marca();
        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        for (int i = 1; i <= 8; i++)
        {
            _inj.MoverSegurando(p.X - i * 15, p.Y);
            Thread.Sleep(16);
        }
        bool arrastava = Nucleo(marca, "DragStart", "Pressed", "Dragging", 1000) is not null;
        bool enviado = _inj.Combinacao([VK_MENU, VK_TAB], "Alt+Tab no meio do arraste", () => Nativo.GetForegroundWindow() == _hReceptor);
        Thread.Sleep(600);
        bool mudouFrente = Nativo.GetForegroundWindow() != _hReceptor;
        _inj.MoverSegurando(p.X - 160, p.Y);
        Thread.Sleep(100);
        _inj.SoltarEsquerdo(p.X - 160, p.Y);
        EventoBuzzy? terminou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] is "DragEnd" or "DragCancel" && e["para"] == "Idle", 3000);
        bool perdeu = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "CAPTURA" && e["perdida"] == "sim");
        (bool livre, bool clicou) = NaoFicouPreso("depois do Alt+Tab");
        Registrar(Criterio, !enviado ? Inconclusivo : arrastava && terminou is not null && livre && clicou ? Ok : Falhou,
            $"arrastando antes={arrastava}; Alt+Tab enviado={enviado} (com o receptor na frente); frente mudou={mudouFrente}; captura perdida={perdeu}; "
            + $"gesto terminou com {terminou?["evento"] ?? "nada"}→Idle; mover sem botão não moveu o personagem={livre}; clique seguinte virou CLICK={clicou}");
        VoltarAoReceptor("depois do Alt+Tab");
    }

    /// <summary>
    /// Critério 4: tecla Windows no meio do arraste (abre o Iniciar). Fecha o Iniciar com Esc, só se
    /// a janela da frente já não for o receptor nem o Buzzy; nada é lido da outra janela.
    /// </summary>
    private void F3TeclaWindowsNoMeioDoArraste()
    {
        const string Criterio = "critério 4 — tecla Windows no meio do arraste encerra o gesto sem travar o personagem";
        Nativo.POINT p = ExigirPontoOpaco("tecla Windows");
        long marca = LogDoBuzzy.Marca();
        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        for (int i = 1; i <= 8; i++)
        {
            _inj.MoverSegurando(p.X + i * 15, p.Y);
            Thread.Sleep(16);
        }
        bool arrastava = Nucleo(marca, "DragStart", "Pressed", "Dragging", 1000) is not null;
        bool enviado = _inj.Combinacao([VK_LWIN], "tecla Windows no meio do arraste", () => Nativo.GetForegroundWindow() == _hReceptor);
        Thread.Sleep(800);
        bool abriuOutra = Nativo.GetForegroundWindow() is var f && f != _hReceptor && !FrenteEhDoBuzzy() && f != 0;
        _inj.MoverSegurando(p.X + 160, p.Y);
        Thread.Sleep(100);
        _inj.SoltarEsquerdo(p.X + 160, p.Y);
        EventoBuzzy? terminou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] is "DragEnd" or "DragCancel" && e["para"] == "Idle", 3000);
        bool perdeu = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "CAPTURA" && e["perdida"] == "sim");

        bool fechouIniciar = false;
        if (abriuOutra)
        {
            fechouIniciar = _inj.TeclaVirtual(VK_ESCAPE, "Esc para fechar o Iniciar aberto pela tecla Windows",
                () => Nativo.GetForegroundWindow() is var g && g != _hReceptor && g != 0 && !FrenteEhDoBuzzy());
            Thread.Sleep(500);
        }
        (bool livre, bool clicou) = NaoFicouPreso("depois da tecla Windows");
        Registrar(Criterio, !enviado ? Inconclusivo : arrastava && terminou is not null && livre && clicou ? Ok : Falhou,
            $"arrastando antes={arrastava}; tecla Windows enviada={enviado}; outra janela na frente depois (o Iniciar, não identificado de propósito)={abriuOutra}; captura perdida={perdeu}; "
            + $"gesto terminou com {terminou?["evento"] ?? "nada"}→Idle; Esc para fechar o Iniciar={fechouIniciar}; mover sem botão não moveu o personagem={livre}; clique seguinte virou CLICK={clicou}");
        _coberturaTeclaWindows = enviado ? "exercitada" : "não enviada";
        VoltarAoReceptor("depois da tecla Windows");
    }

    // ------------------------------------------------------------------ apoio da Fase 3

    /// <summary>
    /// Depois de um gesto interrompido: mover o cursor sem botão por cima e ao lado do personagem
    /// não pode movê-lo (nada preso ao cursor), e um clique nele precisa virar CLICK.
    /// </summary>
    private (bool Livre, bool Clicou) NaoFicouPreso(string quando)
    {
        Thread.Sleep(300);
        Nativo.RECT antes = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT c = Nativo.Cursor();
        _inj.MoverSemBotao(c.X + 40, c.Y - 30);
        Thread.Sleep(150);
        _inj.MoverSemBotao(c.X - 60, c.Y + 10);
        Thread.Sleep(250);
        bool livre = Nativo.Retangulo(_hBuzzy).Equals(antes);

        Nativo.POINT p = PontoOpaco();
        bool clicou = false;
        if (Nativo.DonoDoPonto(p.X, p.Y) == _hBuzzy)
        {
            long marca = LogDoBuzzy.Marca();
            try
            {
                _inj.CliqueEsquerdo(p.X, p.Y, _hBuzzy);
                clicou = Nucleo(marca, "Click", "Pressed", "Reacting", 2000) is not null;
            }
            catch (CliqueRecusado e)
            {
                _rel.Linha($"   clique de conferência {quando} recusado: {e.Message}");
            }
        }
        else _rel.Linha($"   clique de conferência {quando}: o ponto opaco {p} não pertence ao Buzzy; nada foi clicado");
        return (livre, clicou);
    }

    /// <summary>
    /// Devolve o foco ao receptor depois de um cenário que o tirou (Alt+Tab, tecla Windows): primeiro
    /// um Alt+Tab de volta (a janela anterior é o receptor), só com outra janela na frente; se não
    /// bastar, o clique de ativação com as conferências de sempre.
    /// </summary>
    private void VoltarAoReceptor(string motivo)
    {
        if (Nativo.GetForegroundWindow() == _hReceptor) return;
        _inj.Combinacao([VK_MENU, VK_TAB], $"Alt+Tab de volta ao receptor ({motivo})",
            () => Nativo.GetForegroundWindow() is var f && f != _hReceptor && f != 0 && !FrenteEhDoBuzzy());
        if (EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 1500))
        {
            _rel.Linha($"   receptor de volta ao primeiro plano por Alt+Tab ({motivo})");
            Thread.Sleep(200);
            return;
        }
        AtivarReceptor(motivo);
    }

    /// <summary>Arrasta do ponto de pressão até o destino em passos, e solta lá.</summary>
    private void ArrastarEmPassos(Nativo.POINT de, Nativo.POINT ate, int passos)
    {
        _inj.Pressionar(de.X, de.Y, _hBuzzy);
        for (int i = 1; i <= passos; i++)
        {
            _inj.MoverSegurando(de.X + (ate.X - de.X) * i / passos, de.Y + (ate.Y - de.Y) * i / passos);
            Thread.Sleep(16);
        }
        _inj.SoltarEsquerdo(ate.X, ate.Y);
    }

    private Nativo.POINT ExigirPontoOpaco(string cenario)
    {
        Nativo.POINT p = PontoOpaco();
        if (Nativo.DonoDoPonto(p.X, p.Y) != _hBuzzy)
            throw new FalhaDeVerificacao($"{cenario}: o ponto opaco {p} não pertence ao Buzzy; nada foi clicado");
        return p;
    }

    /// <summary>Retângulo do sprite com a âncora dada (centro da base), no tamanho de <paramref name="modelo"/>.</summary>
    private static Nativo.RECT SpriteEm(Nativo.POINT ancora, Nativo.RECT modelo)
    {
        int esquerda = ancora.X - modelo.Largura / 2;
        return new Nativo.RECT { Left = esquerda, Top = ancora.Y - modelo.Altura, Right = esquerda + modelo.Largura, Bottom = ancora.Y };
    }

    /// <summary>Espera a janela do Buzzy chegar ao retângulo; devolve quanto esperou (ms) ou nulo se não chegou.</summary>
    private double? EsperarRetangulo(Nativo.RECT esperado, int limiteMs)
    {
        var relogio = Stopwatch.StartNew();
        while (true)
        {
            if (Nativo.Retangulo(_hBuzzy).Equals(esperado)) return relogio.Elapsed.TotalMilliseconds;
            if (relogio.ElapsedMilliseconds > limiteMs) return null;
            Thread.Sleep(1);
        }
    }

    private static EventoBuzzy? Nucleo(long marca, string evento, string de, string para, int limiteMs)
        => LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == evento && e["de"] == de && e["para"] == para, limiteMs);

    private (bool Frente, bool Desativou) FocoNoReceptor(int marcaR)
    {
        Thread.Sleep(200);
        bool frente = Nativo.GetForegroundWindow() == _hReceptor;
        bool desativou = _logReceptor.Desde(marcaR).Any(l => l.Contains("RECEPTOR|ATIVA|nao", StringComparison.Ordinal));
        return (frente, desativou);
    }

    private void RegistrarCoberturaFase3()
    {
        _rel.Linha("   Cobertura da Fase 3:");
        _rel.Linha($"     - ClickLock (critério 7): {_coberturaClickLock}");
        _rel.Linha($"     - tecla Windows no meio do arraste (critério 4): {_coberturaTeclaWindows}");
        _rel.Linha("     - janela de UAC no meio do arraste (critério 4): não exercitada (exige um pedido de elevação real); pendente de verificação manual");
        _naoExercitados.Add("critério 4 da Fase 3 com janela de UAC");
    }
}
