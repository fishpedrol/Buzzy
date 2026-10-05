using System.IO;
using System.Security.Principal;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Instância única (Q-03): abrir o Buzzy de novo revela o existente e não cria outro.
///
/// A primeira instância cria um mutex nomeado e um evento nomeado, ambos no espaço da sessão
/// (<c>Local\</c>) e com o SID do usuário no nome. A segunda encontra o mutex, sinaliza o
/// evento e sai. A primeira espera o evento com <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object?, int, bool)"/>,
/// que bloqueia no kernel sem nenhuma consulta periódica (DEC-011). O evento não carrega
/// dado nenhum: o único efeito possível de sinalizá-lo é o Buzzy aparecer.
/// </summary>
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

    /// <summary>
    /// Na segunda instância: pede para a primeira aparecer. Tenta por até 3 s, cobrindo o
    /// instante em que a primeira já criou o mutex mas ainda não o evento.
    /// </summary>
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
                // Por exemplo, a primeira instância rodando com outro nível de acesso. Só o tipo e o código, que vão
                // para o log: a mensagem traz o nome do evento, com o SID da conta (SECURITY.md 6).
                erro = $"{e.GetType().Name} 0x{e.HResult:X8}";
                return false;
            }
            Thread.Sleep(100);
        }
        erro = "evento da primeira instância não encontrado em 3 s";
        return false;
    }

    /// <summary>Na primeira instância: chama <paramref name="aoPedido"/> (em outra thread) a cada pedido.</summary>
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
