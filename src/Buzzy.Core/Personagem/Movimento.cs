namespace Buzzy.Core.Personagem;

/// <summary>
/// Parâmetros físicos do movimento (ARCHITECTURE.md 2.9), em DIPs e segundos, convertidos pela
/// escala do monitor da âncora. São os mesmos em todos os níveis de energia (invariante 12): a
/// energia muda a frequência e o tamanho das ações (<see cref="PerfilDeEnergia"/>), nunca as
/// velocidades nem a gravidade.
/// </summary>
public sealed record ParametrosDeMovimento
{
    /// <summary>Caminhada no chão, em DIP/s.</summary>
    public double VelocidadeAndando { get; init; } = 90;

    /// <summary>Subida e descida nas paredes laterais, em DIP/s.</summary>
    public double VelocidadeEscalando { get; init; } = 110;

    /// <summary>Deslocamento pendurado na borda superior, em DIP/s.</summary>
    public double VelocidadePendurado { get; init; } = 80;

    /// <summary>Gravidade, em DIP/s².</summary>
    public double Gravidade { get; init; } = 2200;

    /// <summary>Velocidade máxima de queda, em DIP/s.</summary>
    public double VelocidadeMaximaDeQueda { get; init; } = 1500;

    /// <summary>Velocidade horizontal ao saltar da parede ou do teto, em DIP/s.</summary>
    public double ImpulsoDaParede { get; init; } = 220;

    /// <summary>Altura do salto para longe da parede, em DIP.</summary>
    public double AlturaDoPuloDaParede { get; init; } = 40;

    /// <summary>
    /// Travessia (Fase 5, passo P13b; DEC-032): a maior subida, em DIP, de um degrau entre o chão de um monitor e o do vizinho
    /// que ele vence num salto; medida pela menor escala dos dois, igual em todo nível de energia (invariante 12). Acima
    /// disso, só pela parede (o transbordo, P13c).
    /// </summary>
    public double SubidaMaxima { get; init; } = 120;

    /// <summary>A maior descida, em DIP, de um degrau que ele vence num salto (P13b).</summary>
    public double DescidaMaxima { get; init; } = 480;

    /// <summary>
    /// Os tempos de voo do salto de travessia, em segundos, na ordem em que o solucionador os tenta (P13b): o primeiro com um
    /// arco em que o sprite fica sempre na união das áreas úteis vence. Os longos servem à subida, que precisa subir antes de
    /// passar da borda.
    /// </summary>
    public IReadOnlyList<double> TemposDoSalto { get; init; } = TemposDoSaltoPadrao;

    private static readonly double[] TemposDoSaltoPadrao = [0.30, 0.40, 0.50, 0.65, 0.80, 1.00, 1.20, 1.40];

    // O pulo da tela cheia (DEC-035, pedido do usuário de 2026-10-03: "ele ta literalmente teleportando"): da posição dele ao
    // cipó do monitor livre e, no fim da tela cheia, de volta, num arco só.

    /// <summary>Velocidade média do pulo da tela cheia ao longo da reta entre a partida e a chegada, em DIP/s.</summary>
    public double VelocidadeDoPuloDaTelaCheia { get; init; } = 2400;

    /// <summary>Menor duração do pulo da tela cheia, em segundos: um pulo curto não vira um piscar.</summary>
    public double TempoMinimoDoPuloDaTelaCheia { get; init; } = 0.6;

    /// <summary>Maior duração do pulo da tela cheia, em segundos: um pulo de uma ponta à outra não se arrasta.</summary>
    public double TempoMaximoDoPuloDaTelaCheia { get; init; } = 1.2;

    /// <summary>
    /// Quanto o arco do pulo da tela cheia sobe acima da reta (ou desce abaixo dela, no balanço de cipó a cipó), em DIP, entre
    /// pontos de alturas parecidas; limitado à metade da distância.
    /// </summary>
    public double AlturaDoPuloDaTelaCheia { get; init; } = 160;

    /// <summary>A que distância, em DIP, da lateral do monitor livre voltada para a partida ele agarra o cipó.</summary>
    public double EntradaNoCipo { get; init; } = 200;

    // O pulinho (DEC-035, item 6; pedido do usuário de 2026-10-03: "quando ele estiver na borda do monitor queria que ele so
    // desse um pulinho msm"): perto da lateral que encosta no monitor livre, ele só salta para o outro lado da borda.

    /// <summary>Até que distância, em DIP, entre a âncora e a lateral que encosta no monitor livre a troca é um pulinho.</summary>
    public double DistanciaDoPulinho { get; init; } = 320;

    /// <summary>A que distância, em DIP, da borda ele pousa do outro lado no pulinho (encostado na parede, se escalava).</summary>
    public double EntradaDoPulinho { get; init; } = 96;

    /// <summary>Quanto o pulinho sobe acima da reta (ou desce, de cipó a cipó), em DIP; limitado à metade da distância.</summary>
    public double AlturaDoPulinho { get; init; } = 72;

