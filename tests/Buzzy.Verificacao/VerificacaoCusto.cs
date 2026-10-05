using System.Diagnostics;
using System.Globalization;

namespace Buzzy.Verificacao;

/// <summary>
/// O custo do observador da janela em primeiro plano com o usuário usando outro aplicativo (protótipo P7, M1 e M2; DEC-013,
/// DEC-026 item 5 e DEC-034), medido com input SINTÉTICO: o mesmo Buzzy parado (<c>--pausado</c>) duas vezes, com o
/// observador ligado e com ele desligado (<c>--sem-tela-cheia</c>), sob a mesma carga: o cursor anda dentro do aplicativo em
/// uso (o receptor), longe do Buzzy, e o teclado digita nele, por <see cref="DuracaoDaCarga"/>. A CPU do processo do Buzzy sai
/// do TotalProcessorTime dele, lido de fora; os eventos que o observador recebeu, da linha <c>TELA_CHEIA|fim</c> que ele escreve
/// ao sair (só contagens). O critério é a meta de repouso de Q-08 (DEC-011): CPU média até 0,1% de um núcleo com o observador
/// ligado. Input por SendInput não é gesto humano: a carga é SINTÉTICA, e a taxa de eventos de um usuário de verdade pode ser
/// outra.
/// </summary>
internal sealed partial class Verificacao
{
    /// <summary>Quanto dura a carga medida em cada rodada.</summary>
    private static readonly TimeSpan DuracaoDaCarga = TimeSpan.FromSeconds(60);

    /// <summary>Espera depois de abrir o Buzzy, antes de medir: a partida é o único trecho em que ele gasta CPU de verdade.</summary>
    private static readonly TimeSpan AquecimentoDoCusto = TimeSpan.FromSeconds(8);

    /// <summary>A meta de repouso de Q-08 (DEC-011): CPU média até 0,1% de um núcleo.</summary>
    private const double MetaDeRepouso = 0.1;

    internal Sumario ExecutarCusto()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            _prefixo = "Custo do observador — ";
            Carga semObservador = MedirCarga(observador: false);
            Carga comObservador = MedirCarga(observador: true);
            _prefixo = "";

