using Buzzy.App.Composicao;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Os gestos sobre as janelas dos itens (DEC-028, passo T8; desenho do núcleo, 2.4; crítica, C15 e C18), sem janela: a
/// segunda instância do árbitro decide clique, clique duplo, arraste e captura perdida com as regras do personagem, e cada
/// gesto vira o evento do item em que começou — Press → ITEM_PRESS, DragStart → ITEM_DRAG_START, DragMove →
/// ITEM_DRAG_MOVE, DragEnd → ITEM_DRAG_END e Click, DoubleClick ou DragCancel → ITEM_RELEASE. O botão direito no item é
/// o CONTEXT_MENU de sempre: abre o mesmo menu (C15).
/// </summary>
internal sealed class GestosDosItensTestes
{
    private static readonly MetricasDeGesto Metricas = new(4, 4, 4, 4, 500);

    private static PonteiroPressionado Esquerdo(int x, int y, long ms = 0) => new(new PontoPx(x, y), BotaoDoPonteiro.Esquerdo, ms, Metricas);

    private static PonteiroSolto SoltarEsquerdo(int x, int y, long ms = 0) => new(new PontoPx(x, y), BotaoDoPonteiro.Esquerdo, ms);

    private static PonteiroMovido Mover(int x, int y, bool comBotao = true, long ms = 0) => new(new PontoPx(x, y), comBotao, ms);

    [Teste]
    public void Traduzir_CadaGestoNoEventoDoItem()
    {
        var p = new PontoPx(10, 20);
        Afirmar.Igual<Evento>(new ItemPress(7, p), GestosDosItens.Traduzir(new Press(p), 7));
        Afirmar.Igual<Evento>(new ItemDragStart(7), GestosDosItens.Traduzir(new DragStart(), 7));
        Afirmar.Igual<Evento>(new ItemDragMove(7, p), GestosDosItens.Traduzir(new DragMove(p), 7));
        Afirmar.Igual<Evento>(new ItemDragEnd(7, p), GestosDosItens.Traduzir(new DragEnd(p), 7));
        Afirmar.Igual<Evento>(new ItemRelease(7), GestosDosItens.Traduzir(new Click(), 7), "clique: o item cai de onde está");
        Afirmar.Igual<Evento>(new ItemRelease(7), GestosDosItens.Traduzir(new DoubleClick(), 7), "clique duplo, idem");
        Afirmar.Igual<Evento>(new ItemRelease(7), GestosDosItens.Traduzir(new DragCancel(), 7), "captura perdida, idem");
        Afirmar.Igual<Evento>(new ContextMenu(p), GestosDosItens.Traduzir(new ContextMenu(p), 7), "botão direito: o menu de sempre (C15)");
        Afirmar.Lanca<ArgumentException>(() => GestosDosItens.Traduzir(new CmdHide(), 7), "só gestos do árbitro");
    }

    [Teste]
    public void Clique_PressionaECaptura_SoltaELarga()
    {
        var g = new GestosDosItens();
        GestoDoItem press = g.Receber(3, Esquerdo(100, 200));
        Afirmar.Sequencia<Evento>([new ItemPress(3, new PontoPx(100, 200))], press.Eventos, "botão pressionado: o item fica na mão");
        Afirmar.Igual((true, (int?)3), (press.Capturar, press.ItemEmGesto), "captura na janela do item");
        Afirmar.Igual((int?)3, g.ItemEmGesto);

        GestoDoItem solto = g.Receber(3, SoltarEsquerdo(101, 201));
        Afirmar.Sequencia<Evento>([new ItemRelease(3)], solto.Eventos, "solto dentro do limiar: clique");
        Afirmar.Igual((false, (int?)null), (solto.Capturar, solto.ItemEmGesto), "a captura acaba com o gesto");
        Afirmar.Nulo(g.ItemEmGesto);
    }

    [Teste]
    public void Arraste_StartMoveEnd_ComOIdDoItem()
    {
        var g = new GestosDosItens();
        g.Receber(2, Esquerdo(100, 100));
        Afirmar.Sequencia<Evento>([], g.Receber(2, Mover(102, 101)).Eventos, "dentro do limiar, nada");
        GestoDoItem comeco = g.Receber(2, Mover(110, 100));
        Afirmar.Sequencia<Evento>([new ItemDragStart(2), new ItemDragMove(2, new PontoPx(110, 100))], comeco.Eventos, "saiu do limiar");
        Afirmar.Verdadeiro(comeco.Capturar, "captura durante o arraste");
        Afirmar.Sequencia<Evento>([new ItemDragMove(2, new PontoPx(150, 90))], g.Receber(2, Mover(150, 90)).Eventos);
        GestoDoItem fim = g.Receber(2, SoltarEsquerdo(160, 80));
        Afirmar.Sequencia<Evento>([new ItemDragEnd(2, new PontoPx(160, 80))], fim.Eventos, "soltou: ITEM_DRAG_END no ponto do cursor");
        Afirmar.Igual((false, (int?)null), (fim.Capturar, fim.ItemEmGesto));
    }

    [Teste]
    public void GestoRapido_SoltaForaDoLimiarSemMovimento_EhArraste()
    {
        var g = new GestosDosItens();
        g.Receber(4, Esquerdo(0, 0));
        Afirmar.Sequencia<Evento>([new ItemDragStart(4), new ItemDragEnd(4, new PontoPx(30, -5))], g.Receber(4, SoltarEsquerdo(30, -5)).Eventos);
    }

