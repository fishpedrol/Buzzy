using System.Windows.Threading;

namespace Buzzy.App.Composicao;

/// <summary>
/// Um disparo único num <see cref="DispatcherTimer"/> (DEC-011: nada periódico), para as agendas da raiz que recebem o
/// agendador injetado (<see cref="AgendaDeGravacao"/> e <see cref="AgendaDaReleitura"/>). Só na thread da interface.
/// </summary>
internal static class DisparoUnico
{
    /// <summary>
    /// Agenda <paramref name="acao"/> para daqui a <paramref name="espera"/> (no mínimo 1 ms), na
    /// <paramref name="prioridade"/> dada. O temporizador para antes de chamar a ação (sem o Stop, dispararia a cada
    /// intervalo). Devolve o que o cancela; cancelado, ou já disparado, não dispara mais.
    /// </summary>
    internal static Action NoDispatcher(TimeSpan espera, Action acao, DispatcherPriority prioridade)
    {
        ArgumentNullException.ThrowIfNull(acao);
        var temporizador = new DispatcherTimer(prioridade) { Interval = espera > TimeSpan.Zero ? espera : TimeSpan.FromMilliseconds(1) };
        bool encerrado = false;
        EventHandler aoDisparar = null!;
        aoDisparar = (_, _) =>
        {
            temporizador.Stop();
            temporizador.Tick -= aoDisparar;
            if (encerrado) return;
            encerrado = true;
            acao();
        };
        temporizador.Tick += aoDisparar;
        temporizador.Start();
        return () =>
        {
            encerrado = true;
            temporizador.Stop();
            temporizador.Tick -= aoDisparar;
        };
    }
}
