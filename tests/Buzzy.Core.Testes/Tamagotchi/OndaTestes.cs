using System.Text;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// A onda da frente do tamagotchi adulto (DEC-028, passo T4), atrás da chave Tamagotchi: o temporizador único da onda
/// (fases e níveis, disparos únicos de 1 s ou mais), o perfil e a física em vigor, o cambaleio e as caras e os gestos da
/// fase. Ainda não há itens: a onda nasce semeada no estado pelo construtor Nucleo(config, estado)
/// (<see cref="SimuladorDeTempo.Semeado"/>), como se um item a tivesse começado agora. Tudo é de desenho animado.
/// </summary>
internal static class OndaTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    /// <summary>
    /// A configuração do aplicativo com o tamagotchi ligado de forma explícita: o aplicativo o liga desde o passo T9, e
    /// estes testes não dependem de a configuração dele continuar assim.
    /// </summary>
    // A onda como na Fase 4 a 6: sem a personalidade e a curiosidade da Fase 7 (DEC-037), que mudam os gestos e o gerador delas.
    private static ConfiguracaoDoNucleo Ligado() => ConfiguracaoDoNucleo.DoAplicativo(Sprite) with { Tamagotchi = true, Personalidade = false, Curiosidade = false };

    /// <summary>Um evento aplicado pela máquina: o estado logo antes, o evento e o resultado.</summary>
    private sealed record Aplicado(EstadoDoNucleo Antes, Evento Evento, Resultado Resultado);

    private static List<Aplicado> Registrar(SimuladorDeTempo sim)
    {
        var aplicados = new List<Aplicado>();
        sim.AoResultado = (antes, e, r) => aplicados.Add(new Aplicado(antes, e, r));
        return aplicados;
    }

    /// <summary>
    /// A onda semeada no estado, como se um item a tivesse começado agora, com a cara da fase (ou sem mexer na cara). O
    /// primeiro evento depois dela, aqui um disparo da onda sem nada agendado, que a máquina ignora, só agenda o
    /// temporizador. <paramref name="antesDoPrimeiroEvento"/> pode registrar os resultados desde esse evento.
    /// </summary>
    private static SimuladorDeTempo Semear(SimuladorDeTempo sim, EstadoDaOnda onda, Action<SimuladorDeTempo>? antesDoPrimeiroEvento = null, bool comACara = true)
    {
        Expressao cara = sim.Nucleo.Configuracao.TabelaDeOndas(onda.Tipo).Cara(onda.Fase);
        SimuladorDeTempo semeado = sim.Semeado(s => s with { Onda = onda, Expressao = comACara ? cara : s.Expressao });
        antesDoPrimeiroEvento?.Invoke(semeado);
        semeado.Aplicar(new ItemEffectTimer(semeado.Estado.GeracaoDaOnda));
        return semeado;
    }

    /// <summary>As fases de um episódio sozinho, desde a subida no nível dado: a subida, o pico do nível até o 1 e a queda (nível 1), se houver.</summary>
    private static List<EstadoDaOnda> Fases(Onda onda, int nivel)
    {
        var fases = new List<EstadoDaOnda> { new(onda, FaseDaOnda.Subida, nivel, nivel) };
        for (int n = nivel; n >= 1; n--) fases.Add(new(onda, FaseDaOnda.Pico, n, nivel));
        if (TabelasDoDesenho.Esperada(onda).QuedaBase is not null) fases.Add(new(onda, FaseDaOnda.Queda, 1, nivel));
        return fases;
    }

    /// <summary>A cara de cada fase, pela transcrição do desenho (4.2): a queda sem cara própria fica com a do pico.</summary>
    private static Expressao CaraEsperada(EstadoDaOnda onda)
    {
        TabelasDoDesenho.OndaEsperada e = TabelasDoDesenho.Esperada(onda.Tipo);
        return onda.Fase switch
        {
            FaseDaOnda.Subida => e.CaraDaSubida,
            FaseDaOnda.Pico => e.CaraDoPico,
            _ => e.CaraDaQueda ?? e.CaraDoPico,
        };
    }

    // ---------------------------------------------------------------- o temporizador da onda

    // Tabela 4.2 e regras de tempo: semeada na subida, sozinha, cada onda em cada nível passa pela subida, por um disparo
    // por nível do pico e pela queda (base × 100, 125 ou 150% pelo pior nível), e acaba. Os atrasos do AgendarOnda são os
    // da tabela, um disparo único por fase ou nível (1 + nível, e mais 1 com queda), cada um com uma geração nova; a cara da
    // fase entra a cada disparo; no fim, a onda some, a cara volta à de base e nenhum temporizador fica pendente. Com a
    // autonomia pausada, nada mais acontece: nem relógio, nem sorteio, nem movimento.
    [Teste]
    public static void CadaOnda_FasesComAsDuracoesDaTabela()
    {
        foreach (Onda onda in Enum.GetValues<Onda>())
        {
            foreach (int nivel in new[] { 1, 2, 3 })
            {
                string caso = $"{onda} no nível {nivel}";
                var sim = new SimuladorDeTempo(Ligado(), 5, TopologiasDeExemplo.UmMonitor);
                sim.Aplicar(new CmdPauseAutonomy());
                List<Aplicado> aplicados = [];
                SimuladorDeTempo semeado = Semear(sim, new EstadoDaOnda(onda, FaseDaOnda.Subida, nivel, nivel), s => aplicados = Registrar(s));
                Aleatorio gerador = semeado.Estado.Aleatorio;
                PontoPx ancora = semeado.Estado.Lugar!.Ancora;
                semeado.Avancar(TimeSpan.FromMinutes(15));

                List<EstadoDaOnda> fases = Fases(onda, nivel);
                AgendarOnda[] agendas = [.. aplicados.SelectMany(a => a.Resultado.Efeitos).OfType<AgendarOnda>()];
                Afirmar.Sequencia(TabelasDoDesenho.Esperada(onda).Atrasos(nivel), agendas.Select(a => a.Atraso), $"{caso}: os atrasos de cada fase");
                Afirmar.Sequencia(Enumerable.Range(1, fases.Count).Select(i => (long)i), agendas.Select(a => a.Geracao), $"{caso}: uma geração nova por agendamento");
                Afirmar.Verdadeiro(aplicados.All(a => a.Evento is ItemEffectTimer), $"{caso}: pausado, só os disparos da onda acontecem ({string.Join(", ", aplicados.Select(a => a.Evento))})");

                Aplicado[] disparos = [.. aplicados.Skip(1)];
                Afirmar.Igual(fases.Count, disparos.Length, $"{caso}: um disparo por fase ou nível");
                for (int i = 0; i < disparos.Length; i++)
                {
                    string onde = $"{caso}, {i + 1}º disparo";
                    EstadoDaOnda? esperada = i + 1 < fases.Count ? fases[i + 1] : null;
                    EstadoDoNucleo depois = disparos[i].Resultado.Estado;
                    Afirmar.Igual(new ItemEffectTimer(i + 1), disparos[i].Evento, $"{onde}: o disparo da geração agendada");
                    Afirmar.Igual(esperada, depois.Onda, $"{onde}: a fase seguinte");
                    Afirmar.Igual(esperada is { } o ? CaraEsperada(o) : Expressao.Neutro, depois.Expressao, $"{onde}: a cara da fase entra na hora (no fim, a de base)");
                    Afirmar.Igual(Estado.Idle, depois.Estado, $"{onde}: continua parado");
                    Transicao t = disparos[i].Resultado.Transicoes.Single();
                    Afirmar.Verdadeiro(t.De == Estado.Idle && t.Para == Estado.Idle && t.Regra.StartsWith("ITEM_EFFECT_TIMER: onda ", StringComparison.Ordinal), $"{onde}: só registra o disparo ({t})");
                    Afirmar.Falso(disparos[i].Resultado.Efeitos.Any(e => e is LigarRelogio or AgendarDecisao or CancelarDecisao or CancelarOnda or MoverJanela), $"{onde}: nem relógio, nem agenda, nem janela");
                }
                Afirmar.Nulo(semeado.Estado.Onda, $"{caso}: a onda acabou");
                Afirmar.Falso(semeado.Estado.OndaAgendada, $"{caso}: sem temporizador pendente");
                Afirmar.Igual(gerador, semeado.Estado.Aleatorio, $"{caso}: nenhum sorteio");
                Afirmar.Igual(ancora, semeado.Estado.Lugar!.Ancora, $"{caso}: não se moveu");
                Afirmar.Falso(semeado.RelogioLigado, $"{caso}: o relógio nunca ligou");
            }
        }
    }

    // A geração protege a onda como protege a agenda: um disparo de outra geração, ou o mesmo disparo repetido depois de
    // atendido, não muda nada nem tem efeito. Semeada sem agendamento, o primeiro disparo também não avança a onda: só
    // agenda o temporizador da fase em que ela está.
    [Teste]
    public static void DisparoAntigoOuRepetido_EhIgnorado()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        var sim = new SimuladorDeTempo(cfg, 5, TopologiasDeExemplo.UmMonitor);
        sim.Aplicar(new CmdPauseAutonomy());
        var subida = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2);
        EstadoDoNucleo semeado = sim.Semeado(s => s with { Onda = subida }).Estado;

        Resultado primeiro = Maquina.Aplicar(semeado, new ItemEffectTimer(semeado.GeracaoDaOnda), cfg);
        Afirmar.Igual(subida, primeiro.Estado.Onda, "sem agendamento, o disparo não avança a onda");
        Afirmar.Sequencia([], primeiro.Transicoes, "nem registra transição");
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(8), 1)], primeiro.Efeitos, "só agenda a subida do bebado (8 s)");
        EstadoDoNucleo agendado = primeiro.Estado;
        Afirmar.Verdadeiro(agendado.OndaAgendada && agendado.GeracaoDaOnda == 1, "pendente, na geração 1");

        foreach (long outra in new[] { 0L, 2L, -1L, long.MaxValue })
        {
            Resultado r = Maquina.Aplicar(agendado, new ItemEffectTimer(outra), cfg);
            Afirmar.Igual(agendado, r.Estado, $"geração {outra}: nada muda");
            Afirmar.Sequencia([], r.Efeitos, $"geração {outra}: sem efeito");
            Afirmar.Sequencia([], r.Transicoes, $"geração {outra}: sem transição");
        }

        Resultado certo = Maquina.Aplicar(agendado, new ItemEffectTimer(1), cfg);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2), certo.Estado.Onda, "o disparo da geração agendada avança para o pico");
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(100), 2)], certo.Efeitos, "e agenda o nível do pico");
        Afirmar.Sequencia([new Transicao(Estado.Idle, Estado.Idle, "ITEM_EFFECT_TIMER: onda Bebado/Subida/2 -> Bebado/Pico/2")], certo.Transicoes, "registra o disparo");
        Resultado repetido = Maquina.Aplicar(certo.Estado, new ItemEffectTimer(1), cfg);
        Afirmar.Igual(certo.Estado, repetido.Estado, "o mesmo disparo de novo: nada muda");
        Afirmar.Sequencia([], repetido.Efeitos, "e nada sai");

        // Um disparo que chega depois de a onda acabar também não faz nada.
        var fim = new EstadoDaOnda(Onda.Satisfeito, FaseDaOnda.Pico, 1, 1);
        Resultado acabou = Maquina.Aplicar(agendado with { Onda = fim }, new ItemEffectTimer(1), cfg);
        EstadoDoNucleo noFim = acabou.Estado;
        Afirmar.Nulo(noFim.Onda, "o satisfeito acaba no fim do pico");
        Afirmar.Sequencia([new Transicao(Estado.Idle, Estado.Idle, "ITEM_EFFECT_TIMER: onda Satisfeito/Pico/1 -> fim")], acabou.Transicoes, "registra o fim");
        Afirmar.Sequencia([], acabou.Efeitos, "sem onda e sem disparo pendente, nada a cancelar");
        Afirmar.Falso(noFim.OndaAgendada, "sem temporizador pendente");
        Resultado atrasado = Maquina.Aplicar(noFim, new ItemEffectTimer(1), cfg);
        Afirmar.Igual(noFim, atrasado.Estado, "disparo atrasado depois do fim: nada muda");
        Afirmar.Sequencia([], atrasado.Efeitos, "e nada sai");
    }

    // Com a autonomia pausada, o disparo da onda só troca a cara (D9): parado no chão, escondido na borda (DEC-025) e
    // preso pelo usuário na parede (DEC-024), nada sai do lugar, o estado não muda, o relógio não liga, a agenda não volta
    // e o gerador não sorteia; a cara de cada fase aparece na hora (as três situações mostram a cara).
    [Teste]
    public static void Pausado_ODisparoSoTrocaACara()
    {
        foreach (string situacao in new[] { "parado", "escondido", "preso na parede" })
        {
            var sim = new SimuladorDeTempo(Ligado(), 4, TopologiasDeExemplo.UmMonitor);
            Posicionamento l = sim.Estado.Lugar!;
            Superficies sup = Superficies.Do(sim.Estado.Topologia!, l.Monitor, l.Tamanho);
            var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 20);
            if (situacao == "escondido")
            {
                foreach (Evento e in new Evento[] { new Press(corpo), new Click(), new Press(corpo), new DoubleClick() }) sim.Aplicar(e);
                sim.Esta(Estado.Peeking, "escondido na borda");
            }
            else if (situacao == "preso na parede")
            {
                var naParede = new PontoPx(sup.Direita - 20, 600 - 20);
                foreach (Evento e in new Evento[] { new Press(corpo), new DragStart(), new DragMove(naParede), new DragEnd(naParede) }) sim.Aplicar(e);
                sim.Esta(Estado.Climbing, "grudado na parede");
                Afirmar.Verdadeiro(sim.Estado.PresoPeloUsuario && sim.Estado.Movimento.Agarrado, "preso e agarrado");
            }
            sim.Aplicar(new CmdPauseAutonomy());
            Estado estado = sim.Estado.Estado;

            List<Aplicado> aplicados = [];
            SimuladorDeTempo semeado = Semear(sim, new EstadoDaOnda(Onda.Eletrico, FaseDaOnda.Subida, 3, 3), s => aplicados = Registrar(s), comACara: false);
            EstadoDoNucleo inicio = semeado.Estado;
            semeado.Avancar(TimeSpan.FromMinutes(15));

            Aplicado[] disparos = [.. aplicados.Skip(1)];
            Afirmar.Igual(Fases(Onda.Eletrico, 3).Count, disparos.Length, $"{situacao}: os disparos da onda, e só eles");
            Afirmar.Verdadeiro(aplicados.All(a => a.Evento is ItemEffectTimer), $"{situacao}: nada além da onda acontece");
            foreach (Aplicado d in disparos)
            {
                EstadoDoNucleo depois = d.Resultado.Estado;
                string onde = $"{situacao}, {d.Evento}";
                Afirmar.Igual(estado, depois.Estado, $"{onde}: o estado não muda");
                Afirmar.Verdadeiro(d.Resultado.Transicoes.All(t => t.De == estado && t.Para == estado), $"{onde}: nenhuma transição de estado");
                Afirmar.Igual(inicio.Lugar, depois.Lugar, $"{onde}: não sai do lugar");
                Afirmar.Igual(inicio.Movimento, depois.Movimento, $"{onde}: nem se move");
                Afirmar.Igual(inicio.Aleatorio, depois.Aleatorio, $"{onde}: nenhum sorteio");
                Afirmar.Falso(depois.RelogioAtivo || depois.DecisaoAgendada, $"{onde}: sem relógio e sem agenda");
                Afirmar.Igual(depois.Onda is { } o ? CaraEsperada(o) : Expressao.Neutro, depois.Expressao, $"{onde}: só a cara muda, para a da fase");
            }
            Afirmar.Nulo(semeado.Estado.Onda, $"{situacao}: a onda acabou");
            Afirmar.Falso(semeado.RelogioLigado, $"{situacao}: sem relógio");
        }
    }

    // Repouso sem relógio (DEC-011) e um temporizador só (invariante 25): uma hora simulada por semente, com a agenda livre,
    // interações sorteadas e ondas semeadas de tipos e níveis sorteados, na configuração do aplicativo, que às vezes o faz
    // fumar um baseado por conta própria (pedido do usuário de 2026-10-01, 19:10). Todo agendamento da onda é um disparo único
    // de 1 s ou mais; com onda há exatamente um pendente, sem onda nenhum; o disparo nunca mexe no relógio, na agenda nem na
    // janela, e só muda as ondas, a carga do episódio e a cara, as duas primeiras pela regra (a frente avança, a de fundo só
    // volta quando a frente acaba, e a carga só zera sem onda de substância); o relógio segue a regra de sempre (com o uso
    // do baseado); a onda da frente acaba em no máximo 2 + nível disparos, contados de novo a cada uso e a cada troca da onda
    // da frente; a agenda sorteia na faixa do perfil efetivo, acima do piso (R11); os gestos, da onda ou não, só acontecem
    // em IDLE; e o apoio vale depois de cada evento.
    [Teste]
    public static void EmRepouso_ORelogioNaoLigaEOsDisparosSaoUnicos()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        var mestre = new Random(2029);
        long eventos = 0, disparos = 0, episodios = 0, comOnda = 0, fumou = 0, fundoVoltou = 0, cargaZerada = 0;
        for (int n = 0; n < 6; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var sim = new SimuladorDeTempo(cfg, (ulong)semente, TopologiasDeExemplo.UmMonitor, new Preferencias((NivelDeEnergia)rnd.Next(3), true));
            int disparosNoEpisodio = 0, limite = 0;
            void Conferir(EstadoDoNucleo antes, Evento e, Resultado r)
            {
                eventos++;
                EstadoDoNucleo depois = r.Estado;
                if (depois.Onda is not null) comOnda++;
                string onde = $"semente {semente}, {e} em {antes.Estado} -> {depois.Estado} (onda {depois.Onda})";
                AgendarOnda[] agendas = [.. r.Efeitos.OfType<AgendarOnda>()];
                Afirmar.Verdadeiro(agendas.All(a => a.Atraso >= TimeSpan.FromSeconds(1)), $"{onde}: disparos únicos de 1 s ou mais");
                Afirmar.Verdadeiro(agendas.Length + r.Efeitos.OfType<CancelarOnda>().Count() <= 1, $"{onde}: um temporizador da onda por vez");
                Afirmar.Igual(depois.Onda is not null, depois.OndaAgendada, $"{onde}: com onda, exatamente um disparo pendente; sem onda, nenhum");
                bool agarrado = depois.Estado is Estado.Climbing or Estado.Hanging && depois.Movimento.Agarrado;
                // USING também (invariante 29): a agenda do aplicativo às vezes o faz fumar um baseado por conta própria.
                bool relogio = (depois.Estado.EmMovimento() && !agarrado) || depois.Estado is Estado.Reacting or Estado.Using || (depois.Estado == Estado.Idle && depois.Gesto != Gesto.Nenhum);
                Afirmar.Igual(relogio, depois.RelogioAtivo, $"{onde}: o relógio segue a regra de sempre");
                if (e is ItemEffectTimer)
                {
                    Afirmar.Falso(r.Efeitos.Any(x => x is LigarRelogio or DesligarRelogio or AgendarDecisao or CancelarDecisao or MoverJanela or MostrarJanela or EsconderJanela), $"{onde}: o disparo não mexe no relógio, na agenda nem na janela");
                    Afirmar.Igual(SemAOnda(antes), SemAOnda(depois), $"{onde}: o disparo só muda as ondas, a carga do episódio e a cara");
                    // As ondas e a carga, pela regra escrita aqui à parte (revisão do baseado por conta própria, achado 9), já que
                    // SemAOnda as deixa de fora: o disparo que vale (com a transição) avança a onda da frente (DepoisDoDisparo), e a
                    // de fundo só muda quando a da frente acaba, voltando inteira para a frente; o que não vale não muda nenhuma.
                    // A carga do episódio fica igual, a não ser que não sobre onda de substância, nem na frente nem no fundo: aí
                    // ela volta a 0.
                    (EstadoDaOnda? frente, EstadoDaOnda? fundo) = r.Transicoes.Count > 0 && antes.Onda is { } daFrente
                        ? DepoisDoDisparo(daFrente, antes.OndaDeFundo)
                        : (antes.Onda, antes.OndaDeFundo);
                    Afirmar.Igual((frente, fundo), (depois.Onda, depois.OndaDeFundo), $"{onde}: a onda da frente e a de fundo depois do disparo (antes, fundo {antes.OndaDeFundo})");
                    CargaDaParanoia carga = DeSubstancia(depois.Onda) || DeSubstancia(depois.OndaDeFundo) ? antes.Carga : CargaDaParanoia.Nenhuma;
                    Afirmar.Igual(carga, depois.Carga, $"{onde}: a carga do episódio depois do disparo (antes, {antes.Carga.Substancias}; fundo {depois.OndaDeFundo})");
                    if (antes.OndaDeFundo is not null && depois.OndaDeFundo is null) fundoVoltou++;
                    if (antes.Carga != CargaDaParanoia.Nenhuma && depois.Carga == CargaDaParanoia.Nenhuma) cargaZerada++;
                    if (r.Transicoes.Count > 0)
                    {
                        disparos++;
                        disparosNoEpisodio++;
                        Afirmar.Verdadeiro(disparosNoEpisodio <= limite, $"{onde}: a onda acaba em no máximo {limite} disparos");
                    }
                }
                // Invariante 25, como em InvariantesTestes: a conta recomeça com a onda da frente nova, a de um uso (o baseado
                // por conta própria) ou a de fundo que voltou.
                bool comecouOUso = r.Transicoes.Any(t => t.Para == Estado.Using && t.De != Estado.Using);
                if (depois.Onda is { } nova && (comecouOUso || antes.Onda is null || antes.Onda.Tipo != nova.Tipo))
                {
                    if (comecouOUso) fumou++;
                    disparosNoEpisodio = 0;
                    limite = 2 + nova.Nivel;
                }
                foreach (AgendarDecisao agenda in r.Efeitos.OfType<AgendarDecisao>())
                {
                    PerfilDeEnergia p = Maquina.PerfilEfetivo(depois, cfg);
                    (TimeSpan minimo, TimeSpan maximo) = depois.Estado switch
                    {
                        Estado.Resting => (p.DescansoMinimo, p.DescansoMaximo),
                        Estado.Climbing => (p.TempoNaParedeMinimo, p.TempoNaParedeMaximo),
                        Estado.Hanging => (p.TempoPenduradoMinimo, p.TempoPenduradoMaximo),
                        _ => (p.DecisaoMinima, p.DecisaoMaxima),
                    };
                    TimeSpan piso = cfg.IntervaloDeAcomodacao;
                    Afirmar.Verdadeiro(agenda.Atraso >= piso && agenda.Atraso >= minimo && agenda.Atraso <= (maximo < piso ? piso : maximo),
                        $"{onde}: R11, atraso {agenda.Atraso} fora da faixa efetiva {minimo}–{maximo} (piso {piso})");
                }
                if (depois.Gesto != Gesto.Nenhum) Afirmar.Igual(Estado.Idle, depois.Estado, $"{onde}: gesto {depois.Gesto} só em IDLE");
                MovimentoTestes.ConferirApoio(depois, onde);
            }

            sim.AoResultado = Conferir;
            while (sim.AgoraMs < 60 * 60 * 1000)
            {
                if (sim.Estado.Onda is null && rnd.Next(2) == 0)
                {
                    var onda = new EstadoDaOnda((Onda)rnd.Next(Enum.GetValues<Onda>().Length), FaseDaOnda.Subida, rnd.Next(1, 4), 0);
                    onda = onda with { Pior = onda.Nivel };
                    episodios++;
                    disparosNoEpisodio = 0;
                    limite = 2 + onda.Nivel;
                    sim = Semear(sim, onda, s => s.AoResultado = Conferir, comACara: sim.Estado.Estado is not (Estado.Resting or Estado.Reacting));
                }
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(5, 60)));
                foreach (Evento e in Interacao(rnd, sim.Estado)) sim.Aplicar(e);
            }
        }
        Console.WriteLine($"         {eventos} eventos em 6 horas simuladas; {episodios} ondas semeadas, {fumou} baseados por conta própria, {disparos} disparos; {comOnda} eventos com onda; "
            + $"{fundoVoltou} disparos com a de fundo de volta e {cargaZerada} com a carga zerada");
        Afirmar.Verdadeiro(episodios >= 30 && disparos >= 3 * episodios / 2, $"ondas e disparos suficientes: {episodios} ondas, {disparos} disparos");
        Afirmar.Verdadeiro(fundoVoltou > 0 && cargaZerada > 0, $"a regra do fundo e a da carga conferidas: {fundoVoltou} e {cargaZerada} disparos");
    }

    /// <summary>Se a onda existe e é de substância, pela transcrição do desenho (4.2), escrita à parte do núcleo.</summary>
    private static bool DeSubstancia(EstadoDaOnda? onda) => onda is not null && TabelasDoDesenho.Esperada(onda.Tipo).DeSubstancia;

    /// <summary>
    /// A onda da frente e a de fundo depois de um disparo que vale (4.5), escrito aqui à parte do núcleo: a subida vira o pico;
    /// o pico desce um nível; o pico do nível 1 vira a queda, se a onda tem queda (pela transcrição do desenho); senão, e na
    /// queda, a onda da frente acaba e a de fundo volta inteira para a frente, com o fundo vazio.
    /// </summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo) DepoisDoDisparo(EstadoDaOnda frente, EstadoDaOnda? fundo) => frente.Fase switch
    {
        FaseDaOnda.Subida => (frente with { Fase = FaseDaOnda.Pico }, fundo),
        FaseDaOnda.Pico when frente.Nivel > 1 => (frente with { Nivel = frente.Nivel - 1 }, fundo),
        FaseDaOnda.Pico when TabelasDoDesenho.Esperada(frente.Tipo).QuedaBase is not null => (frente with { Fase = FaseDaOnda.Queda, Nivel = 1 }, fundo),
        _ => (fundo, null),
    };

    /// <summary>
    /// O estado sem a cara, as ondas, o temporizador delas, a carga do episódio e o sinal pontual: o que o disparo da onda não
    /// pode mudar. A onda de fundo volta quando a da frente acaba, e a carga da paranoia zera quando não sobra onda de
    /// substância (o baseado por conta própria começa uma).
    /// </summary>
    private static EstadoDoNucleo SemAOnda(EstadoDoNucleo s)
        => s with { Expressao = Expressao.Neutro, Onda = null, OndaDeFundo = null, Carga = CargaDaParanoia.Nenhuma, GeracaoDaOnda = 0, OndaAgendada = false, Sinal = Sinal.Nenhum };

    // ---------------------------------------------------------------- perfil e física em vigor

    // Tabela 4.3, em cada energia, onda, fase e nível: o perfil efetivo aplica os percentuais da fase com as regras do
    // desenho (intervalos truncados em ms, pesos arredondados e nunca zerados se eram positivos, altura do pulo em DIP,
    // foguete da fase ou o do perfil) e, com a velocidade reduzida, alonga o tempo na parede e o pendurado por
    // 100/Velocidade (L16), para a subida mais lenta não ser cortada pela agenda. Sem onda, ou com a chave desligada, é a
    // mesma instância do perfil de energia. Alguns valores conferidos à mão.
    [Teste]
    public static void PerfilEfetivo_BateComATabela()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        EstadoDoNucleo inicial = EstadoDoNucleo.Inicial(1);
        foreach (NivelDeEnergia energia in Enum.GetValues<NivelDeEnergia>())
        {
            PerfilDeEnergia baseDoNivel = cfg.Perfil(energia);
            EstadoDoNucleo s = inicial with { Preferencias = Preferencias.Padrao with { Energia = energia } };
            Afirmar.Verdadeiro(ReferenceEquals(baseDoNivel, Maquina.PerfilEfetivo(s, cfg)), $"{energia}: sem onda, o mesmo perfil");
            foreach (Onda onda in Enum.GetValues<Onda>())
            {
                DadosDaOnda dados = TabelaDoTamagotchi.DaOnda(onda);
                foreach (EstadoDaOnda fase in FasesDeTodosOsNiveis(onda))
                {
                    EstadoDoNucleo comOnda = s with { Onda = fase };
                    PerfilDaOnda p = dados.Perfil(fase.Fase, fase.Nivel);
                    string caso = $"{energia}, {onda} {fase.Fase} {fase.Nivel}";
                    Afirmar.Igual(Esperado(baseDoNivel, p), Maquina.PerfilEfetivo(comOnda, cfg), caso);
                    Afirmar.Verdadeiro(ReferenceEquals(baseDoNivel, Maquina.PerfilEfetivo(comOnda, cfg with { Tamagotchi = false })), $"{caso}: com a chave desligada, a onda não vale");
                    Afirmar.Igual(TabelasDoDesenho.Descrever(p), TabelasDoDesenho.Descrever(Afirmar.NaoNulo(Maquina.PerfilDaFase(comOnda, cfg), caso)), $"{caso}: o perfil da fase");
                }
            }
        }
        Afirmar.Nulo(Maquina.PerfilDaFase(inicial with { Onda = new(Onda.Bebado, FaseDaOnda.Pico, 1, 1) }, cfg with { Tamagotchi = false }), "chave desligada: sem fase");

        // À mão: elétrico no pico do nível 3, energia média (decisão 8–20 s, descanso 30–90 s, pesos 4/2/1/3/3/2, pulo 50–100).
        PerfilDeEnergia eletrico = Maquina.PerfilEfetivo(inicial with { Onda = new(Onda.Eletrico, FaseDaOnda.Pico, 3, 3) }, cfg);
        Afirmar.Igual((TimeSpan.FromSeconds(1.6), TimeSpan.FromSeconds(4)), (eletrico.DecisaoMinima, eletrico.DecisaoMaxima), "elétrico 3: decisão a 20%");
        Afirmar.Igual((TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9)), (eletrico.DescansoMinimo, eletrico.DescansoMaximo), "elétrico 3: descanso a 10%");
        Afirmar.Igual((8, 4, 2, 1, 5, 4), (eletrico.PesoAndar, eletrico.PesoEscalar, eletrico.PesoPular, eletrico.PesoDescansar, eletrico.PesoGesto, eletrico.PesoTrocarExpressao), "elétrico 3: pesos (descansar a 5% fica em 1)");
        Afirmar.Igual((70, 140, 80), (eletrico.AlturaDoPuloMinima, eletrico.AlturaDoPuloMaxima, eletrico.ChanceDoFoguete), "elétrico 3: pulo a 140% e foguete 80");
        Afirmar.Igual((TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(35)), (eletrico.TempoNaParedeMinimo, eletrico.TempoNaParedeMaximo), "elétrico 3, mais rápido: o tempo na parede não muda");

        // À mão: bêbado no pico do nível 3 (velocidade 60%): parede 15–35 s e pendurado 3–8 s ficam 100/60 mais longos (L16).
        PerfilDeEnergia bebado = Maquina.PerfilEfetivo(inicial with { Onda = new(Onda.Bebado, FaseDaOnda.Pico, 3, 3) }, cfg);
        Afirmar.Igual((TimeSpan.FromSeconds(25), TimeSpan.FromMilliseconds(58333)), (bebado.TempoNaParedeMinimo, bebado.TempoNaParedeMaximo), "bêbado 3: tempo na parede × 100/60");
        Afirmar.Igual((TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(13333)), (bebado.TempoPenduradoMinimo, bebado.TempoPenduradoMaximo), "bêbado 3: tempo pendurado × 100/60");
        Afirmar.Igual((5, 5), (bebado.PesoAndar, bebado.ChanceDoFoguete), "bêbado 3: andar a 130% e foguete 5");

        // À mão: tonto no pico (escalar e pular a 0%): nunca escala nem pula.
        PerfilDeEnergia tonto = Maquina.PerfilEfetivo(inicial with { Onda = new(Onda.Tonto, FaseDaOnda.Pico, 1, 1) }, cfg);
        Afirmar.Igual((0, 0, 0), (tonto.PesoEscalar, tonto.PesoPular, tonto.ChanceDoFoguete), "tonto: sem escalar, pular nem foguete");
        Afirmar.Igual((TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10)), (tonto.DecisaoMinima, tonto.DecisaoMaxima), "tonto: decisão a 50%");
    }

    /// <summary>As regras do desenho (4.3) aplicadas a um perfil de energia, escritas aqui à parte.</summary>
    private static PerfilDeEnergia Esperado(PerfilDeEnergia e, PerfilDaOnda p)
    {
        static TimeSpan Pct(TimeSpan t, int pct) => TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * pct / 100);
        static int Peso(int w, int pct) => w <= 0 || pct <= 0 ? 0 : Math.Max(1, (w * pct + 50) / 100);
        static int Dip(int d, int pct) => Math.Max(1, (d * pct + 50) / 100);
        TimeSpan Lento(TimeSpan t) => p.Velocidade >= 100 ? t : TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * 100 / p.Velocidade);
        return e with
        {
            DecisaoMinima = Pct(e.DecisaoMinima, p.Intervalo),
            DecisaoMaxima = Pct(e.DecisaoMaxima, p.Intervalo),
            DescansoMinimo = Pct(e.DescansoMinimo, p.Descanso),
            DescansoMaximo = Pct(e.DescansoMaximo, p.Descanso),
            PesoAndar = Peso(e.PesoAndar, p.Andar),
            PesoEscalar = Peso(e.PesoEscalar, p.Escalar),
            PesoPular = Peso(e.PesoPular, p.Pular),
            PesoDescansar = Peso(e.PesoDescansar, p.Descansar),
            PesoGesto = Peso(e.PesoGesto, p.Gesticular),
            PesoTrocarExpressao = Peso(e.PesoTrocarExpressao, p.TrocarCara),
            // Atravessar é andar até o outro monitor (Fase 5, passo P13): o percentual de andar da fase.
            PesoAtravessar = Peso(e.PesoAtravessar, p.Andar),
            PesoIrAoOutroMonitor = Peso(e.PesoIrAoOutroMonitor, p.Andar),
            AlturaDoPuloMinima = Dip(e.AlturaDoPuloMinima, p.AlturaDoPulo),
            AlturaDoPuloMaxima = Dip(e.AlturaDoPuloMaxima, p.AlturaDoPulo),
            ChanceDoFoguete = p.ChanceDoFoguete ?? e.ChanceDoFoguete,
            TempoNaParedeMinimo = Lento(e.TempoNaParedeMinimo),
            TempoNaParedeMaximo = Lento(e.TempoNaParedeMaximo),
            TempoPenduradoMinimo = Lento(e.TempoPenduradoMinimo),
            TempoPenduradoMaximo = Lento(e.TempoPenduradoMaximo),
        };
    }

    /// <summary>Todas as fases que uma onda pode ter: a subida e o pico em cada nível, e a queda, se houver.</summary>
    private static IEnumerable<EstadoDaOnda> FasesDeTodosOsNiveis(Onda onda)
    {
        foreach (int nivel in new[] { 1, 2, 3 })
        {
            yield return new EstadoDaOnda(onda, FaseDaOnda.Subida, nivel, nivel);
            yield return new EstadoDaOnda(onda, FaseDaOnda.Pico, nivel, nivel);
        }
        if (TabelaDoTamagotchi.DaOnda(onda).Queda is not null)
            foreach (int pior in new[] { 1, 2, 3 }) yield return new EstadoDaOnda(onda, FaseDaOnda.Queda, 1, pior);
    }

    // D10 (exceção documentada ao invariante 12): com onda, a física em vigor só muda as velocidades de andar, escalar e
    // pendurar, pelo percentual da fase, sempre entre 50 e 200%; gravidade, queda máxima, quique, foguete, agarrar e o
    // resto ficam os mesmos. Sem onda, na velocidade 100 ou com a chave desligada, é a mesma instância da configuração.
    [Teste]
    public static void FisicaEfetiva_SoAsTresVelocidades()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        ParametrosDeMovimento f = cfg.Fisica;
        EstadoDoNucleo inicial = EstadoDoNucleo.Inicial(1);
        Afirmar.Verdadeiro(ReferenceEquals(f, Maquina.FisicaEfetiva(inicial, cfg)), "sem onda, a mesma física");
        var vistas = new HashSet<int>();
        foreach (Onda onda in Enum.GetValues<Onda>())
        {
            foreach (EstadoDaOnda fase in FasesDeTodosOsNiveis(onda))
            {
                EstadoDoNucleo s = inicial with { Onda = fase };
                int v = TabelaDoTamagotchi.DaOnda(onda).Perfil(fase.Fase, fase.Nivel).Velocidade;
                vistas.Add(v);
                string caso = $"{onda} {fase.Fase} {fase.Nivel} ({v}%)";
                ParametrosDeMovimento efetiva = Maquina.FisicaEfetiva(s, cfg);
                Afirmar.Igual(f with
                {
                    VelocidadeAndando = 90.0 * v / 100,
                    VelocidadeEscalando = 110.0 * v / 100,
                    VelocidadePendurado = 80.0 * v / 100,
                }, efetiva, $"{caso}: só as três velocidades");
                Afirmar.Verdadeiro(efetiva.VelocidadeAndando is >= 45 and <= 180, $"{caso}: entre 50 e 200% da caminhada");
                if (v == 100) Afirmar.Verdadeiro(ReferenceEquals(f, efetiva), $"{caso}: a 100%, a mesma física");
                Afirmar.Verdadeiro(ReferenceEquals(f, Maquina.FisicaEfetiva(s, cfg with { Tamagotchi = false })), $"{caso}: com a chave desligada, a onda não vale");
            }
        }
        Afirmar.Verdadeiro(vistas.Min() == 50 && vistas.Max() == 200, $"as tabelas vão de 50 a 200% ({string.Join(", ", vistas.Order())})");
    }

    // As velocidades da fase valem nos cinco lugares do passo físico (desenho 3.7): andando, escalando, pendurado no cipó
    // por conta própria e preso pelo usuário passeando pela parede e pelo cipó. A 200% (elétrico 3) e a 50% (chapado 3),
    // sem cambaleio, a âncora fina anda exatamente velocidade × percentual por passo, só no eixo do movimento; o foguete
    // não é afetado (fica de fora dos casos).
    [Teste]
    public static void VelocidadesDaFase_NosCincoLugaresDoPassoFisico()
    {
        foreach ((Onda onda, int percentual) in new[] { (Onda.Eletrico, 200), (Onda.Chapado, 50), (Onda.Satisfeito, 100) })
        {
            PerfilDaOnda fase = TabelaDoTamagotchi.DaOnda(onda).Perfil(FaseDaOnda.Pico, 3);
            Afirmar.Igual((percentual, 0), (fase.Velocidade, fase.Cambaleio), $"{onda} 3: a velocidade da fase, sem cambaleio");
            foreach (string lugar in new[] { "andando", "escalando", "pendurado", "preso na parede", "preso no cipó" })
            {
                string caso = $"{onda} ({percentual}%), {lugar}";
                (SimuladorDeTempo sim, double dipPorSegundo, bool noX) = Chegar(lugar);
                SimuladorDeTempo semeado = Semear(sim, new EstadoDaOnda(onda, FaseDaOnda.Pico, 3, 3));
                int passos = lugar.StartsWith("preso", StringComparison.Ordinal) ? 10 : 20;
                (double dx, double dy) = Deslocamento(semeado, passos, caso);
                double esperado = passos * dipPorSegundo * percentual / 100 / 60;
                Afirmar.Aproximado(esperado, Math.Abs(noX ? dx : dy), 1e-9, $"{caso}: {passos} passos a {dipPorSegundo * percentual / 100} DIP/s");
                Afirmar.Igual(0.0, noX ? dy : dx, $"{caso}: só no eixo do movimento");
            }
        }
    }

    /// <summary>
    /// Leva o personagem, pela própria máquina, a um dos cinco lugares do passo físico, no começo do movimento, com folga
    /// para 20 passos a 200%. Devolve o simulador, a velocidade-base daquele passo (DIP/s) e se ele anda no eixo X.
    /// </summary>
    private static (SimuladorDeTempo Sim, double DipPorSegundo, bool NoX) Chegar(string lugar)
    {
        ParametrosDeMovimento f = new ConfiguracaoDoNucleo().Fisica;
        static bool Livre(EstadoDoNucleo s) => !s.Movimento.Agarrado && !s.PresoPeloUsuario && !s.Movimento.Foguete;
        switch (lugar)
        {
            case "andando":
                return (Achar(AcoesAutonomas.Andar, s => s.Estado == Estado.Walking && Folga(s) >= 120), f.VelocidadeAndando, true);
            case "escalando":
                return (Achar(AcoesAutonomas.Escalar, s => s.Estado == Estado.Climbing && Livre(s) && s.Movimento.SentidoVertical < 0), f.VelocidadeEscalando, false);
            case "pendurado":
                return (Achar(AcoesAutonomas.Escalar, s => s.Estado == Estado.Hanging && Livre(s) && Folga(s) >= 120), f.VelocidadePendurado, true);
            case "preso na parede":
            {
                SimuladorDeTempo sim = SoltarEm(sup => new PontoPx(sup.Direita - 20, 600 - 30));
                sim.Esta(Estado.Climbing, "grudado na parede");
                sim.Avancar(TimeSpan.FromMinutes(10), s => !s.Movimento.Agarrado);
                Afirmar.Verdadeiro(sim.Estado.Estado == Estado.Climbing && sim.Estado.PresoPeloUsuario && !sim.Estado.Movimento.Agarrado, "preso, passeando pela parede");
                return (sim, f.VelocidadeEscalando, false);
            }
            default:
            {
                SimuladorDeTempo sim = SoltarEm(sup => new PontoPx(960, sup.Teto + 12 - 30));
                sim.Esta(Estado.Hanging, "agarrado ao cipó");
                sim.Avancar(TimeSpan.FromMinutes(10), s => !s.Movimento.Agarrado);
                Afirmar.Verdadeiro(sim.Estado.Estado == Estado.Hanging && sim.Estado.PresoPeloUsuario && !sim.Estado.Movimento.Agarrado, "preso, passeando pelo cipó");
                return (sim, f.VelocidadePendurado, true);
            }
        }
    }

    /// <summary>A primeira semente em que a agenda, só com as ações dadas, leva o personagem à condição em até 5 minutos.</summary>
    private static SimuladorDeTempo Achar(AcoesAutonomas acoes, Func<EstadoDoNucleo, bool> condicao)
    {
        for (ulong semente = 1; semente <= 60; semente++)
        {
            var sim = new SimuladorDeTempo(Ligado() with { Acoes = acoes }, semente, TopologiasDeExemplo.UmMonitor);
            sim.Avancar(TimeSpan.FromMinutes(5), condicao);
            if (condicao(sim.Estado)) return sim;
        }
        Afirmar.Falhar($"nenhuma semente chegou à condição com {acoes}");
        return null!;
    }

    /// <summary>Solta o personagem, pela mão do usuário, com a âncora 30 px abaixo do ponto dado pela área útil.</summary>
    private static SimuladorDeTempo SoltarEm(Func<Superficies, PontoPx> alvo)
    {
        var sim = new SimuladorDeTempo(Ligado(), 6, TopologiasDeExemplo.UmMonitor);
        Posicionamento l = sim.Estado.Lugar!;
        PontoPx destino = alvo(Sup(sim.Estado));
        foreach (Evento e in new Evento[] { new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)), new DragStart(), new DragMove(destino), new DragEnd(destino) })
            sim.Aplicar(e);
        return sim;
    }

    /// <summary>Quanto a âncora fina anda em <paramref name="passos"/> passos do relógio, conferindo que o estado não muda.</summary>
    private static (double DX, double DY) Deslocamento(SimuladorDeTempo sim, int passos, string caso)
    {
        Estado estado = sim.Estado.Estado;
        EstadoDoMovimento antes = sim.Estado.Movimento;
        for (int i = 0; i < passos; i++)
        {
            Afirmar.Igual(1, sim.Passos(1), $"{caso}: o relógio corre no passo {i + 1}");
            Afirmar.Igual(estado, sim.Estado.Estado, $"{caso}: continua em {estado} no passo {i + 1}");
        }
        return (sim.Estado.Movimento.X - antes.X, sim.Estado.Movimento.Y - antes.Y);
    }

    private static Superficies Sup(EstadoDoNucleo s)
    {
        Posicionamento l = Afirmar.NaoNulo(s.Lugar, "lugar");
        return Superficies.Do(Afirmar.NaoNulo(s.Topologia, "topologia"), l.Monitor, l.Tamanho);
    }

    /// <summary>Espaço livre à frente, em pixels, até o limite da superfície no sentido em que ele está virado.</summary>
    private static double Folga(EstadoDoNucleo s)
    {
        Superficies sup = Sup(s);
        return s.Direcao == Direcao.Direita ? sup.Direita - s.Movimento.X : s.Movimento.X - sup.Esquerda;
    }

    // ---------------------------------------------------------------- cambaleio (4.9)

    // A onda triangular de 48 passos (0,8 s), só com soma, subtração, multiplicação e divisão: com amplitude 0 anda reto;
    // a 120%, vai de −0,2 (um pequeno recuo) no começo da volta a 2,2 no meio; é simétrica e, numa volta inteira, anda em
    // média o mesmo que sem cambaleio.
    [Teste]
    public static void FatorDoCambaleio_TrianguloDe48Passos()
    {
        Afirmar.Igual(48, Maquina.PassosDoCambaleio, "uma volta");
        foreach (long passo in new long[] { 0, 1, 23, 24, 47, 1000, long.MaxValue }) Afirmar.Igual(1.0, Maquina.FatorDoCambaleio(passo, 0), $"sem cambaleio, passo {passo}");
        (long Passo, double Fator)[] valores = [(0, -0.2), (1, -0.1), (2, 0.0), (12, 1.0), (24, 2.2), (36, 1.0), (47, -0.1), (48, -0.2), (72, 2.2), (4800, -0.2)];
        foreach ((long passo, double fator) in valores) Afirmar.Aproximado(fator, Maquina.FatorDoCambaleio(passo, 120), 1e-12, $"120%, passo {passo}");
        Afirmar.Aproximado(0.4, Maquina.FatorDoCambaleio(0, 60), 1e-12, "60%: de 0,4");
        Afirmar.Aproximado(1.6, Maquina.FatorDoCambaleio(24, 60), 1e-12, "60%: a 1,6");
        Afirmar.Aproximado(0.7, Maquina.FatorDoCambaleio(0, 30), 1e-12, "30% (a ressaca): de 0,7");
        foreach (int amplitude in new[] { 30, 60, 90, 100, 120 })
        {
            double soma = 0;
            for (long k = 0; k < 48; k++)
            {
                soma += Maquina.FatorDoCambaleio(k, amplitude);
                Afirmar.Igual(Maquina.FatorDoCambaleio(24 + (k % 24), amplitude), Maquina.FatorDoCambaleio(24 - (k % 24), amplitude), $"{amplitude}%: simétrico em volta do meio ({k})");
            }
            Afirmar.Aproximado(48.0, soma, 1e-9, $"{amplitude}%: numa volta, o mesmo caminho que sem cambaleio");
        }
    }

    // O bêbado no nível 3 (60% da velocidade, cambaleio de 120%) cambaleia andando: cada passo é o da caminhada lenta vezes
    // a onda triangular, com recuos e paradas, sempre com os pés no chão e dentro dos limites da superfície; numa volta de
    // 48 passos, anda o mesmo que sem cambalear. Duas execuções com a mesma semente dão a mesma trilha.
    [Teste]
    public static void Bebado3_CambaleiaDeterministicoNoChao()
    {
        string Trilha()
        {
            SimuladorDeTempo sim = Achar(AcoesAutonomas.Andar, s => s.Estado == Estado.Walking && Folga(s) >= 200 && s.Movimento.Restante >= 200);
            SimuladorDeTempo bebado = Semear(sim, new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 3, 3));
            Superficies sup = Sup(bebado.Estado);
            int sentido = bebado.Estado.Direcao == Direcao.Direita ? 1 : -1;
            const double PassoLento = 90.0 * 60 / 100 / 60;
            var trilha = new StringBuilder();
            int recuos = 0, parados = 0, passos = 0;
            double inicioDaVolta = double.NaN;
            while (passos < 400)
            {
                double antes = bebado.Estado.Movimento.X;
                double restanteAntes = bebado.Estado.Movimento.Restante;
                Afirmar.Igual(1, bebado.Passos(1), "o relógio corre andando");
                EstadoDoNucleo s = bebado.Estado;
                if (s.Estado != Estado.Walking) break;
                passos++;
                long k = s.Passos % 48;
                double distancia = Math.Abs(k - 24);
                double dx = (s.Movimento.X - antes) * sentido;
                Afirmar.Aproximado(PassoLento * (1 + 1.2 * (1 - distancia / 12)), dx, 1e-9, $"passo {passos} (volta {k}): o passo lento vezes a onda triangular");
                // O percurso que falta diminui pelo passo, com o sinal: o recuo devolve distância (4.9).
                Afirmar.Aproximado(dx, restanteAntes - s.Movimento.Restante, 1e-9, $"passo {passos} (volta {k}): o percurso restante anda com o passo, com o sinal");
                Afirmar.Igual(sup.Chao, s.Lugar!.Ancora.Y, $"passo {passos}: os pés no chão");
                Afirmar.Igual((double)sup.Chao, s.Movimento.Y, $"passo {passos}: a âncora fina também");
                Afirmar.Verdadeiro(s.Lugar.Ancora.X >= sup.Esquerda && s.Lugar.Ancora.X <= sup.Direita, $"passo {passos}: dentro dos limites");
                if (dx < -1e-12) recuos++;
                if (Math.Abs(dx) <= 1e-12) parados++;
                if (k == 0)
                {
                    if (!double.IsNaN(inicioDaVolta))
                        Afirmar.Aproximado(48 * PassoLento, (antes - inicioDaVolta) * sentido, 1e-9, $"passo {passos}: uma volta inteira anda o mesmo que sem cambalear");
                    inicioDaVolta = antes;
                }
                trilha.Append(s.Lugar.Ancora.X).Append(';');
            }
            Afirmar.Verdadeiro(passos >= 96, $"andou {passos} passos (pelo menos duas voltas)");
            Afirmar.Verdadeiro(recuos >= 4 && parados >= 2, $"cambaleou: {recuos} recuos e {parados} paradas em {passos} passos");
            return trilha.ToString();
        }
        Afirmar.Igual(Trilha(), Trilha(), "determinístico: a mesma semente dá a mesma trilha");
    }

    // O recuo do cambaleio nunca passa da lateral de trás: encostado na lateral esquerda e andando para a direita, com o
    // primeiro passo no começo da volta (−0,2), ele fica na lateral, sem sinal de parede nem outra transição, e depois anda.
    [Teste]
    public static void Cambaleio_RecuoNaoPassaDaLateralDeTras()
    {
        var sim = new SimuladorDeTempo(Ligado() with { Acoes = AcoesAutonomas.Andar }, 3, TopologiasDeExemplo.UmMonitor);
        Posicionamento l = sim.Estado.Lugar!;
        var naLateral = new PontoPx(0, l.Ancora.Y - 30);
        foreach (Evento e in new Evento[] { new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)), new DragStart(), new DragMove(naLateral), new DragEnd(naLateral) })
            sim.Aplicar(e);
        Superficies sup = Sup(sim.Estado);
        sim.Esta(Estado.Idle, "no chão");
        Afirmar.Igual(new PontoPx(sup.Esquerda, sup.Chao), sim.Estado.Lugar!.Ancora, "encostado na lateral esquerda");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Estado == Estado.Walking);
        sim.Esta(Estado.Walking, "começa a andar");
        Afirmar.Igual(Direcao.Direita, sim.Estado.Direcao, "para longe da lateral");

        // O próximo passo do relógio é o 48: começo da volta.
        SimuladorDeTempo bebado = sim.Semeado(s => s with { Onda = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 3, 3), Passos = 47 });
        List<Aplicado> aplicados = Registrar(bebado);
        Afirmar.Igual(3, bebado.Passos(3), "três passos: recuo, recuo e parado");
        foreach (Aplicado a in aplicados) Afirmar.Sequencia([], a.Resultado.Transicoes, $"passo {a.Resultado.Estado.Passos}: sem sinal de parede nem outra transição");
        bebado.Esta(Estado.Walking, "continua andando");
        Afirmar.Igual((double)sup.Esquerda, bebado.Estado.Movimento.X, "o recuo para na lateral de trás");
        Afirmar.Igual(new PontoPx(sup.Esquerda, sup.Chao), bebado.Estado.Lugar!.Ancora, "a janela também");
        bebado.Passos(1);
        Afirmar.Verdadeiro(bebado.Estado.Movimento.X > sup.Esquerda, "depois anda para a frente");
    }

    // ---------------------------------------------------------------- caras e gestos da fase

    // Com uma onda que não muda o comportamento (todos os percentuais em 100) e as caras e os gestos do pico do bêbado,
    // dez minutos de agenda livre com interações sorteadas, na configuração do aplicativo sem o baseado por conta própria
    // (que começaria uma onda na execução sem onda): evento a evento, as mesmas
    // transições (a não ser o nome do gesto), os mesmos efeitos (a não ser o agendamento da onda) e o mesmo estado, inclusive
    // o gerador, a não ser a cara e o gesto. Isto é, cada sorteio de cara e de gesto continua sendo um sorteio só (as
    // referências gravadas não mudam), com ou sem a emoção dominante. Com a onda, as trocas de cara (no chão, no
    // esconderijo e preso pelo usuário) só dão caras da fase e os gestos só os da fase, inclusive os novos; ao acordar,
    // vale a cara da fase; a dominante não entra nas trocas.
    [Teste]
    public static void CarasEGestosDaFase_ComOsMesmosSorteios()
    {
        // Sem o baseado por conta própria (FumarBaseado; BaseadoPorContaPropriaTestes): ele começaria uma onda de verdade na
        // execução sem onda.
        ConfiguracaoDoNucleo cfg = Ligado() with { TabelaDeOndas = OndaNeutra, Acoes = AcoesAutonomas.Todas };
        PerfilDaOnda bebado = TabelaDoTamagotchi.DaOnda(Onda.Bebado).Perfil(FaseDaOnda.Pico, 1);
        var carasDaFase = bebado.Caras.Select(c => c.Cara).ToHashSet();
        var gestosDaFase = bebado.Gestos.Select(g => g.Gesto).ToHashSet();
        var carasVistas = new Dictionary<Expressao, int>();
        var gestosVistos = new Dictionary<Gesto, int>();
        var mestre = new Random(2028);
        long eventos = 0, trocas = 0, gestos = 0, espiando = 0, presos = 0, acordou = 0;
        for (int n = 0; n < 30; n++)
        {
            int semente = mestre.Next();
            Expressao? dominante = n % 3 == 0 ? Expressoes.DeHumor[n % 14] : null;
            string Onde(int i, Evento e) => $"semente {semente} (sequência {n}, dominante {dominante?.ToString() ?? "automática"}), {i}º evento ({e})";
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            Preferencias preferencias = Preferencias.Padrao with { EmocaoDominante = dominante };
            var semOnda = new SimuladorDeTempo(cfg, (ulong)semente, topologia, preferencias);
            SimuladorDeTempo comOnda = new SimuladorDeTempo(cfg, (ulong)semente, topologia, preferencias)
                .Semeado(s => s with { Onda = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 1, 1) });
            List<Aplicado> a = Registrar(semOnda), b = Registrar(comOnda);
            semOnda.Aplicar(new ItemEffectTimer(0));
            comOnda.Aplicar(new ItemEffectTimer(0));

            // Interações espaçadas, para a agenda também trocar a cara, descansar e acordar sozinha.
            var rnd = new Random(semente);
            while (semOnda.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(10, 120));
                semOnda.Avancar(espera);
                comOnda.Avancar(espera);
                foreach (Evento e in Interacao(rnd, semOnda.Estado))
                {
                    semOnda.Aplicar(e);
                    comOnda.Aplicar(e);
                }
            }

            Afirmar.Igual(a.Count, b.Count, $"semente {semente}: o mesmo número de eventos aplicados");
            for (int i = 0; i < a.Count; i++)
            {
                Aplicado sem = a[i], com = b[i];
                string onde = Onde(i, sem.Evento);
                Afirmar.Igual(sem.Evento, com.Evento, $"{onde}: o mesmo evento");
                Afirmar.Sequencia(sem.Resultado.Transicoes.Select(SemONomeDoGesto), com.Resultado.Transicoes.Select(SemONomeDoGesto), $"{onde}: as mesmas transições");
                Afirmar.Sequencia(sem.Resultado.Efeitos, com.Resultado.Efeitos.Where(e => e is not (AgendarOnda or CancelarOnda)), $"{onde}: os mesmos efeitos");
                Afirmar.Igual(SoOComportamento(sem.Resultado.Estado), SoOComportamento(com.Resultado.Estado), $"{onde}: o mesmo estado, a não ser a cara e o gesto");

                EstadoDoNucleo antes = com.Antes, depois = com.Resultado.Estado;
                if (depois.Gesto != Gesto.Nenhum && antes.Gesto == Gesto.Nenhum)
                {
                    gestos++;
                    gestosVistos[depois.Gesto] = gestosVistos.GetValueOrDefault(depois.Gesto) + 1;
                    Afirmar.Verdadeiro(gestosDaFase.Contains(depois.Gesto), $"{onde}: gesto {depois.Gesto} da fase");
                    Afirmar.Verdadeiro(sem.Resultado.Estado.Gesto is >= Gesto.Espiar and <= Gesto.Brincar, $"{onde}: sem onda, os gestos de sempre ({sem.Resultado.Estado.Gesto})");
                }
                if (com.Evento is not AutonomyTimer) continue;
                bool trocouACara = antes.Estado == Estado.Idle && depois.Estado == Estado.Idle && depois.Gesto == Gesto.Nenhum && com.Resultado.Transicoes.Count == 0;
                bool espiou = com.Resultado.Transicoes.Any(t => t.Regra.EndsWith("espia com outra cara", StringComparison.Ordinal));
                bool olhou = com.Resultado.Transicoes.Any(t => t.Regra.EndsWith("fica e olha em volta", StringComparison.Ordinal));
                if (trocouACara || espiou || olhou)
                {
                    if (trocouACara) trocas++;
                    if (espiou) espiando++;
                    if (olhou) presos++;
                    carasVistas[depois.Expressao] = carasVistas.GetValueOrDefault(depois.Expressao) + 1;
                    Afirmar.Verdadeiro(carasDaFase.Contains(depois.Expressao), $"{onde}: cara {depois.Expressao} da fase");
                }
                if (antes.Estado == Estado.Resting && depois.Estado == Estado.Idle)
                {
                    acordou++;
                    Afirmar.Igual(Expressao.Bebado, depois.Expressao, $"{onde}: acorda com a cara da fase");
                    Afirmar.Igual(dominante ?? Expressao.Neutro, sem.Resultado.Estado.Expressao, $"{onde}: sem onda, acorda com a de base");
                }
            }
            eventos += a.Count;
        }
        string Contagem<T>(Dictionary<T, int> vezes) where T : notnull => string.Join(", ", vezes.OrderByDescending(v => v.Value).Select(v => $"{v.Key}={v.Value}"));
        Console.WriteLine($"         {eventos} eventos em 30 sementes; {trocas} trocas de cara, {espiando} no esconderijo, {presos} presos, {gestos} gestos, {acordou} ao acordar");
        Console.WriteLine($"         caras: {Contagem(carasVistas)}; gestos: {Contagem(gestosVistos)}");
        Afirmar.Sequencia(carasDaFase.Order(), carasVistas.Keys.Order(), "todas as caras da fase aparecem, e só elas");
        Afirmar.Sequencia(gestosDaFase.Order(), gestosVistos.Keys.Order(), "todos os gestos da fase aparecem, inclusive os novos, e só eles");
        Afirmar.Verdadeiro(trocas >= 20 && gestos >= 20 && espiando >= 3 && presos >= 3 && acordou >= 3, "os casos aconteceram");
        // Pelos pesos da tabela 4.4: a cara bêbada (4 de 8) sai em metade dos sorteios, e o soluço (3 de 5), em 60%.
        int sorteiosDeCara = carasVistas.Values.Sum();
        Afirmar.Verdadeiro(carasVistas[Expressao.Bebado] >= sorteiosDeCara * 4 / 10 && carasVistas.Values.Max() == carasVistas[Expressao.Bebado],
            $"a cara de maior peso é a mais sorteada ({Contagem(carasVistas)})");
        Afirmar.Verdadeiro(gestosVistos[Gesto.Soluco] >= gestos * 45 / 100 && gestosVistos.Values.Max() == gestosVistos[Gesto.Soluco],
            $"o gesto de maior peso é o mais sorteado ({Contagem(gestosVistos)})");
    }

    /// <summary>
    /// Uma onda de teste que não muda o comportamento (todos os percentuais em 100, sem cambaleio e com o foguete do
    /// perfil), com os gestos e as caras do pico do bêbado e fases de duas horas: só a cara e o gesto podem mudar.
    /// </summary>
    private static DadosDaOnda OndaNeutra(Onda onda)
    {
        PerfilDaOnda bebado = TabelaDoTamagotchi.DaOnda(Onda.Bebado).Perfil(FaseDaOnda.Pico, 1);
        var neutro = new PerfilDaOnda(100, 100, 100, 100, 100, 100, 100, 100, 100, 0, null, 100, bebado.Gestos, bebado.Caras);
        TimeSpan duasHoras = TimeSpan.FromHours(2);
        return new DadosDaOnda(onda, 3, duasHoras, duasHoras, duasHoras, Expressao.Feliz, Expressao.Bebado, Expressao.Enjoado, [neutro, neutro, neutro], neutro, DeSubstancia: true);
    }

    /// <summary>O estado sem a cara, o nome do gesto e a onda: o que a onda neutra não pode mudar.</summary>
    private static EstadoDoNucleo SoOComportamento(EstadoDoNucleo s) => s with
    {
        Expressao = Expressao.Neutro,
        Gesto = s.Gesto == Gesto.Nenhum ? Gesto.Nenhum : Gesto.Espiar,
        Onda = null,
        GeracaoDaOnda = 0,
        OndaAgendada = false,
    };

    private static string SemONomeDoGesto(Transicao t)
    {
        int i = t.Regra.IndexOf("gesto ", StringComparison.Ordinal);
        return $"{t.De}->{t.Para} {(i < 0 ? t.Regra : t.Regra[..(i + 6)] + "*")}";
    }

    /// <summary>
    /// Uma interação sorteada a partir do estado: clique, arraste até um ponto qualquer do monitor, clique duplo (que
    /// esconde ou tira do esconderijo), esconder e mostrar pela bandeja, pausar ou retomar, ou nada.
    /// </summary>
    internal static Evento[] Interacao(Random rnd, EstadoDoNucleo s)
    {
        if (s.Estado == Estado.Hidden) return [new CmdShow()];
        if (s.Lugar is not { } lugar) return [];
        var corpo = new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 20);
        var alvo = new PontoPx(rnd.Next(0, 1920), rnd.Next(60, 1032));
        return rnd.Next(7) switch
        {
            0 => [new Press(corpo), new Click()],
            1 or 2 => [new Press(corpo), new DragStart(), new DragMove(alvo), new DragEnd(alvo)],
            3 => [new Press(corpo), new Click(), new Press(corpo), new DoubleClick()],
            4 => [new CmdHide()],
            5 => [s.AutonomiaPausada ? new CmdResumeAutonomy() : new CmdPauseAutonomy()],
            _ => [],
        };
    }

    // A cara da onda tem precedência sobre a emoção dominante (DEC-027 com DEC-028), e a dominante volta quando a onda
    // acaba. Escolher outra dominante no meio da onda (pelo menu ou pelas configurações) vale e é gravada, mas a cara
    // continua a da fase; no fim, entra a última escolhida.
    [Teste]
    public static void Onda_TemPrecedenciaSobreADominanteEDepoisVolta()
    {
        var sim = new SimuladorDeTempo(Ligado(), 5, TopologiasDeExemplo.UmMonitor, Preferencias.Padrao with { EmocaoDominante = Expressao.Pensativo });
        sim.Aplicar(new CmdPauseAutonomy());
        Afirmar.Igual(Expressao.Pensativo, sim.Estado.Expressao, "começa com a dominante");
        List<Aplicado> aplicados = [];
        SimuladorDeTempo tonto = Semear(sim, new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 1, 1), s => aplicados = Registrar(s));
        Afirmar.Igual(Expressao.Surpreso, tonto.Estado.Expressao, "a cara da subida do tonto");

        tonto.Aplicar(new CmdSetDominantEmotion(Expressao.Feliz));
        Afirmar.Igual(Expressao.Feliz, tonto.Estado.Preferencias.EmocaoDominante, "a escolha no meio da onda vale");
        Afirmar.Verdadeiro(aplicados[^1].Resultado.Efeitos.OfType<GravarPreferencias>().Any(), "e é gravada");
        Afirmar.Igual(Expressao.Surpreso, tonto.Estado.Expressao, "mas a cara continua a da onda");
        tonto.Aplicar(new SettingsChanged(tonto.Estado.Preferencias with { EmocaoDominante = Expressao.Curioso }));
        Afirmar.Igual(Expressao.Surpreso, tonto.Estado.Expressao, "pelas configurações também");

        tonto.Avancar(TimeSpan.FromSeconds(1.5));
        Afirmar.Igual((FaseDaOnda.Pico, Expressao.Tonto), (tonto.Estado.Onda!.Fase, tonto.Estado.Expressao), "o pico do tonto");
        tonto.Avancar(TimeSpan.FromSeconds(15));
        Afirmar.Igual((FaseDaOnda.Queda, Expressao.Sonolento), (tonto.Estado.Onda!.Fase, tonto.Estado.Expressao), "a queda do tonto");
        tonto.Avancar(TimeSpan.FromSeconds(10));
        Afirmar.Nulo(tonto.Estado.Onda, "a onda acabou");
        Afirmar.Igual(Expressao.Curioso, tonto.Estado.Expressao, "e volta a dominante, a última escolhida");
    }

    // Descansando (sonolento), reagindo ao clique (feliz) e pousando, a cara do estado continua quando a onda muda de fase;
    // no fim deles, entra a cara da fase em que a onda estiver: ao acordar, no fim da reação e no fim do pouso.
    [Teste]
    public static void EmRestingReactingELanding_ACaraDaFaseEsperaOFim()
    {
        // Descansando: o disparo da subida chega dormindo; ao acordar, a cara do pico do bêbado.
        SimuladorDeTempo descanso = Achar(AcoesAutonomas.Descansar, s => s.Estado == Estado.Resting);
        List<Aplicado> aplicados = [];
        descanso = Semear(descanso, new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2), s => aplicados = Registrar(s), comACara: false);
        descanso.Avancar(TimeSpan.FromSeconds(8.5));
        descanso.Esta(Estado.Resting, "ainda descansando depois da subida");
        Afirmar.Igual((FaseDaOnda.Pico, Expressao.Sonolento), (descanso.Estado.Onda!.Fase, descanso.Estado.Expressao), "no pico, a cara de quem descansa continua");
        descanso.Avancar(TimeSpan.FromMinutes(3), s => s.Estado == Estado.Idle);
        descanso.Esta(Estado.Idle, "acordou");
        Afirmar.Igual(Sinal.Acordou, aplicados.Last(a => a.Resultado.Estado.Estado == Estado.Idle).Resultado.Estado.Sinal, "pelo acordar");
        Afirmar.Igual(Expressao.Bebado, descanso.Estado.Expressao, "e acorda com a cara do pico");

        // Reagindo: o disparo da subida do chapado chega no meio da reação ao clique.
        var sim = new SimuladorDeTempo(Ligado(), 5, TopologiasDeExemplo.UmMonitor);
        sim.Aplicar(new CmdPauseAutonomy());
        SimuladorDeTempo chapado = Semear(sim, new EstadoDaOnda(Onda.Chapado, FaseDaOnda.Subida, 1, 1), s => aplicados = Registrar(s));
        chapado.Avancar(TimeSpan.FromSeconds(9.7));
        PontoPx a0 = chapado.Estado.Lugar!.Ancora;
        chapado.Aplicar(new Press(new PontoPx(a0.X, a0.Y - 20)));
        chapado.Aplicar(new Click());
        chapado.Esta(Estado.Reacting, "reagindo ao clique");
        chapado.Avancar(TimeSpan.FromSeconds(0.45));
        Aplicado disparo = aplicados.Single(a => a.Evento is ItemEffectTimer && a.Resultado.Transicoes.Count > 0);
        Afirmar.Igual(Estado.Reacting, disparo.Resultado.Estado.Estado, "o disparo chegou no meio da reação");
        Afirmar.Igual((FaseDaOnda.Pico, Expressao.Feliz), (disparo.Resultado.Estado.Onda!.Fase, disparo.Resultado.Estado.Expressao), "no pico, a cara da reação continua");
        chapado.Avancar(TimeSpan.FromSeconds(1));
        chapado.Esta(Estado.Idle, "fim da reação");
        Afirmar.Igual(Expressao.Chapado, chapado.Estado.Expressao, "e a cara do pico entra");

        // Pousando: solto do alto, quica rindo (toon force) e, no fim do pouso, fica com a cara da fase.
        var queda = new SimuladorDeTempo(Ligado() with { Acoes = AcoesAutonomas.Nenhuma }, 3, TopologiasDeExemplo.UmMonitor);
        SimuladorDeTempo viajando = Semear(queda, new EstadoDaOnda(Onda.Viajando, FaseDaOnda.Pico, 3, 3));
        RetanguloPx area = viajando.Estado.Lugar!.Monitor.AreaUtil;
        PontoPx a1 = viajando.Estado.Lugar.Ancora;
        var alto = new PontoPx(area.Esquerda + area.Largura / 2, area.Base - 600 - 30);
        foreach (Evento e in new Evento[] { new Press(new PontoPx(a1.X, a1.Y - 30)), new DragStart(), new DragMove(alto), new DragEnd(alto) }) viajando.Aplicar(e);
        viajando.Esta(Estado.Falling, "solto no ar");
        bool quicouRindo = false;
        while (viajando.Estado.Estado is Estado.Falling or Estado.Jumping && viajando.Passos(1) == 1)
            quicouRindo |= viajando.Estado.Estado == Estado.Jumping && viajando.Estado.Expressao == Expressao.Rindo;
        Afirmar.Verdadeiro(quicouRindo, "quicou rindo");
        viajando.Esta(Estado.Landing, "pousando");
        viajando.Passos(new ConfiguracaoDoNucleo().PassosDoPouso);
        viajando.Esta(Estado.Idle, "fim do pouso");
        Afirmar.Igual(Expressao.Viajando, viajando.Estado.Expressao, "com a cara do pico do viajando");
    }

    // ---------------------------------------------------------------- chave, retrato, gravação, fila e simulador

    // Com a chave desligada (como no aplicativo até o passo T9; desde então, só por configuração), uma onda no estado não
    // vale: o disparo é ignorado, nenhum efeito novo sai do núcleo (nem o agendamento de uma onda sem disparo pendente, nem
    // o cancelamento ao sair), e dez minutos de agenda livre com interações, terminando com a saída, dão exatamente os
    // mesmos eventos, efeitos, transições e estados que sem a onda (a não ser os próprios campos da onda), com as caras,
    // os gestos, os pesos, as velocidades e o cambaleio de sempre.
    [Teste]
    public static void ChaveDesligada_OndaNoEstadoNaoVale()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(Sprite) with { Tamagotchi = false, Personalidade = false, Curiosidade = false };
        Afirmar.Falso(cfg.Tamagotchi, "a física do aplicativo, com a chave desligada");
        var mestre = new Random(2030);
        long eventos = 0;
        for (int n = 0; n < 12; n++)
        {
            int semente = mestre.Next();
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            bool comDisparoPendente = n % 2 == 0;
            var semOnda = new SimuladorDeTempo(cfg, (ulong)semente, topologia);
            SimuladorDeTempo comOnda = new SimuladorDeTempo(cfg, (ulong)semente, topologia)
                .Semeado(s => s with { Onda = new EstadoDaOnda((Onda)(n % 10), FaseDaOnda.Pico, 3, 3), GeracaoDaOnda = 7, OndaAgendada = comDisparoPendente });
            List<Aplicado> a = Registrar(semOnda), b = Registrar(comOnda);
            semOnda.Aplicar(new ItemEffectTimer(7));
            comOnda.Aplicar(new ItemEffectTimer(7));
            Afirmar.Sequencia([], b[0].Resultado.Efeitos, $"semente {semente}: o disparo é ignorado, sem efeito");
            Afirmar.Igual(b[0].Antes, b[0].Resultado.Estado, $"semente {semente}: e nada muda");

            var rnd = new Random(semente);
            while (semOnda.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(5, 40));
                semOnda.Avancar(espera);
                comOnda.Avancar(espera);
                foreach (Evento e in Interacao(rnd, semOnda.Estado))
                {
                    semOnda.Aplicar(e);
                    comOnda.Aplicar(e);
                }
            }
            semOnda.Aplicar(new CmdExit());
            comOnda.Aplicar(new CmdExit());
            Afirmar.Igual(Estado.Exiting, comOnda.Estado.Estado, $"semente {semente}: saiu");
            Afirmar.Igual(a.Count, b.Count, $"semente {semente}: o mesmo número de eventos");
            for (int i = 0; i < a.Count; i++)
            {
                string onde = $"semente {semente}, {i}º evento ({a[i].Evento})";
                Afirmar.Igual(a[i].Evento, b[i].Evento, $"{onde}: o mesmo evento");
                Afirmar.Sequencia(a[i].Resultado.Transicoes, b[i].Resultado.Transicoes, $"{onde}: as mesmas transições");
                Afirmar.Sequencia(a[i].Resultado.Efeitos, b[i].Resultado.Efeitos, $"{onde}: os mesmos efeitos, nenhum da onda");
                Afirmar.Igual(a[i].Resultado.Estado, b[i].Resultado.Estado with { Onda = null, GeracaoDaOnda = 0, OndaAgendada = false }, $"{onde}: o mesmo estado");
            }
            eventos += a.Count;
        }
        Console.WriteLine($"         {eventos} eventos em 12 sementes de 10 minutos com a chave desligada");
    }

    // O retrato leva a onda, para as sobreposições da apresentação, e a linha canônica só a mostra quando há uma, como
    // "onda=Bebado/Pico/2", antes da emoção dominante: sem onda, a linha é a de antes (referências gravadas 01 a 05).
    [Teste]
    public static void Retrato_OndaSoApareceComValor()
    {
        Cenario c = Cenario.Parado(Ligado());
        Afirmar.Nulo(c.Retrato.Onda, "sem onda");
        Afirmar.Falso(c.Retrato.Descrever().Contains("onda=", StringComparison.Ordinal), $"sem onda, a linha de antes: {c.Retrato.Descrever()}");
        EstadoDoNucleo comOnda = c.Atual with { Onda = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 3) };
        Afirmar.Igual(comOnda.Onda, comOnda.Retrato().Onda, "o retrato leva a onda");
        string linha = comOnda.Retrato().Descrever();
        Afirmar.Verdadeiro(linha.EndsWith(" sinal=Nenhum onda=Bebado/Pico/2", StringComparison.Ordinal), $"com onda: {linha}");
        string comEmocao = (comOnda with { Preferencias = comOnda.Preferencias with { EmocaoDominante = Expressao.Feliz } }).Retrato().Descrever();
        Afirmar.Verdadeiro(comEmocao.EndsWith(" sinal=Nenhum onda=Bebado/Pico/2 emocao=Feliz", StringComparison.Ordinal), $"com onda e emoção: {comEmocao}");
        Afirmar.Igual(c.Retrato.Descrever(), (comOnda with { Onda = null }).Retrato().Descrever(), "sem a onda de novo, a mesma linha");
    }

    // A gravação das reproduções: o disparo da onda é escrito com a geração e, sem ela, lido com a da onda agendada (e não
    // com a da agenda); o agendamento mostra o atraso em milissegundos e a geração, e o cancelamento, o nome.
    [Teste]
    public static void Gravacao_DisparoEAgendamentoDaOnda()
    {
        Topologia umMonitor = TopologiasDeExemplo.UmMonitor;
        EstadoDoNucleo estado = EstadoDoNucleo.Inicial(1) with { GeracaoDaOnda = 9, Geracao = 4 };
        Afirmar.Igual("ItemEffectTimer geracao=7", Gravacao.Escrever(new ItemEffectTimer(7), _ => "UmMonitor"), "escrita");
        Afirmar.Igual(new ItemEffectTimer(7), Gravacao.Ler("ItemEffectTimer geracao=7", _ => umMonitor, estado).Single(), "lida");
        Afirmar.Igual(new ItemEffectTimer(9), Gravacao.Ler("ItemEffectTimer", _ => umMonitor, estado).Single(), "sem geração, a da onda agendada");
        Afirmar.Igual(new AutonomyTimer(4), Gravacao.Ler("AutonomyTimer", _ => umMonitor, estado).Single(), "e a agenda continua com a dela");
        Afirmar.Igual("AgendarOnda atrasoMs=112500 geracao=3", Gravacao.DescreverEfeito(new AgendarOnda(TimeSpan.FromSeconds(112.5), 3)), "agendamento");
        Afirmar.Igual("CancelarOnda", Gravacao.DescreverEfeito(new CancelarOnda()), "cancelamento");
    }

    // O disparo da onda tem a prioridade do relógio: com o usuário segurando o personagem, ele entra na fila e é aplicado
    // (a agenda, autônoma, é descartada), e a onda avança sem soltar o personagem; um gesto em curso não termina com ele.
    [Teste]
    public static void Fila_ODisparoDaOndaTemAPrioridadeDoRelogio()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        Afirmar.Igual(Origem.Relogio, new ItemEffectTimer(1).Origem, "origem do relógio");
        var carregado = new Nucleo(cfg, 3);
        carregado.Enfileirar(new Loaded(TopologiasDeExemplo.UmMonitor, null, Preferencias.Padrao));
        carregado.Processar();
        long geracaoDaAgenda = carregado.Estado.Geracao;
        PontoPx a = carregado.Estado.Lugar!.Ancora;
        carregado.Enfileirar(new Press(new PontoPx(a.X, a.Y - 20)));
        carregado.Processar();
        Afirmar.Igual(Estado.Pressed, carregado.Retrato.Estado, "segurado");

        var nucleo = new Nucleo(cfg, carregado.Estado with { Onda = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2) });
        nucleo.Enfileirar(new ItemEffectTimer(0));
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(8), 1)], nucleo.Processar(), "a onda é agendada com ele segurado");
        Afirmar.Verdadeiro(nucleo.Enfileirar(new ItemEffectTimer(1)), "o disparo da onda entra na fila com o usuário no controle");
        Afirmar.Falso(nucleo.Enfileirar(new AutonomyTimer(geracaoDaAgenda)), "o da agenda é descartado");
        IReadOnlyList<Efeito> efeitos = nucleo.Processar();
        Afirmar.Igual(Estado.Pressed, nucleo.Retrato.Estado, "continua segurado");
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2), nucleo.Estado.Onda, "e a onda avançou");
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(100), 2)], efeitos, "com o próximo disparo agendado");

        Cenario gesto = Cenario.Parado(cfg with { Acoes = AcoesAutonomas.Gesto, Movimento = false }).Decidir();
        Afirmar.Diferente(Gesto.Nenhum, gesto.Atual.Gesto, "um gesto em curso");
        EstadoDoNucleo comOnda = gesto.Atual with { Onda = new EstadoDaOnda(Onda.Alegre, FaseDaOnda.Subida, 1, 1), GeracaoDaOnda = 5, OndaAgendada = true };
        Resultado r = Maquina.Aplicar(comOnda, new ItemEffectTimer(5), cfg);
        Afirmar.Igual((comOnda.Gesto, comOnda.PassosDoGesto), (r.Estado.Gesto, r.Estado.PassosDoGesto), "o gesto continua");
        Afirmar.Falso(r.Efeitos.Any(e => e is AgendarDecisao or CancelarDecisao or DesligarRelogio), "sem mexer na agenda nem no relógio");
    }

    // O simulador faz o papel da raiz com os dois temporizadores: num empate, entrega o disparo da onda antes do da
    // agenda (a onda tem a prioridade do relógio). Saindo, a máquina cancela a onda, e o disparo pendente não chega mais.
    [Teste]
    public static void Simulador_NoEmpateAOndaVemAntes_ESairCancelaAOnda()
    {
        PerfilDeEnergia oitoSegundos = PerfilDeEnergia.Media with { DecisaoMinima = TimeSpan.FromSeconds(8), DecisaoMaxima = TimeSpan.FromSeconds(8) };
        ConfiguracaoDoNucleo cfg = Ligado() with { Perfil = _ => oitoSegundos, Acoes = AcoesAutonomas.TrocarExpressao };
        var sim = new SimuladorDeTempo(cfg, 5, TopologiasDeExemplo.UmMonitor);
        List<Aplicado> aplicados = [];
        SimuladorDeTempo semeado = Semear(sim, new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 1, 1), s => aplicados = Registrar(s));
        Afirmar.Sequencia([new AgendarOnda(TimeSpan.FromSeconds(8), 1)], aplicados[0].Resultado.Efeitos, "a subida do bêbado vence aos 8 s, junto com a agenda");
        semeado.Avancar(TimeSpan.FromSeconds(8.5));
        Afirmar.Sequencia(["ItemEffectTimer", "ItemEffectTimer", "AutonomyTimer"], aplicados.Select(a => a.Evento.GetType().Name), "no empate, a onda antes da agenda");

        semeado.Aplicar(new CmdExit());
        Resultado saida = aplicados[^1].Resultado;
        Afirmar.Igual(Estado.Exiting, saida.Estado.Estado, "saindo");
        Afirmar.Verdadeiro(saida.Efeitos.OfType<CancelarOnda>().Count() == 1 && saida.Efeitos[^1] is Encerrar, $"cancela a onda antes de encerrar ({string.Join(", ", saida.Efeitos)})");
        Afirmar.Falso(saida.Estado.OndaAgendada, "sem disparo pendente");
        int antes = aplicados.Count;
        semeado.Avancar(TimeSpan.FromMinutes(10));
        Afirmar.Igual(antes, aplicados.Count, "nada mais chega depois de sair");
    }

    // ---------------------------------------------------------------- pelos itens: combinação, onda de fundo e água (T5)

    /// <summary>Usa o item, solto sobre ele, e deixa o uso acabar; devolve o resultado do soltar.</summary>
    private static Resultado Usar(Cenario c, Item item)
    {
        ItemNoMundo it = ApoioDosItens.InvocarEAssentar(c, item);
        c.SoltarSobreEle(it.Id).Esta(Estado.Using, $"usando {item}");
        Resultado soltar = Afirmar.NaoNulo(c.Ultimo, "o resultado do soltar");
        c.Passos(c.Atual.PassosRestantes).Esta(Estado.Idle, $"fim do uso de {item}");
        return soltar;
    }

    /// <summary>O disparo da onda na geração agendada; devolve o atraso agendado em seguida, ou nulo.</summary>
    private static TimeSpan? Disparar(Cenario c)
    {
        c.Aplicar(new ItemEffectTimer(c.Atual.GeracaoDaOnda));
        return c.Efeitos.OfType<AgendarOnda>().SingleOrDefault()?.Atraso;
    }

    // Tabela 4.1 pelos itens: cada item com onda, usado sozinho, começa a onda dele na subida, no nível da intensidade, já
    // no soltar; os disparos seguintes têm os atrasos da tabela (4.2), e a soma é a "duração sozinho" da 4.1. No fim, sem
    // onda nem temporizador, e a cara de base (a neutra, na automática).
    [Teste]
    public static void CadaItem_FasesComAsDuracoesDaTabela()
    {
        foreach (TabelasDoDesenho.ItemEsperado esperado in TabelasDoDesenho.ItensEsperados())
        {
            if (esperado.Onda is not { } onda) continue;
            string caso = esperado.Item.ToString();
            Cenario c = Cenario.Parado(ApoioDosItens.SemFisica()).Aplicar(new CmdPauseAutonomy());
            Resultado soltar = Usar(c, esperado.Item);
            Afirmar.Igual(new EstadoDaOnda(onda, FaseDaOnda.Subida, esperado.Intensidade, esperado.Intensidade), soltar.Estado.Onda, $"{caso}: a onda começa no soltar");
            var atrasos = new List<TimeSpan> { soltar.Efeitos.OfType<AgendarOnda>().Single().Atraso };
            int disparos = 0;
            while (c.Atual.Onda is not null)
            {
                Afirmar.Verdadeiro(disparos++ < 10, $"{caso}: a onda acaba");
                if (Disparar(c) is { } atraso) atrasos.Add(atraso);
                Afirmar.Igual(c.Atual.Onda is { } o ? CaraEsperada(o) : Expressao.Neutro, c.Atual.Expressao, $"{caso}: a cara da fase, no {disparos}º disparo");
            }
            Afirmar.Sequencia(TabelasDoDesenho.Esperada(onda).Atrasos(esperado.Intensidade), atrasos, $"{caso}: os atrasos da tabela");
            Afirmar.Igual(atrasos.Count, disparos, $"{caso}: um disparo por agendamento");
            Afirmar.Igual(esperado.DuracaoSozinho, atrasos.Aggregate(TimeSpan.Zero, (a, b) => a + b), $"{caso}: a duração sozinho da tabela 4.1");
            Afirmar.Falso(c.Atual.OndaAgendada, $"{caso}: sem temporizador pendente");
        }
    }

    // 4.5: o mesmo tipo acumula, até o nível 3. Cerveja (bêbado 1) e depois vodka (+2): nível 3, pior 3, e a subida
    // recomeça; o pico dura três níveis e a queda, 150% (135 s). Na queda, outra cerveja volta ao pico, no nível 2, e mais
    // uma vodka o deixa no nível 3, sem passar dele. Só álcool, sem droga sintética: nada de paranoia (pedido do usuário de
    // 2026-10-01; ParanoiaTestes).
    [Teste]
    public static void MesmoTipo_AcumulaAteONivel3()
    {
        Cenario c = Cenario.Parado(ApoioDosItens.SemFisica()).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Cerveja);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 1, 1), c.Atual.Onda, "a cerveja: bêbado no nível 1");
        Resultado vodka = Usar(c, Item.Vodka);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 3, 3), vodka.Estado.Onda, "com a vodka: nível 3, e a subida recomeça");
        Afirmar.Sequencia([TimeSpan.FromSeconds(8)], vodka.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "a subida inteira de novo");
        Afirmar.Sequencia(
            [TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(135)],
            [Disparar(c)!.Value, Disparar(c)!.Value, Disparar(c)!.Value, Disparar(c)!.Value], "o pico em três níveis e a queda a 150%");
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Queda, 1, 3), c.Atual.Onda, "na queda, com o pior nível 3");

        Resultado outra = Usar(c, Item.Cerveja);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 3), outra.Estado.Onda, "outra cerveja na queda: de volta ao pico, no nível 2");
        Afirmar.Sequencia([TimeSpan.FromSeconds(100)], outra.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "o pico recomeça");
        Usar(c, Item.Vodka);
        Afirmar.Igual((new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 3, 3), (EstadoDaOnda?)null), (c.Atual.Onda, c.Atual.OndaDeFundo), "e nunca passa do nível 3; só álcool, sem paranoia");
    }

    // 4.5: uma onda de precedência maior ou igual vai para a frente, e a anterior fica atrás, congelada: o bêbado no pico do
    // nível 2 e, por cima, o lança-perfume (tonto, nível 2). Só o tonto avança e manda no comportamento; quando ele acaba,
    // o bêbado volta à frente na fase em que estava, com a duração cheia (100 s) e a cara do pico. A vodka e o lança-perfume
    // são uma mistura com droga sintética, que sorteia a paranoia; aqui ela fica de fora (a chance de 0 em 1), e os testes
    // dela estão em ParanoiaTestes.
    [Teste]
    public static void MaisForte_VaiParaAFrenteEAAnteriorVolta()
    {
        ConfiguracaoDoNucleo cfg = ApoioDosItens.SemFisica() with { ChanceDaParanoia = ApoioDosItens.NuncaParanoia };
        Cenario c = Cenario.Parado(cfg).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Vodka);
        Disparar(c);
        var bebado = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2);
        Afirmar.Igual(bebado, c.Atual.Onda, "o bêbado no pico do nível 2");

        Resultado lanca = Usar(c, Item.LancaPerfume);
        Afirmar.Igual((new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 2, 2), bebado), (lanca.Estado.Onda, lanca.Estado.OndaDeFundo), "o tonto na frente, o bêbado atrás");
        Afirmar.Sequencia([TimeSpan.FromSeconds(1)], lanca.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "o temporizador é o do tonto");
        Afirmar.Igual(TabelaDoTamagotchi.DaOnda(Onda.Tonto).Perfil(FaseDaOnda.Subida, 2).Velocidade, Maquina.PerfilDaFase(c.Atual, cfg)!.Velocidade, "só a da frente vale");

        Afirmar.Sequencia([TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(12.5)], [Disparar(c)!.Value, Disparar(c)!.Value, Disparar(c)!.Value], "o tonto inteiro");
        Afirmar.Igual(bebado, c.Atual.OndaDeFundo, "o bêbado ficou congelado");
        TimeSpan? volta = Disparar(c);
        Afirmar.Igual((bebado, (EstadoDaOnda?)null), (c.Atual.Onda, c.Atual.OndaDeFundo), "o bêbado volta à frente, na fase em que estava");
        Afirmar.Igual(TimeSpan.FromSeconds(100), volta, "com a duração cheia do nível");
        Afirmar.Igual("ITEM_EFFECT_TIMER: onda Tonto/Queda/1 -> fim; a de fundo volta: Bebado/Pico/2", c.Transicoes.Single().Regra, "a regra");
        Afirmar.Igual(Expressao.Bebado, c.Atual.Expressao, "com a cara do pico");

        // Precedência igual também vai para a frente: a banana (satisfeito) e depois o cigarro (relaxado), os dois de
        // precedência 1. (Antes era a bala, com o alegre; desde 2026-10-01 a bala é droga sintética, com o eufórico.)
        Cenario iguais = Cenario.Parado(cfg).Aplicar(new CmdPauseAutonomy());
        Usar(iguais, Item.Banana);
        Usar(iguais, Item.Cigarro);
        Afirmar.Igual((Onda.Relaxado, Onda.Satisfeito), (iguais.Atual.Onda!.Tipo, iguais.Atual.OndaDeFundo!.Tipo), "a de precedência igual vai para a frente");
    }

    // 4.5: uma onda de precedência menor é absorvida: nem a onda nem o temporizador mudam, e nenhuma vai para o fundo. O
    // cigarro (relaxado) por cima do elétrico e, entre as ondas leves, a banana (satisfeito) por cima do ligado do café e do
    // energético. Com o alívio (pedido do usuário de 2026-10-01), a comida e a bebida sem álcool não são mais absorvidas por
    // uma onda de substância: elas a aliviam um passo (AlivioTestes). A cocaína e o cigarro são uma mistura com droga
    // sintética, que sorteia a paranoia; aqui ela fica de fora (a chance de 0 em 1; ParanoiaTestes).
    [Teste]
    public static void MaisFraca_EhAbsorvida()
    {
        foreach ((Item forte, Item fraco) in new[] { (Item.Cocaina, Item.Cigarro), (Item.Cafe, Item.Banana), (Item.Energetico, Item.Banana) })
        {
            Cenario c = Cenario.Parado(ApoioDosItens.SemFisica() with { ChanceDaParanoia = ApoioDosItens.NuncaParanoia }).Aplicar(new CmdPauseAutonomy());
            Usar(c, forte);
            EstadoDoNucleo antes = c.Atual;
            Resultado absorvido = Usar(c, fraco);
            string caso = $"{fraco} por cima de {forte}";
            Afirmar.Igual(antes.Onda, absorvido.Estado.Onda, $"{caso}: a onda da frente não muda");
            Afirmar.Nulo(absorvido.Estado.OndaDeFundo, $"{caso}: nada vai para o fundo");
            Afirmar.Falso(absorvido.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), $"{caso}: o temporizador não muda");
            Afirmar.Igual(antes.GeracaoDaOnda, c.Atual.GeracaoDaOnda, $"{caso}: nem a geração");
        }
    }

    // 4.5: só cabem duas: bêbado, depois chapado, depois elétrico deixam o elétrico na frente e só o chapado atrás (o bêbado
    // é descartado). Quando o elétrico acaba, volta o chapado; quando o chapado acaba, não sobra onda. A cocaína fecha uma
    // mistura com droga sintética, que sorteia a paranoia; aqui ela fica de fora (NuncaParanoia; ParanoiaTestes).
    [Teste]
    public static void SoCabemDuas()
    {
        Cenario c = Cenario.Parado(ApoioDosItens.SemFisica() with { ChanceDaParanoia = ApoioDosItens.NuncaParanoia }).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Vodka);
        Usar(c, Item.Baseado);
        Afirmar.Igual((Onda.Chapado, Onda.Bebado), (c.Atual.Onda!.Tipo, c.Atual.OndaDeFundo!.Tipo), "chapado na frente, bêbado atrás");
        Usar(c, Item.Cocaina);
        Afirmar.Igual((Onda.Eletrico, Onda.Chapado), (c.Atual.Onda!.Tipo, c.Atual.OndaDeFundo!.Tipo), "elétrico na frente, só o chapado atrás");
        int disparos = 0;
        while (c.Atual.Onda!.Tipo == Onda.Eletrico)
        {
            Afirmar.Verdadeiro(disparos++ < 10, "o elétrico acaba");
            Disparar(c);
        }
        Afirmar.Igual((Onda.Chapado, (EstadoDaOnda?)null), (c.Atual.Onda.Tipo, c.Atual.OndaDeFundo), "volta o chapado, sem fundo");
        while (c.Atual.Onda is not null)
        {
            Afirmar.Verdadeiro(disparos++ < 20, "o chapado acaba");
            Disparar(c);
        }
        Afirmar.Falso(c.Atual.OndaAgendada, "sem onda, sem temporizador");
    }

    // 4.5, com o desvio 3 do T5: um item do mesmo tipo da onda de fundo soma os níveis dela, que continua congelada; se ela
    // estava na queda, volta ao pico, como a da frente (manter a fase daria uma queda acima do nível 1, que não existe). A
    // vodka até a queda, o lança-perfume por cima (o bêbado vai para o fundo, na queda) e uma cerveja: o fundo fica no pico
    // do nível 2, sem mexer no temporizador do tonto; quando o tonto acaba, o bêbado volta no pico, com a duração cheia. A
    // vodka e o lança-perfume são uma mistura com droga sintética, que sorteia a paranoia; aqui ela fica de fora
    // (NuncaParanoia; ParanoiaTestes).
    [Teste]
    public static void MesmoTipoDaDeFundo_NaQueda_VoltaAoPico()
    {
        Cenario c = Cenario.Parado(ApoioDosItens.SemFisica() with { ChanceDaParanoia = ApoioDosItens.NuncaParanoia }).Aplicar(new CmdPauseAutonomy());
        Usar(c, Item.Vodka);
        while (c.Atual.Onda!.Fase != FaseDaOnda.Queda) Disparar(c);
        var naQueda = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Queda, 1, 2);
        Afirmar.Igual(naQueda, c.Atual.Onda, "a vodka na queda");
        Usar(c, Item.LancaPerfume);
        var tonto = new EstadoDaOnda(Onda.Tonto, FaseDaOnda.Subida, 2, 2);
        Afirmar.Igual((tonto, naQueda), (c.Atual.Onda, c.Atual.OndaDeFundo), "o tonto na frente, o bêbado no fundo, na queda");
        long geracao = c.Atual.GeracaoDaOnda;

        Resultado cerveja = Usar(c, Item.Cerveja);
        var noPico = new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 2, 2);
        Afirmar.Igual((tonto, noPico), (cerveja.Estado.Onda, cerveja.Estado.OndaDeFundo), "a cerveja soma no fundo, que volta ao pico do nível 2");
        Afirmar.Falso(cerveja.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem mexer no temporizador do tonto");
        Afirmar.Igual(geracao, c.Atual.GeracaoDaOnda, "nem na geração dele");

        TimeSpan? volta = null;
        for (int disparos = 0; c.Atual.Onda!.Tipo == Onda.Tonto; disparos++)
        {
            Afirmar.Verdadeiro(disparos < 10, "o tonto acaba");
            volta = Disparar(c);
        }
        Afirmar.Igual((noPico, (EstadoDaOnda?)null), (c.Atual.Onda, c.Atual.OndaDeFundo), "o bêbado volta à frente, no pico do nível 2");
        Afirmar.Igual(TimeSpan.FromSeconds(100), volta, "com a duração cheia de um nível do pico");
    }

    // 4.5, a água, com o alívio (pedido do usuário de 2026-10-01): sem onda, nada; na subida acima do nível 1 e no pico acima
    // do 1, baixa um nível sem mexer no temporizador; no nível 1, da subida ou do pico, vai para a queda (ou acaba, sem
    // queda); na queda, acaba. Com uma onda de fundo, a que acaba dá lugar a ela. Antes do alívio, a subida do nível 1
    // acabava; o resto é o mesmo. A vodka e o lança-perfume do fim são uma mistura com droga sintética, que sorteia a
    // paranoia; aqui ela fica de fora (NuncaParanoia; ParanoiaTestes).
    [Teste]
    public static void Agua_BaixaUmNivelEEncerraAQueda()
    {
        Cenario c = Cenario.Parado(ApoioDosItens.SemFisica() with { ChanceDaParanoia = ApoioDosItens.NuncaParanoia }).Aplicar(new CmdPauseAutonomy());
        Resultado sem = Usar(c, Item.Agua);
        Afirmar.Nulo(sem.Estado.Onda, "sem onda: a água não faz nada");
        Afirmar.Falso(sem.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "nem no temporizador");

        Usar(c, Item.Vodka);
        Resultado subida = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 1, 2), subida.Estado.Onda, "na subida do nível 2: nível 1, o pior continua 2");
        Afirmar.Falso(subida.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem mexer no temporizador");
        Resultado daSubidaAQueda = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Queda, 1, 2), daSubidaAQueda.Estado.Onda, "na subida do nível 1: a queda (antes do alívio, a onda acabava)");
        Afirmar.Sequencia([TimeSpan.FromSeconds(112.5)], daSubidaAQueda.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "com a duração cheia da queda, pelo pior nível 2");
        Resultado fim = Usar(c, Item.Agua);
        Afirmar.Nulo(fim.Estado.Onda, "na queda: a onda acaba");
        Afirmar.Verdadeiro(fim.Efeitos.OfType<CancelarOnda>().Count() == 1, "e o temporizador é cancelado");

        Usar(c, Item.Vodka);
        Disparar(c);
        Resultado pico = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Pico, 1, 2), pico.Estado.Onda, "no pico do nível 2: nível 1");
        Afirmar.Falso(pico.Efeitos.Any(e => e is AgendarOnda or CancelarOnda), "sem mexer no temporizador");
        Resultado queda = Usar(c, Item.Agua);
        Afirmar.Igual(new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Queda, 1, 2), queda.Estado.Onda, "no pico do nível 1: a queda");
        Afirmar.Sequencia([TimeSpan.FromSeconds(112.5)], queda.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "com a duração da queda");
        Afirmar.Nulo(Usar(c, Item.Agua).Estado.Onda, "na queda: a onda acaba");

        // Sem queda (satisfeito), o pico do nível 1 acaba.
        Usar(c, Item.Banana);
        Disparar(c);
        Afirmar.Nulo(Usar(c, Item.Agua).Estado.Onda, "o satisfeito no pico do nível 1, sem queda: acaba");

        // Com uma de fundo: o tonto na queda, por cima do bêbado; a água acaba o tonto e o bêbado volta.
        Usar(c, Item.Vodka);
        Usar(c, Item.LancaPerfume);
        while (c.Atual.Onda!.Fase != FaseDaOnda.Queda) Disparar(c);
        Resultado volta = Usar(c, Item.Agua);
        Afirmar.Igual((new EstadoDaOnda(Onda.Bebado, FaseDaOnda.Subida, 2, 2), (EstadoDaOnda?)null), (volta.Estado.Onda, volta.Estado.OndaDeFundo), "o bêbado volta, na subida em que estava");
        Afirmar.Sequencia([TimeSpan.FromSeconds(8)], volta.Efeitos.OfType<AgendarOnda>().Select(a => a.Atraso), "com a subida inteira");
    }

    // Invariantes 24 a 26, 28 e 29 com a física do aplicativo (os de InvariantesTestes rodam sem ela): uma hora simulada por
    // semente, com a agenda livre e interações sorteadas, com itens de verdade (invocar, soltar sobre ele ou longe, largar,
    // recolher) e o personagem posto no chão, no ar, na parede, no cipó e no esconderijo. A cada evento: o temporizador da
    // onda (disparos únicos de 1 s ou mais, um só pendente, no máximo 2 + nível por onda da frente), o relógio, o R11 pelo
    // perfil efetivo, gestos só em IDLE, o apoio do personagem (o cambaleio sempre no chão e entre as laterais; com calma,
    // agarrado só preso ou atento, DEC-022), os itens na área útil (parados no chão; nenhum invisível caindo) e, no fim de
    // cada uso, a volta ao mesmo apoio: o chão, a parede ou o cipó (agarrado, preso se já estava, com ele preso ou por
    // conta própria; com calma, sem estar preso, solto para descer) e o esconderijo, na mesma borda. O uso na parede e no
    // cipó por conta própria é conferido quando acontece, mas não é exigido: ele escala e se pendura sozinho poucos segundos
    // por hora, e o uso até o fim ali sai por sorte (de nenhum a poucos em 4 horas, conforme a trajetória, que muda com
    // qualquer regra nova, como o alívio do pedido do usuário de 2026-10-01). Os casos determinísticos ficam em UsoTestes
    // (Usar_NaParedePertoDoChao_VoltaAParede e Usar_NoAltoPorContaPropria_VoltaAoMesmoApoio).
    [Teste]
    public static void EmRepouso_ComItens_ORelogioNaoLigaEOsDisparosSaoUnicos()
    {
        ConfiguracaoDoNucleo cfg = Ligado();
        var mestre = new Random(2032);
        long eventos = 0, disparos = 0, comItem = 0;
        var usos = new Dictionary<string, int>();
        for (int n = 0; n < 4; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var sim = new SimuladorDeTempo(cfg, (ulong)semente, TopologiasDeExemplo.UmMonitor, new Preferencias((NivelDeEnergia)rnd.Next(3), true));
            int disparosNoEpisodio = 0, limite = 0, passosNoUso = 0;
            (Uso Uso, EstadoDoNucleo Antes)? emUso = null;
            void Conferir(EstadoDoNucleo antes, Evento e, Resultado r)
            {
                eventos++;
                EstadoDoNucleo depois = r.Estado;
                string onde = $"semente {semente}, {e} em {antes.Estado} -> {depois.Estado} (onda {depois.Onda}, uso {depois.Uso})";
                if (depois.Itens.Quantidade > 0) comItem++;

                // O temporizador da onda (invariante 25).
                AgendarOnda[] agendas = [.. r.Efeitos.OfType<AgendarOnda>()];
                Afirmar.Verdadeiro(agendas.All(a => a.Atraso >= TimeSpan.FromSeconds(1)), $"{onde}: disparos únicos de 1 s ou mais");
                Afirmar.Verdadeiro(agendas.Length + r.Efeitos.OfType<CancelarOnda>().Count() <= 1, $"{onde}: um temporizador da onda por vez");
                if (depois.Estado != Estado.Exiting) Afirmar.Igual(depois.Onda is not null, depois.OndaAgendada, $"{onde}: com onda, um disparo pendente; sem onda, nenhum");
                if (e is ItemEffectTimer && r.Transicoes.Count > 0)
                {
                    disparos++;
                    Afirmar.Verdadeiro(++disparosNoEpisodio <= limite, $"{onde}: a onda da frente acaba em no máximo {limite} disparos");
                }
                bool usou = r.Transicoes.Any(t => t.Para == Estado.Using && t.De != Estado.Using);
                if (depois.Onda is { } frente && (usou || antes.Onda is null || antes.Onda.Tipo != frente.Tipo))
                {
                    disparosNoEpisodio = 0;
                    limite = 2 + frente.Nivel;
                }

                // O relógio (invariante 29), a agenda pelo perfil efetivo (R11) e os gestos.
                bool agarrado = depois.Estado is Estado.Climbing or Estado.Hanging && depois.Movimento.Agarrado;
                bool caindo = depois.Itens.Todos.Any(i => i.Situacao == SituacaoDoItem.Caindo && Maquina.ItemVisivel(depois, i));
                bool relogio = (depois.Estado.EmMovimento() && !agarrado) || depois.Estado is Estado.Reacting or Estado.Using
                    || (depois.Estado == Estado.Idle && depois.Gesto != Gesto.Nenhum) || caindo;
                Afirmar.Igual(relogio, depois.RelogioAtivo, $"{onde}: o relógio pelo invariante 29");
                foreach (AgendarDecisao agenda in r.Efeitos.OfType<AgendarDecisao>())
                {
                    PerfilDeEnergia p = Maquina.PerfilEfetivo(depois, cfg);
                    (TimeSpan minimo, TimeSpan maximo) = depois.Estado switch
                    {
                        Estado.Resting => (p.DescansoMinimo, p.DescansoMaximo),
                        Estado.Climbing => (p.TempoNaParedeMinimo, p.TempoNaParedeMaximo),
                        Estado.Hanging => (p.TempoPenduradoMinimo, p.TempoPenduradoMaximo),
                        _ => (p.DecisaoMinima, p.DecisaoMaxima),
                    };
                    TimeSpan piso = cfg.IntervaloDeAcomodacao;
                    Afirmar.Verdadeiro(agenda.Atraso >= piso && agenda.Atraso >= minimo && agenda.Atraso <= (maximo < piso ? piso : maximo), $"{onde}: R11, atraso {agenda.Atraso} fora de {minimo}–{maximo}");
                    Afirmar.Falso(depois.Atento, $"{onde}: nenhuma agenda com o usuário segurando um item");
                }
                if (depois.Gesto != Gesto.Nenhum) Afirmar.Igual(Estado.Idle, depois.Estado, $"{onde}: gesto {depois.Gesto} só em IDLE");

                // O apoio dele (o cambaleio, sempre no chão e entre as laterais: invariante 26) e os itens (invariante 28).
                MovimentoTestes.ConferirApoio(depois, onde);
                // DEC-022: com calma, só fica agarrado quem está preso pelo usuário ou atento a um item na mão.
                if ((depois.AutonomiaPausada || depois.PainelAberto) && depois.Estado is Estado.Climbing or Estado.Hanging && depois.Movimento.Agarrado)
                    Afirmar.Verdadeiro(depois.PresoPeloUsuario || depois.Atento, $"{onde}: com calma, agarrado sem estar preso nem atento");
                Afirmar.Verdadeiro(depois.Itens.Quantidade <= cfg.MaximoDeItens, $"{onde}: no máximo {cfg.MaximoDeItens} itens");
                foreach (ItemNoMundo item in depois.Itens.Todos.Where(i => !i.NaMao))
                {
                    RetanguloPx area = item.Lugar.Monitor.AreaUtil;
                    Afirmar.Verdadeiro(area.Contem(item.Lugar.Retangulo), $"{onde}: o item {item.Id} inteiro na área útil");
                    if (item.Situacao == SituacaoDoItem.NoChao) Afirmar.Igual(area.Base, item.Lugar.Ancora.Y, $"{onde}: o item {item.Id} parado no chão");
                    if (item.Situacao == SituacaoDoItem.Caindo) Afirmar.Verdadeiro(Maquina.ItemVisivel(depois, item), $"{onde}: L5, o item {item.Id} cai sem aparecer");
                }

                // O uso (invariante 24): dura os passos do item e volta ao mesmo apoio.
                if (usou) { emUso = (depois.Uso!, antes); passosNoUso = 0; }
                if (antes.Estado == Estado.Using && e is Tick) passosNoUso++;
                if (antes.Estado == Estado.Using && depois.Estado != Estado.Using && emUso is { } u
                    && r.Transicoes[0].Regra.StartsWith("USING: fim do uso", StringComparison.Ordinal))
                {
                    Afirmar.Igual(u.Uso.Passos, passosNoUso, $"{onde}: o uso dura os passos do item");
                    EstadoDoNucleo deAntes = u.Antes;
                    string apoio = u.Uso.Apoio.ToString();
                    switch (u.Uso.Apoio)
                    {
                        case ApoioDoUso.Esconderijo:
                            Afirmar.Igual((Estado.Peeking, deAntes.Esconderijo), (depois.Estado, depois.Esconderijo), $"{onde}: volta ao esconderijo, na mesma borda");
                            break;
                        case ApoioDoUso.Parede or ApoioDoUso.Cipo:
                            Afirmar.Igual(u.Uso.Apoio == ApoioDoUso.Parede ? Estado.Climbing : Estado.Hanging, depois.Estado, $"{onde}: volta a {u.Uso.Apoio}");
                            // Agarrado, e preso se já estava; sem estar preso, com calma e sem item na mão, já solto para
                            // descer ou se soltar nos passos seguintes (DEC-022).
                            bool espera = depois.PresoPeloUsuario || !(depois.AutonomiaPausada || depois.PainelAberto) || depois.Atento;
                            Afirmar.Verdadeiro(depois.Movimento.Agarrado == espera && depois.PresoPeloUsuario == deAntes.PresoPeloUsuario, $"{onde}: agarrado, preso se já estava");
                            Afirmar.Igual(deAntes.Lugar!.Ancora, depois.Lugar!.Ancora, $"{onde}: no mesmo lugar");
                            if (deAntes.PresoPeloUsuario) apoio += " (preso)";
                            break;
                        default:
                            if (deAntes.Lugar!.Ancora.Y == deAntes.Lugar.Monitor.AreaUtil.Base)
                                Afirmar.Igual((Estado.Idle, deAntes.Lugar.Ancora), (depois.Estado, depois.Lugar!.Ancora), $"{onde}: no chão, volta a IDLE no mesmo lugar");
                            else apoio += " (no ar)";
                            break;
                    }
                    usos[apoio] = usos.GetValueOrDefault(apoio) + 1;
                    emUso = null;
                }
            }
            sim.AoResultado = Conferir;
            bool posto = false;
            while (sim.AgoraMs < 60 * 60 * 1000)
            {
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(2, 30)));
                foreach (Evento e in InteracaoComItens(rnd, sim.Estado, cfg, ref posto)) sim.Aplicar(e);
            }
        }
        Console.WriteLine($"         {eventos} eventos em 4 horas simuladas, {comItem} com itens; {disparos} disparos; usos até o fim: {string.Join(", ", usos.OrderBy(u => u.Key).Select(u => $"{u.Key}={u.Value}"))}");
        // Exigidos, os apoios que a simulação sempre alcança: o chão, a parede e o cipó com ele preso pelo usuário, e o
        // esconderijo. Por conta própria ("Parede" e "Cipo", sem estar preso), só contados (veja o comentário do teste).
        foreach (string apoio in new[] { "Chao", "Parede (preso)", "Cipo (preso)", "Esconderijo" })
            Afirmar.Verdadeiro(usos.GetValueOrDefault(apoio) > 0, $"um uso até o fim em {apoio}");
        Afirmar.Verdadeiro(disparos >= 40, $"disparos da onda: {disparos}");
    }

    /// <summary>
    /// Uma interação sorteada com itens: invocar; soltar um item sobre ele (mais vezes logo depois de pô-lo num lugar, ou
    /// com ele escalando ou pendurado por conta própria) ou longe; clicar num item; clicar nele; pô-lo no chão, no ar, junto
    /// de uma lateral ou perto da borda de cima; clique duplo (o esconderijo); esconder; pausar ou retomar; recolher.
    /// </summary>
    internal static Evento[] InteracaoComItens(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, ref bool posto)
    {
        if (s.Estado == Estado.Hidden) return [new CmdShow()];
        if (s.Lugar is not { } lugar) return [];
        var corpo = new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 20);
        ItemNoMundo[] visiveis = [.. s.Itens.Todos.Where(i => !i.NaMao && Maquina.ItemVisivel(s, i))];
        ItemNoMundo? item = visiveis.Length > 0 ? visiveis[rnd.Next(visiveis.Length)] : null;
        bool livreNoAlto = s.Estado is Estado.Climbing or Estado.Hanging && !s.PresoPeloUsuario;
        int sorteio = (posto || livreNoAlto) && item is not null && rnd.Next(2) == 0 ? 1 : rnd.Next(12);
        posto = false;
        switch (sorteio)
        {
            case 0:
                return [new CmdSummonItem((Item)rnd.Next(13))];
            case 1 or 2 when item is not null:
                return ApoioDosItens.Arraste(item, Cenario.MeioDoPersonagem(s, cfg));
            case 3 when item is not null:
                return ApoioDosItens.Arraste(item, new PontoPx(rnd.Next(0, 1920), rnd.Next(60, 1032)));
            case 4 when item is not null:
                return [new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 5)), new ItemRelease(item.Id)];
            case 5:
                return [new Press(corpo), new Click()];
            case 6 or 7:
            {
                posto = true;
                Superficies sup = ApoioDosItens.Sup(s);
                PontoPx destino = rnd.Next(4) switch
                {
                    0 => new PontoPx(rnd.Next(sup.Esquerda, sup.Direita + 1), sup.Chao),
                    1 => new PontoPx(rnd.Next(2) == 0 ? sup.Direita - 10 : sup.Esquerda + 10, rnd.Next(sup.Teto + 100, sup.Chao - 100)),
                    2 => new PontoPx(rnd.Next(sup.Esquerda + 100, sup.Direita - 100), sup.Teto + 20),
                    _ => new PontoPx(rnd.Next(sup.Esquerda, sup.Direita + 1), rnd.Next(sup.Teto, sup.Chao)),
                };
                var cursor = new PontoPx(destino.X, destino.Y - 20);
                return [new Press(corpo), new DragStart(), new DragMove(cursor), new DragEnd(cursor)];
            }
            case 8:
                posto = true;
                return [new Press(corpo), new Click(), new Press(corpo), new DoubleClick()];
            case 9:
                return [new CmdHide()];
            case 10:
                return [s.AutonomiaPausada ? new CmdResumeAutonomy() : new CmdPauseAutonomy()];
            case 11 when rnd.Next(4) == 0:
                return [new CmdClearItems()];
            default:
                return [];
        }
    }

    /// <summary>Uma tabela de ondas com a subida de 1 s e o pico de duas horas: a onda fica no pico durante toda a simulação.</summary>
    private static DadosDaOnda PicoLongo(Onda onda) => TabelaDoTamagotchi.DaOnda(onda) with { Subida = TimeSpan.FromSeconds(1), NivelDoPico = TimeSpan.FromHours(2) };

    // D9 e D10: com o pico do elétrico, a agenda decide mais vezes e ele passa mais tempo em movimento; com o do chapado,
    // menos (10 minutos por semente, na configuração do aplicativo, somados em 8 sementes, com a onda pelo item).
    [Teste]
    public static void Eletrico_MaisAcoesChapado_Menos()
    {
        (long Acoes, long EmMovimento) Simular(Item? item)
        {
            long acoes = 0, emMovimento = 0;
            for (ulong semente = 1; semente <= 8; semente++)
            {
                var sim = new SimuladorDeTempo(Ligado() with { TabelaDeOndas = PicoLongo }, semente, TopologiasDeExemplo.UmMonitor);
                if (item is { } i)
                {
                    ApoioDosItens.SoltarSobreEle(sim, ApoioDosItens.InvocarEAssentar(sim, i).Id);
                    sim.Passos(sim.Estado.PassosRestantes);
                    sim.Avancar(TimeSpan.FromSeconds(2));
                    Afirmar.Igual(FaseDaOnda.Pico, sim.Estado.Onda!.Fase, $"{i}: no pico");
                }
                sim.AoResultado = (antes, e, r) =>
                {
                    if (e is AutonomyTimer && (r.Transicoes.Count > 0 || r.Estado.Gesto != antes.Gesto || r.Estado.Expressao != antes.Expressao)) acoes++;
                    if (e is Tick && r.Estado.Estado.EmMovimento()) emMovimento++;
                };
                sim.Avancar(TimeSpan.FromMinutes(10));
            }
            return (acoes, emMovimento);
        }
        (long acoes, long movimento) semOnda = Simular(null), eletrico = Simular(Item.Cocaina), chapado = Simular(Item.Baseado);
        Console.WriteLine($"         ações e passos em movimento: elétrico {eletrico}, sem onda {semOnda}, chapado {chapado}");
        Afirmar.Verdadeiro(eletrico.acoes > semOnda.acoes && semOnda.acoes > chapado.acoes, $"ações: elétrico {eletrico.acoes} > sem onda {semOnda.acoes} > chapado {chapado.acoes}");
        Afirmar.Verdadeiro(eletrico.movimento > semOnda.movimento && semOnda.movimento > chapado.movimento,
            $"em movimento: elétrico {eletrico.movimento} > sem onda {semOnda.movimento} > chapado {chapado.movimento}");
    }

    // D12 e invariante 15: os gestos da onda (do pico do bêbado: soluço, dança e gargalhada) só acontecem em IDLE e terminam
    // com qualquer evento de prioridade maior que a do relógio, inclusive um gesto sobre um item ou o menu dos itens.
    [Teste]
    public static void GestosDaOnda_SoEmIdleEInterrompidos()
    {
        var gestos = new Dictionary<Gesto, int>();
        int interrompidos = 0;
        for (ulong semente = 1; semente <= 6; semente++)
        {
            var sim = new SimuladorDeTempo(Ligado() with { TabelaDeOndas = PicoLongo, Acoes = AcoesAutonomas.Gesto }, semente, TopologiasDeExemplo.UmMonitor);
            ItemNoMundo outro = ApoioDosItens.InvocarEAssentar(sim, Item.Agua);
            ApoioDosItens.SoltarSobreEle(sim, ApoioDosItens.InvocarEAssentar(sim, Item.Vodka).Id);
            sim.Passos(sim.Estado.PassosRestantes);
            sim.AoResultado = (antes, e, r) =>
            {
                if (r.Estado.Gesto != Gesto.Nenhum) Afirmar.Igual(Estado.Idle, r.Estado.Estado, $"semente {semente}: gesto {r.Estado.Gesto} só em IDLE");
                if (antes.Gesto != Gesto.Nenhum && e.Origem > Origem.Relogio) Afirmar.Igual(Gesto.Nenhum, r.Estado.Gesto, $"semente {semente}: {e} encerra o gesto");
            };
            Evento[] interrupcoes = [new CmdSummonItem(Item.Banana), new ItemPress(outro.Id, new PontoPx(outro.Lugar.Ancora.X, outro.Lugar.Ancora.Y - 5)), new ItemRelease(outro.Id), new CmdClearItems()];
            for (int i = 0; i < 40; i++)
            {
                sim.Avancar(TimeSpan.FromMinutes(1), s => s.Gesto != Gesto.Nenhum);
                if (sim.Estado.Gesto == Gesto.Nenhum) continue;
                gestos[sim.Estado.Gesto] = gestos.GetValueOrDefault(sim.Estado.Gesto) + 1;
                Evento interrupcao = interrupcoes[i % interrupcoes.Length];
                if (interrupcao is ItemPress or ItemRelease && sim.Estado.Itens.PorId(outro.Id) is null) interrupcao = new CmdSummonItem(Item.Agua);
                sim.Aplicar(interrupcao);
                Afirmar.Igual(Gesto.Nenhum, sim.Estado.Gesto, $"{interrupcao} encerrou o gesto");
                interrompidos++;
                sim.Aplicar(new ItemRelease(outro.Id));
            }
        }
        Console.WriteLine($"         gestos da onda: {string.Join(", ", gestos.Select(g => $"{g.Key}={g.Value}"))}; {interrompidos} interrompidos");
        Afirmar.Sequencia(new[] { Gesto.Soluco, Gesto.Danca, Gesto.Gargalhada }.Order(), gestos.Keys.Order(), "só os gestos do pico do bêbado, todos");
        Afirmar.Verdadeiro(interrompidos >= 30, $"gestos interrompidos: {interrompidos}");
    }
}
