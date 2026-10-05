using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// A boca e o nariz do esqueleto (<see cref="BonecoPixel.Pontos"/>), que levam a ponta do item ao rosto
/// nas poses de uso (DEC-028), conferidos contra o desenho e não contra a própria fórmula: o nariz é o
/// bloco de pêssego desenhado, a boca fica no meio dos pixels que mudam entre duas bocas bem diferentes,
/// e com o item no rosto os olhos continuam à vista. (Revisão da arte, achado 3: com a boca ou o nariz
/// 3 pixels fora do lugar, os itens iam ao queixo ou cobriam os olhos e nenhum teste percebia.)
/// </summary>
internal sealed class RostoDoDesenhoTestes
{
    /// <summary>A pose só com o corpo e o rosto: sem item, sem efeito, sem cipó e com os braços atrás da cabeça.</summary>
    private static Tela SoORosto(PosePixel pose, string cara)
        => BonecoPixel.Desenhar(pose with { Segura = Segura.Nada, BracoANaFrente = false, BracoBNaFrente = false, EfeitoDaPose = EfeitoVisual.Nenhum, Cipo = null }, cara);

    /// <summary>Todas as poses com o rosto inteiro no quadro (sem borda): as de estado, as de gesto e as de uso.</summary>
    private static IEnumerable<PosePixel> PosesComRosto()
        => PosesPixel.Todas.Concat(PosesPixel.DosGestos).Concat(UsosPixel.Poses).Where(p => p.Borda is null);

    private static (int X, int Y) Arredondado((double X, double Y) p) => ((int)Math.Round(p.X), (int)Math.Round(p.Y));

    /// <summary>
    /// O centro do nariz desenhado: de frente, o bloco de 2 × 2 com pêssego em cima e pêssego escuro embaixo;
    /// de perfil, o par de pêssego com o pêssego escuro embaixo do primeiro. Nulo se não houver um só.
    /// </summary>
    private static (double X, double Y)? NarizDesenhado(Tela t, PosePixel pose, (double X, double Y) cabeca)
    {
        bool frente = pose.Vista == Vista.Frente;
        (int cx, int cy) = Arredondado(cabeca);
        var achados = new List<(double X, double Y)>();
        for (int y = cy - 3; y <= cy + 9; y++)
        {
            for (int x = cx + (frente ? -5 : 5); x <= cx + (frente ? 5 : 16); x++)
            {
                bool par = t[x, y] == Cor.Pessego && t[x + 1, y] == Cor.Pessego && t[x, y + 1] == Cor.PessegoEscuro;
                if (par && (!frente || t[x + 1, y + 1] == Cor.PessegoEscuro)) achados.Add((x + 1, y + 1));
            }
        }
        return achados.Count == 1 ? achados[0] : null;
    }

    [Teste]
    public void ONarizDoEsqueletoEhONarizDesenhado()
    {
        foreach (PosePixel pose in PosesComRosto())
        {
            PontosDoEsqueleto pontos = BonecoPixel.Pontos(pose);
            (double X, double Y) desenhado = Afirmar.NaoNulo(NarizDesenhado(SoORosto(pose, "neutro"), pose, pontos.Cabeca), $"{pose.Nome}: um nariz só, perto da cabeça");
            Afirmar.Igual(desenhado, pontos.Nariz, $"{pose.Nome}: o nariz do esqueleto é o centro do nariz desenhado");
        }
    }

    [Teste]
    public void ABocaDoEsqueletoFicaNoMeioDaBocaDesenhada()
    {
        foreach (PosePixel pose in PosesComRosto())
        {
            PontosDoEsqueleto pontos = BonecoPixel.Pontos(pose);
            bool frente = pose.Vista == Vista.Frente;
            Tela sorriso = SoORosto(pose, "neutro"), o = SoORosto(pose, "surpreso");
            (double X, double Y) nariz = Afirmar.NaoNulo(NarizDesenhado(sorriso, pose, pontos.Cabeca), pose.Nome);
            // Abaixo do nariz, os pixels que mudam entre o sorriso do neutro e o "o" do surpreso são as bocas.
            int e = int.MaxValue, t = int.MaxValue, d = int.MinValue, b = int.MinValue;
            for (int y = (int)nariz.Y; y < (int)nariz.Y + 8; y++)
            {
                for (int x = 0; x < BonecoPixel.Lado; x++)
                {
                    bool perto = frente ? Math.Abs(x + 0.5 - pontos.Cabeca.X) <= 8 : x + 0.5 >= pontos.Cabeca.X + 3;
                    if (!perto || sorriso[x, y] == o[x, y]) continue;
                    e = Math.Min(e, x);
                    t = Math.Min(t, y);
                    d = Math.Max(d, x + 1);
                    b = Math.Max(b, y + 1);
                }
            }
            Afirmar.Verdadeiro(e < d && t < b, $"{pose.Nome}: a boca aparece abaixo do nariz");
            (double X, double Y) boca = pontos.Boca, meio = ((e + d) / 2.0, (t + b) / 2.0);
            string onde = $"{pose.Nome}: a boca do esqueleto {boca} e a desenhada ({e},{t})-({d},{b})";
            Afirmar.Verdadeiro(boca.X >= e && boca.X <= d && boca.Y >= t && boca.Y <= b, $"{onde}: fora dela");
            Afirmar.Verdadeiro(Math.Abs(boca.X - meio.X) <= 0.5 && Math.Abs(boca.Y - meio.Y) <= 1, $"{onde}: longe do meio {meio}");
        }
    }

    [Teste]
    public void ComOItemNoRostoOsOlhosContinuamAVista()
    {
        // O item vai à boca ou ao nariz, nunca aos olhos: cada pixel dos carimbos dos olhos da cara da
        // pose continua com a cor do carimbo, com o item e o braço da frente desenhados.
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (PosePixel pose in UsosPixel.Sequencia(verbo).Select(q => q.Pose).DistinctBy(p => p.Nome))
            {
                Rosto rosto = Rostos.Expressoes[pose.Expressao];
                foreach (string item in ItensPixel.DoVerbo(verbo))
                {
                    PosePixel ajustada = BonecoPixel.AjustadaAoItem(pose, item);
                    Tela so = SoORosto(ajustada, pose.Expressao), com = BonecoPixel.Desenhar(pose, null, item);
                    (int ex, int ey) = Arredondado(BonecoPixel.Pontos(ajustada).Cabeca);
                    int cobertos = 0;
                    foreach ((string olho, int x0) in new[] { (rosto.OlhoE, ex - 9), (rosto.OlhoD, ex + 2) })
                    {
                        Carimbo c = Rostos.Olhos[olho];
                        for (int y = 0; y < c.Altura; y++)
                        {
                            for (int x = 0; x < c.Largura; x++)
                            {
                                if (c[x, y] is not { } cor) continue;
                                // O carimbo está mesmo ali no desenho do rosto (senão o teste procuraria no lugar errado).
                                Afirmar.Igual(cor, so[x0 + x, ey - 4 + y], $"{pose.Nome}: o olho '{olho}' desenhado em ({x0 + x},{ey - 4 + y})");
                                if (com[x0 + x, ey - 4 + y] != cor) cobertos++;
                            }
                        }
                    }
                    Afirmar.Igual(0, cobertos, $"{pose.Nome}/{item}: pixels dos olhos cobertos pelo item ou pelo braço");
                }
            }
        }
    }
}
