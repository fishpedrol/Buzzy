using System.Windows.Threading;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Painel de energia. Se abrir sem foco, fecha logo (uma conferência adiada, sem timer),
// pra não pausar a autonomia sem ninguém ver. Fechado pelo usuário avisa o núcleo uma
// vez (ENERGY_PANEL_CLOSE); fechado pelo núcleo, não avisa.
internal sealed partial class Aplicacao
{
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

        // HWND no monitor do personagem antes do Show, pro WPF pegar o DPI de lá; o lugar final vem depois.
        var ajudante = new System.Windows.Interop.WindowInteropHelper(painel);
        ajudante.EnsureHandle();
        MonitorDoDesktop monitor = MonitorDasJanelas();
        Win32.SetWindowPos(ajudante.Handle, 0, monitor.AreaUtil.Esquerda, monitor.AreaUtil.Topo, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        // Sem "sempre no topo", sobe o personagem acima das janelas comuns pro painel não ficar
        // ao lado de um personagem coberto; os itens vão junto, logo abaixo dele.
        _personagem?.AoTopoDaFaixa();
        _itens?.ReordenarAbaixoDoPersonagem();
        painel.Show();
        PosicionarPainel(painel);
        painel.Activate();
        Diagnostico.Evento("PAINEL", ("aberto", "sim"), ("hwnd", ajudante.Handle), ("ret", painel.RetanguloNaTela()?.ToString() ?? "-"), ("dpi", monitor.Dpi));

        // Confere uma vez, depois da ativação processada: sem foco, fecha e avisa o núcleo.
        _app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (!ReferenceEquals(_painel, painel) || painel.IsActive) return;
            painel.FecharPeloUsuario("semFoco");
        });
    }

    private void PosicionarPainel(PainelDeEnergia painel)
    {
        if (painel.RetanguloNaTela() is not { } atual) return;
        MonitorDoDesktop monitor = MonitorDasJanelas();
        int folga = (int)Math.Round(FolgaDoPainelDip * monitor.Dpi / 96.0);
        RetanguloPx destino = AncoragemDoPainel.Calcular(_posicionamento.Retangulo, atual.Tamanho, monitor.AreaUtil, folga, _nucleo?.Estado.Esconderijo ?? LadoDoEsconderijo.Nenhum);
        if (destino.Esquerda == atual.Esquerda && destino.Topo == atual.Topo) return;
        Win32.SetWindowPos(painel.Hwnd, 0, destino.Esquerda, destino.Topo, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    // Arraste, esconder ou sair: fecha sem avisar o núcleo.
    private void FecharPainelDeEnergiaPeloNucleo()
    {
        PainelDeEnergia? painel = _painel;
        _painel = null;
        if (painel is null) return;
        Diagnostico.Evento("PAINEL", ("fechado", "nucleo"));
        painel.FecharPeloNucleo();
    }

    // Na partida (antes de mostrar) ou pelo comando. Os itens voltam pra logo abaixo do personagem.
    private void AplicarSempreNoTopo(bool ligado, string motivo)
    {
        if (_personagem is null) return;
        _personagem.AplicarSempreNoTopo(ligado);
        _itens?.AplicarSempreNoTopo(ligado);
        _itens?.ReordenarAbaixoDoPersonagem();
        Diagnostico.Evento("TOPO", ("ligado", ligado ? "sim" : "nao"), ("motivo", motivo));
    }

    // As janelas abertas só mudam a marca por aqui, com o que o núcleo gravou.
    private void AtualizarJanelasDasPreferencias(Preferencias preferencias)
    {
        _painel?.Marcar(preferencias.Energia);
        _configuracoes?.Atualizar(preferencias);
    }
}
