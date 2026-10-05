using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Animacao;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 6, passo F6-P4 (DEC-036, item 5): os clipes animados — coçar, espreguiçar, olhar ao redor, espiar, brincar, cair,
/// reagir e a gargalhada da onda — escolhidos pela apresentação passo a passo, com os quadros e os tempos do manifesto; as
/// situações em que o relógio fica parado com um quadro só (item 3); e nenhum quadro novo da arte sem uso.
/// </summary>
internal sealed class ClipesAnimadosTestes
{
    private static Retrato R(Estado estado, Gesto gesto = Gesto.Nenhum)
        => new(estado, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), Direcao.Direita, Expressao.Neutro, gesto, false, false, NivelDeEnergia.Media, true, Sinal.Nenhum);

    private static string Pose(Retrato r, long passos) => PoseDoPersonagem.Escolher(r, passos).Pose;

    // Os quadros e os tempos: em cada passo pedido, a pose do manifesto, como a apresentação a escolhe pelo retrato.
    [Teste]
    public void ClipesAnimados_OsQuadrosNosTemposDoManifesto()
    {
        (string Caso, Retrato R, (long Passos, string Pose)[] Esperado)[] casos =
        [
            ("coçar", R(Estado.Idle, Gesto.Cocar), [(0, "cocando"), (7, "cocando"), (8, "cocando-2"), (15, "cocando-2"), (16, "cocando"), (24, "cocando-2")]),
            ("espreguiçar", R(Estado.Idle, Gesto.Espreguicar), [(0, "espreguicando-1"), (14, "espreguicando-1"), (15, "espreguicando"), (44, "espreguicando"), (45, "espreguicando-2"), (179, "espreguicando-2")]),
            ("olhar ao redor", R(Estado.Idle, Gesto.OlharAoRedor), [(0, "olhando"), (29, "olhando"), (30, "olhando-2"), (60, "olhando")]),
            ("espiar", R(Estado.Idle, Gesto.Espiar), [(0, "espiando"), (23, "espiando"), (24, "espiando-2"), (36, "espiando")]),
            ("brincar", R(Estado.Idle, Gesto.Brincar), [(0, "brincando"), (10, "brincando-2"), (20, "brincando")]),
            ("cair", R(Estado.Falling), [(0, "caindo"), (6, "caindo-2"), (12, "caindo")]),
            ("reagir", R(Estado.Reacting), [(0, "reagindo"), (6, "reagindo-2"), (12, "reagindo"), (35, "reagindo-2")]),
            ("gargalhada", R(Estado.Idle, Gesto.Gargalhada), [(0, "gargalhada"), (6, "reagindo-2"), (12, "gargalhada")]),
        ];
        foreach ((string caso, Retrato r, (long Passos, string Pose)[] esperado) in casos)
            foreach ((long passos, string pose) in esperado)
                Afirmar.Igual(pose, Pose(r, passos), $"{caso}, passo {passos}");
    }

    // Os dois quadros de cada animação são desenhos diferentes (o corpo se mexe), e a cair continua esticando com a
    // velocidade nos dois quadros.
    [Teste]
    public void ClipesAnimados_QuadrosDiferentes_EACaidaEsticaNosDois()
    {
        foreach (string situacao in ManifestoDeClipesTestes.Animadas)
        {
            Clipe clipe = PoseDoPersonagem.Manifesto[situacao];
            Afirmar.Verdadeiro(clipe.Quadros.Count >= 2, $"{situacao}: mais de um quadro");
            uint[][] desenhos = [.. clipe.Quadros.Select(q => BonecoPixel.Desenhar(PosesPixel.PorNome(q.Pose)!, q.Cara).ParaArgb())];
            for (int i = 1; i < desenhos.Length; i++)
                Afirmar.Falso(desenhos[i].SequenceEqual(desenhos[i - 1]), $"{situacao}: o quadro {i} é outro desenho");
        }
        foreach (long passos in new long[] { 0, 6 })
            Afirmar.Igual(Deformacao.Esticado, PoseDoPersonagem.Escolher(R(Estado.Falling), passos, new Dinamica(1200, 0, false)).Deformacao, $"caindo rápido, passo {passos}");
    }

    // Sem relógio, os passos não andam e o quadro não muda (DEC-011; DEC-036, item 3): as situações dos estados que não
    // ligam o relógio — descansando, escondido, pressionado, arrastado, agarrado e parado sem gesto — têm um quadro só.
    [Teste]
    public void SemRelogio_UmQuadroSo()
    {
        foreach (string situacao in new[] { "parado", "sentado", "dormindo", "segurado", "escondido", "escondido-pressionado", "escalando-agarrado", "cipo-agarrado", "uso-parede", "uso-cipo", "uso-esconderijo" })
            Afirmar.Igual(1, PoseDoPersonagem.Manifesto[situacao].Quadros.Count, $"{situacao}: um quadro");
    }

    // Toda pose nova da arte (PosesPixel.DosClipes) está em algum clipe do manifesto, e o nome dela é o da pose-chave com
    // um sufixo de quadro.
    [Teste]
    public void QuadrosNovos_TodosUsados()
    {
        HashSet<string> usadas = [.. PoseDoPersonagem.Manifesto.Clipes.SelectMany(c => c.Quadros).Select(q => q.Pose)];
        foreach (PosePixel p in PosesPixel.DosClipes)
        {
            Afirmar.Verdadeiro(usadas.Contains(p.Nome), $"{p.Nome}: usado num clipe");
            string chave = p.Nome[..p.Nome.LastIndexOf('-')];
            Afirmar.Verdadeiro(PosesPixel.Todas.Any(t => t.Nome == chave), $"{p.Nome}: sai da pose-chave {chave}");
        }
    }
}
