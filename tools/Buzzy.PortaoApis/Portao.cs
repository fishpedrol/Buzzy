using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml;

namespace Buzzy.PortaoApis;

/// <summary>Linha de comando ou pasta inválida, arquivo ausente: código de saída 2.</summary>
internal sealed class ErroDeUso(string mensagem) : Exception(mensagem);

/// <summary>Opções da linha de comando, com os caminhos já completos.</summary>
/// <param name="Aplicativo">
/// Nome do aplicativo, sem extensão: define &lt;nome&gt;.dll, &lt;nome&gt;.*.dll e &lt;nome&gt;.exe.
/// O padrão é Buzzy; outro nome serve para testar o portão num protótipo (--aplicativo BuzzySpike).
/// </param>
internal sealed record Opcoes(string Binarios, IReadOnlyList<string> Fontes, string? Manifesto, string Aplicativo)
{
    public const string Uso = """
        Uso: Buzzy.PortaoApis --binarios <pasta> --fonte <pasta> [--fonte <pasta> ...] [--manifesto <arquivo>] [--aplicativo <nome>]

          --binarios    pasta de saída do build, com Buzzy.exe, Buzzy.dll e Buzzy.*.dll
          --fonte       pasta de código-fonte; todos os .cs, sem descer em bin/ e obj/ (repetível)
          --manifesto   app.manifest do aplicativo (opcional)
          --aplicativo  nome do aplicativo, sem extensão (padrão: Buzzy)

        Código de saída: 0 sem violações; 1 com violações; 2 erro de uso ou de leitura.
        """;

    /// <summary>Interpreta os argumentos; nulo quando foi pedida a ajuda.</summary>
    public static Opcoes? Interpretar(IReadOnlyList<string> argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);
        string? binarios = null;
        string? manifesto = null;
        string? aplicativo = null;
        var fontes = new List<string>();

        for (int i = 0; i < argumentos.Count; i++)
        {
            string argumento = argumentos[i];
            switch (argumento)
            {
                case "--ajuda" or "--help" or "-h" or "/?":
                    return null;
                case "--binarios":
                    if (binarios is not null) throw new ErroDeUso("--binarios informado mais de uma vez.");
                    binarios = Valor(argumentos, ref i);
                    break;
                case "--fonte":
                    fontes.Add(Valor(argumentos, ref i));
                    break;
                case "--manifesto":
                    if (manifesto is not null) throw new ErroDeUso("--manifesto informado mais de uma vez.");
                    manifesto = Valor(argumentos, ref i);
                    break;
                case "--aplicativo":
                    if (aplicativo is not null) throw new ErroDeUso("--aplicativo informado mais de uma vez.");
                    aplicativo = Valor(argumentos, ref i);
                    if (aplicativo.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0 || aplicativo.Trim().Length == 0)
                        throw new ErroDeUso($"--aplicativo precisa ser um nome simples, sem caminho: \"{aplicativo}\".");
                    break;
                default:
                    throw new ErroDeUso($"Argumento não reconhecido: {argumento}");
            }
        }

