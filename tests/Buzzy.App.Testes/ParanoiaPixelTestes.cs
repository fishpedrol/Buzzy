using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Arte da onda Paranoico (DEC-028, adicional de 2026-10-01: "os cara tá no teto", de desenho animado): a cara
/// "paranoico", no fim das caras de efeito, com os olhos arregalados olhando para cima, as sobrancelhas aflitas, a
/// boca tensa e uma gota de suor na têmpora; os gestos "olharproteto" e "agachar", no fim de
/// <see cref="PosesPixel.DosGestos"/>; e a sobreposição <see cref="EfeitoVisual.Suor"/>, com o tremidinho de 1 pixel.
/// Depois da revisão adversarial da paranoia, os testes também prendem os traços da cara (olho branco sem íris,
/// sobrancelhas aflitas, boca tensa), a gota da têmpora inteira em toda pose, o indicador comprido e reto para cima, o
/// pescoço esticado, o suor longe da mão que aponta, as gotas com forma de gota (2 ou 3 também no cipó) e a gota a
/// salvo de qualquer sobreposição.
/// </summary>
internal sealed class ParanoiaPixelTestes
{
    private static PosePixel Pose(string nome) => PosesPixel.Todas.First(p => p.Nome == nome);

    private static (int X, int Y) CentroDosCarimbos(PosePixel pose)
    {
        (double x, double y) = BonecoPixel.Pontos(pose).Cabeca;
        return ((int)Math.Round(x), (int)Math.Round(y));
    }

    /// <summary>Os pixels de pupila dentro de um retângulo do desenho.</summary>
    private static List<(int X, int Y)> Pupilas(Tela t, int x0, int y0, int x1, int y1)
    {
        var achadas = new List<(int X, int Y)>();
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (t[x, y] == Cor.Pupila) achadas.Add((x, y));
        return achadas;
    }

    [Teste]
    public void AParanoicaFechaAsCarasDeEfeitoEResolveOsCarimbos()
    {
        Afirmar.Igual("paranoico", Rostos.DeEfeito[^1], "a cara nova entra no fim das de efeito, depois de viajando");
        Afirmar.Igual("viajando", Rostos.DeEfeito[^2], "as de antes ficam na ordem");
        Rosto r = Afirmar.NaoNulo(Rostos.Expressoes.GetValueOrDefault("paranoico"), "a cara existe nos rostos");
        Afirmar.Verdadeiro(Rostos.Olhos.ContainsKey(r.OlhoE) && Rostos.Olhos.ContainsKey(r.OlhoD), "olhos de frente");
        Afirmar.Verdadeiro(Rostos.Sobrancelhas.ContainsKey(r.Sobrancelhas), "sobrancelhas");
        Afirmar.Verdadeiro(Rostos.Bocas.ContainsKey(r.Boca), "boca de frente");
        Afirmar.Verdadeiro(Rostos.OlhosPerfil.ContainsKey(r.OlhoPerfil) && Rostos.BocasPerfil.ContainsKey(r.BocaPerfil), "olho e boca de perfil");
        Afirmar.Falso(Rostos.DeHumor.Contains("paranoico") || Rostos.Passageiras.Contains("paranoico"), "é de efeito, não de humor nem passageira");
    }

    [Teste]
    public void OsOlhosDaParanoicaOlhamParaCima()
    {
        // De frente, cada olho é o carimbo de 7 × 8 em (ex − 9, ey − 4) e (ex + 2, ey − 4): a pupila inteira fica na
        // metade de cima, nas linhas ey − 4 a ey − 1.
        PosePixel parado = Pose("parado");
        (int ex, int ey) = CentroDosCarimbos(parado);
        foreach ((string cara, bool paraCima) in new[] { ("paranoico", true), ("surpreso", false), ("assustado", false) })
        {
            Tela t = BonecoPixel.Desenhar(parado, cara);
            foreach (int x0 in new[] { ex - 9, ex + 2 })
            {
                List<(int X, int Y)> pupila = Pupilas(t, x0, ey - 4, x0 + 6, ey + 3);
                Afirmar.Verdadeiro(pupila.Count > 0, $"{cara}: o olho em x = {x0} tem pupila");
                Afirmar.Igual(paraCima, pupila.All(p => p.Y <= ey - 1), $"{cara}: a pupila do olho em x = {x0} só na metade de cima ({string.Join(" ", pupila)})");
            }
        }

        // De perfil (andando), o olho é o carimbo de 5 × 8 em (ex + 3, ey − 5): metade de cima, linhas ey − 5 a ey − 2.
        foreach (string nome in new[] { "andando-1", "andando-2" })
        {
            PosePixel andando = Pose(nome);
            (int px, int py) = CentroDosCarimbos(andando);
            foreach ((string cara, bool paraCima) in new[] { ("paranoico", true), ("surpreso", false) })
            {
                List<(int X, int Y)> pupila = Pupilas(BonecoPixel.Desenhar(andando, cara), px + 3, py - 5, px + 7, py + 2);
                Afirmar.Verdadeiro(pupila.Count > 0, $"{nome}/{cara}: o olho de perfil tem pupila");
                Afirmar.Igual(paraCima, pupila.All(p => p.Y <= py - 2), $"{nome}/{cara}: a pupila de perfil só na metade de cima ({string.Join(" ", pupila)})");
            }
        }
    }

