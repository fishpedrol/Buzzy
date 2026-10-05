using System.IO;
using System.Text;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Animacao;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Apresentacao;

/// <summary>Esticar e achatar de desenho animado nas poses provisórias (toon force, DEC-023).</summary>
internal enum Deformacao
{
    Nenhuma,

    /// <summary>Mais largo e mais baixo: o impacto no chão.</summary>
    Achatado,

    /// <summary>Mais estreito e mais alto: a velocidade, no foguete e na queda rápida.</summary>
    Esticado,
}

/// <summary>Giro de 90° da pose (DEC-025): o esconderijo numa lateral é o de baixo, girado.</summary>
internal enum Giro
{
    Nenhum,

    /// <summary>A borda de baixo do quadro vai para a esquerda: esconderijo na lateral esquerda.</summary>
    Horario,

    /// <summary>A borda de baixo do quadro vai para a direita: esconderijo na lateral direita.</summary>
    AntiHorario,

    /// <summary>Meia volta: a borda de baixo do quadro vai para cima, de cabeça para baixo (esconderijo na borda de cima).</summary>
    MeiaVolta,
}

/// <summary>
/// Quadro do sprite pedido à pixel art: pose, espelhamento, expressão, deformação e giro, e, no tamagotchi (DEC-028), o
/// item na mão e a sobreposição da onda. Todos os campos entram na chave do cache de quadros (crítica, C28).
/// </summary>
/// <param name="Pose">Nome da pose em <c>PosesPixel</c> (docs/IDENTIDADE_VISUAL.md, seção 7), achada por <see cref="PosesPixel.PorNome"/>.</param>
/// <param name="Espelhado">As poses de perfil olham para a direita; a esquerda é o espelho.</param>
/// <param name="Expressao">Expressão do rosto, ou nulo para a expressão própria da pose.</param>
/// <param name="Deformacao">Esticar e achatar de desenho animado (toon force).</param>
/// <param name="Giro">Giro de 90° (esconderijo numa lateral).</param>
internal readonly record struct QuadroDoSprite(string Pose, bool Espelhado, string? Expressao, Deformacao Deformacao = Deformacao.Nenhuma, Giro Giro = Giro.Nenhum)
{
    /// <summary>
    /// O item na mão (chave de <see cref="ItensPixel"/>: o nome do valor de <c>Item</c> em minúsculas), só nas poses de
    /// uso; nulo sem item. Nas outras poses, a pixel art não o desenha.
    /// </summary>
    public string? Item { get; init; }

    /// <summary>A sobreposição da onda (crítica, L12), por cima do boneco; o modificador de pose dela vale onde a pose é modificável.</summary>
    public EfeitoVisual Efeito { get; init; }

    /// <summary>
    /// A fase da sobreposição, de 0 a <see cref="EfeitosPixel.Fases"/> − 1: com o relógio ligado, troca a cada 12 passos;
    /// com ele parado, e sem sobreposição, é sempre 0 (DEC-011).
    /// </summary>
    public int Fase { get; init; }
}

/// <summary>O que a pose precisa do movimento em curso (Fase 4, DEC-022 a DEC-024).</summary>
/// <param name="VelocidadeVerticalDip">Velocidade vertical em DIP/s, positiva para baixo.</param>
/// <param name="Quiques">Quiques de borracha já dados nesta queda.</param>
/// <param name="Foguete">Se a escalada é um foguete de borracha.</param>
/// <param name="Agarrado">Parado, agarrado à parede ou ao cipó.</param>
/// <param name="Esconderijo">Em que borda está escondido (DEC-025).</param>
internal readonly record struct Dinamica(double VelocidadeVerticalDip, int Quiques, bool Foguete, bool Agarrado = false,
    LadoDoEsconderijo Esconderijo = LadoDoEsconderijo.Nenhum);

