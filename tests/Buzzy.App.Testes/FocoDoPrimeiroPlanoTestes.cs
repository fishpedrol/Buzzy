using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 7, passo F7-P6 (DEC-037, item 2): o adaptador do foco, sem janela. O monitor do primeiro plano é o da maior
/// interseção com as áreas úteis; a janela fora de toda área útil e a que cobre dois monitores não publicam; a do próprio
/// Buzzy não muda nada. A carência confirma o monitor depois de 2 s e publica só a mudança; um candidato novo recomeça a
/// contagem. O vão é o trecho horizontal recortado à área útil, em dezesseis avos arredondados para fora, só a pedido.
/// </summary>
internal sealed class FocoDoPrimeiroPlanoTestes
{
    private static readonly MonitorDoDesktop Direita = new("mon:direita", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true);
    private static readonly MonitorDoDesktop Esquerda = new("mon:esquerda", new RetanguloPx(-1920, 0, 0, 1080), new RetanguloPx(-1920, 0, 0, 1032), 96, false);
    private static readonly Topologia DoisMonitores = new([Direita, Esquerda]);

    private sealed class RelogioFalso
    {
        private readonly List<(TimeSpan Vence, Action Acao, bool[] Encerrado)> _todos = [];

        internal TimeSpan Agora { get; private set; }

        internal int Pendentes => _todos.Count(a => !a.Encerrado[0]);

        internal Action Agendar(TimeSpan espera, Action acao)
        {
            bool[] encerrado = [false];
            _todos.Add((Agora + espera, acao, encerrado));
            return () => encerrado[0] = true;
        }

        internal void Avancar(double ms)
        {
            TimeSpan ate = Agora + TimeSpan.FromMilliseconds(ms);
            while (_todos.Where(a => !a.Encerrado[0] && a.Vence <= ate).OrderBy(a => a.Vence).FirstOrDefault() is { Acao: not null } proximo)
            {
                Agora = proximo.Vence;
                proximo.Encerrado[0] = true;
                proximo.Acao();
            }
            Agora = ate;
        }
    }

