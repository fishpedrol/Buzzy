using Buzzy.App.Apresentacao;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// A escolha da pose da Fase 5, congelada antes do manifesto de clipes (Fase 6, passo F6-P2; DEC-036): uma cópia de
/// `PoseDoPersonagem.Pose` e `DeUso` de 2026-10-03, sem a sobreposição da onda, que não mudou. `ManifestoNoAppTestes`
/// compara a escolha nova com esta, chamada por chamada. Não é código do produto: os clipes que a Fase 6 anima de propósito
/// (passo F6-P4) saem da comparação, com os testes próprios.
/// </summary>
internal static class PoseDaFase5
{
    private const int PassosPorQuadroAndando = 8;
    private const int PassosPorQuadroEscalando = 12;
    private const int PassosDoImpulso = 6;
    private const int PassosPorQuadroNoCipo = 10;
    private const int PassosDoAchatamento = 5;
    private const double VelocidadeDoEsticamento = 700;
    private const int PassosPorQuadroDaTremedeira = 4;
    private static readonly int[] BalancoDoCipo = [1, 2, 3, 2];
    private const string PoseAgarradoNaParede = "escalando-1";
    private const string PoseAgarradoNoCipo = "cipo-2";
    private const string PoseEscondido = "escondido";
    private static readonly string? CaraDaTremedeira = PosesPixel.PorNome(NomeDoGesto(Gesto.Tremedeira))?.Expressao;

    internal static QuadroDoSprite Pose(Retrato r, long passosNoEstado, Dinamica dinamica)
    {
        bool esquerda = r.Direcao == Direcao.Esquerda;
        string expressao = NomeDaExpressao(r.Expressao);
        Deformacao rapido = Math.Abs(dinamica.VelocidadeVerticalDip) >= VelocidadeDoEsticamento ? Deformacao.Esticado : Deformacao.Nenhuma;
        if (r.Estado == Estado.Using && r.Uso is { } uso) return DeUso(uso, r.PassoDoUso, esquerda, expressao, dinamica);
        // Escondido (DEC-025): no esconderijo, e também na reação e no pressionar de quem continua
        // escondido, só a cabeça e as mãos aparecem; o corpo nunca surge de relance.
        if (dinamica.Esconderijo != LadoDoEsconderijo.Nenhum && r.Estado is Estado.Peeking or Estado.Reacting or Estado.Pressed)
        {
            string? cara = r.Estado == Estado.Pressed ? "surpreso" : r.Expressao == Expressao.Neutro ? null : expressao;
            return new(PoseEscondido, false, cara, Deformacao.Nenhuma, GiroDoEsconderijo(dinamica.Esconderijo));
        }
        return r.Estado switch
        {
            Estado.Walking => new($"andando-{1 + (int)(passosNoEstado / PassosPorQuadroAndando % 4)}", esquerda, expressao),
            // Toon force: o foguete de borracha sobe esticado, com o braço para cima.
            Estado.Climbing when dinamica.Foguete => new("impulso", esquerda, null, Deformacao.Esticado),
            // Na parede, o macaquinho olha para ela: parede da direita sem espelho, da esquerda espelhada.
            Estado.Climbing when dinamica.Agarrado => new(PoseAgarradoNaParede, esquerda, null),
            Estado.Climbing => new($"escalando-{1 + (int)(passosNoEstado / PassosPorQuadroEscalando % 2)}", esquerda, null),
            // Na borda de cima, pendurado num cipó (DEC-024): parado, o quadro do meio; andando, balança.
            // Com a cara neutra, fica a da pose: rindo no cipó.
            Estado.Hanging when dinamica.Agarrado => new(PoseAgarradoNoCipo, esquerda, r.Expressao == Expressao.Neutro ? null : expressao),
            Estado.Hanging => new($"cipo-{BalancoDoCipo[(int)(passosNoEstado / PassosPorQuadroNoCipo % BalancoDoCipo.Length)]}", esquerda,
                r.Expressao == Expressao.Neutro ? null : expressao),
            // Toon force: o quique começa achatado no chão e sobe esticado.
            Estado.Jumping when dinamica.Quiques > 0 && passosNoEstado < PassosDoAchatamento => new("pousando", false, null, Deformacao.Achatado),
            Estado.Jumping when dinamica.Quiques > 0 && rapido == Deformacao.Esticado => new("impulso", esquerda, null, Deformacao.Esticado),
            Estado.Jumping when dinamica.Quiques > 0 => new("no-ar", esquerda, null),
            Estado.Jumping => new(passosNoEstado < PassosDoImpulso ? "impulso" : "no-ar", esquerda, null, passosNoEstado < PassosDoImpulso ? Deformacao.Nenhuma : rapido),
            Estado.Falling => new("caindo", false, null, rapido),
            Estado.Landing => new("pousando", false, null, passosNoEstado < PassosDoAchatamento ? Deformacao.Achatado : Deformacao.Nenhuma),
            Estado.Resting => new(r.Expressao is Expressao.Dormindo ? "dormindo" : "sentado", false, null),
            Estado.Pressed or Estado.Dragging => new("segurado", false, null),
            Estado.Reacting => new("reagindo", false, null),
            Estado.Idle => r.Gesto switch
            {
                Gesto.Espiar => new("espiando", false, null),
                Gesto.OlharAoRedor => new("olhando", false, null),
                Gesto.Cocar => new("cocando", false, null),
                Gesto.Espreguicar => new("espreguicando", false, null),
                Gesto.Brincar => new("brincando", false, null),
                // Gestos da onda (DEC-028): as poses provisórias da arte (crítica, L11), com a cara delas, como os outros
                // gestos. A tremedeira é o parado deslocado 1 pixel: os dois quadros se alternam, com a mesma cara. Os da
                // paranoia (adicional de 2026-10-01), olhar pro teto e agachar, têm desenho próprio, com a cara "paranoico".
                Gesto.Tremedeira when passosNoEstado / PassosPorQuadroDaTremedeira % 2 != 0 => new("parado", false, CaraDaTremedeira),
                Gesto.Soluco or Gesto.Danca or Gesto.Gargalhada or Gesto.Espirro or Gesto.Tosse or Gesto.Tremedeira
                    or Gesto.OlharProTeto or Gesto.Agachar => new(NomeDoGesto(r.Gesto), false, null),
                _ => new("parado", false, expressao),
            },
            _ => new("parado", false, expressao),
        };
    }

