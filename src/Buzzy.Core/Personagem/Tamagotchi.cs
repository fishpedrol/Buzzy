using System.Globalization;
using System.Numerics;

namespace Buzzy.Core.Personagem;

/// <summary>
/// Itens do tamagotchi adulto (DEC-028), na ordem da resposta do usuário e do menu. Só nomes: o que cada um faz é de
/// desenho animado (<see cref="TabelaDoTamagotchi"/>). A chave da arte é o nome em minúsculas, como nas expressões.
/// </summary>
public enum Item
{
    Banana,
    Agua,
    Vodka,
    Cerveja,
    Baseado,
    Cigarro,
    Cocaina,
    Md,
    LancaPerfume,
    Cafe,
    Energetico,
    Cogumelo,
    Bala,
}

/// <summary>Como ele usa um item: cada verbo tem a sua animação, com a duração de <see cref="TabelaDoTamagotchi.PassosDoUso"/>.</summary>
public enum VerboDeUso
{
    Comer,
    Beber,
    Fumar,
    Cheirar,
    Engolir,
    Inalar,
}

/// <summary>
/// A onda de desenho animado que um item começa (DEC-028): sobe, fica no pico por níveis e cai, mudando pesos,
/// intervalos, gestos, caras e as velocidades de andar, escalar e pendurar. O nome não é "efeito" porque
/// <see cref="Efeito"/> é o pedido do núcleo à raiz.
/// </summary>
public enum Onda
{
    Satisfeito,

    /// <summary>Sem item desde 2026-10-01 (a bala, droga sintética, passou ao eufórico); fica no enum e na tabela, no mesmo lugar.</summary>
    Alegre,
    Relaxado,
    Ligado,
    Bebado,
    Chapado,
    Eletrico,
    Euforico,
    Tonto,
    Viajando,

    /// <summary>
    /// A paranoia (pedidos do usuário de 2026-10-01), de desenho animado: "tem alguém no teto". Nenhum item a começa; ela vem
    /// do sorteio de um episódio de mistura de substâncias com droga sintética, um só por episódio, com a chance de 1 em 8
    /// (<see cref="CargaDaParanoia.MisturaComSintetica"/>), com a maior precedência de todas.
    /// </summary>
    Paranoico,
}

/// <summary>Fase da onda. Cada fase, e cada nível do pico, dura um disparo único do temporizador da onda.</summary>
public enum FaseDaOnda
{
    Subida,
    Pico,
    Queda,
}

/// <summary>
/// O que a tabela diz de um item (desenho do núcleo, 4.1).
/// </summary>
/// <param name="Item">O item.</param>
/// <param name="Verbo">Como ele o usa.</param>
/// <param name="PassosDoUso">Duração do uso, em passos do relógio: a do verbo, igual à soma dos quadros da animação dele.</param>
/// <param name="CaraDurante">A cara, de humor, nos apoios em que a animação de uso não tem a própria.</param>
/// <param name="Onda">
/// A onda que o item começa, salvo no alívio (com uma onda de substância na frente, a comida e a bebida sem álcool só a
/// acalmam, sem começar a delas); nula na água, que só alivia a onda que houver.
/// </param>
/// <param name="Intensidade">Quantos níveis o item soma à onda: 1 ou 2, e 0 na água. É ponto de jogo.</param>
/// <param name="Alivio">
/// Se o item é de alívio, comida ou bebida sem álcool (o pedido do usuário de 2026-10-01): a banana, a água, o café e o
/// energético. Comer e beber acalmam a onda de desenho animado aos poucos, um passo por item
/// (<see cref="DadosDaOnda.DeSubstancia"/>). Os outros itens, inclusive o cogumelo, que também se come, e a bala, que é
/// droga sintética, são de substância e combinam as ondas como sempre.
/// </param>
/// <param name="Sintetica">
/// Se o item é droga sintética (o pedido do usuário de 2026-10-01: "como bala, md, coca e lança"): a bala, o MD, a cocaína e
/// o lança-perfume, todos de substância. É regra de jogo, de desenho animado: só um episódio que mistura substâncias com
/// pelo menos uma delas sorteia a paranoia (<see cref="CargaDaParanoia.MisturaComSintetica"/>).
/// </param>
public sealed record DadosDoItem(Item Item, VerboDeUso Verbo, int PassosDoUso, Expressao CaraDurante, Onda? Onda, int Intensidade, bool Alivio, bool Sintetica);

