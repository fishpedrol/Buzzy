using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Testes de integração da Fase 3: mensagens de mouse POSTADAS à janela do Buzzy aberto pelo
/// próprio teste percorrem o adaptador, a arbitragem e o núcleo até a janela. Não injetam input:
/// nada passa pela fila de input do Windows nem por outro aplicativo, e o resultado vale como
/// integração automatizada, não como gesto (a verificação com input sintético fica em
/// Buzzy.Verificacao). Só rodam com --integracao, depois de avisar o usuário.
/// </summary>
[Integracao]
internal sealed class GestosTestes
{
    private static RetanguloPx Sprite(PontoPx ancora, TamanhoPx tamanho)
    {
        int esquerda = ancora.X - tamanho.Largura / 2;
        return new RetanguloPx(esquerda, ancora.Y - tamanho.Altura, esquerda + tamanho.Largura, ancora.Y);
    }

    private static PontoPx Ancora(RetanguloPx janela) => new(janela.Esquerda + janela.Largura / 2, janela.Base);

    private static EventoDoLog Nucleo(BuzzyEmTeste b, long marca, string evento, string de, string para, int limiteMs, string oQue)
        => EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == evento && (de.Length == 0 || e["de"] == de) && e["para"] == para, limiteMs, oQue);

    private static EventoDoLog EsperarDesde(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoDoLog? achado = BuzzyEmTeste.EventosDesde(marca).FirstOrDefault(condicao);
            if (achado is not null) return achado;
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(50);
        }
    }

    private static (PontoPx Opaco, RetanguloPx Janela) Preparar(BuzzyEmTeste b)
    {
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        EventoDoLog posicao = b.Esperar(e => e.Chave == "POSICAO", 5000, "posição");
        return (EventoDoLog.Ponto(posicao["pontoOpaco"]), b.RetanguloDaJanela());
    }

    [Teste]
    public void Arraste_MensagensPostadasLevamOBuzzyPeloNucleoESoltarNoArCaiAteOChao()
    {
        Afirmar.Verdadeiro(NativoTeste.ThreadEmPerMonitorV2(), "a thread do teste está em Per-Monitor V2");
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        (PontoPx opaco, RetanguloPx antes) = Preparar(b);
        Topologia topologia = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
        MonitorDoDesktop principal = topologia.Principal;
        PontoPx ancora = Ancora(antes);
        var pegada = new PontoPx(opaco.X - ancora.X, opaco.Y - ancora.Y);
        long marca = BuzzyEmTeste.MarcaDoLog();

        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        Nucleo(b, marca, "Press", "", "Pressed", 3000, "PRESS no ponto opaco");

        // Dentro do limiar de arraste (4 px de cada lado a 96 DPI), nada se move.
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(opaco.X - 3, opaco.Y + 2));
        Thread.Sleep(200);
        Afirmar.Igual(antes, b.RetanguloDaJanela(), "dentro do limiar a janela fica parada");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "NUCLEO" && e["evento"] == "DragStart"), "sem DRAG_START dentro do limiar");

        // Passou do limiar: a janela vai para o cursor menos a pegada, sem limite (invariante 2).
        var meio = new PontoPx(opaco.X - 150, opaco.Y - 100);
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, meio);
        Nucleo(b, marca, "DragStart", "Pressed", "Dragging", 3000, "DRAG_START");
        b.EsperarRetangulo(Sprite(new PontoPx(meio.X - pegada.X, meio.Y - pegada.Y), antes.Tamanho), 3000, "janela no cursor menos a pegada");

        var alvo = new PontoPx(opaco.X - 300, opaco.Y - 200);
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, alvo);
        b.EsperarRetangulo(Sprite(new PontoPx(alvo.X - pegada.X, alvo.Y - pegada.Y), antes.Tamanho), 3000, "janela acompanhando o cursor");

        // Solto 200 px acima do chão: sem apoio, cai (Fase 4) até os pés tocarem o chão da área útil, no mesmo x.
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, alvo);
        Nucleo(b, marca, "DragEnd", "Dragging", "Settling", 3000, "DRAG_END");
        Nucleo(b, marca, "DragEnd", "Settling", "Falling", 3000, "sem apoio depois de soltar: cai");
        Nucleo(b, marca, "Tick", "Landing", "Idle", 5000, "pousou e voltou a IDLE");
        RetanguloPx esperado = Sprite(new PontoPx(alvo.X - pegada.X, principal.AreaUtil.Base), antes.Tamanho);
        b.EsperarRetangulo(esperado, 3000, "no chão, no mesmo x em que foi solto");
        Afirmar.Verdadeiro(principal.AreaUtil.Contem(esperado), $"janela {esperado} dentro da área útil {principal.AreaUtil}");

        EventoDoLog arraste = EsperarDesde(b, marca, e => e.Chave == "ARRASTE", 3000, "resumo do arraste (M5)");
        Afirmar.Igual("DragEnd", arraste["fim"], "o arraste terminou soltando");
        Afirmar.Verdadeiro(int.Parse(arraste["movimentos"], System.Globalization.CultureInfo.InvariantCulture) >= 2, $"movimentos medidos: {arraste["movimentos"]}");
        double p95 = double.Parse(arraste["m5P95Ms"], System.Globalization.CultureInfo.InvariantCulture);
        Afirmar.Verdadeiro(p95 >= 0 && p95 < 16.7, $"M5 p95 dentro de um quadro a 60 Hz (Q-08): {p95} ms");
        Console.WriteLine($"         M5 do arraste postado: média {arraste["m5MediaMs"]} ms, p95 {arraste["m5P95Ms"]} ms, máx {arraste["m5MaxMs"]} ms");

        EventoDoLog posicao = EsperarDesde(b, marca, e => e.Chave == "POSICAO", 3000, "posição validada registrada");
        Afirmar.Igual(esperado, EventoDoLog.Retangulo(posicao["retangulo"]), "posição registrada = posição real");
        Afirmar.Igual(1, BuzzyEmTeste.EventosDesde(marca).Count(e => e.Chave == "POSICAO"), "durante o arraste nenhuma posição é registrada, só a validada");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void CliqueCliqueDuploEBotaoDireito_ViramGestosDoNucleo()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        (PontoPx opaco, RetanguloPx antes) = Preparar(b);
        long marca = BuzzyEmTeste.MarcaDoLog();

        // Clique: reação curta, e depois a validação de volta ao repouso.
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, opaco);
        Nucleo(b, marca, "Click", "Pressed", "Reacting", 3000, "CLICK");
        Nucleo(b, marca, "Tick", "Reacting", "Settling", 3000, "fim da reação pelo relógio");
        Afirmar.Igual(antes, b.RetanguloDaJanela(), "o clique não move o personagem");

        // Clique duplo: o segundo pressionar chega bem antes do tempo de clique duplo.
        long marcaDupla = BuzzyEmTeste.MarcaDoLog();
        foreach (int msg in new[] { NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP, NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP })
            b.PostarMouse(msg, msg == NativoTeste.WM_LBUTTONDOWN ? NativoTeste.MK_LBUTTON : 0, opaco);
        Nucleo(b, marcaDupla, "Click", "Pressed", "Reacting", 3000, "primeiro clique, na hora");
        Nucleo(b, marcaDupla, "DoubleClick", "Pressed", "Settling", 3000, "DOUBLE_CLICK a partir de PRESSED");
        // DEC-025: o clique duplo esconde o personagem atrás da borda de baixo, no mesmo x.
        Nucleo(b, marcaDupla, "DoubleClick", "Settling", "Peeking", 3000, "escondido atrás da borda de baixo");
        Afirmar.Igual(antes, b.RetanguloDaJanela(), "no chão, o esconderijo fica no mesmo lugar: só a pose muda");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marcaDupla).Any(e => e.Chave == "NUCLEO" && e["efeitoPendente"] == "AbrirPainelDeEnergia"), "o clique duplo não abre o painel de energia");

        // Outro clique duplo tira do esconderijo, numa posição onde a cabeça aparece: o miolo de baixo do quadro.
        var cabeca = new PontoPx((antes.Esquerda + antes.Direita) / 2, antes.Base - 20);
        long marcaSaida = BuzzyEmTeste.MarcaDoLog();
        foreach (int msg in new[] { NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP, NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP })
            b.PostarMouse(msg, msg == NativoTeste.WM_LBUTTONDOWN ? NativoTeste.MK_LBUTTON : 0, cabeca);
        Nucleo(b, marcaSaida, "DoubleClick", "Settling", "Idle", 3000, "saiu do esconderijo, de pé no chão");

        // Botão direito: o mesmo menu da Fase 1, pela arbitragem; fechado sem teclado.
        long marcaMenu = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, opaco);
        b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, opaco);
        EsperarDesde(b, marcaMenu, e => e.Chave == "MENU" && e["aberto"] == "personagem", 3000, "menu aberto pelo botão direito");
        EventoDoLog exibindo = EsperarDesde(b, marcaMenu, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000, "menu exibido");
        var dono = (nint)long.Parse(exibindo["dono"], System.Globalization.CultureInfo.InvariantCulture);
        Afirmar.Igual((uint)b.Processo.Id, NativoTeste.PidDe(dono), "o dono do menu é deste Buzzy");
        Afirmar.Verdadeiro(b.Postar(dono, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE ao dono do menu");
        EventoDoLog fechado = EsperarDesde(b, marcaMenu, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 3000, "menu fechado");
        Afirmar.Igual("Nenhum", fechado["fechado"], "cancelar o menu não escolhe nada");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "o Buzzy continua visível");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void CapturaPerdidaNoMeioDoArraste_EncerraOGestoSemPrenderOPersonagemAoCursor()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        (PontoPx opaco, RetanguloPx antes) = Preparar(b);
        PontoPx ancora = Ancora(antes);
        var pegada = new PontoPx(opaco.X - ancora.X, opaco.Y - ancora.Y);
        long marca = BuzzyEmTeste.MarcaDoLog();

        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        var longe = new PontoPx(opaco.X - 120, opaco.Y);
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, longe);
        RetanguloPx arrastado = Sprite(new PontoPx(longe.X - pegada.X, longe.Y - pegada.Y), antes.Tamanho);
        b.EsperarRetangulo(arrastado, 3000, "arrastando");

        // Cancelar o modo (o que acontece quando outra janela toma o mouse): captura perdida.
        Afirmar.Verdadeiro(b.Postar(b.Janela, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE à janela do personagem");
        EsperarDesde(b, marca, e => e.Chave == "CAPTURA" && e["perdida"] == "sim", 3000, "captura perdida registrada");
        Nucleo(b, marca, "DragCancel", "Dragging", "Settling", 3000, "DRAG_CANCEL");
        Nucleo(b, marca, "DragCancel", "Settling", "Idle", 3000, "acomodado onde estava");
        EventoDoLog arraste = EsperarDesde(b, marca, e => e.Chave == "ARRASTE", 3000, "resumo do arraste");
        Afirmar.Igual("DragCancel", arraste["fim"], "o arraste terminou cancelado");
        Afirmar.Igual(arrastado, b.RetanguloDaJanela(), "fica onde estava, sem voltar à origem (ARCHITECTURE.md 2.7, passo 6)");

        // Um movimento depois da perda não arrasta nada: o personagem não ficou preso ao cursor.
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(longe.X - 200, longe.Y - 50));
        Thread.Sleep(250);
        Afirmar.Igual(arrastado, b.RetanguloDaJanela(), "movimento depois da captura perdida não move o personagem");

        // E o próximo clique funciona.
        long marcaClique = BuzzyEmTeste.MarcaDoLog();
        PontoPx opacoAgora = new(arrastado.Esquerda + (opaco.X - antes.Esquerda), arrastado.Topo + (opaco.Y - antes.Topo));
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opacoAgora);
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, opacoAgora);
        Nucleo(b, marcaClique, "Click", "Pressed", "Reacting", 3000, "clique depois da captura perdida");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }
}
