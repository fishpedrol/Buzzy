using System.Text.RegularExpressions;
using Buzzy.PortaoApis.Testes.Amostras;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>
/// O portão inteiro, pela mesma entrada da linha de comando (Portao.Executar), sobre pastas
/// montadas num diretório temporário: binários, fontes e manifesto.
/// </summary>
public sealed class TestesDoPortao : IDisposable
{
    // Formato de erro do MSBuild: "origem: error CÓDIGO: texto", com (linha,coluna) opcionais.
    private static readonly Regex LinhaDeErro = new(@"^(?<origem>.+?)(\((?<linha>\d+),(?<coluna>\d+)\))?: error (?<codigo>BZP\d{3}): (?<texto>.+)$", RegexOptions.CultureInvariant);

    private readonly PastaTemporaria _pasta = new();

    public void Dispose() => _pasta.Dispose();

    private sealed record Produto(string Binarios, string Fonte, string Manifesto);

    private static (int Codigo, string Saida, string Erros) Rodar(params string[] argumentos)
    {
        using var saida = new StringWriter();
        using var erros = new StringWriter();
        int codigo = Portao.Executar(argumentos, saida, erros);
        return (codigo, saida.ToString(), erros.ToString());
    }

    private static (int Codigo, string Saida, string Erros) Rodar(Produto p, params string[] extras)
        => Rodar(["--binarios", p.Binarios, "--fonte", p.Fonte, "--manifesto", p.Manifesto, .. extras]);

    /// <summary>
    /// Pasta de produto limpa: Buzzy.dll e Buzzy.Core.dll sintéticos sem nada proibido, o
    /// apphost real como Buzzy.exe, um assembly de teste sujo que precisa ser ignorado, fonte limpa
    /// com lixo em obj/ e bin/, e manifesto correto.
    /// </summary>
    private Produto MontarProdutoLimpo()
    {
        string binarios = _pasta.Subpasta("bin");
        var buzzy = new AssemblySintetico("Buzzy");
        buzzy.ReferenciarAssembly("Buzzy.Core");
        buzzy.ReferenciarMembro("System.Diagnostics", "Process", "GetCurrentProcess");
        buzzy.DeclararPInvoke("user32.dll", "SetWindowPos");
        buzzy.DeclararPInvoke("shell32.dll", "Shell_NotifyIconW");
        buzzy.Gravar(Path.Combine(binarios, "Buzzy.dll"));

        var core = new AssemblySintetico("Buzzy.Core");
        core.ReferenciarMembro("System", "Math", "Round");
        core.Gravar(Path.Combine(binarios, "Buzzy.Core.dll"));

        var testes = new AssemblySintetico("Buzzy.App.Testes");
        testes.DeclararPInvoke("user32.dll", "SendInput");
        testes.Gravar(Path.Combine(binarios, "Buzzy.App.Testes.dll"));

        File.Copy(Repositorio.ApphostReal(), Path.Combine(binarios, "Buzzy.exe"));
        _pasta.Escrever(@"bin\Buzzy.deps.json", "{}");

        _pasta.Escrever(@"src\Limpo.cs", "namespace Buzzy;\n\n// Nada de SendInput aqui.\ninternal static class Limpo { }\n");
        _pasta.Escrever(@"src\obj\Release\Buzzy.GlobalUsings.g.cs", "global using System.Net.Http;\n");
        _pasta.Escrever(@"src\bin\Copia.cs", "SendInput();\n");
        string manifesto = _pasta.Escrever("app.manifest", TestesDoVerificadorDeManifesto.Manifesto());
        return new Produto(binarios, Path.Combine(_pasta.Caminho, "src"), manifesto);
    }

