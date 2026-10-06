using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Tamagotchi.ApoioDosItens;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// A edição pública do download (DEC-044, item 2): só vodka, cerveja e cigarro como itens adultos; as seis drogas ilícitas
/// (baseado, cocaína, MD, lança-perfume, cogumelo e bala) existem só na edição completa. O núcleo recusa invocar ou marcar
/// um item fora da edição e tira-o das preferências carregadas; o baseado por conta própria some com o baseado. O esperado
/// vem da decisão, escrito aqui à parte do núcleo.
/// </summary>
internal static class EdicaoPublicaTestes
{
    private static readonly Item[] Ilicitas = [Item.Baseado, Item.Cocaina, Item.Md, Item.LancaPerfume, Item.Cogumelo, Item.Bala];

    private static ConjuntoDeItens Conjunto(params Item[] itens) => itens.Aggregate(ConjuntoDeItens.Vazio, (c, i) => c.Com(i));

    private static ConfiguracaoDoNucleo Publica() => SemFisica() with { ItensDaEdicao = TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica) };

    [Teste]
    public static void ItensDaEdicao_PublicaSemAsSeisIlicitas_ECompletaComOsTreze()
    {
        Afirmar.Igual(Conjunto([.. TabelaDoTamagotchi.Itens]), TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa), "a completa com os treze");
        Afirmar.Igual(Conjunto(Item.Banana, Item.Agua, Item.Vodka, Item.Cerveja, Item.Cigarro, Item.Cafe, Item.Energetico),
            TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica), "a pública: os quatro de alívio, vodka, cerveja e cigarro");
        Afirmar.Igual(TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa), new ConfiguracaoDoNucleo().ItensDaEdicao, "o padrão da configuração é a completa");
    }

    // O aplicativo: a completa é a de sempre (com o baseado por conta própria); a pública tira os itens e a ação.
    [Teste]
    public static void DoAplicativo_PublicaSemAsIlicitasNemOBaseadoPorContaPropria()
    {
        ConfiguracaoDoNucleo completa = ConfiguracaoDoNucleo.DoAplicativo(EscalaDoPersonagem.Media);
        Afirmar.Igual(TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa), completa.ItensDaEdicao, "completa: os treze");
        Afirmar.Verdadeiro(completa.Acoes.HasFlag(AcoesAutonomas.UsarPorContaPropria), "completa: o baseado por conta própria continua");

        ConfiguracaoDoNucleo publica = ConfiguracaoDoNucleo.DoAplicativo(EscalaDoPersonagem.Media, EdicaoDoBuzzy.Publica);
        Afirmar.Igual(TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Publica), publica.ItensDaEdicao, "pública: sem as seis");
        Afirmar.Falso(publica.Acoes.HasFlag(AcoesAutonomas.UsarPorContaPropria), "pública: sem o baseado por conta própria");
        Afirmar.Igual(completa.Acoes & ~AcoesAutonomas.UsarPorContaPropria, publica.Acoes, "o resto das ações igual");
        Afirmar.Igual(completa.Tamanho, publica.Tamanho, "o mesmo tamanho");
    }

    // Preferências salvas pela completa (as nove marcadas) chegam à pública sem as seis; a chave geral e o resto ficam.
    [Teste]
    public static void Carga_TiraAsIlicitasDasPreferencias()
    {
        Cenario c = Cenario.Parado(Publica());
        Afirmar.Igual(Conjunto(Item.Vodka, Item.Cerveja, Item.Cigarro), c.Atual.Preferencias.ItensAdultosHabilitados, "só as três da edição");
        Afirmar.Verdadeiro(c.Atual.Preferencias.ConteudoAdulto, "a chave geral preservada");
    }

    // Invocar ou marcar uma ilícita não faz nada; os itens da edição nascem; desmarcar um deles continua valendo.
    [Teste]
    public static void Ilicitas_NaoNascemNemSaoMarcadas()
    {
        Cenario c = Cenario.Parado(Publica());
        EstadoDoNucleo antes = c.Atual;
        foreach (Item item in Ilicitas)
        {
            c.Aplicar(new CmdSummonItem(item));
            Afirmar.Igual((antes, 0), (c.Atual, c.Efeitos.Count), $"{item}: não nasce");
            c.Aplicar(new CmdSetAdultItemEnabled(item, true));
            Afirmar.Igual((antes, 0), (c.Atual, c.Efeitos.Count), $"{item}: não é marcado");
        }

        c.Aplicar(new CmdSummonItem(Item.Vodka));
        c.Aplicar(new CmdSummonItem(Item.Banana));
        Afirmar.Igual(2, c.Atual.Itens.Quantidade, "a vodka e a banana nascem");
        c.Aplicar(new CmdSetAdultItemEnabled(Item.Cigarro, false));
        Afirmar.Falso(c.Atual.Preferencias.ItensAdultosHabilitados.Contem(Item.Cigarro), "desmarcar um da edição vale");
    }

    // Preferências trocadas por inteiro (o caminho das configurações) também perdem as ilícitas.
    [Teste]
    public static void PreferenciasNovas_TambemSemAsIlicitas()
    {
        Cenario c = Cenario.Parado(Publica());
        c.Aplicar(new SettingsChanged(c.Atual.Preferencias with { ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos }));
        Afirmar.Igual(Conjunto(Item.Vodka, Item.Cerveja, Item.Cigarro), c.Atual.Preferencias.ItensAdultosHabilitados, "as seis ficam de fora");
    }
}
