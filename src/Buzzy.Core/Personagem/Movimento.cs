namespace Buzzy.Core.Personagem;

// Física do movimento, em DIP e segundos (convertidos pela escala do monitor
// da âncora). Igual em todo nível de energia: a energia muda frequência e
// tamanho das ações (PerfilDeEnergia), nunca velocidade nem gravidade.
public sealed record ParametrosDeMovimento
{
    // Velocidades em DIP/s.
    public double VelocidadeAndando { get; init; } = 90;

    public double VelocidadeEscalando { get; init; } = 110;

    // Andando pendurado na borda de cima.
    public double VelocidadePendurado { get; init; } = 80;

    // DIP/s².
    public double Gravidade { get; init; } = 2200;

    public double VelocidadeMaximaDeQueda { get; init; } = 1500;

    // Velocidade horizontal ao saltar da parede ou do teto.
    public double ImpulsoDaParede { get; init; } = 220;

    // DIP.
    public double AlturaDoPuloDaParede { get; init; } = 40;

    // Travessia: maior degrau, em DIP, entre o chão de um monitor e o do
    // vizinho que ele sobe num salto, medido pela menor escala dos dois. Mais
    // alto que isso, só pela parede.
    public double SubidaMaxima { get; init; } = 120;

    // Maior degrau, em DIP, que ele desce num salto.
    public double DescidaMaxima { get; init; } = 480;

    // Tempos de voo (s) do salto de travessia, na ordem em que o solucionador
    // tenta: vence o primeiro cujo arco deixa o sprite sempre na união das
    // áreas úteis. Os longos são pra subida, que precisa subir antes de passar
    // da borda.
    public IReadOnlyList<double> TemposDoSalto { get; init; } = TemposDoSaltoPadrao;

    private static readonly double[] TemposDoSaltoPadrao = [0.30, 0.40, 0.50, 0.65, 0.80, 1.00, 1.20, 1.40];

    // Pulo da tela cheia: em vez de teleportar, um arco só até o cipó do
    // monitor livre e, no fim da tela cheia, de volta.

    // Média ao longo da reta entre partida e chegada, em DIP/s.
    public double VelocidadeDoPuloDaTelaCheia { get; init; } = 2400;

    // Em s: pulo curto não pode virar um piscar.
    public double TempoMinimoDoPuloDaTelaCheia { get; init; } = 0.6;

    // Em s: de uma ponta à outra não pode se arrastar.
    public double TempoMaximoDoPuloDaTelaCheia { get; init; } = 1.2;

    // Quanto o arco sobe acima da reta (ou desce, no balanço de cipó a cipó),
    // em DIP, entre pontos de alturas parecidas; no máximo metade da distância.
    public double AlturaDoPuloDaTelaCheia { get; init; } = 160;

    // Distância, em DIP, da lateral do monitor livre (do lado de onde ele vem)
    // em que agarra o cipó.
    public double EntradaNoCipo { get; init; } = 200;

    // Pulinho: perto da lateral que encosta no monitor livre, só salta pro
    // outro lado da borda.

    // Até essa distância (DIP) entre a âncora e a lateral, vira pulinho.
    public double DistanciaDoPulinho { get; init; } = 320;

    // A que distância (DIP) da borda ele pousa do outro lado; encostado na
    // parede, se estava escalando.
    public double EntradaDoPulinho { get; init; } = 96;

    // DIP acima da reta (ou abaixo, de cipó a cipó); no máximo metade da distância.
    public double AlturaDoPulinho { get; init; } = 72;

    // Em s.
    public double TempoMinimoDoPulinho { get; init; } = 0.35;

    // Espaço livre mínimo (DIP) pra andar ou pular numa direção; menos que isso, vira.
    public double EspacoMinimo { get; init; } = 24;

    // Física de desenho animado, de borracha como o Luffy, igual em todo
    // nível de energia. Zero em RestituicaoDoQuique ou VelocidadeDoFoguete desliga.

    // Fração da velocidade de impacto devolvida pra cima.
    public double RestituicaoDoQuique { get; init; } = 0.5;

    // DIP/s; abaixo disso, pousa.
    public double ImpactoMinimoDoQuique { get; init; } = 600;

    public int QuiquesMaximos { get; init; } = 2;

