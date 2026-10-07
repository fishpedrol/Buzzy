using System.IO;
using System.Security.Principal;

namespace Buzzy.App.Plataforma;

// Abrir de novo só faz o Buzzy aberto aparecer. Mutex + evento nomeados em Local\ com o
// SID no nome; a segunda sinaliza o evento e sai. A primeira espera com
// RegisterWaitForSingleObject, que bloqueia no kernel sem polling. O evento não leva
// dado: sinalizar só pode fazer o Buzzy aparecer.
internal sealed class InstanciaUnica : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _evento;
    private RegisteredWaitHandle? _espera;
    private bool _dono;

    private InstanciaUnica(Mutex mutex, bool dono, EventWaitHandle? evento)
    {
        _mutex = mutex;
        _dono = dono;
        _evento = evento;
    }

    internal bool EhPrimeira => _dono;

    internal static InstanciaUnica Obter()
    {
        string sufixo = WindowsIdentity.GetCurrent().User?.Value ?? "sem-sid";
        var mutex = new Mutex(initiallyOwned: true, $@"Local\Buzzy.Instancia.{sufixo}", out bool criadoAgora);
        if (!criadoAgora)
            return new InstanciaUnica(mutex, dono: false, evento: null);

        var evento = new EventWaitHandle(false, EventResetMode.AutoReset, NomeDoEvento(sufixo));
        return new InstanciaUnica(mutex, dono: true, evento);
    }

    private static string NomeDoEvento(string sufixo) => $@"Local\Buzzy.Mostrar.{sufixo}";

    // Tenta por até 3 s: cobre a janela em que a primeira já criou o mutex mas não o evento.
    internal bool PedirParaAPrimeiraAparecer(out string? erro)
    {
        erro = null;
        string sufixo = WindowsIdentity.GetCurrent().User?.Value ?? "sem-sid";
        DateTime limite = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < limite)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(NomeDoEvento(sufixo), out EventWaitHandle? evento))
                {
                    using (evento)
                    {
                        return evento.Set();
                    }
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
            {
                // Ex.: a primeira rodando com outro nível de acesso. Só tipo + código pro log,
                // porque a mensagem traz o nome do evento com o SID.
                erro = $"{e.GetType().Name} 0x{e.HResult:X8}";
                return false;
            }
            Thread.Sleep(100);
        }
        erro = "evento da primeira instância não encontrado em 3 s";
        return false;
    }

    // aoPedido roda em thread do pool, não na da UI.
    internal void EscutarPedidos(Action aoPedido)
    {
        if (_evento is null) throw new InvalidOperationException("Só a primeira instância escuta pedidos.");
        _espera = ThreadPool.RegisterWaitForSingleObject(_evento, (_, _) => aoPedido(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _espera?.Unregister(null);
        _evento?.Dispose();
        if (_dono)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
            _dono = false;
        }
        _mutex.Dispose();
    }
}
