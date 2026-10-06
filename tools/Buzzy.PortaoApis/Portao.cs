using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml;

namespace Buzzy.PortaoApis;

/// <summary>Linha de comando ou pasta inválida, arquivo ausente: código de saída 2.</summary>
internal sealed class ErroDeUso(string mensagem) : Exception(mensagem);

/// <summary>Opções da linha de comando, com os caminhos já completos.</summary>
/// <param name="Binarios">A pasta de saída do build; com --pacote, o próprio executável de arquivo único.</param>
/// <param name="Aplicativo">
/// Nome do aplicativo, sem extensão: define &lt;nome&gt;.dll, &lt;nome&gt;.*.dll e &lt;nome&gt;.exe.
/// O padrão é Buzzy; outro nome serve para testar o portão num protótipo (--aplicativo BuzzySpike).
/// </param>
/// <param name="Runtimes">Pastas dos pacotes de runtime da Microsoft para a procedência (F9-P10); vazia no build comum.</param>
/// <param name="Pacote">O executável de arquivo único a verificar por dentro (F9-P10); nulo no modo de pasta.</param>
/// <param name="HostDeArquivoUnico">O singlefilehost.exe do pacote de host que o SDK usou; obrigatório com --pacote.</param>
internal sealed record Opcoes(string Binarios, IReadOnlyList<string> Fontes, string? Manifesto, string Aplicativo)
{
    public IReadOnlyList<string> Runtimes { get; init; } = [];
    public string? Pacote { get; init; }
    public string? HostDeArquivoUnico { get; init; }

    public const string Uso = """
        Uso: Buzzy.PortaoApis (--binarios <pasta> | --pacote <arquivo.exe> --host-de-arquivo-unico <singlefilehost.exe>)
                              --fonte <pasta> [--fonte <pasta> ...] [--runtime <pasta> ...] [--manifesto <arquivo>] [--aplicativo <nome>]

          --binarios               pasta de saída do build, com Buzzy.exe, Buzzy.dll e Buzzy.*.dll
          --pacote                 o Buzzy.exe de arquivo único (F9-P10): o host, os assemblies do produto lidos de dentro
                                   dele e a procedência de todo o resto
          --host-de-arquivo-unico  o singlefilehost.exe do pacote de host que o SDK usou (obrigatório com --pacote)
          --runtime                pasta de um pacote de runtime da Microsoft (repetível): binário que não é do produto só
                                   passa com o mesmo nome e o mesmo SHA-256 de um arquivo dele (obrigatório com --pacote)
          --fonte                  pasta de código-fonte; todos os .cs, sem descer em bin/ e obj/ (repetível)
          --manifesto              app.manifest do aplicativo (opcional)
          --aplicativo             nome do aplicativo, sem extensão (padrão: Buzzy)

        Código de saída: 0 sem violações; 1 com violações; 2 erro de uso ou de leitura.
        """;

    /// <summary>Interpreta os argumentos; nulo quando foi pedida a ajuda.</summary>
    public static Opcoes? Interpretar(IReadOnlyList<string> argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);
        string? binarios = null;
        string? pacote = null;
        string? host = null;
        string? manifesto = null;
        string? aplicativo = null;
        var fontes = new List<string>();
        var runtimes = new List<string>();

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
                case "--pacote":
                    if (pacote is not null) throw new ErroDeUso("--pacote informado mais de uma vez.");
                    pacote = Valor(argumentos, ref i);
                    break;
                case "--host-de-arquivo-unico":
                    if (host is not null) throw new ErroDeUso("--host-de-arquivo-unico informado mais de uma vez.");
                    host = Valor(argumentos, ref i);
                    break;
                case "--runtime":
                    runtimes.Add(Valor(argumentos, ref i));
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

