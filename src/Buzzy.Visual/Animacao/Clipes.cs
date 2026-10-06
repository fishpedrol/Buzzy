using System.Text.Json;

namespace Buzzy.Visual.Animacao;

/// <summary>De onde vem a cara de um clipe (DEC-036, item 1).</summary>
public enum OrigemDaCara
{
    /// <summary>A cara do retrato do núcleo (a emoção, a onda ou a troca de cara).</summary>
    Retrato,

    /// <summary>A cara própria da pose.</summary>
    Pose,

    /// <summary>A do retrato, mas a neutra dá lugar à da pose (no cipó, rindo; no esconderijo, a da pose).</summary>
    RetratoSemNeutro,
}

/// <summary>A deformação de desenho animado de um quadro (toon force, DEC-023).</summary>
public enum DeformacaoDoQuadro
{
    Nenhuma,

    /// <summary>Mais largo e mais baixo: o impacto.</summary>
    Achatado,

    /// <summary>Mais estreito e mais alto: a velocidade.</summary>
    Esticado,

    /// <summary>Esticado só com a velocidade vertical alta (<see cref="Deformacoes.VelocidadeDoEsticamento"/>); senão, nenhuma.</summary>
    PelaVelocidade,
}

/// <summary>
/// Um quadro de um clipe: a pose de <c>PosesPixel</c>, quantos passos do relógio lógico ele dura e, opcionais, a cara (o
/// nome de <c>Rostos.Expressoes</c>, que vale no lugar da origem do clipe) e a deformação (no lugar da do clipe).
/// </summary>
public sealed record QuadroDoClipe(string Pose, int Passos, string? Cara = null, DeformacaoDoQuadro? Deformacao = null);

/// <summary>
/// Um clipe do manifesto (DEC-036): os quadros de uma situação, se repete ou para no último, de onde vem a cara, se espelha
/// com a direção (as poses de perfil olham para a direita) e a deformação dos quadros que não têm a sua.
/// </summary>
public sealed record Clipe(string Situacao, IReadOnlyList<QuadroDoClipe> Quadros, bool Repete, OrigemDaCara Cara, bool Espelha, DeformacaoDoQuadro Deformacao)
{
    /// <summary>A soma dos passos dos quadros: a duração de uma volta do clipe.</summary>
    public int Duracao => Quadros.Sum(q => q.Passos);
}

/// <summary>
/// As escalas do achatar e do esticar de desenho animado (toon force, DEC-023), as mesmas para o app e para a validação
/// do manifesto, e a velocidade a partir da qual o corpo aparece esticado.
/// </summary>
public static class Deformacoes
{
    /// <summary>Escalas horizontal e vertical do corpo achatado no impacto.</summary>
    public static readonly (double X, double Y) Achatado = (1.3, 0.7);

    /// <summary>Escalas horizontal e vertical do corpo esticado pela velocidade.</summary>
    public static readonly (double X, double Y) Esticado = (0.8, 1.25);

    /// <summary>A partir desta velocidade vertical, em DIP/s, o corpo aparece esticado.</summary>
    public const double VelocidadeDoEsticamento = 700;
}

/// <summary>
/// As situações da apresentação (DEC-036, item 1): a lista fechada do que a escolha do quadro pode pedir ao manifesto. A
/// apresentação decide a situação pelo retrato e pela dinâmica; o manifesto tem exatamente um clipe para cada uma.
/// </summary>
public static class Situacoes
{
    /// <summary>Os gestos da agenda e da onda que têm clipe próprio, pelo nome do gesto do núcleo em minúsculas.</summary>
    public static readonly IReadOnlyList<string> Gestos =
        ["espiar", "olharaoredor", "cocar", "espreguicar", "brincar", "soluco", "danca", "gargalhada", "espirro", "tosse", "tremedeira", "olharproteto", "agachar"];

