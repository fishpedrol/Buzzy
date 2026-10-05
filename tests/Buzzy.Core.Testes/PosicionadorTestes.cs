using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes;

/// <summary>
/// Retângulo do sprite, prender na área útil, posição inicial, posição por frações e
/// descrição relativa (Posicionador.cs). Reacomodar fica em <see cref="ReacomodarTestes"/>.
/// </summary>
internal static class PosicionadorTestes
{
    // Sprite par do placeholder (200x200 DIP) e um ímpar, para a coluna da âncora.
    private static readonly TamanhoDip SpritePar = new(200, 200);
    private static readonly TamanhoDip SpriteImpar = new(101, 151);

    // Área útil do principal da máquina do usuário. Com o sprite de 200x200 px, a âncora
    // válida vai de x = 100 a 1820 e de y = 200 a 1032.
    private static readonly RetanguloPx Util = Ret(0, 0, 1920, 1032);
    private static readonly TamanhoPx Sprite200 = new(200, 200);

    // ---------------------------------------------------------------- retângulo do sprite

    [Teste]
    public static void RetanguloDoSpriteComLarguraPar()
    {
        // 200x100 com a âncora em (1000,500): 100 colunas à esquerda, 100 à direita
        // contando a da âncora, e a base (exclusiva) na linha da âncora.
        RetanguloPx r = Posicionador.RetanguloDoSprite(new PontoPx(1000, 500), new TamanhoPx(200, 100));
        Afirmar.Igual(Ret(900, 400, 1100, 500), r);
        Afirmar.Igual(1000, r.Centro.X, "âncora na coluna do centro");
    }

    [Teste]
    public static void RetanguloDoSpriteComLarguraImpar()
    {
        // 101 colunas: 50 de cada lado da coluna da âncora.
        Afirmar.Igual(Ret(950, 349, 1051, 500), Posicionador.RetanguloDoSprite(new PontoPx(1000, 500), new TamanhoPx(101, 151)));
        Afirmar.Igual(Ret(-1050, 881, -949, 1032), Posicionador.RetanguloDoSprite(new PontoPx(-1000, 1032), new TamanhoPx(101, 151)), "em x negativo");
        Afirmar.Igual(Ret(7, -1, 8, 0), Posicionador.RetanguloDoSprite(new PontoPx(7, 0), new TamanhoPx(1, 1)), "um pixel");
    }

    // ---------------------------------------------------------------- prender na área útil

    [Teste]
    public static void PrenderNaAreaUtilNaoMexeEmAncoraValida()
    {
        Afirmar.Igual(new PontoPx(960, 700), Posicionador.PrenderNaAreaUtil(new PontoPx(960, 700), Sprite200, Util), "no meio");
        Afirmar.Igual(new PontoPx(100, 200), Posicionador.PrenderNaAreaUtil(new PontoPx(100, 200), Sprite200, Util), "limite superior esquerdo");
        Afirmar.Igual(new PontoPx(1820, 1032), Posicionador.PrenderNaAreaUtil(new PontoPx(1820, 1032), Sprite200, Util), "limite inferior direito");
    }

    [Teste]
    public static void PrenderNaAreaUtilEmCadaBorda()
    {
        Afirmar.Igual(new PontoPx(100, 700), Posicionador.PrenderNaAreaUtil(new PontoPx(50, 700), Sprite200, Util), "esquerda");
        Afirmar.Igual(new PontoPx(100, 700), Posicionador.PrenderNaAreaUtil(new PontoPx(-5000, 700), Sprite200, Util), "muito à esquerda");
        Afirmar.Igual(new PontoPx(1820, 700), Posicionador.PrenderNaAreaUtil(new PontoPx(1900, 700), Sprite200, Util), "direita");
        Afirmar.Igual(new PontoPx(960, 200), Posicionador.PrenderNaAreaUtil(new PontoPx(960, 100), Sprite200, Util), "topo");
        Afirmar.Igual(new PontoPx(960, 1032), Posicionador.PrenderNaAreaUtil(new PontoPx(960, 2000), Sprite200, Util), "abaixo do chão");
    }

    [Teste]
    public static void PrenderNaAreaUtilNosCantos()
    {
        Afirmar.Igual(new PontoPx(100, 200), Posicionador.PrenderNaAreaUtil(new PontoPx(-10, -10), Sprite200, Util), "superior esquerdo");
        Afirmar.Igual(new PontoPx(1820, 200), Posicionador.PrenderNaAreaUtil(new PontoPx(5000, -10), Sprite200, Util), "superior direito");
        Afirmar.Igual(new PontoPx(100, 1032), Posicionador.PrenderNaAreaUtil(new PontoPx(-10, 5000), Sprite200, Util), "inferior esquerdo");
        Afirmar.Igual(new PontoPx(1820, 1032), Posicionador.PrenderNaAreaUtil(new PontoPx(5000, 5000), Sprite200, Util), "inferior direito");
    }

