namespace Buzzy.Visual.Pixel;

// Grade de índices da Paleta, (0,0) no canto de cima à esquerda. Escrever fora é ignorado.
public sealed class Tela
{
    private readonly Cor[] _pixels;

    public Tela(int largura, int altura)
    {
        if (largura <= 0 || altura <= 0) throw new ArgumentOutOfRangeException(nameof(largura), "Tamanho inválido.");
        Largura = largura;
        Altura = altura;
        _pixels = new Cor[largura * altura];
    }

    public int Largura { get; }

    public int Altura { get; }

    public Cor this[int x, int y]
    {
        get => Dentro(x, y) ? _pixels[y * Largura + x] : Cor.Nada;
        set
        {
            if (Dentro(x, y)) _pixels[y * Largura + x] = value;
        }
    }

    public bool Dentro(int x, int y) => x >= 0 && y >= 0 && x < Largura && y < Altura;

    public bool Opaco(int x, int y) => this[x, y] != Cor.Nada;

// Luz de cima e da esquerda: sombra embaixo e à direita, realce opcional no lado oposto. Onde
// encosta em algo já desenhado, linhaInterna separa as duas partes.
    public void Pintar(Mascara m, Cor cor, Cor sombra, Cor? realce = null, Cor? linhaInterna = null, int larguraDaSombra = 1)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (linhaInterna is { } linha)
        {
            var bordas = new List<(int X, int Y)>();
            for (int y = 0; y < Altura; y++)
                for (int x = 0; x < Largura; x++)
                    if (!m[x, y] && Opaco(x, y) && (m[x - 1, y] || m[x + 1, y] || m[x, y - 1] || m[x, y + 1]))
                        bordas.Add((x, y));
            foreach ((int x, int y) in bordas) this[x, y] = linha;
        }

        for (int y = 0; y < Altura; y++)
        {
            for (int x = 0; x < Largura; x++)
            {
                if (!m[x, y]) continue;
                bool naSombra = !m[x + 1, y + 1] || !m[x, y + 1] && !m[x + 1, y];
                for (int k = 2; !naSombra && k <= larguraDaSombra; k++) naSombra = !m[x + k, y + k];
                bool noRealce = realce is not null && !m[x - 1, y - 1] && !naSombra;
                this[x, y] = naSombra ? sombra : noRealce ? realce!.Value : cor;
            }
        }
    }

    // Cor chapada, sem sombra nem linhas.
    public void Preencher(Mascara m, Cor cor)
    {
        ArgumentNullException.ThrowIfNull(m);
        for (int y = 0; y < Altura; y++)
            for (int x = 0; x < Largura; x++)
                if (m[x, y]) this[x, y] = cor;
    }

    // 1 px por fora de tudo, vizinhança de 4.
    public void Contornar(Cor contorno)
    {
        var novos = new List<(int X, int Y)>();
        for (int y = 0; y < Altura; y++)
            for (int x = 0; x < Largura; x++)
                if (!Opaco(x, y) && (Opaco(x - 1, y) || Opaco(x + 1, y) || Opaco(x, y - 1) || Opaco(x, y + 1)))
                    novos.Add((x, y));
        foreach ((int x, int y) in novos) this[x, y] = contorno;
    }

    // (x, y) = canto de cima à esquerda. linhaInterna funciona como em Pintar.
    public void Carimbar(Carimbo carimbo, int x, int y, bool espelhar = false, Cor? linhaInterna = null)
    {
        ArgumentNullException.ThrowIfNull(carimbo);
        if (linhaInterna is { } linha)
        {
            bool NoCarimbo(int px, int py) => carimbo[espelhar ? carimbo.Largura - 1 - (px - x) : px - x, py - y] is not null;
            var bordas = new List<(int X, int Y)>();
            for (int py = y - 1; py <= y + carimbo.Altura; py++)
                for (int px = x - 1; px <= x + carimbo.Largura; px++)
                    if (!NoCarimbo(px, py) && Opaco(px, py) && (NoCarimbo(px - 1, py) || NoCarimbo(px + 1, py) || NoCarimbo(px, py - 1) || NoCarimbo(px, py + 1)))
                        bordas.Add((px, py));
            foreach ((int px, int py) in bordas) this[px, py] = linha;
        }
        for (int cy = 0; cy < carimbo.Altura; cy++)
        {
            for (int cx = 0; cx < carimbo.Largura; cx++)
            {
                Cor? c = carimbo[espelhar ? carimbo.Largura - 1 - cx : cx, cy];
                if (c is { } cor) this[x + cx, y + cy] = cor;
            }
        }
    }