/// <summary>
/// O perfil de uma fase da onda (desenho do núcleo, 4.3 e 4.4): percentuais sobre o perfil de energia (100 = igual),
/// aplicados por <see cref="Maquina.PerfilEfetivo"/> e <see cref="Maquina.FisicaEfetiva"/>, e os gestos e as caras que
/// a agenda sorteia na fase, com os pesos.
/// </summary>
/// <param name="Intervalo">Sobre o intervalo entre decisões.</param>
/// <param name="Descanso">Sobre a duração do descanso.</param>
/// <param name="Andar">Sobre o peso de andar.</param>
/// <param name="Escalar">Sobre o peso de escalar.</param>
/// <param name="Pular">Sobre o peso de pular.</param>
/// <param name="Descansar">Sobre o peso de descansar.</param>
/// <param name="Gesticular">Sobre o peso de fazer um gesto.</param>
/// <param name="TrocarCara">Sobre o peso de trocar de cara.</param>
/// <param name="Velocidade">Sobre as velocidades de andar, escalar e pendurar, de 50 a 200% (exceção ao invariante 12).</param>
/// <param name="Cambaleio">Amplitude do cambaleio da caminhada, em % do passo; 0 anda reto.</param>
/// <param name="ChanceDoFoguete">Foguetes de cada 100 subidas; nula, a do perfil de energia.</param>
/// <param name="AlturaDoPulo">Sobre a altura do pulo.</param>
/// <param name="Gestos">Os gestos que a agenda sorteia nesta fase, com os pesos.</param>
/// <param name="Caras">As caras que a agenda sorteia nesta fase, com os pesos.</param>
public sealed record PerfilDaOnda(
    int Intervalo,
    int Descanso,
    int Andar,
    int Escalar,
    int Pular,
    int Descansar,
    int Gesticular,
    int TrocarCara,
    int Velocidade,
    int Cambaleio,
    int? ChanceDoFoguete,
    int AlturaDoPulo,
    IReadOnlyList<(Gesto Gesto, int Peso)> Gestos,
    IReadOnlyList<(Expressao Cara, int Peso)> Caras);

