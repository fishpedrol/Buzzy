using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P7 (DEC-037; invariantes 31 a 33 da ARCHITECTURE.md 2.6): sequências aleatórias com a curiosidade e a
/// personalidade ligadas, no relógio virtual, misturando o foco, o tempo e o usuário (clique, arraste, pausa, ocultação,
/// sessão, tela cheia, redefinir a posição e topologias). Em cada evento: a curiosidade só move o personagem a partir de
/// IDLE, sem pausa, sem dispensa e nunca para um monitor ocupado (31 e 32); a dispensa só cai pelo disparo dela; o vão só é
/// pedido no monitor do foco; o disparo agendado no núcleo é o pendente no simulador; e nada guarda o vão (33).
/// </summary>
internal static class CuriosidadeAleatoriaTestes
{
    private const int Sequencias = 120, PassosPorSequencia = 160;

    private static readonly Topologia[] Topologias =
    [
        TopologiasDeExemplo.LadoALado, TopologiasDeExemplo.UmMonitor, TopologiasDeExemplo.EmpilhadoSecundarioAcima,
        TopologiasDeExemplo.EmL, TopologiasDeExemplo.SecundarioAEsquerda, TopologiasDeExemplo.EscalasMistas,
    ];

    [Teste]
    public static void SequenciasAleatorias_OsInvariantesDaCuriosidade()
    {
        var contagem = new Dictionary<string, int>();
        void Contar(string chave) => contagem[chave] = contagem.GetValueOrDefault(chave) + 1;
        for (int semente = 1; semente <= Sequencias; semente++)
        {
            var rnd = new Random(semente);
            var energia = (NivelDeEnergia)rnd.Next(3);
            ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with
            {
                Curiosidade = true,
                Personalidade = true,
                Acoes = rnd.Next(3) == 0 ? AcoesAutonomas.Nenhuma : AcoesAutonomas.Todas | AcoesAutonomas.IrAoOutroMonitor,
            };
            Topologia topologia = Topologias[rnd.Next(Topologias.Length)];
            var sim = new SimuladorDeTempo(cfg, (ulong)semente, topologia, new Preferencias(energia, true))
            {
                ResponderVao = _ => rnd.Next(6) switch
                {
                    0 => null,
                    1 => new VaoDaJanela(0, 16),
                    _ => new VaoDaJanela(rnd.Next(0, 8), rnd.Next(9, 17)),
                },
            };
            string onde = $"semente {semente}";
            sim.AoResultado = (antes, e, r) =>
            {
                foreach (Transicao t in r.Transicoes)
                {
                    if (t.Regra.StartsWith("IDLE + CURIOSIDADE: vai ver", StringComparison.Ordinal))
                    {
                        Contar("ida");
                        Afirmar.Verdadeiro(e is CuriosityTimer or AutonomyTimer, $"{onde}: a ida só num disparo ({e})");
                        Afirmar.Verdadeiro(antes.Estado == Estado.Idle && !antes.AutonomiaPausada && !antes.DispensaAtiva && antes.Estado.Visivel(), $"{onde}: a ida só de IDLE livre ({antes.Estado}, pausada {antes.AutonomiaPausada}, dispensa {antes.DispensaAtiva})");
                        Afirmar.Falso(antes.FocoDoPrimeiroPlano is { } foco && antes.Ocupados.Contem(foco), $"{onde}: a ida nunca para um ocupado");
                    }
                    if (t.Regra.StartsWith("IDLE + CURIOSIDADE: chega perto", StringComparison.Ordinal))
                    {
                        Contar("aproximação");
                        Afirmar.Verdadeiro(e is ActiveWindowSpan && antes.Estado == Estado.Idle && !antes.AutonomiaPausada && !antes.DispensaAtiva, $"{onde}: chegar perto só com o vão, em IDLE livre ({e}, {antes.Estado})");
                    }
                    if (t.Regra == "CURIOSIDADE: olha a janela") Contar("olhar");
                    if (t.Regra.StartsWith("IDLE + CURIOSIDADE: olha de longe", StringComparison.Ordinal)) Contar("de longe");
                    if (t.Regra.StartsWith("CURIOSIDADE: dispensada", StringComparison.Ordinal)) Contar("dispensa");
                }
                if (antes.DispensaAtiva && !r.Estado.DispensaAtiva)
                    Afirmar.Verdadeiro(e is CuriosityTimer, $"{onde}: a dispensa só cai pelo disparo dela ({e})");
                foreach (PedirVaoDaJanelaAtiva pedido in r.Efeitos.OfType<PedirVaoDaJanelaAtiva>())
                {
                    Contar("vão");
                    Afirmar.Igual(r.Estado.Lugar?.Monitor.Chave, pedido.Chave, $"{onde}: o vão no monitor dele");
                    Afirmar.Igual(r.Estado.FocoDoPrimeiroPlano, pedido.Chave, $"{onde}: o vão no monitor do foco");
                }
                Afirmar.Verdadeiro(r.Efeitos.OfType<AgendarCuriosidade>().Count() + r.Efeitos.OfType<CancelarCuriosidade>().Count() <= 1, $"{onde}: um efeito do disparo por evento");
            };
            sim.AoAplicar = (_, e, depois) =>
            {
                if (e is ActiveWindowSpan) return; // a resposta sai de dentro do Aplicar do pedido
                Afirmar.Igual(depois.CuriosidadeAgendada, sim.CuriosidadeVenceEmMs is not null, $"{onde}: o disparo agendado no núcleo é o pendente no simulador ({e})");
            };

            string[] chaves = [.. topologia.Monitores.Select(m => m.Chave), "mon:sumiu"];
            for (int passo = 0; passo < PassosPorSequencia; passo++)
            {
                int sorteio = rnd.Next(100);
                if (sorteio < 45)
                {
                    sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 120)));
                    continue;
                }
                if (sim.Estado.Lugar is not { } lugar) continue;
                var noCorpo = new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 30);
                switch (sorteio)
                {
                    case < 60:
                        sim.Aplicar(new ForegroundMonitorChanged(chaves[rnd.Next(chaves.Length)]));
                        break;
                    case < 66:
                        sim.Aplicar(new Press(noCorpo));
                        sim.Aplicar(new Click());
                        break;
                    case < 71:
                    {
                        MonitorDoDesktop m = topologia.Monitores[rnd.Next(topologia.Monitores.Count)];
                        var cursor = new PontoPx(rnd.Next(m.AreaUtil.Esquerda + 80, m.AreaUtil.Direita - 80), m.AreaUtil.Base - 30);
                        sim.Aplicar(new Press(noCorpo));
                        sim.Aplicar(new DragStart());
                        sim.Aplicar(new DragMove(cursor));
                        sim.Aplicar(new DragEnd(cursor));
                        break;
                    }
                    case < 75:
                        sim.Aplicar(sim.Estado.AutonomiaPausada ? new CmdResumeAutonomy() : new CmdPauseAutonomy());
                        break;
                    case < 79:
                        sim.Aplicar(sim.Estado.Estado == Estado.Hidden ? new CmdShow() : new CmdHide());
                        break;
                    case < 82:
                        sim.Aplicar(new SessionLocked());
                        sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 60)));
                        sim.Aplicar(new SessionUnlocked());
                        break;
                    case < 88:
                        sim.Aplicar(new FullscreenTargetsChanged(rnd.Next(2) == 0 ? MonitoresOcupados.Nenhum
                            : new MonitoresOcupados([topologia.Monitores[rnd.Next(topologia.Monitores.Count)].Chave])));
                        break;
                    case < 90:
                        sim.Aplicar(new CmdResetPosition());
                        break;
                    case < 93:
                        sim.Aplicar(new CmdSetFullscreenMode(rnd.Next(2) == 0));
                        break;
                    case < 95:
                        sim.ResponderAoVao = !sim.ResponderAoVao;
                        break;
                    default:
                        topologia = Topologias[rnd.Next(Topologias.Length)];
                        chaves = [.. topologia.Monitores.Select(m => m.Chave), "mon:sumiu"];
                        sim.Aplicar(new TopologyChanged(topologia));
                        break;
                }
            }
        }
        Console.WriteLine($"         {string.Join(", ", contagem.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))}");
        foreach (string caso in new[] { "ida", "aproximação", "olhar", "de longe", "dispensa", "vão" })
            Afirmar.Verdadeiro(contagem.GetValueOrDefault(caso) >= 10, $"o caso \"{caso}\" aconteceu ({contagem.GetValueOrDefault(caso)})");
    }
}
