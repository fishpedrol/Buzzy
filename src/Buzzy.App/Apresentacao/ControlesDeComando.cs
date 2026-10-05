using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// Uma caixa de marcar só por intenção (Fase 8; DEC-038, item 6): o mouse, o Espaço, a tecla de acesso, as teclas + e - e o
/// Toggle da UIA (o Narrador) levantam <see cref="Pedido"/> com o valor desejado e nunca mudam a marca. A marca só muda pelo
/// código (<see cref="Marcar"/>), a partir do núcleo ou da leitura do início com o Windows, e isso nunca levanta pedido.
/// </summary>
internal sealed class CaixaDeComando : CheckBox
{
    /// <summary>O usuário pediu este valor; quem ouve decide e, depois, marca.</summary>
    internal event Action<bool>? Pedido;

    /// <summary>Marca pelo código, sem pedido.</summary>
    internal void Marcar(bool marcada) => IsChecked = marcada;

    /// <summary>O clique, o Espaço, a tecla de acesso e o Toggle da UIA chegam aqui: só o pedido, sem mudar a marca.</summary>
    protected override void OnToggle() => Pedido?.Invoke(IsChecked != true);

    /// <summary>As teclas + e - da caixa marcariam direto; aqui viram pedido.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Teclar(e.Key))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>As teclas + e - viram pedido (só quando mudam algo); devolve se a tecla foi tratada.</summary>
    internal bool Teclar(Key tecla)
    {
        bool? pedido = tecla switch
        {
            Key.Add or Key.OemPlus => true,
            Key.Subtract or Key.OemMinus => false,
            _ => null,
        };
        if (pedido is not { } valor) return false;
        if (valor != (IsChecked == true)) Pedido?.Invoke(valor);
        return true;
    }
}

/// <summary>
/// Um botão de opção só por intenção (Fase 8; DEC-038, item 6): o clique, o Espaço, a tecla de acesso e o Select da UIA (o
/// Narrador, pelo <see cref="RadioDeComandoPeer"/>) levantam <see cref="Pedido"/> e nunca mudam a marca; ela só muda pelo
/// código. As setas são do <see cref="Seletor{T}"/>.
/// </summary>
internal sealed class RadioDeComando : RadioButton
{
    /// <summary>O usuário pediu esta opção.</summary>
    internal event Action? Pedido;

    /// <summary>Marca pelo código, sem pedido.</summary>
    internal void Marcar(bool marcada) => IsChecked = marcada;

    /// <summary>O caminho único do pedido, também do peer da UIA.</summary>
    internal void Pedir() => Pedido?.Invoke();

    protected override void OnToggle() => Pedir();

    protected override AutomationPeer OnCreateAutomationPeer() => new RadioDeComandoPeer(this);
}

/// <summary>
/// O peer do <see cref="RadioDeComando"/>: o <c>Select</c> da UIA vira o mesmo pedido do clique, em vez de marcar direto, como o
/// peer do WPF faria; o resto do padrão SelectionItem segue o do botão de opção.
/// </summary>
internal sealed class RadioDeComandoPeer(RadioDeComando dono) : RadioButtonAutomationPeer(dono), ISelectionItemProvider
{
    private readonly RadioDeComando _dono = dono;

    bool ISelectionItemProvider.IsSelected => _dono.IsChecked == true;

    IRawElementProviderSimple ISelectionItemProvider.SelectionContainer => null!;

    void ISelectionItemProvider.AddToSelection()
    {
        if (_dono.IsChecked != true) throw new InvalidOperationException("Um botão de opção só se seleciona por Select.");
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        if (_dono.IsChecked == true) throw new InvalidOperationException("Um botão de opção marcado não sai da seleção sozinho.");
    }

    void ISelectionItemProvider.Select()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        _dono.Pedir();
    }
}

/// <summary>
/// Um grupo de opções com nome (Fase 8; DEC-038, item 7): um <see cref="GroupBox"/>, que a UIA expõe como Group com o título
/// como nome, com um <see cref="RadioDeComando"/> por opção, cada um com o texto de ajuda. O grupo é uma parada só do Tab, na
/// opção marcada, e as setas escolhem a vizinha (como nos grupos do Win32) e levantam <see cref="Escolheu"/>; a marca só muda
/// por <see cref="Marcar"/>.
/// </summary>
internal sealed class Seletor<T> : GroupBox where T : struct
{
    private readonly IReadOnlyList<(T Valor, RadioDeComando Radio)> _opcoes;