// O que cai fora fica transparente. Mesmo recorte usado em expressoes.png e nos rostos do menu.
    public Tela Recortada(int x, int y, int largura, int altura)
    {
        var r = new Tela(largura, altura);
        for (int j = 0; j < altura; j++)
            for (int i = 0; i < largura; i++)
                r[i, j] = this[x + i, y + j];
        return r;
    }

    public Tela Espelhada()
    {
        var t = new Tela(Largura, Altura);
        for (int y = 0; y < Altura; y++)
            for (int x = 0; x < Largura; x++)
                t[Largura - 1 - x, y] = this[x, y];
        return t;
    }

    // 90° sem perda. Horário: linha de baixo vira a coluna da esquerda (esconderijo na lateral
    // esquerda); anti-horário, a da direita.
    public Tela Girada(bool horario)
    {
        if (Largura != Altura) throw new InvalidOperationException("Só telas quadradas giram sem mudar de tamanho.");
        int n = Largura;
        var t = new Tela(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t[x, y] = horario ? this[y, n - 1 - x] : this[n - 1 - y, x];
        return t;
    }

    // Px livres até as bordas de cima e dos lados.
    public const int MargemDaDeformacao = 3;

    // Esticar/achatar (toon force) por vizinho mais próximo em volta do centro da base, que não sai
    // do lugar (pés no chão). A escala é reduzida o quanto precisar pra manter a margem sem cortar
    // o contorno. Não cria cor nova: pixels só se repetem ou somem.
    public Tela Deformada(double escalaX, double escalaY)
    {
        if (!(escalaX > 0) || !(escalaY > 0) || double.IsInfinity(escalaX) || double.IsInfinity(escalaY))
            throw new ArgumentOutOfRangeException(nameof(escalaX), "As escalas precisam ser números positivos.");
        var nova = new Tela(Largura, Altura);
        if (Limites() is not { } l) return nova;

        double centro = (l.Esquerda + l.Direita) / 2.0;
        double baseY = l.Base;
        const int m = MargemDaDeformacao;
        double cabeX = Math.Min((centro - m) / (centro - l.Esquerda), (Largura - m - centro) / (l.Direita - centro));
        double cabeY = (baseY - m) / (baseY - l.Topo);
        // Nunca encolhe por falta de espaço: se já passa da margem, fica como está.
        escalaX = Math.Min(escalaX, Math.Max(1, cabeX));
        escalaY = Math.Min(escalaY, Math.Max(1, cabeY));
        for (int y = 0; y < Altura; y++)
        {
            int origemY = (int)Math.Floor(baseY - (baseY - (y + 0.5)) / escalaY);
            for (int x = 0; x < Largura; x++)
                nova[x, y] = this[(int)Math.Floor(centro + (x + 0.5 - centro) / escalaX), origemY];
        }
        return nova;
    }

    public uint[] ParaArgb()
    {
        var saida = new uint[_pixels.Length];
        for (int i = 0; i < _pixels.Length; i++) saida[i] = Paleta.Argb(_pixels[i]);
        return saida;
    }

    // Caixa dos pixels opacos, direita e base exclusivas; nulo se vazia.
    public (int Esquerda, int Topo, int Direita, int Base)? Limites()
    {
        int e = int.MaxValue, t = int.MaxValue, d = int.MinValue, b = int.MinValue;
        for (int y = 0; y < Altura; y++)
        {
            for (int x = 0; x < Largura; x++)
            {
                if (!Opaco(x, y)) continue;
                e = Math.Min(e, x);
                t = Math.Min(t, y);
                d = Math.Max(d, x + 1);
                b = Math.Max(b, y + 1);
            }
        }
        return e == int.MaxValue ? null : (e, t, d, b);
    }
}

// Pixel entra na forma se o centro (x + 0,5; y + 0,5) está dentro: serrilhado, sem meio-tom.
public sealed class Mascara
{
    private readonly bool[] _m;

    public Mascara(int largura, int altura)
    {
        Largura = largura;
        Altura = altura;
        _m = new bool[largura * altura];
    }

    public int Largura { get; }

    public int Altura { get; }

    public bool this[int x, int y]
    {
        get => x >= 0 && y >= 0 && x < Largura && y < Altura && _m[y * Largura + x];
        set
        {
            if (x >= 0 && y >= 0 && x < Largura && y < Altura) _m[y * Largura + x] = value;
        }
    }

    public bool Vazia => !_m.Any(v => v);

    public Mascara Circulo(double cx, double cy, double r) => Elipse(cx, cy, r, r);

    // graus no sentido horário da tela.
    public Mascara Elipse(double cx, double cy, double rx, double ry, double graus = 0)
    {
        double a = graus * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double alcance = Math.Max(rx, ry) + 1;
        Varrer(cx - alcance, cy - alcance, cx + alcance, cy + alcance, (px, py) =>
        {
            double dx = px - cx, dy = py - cy;
            double u = dx * cos + dy * sin, v = -dx * sin + dy * cos;
            return u * u / (rx * rx) + v * v / (ry * ry) <= 1.0;
        });
        return this;
    }

