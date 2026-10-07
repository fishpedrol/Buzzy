using Buzzy.Core.Personagem;

namespace Buzzy.Core.Entrada;

// Como nas mensagens do Windows: Esquerdo é o primário, mesmo com botões trocados pra canhoto.
public enum BotaoDoPonteiro
{
    Esquerdo,
    Direito,
}

// Métricas do sistema já no DPI do monitor onde o botão foi pressionado.
// Arraste = SM_CXDRAG/SM_CYDRAG (px pra cada lado do ponto de pressão).
// CliqueDuplo = SM_CXDOUBLECLK/SM_CYDOUBLECLK (retângulo centrado no 1º clique) e GetDoubleClickTime.
public sealed record MetricasDeGesto(int ArrasteX, int ArrasteY, int CliqueDuploLargura, int CliqueDuploAltura, int TempoDeCliqueDuploMs)
{
    // Padrão do Windows a 96 DPI.
    public static readonly MetricasDeGesto Padrao = new(4, 4, 4, 4, 500);
}

// Só chega o que o Windows entrega às janelas do Buzzy ou à captura de um gesto começado nele.
// Pixels físicos do desktop virtual; Ms é relógio monotônico.
public abstract record EventoDePonteiro(long Ms);

public sealed record PonteiroPressionado(PontoPx Ponto, BotaoDoPonteiro Botao, long Ms, MetricasDeGesto Metricas) : EventoDePonteiro(Ms);

// EsquerdoPressionado vem do MK_LBUTTON da mensagem: com ClickLock, o Windows mantém o botão
// logicamente pressionado.
public sealed record PonteiroMovido(PontoPx Ponto, bool EsquerdoPressionado, long Ms) : EventoDePonteiro(Ms);

public sealed record PonteiroSolto(PontoPx Ponto, BotaoDoPonteiro Botao, long Ms) : EventoDePonteiro(Ms);

// Outra janela ficou com o mouse (Alt+Tab, UAC, tecla Windows).
public sealed record CapturaPerdida(long Ms) : EventoDePonteiro(Ms);

// Capturar: manter a captura do mouse, do esquerdo pressionado até o fim do gesto.
public sealed record Arbitragem(IReadOnlyList<Evento> Gestos, bool Capturar);

// Transforma eventos de ponteiro em gestos com as regras do Windows:
// - arraste quando sai do retângulo SM_CXDRAG x SM_CYDRAG; soltar dentro é clique, sem limite
//   de tempo (por causa do ClickLock);
// - clique duplo se o 2º press vem antes de GetDoubleClickTime e a menos de meio retângulo de
//   clique duplo; o 1º clique sai na hora, sem esperar;
// - direito solto fora de um gesto do esquerdo abre o menu;
// - captura perdida, press sem o soltar anterior ou movimento sem o esquerdo viram DragCancel,
//   pra nada ficar grudado no cursor.
// Não lê relógio nem sistema. Não é thread-safe: só a thread da UI usa.
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

    public bool EmGesto => _fase != Fase.Livre;

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
                // Direito: o menu só abre ao soltar.
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

    // Esquece o gesto sem emitir nada: o núcleo já encerrou (esconder ou sair no meio do arraste)
    // e a captura já foi solta.
    public void Reiniciar()
    {
        _fase = Fase.Livre;
        _ultimoClique = null;
    }

    private void Pressionar(PonteiroPressionado p, List<Evento> gestos)
    {
        // Se o soltar anterior nunca chegou, cancela o gesto velho antes do novo.
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
            // Botão solto sem o evento de soltar: encerra pra não grudar no cursor.
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
                // Pressionado noutra janela.
                return;
            case Fase.Pressionado when ForaDoLimiar(ponto):
                // Gesto rápido: soltou longe sem nenhum move no meio. Conta como arraste.
                gestos.Add(new DragStart());
                gestos.Add(new DragEnd(ponto));
                _ultimoClique = null;
                break;
            case Fase.Pressionado when _segundoClique:
                gestos.Add(new DoubleClick());
                // O 3º clique começa sequência nova, como no Windows.
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

    // Limiar vale pra cada lado: sai quem anda mais que ele em qualquer eixo.
    private bool ForaDoLimiar(PontoPx p)
        => Math.Abs((long)p.X - _pressao.X) > Math.Abs(_metricas.ArrasteX)
        || Math.Abs((long)p.Y - _pressao.Y) > Math.Abs(_metricas.ArrasteY);

    private bool CompletaCliqueDuplo(PonteiroPressionado p)
    {
        if (_ultimoClique is not { } primeiro) return false;
        long decorrido = p.Ms - primeiro.Ms;
        if (decorrido < 0 || decorrido >= primeiro.Metricas.TempoDeCliqueDuploMs) return false;
        // Metade inteira, como o Windows: 4 px de largura aceitam 1 px de distância.
        return Math.Abs((long)p.Ponto.X - primeiro.Ponto.X) < Math.Abs(primeiro.Metricas.CliqueDuploLargura) / 2
            && Math.Abs((long)p.Ponto.Y - primeiro.Ponto.Y) < Math.Abs(primeiro.Metricas.CliqueDuploAltura) / 2;
    }
}