            _rel.Linha($"   sem o observador: {semObservador}");
            _rel.Linha($"   com o observador: {comObservador}");
            double diferenca = comObservador.CpuPorNucleo - semObservador.CpuPorNucleo;
            Registrar("P7 (M1; para a M2, a contagem de eventos) — com o mouse e o teclado em uso em outro aplicativo, o observador da janela em primeiro plano fica dentro da meta de repouso (CPU média até 0,1% de um núcleo), por eventos e sem polling",
                comObservador.CpuPorNucleo <= MetaDeRepouso && comObservador.Eventos is not null,
                string.Create(CultureInfo.InvariantCulture,
                    $"carga SINTÉTICA de {DuracaoDaCarga.TotalSeconds:0} s por rodada; com o observador {comObservador.CpuPorNucleo:0.000}% de um núcleo, sem ele {semObservador.CpuPorNucleo:0.000}% (diferença {diferenca:+0.000;-0.000}%); eventos recebidos com o observador: {comObservador.Eventos ?? "linha TELA_CHEIA|fim não encontrada"}"));
            Registrar("P7 — sem o observador, nenhum evento da janela em primeiro plano é recebido",
                semObservador.Eventos is null,
                $"sem o observador, linha TELA_CHEIA|fim: {semObservador.Eventos ?? "nenhuma (o observador nem foi ligado)"}");

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
        _rel.Linha("   Cobertura: a carga é SINTÉTICA (cursor e teclado por SendInput no aplicativo em uso); o uso de verdade, com o jogo e com outros aplicativos, fica com o usuário, por --diagnostico.");
        _naoExercitados.Add("o custo com um usuário de verdade, jogando ou trabalhando em outros aplicativos (P7) [MANUAL]");
        return Resumir();
    }

    /// <summary>O resultado de uma rodada: a CPU média do Buzzy em % de um núcleo, a linha de eventos do observador e a carga.</summary>
    private sealed record Carga(double CpuPorNucleo, string? Eventos, int Movimentos, int Teclas, double Segundos)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"CPU {CpuPorNucleo:0.000}% de um núcleo em {Segundos:0.0} s; {Movimentos} movimentos do cursor e {Teclas} teclas; eventos: {Eventos ?? "nenhum (observador desligado)"}");
    }

    /// <summary>
    /// Uma rodada: abre o Buzzy parado, com ou sem o observador, espera o aquecimento, mede a CPU dele durante a carga
    /// SINTÉTICA no receptor e o fecha, lendo a contagem de eventos que o observador escreve ao sair.
    /// </summary>
    private Carga MedirCarga(bool observador)
    {
        _inicioLogBuzzy = LogDoBuzzy.Marca();
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy, "--pausado");
        if (observador) PerfilDaVerificacao.LigarOObservador(psi);
        ExigirNenhumBuzzyAberto();
        PerfilDaVerificacao.Limpar();
        ExigirNenhumBuzzyAberto(); // repetida imediatamente antes de iniciar
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não iniciou.");
        _inicioBuzzy = _buzzy.StartTime;
        _buzzyEncerrado = false;
        _hBuzzy = JanelaDoBuzzy(Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy")["hwnd"], "JANELA");
        Esperar(e => e.Chave == "BANDEJA" && e.Campos.ContainsKey("adicionado"), 5000, "bandeja");
        _rel.Linha($"   Buzzy aberto parado, {(observador ? "com" : "sem")} o observador: pid {_buzzy.Id}");
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("antes da carga");
        Thread.Sleep(AquecimentoDoCusto);

        // O cursor anda num retângulo dentro do receptor, do lado oposto ao Buzzy; nada passa sobre a janela dele.
        Nativo.RECT rr = Nativo.Retangulo(_hReceptor);
        Nativo.RECT rb = Nativo.Retangulo(_hBuzzy);
        int x0 = rr.Left + 60, x1 = Math.Min(rr.Left + 360, rb.Left - 60), y0 = rr.Top + 120, y1 = rr.Bottom - 120;
        if (x1 - x0 < 100 || y1 - y0 < 100) throw new FalhaDeVerificacao($"sem espaço no receptor {rr} longe do Buzzy {rb} para mover o cursor.");

        _buzzy.Refresh();
        TimeSpan cpuAntes = _buzzy.TotalProcessorTime;
        var relogio = Stopwatch.StartNew();
        int movimentos = 0, teclas = 0;
        long proximaTecla = 0;
        while (relogio.Elapsed < DuracaoDaCarga)
        {
            if (Programa.Cancelado) throw new FalhaDeVerificacao("cancelado pelo usuário.");
            // Um contorno do retângulo a cada 4 s, como uma mão que passeia com o mouse.
            double t = relogio.Elapsed.TotalSeconds % 4 / 4;
            (int x, int y) = t < 0.25 ? (x0 + (int)((x1 - x0) * t * 4), y0)
                : t < 0.5 ? (x1, y0 + (int)((y1 - y0) * (t - 0.25) * 4))
                : t < 0.75 ? (x1 - (int)((x1 - x0) * (t - 0.5) * 4), y1)
                : (x0, y1 - (int)((y1 - y0) * (t - 0.75) * 4));
            _inj.MoverSemBotao(x, y);
            movimentos++;
            // Uma tecla a cada 200 ms (cerca de 60 palavras por minuto), só com o receptor na frente.
            if (relogio.ElapsedMilliseconds >= proximaTecla)
            {
                if (!_inj.Digitar("x", () => Nativo.GetForegroundWindow() == _hReceptor))
                    throw new FalhaDeVerificacao($"o receptor saiu do primeiro plano durante a carga: a janela da frente era {Quem(Nativo.GetForegroundWindow())}; nada foi digitado.");
                _textoEsperado.Append('x');
                teclas++;
                proximaTecla += 200;
            }
            Thread.Sleep(16);
        }
        double segundos = relogio.Elapsed.TotalSeconds;
        _buzzy.Refresh();
        TimeSpan cpuDepois = _buzzy.TotalProcessorTime;
        double porNucleo = (cpuDepois - cpuAntes).TotalSeconds / segundos * 100;

        FecharBuzzy(observador ? "custo, com o observador" : "custo, sem o observador");
        EventoBuzzy? fim = LogDoBuzzy.Desde(_inicioLogBuzzy).LastOrDefault(e => e.Chave == "TELA_CHEIA" && e["fim"] == "sim");
        string? eventos = fim is null ? null : $"primeiro plano {fim["eventosPrimeiroPlano"]}, geometria {fim["eventosGeometria"]} (desde a partida)";
        return new Carga(porNucleo, eventos, movimentos, teclas, segundos);
    }
}