    [Teste]
    public void OsOlhosDaParanoicaSaoBrancosComPupilaPequenaESemIris()
    {
        // Revisão da paranoia, achado 1 (R04): o medo se lê pelo olho todo branco, com uma pupila pequena e sem a íris
        // castanha das outras caras (o pensativo também olha para cima, mas com a íris grande).
        PosePixel parado = Pose("parado");
        (int ex, int ey) = CentroDosCarimbos(parado);
        Tela t = BonecoPixel.Desenhar(parado, "paranoico");
        foreach (int x0 in new[] { ex - 9, ex + 2 })
            ConferirOlho(t, x0, ey - 4, 7, brancoMinimo: 20, $"de frente, o olho em x = {x0}");
        foreach (string nome in new[] { "andando-1", "andando-2" })
        {
            PosePixel andando = Pose(nome);
            (int px, int py) = CentroDosCarimbos(andando);
            ConferirOlho(BonecoPixel.Desenhar(andando, "paranoico"), px + 3, py - 5, 5, brancoMinimo: 12, $"{nome}, de perfil");
        }
    }

    /// <summary>Confere o carimbo de um olho (de <paramref name="largura"/> × 8) no desenho: branco, pupila pequena, sem íris.</summary>
    private static void ConferirOlho(Tela t, int x0, int y0, int largura, int brancoMinimo, string onde)
    {
        int branco = 0, pupila = 0, iris = 0;
        for (int y = y0; y < y0 + 8; y++)
        {
            for (int x = x0; x < x0 + largura; x++)
            {
                if (t[x, y] == Cor.Branco) branco++;
                else if (t[x, y] == Cor.Pupila) pupila++;
                else if (t[x, y] is Cor.Iris or Cor.IrisClara) iris++;
            }
        }
        Afirmar.Verdadeiro(branco >= brancoMinimo && pupila is > 0 and <= 4 && iris == 0, $"{onde}: {branco} pixels de branco, {pupila} de pupila e {iris} de íris");
    }

    [Teste]
    public void AsSobrancelhasDaParanoicaSaoAflitasEErguidas()
    {
        // Revisão da paranoia, achado 1 (R01, R02): preocupadas e erguidas. De frente, o par de sobrancelhas é o carimbo de
        // 15 × 3 em (ex − 8, ey − 7), com o meio na coluna ex − 1. Em cada uma, a ponta de dentro é o ponto mais alto e a
        // metade de fora fica toda mais baixa que ela (as "erguidas" do surpreso são um arco reto em cima, as "bravas"
        // descem para dentro); e a linha ey − 5, logo acima do olho, fica livre (as "preocupadas" do assustado descem até ela).
        foreach (string nome in new[] { "parado", "sentado" })
        {
            PosePixel pose = Pose(nome);
            (int ex, int ey) = CentroDosCarimbos(pose);
            Tela t = BonecoPixel.Desenhar(pose, "paranoico");
            int meio = ex - 1;
            for (int x = ex - 8; x <= ex + 6; x++)
                Afirmar.Verdadeiro(t[x, ey - 5] != Cor.Sobrancelha, $"{nome}: a sobrancelha desce até a linha logo acima do olho, em ({x},{ey - 5})");
            foreach ((string lado, int de, int ate) in new[] { ("esquerda", ex - 8, meio - 1), ("direita", meio + 1, ex + 6) })
            {
                List<(int X, int Y)> pelos = [.. Enumerable.Range(de, ate - de + 1)
                    .SelectMany(x => Enumerable.Range(ey - 7, 3).Select(y => (X: x, Y: y)))
                    .Where(q => t[q.X, q.Y] == Cor.Sobrancelha)];
                Afirmar.Verdadeiro(pelos.Count >= 3, $"{nome}: a sobrancelha da {lado} tem {pelos.Count} pixels");
                int dentro = pelos.Min(q => Math.Abs(q.X - meio)), fora = pelos.Max(q => Math.Abs(q.X - meio));
                int alturaDaPonta = pelos.Where(q => Math.Abs(q.X - meio) == dentro).Min(q => q.Y);
                Afirmar.Igual(pelos.Min(q => q.Y), alturaDaPonta, $"{nome}: na sobrancelha da {lado}, a ponta de dentro é a mais alta ({string.Join(" ", pelos)})");
                foreach ((int x, int y) in pelos.Where(q => Math.Abs(q.X - meio) > (dentro + fora) / 2.0))
                    Afirmar.Verdadeiro(y > alturaDaPonta, $"{nome}: na sobrancelha da {lado}, o pixel de fora ({x},{y}) não fica abaixo da ponta de dentro");
            }
        }
    }