/// <summary>
/// O que a tabela diz de uma onda (desenho do núcleo, 4.2 a 4.4): a precedência, os tempos de cada fase, a cara de
/// cada fase e os perfis.
/// </summary>
/// <param name="Onda">A onda.</param>
/// <param name="Precedencia">
/// De 1 a 4: uma onda de precedência maior ou igual vai para a frente da que houver. A 4 é só a da paranoia, maior que a
/// de todas as ondas dos itens, que vão de 1 a 3.
/// </param>
/// <param name="Subida">Duração da subida.</param>
/// <param name="NivelDoPico">Duração de cada nível do pico: a cada disparo, o nível cai um, até o 1.</param>
/// <param name="QuedaBase">Duração da queda com o pior nível 1; <see cref="TimeSpan.Zero"/> sem queda.</param>
/// <param name="CaraDaSubida">A cara da subida (de humor).</param>
/// <param name="CaraDoPico">A cara do pico.</param>
/// <param name="CaraDaQueda">A cara da queda; nula, a do pico.</param>
/// <param name="PicoPorNivel">O perfil do pico nos níveis 1, 2 e 3.</param>
/// <param name="Queda">O perfil da queda, igual em todos os níveis; nulo sem queda (a onda acaba no fim do pico).</param>
/// <param name="DeSubstancia">
/// Se é uma onda de substância, que um item de alívio acalma um passo, sem começar a onda dele; senão, é uma onda
/// leve, e só a água a acalma: os outros itens de alívio a combinam como sempre.
/// </param>
public sealed record DadosDaOnda(
    Onda Onda,
    int Precedencia,
    TimeSpan Subida,
    TimeSpan NivelDoPico,
    TimeSpan QuedaBase,
    Expressao CaraDaSubida,
    Expressao CaraDoPico,
    Expressao? CaraDaQueda,
    IReadOnlyList<PerfilDaOnda> PicoPorNivel,
    PerfilDaOnda? Queda,
    bool DeSubstancia)
{
    /// <summary>Nenhuma fase dura menos que isto: o temporizador da onda só faz disparos únicos de 1 s ou mais.</summary>
    public static readonly TimeSpan DuracaoMinima = TimeSpan.FromSeconds(1);

    /// <summary>
    /// O perfil da fase: na subida, o do nível 1 do pico, com a cara da subida só; no pico, o do nível; na queda, o da
    /// queda, que não depende do nível.
    /// </summary>
    public PerfilDaOnda Perfil(FaseDaOnda fase, int nivel)
    {
        ValidarNivel(nivel, nameof(nivel));
        return fase switch
        {
            FaseDaOnda.Subida => PicoPorNivel[0] with { Caras = [(CaraDaSubida, 1)] },
            FaseDaOnda.Pico => PicoPorNivel[nivel - 1],
            FaseDaOnda.Queda => Queda ?? throw new InvalidOperationException($"A onda {Onda} não tem queda."),
            _ => throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase da onda desconhecida."),
        };
    }

    /// <summary>
    /// Quanto dura a fase: a subida e cada nível do pico, fixos; a queda, a base × 100, 125 ou 150% pelo pior nível
    /// atingido (1, 2 ou 3). Nunca menos que <see cref="DuracaoMinima"/>.
    /// </summary>
    public TimeSpan Duracao(FaseDaOnda fase, int pior)
    {
        ValidarNivel(pior, nameof(pior));
        TimeSpan duracao = fase switch
        {
            FaseDaOnda.Subida => Subida,
            FaseDaOnda.Pico => NivelDoPico,
            FaseDaOnda.Queda when Queda is null => throw new InvalidOperationException($"A onda {Onda} não tem queda."),
            FaseDaOnda.Queda => TimeSpan.FromTicks(QuedaBase.Ticks * (75 + 25 * pior) / 100),
            _ => throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase da onda desconhecida."),
        };
        return duracao < DuracaoMinima ? DuracaoMinima : duracao;
    }

    /// <summary>A cara da fase: a da subida, a do pico ou a da queda (sem cara própria na queda, a do pico).</summary>
    public Expressao Cara(FaseDaOnda fase) => fase switch
    {
        FaseDaOnda.Subida => CaraDaSubida,
        FaseDaOnda.Pico => CaraDoPico,
        FaseDaOnda.Queda => CaraDaQueda ?? CaraDoPico,
        _ => throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase da onda desconhecida."),
    };

    private static void ValidarNivel(int nivel, string nome)
    {
        if (nivel is < 1 or > 3) throw new ArgumentOutOfRangeException(nome, nivel, "O nível da onda vai de 1 a 3.");
    }
}

/// <summary>
/// A onda em curso, parte do estado do núcleo (só em memória; nunca é gravada).
/// </summary>
/// <param name="Tipo">Qual onda.</param>
/// <param name="Fase">Em que fase ela está.</param>
/// <param name="Nivel">O nível atual, de 1 a 3; na queda, 1.</param>
/// <param name="Pior">O maior nível atingido no episódio, que alonga a queda.</param>
public sealed record EstadoDaOnda(Onda Tipo, FaseDaOnda Fase, int Nivel, int Pior);

/// <summary>
/// Onde ele estava quando o item foi solto sobre ele (desenho do núcleo, 4.6): a pose de uso por apoio e o lugar para
/// onde ele volta no fim do uso. No chão, volta a IDLE; na parede e no cipó, agarrado, e preso se já estava (DEC-024); no
/// esconderijo, espiando na mesma borda (DEC-025).
/// </summary>
public enum ApoioDoUso
{
    Chao,
    Parede,
    Cipo,
    Esconderijo,
}

/// <summary>O que acontece com um item no mundo: caindo, parado no chão, segurado pelo usuário ou arrastado por ele.</summary>
public enum SituacaoDoItem
{
    Caindo,
    NoChao,
    Segurado,
    Arrastado,
}

/// <summary>Por que um item sai da tela (efeito <see cref="RemoverItem"/>).</summary>
public enum MotivoDaRemocao
{
    /// <summary>Solto sobre ele, que o usou.</summary>
    Usado,

    /// <summary>"Recolher itens" do menu.</summary>
    Recolhido,

    /// <summary>O sétimo item tirou o mais antigo que não estava na mão do usuário.</summary>
    Substituido,
}

