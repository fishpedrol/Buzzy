using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O gerente das janelas dos itens (DEC-028, passo T8; crítica, C19 e L17), com janelas falsas e efeitos montados à mão,
/// sem janela de verdade: cria, move, mostra, esconde e fecha pelas ordens do núcleo; pula um MOVER_ITEM só quando há
/// outro do mesmo Id adiante no lote; ignora o que chega para um Id que não conhece; e cuida da ordem Z (o item fica
/// abaixo do personagem, e no topo durante o gesto sobre ele), sempre por evento.
/// </summary>
internal sealed class GerenteDosItensTestes
{
    private const nint HwndDoPersonagem = 77;

    private static readonly MonitorDoDesktop M96 = new("m1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1040), 96, true);
    private static readonly MonitorDoDesktop M144 = new("m2", new RetanguloPx(1920, 0, 3840, 1080), new RetanguloPx(1920, 0, 3840, 1040), 144, false);

    private sealed class Cenario
    {
        internal Cenario(nint personagem = HwndDoPersonagem, Func<int, bool>? emRepouso = null)
        {
            Gerente = new GerenteDosItens(id =>
            {
                var janela = new JanelaDoItemFalsa(id);
                Criadas.Add(janela);
                return janela;
            }, () => personagem, emRepouso);
        }

        internal GerenteDosItens Gerente { get; }

        internal List<JanelaDoItemFalsa> Criadas { get; } = [];

        internal JanelaDoItemFalsa Janela(int id) => Criadas.Single(j => j.Id == id);

        /// <summary>O mesmo laço da raiz (ProcessarFilaDoNucleo): pula um MOVER_ITEM com outro do mesmo Id adiante.</summary>
        internal void Lote(params Efeito[] lote)
        {
            for (int i = 0; i < lote.Length; i++)
            {
                if (GerenteDosItens.MovimentoPosterior(lote, i)) continue;
                Gerente.Executar(lote[i]);
            }
        }

        internal void Limpar()
        {
            foreach (JanelaDoItemFalsa j in Criadas) j.Chamadas.Clear();
        }
    }

    private static Posicionamento Lugar(MonitorDoDesktop m, int x, int y)
    {
        TamanhoPx tamanho = SpriteDoItem.TamanhoLogico.ParaPixels(m.Dpi);
        var ancora = new PontoPx(x, y);
        return new Posicionamento(m, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
    }

    [Teste]
    public void MostrarItem_CriaAJanela_SpriteNoDpi_LugarAntesEDepoisDeMostrar_AbaixoDoPersonagem()
    {
        var c = new Cenario();
        Posicionamento lugar = Lugar(M96, 500, 800);
        c.Gerente.Executar(new MostrarItem(1, Item.Banana, lugar));

        Afirmar.Igual(1, c.Criadas.Count, "uma janela criada");
        JanelaDoItemFalsa j = c.Janela(1);
        Afirmar.Sequencia(
            ["sprite 48x48", $"retangulo {lugar.Retangulo}", "mostrar", $"retangulo {lugar.Retangulo}", $"abaixo de {HwndDoPersonagem}"],
            j.Chamadas, "desenho, lugar, mostrar sem ativar, o lugar de novo (o WPF pode mudá-lo ao mostrar) e a ordem Z");
        Afirmar.Verdadeiro(ReferenceEquals(SpriteDoItem.Renderizar(Item.Banana, 96), j.Sprite), "o sprite é o da banana a 96 DPI, do cache");
        Afirmar.Igual((1, 1), (c.Gerente.Quantas, c.Gerente.Visiveis), "uma janela, à vista");
        Afirmar.Igual(1, j.Ouvintes, "o gerente ouve o ponteiro da janela");
    }

    [Teste]
    public void MoverItem_PulaSoQuandoHaOutroDoMesmoIdAdianteNoLote()
    {
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Agua, Lugar(M96, 100, 900)));
        c.Gerente.Executar(new MostrarItem(2, Item.Vodka, Lugar(M96, 300, 900)));
        c.Limpar();

        Posicionamento a = Lugar(M96, 110, 910), b = Lugar(M96, 310, 920), d = Lugar(M96, 130, 930);
        Efeito[] lote = [new MoverItem(1, a), new MoverItem(2, b), new MoverItem(1, d)];
        Afirmar.Sequencia([true, false, false], Enumerable.Range(0, lote.Length).Select(i => GerenteDosItens.MovimentoPosterior(lote, i)),
            "pula só o primeiro movimento do item 1; o do item 2 nunca");
        c.Lote(lote);
        Afirmar.Sequencia([$"retangulo {d.Retangulo}"], c.Janela(1).Chamadas, "o item 1 vai direto para o último lugar do lote");
        Afirmar.Sequencia([$"retangulo {b.Retangulo}"], c.Janela(2).Chamadas, "o item 2 vai para o lugar dele");
    }

