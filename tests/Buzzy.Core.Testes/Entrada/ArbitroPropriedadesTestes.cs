using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Entrada;

/// <summary>
/// Testes de propriedade da Fase 3 (TODO.md: "invariantes 1 e 2 sob sequências aleatórias"):
/// milhares de sequências de eventos de ponteiro, misturadas a comandos e eventos do sistema,
/// passam pelo árbitro e pelo núcleo, como na raiz de composição. A cada passo são conferidos a
/// gramática dos gestos, a captura, a regra de clique duplo, a coerência entre o árbitro e o
/// núcleo (nada fica preso ao cursor) e os invariantes 1, 2 e 5 de ARCHITECTURE.md 2.6. A
/// semente é fixa; toda falha informa a sequência e o passo.
/// </summary>
internal static class ArbitroPropriedadesTestes
{
    private const int Semente = 20260930;
    private const int Sequencias = 2500;
    private const int EventosPorSequencia = 160;

    /// <summary>O que o espelho sabe do gesto em curso, calculado só a partir dos eventos de ponteiro.</summary>
    private sealed class Espelho
    {
        public bool EmGesto;
        public bool Arrastou;
        public PontoPx Pressao;
        public long MsDaPressao;
        public MetricasDeGesto Metricas = MetricasDeGesto.Padrao;
        public bool DeveriaSerDuplo;
        public (PontoPx Ponto, long Ms, MetricasDeGesto Metricas)? CliqueAnterior;

        public void Reiniciar()
        {
            EmGesto = false;
            Arrastou = false;
            CliqueAnterior = null;
        }

        public bool ForaDoLimiar(PontoPx p)
            => Math.Abs((long)p.X - Pressao.X) > Math.Abs(Metricas.ArrasteX) || Math.Abs((long)p.Y - Pressao.Y) > Math.Abs(Metricas.ArrasteY);
    }

    [Teste]
    public static void GestosNucleoEInvariantesValemEmMilharesDeSequenciasDePonteiro()
    {
        var mestre = new Random(Semente);
        var contagem = new Dictionary<string, long>(StringComparer.Ordinal);
        long passos = 0;

        for (int n = 0; n < Sequencias; n++)
        {
            int sementeDaSequencia = mestre.Next();
            var rnd = new Random(sementeDaSequencia);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia topologia = gerador.NovaTopologia();
            var cfg = new ConfiguracaoDoNucleo
            {
                // Fase 3: sem queda animada, o personagem solto sem apoio vai direto ao chão.
                QuedaFisica = false,
                Acoes = AcoesAutonomas.Descansar | AcoesAutonomas.TrocarExpressao | (rnd.Next(2) == 0 ? AcoesAutonomas.Gesto : 0),
            };
            var nucleo = new Nucleo(cfg, (ulong)sementeDaSequencia);
            nucleo.Enfileirar(new Loaded(topologia, null, new Preferencias((NivelDeEnergia)rnd.Next(3), rnd.Next(2) == 0)));
            nucleo.Processar();

            var arbitro = new ArbitroDeGestos();
            var espelho = new Espelho();
            long ms = rnd.Next(0, 100_000);
            PontoPx cursor = NoCorpo(rnd, nucleo.Estado);

            for (int i = 0; i < EventosPorSequencia && nucleo.Estado.Estado != Estado.Exiting; i++)
            {
                string Onde() => $"sequência {n} (semente {sementeDaSequencia}), passo {i}";
                ms += rnd.Next(10) == 0 ? rnd.Next(0, 2000) : rnd.Next(0, 120);
                passos++;

                if (rnd.Next(100) < 12)
                {
                    // Evento do sistema, comando ou relógio, como os que a raiz entrega no meio de um gesto.
                    Evento outro = SortearOutro(rnd, gerador, nucleo.Estado, ref topologia);
                    Aplicar(nucleo, outro, arbitro, espelho, Onde, contagem);
                    Conferir(arbitro, espelho, nucleo.Estado, Onde);
                    continue;
                }

                EventoDePonteiro ponteiro = SortearPonteiro(rnd, gerador, topologia, nucleo.Estado, arbitro, espelho, ref cursor, ms);
                Arbitragem a = arbitro.Receber(ponteiro);
                ConferirGramatica(ponteiro, a, espelho, Onde, contagem);

                foreach (Evento gesto in a.Gestos)
                    Aplicar(nucleo, gesto, arbitro, espelho, Onde, contagem);

                Conferir(arbitro, espelho, nucleo.Estado, Onde);
            }
        }

        string resumo = string.Join(", ", contagem.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}={k.Value}"));
        Console.WriteLine($"         {Sequencias} sequências, {passos} passos; gestos: {resumo}");
        foreach (string gesto in new[] { "Press", "Click", "DoubleClick", "DragStart", "DragMove", "DragEnd", "DragCancel", "ContextMenu" })
            Afirmar.Verdadeiro(contagem.GetValueOrDefault(gesto) > 100, $"a geração exercitou {gesto} ({contagem.GetValueOrDefault(gesto)} vezes)");
    }

