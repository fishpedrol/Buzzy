using System.Diagnostics;
using System.Globalization;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Testes de integração da Fase 1 (TODO.md: "teste de fumaça que inicia o app, encontra a
/// janela, confere os estilos e encerra"). Iniciam o Buzzy.exe de verdade, então só rodam
/// com --integracao, depois de avisar o usuário. Nenhum injeta input: mensagens são
/// enviadas só às janelas do Buzzy aberto pelo próprio teste.
/// </summary>
[Integracao]
internal sealed class IntegracaoTestes
{
    [Teste]
    public void Fumaca_IniciaNoChaoDaAreaUtilDoPrincipalComOsEstilosCertosEEncerraLimpo()
    {
        // As coordenadas lidas aqui só batem com as do log do Buzzy (pixels físicos) se este
        // processo também estiver em Per-Monitor V2 (app.manifest dos testes). Conferido antes
        // de abrir o Buzzy e de comparar qualquer coordenada.
        Afirmar.Verdadeiro(NativoTeste.ThreadEmPerMonitorV2(), "a thread do teste está em Per-Monitor V2 (coordenadas físicas, como o log do Buzzy)");

        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();

        long ex = NativoTeste.EstiloEstendido(b.Janela);
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_LAYERED) != 0, "WS_EX_LAYERED: transparência por pixel do WPF");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_NOACTIVATE) != 0, "WS_EX_NOACTIVATE: não rouba foco");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_TOOLWINDOW) != 0, "WS_EX_TOOLWINDOW: fora da barra de tarefas e do Alt+Tab");
        Afirmar.Verdadeiro((ex & NativoTeste.WS_EX_TOPMOST) != 0, "WS_EX_TOPMOST: sempre no topo por padrão");
        Afirmar.Falso((ex & NativoTeste.WS_EX_APPWINDOW) != 0, "sem WS_EX_APPWINDOW");
        Afirmar.Falso((ex & NativoTeste.WS_EX_TRANSPARENT) != 0, "sem WS_EX_TRANSPARENT: modo fantasma fora do MVP (Q-21)");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "janela visível");

        EventoDoLog carregamento = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Settling", 5000, "núcleo carregado");
        Afirmar.Igual("Booting", carregamento["de"], "o núcleo parte de Booting");
        Afirmar.Igual("início", carregamento["motivo"], "a carga mantém o motivo de inicialização no log");
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling" && e["para"] == "Idle", 5000, "posição inicial acomodada pelo núcleo");

        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.LerDetalhado(out string? erro), erro);
        MonitorDoDesktop principal = leitura.Topologia.Principal;
        RetanguloPx janela = b.RetanguloDaJanela();
        TamanhoPx esperado = new TamanhoDip(128, 128).ParaPixels(principal.Dpi);
        Afirmar.Igual(esperado, janela.Tamanho, "tamanho da janela = tamanho do sprite no DPI do principal");
        Afirmar.Verdadeiro(principal.AreaUtil.Contem(janela), $"janela {janela} dentro da área útil {principal.AreaUtil} do principal");
        Afirmar.Igual(principal.AreaUtil.Base, janela.Base, "pés no chão da área útil");

        EventoDoLog posicao = b.Esperar(e => e.Chave == "POSICAO", 5000, "posição");
        Afirmar.Igual(janela, EventoDoLog.Retangulo(posicao["retangulo"]), "retângulo registrado = retângulo real");

        // Chave estável do monitor (DEC-030): o Buzzy e este processo calculam a mesma chave, cada um por si, e o log
        // leva a chave opaca com o nome GDI ao lado, nunca o caminho do dispositivo.
        Afirmar.Igual(principal.Chave, posicao["monitor"], "a chave do Buzzy é a calculada por este processo");
        Afirmar.Igual(leitura.NomeGdi(principal.Chave), posicao["gdi"], "o nome GDI do principal ao lado da chave");
        Afirmar.Diferente(posicao["gdi"], posicao["monitor"], "a chave não é mais o nome GDI");
        EventoDoLog inicio = b.Esperar(e => e.Chave == "TOPOLOGIA" && e["motivo"] == "início", 1000, "topologia inicial");
        Afirmar.Contem($"{principal.Chave}={leitura.NomeGdi(principal.Chave)}", inicio["chaves"], "as chaves da topologia inicial, com o nome GDI");
        if (leitura.ErroDaConsulta is null)
        {
            Afirmar.Verdadeiro(System.Text.RegularExpressions.Regex.IsMatch(posicao["monitor"], "^mon:[0-9a-f]{16}$"), $"chave estável e opaca: {posicao["monitor"]}");
            Afirmar.Igual(("ok", "0"), (inicio["consulta"], inicio["reserva"]), "a consulta da configuração de vídeo também deu certo no Buzzy");
        }

        EventoDoLog quadro = b.Esperar(e => e.Chave == "PRIMEIRO_QUADRO", 5000, "primeiro quadro (M6)");
        double ms = double.Parse(quadro["ms"], CultureInfo.InvariantCulture);
        Afirmar.Verdadeiro(ms > 0 && ms < 30000, $"M6 plausível: {ms} ms");
        Console.WriteLine($"         M6 tempo até o primeiro quadro: {ms} ms");

        EventoDoLog bandeja = b.Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 1000, "bandeja");
        Afirmar.Igual("True", bandeja["adicionado"], "ícone adicionado à bandeja");
        Afirmar.Igual("True", bandeja["versao4"], "notificações na versão 4");

        // Com --sem-tela-cheia (o padrão dos testes), o observador não liga: o lugar inicial não depende do que estiver em
        // tela cheia na máquina (DEC-034). O observador ligado tem o teste dele, logo abaixo.
        EventoDoLog observador = b.Esperar(e => e.Chave == "TELA_CHEIA" && e.Campos.ContainsKey("observador"), 1000, "observador de tela cheia");
        Afirmar.Igual(("desligado", "--sem-tela-cheia"), (observador["observador"], observador["motivo"]), "a opção da linha de comando");

        // Só contam processos criados depois do início do Buzzy: o pai registrado pelo Windows
        // pode ser um PID antigo reaproveitado.
        Afirmar.Igual(0, NativoTeste.Filhos(b.Processo.Id, b.Inicio).Count, "o Buzzy não cria processos filhos");

        int codigo = b.FecharPorWmClose();
        Afirmar.Igual(0, codigo, "código de saída");
        List<EventoDoLog> ev = b.Eventos();
        Afirmar.Verdadeiro(ev.Any(e => e.Chave == "BANDEJA" && e["removido"] == "True"), "ícone removido ao sair");
        Afirmar.Verdadeiro(ev.Any(e => e.Chave == "FIM" && e["codigo"] == "0"), "FIM registrado");
        Afirmar.Igual(0, NativoTeste.Filhos(b.Processo.Id, b.Inicio).Count, "nenhum processo filho do Buzzy sobra depois do encerramento");
    }

    // O modo de tela cheia como no uso real (DEC-034), sem --sem-tela-cheia: o observador assina a troca de primeiro plano
    // na partida e avalia na hora; os ganchos saem no encerramento, com as contagens de eventos (a medição do P7); e nenhuma
    // linha TELA_CHEIA leva a janela observada (identificador ou retângulo). O que estiver em primeiro plano na máquina
    // decide se algum monitor fica ocupado: o teste só confere que as chaves publicadas são da topologia e que o Buzzy, à
    // vista, não ficou num monitor ocupado.
    [Teste]
    public void TelaCheia_ObservadorLigaNaPartida_SaiNoEncerramento_ENadaDaJanelaVaiAoLog()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(telaCheia: true);
        EventoDoLog observador = b.Esperar(e => e.Chave == "TELA_CHEIA" && e.Campos.ContainsKey("observador"), 1000, "observador de tela cheia");
        Afirmar.Igual("ligado", observador["observador"], "a troca de primeiro plano assinada");
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        Thread.Sleep(500);

        LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.LerDetalhado(out string? erro), erro);
        string[] ocupados = [.. b.Eventos().Where(e => e.Chave == "TELA_CHEIA" && e.Campos.ContainsKey("ocupados")).Select(e => e["ocupados"]).LastOrDefault()?
            .Split(';', StringSplitOptions.RemoveEmptyEntries).Where(c => c != "-") ?? []];
        foreach (string chave in ocupados)
            Afirmar.NaoNulo(leitura.Topologia.PorChave(chave), $"o ocupado {chave} é um monitor da topologia");
        RetanguloPx janela = b.RetanguloDaJanela();
        MonitorDoDesktop? doBuzzy = leitura.Topologia.MonitorQueContem(new PontoPx(janela.Esquerda + janela.Largura / 2, janela.Base - 1));
        Afirmar.Falso(doBuzzy is not null && ocupados.Contains(doBuzzy.Chave) && ocupados.Length < leitura.Topologia.Monitores.Count,
            $"à vista num monitor ocupado ({doBuzzy?.Chave}), com outro livre");
        Console.WriteLine($"         tela cheia na partida: {(ocupados.Length == 0 ? "nenhum monitor ocupado" : string.Join(", ", ocupados))}");

        Afirmar.Igual(0, b.FecharPorWmClose());
        List<EventoDoLog> ev = b.Eventos();
        EventoDoLog fim = Afirmar.NaoNulo(ev.SingleOrDefault(e => e.Chave == "TELA_CHEIA" && e.Campos.ContainsKey("fim")), "os ganchos saem no encerramento");
        Afirmar.Verdadeiro(fim.Campos.ContainsKey("eventosPrimeiroPlano") && fim.Campos.ContainsKey("eventosGeometria"), "com as contagens de eventos");
        // DEC-037, item 11: no fim, também quantos vãos o núcleo pediu, e nada mais da curiosidade.
        Afirmar.Verdadeiro(fim.Campos.ContainsKey("vaosPedidos"), "com os vãos pedidos");
        string[] permitidos = ["observador", "motivo", "ocupados", "shell", "candidatos", "eventosPrimeiroPlano", "eventosGeometria", "vaosPedidos", "fim"];
        foreach (EventoDoLog linha in ev.Where(e => e.Chave == "TELA_CHEIA"))
            Afirmar.Verdadeiro(linha.Campos.Keys.All(permitidos.Contains), $"só os campos permitidos em TELA_CHEIA: {linha.Linha}");
    }

    [Teste]
    public void InstanciaUnica_SegundaAberturaRevelaOBuzzyEscondidoESai()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();

        // Minimizar sem ativar nada: o Buzzy deve se esconder (Q-03: minimizar esconde).
        b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
        b.Esperar(e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "escondido ao ser minimizado");
        Afirmar.Falso(NativoTeste.IsWindowVisible(b.Janela), "escondido depois de minimizado");

        // A segunda instância é encerrada pelo próprio teste se não sair no tempo (try/finally
        // dentro de AbrirSegundaInstancia), e o fato aparece na mensagem de falha.
        SegundaInstancia segunda = BuzzyEmTeste.AbrirSegundaInstancia();
        Afirmar.Verdadeiro(segunda.SaiuSozinha, $"a segunda instância sai sozinha ({segunda.Encerramento ?? "saiu"})");
        Afirmar.Igual<int?>(CodigosDeSaida.Normal, segunda.Codigo, $"código da segunda instância ({BuzzyEmTeste.DescreverCodigo(segunda.Codigo)})");

        b.Esperar(e => e.Chave == "INSTANCIA" && e["papel"] == "segunda" && e["pedidoEntregue"] == "True", 3000, "pedido da segunda instância");
        b.Esperar(e => e.Chave == "VISIVEL" && e["visivel"] == "sim" && e["motivo"] == "segunda instância", 3000, "reaparece pela segunda instância");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "visível de novo");
        Afirmar.Igual(1, Process.GetProcessesByName("Buzzy").Length, "continua uma instância só");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void Topologia_MensagensDeMudancaSaoAgrupadasNumaReleituraEOSpriteContinuaNaAreaUtil()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        RetanguloPx antes = b.RetanguloDaJanela();
        long marca = BuzzyEmTeste.MarcaDoLog();

        // As mesmas mensagens que o Windows envia ao trocar resolução e mover a barra de tarefas.
        // A topologia real não mudou, então a releitura precisa concluir "mudou=nao".
        // WM_DISPLAYCHANGE pode ser postada; WM_SETTINGCHANGE leva ponteiro e só vai por envio
        // síncrono, como o próprio Windows faz na difusão.
        Afirmar.Verdadeiro(b.Postar(b.Servico, NativoTeste.WM_DISPLAYCHANGE, 32, (nint)(1920 | (1080 << 16)), "WM_DISPLAYCHANGE"), "WM_DISPLAYCHANGE postada");
        nint enviada = b.Enviar(b.Servico, NativoTeste.WM_SETTINGCHANGE, NativoTeste.SPI_SETWORKAREA, 0, "WM_SETTINGCHANGE");
        Afirmar.Verdadeiro(enviada != 0, "WM_SETTINGCHANGE(SPI_SETWORKAREA) entregue");

        b.Esperar(e => e.Chave == "TOPOLOGIA" && e["motivo"].Contains("WM_DISPLAYCHANGE", StringComparison.Ordinal), 3000, "releitura da topologia");
        Thread.Sleep(700);
        List<EventoDoLog> releituras = [.. BuzzyEmTeste.EventosDesde(marca).Where(e => e.Chave == "TOPOLOGIA")];
        Afirmar.Igual(1, releituras.Count, "as duas mensagens viram uma releitura só");
        string[] motivos = releituras[0]["motivo"].Split(',');
        Afirmar.Verdadeiro(motivos.Contains("WM_DISPLAYCHANGE") && motivos.Contains("SPI_SETWORKAREA"), $"a releitura junta as duas mensagens (motivo={releituras[0]["motivo"]})");
        Afirmar.Igual("nao", releituras[0]["mudou"], "topologia real igual");

        RetanguloPx depois = b.RetanguloDaJanela();
        Afirmar.Igual(antes, depois, "sprite no mesmo lugar");
        Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
        Afirmar.Verdadeiro(t.Principal.AreaUtil.Contem(depois), "sprite dentro da área útil");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void Bandeja_IconeERecriadoQuandoABarraDeTarefasReinicia()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling" && e["para"] == "Idle", 5000, "posição inicial acomodada pelo núcleo");
        long marca = BuzzyEmTeste.MarcaDoLog();
        int taskbarCreated = NativoTeste.RegisterWindowMessage("TaskbarCreated");
        Afirmar.Verdadeiro(taskbarCreated != 0, "mensagem TaskbarCreated registrada");

        Afirmar.Verdadeiro(b.Postar(b.Servico, taskbarCreated, 0, 0, "TaskbarCreated"), "TaskbarCreated postada");

        // O ícone volta na hora. A topologia, que pode ter mudado com a barra (a área útil, o DPI do principal), vai ao núcleo
        // pela releitura agrupada, como as outras mensagens (Fase 5, passo P9; crítica, C12): antes, a raiz a relia sem
        // avisar o núcleo.
        EventoDoLog releitura = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && Motivos(e).Contains("TaskbarCreated"), 3000, "releitura da topologia pedida pela TaskbarCreated");
        EsperarDesde(b, marca, e => e.Chave == "BANDEJA" && e["adicionado"] == "True", 3000, "ícone adicionado de novo");
        List<EventoDoLog> depois = BuzzyEmTeste.EventosDesde(marca);
        int recriada = depois.FindIndex(e => e.Chave == "BANDEJA" && e["barraDeTarefasRecriada"] == "sim");
        int adicao = depois.FindIndex(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"));
        int mensagem = depois.FindIndex(e => e.Chave == "MENSAGEM" && e["tipo"] == "TaskbarCreated");
        int topologia = depois.FindIndex(e => e.Linha == releitura.Linha);
        Afirmar.Verdadeiro(recriada >= 0, "reagiu a TaskbarCreated");
        Afirmar.Verdadeiro(adicao > recriada, "o ícone volta na hora, antes da releitura");
        Afirmar.Verdadeiro(mensagem > adicao && topologia > mensagem, $"a mensagem crua no log (MENSAGEM|tipo=TaskbarCreated) e depois a releitura (índices {adicao}, {mensagem}, {topologia})");
        Afirmar.Igual("nao", releitura["mudou"], "topologia real igual");
        double ms = (releitura.Instante - depois[mensagem].Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(ms >= 250, $"a releitura espera o agrupamento de 300 ms: {ms} ms");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void Topologia_RajadaSemFim_ReleNoTetoDeUmSegundoSemEsperarOFimDela_ECadaMensagemVaiAoLog()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling" && e["para"] == "Idle", 5000, "posição inicial acomodada pelo núcleo");
        Thread.Sleep(1000); // uma releitura da partida (WM_DPICHANGED ao mostrar) já saiu
        long marca = BuzzyEmTeste.MarcaDoLog();

        // Uma rajada sem fim: WM_DISPLAYCHANGE a cada 100 ms, por 2,5 s, sempre a menos de 300 ms da anterior. Sem o teto
        // (crítica, C11), a releitura esperaria o fim da rajada; com ele, sai 1 s depois da primeira mensagem, e a mensagem
        // seguinte começa outra rajada. A topologia real não muda.
        const int Mensagens = 26;
        var relogio = Stopwatch.StartNew();
        for (int i = 0; i < Mensagens; i++)
        {
            while (relogio.ElapsedMilliseconds < i * 100L) Thread.Sleep(5);
            Afirmar.Verdadeiro(b.Postar(b.Servico, NativoTeste.WM_DISPLAYCHANGE, 32, (nint)(1920 | (1080 << 16)), "WM_DISPLAYCHANGE"), "WM_DISPLAYCHANGE postada");
        }
        // A última rajada relê 300 ms depois da última mensagem; depois, nada mais (nenhuma releitura sem mensagem).
        Thread.Sleep(2000);

        List<EventoDoLog> ev = BuzzyEmTeste.EventosDesde(marca);
        Afirmar.Verdadeiro(ev.Where(e => e.Chave == "MENSAGEM").All(e => e.Campos.Count == 1 && e.Campos.ContainsKey("tipo")), "MENSAGEM leva só o tipo da mensagem");
        List<EventoDoLog> mensagens = [.. ev.Where(e => e.Chave == "MENSAGEM" && e["tipo"] == "WM_DISPLAYCHANGE")];
        Afirmar.Igual(Mensagens, mensagens.Count, "cada mensagem crua vai ao log (MENSAGEM|tipo=WM_DISPLAYCHANGE)");
        List<EventoDoLog> releituras = [.. ev.Where(e => e.Chave == "TOPOLOGIA" && e.Campos.ContainsKey("mudou"))];
        string resumo = string.Join(" | ", releituras.Select(r => $"{(r.Instante - mensagens[0].Instante).TotalMilliseconds:0} ms: {Motivos(r).Length} mensagem(ns)"));
        Console.WriteLine($"         releituras desde a primeira mensagem (a última em {(mensagens[^1].Instante - mensagens[0].Instante).TotalMilliseconds:0} ms): {resumo}");
        Afirmar.Verdadeiro(releituras.Count >= 2, $"mais de uma releitura numa rajada de 2,5 s: {resumo}");
        Afirmar.Verdadeiro(releituras[0].Instante < mensagens[^1].Instante, $"a primeira releitura sai no meio da rajada, pelo teto, sem esperar a última mensagem: {resumo}");
        double primeira = (releituras[0].Instante - mensagens[0].Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(primeira >= 250 && primeira <= 1600, $"a primeira releitura, até o teto de 1 s (com folga para a fila): {primeira:0} ms");
        Afirmar.Igual(Mensagens, releituras.Sum(r => Motivos(r).Count(m => m == "WM_DISPLAYCHANGE")), $"cada mensagem numa releitura só: {resumo}");
        Afirmar.Verdadeiro(releituras.All(r => r["mudou"] == "nao"), "a topologia real não mudou");
        double ultima = (releituras[^1].Instante - mensagens[^1].Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(ultima >= 250 && ultima <= 1300, $"a última releitura, no agrupamento depois da última mensagem: {ultima:0} ms");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    /// <summary>Os motivos de uma linha TOPOLOGIA (as mensagens agrupadas, separadas por vírgula).</summary>
    private static string[] Motivos(EventoDoLog e) => e["motivo"].Split(',');

    /// <summary>O primeiro evento depois da <paramref name="marca"/> que satisfaz a condição.</summary>
    private static EventoDoLog EsperarDesde(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoDoLog? achado = BuzzyEmTeste.EventosDesde(marca).FirstOrDefault(condicao);
            if (achado is not null) return achado;
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(50);
        }
    }
}
