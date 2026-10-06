using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// Emoção dominante (DEC-027, pedido do usuário): uma das 14 caras de humor, escolhida pelo menu, vira a cara de
/// base e a mais sorteada nas trocas de expressão; "Automática" (nula) é o comportamento de antes. Ela só muda as
/// caras: nunca as ações, os pesos da agenda, a física nem a prioridade do usuário (invariante 27).
/// </summary>
internal static class EmocaoDominanteTestes
{
    // As 14 caras de humor na ordem de expressoes.png e do enum, e as companheiras de cada uma: a tabela do
    // desenho (núcleo, 4.7), escrita aqui à parte, como fonte independente.
    [Teste]
    public static void Expressoes_DeHumorECompanheirasSaoAsDaTabela()
    {
        Expressao[] quatorze =
        [
            Expressao.Neutro, Expressao.Feliz, Expressao.Rindo, Expressao.Curioso, Expressao.Surpreso, Expressao.Assustado, Expressao.Sonolento,
            Expressao.Bocejando, Expressao.Dormindo, Expressao.Travesso, Expressao.Entediado, Expressao.Pensativo, Expressao.Empolgado, Expressao.Determinado,
        ];
        Afirmar.Sequencia(quatorze, Expressoes.DeHumor, "as 14 caras de humor, na ordem de expressoes.png");
        foreach (Expressao e in quatorze) Afirmar.Verdadeiro(Expressoes.EhDeHumor(e), $"{e} é de humor");
        foreach (int fora in new[] { -1, 14, 20, 99, int.MaxValue, int.MinValue })
            Afirmar.Falso(Expressoes.EhDeHumor((Expressao)fora), $"{fora} não é de humor");

        (Expressao Dominante, Expressao[] Companheiras)[] tabela =
        [
            (Expressao.Neutro, [Expressao.Feliz, Expressao.Curioso, Expressao.Pensativo, Expressao.Entediado]),
            (Expressao.Feliz, [Expressao.Rindo, Expressao.Empolgado, Expressao.Travesso, Expressao.Curioso]),
            (Expressao.Rindo, [Expressao.Feliz, Expressao.Travesso, Expressao.Empolgado, Expressao.Surpreso]),
            (Expressao.Curioso, [Expressao.Pensativo, Expressao.Surpreso, Expressao.Feliz, Expressao.Travesso]),
            (Expressao.Surpreso, [Expressao.Assustado, Expressao.Curioso, Expressao.Empolgado, Expressao.Rindo]),
            (Expressao.Assustado, [Expressao.Surpreso, Expressao.Pensativo, Expressao.Curioso, Expressao.Neutro]),
            (Expressao.Sonolento, [Expressao.Bocejando, Expressao.Dormindo, Expressao.Entediado, Expressao.Neutro]),
            (Expressao.Bocejando, [Expressao.Sonolento, Expressao.Entediado, Expressao.Neutro, Expressao.Pensativo]),
            (Expressao.Dormindo, [Expressao.Sonolento, Expressao.Bocejando, Expressao.Neutro, Expressao.Feliz]),
            (Expressao.Travesso, [Expressao.Rindo, Expressao.Feliz, Expressao.Curioso, Expressao.Empolgado]),
            (Expressao.Entediado, [Expressao.Sonolento, Expressao.Bocejando, Expressao.Pensativo, Expressao.Neutro]),
            (Expressao.Pensativo, [Expressao.Curioso, Expressao.Neutro, Expressao.Entediado, Expressao.Determinado]),
            (Expressao.Empolgado, [Expressao.Feliz, Expressao.Rindo, Expressao.Surpreso, Expressao.Determinado]),
            (Expressao.Determinado, [Expressao.Empolgado, Expressao.Pensativo, Expressao.Neutro, Expressao.Feliz]),
        ];
        Afirmar.Sequencia(quatorze, tabela.Select(t => t.Dominante), "uma linha por cara de humor");
        foreach ((Expressao dominante, Expressao[] companheiras) in tabela)
            Afirmar.Sequencia(companheiras, Expressoes.Companheiras(dominante), $"companheiras de {dominante}");
        foreach (int fora in new[] { -1, 14, 99 })
            Afirmar.Lanca<ArgumentOutOfRangeException>(() => Expressoes.Companheiras((Expressao)fora), $"sem companheiras para {fora}");
    }