    /// <summary>Aplica um evento ao núcleo, como a raiz: soltar a captura pedida pelo núcleo zera o árbitro.</summary>
    private static void Aplicar(Nucleo nucleo, Evento evento, ArbitroDeGestos arbitro, Espelho espelho, Func<string> onde, Dictionary<string, long> contagem)
    {
        EstadoDoNucleo antes = nucleo.Estado;
        var resultados = new List<Resultado>();
        if (!nucleo.Enfileirar(evento)) return;
        IReadOnlyList<Efeito> efeitos = nucleo.Processar((_, r) => resultados.Add(r));
        EstadoDoNucleo depois = nucleo.Estado;
        contagem[evento.GetType().Name] = contagem.GetValueOrDefault(evento.GetType().Name) + 1;

        if (efeitos.Any(e => e is LiberarCaptura))
        {
            arbitro.Reiniciar();
            espelho.Reiniciar();
        }

        PontoPx? ancoraAntes = antes.Lugar?.Ancora, ancoraDepois = depois.Lugar?.Ancora;

        // Invariante 1: em PRESSED, DRAGGING e SETTLING nada autônomo muda estado ou posição.
        if (antes.Estado is Estado.Pressed or Estado.Dragging or Estado.Settling && evento.Origem is Origem.Autonomo or Origem.Relogio)
            Verificar(antes.Estado == depois.Estado && ancoraAntes == ancoraDepois, () => $"invariante 1: {onde()}: {evento} levou {antes.Estado}→{depois.Estado}");

        // Invariante 2: em DRAGGING, posição = cursor − pegada, e nenhuma física (sem relógio).
        if (depois.Estado == Estado.Dragging)
        {
            if (evento is DragMove m)
                Verificar(ancoraDepois == new PontoPx(m.Cursor.X - depois.Pegada.X, m.Cursor.Y - depois.Pegada.Y), () => $"invariante 2: {onde()}: âncora {ancoraDepois} para o cursor {m.Cursor}");
            Verificar(!depois.RelogioAtivo, () => $"invariante 2: {onde()}: relógio ligado durante o arraste");
        }

        // Invariante 5 e Fase 3: depois de SETTLING, a âncora fica na área útil do monitor
        // escolhido e, sem queda física, com os pés no chão dele.
        if (resultados.SelectMany(r => r.Transicoes).Any(t => t.De == Estado.Settling) && depois.Lugar is { } lugar)
        {
            RetanguloPx area = lugar.Monitor.AreaUtil;
            Verificar(lugar.Ancora.X >= area.Esquerda && lugar.Ancora.X < area.Direita && lugar.Ancora.Y > area.Topo && lugar.Ancora.Y <= area.Base,
                () => $"invariante 5: {onde()}: âncora {lugar.Ancora} fora da área útil {area} de {lugar.Monitor.Chave}");
            Verificar(lugar.Ancora.Y == area.Base, () => $"Fase 3: {onde()}: solto sem apoio fora do chão ({lugar.Ancora} com chão em {area.Base})");
            Verificar(depois.Topologia?.PorChave(lugar.Monitor.Chave) is not null, () => $"invariante 5: {onde()}: monitor {lugar.Monitor.Chave} não existe na topologia");
        }
    }

