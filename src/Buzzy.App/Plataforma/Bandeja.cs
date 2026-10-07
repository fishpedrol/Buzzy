using System.Runtime.InteropServices;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

// Ícone da bandeja direto na API da Shell. Notificações versão 4, identificado por
// hwnd + id (sem GUID). Recriado quando a barra reinicia ou o DPI do principal muda.
internal sealed class Bandeja : IDisposable
{
    internal const uint IdDoIcone = 1;
    internal const int MensagemDeRetorno = Win32.WM_APP + 1;

    private readonly nint _hwnd;
    private nint _icone;
    private bool _adicionado;

    // Passa a ser dona do HICON e o destrói no fim.
    internal Bandeja(nint hwndServico, nint icone)
    {
        _hwnd = hwndServico;
        _icone = icone;
    }

    internal bool Adicionado => _adicionado;

    // Com a versão 4, as coordenadas da âncora vêm no wParam.
    internal bool Versao4 { get; private set; }

    internal bool Adicionar()
    {
        Win32.NOTIFYICONDATA dados = Dados(Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP);
        bool adicionou = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref dados);
        int erro = adicionou ? 0 : Marshal.GetLastWin32Error();
        Versao4 = false;
        if (adicionou)
        {
            dados.uTimeoutOrVersion = Win32.NOTIFYICON_VERSION_4;
            Versao4 = Win32.Shell_NotifyIcon(Win32.NIM_SETVERSION, ref dados);
        }
        _adicionado = adicionou;
        Diagnostico.Evento("BANDEJA", ("adicionado", adicionou), ("versao4", Versao4), ("erro", erro), ("retangulo", Retangulo()?.ToString() ?? "indisponível"));
        return adicionou;
    }

    // Depois de "TaskbarCreated". Remove antes, ignorando falha: se a mensagem vier com
    // o ícone ainda lá (mudança de DPI), o NIM_ADD sozinho falharia.
    internal bool Recriar()
    {
        Win32.NOTIFYICONDATA dados = Dados(0);
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref dados);
        _adicionado = false;
        return Adicionar();
    }

    // O HICON antigo só é destruído depois de trocar.
    internal void TrocarIcone(nint novo, bool aplicar)
    {
        if (novo == 0) return;
        nint antigo = _icone;
        _icone = novo;
        if (aplicar && _adicionado)
        {
            Win32.NOTIFYICONDATA dados = Dados(Win32.NIF_ICON);
            Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref dados);
        }
        if (antigo != 0) Win32.DestroyIcon(antigo);
    }

    // Devolve o foco do teclado à área de notificação, como a Shell pede.
    internal void DevolverFoco()
    {
        if (!_adicionado) return;
        Win32.NOTIFYICONDATA dados = Dados(0);
        Win32.Shell_NotifyIcon(Win32.NIM_SETFOCUS, ref dados);
    }

    // NIM_DELETE é idempotente, então manda mesmo sem saber se o ícone existe.
    internal void Remover()
    {
        Win32.NOTIFYICONDATA dados = Dados(0);
        bool removeu = Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref dados);
        bool estava = _adicionado;
        _adicionado = false;
        if (estava || removeu) Diagnostico.Evento("BANDEJA", ("removido", removeu));
    }

    // Pode vir o retângulo da área de ícones ocultos.
    internal RetanguloPx? Retangulo()
    {
        var id = new Win32.NOTIFYICONIDENTIFIER
        {
            cbSize = Marshal.SizeOf<Win32.NOTIFYICONIDENTIFIER>(),
            hWnd = _hwnd,
            uID = IdDoIcone,
        };
        return Win32.Shell_NotifyIconGetRect(ref id, out Win32.RECT r) == 0 ? LeitorDeTopologia.Retangulo(r) : null;
    }

    private Win32.NOTIFYICONDATA Dados(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = IdDoIcone,
        uFlags = flags,
        uCallbackMessage = MensagemDeRetorno,
        hIcon = _icone,
        szTip = Textos.DicaDaBandeja,
        szInfo = "",
        szInfoTitle = "",
        uTimeoutOrVersion = Versao4 ? Win32.NOTIFYICON_VERSION_4 : 0,
    };

    public void Dispose()
    {
        Remover();
        if (_icone != 0)
        {
            Win32.DestroyIcon(_icone);
            _icone = 0;
        }
    }
}
