using Buzzy.Core.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Fase 5, passo P13 — a geometria das passagens entre monitores (desenho da travessia, D1 e D2; DEC-032): uma porta nasce
/// da adjacência exata das ÁREAS ÚTEIS, e não das telas, numa faixa vertical em que as duas se sobrepõem; o resto da lateral
/// é parede. A travessia plana exige o mesmo chão dos dois lados, a porta indo até o chão e com altura para o sprite maior
/// dos dois, e o sprite cabendo no vizinho. Os valores esperados são calculados à mão a partir das topologias de exemplo.
/// </summary>
internal static class PassagensTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    private static MonitorDoDesktop M(Topologia t, string chave) => Afirmar.NaoNulo(t.PorChave(chave), chave);

    private static IReadOnlyList<Porta> Portas(Topologia t, string chave, int lado) => Passagens.Portas(t, M(t, chave), lado);

    // D1: as portas de cada lateral, por adjacência exata das áreas úteis, na faixa em que elas se sobrepõem na vertical.
    [Teste]
    public static void Portas_PelaAdjacenciaDasAreasUteis_NasTopologiasDeExemplo()
    {
        (string Nome, Topologia T, string Chave, int Lado, Porta[] Esperadas)[] casos =
        [
            ("UmMonitor", UmMonitor, Display1, -1, []),
            ("UmMonitor", UmMonitor, Display1, +1, []),
            ("LadoALado", LadoALado, Display1, +1, [new Porta(Display2, +1, 1920, 0, 1032)]),
            ("LadoALado", LadoALado, Display2, -1, [new Porta(Display1, -1, 1920, 0, 1032)]),
            ("LadoALado", LadoALado, Display1, -1, []),
            ("SecundarioAEsquerda", SecundarioAEsquerda, Display1, -1, [new Porta(Display2, -1, 0, 0, 1032)]),
            ("SecundarioAEsquerda", SecundarioAEsquerda, Display2, +1, [new Porta(Display1, +1, 0, 0, 1032)]),
            ("PrincipalADireita", PrincipalADireita, Display2, +1, [new Porta(Display1, +1, 0, 0, 1032)]),
            ("Retrato", TopologiasDeExemplo.Retrato, Display1, +1, [new Porta(Display2, +1, 1920, 0, 1032)]),
            ("Retrato", TopologiasDeExemplo.Retrato, Display2, -1, [new Porta(Display1, -1, 1920, 0, 1032)]),
            ("EscalasMistas", EscalasMistas, Display1, +1, [new Porta(Display2, +1, 2560, 360, 1368)]),
            ("EscalasMistas", EscalasMistas, Display1, -1, [new Porta(Display3, -1, 0, 0, 1344)]),
            ("EscalasMistas", EscalasMistas, Display3, +1, [new Porta(Display1, +1, 0, 0, 1344)]),
            ("DegrauDesalinhado", DegrauDesalinhado, Display1, +1, [new Porta(Display2, +1, 1920, 400, 1032)]),
            ("DegrauDesalinhado", DegrauDesalinhado, Display2, +1, [new Porta(Display3, +1, 3840, 800, 1432)]),
            ("TresMonitores", TresMonitores, Display1, -1, [new Porta(Display2, -1, 0, 180, 1212)]),
            ("TresMonitores", TresMonitores, Display1, +1, [new Porta(Display3, +1, 2560, 0, 1020)]),
            ("EmL", EmL, Display1, +1, [new Porta(Display2, +1, 1920, 0, 1032)]),
            ("EmL", EmL, Display3, +1, []),
            ("VaoEntreMonitores", VaoEntreMonitores, Display1, +1, []),
            ("QuinaComQuina", QuinaComQuina, Display1, +1, []),
            ("EmpilhadoSecundarioAcima", EmpilhadoSecundarioAcima, Display1, -1, []),
            ("EmpilhadoSecundarioAcima", EmpilhadoSecundarioAcima, Display1, +1, []),
        ];
        foreach ((string nome, Topologia t, string chave, int lado, Porta[] esperadas) in casos)
            Afirmar.Sequencia(esperadas, Portas(t, chave, lado), $"{nome}, {chave}, lado {lado}");
    }

    // D1: com uma barra de tarefas vertical entre os dois monitores, as áreas úteis não se tocam, e a lateral é parede, mesmo
    // com as telas encostadas.
    [Teste]
    public static void Portas_BarraVerticalEntreOsMonitores_EhParede()
    {
        var t = new Topologia([
            new MonitorDoDesktop(Display1, new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true),
            new MonitorDoDesktop(Display2, new RetanguloPx(1920, 0, 3840, 1080), new RetanguloPx(1982, 0, 3840, 1080), 96, false),
        ]);
        Afirmar.Sequencia([], Portas(t, Display1, +1), "a barra entre os dois: sem porta");
        Afirmar.Sequencia([], Portas(t, Display2, -1), "nem no sentido contrário");
    }

    // O resto da lateral é parede, em trechos.
    [Teste]
    public static void TrechosDeParede_ALateralMenosAsPortas()
    {
        Afirmar.Sequencia([(0, 1032)], Passagens.TrechosDeParede(UmMonitor, M(UmMonitor, Display1), +1), "sem vizinho, a lateral inteira");
        Afirmar.Sequencia([], Passagens.TrechosDeParede(LadoALado, M(LadoALado, Display1), +1), "porta na altura inteira: nenhuma parede");
        Afirmar.Sequencia([(0, 400)], Passagens.TrechosDeParede(DegrauDesalinhado, M(DegrauDesalinhado, Display1), +1), "acima da porta do degrau");
        Afirmar.Sequencia([(1032, 1432)], Passagens.TrechosDeParede(DegrauDesalinhado, M(DegrauDesalinhado, Display2), -1), "o espelho do degrau, abaixo da porta");
        Afirmar.Sequencia([(-420, 0), (1032, 1452)], Passagens.TrechosDeParede(TopologiasDeExemplo.Retrato, M(TopologiasDeExemplo.Retrato, Display2), -1), "acima e abaixo da porta do retrato");
    }

    // D2 e 4.1: a travessia plana. Mesmo chão, porta indo até o chão com a altura do sprite maior dos dois e o sprite cabendo
    // no vizinho. Degrau, vão e quina não são planos (o degrau é o passo seguinte do P13).
    [Teste]
    public static void PortaPlana_SoComOMesmoChao()
    {
        Afirmar.Igual(new Porta(Display2, -1, 0, 0, 1032), Passagens.PortaPlana(SecundarioAEsquerda, M(SecundarioAEsquerda, Display1), -1, Sprite), "a máquina do usuário: plana");
        Afirmar.Igual(new Porta(Display1, +1, 0, 0, 1032), Passagens.PortaPlana(SecundarioAEsquerda, M(SecundarioAEsquerda, Display2), +1, Sprite), "e de volta");
        Afirmar.Igual(new Porta(Display2, +1, 1920, 0, 1032), Passagens.PortaPlana(LadoALado, M(LadoALado, Display1), +1, Sprite), "lado a lado");
        Afirmar.Igual(new Porta(Display1, +1, 0, 0, 1032), Passagens.PortaPlana(PrincipalADireita, M(PrincipalADireita, Display2), +1, Sprite), "mesmo chão, telas de alturas diferentes");
        Afirmar.Nulo(Passagens.PortaPlana(DegrauDesalinhado, M(DegrauDesalinhado, Display1), +1, Sprite), "degrau: chãos diferentes");
        Afirmar.Nulo(Passagens.PortaPlana(TopologiasDeExemplo.Retrato, M(TopologiasDeExemplo.Retrato, Display1), +1, Sprite), "retrato: o chão do vizinho é mais baixo");
        Afirmar.Nulo(Passagens.PortaPlana(VaoEntreMonitores, M(VaoEntreMonitores, Display1), +1, Sprite), "vão: sem porta");
        Afirmar.Nulo(Passagens.PortaPlana(SecundarioAEsquerda, M(SecundarioAEsquerda, Display1), +1, Sprite), "do lado sem vizinho");

        // A porta precisa ter a altura do sprite maior dos dois: uma porta de 100 px com um sprite de 128 px não serve.
        var baixa = new Topologia([
            new MonitorDoDesktop(Display1, new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true),
            new MonitorDoDesktop(Display2, new RetanguloPx(1920, 932, 3840, 2012), new RetanguloPx(1920, 932, 3840, 1032), 96, false),
        ]);
        Afirmar.Igual(new Porta(Display2, +1, 1920, 932, 1032), Passagens.Portas(baixa, M(baixa, Display1), +1).Single(), "a porta existe, com 100 px");
        Afirmar.Nulo(Passagens.PortaPlana(baixa, M(baixa, Display1), +1, Sprite), "mas é baixa demais para o sprite de 128 px");
        Afirmar.Igual(new Porta(Display2, +1, 1920, 932, 1032), Passagens.PortaPlana(baixa, M(baixa, Display1), +1, new TamanhoDip(64, 64)), "um sprite de 64 px passa");
    }

    // A união das áreas úteis (4.2): um retângulo dentro dela, mesmo montado entre dois monitores, conta; um pedaço fora (na
    // barra de tarefas, num vão ou acima do degrau), não.
    [Teste]
    public static void NaUniaoDasAreasUteis_MontadoEntreDoisMonitores()
    {
        Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(SecundarioAEsquerda, new RetanguloPx(-64, 904, 64, 1032)), "montado sobre x = 0, no chão");
        Afirmar.Falso(Passagens.NaUniaoDasAreasUteis(SecundarioAEsquerda, new RetanguloPx(-64, 905, 64, 1033)), "um pixel na barra");
        Afirmar.Falso(Passagens.NaUniaoDasAreasUteis(VaoEntreMonitores, new RetanguloPx(1900, 500, 1940, 540)), "no vão");
        Afirmar.Falso(Passagens.NaUniaoDasAreasUteis(DegrauDesalinhado, new RetanguloPx(1900, 300, 1940, 340)), "acima do degrau, do lado do vizinho");
        Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(DegrauDesalinhado, new RetanguloPx(1900, 500, 1940, 540)), "na faixa da porta");
    }

    // ---------------------------------------------------------------- P13b: o salto de degrau (D2, D3, D6 e 4.2)

    private static readonly ParametrosDeMovimento Fisica = new();

    /// <summary>O salto de degrau, com um recuo de até duas larguras do sprite antes da lateral (o planejado; P13b).</summary>
    private static Travessia? Salto(Topologia t, string chave, int lado, int recuoMaximo = 256) => Passagens.SaltoDeDegrau(t, M(t, chave), lado, Sprite, Fisica, 60, recuoMaximo);

    // D2 e D3: o degrau, com o chão do vizinho a uma distância Δ ≠ 0 dentro do alcance, medida em DIP pela menor escala dos
    // dois: até 480 DIP para baixo e até 120 DIP para cima. Fora disso, ou sem porta, nada (a subida maior é o transbordo, P13c).
    // Saindo da beirada, sem recuo (a caminhada comum que chega à porta), a descida serve e uma subida de 120 DIP, não: o
    // sprite precisa ganhar altura antes de a frente dele passar da borda.
    [Teste]
    public static void SaltoDeDegrau_PeloAlcanceNaMenorEscala()
    {
        Afirmar.NaoNulo(Salto(DegrauDesalinhado, Display1, +1), "degrau de 400 DIP para baixo: salta");
        Afirmar.Nulo(Salto(DegrauDesalinhado, Display2, -1), "degrau de 400 DIP para cima: além de 120");
        Afirmar.NaoNulo(Salto(TopologiasDeExemplo.Retrato, Display1, +1), "420 DIP para baixo: salta");
        Afirmar.NaoNulo(Salto(EscalasMistas, Display1, +1), "24 px para baixo, 24 DIP na menor escala (1): salta");
        Afirmar.NaoNulo(Salto(EscalasMistas, Display2, -1), "24 DIP para cima: salta");
        Afirmar.NaoNulo(Salto(EscalasMistas, Display1, -1), "24 px para cima, 16 DIP na menor escala (1,5): salta");
        Afirmar.NaoNulo(Salto(TresMonitores, Display2, +1), "168 DIP para baixo: salta");
        Afirmar.Nulo(Salto(TresMonitores, Display1, -1), "168 DIP para cima: além de 120");
        Afirmar.Nulo(Salto(SecundarioAEsquerda, Display1, -1), "o mesmo chão é a travessia plana, não o salto");
        Afirmar.Nulo(Salto(VaoEntreMonitores, Display1, +1), "sem porta, sem salto");

        // Os limites: 480 e 120 DIP valem; 481 e 121, não.
        static Topologia Degrau(int delta) => new([
            new MonitorDoDesktop(Display1, new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true),
            new MonitorDoDesktop(Display2, new RetanguloPx(1920, delta, 3840, 1080 + delta), new RetanguloPx(1920, delta, 3840, 1032 + delta), 96, false),
        ]);
        Afirmar.NaoNulo(Salto(Degrau(480), Display1, +1), "480 DIP para baixo");
        Afirmar.Nulo(Salto(Degrau(481), Display1, +1), "481 DIP para baixo");
        Afirmar.NaoNulo(Salto(Degrau(-120), Display1, +1), "120 DIP para cima");
        Afirmar.Nulo(Salto(Degrau(-121), Display1, +1), "121 DIP para cima");
        Afirmar.NaoNulo(Salto(Degrau(480), Display1, +1, recuoMaximo: 0), "descer saindo da beirada");
        Afirmar.Nulo(Salto(Degrau(-120), Display1, +1, recuoMaximo: 0), "subir 120 DIP saindo da beirada: sem altura antes da borda");
        Travessia comRecuo = Afirmar.NaoNulo(Salto(Degrau(-120), Display1, +1), "com recuo");
        Afirmar.Verdadeiro(comRecuo.X0 < 1920 - 64, $"a partida antes da beirada ({comRecuo.X0})");
    }

    // D6: o arco é fechado, com a gravidade real na escala da origem; em todos os passos o sprite (com o tamanho do monitor
    // da âncora) fica na união das áreas úteis, e o último passo é exatamente o destino: a âncora no chão do vizinho, dentro
    // dele. Pousar encostado na lateral de entrada faria o sprite passar pela barra do monitor de cima, ao descer, ou subir
    // por dentro dela: o solucionador procura o tempo de voo e um pouso um pouco para dentro (DEC-032).
    [Teste]
    public static void SaltoDeDegrau_ArcoNaUniaoEPousoExato()
    {
        foreach ((string nome, Topologia t, string chave, int lado) in new[]
        {
            ("DegrauDesalinhado", DegrauDesalinhado, Display1, +1),
            ("Retrato", TopologiasDeExemplo.Retrato, Display1, +1),
            ("EscalasMistas", EscalasMistas, Display1, +1),
            ("EscalasMistas", EscalasMistas, Display2, -1),
            ("TresMonitores", TresMonitores, Display2, +1),
        })
        {
            Travessia s = Afirmar.NaoNulo(Salto(t, chave, lado), nome);
            MonitorDoDesktop origem = M(t, s.ChaveOrigem), destino = M(t, s.ChaveDestino);
            Superficies sd = Superficies.Do(t, destino, Sprite.ParaPixels(destino.Dpi));
            Afirmar.Igual((double)sd.Chao, s.YDestino, $"{nome}: o pouso no chão do vizinho");
            Afirmar.Verdadeiro(s.XDestino >= sd.Esquerda && s.XDestino <= sd.Direita, $"{nome}: o pouso dentro do vizinho ({s.XDestino}, entre {sd.Esquerda} e {sd.Direita})");
            Afirmar.Verdadeiro(s.PassosTotais > 0, $"{nome}: passos");
            for (int k = 1; k <= s.PassosTotais; k++)
            {
                (double x, double y) = Passagens.PosicaoNoSalto(s, k, 60);
                var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
                MonitorDoDesktop m = Passagens.PassouDaBorda(s, ancora) ? destino : origem;
                RetanguloPx r = Posicionador.RetanguloDoSprite(ancora, Sprite.ParaPixels(m.Dpi));
                Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(t, r), $"{nome}, passo {k} de {s.PassosTotais}: {r} na união");
            }
            Afirmar.Igual((s.XDestino, s.YDestino), Passagens.PosicaoNoSalto(s, s.PassosTotais, 60), $"{nome}: o último passo é o destino");
        }
    }

    // ---------------------------------------------------------------- P13c: o transbordo (D5 e C14 da crítica)

    // Escalando a lateral do monitor mais baixo, com os pés na altura do chão do vizinho mais alto (o topo do trecho de parede
    // abaixo da porta), um arco curto o leva ao chão do vizinho, com o sprite sempre na união das áreas úteis.
    [Teste]
    public static void Transbordo_DoTopoDoTrechoDeParedeParaOChaoDoVizinho()
    {
        foreach ((string nome, Topologia t, string chave, int lado, string vizinho, int chao) in new[]
        {
            ("DegrauDesalinhado", DegrauDesalinhado, Display2, -1, Display1, 1032),
            ("TresMonitores, esquerda", TresMonitores, Display1, -1, Display2, 1212),
            ("TresMonitores, direita", TresMonitores, Display1, +1, Display3, 1020),
        })
        {
            Travessia s = Afirmar.NaoNulo(Passagens.Transbordo(t, M(t, chave), lado, chao, Sprite, Fisica, 60), nome);
            Afirmar.Igual((vizinho, (double)chao, (double)chao), (s.ChaveDestino, s.Y0, s.YDestino), $"{nome}: parte e pousa na altura do chão do vizinho");
            MonitorDoDesktop origem = M(t, s.ChaveOrigem), destino = M(t, s.ChaveDestino);
            for (int k = 1; k <= s.PassosTotais; k++)
            {
                (double x, double y) = Passagens.PosicaoNoSalto(s, k, 60);
                var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
                MonitorDoDesktop m = Passagens.PassouDaBorda(s, ancora) ? destino : origem;
                Afirmar.Verdadeiro(Passagens.NaUniaoDasAreasUteis(t, Posicionador.RetanguloDoSprite(ancora, Sprite.ParaPixels(m.Dpi))), $"{nome}, passo {k}: na união");
            }
        }
        Afirmar.Nulo(Passagens.Transbordo(DegrauDesalinhado, M(DegrauDesalinhado, Display1), +1, 1432, Sprite, Fisica, 60), "o vizinho mais baixo não é transbordo (é o salto para baixo)");
        Afirmar.Nulo(Passagens.Transbordo(DegrauDesalinhado, M(DegrauDesalinhado, Display2), -1, 1000, Sprite, Fisica, 60), "fora da altura do chão do vizinho, nada");
        Afirmar.Nulo(Passagens.Transbordo(SecundarioAEsquerda, M(SecundarioAEsquerda, Display1), -1, 1032, Sprite, Fisica, 60), "o mesmo chão é a travessia plana");
    }
}
