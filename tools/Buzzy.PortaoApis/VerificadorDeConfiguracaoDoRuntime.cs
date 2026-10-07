using System.Text.Json;

namespace Buzzy.PortaoApis;

// runtimeconfig.json e deps.json por lista fechada. O runtime lê os dois antes do Main e carrega
// o que mandam: STARTUP_HOOKS, additionalProbingPaths, framework de fora ou pacote extra trariam
// código que nunca passou pelas regras do IL. Vale na pasta e dentro do pacote.
//
// O que o SDK 10.0.401 grava: runtimeconfig com runtimeOptions (tfm, frameworks ou
// includedFrameworks dos dois frameworks da Microsoft, três configProperties false); deps.json com
// runtimeTarget, compilationOptions, targets, libraries e, no autocontido, runtimes. Bibliotecas:
// projetos do Buzzy e os dois runtimepacks. Qualquer outra coisa reprova (BZP008) até ser revisada.
internal static class VerificadorDeConfiguracaoDoRuntime
{
    private static readonly string[] Frameworks = ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"];

    // Único valor aceito de cada uma.
    private static readonly Dictionary<string, bool> PropriedadesRevisadas = new(StringComparer.Ordinal)
    {
        ["System.Reflection.Metadata.MetadataUpdater.IsSupported"] = false,
        ["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"] = false,
        ["CSWINRT_USE_WINDOWS_UI_XAML_PROJECTIONS"] = false,
    };

    private static readonly string[] PacotesDeRuntime =
        ["runtimepack.Microsoft.NETCore.App.Runtime.win-x64/", "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/"];

    private static readonly JsonDocumentOptions Opcoes = new() { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false };

    public static IReadOnlyList<Violacao> VerificarRuntimeConfig(string origem, ReadOnlySpan<byte> conteudo)
    {
        var violacoes = new List<Violacao>();
        void Acusar(string api, string detalhe) => violacoes.Add(new Violacao(origem, 0, 0, Codigos.ConfiguracaoDoRuntime, Categoria.CodigoDinamico, api, detalhe, null));

        using JsonDocument documento = Analisar(origem, conteudo);
        JsonElement raiz = documento.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("runtimeOptions", out JsonElement opcoes) || opcoes.ValueKind != JsonValueKind.Object)
        {
            Acusar("runtimeOptions", "sem o objeto runtimeOptions");
            return violacoes;
        }
        foreach (JsonProperty p in raiz.EnumerateObject().Where(p => p.Name != "runtimeOptions"))
            Acusar(p.Name, "chave da raiz que o build não grava");

        foreach (JsonProperty p in opcoes.EnumerateObject())
        {
            switch (p.Name)
            {
                case "tfm" when p.Value.ValueKind == JsonValueKind.String:
                    break;
                case "frameworks" or "includedFrameworks" when p.Value.ValueKind == JsonValueKind.Array:
                    foreach (JsonElement f in p.Value.EnumerateArray())
                    {
                        bool conhecido = f.ValueKind == JsonValueKind.Object
                            && f.EnumerateObject().All(c => c.Name is "name" or "version" && c.Value.ValueKind == JsonValueKind.String)
                            && f.TryGetProperty("name", out JsonElement nome) && Frameworks.Contains(nome.GetString());
                        if (!conhecido) Acusar($"{p.Name}: {f.GetRawText()}", "framework que não é um dos dois da Microsoft revisados");
                    }
                    break;
                case "configProperties" when p.Value.ValueKind == JsonValueKind.Object:
                    foreach (JsonProperty c in p.Value.EnumerateObject())
                    {
                        if (!PropriedadesRevisadas.TryGetValue(c.Name, out bool esperado))
                            Acusar(c.Name, "configProperty não revisada (STARTUP_HOOKS e afins carregariam código de fora antes do Main)");
                        else if (c.Value.ValueKind != (esperado ? JsonValueKind.True : JsonValueKind.False))
                            Acusar(c.Name, $"configProperty com valor diferente do revisado ({(esperado ? "true" : "false")})");
                    }
                    break;
                default:
                    Acusar(p.Name, "opção do runtime que o build não grava (additionalProbingPaths, rollForward e afins mudam o que o runtime carrega)");
                    break;
            }
        }
        return violacoes;
    }

    public static IReadOnlyList<Violacao> VerificarDeps(string origem, ReadOnlySpan<byte> conteudo, string aplicativo)
    {
        var violacoes = new List<Violacao>();
        void Acusar(string api, string detalhe) => violacoes.Add(new Violacao(origem, 0, 0, Codigos.ConfiguracaoDoRuntime, Categoria.CodigoDinamico, api, detalhe, null));
        bool DoProduto(string biblioteca) => biblioteca.StartsWith(aplicativo + "/", StringComparison.Ordinal) || biblioteca.StartsWith(aplicativo + ".", StringComparison.Ordinal);

        using JsonDocument documento = Analisar(origem, conteudo);
        JsonElement raiz = documento.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object)
        {
            Acusar("(raiz)", "o deps.json não é um objeto");
            return violacoes;
        }
        foreach (JsonProperty p in raiz.EnumerateObject())
        {
            switch (p.Name)
            {
                case "runtimeTarget" or "compilationOptions" or "runtimes":
                    break;
                case "libraries" when p.Value.ValueKind == JsonValueKind.Object:
                    foreach (JsonProperty b in p.Value.EnumerateObject())
                    {
                        string tipo = b.Value.ValueKind == JsonValueKind.Object && b.Value.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";
                        bool aceita = (tipo == "project" && DoProduto(b.Name)) || (tipo == "runtimepack" && PacotesDeRuntime.Any(r => b.Name.StartsWith(r, StringComparison.Ordinal)));
                        if (!aceita) Acusar($"{b.Name} ({tipo})", "biblioteca que não é um projeto do Buzzy nem um dos dois pacotes de runtime revisados");
                    }
                    break;
                case "targets" when p.Value.ValueKind == JsonValueKind.Object:
                    foreach (JsonProperty alvo in p.Value.EnumerateObject())
                    {
                        if (alvo.Value.ValueKind != JsonValueKind.Object) { Acusar(alvo.Name, "alvo que não é objeto"); continue; }
                        foreach (JsonProperty b in alvo.Value.EnumerateObject())
                        {
                            if (b.Value.ValueKind != JsonValueKind.Object) { Acusar(b.Name, "biblioteca do alvo que não é objeto"); continue; }
                            foreach (JsonProperty a in b.Value.EnumerateObject())
                            {
                                if (a.Name is not ("dependencies" or "runtime" or "native"))
                                    Acusar($"{b.Name}: {a.Name}", "recurso do alvo que o build não grava (runtimeTargets, resources e afins)");
                                else if (a.Name == "runtime" && DoProduto(b.Name) && a.Value.ValueKind == JsonValueKind.Object
                                    && a.Value.EnumerateObject().Any(r => r.Name.Contains('/', StringComparison.Ordinal) || r.Name.Contains('\\', StringComparison.Ordinal)))
                                    Acusar($"{b.Name}: runtime", "assembly do produto fora da raiz");
                            }
                        }
                    }
                    break;
                default:
                    Acusar(p.Name, "chave do deps.json que o build não grava");
                    break;
            }
        }
        return violacoes;
    }

    private static JsonDocument Analisar(string origem, ReadOnlySpan<byte> conteudo)
    {
        try
        {
            return JsonDocument.Parse(conteudo.ToArray(), Opcoes);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{origem}: JSON inválido ({e.Message})", e);
        }
    }
}
