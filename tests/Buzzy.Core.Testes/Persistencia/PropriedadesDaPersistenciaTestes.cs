using System.Globalization;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Persistencia;

/// <summary>
/// Propriedades da restauração na partida e do settings.json (Fase 5; ARCHITECTURE.md 2.8 e 2.12), no estilo
/// de <see cref="PropriedadesTestes"/>: 5.000 casos com topologias aleatórias (<see cref="GeradorDeTopologias"/>),
/// cada um com a própria semente, tirada de uma semente mestra fixa; e sequências longas na configuração do
/// aplicativo, com a física da Fase 4. Toda falha informa as sementes e as entradas, para o caso virar um
/// teste de exemplo.
/// </summary>
internal static class PropriedadesDaPersistenciaTestes
{
    private const int Semente = 20260930;
    private const int Casos = 5000;

    // A partida restaura pela cascata (ARCHITECTURE.md 2.8): o monitor da chave; sem ele, o primeiro com a
    // tela salva; sem nenhum dos dois, o principal. Termina na área útil desse monitor, na posição relativa
    // salva, saneada, e a posição passa a ser dele, com a tela dele. É determinística; restaurar ou
    // reacomodar de novo, na mesma topologia, não move o personagem.
    [Teste]
    public static void RestaurarSegueACascataETerminaNaAreaUtil()
    {
        var porOrigem = new Dictionary<OrigemDaRestauracao, int>();
        ParaCadaCaso((rnd, gerador, contexto) =>
        {
            Topologia t = gerador.NovaTopologia();
            TamanhoDip tamanho = gerador.Sprite();
            PosicaoDoPersonagem salva = PosicaoSalvaAleatoria(rnd, gerador, t);
            string Onde() => $"{contexto()}; topologia: {t}; salva: {Descrever(salva)}; sprite {tamanho}";

            MonitorDoDesktop? daChave = t.PorChave(salva.ChaveMonitor);
            MonitorDoDesktop? daTela = daChave is null && salva.TelaDoMonitor is { } tela ? t.Monitores.FirstOrDefault(m => m.Tela == tela) : null;
            (MonitorDoDesktop esperado, OrigemDaRestauracao origemEsperada) =
                daChave is not null ? (daChave, OrigemDaRestauracao.PelaChave)
                : daTela is not null ? (daTela, OrigemDaRestauracao.PeloRetangulo)
                : (t.Principal, OrigemDaRestauracao.NoPrincipal);

            (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(t, salva, tamanho);
            porOrigem[origem] = porOrigem.GetValueOrDefault(origem) + 1;
            Verificar(origem == origemEsperada && ReferenceEquals(r.Monitor, esperado),
                () => $"{Onde()}: restaurou {origem} em {r.Monitor.Chave}; esperado {origemEsperada} em {esperado.Chave}");
            PropriedadesTestes.VerificarPosicionamento(r, tamanho, Onde);
            Verificar(ReferenceEquals(Maquina.MonitorDaAncora(t, r.Ancora), r.Monitor), () => $"{Onde()}: a validação atribui a âncora {r.Ancora} a outro monitor");

            double fx = Saneada(salva.FracaoX), fy = Saneada(salva.FracaoY);
            Verificar(r == Posicionador.NoMonitor(esperado, fx, fy, tamanho), () => $"{Onde()}: não está nas frações salvas, saneadas ({fx}; {fy})");
            Verificar(nova.ChaveMonitor == esperado.Chave && nova.TelaDoMonitor == esperado.Tela && nova.AncoraAbsoluta == r.Ancora
                    && nova.FracaoX.Equals(fx) && nova.FracaoY.Equals(fy),
                () => $"{Onde()}: posição nova {Descrever(nova)}");

            // Determinística: cópias iguais das entradas dão o mesmo resultado.
            var copia = new Topologia(t.Monitores.Select(m => m with { }));
            Verificar((r, nova, origem) == Posicionador.Restaurar(copia, salva with { }, tamanho), () => $"{Onde()}: resultado mudou com cópias das entradas");

            // Restaurar de novo (agora pela chave) e a reacomodação da execução não movem o personagem.
            (Posicionamento deNovo, PosicaoDoPersonagem novaDeNovo, OrigemDaRestauracao origemDeNovo) = Posicionador.Restaurar(t, nova, tamanho);
            Verificar(origemDeNovo == OrigemDaRestauracao.PelaChave && deNovo == r && novaDeNovo == nova, () => $"{Onde()}: restaurar de novo moveu para {deNovo.Ancora} ({origemDeNovo})");
            Verificar((r, nova) == Posicionador.Reacomodar(t, nova, tamanho), () => $"{Onde()}: reacomodar na mesma topologia moveu o personagem");
        });

        Console.WriteLine("         " + string.Join(", ", porOrigem.OrderBy(o => o.Key).Select(o => $"{o.Key}={o.Value}")));
        foreach (OrigemDaRestauracao origem in Enum.GetValues<OrigemDaRestauracao>())
            Afirmar.Verdadeiro(porOrigem.GetValueOrDefault(origem) > 500, $"o gerador exercita a restauração {origem} ({porOrigem.GetValueOrDefault(origem)} casos)");
    }

    // A posição que a execução grava (Descrever) passa pelo arquivo, volta igual e, restaurada na mesma
    // topologia, dá o mesmo resultado que a reacomodação da execução (Reacomodar). Numa topologia mudada em
    // que o monitor continua, também; sem ele, a partida segue a cascata (pela tela ou no principal), e
    // nunca a âncora absoluta, como a reacomodação faria. As preferências voltam iguais, com a emoção dominante
    // (DEC-027), e a postura também (a borda do esconderijo e a marca de preso, esquema v3), cada uma de um gerador
    // próprio, para não mudar os outros sorteios.
    [Teste]
    public static void ArquivoIdaEVolta_EquivaleAReacomodarNaMesmaTopologia()
    {
        int mesmaChave = 0, pelaTela = 0, noPrincipal = 0, comEmocao = 0, comPostura = 0;
        var emocoes = new Random(unchecked(Semente * 37 + 11));
        var posturas = new Random(unchecked(Semente * 41 + 13));
        ParaCadaCaso((rnd, gerador, contexto) =>
        {
            Topologia t = gerador.NovaTopologia();
            TamanhoDip tamanho = gerador.Sprite();
            MonitorDoDesktop m = t.Monitores[rnd.Next(t.Monitores.Count)];
            PosicaoDoPersonagem gravada = Posicionador.Descrever(Posicionador.NoMonitor(m, gerador.Fracao(), gerador.Fracao(), tamanho));
            var preferencias = new Preferencias((NivelDeEnergia)rnd.Next(3), rnd.Next(2) == 0, rnd.Next(2) == 0) { EmocaoDominante = EmocaoGravavel(emocoes) };
            if (preferencias.EmocaoDominante is not null) comEmocao++;
            (LadoDoEsconderijo borda, bool preso) = PosturaGravavel(posturas);
            if (borda != LadoDoEsconderijo.Nenhum || preso) comPostura++;
            var configuracoes = new ConfiguracoesSalvas(gravada, preferencias) { Esconderijo = borda, PresoPeloUsuario = preso };
            string Onde() => $"{contexto()}; topologia: {t}; gravada: {Descrever(gravada)}; sprite {tamanho}";

            LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(EsquemaDeConfiguracoes.Escrever(configuracoes));
            Verificar(lida.Situacao == SituacaoDaLeitura.Valida && lida.Avisos.Count == 0 && lida.Configuracoes == configuracoes,
                () => $"{Onde()}: o arquivo devolveu {lida.Situacao}, {Descrever(lida.Configuracoes.Posicao)}, avisos: {string.Join(" | ", lida.Avisos)}");
            PosicaoDoPersonagem salva = lida.Configuracoes.Posicao!;

            (Posicionamento restaurado, PosicaoDoPersonagem restaurada, OrigemDaRestauracao origem) = Posicionador.Restaurar(t, salva, tamanho);
            (Posicionamento reacomodado, PosicaoDoPersonagem reacomodada) = Posicionador.Reacomodar(t, gravada, tamanho);
            Verificar(origem == OrigemDaRestauracao.PelaChave && restaurado == reacomodado && restaurada == reacomodada,
                () => $"{Onde()}: restaurada em {restaurado.Monitor.Chave} {restaurado.Ancora} ({origem}), reacomodada em {reacomodado.Monitor.Chave} {reacomodado.Ancora}");

            // Na partida seguinte, noutra topologia.
            Topologia outra = gerador.Mudar(t);
            (Posicionamento naOutra, _, OrigemDaRestauracao origemNaOutra) = Posicionador.Restaurar(outra, salva, tamanho);
            if (outra.PorChave(gravada.ChaveMonitor) is not null)
            {
                mesmaChave++;
                Posicionamento esperado = Posicionador.Reacomodar(outra, gravada, tamanho).Resultado;
                Verificar(origemNaOutra == OrigemDaRestauracao.PelaChave && naOutra == esperado,
                    () => $"{Onde()}; outra: {outra}: com o monitor presente, restaurou em {naOutra.Monitor.Chave} {naOutra.Ancora} ({origemNaOutra}), reacomodaria em {esperado.Monitor.Chave} {esperado.Ancora}");
                return;
            }
            MonitorDoDesktop? daTela = outra.Monitores.FirstOrDefault(x => x.Tela == gravada.TelaDoMonitor);
            if (daTela is not null) pelaTela++;
            else noPrincipal++;
            MonitorDoDesktop monitorEsperado = daTela ?? outra.Principal;
            OrigemDaRestauracao origemEsperada = daTela is not null ? OrigemDaRestauracao.PeloRetangulo : OrigemDaRestauracao.NoPrincipal;
            Verificar(origemNaOutra == origemEsperada && ReferenceEquals(naOutra.Monitor, monitorEsperado),
                () => $"{Onde()}; outra: {outra}: sem o monitor, restaurou {origemNaOutra} em {naOutra.Monitor.Chave}; esperado {origemEsperada} em {monitorEsperado.Chave}");
        });

        Console.WriteLine($"         na topologia mudada: {mesmaChave} pela chave, {pelaTela} pela tela, {noPrincipal} no principal; {comEmocao} com emoção dominante; {comPostura} com esconderijo ou preso");
        Afirmar.Verdadeiro(mesmaChave > 1000 && pelaTela > 0 && noPrincipal > 100, "o gerador exercita os três passos na topologia mudada");
        Afirmar.Verdadeiro(comEmocao > 1000 && comEmocao < Casos - 500, "o gerador exercita a emoção escolhida e a automática");
        Afirmar.Verdadeiro(comPostura > 1000 && comPostura < Casos - 500, "o gerador exercita a postura gravada e a sem postura");
    }

    // Na configuração do aplicativo (ConfiguracaoDoNucleo.DoAplicativo: física, queda, agarrar e esconderijo no clique
    // duplo), o que as sequências aleatórias de InvariantesTestes conferem sem a física. Cada sequência corre o
    // tempo (a agenda anda, escala, pula, pendura e descansa) entre interações sorteadas: arrastar e soltar em
    // qualquer ponto (no chão, no ar, junto das laterais e da borda de cima, fora dos monitores), clicar, clique
    // duplo, bandeja, bloqueio, suspensão, redefinição, topologia, tela cheia e preferências; no fim, a saída.
    // Todo GravarPosicao segue o invariante 18 (InvarianteDezoito), no máximo um por evento. A carga restaura pela
    // cascata; a partida seguinte, com a última posição gravada depois de passar pelo settings.json, também. A
    // âncora só é conferida na coluna das frações salvas quando o personagem não agarrou uma lateral ou o cipó
    // (DEC-024) nem voltou escondido atrás de uma lateral (DEC-025): agarrado, ele vai para a parede ou para a borda de
    // cima, e escondido na lateral, para a beira dela. A emoção dominante (DEC-027) entra na carga,
    // em SETTINGS_CHANGED e em comandos do menu entre as interações, de um gerador próprio (sem mudar os outros
    // sorteios nem as contagens); cada GravarPosicao volta do arquivo com ela (invariante 18), e a partida seguinte
    // começa com a mesma emoção e com a cara dela. A postura gravada com a posição (esquema v3, DEC-029, item 11) também:
    // a carga sorteia uma borda do esconderijo e a marca de preso (de outro gerador próprio), todo GravarPosicao leva a
    // postura do estado depois do evento, e a partida seguinte, com a postura da saída, volta escondida na mesma borda ou
    // agarrada e presa (ConferirPosturaNaCarga).
    [Teste]
    public static void NaConfiguracaoDoAplicativo_GravacoesECargasSeguemAsRegras()
    {
        const int Sequencias = 150, Interacoes = 40;
        var sprite = new TamanhoDip(128, 128);
        ConfiguracaoDoNucleo doAplicativo = ConfiguracaoDoNucleo.DoAplicativo(sprite);
        var mestre = new Random(Semente);
        var gravacoesPorEvento = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var cargasPorPasso = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var posturasNaCarga = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int gravacoesComAFisica = 0, gravacoesEscondidoNaBorda = 0, cargasAgarradas = 0, partidasSeguintes = 0;
        int gravacoesComEmocao = 0, partidasSeguintesComEmocao = 0, gravacoesPresas = 0, gravacoesEscondidas = 0;
        void ContarCarga((string Passo, bool Agarrou) carga)
        {
            cargasPorPasso[carga.Passo] = cargasPorPasso.GetValueOrDefault(carga.Passo) + 1;
            if (carga.Agarrou) cargasAgarradas++;
        }
        void ContarPostura(string postura) => posturasNaCarga[postura] = posturasNaCarga.GetValueOrDefault(postura) + 1;

        for (int n = 0; n < Sequencias; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia t = gerador.NovaTopologia();
            PosicaoDoPersonagem? salva = rnd.Next(4) == 0 ? null : PosicaoSalvaAleatoria(rnd, gerador, t);
            var emocoes = new Random(unchecked(semente * 37 + 11));
            var preferencias = new Preferencias((NivelDeEnergia)rnd.Next(3), rnd.Next(2) == 0, rnd.Next(2) == 0) { EmocaoDominante = EmocaoGravavel(emocoes) };
            var posturas = new Random(unchecked(semente * 41 + 13));
            (LadoDoEsconderijo bordaSalva, bool presoSalvo) = PosturaGravavel(posturas, comForaDoEnum: true);
            int numero = n;
            string Contexto() => $"semente {Semente}, sequência {numero} (semente da sequência {semente})";

            Loaded primeira = new(t, salva, preferencias) { Esconderijo = bordaSalva, PresoPeloUsuario = presoSalvo };
            var sim = new SimuladorDeTempo(doAplicativo, (ulong)semente, primeira);
            string OndeNaCarga() => $"{Contexto()}: carga; topologia {t}; salva {Descrever(salva)}, {bordaSalva}/preso {presoSalvo}";
            ContarCarga(ConferirCargaNoAplicativo(sim, t, salva, sprite, OndeNaCarga, primeira.Esconderijo));
            ContarPostura(ConferirPosturaNaCarga(sim, primeira, OndeNaCarga));

            GravarPosicao? ultimaGravada = null;
            sim.AoResultado = (antes, evento, r) =>
            {
                GravarPosicao[] gravadas = [.. r.Efeitos.OfType<GravarPosicao>()];
                Verificar(gravadas.Length <= 1, () => $"invariante 18: {Contexto()}, {evento} em {antes.Estado}: {gravadas.Length} GravarPosicao num só evento");
                foreach (GravarPosicao g in gravadas)
                {
                    string? violacao = InvarianteDezoito.Violacao(evento, g, r.Estado);
                    Verificar(violacao is null, () => $"invariante 18: {Contexto()}, {evento} em {antes.Estado} ({r.Estado.Estado} depois): {violacao}");
                    string tipo = evento.GetType().Name;
                    gravacoesPorEvento[tipo] = gravacoesPorEvento.GetValueOrDefault(tipo) + 1;
                    if (antes.Estado.EmMovimento() || r.Estado.Estado is Estado.Climbing or Estado.Hanging or Estado.Falling) gravacoesComAFisica++;
                    if (antes.Esconderijo != LadoDoEsconderijo.Nenhum) gravacoesEscondidoNaBorda++;
                    if (r.Estado.Preferencias.EmocaoDominante is not null) gravacoesComEmocao++;
                    if (g.PresoPeloUsuario) gravacoesPresas++;
                    if (g.Esconderijo != LadoDoEsconderijo.Nenhum) gravacoesEscondidas++;
                    ultimaGravada = g;
                }
            };
            for (int k = 0; k < Interacoes; k++)
            {
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 30)));
                // O comando de emoção pelo menu, nunca no meio de um gesto curto, que ele encerraria (invariante 15):
                // assim a sequência continua a mesma, só com outras caras (invariante 27).
                if (emocoes.Next(3) == 0 && sim.Estado.Gesto == Gesto.Nenhum)
                    sim.Aplicar(new CmdSetDominantEmotion(EsquemaDeConfiguracoesTestes.EmocaoAleatoria(emocoes)));
                Interagir(sim, rnd, emocoes, gerador, ref t);
            }
            sim.Aplicar(rnd.Next(2) == 0 ? new CmdExit() : new SessionEnding());
            Verificar(sim.Estado.Estado == Estado.Exiting, () => $"{Contexto()}: não saiu ({sim.Estado.Estado})");
            if (ultimaGravada is not { } gravada) continue;