    [Teste]
    public void ProdutoLimpoAprovaEMostraAsPermissoesDoApphost()
    {
        Produto produto = MontarProdutoLimpo();
        (int codigo, string saida, string erros) = Rodar(produto);

        Afirmar.Igual(0, codigo, saida + erros);
        Afirmar.Igual("", erros);
        Afirmar.Contem("Resumo: APROVADO", saida);
        Afirmar.Igual(4, Regex.Matches(saida, "permitida no apphost - ").Count, saida);
        Afirmar.Contem("Buzzy.exe: SHELL32.dll!ShellExecuteW [Processos] permitida no apphost", saida);
        Afirmar.Contem("apphost nativo", saida);
        Afirmar.Contem("Buzzy.Core.dll", saida);
        Afirmar.Contem("Buzzy.App.Testes.dll (teste)", saida);
        Afirmar.Contem("Código-fonte: 1 arquivo(s) .cs", saida);
        Afirmar.Falso(saida.Contains(": error ", StringComparison.Ordinal), saida);
    }

    // Fase 9, F9-P2 (DEC-040, item 6): um binário que não é do Buzzy nem de teste, na pasta de saída, reprova (antes só
    // aparecia como fora do portão).
    [Teste]
    public void BinarioDeTerceiroNaPastaReprova()
    {
        Produto produto = MontarProdutoLimpo();
        var terceiro = new AssemblySintetico("Terceiro");
        terceiro.ReferenciarMembro("System", "Math", "Round");
        terceiro.Gravar(Path.Combine(produto.Binarios, "Terceiro.dll"));
        (int codigo, string saida, string erros) = Rodar(produto);
        Afirmar.Igual(1, codigo, saida + erros);
        Afirmar.Contem("BZP006", saida + erros);
        Afirmar.Contem("Terceiro.dll", saida + erros);
    }

    [Teste]
    public void ObservadorDeTelaCheiaAprovaComoUsoRestrito()
    {
        // DEC-034, de ponta a ponta: as três funções declaradas no tipo do observador e citadas só no arquivo dele aprovam, e
        // o relatório mostra cada uma como uso restrito.
        Produto produto = MontarProdutoLimpo();
        var buzzy = new AssemblySintetico("Buzzy", "Buzzy.App.Plataforma", "ObservadorDeTelaCheia", "Nativo");
        buzzy.DeclararPInvoke("user32.dll", "SetWindowPos");
        buzzy.DeclararPInvoke("user32.dll", "SetWinEventHook");
        buzzy.DeclararPInvoke("user32.dll", "GetForegroundWindow");
        buzzy.DeclararPInvoke("user32.dll", "GetWindowThreadProcessId");
        buzzy.Gravar(Path.Combine(produto.Binarios, "Buzzy.dll"));
        _pasta.Escrever(@"src\Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs",
            "namespace Buzzy.App.Plataforma;\ninternal sealed class ObservadorDeTelaCheia\n{\n    void F() { SetWinEventHook(); GetForegroundWindow(); GetWindowThreadProcessId(0, 0); }\n}\n");
        (int codigo, string saida, string erros) = Rodar(produto);

        Afirmar.Igual(0, codigo, saida + erros);
        Afirmar.Contem("Resumo: APROVADO - nenhuma violação; 4 importação(ões) permitida(s) no apphost; 3 uso(s) restrito(s).", saida);
        Afirmar.Igual(3, Regex.Matches(saida, @" uso restrito em Buzzy\.App\.Plataforma\.ObservadorDeTelaCheia\+Nativo\.").Count, saida);
        Afirmar.Contem("Buzzy.dll: user32.dll!SetWinEventHook [Ler outros aplicativos] uso restrito em", saida);

        // A mesma chamada em outro arquivo reprova, e o arquivo do observador continua sem acusação.
        string outro = _pasta.Escrever(@"src\Buzzy.App\Plataforma\Outro.cs", "static class O\n{\n    static void F() => GetForegroundWindow();\n}\n");
        (codigo, saida, _) = Rodar(produto);
        Afirmar.Igual(1, codigo, saida);
        string linha = saida.Split('\n').Select(l => l.TrimEnd('\r')).Single(l => LinhaDeErro.IsMatch(l));
        Afirmar.Igual(outro, LinhaDeErro.Match(linha).Groups["origem"].Value);
        Afirmar.Contem("Resumo: REPROVADO - 1 violação(ões) em 1 arquivo(s) (Ler outros aplicativos: 1)", saida);
    }

