namespace Buzzy.Visual.Pixel;

// Passos do relógio, a 60 por segundo.
public readonly record struct QuadroDeUso(PosePixel Pose, int Passos);

// Animações de uso, no chão e de frente, com a cara da própria pose. A soma dos passos de cada verbo
// tem que bater com TabelaDoTamagotchi.PassosDoUso no núcleo: comer 150, beber 120, fumar 210,
// cheirar 120, engolir 90, inalar 120. Na parede, no cipó e no esconderijo não tem pose de uso:
// mostra a pose do apoio com a cara e o efeito, sem o objeto.
public static class UsosPixel
{
    private static readonly PosePixel Parado = PosesPixel.Todas.First(p => p.Nome == "parado");

    // Só dizem pra que lado o braço dobra; a mão vai aonde a pose pede (Alcancar).
    private static readonly Membro CotoveloEmBaixoB = new(-6, 170);
    private static readonly Membro CotoveloErguidoB = new(120, -60);
    private static readonly Membro CotoveloParaForaB = new(45, -150);
    private static readonly Membro CotoveloParaForaA = new(-45, 150);

    // Mão B com o item à vista do lado do peito, e um pouco mais baixa no fim.
    private static readonly (double X, double Y) MaoNoPeito = (43.5, 39.5);
    private static readonly (double X, double Y) MaoNoFim = (44.5, 43.5);

    // Base: o parado com o item na mão B fechada.
    private static PosePixel Uso(string nome, Verbo verbo, string cara, string variante) => Parado with
    {
        Nome = nome,
        Estado = $"USING: {verbo.ToString().ToLowerInvariant()} (chão)",
        Expressao = cara,
        Segura = Segura.MaoB,
        VarianteDoItem = variante,
        MaoB = Mao.Fechada,
        BracoB = CotoveloEmBaixoB,
        BracoBNaFrente = true,
    };

    private static PosePixel Segurando(string nome, Verbo verbo, string cara, string variante, (double X, double Y) mao)
        => BonecoPixel.Alcancar(Uso(nome, verbo, cara, variante), bracoB: true, mao);

    private static PosePixel NaBoca(string nome, Verbo verbo, string cara, string variante, Membro? cotovelo = null)
        => Uso(nome, verbo, cara, variante) with { PontaNo = PontaNoRosto.Boca, BracoB = cotovelo ?? CotoveloEmBaixoB };

    // ------------------------------------------------------------------ comer (banana, cogumelo)
    private static readonly PosePixel ComendoSegura = Segurando("comendo-1", Verbo.Comer, "empolgado", "aberto", MaoNoPeito);
    private static readonly PosePixel ComendoMorde = NaBoca("comendo-2", Verbo.Comer, "mordendo", "aberto");
    private static readonly PosePixel ComendoMastiga = Segurando("comendo-3", Verbo.Comer, "mastigando", "mordido", MaoNoPeito);
    private static readonly PosePixel ComendoMastigaAberto = Segurando("comendo-4", Verbo.Comer, "feliz", "mordido", MaoNoPeito);
    private static readonly PosePixel ComendoMordeDeNovo = NaBoca("comendo-5", Verbo.Comer, "mordendo", "mordido");
    private static readonly PosePixel ComendoMastigaResto = Segurando("comendo-6", Verbo.Comer, "mastigando", "resto", MaoNoPeito);
    private static readonly PosePixel ComendoMastigaRestoAberto = Segurando("comendo-7", Verbo.Comer, "feliz", "resto", MaoNoPeito);
    private static readonly PosePixel ComendoSatisfeito = Segurando("comendo-8", Verbo.Comer, "rindo", "resto", MaoNoFim);

    // ------------------------------------------------------------------ beber
    private static readonly PosePixel BebendoSegura = Segurando("bebendo-1", Verbo.Beber, "empolgado", "normal", MaoNoPeito);
    private static readonly PosePixel BebendoGole = NaBoca("bebendo-2", Verbo.Beber, "engolindo", "gole", CotoveloErguidoB);
    // O gole seguinte: a cabeça sobe um pixel, e o item vai junto (glub, glub).
    private static readonly PosePixel BebendoGoleAlto = NaBoca("bebendo-3", Verbo.Beber, "engolindo", "gole", CotoveloErguidoB) with { CabecaDescida = -1 };
    private static readonly PosePixel BebendoSatisfeito = Segurando("bebendo-4", Verbo.Beber, "rindo", "normal", MaoNoFim);

