using Buzzy.Core.Personagem;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// As sementes da paranoia (pedidos do usuário de 2026-10-01; DEC-028) para o que abre o Buzzy de verdade: se o primeiro
/// sorteio do gerador da paranoia sai com uma semente, com o gerador do estado inicial do núcleo
/// (<see cref="EstadoDoNucleo.Inicial"/>) e a chance dada (a do aplicativo, 1 em 8), e a primeira semente em que ele sai. A
/// escolha depende só do gerador; a regra da paranoia (quem sorteia e quando: o uso que fecha a mistura com droga
/// sintética, uma vez por episódio) é o que os testes conferem na tela. O teste de integração da paranoia e a verificação de
/// tela (Buzzy.Verificacao, que compila este arquivo, como o <see cref="PerfilDeTeste"/>) escolhem a semente do mesmo jeito.
/// </summary>
internal static class SementesDaParanoia
{
    /// <summary>Se o primeiro sorteio do gerador da paranoia sai com a semente e a chance dadas.</summary>
    internal static bool PrimeiroSorteioSai(ulong semente, Chance chance)
    {
        ArgumentNullException.ThrowIfNull(chance);
        return EstadoDoNucleo.Inicial(semente).AleatorioDaParanoia.Sortear(chance).Saiu;
    }

    /// <summary>A primeira semente, de 1 a <paramref name="ultima"/>, em que o primeiro sorteio sai; nula se nenhuma.</summary>
    internal static ulong? PrimeiraEmQueOPrimeiroSorteioSai(Chance chance, ulong ultima)
    {
        for (ulong semente = 1; semente <= ultima; semente++)
        {
            if (PrimeiroSorteioSai(semente, chance)) return semente;
        }
        return null;
    }
}
