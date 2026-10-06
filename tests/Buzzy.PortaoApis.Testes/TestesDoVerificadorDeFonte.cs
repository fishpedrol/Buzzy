using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>Busca dos identificadores proibidos no código-fonte, depois da remoção de comentários.</summary>
public sealed class TestesDoVerificadorDeFonte : IDisposable
{
    private const string Arquivo = @"C:\src\Amostra.cs";

    private readonly PastaTemporaria _pasta = new();

    public void Dispose() => _pasta.Dispose();

    private static List<Violacao> Verificar(string fonte) => [.. VerificadorDeFonte.VerificarTexto(Arquivo, fonte)];

    [Teste]
    public void NomeDeApiDentroDeTextoEhAcusado()
    {
        const string fonte = "var p = GetProcAddress(modulo, \"SendInput\");";
        List<Violacao> violacoes = Verificar(fonte);

        Afirmar.Sequencia<string>(["GetProcAddress", "SendInput"], violacoes.Select(v => v.Api));
        Afirmar.Igual(Categoria.CodigoDinamico, violacoes[0].Categoria);
        Afirmar.Igual(Categoria.InjetarInput, violacoes[1].Categoria);
        Afirmar.Igual(1, violacoes[1].Linha);
        Afirmar.Igual(fonte.IndexOf("SendInput", StringComparison.Ordinal) + 1, violacoes[1].Coluna);
        Afirmar.Igual(Codigos.CodigoFonte, violacoes[1].Codigo);
        Afirmar.Igual(Arquivo, violacoes[1].Arquivo);
    }

    [Teste]
    public void UsoRestritoSoValeNoArquivoDoObservador()
    {
        // DEC-034: só o arquivo do observador de tela cheia cita as três funções dele; as outras regras valem nele também.
        const string fonte = "SetWinEventHook(a); GetForegroundWindow(); GetWindowThreadProcessId(h, 0); WindowFromPoint(p); SendInput();";
        string[] doObservador =
        [
            @"C:\repo\src\Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs",
            "C:/repo/src/Buzzy.App/Plataforma/ObservadorDeTelaCheia.cs",
            @"C:\REPO\SRC\BUZZY.APP\PLATAFORMA\observadordetelacheia.cs",
        ];
        foreach (string arquivo in doObservador)
            Afirmar.Sequencia<string>(["WindowFromPoint", "SendInput"], VerificadorDeFonte.VerificarTexto(arquivo, fonte).Select(v => v.Api), arquivo);

        string[] outros =
        [
            @"C:\repo\src\Buzzy.App\Plataforma\Win32.cs",
            @"C:\repo\src\Buzzy.Core\Plataforma\ObservadorDeTelaCheia.cs",
            @"C:\repo\src\Buzzy.App\ObservadorDeTelaCheia.cs",
            @"C:\repo\src\Buzzy.App\Plataforma\OutroObservadorDeTelaCheia.cs",
            @"C:\repo\Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs",
        ];
        foreach (string arquivo in outros)
        {
            Afirmar.Sequencia<string>(["SetWinEventHook", "GetForegroundWindow", "GetWindowThreadProcessId", "WindowFromPoint", "SendInput"],
                VerificadorDeFonte.VerificarTexto(arquivo, fonte).Select(v => v.Api), arquivo);
        }
    }

    [Teste]
    public void ComentariosNaoSaoAcusados()
    {
        const string fonte = "// SendInput\n/* GetForegroundWindow\n WindowFromPoint */\n/// <see cref=\"BitBlt\"/>\nint x; // Process.Start";
        Afirmar.Igual(0, Verificar(fonte).Count);
    }

    [Teste]
    public void SoPalavrasInteirasContam()
    {
        Afirmar.Igual(0, Verificar("EnviarSendInputAgora(); var MySendInput = 1; SendInput_2(); x.SendInputs(); var ws2_32x = 0;").Count);
        List<Violacao> violacoes = Verificar("SendInputW(); sendinput(); @SendInput(); x.SendInputA();");
        Afirmar.Sequencia<string>(["SendInputW", "sendinput", "SendInput", "SendInputA"], violacoes.Select(v => v.Api));
        Afirmar.Verdadeiro(violacoes.All(v => v.Regra?.Alvo == "SendInput"));
    }

