using Buzzy.App.Plataforma;
using Buzzy.App.Testes.Integracao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// A montagem da lista de entradas num HMENU (DEC-027; crítica, L13 e seção 4), pelo mesmo ciclo de vida da abertura
/// real (<see cref="MenuNativo.ComMenuMontado"/>), só que sem exibir: um menu nativo existe sem janela, e o teste o lê
/// de volta com GetMenuItemInfoW. Confere ids, textos, marcas de rádio, rostos anexados como bitmap, o submenu anexado
/// ao principal e que, depois do DestroyMenu e do Dispose, os objetos GDI e USER voltam ao patamar.
/// </summary>
internal sealed class MontagemDoMenuTestes
{
    private const uint MFT_RADIOCHECK = 0x200, MFT_SEPARATOR = 0x800, MFS_CHECKED = 0x8, MFS_GRAYED = 0x3;

    private static ModeloDoMenu Modelo(Expressao? emocao, bool altoContraste = false)
        => new(BuzzyVisivel: true, MovimentoPausado: false, emocao, altoContraste, Tamagotchi: false);

    [Teste]
    public void NoHmenu_IdsTextosMarcasERostosDoSubmenuAnexado_EDepoisTudoDestruido()
    {
        IReadOnlyList<EntradaDoMenu> entradas = MenuNativo.Entradas(Modelo(Expressao.Feliz));
        EntradaDoMenu[] esperadas = [.. entradas.Single(e => e.Tipo == TipoDeEntrada.Submenu).Filhas!];
        nint menuLido = 0;
        nint[] bitmaps = [];
        bool exibiu = false;

        AberturaDoMenu abertura = MenuNativo.ComMenuMontado(entradas, fator: 2, (menu, icones) =>
        {
            exibiu = true;
            menuLido = menu;
            Afirmar.Igual(14, icones, "um rosto por emoção, já anexado quando o menu é exibido");

            Afirmar.Igual(7, NativoTeste.GetMenuItemCount(menu), "itens do menu principal");
            NativoTeste.ItemDoMenuLido[] principal = [.. Enumerable.Range(0, 7).Select(i => NativoTeste.LerItemDoMenu(menu, (uint)i))];
            Afirmar.Sequencia(entradas.Select(e => e.Rotulo), principal.Select(i => i.Texto), "textos do menu principal");
            Afirmar.Sequencia([1u, 3u, 8u, 2u], principal.Where((_, i) => i is 0 or 1 or 4 or 6).Select(i => i.Id), "ids de hoje");
            Afirmar.Verdadeiro((principal[2].Tipo & MFT_SEPARATOR) != 0 && (principal[5].Tipo & MFT_SEPARATOR) != 0, "separadores");
            // "Desviar da tela cheia" (DEC-034): a marca de seleção, ligada por padrão, e não a de rádio.
            Afirmar.Igual((8u, MFS_CHECKED, 0u), (principal[4].Id, principal[4].Estado & MFS_CHECKED, principal[4].Tipo & MFT_RADIOCHECK), "Desviar da tela cheia, marcado");
            // O Windows informa todo separador como desabilitado (estado 0x3); os outros itens ficam habilitados.
            Afirmar.Verdadeiro(principal.Where((i, k) => (i.Tipo & MFT_SEPARATOR) == 0 && k != 4).All(i => i.Bitmap == 0 && (i.Estado & (MFS_CHECKED | MFS_GRAYED)) == 0),
                "principal sem ícone, sem marca, habilitado: " + string.Join("; ", principal.Select(i => $"{i.Texto}: tipo 0x{i.Tipo:X} estado 0x{i.Estado:X} bitmap {i.Bitmap}")));

            nint submenu = principal[3].Submenu;
            Afirmar.Diferente((nint)0, submenu, "o submenu está anexado ao item \"Emoção dominante\"");
            Afirmar.Igual(16, NativoTeste.GetMenuItemCount(submenu), "Automática, separador e 14 emoções");
            NativoTeste.ItemDoMenuLido[] opcoes = [.. Enumerable.Range(0, 16).Select(i => NativoTeste.LerItemDoMenu(submenu, (uint)i))];
            Afirmar.Sequencia(esperadas.Select(e => e.Rotulo), opcoes.Select(o => o.Texto), "textos do submenu");
            Afirmar.Sequencia(esperadas.Select(e => (uint)e.Id), opcoes.Select(o => o.Id), "ids do submenu");
            Afirmar.Verdadeiro((opcoes[1].Tipo & MFT_SEPARATOR) != 0, "separador depois de Automática");
            Afirmar.Igual((nint)0, opcoes[0].Bitmap, "Automática sem rosto");
            for (int i = 0; i < opcoes.Length; i++)
            {
                if (i == 1) continue;
                Afirmar.Verdadeiro((opcoes[i].Tipo & MFT_RADIOCHECK) != 0, $"{opcoes[i].Texto}: rádio");
                Afirmar.Igual(opcoes[i].Texto == "&Feliz", (opcoes[i].Estado & MFS_CHECKED) != 0, $"{opcoes[i].Texto}: marca só na atual");
                Afirmar.Igual(0u, opcoes[i].Estado & MFS_GRAYED, $"{opcoes[i].Texto}: habilitada");
            }

            bitmaps = [.. opcoes.Skip(2).Select(o => o.Bitmap)];
            Afirmar.Igual(14, bitmaps.Where(b => b != 0).Distinct().Count(), "um bitmap para cada rosto");
            foreach (nint b in bitmaps)
            {
                NativoTeste.DIBSECTION dib = NativoTeste.LerDib(b);
                Afirmar.Igual((80, 64, 32), (dib.dsBmih.biWidth, dib.dsBmih.biHeight, (int)dib.dsBmih.biBitCount), "rosto de 40 × 32 ampliado 2×, de baixo para cima");
            }
            return 1001;
        });

        Afirmar.Verdadeiro(exibiu, "o menu foi exibido");
        Afirmar.Igual(new AberturaDoMenu(1001, 14, 14, 14), abertura, "id escolhido e contas: criados = apagados");
        Afirmar.Falso(NativoTeste.IsMenu(menuLido), "DestroyMenu: o menu não existe mais");
        Afirmar.Falso(bitmaps.Any(NativoTeste.ObjetoGdiVivo), "Dispose: nenhum bitmap continua vivo");
    }

