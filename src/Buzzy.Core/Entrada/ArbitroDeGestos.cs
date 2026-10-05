using Buzzy.Core.Personagem;

namespace Buzzy.Core.Entrada;

/// <summary>
/// Botão do mouse na convenção das mensagens do Windows: <see cref="Esquerdo"/> é o botão
/// primário, mesmo com os botões trocados para canhotos.
/// </summary>
public enum BotaoDoPonteiro
{
    Esquerdo,
    Direito,
}

/// <summary>
/// Métricas do sistema que separam clique, clique duplo e arraste (ARCHITECTURE.md 2.7), já
/// resolvidas pelo adaptador para o DPI do monitor em que o botão foi pressionado.
/// </summary>
/// <param name="ArrasteX">
/// <c>SM_CXDRAG</c>: pixels que o cursor pode andar para cada lado do ponto de pressão antes de o
/// gesto virar arraste.
/// </param>
/// <param name="ArrasteY"><c>SM_CYDRAG</c>, na vertical.</param>
/// <param name="CliqueDuploLargura">
/// <c>SM_CXDOUBLECLK</c>: largura do retângulo centrado no primeiro clique em que o segundo
/// precisa cair.
/// </param>
/// <param name="CliqueDuploAltura"><c>SM_CYDOUBLECLK</c>, na vertical.</param>
/// <param name="TempoDeCliqueDuploMs"><c>GetDoubleClickTime</c>, entre os dois botões pressionados.</param>
public sealed record MetricasDeGesto(int ArrasteX, int ArrasteY, int CliqueDuploLargura, int CliqueDuploAltura, int TempoDeCliqueDuploMs)
{
    /// <summary>Valores padrão do Windows a 96 DPI.</summary>
    public static readonly MetricasDeGesto Padrao = new(4, 4, 4, 4, 500);
}

/// <summary>
/// Evento de ponteiro normalizado pelo adaptador (ARCHITECTURE.md 2.6, tabela de eventos): só
/// chega o que o Windows entrega às janelas do Buzzy ou à captura de um gesto começado nele.
/// Coordenadas em pixels físicos do desktop virtual; <paramref name="Ms"/> é um relógio
/// monotônico em milissegundos.
/// </summary>
public abstract record EventoDePonteiro(long Ms);

/// <summary><c>POINTER_DOWN(p, botão)</c>, com as métricas do DPI do monitor do ponto.</summary>
public sealed record PonteiroPressionado(PontoPx Ponto, BotaoDoPonteiro Botao, long Ms, MetricasDeGesto Metricas) : EventoDePonteiro(Ms);

/// <summary>
/// <c>POINTER_MOVE(p)</c>. <paramref name="EsquerdoPressionado"/> vem da própria mensagem
/// (<c>MK_LBUTTON</c>): com o ClickLock ligado, o Windows mantém o botão logicamente pressionado.
/// </summary>
public sealed record PonteiroMovido(PontoPx Ponto, bool EsquerdoPressionado, long Ms) : EventoDePonteiro(Ms);

/// <summary><c>POINTER_UP(p, botão)</c>.</summary>
public sealed record PonteiroSolto(PontoPx Ponto, BotaoDoPonteiro Botao, long Ms) : EventoDePonteiro(Ms);

/// <summary><c>CAPTURE_LOST</c>: outra janela ficou com o mouse (Alt+Tab, UAC, tecla Windows).</summary>
public sealed record CapturaPerdida(long Ms) : EventoDePonteiro(Ms);

/// <summary>Resultado de um evento de ponteiro.</summary>
/// <param name="Gestos">Gestos para a fila do núcleo, na ordem.</param>
/// <param name="Capturar">
/// Se o adaptador mantém a captura do mouse depois deste evento: verdadeiro do botão esquerdo
/// pressionado até o fim do gesto (ARCHITECTURE.md 2.7, passo 1 do ciclo).
/// </param>
public sealed record Arbitragem(IReadOnlyList<Evento> Gestos, bool Capturar);