    [Teste]
    public void MovimentoPosterior_ParaNoMostrarEsconderOuRemoverDoMesmoId_EOsOutrosEfeitosNuncaSaoPulados()
    {
        Posicionamento a = Lugar(M96, 10, 900), b = Lugar(M96, 20, 900);
        Efeito[][] semPulo =
        [
            [new MoverItem(1, a), new EsconderItem(1), new MoverItem(1, b)],
            [new MoverItem(1, a), new MostrarItem(1, Item.Bala, b), new MoverItem(1, b)],
            [new MoverItem(1, a), new RemoverItem(1, MotivoDaRemocao.Usado), new MoverItem(1, b)],
            [new MoverItem(1, a), new MoverItem(2, b)],
            [new MoverItem(1, a)],
        ];
        foreach (Efeito[] lote in semPulo)
            Afirmar.Falso(GerenteDosItens.MovimentoPosterior(lote, 0), $"não pula: {string.Join(", ", lote.Select(e => e.GetType().Name))}");

        // Com outro efeito do personagem ou do tempo no meio, o mesmo Id adiante ainda pula.
        Efeito[] comOutros = [new MoverItem(1, a), new MoverJanela(a), new LigarRelogio(), new EsconderItem(2), new MoverItem(1, b)];
        Afirmar.Verdadeiro(GerenteDosItens.MovimentoPosterior(comOutros, 0), "efeitos de outra janela não contam");

        // Mostrar, esconder e remover nunca são pulados, nem quando se repetem.
        Efeito[] repetidos = [new MostrarItem(3, Item.Cafe, a), new EsconderItem(3), new MostrarItem(3, Item.Cafe, b), new RemoverItem(3, MotivoDaRemocao.Recolhido), new RemoverItem(3, MotivoDaRemocao.Recolhido)];
        Afirmar.Verdadeiro(Enumerable.Range(0, repetidos.Length).All(i => !GerenteDosItens.MovimentoPosterior(repetidos, i)), "nunca pulados");
        var c = new Cenario();
        c.Lote(repetidos);
        Afirmar.Sequencia(
            ["sprite 48x48", $"retangulo {a.Retangulo}", "mostrar", $"retangulo {a.Retangulo}", $"abaixo de {HwndDoPersonagem}",
             "esconder",
             $"retangulo {b.Retangulo}", "mostrar", $"retangulo {b.Retangulo}", $"abaixo de {HwndDoPersonagem}",
             "fechar"],
            c.Janela(3).Chamadas, "mostrar, esconder, mostrar de novo na mesma janela, fechar uma vez; o segundo remover é de um Id que já saiu");
        Afirmar.Igual(0, c.Gerente.Quantas);
    }

