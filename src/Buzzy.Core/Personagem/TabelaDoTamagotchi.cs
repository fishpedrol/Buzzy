namespace Buzzy.Core.Personagem;

// Só dados: itens na ordem do menu, verbo e duração do uso, classe de cada
// item (alívio, substância, sintética) e as ondas com precedência, tempos,
// caras e perfis por fase. A paranoia é a última onda e nenhum item começa
// ela. Tudo de desenho animado: números e classes são regra de jogo pra ler
// bem na tela, sem relação com nada real. Os testes podem trocar as tabelas.
public static class TabelaDoTamagotchi
{
    // Completa: os treze. Pública (download do site): sem as seis ilícitas, e
    // com elas somem o baseado por conta própria e a paranoia (que precisa de
    // uma sintética).
    public static ConjuntoDeItens ItensDaEdicao(EdicaoDoBuzzy edicao)
    {
        ConjuntoDeItens itens = ConjuntoDeItens.Vazio;
        foreach (Item item in Itens)
            if (edicao == EdicaoDoBuzzy.Completa || !Ilicitos.Contem(item)) itens = itens.Com(item);
        return itens;
    }

    // Fora da edição pública, e as únicas que ele pode usar por conta própria.
    public static ConjuntoDeItens Ilicitos { get; } = ConjuntoDeItens.Vazio
        .Com(Item.Baseado).Com(Item.Cocaina).Com(Item.Md).Com(Item.LancaPerfume).Com(Item.Cogumelo).Com(Item.Bala);

    // Ordem do menu.
    public static IReadOnlyList<Item> Itens { get; } =
    [
        Item.Banana, Item.Agua, Item.Vodka, Item.Cerveja, Item.Baseado, Item.Cigarro, Item.Cocaina,
        Item.Md, Item.LancaPerfume, Item.Cafe, Item.Energetico, Item.Cogumelo, Item.Bala,
    ];

    // Em passos (60/s). Tem que bater com a soma dos quadros da animação do
    // verbo na arte.
    public static int PassosDoUso(VerboDeUso verbo) => verbo switch
    {
        VerboDeUso.Comer => 150,
        VerboDeUso.Beber => 120,
        VerboDeUso.Fumar => 210,
        VerboDeUso.Cheirar => 120,
        VerboDeUso.Engolir => 90,
        VerboDeUso.Inalar => 120,
        _ => throw new ArgumentOutOfRangeException(nameof(verbo), verbo, "Verbo de uso desconhecido."),
    };

    // Fora do enum, lança.
    public static DadosDoItem DoItem(Item item)
        => (uint)item < (uint)DadosDosItens.Length
            ? DadosDosItens[(int)item]
            : throw new ArgumentOutOfRangeException(nameof(item), item, "Item desconhecido.");

    // Adulto = tudo que não é alívio (banana, água, café, energético). Com a
    // chave desligada, o menu esconde e o núcleo recusa. Fora do enum, lança.
    public static bool Adulto(Item item) => !DoItem(item).Alivio;

    // Fora do enum, lança.
    public static DadosDaOnda DaOnda(Onda onda)
        => (uint)onda < (uint)DadosDasOndas.Length
            ? DadosDasOndas[(int)onda]
            : throw new ArgumentOutOfRangeException(nameof(onda), onda, "Onda desconhecida.");

    // Alívio = comida e bebida sem álcool. Sintética = bala, MD, coca e lança.
    private enum Classe
    {
        Alivio,
        Substancia,
        Sintetica,
    }

    // Na ordem do enum: verbo, cara durante o uso, onda, intensidade, classe.
    // A bala começa o eufórico no nível 1 (o MD, no 2).
    private static readonly DadosDoItem[] DadosDosItens =
    [
        DeItem(Item.Banana, VerboDeUso.Comer, Expressao.Feliz, Onda.Satisfeito, 1, Classe.Alivio),
        DeItem(Item.Agua, VerboDeUso.Beber, Expressao.Feliz, null, 0, Classe.Alivio),
        DeItem(Item.Vodka, VerboDeUso.Beber, Expressao.Determinado, Onda.Bebado, 2, Classe.Substancia),
        DeItem(Item.Cerveja, VerboDeUso.Beber, Expressao.Feliz, Onda.Bebado, 1, Classe.Substancia),
        DeItem(Item.Baseado, VerboDeUso.Fumar, Expressao.Pensativo, Onda.Chapado, 2, Classe.Substancia),
        DeItem(Item.Cigarro, VerboDeUso.Fumar, Expressao.Pensativo, Onda.Relaxado, 1, Classe.Substancia),
        DeItem(Item.Cocaina, VerboDeUso.Cheirar, Expressao.Surpreso, Onda.Eletrico, 2, Classe.Sintetica),
        DeItem(Item.Md, VerboDeUso.Engolir, Expressao.Travesso, Onda.Euforico, 2, Classe.Sintetica),
        DeItem(Item.LancaPerfume, VerboDeUso.Inalar, Expressao.Surpreso, Onda.Tonto, 2, Classe.Sintetica),
        DeItem(Item.Cafe, VerboDeUso.Beber, Expressao.Determinado, Onda.Ligado, 1, Classe.Alivio),
        DeItem(Item.Energetico, VerboDeUso.Beber, Expressao.Empolgado, Onda.Ligado, 2, Classe.Alivio),
        DeItem(Item.Cogumelo, VerboDeUso.Comer, Expressao.Curioso, Onda.Viajando, 2, Classe.Substancia),
        DeItem(Item.Bala, VerboDeUso.Engolir, Expressao.Feliz, Onda.Euforico, 1, Classe.Sintetica),
    ];

