using Buzzy.App.Apresentacao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// O contrato entre o núcleo e a arte do tamagotchi (DEC-028; crítica, F6 e passo T7). O Buzzy.Visual não referencia o
/// núcleo: os dois lados se ligam pelo nome em minúsculas. Um valor novo num enum do núcleo sem a arte correspondente
/// faria a apresentação pedir uma chave que não existe, e o app cairia (crítica, F5); estes testes pegam isso antes.
/// </summary>
internal sealed class NucleoEArteTestes
{
    [Teste]
    public void ItemCobreOsItensDaArteNaMesmaOrdem()
    {
        Afirmar.Sequencia(Enum.GetValues<Item>().Select(i => i.ToString().ToLowerInvariant()), ItensPixel.Todos, "Item contra ItensPixel.Todos");
        Afirmar.Sequencia(ItensPixel.Todos, Enum.GetValues<Item>().Select(PoseDoPersonagem.NomeDoItem), "a chave que a apresentação pede");
        Afirmar.Sequencia(Enum.GetValues<Item>(), TabelaDoTamagotchi.Itens, "a ordem do menu é a do enum");
        // A chave é o nome inteiro em minúsculas (crítica, C7): "lancaperfume", não "lanca".
        Afirmar.Igual("lancaperfume", PoseDoPersonagem.NomeDoItem(Item.LancaPerfume));
        Afirmar.Igual("md", PoseDoPersonagem.NomeDoItem(Item.Md));
    }

    [Teste]
    public void VerboDeUsoCobreOsVerbosDaArte()
    {
        Afirmar.Sequencia(Enum.GetValues<Verbo>().Select(v => v.ToString()), Enum.GetValues<VerboDeUso>().Select(v => v.ToString()), "os mesmos nomes, na mesma ordem");
        foreach (VerboDeUso verbo in Enum.GetValues<VerboDeUso>())
            Afirmar.Igual(verbo.ToString(), PoseDoPersonagem.VerboDaArte(verbo).ToString(), $"{verbo}: o verbo da arte");
        foreach (Item item in Enum.GetValues<Item>())
            Afirmar.Igual(TabelaDoTamagotchi.DoItem(item).Verbo.ToString(), ItensPixel.VerboDe(PoseDoPersonagem.NomeDoItem(item)).ToString(), $"{item}: o mesmo verbo nos dois lados");
    }

    [Teste]
    public void PassosDoUsoSaoADuracaoDaAnimacaoDoVerbo()
    {
        // A duração do uso no núcleo é a soma dos quadros da animação do verbo na arte (crítica, C9).
        foreach (VerboDeUso verbo in Enum.GetValues<VerboDeUso>())
            Afirmar.Igual(UsosPixel.Passos(PoseDoPersonagem.VerboDaArte(verbo)), TabelaDoTamagotchi.PassosDoUso(verbo), $"{verbo}");
        foreach (Item item in Enum.GetValues<Item>())
        {
            Verbo daArte = ItensPixel.VerboDe(PoseDoPersonagem.NomeDoItem(item));
            Afirmar.Igual(UsosPixel.Sequencia(daArte).Sum(q => q.Passos), TabelaDoTamagotchi.DoItem(item).PassosDoUso, $"{item}: a soma dos quadros de {daArte}");
        }
    }

    [Teste]
    public void ExpressaoCobreOsRostosDeHumorEDeEfeito()
    {
        Afirmar.Sequencia(Rostos.DeHumor, Expressoes.DeHumor.Select(PoseDoPersonagem.NomeDaExpressao), "as 14 de humor, na ordem de expressoes.png");
        Afirmar.Sequencia(Rostos.DeEfeito, Expressoes.DeEfeito.Select(PoseDoPersonagem.NomeDaExpressao), "as 8 de efeito, na ordem do fim do enum (a paranoico por último)");
        Afirmar.Sequencia(Enum.GetValues<Expressao>(), Expressoes.DeHumor.Concat(Expressoes.DeEfeito), "o enum inteiro é humor e efeito, nessa ordem");
        foreach (Expressao expressao in Enum.GetValues<Expressao>())
            Afirmar.Verdadeiro(Rostos.Expressoes.ContainsKey(PoseDoPersonagem.NomeDaExpressao(expressao)), $"{expressao}: rosto na arte");
        // A cara durante o uso, que aparece nos apoios sem pose de uso (crítica, C25), é de humor.
        foreach (Item item in Enum.GetValues<Item>())
            Afirmar.Verdadeiro(Expressoes.EhDeHumor(TabelaDoTamagotchi.DoItem(item).CaraDurante), $"{item}: a cara durante o uso é de humor");
    }

    [Teste]
    public void OsOitoGestosNovosSaoAsPosesDosGestosDaArte()
    {
        Gesto[] daOnda = [.. Enum.GetValues<Gesto>().Where(g => g > Gesto.Brincar)];
        Afirmar.Igual(8, daOnda.Length, "oito gestos da onda no fim do enum (os dois da paranoia por último)");
        Afirmar.Sequencia(PosesPixel.DosGestos.Select(p => p.Nome), daOnda.Select(PoseDoPersonagem.NomeDoGesto), "os nomes e a ordem do fim de Gesto");
        foreach (Gesto gesto in daOnda)
            Afirmar.Verdadeiro(PosesPixel.PorNome(PoseDoPersonagem.NomeDoGesto(gesto)) is not null, $"{gesto}: a pose é achada pelo nome");
    }
}
