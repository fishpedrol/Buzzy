using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Poses dos gestos da onda do tamagotchi (DEC-028; crítica, L11): as seis provisórias, que a crítica pedia no
/// passo A4 e a arte tinha deixado para a apresentação (revisão da arte, achado 4), e as duas da paranoia
/// (adicional de 2026-10-01; testes próprios em ParanoiaPixelTestes): com os nomes que o
/// núcleo gera do enum <c>Gesto</c> em minúsculas, achadas por <see cref="PosesPixel.PorNome"/> (uma pose
/// ausente derruba o app: crítica, F5), no mapeamento do L11 e dentro do quadro com qualquer cara e efeito.
/// Depois da mescla com o T3, um teste da apresentação amarra o enum <c>Gesto</c> a esta lista.
/// </summary>
internal sealed class GestosPixelTestes
{
    /// <summary>Os gestos da onda, no fim do enum <c>Gesto</c> do núcleo (DEC-028), em minúsculas e na ordem.</summary>
    private static readonly string[] Gestos = ["soluco", "danca", "gargalhada", "espirro", "tosse", "tremedeira", "olharproteto", "agachar"];

    private static PosePixel Pose(string nome) => PosesPixel.Todas.First(p => p.Nome == nome);

    /// <summary>A pose sem o nome, o estado, a cara e o efeito: só o corpo.</summary>
    private static PosePixel Corpo(PosePixel p) => p with { Nome = "", Estado = "", Expressao = "neutro", EfeitoDaPose = EfeitoVisual.Nenhum, FaseDoEfeito = 0 };

    [Teste]
    public void OsGestosDaOndaTemPoseComONomeDoNucleo()
    {
        Afirmar.Sequencia(Gestos, PosesPixel.DosGestos.Select(p => p.Nome), "os nomes e a ordem do fim do enum Gesto");
        foreach (PosePixel pose in PosesPixel.DosGestos)
        {
            Afirmar.Igual(pose, PosesPixel.PorNome(pose.Nome), $"{pose.Nome}: achada pelo nome");
            Afirmar.Falso(PosesPixel.Todas.Any(p => p.Nome == pose.Nome) || UsosPixel.Poses.Any(p => p.Nome == pose.Nome), $"{pose.Nome}: não colide com outra pose");
            Afirmar.Verdadeiro(pose.Estado.StartsWith("gesto: ", StringComparison.Ordinal), $"{pose.Nome}: estado {pose.Estado}");
            Afirmar.Igual(Vista.Frente, pose.Vista, $"{pose.Nome}: de frente, no chão");
            Afirmar.Verdadeiro(pose.PesNoChao && pose.Borda is null && pose.Cipo is null && pose.Segura == Segura.Nada, $"{pose.Nome}: no chão, de mãos vazias");
            Afirmar.Verdadeiro(EfeitosPixel.Modificavel(pose), $"{pose.Nome}: o corpo reage à onda, como nos outros gestos");
            Afirmar.Verdadeiro(Rostos.Expressoes.ContainsKey(pose.Expressao), $"{pose.Nome}: cara {pose.Expressao}");
        }
        // As poses de antes continuam as mesmas: a folha nativa não ganha quadro.
        Afirmar.Igual(25, PosesPixel.Todas.Count, "poses de estado e gesto");
    }

