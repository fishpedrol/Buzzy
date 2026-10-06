using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Visual.Pixel;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética do tamagotchi adulto e da emoção dominante (DEC-027 e DEC-028; crítica de integração, seção 6,
/// casos V1 a V16 e X1), com a chave ligada no aplicativo (passo T9), mais o caso "pausar com ele na parede sem estar
/// preso" (correção do núcleo, achado 4), os do alívio e da paranoia (pedidos do usuário de 2026-10-01: V18, V17b e
/// V17), o do baseado por conta própria (pedido das 19:10: V19) e o da chave do conteúdo adulto (pedido de 2026-10-02:
/// V20). Abre o receptor (o "aplicativo do usuário", com o foco) e
/// o Buzzy várias vezes, sempre com o perfil de teste
/// <c>verificacao</c>, <c>--diagnostico</c> e <c>--semente</c>: pausado com uma semente fixa (V1 a V7, V10 e V13 a V15,
/// X1, V18, V20 e, de novo, V9), pausado com a semente em que o primeiro sorteio da paranoia sai (V17b) e com a agenda ligada e
/// sementes escolhidas por simulação do núcleo (<see cref="SementesDoTamagotchi"/>: V8, V11, V12, a parede e V17; e
/// <see cref="Buzzy.App.Testes.Integracao.SementesDoBaseado"/>: V19). Todo
/// clique, arraste e tecla é SINTÉTICO (SendInput), com as conferências do <see cref="Injetor"/> (dono do ponto, deriva do
/// cursor, input de outra fonte). Os itens só aparecem pelo nome no menu; tudo é de desenho animado, e a classe de cada
/// item (droga sintética ou não) é regra de jogo.
///
/// O V16 (a emoção restaurada ao reabrir, com a persistência ligada no passo P7 da Fase 5) reabre o Buzzy sem limpar a
/// pasta do perfil e confere o settings.json dela pelo esquema do núcleo. O que não dá para exercitar aqui fica como N/A ou
/// "não exercitado": X1 com um monitor só, os 10 minutos do V13 (aqui, 60 s; os 10 minutos ficam em
/// tools\medir-desempenho.ps1 -Modo onda) e as pendências [MANUAL] e [HW] da crítica.
/// </summary>
internal sealed partial class Verificacao
{
    /// <summary>Semente dos Buzzy abertos pausados (<c>--semente</c>): os sorteios de cara, que a onda faz mesmo pausado, se repetem.</summary>
    internal const ulong SementePadraoDoTamagotchi = 2028;

    /// <summary>Duração da medição curta do V13, em segundos (a de 10 minutos é a do medir-desempenho).</summary>
    private const int SegundosDoRepousoComOnda = 60;

    private static readonly Regex ReOndaQueVirou = new(@"ITEM_EFFECT_TIMER: onda \w+/\w+/\d -> (?<tipo>\w+)/(?<fase>\w+)/(?<nivel>\d)", RegexOptions.Compiled);

    private ulong _sementeDoTamagotchi = SementePadraoDoTamagotchi;
    private int _menusDoTamagotchi;
    private int _menusSemVoltarOFoco;

    /// <summary>Um item na tela, como o log do Buzzy o descreve: o Id do núcleo, o nome, a janela (deste Buzzy) e os pontos de teste em relação à janela.</summary>
    private sealed record ItemVisto(int Id, Item Item, nint Hwnd, Nativo.POINT DeslocOpaco, Nativo.POINT DeslocTransparente)
    {
        internal string IdNoLog => Id.ToString(CultureInfo.InvariantCulture);

        internal string Nome => Item.ToString();

        internal Nativo.POINT Opaco(Nativo.RECT r) => new(r.Left + DeslocOpaco.X, r.Top + DeslocOpaco.Y);

        internal Nativo.POINT Transparente(Nativo.RECT r) => new(r.Left + DeslocTransparente.X, r.Top + DeslocTransparente.Y);
    }

    /// <param name="semente">A semente dos Buzzy abertos pausados; nula, <see cref="SementePadraoDoTamagotchi"/>.</param>
    internal Sumario ExecutarTamagotchi(ulong? semente)
    {
        if (semente is { } s) _sementeDoTamagotchi = s;
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            Topologia topologia = TopologiaLida();
            _rel.Linha($"   Buzzy pausado com a semente {_sementeDoTamagotchi}; com a agenda ligada, sementes escolhidas por simulação do núcleo (configuração do aplicativo, chave do tamagotchi ligada)");

            // Parte 1: pausado. O menu dá o primeiro plano ao dono dele de propósito (DEC-016): o período sem nenhuma
            // perda de foco fecha antes do primeiro menu; depois, o foco é conferido cenário a cenário.
            _prefixo = "Fase 1 (regressão) — ";
            AbrirBuzzy(_sementeDoTamagotchi, comOsNoveAdultos: true);
            _prefixo = "Tamagotchi — ";
            _marcaFoco = _logReceptor.Contar();
            Digitar("T1-inicio;");
            FecharPeriodoDeFoco("da abertura até o primeiro menu");

            V1EmocaoPeloMenu();
            Digitar("T2-emocao;");
            V2VinteAberturasDoMenu();
            Digitar("T3-menu;");
            (ItemVisto Item, Nativo.RECT NoChao)? banana = V3InvocarBanana();
            Digitar("T4-banana;");
            if (banana is { } b) V4ArrastarAteEle(b.Item, b.NoChao);
            else Registrar("V4 — arrastar a banana até ele", Inconclusivo, "o V3 não deixou a banana no chão");
            Digitar("T5-uso;");
            V6PressionarNoMeioDoUso();
            Digitar("T6-press;");
            ItemVisto? solta = V5SoltarLonge();
            Digitar("T7-longe;");
            List<ItemVisto> itens = V7SetimoItem(solta);
            Digitar("T8-setimo;");
            V15BotaoDireitoNumItem(itens);
            Digitar("T9-direito;");
            X1OutroMonitor();
            Digitar("T10-monitor;");
            V10EsconderEMostrar();
            Digitar("T11-esconder;");
            V13RepousoComAOndaDaVodka();
            Digitar("T12-repouso;");
            V14SairComItensNaTela();
            Digitar("T13-sair;");
            V16EmocaoGravadaERestauradaAoReabrir();
            Digitar("T13-reabrir;");

            // Parte 2: preso no cipó, na parede e no esconderijo (pausado, de novo da posição inicial).
            V9PresoNoCipoNaParedeENoEsconderijo(topologia);
            Digitar("T14-preso;");

            // Parte 3: com a agenda ligada.
            V8SegurarUmItemComAAgendaLigada(topologia);
            Digitar("T15-segurar;");
            V11CocainaContraBaseado(topologia);
            Digitar("T16-velocidade;");
            V12BebedeiraNoNivel3(topologia);
            Digitar("T17-nivel3;");
            PausarComEleNaParedeSemEstarPreso(topologia);
            Digitar("T18-parede;");

            // Parte 4: o alívio e a paranoia (pedidos do usuário de 2026-10-01), de novo da posição inicial: pausado (V18 e
            // V17b) e com a agenda ligada (V17).
            V18ComidaAcalmaUmPasso();
            Digitar("T19-alivio;");
            V17bMisturaSemSinteticaNaoDeixaParanoico();
            Digitar("T20-sem-sintetica;");
            V17ParanoiaComMisturaComSintetica(topologia);
            Digitar("T21-paranoia;");

            // Parte 5: o baseado por conta própria (pedido do usuário de 2026-10-01, 19:10), com a agenda ligada.
            V19BaseadoPorContaPropria(topologia);
            Digitar("T22-baseado;");

            // Parte 6: a chave do conteúdo adulto (pedido do usuário de 2026-10-02; DEC-033), pausado.
            V20ConteudoAdulto();
            Digitar("T23-adulto;");

            Registrar("Menus do tamagotchi — depois de fechar, o foco volta ao aplicativo em uso", _menusSemVoltarOFoco == 0,
                $"{_menusDoTamagotchi} menu(s) operado(s); em {_menusSemVoltarOFoco}, o receptor não voltou sozinho ao primeiro plano em 2 s e foi ativado pela ferramenta");
            _prefixo = "";

            _inj.ConferirUltimoInput();
        }
        catch (Interferencia e)
        {
            _houveInterferencia = true;
            _prefixo = "";
            Registrar("EXECUÇÃO", Invalida, "interferência humana detectada: " + e.Message + " Os resultados desta execução não valem.");
        }
        catch (Exception e)
        {
            _prefixo = "";
            Registrar("EXECUÇÃO", Falhou, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _prefixo = "";
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        RegistrarCoberturaDoTamagotchi();
        return Resumir();
    }

    // ------------------------------------------------------------------ parte 1: pausado

    /// <summary>V1: D e a letra no menu do personagem escolhem a emoção dominante; a cara muda e a opção fica marcada; "Automática" também.</summary>
    private void V1EmocaoPeloMenu()
    {
        const string Criterio = "V1 — emoção dominante pelo menu (clique direito, D e a letra): a cara muda e a opção fica marcada; \"Automática\" também funciona";
        long marca = LogDoBuzzy.Marca();
        MenuOperado feliz = MenuNoPersonagem("V1, Feliz", (Vk('D'), "D (Emoção dominante)"), (Vk('F'), "F (Feliz)"));
        // Vale a linha NUCLEO da transição, que traz de/para e a regra (o pedido de gravação do GravarPreferencias vai para as linhas CONFIG).
        EventoBuzzy? nucleoFeliz = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetDominantEmotion" && e["para"].Length > 0, 3000);
        EventoBuzzy? caraFeliz = LogDoBuzzy.Esperar(marca, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["expressao"] == "feliz", 3000);

        long marcaAutomatica = LogDoBuzzy.Marca();
        MenuOperado automatica = MenuNoPersonagem("V1, Automática", (Vk('D'), "D (Emoção dominante)"), (Vk('A'), "A (Automática)"));
        EventoBuzzy? marcadaFeliz = PrimeiroDesde(marcaAutomatica, e => e.Chave == "MENU" && e["exibindo"] == "sim");
        EventoBuzzy? nucleoAutomatica = LogDoBuzzy.Esperar(marcaAutomatica, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetDominantEmotion" && e["para"].Length > 0, 3000);

        long marcaFinal = LogDoBuzzy.Marca();
        MenuOperado conferencia = MenuNoPersonagem("V1, conferência", (VK_ESCAPE, "Esc (fechar sem escolher)"));
        EventoBuzzy? marcadaAutomatica = PrimeiroDesde(marcaFinal, e => e.Chave == "MENU" && e["exibindo"] == "sim");

        bool escolheuFeliz = feliz.Fechado?["fechado"] == "Emocao" && feliz.Fechado?["argumento"] == "Feliz";
        bool escolheuAutomatica = automatica.Fechado?["fechado"] == "Emocao" && automatica.Fechado?["argumento"] == "Automatica";
        bool ok = escolheuFeliz && nucleoFeliz?["regra"].Contains("Feliz", StringComparison.Ordinal) == true && caraFeliz is not null
            && marcadaFeliz?["emocaoMarcada"] == "Feliz" && escolheuAutomatica && nucleoAutomatica?["regra"].Contains("Automatica", StringComparison.Ordinal) == true
            && marcadaAutomatica?["emocaoMarcada"] == "Automatica" && conferencia.Fechado?["fechado"] == "Nenhum";
        Registrar(Criterio, feliz.Recusado || automatica.Recusado ? Inconclusivo : ok ? Ok : Falhou,
            $"Feliz: {DescreverMenu(feliz)}; núcleo={nucleoFeliz?["regra"] ?? "nada"}; sprite parado com a cara feliz={caraFeliz is not null}; "
            + $"na abertura seguinte, marcada={marcadaFeliz?["emocaoMarcada"] ?? "?"}; Automática: {DescreverMenu(automatica)}; núcleo={nucleoAutomatica?["regra"] ?? "nada"}; "
            + $"na terceira abertura, marcada={marcadaAutomatica?["emocaoMarcada"] ?? "?"}");
    }

    /// <summary>V2: 20 aberturas do menu (clique direito e Esc), com os objetos GDI e USER do Buzzy estáveis e os bitmaps de cada abertura apagados.</summary>
    private void V2VinteAberturasDoMenu()
    {
        const string Criterio = "V2 — 20 aberturas do menu: objetos GDI e USER do Buzzy estáveis (±2) e, em cada abertura, bitmaps criados = apagados";
        // As duas primeiras carregam o tema e as fontes do menu, que ficam para as próximas.
        for (int i = 0; i < 2; i++) MenuNoPersonagem("V2, aquecimento", (VK_ESCAPE, "Esc"));
        Thread.Sleep(300);
        (uint gdiAntes, uint userAntes) = ObjetosDoBuzzy();
        var fechamentos = new List<EventoBuzzy?>();
        int recusados = 0;
        for (int i = 0; i < 20; i++)
        {
            MenuOperado m = MenuNoPersonagem("V2", (VK_ESCAPE, "Esc"));
            fechamentos.Add(m.Fechado);
            if (m.Recusado) recusados++;
        }
        Thread.Sleep(300);
        (uint gdiDepois, uint userDepois) = ObjetosDoBuzzy();
        string icones = System.Windows.SystemParameters.HighContrast ? "0" : "27";
        int certas = fechamentos.Count(f => f?["fechado"] == "Nenhum" && f["icones"] == icones && f["bitmapsCriados"] == f["bitmapsApagados"]);
        bool estaveis = Math.Abs((long)gdiDepois - gdiAntes) <= 2 && Math.Abs((long)userDepois - userAntes) <= 2;
        Registrar(Criterio, recusados > 0 ? Inconclusivo : certas == 20 && estaveis ? Ok : Falhou,
            $"GDI {gdiAntes} → {gdiDepois}; USER {userAntes} → {userDepois}; aberturas com {icones} ícones (14 rostos e 13 itens; nenhum em alto contraste), fechadas sem escolha e com criados = apagados: {certas}/20; recusadas: {recusados}");
    }

    /// <summary>
    /// V3: a banana pelo menu pousa ao lado em até 1,5 s e fica parada; o relógio desliga; o ponto opaco é da janela dela e o
    /// transparente, do receptor (um clique nele chega ao receptor); o foco fica no receptor.
    /// </summary>
    private (ItemVisto Item, Nativo.RECT NoChao)? V3InvocarBanana()
    {
        const string Criterio = "V3 — invocar a banana: pousa ao lado em até 1,5 s e fica 3 s parada; o relógio desliga; o ponto opaco é da janela do item e o transparente, do receptor; o foco fica no receptor";
        long marca = LogDoBuzzy.Marca();
        if (InvocarItem(Item.Banana, 'B', "V3") is not { } banana) return null;
        int marcaR = _logReceptor.Contar();
        Nativo.RECT corpo = Nativo.Retangulo(_hBuzzy);
        if (EsperarItemNoChao(banana, marca, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(Criterio, Falhou, "a banana não parou no chão em 4 s");
            return null;
        }
        // Do item à vista ao pouso: o relógio, ligado só pela queda (ele está pausado e parado), desliga no passo em que o
        // item para no chão. A linha ITEM|movido…|parado=sim do pouso sai no fim do mesmo processamento, depois dela.
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
        int iMostrado = ev.FindIndex(e => e.Chave == "ITEM" && e["mostrado"] == banana.IdNoLog);
        EventoBuzzy? mostrado = iMostrado >= 0 ? ev[iMostrado] : null;
        EventoBuzzy? pousou = iMostrado >= 0 ? ev.Skip(iMostrado).FirstOrDefault(e => e.Chave == "RELOGIO" && e["ligado"] == "nao") : null;
        double? segundos = mostrado?.Instante is { } t0 && pousou?.Instante is { } t1 ? (t1 - t0).TotalSeconds : null;

        // Fica parada 3 s: a janela não sai do lugar e não há outro movimento dela no log.
        long marcaParada = LogDoBuzzy.Marca();
        bool ficou = true;
        var relogio = Stopwatch.StartNew();
        while (relogio.ElapsedMilliseconds < 3000)
        {
            ficou &= Nativo.Retangulo(banana.Hwnd).Equals(noChao);
            Thread.Sleep(100);
        }
        ficou &= !LogDoBuzzy.Desde(marcaParada).Any(e => e.Chave == "ITEM" && e["movido"] == banana.IdNoLog);
        List<EventoBuzzy> todos = LogDoBuzzy.Desde(marca);
        int ligou = todos.FindIndex(e => e.Chave == "RELOGIO" && e["ligado"] == "sim");
        bool desligou = ligou >= 0 && todos.Skip(ligou).Any(e => e.Chave == "RELOGIO" && e["ligado"] == "nao")
            && todos.LastOrDefault(e => e.Chave == "RELOGIO")?["ligado"] == "nao";

        // Os pontos de teste: o opaco é da janela do item; o transparente, do receptor, e um clique nele chega lá.
        Nativo.POINT opaco = banana.Opaco(noChao), transparente = banana.Transparente(noChao);
        nint donoOpaco = Nativo.DonoDoPonto(opaco.X, opaco.Y), donoTransparente = Nativo.DonoDoPonto(transparente.X, transparente.Y);
        bool cliqueNoReceptor = false, buzzyRecebeu = false;
        string recusa = "";
        if (donoTransparente == _hReceptor)
        {
            long marcaClique = LogDoBuzzy.Marca();
            int marcaCliqueR = _logReceptor.Contar();
            try
            {
                _inj.CliqueEsquerdo(transparente.X, transparente.Y, _hReceptor);
                Thread.Sleep(500);
                cliqueNoReceptor = _logReceptor.Desde(marcaCliqueR).Select(l => ReClique.Match(l)).Any(m => m.Success
                    && Math.Abs(int.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture) - transparente.X) <= 1
                    && Math.Abs(int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) - transparente.Y) <= 1);
                buzzyRecebeu = LogDoBuzzy.Desde(marcaClique).Any(e => e.Chave == "CLIQUE" || (e.Chave == "ITEM" && e.Campos.ContainsKey("clique")));
            }
            catch (CliqueRecusado e)
            {
                recusa = e.Message;
            }
        }
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        bool aoLado = !Intersectam(noChao, corpo) && noChao.Bottom == _areaUtilPrincipal.Bottom;
        bool ok = segundos is <= 1.5 && ficou && desligou && donoOpaco == banana.Hwnd && cliqueNoReceptor && !buzzyRecebeu && aoLado && frente && !desativou;
        Registrar(Criterio, recusa.Length > 0 ? Inconclusivo : ok ? Ok : Falhou,
            $"do item à vista até o pouso (relógio desligado): {(segundos is { } x ? x.ToString("0.000", CultureInfo.InvariantCulture) + " s" : "sem as linhas ITEM|mostrado e RELOGIO|ligado=nao")}; "
            + $"no chão, ao lado do personagem, sem cobri-lo={aoLado} (item {noChao}, personagem {corpo}); parado 3 s={ficou}; relógio ligou na queda e desligou={desligou}; "
            + $"ponto opaco {opaco} é de {Quem(donoOpaco)}{(donoOpaco == banana.Hwnd ? " (a janela da banana)" : "")}; ponto transparente {transparente} é de {Quem(donoTransparente)}; "
            + $"clique SINTÉTICO no transparente chegou ao receptor={cliqueNoReceptor}, ao Buzzy={buzzyRecebeu}{(recusa.Length > 0 ? "; " + recusa : "")}; receptor na frente={frente}; desativado={desativou}");
        return (banana, noChao);
    }

    /// <summary>V4: a banana arrastada até ele: USING, os quadros de uso na ordem, a janela some e ele volta a IDLE; M5 do arraste do item.</summary>
    private void V4ArrastarAteEle(ItemVisto banana, Nativo.RECT noChao)
    {
        const string Criterio = "V4 — arrastar a banana até ele: USING, os quadros de uso na ordem, a janela do item some e ele volta a IDLE; arraste do item com p95 abaixo de 16,7 ms";
        int marcaR = _logReceptor.Contar();
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(banana, noChao, SolturaNoCentroDele(banana, noChao), out string recusa))
        {
            Registrar(Criterio, Inconclusivo, recusa);
            return;
        }
        EventoBuzzy? uso = Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000);
        EventoBuzzy? fim = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Using" && e["para"] == "Settling", 6000);
        EventoBuzzy? idle = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Settling" && e["para"] == "Idle", 3000);
        EventoBuzzy? solto = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["solto"] == banana.IdNoLog, 2000);
        EventoBuzzy? removido = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["removido"] == banana.IdNoLog && e["motivo"] == "Usado", 2000);
        Thread.Sleep(200);
        bool sumiu = !Nativo.IsWindow(banana.Hwnd);
        double p95 = solto is not null && double.TryParse(solto["m5P95Ms"], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;

        // Os quadros de uso: as linhas SPRITE com a banana na mão, na ordem em que aparecem (cada quadro é desenhado uma
        // vez neste processo: é o primeiro uso da banana), contra a sequência de "comer" da pixel art, sem repetição.
        List<EventoBuzzy> trecho = LogDoBuzzy.Desde(marca);
        string[] vistos = [.. trecho.Where(e => e.Chave == "SPRITE" && e["item"] == "banana").Select(e => e["pose"])];
        IReadOnlyList<QuadroDeUso> sequencia = UsosPixel.Sequencia(VerboDaArte(TabelaDoTamagotchi.DoItem(Item.Banana).Verbo));
        string[] esperados = [.. sequencia.Select(q => q.Pose.Nome).Distinct()];
        bool naOrdem = vistos.SequenceEqual(esperados);
        string[]? pulados = naOrdem ? [] : QuadrosPulados(vistos, sequencia);
        bool soCurtosPulados = !naOrdem && pulados is not null;
        bool relogioAtrasou = trecho.Any(e => e.Chave == "RELOGIO" && e["atraso"] == "limitado");
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        bool resto = uso is not null && fim is not null && idle is not null && solto?["sobre"] == "sim" && solto["usado"] == "sim" && removido is not null && sumiu
            && p95 < 16.7 && frente && !desativou;
        // Revisão de correção, achado 4: um quadro curto ausente, com os outros na ordem, não prova defeito. Numa parada da
        // thread da interface, o relógio aplica os passos atrasados num lote só (até 15 passos, 250 ms: acima disso, a
        // linha RELOGIO|atraso=limitado), e um quadro de até 14 passos que caia inteiro dentro do lote não é desenhado.
        string resultado = resto && naOrdem ? Ok : resto && soCurtosPulados ? Inconclusivo : Falhou;
        Registrar(Criterio, resultado,
            $"ITEM_DRAG_END→USING={uso is not null} ({uso?["regra"] ?? "-"}); fim do uso→SETTLING→IDLE={fim is not null && idle is not null}; solto sobre ele={solto?["sobre"] ?? "?"}, usado={solto?["usado"] ?? "?"}; "
            + $"janela removida (motivo Usado)={removido is not null}, não existe mais={sumiu}; quadros vistos [{string.Join(", ", vistos)}], esperados [{string.Join(", ", esperados)}]"
            + (naOrdem ? "" : pulados is null
                ? $" (fora de ordem, repetidos, a mais ou com um quadro de {MaiorLoteDoRelogio} passos ou mais faltando: defeito; RELOGIO|atraso=limitado no trecho={relogioAtrasou})"
                : $" (pulados [{string.Join(", ", pulados)}], todos com menos de {MaiorLoteDoRelogio} passos: uma parada da thread da interface explica; repita a verificação; RELOGIO|atraso=limitado no trecho={relogioAtrasou})")
            + $"; M5 do arraste do item: {solto?["movimentos"] ?? "?"} movimentos, média {solto?["m5MediaMs"] ?? "?"} ms, p95 {solto?["m5P95Ms"] ?? "?"} ms; receptor na frente={frente}; desativado={desativou}");
    }

    /// <summary>
    /// O maior lote de passos que o relógio aplica de uma vez depois de uma parada da thread da interface: 250 ms a 60 passos
    /// por segundo (Aplicacao.AtrasoMaximoDoRelogio). Um quadro com este número de passos ou mais nunca é pulado.
    /// </summary>
    private const int MaiorLoteDoRelogio = 15;

    /// <summary>
    /// Se os quadros <paramref name="vistos"/> (as linhas SPRITE: cada pose desenhada uma vez, na primeira vez em que
    /// aparece, e depois tirada do cache) saem da <paramref name="sequencia"/> pulando só ocorrências curtas (menos de
    /// <see cref="MaiorLoteDoRelogio"/> passos), devolve as ocorrências puladas ("pose (N passos)"); senão (fora de ordem,
    /// repetido, a mais ou uma ocorrência longa pulada), nulo.
    /// </summary>
    internal static string[]? QuadrosPulados(IReadOnlyList<string> vistos, IReadOnlyList<QuadroDeUso> sequencia)
    {
        var desenhados = new HashSet<string>(StringComparer.Ordinal);
        var pulados = new List<string>();
        int i = 0;
        foreach (QuadroDeUso q in sequencia)
        {
            string pose = q.Pose.Nome;
            if (desenhados.Contains(pose)) continue; // já no cache: nenhuma linha nova
            if (i < vistos.Count && vistos[i] == pose)
            {
                desenhados.Add(pose);
                i++;
                continue;
            }
            if (q.Passos >= MaiorLoteDoRelogio) return null;
            pulados.Add($"{pose} ({q.Passos} passos)");
        }
        return i == vistos.Count ? [.. pulados] : null;
    }

    /// <summary>V6: no meio do uso de um cigarro (3,5 s), o PRESS nele vai para PRESSED no mesmo evento, e a onda continua.</summary>
    private void V6PressionarNoMeioDoUso()
    {
        const string Criterio = "V6 — pressionar no meio do uso: PRESSED no mesmo evento (até um quadro), e a onda continua";
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(Item.Cigarro, 'I', "V6") is not { } cigarro) return;
        if (EsperarItemNoChao(cigarro, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(Criterio, Falhou, "o cigarro não parou no chão em 4 s");
            return;
        }
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(cigarro, noChao, SolturaNoCentroDele(cigarro, noChao), out string recusa))
        {
            Registrar(Criterio, Inconclusivo, recusa);
            return;
        }
        EventoBuzzy? uso = Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000);
        if (uso is null)
        {
            Registrar(Criterio, Falhou, "o cigarro solto sobre ele não começou o uso (ITEM_DRAG_END→USING)");
            return;
        }
        long marcaPress = LogDoBuzzy.Marca();
        Nativo.POINT corpo;
        var latencia = new Stopwatch();
        try
        {
            _inj.EsperarIntervaloDeCliqueDuplo();
            corpo = PontoDoCorpo();
            _inj.Pressionar(corpo.X, corpo.Y, _hBuzzy);
            latencia.Start();
        }
        catch (CliqueRecusado e)
        {
            Registrar(Criterio, Inconclusivo, $"o PRESS no meio do uso foi recusado: {e.Message}");
            return;
        }
        EventoBuzzy? press = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "NUCLEO" && e["evento"] == "Press", 1500);
        double ms = latencia.Elapsed.TotalMilliseconds;
        _inj.SoltarEsquerdo(corpo.X, corpo.Y);
        EventoBuzzy? clique = Nucleo(marcaPress, "Click", "Pressed", "Reacting", 2000);
        // A onda (a do cigarro) começou ao soltar o item; o PRESS não a cancela, e o próximo disparo vem depois dele.
        EventoBuzzy? disparou = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "ONDA" && e["disparada"] == "sim", 6000);
        bool cancelada = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "ONDA" && e["cancelada"] == "sim");
        bool ok = press?["de"] == "Using" && press["para"] == "Pressed" && clique is not null && disparou is not null && !cancelada;
        Registrar(Criterio, ok,
            $"PRESS SINTÉTICO no ponto {corpo} no meio do uso: transição {press?["de"] ?? "?"}→{press?["para"] ?? "?"} no próprio evento PRESS, vista no log {ms:0} ms depois (o log é lido a cada 100 ms); "
            + $"soltar virou clique={clique is not null}; onda cancelada={cancelada}; o próximo disparo da onda veio depois do PRESS={disparou is not null}");
    }

    /// <summary>V5: solta longe dele e no ar, a banana cai de onde foi solta, sem uso. Devolve a banana, que fica no chão para o V7.</summary>
    private ItemVisto? V5SoltarLonge()
    {
        const string Criterio = "V5 — soltar longe: o item cai de onde foi solto, sem uso";
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(Item.Banana, 'B', "V5") is not { } banana) return null;
        if (EsperarItemNoChao(banana, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(Criterio, Falhou, "a banana não parou no chão em 4 s");
            return banana;
        }
        Nativo.RECT corpo = Nativo.Retangulo(_hBuzzy);
        Nativo.POINT de = banana.Opaco(noChao);
        int largura = noChao.Largura, ancoraX = noChao.Left + largura / 2, pegadaX = de.X - ancoraX;
        int lado = noChao.Centro.X >= corpo.Centro.X ? 1 : -1;
        int x = Math.Clamp(de.X + lado * 300, _areaUtilPrincipal.Left + largura + pegadaX, _areaUtilPrincipal.Right - largura + pegadaX);
        var ate = new Nativo.POINT(x, de.Y - 250);
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(banana, noChao, ate, out string recusa))
        {
            Registrar(Criterio, Inconclusivo, recusa);
            return banana;
        }
        EventoBuzzy? solto = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["solto"] == banana.IdNoLog, 2000);
        Nativo.RECT? final = EsperarItemNoChao(banana, marca, _areaUtilPrincipal, 4000);
        int esquerda = x - pegadaX - largura / 2;
        bool naColuna = final is { } f && f.Left == esquerda && f.Bottom == _areaUtilPrincipal.Bottom;
        bool semUso = !LogDoBuzzy.Desde(marca).Any(e => e.Chave == "NUCLEO" && e["para"] == "Using");
        Registrar(Criterio, solto?["sobre"] == "nao" && solto["usado"] == "nao" && naColuna && semUso,
            $"solto em {ate}, 250 px acima e longe dele: sobre ele={solto?["sobre"] ?? "?"}, usado={solto?["usado"] ?? "?"}; caiu até o chão na coluna em que foi solto={naColuna} "
            + $"(janela {final?.ToString() ?? "não parou"}, esperada à esquerda em x={esquerda}, pés no chão {_areaUtilPrincipal.Bottom}); nenhum uso={semUso}");
        return banana;
    }

    /// <summary>V7: com a banana do V5 no chão, mais seis itens: o sétimo tira o mais antigo. Devolve os seis que ficam.</summary>
    private List<ItemVisto> V7SetimoItem(ItemVisto? primeiro)
    {
        const string Criterio = "V7 — sétimo item: o mais antigo sai";
        var itens = new List<ItemVisto>();
        if (primeiro is not null && Nativo.IsWindow(primeiro.Hwnd)) itens.Add(primeiro);
        else if (InvocarItem(Item.Banana, 'B', "V7") is { } outra) itens.Add(outra);
        long marca = LogDoBuzzy.Marca();
        foreach ((Item item, char tecla) in new[] { (Item.Agua, 'G'), (Item.Cerveja, 'C'), (Item.Cafe, 'F'), (Item.Energetico, 'N'), (Item.Cogumelo, 'U'), (Item.Bala, 'A') })
        {
            if (InvocarItem(item, tecla, "V7") is { } novo) itens.Add(novo);
        }
        if (itens.Count != 7)
        {
            Registrar(Criterio, Falhou, $"só {itens.Count} de 7 itens foram invocados");
            return [.. itens.Where(i => Nativo.IsWindow(i.Hwnd))];
        }
        ItemVisto maisAntigo = itens[0];
        EventoBuzzy? removido = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["removido"] == maisAntigo.IdNoLog, 3000);
        Thread.Sleep(300);
        bool fechou = !Nativo.IsWindow(maisAntigo.Hwnd);
        List<ItemVisto> ficam = [.. itens.Skip(1)];
        int vivas = ficam.Count(i => Nativo.IsWindow(i.Hwnd) && Nativo.IsWindowVisible(i.Hwnd));
        Registrar(Criterio, removido?["motivo"] == "Substituido" && fechou && vivas == 6,
            $"o mais antigo ({maisAntigo.Nome}, Id {maisAntigo.Id}) saiu com o motivo {removido?["motivo"] ?? "nenhum"}; a janela dele fechou={fechou}; os outros seis à vista={vivas}/6");
        return ficam;
    }

    /// <summary>V15: o botão direito num item abre o mesmo menu; "Recolher itens" (I e R) fecha todas as janelas.</summary>
    private void V15BotaoDireitoNumItem(List<ItemVisto> itens)
    {
        const string Criterio = "V15 — botão direito num item abre o menu";
        const string CriterioRecolher = "Recolher itens (I e R): todas as janelas dos itens fecham";
        if (itens.Count == 0)
        {
            Registrar(Criterio, Inconclusivo, "nenhum item na tela");
            return;
        }
        ItemVisto alvo = itens[^1];
        if (EsperarItemNoChao(alvo, _inicioLogBuzzy, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(Criterio, Inconclusivo, $"{alvo.Nome} não parou no chão");
            return;
        }
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuPeloCliqueDireito(alvo.Opaco(noChao), alvo.Hwnd, "V15", (Vk('I'), "I (Itens)"), (Vk('R'), "R (Recolher itens)"));
        EventoBuzzy? pedido = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["menuPedido"] == alvo.IdNoLog, 1000);
        EventoBuzzy? aberto = LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e["aberto"] == "personagem", 1000);
        Registrar(Criterio, m.Recusado ? Inconclusivo : m.Abriu && pedido is not null && aberto is not null ? Ok : Falhou,
            $"clique direito SINTÉTICO no ponto opaco de {alvo.Nome}: ITEM|menuPedido={pedido is not null}; o mesmo menu do personagem (MENU|aberto=personagem)={aberto is not null}; {DescreverMenu(m)}");
        Thread.Sleep(300);
        int recolhidos = itens.Count(i => LogDoBuzzy.Desde(marca).Any(e => e.Chave == "ITEM" && e["removido"] == i.IdNoLog && e["motivo"] == "Recolhido"));
        int vivas = itens.Count(i => Nativo.IsWindow(i.Hwnd));
        Registrar(CriterioRecolher, m.Fechado?["fechado"] == "RecolherItens" && recolhidos == itens.Count && vivas == 0,
            $"menu fechado com {m.Fechado?["fechado"] ?? "nada"}; recolhidos {recolhidos}/{itens.Count}; janelas de item ainda vivas={vivas}");
    }

    /// <summary>X1: com dois monitores, uma banana arrastada até o outro e solta no ar cai até o chão dele. Com um monitor só, N/A.</summary>
    private void X1OutroMonitor()
    {
        const string Criterio = "X1 [HW] — arrastar um item até o outro monitor e soltar no ar: ele cai até o chão de lá";
        List<Nativo.MonitorLido> monitores = Nativo.Monitores();
        if (monitores.FirstOrDefault(m => !m.Principal) is not { } sec)
        {
            Registrar(Criterio, NaoAplicavel, $"EnumDisplayMonitors encontrou {monitores.Count} monitor(es), nenhum secundário");
            return;
        }
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(Item.Banana, 'B', "X1") is not { } banana) return;
        if (EsperarItemNoChao(banana, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(Criterio, Falhou, "a banana não parou no chão do principal em 4 s");
            return;
        }
        Nativo.RECT area = sec.Info.rcWork;
        Nativo.POINT de = banana.Opaco(noChao);
        var ate = new Nativo.POINT((area.Left + area.Right) / 2, area.Bottom - 250);
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(banana, noChao, ate, out string recusa, passos: 40))
        {
            Registrar(Criterio, Inconclusivo, recusa);
            return;
        }
        EventoBuzzy? solto = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["solto"] == banana.IdNoLog, 2000);
        Nativo.RECT? final = EsperarItemNoChao(banana, marca, area, 4000);
        EventoBuzzy? parado = LogDoBuzzy.Desde(marca).LastOrDefault(e => e.Chave == "ITEM" && e["movido"] == banana.IdNoLog && e["parado"] == "sim");
        bool noChaoDele = final is { } f && f.Bottom == area.Bottom && area.Contem(f);
        // A linha ITEM leva só a chave opaca do monitor (DEC-030); a do secundário vem da linha TOPOLOGIA.
        string? chaveDoSecundario = ChaveDoMonitorNoLog(sec.Info.szDevice);
        bool monitorCerto = parado is null || (chaveDoSecundario is not null && parado["monitor"] == chaveDoSecundario);
        Registrar(Criterio, solto?["sobre"] == "nao" && noChaoDele && monitorCerto,
            $"de {de} no principal até {ate} em {sec.Info.szDevice} ({sec.Dpi} DPI; principal {_dpiPrincipal}); solto sobre ele={solto?["sobre"] ?? "?"}; "
            + $"janela {final?.ToString() ?? "não parou"} no chão da área útil {area}={noChaoDele}; monitor no log={parado?["monitor"] ?? "sem a linha parado=sim"} "
            + $"(chave de {sec.Info.szDevice} na topologia: {chaveDoSecundario ?? "não registrada"})");
        if (sec.Dpi == _dpiPrincipal) _naoExercitados.Add("X1 entre monitores de escalas diferentes [HW]");
    }

    /// <summary>V10: com itens na tela, esconder pelo menu esconde as janelas deles; mostrar (pela segunda instância) as mostra de novo, logo abaixo do personagem.</summary>
    private void V10EsconderEMostrar()
    {
        const string Criterio = "V10 — esconder e mostrar: as janelas dos itens acompanham";
        foreach ((Item item, char tecla) in new[] { (Item.Agua, 'G'), (Item.Cafe, 'F') }) _ = InvocarItem(item, tecla, "V10");
        Thread.Sleep(1500);
        List<(int Id, nint Hwnd)> naTela = ItensNaTelaPeloLog();
        if (naTela.Count == 0)
        {
            Registrar(Criterio, Inconclusivo, "nenhum item na tela para acompanhar");
            return;
        }
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuNoPersonagem("V10, esconder", (Vk('E'), "E (Esconder Buzzy)"));
        EventoBuzzy? escondeu = LogDoBuzzy.Esperar(marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000);
        Thread.Sleep(300);
        int escondidas = naTela.Count(i => LogDoBuzzy.Desde(marca).Any(e => e.Chave == "ITEM" && e["escondido"] == Id(i.Id)) && !Nativo.IsWindowVisible(i.Hwnd) && Nativo.IsWindow(i.Hwnd));
        bool personagemEscondido = !Nativo.IsWindowVisible(_hBuzzy);

        long marcaMostrar = LogDoBuzzy.Marca();
        int marcaR = _logReceptor.Contar();
        (bool saiu, int codigo) = SegundaInstanciaQueMostra();
        LogDoBuzzy.Esperar(marcaMostrar, e => e.Chave == "VISIVEL" && e["visivel"] == "sim", 3000);
        Thread.Sleep(500);
        List<EventoBuzzy> depois = LogDoBuzzy.Desde(marcaMostrar);
        int mostradas = naTela.Count(i => depois.Any(e => e.Chave == "ITEM" && e["mostrado"] == Id(i.Id) && e["criada"] == "nao") && Nativo.IsWindowVisible(i.Hwnd));
        int abaixo = naTela.Count(i => Nativo.PosicoesAbaixo(_hBuzzy, i.Hwnd) >= 1);
        (bool frente, bool desativou) = FocoNoReceptor(marcaR);
        bool ok = m.Fechado?["fechado"] == "AlternarVisibilidade" && escondeu is not null && personagemEscondido && escondidas == naTela.Count
            && saiu && codigo == 0 && mostradas == naTela.Count && abaixo == naTela.Count && frente && !desativou;
        Registrar(Criterio, m.Recusado ? Inconclusivo : ok ? Ok : Falhou,
            $"{naTela.Count} item(ns) na tela; esconder pelo menu: {DescreverMenu(m)}; personagem escondido={personagemEscondido}; janelas de item escondidas (e não fechadas) {escondidas}/{naTela.Count}; "
            + $"segunda instância saiu sozinha={saiu} código={codigo}; janelas mostradas de novo, as mesmas, {mostradas}/{naTela.Count}; abaixo do personagem na ordem Z {abaixo}/{naTela.Count}; "
            + $"reaparecer não tirou o foco: receptor na frente={frente}, desativado={desativou}");
    }

    /// <summary>
    /// V13, em medição curta: com a vodka usada (onda de bebedeira), pausado e parado, 60 s de repouso com a CPU média até
    /// 0,1% de um núcleo e o relógio desligado, mesmo com os disparos únicos da onda trocando a cara.
    /// </summary>
    private void V13RepousoComAOndaDaVodka()
    {
        string criterio = $"V13 (medição curta, {SegundosDoRepousoComOnda} s; os 10 minutos ficam em tools\\medir-desempenho.ps1 -Modo onda) — repouso pausado com a vodka ativa: CPU média até 0,1%, relógio desligado";
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(Item.Vodka, 'V', "V13") is not { } vodka) return;
        if (EsperarItemNoChao(vodka, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao)
        {
            Registrar(criterio, Falhou, "a vodka não parou no chão em 4 s");
            return;
        }
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(vodka, noChao, SolturaNoCentroDele(vodka, noChao), out string recusa))
        {
            Registrar(criterio, Inconclusivo, recusa);
            return;
        }
        EventoBuzzy? uso = Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000);
        EventoBuzzy? fim = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["de"] == "Using", 6000);
        EventoBuzzy? desligou = LogDoBuzzy.Esperar(marca, e => e.Chave == "RELOGIO" && e["ligado"] == "nao", 6000);
        if (uso is null || fim is null || desligou is null)
        {
            Registrar(criterio, Falhou, $"a vodka não foi usada até o fim: USING={uso is not null}; fim={fim is not null}; relógio desligado={desligou is not null}");
            return;
        }
        Thread.Sleep(1000);
        long marcaRepouso = LogDoBuzzy.Marca();
        _buzzy!.Refresh();
        TimeSpan cpuAntes = _buzzy.TotalProcessorTime;
        var relogio = Stopwatch.StartNew();
        Thread.Sleep(SegundosDoRepousoComOnda * 1000);
        _buzzy.Refresh();
        double cpu = (_buzzy.TotalProcessorTime - cpuAntes).TotalMilliseconds / relogio.Elapsed.TotalMilliseconds * 100;
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marcaRepouso);
        bool relogioLigou = ev.Any(e => e.Chave == "RELOGIO" && e["ligado"] == "sim");
        int disparos = ev.Count(e => e.Chave == "ONDA" && e["disparada"] == "sim");
        string[] viradas = [.. ev.Where(e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer").Select(e => e["regra"])];
        Registrar(criterio, cpu <= 0.1 && !relogioLigou && disparos >= 1,
            $"CPU do Buzzy em {relogio.Elapsed.TotalSeconds:0} s: {cpu:0.000}% de um núcleo (Q-08: média até 0,1%); relógio ligou no repouso={relogioLigou}; "
            + $"disparos únicos da onda no repouso={disparos} ({string.Join("; ", viradas)}): pausado, só a cara muda");
        _naoExercitados.Add("V13 com 10 minutos de repouso (tools\\medir-desempenho.ps1 -Modo onda)");
    }

    /// <summary>V14: "Sair" pelo menu com itens na tela: código 0, as janelas dos itens fechadas pela raiz e nenhuma viva.</summary>
    private void V14SairComItensNaTela()
    {
        const string Criterio = "V14 — sair com itens na tela: código 0 e nenhuma janela viva";
        List<(int Id, nint Hwnd)> naTela = ItensNaTelaPeloLog();
        if (naTela.Count == 0 && InvocarItem(Item.Banana, 'B', "V14") is not null) naTela = ItensNaTelaPeloLog();
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuNoPersonagem("V14, sair", (Vk('S'), "S (Sair)"));
        bool saiu = _buzzy!.WaitForExit(5000);
        int codigo = saiu ? _buzzy.ExitCode : -1;
        _buzzyEncerrado = saiu;
        Thread.Sleep(300);
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(marca);
        EventoBuzzy? fechadas = ev.LastOrDefault(e => e.Chave == "ITEM" && e.Campos.ContainsKey("fechadas"));
        bool fimZero = ev.Any(e => e.Chave == "FIM" && e["codigo"] == "0");
        int vivas = naTela.Count(i => Nativo.IsWindow(i.Hwnd)) + (Nativo.IsWindow(_hBuzzy) ? 1 : 0);
        bool ok = m.Fechado?["fechado"] == "Sair" && saiu && codigo == 0 && fimZero && fechadas?["fechadas"] == naTela.Count.ToString(CultureInfo.InvariantCulture)
            && vivas == 0 && ContarBuzzys() == 0;
        Registrar(Criterio, m.Recusado ? Inconclusivo : ok ? Ok : Falhou,
            $"{naTela.Count} item(ns) na tela; {DescreverMenu(m)}; saiu={saiu} código={codigo}; FIM com código 0={fimZero}; janelas de item fechadas pela raiz={fechadas?["fechadas"] ?? "sem a linha"}; "
            + $"janelas do Buzzy vivas depois={vivas}; processos Buzzy={ContarBuzzys()}");
    }

    /// <summary>
    /// V16: a emoção dominante escolhida pelo menu (D e F, Feliz) vai para o settings.json do perfil de teste e volta ao
    /// reabrir o Buzzy sem limpar a pasta do perfil (persistência, passo P7 da Fase 5): "Sair" pelo menu grava na hora, o
    /// arquivo do perfil, lido pelo esquema do núcleo, é válido e tem a emoção; reaberto, o Buzzy lê o principal, o
    /// personagem parado começa com a cara feliz e o menu a mostra marcada.
    /// </summary>
    private void V16EmocaoGravadaERestauradaAoReabrir()
    {
        const string Criterio = "V16 — emoção gravada no perfil de teste e restaurada ao reabrir";
        AbrirBuzzyDoTamagotchi(_sementeDoTamagotchi, pausado: true, "V16");
        try
        {
            MenuOperado feliz = MenuNoPersonagem("V16, Feliz", (Vk('D'), "D (Emoção dominante)"), (Vk('F'), "F (Feliz)"));
            long marcaSair = LogDoBuzzy.Marca();
            MenuOperado sair = MenuNoPersonagem("V16, sair", (Vk('S'), "S (Sair)"));
            bool saiu = _buzzy!.WaitForExit(5000);
            _buzzyEncerrado = saiu;
            EventoBuzzy? gravou = LogDoBuzzy.Desde(marcaSair).LastOrDefault(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("gravado") && e["motivo"] == "CmdExit");
            (bool valido, Expressao? noArquivo) = EmocaoNoPerfil();
            if (feliz.Recusado || sair.Recusado || !saiu)
            {
                Registrar(Criterio, feliz.Recusado || sair.Recusado ? Inconclusivo : Falhou,
                    $"escolher Feliz: {DescreverMenu(feliz)}; sair: {DescreverMenu(sair)}; saiu={saiu}");
                return;
            }

            long marcaReabrir = LogDoBuzzy.Marca();
            AbrirBuzzyDoTamagotchi(_sementeDoTamagotchi, pausado: true, "V16, reaberto sem limpar o perfil", limpar: false);
            EventoBuzzy? lido = LogDoBuzzy.Esperar(marcaReabrir, e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"), 3000);
            EventoBuzzy? cara = LogDoBuzzy.Esperar(marcaReabrir, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["expressao"] == "feliz", 3000);
            long marcaMenu = LogDoBuzzy.Marca();
            MenuOperado conferencia = MenuNoPersonagem("V16, conferência", (VK_ESCAPE, "Esc (fechar sem escolher)"));
            EventoBuzzy? marcada = PrimeiroDesde(marcaMenu, e => e.Chave == "MENU" && e["exibindo"] == "sim");
            bool ok = feliz.Fechado?["fechado"] == "Emocao" && feliz.Fechado?["argumento"] == "Feliz" && sair.Fechado?["fechado"] == "Sair"
                && gravou?["gravado"] == "sim" && valido && noArquivo == Expressao.Feliz
                && lido?["lido"] == "principal" && cara is not null && marcada?["emocaoMarcada"] == "Feliz" && conferencia.Fechado?["fechado"] == "Nenhum";
            Registrar(Criterio, conferencia.Recusado ? Inconclusivo : ok ? Ok : Falhou,
                $"escolher Feliz: {DescreverMenu(feliz)}; sair: {DescreverMenu(sair)}, gravado na saída={gravou?["gravado"] ?? "sem a linha"}; "
                + $"settings.json do perfil válido={valido}, emoção={noArquivo?.ToString() ?? "automática"}; reaberto: lido={lido?["lido"] ?? "sem a linha"}, "
                + $"parado com a cara feliz={cara is not null}, marcada no menu={marcada?["emocaoMarcada"] ?? "?"}");
        }
        finally
        {
            FecharBuzzy("V16");
        }
    }

    /// <summary>
    /// A emoção dominante no settings.json do perfil da verificação, lido pelo esquema do núcleo (a pasta vem da regra do
    /// próprio Buzzy, <see cref="PastaDeDados.DoPerfilDeTeste"/>); falso sem o arquivo ou com ele inválido. Só o perfil de
    /// teste: o arquivo real do usuário nunca é lido.
    /// </summary>
    private static (bool Valido, Expressao? Emocao) EmocaoNoPerfil()
    {
        string? pasta = PastaDeDados.DoPerfilDeTeste(PerfilDaVerificacao.Nome);
        string? arquivo = pasta is null ? null : Path.Combine(pasta, "settings.json");
        if (arquivo is null || !File.Exists(arquivo)) return (false, null);
        LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(File.ReadAllBytes(arquivo));
        return (lida.Situacao == SituacaoDaLeitura.Valida, lida.Configuracoes.Preferencias.EmocaoDominante);
    }

    // ------------------------------------------------------------------ parte 2: preso

    /// <summary>
    /// V9: preso pelo usuário no cipó e na parede (DEC-024) e escondido na borda (DEC-025), um item solto sobre ele é usado e,
    /// no fim do uso, ele volta ao mesmo lugar. Pausado, quem não estivesse preso desceria da parede ou se soltaria do cipó
    /// (regra da calma): ficar lá é a prova de que continua preso.
    /// </summary>
    private void V9PresoNoCipoNaParedeENoEsconderijo(Topologia topologia)
    {
        AbrirBuzzyDoTamagotchi(_sementeDoTamagotchi, pausado: true, "V9");
        try
        {
            MonitorDoDesktop principal = topologia.Principal;
            Superficies sup = Superficies.Do(topologia, principal, SementesDoTamagotchi.Configuracao.Tamanho.ParaPixels(principal.Dpi));
            Nativo.RECT area = _areaUtilPrincipal;

            bool noCipo = ArrastarAncoraPara(new PontoPx((sup.Esquerda + sup.Direita) / 2, sup.Teto + 60), "agarra o cipó");
            (bool cipo, string resumoCipo) = noCipo ? UsarNoMesmoLugar(Item.Banana, 'B', "Hanging", q => q.Top == area.Top) : (false, "não agarrou o cipó");
            Registrar("V9 — preso no cipó, solto um item nele: usa e volta preso ao mesmo lugar", cipo, resumoCipo);

            bool naParede = ArrastarAncoraPara(new PontoPx(sup.Direita - 30, (sup.Teto + sup.Chao) / 2), "fica grudado na parede");
            (bool parede, string resumoParede) = naParede ? UsarNoMesmoLugar(Item.Cerveja, 'C', "Climbing", q => q.Right == area.Right) : (false, "não grudou na parede");
            Registrar("V9 — preso na parede, solto um item nele: usa e volta preso ao mesmo lugar", parede, resumoParede);

            ArrastarAncoraPara(new PontoPx((sup.Esquerda + sup.Direita) / 2, sup.Chao), null);
            Thread.Sleep(500);
            bool escondido = CliqueDuploEspera("Peeking");
            (bool esconderijo, string resumoEsconderijo) = escondido ? UsarNoMesmoLugar(Item.Cafe, 'F', "Peeking", q => q.Bottom == area.Bottom) : (false, "o clique duplo não o escondeu");
            Registrar("V9 — escondido na borda, solto um item nele: usa e volta ao esconderijo, no mesmo lugar", esconderijo, resumoEsconderijo);
        }
        finally
        {
            FecharBuzzy("V9");
        }
    }

    /// <summary>
    /// Invoca um item, espera ele cair, arrasta até o centro do personagem e solta; confere o uso a partir de
    /// <paramref name="estado"/>, a volta ao mesmo estado e ao mesmo retângulo no fim do uso, e que ele fica lá 5 s.
    /// </summary>
    private (bool Ok, string Resumo) UsarNoMesmoLugar(Item item, char tecla, string estado, Func<Nativo.RECT, bool> encostado)
    {
        Thread.Sleep(300);
        Nativo.RECT antes = Nativo.Retangulo(_hBuzzy);
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(item, tecla, "V9") is not { } visto) return (false, $"{item} não foi invocado");
        if (EsperarItemNoChao(visto, marcaItem, _areaUtilPrincipal, 5000) is not { } noChao) return (false, $"{item} não parou no chão");
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(visto, noChao, SolturaNoCentroDele(visto, noChao), out string recusa, passos: 40)) return (false, recusa);
        EventoBuzzy? uso = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000);
        EventoBuzzy? volta = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Settling" && e["para"] == estado, 6000);
        Thread.Sleep(300);
        Nativo.RECT depois = Nativo.Retangulo(_hBuzzy);
        (bool ficou, string resumo) = Observar(estado, encostado, TimeSpan.FromSeconds(5));
        bool ok = uso?["de"] == estado && volta is not null && depois.Equals(antes) && ficou;
        return (ok, $"{item} solto nele: USING a partir de {uso?["de"] ?? "nada"} ({uso?["regra"] ?? "-"}); fim do uso de volta a {estado}={volta is not null} ({volta?["regra"] ?? "-"}); "
            + $"no mesmo retângulo={depois.Equals(antes)} (antes {antes}, depois {depois}); pausado por 5 s, ficou={ficou} ({resumo})");
    }

    // ------------------------------------------------------------------ parte 3: com a agenda ligada

    /// <summary>V8: com a agenda ligada, segurar um item com ele andando: WALKING vira IDLE, nenhuma agenda durante o gesto e a próxima em 3 s ou mais.</summary>
    private void V8SegurarUmItemComAAgendaLigada(Topologia topologia)
    {
        const string Criterio = "V8 — com a agenda ligada, segurar um item: WALKING vira IDLE, nenhuma agenda durante o gesto, e a próxima vem em 3 s ou mais";
        if (SementesDoTamagotchi.SegurarItemAndando(topologia) is not { } p)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente em que ele anda logo depois de a banana cair, longe dela");
            return;
        }
        _rel.Linha($"   V8: {p.Resumo}");
        AbrirBuzzyDoTamagotchi(p.Semente, pausado: false, "V8");
        try
        {
            long marcaItem = LogDoBuzzy.Marca();
            if (InvocarItem(Item.Banana, 'B', "V8") is not { } banana) return;
            if (EsperarItemNoChao(banana, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao)
            {
                Registrar(Criterio, Falhou, "a banana não parou no chão em 4 s");
                return;
            }
            bool decidiuAntes = LogDoBuzzy.Desde(_inicioLogBuzzy).Any(e => e.Chave == "AGENDA" && e.Campos.ContainsKey("disparo"));
            EventoBuzzy? andou = LogDoBuzzy.Esperar(_inicioLogBuzzy, e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == "Walking",
                (int)p.PrimeiraDecisao.TotalMilliseconds + 5000, () => _buzzy is { HasExited: true });
            if (andou is null || decidiuAntes)
            {
                Registrar(Criterio, Falhou, $"semente {p.Semente}: a caminhada prevista em {p.PrimeiraDecisao.TotalSeconds:0.0} s não começou (decisão antes de a banana cair={decidiuAntes})");
                return;
            }
            long marca = LogDoBuzzy.Marca();
            Nativo.POINT opaco = banana.Opaco(noChao);
            try
            {
                _inj.Pressionar(opaco.X, opaco.Y, banana.Hwnd);
            }
            catch (CliqueRecusado e)
            {
                Registrar(Criterio, Inconclusivo, $"o botão pressionado na banana foi recusado: {e.Message}");
                return;
            }
            EventoBuzzy? parou = Nucleo(marca, "ItemPress", "Walking", "Idle", 2000);
            Thread.Sleep(1500);
            List<EventoBuzzy> durante = LogDoBuzzy.Desde(marca);
            int agendas = durante.Count(e => e.Chave == "AGENDA" && (e.Campos.ContainsKey("atrasoMs") || e.Campos.ContainsKey("disparo")));
            // Andando, nenhuma decisão fica pendente (ela é consumida ao começar a andar): o ITEM_PRESS não tem o que
            // cancelar. Uma decisão pendente que o ITEM_PRESS cancelasse apareceria aqui; só informa.
            bool cancelou = durante.Any(e => e.Chave == "AGENDA" && e["cancelada"] == "sim");
            long marcaSoltar = LogDoBuzzy.Marca();
            _inj.SoltarEsquerdo(opaco.X, opaco.Y);
            EventoBuzzy? proxima = LogDoBuzzy.Esperar(marcaSoltar, e => e.Chave == "AGENDA" && e.Campos.ContainsKey("atrasoMs"), 2000);
            long atraso = proxima is not null && long.TryParse(proxima["atrasoMs"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long a) ? a : -1;
            Registrar(Criterio, parou is not null && agendas == 0 && atraso >= 3000,
                $"semente {p.Semente}; ITEM_PRESS com ele andando: WALKING→IDLE={parou is not null} ({parou?["regra"] ?? "-"}); "
                + $"agendamentos ou disparos durante o gesto de 1,5 s={agendas} (decisão pendente cancelada no ITEM_PRESS={cancelou}; andando, não há nenhuma); "
                + $"depois de soltar, próxima decisão em {atraso} ms (no mínimo 3000)");
        }
        finally
        {
            FecharBuzzy("V8");
        }
    }

    /// <summary>V11: com a onda da cocaína e com a do baseado, a velocidade da caminhada é a da fase da onda (±10%), e a cocaína anda mais rápido.</summary>
    private void V11CocainaContraBaseado(Topologia topologia)
    {
        const string Criterio = "V11 — cocaína contra baseado: velocidades de caminhada da onda, com tolerância de ±10%";
        var medidas = new List<string>();
        var velocidades = new Dictionary<Item, double>();
        bool todasNaFaixa = true, inconclusivo = false;
        foreach ((Item item, char tecla) in new[] { (Item.Cocaina, 'O'), (Item.Baseado, 'S') })
        {
            if (SementesDoTamagotchi.AndarComOnda(topologia, item) is not { } p)
            {
                medidas.Add($"{item}: nenhuma semente em que ele anda com a onda");
                inconclusivo = true;
                continue;
            }
            _rel.Linha($"   V11 ({item}): {p.Resumo}");
            AbrirBuzzyDoTamagotchi(p.Semente, pausado: false, $"V11, {item}");
            try
            {
                (double? medida, double esperada, string detalhe) = AndarComAOndaDe(item, tecla, p);
                medidas.Add($"{item}: {detalhe}");
                if (medida is not { } m)
                {
                    inconclusivo = true;
                    continue;
                }
                velocidades[item] = m;
                todasNaFaixa &= Math.Abs(m - esperada) <= esperada * 0.10;
            }
            finally
            {
                FecharBuzzy($"V11 {item}");
            }
        }
        bool cocainaMaisRapida = velocidades.TryGetValue(Item.Cocaina, out double c) && velocidades.TryGetValue(Item.Baseado, out double b) && c > b;
        Registrar(Criterio, inconclusivo ? Inconclusivo : todasNaFaixa && cocainaMaisRapida ? Ok : Falhou,
            string.Join("; ", medidas) + $"; a cocaína anda mais rápido que o baseado={cocainaMaisRapida}");
    }

    /// <summary>
    /// Invoca o item, solta nele, espera a caminhada com a onda e mede a velocidade da janela. A esperada é a física (90
    /// DIP/s) na escala do monitor, vezes a velocidade da fase da onda em que ele está, lida do log (a última virada).
    /// </summary>
    private (double? Medida, double Esperada, string Detalhe) AndarComAOndaDe(Item item, char tecla, Previsao p)
    {
        long marcaItem = LogDoBuzzy.Marca();
        if (InvocarItem(item, tecla, "V11") is not { } visto) return (null, 0, "não foi invocado");
        if (EsperarItemNoChao(visto, marcaItem, _areaUtilPrincipal, 4000) is not { } noChao) return (null, 0, "não parou no chão");
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(visto, noChao, SolturaNoCentroDele(visto, noChao), out string recusa)) return (null, 0, recusa);
        if (Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000) is null) return (null, 0, "solto nele, não começou o uso");
        EventoBuzzy? andou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == "Walking", 60000, () => _buzzy is { HasExited: true });
        if (andou is null) return (null, 0, $"não andou em 60 s (previsto: {p.Resumo})");
        (string onda, int percentual) = OndaNoLog(marca, item);
        double esperada = SementesDoTamagotchi.Configuracao.Fisica.VelocidadeAndando * _dpiPrincipal / 96.0 * percentual / 100;
        (double? medida, string amostras) = MedirCaminhada(TimeSpan.FromMilliseconds(1500));
        string texto = $"onda {onda} (prevista {p.Onda}), {percentual}%: esperada {esperada:0.0} px/s, medida {(medida is { } m ? m.ToString("0.0", CultureInfo.InvariantCulture) : "?")} px/s ({amostras})";
        return (medida, esperada, texto);
    }

    /// <summary>V12: no pico do nível 3 da bebedeira (vodka e cerveja), ele recua na caminhada (cambaleio a 120%) e fica sempre no chão.</summary>
    private void V12BebedeiraNoNivel3(Topologia topologia)
    {
        const string Criterio = "V12 — nível 3 de bebedeira: recua na caminhada, sempre no chão";
        if (SementesDoTamagotchi.CambalearNoNivel3(topologia) is not { } p)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente em que ele anda no pico do nível 3");
            return;
        }
        _rel.Linha($"   V12: {p.Resumo}");
        AbrirBuzzyDoTamagotchi(p.Semente, pausado: false, "V12");
        try
        {
            long marcaItens = LogDoBuzzy.Marca();
            if (InvocarItem(Item.Vodka, 'V', "V12") is not { } vodka || InvocarItem(Item.Cerveja, 'C', "V12") is not { } cerveja) return;
            if (EsperarItemNoChao(vodka, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaVodka
                || EsperarItemNoChao(cerveja, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaCerveja)
            {
                Registrar(Criterio, Falhou, "os itens não pararam no chão em 4 s");
                return;
            }
            long marca = LogDoBuzzy.Marca();
            if (!ArrastarItem(vodka, chaoDaVodka, SolturaNoCentroDele(vodka, chaoDaVodka), out string recusa))
            {
                Registrar(Criterio, Inconclusivo, recusa);
                return;
            }
            if (Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000) is null)
            {
                Registrar(Criterio, Falhou, "a vodka solta nele não começou o uso");
                return;
            }
            // Durante o uso da vodka, a cerveja na mão, sobre ele; solta logo que o uso acaba (o atento pausa a agenda).
            Nativo.POINT soltura = SolturaNoCentroDele(cerveja, chaoDaCerveja);
            if (!ArrastarItem(cerveja, chaoDaCerveja, soltura, out recusa, soltar: false, esperarCliqueDuplo: false))
            {
                Registrar(Criterio, Inconclusivo, recusa);
                return;
            }
            EventoBuzzy? acabou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Settling" && e["para"] == "Idle", 5000);
            long marcaCerveja = LogDoBuzzy.Marca();
            _inj.SoltarEsquerdo(soltura.X, soltura.Y);
            if (acabou is null || Nucleo(marcaCerveja, "ItemDragEnd", "Idle", "Using", 3000) is null)
            {
                Registrar(Criterio, Falhou, $"o uso da vodka acabou com a cerveja na mão={acabou is not null}; a cerveja solta nele não começou o uso");
                return;
            }
            EventoBuzzy? andou = LogDoBuzzy.Esperar(marcaCerveja, e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == "Walking", 60000, () => _buzzy is { HasExited: true });
            if (andou is null)
            {
                Registrar(Criterio, Falhou, $"não andou em 60 s (previsto: {p.Resumo})");
                return;
            }
            (string onda, _) = OndaNoLog(marca, Item.Vodka);
            Cambaleio c = ObservarCambaleio(marcaCerveja, TimeSpan.FromSeconds(6));
            bool pico3 = onda == "Bebado/Pico/3";
            string detalhe = $"semente {p.Semente}; onda na caminhada {onda} (prevista {p.Onda}); {c.Amostras} posições distintas em {c.Segundos:0.0} s andando no chão: "
                + $"recuos de 1 px contra o sentido={c.Recuos}; pés sempre no chão enquanto andava={c.SempreNoChao}; fim da observação: {c.Fim}";
            // Sem recuo visto, a condição não se estabeleceu: o recuo de cada volta do cambaleio soma menos de 1 px a
            // 96 DPI, e um quadro do compositor com dois passos do relógio o esconde da amostragem.
            Registrar(Criterio, !pico3 || !c.SempreNoChao ? Falhou : c.Recuos >= 1 ? Ok : Inconclusivo,
                detalhe + (c.Recuos == 0 ? " (nenhuma amostra pegou um recuo de 1 px inteiro)" : ""));
        }
        finally
        {
            FecharBuzzy("V12");
        }
    }

    /// <summary>
    /// Correção do núcleo (achado 4; DEC-022 e DEC-024): com a agenda ligada, ele escala uma lateral sozinho; um clique nele
    /// vira reação, e a acomodação o deixa agarrado à parede SEM estar preso; pausado pelo menu, ele desce até o chão, em vez
    /// de ficar na parede à espera de uma agenda que não decide.
    /// </summary>
    private void PausarComEleNaParedeSemEstarPreso(Topologia topologia)
    {
        const string Criterio = "Pausar com ele agarrado na parede sem estar preso: ele desce até o chão (regra da calma, achado 4 da revisão do núcleo)";
        if (SementesDoTamagotchi.AgarrarNaParedeSemEstarPreso(topologia) is not { } p)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente em que ele escala uma lateral sem foguete logo no começo");
            return;
        }
        _rel.Linha($"   parede sem estar preso: {p.Resumo}");
        AbrirBuzzyDoTamagotchi(p.Semente, pausado: false, "parede sem estar preso");
        try
        {
            EventoBuzzy? subiu = LogDoBuzzy.Esperar(_inicioLogBuzzy, e => e.Chave == "NUCLEO" && e["para"] == "Climbing", (int)p.PrimeiraDecisao.TotalMilliseconds + 25000, () => _buzzy is { HasExited: true });
            if (subiu is null)
            {
                Registrar(Criterio, Falhou, $"semente {p.Semente}: a escalada prevista não começou");
                return;
            }
            Nativo.RECT area = _areaUtilPrincipal;
            if (!EsperarAte(() => Nativo.Retangulo(_hBuzzy).Bottom <= area.Bottom - 150, 5000))
            {
                Registrar(Criterio, Inconclusivo, "não subiu 150 px em 5 s");
                return;
            }
            long marca = LogDoBuzzy.Marca();
            // Ele se move: o ponto do corpo é escolhido só depois da espera do clique duplo, firme na vertical (o vizinho a
            // 6 px acima e abaixo também é dele), e o injetor o reconfere antes do clique. Mesmo assim ele pode sair de baixo
            // do cursor entre a conferência e o clique; sem PRESS no log, tenta de novo, até 3 vezes.
            EventoBuzzy? apertou = null;
            int tentativas = 0;
            string? recusa = null;
            while (apertou is null && tentativas < 3)
            {
                tentativas++;
                long marcaDoClique = LogDoBuzzy.Marca();
                try
                {
                    _inj.EsperarIntervaloDeCliqueDuplo();
                    Nativo.POINT corpo = PontoFirmeDoCorpo();
                    _inj.CliqueEsquerdo(corpo.X, corpo.Y, _hBuzzy);
                }
                catch (CliqueRecusado e)
                {
                    recusa = e.Message;
                    continue;
                }
                apertou = LogDoBuzzy.Esperar(marcaDoClique, e => e.Chave == "NUCLEO" && e["evento"] == "Press" && e["para"] == "Pressed", 700);
            }
            if (apertou is null)
            {
                Registrar(Criterio, Inconclusivo, $"semente {p.Semente}: em {tentativas} tentativa(s), nenhum clique nele escalando chegou ao Buzzy"
                    + (recusa is null ? " (ele se move entre a conferência do ponto e o clique)" : $"; última recusa: {recusa}"));
                return;
            }
            EventoBuzzy? reagiu = Nucleo(marca, "Click", "Pressed", "Reacting", 2000);
            EventoBuzzy? agarrou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["de"] == "Settling" && e["para"] == "Climbing", 3000);
            Thread.Sleep(400);
            Nativo.RECT naParede = Nativo.Retangulo(_hBuzzy);
            bool encostado = naParede.Right == area.Right || naParede.Left == area.Left;
            // Pausar não muda o estado (o log do núcleo só traz transições): o que conta é o menu ter escolhido o item.
            long marcaPausa = LogDoBuzzy.Marca();
            MenuOperado m = MenuNoPersonagem("pausar na parede", (Vk('P'), "P (Pausar movimento)"));
            bool pausou = m.Fechado?["fechado"] == "AlternarMovimento";
            // Desce encostado na mesma lateral até os pés tocarem o chão.
            int amostras = 0, naLateral = 0;
            var relogio = Stopwatch.StartNew();
            bool noChao = false;
            while (relogio.Elapsed.TotalSeconds < 20 && !noChao)
            {
                Nativo.RECT q = Nativo.Retangulo(_hBuzzy);
                amostras++;
                if (q.Left == naParede.Left) naLateral++;
                noChao = q.Bottom == area.Bottom;
                Thread.Sleep(40);
            }
            EventoBuzzy? idle = LogDoBuzzy.Esperar(marcaPausa, e => e.Chave == "NUCLEO" && e["de"] == "Climbing" && e["para"] == "Idle", 3000);
            bool ok = reagiu is not null && agarrou is not null && encostado && pausou && noChao && idle is not null && naLateral == amostras;
            Registrar(Criterio, m.Recusado ? Inconclusivo : ok ? Ok : Falhou,
                $"semente {p.Semente}; clique nele escalando virou reação={reagiu is not null}; a acomodação o deixou na parede (SETTLING→CLIMBING)={agarrou is not null} ({agarrou?["regra"] ?? "-"}), "
                + $"encostado na lateral={encostado} em {naParede}; pausar pelo menu: {DescreverMenu(m)}; desceu pela mesma lateral {naLateral}/{amostras} amostras; "
                + $"pés no chão em {relogio.Elapsed.TotalSeconds:0.0} s={noChao}; CLIMBING→IDLE={idle is not null}");
        }
        finally
        {
            FecharBuzzy("parede sem estar preso");
        }
    }

    /// <summary>
    /// Como <see cref="PontoDoCorpo"/>, mas só aceita um ponto cujos vizinhos a 6 px acima e abaixo também são do Buzzy:
    /// escalando ou descendo, um deslocamento de poucos pixels entre a conferência e o clique não o tira do corpo.
    /// Sem nenhum assim, devolve o de <see cref="PontoDoCorpo"/>.
    /// </summary>
    private Nativo.POINT PontoFirmeDoCorpo()
    {
        const int Folga = 6;
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        int passo = Math.Max(2, r.Largura / 16);
        int cx = (r.Left + r.Right) / 2, cy = (r.Top + r.Bottom) / 2;
        var candidatos = new List<Nativo.POINT>();
        for (int y = r.Top + passo + Folga; y < r.Bottom - passo - Folga; y += passo)
            for (int x = r.Left + passo; x < r.Right - passo; x += passo)
                candidatos.Add(new Nativo.POINT(x, y));
        foreach (Nativo.POINT c in candidatos.OrderBy(c => Math.Abs(c.X - cx) + Math.Abs(c.Y - cy)))
        {
            if (Nativo.DonoDoPonto(c.X, c.Y) == _hBuzzy
                && Nativo.DonoDoPonto(c.X, c.Y - Folga) == _hBuzzy
                && Nativo.DonoDoPonto(c.X, c.Y + Folga) == _hBuzzy)
                return c;
        }
        return PontoDoCorpo();
    }

    // ------------------------------------------------------------------ parte 4: o alívio e a paranoia

    /// <summary>A regra do soltar da banana sobre o bêbado do nível 2: um passo abaixo, na mesma fase.</summary>
    private static readonly Regex ReBananaNaVodka = new(@"Comer Banana; alivia Bebado/(?<fase>Subida|Pico)/2 -> Bebado/\k<fase>/1$", RegexOptions.Compiled);

    /// <summary>As transições que o pico da paranoia nunca tem: ele não escala, não pula e não descansa (DEC-028).</summary>
    private static readonly string[] ForaDoPicoDaParanoia = ["Climbing", "Jumping", "Resting"];

    /// <summary>Um uso visto no log: a regra do soltar (ITEM_DRAG_END → USING), a marca do log logo antes do arraste e se o uso acabou a tempo (SETTLING → IDLE).</summary>
    private sealed record UsoVisto(string Regra, long Marca, bool Acabou);

    /// <summary>
    /// V18 (o alívio, pedido do usuário de 2026-10-01, 12:20: "só quero que alimentos ou bebidas sem ser alcoólicas diminuam
    /// aos poucos o efeito da onda"): pausado, a vodka e a banana invocadas pelo menu; a vodka arrastada até ele (o bêbado,
    /// do nível 2) e, logo depois do uso dela, a banana. Comer acalma um passo a onda de substância da frente, sem começar a
    /// onda da própria banana: só o nível cai, na mesma fase, e o temporizador em curso continua, sem reagendar; na subida,
    /// a virada seguinte (o mesmo disparo agendado no soltar da vodka) já sai do nível 1.
    /// </summary>
    private void V18ComidaAcalmaUmPasso()
    {
        const string Criterio = "V18 — comida acalma um passo uma onda de substância: a banana na vodka (Bebado/…/2 → Bebado/…/1), sem começar a onda dela nem reagendar o temporizador";
        AbrirBuzzyDoTamagotchi(_sementeDoTamagotchi, pausado: true, "V18");
        try
        {
            long marcaItens = LogDoBuzzy.Marca();
            if (InvocarItem(Item.Vodka, 'V', "V18") is not { } vodka || InvocarItem(Item.Banana, 'B', "V18") is not { } banana) return;
            if (EsperarItemNoChao(vodka, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaVodka
                || EsperarItemNoChao(banana, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaBanana)
            {
                Registrar(Criterio, Falhou, "os itens não pararam no chão em 4 s");
                return;
            }
            // O foco é conferido depois dos menus, que dão o primeiro plano ao dono deles de propósito (DEC-016).
            int marcaR = _logReceptor.Contar();
            (UsoVisto? usoDaVodka, string situacao, string motivo) = UsarNoCentroDele(vodka, chaoDaVodka);
            if (usoDaVodka is null)
            {
                Registrar(Criterio, situacao, $"vodka: {motivo}");
                return;
            }
            (UsoVisto? usoDaBanana, situacao, motivo) = UsarNoCentroDele(banana, chaoDaBanana);
            if (usoDaBanana is null)
            {
                Registrar(Criterio, situacao, $"vodka: {usoDaVodka.Regra}; banana: {motivo}");
                return;
            }

            // O disparo da onda agendado no soltar da vodka: a subida do bêbado, num disparo único.
            List<EventoBuzzy> desdeAVodka = LogDoBuzzy.Desde(usoDaVodka.Marca);
            EventoBuzzy? agendada = desdeAVodka.FirstOrDefault(e => e.Chave == "ONDA" && e["agendada"] == "sim");
            Match alivio = ReBananaNaVodka.Match(usoDaBanana.Regra);
            bool naSubida = alivio.Success && alivio.Groups["fase"].Value == "Subida";
            // Na subida, a virada seguinte vem no fim dela (8 s depois do soltar da vodka): espera por ela, do nível 1.
            EventoBuzzy? virada = naSubida
                ? LogDoBuzzy.Esperar(usoDaBanana.Marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer", 12000, () => _buzzy is { HasExited: true })
                : null;
            List<EventoBuzzy> desdeABanana = LogDoBuzzy.Desde(usoDaBanana.Marca);
            int iSoltou = desdeABanana.FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using");
            int iVirada = desdeABanana.FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer");
            // Do soltar da banana até a virada (ou, sem ela, até agora): nenhum disparo novo agendado.
            int ate = iVirada >= 0 ? iVirada : desdeABanana.Count;
            bool reagendou = iSoltou >= 0 && desdeABanana.Skip(iSoltou).Take(ate - iSoltou).Any(e => e.Chave == "ONDA" && e["agendada"] == "sim");
            EventoBuzzy? disparo = iVirada >= 0 ? desdeABanana.Take(iVirada).LastOrDefault(e => e.Chave == "ONDA" && e["disparada"] == "sim") : null;
            bool viradaDoNivel1 = virada?["regra"].Contains("onda Bebado/Subida/1 -> Bebado/Pico/1", StringComparison.Ordinal) == true;
            bool mesmoDisparo = disparo is not null && agendada is not null && disparo["geracao"] == agendada["geracao"];
            bool ondaDaBanana = desdeAVodka.Any(e => e.Chave == "NUCLEO" && e["regra"].Contains(nameof(Onda.Satisfeito), StringComparison.Ordinal));
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);
            bool ok = usoDaVodka.Regra.Contains("Beber Vodka", StringComparison.Ordinal) && agendada is not null && alivio.Success && !reagendou && !ondaDaBanana
                && (!naSubida || (viradaDoNivel1 && mesmoDisparo)) && usoDaVodka.Acabou && usoDaBanana.Acabou && frente && !desativou;
            Registrar(Criterio, ok,
                $"vodka: {usoDaVodka.Regra} (disparo agendado: {agendada?["atrasoMs"] ?? "?"} ms, geração {agendada?["geracao"] ?? "?"}); banana: {usoDaBanana.Regra}; "
                + $"um passo abaixo, na mesma fase={alivio.Success}; disparo novo agendado entre o soltar da banana e a virada seguinte={reagendou}; a onda da banana (Satisfeito) apareceu={ondaDaBanana}; "
                + (naSubida
                    ? $"virada seguinte: {virada?["regra"] ?? "nenhuma em 12 s"} (do nível 1={viradaDoNivel1}; o mesmo disparo agendado no soltar da vodka={mesmoDisparo}, geração {disparo?["geracao"] ?? "?"})"
                    : "no pico, a virada seguinte é longa e não foi esperada")
                + $"; usos acabaram={usoDaVodka.Acabou && usoDaBanana.Acabou}; receptor na frente={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("V18");
        }
    }

    /// <summary>
    /// V17b (pedido do usuário de 2026-10-01, 18:50: "só quero que ele fique paranoico se misturar substâncias com alguma
    /// droga sintética, como bala, md, coca e lança; usando álcool e maconha não"): pausado, com a semente em que o primeiro
    /// sorteio do gerador da paranoia sai (<see cref="SementesDoTamagotchi.SementeEmQueOPrimeiroSorteioDaParanoiaSai"/>), a
    /// vodka, o baseado, o cigarro, o cogumelo e a bala invocados pelo menu; os quatro primeiros, um depois do outro, soltos
    /// nele: nenhuma regra fala da paranoia, e nem a cara paranoica, nem o suor, nem os gestos dela aparecem. Com esta
    /// semente, um sorteio gasto por qualquer um deles teria saído, e a paranoia teria começado ali: não houve sorteio. O
    /// controle: a bala depois deles, a primeira droga sintética do episódio, fecha a mistura com sintética e faz o primeiro
    /// sorteio, que sai na hora ("a paranoia começa").
    /// </summary>
    private void V17bMisturaSemSinteticaNaoDeixaParanoico()
    {
        const string Criterio = "V17b — álcool, maconha, cigarro e cogumelo misturados (vodka, baseado, cigarro e cogumelo) não deixam paranoico e não sorteiam; a bala depois deles faz o primeiro sorteio, que sai";
        if (SementesDoTamagotchi.SementeEmQueOPrimeiroSorteioDaParanoiaSai() is not { } semente)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente em que o primeiro sorteio do gerador da paranoia sai");
            return;
        }
        _rel.Linha($"   V17b: semente {semente}: o primeiro sorteio do gerador da paranoia sai (chance de {SementesDoTamagotchi.Configuracao.ChanceDaParanoia})");
        AbrirBuzzyDoTamagotchi(semente, pausado: true, "V17b");
        try
        {
            // Os cinco itens no chão antes do primeiro uso (cabem seis): os menus ficam todos antes dos usos.
            long marcaItens = LogDoBuzzy.Marca();
            var itens = new List<ItemVisto>();
            foreach ((Item item, char tecla) in new[] { (Item.Vodka, 'V'), (Item.Baseado, 'S'), (Item.Cigarro, 'I'), (Item.Cogumelo, 'U'), (Item.Bala, 'A') })
            {
                if (InvocarItem(item, tecla, "V17b") is not { } visto) return;
                itens.Add(visto);
            }
            var noChao = new List<Nativo.RECT>();
            foreach (ItemVisto visto in itens)
            {
                if (EsperarItemNoChao(visto, marcaItens, _areaUtilPrincipal, 4000) is not { } r)
                {
                    Registrar(Criterio, Falhou, $"{visto.Nome} não parou no chão em 4 s");
                    return;
                }
                noChao.Add(r);
            }
            // O foco é conferido depois dos menus, que dão o primeiro plano ao dono deles de propósito (DEC-016).
            int marcaR = _logReceptor.Contar();
            long inicio = LogDoBuzzy.Marca();
            var regras = new List<string>();
            bool acabaram = true;
            for (int i = 0; i < 4; i++)
            {
                (UsoVisto? uso, string situacao, string motivo) = UsarNoCentroDele(itens[i], noChao[i]);
                if (uso is null)
                {
                    Registrar(Criterio, situacao, $"semente {semente}; {itens[i].Nome}: {motivo}; regras do soltar até aqui: {string.Join(" | ", regras)}");
                    return;
                }
                regras.Add(uso.Regra);
                acabaram &= uso.Acabou;
            }
            List<EventoBuzzy> semSintetica = LogDoBuzzy.Desde(inicio);
            string[] paranoiaNoNucleo = [.. semSintetica.Where(e => e.Chave == "NUCLEO" && (e["regra"].Contains("paranoia", StringComparison.Ordinal) || e["regra"].Contains(nameof(Onda.Paranoico), StringComparison.Ordinal))).Select(e => e["regra"])];
            string[] paranoiaNoSprite = [.. semSintetica.Where(e => e.Chave == "SPRITE" && (e["expressao"] == "paranoico" || e["efeito"] == nameof(EfeitoVisual.Suor) || e["pose"] is "olharproteto" or "agachar"))
                .Select(e => $"{e["pose"]}/{e["expressao"]}/{e["efeito"]}")];

            // O controle: a bala, a primeira droga sintética do episódio, faz o primeiro sorteio, que sai com esta semente.
            (UsoVisto? usoDaBala, string situacaoDaBala, string motivoDaBala) = UsarNoCentroDele(itens[4], noChao[4]);
            bool comecouNaBala = usoDaBala?.Regra.Contains("Engolir Bala; a paranoia começa: Paranoico/Subida/1", StringComparison.Ordinal) == true;
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);
            string resultado = usoDaBala is null ? situacaoDaBala
                : paranoiaNoNucleo.Length == 0 && paranoiaNoSprite.Length == 0 && comecouNaBala && acabaram && frente && !desativou ? Ok : Falhou;
            Registrar(Criterio, resultado,
                $"semente {semente}; regras do soltar: {string.Join(" | ", regras)}; usos acabaram={acabaram}; "
                + $"a paranoia no log do núcleo antes da bala: {(paranoiaNoNucleo.Length == 0 ? "nenhuma" : string.Join(" | ", paranoiaNoNucleo))}; "
                + $"a cara paranoica, o suor ou os gestos dela antes da bala: {(paranoiaNoSprite.Length == 0 ? "nenhum" : string.Join(" | ", paranoiaNoSprite))}; "
                + $"bala: {usoDaBala?.Regra ?? motivoDaBala} (a paranoia começou nela, no primeiro sorteio={comecouNaBala}); receptor na frente={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("V17b");
        }
    }

    /// <summary>
    /// V17 (a paranoia, pedidos do usuário de 2026-10-01; DEC-028), com a agenda ligada e a semente escolhida por simulação
    /// do núcleo (<see cref="SementesDoTamagotchi.ParanoiaNoPico"/>), em que o sorteio de 1 em 8 sai cedo: a vodka, a bala e
    /// a água invocadas pelo menu; a vodka arrastada até ele e, durante o uso dela, a bala segurada sobre ele e solta logo
    /// que o uso acaba (como no V12): a mistura com droga sintética. Confere, pelo log: a regra do soltar da bala ("a
    /// paranoia começa: Paranoico/Subida/1") e a virada para o pico (NUCLEO com a onda Paranoico); no fim do uso, o olhar pro
    /// teto (NUCLEO, sem a agenda, e SPRITE olharproteto, com a cara da pose e o suor); a cara paranoica com o suor (SPRITE);
    /// no pico, o agachar da agenda (NUCLEO e SPRITE agachar, com o suor) e nenhuma transição para CLIMBING, JUMPING ou
    /// RESTING; e a água arrastada até ele, que acalma um passo: o pico vira queda. Sem a agenda seguir a simulação (a
    /// primeira decisão antes da vodka, nenhum agachar decidido no pico, o pico acabado antes da água), o resultado é
    /// INCONCLUSIVO, não defeito.
    /// </summary>
    private void V17ParanoiaComMisturaComSintetica(Topologia topologia)
    {
        const string Criterio = "V17 — paranoia: a mistura com droga sintética (vodka e bala) invocada pelo menu e arrastada até ele, com o sorteio de 1 em 8 do episódio saindo na bala: a onda Paranoico; olha pro teto e agacha, com a cara paranoica e o suor; no pico, nenhuma transição para CLIMBING, JUMPING ou RESTING; uma água acalma um passo";
        if (SementesDoTamagotchi.ParanoiaNoPico(topologia) is not { Agachar: { } agacharPrevisto } p)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente em que o sorteio da paranoia sai na bala e ele agacha cedo no pico, longe da água");
            return;
        }
        _rel.Linha($"   V17: {p.Resumo}");
        AbrirBuzzyDoTamagotchi(p.Semente, pausado: false, "V17");
        try
        {
            long marcaItens = LogDoBuzzy.Marca();
            if (InvocarItem(Item.Vodka, 'V', "V17") is not { } vodka || InvocarItem(Item.Bala, 'A', "V17") is not { } bala
                || InvocarItem(Item.Agua, 'G', "V17") is not { } agua) return;
            if (EsperarItemNoChao(vodka, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaVodka
                || EsperarItemNoChao(bala, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaBala
                || EsperarItemNoChao(agua, marcaItens, _areaUtilPrincipal, 4000) is not { } chaoDaAgua)
            {
                Registrar(Criterio, Falhou, "os itens não pararam no chão em 4 s");
                return;
            }
            // O foco é conferido depois dos menus, que dão o primeiro plano ao dono deles de propósito (DEC-016).
            int marcaR = _logReceptor.Contar();
            if (LogDoBuzzy.Desde(_inicioLogBuzzy).Any(e => e.Chave == "AGENDA" && e.Campos.ContainsKey("disparo")))
            {
                Registrar(Criterio, Inconclusivo, $"semente {p.Semente}: a primeira decisão da agenda (prevista em {p.PrimeiraDecisao.TotalSeconds:0.0} s) veio antes da vodka; a sequência saiu da simulada");
                return;
            }
            long marca = LogDoBuzzy.Marca();
            if (!ArrastarItem(vodka, chaoDaVodka, SolturaNoCentroDele(vodka, chaoDaVodka), out string recusa))
            {
                Registrar(Criterio, Inconclusivo, recusa);
                return;
            }
            if (Nucleo(marca, "ItemDragEnd", "Idle", "Using", 3000) is null)
            {
                Registrar(Criterio, Falhou, "a vodka solta nele não começou o uso");
                return;
            }
            // Durante o uso da vodka, a bala na mão, sobre ele; solta logo que o uso acaba (o atento pausa a agenda).
            Nativo.POINT soltura = SolturaNoCentroDele(bala, chaoDaBala);
            if (!ArrastarItem(bala, chaoDaBala, soltura, out recusa, soltar: false, esperarCliqueDuplo: false))
            {
                Registrar(Criterio, Inconclusivo, recusa);
                return;
            }
            EventoBuzzy? acabou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Settling" && e["para"] == "Idle", 5000);
            long marcaDaBala = LogDoBuzzy.Marca();
            _inj.SoltarEsquerdo(soltura.X, soltura.Y);
            EventoBuzzy? usouABala = acabou is null ? null : Nucleo(marcaDaBala, "ItemDragEnd", "Idle", "Using", 3000);
            if (usouABala is null)
            {
                Registrar(Criterio, Falhou, $"o uso da vodka acabou com a bala na mão={acabou is not null}; a bala solta nele não começou o uso");
                return;
            }
            bool comecou = usouABala["regra"].Contains("Engolir Bala; a paranoia começa: Paranoico/Subida/1", StringComparison.Ordinal);
            // As esperas só dão o ritmo; as conferências usam o log inteiro desde o soltar da bala, lido depois da água.
            LogDoBuzzy.Esperar(marcaDaBala, e => e.Chave == "SPRITE" && e["pose"] == "olharproteto", 5000);
            // O agachar que a simulação previu no pico: até 10 s além da previsão, contada do soltar da bala.
            LogDoBuzzy.Esperar(marcaDaBala, e => e.Chave == "SPRITE" && e["pose"] == "agachar", (int)agacharPrevisto.TotalMilliseconds + 10000, () => _buzzy is { HasExited: true });

            // A água, arrastada até ele (parado, no gesto ou depois dele): o alívio.
            long marcaDaAgua = LogDoBuzzy.Marca();
            bool arrastouAAgua = ArrastarItem(agua, chaoDaAgua, SolturaNoCentroDele(agua, chaoDaAgua), out string recusaDaAgua);
            EventoBuzzy? bebeu = arrastouAAgua
                ? LogDoBuzzy.Esperar(marcaDaAgua, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000)
                : null;
            if (bebeu is not null) LogDoBuzzy.Esperar(marcaDaAgua, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Using", 5000);

            List<EventoBuzzy> ev = LogDoBuzzy.Desde(marcaDaBala);
            int iPico = ev.FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer" && e["regra"].Contains("onda Paranoico/Subida/1 -> Paranoico/Pico/1", StringComparison.Ordinal));
            int iAgua = ev.FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using" && e["regra"].Contains("Beber Agua", StringComparison.Ordinal));
            int iQueda = ev.FindIndex(e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer" && e["regra"].Contains("onda Paranoico/Pico/1 -> Paranoico/Queda/1", StringComparison.Ordinal));
            // O pico, da virada para ele até a água (ou até a queda pelo temporizador, se ela veio antes).
            int fimDoPico = iQueda >= 0 && (iAgua < 0 || iQueda < iAgua) ? iQueda : iAgua >= 0 ? iAgua : ev.Count;
            List<EventoBuzzy> noPico = iPico >= 0 ? ev.GetRange(iPico, Math.Max(0, fimDoPico - iPico)) : [];
            string[] proibidas = [.. noPico.Where(e => e.Chave == "NUCLEO" && ForaDoPicoDaParanoia.Contains(e["para"])).Select(e => $"{e["de"]}→{e["para"]} ({e["regra"]})")];
            string[] gestos = [.. noPico.Where(e => e.Chave == "NUCLEO" && e["regra"].StartsWith("IDLE + AUTONOMY_TIMER: gesto ", StringComparison.Ordinal)).Select(e => e["regra"]["IDLE + AUTONOMY_TIMER: gesto ".Length..])];
            bool decidiuAgachar = gestos.Contains(nameof(Gesto.Agachar));
            EventoBuzzy? olhou = ev.FirstOrDefault(e => e.Chave == "NUCLEO" && e["regra"] == $"IDLE: a paranoia começou, gesto {nameof(Gesto.OlharProTeto)}");
            EventoBuzzy? teto = ev.FirstOrDefault(e => e.Chave == "SPRITE" && e["pose"] == "olharproteto");
            EventoBuzzy? agachou = ev.FirstOrDefault(e => e.Chave == "SPRITE" && e["pose"] == "agachar");
            EventoBuzzy? cara = ev.FirstOrDefault(e => e.Chave == "SPRITE" && e["expressao"] == "paranoico");
            bool tetoComSuor = teto is not null && teto["expressao"] == "-" && teto["item"] == "-" && teto["efeito"] == nameof(EfeitoVisual.Suor);
            bool agacharComSuor = agachou is not null && agachou["expressao"] == "-" && agachou["item"] == "-" && agachou["efeito"] == nameof(EfeitoVisual.Suor);
            bool caraComSuor = cara?["efeito"] == nameof(EfeitoVisual.Suor);
            bool aguaNoPico = iAgua >= 0 && iPico >= 0 && iPico < iAgua && (iQueda < 0 || iQueda > iAgua);
            bool acalmou = bebeu?["regra"].Contains("Beber Agua; alivia Paranoico/Pico/1 -> Paranoico/Queda/1", StringComparison.Ordinal) == true;
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);

            bool defeito = !comecou || iPico < 0 || olhou is null || !tetoComSuor || !caraComSuor || proibidas.Length > 0
                || (decidiuAgachar && !agacharComSuor) || (aguaNoPico && !acalmou) || !frente || desativou;
            // Sem defeito, o que depende da agenda seguir a simulação: o agachar decidido no pico e a água ainda nele.
            string resultado = defeito ? Falhou : !decidiuAgachar || !aguaNoPico ? Inconclusivo : Ok;
            Registrar(Criterio, resultado,
                $"semente {p.Semente}; bala: {usouABala["regra"]} (a paranoia começou={comecou}); virada para o pico={iPico >= 0}; "
                + $"olhar pro teto no fim do uso: {olhou?["regra"] ?? "nenhum"}, quadro {DescreverQuadro(teto)}; cara paranoica: quadro {DescreverQuadro(cara)}; "
                + $"gestos da agenda no pico: [{string.Join(", ", gestos)}] (agachar previsto {agacharPrevisto.TotalSeconds:0.0} s depois da bala), quadro do agachar {DescreverQuadro(agachou)}; "
                + $"transições para CLIMBING, JUMPING ou RESTING no pico: {(proibidas.Length == 0 ? "nenhuma" : string.Join(" | ", proibidas))}; "
                + $"água: {(arrastouAAgua ? bebeu?["regra"] ?? "não começou o uso" : recusaDaAgua)} (solta no pico={aguaNoPico}; um passo, do pico à queda={acalmou}); "
                + $"receptor na frente={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("V17");
        }
    }

    /// <summary>
    /// V19 (o baseado por conta própria, pedido do usuário de 2026-10-01, 19:10; DEC-028), com a agenda ligada e a semente em
    /// que a primeira decisão, cedo, é fumar (<see cref="Buzzy.App.Testes.Integracao.SementesDoBaseado"/>, pela simulação do
    /// núcleo com a configuração do aplicativo e a topologia desta máquina, a mesma escolha do teste de integração): sem
    /// nenhum item invocado, ele fuma sozinho. No log: NUCLEO de IDLE para USING pelo AUTONOMY_TIMER, com a regra do baseado
    /// por conta própria e a subida do chapado agendada (um disparo único de 10 s); SPRITE com a pose de fumar, o baseado na
    /// mão, a cara da pose e a fumaça do chapado; nenhuma linha ITEM (nenhuma janela de item aberta). Um PRESS SINTÉTICO no
    /// corpo, no meio do uso (depois do primeiro trago, no quadro fumando-3), o interrompe no mesmo evento (USING para
    /// PRESSED), o soltar vira clique e a onda continua (nada cancelado; depois da reação, parado, a fumaça do chapado). Se a
    /// agenda real sair da simulada (a primeira decisão não é o baseado) ou o uso acabar antes do clique, INCONCLUSIVO.
    /// </summary>
    private void V19BaseadoPorContaPropria(Topologia topologia)
    {
        const string Criterio = "V19 — o baseado por conta própria: com a agenda ligada e nenhum item, ele fuma sozinho (NUCLEO IDLE→USING pelo AUTONOMY_TIMER, com o uso do baseado e a onda Chapado), com a pose de fumar e o baseado na mão, sem janela de item; o clique no meio do uso o interrompe, e a onda continua";
        if (Buzzy.App.Testes.Integracao.SementesDoBaseado.PrimeiraEmQueFumaCedo(SementesDoTamagotchi.Configuracao, topologia, TimeSpan.FromSeconds(12), 5000) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente até 5000 em que a primeira decisão, em até 12 s, é o baseado por conta própria");
            return;
        }
        (ulong semente, TimeSpan primeira) = escolha;
        _rel.Linha($"   V19: semente {semente}: a primeira decisão da agenda, prevista em {primeira.TotalSeconds:0.0} s, é o baseado por conta própria");
        AbrirBuzzyDoTamagotchi(semente, pausado: false, "V19");
        try
        {
            int marcaR = _logReceptor.Contar();
            long inicio = _inicioLogBuzzy;
            bool Decisao(EventoBuzzy e) => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer";
            EventoBuzzy? decisao = LogDoBuzzy.Esperar(inicio, Decisao, (int)primeira.TotalMilliseconds + 8000, () => _buzzy is { HasExited: true }) is null
                ? null
                : LogDoBuzzy.Desde(inicio).First(Decisao);
            if (decisao is null || decisao["para"] != "Using")
            {
                Registrar(Criterio, Inconclusivo, $"semente {semente}: a primeira decisão da agenda (prevista em {primeira.TotalSeconds:0.0} s) foi {(decisao is null ? "nenhuma" : $"{decisao["de"]}→{decisao["para"]} ({decisao["regra"]})")}; a agenda saiu da simulada");
                return;
            }
            EventoBuzzy? subida = LogDoBuzzy.Esperar(inicio, e => e.Chave == "ONDA" && e["agendada"] == "sim", 3000);
            EventoBuzzy? quadro = LogDoBuzzy.Esperar(inicio, e => e.Chave == "SPRITE" && e["pose"].StartsWith("fumando-", StringComparison.Ordinal), 3000);

            // O clique no meio do uso (3,5 s), e não no primeiro quadro (revisão do baseado por conta própria, achado 5): depois
            // do primeiro trago, no quadro fumando-3 (o passo 50 dos 210, 0,8 s depois do começo), um PRESS SINTÉTICO no corpo.
            EventoBuzzy? meio = LogDoBuzzy.Esperar(inicio, e => e.Chave == "SPRITE" && e["pose"] == "fumando-3", 3000);
            long marcaPress = LogDoBuzzy.Marca();
            Nativo.POINT corpo;
            try
            {
                corpo = PontoDoCorpo();
                _inj.Pressionar(corpo.X, corpo.Y, _hBuzzy);
            }
            catch (CliqueRecusado e)
            {
                Registrar(Criterio, Inconclusivo, $"o PRESS no meio do baseado foi recusado: {e.Message}");
                return;
            }
            EventoBuzzy? press = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "NUCLEO" && e["evento"] == "Press", 1500);
            _inj.SoltarEsquerdo(corpo.X, corpo.Y);
            EventoBuzzy? clique = Nucleo(marcaPress, "Click", "Pressed", "Reacting", 2000);
            EventoBuzzy? parado = LogDoBuzzy.Esperar(marcaPress, e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["efeito"] == nameof(EfeitoVisual.Fumaca), 5000);

            List<EventoBuzzy> ev = LogDoBuzzy.Desde(inicio);
            bool regra = decisao["de"] == "Idle" && decisao["regra"] == "IDLE + AUTONOMY_TIMER: Fumar Baseado por conta própria";
            string atrasoDaSubida = ((long)TabelaDoTamagotchi.DaOnda(Onda.Chapado).Duracao(FaseDaOnda.Subida, 2).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            bool chapado = subida?["atrasoMs"] == atrasoDaSubida;
            bool fumando = quadro is not null && quadro["item"] == "baseado" && quadro["expressao"] == "-" && quadro["efeito"] == nameof(EfeitoVisual.Fumaca);
            string[] itens = [.. ev.Where(e => e.Chave == "ITEM").Select(e => string.Join("|", e.Campos.Select(c => $"{c.Key}={c.Value}")))];
            bool cancelada = ev.Any(e => e.Chave == "ONDA" && e["cancelada"] == "sim");
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);
            if (press is not null && press["de"] != "Using")
            {
                Registrar(Criterio, Inconclusivo, $"semente {semente}: o uso acabou antes do clique (PRESS de {press["de"]}); regra={regra}, quadro {DescreverQuadro(quadro)}, quadro do meio {DescreverQuadro(meio)}, linhas ITEM={itens.Length}");
                return;
            }
            bool ok = regra && chapado && fumando && meio is not null && itens.Length == 0 && press?["para"] == "Pressed" && clique is not null && !cancelada && parado is not null && frente && !desativou;
            Registrar(Criterio, ok,
                $"semente {semente}; decisão {decisao["de"]}→{decisao["para"]} ({decisao["regra"]}); subida do chapado agendada {subida?["atrasoMs"] ?? "nenhuma"} ms (esperado {atrasoDaSubida}); "
                + $"quadro {DescreverQuadro(quadro)}; linhas ITEM: {(itens.Length == 0 ? "nenhuma" : string.Join(" ; ", itens))}; "
                + $"PRESS SINTÉTICO no ponto {corpo} no meio do uso, depois do quadro {DescreverQuadro(meio)}: {press?["de"] ?? "?"}→{press?["para"] ?? "?"}; soltar virou clique={clique is not null}; onda cancelada={cancelada}; "
                + $"depois da reação, parado com a fumaça: quadro {DescreverQuadro(parado)}; receptor na frente={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("V19");
        }
    }

    /// <summary>
    /// V20 (DEC-033): a chave "Conteúdo adulto" pelo menu, com o Buzzy pausado. Com a banana e a vodka no chão, A desliga: a
    /// janela da vodka fecha (recolhida) e a da banana fica; o menu seguinte mostra a chave desmarcada, e V no submenu "Itens"
    /// não invoca nada (só os quatro de alívio estão lá); A religa. Tudo por clique direito e teclas SINTÉTICOS.
    /// </summary>
    private void V20ConteudoAdulto()
    {
        const string Criterio = "V20 — Conteúdo adulto pelo menu (clique direito e A): desligado, a janela da vodka fecha e a da banana fica; o submenu \"Itens\" só tem os de alívio (V não invoca nada); A religa";
        AbrirBuzzyDoTamagotchi(_sementeDoTamagotchi, pausado: true, "V20");
        try
        {
            long marcaItens = LogDoBuzzy.Marca();
            if (InvocarItem(Item.Banana, 'B', "V20") is not { } banana || InvocarItem(Item.Vodka, 'V', "V20") is not { } vodka) return;
            if (EsperarItemNoChao(banana, marcaItens, _areaUtilPrincipal, 4000) is null || EsperarItemNoChao(vodka, marcaItens, _areaUtilPrincipal, 4000) is null)
            {
                Registrar(Criterio, Falhou, "os itens não pararam no chão em 4 s");
                return;
            }

            long marca = LogDoBuzzy.Marca();
            MenuOperado desligar = MenuNoPersonagem("V20, desligar", (Vk('A'), "A (Conteúdo adulto)"));
            EventoBuzzy? desligado = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetAdultContent", 2000);
            EventoBuzzy? removida = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e["removido"] == vodka.IdNoLog && e["motivo"] == "Recolhido", 3000);
            Thread.Sleep(300);
            bool vodkaFechou = !Nativo.IsWindow(vodka.Hwnd);
            bool bananaFicou = Nativo.IsWindowVisible(banana.Hwnd) && !LogDoBuzzy.Desde(marca).Any(e => e.Chave == "ITEM" && e["removido"] == banana.IdNoLog);

            long marcaDoMenu = LogDoBuzzy.Marca();
            MenuOperado semVodka = MenuNoPersonagem("V20, V sem a vodka", (Vk('I'), "I (Itens)"), (Vk('V'), "V (nenhum item)"));
            EventoBuzzy? exibindo = LogDoBuzzy.Desde(marcaDoMenu).FirstOrDefault(e => e.Chave == "MENU" && e["exibindo"] == "sim");
            bool nadaNovo = !LogDoBuzzy.Desde(marcaDoMenu).Any(e => e.Chave == "ITEM" && e.Campos.ContainsKey("mostrado"));

            long marcaReligar = LogDoBuzzy.Marca();
            MenuOperado religar = MenuNoPersonagem("V20, religar", (Vk('A'), "A (Conteúdo adulto)"));
            EventoBuzzy? religado = LogDoBuzzy.Esperar(marcaReligar, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetAdultContent", 2000);

            bool recusado = desligar.Recusado || semVodka.Recusado || religar.Recusado;
            bool ok = desligar.Fechado?["fechado"] == "ConteudoAdulto" && desligado?["regra"] == "CMD_SET_ADULT_CONTENT: desligado" && removida is not null && vodkaFechou && bananaFicou
                && exibindo?["conteudoAdulto"] == "nao" && semVodka.Fechado?["fechado"] == "Nenhum" && nadaNovo
                && religar.Fechado?["fechado"] == "ConteudoAdulto" && religado?["regra"] == "CMD_SET_ADULT_CONTENT: ligado";
            Registrar(Criterio, recusado ? Inconclusivo : ok ? Ok : Falhou,
                $"desligar: {DescreverMenu(desligar)}, regra {desligado?["regra"] ?? "nenhuma"}; vodka recolhida={removida is not null}, janela fechada={vodkaFechou}; banana ficou={bananaFicou}; "
                + $"menu seguinte com conteudoAdulto={exibindo?["conteudoAdulto"] ?? "?"} e ícones {exibindo?["icones"] ?? "?"}; I e V: {DescreverMenu(semVodka)}, nenhum item novo={nadaNovo}; "
                + $"religar: {DescreverMenu(religar)}, regra {religado?["regra"] ?? "nenhuma"}");
        }
        finally
        {
            FecharBuzzy("V20");
        }
    }

    /// <summary>Um quadro do log (SPRITE) em uma linha: pose, cara, item, sobreposição e fase; "nenhum" sem a linha.</summary>
    private static string DescreverQuadro(EventoBuzzy? quadro)
        => quadro is null ? "nenhum" : $"{quadro["pose"]}/{quadro["expressao"]}/{quadro["item"]}/{quadro["efeito"]}/{quadro["fase"]}";

    /// <summary>
    /// Arrasta o item, já no chão, até o centro dele e solta (o arraste SINTÉTICO de sempre, com as conferências do
    /// injetor); espera o uso começar (ITEM_DRAG_END → USING) e acabar (SETTLING → IDLE), no tempo do uso mais 3 s. Devolve
    /// o uso visto, ou nulo com a situação do registro (INCONCLUSIVO numa recusa do injetor; FALHOU sem o uso) e o motivo.
    /// </summary>
    private (UsoVisto? Uso, string Situacao, string Motivo) UsarNoCentroDele(ItemVisto visto, Nativo.RECT noChao)
    {
        long marca = LogDoBuzzy.Marca();
        if (!ArrastarItem(visto, noChao, SolturaNoCentroDele(visto, noChao), out string recusa)) return (null, Inconclusivo, recusa);
        EventoBuzzy? uso = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "ItemDragEnd" && e["para"] == "Using", 3000);
        if (uso is null) return (null, Falhou, $"{visto.Nome} solto nele não começou o uso (ITEM_DRAG_END→USING)");
        int limiteMs = TabelaDoTamagotchi.DoItem(visto.Item).PassosDoUso * 1000 / SementesDoTamagotchi.Configuracao.PassosPorSegundo + 3000;
        bool acabou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "Tick" && e["de"] == "Settling" && e["para"] == "Idle", limiteMs) is not null;
        return (new UsoVisto(uso["regra"], marca, acabou), Ok, "");
    }

    // ------------------------------------------------------------------ apoio do tamagotchi

    private static ushort Vk(char letra) => char.ToUpperInvariant(letra);

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static bool Intersectam(Nativo.RECT a, Nativo.RECT b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    /// <summary>O verbo da arte para o verbo de uso do núcleo (os mesmos nomes; o teste NucleoEArteTestes amarra os dois).</summary>
    private static Verbo VerboDaArte(VerboDeUso verbo) => verbo switch
    {
        VerboDeUso.Comer => Verbo.Comer,
        VerboDeUso.Beber => Verbo.Beber,
        VerboDeUso.Fumar => Verbo.Fumar,
        VerboDeUso.Cheirar => Verbo.Cheirar,
        VerboDeUso.Engolir => Verbo.Engolir,
        VerboDeUso.Inalar => Verbo.Inalar,
        _ => throw new ArgumentOutOfRangeException(nameof(verbo), verbo, "Verbo de uso sem animação na arte."),
    };

    private static EventoBuzzy? PrimeiroDesde(long marca, Func<EventoBuzzy, bool> condicao) => LogDoBuzzy.Desde(marca).FirstOrDefault(condicao);

    /// <summary>
    /// Abre um Buzzy da verificação do tamagotchi, pausado ou não, com a semente dada, o perfil de teste e o diagnóstico; a
    /// pasta do perfil é apagada antes, para ele partir da posição inicial, menos com <paramref name="limpar"/> falso (o
    /// V16, que reabre com o que a execução anterior gravou). Confere que as janelas são deste processo.
    /// </summary>
    private void AbrirBuzzyDoTamagotchi(ulong semente, bool pausado, string rotulo, bool limpar = true)
    {
        _inicioLogBuzzy = LogDoBuzzy.Marca();
        string valor = semente.ToString(CultureInfo.InvariantCulture);
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy, pausado ? ["--pausado", "--semente", valor] : ["--semente", valor]);
        ExigirNenhumBuzzyAberto();
        if (limpar)
        {
            PerfilDaVerificacao.Limpar();
            PerfilDaVerificacao.ComOsNoveAdultos();
        }
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não iniciou.");
        _inicioBuzzy = _buzzy.StartTime;
        _buzzyEncerrado = false;
        _hBuzzy = JanelaDoBuzzy(Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy")["hwnd"], "JANELA");
        _hServico = JanelaDoBuzzy(Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço")["hwnd"], "SERVICO");
        Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 5000, "bandeja");
        Esperar(e => e.Chave == "POSICAO", 5000, "posição");
        // Antes do primeiro quadro, a janela em camadas ainda não tem pixel opaco: o teste de acerto não acharia o corpo.
        Esperar(e => e.Chave == "PRIMEIRO_QUADRO", 10000, "primeiro quadro");
        Thread.Sleep(500);
        _rel.Linha($"   Buzzy aberto ({rotulo}) {(pausado ? "pausado" : "com a agenda ligada")}, semente {semente}: pid {_buzzy.Id}, janela {Nativo.Retangulo(_hBuzzy)}");
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor($"depois de abrir o Buzzy ({rotulo})");
    }

    /// <summary>A segunda instância (o mesmo perfil, sem limpar a pasta) pede para a primeira aparecer e sai; devolve se saiu e o código.</summary>
    private (bool Saiu, int Codigo) SegundaInstanciaQueMostra()
    {
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy);
        Process segunda = Process.Start(psi) ?? throw new FalhaDeVerificacao("A segunda instância não iniciou.");
        try
        {
            bool saiu = segunda.WaitForExit(8000);
            return (saiu, saiu ? segunda.ExitCode : -1);
        }
        finally
        {
            if (!segunda.HasExited) _rel.Linha("   " + Processos.EncerrarAForca(segunda, "segunda instância"));
            segunda.Dispose();
        }
    }

    /// <summary>Os objetos GDI e USER do Buzzy aberto pela verificação.</summary>
    private (uint Gdi, uint User) ObjetosDoBuzzy()
        => (Nativo.GetGuiResources(_buzzy!.Handle, Nativo.GR_GDIOBJECTS), Nativo.GetGuiResources(_buzzy.Handle, Nativo.GR_USEROBJECTS));

    /// <summary>Abre o menu pelo clique direito SINTÉTICO num ponto do corpo do personagem e escolhe pelas teclas.</summary>
    private MenuOperado MenuNoPersonagem(string rotulo, params (ushort Vk, string Nome)[] teclas)
        => MenuPeloCliqueDireito(PontoDoCorpo(), _hBuzzy, rotulo, teclas);

    /// <summary>
    /// Abre o menu do Buzzy pelo clique direito SINTÉTICO no ponto de uma janela dele (o personagem ou um item), com as
    /// conferências do injetor, e escolhe pelas teclas de acesso, uma a uma, cada uma só com a janela da frente do Buzzy (o
    /// dono do menu). Sem isso, fecha o menu sem teclado (WM_CANCELMODE ao dono). Depois, devolve o foco ao receptor se ele
    /// não voltar sozinho, e conta o caso.
    /// </summary>
    private MenuOperado MenuPeloCliqueDireito(Nativo.POINT ponto, nint janela, string rotulo, params (ushort Vk, string Nome)[] teclas)
    {
        long marca = LogDoBuzzy.Marca();
        _menusDoTamagotchi++;
        MenuOperado m;
        try
        {
            _inj.CliqueDireito(ponto.X, ponto.Y, janela);
            m = LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("aberto"), 3000) is null
                ? new MenuOperado(false, null, false, false, "o menu não abriu em 3 s")
                : OperarMenuComTeclas(marca, teclas);
        }
        catch (CliqueRecusado e)
        {
            m = new MenuOperado(false, null, false, false, e.Message, Recusado: true);
        }
        VoltarAoReceptorDepoisDoMenu(rotulo);
        return m;
    }

    /// <summary>Como <see cref="OperarMenu"/>, com várias teclas (o submenu e a opção), cada uma só com a janela da frente do Buzzy.</summary>
    private MenuOperado OperarMenuComTeclas(long marca, (ushort Vk, string Nome)[] teclas)
    {
        EventoBuzzy? exibindo = LogDoBuzzy.Esperar(marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000);
        if (exibindo is null) return new MenuOperado(false, null, false, false, "o Buzzy não registrou MENU|exibindo");
        nint dono = long.TryParse(exibindo["dono"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long d) ? (nint)d : 0;
        bool donoNaFrente = exibindo["donoEmPrimeiroPlano"] == "True";
        Thread.Sleep(400);
        var detalhe = new StringBuilder();
        bool enviadas = donoNaFrente;
        if (!donoNaFrente) detalhe.Append("o Buzzy registrou donoEmPrimeiroPlano=False: o menu não recebe teclado; ");
        foreach ((ushort vk, string nome) in teclas)
        {
            if (!enviadas) break;
            enviadas = _inj.TeclaVirtual(vk, $"tecla de acesso {nome}", FrenteEhDoBuzzy);
            if (!enviadas) detalhe.Append($"a janela da frente não era do Buzzy no momento da tecla {nome}: nada foi enviado; ");
            Thread.Sleep(250);
        }
        EventoBuzzy? fechado = enviadas ? EsperarFechamento(marca, 3000) : null;
        if (fechado is null)
        {
            detalhe.Append(enviadas ? "o menu não fechou com as teclas; " : "o teclado não pôde ser usado; ");
            fechado = CancelarMenu(marca, dono, detalhe);
        }
        return new MenuOperado(true, fechado, donoNaFrente, enviadas, detalhe.ToString().TrimEnd(' ', ';'));
    }

    /// <summary>Depois de um menu, o receptor volta ao primeiro plano sozinho; se não voltar em 2 s, a ferramenta o ativa, e o caso é contado.</summary>
    private void VoltarAoReceptorDepoisDoMenu(string motivo)
    {
        if (_receptor is null || _receptor.HasExited) return;
        if (EsperarAte(() => Nativo.GetForegroundWindow() == _hReceptor, 2000)) return;
        _menusSemVoltarOFoco++;
        AtivarReceptor($"depois do menu ({motivo})");
    }

    /// <summary>
    /// Invoca um item pelo submenu "Itens" do menu do personagem (I e a tecla do item) e espera a janela dele no log, com o
    /// HWND conferido como do Buzzy aberto. Se não der, registra a falha do cenário e devolve nulo.
    /// </summary>
    private ItemVisto? InvocarItem(Item item, char tecla, string caso)
    {
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuNoPersonagem($"{caso}, {item}", (Vk('I'), "I (Itens)"), (Vk(tecla), $"{char.ToUpperInvariant(tecla)} ({item})"));
        if (m.Fechado?["fechado"] != "Item" || m.Fechado?["argumento"] != item.ToString())
        {
            Registrar($"{caso}: o menu invoca {item}", m.Recusado ? Inconclusivo : Falhou, DescreverMenu(m));
            return null;
        }
        EventoBuzzy? mostrado = LogDoBuzzy.Esperar(marca, e => e.Chave == "ITEM" && e.Campos.ContainsKey("mostrado") && e["item"] == item.ToString(), 3000);
        if (mostrado is null || EventoBuzzy.Retangulo(mostrado["retangulo"]) is not { } ret)
        {
            Registrar($"{caso}: a janela de {item} aparece", Falhou, "o Buzzy não registrou ITEM|mostrado com o retângulo");
            return null;
        }
        try
        {
            Nativo.POINT opaco = EventoBuzzy.Ponto(mostrado["pontoOpaco"]), transparente = EventoBuzzy.Ponto(mostrado["pontoTransparente"]);
            int id = int.Parse(mostrado["mostrado"], CultureInfo.InvariantCulture);
            return new ItemVisto(id, item, JanelaDoBuzzy(mostrado["hwnd"], "ITEM"),
                new Nativo.POINT(opaco.X - ret.Left, opaco.Y - ret.Top), new Nativo.POINT(transparente.X - ret.Left, transparente.Y - ret.Top));
        }
        catch (FormatException e)
        {
            Registrar($"{caso}: os pontos de teste de {item}", Falhou, e.Message);
            return null;
        }
    }

    /// <summary>
    /// Espera o item parar no chão de <paramref name="area"/>: a linha ITEM|movido…|parado=sim depois de
    /// <paramref name="marca"/> (uma por pouso, mesmo quando o passo do pouso não move a janela: revisão de correção do
    /// app, achado 5) ou, por segurança, a janela parada no chão por 400 ms. Devolve o retângulo, ou nulo.
    /// </summary>
    private static Nativo.RECT? EsperarItemNoChao(ItemVisto item, long marca, Nativo.RECT area, int limiteMs)
    {
        var relogio = Stopwatch.StartNew();
        Nativo.RECT? anterior = null;
        long estavelDesde = -1;
        while (relogio.ElapsedMilliseconds < limiteMs)
        {
            EventoBuzzy? parado = LogDoBuzzy.Desde(marca).LastOrDefault(e => e.Chave == "ITEM" && e["movido"] == item.IdNoLog && e["parado"] == "sim");
            if (parado is not null && EventoBuzzy.Retangulo(parado["retangulo"]) is { } r && r.Bottom == area.Bottom) return r;
            Nativo.RECT agora = Nativo.Retangulo(item.Hwnd);
            if (agora.Bottom == area.Bottom && anterior is { } a && a.Equals(agora))
            {
                if (estavelDesde < 0) estavelDesde = relogio.ElapsedMilliseconds;
                else if (relogio.ElapsedMilliseconds - estavelDesde >= 400) return agora;
            }
            else
            {
                estavelDesde = -1;
            }
            anterior = agora;
            Thread.Sleep(50);
        }
        return null;
    }

    /// <summary>O cursor para soltar o item de modo que o centro dele fique no centro do personagem (o botão pressionado no ponto opaco).</summary>
    private Nativo.POINT SolturaNoCentroDele(ItemVisto item, Nativo.RECT noChao)
    {
        Nativo.POINT de = item.Opaco(noChao);
        Nativo.RECT corpo = Nativo.Retangulo(_hBuzzy);
        return new Nativo.POINT(de.X + (corpo.Centro.X - noChao.Centro.X), de.Y + (corpo.Centro.Y - noChao.Centro.Y));
    }

    /// <summary>
    /// Arraste SINTÉTICO de um item: o botão pressionado no ponto opaco dele (o injetor confere que o ponto é da janela do
    /// item), movimentos em passos até <paramref name="ate"/> e, com <paramref name="soltar"/>, o soltar lá.
    /// </summary>
    private bool ArrastarItem(ItemVisto item, Nativo.RECT noChao, Nativo.POINT ate, out string recusa, int passos = 24, bool soltar = true, bool esperarCliqueDuplo = true)
    {
        Nativo.POINT de = item.Opaco(noChao);
        try
        {
            _inj.Pressionar(de.X, de.Y, item.Hwnd, esperarCliqueDuplo: esperarCliqueDuplo);
        }
        catch (CliqueRecusado e)
        {
            recusa = $"o botão pressionado em {item.Nome} foi recusado: {e.Message}";
            return false;
        }
        for (int i = 1; i <= passos; i++)
        {
            _inj.MoverSegurando(de.X + (ate.X - de.X) * i / passos, de.Y + (ate.Y - de.Y) * i / passos);
            Thread.Sleep(16);
        }
        if (soltar) _inj.SoltarEsquerdo(ate.X, ate.Y);
        recusa = "";
        return true;
    }

    /// <summary>Os itens com janela à vista ou escondida, pelo log deste Buzzy: os mostrados (criados) menos os removidos.</summary>
    private List<(int Id, nint Hwnd)> ItensNaTelaPeloLog()
    {
        List<EventoBuzzy> ev = LogDoBuzzy.Desde(_inicioLogBuzzy);
        var removidos = new HashSet<string>(ev.Where(e => e.Chave == "ITEM" && e.Campos.ContainsKey("removido")).Select(e => e["removido"]), StringComparer.Ordinal);
        var itens = new List<(int, nint)>();
        foreach (EventoBuzzy e in ev.Where(e => e.Chave == "ITEM" && e.Campos.ContainsKey("mostrado") && e["criada"] == "sim"))
        {
            if (removidos.Contains(e["mostrado"])) continue;
            nint hwnd = long.TryParse(e["hwnd"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long h) ? (nint)h : 0;
            if (hwnd != 0 && Nativo.PidDe(hwnd) == (uint)_buzzy!.Id) itens.Add((int.Parse(e["mostrado"], CultureInfo.InvariantCulture), hwnd));
        }
        return itens;
    }

    /// <summary>
    /// A onda da frente pelo log: a última virada (ITEM_EFFECT_TIMER) depois de <paramref name="marca"/>, ou, sem virada, a
    /// subida da onda do item no nível da intensidade dele. Devolve Tipo/Fase/Nível e a velocidade da fase, em %.
    /// </summary>
    private static (string Onda, int Percentual) OndaNoLog(long marca, Item item)
    {
        EventoBuzzy? virada = LogDoBuzzy.Desde(marca).LastOrDefault(e => e.Chave == "NUCLEO" && e["evento"] == "ItemEffectTimer" && ReOndaQueVirou.IsMatch(e["regra"]));
        DadosDoItem dados = TabelaDoTamagotchi.DoItem(item);
        Onda tipo = dados.Onda ?? Onda.Satisfeito;
        FaseDaOnda fase = FaseDaOnda.Subida;
        int nivel = Math.Clamp(dados.Intensidade, 1, 3);
        if (virada is not null)
        {
            Match m = ReOndaQueVirou.Match(virada["regra"]);
            foreach (Onda o in Enum.GetValues<Onda>()) if (o.ToString() == m.Groups["tipo"].Value) tipo = o;
            foreach (FaseDaOnda f in Enum.GetValues<FaseDaOnda>()) if (f.ToString() == m.Groups["fase"].Value) fase = f;
            nivel = int.Parse(m.Groups["nivel"].Value, CultureInfo.InvariantCulture);
        }
        int percentual = TabelaDoTamagotchi.DaOnda(tipo).Perfil(fase, nivel).Velocidade;
        return (SementesDoTamagotchi.Descrever(new EstadoDaOnda(tipo, fase, nivel, nivel)), percentual);
    }

    /// <summary>
    /// Mede a velocidade horizontal da janela dele andando, por amostragem de outro processo (não é gravação de tela): o
    /// deslocamento entre a primeira e a última posição, num trecho em que ele só anda num sentido, sobre o tempo entre elas.
    /// </summary>
    private (double? PxPorSegundo, string Resumo) MedirCaminhada(TimeSpan duracao)
    {
        Thread.Sleep(150);
        var amostras = new List<(double Ms, int X, int Bottom)>();
        var relogio = Stopwatch.StartNew();
        while (relogio.Elapsed < duracao)
        {
            Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
            amostras.Add((relogio.Elapsed.TotalMilliseconds, r.Left, r.Bottom));
            Thread.Sleep(0);
        }
        // Só o trecho do começo até ele parar, virar ou sair do chão.
        int sentido = 0, fim = 0;
        for (int i = 1; i < amostras.Count; i++)
        {
            int dx = Math.Sign(amostras[i].X - amostras[i - 1].X);
            if (amostras[i].Bottom != amostras[0].Bottom) break;
            if (dx != 0)
            {
                if (sentido == 0) sentido = dx;
                else if (dx != sentido) break;
                fim = i;
            }
        }
        if (fim == 0 || amostras[fim].Ms < 600) return (null, $"{amostras.Count} amostras: trecho andando curto demais para medir");
        double v = Math.Abs(amostras[fim].X - amostras[0].X) / (amostras[fim].Ms / 1000.0);
        return (v, $"{amostras.Count} amostras; {Math.Abs(amostras[fim].X - amostras[0].X)} px em {amostras[fim].Ms / 1000.0:0.00} s");
    }

    /// <summary>O que a observação do cambaleio viu (V12).</summary>
    /// <param name="Recuos">Posições que voltaram 1 px contra o sentido da caminhada e logo depois seguiram a favor dele.</param>
    /// <param name="SempreNoChao">Falso só se a janela saiu do chão sem o núcleo sair da caminhada.</param>
    /// <param name="Amostras">Posições distintas da janela no trecho no chão.</param>
    /// <param name="Segundos">Duração do trecho observado no chão.</param>
    /// <param name="Fim">Por que a observação acabou.</param>
    private sealed record Cambaleio(int Recuos, bool SempreNoChao, int Amostras, double Segundos, string Fim);

    /// <summary>
    /// Observa a caminhada com o cambaleio: amostras rápidas da janela, de outro processo, até ele ficar parado por 1 s,
    /// sair do chão ou até a duração. Só o trecho no chão conta. Sair do chão vale quando o núcleo saiu da caminhada (por
    /// exemplo, WALKING → CLIMBING ao chegar à parede que ia escalar, no log desde <paramref name="marca"/>); sair do chão
    /// ainda andando é falha. Conta os recuos: posições que voltam 1 px contra o sentido da caminhada e logo depois seguem
    /// a favor dele (uma virada no fim da caminhada não conta).
    /// </summary>
    private Cambaleio ObservarCambaleio(long marca, TimeSpan duracao)
    {
        var xs = new List<int>();
        var relogio = Stopwatch.StartNew();
        double ultimaMudanca = 0, noChaoAte = 0;
        string fim = $"{duracao.TotalSeconds:0} s de observação";
        bool saiuDoChao = false;
        while (relogio.Elapsed < duracao)
        {
            Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
            double ms = relogio.Elapsed.TotalMilliseconds;
            if (r.Bottom != _areaUtilPrincipal.Bottom)
            {
                saiuDoChao = true;
                break;
            }
            noChaoAte = ms;
            if (xs.Count == 0 || xs[^1] != r.Left)
            {
                xs.Add(r.Left);
                ultimaMudanca = ms;
            }
            else if (ms - ultimaMudanca > 1000)
            {
                fim = "parado por 1 s";
                break;
            }
            Thread.Sleep(0);
        }
        bool sempreNoChao = true;
        if (saiuDoChao)
        {
            EventoBuzzy? saiu = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["de"] == "Walking" && e["para"] is { Length: > 0 } para && para != "Walking", 1000);
            sempreNoChao = saiu is not null;
            fim = saiu is null
                ? "saiu do chão AINDA ANDANDO (nenhuma transição a partir de WALKING no log)"
                : $"saiu do chão depois de deixar a caminhada ({saiu["de"]}→{saiu["para"]}: {saiu["regra"]})";
        }
        // O sentido da caminhada: o da maioria dos primeiros movimentos.
        int sentido = Math.Sign(xs.Zip(xs.Skip(1), (a, b) => Math.Sign(b - a)).Take(20).Sum());
        int recuos = 0;
        for (int i = 1; i + 1 < xs.Count; i++)
        {
            if (sentido != 0 && Math.Sign(xs[i] - xs[i - 1]) == -sentido && Math.Sign(xs[i + 1] - xs[i]) == sentido) recuos++;
        }
        return new Cambaleio(recuos, sempreNoChao, xs.Count, noChaoAte / 1000, fim);
    }

    private void RegistrarCoberturaDoTamagotchi()
    {
        _rel.Linha("   Cobertura do tamagotchi:");
        _rel.Linha("     - V16 (emoção restaurada ao reabrir): com a persistência do passo P7 da Fase 5, só no perfil de teste da verificação");
        _rel.Linha("     - [MANUAL] marca de rádio ao lado do rosto nos temas claro, escuro e alto contraste; ícones dos itens nos três temas; Narrador lendo os submenus");
        _rel.Linha("     - [MANUAL] revisão visual das animações de uso, dos efeitos e do tom; conforto para agarrar os itens pequenos (L14); gravação a 120 qps das animações de uso");
        _rel.Linha("     - [MANUAL] revisão pelo usuário do tom da paranoia (o suor, o olhar pro teto, o agachar; a chance de 1 em 8 vale uma vez por mistura com droga sintética, no uso que a fecha; a arte da bala continua um doce, agora com os corações do eufórico) e do alívio aos poucos (V17, V17b e V18 só conferem a regra e os quadros)");
        _rel.Linha("     - [MANUAL] revisão pelo usuário do baseado por conta própria: a frequência (cerca de um a cada 4 minutos parado no chão, sem estar chapado, na energia Média) e o tom (V19 só confere a regra, os quadros e o clique)");
        _naoExercitados.Add("revisão da frequência e do tom do baseado por conta própria pelo usuário [MANUAL]");
        _naoExercitados.Add("revisão de tom da paranoia e do alívio pelo usuário [MANUAL]");
        _rel.Linha("     - [HW] menu e janelas de item a 125, 150, 175 e 200%; arrastar um item entre monitores de escalas diferentes; desconectar o monitor com itens na tela (depois da P12)");
        _naoExercitados.Add("marca de rádio e ícones do menu nos temas claro, escuro e alto contraste [MANUAL]");
        _naoExercitados.Add("Narrador nos submenus do menu [MANUAL]");
        _naoExercitados.Add("revisão visual e de tom das animações de uso e dos efeitos [MANUAL]");
        _naoExercitados.Add("conforto para agarrar os itens pequenos (L14) [MANUAL]");
        _naoExercitados.Add("menu e janelas de item a 125 a 200% [HW]");
    }
}
