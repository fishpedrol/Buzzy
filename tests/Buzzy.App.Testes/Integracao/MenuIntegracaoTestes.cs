using System.Globalization;
using System.Windows;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// O menu de verdade, no Buzzy.exe aberto pelo teste (DEC-027; crítica, seção 6, V1 e V2): o botão direito e as teclas
/// de acesso são mensagens POSTADAS às janelas do próprio Buzzy, com o PID conferido antes de cada uma; nada passa pela
/// fila de input do Windows nem por outro aplicativo. O teclado vai para o dono do menu, cuja fila o laço modal do menu
/// lê. Só rodam com --integracao, depois de avisar o usuário: o menu aparece e some na tela.
/// </summary>
[Integracao]
internal sealed class MenuIntegracaoTestes
{
    private const int WM_CHAR = 0x0102;

    private static EventoDoLog EsperarDesde(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoDoLog? achado = BuzzyEmTeste.EventosDesde(marca).FirstOrDefault(condicao);
            if (achado is not null) return achado;
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(50);
        }
    }

    private static PontoPx Preparar(BuzzyEmTeste b)
    {
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        return EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
    }

    /// <summary>Abre o menu pelo botão direito postado no ponto opaco e devolve o dono, conferido como deste Buzzy.</summary>
    private static (nint Dono, EventoDoLog Exibindo) AbrirMenu(BuzzyEmTeste b, PontoPx opaco, long marca)
    {
        b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, opaco);
        b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, opaco);
        EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000, "menu exibido");
        var dono = (nint)long.Parse(exibindo["dono"], CultureInfo.InvariantCulture);
        Afirmar.Igual((uint)b.Processo.Id, NativoTeste.PidDe(dono), "o dono do menu é deste Buzzy");
        return (dono, exibindo);
    }

    private static EventoDoLog Fechado(BuzzyEmTeste b, long marca, string oQue)
        => EsperarDesde(b, marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 3000, oQue);

    private static EventoDoLog AbrirECancelar(BuzzyEmTeste b, PontoPx opaco)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        (nint dono, _) = AbrirMenu(b, opaco, marca);
        Afirmar.Verdadeiro(b.Postar(dono, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE ao dono do menu");
        return Fechado(b, marca, "menu fechado por WM_CANCELMODE");
    }

    private static (uint Gdi, uint User) Objetos(BuzzyEmTeste b)
        => (NativoTeste.GetGuiResources(b.Processo.Handle, NativoTeste.GR_GDIOBJECTS), NativoTeste.GetGuiResources(b.Processo.Handle, NativoTeste.GR_USEROBJECTS));

    /// <summary>Escolhe pelo teclado: a tecla do submenu e a da opção, como WM_CHAR postado ao dono do menu.</summary>
    private static EventoDoLog Escolher(BuzzyEmTeste b, PontoPx opaco, char submenu, char opcao, string emocaoMarcadaAntes)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        (nint dono, EventoDoLog exibindo) = AbrirMenu(b, opaco, marca);
        Afirmar.Igual(emocaoMarcadaAntes, exibindo["emocaoMarcada"], "a marca de rádio está na emoção atual");
        Afirmar.Verdadeiro(b.Postar(dono, WM_CHAR, submenu, 0, $"WM_CHAR '{submenu}'"), $"WM_CHAR '{submenu}' ao dono do menu");
        Afirmar.Verdadeiro(b.Postar(dono, WM_CHAR, opcao, 0, $"WM_CHAR '{opcao}'"), $"WM_CHAR '{opcao}' ao dono do menu");
        return Fechado(b, marca, $"menu fechado pelas teclas {submenu} e {opcao}");
    }

    [Teste]
    public void VinteAberturas_GdiEUserEstaveis_ECadaAberturaApagaOsBitmapsQueCriou()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(comOsNoveAdultos: true);
        PontoPx opaco = Preparar(b);
        // A primeira abertura carrega o tema do menu e as fontes, que ficam para as próximas.
        for (int i = 0; i < 2; i++) AbrirECancelar(b, opaco);
        Thread.Sleep(300);
        (uint Gdi, uint User) antes = Objetos(b);

        var fechamentos = new List<EventoDoLog>();
        for (int i = 0; i < 20; i++) fechamentos.Add(AbrirECancelar(b, opaco));
        Thread.Sleep(300);
        (uint Gdi, uint User) depois = Objetos(b);

        // Com a chave do tamagotchi ligada no aplicativo (passo T9), o menu tem os 14 rostos da emoção dominante e os 13
        // desenhos dos itens; em alto contraste, nenhum ícone.
        string icones = SystemParameters.HighContrast ? "0" : "27";
        foreach (EventoDoLog f in fechamentos)
        {
            Afirmar.Igual("Nenhum", f["fechado"], "cancelar não escolhe nada");
            Afirmar.Igual(icones, f["icones"], "os 14 rostos e os 13 itens (nenhum em alto contraste)");
            Afirmar.Igual(f["icones"], f["bitmapsCriados"], "um bitmap por ícone");
            Afirmar.Igual(f["bitmapsCriados"], f["bitmapsApagados"], "criados = apagados");
        }
        Console.WriteLine($"         GDI {antes.Gdi} → {depois.Gdi}; USER {antes.User} → {depois.User}; rostos {fechamentos[0]["lado"]} a {fechamentos[0]["dpi"]} DPI");
        Afirmar.Verdadeiro(Math.Abs((long)depois.Gdi - antes.Gdi) <= 2, $"objetos GDI estáveis (±2) depois de 20 aberturas: {antes.Gdi} → {depois.Gdi}");
        Afirmar.Verdadeiro(Math.Abs((long)depois.User - antes.User) <= 2, $"objetos USER estáveis (±2) depois de 20 aberturas: {antes.User} → {depois.User}");
        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void EmocaoPeloMenu_TecladoPostadoAoDono_TrocaACaraEMarcaAOpcao()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        PontoPx opaco = Preparar(b);

        // D abre "Emoção dominante" e F escolhe "Feliz".
        long marca = BuzzyEmTeste.MarcaDoLog();
        EventoDoLog feliz = Escolher(b, opaco, 'd', 'f', emocaoMarcadaAntes: "Automatica");
        Afirmar.Igual(("Emocao", "Feliz"), (feliz["fechado"], feliz["argumento"]), "o menu escolheu Feliz");
        Afirmar.Igual(feliz["bitmapsCriados"], feliz["bitmapsApagados"], "criados = apagados");
        EventoDoLog nucleo = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetDominantEmotion", 3000, "CMD_SET_DOMINANT_EMOTION no núcleo");
        Afirmar.Igual(("menu", "Idle", "Idle"), (nucleo["motivo"], nucleo["de"], nucleo["para"]), "vindo do menu, sem mudar o estado");
        Afirmar.Contem("Feliz", nucleo["regra"]);
        EsperarDesde(b, marca, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["expressao"] == "feliz", 3000, "o personagem parado com a cara feliz");

        // Na abertura seguinte, a marca está em Feliz; A volta para "Automática".
        long marcaAutomatica = BuzzyEmTeste.MarcaDoLog();
        EventoDoLog automatica = Escolher(b, opaco, 'd', 'a', emocaoMarcadaAntes: "Feliz");
        Afirmar.Igual(("Emocao", "Automatica"), (automatica["fechado"], automatica["argumento"]), "o menu escolheu Automática");
        EventoDoLog nucleoAutomatica = EsperarDesde(b, marcaAutomatica, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetDominantEmotion", 3000, "Automática no núcleo");
        Afirmar.Contem("Automatica", nucleoAutomatica["regra"]);

        long marcaFinal = BuzzyEmTeste.MarcaDoLog();
        (nint dono, EventoDoLog exibindo) = AbrirMenu(b, opaco, marcaFinal);
        Afirmar.Igual("Automatica", exibindo["emocaoMarcada"], "a marca voltou para Automática");
        Afirmar.Verdadeiro(b.Postar(dono, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE ao dono do menu");
        Afirmar.Igual("Nenhum", Fechado(b, marcaFinal, "menu cancelado")["fechado"]);

        Afirmar.Igual(0, b.FecharPorWmClose());
    }
}
