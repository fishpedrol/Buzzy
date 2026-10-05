using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Entrada;

/// <summary>
/// Reconhecedor de gestos da Fase 3 (TODO.md, "Testes automatizados": limiar por DPI, clique
/// duplo, perda de captura e botão direito), com as regras de ARCHITECTURE.md 2.7. Os números
/// seguem os padrões do Windows: limiar de arraste de 4 px de cada lado a 96 DPI (6 a 144 DPI,
/// 8 a 192 DPI), retângulo de clique duplo de 4 × 4 px e 500 ms.
/// </summary>
internal static class ArbitroDeGestosTestes
{
    private static readonly MetricasDeGesto Dpi96 = MetricasDeGesto.Padrao;
    private static readonly MetricasDeGesto Dpi144 = new(6, 6, 6, 6, 500);
    private static readonly MetricasDeGesto Dpi192 = new(8, 8, 8, 8, 500);

    private static PontoPx P(int x, int y) => new(x, y);

    private static PonteiroPressionado Descer(int x, int y, long ms = 0, MetricasDeGesto? m = null)
        => new(P(x, y), BotaoDoPonteiro.Esquerdo, ms, m ?? Dpi96);

    private static PonteiroMovido Mover(int x, int y, long ms = 0, bool pressionado = true) => new(P(x, y), pressionado, ms);

    private static PonteiroSolto Subir(int x, int y, long ms = 0) => new(P(x, y), BotaoDoPonteiro.Esquerdo, ms);

    /// <summary>Nome e dados de cada gesto, para comparar sequências.</summary>
    private static string Nome(Evento e) => e switch
    {
        Press p => $"Press{p.Cursor}",
        DragMove m => $"DragMove{m.Cursor}",
        DragEnd d => $"DragEnd{d.Cursor}",
        ContextMenu c => $"ContextMenu{c.Cursor}",
        _ => e.GetType().Name,
    };

    private static void Gera(Arbitragem a, bool capturar, params string[] esperados)
    {
        Afirmar.Sequencia(esperados, a.Gestos.Select(Nome), "gestos");
        Afirmar.Igual(capturar, a.Capturar, "captura depois do evento");
    }

    [Teste]
    public static void SoltarDentroDoLimiar_EhClique()
    {
        var a = new ArbitroDeGestos();
        Gera(a.Receber(Descer(100, 100)), true, "Press(100,100)");
        // 4 px de cada lado ainda é clique: só sai do retângulo quem anda mais que o limiar.
        Gera(a.Receber(Mover(104, 96)), true);
        Gera(a.Receber(Mover(96, 104)), true);
        Gera(a.Receber(Subir(104, 104)), false, "Click");
        Afirmar.Falso(a.EmGesto, "sem gesto depois do clique");
    }

