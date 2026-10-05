using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Poses provisórias por estado (Fase 4) e o esticar e achatar de desenho animado da toon force
/// (DEC-023): a pose escolhida pelo retrato e pela dinâmica, e a deformação da pixel art, que
/// mantém os pés na mesma linha, o alfa só 0 ou 255 e os cantos transparentes.
/// </summary>
internal sealed class PoseTestes
{
    private static Retrato R(Estado estado, Direcao direcao = Direcao.Direita, Expressao expressao = Expressao.Neutro, Gesto gesto = Gesto.Nenhum, bool relogio = true)
        => new(estado, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), direcao, expressao, gesto, false, false, NivelDeEnergia.Media, relogio, Sinal.Nenhum);

    private static int[] Pixels(BitmapSource bmp)
    {
        int[] p = new int[bmp.PixelWidth * bmp.PixelHeight];
        bmp.CopyPixels(p, bmp.PixelWidth * 4, 0);
        return p;
    }

    private static byte Alfa(int pixel) => (byte)((uint)pixel >> 24);

    /// <summary>Retângulo dos pixels opacos: (esquerda, topo, direita exclusiva, base exclusiva).</summary>
    private static (int E, int T, int D, int B) Limites(BitmapSource bmp)
    {
        int[] p = Pixels(bmp);
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        int e = int.MaxValue, t = int.MaxValue, d = int.MinValue, b = int.MinValue;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (Alfa(p[y * w + x]) != 255) continue;
                e = Math.Min(e, x);
                t = Math.Min(t, y);
                d = Math.Max(d, x + 1);
                b = Math.Max(b, y + 1);
            }
        }
        return (e, t, d, b);
    }

    [Teste]
    public void Foguete_SobeEsticadoOlhandoParaAParede()
    {
        foreach (Direcao direcao in new[] { Direcao.Direita, Direcao.Esquerda })
        {
            QuadroDoSprite q = PoseDoPersonagem.Escolher(R(Estado.Climbing, direcao), 3, new Dinamica(-1000, 0, Foguete: true));
            Afirmar.Igual(Deformacao.Esticado, q.Deformacao, $"foguete {direcao}");
            Afirmar.Igual(direcao == Direcao.Esquerda, q.Espelhado, $"foguete {direcao}: olha para a parede");
            QuadroDoSprite normal = PoseDoPersonagem.Escolher(R(Estado.Climbing, direcao), 3, new Dinamica(-110, 0, Foguete: false));
            Afirmar.Igual(Deformacao.Nenhuma, normal.Deformacao, $"escalada normal {direcao}");
            Afirmar.Verdadeiro(normal.Pose.StartsWith("escalando-", StringComparison.Ordinal), $"escalada normal {direcao}: {normal.Pose}");
        }
    }

    [Teste]
    public void Quique_ComecaAchatadoNoChaoESobeEsticado()
    {
        QuadroDoSprite impacto = PoseDoPersonagem.Escolher(R(Estado.Jumping), 0, new Dinamica(-750, 1, false));
        Afirmar.Igual(Deformacao.Achatado, impacto.Deformacao, "o quique começa achatado");
        QuadroDoSprite subindo = PoseDoPersonagem.Escolher(R(Estado.Jumping), PoseDoPersonagem.PassosDoAchatamento, new Dinamica(-720, 1, false));
        Afirmar.Igual(("impulso", Deformacao.Esticado), (subindo.Pose, subindo.Deformacao), "depois sobe esticado");
        QuadroDoSprite noTopo = PoseDoPersonagem.Escolher(R(Estado.Jumping), 20, new Dinamica(-50, 1, false));
        Afirmar.Igual(Deformacao.Nenhuma, noTopo.Deformacao, "devagar, no alto do quique, o corpo volta ao normal");
    }

    [Teste]
    public void PousoAchataEQuedaRapidaEstica()
    {
        Afirmar.Igual(Deformacao.Achatado, PoseDoPersonagem.Escolher(R(Estado.Landing), 0).Deformacao, "o pouso começa achatado");
        Afirmar.Igual(Deformacao.Nenhuma, PoseDoPersonagem.Escolher(R(Estado.Landing), PoseDoPersonagem.PassosDoAchatamento).Deformacao, "e desamassa");
        Afirmar.Igual(Deformacao.Esticado, PoseDoPersonagem.Escolher(R(Estado.Falling), 30, new Dinamica(1200, 0, false)).Deformacao, "queda rápida estica");
        Afirmar.Igual(Deformacao.Nenhuma, PoseDoPersonagem.Escolher(R(Estado.Falling), 2, new Dinamica(100, 0, false)).Deformacao, "começo da queda, devagar");
        Afirmar.Igual(Deformacao.Nenhuma, PoseDoPersonagem.Escolher(R(Estado.Jumping), 2, new Dinamica(-900, 0, false)).Deformacao, "o impulso de um pulo comum não deforma");
    }

    [Teste]
    public void Cipo_BalancaAndandoEFicaNoMeioAgarrado()
    {
        string[] quadros = [.. Enumerable.Range(0, 8).Select(i => PoseDoPersonagem.Escolher(R(Estado.Hanging), i * 10).Pose)];
        Afirmar.Sequencia(new[] { "cipo-1", "cipo-2", "cipo-3", "cipo-2", "cipo-1", "cipo-2", "cipo-3", "cipo-2" }, quadros, "balanço: esquerda, meio, direita, meio");
        Afirmar.Igual("cipo-2", PoseDoPersonagem.Escolher(R(Estado.Hanging), 37, new Dinamica(0, 0, false, Agarrado: true)).Pose, "agarrado, parado no meio");
        Afirmar.Igual("escalando-1", PoseDoPersonagem.Escolher(R(Estado.Climbing), 37, new Dinamica(0, 0, false, Agarrado: true)).Pose, "grudado na parede, parado");
        Afirmar.Nulo(PoseDoPersonagem.Escolher(R(Estado.Hanging), 0).Expressao, "com a cara neutra, fica o riso da pose");
        Afirmar.Igual("curioso", PoseDoPersonagem.Escolher(R(Estado.Hanging, expressao: Expressao.Curioso), 0).Expressao, "outras caras aparecem no cipó");
    }

    [Teste]
    public void Cipo_EncostaNaBordaDeCimaEOCorpoNaoEncostaNasLaterais()
    {
        foreach (string pose in new[] { "cipo-1", "cipo-2", "cipo-3" })
        {
            foreach (bool espelhado in new[] { false, true })
            {
                BitmapSource bmp = SpriteProvisorio.Renderizar(new QuadroDoSprite(pose, espelhado, null), 96);
                int[] p = Pixels(bmp);
                int w = bmp.PixelWidth, h = bmp.PixelHeight;
                string onde = $"{pose}{(espelhado ? " espelhado" : "")}";
                // Os pés podem tocar a linha de baixo, como em toda pose (a âncora fica ali).
                Afirmar.Verdadeiro(Enumerable.Range(0, w).Any(x => Alfa(p[x]) == 255), $"{onde}: o cipó encosta na borda de cima, onde se prende na tela");
                Afirmar.Verdadeiro(Enumerable.Range(0, h).All(y => Alfa(p[y * w]) == 0 && Alfa(p[y * w + w - 1]) == 0), $"{onde}: nada encosta nas laterais");
                Afirmar.Igual(0, p.Count(x => Alfa(x) is not 0 and not 255), $"{onde}: alfa só 0 ou 255");
            }
        }
    }

    [Teste]
    public void Escondido_SoACabecaEAsMaosAparecemJuntoABordaDoEsconderijo()
    {
        foreach ((LadoDoEsconderijo lado, Giro giro) in new[] { (LadoDoEsconderijo.Baixo, Giro.Nenhum), (LadoDoEsconderijo.Esquerda, Giro.Horario), (LadoDoEsconderijo.Direita, Giro.AntiHorario), (LadoDoEsconderijo.Cima, Giro.MeiaVolta) })
        {
            var dinamica = new Dinamica(0, 0, false, Esconderijo: lado);
            foreach (Estado estado in new[] { Estado.Peeking, Estado.Reacting, Estado.Pressed })
            {
                QuadroDoSprite q = PoseDoPersonagem.Escolher(R(estado), 0, dinamica);
                Afirmar.Igual(("escondido", giro), (q.Pose, q.Giro), $"{lado}, {estado}: a pose do esconderijo, sem o corpo aparecer de relance");
            }
            BitmapSource bmp = SpriteProvisorio.Renderizar(PoseDoPersonagem.Escolher(R(Estado.Peeking), 0, dinamica), 96);
            (int e, int t, int d, int b) = Limites(bmp);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            bool certo = lado switch
            {
                LadoDoEsconderijo.Baixo => b == h && t > h / 2,
                LadoDoEsconderijo.Esquerda => e == 0 && d < w / 2,
                LadoDoEsconderijo.Cima => t == 0 && b < h / 2,
                _ => d == w && e > w / 2,
            };
            Afirmar.Verdadeiro(certo, $"{lado}: só a cabeça e as mãos, encostadas na borda do esconderijo ({e},{t})-({d},{b}) num quadro de {w}×{h}");
        }
        Afirmar.Igual("surpreso", PoseDoPersonagem.Escolher(R(Estado.Pressed), 0, new Dinamica(0, 0, false, Esconderijo: LadoDoEsconderijo.Baixo)).Expressao, "pressionado no esconderijo, cara de surpresa");
    }

    [Teste]
    public void TodaPoseEscolhidaExisteNaPixelArtComQualquerDeformacao()
    {
        var dinamicas = new[] { default(Dinamica), new Dinamica(1200, 0, false), new Dinamica(-800, 1, false), new Dinamica(-1000, 0, true) };
        foreach (Estado estado in Enum.GetValues<Estado>())
        {
            foreach (Dinamica dinamica in dinamicas)
            {
                foreach (long passos in new long[] { 0, 3, 7, 40 })
                {
                    QuadroDoSprite q = PoseDoPersonagem.Escolher(R(estado, Direcao.Esquerda), passos, dinamica);
                    BitmapSource bmp = SpriteProvisorio.Renderizar(q, 96);
                    Afirmar.Igual(128, bmp.PixelWidth, $"{estado} {dinamica} {passos}: mesmo tamanho de janela");
                }
            }
        }

        // Tamagotchi (DEC-028; crítica, F5): uma chave ausente na arte derrubaria o app. Todo quadro que a apresentação
        // escolhe — em USING, cada verbo, cada passo e cada apoio; todas as caras; todos os gestos; com e sem onda —
        // existe na pixel art e se desenha.
        HashSet<QuadroDoSprite> quadros = [.. TodosOsQuadrosEscolhidos()];
        foreach (QuadroDoSprite q in quadros)
        {
            Tela t = SpriteProvisorio.Compor(q);
            Afirmar.Igual((BonecoPixel.Lado, BonecoPixel.Lado), (t.Largura, t.Altura), $"{q}: o quadro de 64 × 64");
            Afirmar.Verdadeiro(t.Limites() is not null, $"{q}: desenha alguma coisa");
        }

        // A enumeração passou mesmo por tudo: toda pose de uso e de gesto (um gesto do núcleo sem caso no switch da
        // apresentação cairia no "parado" e ficaria vermelho aqui), toda cara, todo item, toda sobreposição da onda e as
        // três fases, e o giro do esconderijo.
        HashSet<string> poses = [.. quadros.Select(q => q.Pose)];
        foreach (string nome in UsosPixel.Poses.Concat(PosesPixel.DosGestos).Select(p => p.Nome))
            Afirmar.Verdadeiro(poses.Contains(nome), $"a pose {nome} nunca foi escolhida");
        HashSet<string?> caras = [.. quadros.Select(q => q.Expressao)];
        foreach (Expressao e in Enum.GetValues<Expressao>())
            Afirmar.Verdadeiro(caras.Contains(PoseDoPersonagem.NomeDaExpressao(e)), $"a cara {e} nunca foi pedida");
        Afirmar.Sequencia(ItensPixel.Todos.Order(StringComparer.Ordinal), quadros.Select(q => q.Item).OfType<string>().Distinct().Order(StringComparer.Ordinal), "todo item na mão");
        Afirmar.Sequencia(
            new[] { EfeitoVisual.Nenhum, EfeitoVisual.Fumaca, EfeitoVisual.Bolhas, EfeitoVisual.Brilhos, EfeitoVisual.Estrelinhas, EfeitoVisual.Coracoes, EfeitoVisual.Cores, EfeitoVisual.Suor },
            quadros.Select(q => q.Efeito).Distinct().Order(), "as sobreposições das ondas");
        // A paranoia (adicional de 2026-10-01, DEC-028), como ela aparece: os dois gestos dela, com a cara da pose, e o
        // parado e a caminhada com a cara paranoica, todos com o suor por cima, nas três fases.
        foreach (string pose in new[] { "olharproteto", "agachar" })
            Afirmar.Sequencia(new[] { 0, 1, 2 }, quadros.Where(q => q.Pose == pose && q.Expressao is null && q.Efeito == EfeitoVisual.Suor).Select(q => q.Fase).Distinct().Order(),
                $"{pose}, com a cara da pose e o suor, nas três fases");
        Afirmar.Verdadeiro(quadros.Any(q => q.Pose == "parado" && q.Expressao == "paranoico" && q.Efeito == EfeitoVisual.Suor), "parado, com a cara paranoica e o suor");
        Afirmar.Verdadeiro(quadros.Any(q => q.Pose.StartsWith("andando-", StringComparison.Ordinal) && q.Expressao == "paranoico" && q.Efeito == EfeitoVisual.Suor),
            "andando, com a cara paranoica e o suor");
        Afirmar.Sequencia(new[] { 0, 1, 2 }, quadros.Select(q => q.Fase).Distinct().Order(), "as três fases");
        Afirmar.Sequencia(Enum.GetValues<Giro>(), quadros.Select(q => q.Giro).Distinct().Order(), "os giros do esconderijo");
        Console.WriteLine($"         {quadros.Count} quadros distintos conferidos");
    }

    /// <summary>
    /// Os quadros que a apresentação escolhe em todos os estados, direções, dinâmicas, caras e gestos, com e sem onda e
    /// com o relógio ligado e parado; e, em USING, para cada item, cada apoio e cada passo do uso (no chão, todos).
    /// </summary>
    private static IEnumerable<QuadroDoSprite> TodosOsQuadrosEscolhidos()
    {
        Dinamica[] esconderijos =
        [
            new(0, 0, false, Esconderijo: LadoDoEsconderijo.Baixo),
            new(0, 0, false, Esconderijo: LadoDoEsconderijo.Esquerda),
            new(0, 0, false, Esconderijo: LadoDoEsconderijo.Direita),
            new(0, 0, false, Esconderijo: LadoDoEsconderijo.Cima),
        ];
        Dinamica[] dinamicas = [default, new(1200, 0, false), new(-800, 1, false), new(-1000, 0, true), new(0, 0, false, Agarrado: true), .. esconderijos];
        long[] passos = [0, 3, 7, 12, 25, 40];
        EstadoDaOnda?[] ondas = [null, .. Enum.GetValues<Onda>().Select(o => new EstadoDaOnda(o, FaseDaOnda.Pico, 1, 1))];
        // Cada onda em cada fase dela, com a cara da fase (tabela 4.4), como o núcleo a põe: a cara paranoica com o suor
        // da paranoia por cima, por exemplo.
        (EstadoDaOnda Onda, Expressao Cara)[] fasesComCara =
        [
            .. Enum.GetValues<Onda>().SelectMany(o => Enum.GetValues<FaseDaOnda>()
                .Where(f => f != FaseDaOnda.Queda || TabelaDoTamagotchi.DaOnda(o).Queda is not null)
                .Select(f => (new EstadoDaOnda(o, f, 1, 1), TabelaDoTamagotchi.DaOnda(o).Cara(f)))),
        ];
        foreach (Estado estado in Enum.GetValues<Estado>())
        {
            Gesto[] gestos = estado == Estado.Idle ? Enum.GetValues<Gesto>() : [Gesto.Nenhum];
            foreach (Direcao direcao in Enum.GetValues<Direcao>())
                foreach (Gesto gesto in gestos)
                    foreach (Dinamica dinamica in dinamicas)
                        foreach (long p in passos)
                            foreach (bool relogio in new[] { false, true })
                            {
                                foreach (Expressao expressao in Enum.GetValues<Expressao>())
                                    yield return PoseDoPersonagem.Escolher(R(estado, direcao, expressao, gesto, relogio), p, dinamica);
                                foreach (EstadoDaOnda? onda in ondas)
                                    yield return PoseDoPersonagem.Escolher(R(estado, direcao, Expressao.Bebado, gesto, relogio) with { Onda = onda }, p, dinamica);
                                foreach ((EstadoDaOnda onda, Expressao cara) in fasesComCara)
                                    yield return PoseDoPersonagem.Escolher(R(estado, direcao, cara, gesto, relogio) with { Onda = onda }, p, dinamica);
                            }
        }

        foreach (Item item in Enum.GetValues<Item>())
        {
            DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
            foreach (ApoioDoUso apoio in Enum.GetValues<ApoioDoUso>())
            {
                Dinamica[] dinamicasDoApoio = apoio == ApoioDoUso.Esconderijo ? esconderijos : [default, new(0, 0, false, Agarrado: true)];
                foreach (Direcao direcao in Enum.GetValues<Direcao>())
                    foreach (EstadoDaOnda? onda in ondas)
                        foreach (Dinamica dinamica in dinamicasDoApoio)
                            for (int passo = 0; passo < dados.PassosDoUso; passo += apoio == ApoioDoUso.Chao ? 1 : 12)
                            {
                                Retrato r = R(Estado.Using, direcao, dados.CaraDurante) with
                                {
                                    Uso = new Uso(item, dados.Verbo, dados.PassosDoUso, apoio),
                                    PassoDoUso = passo,
                                    Onda = onda,
                                };
                                yield return PoseDoPersonagem.Escolher(r, passo, dinamica);
                            }
            }
        }
    }

    [Teste]
    public void AchatarEEsticarMantemOsPesOAlfaBinarioEOsCantosTransparentes()
    {
        foreach (string pose in new[] { "pousando", "no-ar", "caindo", "parado", "impulso" })
        {
            foreach (int dpi in new[] { 96, 144, 192 })
            {
                (int eN, int tN, int dN, int bN) = Limites(SpriteProvisorio.Renderizar(new QuadroDoSprite(pose, false, null), dpi));
                foreach (Deformacao d in new[] { Deformacao.Achatado, Deformacao.Esticado })
                {
                    BitmapSource bmp = SpriteProvisorio.Renderizar(new QuadroDoSprite(pose, false, null, d), dpi);
                    int[] p = Pixels(bmp);
                    string onde = $"{pose} {d} em {dpi} DPI";
                    Afirmar.Igual(0, p.Count(x => Alfa(x) is not 0 and not 255), $"{onde}: alfa só 0 ou 255");
                    (int e, int t, int dd, int b) = Limites(bmp);
                    Afirmar.Igual(bN, b, $"{onde}: os pés continuam na mesma linha");
                    Afirmar.Verdadeiro(e > 0 && t > 0 && dd < bmp.PixelWidth, $"{onde}: o desenho não encosta nas bordas de cima e dos lados ({e},{t},{dd},{b})");
                    int w = bmp.PixelWidth, h = bmp.PixelHeight, bloco = Math.Max(3, w / 20);
                    foreach ((int x0, int y0) in new[] { (0, 0), (w - bloco, 0), (0, h - bloco), (w - bloco, h - bloco) })
                        for (int y = y0; y < y0 + bloco; y++)
                            for (int x = x0; x < x0 + bloco; x++)
                                Afirmar.Igual((byte)0, Alfa(p[y * w + x]), $"{onde}: canto ({x},{y}) transparente");
                    if (d == Deformacao.Achatado)
                        Afirmar.Verdadeiro(dd - e > dN - eN && b - t < bN - tN, $"{onde}: mais largo e mais baixo ({dd - e}x{b - t} contra {dN - eN}x{bN - tN})");
                    else // Uma pose que já ocupa a altura do quadro só afina: a margem de cima vale mais.
                        Afirmar.Verdadeiro(dd - e < dN - eN && b - t >= bN - tN, $"{onde}: mais estreito e não mais baixo ({dd - e}x{b - t} contra {dN - eN}x{bN - tN})");
                }
            }
        }
    }
}
