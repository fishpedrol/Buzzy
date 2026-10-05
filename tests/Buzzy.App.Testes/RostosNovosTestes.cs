using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Caras novas do tamagotchi (DEC-028, passo A2): as 8 caras de efeito, com as chaves que o núcleo
/// gera a partir do enum <c>Expressao</c> (o nome em minúsculas: "eletrico", não "acelerado"; a
/// "paranoico", da onda Paranoico, adicional de 2026-10-01, por último), e as 7 caras passageiras das
/// poses de uso, que só existem nos rostos. As 14 caras de humor continuam iguais e na ordem de
/// expressoes.png. O chapéu torto do bêbado e o rubor forte ou verde ficam no quadro em todas as poses.
/// </summary>
internal sealed class RostosNovosTestes
{
    private static readonly string[] Humor =
        ["neutro", "feliz", "rindo", "curioso", "surpreso", "assustado", "sonolento", "bocejando", "dormindo", "travesso", "entediado", "pensativo", "empolgado", "determinado"];

    /// <summary>Ordem do fim do enum <c>Expressao</c> do núcleo (DEC-028), em minúsculas.</summary>
    private static readonly string[] Efeito = ["bebado", "enjoado", "chapado", "eletrico", "apaixonado", "tonto", "viajando", "paranoico"];

    private static readonly string[] Passageiras = ["mordendo", "mastigando", "engolindo", "tragando", "soltando", "fungando", "tossindo"];

    private static PosePixel Pose(string nome) => PosesPixel.Todas.First(p => p.Nome == nome);

    private static int Contar(Tela t, Cor cor) => t.ParaArgb().Count(p => p == Paleta.Argb(cor));

    private static int Diferencas(Tela a, Tela b)
    {
        uint[] x = a.ParaArgb(), y = b.ParaArgb();
        return x.Zip(y).Count(p => p.First != p.Second);
    }

    [Teste]
    public void AsOitoCarasDeEfeitoTemAsChavesDoNucleo()
    {
        Afirmar.Sequencia(Efeito, Rostos.DeEfeito);
        foreach (string chave in Efeito) Afirmar.Verdadeiro(Rostos.Expressoes.ContainsKey(chave), $"'{chave}' existe nos rostos");
        Afirmar.Falso(Rostos.Expressoes.ContainsKey("acelerado"), "o nome do núcleo é 'eletrico' (crítica, C3)");
    }

    [Teste]
    public void AsCatorzeCarasDeHumorContinuamNaOrdemDaPrevia()
    {
        Afirmar.Sequencia(Humor, Rostos.DeHumor);
    }

    [Teste]
    public void AsCarasPassageirasSoExistemNosRostos()
    {
        Afirmar.Sequencia(Passageiras, Rostos.Passageiras);
        // Toda cara está em uma lista só, e as três listas cobrem os rostos inteiros.
        string[] todas = [.. Humor, .. Efeito, .. Passageiras];
        Afirmar.Igual(todas.Length, todas.Distinct().Count(), "uma cara em duas listas");
        Afirmar.Sequencia(todas.Order(StringComparer.Ordinal), Rostos.Expressoes.Keys.Order(StringComparer.Ordinal), "rostos fora das listas");
    }

    [Teste]
    public void TodaChaveDeCarimboExiste()
    {
        foreach ((string nome, Rosto r) in Rostos.Expressoes)
        {
            Afirmar.Verdadeiro(Rostos.Olhos.ContainsKey(r.OlhoE) && Rostos.Olhos.ContainsKey(r.OlhoD), $"{nome}: olhos");
            Afirmar.Verdadeiro(Rostos.Sobrancelhas.ContainsKey(r.Sobrancelhas), $"{nome}: sobrancelhas");
            Afirmar.Verdadeiro(Rostos.Bocas.ContainsKey(r.Boca), $"{nome}: boca");
            Afirmar.Verdadeiro(Rostos.OlhosPerfil.ContainsKey(r.OlhoPerfil) && Rostos.BocasPerfil.ContainsKey(r.BocaPerfil), $"{nome}: perfil");
        }
    }

    [Teste]
    public void CarasNovasSaoDistintasDeFrente()
    {
        PosePixel parado = Pose("parado");
        Tela neutro = BonecoPixel.Desenhar(parado, "neutro");
        string[] todas = [.. Humor, .. Efeito, .. Passageiras];
        var telas = todas.ToDictionary(c => c, c => BonecoPixel.Desenhar(parado, c));
        foreach (string nova in Efeito.Concat(Passageiras))
            Afirmar.Verdadeiro(Diferencas(telas[nova], neutro) >= 10, $"{nova}: diferente do neutro");
        for (int a = 0; a < todas.Length; a++)
            for (int b = a + 1; b < todas.Length; b++)
                Afirmar.Verdadeiro(Diferencas(telas[todas[a]], telas[todas[b]]) > 0, $"{todas[a]} e {todas[b]}: mesma cara de frente");
    }

