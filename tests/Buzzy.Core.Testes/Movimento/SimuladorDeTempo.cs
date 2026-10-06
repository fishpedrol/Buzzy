using Buzzy.Core.Personagem;

namespace Buzzy.Core.Testes.Movimento;

/// <summary>
/// Faz o papel da raiz de composição num relógio virtual, sem janela: executa os efeitos de
/// tempo do núcleo (relógio de passo fixo, agenda autônoma e temporizador da onda do tamagotchi)
/// e entrega <see cref="Tick"/>, <see cref="AutonomyTimer"/> e <see cref="ItemEffectTimer"/>
/// quando venceriam. Cada passo do relógio virtual dura um passo fixo do núcleo (1/60 s). Usado
/// pelos testes da Fase 4 e do tamagotchi para simular minutos de comportamento autônomo de
/// forma determinística.
/// </summary>
internal sealed class SimuladorDeTempo
{
    private readonly Nucleo _nucleo;
    private readonly double _passoMs;
    private double _agoraMs;
    private bool _relogio;
    private (double VenceEmMs, long Geracao)? _decisao;
    private (double VenceEmMs, long Geracao)? _onda;
    private (double VenceEmMs, long Geracao)? _curiosidade;

    /// <param name="preferencias">Nula, as dos cenários: a chave adulta ligada e os nove itens (o arquivo também entra na verificação de tela, sem o Cenario).</param>
    /// <param name="posicaoSalva">A posição salva da carga, como a partida a lê do settings.json (Fase 5); nula, a inicial.</param>
    public SimuladorDeTempo(ConfiguracaoDoNucleo config, ulong semente, Topologia topologia, Preferencias? preferencias = null, PosicaoDoPersonagem? posicaoSalva = null)
        : this(config, semente, new Loaded(topologia, posicaoSalva, preferencias ?? Preferencias.Padrao with { AtravessarMonitores = true, ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos, ItensPorContaPropria = ConjuntoDeItens.Vazio.Com(Item.Baseado) }))
    {
    }

    /// <summary>Um simulador que começa com esta carga (com a borda do esconderijo e a marca de preso gravadas, por exemplo).</summary>
    public SimuladorDeTempo(ConfiguracaoDoNucleo config, ulong semente, Loaded carga)
    {
        ArgumentNullException.ThrowIfNull(carga);
        _nucleo = new Nucleo(config, semente);
        _passoMs = 1000.0 / config.PassosPorSegundo;
        Aplicar(carga);
    }

    private SimuladorDeTempo(Nucleo nucleo, double passoMs, double agoraMs, bool relogio, (double, long)? decisao, (double, long)? onda, (double, long)? curiosidade)
    {
        _curiosidade = curiosidade;
        _nucleo = nucleo;
        _passoMs = passoMs;
        _agoraMs = agoraMs;
        _relogio = relogio;
        _decisao = decisao;
        _onda = onda;
    }

    /// <summary>
    /// Um simulador que continua deste com o núcleo recriado pelo construtor <c>Nucleo(config, estado)</c>, a partir do
    /// estado atual mudado por <paramref name="mudar"/>: é assim que os testes do tamagotchi semeiam uma onda antes de
    /// existir quem a comece (DEC-028). O relógio virtual, o relógio de passo fixo e os temporizadores pendentes
    /// continuam os deste; os callbacks não. Este simulador não muda.
    /// </summary>
    public SimuladorDeTempo Semeado(Func<EstadoDoNucleo, EstadoDoNucleo> mudar)
    {
        ArgumentNullException.ThrowIfNull(mudar);
        return new SimuladorDeTempo(new Nucleo(_nucleo.Configuracao, mudar(_nucleo.Estado)), _passoMs, _agoraMs, _relogio, _decisao, _onda, _curiosidade);
    }

    public Nucleo Nucleo => _nucleo;

    public EstadoDoNucleo Estado => _nucleo.Estado;

    public double AgoraMs => _agoraMs;

    /// <summary>Se o relógio de passo fixo está ligado, segundo os efeitos do núcleo.</summary>
    public bool RelogioLigado => _relogio;

    /// <summary>Transições aplicadas desde a criação, na ordem.</summary>
    public List<Transicao> Transicoes { get; } = [];

    /// <summary>Chamado depois de cada evento aplicado, com o estado antes e depois.</summary>
    public Action<EstadoDoNucleo, Evento, EstadoDoNucleo>? AoAplicar { get; set; }

    /// <summary>
    /// Chamado para cada evento que a máquina aplicou, com o estado logo antes dele e o resultado (efeitos e
    /// transições): para conferir os efeitos, como o GravarPosicao.
    /// </summary>
    public Action<EstadoDoNucleo, Evento, Resultado>? AoResultado { get; set; }

    /// <summary>
    /// Responde ao pedido do vão da janela ativa (DEC-037), como o adaptador: a chave do monitor dá o vão, ou nulo. Sem
    /// resposta definida, nulo. A resposta sai logo depois do evento que pediu, como o Dispatcher faria.
    /// </summary>
    public Func<string, VaoDaJanela?>? ResponderVao { get; set; }

