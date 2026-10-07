using System.IO;
using System.Text;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Animacao;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

// Esticar e achatar de desenho animado (toon force).
internal enum Deformacao
{
    Nenhuma,

    // Impacto no chão.
    Achatado,

    // Velocidade: foguete e queda rápida.
    Esticado,
}

// O esconderijo numa lateral é o de baixo, girado 90°.
internal enum Giro
{
    Nenhum,

    // Borda de baixo do quadro vai pra esquerda.
    Horario,

    // Borda de baixo do quadro vai pra direita.
    AntiHorario,

    // De cabeça pra baixo: esconderijo na borda de cima.
    MeiaVolta,
}

// Quadro pedido à pixel art. Todos os campos entram na chave do cache de quadros.
// Pose é o nome em PosesPixel; as de perfil olham pra direita e Espelhado vira pra
// esquerda. Expressao nula usa a cara própria da pose.
internal readonly record struct QuadroDoSprite(string Pose, bool Espelhado, string? Expressao, Deformacao Deformacao = Deformacao.Nenhuma, Giro Giro = Giro.Nenhum)
{
    // Chave de ItensPixel (nome do Item em minúsculas). Só as poses de uso desenham o item.
    public string? Item { get; init; }

    // Sobreposição da onda, por cima do boneco.
    public EfeitoVisual Efeito { get; init; }

    // 0 a EfeitosPixel.Fases − 1. Relógio parado ou sem sobreposição: sempre 0.
    public int Fase { get; init; }
}

// O que a pose precisa do movimento. Velocidade em DIP/s, positiva pra baixo.
internal readonly record struct Dinamica(double VelocidadeVerticalDip, int Quiques, bool Foguete, bool Agarrado = false,
    LadoDoEsconderijo Esconderijo = LadoDoEsconderijo.Nenhum);

// Escolhe o quadro do personagem. Retrato + dinâmica decidem a situação, e o clipe
// do manifesto dá pose, cara, espelho e deformação pelos passos no estado. Giro,
// item e sobreposição ficam aqui porque não dependem do tempo. O uso no chão vem de
// UsosPixel, cuja soma de passos é a duração do uso no núcleo. Não muda estado nem posição.
internal static class PoseDoPersonagem
{
    // Passos em que o impacto do quique aparece achatado.
    internal const int PassosDoAchatamento = 5;

    // DIP/s.
    internal const double VelocidadeDoEsticamento = Deformacoes.VelocidadeDoEsticamento;

    // 5 trocas por segundo a 60 passos por segundo.
    internal const int PassosPorFaseDaSobreposicao = 12;

    internal const string RecursoDoManifesto = "Buzzy.App.Apresentacao.clipes.json";

    // Lido uma vez; o build já validou o manifesto.
    internal static ManifestoDeClipes Manifesto { get; } = LerManifestoEmbutido();

