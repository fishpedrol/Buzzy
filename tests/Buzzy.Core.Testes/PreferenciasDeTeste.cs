using Buzzy.Core.Personagem;

namespace Buzzy.Core.Testes;

/// <summary>
/// As preferências dos testes de mecânica e de formato: as padrão com o conteúdo adulto e os nove itens adultos ligados,
/// o que valia antes da DEC-041 e o que as reproduções gravadas assumem quando não trazem os campos. O padrão do arquivo de
/// configurações (adulto desligado, três itens marcados; DEC-041, item 7) tem os testes dele em ConteudoAdultoTestes e
/// EsquemaDeConfiguracoesTestes.
/// </summary>
internal static class PreferenciasDeTeste
{
    /// <summary>O que valia antes da DEC-041, da DEC-045 e da DEC-046: também a travessia ligada.</summary>
    public static readonly Preferencias Completas = Preferencias.Padrao.Completa() with { AtravessarMonitores = true };

    /// <summary>As mesmas preferências com o conteúdo adulto e os nove itens ligados (o que <c>new Preferencias(...)</c> valia antes da DEC-041).</summary>
    /// O baseado por conta própria também, como antes da DEC-045 (as reproduções gravadas assumem isso sem o campo).
    public static Preferencias Completa(this Preferencias p) => p with
    {
        ConteudoAdulto = true,
        ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos,
        ItensPorContaPropria = ConjuntoDeItens.Vazio.Com(Item.Baseado),
    };

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
        // O uso por conta própria (DEC-045): quase sempre o baseado, às vezes outras das seis ou nenhuma.
        ConjuntoDeItens proprios = ConjuntoDeItens.Vazio.Com(Item.Baseado);
        if (rnd.Next(3) == 0)
        {
            proprios = ConjuntoDeItens.Vazio;
            foreach (Item item in TabelaDoTamagotchi.Ilicitos.Itens)
                if (rnd.Next(2) == 0) proprios = proprios.Com(item);
        }
        return p with { ConteudoAdulto = rnd.Next(8) != 0, ItensAdultosHabilitados = itens, ItensPorContaPropria = proprios };
    }
}
