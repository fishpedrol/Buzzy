using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

// Curiosidade: a agenda da tela cheia sugere o monitor do foco, a carência confirma
// depois de 2 s e manda pro núcleo só quando muda. O disparo espera o menu de contexto
// fechar. O vão da janela ativa é lido uma vez por pedido, sem guardar. Nada vai pro
// log a cada troca de foco. Com --sem-tela-cheia não chega foco e ela não age.
internal sealed partial class Aplicacao
{
    private CarenciaDoFoco? _carenciaDoFoco;
    private TemporizadorDaOnda? _temporizadorDaCuriosidade;
    private bool _menuAberto;
    private long? _curiosidadeAdiada;

    // Nulo com a curiosidade desligada.
    private Action<string>? IniciarCuriosidade()
    {
        if (_nucleo?.Configuracao.Curiosidade != true) return null;
        _temporizadorDaCuriosidade = new TemporizadorDaOnda(AoDispararCuriosidade);
        _carenciaDoFoco = new CarenciaDoFoco((espera, acao) => DisparoUnico.NoDispatcher(espera, acao, System.Windows.Threading.DispatcherPriority.Normal),
            chave => Enviar(new ForegroundMonitorChanged(chave), "foco"));
        return chave => _carenciaDoFoco?.Candidatar(chave);
    }

    private void ExecutarEfeitoDaCuriosidade(Efeito efeito)
    {
        switch (efeito)
        {
            case AgendarCuriosidade agendar:
                _temporizadorDaCuriosidade?.Agendar(agendar.Atraso, agendar.Geracao);
                break;
            case CancelarCuriosidade:
                _temporizadorDaCuriosidade?.Cancelar();
                _curiosidadeAdiada = null;
                break;
            case PedirVaoDaJanelaAtiva pedido:
                // Lido e convertido na hora; o vão só existe dentro do evento.
                Adiar(() => Enviar(new ActiveWindowSpan(pedido.Geracao, _telaCheia?.LerVao(pedido.Chave)), "vão"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(efeito), efeito, "Efeito da curiosidade sem adaptador.");
        }
    }

    // Com o menu de contexto aberto, espera ele fechar.
    private void AoDispararCuriosidade(long geracao)
    {
        if (_menuAberto)
        {
            _curiosidadeAdiada = geracao;
            return;
        }
        Enviar(new CuriosityTimer(geracao), "curiosidade");
    }

    // Sai depois do comando escolhido no menu.
    private void EntregarCuriosidadeAdiada()
    {
        if (_menuAberto || _curiosidadeAdiada is not { } geracao || _encerrando) return;
        _curiosidadeAdiada = null;
        Enviar(new CuriosityTimer(geracao), "curiosidade (depois do menu)");
    }

    private void PararCuriosidade()
    {
        _carenciaDoFoco?.Parar();
        _temporizadorDaCuriosidade?.Parar();
        _curiosidadeAdiada = null;
    }
}
