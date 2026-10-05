using System.Runtime.InteropServices;
using System.Windows.Interop;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Plataforma;

/// <summary>Comandos do menu. O nome vai para o log <c>MENU|fechado=</c>, que a verificação de tela lê.</summary>
internal enum ComandoDoMenu
{
    Nenhum = 0,
    AlternarVisibilidade = 1,
    Sair = 2,

    /// <summary>Pausar ou retomar o movimento autônomo (CMD_PAUSE_AUTONOMY / CMD_RESUME_AUTONOMY).</summary>
    AlternarMovimento = 3,

    /// <summary>Escolher a emoção dominante (CMD_SET_DOMINANT_EMOTION, DEC-027): uma das 14 caras de humor ou "Automática".</summary>
    Emocao = 4,

    /// <summary>Invocar um item do tamagotchi (CMD_SUMMON_ITEM, DEC-028).</summary>
    Item = 5,

    /// <summary>"Recolher itens" (CMD_CLEAR_ITEMS, DEC-028).</summary>
    RecolherItens = 6,

    /// <summary>Ligar ou desligar o conteúdo adulto (CMD_SET_ADULT_CONTENT, DEC-033).</summary>
    ConteudoAdulto = 7,

    /// <summary>"Desviar da tela cheia": ligar ou desligar o modo de tela cheia (CMD_SET_FULLSCREEN_MODE, DEC-034; Q-09).</summary>
    ModoTelaCheia = 8,

    /// <summary>"Energia…": abre o painel compacto de energia (ENERGY_PANEL_OPEN, DEC-038, item 4).</summary>
    Energia = 9,

    /// <summary>"Configurações…": abre a janela de configurações (CMD_OPEN_SETTINGS, DEC-038, item 5).</summary>
    Configuracoes = 10,
}

/// <summary>
/// O que o usuário escolheu no menu: o comando e, na emoção dominante, qual (nula = "Automática"); no item, qual.
/// </summary>
internal readonly record struct EscolhaDoMenu(ComandoDoMenu Comando, Expressao? Emocao = null, Item? Item = null)
{
    internal static EscolhaDoMenu Nenhuma => new(ComandoDoMenu.Nenhum);
}

/// <summary>O estado que o menu mostra, lido no momento em que ele abre.</summary>
/// <param name="BuzzyVisivel">"Esconder Buzzy" ou "Mostrar Buzzy".</param>
/// <param name="MovimentoPausado">"Pausar movimento" ou "Retomar movimento".</param>
/// <param name="EmocaoDominante">A marca de rádio no submenu da emoção dominante; nula marca "Automática".</param>
/// <param name="AltoContraste">Em alto contraste, o menu fica só com texto: nenhum rosto.</param>
/// <param name="Tamagotchi">
/// A chave do tamagotchi (DEC-028): o submenu "Itens" só existe com ela ligada (crítica, L13).
/// </param>
/// <param name="ItensNaTela">Quantos itens do tamagotchi estão na tela: sem nenhum, "Recolher itens" fica desabilitado.</param>
/// <param name="ConteudoAdulto">
/// A chave do conteúdo adulto (DEC-033): a marca em "Conteúdo adulto"; desligada, o submenu "Itens" só tem os de alívio.
/// </param>
/// <param name="ModoTelaCheia">O modo de tela cheia (Q-09; DEC-034): a marca em "Desviar da tela cheia".</param>
/// <param name="PainelDeEnergia">Se o painel de energia existe (DEC-038): "Energia…", desabilitado com o Buzzy escondido.</param>
/// <param name="Configuracoes">Se as configurações existem (DEC-038): "Configurações…".</param>
internal sealed record ModeloDoMenu(bool BuzzyVisivel, bool MovimentoPausado, Expressao? EmocaoDominante, bool AltoContraste, bool Tamagotchi, int ItensNaTela = 0, bool ConteudoAdulto = true, bool ModoTelaCheia = true,
    bool PainelDeEnergia = false, bool Configuracoes = false);

/// <summary>O tipo de uma linha do menu.</summary>
internal enum TipoDeEntrada
{
    Comando,
    Separador,
    Submenu,
}

