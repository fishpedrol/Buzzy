using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Buzzy.App.Composicao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A agenda da releitura da topologia (Fase 5, passo P9; DEC-030; crítica, C11), sem janela: um relógio virtual com
/// disparos únicos no lugar do DispatcherTimer. Cada mensagem pede a releitura, que sai em
/// <c>max(nãoAntesDe, min(última + 300 ms, primeira + 1 s))</c>; a leitura incoerente tenta de novo em 500 ms, 1 s e 2 s;
/// a releitura publicada arma a conferência tardia, 1,5 s depois, uma vez por rajada; nada é periódico, e parar cancela
/// tudo. O agendador de verdade (<see cref="DisparoUnico.NoDispatcher"/>) tem um teste à parte, com o laço de mensagens.
/// </summary>
internal sealed class AgendaDaReleituraTestes
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    /// <summary>
    /// Um relógio virtual com disparos únicos: cada agendamento vence em agora + espera; <see cref="AvancarAte"/> dispara os
    /// vencidos na ordem dos vencimentos (e, no empate, na ordem em que foram agendados), com o relógio no instante de cada
    /// um, como o DispatcherTimer na thread da interface.
    /// </summary>
    private sealed class RelogioFalso
    {
        private sealed class Agendamento(TimeSpan vence, Action acao, int ordem)
        {
            internal TimeSpan Vence { get; } = vence;
            internal Action Acao { get; } = acao;
            internal int Ordem { get; } = ordem;
            internal bool Encerrado { get; set; }
        }

        private readonly List<Agendamento> _todos = [];

        internal TimeSpan Agora { get; private set; }

        /// <summary>Quantos disparos foram agendados até agora (inclusive os cancelados e os já disparados).</summary>
        internal int Agendados => _todos.Count;

        /// <summary>Quantos disparos estão pendentes (nem disparados nem cancelados).</summary>
        internal int Pendentes => _todos.Count(a => !a.Encerrado);

        internal Action Agendar(TimeSpan espera, Action acao)
        {
            Afirmar.Verdadeiro(espera >= TimeSpan.Zero, $"espera não negativa ({espera.TotalMilliseconds} ms)");
            var a = new Agendamento(Agora + espera, acao, _todos.Count);
            _todos.Add(a);
            return () => a.Encerrado = true;
        }

        internal void AvancarAte(TimeSpan instante)
        {
            while (_todos.Where(a => !a.Encerrado && a.Vence <= instante).OrderBy(a => a.Vence).ThenBy(a => a.Ordem).FirstOrDefault() is { } proximo)
            {
                if (proximo.Vence > Agora) Agora = proximo.Vence;
                proximo.Encerrado = true;
                proximo.Acao();
            }
            if (instante > Agora) Agora = instante;
        }

        internal void AvancarAte(double ms) => AvancarAte(Ms(ms));
    }

    /// <summary>Uma agenda com o relógio virtual: guarda cada releitura (instante e pedido) e cada conferência tardia.</summary>
    private sealed class Cenario
    {
        internal Cenario()
        {
            Agenda = new AgendaDaReleitura(() => Relogio.Agora, Relogio.Agendar, p =>
            {
                Releituras.Add((Relogio.Agora, p));
                DuranteAReleitura?.Invoke();
                return Coerente;
            }, () => Reafirmacoes.Add(Relogio.Agora));
        }

        internal RelogioFalso Relogio { get; } = new();

        internal AgendaDaReleitura Agenda { get; }

        internal List<(TimeSpan Quando, PedidoDeReleitura Pedido)> Releituras { get; } = [];

        internal List<TimeSpan> Reafirmacoes { get; } = [];

        /// <summary>Se a leitura vem coerente (publicada) ou incoerente.</summary>
        internal bool Coerente { get; set; } = true;

        /// <summary>O que acontece dentro da releitura (uma mensagem que chega no meio dela).</summary>
        internal Action? DuranteAReleitura { get; set; }

        /// <summary>Uma mensagem no instante <paramref name="ms"/>, com o relógio levado até lá.</summary>
        internal void Mensagem(double ms, string motivo = "WM_DISPLAYCHANGE", double naoAntesDeMs = 0)
        {
            Relogio.AvancarAte(ms);
            Agenda.Agendar(motivo, Ms(naoAntesDeMs));
        }

        /// <summary>O WM_DPICHANGED da própria janela do personagem no instante <paramref name="ms"/> (passo P14).</summary>
        internal void DaPropriaJanela(double ms)
        {
            Relogio.AvancarAte(ms);
            Agenda.Agendar("WM_DPICHANGED 144", daPropriaJanela: true);
        }

        internal double[] Instantes => [.. Releituras.Select(r => r.Quando.TotalMilliseconds)];
    }

    // ------------------------------------------------------------------ agrupamento

    [Teste]
    public void UmaMensagem_UmaReleitura300msDepois_EAConferenciaTardia1500msDepoisDela_UmaVezSo()
    {
        var c = new Cenario();
        Afirmar.Igual((0, false, false), (c.Relogio.Pendentes, c.Agenda.ReleituraPendente, c.Agenda.ReafirmacaoPendente), "em repouso, nada agendado");

        c.Mensagem(0);
        c.Relogio.AvancarAte(299);
        Afirmar.Igual(0, c.Releituras.Count, "nada antes do agrupamento");
        c.Relogio.AvancarAte(300);
        Afirmar.Sequencia([300.0], c.Instantes, "uma releitura, 300 ms depois da mensagem");
        Afirmar.Igual(new PedidoDeReleitura("WM_DISPLAYCHANGE", true), c.Releituras[0].Pedido, "o motivo e a nova tentativa, se falhar");
        Afirmar.Igual((false, true), (c.Agenda.ReleituraPendente, c.Agenda.ReafirmacaoPendente), "publicada: a conferência tardia agendada");

        c.Relogio.AvancarAte(1799);
        Afirmar.Igual(0, c.Reafirmacoes.Count, "nada antes de 1,5 s");
        c.Relogio.AvancarAte(1800);
        Afirmar.Sequencia([Ms(1800)], c.Reafirmacoes, "a conferência tardia, 1,5 s depois da releitura publicada");

        // Disparos únicos: uma hora depois, nada mais, e nada pendente (repouso sem timer).
        c.Relogio.AvancarAte(3_600_000);
        Afirmar.Igual((1, 1, 0), (c.Releituras.Count, c.Reafirmacoes.Count, c.Relogio.Pendentes), "uma releitura, uma conferência e nada pendente");
        Afirmar.Igual(2, c.Relogio.Agendados, "dois disparos agendados ao todo: a releitura e a conferência");
    }

    [Teste]
    public void MensagensSeguidas_UmaReleitura300msDepoisDaUltima_ComOsMotivosNaOrdem()
    {
        var c = new Cenario();
        c.Mensagem(0, "WM_DISPLAYCHANGE");
        c.Mensagem(100, "SPI_SETWORKAREA");
        c.Mensagem(250, "WM_DPICHANGED 144");
        c.Relogio.AvancarAte(549);
        Afirmar.Igual(0, c.Releituras.Count, "a rajada continua enquanto as mensagens chegam a menos de 300 ms");
        c.Relogio.AvancarAte(5000);
        Afirmar.Sequencia([550.0], c.Instantes, "uma releitura, 300 ms depois da última");
        Afirmar.Igual("WM_DISPLAYCHANGE,SPI_SETWORKAREA,WM_DPICHANGED 144", c.Releituras[0].Pedido.Motivos, "as três mensagens, na ordem");
        Afirmar.Sequencia([Ms(2050)], c.Reafirmacoes, "uma conferência tardia");
    }

    [Teste]
    public void RajadaSemFim_ReleNoTetoDe1sDesdeAPrimeira_EAProximaMensagemComecaOutraRajada()
    {
        // Mensagens a cada 150 ms, de 0 a 2100 ms: sem o teto, só haveria uma releitura, 300 ms depois da última.
        var c = new Cenario();
        for (int t = 0; t <= 2100; t += 150) c.Mensagem(t);
        c.Relogio.AvancarAte(10_000);

        // A primeira rajada (0 a 900 ms) relê no teto, em 1000 ms; a mensagem de 1050 ms começa outra, que relê em 2050 ms;
        // a de 2100 ms começa a terceira, que acaba no agrupamento, 300 ms depois.
        Afirmar.Sequencia([1000.0, 2050.0, 2400.0], c.Instantes, "no teto, no teto, e 300 ms depois da última");
        Afirmar.Sequencia([7, 7, 1], c.Releituras.Select(r => r.Pedido.Motivos.Split(',').Length), "cada mensagem numa releitura só");
        Afirmar.Sequencia([Ms(3900)], c.Reafirmacoes, "uma conferência tardia, depois da última releitura publicada");
    }

    [Teste]
    public void Teto_NaoReagendaQuandoOPrazoNaoMuda()
    {
        // Perto do fim da rajada, o teto decide o prazo (1000 ms desde a mensagem de 700 ms), e uma mensagem nova não cria
        // outro disparo.
        var c = new Cenario();
        c.Mensagem(0);
        c.Mensagem(200);
        c.Mensagem(450);
        c.Mensagem(700);
        int antes = c.Relogio.Agendados;
        c.Mensagem(800);
        c.Mensagem(900);
        Afirmar.Igual(antes, c.Relogio.Agendados, "o prazo continua no teto: nenhum disparo novo");
        c.Relogio.AvancarAte(5000);
        Afirmar.Sequencia([1000.0], c.Instantes);
    }

    [Teste]
    public void NaoAntesDe_PrevaleceSobreOAgrupamentoESobreOTeto_EEhEstendidoPeloMaior()
    {
        // A retomada (passo P10) pede a releitura não antes de 1,5 s: as mensagens seguintes não a adiantam.
        var c = new Cenario();
        c.Mensagem(0, "retomada", naoAntesDeMs: 1500);
        c.Mensagem(400, "WM_DISPLAYCHANGE");
        c.Relogio.AvancarAte(1499);
        Afirmar.Igual(0, c.Releituras.Count, "nem o agrupamento (700 ms) nem o teto (1000 ms) adiantam a releitura");
        c.Relogio.AvancarAte(1500);
        Afirmar.Sequencia([1500.0], c.Instantes, "no \"não antes de\"");
        Afirmar.Igual("retomada,WM_DISPLAYCHANGE", c.Releituras[0].Pedido.Motivos);

        // Um "não antes de" novo estende o anterior; um menor não o encurta.
        var d = new Cenario();
        d.Mensagem(0, "retomada", naoAntesDeMs: 1500);
        d.Mensagem(1000, "retomada repetida", naoAntesDeMs: 1500);
        d.Mensagem(1100, "outra", naoAntesDeMs: 100);
        d.Relogio.AvancarAte(10_000);
        Afirmar.Sequencia([2500.0], d.Instantes, "o maior dos dois, 1000 + 1500 ms");

        // Já vencido, ele não atrasa a rajada seguinte.
        d.Mensagem(20_000);
        d.Relogio.AvancarAte(30_000);
        Afirmar.Sequencia([2500.0, 20_300.0], d.Instantes, "a rajada seguinte, no agrupamento de sempre");
    }

    // ------------------------------------------------------------------ leitura incoerente

    [Teste]
    public void LeituraIncoerente_TentaDeNovoEm500ms1sE2s_DepoisDesisteAteAProximaMensagem()
    {
        var c = new Cenario { Coerente = false };
        c.Mensagem(0, "WM_DISPLAYCHANGE");
        c.Relogio.AvancarAte(60_000);
        Afirmar.Sequencia([300.0, 800.0, 1800.0, 3800.0], c.Instantes, "a releitura e três novas tentativas, em 500 ms, 1 s e 2 s");
        Afirmar.Sequencia([true, true, true, false], c.Releituras.Select(r => r.Pedido.NovaTentativaSeFalhar), "a última avisa que desiste");
        Afirmar.Verdadeiro(c.Releituras.All(r => r.Pedido.Motivos == "WM_DISPLAYCHANGE"), "o motivo acompanha as novas tentativas");
        Afirmar.Igual((0, 0), (c.Reafirmacoes.Count, c.Relogio.Pendentes), "nada publicado: sem conferência tardia, e nada pendente");

        // A próxima mensagem recomeça, com as tentativas do zero; o motivo de antes, que desistiu, fica para trás.
        c.Coerente = true;
        c.Mensagem(100_000, "SPI_SETWORKAREA");
        c.Relogio.AvancarAte(200_000);
        Afirmar.Igual(5, c.Releituras.Count);
        Afirmar.Igual((Ms(100_300), new PedidoDeReleitura("SPI_SETWORKAREA", true)), c.Releituras[4], "uma rajada nova, com as tentativas recomeçadas");
        Afirmar.Sequencia([Ms(101_800)], c.Reafirmacoes);
    }

    [Teste]
    public void MensagemDuranteANovaTentativa_ComecaOutraRajada_ComOsMotivosPendentesEAsTentativasDoZero()
    {
        var c = new Cenario { Coerente = false };
        c.Mensagem(0, "WM_DISPLAYCHANGE");
        c.Relogio.AvancarAte(300); // incoerente: nova tentativa em 800 ms
        c.Relogio.AvancarAte(800); // de novo: nova tentativa em 1800 ms
        c.Mensagem(1000, "SPI_SETWORKAREA");
        c.Relogio.AvancarAte(1299);
        Afirmar.Sequencia([300.0, 800.0], c.Instantes, "nada antes do agrupamento da mensagem nova");
        c.Relogio.AvancarAte(1300);
        Afirmar.Sequencia([300.0, 800.0, 1300.0], c.Instantes, "a mensagem nova substitui a nova tentativa: 300 ms depois dela");
        Afirmar.Igual(new PedidoDeReleitura("WM_DISPLAYCHANGE,SPI_SETWORKAREA", true), c.Releituras[2].Pedido, "os dois motivos, e as tentativas do zero");
        c.Relogio.AvancarAte(1799);
        Afirmar.Igual(3, c.Releituras.Count, "a nova tentativa antiga, de 1800 ms, foi cancelada");
        c.Relogio.AvancarAte(1800);
        Afirmar.Sequencia([300.0, 800.0, 1300.0, 1800.0], c.Instantes, "a primeira nova tentativa da rajada nova, 500 ms depois");
    }

    [Teste]
    public void MensagemNoMeioDaLeituraIncoerente_ARajadaNovaVale_SemTrocarPelaNovaTentativa()
    {
        var c = new Cenario { Coerente = false };
        bool primeira = true;
        c.DuranteAReleitura = () =>
        {
            if (!primeira) return;
            primeira = false;
            c.Agenda.Agendar("SPI_SETWORKAREA");
        };
        c.Mensagem(0, "WM_DISPLAYCHANGE");
        c.Relogio.AvancarAte(600);
        Afirmar.Sequencia([300.0, 600.0], c.Instantes, "a rajada da mensagem que chegou no meio da leitura, 300 ms depois dela, e não a nova tentativa de 500 ms");
        Afirmar.Igual(new PedidoDeReleitura("WM_DISPLAYCHANGE,SPI_SETWORKAREA", true), c.Releituras[1].Pedido, "os dois motivos, na ordem, e as tentativas do zero");
    }

    [Teste]
    public void Motivos_NoMaximo32PorReleitura_ORestoSoContado_MesmoComALeituraIncoerenteParaSempre()
    {
        // Revisão de segurança do bloco P6-P9, achado 4: com a leitura incoerente persistente e mensagens chegando sem parar,
        // os motivos pendentes cresciam sem teto, na memória e na linha TOPOLOGIA do log. Ficam os 32 primeiros, na ordem; o
        // resto vira uma contagem no fim (",+k").
        var c = new Cenario();
        for (int i = 0; i < 40; i++) c.Mensagem(i * 10, $"M{i}");
        c.Relogio.AvancarAte(5000);
        Afirmar.Igual(1, c.Releituras.Count, "uma releitura");
        Afirmar.Sequencia([.. Enumerable.Range(0, AgendaDaReleitura.MaximoDeMotivos).Select(i => $"M{i}"), "+8"], c.Releituras[0].Pedido.Motivos.Split(','),
            "os 32 primeiros, na ordem, e a contagem dos outros 8");

        // Incoerente para sempre, com uma mensagem a cada 400 ms durante um minuto: cada leitura leva os pendentes de volta,
        // e o texto nunca passa de 32 motivos e a contagem. Depois da última mensagem, as três novas tentativas e a desistência
        // levam todos: 32 e +118.
        var d = new Cenario { Coerente = false };
        for (int t = 0; t < 60_000; t += 400) d.Mensagem(t, "WM_SETTINGCHANGE");
        d.Relogio.AvancarAte(120_000);
        int maior = d.Releituras.Max(r => r.Pedido.Motivos.Split(',').Length);
        Afirmar.Verdadeiro(maior == AgendaDaReleitura.MaximoDeMotivos + 1, $"no máximo 32 motivos e a contagem: {maior} partes");
        Afirmar.Igual(new PedidoDeReleitura(string.Join(",", Enumerable.Repeat("WM_SETTINGCHANGE", 32)) + ",+118", false), d.Releituras[^1].Pedido, "a última, que desiste");
        Afirmar.Igual(0, d.Relogio.Pendentes, "desistiu: nada pendente");

        // Depois de desistir, a próxima mensagem começa do zero, sem a contagem de antes.
        d.Coerente = true;
        d.Mensagem(200_000, "TaskbarCreated");
        d.Relogio.AvancarAte(201_000);
        Afirmar.Igual("TaskbarCreated", d.Releituras[^1].Pedido.Motivos, "do zero");

        // Com a lista cheia, mensagens que chegam no meio da própria leitura incoerente: os pendentes voltam na frente, e o
        // que passa do teto vira contagem (32 e +5, depois +6 com mais uma).
        var e = new Cenario { Coerente = false };
        bool primeira = true;
        e.DuranteAReleitura = () =>
        {
            if (!primeira) return;
            primeira = false;
            for (int i = 0; i < 5; i++) e.Agenda.Agendar("WM_DPICHANGED 144");
        };
        for (int i = 0; i < AgendaDaReleitura.MaximoDeMotivos; i++) e.Mensagem(i, "WM_DISPLAYCHANGE");
        e.Relogio.AvancarAte(400);
        e.Mensagem(450, "SPI_SETWORKAREA");
        e.Relogio.AvancarAte(10_000);
        Afirmar.Igual(string.Join(",", Enumerable.Repeat("WM_DISPLAYCHANGE", 32)) + ",+6", e.Releituras[1].Pedido.Motivos,
            "os 32 primeiros na frente, e os 6 que chegaram depois só contados");
    }

    // ------------------------------------------------------------------ conferência tardia

    [Teste]
    public void DuasReleiturasPublicadas_AConferenciaTardiaSaiUmaVez_1500msDepoisDaUltima()
    {
        var c = new Cenario();
        c.Mensagem(0);
        c.Relogio.AvancarAte(300);
        c.Mensagem(1000);
        c.Relogio.AvancarAte(1300);
        c.Relogio.AvancarAte(1800);
        Afirmar.Igual(0, c.Reafirmacoes.Count, "a segunda releitura adiou a conferência");
        c.Relogio.AvancarAte(60_000);
        Afirmar.Sequencia([300.0, 1300.0], c.Instantes);
        Afirmar.Sequencia([Ms(2800)], c.Reafirmacoes, "uma conferência, 1,5 s depois da última releitura");
    }

    [Teste]
    public void MensagemDuranteAPropriaReleitura_ComecaOutraRajada_SemRepetirOsMotivosConsumidos()
    {
        // A releitura publicada move a janela para um monitor de outro DPI, e o WM_DPICHANGED dela chega no meio da própria
        // releitura (no mesmo tratamento).
        var c = new Cenario();
        bool primeira = true;
        c.DuranteAReleitura = () =>
        {
            if (!primeira) return;
            primeira = false;
            c.Agenda.Agendar("WM_DPICHANGED 144");
        };
        c.Mensagem(0, "WM_DISPLAYCHANGE");
        c.Relogio.AvancarAte(10_000);
        Afirmar.Sequencia([300.0, 600.0], c.Instantes, "outra rajada, 300 ms depois da mensagem que chegou na releitura");
        Afirmar.Sequencia(["WM_DISPLAYCHANGE", "WM_DPICHANGED 144"], c.Releituras.Select(r => r.Pedido.Motivos), "cada motivo numa releitura");
        Afirmar.Sequencia([Ms(2100)], c.Reafirmacoes, "uma conferência tardia, depois da segunda");
    }

    // ------------------------------------------------------------------ encerramento

    // ------------------------------------------------------------------ o WM_DPICHANGED da própria janela (passo P14)

    // A releitura reafirma o lugar da janela, e a janela montada entre monitores de DPI diferente pode mandar outro
    // WM_DPICHANGED: três releituras seguidas pedidas só por ele valem; a quarta é ignorada. Outra mensagem zera a conta, e a
    // própria janela volta a valer.
    [Teste]
    public void PropriaJanela_TresRodadasSeguidas_AQuartaEhIgnorada_OutraMensagemZeraAConta()
    {
        var c = new Cenario();
        c.DaPropriaJanela(0);
        c.Relogio.AvancarAte(400);
        c.DaPropriaJanela(400);
        c.Relogio.AvancarAte(800);
        c.DaPropriaJanela(800);
        c.Relogio.AvancarAte(1200);
        Afirmar.Sequencia([300.0, 700.0, 1100.0], c.Instantes, "três rodadas seguidas");
        Afirmar.Verdadeiro(c.Agenda.IgnoraAPropriaJanela, "no limite");
        c.DaPropriaJanela(1200);
        Afirmar.Falso(c.Agenda.ReleituraPendente, "a quarta é ignorada");
        c.Relogio.AvancarAte(1600);
        Afirmar.Igual(3, c.Releituras.Count, "nenhuma releitura a mais");

        c.Mensagem(1700);
        Afirmar.Falso(c.Agenda.IgnoraAPropriaJanela, "outra mensagem zera a conta");
        c.Relogio.AvancarAte(2000);
        c.DaPropriaJanela(2100);
        c.Relogio.AvancarAte(2400);
        Afirmar.Sequencia([300.0, 700.0, 1100.0, 2000.0, 2400.0], c.Instantes, "a da outra mensagem e, depois, a da própria janela");
    }

    // A conta é de rodadas seguidas: 5 s sem nenhuma delas a zera, e a travessia seguinte, minutos depois, vale.
    [Teste]
    public void PropriaJanela_DepoisDoIntervaloSemRodadas_VoltaAValer()
    {
        var c = new Cenario();
        foreach (double ms in new[] { 0.0, 400, 800 })
        {
            c.DaPropriaJanela(ms);
            c.Relogio.AvancarAte(ms + 300);
        }
        Afirmar.Verdadeiro(c.Agenda.IgnoraAPropriaJanela, "no limite logo depois");
        c.Relogio.AvancarAte(1100 + 4999);
        Afirmar.Verdadeiro(c.Agenda.IgnoraAPropriaJanela, "ainda no limite antes de 5 s");
        c.Relogio.AvancarAte(1100 + 5000);
        Afirmar.Falso(c.Agenda.IgnoraAPropriaJanela, "5 s depois da última, volta a valer");
        c.DaPropriaJanela(6100);
        c.Relogio.AvancarAte(6400);
        Afirmar.Igual(4, c.Releituras.Count, "a rodada nova sai");
        Afirmar.Falso(c.Agenda.IgnoraAPropriaJanela, "e conta de novo do um");
    }

    // Uma rajada com a própria janela e outra mensagem não conta como rodada só dela; uma leitura incoerente também não.
    [Teste]
    public void PropriaJanela_RajadaMistaOuLeituraIncoerente_NaoContam()
    {
        var c = new Cenario();
        foreach (double ms in new[] { 0.0, 1000, 2000 })
        {
            c.DaPropriaJanela(ms);
            c.Mensagem(ms + 10);
            c.Relogio.AvancarAte(ms + 400);
        }
        Afirmar.Falso(c.Agenda.IgnoraAPropriaJanela, "rajadas mistas não contam");

        // Uma rajada mista e, logo depois, duas só da própria janela: são duas rodadas seguidas, não três.
        var depois = new Cenario();
        depois.DaPropriaJanela(0);
        depois.Relogio.AvancarAte(5);
        depois.Agenda.Agendar("WM_DISPLAYCHANGE");
        depois.Relogio.AvancarAte(400);
        depois.DaPropriaJanela(400);
        depois.Relogio.AvancarAte(800);
        depois.DaPropriaJanela(800);
        depois.Relogio.AvancarAte(1200);
        Afirmar.Igual(3, depois.Releituras.Count, "três releituras");
        Afirmar.Falso(depois.Agenda.IgnoraAPropriaJanela, "só duas contam");

        // Cada leitura incoerente e as três novas tentativas dela: nenhuma conta, nem logo depois da última.
        var incoerente = new Cenario { Coerente = false };
        for (int i = 0; i < 4; i++)
        {
            incoerente.DaPropriaJanela(i * 10_000);
            incoerente.Relogio.AvancarAte(i * 10_000 + 3_900);
            Afirmar.Igual(4 * (i + 1), incoerente.Releituras.Count, $"rajada {i}: a leitura e as três novas tentativas");
            Afirmar.Falso(incoerente.Agenda.IgnoraAPropriaJanela, $"rajada {i}: leituras incoerentes não contam");
        }
    }

    [Teste]
    public void Raiz_ParaAsAgendasNoEncerramento_EAUltimaTentativaEhParcial_ConferidoNaFonte()
    {
        // O que não dá para provocar nos testes sem janela nem falha do hardware, conferido na fonte da raiz (Aplicacao.cs;
        // revisão do bloco P6-P9): o encerramento descarrega a gravação e para as duas agendas antes de fechar as janelas
        // (nenhum disparo depois dele); a última tentativa de uma rajada lê a topologia parcial; e a partida só lê a parcial
        // depois das cinco leituras de sempre.
        string fonte = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Composicao", "Aplicacao.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string encerrar = Trecho(fonte, "private void EncerrarAplicacao(");
        int descarregar = encerrar.IndexOf("_gravacao?.Descarregar(\"encerrar\");", StringComparison.Ordinal);
        int pararGravacao = encerrar.IndexOf("_gravacao?.Parar();", StringComparison.Ordinal);
        int pararReleitura = encerrar.IndexOf("_releitura.Parar();", StringComparison.Ordinal);
        int fechar = encerrar.IndexOf("_personagem?.Close();", StringComparison.Ordinal);
        Afirmar.Verdadeiro(descarregar >= 0 && descarregar < pararGravacao && pararGravacao < fechar, "descarrega e para a gravação antes de fechar");
        Afirmar.Verdadeiro(pararReleitura >= 0 && pararReleitura < fechar, "para a releitura antes de fechar");
        Afirmar.Contem("LeitorDeTopologia.LerDetalhado(out string? erro, parcial: !pedido.NovaTentativaSeFalhar)", Trecho(fonte, "private bool RelerAgrupada("));
        string partida = Trecho(fonte, "private static LeituraDaTopologia? LerTopologiaNaPartida(");
        int estritas = partida.IndexOf("LeitorDeTopologia.LerDetalhado(out erro);", StringComparison.Ordinal);
        int parcial = partida.IndexOf("LeitorDeTopologia.LerDetalhado(out string? erroDaParcial, parcial: true)", StringComparison.Ordinal);
        Afirmar.Verdadeiro(estritas >= 0 && parcial > estritas, "a partida: as leituras de sempre, depois a parcial");

        // Passo P14: o WM_DPICHANGED da própria janela, no limite da agenda, é ignorado antes de avisar o árbitro dos eventos do
        // sistema, que seguraria um desbloqueio ou uma retomada à espera de uma releitura que não vem.
        string mensagem = Trecho(fonte, "private void AoPossivelMudancaDeTopologia(");
        int ignora = mensagem.IndexOf("_releitura.IgnoraAPropriaJanela", StringComparison.Ordinal);
        int arbitro = mensagem.IndexOf("_eventosDoSistema.SinalizarMudancaDeTopologia()", StringComparison.Ordinal);
        Afirmar.Verdadeiro(ignora >= 0 && ignora < arbitro, "a consulta do limite antes do árbitro");
        Afirmar.Contem("AoPossivelMudancaDeTopologia($\"WM_DPICHANGED {dpi}\", daPropriaJanela: true)", fonte);
    }

    /// <summary>O corpo de um método da fonte, da assinatura até a chave que o fecha (quatro espaços de recuo).</summary>
    private static string Trecho(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Afirmar.Verdadeiro(inicio >= 0, $"achou {assinatura}");
        int fim = fonte.IndexOf("\n    }\n", inicio, StringComparison.Ordinal);
        return fonte[inicio..fim];
    }

    [Teste]
    public void Parar_CancelaAReleituraEAConferencia_ENadaMaisEAgendado()
    {
        var c = new Cenario();
        c.Mensagem(0);
        c.Relogio.AvancarAte(300);
        c.Mensagem(400);
        Afirmar.Igual((true, true), (c.Agenda.ReleituraPendente, c.Agenda.ReafirmacaoPendente), "uma releitura e a conferência pendentes");

        c.Agenda.Parar();
        Afirmar.Igual((false, false, 0), (c.Agenda.ReleituraPendente, c.Agenda.ReafirmacaoPendente, c.Relogio.Pendentes), "parada: nada pendente");
        c.Mensagem(500);
        c.Relogio.AvancarAte(60_000);
        Afirmar.Sequencia([300.0], c.Instantes, "nenhuma releitura depois de parar");
        Afirmar.Igual((0, 0), (c.Reafirmacoes.Count, c.Relogio.Pendentes), "nenhuma conferência, e nada agendado depois de parar");
    }

    [Teste]
    public void Parar_DentroDaReleitura_NaoAgendaAConferenciaNemNovaTentativa()
    {
        var publicada = new Cenario();
        publicada.DuranteAReleitura = publicada.Agenda.Parar;
        publicada.Mensagem(0);
        publicada.Relogio.AvancarAte(60_000);
        Afirmar.Igual((1, 0, 0), (publicada.Releituras.Count, publicada.Reafirmacoes.Count, publicada.Relogio.Pendentes), "parada no meio da releitura publicada: sem conferência");

        var incoerente = new Cenario { Coerente = false };
        incoerente.DuranteAReleitura = incoerente.Agenda.Parar;
        incoerente.Mensagem(0);
        incoerente.Relogio.AvancarAte(60_000);
        Afirmar.Igual((1, 0), (incoerente.Releituras.Count, incoerente.Relogio.Pendentes), "parada no meio da leitura incoerente: sem nova tentativa");
    }

    // ------------------------------------------------------------------ o agendador de verdade

    /// <summary>Roda o laço de mensagens desta thread por <paramref name="duracao"/>.</summary>
    private static void Bombear(TimeSpan duracao)
    {
        var quadro = new DispatcherFrame();
        var fim = new DispatcherTimer(DispatcherPriority.Normal) { Interval = duracao };
        fim.Tick += (_, _) =>
        {
            fim.Stop();
            quadro.Continue = false;
        };
        fim.Start();
        Dispatcher.PushFrame(quadro);
    }

    private static bool BombearAte(Func<bool> condicao, TimeSpan limite)
    {
        var relogio = Stopwatch.StartNew();
        while (!condicao())
        {
            if (relogio.Elapsed > limite) return false;
            Bombear(Ms(20));
        }
        return true;
    }

    // O agendador da raiz: um DispatcherTimer de disparo único, na prioridade normal (a do agrupador de antes), que dispara
    // uma vez só e para; cancelado, não dispara.
    [Teste]
    public void DisparoUnico_NaPrioridadeNormal_DisparaUmaVezSo_ECanceladoNaoDispara()
    {
        int disparos = 0;
        Action cancelar = DisparoUnico.NoDispatcher(Ms(30), () => disparos++, DispatcherPriority.Normal);
        Afirmar.Verdadeiro(BombearAte(() => disparos > 0, TimeSpan.FromSeconds(3)), "disparou");
        Bombear(Ms(250));
        Afirmar.Igual(1, disparos, "uma vez só: o temporizador para no próprio disparo");
        cancelar();

        int outros = 0;
        Action cancelarOutro = DisparoUnico.NoDispatcher(Ms(30), () => outros++, DispatcherPriority.Normal);
        cancelarOutro();
        Bombear(Ms(250));
        Afirmar.Igual(0, outros, "cancelado, não dispara");
    }
}
