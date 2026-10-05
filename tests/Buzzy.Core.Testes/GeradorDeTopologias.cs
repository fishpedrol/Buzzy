namespace Buzzy.Core.Testes;

/// <summary>
/// Gera topologias válidas aleatórias para os testes de propriedade
/// (<see cref="PropriedadesTestes"/>): 1 a 4 monitores sem sobreposição, um principal com
/// origem (0,0), DPIs entre 96 e 288 e áreas úteis dentro das telas. Cada monitor novo fica
/// encostado num já posto ou separado dele por um vão, alinhado ou desalinhado, em qualquer
/// lado; a ordem da lista é embaralhada. Tudo sai de um único <see cref="Random"/> com
/// semente, para o caso poder ser repetido.
/// </summary>
internal sealed class GeradorDeTopologias(Random aleatorio)
{
    public const int MaximoDeMonitores = 4;

    private static readonly TamanhoPx[] ResolucoesComuns =
    [
        new(1920, 1080), new(2560, 1440), new(3840, 2160), new(1366, 768), new(1280, 1024), new(1080, 1920),
        new(1440, 2560), new(3440, 1440), new(1024, 768), new(800, 600), new(2880, 1800),
    ];

    private static readonly int[] DpisComuns = [96, 120, 144, 168, 192, 240, 288];

    /// <summary>Topologia nova com 1 a <see cref="MaximoDeMonitores"/> monitores.</summary>
    public Topologia NovaTopologia()
    {
        int quantidade = aleatorio.Next(1, MaximoDeMonitores + 1);
        TamanhoPx primeira = Resolucao();
        var telas = new List<RetanguloPx> { new(0, 0, primeira.Largura, primeira.Altura) };
        for (int tentativa = 0; telas.Count < quantidade && tentativa < 100; tentativa++)
        {
            if (TelaVizinha(telas) is { } tela)
                telas.Add(tela);
        }

        // Números de DISPLAY1 a DISPLAY9 embaralhados: a ordem das chaves não segue a da lista.
        List<int> numeros = Embaralhar(Enumerable.Range(1, 9).ToList());
        var monitores = new List<MonitorDoDesktop>();
        for (int i = 0; i < telas.Count; i++)
        {
            int dpi = Dpi();
            monitores.Add(new MonitorDoDesktop(Chave(numeros[i]), telas[i], AreaUtil(telas[i], dpi), dpi, Principal: i == 0));
        }
        return new Topologia(Embaralhar(monitores));
    }

    /// <summary>
    /// A topologia depois de uma a três mudanças do tipo que o Windows publica: resolução,
    /// escala, barra de tarefas, monitor desconectado ou conectado, troca do principal (com a
    /// origem passando para ele) ou uma configuração inteiramente nova.
    /// </summary>
    public Topologia Mudar(Topologia topologia)
    {
        List<MonitorDoDesktop> lista = [.. topologia.Monitores];
        int mudancas = aleatorio.Next(1, 4);
        for (int i = 0; i < mudancas; i++)
        {
            int k = aleatorio.Next(lista.Count);
            MonitorDoDesktop m = lista[k];
            switch (aleatorio.Next(7))
            {
                case 0:
                {
                    // Resolução nova, mantendo o canto superior esquerdo (e a origem do principal).
                    var tela = RetanguloPx.DePosicaoETamanho(new PontoPx(m.Tela.Esquerda, m.Tela.Topo), Resolucao());
                    if (lista.Where((_, j) => j != k).All(o => !o.Tela.Intersecta(tela)))
                        lista[k] = m with { Tela = tela, AreaUtil = AreaUtil(tela, m.Dpi) };
                    break;
                }
                case 1:
                {
                    // Escala nova; a barra de tarefas acompanha.
                    int dpi = Dpi();
                    lista[k] = m with { Dpi = dpi, AreaUtil = AreaUtil(m.Tela, dpi) };
                    break;
                }
                case 2:
                    // Barra de tarefas em outro lugar ou com outro tamanho.
                    lista[k] = m with { AreaUtil = AreaUtil(m.Tela, m.Dpi) };
                    break;
                case 3:
                    // Monitor desconectado; se era o principal, outro assume e a origem vai para ele.
                    if (lista.Count > 1)
                    {
                        lista.RemoveAt(k);
                        if (m.Principal) Rebasear(lista, aleatorio.Next(lista.Count));
                    }
                    break;
                case 4:
                    // Monitor conectado, numa chave livre e em qualquer posição da lista.
                    if (lista.Count < MaximoDeMonitores && TelaVizinha([.. lista.Select(o => o.Tela)]) is { } nova)
                    {
                        List<int> livres = [.. Enumerable.Range(1, 9).Where(n => lista.All(o => o.Chave != Chave(n)))];
                        int dpi = Dpi();
                        var monitor = new MonitorDoDesktop(Chave(livres[aleatorio.Next(livres.Count)]), nova, AreaUtil(nova, dpi), dpi, Principal: false);
                        lista.Insert(aleatorio.Next(lista.Count + 1), monitor);
                    }
                    break;
                case 5:
                    // Outro monitor vira principal.
                    if (lista.Count > 1) Rebasear(lista, aleatorio.Next(lista.Count));
                    break;
                default:
                    // Configuração inteiramente nova; as chaves podem coincidir por acaso.
                    lista = [.. NovaTopologia().Monitores];
                    break;
            }
        }
        return new Topologia(lista);
    }