/// <summary>
/// Uma linha do menu, sem nada do Windows: o que a montagem no HMENU põe em cada item.
/// </summary>
/// <param name="Tipo">Comando, separador ou submenu.</param>
/// <param name="Rotulo">O texto, com <c>&amp;</c> antes da tecla de acesso.</param>
/// <param name="Id">O id que <c>TrackPopupMenuEx</c> devolve quando o comando é escolhido (<see cref="MenuNativo.Escolha"/>).</param>
/// <param name="Radio">Opção de rádio: a marca é uma bolinha.</param>
/// <param name="Marcada">A opção atual.</param>
/// <param name="Desabilitada">Visível, mas sem poder ser escolhida.</param>
/// <param name="Rosto">A chave da arte do rosto desenhado como ícone (<c>IconesDoMenu.Rosto</c>), ou nula: só texto.</param>
/// <param name="Filhas">As linhas do submenu.</param>
/// <param name="Item">
/// A chave da arte do item desenhado como ícone (<c>IconesDoMenu.Item</c>: o desenho do chão), ou nula. Uma linha tem no
/// máximo um ícone: o rosto ou o item.
/// </param>
internal sealed record EntradaDoMenu(
    TipoDeEntrada Tipo,
    string Rotulo = "",
    int Id = 0,
    bool Radio = false,
    bool Marcada = false,
    bool Desabilitada = false,
    string? Rosto = null,
    IReadOnlyList<EntradaDoMenu>? Filhas = null,
    string? Item = null)
{
    internal static readonly EntradaDoMenu Separador = new(TipoDeEntrada.Separador);
}

/// <summary>Uma abertura do menu: o id escolhido (0 = cancelado) e as contas dos ícones, para o log.</summary>
internal readonly record struct AberturaDoMenu(int Id, int Icones, int BitmapsCriados, int BitmapsApagados);

/// <summary>
/// O menu do Buzzy, o mesmo para o botão direito no personagem e para o ícone da bandeja
/// (Q-03). É o menu nativo do Windows: tem teclas de acesso, navegação por teclado e
/// acessibilidade prontas, e escala por monitor no modo Per-Monitor V2.
///
/// Um menu só fecha direito ao clicar fora quando o dono está em primeiro plano. O dono é
/// uma janela oculta TEMPORÁRIA, criada para cada abertura: ela recebe o primeiro plano só
/// porque o usuário acabou de pedir o menu, e é destruída quando o menu fecha, para que o
/// Windows devolva a ativação à janela seguinte na ordem Z — em geral, o aplicativo que o
/// usuário estava usando. O Buzzy não lê qual é essa janela.
///
/// O menu tem duas partes separadas (crítica, L13): a lista de entradas (<see cref="Entradas"/>, função pura, testável
/// sem o Windows) e a montagem dela num HMENU a cada abertura (<see cref="ComMenuMontado"/>), com o submenu da emoção
/// dominante (DEC-027) e, com a chave do tamagotchi ligada, o dos itens (DEC-028), com os rostos e os desenhos dos itens
/// da pixel art como ícones, em bitmaps criados e apagados na própria abertura.
/// </summary>
internal static class MenuNativo
{
    /// <summary>Id de "Automática" no submenu da emoção dominante.</summary>
    internal const int IdDaAutomatica = 999;

    /// <summary>Id da primeira das 14 emoções; as outras seguem a ordem de <see cref="Expressoes.DeHumor"/>.</summary>
    internal const int IdDaPrimeiraEmocao = 1000;

    /// <summary>Id do primeiro dos 13 itens do tamagotchi; os outros seguem a ordem de <see cref="TabelaDoTamagotchi.Itens"/>.</summary>
    internal const int IdDoPrimeiroItem = 2000;

    /// <summary>Id de "Recolher itens" no submenu dos itens.</summary>
    internal const int IdDeRecolherItens = 2999;

