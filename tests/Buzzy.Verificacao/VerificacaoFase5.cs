using System.Diagnostics;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética da Fase 5 (passo P15; TODO.md), na máquina em que roda:
/// <list type="bullet">
/// <item>T1, a travessia andando entre monitores (DEC-032): com uma semente escolhida por simulação do núcleo (a mesma
/// configuração do app) em que a primeira decisão é ir ao outro monitor, ele anda até a porta e cruza a borda; a janela,
/// amostrada de outro processo, fica sempre inteira na união das áreas úteis, passa montada sobre a borda e termina inteira no
/// vizinho, com os pés no chão, sem tirar o foco do aplicativo em uso;</item>
/// <item>S2 e S7, soltar, fechar e reabrir no mesmo lugar (DEC-029): arrastado por input SINTÉTICO até o monitor em x negativo
/// (ou outro que não o principal; sem ele, no principal), gravado com atraso, fechado e reaberto sem limpar o perfil de teste,
/// ele volta ao mesmo retângulo, restaurado pela chave.</item>
/// </list>
/// Sem um monitor vizinho com o mesmo chão, o T1 fica N/A. O salto de degrau, o transbordo e a escala mista não existem nesta
/// máquina: ficam com os testes automatizados e o [HW]. Todo input é SINTÉTICO.
/// </summary>
internal sealed partial class Verificacao
{
    private static readonly TamanhoDip SpriteDoApp = new(128, 128);

