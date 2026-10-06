using Buzzy.Core.Personagem;

namespace Buzzy.Core.Testes;

/// <summary>
/// As preferências dos testes de mecânica e de formato: as padrão com o conteúdo adulto e os nove itens adultos ligados,
/// o que valia antes da DEC-041 e o que as reproduções gravadas assumem quando não trazem os campos. O padrão do arquivo de
/// configurações (adulto desligado, quatro itens marcados; DEC-041, item 2) tem os testes dele em ConteudoAdultoTestes e
/// EsquemaDeConfiguracoesTestes.
/// </summary>
internal static class PreferenciasDeTeste
{
    public static readonly Preferencias Completas = Preferencias.Padrao with { ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos };

    /// <summary>As mesmas preferências com o conteúdo adulto e os nove itens ligados (o que <c>new Preferencias(...)</c> valia antes da DEC-041).</summary>
    public static Preferencias Completa(this Preferencias p) => p with { ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos };

    /// <summary>A chave geral e a seleção sorteadas, quase sempre ligadas, para as sequências aleatórias exercitarem a DEC-041.</summary>
    public static Preferencias ComAdultosAleatorios(this Preferencias p, Random rnd)
    {
        ConjuntoDeItens itens = Preferencias.TodosOsItensAdultos;
        if (rnd.Next(4) == 0)
        {
            itens = ConjuntoDeItens.Vazio;
            foreach (Item item in Preferencias.TodosOsItensAdultos.Itens)
                if (rnd.Next(4) != 0) itens = itens.Com(item);
        }
        return p with { ConteudoAdulto = rnd.Next(8) != 0, ItensAdultosHabilitados = itens };
    }
}
