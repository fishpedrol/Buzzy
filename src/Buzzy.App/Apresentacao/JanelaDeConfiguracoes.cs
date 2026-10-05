using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// A janela de configurações (Fase 8; DEC-038, itens 5 a 8; critérios 7 a 10): uma coluna em três grupos — Comportamento
/// (energia, desviar da tela cheia, conteúdo adulto), Aparência (tamanho, sempre no topo) e Windows (o início com o Windows) —
/// e o botão Fechar (Esc). Cada controle aplica na hora pelo pedido dele, sem OK nem Cancelar,
/// sem campo de texto; a marca só muda pela raiz (<see cref="Atualizar"/>), a partir do que o núcleo gravou. Janela comum,
/// nunca topmost, com a fonte do sistema e o tema "chapéu de palha" (<see cref="TemaDoBuzzy"/>, DEC-039; no alto contraste, as
/// cores do sistema), largura máxima de 420 DIP e textos de ajuda com quebra. Nada periódico.
/// </summary>
internal sealed class JanelaDeConfiguracoes : Window
{
    /// <summary>A largura máxima do conteúdo, em DIP (os textos de ajuda quebram dentro dela).</summary>
    internal const double LarguraMaxima = 420;

    private readonly EscalaDoPersonagem _escalaEmVigor;
    private readonly TextBlock _proximaVez;

    /// <param name="preferencias">As preferências do núcleo na abertura.</param>
    /// <param name="escalaEmVigor">O tamanho com que o Buzzy abriu, para o aviso de "próxima vez".</param>
    /// <param name="comConteudoAdulto">Se a caixa do conteúdo adulto existe (só com o tamagotchi).</param>
    internal JanelaDeConfiguracoes(Preferencias preferencias, EscalaDoPersonagem escalaEmVigor, bool comConteudoAdulto)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        _escalaEmVigor = escalaEmVigor;
        Title = Textos.ConfigTitulo;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = true;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontSize = 13;
        UseLayoutRounding = true;
        TemaDoBuzzy.Aplicar(this);

        Energia = new Seletor<NivelDeEnergia>(Textos.EnergiaGrupo,
        [
            (NivelDeEnergia.Baixa, Textos.EnergiaBaixa, null),
            (NivelDeEnergia.Media, Textos.EnergiaMedia, null),
            (NivelDeEnergia.Alta, Textos.EnergiaAlta, null),
        ], Textos.ConfigEnergiaAjuda);
        TelaCheia = Caixa(Textos.ConfigTelaCheia, Textos.ConfigTelaCheiaAjuda);
        Adulto = comConteudoAdulto ? Caixa(Textos.ConfigAdulto, Textos.ConfigAdultoAjuda) : null;
        Tamanho = new Seletor<EscalaDoPersonagem>(Textos.ConfigTamanho,
        [
            (EscalaDoPersonagem.Pequena, Textos.ConfigTamanhoPequeno, null),
            (EscalaDoPersonagem.Media, Textos.ConfigTamanhoMedio, null),
            (EscalaDoPersonagem.Grande, Textos.ConfigTamanhoGrande, null),
        ], Textos.ConfigTamanhoProximaVez);
        _proximaVez = Ajuda(Textos.ConfigTamanhoProximaVez);
        Topo = Caixa(Textos.ConfigTopo, Textos.ConfigTopoAjuda);
        Fechar = new Button { Content = Textos.ConfigFechar, IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        AutomationProperties.SetName(Fechar, Textos.ConfigFechar.Replace("_", "", StringComparison.Ordinal));
        Fechar.Click += (_, _) => Close();

        var comportamento = new StackPanel();
        comportamento.Children.Add(Energia);
        comportamento.Children.Add(TelaCheia);
        comportamento.Children.Add(Ajuda(Textos.ConfigTelaCheiaAjuda));
        if (Adulto is not null)
        {
            comportamento.Children.Add(Adulto);
            comportamento.Children.Add(Ajuda(Textos.ConfigAdultoAjuda));
        }
        var aparencia = new StackPanel();
        aparencia.Children.Add(Tamanho);
        aparencia.Children.Add(_proximaVez);
        aparencia.Children.Add(Topo);
        aparencia.Children.Add(Ajuda(Textos.ConfigTopoAjuda));
        Inicio = Caixa(Textos.ConfigInicio, Textos.ConfigInicioAjuda);
        EstadoDoInicio = Ajuda("");
        EstadoDoInicio.Visibility = Visibility.Collapsed;
        Windows = new StackPanel();
        Windows.Children.Add(Inicio);
        Windows.Children.Add(Ajuda(Textos.ConfigInicioAjuda));
        Windows.Children.Add(EstadoDoInicio);

        var coluna = new StackPanel { Margin = new Thickness(16, 12, 16, 16), MaxWidth = LarguraMaxima };
        coluna.Children.Add(Grupo(Textos.ConfigComportamento, comportamento));
        coluna.Children.Add(Grupo(Textos.ConfigAparencia, aparencia));
        coluna.Children.Add(Grupo(Textos.ConfigWindows, Windows));
        coluna.Children.Add(Fechar);
        var pagina = new DockPanel { LastChildFill = true };
        FrameworkElement cabecalho = TemaDoBuzzy.Cabecalho(Textos.ConfigTitulo, 18);
        DockPanel.SetDock(cabecalho, Dock.Top);
        pagina.Children.Add(cabecalho);
        // Numa área útil baixa (DPI alto, tela pequena), a coluna rola; o foco do teclado a leva até o controle.
        Rolagem = new ScrollViewer
        {
            Content = coluna,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            IsTabStop = false,
        };
        pagina.Children.Add(Rolagem);
        Content = pagina;

        Atualizar(preferencias);
        Energia.Escolheu += nivel => PediuEnergia?.Invoke(nivel);
        Tamanho.Escolheu += escala => PediuEscala?.Invoke(escala);
        TelaCheia.Pedido += ligado => PediuTelaCheia?.Invoke(ligado);
        if (Adulto is not null) Adulto.Pedido += ligado => PediuAdulto?.Invoke(ligado);
        Topo.Pedido += ligado => PediuTopo?.Invoke(ligado);
        Activated += (_, _) => Ativada?.Invoke();
        Loaded += (_, _) => Energia.Focar();
    }