/// <summary>
/// A escolha do quadro do personagem (ARCHITECTURE.md 2.10; DEC-036): pelo retrato e pela dinâmica, a apresentação decide a
/// situação (`Situacoes`), e o clipe do manifesto dá o quadro pelos passos no estado, a cara, o espelho e a deformação. O
/// giro do esconderijo, o item e a sobreposição da onda continuam aqui, porque não são tempo; os quadros de uso no chão vêm
/// de `UsosPixel`, cuja soma de passos é a duração do uso no núcleo (DEC-028). Nada disso muda estado nem posição.
/// </summary>
internal static class PoseDoPersonagem
{
    /// <summary>Passos em que o impacto do quique de borracha aparece achatado (toon force, DEC-023).</summary>
    internal const int PassosDoAchatamento = 5;

    /// <summary>A partir desta velocidade vertical, em DIP/s, o corpo aparece esticado.</summary>
    internal const double VelocidadeDoEsticamento = Deformacoes.VelocidadeDoEsticamento;

    /// <summary>
    /// Passos por fase da sobreposição da onda com o relógio ligado: 5 trocas por segundo a 60 passos por segundo
    /// (desenho da arte, 4.8).
    /// </summary>
    internal const int PassosPorFaseDaSobreposicao = 12;

    /// <summary>O nome do manifesto embutido no app (Apresentacao/clipes.json).</summary>
    internal const string RecursoDoManifesto = "Buzzy.App.Apresentacao.clipes.json";

    /// <summary>O manifesto de clipes do app (DEC-036), embutido e lido uma vez; a validação do build o confere antes.</summary>
    internal static ManifestoDeClipes Manifesto { get; } = LerManifestoEmbutido();

    private static ManifestoDeClipes LerManifestoEmbutido()
    {
        using Stream recurso = typeof(PoseDoPersonagem).Assembly.GetManifestResourceStream(RecursoDoManifesto)
            ?? throw new InvalidOperationException($"O manifesto de clipes ({RecursoDoManifesto}) não está embutido no app.");
        using var leitor = new StreamReader(recurso, Encoding.UTF8);
        return ManifestoDeClipes.Ler(leitor.ReadToEnd());
    }

    /// <summary>
    /// O quadro do sprite para o retrato, sem mudar estado nem posição (ARCHITECTURE.md 2.10): a pose pelo estado, pela
    /// dinâmica e, no tamagotchi (DEC-028), pelo uso e pelo gesto da onda; e, por cima de qualquer pose, a sobreposição da
    /// onda da frente (crítica, L12), na fase do relógio. As caras de efeito e a emoção dominante chegam pela expressão do
    /// retrato, nas poses que mostram a cara dele (crítica, F9).
    /// </summary>
    internal static QuadroDoSprite Escolher(Retrato r, long passosNoEstado, Dinamica dinamica = default) => Escolher(Manifesto, r, passosNoEstado, dinamica);

    /// <summary>O mesmo, com outro manifesto (os testes da Fase 6, critério 1).</summary>
    internal static QuadroDoSprite Escolher(ManifestoDeClipes manifesto, Retrato r, long passosNoEstado, Dinamica dinamica = default)
    {
        ArgumentNullException.ThrowIfNull(manifesto);
        ArgumentNullException.ThrowIfNull(r);
        QuadroDoSprite quadro = Pose(manifesto, r, passosNoEstado, dinamica);
        EfeitoVisual efeito = r.Onda is { } onda ? SobreposicaoDaOnda(onda.Tipo) : EfeitoVisual.Nenhum;
        // Sem sobreposição, a fase fica em 0: o cache não guarda o mesmo desenho uma vez por fase.
        return efeito == EfeitoVisual.Nenhum ? quadro : quadro with { Efeito = efeito, Fase = FaseDaSobreposicao(r.RelogioAtivo, passosNoEstado) };
    }