/// <summary>
/// Arbitragem de input (ARCHITECTURE.md 2.2 e 2.7): converte eventos de ponteiro nos gestos
/// <c>PRESS</c>, <c>CLICK</c>, <c>DOUBLE_CLICK</c>, <c>DRAG_START</c>, <c>DRAG_MOVE</c>,
/// <c>DRAG_END</c>, <c>DRAG_CANCEL</c> e <c>CONTEXT_MENU</c>, com as regras do Windows:
/// <list type="bullet">
/// <item>arraste quando o cursor sai do retângulo <c>SM_CXDRAG</c> × <c>SM_CYDRAG</c> de cada lado
/// do ponto de pressão; soltar dentro dele é clique, sem limite de tempo (vale para o ClickLock);</item>
/// <item>clique duplo quando o segundo botão pressionado chega antes de <c>GetDoubleClickTime</c>
/// e a menos de meio <c>SM_CXDOUBLECLK</c> × <c>SM_CYDOUBLECLK</c> do primeiro; o primeiro clique
/// sai na hora, sem esperar o segundo;</item>
/// <item>botão direito solto fora de um gesto do esquerdo pede o menu;</item>
/// <item>captura perdida, novo botão pressionado sem o soltar anterior ou movimento sem o botão
/// esquerdo encerram o gesto com <c>DRAG_CANCEL</c>: nada fica preso ao cursor.</item>
/// </list>
/// Não lê relógio nem sistema: tempo e métricas entram nos eventos. Não é seguro para várias
/// threads; a raiz de composição o usa só na thread da interface.
/// </summary>
public sealed class ArbitroDeGestos
{
    private enum Fase
    {
        Livre,
        Pressionado,
        Arrastando,
    }

    private readonly record struct Clique(PontoPx Ponto, long Ms, MetricasDeGesto Metricas);

    private Fase _fase;
    private PontoPx _pressao;
    private long _msDaPressao;
    private MetricasDeGesto _metricas = MetricasDeGesto.Padrao;
    private bool _segundoClique;
    private Clique? _ultimoClique;

    /// <summary>Se há um gesto do botão esquerdo em curso (pressionado ou arrastando).</summary>
    public bool EmGesto => _fase != Fase.Livre;

    /// <summary>Se o gesto em curso já passou do limiar de arraste.</summary>
    public bool Arrastando => _fase == Fase.Arrastando;

    public Arbitragem Receber(EventoDePonteiro evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var gestos = new List<Evento>(2);
        switch (evento)
        {
            case PonteiroPressionado { Botao: BotaoDoPonteiro.Esquerdo } p:
                Pressionar(p, gestos);
                break;
            case PonteiroPressionado:
                // Botão direito: o menu só abre ao soltar (ARCHITECTURE.md 2.7, passo 5).
                break;
            case PonteiroMovido m:
                Mover(m, gestos);
                break;
            case PonteiroSolto { Botao: BotaoDoPonteiro.Esquerdo } s:
                SoltarEsquerdo(s.Ponto, gestos);
                break;
            case PonteiroSolto s:
                // Durante um gesto do botão esquerdo, o direito não abre menu.
                if (_fase == Fase.Livre) gestos.Add(new ContextMenu(s.Ponto));
                break;
            case CapturaPerdida:
                Cancelar(gestos);
                break;
            default:
                throw new ArgumentException($"Evento de ponteiro desconhecido: {evento}.", nameof(evento));
        }
        return new Arbitragem(gestos, EmGesto);
    }

    /// <summary>
    /// Esquece o gesto em curso sem emitir nada. Para quando o núcleo já o encerrou por conta
    /// própria (esconder ou sair no meio do arraste) e o adaptador soltou a captura.
    /// </summary>
    public void Reiniciar()
    {
        _fase = Fase.Livre;
        _ultimoClique = null;
    }

