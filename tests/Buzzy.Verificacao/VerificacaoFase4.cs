using System.Diagnostics;
using System.Globalization;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética dos critérios [MANUAL] da Fase 4 — movimento e superfícies (TODO.md).
/// Abre o receptor (o "aplicativo do usuário"), e o Buzzy várias vezes: pausado, para segurar
/// o personagem no meio de uma queda provocada pelo próprio arraste (critério 3); e com a
/// autonomia ligada e uma semente escolhida por simulação do núcleo (a mesma configuração do
/// app), para medir a suavidade da caminhada (critério 5, por amostragem da posição da janela,
/// não gravação de tela), observar a escalada até a borda, o pendurar e a volta ao chão
/// (critério 7) e medir a CPU em RESTING (critério 4). Todo input é SINTÉTICO.
/// </summary>
internal sealed partial class Verificacao
{
    /// <summary>A configuração do núcleo que Aplicacao.Iniciar usa (fonte única no núcleo).</summary>
    private static ConfiguracaoDoNucleo ConfiguracaoDoApp => ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    internal Sumario ExecutarFase4()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            _prefixo = "Fase 1 (regressão) — ";
            AbrirBuzzy();
            _prefixo = "Fase 4 — ";
            _marcaFoco = _logReceptor.Contar();
            Digitar("H1-inicio;");

            F4SegurarNoMeioDaQueda();
            Digitar("H2-queda;");
            FecharPeriodoDeFoco("do início até o fim da queda segurada");
            FecharBuzzy("fim da parte pausada");