    // ------------------------------------------------------------------ fumar (baseado, cigarro)
    private static readonly PosePixel FumandoSegura = Segurando("fumando-1", Verbo.Fumar, "pensativo", "aceso", MaoNoPeito);
    // Cotovelo pra fora na tragada: pra dentro, o antebraço atravessava o queixo.
    private static readonly PosePixel FumandoTraga = NaBoca("fumando-2", Verbo.Fumar, "tragando", "tragando", CotoveloParaForaB);
    private static readonly PosePixel FumandoSolta = Segurando("fumando-3", Verbo.Fumar, "soltando", "aceso", MaoNoPeito) with { EfeitoDaPose = EfeitoVisual.Fumaca };
    private static readonly PosePixel FumandoFumaca = Segurando("fumando-4", Verbo.Fumar, "tragando", "aceso", MaoNoPeito) with { EfeitoDaPose = EfeitoVisual.Fumaca, FaseDoEfeito = 1 };
    private static readonly PosePixel FumandoRelaxa = Segurando("fumando-5", Verbo.Fumar, "sonolento", "aceso", MaoNoFim) with { EfeitoDaPose = EfeitoVisual.Fumaca, FaseDoEfeito = 2 };

    // ------------------------------------------------------------------ cheirar (cocaína)
    // Espelhinho numa mão só (com as duas na barriga parecia biquíni e sunga; no nariz, uma tigela).
    // Primeiro na palma ao lado do peito, como bandeja; depois no nariz, cotovelo pra fora; por fim,
    // mãos vazias e punhos pra cima, elétrico.
    private static readonly PosePixel CheirandoSegura = Segurando("cheirando-1", Verbo.Cheirar, "determinado", "cheia", MaoNoPeito);

    private static PosePixel Fungada(string nome, string variante, int fase) => Uso(nome, Verbo.Cheirar, "fungando", variante) with
    {
        PontaNo = PontaNoRosto.Nariz,
        BracoB = CotoveloParaForaB,
        CabecaDescida = 1,
        EfeitoDaPose = EfeitoVisual.Poeira,
        FaseDoEfeito = fase,
    };

    private static readonly PosePixel CheirandoFunga = Fungada("cheirando-2", "meia", 0);
    private static readonly PosePixel CheirandoFungaDeNovo = Fungada("cheirando-3", "vazia", 1);

    // Punhos pra cima como no "reagindo", atrás da cabeça.
    private static PosePixel Ligado(string nome, int fase) => Uso(nome, Verbo.Cheirar, "eletrico", "vazia") with
    {
        Segura = Segura.Nada,
        MaoA = Mao.Fechada,
        MaoB = Mao.Fechada,
        BracoA = new(-128 - 6 * (fase % 2), -158),
        BracoB = new(128 + 6 * (fase % 2), 158),
        BracoBNaFrente = false,
        EfeitoDaPose = EfeitoVisual.Brilhos,
        FaseDoEfeito = fase,
    };

    private static readonly PosePixel CheirandoLigado = Ligado("cheirando-4", 0);
    private static readonly PosePixel CheirandoLigado1 = Ligado("cheirando-5", 1);
    private static readonly PosePixel CheirandoLigado2 = Ligado("cheirando-6", 2);

    // ------------------------------------------------------------------ engolir (MD, bala)
    private static readonly PosePixel EngolindoSegura = Segurando("engolindo-1", Verbo.Engolir, "curioso", "normal", (42.5, 37.5));
    private static readonly PosePixel EngolindoPoe = NaBoca("engolindo-2", Verbo.Engolir, "surpreso", "na-boca");
    private static readonly PosePixel EngolindoEngole = Uso("engolindo-3", Verbo.Engolir, "engolindo", "normal") with { Segura = Segura.Nada, BracoB = Parado.BracoB, MaoB = Mao.Aberta };
    private static readonly PosePixel EngolindoFim = EngolindoEngole with { Nome = "engolindo-4", Expressao = "travesso" };

    // ------------------------------------------------------------------ inalar (lança-perfume)
    // Frasco na A, lenço na B, cotovelos pra fora (pra dentro, os antebraços cobriam a barriga
    // como um colete escuro).
    private static PosePixel Lanca(string nome, string cara) => Uso(nome, Verbo.Inalar, cara, "lenco") with
    {
        Segura = Segura.Inalar,
        MaoA = Mao.Fechada,
        BracoA = CotoveloParaForaA,
        BracoB = CotoveloParaForaB,
        BracoANaFrente = true,
    };

    private static PosePixel LancaNasMaos(PosePixel pose, (double X, double Y) maoA, (double X, double Y) maoB)
        => BonecoPixel.Alcancar(BonecoPixel.Alcancar(pose, bracoB: false, maoA), bracoB: true, maoB);

