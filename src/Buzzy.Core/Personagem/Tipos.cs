namespace Buzzy.Core.Personagem;

// Estado de comportamento. Nomes em inglês de propósito, pra bater linha a
// linha com a tabela de transições e os testes.
public enum Estado
{
    // Carrega configurações e topologia e restaura a posição.
    Booting,

    Idle,

    Walking,

    // Subindo, descendo ou parado numa parede.
    Climbing,

    // Pendurado na borda de cima.
    Hanging,

    // Trajetória balística iniciada por decisão autônoma.
    Jumping,

    Falling,

    // Transição curta depois de tocar o chão.
    Landing,

    // Sem animação contínua; o relógio fica parado.
    Resting,

    // Botão apertado sobre ele; ainda não se sabe se é clique ou arraste.
    Pressed,

    Dragging,

    // Validação logo depois de soltar, mostrar ou mudar a topologia.
    Settling,

    // Reação curta a um clique.
    Reacting,

    // Sem relógio e sem desenho; o motivo fica em MotivoDoOcultamento.
    Hidden,

    Exiting,

    // Escondido atrás de uma borda, só com cabeça e mãos pra fora. Entra e
    // sai pelo clique duplo; sem relógio; a agenda só troca a cara.
    Peeking,

    // Usando o item que soltaram nele, por um número fixo de passos, no apoio
    // em que estava (chão, parede, cipó ou esconderijo). O relógio corre, nada
    // autônomo entra e um PRESS interrompe na hora. No fim, a acomodação o
    // devolve ao mesmo apoio. Também é o estado do baseado por conta própria
    // (no chão, sem item no mundo).
    Using,
}

// Arte de 64 px ampliada sem suavizar. Vale na próxima abertura; itens
// ficam em 48 DIP.
public enum EscalaDoPersonagem
{
    // 96 DIP (1,5×).
    Pequena,

    // 128 DIP (2×), o padrão.
    Media,

    // 192 DIP (3×).
    Grande,
}

// Trecho horizontal da janela em primeiro plano, recortado à área útil do
// monitor do foco, em 16 avos da largura (0 ≤ Inicio < Fim ≤ 16). Nada
// vertical, nenhum pixel: só serve de destino pro personagem, é pedido uma
// vez por aproximação e descartado.
public readonly record struct VaoDaJanela(int Inicio, int Fim)
{
    public const int Partes = 16;

    public bool Valido => Inicio >= 0 && Fim <= Partes && Inicio < Fim;
}

// Só em memória. IrVer e Aproximar são os "maduros": agem na próxima decisão
// da agenda em IDLE ou no próprio disparo.
public enum EstagioDaCuriosidade
{
    // Sem foco, ou dispensada pelo usuário.
    Nenhuma,

    // Foco novo: conta os 28 s que faltam dos 30.
    Aguardando,

    // Foco no monitor dele: conta o resto do tempo do perfil.
    AguardandoPerto,

    // Foco ficou 30 s noutro monitor.
    IrVer,

    // Indo até a porta e atravessando; a decisão é revista na chegada.
    IndoVer,

    // Foco ficou bastante tempo no monitor dele.
    Aproximar,

    // Esperando o vão pedido ao adaptador.
    PedindoVao,

    // Andando até o lado da janela.
    Aproximando,

    Olhando,

    // Episódio acabou: espera o intervalo do perfil.
    Satisfeita,
}

// Escolhida por regra, sem sorteio, com Personalidade ligada; sem ela, sempre Padrao.
public enum VarianteDaReacao
{
    // Feliz no núcleo, risada na tela.
    Padrao,

    // Clicado descansando.
    Susto,

    // Clicado olhando a janela ativa: surpreso e depois travesso.
    Flagra,

    // Segundo clique seguido desde a última decisão, ou clique comum na energia Alta.
    Empolgada,

    // Clique comum na energia Baixa.
    Preguica,
}

public enum LadoDoEsconderijo
{
    Nenhum,

    // Atrás da borda de baixo (a barra de tarefas, se ela estiver embaixo).
    Baixo,

    Esquerda,

    Direita,

    // Atrás da borda de cima, de cabeça pra baixo (a partir do cipó).
    Cima,
}

public enum GrupoDoEstado
{
    Sistema,
    Autonomo,
    Fisico,
    Usuario,
}

// Decide quais eventos podem mostrar ele de novo.
public enum MotivoDoOcultamento
{
    Nenhum,
    PorUsuario,
    PorSessao,
    PorSuspensao,
    PorTelaCheia,
}