/// <summary>
/// O uso em curso, em <see cref="Estado.Using"/>: o item, o verbo, quantos passos ele dura e o apoio em que acontece. O
/// que falta fica em <see cref="EstadoDoNucleo.PassosRestantes"/>.
/// </summary>
public sealed record Uso(Item Item, VerboDeUso Verbo, int Passos, ApoioDoUso Apoio)
{
    /// <summary>
    /// Se este uso começou a paranoia (pedido do usuário de 2026-10-01): ela começa no soltar, com ele já usando; se o uso
    /// vai até o fim e o devolve a IDLE sem gesto, ele olha pro teto na hora (<see cref="Gesto.OlharProTeto"/>). Um uso
    /// interrompido leva a marca junto. Fica fora do construtor posicional.
    /// </summary>
    public bool ComecouAParanoia { get; init; }
}

/// <summary>
/// A carga da paranoia num episódio (pedidos do usuário de 2026-10-01; DEC-028), parte do estado do núcleo e só em memória,
/// como a onda: quantos itens de substância ele usou, se algum era droga sintética, quais itens de substância distintos
/// foram e se o episódio já fez o sorteio da paranoia, o único dele. Cada item de substância entra depois da combinação
/// (<see cref="Com"/>); a comida e a bebida sem álcool, nunca. Tudo volta junto a <see cref="Nenhuma"/> no fim de todo
/// evento em que nem a onda da frente nem a de fundo é de substância (a paranoia conta como substância): o episódio acabou,
/// e o seguinte sorteia de novo.
/// </summary>
/// <param name="Substancias">Quantos itens de substância no episódio; o retrato e a linha canônica (<c>carga=</c>) mostram este número.</param>
/// <param name="Sintetica">Se algum deles era droga sintética (<see cref="DadosDoItem.Sintetica"/>).</param>
/// <param name="Distintas">Os itens de substância distintos do episódio.</param>
/// <param name="Sorteada">
/// Se o episódio já fez o sorteio da paranoia (pedido do usuário de 2026-10-01, 19:00: "quero que a chance dele ficar
/// paranoico seja de 1 em 8"; e a escolha dele às 23:03, "uma vez por mistura"): o uso que fecha a mistura com sintética
/// sorteia, e, saindo ou não, o episódio não sorteia mais. Assim a chance de ele ficar paranoico num episódio é a da
/// configuração, por mais substâncias que ele use.
/// </param>
public sealed record CargaDaParanoia(int Substancias, bool Sintetica, ConjuntoDeItens Distintas, bool Sorteada)
{
    /// <summary>Sem episódio: nada usado, nada sorteado.</summary>
    public static readonly CargaDaParanoia Nenhuma = new(0, false, ConjuntoDeItens.Vazio, Sorteada: false);

    /// <summary>
    /// Quantas vezes cada item participou do episódio; só em memória (DEC-041), para desfazer exatamente um item desmarcado.
    /// É a proveniência, não a identidade da carga: fica fora da igualdade (abaixo), e o total dela bate com
    /// <see cref="Substancias"/> e os itens contados com <see cref="Distintas"/> (CargaDaParanoiaTestes).
    /// </summary>
    public ContagensDeSubstancias PorItem { get; init; }

    /// <summary>A igualdade da carga: as substâncias, a sintética, os distintos e o sorteio, sem a proveniência.</summary>
    public bool Equals(CargaDaParanoia? outra)
        => outra is not null && Substancias == outra.Substancias && Sintetica == outra.Sintetica && Distintas == outra.Distintas && Sorteada == outra.Sorteada;

    public override int GetHashCode() => HashCode.Combine(Substancias, Sintetica, Distintas, Sorteada);

    /// <summary>
    /// Se o episódio é uma mistura com droga sintética (o pedido do usuário de 2026-10-01): pelo menos uma sintética e pelo
    /// menos dois itens de substância distintos. Só ela sorteia a paranoia, uma vez por episódio; álcool, maconha, cigarro e
    /// cogumelo, sozinhos ou misturados entre si, e uma sintética sozinha, repetida, nunca.
    /// </summary>
    public bool MisturaComSintetica => Sintetica && Distintas.Quantidade >= 2;

