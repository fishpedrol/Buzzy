using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;
using static Buzzy.Core.Testes.TopologiasDeExemplo;

namespace Buzzy.Core.Testes;

/// <summary>
/// Posição restaurada na partida (Fase 5, ARCHITECTURE.md 2.8): a posição gravada guarda a tela do
/// monitor da época, e a carga a restaura pela chave, pelo retângulo desse monitor ou no principal,
/// sempre com as frações salvas. Sprite de 128x128 DIP, o do aplicativo; as âncoras esperadas são
/// calculadas à mão a partir das topologias de exemplo.
/// </summary>
internal static class RestaurarTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    /// <summary>Âncora absoluta salva que nenhum monitor dos exemplos tem: a restauração nunca a usa.</summary>
    private static readonly PontoPx AncoraDeOutraSessao = new(30000, -30000);

    /// <summary>Chave de um monitor que nenhuma topologia de exemplo tem (salva por outra sessão).</summary>
    private const string ChaveAntiga = "mon:00000000000000aa";

    // ---------------------------------------------------------------- pela chave (S1 a S7)

    // Âncoras calculadas à mão: x = área.Esquerda + fx·área.Largura e y = área.Topo + fy·área.Altura,
    // presas para o sprite caber (128 px a 96 DPI, 256 px a 192 DPI).
    [Teste]
    public static void PelaChave_S1aS7_AncorasCalculadasAMao()
    {
        (string Caso, Topologia Topologia, string Chave, double Fx, PontoPx Ancora, int Lado)[] casos =
        [
            ("S1 LadoALado, DISPLAY2", LadoALado, Display2, 0.5, new PontoPx(1920 + 960, 1032), 128),
            ("S2 SecundarioAEsquerda, DISPLAY2", SecundarioAEsquerda, Display2, 0.25, new PontoPx(-1920 + 480, 1032), 128),
            ("S3 EmpilhadoSecundarioAcima, DISPLAY2 (y negativo)", EmpilhadoSecundarioAcima, Display2, 0.5, new PontoPx(-320 + 1280, -48), 128),
            ("S4 DegrauDesalinhado, DISPLAY3", DegrauDesalinhado, Display3, 0.5, new PontoPx(3840 + 960, 1832), 128),
            ("S5 EscalasMistas, DISPLAY3 a 192 DPI", EscalasMistas, Display3, 0.5, new PontoPx(-3840 + 1920, 1344), 256),
            ("S6 Retrato, DISPLAY2", TopologiasDeExemplo.Retrato, Display2, 0.5, new PontoPx(1920 + 540, 1452), 128),
            ("S7 PrincipalADireita, DISPLAY2", PrincipalADireita, Display2, 0.5, new PontoPx(-2560 + 1280, 1032), 128),
        ];
        foreach ((string caso, Topologia t, string chave, double fx, PontoPx ancora, int lado) in casos)
            AfirmarRestauradaPelaChave(t, new PosicaoDoPersonagem(chave, fx, 1, AncoraDeOutraSessao), ancora, lado, caso);
    }

    // Entre as sessões mudou o principal, a escala, a orientação ou a barra de tarefas: a chave ainda
    // existe, e vale a posição relativa salva, nunca a âncora absoluta da outra sessão.
    [Teste]
    public static void MesmaChaveComOutraGeometria_VaiPelaFracaoNaoPelaAncora()
    {
        // Troca de principal: o DISPLAY2 vira principal em (0,0), e o DISPLAY1 passa a (-1920,0)-(0,1080).
        var principalTrocado = new Topologia([
            Secundario(Display1, Ret(-1920, 0, 0, 1080), Ret(-1920, 0, 0, 1032), 96),
            Principal(Display2, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        ]);
        PosicaoDoPersonagem aOitentaECincoPorCento = Posicionador.Descrever(Posicionador.Inicial(LadoALado, Sprite));
        Afirmar.Igual(new PontoPx(1632, 1032), aOitentaECincoPorCento.AncoraAbsoluta, "salva a 85% do DISPLAY1");
        AfirmarRestauradaPelaChave(principalTrocado, aOitentaECincoPorCento, new PontoPx(-1920 + 1632, 1032), 128, "troca de principal");

        // S5: salva no DISPLAY3 de EscalasMistas a 192 DPI; reaberta com ele a 144 DPI e a barra de 72 px.
        PosicaoDoPersonagem a192 = DescreverNo(EscalasMistas, Display3, 0.5, 1);
        Afirmar.Igual(new PontoPx(-1920, 1344), a192.AncoraAbsoluta, "salva a 192 DPI");
        Topologia a144 = ComMonitor(EscalasMistas, Display3, m => m with { Dpi = 144, AreaUtil = Ret(-3840, -720, 0, 1368) });
        AfirmarRestauradaPelaChave(a144, a192, new PontoPx(-1920, 1368), 192, "S5, a 144 DPI");

        // S6: salva no monitor em retrato; reaberta com ele girado para paisagem, ao lado do principal.
        PosicaoDoPersonagem emRetrato = DescreverNo(TopologiasDeExemplo.Retrato, Display2, 0.5, 1);
        Afirmar.Igual(new PontoPx(2460, 1452), emRetrato.AncoraAbsoluta, "salva em retrato");
        Topologia girado = ComMonitor(TopologiasDeExemplo.Retrato, Display2, m => m with { Tela = Ret(1920, 0, 3840, 1080), AreaUtil = Ret(1920, 0, 3840, 1032) });
        AfirmarRestauradaPelaChave(girado, emRetrato, new PontoPx(1920 + 960, 1032), 128, "S6, em paisagem");

        // S11: a barra de tarefas foi para outra borda ou passou a se esconder sozinha.
        PosicaoDoPersonagem comABarraEmbaixo = Posicionador.Descrever(Posicionador.Inicial(UmMonitor, Sprite));
        AfirmarRestauradaPelaChave(BarraNoTopo, comABarraEmbaixo, new PontoPx(1632, 1080), 128, "S11, barra no topo");
        AfirmarRestauradaPelaChave(BarraADireita, comABarraEmbaixo, new PontoPx(1579, 1080), 128, "S11, barra à direita: 0,85 · 1858 = 1579,3");
        AfirmarRestauradaPelaChave(BarraAEsquerda, comABarraEmbaixo, new PontoPx(62 + 1579, 1080), 128, "S11, barra à esquerda");
        AfirmarRestauradaPelaChave(BarraOculta, comABarraEmbaixo, new PontoPx(1632, 1080), 128, "S11, barra oculta");
    }

    // ---------------------------------------------------------------- pelo retângulo (S9, chave nova)

    // A chave do monitor mudou entre as sessões (o nome GDI salvo por uma versão antiga, um driver
    // novo), mas o monitor com a mesma tela continua lá: vai para ele, e a posição passa a ter a chave nova.
    [Teste]
    public static void ChaveAusenteComAMesmaTela_PeloRetangulo()
    {
        const string ChaveNova1 = "mon:0000000000000001", ChaveNova2 = "mon:0000000000000002";
        var renomeada = new Topologia(SecundarioAEsquerda.Monitores.Select(m => m with { Chave = m.Chave == Display1 ? ChaveNova1 : ChaveNova2 }));
        var salva = new PosicaoDoPersonagem(Display2, 0.25, 1, AncoraDeOutraSessao) { TelaDoMonitor = Ret(-1920, 0, 0, 1080) };

        (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(renomeada, salva, Sprite);
        Afirmar.Igual(OrigemDaRestauracao.PeloRetangulo, origem, "origem");
        Afirmar.Igual(ChaveNova2, r.Monitor.Chave, "o monitor da tela salva");
        Afirmar.Igual(new PontoPx(-1440, 1032), r.Ancora, "25% da área útil dele, no chão");
        AfirmarPosicao(ChaveNova2, 0.25, 1, new PontoPx(-1440, 1032), Ret(-1920, 0, 0, 1080), nova, "a posição passa a ter a chave nova");

        // A chave vem antes do retângulo: com a chave presente, a tela de outro monitor não importa.
        var comAsDuas = new PosicaoDoPersonagem(Display1, 0.25, 1, AncoraDeOutraSessao) { TelaDoMonitor = Ret(-1920, 0, 0, 1080) };
        (Posicionamento pelaChave, _, OrigemDaRestauracao origemPelaChave) = Posicionador.Restaurar(SecundarioAEsquerda, comAsDuas, Sprite);
        Afirmar.Igual(OrigemDaRestauracao.PelaChave, origemPelaChave, "chave presente");
        Afirmar.Igual(new PontoPx(480, 1032), pelaChave.Ancora, "no DISPLAY1, o da chave");
    }

    // ---------------------------------------------------------------- no principal (S9, monitor ausente)

    // Salvo no secundário da máquina do usuário, reaberto só com o monitor do notebook: nem a chave
    // nem a tela existem, e ele aparece no principal com as mesmas frações.
    [Teste]
    public static void MonitorSalvoAusente_NoPrincipalComAsMesmasFracoes()
    {
        PosicaoDoPersonagem salva = Posicionador.Descrever(Posicionador.NoMonitor(Afirmar.NaoNulo(SecundarioAEsquerda.PorChave(Display2)), 0.25, 1, Sprite));
        Afirmar.Igual(Ret(-1920, 0, 0, 1080), salva.TelaDoMonitor, "gravada com a tela do secundário");

        (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(UmMonitor, salva, Sprite);
        Afirmar.Igual(OrigemDaRestauracao.NoPrincipal, origem, "origem");
        Afirmar.Igual(Display1, r.Monitor.Chave, "o principal");
        Afirmar.Igual(new PontoPx(480, 1032), r.Ancora, "25% da área útil do principal");
        AfirmarPosicao(Display1, 0.25, 1, new PontoPx(480, 1032), Ret(0, 0, 1920, 1080), nova, "a posição passa a ser do principal");
    }

    // S7: o principal não é o mais à esquerda nem o primeiro da lista, e a âncora salva cai no outro
    // monitor; sem chave nem tela, vale o principal, nunca o mais próximo da âncora salva.
    [Teste]
    public static void SemChaveNemTela_VaiAoPrincipalMesmoComAAncoraSalvaEmOutroMonitor()
    {
        var salva = new PosicaoDoPersonagem(ChaveAntiga, 0.1, 1, new PontoPx(-1280, 1032));
        Afirmar.Igual(Display2, PrincipalADireita.MonitorMaisProximo(salva.AncoraAbsoluta).Chave, "a âncora salva fica no DISPLAY2");

        (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(PrincipalADireita, salva, Sprite);
        Afirmar.Igual(OrigemDaRestauracao.NoPrincipal, origem, "origem");
        Afirmar.Igual(new PontoPx(192, 1032), r.Ancora, "10% de 1920 no DISPLAY1");
        AfirmarPosicao(Display1, 0.1, 1, new PontoPx(192, 1032), Ret(0, 0, 1920, 1080), nova, "posição no principal");

        // Uma tela salva que nenhum monitor tem também não casa com nada.
        (Posicionamento semTela, _, OrigemDaRestauracao origemSemTela) = Posicionador.Restaurar(PrincipalADireita, salva with { TelaDoMonitor = Ret(0, 0, 2560, 1440) }, Sprite);
        Afirmar.Igual(OrigemDaRestauracao.NoPrincipal, origemSemTela, "tela que não existe");
        Afirmar.Igual(new PontoPx(192, 1032), semTela.Ancora, "também no principal");
    }

    // ---------------------------------------------------------------- frações salvas

    // Um arquivo editado à mão pode trazer frações inválidas: NaN vira 0,5 e o resto é preso em
    // [0, 1], no lugar e na posição guardada, nos três passos.
    [Teste]
    public static void FracoesInvalidas_SaoSaneadasNosTresPassos()
    {
        (string Passo, OrigemDaRestauracao Origem, PosicaoDoPersonagem Salva)[] passos =
        [
            ("pela chave", OrigemDaRestauracao.PelaChave, new PosicaoDoPersonagem(Display1, 0, 0, AncoraDeOutraSessao)),
            ("pelo retângulo", OrigemDaRestauracao.PeloRetangulo, new PosicaoDoPersonagem(ChaveAntiga, 0, 0, AncoraDeOutraSessao) { TelaDoMonitor = Ret(0, 0, 1920, 1080) }),
            ("no principal", OrigemDaRestauracao.NoPrincipal, new PosicaoDoPersonagem(ChaveAntiga, 0, 0, AncoraDeOutraSessao)),
        ];
        // Área útil (0,0)-(1920,1032) e sprite de 128 px: âncora de x = 64 a 1856 e de y = 128 a 1032.
        (double Fx, double Fy, double FxSaneada, double FySaneada, PontoPx Ancora)[] fracoes =
        [
            (double.NaN, double.NaN, 0.5, 0.5, new PontoPx(960, 516)),
            (3, -1, 1, 0, new PontoPx(1856, 128)),
            (double.PositiveInfinity, double.NegativeInfinity, 1, 0, new PontoPx(1856, 128)),
            (-0.25, 1.5, 0, 1, new PontoPx(64, 1032)),
        ];
        foreach ((string passo, OrigemDaRestauracao origemEsperada, PosicaoDoPersonagem modelo) in passos)
        {
            foreach ((double fx, double fy, double fxSaneada, double fySaneada, PontoPx ancora) in fracoes)
            {
                string caso = $"{passo}, frações ({fx}; {fy})";
                (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(UmMonitor, modelo with { FracaoX = fx, FracaoY = fy }, Sprite);
                Afirmar.Igual(origemEsperada, origem, $"{caso}: origem");
                Afirmar.Igual(ancora, r.Ancora, $"{caso}: âncora");
                AfirmarPosicao(Display1, fxSaneada, fySaneada, ancora, Ret(0, 0, 1920, 1080), nova, caso);
            }
        }
    }

    // ---------------------------------------------------------------- idempotência

    // Restaurar de novo o resultado, na mesma topologia, não move o personagem nem muda a posição:
    // agora ela vem pela chave. A reacomodação da execução também não o move.
    [Teste]
    public static void RestaurarDeNovo_NaoMove_EmTodasAsTopologiasDeExemplo()
    {
        double[] fracoes = [0, 0.25, 0.5, 1];
        int conferidos = 0;
        foreach ((string nome, Topologia t) in Todas)
        {
            foreach (MonitorDoDesktop m in t.Monitores)
            {
                foreach (double fx in fracoes)
                {
                    foreach (double fy in fracoes)
                    {
                        (string Passo, PosicaoDoPersonagem Salva, MonitorDoDesktop Destino)[] passos =
                        [
                            ("pela chave", new PosicaoDoPersonagem(m.Chave, fx, fy, AncoraDeOutraSessao), m),
                            ("pelo retângulo", new PosicaoDoPersonagem(ChaveAntiga, fx, fy, AncoraDeOutraSessao) { TelaDoMonitor = m.Tela }, m),
                            ("no principal", new PosicaoDoPersonagem(ChaveAntiga, fx, fy, AncoraDeOutraSessao), t.Principal),
                        ];
                        foreach ((string passo, PosicaoDoPersonagem salva, MonitorDoDesktop destino) in passos)
                        {
                            string caso = $"{nome}, {m.Chave}, {passo}, frações ({fx}; {fy})";
                            (Posicionamento r, PosicaoDoPersonagem nova, _) = Posicionador.Restaurar(t, salva, Sprite);
                            Afirmar.Verdadeiro(ReferenceEquals(destino, r.Monitor), $"{caso}: foi para {r.Monitor.Chave}, esperado {destino.Chave}");
                            Afirmar.Igual(Sprite.ParaPixels(destino.Dpi), r.Tamanho, $"{caso}: tamanho aparente de 128 DIP");
                            Afirmar.Verdadeiro(destino.AreaUtil.Contem(r.Retangulo), $"{caso}: sprite {r.Retangulo} fora da área útil {destino.AreaUtil}");
                            Afirmar.Igual(destino.Chave, Maquina.MonitorDaAncora(t, r.Ancora).Chave, $"{caso}: a validação atribui a âncora ao mesmo monitor");

                            (Posicionamento deNovo, PosicaoDoPersonagem novaDeNovo, OrigemDaRestauracao origem) = Posicionador.Restaurar(t, nova, Sprite);
                            Afirmar.Igual(OrigemDaRestauracao.PelaChave, origem, $"{caso}: de novo, pela chave");
                            Afirmar.Igual(r, deNovo, $"{caso}: restaurar de novo não move");
                            Afirmar.Igual(nova, novaDeNovo, $"{caso}: restaurar de novo não muda a posição");
                            Afirmar.Igual(r, Posicionador.Reacomodar(t, nova, Sprite).Resultado, $"{caso}: reacomodar na mesma topologia não move");
                            conferidos++;
                        }
                    }
                }
            }
        }
        Afirmar.Verdadeiro(conferidos > 1000, $"{conferidos} restaurações conferidas");
    }

    // ---------------------------------------------------------------- a carga (máquina de estados)

    // Linha BOOTING | configurações e topologia carregadas | SETTLING: a posição salva é restaurada
    // pela cascata, e a regra diz de onde veio o monitor.
    [Teste]
    public static void Carga_RestauraPelaCascataEARegraDizOPasso()
    {
        const string ChaveNova2 = "mon:0000000000000002";
        var renomeada = new Topologia(SecundarioAEsquerda.Monitores.Select(m => m.Chave == Display2 ? m with { Chave = ChaveNova2 } : m));
        PosicaoDoPersonagem noSecundario = DescreverNo(SecundarioAEsquerda, Display2, 0.25, 1);
        (string Caso, Topologia Topologia, string Passo, string Chave, PontoPx Ancora, RetanguloPx Tela)[] casos =
        [
            ("mesma topologia", SecundarioAEsquerda, "pela chave", Display2, new PontoPx(-1440, 1032), Ret(-1920, 0, 0, 1080)),
            ("chave renomeada, mesma tela", renomeada, "pelo retângulo do monitor", ChaveNova2, new PontoPx(-1440, 1032), Ret(-1920, 0, 0, 1080)),
            ("S9: só o monitor do notebook", UmMonitor, "no monitor principal", Display1, new PontoPx(480, 1032), Ret(0, 0, 1920, 1080)),
        ];
        foreach ((string caso, Topologia t, string passo, string chave, PontoPx ancora, RetanguloPx tela) in casos)
        {
            Cenario c = new Cenario().Aplicar(new Loaded(t, noSecundario, PreferenciasDeTeste.Completas));
            c.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
            Afirmar.Igual($"BOOTING: configurações e topologia carregadas; posição salva restaurada {passo}", c.Transicoes[0].Regra, $"{caso}: regra");
            Afirmar.Igual(chave, c.Retrato.ChaveMonitor, $"{caso}: monitor");
            Afirmar.Igual(ancora, c.Ancora, $"{caso}: âncora");
            AfirmarPosicao(chave, 0.25, 1, ancora, tela, Afirmar.NaoNulo(c.Atual.Posicao, caso), $"{caso}: posição do núcleo");
        }

        // Escondido e mostrado antes da carga: a carga o mostra, e a regra também diz o passo.
        Cenario antes = new Cenario().Aplicar(new CmdHide(), new CmdShow(), new Loaded(UmMonitor, noSecundario, PreferenciasDeTeste.Completas));
        antes.Percorreu(Estado.Hidden, Estado.Settling, Estado.Idle);
        Afirmar.Igual("HIDDEN: pedido de mostrar anterior à carga; posição salva restaurada no monitor principal", antes.Transicoes[0].Regra, "pedido de mostrar anterior à carga");
        Afirmar.Igual(new PontoPx(480, 1032), antes.Ancora, "no principal, a 25%");
    }

    // S1 a S7 pela máquina, com a configuração do aplicativo: salvo no chão de qualquer monitor, na
    // mesma topologia, volta ao mesmo lugar, com o mesmo tamanho aparente (128 DIP no DPI dele).
    [Teste]
    public static void Carga_S1aS7_VoltaAoMesmoLugarEmCadaMonitor()
    {
        (string Id, Topologia Topologia)[] topologias =
        [
            ("S1", LadoALado), ("S2", SecundarioAEsquerda), ("S3", EmpilhadoSecundarioAcima), ("S4", DegrauDesalinhado),
            ("S5", EscalasMistas), ("S6", TopologiasDeExemplo.Retrato), ("S7", PrincipalADireita),
        ];
        ConfiguracaoDoNucleo doAplicativo = ConfiguracaoDoNucleo.DoAplicativo(Sprite);
        foreach ((string id, Topologia t) in topologias)
        {
            foreach (MonitorDoDesktop m in t.Monitores)
            {
                foreach (double fx in new[] { 0, 0.25, 0.5, 0.85, 1 })
                {
                    string caso = $"{id}, {m.Chave}, fração {fx}";
                    PosicaoDoPersonagem salva = DescreverNo(t, m.Chave, fx, 1);
                    Cenario c = new Cenario(doAplicativo).Aplicar(new Loaded(t, salva, PreferenciasDeTeste.Completas));
                    c.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
                    Afirmar.Igual("BOOTING: configurações e topologia carregadas; posição salva restaurada pela chave", c.Transicoes[0].Regra, $"{caso}: regra");
                    Afirmar.Igual(m.Chave, c.Retrato.ChaveMonitor, $"{caso}: monitor");
                    Afirmar.Igual(salva.AncoraAbsoluta, c.Ancora, $"{caso}: mesmo lugar");
                    Afirmar.Igual(Sprite.ParaPixels(m.Dpi), c.Retrato.Tamanho, $"{caso}: tamanho aparente");
                    AfirmarPosicao(m.Chave, salva.FracaoX, 1, salva.AncoraAbsoluta, m.Tela, Afirmar.NaoNulo(c.Atual.Posicao, caso), $"{caso}: posição do núcleo");
                }
            }
        }
    }

    // Sem posição salva, o texto da regra não muda (as reproduções gravadas 01 a 05 dependem dele).
    [Teste]
    public static void Carga_SemPosicaoSalva_RegraInalterada()
    {
        Cenario c = new Cenario().Aplicar(new Loaded(UmMonitor, null, PreferenciasDeTeste.Completas));
        c.Percorreu(Estado.Booting, Estado.Settling, Estado.Idle);
        Afirmar.Igual("BOOTING: configurações e topologia carregadas", c.Transicoes[0].Regra, "regra");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "posição inicial");
    }

    // A posição é gravada onde ele estava, inclusive no ar, na parede ou no cipó. Na partida, com a
    // física da Fase 4, SETTLING decide como na mão do usuário (DEC-024): perto da borda de cima
    // agarra o cipó, junto a uma lateral gruda na parede, e no meio do ar cai até o chão. Sem a marca de
    // "preso pelo usuário" na carga (um arquivo v1 ou v2, ou uma gravação longe da parede e do cipó), agarrado na
    // partida, ele volta à agenda comum; com ela (esquema v3), continua preso (PosturaSalvaTestes).
    [Teste]
    public static void Carga_SalvaNoAr_AgarraPertoDoTetoOuDeUmaLateralESenaoCai()
    {
        // UmMonitor, sprite de 128 px: âncora de x = 64 a 1856; teto em y = 128 e chão em y = 1032.
        (string Caso, double Fx, double Fy, Estado Estado, string Regra, PontoPx Ancora, Direcao Direcao)[] casos =
        [
            ("perto da borda de cima (y = 186)", 0.5, 0.18, Estado.Hanging, "SETTLING: solto perto da borda de cima, agarra o cipó", new PontoPx(960, 128), Direcao.Direita),
            ("junto à lateral direita (x = 1824)", 0.95, 0.5, Estado.Climbing, "SETTLING: solto junto a uma lateral, fica grudado na parede", new PontoPx(1856, 516), Direcao.Direita),
            ("junto à lateral esquerda (x = 96)", 0.05, 0.5, Estado.Climbing, "SETTLING: solto junto a uma lateral, fica grudado na parede", new PontoPx(64, 516), Direcao.Esquerda),
            ("no meio do ar (y = 332)", 0.5, 0.321705, Estado.Falling, "SETTLING sem apoio", new PontoPx(960, 332), Direcao.Direita),
            ("junto à lateral, mas a 10 px do chão", 0.95, 0.99, Estado.Falling, "SETTLING sem apoio", new PontoPx(1824, 1022), Direcao.Direita),
        ];
        foreach ((string caso, double fx, double fy, Estado estado, string regra, PontoPx ancora, Direcao direcao) in casos)
        {
            var salva = new PosicaoDoPersonagem(Display1, fx, fy, AncoraDeOutraSessao);
            Cenario c = new Cenario(MovimentoTestes.Fase4()).Aplicar(new Loaded(UmMonitor, salva, PreferenciasDeTeste.Completas));
            c.Percorreu(Estado.Booting, Estado.Settling, estado);
            Afirmar.Igual("BOOTING: configurações e topologia carregadas; posição salva restaurada pela chave", c.Transicoes[0].Regra, $"{caso}: regra da carga");
            Afirmar.Igual(regra, c.Transicoes[1].Regra, $"{caso}: regra de SETTLING");
            Afirmar.Igual(ancora, c.Ancora, $"{caso}: âncora");
            Afirmar.Igual(direcao, c.Atual.Direcao, $"{caso}: direção");
            Afirmar.Falso(c.Atual.PresoPeloUsuario, $"{caso}: não fica preso pelo usuário");
            MovimentoTestes.ConferirApoio(c.Atual, caso);
            if (estado == Estado.Falling)
            {
                Afirmar.Verdadeiro(c.Atual.RelogioAtivo, $"{caso}: a queda é animada");
                PassosAteIdle(c, caso);
                Afirmar.Igual(new PontoPx(ancora.X, 1032), c.Ancora, $"{caso}: pousou no chão, na mesma coluna");
            }
            else
            {
                Afirmar.Verdadeiro(c.Atual.Movimento.Agarrado, $"{caso}: parado, agarrado");
                Afirmar.Falso(c.Atual.RelogioAtivo, $"{caso}: agarrado, sem relógio");
                Afirmar.Verdadeiro(c.Atual.DecisaoAgendada, $"{caso}: a agenda comum volta a movê-lo");
            }
        }
    }

    // S9: aberto sem o monitor salvo, o personagem vai ao principal; quando o monitor volta, ele não
    // pula de volta, e a saída grava o principal, com a tela dele.
    [Teste]
    public static void Carga_MonitorSalvoAusente_NaoVoltaQuandoReconectaEGravaOPrincipal()
    {
        PosicaoDoPersonagem noSecundario = DescreverNo(SecundarioAEsquerda, Display2, 0.25, 1);
        Cenario c = new Cenario().Aplicar(new Loaded(UmMonitor, noSecundario, PreferenciasDeTeste.Completas));
        Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, "no principal, a 25%");

        c.Aplicar(new TopologyChanged(SecundarioAEsquerda)).Esta(Estado.Idle);
        Afirmar.Igual(Display1, c.Retrato.ChaveMonitor, "o secundário voltou, e ele continua no principal");
        Afirmar.Igual(new PontoPx(480, 1032), c.Ancora, "no mesmo lugar");

        c.Aplicar(new CmdExit()).Percorreu(Estado.Idle, Estado.Exiting);
        AfirmarPosicao(Display1, 0.25, 1, new PontoPx(480, 1032), Ret(0, 0, 1920, 1080), c.Efeito<GravarPosicao>().Posicao, "grava o principal");
    }

    // ---------------------------------------------------------------- reproduções gravadas (Gravacao)

    // A posição salva do Loaded leva a tela, quando conhecida (9 campos), para a reprodução restaurar
    // pelo retângulo como a partida. O efeito GravarPosicao continua com 5 campos: as referências
    // gravadas 01 a 05 não mudam.
    [Teste]
    public static void Reproducao_LoadedLevaATelaEGravarPosicaoContinuaComCincoCampos()
    {
        var comTela = new PosicaoDoPersonagem(Display2, 0.25, 1, new PontoPx(-1440, 1032)) { TelaDoMonitor = Ret(-1920, 0, 0, 1080) };
        string linha = Gravacao.Escrever(new Loaded(UmMonitor, comTela, PreferenciasDeTeste.Completas), _ => "UmMonitor");
        Afirmar.Igual(@"Loaded topologia=UmMonitor energia=Media telaCheia=sim posicao=\\.\DISPLAY2;0.25;1;-1440;1032;-1920;0;0;1080", linha, "com a tela, 9 campos");
        Loaded lido = (Loaded)Gravacao.Ler(linha, _ => UmMonitor, EstadoDoNucleo.Inicial(1)).Single();
        Afirmar.Igual(comTela, lido.PosicaoSalva, "ida e volta com a tela");

        var semTela = comTela with { TelaDoMonitor = null };
        Afirmar.Igual(@"Loaded topologia=UmMonitor energia=Media telaCheia=sim posicao=\\.\DISPLAY2;0.25;1;-1440;1032",
            Gravacao.Escrever(new Loaded(UmMonitor, semTela, PreferenciasDeTeste.Completas), _ => "UmMonitor"), "sem a tela, 5 campos");
        Afirmar.Igual(semTela, Gravacao.LerPosicao(@"\\.\DISPLAY2;0.25;1;-1440;1032"), "5 campos: tela desconhecida");

        // Uma tela vazia também é desconhecida: 5 campos, que a leitura aceita (9 campos com ela, a leitura recusaria).
        foreach (RetanguloPx vazia in new RetanguloPx[] { default, Ret(-1920, 0, -1920, 1080), Ret(0, 1080, 1920, 0) })
        {
            string escrita = Gravacao.Escrever(new Loaded(UmMonitor, comTela with { TelaDoMonitor = vazia }, PreferenciasDeTeste.Completas), _ => "UmMonitor");
            Afirmar.Igual(@"Loaded topologia=UmMonitor energia=Media telaCheia=sim posicao=\\.\DISPLAY2;0.25;1;-1440;1032", escrita, $"tela vazia {vazia}: 5 campos");
            Loaded relida = (Loaded)Gravacao.Ler(escrita, _ => UmMonitor, EstadoDoNucleo.Inicial(1)).Single();
            Afirmar.Igual(semTela, relida.PosicaoSalva, $"tela vazia {vazia}: volta com a tela desconhecida");
        }

        Afirmar.Igual(@"GravarPosicao posicao=\\.\DISPLAY2;0.25;1;-1440;1032", Gravacao.DescreverEfeito(new GravarPosicao(comTela)), "o efeito continua com 5 campos");

        // Outra quantidade de campos, ou uma tela vazia, é erro de formato.
        foreach (string invalida in new[] { "a;1;1;0", "a;1;1;0;0;1", "a;1;1;0;0;0;0;10", "a;1;1;0;0;0;0;10;10;5", "a;1;1;0;0;10;0;10;10" })
            Afirmar.Lanca<FormatException>(() => Gravacao.LerPosicao(invalida), invalida);
    }

    // ---------------------------------------------------------------- a tela da época

    [Teste]
    public static void GravarPosicao_TrazATelaDoMonitorDaEpoca()
    {
        // Solto no chão do DISPLAY2 de LadoALado: grava a tela dele.
        Cenario c = Cenario.Parado(topologia: LadoALado);
        var noDisplay2 = new PontoPx(2880, 1000);
        c.Aplicar(new Press(Cenario.PontoOpaco), new DragStart(), new DragMove(noDisplay2), new DragEnd(noDisplay2));
        PosicaoDoPersonagem solto = c.Efeito<GravarPosicao>().Posicao;
        Afirmar.Igual(Display2, solto.ChaveMonitor, "soltou no DISPLAY2");
        Afirmar.Igual(Ret(1920, 0, 3840, 1080), solto.TelaDoMonitor, "tela do DISPLAY2 ao soltar");

        // O DISPLAY2 passa a 2560x1440 e o personagem é escondido: grava a tela nova.
        Topologia maior = ComMonitor(LadoALado, Display2, m => m with { Tela = Ret(1920, 0, 4480, 1440), AreaUtil = Ret(1920, 0, 4480, 1392) });
        c.Aplicar(new TopologyChanged(maior), new CmdHide());
        PosicaoDoPersonagem escondido = c.Efeito<GravarPosicao>().Posicao;
        Afirmar.Igual(Display2, escondido.ChaveMonitor, "continua no DISPLAY2");
        Afirmar.Igual(Ret(1920, 0, 4480, 1440), escondido.TelaDoMonitor, "tela nova ao esconder");
    }

    // Com o botão pressionado, TOPOLOGY_CHANGED não move a janela nem interrompe o gesto, mas a posição acompanha a
    // topologia (passo P8, DEC-030): o monitor ficou mais largo (2560x1080), com o mesmo chão, e a posição passa a
    // ter a tela nova e a âncora da mesma fração na área útil nova. O CLICK valida a partir dela, porque o monitor do
    // personagem mudou. Esconder durante a reação grava a tela nova, e as frações gravadas descrevem o lugar validado.
    [Teste]
    public static void CliqueDepoisDeOMonitorMudarNoGesto_GravaATelaNova()
    {
        Cenario c = Cenario.Em(Estado.Pressed);
        Afirmar.Igual(Ret(0, 0, 1920, 1080), Afirmar.NaoNulo(c.Atual.Posicao).TelaDoMonitor, "tela de antes do gesto");

        Topologia maisLargo = ComMonitor(UmMonitor, Display1, m => m with { Tela = Ret(0, 0, 2560, 1080), AreaUtil = Ret(0, 0, 2560, 1032) });
        c.Aplicar(new TopologyChanged(maisLargo)).Esta(Estado.Pressed, "o gesto não é interrompido");
        Afirmar.Igual(Cenario.AncoraInicial, c.Ancora, "a janela não se move no gesto");
        AfirmarPosicao(Display1, 0.85, 1, new PontoPx(2176, 1032), Ret(0, 0, 2560, 1080), Afirmar.NaoNulo(c.Atual.Posicao), "no gesto, a posição acompanha: 0,85 · 2560");

        c.Aplicar(new Click()).Percorreu(Estado.Pressed, Estado.Reacting);
        Afirmar.Igual(new PontoPx(2176, 1032), c.Ancora, "validada no clique a partir da posição que acompanhou");
        Afirmar.Igual(Ret(0, 0, 2560, 1080), Afirmar.NaoNulo(c.Atual.Posicao).TelaDoMonitor, "com a tela nova");

        c.Aplicar(new CmdHide()).EstaEscondido(MotivoDoOcultamento.PorUsuario);
        AfirmarPosicao(Display1, 0.85, 1, new PontoPx(2176, 1032), Ret(0, 0, 2560, 1080), c.Efeito<GravarPosicao>().Posicao, "grava onde ele está, com a tela nova");
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>Posição descrita no monitor da chave, nas frações dadas, como a execução a grava.</summary>
    private static PosicaoDoPersonagem DescreverNo(Topologia t, string chave, double fx, double fy)
        => Posicionador.Descrever(Posicionador.NoMonitor(Afirmar.NaoNulo(t.PorChave(chave), chave), fx, fy, Sprite));

    /// <summary>
    /// Restaura e confere o passo "pela chave": o monitor da chave, a âncora esperada, o tamanho
    /// aparente de 128 DIP, o monitor que a validação atribui à âncora e a posição nova, com as frações
    /// salvas e a tela atual do monitor.
    /// </summary>
    private static void AfirmarRestauradaPelaChave(Topologia t, PosicaoDoPersonagem salva, PontoPx ancora, int lado, string caso)
    {
        (Posicionamento r, PosicaoDoPersonagem nova, OrigemDaRestauracao origem) = Posicionador.Restaurar(t, salva, Sprite);
        MonitorDoDesktop monitor = Afirmar.NaoNulo(t.PorChave(salva.ChaveMonitor), caso);
        Afirmar.Igual(OrigemDaRestauracao.PelaChave, origem, $"{caso}: origem");
        Afirmar.Verdadeiro(ReferenceEquals(monitor, r.Monitor), $"{caso}: monitor da chave");
        Afirmar.Igual(ancora, r.Ancora, $"{caso}: âncora");
        Afirmar.Igual(new TamanhoPx(lado, lado), r.Tamanho, $"{caso}: tamanho aparente de 128 DIP");
        Afirmar.Igual(salva.ChaveMonitor, Maquina.MonitorDaAncora(t, r.Ancora).Chave, $"{caso}: a validação atribui a âncora ao mesmo monitor");
        AfirmarPosicao(salva.ChaveMonitor, salva.FracaoX, salva.FracaoY, ancora, monitor.Tela, nova, caso);
    }

    /// <summary>Passos de relógio até IDLE (no máximo 1000), conferindo o apoio a cada passo.</summary>
    private static void PassosAteIdle(Cenario c, string caso)
    {
        for (int i = 0; i < 1000 && c.Atual.Estado != Estado.Idle; i++)
        {
            c.Aplicar(new Tick());
            MovimentoTestes.ConferirApoio(c.Atual, $"{caso}, passo {i + 1}");
        }
        c.Esta(Estado.Idle, $"{caso}: chegou a IDLE");
    }

    /// <summary>Confere a posição campo a campo: a igualdade do registro também compara a tela guardada.</summary>
    private static void AfirmarPosicao(string chave, double fx, double fy, PontoPx ancora, RetanguloPx? tela, PosicaoDoPersonagem obtida, string caso)
    {
        Afirmar.Igual(chave, obtida.ChaveMonitor, $"{caso}: chave da posição");
        Afirmar.Igual(fx, obtida.FracaoX, $"{caso}: fração x da posição");
        Afirmar.Igual(fy, obtida.FracaoY, $"{caso}: fração y da posição");
        Afirmar.Igual(ancora, obtida.AncoraAbsoluta, $"{caso}: âncora absoluta da posição");
        Afirmar.Igual(tela, obtida.TelaDoMonitor, $"{caso}: tela guardada na posição");
    }
}