// Muda frequência e duração das ações autônomas, nunca a física.
public enum NivelDeEnergia
{
    Baixa,
    Media,
    Alta,
}

// Independe do estado. As 14 primeiras são as de humor (Expressoes.DeHumor);
// as do fim são de efeito do tamagotchi, só na onda de um item ou na
// paranoia. A chave da arte é o nome em minúsculas, então toda expressão
// precisa existir no manifesto.
public enum Expressao
{
    Neutro,
    Feliz,
    Rindo,
    Curioso,
    Surpreso,
    Assustado,
    Sonolento,
    Bocejando,
    Dormindo,
    Travesso,
    Entediado,
    Pensativo,
    Empolgado,
    Determinado,

    // Caras de efeito, só na onda de um item.
    Bebado,
    Enjoado,
    Chapado,
    Eletrico,
    Apaixonado,
    Tonto,
    Viajando,

    // Paranoia: olhando pro teto.
    Paranoico,
}

// Listas fixas pra que valor novo no fim de Expressao não mude os sorteios
// nem as reproduções gravadas.
public static class Expressoes
{
    // Na ordem do enum e de expressoes.png. Alimentam a troca automática e
    // as opções da emoção dominante (além de "Automática").
    public static IReadOnlyList<Expressao> DeHumor { get; } =
    [
        Expressao.Neutro, Expressao.Feliz, Expressao.Rindo, Expressao.Curioso, Expressao.Surpreso, Expressao.Assustado, Expressao.Sonolento,
        Expressao.Bocejando, Expressao.Dormindo, Expressao.Travesso, Expressao.Entediado, Expressao.Pensativo, Expressao.Empolgado, Expressao.Determinado,
    ];

    // Só a onda ou a paranoia mostram. Nunca são emoção dominante nem saem da
    // troca automática.
    public static IReadOnlyList<Expressao> DeEfeito { get; } =
    [
        Expressao.Bebado, Expressao.Enjoado, Expressao.Chapado, Expressao.Eletrico, Expressao.Apaixonado, Expressao.Tonto, Expressao.Viajando,
        Expressao.Paranoico,
    ];

    // Peso 1 cada, contra 6 da dominante. Mesma ordem de DeHumor.
    private static readonly IReadOnlyList<Expressao>[] TabelaDeCompanheiras =
    [
        [Expressao.Feliz, Expressao.Curioso, Expressao.Pensativo, Expressao.Entediado],      // Neutro
        [Expressao.Rindo, Expressao.Empolgado, Expressao.Travesso, Expressao.Curioso],       // Feliz
        [Expressao.Feliz, Expressao.Travesso, Expressao.Empolgado, Expressao.Surpreso],      // Rindo
        [Expressao.Pensativo, Expressao.Surpreso, Expressao.Feliz, Expressao.Travesso],      // Curioso
        [Expressao.Assustado, Expressao.Curioso, Expressao.Empolgado, Expressao.Rindo],      // Surpreso
        [Expressao.Surpreso, Expressao.Pensativo, Expressao.Curioso, Expressao.Neutro],      // Assustado
        [Expressao.Bocejando, Expressao.Dormindo, Expressao.Entediado, Expressao.Neutro],    // Sonolento
        [Expressao.Sonolento, Expressao.Entediado, Expressao.Neutro, Expressao.Pensativo],   // Bocejando
        [Expressao.Sonolento, Expressao.Bocejando, Expressao.Neutro, Expressao.Feliz],       // Dormindo
        [Expressao.Rindo, Expressao.Feliz, Expressao.Curioso, Expressao.Empolgado],          // Travesso
        [Expressao.Sonolento, Expressao.Bocejando, Expressao.Pensativo, Expressao.Neutro],   // Entediado
        [Expressao.Curioso, Expressao.Neutro, Expressao.Entediado, Expressao.Determinado],   // Pensativo
        [Expressao.Feliz, Expressao.Rindo, Expressao.Surpreso, Expressao.Determinado],       // Empolgado
        [Expressao.Empolgado, Expressao.Pensativo, Expressao.Neutro, Expressao.Feliz],       // Determinado
    ];

    // Só essas podem ser emoção dominante.
    public static bool EhDeHumor(Expressao expressao) => expressao is >= Expressao.Neutro and <= Expressao.Determinado;

    // Fora das 14 de humor, lança.
    public static IReadOnlyList<Expressao> Companheiras(Expressao dominante)
        => EhDeHumor(dominante)
            ? TabelaDeCompanheiras[(int)dominante]
            : throw new ArgumentOutOfRangeException(nameof(dominante), dominante, "A emoção dominante é uma das 14 caras de humor.");
}

