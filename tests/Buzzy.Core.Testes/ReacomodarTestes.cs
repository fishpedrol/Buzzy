using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes;

/// <summary>
/// Reacomodação depois de uma mudança de topologia (Posicionador.Reacomodar,
/// ARCHITECTURE.md 2.8). Quase todos os cenários começam na máquina do usuário
/// (<see cref="TopologiasDeExemplo.SecundarioAEsquerda"/>), com o sprite de 200x200 DIP.
/// </summary>
internal static class ReacomodarTestes
{
    private static readonly TamanhoDip Sprite = new(200, 200);

    // Posição inicial na máquina do usuário: principal, âncora (1632,1032), frações (0,85; 1).
    private static PosicaoDoPersonagem PosicaoInicial => Posicionador.Descrever(Posicionador.Inicial(SecundarioAEsquerda, Sprite));

    // Posição no secundário da máquina do usuário, a um quarto da largura: âncora (-1440,1032).
    private static PosicaoDoPersonagem PosicaoNoSecundario
        => Posicionador.Descrever(Posicionador.NoMonitor(Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2)), 0.25, 1, Sprite));

    [Teste]
    public static void MesmaTopologiaNaoMoveOPersonagem()
    {
        foreach ((string nome, Topologia t) in Todas)
        {
            Posicionamento inicial = Posicionador.Inicial(t, Sprite);
            PosicaoDoPersonagem posicao = Posicionador.Descrever(inicial);
            (Posicionamento resultado, PosicaoDoPersonagem nova) = Posicionador.Reacomodar(t, posicao, Sprite);
            Afirmar.Igual(inicial, resultado, nome);
            Afirmar.Igual(posicao, nova, nome);
        }
    }

    // ---------------------------------------------------------------- mesmo monitor

    [Teste]
    public static void TrocaDeResolucaoMantemAPosicaoRelativa()
    {
        Topologia maior = ComMonitor(SecundarioAEsquerda, Display1, m => m with { Tela = Ret(0, 0, 2560, 1440), AreaUtil = Ret(0, 0, 2560, 1392) });
        AfirmarMesmaPosicaoRelativa(maior, PosicaoInicial, new PontoPx(2176, 1392), "2560x1440: 0,85 · 2560");

        Topologia menor = ComMonitor(SecundarioAEsquerda, Display1, m => m with { Tela = Ret(0, 0, 1280, 720), AreaUtil = Ret(0, 0, 1280, 672) });
        AfirmarMesmaPosicaoRelativa(menor, PosicaoInicial, new PontoPx(1088, 672), "1280x720: 0,85 · 1280");
    }

    [Teste]
    public static void BarraDeTarefasMovidaMantemAPosicaoRelativa()
    {
        (string Caso, RetanguloPx AreaUtil, PontoPx Ancora)[] casos =
        [
            ("barra no topo", Ret(0, 48, 1920, 1080), new PontoPx(1632, 1080)),
            ("área útil menor: barra de 96 px", Ret(0, 0, 1920, 984), new PontoPx(1632, 984)),
            ("área útil maior: barra oculta", Ret(0, 0, 1920, 1080), new PontoPx(1632, 1080)),
            ("barra à direita", Ret(0, 0, 1858, 1080), new PontoPx(1579, 1080)),
            ("barra à esquerda", Ret(62, 0, 1920, 1080), new PontoPx(1641, 1080)),
        ];

        foreach ((string caso, RetanguloPx util, PontoPx ancora) in casos)
            AfirmarMesmaPosicaoRelativa(ComMonitor(SecundarioAEsquerda, Display1, m => m with { AreaUtil = util }), PosicaoInicial, ancora, caso);
    }

    [Teste]
    public static void TrocaDeDpiRecalculaOTamanhoEMantemAPosicaoRelativa()
    {
        // 150%: barra de 72 px e sprite de 300 px.
        Topologia a144 = ComMonitor(SecundarioAEsquerda, Display1, m => m with { Dpi = 144, AreaUtil = Ret(0, 0, 1920, 1008) });
        Posicionamento p = AfirmarMesmaPosicaoRelativa(a144, PosicaoInicial, new PontoPx(1632, 1008), "144 DPI");
        Afirmar.Igual(new TamanhoPx(300, 300), p.Tamanho, "tamanho a 144 DPI");
        Afirmar.Igual(Ret(1482, 708, 1782, 1008), p.Retangulo, "retângulo a 144 DPI");

        PosicaoDoPersonagem depois = Posicionador.Reacomodar(a144, PosicaoInicial, Sprite).NovaPosicao;
        AfirmarMesmaPosicaoRelativa(SecundarioAEsquerda, depois, new PontoPx(1632, 1032), "de volta a 96 DPI");
    }

    [Teste]
    public static void TrocaDeDpiPertoDaBordaPrendeSemPerderAPosicaoDesejada()
    {
        // Âncora em x = 1800 (fração 0,9375). A 200% o sprite tem 400 px e só cabe com a
        // âncora até x = 1720; a fração guardada continua 0,9375 e, de volta a 100%, o
        // personagem volta para x = 1800.
        PosicaoDoPersonagem pertoDaBorda = Posicionador.Descrever(Posicionador.NoMonitor(SecundarioAEsquerda.Principal, 0.9375, 1, Sprite));
        Afirmar.Igual(new PontoPx(1800, 1032), pertoDaBorda.AncoraAbsoluta, "posição de partida");

        Topologia a192 = ComMonitor(SecundarioAEsquerda, Display1, m => m with { Dpi = 192, AreaUtil = Ret(0, 0, 1920, 984) });
        (Posicionamento r, PosicaoDoPersonagem nova) = Posicionador.Reacomodar(a192, pertoDaBorda, Sprite);
        Afirmar.Igual(new PontoPx(1720, 984), r.Ancora, "presa a 192 DPI");
        Afirmar.Igual(Ret(1520, 584, 1920, 984), r.Retangulo, "encostada na borda direita");
        Afirmar.Igual(pertoDaBorda.FracaoX, nova.FracaoX, "fração guardada");

        Afirmar.Igual(new PontoPx(1800, 1032), Posicionador.Reacomodar(SecundarioAEsquerda, nova, Sprite).Resultado.Ancora, "de volta a 96 DPI");
    }

    [Teste]
    public static void TrocaDoMonitorPrincipalMantemOPersonagemNoMesmoMonitor()
    {
        // O usuário torna o monitor da esquerda principal: o Windows move a origem para ele,
        // e o antigo principal passa para (1920,0)-(3840,1080).
        var principalTrocado = new Topologia([
            Secundario(Display1, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96),
            Principal(Display2, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        ]);
        AfirmarMesmaPosicaoRelativa(principalTrocado, PosicaoInicial, new PontoPx(3552, 1032), "1920 + 0,85 · 1920");
    }

    [Teste]
    public static void SecundarioEmCoordenadasNegativas()
    {
        Topologia maior = ComMonitor(SecundarioAEsquerda, Display2, m => m with { Tela = Ret(-2560, -360, 0, 1080), AreaUtil = Ret(-2560, -360, 0, 1032) });
        Posicionamento r = AfirmarMesmaPosicaoRelativa(maior, PosicaoNoSecundario, new PontoPx(-1920, 1032), "secundário passa a 2560x1440");
        Afirmar.Igual(Ret(-2020, 832, -1820, 1032), r.Retangulo, "retângulo no secundário maior");

        Topologia a144 = ComMonitor(SecundarioAEsquerda, Display2, m => m with { Dpi = 144, AreaUtil = Ret(-1920, 0, 0, 1008) });
        AfirmarMesmaPosicaoRelativa(a144, PosicaoNoSecundario, new PontoPx(-1440, 1008), "secundário a 144 DPI");

        // Encostado na borda x = 0: a 144 DPI o sprite de 300 px continua terminando em x = 0.
        PosicaoDoPersonagem naBorda = Posicionador.Descrever(Posicionador.NoMonitor(Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2)), 1, 1, Sprite));
        Afirmar.Igual(new PontoPx(-100, 1032), naBorda.AncoraAbsoluta, "encostado em x = 0 a 96 DPI");
        Posicionamento r144 = AfirmarMesmaPosicaoRelativa(a144, naBorda, new PontoPx(-150, 1008), "encostado em x = 0 a 144 DPI");
        Afirmar.Igual(Ret(-300, 708, 0, 1008), r144.Retangulo, "retângulo encostado em x = 0");
    }

    // Fase 5 (ARCHITECTURE.md 2.8): a posição passa a guardar a tela do monitor em que foi descrita
    // por último, que é a que a partida seguinte procura quando a chave não existir mais.
    [Teste]
    public static void MesmoMonitorComTelaNovaGuardaATelaNova()
    {
        Topologia maior = ComMonitor(SecundarioAEsquerda, Display2, m => m with { Tela = Ret(-2560, -360, 0, 1080), AreaUtil = Ret(-2560, -360, 0, 1032) });
        PosicaoDoPersonagem nova = Posicionador.Reacomodar(maior, PosicaoNoSecundario, Sprite).NovaPosicao;
        Afirmar.Igual(Ret(-2560, -360, 0, 1080), nova.TelaDoMonitor, "tela nova do secundário");
        Afirmar.Igual(PosicaoNoSecundario.FracaoX, nova.FracaoX, "fração x guardada");

        // Sem mudança, a tela guardada continua a mesma.
        Afirmar.Igual(Ret(-1920, 0, 0, 1080), Posicionador.Reacomodar(SecundarioAEsquerda, PosicaoNoSecundario, Sprite).NovaPosicao.TelaDoMonitor, "mesma topologia");
    }

    // ---------------------------------------------------------------- monitor que some

    [Teste]
    public static void MonitorRemovidoCaiNoMaisProximoDaUltimaAncora()
    {
        Topologia degrau = DegrauDesalinhado;
        Topologia semMeio = SemMonitor(degrau, Display2);
        MonitorDoDesktop meio = Afirmar.NaoNulo(degrau.PorChave(Display2));

        // Perto da direita do degrau do meio, em (3648,1432): o terceiro está a 192 px e o
        // principal a mais de 1700 px.
        PosicaoDoPersonagem aDireita = Posicionador.Descrever(Posicionador.NoMonitor(meio, 0.9, 1, Sprite));
        Afirmar.Igual(new PontoPx(3648, 1432), aDireita.AncoraAbsoluta, "partida à direita");
        (_, PosicaoDoPersonagem novaDireita) = AfirmarReacomodacao(semMeio, aDireita, Display3, new PontoPx(5568, 1832), "cai no terceiro");
        AfirmarPosicao(Display3, 0.9, 1.0, new PontoPx(5568, 1832), novaDireita, "posição passa a ser do terceiro");

        // Perto da esquerda, em (2112,1432): do pixel dos pés (2112,1431), o principal está a
        // √161153 ≈ 401 px e o terceiro a 1728 px.
        PosicaoDoPersonagem aEsquerda = Posicionador.Descrever(Posicionador.NoMonitor(meio, 0.1, 1, Sprite));
        Afirmar.Igual(new PontoPx(2112, 1432), aEsquerda.AncoraAbsoluta, "partida à esquerda");
        (_, PosicaoDoPersonagem novaEsquerda) = AfirmarReacomodacao(semMeio, aEsquerda, Display1, new PontoPx(192, 1032), "cai no principal");
        AfirmarPosicao(Display1, 0.1, 1.0, new PontoPx(192, 1032), novaEsquerda, "posição passa a ser do principal");
    }

    // Fase 5: o monitor mais próximo é medido a partir do pixel dos pés, a mesma convenção de
    // Maquina.MonitorDaAncora. Com a barra oculta, a âncora no chão fica em Tela.Base, que numa
    // pilha já é o primeiro pixel do monitor de baixo.
    [Teste]
    public static void MonitorRemovidoComAAncoraNaBaseDaTelaUsaOPixelDosPes()
    {
        var pilha = new Topologia([
            Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1080), 96),
            Secundario(Display2, Ret(0, 1080, 1920, 2160), Ret(0, 1080, 1920, 2112), 96),
        ]);
        var noChaoDeUmMonitorQueSumiu = new PosicaoDoPersonagem(Display3, 0.5, 1, new PontoPx(960, 1080));
        AfirmarReacomodacao(pilha, noChaoDeUmMonitorQueSumiu, Display1, new PontoPx(960, 1080), "o pixel (960,1079) é do DISPLAY1; a âncora crua cairia no DISPLAY2");
        Afirmar.Igual(new PontoPx(960, 1079), Posicionador.PixelDosPes(new PontoPx(960, 1080)), "o pixel dos pés fica logo acima da âncora");
    }

    [Teste]
    public static void MonitorRemovidoComOutraEscalaRecalculaOTamanho()
    {
        // Escalas mistas: o personagem no monitor de 96 DPI, em (3520,1392), cai no
        // principal de 144 DPI (961 px de distância; o de 192 DPI fica a 3521 px).
        MonitorDoDesktop a96 = Afirmar.NaoNulo(EscalasMistas.PorChave(Display2));
        PosicaoDoPersonagem posicao = Posicionador.Descrever(Posicionador.NoMonitor(a96, 0.5, 1, Sprite));
        Afirmar.Igual(new PontoPx(3520, 1392), posicao.AncoraAbsoluta, "partida");

        (Posicionamento r, _) = AfirmarReacomodacao(SemMonitor(EscalasMistas, Display2), posicao, Display1, new PontoPx(1280, 1368), "cai no principal");
        Afirmar.Igual(new TamanhoPx(300, 300), r.Tamanho, "tamanho a 144 DPI");
        Afirmar.Igual(Ret(1130, 1068, 1430, 1368), r.Retangulo, "retângulo a 144 DPI");
    }

    [Teste]
    public static void MonitorQueVoltaNaoFazOPersonagemPularDeVolta()
    {
        Afirmar.Igual(new PontoPx(-1440, 1032), PosicaoNoSecundario.AncoraAbsoluta, "partida no secundário");

        // O secundário é desconectado: o personagem cai no principal, a um quarto da largura.
        Topologia semSecundario = SemMonitor(SecundarioAEsquerda, Display2);
        (_, PosicaoDoPersonagem noPrincipal) = AfirmarReacomodacao(semSecundario, PosicaoNoSecundario, Display1, new PontoPx(480, 1032), "desconectado");
        AfirmarPosicao(Display1, 0.25, 1.0, new PontoPx(480, 1032), noPrincipal, "posição passa a ser do principal");

        // O secundário volta: o personagem continua no principal, no mesmo lugar.
        (_, PosicaoDoPersonagem depois) = AfirmarReacomodacao(SecundarioAEsquerda, noPrincipal, Display1, new PontoPx(480, 1032), "reconectado");
        Afirmar.Igual(noPrincipal, depois, "posição não muda quando o monitor volta");
    }

    [Teste]
    public static void SecundarioNegativoRemovidoComOPersonagemNaBorda()
    {
        // Encostado em x = 0 no secundário (âncora em -100, fração 1820/1920), cai no
        // principal na mesma posição relativa: âncora em 1820, encostado na borda direita.
        PosicaoDoPersonagem naBorda = Posicionador.Descrever(Posicionador.NoMonitor(Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2)), 1, 1, Sprite));
        (Posicionamento r, _) = AfirmarReacomodacao(SemMonitor(SecundarioAEsquerda, Display2), naBorda, Display1, new PontoPx(1820, 1032), "cai no principal");
        Afirmar.Igual(Ret(1720, 832, 1920, 1032), r.Retangulo);
    }

    [Teste]
    public static void VaoEntreMonitoresEscolheOMonitorMaisProximo()
    {
        // No secundário separado por um vão, no meio do chão: âncora (2760,1076).
        MonitorDoDesktop separado = Afirmar.NaoNulo(VaoEntreMonitores.PorChave(Display2));
        PosicaoDoPersonagem noSeparado = Posicionador.Descrever(Posicionador.NoMonitor(separado, 0.5, 1, Sprite));
        Afirmar.Igual(new PontoPx(2760, 1076), noSeparado.AncoraAbsoluta, "partida");
        AfirmarReacomodacao(SemMonitor(VaoEntreMonitores, Display2), noSeparado, Display1, new PontoPx(960, 1032), "secundário removido");

        // Monitor que não existe mais e última âncora dentro do vão: vence o monitor de tela
        // mais próxima (x = 2020 fica a 100 px do secundário e a 101 px do principal).
        var noVao = new PosicaoDoPersonagem(@"\\.\DISPLAY9", 0.5, 1, new PontoPx(2020, 600));
        AfirmarReacomodacao(VaoEntreMonitores, noVao, Display2, new PontoPx(2760, 1076), "vão, mais perto do secundário");
        AfirmarReacomodacao(VaoEntreMonitores, noVao with { AncoraAbsoluta = new PontoPx(2019, 600) }, Display1, new PontoPx(960, 1032), "vão, mais perto do principal");
    }

    [Teste]
    public static void ReacomodarComArgumentoNuloLanca()
    {
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Reacomodar(null!, PosicaoInicial, Sprite), "topologia nula");
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Reacomodar(SecundarioAEsquerda, null!, Sprite), "posição nula");
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>
    /// Reacomoda e confere o que vale para qualquer resultado: monitor da nova topologia com
    /// a chave esperada, âncora esperada, tamanho pelo DPI do monitor, retângulo inteiro
    /// dentro da área útil e nova posição apontando para o resultado.
    /// </summary>
    private static (Posicionamento Resultado, PosicaoDoPersonagem NovaPosicao) AfirmarReacomodacao(
        Topologia nova, PosicaoDoPersonagem atual, string chave, PontoPx ancora, string caso)
    {
        (Posicionamento r, PosicaoDoPersonagem posicao) = Posicionador.Reacomodar(nova, atual, Sprite);
        Afirmar.Igual(chave, r.Monitor.Chave, $"{caso}: monitor");
        Afirmar.Verdadeiro(ReferenceEquals(nova.PorChave(chave), r.Monitor), $"{caso}: monitor não é o da nova topologia");
        Afirmar.Igual(ancora, r.Ancora, $"{caso}: âncora");
        Afirmar.Igual(Sprite.ParaPixels(r.Monitor.Dpi), r.Tamanho, $"{caso}: tamanho");
        Afirmar.Verdadeiro(r.Monitor.AreaUtil.Contem(r.Retangulo), $"{caso}: retângulo {r.Retangulo} fora da área útil {r.Monitor.AreaUtil}");
        Afirmar.Igual(chave, posicao.ChaveMonitor, $"{caso}: chave da nova posição");
        Afirmar.Igual(r.Ancora, posicao.AncoraAbsoluta, $"{caso}: âncora absoluta da nova posição");
        return (r, posicao);
    }

    /// <summary>Reacomoda no mesmo monitor e confere que as frações guardadas não mudaram.</summary>
    private static Posicionamento AfirmarMesmaPosicaoRelativa(Topologia nova, PosicaoDoPersonagem atual, PontoPx ancora, string caso)
    {
        (Posicionamento r, PosicaoDoPersonagem posicao) = AfirmarReacomodacao(nova, atual, atual.ChaveMonitor, ancora, caso);
        Afirmar.Igual(atual.FracaoX, posicao.FracaoX, $"{caso}: fração x guardada");
        Afirmar.Igual(atual.FracaoY, posicao.FracaoY, $"{caso}: fração y guardada");
        return r;
    }

    private static void AfirmarPosicao(string chave, double fracaoX, double fracaoY, PontoPx ancora, PosicaoDoPersonagem obtida, string caso)
    {
        Afirmar.Igual(chave, obtida.ChaveMonitor, $"{caso}: chave");
        Afirmar.Aproximado(fracaoX, obtida.FracaoX, 1e-12, $"{caso}: fração x");
        Afirmar.Aproximado(fracaoY, obtida.FracaoY, 1e-12, $"{caso}: fração y");
        Afirmar.Igual(ancora, obtida.AncoraAbsoluta, $"{caso}: âncora absoluta");
    }
}
