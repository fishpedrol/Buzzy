namespace Buzzy.App.Composicao;

// Motivos: mensagens separadas por vírgula, com ",+k" quando k passaram do teto.
// Os dois campos vão pro log TOPOLOGIA.
internal readonly record struct PedidoDeReleitura(string Motivos, bool NovaTentativaSeFalhar);

// Quando reler os monitores depois das mensagens do Windows. Relógio e agendador
// injetados pra testar sem janela. Só disparos únicos; parado, nenhum timer.
// - Releitura em max(nãoAntesDe, min(última + 300 ms, primeira + 1 s)): junta a rajada,
//   o teto impede adiar pra sempre, e nãoAntesDe (espera da retomada) vale sobre os dois.
// - A rajada acaba quando a releitura sai; mensagem no meio dela (ex.: WM_DPICHANGED da
//   janela que acabou de mudar de monitor) começa outra.
// - Leitura incoerente mantém a topologia anterior e tenta de novo em 500 ms, 1 s e 2 s;
//   depois desiste até a próxima mensagem.
// - Toda releitura publicada (re)arma a conferência do lugar das janelas 1,5 s depois:
//   o Windows pode devolver uma janela ao monitor reconectado depois da releitura.
// - WM_DPICHANGED da própria janela pode entrar em ciclo quando ela fica entre monitores
//   de DPI diferente. Depois de 3 rodadas seguidas só dela (até 5 s entre cada), o próximo
//   é ignorado; qualquer outra mensagem, ou 5 s sem rodada, zera a conta.
// Só na thread da interface.
internal sealed class AgendaDaReleitura
{
    // Os valores de tempo abaixo são provisórios, ainda não calibrados.
    internal static readonly TimeSpan Agrupamento = TimeSpan.FromMilliseconds(300);

    internal static readonly TimeSpan Teto = TimeSpan.FromSeconds(1);

    internal static readonly IReadOnlyList<TimeSpan> EsperasDeNovaTentativa = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    internal static readonly TimeSpan ReafirmacaoTardia = TimeSpan.FromMilliseconds(1500);

    // Sem teto, leitura incoerente com mensagens sem fim fazia a lista e a linha
    // TOPOLOGIA do log crescerem sem limite. O excedente só é contado.
    internal const int MaximoDeMotivos = 32;

    internal const int MaximoDeRodadasDaPropriaJanela = 3;

    internal static readonly TimeSpan IntervaloDasRodadasDaPropriaJanela = TimeSpan.FromSeconds(5);

    private readonly Func<TimeSpan> _agora;
    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Func<PedidoDeReleitura, bool> _reler;
    private readonly Action _reafirmar;

    // Da rajada em curso e de uma leitura incoerente esperando nova tentativa.
    private readonly List<string> _motivos = [];

    private int _motivosAMais;

    // Nulo sem rajada (parado ou esperando nova tentativa).
    private TimeSpan? _inicioDaRajada;

    // Instante absoluto no relógio de _agora; já vencido, não pesa.
    private TimeSpan _naoAntesDe = TimeSpan.MinValue;

    private TimeSpan _prazo;

    private Action? _cancelarReleitura;
    private Action? _cancelarReafirmacao;

    // Falhas seguidas desde a última mensagem; escolhe a espera da nova tentativa.
    private int _falhas;

    private bool _parada;

    private bool _soDaPropriaJanela;

    private int _rodadasDaPropriaJanela;

    private TimeSpan _ultimaRodadaDaPropriaJanela;

    // agora: relógio monotônico. reler devolve se a leitura foi coerente e publicada.
    internal AgendaDaReleitura(Func<TimeSpan> agora, Func<TimeSpan, Action, Action> agendarUmaVez, Func<PedidoDeReleitura, bool> reler, Action reafirmar)
    {
        ArgumentNullException.ThrowIfNull(agora);
        ArgumentNullException.ThrowIfNull(agendarUmaVez);
        ArgumentNullException.ThrowIfNull(reler);
        ArgumentNullException.ThrowIfNull(reafirmar);
        _agora = agora;
        _agendarUmaVez = agendarUmaVez;
        _reler = reler;
        _reafirmar = reafirmar;
    }

    internal bool ReleituraPendente => _cancelarReleitura is not null;

    internal bool ReafirmacaoPendente => _cancelarReafirmacao is not null;

    // Consultar antes de avisar o árbitro de eventos: senão ele fica esperando uma
    // releitura que não vem.
    internal bool IgnoraAPropriaJanela
        => _rodadasDaPropriaJanela >= MaximoDeRodadasDaPropriaJanela && _agora() - _ultimaRodadaDaPropriaJanela < IntervaloDasRodadasDaPropriaJanela;