    [Teste]
    public void OsBitmapsVivemEnquantoOMenuEhExibido_EOMenuNaoOsApaga()
    {
        // DestroyMenu não apaga hbmpItem: quem cria apaga, depois. E enquanto o menu está na tela, os rostos existem.
        nint menuLido = 0, primeiro = 0;
        AberturaDoMenu abertura = MenuNativo.ComMenuMontado(MenuNativo.Entradas(Modelo(null)), fator: 1, (menu, _) =>
        {
            menuLido = menu;
            nint submenu = NativoTeste.LerItemDoMenu(menu, 3).Submenu;
            primeiro = NativoTeste.LerItemDoMenu(submenu, 2).Bitmap;
            Afirmar.Verdadeiro(NativoTeste.ObjetoGdiVivo(primeiro), "o rosto de Neutro está vivo durante a exibição");
            Afirmar.Verdadeiro((NativoTeste.LerItemDoMenu(submenu, 0).Estado & MFS_CHECKED) != 0, "sem emoção dominante, Automática marcada");
            return 0;
        });
        Afirmar.Igual(new AberturaDoMenu(0, 14, 14, 14), abertura, "cancelado: id 0, e os bitmaps apagados mesmo assim");
        Afirmar.Falso(NativoTeste.IsMenu(menuLido) || NativoTeste.ObjetoGdiVivo(primeiro), "menu destruído e bitmap apagado");
    }

    [Teste]
    public void AltoContraste_NenhumBitmap()
    {
        int opcoes = 0;
        AberturaDoMenu abertura = MenuNativo.ComMenuMontado(MenuNativo.Entradas(Modelo(Expressao.Feliz, altoContraste: true)), fator: 1, (menu, _) =>
        {
            nint submenu = NativoTeste.LerItemDoMenu(menu, 3).Submenu;
            opcoes = NativoTeste.GetMenuItemCount(submenu);
            Afirmar.Verdadeiro(Enumerable.Range(0, opcoes).All(i => NativoTeste.LerItemDoMenu(submenu, (uint)i).Bitmap == 0), "só texto");
            return 0;
        });
        Afirmar.Igual(16, opcoes, "o submenu inteiro, só com texto");
        Afirmar.Igual(new AberturaDoMenu(0, 0, 0, 0), abertura);
    }