    [Teste]
    public void ABocaDaParanoicaEPequenaETensa()
    {
        // Revisão da paranoia, achado 1 (R03): a boca é pequena e tensa, os dentes cerrados entre duas linhas escuras;
        // nem aberta (sem o vermelho de dentro nem a língua) nem sorrindo (os cantos não sobem acima do meio).
        PosePixel parado = Pose("parado");
        (int ex, int ey) = CentroDosCarimbos(parado);
        Tela t = BonecoPixel.Desenhar(parado, "paranoico");
        // De frente, o carimbo da boca tem 9 × 4, em (ex − 5, ey + 6).
        List<(int X, int Y)> boca = Boca(t, ex - 5, ey + 6, 9, 4);
        int esquerda = boca.Min(q => q.X), direita = boca.Max(q => q.X), topo = boca.Min(q => q.Y);
        Afirmar.Verdadeiro(direita - esquerda + 1 <= 5, $"de frente, a boca tem {direita - esquerda + 1} pixels de largura, e não até 5");
        foreach (int x in new[] { esquerda, (esquerda + direita) / 2, direita })
            Afirmar.Igual(topo, boca.Where(q => q.X == x).Min(q => q.Y), $"de frente, a coluna {x} da boca começa na linha de cima: sem cantos levantados");
        ConferirBocaFechadaETensa(t, boca, "de frente");

        // De perfil (andando), o carimbo da boca tem 5 × 4, em (ex + 7, ey + 5).
        foreach (string nome in new[] { "andando-1", "andando-2" })
        {
            PosePixel andando = Pose(nome);
            (int px, int py) = CentroDosCarimbos(andando);
            Tela p = BonecoPixel.Desenhar(andando, "paranoico");
            ConferirBocaFechadaETensa(p, Boca(p, px + 7, py + 5, 5, 4), $"{nome}, de perfil");
        }
    }

    /// <summary>Os pixels da boca num retângulo do rosto: o traço escuro, os dentes, o vermelho de dentro e a língua.</summary>
    private static List<(int X, int Y)> Boca(Tela t, int x0, int y0, int largura, int altura)
        => [.. Enumerable.Range(x0, largura).SelectMany(x => Enumerable.Range(y0, altura).Select(y => (X: x, Y: y)))
            .Where(q => t[q.X, q.Y] is Cor.Contorno or Cor.Branco or Cor.Boca or Cor.Lingua)];

    private static void ConferirBocaFechadaETensa(Tela t, List<(int X, int Y)> boca, string onde)
    {
        Afirmar.Verdadeiro(boca.Count >= 6, $"{onde}: a boca tem {boca.Count} pixels");
        Afirmar.Falso(boca.Any(q => t[q.X, q.Y] is Cor.Boca or Cor.Lingua), $"{onde}: a boca está aberta");
        Afirmar.Verdadeiro(boca.Any(q => t[q.X, q.Y] == Cor.Branco && t[q.X, q.Y - 1] == Cor.Contorno && t[q.X, q.Y + 1] == Cor.Contorno),
            $"{onde}: sem dentes cerrados (branco entre duas linhas escuras)");
    }

    private static int Diferencas(Tela a, Tela b) => a.ParaArgb().Zip(b.ParaArgb()).Count(p => p.First != p.Second);

    [Teste]
    public void AParanoicaNaoSeConfundeComAssustadoNemSurpreso()
    {
        // Todas têm os olhos arregalados e o chapéu eriçado: a paranoia se lê pelo olhar para cima, as sobrancelhas
        // aflitas, a boca tensa e a gota de suor, de frente e de perfil.
        foreach (string nome in new[] { "parado", "sentado", "andando-1", "andando-2" })
        {
            Tela paranoico = BonecoPixel.Desenhar(Pose(nome), "paranoico");
            foreach (string outra in new[] { "assustado", "surpreso" })
            {
                int d = Diferencas(paranoico, BonecoPixel.Desenhar(Pose(nome), outra));
                Afirmar.Verdadeiro(d >= 10, $"{nome}: paranoico e {outra} diferem em {d} pixels");
            }
        }
    }

    /// <summary>Os pixels de água do desenho: a gota de suor (o resto do boneco não usa essas cores).</summary>
    private static List<(int X, int Y)> Agua(Tela t)
    {
        var achados = new List<(int X, int Y)>();
        for (int y = 0; y < t.Altura; y++)
            for (int x = 0; x < t.Largura; x++)
                if (t[x, y] is Cor.Agua or Cor.AguaClara or Cor.AguaEscura) achados.Add((x, y));
        return achados;
    }

