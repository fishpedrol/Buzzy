using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Ordena os eventos de sessão com a releitura dos monitores. Bloqueio e suspensão
// vão pro núcleo na hora; desbloqueio e retomada esperam uma leitura publicada, pro
// personagem reaparecer já na topologia atual. Só na thread da interface.
internal sealed class ArbitroDeEventosDoSistema
{
    // Valor provisório, ainda não calibrado.
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

    // Devolve a espera mínima pra agenda da topologia (só Resumed pede 1,5 s).
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

    // Chamado entre o log MENSAGEM e o pedido à agenda.
    internal TimeSpan SinalizarMudancaDeTopologia()
    {
        if (!_parado) _releituraSinalizada = true;
        return TimeSpan.Zero;
    }

    // Solta os eventos retidos, na ordem em que chegaram.
    internal void TopologiaRelida(bool publicada)
    {
        if (_parado || !publicada) return;
        if (!_releituraSinalizada) return;

        _releituraSinalizada = false;
        Evento[] liberar = [.. _aposReleitura];
        _aposReleitura.Clear();
        foreach (Evento evento in liberar) _enviar(evento);
    }

    // Descarta o que ainda estava retido.
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