    /// <summary>
    /// Uma mudança que muitas vezes conserva a geometria do monitor do personagem (Fase 5, DEC-030): troca de principal, com a
    /// origem indo para ele; um monitor conectado; um desconectado (se era o principal, outro assume e a origem vai para ele);
    /// as chaves renomeadas com as mesmas telas, como a reserva <c>gdi:</c> que vira <c>mon:</c>; ou uma mudança qualquer de
    /// <see cref="Mudar"/>. Fica à parte de <see cref="Mudar"/> para não alterar o fluxo aleatório das propriedades que já o usam.
    /// </summary>
    public Topologia MudarComIdentidade(Topologia topologia)
    {
        List<MonitorDoDesktop> lista = [.. topologia.Monitores];
        switch (aleatorio.Next(5))
        {
            case 0 when lista.Count > 1:
                Rebasear(lista, aleatorio.Next(lista.Count));
                break;
            case 1 when lista.Count < MaximoDeMonitores && TelaVizinha([.. lista.Select(o => o.Tela)]) is { } nova:
            {
                List<int> livres = [.. Enumerable.Range(1, 9).Where(n => lista.All(o => o.Chave != Chave(n)))];
                int dpi = Dpi();
                lista.Insert(aleatorio.Next(lista.Count + 1), new MonitorDoDesktop(Chave(livres[aleatorio.Next(livres.Count)]), nova, AreaUtil(nova, dpi), dpi, Principal: false));
                break;
            }
            case 2 when lista.Count > 1:
            {
                int k = aleatorio.Next(lista.Count);
                MonitorDoDesktop saiu = lista[k];
                lista.RemoveAt(k);
                if (saiu.Principal) Rebasear(lista, aleatorio.Next(lista.Count));
                break;
            }
            case 3:
                lista = [.. lista.Select(m => m with { Chave = m.Chave.StartsWith("mon:", StringComparison.Ordinal) ? m.Chave[4..] : "mon:" + m.Chave })];
                break;
            default:
                return Mudar(topologia);
        }
        return new Topologia(lista);
    }

    /// <summary>DPI comum (96 a 288, em passos de 25%) ou qualquer valor inteiro entre 96 e 288.</summary>
    public int Dpi() => aleatorio.Next(3) == 0 ? aleatorio.Next(96, 289) : DpisComuns[aleatorio.Next(DpisComuns.Length)];

    /// <summary>Resolução comum ou qualquer tamanho de 200x200 a 5000x3000.</summary>
    public TamanhoPx Resolucao() => aleatorio.Next(4) == 0
        ? new TamanhoPx(aleatorio.Next(200, 5001), aleatorio.Next(200, 3001))
        : ResolucoesComuns[aleatorio.Next(ResolucoesComuns.Length)];

    /// <summary>
    /// Área útil dentro da tela: sem barra (oculta), com a barra de tarefas em uma das quatro
    /// bordas e, às vezes, com mais uma faixa reservada por outro programa. Cada faixa fica
    /// abaixo de um terço (a barra) ou de um quarto (a outra) do lado, então a área nunca é vazia.
    /// </summary>
    public RetanguloPx AreaUtil(RetanguloPx tela, int dpi)
    {
        int esquerda = tela.Esquerda, topo = tela.Topo, direita = tela.Direita, baseY = tela.Base;

        // A barra do Windows 11 tem 48 DIP; um terço das vezes vale outra espessura qualquer.
        int barra = aleatorio.Next(3) == 0 ? aleatorio.Next(0, 200) : (int)Math.Round(48 * dpi / 96.0);
        switch (aleatorio.Next(10))
        {
            case 0: break;
            case 1: topo += Math.Min(barra, tela.Altura / 3); break;
            case 2: esquerda += Math.Min(barra, tela.Largura / 3); break;
            case 3: direita -= Math.Min(barra, tela.Largura / 3); break;
            default: baseY -= Math.Min(barra, tela.Altura / 3); break;
        }

        if (aleatorio.Next(8) == 0)
        {
            switch (aleatorio.Next(4))
            {
                case 0: topo += aleatorio.Next(tela.Altura / 4); break;
                case 1: esquerda += aleatorio.Next(tela.Largura / 4); break;
                case 2: direita -= aleatorio.Next(tela.Largura / 4); break;
                default: baseY -= aleatorio.Next(tela.Altura / 4); break;
            }
        }
        return new RetanguloPx(esquerda, topo, direita, baseY);
    }

    /// <summary>Tamanho lógico do sprite; uma vez em dez, grande o bastante para costumar não caber.</summary>
    public TamanhoDip Sprite() => aleatorio.Next(10) == 0
        ? new TamanhoDip(aleatorio.Next(1, 3001), aleatorio.Next(1, 3001))
        : new TamanhoDip(aleatorio.Next(1, 401), aleatorio.Next(1, 401));