    /// <summary>
    /// A carga com mais um item de substância: um a mais, a sintética se ele for, e ele entre os distintos; o sorteio feito
    /// continua feito.
    /// </summary>
    public CargaDaParanoia Com(DadosDoItem dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        if (dados.Alivio) throw new ArgumentException($"{dados.Item} é de alívio: não entra na carga da paranoia.", nameof(dados));
        return new CargaDaParanoia(Substancias == int.MaxValue ? int.MaxValue : Substancias + 1,
            Sintetica || dados.Sintetica, Distintas.Com(dados.Item), Sorteada)
        { PorItem = PorItem.Com(dados.Item) };
    }

    /// <summary>Recalcula o episódio depois que um item foi desmarcado, sem sortear novamente.</summary>
    public CargaDaParanoia Sem(Item item)
    {
        int removidas = PorItem.Contagem(item);
        if (removidas == 0) return this;
        ContagensDeSubstancias restantes = PorItem.Sem(item);
        if (restantes.Total == 0) return Nenhuma;
        ConjuntoDeItens distintas = Distintas.Sem(item);
        bool sintetica = distintas.Itens.Any(i => TabelaDoTamagotchi.DoItem(i).Sintetica);
        return this with
        {
            Substancias = Math.Max(0, Substancias - removidas),
            Sintetica = sintetica,
            Distintas = distintas,
            PorItem = restantes,
        };
    }
}

/// <summary>Contagem imutável por item adulto, para reconstruir uma mistura sem a origem desmarcada.</summary>
public readonly record struct ContagensDeSubstancias
{
    private int Vodka { get; init; }
    private int Cerveja { get; init; }
    private int Baseado { get; init; }
    private int Cigarro { get; init; }
    private int Cocaina { get; init; }
    private int Md { get; init; }
    private int LancaPerfume { get; init; }
    private int Cogumelo { get; init; }
    private int Bala { get; init; }

    private ContagensDeSubstancias(int vodka, int cerveja, int baseado, int cigarro, int cocaina, int md, int lancaPerfume, int cogumelo, int bala)
        => (Vodka, Cerveja, Baseado, Cigarro, Cocaina, Md, LancaPerfume, Cogumelo, Bala)
            = (vodka, cerveja, baseado, cigarro, cocaina, md, lancaPerfume, cogumelo, bala);

    /// <summary>Quantas vezes este item entrou na mistura.</summary>
    public int Contagem(Item item) => item switch
    {
        Item.Vodka => Vodka,
        Item.Cerveja => Cerveja,
        Item.Baseado => Baseado,
        Item.Cigarro => Cigarro,
        Item.Cocaina => Cocaina,
        Item.Md => Md,
        Item.LancaPerfume => LancaPerfume,
        Item.Cogumelo => Cogumelo,
        Item.Bala => Bala,
        _ => 0,
    };

    /// <summary>Soma as contagens, com saturação de inteiro.</summary>
    public int Total => (int)Math.Min(int.MaxValue, (long)Vodka + Cerveja + Baseado + Cigarro + Cocaina + Md + LancaPerfume + Cogumelo + Bala);

    /// <summary>Incrementa a ocorrência do item; valores fora dos nove itens adultos são recusados.</summary>
    public ContagensDeSubstancias Com(Item item)
    {
        static int Incrementar(int atual) => atual == int.MaxValue ? atual : atual + 1;
        return item switch
        {
            Item.Vodka => this with { Vodka = Incrementar(Vodka) },
            Item.Cerveja => this with { Cerveja = Incrementar(Cerveja) },
            Item.Baseado => this with { Baseado = Incrementar(Baseado) },
            Item.Cigarro => this with { Cigarro = Incrementar(Cigarro) },
            Item.Cocaina => this with { Cocaina = Incrementar(Cocaina) },
            Item.Md => this with { Md = Incrementar(Md) },
            Item.LancaPerfume => this with { LancaPerfume = Incrementar(LancaPerfume) },
            Item.Cogumelo => this with { Cogumelo = Incrementar(Cogumelo) },
            Item.Bala => this with { Bala = Incrementar(Bala) },
            _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Item não é uma substância adulta."),
        };
    }

    /// <summary>Zera as ocorrências do item.</summary>
    public ContagensDeSubstancias Sem(Item item) => item switch
    {
        Item.Vodka => this with { Vodka = 0 },
        Item.Cerveja => this with { Cerveja = 0 },
        Item.Baseado => this with { Baseado = 0 },
        Item.Cigarro => this with { Cigarro = 0 },
        Item.Cocaina => this with { Cocaina = 0 },
        Item.Md => this with { Md = 0 },
        Item.LancaPerfume => this with { LancaPerfume = 0 },
        Item.Cogumelo => this with { Cogumelo = 0 },
        Item.Bala => this with { Bala = 0 },
        _ => this,
    };
}