    [Teste]
    public void SeguemOMapeamentoDaCritica()
    {
        PosePixel parado = Pose("parado");
        Afirmar.Igual(Corpo(Pose("brincando")), Corpo(PosesPixel.PorNome("danca")!), "a dança é o brincando");
        Afirmar.Igual(Pose("brincando").Expressao, PosesPixel.PorNome("danca")!.Expressao, "com a cara dele");
        Afirmar.Igual(Corpo(Pose("reagindo")), Corpo(PosesPixel.PorNome("gargalhada")!), "a gargalhada é o reagindo");
        Afirmar.Igual("rindo", PosesPixel.PorNome("gargalhada")!.Expressao, "rindo");
        foreach (string nome in new[] { "soluco", "espirro", "tosse" })
            Afirmar.Igual(Corpo(parado), Corpo(PosesPixel.PorNome(nome)!), $"{nome}: o corpo do parado");
        Afirmar.Igual(("surpreso", EfeitoVisual.Nenhum), (PosesPixel.PorNome("soluco")!.Expressao, PosesPixel.PorNome("soluco")!.EfeitoDaPose), "o soluço assusta");
        // A cara "tossindo", das passageiras, não estava em nenhum quadro (achado 4): agora é a da tosse e a do espirro.
        Afirmar.Igual(("tossindo", EfeitoVisual.Fumaca), (PosesPixel.PorNome("tosse")!.Expressao, PosesPixel.PorNome("tosse")!.EfeitoDaPose), "a tosse solta fumaça pela boca");
        Afirmar.Igual(("tossindo", EfeitoVisual.Poeira), (PosesPixel.PorNome("espirro")!.Expressao, PosesPixel.PorNome("espirro")!.EfeitoDaPose), "o espirro solta poeira pelo nariz");
        // A tremedeira é o parado deslocado 1 pixel: a apresentação alterna os dois quadros.
        PosePixel tremedeira = PosesPixel.PorNome("tremedeira")!;
        Afirmar.Igual(Corpo(parado) with { QuadrilX = parado.QuadrilX + 1 }, Corpo(tremedeira), "o corpo do parado, 1 pixel para o lado");
        Tela a = BonecoPixel.Desenhar(parado, tremedeira.Expressao), b = BonecoPixel.Desenhar(tremedeira);
        static bool Palha(Cor c) => c is Cor.Palha or Cor.PalhaEscura or Cor.PalhaClara;
        for (int y = 0; y < BonecoPixel.Lado; y++)
            for (int x = 0; x < BonecoPixel.Lado - 1; x++)
                // A trama da palha fica presa à grade do quadro: ela não anda com o chapéu.
                if (a[x, y] != b[x + 1, y] && !(Palha(a[x, y]) && Palha(b[x + 1, y])))
                    Afirmar.Falhar($"tremedeira: o pixel ({x + 1},{y}) não é o ({x},{y}) do parado ({b[x + 1, y]} contra {a[x, y]})");
    }

    [Teste]
    public void CabemNoQuadroComQualquerCaraEEfeito()
    {
        foreach (PosePixel pose in PosesPixel.DosGestos)
        {
            foreach (string cara in Rostos.Expressoes.Keys)
            {
                Tela t = BonecoPixel.Desenhar(pose, cara);
                (int e, int topo, int d, int b) = Afirmar.NaoNulo(t.Limites(), pose.Nome);
                Afirmar.Igual(0, t.ParaArgb().Count(p => (p >> 24) is not 0 and not 255), $"{pose.Nome}/{cara}: alfa só 0 ou 255");
                Afirmar.Verdadeiro(e >= 1 && d <= BonecoPixel.Lado - 1 && b <= BonecoPixel.Lado, $"{pose.Nome}/{cara}: encosta numa lateral ({e},{topo})-({d},{b})");
                for (int i = 0; i < BonecoPixel.Lado; i++)
                    Afirmar.Verdadeiro(t[i, 0] is Cor.Nada or Cor.Contorno, $"{pose.Nome}/{cara}: preenchimento na borda de cima em ({i},0)");
            }
            foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    foreach (string cara in new[] { pose.Expressao, "bebado", "eletrico", "tonto" })
                    {
                        Tela t = BonecoPixel.Desenhar(EfeitosPixel.Modificar(pose, efeito, fase), cara, null, efeito, fase);
                        (int e, int topo, int d, int _) = Afirmar.NaoNulo(t.Limites(), pose.Nome);
                        Afirmar.Verdadeiro(e >= 1 && d <= BonecoPixel.Lado - 1 && topo >= 0, $"{pose.Nome}/{cara}, {efeito} fase {fase}: encosta numa lateral");
                        for (int i = 0; i < BonecoPixel.Lado; i++)
                            Afirmar.Verdadeiro(t[i, 0] is Cor.Nada or Cor.Contorno, $"{pose.Nome}/{cara}, {efeito} fase {fase}: preenchimento na borda de cima em ({i},0)");
                    }
                }
            }
        }
    }

    [Teste]
    public void TodaCaraPassageiraApareceEmAlgumQuadro()
    {
        // Achado 4: a cara "tossindo" não era usada por nenhum quadro.
        HashSet<string> usadas = [.. UsosPixel.Poses.Concat(PosesPixel.DosGestos).Select(p => p.Expressao)];
        foreach (string cara in Rostos.Passageiras)
            Afirmar.Verdadeiro(usadas.Contains(cara), $"a cara passageira '{cara}' não aparece em nenhum quadro de uso ou de gesto");
    }
}
