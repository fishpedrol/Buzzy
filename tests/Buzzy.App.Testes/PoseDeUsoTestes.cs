using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// A apresentação do tamagotchi (DEC-028, passo T7): o quadro escolhido pelo retrato em USING (no chão, a animação do
/// verbo com o item na mão e a cara da pose; na parede, no cipó e no esconderijo, a pose do apoio com a cara do item e
/// sem objeto, crítica C10 e C25), os gestos da onda (L11), a sobreposição da onda na fase do relógio (L12) e o quadro
/// desenhado pela pixel art a partir dele.
/// </summary>
internal sealed class PoseDeUsoTestes
{
    private static Retrato R(Estado estado, Direcao direcao = Direcao.Direita, Expressao expressao = Expressao.Neutro, Gesto gesto = Gesto.Nenhum, bool relogio = false)
        => new(estado, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), direcao, expressao, gesto, false, false, NivelDeEnergia.Media, relogio, Sinal.Nenhum);

    /// <summary>O retrato de quem usa o item no apoio dado, no passo dado, com a cara que o núcleo põe durante o uso.</summary>
    private static Retrato EmUso(Item item, ApoioDoUso apoio, int passo, Direcao direcao = Direcao.Direita, EstadoDaOnda? onda = null)
    {
        DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
        return R(Estado.Using, direcao, dados.CaraDurante, relogio: true) with
        {
            Uso = new Uso(item, dados.Verbo, dados.PassosDoUso, apoio),
            PassoDoUso = passo,
            Onda = onda,
        };
    }

    /// <summary>A chave do item na arte, pela lista da própria arte (na ordem do enum, amarrada pelo contrato).</summary>
    private static string ChaveDaArte(Item item) => ItensPixel.Todos[(int)item];

    private static string Minusculas(Expressao e) => e.ToString().ToLowerInvariant();

    private static PosePixel PoseDaArte(string nome) => Afirmar.NaoNulo(PosesPixel.PorNome(nome), nome);

    private static int Diferencas(Tela a, Tela b) => a.ParaArgb().Zip(b.ParaArgb()).Count(p => p.First != p.Second);

    private static void Iguais(Tela esperada, Tela obtida, string onde)
    {
        Afirmar.Igual((esperada.Largura, esperada.Altura), (obtida.Largura, obtida.Altura), $"{onde}: tamanho");
        int diferentes = Diferencas(esperada, obtida);
        Afirmar.Igual(0, diferentes, $"{onde}: pixels diferentes do esperado");
    }

    [Teste]
    public void NoChao_OQuadroSegueAAnimacaoDoVerboComOItemNaMao()
    {
        // As faixas de passos da arte (UsosPixel), escritas por extenso para dois verbos.
        (int De, int Ate, string Pose)[] engolir = [(0, 20, "engolindo-1"), (20, 36, "engolindo-2"), (36, 60, "engolindo-3"), (60, 90, "engolindo-4")];
        (int De, int Ate, string Pose)[] beber =
            [(0, 14, "bebendo-1"), (14, 36, "bebendo-2"), (36, 44, "bebendo-3"), (44, 66, "bebendo-2"), (66, 74, "bebendo-3"), (74, 90, "bebendo-2"), (90, 120, "bebendo-4")];
        foreach ((Item item, (int De, int Ate, string Pose)[] faixas) in new[] { (Item.Md, engolir), (Item.Bala, engolir), (Item.Vodka, beber), (Item.Cafe, beber) })
        {
            foreach ((int de, int ate, string pose) in faixas)
            {
                for (int passo = de; passo < ate; passo++)
                {
                    QuadroDoSprite q = PoseDoPersonagem.Escolher(EmUso(item, ApoioDoUso.Chao, passo), passo);
                    Afirmar.Igual((pose, ChaveDaArte(item)), (q.Pose, q.Item), $"{item}, passo {passo}");
                }
            }
        }

        // Todos os itens: do primeiro ao último passo, os quadros do verbo na ordem da sequência da arte, com o item
        // na mão, a cara da pose (expressão nula, C10), de frente e sem espelho, deformação ou giro.
        foreach (Item item in Enum.GetValues<Item>())
        {
            DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
            Verbo verbo = ItensPixel.VerboDe(ChaveDaArte(item));
            var vistos = new List<string>();
            foreach (Direcao direcao in new[] { Direcao.Direita, Direcao.Esquerda })
            {
                vistos.Clear();
                for (int passo = 0; passo < dados.PassosDoUso; passo++)
                {
                    QuadroDoSprite q = PoseDoPersonagem.Escolher(EmUso(item, ApoioDoUso.Chao, passo, direcao), passo);
                    string onde = $"{item} olhando para a {direcao}, passo {passo}";
                    Afirmar.Igual(ChaveDaArte(item), q.Item, $"{onde}: o item na mão");
                    Afirmar.Nulo(q.Expressao, $"{onde}: a cara é a da pose de uso");
                    Afirmar.Igual((false, Deformacao.Nenhuma, Giro.Nenhum), (q.Espelhado, q.Deformacao, q.Giro), $"{onde}: de frente, como as outras poses de frente");
                    if (vistos.Count == 0 || vistos[^1] != q.Pose) vistos.Add(q.Pose);
                }
                Afirmar.Sequencia(UsosPixel.Sequencia(verbo).Select(s => s.Pose.Nome), vistos, $"{item}: os quadros de {verbo}, na ordem");
            }
        }
    }

    [Teste]
    public void NaParedeNoCipoENoEsconderijo_APoseDoApoioComACaraDoItemSemObjeto()
    {
        var agarrado = new Dinamica(0, 0, false, Agarrado: true);
        foreach (Item item in Enum.GetValues<Item>())
        {
            string cara = Minusculas(TabelaDoTamagotchi.DoItem(item).CaraDurante);
            foreach (Direcao direcao in new[] { Direcao.Direita, Direcao.Esquerda })
            {
                foreach (Dinamica dinamica in new[] { default, agarrado })
                {
                    string onde = $"{item}, olhando para a {direcao}, {dinamica}";
                    // Na parede: a pose de hoje de quem está agarrado, virado para ela.
                    QuadroDoSprite hojeNaParede = PoseDoPersonagem.Escolher(R(Estado.Climbing, direcao), 30, agarrado);
                    QuadroDoSprite parede = PoseDoPersonagem.Escolher(EmUso(item, ApoioDoUso.Parede, 30, direcao), 30, dinamica);
                    Afirmar.Igual(("escalando-1", direcao == Direcao.Esquerda), (parede.Pose, parede.Espelhado), $"{onde}: na parede");
                    Afirmar.Igual((hojeNaParede.Pose, hojeNaParede.Espelhado, hojeNaParede.Giro, hojeNaParede.Deformacao), (parede.Pose, parede.Espelhado, parede.Giro, parede.Deformacao), $"{onde}: a pose de quem está agarrado à parede");
                    Afirmar.Igual((cara, (string?)null), (parede.Expressao, parede.Item), $"{onde}: na parede, a cara do item e nada na mão");

                    // No cipó: o quadro do meio, de quem está agarrado.
                    QuadroDoSprite hojeNoCipo = PoseDoPersonagem.Escolher(R(Estado.Hanging, direcao, Expressao.Curioso), 30, agarrado);
                    QuadroDoSprite cipo = PoseDoPersonagem.Escolher(EmUso(item, ApoioDoUso.Cipo, 30, direcao), 30, dinamica);
                    Afirmar.Igual(("cipo-2", direcao == Direcao.Esquerda), (cipo.Pose, cipo.Espelhado), $"{onde}: no cipó");
                    Afirmar.Igual((hojeNoCipo.Pose, hojeNoCipo.Espelhado, hojeNoCipo.Giro, hojeNoCipo.Deformacao), (cipo.Pose, cipo.Espelhado, cipo.Giro, cipo.Deformacao), $"{onde}: a pose de quem está agarrado ao cipó");
                    Afirmar.Igual((cara, (string?)null), (cipo.Expressao, cipo.Item), $"{onde}: no cipó, a cara do item e nada na mão");
                }
            }

            // No esconderijo: a pose de quem espia na mesma borda, girada nas laterais.
            foreach ((LadoDoEsconderijo lado, Giro giro) in new[] { (LadoDoEsconderijo.Baixo, Giro.Nenhum), (LadoDoEsconderijo.Esquerda, Giro.Horario), (LadoDoEsconderijo.Direita, Giro.AntiHorario) })
            {
                var dinamica = new Dinamica(0, 0, false, Esconderijo: lado);
                QuadroDoSprite hoje = PoseDoPersonagem.Escolher(R(Estado.Peeking, expressao: Expressao.Curioso), 30, dinamica);
                QuadroDoSprite q = PoseDoPersonagem.Escolher(EmUso(item, ApoioDoUso.Esconderijo, 30), 30, dinamica);
                Afirmar.Igual(("escondido", false, giro), (q.Pose, q.Espelhado, q.Giro), $"{item}, esconderijo {lado}");
                Afirmar.Igual((hoje.Pose, hoje.Espelhado, hoje.Giro, hoje.Deformacao), (q.Pose, q.Espelhado, q.Giro, q.Deformacao), $"{item}, esconderijo {lado}: a pose de quem espia");
                Afirmar.Igual((cara, (string?)null), (q.Expressao, q.Item), $"{item}, esconderijo {lado}: a cara do item e nada na mão");
            }
        }
    }

    /// <summary>
    /// A sobreposição de cada onda (crítica, L12), escrita por extenso; a paranoia (adicional de 2026-10-01, DEC-028), o
    /// suor de quem acha que tem alguém no teto.
    /// </summary>
    private static readonly Dictionary<Onda, EfeitoVisual> SobreposicaoDaTabela = new()
    {
        [Onda.Satisfeito] = EfeitoVisual.Nenhum,
        [Onda.Alegre] = EfeitoVisual.Nenhum,
        [Onda.Relaxado] = EfeitoVisual.Nenhum,
        [Onda.Ligado] = EfeitoVisual.Nenhum,
        [Onda.Bebado] = EfeitoVisual.Bolhas,
        [Onda.Chapado] = EfeitoVisual.Fumaca,
        [Onda.Eletrico] = EfeitoVisual.Brilhos,
        [Onda.Euforico] = EfeitoVisual.Coracoes,
        [Onda.Tonto] = EfeitoVisual.Estrelinhas,
        [Onda.Viajando] = EfeitoVisual.Cores,
        [Onda.Paranoico] = EfeitoVisual.Suor,
    };

    /// <summary>Retratos e dinâmicas de lugares diferentes, com a onda dada: andando, parado, no cipó, com gesto, usando no chão e na parede, escondido.</summary>
    private static IEnumerable<(Retrato Retrato, Dinamica Dinamica, string Onde)> Lugares(EstadoDaOnda? onda)
    {
        yield return (R(Estado.Walking) with { Onda = onda }, default, "andando");
        yield return (R(Estado.Idle) with { Onda = onda }, default, "parado");
        yield return (R(Estado.Idle, gesto: Gesto.Soluco) with { Onda = onda }, default, "soluçando");
        yield return (R(Estado.Hanging, Direcao.Esquerda) with { Onda = onda }, default, "no cipó");
        yield return (R(Estado.Falling) with { Onda = onda }, new Dinamica(1200, 0, false), "caindo");
        yield return (R(Estado.Peeking) with { Onda = onda }, new Dinamica(0, 0, false, Esconderijo: LadoDoEsconderijo.Direita), "escondido");
        yield return (EmUso(Item.Vodka, ApoioDoUso.Chao, 50, onda: onda), default, "bebendo no chão");
        yield return (EmUso(Item.Cocaina, ApoioDoUso.Parede, 50, onda: onda), new Dinamica(0, 0, false, Agarrado: true), "usando na parede");
    }

    [Teste]
    public void AOndaViraASobreposicaoDaTabelaNaFaseDoRelogio()
    {
        Afirmar.Sequencia(Enum.GetValues<Onda>(), SobreposicaoDaTabela.Keys, "uma linha por onda");
        // Com o relógio ligado, a fase troca a cada 12 passos no estado, em ciclo de 3 (por extenso).
        int[] fases = [.. Enumerable.Range(0, 72).Select(p => p < 12 ? 0 : p < 24 ? 1 : p < 36 ? 2 : p < 48 ? 0 : p < 60 ? 1 : 2)];
        foreach ((Onda onda, EfeitoVisual efeito) in SobreposicaoDaTabela)
        {
            foreach (FaseDaOnda faseDaOnda in Enum.GetValues<FaseDaOnda>())
            {
                foreach ((Retrato r, Dinamica dinamica, string onde) in Lugares(new EstadoDaOnda(onda, faseDaOnda, 1, 1)))
                {
                    for (int passos = 0; passos < fases.Length; passos++)
                    {
                        QuadroDoSprite ligado = PoseDoPersonagem.Escolher(r with { RelogioAtivo = true }, passos, dinamica);
                        Afirmar.Igual((efeito, efeito == EfeitoVisual.Nenhum ? 0 : fases[passos]), (ligado.Efeito, ligado.Fase), $"{onda} ({faseDaOnda}) {onde}, relógio ligado, {passos} passos");
                        QuadroDoSprite parado = PoseDoPersonagem.Escolher(r with { RelogioAtivo = false }, passos, dinamica);
                        Afirmar.Igual((efeito, 0), (parado.Efeito, parado.Fase), $"{onda} ({faseDaOnda}) {onde}, relógio parado: a fase parada (DEC-011)");
                        // A sobreposição não muda a pose, a cara nem o item.
                        QuadroDoSprite semOnda = PoseDoPersonagem.Escolher(r with { RelogioAtivo = true, Onda = null }, passos, dinamica);
                        Afirmar.Igual(semOnda, ligado with { Efeito = EfeitoVisual.Nenhum, Fase = 0 }, $"{onda} {onde}: só a sobreposição muda");
                    }
                }
            }
        }

        // Sem onda, nada por cima, e a fase fica em 0 (o cache não guarda o mesmo desenho três vezes).
        foreach ((Retrato r, Dinamica dinamica, string onde) in Lugares(null))
        {
            for (int passos = 0; passos < 40; passos++)
            {
                QuadroDoSprite q = PoseDoPersonagem.Escolher(r with { RelogioAtivo = true }, passos, dinamica);
                Afirmar.Igual((EfeitoVisual.Nenhum, 0), (q.Efeito, q.Fase), $"sem onda, {onde}, {passos} passos");
            }
        }

        // Só a onda da frente conta: a de fundo fica congelada, sem efeito (desenho do núcleo, 4.5).
        Retrato comFundo = R(Estado.Idle, relogio: true) with
        {
            Onda = new EstadoDaOnda(Onda.Satisfeito, FaseDaOnda.Pico, 1, 1),
            OndaDeFundo = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2),
        };
        Afirmar.Igual(EfeitoVisual.Nenhum, PoseDoPersonagem.Escolher(comFundo, 30).Efeito, "a onda de fundo não desenha");
    }

    [Teste]
    public void OsGestosDaOndaUsamAsPosesProvisoriasEATremedeiraAlternaComOParado()
    {
        // Os da paranoia (adicional de 2026-10-01, DEC-028) têm desenho próprio na arte, com a cara "paranoico" da pose:
        // olhar pro teto, apontando para cima, e agachar, segurando o chapéu.
        var poses = new Dictionary<Gesto, string>
        {
            [Gesto.Soluco] = "soluco",
            [Gesto.Danca] = "danca",
            [Gesto.Gargalhada] = "gargalhada",
            [Gesto.Espirro] = "espirro",
            [Gesto.Tosse] = "tosse",
            [Gesto.OlharProTeto] = "olharproteto",
            [Gesto.Agachar] = "agachar",
        };
        foreach ((Gesto gesto, string pose) in poses)
        {
            foreach (Expressao cara in new[] { Expressao.Neutro, Expressao.Bebado, Expressao.Feliz })
            {
                for (int passos = 0; passos < 40; passos++)
                {
                    QuadroDoSprite q = PoseDoPersonagem.Escolher(R(Estado.Idle, Direcao.Esquerda, cara, gesto, relogio: true), passos);
                    // A gargalhada é animada desde a Fase 6 (DEC-036, item 5): alterna com o pulinho da reação a cada 6 passos.
                    string esperada = gesto == Gesto.Gargalhada && passos / 6 % 2 == 1 ? "reagindo-2" : pose;
                    Afirmar.Igual((esperada, false, (string?)null, (string?)null), (q.Pose, q.Espelhado, q.Expressao, q.Item), $"{gesto}, {cara}, {passos} passos: a pose do gesto, com a cara dela");
                }
            }
        }

        // A tremedeira é o parado deslocado 1 pixel (L11): os dois quadros se alternam a cada 4 passos, e o parado vem
        // com a cara da tremedeira, para só o corpo tremer.
        string caraDaTremedeira = PosesPixel.DosGestos.Single(p => p.Nome == "tremedeira").Expressao;
        Afirmar.Igual("eletrico", caraDaTremedeira, "a cara da pose da tremedeira");
        string[] esperadas = [.. Enumerable.Range(0, 24).Select(p => p / 4 % 2 == 0 ? "tremedeira" : "parado")];
        foreach (Expressao cara in new[] { Expressao.Neutro, Expressao.Eletrico, Expressao.Determinado })
        {
            QuadroDoSprite[] quadros = [.. Enumerable.Range(0, 24).Select(p => PoseDoPersonagem.Escolher(R(Estado.Idle, expressao: cara, gesto: Gesto.Tremedeira, relogio: true), p))];
            Afirmar.Sequencia(esperadas, quadros.Select(q => q.Pose), $"tremedeira com a cara {cara}");
            foreach (QuadroDoSprite q in quadros)
                Afirmar.Igual(q.Pose == "tremedeira" ? null : caraDaTremedeira, q.Expressao, $"tremedeira com a cara {cara}: {q.Pose} com a cara da tremedeira");
        }
    }

    [Teste]
    public void AArteDesenhaOUsoComOItemNaMaoEACaraDaPose()
    {
        foreach (Item item in Enum.GetValues<Item>())
        {
            string chave = ChaveDaArte(item);
            foreach (PosePixel pose in UsosPixel.Sequencia(ItensPixel.VerboDe(chave)).Select(q => q.Pose).DistinctBy(p => p.Nome))
            {
                string onde = $"{chave}, {pose.Nome}";
                var quadro = new QuadroDoSprite(pose.Nome, false, null) { Item = chave };
                Tela desenhada = SpriteProvisorio.Compor(quadro);
                Iguais(BonecoPixel.Desenhar(pose, null, chave), desenhada, onde);
                // Com o item na mão, o desenho não é o da pose vazia.
                if (pose.Segura != Segura.Nada)
                    Afirmar.Verdadeiro(Diferencas(desenhada, SpriteProvisorio.Compor(quadro with { Item = null })) > 0, $"{onde}: o item aparece na mão");
                // Crítica, C10: nas poses de uso, a cara é a da própria pose, mesmo que o quadro peça outra.
                Iguais(desenhada, SpriteProvisorio.Compor(quadro with { Expressao = "bebado" }), $"{onde}: a cara pedida é ignorada na pose de uso");
            }
        }

        // O quadro de uso escolhido pelo retrato vira um bitmap do tamanho da janela, como os outros.
        QuadroDoSprite escolhido = PoseDoPersonagem.Escolher(EmUso(Item.Cerveja, ApoioDoUso.Chao, 20), 20);
        Afirmar.Igual((128, 128), (SpriteProvisorio.Renderizar(escolhido, 96).PixelWidth, SpriteProvisorio.Renderizar(escolhido, 96).PixelHeight), "bitmap de uso a 96 DPI");
    }

    [Teste]
    public void AArteDesenhaOsGestosDaOndaComAsPosesProvisorias()
    {
        foreach (PosePixel pose in PosesPixel.DosGestos)
            Iguais(BonecoPixel.Desenhar(pose), SpriteProvisorio.Compor(new QuadroDoSprite(pose.Nome, false, null)), pose.Nome);
        // Na tremedeira, os dois quadros têm a mesma cara e o corpo anda 1 pixel.
        QuadroDoSprite a = PoseDoPersonagem.Escolher(R(Estado.Idle, gesto: Gesto.Tremedeira, relogio: true), 0);
        QuadroDoSprite b = PoseDoPersonagem.Escolher(R(Estado.Idle, gesto: Gesto.Tremedeira, relogio: true), PoseDoPersonagem.Manifesto["gesto-tremedeira"].Quadros[0].Passos);
        Afirmar.Diferente(a.Pose, b.Pose, "a tremedeira alterna dois quadros");
        Tela ta = SpriteProvisorio.Compor(a), tb = SpriteProvisorio.Compor(b);
        Afirmar.Verdadeiro(Diferencas(ta, tb) > 0, "o corpo treme");
        Iguais(BonecoPixel.Desenhar(PoseDaArte("parado"), "eletrico"), a.Pose == "parado" ? ta : tb, "o quadro do parado tem a cara da tremedeira");
    }

    [Teste]
    public void ASobreposicaoVemComOModificadorSoNasPosesQueOAceitam()
    {
        PosePixel parado = PoseDaArte("parado"), naParede = PoseDaArte("escalando-1"), bebendo = PoseDaArte("bebendo-2");
        foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
        {
            for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
            {
                string onde = $"{efeito}, fase {fase}";
                // No chão, o corpo reage à onda (desenho da arte, 4.9).
                Iguais(BonecoPixel.Desenhar(EfeitosPixel.Modificar(parado, efeito, fase), "bebado", null, efeito, fase),
                    SpriteProvisorio.Compor(new QuadroDoSprite("parado", false, "bebado") { Efeito = efeito, Fase = fase }), $"{onde}: parado");
                // Na parede e nas poses de uso, a pose fica como está; só a sobreposição entra.
                Iguais(BonecoPixel.Desenhar(naParede, "surpreso", null, efeito, fase).Espelhada(),
                    SpriteProvisorio.Compor(new QuadroDoSprite("escalando-1", true, "surpreso") { Efeito = efeito, Fase = fase }), $"{onde}: na parede, espelhado");
                Iguais(BonecoPixel.Desenhar(bebendo, null, "vodka", efeito, fase),
                    SpriteProvisorio.Compor(new QuadroDoSprite("bebendo-2", false, null) { Item = "vodka", Efeito = efeito, Fase = fase }), $"{onde}: bebendo");
            }
        }
        // O modificador muda o corpo de verdade: de pé, o bêbado balança (fora dele, ficaria reto).
        Afirmar.Verdadeiro(Diferencas(BonecoPixel.Desenhar(parado, null, null, EfeitoVisual.Bolhas, 0),
            SpriteProvisorio.Compor(new QuadroDoSprite("parado", false, null) { Efeito = EfeitoVisual.Bolhas, Fase = 0 })) > 20, "o bêbado de pé balança");
        // Sem sobreposição, o quadro é o de sempre.
        Iguais(BonecoPixel.Desenhar(parado, "feliz"), SpriteProvisorio.Compor(new QuadroDoSprite("parado", false, "feliz")), "parado feliz, sem onda");
    }

    // O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10; DEC-028): o núcleo, de verdade, o faz fumar
    // sozinho, sem item no mundo. A apresentação mostra, passo a passo, o mesmo quadro do baseado solto pelo usuário: a
    // animação do fumar, com o baseado na mão (a chave da arte, sem janela de item nem Id), a cara da própria pose e, por
    // cima, a fumaça do chapado, a onda que o baseado começa.
    [Teste]
    public void BaseadoPorContaPropria_OQuadroDoBaseadoSoltoPeloUsuario()
    {
        var cfg = new ConfiguracaoDoNucleo { Tamagotchi = true, Acoes = AcoesAutonomas.FumarBaseado };
        var topologia = new Topologia([new MonitorDoDesktop("m1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1040), 96, true)]);
        EstadoDoNucleo s = Maquina.Aplicar(EstadoDoNucleo.Inicial(7), new Loaded(topologia, null, Preferencias.Padrao), cfg).Estado;
        s = Maquina.Aplicar(s, new AutonomyTimer(s.Geracao), cfg).Estado;
        Afirmar.Igual(Estado.Using, s.Estado, "fumou por conta própria");
        Afirmar.Igual(ItensNoMundo.Nenhum, s.Itens, "sem item no mundo");
        DadosDoItem dados = TabelaDoTamagotchi.DoItem(Item.Baseado);
        var vistos = new List<string>();
        for (int passo = 0; passo < dados.PassosDoUso; passo++)
        {
            Retrato r = s.Retrato();
            Afirmar.Igual(passo, r.PassoDoUso, $"passo {passo}");
            QuadroDoSprite q = PoseDoPersonagem.Escolher(r, passo);
            QuadroDoSprite solto = PoseDoPersonagem.Escolher(EmUso(Item.Baseado, ApoioDoUso.Chao, passo, onda: r.Onda), passo);
            Afirmar.Igual(solto, q, $"passo {passo}: o quadro do baseado solto pelo usuário");
            Afirmar.Igual((UsosPixel.Quadro(Verbo.Fumar, passo).Nome, ChaveDaArte(Item.Baseado), (string?)null, EfeitoVisual.Fumaca),
                (q.Pose, q.Item, q.Expressao, q.Efeito), $"passo {passo}: fumando, com o baseado na mão, a cara da pose e a fumaça do chapado");
            if (vistos.Count == 0 || vistos[^1] != q.Pose) vistos.Add(q.Pose);
            s = Maquina.Aplicar(s, new Tick(), cfg).Estado;
        }
        Afirmar.Sequencia(UsosPixel.Sequencia(Verbo.Fumar).Select(q => q.Pose.Nome), vistos, "a sequência inteira do fumar, na ordem");
        Afirmar.Igual(Estado.Idle, s.Estado, "no fim, de volta a IDLE");
    }
}