    [Teste]
    public void LinhasQueNaoSaoViolacaoNaoParecemErroOuAvisoDoMsbuild()
    {
        Produto produto = MontarProdutoLimpo();
        _pasta.Escrever(@"src\Nativo.cs", "static class N\n{\n    static void F() => GetForegroundWindow();\n}\n");
        (int codigo, string saida, _) = Rodar(produto);

        Afirmar.Igual(1, codigo);
        // Aproximação do formato canônico do MSBuild: "error" ou "warning" como palavra, depois
        // de início de linha, dois-pontos ou espaço, seguido de código opcional e dois-pontos.
        var aviso = new Regex(@"(^|[:\s])(error|warning)(\s+[^:\s]+)?\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (string linha in saida.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (LinhaDeErro.IsMatch(linha)) continue;
            Afirmar.Falso(aviso.IsMatch(linha), $"linha parece erro do MSBuild: {linha}");
        }
    }

    [Teste]
    public void ViolacaoNaFonteSaiNoFormatoDoMsbuild()
    {
        Produto produto = MontarProdutoLimpo();
        string nativo = _pasta.Escrever(@"src\Nativo.cs", "static class N\n{\n    static void F() => GetForegroundWindow();\n}\n");
        (int codigo, string saida, _) = Rodar(produto);

        Afirmar.Igual(1, codigo, saida);
        string linha = saida.Split('\n').Select(l => l.TrimEnd('\r')).Single(l => LinhaDeErro.IsMatch(l));
        Match m = LinhaDeErro.Match(linha);
        Afirmar.Igual(nativo, m.Groups["origem"].Value);
        Afirmar.Igual("3", m.Groups["linha"].Value);
        Afirmar.Igual("24", m.Groups["coluna"].Value);
        Afirmar.Igual(Codigos.CodigoFonte, m.Groups["codigo"].Value);
        Afirmar.Contem("[Ler outros aplicativos] GetForegroundWindow", m.Groups["texto"].Value);
        Afirmar.Contem("Resumo: REPROVADO - 1 violação(ões) em 1 arquivo(s) (Ler outros aplicativos: 1)", saida);
    }

    [Teste]
    public void AssemblyDasAmostrasReprova()
    {
        Produto produto = MontarProdutoLimpo();
        // O assembly de testes, com as amostras proibidas, no lugar do Buzzy.dll, e as dependências
        // Buzzy.* dele na mesma pasta (Buzzy.PortaoApis.dll também passa pelo portão).
        File.Copy(typeof(PInvokesProibidos).Assembly.Location, Path.Combine(produto.Binarios, "Buzzy.dll"), overwrite: true);
        string[] dependencias = ["Buzzy.PortaoApis.dll", "Buzzy.Testes.Executor.dll"];
        foreach (string dependencia in dependencias)
            File.Copy(Path.Combine(AppContext.BaseDirectory, dependencia), Path.Combine(produto.Binarios, dependencia));

        (int codigo, string saida, _) = Rodar(produto);

        Afirmar.Igual(1, codigo, saida);
        string buzzyDll = Path.Combine(produto.Binarios, "Buzzy.dll");
        Afirmar.Contem($"{buzzyDll}: error BZP001: [Injetar input] user32.dll!SendInput - P/Invoke em", saida);
        Afirmar.Contem($"{buzzyDll}: error BZP002: [Processos] System.Diagnostics.Process.Start - referência ao membro", saida);
        Afirmar.Contem($"{buzzyDll}: error BZP002: [Rede] System.Net.Http.HttpClient - referência a tipo do namespace proibido System.Net.Http", saida);
        Afirmar.Contem("Buzzy.Testes.Executor.dll (teste)", saida);
        Afirmar.Falso(saida.Contains("Buzzy.PortaoApis.dll: error", StringComparison.Ordinal), "o próprio portão tem violação");
        Afirmar.Contem("Resumo: REPROVADO", saida);
    }

    [Teste]
    public void BuzzyCoreTambemPassaPeloPortao()
    {
        Produto produto = MontarProdutoLimpo();
        var core = new AssemblySintetico("Buzzy.Core");
        core.ReferenciarMembro("System.Diagnostics", "Process", "Start");
        core.Gravar(Path.Combine(produto.Binarios, "Buzzy.Core.dll"));

        (int codigo, string saida, _) = Rodar(produto);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem($"{Path.Combine(produto.Binarios, "Buzzy.Core.dll")}: error BZP002: [Processos] System.Diagnostics.Process.Start", saida);
    }

