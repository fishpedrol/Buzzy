using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A agenda da tela cheia (DEC-013 e DEC-034; Q-09), sem janela: a leitura da janela em primeiro plano é falsa e os disparos
/// únicos rodam num relógio virtual. Ocupados são os monitores que a janela cobre inteiros, só com o shell confirmando a tela
/// cheia; a avaliação sai 250 ms depois do primeiro sinal de uma rajada, sem ser adiada; só uma mudança é publicada, uma
/// vez; a janela do próprio Buzzy não muda nada; nada é periódico, e parar cancela tudo. O observador de verdade, que
/// assina os eventos do Windows, roda na integração.
/// </summary>
internal sealed class AgendaDaTelaCheiaTestes
{
    // A máquina do usuário (2026-10-03): o principal à direita e o secundário à esquerda, em x negativo, com a barra embaixo.
    private static readonly MonitorDoDesktop Direita = new("mon:direita", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true);
    private static readonly MonitorDoDesktop Esquerda = new("mon:esquerda", new RetanguloPx(-1920, 0, 0, 1080), new RetanguloPx(-1920, 0, 0, 1032), 96, false);
    private static readonly Topologia DoisMonitores = new([Direita, Esquerda]);

    /// <summary>Um relógio virtual com disparos únicos, como o DispatcherTimer na thread da interface.</summary>
    private sealed class RelogioFalso
    {
        private readonly List<(TimeSpan Vence, Action Acao, bool[] Encerrado)> _todos = [];

        internal TimeSpan Agora { get; private set; }

        internal int Pendentes => _todos.Count(a => !a.Encerrado[0]);

        internal List<TimeSpan> Esperas { get; } = [];

