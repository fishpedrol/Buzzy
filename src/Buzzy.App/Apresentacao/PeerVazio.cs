using System.Windows;
using System.Windows.Automation.Peers;

namespace Buzzy.App.Apresentacao;

/// <summary>
/// O peer de automação das janelas do personagem e dos itens (Q-20; DEC-038, item 7): fora das árvores de controle e de
/// conteúdo e sem filhos, para o Narrador não anunciar a imagem nem navegar até ela. O produto é não verbal: não há texto a
/// ler nessas janelas. O proxy que o Windows cria para o próprio HWND fica UNCERTAIN, [MANUAL] com o Narrador.
/// </summary>
internal sealed class PeerVazio(FrameworkElement dono) : FrameworkElementAutomationPeer(dono)
{
    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override string GetNameCore() => "";

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;
}