    /// <summary>
    /// A sobreposição de cada onda (crítica, L12): Bebado, bolhas; Chapado, fumaça; Eletrico, brilhos; Tonto,
    /// estrelinhas; Euforico, corações; Viajando, cores; e a paranoia (adicional de 2026-10-01, DEC-028), o suor de
    /// desenho animado de quem acha que tem alguém no teto, com o tremidinho de 1 pixel da arte. Satisfeito, Alegre,
    /// Relaxado e Ligado só mudam a cara e o jeito.
    /// </summary>
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

    /// <summary>
    /// A fase da sobreposição: com o relógio ligado, (passos no estado / 12) % 3; com ele parado, sempre a 0, a fase
    /// parada (DEC-011): sem relógio, nada no sprite muda sozinho.
    /// </summary>
    internal static int FaseDaSobreposicao(bool relogioLigado, long passosNoEstado)
        => relogioLigado ? (int)(Math.Max(0, passosNoEstado) / PassosPorFaseDaSobreposicao % EfeitosPixel.Fases) : 0;

    private static QuadroDoSprite Pose(ManifestoDeClipes manifesto, Retrato r, long passosNoEstado, Dinamica dinamica)
    {
        // Uso no chão (DEC-028): o quadro da animação do verbo no passo do uso, com o item na mão e a cara da própria pose
        // (crítica, C10), de frente e sem espelho.
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

    /// <summary>
    /// A situação do retrato (DEC-036, item 1), e o giro do esconderijo, que não é do clipe:
    /// <list type="bullet">
    /// <item>em USING, fora do chão, a pose do apoio (parede, cipó ou esconderijo), com a cara do item, que o núcleo fixa do
    /// começo ao fim do uso; ainda não há poses de uso por apoio (crítica, C25);</item>
    /// <item>escondido (DEC-025), também na reação e no pressionar de quem continua escondido: só a cabeça e as mãos, e o
    /// corpo nunca surge de relance; pressionado, com a cara de surpresa;</item>
    /// <item>pela dinâmica (DEC-022 a DEC-024): o foguete de borracha e quem está agarrado à parede ou ao cipó; no quique de
    /// borracha, o impacto achatado nos primeiros passos, depois esticado com a velocidade alta, senão no ar;</item>
    /// <item>em IDLE, o gesto em curso, com o clipe dele; sem gesto, e nos outros estados, parado.</item>
    /// </list>
    /// </summary>
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

    /// <summary>O esconderijo numa lateral é o de baixo, girado: a borda de baixo do quadro vai para a borda da tela (DEC-025).</summary>
    private static Giro GiroDoEsconderijo(LadoDoEsconderijo lado) => lado switch
    {
        LadoDoEsconderijo.Esquerda => Giro.Horario,
        LadoDoEsconderijo.Direita => Giro.AntiHorario,
        LadoDoEsconderijo.Cima => Giro.MeiaVolta,
        _ => Giro.Nenhum,
    };

    /// <summary>Nome da expressão na pixel art: o mesmo do núcleo, em minúsculas (IDENTIDADE_VISUAL.md, seção 6).</summary>
    internal static string NomeDaExpressao(Expressao e) => e.ToString().ToLowerInvariant();

    /// <summary>
    /// Chave do item na pixel art (<see cref="ItensPixel"/>): o nome do valor de <see cref="Item"/> em minúsculas
    /// (crítica, C7), como nas expressões: <c>lancaperfume</c>, <c>md</c>.
    /// </summary>
    internal static string NomeDoItem(Item item) => item.ToString().ToLowerInvariant();

    /// <summary>Nome da pose de um gesto da onda em <see cref="PosesPixel.DosGestos"/>: o nome do gesto em minúsculas (crítica, L11).</summary>
    internal static string NomeDoGesto(Gesto gesto) => gesto.ToString().ToLowerInvariant();

    /// <summary>
    /// O verbo da arte (<see cref="Verbo"/>) para o verbo de uso do núcleo: os mesmos nomes, na mesma ordem
    /// (o teste do contrato entre o núcleo e a arte amarra os dois enums).
    /// </summary>
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