    // naoAntesDe: a releitura não sai antes de agora + isso (vale o maior pedido).
    // daPropriaJanela: WM_DPICHANGED da janela do personagem.
    internal void Agendar(string motivo, TimeSpan naoAntesDe = default, bool daPropriaJanela = false)
    {
        ArgumentNullException.ThrowIfNull(motivo);
        if (_parada) return;
        if (daPropriaJanela && IgnoraAPropriaJanela) return;
        if (!daPropriaJanela) _rodadasDaPropriaJanela = 0;
        TimeSpan agora = _agora();
        _soDaPropriaJanela = (_motivos.Count == 0 && _motivosAMais == 0 || _soDaPropriaJanela) && daPropriaJanela;
        if (_motivos.Count < MaximoDeMotivos) _motivos.Add(motivo);
        else _motivosAMais++;
        _falhas = 0;
        TimeSpan inicio = _inicioDaRajada ??= agora;
        if (naoAntesDe > TimeSpan.Zero && agora + naoAntesDe > _naoAntesDe) _naoAntesDe = agora + naoAntesDe;
        TimeSpan prazo = Max(_naoAntesDe, Min(agora + Agrupamento, inicio + Teto));
        // Mesmo prazo já agendado (o teto decidiu): não precisa rearmar.
        if (_cancelarReleitura is not null && prazo == _prazo) return;
        Armar(prazo, agora);
    }

    internal void Parar()
    {
        _parada = true;
        _motivos.Clear();
        _motivosAMais = 0;
        _inicioDaRajada = null;
        CancelarReleitura();
        CancelarReafirmacao();
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private void Armar(TimeSpan prazo, TimeSpan agora)
    {
        CancelarReleitura();
        _prazo = prazo;
        _cancelarReleitura = _agendarUmaVez(prazo > agora ? prazo - agora : TimeSpan.Zero, AoDisparar);
    }

    private void AoDisparar()
    {
        _cancelarReleitura = null;
        if (_parada) return;

        // A rajada acaba aqui: mensagem que chegar durante a releitura começa outra.
        _inicioDaRajada = null;
        string[] motivos = [.. _motivos];
        int aMais = _motivosAMais;
        _motivos.Clear();
        _motivosAMais = 0;
        bool novaTentativa = _falhas < EsperasDeNovaTentativa.Count;
        string texto = string.Join(",", motivos) + (aMais > 0 ? $",+{aMais}" : "");
        bool publicada = _reler(new PedidoDeReleitura(texto, novaTentativa));
        if (_parada) return;

        if (publicada)
        {
            _falhas = 0;
            TimeSpan fim = _agora();
            if (!_soDaPropriaJanela) _rodadasDaPropriaJanela = 0;
            else
            {
                if (fim - _ultimaRodadaDaPropriaJanela >= IntervaloDasRodadasDaPropriaJanela) _rodadasDaPropriaJanela = 0;
                _rodadasDaPropriaJanela++;
                _ultimaRodadaDaPropriaJanela = fim;
            }
            CancelarReafirmacao();
            _cancelarReafirmacao = _agendarUmaVez(ReafirmacaoTardia, AoReafirmar);
            return;
        }

        // Incoerente: a topologia anterior continua. Os motivos voltam pra fila, mais antigos primeiro.
        _motivos.InsertRange(0, motivos);
        _motivosAMais += aMais;
        if (_motivos.Count > MaximoDeMotivos)
        {
            _motivosAMais += _motivos.Count - MaximoDeMotivos;
            _motivos.RemoveRange(MaximoDeMotivos, _motivos.Count - MaximoDeMotivos);
        }
        if (_cancelarReleitura is not null) return; // mensagem no meio já agendou a próxima
        if (!novaTentativa)
        {
            // Desiste até a próxima mensagem.
            _motivos.Clear();
            _motivosAMais = 0;
            return;
        }
        TimeSpan agora = _agora();
        Armar(agora + EsperasDeNovaTentativa[_falhas++], agora);
    }

    private void AoReafirmar()
    {
        _cancelarReafirmacao = null;
        if (_parada) return;
        _reafirmar();
    }

    private void CancelarReleitura()
    {
        Action? cancelar = _cancelarReleitura;
        _cancelarReleitura = null;
        cancelar?.Invoke();
    }

    private void CancelarReafirmacao()
    {
        Action? cancelar = _cancelarReafirmacao;
        _cancelarReafirmacao = null;
        cancelar?.Invoke();
    }
}