// Ação visual curta na superfície atual; não muda estado, posição nem
// superfície. A agenda sorteia de Espiar a Brincar. Os do fim só a onda
// sorteia, também só em IDLE (OlharProTeto ainda vem, sem sorteio, no começo
// da paranoia).
public enum Gesto
{
    Nenhum,
    Espiar,
    OlharAoRedor,
    Cocar,
    Espreguicar,
    Brincar,

    // Da onda: só em IDLE e só pelos sorteios dela.
    Soluco,
    Danca,
    Gargalhada,
    Espirro,
    Tosse,
    Tremedeira,

    // Da paranoia: "tem alguém no teto".
    OlharProTeto,
    Agachar,
}

// As poses de perfil são desenhadas viradas pra direita.
public enum Direcao
{
    Direita,
    Esquerda,
}

// Acontecimento pontual que a apresentação pode mostrar.
public enum Sinal
{
    Nenhum,
    FoiClicado,
    FoiClicadoDuasVezes,
    Pousou,
    Acordou,
}

// O que o movimento avisa quando encontra uma superfície. Sai do passo
// físico; os testes também injetam pra exercitar a tabela de transições.
public enum SinalDeMovimento
{
    // Andando, chegou numa parede.
    Parede,

    // Andando, chegou numa passagem pro monitor vizinho.
    Passagem,

    // Andando, o chão acabou sem parede.
    FimDoChao,

    // Escalando, chegou no topo da área útil.
    TopoDaParede,

    // Descendo, a parede acabou no chão.
    FimDaParede,

    // Escalando, alcançou uma borda de cima onde dá pra pendurar.
    BordaSuperior,

    // Pendurado, chegou numa passagem compatível ou no fim da borda.
    FimDaBorda,

    // Pulando ou caindo, tocou o chão.
    ContatoComOChao,
}

// O que a agenda pode escolher em Idle.
[Flags]
public enum AcoesAutonomas
{
    Nenhuma = 0,
    Andar = 1,
    Escalar = 2,
    Pular = 4,
    Descansar = 8,
    Gesto = 16,
    TrocarExpressao = 32,

    // As seis de sempre. UsarPorContaPropria e IrAoOutroMonitor ficam de fora:
    // só DoAplicativo e os testes que pedem ligam, e configs antigas não mudam.
    Todas = Andar | Escalar | Pular | Descansar | Gesto | TrocarExpressao,

    // Ele "tira do chapéu" um baseado, sem item no mundo, e usa como se
    // tivessem soltado nele. No fim do enum pra não mudar os outros valores.
    // Só com o tamagotchi ligado (senão o peso é zero), em IDLE no chão, fora
    // do esconderijo, e nunca com Chapado ou paranoia na frente.
    UsarPorContaPropria = 64,

    // Anda até a porta plana e atravessa, sem o sorteio da porta das
    // caminhadas comuns. Só com a travessia ligada (configuração e
    // preferência) e com porta plana.
    IrAoOutroMonitor = 128,
}

public static class Estados
{
    public static GrupoDoEstado Grupo(this Estado estado) => estado switch
    {
        Estado.Booting or Estado.Hidden or Estado.Exiting => GrupoDoEstado.Sistema,
        Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Resting or Estado.Peeking => GrupoDoEstado.Autonomo,
        Estado.Falling or Estado.Landing => GrupoDoEstado.Fisico,
        Estado.Pressed or Estado.Dragging or Estado.Settling or Estado.Reacting or Estado.Using => GrupoDoEstado.Usuario,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado desconhecido."),
    };

    public static bool Visivel(this Estado estado) => estado is not (Estado.Booting or Estado.Hidden or Estado.Exiting);

    // Nesses, evento autônomo que chega é descartado.
    public static bool ControladoPeloUsuario(this Estado estado) => estado.Grupo() == GrupoDoEstado.Usuario;

    // Estados em que o passo físico move o personagem e o relógio precisa correr.
    public static bool EmMovimento(this Estado estado)
        => estado is Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Falling or Estado.Landing;

    // Autônomos, físicos, Reacting e Using (o usuário prevalece e o uso acaba na hora).
    public static bool AceitaPressionar(this Estado estado)
        => estado.Grupo() is GrupoDoEstado.Autonomo or GrupoDoEstado.Fisico || estado is Estado.Reacting or Estado.Using;
}
