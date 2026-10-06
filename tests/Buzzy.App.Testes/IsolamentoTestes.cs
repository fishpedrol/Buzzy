using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.App.Testes.Integracao;
using Buzzy.Testes;
using Microsoft.Win32.SafeHandles;
using ProgramaDoBuzzy = Buzzy.App.Programa;

namespace Buzzy.App.Testes;

/// <summary>
/// Isolamento dos testes (Fase 5): todo Buzzy aberto por um teste ou por uma ferramenta usa um perfil de teste
/// (<c>--perfil-de-teste</c>), nunca as configurações reais do usuário. Tudo sem abrir o Buzzy: a leitura das
/// opções (também escritas de outro jeito), os lançadores (os argumentos que montam e os únicos lugares que abrem o
/// Buzzy), a limpeza da pasta do perfil (numa pasta temporária), a regra do aplicativo que escolhe a pasta das
/// configurações e o instante das linhas do log, usado para medir durações.
/// </summary>
internal sealed class IsolamentoTestes : IDisposable
{
    private readonly string _raiz = Directory.CreateTempSubdirectory("buzzy-isolamento-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"         limpeza: a pasta temporária {_raiz} ficou para o sistema apagar ({e.GetType().Name})");
        }
    }

    // ------------------------------------------------------------------ linha de comando do Buzzy

    [Teste]
    public void LerOpcoes_SemPerfil_ComoAntes()
    {
        Afirmar.Igual(OpcoesDaAplicacao.Padrao, ProgramaDoBuzzy.LerOpcoes([]));
        Afirmar.Igual(new OpcoesDaAplicacao(true, 42UL), ProgramaDoBuzzy.LerOpcoes(["--diagnostico", "--pausado", "--semente", "42"]));
        OpcoesDaAplicacao opcoes = ProgramaDoBuzzy.LerOpcoes(["--pausado"]);
        Afirmar.Nulo(opcoes.PerfilDeTeste);
        Afirmar.Falso(opcoes.PersistenciaDesligada);
        Afirmar.Falso(opcoes.SemTelaCheia, "o observador de tela cheia liga por padrão (DEC-034)");
        Afirmar.Verdadeiro(ProgramaDoBuzzy.LerOpcoes(["--diagnostico", "--sem-tela-cheia", "--pausado"]).SemTelaCheia, "--sem-tela-cheia");
        Afirmar.Falso(ProgramaDoBuzzy.LerOpcoes(["--Sem-Tela-Cheia"]).SemTelaCheia, "só a grafia exata");
    }

    [Teste]
    public void LerOpcoes_PerfilValido_IsolaSemDesligar()
    {
        // "perfil-de-teste" também é um nome válido: sem traço ou barra na frente, não é outra grafia da opção.
        foreach (string nome in new[] { "integracao", "verificacao", "desempenho", "a-1", "perfil-de-teste", new string('z', 32) })
        {
            OpcoesDaAplicacao opcoes = ProgramaDoBuzzy.LerOpcoes(["--diagnostico", "--perfil-de-teste", nome, "--pausado", "--semente", "7"]);
            Afirmar.Igual(nome, opcoes.PerfilDeTeste);
            Afirmar.Falso(opcoes.PersistenciaDesligada, nome);
            Afirmar.Verdadeiro(opcoes.MovimentoPausado, nome);
            Afirmar.Igual<ulong?>(7, opcoes.Semente, nome);
        }
    }

    [Teste]
    public void LerOpcoes_PerfilInvalidoOuSemNome_DesligaAPersistencia()
    {
        // Falha fechada: sem nome ou com um nome que sairia da pasta de testes, nada de pasta real.
        string[][] casos =
        [
            ["--perfil-de-teste"],
            ["--perfil-de-teste", ""],
            ["--perfil-de-teste", ".."],
            ["--perfil-de-teste", @"..\..\Buzzy"],
            ["--perfil-de-teste", "../x"],
            ["--perfil-de-teste", @"C:\Users"],
            ["--perfil-de-teste", "Integracao"],
            ["--perfil-de-teste", "con"],
            ["--perfil-de-teste", "a b"],
            ["--perfil-de-teste", new string('z', 33)],
            ["--perfil-de-teste", "--pausado"],
        ];
        foreach (string[] argumentos in casos)
        {
            string rotulo = string.Join(" ", argumentos.Select(a => $"[{a}]"));
            OpcoesDaAplicacao opcoes = ProgramaDoBuzzy.LerOpcoes(argumentos);
            Afirmar.Nulo(opcoes.PerfilDeTeste, rotulo);
            Afirmar.Verdadeiro(opcoes.PersistenciaDesligada, rotulo);
        }

        // O resto da linha de comando continua valendo.
        OpcoesDaAplicacao comOutras = ProgramaDoBuzzy.LerOpcoes(["--perfil-de-teste", "--pausado", "--semente", "7"]);
        Afirmar.Verdadeiro(comOutras.PersistenciaDesligada);
        Afirmar.Verdadeiro(comOutras.MovimentoPausado);
        Afirmar.Igual<ulong?>(7, comOutras.Semente);
    }

    [Teste]
    public void LerOpcoes_OutraGrafiaDaOpcao_DesligaAPersistencia()
    {
        // Quem escreveu a opção de outro jeito quis isolar o Buzzy: em vez de ignorá-la e cair na pasta real do
        // usuário, a persistência fica desligada. Também quando a grafia exata vem junto, com um nome válido.
        string[][] casos =
        [
            ["--perfil-de-teste=integracao"],
            ["--perfil-de-teste:integracao"],
            ["--Perfil-De-Teste", "integracao"],
            ["--PERFIL-DE-TESTE", "integracao"],
            ["/perfil-de-teste", "integracao"],
            ["-perfil-de-teste", "integracao"],
            ["---perfil-de-teste", "integracao"],
            ["--perfil_de_teste", "integracao"],
            ["--perfildeteste", "integracao"],
            ["—perfil-de-teste", "integracao"],
            ["--diagnostico", "--perfil-de-teste", "integracao", "--perfil-de-teste=outro"],
            ["/Perfil-de-teste=integracao", "--pausado"],
        ];
        foreach (string[] argumentos in casos)
        {
            string rotulo = string.Join(" ", argumentos.Select(a => $"[{a}]"));
            OpcoesDaAplicacao opcoes = ProgramaDoBuzzy.LerOpcoes(argumentos);
            Afirmar.Nulo(opcoes.PerfilDeTeste, rotulo);
            Afirmar.Verdadeiro(opcoes.PersistenciaDesligada, rotulo);
        }
        Afirmar.Verdadeiro(ProgramaDoBuzzy.LerOpcoes(["/perfil-de-teste", "x", "--pausado"]).MovimentoPausado, "o resto continua valendo");

        // Não são a opção: a grafia exata, um nome sem traço nem barra na frente e outras opções.
        foreach (string argumento in new[] { "--perfil-de-teste", "perfil-de-teste", "integracao", "--pausado", "--semente", "--perfil", "--perfil-de-energia", "-", "/", "" })
            Afirmar.Falso(ProgramaDoBuzzy.ParecidoComAOpcaoDoPerfil(argumento), $"[{argumento}]");
    }

    // ------------------------------------------------------------------ lançadores

    [Teste]
    public void BuzzyEmTeste_AbreComOPerfilDeIntegracao()
    {
        // Sem pedir a tela cheia, vai --sem-tela-cheia (DEC-034): o lugar inicial não depende do que estiver em tela cheia.
        List<string> argumentos = [.. BuzzyEmTeste.DescreverProcesso().ArgumentList];
        Afirmar.Sequencia(["--diagnostico", "--perfil-de-teste", PerfilDeTeste.Integracao, "--sem-tela-cheia", "--pausado"], argumentos);

        argumentos = [.. BuzzyEmTeste.DescreverProcesso(pausado: false, semente: 5, perfil: "persistencia").ArgumentList];
        Afirmar.Sequencia(["--diagnostico", "--perfil-de-teste", "persistencia", "--sem-tela-cheia", "--semente", "5"], argumentos);

        argumentos = [.. BuzzyEmTeste.DescreverProcesso(telaCheia: true).ArgumentList];
        Afirmar.Sequencia(["--diagnostico", "--perfil-de-teste", PerfilDeTeste.Integracao, "--pausado"], argumentos, "com a tela cheia, como no uso real");

        // A pasta que o teste limpa é a mesma que o Buzzy usa com esse perfil.
        string buzzy = Afirmar.NaoNulo(PastaDeDados.DoBuzzy());
        Afirmar.Igual(PastaDeDados.DoPerfilDeTeste(PerfilDeTeste.Integracao), PerfilDeTeste.Pasta(PerfilDeTeste.Integracao, buzzy));
    }

    // DEC-038, item 12: em tests/ e tools/, só a foto do registro real (RegistroReal.cs e a função da medição) toca o
    // registro, e só para ler; o portão e as amostras dele citam as funções como dados, sem chamá-las. Nenhum teste nem
    // ferramenta grava no registro real do usuário.
    [Teste]
    public void Registro_SoAFotoLe_ENinguemGrava()
    {
        string[] fontes = [.. new[] { "tests", "tools" }.SelectMany(p => Directory.GetFiles(Path.Combine(Caminhos.Raiz, p), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".ps1", StringComparison.Ordinal))
                && !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]|[\\/]Buzzy\.PortaoApis(\.Testes)?[\\/]")
                && Path.GetFileName(f) is not ("IsolamentoTestes.cs" or "InicioComOWindowsTestes.cs" or "CercasDoPortaoTestes.cs" or "verificar-gravacoes.ps1" or "VerificarGravacoesTestes.cs"))];
        // InicioComOWindowsTestes só lê a fonte do adaptador (a contenção na fonte), sem chamar nenhum método dele nem o Windows.
        string contencao = File.ReadAllText(Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes", "InicioComOWindowsTestes.cs"));
        Afirmar.Falso(Regex.IsMatch(contencao, @"DllImport|LibraryImport|Microsoft\.Win32|DaExecucao\([^()]*\)\.(?!Modo\b)"), "a contenção do início só lê a fonte (do real, só o modo)");
        // CercasDoPortaoTestes (DEC-040, item 6) também só lê a fonte: cita o registro como texto, nos padrões.
        string cercas = File.ReadAllText(Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes", "CercasDoPortaoTestes.cs"));
        Afirmar.Falso(Regex.IsMatch(cercas, @"\[(DllImport|LibraryImport)\(|using Microsoft\.Win32|RegistryKey\."), "as cercas do portão só leem a fonte");
        // tools\verificar-gravacoes.ps1 (DEC-040, item 10) cita o registro como texto, nos caminhos do Process Monitor: só lê um CSV.
        string analisador = File.ReadAllText(Path.Combine(Caminhos.Raiz, "tools", "verificar-gravacoes.ps1"));
        Afirmar.Falso(Regex.IsMatch(analisador, @"Microsoft\.Win32|RegistryKey|\breg(\.exe)?\s+(add|delete|import|query)|-ItemProperty|HKCU:|HKLM:|Start-Process|Invoke-WebRequest", RegexOptions.IgnoreCase), "o analisador só lê o CSV");
        Afirmar.Contem("Import-Csv", analisador);
        // VerificarGravacoesTestes cita o registro só nas linhas de CSV que monta, e roda só o analisador sobre elas.
        string testesDoAnalisador = File.ReadAllText(Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes", "VerificarGravacoesTestes.cs"));
        Afirmar.Falso(Regex.IsMatch(testesDoAnalisador, @"\[(DllImport|LibraryImport)\(|using Microsoft\.Win32|RegistryKey\.|Registry\.(CurrentUser|LocalMachine)"), "os testes do analisador não tocam o registro");
        Afirmar.Verdadeiro(fontes.Length > 50, $"achou as fontes ({fontes.Length})");
        var registro = new Regex(@"advapi32|Microsoft\.Win32\.Registry|RegistryKey|Registry\.CurrentUser|HKCU:|HKEY_|\breg(\.exe)?\s+(add|delete|import)|-ItemProperty", RegexOptions.IgnoreCase);
        Afirmar.Sequencia(["RegistroReal.cs", "empacotar.ps1", "medir-desempenho.ps1"],
            fontes.Where(f => registro.IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).Order(StringComparer.Ordinal), "quem cita o registro");
        var escrita = new Regex(@"RegSetValue|RegSetKeyValue|RegDelete|RegCreateKey|SHSetValue|SHRegSet|CreateSubKey|DeleteSubKey|DeleteValue|\.SetValue\(|KEY_SET_VALUE|KEY_WRITE|KEY_ALL_ACCESS|writable\s*:\s*true|OpenSubKey\([^)]*,\s*true", RegexOptions.IgnoreCase);
        foreach (string f in fontes.Where(f => registro.IsMatch(File.ReadAllText(f))))
        {
            string codigo = Regex.Replace(File.ReadAllText(f), @"//.*|^\s*#.*$|///.*", "", RegexOptions.Multiline);
            Afirmar.Falso(escrita.IsMatch(codigo), $"{Path.GetFileName(f)} só lê o registro");
        }
        Afirmar.Contem("KEY_QUERY_VALUE", File.ReadAllText(Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes", "Integracao", "RegistroReal.cs")));
        string foto = RegistroReal.Foto();
        Afirmar.Verdadeiro(foto.StartsWith("registro: Run", StringComparison.Ordinal) && foto.Contains("StartupApproved\\Run", StringComparison.Ordinal), foto);
        Afirmar.Contem(foto, ArquivosReais.Foto(), "a foto dos arquivos reais leva a do registro");
    }

    // DEC-038, item 11: em src/, só a PastaDeDados pede uma pasta do sistema, e só a pasta local do usuário; nenhuma outra
    // pasta especial (a Inicializar inclusive) nem o menu Iniciar aparecem no produto.
    [Teste]
    public void PastasDoSistema_SoAPastaLocal_NaPastaDeDados()
    {
        string[] fontes = [.. Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src"), "*.cs", SearchOption.AllDirectories).Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"))];
        var pasta = new Regex(@"GetFolderPath|SHGetKnownFolderPath|SHGetFolderPath|SpecialFolder\.|Start Menu|Menu Iniciar|Programs\\{1,2}Startup|""Startup""", RegexOptions.IgnoreCase);
        Afirmar.Sequencia(["PastaDeDados.cs"], fontes.Where(f => pasta.IsMatch(File.ReadAllText(f))).Select(Path.GetFileName), "quem pede uma pasta do sistema");
        string[] especiais = [.. Regex.Matches(File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Plataforma", "PastaDeDados.cs")), @"SpecialFolder\.(\w+)").Select(m => m.Groups[1].Value).Distinct()];
        Afirmar.Sequencia(["LocalApplicationData"], especiais, "só a pasta local");
    }

    [Teste]
    public void Verificacao_AbreOBuzzySoComOPerfilDeTeste()
    {
        string pasta = Path.Combine(Caminhos.Raiz, "tests", "Buzzy.Verificacao");
        string fontes = string.Join("\n", FontesCs(pasta).Select(File.ReadAllText));

        // Todo processo que a verificação inicia é Process.Start(psi), e todo psi é o do receptor (o próprio
        // executável) ou o do descritor com o perfil; nenhum ProcessStartInfo montado de outro jeito.
        Afirmar.Sequencia(["psi"], Regex.Matches(fontes, @"Process\.Start\(\s*([^)]*?)\s*\)").Select(m => m.Groups[1].Value).Distinct(), "o que Process.Start recebe");
        string[] origens = [.. Regex.Matches(fontes, @"\bpsi\s*=\s*(PerfilDaVerificacao\.Descrever\(\w+|new ProcessStartInfo\(\w+\))").Select(m => m.Groups[1].Value)];
        Afirmar.Igual(Regex.Matches(fontes, @"\bpsi\s*=").Count, origens.Length, "todo psi vem de um dos dois descritores");
        Afirmar.Sequencia(["PerfilDaVerificacao.Descrever(_exeBuzzy", "new ProcessStartInfo(_exeProprio)", "new ProcessStartInfo(exeBuzzy)"],
            origens.Distinct().Order(StringComparer.Ordinal), "de onde vem cada psi");
        Afirmar.Igual(0, Regex.Matches(fontes, @"new\s+ProcessStartInfo\s*(\(\s*\)|\{)|\.StartInfo\b|UseShellExecute\s*=\s*true").Count, "ProcessStartInfo sem o executável, StartInfo ou shell");
        string[] alvos = [.. Regex.Matches(fontes, @"new ProcessStartInfo\((\w+)\)").Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(["_exeProprio", "exeBuzzy"], alvos, "processos que a verificação descreve");
        Afirmar.Verdadeiro(Regex.Matches(fontes, @"PerfilDaVerificacao\.Descrever\(_exeBuzzy").Count >= 3, "abrir, segunda instância e abrir com a autonomia");

        string apoio = File.ReadAllText(Path.Combine(pasta, "Apoio.cs"));
        Afirmar.Contem("psi.ArgumentList.Add(\"--perfil-de-teste\");", apoio);
        Afirmar.Contem("internal const string Nome = \"verificacao\";", apoio);
        Afirmar.Verdadeiro(PastaDeDados.NomeDePerfilValido("verificacao"), "o nome do perfil da verificação é válido");

        // A pasta do perfil e a limpeza dela não são uma cópia: são as do Buzzy e as destes testes, compiladas junto.
        string projeto = File.ReadAllText(Path.Combine(pasta, "Buzzy.Verificacao.csproj"));
        Afirmar.Contem(@"<Compile Include=""..\..\src\Buzzy.App\Plataforma\PastaDeDados.cs""", projeto);
        Afirmar.Contem(@"<Compile Include=""..\Buzzy.App.Testes\Integracao\PerfilDeTeste.cs""", projeto);
        Afirmar.Contem("PerfilDeTeste.Limpar(Nome);", apoio);
        Afirmar.Igual(0, Regex.Matches(fontes, @"Directory\.Delete\(|""testes""").Count, "a verificação não monta o caminho nem apaga pastas por conta própria");
    }

    [Teste]
    public void TestesDoAplicativo_AbremOBuzzySoPeloBuzzyEmTeste()
    {
        // Nos testes do aplicativo, o Buzzy.exe só é iniciado por BuzzyEmTeste, sempre com um perfil de teste
        // (BuzzyEmTeste_AbreComOPerfilDeIntegracao); o outro processo que eles iniciam é o próprio executável de
        // testes, como gravador (ArquivoDeConfiguracoesTestes.MatarDuranteGravacoes_NuncaDeixaIlegivel). A validação do manifesto de
        // clipes (Fase 6, passo F6-P3) roda o dotnet com a DLL da ferramenta do build, nunca o Buzzy.exe.
        string pasta = Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes");
        Dictionary<string, string> fontes = FontesCs(pasta)
            .Where(f => Path.GetFileName(f) != "IsolamentoTestes.cs")
            .ToDictionary(f => Path.GetRelativePath(pasta, f), File.ReadAllText, StringComparer.Ordinal);
        string[] Onde(string padrao) => [.. fontes.Where(f => Regex.IsMatch(f.Value, padrao)).Select(f => f.Key).Order(StringComparer.Ordinal)];

        Afirmar.Sequencia(["ArquivoDeConfiguracoesTestes.cs", @"Integracao\BuzzyEmTeste.cs", "ValidacaoDeClipesNoBuildTestes.cs", "VerificarGravacoesTestes.cs"], Onde(@"Process\.Start\("), "quem inicia processos");
        // VerificarGravacoesTestes inicia só o PowerShell com o analisador do CSV (DEC-040, item 10), nunca o Buzzy.
        string analisa = fontes["VerificarGravacoesTestes.cs"];
        Afirmar.Verdadeiro(Regex.Matches(analisa, @"new ProcessStartInfo\(""([^""]+)""\)").All(m => m.Groups[1].Value == "powershell.exe") && analisa.Contains("verificar-gravacoes.ps1", StringComparison.Ordinal), "os testes do analisador só rodam o analisador");
        Afirmar.Sequencia(["Caminhos.cs", "CaminhosTestes.cs", @"Integracao\BuzzyEmTeste.cs"], Onde(@"\bExeDoBuzzy\("), "quem usa o caminho do Buzzy.exe");
        Afirmar.Sequencia([], Onde(@"new\s+ProcessStartInfo\s*(\(\s*\)|\{)|\.StartInfo\b|UseShellExecute\s*=\s*true"), "ProcessStartInfo sem o executável, StartInfo ou shell");

        string buzzyEmTeste = fontes[@"Integracao\BuzzyEmTeste.cs"];
        Afirmar.Igual(1, Regex.Matches(buzzyEmTeste, @"Process\.Start\(").Count, "BuzzyEmTeste inicia o Buzzy num lugar só");
        Afirmar.Contem("Process.Start(DescreverProcesso(pausado, semente, perfil, telaCheia))", buzzyEmTeste);
        Afirmar.Igual(1, Regex.Matches(buzzyEmTeste, @"new ProcessStartInfo\(").Count, "e o descreve num lugar só");
        Afirmar.Contem("new ProcessStartInfo(Caminhos.ExeDoBuzzy())", buzzyEmTeste);

        string arquivo = fontes["ArquivoDeConfiguracoesTestes.cs"];
        Afirmar.Igual(1, Regex.Matches(arquivo, @"Process\.Start\(").Count, "um processo filho só");
        Afirmar.Contem("new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, \"Buzzy.App.Testes.exe\"))", arquivo);
    }

    [Teste]
    public void TestesDoAplicativo_SoMexemNasJanelasDoBuzzyComOPidConferido()
    {
        // SECURITY.md 3.2 (ferramentas de teste): os testes só postam mensagens às janelas do Buzzy que abriram, com o PID
        // conferido antes de cada uma (revisão de segurança do bloco P6-P9, achado 3): se o Buzzy cair no meio do teste e o
        // HWND for reaproveitado, nada vai para a janela de outro programa. Só as portas de BuzzyEmTeste chamam o
        // PostMessage, o SendMessageTimeout, o SetWindowPos e o ShowWindow de NativoTeste, e cada uma confere o PID logo
        // antes. A outra exceção é o envio síncrono à janela de item que o próprio processo de testes cria e fecha
        // (JanelaDoItemTestes): ela não é do Buzzy aberto.
        string pasta = Path.Combine(Caminhos.Raiz, "tests", "Buzzy.App.Testes");
        var chamada = new Regex(@"NativoTeste\.(PostMessage|SendMessageTimeout|SetWindowPos|ShowWindow)\(");
        Dictionary<string, string> fontes = FontesCs(pasta)
            .Where(f => Path.GetFileName(f) != "IsolamentoTestes.cs")
            .ToDictionary(f => Path.GetRelativePath(pasta, f), File.ReadAllText, StringComparer.Ordinal);
        Afirmar.Sequencia([@"Integracao\BuzzyEmTeste.cs", "JanelaDoItemTestes.cs"],
            fontes.Where(f => chamada.IsMatch(f.Value)).Select(f => f.Key).Order(StringComparer.Ordinal), "quem age sobre janelas");
        Afirmar.Sequencia(["SendMessageTimeout"], chamada.Matches(fontes["JanelaDoItemTestes.cs"]).Select(m => m.Groups[1].Value).Distinct(),
            "na janela de item do próprio processo de testes, só o envio síncrono");

        // Em BuzzyEmTeste, cada chamada fica num método que confere o PID antes dela.
        string buzzy = fontes[@"Integracao\BuzzyEmTeste.cs"];
        MatchCollection achadas = chamada.Matches(buzzy);
        Afirmar.Verdadeiro(achadas.Count >= 6, $"as portas: {achadas.Count} chamadas");
        foreach (Match m in achadas)
        {
            int metodo = new[] { "\n    internal ", "\n    public ", "\n    private " }.Max(d => buzzy.LastIndexOf(d, m.Index, StringComparison.Ordinal));
            string antes = buzzy[metodo..m.Index];
            Afirmar.Contem("ExigirDesteProcesso(", antes, $"a chamada {m.Value} da linha {buzzy[..m.Index].Count(c => c == '\n') + 1} confere o PID antes");
        }
        Afirmar.Verdadeiro(Regex.IsMatch(buzzy, @"NativoTeste\.PidDe\(Janela\) == \(uint\)Processo\.Id && Postar\(Janela, NativoTeste\.WM_CLOSE"),
            "o Dispose confere o PID antes do WM_CLOSE, sem lançar");
    }

    [Teste]
    public void ArquivosReais_FotoSoPorFora_EOsNomesSaoOsDoArquivoDeConfiguracoes()
    {
        // A foto dos arquivos reais (revisão de segurança do bloco P6-P9, achado 8) vê só metadados: existência, tamanho e
        // datas, nunca o conteúdo. Os nomes são os do ArquivoDeConfiguracoes, que a verificação de tela não compila.
        Afirmar.Sequencia(ArquivoDeConfiguracoes.Nomes, ArquivosReais.Nomes, "os mesmos quatro nomes");
        string pasta = Path.Combine(_raiz, "fotos");
        Directory.CreateDirectory(pasta);
        string vazia = ArquivosReais.Foto(pasta);
        Afirmar.Igual(string.Join("; ", ArquivoDeConfiguracoes.Nomes.Select(n => $"{n}: ausente")), vazia, "sem arquivos");

        string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
        File.WriteAllText(principal, "{\"segredo\":\"nao-pode-aparecer\"}");
        string com = ArquivosReais.Foto(pasta);
        Afirmar.Contem("settings.json: 31 bytes, criado ", com);
        Afirmar.Falso(com.Contains("segredo", StringComparison.Ordinal) || com.Contains("nao-pode-aparecer", StringComparison.Ordinal), "o conteúdo nunca aparece");
        File.SetLastWriteTimeUtc(principal, File.GetLastWriteTimeUtc(principal).AddSeconds(5));
        Afirmar.Diferente(com, ArquivosReais.Foto(pasta), "uma escrita muda a foto");
        File.Delete(principal);
        Afirmar.Igual(vazia, ArquivosReais.Foto(pasta), "apagado, volta a ausente");
    }

    [Teste]
    public void MedirDesempenho_AbreOBuzzyComOPerfilDeTesteLimpo()
    {
        string[] linhas = File.ReadAllLines(Path.Combine(Caminhos.Raiz, "tools", "medir-desempenho.ps1"));
        int[] Linhas(string trecho) => [.. linhas.Select((l, i) => (l, i)).Where(x => x.l.Contains(trecho, StringComparison.Ordinal)).Select(x => x.i)];
        string[] Atribuicoes(string variavel) => [.. linhas.Select(l => l.Trim()).Where(l => l.StartsWith(variavel + " = ", StringComparison.Ordinal))];

        Afirmar.Sequencia(["$perfilDeTeste = 'desempenho'"], Atribuicoes("$perfilDeTeste"), "o perfil, numa atribuição só");
        Afirmar.Verdadeiro(PastaDeDados.NomeDePerfilValido("desempenho"), "nome de perfil válido");
        Afirmar.Sequencia(["$argumentosDoBuzzy = '--diagnostico --perfil-de-teste ' + $perfilDeTeste"], Atribuicoes("$argumentosDoBuzzy"), "uma só atribuição; o resto só acrescenta");
        int[] aberturas = Linhas("[Diagnostics.Process]::Start($infoPartida)");
        Afirmar.Igual(1, aberturas.Length, "o Buzzy é aberto num lugar só");
        int abrir = aberturas[0];
        Afirmar.Verdadeiro(Linhas("$infoPartida.Arguments = $argumentosDoBuzzy").Any(i => i < abrir), "o Buzzy medido recebe esses argumentos");

        // Antes de abrir, a pasta do perfil é apagada, entre duas conferências de que nenhum Buzzy está aberto: a
        // medição parte sempre da posição inicial, sem a posição gravada pela anterior (mesma semente, mesmo começo).
        int[] limpezas = Linhas("LimparPerfilDeTeste $");
        Afirmar.Igual(1, limpezas.Length, "uma limpeza");
        int limpar = limpezas[0];
        int[] conferencias = Linhas("@(ProcessosBuzzyAbertos)");
        Afirmar.Verdadeiro(limpar < abrir && conferencias.Any(i => i < limpar) && conferencias.Any(i => i > limpar && i < abrir),
            "limpa antes de abrir, com nenhum Buzzy aberto antes e depois");

        // A limpeza é a dos testes (PerfilDeTeste.Limpar): só a pasta testes\<perfil> e sem seguir junção nem link.
        int[] definicoes = Linhas("function LimparPerfilDeTeste(");
        Afirmar.Igual(1, definicoes.Length, "a função de limpeza");
        string funcao = string.Join("\n", linhas.Skip(definicoes[0]).TakeWhile(l => l != "}"));
        foreach (string trecho in new[] { "'Buzzy'", "'testes'", "ReparsePoint", "Abortar", "[IO.Directory]::Delete($pasta, $true)" })
            Afirmar.Contem(trecho, funcao, "LimparPerfilDeTeste");
    }

    // Fase 9, F9-P5 e F9-P10 (DEC-040, item 9; DEC-042): o empacotamento abre o Buzzy da pasta temporária só com o perfil de teste "pacote",
    // limpo entre duas conferências de que nenhum Buzzy está aberto, com a foto dos arquivos e do registro reais antes e
    // depois (falha se mudarem), nunca elevado, fecha só a janela do PID que abriu e não publica nada.
    [Teste]
    public void Empacotar_AbreOBuzzySoComOPerfilDeTeste_EConfereOsArquivosReais()
    {
        string[] linhas = File.ReadAllLines(Path.Combine(Caminhos.Raiz, "tools", "empacotar.ps1"));
        int[] Linhas(string trecho) => [.. linhas.Select((l, i) => (l, i)).Where(x => x.l.Contains(trecho, StringComparison.Ordinal)).Select(x => x.i)];
        Afirmar.Sequencia(["$perfilDeTeste = 'pacote'"], linhas.Select(l => l.Trim()).Where(l => l.StartsWith("$perfilDeTeste = ", StringComparison.Ordinal)), "o perfil, numa atribuição só");
        Afirmar.Verdadeiro(PastaDeDados.NomeDePerfilValido("pacote"), "nome de perfil válido");
        int abrir = Linhas("[Diagnostics.Process]::Start($info)").Single();
        Afirmar.Verdadeiro(Linhas("$info.Arguments = '--diagnostico --perfil-de-teste ' + $perfilDeTeste").Any(i => i < abrir), "abre com o perfil de teste");
        int limpar = Linhas("LimparPerfilDeTeste $pastaLocal $perfilDeTeste").Single();
        int[] conferencias = Linhas("@(ProcessosBuzzyAbertos).Count -gt 0");
        Afirmar.Verdadeiro(conferencias.Any(i => i < limpar) && conferencias.Any(i => i > limpar && i < abrir), "limpa entre duas conferências, antes de abrir");
        int antes = Linhas("$fotoAntes = FotoDosArquivosReais").Single(), depois = Linhas("$fotoDepois = FotoDosArquivosReais").Single();
        Afirmar.Verdadeiro(antes < abrir && depois > abrir, "a foto antes e depois");
        Afirmar.Contem("if ($fotoDepois -ne $fotoAntes) {", linhas[depois + 1]);
        Afirmar.Verdadeiro(Linhas("WindowsBuiltInRole]::Administrator").Single() < abrir, "recusa rodar elevado antes de abrir");
        Afirmar.Verdadeiro(Linhas("GetWindowThreadProcessId($candidato").Length == 1 && Linhas("PostMessage($hwnd, 0x0010").Length == 1, "WM_CLOSE só para a janela do PID aberto");
        Afirmar.Falso(linhas.Any(l => Regex.IsMatch(l, @"Invoke-WebRequest|Invoke-RestMethod|Publish-|gh release|git push", RegexOptions.IgnoreCase)), "nada é publicado nem enviado");
        // No .exe único, só apaga a pasta de extração que a própria execução criou, direto em %TEMP%\.net\Buzzy, sem link.
        string fim = string.Join("\n", linhas.SkipWhile(l => !l.Contains("} finally {", StringComparison.Ordinal)));
        Afirmar.Contem("$extracoesAntes -notcontains $_", string.Join("\n", linhas), "a pasta nova é a que não existia antes");
        foreach (string trecho in new[] { "ReparsePoint", "(Split-Path -Parent $extracaoNova) -eq $extracaoDoRuntime", "Remove-Item -LiteralPath $extracaoNova" })
            Afirmar.Contem(trecho, fim, "a limpeza da extração");
    }

    [Teste]
    public void MedirDesempenho_ConfereOsArquivosReaisAntesEDepois_SoPorFora()
    {
        // Revisão de segurança do bloco P6-P9, achado 8: a medição tira a foto dos arquivos reais (só metadados, como
        // ArquivosReais.cs) antes de abrir o Buzzy e no fim, e falha se mudaram.
        string[] linhas = File.ReadAllLines(Path.Combine(Caminhos.Raiz, "tools", "medir-desempenho.ps1"));
        int[] Linhas(string trecho) => [.. linhas.Select((l, i) => (l, i)).Where(x => x.l.Contains(trecho, StringComparison.Ordinal)).Select(x => x.i)];
        int abrir = Linhas("[Diagnostics.Process]::Start($infoPartida)").Single();
        int[] antes = Linhas("$fotoAntes = FotoDosArquivosReais"), depois = Linhas("$fotoDepois = FotoDosArquivosReais");
        Afirmar.Verdadeiro(antes.Length == 1 && antes[0] < abrir, "a foto de antes, antes de abrir o Buzzy");
        Afirmar.Verdadeiro(depois.Length == 1 && depois[0] > abrir, "a foto do fim, depois");
        Afirmar.Contem("if ($fotoDepois -ne $fotoAntes) {", linhas[depois[0] + 1]);
        Afirmar.Contem("exit 1", linhas[depois[0] + 3], "mudou: a medição falha");

        int funcao = Linhas("function FotoDosArquivosReais {").Single();
        string corpo = string.Join("\n", linhas.Skip(funcao).TakeWhile(l => l != "}"));
        string nomes = Regex.Match(corpo, @"@\(('[^']+'(, )?)+\)").Value;
        Afirmar.Igual("@(" + string.Join(", ", ArquivoDeConfiguracoes.Nomes.Select(n => $"'{n}'")) + ")", nomes, "os quatro nomes do ArquivoDeConfiguracoes");
        foreach (string proibido in new[] { "Get-Content", "ReadAll", "OpenRead", "StreamReader" })
            Afirmar.Falso(corpo.Contains(proibido, StringComparison.Ordinal), $"só metadados: {proibido}");
    }

    [Teste]
    public void Aplicativo_SoCriaOArquivoDeConfiguracoesPelaRegraDaPasta()
    {
        // Em src\, o arquivo de configurações só nasce de ArquivoDeConfiguracoes.DaExecucao, com a pasta de
        // PastaDeDados.DasConfiguracoes (a falha fechada, numa regra só, testada em ArquivoDeConfiguracoesTestes); e a
        // pasta real do Buzzy só é pedida por essa regra e pelo log de diagnóstico. Um "DoPerfilDeTeste(p) ?? DoBuzzy()"
        // em outro lugar faria o isolamento dos testes falhar aberto.
        string src = Path.Combine(Caminhos.Raiz, "src");
        foreach (string arquivo in FontesCs(src))
        {
            string nome = Path.GetRelativePath(src, arquivo);
            string texto = File.ReadAllText(arquivo);
            int criacoes = Regex.Matches(texto, @"new\s+ArquivoDeConfiguracoes\s*\(").Count
                + Regex.Matches(texto, @"ArquivoDeConfiguracoes\??\s+\w+\s*=\s*new\s*\(").Count;
            Afirmar.Igual(nome == @"Buzzy.App\Plataforma\ArquivoDeConfiguracoes.cs" ? 1 : 0, criacoes, $"{nome}: onde o arquivo de configurações é criado");
            if (nome is not (@"Buzzy.App\Plataforma\PastaDeDados.cs" or @"Buzzy.App\Plataforma\Diagnostico.cs"))
                Afirmar.Igual(0, Regex.Matches(texto, @"\bDoBuzzy\s*\(").Count, $"{nome}: pede a pasta real do Buzzy");
        }
    }

    // ------------------------------------------------------------------ limpeza da pasta do perfil

    [Teste]
    public void PerfilDeTeste_LimparApagaSoAPastaDoPerfil()
    {
        string buzzy = Path.Combine(_raiz, "Buzzy");
        string perfil = Path.Combine(buzzy, "testes", "integracao");
        string[] vizinhos =
        [
            Path.Combine(buzzy, "settings.json"),
            Path.Combine(buzzy, "diagnostico.log"),
            Path.Combine(buzzy, "testes", "verificacao", "settings.json"),
            Path.Combine(buzzy, "testes", "solto.txt"),
            Path.Combine(_raiz, "integracao", "settings.json"),
        ];
        foreach (string arquivo in vizinhos) Escrever(arquivo, "vizinho");
        Escrever(Path.Combine(perfil, "settings.json"), "do perfil");
        Escrever(Path.Combine(perfil, "sub", "outro.txt"), "do perfil");

        Afirmar.Verdadeiro(PerfilDeTeste.Limpar("integracao", buzzy), "apagou");
        Afirmar.Falso(Directory.Exists(perfil), "a pasta do perfil foi apagada inteira");
        foreach (string arquivo in vizinhos)
            Afirmar.Verdadeiro(File.Exists(arquivo), $"intacto: {Path.GetRelativePath(_raiz, arquivo)}");

        Afirmar.Falso(PerfilDeTeste.Limpar("integracao", buzzy), "sem a pasta, nada a apagar");
        Afirmar.Falso(PerfilDeTeste.Limpar("persistencia", buzzy), "perfil que nunca existiu");
    }

    [Teste]
    public void PerfilDeTeste_LimparRecusaNomeInvalidoSemApagarNada()
    {
        string buzzy = Path.Combine(_raiz, "Buzzy");
        string[] arquivos =
        [
            Path.Combine(buzzy, "settings.json"),
            Path.Combine(buzzy, "testes", "integracao", "settings.json"),
            Path.Combine(_raiz, "fora.txt"),
        ];
        foreach (string arquivo in arquivos) Escrever(arquivo, "x");

        foreach (string nome in new[] { "", ".", "..", @"..\..", "../..", @"integracao\..\..", "a/b", "Integracao", "con", @"C:\" })
            Afirmar.Lanca<ArgumentException>(() => PerfilDeTeste.Limpar(nome, buzzy), $"\"{nome}\"");
        foreach (string arquivo in arquivos)
            Afirmar.Verdadeiro(File.Exists(arquivo), $"intacto: {Path.GetRelativePath(_raiz, arquivo)}");
    }

    [Teste]
    public void PerfilDeTeste_LimparRecusaJuncaoNoCaminhoSemApagarNadaDeFora()
    {
        // Uma junção, que qualquer usuário cria sem privilégio, na pasta do Buzzy, em testes ou na própria pasta do
        // perfil levaria a limpeza para fora: a pasta do perfil, resolvida através dela, é uma pasta comum, e apagá-la
        // apagaria o que está do outro lado. A limpeza recusa sem apagar nada. É a mesma limpeza da verificação de tela.
        foreach (string ondeFicaAJuncao in new[] { "Buzzy", @"Buzzy\testes", @"Buzzy\testes\integracao" })
        {
            string caso = Path.Combine(_raiz, $"juncao-{ondeFicaAJuncao.Count(c => c == '\\')}");
            string buzzy = Path.Combine(caso, "Buzzy");
            string juncao = Path.Combine(caso, ondeFicaAJuncao);
            string fora = Path.Combine(caso, "fora");

            // Do outro lado da junção, o que estaria no lugar dela: a pasta do perfil fica fora da pasta do Buzzy.
            string perfilDoOutroLado = Path.GetFullPath(Path.Combine(fora, Path.GetRelativePath(juncao, Path.Combine(buzzy, "testes", "integracao"))));
            string importante = Path.Combine(perfilDoOutroLado, "importante.txt");
            Escrever(importante, "fora da pasta do Buzzy");
            Directory.CreateDirectory(Path.GetDirectoryName(juncao)!);
            CriarJuncao(juncao, fora);
            try
            {
                Afirmar.Verdadeiro(File.Exists(Path.Combine(buzzy, "testes", "integracao", "importante.txt")), $"{ondeFicaAJuncao}: a pasta do perfil chega ao outro lado");
                InvalidOperationException recusa = Afirmar.Lanca<InvalidOperationException>(() => PerfilDeTeste.Limpar("integracao", buzzy), ondeFicaAJuncao);
                Afirmar.Contem("junção", recusa.Message, ondeFicaAJuncao);
                Afirmar.Verdadeiro(File.Exists(importante), $"{ondeFicaAJuncao}: nada do outro lado foi apagado");
            }
            finally
            {
                // Apagar a junção (sem recursão) remove só ela, nunca o outro lado.
                if (Directory.Exists(juncao)) Directory.Delete(juncao);
            }
            Afirmar.Verdadeiro(File.Exists(importante), $"{ondeFicaAJuncao}: desfeita a junção, o outro lado continua");
        }
    }

    [Teste]
    public void PerfilDeTeste_LimparRecusaJuncaoMesmoSemAPastaDoPerfil()
    {
        // Revisão de segurança do bloco P6-P9, achado 5: a recusa vinha depois de "a pasta do perfil não existe, nada a
        // apagar". Com a pasta do Buzzy ou a dos testes como junção para um lugar sem a pasta do perfil, nada era recusado, e o
        // Buzzy de teste (que grava desde o passo P7) e o próprio teste gravavam do outro lado, fora da pasta do Buzzy.
        foreach (string ondeFicaAJuncao in new[] { "Buzzy", @"Buzzy\testes" })
        {
            string caso = Path.Combine(_raiz, $"sem-perfil-{ondeFicaAJuncao.Count(c => c == '\\')}");
            string buzzy = Path.Combine(caso, "Buzzy");
            string juncao = Path.Combine(caso, ondeFicaAJuncao);
            string fora = Path.Combine(caso, "fora");
            Directory.CreateDirectory(fora);
            Directory.CreateDirectory(Path.GetDirectoryName(juncao)!);
            CriarJuncao(juncao, fora);
            try
            {
                Afirmar.Falso(Directory.Exists(PerfilDeTeste.Pasta("integracao", buzzy)), $"{ondeFicaAJuncao}: premissa, a pasta do perfil não existe");
                InvalidOperationException recusa = Afirmar.Lanca<InvalidOperationException>(() => PerfilDeTeste.Limpar("integracao", buzzy), ondeFicaAJuncao);
                Afirmar.Contem("junção", recusa.Message, ondeFicaAJuncao);
                Afirmar.Sequencia([], Directory.GetFileSystemEntries(fora), $"{ondeFicaAJuncao}: nada criado do outro lado");
            }
            finally
            {
                if (Directory.Exists(juncao)) Directory.Delete(juncao);
            }
        }
    }

    // ------------------------------------------------------------------ log

    [Teste]
    public void EventoDoLog_InstanteLidoDoPrefixo()
    {
        EventoDoLog lido = Afirmar.NaoNulo(EventoDoLog.Ler("[01:02:03.456] BUZZY|CONFIG|lido=principal"));
        Afirmar.Igual(new TimeSpan(0, 1, 2, 3, 456), lido.Instante);
        EventoDoLog gravado = Afirmar.NaoNulo(EventoDoLog.Ler("[01:02:05.006] BUZZY|CONFIG|gravado=sim"));
        Afirmar.Igual(TimeSpan.FromMilliseconds(1550), gravado.Instante - lido.Instante);

        foreach (string sem in new[] { "BUZZY|X|a=1", "[1:2] BUZZY|X|a=1", "[01:02:03] BUZZY|X|a=1", "[] BUZZY|X|a=1", "x[01:02:03.456] BUZZY|X|a=1" })
        {
            EventoDoLog evento = Afirmar.NaoNulo(EventoDoLog.Ler(sem));
            Afirmar.Lanca<FormatException>(() => _ = evento.Instante, sem);
        }
    }

    private static void Escrever(string arquivo, string texto)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        File.WriteAllText(arquivo, texto);
    }

    /// <summary>
    /// Cria <paramref name="juncao"/>, que não pode existir, como junção (ponto de montagem) para a pasta
    /// <paramref name="alvo"/>, pelo FSCTL_SET_REPARSE_POINT, como o <c>mklink /J</c>: sem privilégio e sem o modo de
    /// desenvolvedor, ao contrário do link simbólico. Só neste teste, fora do produto.
    /// </summary>
    private static void CriarJuncao(string juncao, string alvo)
    {
        const uint EtiquetaDePontoDeMontagem = 0xA0000003;   // IO_REPARSE_TAG_MOUNT_POINT
        const uint DefinirPontoDeReanalise = 0x000900A4;     // FSCTL_SET_REPARSE_POINT
        const uint EscritaGenerica = 0x40000000;             // GENERIC_WRITE
        const uint AbrirExistente = 3;                       // OPEN_EXISTING
        const uint SemanticaDeBackupEPontoDeReanalise = 0x02000000 | 0x00200000; // FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT

        string exibido = Path.GetFullPath(alvo);
        string substituto = @"\??\" + exibido;
        byte[] nomes = Encoding.Unicode.GetBytes(substituto + "\0" + exibido + "\0");

        // REPARSE_DATA_BUFFER do ponto de montagem: etiqueta, tamanho dos dados e reservado; depois os deslocamentos e
        // tamanhos (em bytes, sem o nulo) do nome substituto e do exibido, e os dois nomes.
        byte[] buffer = new byte[16 + nomes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), EtiquetaDePontoDeMontagem);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)(8 + nomes.Length)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)(substituto.Length * 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)(substituto.Length * 2 + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)(exibido.Length * 2)));
        nomes.CopyTo(buffer, 16);

        Directory.CreateDirectory(juncao);
        using SafeFileHandle pasta = AbrirParaReanalise(juncao, EscritaGenerica, 0, 0, AbrirExistente, SemanticaDeBackupEPontoDeReanalise, 0);
        if (pasta.IsInvalid)
            throw new IOException($"CreateFileW falhou (erro {Marshal.GetLastPInvokeError()}).");
        if (!ControlarDispositivo(pasta, DefinirPontoDeReanalise, buffer, buffer.Length, 0, 0, out _, 0))
            throw new IOException($"FSCTL_SET_REPARSE_POINT falhou (erro {Marshal.GetLastPInvokeError()}).");
        Afirmar.Verdadeiro((File.GetAttributes(juncao) & FileAttributes.ReparsePoint) != 0, "a junção foi criada");
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle AbrirParaReanalise(string nome, uint acesso, uint compartilhamento, nint seguranca, uint disposicao, uint atributos, nint modelo);

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlarDispositivo(SafeFileHandle dispositivo, uint codigo, byte[] entrada, int tamanhoDaEntrada, nint saida, int tamanhoDaSaida, out int devolvidos, nint sobreposto);

    /// <summary>Os fontes C# da pasta e das subpastas, sem os gerados pelo build (bin e obj), em ordem ordinal.</summary>
    private static string[] FontesCs(string pasta)
        => [.. Directory.GetFiles(pasta, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(pasta, f).Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj"))
            .Order(StringComparer.Ordinal)];
}