    /// <summary>
    /// A lista de entradas do menu para o estado lido na abertura. Função pura: nada do Windows. O menu principal tem os
    /// comandos de hoje e, antes de "Sair", o submenu da emoção dominante (DEC-027): "Automática", um separador e as 14
    /// caras de humor na ordem de expressoes.png, cada uma com o próprio rosto como ícone (só texto em alto contraste) e
    /// a marca de rádio na atual. Com a chave do tamagotchi ligada (DEC-028; crítica, L13), vem depois o submenu "Itens"
    /// (<see cref="SubmenuDosItens"/>) e o comando "Conteúdo adulto", com a marca quando ligado (DEC-033). Sempre, antes do
    /// último separador, "Desviar da tela cheia", com a marca quando o modo está ligado (DEC-034).
    /// </summary>
    internal static IReadOnlyList<EntradaDoMenu> Entradas(ModeloDoMenu modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        var emocoes = new List<EntradaDoMenu>(Expressoes.DeHumor.Count + 2)
        {
            new(TipoDeEntrada.Comando, Textos.MenuEmocaoAutomatica, IdDaAutomatica, Radio: true, Marcada: modelo.EmocaoDominante is null),
            EntradaDoMenu.Separador,
        };
        for (int i = 0; i < Expressoes.DeHumor.Count; i++)
        {
            Expressao emocao = Expressoes.DeHumor[i];
            emocoes.Add(new(TipoDeEntrada.Comando, Textos.Emocao(emocao), IdDaPrimeiraEmocao + i, Radio: true,
                Marcada: modelo.EmocaoDominante == emocao,
                Rosto: modelo.AltoContraste ? null : PoseDoPersonagem.NomeDaExpressao(emocao)));
        }

        var principal = new List<EntradaDoMenu>(9)
        {
            new(TipoDeEntrada.Comando, modelo.BuzzyVisivel ? Textos.MenuEsconder : Textos.MenuMostrar, (int)ComandoDoMenu.AlternarVisibilidade),
            new(TipoDeEntrada.Comando, modelo.MovimentoPausado ? Textos.MenuRetomar : Textos.MenuPausar, (int)ComandoDoMenu.AlternarMovimento),
            EntradaDoMenu.Separador,
            new(TipoDeEntrada.Submenu, Textos.MenuEmocaoDominante, Filhas: emocoes),
        };
        if (modelo.Tamagotchi)
        {
            principal.Add(SubmenuDosItens(modelo));
            principal.Add(new(TipoDeEntrada.Comando, Textos.MenuConteudoAdulto, (int)ComandoDoMenu.ConteudoAdulto, Marcada: modelo.ConteudoAdulto));
        }
        principal.Add(new(TipoDeEntrada.Comando, Textos.MenuModoTelaCheia, (int)ComandoDoMenu.ModoTelaCheia, Marcada: modelo.ModoTelaCheia));
        // Fase 8 (DEC-038): o painel de energia (o núcleo o ignora com o Buzzy escondido) e as configurações.
        if (modelo.PainelDeEnergia || modelo.Configuracoes) principal.Add(EntradaDoMenu.Separador);
        if (modelo.PainelDeEnergia) principal.Add(new(TipoDeEntrada.Comando, Textos.MenuEnergia, (int)ComandoDoMenu.Energia, Desabilitada: !modelo.BuzzyVisivel));
        if (modelo.Configuracoes) principal.Add(new(TipoDeEntrada.Comando, Textos.MenuConfiguracoes, (int)ComandoDoMenu.Configuracoes));
        principal.Add(EntradaDoMenu.Separador);
        principal.Add(new(TipoDeEntrada.Comando, Textos.MenuSair, (int)ComandoDoMenu.Sair));
        return principal;
    }

