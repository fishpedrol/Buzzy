using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Janela de configurações: uma só (se já aberta, só ativa), longe do sprite. Cada
// controle vira o comando do seu campo; o núcleo grava e a marca volta pelo
// GravarPreferencias. O app nunca monta SETTINGS_CHANGED, e fechar não avisa o núcleo.
internal sealed partial class Aplicacao
{
    private JanelaDeConfiguracoes? _configuracoes;

    // Escolhida na partida, sem tocar o registro.
    private IInicioComOWindows _inicio = new InicioIndisponivel();

    // Tamanho lido na partida; mudar a escala só vale na próxima abertura.
    private EscalaDoPersonagem _escalaEmVigor = EscalaDoPersonagem.Media;

    private void AbrirConfiguracoesPeloNucleo()
    {
        if (_encerrando || _nucleo is null) return;
        if (_configuracoes is not null)
        {
            // Minimizada (Win+D), Activate não a traz de volta: restaura antes.
            if (_configuracoes.WindowState == System.Windows.WindowState.Minimized) _configuracoes.WindowState = System.Windows.WindowState.Normal;
            _configuracoes.Activate();
            Diagnostico.Evento("CONFIGURACOES", ("ativada", "sim"));
            return;
        }
        var janela = new JanelaDeConfiguracoes(_nucleo.Estado.Preferencias, _escalaEmVigor, _nucleo.Configuracao.Tamagotchi, _nucleo.Configuracao.ItensDaEdicao);
        janela.PediuEnergia += nivel => Enviar(new CmdSetEnergy(nivel), "configurações");
        janela.PediuTelaCheia += ligado => Enviar(new CmdSetFullscreenMode(ligado), "configurações");
        janela.PediuAdulto += ligado => Enviar(new CmdSetAdultContent(ligado), "configurações");
        janela.PediuItemAdulto += (item, ligado) => Enviar(new CmdSetAdultItemEnabled(item, ligado), "configurações");
        janela.PediuPorContaPropria += (item, ligado) => Enviar(new CmdSetSelfUseItem(item, ligado), "configurações");
        janela.PediuTravessia += ligado => Enviar(new CmdSetCrossMonitors(ligado), "configurações");
        janela.PediuEscala += escala => Enviar(new CmdSetScale(escala), "configurações");
        janela.PediuTopo += ligado => Enviar(new CmdSetAlwaysOnTop(ligado), "configurações");
        // O início com o Windows não passa pelo núcleo: a caixa fala direto com a porta, só no clique.
        var inicio = new ControleDoInicio(janela.Inicio, janela.EstadoDoInicio, _inicio, campos => Diagnostico.Evento("INICIO", campos));
        inicio.Atualizar("aberta");
        janela.Ativada += () => inicio.Atualizar("ativada");
        janela.Closed += (_, _) =>
        {
            if (ReferenceEquals(_configuracoes, janela)) _configuracoes = null;
            Diagnostico.Evento("CONFIGURACOES", ("fechada", "sim"));
        };
        _configuracoes = janela;

        // Põe o HWND no monitor antes do Show pra pegar o DPI de lá; o lugar final só depois, com o tamanho real.
        var ajudante = new System.Windows.Interop.WindowInteropHelper(janela);
        ajudante.EnsureHandle();
        MonitorDoDesktop monitor = MonitorDasJanelas();
        Win32.SetWindowPos(ajudante.Handle, 0, monitor.AreaUtil.Esquerda, monitor.AreaUtil.Topo, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        janela.LimitarAltura(LugarDasConfiguracoes.AlturaMaximaDip(monitor));
        janela.Show();
        PosicionarConfiguracoes(janela);
        janela.DpiChanged += (_, _) => PosicionarConfiguracoes(janela);
        // Cresce com a linha do início ou o aviso do tamanho; tem que continuar dentro da área útil.
        janela.SizeChanged += (_, _) => PosicionarConfiguracoes(janela);
        janela.Activate();
        Diagnostico.Evento("CONFIGURACOES", ("aberta", "sim"), ("hwnd", ajudante.Handle), ("ret", janela.RetanguloNaTela()?.ToString() ?? "-"), ("dpi", monitor.Dpi));
    }

    private MonitorDoDesktop MonitorDasJanelas()
        => LugarDasConfiguracoes.Monitor(_topologia, _posicionamento.Monitor, _posicionamento.Retangulo);

    private void PosicionarConfiguracoes(JanelaDeConfiguracoes janela)
    {
        if (janela.RetanguloNaTela() is not { } atual) return;
        MonitorDoDesktop monitor = MonitorDasJanelas();
        janela.LimitarAltura(LugarDasConfiguracoes.AlturaMaximaDip(monitor));
        bool spriteAqui = _visivel && monitor.Chave == _posicionamento.Monitor.Chave;
        RetanguloPx destino = LugarDasConfiguracoes.Calcular(monitor.AreaUtil, spriteAqui ? _posicionamento.Retangulo : null, atual.Tamanho);
        if (destino.Esquerda == atual.Esquerda && destino.Topo == atual.Topo) return;
        Win32.SetWindowPos(janela.Hwnd, 0, destino.Esquerda, destino.Topo, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    private void FecharConfiguracoes()
    {
        JanelaDeConfiguracoes? janela = _configuracoes;
        _configuracoes = null;
        janela?.Close();
    }
}
