using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P5 (DEC-038, itens 4, 6 e 7; critérios 10 e 11): os controles por intenção, o painel compacto de energia,
/// a ancoragem dele e "Energia…" no menu. Na thread STA dos testes, sem mostrar janelas: a UIA pelos peers, o teclado pelos
/// tratadores internos (os eventos da janela só os chamam).
/// </summary>
internal sealed class PainelDeEnergiaTestes
{
    // Os controles só pedem: o Toggle e o Select da UIA (o Narrador), como o clique, levantam o pedido e não mudam a marca; a
    // marca posta pelo código não pede nada.
    [Teste]
    public void Controles_SoPorIntencao_InclusivePelaUIA()
    {
        var caixa = new CaixaDeComando { Content = "_Teste" };
        var pedidos = new List<bool>();
        caixa.Pedido += pedidos.Add;
        var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(caixa).GetPattern(PatternInterface.Toggle);
        toggle.Toggle();
        Afirmar.Igual((false, 1, true), (caixa.IsChecked == true, pedidos.Count, pedidos[0]), "o Toggle da UIA pede marcar e não marca");
        caixa.Marcar(true);
        Afirmar.Igual((true, 1), (caixa.IsChecked == true, pedidos.Count), "a marca pelo código não pede");
        toggle.Toggle();
        Afirmar.Igual((true, false), (caixa.IsChecked == true, pedidos[^1]), "marcada, o Toggle pede desmarcar");
        Afirmar.Verdadeiro(caixa.Teclar(Key.Subtract) && pedidos[^1] == false && pedidos.Count == 3, "o - pede desmarcar");
        Afirmar.Verdadeiro(caixa.Teclar(Key.OemPlus) && pedidos.Count == 3, "o + com ela marcada não pede nada");
        Afirmar.Falso(caixa.Teclar(Key.A), "outra tecla não é tratada");

        var radio = new RadioDeComando { Content = "_Opção" };
        int pedidosDoRadio = 0;
        radio.Pedido += () => pedidosDoRadio++;
        var selecao = (ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(radio).GetPattern(PatternInterface.SelectionItem);
        selecao.Select();
        Afirmar.Igual((false, 1, false), (radio.IsChecked == true, pedidosDoRadio, selecao.IsSelected), "o Select da UIA pede e não marca");
        radio.Marcar(true);
        Afirmar.Igual((1, true), (pedidosDoRadio, selecao.IsSelected), "a marca pelo código não pede");
    }

    [Teste]
    public void Seletor_GrupoComNome_SetasEscolhemSemDarAVolta()
    {
        var seletor = new Seletor<NivelDeEnergia>("Energia",
            [(NivelDeEnergia.Baixa, "_Baixa", null), (NivelDeEnergia.Media, "_Média", null), (NivelDeEnergia.Alta, "_Alta", null)], "ajuda do grupo");
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(seletor);
        Afirmar.Igual((AutomationControlType.Group, "Energia"), (peer.GetAutomationControlType(), peer.GetName()), "um grupo com nome na UIA");
        Afirmar.Verdadeiro(seletor.Botoes.All(b => AutomationProperties.GetHelpText(b) == "ajuda do grupo"), "cada opção leva a ajuda");
        Afirmar.Sequencia(["Baixa", "Média", "Alta"], seletor.Botoes.Select(AutomationProperties.GetName), "os nomes sem a marca da tecla de acesso");

        var escolhas = new List<NivelDeEnergia>();
        seletor.Escolheu += escolhas.Add;
        seletor.Marcar(NivelDeEnergia.Media);
        Afirmar.Igual(0, escolhas.Count, "marcar não escolhe");
        Afirmar.Sequencia([seletor.Botoes[1]], seletor.ParadasDoTab, "uma parada do Tab por grupo, na marcada");
        seletor.Marcar(NivelDeEnergia.Alta);
        Afirmar.Sequencia([seletor.Botoes[2]], seletor.ParadasDoTab, "a parada acompanha a marca");
        seletor.Marcar(NivelDeEnergia.Media);
        Afirmar.Verdadeiro(seletor.Teclar(Key.Down, 1), "seta para baixo");
        Afirmar.Verdadeiro(seletor.Teclar(Key.Up, 0), "seta para cima na primeira: tratada, sem dar a volta");
        Afirmar.Verdadeiro(seletor.Teclar(Key.Right, 2), "seta à direita na última: tratada, sem dar a volta");
        Afirmar.Verdadeiro(seletor.Teclar(Key.Left, 1), "seta à esquerda");
        Afirmar.Sequencia([NivelDeEnergia.Alta, NivelDeEnergia.Baixa], escolhas, "as escolhas das setas");
        Afirmar.Falso(seletor.Teclar(Key.Tab, 1), "outra tecla não é tratada");
        Afirmar.Igual(NivelDeEnergia.Media, seletor.Botoes.Select((b, i) => (b, i)).Single(x => x.b.IsChecked == true).i switch { 0 => NivelDeEnergia.Baixa, 1 => NivelDeEnergia.Media, _ => NivelDeEnergia.Alta }, "a marca não mudou com as setas");
    }

    // Critério 11: só o seletor (três opções, sem botão, sem campo de texto); Esc e Enter fecham avisando uma vez; o núcleo
    // fecha sem avisar; a marca acompanha o núcleo.
    [Teste]
    public void Painel_SoOSeletor_FechaUmaVez()
    {
        var painel = new PainelDeEnergia(NivelDeEnergia.Alta);
        IEnumerable<DependencyObject> todos = Descendentes(painel);
        Afirmar.Igual(3, todos.OfType<RadioButton>().Count(), "três opções");
        Afirmar.Igual(0, todos.Count(o => o is TextBox or Button or ComboBox or (ToggleButton and not RadioButton)), "nenhum botão, campo de texto ou outra caixa");
        Afirmar.Igual("Energia do Buzzy", painel.Title, "o título lido pelo Narrador");
        Afirmar.Verdadeiro(painel.Topmost && !painel.ShowInTaskbar && painel.WindowStyle == WindowStyle.None, "no topo, fora da barra de tarefas, sem moldura");
        Afirmar.Igual(NivelDeEnergia.Alta, Marcado(painel), "abre no nível atual");
        painel.Marcar(NivelDeEnergia.Baixa);
        Afirmar.Igual(NivelDeEnergia.Baixa, Marcado(painel), "a marca acompanha o núcleo");

        var escolhas = new List<NivelDeEnergia>();
        painel.Escolheu += escolhas.Add;
        ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(painel.Seletor.Botoes[1]).GetPattern(PatternInterface.SelectionItem)).Select();
        Afirmar.Sequencia([NivelDeEnergia.Media], escolhas, "escolher pela UIA");

        var fechados = new List<string>();
        painel.FechadoPeloUsuario += fechados.Add;
        Afirmar.Verdadeiro(painel.Teclar(Key.Escape), "Esc tratado");
        painel.FecharPeloUsuario("foco");
        Afirmar.Sequencia(["esc"], fechados, "avisa uma vez só");
        Afirmar.Falso(painel.Teclar(Key.Space), "o Espaço é do rádio");

        var outro = new PainelDeEnergia(NivelDeEnergia.Media);
        var doOutro = new List<string>();
        outro.FechadoPeloUsuario += doOutro.Add;
        outro.FecharPeloNucleo();
        outro.FecharPeloUsuario("enter");
        Afirmar.Igual(0, doOutro.Count, "fechado pelo núcleo, não avisa");
    }