    /// <param name="titulo">O título do grupo, que é o nome dele na UIA.</param>
    /// <param name="opcoes">O valor, o rótulo (com a tecla de acesso) e o texto de ajuda de cada opção, na ordem.</param>
    /// <param name="ajuda">O texto de ajuda do grupo.</param>
    internal Seletor(string titulo, IReadOnlyList<(T Valor, string Rotulo, string? Ajuda)> opcoes, string? ajuda = null)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        // O nome do grupo na UIA vem do título (GroupBoxAutomationPeer).
        Header = titulo;
        if (ajuda is not null) AutomationProperties.SetHelpText(this, ajuda);
        string grupo = $"seletor-{Guid.NewGuid():N}";
        var painel = new StackPanel { Margin = new Thickness(6, 4, 6, 2) };
        var lista = new List<(T, RadioDeComando)>(opcoes.Count);
        foreach ((T valor, string rotulo, string? ajudaDaOpcao) in opcoes)
        {
            var radio = new RadioDeComando { Content = rotulo, GroupName = grupo, Margin = new Thickness(0, 2, 0, 2) };
            AutomationProperties.SetName(radio, rotulo.Replace("_", "", StringComparison.Ordinal));
            if ((ajudaDaOpcao ?? ajuda) is { } texto) AutomationProperties.SetHelpText(radio, texto);
            T v = valor;
            radio.Pedido += () => Escolheu?.Invoke(v);
            painel.Children.Add(radio);
            lista.Add((valor, radio));
        }
        _opcoes = lista;
        painel.PreviewKeyDown += AoTeclar;
        AjustarParadaDoTab();
        Content = painel;
    }

    /// <summary>O usuário escolheu esta opção (clique, Espaço, tecla de acesso, setas ou a UIA).</summary>
    internal event Action<T>? Escolheu;

    /// <summary>Os botões, na ordem (para os testes e para dar o foco).</summary>
    internal IReadOnlyList<RadioDeComando> Botoes => [.. _opcoes.Select(o => o.Radio)];

    /// <summary>Marca a opção do valor pelo código, sem levantar nada.</summary>
    internal void Marcar(T valor)
    {
        foreach ((T v, RadioDeComando radio) in _opcoes) radio.Marcar(EqualityComparer<T>.Default.Equals(v, valor));
        AjustarParadaDoTab();
    }

    /// <summary>As opções onde o Tab para: só a marcada (ou a primeira, sem marca), como nos grupos do Win32.</summary>
    internal IReadOnlyList<RadioDeComando> ParadasDoTab => [.. _opcoes.Select(o => o.Radio).Where(r => r.IsTabStop)];

    private void AjustarParadaDoTab()
    {
        RadioDeComando parada = _opcoes.FirstOrDefault(o => o.Radio.IsChecked == true).Radio ?? _opcoes[0].Radio;
        foreach ((_, RadioDeComando radio) in _opcoes) radio.IsTabStop = ReferenceEquals(radio, parada);
    }

    /// <summary>Dá o foco à opção marcada (ou à primeira).</summary>
    internal void Focar() => (_opcoes.FirstOrDefault(o => o.Radio.IsChecked == true).Radio ?? _opcoes[0].Radio).Focus();

    private void AoTeclar(object sender, KeyEventArgs e)
    {
        int atual = -1;
        for (int i = 0; i < _opcoes.Count; i++)
        {
            if (_opcoes[i].Radio.IsKeyboardFocusWithin) atual = i;
        }
        if (Teclar(e.Key, atual)) e.Handled = true;
    }

    /// <summary>
    /// As setas a partir da opção <paramref name="atual"/> (a do foco): a vizinha recebe o foco e é escolhida, sem dar a
    /// volta; devolve se a tecla foi tratada.
    /// </summary>
    internal bool Teclar(Key tecla, int atual)
    {
        int passo = tecla switch
        {
            Key.Up or Key.Left => -1,
            Key.Down or Key.Right => +1,
            _ => 0,
        };
        if (passo == 0 || atual < 0 || atual >= _opcoes.Count) return false;
        int novo = Math.Clamp(atual + passo, 0, _opcoes.Count - 1);
        if (novo == atual) return true;
        _opcoes[novo].Radio.Focus();
        Escolheu?.Invoke(_opcoes[novo].Valor);
        return true;
    }
}
