using System.Runtime.InteropServices;
using Buzzy.App.Apresentacao;
using Buzzy.App.Testes.Integracao;
using Buzzy.Core;
using Buzzy.Core.Entrada;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A janela de um item do tamagotchi (DEC-028, passo T8) com a receita da janela do personagem, criada SEM ser mostrada
/// (só o HWND, por EnsureHandle): nada aparece na tela, nada é ativado, e as mensagens vão só para o HWND deste próprio
/// processo de testes, pelo SendMessage, sem passar pela fila de input do Windows. Confere o estilo sem ativação e fora
/// do Alt+Tab, a resposta MA_NOACTIVATE, o tamanho do item no WM_GETDPISCALEDSIZE, o mouse virando evento de ponteiro só
/// nos botões fora de um gesto, e que só a raiz fecha a janela. A captura e a janela na tela ficam para a integração (T9).
/// </summary>
internal sealed class JanelaDoItemTestes
{
    private const int WM_MOUSEACTIVATE = 0x0021, WM_CONTEXTMENU = 0x007B, WM_RBUTTONDOWN = 0x0204, WM_CAPTURECHANGED = 0x0215, WM_GETDPISCALEDSIZE = 0x02E4;
    private const int MA_NOACTIVATE = 3;

    private static readonly TamanhoDip Tamanho = new(48, 48);

    private static nint Enviar(nint hwnd, int msg, nint wParam, nint lParam)
    {
        Afirmar.Diferente((nint)0, NativoTeste.SendMessageTimeout(hwnd, msg, wParam, lParam, NativoTeste.SMTO_ABORTIFHUNG, 2000, out nint resultado),
            $"mensagem 0x{msg:X4} entregue");
        return resultado;
    }

    private static nint Cliente(int x, int y) => (nint)((uint)(ushort)(short)x | ((uint)(ushort)(short)y << 16));

    /// <summary>Cria a janela sem mostrar, roda o teste e fecha pela raiz, conferindo que o HWND some.</summary>
    private static void ComJanela(Action<JanelaDoItem> teste)
    {
        var janela = new JanelaDoItem(5, Tamanho);
        nint hwnd = 0;
        try
        {
            janela.CriarSemMostrar();
            hwnd = janela.Hwnd;
            Afirmar.Diferente((nint)0, hwnd, "o HWND existe");
            teste(janela);
            Afirmar.Falso(NativoTeste.IsWindowVisible(hwnd), "a janela nunca apareceu na tela");
        }
        finally
        {
            janela.Fechar();
        }
        Afirmar.Falso(NativoTeste.IsWindow(hwnd), "fechada pela raiz, o HWND não existe mais");
    }

    [Teste]
    public void Estilo_SemAtivacao_ForaDoAltTab_Layered_NoTopo_EMaNoActivate()
    {
        ComJanela(j =>
        {
            long estilo = NativoTeste.EstiloEstendido(j.Hwnd);
            Afirmar.Verdadeiro((estilo & NativoTeste.WS_EX_NOACTIVATE) != 0, "WS_EX_NOACTIVATE: nunca ativada");
            Afirmar.Verdadeiro((estilo & NativoTeste.WS_EX_TOOLWINDOW) != 0, "WS_EX_TOOLWINDOW: fora do Alt+Tab");
            Afirmar.Verdadeiro((estilo & NativoTeste.WS_EX_LAYERED) != 0, "WS_EX_LAYERED: transparência por pixel, o clique só nos pixels opacos");
            Afirmar.Igual(0L, estilo & NativoTeste.WS_EX_APPWINDOW, "sem WS_EX_APPWINDOW: fora da barra de tarefas");
            Afirmar.Verdadeiro(j.Topmost && !j.ShowActivated && !j.ShowInTaskbar && !j.Focusable, "sempre no topo, mostrar sem ativar, sem foco");
            Afirmar.Igual((nint)MA_NOACTIVATE, Enviar(j.Hwnd, WM_MOUSEACTIVATE, j.Hwnd, 0), "o clique chega, mas o aplicativo em uso continua com o foco");
        });
    }

    [Teste]
    public void WmGetDpiScaledSize_OTamanhoDoItemNoDpiNovo()
    {
        ComJanela(j =>
        {
            nint tamanho = Marshal.AllocHGlobal(8);
            try
            {
                foreach (int dpi in new[] { 96, 120, 144, 192, 288 })
                {
                    Marshal.WriteInt64(tamanho, 0);
                    Afirmar.Igual((nint)1, Enviar(j.Hwnd, WM_GETDPISCALEDSIZE, dpi, tamanho), $"{dpi} DPI: tratado");
                    TamanhoPx esperado = Tamanho.ParaPixels(dpi);
                    Afirmar.Igual((esperado.Largura, esperado.Altura), (Marshal.ReadInt32(tamanho, 0), Marshal.ReadInt32(tamanho, 4)), $"{dpi} DPI: o tamanho do item, não o pedido escalado");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tamanho);
            }
        });
    }