        internal Action Agendar(TimeSpan espera, Action acao)
        {
            Esperas.Add(espera);
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

    private sealed class Cenario
    {
        internal Cenario()
        {
            Agenda = new AgendaDaTelaCheia(
                () =>
                {
                    Leituras++;
                    return Leitura;
                },
                () => Topologia,
                Relogio.Agendar,
                Publicadas.Add);
        }

        internal RelogioFalso Relogio { get; } = new();

        internal AgendaDaTelaCheia Agenda { get; }

        internal LeituraDoPrimeiroPlano Leitura { get; set; } = new(null, EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);

        internal Topologia? Topologia { get; set; } = DoisMonitores;

        internal int Leituras { get; private set; }

        internal List<MudancaDaTelaCheia> Publicadas { get; } = [];
    }

    private static LeituraDoPrimeiroPlano Jogo(RetanguloPx janela, EstadoDoShell shell = EstadoDoShell.Ocupado) => new(janela, shell, DoBuzzy: false);

    // ---------------------------------------------------------------- a função pura

    [Teste]
    public void Cobertos_SoOsMonitoresComATelaInteiraDentroDaJanela()
    {
        Afirmar.Sequencia<string>(["mon:direita"], AgendaDaTelaCheia.Cobertos(DoisMonitores, Direita.Tela), "a tela exata do monitor");
        Afirmar.Sequencia<string>(["mon:direita"], AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(-1, -1, 1921, 1081)), "um pouco maior que ela");
        Afirmar.Sequencia<string>(["mon:direita", "mon:esquerda"], AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(-1920, 0, 1920, 1080)), "as duas telas, em ordem de chave");
        // Uma janela maximizada passa um pouco da área útil, mas não cobre a barra: não é tela cheia.
        Afirmar.Igual(0, AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(-8, -8, 1928, 1040)).Count, "maximizada");
        Afirmar.Igual(0, AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(0, 0, 1919, 1080)).Count, "um pixel a menos");
        Afirmar.Igual(0, AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(100, 100, 100, 100)).Count, "vazia");
        Afirmar.Igual(0, AgendaDaTelaCheia.Cobertos(DoisMonitores, new RetanguloPx(-32000, -32000, -31840, -31972)).Count, "minimizada, fora da tela");
    }

    [Teste]
    public void ShellEmTelaCheia_SoAplicativoEmTelaCheiaDirect3dOuDaStore()
    {
        foreach (EstadoDoShell estado in Enum.GetValues<EstadoDoShell>())
        {
            bool esperado = estado is EstadoDoShell.Ocupado or EstadoDoShell.D3dEmTelaCheia or EstadoDoShell.App;
            Afirmar.Igual(esperado, AgendaDaTelaCheia.ShellEmTelaCheia(estado), estado.ToString());
        }
        Afirmar.Falso(AgendaDaTelaCheia.ShellEmTelaCheia(null), "a consulta falhou");
        Afirmar.Igual(7, Enum.GetValues<EstadoDoShell>().Length, "os sete valores de QUERY_USER_NOTIFICATION_STATE");
        Afirmar.Igual(2, (int)EstadoDoShell.Ocupado, "QUNS_BUSY");
        Afirmar.Igual(3, (int)EstadoDoShell.D3dEmTelaCheia, "QUNS_RUNNING_D3D_FULL_SCREEN");
        Afirmar.Igual(7, (int)EstadoDoShell.App, "QUNS_APP");
    }

    // ---------------------------------------------------------------- a agenda

    // O relato do usuário: o jogo em tela cheia no monitor da direita. A troca de primeiro plano e o redimensionamento que vem
    // com ela viram uma avaliação só, 250 ms depois do primeiro sinal, com os dois motivos; o monitor do jogo é publicado
    // uma vez, e a mesma leitura de novo não publica nada.
    [Teste]
    public void JogoEmTelaCheia_UmaAvaliacao250msDepoisDoPrimeiroSinal_PublicaOMonitorDeleUmaVez()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        c.Agenda.Sinalizar("primeiro plano");
        c.Relogio.Avancar(100);
        c.Agenda.Sinalizar("geometria");
        c.Relogio.Avancar(149);
        Afirmar.Igual(0, c.Leituras, "ainda não leu");
        c.Relogio.Avancar(1);
        Afirmar.Igual(1, c.Leituras, "uma leitura, 250 ms depois do primeiro sinal");
        Afirmar.Sequencia([TimeSpan.FromMilliseconds(250)], c.Relogio.Esperas, "um disparo só, sem adiar");
        MudancaDaTelaCheia publicada = c.Publicadas.Single();
        Afirmar.Sequencia<string>(["mon:direita"], publicada.Ocupados.Chaves);
        Afirmar.Igual(("primeiro plano,geometria", (EstadoDoShell?)EstadoDoShell.Ocupado, 1), (publicada.Motivos, publicada.Shell, publicada.Candidatos));
        Afirmar.Igual(publicada.Ocupados, c.Agenda.Publicados);

        c.Agenda.Sinalizar("geometria");
        c.Relogio.Avancar(250);
        Afirmar.Igual(2, c.Leituras, "leu de novo");
        Afirmar.Igual(1, c.Publicadas.Count, "o mesmo conjunto não é publicado de novo");
        Afirmar.Igual(0, c.Relogio.Pendentes, "nada periódico");
    }

    // O Alt+Tab para uma janela comum no outro monitor acaba a tela cheia: o conjunto vazio é publicado. Voltar ao jogo ocupa
    // de novo.
    [Teste]
    public void AltTabParaJanelaComum_PublicaOVazio_EVoltarAoJogoOcupaDeNovo()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        c.Agenda.AvaliarAgora("início");
        c.Leitura = new LeituraDoPrimeiroPlano(new RetanguloPx(-1928, -8, 8, 1040), EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);
        c.Agenda.Sinalizar("primeiro plano");
        c.Relogio.Avancar(250);
        c.Leitura = Jogo(Direita.Tela, EstadoDoShell.D3dEmTelaCheia);
        c.Agenda.Sinalizar("primeiro plano");
        c.Relogio.Avancar(250);
        Afirmar.Sequencia(["mon:direita", "-", "mon:direita"], c.Publicadas.Select(p => p.Ocupados.Vazio ? "-" : string.Join(";", p.Ocupados.Chaves)));
        Afirmar.Sequencia(["início", "primeiro plano", "primeiro plano"], c.Publicadas.Select(p => p.Motivos));
    }

    // A área de trabalho clicada cobre os dois monitores, mas o shell não diz tela cheia: nada fica ocupado. O mesmo para uma
    // janela que cobre o monitor com o shell em apresentação, ausente ou com a consulta falhando.
    [Teste]
    public void SemOSinalDoShell_NadaFicaOcupado_MesmoComAJanelaCobrindoTudo()
    {
        foreach (EstadoDoShell? shell in new EstadoDoShell?[] { EstadoDoShell.AceitaNotificacoes, EstadoDoShell.Apresentacao, EstadoDoShell.NaoPresente, EstadoDoShell.HoraQuieta, null })
        {
            var c = new Cenario { Leitura = new LeituraDoPrimeiroPlano(new RetanguloPx(-1920, 0, 1920, 1080), shell, DoBuzzy: false) };
            c.Agenda.AvaliarAgora("início");
            Afirmar.Igual(0, c.Publicadas.Count, $"shell {shell?.ToString() ?? "falhou"}: nada publicado");
            Afirmar.Verdadeiro(c.Agenda.Publicados.Vazio, $"shell {shell?.ToString() ?? "falhou"}: nada ocupado");
        }

        // Com o shell dizendo tela cheia e a janela cobrindo os dois, os dois ficam ocupados (um jogo esticado nos dois).
        var dois = new Cenario { Leitura = Jogo(new RetanguloPx(-1920, 0, 1920, 1080)) };
        dois.Agenda.AvaliarAgora("início");
        Afirmar.Sequencia<string>(["mon:direita", "mon:esquerda"], dois.Publicadas.Single().Ocupados.Chaves);
        Afirmar.Igual(2, dois.Publicadas.Single().Candidatos);
    }

    // Com o shell dizendo tela cheia, uma janela que não cobre nenhum monitor inteiro (outra janela em primeiro plano, a
    // maximizada, nenhuma) não ocupa nada: o candidato conta zero.
    [Teste]
    public void ShellEmTelaCheiaComUmaJanelaComum_NadaFicaOcupado()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        c.Agenda.AvaliarAgora("início");
        c.Leitura = Jogo(new RetanguloPx(-8, -8, 1928, 1040));
        c.Agenda.AvaliarAgora("primeiro plano");
        c.Leitura = new LeituraDoPrimeiroPlano(null, EstadoDoShell.Ocupado, DoBuzzy: false);
        c.Agenda.AvaliarAgora("primeiro plano");
        Afirmar.Igual(2, c.Publicadas.Count, "o jogo e o vazio; a janela nenhuma não muda mais nada");
        Afirmar.Igual((true, 0), (c.Publicadas[1].Ocupados.Vazio, c.Publicadas[1].Candidatos));
    }

    // O menu do Buzzy em primeiro plano (o observador não recebe o evento do próprio processo, mas uma avaliação pendente
    // pode cair com ele aberto): nada muda, e o jogo continua ocupando o monitor.
    [Teste]
    public void JanelaDoBuzzyEmPrimeiroPlano_NadaMuda()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        c.Agenda.AvaliarAgora("início");
        c.Leitura = new LeituraDoPrimeiroPlano(null, null, DoBuzzy: true);
        c.Agenda.Sinalizar("geometria");
        c.Relogio.Avancar(250);
        Afirmar.Igual(1, c.Publicadas.Count, "só o jogo");
        Afirmar.Sequencia<string>(["mon:direita"], c.Agenda.Publicados.Chaves, "continua ocupado");
    }

    // Antes de haver topologia, a avaliação não publica nada; a seguinte, com ela, publica.
    [Teste]
    public void SemTopologia_NadaPublicado()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela), Topologia = null };
        c.Agenda.AvaliarAgora("início");
        Afirmar.Igual(0, c.Publicadas.Count);
        c.Topologia = DoisMonitores;
        c.Agenda.AvaliarAgora("topologia");
        Afirmar.Igual(1, c.Publicadas.Count);
    }

    // Avaliar agora cancela a pendente e leva os motivos dela; parar cancela a pendente, e nenhum sinal depois agenda,
    // lê ou publica.
    [Teste]
    public void AvaliarAgoraLevaOsMotivosPendentes_PararCancelaTudo()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        c.Agenda.Sinalizar("primeiro plano");
        Afirmar.Verdadeiro(c.Agenda.AvaliacaoPendente, "agendada");
        c.Agenda.AvaliarAgora("início");
        Afirmar.Falso(c.Agenda.AvaliacaoPendente, "a pendente foi cancelada");
        Afirmar.Igual("primeiro plano,início", c.Publicadas.Single().Motivos);
        c.Relogio.Avancar(1000);
        Afirmar.Igual(1, c.Leituras, "a cancelada não lê");

        c.Leitura = new LeituraDoPrimeiroPlano(null, EstadoDoShell.AceitaNotificacoes, DoBuzzy: false);
        c.Agenda.Sinalizar("geometria");
        c.Agenda.Parar();
        c.Agenda.Sinalizar("primeiro plano");
        c.Agenda.AvaliarAgora("topologia");
        c.Relogio.Avancar(1000);
        Afirmar.Igual((1, 1, 0), (c.Leituras, c.Publicadas.Count, c.Relogio.Pendentes), "parada: nada mais");
    }

    // Os motivos de uma avaliação têm teto: os oito primeiros, na ordem, e quantos passaram dele.
    [Teste]
    public void MotivosComTeto()
    {
        var c = new Cenario { Leitura = Jogo(Direita.Tela) };
        for (int i = 1; i <= 11; i++) c.Agenda.Sinalizar($"s{i}");
        c.Relogio.Avancar(250);
        Afirmar.Igual("s1,s2,s3,s4,s5,s6,s7,s8,+3", c.Publicadas.Single().Motivos);
        Afirmar.Igual(8, AgendaDaTelaCheia.MaximoDeMotivos);
    }
}