    /// <summary>
    /// O submenu "Itens" (DEC-028): os 13 itens na ordem do enum <see cref="Item"/> (com o conteúdo adulto desligado, só os
    /// de alívio, com os mesmos ids; DEC-033), só com o nome e o desenho do chão como
    /// ícone (só texto em alto contraste), um separador e "Recolher itens", desabilitado sem itens na tela. Com o Buzzy
    /// escondido, o submenu inteiro fica desabilitado: um item invocado nem apareceria.
    /// </summary>
    private static EntradaDoMenu SubmenuDosItens(ModeloDoMenu modelo)
    {
        var itens = new List<EntradaDoMenu>(TabelaDoTamagotchi.Itens.Count + 2);
        for (int i = 0; i < TabelaDoTamagotchi.Itens.Count; i++)
        {
            Item item = TabelaDoTamagotchi.Itens[i];
            if (!modelo.ConteudoAdulto && TabelaDoTamagotchi.Adulto(item)) continue;
            itens.Add(new(TipoDeEntrada.Comando, Textos.Item(item), IdDoPrimeiroItem + i,
                Item: modelo.AltoContraste ? null : PoseDoPersonagem.NomeDoItem(item)));
        }
        itens.Add(EntradaDoMenu.Separador);
        itens.Add(new(TipoDeEntrada.Comando, Textos.MenuRecolherItens, IdDeRecolherItens, Desabilitada: modelo.ItensNaTela <= 0));
        return new(TipoDeEntrada.Submenu, Textos.MenuItens, Desabilitada: !modelo.BuzzyVisivel, Filhas: itens);
    }

    /// <summary>
    /// O estado que o menu mostra, lido na abertura (o texto que o usuário lê é o que vale na escolha): à vista ou não (da
    /// raiz), e do núcleo o movimento pausado, a emoção dominante (a marca de rádio, DEC-027), a chave do tamagotchi e
    /// quantos itens estão na tela ("Recolher itens" só vale com algum, DEC-028); em alto contraste, só texto. Sem núcleo
    /// (antes de criado), o menu de partida. Função pura, testável sem o Windows (revisão de correção do app, achado 3).
    /// </summary>
    internal static ModeloDoMenu ModeloAoAbrir(Nucleo? nucleo, bool visivel, bool altoContraste)
        => new(visivel, nucleo?.Estado.AutonomiaPausada ?? false, nucleo?.Estado.Preferencias.EmocaoDominante,
            AltoContraste: altoContraste, Tamagotchi: nucleo?.Configuracao.Tamagotchi ?? false, ItensNaTela: nucleo?.Estado.Itens.Quantidade ?? 0,
            ConteudoAdulto: nucleo?.Estado.Preferencias.ConteudoAdulto ?? true,
            ModoTelaCheia: nucleo?.Estado.Preferencias.ModoTelaCheia ?? true,
            PainelDeEnergia: nucleo?.Configuracao.PainelDeEnergiaDisponivel ?? false,
            Configuracoes: nucleo?.Configuracao.ConfiguracoesDisponiveis ?? false);

