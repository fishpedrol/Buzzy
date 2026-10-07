using System.Globalization;
using System.Numerics;

namespace Buzzy.Core.Personagem;

// Na ordem do menu. A chave da arte é o nome em minúsculas, como nas expressões.
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

// Cada verbo tem a sua animação.
public enum VerboDeUso
{
    Comer,
    Beber,
    Fumar,
    Cheirar,
    Engolir,
    Inalar,
}

// Sobe, fica no pico por níveis e cai. Não se chama "efeito" porque Efeito já é o pedido do núcleo à raiz.
public enum Onda
{
    Satisfeito,

    // Nenhum item usa mais (a bala passou pro eufórico), mas fica no mesmo lugar do enum e da tabela.
    Alegre,
    Relaxado,
    Ligado,
    Bebado,
    Chapado,
    Eletrico,
    Euforico,
    Tonto,
    Viajando,

    // "Tem alguém no teto". Nenhum item começa: vem do sorteio (1 em 8, uma vez por episódio) de uma
    // mistura com droga sintética. Maior precedência de todas.
    Paranoico,
}

// Cada fase, e cada nível do pico, dura um disparo do timer da onda.
public enum FaseDaOnda
{
    Subida,
    Pico,
    Queda,
}

// PassosDoUso: em passos do relógio, igual à soma dos quadros da animação do verbo.
// CaraDurante: usada nos apoios em que a animação de uso não tem cara própria.
// Onda: nula na água, que só alivia. Intensidade: 1 ou 2 (0 na água).
// Alivio: banana, água, café e energético; acalmam a onda um passo por item. O cogumelo, apesar de comido,
// é substância. Sintetica: bala, MD, cocaína e lança; só mistura com uma delas sorteia a paranoia.
public sealed record DadosDoItem(Item Item, VerboDeUso Verbo, int PassosDoUso, Expressao CaraDurante, Onda? Onda, int Intensidade, bool Alivio, bool Sintetica);

// Os números são percentuais sobre o perfil de energia (100 = igual). Velocidade vai de 50 a 200%.
// Cambaleio: amplitude em % do passo (0 = reto). ChanceDoFoguete: em 100 subidas; nula usa a do perfil.
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

// Precedencia: 1 a 3 nos itens, 4 só na paranoia; maior ou igual vai pra frente.
// QuedaBase: com pior nível 1; Zero/Queda nula = sem queda, a onda acaba no fim do pico.
// DeSubstancia: qualquer alívio acalma; onda leve só a água acalma.
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
    public static readonly TimeSpan DuracaoMinima = TimeSpan.FromSeconds(1);

    // A subida usa o perfil do nível 1 do pico, só com a cara da subida.
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

    // A queda é a base x 100, 125 ou 150% pelo pior nível (1, 2 ou 3).
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

// Só em memória, nunca é gravada. Nivel 1 a 3 (1 na queda); Pior é o maior nível do episódio e alonga a queda.
public sealed record EstadoDaOnda(Onda Tipo, FaseDaOnda Fase, int Nivel, int Pior);

// Onde ele estava ao receber o item: define a pose de uso e pra onde volta no fim.
public enum ApoioDoUso
{
    Chao,
    Parede,
    Cipo,
    Esconderijo,
}

public enum SituacaoDoItem
{
    Caindo,
    NoChao,
    Segurado,
    Arrastado,
}

public enum MotivoDaRemocao
{
    Usado,

    // "Recolher itens" do menu.
    Recolhido,

    // Um item novo passou do limite e tirou o mais antigo fora da mão.
    Substituido,
}

// O que falta do uso fica em EstadoDoNucleo.PassosRestantes.
public sealed record Uso(Item Item, VerboDeUso Verbo, int Passos, ApoioDoUso Apoio)
{
    // Se o uso terminar em IDLE sem gesto, ele olha pro teto. Fora do construtor posicional de propósito.
    public bool ComecouAParanoia { get; init; }
}

// Carga de substâncias de um episódio, só em memória. Comida e bebida sem álcool nunca entram. Zera
// inteira quando não sobra onda de substância (a paranoia conta). Substancias aparece no log como carga=.
// Sorteada: o sorteio da paranoia é um só por episódio, saia ou não, então a chance fica a da configuração
// por mais que ele use.
public sealed record CargaDaParanoia(int Substancias, bool Sintetica, ConjuntoDeItens Distintas, bool Sorteada)
{
    public static readonly CargaDaParanoia Nenhuma = new(0, false, ConjuntoDeItens.Vazio, Sorteada: false);

    // Quantas vezes cada item entrou, pra desfazer certinho um item desmarcado. Fica fora da igualdade:
    // é de onde veio a carga, não a carga em si.
    public ContagensDeSubstancias PorItem { get; init; }

    public bool Equals(CargaDaParanoia? outra)
        => outra is not null && Substancias == outra.Substancias && Sintetica == outra.Sintetica && Distintas == outra.Distintas && Sorteada == outra.Sorteada;

    public override int GetHashCode() => HashCode.Combine(Substancias, Sintetica, Distintas, Sorteada);

    // Pelo menos uma sintética e dois itens distintos. Uma sintética sozinha, mesmo repetida, não conta.
    public bool MisturaComSintetica => Sintetica && Distintas.Quantidade >= 2;

    public CargaDaParanoia Com(DadosDoItem dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        if (dados.Alivio) throw new ArgumentException($"{dados.Item} é de alívio: não entra na carga da paranoia.", nameof(dados));
        return new CargaDaParanoia(Substancias == int.MaxValue ? int.MaxValue : Substancias + 1,
            Sintetica || dados.Sintetica, Distintas.Com(dados.Item), Sorteada)
        { PorItem = PorItem.Com(dados.Item) };
    }

