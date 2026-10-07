using System.Runtime.InteropServices;
using System.Windows.Interop;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Plataforma;

// O nome vai pro log MENU|fechado=, que a verificação de tela lê: não renomear.
internal enum ComandoDoMenu
{
    Nenhum = 0,
    AlternarVisibilidade = 1,
    Sair = 2,

    // Pausar/retomar o movimento autônomo.
    AlternarMovimento = 3,

    // Uma das 14 caras de humor ou "Automática".
    Emocao = 4,

    Item = 5,

    RecolherItens = 6,

    ConteudoAdulto = 7,

    // "Desviar da tela cheia".
    ModoTelaCheia = 8,

    // Abre o painel compacto de energia.
    Energia = 9,

    Configuracoes = 10,
}

// Emocao nula = "Automática".
internal readonly record struct EscolhaDoMenu(ComandoDoMenu Comando, Expressao? Emocao = null, Item? Item = null)
{
    internal static EscolhaDoMenu Nenhuma => new(ComandoDoMenu.Nenhum);
}

// Estado lido na hora em que o menu abre. Em alto contraste, só texto (sem rostos).
// "Itens" só existe com o tamagotchi ligado; com conteúdo adulto desligado, só os de alívio.
internal sealed record ModeloDoMenu(bool BuzzyVisivel, bool MovimentoPausado, Expressao? EmocaoDominante, bool AltoContraste, bool Tamagotchi, int ItensNaTela = 0, bool ConteudoAdulto = true, bool ModoTelaCheia = true,
    bool PainelDeEnergia = false, bool Configuracoes = false)
{
    // Item adulto só aparece com a chave geral ligada e ele marcado aqui. O padrão mostra
    // tudo; em produção vem sempre do núcleo (ModeloAoAbrir).
    internal ConjuntoDeItens ItensAdultosHabilitados { get; init; } = Preferencias.TodosOsItensAdultos;
}

internal enum TipoDeEntrada
{
    Comando,
    Separador,
    Submenu,
}

// Uma linha do menu sem nada do Windows. Rotulo leva & antes da tecla de acesso.
// Id é o que TrackPopupMenuEx devolve. Rosto e Item são chaves da arte do ícone;
// no máximo um dos dois por linha.
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

// Id 0 = cancelado. As contas dos ícones vão pro log.
internal readonly record struct AberturaDoMenu(int Id, int Icones, int BitmapsCriados, int BitmapsApagados);

// Menu do Buzzy, o mesmo no personagem e na bandeja. Menu nativo pra ganhar de graça
// teclas de acesso, teclado, acessibilidade e escala Per-Monitor V2.
//
// Menu só fecha direito ao clicar fora se o dono estiver em primeiro plano. Por isso o
// dono é uma janela oculta temporária, criada a cada abertura e destruída no fim, pro
// Windows devolver a ativação à próxima janela na ordem Z (sem a gente ler qual é).
//
// A lista de entradas (Entradas) é pura e testável; a montagem no HMENU acontece a
// cada abertura, com os bitmaps dos ícones criados e apagados ali mesmo.
internal static class MenuNativo
{
    internal const int IdDaAutomatica = 999;

    // As 14 emoções seguem a ordem de Expressoes.DeHumor a partir daqui.
    internal const int IdDaPrimeiraEmocao = 1000;

    // Os 13 itens seguem a ordem de TabelaDoTamagotchi.Itens a partir daqui.
    internal const int IdDoPrimeiroItem = 2000;

    internal const int IdDeRecolherItens = 2999;