    [Teste]
    public static void PrenderNaAreaUtilComLarguraImparEncostaNasBordas()
    {
        // 101x151: âncora válida de x = 50 a 1869 e de y = 151 a 1032.
        var tamanho = new TamanhoPx(101, 151);
        PontoPx inferiorDireito = Posicionador.PrenderNaAreaUtil(new PontoPx(5000, 5000), tamanho, Util);
        Afirmar.Igual(new PontoPx(1869, 1032), inferiorDireito);
        Afirmar.Igual(Ret(1819, 881, 1920, 1032), Posicionador.RetanguloDoSprite(inferiorDireito, tamanho), "encostado na direita e no chão");

        PontoPx superiorEsquerdo = Posicionador.PrenderNaAreaUtil(new PontoPx(-5, -5), tamanho, Util);
        Afirmar.Igual(new PontoPx(50, 151), superiorEsquerdo);
        Afirmar.Igual(Ret(0, 0, 101, 151), Posicionador.RetanguloDoSprite(superiorEsquerdo, tamanho), "encostado na esquerda e no topo");
    }

    [Teste]
    public static void PrenderNaAreaUtilComCoordenadasNegativas()
    {
        // Área útil do secundário da máquina do usuário: (-1920,0)-(0,1032).
        RetanguloPx util = Ret(-1920, 0, 0, 1032);
        PontoPx direita = Posicionador.PrenderNaAreaUtil(new PontoPx(0, 1032), Sprite200, util);
        Afirmar.Igual(new PontoPx(-100, 1032), direita, "borda x = 0");
        Afirmar.Igual(Ret(-200, 832, 0, 1032), Posicionador.RetanguloDoSprite(direita, Sprite200), "encostado na borda x = 0");
        Afirmar.Igual(new PontoPx(-1820, 500), Posicionador.PrenderNaAreaUtil(new PontoPx(-5000, 500), Sprite200, util), "borda esquerda");
    }

    [Teste]
    public static void SpriteMaisLargoQueAAreaUtilFicaCentralizado()
    {
        // Área de 100 px de largura, de x = 1000 a 1100, com centro em 1050.
        RetanguloPx estreita = Ret(1000, 0, 1100, 1032);
        var largo = new TamanhoPx(300, 200);
        Afirmar.Igual(new PontoPx(1050, 500), Posicionador.PrenderNaAreaUtil(new PontoPx(-99_999, 500), largo, estreita), "vindo da esquerda");
        Afirmar.Igual(new PontoPx(1050, 500), Posicionador.PrenderNaAreaUtil(new PontoPx(99_999, 500), largo, estreita), "vindo da direita");
        Afirmar.Igual(Ret(900, 300, 1200, 500), Posicionador.RetanguloDoSprite(new PontoPx(1050, 500), largo), "passa 100 px de cada lado");

        // Largura ímpar: sobra 201 px, 100 de um lado e 101 do outro.
        var largoImpar = new TamanhoPx(301, 200);
        PontoPx ancora = Posicionador.PrenderNaAreaUtil(new PontoPx(0, 500), largoImpar, estreita);
        Afirmar.Igual(new PontoPx(1050, 500), ancora, "ímpar");
        Afirmar.Igual(Ret(900, 300, 1201, 500), Posicionador.RetanguloDoSprite(ancora, largoImpar), "ímpar passa 100 e 101 px");
    }

    [Teste]
    public static void SpriteMaisAltoQueAAreaUtilFicaComOsPesNoChao()
    {
        // Área de 150 px de altura: os pés ficam no chão (y = 150) e a cabeça passa do topo.
        RetanguloPx baixa = Ret(0, 0, 1920, 150);
        Afirmar.Igual(new PontoPx(100, 150), Posicionador.PrenderNaAreaUtil(new PontoPx(50, -500), Sprite200, baixa), "vindo de cima");
        Afirmar.Igual(new PontoPx(960, 150), Posicionador.PrenderNaAreaUtil(new PontoPx(960, 99_999), Sprite200, baixa), "vindo de baixo");
        Afirmar.Igual(Ret(860, -50, 1060, 150), Posicionador.RetanguloDoSprite(new PontoPx(960, 150), Sprite200), "cabeça 50 px acima do topo");

        // Mais largo e mais alto ao mesmo tempo: centralizado e com os pés no chão.
        Afirmar.Igual(new PontoPx(50, 150), Posicionador.PrenderNaAreaUtil(new PontoPx(-7, -7), new TamanhoPx(300, 200), Ret(0, 0, 100, 150)));
    }

