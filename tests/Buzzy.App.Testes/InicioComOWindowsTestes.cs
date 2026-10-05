using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P9 (Q-04; DEC-038, itens 10 a 12; critérios 5 e 6 na parte automática): o início com o Windows. As regras
/// puras por tabela; o simulado; a regra única da execução, que com perfil de teste nunca chega ao registro; o controle da
/// caixa, só pelo pedido e com a marca vinda da leitura; e a contenção na fonte do adaptador real, que nenhum teste executa.
/// </summary>
internal sealed class InicioComOWindowsTestes
{
    private const string Aqui = @"C:\Programas\Buzzy\Buzzy.exe";
    private static readonly byte[] AprovadoPeloWindows = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] DesligadoPeloWindows = [3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8];

    [Teste]
    public void Regras_PorTabela()
    {
        foreach ((string? caminho, bool valido) in new (string?, bool)[]
        {
            (Aqui, true), (@"D:\x\BUZZY.EXE", true), (@"C:\Program Files\dotnet\dotnet.exe", false), (@"Buzzy.exe", false), (@"C:\a""b\Buzzy.exe", false),
            ("C:\\a\tb\\Buzzy.exe", false), (null, false), ("", false), (@"C:\" + new string('a', 1100) + @"\Buzzy.exe", false), (@"\\servidor\pasta\Buzzy.exe", true),
        })
            Afirmar.Igual(valido, RegrasDoInicio.CaminhoValido(caminho), $"caminho {caminho?[..Math.Min(40, caminho.Length)]}");
        Afirmar.Igual($"\"{Aqui}\"", RegrasDoInicio.DadoDoRun(Aqui), "entre aspas, sem argumentos");

        foreach ((string? valor, bool desta) in new (string?, bool)[]
        {
            ($"\"{Aqui}\"", true), (Aqui, true), (@"""c:\programas\buzzy\BUZZY.exe""", true), (@"""C:\Programas\Buzzy\.\Buzzy.exe""", true),
            (@"""D:\outra\Buzzy.exe""", false), (@"""C:\Programas\Buzzy\Buzzy.exe"" --perfil-de-teste x", false), ("", false), (null, false), ("\"\0\"", false),
            // Relativos dependeriam da pasta atual do processo: nunca desta cópia (DEC-040, item 7).
            ("Buzzy.exe", false), (@".\Buzzy.exe", false), (@"\Programas\Buzzy\Buzzy.exe", false), (@"C:Programas\Buzzy\Buzzy.exe", false), (@"""Buzzy.exe""", false),
        })
            Afirmar.Igual(desta, RegrasDoInicio.DestaCopia(valor, Aqui), $"valor {valor}");

        Afirmar.Igual(AprovacaoDoInicio.Ausente, RegrasDoInicio.Aprovacao(null), "sem marca");
        Afirmar.Igual(AprovacaoDoInicio.Ligada, RegrasDoInicio.Aprovacao(AprovadoPeloWindows), "0x02");
        Afirmar.Igual(AprovacaoDoInicio.Desligada, RegrasDoInicio.Aprovacao(DesligadoPeloWindows), "0x03");
        Afirmar.Igual(AprovacaoDoInicio.Desconhecida, RegrasDoInicio.Aprovacao([3, 0]), "outro tamanho");

        Afirmar.Igual(EstadoDoInicio.Indisponivel, RegrasDoInicio.Avaliar(false, null, null, Aqui), "erro de leitura");
        Afirmar.Igual(EstadoDoInicio.Indisponivel, RegrasDoInicio.Avaliar(true, null, null, @"C:\dotnet\dotnet.exe"), "rodando pelo dotnet");
        Afirmar.Igual(EstadoDoInicio.Desligado, RegrasDoInicio.Avaliar(true, null, DesligadoPeloWindows, Aqui), "sem o valor");
        Afirmar.Igual(EstadoDoInicio.Ligado, RegrasDoInicio.Avaliar(true, $"\"{Aqui}\"", null, Aqui), "desta cópia");
        Afirmar.Igual(EstadoDoInicio.Ligado, RegrasDoInicio.Avaliar(true, $"\"{Aqui}\"", [9], Aqui), "aprovação desconhecida vale aprovada");
        Afirmar.Igual(EstadoDoInicio.DesativadoPeloWindows, RegrasDoInicio.Avaliar(true, $"\"{Aqui}\"", DesligadoPeloWindows, Aqui), "desligado no Windows");
        Afirmar.Igual(EstadoDoInicio.OutroCaminho, RegrasDoInicio.Avaliar(true, @"""D:\x\Buzzy.exe""", null, Aqui), "outra cópia");

        foreach ((EstadoDoInicio estado, bool marcar, bool? acao) in new (EstadoDoInicio, bool, bool?)[]
        {
            (EstadoDoInicio.Desligado, true, true), (EstadoDoInicio.OutroCaminho, true, true), (EstadoDoInicio.Ligado, false, false),
            (EstadoDoInicio.DesativadoPeloWindows, false, false), (EstadoDoInicio.Ligado, true, null), (EstadoDoInicio.Desligado, false, null),
            (EstadoDoInicio.OutroCaminho, false, null), (EstadoDoInicio.DesativadoPeloWindows, true, null), (EstadoDoInicio.Indisponivel, true, null), (EstadoDoInicio.Indisponivel, false, null),
        })
            Afirmar.Igual(acao, RegrasDoInicio.AcaoDoPedido(estado, marcar), $"{estado}, marcar={marcar}");
    }

    [Teste]
    public void Simulado_LigaComAspas_ENuncaApagaOutraCopia()
    {
        var s = new InicioSimulado(Aqui);
        Afirmar.Igual((ModoDoInicio.Simulado, EstadoDoInicio.Desligado), (s.Modo, s.Ler()), "começa desligado");
        Afirmar.Igual((ResultadoDoInicio.Ok, $"\"{Aqui}\"", EstadoDoInicio.Ligado), (s.Ligar(), s.ValorRun, s.Ler()), "ligar grava o caminho entre aspas");
        s.AprovacaoDoWindows = DesligadoPeloWindows;
        Afirmar.Igual(EstadoDoInicio.DesativadoPeloWindows, s.Ler(), "o Windows desligou");
        Afirmar.Igual((ResultadoDoInicio.Ok, (string?)null), (s.Desligar(), s.ValorRun), "desligar apaga o desta cópia");

        var outra = new InicioSimulado(Aqui, @"""D:\x\Buzzy.exe""");
        Afirmar.Igual((ResultadoDoInicio.NaoEDestaCopia, @"""D:\x\Buzzy.exe"""), (outra.Desligar(), outra.ValorRun), "o valor de outra cópia nunca é apagado");
        Afirmar.Igual(ResultadoDoInicio.Indisponivel, new InicioSimulado(@"C:\dotnet\dotnet.exe").Ligar(), "pelo dotnet, indisponível");
        Afirmar.Igual((EstadoDoInicio.Indisponivel, ResultadoDoInicio.Indisponivel, ResultadoDoInicio.Indisponivel), (new InicioIndisponivel().Ler(), new InicioIndisponivel().Ligar(), new InicioIndisponivel().Desligar()), "nenhum");
    }

    // A regra única da execução: com perfil de teste, nunca o registro; o registro só sem perfil, com a pasta do Buzzy e com
    // a persistência ligada. Só o modo é conferido: nenhum método do adaptador real é chamado.
    [Teste]
    public void DaExecucao_ComPerfilNuncaORegistro()
    {
        foreach ((string? perfil, bool desligada, string? pasta, ModoDoInicio modo) in new (string?, bool, string?, ModoDoInicio)[]
        {
            ("integracao", false, @"C:\x\Buzzy", ModoDoInicio.Simulado),
            ("Integracao", false, @"C:\x\Buzzy", ModoDoInicio.Indisponivel),
            ("..", false, @"C:\x\Buzzy", ModoDoInicio.Indisponivel),
            ("integracao", true, @"C:\x\Buzzy", ModoDoInicio.Indisponivel),
            ("integracao", false, null, ModoDoInicio.Simulado),
            (null, true, @"C:\x\Buzzy", ModoDoInicio.Indisponivel),
            (null, false, null, ModoDoInicio.Indisponivel),
            (null, false, @"C:\x\Buzzy", ModoDoInicio.Registro),
        })
            Afirmar.Igual(modo, InicioComOWindows.DaExecucao(perfil, desligada, pasta, Aqui).Modo, $"perfil {perfil ?? "-"}, persistência desligada {desligada}, pasta {pasta ?? "-"}");
    }

    private static (CaixaDeComando Caixa, TextBlock Estado, ControleDoInicio Controle, List<string> Log) Montar(InicioSimulado s)
    {
        var caixa = new CaixaDeComando { Content = "_Iniciar com o Windows" };
        var estado = new TextBlock();
        var log = new List<string>();
        var controle = new ControleDoInicio(caixa, estado, s, campos => log.Add(string.Join("|", campos.Select(c => $"{c.Campo}={c.Valor}"))));
        controle.Atualizar("aberta");
        return (caixa, estado, controle, log);
    }

    private static void Toggle(CaixaDeComando c) => ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(c).GetPattern(PatternInterface.Toggle)).Toggle();

    // O controle: a marca vem da leitura; o pedido (inclusive o Toggle do Narrador) é o único caminho até Ligar e Desligar; a
    // marca posta pelo código nunca liga; desativado pelo Windows, marcada e habilitada, e desmarcar desliga; indisponível,
    // desabilitada.
    [Teste]
    public void Controle_SoPeloPedido_EAMarcaDaLeitura()
    {
        var s = new InicioSimulado(Aqui);
        (CaixaDeComando caixa, TextBlock texto, ControleDoInicio controle, List<string> log) = Montar(s);
        Afirmar.Igual((false, true, Visibility.Visible), (caixa.IsChecked == true, caixa.IsEnabled, texto.Visibility), "desligado, com o aviso de simulado");
        Afirmar.Contem("simulado", texto.Text);
        Afirmar.Igual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(texto), "região viva");

        caixa.Marcar(true);
        Afirmar.Igual(0, s.Ligacoes, "a marca pelo código não liga");
        caixa.Marcar(false);
        Toggle(caixa);
        Afirmar.Igual((1, true, EstadoDoInicio.Ligado), (s.Ligacoes, caixa.IsChecked == true, controle.Estado), "o Toggle da UIA liga, e a marca vem da leitura");
        Afirmar.Verdadeiro(log.Any(l => l.Contains("acao=ligar", StringComparison.Ordinal) && l.Contains("resultado=Ok", StringComparison.Ordinal)), string.Join(" / ", log));
        Afirmar.Falso(log.Any(l => l.Contains(Aqui, StringComparison.OrdinalIgnoreCase)), "o caminho nunca vai ao log");

        s.AprovacaoDoWindows = DesligadoPeloWindows;
        controle.Atualizar("ativada");
        Afirmar.Igual((true, true, EstadoDoInicio.DesativadoPeloWindows), (caixa.IsChecked == true, caixa.IsEnabled, controle.Estado), "desativado pelo Windows: marcada e habilitada");
        Afirmar.Contem("Desativado nas Configurações do Windows", texto.Text);
        Toggle(caixa);
        Afirmar.Igual((1, EstadoDoInicio.Desligado), (s.Desligamentos, controle.Estado), "desmarcar desliga");
        Afirmar.Igual(DesligadoPeloWindows, s.AprovacaoDoWindows, "a marca do Windows nunca é tocada");

        var outra = new InicioSimulado(Aqui, @"""D:\x\Buzzy.exe""");
        (CaixaDeComando caixaOutra, TextBlock textoOutra, ControleDoInicio controleOutra, _) = Montar(outra);
        Afirmar.Igual((false, EstadoDoInicio.OutroCaminho), (caixaOutra.IsChecked == true, controleOutra.Estado), "outra cópia: desmarcada");
        Afirmar.Contem("Outra cópia", textoOutra.Text);
        caixaOutra.Marcar(false);
        Afirmar.Igual(0, outra.Desligamentos, "nada a desmarcar");
        Toggle(caixaOutra);
        Afirmar.Igual((1, EstadoDoInicio.Ligado), (outra.Ligacoes, controleOutra.Estado), "marcar passa a usar esta cópia");

        var porDotnet = new InicioSimulado(@"C:\dotnet\dotnet.exe");
        (CaixaDeComando caixaDotnet, _, _, _) = Montar(porDotnet);
        Afirmar.Falso(caixaDotnet.IsEnabled, "indisponível: desabilitada");
    }

    private static string Fonte(params string[] partes) => File.ReadAllText(Path.Combine([Caminhos.Raiz, "src", .. partes]));

    private static IEnumerable<string> FontesDoProduto() => Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"));

    // A contenção na fonte (DEC-038, item 12): a gravação e o apagamento só no adaptador, uma declaração e uma chamada cada,
    // com chave, nome e tipo constantes; o construtor privado, criado só pela regra da execução; Ligar e Desligar chamados só
    // pelo controle da caixa; o núcleo sem nada do início.
    [Teste]
    public void Contencao_NaFonte()
    {
        string arquivo = Path.Combine("Buzzy.App", "Plataforma", "InicioComOWindows.cs");
        Afirmar.Sequencia(["InicioComOWindows.cs"], FontesDoProduto().Where(f => Regex.IsMatch(File.ReadAllText(f), @"RegSetValue|RegDeleteValue|RegCreateKey")).Select(Path.GetFileName), "quem grava ou apaga no registro");
        string codigo = Regex.Replace(Fonte("Buzzy.App", "Plataforma", "InicioComOWindows.cs"), @"//.*|/\*[\s\S]*?\*/", "");
        Afirmar.Igual(1, Regex.Matches(codigo, @"extern int RegSetValueExW\(").Count, "uma declaração de RegSetValueExW");
        Afirmar.Igual(1, Regex.Matches(codigo, @"extern int RegDeleteValueW\(").Count, "uma declaração de RegDeleteValueW");
        MatchCollection grava = Regex.Matches(codigo, @"Nativo\.RegSetValueExW\(([^;]*)\);");
        MatchCollection apaga = Regex.Matches(codigo, @"Nativo\.RegDeleteValueW\(([^;]*)\);");
        Afirmar.Igual((1, 1), (grava.Count, apaga.Count), "uma chamada de cada");
        Afirmar.Igual("chave, NomeDoValor, 0, REG_SZ, bytes, bytes.Length", grava[0].Groups[1].Value, "a gravação só com constantes");
        Afirmar.Igual("chave, NomeDoValor", apaga[0].Groups[1].Value, "o apagamento só com constantes");
        Afirmar.Sequencia(["ChaveRun KEY_SET_VALUE", "ChaveRun KEY_SET_VALUE", "ChaveRun KEY_QUERY_VALUE"],
            Regex.Matches(codigo, @"RegOpenKeyExW\(HKEY_CURRENT_USER, (\w+), 0, (\w+),").Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}"), "só a chave Run é aberta: para gravar nas duas ações, para consultar na conferência");
        Afirmar.Contem(@"private const string ChaveRun = @""Software\Microsoft\Windows\CurrentVersion\Run"";", codigo);
        Afirmar.Contem("private const string NomeDoValor = \"Buzzy\";", codigo);
        Afirmar.Contem("private const int REG_SZ = 1;", codigo);
        Afirmar.Contem("private InicioComOWindows(string? caminho)", codigo);
        Afirmar.Igual(1, Regex.Matches(codigo, @"new InicioComOWindows\(").Count, "criado só em DaExecucao");
        int ligar = codigo.IndexOf("public ResultadoDoInicio Ligar()", StringComparison.Ordinal), desligar = codigo.IndexOf("public ResultadoDoInicio Desligar()", StringComparison.Ordinal);
        Afirmar.Verdadeiro(ligar < grava[0].Index && grava[0].Index < desligar && desligar < apaga[0].Index, "a gravação em Ligar e o apagamento em Desligar");
        Afirmar.Igual(Regex.Matches(codigo, "ChaveDaAprovacao").Count - 1, Regex.Matches(codigo, @"RegGetValueW\(HKEY_CURRENT_USER, ChaveDaAprovacao").Count, "o StartupApproved só nas leituras");
        Afirmar.Falso(Regex.IsMatch(codigo, @"RegOpenKeyExW\([^)]*ChaveDaAprovacao"), "o StartupApproved nunca é aberto para gravar");

        string[] chamadores = [.. FontesDoProduto().Where(f => Regex.IsMatch(File.ReadAllText(f), @"(?<!Diagnostico)\.(Ligar|Desligar)\(\)")).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(["ControleDoInicio.cs"], chamadores, "quem chama Ligar e Desligar");
        Afirmar.Falso(Regex.IsMatch(File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", arquivo)), @"Diagnostico\.Evento"), "o adaptador não escreve no log (o caminho nunca vai lá)");
        string[] nucleo = [.. Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src", "Buzzy.Core"), "*.cs", SearchOption.AllDirectories).Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"InicioComOWindows|IniciarComOWindows|CurrentVersion\\Run"))];
        Afirmar.Sequencia([], nucleo.Select(Path.GetFileName), "o núcleo não conhece o início com o Windows");
    }
}