    /// <summary>
    /// O DPI dos ícones do menu: o do monitor em que ele abre (o que contém o ponto ou, num vão, o mais próximo), e não o do
    /// principal; o Windows não amplia o bitmap de um item de menu (revisão de correção do app, achado 3).
    /// </summary>
    internal static int DpiAoAbrir(Topologia topologia, PontoPx ponto)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return topologia.MonitorMaisProximo(ponto).Dpi;
    }

    /// <summary>
    /// O que o id devolvido pelo menu quer dizer. Só os ids das entradas valem; qualquer outro (0 = menu cancelado) não
    /// escolhe nada. A emoção sai da lista fixa <see cref="Expressoes.DeHumor"/> e o item da lista fixa
    /// <see cref="TabelaDoTamagotchi.Itens"/>, nunca de uma conversão do número.
    /// </summary>
    internal static EscolhaDoMenu Escolha(int id) => id switch
    {
        (int)ComandoDoMenu.AlternarVisibilidade => new(ComandoDoMenu.AlternarVisibilidade),
        (int)ComandoDoMenu.AlternarMovimento => new(ComandoDoMenu.AlternarMovimento),
        (int)ComandoDoMenu.Sair => new(ComandoDoMenu.Sair),
        IdDaAutomatica => new(ComandoDoMenu.Emocao, null),
        (int)ComandoDoMenu.ConteudoAdulto => new(ComandoDoMenu.ConteudoAdulto),
        (int)ComandoDoMenu.ModoTelaCheia => new(ComandoDoMenu.ModoTelaCheia),
        (int)ComandoDoMenu.Energia => new(ComandoDoMenu.Energia),
        (int)ComandoDoMenu.Configuracoes => new(ComandoDoMenu.Configuracoes),
        IdDeRecolherItens => new(ComandoDoMenu.RecolherItens),
        >= IdDaPrimeiraEmocao when id - IdDaPrimeiraEmocao < Expressoes.DeHumor.Count => new(ComandoDoMenu.Emocao, Expressoes.DeHumor[id - IdDaPrimeiraEmocao]),
        >= IdDoPrimeiroItem when id - IdDoPrimeiroItem < TabelaDoTamagotchi.Itens.Count => new(ComandoDoMenu.Item, Item: TabelaDoTamagotchi.Itens[id - IdDoPrimeiroItem]),
        _ => EscolhaDoMenu.Nenhuma,
    };

    /// <summary>
    /// Mostra o menu no <paramref name="ponto"/> e devolve a escolha. Os ícones são ampliados pelo DPI do monitor do
    /// ponto (<paramref name="dpi"/>, <see cref="IconesDoMenu.Fator"/>), porque o Windows não amplia o bitmap de um item.
    /// O log <c>MENU|fechado=</c> continua com o nome de <see cref="ComandoDoMenu"/>, que a verificação de tela lê, com o
    /// <c>argumento=</c> da emoção ou do item escolhido, e traz as contas dos ícones da abertura (criados = apagados) e o
    /// lado dos rostos (<c>lado=</c>) e dos itens (<c>ladoItem=</c>). O comando escolhido roda em <paramref name="executar"/>
    /// com o dono temporário do menu ainda vivo e em primeiro plano, para uma janela aberta por ele (o painel, as
    /// configurações) receber a ativação (DEC-038, item 4).
    /// </summary>
    internal static EscolhaDoMenu Mostrar(PontoPx ponto, ModeloDoMenu modelo, int dpi, bool abrirParaCima, Action<EscolhaDoMenu> executar)
    {
        ArgumentNullException.ThrowIfNull(executar);
        IReadOnlyList<EntradaDoMenu> entradas = Entradas(modelo);
        int fator = IconesDoMenu.Fator(dpi); // só para o log; a ampliação dos ícones é a de ComMenuMontadoNoDpi
        using var dono = new HwndSource(new HwndSourceParameters("Buzzy.Menu")
        {
            WindowStyle = Win32.WS_POPUP,
            ExtendedWindowStyle = (int)Win32.WS_EX_TOOLWINDOW,
            PositionX = ponto.X,
            PositionY = ponto.Y,
            Width = 0,
            Height = 0,
        });

        bool primeiroPlano = false;
        AberturaDoMenu abertura = ComMenuMontadoNoDpi(entradas, dpi, (menu, icones) =>
        {
            primeiroPlano = Win32.SetForegroundWindow(dono.Handle);
            // Para diagnóstico e para a verificação da Fase 1: o dono do menu é janela do
            // próprio Buzzy; sem primeiro plano, o menu não recebe teclado nem fecha ao clicar fora.
            Diagnostico.Evento("MENU", ("exibindo", "sim"), ("dono", dono.Handle), ("donoEmPrimeiroPlano", primeiroPlano),
                ("emocaoMarcada", NomeNoLog(modelo.EmocaoDominante)), ("conteudoAdulto", modelo.ConteudoAdulto ? "sim" : "nao"), ("modoTelaCheia", modelo.ModoTelaCheia ? "sim" : "nao"), ("icones", icones));
            uint opcoes = Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | Win32.TPM_RIGHTBUTTON | Win32.TPM_LEFTALIGN
                | (abrirParaCima ? Win32.TPM_BOTTOMALIGN : Win32.TPM_TOPALIGN);
            int escolhido = Win32.TrackPopupMenuEx(menu, opcoes, ponto.X, ponto.Y, dono.Handle, 0);

            // Recomendação da documentação do Shell_NotifyIcon: uma mensagem qualquer ao dono
            // depois do menu, para o próximo clique fora dele funcionar.
            Win32.PostMessage(dono.Handle, Win32.WM_NULL, 0, 0);
            return escolhido;
        });

        EscolhaDoMenu escolha = Escolha(abertura.Id);
        var campos = new List<(string, object?)> { ("fechado", escolha.Comando) };
        if (escolha.Comando == ComandoDoMenu.Emocao) campos.Add(("argumento", NomeNoLog(escolha.Emocao)));
        if (escolha.Comando == ComandoDoMenu.Item) campos.Add(("argumento", escolha.Item));
        campos.Add(("donoEmPrimeiroPlano", primeiroPlano));
        campos.Add(("icones", abertura.Icones));
        campos.Add(("bitmapsCriados", abertura.BitmapsCriados));
        campos.Add(("bitmapsApagados", abertura.BitmapsApagados));
        campos.Add(("lado", PedeRosto(entradas) ? $"{IconesDoMenu.LarguraDoRosto * fator}x{IconesDoMenu.AlturaDoRosto * fator}" : "-"));
        campos.Add(("ladoItem", PedeItem(entradas) ? $"{IconesDoMenu.LadoDoItem * fator}x{IconesDoMenu.LadoDoItem * fator}" : "-"));
        campos.Add(("dpi", dpi));
        Diagnostico.Evento("MENU", [.. campos]);
        executar(escolha);
        return escolha;
    }

    /// <summary>
    /// <see cref="ComMenuMontado"/> com os ícones ampliados pelo fator inteiro do <paramref name="dpi"/> do monitor em que o
    /// menu abre (<see cref="IconesDoMenu.Fator"/>): é a abertura de <see cref="Mostrar"/>, testável sem exibir.
    /// </summary>
    internal static AberturaDoMenu ComMenuMontadoNoDpi(IReadOnlyList<EntradaDoMenu> entradas, int dpi, Func<nint, int, int> exibir)
        => ComMenuMontado(entradas, IconesDoMenu.Fator(dpi), exibir);

    /// <summary>
    /// Uma abertura do menu, do começo ao fim: monta as entradas num menu novo, com os rostos em bitmaps desta abertura,
    /// chama <paramref name="exibir"/> com o menu e o número de ícones e devolve o id que ela escolheu. Depois, mesmo com
    /// erro, destrói o menu (o DestroyMenu do principal destrói o submenu, anexado logo depois de criado) e só então apaga
    /// os bitmaps, que o DestroyMenu não apaga: o <c>using</c> deles fica declarado antes do <c>try</c>.
    /// </summary>
    internal static AberturaDoMenu ComMenuMontado(IReadOnlyList<EntradaDoMenu> entradas, int fator, Func<nint, int, int> exibir)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        ArgumentNullException.ThrowIfNull(exibir);
        var bitmaps = new BitmapsDoMenu();
        int icones = 0, id = 0;
        using (bitmaps)
        {
            nint menu = Win32.CreatePopupMenu();
            if (menu == 0)
            {
                Diagnostico.Evento("MENU", ("menu", "falhou"), ("codigo", Marshal.GetLastPInvokeError()));
                return new AberturaDoMenu(0, 0, 0, 0);
            }
            try
            {
                icones = Montar(menu, entradas, bitmaps, fator);
                id = exibir(menu, icones);
            }
            finally
            {
                Win32.DestroyMenu(menu);
            }
        }
        return new AberturaDoMenu(id, icones, bitmaps.Criados, bitmaps.Apagados);
    }

    /// <summary>
    /// Põe as entradas no menu, na ordem, por InsertMenuItemW com a posição explícita. Um submenu é anexado ao item logo
    /// depois de criado e só então recebe as linhas dele (crítica, L13): se algo falhar no meio, o DestroyMenu do menu
    /// principal o destrói junto. Sem MNS_CHECKORBMP, a marca de rádio e o rosto ficam lado a lado. Uma falha do Windows
    /// deixa o item de fora, ou sem ícone, e vai para o log só com o código. Devolve quantos itens ficaram com ícone.
    /// </summary>
    private static int Montar(nint menu, IReadOnlyList<EntradaDoMenu> entradas, BitmapsDoMenu bitmaps, int fator)
    {
        int icones = 0;
        uint posicao = 0;
        foreach (EntradaDoMenu entrada in entradas)
        {
            var item = new Win32.MENUITEMINFO { cbSize = Marshal.SizeOf<Win32.MENUITEMINFO>() };
            nint submenu = 0;
            switch (entrada.Tipo)
            {
                case TipoDeEntrada.Separador:
                    item.fMask = Win32.MIIM_FTYPE;
                    item.fType = Win32.MFT_SEPARATOR;
                    break;

                case TipoDeEntrada.Submenu:
                    submenu = Win32.CreatePopupMenu();
                    if (submenu == 0)
                    {
                        Diagnostico.Evento("MENU", ("submenu", "falhou"), ("codigo", Marshal.GetLastPInvokeError()));
                        continue;
                    }
                    item.fMask = Win32.MIIM_SUBMENU | Win32.MIIM_STRING | Win32.MIIM_FTYPE | Win32.MIIM_STATE;
                    item.fType = Win32.MFT_STRING;
                    item.fState = entrada.Desabilitada ? Win32.MFS_GRAYED : Win32.MFS_ENABLED;
                    item.hSubMenu = submenu;
                    item.dwTypeData = entrada.Rotulo;
                    break;

                default:
                    item.fMask = Win32.MIIM_ID | Win32.MIIM_STRING | Win32.MIIM_FTYPE | Win32.MIIM_STATE;
                    item.fType = Win32.MFT_STRING | (entrada.Radio ? Win32.MFT_RADIOCHECK : 0);
                    item.fState = (entrada.Marcada ? Win32.MFS_CHECKED : 0) | (entrada.Desabilitada ? Win32.MFS_GRAYED : 0);
                    item.wID = (uint)entrada.Id;
                    item.dwTypeData = entrada.Rotulo;
                    if (Icone(entrada, fator) is { } icone)
                    {
                        nint bitmap = bitmaps.Criar(icone.Pixels, icone.Largura, icone.Altura);
                        if (bitmap != 0)
                        {
                            item.fMask |= Win32.MIIM_BITMAP;
                            item.hbmpItem = bitmap;
                        }
                    }
                    break;
            }

            if (!Win32.InsertMenuItem(menu, posicao, true, ref item))
            {
                Diagnostico.Evento("MENU", ("item", "falhou"), ("codigo", Marshal.GetLastPInvokeError()));
                if (submenu != 0) Win32.DestroyMenu(submenu); // não chegou a ser anexado: destruído na hora
                continue;
            }
            posicao++;
            if (item.hbmpItem != 0) icones++;
            if (submenu != 0) icones += Montar(submenu, entrada.Filhas ?? [], bitmaps, fator);
        }
        return icones;
    }

    /// <summary>
    /// Os pixels do ícone de uma linha, já ampliados pelo <paramref name="fator"/> do DPI, ou nulo: só texto. O rosto é a
    /// célula de expressoes.png (40 × 32, C29); o item, o desenho do chão (24 × 24).
    /// </summary>
    private static (uint[] Pixels, int Largura, int Altura)? Icone(EntradaDoMenu entrada, int fator)
    {
        if (entrada.Rosto is { } rosto)
            return (IconesDoMenu.Ampliar(IconesDoMenu.Rosto(rosto), fator), IconesDoMenu.LarguraDoRosto * fator, IconesDoMenu.AlturaDoRosto * fator);
        if (entrada.Item is { } desenho)
            return (IconesDoMenu.Ampliar(IconesDoMenu.Item(desenho), fator), IconesDoMenu.LadoDoItem * fator, IconesDoMenu.LadoDoItem * fator);
        return null;
    }

    /// <summary>A emoção no log do menu: o nome do valor, ou "Automatica", como na gravação do comando.</summary>
    private static string NomeNoLog(Expressao? emocao) => emocao?.ToString() ?? "Automatica";

    /// <summary>Se alguma entrada, em qualquer nível, pede um rosto como ícone (fora do alto contraste).</summary>
    private static bool PedeRosto(IEnumerable<EntradaDoMenu> entradas)
        => entradas.Any(e => e.Rosto is not null || (e.Filhas is { } filhas && PedeRosto(filhas)));

    /// <summary>Se alguma entrada, em qualquer nível, pede o desenho de um item como ícone (fora do alto contraste).</summary>
    private static bool PedeItem(IEnumerable<EntradaDoMenu> entradas)
        => entradas.Any(e => e.Item is not null || (e.Filhas is { } filhas && PedeItem(filhas)));
}