    [Teste]
    public static void PrenderNaAreaUtilComAreaVaziaOuTamanhoInvalidoLanca()
    {
        var ancora = new PontoPx(10, 10);
        Afirmar.Lanca<ArgumentException>(() => Posicionador.PrenderNaAreaUtil(ancora, Sprite200, Ret(0, 0, 0, 100)), "área vazia");
        Afirmar.Lanca<ArgumentException>(() => Posicionador.PrenderNaAreaUtil(ancora, new TamanhoPx(0, 200), Util), "largura zero");
        Afirmar.Lanca<ArgumentException>(() => Posicionador.PrenderNaAreaUtil(ancora, new TamanhoPx(200, 0), Util), "altura zero");
        Afirmar.Lanca<ArgumentException>(() => Posicionador.PrenderNaAreaUtil(ancora, new TamanhoPx(-1, 200), Util), "largura negativa");
    }

    // ---------------------------------------------------------------- posição inicial

    [Teste]
    public static void InicialEmCadaTopologiaDeExemplo()
    {
        // Tamanho físico de 200x200 DIP em cada DPI de principal usado nos exemplos.
        var fisicoPorDpi = new Dictionary<int, TamanhoPx> { [96] = new(200, 200), [120] = new(250, 250), [144] = new(300, 300) };

        foreach ((string nome, Topologia t) in Todas)
        {
            Posicionamento p = Posicionador.Inicial(t, SpritePar);
            RetanguloPx area = t.Principal.AreaUtil;
            Afirmar.Verdadeiro(ReferenceEquals(t.Principal, p.Monitor), $"{nome}: não ficou no monitor principal ({p.Monitor.Chave})");
            Afirmar.Igual(fisicoPorDpi[t.Principal.Dpi], p.Tamanho, $"{nome}: tamanho físico a {t.Principal.Dpi} DPI");
            Afirmar.Igual(area.Base, p.Ancora.Y, $"{nome}: pés no chão da área útil");
            Afirmar.Igual(area.Base, p.Retangulo.Base, $"{nome}: última linha do sprite na última linha da área útil");
            Afirmar.Verdadeiro(area.Contem(p.Retangulo), $"{nome}: retângulo {p.Retangulo} fora da área útil {area}");
            Afirmar.Igual(Posicionador.RetanguloDoSprite(p.Ancora, p.Tamanho), p.Retangulo, $"{nome}: retângulo coerente com a âncora");
            Afirmar.Verdadeiro(p.Ancora.X > area.Centro.X, $"{nome}: âncora {p.Ancora} não está no lado direito da área útil");
        }
    }

    [Teste]
    public static void InicialComValoresCalculadosAMao()
    {
        // x = esquerda + 0,85 · largura da área útil; y = base da área útil.
        AfirmarInicial(SecundarioAEsquerda, new PontoPx(1632, 1032), Ret(1532, 832, 1732, 1032), "máquina do usuário");
        AfirmarInicial(PrincipalADireita, new PontoPx(1632, 1032), Ret(1532, 832, 1732, 1032), "principal à direita");
        AfirmarInicial(EscalasMistas, new PontoPx(2176, 1368), Ret(2026, 1068, 2326, 1368), "principal a 144 DPI (300 px)");
        AfirmarInicial(TresMonitores, new PontoPx(2176, 1380), Ret(2051, 1130, 2301, 1380), "principal a 120 DPI (250 px)");
        AfirmarInicial(BarraAEsquerda, new PontoPx(1641, 1080), Ret(1541, 880, 1741, 1080), "barra à esquerda: 62 + 0,85 · 1858");
        AfirmarInicial(BarraNoTopo, new PontoPx(1632, 1080), Ret(1532, 880, 1732, 1080), "barra no topo");
        AfirmarInicial(BarraADireita, new PontoPx(1579, 1080), Ret(1479, 880, 1679, 1080), "barra à direita: 0,85 · 1858");
    }

    [Teste]
    public static void InicialComSpriteImpar()
    {
        Posicionamento p = Posicionador.Inicial(SecundarioAEsquerda, SpriteImpar);
        Afirmar.Igual(new PontoPx(1632, 1032), p.Ancora);
        Afirmar.Igual(Ret(1582, 881, 1683, 1032), p.Retangulo);
    }