    [Teste]
    public void NoHmenu_SubmenuItens_13IconesDoChaoSeparadorERecolher_EDepoisTudoApagado()
    {
        // Com a chave do tamagotchi (DEC-028, T8) e nenhum item na tela: "Itens" logo depois da emoção, os 13 itens com o
        // desenho do chão como ícone, um separador e "Recolher itens" desabilitado.
        var modelo = new ModeloDoMenu(BuzzyVisivel: true, MovimentoPausado: false, null, AltoContraste: false, Tamagotchi: true, ItensNaTela: 0);
        IReadOnlyList<EntradaDoMenu> entradas = MenuNativo.Entradas(modelo);
        EntradaDoMenu[] esperadas = [.. entradas.Single(e => e.Rotulo == "&Itens").Filhas!];
        nint menuLido = 0;
        nint[] bitmaps = [];

        AberturaDoMenu abertura = MenuNativo.ComMenuMontado(entradas, fator: 2, (menu, icones) =>
        {
            menuLido = menu;
            Afirmar.Igual(14 + 13, icones, "14 rostos e 13 itens, já anexados quando o menu é exibido");
            Afirmar.Igual(9, NativoTeste.GetMenuItemCount(menu), "itens do menu principal");
            NativoTeste.ItemDoMenuLido[] principal = [.. Enumerable.Range(0, 9).Select(i => NativoTeste.LerItemDoMenu(menu, (uint)i))];
            // "Conteúdo adulto" (DEC-033), depois de "Itens", e "Desviar da tela cheia" (DEC-034): a marca de seleção, ligada por
            // padrão, e não a de rádio.
            Afirmar.Igual((7u, MFS_CHECKED, 0u), (principal[5].Id, principal[5].Estado & MFS_CHECKED, principal[5].Tipo & MFT_RADIOCHECK), "Conteúdo adulto, marcado");
            Afirmar.Igual((8u, MFS_CHECKED, 0u), (principal[6].Id, principal[6].Estado & MFS_CHECKED, principal[6].Tipo & MFT_RADIOCHECK), "Desviar da tela cheia, marcado");
            Afirmar.Sequencia(entradas.Select(e => e.Rotulo), principal.Select(i => i.Texto), "textos do menu principal");
            Afirmar.Diferente((nint)0, principal[3].Submenu, "o submenu da emoção está anexado");
            nint submenu = principal[4].Submenu;
            Afirmar.Diferente((nint)0, submenu, "o submenu \"Itens\" está anexado ao item dele");
            Afirmar.Igual(0u, principal[4].Estado & MFS_GRAYED, "com o Buzzy à vista, \"Itens\" habilitado");

            Afirmar.Igual(15, NativoTeste.GetMenuItemCount(submenu), "13 itens, separador e Recolher");
            NativoTeste.ItemDoMenuLido[] linhas = [.. Enumerable.Range(0, 15).Select(i => NativoTeste.LerItemDoMenu(submenu, (uint)i))];
            Afirmar.Sequencia(esperadas.Select(e => e.Rotulo), linhas.Select(o => o.Texto), "textos do submenu dos itens");
            Afirmar.Sequencia(esperadas.Select(e => (uint)e.Id), linhas.Select(o => o.Id), "ids 2000 a 2012 e 2999");
            for (int i = 0; i < 13; i++)
            {
                Afirmar.Igual(0u, linhas[i].Tipo & (MFT_RADIOCHECK | MFT_SEPARATOR), $"{linhas[i].Texto}: comando simples");
                Afirmar.Igual(0u, linhas[i].Estado & (MFS_CHECKED | MFS_GRAYED), $"{linhas[i].Texto}: habilitado, sem marca");
                NativoTeste.DIBSECTION dib = NativoTeste.LerDib(linhas[i].Bitmap);
                Afirmar.Igual((48, 48, 32), (dib.dsBmih.biWidth, dib.dsBmih.biHeight, (int)dib.dsBmih.biBitCount), $"{linhas[i].Texto}: o desenho de 24 × 24 ampliado 2×");
            }
            Afirmar.Verdadeiro((linhas[13].Tipo & MFT_SEPARATOR) != 0, "separador antes de Recolher");
            Afirmar.Igual((nint)0, linhas[14].Bitmap, "Recolher sem ícone");
            Afirmar.Verdadeiro((linhas[14].Estado & MFS_GRAYED) == MFS_GRAYED, "sem itens na tela, Recolher desabilitado");

            // O ícone que o Windows enxerga é o desenho do chão do item, de cima para baixo (a vodka, na linha 2).
            uint[] vista = NativoTeste.ImagemDoBitmap(linhas[2].Bitmap, 48, 48);
            Afirmar.Sequencia(IconesDoMenu.Ampliar(IconesDoMenu.Item("vodka"), 2), vista, "o ícone da vodka, pixel a pixel");

            bitmaps = [.. linhas.Take(13).Select(o => o.Bitmap)];
            Afirmar.Igual(13, bitmaps.Where(b => b != 0).Distinct().Count(), "um bitmap para cada item");
            return 2003;
        });

        Afirmar.Igual(new AberturaDoMenu(2003, 27, 27, 27), abertura, "id escolhido e contas: criados = apagados");
        Afirmar.Falso(NativoTeste.IsMenu(menuLido), "DestroyMenu: o menu não existe mais");
        Afirmar.Falso(bitmaps.Any(NativoTeste.ObjetoGdiVivo), "Dispose: nenhum ícone de item continua vivo");
    }

