using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// A ligação da curiosidade da Fase 7 (DEC-037): a avaliação da tela cheia candidata o monitor do primeiro plano, a carência
/// (<see cref="CarenciaDoFoco"/>) o confirma depois de 2 s e o publica ao núcleo (FOREGROUND_MONITOR_CHANGED), só quando
/// muda; o disparo da curiosidade é um temporizador único com geração, adiado enquanto o menu de contexto está aberto; e o
/// vão é lido uma vez por pedido do núcleo (ACTIVE_WINDOW_SPAN), sem guardar nada. Nenhuma linha de log por troca de foco:
/// o log só leva as decisões do núcleo e, no fim, quantos vãos foram pedidos. Sem o observador (<c>--sem-tela-cheia</c>),
/// nenhum foco chega, e a curiosidade não age.
/// </summary>
internal sealed partial class Aplicacao
{
    private CarenciaDoFoco? _carenciaDoFoco;
    private TemporizadorDaOnda? _temporizadorDaCuriosidade;
    private bool _menuAberto;
    private long? _curiosidadeAdiada;

    /// <summary>
    /// O receptor do candidato a foco para a agenda da tela cheia, com a curiosidade ligada; nulo sem ela. Cria a carência e o
    /// temporizador da curiosidade.
    /// </summary>
    private Action<string>? IniciarCuriosidade()
    {
        if (_nucleo?.Configuracao.Curiosidade != true) return null;
        _temporizadorDaCuriosidade = new TemporizadorDaOnda(AoDispararCuriosidade);
        _carenciaDoFoco = new CarenciaDoFoco((espera, acao) => DisparoUnico.NoDispatcher(espera, acao, System.Windows.Threading.DispatcherPriority.Normal),
            chave => Enviar(new ForegroundMonitorChanged(chave), "foco"));
        return chave => _carenciaDoFoco?.Candidatar(chave);
    }

    /// <summary>Os efeitos da curiosidade: o disparo único e o pedido do vão, respondido depois do processamento.</summary>
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
                // Uma leitura, convertida na hora pela agenda; o vão só vive no evento.
                Adiar(() => Enviar(new ActiveWindowSpan(pedido.Geracao, _telaCheia?.LerVao(pedido.Chave)), "vão"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(efeito), efeito, "Efeito da curiosidade sem adaptador.");
        }
    }

    /// <summary>O disparo da curiosidade: com o menu de contexto aberto, espera ele fechar (DEC-037, item 6).</summary>
    private void AoDispararCuriosidade(long geracao)
    {
        if (_menuAberto)
        {
            _curiosidadeAdiada = geracao;
            return;
        }
        Enviar(new CuriosityTimer(geracao), "curiosidade");
    }

    /// <summary>Depois do menu: o disparo que chegou com ele aberto sai agora, depois do comando escolhido.</summary>
    private void EntregarCuriosidadeAdiada()
    {
        if (_menuAberto || _curiosidadeAdiada is not { } geracao || _encerrando) return;
        _curiosidadeAdiada = null;
        Enviar(new CuriosityTimer(geracao), "curiosidade (depois do menu)");
    }

    /// <summary>Encerramento: a carência e o disparo param; nada mais chega ao núcleo.</summary>
    private void PararCuriosidade()
    {
        _carenciaDoFoco?.Parar();
        _temporizadorDaCuriosidade?.Parar();
        _curiosidadeAdiada = null;
    }
}
