using Buzzy.Visual.Pixel;

namespace Buzzy.Visual.Animacao;

// Reprova o build se: falta clipe pra alguma situação; um quadro cita pose ou cara que a arte não
// tem; ou algum quadro, com qualquer cara e deformação possíveis, tem pixel de alfa fora de 0/255.
// Espelho e giros não mudam o alfa, então não precisam ser testados. Onda e item na mão ficam com
// os testes da arte.
public static class ValidadorDeClipes
{
    // Um problema por linha; vazio = passou.
    public static IReadOnlyList<string> Validar(ManifestoDeClipes manifesto) => Validar(manifesto, (pose, cara, deformacao) => Desenhar(pose, cara, deformacao).ParaArgb());

    // desenhar devolve ARGB 64x64. Existe pros testes injetarem um quadro semitransparente, que a
    // pixel art nunca produz, e provar que a checagem reprova.
    public static IReadOnlyList<string> Validar(ManifestoDeClipes manifesto, Func<PosePixel, string?, DeformacaoDoQuadro, uint[]> desenhar)
    {
        ArgumentNullException.ThrowIfNull(manifesto);
        ArgumentNullException.ThrowIfNull(desenhar);
        var problemas = new List<string>();
        foreach (string situacao in Situacoes.Todas)
            if (!manifesto.Tem(situacao)) problemas.Add($"situação \"{situacao}\" sem clipe");

        foreach (Clipe clipe in manifesto.Clipes)
        {
            for (int i = 0; i < clipe.Quadros.Count; i++)
            {
                QuadroDoClipe quadro = clipe.Quadros[i];
                string onde = $"clipe \"{clipe.Situacao}\", quadro {i}";
                PosePixel? pose = PosesPixel.PorNome(quadro.Pose);
                if (pose is null)
                {
                    problemas.Add($"{onde}: a pose \"{quadro.Pose}\" não existe na arte");
                    continue;
                }
                if (UsosPixel.EhDeUso(pose))
                {
                    problemas.Add($"{onde}: a pose \"{quadro.Pose}\" é de uso, que fica fora do manifesto (DEC-036, item 1)");
                    continue;
                }
                if (quadro.Cara is { } cara && !Rostos.Expressoes.ContainsKey(cara))
                {
                    problemas.Add($"{onde}: a cara \"{cara}\" não existe na arte");
                    continue;
                }
                foreach (string? caraPossivel in CarasPossiveis(clipe, quadro))
                    foreach (DeformacaoDoQuadro deformacao in DeformacoesPossiveis(clipe, quadro))
                        if (PixelSemitransparente(desenhar(pose, caraPossivel, deformacao), BonecoPixel.Lado) is { } p)
                            problemas.Add($"{onde}: com a cara {caraPossivel ?? "da pose"} e {deformacao}, o pixel ({p.X},{p.Y}) tem alfa {p.Alfa}, fora de 0 e 255");
            }
        }
        return problemas;
    }

    // Nula = cara da pose.
    private static IEnumerable<string?> CarasPossiveis(Clipe clipe, QuadroDoClipe quadro)
    {
        if (quadro.Cara is { } cara) return [cara];
        string?[] daPose = [null];
        return clipe.Cara switch
        {
            OrigemDaCara.Pose => daPose,
            OrigemDaCara.RetratoSemNeutro => daPose.Concat(Rostos.Expressoes.Keys.Where(k => k != "neutro")),
            _ => Rostos.Expressoes.Keys,
        };
    }

    // PelaVelocidade pode sair dos dois jeitos.
    private static IEnumerable<DeformacaoDoQuadro> DeformacoesPossiveis(Clipe clipe, QuadroDoClipe quadro) => (quadro.Deformacao ?? clipe.Deformacao) switch
    {
        DeformacaoDoQuadro.PelaVelocidade => [DeformacaoDoQuadro.Nenhuma, DeformacaoDoQuadro.Esticado],
        DeformacaoDoQuadro d => [d],
    };

    // Como o app compõe, só sem espelho e giro.
    public static Tela Desenhar(PosePixel pose, string? cara, DeformacaoDoQuadro deformacao)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Tela tela = BonecoPixel.Desenhar(pose, cara);
        return deformacao switch
        {
            DeformacaoDoQuadro.Achatado => tela.Deformada(Deformacoes.Achatado.X, Deformacoes.Achatado.Y),
            DeformacaoDoQuadro.Esticado => tela.Deformada(Deformacoes.Esticado.X, Deformacoes.Esticado.Y),
            _ => tela,
        };
    }

    // Primeiro pixel com alfa fora de 0 e 255, ou nulo.
    public static (int X, int Y, int Alfa)? PixelSemitransparente(Tela tela) => PixelSemitransparente(tela.ParaArgb(), tela.Largura);

    public static (int X, int Y, int Alfa)? PixelSemitransparente(IReadOnlyList<uint> argb, int largura)
    {
        ArgumentNullException.ThrowIfNull(argb);
        for (int i = 0; i < argb.Count; i++)
        {
            int alfa = (int)(argb[i] >> 24);
            if (alfa is not (0 or 255)) return (i % largura, i / largura, alfa);
        }
        return null;
    }
}
