using System.Text.Json;

namespace Buzzy.Visual.Animacao;

public enum OrigemDaCara
{
    // Do retrato do núcleo (emoção, onda ou troca de cara).
    Retrato,

    Pose,

    // Do retrato, mas a neutra cede pra da pose (no cipó, rindo).
    RetratoSemNeutro,
}

// Toon force.
public enum DeformacaoDoQuadro
{
    Nenhuma,

    // Mais largo e baixo: impacto.
    Achatado,

    // Mais estreito e alto: velocidade.
    Esticado,

    // Esticado só acima de VelocidadeDoEsticamento na vertical.
    PelaVelocidade,
}

// Passos = ticks do relógio lógico. Cara e Deformacao, se vierem, substituem as do clipe.
public sealed record QuadroDoClipe(string Pose, int Passos, string? Cara = null, DeformacaoDoQuadro? Deformacao = null);

// Espelha acompanha a direção: as poses de perfil olham pra direita.
public sealed record Clipe(string Situacao, IReadOnlyList<QuadroDoClipe> Quadros, bool Repete, OrigemDaCara Cara, bool Espelha, DeformacaoDoQuadro Deformacao)
{
    public int Duracao => Quadros.Sum(q => q.Passos);
}

// Mesmos valores no app e na validação do manifesto.
public static class Deformacoes
{
    public static readonly (double X, double Y) Achatado = (1.3, 0.7);

    public static readonly (double X, double Y) Esticado = (0.8, 1.25);

    // DIP/s, vertical.
    public const double VelocidadeDoEsticamento = 700;
}

// Lista fechada do que a apresentação pode pedir ao manifesto; ele tem exatamente um clipe por situação.
public static class Situacoes
{
    // Nome do gesto do núcleo em minúsculas.
    public static readonly IReadOnlyList<string> Gestos =
        ["espiar", "olharaoredor", "cocar", "espreguicar", "brincar", "soluco", "danca", "gargalhada", "espirro", "tosse", "tremedeira", "olharproteto", "agachar"];

    // Variantes da reação ao clique; a genérica é "reagindo".
    public static readonly IReadOnlyList<string> Reacoes = ["susto", "flagra", "empolgado", "preguica"];

    // Na ordem do manifesto.
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

    // Olhando a janela em primeiro plano, por curiosidade.
    public const string OlhandoJanela = "olhando-janela";

    // Espiando pra fora da lateral, virado pra fora.
    public const string EspiandoNaBorda = "espiando-na-borda";

    public static string DaReacao(string variante) => "reagindo-" + variante;

    public static string DoGesto(string gesto) => "gesto-" + gesto;
}

// Um clipe por situação, de um JSON versionado. Leitor estrito, sem JsonSerializer/reflexão: só
// campos conhecidos, listas fechadas e limites de tamanho. O núcleo não conhece isso: trocar a
// animação é trocar manifesto e poses.
public sealed class ManifestoDeClipes
{
    public const int Versao = 1;

    public const int MaximoDeQuadros = 32, MaximoDePassos = 600, MaximoDeCaracteres = 256 * 1024;

    private readonly Dictionary<string, Clipe> _clipes;

    private ManifestoDeClipes(Dictionary<string, Clipe> clipes) => _clipes = clipes;

    // Na ordem do arquivo.
    public IReadOnlyCollection<Clipe> Clipes => _clipes.Values;

    // A validação no build garante que todas as situações existem.
    public Clipe this[string situacao]
        => _clipes.TryGetValue(situacao, out Clipe? clipe) ? clipe : throw new KeyNotFoundException($"O manifesto não tem clipe para a situação \"{situacao}\".");

    public bool Tem(string situacao) => _clipes.ContainsKey(situacao);

    // Qualquer desvio do formato vira FormatException com o caminho do campo; nenhuma outra exceção
    // sai daqui. Não confere se poses e caras existem na arte: isso é do ValidadorDeClipes.
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
            // ArgumentException: surrogate cru (não escapado), que a conversão pra UTF-8 recusa.
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
                // Só pode ser escape de surrogate solto: os tipos já são conferidos antes de cada leitura.
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

    // Todos os obrigatórios, opcionais à vontade, nada repetido nem desconhecido.
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

// Quadro pelos passos do relógio lógico desde que entrou na situação. Clipe que repete dá a volta;
// o que não repete para no último. Relógio parado, quadro parado.
public static class ReprodutorDeClipes
{
    // Passo negativo vale 0.
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
