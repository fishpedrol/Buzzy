using System.Windows;
using System.Windows.Automation.Peers;

namespace Buzzy.App.Apresentacao;

// Peer das janelas do personagem e dos itens: fora das árvores de controle e de
// conteúdo e sem filhos, pro Narrador não anunciar a imagem. Não há texto pra ler.
// O proxy que o Windows cria pro próprio HWND não é coberto aqui.
internal sealed class PeerVazio(FrameworkElement dono) : FrameworkElementAutomationPeer(dono)
{
    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override string GetNameCore() => "";

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;
}