    private void Pressionar(PonteiroPressionado p, List<Evento> gestos)
    {
        // Um soltar que nunca chegou (captura perdida sem aviso): o gesto anterior termina
        // como cancelado antes de o novo começar.
        Cancelar(gestos);

        _segundoClique = CompletaCliqueDuplo(p);
        _fase = Fase.Pressionado;
        _pressao = p.Ponto;
        _msDaPressao = p.Ms;
        _metricas = p.Metricas;
        gestos.Add(new Press(p.Ponto));
    }

    private void Mover(PonteiroMovido m, List<Evento> gestos)
    {
        if (_fase == Fase.Livre) return;
        if (!m.EsquerdoPressionado)
        {
            // O botão já não está pressionado e o soltar não chegou: encerrar em vez de deixar
            // o personagem grudado no cursor (critério 4 da Fase 3).
            Cancelar(gestos);
            return;
        }
        if (_fase == Fase.Pressionado)
        {
            if (!ForaDoLimiar(m.Ponto)) return;
            _fase = Fase.Arrastando;
            _ultimoClique = null;
            gestos.Add(new DragStart());
        }
        gestos.Add(new DragMove(m.Ponto));
    }

    private void SoltarEsquerdo(PontoPx ponto, List<Evento> gestos)
    {
        switch (_fase)
        {
            case Fase.Livre:
                // O botão foi pressionado noutra janela; nada a fazer.
                return;
            case Fase.Pressionado when ForaDoLimiar(ponto):
                // Gesto rápido: soltou fora do retângulo sem nenhum movimento no meio. É arraste,
                // e o personagem vai para onde o botão foi solto.
                gestos.Add(new DragStart());
                gestos.Add(new DragEnd(ponto));
                _ultimoClique = null;
                break;
            case Fase.Pressionado when _segundoClique:
                gestos.Add(new DoubleClick());
                // Um terceiro clique começa uma sequência nova, como no Windows.
                _ultimoClique = null;
                break;
            case Fase.Pressionado:
                gestos.Add(new Click());
                _ultimoClique = new Clique(_pressao, _msDaPressao, _metricas);
                break;
            case Fase.Arrastando:
                gestos.Add(new DragEnd(ponto));
                _ultimoClique = null;
                break;
        }
        _fase = Fase.Livre;
    }

    private void Cancelar(List<Evento> gestos)
    {
        if (_fase == Fase.Livre) return;
        gestos.Add(new DragCancel());
        _fase = Fase.Livre;
        _ultimoClique = null;
    }

    /// <summary>"Pixels de cada lado": sai do retângulo quem anda mais que o limiar num dos eixos.</summary>
    private bool ForaDoLimiar(PontoPx p)
        => Math.Abs((long)p.X - _pressao.X) > Math.Abs(_metricas.ArrasteX)
        || Math.Abs((long)p.Y - _pressao.Y) > Math.Abs(_metricas.ArrasteY);

    /// <summary>
    /// A regra do Windows para o segundo botão pressionado: menos que o tempo de clique duplo
    /// desde o primeiro e menos que meio retângulo de clique duplo de distância dele.
    /// </summary>
    private bool CompletaCliqueDuplo(PonteiroPressionado p)
    {
        if (_ultimoClique is not { } primeiro) return false;
        long decorrido = p.Ms - primeiro.Ms;
        if (decorrido < 0 || decorrido >= primeiro.Metricas.TempoDeCliqueDuploMs) return false;
        // Metade inteira, como o próprio Windows compara (4 px de largura aceitam 1 px de distância).
        return Math.Abs((long)p.Ponto.X - primeiro.Ponto.X) < Math.Abs(primeiro.Metricas.CliqueDuploLargura) / 2
            && Math.Abs((long)p.Ponto.Y - primeiro.Ponto.Y) < Math.Abs(primeiro.Metricas.CliqueDuploAltura) / 2;
    }
}
