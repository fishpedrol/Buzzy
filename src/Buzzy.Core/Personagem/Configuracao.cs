namespace Buzzy.Core.Personagem;

/// <summary>
/// Parâmetros fixos do núcleo numa execução. As capacidades que chegam em fases posteriores
/// (queda física, painel de energia) ficam desligadas até lá, sem mudar a tabela de transições.
/// </summary>
public sealed record ConfiguracaoDoNucleo
{
    /// <summary>Tamanho lógico do sprite em DIPs (ARCHITECTURE.md 2.4).</summary>
    public TamanhoDip Tamanho { get; init; } = new(128, 128);

    /// <summary>Passos do relógio lógico por segundo (passo fixo proposto de 1/60 s, ARCHITECTURE.md 2.9).</summary>
    public int PassosPorSegundo { get; init; } = 60;

    /// <summary>Duração da reação a um clique, em passos.</summary>
    public int PassosDaReacao { get; init; } = 36;

    /// <summary>Duração do pouso, em passos.</summary>
    public int PassosDoPouso { get; init; } = 12;

    /// <summary>
    /// Intervalo de acomodação (ARCHITECTURE.md 2.6 e 2.7): tempo mínimo entre o fim de uma
    /// interação do usuário e a próxima decisão autônoma.
    /// </summary>
    public TimeSpan IntervaloDeAcomodacao { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Sem apoio depois de acomodar: verdadeiro vai para <see cref="Estado.Falling"/> (Fase 4);
    /// falso prende a âncora no chão da área útil, como a Fase 3 pede antes da queda animada.
    /// </summary>
    public bool QuedaFisica { get; init; }

    /// <summary>
    /// Se o passo do relógio move o personagem pelas superfícies (Fase 4, DEC-022): andar, escalar,
    /// pendurar-se, pular e cair. Desligado, os estados de movimento só mudam pelos sinais de
    /// movimento, como na Fase 2.
    /// </summary>
    public bool Movimento { get; init; }

    /// <summary>
    /// Se o personagem pode atravessar de um monitor para outro pelas portas planas (Fase 5, passo P13; DEC-032): a
    /// capacidade, como <see cref="Movimento"/> na Fase 4; a escolha do usuário é <see cref="Preferencias.AtravessarMonitores"/>.
    /// Desligada por padrão; o aplicativo a liga.
    /// </summary>
    public bool Travessia { get; init; }

    /// <summary>
    /// Se a troca de monitor do modo de tela cheia é um pulo (DEC-035): com o <see cref="Movimento"/> ligado, ele salta num
    /// arco até o cipó do monitor livre e, no fim da tela cheia, de volta à posição anterior; sem um arco que caiba na união
    /// das áreas úteis, aparece direto lá, como antes. Desligado por padrão; o aplicativo o liga.
    /// </summary>
    public bool PuloDaTelaCheia { get; init; }

    /// <summary>
    /// Se o Buzzy é curioso (Fase 7; DEC-026 e DEC-037): com a janela em primeiro plano há 30 s noutro monitor, vai ver; com
    /// ela no monitor dele por bastante tempo, chega perto e fica olhando. Desligada por padrão: os eventos da janela ativa são
    /// descartados e nada muda. O aplicativo a liga.
    /// </summary>
    public bool Curiosidade { get; init; }

    /// <summary>
    /// A personalidade da Fase 7 (DEC-037, itens 7 a 9): as reações ao clique por regra, os gestos por energia e explorar
    /// bordas, com o gerador próprio da personalidade. Desligada por padrão: tudo como antes. O aplicativo a liga.
    /// </summary>
    public bool Personalidade { get; init; }

    /// <summary>Quanto tempo com o primeiro plano noutro monitor faz o Buzzy ir ver (DEC-026: "30 segundos"), em todo nível de energia.</summary>
    public TimeSpan LimiarDeOutroMonitor { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Quanto o núcleo espera pelo vão da janela ativa pedido ao adaptador antes de desistir da aproximação (DEC-037).</summary>
    public TimeSpan EsperaPeloVao { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>A folga, em DIP, entre o sprite e a borda do trecho da janela ativa quando ele chega perto para olhar (DEC-037, item 5).</summary>
    public int FolgaDoOlhar { get; init; } = 16;

    /// <summary>Velocidades e gravidade do movimento (iguais em todos os níveis de energia).</summary>
    public ParametrosDeMovimento Fisica { get; init; } = new();

    /// <summary>Se o painel de energia existe (Fase 8). Antes, o clique duplo só produz reação.</summary>
    public bool PainelDeEnergiaDisponivel { get; init; }

    /// <summary>
    /// Se o clique duplo alterna o esconderijo (DEC-025, pedido do usuário): esconde o personagem
    /// atrás da borda mais próxima, só com a cabeça e as mãos para fora, e o tira de lá no clique
    /// duplo seguinte. Ligado, o painel de energia não abre pelo clique duplo; abre pelo menu.
    /// </summary>
    public bool EsconderijoNoCliqueDuplo { get; init; }

    /// <summary>Se a janela de configurações existe (Fase 8).</summary>
    public bool ConfiguracoesDisponiveis { get; init; }

    /// <summary>Ações que a agenda pode escolher. O app liga cada uma quando a fase dela chega.</summary>
    public AcoesAutonomas Acoes { get; init; } = AcoesAutonomas.Todas;

    /// <summary>Perfis por nível de energia.</summary>
    public Func<NivelDeEnergia, PerfilDeEnergia> Perfil { get; init; } = PerfilDeEnergia.Padrao;

    /// <summary>
    /// Se o tamagotchi adulto existe (DEC-028): itens, o uso e a onda de desenho animado de cada um. Desligado, nenhum
    /// efeito novo sai do núcleo, os eventos dele são ignorados e uma onda no estado não vale: tudo é como antes. Desligado
    /// por padrão; o aplicativo (<see cref="DoAplicativo"/>) o liga desde o passo T9, com o app e a arte prontos.
    /// </summary>
    public bool Tamagotchi { get; init; }

    /// <summary>Tamanho lógico do sprite de um item em DIPs (24 × 24 px de arte, ampliados 2×).</summary>
    public TamanhoDip TamanhoDoItem { get; init; } = new(48, 48);

    /// <summary>Quantos itens ficam na tela no máximo: um item a mais tira o mais antigo que não está na mão do usuário.</summary>
    public int MaximoDeItens { get; init; } = 6;

    /// <summary>
    /// Quanto o retângulo do personagem encolhe de cada lado, em %, para o alvo do soltar: o item solto conta "sobre ele"
    /// se o retângulo do item, já preso na área útil, cruza o retângulo do personagem encolhido.
    /// </summary>
    public int MargemDoAlvo { get; init; } = 20;

    /// <summary>A tabela dos itens; os testes podem trocá-la.</summary>
    public Func<Item, DadosDoItem> TabelaDeItens { get; init; } = TabelaDoTamagotchi.DoItem;

    /// <summary>A tabela das ondas; os testes podem trocá-la, por exemplo por durações curtas ou picos longos.</summary>
    public Func<Onda, DadosDaOnda> TabelaDeOndas { get; init; } = TabelaDoTamagotchi.DaOnda;

    /// <summary>
    /// A chance da paranoia (pedido do usuário de 2026-10-01: "quero que a chance dele ficar paranoico seja de 1 em 8"; e a
    /// escolha dele às 23:03, "uma vez por mistura"): a do sorteio de cada episódio de mistura com droga sintética, o único
    /// dele, feito pelo uso que fecha a mistura, sem a paranoia na frente (<see cref="CargaDaParanoia.MisturaComSintetica"/>
    /// e <see cref="CargaDaParanoia.Sorteada"/>), no gerador próprio da paranoia
    /// (<see cref="EstadoDoNucleo.AleatorioDaParanoia"/>): é a chance de ele ficar paranoico num episódio, por mais
    /// substâncias que ele use. É regra de jogo, de desenho animado. Os testes podem trocá-la, por exemplo por 1 em 1, para o
    /// sorteio sempre sair, ou 0 em 1, para nunca sair.
    /// </summary>
    public Chance ChanceDaParanoia { get; init; } = new(1, 8);

    /// <summary>
    /// Os itens que existem nesta edição (DEC-044, item 2); a completa por padrão. Um item fora dela não nasce, não é marcado
    /// e sai das preferências carregadas ou trocadas.
    /// </summary>
    public ConjuntoDeItens ItensDaEdicao { get; init; } = TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa);

    /// <summary>
    /// O tamanho do personagem em cada passo da escala (DEC-038, item 9): 96, 128 ou 192 DIP (1,5×, 2× ou 3× a arte de
    /// 64 px, ampliada sem suavização); fora do enum, o passo Médio.
    /// </summary>
    public static TamanhoDip TamanhoDoPersonagem(EscalaDoPersonagem escala) => escala switch
    {
        EscalaDoPersonagem.Pequena => new TamanhoDip(96, 96),
        EscalaDoPersonagem.Grande => new TamanhoDip(192, 192),
        _ => new TamanhoDip(128, 128),
    };

    /// <summary>A configuração do aplicativo com o personagem no passo de escala dado (DEC-038, item 9); os itens continuam com 48 DIP.</summary>
    public static ConfiguracaoDoNucleo DoAplicativo(EscalaDoPersonagem escala, EdicaoDoBuzzy edicao = EdicaoDoBuzzy.Completa)
        => DoAplicativo(TamanhoDoPersonagem(escala), edicao);

    /// <summary>
    /// A configuração que o aplicativo usa hoje. É a fonte única: o app e as simulações dos testes
    /// que escolhem sementes para ele partem daqui, para nunca divergirem.
    /// </summary>
    public static ConfiguracaoDoNucleo DoAplicativo(TamanhoDip tamanho, EdicaoDoBuzzy edicao = EdicaoDoBuzzy.Completa) => new()
    {
        Tamanho = tamanho,
        // A edição (DEC-044, item 2): a pública não tem as drogas ilícitas nem, com o baseado, o baseado por conta própria.
        ItensDaEdicao = TabelaDoTamagotchi.ItensDaEdicao(edicao),
        // Fase 4 (DEC-022 a DEC-025): física, queda animada, todas as ações, esconderijo no clique duplo. E o baseado por
        // conta própria (pedido do usuário de 2026-10-01, 19:10: ele às vezes fuma um baseado sozinho, quando quer), que
        // fica fora de Todas e só existe com a chave do tamagotchi ligada.
        Acoes = AcoesAutonomas.Todas | AcoesAutonomas.IrAoOutroMonitor
            | (edicao == EdicaoDoBuzzy.Completa ? AcoesAutonomas.UsarPorContaPropria : 0),
        QuedaFisica = true,
        Movimento = true,
        EsconderijoNoCliqueDuplo = true,
        // O tamagotchi adulto (DEC-028), ligado no passo T9, com o app e a arte prontos (D15): itens pelo menu, o uso e a
        // onda de desenho animado. Sem itens e sem o baseado por conta própria, tudo é como com a chave desligada
        // (invariante 22, ChaveLigadaTestes).
        Tamagotchi = true,
        // A travessia entre monitores (Fase 5, passo P13; DEC-032), pelas portas planas.
        Travessia = true,
        // A troca de monitor da tela cheia num pulo até o cipó (DEC-035).
        PuloDaTelaCheia = true,
        // Fase 8 (DEC-038, item 14): o painel compacto de energia, aberto pelo menu.
        PainelDeEnergiaDisponivel = true,
        ConfiguracoesDisponiveis = true,
        // Fase 7 (DEC-037, item 14): a curiosidade e a personalidade; desligar as duas é a chave de reversão.
        Curiosidade = true,
        Personalidade = true,
    };
}

/// <summary>
/// Pesos e tempos da agenda autônoma para um nível de energia (DEC-014, ARCHITECTURE.md 2.11).
/// Muda frequência e duração das ações, nunca a física nem as regras de apoio e segurança
/// (invariante 12). Os valores são iniciais e serão calibrados nas Fases 4 e 7.
/// </summary>
public sealed record PerfilDeEnergia(
    NivelDeEnergia Nivel,
    TimeSpan DecisaoMinima,
    TimeSpan DecisaoMaxima,
    TimeSpan DescansoMinimo,
    TimeSpan DescansoMaximo,
    int PassosDoGestoMinimo,
    int PassosDoGestoMaximo,
    int PesoAndar,
    int PesoEscalar,
    int PesoPular,
    int PesoDescansar,
    int PesoGesto,
    int PesoTrocarExpressao)
{
    /// <summary>Distância de uma caminhada autônoma, em DIP (Fase 4).</summary>
    public int DistanciaAndandoMinima { get; init; } = 150;

    public int DistanciaAndandoMaxima { get; init; } = 500;

    /// <summary>Distância horizontal de um pulo, em DIP (Fase 4).</summary>
    public int DistanciaDoPuloMinima { get; init; } = 80;

    public int DistanciaDoPuloMaxima { get; init; } = 200;

    /// <summary>Altura do arco de um pulo, em DIP (Fase 4).</summary>
    public int AlturaDoPuloMinima { get; init; } = 50;

    public int AlturaDoPuloMaxima { get; init; } = 100;

    /// <summary>Tempo na parede até a agenda decidir saltar ou soltar (Fase 4).</summary>
    public TimeSpan TempoNaParedeMinimo { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan TempoNaParedeMaximo { get; init; } = TimeSpan.FromSeconds(35);

    /// <summary>Tempo pendurado até a próxima decisão: "por pouco tempo" (Fase 4).</summary>
    public TimeSpan TempoPenduradoMinimo { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan TempoPenduradoMaximo { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Em quantas subidas, de cada 100, ele dispara parede acima num foguete de borracha (toon
    /// force, DEC-023). É frequência de uma ação; a velocidade do foguete é a mesma em todo nível.
    /// </summary>
    public int ChanceDoFoguete { get; init; } = 30;

    /// <summary>
    /// O peso de fumar um baseado por conta própria (<see cref="AcoesAutonomas.UsarPorContaPropria"/>; pedido do usuário de
    /// 2026-10-01, 19:10), o último do sorteio de IDLE. Vale só com a chave do tamagotchi ligada, em IDLE no chão, sem estar
    /// escondido, e é zero com a onda Chapado ou a paranoia na frente, para ele não emendar um no outro; a onda de um item não
    /// o muda. O mesmo nos três níveis, calibrado por simulação do núcleo, só com a autonomia, para cerca de um baseado a
    /// cada 4 minutos de tempo elegível (IDLE no chão, sem Chapado nem paranoia na frente) na energia Média; o nível muda a
    /// frequência pelos intervalos e pelos outros pesos, como em toda ação. Número de jogo, de desenho animado.
    /// </summary>
    public int PesoUsarPorContaPropria { get; init; } = 1;

    /// <summary>
    /// O peso de atravessar para o monitor vizinho ao chegar a uma porta plana andando (Fase 5, passo P13; DEC-032), contra o
    /// caminho de sempre da parede (parar, escalar ou virar; DEC-023). Frequência, nunca física (invariante 12). Valores
    /// iniciais, a calibrar na verificação de tela.
    /// </summary>
    public int PesoAtravessar { get; init; } = 5;

    /// <summary>
    /// O peso de ir ao outro monitor (<see cref="AcoesAutonomas.IrAoOutroMonitor"/>; Fase 5, passo P13) no sorteio de IDLE:
    /// andar até a porta plana e atravessar. Frequência, nunca física (invariante 12). Valores iniciais, a calibrar.
    /// </summary>
    public int PesoIrAoOutroMonitor { get; init; } = 2;

    /// <summary>
    /// A tendência de expressão (Fase 6; DEC-036, item 6; Q-23): o peso de cada cara de humor na troca de cara automática,
    /// na ordem de <see cref="Expressoes.DeHumor"/>, num único sorteio que nunca repete a cara atual. Nulo, todas as outras
    /// valem igual, com o sorteio de antes (a Média: as referências gravadas não mudam). Frequência de caras, nunca física
    /// (invariante 12).
    /// </summary>
    public IReadOnlyList<int>? PesosDasCaras { get; init; }

    // A Fase 7 (DEC-037): os valores padrão são os da Média; a Baixa e a Alta os trocam. Frequências e tempos, nunca física
    // (invariante 12). Iniciais, a calibrar no passo F7-P10.

    /// <summary>Quanto tempo com o primeiro plano no monitor dele até a curiosidade o fazer chegar perto da janela (DEC-037, item 5).</summary>
    public TimeSpan TempoParaAproximar { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Quantas decisões da agenda ele fica olhando a janela, no mínimo.</summary>
    public int RodadasOlhandoMinimo { get; init; } = 2;

    /// <summary>Quantas decisões da agenda ele fica olhando a janela, no máximo.</summary>
    public int RodadasOlhandoMaximo { get; init; } = 4;

    /// <summary>
    /// Os pesos de cada decisão enquanto olha a janela, nesta ordem: ficar olhando, olhar ao redor, coçar-se e perder o
    /// interesse.
    /// </summary>
    public IReadOnlyList<int> PesosOlhando { get; init; } = [4, 2, 1, 1];

    /// <summary>Quanto ele espera, depois de perder o interesse, antes de chegar perto de novo.</summary>
    public TimeSpan IntervaloEntreCuriosidades { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Depois de o usuário soltá-lo, mostrá-lo ou redefinir a posição, quanto a curiosidade fica dispensada (DEC-037, item 6).</summary>
    public TimeSpan EsperaDepoisDoUsuario { get; init; } = TimeSpan.FromMinutes(8);

    /// <summary>A duração da reação ao clique comum com a personalidade ligada, em passos (DEC-037, item 8); sem ela, vale a da configuração.</summary>
    public int PassosDaReacao { get; init; } = 36;

    /// <summary>
    /// Os pesos do tipo do gesto com a personalidade (DEC-037, item 9), na ordem do enum: espiar, olhar ao redor, coçar-se,
    /// espreguiçar-se e brincar. Nulo, o sorteio uniforme de antes.
    /// </summary>
    public IReadOnlyList<int>? PesosDosGestos { get; init; }

    /// <summary>Explorar a borda (DEC-037, item 9): uma caminhada em cada este número vai à lateral mais próxima e espia para fora.</summary>
    public int ExplorarBordaUmEm { get; init; } = 4;

    public static readonly PerfilDeEnergia Baixa = new(
        NivelDeEnergia.Baixa,
        TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(45),
        TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(180),
        60, 120,
        PesoAndar: 2, PesoEscalar: 1, PesoPular: 0, PesoDescansar: 6, PesoGesto: 3, PesoTrocarExpressao: 3)
    {
        DistanciaAndandoMinima = 80,
        DistanciaAndandoMaxima = 250,
        DistanciaDoPuloMinima = 60,
        DistanciaDoPuloMaxima = 120,
        AlturaDoPuloMinima = 30,
        AlturaDoPuloMaxima = 60,
        TempoNaParedeMinimo = TimeSpan.FromSeconds(10),
        TempoNaParedeMaximo = TimeSpan.FromSeconds(20),
        TempoPenduradoMinimo = TimeSpan.FromSeconds(2),
        TempoPenduradoMaximo = TimeSpan.FromSeconds(5),
        ChanceDoFoguete = 10,
        PesoAtravessar = 3,
        PesoIrAoOutroMonitor = 1,
        // Calmo: sono, tédio e pensamento na frente; agitação, rara.
        //                 neutro feliz rindo curioso surpreso assustado sonolento bocejando dormindo travesso entediado pensativo empolgado determinado
        PesosDasCaras = [      3,    2,    1,      2,       1,        1,        4,        4,       2,       1,        4,        4,        1,          1],
        TempoParaAproximar = TimeSpan.FromSeconds(180),
        RodadasOlhandoMinimo = 3,
        RodadasOlhandoMaximo = 5,
        PesosOlhando = [6, 1, 1, 1],
        IntervaloEntreCuriosidades = TimeSpan.FromMinutes(10),
        EsperaDepoisDoUsuario = TimeSpan.FromMinutes(12),
        PassosDaReacao = 30,
        // Calmo: coçar-se e espreguiçar-se.   espiar olhar coçar espreguiçar brincar
        PesosDosGestos = [                        1,     2,    3,          4,      1],
        ExplorarBordaUmEm = 5,
    };

    public static readonly PerfilDeEnergia Media = new(
        NivelDeEnergia.Media,
        TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90),
        60, 150,
        PesoAndar: 4, PesoEscalar: 2, PesoPular: 1, PesoDescansar: 3, PesoGesto: 3, PesoTrocarExpressao: 2);

    public static readonly PerfilDeEnergia Alta = new(
        NivelDeEnergia.Alta,
        TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40),
        90, 180,
        PesoAndar: 5, PesoEscalar: 3, PesoPular: 3, PesoDescansar: 1, PesoGesto: 4, PesoTrocarExpressao: 2)
    {
        DistanciaAndandoMinima = 250,
        DistanciaAndandoMaxima = 900,
        DistanciaDoPuloMinima = 120,
        DistanciaDoPuloMaxima = 320,
        AlturaDoPuloMinima = 70,
        AlturaDoPuloMaxima = 150,
        TempoNaParedeMinimo = TimeSpan.FromSeconds(20),
        TempoNaParedeMaximo = TimeSpan.FromSeconds(45),
        TempoPenduradoMinimo = TimeSpan.FromSeconds(5),
        TempoPenduradoMaximo = TimeSpan.FromSeconds(12),
        ChanceDoFoguete = 50,
        PesoAtravessar = 7,
        PesoIrAoOutroMonitor = 3,
        // Agitado: empolgação, riso e travessura na frente; sono, raro.
        //                 neutro feliz rindo curioso surpreso assustado sonolento bocejando dormindo travesso entediado pensativo empolgado determinado
        PesosDasCaras = [      1,    3,    4,      3,       3,        1,        1,        1,       1,       4,        1,        1,        4,          3],
        TempoParaAproximar = TimeSpan.FromSeconds(45),
        RodadasOlhandoMinimo = 1,
        RodadasOlhandoMaximo = 3,
        PesosOlhando = [2, 3, 2, 2],
        IntervaloEntreCuriosidades = TimeSpan.FromMinutes(3),
        EsperaDepoisDoUsuario = TimeSpan.FromMinutes(5),
        PassosDaReacao = 48,
        // Agitado: brincar, olhar ao redor e espiar. espiar olhar coçar espreguiçar brincar
        PesosDosGestos = [                              3,     3,    1,          1,      4],
        ExplorarBordaUmEm = 3,
    };

    public static PerfilDeEnergia Padrao(NivelDeEnergia nivel) => nivel switch
    {
        NivelDeEnergia.Baixa => Baixa,
        NivelDeEnergia.Media => Media,
        NivelDeEnergia.Alta => Alta,
        _ => throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "Nível de energia desconhecido."),
    };
}
