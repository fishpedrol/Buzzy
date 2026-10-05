using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P6 (DEC-038, itens 2, 5 a 7; critérios 7 a 10): a janela de configurações, na thread STA dos testes, sem
/// mostrá-la. Cada controle pede o seu campo, pelo clique ou pela UIA, e nunca marca sozinho; a marca vem só de
/// <see cref="JanelaDeConfiguracoes.Atualizar"/>; o aviso do tamanho aparece quando o escolhido difere do que está em vigor;
/// cada controle tem nome e ajuda; as teclas de acesso são únicas; não há campo de texto nem opacidade; e o app nunca
/// constrói SETTINGS_CHANGED.
/// </summary>
internal sealed class JanelaDeConfiguracoesTestes
{
    private static readonly Preferencias Iniciais = Preferencias.Padrao with { Energia = NivelDeEnergia.Baixa, ConteudoAdulto = false };

    private static void Toggle(UIElement e) => ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(e).GetPattern(PatternInterface.Toggle)).Toggle();

    private static void Select(UIElement e) => ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(e).GetPattern(PatternInterface.SelectionItem)).Select();

    [Teste]
    public void CadaControle_PedeOSeuCampo_SemMarcarSozinho()
    {
        var janela = new JanelaDeConfiguracoes(Iniciais, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        var pedidos = new List<string>();
        janela.PediuEnergia += n => pedidos.Add($"energia={n}");
        janela.PediuTelaCheia += l => pedidos.Add($"telaCheia={l}");
        janela.PediuAdulto += l => pedidos.Add($"adulto={l}");
        janela.PediuEscala += e => pedidos.Add($"escala={e}");
        janela.PediuTopo += l => pedidos.Add($"topo={l}");

        Select(janela.Energia.Botoes[2]);
        Toggle(janela.TelaCheia);
        Toggle(Afirmar.NaoNulo(janela.Adulto));
        Select(janela.Tamanho.Botoes[0]);
        Toggle(janela.Topo);
        Afirmar.Sequencia(["energia=Alta", "telaCheia=False", "adulto=True", "escala=Pequena", "topo=False"], pedidos, "cada controle, o seu pedido");
        Afirmar.Igual((true, true, false, true), (janela.Energia.Botoes[0].IsChecked == true, janela.TelaCheia.IsChecked == true, janela.Adulto!.IsChecked == true, janela.Topo.IsChecked == true),
            "nenhuma marca mudou sozinha");
        Afirmar.Falso(janela.AvisoDaProximaVez, "o tamanho em vigor: sem aviso");

        // A raiz atualiza pelo que o núcleo gravou: marca sem pedir, e o aviso aparece com outro tamanho.
        janela.Atualizar(Iniciais with { Energia = NivelDeEnergia.Alta, Escala = EscalaDoPersonagem.Grande, SempreNoTopo = false });
        Afirmar.Igual(5, pedidos.Count, "atualizar não pede nada");
        Afirmar.Igual((true, true, false), (janela.Energia.Botoes[2].IsChecked == true, janela.Tamanho.Botoes[2].IsChecked == true, janela.Topo.IsChecked == true), "as marcas novas");
        Afirmar.Verdadeiro(janela.AvisoDaProximaVez, "outro tamanho: o aviso de próxima vez");
        janela.Atualizar(Iniciais);
        Afirmar.Falso(janela.AvisoDaProximaVez, "de volta ao em vigor: sem aviso");

        Afirmar.Nulo(new JanelaDeConfiguracoes(Iniciais, EscalaDoPersonagem.Media, comConteudoAdulto: false).Adulto, "sem o tamagotchi, sem a caixa do conteúdo adulto");
    }

    // Critério 7 (a parte automática): nome e ajuda em cada controle, grupos com nome, teclas de acesso únicas, Esc fecha, sem
    // campo de texto; janela comum, nunca topmost, na barra de tarefas, com largura máxima.
    [Teste]
    public void Acessivel_PorTecladoELeitorDeTela()
    {
        var janela = new JanelaDeConfiguracoes(Iniciais, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        List<DependencyObject> todos = [.. Descendentes(janela)];
        Afirmar.Igual(0, todos.Count(o => o is TextBox or PasswordBox or ComboBox), "nenhum campo de texto");
        Afirmar.Igual((false, true, ResizeMode.NoResize), (janela.Topmost, janela.ShowInTaskbar, janela.ResizeMode), "janela comum, nunca no topo, na barra de tarefas");
        Afirmar.Verdadeiro(janela.Fechar.IsCancel, "Esc fecha pelo botão Fechar");
        Afirmar.Igual(1, todos.OfType<StackPanel>().Count(p => p.MaxWidth == JanelaDeConfiguracoes.LarguraMaxima), "largura máxima na coluna");

        List<Control> controles = [.. todos.OfType<Control>().Where(c => c is ButtonBase or RadioButton)];
        Afirmar.Igual(11, controles.Count, "3 de energia, 2 caixas, 3 de tamanho, o topo, o início e o Fechar");
        foreach (Control c in controles)
            Afirmar.Verdadeiro(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)), $"{c.GetType().Name} {c}: com nome");
        foreach (CaixaDeComando caixa in controles.OfType<CaixaDeComando>())
            Afirmar.Verdadeiro(!string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(caixa)), $"{AutomationProperties.GetName(caixa)}: com ajuda");
        foreach (GroupBox grupo in todos.OfType<GroupBox>())
            Afirmar.Igual(AutomationControlType.Group, UIElementAutomationPeer.CreatePeerForElement(grupo).GetAutomationControlType(), $"{grupo.Header}: grupo na UIA");
        Afirmar.Igual(5, todos.OfType<GroupBox>().Count(), "Comportamento, Energia, Aparência, Tamanho e Windows");

        char[] teclas = [.. controles.Select(c => c.GetValue(ContentControl.ContentProperty) as string).OfType<string>()
            .Where(t => t.Contains('_')).Select(t => char.ToLowerInvariant(t[t.IndexOf('_') + 1]))];
        Afirmar.Igual(controles.Count, teclas.Length, "toda opção tem tecla de acesso");
        Afirmar.Igual(teclas.Length, teclas.Distinct().Count(), $"teclas de acesso únicas: {new string(teclas)}");
    }

    // O lugar: centrada na área útil; com o sprite no centro, na metade oposta; sempre presa na área.
    [Teste]
    public void Lugar_LongeDoSprite_PresoNaArea()
    {
        var area = new RetanguloPx(0, 0, 1920, 1032);
        var janela = new TamanhoPx(440, 600);
        Afirmar.Igual(new RetanguloPx(740, 216, 1180, 816), LugarDasConfiguracoes.Calcular(area, null, janela), "escondido: no centro");
        Afirmar.Igual(new RetanguloPx(740, 216, 1180, 816), LugarDasConfiguracoes.Calcular(area, new RetanguloPx(1600, 904, 1728, 1032), janela), "sprite longe: no centro");
        Afirmar.Igual(new RetanguloPx(1220, 216, 1660, 816), LugarDasConfiguracoes.Calcular(area, new RetanguloPx(800, 500, 928, 628), janela), "sprite no centro, à esquerda: na metade direita");
        Afirmar.Igual(new RetanguloPx(260, 216, 700, 816), LugarDasConfiguracoes.Calcular(area, new RetanguloPx(1000, 500, 1128, 628), janela), "sprite no centro, à direita: na metade esquerda");
        Afirmar.Igual(new RetanguloPx(0, 0, 2000, 1200), LugarDasConfiguracoes.Calcular(area, null, new TamanhoPx(2000, 1200)), "maior que a área: no canto");
        var negativa = new RetanguloPx(-1920, 0, 0, 1032);
        Afirmar.Verdadeiro(negativa.Contem(LugarDasConfiguracoes.Calcular(negativa, new RetanguloPx(-1000, 400, -872, 528), janela)), "em x negativo, dentro da área");
    }

    // DEC-038, item 2 e Q-03: o app nunca constrói SETTINGS_CHANGED (só o teste e as reproduções), e nenhuma opacidade.
    [Teste]
    public void Fonte_SemSettingsChanged_ESemOpacidade()
    {
        string[] fontes = [.. Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App"), "*.cs", SearchOption.AllDirectories).Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"))];
        Afirmar.Sequencia([], fontes.Where(f => File.ReadAllText(f).Contains("new SettingsChanged", StringComparison.Ordinal)).Select(Path.GetFileName), "quem constrói SETTINGS_CHANGED no app");
        Afirmar.Sequencia([], fontes.Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bOpacity\b|opacidade", RegexOptions.IgnoreCase)).Select(Path.GetFileName), "opacidade no app");
        Afirmar.Falso(File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Textos.resx")).Contains("opacidade", StringComparison.OrdinalIgnoreCase), "opacidade nos textos");
    }

    private static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
    {
        foreach (object filho in LogicalTreeHelper.GetChildren(raiz))
        {
            if (filho is not DependencyObject d) continue;
            yield return d;
            foreach (DependencyObject neto in Descendentes(d)) yield return neto;
        }
    }
}