    /// <summary>A gramática: todo PRESS termina num só gesto final, e a captura acompanha o gesto.</summary>
    private static void ConferirGramatica(EventoDePonteiro ponteiro, Arbitragem a, Espelho e, Func<string> onde, Dictionary<string, long> contagem)
    {
        foreach (Evento g in a.Gestos)
        {
            string nome = g.GetType().Name;
            switch (g)
            {
                case Press p:
                    Verificar(!e.EmGesto, () => $"gramática: {onde()}: PRESS com gesto aberto");
                    Verificar(ponteiro is PonteiroPressionado { Botao: BotaoDoPonteiro.Esquerdo }, () => $"gramática: {onde()}: PRESS sem botão esquerdo pressionado ({ponteiro})");
                    var pp = (PonteiroPressionado)ponteiro;
                    e.EmGesto = true;
                    e.Arrastou = false;
                    e.Pressao = p.Cursor;
                    e.MsDaPressao = pp.Ms;
                    e.Metricas = pp.Metricas;
                    e.DeveriaSerDuplo = e.CliqueAnterior is { } c
                        && pp.Ms - c.Ms >= 0 && pp.Ms - c.Ms < c.Metricas.TempoDeCliqueDuploMs
                        && Math.Abs((long)p.Cursor.X - c.Ponto.X) < Math.Abs(c.Metricas.CliqueDuploLargura) / 2
                        && Math.Abs((long)p.Cursor.Y - c.Ponto.Y) < Math.Abs(c.Metricas.CliqueDuploAltura) / 2;
                    break;
                case DragStart:
                    Verificar(e.EmGesto && !e.Arrastou, () => $"gramática: {onde()}: DRAG_START fora de gesto ou repetido");
                    PontoPx? onde2 = ponteiro switch { PonteiroMovido m => m.Ponto, PonteiroSolto s => s.Ponto, _ => null };
                    Verificar(onde2 is { } q && e.ForaDoLimiar(q), () => $"limiar: {onde()}: DRAG_START sem sair do retângulo ({ponteiro})");
                    e.Arrastou = true;
                    e.CliqueAnterior = null;
                    break;
                case DragMove:
                    Verificar(e.EmGesto && e.Arrastou, () => $"gramática: {onde()}: DRAG_MOVE sem DRAG_START");
                    break;
                case DragEnd:
                    Verificar(e.EmGesto && e.Arrastou, () => $"gramática: {onde()}: DRAG_END sem DRAG_START");
                    e.EmGesto = false;
                    e.CliqueAnterior = null;
                    break;
                case Click:
                    Verificar(e.EmGesto && !e.Arrastou, () => $"gramática: {onde()}: CLICK fora de gesto ou depois de arrastar");
                    Verificar(ponteiro is PonteiroSolto s1 && !e.ForaDoLimiar(s1.Ponto), () => $"limiar: {onde()}: CLICK solto fora do retângulo");
                    Verificar(!e.DeveriaSerDuplo, () => $"clique duplo: {onde()}: CLICK onde o Windows daria clique duplo");
                    e.EmGesto = false;
                    e.CliqueAnterior = (e.Pressao, e.MsDaPressao, e.Metricas);
                    break;
                case DoubleClick:
                    Verificar(e.EmGesto && !e.Arrastou, () => $"gramática: {onde()}: DOUBLE_CLICK fora de gesto ou depois de arrastar");
                    Verificar(e.DeveriaSerDuplo, () => $"clique duplo: {onde()}: DOUBLE_CLICK sem clique anterior no tempo e no retângulo");
                    e.EmGesto = false;
                    e.CliqueAnterior = null;
                    break;
                case DragCancel:
                    Verificar(e.EmGesto, () => $"gramática: {onde()}: DRAG_CANCEL sem gesto");
                    e.EmGesto = false;
                    e.CliqueAnterior = null;
                    break;
                case ContextMenu:
                    Verificar(!e.EmGesto, () => $"gramática: {onde()}: CONTEXT_MENU durante gesto");
                    Verificar(ponteiro is PonteiroSolto { Botao: BotaoDoPonteiro.Direito }, () => $"gramática: {onde()}: CONTEXT_MENU sem botão direito solto");
                    break;
                default:
                    Afirmar.Falhar($"gramática: {onde()}: gesto inesperado {g}");
                    break;
            }
            contagem["gerado:" + nome] = contagem.GetValueOrDefault("gerado:" + nome) + 1;
        }
        Verificar(a.Capturar == e.EmGesto, () => $"captura: {onde()}: capturar={a.Capturar} com gesto aberto={e.EmGesto}");
    }

    /// <summary>Árbitro e núcleo concordam: sem gesto aberto, nada fica preso ao cursor.</summary>
    private static void Conferir(ArbitroDeGestos arbitro, Espelho e, EstadoDoNucleo s, Func<string> onde)
    {
        Verificar(arbitro.EmGesto == e.EmGesto && arbitro.Arrastando == (e.EmGesto && e.Arrastou), () => $"espelho: {onde()}: árbitro {arbitro.EmGesto}/{arbitro.Arrastando}, espelho {e.EmGesto}/{e.Arrastou}");
        if (!arbitro.EmGesto)
            Verificar(s.Estado is not (Estado.Pressed or Estado.Dragging), () => $"preso ao cursor: {onde()}: núcleo em {s.Estado} sem gesto no árbitro");
        if (s.Estado == Estado.Dragging)
            Verificar(arbitro.Arrastando, () => $"coerência: {onde()}: núcleo arrastando sem arraste no árbitro");
        if (s.Estado == Estado.Pressed)
            Verificar(arbitro.EmGesto && !arbitro.Arrastando, () => $"coerência: {onde()}: núcleo pressionado com árbitro {arbitro.EmGesto}/{arbitro.Arrastando}");
    }