    private static NivelDeEnergia Marcado(PainelDeEnergia painel)
        => (NivelDeEnergia)painel.Seletor.Botoes.Select((b, i) => (b, i)).Single(x => x.b.IsChecked == true).i;

    private static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
    {
        foreach (object filho in LogicalTreeHelper.GetChildren(raiz))
        {
            if (filho is not DependencyObject d) continue;
            yield return d;
            foreach (DependencyObject neto in Descendentes(d)) yield return neto;
        }
    }

    // A ancoragem: do lado com mais espaço, centrado no sprite, inteiro na área útil, nunca sobre o sprite quando cabe.
    [Teste]
    public void Ancoragem_PorTabela()
    {
        var area = new RetanguloPx(0, 0, 1920, 1032);
        var painel = new TamanhoPx(200, 120);
        (string Caso, RetanguloPx Personagem, LadoDoEsconderijo Lado, RetanguloPx Esperado)[] casos =
        [
            ("no meio, mais espaço à direita", new RetanguloPx(800, 904, 928, 1032), LadoDoEsconderijo.Nenhum, new RetanguloPx(936, 908, 1136, 1028)),
            ("à direita, vai à esquerda", new RetanguloPx(1700, 904, 1828, 1032), LadoDoEsconderijo.Nenhum, new RetanguloPx(1492, 908, 1692, 1028)),
            ("encostado embaixo: preso na área", new RetanguloPx(100, 960, 228, 1088), LadoDoEsconderijo.Nenhum, new RetanguloPx(236, 912, 436, 1032)),
            ("escondido embaixo: acima", new RetanguloPx(800, 980, 928, 1108), LadoDoEsconderijo.Baixo, new RetanguloPx(764, 852, 964, 972)),
            ("escondido em cima: abaixo", new RetanguloPx(800, -60, 928, 68), LadoDoEsconderijo.Cima, new RetanguloPx(764, 76, 964, 196)),
        ];
        foreach ((string caso, RetanguloPx personagem, LadoDoEsconderijo lado, RetanguloPx esperado) in casos)
            Afirmar.Igual(esperado, AncoragemDoPainel.Calcular(personagem, painel, area, 8, lado), caso);

        // Em x negativo, com DPI 150% e tamanhos variados: sempre inteiro na área útil, e fora do sprite quando cabe.
        var esquerda = new RetanguloPx(-1920, 0, 0, 1032);
        var rnd = new Random(8);
        for (int i = 0; i < 2000; i++)
        {
            int x = rnd.Next(-1920, -192), y = rnd.Next(0, 1032 - 192);
            var personagem = new RetanguloPx(x, y, x + 192, y + 192);
            var tamanho = new TamanhoPx(rnd.Next(100, 400), rnd.Next(80, 300));
            RetanguloPx r = AncoragemDoPainel.Calcular(personagem, tamanho, esquerda, 12, (LadoDoEsconderijo)rnd.Next(0, 3));
            Afirmar.Verdadeiro(esquerda.Contem(r) && r.Tamanho == tamanho, $"{personagem} {tamanho}: {r} inteiro na área");
            Afirmar.Falso(r.Intersecta(personagem), $"{personagem} {tamanho}: {r} fora do sprite");
        }
        // Maior que a área: no canto de cima e da esquerda.
        Afirmar.Igual(new RetanguloPx(0, 0, 2000, 120), AncoragemDoPainel.Calcular(new RetanguloPx(800, 904, 928, 1032), new TamanhoPx(2000, 120), area, 8), "maior que a área");
    }

