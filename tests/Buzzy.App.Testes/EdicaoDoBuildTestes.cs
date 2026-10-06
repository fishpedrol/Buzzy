using System.IO;
using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A edição do build (DEC-044, item 2): os testes rodam na completa, a de sempre; a pública sai só com
/// <c>-p:BuzzyEdicao=publica</c> (o empacotador confere a edição pelo log do <c>.exe</c>). A constante de compilação mora num
/// arquivo só, a raiz cria o núcleo pela edição do build, e a janela de configurações lista só os itens adultos da edição.
/// </summary>
internal sealed class EdicaoDoBuildTestes
{
    [Teste]
    public void OsTestes_RodamNaCompleta()
        => Afirmar.Igual(EdicaoDoBuzzy.Completa, EdicaoDoBuild.Atual, "sem -p:BuzzyEdicao=publica, a completa");

    [Teste]
    public void JanelaDaPublica_SoOsItensAdultosDaEdicao()
    {
        var publica = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true,
            itensDaEdicao: TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica));
        Afirmar.Sequencia([Item.Vodka, Item.Cerveja, Item.Cigarro], publica.CaixasDosItensAdultos.Keys, "pública: só as três caixas, na ordem do menu");

        var completa = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        Afirmar.Igual(9, completa.CaixasDosItensAdultos.Count, "completa: as nove");
    }

    // A constante só num arquivo; a raiz e a janela usam a edição do núcleo; a partida diz a edição no log.
    [Teste]
    public void Fonte_AEdicaoNumLugarSo()
    {
        string[] comConstante = [.. FonteDoProduto.Arquivos().Where(f => File.ReadAllText(f).Contains("BUZZY_PUBLICO", StringComparison.Ordinal)).Select(FonteDoProduto.Relativo)];
        Afirmar.Sequencia([Path.Combine("Buzzy.App", "Composicao", "EdicaoDoBuild.cs")], comConstante, "a constante num arquivo só");

        string aplicacao = File.ReadAllText(Path.Combine(FonteDoProduto.Src, "Buzzy.App", "Composicao", "Aplicacao.cs"));
        Afirmar.Verdadeiro(aplicacao.Contains("ConfiguracaoDoNucleo.DoAplicativo(_escalaEmVigor, EdicaoDoBuild.Atual)", StringComparison.Ordinal), "o núcleo criado pela edição do build");
        string configuracoes = File.ReadAllText(Path.Combine(FonteDoProduto.Src, "Buzzy.App", "Composicao", "Aplicacao.Configuracoes.cs"));
        Afirmar.Verdadeiro(configuracoes.Contains("_nucleo.Configuracao.ItensDaEdicao", StringComparison.Ordinal), "a janela recebe os itens da edição");
        string programa = File.ReadAllText(Path.Combine(FonteDoProduto.Src, "Buzzy.App", "Programa.cs"));
        Afirmar.Verdadeiro(programa.Contains("(\"edicao\", EdicaoDoBuild.NomeNoLog)", StringComparison.Ordinal), "a partida diz a edição no log");
    }

    [Teste]
    public void NomeNoLog_DaEdicao()
        => Afirmar.Igual("completa", EdicaoDoBuild.NomeNoLog, "o nome que o empacotador confere");
}
