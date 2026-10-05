using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Poses de uso do tamagotchi (DEC-028, passo A3): no chão e de frente (crítica, C25), uma sequência de
/// quadros por verbo cuja soma de passos é a duração do uso no núcleo (TabelaDoTamagotchi.PassosDoUso,
/// fixada pelo coordenador), com o item na pega da mão, a ponta dele na boca ou no nariz nos quadros
/// em que vai ao rosto (a até 2 pixels) e a cara da própria pose.
/// </summary>
internal sealed class UsosPixelTestes
{
    /// <summary>Duração do uso por verbo, em passos de 60 por segundo (decisão do coordenador).</summary>
    private static readonly Dictionary<Verbo, int> Duracao = new()
    {
        [Verbo.Comer] = 150,
        [Verbo.Beber] = 120,
        [Verbo.Fumar] = 210,
        [Verbo.Cheirar] = 120,
        [Verbo.Engolir] = 90,
        [Verbo.Inalar] = 120,
    };

    private static IEnumerable<PosePixel> PosesDoVerbo(Verbo verbo) => UsosPixel.Sequencia(verbo).Select(q => q.Pose).DistinctBy(p => p.Nome);

    private static double Distancia((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static int Contar(Tela t, Cor cor) => t.ParaArgb().Count(p => p == Paleta.Argb(cor));

    [Teste]
    public void ASomaDosPassosDeCadaVerboEhADuracaoDoUsoNoNucleo()
    {
        Afirmar.Sequencia(Enum.GetValues<Verbo>(), Duracao.Keys, "um número por verbo");
        foreach ((Verbo verbo, int passos) in Duracao)
        {
            IReadOnlyList<QuadroDeUso> quadros = UsosPixel.Sequencia(verbo);
            Afirmar.Igual(passos, quadros.Sum(q => q.Passos), $"{verbo}: soma dos quadros");
            Afirmar.Igual(passos, UsosPixel.Passos(verbo), $"{verbo}: Passos");
            // Nenhum quadro pisca: cada um fica ao menos 6 passos (0,1 s) na tela.
            Afirmar.Verdadeiro(quadros.All(q => q.Passos >= 6), $"{verbo}: quadros de 6 passos ou mais");
            Afirmar.Verdadeiro(quadros.Count >= 4, $"{verbo}: uma animação de verdade ({quadros.Count} quadros)");
        }
    }

    /// <summary>Os trechos seguidos de quadros com o item no rosto, cada um com os nomes das poses na ordem.</summary>
    private static List<List<string>> TrechosNoRosto(Verbo verbo)
    {
        var trechos = new List<List<string>>();
        List<string>? atual = null;
        foreach (QuadroDeUso q in UsosPixel.Sequencia(verbo))
        {
            if (q.Pose.PontaNo == PontaNoRosto.Nenhuma)
            {
                atual = null;
                continue;
            }
            if (atual is null) trechos.Add(atual = []);
            atual.Add(q.Pose.Nome);
        }
        return trechos;
    }

    [Teste]
    public void GolesTragadasEMordidasSeRepetem()
    {
        // Repetir ajuda a animação a se ler (revisão da arte, achado 6): duas mordidas e duas tragadas
        // separadas por quadros longe da boca; no gole, a cabeça sobe e desce ao menos duas vezes com a
        // garrafa na boca (glub, glub); e duas fungadas.
        foreach (Verbo verbo in new[] { Verbo.Comer, Verbo.Fumar })
            Afirmar.Verdadeiro(TrechosNoRosto(verbo).Count >= 2, $"{verbo}: {TrechosNoRosto(verbo).Count} trecho(s) com o item no rosto");
        // No gole, num trecho, um quadro volta depois de outro (A, B, A): a cabeça sobe e desce de novo.
        bool repete = TrechosNoRosto(Verbo.Beber).Any(t => Enumerable.Range(2, Math.Max(0, t.Count - 2)).Any(i => t[i] == t[i - 2] && t[i] != t[i - 1]));
        Afirmar.Verdadeiro(repete || TrechosNoRosto(Verbo.Beber).Count >= 2, $"Beber: o gole não se repete ({string.Join(" | ", TrechosNoRosto(Verbo.Beber).Select(t => string.Join(", ", t)))})");
        // Duas fungadas: dois quadros no nariz, e o espelho esvazia de uma para a outra.
        string[] fungadas = [.. UsosPixel.Sequencia(Verbo.Cheirar).Where(q => q.Pose.PontaNo != PontaNoRosto.Nenhuma).Select(q => q.Pose.VarianteDoItem!)];
        Afirmar.Verdadeiro(fungadas.Distinct().Count() >= 2, $"Cheirar: {fungadas.Length} quadro(s) no nariz ({string.Join(", ", fungadas)})");
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
            Afirmar.Verdadeiro(TrechosNoRosto(verbo).Count > 0, $"{verbo}: o item vai ao rosto");
    }

    [Teste]
    public void NoRostoACaraEhADoVerbo()
    {
        // Achado 6: com o item no rosto, a cara é a da ação (morder, engolir o gole, tragar, fungar); a
        // pílula e a bala entram na boca com cara de surpresa.
        var cara = new Dictionary<Verbo, string>
        {
            [Verbo.Comer] = "mordendo",
            [Verbo.Beber] = "engolindo",
            [Verbo.Fumar] = "tragando",
            [Verbo.Cheirar] = "fungando",
            [Verbo.Engolir] = "surpreso",
            [Verbo.Inalar] = "fungando",
        };
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
            foreach (QuadroDeUso q in UsosPixel.Sequencia(verbo).Where(q => q.Pose.PontaNo != PontaNoRosto.Nenhuma))
                Afirmar.Igual(cara[verbo], q.Pose.Expressao, $"{verbo}, {q.Pose.Nome}: a cara no rosto");
    }

    [Teste]
    public void OUsoComecaComOItemAVistaETerminaComEleLongeDoRosto()
    {
        // Achado 6: a ordem se lê — primeiro o item à vista, na mão, com uma cara de humor (não a da ação);
        // no fim, o item longe do rosto e não mais alto que no começo (ou já fora da mão), com uma cara de reação.
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            IReadOnlyList<QuadroDeUso> quadros = UsosPixel.Sequencia(verbo);
            PosePixel primeiro = quadros[0].Pose, ultimo = quadros[^1].Pose;
            Afirmar.Diferente(Segura.Nada, primeiro.Segura, $"{verbo}: começa com o item na mão");
            Afirmar.Igual(PontaNoRosto.Nenhuma, primeiro.PontaNo, $"{verbo}: começa com o item à vista, longe do rosto");
            Afirmar.Igual(PontaNoRosto.Nenhuma, ultimo.PontaNo, $"{verbo}: termina com o item longe do rosto");
            foreach (PosePixel pose in new[] { primeiro, ultimo })
                Afirmar.Falso(Rostos.Passageiras.Contains(pose.Expressao), $"{verbo}, {pose.Nome}: cara de ação ({pose.Expressao}) fora do rosto");
            Afirmar.Diferente(primeiro.Expressao, ultimo.Expressao, $"{verbo}: a cara do fim é a da reação, não a do começo");
            if (ultimo.Segura == Segura.Nada) continue;
            double Altura(PosePixel p)
            {
                PontosDoEsqueleto pontos = BonecoPixel.Pontos(p);
                return p.Segura == Segura.MaoB ? pontos.MaoB.Y : (pontos.MaoA.Y + pontos.MaoB.Y) / 2;
            }
            Afirmar.Verdadeiro(Altura(ultimo) >= Altura(primeiro) - 0.01, $"{verbo}: no fim o item desce ou fica ({Altura(ultimo):0.0} contra {Altura(primeiro):0.0} no começo)");
        }
    }

    [Teste]
    public void CheirarEInalarVaoAoNarizOsOutrosABoca()
    {
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            PontaNoRosto esperado = verbo is Verbo.Cheirar or Verbo.Inalar ? PontaNoRosto.Nariz : PontaNoRosto.Boca;
            foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.PontaNo != PontaNoRosto.Nenhuma))
                Afirmar.Igual(esperado, pose.PontaNo, pose.Nome);
        }
    }

    [Teste]
    public void QuadrosDeUsoSaoDeFrenteNoChaoComNomesProprios()
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal);
        foreach (PosePixel pose in UsosPixel.Poses)
        {
            Afirmar.Verdadeiro(nomes.Add(pose.Nome), $"{pose.Nome}: nome repetido");
            Afirmar.Igual(Vista.Frente, pose.Vista, $"{pose.Nome}: de frente");
            Afirmar.Nulo(pose.Borda, $"{pose.Nome}: no chão, sem borda (C25)");
            Afirmar.Nulo(pose.Cipo, $"{pose.Nome}: no chão, sem cipó (C25)");
            Afirmar.Verdadeiro(pose.PesNoChao, $"{pose.Nome}: os pés no chão");
            Afirmar.Verdadeiro(pose.Estado.StartsWith("USING", StringComparison.Ordinal), $"{pose.Nome}: estado {pose.Estado}");
            Afirmar.Verdadeiro(Rostos.Expressoes.ContainsKey(pose.Expressao), $"{pose.Nome}: cara {pose.Expressao}");
            Afirmar.Falso(PosesPixel.Todas.Any(p => p.Nome == pose.Nome), $"{pose.Nome}: não colide com as poses de estado");
            Afirmar.Igual(pose, PosesPixel.PorNome(pose.Nome), $"{pose.Nome}: achada pelo nome");
            Afirmar.Verdadeiro(UsosPixel.EhDeUso(pose), pose.Nome);
        }
        Afirmar.Sequencia(Enum.GetValues<Verbo>().SelectMany(PosesDoVerbo).Select(p => p.Nome), UsosPixel.Poses.Select(p => p.Nome), "as poses são as das sequências, sem sobra");
        Afirmar.Igual(PosesPixel.Todas[0], PosesPixel.PorNome("parado"));
        Afirmar.Nulo(PosesPixel.PorNome("bebendo-99"));
        Afirmar.Falso(UsosPixel.EhDeUso(PosesPixel.Todas[0]), "o parado não é de uso");
    }

    [Teste]
    public void ASequenciaEhSoParaLeitura()
    {
        // Revisão da arte, achado 12: Sequencia devolvia o vetor interno; com um cast, quem chamava trocava
        // um quadro de todas as animações.
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            IReadOnlyList<QuadroDeUso> quadros = UsosPixel.Sequencia(verbo);
            Afirmar.Falso(quadros is QuadroDeUso[], $"{verbo}: o vetor interno");
            QuadroDeUso primeiro = quadros[0];
            Afirmar.Lanca<NotSupportedException>(() => ((IList<QuadroDeUso>)quadros)[0] = quadros[^1], $"{verbo}: trocar um quadro");
            Afirmar.Igual(primeiro, UsosPixel.Sequencia(verbo)[0], $"{verbo}: o quadro continua o mesmo");
        }
    }

    [Teste]
    public void QuadroPeloPassoSegueASequencia()
    {
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            IReadOnlyList<QuadroDeUso> quadros = UsosPixel.Sequencia(verbo);
            long inicio = 0;
            foreach (QuadroDeUso q in quadros)
            {
                Afirmar.Igual(q.Pose, UsosPixel.Quadro(verbo, inicio), $"{verbo}: começo do quadro no passo {inicio}");
                Afirmar.Igual(q.Pose, UsosPixel.Quadro(verbo, inicio + q.Passos - 1), $"{verbo}: fim do quadro no passo {inicio + q.Passos - 1}");
                inicio += q.Passos;
            }
            Afirmar.Igual(quadros[0].Pose, UsosPixel.Quadro(verbo, -5), $"{verbo}: antes do começo, o primeiro");
            Afirmar.Igual(quadros[^1].Pose, UsosPixel.Quadro(verbo, inicio), $"{verbo}: depois do fim, o último");
            Afirmar.Igual(quadros[^1].Pose, UsosPixel.Quadro(verbo, long.MaxValue), $"{verbo}: muito depois do fim, o último");
        }
    }

    [Teste]
    public void CadaVerboUsaAsVariantesQueOsItensDeleTem()
    {
        // Se faltasse a variante, valeria a primeira em silêncio (a banana inteira no fim, por exemplo).
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            Afirmar.Verdadeiro(ItensPixel.DoVerbo(verbo).Count > 0, $"{verbo}: tem itens");
            foreach (string item in ItensPixel.DoVerbo(verbo))
                foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.Segura != Segura.Nada))
                    Afirmar.Verdadeiro(pose.VarianteDoItem is not null && ItensPixel.VariantesNaMao(item).Contains(pose.VarianteDoItem), $"{pose.Nome}/{item}: variante {pose.VarianteDoItem}");
        }
    }

    [Teste]
    public void QuadrosDeUsoCabemNoQuadroComOsPesNoLugar()
    {
        (int _, int _, int _, int baseDoParado) = BonecoPixel.Desenhar(PosesPixel.Todas[0]).Limites()!.Value;
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo))
                {
                    foreach (bool espelhado in new[] { false, true })
                    {
                        Tela t = BonecoPixel.Desenhar(pose, null, item);
                        if (espelhado) t = t.Espelhada();
                        string onde = $"{pose.Nome}/{item}{(espelhado ? " espelhado" : "")}";
                        Afirmar.Igual(0, t.ParaArgb().Count(p => (p >> 24) is not 0 and not 255), $"{onde}: alfa só 0 ou 255");
                        (int e, int topo, int d, int b) = Afirmar.NaoNulo(t.Limites(), onde);
                        Afirmar.Verdadeiro(e >= 1 && topo >= 1 && d <= BonecoPixel.Lado - 1, $"{onde}: encosta numa borda ({e},{topo})-({d},{b})");
                        Afirmar.Igual(baseDoParado, b, $"{onde}: os pés no chão, como no parado");
                    }
                }
            }
        }
    }

    [Teste]
    public void APontaDoItemChegaNaBocaOuNoNarizEFicaAVista()
    {
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.PontaNo != PontaNoRosto.Nenhuma))
            {
                PontosDoEsqueleto pontos = BonecoPixel.Pontos(pose);
                (double X, double Y) alvo = pose.PontaNo == PontaNoRosto.Boca ? pontos.Boca : pontos.Nariz;
                foreach (string item in ItensPixel.DoVerbo(verbo))
                {
                    string onde = $"{pose.Nome}/{item}";
                    ItemColocado[] comPonta = [.. BonecoPixel.ItensColocados(pose, item).Where(c => c.Item.Ponta is not null)];
                    Afirmar.Igual(1, comPonta.Length, $"{onde}: um item vai ao rosto");
                    ItemColocado colocado = comPonta[0];
                    (double X, double Y) ponta = colocado.Ponta!.Value;
                    Afirmar.Verdadeiro(Distancia(ponta, alvo) <= 2, $"{onde}: a ponta {ponta} fica a {Distancia(ponta, alvo):0.00} px de {pose.PontaNo} {alvo}");
                    // A ponta está desenhada de verdade: nem a palma nem o braço a cobrem.
                    Tela t = BonecoPixel.Desenhar(pose, null, item);
                    (int px, int py) = ((int)Math.Floor(ponta.X), (int)Math.Floor(ponta.Y));
                    Cor esperada = colocado.Item.Desenho[colocado.Item.Ponta!.Value.X, colocado.Item.Ponta!.Value.Y]!.Value;
                    Afirmar.Igual(esperada, t[px, py], $"{onde}: o pixel da ponta ({px},{py}) à vista");
                }
            }
        }
    }

    [Teste]
    public void SemPontaNoRostoOBracoFicaComoAPoseDiz()
    {
        foreach (PosePixel pose in UsosPixel.Poses)
        {
            foreach (string item in ItensPixel.Todos)
            {
                PosePixel ajustada = BonecoPixel.AjustadaAoItem(pose, item);
                if (pose.PontaNo == PontaNoRosto.Nenhuma || pose.Segura == Segura.Nada)
                    Afirmar.Igual(pose, ajustada, $"{pose.Nome}/{item}: sem ponta no rosto, nada muda");
                Afirmar.Igual(pose, BonecoPixel.AjustadaAoItem(pose, null), $"{pose.Nome}: sem item, nada muda");
                Afirmar.Igual((pose.Nome, pose.Expressao, pose.QuadrilX, pose.QuadrilY, pose.Tronco, pose.Cabeca, pose.CabecaDescida), (ajustada.Nome, ajustada.Expressao, ajustada.QuadrilX, ajustada.QuadrilY, ajustada.Tronco, ajustada.Cabeca, ajustada.CabecaDescida), $"{pose.Nome}/{item}: só os braços mudam");
            }
        }
    }

    [Teste]
    public void APalmaCobreAPegaDoItem()
    {
        // O item fica entre o antebraço e a palma: no centro de cada mão que segura, a cor é da palma.
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.Segura != Segura.Nada))
                {
                    PontosDoEsqueleto pontos = BonecoPixel.Pontos(BonecoPixel.AjustadaAoItem(pose, item));
                    Tela t = BonecoPixel.Desenhar(pose, null, item);
                    var maos = new List<(double X, double Y)> { pontos.MaoB };
                    if (pose.Segura == Segura.Inalar) maos.Add(pontos.MaoA);
                    foreach ((double x, double y) in maos)
                        Afirmar.Verdadeiro(t[(int)Math.Floor(x), (int)Math.Floor(y)] is Cor.Creme or Cor.CremeSombra, $"{pose.Nome}/{item}: a mão em ({x:0.0},{y:0.0}) por cima do item ({t[(int)Math.Floor(x), (int)Math.Floor(y)]})");
                }
            }
        }
    }

    /// <summary>A pose só com o corpo e o rosto: sem item, sem efeito e com os braços atrás da cabeça.</summary>
    private static Tela SoOCorpo(PosePixel pose)
        => BonecoPixel.Desenhar(pose with { Segura = Segura.Nada, BracoANaFrente = false, BracoBNaFrente = false, EfeitoDaPose = EfeitoVisual.Nenhum });

    private static bool Pele(Cor c) => c is Cor.Creme or Cor.CremeSombra or Cor.CremeClaro;

    private static bool Pelo(Cor c) => c is Cor.Pelo or Cor.PeloEscuro or Cor.PeloClaro;

    [Teste]
    public void OBracoNaoPassaComOPeloNaFrenteDoRosto()
    {
        // Revisão da arte, achados 2 e 9: na tragada, o cotovelo dobrado para dentro atravessava o queixo
        // e o pelo do braço virava uma barba escura. Do nariz para baixo, a pele do rosto só é coberta pelo
        // item e pela mão (o antebraço pode tocar a borda da bochecha junto da mão: no máximo 3 pixels).
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo))
                {
                    PosePixel ajustada = BonecoPixel.AjustadaAoItem(pose, item);
                    Tela so = SoOCorpo(ajustada), com = BonecoPixel.Desenhar(pose, null, item);
                    var doItem = new HashSet<(int, int)>();
                    foreach (ItemColocado c in BonecoPixel.ItensColocados(pose, item))
                        for (int y = 0; y < c.Item.Desenho.Altura; y++)
                            for (int x = 0; x < c.Item.Desenho.Largura; x++)
                                if (c.Item.Desenho[x, y] is not null) doItem.Add((c.X + x, c.Y + y));
                    (double cx, double cy) = BonecoPixel.Pontos(ajustada).Cabeca;
                    int ex = (int)Math.Round(cx), ey = (int)Math.Round(cy);
                    var pelo = new List<(int, int)>();
                    for (int y = ey + 4; y <= ey + 12; y++)
                        for (int x = ex - 10; x <= ex + 10; x++)
                            if (Pele(so[x, y]) && Pelo(com[x, y]) && !doItem.Contains((x, y))) pelo.Add((x, y));
                    Afirmar.Verdadeiro(pelo.Count <= 3, $"{pose.Nome}/{item}: o braço cobre a pele do rosto em {pelo.Count} pixels: {string.Join(" ", pelo)}");
                }
            }
        }
    }

    [Teste]
    public void OsBracosNaoEscondemABarriga()
    {
        // Achado 9: com os dois cotovelos dobrados para dentro, os antebraços cobriam a barriga creme como
        // um colete escuro. Em todo quadro de uso, ao menos 40% da barriga do parado continua à vista.
        PosePixel parado = PosesPixel.Todas[0];
        Tela p = BonecoPixel.Desenhar(parado);
        var barriga = new List<(int X, int Y)>();
        for (int y = (int)parado.QuadrilY - 12; y <= (int)parado.QuadrilY + 2; y++)
            for (int x = (int)parado.QuadrilX - 6; x <= (int)parado.QuadrilX + 6; x++)
                if (Pele(p[x, y])) barriga.Add((x, y));
        Afirmar.Verdadeiro(barriga.Count >= 60, $"a barriga do parado tem {barriga.Count} pixels de pele");
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo))
                {
                    Afirmar.Igual((parado.QuadrilX, parado.QuadrilY, parado.Tronco), (pose.QuadrilX, pose.QuadrilY, pose.Tronco), $"{pose.Nome}: o tronco do parado");
                    Tela t = BonecoPixel.Desenhar(pose, null, item);
                    int visivel = barriga.Count(b => Pele(t[b.X, b.Y]));
                    Afirmar.Verdadeiro(visivel * 10 >= barriga.Count * 4, $"{pose.Nome}/{item}: só {visivel} de {barriga.Count} pixels da barriga à vista");
                }
            }
        }
    }

    [Teste]
    public void NaBocaABalaVaiSemOPapel()
    {
        // Revisão da arte, achado 8: embrulhada, com as pontas torcidas, a bala na boca lia como uma
        // gravata-borboleta. Na mão ela vem embrulhada; na boca, sem o papel: redonda e pequena, como o
        // comprimido (no máximo 6 × 6 pixels).
        Afirmar.Verdadeiro(ItensPixel.NaMao("bala", "normal").Desenho.Largura > 6, "na mão, ainda embrulhada");
        foreach (PosePixel pose in PosesDoVerbo(Verbo.Engolir).Where(p => p.PontaNo == PontaNoRosto.Boca))
        {
            foreach (string item in ItensPixel.DoVerbo(Verbo.Engolir))
            {
                Carimbo naBoca = BonecoPixel.ItensColocados(pose, item).Single().Item.Desenho;
                Afirmar.Verdadeiro(naBoca.Largura <= 6 && naBoca.Altura <= 6, $"{pose.Nome}/{item}: {naBoca.Largura}×{naBoca.Altura} na boca");
            }
        }
    }

    [Teste]
    public void NenhumItemFicaNoMeioDoCorpoComoRoupa()
    {
        // Revisão da arte, achado 1: o espelhinho seguro pelas duas mãos no meio do corpo, da altura do
        // peito para baixo, parecia um biquíni e uma sunga. Abaixo do peito, nenhum item fica centrado no
        // tronco: fica ao lado, numa mão.
        PosePixel parado = PosesPixel.Todas[0];
        double peito = parado.QuadrilY - 9;
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.Segura != Segura.Nada))
                {
                    foreach (ItemColocado c in BonecoPixel.ItensColocados(pose, item))
                    {
                        (double x, double y) = (c.X + c.Item.Desenho.Largura / 2.0, c.Y + c.Item.Desenho.Altura / 2.0);
                        if (y >= peito)
                            Afirmar.Verdadeiro(Math.Abs(x - parado.QuadrilX) >= 6, $"{pose.Nome}/{item}: o item fica no meio do corpo, em ({x:0.0},{y:0.0})");
                    }
                }
            }
        }
    }

    [Teste]
    public void OItemTemUmaLinhaDeContornoOndeEncostaNoCorpo()
    {
        // Achado 6: o item na mão não encosta direto no pelo, na pele ou na palma — onde um pixel visível
        // do item toca algo fora dele, esse vizinho é a linha de contorno (a linha interna do carimbo, a
        // da palma por cima ou o contorno de fora).
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo).Where(p => p.Segura != Segura.Nada))
                {
                    Tela t = BonecoPixel.Desenhar(pose, null, item);
                    // O mesmo quadro sem o item, com os braços onde o item os pôs: um pixel do item só conta como
                    // visível onde o quadro muda (a palma tem a cor do pé do cogumelo, por exemplo).
                    Tela semItem = BonecoPixel.Desenhar(BonecoPixel.AjustadaAoItem(pose, item) with { Segura = Segura.Nada });
                    foreach (ItemColocado c in BonecoPixel.ItensColocados(pose, item))
                    {
                        Carimbo d = c.Item.Desenho;
                        for (int y = 0; y < d.Altura; y++)
                        {
                            for (int x = 0; x < d.Largura; x++)
                            {
                                if (d[x, y] is not { } cor || t[c.X + x, c.Y + y] != cor || semItem[c.X + x, c.Y + y] == cor) continue;
                                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                                {
                                    int qx = c.X + x + dx, qy = c.Y + y + dy;
                                    if (d[x + dx, y + dy] is not null || !t.Dentro(qx, qy)) continue;
                                    if (t[qx, qy] != Cor.Contorno)
                                        Afirmar.Falhar($"{pose.Nome}/{item}: o item em ({c.X + x},{c.Y + y}) encosta em {t[qx, qy]} ({qx},{qy}) sem linha de contorno");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Teste]
    public void OItemApareceNasPosesQueOSeguram()
    {
        var assinatura = new Dictionary<string, Cor>
        {
            ["banana"] = Cor.Banana,
            ["agua"] = Cor.Agua,
            ["vodka"] = Cor.Faixa,
            ["cerveja"] = Cor.Cerveja,
            ["baseado"] = Cor.Papel,
            ["cigarro"] = Cor.Papel,
            ["cocaina"] = Cor.AguaEscura,
            ["md"] = Cor.Lilas,
            ["lancaperfume"] = Cor.AguaEscura,
            ["cafe"] = Cor.Branco,
            ["energetico"] = Cor.Neon,
            ["cogumelo"] = Cor.Coracao,
            ["bala"] = Cor.Rosa,
        };
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoVerbo(verbo))
                {
                    Tela com = BonecoPixel.Desenhar(pose, null, item), sem = BonecoPixel.Desenhar(pose);
                    int mudados = com.ParaArgb().Zip(sem.ParaArgb()).Count(p => p.First != p.Second);
                    int daCor = Contar(com, assinatura[item]) - Contar(sem, assinatura[item]);
                    string onde = $"{pose.Nome}/{item}";
                    if (pose.Segura == Segura.Nada)
                    {
                        Afirmar.Igual(0, mudados, $"{onde}: a mão vazia, o item já foi");
                        continue;
                    }
                    Afirmar.Verdadeiro(mudados >= 6, $"{onde}: o item muda o quadro ({mudados} pixels)");
                    // O resto (a casca, o pezinho do cogumelo) não precisa ter a cor do item inteiro.
                    if (pose.VarianteDoItem != "resto")
                        Afirmar.Verdadeiro(daCor >= 2, $"{onde}: o item à vista ({daCor} pixels de {assinatura[item]})");
                }
            }
        }
    }

    [Teste]
    public void ACaraEhADaPoseEOItemSoApareceNelas()
    {
        foreach (PosePixel pose in UsosPixel.Poses)
        {
            Verbo verbo = Enum.GetValues<Verbo>().First(v => PosesDoVerbo(v).Contains(pose));
            string item = ItensPixel.DoVerbo(verbo)[0];
            Afirmar.Sequencia(BonecoPixel.Desenhar(pose, pose.Expressao, item).ParaArgb(), BonecoPixel.Desenhar(pose, null, item).ParaArgb(), $"{pose.Nome}: a apresentação passa a cara nula");
        }
        // Fora das poses de uso, o item é ignorado: o parado com um item é o parado.
        PosePixel parado = PosesPixel.Todas[0];
        Afirmar.Sequencia(BonecoPixel.Desenhar(parado).ParaArgb(), BonecoPixel.Desenhar(parado, null, "vodka").ParaArgb(), "o parado não segura nada");
    }

    [Teste]
    public void ComerFumarECheirarGastamOItem()
    {
        // A comida diminui, o espelho esvazia e a brasa acende na tragada.
        foreach (string item in ItensPixel.DoVerbo(Verbo.Comer))
        {
            string[] variantes = [.. UsosPixel.Sequencia(Verbo.Comer).Select(q => q.Pose.VarianteDoItem!)];
            Afirmar.Igual("resto", variantes[^1], $"{item}: termina com o resto");
            Afirmar.Sequencia(variantes.OrderBy(v => Array.IndexOf(["aberto", "mordido", "resto"], v)), variantes, $"{item}: as mordidas só avançam");
        }
        string[] espelho = [.. UsosPixel.Sequencia(Verbo.Cheirar).Select(q => q.Pose.VarianteDoItem!)];
        Afirmar.Sequencia(espelho.OrderBy(v => Array.IndexOf(["cheia", "meia", "vazia"], v)), espelho, "as carreiras só somem");
        Afirmar.Igual("vazia", espelho[^1]);
        foreach (string item in ItensPixel.DoVerbo(Verbo.Fumar))
        {
            foreach (PosePixel pose in PosesDoVerbo(Verbo.Fumar))
            {
                int brasaClara = Contar(BonecoPixel.Desenhar(pose, null, item), Cor.BrasaClara);
                if (pose.PontaNo == PontaNoRosto.Boca) Afirmar.Verdadeiro(brasaClara > 0, $"{pose.Nome}/{item}: na tragada, a brasa acende");
                else Afirmar.Igual(0, brasaClara, $"{pose.Nome}/{item}: fora da boca, a brasa normal");
            }
        }
        // Engolir: depois de ir à boca, a mão fica vazia.
        QuadroDeUso[] engolir = [.. UsosPixel.Sequencia(Verbo.Engolir)];
        int naBoca = Array.FindIndex(engolir, q => q.Pose.PontaNo == PontaNoRosto.Boca);
        Afirmar.Verdadeiro(naBoca >= 0 && engolir.Skip(naBoca + 1).All(q => q.Pose.Segura == Segura.Nada) && engolir.Skip(naBoca + 1).Any(), "engolido, o item some da mão");
    }

    [Teste]
    public void AlcancarLevaAMaoAoAlvoComOCotoveloDoLadoPedido()
    {
        PosePixel parado = PosesPixel.Todas[0];
        foreach ((double x, double y) in new[] { (43.5, 39.5), (36.5, 30.5), (44.5, 46.5), (31.5, 37.5), (40.5, 26.5) })
        {
            PosePixel b = BonecoPixel.Alcancar(parado with { MaoB = Mao.Fechada }, bracoB: true, (x, y));
            (double X, double Y) mao = BonecoPixel.Pontos(b).MaoB;
            Afirmar.Verdadeiro(Distancia(mao, (x, y)) < 1e-6, $"mão B em ({x},{y}): obtido {mao}");
            Afirmar.Igual(parado.BracoA, b.BracoA, "o outro braço fica");
            PosePixel a = BonecoPixel.Alcancar(parado with { MaoA = Mao.Fechada }, bracoB: false, (64 - x, y));
            Afirmar.Verdadeiro(Distancia(BonecoPixel.Pontos(a).MaoA, (64 - x, y)) < 1e-6, $"mão A em ({64 - x},{y})");
        }
        // O cotovelo vai para o lado que os ângulos da pose indicam: para baixo ou erguido.
        (double X, double Y) alvo = (46.5, 33.5);
        PosePixel baixo = BonecoPixel.Alcancar(parado with { BracoB = new(-6, 170), MaoB = Mao.Fechada }, bracoB: true, alvo);
        PosePixel alto = BonecoPixel.Alcancar(parado with { BracoB = new(120, -60), MaoB = Mao.Fechada }, bracoB: true, alvo);
        Afirmar.Verdadeiro(Math.Cos(baixo.BracoB.Superior * Math.PI / 180) > Math.Cos(alto.BracoB.Superior * Math.PI / 180), $"cotovelo embaixo ({baixo.BracoB}) contra erguido ({alto.BracoB})");
        // Fora do alcance, a mão fica no caminho do alvo, esticada.
        PosePixel longe = BonecoPixel.Alcancar(parado with { MaoB = Mao.Fechada }, bracoB: true, (63, 63));
        Afirmar.Verdadeiro(Distancia(BonecoPixel.Pontos(longe).MaoB, (63, 63)) > 5, "longe demais, a mão não chega");
    }

    [Teste]
    public void DesenharSemItemNaoFalhaEDeixaAMaoVazia()
    {
        foreach (PosePixel pose in UsosPixel.Poses)
        {
            Tela semItem = BonecoPixel.Desenhar(pose);
            Afirmar.Verdadeiro(semItem.Limites() is not null, pose.Nome);
            foreach (string item in ItensPixel.Todos)
                Afirmar.Verdadeiro(BonecoPixel.Desenhar(pose, null, item).Limites() is not null, $"{pose.Nome}/{item}");
        }
    }
}