    [Teste]
    public void CliqueDuplo_CadaPressaoPegaECadaSolturaLarga()
    {
        var g = new GestosDosItens();
        Afirmar.Sequencia<Evento>([new ItemPress(1, new PontoPx(5, 5))], g.Receber(1, Esquerdo(5, 5, ms: 0)).Eventos);
        Afirmar.Sequencia<Evento>([new ItemRelease(1)], g.Receber(1, SoltarEsquerdo(5, 5, ms: 50)).Eventos, "primeiro clique");
        Afirmar.Sequencia<Evento>([new ItemPress(1, new PontoPx(5, 6))], g.Receber(1, Esquerdo(5, 6, ms: 200)).Eventos);
        Afirmar.Sequencia<Evento>([new ItemRelease(1)], g.Receber(1, SoltarEsquerdo(5, 6, ms: 250)).Eventos, "o clique duplo também larga");
    }

    [Teste]
    public void CapturaPerdidaOuMovimentoSemBotao_LargaOItemDoGesto()
    {
        var g = new GestosDosItens();
        g.Receber(8, Esquerdo(0, 0));
        g.Receber(8, Mover(20, 0));
        GestoDoItem perdida = g.Receber(8, new CapturaPerdida(10));
        Afirmar.Sequencia<Evento>([new ItemRelease(8)], perdida.Eventos, "captura perdida: o item cai de onde está");
        Afirmar.Igual((false, (int?)null), (perdida.Capturar, perdida.ItemEmGesto));
        Afirmar.Sequencia<Evento>([], g.Receber(8, SoltarEsquerdo(20, 0)).Eventos, "o soltar que chega depois não faz nada");

        g.Receber(9, Esquerdo(0, 0));
        Afirmar.Sequencia<Evento>([new ItemRelease(9)], g.Receber(9, Mover(1, 1, comBotao: false)).Eventos, "movimento sem o botão: nada fica preso ao cursor");
        Afirmar.Nulo(g.ItemEmGesto);
    }

    [Teste]
    public void PressionarOutroItemNoMeioDoGesto_LargaOAnteriorAntesDePegarONovo()
    {
        // Com mensagens postadas (risco R11 do desenho do app), um botão pressionado sem o soltar anterior: o árbitro
        // cancela o gesto antigo antes de começar o novo, e o ITEM_RELEASE vai para o item antigo, na ordem.
        var g = new GestosDosItens();
        g.Receber(4, Esquerdo(0, 0));
        g.Receber(4, Mover(40, 0));
        GestoDoItem outro = g.Receber(5, Esquerdo(300, 300));
        Afirmar.Sequencia<Evento>([new ItemRelease(4), new ItemPress(5, new PontoPx(300, 300))], outro.Eventos, "larga o 4 e pega o 5");
        Afirmar.Igual((true, (int?)5), (outro.Capturar, outro.ItemEmGesto), "a captura passa para o 5");
        Afirmar.Sequencia<Evento>([new ItemDragStart(5), new ItemDragMove(5, new PontoPx(320, 300))], g.Receber(5, Mover(320, 300)).Eventos);
    }

    [Teste]
    public void MovimentoQueChegaPorOutraJanela_ContinuaNoItemDoGesto()
    {
        // Com a captura, o mouse é entregue à janela do item do gesto; mesmo que a mensagem venha por outra, o gesto é dele.
        var g = new GestosDosItens();
        g.Receber(4, Esquerdo(0, 0));
        Afirmar.Sequencia<Evento>([new ItemDragStart(4), new ItemDragMove(4, new PontoPx(50, 0))], g.Receber(7, Mover(50, 0)).Eventos);
        Afirmar.Sequencia<Evento>([new ItemDragEnd(4, new PontoPx(60, 0))], g.Receber(7, SoltarEsquerdo(60, 0)).Eventos);
    }

    [Teste]
    public void BotaoDireito_AbreOMenuDeSempre_ForaDeUmGestoDoEsquerdo()
    {
        var g = new GestosDosItens();
        var p = new PontoPx(70, 80);
        Afirmar.Sequencia<Evento>([], g.Receber(6, new PonteiroPressionado(p, BotaoDoPonteiro.Direito, 0, Metricas)).Eventos, "o menu abre ao soltar");
        GestoDoItem menu = g.Receber(6, new PonteiroSolto(p, BotaoDoPonteiro.Direito, 10));
        Afirmar.Sequencia<Evento>([new ContextMenu(p)], menu.Eventos, "CONTEXT_MENU, como no personagem (C15)");
        Afirmar.Igual((false, (int?)null), (menu.Capturar, menu.ItemEmGesto), "sem captura");

        g.Receber(6, Esquerdo(0, 0));
        Afirmar.Sequencia<Evento>([], g.Receber(6, new PonteiroSolto(p, BotaoDoPonteiro.Direito, 20)).Eventos, "durante um gesto do esquerdo, o direito não abre o menu");
        Afirmar.Igual((int?)6, g.ItemEmGesto, "o gesto do esquerdo continua");
    }

    [Teste]
    public void Reiniciar_EsqueceOGestoSemLargarOItem()
    {
        // LIBERAR_CAPTURA_DO_ITEM: o núcleo já encerrou o gesto (esconder, sair, recolher); o árbitro esquece, e o soltar
        // que chegar depois não vira ITEM_RELEASE.
        var g = new GestosDosItens();
        g.Receber(3, Esquerdo(0, 0));
        g.Receber(3, Mover(30, 0));
        g.Reiniciar();
        Afirmar.Nulo(g.ItemEmGesto, "nenhum gesto em curso");
        GestoDoItem depois = g.Receber(3, SoltarEsquerdo(30, 0));
        Afirmar.Sequencia<Evento>([], depois.Eventos, "nada é emitido");
        Afirmar.Falso(depois.Capturar, "sem captura");
        Afirmar.Sequencia<Evento>([], g.Receber(3, Mover(40, 0)).Eventos, "nem os movimentos seguintes");
    }
}
