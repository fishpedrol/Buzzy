using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Buzzy.Verificacao;

/// <summary>
/// Buzzy.Verificacao — verificação sintética dos critérios [MANUAL] das Fases 1, 3, 4 e 5 e do tamagotchi (DEC-027 e
/// DEC-028; crítica de integração, seção 6).
///
/// Uso:
///   Buzzy.Verificacao.exe --injetar-input-na-tela [--fase 1|3|4|5|tamagotchi] [--semente N] [--ocioso S] [--espera-ocioso S]
///   Buzzy.Verificacao.exe --receptor &lt;log&gt; &lt;x&gt; &lt;y&gt; &lt;largura&gt; &lt;altura&gt;   (uso interno)
///
/// Sem <c>--injetar-input-na-tela</c>, só imprime o uso e sai com código 2, sem abrir nada.
/// Com ela, move o cursor e envia cliques e teclas SINTÉTICOS: só rodar depois de avisar o
/// usuário. Espera o usuário ficar S segundos sem mexer no mouse ou no teclado (padrão 20) e
/// aborta se houver input que não seja da ferramenta durante a verificação.
///
/// Códigos de saída: 0 sem falhas (itens SIMULADO listados na última linha); 1 com falhas,
/// execução inválida ou inconclusiva; 2 uso inválido ou pré-condição não atendida (nada foi
/// injetado); 3 o usuário não ficou ocioso a tempo (nada foi injetado).
/// </summary>
internal static class Programa
{
    private const string OpcaoDeInjetar = "--injetar-input-na-tela";

    private const string TextoDeUso = """
        Buzzy.Verificacao: verificação dos critérios [MANUAL] das Fases 1, 3, 4 e 5 e do tamagotchi com input SINTÉTICO.

        ATENÇÃO: abre o Buzzy e uma janela de teste, move o cursor e envia cliques e teclas por
        SendInput. Rode só com o computador livre e depois de avisar quem o usa. A Fase 3 também
        arrasta o Buzzy pela tela e usa Alt+Tab e a tecla Windows no meio de um arraste. O
        tamagotchi invoca itens pelo menu, arrasta-os até o personagem e o deixa andar sozinho,
        também paranoico, achando que tem alguém no teto, e fumando um baseado por conta própria
        (de desenho animado); leva de 7 a 13 minutos, com um repouso de 60 s em que nada deve ser
        tocado. A Fase 5 deixa o Buzzy atravessar sozinho para o outro monitor e o arrasta até lá, fecha e
        reabre; leva cerca de 2 minutos. O custo (--fase custo) abre o Buzzy parado duas vezes, com e sem o
        observador da janela em primeiro plano, e por 60 s em cada uma move o cursor e digita no aplicativo
        de teste, medindo a CPU do Buzzy (protótipo P7); leva cerca de 3 minutos.

        Os Buzzy abertos usam o perfil de teste "verificacao" (%LOCALAPPDATA%\Buzzy\testes\verificacao),
        apagado antes de cada abertura: as configurações reais do usuário não são lidas nem gravadas.

        Uso:
          Buzzy.Verificacao.exe --injetar-input-na-tela [--fase 1|3|4|5|7|tamagotchi|custo|configuracoes] [--semente N] [--ocioso S] [--espera-ocioso S]

          --injetar-input-na-tela  obrigatória: confirma que a ferramenta pode agir na tela.
          --fase F                 1 (padrão): shell do desktop; 3: input e arraste; 4: movimento e superfícies;
                                   5: travessia entre monitores e soltar, fechar e reabrir no mesmo lugar;
                                   tamagotchi: emoção dominante, itens, uso e onda (DEC-027 e DEC-028);
                                   custo: o custo do observador da janela em primeiro plano com o mouse e o teclado em uso (P7);
                                   configuracoes: o painel de energia e as configurações pelo menu, com teclado e UIA (Fase 8; cerca de 1 minuto).
          --semente N              só com --fase tamagotchi: a semente dos Buzzy abertos pausados (padrão 2028);
                                   os abertos com a agenda ligada usam sementes escolhidas por simulação do núcleo.
          --ocioso S               segundos sem input do usuário antes de começar (padrão 20).
          --espera-ocioso S        quanto esperar por essa ociosidade antes de desistir (padrão 180).

        Rótulos do relatório: input por SendInput é SINTÉTICO; notificação da bandeja postada pela
        ferramenta é SIMULADA. Nenhum dos dois é gesto humano, e SIMULADO nunca vale como OK.

        Códigos de saída: 0 sem falhas (itens SIMULADO listados no fim); 1 falhas ou execução
        inválida ou inconclusiva; 2 uso inválido ou pré-condição não atendida; 3 sem ociosidade.
        """;

    internal static volatile bool Cancelado;