    [Teste]
    public void AGotaDeSuorFicaNaTemporaEAAreaDoRostoAProtege()
    {
        foreach ((string nome, bool frente) in new[] { ("parado", true), ("sentado", true), ("cocando", true), ("andando-1", false), ("andando-2", false) })
        {
            PosePixel pose = Pose(nome);
            (int ex, int ey) = CentroDosCarimbos(pose);
            Tela t = BonecoPixel.Desenhar(pose, "paranoico");
            List<(int X, int Y)> gota = Agua(t);
            Afirmar.Verdadeiro(gota.Count >= 6, $"{nome}: a gota tem {gota.Count} pixels de água");
            // De frente, na têmpora da esquerda da tela (a mão que aponta é a B, à direita): à esquerda do olho esquerdo
            // (que começa em ex − 9), acima do meio da cabeça; de perfil, atrás do olho (que começa em ex + 3), acima dele.
            foreach ((int x, int y) in gota)
            {
                bool naTempora = frente ? x < ex - 9 && y < ey : x < ex + 3 && y < ey - 2;
                Afirmar.Verdadeiro(naTempora, $"{nome}: pixel da gota fora da têmpora em ({x},{y}), cabeça em ({ex},{ey})");
                // A gota tem contorno: em volta dela, só a própria gota, o brilho branco ou a linha escura.
                foreach ((int qx, int qy) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    Afirmar.Verdadeiro(t[qx, qy] is Cor.Agua or Cor.AguaClara or Cor.AguaEscura or Cor.Branco or Cor.Contorno, $"{nome}: a gota em ({x},{y}) encosta em {t[qx, qy]} sem contorno");
            }

            // A área que as sobreposições não cobrem inclui a gota quando a cara é a paranoica, e só então.
            IReadOnlySet<(int X, int Y)> area = EfeitosPixel.AreaDoRosto(pose, "paranoico");
            foreach ((int x, int y) in gota)
                Afirmar.Verdadeiro(area.Contains((x, y)), $"{nome}: a gota em ({x},{y}) fica fora da área do rosto");
            Afirmar.Verdadeiro(area.IsSupersetOf(EfeitosPixel.AreaDoRosto(pose)), $"{nome}: a área da paranoica contém a de sempre");
            foreach (string antiga in new[] { "surpreso", "assustado", "eletrico", pose.Expressao })
            {
                Afirmar.Igual(0, Agua(BonecoPixel.Desenhar(pose, antiga)).Count, $"{nome}/{antiga}: as caras de antes não suam");
                Afirmar.Verdadeiro(EfeitosPixel.AreaDoRosto(pose, antiga).SetEquals(EfeitosPixel.AreaDoRosto(pose)), $"{nome}/{antiga}: a área de antes não muda");
            }
        }
    }

    /// <summary>A gota da têmpora como ela é desenhada: a ponta em cima, o brilho branco à esquerda e a sombra à direita.</summary>
    private static readonly string[] DesenhoDaGota = [".A.", "AAa", "WAa", "Aaa"];

    [Teste]
    public void AGotaDaTemporaFicaInteiraAVistaEmTodaPose()
    {
        // Revisão da paranoia, achado 1 (R06): desenhada antes dos braços da frente, a gota sumia inteira no agachar. Em
        // toda pose de estado e de gesto, com a cara paranoica, ela aparece inteira, como no desenho, a 2 pixels das bordas.
        foreach (PosePixel pose in PosesPixel.Todas.Concat(PosesPixel.DosGestos))
        {
            Tela t = BonecoPixel.Desenhar(pose, "paranoico");
            List<(int X, int Y)> agua = Agua(t);
            Afirmar.Igual(9, agua.Count, $"{pose.Nome}: os 9 pixels de água da gota à vista ({string.Join(" ", agua)})");
            int x0 = agua.Min(q => q.X), y0 = agua.Min(q => q.Y);
            for (int y = 0; y < DesenhoDaGota.Length; y++)
            {
                for (int x = 0; x < DesenhoDaGota[y].Length; x++)
                {
                    Cor? esperada = DesenhoDaGota[y][x] switch { 'A' => Cor.AguaClara, 'a' => Cor.Agua, 'W' => Cor.Branco, _ => null };
                    if (esperada is { } cor && t[x0 + x, y0 + y] != cor)
                        Afirmar.Falhar($"{pose.Nome}: a gota em ({x0 + x},{y0 + y}) tem {t[x0 + x, y0 + y]} no lugar de {cor}");
                }
            }
            Afirmar.Verdadeiro(x0 >= 2 && y0 >= 2 && x0 + 3 <= BonecoPixel.Lado - 2 && y0 + 4 <= BonecoPixel.Lado - 2, $"{pose.Nome}: a gota em ({x0},{y0}) a menos de 2 pixels de uma borda");
        }
    }

    // ------------------------------------------------------------------ gestos

    private static readonly string[] GestosDaParanoia = ["olharproteto", "agachar"];

    private static PosePixel Gesto(string nome) => Afirmar.NaoNulo(PosesPixel.PorNome(nome), $"a pose {nome} existe");

    private static bool Palha(Cor c) => c is Cor.Palha or Cor.PalhaClara or Cor.PalhaEscura or Cor.Faixa or Cor.FaixaEscura;

    /// <summary>A pose com os dois braços atrás da cabeça: o que os braços da frente cobririam fica à vista.</summary>
    private static PosePixel BracosAtras(PosePixel p) => p with { BracoANaFrente = false, BracoBNaFrente = false };

    [Teste]
    public void OsGestosDaParanoiaFicamNoFimDosGestosDaOnda()
    {
        Afirmar.Sequencia(["soluco", "danca", "gargalhada", "espirro", "tosse", "tremedeira", .. GestosDaParanoia], PosesPixel.DosGestos.Select(p => p.Nome),
            "os seis de antes e, no fim, os da paranoia, na ordem de Gesto.OlharProTeto e Gesto.Agachar");
        foreach (string nome in GestosDaParanoia)
        {
            PosePixel pose = Gesto(nome);
            Afirmar.Igual("paranoico", pose.Expressao, $"{nome}: a cara da própria pose");
            Afirmar.Verdadeiro(pose.Estado.StartsWith("gesto: ", StringComparison.Ordinal) && EfeitosPixel.Modificavel(pose), $"{nome}: é gesto do chão, e o corpo treme com a onda");
            Afirmar.Falso(PosesPixel.Todas.Any(p => p.Nome == nome), $"{nome}: fora de Todas, a folha nativa não ganha quadro");
        }
    }

    [Teste]
    public void OlharProTetoApontaParaCimaSemCobrirORosto()
    {
        PosePixel pose = Gesto("olharproteto"), parado = Pose("parado");
        PontosDoEsqueleto p = BonecoPixel.Pontos(pose);
        Afirmar.Igual(Mao.Apontando, pose.MaoB, "a mão B aponta, com o indicador esticado");
        // (Com o pescoço esticado, a cabeça subiu 1 pixel: a mão fica 3 pixels acima do meio dela, e o dedo, abaixo, chega
        // à altura da aba do chapéu.)
        Afirmar.Verdadeiro(p.MaoB.Y < p.Cabeca.Y - 3 && p.MaoB.X > p.Cabeca.X + 12, $"a mão que aponta fica no alto, ao lado da cabeça: mão {p.MaoB}, cabeça {p.Cabeca}");
        Afirmar.Verdadeiro(pose.QuadrilY > parado.QuadrilY && Math.Abs(pose.PernaA.Superior) > Math.Abs(parado.PernaA.Superior) + 10, "joelhos dobrados, para fora");
        // Revisão da paranoia, achado 5: de frente, o boneco não inclina a cabeça para trás; o pescoço estica (a cabeça
        // sobe em relação ao quadril) e os olhos olham para cima.
        PontosDoEsqueleto emPe = BonecoPixel.Pontos(parado);
        Afirmar.Verdadeiro(pose.QuadrilY - p.Cabeca.Y >= parado.QuadrilY - emPe.Cabeca.Y + 0.9, $"o pescoço estica: cabeça {p.Cabeca}, quadril {pose.QuadrilY}");

        // O dedo: o que muda entre apontar e fechar a mão, em pele.
        Tela t = BonecoPixel.Desenhar(pose), punho = BonecoPixel.Desenhar(pose with { MaoB = Mao.Fechada });
        List<(int X, int Y)> dedo = [.. Enumerable.Range(0, BonecoPixel.Lado * BonecoPixel.Lado)
            .Select(i => (X: i % BonecoPixel.Lado, Y: i / BonecoPixel.Lado))
            .Where(q => t[q.X, q.Y] != punho[q.X, q.Y] && t[q.X, q.Y] is Cor.Creme or Cor.CremeSombra)];
        Afirmar.Verdadeiro(dedo.Count >= 2, $"o dedo tem {dedo.Count} pixels de pele");
        Afirmar.Verdadeiro(dedo.All(q => q.Y + 0.5 < p.MaoB.Y), $"o dedo aponta para cima do punho ({string.Join(" ", dedo)}; punho em {p.MaoB})");
        // Revisão da paranoia, achados 1 (R07, R08) e 4: o indicador sai do meio do alto do punho, reto para cima e comprido.
        // Curto e na beira do punho, inclinado com o antebraço, ele lia como um polegar (um joinha).
        int coluna = (int)Math.Floor(p.MaoB.X);
        int topoDoPunho = Enumerable.Range(0, BonecoPixel.Lado * BonecoPixel.Lado)
            .Select(i => (X: i % BonecoPixel.Lado, Y: i / BonecoPixel.Lado))
            .Where(q => punho[q.X, q.Y] is Cor.Creme or Cor.CremeSombra && Math.Abs(q.X + 0.5 - p.MaoB.X) <= 3 && Math.Abs(q.Y + 0.5 - p.MaoB.Y) <= 3)
            .Min(q => q.Y);
        int ponta = dedo.Min(q => q.Y);
        Afirmar.Verdadeiro(dedo.All(q => q.X == coluna), $"o dedo é um traço reto na coluna do meio do punho, x = {coluna} ({string.Join(" ", dedo)})");
        Afirmar.Sequencia(Enumerable.Range(ponta, topoDoPunho - ponta), dedo.Select(q => q.Y).Order(), "o dedo vai do alto do punho à ponta, sem buraco");
        Afirmar.Verdadeiro(topoDoPunho - ponta >= 4, $"a ponta do dedo (linha {ponta}) fica só {topoDoPunho - ponta} linhas acima do punho (linha {topoDoPunho})");
        Afirmar.Verdadeiro(ponta + 0.5 <= p.Cabeca.Y - 9, $"a ponta do dedo (linha {ponta}) não chega à altura da aba do chapéu (cabeça em {p.Cabeca})");
        // O dedo fica à parte do chapéu, para ler sozinho: entre o contorno dele e o da aba fica ao menos um pixel de fundo.
        int pertoDaPalha = dedo.Min(q => Enumerable.Range(0, BonecoPixel.Lado * BonecoPixel.Lado)
            .Where(i => Palha(t[i % BonecoPixel.Lado, i / BonecoPixel.Lado]))
            .Min(i => Math.Max(Math.Abs(i % BonecoPixel.Lado - q.X), Math.Abs(i / BonecoPixel.Lado - q.Y))));
        Afirmar.Verdadeiro(pertoDaPalha >= 3, $"o dedo fica a {pertoDaPalha} pixel(s) da palha do chapéu");

        // O braço que aponta passa pela frente da orelha, nunca do rosto nem da gota: a área do rosto fica igual à da
        // pose com os braços atrás da cabeça.
        Tela atras = BonecoPixel.Desenhar(BracosAtras(pose));
        foreach ((int x, int y) in EfeitosPixel.AreaDoRosto(pose))
            if (t[x, y] != atras[x, y])
                Afirmar.Falhar($"o braço cobre o rosto em ({x},{y}): {t[x, y]} no lugar de {atras[x, y]}");
    }

    [Teste]
    public void AgacharSeguraOChapeuComAsDuasMaosEEspiaParaCima()
    {
        PosePixel pose = Gesto("agachar"), parado = Pose("parado");
        PontosDoEsqueleto p = BonecoPixel.Pontos(pose), emPe = BonecoPixel.Pontos(parado);
        Afirmar.Verdadeiro(pose.QuadrilY >= parado.QuadrilY + 3, "o quadril desce: agachado");
        Afirmar.Verdadeiro(p.Cabeca.Y >= emPe.Cabeca.Y + 6, $"a cabeça desce entre os ombros: {p.Cabeca} contra {emPe.Cabeca}");
        Afirmar.Verdadeiro(pose.BracoANaFrente && pose.BracoBNaFrente, "os dois braços vêm por cima do chapéu");

        // Cada mão fica sobre o chapéu: em volta do centro dela, o desenho sem os braços na frente é de palha ou da faixa.
        Tela atras = BonecoPixel.Desenhar(BracosAtras(pose));
        foreach ((string lado, (double X, double Y) mao) in new[] { ("A", p.MaoA), ("B", p.MaoB) })
        {
            int palha = 0;
            for (int y = (int)mao.Y - 3; y <= (int)mao.Y + 3; y++)
                for (int x = (int)mao.X - 3; x <= (int)mao.X + 3; x++)
                    if ((x + 0.5 - mao.X) * (x + 0.5 - mao.X) + (y + 0.5 - mao.Y) * (y + 0.5 - mao.Y) <= 2.5 * 2.5 && Palha(atras[x, y])) palha++;
            Afirmar.Verdadeiro(palha >= 6, $"a mão {lado} em {mao} fica sobre o chapéu ({palha} pixels de palha embaixo dela)");
        }

        // Os olhos espiam para cima, à vista entre os antebraços: cada pixel dos carimbos dos olhos está no desenho.
        Tela t = BonecoPixel.Desenhar(pose);
        (int ex, int ey) = CentroDosCarimbos(pose);
        Rosto rosto = Rostos.Expressoes["paranoico"];
        foreach ((string olho, int x0) in new[] { (rosto.OlhoE, ex - 9), (rosto.OlhoD, ex + 2) })
        {
            Carimbo c = Rostos.Olhos[olho];
            for (int y = 0; y < c.Altura; y++)
                for (int x = 0; x < c.Largura; x++)
                    if (c[x, y] is { } cor && t[x0 + x, ey - 4 + y] != cor)
                        Afirmar.Falhar($"o olho em ({x0 + x},{ey - 4 + y}) está coberto: {t[x0 + x, ey - 4 + y]} no lugar de {cor}");
        }
    }

    [Teste]
    public void OsGestosDaParanoiaCabemNoQuadroEPousamOsPesComoOsOutros()
    {
        // O parado é a referência: os pés pousam na última linha só com o contorno da sola, e a pele dos pés logo acima.
        foreach (string nome in GestosDaParanoia.Prepend("parado"))
        {
            PosePixel pose = PosesPixel.PorNome(nome)!;
            foreach (string cara in Rostos.Expressoes.Keys)
            {
                Tela t = BonecoPixel.Desenhar(pose, cara);
                (int e, int topo, int d, int b) = Afirmar.NaoNulo(t.Limites(), nome);
                string onde = $"{nome}/{cara}: ({e},{topo})-({d},{b})";
                Afirmar.Verdadeiro(e >= 1 && d <= BonecoPixel.Lado - 1, $"{onde}: encosta numa lateral");
                Afirmar.Verdadeiro(cara != pose.Expressao || topo >= 1, $"{onde}: com a própria cara, encosta em cima");
                for (int i = 0; i < BonecoPixel.Lado; i++)
                    Afirmar.Verdadeiro(t[i, 0] is Cor.Nada or Cor.Contorno && t[0, i] is Cor.Nada or Cor.Contorno && t[BonecoPixel.Lado - 1, i] is Cor.Nada or Cor.Contorno, $"{onde}: preenchimento numa borda");
                Afirmar.Igual(BonecoPixel.Lado, b, $"{onde}: os pés pousam na última linha");
                Afirmar.Verdadeiro(Enumerable.Range(0, BonecoPixel.Lado).All(x => t[x, BonecoPixel.Lado - 1] is Cor.Nada or Cor.Contorno), $"{onde}: na última linha, só o contorno da sola");
                Afirmar.Verdadeiro(Enumerable.Range(0, BonecoPixel.Lado).Count(x => t[x, BonecoPixel.Lado - 2] is Cor.Creme or Cor.CremeSombra) >= 4, $"{onde}: os pés logo acima da sola");
            }
        }
    }

    // ------------------------------------------------------------------ sobreposição de suor

    /// <summary>Os pixels que a sobreposição pintou (sem o contorno dela), agrupados em gotas pela vizinhança de 8.</summary>
    private static List<List<(int X, int Y)>> Gotas(Tela sem, Tela com)
    {
        var restantes = new HashSet<(int X, int Y)>();
        for (int y = 0; y < BonecoPixel.Lado; y++)
            for (int x = 0; x < BonecoPixel.Lado; x++)
                if (com[x, y] != sem[x, y] && com[x, y] != Cor.Contorno) restantes.Add((x, y));
        var gotas = new List<List<(int X, int Y)>>();
        while (restantes.Count > 0)
        {
            (int X, int Y) inicio = restantes.First();
            restantes.Remove(inicio);
            var gota = new List<(int X, int Y)> { inicio };
            for (int i = 0; i < gota.Count; i++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (restantes.Remove((gota[i].X + dx, gota[i].Y + dy))) gota.Add((gota[i].X + dx, gota[i].Y + dy));
            gotas.Add(gota);
        }
        return gotas;
    }

    [Teste]
    public void OSuorFicaNoFimDosEfeitos()
    {
        Afirmar.Igual(EfeitoVisual.Suor, Enum.GetValues<EfeitoVisual>()[^1], "no fim do enum EfeitoVisual");
        Afirmar.Igual(EfeitoVisual.Suor, EfeitosPixel.Todos[^1], "no fim dos efeitos que desenham");
    }

    [Teste]
    public void OSuorSaltaDaCabecaEmGotasNasTresFases()
    {
        // Também pendurado no cipó (revisão da paranoia, achado 6): a gota que cairia sobre o cipó ou a mão que o segura
        // salta do outro lado da cabeça, e o suor continua com 2 ou 3 gotas.
        foreach (string nome in new[] { "parado", "sentado", "andando-2", "olharproteto", "agachar", "cipo-1", "cipo-2", "cipo-3" })
        {
            PosePixel pose = PosesPixel.PorNome(nome)!;
            (double cx, double cy) = BonecoPixel.Pontos(pose).Cabeca;
            Tela sem = BonecoPixel.Desenhar(pose, "paranoico");
            var desenhos = new List<uint[]>();
            for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
            {
                Tela com = BonecoPixel.Desenhar(pose, "paranoico", null, EfeitoVisual.Suor, fase);
                List<List<(int X, int Y)>> gotas = Gotas(sem, com);
                string onde = $"{nome}, fase {fase}";
                Afirmar.Verdadeiro(gotas.Count is 2 or 3, $"{onde}: {gotas.Count} gotas, e não 2 ou 3");
                foreach (List<(int X, int Y)> gota in gotas)
                {
                    Afirmar.Verdadeiro(gota.Count >= 3 && gota.All(q => com[q.X, q.Y] is Cor.Agua or Cor.AguaClara or Cor.Branco), $"{onde}: gota de água com {gota.Count} pixels");
                    // Formato de gota, e não de bolha (achado 1, R09): inteira (a pequena tem 7 pixels, a grande 10), com um
                    // pixel só na ponta, em cima, e a linha de baixo a mais larga, com 3.
                    int alto = gota.Min(q => q.Y), baixo = gota.Max(q => q.Y);
                    Afirmar.Verdadeiro(gota.Count is 7 or 10, $"{onde}: a gota em {gota[0]} tem {gota.Count} pixels, e não 7 ou 10");
                    Afirmar.Igual(1, gota.Count(q => q.Y == alto), $"{onde}: a ponta da gota em {gota[0]} tem um pixel só");
                    Afirmar.Igual(3, gota.Count(q => q.Y == baixo), $"{onde}: a base da gota em {gota[0]} tem 3 pixels");
                    // Saltam da cabeça: em volta dela (fora do círculo da cabeça, até pouco além das pontas da aba), à
                    // altura do chapéu e dos olhos, nunca embaixo, perto do corpo.
                    (double gx, double gy) = (gota.Average(q => q.X + 0.5), gota.Average(q => q.Y + 0.5));
                    double distancia = Math.Sqrt((gx - cx) * (gx - cx) + (gy - cy) * (gy - cy));
                    Afirmar.Verdadeiro(distancia >= 11 && distancia <= 23 && gy <= cy + 2, $"{onde}: gota em ({gx:0.0},{gy:0.0}) longe da cabeça em ({cx:0.0},{cy:0.0})");
                }
                desenhos.Add(com.ParaArgb());
            }
            for (int a = 0; a < desenhos.Count; a++)
                for (int b = a + 1; b < desenhos.Count; b++)
                    Afirmar.Falso(desenhos[a].SequenceEqual(desenhos[b]), $"{nome}: as fases {a} e {b} são iguais");
            // A fase 0 é a parada (DEC-011): sem relógio, é sempre ela; a 3 volta à 0.
            Afirmar.Sequencia(desenhos[0], BonecoPixel.Desenhar(pose, "paranoico", null, EfeitoVisual.Suor, 0).ParaArgb(), $"{nome}: a fase parada é sempre a mesma");
            Afirmar.Sequencia(desenhos[0], BonecoPixel.Desenhar(pose, "paranoico", null, EfeitoVisual.Suor, EfeitosPixel.Fases).ParaArgb(), $"{nome}: a fase 3 é a 0");
        }
    }

    [Teste]
    public void OSuorTremeOCorpoUmPixelSoNoChao()
    {
        // O tremidinho da paranoia: o quadril vai 1 pixel para cada lado (um terço do tempo de cada lado e um no meio),
        // e nada mais muda; fora do chão (parede, cipó, no ar, segurado, esconderijo e uso), a pose fica como está.
        foreach (PosePixel pose in PosesPixel.Todas.Concat(PosesPixel.DosGestos).Concat(UsosPixel.Poses))
        {
            PosePixel[] fases = [.. Enumerable.Range(0, EfeitosPixel.Fases).Select(f => EfeitosPixel.Modificar(pose, EfeitoVisual.Suor, f))];
            if (!EfeitosPixel.Modificavel(pose))
            {
                foreach (PosePixel m in fases) Afirmar.Igual(pose, m, $"{pose.Nome}: fora do chão, a pose fica como está");
                continue;
            }
            Afirmar.Sequencia([0.0, -1.0, 1.0], fases.Select(m => m.QuadrilX - pose.QuadrilX), $"{pose.Nome}: o quadril treme 1 pixel");
            foreach (PosePixel m in fases) Afirmar.Igual(pose with { QuadrilX = m.QuadrilX }, m, $"{pose.Nome}: só o quadril anda");
        }
        Afirmar.Verdadeiro(PosesPixel.DosGestos.Where(p => p.Expressao == "paranoico").All(EfeitosPixel.Modificavel), "os gestos da paranoia tremem");
    }

    [Teste]
    public void NenhumaSobreposicaoCobreORostoNemAGotaDaParanoica()
    {
        // Com a cara paranoica, que tem a gota na têmpora, em todas as poses de estado (com e sem o cipó) e de gesto, com o
        // corpo mexido pelo modificador de cada efeito: a área do rosto dessa cara, com a gota, fica intacta sob qualquer
        // sobreposição, e não só sob o suor (revisão da paranoia, achado 1, R10: sem a proteção da gota, corações, cores,
        // estrelinhas e poeira a cobriam).
        IEnumerable<PosePixel> poses = PosesPixel.Todas
            .Concat(PosesPixel.Todas.Where(p => p.Cipo is not null).Select(p => p with { Cipo = null }))
            .Concat(PosesPixel.DosGestos);
        foreach (PosePixel pose in poses)
        {
            foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
            {
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    PosePixel m = EfeitosPixel.Modificar(pose, efeito, fase);
                    Tela sem = BonecoPixel.Desenhar(m, "paranoico"), com = BonecoPixel.Desenhar(m, "paranoico", null, efeito, fase);
                    foreach ((int x, int y) in EfeitosPixel.AreaDoRosto(m, "paranoico"))
                        if (com[x, y] != sem[x, y])
                            Afirmar.Falhar($"{pose.Nome}{(pose.Cipo is null ? "" : " (cipó)")}, {efeito} fase {fase}: cobre o rosto em ({x},{y}): {com[x, y]} no lugar de {sem[x, y]}");
                }
            }
        }
    }

    [Teste]
    public void OSuorNaoEncostaNaMaoQueApontaNemNoDedo()
    {
        // Revisão da paranoia, achados 1 (R05) e 3: na fase 2, uma gota pousava na ponta do dedo. No olharproteto, com o
        // tremidinho, nada que o suor pinta (a gota e o contorno dela) chega a menos de 3 pixels da pele da mão que aponta:
        // entre o contorno da gota e o da mão fica ao menos um pixel de fundo.
        PosePixel pose = Gesto("olharproteto");
        for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
        {
            PosePixel m = EfeitosPixel.Modificar(pose, EfeitoVisual.Suor, fase);
            (double mx, double my) = BonecoPixel.Pontos(m).MaoB;
            Tela sem = BonecoPixel.Desenhar(m), com = BonecoPixel.Desenhar(m, null, null, EfeitoVisual.Suor, fase);
            var mao = new List<(int X, int Y)>();
            var suor = new List<(int X, int Y)>();
            for (int y = 0; y < BonecoPixel.Lado; y++)
            {
                for (int x = 0; x < BonecoPixel.Lado; x++)
                {
                    // A pele em volta do punho e acima dele, até a ponta do dedo.
                    if (sem[x, y] is Cor.Creme or Cor.CremeSombra && Math.Abs(x + 0.5 - mx) <= 3 && y + 0.5 >= my - 8 && y + 0.5 <= my + 3) mao.Add((x, y));
                    if (com[x, y] != sem[x, y]) suor.Add((x, y));
                }
            }
            Afirmar.Verdadeiro(mao.Count >= 15 && suor.Count > 0, $"fase {fase}: {mao.Count} pixels de pele na mão e {suor.Count} pintados pelo suor");
            int perto = suor.Min(a => mao.Min(b => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y))));
            Afirmar.Verdadeiro(perto >= 3, $"fase {fase}: o suor chega a {perto} pixel(s) da mão que aponta");
        }
    }
}
