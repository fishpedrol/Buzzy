using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzySpike;

// Faz o papel do app do usuário que está com o foco enquanto o Buzzy é arrastado. Roda em
// outro processo (BuzzySpike --modo receptor), como um app de verdade.
// Substitui o Bloco de Notas: não dá pra ler o que foi digitado em app alheio, e o Bloco de
// Notas do Windows 11 pode reabrir abas antigas e o teste acabaria digitando em documento do
// usuário. Aqui a janela é nossa, então registrar o que ela recebe não lê nada de terceiros.
// Loga só eventos da própria janela em resultados/receptor.log, com o texto mascarado.
internal sealed class JanelaReceptor : Window
{
    internal const int LarguraPx = 900;
    internal const int AlturaPx = 560;

    private readonly int? _xPedido;
    private readonly int? _yPedido;
    private readonly TextBox _caixa;
    private nint _hwnd;

    internal JanelaReceptor(int? x, int? y)
    {
        _xPedido = x;
        _yPedido = y;

        Title = "Receptor de teste P3 (BuzzySpike, descartável)";
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = true;
        ShowInTaskbar = true;
        Topmost = false;
        ResizeMode = ResizeMode.NoResize;

        _caixa = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Padding = new Thickness(8),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Content = _caixa;

        Activated += (_, _) => Diagnostico.Linha("RECEPTOR|ATIVA|sim");
        Deactivated += (_, _) => Diagnostico.Linha("RECEPTOR|ATIVA|nao");
        _caixa.GotKeyboardFocus += (_, _) => Diagnostico.Linha("RECEPTOR|FOCO_TECLADO|sim");
        _caixa.LostKeyboardFocus += (_, _) => Diagnostico.Linha("RECEPTOR|FOCO_TECLADO|nao");
        _caixa.PreviewTextInput += AoReceberTexto;
        PreviewMouseDown += AoClicar;

        Loaded += AoCarregar;
        Closed += (_, _) => Diagnostico.Linha($"RECEPTOR|CONTEUDO_FINAL|{Mascarar(_caixa.Text)}");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
    }

    private void AoCarregar(object? remetente, RoutedEventArgs e)
    {
        // Px físicos, como o protótipo, pro harness não ter que converter DPI.
        if (_xPedido is int x && _yPedido is int y)
        {
            Interop.SetWindowPos(_hwnd, 0, x, y, LarguraPx, AlturaPx,
                Interop.SWP_NOZORDER | Interop.SWP_NOACTIVATE);
        }

        _caixa.Focus();

        Interop.GetWindowRect(_hwnd, out Interop.RECT r);

        // Ponto pra ativar: dentro da caixa, longe de onde o harness põe o protótipo.
        int alvoX = r.Left + 60;
        int alvoY = r.Top + 90;

        Diagnostico.Linha($"SONDA|HWND|{_hwnd}");
        Diagnostico.Linha($"SONDA|RECT|{r.Left}|{r.Top}|{r.Right}|{r.Bottom}");
        Diagnostico.Linha($"SONDA|ALVO|{alvoX}|{alvoY}");
        Diagnostico.Linha($"RECEPTOR|PRONTO|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void AoReceberTexto(object remetente, TextCompositionEventArgs e)
    {
        // O clique de ativação pode ter posto o cursor no meio do texto. Jogando pro fim (antes
        // da inserção), o conteúdo final é tudo concatenado na ordem.
        _caixa.CaretIndex = _caixa.Text.Length;
        Diagnostico.Linha($"RECEPTOR|TEXTO|{Mascarar(e.Text)}|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void AoClicar(object remetente, MouseButtonEventArgs e)
    {
        Point p = PointToScreen(e.GetPosition(this));
        Diagnostico.Linha($"RECEPTOR|CLIQUE|{e.ChangedButton}|tela ({p.X:0},{p.Y:0})|ativa={IsActive}");
    }

    // Só ASCII alfanumérico, ';' e '-' (o alfabeto dos marcadores); o resto vira '?'. Assim o log
    // fica numa linha, sem escape, e não guarda nada fora dos marcadores.
    private static string Mascarar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (char c in texto)
            sb.Append((char.IsAsciiLetterOrDigit(c) || c is ';' or '-') ? c : '?');
        return sb.ToString();
    }
}
