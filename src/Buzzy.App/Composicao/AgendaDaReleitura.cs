namespace Buzzy.App.Composicao;

/// <summary>Uma releitura agrupada da topologia, como a <see cref="AgendaDaReleitura"/> a pede a quem relê.</summary>
/// <param name="Motivos">
/// As mensagens agrupadas, na ordem em que chegaram, separadas por vírgula (vão ao log TOPOLOGIA): no máximo
/// <see cref="AgendaDaReleitura.MaximoDeMotivos"/>, com <c>,+k</c> no fim quando outras k passaram do teto.
/// </param>
/// <param name="NovaTentativaSeFalhar">Se uma leitura incoerente agora ainda terá nova tentativa (vai ao log TOPOLOGIA).</param>
internal readonly record struct PedidoDeReleitura(string Motivos, bool NovaTentativaSeFalhar);

/// <summary>
/// Quando a raiz relê a topologia depois das mensagens do Windows (Fase 5, passo P9; DEC-030; crítica, C11; ARCHITECTURE.md
/// 2.4 e 2.8), com o relógio e o agendador injetados, para ser testada sem janela:
/// <list type="bullet">
/// <item>cada mensagem pede a releitura (<see cref="Agendar"/>), que sai num disparo único em
/// <c>max(nãoAntesDe, min(última + 300 ms, primeira + 1 s))</c>: o agrupamento junta as mensagens de uma rajada, o teto
/// impede que uma rajada sem fim adie a releitura para sempre, e "não antes de" é a espera que a retomada vai pedir (passo
/// P10), que prevalece sobre os dois;</item>
/// <item>a rajada acaba quando a releitura sai: uma mensagem depois disso, inclusive no meio da própria releitura (o
/// WM_DPICHANGED da janela que a releitura levou a um monitor de outro DPI), começa outra;</item>
/// <item>uma leitura incoerente mantém a topologia anterior e tenta de novo em 500 ms, 1 s e 2 s, com os mesmos motivos;
/// depois, desiste até a próxima mensagem. Uma mensagem durante a espera começa outra rajada, com os motivos pendentes e as
/// tentativas do zero. Os motivos guardados têm teto (<see cref="MaximoDeMotivos"/>): os outros só são contados;</item>
/// <item>toda releitura publicada (re)arma a conferência tardia do lugar das janelas, 1,5 s depois (D14 do desenho dos
/// monitores): o Windows pode devolver uma janela ao monitor reconectado depois da releitura, e quem decide o lugar é o
/// núcleo. Uma releitura publicada antes do disparo o adia: no máximo uma conferência por rajada;</item>
/// <item>o WM_DPICHANGED da própria janela do personagem (passo P14): a releitura reafirma o lugar dela, e, com a janela
/// montada entre monitores de DPI diferente, isso pode trazer outro WM_DPICHANGED, e assim por diante. Depois de
/// <see cref="MaximoDeRodadasDaPropriaJanela"/> releituras publicadas seguidas pedidas só por ele, cada uma até
/// <see cref="IntervaloDasRodadasDaPropriaJanela"/> depois da anterior, o próximo pedido dele é ignorado
/// (<see cref="IgnoraAPropriaJanela"/>); qualquer outra mensagem, ou esse intervalo sem rodadas, zera a conta.</item>
/// </list>
/// Nada é periódico: só disparos únicos depois de uma mensagem; em repouso, nenhum. <see cref="Parar"/>, no encerramento,
/// cancela os dois, e nada mais é agendado. Só na thread da interface.
/// </summary>
internal sealed class AgendaDaReleitura
{
    /// <summary>Espera depois da última mensagem de uma rajada (ARCHITECTURE.md 2.4). Provisória até o protótipo P5.</summary>
    internal static readonly TimeSpan Agrupamento = TimeSpan.FromMilliseconds(300);

    /// <summary>Espera máxima de uma rajada, desde a primeira mensagem (D15 do desenho dos monitores). Provisória até P5.</summary>
    internal static readonly TimeSpan Teto = TimeSpan.FromSeconds(1);