    /// <summary>As variantes da reação ao clique com clipe próprio (DEC-037, item 8); a de antes é <c>reagindo</c>.</summary>
    public static readonly IReadOnlyList<string> Reacoes = ["susto", "flagra", "empolgado", "preguica"];

    /// <summary>Todas as situações, na ordem do manifesto.</summary>
    public static readonly IReadOnlyList<string> Todas =
    [
        "parado", "andando", "escalando", "escalando-agarrado", "foguete", "cipo", "cipo-agarrado",
        "pulo", "quique-impacto", "quique-esticado", "quique-voo", "caindo", "pousando",
        "sentado", "dormindo", "segurado", "reagindo", "escondido", "escondido-pressionado",
        "uso-parede", "uso-cipo", "uso-esconderijo",
        .. Gestos.Select(DoGesto),
        .. Reacoes.Select(DaReacao),
        EspiandoNaBorda,
        OlhandoJanela,
    ];

    /// <summary>Olhar a janela em primeiro plano pela curiosidade (DEC-037, item 5), virado para ela.</summary>
    public const string OlhandoJanela = "olhando-janela";

    /// <summary>Espiar para fora da lateral explorada (DEC-037, item 9), virado para fora.</summary>
    public const string EspiandoNaBorda = "espiando-na-borda";

    /// <summary>A situação de uma variante da reação: <c>reagindo-</c> e o nome dela.</summary>
    public static string DaReacao(string variante) => "reagindo-" + variante;

    /// <summary>A situação de um gesto: <c>gesto-</c> e o nome dele em minúsculas.</summary>
    public static string DoGesto(string gesto) => "gesto-" + gesto;
}

/// <summary>
/// O manifesto de clipes (DEC-036): um clipe por situação, lido de um JSON com versão por um leitor estrito, sem
/// JsonSerializer nem reflexão: só os campos conhecidos, listas fechadas e limites de tamanho (SECURITY.md 7). O núcleo
/// não o conhece; trocar a animação é trocar o manifesto e as poses.
/// </summary>
public sealed class ManifestoDeClipes
{
    /// <summary>A versão do formato que este leitor entende.</summary>
    public const int Versao = 1;

    /// <summary>Limites do formato: quadros por clipe, passos por quadro e tamanho do texto.</summary>
    public const int MaximoDeQuadros = 32, MaximoDePassos = 600, MaximoDeCaracteres = 256 * 1024;

    private readonly Dictionary<string, Clipe> _clipes;

    private ManifestoDeClipes(Dictionary<string, Clipe> clipes) => _clipes = clipes;

    /// <summary>Os clipes, na ordem do arquivo.</summary>
    public IReadOnlyCollection<Clipe> Clipes => _clipes.Values;

    /// <summary>O clipe de uma situação; lança se o manifesto não tem (a validação o impede no build).</summary>
    public Clipe this[string situacao]
        => _clipes.TryGetValue(situacao, out Clipe? clipe) ? clipe : throw new KeyNotFoundException($"O manifesto não tem clipe para a situação \"{situacao}\".");

    /// <summary>Se o manifesto tem o clipe de uma situação.</summary>
    public bool Tem(string situacao) => _clipes.ContainsKey(situacao);

