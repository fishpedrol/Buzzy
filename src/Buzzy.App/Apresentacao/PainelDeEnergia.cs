using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// O painel compacto de energia (Fase 8; DEC-038, item 4; critério 11): só o seletor de três posições, num grupo "Energia",
/// sem botão de fechar, sem texto livre e sem animação; a fonte do sistema e o tema "chapéu de palha" das configurações
/// (<see cref="TemaDoBuzzy"/>, DEC-039: a faixa azul com o retrato e o título, e o seletor sem título à vista; no alto
/// contraste, as cores do sistema). Fica no topo enquanto
/// está aberto e fora da barra de tarefas e do Alt+Tab. Fecha com Esc, Enter, Alt+F4 ou a perda de foco (pelo usuário,
/// <see cref="FechadoPeloUsuario"/>, uma vez só) ou pelo núcleo (<see cref="FecharPeloNucleo"/>, sem avisar). Escolher não
/// fecha. Nada periódico: só eventos da janela.
/// </summary>
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

    /// <summary>O usuário escolheu um nível (o painel continua aberto).</summary>
    internal event Action<NivelDeEnergia>? Escolheu;

    /// <summary>O usuário fechou o painel, com o motivo (<c>esc</c>, <c>enter</c>, <c>foco</c>, <c>altF4</c>, <c>semFoco</c>); uma vez só.</summary>
    internal event Action<string>? FechadoPeloUsuario;

    /// <summary>O seletor, para os testes.</summary>
    internal Seletor<NivelDeEnergia> Seletor => _seletor;

    /// <summary>O HWND, depois de criado.</summary>
    internal nint Hwnd => new WindowInteropHelper(this).Handle;

    /// <summary>O retângulo do próprio painel, em pixels físicos; nulo antes de criado.</summary>
    internal RetanguloPx? RetanguloNaTela() => Win32.GetWindowRect(Hwnd, out Win32.RECT r) ? new RetanguloPx(r.Left, r.Top, r.Right, r.Bottom) : null;

    /// <summary>Atualiza a marca a partir do núcleo (o efeito <c>GravarPreferencias</c>), sem pedido.</summary>
    internal void Marcar(NivelDeEnergia nivel) => _seletor.Marcar(nivel);

    /// <summary>Fecha a pedido do usuário: avisa uma vez e fecha.</summary>
    internal void FecharPeloUsuario(string motivo)
    {
        if (_fechando) return;
        Avisar(motivo);
        Close();
    }

    /// <summary>Fecha a pedido do núcleo (arraste, esconder, sair): sem avisar.</summary>
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

    /// <summary>Esc e Enter fecham pelo usuário; devolve se a tecla foi tratada.</summary>
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
