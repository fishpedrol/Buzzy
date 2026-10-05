using Buzzy.Visual.Pixel;

namespace Buzzy.Visual.Animacao;

/// <summary>
/// A validação do manifesto (DEC-036, item 4; critérios 2 e 3 da Fase 6), que reprova o build:
/// <list type="bullet">
/// <item>toda situação de <see cref="Situacoes.Todas"/> tem clipe (o leitor já recusa situação desconhecida ou repetida);</item>
/// <item>todo quadro cita uma pose que a arte tem (<see cref="PosesPixel.PorNome"/>) e, se tiver, uma cara de
/// <see cref="Rostos.Expressoes"/>;</item>
/// <item>todo quadro, desenhado com cada cara que pode receber e cada deformação que pode ter, só tem pixels de alfa 0 ou
/// 255: mais estrito que o critério 3, que tolera semitransparência numa borda de 2 pixels em volta da silhueta.</item>
/// </list>
/// O espelho e os giros do esconderijo só trocam pixels de lugar e não mudam o alfa. A sobreposição da onda e o item na mão
/// ficam com os testes da arte.
/// </summary>
public static class ValidadorDeClipes
{
    /// <summary>Os problemas do manifesto, um por linha; vazio, ele passa.</summary>
    public static IReadOnlyList<string> Validar(ManifestoDeClipes manifesto) => Validar(manifesto, (pose, cara, deformacao) => Desenhar(pose, cara, deformacao).ParaArgb());

    /// <summary>
    /// O mesmo, com o desenho de cada quadro em ARGB de 64 × 64 vindo de <paramref name="desenhar"/>: os testes entregam um
    /// quadro semitransparente, que a pixel art não produz, para provar que a leitura do alfa reprova.
    /// </summary>
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

    /// <summary>As caras que o quadro pode receber (nula é a da pose).</summary>
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

    /// <summary>As deformações que o quadro pode ter: pela velocidade, as duas.</summary>
    private static IEnumerable<DeformacaoDoQuadro> DeformacoesPossiveis(Clipe clipe, QuadroDoClipe quadro) => (quadro.Deformacao ?? clipe.Deformacao) switch
    {
        DeformacaoDoQuadro.PelaVelocidade => [DeformacaoDoQuadro.Nenhuma, DeformacaoDoQuadro.Esticado],
        DeformacaoDoQuadro d => [d],
    };

    /// <summary>O quadro em pixels de arte, como o app o compõe (sem espelho e sem giro, que não mudam o alfa).</summary>
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

    /// <summary>O primeiro pixel com alfa fora de 0 e 255, ou nulo.</summary>
    public static (int X, int Y, int Alfa)? PixelSemitransparente(Tela tela) => PixelSemitransparente(tela.ParaArgb(), tela.Largura);

    /// <summary>O primeiro pixel ARGB com alfa fora de 0 e 255, numa imagem de <paramref name="largura"/> pixels por linha, ou nulo.</summary>
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