    private static readonly PosePixel InalandoBorrifa = LancaNasMaos(Lanca("inalando-1", "travesso") with { EfeitoDaPose = EfeitoVisual.Borrifo }, (24.5, 44.5), (40.5, 44.5));
    private static readonly PosePixel InalandoInala = BonecoPixel.Alcancar(Lanca("inalando-2", "fungando") with { PontaNo = PontaNoRosto.Nariz }, bracoB: false, (21.5, 43.5));
    // Zonzo: braços soltos com frasco e lenço pendurados, cabeça balançando com as estrelinhas.
    private static PosePixel Tonto(string nome, int fase)
        => LancaNasMaos(Lanca(nome, "tonto") with { EfeitoDaPose = EfeitoVisual.Estrelinhas, FaseDoEfeito = fase, Cabeca = (fase - 1) * 5 }, (20.5, 54.5), (43.5, 54.5));

    private static readonly PosePixel InalandoTonto = Tonto("inalando-3", 0);
    private static readonly PosePixel InalandoTonto1 = Tonto("inalando-4", 1);
    private static readonly PosePixel InalandoTonto2 = Tonto("inalando-5", 2);

    private static readonly Dictionary<Verbo, QuadroDeUso[]> Sequencias = new()
    {
        [Verbo.Comer] =
        [
            new(ComendoSegura, 20),
            new(ComendoMorde, 14), new(ComendoMastiga, 10), new(ComendoMastigaAberto, 8), new(ComendoMastiga, 10),
            new(ComendoMordeDeNovo, 14), new(ComendoMastigaResto, 10), new(ComendoMastigaRestoAberto, 8), new(ComendoMastigaResto, 10),
            new(ComendoSatisfeito, 46),
        ],
        [Verbo.Beber] =
        [
            new(BebendoSegura, 14),
            new(BebendoGole, 22), new(BebendoGoleAlto, 8), new(BebendoGole, 22), new(BebendoGoleAlto, 8), new(BebendoGole, 16),
            new(BebendoSatisfeito, 30),
        ],
        [Verbo.Fumar] =
        [
            new(FumandoSegura, 20),
            new(FumandoTraga, 30), new(FumandoSolta, 24), new(FumandoFumaca, 24),
            new(FumandoTraga, 30), new(FumandoSolta, 24), new(FumandoFumaca, 24),
            new(FumandoRelaxa, 34),
        ],
        [Verbo.Cheirar] =
        [
            new(CheirandoSegura, 24),
            new(CheirandoFunga, 18), new(CheirandoFungaDeNovo, 18),
            new(CheirandoLigado, 20), new(CheirandoLigado1, 20), new(CheirandoLigado2, 20),
        ],
        [Verbo.Engolir] =
        [
            new(EngolindoSegura, 20), new(EngolindoPoe, 16), new(EngolindoEngole, 24), new(EngolindoFim, 30),
        ],
        [Verbo.Inalar] =
        [
            new(InalandoBorrifa, 24), new(InalandoInala, 30),
            new(InalandoTonto, 22), new(InalandoTonto1, 22), new(InalandoTonto2, 22),
        ],
    };

    // Sem repetição, na ordem dos verbos e da primeira aparição.
    public static readonly IReadOnlyList<PosePixel> Poses =
        [.. Enum.GetValues<Verbo>().SelectMany(v => Sequencias[v]).Select(q => q.Pose).DistinctBy(p => p.Nome)];

    private static readonly HashSet<string> NomesDeUso = [.. Poses.Select(p => p.Nome)];

    // Pelo nome.
    public static bool EhDeUso(PosePixel pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return NomesDeUso.Contains(pose.Nome);
    }

    // Só leitura, pra quem recebe não conseguir trocar um quadro.
    private static readonly Dictionary<Verbo, IReadOnlyList<QuadroDeUso>> SoLeitura = Sequencias.ToDictionary(s => s.Key, s => (IReadOnlyList<QuadroDeUso>)Array.AsReadOnly(s.Value));

    public static IReadOnlyList<QuadroDeUso> Sequencia(Verbo verbo)
        => SoLeitura.TryGetValue(verbo, out IReadOnlyList<QuadroDeUso>? quadros)
            ? quadros
            : throw new ArgumentOutOfRangeException(nameof(verbo), verbo, "Verbo desconhecido.");

    public static int Passos(Verbo verbo) => Sequencia(verbo).Sum(q => q.Passos);

    // Antes do começo vale o primeiro quadro; depois do fim, o último.
    public static PosePixel Quadro(Verbo verbo, long passo)
    {
        IReadOnlyList<QuadroDeUso> quadros = Sequencia(verbo);
        long resto = Math.Max(0, passo);
        foreach (QuadroDeUso q in quadros)
        {
            if (resto < q.Passos) return q.Pose;
            resto -= q.Passos;
        }
        return quadros[^1].Pose;
    }
}