    // Pura, nada do Windows. Emoções na ordem de expressoes.png, rádio na atual.
    // Com o tamagotchi ligado entram "Itens" e "Conteúdo adulto".
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
        // Energia fica desabilitada com o Buzzy escondido (o núcleo ignoraria).
        if (modelo.PainelDeEnergia || modelo.Configuracoes) principal.Add(EntradaDoMenu.Separador);
        if (modelo.PainelDeEnergia) principal.Add(new(TipoDeEntrada.Comando, Textos.MenuEnergia, (int)ComandoDoMenu.Energia, Desabilitada: !modelo.BuzzyVisivel));
        if (modelo.Configuracoes) principal.Add(new(TipoDeEntrada.Comando, Textos.MenuConfiguracoes, (int)ComandoDoMenu.Configuracoes));
        principal.Add(EntradaDoMenu.Separador);
        principal.Add(new(TipoDeEntrada.Comando, Textos.MenuSair, (int)ComandoDoMenu.Sair));
        return principal;
    }

    // Itens filtrados mantêm o mesmo id. Com o Buzzy escondido o submenu inteiro fica
    // desabilitado, porque o item invocado nem apareceria.
    private static EntradaDoMenu SubmenuDosItens(ModeloDoMenu modelo)
    {
        var itens = new List<EntradaDoMenu>(TabelaDoTamagotchi.Itens.Count + 2);
        for (int i = 0; i < TabelaDoTamagotchi.Itens.Count; i++)
        {
            Item item = TabelaDoTamagotchi.Itens[i];
            if (TabelaDoTamagotchi.Adulto(item)
                && (!modelo.ConteudoAdulto || !modelo.ItensAdultosHabilitados.Contem(item))) continue;
            itens.Add(new(TipoDeEntrada.Comando, Textos.Item(item), IdDoPrimeiroItem + i,
                Item: modelo.AltoContraste ? null : PoseDoPersonagem.NomeDoItem(item)));
        }
        itens.Add(EntradaDoMenu.Separador);
        itens.Add(new(TipoDeEntrada.Comando, Textos.MenuRecolherItens, IdDeRecolherItens, Desabilitada: modelo.ItensNaTela <= 0));
        return new(TipoDeEntrada.Submenu, Textos.MenuItens, Desabilitada: !modelo.BuzzyVisivel, Filhas: itens);
    }

    // Lido na abertura: o texto que aparece é o que vale na escolha. Sem núcleo ainda,
    // sai o menu de partida.
    internal static ModeloDoMenu ModeloAoAbrir(Nucleo? nucleo, bool visivel, bool altoContraste)
        => new(visivel, nucleo?.Estado.AutonomiaPausada ?? false, nucleo?.Estado.Preferencias.EmocaoDominante,
            AltoContraste: altoContraste, Tamagotchi: nucleo?.Configuracao.Tamagotchi ?? false, ItensNaTela: nucleo?.Estado.Itens.Quantidade ?? 0,
            ConteudoAdulto: (nucleo?.Estado.Preferencias ?? Preferencias.Padrao).ConteudoAdulto,
            ModoTelaCheia: nucleo?.Estado.Preferencias.ModoTelaCheia ?? true,
            PainelDeEnergia: nucleo?.Configuracao.PainelDeEnergiaDisponivel ?? false,
            Configuracoes: nucleo?.Configuracao.ConfiguracoesDisponiveis ?? false)
        { ItensAdultosHabilitados = (nucleo?.Estado.Preferencias ?? Preferencias.Padrao).ItensAdultosHabilitados };

    // DPI do monitor onde o menu abre (não o do principal), porque o Windows não amplia
    // o bitmap de item de menu.
    internal static int DpiAoAbrir(Topologia topologia, PontoPx ponto)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return topologia.MonitorMaisProximo(ponto).Dpi;
    }

    // Id desconhecido (ou 0, cancelado) não escolhe nada. Emoção e item saem das listas
    // fixas, nunca de cast do número.
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

    // executar roda com o dono temporário ainda vivo e em primeiro plano, pra janela que
    // o comando abrir (painel, configurações) receber a ativação.
    internal static EscolhaDoMenu Mostrar(PontoPx ponto, ModeloDoMenu modelo, int dpi, bool abrirParaCima, Action<EscolhaDoMenu> executar)
    {
        ArgumentNullException.ThrowIfNull(executar);
        IReadOnlyList<EntradaDoMenu> entradas = Entradas(modelo);
        int fator = IconesDoMenu.Fator(dpi); // só pro log; quem amplia é ComMenuMontadoNoDpi
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
            // Sem primeiro plano o menu não recebe teclado nem fecha ao clicar fora.
            Diagnostico.Evento("MENU", ("exibindo", "sim"), ("dono", dono.Handle), ("donoEmPrimeiroPlano", primeiroPlano),
                ("emocaoMarcada", NomeNoLog(modelo.EmocaoDominante)), ("conteudoAdulto", modelo.ConteudoAdulto ? "sim" : "nao"), ("modoTelaCheia", modelo.ModoTelaCheia ? "sim" : "nao"), ("icones", icones));
            uint opcoes = Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | Win32.TPM_RIGHTBUTTON | Win32.TPM_LEFTALIGN
                | (abrirParaCima ? Win32.TPM_BOTTOMALIGN : Win32.TPM_TOPALIGN);
            int escolhido = Win32.TrackPopupMenuEx(menu, opcoes, ponto.X, ponto.Y, dono.Handle, 0);

            // Truque da doc do Shell_NotifyIcon: uma mensagem qualquer pro dono depois do
            // menu, senão o próximo clique fora falha.
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

    // Separado do Mostrar pra testar sem exibir. Fator inteiro do DPI.
    internal static AberturaDoMenu ComMenuMontadoNoDpi(IReadOnlyList<EntradaDoMenu> entradas, int dpi, Func<nint, int, int> exibir)
        => ComMenuMontado(entradas, IconesDoMenu.Fator(dpi), exibir);

    // Ordem importa: DestroyMenu (que leva os submenus junto) e só depois apagar os
    // bitmaps, que o DestroyMenu não apaga. Por isso o using fica por fora do try.
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

    // O submenu é anexado logo depois de criado e só então recebe as linhas: se algo
    // falhar no meio, o DestroyMenu do principal leva ele junto. Sem MNS_CHECKORBMP, rádio
    // e rosto ficam lado a lado. Devolve quantos itens ficaram com ícone.
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
                if (submenu != 0) Win32.DestroyMenu(submenu); // não foi anexado, destrói na hora
                continue;
            }
            posicao++;
            if (item.hbmpItem != 0) icones++;
            if (submenu != 0) icones += Montar(submenu, entrada.Filhas ?? [], bitmaps, fator);
        }
        return icones;
    }

    // Rosto: célula de expressoes.png (40x32). Item: desenho do chão (24x24). Nulo = só texto.
    private static (uint[] Pixels, int Largura, int Altura)? Icone(EntradaDoMenu entrada, int fator)
    {
        if (entrada.Rosto is { } rosto)
            return (IconesDoMenu.Ampliar(IconesDoMenu.Rosto(rosto), fator), IconesDoMenu.LarguraDoRosto * fator, IconesDoMenu.AlturaDoRosto * fator);
        if (entrada.Item is { } desenho)
            return (IconesDoMenu.Ampliar(IconesDoMenu.Item(desenho), fator), IconesDoMenu.LadoDoItem * fator, IconesDoMenu.LadoDoItem * fator);
        return null;
    }

    // "Automatica" sem acento, igual à gravação do comando.
    private static string NomeNoLog(Expressao? emocao) => emocao?.ToString() ?? "Automatica";

    private static bool PedeRosto(IEnumerable<EntradaDoMenu> entradas)
        => entradas.Any(e => e.Rosto is not null || (e.Filhas is { } filhas && PedeRosto(filhas)));

    private static bool PedeItem(IEnumerable<EntradaDoMenu> entradas)
        => entradas.Any(e => e.Item is not null || (e.Filhas is { } filhas && PedeItem(filhas)));
}