    [Teste]
    public void NoHmenu_ItensDesabilitadoComOBuzzyEscondido_ERecolherHabilitadoComItens()
    {
        foreach ((bool visivel, int naTela) in new[] { (false, 2), (true, 2) })
        {
            var modelo = new ModeloDoMenu(visivel, MovimentoPausado: false, null, AltoContraste: false, Tamagotchi: true, ItensNaTela: naTela);
            AberturaDoMenu abertura = MenuNativo.ComMenuMontado(MenuNativo.Entradas(modelo), fator: 1, (menu, _) =>
            {
                NativoTeste.ItemDoMenuLido itens = NativoTeste.LerItemDoMenu(menu, 4);
                Afirmar.Igual("&Itens", itens.Texto);
                Afirmar.Igual(visivel ? 0u : MFS_GRAYED, itens.Estado & MFS_GRAYED, $"\"Itens\" com o Buzzy {(visivel ? "à vista" : "escondido")}");
                Afirmar.Igual(0u, NativoTeste.LerItemDoMenu(itens.Submenu, 14).Estado & MFS_GRAYED, "com itens na tela, Recolher habilitado");
                return 0;
            });
            Afirmar.Igual((27, 27), (abertura.BitmapsCriados, abertura.BitmapsApagados), "criados = apagados");
        }
    }

    [Teste]
    public void NoDpiDoMonitor_RostosEItensAmpliadosPeloFatorInteiro()
    {
        // Revisão de correção, achado 3: o Windows não amplia o bitmap de um item de menu; a abertura amplia os ícones pelo
        // fator inteiro do DPI do monitor (1 até 191 DPI, 2 de 192 a 287, 3 a 288).
        var modelo = new ModeloDoMenu(BuzzyVisivel: true, MovimentoPausado: false, null, AltoContraste: false, Tamagotchi: true, ItensNaTela: 0);
        IReadOnlyList<EntradaDoMenu> entradas = MenuNativo.Entradas(modelo);
        foreach ((int dpi, int fator) in new[] { (96, 1), (144, 1), (192, 2), (288, 3) })
        {
            (int, int, int, int)? lados = null;
            AberturaDoMenu abertura = MenuNativo.ComMenuMontadoNoDpi(entradas, dpi, (menu, _) =>
            {
                nint rosto = NativoTeste.LerItemDoMenu(NativoTeste.LerItemDoMenu(menu, 3).Submenu, 2).Bitmap;
                nint item = NativoTeste.LerItemDoMenu(NativoTeste.LerItemDoMenu(menu, 4).Submenu, 0).Bitmap;
                NativoTeste.DIBSECTION r = NativoTeste.LerDib(rosto), i = NativoTeste.LerDib(item);
                lados = (r.dsBmih.biWidth, r.dsBmih.biHeight, i.dsBmih.biWidth, i.dsBmih.biHeight);
                return 0;
            });
            Afirmar.Igual(((int, int, int, int)?)(40 * fator, 32 * fator, 24 * fator, 24 * fator), lados, $"a {dpi} DPI, rosto de 40 × 32 e item de 24 × 24 ampliados {fator}×");
            Afirmar.Igual((27, 27), (abertura.BitmapsCriados, abertura.BitmapsApagados), $"a {dpi} DPI: criados = apagados");
        }
    }