    [Teste]
    public static void PassarDoLimiar_IniciaArrasteQueAcompanhaOCursorAteSoltar()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        Gera(a.Receber(Mover(105, 100)), true, "DragStart", "DragMove(105,100)");
        Afirmar.Verdadeiro(a.Arrastando, "arrastando");
        Gera(a.Receber(Mover(300, 40)), true, "DragMove(300,40)");
        // Voltar para perto do ponto de pressão não desfaz o arraste.
        Gera(a.Receber(Mover(101, 100)), true, "DragMove(101,100)");
        Gera(a.Receber(Subir(310, 45)), false, "DragEnd(310,45)");
    }

    [Teste]
    public static void LimiarVerticalSozinhoTambemIniciaOArraste()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        Gera(a.Receber(Mover(100, 95)), true, "DragStart", "DragMove(100,95)");
    }

    [Teste]
    public static void LimiarAcompanhaAsMetricasDoDpiDoMonitor()
    {
        foreach ((MetricasDeGesto m, int limiar) in new[] { (Dpi96, 4), (Dpi144, 6), (Dpi192, 8) })
        {
            var a = new ArbitroDeGestos();
            a.Receber(Descer(0, 0, 0, m));
            Gera(a.Receber(Mover(limiar, -limiar)), true);
            Gera(a.Receber(Mover(-limiar - 1, 0)), true, "DragStart", $"DragMove({-limiar - 1},0)");
        }
    }

    [Teste]
    public static void SoltarForaDoLimiarSemMovimento_EhArrasteAteOPontoSolto()
    {
        // Gesto muito rápido (P3, cenário B3): nenhum movimento chega entre pressionar e soltar.
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        Gera(a.Receber(Subir(400, 250)), false, "DragStart", "DragEnd(400,250)");
    }

    [Teste]
    public static void SegurarPorMuitoTempo_NaoTemLimiteNemParaCliqueNemParaArraste()
    {
        // Critério 7 da Fase 3 (ClickLock): nenhuma regra de tempo encerra ou reclassifica o gesto.
        var clique = new ArbitroDeGestos();
        clique.Receber(Descer(10, 10, ms: 0));
        Gera(clique.Receber(Mover(12, 12, ms: 60_000)), true);
        Gera(clique.Receber(Subir(12, 12, ms: 3_600_000)), false, "Click");

        var arraste = new ArbitroDeGestos();
        arraste.Receber(Descer(10, 10, ms: 0));
        Gera(arraste.Receber(Mover(50, 10, ms: 1_800)), true, "DragStart", "DragMove(50,10)");
        Gera(arraste.Receber(Mover(90, 10, ms: 7_200_000)), true, "DragMove(90,10)");
        Gera(arraste.Receber(Subir(95, 10, ms: 7_200_001)), false, "DragEnd(95,10)");
    }

    [Teste]
    public static void CliqueDuplo_SegundoPressionarDentroDoTempoEDoRetangulo()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 1000));
        Gera(a.Receber(Subir(100, 100, ms: 1080)), false, "Click");
        // O primeiro clique sai na hora; o segundo pressionar é um PRESS comum.
        Gera(a.Receber(Descer(101, 99, ms: 1499)), true, "Press(101,99)");
        Gera(a.Receber(Subir(101, 99, ms: 1600)), false, "DoubleClick");
    }

    [Teste]
    public static void CliqueDuplo_TempoContaEntreOsDoisPressionarEEEstrito()
    {
        // 500 ms exatos já passaram do tempo.
        var noLimite = new ArbitroDeGestos();
        noLimite.Receber(Descer(100, 100, ms: 0));
        noLimite.Receber(Subir(100, 100, ms: 50));
        noLimite.Receber(Descer(100, 100, ms: 500));
        Gera(noLimite.Receber(Subir(100, 100, ms: 520)), false, "Click");

        // Soltar o primeiro devagar não estende a janela: conta do primeiro PRESSIONAR.
        var devagar = new ArbitroDeGestos();
        devagar.Receber(Descer(100, 100, ms: 0));
        devagar.Receber(Subir(100, 100, ms: 450));
        devagar.Receber(Descer(100, 100, ms: 499));
        Gera(devagar.Receber(Subir(100, 100, ms: 900)), false, "DoubleClick");
    }

    [Teste]
    public static void CliqueDuplo_ForaDaMetadeDoRetanguloEhOutroClique()
    {
        // Retângulo de 4 px: aceita 1 px de distância em cada eixo, não 2.
        foreach (PontoPx segundo in new[] { P(102, 100), P(98, 100), P(100, 102), P(100, 98) })
        {
            var a = new ArbitroDeGestos();
            a.Receber(Descer(100, 100, ms: 0));
            a.Receber(Subir(100, 100, ms: 10));
            a.Receber(Descer(segundo.X, segundo.Y, ms: 100));
            Gera(a.Receber(Subir(segundo.X, segundo.Y, ms: 110)), false, "Click");
        }

        var perto = new ArbitroDeGestos();
        perto.Receber(Descer(100, 100, ms: 0));
        perto.Receber(Subir(100, 100, ms: 10));
        perto.Receber(Descer(99, 101, ms: 100));
        Gera(perto.Receber(Subir(99, 101, ms: 110)), false, "DoubleClick");
    }

    [Teste]
    public static void CliqueDuplo_UsaAsMetricasDoPrimeiroClique()
    {
        // Retângulo de 8 px a 192 DPI: 3 px de distância ainda completam o clique duplo.
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 0, Dpi192));
        a.Receber(Subir(100, 100, ms: 10));
        a.Receber(Descer(103, 97, ms: 100, Dpi96));
        Gera(a.Receber(Subir(103, 97, ms: 110)), false, "DoubleClick");
    }

    [Teste]
    public static void TerceiroCliqueRapidoComecaUmaSequenciaNova()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 0));
        Gera(a.Receber(Subir(100, 100, ms: 10)), false, "Click");
        a.Receber(Descer(100, 100, ms: 100));
        Gera(a.Receber(Subir(100, 100, ms: 110)), false, "DoubleClick");
        a.Receber(Descer(100, 100, ms: 200));
        Gera(a.Receber(Subir(100, 100, ms: 210)), false, "Click");
        a.Receber(Descer(100, 100, ms: 300));
        Gera(a.Receber(Subir(100, 100, ms: 310)), false, "DoubleClick");
    }

    [Teste]
    public static void SegundoPressionarQueViraArraste_NaoEhCliqueDuplo()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 0));
        a.Receber(Subir(100, 100, ms: 10));
        a.Receber(Descer(100, 100, ms: 100));
        Gera(a.Receber(Mover(140, 100, ms: 150)), true, "DragStart", "DragMove(140,100)");
        Gera(a.Receber(Subir(140, 100, ms: 200)), false, "DragEnd(140,100)");
        // Um arraste encerra a sequência: o clique seguinte é simples.
        a.Receber(Descer(140, 100, ms: 250));
        Gera(a.Receber(Subir(140, 100, ms: 260)), false, "Click");
    }

    [Teste]
    public static void CapturaPerdida_EncerraOGestoComDragCancel()
    {
        var arrastando = new ArbitroDeGestos();
        arrastando.Receber(Descer(100, 100));
        arrastando.Receber(Mover(150, 100));
        Gera(arrastando.Receber(new CapturaPerdida(5)), false, "DragCancel");
        // Depois da perda, movimentos e o soltar tardio não mexem em nada.
        Gera(arrastando.Receber(Mover(400, 400)), false);
        Gera(arrastando.Receber(Subir(400, 400)), false);

        var pressionado = new ArbitroDeGestos();
        pressionado.Receber(Descer(100, 100));
        Gera(pressionado.Receber(new CapturaPerdida(5)), false, "DragCancel");

        var livre = new ArbitroDeGestos();
        Gera(livre.Receber(new CapturaPerdida(5)), false);
    }

    [Teste]
    public static void CapturaPerdida_CortaASequenciaDeCliqueDuplo()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 0));
        a.Receber(Subir(100, 100, ms: 10));
        a.Receber(Descer(100, 100, ms: 100));
        Gera(a.Receber(new CapturaPerdida(120)), false, "DragCancel");
        a.Receber(Descer(100, 100, ms: 200));
        Gera(a.Receber(Subir(100, 100, ms: 210)), false, "Click");
    }

    [Teste]
    public static void MovimentoSemOBotaoEsquerdo_EncerraOGestoEmVezDeGrudarNoCursor()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        a.Receber(Mover(200, 100));
        Gera(a.Receber(Mover(260, 100, pressionado: false)), false, "DragCancel");
        Gera(a.Receber(Mover(300, 100)), false);

        var pressionado = new ArbitroDeGestos();
        pressionado.Receber(Descer(100, 100));
        Gera(pressionado.Receber(Mover(100, 100, pressionado: false)), false, "DragCancel");
    }

    [Teste]
    public static void NovoPressionarSemOSoltarAnterior_CancelaEComecaOutroGesto()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        a.Receber(Mover(200, 100));
        Gera(a.Receber(Descer(210, 90)), true, "DragCancel", "Press(210,90)");
        Gera(a.Receber(Subir(210, 90)), false, "Click");
    }

    [Teste]
    public static void BotaoDireitoSoltoForaDeGesto_PedeOMenuNoPonto()
    {
        var a = new ArbitroDeGestos();
        Gera(a.Receber(new PonteiroPressionado(P(-500, 300), BotaoDoPonteiro.Direito, 0, Dpi96)), false);
        Gera(a.Receber(new PonteiroSolto(P(-500, 300), BotaoDoPonteiro.Direito, 10)), false, "ContextMenu(-500,300)");
    }

    [Teste]
    public static void BotaoDireitoDuranteGestoDoEsquerdo_EhIgnorado()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100));
        Gera(a.Receber(new PonteiroPressionado(P(100, 100), BotaoDoPonteiro.Direito, 5, Dpi96)), true);
        Gera(a.Receber(new PonteiroSolto(P(100, 100), BotaoDoPonteiro.Direito, 6)), true);
        a.Receber(Mover(160, 100));
        Gera(a.Receber(new PonteiroSolto(P(160, 100), BotaoDoPonteiro.Direito, 7)), true);
        Gera(a.Receber(Subir(160, 100)), false, "DragEnd(160,100)");
    }

    [Teste]
    public static void SemGesto_SoltarEMoverNaoGeramNada()
    {
        var a = new ArbitroDeGestos();
        Gera(a.Receber(Mover(100, 100)), false);
        Gera(a.Receber(Subir(100, 100)), false);
    }

    [Teste]
    public static void CoordenadasNegativasSaoPreservadas()
    {
        // Monitor à esquerda do principal (ARCHITECTURE.md 2.4): x negativo o tempo todo.
        var a = new ArbitroDeGestos();
        Gera(a.Receber(Descer(-960, 983)), true, "Press(-960,983)");
        Gera(a.Receber(Mover(-1500, 700)), true, "DragStart", "DragMove(-1500,700)");
        Gera(a.Receber(Mover(200, -30)), true, "DragMove(200,-30)");
        Gera(a.Receber(Subir(-1, -1)), false, "DragEnd(-1,-1)");
    }

    [Teste]
    public static void Reiniciar_EsqueceOGestoEACorridaDeCliqueDuploSemEmitir()
    {
        var a = new ArbitroDeGestos();
        a.Receber(Descer(100, 100, ms: 0));
        a.Receber(Subir(100, 100, ms: 10));
        a.Receber(Descer(100, 100, ms: 100));
        a.Receber(Mover(200, 100, ms: 110));
        a.Reiniciar();
        Afirmar.Falso(a.EmGesto, "sem gesto depois de reiniciar");
        Gera(a.Receber(Mover(300, 100, ms: 120)), false);
        a.Receber(Descer(300, 100, ms: 130));
        Gera(a.Receber(Subir(300, 100, ms: 140)), false, "Click");
    }
}