    [Teste]
    public void EfeitosDeIdDesconhecido_SaoIgnorados()
    {
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Cerveja, Lugar(M96, 400, 900)));
        c.Limpar();

        c.Gerente.Executar(new RemoverItem(99, MotivoDaRemocao.Usado));
        c.Gerente.Executar(new MoverItem(98, Lugar(M96, 10, 10)));
        c.Gerente.Executar(new EsconderItem(97));
        c.Gerente.Executar(new LiberarCapturaDoItem(94));
        c.Gerente.TerminarGesto(96);
        c.Gerente.ComecarGesto(95);
        Afirmar.Igual((1, 1), (c.Criadas.Count, c.Gerente.Quantas), "nenhuma janela criada nem fechada");
        Afirmar.Sequencia([], c.Janela(1).Chamadas, "a janela do item 1 não foi tocada");
        Afirmar.Lanca<ArgumentException>(() => c.Gerente.Executar(new MoverJanela(Lugar(M96, 1, 1))), "só efeitos das janelas dos itens");
    }

    [Teste]
    public void RemoverItem_SoltaACapturaSemEventoEFechaAJanela()
    {
        var c = new Cenario();
        var recebidos = new List<(int, EventoDePonteiro)>();
        c.Gerente.Ponteiro += (id, e) => recebidos.Add((id, e));
        c.Gerente.Executar(new MostrarItem(1, Item.Cigarro, Lugar(M96, 400, 900)));
        c.Gerente.ComecarGesto(1);
        c.Limpar();

        c.Gerente.Executar(new RemoverItem(1, MotivoDaRemocao.Recolhido));
        JanelaDoItemFalsa j = c.Janela(1);
        Afirmar.Sequencia(["soltar", "fechar"], j.Chamadas, "solta a captura antes de fechar");
        Afirmar.Igual(0, j.Ouvintes, "o gerente deixa de ouvir a janela fechada");
        j.Disparar(new CapturaPerdida(5));
        Afirmar.Sequencia([], recebidos, "nada da janela fechada chega à raiz");
        Afirmar.Igual((0, 0), (c.Gerente.Quantas, c.Gerente.Visiveis));

        c.Gerente.Executar(new MoverItem(1, Lugar(M96, 1, 1)));
        Afirmar.Sequencia(["soltar", "fechar"], j.Chamadas, "um movimento depois de remover é ignorado");
    }

    [Teste]
    public void LiberarCapturaDoItem_SoltaOMouseEVoltaParaBaixo_AJanelaFica()
    {
        // O núcleo encerrou o gesto sobre o item por conta própria (esconder, sair, recolher ou pegar outro no meio dele):
        // a janela solta o mouse e volta para baixo do personagem; ela continua viva até o núcleo dizer o que fazer com ela.
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Baseado, Lugar(M96, 400, 900)));
        c.Gerente.ComecarGesto(1);
        c.Limpar();

        c.Gerente.Executar(new LiberarCapturaDoItem(1));
        Afirmar.Sequencia(["soltar", $"abaixo de {HwndDoPersonagem}"], c.Janela(1).Chamadas, "solta a captura e volta para baixo do personagem");
        Afirmar.Falso(c.Janela(1).Capturando, "sem captura");
        Afirmar.Igual((1, 1), (c.Gerente.Quantas, c.Gerente.Visiveis), "a janela continua, à vista");

        c.Limpar();
        c.Gerente.Executar(new LiberarCapturaDoItem(42));
        Afirmar.Sequencia([], c.Janela(1).Chamadas, "um Id desconhecido é ignorado");
        Afirmar.Falso(GerenteDosItens.MovimentoPosterior([new MoverItem(1, Lugar(M96, 1, 900)), new LiberarCapturaDoItem(1), new MoverItem(1, Lugar(M96, 2, 900))], 1),
            "LIBERAR_CAPTURA_DO_ITEM nunca é pulado");
    }

    [Teste]
    public void EsconderEMostrarDeNovo_AMesmaJanela_SemRedesenhar()
    {
        var c = new Cenario();
        Posicionamento lugar = Lugar(M96, 400, 900);
        c.Gerente.Executar(new MostrarItem(1, Item.Cocaina, lugar));
        c.Limpar();
        c.Gerente.Executar(new EsconderItem(1));
        Afirmar.Igual((1, 0), (c.Gerente.Quantas, c.Gerente.Visiveis), "escondida, mas viva");
        c.Gerente.Executar(new EsconderItem(1));
        Afirmar.Sequencia(["esconder"], c.Janela(1).Chamadas, "esconder duas vezes esconde uma");

        c.Limpar();
        Posicionamento outro = Lugar(M96, 420, 900);
        c.Gerente.Executar(new MostrarItem(1, Item.Cocaina, outro));
        Afirmar.Igual(1, c.Criadas.Count, "nenhuma janela nova");
        Afirmar.Sequencia([$"retangulo {outro.Retangulo}", "mostrar", $"retangulo {outro.Retangulo}", $"abaixo de {HwndDoPersonagem}"],
            c.Janela(1).Chamadas, "o mesmo desenho no mesmo DPI: só o lugar e mostrar");
    }

    [Teste]
    public void OutroDpi_RedesenhaOSpriteUmaVez()
    {
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Energetico, Lugar(M96, 400, 900)));
        c.Limpar();
        Posicionamento noOutro = Lugar(M144, 2400, 900), maisLa = Lugar(M144, 2500, 900);
        c.Gerente.Executar(new MoverItem(1, noOutro));
        c.Gerente.Executar(new MoverItem(1, maisLa));
        Afirmar.Sequencia(["sprite 72x72", $"retangulo {noOutro.Retangulo}", $"retangulo {maisLa.Retangulo}"], c.Janela(1).Chamadas,
            "no monitor de 144 DPI, o desenho de 72 px; depois, só o lugar");
        Afirmar.Verdadeiro(ReferenceEquals(SpriteDoItem.Renderizar(Item.Energetico, 144), c.Janela(1).Sprite), "o desenho de 144 DPI");
    }

    [Teste]
    public void Gesto_NoTopoDuranteEAbaixoDoPersonagemDepois()
    {
        // L17: abaixo do personagem, o item sumiria atrás dele justamente quando vai ser solto sobre ele.
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Md, Lugar(M96, 400, 900)));
        c.Limpar();

        c.Gerente.ComecarGesto(1);
        Afirmar.Sequencia(["capturar", "frente"], c.Janela(1).Chamadas, "no gesto: captura e vai para o topo");
        c.Limpar();
        Posicionamento arrastado = Lugar(M96, 600, 700);
        c.Gerente.Executar(new MoverItem(1, arrastado));
        Afirmar.Sequencia([$"retangulo {arrastado.Retangulo}"], c.Janela(1).Chamadas, "durante o arraste, só o lugar: a ordem Z fica");
        c.Limpar();
        c.Gerente.ReordenarAbaixoDoPersonagem();
        Afirmar.Sequencia(["frente"], c.Janela(1).Chamadas, "com o personagem reafirmado no topo, o item do gesto volta para cima dele");

        c.Limpar();
        c.Gerente.TerminarGesto(1);
        Afirmar.Sequencia(["soltar", $"abaixo de {HwndDoPersonagem}"], c.Janela(1).Chamadas, "fim do gesto: solta e volta para baixo do personagem");
    }

    [Teste]
    public void ReordenarAbaixoDoPersonagem_SoAsVisiveis_ESemPersonagemNada()
    {
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Cafe, Lugar(M96, 100, 900)));
        c.Gerente.Executar(new MostrarItem(2, Item.Bala, Lugar(M96, 300, 900)));
        c.Gerente.Executar(new EsconderItem(2));
        c.Limpar();
        c.Gerente.ReordenarAbaixoDoPersonagem();
        Afirmar.Sequencia([$"abaixo de {HwndDoPersonagem}"], c.Janela(1).Chamadas, "a visível vai para baixo do personagem");
        Afirmar.Sequencia([], c.Janela(2).Chamadas, "a escondida fica como está");

        var semPersonagem = new Cenario(personagem: 0);
        semPersonagem.Gerente.Executar(new MostrarItem(1, Item.Cafe, Lugar(M96, 100, 900)));
        Afirmar.Falso(semPersonagem.Janela(1).Chamadas.Any(ch => ch.StartsWith("abaixo", StringComparison.Ordinal)), "sem a janela do personagem, nenhuma ordem Z");
    }

    [Teste]
    public void ReafirmarLugares_AJanelaMovidaPorForaVoltaAoLugarDoNucleo_SemMexerNaOrdemZ()
    {
        // Fase 5, passo P9 (D14 do desenho dos monitores): depois da releitura, o Windows pode levar uma janela de volta a um
        // monitor reconectado; quem decide o lugar é o núcleo. A ordem Z não é tocada: ela só muda quando o item aparece, no
        // gesto sobre ele e quando o personagem reaparece (DEC-028, item 22; SECURITY.md 2), e a conferência tardia é um
        // temporizador (revisão do bloco P6-P9: segurança, achado 1; correção, achado 4). A janela é movida sem mudar a
        // ordem Z (SWP_NOZORDER).
        var c = new Cenario();
        Posicionamento l1 = Lugar(M96, 100, 1040), l2 = Lugar(M96, 300, 1040), l3 = Lugar(M96, 500, 1040), l4 = Lugar(M96, 700, 900);
        c.Gerente.Executar(new MostrarItem(1, Item.Banana, l1));
        c.Gerente.Executar(new MostrarItem(2, Item.Cafe, l2));
        c.Gerente.Executar(new MostrarItem(3, Item.Agua, l3));
        c.Gerente.Executar(new MostrarItem(4, Item.Vodka, l4));
        c.Gerente.Executar(new EsconderItem(3));
        c.Gerente.ComecarGesto(4);
        c.Janela(1).MovidaPorFora = l1.Retangulo.Deslocado(-1920, 0); // levada a outro monitor
        c.Janela(3).MovidaPorFora = l3.Retangulo.Deslocado(50, 0);    // escondida: fica como está
        c.Janela(4).MovidaPorFora = l4.Retangulo.Deslocado(10, 10);   // no gesto: segue o cursor, e o lugar fica
        c.Limpar();

        IReadOnlyList<int> reaplicados = c.Gerente.ReafirmarLugares("WM_DISPLAYCHANGE");
        Afirmar.Sequencia([1], reaplicados, "só a janela à vista, fora do gesto, que saiu do lugar");
        Afirmar.Sequencia([$"retangulo {l1.Retangulo}"], c.Janela(1).Chamadas, "de volta ao lugar do núcleo, sem mexer na ordem Z");
        Afirmar.Sequencia([], c.Janela(2).Chamadas, "no lugar: nada");
        Afirmar.Sequencia([], c.Janela(3).Chamadas, "a escondida fica como está");
        Afirmar.Sequencia([], c.Janela(4).Chamadas, "a do gesto fica onde o cursor a pôs, e na ordem Z em que está");

        c.Limpar();
        Afirmar.Sequencia([], c.Gerente.ReafirmarLugares("reafirmação tardia"), "de volta ao lugar: nada a reaplicar");
        Afirmar.Verdadeiro(Enumerable.Range(1, 4).All(i => c.Janela(i).Chamadas.Count == 0), "nem lugar nem ordem Z na conferência tardia");
        Afirmar.Sequencia([], new Cenario().Gerente.ReafirmarLugares("WM_DISPLAYCHANGE"), "sem janelas, nada");
    }

    [Teste]
    public void FecharTodas_NoEncerramento_CadaJanelaUmaVez()
    {
        var c = new Cenario();
        c.Gerente.Executar(new MostrarItem(1, Item.Banana, Lugar(M96, 100, 900)));
        c.Gerente.Executar(new MostrarItem(2, Item.Agua, Lugar(M96, 300, 900)));
        c.Gerente.Executar(new MostrarItem(3, Item.Vodka, Lugar(M96, 500, 900)));
        c.Gerente.Executar(new EsconderItem(2));
        c.Gerente.ComecarGesto(3);
        c.Limpar();

        c.Gerente.FecharTodas();
        Afirmar.Sequencia(["fechar"], c.Janela(1).Chamadas);
        Afirmar.Sequencia(["fechar"], c.Janela(2).Chamadas, "a escondida também fecha");
        Afirmar.Sequencia(["soltar", "fechar"], c.Janela(3).Chamadas, "a do gesto solta a captura antes");
        Afirmar.Verdadeiro(c.Criadas.All(j => j.Ouvintes == 0), "nenhuma janela continua ouvida");
        Afirmar.Igual(0, c.Gerente.Quantas);
        c.Gerente.FecharTodas();
        Afirmar.Verdadeiro(c.Criadas.All(j => j.Chamadas.Count(ch => ch == "fechar") == 1), "fechar de novo não fecha nada");
    }

    [Teste]
    public void RegistrarPousos_UmaVezPorPouso_MesmoSemMovimentoNoPassoDoPouso()
    {
        // Revisão de correção, achado 5: a linha ITEM|movido…|parado=sim saía só quando havia um MOVER_ITEM no passo do
        // pouso; quando o último passo no ar já arredondava para o chão, ela faltava. Agora o pouso é o fato do núcleo (o
        // item parado no chão), conferido no fim de cada processamento, com o lugar em que a janela está.
        var parados = new HashSet<int>();
        var c = new Cenario(emRepouso: parados.Contains);
        Posicionamento noAr = Lugar(M96, 500, 700), noChao = Lugar(M96, 500, 1040), aoLado = Lugar(M96, 540, 1040);
        c.Gerente.Executar(new MostrarItem(1, Item.Banana, noAr));
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "caindo: nada");
        c.Gerente.Executar(new MoverItem(1, noChao));
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "a janela já no chão, mas o núcleo ainda com o item caindo");

        parados.Add(1); // o passo do pouso não moveu a janela: nenhum MOVER_ITEM
        Afirmar.Sequencia([1], c.Gerente.RegistrarPousos(), "o pouso, sem movimento no passo");
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "uma vez por pouso");

        c.Gerente.Executar(new MoverItem(1, aoLado)); // levado pelo núcleo sem sair do chão (topologia nova)
        Afirmar.Sequencia([1], c.Gerente.RegistrarPousos(), "parado em outro lugar: de novo");

        parados.Remove(1); // na mão do usuário, e solto de novo no mesmo lugar
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "na mão: nada");
        parados.Add(1);
        Afirmar.Sequencia([1], c.Gerente.RegistrarPousos(), "pousou de novo, mesmo no mesmo lugar");

        c.Gerente.Executar(new EsconderItem(1));
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "escondida: nada");
        c.Gerente.Executar(new MostrarItem(1, Item.Banana, aoLado));
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "mostrada de novo onde já estava parada: nada novo");

        // Escondida no meio da queda, o núcleo a assenta no chão; ao mostrar, ela aparece parada num lugar novo.
        c.Gerente.Executar(new MostrarItem(2, Item.Cafe, noAr));
        c.Gerente.Executar(new EsconderItem(2));
        parados.Add(2);
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "a escondida não conta");
        c.Gerente.Executar(new MostrarItem(2, Item.Cafe, Lugar(M96, 300, 1040)));
        Afirmar.Sequencia([2], c.Gerente.RegistrarPousos(), "mostrada parada num lugar novo: uma linha");

        c.Gerente.Executar(new RemoverItem(1, MotivoDaRemocao.Recolhido));
        Afirmar.Sequencia([], c.Gerente.RegistrarPousos(), "removida: nada");
        Afirmar.Sequencia([], new Cenario().Gerente.RegistrarPousos(), "sem janelas, nada");
    }

    [Teste]
    public void Ponteiro_ChegaComOIdDaJanela()
    {
        var c = new Cenario();
        var recebidos = new List<(int, EventoDePonteiro)>();
        c.Gerente.Ponteiro += (id, e) => recebidos.Add((id, e));
        c.Gerente.Executar(new MostrarItem(4, Item.Cogumelo, Lugar(M96, 100, 900)));
        c.Gerente.Executar(new MostrarItem(5, Item.Bala, Lugar(M96, 300, 900)));
        var a = new PonteiroSolto(new PontoPx(1, 2), BotaoDoPonteiro.Direito, 3);
        var b = new CapturaPerdida(4);
        c.Janela(5).Disparar(a);
        c.Janela(4).Disparar(b);
        Afirmar.Sequencia<(int, EventoDePonteiro)>([(5, a), (4, b)], recebidos);
    }
}
