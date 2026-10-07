namespace Buzzy.Core;

// Posição que sobrevive a mudança de monitores: chave do monitor, frações 0..1 dentro da área
// útil dele e a última âncora absoluta, de reserva se o monitor sumir.
public sealed record PosicaoDoPersonagem(string ChaveMonitor, double FracaoX, double FracaoY, PontoPx AncoraAbsoluta)
{
    // Tela do monitor da última vez que a posição foi descrita nele (nula = desconhecida). Serve pra
    // achar o monitor quando a chave muda (Restaurar, MonitorCorrespondente). Nunca é deslocada por
    // cálculo: sempre é a tela de um monitor real ou a de antes. Fora do construtor de propósito.
    public RetanguloPx? TelaDoMonitor { get; init; }
}

// Retangulo é o da janela; Tamanho, em pixels físicos.
public sealed record Posicionamento(MonitorDoDesktop Monitor, PontoPx Ancora, TamanhoPx Tamanho, RetanguloPx Retangulo);

// Como Restaurar achou o monitor da posição salva.
public enum OrigemDaRestauracao
{
    PelaChave,

    // Chave sumiu, mas há um monitor com a mesma tela salva.
    PeloRetangulo,

    NoPrincipal,
}

// A âncora é o ponto entre os pés (centro da borda de baixo do sprite). O sprite sempre
// termina inteiro dentro da área útil de algum monitor, se couber.
public static class Posicionador
{
    // Perto do canto inferior direito do principal, onde atrapalha menos. Só sem posição salva.
    public const double FracaoInicialX = 0.85;

    // Âncora na coluna Esquerda + Largura / 2 e na borda de baixo exclusiva (Base): com a
    // âncora no chão da área útil, a última linha do sprite é a última da área.
    public static RetanguloPx RetanguloDoSprite(PontoPx ancora, TamanhoPx tamanho)
    {
        int esquerda = ancora.X - tamanho.Largura / 2;
        return new RetanguloPx(esquerda, ancora.Y - tamanho.Altura, esquerda + tamanho.Largura, ancora.Y);
    }

    // Âncora mais próxima com o sprite inteiro na área útil. Mais largo que a área: centraliza.
    // Mais alto: pés no chão e a cabeça passa do topo.
    public static PontoPx PrenderNaAreaUtil(PontoPx ancora, TamanhoPx tamanho, RetanguloPx areaUtil)
    {
        if (areaUtil.Vazio) throw new ArgumentException("Área útil vazia.", nameof(areaUtil));
        if (tamanho.Largura <= 0 || tamanho.Altura <= 0) throw new ArgumentException($"Tamanho inválido: {tamanho}.", nameof(tamanho));

        int aEsquerda = tamanho.Largura / 2;
        int aDireita = tamanho.Largura - aEsquerda;

        int x = tamanho.Largura <= areaUtil.Largura
            ? Math.Clamp(ancora.X, areaUtil.Esquerda + aEsquerda, areaUtil.Direita - aDireita)
            : areaUtil.Esquerda + (areaUtil.Largura - tamanho.Largura) / 2 + aEsquerda;

        int y = tamanho.Altura <= areaUtil.Altura
            ? Math.Clamp(ancora.Y, areaUtil.Topo + tamanho.Altura, areaUtil.Base)
            : areaUtil.Base;

        return new PontoPx(x, y);
    }

    // Um pixel acima da âncora. A âncora é exclusiva: com monitores empilhados (ou barra oculta)
    // ela já cai no monitor de baixo. O monitor do personagem é o que contém os pés.
    public static PontoPx PixelDosPes(PontoPx ancora) => new(ancora.X, ancora.Y - 1);

