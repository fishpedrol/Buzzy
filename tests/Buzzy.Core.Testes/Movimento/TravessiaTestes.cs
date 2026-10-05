using Buzzy.Core.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.Movimento.MovimentoTestes;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Fase 5, passo P13 — a travessia plana entre monitores (desenho da travessia, D7, D8 e 4.1, conciliado com a toon force
/// pela crítica de integração, C14 e C15; DEC-032). Andando até uma porta plana, com a travessia ligada na configuração e
/// na preferência e sem calma, a agenda sorteia entre atravessar e o caminho de sempre da parede (DEC-023). A travessia é
/// atômica: nenhuma parada, meia-volta ou decisão até o sprite estar inteiro no destino; a pausa e o item na mão esperam o
/// fim; o clique a interrompe; uma mudança de topologia a desfaz. O monitor da âncora troca exatamente na borda, pela
/// âncora arredondada. O relógio é virtual (<see cref="SimuladorDeTempo"/>); nada abre janela.
/// </summary>
internal static class TravessiaTestes
{
    /// <summary>A Fase 5 com a travessia ligada e um peso de atravessar alto: na porta, ele quase sempre atravessa.</summary>
    internal static ConfiguracaoDoNucleo Fase5(AcoesAutonomas acoes = AcoesAutonomas.Andar, int pesoAtravessar = 1000)
        => Fase4(acoes) with { Travessia = true, Perfil = n => PerfilDeEnergia.Padrao(n) with { PesoAtravessar = pesoAtravessar } };

    /// <summary>
    /// O apoio da travessia: fora dela, o de sempre (<see cref="MovimentoTestes.ConferirApoio"/>); no meio dela (andando, ou
    /// no voo do salto de degrau), o sprite inteiro na união das áreas úteis e, andando, os pés no chão do monitor da âncora.
    /// A caminhada até a partida de um salto planejado ainda não é travessia.
    /// </summary>
    internal static void ConferirApoioNaTravessia(EstadoDoNucleo s, string onde)
    {
        Travessia? tr = s.Movimento.Travessia;
        bool atravessando = tr is not null && (s.Estado == Estado.Jumping || (s.Estado == Estado.Walking && tr.Tipo == TipoDeTravessia.Andando));
        if (!atravessando)
        {
            ConferirApoio(s, onde);
            return;
        }
        Posicionamento l = Afirmar.NaoNulo(s.Lugar, "lugar");
        Topologia t = Afirmar.NaoNulo(s.Topologia, "topologia");
        if (s.Estado == Estado.Walking) Afirmar.Igual(l.Monitor.AreaUtil.Base, l.Ancora.Y, $"{onde}: atravessando com os pés no chão");
        Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(t, l.Retangulo), $"{onde}: atravessando com o sprite {l.Retangulo} na união das áreas úteis");
    }

    /// <summary>
    /// Avança até ele estar no meio de uma travessia do principal para o monitor da esquerda, montado entre os dois, com a
    /// primeira semente, de 1 em diante, em que isso acontece em 10 minutos; devolve o simulador.
    /// </summary>
    private static SimuladorDeTempo NoMeioDaTravessia(ConfiguracaoDoNucleo? cfg = null)
    {
        static bool Montado(EstadoDoNucleo s) => s.Movimento.Travessia is { ChaveDestino: TopologiasDeExemplo.Display2 } && s.Lugar is { } l && l.Retangulo.Esquerda < 0 && l.Retangulo.Direita > 0;
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var sim = new SimuladorDeTempo(cfg ?? Fase5(), semente, TopologiasDeExemplo.SecundarioAEsquerda);
            sim.Avancar(TimeSpan.FromMinutes(10), Montado);
            if (Montado(sim.Estado)) return sim;
        }
        throw new InvalidOperationException("Nenhuma semente de 1 a 50 atravessou para a esquerda em 10 minutos.");
    }

    // S2 (a máquina do usuário): atravessa andando para o monitor em x negativo e volta. O monitor da âncora troca exatamente
    // na borda x = 0, pela âncora arredondada (âncora negativa: o da esquerda), sem passo maior que 2 px; nunca para nem decide
    // montado entre os dois; e o apoio vale em cada evento.
    [Teste]
    public static void S2_AtravessaAndandoParaXNegativoEVolta()
    {
        var sim = new SimuladorDeTempo(Fase5(), 5, TopologiasDeExemplo.SecundarioAEsquerda);
        int idas = 0, voltas = 0;
        PontoPx? anterior = null;
        string? monitorAnterior = null;
        sim.AoAplicar = (_, e, depois) =>
        {
            ConferirApoioNaTravessia(depois, $"{e}");
            if (depois.Lugar is not { } l) return;
            if (anterior is { } a && depois.Estado == Estado.Walking && e is Tick)
                Afirmar.Verdadeiro(Math.Abs(l.Ancora.X - a.X) <= 2, $"passo de {l.Ancora.X - a.X} px");
            if (depois.Estado is Estado.Idle or Estado.Resting)
                Afirmar.Falso(l.Retangulo.Esquerda < 0 && l.Retangulo.Direita > 0, $"{depois.Estado} montado entre os dois monitores ({l.Retangulo})");
            if (depois.Estado == Estado.Walking)
                Afirmar.Igual(l.Ancora.X < 0, l.Monitor.Chave == TopologiasDeExemplo.Display2, $"âncora {l.Ancora.X} no monitor {l.Monitor.Chave}: a troca é na borda x = 0");
            if (monitorAnterior is not null && monitorAnterior != l.Monitor.Chave)
            {
                if (l.Monitor.Chave == TopologiasDeExemplo.Display2) idas++;
                else voltas++;
            }
            anterior = l.Ancora;
            monitorAnterior = l.Monitor.Chave;
        };
        sim.Avancar(TimeSpan.FromMinutes(20), s => voltas > 0);
        Afirmar.Verdadeiro(idas > 0 && voltas > 0, $"atravessou {idas} vez(es) para a esquerda e voltou {voltas}");
    }

    // A travessia começa na borda, registrada, e termina com o sprite inteiro no destino (âncora em −64, para um sprite de
    // 128 px a 96 DPI); em seguida, ele segue o percurso ou para.
    [Teste]
    public static void Travessia_ComecaNaBordaETerminaComOSpriteInteiroNoDestino()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia();
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "WALKING: passagem (atravessa)"), "o começo registrado");
        Travessia tr = Afirmar.NaoNulo(sim.Estado.Movimento.Travessia, "o plano");
        Afirmar.Igual((TopologiasDeExemplo.Display1, TopologiasDeExemplo.Display2, -1, 0), (tr.ChaveOrigem, tr.ChaveDestino, tr.Lado, tr.Borda), "do principal para a esquerda, pela borda x = 0");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Movimento.Travessia is null);
        Afirmar.Nulo(sim.Estado.Movimento.Travessia, "a travessia acabou");
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual(TopologiasDeExemplo.Display2, l.Monitor.Chave, "no monitor da esquerda");
        Afirmar.Verdadeiro(l.Ancora.X <= -64, $"o sprite inteiro no destino (âncora {l.Ancora.X})");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "WALKING: travessia completa"), "o fim registrado");
    }

    // Pausar no meio não para montado: a travessia termina, e só depois ele para (D7).
    [Teste]
    public static void PausaNoMeio_ATravessiaTerminaEDepoisPara()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia();
        sim.Aplicar(new CmdPauseAutonomy());
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "a pausa não para no meio");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado == Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parou depois");
        Afirmar.Verdadeiro(l.Ancora.X <= -64 && l.Monitor.Chave == TopologiasDeExemplo.Display2, $"inteiro no destino ({l.Ancora.X})");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "WALKING: fim do percurso depois da travessia"), "a regra da parada");
    }

    // O clique no meio interrompe: PRESSED montado entre os dois e, ao soltar, a acomodação o deixa inteiro num monitor.
    [Teste]
    public static void CliqueNoMeio_InterrompeEAAcomodacaoOPoeNumMonitor()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia();
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        var corpo = new PontoPx(l.Ancora.X, l.Ancora.Y - 40);
        sim.Aplicar(new Press(corpo));
        Afirmar.Igual(Estado.Pressed, sim.Estado.Estado, "PRESSED no meio da travessia");
        Afirmar.Nulo(sim.Estado.Movimento.Travessia, "o plano caiu");
        sim.Aplicar(new Click());
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado is Estado.Idle or Estado.Walking);
        Posicionamento depois = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Verdadeiro(depois.Monitor.AreaUtil.Contem(depois.Retangulo), $"inteiro num monitor ({depois.Retangulo})");
    }

    // Uma mudança de topologia no meio desfaz o plano e acomoda (C15), mesmo se só o outro monitor mudou.
    [Teste]
    public static void TopologiaMudaNoMeio_DesfazOPlanoEAcomoda()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia();
        Topologia semOVizinho = TopologiasDeExemplo.SemMonitor(TopologiasDeExemplo.SecundarioAEsquerda, TopologiasDeExemplo.Display2);
        sim.Aplicar(new TopologyChanged(semOVizinho));
        Afirmar.Nulo(sim.Estado.Movimento.Travessia, "o plano caiu");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra.StartsWith("TOPOLOGY_CHANGED: travessia interrompida", StringComparison.Ordinal)), "a regra");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado is Estado.Idle or Estado.Walking);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual(TopologiasDeExemplo.Display1, l.Monitor.Chave, "no único monitor que sobrou");
        Afirmar.Verdadeiro(l.Monitor.AreaUtil.Contem(l.Retangulo), $"inteiro nele ({l.Retangulo})");
    }

    // Com a preferência desligada (Q-05), a lateral continua parede, como na Fase 4: ele nunca troca de monitor.
    [Teste]
    public static void PreferenciaDesligada_NuncaAtravessa()
    {
        var prefs = new Preferencias(NivelDeEnergia.Media, true, AtravessarMonitores: false);
        for (ulong semente = 1; semente <= 10; semente++)
        {
            var sim = new SimuladorDeTempo(Fase5(AcoesAutonomas.Andar | AcoesAutonomas.Escalar), semente, TopologiasDeExemplo.SecundarioAEsquerda, prefs);
            sim.AoAplicar = (_, _, depois) =>
            {
                if (depois.Lugar is { } l) Afirmar.Igual(TopologiasDeExemplo.Display1, l.Monitor.Chave, $"semente {semente}: continua no principal");
            };
            sim.Avancar(TimeSpan.FromMinutes(4));
        }
    }

    // Sem porta (um vão entre os monitores, ou só a quina encostada), não há travessia.
    [Teste]
    public static void VaoEQuina_NuncaAtravessam()
    {
        foreach ((string nome, Topologia t) in new[] { ("VaoEntreMonitores", TopologiasDeExemplo.VaoEntreMonitores), ("QuinaComQuina", TopologiasDeExemplo.QuinaComQuina) })
        {
            for (ulong semente = 1; semente <= 10; semente++)
            {
                var sim = new SimuladorDeTempo(Fase5(AcoesAutonomas.Andar | AcoesAutonomas.Escalar | AcoesAutonomas.Pular), semente, t);
                sim.AoAplicar = (_, e, depois) =>
                {
                    if (depois.Lugar is { } l) Afirmar.Igual(TopologiasDeExemplo.Display1, l.Monitor.Chave, $"{nome}, semente {semente}: continua no principal");
                    ConferirApoioNaTravessia(depois, $"{nome}, semente {semente}, {e}");
                };
                sim.Avancar(TimeSpan.FromMinutes(3));
            }
        }
    }

    // Com a travessia desligada na configuração (a Fase 4), a lateral encostada continua só parede, com a toon force: o
    // teste da Fase 4 (Passagem_NaoEhAtravessadaNaFase4MasSeEscaladaComToonForce) continua valendo com a Fase 4.
    [Teste]
    public static void ConfiguracaoSemTravessia_NuncaAtravessa()
    {
        for (ulong semente = 1; semente <= 10; semente++)
        {
            var sim = new SimuladorDeTempo(Fase4(AcoesAutonomas.Andar), semente, TopologiasDeExemplo.SecundarioAEsquerda);
            sim.AoAplicar = (_, _, depois) =>
            {
                if (depois.Lugar is { } l) Afirmar.Igual(TopologiasDeExemplo.Display1, l.Monitor.Chave, $"semente {semente}: continua no principal");
                Afirmar.Nulo(depois.Movimento.Travessia, "nenhum plano de travessia");
            };
            sim.Avancar(TimeSpan.FromMinutes(4));
        }
    }

    // Com a toon force (DEC-023), a lateral com porta continua escalável: com um peso de atravessar baixo, numa parte das
    // chegadas à porta ele sobe por ela; noutras, atravessa.
    [Teste]
    public static void NaPorta_ASorteiaEntreAtravessarEOCaminhoDaParede()
    {
        int atravessou = 0, subiu = 0;
        for (ulong semente = 1; semente <= 30; semente++)
        {
            var sim = new SimuladorDeTempo(Fase5(AcoesAutonomas.Andar | AcoesAutonomas.Escalar, pesoAtravessar: 3), semente, TopologiasDeExemplo.SecundarioAEsquerda);
            bool a = false, s = false;
            sim.AoAplicar = (_, _, depois) =>
            {
                if (depois.Lugar is not { } l) return;
                if (l.Monitor.Chave == TopologiasDeExemplo.Display2) a = true;
                if (depois.Estado == Estado.Climbing && l.Monitor.Chave == TopologiasDeExemplo.Display1 && l.Ancora.X == 64) s = true;
            };
            sim.Avancar(TimeSpan.FromMinutes(6));
            if (a) atravessou++;
            if (s) subiu++;
        }
        Afirmar.Verdadeiro(atravessou > 0 && subiu > 0, $"atravessou em {atravessou} e subiu pela lateral da porta em {subiu} de 30 sementes");
    }

    // Propriedade (critério 2 com a travessia): simulações longas em topologias geradas, com o usuário pressionando,
    // arrastando e soltando, mudanças de topologia e pausas, todas as ações e o peso de atravessar do perfil. Em cada evento, o
    // apoio vale e, no meio de uma travessia, o sprite fica na união das áreas úteis. As travessias completas são contadas.
    [Teste]
    public static void ApoioEUniao_EmMilharesDePassosComATravessia()
    {
        var mestre = new Random(20261002);
        int completas = 0;
        for (int n = 0; n < 60; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia t = rnd.Next(3) == 0 ? TopologiasDeExemplo.SecundarioAEsquerda : gerador.NovaTopologia();
            var cfg = Fase4(AcoesAutonomas.Todas | AcoesAutonomas.IrAoOutroMonitor) with { Travessia = true };
            var sim = new SimuladorDeTempo(cfg, (ulong)semente, t, new Preferencias((NivelDeEnergia)rnd.Next(3), true));
            sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"sequência {n} (semente {semente}), {e}");
            for (int k = 0; k < 40; k++)
            {
                int antes = sim.Transicoes.Count;
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 30)));
                completas += sim.Transicoes.Skip(antes).Count(tr => tr.Regra == "WALKING: travessia completa");
                if (sim.Estado.Lugar is not { } l) continue;
                switch (rnd.Next(12))
                {
                    case 0:
                        sim.Aplicar(new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)));
                        sim.Aplicar(new DragStart());
                        PontoPx alvo = gerador.Ponto(t);
                        sim.Aplicar(new DragMove(alvo));
                        sim.Aplicar(new DragEnd(alvo));
                        break;
                    case 1:
                        sim.Aplicar(new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)));
                        sim.Aplicar(new Click());
                        break;
                    case 2:
                        // Às vezes, de volta à topologia do usuário, com uma porta plana.
                        t = rnd.Next(2) == 0 ? TopologiasDeExemplo.SecundarioAEsquerda : rnd.Next(3) == 0 ? gerador.NovaTopologia() : gerador.Mudar(t);
                        sim.Aplicar(new TopologyChanged(t));
                        break;
                    case 3:
                        sim.Aplicar(rnd.Next(2) == 0 ? new CmdPauseAutonomy() : new CmdResumeAutonomy());
                        break;
                }
            }
        }
        Afirmar.Verdadeiro(completas >= 10, $"travessias completas nas 60 sequências: {completas}");
    }

    // A ação autônoma de ir ao outro monitor (passo P13; DEC-032): ele anda até a porta plana do monitor em que está e
    // atravessa, sem o sorteio da porta, que é das caminhadas comuns; do outro lado, segue o resto do percurso ou para.
    [Teste]
    public static void IrAoOutroMonitor_AndaAtePortaEAtravessaSemSorteioNaPorta()
    {
        var cfg = Fase4(AcoesAutonomas.IrAoOutroMonitor) with { Travessia = true };
        var sim = new SimuladorDeTempo(cfg, 3, TopologiasDeExemplo.SecundarioAEsquerda);
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar?.Monitor.Chave == TopologiasDeExemplo.Display2 && s.Movimento.Travessia is null);
        Afirmar.Igual(TopologiasDeExemplo.Display2, sim.Estado.Lugar?.Monitor.Chave, "chegou ao monitor da esquerda");
        string[] regras = [.. sim.Transicoes.Select(t => t.Regra)];
        int decidiu = Array.IndexOf(regras, "IDLE + AUTONOMY_TIMER: ir ao outro monitor (anda até a porta)");
        int atravessou = Array.IndexOf(regras, "WALKING: passagem (atravessa)");
        Afirmar.Verdadeiro(decidiu >= 0 && atravessou > decidiu, $"decidiu e atravessou: {string.Join(" | ", regras)}");
        Afirmar.Falso(regras.Skip(decidiu).Take(atravessou - decidiu).Any(r => r.StartsWith("WALKING: parede", StringComparison.Ordinal)), "sem o caminho da parede na porta");
    }

    // Sem porta no monitor dele, ir ao outro monitor não é opção: com só essa ação, ele fica parado.
    [Teste]
    public static void IrAoOutroMonitor_SemPorta_NaoEhOpcao()
    {
        var cfg = Fase4(AcoesAutonomas.IrAoOutroMonitor) with { Travessia = true };
        // Só a quina encostada, ou um vão: nenhuma porta.
        foreach (Topologia t in new[] { TopologiasDeExemplo.UmMonitor, TopologiasDeExemplo.VaoEntreMonitores, TopologiasDeExemplo.QuinaComQuina })
        {
            var sim = new SimuladorDeTempo(cfg, 3, t);
            sim.Avancar(TimeSpan.FromMinutes(2));
            Afirmar.Falso(sim.Transicoes.Any(tr => tr.Para == Estado.Walking), "nunca anda");
        }
        var desligada = new SimuladorDeTempo(cfg, 3, TopologiasDeExemplo.SecundarioAEsquerda, new Preferencias(NivelDeEnergia.Media, true, AtravessarMonitores: false));
        desligada.Avancar(TimeSpan.FromMinutes(2));
        Afirmar.Falso(desligada.Transicoes.Any(tr => tr.Para == Estado.Walking), "com a preferência desligada, também não");
    }

    // (tamagotchi) Com um item na mão do usuário no meio da travessia, ele fica atento, mas só para quando a travessia acaba,
    // como na pausa: nunca parado montado entre os dois monitores.
    [Teste]
    public static void ItemNaMaoNoMeio_EsperaATravessiaAcabarParaParar()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia(Fase5() with { Tamagotchi = true });
        sim.Aplicar(new CmdSummonItem(Item.Banana));
        ItemNoMundo banana = Afirmar.NaoNulo(sim.Estado.Itens.Todos.SingleOrDefault(), "a banana nasceu");
        sim.Aplicar(new ItemPress(banana.Id, banana.Lugar.Ancora));
        Afirmar.Verdadeiro(sim.Estado.Atento, "segurando a banana");
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "continua atravessando");
        Afirmar.NaoNulo(sim.Estado.Movimento.Travessia, "com o plano");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado != Estado.Walking);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parou depois da travessia");
        Afirmar.Verdadeiro(l.Ancora.X <= -64 && l.Monitor.Chave == TopologiasDeExemplo.Display2, $"inteiro no destino ({l.Ancora.X})");
    }

    // ---------------------------------------------------------------- P13b: o salto de degrau

    private static ConfiguracaoDoNucleo IrAoOutro() => Fase4(AcoesAutonomas.IrAoOutroMonitor) with { Travessia = true };

    // S4 (degraus desalinhados): indo ao outro monitor, ele anda até a partida, salta (WALKING → JUMPING), voa com o sprite
    // sempre na união das áreas úteis e pousa no chão do monitor de baixo (LANDING), 400 DIP abaixo.
    [Teste]
    public static void S4_DesceODegrauNumSalto()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar?.Monitor.Chave == TopologiasDeExemplo.Display2 && s.Estado == Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual((TopologiasDeExemplo.Display2, 1432), (l.Monitor.Chave, l.Ancora.Y), "no chão do monitor de baixo");
        string[] regras = [.. sim.Transicoes.Select(tr => tr.Regra)];
        int saltou = Array.IndexOf(regras, "WALKING: degrau alcançável (salto de travessia)");
        int completo = Array.IndexOf(regras, "JUMPING: salto de travessia completo");
        int pousou = Array.IndexOf(regras, "JUMPING: contato com o chão");
        Afirmar.Verdadeiro(saltou >= 0 && completo > saltou && pousou > completo, $"saltou, completou e pousou: {string.Join(" | ", regras)}");
    }

    // Escalas mistas (150% no meio, 100% à direita e 200% à esquerda, com chãos 24 px diferentes): ele sobe e desce degraus
    // pequenos nos dois sentidos, e o tamanho do sprite é sempre o do monitor da âncora, trocando na borda.
    [Teste]
    public static void EscalasMistas_SobeEDesceDegrausPequenos_OTamanhoTrocaNaBorda()
    {
        var tamanho = new TamanhoDip(128, 128);
        var sim = new SimuladorDeTempo(IrAoOutro(), 5, TopologiasDeExemplo.EscalasMistas);
        int subidas = 0, descidas = 0;
        int? chaoAntes = null;
        sim.AoAplicar = (_, e, depois) =>
        {
            ConferirApoioNaTravessia(depois, $"{e}");
            if (depois.Lugar is not { } l) return;
            Afirmar.Igual(tamanho.ParaPixels(l.Monitor.Dpi), l.Tamanho, $"{e}: o tamanho do monitor da âncora ({l.Monitor.Chave})");
            if (depois.Estado == Estado.Idle)
            {
                int chao = l.Monitor.AreaUtil.Base;
                if (chaoAntes is { } c && c != chao)
                {
                    if (chao < c) subidas++;
                    else descidas++;
                }
                chaoAntes = chao;
            }
        };
        sim.Avancar(TimeSpan.FromMinutes(20), _ => subidas > 0 && descidas > 0);
        Afirmar.Verdadeiro(subidas > 0 && descidas > 0, $"subiu {subidas} e desceu {descidas} degrau(s)");
    }

    // Pausar a caminhada até a partida de um salto planejado para ele, antes do salto: ainda não é travessia (D7 vale do salto
    // em diante).
    [Teste]
    public static void PausaAntesDaPartida_ParaSemSaltar()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Walking && s.Movimento.Travessia is { Tipo: TipoDeTravessia.Salto });
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "a caminhada até a partida");
        sim.Aplicar(new CmdPauseAutonomy());
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parou");
        Afirmar.Nulo(sim.Estado.Movimento.Travessia, "sem o plano");
        Afirmar.Igual(TopologiasDeExemplo.Display1, sim.Estado.Lugar?.Monitor.Chave, "no monitor de origem");
    }

    // Uma mudança de topologia no meio do voo desfaz o plano e acomoda (C15).
    [Teste]
    public static void TopologiaMudaNoVoo_DesfazOPlanoEAcomoda()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Jumping && s.Movimento.Travessia is { Passo: > 5 });
        Afirmar.Igual(Estado.Jumping, sim.Estado.Estado, "no meio do salto");
        sim.Aplicar(new TopologyChanged(TopologiasDeExemplo.UmMonitor));
        Afirmar.Nulo(sim.Estado.Movimento.Travessia, "o plano caiu");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado is Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Verdadeiro(l.Monitor.AreaUtil.Contem(l.Retangulo), $"inteiro num monitor ({l.Retangulo})");
    }

    // ---------------------------------------------------------------- P13c: o transbordo

    // S4 de volta: do monitor de baixo, o degrau de 400 DIP para cima passa do alcance do salto; indo ao outro monitor, ele
    // anda até a lateral, escala a parede abaixo da porta e, com os pés na altura do chão do vizinho, transborda para ele.
    [Teste]
    public static void S4_SobeODegrauPelaParedeETransborda()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromMinutes(2), s => s.Lugar?.Monitor.Chave == TopologiasDeExemplo.Display2 && s.Estado == Estado.Idle);
        int antes = sim.Transicoes.Count;
        sim.Avancar(TimeSpan.FromMinutes(3), s => s.Lugar?.Monitor.Chave == TopologiasDeExemplo.Display1 && s.Estado == Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual((TopologiasDeExemplo.Display1, 1032), (l.Monitor.Chave, l.Ancora.Y), "de volta ao chão do monitor de cima");
        string[] regras = [.. sim.Transicoes.Skip(antes).Select(tr => tr.Regra)];
        int escalou = Array.FindIndex(regras, r => r.StartsWith("WALKING: parede (andava até ela para escalar)", StringComparison.Ordinal));
        int transbordou = escalou < 0 ? -1 : Array.IndexOf(regras, "CLIMBING: transbordo para o chão do vizinho", escalou);
        int pousou = transbordou < 0 ? -1 : Array.IndexOf(regras, "JUMPING: contato com o chão", transbordou);
        Afirmar.Verdadeiro(escalou >= 0 && transbordou > escalou && pousou > transbordou, $"escalou, transbordou e pousou: {string.Join(" | ", regras)}");
    }

    // ---------------------------------------------------------------- DEC-034: a tela cheia fecha as portas

    private static FullscreenTargetsChanged Ocupado(params string[] chaves) => new(new MonitoresOcupados(chaves));

    private static bool NoVizinho(EstadoDoNucleo s) => s.Lugar?.Monitor.Chave == TopologiasDeExemplo.Display2;

    // Com o vizinho ocupado pela tela cheia e o modo ligado, a autonomia nunca o leva para lá: a lateral que encosta nele é
    // parede, para andar, saltar o degrau ou transbordar, e ir ao outro monitor deixa de ser opção. O controle, com as
    // mesmas sementes e sem a tela cheia, atravessa: o teste não passa por falta de oportunidade.
    [Teste]
    public static void VizinhoOcupado_AAutonomiaNuncaVaiParaEle()
    {
        (string Nome, Topologia Topologia, ConfiguracaoDoNucleo Cfg)[] casos =
        [
            ("porta plana", TopologiasDeExemplo.SecundarioAEsquerda, Fase5(AcoesAutonomas.Andar | AcoesAutonomas.Escalar | AcoesAutonomas.IrAoOutroMonitor)),
            ("degrau", TopologiasDeExemplo.DegrauDesalinhado, IrAoOutro()),
        ];
        foreach ((string nome, Topologia topologia, ConfiguracaoDoNucleo cfg) in casos)
        {
            int controles = 0;
            for (ulong semente = 1; semente <= 3; semente++)
            {
                var controle = new SimuladorDeTempo(cfg, semente, topologia);
                controle.Avancar(TimeSpan.FromMinutes(5), NoVizinho);
                if (NoVizinho(controle.Estado)) controles++;

                var sim = new SimuladorDeTempo(cfg, semente, topologia);
                sim.Aplicar(Ocupado(TopologiasDeExemplo.Display2));
                sim.AoAplicar = (_, e, depois) =>
                {
                    ConferirApoioNaTravessia(depois, $"{nome}, semente {semente}: {e}");
                    Afirmar.Falso(NoVizinho(depois), $"{nome}, semente {semente}: foi para o monitor ocupado ({e})");
                };
                sim.Avancar(TimeSpan.FromMinutes(5));
            }
            Afirmar.Verdadeiro(controles > 0, $"{nome}: sem a tela cheia, nenhuma semente atravessou em 5 minutos");
        }
    }

    // Com o modo desligado, a tela cheia só fica no cache (DEC-013): a porta continua aberta.
    [Teste]
    public static void ModoDesligado_OcupadoNaoFechaAPorta()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.SecundarioAEsquerda, new Preferencias(NivelDeEnergia.Media, false));
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display2));
        sim.Avancar(TimeSpan.FromMinutes(2), s => NoVizinho(s) && s.Movimento.Travessia is null);
        Afirmar.Verdadeiro(NoVizinho(sim.Estado) && sim.Estado.Movimento.Travessia is null, "com o modo desligado, atravessa até o fim");
        Afirmar.Falso(sim.Transicoes.Any(t => t.Regra.Contains("volta pela porta", StringComparison.Ordinal)), "e não volta pela porta");
    }

    // A tela cheia ocupa o destino no meio da travessia andando, com a âncora ainda na origem: a travessia é atômica e
    // termina; na chegada, a porta fechou, e ele volta inteiro para a origem, encostado na lateral da porta, sem retorno
    // guardado. O fim da tela cheia não o move.
    [Teste]
    public static void DestinoOcupadoNoMeioDaTravessia_TerminaEVoltaPelaPorta()
    {
        SimuladorDeTempo sim = NoMeioDaTravessia();
        Afirmar.Verdadeiro(sim.Estado.Lugar!.Ancora.X >= 0, $"a âncora ainda na origem ({sim.Estado.Lugar.Ancora.X})");
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display2));
        Afirmar.Igual(Estado.Walking, sim.Estado.Estado, "a travessia continua");
        Afirmar.NaoNulo(sim.Estado.Movimento.Travessia, "com o plano");
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado == Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual(Estado.Idle, sim.Estado.Estado, "parado");
        Afirmar.Igual((TopologiasDeExemplo.Display1, 64, 1032), (l.Monitor.Chave, l.Ancora.X, l.Ancora.Y), "de volta à origem, encostado na porta");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "WALKING: o destino da travessia ficou ocupado pela tela cheia (volta pela porta)"), "a regra da volta");
        Afirmar.Nulo(sim.Estado.RetornoDaTelaCheia, "sem retorno guardado");
        sim.Aplicar(Ocupado());
        Afirmar.Igual(new PontoPx(64, 1032), sim.Estado.Lugar!.Ancora, "o fim da tela cheia não o move");
    }

    // O mesmo no salto de degrau: o destino ocupado logo depois da decolagem; o salto termina e ele volta pela porta. E o salto
    // planejado, na caminhada até a partida, cai: ele para sem saltar.
    [Teste]
    public static void DestinoOcupadoNoSalto_VoltaPelaPortaEOPlanejadoCai()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        sim.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Jumping && s.Movimento.Travessia is { Passo: 1 });
        Afirmar.Igual(Estado.Jumping, sim.Estado.Estado, "decolou");
        Afirmar.Igual(TopologiasDeExemplo.Display1, sim.Estado.Lugar?.Monitor.Chave, "ainda na origem");
        sim.Aplicar(Ocupado(TopologiasDeExemplo.Display2));
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromSeconds(5), s => s.Estado == Estado.Idle);
        Posicionamento l = Afirmar.NaoNulo(sim.Estado.Lugar, "lugar");
        Afirmar.Igual((TopologiasDeExemplo.Display1, 1856, 1032), (l.Monitor.Chave, l.Ancora.X, l.Ancora.Y), "de volta à origem, encostado na porta");
        Afirmar.Verdadeiro(sim.Transicoes.Any(t => t.Regra == "JUMPING: o destino do salto ficou ocupado pela tela cheia (volta pela porta)"), "a regra da volta");

        var planejado = new SimuladorDeTempo(IrAoOutro(), 3, TopologiasDeExemplo.DegrauDesalinhado);
        planejado.Avancar(TimeSpan.FromMinutes(1), s => s.Estado == Estado.Walking && s.Movimento.Travessia is { Tipo: TipoDeTravessia.Salto });
        Afirmar.Igual(Estado.Walking, planejado.Estado.Estado, "a caminhada até a partida");
        planejado.Aplicar(Ocupado(TopologiasDeExemplo.Display2));
        Afirmar.Nulo(planejado.Estado.Movimento.Travessia, "o plano caiu");
        planejado.AoAplicar = (_, e, depois) => Afirmar.Falso(NoVizinho(depois) || depois.Estado == Estado.Jumping, $"saltou para o monitor ocupado ({e})");
        planejado.Avancar(TimeSpan.FromMinutes(1));
    }

    // Três monitores: do principal, os dois vizinhos estão acima do alcance do salto (168 e 288 DIP), e ele chega a eles pelo
    // transbordo.
    [Teste]
    public static void TresMonitores_ChegaAosVizinhosMaisAltosPeloTransbordo()
    {
        var sim = new SimuladorDeTempo(IrAoOutro(), 7, TopologiasDeExemplo.TresMonitores);
        sim.AoAplicar = (_, e, depois) => ConferirApoioNaTravessia(depois, $"{e}");
        sim.Avancar(TimeSpan.FromMinutes(3), s => s.Lugar?.Monitor.Chave != TopologiasDeExemplo.Display1 && s.Estado == Estado.Idle);
        Afirmar.Diferente(TopologiasDeExemplo.Display1, sim.Estado.Lugar?.Monitor.Chave, "saiu do principal");
        Afirmar.Verdadeiro(sim.Transicoes.Any(tr => tr.Regra == "CLIMBING: transbordo para o chão do vizinho"), "pelo transbordo");
    }
}