        if (binarios is not null && pacote is not null) throw new ErroDeUso("--binarios e --pacote não vão juntos: uma execução verifica a pasta ou o pacote.");
        if (binarios is null && pacote is null) throw new ErroDeUso("Falta --binarios <pasta>.");
        if (pacote is not null && host is null) throw new ErroDeUso("--pacote precisa de --host-de-arquivo-unico <singlefilehost.exe>.");
        if (pacote is not null && runtimes.Count == 0) throw new ErroDeUso("--pacote precisa de pelo menos um --runtime <pasta>.");
        if (host is not null && pacote is null) throw new ErroDeUso("--host-de-arquivo-unico só vale com --pacote.");
        if (fontes.Count == 0) throw new ErroDeUso("Falta pelo menos um --fonte <pasta>.");
        return new Opcoes(
            Caminho(pacote ?? binarios!),
            [.. fontes.Select(Caminho)],
            manifesto is null ? null : Caminho(manifesto),
            aplicativo ?? "Buzzy")
        {
            Runtimes = [.. runtimes.Select(Caminho)],
            Pacote = pacote is null ? null : Caminho(pacote),
            HostDeArquivoUnico = host is null ? null : Caminho(host),
        };
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
    IReadOnlyList<UsoRestritoVisto> UsosRestritos)
{
    /// <summary>Binários de fora do produto aceitos por procedência dos pacotes de runtime (F9-P10).</summary>
    public int DoRuntime { get; init; }

    /// <summary>Com --pacote: o que o pacote tem e o que se conferiu nele (nulo no modo de pasta).</summary>
    public string? ResumoDoPacote { get; init; }
}

