using System.Windows.Threading;

namespace Buzzy.App.Composicao;

// Disparo único num DispatcherTimer (nada de timer periódico), usado pelas
// agendas de gravação e releitura. Só na thread da interface.
internal static class DisparoUnico
{
    // Espera mínima de 1 ms. O Stop vem antes da ação, senão o timer dispara de novo
    // a cada intervalo. Devolve o cancelamento; depois de disparar ou cancelar, não dispara mais.
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