    /// <summary>Fração de posição: quase sempre em [0, 1], às vezes 0, 1, fora do intervalo, infinita ou NaN.</summary>
    public double Fracao() => aleatorio.Next(20) switch
    {
        0 => double.NaN,
        1 => aleatorio.Next(2) == 0 ? double.PositiveInfinity : double.NegativeInfinity,
        2 => aleatorio.NextDouble() * 4 - 2,
        3 => 0,
        4 => 1,
        _ => aleatorio.NextDouble(),
    };

    /// <summary>
    /// Ponto qualquer: um terço das vezes na borda de um monitor (colunas e linhas limite e
    /// logo fora delas); no resto, em qualquer lugar da caixa que envolve os monitores com
    /// 2000 px de folga, o que inclui vãos e o lado de fora de todos.
    /// </summary>
    public PontoPx Ponto(Topologia topologia)
    {
        if (aleatorio.Next(3) == 0)
        {
            RetanguloPx tela = topologia.Monitores[aleatorio.Next(topologia.Monitores.Count)].Tela;
            int[] xs = [tela.Esquerda - 1, tela.Esquerda, tela.Direita - 1, tela.Direita, aleatorio.Next(tela.Esquerda, tela.Direita)];
            int[] ys = [tela.Topo - 1, tela.Topo, tela.Base - 1, tela.Base, aleatorio.Next(tela.Topo, tela.Base)];
            return new PontoPx(xs[aleatorio.Next(xs.Length)], ys[aleatorio.Next(ys.Length)]);
        }

        int esquerda = topologia.Monitores.Min(m => m.Tela.Esquerda) - 2000;
        int topo = topologia.Monitores.Min(m => m.Tela.Topo) - 2000;
        int direita = topologia.Monitores.Max(m => m.Tela.Direita) + 2000;
        int baseY = topologia.Monitores.Max(m => m.Tela.Base) + 2000;
        return new PontoPx(aleatorio.Next(esquerda, direita), aleatorio.Next(topo, baseY));
    }

    public static string Chave(int numero) => $@"\\.\DISPLAY{numero}";

    /// <summary>
    /// Tela nova encostada num monitor já posto, ou separada dele por um vão, em qualquer lado;
    /// nulo se ela sobrepuser algum monitor.
    /// </summary>
    private RetanguloPx? TelaVizinha(IReadOnlyList<RetanguloPx> telas)
    {
        RetanguloPx vizinho = telas[aleatorio.Next(telas.Count)];
        TamanhoPx r = Resolucao();
        int vao = aleatorio.Next(4) == 0 ? aleatorio.Next(1, 400) : 0;
        (int x, int y) = aleatorio.Next(4) switch
        {
            0 => (vizinho.Direita + vao, vizinho.Topo + Deslocamento(vizinho.Altura, r.Altura)),
            1 => (vizinho.Esquerda - vao - r.Largura, vizinho.Topo + Deslocamento(vizinho.Altura, r.Altura)),
            2 => (vizinho.Esquerda + Deslocamento(vizinho.Largura, r.Largura), vizinho.Base + vao),
            _ => (vizinho.Esquerda + Deslocamento(vizinho.Largura, r.Largura), vizinho.Topo - vao - r.Altura),
        };
        var tela = RetanguloPx.DePosicaoETamanho(new PontoPx(x, y), r);
        return telas.Any(t => t.Intersecta(tela)) ? null : tela;
    }

    /// <summary>
    /// Deslocamento ao longo do lado encostado: alinhado pelo início, pelo fim, centralizado,
    /// qualquer valor que ainda encoste (inclusive só pela quina) ou um que nem encoste.
    /// </summary>
    private int Deslocamento(int ladoDoVizinho, int ladoNovo) => aleatorio.Next(5) switch
    {
        0 => 0,
        1 => ladoDoVizinho - ladoNovo,
        2 => (ladoDoVizinho - ladoNovo) / 2,
        3 => aleatorio.Next(-ladoNovo, ladoDoVizinho + 1),
        _ => aleatorio.Next(-ladoNovo - 300, ladoDoVizinho + 301),
    };

    /// <summary>
    /// Torna principal o monitor do índice e move a origem (0,0) para ele, como o Windows faz. Também serve às topologias
    /// de exemplo da Fase 5 (<see cref="TopologiasDeExemplo.Rebaseada"/>), fora do fluxo aleatório.
    /// </summary>
    internal static void Rebasear(List<MonitorDoDesktop> lista, int principal)
    {
        int dx = -lista[principal].Tela.Esquerda;
        int dy = -lista[principal].Tela.Topo;
        for (int i = 0; i < lista.Count; i++)
        {
            lista[i] = lista[i] with
            {
                Tela = lista[i].Tela.Deslocado(dx, dy),
                AreaUtil = lista[i].AreaUtil.Deslocado(dx, dy),
                Principal = i == principal,
            };
        }
    }

    private List<T> Embaralhar<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = aleatorio.Next(i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
        return lista;
    }
}
