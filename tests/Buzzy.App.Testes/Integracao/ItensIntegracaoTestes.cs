using System.Globalization;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Os itens do tamagotchi adulto no Buzzy.exe de verdade, com a chave ligada no aplicativo (DEC-028, passo T9; crítica,
/// seção 6): o menu (pelo personagem, pela bandeja e pelo botão direito num item; "Recolher itens" desabilitado sem
/// itens), as janelas dos itens, o arraste até ele, o fim do gesto (captura solta e ordem Z), o uso, o PRESS no meio do
/// uso, recolher, esconder e mostrar (também com outra janela "sempre no topo" à frente), minimizar no meio do arraste de
/// um item, arrastar o personagem com um item na tela, sair com itens na tela, o temporizador da onda, a paranoia (pedidos
/// de 2026-10-01: a mistura com droga sintética, com a semente em que o sorteio de 1 em 8 sai, o olhar pro teto com o suor e
/// a água que acalma um passo) e o baseado por conta própria (pedido de 19:10: com a agenda ligada e a semente em que a
/// primeira decisão é fumar, ele fuma sozinho, sem janela de item, fica chapado, e o clique o interrompe).
/// Tudo por mensagens POSTADAS às janelas do próprio Buzzy aberto pelo teste, com o PID de cada janela conferido antes
/// de cada mensagem (as dos itens, a do dono de cada menu e a de serviço vêm do log): nada passa pela fila de input do
/// Windows nem por outro aplicativo, e o resultado vale como integração automatizada, não como gesto. Só rodam com
/// --integracao, depois de avisar o usuário: o Buzzy, o menu e os itens aparecem e somem na tela. Tudo é de desenho
/// animado; o menu só nomeia os itens.
/// </summary>
[Integracao]
internal sealed class ItensIntegracaoTestes
{
    /// <summary>Um item na tela: o Id do núcleo, o nome do valor de <see cref="Item"/>, a janela (deste Buzzy) e a linha ITEM|mostrado.</summary>
    private sealed record ItemNaTela(int Id, string Nome, nint Hwnd, EventoDoLog Mostrado)
    {
        internal string IdNoLog => Id.ToString(CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ testes

    [Teste]
    public void Banana_PeloMenuDoPersonagem_AJanelaApareceSemAtivar_CaiEParaNoChaoAoLadoELogoAbaixoDele()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        MonitorDoDesktop principal = Principal();
        RetanguloPx corpo = b.RetanguloDaJanela();

        // Sem nenhum item na tela, "Recolher itens" está desabilitado no menu de verdade (revisão de correção, achado 3): a
        // tecla dele não escolhe nada, e o menu fecha sem comando.
        EventoDoLog semItens = MenuSemEscolhaObrigatoria(b, () => AbrirPeloPersonagem(b, personagem), "Recolher sem itens", 'i', 'r');
        Afirmar.Igual("Nenhum", semItens["fechado"], "sem itens na tela, \"Recolher itens\" não pode ser escolhido");

        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        Afirmar.Igual("sim", banana.Mostrado["criada"], "uma janela nova para o item");
        long ex = NativoTeste.EstiloEstendido(banana.Hwnd);
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_NOACTIVATE) != 0, "WS_EX_NOACTIVATE: a janela do item nunca é ativada");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_TOOLWINDOW) != 0, "WS_EX_TOOLWINDOW: fora da barra de tarefas e do Alt+Tab");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_LAYERED) != 0, "WS_EX_LAYERED: só os pixels opacos recebem clique");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_TOPMOST) != 0, "WS_EX_TOPMOST, como o personagem");
        Afirmar.Falso((ex & NativoTeste.WS_EX_APPWINDOW) != 0, "sem WS_EX_APPWINDOW");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(banana.Hwnd), "a janela do item está à vista");
        nint frente = FrenteDepoisDoMenu(b);

        (RetanguloPx noChao, _) = EsperarParado(b, inicio, banana);
        Afirmar.Igual(principal.AreaUtil.Base, noChao.Base, "parou no chão da área útil");
        Afirmar.Igual(SpriteDoItem.TamanhoLogico.ParaPixels(principal.Dpi), noChao.Tamanho, "48 × 48 DIP no DPI do monitor");
        Afirmar.Falso(noChao.Intersecta(corpo), $"nasceu ao lado do personagem, sem cobri-lo: item {noChao}, personagem {corpo}");
        Afirmar.Igual(noChao, RetanguloDe(banana.Hwnd), "a janela está onde o log diz");

        // O relógio só correu com o item caindo (invariante 29): ligou na invocação e desligou no pouso.
        List<EventoDoLog> ev = BuzzyEmTeste.EventosDesde(inicio);
        int ligou = ev.FindIndex(e => e.Chave == "RELOGIO" && e["ligado"] == "sim");
        Afirmar.Verdadeiro(ligou >= 0, "o relógio ligou com o item caindo");
        Afirmar.Verdadeiro(ev.Skip(ligou).Any(e => e.Chave == "RELOGIO" && e["ligado"] == "nao"), "e desligou quando ele parou no chão");

        // Ordem Z por evento (crítica, L17): parado, o item fica logo abaixo do personagem.
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "o item fica logo abaixo do personagem na ordem Z");

        // Nem a janela do item nem a do personagem tomaram o primeiro plano enquanto o item aparecia, caía e parava.
        Thread.Sleep(300);
        Afirmar.Falso(b.FrenteEhDesteBuzzy(), "nenhuma janela do Buzzy em primeiro plano");
        Afirmar.Igual(frente, NativoTeste.GetForegroundWindow(), "o primeiro plano não mudou com o item caindo e parando");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void ArrasteAteOPersonagem_EleUsaOItemEAJanelaSome_EOPressNoMeioDoUsoVaiParaPressedNoMesmoEvento()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        (_, PontoPx opaco) = EsperarParado(b, inicio, banana);

        long marca = BuzzyEmTeste.MarcaDoLog();
        Arrastar(b, banana, opaco, personagem);
        EventoDoLog uso = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000, "ITEM_DRAG_END sobre ele: USING");
        Afirmar.Igual(("Idle", true), (uso["de"], uso["regra"].Contains("Comer Banana", StringComparison.Ordinal)), $"de IDLE para USING, comendo a banana ({uso["regra"]})");

        // O usuário prevalece (crítica, seção 4): o PRESS no personagem no meio do uso vai para PRESSED no mesmo evento.
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, personagem);
        EventoDoLog press = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "Press", 2000, "PRESS no personagem");
        Afirmar.Igual(("Using", "Pressed"), (press["de"], press["para"]), "PRESS a partir de USING, no mesmo evento, sem esperar o fim do uso");
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, personagem);
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "Click" && e["de"] == "Pressed" && e["para"] == "Reacting", 2000, "o soltar vira clique");

        EventoDoLog solto = EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["solto"] == banana.IdNoLog, 3000, "ITEM|solto");
        Afirmar.Igual(("ItemDragEnd", "sim", "sim"), (solto["fim"], solto["sobre"], solto["usado"]), "solto sobre ele e usado");
        double p95 = double.Parse(solto["m5P95Ms"], CultureInfo.InvariantCulture);
        Afirmar.Verdadeiro(p95 >= 0 && p95 < 16.7, $"M5 do arraste do item dentro de um quadro a 60 Hz (Q-08): p95 {p95} ms");
        Console.WriteLine($"         M5 do arraste do item: {solto["movimentos"]} movimentos, média {solto["m5MediaMs"]} ms, p95 {solto["m5P95Ms"]} ms");
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["removido"] == banana.IdNoLog && e["motivo"] == "Usado", 3000, "a janela do item sai, motivo Usado");
        Thread.Sleep(200);
        Afirmar.Falso(NativoTeste.IsWindow(banana.Hwnd), "a janela do item não existe mais");

        // A onda vale desde o soltar (crítica, C16), e o PRESS não a cancela.
        EsperarDesde(b, marca, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "onda agendada ao soltar o item nele");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "ONDA" && e["cancelada"] == "sim"), "a onda continua depois do PRESS");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void LancaPerfume_OTemporizadorDaOndaDisparaUmaVezEReagenda_EOPendenteECanceladoAoSair()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela lanca = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'l', "LancaPerfume");
        (_, PontoPx opaco) = EsperarParado(b, inicio, lanca);

        long marca = BuzzyEmTeste.MarcaDoLog();
        Arrastar(b, lanca, opaco, personagem);
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000, "USING");

        // Lança-perfume: onda Tonto, intensidade 2 (tabelas 4.1 e 4.2). A subida e o nível do pico, pela tabela do núcleo.
        DadosDaOnda tonto = TabelaDoTamagotchi.DaOnda(Onda.Tonto);
        string subida = ((long)tonto.Duracao(FaseDaOnda.Subida, 2).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        string pico = ((long)tonto.Duracao(FaseDaOnda.Pico, 2).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        EventoDoLog agendada = EsperarDesde(b, marca, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "onda agendada");
        Afirmar.Igual(subida, agendada["atrasoMs"], "um disparo único com a duração da subida");
        EventoDoLog disparada = EsperarDesde(b, marca, e => e.Chave == "ONDA" && e["disparada"] == "sim", 6000, "o temporizador da onda disparou");
        Afirmar.Igual(agendada["geracao"], disparada["geracao"], "o disparo leva a geração agendada");
        double segundos = (disparada.Instante - agendada.Instante).TotalSeconds;
        Afirmar.Verdadeiro(segundos >= 0.9 && segundos < 3, $"disparou depois da subida: {segundos:0.000} s");
        EventoDoLog avancou = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer", 2000, "ITEM_EFFECT_TIMER no núcleo");
        Afirmar.Contem("Tonto/Subida/2 -> Tonto/Pico/2", avancou["regra"]);
        EventoDoLog reagendada = EsperarDesde(b, marca, e => e.Chave == "ONDA" && e["agendada"] == "sim" && e["geracao"] != agendada["geracao"], 2000, "a próxima fase reagendada");
        Afirmar.Igual(pico, reagendada["atrasoMs"], "outro disparo único, com a duração do nível do pico");
        Afirmar.Igual(1, BuzzyEmTeste.EventosDesde(marca).Count(e => e.Chave == "ONDA" && e["disparada"] == "sim"), "um disparo por agendamento: nada periódico");

        // Sair com o disparo do pico pendente: CANCELAR_ONDA antes do encerramento, e nenhum disparo depois.
        long marcaSaida = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Igual(0, b.FecharPorWmClose(), "sai com código 0");
        List<EventoDoLog> saida = BuzzyEmTeste.EventosDesde(marcaSaida);
        int cancelada = saida.FindIndex(e => e.Chave == "ONDA" && e["cancelada"] == "sim");
        int encerrando = saida.FindIndex(e => e.Chave == "ENCERRANDO");
        Afirmar.Verdadeiro(cancelada >= 0 && saida[cancelada]["pendente"] == "sim", "o disparo pendente foi cancelado ao sair");
        Afirmar.Verdadeiro(encerrando > cancelada, "antes do encerramento");
        Afirmar.Falso(saida.Any(e => e.Chave == "ONDA" && e["disparada"] == "sim"), "nenhum disparo depois de sair");
    }

    [Teste]
    public void Paranoia_MisturaComSintetica_OSorteioSaiNaBala_OlhaProTetoComOSuor_EAAguaAcalmaUmPasso()
    {
        // A paranoia (pedidos do usuário de 2026-10-01, DEC-028), de desenho animado: pausado, com a semente em que o primeiro
        // sorteio do gerador da paranoia sai, a vodka e depois a bala, soltas nele. A vodka sozinha (álcool) não sorteia nada;
        // a bala, droga sintética na regra do jogo, fecha a mistura com sintética, e o primeiro sorteio sai: a onda Paranoico
        // vai para a frente. No fim do uso que a começou, ele olha pro teto na hora (a pose da arte, com a cara dela e o suor
        // por cima); depois, parado, a cara paranoica com o suor. A água acalma um passo: o pico vira queda.
        ulong semente = SementeEmQueOPrimeiroSorteioDaParanoiaSai();
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(semente: semente);
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        DadosDaOnda paranoia = TabelaDoTamagotchi.DaOnda(Onda.Paranoico);
        string Ms(TimeSpan t) => ((long)t.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

        var regras = new List<string>();
        long marcaDaBala = 0;
        foreach ((char tecla, string nome) in new[] { ('v', "Vodka"), ('a', "Bala") })
        {
            long marcaDoItem = BuzzyEmTeste.MarcaDoLog();
            ItemNaTela item = Invocar(b, () => AbrirPeloPersonagem(b, personagem), tecla, nome);
            (_, PontoPx opaco) = EsperarParado(b, marcaDoItem, item);
            marcaDaBala = BuzzyEmTeste.MarcaDoLog();
            Arrastar(b, item, opaco, personagem);
            regras.Add(EsperarDesde(b, marcaDaBala, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000, $"{nome} solto nele: USING")["regra"]);
            EsperarDesde(b, marcaDaBala, e => e.Chave == "NUCLEO" && e["de"] == "Settling" && e["para"] == "Idle", 8000, $"o fim do uso de {nome}");
        }
        Afirmar.Falso(regras[0].Contains("paranoia", StringComparison.Ordinal), $"a vodka sozinha (álcool) não sorteia nem começa a paranoia: {regras[0]}");
        Afirmar.Contem("Engolir Bala; a paranoia começa: Paranoico/Subida/1", regras[1]);
        // O sorteio, numa linha à parte da regra (a que também mostra o sorteio que não sai): só um, o da bala, que fechou a
        // mistura com a vodka e saiu; a vodka sozinha não sorteou.
        EventoDoLog[] sorteios = [.. BuzzyEmTeste.EventosDesde(inicio).Where(e => e.Chave == "PARANOIA")];
        Afirmar.Igual(1, sorteios.Length, "um sorteio só no log, o da bala");
        Afirmar.Igual(("Bala", "1 em 8", "sim", "2", "Vodka,Bala"), (sorteios[0]["item"], sorteios[0]["chance"], sorteios[0]["saiu"], sorteios[0]["substancias"], sorteios[0]["distintas"]),
            "a linha do sorteio: a bala, 1 em 8, saiu, com a vodka no episódio");

        // A onda, em disparos únicos com a duração da tabela: a subida e, depois dela, o pico do nível 1. Tudo é procurado
        // depois do soltar da bala: o temporizador da onda anterior (o bêbado) pode ter disparado no meio do arraste.
        int soltou = BuzzyEmTeste.EventosDesde(marcaDaBala).FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using");
        int iSubida = EsperarIndice(b, marcaDaBala, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "a subida da paranoia agendada", aPartirDe: soltou);
        EventoDoLog subida = BuzzyEmTeste.EventosDesde(marcaDaBala)[iSubida];
        Afirmar.Igual(Ms(paranoia.Duracao(FaseDaOnda.Subida, 1)), subida["atrasoMs"], "a subida da paranoia, um disparo único");
        EsperarDesde(b, marcaDaBala, e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer" && e["regra"].Contains("Paranoico/Subida/1 -> Paranoico/Pico/1", StringComparison.Ordinal),
            4000, "a subida vira pico", aPartirDe: soltou);
        EventoDoLog pico = EsperarDesde(b, marcaDaBala, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "o pico agendado", aPartirDe: iSubida);
        Afirmar.Igual(Ms(paranoia.Duracao(FaseDaOnda.Pico, 1)), pico["atrasoMs"], "o pico do nível 1, um disparo único");

        // No fim do uso que a começou, livre e no chão, ele olha pro teto na hora, sem sorteio: a pose da arte, com a cara
        // dela, e o suor da paranoia por cima. Acabado o gesto, parado, a cara paranoica, com o suor.
        EventoDoLog olhou = EsperarDesde(b, marcaDaBala, e => e.Chave == "NUCLEO" && e["de"] == "Idle" && e["para"] == "Idle" && e["regra"].Contains("a paranoia começou", StringComparison.Ordinal),
            4000, "o olhar pro teto no começo da paranoia");
        Afirmar.Igual(("Tick", "IDLE: a paranoia começou, gesto OlharProTeto"), (olhou["evento"], olhou["regra"]), "o gesto no fim do uso, sem a agenda");
        EventoDoLog teto = EsperarDesde(b, marcaDaBala, e => e.Chave == "SPRITE" && e["pose"] == "olharproteto", 3000, "o quadro do olhar pro teto");
        Afirmar.Igual(("-", "-", "Suor"), (teto["expressao"], teto["item"], teto["efeito"]), "olhar pro teto: a cara da pose, sem item e com o suor por cima");
        EventoDoLog parado = EsperarDesde(b, marcaDaBala, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["expressao"] == "paranoico", 5000, "parado com a cara paranoica");
        Afirmar.Igual(("Suor", "0"), (parado["efeito"], parado["fase"]), "parado com a cara paranoica e o suor, na fase parada (relógio desligado)");

        // A água acalma um passo: o pico do nível 1 vira a queda, que recomeça com a duração dela.
        long marcaDaAgua = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela agua = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'g', "Agua");
        (_, PontoPx opacoDaAgua) = EsperarParado(b, marcaDaAgua, agua);
        long marcaDoAlivio = BuzzyEmTeste.MarcaDoLog();
        Arrastar(b, agua, opacoDaAgua, personagem);
        int bebeu = EsperarIndice(b, marcaDoAlivio, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000, "a água solta nele");
        Afirmar.Contem("Beber Agua; alivia Paranoico/Pico/1 -> Paranoico/Queda/1", BuzzyEmTeste.EventosDesde(marcaDoAlivio)[bebeu]["regra"]);
        EventoDoLog queda = EsperarDesde(b, marcaDoAlivio, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "a queda agendada", aPartirDe: bebeu);
        Afirmar.Igual(Ms(paranoia.Duracao(FaseDaOnda.Queda, 1)), queda["atrasoMs"], "a queda, um disparo único com a duração cheia dela");
        Afirmar.Igual(1, BuzzyEmTeste.EventosDesde(inicio).Count(e => e.Chave == "PARANOIA"), "a água (alívio) não sorteia: o sorteio da bala continua o único");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(inicio).Any(e => e.Chave == "ERRO"), "sem erro");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void BaseadoPorContaPropria_FumaSozinho_FicaChapado_SemJanelaDeItem_EOCliqueInterrompe()
    {
        // O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10; DEC-028), de desenho animado: com a agenda
        // ligada e a semente em que a primeira decisão, cedo, é fumar (SementesDoBaseado, pela simulação do núcleo com a
        // configuração do aplicativo e a topologia desta máquina), ele fuma sozinho, sem nenhum item no mundo: nenhuma janela
        // de item aparece e nenhuma linha ITEM sai. O quadro é o do fumar, com o baseado na mão e a fumaça do chapado por
        // cima; a onda do chapado começa na subida, um disparo único. O clique no meio do uso (depois do primeiro trago, no
        // quadro fumando-3) o interrompe na hora, e a onda continua: depois da reação, parado, ele está chapado, com a fumaça.
        Topologia topologia = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(SpriteProvisorio.TamanhoLogico);
        (ulong semente, TimeSpan primeira) = SementesDoBaseado.PrimeiraEmQueFumaCedo(cfg, topologia, TimeSpan.FromSeconds(12), 5000)
            ?? throw new InvalidOperationException("Nenhuma semente até 5000 em que a primeira decisão, em até 12 s, é o baseado por conta própria.");
        Console.WriteLine($"         semente {semente}: a primeira decisão, em {primeira.TotalSeconds:0.0} s, é o baseado por conta própria");
        DadosDaOnda chapado = TabelaDoTamagotchi.DaOnda(Onda.Chapado);
        string Ms(TimeSpan t) => ((long)t.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(pausado: false, semente: semente);
        b.Esperar(e => e.Chave == "NUCLEO" && e["semente"] == semente.ToString(CultureInfo.InvariantCulture), 5000, "semente aplicada");
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        int iFumou = EsperarIndice(b, inicio, e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == "Using",
            (int)primeira.TotalMilliseconds + 5000, "o baseado por conta própria");
        EventoDoLog fumou = BuzzyEmTeste.EventosDesde(inicio)[iFumou];
        Afirmar.Igual(("Idle", "IDLE + AUTONOMY_TIMER: Fumar Baseado por conta própria"), (fumou["de"], fumou["regra"]), "a agenda o fez fumar, sem paranoia");
        EventoDoLog subida = EsperarDesde(b, inicio, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000, "a subida do chapado agendada", aPartirDe: iFumou);
        Afirmar.Igual(Ms(chapado.Duracao(FaseDaOnda.Subida, 2)), subida["atrasoMs"], "a subida do chapado, um disparo único");
        EventoDoLog quadro = EsperarDesde(b, inicio, e => e.Chave == "SPRITE" && e["pose"].StartsWith("fumando-", StringComparison.Ordinal), 3000, "o quadro do fumar", aPartirDe: iFumou);
        Afirmar.Igual(("baseado", "-", "Fumaca"), (quadro["item"], quadro["expressao"], quadro["efeito"]), "fumando, com o baseado na mão, a cara da pose e a fumaça do chapado");
        Afirmar.Falso(b.Eventos().Any(e => e.Chave == "ITEM"), "nenhuma janela de item: nenhuma linha ITEM");

        // O clique no meio do uso, e não no primeiro quadro (revisão do baseado por conta própria, achado 5): depois do primeiro
        // trago, no quadro fumando-3 (o passo 50 dos 210, 0,8 s depois do começo; o uso dura 3,5 s), o PRESS o segura na hora,
        // e o uso acaba; a onda continua.
        int iMeio = EsperarIndice(b, inicio, e => e.Chave == "SPRITE" && e["pose"] == "fumando-3", 3000, "o quadro fumando-3, no meio do uso", aPartirDe: iFumou);
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, personagem);
        int iPress = EsperarIndice(b, inicio, e => e.Chave == "NUCLEO" && e["evento"] == "Press" && e["para"] == "Pressed", 2000, "PRESS no meio do baseado", aPartirDe: iMeio);
        Afirmar.Igual("Using", BuzzyEmTeste.EventosDesde(inicio)[iPress]["de"], "o clique no meio do uso interrompeu o baseado por conta própria");
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, personagem);
        int iClique = EsperarIndice(b, inicio, e => e.Chave == "NUCLEO" && e["evento"] == "Click" && e["para"] == "Reacting", 2000, "o clique vira reação", aPartirDe: iPress);
        EventoDoLog parado = EsperarDesde(b, inicio, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["efeito"] == "Fumaca", 5000,
            "depois da reação, parado e chapado, com a fumaça", aPartirDe: iClique);
        Afirmar.Igual("0", parado["fase"], "parado, a fumaça na fase parada (relógio desligado)");
        Afirmar.Falso(b.Eventos().Any(e => e.Chave == "ONDA" && e["cancelada"] == "sim"), "a onda continua: nada cancelado");
        Afirmar.Falso(b.Eventos().Any(e => e.Chave == "ITEM"), "nenhuma janela de item, do começo ao fim");
        Afirmar.Falso(b.Eventos().Any(e => e.Chave == "ERRO"), "sem erro");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void SoltarLonge_OItemCaiDeOndeFoiSolto_SemUso()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        RetanguloPx area = Principal().AreaUtil;
        RetanguloPx corpo = b.RetanguloDaJanela();
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        (RetanguloPx noChao, PontoPx opaco) = EsperarParado(b, inicio, banana);

        // Solto no ar, 240 px acima e 300 px mais longe do personagem, do lado em que o item nasceu.
        int lado = noChao.Centro.X >= corpo.Centro.X ? 1 : -1;
        int pegadaX = opaco.X - (noChao.Esquerda + noChao.Largura / 2);
        int xDoSoltar = Math.Clamp(opaco.X + lado * 300, area.Esquerda + noChao.Largura + pegadaX, area.Direita - noChao.Largura + pegadaX);
        var soltar = new PontoPx(xDoSoltar, opaco.Y - 240);
        long marca = BuzzyEmTeste.MarcaDoLog();
        Arrastar(b, banana, opaco, soltar);
        EventoDoLog solto = EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["solto"] == banana.IdNoLog, 3000, "ITEM|solto");
        Afirmar.Igual(("ItemDragEnd", "nao", "nao"), (solto["fim"], solto["sobre"], solto["usado"]), "solto longe dele: não é usado");

        // O fim do gesto (revisão de correção, achado 1): a janela do item solta o mouse e volta para logo abaixo do
        // personagem no mesmo tratamento do soltar, que já passou quando a linha ITEM|solto existe. Sem isso, o próximo
        // clique no personagem ou noutro item iria para o item já solto.
        Afirmar.Igual((nint)0, NativoTeste.CapturaNaThreadDe(b.Janela), "depois de soltar, nenhuma janela do Buzzy fica com a captura do mouse");
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "depois de soltar, o item volta para logo abaixo do personagem");

        // Cai da coluna em que foi solto até o chão; o relógio liga na queda e desliga no pouso, e o pouso tem a linha
        // ITEM|movido…|parado=sim com o lugar final (uma por pouso).
        int ligou = EsperarIndice(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "sim", 3000, "o relógio ligou com o item caindo");
        EsperarDesde(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "nao", 5000, "o relógio desligou no pouso", aPartirDe: ligou);
        int ancoraX = xDoSoltar - pegadaX;
        var esperado = new RetanguloPx(ancoraX - noChao.Largura / 2, area.Base - noChao.Altura, ancoraX - noChao.Largura / 2 + noChao.Largura, area.Base);
        EsperarRetangulo(b, banana.Hwnd, esperado, 2000, "o item no chão, na coluna em que foi solto");
        (RetanguloPx pousou, _) = EsperarParado(b, marca, banana);
        Afirmar.Igual(esperado, pousou, "a linha do pouso traz o lugar final");
        Afirmar.Igual(1, BuzzyEmTeste.EventosDesde(marca).Count(e => e.Chave == "ITEM" && e["movido"] == banana.IdNoLog && e["parado"] == "sim"), "uma linha por pouso");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "NUCLEO" && e["para"] == "Using"), "nenhum uso");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(banana.Hwnd), "o item continua à vista, esperando");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void BotaoDireitoNumItem_AbreOMenu_ERecolherFechaAsJanelas_EOMenuDaBandejaTambemInvoca()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        RetanguloPx area = Principal().AreaUtil;
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");

        // Pelo comando do menu da bandeja (a notificação que a Shell mandaria, postada à janela de serviço deste Buzzy).
        long marcaBandeja = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela vodka = Invocar(b, () => AbrirPelaBandeja(b, new PontoPx(area.Direita - 40, area.Base - 1)), 'v', "Vodka");
        EsperarDesde(b, marcaBandeja, e => e.Chave == "MENU" && e["aberto"] == "bandeja", 1000, "o menu da vodka abriu pela bandeja");
        EsperarParado(b, inicio, banana);
        (_, PontoPx opacoDaVodka) = EsperarParado(b, inicio, vodka);

        // O botão direito num item abre o mesmo menu do personagem (crítica, C15); "Recolher itens" fecha as janelas.
        long marca = BuzzyEmTeste.MarcaDoLog();
        EventoDoLog fechado = Menu(b, () =>
        {
            b.PostarMouse(vodka.Hwnd, NativoTeste.WM_RBUTTONDOWN, 0, opacoDaVodka);
            b.PostarMouse(vodka.Hwnd, NativoTeste.WM_RBUTTONUP, 0, opacoDaVodka);
        }, "botão direito na vodka", 'i', 'r');
        Afirmar.Igual("RecolherItens", fechado["fechado"], "\"Recolher itens\"");
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["menuPedido"] == vodka.IdNoLog, 1000, "o menu foi pedido pelo item");
        EsperarDesde(b, marca, e => e.Chave == "MENU" && e["aberto"] == "personagem", 1000, "o mesmo menu do personagem");
        foreach (ItemNaTela item in new[] { banana, vodka })
            EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["removido"] == item.IdNoLog && e["motivo"] == "Recolhido", 3000, $"{item.Nome} recolhida");
        Thread.Sleep(200);
        Afirmar.Falso(NativoTeste.IsWindow(banana.Hwnd) || NativoTeste.IsWindow(vodka.Hwnd), "as duas janelas fecharam");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "o personagem continua à vista");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void EsconderEMostrar_AsJanelasDosItensAcompanham_EVoltamLogoAbaixoDoPersonagem()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela cerveja = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'c', "Cerveja");
        EsperarParado(b, inicio, cerveja);

        long marca = BuzzyEmTeste.MarcaDoLog();
        EventoDoLog fechado = Menu(b, () => AbrirPeloPersonagem(b, personagem), "esconder", 'e');
        Afirmar.Igual("AlternarVisibilidade", fechado["fechado"], "\"Esconder Buzzy\"");
        EsperarDesde(b, marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "personagem escondido");
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["escondido"] == cerveja.IdNoLog, 3000, "a janela do item escondida junto");
        Thread.Sleep(150);
        Afirmar.Falso(NativoTeste.IsWindowVisible(cerveja.Hwnd) || NativoTeste.IsWindowVisible(b.Janela), "os dois escondidos");
        Afirmar.Verdadeiro(NativoTeste.IsWindow(cerveja.Hwnd), "a janela do item foi escondida, não fechada");

        // Enquanto ele está escondido, outra janela "sempre no topo" passa à frente dos dois. A intrusa é uma janela
        // escondida do próprio processo de testes, nunca mostrada nem ativada.
        using var intrusa = new NativoTeste.JanelaIntrusa();
        intrusa.PorNoTopo();

        long marcaMostrar = BuzzyEmTeste.MarcaDoLog();
        SegundaInstancia segunda = BuzzyEmTeste.AbrirSegundaInstancia();
        Afirmar.Igual((true, (int?)CodigosDeSaida.Normal), (segunda.SaiuSozinha, segunda.Codigo), $"a segunda instância pede para mostrar e sai ({segunda.Encerramento ?? "saiu"})");
        EventoDoLog deNovo = EsperarDesde(b, marcaMostrar, e => e.Chave == "ITEM" && e["mostrado"] == cerveja.IdNoLog, 4000, "a janela do item mostrada de novo");
        Afirmar.Igual("nao", deNovo["criada"], "a mesma janela, sem criar outra");
        Afirmar.Igual(cerveja.Hwnd, b.JanelaDoLog(deNovo["hwnd"], "ITEM|hwnd"), "o mesmo HWND");
        EsperarDesde(b, marcaMostrar, e => e.Chave == "VISIVEL" && e["visivel"] == "sim", 2000, "o personagem à vista de novo");
        Thread.Sleep(200);
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(cerveja.Hwnd) && NativoTeste.IsWindowVisible(b.Janela), "os dois à vista");
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, cerveja.Hwnd), "o item volta para logo abaixo do personagem, à frente da outra janela \"sempre no topo\"");

        // Com os dois à vista, a outra janela "sempre no topo" passa à frente deles; pedir para mostrar de novo (segunda
        // instância) traz o personagem ao topo, e o item tem de ir junto, para logo abaixo dele, em vez de ficar atrás da
        // outra (revisão de correção, achado 7: a reafirmação da ordem Z depois de ReafirmarTopo).
        intrusa.PorNoTopo();
        Afirmar.Verdadeiro(NativoTeste.EstaAcima(intrusa.Hwnd, b.Janela), "premissa: a intrusa passou à frente do personagem");
        long marcaDeNovo = BuzzyEmTeste.MarcaDoLog();
        SegundaInstancia outra = BuzzyEmTeste.AbrirSegundaInstancia();
        Afirmar.Igual((true, (int?)CodigosDeSaida.Normal), (outra.SaiuSozinha, outra.Codigo), $"a segunda instância de novo ({outra.Encerramento ?? "saiu"})");
        EsperarDesde(b, marcaDeNovo, e => e.Chave == "VISIVEL" && e["visivel"] == "sim" && e["motivo"] == "segunda instância", 3000, "mostrar com ele já à vista");
        Thread.Sleep(200);
        Afirmar.Verdadeiro(NativoTeste.EstaAcima(b.Janela, intrusa.Hwnd), "o personagem voltou ao topo, à frente da intrusa");
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, cerveja.Hwnd), "o item foi junto: logo abaixo do personagem, à frente da intrusa");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void ArrastarOPersonagemComUmItemNaTela_OItemNaoSeMexe_EContinuaLogoAbaixoDele()
    {
        // Revisão de regras, lacuna 5: o arraste do personagem tem árbitro próprio; os itens à vista não são tocados por ele.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        RetanguloPx area = Principal().AreaUtil;
        RetanguloPx corpo = b.RetanguloDaJanela();
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        (RetanguloPx noChao, _) = EsperarParado(b, inicio, banana);

        // O personagem arrastado para o lado oposto ao da banana e solto no ar: cai até o chão.
        int lado = noChao.Centro.X >= corpo.Centro.X ? -1 : 1;
        int x = Math.Clamp(personagem.X + lado * 260, area.Esquerda + corpo.Largura, area.Direita - corpo.Largura);
        var alvo = new PontoPx(x, personagem.Y - 200);
        long marca = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, personagem);
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "Press" && e["para"] == "Pressed", 3000, "PRESS no personagem");
        for (int i = 1; i <= 8; i++)
        {
            b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(personagem.X + (alvo.X - personagem.X) * i / 8, personagem.Y + (alvo.Y - personagem.Y) * i / 8));
            Thread.Sleep(40);
        }
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "DragStart" && e["para"] == "Dragging", 3000, "o personagem arrastado");
        Afirmar.Igual(noChao, RetanguloDe(banana.Hwnd), "no arraste do personagem, a banana não se mexe");
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, alvo);
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "DragEnd", 3000, "o personagem solto");
        int ligou = EsperarIndice(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "sim", 3000, "o relógio ligou na queda dele");
        EsperarDesde(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "nao", 5000, "o relógio desligou quando ele pousou", aPartirDe: ligou);
        Thread.Sleep(200);

        List<EventoDoLog> ev = BuzzyEmTeste.EventosDesde(marca);
        Afirmar.Igual(area.Base, b.RetanguloDaJanela().Base, "o personagem pousou no chão");
        Afirmar.Igual(noChao, RetanguloDe(banana.Hwnd), "a banana continua no mesmo lugar");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(banana.Hwnd), "e à vista");
        Afirmar.Falso(ev.Any(e => e.Chave == "ITEM" && e.Campos.ContainsKey("movido") && e["movido"] == banana.IdNoLog), "nenhuma linha de movimento da banana");
        Afirmar.Falso(ev.Any(e => e.Chave == "NUCLEO" && e["evento"].StartsWith("Item", StringComparison.Ordinal)), "nenhum evento de item no arraste do personagem");
        Afirmar.Falso(ev.Any(e => e.Chave == "ERRO"), "sem erro");
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "a banana continua logo abaixo do personagem");
        Afirmar.Igual((nint)0, NativoTeste.CapturaNaThreadDe(b.Janela), "ninguém com a captura depois do arraste");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void MinimizadoPeloWindowsNoMeioDoArrasteDeUmItem_ACapturaSolta_OSoltarTardioNaoViraNada_EOItemVoltaNoChao()
    {
        // Revisão de correção, achado 2: o menu tira a captura antes de esconder, mas o Windows minimizar o personagem (ou a
        // sessão bloquear) chega ao núcleo com o item na mão. O núcleo encerra o gesto (LIBERAR_CAPTURA_DO_ITEM): a janela
        // solta o mouse, o árbitro dos itens esquece o gesto e o soltar que chega depois não vira nada.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        RetanguloPx area = Principal().AreaUtil;
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        (RetanguloPx noChao, PontoPx opaco) = EsperarParado(b, inicio, banana);

        // A banana na mão: botão pressionado e movimentos além do limiar de arraste, sem soltar.
        long marca = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(banana.Hwnd, NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["clique"] == banana.IdNoLog, 3000, "o botão pressionado chegou à banana");
        Thread.Sleep(80);
        for (int i = 1; i <= 3; i++)
        {
            b.PostarMouse(banana.Hwnd, NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(opaco.X - 20 * i, opaco.Y - 30 * i));
            Thread.Sleep(60);
        }
        EsperarRetangulo(b, banana.Hwnd, noChao.Deslocado(-60, -90), 2000, "a banana acompanha o cursor (arraste em curso)");
        Afirmar.Igual(banana.Hwnd, NativoTeste.CapturaNaThreadDe(b.Janela), "no arraste, a janela da banana tem a captura do mouse");

        // O Windows minimiza o personagem, sem ativar nada (SW_SHOWMINNOACTIVE, como em IntegracaoTestes): o Buzzy se esconde.
        long marcaMinimizar = BuzzyEmTeste.MarcaDoLog();
        b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
        EsperarDesde(b, marcaMinimizar, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "escondido ao ser minimizado");
        EsperarDesde(b, marcaMinimizar, e => e.Chave == "ITEM" && e["capturaLiberada"] == banana.IdNoLog, 2000, "LIBERAR_CAPTURA_DO_ITEM na banana");
        EsperarDesde(b, marcaMinimizar, e => e.Chave == "ITEM" && e["escondido"] == banana.IdNoLog, 2000, "a janela da banana escondida junto");
        Afirmar.Igual((nint)0, NativoTeste.CapturaNaThreadDe(b.Janela), "escondido no meio do arraste, a captura do mouse é solta");
        Afirmar.Verdadeiro(NativoTeste.IsWindow(banana.Hwnd) && !NativoTeste.IsWindowVisible(banana.Hwnd), "a janela da banana escondida, não fechada");

        // O soltar que chega depois (o usuário solta o botão com o Buzzy já escondido) não vira ITEM_DRAG_END nem ITEM_RELEASE.
        long marcaSoltar = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(banana.Hwnd, NativoTeste.WM_LBUTTONUP, 0, new PontoPx(opaco.X - 60, opaco.Y - 90));
        Thread.Sleep(400); // a janela não registra o soltar; com a interface ociosa, 400 ms bastam para a mensagem postada
        List<EventoDoLog> depoisDoSoltar = BuzzyEmTeste.EventosDesde(marcaSoltar);
        Afirmar.Falso(depoisDoSoltar.Any(e => e.Chave == "NUCLEO" && e["evento"] is "ItemDragEnd" or "ItemRelease"),
            "o soltar tardio não vira ITEM_DRAG_END nem ITEM_RELEASE: " + string.Join(" | ", depoisDoSoltar.Select(e => e.Linha)));
        Afirmar.Falso(depoisDoSoltar.Any(e => e.Chave == "ITEM" && e.Campos.ContainsKey("solto")), "nem fim de gesto do item (ITEM|solto)");
        Afirmar.Falso(depoisDoSoltar.Any(e => e.Chave == "ERRO"), "sem erro");

        // Mostrar de novo (a segunda instância): a mesma janela, no chão, logo abaixo do personagem.
        long marcaMostrar = BuzzyEmTeste.MarcaDoLog();
        SegundaInstancia segunda = BuzzyEmTeste.AbrirSegundaInstancia();
        Afirmar.Igual((true, (int?)CodigosDeSaida.Normal), (segunda.SaiuSozinha, segunda.Codigo), $"a segunda instância pede para mostrar e sai ({segunda.Encerramento ?? "saiu"})");
        EventoDoLog deNovo = EsperarDesde(b, marcaMostrar, e => e.Chave == "ITEM" && e["mostrado"] == banana.IdNoLog, 4000, "a banana mostrada de novo");
        Afirmar.Igual(("nao", banana.Hwnd), (deNovo["criada"], b.JanelaDoLog(deNovo["hwnd"], "ITEM|hwnd")), "a mesma janela");
        Afirmar.Igual(area.Base, EventoDoLog.Retangulo(deNovo["retangulo"]).Base, "o item largado pelo esconder fica no chão");
        Thread.Sleep(200);
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "logo abaixo do personagem");
        Afirmar.Igual((nint)0, NativoTeste.CapturaNaThreadDe(b.Janela), "ninguém com a captura");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void ReleituraDaTopologia_OItemMovidoPorForaVoltaAoLugar_ETambemNaConferenciaTardia_SemMexerNaOrdemZ()
    {
        // Fase 5, passo P9: as janelas dos itens acompanham a releitura. O núcleo reacomoda os itens pela topologia nova
        // (DEC-030); a raiz devolve ao lugar do núcleo uma janela que o Windows tenha levado para outro lugar (ao reconectar
        // um monitor, com "Lembrar locais das janelas"), na releitura e na conferência tardia dela. A ordem Z não é tocada
        // nelas: só muda quando o item aparece, no gesto sobre ele e quando o personagem reaparece (DEC-028, item 22;
        // SECURITY.md 2), e a conferência tardia é um temporizador (revisão do bloco P6-P9). Posta acima do personagem por
        // outro agente, a banana fica lá.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        (RetanguloPx noChao, _) = EsperarParado(b, inicio, banana);
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "premissa: o item logo abaixo do personagem");

        // Outro agente (aqui, o teste) leva a janela da banana para longe e para o topo do grupo "sempre no topo", acima do
        // personagem, sem passar pelo núcleo.
        RetanguloPx fora = noChao.Deslocado(-200, -150);
        Afirmar.Verdadeiro(b.MoverPorFora(banana.Hwnd, NativoTeste.HWND_TOPMOST, fora.Esquerda, fora.Topo, 0, "mover a banana para o topo"), "a banana movida por fora");
        Afirmar.Igual(fora, RetanguloDe(banana.Hwnd), "premissa: a banana saiu do lugar");
        Afirmar.Diferente(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "premissa: a banana saiu de logo abaixo do personagem");

        // A releitura da topologia (sem mudança real) devolve a banana ao lugar do núcleo, sem mexer na ordem Z.
        long marca = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Verdadeiro(b.Postar(b.Servico, NativoTeste.WM_DISPLAYCHANGE, 32, (nint)(1920 | (1080 << 16)), "WM_DISPLAYCHANGE"), "WM_DISPLAYCHANGE postada");
        EventoDoLog reaplicado = EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["reaplicado"] == banana.IdNoLog, 3000, "a banana reaplicada na releitura");
        Afirmar.Igual(("WM_DISPLAYCHANGE", fora, noChao), (reaplicado["motivo"], EventoDoLog.Retangulo(reaplicado["real"]), EventoDoLog.Retangulo(reaplicado["nucleo"])),
            "o motivo, onde a janela estava e o lugar do núcleo");
        EventoDoLog releitura = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["motivo"] == "WM_DISPLAYCHANGE", 2000, "a releitura");
        EsperarRetangulo(b, banana.Hwnd, noChao, 2000, "a banana de volta ao chão, onde o núcleo a pôs");
        Afirmar.Diferente(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "a releitura não reafirma a ordem Z: a banana continua acima do personagem");

        // A conferência tardia faz o mesmo com o lugar, 1,5 s depois da releitura, e também não mexe na ordem Z.
        long marcaTardia = BuzzyEmTeste.MarcaDoLog();
        RetanguloPx deNovo = noChao.Deslocado(150, -100);
        Afirmar.Verdadeiro(b.MoverPorFora(banana.Hwnd, 0, deNovo.Esquerda, deNovo.Topo, NativoTeste.SWP_NOZORDER, "mover a banana"), "a banana movida por fora depois da releitura");
        EventoDoLog tardio = EsperarDesde(b, marcaTardia, e => e.Chave == "ITEM" && e["reaplicado"] == banana.IdNoLog, 4000, "a banana reaplicada pela conferência tardia");
        Afirmar.Igual("reafirmação tardia", tardio["motivo"], "o motivo da reaplicação");
        double ms = (tardio.Instante - releitura.Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(ms >= 1400 && ms < 3000, $"1,5 s depois da releitura: {ms:0} ms");
        EsperarRetangulo(b, banana.Hwnd, noChao, 2000, "a banana de volta ao chão de novo");
        Afirmar.Diferente(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "a conferência tardia (um temporizador) não reafirma a ordem Z");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "ERRO"), "sem erro");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }


    [Teste]
    public void SairComItensNaTelaEUmNaMao_Codigo0_ACapturaSoltaENenhumaJanelaFicaViva()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx personagem = Preparar(b);
        long inicio = BuzzyEmTeste.MarcaDoLog();
        ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
        ItemNaTela cafe = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'f', "Cafe");
        EsperarParado(b, inicio, banana);
        (RetanguloPx noChao, PontoPx opaco) = EsperarParado(b, inicio, cafe);

        // O café fica na mão: botão pressionado e um movimento além do limiar de arraste, sem soltar.
        long marca = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(cafe.Hwnd, NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["clique"] == cafe.IdNoLog, 3000, "o botão pressionado chegou ao café");
        Thread.Sleep(80);
        b.PostarMouse(cafe.Hwnd, NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(opaco.X - 30, opaco.Y - 40));
        EsperarRetangulo(b, cafe.Hwnd, noChao.Deslocado(-30, -40), 2000, "o café acompanha o cursor (arraste em curso)");
        // No gesto, o item vai para cima do personagem, para não sumir atrás dele (crítica, L17); o outro fica abaixo dele.
        Afirmar.Verdadeiro(NativoTeste.PosicoesAbaixo(cafe.Hwnd, b.Janela) >= 1, "no gesto, o café fica acima do personagem na ordem Z");
        Afirmar.Igual(1, NativoTeste.PosicoesAbaixo(b.Janela, banana.Hwnd), "a banana, parada, continua logo abaixo do personagem");

        // Sair no meio do arraste: o núcleo solta a captura do item, e a raiz fecha todas as janelas.
        long marcaSaida = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Igual(0, b.FecharPorWmClose(), "sai com código 0");
        List<EventoDoLog> saida = BuzzyEmTeste.EventosDesde(marcaSaida);
        Afirmar.Verdadeiro(saida.Any(e => e.Chave == "ITEM" && e["capturaLiberada"] == cafe.IdNoLog), "a captura do café foi solta ao sair");
        int encerrando = saida.FindIndex(e => e.Chave == "ENCERRANDO");
        int fechadas = saida.FindIndex(e => e.Chave == "ITEM" && e.Campos.ContainsKey("fechadas"));
        Afirmar.Verdadeiro(encerrando >= 0 && fechadas > encerrando, "as janelas dos itens fecham no encerramento");
        Afirmar.Igual("2", saida[fechadas]["fechadas"], "as duas janelas de item");
        Afirmar.Verdadeiro(saida.Any(e => e.Chave == "FIM" && e["codigo"] == "0"), "FIM registrado com código 0");
        Afirmar.Falso(NativoTeste.IsWindow(banana.Hwnd) || NativoTeste.IsWindow(cafe.Hwnd) || NativoTeste.IsWindow(b.Janela), "nenhuma janela do Buzzy fica viva");
    }

    // A chave "Conteúdo adulto" (DEC-033) pelo menu de verdade. Com a banana e a vodka na tela, A desliga: a janela da vodka
    // sai como recolhida, a da banana fica; o menu seguinte mostra a chave desmarcada e o submenu "Itens" só com os quatro de
    // alívio (14 rostos e 4 itens como ícones), e V lá não acha nada. A escolha vai ao settings.json do perfil de teste, com
    // atraso, e volta ao reabrir; A religa. Tudo por mensagens postadas às janelas do próprio Buzzy.
    [Teste]
    public void ConteudoAdulto_PeloMenu_TiraAVodka_EscondeOsAdultos_EVoltaAoReabrir()
    {
        string icones = System.Windows.SystemParameters.HighContrast ? "0" : "18";
        using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar())
        {
            PontoPx personagem = Preparar(b);
            long inicio = BuzzyEmTeste.MarcaDoLog();
            ItemNaTela banana = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'b', "Banana");
            ItemNaTela vodka = Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'v', "Vodka");
            EsperarParado(b, inicio, banana);
            EsperarParado(b, inicio, vodka);

            long marca = BuzzyEmTeste.MarcaDoLog();
            EventoDoLog desligou = Menu(b, () => AbrirPeloPersonagem(b, personagem), "desligar o conteúdo adulto", 'a');
            Afirmar.Igual("ConteudoAdulto", desligou["fechado"], "o menu escolheu \"Conteúdo adulto\"");
            EventoDoLog nucleo = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetAdultContent", 3000, "CMD_SET_ADULT_CONTENT no núcleo");
            Afirmar.Igual(("menu", "CMD_SET_ADULT_CONTENT: desligado"), (nucleo["motivo"], nucleo["regra"]), "desligado pelo menu");
            EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["removido"] == vodka.IdNoLog && e["motivo"] == "Recolhido", 3000, "a vodka recolhida");
            Thread.Sleep(200);
            Afirmar.Falso(NativoTeste.IsWindow(vodka.Hwnd), "a janela da vodka fechou");
            Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(banana.Hwnd), "a banana continua na tela");
            Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "ITEM" && e["removido"] == banana.IdNoLog), "a banana não saiu");

            long marcaDoMenu = BuzzyEmTeste.MarcaDoLog();
            EventoDoLog semVodka = MenuSemEscolhaObrigatoria(b, () => AbrirPeloPersonagem(b, personagem), "V com o conteúdo adulto desligado", 'i', 'v');
            EventoDoLog exibindo = EsperarDesde(b, marcaDoMenu, e => e.Chave == "MENU" && e["exibindo"] == "sim", 1000, "o menu desligado");
            Afirmar.Igual(("nao", icones), (exibindo["conteudoAdulto"], exibindo["icones"]), "a chave desmarcada; 14 rostos e 4 itens");
            Afirmar.Igual("Nenhum", semVodka["fechado"], "V não acha a vodka");
            Afirmar.Falso(BuzzyEmTeste.EventosDesde(marcaDoMenu).Any(e => e.Chave == "ITEM" && e.Campos.ContainsKey("mostrado")), "nenhum item novo");

            EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["pedido"] == "preferencias" && e["evento"] == "CmdSetAdultContent", 3000, "o pedido de gravação");
            EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["gravado"] == "sim" && e["motivo"] == "atraso", 8000, "a escolha gravada com atraso");
            Afirmar.Igual(0, b.FecharPorWmClose());
        }

        using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(limpar: false))
        {
            PontoPx personagem = Preparar(b);
            long marca = BuzzyEmTeste.MarcaDoLog();
            EventoDoLog religou = Menu(b, () => AbrirPeloPersonagem(b, personagem), "religar o conteúdo adulto", 'a');
            EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 1000, "o menu ao reabrir");
            Afirmar.Igual("nao", exibindo["conteudoAdulto"], "reaberto, continua desligado");
            Afirmar.Igual("ConteudoAdulto", religou["fechado"], "o menu escolheu \"Conteúdo adulto\"");
            EventoDoLog nucleo = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetAdultContent", 3000, "CMD_SET_ADULT_CONTENT no núcleo");
            Afirmar.Igual("CMD_SET_ADULT_CONTENT: ligado", nucleo["regra"], "religado pelo menu");
            Invocar(b, () => AbrirPeloPersonagem(b, personagem), 'v', "Vodka");
            Afirmar.Igual(0, b.FecharPorWmClose());
        }
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>
    /// A primeira semente em que o primeiro sorteio do gerador da paranoia sai, com a chance da configuração do aplicativo
    /// (1 em 8), pela mesma escolha da verificação de tela (<see cref="SementesDaParanoia"/>).
    /// </summary>
    private static ulong SementeEmQueOPrimeiroSorteioDaParanoiaSai()
    {
        Chance chance = ConfiguracaoDoNucleo.DoAplicativo(SpriteProvisorio.TamanhoLogico).ChanceDaParanoia;
        return SementesDaParanoia.PrimeiraEmQueOPrimeiroSorteioSai(chance, 10_000)
            ?? throw new InvalidOperationException($"Nenhuma semente até 10000 em que o primeiro sorteio da paranoia ({chance}) sai.");
    }

    private static MonitorDoDesktop Principal()
        => Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro).Principal;

    private static PontoPx Preparar(BuzzyEmTeste b)
    {
        Afirmar.Verdadeiro(NativoTeste.ThreadEmPerMonitorV2(), "a thread do teste está em Per-Monitor V2 (coordenadas físicas, como o log do Buzzy)");
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        return EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
    }

    /// <summary>O primeiro evento depois da <paramref name="marca"/> (e depois do índice <paramref name="aPartirDe"/>) que satisfaz a condição.</summary>
    private static EventoDoLog EsperarDesde(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue, int aPartirDe = -1)
    {
        int i = EsperarIndice(b, marca, condicao, limiteMs, oQue, aPartirDe);
        return BuzzyEmTeste.EventosDesde(marca)[i];
    }

    private static int EsperarIndice(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue, int aPartirDe = -1)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            List<EventoDoLog> eventos = BuzzyEmTeste.EventosDesde(marca);
            for (int i = aPartirDe + 1; i < eventos.Count; i++)
            {
                if (condicao(eventos[i])) return i;
            }
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(30);
        }
    }

    private static void AbrirPeloPersonagem(BuzzyEmTeste b, PontoPx personagem)
    {
        b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, personagem);
        b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, personagem);
    }

    /// <summary>
    /// O botão direito no ícone da bandeja, como a Shell o entrega na versão 4: WM_RBUTTONUP e WM_CONTEXTMENU, com o id
    /// do ícone e a âncora, postados à janela de serviço deste Buzzy (PID conferido). É notificação simulada, não clique.
    /// </summary>
    private static void AbrirPelaBandeja(BuzzyEmTeste b, PontoPx ancora)
    {
        Afirmar.Igual((uint)b.Processo.Id, NativoTeste.PidDe(b.Servico), "a janela de serviço é deste Buzzy");
        nint wParam = NativoTeste.MakeLParam(ancora.X, ancora.Y);
        foreach (int evento in new[] { NativoTeste.WM_RBUTTONUP, NativoTeste.WM_CONTEXTMENU })
            Afirmar.Verdadeiro(b.Postar(b.Servico, Bandeja.MensagemDeRetorno, wParam, NativoTeste.MakeLParam(evento, (int)Bandeja.IdDoIcone), $"notificação 0x{evento:X4} da bandeja"), $"notificação 0x{evento:X4} da bandeja postada");
    }

    /// <summary>
    /// Abre o menu (<paramref name="abrir"/>), espera o dono dele no log, confere que é deste Buzzy e escolhe pelas teclas
    /// de acesso, como WM_CHAR postado ao dono, cuja fila o laço modal do menu lê. Devolve a linha MENU|fechado.
    /// </summary>
    private static EventoDoLog Menu(BuzzyEmTeste b, Action abrir, string oQue, params char[] teclas)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        abrir();
        EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 4000, $"menu exibido ({oQue})");
        nint dono = b.JanelaDoLog(exibindo["dono"], "MENU|dono");
        foreach (char tecla in teclas) b.PostarChar(dono, tecla);
        EventoDoLog fechado = EsperarDesde(b, marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 4000, $"menu fechado pelas teclas {string.Join(" e ", teclas)} ({oQue})");
        Afirmar.Igual(fechado["bitmapsCriados"], fechado["bitmapsApagados"], $"{oQue}: a abertura apagou os bitmaps que criou");
        return fechado;
    }

    /// <summary>
    /// Como <see cref="Menu"/>, para teclas que podem não escolher nada (um item desabilitado): se o menu não fechar em
    /// 800 ms, fecha com WM_CANCELMODE ao dono. Devolve a linha MENU|fechado.
    /// </summary>
    private static EventoDoLog MenuSemEscolhaObrigatoria(BuzzyEmTeste b, Action abrir, string oQue, params char[] teclas)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        abrir();
        EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 4000, $"menu exibido ({oQue})");
        nint dono = b.JanelaDoLog(exibindo["dono"], "MENU|dono");
        foreach (char tecla in teclas) b.PostarChar(dono, tecla);
        Thread.Sleep(800);
        // O dono do menu só existe enquanto o menu está aberto: a mensagem só vai se ele ainda for deste Buzzy.
        if (!BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado")) && NativoTeste.PidDe(dono) == (uint)b.Processo.Id)
            Afirmar.Verdadeiro(b.Postar(dono, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE ao dono do menu");
        EventoDoLog fechado = EsperarDesde(b, marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 4000, $"menu fechado ({oQue})");
        Afirmar.Igual(fechado["bitmapsCriados"], fechado["bitmapsApagados"], $"{oQue}: a abertura apagou os bitmaps que criou");
        return fechado;
    }

    /// <summary>Invoca um item pelo submenu "Itens" (I e a tecla do item) e devolve a janela dele, conferida como deste Buzzy.</summary>
    private static ItemNaTela Invocar(BuzzyEmTeste b, Action abrir, char tecla, string nome)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        EventoDoLog fechado = Menu(b, abrir, $"invocar {nome}", 'i', tecla);
        Afirmar.Igual(("Item", nome), (fechado["fechado"], fechado["argumento"]), $"o menu escolheu {nome}");
        EventoDoLog mostrado = EsperarDesde(b, marca, e => e.Chave == "ITEM" && e.Campos.ContainsKey("mostrado") && e["item"] == nome, 4000, $"janela de {nome} mostrada");
        int id = int.Parse(mostrado["mostrado"], CultureInfo.InvariantCulture);
        return new ItemNaTela(id, nome, b.JanelaDoLog(mostrado["hwnd"], "ITEM|hwnd"), mostrado);
    }

    /// <summary>
    /// Espera o item parar no chão da área útil e devolve o retângulo e o ponto opaco dele lá, pela linha
    /// ITEM|movido…|parado=sim, que sai uma vez por pouso mesmo quando o passo do pouso não move a janela (revisão de
    /// correção, achado 5: antes, ela faltava quando o último passo no ar já arredondava para o chão). Confere que o pouso é
    /// no chão da área útil e que a janela está onde o log diz.
    /// </summary>
    private static (RetanguloPx Retangulo, PontoPx Opaco) EsperarParado(BuzzyEmTeste b, long marca, ItemNaTela item)
    {
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["movido"] == item.IdNoLog && e["parado"] == "sim", 6000, $"{item.Nome} parado no chão (ITEM|movido…|parado=sim)");
        EventoDoLog parado = BuzzyEmTeste.EventosDesde(marca).Last(e => e.Chave == "ITEM" && e["movido"] == item.IdNoLog && e["parado"] == "sim");
        RetanguloPx ret = EventoDoLog.Retangulo(parado["retangulo"]);
        Afirmar.Igual(Principal().AreaUtil.Base, ret.Base, $"{item.Nome} parado no chão da área útil");
        Afirmar.Igual(ret, RetanguloDe(item.Hwnd), $"a janela de {item.Nome} está onde o log diz");
        return (ret, EventoDoLog.Ponto(parado["pontoOpaco"]));
    }

    /// <summary>Espera o primeiro plano sair do dono do menu e devolve a janela em primeiro plano, que não pode ser deste Buzzy.</summary>
    private static nint FrenteDepoisDoMenu(BuzzyEmTeste b)
    {
        var fim = DateTime.UtcNow.AddSeconds(2);
        while (b.FrenteEhDesteBuzzy() && DateTime.UtcNow < fim) Thread.Sleep(30);
        Afirmar.Falso(b.FrenteEhDesteBuzzy(), "depois de o menu fechar, nenhuma janela do Buzzy fica em primeiro plano");
        nint frente = NativoTeste.GetForegroundWindow();
        Console.WriteLine($"         primeiro plano depois do menu: {(frente == 0 ? "nenhuma janela" : "janela de outro processo (não identificada de propósito)")}");
        return frente;
    }

    /// <summary>
    /// Arrasta o item da janela dada por mensagens postadas a ela: o botão pressionado em <paramref name="de"/>, oito
    /// movimentos com o botão até <paramref name="ate"/> e o soltar lá. A cada movimento, o ponto vira coordenada de cliente
    /// pela posição atual da janela, que acompanha o cursor.
    /// </summary>
    private static void Arrastar(BuzzyEmTeste b, ItemNaTela item, PontoPx de, PontoPx ate)
    {
        const int Passos = 8;
        long marca = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(item.Hwnd, NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, de);
        EsperarDesde(b, marca, e => e.Chave == "ITEM" && e["clique"] == item.IdNoLog && e["botao"] == "esquerdo", 3000, $"o botão pressionado chegou a {item.Nome}");
        Thread.Sleep(80);
        for (int i = 1; i <= Passos; i++)
        {
            b.PostarMouse(item.Hwnd, NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, new PontoPx(de.X + (ate.X - de.X) * i / Passos, de.Y + (ate.Y - de.Y) * i / Passos));
            Thread.Sleep(60);
        }
        b.PostarMouse(item.Hwnd, NativoTeste.WM_LBUTTONUP, 0, ate);
    }

    private static RetanguloPx RetanguloDe(nint janela)
    {
        NativoTeste.GetWindowRect(janela, out NativoTeste.RECT r);
        return new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom);
    }

    private static void EsperarRetangulo(BuzzyEmTeste b, nint janela, RetanguloPx esperado, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        RetanguloPx atual;
        while ((atual = RetanguloDe(janela)) != esperado)
        {
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando {oQue}: janela em {atual}, esperado {esperado}.");
            Thread.Sleep(20);
        }
    }
}
