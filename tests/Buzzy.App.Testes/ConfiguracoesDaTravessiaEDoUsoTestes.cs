using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Buzzy.App.Apresentacao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// As duas opções novas das Configurações: "Atravessar entre monitores" (DEC-046, desligada por padrão) e, ao lado de cada
/// droga ilícita da edição, "por conta própria" (DEC-045). Como as outras caixas, cada uma só pede o seu comando e nunca
/// marca sozinha; a marca vem de <see cref="JanelaDeConfiguracoes.Atualizar"/>. Na edição pública não há caixa de uso por
/// conta própria.
/// </summary>
internal sealed class ConfiguracoesDaTravessiaEDoUsoTestes
{
    private static readonly Item[] Seis = [Item.Baseado, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala];

    private static void Toggle(UIElement e) => ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(e).GetPattern(PatternInterface.Toggle)).Toggle();

    [Teste]
    public void Travessia_PedeOComando_SemMarcarSozinha()
    {
        var janela = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        var pedidos = new List<bool>();
        janela.PediuTravessia += pedidos.Add;
        Afirmar.Falso(janela.Travessia.IsChecked == true, "desmarcada, como o padrão");
        Toggle(janela.Travessia);
        Afirmar.Sequencia([true], pedidos, "pediu para ligar");
        Afirmar.Falso(janela.Travessia.IsChecked == true, "não marcou sozinha");
        janela.Atualizar(Preferencias.Padrao with { AtravessarMonitores = true });
        Afirmar.Verdadeiro(janela.Travessia.IsChecked == true, "marcada pela raiz");
        Afirmar.Igual("Atravessar entre monitores", AutomationProperties.GetName(janela.Travessia), "o nome para o leitor de tela");
        Afirmar.Falso(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(janela.Travessia)), "com ajuda");
    }

    [Teste]
    public void PorContaPropria_UmaCaixaPorIlicita_QuePedeOComando()
    {
        var janela = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        Afirmar.Sequencia(Seis, janela.CaixasPorContaPropria.Keys, "as seis, na ordem do menu");
        var pedidos = new List<(Item, bool)>();
        janela.PediuPorContaPropria += (item, ligado) => pedidos.Add((item, ligado));
        Toggle(janela.CaixasPorContaPropria[Item.Md]);
        Afirmar.Sequencia([(Item.Md, true)], pedidos, "pediu o MD por conta própria");
        Afirmar.Falso(janela.CaixasPorContaPropria[Item.Md].IsChecked == true, "não marcou sozinha");
        janela.Atualizar(Preferencias.Padrao with { ItensPorContaPropria = ConjuntoDeItens.Vazio.Com(Item.Md).Com(Item.Bala) });
        Afirmar.Sequencia([Item.Md, Item.Bala], Seis.Where(i => janela.CaixasPorContaPropria[i].IsChecked == true), "marcadas pela raiz");
        Afirmar.Igual("MD por conta própria", AutomationProperties.GetName(janela.CaixasPorContaPropria[Item.Md]), "o nome para o leitor de tela");
        foreach (Item item in Seis)
            Afirmar.Falso(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(janela.CaixasPorContaPropria[item])), $"{item}: com ajuda");
    }

    [Teste]
    public void Publica_SemCaixasDeUsoPorContaPropria()
    {
        var publica = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true,
            itensDaEdicao: TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica));
        Afirmar.Igual(0, publica.CaixasPorContaPropria.Count, "nenhuma na edição pública");
        Afirmar.NaoNulo(publica.Travessia, "a travessia existe nas duas edições");
    }

    // A raiz liga os pedidos aos comandos do núcleo.
    [Teste]
    public void Fonte_ARaizEnviaOsComandos()
    {
        string raiz = File.ReadAllText(Path.Combine(FonteDoProduto.Src, "Buzzy.App", "Composicao", "Aplicacao.Configuracoes.cs"));
        Afirmar.Verdadeiro(raiz.Contains("new CmdSetCrossMonitors(ligado)", StringComparison.Ordinal), "a travessia");
        Afirmar.Verdadeiro(raiz.Contains("new CmdSetSelfUseItem(item, ligado)", StringComparison.Ordinal), "o uso por conta própria");
    }
}
