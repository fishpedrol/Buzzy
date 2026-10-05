using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes;

/// <summary>
/// A posição guardada acompanha a topologia em execução (DEC-030, passo P8 da Fase 5): <see cref="Posicionador.SoTranslacao"/>
/// diz se o monitor só mudou de lugar no desktop virtual, <see cref="Posicionador.MonitorCorrespondente"/> acha o mesmo monitor
/// na topologia nova (pela chave ou, com chave nova, pela tela) e <see cref="Posicionador.Rebasear"/> leva a posição para as
/// coordenadas novas: pelas frações, no monitor que continua; transladada com o sobrevivente medido nas coordenadas antigas,
/// quando ele some. A tela guardada nunca é transladada por cálculo. As âncoras esperadas são calculadas à mão; o sprite tem
/// 128 DIP.
/// </summary>
internal static class RebasearTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    // ---------------------------------------------------------------- SoTranslacao

    [Teste]
    public static void SoTranslacao_TrocaDePrincipalERearranjo()
    {
        // S2, máquina do usuário: o DISPLAY2 vira principal e a origem vai para ele; todos andam +1920 em x.
        Topologia s2 = SecundarioAEsquerda, s2Rebaseada = Rebaseada(s2, Display2);
        AfirmarTranslacao(Monitor(s2, Display1), Monitor(s2Rebaseada, Display1), 1920, 0, "S2, o principal antigo");
        AfirmarTranslacao(Monitor(s2, Display2), Monitor(s2Rebaseada, Display2), 1920, 0, "S2, o principal novo");

        // S3, secundário acima: a origem vai para o canto dele, (-320,-1440); todos andam (+320,+1440).
        Topologia s3 = EmpilhadoSecundarioAcima;
        AfirmarTranslacao(Monitor(s3, Display1), Monitor(Rebaseada(s3, Display2), Display1), 320, 1440, "S3, y negativo");

        // Nada mudou, ou só a marca de principal: translação nula.
        AfirmarTranslacao(Monitor(s2, Display1), Monitor(LadoALado, Display1), 0, 0, "o mesmo monitor em outra topologia");
        AfirmarTranslacao(Monitor(s2, Display2), Monitor(s2, Display2) with { Principal = true }, 0, 0, "o principal não conta");
    }

    [Teste]
    public static void SoTranslacao_GeometriaDiferenteNaoEhTranslacao()
    {
        MonitorDoDesktop m = Monitor(UmMonitor, Display1);
        (string Caso, MonitorDoDesktop Depois)[] casos =
        [
            ("barra de tarefas no topo", Monitor(BarraNoTopo, Display1)),
            ("barra oculta", Monitor(BarraOculta, Display1)),
            ("144 DPI", m with { Dpi = 144, AreaUtil = Ret(0, 0, 1920, 1008) }),
            ("só o DPI", m with { Dpi = 120 }),
            ("2560x1440 com o mesmo canto", m with { Tela = Ret(0, 0, 2560, 1440), AreaUtil = Ret(0, 0, 2560, 1392) }),
            ("girado", m with { Tela = Ret(0, 0, 1080, 1920), AreaUtil = Ret(0, 0, 1080, 1872) }),
            ("tela e área útil deslocadas por valores diferentes", m with { Tela = Ret(100, 0, 2020, 1080), AreaUtil = Ret(100, 48, 2020, 1080) }),
        ];
        foreach ((string caso, MonitorDoDesktop depois) in casos)
            Afirmar.Falso(Posicionador.SoTranslacao(m, depois, out _, out _), caso);
    }

    // ---------------------------------------------------------------- MonitorCorrespondente

    [Teste]
    public static void MonitorCorrespondente_PelaChaveMesmoComOutraGeometria()
    {
        Topologia s2 = SecundarioAEsquerda;
        Topologia maior = ComMonitor(s2, Display2, m => m with { Tela = Ret(-2560, -360, 0, 1080), AreaUtil = Ret(-2560, -360, 0, 1032) });
        Afirmar.Igual(Monitor(maior, Display2), Posicionador.MonitorCorrespondente(s2, maior, Display2, Ret(-1920, 0, 0, 1080)), "a chave vale mais que a tela");
        Afirmar.Igual(Monitor(maior, Display2), Posicionador.MonitorCorrespondente(s2, maior, Display2, null), "sem tela, pela chave");
    }

    [Teste]
    public static void MonitorCorrespondente_ChaveNovaComAMesmaTela_ApelidoPorRetangulo()
    {
        // A consulta de vídeo voltou: as chaves de reserva viram resumos do caminho, com as mesmas telas.
        Topologia s2 = SecundarioAEsquerda;
        Topologia renomeada = ComChavesRenomeadas(s2, Renomear);
        Afirmar.Igual(Monitor(renomeada, Renomear(Display2)), Posicionador.MonitorCorrespondente(s2, renomeada, Display2, Ret(-1920, 0, 0, 1080)), "o mesmo monitor, com a chave nova");
        Afirmar.Igual(Monitor(renomeada, Renomear(Display1)), Posicionador.MonitorCorrespondente(s2, renomeada, Display1, Ret(0, 0, 1920, 1080)), "o principal também");
        Afirmar.Nulo(Posicionador.MonitorCorrespondente(s2, renomeada, Display2, null), "sem a tela, não há apelido");
        Afirmar.Nulo(Posicionador.MonitorCorrespondente(s2, renomeada, Display2, Ret(-1920, 0, 0, 1079)), "só com a tela exatamente igual");
    }

    [Teste]
    public static void MonitorCorrespondente_SobreviventeNaOrigemNaoEhApelido()
    {
        // S2, o principal é desconectado: o DISPLAY2 assume e vai para (0,0)-(1920,1080), a tela que era do DISPLAY1. Ele
        // já existia, então não é o DISPLAY1 com outra chave (exemplo R15b do desenho).
        Topologia s2 = SecundarioAEsquerda;
        Topologia semOPrincipal = SemOPrincipal(s2, Display1, Display2);
        Afirmar.Igual(Ret(0, 0, 1920, 1080), Monitor(semOPrincipal, Display2).Tela, "o sobrevivente ocupa a tela do principal antigo");
        Afirmar.Nulo(Posicionador.MonitorCorrespondente(s2, semOPrincipal, Display1, Ret(0, 0, 1920, 1080)), "não é apelido");
    }

    // ---------------------------------------------------------------- Rebasear: o monitor continua

    [Teste]
    public static void Rebasear_MonitorPresente_AcompanhaAsFracoesNaAreaUtilAtual()
    {
        // S2: a 25% do DISPLAY2, (-1440,1032). Ele vira principal, em (0,0)-(1920,1080): a âncora vai a (480,1032).
        Topologia s2 = SecundarioAEsquerda;
        PosicaoDoPersonagem noSecundario = DescreverNo(s2, Display2, 0.25, 1);
        AfirmarPosicao(Display2, 0.25, 1, new PontoPx(-1440, 1032), Ret(-1920, 0, 0, 1080), noSecundario, "antes");
        AfirmarPosicao(Display2, 0.25, 1, new PontoPx(480, 1032), Ret(0, 0, 1920, 1080),
            Posicionador.Rebasear(s2, Rebaseada(s2, Display2), noSecundario, Sprite), "troca de principal: as coordenadas novas, com a tela nova");

        // R15h: o DISPLAY1 passa a 144 DPI, com a área útil (0,0)-(1920,1008). A 85%: 0,85 · 1920 = 1632, no chão novo.
        Topologia a144 = ComMonitor(s2, Display1, m => m with { Dpi = 144, AreaUtil = Ret(0, 0, 1920, 1008) });
        AfirmarPosicao(Display1, 0.85, 1, new PontoPx(1632, 1008), Ret(0, 0, 1920, 1080),
            Posicionador.Rebasear(s2, a144, DescreverNo(s2, Display1, 0.85, 1), Sprite), "outra escala: a mesma fração na área útil nova");

        // Nada mudou no monitor dela: a posição fica igual.
        PosicaoDoPersonagem inicial = DescreverNo(s2, Display1, 0.85, 1);
        Afirmar.Igual(inicial, Posicionador.Rebasear(s2, LadoALado, inicial, Sprite), "outro monitor mudou: igual");
    }

    [Teste]
    public static void Rebasear_ChaveNovaComAMesmaTela_PassaAChaveNova()
    {
        Topologia s2 = SecundarioAEsquerda;
        PosicaoDoPersonagem noSecundario = DescreverNo(s2, Display2, 0.25, 1);
        AfirmarPosicao(Renomear(Display2), 0.25, 1, new PontoPx(-1440, 1032), Ret(-1920, 0, 0, 1080),
            Posicionador.Rebasear(s2, ComChavesRenomeadas(s2, Renomear), noSecundario, Sprite), "pelo retângulo, com a chave nova");
    }

    // ---------------------------------------------------------------- Rebasear: o monitor sumiu

    [Teste]
    public static void Rebasear_MonitorSumido_TransladaComOSobreviventeMedidoNasCoordenadasAntigas()
    {
        // R15c: [2](-1920..0) [1*](0..1920) [3](1920..3840), no 1 a 85%, (1632,1032). Nas coordenadas antigas, o 3 fica a
        // 288 px do pixel dos pés e o 2 a 1633 px: o sobrevivente é o 3.
        Topologia t = TresEmLinhaComPrincipalNoMeio;
        PosicaoDoPersonagem noMeio = DescreverNo(t, Display1, 0.85, 1);

        // O 1 é desconectado e o 2 assume: a origem vai para ele (+1920), o 3 vai a (3840..5760). A âncora anda com o 3.
        Topologia promove2 = SemOPrincipal(t, Display1, Display2);
        PosicaoDoPersonagem rebaseada = Posicionador.Rebasear(t, promove2, noMeio, Sprite);
        AfirmarPosicao(Display1, 0.85, 1, new PontoPx(3552, 1032), Ret(0, 0, 1920, 1080), rebaseada, "promove o 2: a chave, as frações e a tela ficam");
        (Posicionamento r, PosicaoDoPersonagem nova) = Posicionador.Reacomodar(promove2, rebaseada, Sprite);
        Afirmar.Igual(Display3, r.Monitor.Chave, "promove o 2: vai ao 3, o mais próximo");
        Afirmar.Igual(new PontoPx(5472, 1032), r.Ancora, "promove o 2: 85% do 3, 3840 + 1632");
        Afirmar.Igual(Display3, nova.ChaveMonitor, "promove o 2: a posição passa a ser do 3");
        // Com a âncora antiga nas coordenadas novas, (1632,1031) cairia dentro do 2: o monitor errado.
        Afirmar.Igual(Display2, promove2.MonitorMaisProximo(new PontoPx(1632, 1031)).Chave, "sem acompanhar, iria para o 2");

        // O 3 assume: a origem vai para ele (-1920). A âncora anda com o 3 e termina a 85% dele, (1632,1032).
        Topologia promove3 = SemOPrincipal(t, Display1, Display3);
        PosicaoDoPersonagem rebaseada3 = Posicionador.Rebasear(t, promove3, noMeio, Sprite);
        AfirmarPosicao(Display1, 0.85, 1, new PontoPx(-288, 1032), Ret(0, 0, 1920, 1080), rebaseada3, "promove o 3");
        (Posicionamento r3, _) = Posicionador.Reacomodar(promove3, rebaseada3, Sprite);
        Afirmar.Igual((Display3, new PontoPx(1632, 1032)), (r3.Monitor.Chave, r3.Ancora), "promove o 3: o 3, a 85%");
    }

    [Teste]
    public static void Rebasear_MonitorSumido_NuncaTransladaATela()
    {
        // C3: a tela é a do monitor da chave na última vez em que a posição foi descrita nele. Transladada, ela seria uma
        // tela que nunca existiu, e a partida seguinte poderia achar por engano o monitor "pelo retângulo".
        Topologia t = TresEmLinhaComPrincipalNoMeio;
        PosicaoDoPersonagem noMeio = DescreverNo(t, Display1, 0.5, 0.5);
        Topologia promove2 = SemOPrincipal(t, Display1, Display2);
        PosicaoDoPersonagem rebaseada = Posicionador.Rebasear(t, promove2, noMeio, Sprite);
        Afirmar.Igual(Ret(0, 0, 1920, 1080), rebaseada.TelaDoMonitor, "a tela da época, sem deslocar");
        Afirmar.Igual(new PontoPx(noMeio.AncoraAbsoluta.X + 1920, noMeio.AncoraAbsoluta.Y), rebaseada.AncoraAbsoluta, "só a âncora anda");
        Afirmar.Igual((Display1, noMeio.FracaoX, noMeio.FracaoY), (rebaseada.ChaveMonitor, rebaseada.FracaoX, rebaseada.FracaoY), "a chave e as frações ficam");

        // Sem tela conhecida, continua desconhecida.
        PosicaoDoPersonagem semTela = noMeio with { TelaDoMonitor = null };
        Afirmar.Nulo(Posicionador.Rebasear(t, promove2, semTela, Sprite).TelaDoMonitor, "desconhecida continua desconhecida");
    }

    [Teste]
    public static void Rebasear_SemSobrevivente_DevolveAPosicao()
    {
        // Todas as chaves são novas e as telas, outras: não há como medir nas coordenadas antigas.
        Topologia s2 = SecundarioAEsquerda;
        var outra = new Topologia([Principal("mon:aaaa", Ret(0, 0, 2560, 1440), Ret(0, 0, 2560, 1392), 96)]);
        PosicaoDoPersonagem p = DescreverNo(s2, Display2, 0.25, 1);
        Afirmar.Igual(p, Posicionador.Rebasear(s2, outra, p, Sprite), "igual");
    }

    [Teste]
    public static void Rebasear_EmpateEntreSobreviventes_VenceOPrincipalDepoisAOrdem()
    {
        // A(0..1000) e C(2001..3001) ficam a 501 px do pixel dos pés no meio do B(1000..2001), que sai. O C ainda é
        // rearranjado +100 em x, para a escolha aparecer na âncora: com o A, ela não anda; com o C, anda 100.
        MonitorDoDesktop a = Principal("A", Ret(0, 0, 1000, 1000), Ret(0, 0, 1000, 1000), 96);
        MonitorDoDesktop b = Secundario("B", Ret(1000, 0, 2001, 1000), Ret(1000, 0, 2001, 1000), 96);
        MonitorDoDesktop c = Secundario("C", Ret(2001, 0, 3001, 1000), Ret(2001, 0, 3001, 1000), 96);
        var antiga = new Topologia([c, b, a]);
        MonitorDoDesktop cMovido = c with { Tela = Ret(2101, 0, 3101, 1000), AreaUtil = Ret(2101, 0, 3101, 1000) };
        var p = new PosicaoDoPersonagem("B", 0.5, 1, new PontoPx(1500, 1000)) { TelaDoMonitor = b.Tela };
        Afirmar.Igual(new PontoPx(1500, 1000), Posicionador.Rebasear(antiga, new Topologia([cMovido, a]), p, Sprite).AncoraAbsoluta, "empate: vence o principal, mesmo depois na lista");

        // Sem o principal no empate, vale a ordem da topologia nova.
        MonitorDoDesktop d = Principal("D", Ret(0, 2000, 500, 2500), Ret(0, 2000, 500, 2500), 96);
        var semPrincipal = new Topologia([c, b, a with { Principal = false }, d]);
        var depois = new Topologia([cMovido, a with { Principal = false }, d]);
        Afirmar.Igual(new PontoPx(1600, 1000), Posicionador.Rebasear(semPrincipal, depois, p, Sprite).AncoraAbsoluta, "empate sem principal: o primeiro da lista nova, o C");
    }

    // ---------------------------------------------------------------- propriedades

    [Teste]
    public static void Rebasear_EmMilharesDeMudancasAleatorias_SoUsaTelasQueExistiram()
    {
        // Para cada mudança do gerador (resolução, escala, barra, conectar, desconectar, trocar o principal, tudo novo):
        // - a tela guardada é a de antes ou a de um monitor da topologia nova, nunca uma calculada (C3);
        // - com o monitor correspondente, a posição passa a ser dele: a chave, a tela e a âncora das frações;
        // - sem ele, a chave e as frações ficam, e a âncora anda pela translação de um sobrevivente (ou fica).
        var mestre = new Random(20261008);
        int correspondentes = 0, sumidos = 0, transladados = 0;
        for (int caso = 0; caso < 3000; caso++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            var gerador = new GeradorDeTopologias(rnd);
            Topologia antiga = gerador.NovaTopologia();
            MonitorDoDesktop m = antiga.Monitores[rnd.Next(antiga.Monitores.Count)];
            PosicaoDoPersonagem p = Posicionador.Descrever(Posicionador.NoMonitor(m, rnd.NextDouble(), rnd.NextDouble(), Sprite));
            Topologia nova = rnd.Next(4) == 0 ? gerador.NovaTopologia() : gerador.Mudar(antiga);
            PosicaoDoPersonagem r = Posicionador.Rebasear(antiga, nova, p, Sprite);
            string onde = $"caso {caso} (semente {semente})";

            Afirmar.Verdadeiro(r.TelaDoMonitor == p.TelaDoMonitor || nova.Monitores.Any(n => n.Tela == r.TelaDoMonitor), $"{onde}: tela {r.TelaDoMonitor} inventada");
            MonitorDoDesktop? n = nova.PorChave(m.Chave) ?? nova.Monitores.FirstOrDefault(x => x.Tela == m.Tela && antiga.PorChave(x.Chave) is null);
            if (n is not null)
            {
                correspondentes++;
                Afirmar.Igual((n.Chave, (RetanguloPx?)n.Tela, Posicionador.NoMonitor(n, p.FracaoX, p.FracaoY, Sprite).Ancora), (r.ChaveMonitor, r.TelaDoMonitor, r.AncoraAbsoluta), $"{onde}: no correspondente");
                Afirmar.Igual((p.FracaoX, p.FracaoY), (r.FracaoX, r.FracaoY), $"{onde}: as frações ficam");
                if (Posicionador.SoTranslacao(m, n, out int dx, out int dy))
                {
                    transladados++;
                    Afirmar.Igual(new PontoPx(p.AncoraAbsoluta.X + dx, p.AncoraAbsoluta.Y + dy), r.AncoraAbsoluta, $"{onde}: só transladado, a âncora anda exatamente ({dx},{dy})");
                }
                continue;
            }
            sumidos++;
            Afirmar.Igual((p.ChaveMonitor, p.FracaoX, p.FracaoY, p.TelaDoMonitor), (r.ChaveMonitor, r.FracaoX, r.FracaoY, r.TelaDoMonitor), $"{onde}: sumido, só a âncora muda");
            (int Dx, int Dy)[] translacoes = [(0, 0), .. nova.Monitores.Where(x => antiga.PorChave(x.Chave) is not null)
                .Select(x => (x.Tela.Esquerda - antiga.PorChave(x.Chave)!.Tela.Esquerda, x.Tela.Topo - antiga.PorChave(x.Chave)!.Tela.Topo))];
            Afirmar.Verdadeiro(translacoes.Contains((r.AncoraAbsoluta.X - p.AncoraAbsoluta.X, r.AncoraAbsoluta.Y - p.AncoraAbsoluta.Y)),
                $"{onde}: a âncora andou ({r.AncoraAbsoluta.X - p.AncoraAbsoluta.X},{r.AncoraAbsoluta.Y - p.AncoraAbsoluta.Y}), que não é a translação de nenhum sobrevivente");
        }
        Console.WriteLine($"         3000 mudanças: {correspondentes} com o monitor correspondente ({transladados} só transladados), {sumidos} com ele sumido");
        Afirmar.Verdadeiro(transladados > 100 && sumidos > 100 && correspondentes - transladados > 100, "o gerador exercita as três situações");
    }

    [Teste]
    public static void ArgumentosNulosLancam()
    {
        Topologia t = UmMonitor;
        PosicaoDoPersonagem p = DescreverNo(t, Display1, 0.5, 1);
        MonitorDoDesktop m = Monitor(t, Display1);
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.SoTranslacao(null!, m, out _, out _));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.SoTranslacao(m, null!, out _, out _));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.MonitorCorrespondente(null!, t, Display1, null));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.MonitorCorrespondente(t, null!, Display1, null));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.MonitorCorrespondente(t, t, null!, null));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Rebasear(null!, t, p, Sprite));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Rebasear(t, null!, p, Sprite));
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Rebasear(t, t, null!, Sprite));
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>Chave de resumo no lugar do nome GDI, como a do app (o texto só importa por ser diferente).</summary>
    private static string Renomear(string chave) => "mon:" + chave[^1];

    private static MonitorDoDesktop Monitor(Topologia t, string chave) => Afirmar.NaoNulo(t.PorChave(chave), chave);

    private static PosicaoDoPersonagem DescreverNo(Topologia t, string chave, double fx, double fy)
        => Posicionador.Descrever(Posicionador.NoMonitor(Monitor(t, chave), fx, fy, Sprite));

    private static void AfirmarTranslacao(MonitorDoDesktop antes, MonitorDoDesktop depois, int dx, int dy, string caso)
    {
        Afirmar.Verdadeiro(Posicionador.SoTranslacao(antes, depois, out int obtidoX, out int obtidoY), $"{caso}: só translação");
        Afirmar.Igual((dx, dy), (obtidoX, obtidoY), $"{caso}: deslocamento");
    }

    /// <summary>Confere a posição campo a campo: a igualdade do registro também compara a tela guardada.</summary>
    private static void AfirmarPosicao(string chave, double fx, double fy, PontoPx ancora, RetanguloPx? tela, PosicaoDoPersonagem obtida, string caso)
    {
        Afirmar.Igual(chave, obtida.ChaveMonitor, $"{caso}: chave");
        Afirmar.Aproximado(fx, obtida.FracaoX, 1e-12, $"{caso}: fração x");
        Afirmar.Aproximado(fy, obtida.FracaoY, 1e-12, $"{caso}: fração y");
        Afirmar.Igual(ancora, obtida.AncoraAbsoluta, $"{caso}: âncora");
        Afirmar.Igual(tela, obtida.TelaDoMonitor, $"{caso}: tela");
    }
}
