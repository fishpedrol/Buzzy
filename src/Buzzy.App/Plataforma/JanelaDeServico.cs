using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Plataforma;

internal enum AcaoNaBandeja
{
    // Clique esquerdo ou Enter/espaço: mostrar o Buzzy.
    Selecionar,

    // Botão direito.
    Menu,

    // Shift+F10 ou tecla de menu.
    MenuPeloTeclado,
}

// Janela top-level oculta, nunca mostrada, que recebe bandeja, TaskbarCreated, mudança
// de vídeo/área útil, sessão e suspensão mesmo com o personagem escondido. Não pode
// ser message-only: essas não recebem broadcast (TaskbarCreated, WM_SETTINGCHANGE).
internal sealed class JanelaDeServico : IDisposable
{
    private readonly HwndSource _fonte;
    private readonly int _mensagemBarraCriada;
    private bool _notificacoesDaSessaoRegistradas;

    // Na versão 4 o botão direito chega como WM_RBUTTONUP + WM_CONTEXTMENU, e a tecla de
    // menu só como WM_CONTEXTMENU. Assim dá pra saber se foi teclado sem ler o teclado.
    private bool _botaoDireitoAcabouDeSoltar;

    internal JanelaDeServico()
    {
        _mensagemBarraCriada = Win32.RegisterWindowMessage("TaskbarCreated");
        _fonte = new HwndSource(new HwndSourceParameters("Buzzy.Servico")
        {
            WindowStyle = Win32.WS_POPUP,
            ExtendedWindowStyle = (int)Win32.WS_EX_TOOLWINDOW,
            PositionX = 0,
            PositionY = 0,
            Width = 0,
            Height = 0,
        });
        _fonte.AddHook(Gancho);

        if (!Win32.WTSRegisterSessionNotification(_fonte.Handle, Win32.NOTIFY_FOR_THIS_SESSION))
        {
            int codigo = Marshal.GetLastWin32Error();
            _fonte.RemoveHook(Gancho);
            _fonte.Dispose();
            throw new Win32Exception(codigo, "Não foi possível registrar notificações da sessão atual do Windows.");
        }
        _notificacoesDaSessaoRegistradas = true;
    }

    internal nint Hwnd => _fonte.Handle;

    // Falso se a Shell recusou NIM_SETVERSION: formato antigo, sem a posição da âncora.
    internal bool NotificacoesVersao4 { get; set; } = true;

    // Âncora nula no formato antigo.
    internal event Action<AcaoNaBandeja, PontoPx?>? BandejaAcionada;

    internal event Action? BarraDeTarefasRecriada;

    internal event Action<string>? TopologiaPodeTerMudado;

    internal event Action<string, Evento>? EventoDoSistema;

    private nint Gancho(nint hwnd, int msg, nint wParam, nint lParam, ref bool tratado)
    {
        if (msg == Bandeja.MensagemDeRetorno)
        {
            TratarBandeja(wParam, lParam);
            tratado = true;
            return 0;
        }

        if (msg == _mensagemBarraCriada && _mensagemBarraCriada != 0)
        {
            BarraDeTarefasRecriada?.Invoke();
            return 0;
        }

        if (msg == Win32.WM_WTSSESSION_CHANGE)
        {
            switch ((int)wParam)
            {
                case Win32.WTS_SESSION_LOCK:
                    EventoDoSistema?.Invoke("WM_WTSSESSION_CHANGE WTS_SESSION_LOCK", new SessionLocked());
                    tratado = true;
                    return 0;
                case Win32.WTS_SESSION_UNLOCK:
                    EventoDoSistema?.Invoke("WM_WTSSESSION_CHANGE WTS_SESSION_UNLOCK", new SessionUnlocked());
                    tratado = true;
                    return 0;
            }
        }

        if (msg == Win32.WM_POWERBROADCAST)
        {
            switch ((int)wParam)
            {
                case Win32.PBT_APMSUSPEND:
                    EventoDoSistema?.Invoke("WM_POWERBROADCAST PBT_APMSUSPEND", new Suspending());
                    tratado = true;
                    return 1;
                case Win32.PBT_APMRESUMEAUTOMATIC:
                    EventoDoSistema?.Invoke("WM_POWERBROADCAST PBT_APMRESUMEAUTOMATIC", new Resumed());
                    tratado = true;
                    return 1;
                case Win32.PBT_APMRESUMESUSPEND:
                    // Pode vir depois do RESUMEAUTOMATIC se houve atividade do usuário;
                    // a retomada já foi tratada lá.
                    tratado = true;
                    return 1;
            }
        }

        switch (msg)
        {
            case Win32.WM_DISPLAYCHANGE:
                TopologiaPodeTerMudado?.Invoke("WM_DISPLAYCHANGE");
                break;
            case Win32.WM_SETTINGCHANGE when (int)wParam == Win32.SPI_SETWORKAREA:
                TopologiaPodeTerMudado?.Invoke("SPI_SETWORKAREA");
                break;
        }
        return 0;
    }

    private void TratarBandeja(nint wParam, nint lParam)
    {
        if (NotificacoesVersao4)
        {
            // V4: LOWORD(lParam) = evento, HIWORD(lParam) = id, wParam = âncora com sinal.
            int evento = Win32.LoWord(lParam);
            var ancora = new PontoPx(Win32.XComSinal(wParam), Win32.YComSinal(wParam));
            switch (evento)
            {
                case Win32.WM_RBUTTONUP:
                    _botaoDireitoAcabouDeSoltar = true;
                    break;
                case Win32.NIN_SELECT or Win32.NIN_KEYSELECT:
                    _botaoDireitoAcabouDeSoltar = false;
                    BandejaAcionada?.Invoke(AcaoNaBandeja.Selecionar, ancora);
                    break;
                case Win32.WM_CONTEXTMENU:
                    AcaoNaBandeja acao = _botaoDireitoAcabouDeSoltar ? AcaoNaBandeja.Menu : AcaoNaBandeja.MenuPeloTeclado;
                    _botaoDireitoAcabouDeSoltar = false;
                    BandejaAcionada?.Invoke(acao, ancora);
                    break;
            }
            return;
        }

        // Formato antigo: lParam = mensagem de mouse, sem coordenadas.
        switch ((int)lParam)
        {
            case Win32.WM_LBUTTONUP:
                BandejaAcionada?.Invoke(AcaoNaBandeja.Selecionar, null);
                break;
            case Win32.WM_RBUTTONUP:
                BandejaAcionada?.Invoke(AcaoNaBandeja.Menu, null);
                break;
        }
    }

    public void Dispose()
    {
        if (_notificacoesDaSessaoRegistradas)
        {
            _notificacoesDaSessaoRegistradas = false;
            Win32.WTSUnRegisterSessionNotification(_fonte.Handle);
        }
        _fonte.RemoveHook(Gancho);
        _fonte.Dispose();
    }
}