    [Teste]
    public void ProcessStartSoQualificado()
    {
        Afirmar.Sequencia<string>(["Process.Start"], Verificar("System.Diagnostics.Process.Start(\"x\");").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Process.Start"], Verificar("Process . Start(x);").Select(v => v.Api));

        List<Violacao> quebrada = Verificar("var p =\n    Process\n        .Start(x);");
        Afirmar.Igual(1, quebrada.Count);
        Afirmar.Igual(2, quebrada[0].Linha);
        Afirmar.Igual(5, quebrada[0].Coluna);

        const string permitido = "temporizador.Start(); Stopwatch.StartNew(); Process.GetCurrentProcess(); processo.StartInfo = null; Process?.Start();";
        Afirmar.Igual(0, Verificar(permitido).Count);
        Afirmar.Sequencia<string>(["ProcessStartInfo"], Verificar("new ProcessStartInfo(\"cmd\");").Select(v => v.Api));
    }

    [Teste]
    public void NamespacesENomesSimplesDeRede()
    {
        Afirmar.Sequencia<string>(["System.Net.Http"], Verificar("using System.Net.Http;").Select(v => v.Api));
        Afirmar.Sequencia<string>(["System.Net.Sockets"], Verificar("global using System.Net.Sockets;").Select(v => v.Api));
        Afirmar.Sequencia<string>(["HttpClient"], Verificar("var c = new HttpClient();").Select(v => v.Api));
        Afirmar.Sequencia<string>(["System.Net.Http", "HttpClient"], Verificar("var c = new global::System.Net.Http.HttpClient();").Select(v => v.Api));
        Afirmar.Sequencia<string>(["HttpListener"], Verificar("var h = new System.Net.HttpListener();").Select(v => v.Api));
        Afirmar.Sequencia<string>(["ws2_32"], Verificar("[DllImport(\"ws2_32.dll\")] static extern int connect(nint s, nint a, int n);").Select(v => v.Api));
        Afirmar.Igual(0, Verificar("using System.Net; var ip = IPAddress.Loopback;").Count);
    }

    [Teste]
    public void ExcecoesDocumentadasNaoSaoAcusadas()
    {
        const string fonte = """
            using Microsoft.Win32;
            using System.Windows.Automation;
            SystemEvents.DisplaySettingsChanged += h;
            AutomationProperties.SetName(botao, "Buzzy");
            var eu = Process.GetCurrentProcess(); var id = eu.Id; var inicio = eu.StartTime;
            var tamanho = Marshal.SizeOf<Ponto>();
            SetWindowPos(janela, 0, 0, 0, 0, 0, 0); Shell_NotifyIcon(1, dados); SetForegroundWindow(janela);
            var exe = Environment.ProcessPath; var pasta = AppContext.BaseDirectory; Assembly.GetExecutingAssembly();
            """;
        List<Violacao> violacoes = Verificar(fonte);
        Afirmar.Igual(0, violacoes.Count, string.Join("; ", violacoes.Select(v => v.Api)));
    }

    [Teste]
    public void RegistroClipboardECodigoDinamico()
    {
        Afirmar.Sequencia<string>(["Registry"], Verificar("Registry.CurrentUser.CreateSubKey(\"x\");").Select(v => v.Api));
        // Clipboard está em duas regras (WPF e Windows Forms): uma violação por ocorrência.
        Afirmar.Sequencia<string>(["Clipboard"], Verificar("System.Windows.Clipboard.GetText();").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Assembly.LoadFrom"], Verificar("Assembly.LoadFrom(caminho);").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Assembly.Load"], Verificar("System.Reflection.Assembly.Load(bytes);").Select(v => v.Api));
        // F9-P10: o que vem vazio ou lança num executável de arquivo único.
        Afirmar.Sequencia<string>(["Assembly.Location"], Verificar("var x = typeof(A).Assembly.Location;").Select(v => v.Api));
        Afirmar.Sequencia<string>(["CodeBase"], Verificar("var x = nome.CodeBase;").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Assembly.GetFiles"], Verificar("var x = typeof(A).Assembly.GetFiles();").Select(v => v.Api));
        Afirmar.Sequencia<string>(["System.Reflection.Emit", "DynamicMethod"], Verificar("new System.Reflection.Emit.DynamicMethod(\"m\", null, null);").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Windows.Graphics.Capture"], Verificar("using Windows.Graphics.Capture;").Select(v => v.Api));
    }

    [Teste]
    public void LinhaEColunaComFimDeLinhaDoWindows()
    {
        List<Violacao> violacoes = Verificar("linha1\r\n  // SendInput\r\n    GetForegroundWindow();");
        Violacao violacao = violacoes.Single();
        Afirmar.Igual(3, violacao.Linha);
        Afirmar.Igual(5, violacao.Coluna);
        Afirmar.Igual(Categoria.LerOutrosAplicativos, violacao.Categoria);
    }

    [Teste]
    public void EscapeDeTextoGrudadoNoNome()
    {
        Afirmar.Sequencia<string>(["SendInput"], Verificar("var s = \"\\tSendInput\";").Select(v => v.Api));
        Afirmar.Sequencia<string>(["Process.Start"], Verificar("var s = \"\\nProcess.Start\";").Select(v => v.Api));
    }

    [Teste]
    public void CadaPadraoDaListaEhEncontrado()
    {
        foreach (Regra regra in ListaProibida.Regras)
        {
            foreach (string padrao in regra.PadroesNaFonte)
            {
                List<Violacao> violacoes = Verificar($"x = {padrao}(1);");
                Afirmar.Verdadeiro(violacoes.Any(v => v.Categoria == regra.Categoria && v.Coluna == 5),
                    $"padrão \"{padrao}\" de {regra.Descricao} não encontrado");
            }
        }
    }

    [Teste]
    public void ListarArquivosIgnoraBinObjEOutrasExtensoes()
    {
        _pasta.Escrever(@"src\A.cs", "class A { }");
        _pasta.Escrever(@"src\Sub\B.cs", "class B { }");
        _pasta.Escrever(@"src\Sub\E.CS", "class E { }");
        _pasta.Escrever(@"src\obj\Gerado.g.cs", "SendInput();");
        _pasta.Escrever(@"src\bin\Release\C.cs", "SendInput();");
        _pasta.Escrever(@"src\Sub\obj\D.cs", "SendInput();");
        _pasta.Escrever(@"src\Sub\BIN\F.cs", "SendInput();");
        _pasta.Escrever(@"src\nota.txt", "SendInput");
        _pasta.Escrever(@"src\Sub\script.csx", "SendInput();");
        string raiz = Path.Combine(_pasta.Caminho, "src");

        List<string> relativos = [.. VerificadorDeFonte.ListarArquivos(raiz).Select(a => Path.GetRelativePath(raiz, a))];
        Afirmar.Sequencia<string>(["A.cs", @"Sub\B.cs", @"Sub\E.CS"], relativos);
    }
}