    private static EventoDePonteiro SortearPonteiro(
        Random rnd, GeradorDeTopologias gerador, Topologia topologia, EstadoDoNucleo s, ArbitroDeGestos arbitro, Espelho e, ref PontoPx cursor, long ms)
    {
        var metricas = rnd.Next(4) switch
        {
            0 => new MetricasDeGesto(6, 6, 6, 6, 500),
            1 => new MetricasDeGesto(rnd.Next(1, 12), rnd.Next(1, 12), rnd.Next(2, 12), rnd.Next(2, 12), rnd.Next(200, 900)),
            _ => MetricasDeGesto.Padrao,
        };
        int sorteio = rnd.Next(100);
        if (!arbitro.EmGesto)
        {
            // Sem gesto: pressionar no corpo (quase sempre perto do clique anterior, para gerar
            // cliques duplos), botão direito, ou movimentos e soltares soltos.
            if (sorteio < 55)
            {
                cursor = e.CliqueAnterior is { } c && rnd.Next(2) == 0
                    ? new PontoPx(c.Ponto.X + rnd.Next(-2, 3), c.Ponto.Y + rnd.Next(-2, 3))
                    : NoCorpo(rnd, s);
                return new PonteiroPressionado(cursor, BotaoDoPonteiro.Esquerdo, ms, metricas);
            }
            if (sorteio < 65) return new PonteiroPressionado(NoCorpo(rnd, s), BotaoDoPonteiro.Direito, ms, metricas);
            if (sorteio < 75) return new PonteiroSolto(NoCorpo(rnd, s), BotaoDoPonteiro.Direito, ms);
            if (sorteio < 85) return new PonteiroSolto(cursor, BotaoDoPonteiro.Esquerdo, ms);
            if (sorteio < 95) return new PonteiroMovido(cursor = Andar(rnd, gerador, topologia, cursor), rnd.Next(2) == 0, ms);
            return new CapturaPerdida(ms);
        }

        // Com gesto: movimentos pequenos (que ficam no limiar) e grandes, soltar, perdas.
        return sorteio switch
        {
            < 55 => new PonteiroMovido(cursor = Andar(rnd, gerador, topologia, cursor), rnd.Next(40) != 0, ms),
            < 80 => new PonteiroSolto(cursor = rnd.Next(3) == 0 ? Andar(rnd, gerador, topologia, cursor) : cursor, BotaoDoPonteiro.Esquerdo, ms),
            < 85 => new CapturaPerdida(ms),
            < 90 => new PonteiroPressionado(cursor, BotaoDoPonteiro.Esquerdo, ms, metricas),
            < 95 => new PonteiroPressionado(cursor, BotaoDoPonteiro.Direito, ms, metricas),
            _ => new PonteiroSolto(cursor, BotaoDoPonteiro.Direito, ms),
        };
    }

    private static PontoPx Andar(Random rnd, GeradorDeTopologias gerador, Topologia topologia, PontoPx cursor) => rnd.Next(10) switch
    {
        < 4 => new PontoPx(cursor.X + rnd.Next(-3, 4), cursor.Y + rnd.Next(-3, 4)),
        < 8 => new PontoPx(cursor.X + rnd.Next(-200, 201), cursor.Y + rnd.Next(-200, 201)),
        _ => gerador.Ponto(topologia),
    };

    private static PontoPx NoCorpo(Random rnd, EstadoDoNucleo s)
    {
        PontoPx ancora = s.Lugar?.Ancora ?? new PontoPx(0, 0);
        return new PontoPx(ancora.X + rnd.Next(-30, 31), ancora.Y - rnd.Next(5, 110));
    }

    private static Evento SortearOutro(Random rnd, GeradorDeTopologias gerador, EstadoDoNucleo s, ref Topologia topologia)
    {
        int sorteio = rnd.Next(100);
        switch (sorteio)
        {
            case < 20:
                return new AutonomyTimer(rnd.Next(5) == 0 ? s.Geracao - 1 : s.Geracao);
            case < 35:
                return new Tick();
            case < 45:
                topologia = rnd.Next(3) == 0 ? gerador.NovaTopologia() : gerador.Mudar(topologia);
                return new TopologyChanged(topologia);
            case < 55:
                return new CmdHide();
            case < 70:
                return new CmdShow();
            case < 75:
                return new SessionLocked();
            case < 80:
                return new SessionUnlocked();
            case < 83:
                return new Suspending();
            case < 86:
                return new Resumed();
            case < 92:
                return new FullscreenTargetsChanged(new MonitoresOcupados(topologia.Monitores.Select(m => m.Chave).Where(_ => rnd.Next(2) == 0)));
            case < 96:
                // As 14 caras de humor: um valor novo no fim do enum não muda os sorteios do gerador.
                return new ExpressionChange((Expressao)rnd.Next(14));
            default:
                return rnd.Next(4) == 0 ? new CmdExit() : new CmdResetPosition();
        }
    }

    private static void Verificar(bool condicao, Func<string> mensagem)
    {
        if (!condicao) Afirmar.Falhar(mensagem());
    }
}
