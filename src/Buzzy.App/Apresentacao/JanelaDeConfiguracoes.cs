using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Apresentacao;

// Configurações numa coluna: Comportamento, Aparência e Windows, mais Fechar (Esc).
// Cada controle aplica na hora, sem OK/Cancelar nem campo de texto; a marca só muda
// por Atualizar, com o que o núcleo gravou. Janela comum (nunca topmost), tema
// chapéu de palha (cores do sistema no alto contraste). Nada periódico.
internal sealed class JanelaDeConfiguracoes : Window
{
    // DIP; os textos de ajuda quebram dentro dela.
    internal const double LarguraMaxima = 420;

    private readonly EscalaDoPersonagem _escalaEmVigor;
    private readonly TextBlock _proximaVez;
    private readonly Dictionary<Item, CaixaDeComando> _caixasDosItensAdultos = [];
    // Só os itens adultos desta edição ganham caixa.
    private readonly ConjuntoDeItens _itensDaEdicao;
    private readonly Dictionary<Item, Image> _iconesDosItensAdultos = [];
    // Ao lado de cada droga ilícita da edição, o uso por conta própria.
    private readonly Dictionary<Item, CaixaDeComando> _caixasPorContaPropria = [];

    // escalaEmVigor é o tamanho com que o Buzzy abriu, pro aviso de "próxima vez".
    // A caixa do conteúdo adulto só existe com o tamagotchi.
    internal JanelaDeConfiguracoes(Preferencias preferencias, EscalaDoPersonagem escalaEmVigor, bool comConteudoAdulto, ConjuntoDeItens? itensDaEdicao = null)
    {
        _itensDaEdicao = itensDaEdicao ?? TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa);
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
        Travessia = Caixa(Textos.ConfigTravessia, Textos.ConfigTravessiaAjuda);
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
        comportamento.Children.Add(Travessia);
        comportamento.Children.Add(Ajuda(Textos.ConfigTravessiaAjuda));
        if (Adulto is not null)
        {
            comportamento.Children.Add(Adulto);
            comportamento.Children.Add(Ajuda(Textos.ConfigAdultoAjuda));
            comportamento.Children.Add(ControlesDosItensAdultos());
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
        // Com área útil baixa (DPI alto, tela pequena) a coluna rola até o controle com foco.
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
        Travessia.Pedido += ligado => PediuTravessia?.Invoke(ligado);
        if (Adulto is not null) Adulto.Pedido += ligado => PediuAdulto?.Invoke(ligado);
        foreach ((Item item, CaixaDeComando caixa) in _caixasDosItensAdultos)
            caixa.Pedido += ligado => PediuItemAdulto?.Invoke(item, ligado);
        foreach ((Item item, CaixaDeComando caixa) in _caixasPorContaPropria)
            caixa.Pedido += ligado => PediuPorContaPropria?.Invoke(item, ligado);
        Topo.Pedido += ligado => PediuTopo?.Invoke(ligado);
        Activated += (_, _) => Ativada?.Invoke();
        Loaded += (_, _) =>
        {
            AtualizarIconesDosItensAdultos();
            Energia.Focar();
        };
        DpiChanged += (_, _) => AtualizarIconesDosItensAdultos();
    }

    internal event Action<NivelDeEnergia>? PediuEnergia;
    internal event Action<bool>? PediuTelaCheia;
    internal event Action<bool>? PediuTravessia;
    internal event Action<Item, bool>? PediuPorContaPropria;
    internal event Action<bool>? PediuAdulto;
    internal event Action<Item, bool>? PediuItemAdulto;
    internal event Action<EscalaDoPersonagem>? PediuEscala;
    internal event Action<bool>? PediuTopo;

    // Pra reler o estado do início com o Windows, que pode mudar por fora.
    internal event Action? Ativada;

    internal Seletor<NivelDeEnergia> Energia { get; }
    internal CaixaDeComando TelaCheia { get; }

    // Desligada por padrão.
    internal CaixaDeComando Travessia { get; }
    internal CaixaDeComando? Adulto { get; }
    internal IReadOnlyDictionary<Item, CaixaDeComando> CaixasDosItensAdultos => _caixasDosItensAdultos;

    // Uma por droga ilícita da edição; vazio na edição pública.
    internal IReadOnlyDictionary<Item, CaixaDeComando> CaixasPorContaPropria => _caixasPorContaPropria;
    internal Seletor<EscalaDoPersonagem> Tamanho { get; }
    internal CaixaDeComando Topo { get; }
    internal Button Fechar { get; }