    // Pontas redondas; raio vai de r1 a r2 linearmente.
    public Mascara Capsula(double x1, double y1, double x2, double y2, double r1, double r2)
    {
        double r = Math.Max(r1, r2) + 1;
        double vx = x2 - x1, vy = y2 - y1, comp2 = vx * vx + vy * vy;
        Varrer(Math.Min(x1, x2) - r, Math.Min(y1, y2) - r, Math.Max(x1, x2) + r, Math.Max(y1, y2) + r, (px, py) =>
        {
            double t = comp2 == 0 ? 0 : Math.Clamp(((px - x1) * vx + (py - y1) * vy) / comp2, 0, 1);
            double qx = x1 + t * vx - px, qy = y1 + t * vy - py;
            double raio = r1 + (r2 - r1) * t;
            return qx * qx + qy * qy <= raio * raio;
        });
        return this;
    }

    // Regra par-ímpar.
    public Mascara Poligono(params (double X, double Y)[] pontos)
    {
        ArgumentNullException.ThrowIfNull(pontos);
        if (pontos.Length < 3) return this;
        double minX = pontos.Min(p => p.X), maxX = pontos.Max(p => p.X), minY = pontos.Min(p => p.Y), maxY = pontos.Max(p => p.Y);
        Varrer(minX, minY, maxX, maxY, (px, py) =>
        {
            bool dentro = false;
            for (int i = 0, j = pontos.Length - 1; i < pontos.Length; j = i++)
            {
                (double xi, double yi) = pontos[i];
                (double xj, double yj) = pontos[j];
                if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) dentro = !dentro;
            }
            return dentro;
        });
        return this;
    }

    // Ao longo de Béziers cúbicas encadeadas, só do trecho t0..t1, raio de r1 a r2.
    public Mascara Traco(IReadOnlyList<(double X, double Y)> caminho, double r1, double r2, double t0 = 0, double t1 = 1)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        List<(double X, double Y)> pontos = Curvas.Amostrar(caminho);
        int n = pontos.Count;
        int inicio = (int)Math.Floor(t0 * (n - 1)), fim = (int)Math.Ceiling(t1 * (n - 1));
        for (int i = inicio; i < fim; i++)
        {
            double f = (double)i / (n - 1), g = (double)(i + 1) / (n - 1);
            Capsula(pontos[i].X, pontos[i].Y, pontos[i + 1].X, pontos[i + 1].Y, r1 + (r2 - r1) * f, r1 + (r2 - r1) * g);
        }
        return this;
    }

    public Mascara Unir(Mascara outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        for (int i = 0; i < _m.Length; i++) _m[i] |= outra._m[i];
        return this;
    }

    public Mascara Subtrair(Mascara outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        for (int i = 0; i < _m.Length; i++) _m[i] &= !outra._m[i];
        return this;
    }

    public Mascara Intersectar(Mascara outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        for (int i = 0; i < _m.Length; i++) _m[i] &= outra._m[i];
        return this;
    }

    public Mascara Ligar(int x, int y)
    {
        this[x, y] = true;
        return this;
    }

    public Mascara Desligar(int x, int y)
    {
        this[x, y] = false;
        return this;
    }

    private void Varrer(double x0, double y0, double x1, double y1, Func<double, double, bool> dentro)
    {
        int xa = Math.Max(0, (int)Math.Floor(x0) - 1), xb = Math.Min(Largura - 1, (int)Math.Ceiling(x1) + 1);
        int ya = Math.Max(0, (int)Math.Floor(y0) - 1), yb = Math.Min(Altura - 1, (int)Math.Ceiling(y1) + 1);
        for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
                if (dentro(x + 0.5, y + 0.5)) _m[y * Largura + x] = true;
    }
}

// Béziers cúbicas encadeadas: P0, C1, C2, P1, C1, C2, P2...
public static class Curvas
{
    public static List<(double X, double Y)> Amostrar(IReadOnlyList<(double X, double Y)> caminho, int passosPorSegmento = 24)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        if (caminho.Count < 4 || (caminho.Count - 1) % 3 != 0)
            throw new ArgumentException("O caminho precisa de 1 + 3n pontos (Bézier cúbicas encadeadas).", nameof(caminho));
        var saida = new List<(double X, double Y)> { caminho[0] };
        for (int s = 0; s + 3 < caminho.Count; s += 3)
        {
            (double X, double Y) p0 = caminho[s], c1 = caminho[s + 1], c2 = caminho[s + 2], p1 = caminho[s + 3];
            for (int i = 1; i <= passosPorSegmento; i++)
            {
                double t = (double)i / passosPorSegmento, u = 1 - t;
                double x = u * u * u * p0.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * p1.X;
                double y = u * u * u * p0.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * p1.Y;
                saida.Add((x, y));
            }
        }
        return saida;
    }
}
