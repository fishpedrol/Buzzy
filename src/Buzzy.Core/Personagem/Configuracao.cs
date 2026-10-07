namespace Buzzy.Core.Personagem;

// Parâmetros fixos do núcleo numa execução. Capacidade nova entra desligada
// aqui, sem mexer na tabela de transições.
public sealed record ConfiguracaoDoNucleo
{
    // Tamanho lógico do sprite, em DIP.
    public TamanhoDip Tamanho { get; init; } = new(128, 128);

    // Passo fixo de 1/60 s.
    public int PassosPorSegundo { get; init; } = 60;

    // Em passos.
    public int PassosDaReacao { get; init; } = 36;

    // Em passos.
    public int PassosDoPouso { get; init; } = 12;

    // Tempo mínimo entre o fim de uma interação do usuário e a próxima
    // decisão autônoma.
    public TimeSpan IntervaloDeAcomodacao { get; init; } = TimeSpan.FromSeconds(3);

    // Sem apoio depois de acomodar: true cai (Falling); false gruda a âncora
    // no chão da área útil, sem animação de queda.
    public bool QuedaFisica { get; init; }

    // Se o relógio move o personagem pelas superfícies (andar, escalar,
    // pendurar, pular, cair). Desligado, os estados de movimento só mudam
    // pelos sinais de movimento.
    public bool Movimento { get; init; }

    // Capacidade de atravessar entre monitores pelas portas planas. A escolha
    // do usuário fica em Preferencias.AtravessarMonitores. O app liga.
    public bool Travessia { get; init; }

    // Troca de monitor da tela cheia vira pulo: com Movimento ligado, salta em
    // arco até o cipó do monitor livre e volta no fim. Se nenhum arco cabe na
    // união das áreas úteis, aparece direto lá. O app liga.
    public bool PuloDaTelaCheia { get; init; }

    // Janela em primeiro plano há 30 s noutro monitor: ele vai ver. No monitor
    // dele por bastante tempo: chega perto e fica olhando. Desligada, os eventos
    // da janela ativa são descartados. O app liga.
    public bool Curiosidade { get; init; }

    // Reações ao clique por regra, gestos por energia e explorar bordas, com
    // gerador próprio. Desligada, tudo como antes. O app liga.
    public bool Personalidade { get; init; }

    // Igual em todo nível de energia.
    public TimeSpan LimiarDeOutroMonitor { get; init; } = TimeSpan.FromSeconds(30);

    // Quanto espera o vão da janela ativa antes de desistir da aproximação.
    public TimeSpan EsperaPeloVao { get; init; } = TimeSpan.FromSeconds(2);

    // Folga em DIP entre o sprite e a borda da janela ativa quando chega perto pra olhar.
    public int FolgaDoOlhar { get; init; } = 16;

    // Igual em todos os níveis de energia.
    public ParametrosDeMovimento Fisica { get; init; } = new();

    // Sem painel, o clique duplo só faz reação.
    public bool PainelDeEnergiaDisponivel { get; init; }

    // Clique duplo esconde atrás da borda mais próxima (só cabeça e mãos pra
    // fora) e o próximo tira de lá. Ligado, o painel de energia abre pelo menu.
    public bool EsconderijoNoCliqueDuplo { get; init; }

    public bool ConfiguracoesDisponiveis { get; init; }

    // O que a agenda pode escolher.
    public AcoesAutonomas Acoes { get; init; } = AcoesAutonomas.Todas;

    public Func<NivelDeEnergia, PerfilDeEnergia> Perfil { get; init; } = PerfilDeEnergia.Padrao;

    // Itens, uso e a onda de desenho animado de cada um. Desligado, nenhum
    // efeito novo sai, os eventos dele são ignorados e uma onda no estado não
    // vale. O app liga (DoAplicativo).
    public bool Tamagotchi { get; init; }

    // Em DIP: arte de 24 × 24 px ampliada 2×.
    public TamanhoDip TamanhoDoItem { get; init; } = new(48, 48);

    // Passou disso, sai o mais antigo que não está na mão do usuário.
    public int MaximoDeItens { get; init; } = 6;

    // Quanto (%) o retângulo do personagem encolhe de cada lado pro alvo do
    // soltar: o item conta "sobre ele" se, já preso na área útil, cruza o
    // retângulo encolhido.
    public int MargemDoAlvo { get; init; } = 20;

    // Os testes trocam.
    public Func<Item, DadosDoItem> TabelaDeItens { get; init; } = TabelaDoTamagotchi.DoItem;

    // Os testes trocam (durações curtas, picos longos etc.).
    public Func<Onda, DadosDaOnda> TabelaDeOndas { get; init; } = TabelaDoTamagotchi.DaOnda;

    // Chance de ficar paranoico, sorteada uma vez por episódio de mistura com
    // droga sintética (no uso que fecha a mistura, se a paranoia não estiver na
    // frente), com o gerador próprio da paranoia. Não importa quantas
    // substâncias ele use. Regra de desenho animado. Nos testes, 1 em 1 sempre
    // sai e 0 em 1 nunca.
    public Chance ChanceDaParanoia { get; init; } = new(1, 8);

    // Item fora da edição não nasce, não é marcado e sai das preferências.
    public ConjuntoDeItens ItensDaEdicao { get; init; } = TabelaDoTamagotchi.ItensDaEdicao(EdicaoDoBuzzy.Completa);

    // 96, 128 ou 192 DIP (1,5×, 2× ou 3× a arte de 64 px, sem suavizar).
    // Valor fora do enum cai no Médio.
    public static TamanhoDip TamanhoDoPersonagem(EscalaDoPersonagem escala) => escala switch
    {
        EscalaDoPersonagem.Pequena => new TamanhoDip(96, 96),
        EscalaDoPersonagem.Grande => new TamanhoDip(192, 192),
        _ => new TamanhoDip(128, 128),
    };

