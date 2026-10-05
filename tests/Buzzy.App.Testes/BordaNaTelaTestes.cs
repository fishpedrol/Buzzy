using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Animacao;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 7, passo F7-P5 (DEC-037, item 9): a espiada na borda explorada na tela. Com o retrato na borda, a situação é
/// <c>espiando-na-borda</c>: de perfil, a mão à frente dos olhos, espelhada pela direção (virado para fora); o espiar comum
/// continua o de antes.
/// </summary>
internal sealed class BordaNaTelaTestes
{
    private static Retrato R(Direcao direcao, bool naBorda)
        => new(Estado.Idle, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), direcao, Expressao.Neutro, Gesto.Espiar, false, false, NivelDeEnergia.Media, true, Sinal.Nenhum) { NaBorda = naBorda };

    [Teste]
    public void NaBorda_DePerfil_ViradoParaFora()
    {
        Afirmar.Igual((Situacoes.EspiandoNaBorda, Giro.Nenhum), PoseDoPersonagem.Situacao(R(Direcao.Direita, true), 0, default), "na borda");
        Afirmar.Igual("gesto-espiar", PoseDoPersonagem.Situacao(R(Direcao.Direita, false), 0, default).Situacao, "o espiar comum");
        foreach ((long passos, string pose) in new[] { (0L, "andando-2-espia1"), (23L, "andando-2-espia1"), (24L, "andando-2-espia2"), (36L, "andando-2-espia1") })
            Afirmar.Igual(pose, PoseDoPersonagem.Escolher(R(Direcao.Direita, true), passos).Pose, $"passo {passos}");
        Afirmar.Igual((false, true), (PoseDoPersonagem.Escolher(R(Direcao.Direita, true), 0).Espelhado, PoseDoPersonagem.Escolher(R(Direcao.Esquerda, true), 0).Espelhado), "espelhado pela direção, como andando");
        Afirmar.Igual(PoseDoPersonagem.Escolher(R(Direcao.Esquerda, true), 0).Espelhado, PoseDoPersonagem.Escolher(R(Direcao.Esquerda, false) with { Estado = Estado.Walking, Gesto = Gesto.Nenhum }, 0).Espelhado, "o mesmo lado da caminhada");
    }

    // F7-P8 (DEC-037, item 5): olhando a janela, de perfil, virado para ela pela direção, com a cara do retrato (curioso);
    // um quadro só, porque parado e sem gesto o relógio fica desligado (invariante 29).
    [Teste]
    public void OlhandoAJanela_DePerfil_ViradoParaEla_ComACaraDoRetrato()
    {
        Retrato olhando = R(Direcao.Esquerda, false) with { Gesto = Gesto.Nenhum, Expressao = Expressao.Curioso, Olhando = true };
        Afirmar.Igual((Situacoes.OlhandoJanela, Giro.Nenhum), PoseDoPersonagem.Situacao(olhando, 0, default), "olhando");
        QuadroDoSprite q = PoseDoPersonagem.Escolher(olhando, 500);
        Afirmar.Igual(("andando-2-olha1", true, (string?)"curioso"), (q.Pose, q.Espelhado, q.Expressao), "de perfil, espelhado para a esquerda, curioso");
        Afirmar.Igual(1, PoseDoPersonagem.Manifesto[Situacoes.OlhandoJanela].Quadros.Count, "um quadro");
        Afirmar.Igual("parado", PoseDoPersonagem.Situacao(olhando with { Olhando = false }, 0, default).Situacao, "sem olhar, parado");
    }
}