    /// <summary>Se o adaptador responde ao pedido do vão; falso, o pedido fica sem resposta (a guarda do núcleo vence).</summary>
    public bool ResponderAoVao { get; set; } = true;

    /// <summary>Os pedidos do vão recebidos, com a chave e a geração.</summary>
    public List<PedirVaoDaJanelaAtiva> VaosPedidos { get; } = [];

    /// <summary>Quando vence o disparo da curiosidade pendente, em ms do relógio virtual; nulo sem ele.</summary>
    public double? CuriosidadeVenceEmMs => _curiosidade?.VenceEmMs;

    /// <summary>Aplica um evento externo (gesto, comando) e executa os efeitos de tempo.</summary>
    public void Aplicar(Evento evento)
    {
        EstadoDoNucleo antes = _nucleo.Estado;
        if (!_nucleo.Enfileirar(evento)) return;
        EstadoDoNucleo anterior = antes;
        IReadOnlyList<Efeito> efeitos = _nucleo.Processar((aplicado, r) =>
        {
            Transicoes.AddRange(r.Transicoes);
            AoResultado?.Invoke(anterior, aplicado, r);
            anterior = r.Estado;
        });
        foreach (Efeito e in efeitos)
        {
            switch (e)
            {
                case LigarRelogio: _relogio = true; break;
                case DesligarRelogio: _relogio = false; break;
                case AgendarDecisao a: _decisao = (_agoraMs + a.Atraso.TotalMilliseconds, a.Geracao); break;
                case CancelarDecisao: _decisao = null; break;
                case AgendarOnda a: _onda = (_agoraMs + a.Atraso.TotalMilliseconds, a.Geracao); break;
                case CancelarOnda: _onda = null; break;
                case AgendarCuriosidade a: _curiosidade = (_agoraMs + a.Atraso.TotalMilliseconds, a.Geracao); break;
                case CancelarCuriosidade: _curiosidade = null; break;
            }
        }
        AoAplicar?.Invoke(antes, evento, _nucleo.Estado);
        foreach (PedirVaoDaJanelaAtiva pedido in efeitos.OfType<PedirVaoDaJanelaAtiva>())
        {
            VaosPedidos.Add(pedido);
            if (!ResponderAoVao) continue;
            Aplicar(new ActiveWindowSpan(pedido.Geracao, ResponderVao?.Invoke(pedido.Chave)));
        }
    }

    /// <summary>
    /// Avança o relógio virtual: a cada passo, entrega o temporizador vencido (a onda ou a agenda,
    /// o que venceu primeiro; num empate, a onda, que tem a prioridade do relógio, maior que a da
    /// agenda) e, com o relógio ligado, um <see cref="Tick"/>. Parado, pula direto para o próximo
    /// temporizador. Para antes do fim se <paramref name="parar"/> devolver verdadeiro.
    /// </summary>
    public void Avancar(TimeSpan duracao, Func<EstadoDoNucleo, bool>? parar = null)
    {
        double fim = _agoraMs + duracao.TotalMilliseconds;
        while (_agoraMs < fim)
        {
            if (parar?.Invoke(_nucleo.Estado) == true) return;
            bool decisaoVencida = _decisao is { } d && d.VenceEmMs <= _agoraMs;
            if (_onda is { } o && o.VenceEmMs <= _agoraMs && (!decisaoVencida || o.VenceEmMs <= _decisao!.Value.VenceEmMs))
            {
                _onda = null;
                Aplicar(new ItemEffectTimer(o.Geracao));
                continue;
            }
            // A curiosidade também é do relógio: num empate, antes da agenda (DEC-037).
            if (_curiosidade is { } c && c.VenceEmMs <= _agoraMs && (!decisaoVencida || c.VenceEmMs <= _decisao!.Value.VenceEmMs))
            {
                _curiosidade = null;
                Aplicar(new CuriosityTimer(c.Geracao));
                continue;
            }
            if (decisaoVencida)
            {
                long geracao = _decisao!.Value.Geracao;
                _decisao = null;
                Aplicar(new AutonomyTimer(geracao));
                continue;
            }
            if (_relogio)
            {
                Aplicar(new Tick());
                _agoraMs += _passoMs;
                continue;
            }
            double proxima = Math.Min(Math.Min(_decisao?.VenceEmMs ?? fim, _onda?.VenceEmMs ?? fim), _curiosidade?.VenceEmMs ?? fim);
            _agoraMs = Math.Min(Math.Max(proxima, _agoraMs), fim);
        }
    }

    /// <summary>Passos do relógio, um a um, enquanto ele estiver ligado (ou até <paramref name="maximo"/>).</summary>
    public int Passos(int maximo)
    {
        int n = 0;
        while (n < maximo && _relogio)
        {
            Aplicar(new Tick());
            _agoraMs += _passoMs;
            n++;
        }
        return n;
    }
}