    [STAThread]
    internal static int Main(string[] args)
    {
        if (args.Length == 6 && args[0] == "--receptor")
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            int N(int i) => int.Parse(args[i], CultureInfo.InvariantCulture);
            return app.Run(new JanelaReceptor(args[1], N(2), N(3), N(4), N(5)));
        }

        Console.OutputEncoding = Encoding.UTF8;
        bool injetar = false;
        int ociosoS = 20, esperaS = 180;
        string fase = "1";
        ulong? semente = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case OpcaoDeInjetar:
                    injetar = true;
                    break;
                case "--fase":
                    if (i + 1 >= args.Length || args[i + 1] is not ("1" or "3" or "4" or "5" or "7" or "tamagotchi" or "custo" or "configuracoes"))
                        return Uso("--fase precisa de 1, 3, 4, 5, 7, tamagotchi, custo ou configuracoes.");
                    fase = args[++i];
                    break;
                case "--semente":
                    if (i + 1 >= args.Length || !ulong.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong valor))
                        return Uso("--semente precisa de um número inteiro, zero ou maior.");
                    semente = valor;
                    i++;
                    break;
                case "--ocioso" or "--espera-ocioso":
                    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int segundos) || segundos < 1)
                        return Uso($"{args[i]} precisa de um número inteiro de segundos, maior que zero.");
                    if (args[i] == "--ocioso") ociosoS = segundos; else esperaS = segundos;
                    i++;
                    break;
                default:
                    return Uso($"Argumento não reconhecido: {args[i]}");
            }
        }
        if (!injetar)
            return Uso($"Falta {OpcaoDeInjetar}. Nada foi aberto nem injetado.");
        if (semente is not null && fase != "tamagotchi")
            return Uso("--semente só vale com --fase tamagotchi. Nada foi aberto nem injetado.");

        string raiz, configuracao;
        try
        {
            raiz = LocalizarRaiz();
            configuracao = DeduzirConfiguracao(AppContext.BaseDirectory);
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine($"ABORTADO: {e.Message} Nada foi aberto nem injetado.");
            return 2;
        }

        // O Buzzy testado é o da mesma configuração (Release ou Debug) desta ferramenta.
        string exeBuzzy = Path.Combine(raiz, "src", "Buzzy.App", "bin", configuracao, "net10.0-windows", "Buzzy.exe");
        string exeProprio = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável desconhecido.");
        string resultados = Path.Combine(raiz, "resultados");
        string nomeDaFase = fase switch { "tamagotchi" => "do tamagotchi (DEC-027 e DEC-028)", "custo" => "do custo do observador (protótipo P7)", "configuracoes" => "das configurações (Fase 8)", _ => $"da Fase {fase}" };
        using var rel = new Relatorio(Path.Combine(resultados, fase switch { "tamagotchi" => "verificacao-tamagotchi.log", "custo" => "verificacao-custo.log", "configuracoes" => "verificacao-configuracoes.log", _ => $"verificacao-fase{fase}.log" }));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; Cancelado = true; };

        rel.Linha("");
        rel.Linha("================================================================");
        rel.Linha($"Buzzy.Verificacao — critérios manuais {nomeDaFase} com input SINTÉTICO — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        rel.Linha($"SO: {RuntimeInformation.OSDescription}; runtime {RuntimeInformation.FrameworkDescription}; configuração {configuracao}");
        rel.Linha("Todo clique e tecla desta verificação é injetado por SendInput (marca de injetado): SINTÉTICO, não é gesto humano.");
        rel.Linha("Notificação da bandeja postada pela ferramenta (PostMessage) é SIMULADA: o resultado recebe SIMULADO, nunca OK,");
        rel.Linha("e fica pendente de verificação manual real.");
        rel.Linha("================================================================");

        if (Environment.IsPrivilegedProcess)
        {
            rel.Linha("ABORTADO: a verificação está rodando como administrador. O Buzzy herdaria a elevação, recusaria rodar e mostraria uma caixa de aviso. Nada foi aberto nem injetado.");
            return 2;
        }
        if (!Nativo.ThreadEmPerMonitorV2())
        {
            rel.Linha("ABORTADO: a verificação não está em Per-Monitor V2 (app.manifest não embutido?); cliques e testes de acerto não bateriam com os pontos do Buzzy. Nada foi aberto nem injetado.");
            return 2;
        }
        if (!File.Exists(exeBuzzy)) { rel.Linha($"ABORTADO: Buzzy.exe ({configuracao}) não compilado em {exeBuzzy}. Nada foi aberto nem injetado."); return 2; }
        if (Injetor.TamanhoInput != 40) { rel.Linha($"ABORTADO: INPUT com {Injetor.TamanhoInput} bytes; nada foi injetado."); return 2; }
        Process[] abertos = Process.GetProcessesByName("Buzzy");
        try
        {
            if (abertos.Length > 0)
            {
                rel.Linha($"ABORTADO: já há Buzzy aberto (pids {string.Join(", ", abertos.Select(p => p.Id))}); a verificação não encerra processos que não abriu. Nada foi injetado.");
                return 2;
            }
        }
        finally
        {
            foreach (Process p in abertos) p.Dispose();
        }

        var espera = Stopwatch.StartNew();
        while (Nativo.OciosoMs() < ociosoS * 1000u)
        {
            if (espera.Elapsed.TotalSeconds > esperaS || Cancelado)
            {
                rel.Linha($"ABORTADO: houve input do usuário nos últimos {ociosoS} s durante {esperaS} s de espera. Nada foi injetado.");
                return 3;
            }
            Thread.Sleep(500);
        }
        // Referência para detectar interferência: qualquer input posterior a este que não seja da
        // ferramenta invalida a execução (GetLastInputInfo dá só a hora, nunca a tecla).
        if (Nativo.UltimoInput() is not { } ultimoInputDoUsuario)
        {
            rel.Linha("ABORTADO: GetLastInputInfo falhou; sem ele não há como detectar interferência pelo teclado. Nada foi injetado.");
            return 2;
        }
        rel.Linha($"Sem input do usuário há {Nativo.OciosoMs() / 1000.0:0.0} s; começando.");

        // Os arquivos reais de configuração do usuário, vistos só por fora (existência, tamanho e datas), antes e depois: os
        // Buzzy abertos usam o perfil de teste e nunca podem mudá-los (revisão de segurança do bloco P6-P9, achado 8).
        string fotoAntes = Buzzy.App.Testes.Integracao.ArquivosReais.Foto();
        var v = new Verificacao(exeBuzzy, exeProprio, resultados, rel, ultimoInputDoUsuario);
        Sumario s = fase switch
        {
            "3" => v.ExecutarFase3(),
            "4" => v.ExecutarFase4(),
            "5" => v.ExecutarFase5(),
            "7" => v.ExecutarFase7(),
            "custo" => v.ExecutarCusto(),
            "configuracoes" => v.ExecutarConfiguracoes(),
            "tamagotchi" => v.ExecutarTamagotchi(semente),
            _ => v.Executar(),
        };
        string fotoDepois = Buzzy.App.Testes.Integracao.ArquivosReais.Foto();
        bool reaisIntocados = fotoDepois == fotoAntes;
        rel.Linha(reaisIntocados
            ? "Arquivos reais do usuário (%LOCALAPPDATA%\\Buzzy), vistos só por fora: intocados."
            : $"FALHA: os arquivos reais do usuário (%LOCALAPPDATA%\\Buzzy) mudaram durante a verificação, vistos só por fora. Antes: {fotoAntes}. Depois: {fotoDepois}.");
        string simulados = s.Simulados.Count == 0 ? "nenhum" : string.Join(" | ", s.Simulados);
        string naoExercitados = s.NaoExercitados.Count == 0 ? "nenhum" : string.Join(" | ", s.NaoExercitados);
        rel.Linha($"==== Resultado: {(s.Falhas == 0 ? "sem falhas" : $"{s.Falhas} falha(s)")} — {s.Ok} OK, {s.NaoAplicavel} N/A, {s.Simulados.Count} SIMULADO " +
                  $"(input SINTÉTICO; nada disto é gesto humano). SIMULADO, pendente de verificação manual real: {simulados}. " +
                  $"Não exercitado, pendente de verificação manual real: {naoExercitados} ====");
        return s.Falhas == 0 && reaisIntocados ? 0 : 1;
    }

    private static int Uso(string motivo)
    {
        Console.Error.WriteLine(motivo);
        Console.Error.WriteLine();
        Console.Error.WriteLine(TextoDeUso);
        return 2;
    }

    private static string LocalizarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Buzzy.Build.props"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Raiz do repositório (Buzzy.Build.props) não encontrada.");
    }

    /// <summary>
    /// Nome da pasta logo abaixo de <c>bin</c> no caminho desta ferramenta
    /// (<c>...\bin\Release\net10.0-windows\</c> dá <c>Release</c>).
    /// </summary>
    private static string DeduzirConfiguracao(string pastaDoExecutavel)
    {
        var dir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(pastaDoExecutavel));
        while (dir.Parent is { } pai)
        {
            if (string.Equals(pai.Name, "bin", StringComparison.OrdinalIgnoreCase)) return dir.Name;
            dir = pai;
        }
        throw new InvalidOperationException($"Não deu para deduzir a configuração (Release ou Debug) da pasta {pastaDoExecutavel}: esperado ...\\bin\\<configuração>\\<framework>\\.");
    }
}
