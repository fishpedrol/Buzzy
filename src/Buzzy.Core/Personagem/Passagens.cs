namespace Buzzy.Core.Personagem;

// Trecho [Topo, Base) da lateral x = Borda de um monitor que encosta na área
// útil do vizinho. Lado é o do monitor de origem: -1 esquerda, +1 direita.
public readonly record struct Porta(string ChaveVizinho, int Lado, int Borda, int Topo, int Base);

public enum TipoDeTravessia
{
    // Pela porta plana.
    Andando,

    // Salto de degrau: arco balístico fechado do chão da origem ao do vizinho.
    Salto,
}

// Nenhum = salto de degrau normal da travessia.
public enum PuloDaTelaCheia
{
    Nenhum,

    // Do monitor em tela cheia pro cipó do monitor livre.
    Ida,

    // No fim da tela cheia, de volta pra posição de antes.
    Volta,
}

// Travessia em curso, em px físicos. A âncora troca de monitor quando,
// arredondada, passa da borda (Passagens.PassouDaBorda).
// No salto o arco é fechado: partida (X0, Y0), velocidades e gravidade G em
// px/s na escala da origem, congelada no voo, e a duração em passos; o último
// passo cai exatamente no pouso (XDestino, YDestino). No pulo da tela cheia
// não há borda: o monitor de cada passo é o da âncora e a chegada é uma acomodação.
public sealed record Travessia(
    TipoDeTravessia Tipo, string ChaveOrigem, string ChaveDestino, int Lado, int Borda,
    double XDestino = 0, double YDestino = 0, double X0 = 0, double Y0 = 0, double VX = 0, double VY0 = 0, double G = 0,
    int PassosTotais = 0, int Passo = 0, PuloDaTelaCheia Pulo = PuloDaTelaCheia.Nenhum);