    internal Sumario ExecutarFase5()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            Topologia topologia = TopologiaLida();
            _prefixo = "Fase 5 — ";
            F5TravessiaAndando(topologia);
            Digitar("F1-travessia;");
            F5SoltarFecharEReabrir(topologia);
            Digitar("F2-reabrir;");
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
        _rel.Linha("   Cobertura da Fase 5: a travessia plana e S2/S7 nesta máquina; o salto de degrau, o transbordo, a escala mista (protótipo P6) e S1, S3–S6 e S8–S12 só com outro arranjo de monitores [HW] (testes automatizados em TravessiaTestes e PassagensTestes).");
        _naoExercitados.Add("salto de degrau, transbordo e escala mista entre monitores [HW]");
        return Resumir();
    }

    // ------------------------------------------------------------------ T1: a travessia andando

    private void F5TravessiaAndando(Topologia topologia)
    {
        const string Criterio = "T1 — travessia andando (DEC-032): indo ao outro monitor, ele cruza a borda com a janela sempre inteira na união das áreas úteis, passa montado sobre ela e termina inteiro no vizinho, com os pés no chão; o foco fica no aplicativo em uso";
        MonitorDoDesktop principal = topologia.Principal;
        bool temPorta = Passagens.PortaPlana(topologia, principal, -1, SpriteDoApp) is not null || Passagens.PortaPlana(topologia, principal, +1, SpriteDoApp) is not null;
        if (!temPorta)
        {
            Registrar(Criterio, NaoAplicavel, $"o principal não tem vizinho encostado com o mesmo chão ({topologia.Monitores.Count} monitor(es))");
            return;
        }
        if (EscolherSementeParaAtravessar(topologia) is not { } escolha)
        {
            Registrar(Criterio, Inconclusivo, "nenhuma semente até 20000 em que a primeira decisão, em até 12 s, leva à travessia completa em até 60 s");
            return;
        }
        _rel.Linha($"   T1: semente {escolha.Semente}: a primeira decisão, prevista em {escolha.Atraso.TotalSeconds:0.0} s, é ir ao outro monitor ({escolha.Destino}); a travessia completa {escolha.AteCompletar.TotalSeconds:0.0} s depois");
        AbrirBuzzyComAutonomia(escolha.Semente);
        try
        {
            int marcaR = _logReceptor.Contar();
            long inicio = _inicioLogBuzzy;
            int limite = (int)(escolha.Atraso + escolha.AteCompletar).TotalMilliseconds + 15000;
            EventoBuzzy? comecou = LogDoBuzzy.Esperar(inicio, e => e.Chave == "NUCLEO" && e["regra"] == "WALKING: passagem (atravessa)", limite, () => _buzzy is { HasExited: true });
            if (comecou is null)
            {
                EventoBuzzy? decisao = LogDoBuzzy.Desde(inicio).FirstOrDefault(e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer");
                Registrar(Criterio, Inconclusivo, $"semente {escolha.Semente}: a travessia não começou em {limite / 1000} s; a primeira decisão foi {(decisao is null ? "nenhuma" : $"{decisao["de"]}→{decisao["para"]} ({decisao["regra"]})")}; a agenda saiu da simulada");
                return;
            }

            // A janela, amostrada de outro processo, até a travessia completar: sempre na união das áreas úteis, e montada
            // sobre a borda em ao menos uma amostra.
            int amostras = 0, fora = 0, montadas = 0;
            Nativo.RECT? primeiraFora = null;
            EventoBuzzy? completa = null;
            var relogio = Stopwatch.StartNew();
            while (relogio.ElapsedMilliseconds < 8000)
            {
                Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
                amostras++;
                var ret = new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom);
                if (!Passagens.NaUniaoDasAreasUteis(topologia, ret))
                {
                    fora++;
                    primeiraFora ??= r;
                }
                if (topologia.Monitores.Count(m => m.AreaUtil.Intersecta(ret)) > 1) montadas++;
                if (amostras % 8 == 0 && (completa = LogDoBuzzy.Desde(inicio).FirstOrDefault(e => e.Chave == "NUCLEO" && e["regra"] == "WALKING: travessia completa")) is not null) break;
                Thread.Sleep(4);
            }
            completa ??= LogDoBuzzy.Desde(inicio).FirstOrDefault(e => e.Chave == "NUCLEO" && e["regra"] == "WALKING: travessia completa");

            // Depois: ele segue o resto do percurso e para; a posição registrada é a do vizinho, com os pés no chão.
            EventoBuzzy? parou = completa is null ? null : LogDoBuzzy.Esperar(inicio, e => e.Chave == "NUCLEO" && e["para"] == "Idle" && e["de"] == "Walking", 10000);
            Thread.Sleep(300);
            Nativo.RECT final = Nativo.Retangulo(_hBuzzy);
            var finalPx = new RetanguloPx(final.Left, final.Top, final.Right, final.Bottom);
            MonitorDoDesktop? destino = topologia.Monitores.FirstOrDefault(m => m.Chave == escolha.Destino);
            bool noDestino = destino is not null && destino.AreaUtil.Contem(finalPx) && final.Bottom == destino.AreaUtil.Base;
            EventoBuzzy? posicao = LogDoBuzzy.Desde(inicio).LastOrDefault(e => e.Chave == "POSICAO" && e.Campos.ContainsKey("gdi"));
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);
            bool ok = completa is not null && parou is not null && fora == 0 && montadas > 0 && noDestino && posicao?["gdi"] == escolha.Destino && frente && !desativou;
            Registrar(Criterio, ok,
                $"semente {escolha.Semente}; começou={comecou is not null}; completou={completa is not null}; parou={parou is not null}; {amostras} amostras da janela, {fora} fora da união das áreas úteis"
                + (primeiraFora is { } pf ? $" (a primeira em {pf})" : "") + $", {montadas} montadas sobre a borda; janela final {final} no {escolha.Destino}={noDestino}; "
                + $"POSICAO final no monitor {posicao?["gdi"] ?? "nenhuma"}; receptor na frente={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("T1");
        }
    }

    /// <summary>Uma semente para o T1: a primeira decisão é ir ao outro monitor, e a travessia completa cedo.</summary>
    private sealed record EscolhaDaTravessia(ulong Semente, TimeSpan Atraso, TimeSpan AteCompletar, string Destino);

    /// <summary>
    /// Semente em que, simulando o núcleo real com a topologia lida agora (a mesma configuração do app), a primeira decisão,
    /// em até 12 s, é ir ao outro monitor, e a travessia andando completa em até 60 s depois dela.
    /// </summary>
    private static EscolhaDaTravessia? EscolherSementeParaAtravessar(Topologia topologia)
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoApp;
        for (ulong semente = 1; semente < 20000; semente++)
        {
            var nucleo = new Nucleo(cfg, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao));
            AgendarDecisao? agenda = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (agenda is null || agenda.Atraso > TimeSpan.FromSeconds(12)) continue;
            nucleo.Enfileirar(new AutonomyTimer(agenda.Geracao));
            nucleo.Processar();
            if (nucleo.Estado.Estado != Estado.Walking || !nucleo.Estado.Movimento.QuerAtravessar) continue;
            for (int passo = 1; passo <= 60 * cfg.PassosPorSegundo; passo++)
            {
                bool atravessando = nucleo.Estado.Movimento.Travessia is not null;
                nucleo.Enfileirar(new Tick());
                nucleo.Processar();
                if (atravessando && nucleo.Estado.Movimento.Travessia is null && nucleo.Estado.Lugar is { } lugar && lugar.Monitor.Chave != topologia.Principal.Chave)
                    return new EscolhaDaTravessia(semente, agenda.Atraso, TimeSpan.FromSeconds((double)passo / cfg.PassosPorSegundo), lugar.Monitor.Chave);
                if (nucleo.Estado.Estado != Estado.Walking) break;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ S2 e S7: soltar, fechar e reabrir

    private void F5SoltarFecharEReabrir(Topologia topologia)
    {
        const string Criterio = "S2 e S7 — soltar no outro monitor (em x negativo, se houver), fechar e reabrir: ele volta ao mesmo retângulo, restaurado pela chave, sem tirar o foco do aplicativo em uso";
        MonitorDoDesktop alvo = topologia.Monitores.FirstOrDefault(m => m.Tela.Esquerda < 0) ?? topologia.Monitores.FirstOrDefault(m => !m.Principal) ?? topologia.Principal;
        TamanhoPx tamanho = SpriteDoApp.ParaPixels(alvo.Dpi);
        var ancora = new PontoPx(alvo.AreaUtil.Esquerda + alvo.AreaUtil.Largura / 4, alvo.AreaUtil.Base);
        var esperado = new Nativo.RECT { Left = ancora.X - tamanho.Largura / 2, Top = ancora.Y - tamanho.Altura, Right = ancora.X - tamanho.Largura / 2 + tamanho.Largura, Bottom = ancora.Y };
        _rel.Linha($"   S2/S7: destino {alvo.Chave}{(alvo.Tela.Esquerda < 0 ? ", em x negativo" : alvo.Principal ? " (o principal: não há outro monitor)" : "")}, âncora {ancora}, janela esperada {esperado}");

        AbrirBuzzyDoTamagotchi(SementeCalma(topologia), pausado: true, "S2/S7");
        try
        {
            int marcaR = _logReceptor.Contar();
            long marca = LogDoBuzzy.Marca();
            Nativo.RECT inicio = Nativo.Retangulo(_hBuzzy);
            Nativo.POINT pegar;
            try
            {
                pegar = PontoDoCorpo();
                _inj.Pressionar(pegar.X, pegar.Y, _hBuzzy);
            }
            catch (CliqueRecusado e)
            {
                Registrar(Criterio, Inconclusivo, $"o pressionar foi recusado: {e.Message}");
                return;
            }
            // A pegada: do cursor à âncora (o meio da base da janela); o cursor vai à âncora de destino mais a pegada.
            int dx = pegar.X - (inicio.Left + inicio.Largura / 2), dy = pegar.Y - inicio.Bottom;
            var soltar = new Nativo.POINT(ancora.X + dx, ancora.Y + dy);
            const int Passos = 40;
            for (int i = 1; i <= Passos; i++)
            {
                _inj.MoverSegurando(pegar.X + (soltar.X - pegar.X) * i / Passos, pegar.Y + (soltar.Y - pegar.Y) * i / Passos);
                Thread.Sleep(12);
            }
            _inj.SoltarEsquerdo(soltar.X, soltar.Y);
            EventoBuzzy? soltou = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["evento"] == "DragEnd", 2000);
            bool parado = LogDoBuzzy.Esperar(marca, e => e.Chave == "NUCLEO" && e["para"] == "Idle", 5000) is not null;
            Thread.Sleep(300);
            Nativo.RECT solto = Nativo.Retangulo(_hBuzzy);
            EventoBuzzy? gravado = LogDoBuzzy.Esperar(marca, e => e.Chave == "CONFIG" && e["gravado"] == "sim", 8000);
            (bool frente, bool desativou) = FocoNoReceptor(marcaR);
            FecharBuzzy("S2/S7, antes de reabrir");

            AbrirBuzzyDoTamagotchi(SementeCalma(topologia), pausado: true, "S2/S7 reaberto", limpar: false);
            EventoBuzzy? lido = LogDoBuzzy.Desde(_inicioLogBuzzy).FirstOrDefault(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"));
            EventoBuzzy? carga = LogDoBuzzy.Desde(_inicioLogBuzzy).FirstOrDefault(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Booting");
            Nativo.RECT reaberto = Nativo.Retangulo(_hBuzzy);
            bool ok = soltou is not null && parado && solto.Equals(esperado) && gravado is not null && lido?["lido"] == "principal"
                && carga?["regra"].Contains("restaurada pela chave", StringComparison.Ordinal) == true && reaberto.Equals(esperado) && frente && !desativou;
            Registrar(Criterio, ok,
                $"arraste SINTÉTICO até {soltar}: DRAG_END={soltou is not null}, parado={parado}, janela {solto} (esperada {esperado}); gravado={gravado is not null}"
                + $"{(gravado is null ? "" : $" ({gravado["motivo"]}, {gravado["bytes"]} bytes)")}; reaberto: lido={lido?["lido"] ?? "nada"}, carga \"{carga?["regra"] ?? "nenhuma"}\", janela {reaberto}; "
                + $"receptor na frente depois do arraste={frente}; desativado={desativou}");
        }
        finally
        {
            FecharBuzzy("S2/S7");
        }
    }
}