            // A partida seguinte, na topologia da saída, lê o que o arquivo guardou da última posição gravada, com a
            // postura dela e as preferências da saída, emoção dominante inclusive.
            partidasSeguintes++;
            var salvas = new ConfiguracoesSalvas(gravada.Posicao, sim.Estado.Preferencias) { Esconderijo = gravada.Esconderijo, PresoPeloUsuario = gravada.PresoPeloUsuario };
            LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(EsquemaDeConfiguracoes.Escrever(salvas));
            Verificar(lida.Configuracoes == salvas,
                () => $"{Contexto()}: o arquivo devolveu {lida.Configuracoes.Esconderijo}/preso {lida.Configuracoes.PresoPeloUsuario}, {lida.Configuracoes.Preferencias}; a saída gravou {salvas.Esconderijo}/preso {salvas.PresoPeloUsuario}, {salvas.Preferencias}");
            if (lida.Configuracoes.Preferencias.EmocaoDominante is not null) partidasSeguintesComEmocao++;
            Loaded carga = lida.Configuracoes.ParaACarga(t);
            var seguinte = new SimuladorDeTempo(doAplicativo, (ulong)semente + 1, carga);
            string OndeNaSeguinte() => $"{Contexto()}: partida seguinte; topologia {t}; gravada {Descrever(gravada.Posicao)}, {gravada.Esconderijo}/preso {gravada.PresoPeloUsuario}";
            ContarCarga(ConferirCargaNoAplicativo(seguinte, t, lida.Configuracoes.Posicao, sprite, OndeNaSeguinte, carga.Esconderijo));
            ContarPostura("seguinte, " + ConferirPosturaNaCarga(seguinte, carga, OndeNaSeguinte));
        }

        Console.WriteLine($"         {Sequencias} sequências com a física do aplicativo e {partidasSeguintes} partidas seguintes; gravações: "
            + string.Join(", ", gravacoesPorEvento.Select(g => $"{g.Key}={g.Value}"))
            + $" ({gravacoesComAFisica} com a física, {gravacoesEscondidoNaBorda} escondido na borda); cargas: "
            + string.Join(", ", cargasPorPasso.Select(c => $"{c.Key}={c.Value}")) + $" ({cargasAgarradas} agarradas)");
        Console.WriteLine($"         emoção dominante: {gravacoesComEmocao} gravações de posição com ela, {partidasSeguintesComEmocao} partidas seguintes com ela");
        Console.WriteLine($"         postura: {gravacoesEscondidas} gravações escondido, {gravacoesPresas} preso; nas cargas: "
            + string.Join(", ", posturasNaCarga.Select(p => $"{p.Key}={p.Value}")));
        Afirmar.Verdadeiro(gravacoesComEmocao > 100 && partidasSeguintesComEmocao > 10 && partidasSeguintesComEmocao < partidasSeguintes,
            "o gerador grava com a emoção escolhida e começa a partida seguinte com ela e sem ela");
        foreach (Type evento in InvarianteDezoito.EventosQueGravam)
            Afirmar.Verdadeiro(gravacoesPorEvento.GetValueOrDefault(evento.Name) > 0, $"o gerador exercita a gravação de {evento.Name}");
        Afirmar.Verdadeiro(gravacoesComAFisica > 0 && gravacoesEscondidoNaBorda > 0, "o gerador grava com a física e escondido na borda");
        foreach (string passo in new[] { "pela chave", "pelo retângulo do monitor", "no monitor principal", "sem posição salva" })
            Afirmar.Verdadeiro(cargasPorPasso.GetValueOrDefault(passo) > 0, $"o gerador exercita a carga {passo}");
        Afirmar.Verdadeiro(cargasAgarradas > 0 && partidasSeguintes > Sequencias / 2, "o gerador exercita a carga agarrada e a partida seguinte");
        Afirmar.Verdadeiro(gravacoesEscondidas > 0 && gravacoesPresas > 0, "o gerador grava escondido e preso");
        foreach (string postura in new[] { "escondido", "preso", "seguinte, escondido", "seguinte, preso" })
            Afirmar.Verdadeiro(posturasNaCarga.GetValueOrDefault(postura) > 0, $"o gerador exercita a carga {postura}");
    }

    /// <summary>
    /// A postura depois da carga (esquema v3, DEC-029, item 11), pelas regras escritas aqui, à parte do núcleo. Sem posição
    /// salva, nem borda nem marca. Com ela, a borda saneada (dentro do enum; nenhuma, senão) é a do estado; escondido
    /// numa borda, ele está espiando (PEEKING) e a marca fica guardada como veio, para quando sair do esconderijo; agarrado à
    /// parede ou ao cipó, a marca é a da carga; em qualquer outro estado (no chão ou caindo), a acomodação a apagou. Devolve
    /// a postura conferida: "escondido", "preso", "agarrado solto", "marca apagada" ou "sem postura".
    /// </summary>
    private static string ConferirPosturaNaCarga(SimuladorDeTempo sim, Loaded carga, Func<string> onde)
    {
        EstadoDoNucleo s = sim.Estado;
        if (carga.PosicaoSalva is null)
        {
            Verificar(s.Esconderijo == LadoDoEsconderijo.Nenhum && !s.PresoPeloUsuario, () => $"{onde()}: sem posição salva, ficou {s.Esconderijo}/preso {s.PresoPeloUsuario}");
            return "sem postura";
        }
        LadoDoEsconderijo borda = Enum.IsDefined(carga.Esconderijo) ? carga.Esconderijo : LadoDoEsconderijo.Nenhum;
        Verificar(s.Esconderijo == borda, () => $"{onde()}: borda {s.Esconderijo}; a carga trouxe {carga.Esconderijo}");
        if (borda != LadoDoEsconderijo.Nenhum)
        {
            Verificar(s.Estado == Estado.Peeking && s.PresoPeloUsuario == carga.PresoPeloUsuario,
                () => $"{onde()}: com a borda {borda}, devia espiar com a marca guardada ({carga.PresoPeloUsuario}); está em {s.Estado}, preso {s.PresoPeloUsuario}");
            return "escondido";
        }
        if (s.Estado is Estado.Climbing or Estado.Hanging)
        {
            Verificar(s.PresoPeloUsuario == carga.PresoPeloUsuario && s.Movimento.Agarrado,
                () => $"{onde()}: agarrado em {s.Estado}, preso {s.PresoPeloUsuario}; a carga trouxe preso {carga.PresoPeloUsuario}");
            return carga.PresoPeloUsuario ? "preso" : "agarrado solto";
        }
        Verificar(!s.PresoPeloUsuario, () => $"{onde()}: em {s.Estado}, longe da parede e do cipó, continuou preso");
        return carga.PresoPeloUsuario ? "marca apagada" : "sem postura";
    }

    /// <summary>
    /// A carga na configuração do aplicativo. Com a posição salva: o monitor da cascata (chave, tela da época,
    /// principal), a regra dizendo o passo, a posição do núcleo com a chave e a tela desse monitor e frações em
    /// [0, 1], e, se ele não agarrou uma lateral ou o cipó nem foi para trás de uma lateral (o esconderijo da
    /// <paramref name="borda"/> salva), a âncora na coluna das frações salvas. Sem ela, a posição inicial e a regra de
    /// sempre. Devolve o passo e se ele agarrou.
    /// </summary>
    private static (string Passo, bool Agarrou) ConferirCargaNoAplicativo(SimuladorDeTempo sim, Topologia t, PosicaoDoPersonagem? salva, TamanhoDip sprite, Func<string> onde,
        LadoDoEsconderijo borda = LadoDoEsconderijo.Nenhum)
    {
        EstadoDoNucleo s = sim.Estado;
        Posicionamento lugar = Afirmar.NaoNulo(s.Lugar, $"{onde()}: lugar depois da carga");
        string regra = Afirmar.NaoNulo(sim.Transicoes.FirstOrDefault(), $"{onde()}: transição da carga").Regra;
        bool agarrou = s.Estado is Estado.Climbing or Estado.Hanging;
        bool atrasDeUmaLateral = borda is LadoDoEsconderijo.Esquerda or LadoDoEsconderijo.Direita;
        if (s.Preferencias.EmocaoDominante is { } dominante)
            Verificar(s.Expressao == dominante, () => $"{onde()}: com a emoção dominante {dominante}, começou com a cara {s.Expressao}");
        if (salva is null)
        {
            Posicionamento inicial = Posicionador.Inicial(t, sprite);
            Verificar(regra == "BOOTING: configurações e topologia carregadas" && lugar.Monitor.Chave == inicial.Monitor.Chave && lugar.Ancora.X == inicial.Ancora.X,
                () => $"{onde()}: sem posição salva, \"{regra}\" em {lugar.Monitor.Chave} {lugar.Ancora}; esperada a posição inicial {inicial.Monitor.Chave} {inicial.Ancora}");
            return ("sem posição salva", agarrou);
        }

        MonitorDoDesktop? daChave = t.PorChave(salva.ChaveMonitor);
        MonitorDoDesktop? daTela = daChave is null && salva.TelaDoMonitor is { } tela ? t.Monitores.FirstOrDefault(m => m.Tela == tela) : null;
        (MonitorDoDesktop esperado, string passo) =
            daChave is not null ? (daChave, "pela chave")
            : daTela is not null ? (daTela, "pelo retângulo do monitor")
            : (t.Principal, "no monitor principal");
        Verificar(regra == $"BOOTING: configurações e topologia carregadas; posição salva restaurada {passo}",
            () => $"{onde()}: regra \"{regra}\"; esperado o passo {passo}");
        Verificar(lugar.Monitor.Chave == esperado.Chave, () => $"{onde()}: restaurada em {lugar.Monitor.Chave}; esperado {esperado.Chave} ({passo})");
        Verificar(s.Posicao is { } p && p.ChaveMonitor == esperado.Chave && p.TelaDoMonitor == esperado.Tela && p.FracaoX is >= 0 and <= 1 && p.FracaoY is >= 0 and <= 1,
            () => $"{onde()}: posição do núcleo {Descrever(s.Posicao)}; esperada em {esperado.Chave}, tela {esperado.Tela}");
        if (!agarrou && !atrasDeUmaLateral)
        {
            int x = Posicionador.NoMonitor(esperado, salva.FracaoX, salva.FracaoY, sprite).Ancora.X;
            Verificar(lugar.Ancora.X == x, () => $"{onde()}: âncora {lugar.Ancora} em {s.Estado}; as frações salvas dão x = {x}");
        }
        return (passo, agarrou);
    }

    /// <summary>
    /// Uma interação sorteada, como a raiz as entrega: arrastar e soltar (ou cancelar) em qualquer ponto, clicar, clique
    /// duplo (PRESS, CLICK, PRESS e DOUBLE_CLICK), esconder e mostrar pela bandeja, bloquear e desbloquear, suspender e
    /// retomar, redefinir a posição, mudar a topologia, a tela cheia ou as preferências, pausar ou retomar a agenda,
    /// ou uma carga repetida, que é ignorada. Escondido, metade das vezes o usuário o mostra antes.
    /// </summary>
    private static void Interagir(SimuladorDeTempo sim, Random rnd, Random emocoes, GeradorDeTopologias gerador, ref Topologia t)
    {
        if (sim.Estado.Estado == Estado.Hidden && rnd.Next(2) == 0) sim.Aplicar(new CmdShow());
        PontoPx? corpo = sim.Estado.Lugar is { } lugar ? new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 20) : null;
        switch (rnd.Next(13))
        {
            case 0 or 1 or 2 when corpo is { } p:
                PontoPx alvo = gerador.Ponto(t);
                sim.Aplicar(new Press(p));
                sim.Aplicar(new DragStart());
                sim.Aplicar(new DragMove(alvo));
                sim.Aplicar(rnd.Next(5) == 0 ? new DragCancel() : new DragEnd(alvo));
                break;
            case 3 when corpo is { } p:
                sim.Aplicar(new Press(p));
                sim.Aplicar(new Click());
                break;
            case 4 when corpo is { } p:
                sim.Aplicar(new Press(p));
                sim.Aplicar(new Click());
                sim.Aplicar(new Press(p));
                sim.Aplicar(new DoubleClick());
                break;
            case 5:
                sim.Aplicar(new CmdHide());
                if (rnd.Next(2) == 0) sim.Aplicar(new CmdShow());
                break;
            case 6:
                sim.Aplicar(new SessionLocked());
                if (rnd.Next(4) != 0) sim.Aplicar(new SessionUnlocked());
                break;
            case 7:
                sim.Aplicar(new Suspending());
                if (rnd.Next(4) != 0) sim.Aplicar(new Resumed());
                break;
            case 8:
                sim.Aplicar(new CmdResetPosition());
                break;
            case 9:
                t = rnd.Next(3) == 0 ? gerador.NovaTopologia() : gerador.Mudar(t);
                sim.Aplicar(new TopologyChanged(t));
                break;
            case 10:
                List<string> ocupados = [.. t.Monitores.Select(m => m.Chave).Where(_ => rnd.Next(3) == 0)];
                sim.Aplicar(new FullscreenTargetsChanged(new MonitoresOcupados(ocupados)));
                break;
            case 11:
                sim.Aplicar(new SettingsChanged(new Preferencias((NivelDeEnergia)rnd.Next(3), rnd.Next(3) != 0, rnd.Next(2) == 0)
                {
                    EmocaoDominante = EsquemaDeConfiguracoesTestes.EmocaoAleatoria(emocoes),
                }));
                break;
            default:
                sim.Aplicar(rnd.Next(3) switch
                {
                    0 => new CmdPauseAutonomy(),
                    1 => new CmdResumeAutonomy(),
                    _ => new Loaded(t, null, Preferencias.Padrao),
                });
                break;
        }
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>
    /// Posição salva por outra sessão: chave de um monitor da topologia, uma do gerador (que pode ou não
    /// existir) ou uma que nenhuma topologia gerada tem; tela desconhecida, a de um monitor da topologia (às
    /// vezes com outra chave) ou um retângulo qualquer; frações quaisquer, inclusive NaN e infinitas; âncora
    /// em qualquer lugar (a partida não a usa).
    /// </summary>
    private static PosicaoDoPersonagem PosicaoSalvaAleatoria(Random rnd, GeradorDeTopologias gerador, Topologia t)
    {
        string chave = rnd.Next(3) switch
        {
            0 => t.Monitores[rnd.Next(t.Monitores.Count)].Chave,
            1 => GeradorDeTopologias.Chave(rnd.Next(1, 12)),
            _ => "mon:" + rnd.Next().ToString("x8", CultureInfo.InvariantCulture),
        };
        RetanguloPx? tela = rnd.Next(3) switch
        {
            0 => null,
            1 => t.Monitores[rnd.Next(t.Monitores.Count)].Tela,
            _ => RetanguloPx.DePosicaoETamanho(gerador.Ponto(t), gerador.Resolucao()),
        };
        return new PosicaoDoPersonagem(chave, gerador.Fracao(), gerador.Fracao(), gerador.Ponto(t)) { TelaDoMonitor = tela };
    }

    /// <summary>Emoção dominante gravável: a automática (nula) uma vez em quatro, senão uma das 14 caras de humor.</summary>
    private static Expressao? EmocaoGravavel(Random emocoes) => emocoes.Next(4) == 0 ? null : Expressoes.DeHumor[emocoes.Next(Expressoes.DeHumor.Count)];

    /// <summary>
    /// Postura gravável (esquema v3): metade das vezes nenhuma borda, senão uma das três; a marca de preso uma vez em três.
    /// Com <paramref name="comForaDoEnum"/>, uma borda em dez fica fora do enum, como num arquivo adulterado.
    /// </summary>
    private static (LadoDoEsconderijo Borda, bool Preso) PosturaGravavel(Random posturas, bool comForaDoEnum = false)
    {
        LadoDoEsconderijo borda = comForaDoEnum && posturas.Next(10) == 0 ? (LadoDoEsconderijo)posturas.Next(4, 20)
            : posturas.Next(2) == 0 ? LadoDoEsconderijo.Nenhum
            : (LadoDoEsconderijo)posturas.Next(1, 4);
        return (borda, posturas.Next(3) == 0);
    }

    /// <summary>A regra das frações salvas, escrita aqui à parte: NaN vira 0,5; o resto é preso em [0, 1].</summary>
    private static double Saneada(double f) => double.IsNaN(f) ? 0.5 : Math.Min(Math.Max(f, 0), 1);

    private static string Descrever(PosicaoDoPersonagem? p)
        => p is null ? "nenhuma" : $"{Gravacao.DescreverPosicaoCompleta(p)}";

    /// <summary>Roda <see cref="Casos"/> casos, cada um com a própria semente tirada da semente mestra.</summary>
    private static void ParaCadaCaso(Action<Random, GeradorDeTopologias, Func<string>> verificar)
    {
        var mestre = new Random(Semente);
        for (int caso = 0; caso < Casos; caso++)
        {
            int sementeDoCaso = mestre.Next();
            var rnd = new Random(sementeDoCaso);
            int numero = caso;
            verificar(rnd, new GeradorDeTopologias(rnd), () => $"semente {Semente}, caso {numero} (semente do caso {sementeDoCaso})");
        }
    }

    private static void Verificar(bool condicao, Func<string> mensagem)
    {
        if (!condicao) Afirmar.Falhar(mensagem());
    }
}