/// <summary>
/// Um conjunto de itens, imutável e com igualdade por valor, um bit por item do enum: os itens de substância distintos de um
/// episódio da paranoia (<see cref="CargaDaParanoia.Distintas"/>). O valor padrão é o vazio, e o estado do núcleo continua
/// comparável por valor.
/// </summary>
public readonly record struct ConjuntoDeItens
{
    private readonly int _bits;

    private ConjuntoDeItens(int bits) => _bits = bits;

    /// <summary>Sem nenhum item.</summary>
    public static ConjuntoDeItens Vazio => default;

    /// <summary>Quantos itens.</summary>
    public int Quantidade => BitOperations.PopCount((uint)_bits);

    /// <summary>Os itens, na ordem do enum.</summary>
    public IEnumerable<Item> Itens
    {
        get
        {
            int bits = _bits;
            return Enum.GetValues<Item>().Where(i => (bits & Bit(i)) != 0);
        }
    }

    /// <summary>Se o item está no conjunto; um valor fora do enum nunca está.</summary>
    public bool Contem(Item item) => Enum.IsDefined(item) && (_bits & Bit(item)) != 0;

    /// <summary>O conjunto com o item; o mesmo, se já estava. Um valor fora do enum lança.</summary>
    public ConjuntoDeItens Com(Item item) => new(_bits | Bit(item));

    /// <summary>O conjunto sem o item; um valor fora do enum lança.</summary>
    public ConjuntoDeItens Sem(Item item) => new(_bits & ~Bit(item));

    /// <summary>Os itens separados por vírgula, na ordem do enum: <c>Vodka,Md</c>.</summary>
    public override string ToString() => string.Join(",", Itens);

    private static int Bit(Item item)
        => Enum.IsDefined(item) && (int)item < 31 ? 1 << (int)item : throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
}

/// <summary>
/// Intensidade ainda atribuída a cada item em uma onda ativa (DEC-041). O valor é imutável e comparável; a contribuição
/// por item e a soma são limitadas a 3, igual ao teto de nível das ondas.
/// </summary>
public readonly record struct ContribuicoesDaOnda
{
    private readonly ulong _bits;

    private ContribuicoesDaOnda(ulong bits) => _bits = bits;

    /// <summary>Nenhuma origem de item.</summary>
    public static ContribuicoesDaOnda Nenhuma => default;

    /// <summary>Intensidade somada e limitada a 3.</summary>
    public int Total
    {
        get
        {
            int total = 0;
            foreach (Item item in TabelaDoTamagotchi.Itens) total += Intensidade(item);
            return Math.Min(3, total);
        }
    }

    /// <summary>Intensidade registrada para o item.</summary>
    public int Intensidade(Item item)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        return (int)((_bits >> ((int)item * 4)) & 0xFUL);
    }

    /// <summary>Soma uma contribuição, limitada a 3 por item.</summary>
    public ContribuicoesDaOnda Com(Item item, int intensidade)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        if (intensidade < 1) throw new ArgumentOutOfRangeException(nameof(intensidade));
        int deslocamento = (int)item * 4;
        ulong mascara = 0xFUL << deslocamento;
        int somada = Math.Min(3, Intensidade(item) + Math.Min(3, intensidade));
        return new((_bits & ~mascara) | ((ulong)somada << deslocamento));
    }

    /// <summary>Retira todas as contribuições desse item.</summary>
    public ContribuicoesDaOnda Sem(Item item)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        return new(_bits & ~(0xFUL << ((int)item * 4)));
    }
}

