using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// A lista de entradas do menu nativo, sem Windows (DEC-027; crítica, L13): ordem, ids, marcas de rádio, rostos pedidos
/// e o caminho de volta do id escolhido. A montagem no HMENU fica em <see cref="MontagemDoMenuTestes"/>, os bitmaps em
/// <see cref="BitmapsDoMenuTestes"/> e o laço do menu na integração (<c>MenuIntegracaoTestes</c>).
/// </summary>
internal sealed class MenuNativoTestes
{
    /// <summary>As 14 caras de humor na ordem de expressoes.png, com a tecla de acesso de cada uma (desenho do app, 4.6).</summary>
    private static readonly (string Nome, string Rotulo)[] Emocoes =
    [
        ("Neutro", "&Neutro"), ("Feliz", "&Feliz"), ("Rindo", "&Rindo"), ("Curioso", "&Curioso"), ("Surpreso", "&Surpreso"),
        ("Assustado", "Ass&ustado"), ("Sonolento", "S&onolento"), ("Bocejando", "&Bocejando"), ("Dormindo", "&Dormindo"),
        ("Travesso", "&Travesso"), ("Entediado", "&Entediado"), ("Pensativo", "&Pensativo"), ("Empolgado", "E&mpolgado"),
        ("Determinado", "Determ&inado"),
    ];

    /// <summary>
    /// Os 13 itens do tamagotchi (DEC-028) na ordem do enum <see cref="Item"/>, com o rótulo e a tecla de acesso de cada um:
    /// só os nomes (decisão do coordenador; desenho do app, 4.6).
    /// </summary>
    private static readonly (Item Item, string Rotulo)[] Itens =
    [
        (Item.Banana, "&Banana"), (Item.Agua, "Á&gua"), (Item.Vodka, "&Vodka"), (Item.Cerveja, "&Cerveja"), (Item.Baseado, "Ba&seado"),
        (Item.Cigarro, "C&igarro"), (Item.Cocaina, "C&ocaína"), (Item.Md, "&MD"), (Item.LancaPerfume, "&Lança-perfume"),
        (Item.Cafe, "Ca&fé"), (Item.Energetico, "E&nergético"), (Item.Cogumelo, "Cog&umelo"), (Item.Bala, "B&ala"),
    ];

    private static ModeloDoMenu Modelo(bool visivel = true, bool pausado = false, Expressao? emocao = null, bool altoContraste = false)
        => new(visivel, pausado, emocao, altoContraste, Tamagotchi: false);

    private static ModeloDoMenu ModeloComItens(bool visivel = true, int itensNaTela = 0, bool altoContraste = false, bool pausado = false, bool adulto = true)
        => new(visivel, pausado, null, altoContraste, Tamagotchi: true, ItensNaTela: itensNaTela, ConteudoAdulto: adulto);

    private static EntradaDoMenu SubmenuDosItens(IReadOnlyList<EntradaDoMenu> menu)
        => menu.Single(e => e.Tipo == TipoDeEntrada.Submenu && e.Rotulo == "&Itens");

    private static EntradaDoMenu SubmenuDaEmocao(IReadOnlyList<EntradaDoMenu> menu)
        => menu.Single(e => e.Tipo == TipoDeEntrada.Submenu);

    private static IEnumerable<EntradaDoMenu> Todas(IEnumerable<EntradaDoMenu> menu)
        => menu.SelectMany(e => new[] { e }.Concat(Todas(e.Filhas ?? [])));

