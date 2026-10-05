using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Sobreposições de efeito do tamagotchi (DEC-028, passo A4): 9 efeitos de desenho animado em 3
/// fases (a 0 é a parada, que vale sem relógio), desenhados por cima do boneco, nunca sobre os olhos e
/// a boca e sempre dentro do quadro; e os modificadores de pose, que mexem só no corpo. O suor (a onda
/// Paranoico, adicional de 2026-10-01) entrou por último; os testes próprios dele estão em ParanoiaPixelTestes.
/// </summary>
internal sealed class EfeitosPixelTestes
{
    private static readonly EfeitoVisual[] Nove =
    [
        EfeitoVisual.Fumaca, EfeitoVisual.Bolhas, EfeitoVisual.Brilhos, EfeitoVisual.Estrelinhas,
        EfeitoVisual.Coracoes, EfeitoVisual.Cores, EfeitoVisual.Poeira, EfeitoVisual.Borrifo,
        EfeitoVisual.Suor,
    ];

    private static PosePixel Pose(string nome) => PosesPixel.Todas.First(p => p.Nome == nome);

    /// <summary>As poses de estado e gesto (sem o cipó, o único desenho que encosta numa borda) e as de uso, com um item do verbo.</summary>
    private static IEnumerable<(string Nome, PosePixel Pose, string? Item)> PosesParaConferir()
    {
        foreach (PosePixel p in PosesPixel.Todas) yield return (p.Nome, p with { Cipo = null }, null);
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            string item = ItensPixel.DoVerbo(verbo)[^1];
            foreach (PosePixel p in UsosPixel.Sequencia(verbo).Select(q => q.Pose).DistinctBy(p => p.Nome))
                yield return ($"{p.Nome}/{item}", p, item);
        }
    }

    private static int Diferencas(Tela a, Tela b) => a.ParaArgb().Zip(b.ParaArgb()).Count(p => p.First != p.Second);

    private static int Contar(Tela t, Cor cor) => t.ParaArgb().Count(p => p == Paleta.Argb(cor));

    [Teste]
    public void SaoNoveEfeitosComTresFases()
    {
        Afirmar.Igual(3, EfeitosPixel.Fases);
        Afirmar.Sequencia(Nove, EfeitosPixel.Todos);
        // Todo valor do enum, menos o Nenhum, desenha alguma coisa, na mesma ordem.
        Afirmar.Sequencia(Enum.GetValues<EfeitoVisual>().Where(e => e != EfeitoVisual.Nenhum), EfeitosPixel.Todos, "o enum inteiro");
    }

    [Teste]
    public void CadaEfeitoDesenhaEmCadaFaseComAsCoresDele()
    {
        var cores = new Dictionary<EfeitoVisual, Cor[]>
        {
            [EfeitoVisual.Fumaca] = [Cor.Fumaca],
            [EfeitoVisual.Bolhas] = [Cor.AguaClara],
            [EfeitoVisual.Brilhos] = [Cor.Estrela],
            [EfeitoVisual.Estrelinhas] = [Cor.Estrela, Cor.EstrelaEscura],
            [EfeitoVisual.Coracoes] = [Cor.Coracao],
            [EfeitoVisual.Cores] = [Cor.Rosa, Cor.Neon, Cor.Lilas],
            [EfeitoVisual.Poeira] = [Cor.Branco],
            [EfeitoVisual.Borrifo] = [Cor.AguaClara],
            [EfeitoVisual.Suor] = [Cor.AguaClara, Cor.Agua],
        };
        PosePixel parado = Pose("parado");
        Tela sem = BonecoPixel.Desenhar(parado);
        foreach (EfeitoVisual efeito in Nove)
        {
            for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
            {
                Tela com = BonecoPixel.Desenhar(parado, efeito: efeito, fase: fase);
                Afirmar.Verdadeiro(Diferencas(com, sem) >= 8, $"{efeito} fase {fase}: desenha alguma coisa");
                foreach (Cor cor in cores[efeito])
                    Afirmar.Verdadeiro(Contar(com, cor) > Contar(sem, cor), $"{efeito} fase {fase}: tem {cor}");
            }
        }
    }

    [Teste]
    public void AsTresFasesSaoDiferentesECiclicas()
    {
        PosePixel parado = Pose("parado");
        foreach (EfeitoVisual efeito in Nove)
        {
            Tela[] fases = [.. Enumerable.Range(0, EfeitosPixel.Fases).Select(f => BonecoPixel.Desenhar(parado, efeito: efeito, fase: f))];
            for (int a = 0; a < fases.Length; a++)
                for (int b = a + 1; b < fases.Length; b++)
                    Afirmar.Verdadeiro(Diferencas(fases[a], fases[b]) > 0, $"{efeito}: as fases {a} e {b} são iguais");
            Afirmar.Igual(0, Diferencas(fases[0], BonecoPixel.Desenhar(parado, efeito: efeito, fase: EfeitosPixel.Fases)), $"{efeito}: a fase 3 volta à 0");
            Afirmar.Igual(0, Diferencas(fases[2], BonecoPixel.Desenhar(parado, efeito: efeito, fase: -1)), $"{efeito}: a fase −1 é a 2");
            Afirmar.Igual(0, Diferencas(fases[0], BonecoPixel.Desenhar(parado, efeito: efeito, fase: 0)), $"{efeito}: a fase parada é sempre a mesma");
        }
    }

    [Teste]
    public void NenhumNaoMudaNada()
    {
        foreach ((string nome, PosePixel pose, string? item) in PosesParaConferir())
            Afirmar.Igual(0, Diferencas(BonecoPixel.Desenhar(pose, null, item), BonecoPixel.Desenhar(pose, null, item, EfeitoVisual.Nenhum, 2)), nome);
    }

    [Teste]
    public void NuncaCobremOsOlhosNemABoca()
    {
        foreach ((string nome, PosePixel pose, string? item) in PosesParaConferir())
        {
            if (pose.Borda is not null) continue;
            Tela sem = BonecoPixel.Desenhar(pose, null, item);
            IReadOnlySet<(int X, int Y)> rosto = EfeitosPixel.AreaDoRosto(pose);
            Afirmar.Verdadeiro(rosto.Count >= 80, $"{nome}: a área do rosto cobre olhos, nariz e boca ({rosto.Count} pixels)");
            foreach (EfeitoVisual efeito in Nove)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    Tela com = BonecoPixel.Desenhar(pose, null, item, efeito, fase);
                    foreach ((int x, int y) in rosto)
                        if (com[x, y] != sem[x, y])
                            Afirmar.Falhar($"{nome}, {efeito} fase {fase}: o pixel ({x},{y}) do rosto mudou de {sem[x, y]} para {com[x, y]}");
                }
            }
        }
    }

    [Teste]
    public void AreaDoRostoCobreOsCarimbosDoRosto()
    {
        // A área protegida é uma máscara dos traços (achado 7 da revisão), não um retângulo: os pixels que
        // mudam entre duas caras do mesmo topete (olhos, sobrancelhas, nariz, rubor e boca) ficam todos nela.
        // O chapéu e o tufo mudam com o topete e ficam de fora. A área depende da cara só na que tem gota de suor
        // (a paranoica): a diferença entre duas caras fica na união das áreas delas.
        foreach (PosePixel pose in PosesPixel.Todas.Where(p => p.Borda is null))
        {
            PosePixel semCipo = pose with { Cipo = null };
            foreach (IGrouping<Topete, string> grupo in Rostos.Expressoes.Keys.GroupBy(c => Rostos.Expressoes[c].Topete))
            {
                Tela referencia = BonecoPixel.Desenhar(semCipo, grupo.First());
                foreach (string cara in grupo.Skip(1))
                {
                    Tela outra = BonecoPixel.Desenhar(semCipo, cara);
                    var rosto = new HashSet<(int X, int Y)>(EfeitosPixel.AreaDoRosto(pose, grupo.First()));
                    rosto.UnionWith(EfeitosPixel.AreaDoRosto(pose, cara));
                    for (int y = 0; y < BonecoPixel.Lado; y++)
                        for (int x = 0; x < BonecoPixel.Lado; x++)
                            if (referencia[x, y] != outra[x, y] && !rosto.Contains((x, y)))
                                Afirmar.Falhar($"{pose.Nome}: {grupo.First()} e {cara} diferem em ({x},{y}), fora da área do rosto");
                }
            }
        }
    }

    [Teste]
    public void DeFrenteAFumacaEAsBolhasSaemDoCantoDaBoca()
    {
        // Achado 7: de frente, a fumaça e as bolhas saíam da orelha e liam como vapor de raiva e gotas de
        // suor. Na fase 0, um carimbo começa no canto da boca: a 3 pixels da boca, à direita, na altura dela.
        foreach (string nome in new[] { "parado", "sentado", "brincando", "olhando" })
        {
            PosePixel pose = Pose(nome);
            Tela sem = BonecoPixel.Desenhar(pose);
            PontosDoEsqueleto pontos = BonecoPixel.Pontos(pose);
            foreach (EfeitoVisual efeito in new[] { EfeitoVisual.Fumaca, EfeitoVisual.Bolhas })
            {
                Tela com = BonecoPixel.Desenhar(pose, efeito: efeito);
                bool noCanto = false;
                for (int y = (int)pontos.Boca.Y - 1; y <= (int)pontos.Boca.Y + 2; y++)
                    for (int x = (int)pontos.Boca.X + 5; x <= (int)pontos.Boca.X + 9; x++)
                        noCanto |= com[x, y] != sem[x, y] && com[x, y] != Cor.Contorno;
                Afirmar.Verdadeiro(noCanto, $"{nome}, {efeito}: nada sai do canto da boca {pontos.Boca}");
            }
        }
    }

    [Teste]
    public void NoCipoOsEfeitosNaoCobremOCipoNemAMaoQueOSegura()
    {
        // Achado 7: no cipó, uma estrela ou um coração cobria a mão agarrada ao cipó. O cipó, as folhas e a
        // mão que o segura ficam sempre à vista, com qualquer efeito, em qualquer fase.
        foreach (PosePixel pose in PosesPixel.Todas.Where(p => p.Cipo is not null))
        {
            Tela sem = BonecoPixel.Desenhar(pose);
            (double mx, double my) = BonecoPixel.Pontos(pose).MaoB;
            bool AVista(int x, int y)
                => sem[x, y] is Cor.Cipo or Cor.CipoEscuro or Cor.CipoClaro or Cor.Folha or Cor.FolhaEscura
                   || (sem[x, y] is Cor.Creme or Cor.CremeSombra && (x + 0.5 - mx) * (x + 0.5 - mx) + (y + 0.5 - my) * (y + 0.5 - my) <= 9);
            // (Em cipo-1 a aba do chapéu esconde quase toda a mão; sobram 3 pixels.)
            Afirmar.Verdadeiro(Enumerable.Range(0, BonecoPixel.Lado * BonecoPixel.Lado).Count(i => sem[i % BonecoPixel.Lado, i / BonecoPixel.Lado] is Cor.Creme or Cor.CremeSombra && AVista(i % BonecoPixel.Lado, i / BonecoPixel.Lado)) >= 3, $"{pose.Nome}: a mão no cipó");
            foreach (EfeitoVisual efeito in Nove)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    Tela com = BonecoPixel.Desenhar(pose, null, null, efeito, fase);
                    for (int y = 0; y < BonecoPixel.Lado; y++)
                        for (int x = 0; x < BonecoPixel.Lado; x++)
                            if (AVista(x, y) && com[x, y] != sem[x, y])
                                Afirmar.Falhar($"{pose.Nome}, {efeito} fase {fase}: o efeito cobre o cipó ou a mão em ({x},{y})");
                }
            }
        }
    }

    [Teste]
    public void FicamDentroDoQuadroComAlfaBinario()
    {
        foreach ((string nome, PosePixel pose, string? item) in PosesParaConferir())
        {
            Tela sem = BonecoPixel.Desenhar(pose, null, item);
            foreach (EfeitoVisual efeito in Nove)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    Tela com = BonecoPixel.Desenhar(pose, null, item, efeito, fase);
                    string onde = $"{nome}, {efeito} fase {fase}";
                    Afirmar.Igual(0, com.ParaArgb().Count(p => (p >> 24) is not 0 and not 255), $"{onde}: alfa só 0 ou 255");
                    for (int y = 0; y < BonecoPixel.Lado; y++)
                        for (int x = 0; x < BonecoPixel.Lado; x++)
                            if (com[x, y] != sem[x, y] && (x < 1 || y < 1 || x > BonecoPixel.Lado - 2 || y > BonecoPixel.Lado - 2))
                                Afirmar.Falhar($"{onde}: o efeito encosta na borda do quadro em ({x},{y})");
                }
            }
        }
    }

    /// <summary>As cores dos carimbos de cada efeito.</summary>
    private static readonly Dictionary<EfeitoVisual, Cor[]> CoresDoEfeito = new()
    {
        [EfeitoVisual.Fumaca] = [Cor.Fumaca, Cor.PapelSombra],
        [EfeitoVisual.Bolhas] = [Cor.AguaClara, Cor.Branco, Cor.Agua],
        [EfeitoVisual.Brilhos] = [Cor.Estrela, Cor.Branco],
        [EfeitoVisual.Estrelinhas] = [Cor.Estrela, Cor.EstrelaEscura],
        [EfeitoVisual.Coracoes] = [Cor.Coracao, Cor.Branco, Cor.Faixa],
        [EfeitoVisual.Cores] = [Cor.Rosa, Cor.Lilas, Cor.Neon, Cor.Agua, Cor.Branco],
        [EfeitoVisual.Poeira] = [Cor.Branco, Cor.PapelSombra],
        [EfeitoVisual.Borrifo] = [Cor.AguaClara, Cor.Agua],
        [EfeitoVisual.Suor] = [Cor.AguaClara, Cor.Agua, Cor.Branco],
    };

    [Teste]
    public void OEfeitoTemUmaLinhaDeContornoOndeEncostaNoCorpo()
    {
        // Achado 6 da revisão: a sobreposição nunca encosta direto no corpo — todo pixel de preenchimento
        // dela que toca um pixel opaco que não mudou toca a linha de contorno. (Um vizinho com uma cor do
        // próprio efeito pode ser parte dele, por cima de um pixel da mesma cor: a fumaça sobre o papel.)
        foreach ((string nome, PosePixel pose, string? item) in PosesParaConferir())
        {
            Tela sem = BonecoPixel.Desenhar(pose, null, item);
            foreach (EfeitoVisual efeito in Nove)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    Tela com = BonecoPixel.Desenhar(pose, null, item, efeito, fase);
                    for (int y = 0; y < BonecoPixel.Lado; y++)
                    {
                        for (int x = 0; x < BonecoPixel.Lado; x++)
                        {
                            if (com[x, y] == sem[x, y] || com[x, y] == Cor.Contorno) continue;
                            foreach ((int qx, int qy) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                                if (com.Opaco(qx, qy) && com[qx, qy] == sem[qx, qy] && com[qx, qy] != Cor.Contorno && !CoresDoEfeito[efeito].Contains(com[qx, qy]))
                                    Afirmar.Falhar($"{nome}, {efeito} fase {fase}: o efeito em ({x},{y}) encosta em {com[qx, qy]} ({qx},{qy}) sem linha");
                        }
                    }
                }
            }
        }
    }

    [Teste]
    public void DePerfilAFumacaEAsBolhasSaemDaFrenteDoRosto()
    {
        // Achado 6: de perfil, a fumaça e as bolhas saem da boca e a poeira do nariz, à frente do rosto (o
        // perfil olha para a direita), nunca da nuca.
        foreach (PosePixel pose in PosesPixel.Todas.Where(p => p.Vista == Vista.Perfil))
        {
            Tela sem = BonecoPixel.Desenhar(pose);
            double cabeca = BonecoPixel.Pontos(pose).Cabeca.X;
            foreach (EfeitoVisual efeito in new[] { EfeitoVisual.Fumaca, EfeitoVisual.Bolhas, EfeitoVisual.Poeira })
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    Tela com = BonecoPixel.Desenhar(pose, efeito: efeito, fase: fase);
                    List<int> xs = [.. Enumerable.Range(0, BonecoPixel.Lado * BonecoPixel.Lado)
                        .Where(i => com[i % BonecoPixel.Lado, i / BonecoPixel.Lado] != sem[i % BonecoPixel.Lado, i / BonecoPixel.Lado] && com[i % BonecoPixel.Lado, i / BonecoPixel.Lado] != Cor.Contorno)
                        .Select(i => i % BonecoPixel.Lado)];
                    Afirmar.Verdadeiro(xs.Count > 0, $"{pose.Nome}, {efeito} fase {fase}: desenha");
                    Afirmar.Verdadeiro(xs.Average() > cabeca + 4, $"{pose.Nome}, {efeito} fase {fase}: o meio do efeito (x = {xs.Average():0.0}) fica atrás da frente do rosto (cabeça em {cabeca:0.0})");
                }
            }
        }
    }

    [Teste]
    public void SaoDeterministicos()
    {
        foreach (EfeitoVisual efeito in Nove)
            for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                Afirmar.Sequencia(BonecoPixel.Desenhar(Pose("andando-2"), efeito: efeito, fase: fase).ParaArgb(), BonecoPixel.Desenhar(Pose("andando-2"), efeito: efeito, fase: fase).ParaArgb(), $"{efeito} {fase}");
    }

    [Teste]
    public void ModificarSegueATabela()
    {
        PosePixel parado = Pose("parado");
        for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
        {
            PosePixel bebado = EfeitosPixel.Modificar(parado, EfeitoVisual.Bolhas, fase);
            double lado = 1 - fase;
            Afirmar.Igual((parado.Tronco + 6 * lado, parado.Cabeca + 4 * lado), (bebado.Tronco, bebado.Cabeca), $"bêbado balança, fase {fase}");
            Afirmar.Igual(parado.CabecaDescida + 1.5, EfeitosPixel.Modificar(parado, EfeitoVisual.Fumaca, fase).CabecaDescida, $"chapado abaixa a cabeça, fase {fase}");
            PosePixel tonto = EfeitosPixel.Modificar(parado, EfeitoVisual.Estrelinhas, fase);
            Afirmar.Igual((parado.Cabeca + new[] { -5.0, 5, 0 }[fase], parado.CabecaDescida + new[] { 0.0, 0, 1 }[fase]), (tonto.Cabeca, tonto.CabecaDescida), $"tonto dá uma volta com a cabeça, fase {fase}");
            // O elétrico treme de lado e o apaixonado balança, sem subir: o chapéu eriçado já fica a 1
            // pixel da borda de cima no parado.
            PosePixel eletrico = EfeitosPixel.Modificar(parado, EfeitoVisual.Brilhos, fase);
            Afirmar.Igual((parado.QuadrilX + new[] { 0.0, 1, -1 }[fase], parado.QuadrilY, Cauda.Alta), (eletrico.QuadrilX, eletrico.QuadrilY, eletrico.Cauda), $"elétrico treme e ergue a cauda, fase {fase}");
            PosePixel apaixonado = EfeitosPixel.Modificar(parado, EfeitoVisual.Coracoes, fase);
            Afirmar.Igual((parado.Tronco + new[] { -3.0, 0, 3 }[fase], parado.QuadrilY, Cauda.Alta), (apaixonado.Tronco, apaixonado.QuadrilY, apaixonado.Cauda), $"apaixonado balança, fase {fase}");
            Afirmar.Igual(parado.Cabeca + new[] { -4.0, 4, 0 }[fase], EfeitosPixel.Modificar(parado, EfeitoVisual.Cores, fase).Cabeca, $"viajando balança a cabeça, fase {fase}");
            Afirmar.Igual(parado with { QuadrilX = parado.QuadrilX + new[] { 0.0, -1, 1 }[fase] }, EfeitosPixel.Modificar(parado, EfeitoVisual.Suor, fase), $"o paranoico treme 1 pixel, fase {fase}");
            foreach (EfeitoVisual sem in new[] { EfeitoVisual.Nenhum, EfeitoVisual.Poeira, EfeitoVisual.Borrifo })
                Afirmar.Igual(parado, EfeitosPixel.Modificar(parado, sem, fase), $"{sem} não mexe na pose");
        }
        Afirmar.Igual(Cauda.PerfilAlta, EfeitosPixel.Modificar(Pose("andando-1"), EfeitoVisual.Brilhos, 0).Cauda, "de perfil, a cauda alta de perfil");
    }

    [Teste]
    public void OsBalancosSaoEquilibradosESemSaltoGrande()
    {
        // Revisão da arte, achado 10: com +6, −6, +6, o bêbado ficava dois terços do tempo do mesmo lado
        // (mancando), e o tonto saltava de +8° para −8°. Cada balanço, somado nas três fases, volta ao meio,
        // e de uma fase para a seguinte (a 2 volta à 0) nada gira mais de 12°.
        PosePixel parado = Pose("parado");
        foreach (EfeitoVisual efeito in Nove)
        {
            foreach ((string parte, Func<PosePixel, double> angulo) in new (string, Func<PosePixel, double>)[] { ("tronco", p => p.Tronco), ("cabeça", p => p.Cabeca) })
            {
                double[] d = [.. Enumerable.Range(0, EfeitosPixel.Fases).Select(f => angulo(EfeitosPixel.Modificar(parado, efeito, f)) - angulo(parado))];
                Afirmar.Aproximado(0, d.Sum(), 1e-9, $"{efeito}, {parte}: o balanço não volta ao meio ({string.Join(", ", d)})");
                for (int f = 0; f < d.Length; f++)
                    Afirmar.Verdadeiro(Math.Abs(d[f] - d[(f + 1) % d.Length]) <= 12, $"{efeito}, {parte}: salto de {d[f]}° para {d[(f + 1) % d.Length]}°");
            }
        }
    }

    [Teste]
    public void ModificarSoValeNoChao()
    {
        // No chão (parado, andando, descansando e nos gestos fora do espiar) o corpo reage à onda; na
        // parede, no cipó, no ar, segurado, no esconderijo e nas poses de uso, a pose fica como está.
        string[] noChao = ["parado", "sentado", "dormindo", "olhando", "cocando", "espreguicando", "brincando", "andando-1", "andando-2", "andando-3", "andando-4"];
        Afirmar.Sequencia(noChao, PosesPixel.Todas.Where(EfeitosPixel.Modificavel).Select(p => p.Nome));
        // Mesmo num estado do chão, o cipó, a borda e o item na mão desligam o modificador.
        PosePixel parado = Pose("parado");
        Afirmar.Falso(EfeitosPixel.Modificavel(parado with { Cipo = 0 }), "com cipó");
        Afirmar.Falso(EfeitosPixel.Modificavel(parado with { Borda = 58 }), "atrás de uma borda");
        Afirmar.Falso(EfeitosPixel.Modificavel(parado with { Segura = Segura.MaoB }), "segurando um item");
        foreach (PosePixel pose in PosesPixel.Todas.Concat(UsosPixel.Poses))
        {
            foreach (EfeitoVisual efeito in Nove)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    PosePixel m = EfeitosPixel.Modificar(pose, efeito, fase);
                    string onde = $"{pose.Nome}, {efeito} fase {fase}";
                    if (!noChao.Contains(pose.Nome)) Afirmar.Igual(pose, m, $"{onde}: fora do chão, a pose fica como está");
                    Afirmar.Igual((pose.Nome, pose.Estado, pose.Borda, pose.Cipo, pose.Expressao, pose.Vista), (m.Nome, m.Estado, m.Borda, m.Cipo, m.Expressao, m.Vista), $"{onde}: só o corpo muda");
                }
            }
        }
    }

    [Teste]
    public void ModificarCabeNoQuadro()
    {
        // Com a cara da pose e com os chapéus mais altos e mais largos (eriçado e torto): o modificador
        // nunca leva o desenho às bordas de cima e dos lados quando a pose sem modificador não as toca.
        // (Embaixo fica o chão, onde os pés pisam.)
        foreach (PosePixel pose in PosesPixel.Todas)
        {
            // O cipó encosta na borda de cima de propósito (DEC-024); confere-se o corpo.
            PosePixel semCipo = pose with { Cipo = null };
            foreach (string cara in new[] { pose.Expressao, "surpreso", "bebado" })
            {
                (int e0, int t0, int d0, int b0) = Afirmar.NaoNulo(BonecoPixel.Desenhar(semCipo, cara).Limites(), pose.Nome);
                foreach (EfeitoVisual efeito in Nove)
                {
                    for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                    {
                        PosePixel m = EfeitosPixel.Modificar(pose, efeito, fase) with { Cipo = null };
                        (int e, int t, int d, int b) = Afirmar.NaoNulo(BonecoPixel.Desenhar(m, cara, null, efeito, fase).Limites(), pose.Nome);
                        string onde = $"{pose.Nome}/{cara}, {efeito} fase {fase}: ({e},{t})-({d},{b}) contra ({e0},{t0})-({d0},{b0})";
                        Afirmar.Verdadeiro(e >= Math.Min(e0, 1) && t >= Math.Min(t0, 1) && d <= Math.Max(d0, BonecoPixel.Lado - 1), $"{onde}: encosta numa borda do quadro");
                    }
                }
            }
        }
    }
}