        if (binarios is null) throw new ErroDeUso("Falta --binarios <pasta>.");
        if (fontes.Count == 0) throw new ErroDeUso("Falta pelo menos um --fonte <pasta>.");
        return new Opcoes(
            Caminho(binarios),
            [.. fontes.Select(Caminho)],
            manifesto is null ? null : Caminho(manifesto),
            aplicativo ?? "Buzzy");
    }

    private static string Valor(IReadOnlyList<string> argumentos, ref int i)
    {
        if (i + 1 >= argumentos.Count || argumentos[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ErroDeUso($"{argumentos[i]} precisa de um valor.");
        return argumentos[++i];
    }

    private static string Caminho(string valor)
    {
        // Um caminho entre aspas que termina em barra invertida ("C:\pasta\") faz a barra escapar
        // a aspa, e o resto da linha de comando cola no caminho.
        if (valor.Contains('"', StringComparison.Ordinal))
            throw new ErroDeUso($"Caminho com aspas: {valor}. Uma barra invertida antes da aspa final a escapa; tire a barra final ou escreva \"$(OutDir).\".");
        return Path.GetFullPath(valor.Trim());
    }
}

/// <summary>Um binário do produto e o que o portão viu nele.</summary>
internal sealed record BinarioVerificado(string Caminho, string Tipo, string Resumo);

/// <summary>Tudo o que uma execução do portão verificou e encontrou.</summary>
/// <param name="Fontes">Cada --fonte com o número de arquivos .cs encontrados nela.</param>
/// <param name="ArquivosDeFonte">Arquivos .cs verificados, cada um uma vez, mesmo com pastas repetidas ou aninhadas.</param>
/// <param name="UsosRestritos">P/Invokes permitidos só no lugar de um uso restrito (<see cref="PortaoApis.UsosRestritos"/>).</param>
internal sealed record ResultadoDoPortao(
    Opcoes Opcoes,
    IReadOnlyList<BinarioVerificado> Binarios,
    IReadOnlyList<string> NaoVerificados,
    IReadOnlyList<(string Pasta, int Arquivos)> Fontes,
    int ArquivosDeFonte,
    IReadOnlyList<Violacao> Violacoes,
    IReadOnlyList<Permitida> Permitidas,
    IReadOnlyList<UsoRestritoVisto> UsosRestritos);

/// <summary>
/// Portão de APIs proibidas do build (SECURITY.md 3.2 e 8, item 1). Verifica, nesta ordem:
/// os assemblies gerenciados do produto (&lt;aplicativo&gt;.dll e &lt;aplicativo&gt;.*.dll que não
/// sejam de teste), a tabela de importação nativa de cada binário do produto, o código-fonte de
/// cada --fonte e o manifesto. Só lê arquivos.
/// </summary>
internal static class Portao
{
    public const int SemViolacoes = 0;
    public const int ComViolacoes = 1;
    public const int ErroDeUsoOuLeitura = 2;

    public static int Executar(IReadOnlyList<string> argumentos, TextWriter saida, TextWriter erros)
    {
        ArgumentNullException.ThrowIfNull(saida);
        ArgumentNullException.ThrowIfNull(erros);
        try
        {
            Opcoes? opcoes = Opcoes.Interpretar(argumentos);
            if (opcoes is null)
            {
                saida.WriteLine(Opcoes.Uso);
                return SemViolacoes;
            }

            ResultadoDoPortao resultado = Verificar(opcoes);
            Relatorio.Escrever(resultado, saida);
            return resultado.Violacoes.Count > 0 ? ComViolacoes : SemViolacoes;
        }
        catch (ErroDeUso e)
        {
            Relatorio.EscreverErro(e.Message, erros);
            erros.WriteLine();
            erros.WriteLine(Opcoes.Uso);
            return ErroDeUsoOuLeitura;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or XmlException)
        {
            Relatorio.EscreverErro($"não foi possível ler: {e.Message}", erros);
            return ErroDeUsoOuLeitura;
        }
        catch (Exception e)
        {
            // Um portão que falha sem conseguir verificar reprova o build do mesmo jeito, com
            // o código de erro de leitura em vez de uma queda sem explicação.
            Relatorio.EscreverErro($"erro inesperado ao verificar ({e.GetType().Name}): {e.Message}", erros);
            erros.WriteLine(e.ToString());
            return ErroDeUsoOuLeitura;
        }
    }

    public static ResultadoDoPortao Verificar(Opcoes opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        if (!Directory.Exists(opcoes.Binarios)) throw new ErroDeUso($"Pasta de binários não encontrada: {opcoes.Binarios}");
        foreach (string fonte in opcoes.Fontes)
        {
            if (!Directory.Exists(fonte)) throw new ErroDeUso($"Pasta de código-fonte não encontrada: {fonte}");
        }
        if (opcoes.Manifesto is not null && !File.Exists(opcoes.Manifesto))
            throw new ErroDeUso($"Manifesto não encontrado: {opcoes.Manifesto}");

        var violacoes = new List<Violacao>();
        var permitidas = new List<Permitida>();
        var usosRestritos = new List<UsoRestritoVisto>();
        var binarios = new List<BinarioVerificado>();

        (string principal, List<string> bibliotecas, string executavel, List<string> naoVerificados) = Localizar(opcoes);

        // DEC-040, item 6: um binário na pasta que não é do produto nem de teste não foi revisado (nenhuma dependência de
        // terceiros foi aceita): reprova, em vez de só aparecer no relatório como fora do portão.
        foreach (string nome in naoVerificados.Where(n => !n.Contains("Teste", StringComparison.OrdinalIgnoreCase)))
            violacoes.Add(new Violacao(Path.Combine(opcoes.Binarios, nome), 0, 0, Codigos.BinarioDeTerceiro, Categoria.CodigoDinamico, nome,
                "binário que não é do Buzzy na pasta de saída; nenhuma dependência de terceiros foi revisada (DEC-040, item 6)", null));

        var referencias = new List<(string Assembly, string Referencia)>();
        foreach (string biblioteca in bibliotecas.Prepend(principal))
            binarios.Add(VerificarBinario(biblioteca, ehExecutavel: false, violacoes, permitidas, usosRestritos, referencias));
        binarios.Add(VerificarBinario(executavel, ehExecutavel: true, violacoes, permitidas, usosRestritos, referencias));

        // Dependências do produto que não estão na pasta não teriam sido verificadas.
        string prefixo = opcoes.Aplicativo + ".";
        foreach ((string assembly, string referencia) in referencias)
        {
            if (referencia.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
                && !File.Exists(Path.Combine(opcoes.Binarios, referencia + ".dll")))
                throw new ErroDeUso($"{Path.GetFileName(assembly)} referencia {referencia}, que não está em {opcoes.Binarios}; o portão não pode verificá-lo.");
        }

        var fontes = new List<(string, int)>();
        var verificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pasta in opcoes.Fontes)
        {
            IReadOnlyList<string> arquivos = VerificadorDeFonte.ListarArquivos(pasta);
            if (arquivos.Count == 0) throw new ErroDeUso($"Nenhum arquivo .cs em {pasta} (bin/ e obj/ não contam).");
            // Pastas repetidas ou aninhadas não acusam o mesmo arquivo duas vezes.
            foreach (string arquivo in arquivos.Where(verificados.Add))
                violacoes.AddRange(VerificadorDeFonte.VerificarArquivo(arquivo));
            fontes.Add((pasta, arquivos.Count));
        }

        if (opcoes.Manifesto is not null) violacoes.AddRange(VerificadorDeManifesto.Verificar(opcoes.Manifesto));

        return new ResultadoDoPortao(opcoes, binarios, naoVerificados, fontes, verificados.Count, violacoes, permitidas, usosRestritos);
    }

    private static (string Principal, List<string> Bibliotecas, string Executavel, List<string> NaoVerificados) Localizar(Opcoes opcoes)
    {
        string principal = Path.Combine(opcoes.Binarios, opcoes.Aplicativo + ".dll");
        if (!File.Exists(principal)) throw new ErroDeUso($"{opcoes.Aplicativo}.dll não encontrado em {opcoes.Binarios}.");
        string executavel = Path.Combine(opcoes.Binarios, opcoes.Aplicativo + ".exe");
        if (!File.Exists(executavel)) throw new ErroDeUso($"{opcoes.Aplicativo}.exe não encontrado em {opcoes.Binarios}; o portão precisa verificar o lançador.");

        string prefixo = opcoes.Aplicativo + ".";
        var bibliotecas = new List<string>();
        var naoVerificados = new List<string>();
        foreach (string arquivo in Directory.EnumerateFiles(opcoes.Binarios).Order(StringComparer.OrdinalIgnoreCase))
        {
            string nome = Path.GetFileName(arquivo);
            string extensao = Path.GetExtension(nome);
            bool binario = extensao.Equals(".dll", StringComparison.OrdinalIgnoreCase) || extensao.Equals(".exe", StringComparison.OrdinalIgnoreCase);
            if (!binario || arquivo.Equals(principal, StringComparison.OrdinalIgnoreCase) || arquivo.Equals(executavel, StringComparison.OrdinalIgnoreCase))
                continue;

            bool doProduto = extensao.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                && nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase);
            // Assemblies de teste ficam de fora (Buzzy.App.Testes.dll e afins).
            bool deTeste = nome.Contains("Teste", StringComparison.OrdinalIgnoreCase);
            if (doProduto && !deTeste) bibliotecas.Add(arquivo);
            else naoVerificados.Add(nome + (doProduto ? " (teste)" : ""));
        }
        return (principal, bibliotecas, executavel, naoVerificados);
    }

    private static BinarioVerificado VerificarBinario(
        string caminho,
        bool ehExecutavel,
        List<Violacao> violacoes,
        List<Permitida> permitidas,
        List<UsoRestritoVisto> usosRestritos,
        List<(string, string)> referencias)
    {
        using FileStream arquivo = File.OpenRead(caminho);
        using var pe = new PEReader(arquivo);
        IReadOnlyList<ImportacaoNativa> importacoes = LeitorDeImportacoesNativas.Ler(pe);
        string resumoDeImportacoes = $"{importacoes.Count} importação(ões) nativa(s) de {importacoes.Select(i => i.Modulo.ToLowerInvariant()).Distinct().Count()} módulo(s)";

        if (pe.HasMetadata)
        {
            AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho, pe.GetMetadataReader());
            violacoes.AddRange(analise.Violacoes);
            usosRestritos.AddRange(analise.UsosRestritos);
            referencias.AddRange(analise.AssembliesReferenciados.Select(r => (caminho, r)));
            // Um .exe gerenciado não é o apphost: não recebe a lista de permissões.
            AnaliseDeImportacoes nativas = VerificadorDeImportacoesNativas.Avaliar(caminho, importacoes, ehApphost: false);
            violacoes.AddRange(nativas.Violacoes);
            return new BinarioVerificado(caminho, "assembly gerenciado",
                $"{analise.PInvokes} P/Invoke(s), {analise.ReferenciasATipos} referência(s) a tipos, {analise.ReferenciasAMembros} a membros, {resumoDeImportacoes}");
        }

        AnaliseDeImportacoes analiseNativa = VerificadorDeImportacoesNativas.Avaliar(caminho, importacoes, ehApphost: ehExecutavel);
        violacoes.AddRange(analiseNativa.Violacoes);
        permitidas.AddRange(analiseNativa.Permitidas);
        return new BinarioVerificado(caminho, ehExecutavel ? "apphost nativo" : "binário nativo", resumoDeImportacoes);
    }
}