/// <summary>
/// Portão de APIs proibidas do build (SECURITY.md 3.2 e 8, item 1). Verifica, nesta ordem:
/// os assemblies gerenciados do produto (&lt;aplicativo&gt;.dll e &lt;aplicativo&gt;.*.dll que não
/// sejam de teste), a tabela de importação nativa de cada binário do produto, o código-fonte de
/// cada --fonte e o manifesto. Com --pacote (F9-P10), lê o executável de arquivo único por dentro:
/// a procedência do host, os assemblies do produto empacotados, a procedência de todo o resto e o
/// manifesto embutido. Só lê arquivos.
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or XmlException or InvalidDataException)
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
        if (opcoes.Pacote is not null)
        {
            if (!File.Exists(opcoes.Pacote)) throw new ErroDeUso($"Pacote não encontrado: {opcoes.Pacote}");
            if (!File.Exists(opcoes.HostDeArquivoUnico)) throw new ErroDeUso($"Host de arquivo único não encontrado: {opcoes.HostDeArquivoUnico}");
        }
        else if (!Directory.Exists(opcoes.Binarios)) throw new ErroDeUso($"Pasta de binários não encontrada: {opcoes.Binarios}");
        foreach (string fonte in opcoes.Fontes)
        {
            if (!Directory.Exists(fonte)) throw new ErroDeUso($"Pasta de código-fonte não encontrada: {fonte}");
        }
        foreach (string runtime in opcoes.Runtimes)
        {
            if (!Directory.Exists(runtime)) throw new ErroDeUso($"Pacote de runtime não encontrado: {runtime}");
        }
        if (opcoes.Manifesto is not null && !File.Exists(opcoes.Manifesto))
            throw new ErroDeUso($"Manifesto não encontrado: {opcoes.Manifesto}");

        var violacoes = new List<Violacao>();
        var permitidas = new List<Permitida>();
        var usosRestritos = new List<UsoRestritoVisto>();
        var binarios = new List<BinarioVerificado>();
        var referencias = new List<(string Assembly, string Referencia)>();
        ProcedenciaDoRuntime? procedencia = opcoes.Runtimes.Count > 0 ? ProcedenciaDoRuntime.Indexar(opcoes.Runtimes) : null;
        List<string> naoVerificados;
        int doRuntime = 0;
        string? resumoDoPacote = null;
        Func<string, bool> produtoPresente;

        if (opcoes.Pacote is null)
        {
            (string principal, List<string> bibliotecas, string executavel, List<string> fora) = Localizar(opcoes);
            naoVerificados = [];
            // DEC-040, item 6: um binário na pasta que não é do produto nem de teste não foi revisado (nenhuma dependência de
            // terceiros foi aceita): reprova, em vez de só aparecer no relatório como fora do portão. Num build autocontido
            // (F9-P10), o do runtime da Microsoft passa por procedência: mesmo nome e mesmo SHA-256 do pacote de runtime.
            foreach (string nome in fora)
            {
                if (nome.Contains("Teste", StringComparison.OrdinalIgnoreCase)) { naoVerificados.Add(nome); continue; }
                string caminho = Path.Combine(opcoes.Binarios, nome);
                if (procedencia is not null && procedencia.EhDoRuntime(nome, File.ReadAllBytes(caminho))) { doRuntime++; continue; }
                naoVerificados.Add(nome);
                violacoes.Add(new Violacao(caminho, 0, 0, Codigos.BinarioDeTerceiro, Categoria.CodigoDinamico, nome,
                    procedencia is null
                        ? "binário que não é do Buzzy na pasta de saída; nenhuma dependência de terceiros foi revisada (DEC-040, item 6)"
                        : "binário que não é do Buzzy nem, com o mesmo SHA-256, dos pacotes de runtime da Microsoft (DEC-040, item 6; F9-P10)", null));
            }

            foreach (string biblioteca in bibliotecas.Prepend(principal))
                binarios.Add(VerificarBinario(biblioteca, File.ReadAllBytes(biblioteca), ListaDePermissoes.Nenhuma, violacoes, permitidas, usosRestritos, referencias));
            binarios.Add(VerificarBinario(executavel, File.ReadAllBytes(executavel), ListaDePermissoes.Apphost, violacoes, permitidas, usosRestritos, referencias));
            produtoPresente = nome => File.Exists(Path.Combine(opcoes.Binarios, nome));

            // O runtimeconfig.json e o deps.json da pasta, quando existem (revisão adversarial do F9-P10).
            string runtimeconfig = Path.Combine(opcoes.Binarios, opcoes.Aplicativo + ".runtimeconfig.json");
            string deps = Path.Combine(opcoes.Binarios, opcoes.Aplicativo + ".deps.json");
            if (File.Exists(runtimeconfig)) violacoes.AddRange(VerificadorDeConfiguracaoDoRuntime.VerificarRuntimeConfig(runtimeconfig, File.ReadAllBytes(runtimeconfig)));
            if (File.Exists(deps)) violacoes.AddRange(VerificadorDeConfiguracaoDoRuntime.VerificarDeps(deps, File.ReadAllBytes(deps), opcoes.Aplicativo));
        }
        else
        {
            (naoVerificados, doRuntime, resumoDoPacote, produtoPresente) =
                VerificarPacote(opcoes, procedencia!, binarios, violacoes, permitidas, usosRestritos, referencias);
        }

        // Dependências do produto que não estão na pasta (ou no pacote) não teriam sido verificadas.
        string prefixo = opcoes.Aplicativo + ".";
        foreach ((string assembly, string referencia) in referencias)
        {
            if (referencia.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase) && !produtoPresente(referencia + ".dll"))
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

        return new ResultadoDoPortao(opcoes, binarios, naoVerificados, fontes, verificados.Count, violacoes, permitidas, usosRestritos)
        {
            DoRuntime = doRuntime,
            ResumoDoPacote = resumoDoPacote,
        };
    }

    /// <summary>
    /// O executável de arquivo único por dentro (F9-P10, DEC-042): o host é o singlefilehost.exe da Microsoft
    /// (<see cref="ProcedenciaDoHost"/>), com as importações da lista revisada (<see cref="PermissoesDoHostDeArquivoUnico"/>);
    /// o manifesto embutido passa pelas mesmas regras do app.manifest; os assemblies do produto, lidos do pacote, passam
    /// pelas regras de sempre; todo o resto é, com o mesmo nome e SHA-256, dos pacotes de runtime; deps.json e
    /// runtimeconfig.json são os únicos arquivos de dados aceitos. Qualquer outra coisa reprova (BZP006 ou BZP007).
    /// </summary>
    private static (List<string> NaoVerificados, int DoRuntime, string Resumo, Func<string, bool> ProdutoPresente) VerificarPacote(
        Opcoes opcoes,
        ProcedenciaDoRuntime procedencia,
        List<BinarioVerificado> binarios,
        List<Violacao> violacoes,
        List<Permitida> permitidas,
        List<UsoRestritoVisto> usosRestritos,
        List<(string, string)> referencias)
    {
        string exe = opcoes.Pacote!;
        PacoteDeArquivoUnico pacote;
        try
        {
            pacote = LeitorDePacote.Ler(exe) ?? throw new ErroDeUso($"{exe} não é um pacote de arquivo único (sem a assinatura do pacote no host).");
        }
        catch (InvalidDataException e)
        {
            throw new IOException($"pacote ilegível: {e.Message}", e);
        }

        string principal = opcoes.Aplicativo + ".dll";
        string prefixo = opcoes.Aplicativo + ".";

        // O host: procedência e importações.
        foreach (string diferenca in ProcedenciaDoHost.Comparar(exe, opcoes.HostDeArquivoUnico!, pacote, principal))
            violacoes.Add(new Violacao(exe, 0, 0, Codigos.HostDeArquivoUnico, Categoria.CodigoDinamico, Path.GetFileName(exe),
                $"o host não é o singlefilehost.exe do pacote da Microsoft: {diferenca}", null));
        binarios.Add(VerificarBinario(exe, File.ReadAllBytes(exe), ListaDePermissoes.HostDeArquivoUnico, violacoes, permitidas, usosRestritos, referencias,
            tipo: "host de arquivo único"));

        // O manifesto embutido: o mesmo que o app.manifest exige (sem elevação, PerMonitorV2).
        string? manifesto = ProcedenciaDoHost.Manifesto(exe);
        if (manifesto is null)
            violacoes.Add(new Violacao(exe, 0, 0, Codigos.Manifesto, Categoria.Manifesto, "RT_MANIFEST", "o pacote não tem o manifesto do aplicativo embutido", null));
        else
            violacoes.AddRange(VerificadorDeManifesto.VerificarTexto(exe + "!RT_MANIFEST", manifesto));

        var naoVerificados = new List<string>();
        var produto = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int doRuntime = 0, dados = 0;
        var configuracoes = new HashSet<TipoNoPacote>();
        foreach (EntradaDoPacote entrada in pacote.Entradas.OrderBy(e => e.Caminho, StringComparer.OrdinalIgnoreCase))
        {
            string nome = entrada.Caminho.Replace('\\', '/');
            string rotulo = $"{exe}!{nome}";
            bool naRaiz = !nome.Contains('/', StringComparison.Ordinal);
            bool doProduto = naRaiz && nome.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && (nome.Equals(principal, StringComparison.OrdinalIgnoreCase) || nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                && !nome.Contains("Teste", StringComparison.OrdinalIgnoreCase);

            if (entrada.Tipo is TipoNoPacote.DepsJson or TipoNoPacote.RuntimeConfigJson)
            {
                bool ehDeps = entrada.Tipo == TipoNoPacote.DepsJson && naRaiz && nome.Equals(opcoes.Aplicativo + ".deps.json", StringComparison.OrdinalIgnoreCase);
                bool ehConfiguracao = entrada.Tipo == TipoNoPacote.RuntimeConfigJson && naRaiz && nome.Equals(opcoes.Aplicativo + ".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
                if (ehDeps || ehConfiguracao)
                {
                    // O conteúdo, que o host lê antes do Main, por lista fechada (revisão adversarial do F9-P10, achado de alta).
                    byte[] conteudo = LeitorDePacote.Conteudo(exe, entrada);
                    violacoes.AddRange(ehDeps
                        ? VerificadorDeConfiguracaoDoRuntime.VerificarDeps(rotulo, conteudo, opcoes.Aplicativo)
                        : VerificadorDeConfiguracaoDoRuntime.VerificarRuntimeConfig(rotulo, conteudo));
                    configuracoes.Add(entrada.Tipo);
                    dados++;
                    continue;
                }
            }
            else if (doProduto && entrada.Tipo == TipoNoPacote.Assembly)
            {
                produto.Add(nome);
                binarios.Add(VerificarBinario(rotulo, LeitorDePacote.Conteudo(exe, entrada), ListaDePermissoes.Nenhuma, violacoes, permitidas, usosRestritos, referencias));
                continue;
            }
            else if (!doProduto && entrada.Tipo is TipoNoPacote.Assembly or TipoNoPacote.BinarioNativo or TipoNoPacote.Desconhecido
                && procedencia.EhDoRuntime(nome, LeitorDePacote.Conteudo(exe, entrada)))
            {
                doRuntime++;
                continue;
            }

            naoVerificados.Add(nome);
            violacoes.Add(new Violacao(rotulo, 0, 0, Codigos.BinarioDeTerceiro, Categoria.CodigoDinamico, nome,
                $"arquivo do pacote ({entrada.Tipo}) que não é do Buzzy, nem o deps.json ou o runtimeconfig.json dele, nem, com o mesmo SHA-256, dos pacotes de runtime da Microsoft (DEC-040, item 6; F9-P10)", null));
        }

        if (!produto.Contains(principal)) throw new ErroDeUso($"{principal} não está no pacote {exe}.");
        if (!configuracoes.Contains(TipoNoPacote.RuntimeConfigJson) || !configuracoes.Contains(TipoNoPacote.DepsJson))
            violacoes.Add(new Violacao(exe, 0, 0, Codigos.ConfiguracaoDoRuntime, Categoria.CodigoDinamico, "runtimeconfig.json/deps.json",
                $"o pacote não tem o {opcoes.Aplicativo}.runtimeconfig.json e o {opcoes.Aplicativo}.deps.json que o build grava", null));
        string resumo = $"pacote {pacote.VersaoMaior}.{pacote.VersaoMenor} com {pacote.Entradas.Count} arquivo(s): {produto.Count} do produto verificados por dentro, "
            + $"{doRuntime} do runtime da Microsoft por procedência (SHA-256), {dados} de configuração (deps.json e runtimeconfig.json), {naoVerificados.Count} fora disso; "
            + $"host conferido contra {opcoes.HostDeArquivoUnico}";
        return (naoVerificados, doRuntime, resumo, nome => produto.Contains(nome));
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
        byte[] conteudo,
        ListaDePermissoes lista,
        List<Violacao> violacoes,
        List<Permitida> permitidas,
        List<UsoRestritoVisto> usosRestritos,
        List<(string, string)> referencias,
        string? tipo = null)
    {
        using var pe = new PEReader(new MemoryStream(conteudo, writable: false));
        IReadOnlyList<ImportacaoNativa> importacoes = LeitorDeImportacoesNativas.Ler(pe);
        string resumoDeImportacoes = $"{importacoes.Count} importação(ões) nativa(s) de {importacoes.Select(i => i.Modulo.ToLowerInvariant()).Distinct().Count()} módulo(s)";

        if (pe.HasMetadata)
        {
            AnaliseDeAssembly analise = VerificadorDeAssembly.Verificar(caminho, pe.GetMetadataReader());
            violacoes.AddRange(analise.Violacoes);
            usosRestritos.AddRange(analise.UsosRestritos);
            referencias.AddRange(analise.AssembliesReferenciados.Select(r => (caminho, r)));
            // Um .exe gerenciado não é o apphost: não recebe a lista de permissões.
            AnaliseDeImportacoes nativas = VerificadorDeImportacoesNativas.Avaliar(caminho, importacoes, ListaDePermissoes.Nenhuma);
            violacoes.AddRange(nativas.Violacoes);
            return new BinarioVerificado(caminho, "assembly gerenciado",
                $"{analise.PInvokes} P/Invoke(s), {analise.ReferenciasATipos} referência(s) a tipos, {analise.ReferenciasAMembros} a membros, {resumoDeImportacoes}");
        }

        AnaliseDeImportacoes analiseNativa = VerificadorDeImportacoesNativas.Avaliar(caminho, importacoes, lista);
        violacoes.AddRange(analiseNativa.Violacoes);
        permitidas.AddRange(analiseNativa.Permitidas);
        return new BinarioVerificado(caminho, tipo ?? (lista == ListaDePermissoes.Apphost ? "apphost nativo" : "binário nativo"), resumoDeImportacoes);
    }
}