    [Teste]
    public void CarasDeEfeitoSaoDistintasDePerfil()
    {
        // Andando, o boneco está de perfil (crítica, F9): a cara de efeito precisa se ler ali também.
        PosePixel andando = Pose("andando-1");
        Tela neutro = BonecoPixel.Desenhar(andando, "neutro");
        var telas = Efeito.ToDictionary(c => c, c => BonecoPixel.Desenhar(andando, c));
        foreach (string e in Efeito) Afirmar.Verdadeiro(Diferencas(telas[e], neutro) >= 4, $"{e}: diferente do neutro de perfil");
        for (int a = 0; a < Efeito.Length; a++)
            for (int b = a + 1; b < Efeito.Length; b++)
                Afirmar.Verdadeiro(Diferencas(telas[Efeito[a]], telas[Efeito[b]]) > 0, $"{Efeito[a]} e {Efeito[b]}: mesma cara de perfil");
    }

    [Teste]
    public void CarasNovasNaoOcupamMaisQueAsAntigasEOChapeuTortoTemFolga()
    {
        // Uma cara antiga de cada topete: o contorno do desenho vem do corpo, das orelhas e do
        // chapéu, e uma cara nova não pode passar do que a antiga do mesmo topete já ocupa. (O contorno
        // do chapéu eriçado já encosta na linha de cima em andando-2 e 4 e em escalando-1 e 2, nas caras
        // antigas também, mas inteiro: ver NenhumaCaraCortaOContornoNaBordaDoQuadro. O torto, que é
        // novo, tem 1 pixel de folga em todas as poses.)
        var antiga = new Dictionary<Topete, string> { [Topete.Normal] = "neutro", [Topete.Ericado] = "surpreso", [Topete.Caido] = "sonolento" };
        foreach (PosePixel pose in PosesPixel.Todas)
        {
            // O cipó é o único desenho que encosta numa borda, a de cima (DEC-024).
            PosePixel semCipo = pose with { Cipo = null };
            var limitesDaAntiga = antiga.ToDictionary(a => a.Key, a => Afirmar.NaoNulo(BonecoPixel.Desenhar(semCipo, a.Value).Limites(), $"{pose.Nome}/{a.Value}"));
            foreach (string cara in Efeito.Concat(Passageiras))
            {
                Topete topete = Rostos.Expressoes[cara].Topete;
                Tela t = BonecoPixel.Desenhar(semCipo, cara);
                string onde = $"{pose.Nome}/{cara}";
                (int e, int topo, int d, int b) = Afirmar.NaoNulo(t.Limites(), onde);
                Afirmar.Igual(0, t.ParaArgb().Count(p => (p >> 24) is not 0 and not 255), $"{onde}: alfa só 0 ou 255");
                if (topete == Topete.Torto)
                {
                    Afirmar.Verdadeiro(e >= 1 && topo >= 1 && d <= BonecoPixel.Lado - 1, $"{onde}: o chapéu torto encosta na borda ({e},{topo})-({d},{b})");
                    continue;
                }
                (int eA, int tA, int dA, int bA) = limitesDaAntiga[topete];
                Afirmar.Verdadeiro(e >= eA && topo >= tA && d <= dA && b <= bA, $"{onde}: ocupa ({e},{topo})-({d},{b}), mais que {antiga[topete]} ({eA},{tA})-({dA},{bA})");
            }
        }
    }

    /// <summary>
    /// Se algum pixel de preenchimento (nem transparente nem contorno) está na borda de cima ou dos lados
    /// do quadro. (Embaixo fica o chão, onde os pés pisam: no pouso, eles chegam à última linha.)
    /// </summary>
    private static (int X, int Y)? PreenchimentoNaBorda(Tela t)
    {
        for (int i = 0; i < BonecoPixel.Lado; i++)
        {
            foreach ((int x, int y) in new[] { (i, 0), (0, i), (BonecoPixel.Lado - 1, i) })
                if (t[x, y] is not Cor.Nada and not Cor.Contorno) return (x, y);
        }
        return null;
    }

    [Teste]
    public void NenhumaCaraCortaOContornoNaBordaDoQuadro()
    {
        // Revisão da arte, achado 5: o chapéu eriçado subia até a linha 0 em escalando-2 e o contorno de
        // cima sumia. O contorno pode encostar na borda (andando-2 e 4, escalando-1 e 2 com o chapéu
        // eriçado), mas nenhuma cara, em nenhuma pose, deixa o preenchimento na borda de cima ou dos lados.
        foreach (PosePixel pose in PosesPixel.Todas)
        {
            // O cipó é o único desenho que atravessa uma borda, a de cima (DEC-024).
            PosePixel semCipo = pose with { Cipo = null };
            foreach (string cara in Rostos.Expressoes.Keys)
            {
                if (PreenchimentoNaBorda(BonecoPixel.Desenhar(semCipo, cara)) is { } p)
                    Afirmar.Falhar($"{pose.Nome}/{cara}: preenchimento ({BonecoPixel.Desenhar(semCipo, cara)[p.X, p.Y]}) na borda do quadro em {p}");
            }
        }
        // O mesmo com o corpo reagindo à onda (EfeitosPixel.Modificar), com um chapéu de cada tipo.
        foreach (PosePixel pose in PosesPixel.Todas.Where(EfeitosPixel.Modificavel))
            foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                    foreach (string cara in new[] { "neutro", "surpreso", "sonolento", "bebado", "eletrico", "tonto", "viajando" })
                        if (PreenchimentoNaBorda(BonecoPixel.Desenhar(EfeitosPixel.Modificar(pose, efeito, fase), cara, null, efeito, fase)) is { } p)
                            Afirmar.Falhar($"{pose.Nome}/{cara}, {efeito} fase {fase}: preenchimento na borda do quadro em {p}");
    }