    [Teste]
    public void Mouse_BotoesViramEventosEmPixelsDaTela_MovimentoSoNoGesto()
    {
        ComJanela(j =>
        {
            var recebidos = new List<EventoDePonteiro>();
            j.Ponteiro += recebidos.Add;
            j.AplicarRetangulo(new RetanguloPx(100, 200, 148, 248)); // sem mostrar: só o lugar do HWND escondido

            Enviar(j.Hwnd, NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, Cliente(3, 4));
            Enviar(j.Hwnd, NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, Cliente(10, 4));
            Enviar(j.Hwnd, NativoTeste.WM_LBUTTONUP, 0, Cliente(5, 6));
            Enviar(j.Hwnd, WM_RBUTTONDOWN, 0, Cliente(7, 8));
            Enviar(j.Hwnd, NativoTeste.WM_RBUTTONUP, 0, Cliente(7, 9));
            Afirmar.Igual((nint)0, Enviar(j.Hwnd, WM_CONTEXTMENU, j.Hwnd, Cliente(107, 209)), "WM_CONTEXTMENU tratado, sem evento: o menu sai do botão direito solto");
            Enviar(j.Hwnd, WM_CAPTURECHANGED, 0, 0);

            Afirmar.Igual(4, recebidos.Count, "dois botões pressionados e dois soltos; o movimento fora de um gesto e a captura que não era dela não contam");
            var pressao = (PonteiroPressionado)recebidos[0];
            Afirmar.Igual((new PontoPx(103, 204), BotaoDoPonteiro.Esquerdo), (pressao.Ponto, pressao.Botao), "botão esquerdo, em pixels da tela");
            Afirmar.Verdadeiro(pressao.Metricas.ArrasteX > 0 && pressao.Metricas.TempoDeCliqueDuploMs > 0, "com as métricas do sistema no DPI da janela");
            var solto = (PonteiroSolto)recebidos[1];
            Afirmar.Igual((new PontoPx(105, 206), BotaoDoPonteiro.Esquerdo), (solto.Ponto, solto.Botao));
            var direito = (PonteiroPressionado)recebidos[2];
            Afirmar.Igual((new PontoPx(107, 208), BotaoDoPonteiro.Direito), (direito.Ponto, direito.Botao));
            var direitoSolto = (PonteiroSolto)recebidos[3];
            Afirmar.Igual((new PontoPx(107, 209), BotaoDoPonteiro.Direito), (direitoSolto.Ponto, direitoSolto.Botao));

            Afirmar.Falso(j.Capturando, "sem gesto pedido pela raiz, nenhuma captura");
            j.SoltarCaptura();
            Afirmar.Igual(4, recebidos.Count, "soltar sem captura não emite nada");
        });
    }

    [Teste]
    public void SoARaizFechaAJanela()
    {
        ComJanela(j =>
        {
            j.Close();
            Afirmar.Verdadeiro(NativoTeste.IsWindow(j.Hwnd), "um pedido de fechamento que não é da raiz é recusado: a janela continua");
        });
    }

    [Teste]
    public void VinteJanelasCriadasEFechadas_ObjetosUserEGdiVoltamAoPatamar()
    {
        // Janelas fechadas ao remover e ao sair (crítica, seção 4): cada janela de item fechada pela raiz devolve o HWND e o
        // resto dos objetos do Windows. A primeira janela do WPF no processo cria recursos que ficam (aquecimento).
        static void CriarEFechar(int id)
        {
            var j = new JanelaDoItem(id, Tamanho);
            try
            {
                j.CriarSemMostrar();
                j.DefinirSprite(SpriteDoItem.Renderizar((Core.Personagem.Item)(id % 13), 96));
                j.AplicarRetangulo(new RetanguloPx(100, 200, 148, 248));
            }
            finally
            {
                j.Fechar();
            }
        }

        for (int i = 0; i < 2; i++) CriarEFechar(100 + i);
        uint gdi = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), user = NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS);
        for (int i = 0; i < 20; i++) CriarEFechar(i);
        Afirmar.Igual((gdi, user), (NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_GDIOBJECTS), NativoTeste.ObjetosDesteProcesso(NativoTeste.GR_USEROBJECTS)),
            "objetos (GDI, USER) depois de 20 janelas de item criadas e fechadas");
    }
}