// Geometria das passagens entre monitores, pura e sem estado. A porta nasce
// da adjacência exata das ÁREAS ÚTEIS, não das telas: com uma barra de
// tarefas vertical entre dois monitores as áreas não se tocam e a lateral é
// parede. Toda lateral continua escalável; a porta só permite atravessar.
public static class Passagens
{
    // Uma porta por vizinho cuja área útil começa exatamente na borda desta,
    // na faixa vertical em comum. Ordem: Topo, depois chave (ordinal). Quina
    // ou vão não dão porta. Vizinho fechado (em tela cheia, com o modo
    // ligado) também não: aquela lateral vira parede.
    public static IReadOnlyList<Porta> Portas(Topologia topologia, MonitorDoDesktop monitor, int lado, Func<string, bool>? fechado = null)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(monitor);
        if (lado is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(lado), lado, "O lado é −1 (esquerda) ou +1 (direita).");
        RetanguloPx a = monitor.AreaUtil;
        int borda = lado > 0 ? a.Direita : a.Esquerda;
        var portas = new List<Porta>();
        foreach (MonitorDoDesktop outro in topologia.Monitores)
        {
            if (outro.Chave == monitor.Chave || fechado?.Invoke(outro.Chave) == true) continue;
            RetanguloPx n = outro.AreaUtil;
            if ((lado > 0 ? n.Esquerda : n.Direita) != borda) continue;
            int topo = Math.Max(a.Topo, n.Topo), baixo = Math.Min(a.Base, n.Base);
            if (topo < baixo) portas.Add(new Porta(outro.Chave, lado, borda, topo, baixo));
        }
        portas.Sort((p, q) => p.Topo != q.Topo ? p.Topo.CompareTo(q.Topo) : string.CompareOrdinal(p.ChaveVizinho, q.ChaveVizinho));
        return portas;
    }

    // Altura da área útil menos as portas, de cima pra baixo.
    public static IReadOnlyList<(int Topo, int Base)> TrechosDeParede(Topologia topologia, MonitorDoDesktop monitor, int lado)
    {
        var trechos = new List<(int Topo, int Base)>();
        int y = monitor.AreaUtil.Topo;
        foreach (Porta p in Portas(topologia, monitor, lado))
        {
            if (p.Topo > y) trechos.Add((y, p.Topo));
            y = Math.Max(y, p.Base);
        }
        if (y < monitor.AreaUtil.Base) trechos.Add((y, monitor.AreaUtil.Base));
        return trechos;
    }

    // Porta plana: vizinho com o mesmo chão, porta indo até o chão com a
    // altura do maior dos dois sprites (o tamanho muda na borda pelo DPI) e
    // sprite cabendo no vizinho. No máximo uma, a que contém o chão.
    public static Porta? PortaPlana(Topologia topologia, MonitorDoDesktop monitor, int lado, TamanhoDip sprite, Func<string, bool>? fechado = null)
    {
        int chao = monitor.AreaUtil.Base;
        foreach (Porta p in Portas(topologia, monitor, lado, fechado))
        {
            if (p.Base != chao || topologia.PorChave(p.ChaveVizinho) is not { } vizinho || vizinho.AreaUtil.Base != chao) continue;
            TamanhoPx aqui = sprite.ParaPixels(monitor.Dpi), la = sprite.ParaPixels(vizinho.Dpi);
            if (p.Topo > chao - Math.Max(aqui.Altura, la.Altura)) continue;
            if (la.Largura > vizinho.AreaUtil.Largura || la.Altura > vizinho.AreaUtil.Altura) continue;
            return p;
        }
        return null;
    }

    // Salto de degrau: vizinho com porta e chão a Δ ≠ 0, em DIP pela menor
    // escala dos dois (igual nos dois sentidos), dentro de DescidaMaxima /
    // SubidaMaxima, e o sprite cabendo. Preferência: menor |Δ|, depois menor Δ
    // (subir primeiro), depois a chave.
    // Busca o arco em ordem: tempo de voo (TemposDoSalto) × recuo da partida
    // (0, ¼, ½, 1, 2 larguras, até recuoMaximo px) × pouso a partir da lateral
    // (0, ¼, ½, 1, 1½, 2 larguras). Vence o primeiro com o sprite sempre na
    // união das áreas úteis. O recuo é o que deixa subir: precisa ganhar altura
    // antes da frente passar da borda. Determinístico, sem sorteio.
    public static Travessia? SaltoDeDegrau(Topologia topologia, MonitorDoDesktop monitor, int lado, TamanhoDip sprite, ParametrosDeMovimento fisica, int passosPorSegundo, int recuoMaximo = 0, Func<string, bool>? fechado = null)
    {
        ArgumentNullException.ThrowIfNull(fisica);
        var vizinhos = new List<(Porta Porta, MonitorDoDesktop Vizinho, int Delta)>();
        foreach (Porta p in Portas(topologia, monitor, lado, fechado))
        {
            if (topologia.PorChave(p.ChaveVizinho) is not { } vizinho) continue;
            int delta = vizinho.AreaUtil.Base - monitor.AreaUtil.Base;
            if (delta == 0) continue;
            double dip = delta / (Math.Min(monitor.Dpi, vizinho.Dpi) / 96.0);
            if (dip < -fisica.SubidaMaxima || dip > fisica.DescidaMaxima) continue;
            vizinhos.Add((p, vizinho, delta));
        }
        vizinhos.Sort((a, b) => Math.Abs(a.Delta) != Math.Abs(b.Delta) ? Math.Abs(a.Delta).CompareTo(Math.Abs(b.Delta))
            : a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) : string.CompareOrdinal(a.Vizinho.Chave, b.Vizinho.Chave));

        foreach ((Porta porta, MonitorDoDesktop vizinho, _) in vizinhos)
        {
            TamanhoPx aqui = sprite.ParaPixels(monitor.Dpi), la = sprite.ParaPixels(vizinho.Dpi);
            if (la.Largura > vizinho.AreaUtil.Largura || la.Altura > vizinho.AreaUtil.Altura) continue;
            Superficies daOrigem = Superficies.Do(topologia, monitor, aqui), doDestino = Superficies.Do(topologia, vizinho, la);
            double limite = lado > 0 ? daOrigem.Direita : daOrigem.Esquerda, y0 = daOrigem.Chao, y1 = doDestino.Chao;
            double g = fisica.Gravidade * monitor.Dpi / 96.0;
            int[] pousos = [0, la.Largura / 4, la.Largura / 2, la.Largura, la.Largura * 3 / 2, la.Largura * 2];
            int[] recuos = [.. new[] { 0, aqui.Largura / 4, aqui.Largura / 2, aqui.Largura, aqui.Largura * 2 }.Where(r => r <= recuoMaximo && r <= daOrigem.Direita - daOrigem.Esquerda)];
            foreach (double tempo in fisica.TemposDoSalto)
            foreach (int recuo in recuos)
            {
                double x0 = limite - lado * recuo;
                int passos = (int)Math.Ceiling(tempo * passosPorSegundo);
                foreach (int dentro in pousos)
                {
                    double x1 = lado > 0 ? doDestino.Esquerda + dentro : doDestino.Direita - dentro;
                    if (x1 < doDestino.Esquerda || x1 > doDestino.Direita) continue;
                    var salto = new Travessia(TipoDeTravessia.Salto, monitor.Chave, vizinho.Chave, lado, porta.Borda,
                        x1, y1, x0, y0, (x1 - x0) / tempo, (y1 - y0 - 0.5 * g * tempo * tempo) / tempo, g, passos);
                    if (ArcoNaUniao(topologia, salto, monitor, vizinho, sprite, passosPorSegundo)) return salto;
                }
            }
        }
        return null;
    }

    // Transbordo: escalando a lateral do monitor mais baixo, com os pés na
    // altura do chão de um vizinho mais alto (topo da parede abaixo da porta),
    // um arco curto leva até o chão do vizinho. Mesmo solucionador do salto,
    // sem recuo. Sem limite de altura: é a subida que o salto não alcança.
    public static Travessia? Transbordo(Topologia topologia, MonitorDoDesktop monitor, int lado, int alturaDoChao, TamanhoDip sprite, ParametrosDeMovimento fisica, int passosPorSegundo, Func<string, bool>? fechado = null)
    {
        ArgumentNullException.ThrowIfNull(fisica);
        if (alturaDoChao >= monitor.AreaUtil.Base) return null;
        TamanhoPx aqui = sprite.ParaPixels(monitor.Dpi);
        if (alturaDoChao - aqui.Altura < monitor.AreaUtil.Topo) return null;
        foreach (Porta porta in Portas(topologia, monitor, lado, fechado))
        {
            if (topologia.PorChave(porta.ChaveVizinho) is not { } vizinho || vizinho.AreaUtil.Base != alturaDoChao) continue;
            TamanhoPx la = sprite.ParaPixels(vizinho.Dpi);
            if (la.Largura > vizinho.AreaUtil.Largura || la.Altura > vizinho.AreaUtil.Altura || porta.Topo > alturaDoChao - Math.Max(aqui.Altura, la.Altura)) continue;
            Superficies daOrigem = Superficies.Do(topologia, monitor, aqui), doDestino = Superficies.Do(topologia, vizinho, la);
            double x0 = lado > 0 ? daOrigem.Direita : daOrigem.Esquerda;
            if (Arco(topologia, monitor, vizinho, porta, lado, x0, alturaDoChao, doDestino, sprite, fisica, passosPorSegundo) is { } arco) return arco;
        }
        return null;
    }

    // Primeiro arco (tempo de voo × pouso) com o sprite sempre na união das áreas úteis.
    private static Travessia? Arco(Topologia topologia, MonitorDoDesktop origem, MonitorDoDesktop destino, Porta porta, int lado, double x0, double y0,
        Superficies doDestino, TamanhoDip sprite, ParametrosDeMovimento fisica, int passosPorSegundo)
    {
        int largura = sprite.ParaPixels(destino.Dpi).Largura;
        int[] pousos = [0, largura / 4, largura / 2, largura, largura * 3 / 2, largura * 2];
        double g = fisica.Gravidade * origem.Dpi / 96.0, y1 = doDestino.Chao;
        foreach (double tempo in fisica.TemposDoSalto)
        {
            int passos = (int)Math.Ceiling(tempo * passosPorSegundo);
            foreach (int dentro in pousos)
            {
                double x1 = lado > 0 ? doDestino.Esquerda + dentro : doDestino.Direita - dentro;
                if (x1 < doDestino.Esquerda || x1 > doDestino.Direita) continue;
                var salto = new Travessia(TipoDeTravessia.Salto, origem.Chave, destino.Chave, lado, porta.Borda,
                    x1, y1, x0, y0, (x1 - x0) / tempo, (y1 - y0 - 0.5 * g * tempo * tempo) / tempo, g, passos);
                if (ArcoNaUniao(topologia, salto, origem, destino, sprite, passosPorSegundo)) return salto;
            }
        }
        return null;
    }

    // Arco de gravidade constante de (x0, y0) a (x1, y1). Duração pela
    // distância e VelocidadeDoPuloDaTelaCheia, presa entre mínimo e máximo e
    // arredondada pra passos inteiros (o último cai exatamente na chegada).
    // H (quanto sobe acima da reta) é tentado nesta ordem, vence o primeiro
    // com o sprite sempre na união das áreas úteis:
    //   1. max(¼ do desnível, altura): com ¼ do desnível o topo do arco é a
    //      ponta mais alta (chega no cipó parando de subir, ou sai sem subir);
    //      com a altura, um pulo de verdade entre pontos de alturas parecidas;
    //   2. ¼ do desnível, se for pelo menos metade da altura;
    //   3. H negativo: balanço de cipó a cipó, desce abaixo da reta e sobe.
    // Altura = AlturaDoPuloDaTelaCheia (ou AlturaDoPulinho), na escala da
    // origem, até metade da distância. Determinístico, sem sorteio.
    public static Travessia? PlanejarPuloDaTelaCheia(Topologia topologia, MonitorDoDesktop origem, double x0, double y0, MonitorDoDesktop destino, double x1, double y1,
        TamanhoDip sprite, ParametrosDeMovimento fisica, int passosPorSegundo, PuloDaTelaCheia pulo, bool pulinho = false)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(destino);
        ArgumentNullException.ThrowIfNull(fisica);
        double escala = origem.Dpi / 96.0;
        double dx = x1 - x0, dy = y1 - y0, distancia = Math.Sqrt(dx * dx + dy * dy);
        double tempo = Math.Clamp(distancia / (fisica.VelocidadeDoPuloDaTelaCheia * escala), pulinho ? fisica.TempoMinimoDoPulinho : fisica.TempoMinimoDoPuloDaTelaCheia, fisica.TempoMaximoDoPuloDaTelaCheia);
        int passos = Math.Max(1, (int)Math.Ceiling(tempo * passosPorSegundo));
        tempo = (double)passos / passosPorSegundo;

        double altura = Math.Min((pulinho ? fisica.AlturaDoPulinho : fisica.AlturaDoPuloDaTelaCheia) * escala, distancia / 2), quarto = Math.Abs(dy) / 4;
        var alturas = new List<double> { Math.Max(quarto, altura) };
        if (quarto >= altura / 2 && quarto < altura) alturas.Add(quarto);
        if (altura > 0) alturas.Add(-altura);
        foreach (double h in alturas)
        {
            // y(s) = y0 + dy·s − 4H·s(1 − s), com s = t/T: a gravidade é 8H/T², e a velocidade vertical de partida, (dy − 4H)/T.
            var salto = new Travessia(TipoDeTravessia.Salto, origem.Chave, destino.Chave, dx >= 0 ? 1 : -1, 0,
                x1, y1, x0, y0, dx / tempo, (dy - 4 * h) / tempo, 8 * h / (tempo * tempo), passos, 0, pulo);
            if (ArcoNaUniao(topologia, salto, origem, destino, sprite, passosPorSegundo)) return salto;
        }
        return null;
    }

    // Pulo da tela cheia: o monitor do pixel dos pés (ou o mais próximo).
    // Travessia: origem até passar da borda, depois destino.
    public static MonitorDoDesktop MonitorNoSalto(Topologia topologia, Travessia salto, MonitorDoDesktop origem, MonitorDoDesktop destino, PontoPx ancora)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(salto);
        if (salto.Pulo != PuloDaTelaCheia.Nenhum) return topologia.MonitorMaisProximo(Posicionador.PixelDosPes(ancora));
        return PassouDaBorda(salto, ancora) ? destino : origem;
    }

    // Analítica, passo de 1 a PassosTotais; o último devolve o pouso exato.
    public static (double X, double Y) PosicaoNoSalto(Travessia salto, int passo, int passosPorSegundo)
    {
        ArgumentNullException.ThrowIfNull(salto);
        if (passo >= salto.PassosTotais) return (salto.XDestino, salto.YDestino);
        double t = (double)passo / passosPorSegundo;
        return (salto.X0 + salto.VX * t, salto.Y0 + salto.VY0 * t + 0.5 * salto.G * t * t);
    }

    // Em cada passo, o sprite usa o tamanho do monitor onde a âncora está.
    private static bool ArcoNaUniao(Topologia topologia, Travessia salto, MonitorDoDesktop origem, MonitorDoDesktop destino, TamanhoDip sprite, int passosPorSegundo)
    {
        for (int k = 1; k <= salto.PassosTotais; k++)
        {
            (double x, double y) = PosicaoNoSalto(salto, k, passosPorSegundo);
            var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
            MonitorDoDesktop m = MonitorNoSalto(topologia, salto, origem, destino, ancora);
            if (!NaUniaoDasAreasUteis(topologia, Posicionador.RetanguloDoSprite(ancora, sprite.ParaPixels(m.Dpi)))) return false;
        }
        return true;
    }

    // Ex.: borda em x = 0 e destino à esquerda: x = -1 já está no destino; x = 0, ainda na origem.
    public static bool PassouDaBorda(Travessia travessia, PontoPx ancora)
    {
        ArgumentNullException.ThrowIfNull(travessia);
        return travessia.Lado > 0 ? ancora.X >= travessia.Borda : ancora.X < travessia.Borda;
    }

    // Soma das interseções com cada área útil == área do retângulo. Exato
    // porque áreas úteis não se sobrepõem. Retângulo vazio conta como dentro.
    public static bool NaUniaoDasAreasUteis(Topologia topologia, RetanguloPx r)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        if (r.Largura <= 0 || r.Altura <= 0) return true;
        long soma = 0;
        foreach (MonitorDoDesktop m in topologia.Monitores)
        {
            RetanguloPx u = m.AreaUtil;
            long largura = Math.Min(r.Direita, u.Direita) - (long)Math.Max(r.Esquerda, u.Esquerda);
            long altura = Math.Min(r.Base, u.Base) - (long)Math.Max(r.Topo, u.Topo);
            if (largura > 0 && altura > 0) soma += largura * altura;
        }
        return soma == (long)r.Largura * r.Altura;
    }
}