    // "Energia…" no menu: só com o painel disponível, desabilitado com o Buzzy escondido, tecla de acesso única; o id volta como
    // o comando.
    [Teste]
    public void Menu_EnergiaEConfiguracoes()
    {
        var modelo = new ModeloDoMenu(true, false, null, AltoContraste: true, Tamagotchi: true, PainelDeEnergia: true, Configuracoes: true);
        IReadOnlyList<EntradaDoMenu> entradas = MenuNativo.Entradas(modelo);
        EntradaDoMenu energia = entradas.Single(e => e.Id == (int)ComandoDoMenu.Energia);
        EntradaDoMenu configuracoes = entradas.Single(e => e.Id == (int)ComandoDoMenu.Configuracoes);
        Afirmar.Igual(("E&nergia…", false), (energia.Rotulo, energia.Desabilitada), "Energia…");
        Afirmar.Igual("&Configurações…", configuracoes.Rotulo, "Configurações…");
        Afirmar.Verdadeiro(MenuNativo.Entradas(modelo with { BuzzyVisivel = false }).Single(e => e.Id == (int)ComandoDoMenu.Energia).Desabilitada, "escondido, desabilitado");
        Afirmar.Falso(MenuNativo.Entradas(modelo with { PainelDeEnergia = false, Configuracoes = false }).Any(e => e.Id is (int)ComandoDoMenu.Energia or (int)ComandoDoMenu.Configuracoes), "sem as capacidades, nada");
        Afirmar.Igual((ComandoDoMenu.Energia, ComandoDoMenu.Configuracoes), (MenuNativo.Escolha(9).Comando, MenuNativo.Escolha(10).Comando), "os ids");
        char[] teclas = [.. entradas.Where(e => e.Rotulo.Contains('&')).Select(e => char.ToLowerInvariant(e.Rotulo[e.Rotulo.IndexOf('&') + 1]))];
        Afirmar.Igual(teclas.Length, teclas.Distinct().Count(), $"teclas de acesso únicas no menu principal: {new string(teclas)}");
    }
}