    // Os itens continuam com 48 DIP em qualquer escala.
    public static ConfiguracaoDoNucleo DoAplicativo(EscalaDoPersonagem escala, EdicaoDoBuzzy edicao = EdicaoDoBuzzy.Completa)
        => DoAplicativo(TamanhoDoPersonagem(escala), edicao);

    // Fonte única da configuração do app: as simulações dos testes que
    // escolhem sementes partem daqui também, pra nunca divergirem.
    public static ConfiguracaoDoNucleo DoAplicativo(TamanhoDip tamanho, EdicaoDoBuzzy edicao = EdicaoDoBuzzy.Completa) => new()
    {
        Tamanho = tamanho,
        // A pública não tem as drogas ilícitas nem o baseado por conta própria.
        ItensDaEdicao = TabelaDoTamagotchi.ItensDaEdicao(edicao),
        // O baseado por conta própria fica fora de Todas e só existe com o
        // tamagotchi ligado.
        Acoes = AcoesAutonomas.Todas | AcoesAutonomas.IrAoOutroMonitor
            | (edicao == EdicaoDoBuzzy.Completa ? AcoesAutonomas.UsarPorContaPropria : 0),
        QuedaFisica = true,
        Movimento = true,
        EsconderijoNoCliqueDuplo = true,
        // Sem itens e sem o baseado por conta própria, fica tudo igual a com
        // a chave desligada.
        Tamagotchi = true,
        Travessia = true,
        PuloDaTelaCheia = true,
        // O painel abre pelo menu.
        PainelDeEnergiaDisponivel = true,
        ConfiguracoesDisponiveis = true,
        // Desligar as duas é a reversão.
        Curiosidade = true,
        Personalidade = true,
    };
}

// Pesos e tempos da agenda autônoma por nível de energia. Muda frequência e
// duração, nunca a física nem as regras de apoio e segurança. Valores iniciais.
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
    // Distâncias e alturas em DIP.
    public int DistanciaAndandoMinima { get; init; } = 150;

    public int DistanciaAndandoMaxima { get; init; } = 500;

    public int DistanciaDoPuloMinima { get; init; } = 80;

    public int DistanciaDoPuloMaxima { get; init; } = 200;

    public int AlturaDoPuloMinima { get; init; } = 50;

    public int AlturaDoPuloMaxima { get; init; } = 100;

    // Até a agenda decidir saltar ou soltar.
    public TimeSpan TempoNaParedeMinimo { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan TempoNaParedeMaximo { get; init; } = TimeSpan.FromSeconds(35);

    // "Por pouco tempo".
    public TimeSpan TempoPenduradoMinimo { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan TempoPenduradoMaximo { get; init; } = TimeSpan.FromSeconds(8);

    // Em quantas subidas, de cada 100, ele dispara parede acima num foguete de
    // borracha. Só a frequência muda por nível; a velocidade é a mesma.
    public int ChanceDoFoguete { get; init; } = 30;

    // Peso de fumar um baseado por conta própria, o último do sorteio de IDLE.
    // Só vale com o tamagotchi ligado, em IDLE no chão e fora do esconderijo;
    // é zero com Chapado ou paranoia na frente, pra não emendar um no outro.
    // Calibrado por simulação pra dar uns 4 min entre baseados de tempo
    // elegível na energia Média. Número de desenho animado.
    public int PesoUsarPorContaPropria { get; init; } = 1;

    // Peso de atravessar pro monitor vizinho ao chegar andando numa porta
    // plana, contra parar, escalar ou virar. Valores iniciais.
    public int PesoAtravessar { get; init; } = 5;

    // Peso, no sorteio de IDLE, de andar até a porta plana e atravessar.
    public int PesoIrAoOutroMonitor { get; init; } = 2;

    // Peso de cada cara de humor na troca automática, na ordem de
    // Expressoes.DeHumor, num sorteio que nunca repete a cara atual. Nulo =
    // todas iguais, com o sorteio antigo (é o da Média; mantém as referências
    // gravadas).
    public IReadOnlyList<int>? PesosDasCaras { get; init; }

    // Daqui pra baixo o padrão é o da Média; Baixa e Alta trocam. Valores
    // iniciais, a calibrar.

    // Tempo com o primeiro plano no monitor dele até chegar perto da janela.
    public TimeSpan TempoParaAproximar { get; init; } = TimeSpan.FromSeconds(90);

    // Em decisões da agenda.
    public int RodadasOlhandoMinimo { get; init; } = 2;

    public int RodadasOlhandoMaximo { get; init; } = 4;

    // Ordem: ficar olhando, olhar ao redor, coçar-se, perder o interesse.
    public IReadOnlyList<int> PesosOlhando { get; init; } = [4, 2, 1, 1];

    // Espera depois de perder o interesse antes de chegar perto de novo.
    public TimeSpan IntervaloEntreCuriosidades { get; init; } = TimeSpan.FromMinutes(5);

    // Depois de o usuário soltar, mostrar ou redefinir a posição, a
    // curiosidade fica de fora por esse tempo.
    public TimeSpan EsperaDepoisDoUsuario { get; init; } = TimeSpan.FromMinutes(8);

    // Em passos, com a personalidade ligada; sem ela vale a da configuração.
    public int PassosDaReacao { get; init; } = 36;

    // Na ordem do enum: espiar, olhar ao redor, coçar-se, espreguiçar-se,
    // brincar. Nulo = sorteio uniforme.
    public IReadOnlyList<int>? PesosDosGestos { get; init; }

    // Uma caminhada em cada N vai à lateral mais próxima e espia pra fora.
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