    [Teste]
    public void ChapeuTortoDoBebadoInclinaSemSairDoQuadro()
    {
        Afirmar.Igual(Topete.Torto, Rostos.Expressoes["bebado"].Topete);
        Tela bebado = BonecoPixel.Desenhar(Pose("parado"), "bebado"), neutro = BonecoPixel.Desenhar(Pose("parado"), "neutro");
        // A aba inclina: a ponta da esquerda desce e a da direita sobe (giro anti-horário).
        (int esquerda, int direita) BaseDaAba(Tela t)
        {
            int Ultima(int x) => Enumerable.Range(0, 32).Where(y => t[x, y] is Cor.Palha or Cor.PalhaClara or Cor.PalhaEscura).DefaultIfEmpty(-1).Max();
            int[] colunas = [.. Enumerable.Range(0, BonecoPixel.Lado).Where(x => Ultima(x) >= 0)];
            return (Ultima(colunas.First() + 1), Ultima(colunas.Last() - 1));
        }
        (int eN, int dN) = BaseDaAba(neutro);
        (int eB, int dB) = BaseDaAba(bebado);
        Afirmar.Igual(eN, dN, "a aba do neutro é reta");
        Afirmar.Verdadeiro(eB - dB >= 3, $"a aba torta desce à esquerda e sobe à direita (esquerda {eB}, direita {dB})");
        Afirmar.Verdadeiro(Diferencas(bebado, neutro) >= 40, "o chapéu e o rosto mudam");
    }

    [Teste]
    public void RuborForteDoBebadoEVerdeDoEnjoadoFicamNaPele()
    {
        // Rubor grande: 3 × 2 pixels em cada bochecha de frente e 3 × 2 na bochecha de perfil, todos
        // onde o neutro tem pele (creme), nunca no pelo da borda do rosto.
        foreach ((string pose, int pixels) in new[] { ("parado", 12), ("andando-1", 6), ("andando-2", 6) })
        {
            Tela neutro = BonecoPixel.Desenhar(Pose(pose), "neutro");
            foreach ((string cara, Cor cor) in new[] { ("bebado", Cor.BochechaForte), ("enjoado", Cor.Enjoo) })
            {
                Tela t = BonecoPixel.Desenhar(Pose(pose), cara);
                Afirmar.Igual(pixels, Contar(t, cor), $"{pose}/{cara}: rubor de {cor}");
                for (int y = 0; y < t.Altura; y++)
                    for (int x = 0; x < t.Largura; x++)
                        if (t[x, y] == cor)
                            Afirmar.Verdadeiro(neutro[x, y] is Cor.Creme or Cor.CremeSombra or Cor.CremeClaro, $"{pose}/{cara}: rubor em ({x},{y}) sobre {neutro[x, y]}");
            }
        }
        Tela feliz = BonecoPixel.Desenhar(Pose("parado"), "feliz");
        Afirmar.Igual(4, Contar(feliz, Cor.Bochecha), "o corado de antes continua com 4 pixels");
        Afirmar.Igual(0, Contar(feliz, Cor.BochechaForte) + Contar(feliz, Cor.Enjoo), "sem rubor novo nas caras antigas");
        Afirmar.Igual(0, Contar(BonecoPixel.Desenhar(Pose("andando-1"), "feliz"), Cor.Bochecha), "de perfil, o corado pequeno continua sem rubor");
    }

    [Teste]
    public void OlhosDeEfeitoUsamAsCoresDoDesenho()
    {
        PosePixel parado = Pose("parado");
        Afirmar.Verdadeiro(Contar(BonecoPixel.Desenhar(parado, "chapado"), Cor.EscleraVermelha) >= 4, "chapado: olho vermelho");
        Afirmar.Verdadeiro(Contar(BonecoPixel.Desenhar(parado, "apaixonado"), Cor.Coracao) >= 20, "apaixonado: olhos de coração");
        Tela viajando = BonecoPixel.Desenhar(parado, "viajando");
        Afirmar.Verdadeiro(Contar(viajando, Cor.Rosa) > 0 && Contar(viajando, Cor.Neon) > 0, "viajando: olhos de arco-íris");
    }
}
