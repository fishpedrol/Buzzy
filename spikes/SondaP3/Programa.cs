using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SondaP3;

internal sealed record Opcoes(int Repeticoes, int OciosoS, int EsperaOciosoS);

// Teste de arraste com um receptor nosso (BuzzySpike --modo receptor) no lugar do Bloco de
// Notas. Prova, sem ler app alheio, que o input continua chegando ao app em foco depois de cada
// gesto e que o foco nunca sai dele. Tudo via SendInput.
// Sem --injetar-input-na-tela só mostra o uso e sai com 2, sem abrir nem injetar nada.
internal static class Programa
{
    internal static volatile bool Cancelado;

    private const string OpcaoInjetar = "--injetar-input-na-tela";

    private static readonly string Uso = $"""
        SondaP3 — P3 SINTÉTICO com receptor controlado pelo spike (ferramenta de teste descartável).

        ATENÇÃO: abre janelas de teste, MOVE O CURSOR e injeta cliques e teclas na tela por
        SendInput. Esse input é SINTÉTICO, não é gesto humano. Não execute enquanto alguém
        estiver usando o computador. Sem a opção {OpcaoInjetar}, nada é aberto,
        gravado nem injetado: a sonda só mostra este texto e sai com o código 2.

        Uso: SondaP3.exe {OpcaoInjetar} [--repeticoes N] [--ocioso S] [--espera-ocioso S]
          {OpcaoInjetar}  obrigatória: autoriza abrir as janelas de teste e injetar input
          --repeticoes N           rodadas completas, de 1 a 100 (padrão 1)
          --ocioso S               segundos seguidos sem input do usuário antes de cada rodada,
                                   de 5 a 3600 (padrão 20)
          --espera-ocioso S        quanto esperar por esse intervalo antes de desistir,
                                   de 0 a 86400 (padrão 180)

        Interferência: a rodada é invalidada e repetida (até 4 tentativas) se houver input do
        mouse ou do teclado depois do último evento injetado pela sonda (GetLastInputInfo, com
        tolerância de {Injetor.ToleranciaInputMs} ms; nenhuma tecla é lida) ou se o cursor se afastar mais de
        3 px de onde a sonda o pôs. Depois de interferência, a sonda só solta o que deixou
        abaixado e fecha os processos que abriu; não reposiciona o cursor.

        Relatório: spikes/resultados/p3-receptor.log.
        Códigos de saída: 0 todas as rodadas OK; 1 alguma rodada não OK; 2 uso incorreto ou
        pré-condição não atendida (nada injetado); 3 usuário ativo ou cancelamento durante a
        espera de ociosidade.
        """;

    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Primeiro de tudo: sem a opção explícita não abre, não grava e não injeta nada.
        if (Interpretar(args, out string? erro) is not { } opcoes)
        {
            Console.Error.WriteLine(erro);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Uso);
            return 2;
        }
        int repeticoes = opcoes.Repeticoes, ociosoS = opcoes.OciosoS, esperaOciosoS = opcoes.EsperaOciosoS;

        // Per-Monitor V2 (-4): tudo aqui é em px físicos.
        Nativo.SetProcessDpiAwarenessContext(-4);

        string? spikes = LocalizarSpikes();
        if (spikes is null)
        {
            Console.Error.WriteLine("Pasta spikes/ não encontrada a partir do executável.");
            return 2;
        }

        string exe = Path.Combine(spikes, "BuzzySpike", "bin", "Release", "net10.0-windows", "BuzzySpike.exe");
        string resultados = Path.Combine(spikes, "resultados");
        Directory.CreateDirectory(resultados);

        using var rel = new Relatorio(Path.Combine(resultados, "p3-receptor.log"));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; Cancelado = true; };

        rel.Linha("");
        rel.Linha("================================================================");
        rel.Linha($"SondaP3 — P3 sintético com receptor controlado pelo spike — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        rel.Linha("TODO o input desta sonda é SINTÉTICO (SendInput, com a marca de injetado do Windows).");
        rel.Linha("Não é gesto humano. O Bloco de Notas não é aberto, lido nem usado.");
        rel.Linha($"Interferência: a rodada é INVALIDADA se houver input do mouse ou do teclado depois do último evento injetado "
            + $"(GetLastInputInfo, tolerância {Injetor.ToleranciaInputMs} ms; nenhuma tecla é lida) ou se o cursor se afastar mais de 3 px; "
            + "depois disso a sonda só solta o que deixou abaixado e fecha seus processos, sem reposicionar o cursor.");
        rel.Linha("================================================================");

        if (!File.Exists(exe))
        {
            rel.Linha($"ABORTADO: protótipo não compilado em {exe}");
            return 2;
        }
        if (Injetor.TamanhoInput != 40)
        {
            rel.Linha($"ABORTADO: INPUT com {Injetor.TamanhoInput} bytes; esperado 40 em x64. Nada foi injetado.");
            return 2;
        }
        Process[] abertos = Process.GetProcessesByName("BuzzySpike");
        if (abertos.Length > 0)
        {
            rel.Linha($"ABORTADO: já há {abertos.Length} processo(s) BuzzySpike aberto(s) (pids {string.Join(", ", abertos.Select(p => p.Id))}). "
                + "Eles misturariam os logs; esta sonda não encerra processos que não abriu.");
            return 2;
        }

        DescreverAmbiente(rel);

        int aprovadas = 0;
        var todas = new List<IReadOnlyList<Veredito>>();
        for (int r = 1; r <= repeticoes && !Cancelado; r++)
        {
            // Interferência humana invalida a rodada: espera ficar ocioso e repete, até 4 vezes.
            // Falha de verdade nunca é repetida.
            for (int tentativa = 1; ; tentativa++)
            {
                if (EsperarOcioso(rel, ociosoS, esperaOciosoS) is not uint ultimoInput) return 3;
                var rodada = new Rodada(r, exe, resultados, rel, ultimoInput);
                bool ok = rodada.Executar();
                if (rodada.Invalidada && tentativa < 4 && !Cancelado)
                {
                    rel.Linha($"   Rodada {r} invalidada por interferência (tentativa {tentativa}); aguardando ociosidade para repetir.");
                    continue;
                }
                if (ok) aprovadas++;
                todas.Add(rodada.Vereditos);
                break;
            }
            if (r < repeticoes) Thread.Sleep(2000);
        }

        rel.Linha("");
        rel.Linha($"==== Resultado: {aprovadas} de {todas.Count} rodada(s) com todos os cenários OK (input sintético) ====");
        foreach (var grupo in todas.SelectMany(v => v).GroupBy(v => v.Cenario))
        {
            string resumo = string.Join(" ", grupo.Select(v => v.Resultado));
            rel.Linha($"   {resumo,-24} {grupo.Key}");
        }

        return aprovadas == todas.Count && todas.Count == repeticoes ? 0 : 1;
    }

    // Sem efeito colateral. Null com erro preenchido; sem OpcaoInjetar é sempre erro.
    internal static Opcoes? Interpretar(string[] args, out string? erro)
    {
        bool injetar = false;
        int repeticoes = 1, ociosoS = 20, esperaOciosoS = 180;
        erro = null;
        for (int i = 0; i < args.Length && erro is null; i++)
        {
            switch (args[i])
            {
                case OpcaoInjetar: injetar = true; break;
                case "--repeticoes" when i + 1 < args.Length: erro = LerInteiro("--repeticoes", args[++i], 1, 100, ref repeticoes); break;
                case "--ocioso" when i + 1 < args.Length: erro = LerInteiro("--ocioso", args[++i], 5, 3600, ref ociosoS); break;
                case "--espera-ocioso" when i + 1 < args.Length: erro = LerInteiro("--espera-ocioso", args[++i], 0, 86400, ref esperaOciosoS); break;
                default: erro = $"Argumento não reconhecido ou sem valor: {args[i]}"; break;
            }
        }
        if (erro is null && !injetar)
            erro = $"Falta a opção explícita {OpcaoInjetar}; nada foi aberto nem injetado.";
        return erro is null ? new Opcoes(repeticoes, ociosoS, esperaOciosoS) : null;
    }

    // Devolve a mensagem de erro, ou null.
    private static string? LerInteiro(string opcao, string texto, int minimo, int maximo, ref int valor)
    {
        if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out int lido) && lido >= minimo && lido <= maximo)
        {
            valor = lido;
            return null;
        }
        return $"Valor inválido para {opcao}: '{texto}' (esperado um inteiro de {minimo} a {maximo}).";
    }

    // O teste mexe no cursor e no teclado, então espera ociosoS segundos sem input. Devolve o
    // último input (relógio do GetTickCount): dali em diante, input que não é da sonda é
    // interferência. Null se desistir. O input injetado zera o contador, então a espera recomeça.
    private static uint? EsperarOcioso(Relatorio rel, int ociosoS, int esperaOciosoS)
    {
        var espera = Stopwatch.StartNew();
        while (true)
        {
            if (Nativo.UltimoInput() is not uint ultimo)
            {
                rel.Linha("ABORTADO: GetLastInputInfo falhou; sem como saber se alguém está usando o computador. Nada mais foi injetado.");
                return null;
            }
            int ociosoMs = Math.Max(0, Nativo.MsDepoisDe(Nativo.Agora(), ultimo));
            if (ociosoMs >= ociosoS * 1000)
            {
                rel.Linha($"Sem input do usuário há {ociosoMs / 1000.0:0.0} s; começando.");
                return ultimo;
            }
            if (Cancelado)
            {
                rel.Linha("ABORTADO: execução cancelada durante a espera de ociosidade. Nada mais foi injetado.");
                return null;
            }
            if (espera.Elapsed.TotalSeconds > esperaOciosoS)
            {
                rel.Linha($"ABORTADO: houve input do usuário nos últimos {ociosoS} s durante {esperaOciosoS} s de espera. Nada mais foi injetado.");
                return null;
            }
            Thread.Sleep(500);
        }
    }

    private static string? LocalizarSpikes()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "BuzzySpike")) && File.Exists(Path.Combine(dir.FullName, "global.json")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static void DescreverAmbiente(Relatorio rel)
    {
        rel.Linha($"SO: {RuntimeInformation.OSDescription}; runtime {RuntimeInformation.FrameworkDescription}");
        var vistos = new HashSet<string>();
        foreach (var p in new[] { new Nativo.POINT(0, 0), new Nativo.POINT(-50, 500), new Nativo.POINT(1970, 500), new Nativo.POINT(500, -50), new Nativo.POINT(500, 1130) })
        {
            if (Nativo.Monitor(p) is { } m && vistos.Add(m.szDevice))
            {
                bool primario = (m.dwFlags & Nativo.MONITORINFOF_PRIMARY) != 0;
                rel.Linha($"Monitor {m.szDevice}{(primario ? " [principal]" : "")}: tela {m.rcMonitor}, área útil {m.rcWork}");
            }
        }
        rel.Linha($"Limiar de arraste: {Nativo.GetSystemMetrics(Nativo.SM_CXDRAG)} x {Nativo.GetSystemMetrics(Nativo.SM_CYDRAG)} px; "
            + $"clique duplo: {Nativo.GetDoubleClickTime()} ms, {Nativo.GetSystemMetrics(Nativo.SM_CXDOUBLECLK)} x {Nativo.GetSystemMetrics(Nativo.SM_CYDOUBLECLK)} px");
        rel.Linha($"ClickLock original: {(Nativo.ClickLock() ? "ligado" : "desligado")}; tempo da trava: {Nativo.TempoClickLockMs()} ms");
        rel.Linha($"Cursor original: {Nativo.Cursor()}");
    }
}