    // Não sorteia de novo.
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

// Contagem por item adulto, pra refazer a mistura sem um item desmarcado.
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

    // Satura em int.MaxValue.
    public int Total => (int)Math.Min(int.MaxValue, (long)Vodka + Cerveja + Baseado + Cigarro + Cocaina + Md + LancaPerfume + Cogumelo + Bala);

    // Lança pra item que não é um dos nove adultos.
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

// Um bit por item do enum, pra o estado do núcleo continuar comparável por valor. default = vazio.
public readonly record struct ConjuntoDeItens
{
    private readonly int _bits;

    private ConjuntoDeItens(int bits) => _bits = bits;

    public static ConjuntoDeItens Vazio => default;

    public int Quantidade => BitOperations.PopCount((uint)_bits);

    // Na ordem do enum.
    public IEnumerable<Item> Itens
    {
        get
        {
            int bits = _bits;
            return Enum.GetValues<Item>().Where(i => (bits & Bit(i)) != 0);
        }
    }

    // Valor fora do enum nunca está (mas Com/Sem lançam).
    public bool Contem(Item item) => Enum.IsDefined(item) && (_bits & Bit(item)) != 0;

    public ConjuntoDeItens Com(Item item) => new(_bits | Bit(item));

    public ConjuntoDeItens Sem(Item item) => new(_bits & ~Bit(item));

    // Ex.: Vodka,Md
    public override string ToString() => string.Join(",", Itens);

    private static int Bit(Item item)
        => Enum.IsDefined(item) && (int)item < 31 ? 1 << (int)item : throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
}

// Quanto cada item ainda contribui pra onda ativa, 4 bits por item. Por item e no total, teto 3, igual ao
// nível máximo da onda.
public readonly record struct ContribuicoesDaOnda
{
    private readonly ulong _bits;

    private ContribuicoesDaOnda(ulong bits) => _bits = bits;

    public static ContribuicoesDaOnda Nenhuma => default;

    public int Total
    {
        get
        {
            int total = 0;
            foreach (Item item in TabelaDoTamagotchi.Itens) total += Intensidade(item);
            return Math.Min(3, total);
        }
    }

    public int Intensidade(Item item)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        return (int)((_bits >> ((int)item * 4)) & 0xFUL);
    }

    public ContribuicoesDaOnda Com(Item item, int intensidade)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        if (intensidade < 1) throw new ArgumentOutOfRangeException(nameof(intensidade));
        int deslocamento = (int)item * 4;
        ulong mascara = 0xFUL << deslocamento;
        int somada = Math.Min(3, Intensidade(item) + Math.Min(3, intensidade));
        return new((_bits & ~mascara) | ((ulong)somada << deslocamento));
    }

    public ContribuicoesDaOnda Sem(Item item)
    {
        if (!Enum.IsDefined(item)) throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");
        return new(_bits & ~(0xFUL << ((int)item * 4)));
    }
}

// Só em memória. Âncora igual à do personagem: centro da borda de baixo do sprite, em px físicos.
// Id nunca é reusado (sempre crescente). Posicao é relativa à área útil e sobrevive à troca de topologia.
public sealed record ItemNoMundo(int Id, Item Item, SituacaoDoItem Situacao, Posicionamento Lugar, PosicaoDoPersonagem Posicao)
{
    // Y fino da queda; a janela usa o arredondado.
    public double Y { get; init; }

    // px físicos/s, positivo pra baixo.
    public double VY { get; init; }

    public int Quiques { get; init; }

    // Cursor - âncora no ITEM_PRESS.
    public PontoPx Pegada { get; init; }

    public bool NaMao => Situacao is SituacaoDoItem.Segurado or SituacaoDoItem.Arrastado;
}

// Imutável, em ordem de Id, no máximo um na mão. Igualdade por valor pro estado do núcleo continuar comparável.
public sealed class ItensNoMundo : IEquatable<ItensNoMundo>
{
    public static readonly ItensNoMundo Nenhum = new([]);

    private readonly ItemNoMundo[] _itens;

    // Lança com Id repetido ou mais de um item na mão.
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

    public ItemNoMundo? NaMao => Array.Find(_itens, i => i.NaMao);

    public bool AlgumCaindo => Array.Exists(_itens, i => i.Situacao == SituacaoDoItem.Caindo);

    public ItemNoMundo? PorId(int id) => Array.Find(_itens, i => i.Id == id);

    // Acrescenta, ou substitui o de mesmo Id.
    public ItensNoMundo Com(ItemNoMundo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItensNoMundo(_itens.Where(i => i.Id != item.Id).Append(item));
    }

    public ItensNoMundo Sem(int id) => Array.Exists(_itens, i => i.Id == id) ? new ItensNoMundo(_itens.Where(i => i.Id != id)) : this;

    public bool Equals(ItensNoMundo? outro) => outro is not null && _itens.AsSpan().SequenceEqual(outro._itens);

    public override bool Equals(object? obj) => Equals(obj as ItensNoMundo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ItemNoMundo item in _itens) hash.Add(item);
        return hash.ToHashCode();
    }

    // Formato da linha do retrato: 1:Banana:NoChao:(1728,1032);2:...
    public override string ToString()
        => string.Join(";", _itens.Select(i => string.Create(CultureInfo.InvariantCulture, $"{i.Id}:{i.Item}:{i.Situacao}:({i.Lugar.Ancora.X},{i.Lugar.Ancora.Y})")));
}