    [Teste]
    public void BuzzyExeGerenciadoNaoRecebeAsPermissoesDoApphost()
    {
        Produto produto = MontarProdutoLimpo();
        var exe = new AssemblySintetico("Buzzy");
        exe.DeclararPInvoke("kernel32.dll", "LoadLibraryExW");
        exe.Gravar(Path.Combine(produto.Binarios, "Buzzy.exe"));

        (int codigo, string saida, _) = Rodar(produto);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem($"{Path.Combine(produto.Binarios, "Buzzy.exe")}: error BZP001: [Código dinâmico] kernel32.dll!LoadLibraryExW", saida);
        Afirmar.Falso(saida.Contains("permitida no apphost", StringComparison.Ordinal), saida);
    }

    [Teste]
    public void ManifestoComElevacaoReprova()
    {
        Produto produto = MontarProdutoLimpo();
        File.WriteAllText(produto.Manifesto, TestesDoVerificadorDeManifesto.Manifesto(
            nivel: """<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />"""));

        (int codigo, string saida, _) = Rodar(produto);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem($"{produto.Manifesto}(7,", saida);
        Afirmar.Contem("error BZP005: [Manifesto] requestedExecutionLevel level", saida);
    }

    [Teste]
    public void SemManifestoAvisaQueNaoVerificou()
    {
        Produto produto = MontarProdutoLimpo();
        (int codigo, string saida, _) = Rodar("--binarios", produto.Binarios, "--fonte", produto.Fonte);
        Afirmar.Igual(0, codigo, saida);
        Afirmar.Contem("Manifesto: não verificado (--manifesto não informado).", saida);
    }

    [Teste]
    public void VariasFontesENomeDeAplicativo()
    {
        string binarios = _pasta.Subpasta("saida");
        new AssemblySintetico("Outro").Gravar(Path.Combine(binarios, "Outro.dll"));
        File.Copy(Repositorio.ApphostReal(), Path.Combine(binarios, "Outro.exe"));
        string fonte1 = Path.GetDirectoryName(_pasta.Escrever(@"f1\A.cs", "class A { }"))!;
        string fonte2 = Path.GetDirectoryName(_pasta.Escrever(@"f2\B.cs", "class B { void F() => keybd_event(0, 0, 0, 0); }"))!;

        (int codigo, string saida, _) = Rodar("--binarios", binarios, "--fonte", fonte1, "--fonte", fonte2, "--aplicativo", "Outro");
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Outro.exe", saida);
        Afirmar.Contem("error BZP004: [Injetar input] keybd_event", saida);
        Afirmar.Contem("Código-fonte: 2 arquivo(s) .cs", saida);
    }

    [Teste]
    public void FonteRepetidaOuAninhadaNaoDuplicaViolacao()
    {
        Produto produto = MontarProdutoLimpo();
        string sub = Path.GetDirectoryName(_pasta.Escrever(@"src\Plataforma\Nativo.cs", "class N { void F() => WindowFromPoint(0); }"))!;

        (int codigo, string saida, _) = Rodar("--binarios", produto.Binarios, "--fonte", produto.Fonte, "--fonte", sub, "--fonte", produto.Fonte);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Igual(1, Regex.Matches(saida, "error BZP004: ").Count, saida);
        Afirmar.Contem("Código-fonte: 2 arquivo(s) .cs", saida);
    }

