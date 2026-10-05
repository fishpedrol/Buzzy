using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// O tema "chapéu de palha" das configurações e do painel de energia (DEC-039, escolha do usuário): as cores da paleta do
/// Buzzy (IDENTIDADE_VISUAL.md, seção 3) — fundo creme, faixa azul-marinho do pelo no topo com o retrato em pixel art e a
/// aba de palha com a faixa vermelha do chapéu embaixo, marcas em palha, foco em vermelho. Só a aparência muda: os controles,
/// os nomes, a ordem do teclado e os peers são os mesmos, e a fonte é a do sistema. No alto contraste do Windows, o tema sai
/// e tudo volta às cores do sistema, também com a janela aberta. Montado em código, sem XAML, como o resto do aplicativo.
/// Nada periódico: só o aviso de mudança das configurações do sistema.
/// </summary>
internal static class TemaDoBuzzy
{
    // Paleta (IDENTIDADE_VISUAL.md, seção 3), e dois tons derivados para o fundo e o texto de ajuda.
    internal static readonly Color Contorno = Rgb(0x12, 0x18, 0x30);
    internal static readonly Color PeloEscuro = Rgb(0x1B, 0x27, 0x48);
    internal static readonly Color Pelo = Rgb(0x28, 0x3A, 0x5F);
    internal static readonly Color PeloClaro = Rgb(0x3B, 0x54, 0x86);
    internal static readonly Color Palha = Rgb(0xEF, 0xB2, 0x62);
    internal static readonly Color PalhaEscura = Rgb(0xC2, 0x7F, 0x45);
    internal static readonly Color Faixa = Rgb(0xB8, 0x3A, 0x37);
    internal static readonly Color CremeClaro = Rgb(0xFF, 0xE9, 0xCC);
    internal static readonly Color Fundo = Rgb(0xFF, 0xF6, 0xE8);
    internal static readonly Color TextoSuave = Rgb(0x6A, 0x34, 0x19);
    internal static readonly Color Branco = Rgb(0xFF, 0xFF, 0xFF);

    // Chaves dos pincéis que as janelas usam por referência dinâmica (o alto contraste troca o dicionário inteiro).
    internal const string ChaveFundo = "Buzzy.Fundo";
    internal const string ChaveTexto = "Buzzy.Texto";
    internal const string ChaveTextoSuave = "Buzzy.TextoSuave";
    internal const string ChaveCabecalho = "Buzzy.Cabecalho";
    internal const string ChaveTextoDoCabecalho = "Buzzy.TextoDoCabecalho";
    internal const string ChaveAba = "Buzzy.Aba";
    internal const string ChaveFaixa = "Buzzy.Faixa";
    internal const string ChaveBorda = "Buzzy.Borda";

    /// <summary>A chave do estilo do seletor sem título visível (o painel já tem o título na faixa de cima).</summary>
    internal const string ChaveSeletorSemTitulo = "Buzzy.SeletorSemTitulo";

    /// <summary>Se o tema está em uso agora (fora do alto contraste).</summary>
    internal static bool Ativo => !SystemParameters.HighContrast;