    [Teste]
    public void MenuPrincipal_ComandosDeHojeEOSubmenuDaEmocaoAntesDeSair()
    {
        IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(Modelo());
        Afirmar.Sequencia(
            [
                (TipoDeEntrada.Comando, "&Esconder Buzzy", 1),
                (TipoDeEntrada.Comando, "&Pausar movimento", 3),
                (TipoDeEntrada.Separador, "", 0),
                (TipoDeEntrada.Submenu, "Emoção &dominante", 0),
                (TipoDeEntrada.Comando, "Desviar da &tela cheia", 8),
                (TipoDeEntrada.Separador, "", 0),
                (TipoDeEntrada.Comando, "&Sair", 2),
            ],
            menu.Select(e => (e.Tipo, e.Rotulo, e.Id)), "visível e com o movimento livre");

        IReadOnlyList<EntradaDoMenu> outro = MenuNativo.Entradas(Modelo(visivel: false, pausado: true));
        Afirmar.Sequencia(["&Mostrar Buzzy", "&Retomar movimento", "", "Emoção &dominante", "Desviar da &tela cheia", "", "&Sair"], outro.Select(e => e.Rotulo), "escondido e pausado");
        Afirmar.Sequencia([1, 3, 0, 0, 8, 0, 2], outro.Select(e => e.Id), "os mesmos ids");
        // Só "Desviar da tela cheia" tem marca: o modo de tela cheia vem ligado (Q-09).
        Afirmar.Falso(menu.Concat(outro).Any(e => e.Id != 8 && (e.Marcada || e.Radio || e.Desabilitada || e.Rosto is not null)), "no menu principal, nada marcado, desabilitado ou com rosto");
    }