    // O comando do menu troca a cara na hora e grava a preferência (um GravarPreferencias com a emoção, e mais
    // nada); a escolha fica registrada como uma transição para o mesmo estado. Repetir a escolha não faz nada.
    // "Automática" grava e mantém a cara atual até a próxima troca.
    [Teste]
    public static void Escolher_TrocaACaraEGrava()
    {
        Cenario c = Cenario.Parado().Aplicar(new CmdSetDominantEmotion(Expressao.Pensativo));
        c.Esta(Estado.Idle).Percorreu(Estado.Idle, Estado.Idle);
        Afirmar.Igual("CMD_SET_DOMINANT_EMOTION: Pensativo", c.Transicoes[0].Regra, "regra");
        Afirmar.Igual(Expressao.Pensativo, c.Atual.Expressao, "a cara muda na hora");
        Afirmar.Igual(Expressao.Pensativo, c.Atual.Preferencias.EmocaoDominante, "a preferência");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "não se move");
        Afirmar.Sequencia([new GravarPreferencias(PreferenciasDeTeste.Completas with { EmocaoDominante = Expressao.Pensativo })], c.Efeitos, "só grava as preferências");
        Afirmar.Igual(Expressao.Pensativo, c.Retrato.EmocaoDominante, "o retrato mostra a escolha, para a marca do menu");

        c.Aplicar(new CmdSetDominantEmotion(Expressao.Pensativo)).SemTransicao();
        Afirmar.Sequencia([], c.Efeitos, "a mesma escolha de novo não faz nada");