    /// <summary>Esperas das novas tentativas depois de uma leitura incoerente.</summary>
    internal static readonly IReadOnlyList<TimeSpan> EsperasDeNovaTentativa = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    /// <summary>Espera da conferência tardia, depois de uma releitura publicada (D14 do desenho dos monitores). Provisória até P5.</summary>
    internal static readonly TimeSpan ReafirmacaoTardia = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Quantos motivos uma releitura leva, no máximo, na ordem em que chegaram; os seguintes só são contados (",+k" no fim do
    /// texto). Sem o teto, a leitura incoerente persistente com mensagens sem fim fazia a lista e a linha TOPOLOGIA do log
    /// crescerem sem limite (revisão de segurança do bloco P6-P9, achado 4).
    /// </summary>
    internal const int MaximoDeMotivos = 32;

    /// <summary>Quantas releituras publicadas seguidas pedidas só pela própria janela valem, antes de ignorar as seguintes (P14).</summary>
    internal const int MaximoDeRodadasDaPropriaJanela = 3;

    /// <summary>Até quanto tempo depois da anterior uma rodada da própria janela conta como seguida (P14). Provisório até o P6.</summary>
    internal static readonly TimeSpan IntervaloDasRodadasDaPropriaJanela = TimeSpan.FromSeconds(5);

    private readonly Func<TimeSpan> _agora;
    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Func<PedidoDeReleitura, bool> _reler;
    private readonly Action _reafirmar;

    /// <summary>
    /// Os motivos que a próxima releitura leva: os da rajada em curso e os de uma leitura incoerente à espera de nova
    /// tentativa, no máximo <see cref="MaximoDeMotivos"/>.
    /// </summary>
    private readonly List<string> _motivos = [];

    /// <summary>Os motivos que passaram do teto e só são contados.</summary>
    private int _motivosAMais;

    /// <summary>A primeira mensagem da rajada em curso; nula sem rajada (em repouso, ou à espera de uma nova tentativa).</summary>
    private TimeSpan? _inicioDaRajada;

    /// <summary>O instante antes do qual a releitura não sai (absoluto, no relógio de <see cref="_agora"/>); já vencido, não pesa.</summary>
    private TimeSpan _naoAntesDe = TimeSpan.MinValue;

    /// <summary>O vencimento da releitura agendada.</summary>
    private TimeSpan _prazo;

    private Action? _cancelarReleitura;
    private Action? _cancelarReafirmacao;

    /// <summary>Leituras incoerentes seguidas desde a última mensagem: escolhem a espera da nova tentativa.</summary>
    private int _falhas;

    private bool _parada;

    /// <summary>Se todos os motivos guardados para a próxima releitura vieram da própria janela (P14).</summary>
    private bool _soDaPropriaJanela;

    /// <summary>Releituras publicadas seguidas pedidas só pela própria janela (P14).</summary>
    private int _rodadasDaPropriaJanela;

    /// <summary>Quando saiu a última delas.</summary>
    private TimeSpan _ultimaRodadaDaPropriaJanela;

    /// <param name="agora">O relógio monotônico.</param>
    /// <param name="agendarUmaVez">Agenda um disparo único e devolve o que o cancela (<see cref="DisparoUnico.NoDispatcher"/>).</param>
    /// <param name="reler">Lê a topologia e, se a leitura for coerente, publica-a ao núcleo; devolve se publicou.</param>
    /// <param name="reafirmar">A conferência tardia do lugar das janelas.</param>
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

    /// <summary>Se há uma releitura agendada (de uma rajada ou de uma nova tentativa).</summary>
    internal bool ReleituraPendente => _cancelarReleitura is not null;

    /// <summary>Se a conferência tardia está agendada.</summary>
    internal bool ReafirmacaoPendente => _cancelarReafirmacao is not null;

    /// <summary>
    /// Se um pedido da própria janela agora seria ignorado (P14): já houve <see cref="MaximoDeRodadasDaPropriaJanela"/>
    /// rodadas seguidas só dela, a última há menos de <see cref="IntervaloDasRodadasDaPropriaJanela"/>. Quem recebe a mensagem
    /// consulta antes de avisar o árbitro dos eventos do sistema, que esperaria por uma releitura que não vem.
    /// </summary>
    internal bool IgnoraAPropriaJanela
        => _rodadasDaPropriaJanela >= MaximoDeRodadasDaPropriaJanela && _agora() - _ultimaRodadaDaPropriaJanela < IntervaloDasRodadasDaPropriaJanela;

    /// <summary>
    /// Uma mensagem que pode ter mudado a topologia pede a releitura: entra na rajada em curso, ou começa outra, e as
    /// tentativas recomeçam. Com <paramref name="naoAntesDe"/>, a releitura não sai antes de agora + essa espera (o maior
    /// pedido vale). <paramref name="daPropriaJanela"/> marca o WM_DPICHANGED da janela do personagem (P14): no limite
    /// (<see cref="IgnoraAPropriaJanela"/>), ele é ignorado; outra mensagem zera a conta.
    /// </summary>
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
        // Com o mesmo prazo já agendado (o teto decide), nenhum disparo novo.
        if (_cancelarReleitura is not null && prazo == _prazo) return;
        Armar(prazo, agora);
    }

    /// <summary>Encerramento: cancela a releitura e a conferência agendadas, e nada mais é agendado.</summary>
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

        // A rajada acaba aqui: uma mensagem que chegue no meio da própria releitura começa outra, com os motivos dela.
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
            // A conta das rodadas da própria janela (P14): seguidas, até o intervalo depois da anterior.
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

        // Incoerente: a topologia anterior continua valendo, e os motivos esperam a próxima leitura, os mais antigos primeiro
        // e com o mesmo teto.
        _motivos.InsertRange(0, motivos);
        _motivosAMais += aMais;
        if (_motivos.Count > MaximoDeMotivos)
        {
            _motivosAMais += _motivos.Count - MaximoDeMotivos;
            _motivos.RemoveRange(MaximoDeMotivos, _motivos.Count - MaximoDeMotivos);
        }
        if (_cancelarReleitura is not null) return; // uma mensagem no meio dela já agendou a próxima
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
