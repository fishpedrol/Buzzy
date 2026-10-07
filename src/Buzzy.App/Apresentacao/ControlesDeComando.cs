using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Buzzy.App.Apresentacao;

// Caixa de marcar que só pede: clique, Espaço, tecla de acesso, + e - e o Toggle
// da UIA (Narrador) levantam Pedido e nunca mudam a marca. A marca só muda por
// Marcar, vinda do núcleo, e isso não levanta pedido.
internal sealed class CaixaDeComando : CheckBox
{
    // Quem ouve decide e depois chama Marcar.
    internal event Action<bool>? Pedido;

    internal void Marcar(bool marcada) => IsChecked = marcada;

    // Clique, Espaço, tecla de acesso e Toggle da UIA passam por aqui.
    protected override void OnToggle() => Pedido?.Invoke(IsChecked != true);

    // O CheckBox marcaria direto com + e -.
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

    // + e - só pedem quando mudariam algo; devolve se a tecla foi tratada.
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

// Botão de opção que só pede, como a CaixaDeComando. O Select da UIA vem pelo
// RadioDeComandoPeer; as setas ficam com o Seletor.
internal sealed class RadioDeComando : RadioButton
{
    internal event Action? Pedido;

    internal void Marcar(bool marcada) => IsChecked = marcada;

    // Caminho único do pedido, usado também pelo peer.
    internal void Pedir() => Pedido?.Invoke();

    protected override void OnToggle() => Pedir();

    protected override AutomationPeer OnCreateAutomationPeer() => new RadioDeComandoPeer(this);
}

// O peer padrão do WPF marcaria direto no Select; aqui vira o mesmo pedido do clique.
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

// Grupo de opções: GroupBox (a UIA usa o título como nome) com um RadioDeComando
// por opção. Como nos grupos do Win32, o Tab para só na marcada e as setas escolhem
// a vizinha, levantando Escolheu; a marca só muda por Marcar.
internal sealed class Seletor<T> : GroupBox where T : struct
{
    private readonly IReadOnlyList<(T Valor, RadioDeComando Radio)> _opcoes;

    // O rótulo de cada opção traz a tecla de acesso (_).
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

    // Clique, Espaço, tecla de acesso, setas ou UIA.
    internal event Action<T>? Escolheu;

    internal IReadOnlyList<RadioDeComando> Botoes => [.. _opcoes.Select(o => o.Radio)];

    // Não levanta Escolheu.
    internal void Marcar(T valor)
    {
        foreach ((T v, RadioDeComando radio) in _opcoes) radio.Marcar(EqualityComparer<T>.Default.Equals(v, valor));
        AjustarParadaDoTab();
    }

    // Só a marcada, ou a primeira se nenhuma estiver.
    internal IReadOnlyList<RadioDeComando> ParadasDoTab => [.. _opcoes.Select(o => o.Radio).Where(r => r.IsTabStop)];

    private void AjustarParadaDoTab()
    {
        RadioDeComando parada = _opcoes.FirstOrDefault(o => o.Radio.IsChecked == true).Radio ?? _opcoes[0].Radio;
        foreach ((_, RadioDeComando radio) in _opcoes) radio.IsTabStop = ReferenceEquals(radio, parada);
    }

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

    // A vizinha da opção com foco ganha o foco e é escolhida, sem dar a volta.
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
