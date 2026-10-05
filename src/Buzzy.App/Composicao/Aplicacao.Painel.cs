using System.Windows.Threading;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// A ligação do painel compacto de energia (Fase 8; DEC-038, item 4). O efeito <c>AbrirPainelDeEnergia</c> cria o painel,
/// posiciona-o no DPI do monitor do personagem (<see cref="AncoragemDoPainel"/>), mostra e ativa; uma única conferência
/// adiada (sem timer) fecha o painel que ficou sem foco, para ele nunca pausar a autonomia sem o usuário ver. A escolha vai ao
/// núcleo como <c>ENERGY_SELECTED</c>; o fechamento pelo usuário, como <c>ENERGY_PANEL_CLOSE</c>, uma vez; o fechamento pelo
/// núcleo (<c>FecharPainelDeEnergia</c>) não avisa. A marca acompanha o conteúdo de cada <c>GravarPreferencias</c>.
/// </summary>
internal sealed partial class Aplicacao
{
    /// <summary>A folga entre o painel e o sprite, em DIP.</summary>
    private const int FolgaDoPainelDip = 8;

    private PainelDeEnergia? _painel;

    private void AbrirPainelDeEnergiaPeloNucleo()
    {
        if (_encerrando || _nucleo is null) return;
        if (_painel is not null)
        {
            _painel.Activate();
            return;
        }
        var painel = new PainelDeEnergia(_nucleo.Estado.Preferencias.Energia);
        painel.Escolheu += nivel => Enviar(new EnergySelected(nivel), "painel");
        painel.FechadoPeloUsuario += motivo =>
        {
            Diagnostico.Evento("PAINEL", ("fechado", motivo));
            if (ReferenceEquals(_painel, painel)) _painel = null;
            Enviar(new EnergyPanelClose(), $"painel: {motivo}");
        };
        painel.DpiChanged += (_, _) => PosicionarPainel(painel);
        _painel = painel;

        // Antes de mostrar: o HWND no monitor do personagem, para o WPF adotar o DPI de lá; depois, o lugar final.
        var ajudante = new System.Windows.Interop.WindowInteropHelper(painel);
        ajudante.EnsureHandle();
        MonitorDoDesktop monitor = MonitorDasJanelas();
        Win32.SetWindowPos(ajudante.Handle, 0, monitor.AreaUtil.Esquerda, monitor.AreaUtil.Topo, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        // Com o "sempre no topo" desligado, o personagem sobe acima das janelas comuns, para o painel não flutuar ao lado
        // de um personagem coberto (DEC-038, item 8); os itens vão junto, logo abaixo dele (L17), como ao mostrar.
        _personagem?.AoTopoDaFaixa();
        _itens?.ReordenarAbaixoDoPersonagem();
        painel.Show();
        PosicionarPainel(painel);
        painel.Activate();
        Diagnostico.Evento("PAINEL", ("aberto", "sim"), ("hwnd", ajudante.Handle), ("ret", painel.RetanguloNaTela()?.ToString() ?? "-"), ("dpi", monitor.Dpi));

        // Uma conferência só, depois de a ativação ser processada: sem foco, o painel fecha e avisa o núcleo.
        _app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (!ReferenceEquals(_painel, painel) || painel.IsActive) return;
            painel.FecharPeloUsuario("semFoco");
        });
    }

    /// <summary>Põe o painel ao lado do personagem, inteiro na área útil do monitor dele, pelo tamanho real da janela.</summary>
    private void PosicionarPainel(PainelDeEnergia painel)
    {
        if (painel.RetanguloNaTela() is not { } atual) return;
        MonitorDoDesktop monitor = MonitorDasJanelas();
        int folga = (int)Math.Round(FolgaDoPainelDip * monitor.Dpi / 96.0);
        RetanguloPx destino = AncoragemDoPainel.Calcular(_posicionamento.Retangulo, atual.Tamanho, monitor.AreaUtil, folga, _nucleo?.Estado.Esconderijo ?? LadoDoEsconderijo.Nenhum);
        if (destino.Esquerda == atual.Esquerda && destino.Topo == atual.Topo) return;
        Win32.SetWindowPos(painel.Hwnd, 0, destino.Esquerda, destino.Topo, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    /// <summary>O núcleo fechou o painel (arraste, esconder, sair): fecha sem avisar.</summary>
    private void FecharPainelDeEnergiaPeloNucleo()
    {
        PainelDeEnergia? painel = _painel;
        _painel = null;
        if (painel is null) return;
        Diagnostico.Evento("PAINEL", ("fechado", "nucleo"));
        painel.FecharPeloNucleo();
    }

    /// <summary>
    /// Aplica o "sempre no topo" ao personagem e aos itens (DEC-038, item 8), na partida (antes de mostrar) ou pelo efeito do
    /// comando; os itens à vista voltam para logo abaixo do personagem. Uma vez, por evento.
    /// </summary>
    private void AplicarSempreNoTopo(bool ligado, string motivo)
    {
        if (_personagem is null) return;
        _personagem.AplicarSempreNoTopo(ligado);
        _itens?.AplicarSempreNoTopo(ligado);
        _itens?.ReordenarAbaixoDoPersonagem();
        Diagnostico.Evento("TOPO", ("ligado", ligado ? "sim" : "nao"), ("motivo", motivo));
    }

    /// <summary>A marca das janelas abertas acompanha o que o núcleo gravou (o efeito é o único canal; DEC-038).</summary>
    private void AtualizarJanelasDasPreferencias(Preferencias preferencias)
    {
        _painel?.Marcar(preferencias.Energia);
        _configuracoes?.Atualizar(preferencias);
    }
}