    [Teste]
    public void MonitorDoFoco_MaiorIntersecao_ForaECobrindoDoisNaoPublicam()
    {
        Afirmar.Igual("mon:direita", AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(100, 100, 900, 700)), "dentro do da direita");
        Afirmar.Igual("mon:esquerda", AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(-1500, 100, 300, 700)), "mais na esquerda");
        Afirmar.Igual("mon:direita", AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(-300, 100, 1500, 700)), "mais na direita");
        Afirmar.Igual("mon:direita", AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(-500, 0, 500, 1080)), "empate: a menor chave");
        Afirmar.Igual("mon:direita", AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(0, 0, 1920, 1080)), "a tela inteira de um só: maximizada ou em tela cheia");
        Afirmar.Nulo(AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(-32000, -32000, -31840, -31972)), "minimizada");
        Afirmar.Nulo(AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(0, 1032, 1920, 1080)), "a barra de tarefas, fora da área útil");
        Afirmar.Nulo(AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(-1920, 0, 1920, 1080)), "a área de trabalho, cobrindo os dois");
        Afirmar.Nulo(AgendaDaTelaCheia.MonitorDoFoco(DoisMonitores, new RetanguloPx(10, 10, 10, 500)), "vazia");
    }

    [Teste]
    public void Vao_EmDezesseisAvos_ArredondadoParaFora_RecortadoAAreaUtil()
    {
        // 1920 / 16 = 120 px por parte.
        Afirmar.Igual(new VaoDaJanela(0, 16), AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(0, 0, 1920, 1032)), "maximizada");
        Afirmar.Igual(new VaoDaJanela(2, 8), AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(240, 50, 960, 600)), "nas divisas");
        Afirmar.Igual(new VaoDaJanela(1, 9), AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(239, 50, 961, 600)), "para fora");
        Afirmar.Igual(new VaoDaJanela(0, 3), AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(-700, 50, 300, 600)), "recortada à esquerda");
        Afirmar.Igual(new VaoDaJanela(15, 16), AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(1919, 50, 2500, 600)), "um pixel dentro");
        Afirmar.Igual(new VaoDaJanela(4, 6), AgendaDaTelaCheia.Vao(Esquerda, new RetanguloPx(-1440, 50, -1200, 600)), "no monitor de x negativo");
        Afirmar.Nulo(AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(-700, 50, 0, 600)), "fora do monitor");
        Afirmar.Nulo(AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(100, 1040, 600, 1080)), "só na barra de tarefas");
        foreach (int x in new[] { 0, 1, 119, 120, 1000, 1799, 1800, 1919 })
        {
            VaoDaJanela v = AgendaDaTelaCheia.Vao(Direita, new RetanguloPx(x, 10, x + 1, 20))!.Value;
            Afirmar.Verdadeiro(v.Valido && v.Fim - v.Inicio == 1 && v.Inicio * 120 <= x && x < v.Fim * 120, $"um pixel em {x}: {v}");
        }
    }

    [Teste]
    public void Agenda_CandidataOFocoNaAvaliacao_SemOBuzzy_ELeOVaoSoAPedido()
    {
        var relogio = new RelogioFalso();
        LeituraDoPrimeiroPlano leitura = new(new RetanguloPx(-1500, 100, -900, 700), EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);
        int leituras = 0;
        var candidatos = new List<string>();
        var agenda = new AgendaDaTelaCheia(() => { leituras++; return leitura; }, () => DoisMonitores, relogio.Agendar, _ => { }, candidatos.Add);
        agenda.AvaliarAgora("início");
        Afirmar.Sequencia(["mon:esquerda"], candidatos, "a avaliação candidata o foco");
        Afirmar.Igual(1, leituras, "uma leitura por avaliação");

        // O menu do Buzzy: mesmo com um retângulo na leitura, nada é candidatado (o contrato é da agenda, não do observador).
        leitura = new(new RetanguloPx(100, 100, 900, 700), null, DoBuzzy: true);
        agenda.Sinalizar("primeiro plano");
        relogio.Avancar(300);
        Afirmar.Sequencia(["mon:esquerda"], candidatos, "o menu do Buzzy não candidata nada");

        leitura = new(new RetanguloPx(-1920, 0, 1920, 1080), EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);
        agenda.Sinalizar("primeiro plano");
        relogio.Avancar(300);
        Afirmar.Sequencia(["mon:esquerda"], candidatos, "a área de trabalho não candidata nada");

        // O vão só a pedido: uma leitura, convertida na hora.
        leitura = new(new RetanguloPx(-1440, 50, -1200, 600), EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);
        int antes = leituras;
        Afirmar.Igual(new VaoDaJanela(4, 6), agenda.LerVao("mon:esquerda"), "o vão no monitor do foco");
        Afirmar.Nulo(agenda.LerVao("mon:direita"), "sem trecho no outro");
        Afirmar.Nulo(agenda.LerVao("mon:inexistente"), "sem o monitor");
        Afirmar.Igual((3, 3L), (leituras - antes, agenda.VaosPedidos), "uma leitura por pedido, contada");
        leitura = new(new RetanguloPx(-1440, 50, -1200, 600), null, DoBuzzy: true);
        Afirmar.Nulo(agenda.LerVao("mon:esquerda"), "com o menu do Buzzy, nenhum");
        agenda.Parar();
        Afirmar.Nulo(agenda.LerVao("mon:esquerda"), "parada, nenhum");
        Afirmar.Igual(4L, agenda.VaosPedidos, "parada, nem conta");

        // Sem a curiosidade (nenhum receptor), a avaliação é a de antes.
        var semFoco = new AgendaDaTelaCheia(() => leitura, () => DoisMonitores, relogio.Agendar, _ => { });
        semFoco.AvaliarAgora("início");
    }

    [Teste]
    public void Carencia_DoisSegundos_SoAMudanca_CandidatoNovoRecomeca()
    {
        var relogio = new RelogioFalso();
        var publicados = new List<(string Chave, double Ms)>();
        var carencia = new CarenciaDoFoco(relogio.Agendar, c => publicados.Add((c, relogio.Agora.TotalMilliseconds)));
        Afirmar.Igual(2.0, CarenciaDoFoco.Carencia.TotalSeconds, "a carência");

        carencia.Candidatar("a");
        relogio.Avancar(1999);
        Afirmar.Igual(0, publicados.Count, "antes dos 2 s, nada");
        carencia.Candidatar("a");
        relogio.Avancar(1);
        Afirmar.Sequencia([("a", 2000.0)], publicados, "o mesmo candidato não recomeça a contagem");
        Afirmar.Igual(("a", (string?)null, 0), (carencia.Publicado, carencia.Candidato, relogio.Pendentes), "publicado, sem pendente");

        carencia.Candidatar("a");
        Afirmar.Igual(0, relogio.Pendentes, "o publicado de novo não agenda nada");

        carencia.Candidatar("b");
        relogio.Avancar(1500);
        carencia.Candidatar("c");
        Afirmar.Igual(("c", 1), (carencia.Candidato, relogio.Pendentes), "o candidato novo cancela o disparo do anterior");
        relogio.Avancar(1500);
        Afirmar.Igual(1, publicados.Count, "o candidato novo recomeçou a contagem");
        relogio.Avancar(500);
        Afirmar.Sequencia([("a", 2000.0), ("c", 5500.0)], publicados, "c, 2 s depois de ele virar candidato (3,5 s)");

        carencia.Candidatar("a");
        relogio.Avancar(1000);
        carencia.Candidatar("c");
        relogio.Avancar(5000);
        Afirmar.Igual((2, (string?)null, 0), (publicados.Count, carencia.Candidato, relogio.Pendentes), "voltar ao publicado cancela a carência");

        carencia.Candidatar("b");
        carencia.Parar();
        Afirmar.Igual(0, relogio.Pendentes, "parar cancela a carência pendente na hora");
        relogio.Avancar(5000);
        carencia.Candidatar("a");
        relogio.Avancar(5000);
        Afirmar.Igual((2, 0), (publicados.Count, relogio.Pendentes), "parada: nada pendente, nada publicado");
    }
}