    // Fração da velocidade horizontal que sobra a cada quique.
    public double AtritoDoQuique { get; init; } = 0.7;

    // Subida disparada parede acima até a borda de cima, em DIP/s.
    public double VelocidadeDoFoguete { get; init; } = 1000;

    // Agarrar onde o usuário solta: no alto, pega o cipó de cima; junto de uma
    // lateral, gruda na parede. Perto do chão, cai.

    // Máximo, em DIP, entre o topo do sprite e a borda de cima.
    public double DistanciaParaOCipo { get; init; } = 96;

    // Máximo, em DIP, entre a âncora e a lateral.
    public double DistanciaParaAParede { get; init; } = 64;

    // Altura mínima dos pés (DIP) pra agarrar em vez de cair; também é a folga
    // que o preso deixa até o chão.
    public double AlturaMinimaParaAgarrar { get; init; } = 32;

    // Passeio, em DIP, de quem está preso pelo usuário na parede ou no cipó.
    public int PasseioPresoMinimo { get; init; } = 40;

    public int PasseioPresoMaximo { get; init; } = 220;

    // Itens: nascem ao lado do personagem, acima do chão, e caem com a mesma
    // gravidade e queda máxima dele, com um quique de leve.

    // DIP acima dos pés do personagem.
    public double AlturaDaQuedaDoItem { get; init; } = 140;

    // DIP entre o item novo e o personagem, e entre dois itens.
    public double FolgaDoItem { get; init; } = 8;

    // Fração da velocidade de impacto devolvida pra cima.
    public double RestituicaoDoItem { get; init; } = 0.35;

    // DIP/s; abaixo disso o item para no chão.
    public double ImpactoMinimoDoItem { get; init; } = 300;

    public int QuiquesDoItem { get; init; } = 1;
}

// Estado físico do movimento em curso. Âncora fina em px físicos (a janela
// usa a arredondada, em EstadoDoNucleo.Lugar); velocidades em px/s.
// VY positivo é pra baixo. Restante: px que faltam na caminhada.
// SentidoVertical na escalada: -1 sobe, +1 desce. QuerEscalar: anda até a
// parede pra escalar.
public sealed record EstadoDoMovimento(double X, double Y, double VX, double VY, double Restante, int SentidoVertical, bool QuerEscalar)
{
    public static readonly EstadoDoMovimento Nenhum = new(0, 0, 0, 0, 0, -1, false);

    // Quiques já dados nesta queda.
    public int Quiques { get; init; }

    // Escalada disparada (foguete de borracha).
    public bool Foguete { get; init; }

    // Parado na lateral ou no cipó: o passo físico não move nada e o relógio
    // fica desligado até a próxima decisão.
    public bool Agarrado { get; init; }

    // Travessia em curso pro monitor vizinho, atômica; nula fora dela. Sair da
    // caminhada desfaz.
    public Travessia? Travessia { get; init; }

    // Anda até a porta plana pra atravessar, sem sortear na porta.
    public bool QuerAtravessar { get; init; }

    // Indo à lateral mais próxima pra espiar pra fora: lá nunca atravessa nem
    // escala, para e espia. Qualquer movimento novo desfaz.
    public bool ExplorandoBorda { get; init; }

    // Curiosidade indo até o lado da janela; na chegada, olha. Qualquer
    // movimento novo desfaz.
    public bool Aproximando { get; init; }
}

// Superfícies do monitor como limites da âncora (centro da base do sprite),
// com o sprite inteiro na área útil. As duas laterais são escaláveis, mesmo a
// que encosta em outro monitor: a borda da tela vira parede pro macaquinho.
// Teto é o Y da âncora com o topo do sprite na borda de cima (pendurado).
// Passagem* = outro monitor encosta naquela lateral.
public readonly record struct Superficies(int Esquerda, int Direita, int Chao, int Teto, bool PassagemEsquerda, bool PassagemDireita)
{
    // Lateral é passagem quando a tela de outro monitor encosta nela na altura
    // da área útil. Janela de outro app nunca é superfície. Sprite mais largo
    // que a área útil: as duas laterais ficam no meio, onde
    // Posicionador.PrenderNaAreaUtil o põe.
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

    // lado: -1 esquerda, +1 direita. Tanto parede quanto passagem contam.
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