    internal StackPanel Windows { get; }

    // Quem liga essa caixa ao registro é o Composicao.ControleDoInicio.
    internal CaixaDeComando Inicio { get; }

    // Só aparece quando há algo a dizer (região viva).
    internal TextBlock EstadoDoInicio { get; }

    internal bool AvisoDaProximaVez => _proximaVez.Visibility == Visibility.Visible;

    internal ScrollViewer Rolagem { get; }

    // DIP, a área útil do monitor; a coluna rola no que passar.
    internal void LimitarAltura(double alturaDip)
    {
        if (alturaDip > 0 && !double.IsNaN(alturaDip) && MaxHeight != alturaDip) MaxHeight = alturaDip;
    }

    internal nint Hwnd => new WindowInteropHelper(this).Handle;

    // Pixels físicos; nulo antes de a janela existir.
    internal RetanguloPx? RetanguloNaTela() => Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    // Marca tudo pelo núcleo, sem levantar pedido.
    internal void Atualizar(Preferencias preferencias)
    {
        ArgumentNullException.ThrowIfNull(preferencias);
        Energia.Marcar(preferencias.Energia);
        TelaCheia.Marcar(preferencias.ModoTelaCheia);
        Travessia.Marcar(preferencias.AtravessarMonitores);
        Adulto?.Marcar(preferencias.ConteudoAdulto);
        foreach ((Item item, CaixaDeComando caixa) in _caixasDosItensAdultos)
            caixa.Marcar(preferencias.ItensAdultosHabilitados.Contem(item));
        foreach ((Item item, CaixaDeComando caixa) in _caixasPorContaPropria)
            caixa.Marcar(preferencias.ItensPorContaPropria.Contem(item));
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

    private StackPanel ControlesDosItensAdultos()
    {
        var grupo = new StackPanel { Margin = new Thickness(18, 4, 2, 0) };
        var titulo = new TextBlock
        {
            Text = Textos.ConfigItensAdultos,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 6, 0),
        };
        titulo.SetResourceReference(TextBlock.ForegroundProperty, TemaDoBuzzy.ChaveTexto);
        grupo.Children.Add(titulo);
        grupo.Children.Add(Ajuda(Textos.ConfigItensAdultosAjuda));

        foreach (Item item in TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.Adulto(i) && _itensDaEdicao.Contem(i)))
        {
            string nome = Textos.NomeDoItem(item);
            var icone = new IconeDecorativo
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(icone, BitmapScalingMode.NearestNeighbor);
            _iconesDosItensAdultos.Add(item, icone);

            var conteudo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            conteudo.Children.Add(icone);
            conteudo.Children.Add(new TextBlock { Text = nome, VerticalAlignment = VerticalAlignment.Center });
            var caixa = new CaixaDeComando { Content = conteudo, Margin = new Thickness(0, 3, 6, 2), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(caixa, nome);
            AutomationProperties.SetHelpText(caixa, Textos.ConfigItemAdultoAjuda);
            _caixasDosItensAdultos.Add(item, caixa);
            if (!TabelaDoTamagotchi.Ilicitos.Contem(item))
            {
                grupo.Children.Add(caixa);
                continue;
            }
            // Droga ilícita: a caixa do uso por conta própria vai na mesma linha.
            string nomeProprio = string.Format(System.Globalization.CultureInfo.InvariantCulture, Textos.ConfigPorContaPropriaNome, nome);
            var propria = new CaixaDeComando { Content = Textos.ConfigPorContaPropria, Margin = new Thickness(12, 3, 6, 2), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(propria, nomeProprio);
            AutomationProperties.SetHelpText(propria, Textos.ConfigPorContaPropriaAjuda);
            _caixasPorContaPropria.Add(item, propria);
            var linha = new WrapPanel { Orientation = Orientation.Horizontal };
            linha.Children.Add(caixa);
            linha.Children.Add(propria);
            grupo.Children.Add(linha);
        }
        return grupo;
    }

    private void AtualizarIconesDosItensAdultos()
    {
        if (_iconesDosItensAdultos.Count == 0) return;
        int dpi = (int)Math.Round(VisualTreeHelper.GetDpi(this).PixelsPerInchX, MidpointRounding.AwayFromZero);
        foreach ((Item item, Image icone) in _iconesDosItensAdultos)
            icone.Source = SpriteDoItem.Renderizar(item, dpi);
    }

    // Ícone só enfeita: sem peer, o nome da caixa já diz o item ao leitor de tela.
    private sealed class IconeDecorativo : Image
    {
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => null!;
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