        c.Aplicar(new CmdSetDominantEmotion(null)).Percorreu(Estado.Idle, Estado.Idle);
        Afirmar.Igual("CMD_SET_DOMINANT_EMOTION: Automatica", c.Transicoes[0].Regra, "regra da automática");
        Afirmar.Igual(Expressao.Pensativo, c.Atual.Expressao, "a automática mantém a cara até a próxima troca");
        Afirmar.Nulo(c.Atual.Preferencias.EmocaoDominante, "a preferência volta a ser a automática");
        Afirmar.Nulo(c.Retrato.EmocaoDominante, "o retrato também");
        Afirmar.Sequencia([new GravarPreferencias(PreferenciasDeTeste.Completas)], c.Efeitos, "e grava");
        c.Aplicar(new CmdSetDominantEmotion(null)).SemTransicao();
        Afirmar.Sequencia([], c.Efeitos, "automática de novo não faz nada");
    }

    // Só as 14 caras de humor valem, como os três níveis de energia (SECURITY.md 7): o comando com outro valor (as
    // caras de efeito que vêm depois, um valor adulterado) é ignorado, sem gravar; a carga e SETTINGS_CHANGED com um
    // valor fora delas valem "Automática". Antes da carga, o comando é ignorado mesmo com uma cara de humor: quem
    // traz a preferência gravada é a carga.
    [Teste]
    public static void ForaDasQuatorze_Ignorada()
    {
        foreach (int fora in new[] { 14, 20, 99, -1, int.MaxValue })
        {
            Cenario c = Cenario.Parado();
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(new CmdSetDominantEmotion((Expressao)fora)).SemTransicao();
            Afirmar.Sequencia([], c.Efeitos, $"comando com {fora}: sem efeito");
            Afirmar.Igual(antes, c.Atual, $"comando com {fora}: nada muda");

            Cenario configuracoes = Cenario.Parado().Aplicar(new SettingsChanged(PreferenciasDeTeste.Completas with { EmocaoDominante = (Expressao)fora }));
            Afirmar.Igual(PreferenciasDeTeste.Completas, configuracoes.Atual.Preferencias, $"SETTINGS_CHANGED com {fora}: automática");
            Afirmar.Igual(Expressao.Neutro, configuracoes.Atual.Expressao, $"SETTINGS_CHANGED com {fora}: a cara não muda");

            Cenario carga = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, PreferenciasDeTeste.Completas with { EmocaoDominante = (Expressao)fora }));
            Afirmar.Igual(PreferenciasDeTeste.Completas, carga.Atual.Preferencias, $"carga com {fora}: automática");
            Afirmar.Igual(Expressao.Neutro, carga.Atual.Expressao, $"carga com {fora}: a cara de sempre");
        }

        foreach (Evento antesDaCarga in new Evento[] { new Tick(), new CmdHide() })
        {
            Cenario c = new Cenario().Aplicar(antesDaCarga);
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(new CmdSetDominantEmotion(Expressao.Feliz)).SemTransicao();
            Afirmar.Sequencia([], c.Efeitos, $"antes da carga ({c.Atual.Estado}): sem efeito");
            Afirmar.Igual(antes, c.Atual, $"antes da carga ({c.Atual.Estado}): nada muda");
        }
    }

    // A carga com a emoção gravada (o settings.json traz a preferência) já começa com a cara dela, também escondido
    // por um pedido anterior à carga. SETTINGS_CHANGED com uma emoção nova a mostra na hora, como o menu, mas não
    // grava, como hoje; descansando, a cara espera ele acordar; antes da carga, quem decide a cara é a carga.
    [Teste]
    public static void Carga_ComDominante_ComecaComACara()
    {
        Preferencias travesso = PreferenciasDeTeste.Completas with { EmocaoDominante = Expressao.Travesso };
        Cenario c = new Cenario().Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, travesso)).Esta(Estado.Idle);
        Afirmar.Igual(Expressao.Travesso, c.Atual.Expressao, "começa com a cara da emoção");
        Afirmar.Igual(travesso, c.Atual.Preferencias, "com a preferência");
        Afirmar.Igual(Expressao.Travesso, c.Retrato.EmocaoDominante, "e o retrato a mostra");

        Cenario escondido = new Cenario().Aplicar(new CmdHide(), new Loaded(TopologiasDeExemplo.UmMonitor, null, travesso)).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        Afirmar.Igual(Expressao.Travesso, escondido.Atual.Expressao, "escondido antes da carga: a cara já é a da emoção");
        Afirmar.Igual(Expressao.Travesso, escondido.Aplicar(new CmdShow()).Esta(Estado.Idle).Atual.Expressao, "e reaparece com ela");

        Preferencias curioso = PreferenciasDeTeste.Completas with { EmocaoDominante = Expressao.Curioso };
        Cenario configuracoes = Cenario.Parado().Aplicar(new SettingsChanged(curioso)).SemTransicao().SemEfeito<GravarPreferencias>();
        Afirmar.Igual(Expressao.Curioso, configuracoes.Atual.Expressao, "SETTINGS_CHANGED: a cara muda na hora");
        Afirmar.Igual(curioso, configuracoes.Atual.Preferencias, "SETTINGS_CHANGED: a preferência");

        Cenario descansando = Cenario.Em(Estado.Resting).Aplicar(new SettingsChanged(curioso));
        Afirmar.Igual(Expressao.Sonolento, descansando.Atual.Expressao, "descansando, a cara espera");

        Cenario antesDaCarga = new Cenario().Aplicar(new SettingsChanged(curioso), new Loaded(TopologiasDeExemplo.UmMonitor, null, PreferenciasDeTeste.Completas));
        Afirmar.Igual(Expressao.Neutro, antesDaCarga.Atual.Expressao, "antes da carga, a cara de partida é a da carga");
        Afirmar.Igual(PreferenciasDeTeste.Completas, antesDaCarga.Atual.Preferencias, "e as preferências também");
    }

    // Com a emoção dominante, a troca de cara da agenda sorteia a dominante (peso 6) ou uma das quatro companheiras
    // (peso 1 cada): em 500 trocas, só elas aparecem, todas aparecem, e a dominante sai em pelo menos metade (o
    // esperado é 60%; a troca pode repetir a cara atual). Na automática, como antes: nunca repete a cara e passa
    // pelas 14.
    [Teste]
    public static void Dominante_Sai60PorCentoNasTrocas()
    {
        const int Trocas = 500;
        var soTrocarACara = new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.TrocarExpressao };
        foreach (Expressao dominante in Expressoes.DeHumor)
        {
            Cenario c = new Cenario(soTrocarACara, semente: 11)
                .Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, PreferenciasDeTeste.Completas with { EmocaoDominante = dominante }));
            var vezes = new Dictionary<Expressao, int>();
            for (int i = 0; i < Trocas; i++)
            {
                c.Decidir().SemTransicao().Esta(Estado.Idle);
                vezes[c.Atual.Expressao] = vezes.GetValueOrDefault(c.Atual.Expressao) + 1;
            }
            Expressao[] esperadas = [dominante, .. Expressoes.Companheiras(dominante)];
            Afirmar.Sequencia(esperadas.Order(), vezes.Keys.Order(), $"{dominante}: só a dominante e as companheiras, todas elas ({Contagem(vezes)})");
            Afirmar.Verdadeiro(vezes[dominante] >= Trocas / 2, $"{dominante}: a dominante sai em pelo menos metade das trocas ({Contagem(vezes)})");
        }

        Cenario automatica = new Cenario(soTrocarACara, semente: 11).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, PreferenciasDeTeste.Completas));
        var vistas = new HashSet<Expressao>();
        for (int i = 0; i < Trocas; i++)
        {
            Expressao antes = automatica.Atual.Expressao;
            automatica.Decidir().SemTransicao();
            Afirmar.Diferente(antes, automatica.Atual.Expressao, $"automática, troca {i}: nunca repete a cara");
            vistas.Add(automatica.Atual.Expressao);
        }
        Afirmar.Sequencia(Expressoes.DeHumor, vistas.Order(), "automática: passa pelas 14 caras de humor");
    }

    // Nos três ganchos, a cara volta à dominante: no fim da reação ao clique (que é feliz), no fim do pouso (depois
    // dos quiques de borracha, que são rindo) e ao acordar (que é sonolento). Escolhida no meio da reação ou do
    // descanso, a emoção espera o fim deles. Na automática, como antes: a cara da reação e a do quique ficam, e ele
    // acorda neutro.
    [Teste]
    public static void VoltaDepoisDeReacaoPousoEAcordar()
    {
        var cfg = new ConfiguracaoDoNucleo();
        Preferencias pensativo = PreferenciasDeTeste.Completas with { EmocaoDominante = Expressao.Pensativo };
        foreach (bool comDominante in new[] { true, false })
        {
            Preferencias preferencias = comDominante ? pensativo : PreferenciasDeTeste.Completas;
            string caso = comDominante ? "com a dominante" : "automática";

            Cenario reacao = new Cenario(cfg).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, preferencias), new Press(Cenario.PontoOpaco), new Click()).Esta(Estado.Reacting);
            Afirmar.Igual(Expressao.Feliz, reacao.Atual.Expressao, $"{caso}: reage feliz ao clique");
            reacao.Passos(cfg.PassosDaReacao).Esta(Estado.Idle);
            Afirmar.Igual(comDominante ? Expressao.Pensativo : Expressao.Feliz, reacao.Atual.Expressao, $"{caso}: fim da reação");

            Cenario descanso = new Cenario(cfg).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, preferencias));
            descanso.AplicarCom(cfg with { Acoes = AcoesAutonomas.Descansar }, new AutonomyTimer(descanso.Atual.Geracao)).Esta(Estado.Resting);
            Afirmar.Igual(Expressao.Sonolento, descanso.Atual.Expressao, $"{caso}: descansa sonolento");
            descanso.Decidir().Esta(Estado.Idle);
            Afirmar.Igual(Sinal.Acordou, descanso.Atual.Sinal, $"{caso}: acordou");
            Afirmar.Igual(comDominante ? Expressao.Pensativo : Expressao.Neutro, descanso.Atual.Expressao, $"{caso}: ao acordar");

            SimuladorDeTempo queda = SoltarDoAlto(preferencias);
            bool quicouRindo = false;
            while (queda.Estado.Estado is Estado.Falling or Estado.Jumping && queda.Passos(1) == 1)
                quicouRindo |= queda.Estado.Estado == Estado.Jumping && queda.Estado.Expressao == Expressao.Rindo;
            queda.Esta(Estado.Landing, $"{caso}: pousa depois dos quiques");
            Afirmar.Verdadeiro(quicouRindo, $"{caso}: quicou rindo");
            queda.Passos(cfg.PassosDoPouso);
            queda.Esta(Estado.Idle, $"{caso}: fim do pouso");
            Afirmar.Igual(comDominante ? Expressao.Pensativo : Expressao.Rindo, queda.Estado.Expressao, $"{caso}: a cara no fim do pouso");
        }

        Cenario noMeioDaReacao = Cenario.Em(Estado.Reacting).Aplicar(new CmdSetDominantEmotion(Expressao.Pensativo));
        Afirmar.Igual(Expressao.Feliz, noMeioDaReacao.Atual.Expressao, "escolhida no meio da reação: a cara da reação continua");
        Afirmar.Igual(Expressao.Pensativo, noMeioDaReacao.Atual.Preferencias.EmocaoDominante, "mas a escolha vale e é gravada");
        noMeioDaReacao.Passos(cfg.PassosDaReacao).Esta(Estado.Idle);
        Afirmar.Igual(Expressao.Pensativo, noMeioDaReacao.Atual.Expressao, "e entra no fim da reação");

        Cenario noMeioDoDescanso = Cenario.Em(Estado.Resting).Aplicar(new CmdSetDominantEmotion(Expressao.Pensativo));
        Afirmar.Igual(Expressao.Sonolento, noMeioDoDescanso.Atual.Expressao, "escolhida no descanso: continua sonolento");
        Afirmar.Igual(Expressao.Pensativo, noMeioDoDescanso.Decidir().Esta(Estado.Idle).Atual.Expressao, "e acorda com ela");
    }

    // Na configuração do aplicativo, escondido na borda (DEC-025) e preso na parede pelo usuário (DEC-024), a agenda
    // só troca a cara: com a emoção dominante, ela ou as companheiras dela, nos mesmos sorteios; na automática, as
    // caras de sempre de quem espia e de quem está preso.
    [Teste]
    public static void EscondidoEPreso_TrocamPelaDominanteESuasCompanheiras()
    {
        ConfiguracaoDoNucleo doAplicativo = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));
        Expressao[] doEscondido = [Expressao.Curioso, Expressao.Travesso, Expressao.Feliz, Expressao.Surpreso, Expressao.Pensativo, Expressao.Rindo];
        Expressao[] doPreso = [Expressao.Feliz, Expressao.Curioso, Expressao.Travesso, Expressao.Rindo, Expressao.Pensativo];
        foreach (Expressao? dominante in new Expressao?[] { Expressao.Entediado, Expressao.Assustado, null })
        {
            string caso = dominante?.ToString() ?? "automática";
            Expressao[] Esperadas(Expressao[] daAutomatica) => dominante is { } d ? [d, .. Expressoes.Companheiras(d)] : daAutomatica;
            Preferencias preferencias = PreferenciasDeTeste.Completas with { EmocaoDominante = dominante };

            var escondido = new SimuladorDeTempo(doAplicativo, 4, TopologiasDeExemplo.UmMonitor, preferencias);
            PontoPx a = escondido.Estado.Lugar!.Ancora;
            foreach (Evento e in new Evento[] { new Press(new PontoPx(a.X, a.Y - 20)), new Click(), new Press(new PontoPx(a.X, a.Y - 20)), new DoubleClick() })
                escondido.Aplicar(e);
            escondido.Esta(Estado.Peeking, $"{caso}: escondido");
            AfirmarCaras(Esperadas(doEscondido), dominante, CarasSorteadas(escondido, Estado.Peeking), $"{caso}: as caras de quem espia");

            var preso = new SimuladorDeTempo(doAplicativo, 6, TopologiasDeExemplo.UmMonitor, preferencias);
            Posicionamento l = preso.Estado.Lugar!;
            Superficies sup = Superficies.Do(preso.Estado.Topologia!, l.Monitor, l.Tamanho);
            var naParede = new PontoPx(sup.Direita - 20, 600 - 30);
            foreach (Evento e in new Evento[] { new Press(new PontoPx(l.Ancora.X, l.Ancora.Y - 30)), new DragStart(), new DragMove(naParede), new DragEnd(naParede) })
                preso.Aplicar(e);
            preso.Esta(Estado.Climbing, $"{caso}: grudado na parede");
            Afirmar.Verdadeiro(preso.Estado.PresoPeloUsuario, $"{caso}: preso pelo usuário");
            AfirmarCaras(Esperadas(doPreso), dominante, CarasSorteadas(preso, Estado.Climbing), $"{caso}: as caras de quem está preso");
        }
    }

    /// <summary>Só caras do conjunto esperado, pelo menos três delas, e a dominante entre elas.</summary>
    private static void AfirmarCaras(Expressao[] esperadas, Expressao? dominante, HashSet<Expressao> vistas, string caso)
    {
        string descricao = $"vistas [{string.Join(", ", vistas.Order())}], esperadas [{string.Join(", ", esperadas.Order())}]";
        Afirmar.Verdadeiro(vistas.IsSubsetOf(esperadas) && vistas.Count >= 3, $"{caso}: {descricao}");
        if (dominante is { } d) Afirmar.Verdadeiro(vistas.Contains(d), $"{caso}: a dominante aparece; {descricao}");
    }

    // Invariante 27 na configuração do aplicativo, com a física, o agarrar e o esconderijo: com a mesma semente e os
    // mesmos eventos (10 minutos de agenda livre, com cliques, arrastes, clique duplo, bandeja e pausa sorteados), a
    // emoção dominante pela carga só muda as caras. Evento a evento: as mesmas transições, os mesmos efeitos e o mesmo
    // estado, inclusive o gerador pseudoaleatório (cada sorteio de cara continua sendo um sorteio só).
    [Teste]
    public static void Dominante_SoMudaACara()
    {
        ConfiguracaoDoNucleo doAplicativo = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));
        var mestre = new Random(2027);
        long eventos = 0, comOutraCara = 0;
        for (int n = 0; n < 40; n++)
        {
            int semente = mestre.Next();
            Expressao dominante = Expressoes.DeHumor[n % Expressoes.DeHumor.Count];
            string Onde(int i) => $"semente {semente} (sequência {n}), dominante {dominante}, {i}º evento";
            // A mesma instância da topologia nas duas execuções: o estado guarda a topologia por referência.
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            var automatica = new SimuladorDeTempo(doAplicativo, (ulong)semente, topologia, PreferenciasDeTeste.Completas);
            var comDominante = new SimuladorDeTempo(doAplicativo, (ulong)semente, topologia, PreferenciasDeTeste.Completas with { EmocaoDominante = dominante });
            Afirmar.Igual(SemAsCaras(automatica.Estado), SemAsCaras(comDominante.Estado), $"{Onde(0)}: a carga");
            Afirmar.Igual(dominante, comDominante.Estado.Expressao, $"{Onde(0)}: começa com a cara da dominante");
            var semEmocao = new List<(Evento Evento, Resultado Resultado)>();
            var comEmocao = new List<(Evento Evento, Resultado Resultado)>();
            automatica.AoResultado = (_, e, r) => semEmocao.Add((e, r));
            comDominante.AoResultado = (_, e, r) => comEmocao.Add((e, r));

            var rnd = new Random(semente);
            while (automatica.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(5, 40));
                automatica.Avancar(espera);
                comDominante.Avancar(espera);
                foreach (Evento e in Interacao(rnd, automatica.Estado))
                {
                    automatica.Aplicar(e);
                    comDominante.Aplicar(e);
                }
            }

            Afirmar.Igual(semEmocao.Count, comEmocao.Count, $"semente {semente}: o mesmo número de eventos aplicados");
            for (int i = 0; i < semEmocao.Count; i++)
            {
                (Evento e, Resultado sem) = semEmocao[i];
                (Evento eComEmocao, Resultado com) = comEmocao[i];
                Afirmar.Igual(e, eComEmocao, $"{Onde(i)}: o mesmo evento");
                Afirmar.Sequencia(sem.Transicoes, com.Transicoes, $"{Onde(i)}: as mesmas transições ({e})");
                Afirmar.Sequencia(sem.Efeitos, com.Efeitos, $"{Onde(i)}: os mesmos efeitos ({e})");
                Afirmar.Igual(SemAsCaras(sem.Estado), SemAsCaras(com.Estado), $"{Onde(i)}: o mesmo estado, a não ser pela cara ({e})");
                if (sem.Estado.Expressao != com.Estado.Expressao) comOutraCara++;
            }
            eventos += semEmocao.Count;
        }
        Console.WriteLine($"         {eventos} eventos em 40 sementes de 10 minutos; {comOutraCara} com outra cara");
        Afirmar.Verdadeiro(comOutraCara > eventos / 4, "a dominante muda a cara em boa parte do tempo");
    }

    /// <summary>
    /// Uma interação sorteada a partir do estado: clique, arraste até um ponto qualquer do monitor, clique duplo (que
    /// esconde ou tira do esconderijo), esconder e mostrar pela bandeja, pausar ou retomar, ou nada.
    /// </summary>
    private static Evento[] Interacao(Random rnd, EstadoDoNucleo s)
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

    /// <summary>O estado sem a cara e sem a emoção dominante: o que a emoção não pode mudar (invariante 27).</summary>
    private static EstadoDoNucleo SemAsCaras(EstadoDoNucleo s)
        => s with { Expressao = Expressao.Neutro, Preferencias = s.Preferencias with { EmocaoDominante = null } };

    /// <summary>As caras que a agenda sorteia em 30 minutos, sempre no estado dado; afirma que ele não sai de lá.</summary>
    private static HashSet<Expressao> CarasSorteadas(SimuladorDeTempo sim, Estado estado)
    {
        var caras = new HashSet<Expressao>();
        sim.AoAplicar = (_, e, depois) =>
        {
            Afirmar.Igual(estado, depois.Estado, $"{e}: continua em {estado}");
            if (e is AutonomyTimer) caras.Add(depois.Expressao);
        };
        sim.Avancar(TimeSpan.FromMinutes(30));
        sim.AoAplicar = null;
        return caras;
    }

    /// <summary>
    /// Com a física da Fase 4 e a agenda parada, solta o personagem 600 DIP acima do chão, no meio da área útil: ele
    /// cai, quica como borracha, rindo (DEC-023), e pousa.
    /// </summary>
    private static SimuladorDeTempo SoltarDoAlto(Preferencias preferencias)
    {
        var sim = new SimuladorDeTempo(MovimentoTestes.Fase4(AcoesAutonomas.Nenhuma), 3, TopologiasDeExemplo.UmMonitor, preferencias);
        RetanguloPx area = sim.Estado.Lugar!.Monitor.AreaUtil;
        PontoPx a = sim.Estado.Lugar.Ancora;
        var alvo = new PontoPx(area.Esquerda + area.Largura / 2, area.Base - 600 - 30);
        sim.Aplicar(new Press(new PontoPx(a.X, a.Y - 30)));
        sim.Aplicar(new DragStart());
        sim.Aplicar(new DragMove(alvo));
        sim.Aplicar(new DragEnd(alvo));
        return sim.Esta(Estado.Falling, "solto no ar, cai");
    }

    private static string Contagem(Dictionary<Expressao, int> vezes) => string.Join(", ", vezes.OrderBy(v => v.Key).Select(v => $"{v.Key}={v.Value}"));
}