    // Na ordem do enum.
    private static readonly DadosDaOnda[] DadosDasOndas =
    [
        Satisfeito(), Alegre(), Relaxado(), Ligado(), Bebado(), Chapado(), Eletrico(), Euforico(), Tonto(), Viajando(), Paranoico(),
    ];

    private static DadosDoItem DeItem(Item item, VerboDeUso verbo, Expressao caraDurante, Onda? onda, int intensidade, Classe classe)
        => new(item, verbo, PassosDoUso(verbo), caraDurante, onda, intensidade, Alivio: classe == Classe.Alivio, Sintetica: classe == Classe.Sintetica);

    private static TimeSpan S(int segundos) => TimeSpan.FromSeconds(segundos);

    // Atalho pra montar um perfil de fase. Foguete nulo = o do perfil de energia.
    private static PerfilDaOnda P(
        int intervalo, int descanso, int andar, int escalar, int pular, int descansar, int gesticular, int trocarCara,
        int velocidade, int cambaleio, int? foguete, int alturaDoPulo, (Gesto, int)[] gestos, (Expressao, int)[] caras)
        => new(intervalo, descanso, andar, escalar, pular, descansar, gesticular, trocarCara, velocidade, cambaleio, foguete, alturaDoPulo, gestos, caras);

    private static DadosDaOnda Satisfeito()
    {
        PerfilDaOnda pico = P(100, 100, 100, 100, 100, 150, 150, 150, 100, 0, null, 100,
            [(Gesto.Cocar, 2), (Gesto.Espreguicar, 2), (Gesto.Brincar, 1)],
            [(Expressao.Feliz, 4), (Expressao.Rindo, 1), (Expressao.Travesso, 1), (Expressao.Sonolento, 1)]);
        return new(Onda.Satisfeito, 1, S(3), S(60), TimeSpan.Zero, Expressao.Feliz, Expressao.Feliz, null, [pico, pico, pico], null, DeSubstancia: false);
    }

    // Nenhum item começa mais o alegre, mas ele fica pra não mexer nos ordinais.
    private static DadosDaOnda Alegre()
    {
        (Gesto, int)[] gestos = [(Gesto.Brincar, 2), (Gesto.Danca, 2), (Gesto.Gargalhada, 1)];
        (Expressao, int)[] caras = [(Expressao.Empolgado, 3), (Expressao.Rindo, 2), (Expressao.Feliz, 2)];
        PerfilDaOnda Pico(int intervalo, int velocidade) => P(intervalo, 60, 130, 130, 200, 30, 150, 100, velocidade, 0, null, 120, gestos, caras);
        PerfilDaOnda queda = P(120, 150, 80, 60, 50, 200, 80, 100, 90, 0, null, 100,
            [(Gesto.Espreguicar, 1)],
            [(Expressao.Entediado, 2), (Expressao.Sonolento, 2)]);
        return new(Onda.Alegre, 1, S(2), S(40), S(20), Expressao.Empolgado, Expressao.Empolgado, Expressao.Entediado,
            [Pico(60, 120), Pico(50, 125), Pico(40, 130)], queda, DeSubstancia: false);
    }

    private static DadosDaOnda Relaxado()
    {
        PerfilDaOnda pico = P(130, 120, 70, 50, 30, 150, 120, 100, 90, 0, null, 100,
            [(Gesto.Espreguicar, 2), (Gesto.Tosse, 1), (Gesto.OlharAoRedor, 1)],
            [(Expressao.Pensativo, 2), (Expressao.Neutro, 2), (Expressao.Sonolento, 1), (Expressao.Feliz, 1)]);
        return new(Onda.Relaxado, 1, S(3), S(60), TimeSpan.Zero, Expressao.Pensativo, Expressao.Pensativo, null, [pico, pico, pico], null, DeSubstancia: true);
    }

