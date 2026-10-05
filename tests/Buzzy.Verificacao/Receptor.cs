using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Buzzy.Verificacao;

/// <summary>
/// Receptor de teste: faz o papel do aplicativo que o usuário está usando. É uma janela
/// comum, ativável, com uma caixa de texto, num processo separado (esta mesma ferramenta com
/// <c>--receptor</c>). Registra só o que ela mesma recebe: ativação, foco, cada caractere e
/// cada clique, com a posição em tela. Nada aqui lê outro aplicativo.
///
/// Privacidade: no log, qualquer caractere fora de [A-Za-z0-9;-] (o alfabeto dos marcadores
/// da verificação) vira '?', no texto recebido e no conteúdo final. Uma tecla humana acidental
/// fora desse alfabeto não fica gravada.
/// </summary>
internal sealed class JanelaReceptor : Window
{
    private readonly TextBox _caixa;
    private readonly string _log;
    private readonly int _x, _y, _largura, _altura;
    private nint _hwnd;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out Nativo.RECT r);

    internal JanelaReceptor(string log, int x, int y, int largura, int altura)
    {
        _log = log;
        _x = x;
        _y = y;
        _largura = largura;
        _altura = altura;

        Title = "Receptor de teste da Fase 1 (Buzzy.Verificacao)";
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = true;
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.NoResize;
        _caixa = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Padding = new Thickness(8),
            Background = Brushes.White,
            Foreground = Brushes.Black,
        };
        Content = _caixa;

        Activated += (_, _) => Linha("RECEPTOR|ATIVA|sim");
        Deactivated += (_, _) => Linha("RECEPTOR|ATIVA|nao");
        _caixa.GotKeyboardFocus += (_, _) => Linha("RECEPTOR|FOCO_TECLADO|sim");
        _caixa.LostKeyboardFocus += (_, _) => Linha("RECEPTOR|FOCO_TECLADO|nao");
        _caixa.PreviewTextInput += AoReceberTexto;
        PreviewMouseDown += (_, e) =>
        {
            Point p = PointToScreen(e.GetPosition(this));
            Linha(string.Create(CultureInfo.InvariantCulture, $"RECEPTOR|CLIQUE|{e.ChangedButton}|{p.X:0},{p.Y:0}|ativa={IsActive}"));
        };
        Loaded += AoCarregar;
        Closed += (_, _) => Linha($"RECEPTOR|CONTEUDO_FINAL|{Mascarar(_caixa.Text)}");
    }

    /// <summary>Se o caractere pertence ao alfabeto dos marcadores da verificação: [A-Za-z0-9;-].</summary>
    internal static bool Permitido(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or ';' or '-';

    /// <summary>Troca por '?' todo caractere fora de [A-Za-z0-9;-]. Por construção, o resultado nunca tem '|' nem quebra de linha.</summary>
    internal static string Mascarar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (char c in texto) sb.Append(Permitido(c) ? c : '?');
        return sb.ToString();
    }

    private void AoReceberTexto(object remetente, TextCompositionEventArgs e)
    {
        // O texto entra sempre no fim: o clique que reativa o receptor cai dentro da caixa e
        // move o cursor de texto; sem isto, o conteúdo final sairia fora de ordem.
        _caixa.CaretIndex = _caixa.Text.Length;
        Linha($"RECEPTOR|TEXTO|{Mascarar(e.Text)}|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void AoCarregar(object? remetente, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(_hwnd, 0, _x, _y, _largura, _altura, 0x0004 | 0x0010);
        _caixa.Focus();
        GetWindowRect(_hwnd, out Nativo.RECT r);
        Linha($"SONDA|HWND|{_hwnd}");
        Linha($"SONDA|RECT|{r.Left}|{r.Top}|{r.Right}|{r.Bottom}");
        Linha($"SONDA|ALVO|{r.Left + 60}|{r.Top + 90}");
        Linha($"RECEPTOR|PRONTO|ativa={IsActive}|foco={_caixa.IsKeyboardFocused}");
    }

    private void Linha(string texto)
    {
        try { File.AppendAllText(_log, texto + Environment.NewLine, new UTF8Encoding(false)); }
        catch (IOException) { }
    }
}