            Topologia topologia = TopologiaLida();
            F4CaminhadaSuave(topologia);
            Digitar("H3-caminhada;");
            F4EscaladaPenduraEVolta(topologia);
            Digitar("H4-escalada;");
            F4DescansoSemCusto(topologia);
            Digitar("H5-descanso;");
            F4QuiqueDeBorracha(topologia);
            Digitar("H6-quique;");
            F4SobePelaLateralInterna(topologia);
            Digitar("H7-lateral;");
            F4ArrastadoFicaNoCipoENaParede(topologia);
            Digitar("H8-preso;");
            F4EsconderijoNaBarraENaLateral(topologia);
            Digitar("H9-esconderijo;");
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
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        _rel.Linha("   Cobertura da Fase 4: o critério 5 foi medido pela posição da janela amostrada de outro processo, não por gravação de tela a 120 qps; a gravação continua pendente de verificação manual real.");
        _naoExercitados.Add("critério 5 da Fase 4 com gravação de tela a 120 qps");
        return Resumir();
    }

    // ------------------------------------------------------------------ cenários

    /// <summary>Critério 3: arrasta para o alto, solta, e pressiona o personagem no meio da queda.</summary>
    private void F4SegurarNoMeioDaQueda()
    {
        const string Criterio = "critério 3 — pressionar o personagem no meio da queda o segura na hora";
        Nativo.POINT p = ExigirPontoOpaco("queda");
        Nativo.RECT inicio = Nativo.Retangulo(_hBuzzy);
        long marca = LogDoBuzzy.Marca();

        // Leva o personagem 320 px para cima e solta: sem apoio, ele cai (Fase 4).
        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        for (int i = 1; i <= 16; i++)
        {
            _inj.MoverSegurando(p.X, p.Y - i * 20);
            Thread.Sleep(16);
        }
        _inj.SoltarEsquerdo(p.X, p.Y - 320);
        bool caindo = Nucleo(marca, "DragEnd", "Settling", "Falling", 1000) is not null;
        Thread.Sleep(60);

        // No meio da queda: o ponto do corpo agora (a janela se move; conferido de novo pelo
        // injetor). O ponto já está bem longe do soltar anterior, então não há clique duplo a evitar.
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        var alvo = new Nativo.POINT(r.Left + (p.X - inicio.Left), r.Top + r.Altura / 2);
        long marcaPress = LogDoBuzzy.Marca();
        try
        {
            _inj.Pressionar(alvo.X, alvo.Y, _hBuzzy, esperarCliqueDuplo: false);
        }
        catch (CliqueRecusado e)
        {
            Registrar(Criterio, Inconclusivo, $"caindo={caindo}; o personagem saiu do ponto antes do pressionar: {e.Message}");
            Nucleo(marca, "Tick", "Landing", "Idle", 3000);
            return;
        }
        EventoBuzzy? press = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "NUCLEO" && e["evento"] == "Press" && e["para"] == "Pressed", 1500);
        Nativo.RECT segurado = Nativo.Retangulo(_hBuzzy);
        Thread.Sleep(600);
        bool parado = Nativo.Retangulo(_hBuzzy).Equals(segurado);
        bool noAr = segurado.Bottom < _areaUtilPrincipal.Bottom - 40;
        _inj.SoltarEsquerdo(alvo.X, alvo.Y);
        bool clicou = Nucleo(marcaPress, "Click", "Pressed", "Reacting", 2000) is not null;
        bool pousou = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "NUCLEO" && e["para"] == "Idle" && e["de"] is "Landing" or "Settling", 5000) is not null;
        Thread.Sleep(300);
        bool noChao = Nativo.Retangulo(_hBuzzy).Bottom == _areaUtilPrincipal.Bottom;
        Registrar(Criterio, caindo && press?["de"] == "Falling" && parado && noAr && clicou && pousou && noChao,
            $"caindo depois de soltar no ar={caindo}; PRESS a partir de {press?["de"] ?? "nenhum"}; segurado em {segurado} (no ar={noAr}); parado 600 ms segurando={parado}; "
            + $"soltar virou CLICK={clicou}; depois caiu e pousou={pousou}; pés no chão={noChao}");
        (bool frente, bool desativou) = FocoNoReceptor(_logReceptor.Contar());
        Registrar("segurar no meio da queda não tira o foco do aplicativo em uso", frente && !desativou, $"receptor na frente={frente}; desativado={desativou}");
    }

    /// <summary>Critério 5 (medição instrumentada): a caminhada avança em passos pequenos e regulares.</summary>
    private void F4CaminhadaSuave(Topologia topologia)
    {
        const string Criterio = "critério 5 (medição instrumentada) — a caminhada avança em passos regulares, sem saltos nem paradas";
        if (EscolherSemente(topologia, Estado.Walking, querEscalar: false) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente com caminhada cedo");
            return;
        }
        AbrirBuzzyComAutonomia(escolha.Semente);
        EventoBuzzy? andou = LogDoBuzzy.Esperar(_inicioLogBuzzy, e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == "Walking", (int)escolha.Atraso.TotalMilliseconds + 5000, () => _buzzy is { HasExited: true });
        if (andou is null)
        {
            Registrar(Criterio, Falhou, $"semente {escolha.Semente}: a caminhada prevista em {escolha.Atraso.TotalSeconds:0.0} s não começou");
            FecharBuzzy("caminhada");
            return;
        }

        // Amostra a posição da janela o mais rápido possível durante a caminhada.
        var amostras = new List<(double Ms, int X)>();
        var relogio = Stopwatch.StartNew();
        while (relogio.ElapsedMilliseconds < 3000)
        {
            amostras.Add((relogio.Elapsed.TotalMilliseconds, Nativo.Retangulo(_hBuzzy).Left));
            Thread.Sleep(0);
        }
        var mudancas = new List<(double Ms, int Dx)>();
        for (int i = 1; i < amostras.Count; i++)
            if (amostras[i].X != amostras[i - 1].X) mudancas.Add((amostras[i].Ms, Math.Abs(amostras[i].X - amostras[i - 1].X)));
        bool andouNaJanela = mudancas.Count > 10;
        double passoEsperado = 90.0 * _dpiPrincipal / 96 / 60;
        int maiorPasso = mudancas.Count > 0 ? mudancas.Max(m => m.Dx) : 0;
        double[] intervalos = [.. mudancas.Zip(mudancas.Skip(1), (a, b) => b.Ms - a.Ms).Order()];
        double p95 = intervalos.Length > 0 ? intervalos[Math.Min(intervalos.Length - 1, (int)(intervalos.Length * 0.95))] : double.NaN;
        double maior = intervalos.Length > 0 ? intervalos[^1] : double.NaN;
        double mediano = intervalos.Length > 0 ? intervalos[intervalos.Length / 2] : double.NaN;
        // Aceita: passo de no máximo três passos físicos (dois passos no mesmo quadro do compositor) e nenhuma parada maior que 100 ms.
        bool regular = andouNaJanela && maiorPasso <= Math.Ceiling(3 * passoEsperado) && maior <= 100;
        Registrar(Criterio, regular,
            $"semente {escolha.Semente}; {amostras.Count} amostras em 3 s, {mudancas.Count} mudanças de posição; passo esperado {passoEsperado:0.00} px por passo físico, maior passo visto {maiorPasso} px; "
            + $"intervalo entre mudanças: mediana {mediano:0.0} ms, p95 {p95:0.0} ms, máx {maior:0.0} ms (amostragem de outro processo, não gravação de tela)");
        FecharBuzzy("caminhada");
    }

    /// <summary>Critério 7: escala até a borda superior, fica pendurado, percorre a borda e volta ao chão sem ficar preso.</summary>
    private void F4EscaladaPenduraEVolta(Topologia topologia)
    {
        const string Criterio = "critério 7 — escala até a borda superior, fica pendurado, percorre a borda e volta ao chão sem ficar preso";
        if (EscolherSemente(topologia, Estado.Walking, querEscalar: true) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente com escalada cedo");
            return;
        }
        AbrirBuzzyComAutonomia(escolha.Semente);
        long marca = _inicioLogBuzzy;
        EventoBuzzy? subiu = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["para"] == "Climbing", (int)escolha.Atraso.TotalMilliseconds + 30000, () => _buzzy is { HasExited: true });
        var naParede = new List<bool>();
        var noTeto = new List<int>();
        bool pendurou = false, voltou = false;
        var relogio = Stopwatch.StartNew();
        while (subiu is not null && relogio.Elapsed.TotalSeconds < 120)
        {
            List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
            string? ultimo = ev.LastOrDefault(e => e.Chave == "NUCLEO" && e.Campos.ContainsKey("para"))?["para"];
            Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
            if (ultimo == "Climbing") naParede.Add(r.Right == _areaUtilPrincipal.Right || r.Left == _areaUtilPrincipal.Left);
            if (ultimo == "Hanging")
            {
                pendurou = true;
                if (r.Top == _areaUtilPrincipal.Top) noTeto.Add(r.Left);
            }
            if (pendurou && ev.Any(e => e.Chave == "NUCLEO" && e["para"] == "Idle" && e["de"] is "Landing" or "Climbing"))
            {
                voltou = true;
                break;
            }
            Thread.Sleep(40);
        }
        bool percorreu = noTeto.Distinct().Count() > 3;
        Registrar(Criterio, subiu is not null && naParede.Count > 0 && naParede.All(x => x) && pendurou && percorreu && voltou,
            $"semente {escolha.Semente}; subiu={subiu is not null}; amostras na parede encostadas na lateral da área útil={naParede.Count(x => x)}/{naParede.Count}; "
            + $"pendurou={pendurou}; posições diferentes no teto={noTeto.Distinct().Count()}; voltou ao chão (IDLE)={voltou} em {relogio.Elapsed.TotalSeconds:0} s");
        FecharBuzzy("escalada");
    }

    /// <summary>Critério 4 (medição instrumentada): em RESTING o relógio para e a CPU volta à linha de base.</summary>
    private void F4DescansoSemCusto(Topologia topologia)
    {
        const string Criterio = "critério 4 (medição instrumentada) — em RESTING o relógio para e o consumo volta à linha de base";
        if (EscolherSemente(topologia, Estado.Resting, querEscalar: false) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente com descanso cedo");
            return;
        }
        AbrirBuzzyComAutonomia(escolha.Semente);
        EventoBuzzy? descansou = LogDoBuzzy.Esperar(_inicioLogBuzzy, e => e.Chave == "NUCLEO" && e["para"] == "Resting", (int)escolha.Atraso.TotalMilliseconds + 5000, () => _buzzy is { HasExited: true });
        Thread.Sleep(500);
        _buzzy!.Refresh();
        TimeSpan cpuAntes = _buzzy.TotalProcessorTime;
        var relogio = Stopwatch.StartNew();
        Thread.Sleep(15000);
        _buzzy.Refresh();
        double cpuPct = (_buzzy.TotalProcessorTime - cpuAntes).TotalMilliseconds / relogio.Elapsed.TotalMilliseconds * 100;
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(_inicioLogBuzzy);
        bool relogioLigadoNoDescanso = ev.SkipWhile(e => !(e.Chave == "NUCLEO" && e["para"] == "Resting")).Any(e => e.Chave == "RELOGIO" && e["ligado"] == "sim");
        bool aindaDescansando = ev.LastOrDefault(e => e.Chave == "NUCLEO" && e.Campos.ContainsKey("para"))?["para"] == "Resting";
        Registrar(Criterio, descansou is not null && aindaDescansando && !relogioLigadoNoDescanso && cpuPct < 0.1,
            $"semente {escolha.Semente}; em RESTING={descansou is not null} (ainda descansando no fim={aindaDescansando}); relógio ligado durante o descanso={relogioLigadoNoDescanso}; "
            + $"CPU do Buzzy em 15 s de descanso: {cpuPct:0.000}% de um núcleo (Q-08: média até 0,1%)");
        FecharBuzzy("descanso");
    }

    /// <summary>
    /// Critério 8 (toon force, DEC-023): solto do alto, ele quica como borracha e depois pousa.
    /// Abre com a autonomia ligada (pausado não quica); pressionar cancela a agenda, e depois do
    /// pouso a próxima decisão só vem após o intervalo de acomodação, então nada autônomo entra no meio.
    /// </summary>
    private void F4QuiqueDeBorracha(Topologia topologia)
    {
        const string Criterio = "critério 8 (toon force) — solto do alto, quica como borracha e depois pousa";
        AbrirBuzzyComAutonomia(SementeCalma(topologia));
        Nativo.POINT p = ExigirPontoOpaco("quique");
        long marca = LogDoBuzzy.Marca();
        // Leva o personagem 500 px para cima e solta: cai de uns 500 px e quica.
        _inj.Pressionar(p.X, p.Y, _hBuzzy);
        for (int i = 1; i <= 25; i++)
        {
            _inj.MoverSegurando(p.X, p.Y - i * 20);
            Thread.Sleep(16);
        }
        _inj.SoltarEsquerdo(p.X, p.Y - 500);
        bool caiu = Nucleo(marca, "DragEnd", "Settling", "Falling", 1500) is not null;
        EventoBuzzy? pousou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["de"] == "Landing" && e["para"] == "Idle", 8000);
        Thread.Sleep(300);
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
        int quiques = ev.Count(e => e.Chave == "NUCLEO" && e["para"] == "Jumping" && e["regra"].Contains("quique de borracha", StringComparison.Ordinal));
        bool noChao = Nativo.Retangulo(_hBuzzy).Bottom == _areaUtilPrincipal.Bottom;
        Registrar(Criterio, caiu && quiques == 2 && pousou is not null && noChao,
            $"caiu depois de soltar no alto={caiu}; quiques de borracha={quiques} (esperado 2); pousou e voltou a IDLE={pousou is not null}; pés no chão={noChao}");
        (bool frente, bool desativou) = FocoNoReceptor(_logReceptor.Contar());
        Registrar("o quique não tira o foco do aplicativo em uso", frente && !desativou, $"receptor na frente={frente}; desativado={desativou}");
        FecharBuzzy("quique");
    }

    /// <summary>
    /// Critério 8 (toon force, DEC-023): sobe pela lateral da área útil que encosta em outro
    /// monitor, sem atravessar para ele. O personagem é levado para perto dessa lateral, e a
    /// semente, escolhida por simulação do núcleo, faz a primeira decisão ser escalar.
    /// </summary>
    private void F4SobePelaLateralInterna(Topologia topologia)
    {
        const string Criterio = "critério 8 (toon force) — sobe pela lateral encostada no outro monitor, sem atravessar para ele";
        MonitorDoDesktop principal = topologia.Principal;
        Superficies sup = Superficies.Do(topologia, principal, ConfiguracaoDoApp.Tamanho.ParaPixels(principal.Dpi));
        int lado = sup.PassagemEsquerda ? -1 : sup.PassagemDireita ? +1 : 0;
        if (lado == 0)
        {
            Registrar(Criterio, NaoAplicavel, "nenhum outro monitor encosta numa lateral do principal");
            return;
        }
        int xLateral = lado < 0 ? sup.Esquerda : sup.Direita;
        var alvo = new PontoPx(xLateral - lado * 40, sup.Chao);
        if (EscolherSementeParaSubirNaLateral(topologia, alvo, xLateral) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente cuja primeira decisão, perto da lateral interna, seja escalar");
            return;
        }
        AbrirBuzzyComAutonomia(escolha.Semente);

        // Leva o personagem até 40 px da lateral interna, no chão: o soltar fica a mesma distância
        // da âncora que o ponto pressionado.
        Nativo.POINT p = ExigirPontoOpaco("lateral interna");
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        var soltar = new Nativo.POINT(alvo.X + (p.X - (r.Left + r.Largura / 2)), alvo.Y + (p.Y - r.Bottom));
        long marca = LogDoBuzzy.Marca();
        ArrastarEmPassos(p, soltar, 40);
        bool soltou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "DragEnd" && e["para"] == "Idle", 2000) is not null;
        EventoBuzzy? subiu = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["para"] == "Climbing", (int)escolha.Atraso.TotalMilliseconds + 15000, () => _buzzy is { HasExited: true });

        RetanguloPx area = principal.AreaUtil;
        var naLateral = new List<bool>();
        bool saiuDoPrincipal = false, pendurou = false;
        var relogio = Stopwatch.StartNew();
        while (subiu is not null && relogio.Elapsed.TotalSeconds < 40)
        {
            string ultimo = LogDoBuzzy.Desde(marca).LastOrDefault(e => e.Chave == "NUCLEO" && e.Campos.ContainsKey("para"))?["para"] ?? "";
            Nativo.RECT q = Nativo.Retangulo(_hBuzzy);
            if (ultimo == "Climbing") naLateral.Add(lado < 0 ? q.Left == area.Esquerda : q.Right == area.Direita);
            if (q.Left < area.Esquerda || q.Right > area.Direita) saiuDoPrincipal = true;
            if (ultimo == "Hanging")
            {
                pendurou = true;
                break;
            }
            Thread.Sleep(20);
        }
        Registrar(Criterio, soltou && subiu is not null && naLateral.Count > 0 && naLateral.All(x => x) && !saiuDoPrincipal && pendurou,
            $"semente {escolha.Semente}; lateral {(lado < 0 ? "esquerda" : "direita")} do principal encostada no outro monitor; solto perto dela={soltou}; subiu={subiu is not null}; "
            + $"amostras escalando encostadas nela={naLateral.Count(x => x)}/{naLateral.Count}; saiu do principal={saiuDoPrincipal}; chegou ao topo e se pendurou={pendurou}");
        FecharBuzzy("lateral interna");
    }

    /// <summary>
    /// Critério 8 (DEC-024, pedidos do usuário): arrastado para perto da borda de cima, agarra o
    /// cipó e fica; arrastado para junto de uma lateral, gruda na parede e fica; com a agenda
    /// ligada, nada autônomo o tira de lá; arrastado para o chão, volta a ser livre.
    /// </summary>
    private void F4ArrastadoFicaNoCipoENaParede(Topologia topologia)
    {
        const string Criterio = "critério 8 (DEC-024) — arrastado para o alto agarra o cipó, para a lateral gruda na parede, e só sai de lá quando o usuário tira";
        AbrirBuzzyComAutonomia(SementeCalma(topologia));
        MonitorDoDesktop principal = topologia.Principal;
        Superficies sup = Superficies.Do(topologia, principal, ConfiguracaoDoApp.Tamanho.ParaPixels(principal.Dpi));
        RetanguloPx area = principal.AreaUtil;

        // 1. Para o alto: a âncora 60 px abaixo do teto, no meio da área útil.
        bool noCipo = ArrastarAncoraPara(new PontoPx((sup.Esquerda + sup.Direita) / 2, sup.Teto + 60), "agarra o cipó");
        (bool ficouNoCipo, string resumoCipo) = Observar("Hanging", q => q.Top == area.Topo, TimeSpan.FromSeconds(12));

        // 2. Para a lateral direita, na meia altura.
        bool naParede = ArrastarAncoraPara(new PontoPx(sup.Direita - 30, (sup.Teto + sup.Chao) / 2), "fica grudado na parede");
        (bool ficouNaParede, string resumoParede) = Observar("Climbing", q => q.Right == area.Direita, TimeSpan.FromSeconds(12));

        // 3. De volta ao chão: livre.
        long marcaChao = LogDoBuzzy.Marca();
        bool noChao = ArrastarAncoraPara(new PontoPx((sup.Esquerda + sup.Direita) / 2, sup.Chao), null)
            && LogDoBuzzy.Esperar(marcaChao, e => e.Chave == "NUCLEO" && e["evento"] == "DragEnd" && e["para"] == "Idle", 2000) is not null;

        Registrar(Criterio, noCipo && ficouNoCipo && naParede && ficouNaParede && noChao,
            $"agarrou o cipó={noCipo}; ficou no cipó={ficouNoCipo} ({resumoCipo}); grudou na parede={naParede}; ficou na parede={ficouNaParede} ({resumoParede}); "
            + $"solto no chão ficou livre (IDLE)={noChao}");
        (bool frente, bool desativou) = FocoNoReceptor(_logReceptor.Contar());
        Registrar("agarrar no cipó e na parede não tira o foco do aplicativo em uso", frente && !desativou, $"receptor na frente={frente}; desativado={desativou}");
        FecharBuzzy("preso");
    }

    /// <summary>
    /// Critério 8 (DEC-025, pedido do usuário): o clique duplo esconde o personagem atrás da borda de
    /// baixo (a barra de tarefas) e outro o tira de lá; na parede, o clique duplo o esconde atrás da
    /// lateral e outro o devolve à parede. Escondido, com a agenda ligada, ele não sai.
    /// </summary>
    private void F4EsconderijoNaBarraENaLateral(Topologia topologia)
    {
        const string Criterio = "critério 8 (DEC-025) — clique duplo esconde atrás da barra de tarefas ou da lateral, só com a cabeça e as mãos, e outro clique duplo tira de lá";
        AbrirBuzzyComAutonomia(SementeCalma(topologia));
        MonitorDoDesktop principal = topologia.Principal;
        Superficies sup = Superficies.Do(topologia, principal, ConfiguracaoDoApp.Tamanho.ParaPixels(principal.Dpi));
        RetanguloPx area = principal.AreaUtil;

        // 1. No chão: esconde atrás da barra de tarefas e fica; outro clique duplo tira de lá.
        bool naBarra = CliqueDuploEspera("Peeking");
        (bool ficouNaBarra, string resumoBarra) = Observar("Peeking", q => q.Bottom == area.Base, TimeSpan.FromSeconds(8));
        bool saiuDaBarra = CliqueDuploEspera("Idle");

        // 2. Na parede direita: esconde atrás da lateral e fica; outro clique duplo o devolve à parede.
        bool naParede = ArrastarAncoraPara(new PontoPx(sup.Direita - 30, (sup.Teto + sup.Chao) / 2), "fica grudado na parede");
        bool naLateral = CliqueDuploEspera("Peeking");
        (bool ficouNaLateral, string resumoLateral) = Observar("Peeking", q => q.Right == area.Direita, TimeSpan.FromSeconds(8));
        bool voltouAParede = CliqueDuploEspera("Climbing");

        // 3. De volta ao chão.
        ArrastarAncoraPara(new PontoPx((sup.Esquerda + sup.Direita) / 2, sup.Chao), null);

        Registrar(Criterio, naBarra && ficouNaBarra && saiuDaBarra && naParede && naLateral && ficouNaLateral && voltouAParede,
            $"escondeu na barra={naBarra}; ficou lá={ficouNaBarra} ({resumoBarra}); saiu com outro clique duplo={saiuDaBarra}; "
            + $"na parede={naParede}; escondeu na lateral={naLateral}; ficou lá={ficouNaLateral} ({resumoLateral}); voltou à parede={voltouAParede}");
        (bool frente, bool desativou) = FocoNoReceptor(_logReceptor.Contar());
        Registrar("o esconderijo não tira o foco do aplicativo em uso", frente && !desativou, $"receptor na frente={frente}; desativado={desativou}");
        FecharBuzzy("esconderijo");
    }

    /// <summary>Clique duplo num ponto do corpo visível; espera a acomodação terminar no estado dado.</summary>
    private bool CliqueDuploEspera(string para)
    {
        Nativo.POINT p = PontoDoCorpo();
        long marca = LogDoBuzzy.Marca();
        _inj.CliqueDuploEsquerdo(p.X, p.Y, _hBuzzy);
        return LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "DoubleClick" && e["para"] == para, 3000) is not null;
    }

    /// <summary>
    /// Arrasta o personagem por um ponto do corpo até a âncora ficar em <paramref name="alvo"/> e
    /// solta; com <paramref name="regra"/>, espera essa transição no log do núcleo.
    /// </summary>
    private bool ArrastarAncoraPara(PontoPx alvo, string? regra)
    {
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT p = PontoDoCorpo();
        var soltar = new Nativo.POINT(alvo.X + (p.X - (r.Left + r.Largura / 2)), alvo.Y + (p.Y - r.Bottom));
        long marca = LogDoBuzzy.Marca();
        ArrastarEmPassos(p, soltar, 30);
        if (regra is null) return true;
        return LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["regra"].Contains(regra, StringComparison.Ordinal), 2000) is not null;
    }

    /// <summary>
    /// Observa por <paramref name="duracao"/>: o estado do núcleo continua <paramref name="estado"/>
    /// e a janela continua encostada onde deve. Devolve o resumo das amostras.
    /// </summary>
    private (bool Ficou, string Resumo) Observar(string estado, Func<Nativo.RECT, bool> encostada, TimeSpan duracao)
    {
        long marca = LogDoBuzzy.Marca();
        int amostras = 0, encostadas = 0;
        var relogio = Stopwatch.StartNew();
        while (relogio.Elapsed < duracao)
        {
            amostras++;
            if (encostada(Nativo.Retangulo(_hBuzzy))) encostadas++;
            Thread.Sleep(50);
        }
        List<EventoBuzzy> saidas = [.. LogDoBuzzy.Desde(marca).Where(e => e.Chave == "NUCLEO" && e.Campos.ContainsKey("para") && e["para"] != estado)];
        int passeios = LogDoBuzzy.Desde(marca).Count(e => e.Chave == "NUCLEO" && e["regra"].Contains("passeia", StringComparison.Ordinal));
        return (saidas.Count == 0 && encostadas == amostras,
            $"{encostadas}/{amostras} amostras encostadas; transições para outro estado={saidas.Count}; passeios da agenda={passeios}");
    }

    /// <summary>
    /// Um ponto do corpo, na pose que estiver: o primeiro ponto de uma grade dentro da janela cujo
    /// dono é o Buzzy (o teste de clique da janela layered só aceita pixel opaco). Não lê pixels.
    /// </summary>
    private Nativo.POINT PontoDoCorpo()
    {
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        int passo = Math.Max(2, r.Largura / 16);
        var candidatos = new List<Nativo.POINT>();
        for (int y = r.Top + passo; y < r.Bottom - passo; y += passo)
            for (int x = r.Left + passo; x < r.Right - passo; x += passo)
                candidatos.Add(new Nativo.POINT(x, y));
        // Primeiro o miolo do corpo: mais longe das bordas do quadro.
        int cx = (r.Left + r.Right) / 2, cy = (r.Top + r.Bottom) / 2;
        foreach (Nativo.POINT c in candidatos.OrderBy(c => Math.Abs(c.X - cx) + Math.Abs(c.Y - cy)))
        {
            if (Nativo.DonoDoPonto(c.X, c.Y) == _hBuzzy) return c;
        }
        throw new FalhaDeVerificacao($"nenhum ponto do corpo do Buzzy em {r}");
    }

    // ------------------------------------------------------------------ apoio da Fase 4

    private sealed record Escolha(ulong Semente, TimeSpan Atraso);

    /// <summary>Folga, depois de abrir o Buzzy, para pegá-lo antes da primeira decisão autônoma.</summary>
    private static readonly TimeSpan TempoParaPegarOPersonagem = TimeSpan.FromSeconds(6);

    /// <summary>Semente cuja primeira decisão autônoma demora pelo menos <see cref="TempoParaPegarOPersonagem"/>.</summary>
    private static ulong SementeCalma(Topologia topologia)
    {
        for (ulong semente = 1; ; semente++)
        {
            var nucleo = new Nucleo(ConfiguracaoDoApp, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao));
            if (nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault() is { } agenda && agenda.Atraso >= TempoParaPegarOPersonagem)
                return semente;
        }
    }

    /// <summary>
    /// Semente em que, depois de carregar e de ser solto no chão em <paramref name="alvo"/>, a
    /// primeira decisão autônoma leva o personagem a escalar a lateral em <paramref name="xLateral"/>.
    /// Simula o núcleo real com a sequência que o app vai receber: carga, pressionar (cancela a
    /// agenda), arraste, soltar (agenda de novo) e o disparo da agenda, seguido de passos do relógio.
    /// </summary>
    private static Escolha? EscolherSementeParaSubirNaLateral(Topologia topologia, PontoPx alvo, int xLateral)
    {
        var pegada = new PontoPx(0, -50);
        for (ulong semente = 1; semente < 20000; semente++)
        {
            var nucleo = new Nucleo(ConfiguracaoDoApp, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao));
            // A primeira decisão precisa demorar o bastante para o arraste chegar antes dela.
            AgendarDecisao? primeira = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (primeira is null || primeira.Atraso < TempoParaPegarOPersonagem || nucleo.Estado.Lugar is not { } lugar) continue;
            var inicio = new PontoPx(lugar.Ancora.X + pegada.X, lugar.Ancora.Y + pegada.Y);
            var fim = new PontoPx(alvo.X + pegada.X, alvo.Y + pegada.Y);
            nucleo.Enfileirar(new Press(inicio));
            nucleo.Enfileirar(new DragStart());
            nucleo.Enfileirar(new DragMove(fim));
            nucleo.Enfileirar(new DragEnd(fim));
            AgendarDecisao? agenda = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (nucleo.Estado.Estado != Estado.Idle || agenda is null || agenda.Atraso > TimeSpan.FromSeconds(12)) continue;
            nucleo.Enfileirar(new AutonomyTimer(agenda.Geracao));
            nucleo.Processar();
            for (int i = 0; i < 600 && nucleo.Estado.Estado == Estado.Walking; i++)
            {
                nucleo.Enfileirar(new Tick());
                nucleo.Processar();
            }
            if (nucleo.Estado.Estado == Estado.Climbing && nucleo.Estado.Lugar?.Ancora.X == xLateral)
                return new Escolha(semente, agenda.Atraso);
        }
        return null;
    }

    /// <summary>
    /// Semente em que a primeira decisão autônoma leva ao destino pedido, cedo (até 12 s), simulada
    /// com o núcleo real e a topologia lida agora (a mesma que o Buzzy vai ler).
    /// </summary>
    private static Escolha? EscolherSemente(Topologia topologia, Estado destino, bool querEscalar)
    {
        for (ulong semente = 1; semente < 20000; semente++)
        {
            var nucleo = new Nucleo(ConfiguracaoDoApp, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao));
            AgendarDecisao? agenda = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (agenda is null || agenda.Atraso > TimeSpan.FromSeconds(12)) continue;
            nucleo.Enfileirar(new AutonomyTimer(agenda.Geracao));
            nucleo.Processar();
            if (nucleo.Estado.Estado == destino && nucleo.Estado.Movimento.QuerEscalar == querEscalar)
                return new Escolha(semente, agenda.Atraso);
        }
        return null;
    }

    private Topologia TopologiaLida()
        => new(Nativo.Monitores().Select(m => new MonitorDoDesktop(m.Info.szDevice, R(m.Info.rcMonitor), R(m.Info.rcWork), m.Dpi, m.Principal)));

    /// <summary>Abre o Buzzy com a autonomia ligada e a semente dada; confere que as janelas são deste processo.</summary>
    private void AbrirBuzzyComAutonomia(ulong semente)
    {
        _inicioLogBuzzy = LogDoBuzzy.Marca();
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy, "--semente", semente.ToString(CultureInfo.InvariantCulture));
        ExigirNenhumBuzzyAberto();
        // A escolha da semente simula a partida sem posição salva: a pasta do perfil é apagada antes de cada abertura.
        PerfilDaVerificacao.Limpar();
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não iniciou.");
        _inicioBuzzy = _buzzy.StartTime;
        _buzzyEncerrado = false;
        _hBuzzy = JanelaDoBuzzy(Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy")["hwnd"], "JANELA");
        _hServico = JanelaDoBuzzy(Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço")["hwnd"], "SERVICO");
        Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 5000, "bandeja");
        _rel.Linha($"   Buzzy aberto com a autonomia ligada e a semente {semente}: pid {_buzzy.Id}");
    }

    private void FecharBuzzy(string quando)
    {
        if (_buzzy is null || _buzzy.HasExited) return;
        Nativo.PostMessage(_hBuzzy, Nativo.WM_CLOSE, 0, 0);
        if (!_buzzy.WaitForExit(5000))
            _rel.Linha("   " + Processos.EncerrarAForca(_buzzy, $"Buzzy ({quando})"));
        _buzzyEncerrado = true;
        Thread.Sleep(300);
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor($"depois de fechar o Buzzy ({quando})");
    }
}