/// <summary>
/// Um item na tela (DEC-028), parte do estado do núcleo e só em memória. A âncora é a do personagem: o centro da borda de
/// baixo do sprite, em pixels físicos; o tamanho é o de <see cref="ConfiguracaoDoNucleo.TamanhoDoItem"/> no DPI do monitor.
/// </summary>
/// <param name="Id">Identificador único: cada Id é usado uma vez só, em ordem crescente.</param>
/// <param name="Item">Qual item.</param>
/// <param name="Situacao">Caindo, no chão, segurado ou arrastado.</param>
/// <param name="Lugar">Onde está a janela do item: o monitor, a âncora, o tamanho e o retângulo.</param>
/// <param name="Posicao">A mesma posição, relativa à área útil do monitor; sobrevive a mudanças de topologia.</param>
public sealed record ItemNoMundo(int Id, Item Item, SituacaoDoItem Situacao, Posicionamento Lugar, PosicaoDoPersonagem Posicao)
{
    /// <summary>Âncora fina, vertical, da queda (a janela usa a arredondada).</summary>
    public double Y { get; init; }

    /// <summary>Velocidade vertical da queda, em pixels físicos por segundo (positiva para baixo).</summary>
    public double VY { get; init; }

    /// <summary>Quiques já dados nesta queda.</summary>
    public int Quiques { get; init; }

    /// <summary>Na mão do usuário: cursor menos âncora no <c>ITEM_PRESS</c>.</summary>
    public PontoPx Pegada { get; init; }

    /// <summary>Se está na mão do usuário: segurado ou arrastado.</summary>
    public bool NaMao => Situacao is SituacaoDoItem.Segurado or SituacaoDoItem.Arrastado;
}

/// <summary>
/// Os itens na tela, imutáveis, em ordem de Id, com no máximo um na mão do usuário e igualdade por valor (como
/// <see cref="MonitoresOcupados"/>): o estado do núcleo continua comparável por valor.
/// </summary>
public sealed class ItensNoMundo : IEquatable<ItensNoMundo>
{
    public static readonly ItensNoMundo Nenhum = new([]);

    private readonly ItemNoMundo[] _itens;

    /// <summary>Os itens dados, ordenados por Id. Lança com Id repetido ou com mais de um item na mão.</summary>
    public ItensNoMundo(IEnumerable<ItemNoMundo> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);
        _itens = [.. itens.OrderBy(i => i.Id)];
        for (int i = 1; i < _itens.Length; i++)
        {
            if (_itens[i].Id == _itens[i - 1].Id) throw new ArgumentException($"Item com Id repetido: {_itens[i].Id}.", nameof(itens));
        }
        if (_itens.Count(i => i.NaMao) > 1) throw new ArgumentException("Mais de um item na mão do usuário.", nameof(itens));
    }

    public IReadOnlyList<ItemNoMundo> Todos => _itens;

    public int Quantidade => _itens.Length;

    /// <summary>O item na mão do usuário (segurado ou arrastado), se houver; no máximo um.</summary>
    public ItemNoMundo? NaMao => Array.Find(_itens, i => i.NaMao);

    /// <summary>Se algum item está caindo.</summary>
    public bool AlgumCaindo => Array.Exists(_itens, i => i.Situacao == SituacaoDoItem.Caindo);

    public ItemNoMundo? PorId(int id) => Array.Find(_itens, i => i.Id == id);

    /// <summary>Com o item acrescentado, ou no lugar do que tem o mesmo Id.</summary>
    public ItensNoMundo Com(ItemNoMundo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItensNoMundo(_itens.Where(i => i.Id != item.Id).Append(item));
    }

    /// <summary>Sem o item do Id dado (o mesmo conjunto, se não há).</summary>
    public ItensNoMundo Sem(int id) => Array.Exists(_itens, i => i.Id == id) ? new ItensNoMundo(_itens.Where(i => i.Id != id)) : this;

    public bool Equals(ItensNoMundo? outro) => outro is not null && _itens.AsSpan().SequenceEqual(outro._itens);

    public override bool Equals(object? obj) => Equals(obj as ItensNoMundo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ItemNoMundo item in _itens) hash.Add(item);
        return hash.ToHashCode();
    }

    /// <summary>Como na linha do retrato: <c>1:Banana:NoChao:(1728,1032);2:…</c>, na cultura invariante.</summary>
    public override string ToString()
        => string.Join(";", _itens.Select(i => string.Create(CultureInfo.InvariantCulture, $"{i.Id}:{i.Item}:{i.Situacao}:({i.Lugar.Ancora.X},{i.Lugar.Ancora.Y})")));
}
