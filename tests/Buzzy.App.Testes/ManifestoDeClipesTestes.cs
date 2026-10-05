using System.IO;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Animacao;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 6, passos F6-P1 e F6-P2 (DEC-036): o manifesto de clipes — o leitor estrito, o reprodutor, a validação que o build
/// roda — e a escolha do quadro pelo manifesto embutido no app, conferida chamada por chamada contra a escolha da Fase 5
/// congelada em <see cref="PoseDaFase5"/>.
/// </summary>
internal sealed class ManifestoDeClipesTestes
{
    private static string Fonte => File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Apresentacao", "clipes.json"));

    private static Retrato R(Estado estado, Direcao direcao = Direcao.Direita, Expressao expressao = Expressao.Neutro, Gesto gesto = Gesto.Nenhum)
        => new(estado, MotivoDoOcultamento.Nenhum, new PontoPx(0, 0), "m", new TamanhoPx(128, 128), direcao, expressao, gesto, false, false, NivelDeEnergia.Media, true, Sinal.Nenhum);

    private static string Clipe(string situacao, string quadros = "[ { \"pose\": \"parado\", \"passos\": 1 } ]", string extra = "")
        => $"{{ \"situacao\": \"{situacao}\", \"repete\": false, \"cara\": \"pose\", \"espelha\": false{extra}, \"quadros\": {quadros} }}";

