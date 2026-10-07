using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Apresentacao;

// Painel compacto de energia: só o seletor de três posições, sem botão de fechar nem
// animação, com o tema das configurações. Topmost enquanto aberto, fora da barra e do
// Alt+Tab. Esc, Enter, Alt+F4 ou perder o foco fecham avisando FechadoPeloUsuario
// (uma vez só); FecharPeloNucleo fecha sem avisar. Escolher não fecha.
internal sealed class PainelDeEnergia : Window
{
    private readonly Seletor<NivelDeEnergia> _seletor;
    private bool _ativado;
    private bool _fechando;

    internal PainelDeEnergia(NivelDeEnergia atual)
    {
        Title = Textos.PainelTitulo;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontSize = 13;
        UseLayoutRounding = true;
        TemaDoBuzzy.Aplicar(this);
        _seletor = new Seletor<NivelDeEnergia>(Textos.EnergiaGrupo,
        [
            (NivelDeEnergia.Baixa, Textos.EnergiaBaixa, null),
            (NivelDeEnergia.Media, Textos.EnergiaMedia, null),
            (NivelDeEnergia.Alta, Textos.EnergiaAlta, null),
        ], Textos.PainelAjuda);
        _seletor.Marcar(atual);
        _seletor.Escolheu += nivel => Escolheu?.Invoke(nivel);
        KeyboardNavigation.SetTabNavigation(_seletor, KeyboardNavigationMode.Cycle);
        _seletor.SetResourceReference(StyleProperty, TemaDoBuzzy.ChaveSeletorSemTitulo);
        _seletor.Margin = new Thickness(8, 6, 12, 10);
        var pilha = new StackPanel();
        pilha.Children.Add(TemaDoBuzzy.Cabecalho(Textos.PainelTitulo, 15));
        pilha.Children.Add(_seletor);
        var moldura = new Border { BorderThickness = new Thickness(2), Child = pilha };
        moldura.SetResourceReference(Border.BorderBrushProperty, TemaDoBuzzy.ChaveBorda);
        Content = moldura;
        PreviewKeyDown += AoTeclar;
        Activated += (_, _) =>
        {
            if (_ativado) return;
            _ativado = true;
            _seletor.Focar();
        };
        Deactivated += (_, _) =>
        {
            if (_ativado) FecharPeloUsuario("foco");
        };
        Closing += (_, _) =>
        {
            // Alt+F4 e o fechamento pelo sistema também são do usuário.
            if (!_fechando) Avisar("altF4");
        };
    }

    // O painel continua aberto.
    internal event Action<NivelDeEnergia>? Escolheu;

    // Motivo: esc, enter, foco, altF4 ou semFoco. Uma vez só.
    internal event Action<string>? FechadoPeloUsuario;

    internal Seletor<NivelDeEnergia> Seletor => _seletor;

    internal nint Hwnd => new WindowInteropHelper(this).Handle;

    // Pixels físicos; nulo antes de a janela existir.
    internal RetanguloPx? RetanguloNaTela() => Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    // Vem do GravarPreferencias do núcleo; não levanta pedido.
    internal void Marcar(NivelDeEnergia nivel) => _seletor.Marcar(nivel);

    internal void FecharPeloUsuario(string motivo)
    {
        if (_fechando) return;
        Avisar(motivo);
        Close();
    }

    // Arraste, esconder, sair: fecha sem avisar.
    internal void FecharPeloNucleo()
    {
        if (_fechando) return;
        _fechando = true;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Fora do Alt+Tab, como uma janela de ferramenta.
        nint estilo = Win32.GetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE, (nint)((long)estilo | Win32.WS_EX_TOOLWINDOW));
    }

    private void Avisar(string motivo)
    {
        if (_fechando) return;
        _fechando = true;
        FechadoPeloUsuario?.Invoke(motivo);
    }

    private void AoTeclar(object sender, KeyEventArgs e)
    {
        if (Teclar(e.Key)) e.Handled = true;
    }

    // Devolve se a tecla foi tratada.
    internal bool Teclar(Key tecla)
    {
        switch (tecla)
        {
            case Key.Escape:
                FecharPeloUsuario("esc");
                return true;
            case Key.Enter:
                FecharPeloUsuario("enter");
                return true;
            default:
                return false;
        }
    }
}
