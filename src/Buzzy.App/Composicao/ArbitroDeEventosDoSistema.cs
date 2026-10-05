using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// Ordena os eventos de sessão com a releitura da topologia (Fase 5, passo P10; DEC-031).
/// Bloqueio e suspensão chegam ao núcleo na hora; desbloqueio e retomada esperam uma leitura
/// publicada para que o núcleo reapareça usando a topologia atual. Só vive na thread da interface.
/// </summary>
internal sealed class ArbitroDeEventosDoSistema
{
    /// <summary>Espera mínima provisória após retomar o Windows, até a calibração do protótipo P5.</summary>
    internal static readonly TimeSpan EsperaMinimaDaRetomada = TimeSpan.FromMilliseconds(1500);

    private readonly Action<Evento> _enviar;
    private readonly List<Evento> _aposReleitura = [];
    private bool _releituraSinalizada;
    private bool _parado;

    internal ArbitroDeEventosDoSistema(Action<Evento> enviar)
    {
        ArgumentNullException.ThrowIfNull(enviar);
        _enviar = enviar;
    }

    /// <summary>
    /// Recebe um evento do sistema. Devolve o prazo mínimo para a agenda da topologia; somente
    /// <see cref="Resumed"/> pede 1,5 s. Desbloqueio e retomada ficam retidos até a leitura publicada.
    /// </summary>
    internal TimeSpan Sinalizar(Evento evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (_parado) return TimeSpan.Zero;

        switch (evento)
        {
            case SessionLocked:
                RemoverPendente<SessionUnlocked>();
                _enviar(evento);
                return TimeSpan.Zero;

            case Suspending:
                RemoverPendente<Resumed>();
                _enviar(evento);
                return TimeSpan.Zero;

            case SessionUnlocked:
                _releituraSinalizada = true;
                EnfileirarUmaVez(evento);
                return TimeSpan.Zero;

            case Resumed:
                _releituraSinalizada = true;
                EnfileirarUmaVez(evento);
                return EsperaMinimaDaRetomada;

            default:
                throw new ArgumentException("O árbitro aceita somente eventos de bloqueio, desbloqueio, suspensão e retomada.", nameof(evento));
        }
    }

    /// <summary>Marca a mensagem de topologia entre o log MENSAGEM e o pedido à agenda.</summary>
    internal TimeSpan SinalizarMudancaDeTopologia()
    {
        if (!_parado) _releituraSinalizada = true;
        return TimeSpan.Zero;
    }

    /// <summary>Libera os eventos retidos, na ordem recebida, somente depois de uma leitura coerente e publicada.</summary>
    internal void TopologiaRelida(bool publicada)
    {
        if (_parado || !publicada) return;
        if (!_releituraSinalizada) return;

        _releituraSinalizada = false;
        Evento[] liberar = [.. _aposReleitura];
        _aposReleitura.Clear();
        foreach (Evento evento in liberar) _enviar(evento);
    }

    /// <summary>Encerra o árbitro e descarta eventos ainda não liberados.</summary>
    internal void Parar()
    {
        _parado = true;
        _releituraSinalizada = false;
        _aposReleitura.Clear();
    }

    private void EnfileirarUmaVez(Evento evento)
    {
        if (_aposReleitura.Any(pendente => pendente.GetType() == evento.GetType())) return;
        _aposReleitura.Add(evento);
    }

    private void RemoverPendente<TEvento>() where TEvento : Evento =>
        _aposReleitura.RemoveAll(evento => evento is TEvento);
}