    public static Posicionamento Inicial(Topologia topologia, TamanhoDip tamanho)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return NoMonitor(topologia.Principal, FracaoInicialX, 1.0, tamanho);
    }

    // Frações fora de [0, 1] são presas; NaN vira 0,5.
    public static Posicionamento NoMonitor(MonitorDoDesktop monitor, double fracaoX, double fracaoY, TamanhoDip tamanho)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        RetanguloPx area = monitor.AreaUtil;
        TamanhoPx fisico = tamanho.ParaPixels(monitor.Dpi);

        var desejada = new PontoPx(
            area.Esquerda + (int)Math.Round(SanearFracao(fracaoX) * area.Largura, MidpointRounding.AwayFromZero),
            area.Topo + (int)Math.Round(SanearFracao(fracaoY) * area.Altura, MidpointRounding.AwayFromZero));

        PontoPx ancora = PrenderNaAreaUtil(desejada, fisico, area);
        return new Posicionamento(monitor, ancora, fisico, RetanguloDoSprite(ancora, fisico));
    }

    public static PosicaoDoPersonagem Descrever(Posicionamento p)
    {
        ArgumentNullException.ThrowIfNull(p);
        RetanguloPx area = p.Monitor.AreaUtil;
        return new PosicaoDoPersonagem(
            p.Monitor.Chave,
            (p.Ancora.X - area.Esquerda) / (double)area.Largura,
            (p.Ancora.Y - area.Topo) / (double)area.Altura,
            p.Ancora)
        {
            TelaDoMonitor = p.Monitor.Tela,
        };
    }

    // Depois de mudar a topologia. Monitor ainda existe: mesma posição relativa na área útil atual
    // (cobre resolução, escala e barra). Sumiu: vai pro monitor mais próximo dos pés e passa a ser
    // dele, então não pula de volta se o original voltar.
    public static (Posicionamento Resultado, PosicaoDoPersonagem NovaPosicao) Reacomodar(
        Topologia nova, PosicaoDoPersonagem atual, TamanhoDip tamanho)
    {
        ArgumentNullException.ThrowIfNull(nova);
        ArgumentNullException.ThrowIfNull(atual);

        MonitorDoDesktop? mesmo = nova.PorChave(atual.ChaveMonitor);
        if (mesmo is not null)
        {
            Posicionamento r = NoMonitor(mesmo, atual.FracaoX, atual.FracaoY, tamanho);
            return (r, atual with { AncoraAbsoluta = r.Ancora, TelaDoMonitor = mesmo.Tela });
        }

        MonitorDoDesktop proximo = nova.MonitorMaisProximo(PixelDosPes(atual.AncoraAbsoluta));
        Posicionamento r2 = NoMonitor(proximo, atual.FracaoX, atual.FracaoY, tamanho);
        return (r2, Descrever(r2));
    }

    // Na partida: monitor da chave; senão, o primeiro com a mesma tela; senão, o principal. Sempre
    // com as frações saneadas. A âncora absoluta salva é ignorada: com outro principal, a origem
    // mudou. Restaurar o resultado de novo não move nada. Em execução, use Reacomodar.
    public static (Posicionamento Resultado, PosicaoDoPersonagem NovaPosicao, OrigemDaRestauracao Origem) Restaurar(
        Topologia topologia, PosicaoDoPersonagem salva, TamanhoDip tamanho)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(salva);

        (MonitorDoDesktop destino, OrigemDaRestauracao origem) =
            topologia.PorChave(salva.ChaveMonitor) is { } daChave ? (daChave, OrigemDaRestauracao.PelaChave)
            : MonitorComATela(topologia, salva.TelaDoMonitor) is { } daTela ? (daTela, OrigemDaRestauracao.PeloRetangulo)
            : (topologia.Principal, OrigemDaRestauracao.NoPrincipal);

        double fx = SanearFracao(salva.FracaoX), fy = SanearFracao(salva.FracaoY);
        Posicionamento r = NoMonitor(destino, fx, fy, tamanho);
        var nova = new PosicaoDoPersonagem(destino.Chave, fx, fy, r.Ancora) { TelaDoMonitor = destino.Tela };
        return (r, nova, origem);
    }

    private static MonitorDoDesktop? MonitorComATela(Topologia topologia, RetanguloPx? tela)
        => tela is { } t ? topologia.Monitores.FirstOrDefault(m => m.Tela == t) : null;

    // ---------------------------------------------------------------- topologia em execução

    // Monitor só transladado (rearranjo ou troca de principal, quando o Windows move a origem):
    // tela e área útil deslocadas pelo mesmo (dx, dy), mesmo DPI. Ser principal não conta.
    // dx = dy = 0 quer dizer que nada relevante mudou.
    public static bool SoTranslacao(MonitorDoDesktop antes, MonitorDoDesktop depois, out int dx, out int dy)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(depois);
        dx = depois.Tela.Esquerda - antes.Tela.Esquerda;
        dy = depois.Tela.Topo - antes.Tela.Topo;
        return depois.Tela == antes.Tela.Deslocado(dx, dy) && depois.AreaUtil == antes.AreaUtil.Deslocado(dx, dy) && depois.Dpi == antes.Dpi;
    }

    // Mesma chave; senão, o primeiro com a mesma tela e chave que não existia antes (a chave passou
    // de gdi: pra mon:, ou o driver mudou o caminho). Exigir chave nova evita confundir com o
    // sobrevivente que o Windows põe na origem quando o principal é desconectado.
    public static MonitorDoDesktop? MonitorCorrespondente(Topologia antiga, Topologia nova, string chave, RetanguloPx? tela)
    {
        ArgumentNullException.ThrowIfNull(antiga);
        ArgumentNullException.ThrowIfNull(nova);
        ArgumentNullException.ThrowIfNull(chave);
        if (nova.PorChave(chave) is { } mesmo) return mesmo;
        return tela is { } t ? nova.Monitores.FirstOrDefault(m => m.Tela == t && antiga.PorChave(m.Chave) is null) : null;
    }

    // Leva uma posição guardada pra topologia nova sem mover nada (posição do personagem e retorno
    // da tela cheia). Com monitor correspondente, adota chave e tela dele. Sem ele, mantém chave,
    // frações e tela (se o monitor voltar, vale de novo) e só a âncora absoluta anda junto com o
    // sobrevivente mais próximo dos pés, medido nas coordenadas ANTIGAS: quando o principal sai, o
    // Windows move a origem e o mais próximo nas novas seria outro.
    // A tela nunca é transladada: viraria uma tela que nunca existiu e enganaria o "pelo retângulo".
    public static PosicaoDoPersonagem Rebasear(Topologia antiga, Topologia nova, PosicaoDoPersonagem posicao, TamanhoDip tamanho)
    {
        ArgumentNullException.ThrowIfNull(antiga);
        ArgumentNullException.ThrowIfNull(nova);
        ArgumentNullException.ThrowIfNull(posicao);

        if (MonitorCorrespondente(antiga, nova, posicao.ChaveMonitor, posicao.TelaDoMonitor) is { } correspondente)
        {
            PontoPx ancora = NoMonitor(correspondente, posicao.FracaoX, posicao.FracaoY, tamanho).Ancora;
            return posicao with { ChaveMonitor = correspondente.Chave, AncoraAbsoluta = ancora, TelaDoMonitor = correspondente.Tela };
        }

        return TranslacaoDoSobrevivente(antiga, nova, PixelDosPes(posicao.AncoraAbsoluta)) is (int dx, int dy)
            ? posicao with { AncoraAbsoluta = new PontoPx(posicao.AncoraAbsoluta.X + dx, posicao.AncoraAbsoluta.Y + dy) }
            : posicao;
    }

    // Ponto livre (âncora de um arraste) anda junto com o monitor onde estava, porque o Windows leva
    // janela e cursor com o monitor físico quando a origem muda. Sem correspondente, segue o
    // sobrevivente mais próximo, como em Rebasear. Não prende nada: quem solta valida.
    public static PontoPx AcompanharPonto(Topologia antiga, Topologia nova, PontoPx ponto)
    {
        ArgumentNullException.ThrowIfNull(antiga);
        ArgumentNullException.ThrowIfNull(nova);
        PontoPx pes = PixelDosPes(ponto);
        MonitorDoDesktop velho = antiga.MonitorMaisProximo(pes);
        (int dx, int dy)? translacao = MonitorCorrespondente(antiga, nova, velho.Chave, velho.Tela) is { } correspondente
            ? (correspondente.Tela.Esquerda - velho.Tela.Esquerda, correspondente.Tela.Topo - velho.Tela.Topo)
            : TranslacaoDoSobrevivente(antiga, nova, pes);
        return translacao is (int x, int y) ? new PontoPx(ponto.X + x, ponto.Y + y) : ponto;
    }

    // Sobrevivente = monitor novo cuja chave já existia. Distância medida nas coordenadas antigas;
    // empate: principal, depois a ordem nova. Nulo se ninguém sobreviveu.
    private static (int Dx, int Dy)? TranslacaoDoSobrevivente(Topologia antiga, Topologia nova, PontoPx pes)
    {
        MonitorDoDesktop? sobrevivente = null;
        MonitorDoDesktop? antes = null;
        long melhor = long.MaxValue;
        foreach (MonitorDoDesktop m in nova.Monitores)
        {
            if (antiga.PorChave(m.Chave) is not { } velho) continue;
            long d = velho.Tela.DistanciaAoQuadrado(pes);
            if (d < melhor || (d == melhor && m.Principal && !sobrevivente!.Principal))
            {
                (sobrevivente, antes, melhor) = (m, velho, d);
            }
        }
        return sobrevivente is null || antes is null ? null : (sobrevivente.Tela.Esquerda - antes.Tela.Esquerda, sobrevivente.Tela.Topo - antes.Tela.Topo);
    }

    // NaN vira 0,5; o resto (inclusive ±∞) é preso em [0, 1]. O settings.json usa a mesma regra.
    internal static double SanearFracao(double fracao) => double.IsNaN(fracao) ? 0.5 : Math.Clamp(fracao, 0.0, 1.0);
}