    // "Desviar da tela cheia" (DEC-034) é um comando com a marca de seleção (não de rádio), marcado com o modo ligado; existe
    // sempre, com ou sem a chave do tamagotchi, à vista ou escondido, logo antes do último separador e de "Sair"; o id 8
    // volta ao comando.
    [Teste]
    public void ModoTelaCheia_ComandoComMarca_SempreAntesDoUltimoSeparador()
    {
        foreach (ModeloDoMenu modelo in new[] { Modelo(), Modelo(visivel: false, pausado: true), ModeloComItens(), ModeloComItens(visivel: false, adulto: false) })
        {
            List<EntradaDoMenu> menu = [.. MenuNativo.Entradas(modelo)];
            int i = menu.FindIndex(e => e.Id == 8);
            Afirmar.Igual((TipoDeEntrada.Comando, "Desviar da &tela cheia", true, false, false), (menu[i].Tipo, menu[i].Rotulo, menu[i].Marcada, menu[i].Radio, menu[i].Desabilitada), $"{modelo}: ligado, marcado e habilitado");
            Afirmar.Igual((TipoDeEntrada.Separador, "&Sair", menu.Count), (menu[i + 1].Tipo, menu[i + 2].Rotulo, i + 3), $"{modelo}: logo antes do último separador e de Sair");
            Afirmar.Falso(MenuNativo.Entradas(modelo with { ModoTelaCheia = false }).Single(e => e.Id == 8).Marcada, $"{modelo}: desligado, sem marca");
        }
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.ModoTelaCheia), MenuNativo.Escolha(8), "o id volta ao comando");
    }

    [Teste]
    public void SubmenuDaEmocao_AutomaticaSeparadorEOs14RostosNaOrdemDeExpressoesPng()
    {
        IReadOnlyList<EntradaDoMenu> filhas = Afirmar.NaoNulo(SubmenuDaEmocao(MenuNativo.Entradas(Modelo())).Filhas);
        Afirmar.Igual(16, filhas.Count, "Automática, separador e 14 emoções");
        Afirmar.Igual((TipoDeEntrada.Comando, "&Automática", 999, true, (string?)null), (filhas[0].Tipo, filhas[0].Rotulo, filhas[0].Id, filhas[0].Radio, filhas[0].Rosto), "Automática: rádio, sem rosto");
        Afirmar.Igual(TipoDeEntrada.Separador, filhas[1].Tipo);

        EntradaDoMenu[] emocoes = [.. filhas.Skip(2)];
        Afirmar.Sequencia(Emocoes.Select(e => e.Rotulo), emocoes.Select(e => e.Rotulo), "rótulos");
        Afirmar.Sequencia(Enumerable.Range(1000, 14), emocoes.Select(e => e.Id), "ids 1000 a 1013");
        Afirmar.Sequencia(Emocoes.Select(e => e.Nome.ToLowerInvariant()), emocoes.Select(e => e.Rosto), "rosto pedido: a chave da arte de cada cara");
        Afirmar.Verdadeiro(emocoes.All(e => e.Tipo == TipoDeEntrada.Comando && e.Radio && !e.Desabilitada && e.Filhas is null), "opções de rádio, habilitadas");

        // Todo rosto pedido existe na arte (uma chave ausente derrubaria o menu, F5) e é a célula de expressoes.png.
        foreach (EntradaDoMenu e in emocoes)
        {
            Tela rosto = IconesDoMenu.Rosto(e.Rosto!);
            Afirmar.Igual((40, 32), (rosto.Largura, rosto.Altura), e.Rosto);
        }
    }

    [Teste]
    public void MarcaDeRadio_SoNaEmocaoAtual_EmAutomaticaQuandoNaoHaEmocao()
    {
        IReadOnlyList<EntradaDoMenu> automatica = MenuNativo.Entradas(Modelo());
        Afirmar.Sequencia([999], Todas(automatica).Where(e => e.Radio && e.Marcada).Select(e => e.Id), "sem emoção dominante: só Automática");
        for (int i = 0; i < Emocoes.Length; i++)
        {
            Expressao emocao = Enum.Parse<Expressao>(Emocoes[i].Nome);
            IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(Modelo(emocao: emocao));
            Afirmar.Sequencia([1000 + i], Todas(menu).Where(e => e.Radio && e.Marcada).Select(e => e.Id), $"{emocao}: só ela marcada");
        }
    }

    [Teste]
    public void NoAplicativo_AChaveDoTamagotchiLigada_TrazOSubmenuItens()
    {
        // Passo T9: o aplicativo liga a chave (D15), e o menu que ele monta, com a chave lida da configuração do núcleo
        // (Aplicacao.ExibirMenuDoDesktop), traz o submenu "Itens" logo depois do da emoção dominante.
        bool chave = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)).Tamagotchi;
        Afirmar.Verdadeiro(chave, "a chave do tamagotchi está ligada no aplicativo");
        IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(new ModeloDoMenu(true, false, null, AltoContraste: false, Tamagotchi: chave, ItensNaTela: 0));
        Afirmar.Sequencia(["Emoção &dominante", "&Itens"], menu.Where(e => e.Tipo == TipoDeEntrada.Submenu).Select(e => e.Rotulo), "os dois submenus");
        Afirmar.Igual(13, Todas(menu).Count(e => e.Item is not null), "os 13 itens com o desenho do chão como ícone");
    }

    [Teste]
    public void SemSubmenuItensComAChaveDoTamagotchiDesligada()
    {
        // O submenu "Itens" (DEC-028, passo T8) só aparece com a chave ligada (crítica, L13); o modelo a traz desligada.
        foreach (bool visivel in new[] { true, false })
        {
            foreach (bool pausado in new[] { true, false })
            {
                // Nem com itens contados no modelo: sem a chave, o submenu não existe.
                IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(Modelo(visivel, pausado) with { ItensNaTela = 3 });
                Afirmar.Sequencia(["Emoção &dominante"], menu.Where(e => e.Tipo == TipoDeEntrada.Submenu).Select(e => e.Rotulo), "um submenu só");
                Afirmar.Sequencia(MenuNativo.Entradas(Modelo(visivel, pausado)).Select(e => (e.Tipo, e.Rotulo, e.Id)), menu.Select(e => (e.Tipo, e.Rotulo, e.Id)),
                    "o menu é o mesmo com ou sem itens contados");
                Afirmar.Falso(Todas(menu).Any(e => e.Rotulo.Replace("&", "", StringComparison.Ordinal).Contains("Itens", StringComparison.OrdinalIgnoreCase)), "nenhuma entrada de itens");
                Afirmar.Verdadeiro(Todas(menu).All(e => e.Id < 2000), "nenhum id da faixa dos itens (2000 em diante)");
            }
        }
    }

    [Teste]
    public void AltoContraste_SoTexto_SemRostos()
    {
        Afirmar.Igual(14, Todas(MenuNativo.Entradas(Modelo())).Count(e => e.Rosto is not null), "com o tema comum, 14 rostos");
        IReadOnlyList<EntradaDoMenu> contraste = MenuNativo.Entradas(Modelo(altoContraste: true, emocao: Expressao.Feliz));
        Afirmar.Verdadeiro(Todas(contraste).All(e => e.Rosto is null), "em alto contraste, nenhum rosto");
        Afirmar.Sequencia([1001], Todas(contraste).Where(e => e.Radio && e.Marcada).Select(e => e.Id), "a marca de rádio continua");
        Afirmar.Sequencia(
            Todas(MenuNativo.Entradas(Modelo(emocao: Expressao.Feliz))).Select(e => (e.Tipo, e.Rotulo, e.Id, e.Marcada)),
            Todas(contraste).Select(e => (e.Tipo, e.Rotulo, e.Id, e.Marcada)), "o resto do menu é o mesmo");
    }

    [Teste]
    public void Escolha_CadaIdVoltaAoComandoEAEmocao_EIdDesconhecidoNaoEscolheNada()
    {
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.AlternarVisibilidade), MenuNativo.Escolha(1));
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.Sair), MenuNativo.Escolha(2));
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.AlternarMovimento), MenuNativo.Escolha(3));
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.Emocao, null), MenuNativo.Escolha(999), "Automática");
        for (int i = 0; i < Emocoes.Length; i++)
            Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.Emocao, Enum.Parse<Expressao>(Emocoes[i].Nome)), MenuNativo.Escolha(1000 + i), Emocoes[i].Nome);

        // 0 é o menu cancelado; 4 a 6 são os valores de ComandoDoMenu.Emocao, Item e RecolherItens, mas não são id de
        // nenhuma entrada. Os itens (2000 a 2012) e "Recolher itens" (2999) estão no teste dos itens.
        foreach (int id in new[] { 0, -1, 4, 5, 6, 998, 1014, 1020, 1999, 2013, 2998, 3000, int.MaxValue })
            Afirmar.Igual(EscolhaDoMenu.Nenhuma, MenuNativo.Escolha(id), $"id {id}");

        // Nenhum id da lista fica sem escolha, nem se repete.
        int[] ids = [.. Todas(MenuNativo.Entradas(Modelo())).Where(e => e.Tipo == TipoDeEntrada.Comando).Select(e => e.Id)];
        Afirmar.Igual(ids.Length, ids.Distinct().Count(), "ids sem repetição");
        Afirmar.Verdadeiro(ids.All(id => MenuNativo.Escolha(id) != EscolhaDoMenu.Nenhuma), "todo id da lista volta a uma escolha");
    }

    // ------------------------------------------------------------------ itens do tamagotchi (DEC-028, passo T8)

    [Teste]
    public void ComAChaveLigada_SubmenuItensDepoisDaEmocao_Os13ItensNaOrdemDoEnum_SeparadorERecolher()
    {
        IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(ModeloComItens(itensNaTela: 2));
        Afirmar.Sequencia(
            [
                (TipoDeEntrada.Comando, "&Esconder Buzzy", 1),
                (TipoDeEntrada.Comando, "&Pausar movimento", 3),
                (TipoDeEntrada.Separador, "", 0),
                (TipoDeEntrada.Submenu, "Emoção &dominante", 0),
                (TipoDeEntrada.Submenu, "&Itens", 0),
                (TipoDeEntrada.Comando, "Conteúdo &adulto", 7),
                (TipoDeEntrada.Comando, "Desviar da &tela cheia", 8),
                (TipoDeEntrada.Separador, "", 0),
                (TipoDeEntrada.Comando, "&Sair", 2),
            ],
            menu.Select(e => (e.Tipo, e.Rotulo, e.Id)), "o submenu \"Itens\" vem logo depois do da emoção dominante, e a chave do conteúdo adulto depois dele");
        Afirmar.Igual(16, Afirmar.NaoNulo(SubmenuDaEmocaoNaChaveLigada(menu).Filhas).Count, "o submenu da emoção continua igual");

        EntradaDoMenu submenu = SubmenuDosItens(menu);
        Afirmar.Falso(submenu.Desabilitada, "com o Buzzy à vista, o submenu fica habilitado");
        IReadOnlyList<EntradaDoMenu> filhas = Afirmar.NaoNulo(submenu.Filhas);
        Afirmar.Igual(15, filhas.Count, "13 itens, separador e \"Recolher itens\"");

        // A ordem é a do enum Item (a da resposta do usuário), e cada item pede o próprio desenho do chão como ícone.
        Afirmar.Sequencia(Enum.GetValues<Item>(), Itens.Select(i => i.Item), "a tabela do teste segue o enum");
        EntradaDoMenu[] itens = [.. filhas.Take(13)];
        Afirmar.Sequencia(Itens.Select(i => i.Rotulo), itens.Select(e => e.Rotulo), "rótulos: só os nomes, com a tecla de acesso");
        Afirmar.Sequencia(Enumerable.Range(2000, 13), itens.Select(e => e.Id), "ids 2000 a 2012");
        Afirmar.Sequencia(Itens.Select(i => i.Item.ToString().ToLowerInvariant()), itens.Select(e => e.Item), "ícone pedido: a chave da arte do item");
        Afirmar.Verdadeiro(itens.All(e => e.Tipo == TipoDeEntrada.Comando && !e.Radio && !e.Marcada && !e.Desabilitada && e.Rosto is null && e.Filhas is null),
            "itens: comandos simples, habilitados, sem marca e sem rosto");
        foreach (EntradaDoMenu e in itens)
        {
            // Todo ícone pedido existe na arte (uma chave ausente derrubaria o menu, F5) e é o desenho do chão.
            Tela icone = IconesDoMenu.Item(e.Item!);
            Afirmar.Igual((24, 24), (icone.Largura, icone.Altura), e.Item);
        }

        Afirmar.Igual(TipoDeEntrada.Separador, filhas[13].Tipo, "separador antes de \"Recolher itens\"");
        Afirmar.Igual((TipoDeEntrada.Comando, "&Recolher itens", 2999, false, (string?)null, (string?)null),
            (filhas[14].Tipo, filhas[14].Rotulo, filhas[14].Id, filhas[14].Desabilitada, filhas[14].Item, filhas[14].Rosto), "\"Recolher itens\", habilitado com itens na tela, sem ícone");
    }

    [Teste]
    public void SubmenuItens_DesabilitadoComOBuzzyEscondido_ERecolherSoComItensNaTela()
    {
        foreach (bool pausado in new[] { false, true })
        {
            EntradaDoMenu escondido = SubmenuDosItens(MenuNativo.Entradas(ModeloComItens(visivel: false, itensNaTela: 3, pausado: pausado)));
            Afirmar.Verdadeiro(escondido.Desabilitada, "com o Buzzy escondido, o submenu \"Itens\" fica desabilitado");
            Afirmar.Igual(15, Afirmar.NaoNulo(escondido.Filhas).Count, "as linhas continuam as mesmas");

            EntradaDoMenu visivel = SubmenuDosItens(MenuNativo.Entradas(ModeloComItens(visivel: true, itensNaTela: 0, pausado: pausado)));
            Afirmar.Falso(visivel.Desabilitada, "à vista, habilitado");
        }

        foreach ((int naTela, bool desabilitado) in new[] { (0, true), (-1, true), (1, false), (6, false) })
        {
            IReadOnlyList<EntradaDoMenu> filhas = Afirmar.NaoNulo(SubmenuDosItens(MenuNativo.Entradas(ModeloComItens(itensNaTela: naTela))).Filhas);
            Afirmar.Igual(desabilitado, filhas[14].Desabilitada, $"\"Recolher itens\" com {naTela} item(ns) na tela");
            Afirmar.Verdadeiro(filhas.Take(13).All(e => !e.Desabilitada), $"os itens ficam habilitados com {naTela} na tela");
        }
    }

    [Teste]
    public void SubmenuItens_EmAltoContraste_SoTexto_ORestoIgual()
    {
        IReadOnlyList<EntradaDoMenu> comum = MenuNativo.Entradas(ModeloComItens(itensNaTela: 1));
        IReadOnlyList<EntradaDoMenu> contraste = MenuNativo.Entradas(ModeloComItens(itensNaTela: 1, altoContraste: true));
        Afirmar.Igual(13, Todas(comum).Count(e => e.Item is not null), "com o tema comum, 13 ícones de item");
        Afirmar.Verdadeiro(Todas(contraste).All(e => e.Item is null && e.Rosto is null), "em alto contraste, nenhum ícone");
        Afirmar.Sequencia(
            Todas(comum).Select(e => (e.Tipo, e.Rotulo, e.Id, e.Marcada, e.Desabilitada)),
            Todas(contraste).Select(e => (e.Tipo, e.Rotulo, e.Id, e.Marcada, e.Desabilitada)), "o resto do menu é o mesmo");
    }

    [Teste]
    public void Escolha_IdsDosItensVoltamAoItem_ERecolherAoComando()
    {
        for (int i = 0; i < Itens.Length; i++)
            Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.Item, Item: Itens[i].Item), MenuNativo.Escolha(2000 + i), Itens[i].Rotulo);
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.RecolherItens), MenuNativo.Escolha(2999), "Recolher itens");
        foreach (int id in new[] { 1999, 2013, 2998, 3000, 5, 6 })
            Afirmar.Igual(EscolhaDoMenu.Nenhuma, MenuNativo.Escolha(id), $"id {id}");

        // Com a chave ligada, todo id da lista volta a uma escolha, sem repetição.
        int[] ids = [.. Todas(MenuNativo.Entradas(ModeloComItens(itensNaTela: 1))).Where(e => e.Tipo == TipoDeEntrada.Comando).Select(e => e.Id)];
        Afirmar.Igual(5 + 15 + 14, ids.Length, "Esconder, Pausar, Conteúdo adulto, Desviar da tela cheia, Sair, 15 opções de emoção e 14 do submenu dos itens");
        Afirmar.Igual(ids.Length, ids.Distinct().Count(), "ids sem repetição");
        Afirmar.Verdadeiro(ids.All(id => MenuNativo.Escolha(id) != EscolhaDoMenu.Nenhuma), "todo id da lista volta a uma escolha");
    }

    private static EntradaDoMenu SubmenuDaEmocaoNaChaveLigada(IReadOnlyList<EntradaDoMenu> menu)
        => menu.Single(e => e.Tipo == TipoDeEntrada.Submenu && e.Rotulo == "Emoção &dominante");

    // ------------------------------------------------------------------ o menu real ligado ao estado do app

    private static readonly MonitorDoDesktop Principal96 = new("m1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1040), 96, true);
    private static readonly MonitorDoDesktop Direita192 = new("m2", new RetanguloPx(1920, 0, 4480, 1440), new RetanguloPx(1920, 0, 4480, 1400), 192, false);
    private static readonly MonitorDoDesktop Acima288 = new("m3", new RetanguloPx(0, -2160, 3840, 0), new RetanguloPx(0, -2160, 3840, -40), 288, false);

    // ------------------------------------------------------------------ conteúdo adulto (DEC-033)

    // "Conteúdo adulto" é um comando com a marca de seleção (não de rádio), marcada quando ligado; só existe com a chave do
    // tamagotchi; o id 7 volta ao comando.
    [Teste]
    public void ConteudoAdulto_ComandoComMarca_SoComOTamagotchi()
    {
        EntradaDoMenu ligado = MenuNativo.Entradas(ModeloComItens()).Single(e => e.Id == 7);
        Afirmar.Igual((TipoDeEntrada.Comando, "Conteúdo &adulto", true, false, false), (ligado.Tipo, ligado.Rotulo, ligado.Marcada, ligado.Radio, ligado.Desabilitada), "ligado: marcado");
        EntradaDoMenu desligado = MenuNativo.Entradas(ModeloComItens(adulto: false)).Single(e => e.Id == 7);
        Afirmar.Falso(desligado.Marcada, "desligado: sem marca");
        Afirmar.Verdadeiro(MenuNativo.Entradas(ModeloComItens(visivel: false)).Single(e => e.Id == 7) is { Desabilitada: false }, "também com o Buzzy escondido");
        Afirmar.Falso(Todas(MenuNativo.Entradas(Modelo())).Any(e => e.Id == 7), "sem a chave do tamagotchi, não aparece");
        Afirmar.Igual(new EscolhaDoMenu(ComandoDoMenu.ConteudoAdulto), MenuNativo.Escolha(7), "o id volta ao comando");
    }

    // Desligado, o submenu "Itens" só tem os quatro de alívio (a banana, a água, o café e o energético), com os mesmos ids de
    // sempre, o separador e "Recolher itens"; os nove adultos somem.
    [Teste]
    public void ConteudoAdultoDesligado_ItensSoComOsQuatroLivres_MesmosIds()
    {
        IReadOnlyList<EntradaDoMenu> filhas = Afirmar.NaoNulo(SubmenuDosItens(MenuNativo.Entradas(ModeloComItens(adulto: false, itensNaTela: 1))).Filhas);
        Afirmar.Sequencia(
            [("&Banana", 2000), ("Á&gua", 2001), ("Ca&fé", 2009), ("E&nergético", 2010), ("", 0), ("&Recolher itens", 2999)],
            filhas.Select(e => (e.Rotulo, e.Id)), "os quatro livres, o separador e recolher");
        foreach (EntradaDoMenu e in filhas.Where(e => e.Id is >= 2000 and < 2999))
            Afirmar.Falso(TabelaDoTamagotchi.Adulto(Afirmar.NaoNulo(MenuNativo.Escolha(e.Id).Item, e.Rotulo)), $"{e.Rotulo}: não é adulto");
        Afirmar.Igual(15, Afirmar.NaoNulo(SubmenuDosItens(MenuNativo.Entradas(ModeloComItens())).Filhas).Count, "ligado: os 13 de sempre");
    }

    [Teste]
    public void ModeloAoAbrir_EmocaoItensPausaEChave_SaemDoEstadoDoNucleo()
    {
        // Revisão de correção, achado 3: o menu real lê do núcleo, na abertura, a marca de rádio, quantos itens estão na tela
        // ("Recolher itens" só vale com algum) e a chave do tamagotchi; o resto vem da raiz.
        var nucleo = new Nucleo(ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)), 7);
        // Com a chave adulta ligada e os nove marcados, que o padrão (DEC-041) não traz.
        nucleo.Enfileirar(new Loaded(new Topologia([Principal96]), PosicaoSalva: null, Preferencias.Padrao with { ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos }));
        nucleo.Processar();
        Afirmar.Igual(new ModeloDoMenu(true, false, null, AltoContraste: false, Tamagotchi: true, ItensNaTela: 0, PainelDeEnergia: true, Configuracoes: true),
            MenuNativo.ModeloAoAbrir(nucleo, visivel: true, altoContraste: false), "recém-carregado: Automática, nenhum item, chave ligada");

        foreach (Evento e in new Evento[] { new CmdSetDominantEmotion(Expressao.Feliz), new CmdSummonItem(Item.Banana), new CmdSummonItem(Item.Agua), new CmdPauseAutonomy() })
            nucleo.Enfileirar(e);
        nucleo.Processar();
        Afirmar.Igual(2, nucleo.Estado.Itens.Quantidade, "dois itens invocados no núcleo");
        Afirmar.Igual(new ModeloDoMenu(true, true, Expressao.Feliz, AltoContraste: false, Tamagotchi: true, ItensNaTela: 2, PainelDeEnergia: true, Configuracoes: true),
            MenuNativo.ModeloAoAbrir(nucleo, visivel: true, altoContraste: false), "a emoção, os dois itens e a pausa do núcleo");
        Afirmar.Igual(new ModeloDoMenu(false, true, Expressao.Feliz, AltoContraste: true, Tamagotchi: true, ItensNaTela: 2, PainelDeEnergia: true, Configuracoes: true),
            MenuNativo.ModeloAoAbrir(nucleo, visivel: false, altoContraste: true), "escondido e em alto contraste");

        nucleo.Enfileirar(new CmdClearItems());
        nucleo.Processar();
        Afirmar.Igual(0, MenuNativo.ModeloAoAbrir(nucleo, true, false).ItensNaTela, "recolhidos: \"Recolher itens\" volta a ficar desabilitado");
        Afirmar.Verdadeiro(MenuNativo.Entradas(MenuNativo.ModeloAoAbrir(nucleo, true, false)).Single(e => e.Rotulo == "&Itens").Filhas![14].Desabilitada,
            "na lista do menu, \"Recolher itens\" desabilitado sem itens");

        Afirmar.Verdadeiro(MenuNativo.ModeloAoAbrir(nucleo, true, false).ConteudoAdulto, "o conteúdo adulto ligado vem do núcleo");
        nucleo.Enfileirar(new CmdSetAdultContent(false));
        nucleo.Processar();
        Afirmar.Falso(MenuNativo.ModeloAoAbrir(nucleo, true, false).ConteudoAdulto, "o conteúdo adulto desligado vem do núcleo");
        nucleo.Enfileirar(new CmdSetAdultContent(true));
        nucleo.Enfileirar(new CmdSetAdultItemEnabled(Item.Md, false));
        nucleo.Processar();
        ModeloDoMenu semMd = MenuNativo.ModeloAoAbrir(nucleo, true, false);
        Afirmar.Falso(semMd.ItensAdultosHabilitados.Contem(Item.Md), "a seleção dos itens adultos vem do núcleo (DEC-041)");
        Afirmar.Falso(Todas(MenuNativo.Entradas(semMd)).Any(e => e.Item == Buzzy.App.Apresentacao.PoseDoPersonagem.NomeDoItem(Item.Md)), "o MD desmarcado some do submenu");
        Afirmar.Igual(12, Todas(MenuNativo.Entradas(semMd)).Count(e => e.Item is not null), "os outros doze ficam");

        Afirmar.Verdadeiro(MenuNativo.ModeloAoAbrir(nucleo, true, false).ModoTelaCheia, "o modo de tela cheia ligado vem do núcleo");
        nucleo.Enfileirar(new CmdSetFullscreenMode(false));
        nucleo.Processar();
        Afirmar.Falso(MenuNativo.ModeloAoAbrir(nucleo, true, false).ModoTelaCheia, "o modo de tela cheia desligado vem do núcleo");

        var desligada = new Nucleo(ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { Tamagotchi = false }, 7);
        Afirmar.Falso(MenuNativo.ModeloAoAbrir(desligada, true, false).Tamagotchi, "a chave vem da configuração do núcleo");
        Afirmar.Igual(new ModeloDoMenu(true, false, null, AltoContraste: false, Tamagotchi: false, ItensNaTela: 0, ConteudoAdulto: Preferencias.Padrao.ConteudoAdulto)
            { ItensAdultosHabilitados = Preferencias.Padrao.ItensAdultosHabilitados },
            MenuNativo.ModeloAoAbrir(null, visivel: true, altoContraste: false), "sem núcleo: o menu de partida, com as preferências padrão");
    }

    [Teste]
    public void DpiAoAbrir_ODoMonitorEmQueOMenuAbre_NaoODoPrincipal()
    {
        // Revisão de correção, achado 3: a 192 DPI ou mais, os ícones do menu têm de vir ampliados pelo monitor do ponto.
        var topologia = new Topologia([Principal96, Direita192, Acima288]);
        Afirmar.Igual(96, MenuNativo.DpiAoAbrir(topologia, new PontoPx(100, 1000)), "no principal");
        Afirmar.Igual(192, MenuNativo.DpiAoAbrir(topologia, new PontoPx(2500, 700)), "no monitor da direita");
        Afirmar.Igual(288, MenuNativo.DpiAoAbrir(topologia, new PontoPx(1000, -500)), "no de cima");
        Afirmar.Igual(192, MenuNativo.DpiAoAbrir(topologia, new PontoPx(4600, 1300)), "fora de todos: o monitor mais próximo");
    }
}
