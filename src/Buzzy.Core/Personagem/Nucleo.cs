namespace Buzzy.Core.Personagem;

/// <summary>
/// O núcleo do personagem com a fila de eventos (ARCHITECTURE.md 2.2 e 2.3). A raiz de
/// composição enfileira eventos normalizados e processa a fila; os eventos saem do mais
/// prioritário para o menos, e na ordem de chegada dentro da mesma prioridade. Não é seguro
/// para uso por várias threads: a raiz só o usa na thread da interface.
/// </summary>
public sealed class Nucleo
{
    private readonly List<(Evento Evento, long Ordem)> _fila = [];
    private long _ordem;

    public Nucleo(ConfiguracaoDoNucleo configuracao, ulong semente)
        : this(configuracao, EstadoDoNucleo.Inicial(semente))
    {
    }

    public Nucleo(ConfiguracaoDoNucleo configuracao, EstadoDoNucleo estado)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(estado);
        Configuracao = configuracao;
        Estado = estado;
    }

    public ConfiguracaoDoNucleo Configuracao { get; }

    public EstadoDoNucleo Estado { get; private set; }

    public Retrato Retrato => Estado.Retrato();

    /// <summary>Eventos na fila, ainda não aplicados.</summary>
    public int Pendentes => _fila.Count;

    /// <summary>Eventos autônomos descartados por chegarem com o usuário no controle.</summary>
    public long Descartados { get; private set; }

    /// <summary>
    /// Enfileira o evento. Um evento autônomo que chega enquanto o personagem está sob
    /// controle do usuário é descartado, não enfileirado (ARCHITECTURE.md 2.3).
    /// </summary>
    /// <returns>Falso se o evento foi descartado.</returns>
    public bool Enfileirar(Evento evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (DescartaAutonomo(evento))
        {
            Descartados++;
            return false;
        }
        _fila.Add((evento, _ordem++));
        return true;
    }

    /// <summary>
    /// Aplica todos os eventos pendentes e devolve os efeitos, na ordem em que devem ser
    /// executados. <paramref name="aoAplicar"/> recebe cada evento aplicado e o resultado.
    /// </summary>
    public IReadOnlyList<Efeito> Processar(Action<Evento, Resultado>? aoAplicar = null)
    {
        var efeitos = new List<Efeito>();
        while (_fila.Count > 0)
        {
            int indice = IndiceDoMaisPrioritario();
            Evento evento = _fila[indice].Evento;
            _fila.RemoveAt(indice);

            // O estado pode ter mudado desde que o evento entrou na fila.
            if (DescartaAutonomo(evento))
            {
                Descartados++;
                continue;
            }

            Resultado resultado = Maquina.Aplicar(Estado, evento, Configuracao);
            Estado = resultado.Estado;
            efeitos.AddRange(resultado.Efeitos);
            aoAplicar?.Invoke(evento, resultado);
        }
        return efeitos;
    }

    private bool DescartaAutonomo(Evento evento)
        => evento.Origem == Origem.Autonomo && Estado.Estado.ControladoPeloUsuario();

    private int IndiceDoMaisPrioritario()
    {
        int melhor = 0;
        for (int i = 1; i < _fila.Count; i++)
        {
            (Evento e, long ordem) = _fila[i];
            (Evento m, long ordemDoMelhor) = _fila[melhor];
            if (e.Origem > m.Origem || (e.Origem == m.Origem && ordem < ordemDoMelhor)) melhor = i;
        }
        return melhor;
    }
}