    [Teste]
    public void ErrosDeUsoSaemComCodigoDois()
    {
        Produto produto = MontarProdutoLimpo();
        string vazia = _pasta.Subpasta("vazia");
        string semBuzzy = _pasta.Subpasta("semBuzzy");
        string semExe = _pasta.Subpasta("semExe");
        File.Copy(Path.Combine(produto.Binarios, "Buzzy.dll"), Path.Combine(semExe, "Buzzy.dll"));
        File.Copy(Path.Combine(produto.Binarios, "Buzzy.Core.dll"), Path.Combine(semExe, "Buzzy.Core.dll"));
        string fonteSemCs = Path.GetDirectoryName(_pasta.Escrever(@"semCs\obj\X.cs", "class X { }"))!;
        fonteSemCs = Path.GetDirectoryName(fonteSemCs)!;

        (string[] Argumentos, string Mensagem)[] casos =
        [
            ([], "Falta --binarios"),
            (["--binarios", produto.Binarios], "Falta pelo menos um --fonte"),
            (["--binarios"], "--binarios precisa de um valor"),
            (["--binarios", produto.Binarios, "--fonte", produto.Fonte, "--extra"], "Argumento não reconhecido: --extra"),
            (["--binarios", produto.Binarios, "--binarios", produto.Binarios, "--fonte", produto.Fonte], "mais de uma vez"),
            (["--binarios", Path.Combine(vazia, "nao-existe"), "--fonte", produto.Fonte], "Pasta de binários não encontrada"),
            (["--binarios", semBuzzy, "--fonte", produto.Fonte], "Buzzy.dll não encontrado"),
            (["--binarios", semExe, "--fonte", produto.Fonte], "Buzzy.exe não encontrado"),
            (["--binarios", produto.Binarios, "--fonte", Path.Combine(vazia, "nao-existe")], "Pasta de código-fonte não encontrada"),
            (["--binarios", produto.Binarios, "--fonte", fonteSemCs], "Nenhum arquivo .cs"),
            (["--binarios", produto.Binarios, "--fonte", produto.Fonte, "--manifesto", Path.Combine(vazia, "app.manifest")], "Manifesto não encontrado"),
            (["--binarios", produto.Binarios + "\" --fonte x", "--fonte", produto.Fonte], "Caminho com aspas"),
            (["--binarios", produto.Binarios, "--fonte", produto.Fonte, "--aplicativo", @"..\Buzzy"], "nome simples"),
        ];

        foreach ((string[] argumentos, string mensagem) in casos)
        {
            (int codigo, string saida, string erros) = Rodar(argumentos);
            string descricao = string.Join(" ", argumentos);
            Afirmar.Igual(2, codigo, descricao);
            Afirmar.Igual("", saida, descricao);
            Afirmar.Contem("Buzzy.PortaoApis: error BZP000: ", erros);
            Afirmar.Contem(mensagem, erros);
            Afirmar.Contem("Uso: Buzzy.PortaoApis --binarios", erros);
        }
    }

    [Teste]
    public void ErrosDeLeituraSaemComCodigoDois()
    {
        Produto produto = MontarProdutoLimpo();
        string manifestoBom = File.ReadAllText(produto.Manifesto);
        File.WriteAllText(produto.Manifesto, "<assembly><trustInfo></assembly>");
        (int codigo, string saida, string erros) = Rodar(produto);
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Igual("", saida);
        Afirmar.Contem("error BZP000: não foi possível ler", erros);

        File.WriteAllText(produto.Manifesto, manifestoBom);
        File.WriteAllBytes(Path.Combine(produto.Binarios, "Buzzy.dll"), [0x4D, 0x5A, 0x00, 0x01, 0x02]);
        (codigo, saida, erros) = Rodar(produto);
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Igual("", saida);
        Afirmar.Contem("error BZP000: não foi possível ler", erros);
    }

    [Teste]
    public void DependenciaDoProdutoAusenteEhErroDeUso()
    {
        Produto produto = MontarProdutoLimpo();
        var buzzy = new AssemblySintetico("Buzzy");
        buzzy.ReferenciarAssembly("Buzzy.Plataforma");
        buzzy.Gravar(Path.Combine(produto.Binarios, "Buzzy.dll"));

        (int codigo, _, string erros) = Rodar(produto);
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("Buzzy.dll referencia Buzzy.Plataforma", erros);
    }

    [Teste]
    public void AjudaSaiComZero()
    {
        (int codigo, string saida, string erros) = Rodar("--ajuda");
        Afirmar.Igual(0, codigo);
        Afirmar.Contem("Uso: Buzzy.PortaoApis --binarios <pasta> --fonte <pasta>", saida);
        Afirmar.Igual("", erros);
    }
}
