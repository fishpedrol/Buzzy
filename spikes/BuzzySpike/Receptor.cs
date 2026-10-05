using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzySpike;

/// <summary>
/// Receptor de teste de P3: faz o papel do "aplicativo do usuário" que está com o foco
/// enquanto o Buzzy é clicado e arrastado. É uma janela comum, ativável, com uma caixa de
/// texto, e roda num PROCESSO SEPARADO do protótipo (outra instância do BuzzySpike com
/// --modo receptor), como um aplicativo real estaria.
///
/// Existe para substituir o Bloco de Notas nos testes automáticos. O Bloco de Notas é de
/// outro aplicativo: o harness não pode ler o que foi digitado nele (SECURITY.md proíbe),
/// e o Bloco de Notas do Windows 11 pode restaurar abas de sessões anteriores, o que faria
/// o teste digitar em documentos do usuário. Este receptor pertence ao próprio spike, então
/// registrar o que ele recebe não lê nada de terceiros.
///
/// Registra, em resultados/receptor.log, apenas eventos da própria janela: ativação,
/// foco de teclado, cada texto recebido e cliques nela. Nada é consultado periodicamente.
/// O texto vai para o log mascarado: só letras ASCII, dígitos, ';' e '-' (o alfabeto dos
/// marcadores da sonda); qualquer outro caractere vira '?'.
/// </summary>
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
        // Posição e tamanho em pixels físicos, como o protótipo, para o harness trabalhar
        // no mesmo sistema de coordenadas sem conversão por DPI.
        if (_xPedido is int x && _yPedido is int y)
        {
            Interop.SetWindowPos(_hwnd, 0, x, y, LarguraPx, AlturaPx,
                Interop.SWP_NOZORDER | Interop.SWP_NOACTIVATE);
        }

        _caixa.Focus();

        Interop.GetWindowRect(_hwnd, out Interop.RECT r);

        // Ponto de ativação: dentro da caixa de texto, perto do canto superior esquerdo,
        // longe de onde o harness coloca o protótipo.
        int alvoX = r.Left + 60;
        int alvoY = r.Top + 90;

        Diagnostico.Linha($"SONDA|HWND|{_hwnd}");
        Diagnostico.Linha($"SONDA|RECT|{r.Left}|{r.Top}|{r.Right}|{r.Bottom}");
        Diagnostico.Linha($"SONDA|ALVO|{alvoX}|{alvoY}");
        Diagnostico.Linha($"RECEPTOR|PRONTO|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void AoReceberTexto(object remetente, TextCompositionEventArgs e)
    {
        // PreviewTextInput vem antes da inserção. O clique de ativação pode ter posto o cursor
        // de texto no meio do que já foi digitado; com ele no fim (o que também desfaz qualquer
        // seleção), cada texto é acrescentado e o conteúdo final é a concatenação, na ordem.
        _caixa.CaretIndex = _caixa.Text.Length;
        Diagnostico.Linha($"RECEPTOR|TEXTO|{Mascarar(e.Text)}|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void AoClicar(object remetente, MouseButtonEventArgs e)
    {
        Point p = PointToScreen(e.GetPosition(this));
        Diagnostico.Linha($"RECEPTOR|CLIQUE|{e.ChangedButton}|tela ({p.X:0},{p.Y:0})|ativa={IsActive}");
    }

    /// <summary>
    /// Mantém só letras ASCII, dígitos, ';' e '-'; qualquer outro caractere (espaço, acento,
    /// pontuação, quebra de linha, separador '|', controle) vira '?'. O log fica numa linha só,
    /// sem nada a desescapar, e não guarda o que fugir do alfabeto dos marcadores.
    /// </summary>
    private static string Mascarar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (char c in texto)
            sb.Append((char.IsAsciiLetterOrDigit(c) || c is ';' or '-') ? c : '?');
        return sb.ToString();
    }
}