    [Teste]
    public void ExibirQueLanca_DestroiOMenuEApagaOsBitmapsMesmoAssim()
    {
        // Revisão de regras, lacuna 4: o caminho de exceção da abertura. O DestroyMenu fica no finally e os bitmaps são
        // apagados depois dele, pelo using declarado antes do try: uma exceção ao exibir não vaza objeto USER nem GDI.
        IReadOnlyList<EntradaDoMenu> entradas = MenuNativo.Entradas(new ModeloDoMenu(true, false, Expressao.Feliz, AltoContraste: false, Tamagotchi: true, ItensNaTela: 1));
        MenuNativo.ComMenuMontado(entradas, fator: 1, (_, _) => 0); // aquecimento
        uint gdi = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), user = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS);
        nint menuVisto = 0, submenuVisto = 0, bitmapVisto = 0;
        Afirmar.Lanca<InvalidOperationException>(() => MenuNativo.ComMenuMontado(entradas, fator: 2, (menu, _) =>
        {
            menuVisto = menu;
            submenuVisto = NativoTeste.LerItemDoMenu(menu, 4).Submenu;
            bitmapVisto = NativoTeste.LerItemDoMenu(submenuVisto, 0).Bitmap;
            Afirmar.Verdadeiro(NativoTeste.IsMenu(menu) && NativoTeste.IsMenu(submenuVisto) && NativoTeste.ObjetoGdiVivo(bitmapVisto), "durante a exibição, tudo vivo");
            throw new InvalidOperationException("falha simulada ao exibir");
        }), "a exceção de exibir sobe");
        Afirmar.Falso(NativoTeste.IsMenu(menuVisto) || NativoTeste.IsMenu(submenuVisto), "o menu e o submenu foram destruídos");
        Afirmar.Falso(NativoTeste.ObjetoGdiVivo(bitmapVisto), "o bitmap do ícone foi apagado");
        Afirmar.Igual((gdi, user), (NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS)),
            "objetos (GDI, USER) no mesmo patamar depois da exceção");
    }

    [Teste]
    public void VinteMontagensComItens_GdiEUserVoltamAoPatamar()
    {
        IReadOnlyList<EntradaDoMenu> comItens = MenuNativo.Entradas(new ModeloDoMenu(true, false, Expressao.Feliz, AltoContraste: false, Tamagotchi: true, ItensNaTela: 1));
        MenuNativo.ComMenuMontado(comItens, fator: 2, (_, _) => 0); // aquecimento
        uint gdi = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), user = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS);
        for (int i = 0; i < 20; i++)
        {
            AberturaDoMenu a = MenuNativo.ComMenuMontado(comItens, fator: 1 + i % 3, (_, _) => 0);
            Afirmar.Igual((27, 27), (a.BitmapsCriados, a.BitmapsApagados), $"montagem {i + 1}: criados = apagados");
        }
        Afirmar.Igual((gdi, user), (NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS)),
            "objetos (GDI, USER) depois de 20 montagens com os dois submenus");
    }

    [Teste]
    public void VinteMontagens_GdiEUserVoltamAoPatamar()
    {
        IReadOnlyList<EntradaDoMenu> comRostos = MenuNativo.Entradas(Modelo(Expressao.Travesso));
        MenuNativo.ComMenuMontado(comRostos, fator: 3, (_, _) => 0); // aquecimento
        uint gdi = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), user = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS);
        for (int i = 0; i < 20; i++)
        {
            AberturaDoMenu a = MenuNativo.ComMenuMontado(comRostos, fator: 1 + i % 3, (_, _) => 0);
            Afirmar.Igual((14, 14), (a.BitmapsCriados, a.BitmapsApagados), $"montagem {i + 1}: criados = apagados");
        }
        Afirmar.Igual((gdi, user), (NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS)),
            "objetos (GDI, USER) depois de 20 montagens");
    }
}
