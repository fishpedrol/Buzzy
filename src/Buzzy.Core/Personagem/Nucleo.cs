namespace Buzzy.Core.Personagem;

// Fila de eventos do personagem: sai o mais prioritário primeiro e, empatando,
// o que chegou antes. Não é thread-safe; só a thread da UI mexe aqui.
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

    public int Pendentes => _fila.Count;

    // Autônomos que chegaram com o personagem sob controle do usuário.
    public long Descartados { get; private set; }

    // Evento autônomo com o usuário no controle é descartado (devolve false).
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

    // Esvazia a fila e devolve os efeitos já na ordem de execução.
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
