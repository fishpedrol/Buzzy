using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Animacao;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 7, passo F7-P4 (DEC-037, item 8; DEC-036): as variantes da reação ao clique na tela. Cada uma tem a situação
/// <c>reagindo-</c> no manifesto, com a cara trocada por quadro; a de antes continua <c>reagindo</c>, com a risada; a cara
/// do primeiro quadro é a do núcleo (ARCHITECTURE.md 2.10); escondido, o esconderijo vale para todas.
/// </summary>
internal sealed class ReacoesNaTelaTestes
{
    private static Retrato R(VarianteDaReacao variante, Expressao cara)
        => new(Estado.Reacting, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), Direcao.Direita, cara, Gesto.Nenhum, false, false, NivelDeEnergia.Media, true, Sinal.FoiClicado) { Reacao = variante };

    private static (string Pose, string? Cara) Quadro(Retrato r, long passos, Dinamica d = default)
    {
        QuadroDoSprite q = PoseDoPersonagem.Escolher(r, passos, d);
        return (q.Pose, q.Expressao);
    }

    [Teste]
    public void CadaVariante_ASuaSituacao_EAsCarasPorQuadro()
    {
        (VarianteDaReacao Variante, Expressao Cara, string Situacao, (long Passos, string Pose, string Cara)[] Quadros)[] casos =
        [
            (VarianteDaReacao.Susto, Expressao.Surpreso, "reagindo-susto", [(0, "reagindo", "surpreso"), (6, "reagindo-2", "surpreso"), (12, "reagindo", "rindo"), (47, "reagindo-2", "rindo"), (200, "reagindo-2", "rindo")]),
            (VarianteDaReacao.Flagra, Expressao.Surpreso, "reagindo-flagra", [(0, "reagindo", "surpreso"), (11, "reagindo", "surpreso"), (12, "reagindo-2", "travesso"), (18, "reagindo", "travesso"), (200, "reagindo", "travesso")]),
            (VarianteDaReacao.Empolgada, Expressao.Empolgado, "reagindo-empolgado", [(0, "reagindo", "empolgado"), (4, "reagindo-2", "empolgado"), (8, "reagindo", "empolgado"), (47, "reagindo-2", "empolgado")]),
            (VarianteDaReacao.Preguica, Expressao.Sonolento, "reagindo-preguica", [(0, "parado", "sonolento"), (8, "espreguicando-1", "bocejando"), (22, "parado", "feliz"), (200, "parado", "feliz")]),
        ];
        foreach ((VarianteDaReacao variante, Expressao cara, string situacao, (long Passos, string Pose, string Cara)[] quadros) in casos)
        {
            Retrato r = R(variante, cara);
            Afirmar.Igual((situacao, Giro.Nenhum), PoseDoPersonagem.Situacao(r, 0, default), $"{variante}: a situação");
            Afirmar.Verdadeiro(Situacoes.Todas.Contains(situacao), $"{situacao} na lista fechada");
            foreach ((long passos, string pose, string caraDoQuadro) in quadros)
                Afirmar.Igual((pose, (string?)caraDoQuadro), Quadro(r, passos), $"{variante}, passo {passos}");
            Afirmar.Igual(PoseDoPersonagem.NomeDaExpressao(cara), Quadro(r, 0).Cara, $"{variante}: a cara do primeiro quadro é a do núcleo");
        }
    }

    [Teste]
    public void Padrao_ComoAntes_EEscondido_OEsconderijo()
    {
        Retrato padrao = R(VarianteDaReacao.Padrao, Expressao.Feliz);
        Afirmar.Igual(("reagindo", Giro.Nenhum), PoseDoPersonagem.Situacao(padrao, 0, default), "a de antes");
        foreach (VarianteDaReacao v in Enum.GetValues<VarianteDaReacao>())
            Afirmar.Igual("escondido", PoseDoPersonagem.Situacao(R(v, Expressao.Surpreso), 0, new Dinamica(0, 0, false, Esconderijo: LadoDoEsconderijo.Baixo)).Situacao, $"{v}: escondido");
    }
}