    private static DadosDaOnda Ligado()
    {
        (Gesto, int)[] gestos = [(Gesto.Tremedeira, 2), (Gesto.OlharAoRedor, 2), (Gesto.Brincar, 1)];
        (Expressao, int)[] caras = [(Expressao.Determinado, 2), (Expressao.Empolgado, 2), (Expressao.Surpreso, 1)];
        PerfilDaOnda Pico(int intervalo, int descanso, int pular, int descansar, int velocidade, int foguete, int alturaDoPulo)
            => P(intervalo, descanso, 150, 150, pular, descansar, 120, 100, velocidade, 0, foguete, alturaDoPulo, gestos, caras);
        PerfilDaOnda queda = P(130, 150, 70, 50, 40, 200, 80, 100, 85, 0, null, 100,
            [(Gesto.Espreguicar, 2), (Gesto.Cocar, 1)],
            [(Expressao.Sonolento, 3), (Expressao.Bocejando, 1)]);
        return new(Onda.Ligado, 2, S(5), S(75), S(45), Expressao.Surpreso, Expressao.Determinado, Expressao.Sonolento,
            [Pico(70, 60, 150, 30, 115, 40, 110), Pico(55, 45, 200, 15, 130, 50, 125), Pico(40, 30, 250, 5, 145, 60, 140)], queda, DeSubstancia: false);
    }

    private static DadosDaOnda Bebado()
    {
        (Gesto, int)[] gestos = [(Gesto.Soluco, 3), (Gesto.Danca, 1), (Gesto.Gargalhada, 1)];
        (Expressao, int)[] caras = [(Expressao.Bebado, 4), (Expressao.Rindo, 2), (Expressao.Feliz, 1), (Expressao.Sonolento, 1)];
        PerfilDaOnda Pico(int velocidade, int cambaleio) => P(100, 120, 130, 40, 40, 120, 200, 150, velocidade, cambaleio, 5, 80, gestos, caras);
        PerfilDaOnda queda = P(150, 200, 60, 20, 10, 250, 80, 80, 75, 30, 5, 100,
            [(Gesto.Soluco, 1), (Gesto.Espreguicar, 1)],
            [(Expressao.Enjoado, 3), (Expressao.Sonolento, 2), (Expressao.Entediado, 1)]);
        return new(Onda.Bebado, 3, S(8), S(100), S(90), Expressao.Feliz, Expressao.Bebado, Expressao.Enjoado,
            [Pico(80, 60), Pico(70, 90), Pico(60, 120)], queda, DeSubstancia: true);
    }

    private static DadosDaOnda Chapado()
    {
        (Gesto, int)[] gestos = [(Gesto.Gargalhada, 3), (Gesto.OlharAoRedor, 1), (Gesto.Cocar, 1)];
        (Expressao, int)[] caras = [(Expressao.Chapado, 4), (Expressao.Rindo, 2), (Expressao.Pensativo, 1), (Expressao.Sonolento, 1)];
        PerfilDaOnda Pico(int velocidade) => P(160, 150, 60, 30, 20, 200, 150, 120, velocidade, 0, 0, 80, gestos, caras);
        PerfilDaOnda queda = P(150, 200, 60, 30, 20, 300, 80, 100, 70, 0, 0, 100,
            [(Gesto.Espreguicar, 2)],
            [(Expressao.Sonolento, 3), (Expressao.Bocejando, 2), (Expressao.Pensativo, 1)]);
        return new(Onda.Chapado, 3, S(10), S(110), S(90), Expressao.Pensativo, Expressao.Chapado, Expressao.Sonolento,
            [Pico(60), Pico(55), Pico(50)], queda, DeSubstancia: true);
    }

    private static DadosDaOnda Eletrico()
    {
        (Gesto, int)[] gestos = [(Gesto.Tremedeira, 3), (Gesto.Espirro, 1), (Gesto.OlharAoRedor, 1)];
        (Expressao, int)[] caras = [(Expressao.Eletrico, 4), (Expressao.Determinado, 1), (Expressao.Surpreso, 1), (Expressao.Empolgado, 1)];
        PerfilDaOnda Pico(int intervalo, int descanso, int descansar, int velocidade, int foguete, int alturaDoPulo)
            => P(intervalo, descanso, 200, 200, 180, descansar, 150, 200, velocidade, 0, foguete, alturaDoPulo, gestos, caras);
        PerfilDaOnda queda = P(150, 180, 60, 40, 30, 250, 80, 100, 80, 0, null, 100,
            [(Gesto.Espreguicar, 1), (Gesto.Cocar, 1)],
            [(Expressao.Entediado, 3), (Expressao.Sonolento, 2), (Expressao.Pensativo, 1)]);
        return new(Onda.Eletrico, 3, S(3), S(75), S(90), Expressao.Surpreso, Expressao.Eletrico, Expressao.Entediado,
            [Pico(35, 30, 10, 170, 60, 120), Pico(28, 20, 5, 185, 70, 130), Pico(20, 10, 5, 200, 80, 140)], queda, DeSubstancia: true);
    }