    /// <summary>Menor duração do pulinho, em segundos.</summary>
    public double TempoMinimoDoPulinho { get; init; } = 0.35;

    /// <summary>Menor espaço livre, em DIP, para uma caminhada ou um pulo naquela direção; menos que isso, vira.</summary>
    public double EspacoMinimo { get; init; } = 24;

    // Toon force (DEC-023): física de desenho animado, de borracha como o Luffy, igual em todos os
    // níveis de energia. Zero em RestituicaoDoQuique ou em VelocidadeDoFoguete desliga o efeito.

    /// <summary>Quique de borracha: fração da velocidade de impacto devolvida para cima.</summary>
    public double RestituicaoDoQuique { get; init; } = 0.5;

    /// <summary>Menor velocidade de impacto que faz quicar, em DIP/s; abaixo dela, pousa.</summary>
    public double ImpactoMinimoDoQuique { get; init; } = 600;

    /// <summary>Quiques seguidos, no máximo, antes de pousar.</summary>
    public int QuiquesMaximos { get; init; } = 2;

    /// <summary>Fração da velocidade horizontal que sobra a cada quique.</summary>
    public double AtritoDoQuique { get; init; } = 0.7;

    /// <summary>Foguete de borracha: subida disparada parede acima até a borda superior, em DIP/s.</summary>
    public double VelocidadeDoFoguete { get; init; } = 1000;

    // Agarrar onde o usuário solta (DEC-024): solto no alto, agarra o cipó da borda de cima; solto
    // junto a uma lateral, fica grudado na parede. Perto do chão, cai como antes.

    /// <summary>Distância máxima, em DIP, entre o topo do sprite e a borda de cima para agarrar o cipó.</summary>
    public double DistanciaParaOCipo { get; init; } = 96;

    /// <summary>Distância máxima, em DIP, entre a âncora e o limite de uma lateral para grudar na parede.</summary>
    public double DistanciaParaAParede { get; init; } = 64;

    /// <summary>Altura mínima dos pés acima do chão, em DIP, para agarrar em vez de cair; também é a folga que o preso deixa até o chão.</summary>
    public double AlturaMinimaParaAgarrar { get; init; } = 32;

    /// <summary>Menor passeio, em DIP, de quem está preso pelo usuário, pela parede ou pelo cipó.</summary>
    public int PasseioPresoMinimo { get; init; } = 40;

    /// <summary>Maior passeio, em DIP, de quem está preso pelo usuário.</summary>
    public int PasseioPresoMaximo { get; init; } = 220;

    // Itens do tamagotchi (DEC-028): o item invocado nasce ao lado do personagem, acima do chão, e cai com a mesma
    // gravidade e a mesma queda máxima dele, quicando uma vez só, de leve.

    /// <summary>Altura, em DIP, acima dos pés do personagem, de onde o item invocado começa a cair.</summary>
    public double AlturaDaQuedaDoItem { get; init; } = 140;

    /// <summary>Folga, em DIP, entre o item que nasce e o personagem, e entre dois itens.</summary>
    public double FolgaDoItem { get; init; } = 8;

    /// <summary>Quique do item: fração da velocidade de impacto devolvida para cima.</summary>
    public double RestituicaoDoItem { get; init; } = 0.35;

    /// <summary>Menor velocidade de impacto, em DIP/s, que faz o item quicar; abaixo dela, ele para no chão.</summary>
    public double ImpactoMinimoDoItem { get; init; } = 300;

    /// <summary>Quiques do item, no máximo, antes de parar.</summary>
    public int QuiquesDoItem { get; init; } = 1;
}

/// <summary>
/// Estado físico do movimento em curso, parte do estado do núcleo. A posição fina da âncora fica
/// em pixels físicos (a janela usa a arredondada, em <see cref="EstadoDoNucleo.Lugar"/>); as
/// velocidades, em pixels físicos por segundo.
/// </summary>
/// <param name="X">Âncora fina, horizontal.</param>
/// <param name="Y">Âncora fina, vertical.</param>
/// <param name="VX">Velocidade horizontal no ar.</param>
/// <param name="VY">Velocidade vertical no ar (positiva para baixo).</param>
/// <param name="Restante">Caminhada: pixels que faltam para o fim do percurso.</param>
/// <param name="SentidoVertical">Escalada: −1 sobe, +1 desce.</param>
/// <param name="QuerEscalar">Caminhada: anda até a parede para escalar.</param>
public sealed record EstadoDoMovimento(double X, double Y, double VX, double VY, double Restante, int SentidoVertical, bool QuerEscalar)
{
    public static readonly EstadoDoMovimento Nenhum = new(0, 0, 0, 0, 0, -1, false);

    /// <summary>Quiques de borracha já dados nesta queda (toon force, DEC-023).</summary>
    public int Quiques { get; init; }