    /// <summary>
    /// Aplica à janela o dicionário certo (o tema ou as cores do sistema) e o troca quando o alto contraste muda, enquanto a
    /// janela existir.
    /// </summary>
    internal static void Aplicar(Window janela)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ResourceDictionary atual = Dicionario(Ativo);
        janela.Resources.MergedDictionaries.Add(atual);
        void AoMudar(object? _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SystemParameters.HighContrast)) return;
            janela.Resources.MergedDictionaries.Remove(atual);
            atual = Dicionario(Ativo);
            janela.Resources.MergedDictionaries.Add(atual);
        }
        SystemParameters.StaticPropertyChanged += AoMudar;
        janela.Closed += (_, _) => SystemParameters.StaticPropertyChanged -= AoMudar;
        janela.SetResourceReference(Control.BackgroundProperty, ChaveFundo);
        janela.SetResourceReference(Control.ForegroundProperty, ChaveTexto);
    }

    /// <summary>
    /// A faixa de cima: azul-marinho, com o retrato do Buzzy e o título, e embaixo a aba de palha e a faixa vermelha do chapéu.
    /// É um enfeite, fora da árvore do leitor de tela: o título já é o da janela, e o retrato não diz nada.
    /// </summary>
    internal static FrameworkElement Cabecalho(string titulo, double tamanhoDoTitulo)
    {
        var retrato = new Image { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom, SnapsToDevicePixels = true };
        RenderOptions.SetBitmapScalingMode(retrato, BitmapScalingMode.NearestNeighbor);
        DesenharRetrato(retrato);
        retrato.Loaded += (_, _) => DesenharRetrato(retrato);
        retrato.DpiChanged += (_, _) => DesenharRetrato(retrato);
        var texto = new TextBlock
        {
            Text = titulo,
            FontSize = tamanhoDoTitulo,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        texto.SetResourceReference(TextBlock.ForegroundProperty, ChaveTextoDoCabecalho);
        var linha = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(retrato, Dock.Left);
        linha.Children.Add(retrato);
        linha.Children.Add(texto);
        var faixaAzul = new Border { Padding = new Thickness(12, 8, 12, 4), Child = linha };
        faixaAzul.SetResourceReference(Border.BackgroundProperty, ChaveCabecalho);
        var aba = new Rectangle { Height = 5 };
        aba.SetResourceReference(Shape.FillProperty, ChaveAba);
        var faixa = new Rectangle { Height = 3 };
        faixa.SetResourceReference(Shape.FillProperty, ChaveFaixa);
        var pilha = new StackPanel();
        pilha.Children.Add(faixaAzul);
        pilha.Children.Add(aba);
        pilha.Children.Add(faixa);
        return new Decoracao { Child = pilha };
    }

    /// <summary>Um enfeite: fora das árvores de controle e de conteúdo do leitor de tela, sem filhos (o peer vazio).</summary>
    private sealed class Decoracao : Border
    {
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PeerVazio(this);
    }

    /// <summary>
    /// O retrato em múltiplos inteiros da arte de 64 px no DPI da janela (nítido, sem pixel desigual): 64 px a 100% e 125%,
    /// 128 px a 150% e 200%, e assim por diante.
    /// </summary>
    private static void DesenharRetrato(Image retrato)
    {
        double escala = VisualTreeHelper.GetDpi(retrato).DpiScaleX;
        int px = 64 * Math.Max(1, (int)Math.Round(escala, MidpointRounding.AwayFromZero));
        BitmapSource bmp = SpriteProvisorio.Renderizar(new QuadroDoSprite("parado", false, "feliz"), 96, new TamanhoDip(px, px));
        retrato.Source = bmp;
        retrato.Width = px / escala;
        retrato.Height = px / escala;
    }

    /// <summary>O dicionário do tema (fora do alto contraste) ou o das cores do sistema.</summary>
    internal static ResourceDictionary Dicionario(bool tema) => tema ? Tema() : Sistema();

    /// <summary>No alto contraste: só os pincéis, apontando para as cores do sistema, e nenhum estilo (os controles nativos).</summary>
    private static ResourceDictionary Sistema()
    {
        var d = new ResourceDictionary();
        d[ChaveFundo] = SystemColors.WindowBrush;
        d[ChaveTexto] = SystemColors.WindowTextBrush;
        d[ChaveTextoSuave] = SystemColors.GrayTextBrush;
        d[ChaveCabecalho] = SystemColors.WindowBrush;
        d[ChaveTextoDoCabecalho] = SystemColors.WindowTextBrush;
        d[ChaveAba] = SystemColors.WindowTextBrush;
        d[ChaveFaixa] = SystemColors.WindowBrush;
        d[ChaveBorda] = SystemColors.WindowFrameBrush;
        return d;
    }

    private static ResourceDictionary Tema()
    {
        var d = new ResourceDictionary();
        d[ChaveFundo] = Pincel(Fundo);
        d[ChaveTexto] = Pincel(Contorno);
        d[ChaveTextoSuave] = Pincel(TextoSuave);
        d[ChaveCabecalho] = Pincel(Pelo);
        d[ChaveTextoDoCabecalho] = Pincel(Fundo);
        d[ChaveAba] = Pincel(Palha);
        d[ChaveFaixa] = Pincel(Faixa);
        d[ChaveBorda] = Pincel(Contorno);

        Style foco = EstiloDoFoco();
        Style caixa = EstiloDaCaixa(foco);
        Style radio = EstiloDoRadio(foco);
        d[typeof(CaixaDeComando)] = caixa;
        d[typeof(CheckBox)] = caixa;
        d[typeof(RadioDeComando)] = radio;
        d[typeof(RadioButton)] = radio;
        d[typeof(Button)] = EstiloDoBotao(foco);
        d[typeof(GroupBox)] = EstiloDoGrupo();
        Style seletor = EstiloDoSeletor(mostrarTitulo: true);
        d[typeof(Seletor<NivelDeEnergia>)] = seletor;
        d[typeof(Seletor<EscalaDoPersonagem>)] = seletor;
        d[ChaveSeletorSemTitulo] = EstiloDoSeletor(mostrarTitulo: false);
        return d;
    }

    /// <summary>O foco do teclado: um contorno vermelho da faixa, por fora do controle (só pelo teclado, como no Windows).</summary>
    private static Style EstiloDoFoco()
    {
        var contorno = new FrameworkElementFactory(typeof(Rectangle));
        contorno.SetValue(Shape.StrokeProperty, Pincel(Faixa));
        contorno.SetValue(Shape.StrokeThicknessProperty, 2.0);
        contorno.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
        contorno.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var estilo = new Style(typeof(Control));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = contorno }));
        estilo.Seal();
        return estilo;
    }

    /// <summary>A caixa: quadrado de contorno grosso; marcada, cheia de palha com o visto em azul-marinho.</summary>
    private static Style EstiloDaCaixa(Style foco)
    {
        var marca = new FrameworkElementFactory(typeof(Polyline), "marca");
        marca.SetValue(Polyline.PointsProperty, new PointCollection([new Point(2.5, 7), new Point(5.5, 10), new Point(11.5, 3.5)]));
        marca.SetValue(Shape.StrokeProperty, Pincel(Contorno));
        marca.SetValue(Shape.StrokeThicknessProperty, 2.5);
        marca.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Square);
        marca.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Square);
        marca.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

        var quadro = new FrameworkElementFactory(typeof(Border), "quadro");
        quadro.SetValue(FrameworkElement.WidthProperty, 18.0);
        quadro.SetValue(FrameworkElement.HeightProperty, 18.0);
        quadro.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        quadro.SetValue(Border.BorderBrushProperty, Pincel(Contorno));
        quadro.SetValue(Border.BackgroundProperty, Pincel(Branco));
        quadro.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        quadro.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 0));
        quadro.AppendChild(marca);

        return EstiloComMarcador(typeof(ToggleButton), quadro, foco,
        [
            Gatilho(ToggleButton.IsCheckedProperty, true, ("quadro", Border.BackgroundProperty, Pincel(Palha)), ("marca", UIElement.VisibilityProperty, Visibility.Visible)),
            Gatilho(UIElement.IsMouseOverProperty, true, ("quadro", Border.BorderBrushProperty, Pincel(PeloClaro))),
        ]);
    }

    /// <summary>O botão de opção: um círculo "em pixel" (octógono); marcado, o miolo vermelho da faixa.</summary>
    private static Style EstiloDoRadio(Style foco)
    {
        var miolo = new FrameworkElementFactory(typeof(Path), "miolo");
        miolo.SetValue(Path.DataProperty, Geometry.Parse("M2,0 L6,0 L8,2 L8,6 L6,8 L2,8 L0,6 L0,2 Z"));
        miolo.SetValue(Shape.FillProperty, Pincel(Faixa));
        miolo.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        miolo.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        miolo.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

        var aro = new FrameworkElementFactory(typeof(Path), "aro");
        aro.SetValue(Path.DataProperty, Geometry.Parse("M6,1 L12,1 L17,6 L17,12 L12,17 L6,17 L1,12 L1,6 Z"));
        aro.SetValue(Shape.StrokeProperty, Pincel(Contorno));
        aro.SetValue(Shape.StrokeThicknessProperty, 2.0);
        aro.SetValue(Shape.FillProperty, Pincel(Branco));

        var marcador = new FrameworkElementFactory(typeof(Grid));
        marcador.SetValue(FrameworkElement.WidthProperty, 18.0);
        marcador.SetValue(FrameworkElement.HeightProperty, 18.0);
        marcador.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        marcador.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 0));
        marcador.AppendChild(aro);
        marcador.AppendChild(miolo);

        return EstiloComMarcador(typeof(ToggleButton), marcador, foco,
        [
            Gatilho(ToggleButton.IsCheckedProperty, true, ("aro", Shape.FillProperty, Pincel(CremeClaro)), ("miolo", UIElement.VisibilityProperty, Visibility.Visible)),
            Gatilho(UIElement.IsMouseOverProperty, true, ("aro", Shape.StrokeProperty, Pincel(PeloClaro))),
        ]);
    }

    /// <summary>O molde comum da caixa e do rádio: o marcador à esquerda e o rótulo (com a tecla de acesso) à direita.</summary>
    private static Style EstiloComMarcador(Type tipo, FrameworkElementFactory marcador, Style foco, IEnumerable<Trigger> gatilhos)
    {
        var rotulo = new FrameworkElementFactory(typeof(ContentPresenter));
        rotulo.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        rotulo.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        rotulo.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        rotulo.SetValue(Grid.ColumnProperty, 1);

        var raiz = new FrameworkElementFactory(typeof(Grid));
        raiz.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        raiz.AppendChild(c0);
        raiz.AppendChild(c1);
        raiz.AppendChild(marcador);
        raiz.AppendChild(rotulo);

        var modelo = new ControlTemplate(tipo) { VisualTree = raiz };
        foreach (Trigger g in gatilhos) modelo.Triggers.Add(g);
        modelo.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, 0.45) } });

        var estilo = new Style(tipo);
        estilo.Setters.Add(new Setter(Control.TemplateProperty, modelo));
        estilo.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, foco));
        estilo.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        estilo.Seal();
        return estilo;
    }

    /// <summary>O botão: azul-marinho do pelo com texto creme, contorno grosso, cantos retos.</summary>
    private static Style EstiloDoBotao(Style foco)
    {
        var rotulo = new FrameworkElementFactory(typeof(ContentPresenter));
        rotulo.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        rotulo.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        rotulo.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        rotulo.SetValue(TextElement.ForegroundProperty, Pincel(Fundo));
        rotulo.SetValue(TextElement.FontWeightProperty, FontWeights.SemiBold);

        var corpo = new FrameworkElementFactory(typeof(Border), "corpo");
        corpo.SetValue(Border.BackgroundProperty, Pincel(Pelo));
        corpo.SetValue(Border.BorderBrushProperty, Pincel(Contorno));
        corpo.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        corpo.SetValue(Border.PaddingProperty, new Thickness(16, 5, 16, 6));
        corpo.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        corpo.AppendChild(rotulo);

        var modelo = new ControlTemplate(typeof(Button)) { VisualTree = corpo };
        modelo.Triggers.Add(Gatilho(UIElement.IsMouseOverProperty, true, ("corpo", Border.BackgroundProperty, Pincel(PeloClaro))));
        modelo.Triggers.Add(Gatilho(ButtonBase.IsPressedProperty, true, ("corpo", Border.BackgroundProperty, Pincel(PeloEscuro))));
        modelo.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, 0.45) } });

        var estilo = new Style(typeof(Button));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, modelo));
        estilo.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, foco));
        estilo.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        estilo.Seal();
        return estilo;
    }

    /// <summary>O grupo de fora (Comportamento, Aparência, Windows): o título forte e, embaixo, um pedaço da faixa vermelha.</summary>
    private static Style EstiloDoGrupo()
    {
        var titulo = new FrameworkElementFactory(typeof(ContentPresenter));
        titulo.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        titulo.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        titulo.SetValue(TextElement.FontSizeProperty, 15.0);
        titulo.SetValue(TextElement.FontWeightProperty, FontWeights.Bold);
        titulo.SetValue(TextElement.ForegroundProperty, Pincel(Pelo));

        var faixa = new FrameworkElementFactory(typeof(Rectangle));
        faixa.SetValue(FrameworkElement.WidthProperty, 32.0);
        faixa.SetValue(FrameworkElement.HeightProperty, 3.0);
        faixa.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        faixa.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 4));
        faixa.SetValue(Shape.FillProperty, Pincel(Faixa));

        var conteudo = new FrameworkElementFactory(typeof(ContentPresenter));

        var pilha = new FrameworkElementFactory(typeof(StackPanel));
        pilha.AppendChild(titulo);
        pilha.AppendChild(faixa);
        pilha.AppendChild(conteudo);

        var estilo = new Style(typeof(GroupBox));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(GroupBox)) { VisualTree = pilha }));
        estilo.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 14)));
        estilo.Seal();
        return estilo;
    }

    /// <summary>O seletor (energia, tamanho): o título num tom só, sem faixa; no painel, sem título à vista (o nome continua).</summary>
    private static Style EstiloDoSeletor(bool mostrarTitulo)
    {
        var conteudo = new FrameworkElementFactory(typeof(ContentPresenter));
        var pilha = new FrameworkElementFactory(typeof(StackPanel));
        if (mostrarTitulo)
        {
            var titulo = new FrameworkElementFactory(typeof(ContentPresenter));
            titulo.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            titulo.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            titulo.SetValue(TextElement.FontWeightProperty, FontWeights.SemiBold);
            titulo.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 0));
            pilha.AppendChild(titulo);
        }
        pilha.AppendChild(conteudo);
        var estilo = new Style(typeof(GroupBox));
        estilo.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(GroupBox)) { VisualTree = pilha }));
        estilo.Seal();
        return estilo;
    }

    private static Trigger Gatilho(DependencyProperty propriedade, object valor, params (string Alvo, DependencyProperty Propriedade, object Valor)[] setters)
    {
        var gatilho = new Trigger { Property = propriedade, Value = valor };
        foreach ((string alvo, DependencyProperty p, object v) in setters) gatilho.Setters.Add(new Setter(p, v, alvo));
        return gatilho;
    }

    private static SolidColorBrush Pincel(Color cor)
    {
        var pincel = new SolidColorBrush(cor);
        pincel.Freeze();
        return pincel;
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