    /// <summary>
    /// USING (DEC-028). No chão, o quadro da animação do verbo no passo do uso, com o item na mão e a cara da própria
    /// pose (crítica, C10): de frente, sem espelho, como as outras poses de frente. Na parede, no cipó e no esconderijo,
    /// que ainda não têm pose de uso (C25), a pose de quem está agarrado ou espia ali, com a cara do item durante o uso
    /// (a do retrato: o núcleo a fixa do começo ao fim do uso) e sem objeto na mão.
    /// </summary>
    private static QuadroDoSprite DeUso(Uso uso, int passoDoUso, bool esquerda, string expressao, Dinamica dinamica) => uso.Apoio switch
    {
        ApoioDoUso.Parede => new(PoseAgarradoNaParede, esquerda, expressao),
        ApoioDoUso.Cipo => new(PoseAgarradoNoCipo, esquerda, expressao),
        ApoioDoUso.Esconderijo => new(PoseEscondido, false, expressao, Deformacao.Nenhuma, GiroDoEsconderijo(dinamica.Esconderijo)),
        _ => new(UsosPixel.Quadro(VerboDaArte(uso.Verbo), passoDoUso).Nome, false, null) { Item = NomeDoItem(uso.Item) },
    };


    private static Giro GiroDoEsconderijo(LadoDoEsconderijo lado) => lado switch
    {
        LadoDoEsconderijo.Esquerda => Giro.Horario,
        LadoDoEsconderijo.Direita => Giro.AntiHorario,
        LadoDoEsconderijo.Cima => Giro.MeiaVolta,
        _ => Giro.Nenhum,
    };

    private static string NomeDaExpressao(Expressao e) => PoseDoPersonagem.NomeDaExpressao(e);

    private static string NomeDoItem(Item item) => PoseDoPersonagem.NomeDoItem(item);

    private static string NomeDoGesto(Gesto gesto) => PoseDoPersonagem.NomeDoGesto(gesto);

    private static Verbo VerboDaArte(VerboDeUso verbo) => PoseDoPersonagem.VerboDaArte(verbo);
}