    /// <summary>Escalada: sobe disparado, num foguete de borracha (toon force, DEC-023).</summary>
    public bool Foguete { get; init; }

    /// <summary>
    /// Parado, agarrado à lateral ou ao cipó (DEC-024): o passo físico não move nada e o relógio
    /// fica desligado até a próxima decisão.
    /// </summary>
    public bool Agarrado { get; init; }

    /// <summary>
    /// Caminhada: a travessia em curso para o monitor vizinho (Fase 5, passo P13; DEC-032), atômica; nula fora dela. Sair da
    /// caminhada a desfaz.
    /// </summary>
    public Travessia? Travessia { get; init; }

    /// <summary>Caminhada: anda até a porta plana para atravessar (<see cref="AcoesAutonomas.IrAoOutroMonitor"/>), sem o sorteio da porta.</summary>
    public bool QuerAtravessar { get; init; }

    /// <summary>
    /// Caminhada até a lateral mais próxima para espiar para fora dela (DEC-037, item 9): na lateral, nunca atravessa nem
    /// escala; para e espia. Todo movimento novo a desfaz.
    /// </summary>
    public bool ExplorandoBorda { get; init; }

    /// <summary>Caminhada da curiosidade até o lado do trecho da janela (DEC-037, item 5): na chegada, olha. Todo movimento novo a desfaz.</summary>
    public bool Aproximando { get; init; }
}

/// <summary>
/// Superfícies do monitor em que o personagem está (ARCHITECTURE.md 2.5), já como limites da
/// âncora (centro da base do sprite), com o sprite inteiro na área útil. Com a toon force
/// (DEC-023), as duas laterais são escaláveis, mesmo a que encosta em outro monitor: a borda da
/// tela vira parede para o macaquinho.
/// </summary>
/// <param name="Esquerda">Menor X da âncora: o sprite encosta na lateral esquerda da área útil.</param>
/// <param name="Direita">Maior X da âncora: o sprite encosta na lateral direita.</param>
/// <param name="Chao">Y da âncora com os pés no chão (a borda de baixo da área útil).</param>
/// <param name="Teto">Y da âncora com o topo do sprite na borda de cima (pendurado).</param>
/// <param name="PassagemEsquerda">Se outro monitor encosta na lateral esquerda (passagem, Fase 5); senão, é parede.</param>
/// <param name="PassagemDireita">Se outro monitor encosta na lateral direita.</param>
public readonly record struct Superficies(int Esquerda, int Direita, int Chao, int Teto, bool PassagemEsquerda, bool PassagemDireita)
{
    /// <summary>
    /// Superfícies do monitor para um sprite do tamanho dado. Uma lateral é passagem quando a tela
    /// de outro monitor encosta nela, na altura da área útil; senão, é parede. Janelas de outros
    /// aplicativos nunca são superfície (Q-05). Com o sprite mais largo que a área útil, as duas
    /// laterais ficam no meio, onde a validação o põe (<see cref="Posicionador.PrenderNaAreaUtil"/>).
    /// </summary>
    public static Superficies Do(Topologia topologia, MonitorDoDesktop monitor, TamanhoPx tamanho)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        ArgumentNullException.ThrowIfNull(monitor);
        RetanguloPx area = monitor.AreaUtil;
        int metade = tamanho.Largura / 2;
        int esquerda = tamanho.Largura <= area.Largura ? area.Esquerda + metade : area.Esquerda + (area.Largura - tamanho.Largura) / 2 + metade;
        int direita = Math.Max(esquerda, area.Direita - (tamanho.Largura - metade));
        int chao = area.Base;
        int teto = Math.Min(chao, area.Topo + tamanho.Altura);
        return new Superficies(esquerda, direita, chao, teto, Encostado(topologia, monitor, -1), Encostado(topologia, monitor, +1));
    }

    /// <summary>
    /// Se a âncora está encostada numa lateral da área útil, parede ou passagem (as duas são
    /// escaláveis com a toon force); devolve o lado (−1 esquerda, +1 direita).
    /// </summary>
    public bool NaLateral(double x, out int lado)
    {
        if (x >= Direita - 0.5) { lado = +1; return true; }
        if (x <= Esquerda + 0.5) { lado = -1; return true; }
        lado = 0;
        return false;
    }

    private static bool Encostado(Topologia topologia, MonitorDoDesktop monitor, int lado)
    {
        foreach (MonitorDoDesktop outro in topologia.Monitores)
        {
            if (ReferenceEquals(outro, monitor) || outro.Chave == monitor.Chave) continue;
            bool encosta = lado < 0 ? outro.Tela.Direita == monitor.Tela.Esquerda : outro.Tela.Esquerda == monitor.Tela.Direita;
            bool naAltura = outro.Tela.Topo < monitor.AreaUtil.Base && outro.Tela.Base > monitor.AreaUtil.Topo;
            if (encosta && naAltura) return true;
        }
        return false;
    }
}
