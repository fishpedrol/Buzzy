using System.Windows.Automation.Peers;
using Buzzy.App.Apresentacao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P10 (Q-20; DEC-038, item 7): as janelas do personagem e dos itens têm um peer vazio, fora das árvores de
/// controle e de conteúdo, sem nome e sem filhos; o leitor de tela não anuncia a imagem. O proxy do HWND fica [MANUAL].
/// </summary>
internal sealed class PeerVazioTestes
{
    [Teste]
    public void PersonagemEItem_ForaDasArvores_SemFilhos()
    {
        var personagem = new JanelaPersonagem();
        var item = new JanelaDoItem(1, SpriteDoItem.TamanhoLogico);
        try
        {
            foreach ((string nome, System.Windows.Window janela) in new (string, System.Windows.Window)[] { ("personagem", personagem), ("item", item) })
            {
                AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(janela);
                Afirmar.Verdadeiro(peer is PeerVazio, $"{nome}: o peer vazio ({peer.GetType().Name})");
                Afirmar.Igual((false, false, "", 0), (peer.IsControlElement(), peer.IsContentElement(), peer.GetName(), peer.GetChildren()?.Count ?? 0), $"{nome}: fora das árvores, sem nome e sem filhos");
            }
        }
        finally
        {
            personagem.Close();
            item.Close();
        }
    }
}
