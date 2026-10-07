using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Buzzy.App.Apresentacao;
using Buzzy.App.Plataforma;

namespace Buzzy.App.Composicao;

// Caixa "Iniciar com o Windows". Só o clique na caixa liga ou desliga, e depois
// de cada ação o estado é relido: a marca vem sempre da leitura, nunca mente sobre
// o registro. A linha de estado é região viva pro Narrador; falha fica nela até a
// próxima leitura, e só o número do erro do Windows vai pro log.
internal sealed class ControleDoInicio
{
    private readonly CaixaDeComando _caixa;
    private readonly TextBlock _estado;
    private readonly IInicioComOWindows _porta;
    private readonly Action<(string Campo, object? Valor)[]> _registrar;

    internal ControleDoInicio(CaixaDeComando caixa, TextBlock estado, IInicioComOWindows porta, Action<(string Campo, object? Valor)[]> registrar)
    {
        _caixa = caixa ?? throw new ArgumentNullException(nameof(caixa));
        _estado = estado ?? throw new ArgumentNullException(nameof(estado));
        _porta = porta ?? throw new ArgumentNullException(nameof(porta));
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        AutomationProperties.SetLiveSetting(_estado, AutomationLiveSetting.Polite);
        _caixa.Pedido += AoPedido;
    }

    internal EstadoDoInicio Estado { get; private set; } = EstadoDoInicio.Indisponivel;

    internal bool Falhou { get; private set; }

    // Chamado na abertura, na ativação da janela e depois de cada ação.
    internal void Atualizar(string motivo) => Ler(motivo, falhou: false);

    private void Ler(string motivo, bool falhou)
    {
        Falhou = falhou;
        Estado = _porta.Ler();
        _registrar(ComCodigo([("acao", "ler"), ("motivo", motivo), ("modo", _porta.Modo), ("estado", Estado)]));
        Mostrar();
    }

    private void AoPedido(bool marcar)
    {
        bool? ligar = RegrasDoInicio.AcaoDoPedido(Estado, marcar);
        if (ligar is not { } acao) return;
        ResultadoDoInicio resultado = acao ? _porta.Ligar() : _porta.Desligar();
        _registrar(ComCodigo([("acao", acao ? "ligar" : "desligar"), ("modo", _porta.Modo), ("resultado", resultado)]));
        Ler("depois da ação", falhou: resultado is ResultadoDoInicio.Erro or ResultadoDoInicio.NaoEDestaCopia);
    }

    private (string Campo, object? Valor)[] ComCodigo((string Campo, object? Valor)[] campos)
        => _porta.UltimoErro is { } codigo ? [.. campos, ("codigo", codigo)] : campos;

    private void Mostrar()
    {
        _caixa.Marcar(Estado is EstadoDoInicio.Ligado or EstadoDoInicio.DesativadoPeloWindows);
        _caixa.IsEnabled = Estado != EstadoDoInicio.Indisponivel;
        string texto = Estado switch
        {
            EstadoDoInicio.DesativadoPeloWindows => Textos.ConfigInicioDesativado,
            EstadoDoInicio.OutroCaminho => Textos.ConfigInicioOutraCopia,
            EstadoDoInicio.Indisponivel => Textos.ConfigInicioIndisponivel,
            _ => "",
        };
        if (Falhou) texto = (Textos.ConfigInicioFalhou + " " + texto).Trim();
        if (_porta.Modo == ModoDoInicio.Simulado) texto = (texto + " " + Textos.ConfigInicioSimulado).Trim();
        bool mudou = _estado.Text != texto;
        _estado.Text = texto;
        _estado.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetHelpText(_caixa, texto.Length == 0 ? Textos.ConfigInicioAjuda : $"{Textos.ConfigInicioAjuda} {texto}");
        if (mudou && UIElementAutomationPeer.FromElement(_estado) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