    internal event Action<NivelDeEnergia>? PediuEnergia;
    internal event Action<bool>? PediuTelaCheia;
    internal event Action<bool>? PediuAdulto;
    internal event Action<EscalaDoPersonagem>? PediuEscala;
    internal event Action<bool>? PediuTopo;

    /// <summary>A janela foi ativada (para reler o estado do início com o Windows, DEC-038, item 7).</summary>
    internal event Action? Ativada;

    internal Seletor<NivelDeEnergia> Energia { get; }
    internal CaixaDeComando TelaCheia { get; }
    internal CaixaDeComando? Adulto { get; }
    internal Seletor<EscalaDoPersonagem> Tamanho { get; }
    internal CaixaDeComando Topo { get; }
    internal Button Fechar { get; }

    /// <summary>O grupo "Windows", com o início com o Windows.</summary>
    internal StackPanel Windows { get; }

    /// <summary>"Iniciar com o Windows": quem a liga à porta é o <see cref="Composicao.ControleDoInicio"/>.</summary>
    internal CaixaDeComando Inicio { get; }

    /// <summary>A linha de estado do início, só quando há algo a dizer (região viva).</summary>
    internal TextBlock EstadoDoInicio { get; }

    /// <summary>Se o aviso de "próxima vez" está à vista.</summary>
    internal bool AvisoDaProximaVez => _proximaVez.Visibility == Visibility.Visible;

    /// <summary>A rolagem da coluna (abaixo da faixa de cima).</summary>
    internal ScrollViewer Rolagem { get; }

    /// <summary>Limita a altura da janela, em DIP, à área útil do monitor (a coluna rola no que passar).</summary>
    internal void LimitarAltura(double alturaDip)
    {
        if (alturaDip > 0 && !double.IsNaN(alturaDip) && MaxHeight != alturaDip) MaxHeight = alturaDip;
    }

    /// <summary>O HWND, depois de criado.</summary>
    internal nint Hwnd => new WindowInteropHelper(this).Handle;

    /// <summary>O retângulo da própria janela, em pixels físicos; nulo antes de criada.</summary>
    internal RetanguloPx? RetanguloNaTela() => Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    /// <summary>As marcas a partir das preferências do núcleo, sem pedido; o aviso do tamanho acompanha.</summary>
    internal void Atualizar(Preferencias preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        Energia.Marcar(preferencias.Energia);
        TelaCheia.Marcar(preferencias.ModoTelaCheia);
        Adulto?.Marcar(preferencias.ConteudoAdulto);
        Tamanho.Marcar(preferencias.Escala);
        Topo.Marcar(preferencias.SempreNoTopo);
        _proximaVez.Visibility = preferencias.Escala == _escalaEmVigor ? Visibility.Collapsed : Visibility.Visible;
    }

    private static CaixaDeComando Caixa(string rotulo, string ajuda)
    {
        var caixa = new CaixaDeComando { Content = rotulo, Margin = new Thickness(6, 8, 6, 0) };
        AutomationProperties.SetName(caixa, rotulo.Replace("_", "", StringComparison.Ordinal));
        AutomationProperties.SetHelpText(caixa, ajuda);
        return caixa;
    }

    private static TextBlock Ajuda(string texto)
    {
        var ajuda = new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(32, 2, 6, 4) };
        ajuda.SetResourceReference(TextBlock.ForegroundProperty, TemaDoBuzzy.ChaveTextoSuave);
        return ajuda;
    }

    private static GroupBox Grupo(string titulo, UIElement conteudo) => new()
    {
        Header = titulo,
        Content = conteudo,
        Padding = new Thickness(4),
        Margin = new Thickness(0, 0, 0, 8),
    };
}