    private static ManifestoDeClipes LerManifestoEmbutido()
    {
        using Stream recurso = typeof(PoseDoPersonagem).Assembly.GetManifestResourceStream(RecursoDoManifesto)
            ?? throw new InvalidOperationException($"O manifesto de clipes ({RecursoDoManifesto}) não está embutido no app.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return ManifestoDeClipes.Ler(leitor.ReadToEnd());
    }

    // Pose pelo estado, dinâmica, uso e gesto da onda; por cima de qualquer pose, a
    // sobreposição da onda na fase do relógio. As caras de efeito chegam pela
    // expressão do retrato, nas poses que mostram a cara.
    internal static QuadroDoSprite Escolher(Retrato r, long passosNoEstado, Dinamica dinamica = default) => Escolher(Manifesto, r, passosNoEstado, dinamica);

    // Com outro manifesto, pros testes.
    internal static QuadroDoSprite Escolher(ManifestoDeClipes manifesto, Retrato r, long passosNoEstado, Dinamica dinamica = default)
    {
        ArgumentNullException.ThrowIfNull(manifesto);
        ArgumentNullException.ThrowIfNull(r);
        QuadroDoSprite quadro = Pose(manifesto, r, passosNoEstado, dinamica);
        EfeitoVisual efeito = r.Onda is { } onda ? SobreposicaoDaOnda(onda.Tipo) : EfeitoVisual.Nenhum;
        // Sem sobreposição a fase fica 0, senão o cache guardaria o mesmo desenho uma vez por fase.
        return efeito == EfeitoVisual.Nenhum ? quadro : quadro with { Efeito = efeito, Fase = FaseDaSobreposicao(r.RelogioAtivo, passosNoEstado) };
    }

    // Paranoico ganha o suor de desenho animado (com tremidinho de 1 px na arte).
    // Satisfeito, Alegre, Relaxado e Ligado só mudam a cara e o jeito.
    internal static EfeitoVisual SobreposicaoDaOnda(Onda onda) => onda switch
    {
        Onda.Bebado => EfeitoVisual.Bolhas,
        Onda.Chapado => EfeitoVisual.Fumaca,
        Onda.Eletrico => EfeitoVisual.Brilhos,
        Onda.Tonto => EfeitoVisual.Estrelinhas,
        Onda.Euforico => EfeitoVisual.Coracoes,
        Onda.Viajando => EfeitoVisual.Cores,
        Onda.Paranoico => EfeitoVisual.Suor,
        _ => EfeitoVisual.Nenhum,
    };

    // Relógio parado: sempre 0, pra nada no sprite mudar sozinho.
    internal static int FaseDaSobreposicao(bool relogioLigado, long passosNoEstado)
        => relogioLigado ? (int)(Math.Max(0, passosNoEstado) / PassosPorFaseDaSobreposicao % EfeitosPixel.Fases) : 0;

    private static QuadroDoSprite Pose(ManifestoDeClipes manifesto, Retrato r, long passosNoEstado, Dinamica dinamica)
    {
        // Uso no chão: quadro da animação do verbo, com o item na mão e a cara da
        // própria pose, de frente e sem espelho.
        if (r.Estado == Estado.Using && r.Uso is { Apoio: ApoioDoUso.Chao } uso)
            return new(UsosPixel.Quadro(VerboDaArte(uso.Verbo), r.PassoDoUso).Nome, false, null) { Item = NomeDoItem(uso.Item) };
        (string situacao, Giro giro) = Situacao(r, passosNoEstado, dinamica);
        Clipe clipe = manifesto[situacao];
        (_, QuadroDoClipe q) = ReprodutorDeClipes.Quadro(clipe, passosNoEstado);
        string? cara = q.Cara ?? clipe.Cara switch
        {
            OrigemDaCara.Pose => null,
            OrigemDaCara.RetratoSemNeutro when r.Expressao == Expressao.Neutro => null,
            _ => NomeDaExpressao(r.Expressao),
        };
        Deformacao deformacao = (q.Deformacao ?? clipe.Deformacao) switch
        {
            DeformacaoDoQuadro.Achatado => Deformacao.Achatado,
            DeformacaoDoQuadro.Esticado => Deformacao.Esticado,
            DeformacaoDoQuadro.PelaVelocidade when Math.Abs(dinamica.VelocidadeVerticalDip) >= VelocidadeDoEsticamento => Deformacao.Esticado,
            _ => Deformacao.Nenhuma,
        };
        return new(q.Pose, clipe.Espelha && r.Direcao == Direcao.Esquerda, cara, deformacao, giro);
    }

    // Situação do retrato e giro do esconderijo (que não é do clipe):
    // - Using fora do chão: pose do apoio, com a cara do item fixa o uso inteiro;
    // - escondido (inclusive reagindo ou pressionado): só cabeça e mãos, o corpo
    //   nunca aparece de relance;
    // - quique: achatado nos primeiros passos, esticado se rápido, senão no ar;
    // - Idle: o clipe do gesto em curso; sem gesto, parado.
    internal static (string Situacao, Giro Giro) Situacao(Retrato r, long passosNoEstado, Dinamica dinamica)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.Estado == Estado.Using && r.Uso is { } uso)
            return uso.Apoio switch
            {
                ApoioDoUso.Parede => ("uso-parede", Giro.Nenhum),
                ApoioDoUso.Cipo => ("uso-cipo", Giro.Nenhum),
                ApoioDoUso.Esconderijo => ("uso-esconderijo", GiroDoEsconderijo(dinamica.Esconderijo)),
                _ => throw new ArgumentOutOfRangeException(nameof(r), uso.Apoio, "O uso no chão não tem situação: vem de UsosPixel."),
            };
        if (dinamica.Esconderijo != LadoDoEsconderijo.Nenhum && r.Estado is Estado.Peeking or Estado.Reacting or Estado.Pressed)
            return (r.Estado == Estado.Pressed ? "escondido-pressionado" : "escondido", GiroDoEsconderijo(dinamica.Esconderijo));
        bool rapido = Math.Abs(dinamica.VelocidadeVerticalDip) >= VelocidadeDoEsticamento;
        string situacao = r.Estado switch
        {
            Estado.Walking => "andando",
            Estado.Climbing when dinamica.Foguete => "foguete",
            Estado.Climbing when dinamica.Agarrado => "escalando-agarrado",
            Estado.Climbing => "escalando",
            Estado.Hanging when dinamica.Agarrado => "cipo-agarrado",
            Estado.Hanging => "cipo",
            Estado.Jumping when dinamica.Quiques > 0 && passosNoEstado < PassosDoAchatamento => "quique-impacto",
            Estado.Jumping when dinamica.Quiques > 0 && rapido => "quique-esticado",
            Estado.Jumping when dinamica.Quiques > 0 => "quique-voo",
            Estado.Jumping => "pulo",
            Estado.Falling => "caindo",
            Estado.Landing => "pousando",
            Estado.Resting => r.Expressao is Expressao.Dormindo ? "dormindo" : "sentado",
            Estado.Pressed or Estado.Dragging => "segurado",
            Estado.Reacting => r.Reacao switch
            {
                VarianteDaReacao.Susto => Situacoes.DaReacao("susto"),
                VarianteDaReacao.Flagra => Situacoes.DaReacao("flagra"),
                VarianteDaReacao.Empolgada => Situacoes.DaReacao("empolgado"),
                VarianteDaReacao.Preguica => Situacoes.DaReacao("preguica"),
                _ => "reagindo",
            },
            Estado.Idle when r.NaBorda => Situacoes.EspiandoNaBorda,
            Estado.Idle when r.Olhando => Situacoes.OlhandoJanela,
            Estado.Idle when r.Gesto != Gesto.Nenhum => Situacoes.DoGesto(NomeDoGesto(r.Gesto)),
            _ => "parado",
        };
        return (situacao, Giro.Nenhum);
    }

    // A borda de baixo do quadro vai pra borda da tela.
    private static Giro GiroDoEsconderijo(LadoDoEsconderijo lado) => lado switch
    {
        LadoDoEsconderijo.Esquerda => Giro.Horario,
        LadoDoEsconderijo.Direita => Giro.AntiHorario,
        LadoDoEsconderijo.Cima => Giro.MeiaVolta,
        _ => Giro.Nenhum,
    };

    // Expressões, itens e gestos usam na arte o nome do enum em minúsculas
    // (ex.: lancaperfume, md).
    internal static string NomeDaExpressao(Expressao e) => e.ToString().ToLowerInvariant();

    internal static string NomeDoItem(Item item) => item.ToString().ToLowerInvariant();

    internal static string NomeDoGesto(Gesto gesto) => gesto.ToString().ToLowerInvariant();

    // Mesmos nomes dos dois lados; um teste de contrato amarra os dois enums.
    internal static Verbo VerboDaArte(VerboDeUso verbo) => verbo switch
    {
        VerboDeUso.Comer => Verbo.Comer,
        VerboDeUso.Beber => Verbo.Beber,
        VerboDeUso.Fumar => Verbo.Fumar,
        VerboDeUso.Cheirar => Verbo.Cheirar,
        VerboDeUso.Engolir => Verbo.Engolir,
        VerboDeUso.Inalar => Verbo.Inalar,
        _ => throw new ArgumentOutOfRangeException(nameof(verbo), verbo, "Verbo de uso sem animação na arte."),
    };
}