    /// <summary>O manifesto do app com o clipe de <paramref name="situacao"/> trocado (ou tirado, com nulo).</summary>
    private static string ComOClipe(string situacao, string? novo)
    {
        string[] linhas = Fonte.Split('\n');
        int i = Array.FindIndex(linhas, l => l.Contains($"\"situacao\": \"{situacao}\"", StringComparison.Ordinal));
        Afirmar.Verdadeiro(i >= 0, $"achou o clipe {situacao}");
        int fim = i;
        while (!linhas[fim].TrimEnd().EndsWith("},", StringComparison.Ordinal) && !linhas[fim].TrimEnd().EndsWith('}')) fim++;
        bool ultimo = !linhas[fim].TrimEnd().EndsWith(',');
        string substituto = novo is null ? "" : novo + (ultimo ? "" : ",");
        string texto = string.Join('\n', linhas[..i].Append(substituto).Concat(linhas[(fim + 1)..]));
        // Sem o último clipe, o penúltimo fica com a vírgula sobrando.
        return texto.Replace(",\n\n  ]", "\n  ]", StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ o manifesto do app

    [Teste]
    public void ManifestoDoApp_LidoEValido_UmClipePorSituacao()
    {
        ManifestoDeClipes m = ManifestoDeClipes.Ler(Fonte);
        Afirmar.Sequencia([], ValidadorDeClipes.Validar(m), "a validação do build passa");
        Afirmar.Sequencia(Situacoes.Todas, m.Clipes.Select(c => c.Situacao), "um clipe por situação, na ordem da lista");
        Afirmar.Sequencia(Situacoes.Todas, PoseDoPersonagem.Manifesto.Clipes.Select(c => c.Situacao), "o embutido é o mesmo arquivo");
        // Os gestos do núcleo e as situações deles: todo gesto, menos o nenhum, tem clipe.
        Afirmar.Sequencia(Enum.GetValues<Gesto>().Where(g => g != Gesto.Nenhum).Select(PoseDoPersonagem.NomeDoGesto), Situacoes.Gestos, "os gestos do núcleo");
    }

    // ------------------------------------------------------------------ o leitor

    // Fase 9, F9-P3 (DEC-040, item 7): 5 000 mutações do manifesto do app, com semente fixa. O leitor ou lança
    // FormatException, ou devolve um manifesto dentro dos limites (situações conhecidas e sem repetição, de 1 a 32 quadros,
    // de 1 a 600 passos); nenhuma outra exceção.
    [Teste]
    public void Leitor_MutacoesDoManifestoDoApp_SoFormatException()
    {
        string original = Fonte;
        var r = new Random(20261005);
        string[] trocas = ["null", "[]", "{}", "\"x\"", "-1", "0", "601", "1.5", "1e400", "true", "\"\\uD800\"", "\"Parado\"", "\"" + new string('a', 65) + "\""];
        int recusados = 0, aceitos = 0;
        for (int caso = 0; caso < 5000; caso++)
        {
            string t = original;
            for (int k = 0, n = 1 + r.Next(3); k < n; k++)
            {
                int i = r.Next(t.Length);
                t = r.Next(6) switch
                {
                    0 => t.Remove(i, Math.Min(1 + r.Next(8), t.Length - i)),
                    1 => t.Insert(i, ((char)r.Next(32, 127)).ToString()),
                    2 => t[..i],
                    3 => t.IndexOf(": ", i, StringComparison.Ordinal) is int j and >= 0 ? t[..(j + 2)] + trocas[r.Next(trocas.Length)] + t[(j + 2)..].TrimStart('"', '-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 't', 'r', 'u', 'e', 'f', 'a', 'l', 's') : t,
                    4 => t.IndexOf('{', i) is int j and >= 0 ? t.Insert(j + 1, " \"repete\": true,") : t,
                    _ => t.Insert(i, "\\uDC00"),
                };
            }
            try
            {
                ManifestoDeClipes m = ManifestoDeClipes.Ler(t);
                aceitos++;
                foreach (string s in Situacoes.Todas.Where(m.Tem))
                {
                    Afirmar.Verdadeiro(Situacoes.Todas.Contains(s, StringComparer.Ordinal), $"caso {caso}: situação {s}");
                    Clipe c = m[s];
                    Afirmar.Verdadeiro(c.Quadros.Count is >= 1 and <= ManifestoDeClipes.MaximoDeQuadros && c.Quadros.All(q => q.Passos is >= 1 and <= ManifestoDeClipes.MaximoDePassos), $"caso {caso}: limites de {s}");
                }
            }
            catch (FormatException)
            {
                recusados++;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"caso {caso}: o leitor lançou {e.GetType().Name}: {e.Message}", e);
            }
        }
        Afirmar.Verdadeiro(recusados > 1000 && aceitos > 10, $"recusados {recusados}, aceitos {aceitos}");
    }

    [Teste]
    public void Leitor_RecusaQualquerDesvioDoFormato()
    {
        string Um(string clipe) => $"{{ \"versao\": 1, \"clipes\": [ {clipe} ] }}";
        (string Caso, string Json)[] recusados =
        [
            ("não é JSON", "{ versao: 1"),
            ("versão 2", "{ \"versao\": 2, \"clipes\": [] }"),
            ("campo desconhecido na raiz", "{ \"versao\": 1, \"clipes\": [], \"extra\": 1 }"),
            ("falta clipes", "{ \"versao\": 1 }"),
            ("clipes não é lista", "{ \"versao\": 1, \"clipes\": {} }"),
            ("situação desconhecida", Um(Clipe("voando"))),
            ("situação repetida", $"{{ \"versao\": 1, \"clipes\": [ {Clipe("parado")}, {Clipe("parado")} ] }}"),
            ("campo desconhecido no clipe", Um(Clipe("parado", extra: ", \"fps\": 10"))),
            ("sem quadros", Um(Clipe("parado", "[]"))),
            ("33 quadros", Um(Clipe("parado", "[" + string.Join(",", Enumerable.Repeat("{ \"pose\": \"parado\", \"passos\": 1 }", 33)) + "]"))),
            ("passos zero", Um(Clipe("parado", "[ { \"pose\": \"parado\", \"passos\": 0 } ]"))),
            ("passos demais", Um(Clipe("parado", "[ { \"pose\": \"parado\", \"passos\": 601 } ]"))),
            ("passos fracionários", Um(Clipe("parado", "[ { \"pose\": \"parado\", \"passos\": 1.5 } ]"))),
            ("pose com maiúscula", Um(Clipe("parado", "[ { \"pose\": \"Parado\", \"passos\": 1 } ]"))),
            ("pose vazia", Um(Clipe("parado", "[ { \"pose\": \"\", \"passos\": 1 } ]"))),
            ("campo desconhecido no quadro", Um(Clipe("parado", "[ { \"pose\": \"parado\", \"passos\": 1, \"x\": 1 } ]"))),
            ("cara fora da lista", Um(Clipe("parado").Replace("\"cara\": \"pose\"", "\"cara\": \"sempre\"", StringComparison.Ordinal))),
            ("deformação fora da lista", Um(Clipe("parado", extra: ", \"deformacao\": \"girado\""))),
            ("repete não booleano", Um(Clipe("parado").Replace("\"repete\": false", "\"repete\": 0", StringComparison.Ordinal))),
            ("grande demais", "{ \"versao\": 1, \"clipes\": [] }" + new string(' ', ManifestoDeClipes.MaximoDeCaracteres)),
            ("campo repetido no clipe", Um(Clipe("parado", extra: ", \"repete\": true"))),
            ("campo repetido na raiz", "{ \"versao\": 1, \"versao\": 1, \"clipes\": [] }"),
            ("surrogate solto num valor", Um(Clipe("parado").Replace("\"situacao\": \"parado\"", "\"situacao\": \"\\uD800\"", StringComparison.Ordinal))),
            ("surrogate solto num nome", "{ \"versao\": 1, \"clipes\": [], \"\\uDC00\": 1 }"),
        ];
        foreach ((string caso, string json) in recusados)
            Afirmar.Lanca<FormatException>(() => ManifestoDeClipes.Ler(json), caso);

        ManifestoDeClipes lido = ManifestoDeClipes.Ler(Um(Clipe("pousando", "[ { \"pose\": \"pousando\", \"passos\": 5, \"cara\": \"surpreso\", \"deformacao\": \"achatado\" }, { \"pose\": \"pousando\", \"passos\": 2 } ]", ", \"deformacao\": \"pela-velocidade\"")));
        Clipe c = lido["pousando"];
        Afirmar.Igual((2, 7, DeformacaoDoQuadro.PelaVelocidade, "surpreso", DeformacaoDoQuadro.Achatado, (DeformacaoDoQuadro?)null),
            (c.Quadros.Count, c.Duracao, c.Deformacao, c.Quadros[0].Cara, c.Quadros[0].Deformacao, c.Quadros[1].Deformacao), "lido campo a campo");
        Afirmar.Lanca<KeyNotFoundException>(() => _ = lido["parado"], "situação sem clipe");
    }

    // ------------------------------------------------------------------ o reprodutor

    [Teste]
    public void Reprodutor_RepeteOuParaNoUltimo_PelosPassos()
    {
        QuadroDoClipe a = new("andando-1", 8), b = new("andando-2", 4), c = new("andando-3", 2);
        var repete = new Clipe("andando", [a, b, c], true, OrigemDaCara.Retrato, true, DeformacaoDoQuadro.Nenhuma);
        var para = repete with { Repete = false };
        (long Passos, int Repete, int Para)[] casos = [(-5, 0, 0), (0, 0, 0), (7, 0, 0), (8, 1, 1), (11, 1, 1), (12, 2, 2), (13, 2, 2), (14, 0, 2), (22, 1, 2), (1_000_000, 1_000_000 % 14 < 8 ? 0 : 1_000_000 % 14 < 12 ? 1 : 2, 2)];
        foreach ((long passos, int esperadoRepete, int esperadoPara) in casos)
        {
            Afirmar.Igual(esperadoRepete, ReprodutorDeClipes.Quadro(repete, passos).Indice, $"repete, passo {passos}");
            Afirmar.Igual(esperadoPara, ReprodutorDeClipes.Quadro(para, passos).Indice, $"para no último, passo {passos}");
        }
    }

    // ------------------------------------------------------------------ a validação

    [Teste]
    public void Validador_AcusaSituacaoSemClipe_PoseOuCaraAusente_PoseDeUso_EAlfaFora()
    {
        (string Caso, string Json, string Trecho)[] casos =
        [
            ("sem o clipe do cipó", ComOClipe("cipo", null), "situação \"cipo\" sem clipe"),
            ("sem o último clipe", ComOClipe("gesto-agachar", null), "situação \"gesto-agachar\" sem clipe"),
            ("pose ausente", ComOClipe("caindo", Clipe("caindo", "[ { \"pose\": \"voando\", \"passos\": 1 } ]")), "a pose \"voando\" não existe na arte"),
            ("cara ausente", ComOClipe("caindo", Clipe("caindo", "[ { \"pose\": \"caindo\", \"passos\": 1, \"cara\": \"zangado\" } ]")), "a cara \"zangado\" não existe na arte"),
            ("pose de uso", ComOClipe("caindo", Clipe("caindo", "[ { \"pose\": \"comendo-1\", \"passos\": 1 } ]")), "é de uso"),
        ];
        foreach ((string caso, string json, string trecho) in casos)
        {
            IReadOnlyList<string> problemas = ValidadorDeClipes.Validar(ManifestoDeClipes.Ler(json));
            Afirmar.Verdadeiro(problemas.Any(p => p.Contains(trecho, StringComparison.Ordinal)), $"{caso}: acusou \"{trecho}\" ({string.Join(" | ", problemas)})");
        }

        // A validação lê o alfa de cada quadro desenhado: com um desenho semitransparente (a pixel art não produz um), reprova
        // com o pixel e o alfa; com o desenho de verdade, passa.
        ManifestoDeClipes doApp = ManifestoDeClipes.Ler(Fonte);
        IReadOnlyList<string> semitransparentes = ValidadorDeClipes.Validar(doApp, (pose, cara, deformacao) =>
        {
            uint[] argb = ValidadorDeClipes.Desenhar(pose, cara, deformacao).ParaArgb();
            if (pose.Nome == "caindo-2") argb[64 * 10 + 20] = 0x80405060;
            return argb;
        });
        Afirmar.Verdadeiro(semitransparentes.Count > 0 && semitransparentes.All(p => p.StartsWith("clipe \"caindo\", quadro 1", StringComparison.Ordinal) && p.Contains("o pixel (20,10) tem alfa 128", StringComparison.Ordinal)),
            $"o quadro semitransparente reprova: {string.Join(" | ", semitransparentes.Take(2))}");

        // Num clipe com a cara do retrato, toda cara que o quadro pode receber é desenhada: só com a cara de bêbado, o primeiro
        // quadro da caminhada sai semitransparente, e a validação acha.
        IReadOnlyList<string> comUmaCara = ValidadorDeClipes.Validar(doApp, (pose, cara, deformacao) =>
        {
            uint[] argb = ValidadorDeClipes.Desenhar(pose, cara, deformacao).ParaArgb();
            if (pose.Nome == "andando-1" && cara == "bebado") argb[64 * 10 + 20] = 0x80405060;
            return argb;
        });
        Afirmar.Verdadeiro(comUmaCara.Any(p => p.StartsWith("clipe \"andando\", quadro 0: com a cara bebado", StringComparison.Ordinal)),
            $"a cara do retrato também é desenhada: {string.Join(" | ", comUmaCara.Take(2))}");

        // O alfa: a leitura dos pixels acha o primeiro fora de 0 e 255, e só ele.
        uint[] limpo = [0x00000000, 0xFF102030, 0x00FFFFFF, 0xFFFFFFFF];
        Afirmar.Nulo(ValidadorDeClipes.PixelSemitransparente(limpo, 2), "só 0 e 255");
        foreach (uint alfa in new uint[] { 1, 128, 254 })
        {
            uint[] sujo = [0x00000000, 0xFF102030, (alfa << 24) | 0x00405060, 0xFFFFFFFF];
            Afirmar.Igual(((int X, int Y, int Alfa)?)(0, 1, (int)alfa), ValidadorDeClipes.PixelSemitransparente(sujo, 2), $"alfa {alfa}");
        }
    }

    // ------------------------------------------------------------------ a escolha pelo manifesto

    /// <summary>As situações que a Fase 6 animou de propósito (passo F6-P4), fora da comparação com a Fase 5.</summary>
    internal static readonly string[] Animadas =
        ["caindo", "reagindo", "gesto-espiar", "gesto-olharaoredor", "gesto-cocar", "gesto-espreguicar", "gesto-brincar", "gesto-gargalhada"];

    // Nenhum outro quadro muda (F6-P2): a escolha pelo manifesto é a da Fase 5, chamada por chamada, em todos os estados,
    // direções, gestos, dinâmicas e passos de 0 a 47 (duas voltas dos ciclos mais longos), com caras de humor, de sono e
    // de efeito; e, em USING, cada item, cada apoio e cada passo do uso. Só as situações animadas no passo F6-P4 ficam
    // de fora, com os testes delas (ClipesAnimadosTestes).
    [Teste]
    public void EscolhaPeloManifesto_IgualAEscolhaDaFase5()
    {
        Dinamica[] dinamicas =
        [
            default, new(1200, 0, false), new(-800, 1, false), new(-1000, 0, true), new(300, 2, false), new(0, 0, false, Agarrado: true),
            .. Enum.GetValues<LadoDoEsconderijo>().Where(l => l != LadoDoEsconderijo.Nenhum).Select(l => new Dinamica(0, 0, false, Esconderijo: l)),
        ];
        Expressao[] caras = [Expressao.Neutro, Expressao.Feliz, Expressao.Dormindo, Expressao.Bebado, Expressao.Paranoico];
        int comparados = 0;
        foreach (Estado estado in Enum.GetValues<Estado>())
        {
            Gesto[] gestos = estado == Estado.Idle ? Enum.GetValues<Gesto>() : [Gesto.Nenhum];
            foreach (Direcao direcao in Enum.GetValues<Direcao>())
                foreach (Gesto gesto in gestos)
                    foreach (Dinamica dinamica in dinamicas)
                        foreach (Expressao cara in caras)
                            for (long passos = 0; passos < 48; passos++)
                            {
                                Retrato r = R(estado, direcao, cara, gesto);
                                if (Animadas.Contains(PoseDoPersonagem.Situacao(r, passos, dinamica).Situacao)) continue;
                                QuadroDoSprite antes = PoseDaFase5.Pose(r, passos, dinamica), agora = PoseDoPersonagem.Escolher(r, passos, dinamica);
                                if (antes != agora) Afirmar.Igual(antes, agora, $"{estado}, {direcao}, {gesto}, {dinamica}, {cara}, passo {passos}");
                                comparados++;
                            }
        }
        foreach (Item item in Enum.GetValues<Item>())
        {
            DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
            foreach (ApoioDoUso apoio in Enum.GetValues<ApoioDoUso>())
                foreach (Dinamica dinamica in dinamicas)
                    foreach (Direcao direcao in Enum.GetValues<Direcao>())
                        for (int passo = 0; passo < dados.PassosDoUso; passo++)
                        {
                            Retrato r = R(Estado.Using, direcao, dados.CaraDurante) with { Uso = new Uso(item, dados.Verbo, dados.PassosDoUso, apoio), PassoDoUso = passo };
                            QuadroDoSprite antes = PoseDaFase5.Pose(r, passo, dinamica), agora = PoseDoPersonagem.Escolher(r, passo, dinamica);
                            if (antes != agora) Afirmar.Igual(antes, agora, $"uso de {item} em {apoio}, {direcao}, {dinamica}, passo {passo}");
                            comparados++;
                        }
        }
        Console.WriteLine($"         {comparados} escolhas comparadas");
    }
}