    [Teste]
    public static void InicialComTopologiaNulaLanca()
    {
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Inicial(null!, SpritePar));
    }

    // ---------------------------------------------------------------- posição por frações

    [Teste]
    public static void NoMonitorComFracoesNosExtremos()
    {
        MonitorDoDesktop principal = SecundarioAEsquerda.Principal;
        AfirmarNoMonitor(principal, 0, 0, new PontoPx(100, 200), Ret(0, 0, 200, 200), "0,0: canto superior esquerdo");
        AfirmarNoMonitor(principal, 1, 1, new PontoPx(1820, 1032), Ret(1720, 832, 1920, 1032), "1,1: canto inferior direito");
        AfirmarNoMonitor(principal, 0.5, 1, new PontoPx(960, 1032), Ret(860, 832, 1060, 1032), "meio do chão");
        AfirmarNoMonitor(principal, 0.25, 0.5, new PontoPx(480, 516), Ret(380, 316, 580, 516), "um quarto, meia altura");
    }

    [Teste]
    public static void NoMonitorPrendeFracoesForaDoIntervalo()
    {
        MonitorDoDesktop principal = SecundarioAEsquerda.Principal;
        AfirmarNoMonitor(principal, -3, 7, new PontoPx(100, 1032), Ret(0, 832, 200, 1032), "-3 vira 0 e 7 vira 1");
        AfirmarNoMonitor(principal, double.PositiveInfinity, double.NegativeInfinity, new PontoPx(1820, 200), Ret(1720, 0, 1920, 200), "infinitos");
    }

    [Teste]
    public static void NoMonitorTrataNaNComoMeio()
    {
        MonitorDoDesktop principal = SecundarioAEsquerda.Principal;
        AfirmarNoMonitor(principal, double.NaN, double.NaN, new PontoPx(960, 516), Ret(860, 316, 1060, 516), "NaN nos dois eixos");
        AfirmarNoMonitor(principal, double.NaN, 1, new PontoPx(960, 1032), Ret(860, 832, 1060, 1032), "NaN só em x");
    }

    [Teste]
    public static void NoMonitorEmCoordenadasNegativas()
    {
        MonitorDoDesktop secundario = Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2));
        AfirmarNoMonitor(secundario, 0.25, 1, new PontoPx(-1440, 1032), Ret(-1540, 832, -1340, 1032), "um quarto");
        AfirmarNoMonitor(secundario, 1, 1, new PontoPx(-100, 1032), Ret(-200, 832, 0, 1032), "encostado na borda x = 0");
        AfirmarNoMonitor(secundario, 0, 1, new PontoPx(-1820, 1032), Ret(-1920, 832, -1720, 1032), "encostado na borda esquerda");
    }

    [Teste]
    public static void NoMonitorUsaODpiDoMonitor()
    {
        MonitorDoDesktop a144 = EscalasMistas.Principal;
        Posicionamento p144 = Posicionador.NoMonitor(a144, 0.5, 1, SpritePar);
        Afirmar.Igual(new TamanhoPx(300, 300), p144.Tamanho, "144 DPI");
        Afirmar.Igual(Ret(1130, 1068, 1430, 1368), p144.Retangulo, "144 DPI");

        MonitorDoDesktop a192 = Afirmar.NaoNulo(EscalasMistas.PorChave(Display3));
        Posicionamento p192 = Posicionador.NoMonitor(a192, 0, 1, SpritePar);
        Afirmar.Igual(new TamanhoPx(400, 400), p192.Tamanho, "192 DPI");
        Afirmar.Igual(Ret(-3840, 944, -3440, 1344), p192.Retangulo, "192 DPI, encostado na esquerda");
    }

    [Teste]
    public static void NoMonitorComArgumentoInvalidoLanca()
    {
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.NoMonitor(null!, 0.5, 1, SpritePar), "monitor nulo");
        Afirmar.Lanca<ArgumentException>(() => Posicionador.NoMonitor(UmMonitor.Principal, 0.5, 1, new TamanhoDip(0, 200)), "tamanho zero");
    }

    // ---------------------------------------------------------------- descrever

    [Teste]
    public static void DescreverDaAPosicaoRelativaNaAreaUtil()
    {
        PosicaoDoPersonagem inicial = Posicionador.Descrever(Posicionador.Inicial(SecundarioAEsquerda, SpritePar));
        Afirmar.Igual(Display1, inicial.ChaveMonitor, "chave");
        Afirmar.Aproximado(0.85, inicial.FracaoX, 1e-12, "1632 / 1920");
        Afirmar.Aproximado(1.0, inicial.FracaoY, 1e-12, "1032 / 1032");
        Afirmar.Igual(new PontoPx(1632, 1032), inicial.AncoraAbsoluta, "âncora absoluta");

        MonitorDoDesktop secundario = Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2));
        PosicaoDoPersonagem negativa = Posicionador.Descrever(Posicionador.NoMonitor(secundario, 0.25, 1, SpritePar));
        Afirmar.Igual(Display2, negativa.ChaveMonitor, "chave do secundário");
        Afirmar.Aproximado(0.25, negativa.FracaoX, 1e-12, "(-1440 + 1920) / 1920");
        Afirmar.Igual(new PontoPx(-1440, 1032), negativa.AncoraAbsoluta, "âncora absoluta negativa");
    }

    [Teste]
    public static void DescreverSeguidoDeNoMonitorReproduzOPosicionamento()
    {
        double[] fracoesX = [0, 0.1, 0.25, 0.5, 0.85, 0.999, 1];
        double[] fracoesY = [0, 0.5, 1];
        // O terceiro tamanho não cabe em nenhum monitor dos exemplos.
        TamanhoDip[] tamanhos = [SpritePar, SpriteImpar, new(3001, 2001)];

        foreach ((string nome, Topologia t) in Todas)
            foreach (MonitorDoDesktop m in t.Monitores)
                foreach (TamanhoDip tamanho in tamanhos)
                    foreach (double fx in fracoesX)
                        foreach (double fy in fracoesY)
                        {
                            string caso = $"{nome}, {m.Chave}, {tamanho}, frações ({fx}; {fy})";
                            Posicionamento p = Posicionador.NoMonitor(m, fx, fy, tamanho);
                            PosicaoDoPersonagem d = Posicionador.Descrever(p);
                            Afirmar.Igual(m.Chave, d.ChaveMonitor, caso);
                            Afirmar.Igual(p.Ancora, d.AncoraAbsoluta, caso);
                            Afirmar.Verdadeiro(d.FracaoX is >= 0 and <= 1 && d.FracaoY is >= 0 and <= 1, $"{caso}: frações descritas fora de [0, 1]: {d}");
                            Afirmar.Igual(p, Posicionador.NoMonitor(m, d.FracaoX, d.FracaoY, tamanho), caso);
                        }
    }

    // Fase 5 (ARCHITECTURE.md 2.8): a posição guarda a tela do monitor da época, não a área útil.
    [Teste]
    public static void DescreverGuardaATelaDoMonitor()
    {
        MonitorDoDesktop secundario = Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2));
        PosicaoDoPersonagem descrita = Posicionador.Descrever(Posicionador.NoMonitor(secundario, 0.25, 1, SpritePar));
        Afirmar.Igual(Ret(-1920, 0, 0, 1080), descrita.TelaDoMonitor, "tela do secundário (a área útil vai até 1032)");

        var feitaAMao = new PosicaoDoPersonagem(Display1, 0.5, 1, new PontoPx(960, 1032));
        Afirmar.Nulo(feitaAMao.TelaDoMonitor, "posição construída sem a tela: desconhecida");
    }

    [Teste]
    public static void DescreverComNuloLanca()
    {
        Afirmar.Lanca<ArgumentNullException>(() => Posicionador.Descrever(null!));
    }

    // ---------------------------------------------------------------- auxiliares

    private static void AfirmarInicial(Topologia t, PontoPx ancora, RetanguloPx retangulo, string caso)
    {
        Posicionamento p = Posicionador.Inicial(t, SpritePar);
        Afirmar.Igual(ancora, p.Ancora, caso);
        Afirmar.Igual(retangulo, p.Retangulo, caso);
    }

    private static void AfirmarNoMonitor(MonitorDoDesktop m, double fx, double fy, PontoPx ancora, RetanguloPx retangulo, string caso)
    {
        Posicionamento p = Posicionador.NoMonitor(m, fx, fy, SpritePar);
        Afirmar.Verdadeiro(ReferenceEquals(m, p.Monitor), $"{caso}: monitor");
        Afirmar.Igual(ancora, p.Ancora, caso);
        Afirmar.Igual(retangulo, p.Retangulo, caso);
        Afirmar.Igual(SpritePar.ParaPixels(m.Dpi), p.Tamanho, caso);
    }
}