    private static DadosDaOnda Euforico()
    {
        PerfilDaOnda pico = P(60, 50, 120, 100, 150, 30, 250, 150, 120, 0, null, 120,
            [(Gesto.Danca, 4), (Gesto.Brincar, 1)],
            [(Expressao.Apaixonado, 3), (Expressao.Empolgado, 2), (Expressao.Feliz, 1), (Expressao.Rindo, 1)]);
        PerfilDaOnda queda = P(140, 150, 70, 60, 50, 200, 80, 100, 85, 0, null, 100,
            [(Gesto.Espreguicar, 1), (Gesto.OlharAoRedor, 1)],
            [(Expressao.Entediado, 2), (Expressao.Pensativo, 2), (Expressao.Sonolento, 1)]);
        return new(Onda.Euforico, 3, S(15), S(110), S(120), Expressao.Feliz, Expressao.Apaixonado, Expressao.Entediado, [pico, pico, pico], queda, DeSubstancia: true);
    }

    private static DadosDaOnda Tonto()
    {
        PerfilDaOnda pico = P(50, 100, 50, 0, 0, 100, 300, 200, 60, 100, 0, 100,
            [(Gesto.Gargalhada, 2), (Gesto.OlharAoRedor, 1)],
            [(Expressao.Tonto, 4), (Expressao.Rindo, 2)]);
        PerfilDaOnda queda = P(100, 100, 80, 50, 50, 120, 80, 100, 80, 0, null, 100,
            [(Gesto.OlharAoRedor, 1)],
            [(Expressao.Sonolento, 1), (Expressao.Surpreso, 1), (Expressao.Neutro, 1)]);
        return new(Onda.Tonto, 3, S(1), S(15), S(10), Expressao.Surpreso, Expressao.Tonto, Expressao.Sonolento, [pico, pico, pico], queda, DeSubstancia: true);
    }

    private static DadosDaOnda Viajando()
    {
        PerfilDaOnda pico = P(130, 120, 80, 80, 60, 100, 200, 250, 70, 0, 20, 100,
            [(Gesto.OlharAoRedor, 2), (Gesto.Danca, 1), (Gesto.Espiar, 1)],
            [(Expressao.Viajando, 4), (Expressao.Surpreso, 1), (Expressao.Pensativo, 1), (Expressao.Curioso, 1), (Expressao.Rindo, 1)]);
        PerfilDaOnda queda = P(120, 120, 80, 70, 60, 150, 100, 150, 85, 0, null, 100,
            [(Gesto.OlharAoRedor, 2), (Gesto.Espiar, 1)],
            [(Expressao.Pensativo, 3), (Expressao.Curioso, 1), (Expressao.Sonolento, 1)]);
        return new(Onda.Viajando, 3, S(20), S(140), S(60), Expressao.Curioso, Expressao.Viajando, Expressao.Pensativo, [pico, pico, pico], queda, DeSubstancia: true);
    }

    // "Tem alguém no teto." Precedência 4, a maior de todas, e conta como
    // substância (comer ou beber sem álcool acalma um passo). No pico, igual nos
    // três níveis: quieto e desconfiado, decide mais vezes, gesticula muito,
    // anda devagar e nunca escala, pula, descansa nem solta foguete. Na queda,
    // o cansaço depois do susto.
    private static DadosDaOnda Paranoico()
    {
        PerfilDaOnda pico = P(60, 30, 50, 0, 0, 0, 300, 150, 70, 0, 0, 100,
            [(Gesto.OlharProTeto, 4), (Gesto.Agachar, 3), (Gesto.Tremedeira, 2), (Gesto.OlharAoRedor, 2), (Gesto.Espiar, 1)],
            [(Expressao.Paranoico, 5), (Expressao.Assustado, 2), (Expressao.Surpreso, 1)]);
        PerfilDaOnda queda = P(120, 150, 80, 50, 50, 150, 100, 100, 85, 0, null, 100,
            [(Gesto.OlharAoRedor, 2), (Gesto.Espreguicar, 1)],
            [(Expressao.Sonolento, 2), (Expressao.Pensativo, 1), (Expressao.Neutro, 1)]);
        return new(Onda.Paranoico, 4, S(1), S(40), S(15), Expressao.Assustado, Expressao.Paranoico, Expressao.Sonolento, [pico, pico, pico], queda, DeSubstancia: true);
    }
}

// Pública é a do download do site.
public enum EdicaoDoBuzzy
{
    Completa,
    Publica,
}