    /// <summary>
    /// Lê o manifesto. Lança <see cref="FormatException"/> com o caminho do campo em qualquer desvio do formato: versão,
    /// campo desconhecido ou ausente, tipo errado, situação fora de <see cref="Situacoes.Todas"/> ou repetida, enumerado
    /// fora da lista, quadros ou passos fora dos limites, campo repetido ou texto que não é UTF-16 válido (um escape de
    /// surrogate solto); nenhuma outra exceção sai daqui. Não confere se as poses e as caras existem na arte: isso é da
    /// validação (<see cref="ValidadorDeClipes"/>).
    /// </summary>
    public static ManifestoDeClipes Ler(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximoDeCaracteres) throw new FormatException($"Manifesto com mais de {MaximoDeCaracteres} caracteres.");
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false });
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            // ArgumentException: um surrogate cru no texto (não escapado), que a transcodificação para UTF-8 recusa.
            throw new FormatException($"Manifesto não é JSON válido: {e.Message}", e);
        }
        using (documento)
        {
            try
            {
                return LerDocumento(documento);
            }
            catch (InvalidOperationException e)
            {
                // Só o System.Text.Json transcodificando um texto com surrogate solto (num nome ou num valor): os tipos dos
                // elementos são conferidos antes de cada leitura.
                throw new FormatException("Manifesto com texto que não é UTF-16 válido (um escape de surrogate solto).", e);
            }
        }
    }

    private static ManifestoDeClipes LerDocumento(JsonDocument documento)
    {
        {
            JsonElement raiz = Objeto(documento.RootElement, "manifesto", ["versao", "clipes"], []);
            int versao = Inteiro(raiz.GetProperty("versao"), "versao");
            if (versao != Versao) throw new FormatException($"versao: {versao}, e este leitor entende a {Versao}.");
            JsonElement lista = raiz.GetProperty("clipes");
            if (lista.ValueKind != JsonValueKind.Array) throw new FormatException("clipes: não é uma lista.");
            var clipes = new Dictionary<string, Clipe>(StringComparer.Ordinal);
            int i = 0;
            foreach (JsonElement elemento in lista.EnumerateArray())
            {
                Clipe clipe = LerClipe(elemento, $"clipes[{i++}]");
                if (!clipes.TryAdd(clipe.Situacao, clipe)) throw new FormatException($"clipes: a situação \"{clipe.Situacao}\" aparece duas vezes.");
            }
            return new ManifestoDeClipes(clipes);
        }
    }

    private static Clipe LerClipe(JsonElement e, string onde)
    {
        e = Objeto(e, onde, ["situacao", "quadros", "repete", "cara", "espelha"], ["deformacao"]);
        string situacao = Texto(e.GetProperty("situacao"), $"{onde}.situacao");
        if (!Situacoes.Todas.Contains(situacao, StringComparer.Ordinal)) throw new FormatException($"{onde}.situacao: \"{situacao}\" não é uma situação conhecida.");
        JsonElement quadros = e.GetProperty("quadros");
        if (quadros.ValueKind != JsonValueKind.Array) throw new FormatException($"{onde}.quadros: não é uma lista.");
        int n = quadros.GetArrayLength();
        if (n is < 1 or > MaximoDeQuadros) throw new FormatException($"{onde}.quadros: {n} quadros; vale de 1 a {MaximoDeQuadros}.");
        var lidos = new List<QuadroDoClipe>(n);
        int j = 0;
        foreach (JsonElement q in quadros.EnumerateArray()) lidos.Add(LerQuadro(q, $"{onde}.quadros[{j++}]"));
        DeformacaoDoQuadro deformacao = e.TryGetProperty("deformacao", out JsonElement d) ? Deformacao(d, $"{onde}.deformacao") : DeformacaoDoQuadro.Nenhuma;
        return new Clipe(situacao, lidos, Booleano(e.GetProperty("repete"), $"{onde}.repete"), Cara(e.GetProperty("cara"), $"{onde}.cara"),
            Booleano(e.GetProperty("espelha"), $"{onde}.espelha"), deformacao);
    }

    private static QuadroDoClipe LerQuadro(JsonElement e, string onde)
    {
        e = Objeto(e, onde, ["pose", "passos"], ["cara", "deformacao"]);
        string pose = Texto(e.GetProperty("pose"), $"{onde}.pose");
        int passos = Inteiro(e.GetProperty("passos"), $"{onde}.passos");
        if (passos is < 1 or > MaximoDePassos) throw new FormatException($"{onde}.passos: {passos}; vale de 1 a {MaximoDePassos}.");
        string? cara = e.TryGetProperty("cara", out JsonElement c) ? Texto(c, $"{onde}.cara") : null;
        DeformacaoDoQuadro? deformacao = e.TryGetProperty("deformacao", out JsonElement d) ? Deformacao(d, $"{onde}.deformacao") : null;
        return new QuadroDoClipe(pose, passos, cara, deformacao);
    }

    /// <summary>Um objeto com exatamente os campos obrigatórios e, no máximo, os opcionais, cada um uma vez só.</summary>
    private static JsonElement Objeto(JsonElement e, string onde, string[] obrigatorios, string[] opcionais)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new FormatException($"{onde}: não é um objeto.");
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty p in e.EnumerateObject())
        {
            if (!obrigatorios.Contains(p.Name, StringComparer.Ordinal) && !opcionais.Contains(p.Name, StringComparer.Ordinal))
                throw new FormatException($"{onde}: campo desconhecido \"{p.Name}\".");
            if (!vistos.Add(p.Name)) throw new FormatException($"{onde}: o campo \"{p.Name}\" aparece duas vezes.");
        }
        foreach (string campo in obrigatorios)
            if (!e.TryGetProperty(campo, out _)) throw new FormatException($"{onde}: falta o campo \"{campo}\".");
        return e;
    }

    private static string Texto(JsonElement e, string onde)
    {
        if (e.ValueKind != JsonValueKind.String) throw new FormatException($"{onde}: não é texto.");
        string t = e.GetString()!;
        if (t.Length is 0 or > 64 || !t.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            throw new FormatException($"{onde}: \"{t}\" não é um nome (de 1 a 64 letras minúsculas, dígitos e hífens).");
        return t;
    }

    private static int Inteiro(JsonElement e, string onde)
        => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? v : throw new FormatException($"{onde}: não é um número inteiro.");

    private static bool Booleano(JsonElement e, string onde) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new FormatException($"{onde}: não é verdadeiro nem falso."),
    };

    private static OrigemDaCara Cara(JsonElement e, string onde) => Texto(e, onde) switch
    {
        "retrato" => OrigemDaCara.Retrato,
        "pose" => OrigemDaCara.Pose,
        "retrato-sem-neutro" => OrigemDaCara.RetratoSemNeutro,
        string t => throw new FormatException($"{onde}: \"{t}\" não é retrato, pose nem retrato-sem-neutro."),
    };

    private static DeformacaoDoQuadro Deformacao(JsonElement e, string onde) => Texto(e, onde) switch
    {
        "nenhuma" => DeformacaoDoQuadro.Nenhuma,
        "achatado" => DeformacaoDoQuadro.Achatado,
        "esticado" => DeformacaoDoQuadro.Esticado,
        "pela-velocidade" => DeformacaoDoQuadro.PelaVelocidade,
        string t => throw new FormatException($"{onde}: \"{t}\" não é nenhuma, achatado, esticado nem pela-velocidade."),
    };
}

/// <summary>
/// O reprodutor de clipes (DEC-036, item 3): o quadro de um clipe pelos passos do relógio lógico desde a entrada na
/// situação. O clipe que repete volta ao começo; o que não repete para no último quadro. Sem relógio, os passos não andam,
/// e o quadro não muda (DEC-011).
/// </summary>
public static class ReprodutorDeClipes
{
    /// <summary>O índice e o quadro do clipe no passo <paramref name="passos"/> (negativo vale 0).</summary>
    public static (int Indice, QuadroDoClipe Quadro) Quadro(Clipe clipe, long passos)
    {
        ArgumentNullException.ThrowIfNull(clipe);
        long t = Math.Max(0, passos);
        int duracao = clipe.Duracao;
        t = clipe.Repete ? t % duracao : Math.Min(t, duracao - 1);
        for (int i = 0; i < clipe.Quadros.Count; i++)
        {
            if (t < clipe.Quadros[i].Passos) return (i, clipe.Quadros[i]);
            t -= clipe.Quadros[i].Passos;
        }
        return (clipe.Quadros.Count - 1, clipe.Quadros[^1]);
    }
}
