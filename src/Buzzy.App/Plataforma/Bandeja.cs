using System.Runtime.InteropServices;
using Buzzy.Core;

namespace Buzzy.App.Plataforma;

/// <summary>
/// Ícone da bandeja (Q-03), pela API da Shell, sem biblioteca de terceiros
/// (ARCHITECTURE.md 2.13.3): versão 4 das notificações, identificado por janela e número,
/// sem GUID, e recriado quando a barra de tarefas reinicia ou o DPI do principal muda.
/// </summary>
internal sealed class Bandeja : IDisposable
{
    internal const uint IdDoIcone = 1;
    internal const int MensagemDeRetorno = Win32.WM_APP + 1;

    private readonly nint _hwnd;
    private nint _icone;
    private bool _adicionado;

    /// <param name="hwndServico">Janela que recebe as notificações do ícone.</param>
    /// <param name="icone">HICON do ícone; esta classe passa a ser dona dele e o destrói no fim.</param>
    internal Bandeja(nint hwndServico, nint icone)
    {
        _hwnd = hwndServico;
        _icone = icone;
    }

    internal bool Adicionado => _adicionado;

    /// <summary>Se a Shell aceitou a versão 4 das notificações (coordenadas da âncora no wParam).</summary>
    internal bool Versao4 { get; private set; }

    /// <summary>Adiciona o ícone e pede a versão 4 das notificações.</summary>
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

    /// <summary>
    /// Recria o ícone depois de "TaskbarCreated". Remove antes, ignorando falha: numa
    /// reinicialização real o ícone antigo já sumiu com a barra; se a mensagem chegar com o
    /// ícone ainda lá (mudança de DPI), o NIM_ADD sozinho falharia.
    /// </summary>
    internal bool Recriar()
    {
        Win32.NOTIFYICONDATA dados = Dados(0);
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref dados);
        _adicionado = false;
        return Adicionar();
    }

    /// <summary>
    /// Troca o HICON (por exemplo, para o tamanho de um DPI novo). Com <paramref name="aplicar"/>,
    /// atualiza o ícone já presente na bandeja. O HICON antigo só é destruído depois.
    /// </summary>
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

    /// <summary>Devolve o foco do teclado à área de notificação (documentação da Shell).</summary>
    internal void DevolverFoco()
    {
        if (!_adicionado) return;
        Win32.NOTIFYICONDATA dados = Dados(0);
        Win32.Shell_NotifyIcon(Win32.NIM_SETFOCUS, ref dados);
    }

    /// <summary>Remove o ícone. NIM_DELETE é idempotente: é enviado mesmo sem confirmação de que o ícone existe.</summary>
    internal void Remover()
    {
        Win32.NOTIFYICONDATA dados = Dados(0);
        bool removeu = Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref dados);
        bool estava = _adicionado;
        _adicionado = false;
        if (estava || removeu) Diagnostico.Evento("BANDEJA", ("removido", removeu));
    }

    /// <summary>Retângulo do ícone na barra, se a Shell informar (pode ser o da área de ícones ocultos).</summary>
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
