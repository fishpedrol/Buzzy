namespace Buzzy.Core.Personagem;

/// <summary>
/// Estado de comportamento (ARCHITECTURE.md 2.6). Os nomes são os da especificação, para que a
/// tabela de transições e os testes possam ser conferidos linha a linha.
/// </summary>
public enum Estado
{
    /// <summary>Carrega configurações e topologia e restaura a posição.</summary>
    Booting,

    /// <summary>Parado sobre uma superfície.</summary>
    Idle,

    /// <summary>Andando sobre o chão.</summary>
    Walking,

    /// <summary>Subindo, descendo ou parado numa parede.</summary>
    Climbing,

    /// <summary>Sustentado pela borda superior.</summary>
    Hanging,

    /// <summary>Em trajetória balística iniciada por decisão autônoma.</summary>
    Jumping,

    /// <summary>Sem apoio, sob gravidade.</summary>
    Falling,

    /// <summary>Transição curta depois de tocar o chão.</summary>
    Landing,

    /// <summary>Descansando ou dormindo, sem animação contínua; o relógio fica parado.</summary>
    Resting,

    /// <summary>Botão pressionado sobre o personagem; ainda não se sabe se é clique ou arraste.</summary>
    Pressed,

    /// <summary>Personagem preso ao cursor.</summary>
    Dragging,

    /// <summary>Validação logo depois de soltar, de mostrar ou de uma mudança de topologia.</summary>
    Settling,

    /// <summary>Reação curta a um clique.</summary>
    Reacting,

    /// <summary>Escondido, sem relógio e sem desenho; o motivo fica em <see cref="MotivoDoOcultamento"/>.</summary>
    Hidden,

    /// <summary>Grava o estado e encerra.</summary>
    Exiting,

    /// <summary>
    /// Escondido atrás da borda de baixo (a barra de tarefas) ou de uma lateral, só com a cabeça e
    /// as mãos para fora (DEC-025). Entra e sai pelo clique duplo; sem relógio; a agenda só troca a cara.
    /// </summary>
    Peeking,

    /// <summary>
    /// Usa o item que o usuário soltou sobre ele (DEC-028): come, bebe, fuma, cheira, engole ou inala, de desenho
    /// animado, por um número fixo de passos, no apoio em que estava (chão, parede, cipó ou esconderijo). O relógio
    /// corre; no grupo do usuário, nada autônomo chega; um PRESS o segura na hora. No fim, a acomodação o devolve ao
    /// mesmo apoio. Também é o estado do baseado que ele fuma por conta própria, no chão, sem item no mundo
    /// (<see cref="AcoesAutonomas.FumarBaseado"/>).
    /// </summary>
    Using,
}

/// <summary>
/// O tamanho do personagem (Fase 8; DEC-038, item 9), em três passos fixos: Pequena (96 DIP, 1,5×), Média (128 DIP, 2×,
/// o padrão) e Grande (192 DIP, 3×) para a arte de 64 px, ampliada sem suavização. Vale na próxima abertura; itens em 48 DIP.
/// </summary>
public enum EscalaDoPersonagem
{
    /// <summary>96 DIP: a arte em 1,5×.</summary>
    Pequena,

    /// <summary>128 DIP: a arte em 2×, o padrão e o tamanho de antes da Fase 8.</summary>
    Media,

    /// <summary>192 DIP: a arte em 3×.</summary>
    Grande,
}

/// <summary>
/// O vão da janela em primeiro plano (DEC-037, item 2): o trecho horizontal dela, recortado à área útil do monitor do foco,
/// em dezesseis avos da largura dessa área, de <see cref="Inicio"/> a <see cref="Fim"/> (0 ≤ início &lt; fim ≤ 16). Nada
/// vertical, nenhum pixel: é só o destino do próprio personagem, pedido uma vez por aproximação e descartado.
/// </summary>
public readonly record struct VaoDaJanela(int Inicio, int Fim)
{
    /// <summary>Em quantas partes a largura da área útil é dividida.</summary>
    public const int Partes = 16;

    /// <summary>Se o vão é válido: dentro de 0 a 16, com o início antes do fim.</summary>
    public bool Valido => Inicio >= 0 && Fim <= Partes && Inicio < Fim;
}

/// <summary>
/// O estágio da curiosidade (Fase 7; DEC-037), só em memória. <see cref="IrVer"/> e <see cref="Aproximar"/> são os maduros:
/// agem na próxima decisão da agenda em IDLE ou no próprio disparo.
/// </summary>
public enum EstagioDaCuriosidade
{
    /// <summary>Sem foco, ou dispensada pelo usuário.</summary>
    Nenhuma,

    /// <summary>Um foco novo: conta os 28 s que faltam dos 30.</summary>
    Aguardando,

    /// <summary>O foco está no monitor dele: conta o resto do bastante tempo do perfil.</summary>
    AguardandoPerto,

    /// <summary>O foco ficou 30 s noutro monitor: vai ver.</summary>
    IrVer,

    /// <summary>Indo até a porta e atravessando; a decisão é revista na chegada.</summary>
    IndoVer,

    /// <summary>O foco ficou bastante tempo no monitor dele: chega perto.</summary>
    Aproximar,

    /// <summary>Esperando o vão da janela pedido ao adaptador.</summary>
    PedindoVao,

    /// <summary>Andando até o lado do trecho da janela.</summary>
    Aproximando,

    /// <summary>Olhando a janela.</summary>
    Olhando,

    /// <summary>O episódio acabou: espera o intervalo do perfil.</summary>
    Satisfeita,
}

/// <summary>
/// A variante da reação ao clique (DEC-037, item 8), escolhida por regra, sem sorteio, com a capacidade
/// <c>Personalidade</c>; sem ela, sempre <see cref="Padrao"/>.
/// </summary>
public enum VarianteDaReacao
{
    /// <summary>A reação de antes: feliz no núcleo, a risada na tela.</summary>
    Padrao,

    /// <summary>Clicado descansando: um susto, surpreso.</summary>
    Susto,

    /// <summary>Clicado olhando a janela ativa (curiosidade): pego no flagra, surpreso e depois travesso.</summary>
    Flagra,

    /// <summary>O segundo clique seguido desde a última decisão, ou o clique comum na energia Alta: empolgado.</summary>
    Empolgada,

    /// <summary>O clique comum na energia Baixa: preguiçoso, sonolento.</summary>
    Preguica,
}

/// <summary>Em que borda o personagem está escondido (DEC-025).</summary>
public enum LadoDoEsconderijo
{
    Nenhum,

    /// <summary>Atrás da borda de baixo da área útil: a barra de tarefas, com a barra embaixo.</summary>
    Baixo,

    /// <summary>Atrás da lateral esquerda da área útil.</summary>
    Esquerda,

    /// <summary>Atrás da lateral direita da área útil.</summary>
    Direita,

    /// <summary>
    /// Atrás da borda de cima da área útil, de cabeça para baixo (pedido do usuário de 2026-10-03: "queria que desse pra
    /// esconder ele em cima tambem, quando ele esta no cipo, dando 2 clicks").
    /// </summary>
    Cima,
}

/// <summary>Grupo do estado na tabela de ARCHITECTURE.md 2.6.</summary>
public enum GrupoDoEstado
{
    Sistema,
    Autonomo,
    Fisico,
    Usuario,
}

/// <summary>Por que o personagem está escondido. Decide quais eventos podem mostrá-lo de novo.</summary>
public enum MotivoDoOcultamento
{
    Nenhum,
    PorUsuario,
    PorSessao,
    PorSuspensao,
    PorTelaCheia,
}

/// <summary>Nível de energia (DEC-014): muda frequência e duração das ações autônomas, nunca a física.</summary>
public enum NivelDeEnergia
{
    Baixa,
    Media,
    Alta,
}

/// <summary>
/// Expressão do rosto, dimensão independente do estado (invariante 6). Uma por expressão de
/// docs/IDENTIDADE_VISUAL.md, seção 6; o manifesto da Fase 6 precisa cobrir todas. As 14 primeiras são as de humor
/// (<see cref="Expressoes.DeHumor"/>); as oito do fim são as caras de efeito do tamagotchi (DEC-028,
/// <see cref="Expressoes.DeEfeito"/>), que só a onda de um item ou a paranoia mostram. A chave da arte é o nome em
/// minúsculas.
/// </summary>
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

    // Caras de efeito (DEC-028): de desenho animado, só na onda de um item.
    Bebado,
    Enjoado,
    Chapado,
    Eletrico,
    Apaixonado,
    Tonto,
    Viajando,

    // A da paranoia (pedido do usuário de 2026-10-01): de desenho animado, olhando para o teto.
    Paranoico,
}

/// <summary>
/// Listas fixas de expressões (DEC-027). A troca de cara da agenda e a emoção dominante usam só as 14 caras de
/// humor, numa lista fixa: um valor novo no fim de <see cref="Expressao"/> não muda os sorteios nem as
/// reproduções gravadas.
/// </summary>
public static class Expressoes
{
    /// <summary>
    /// As 14 caras de humor, na ordem do enum e de expressoes.png: o sorteio automático da troca de cara e as
    /// opções da emoção dominante, além de "Automática".
    /// </summary>
    public static IReadOnlyList<Expressao> DeHumor { get; } =
    [
        Expressao.Neutro, Expressao.Feliz, Expressao.Rindo, Expressao.Curioso, Expressao.Surpreso, Expressao.Assustado, Expressao.Sonolento,
        Expressao.Bocejando, Expressao.Dormindo, Expressao.Travesso, Expressao.Entediado, Expressao.Pensativo, Expressao.Empolgado, Expressao.Determinado,
    ];

    /// <summary>
    /// As oito caras de efeito do tamagotchi (DEC-028), no fim do enum: só a onda de um item ou a paranoia as mostram, pela
    /// cara da fase e pelos sorteios dela (<see cref="TabelaDoTamagotchi"/>). Nunca são a emoção dominante nem saem da
    /// troca automática.
    /// </summary>
    public static IReadOnlyList<Expressao> DeEfeito { get; } =
    [
        Expressao.Bebado, Expressao.Enjoado, Expressao.Chapado, Expressao.Eletrico, Expressao.Apaixonado, Expressao.Tonto, Expressao.Viajando,
        Expressao.Paranoico,
    ];

    /// <summary>
    /// As quatro companheiras de cada cara de humor, na ordem de <see cref="DeHumor"/>: sorteadas com peso 1 cada,
    /// contra 6 da dominante.
    /// </summary>
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

    /// <summary>Se a cara é uma das 14 de humor (<see cref="DeHumor"/>), as únicas que podem ser a emoção dominante.</summary>
    public static bool EhDeHumor(Expressao expressao) => expressao is >= Expressao.Neutro and <= Expressao.Determinado;

    /// <summary>As quatro caras que acompanham a <paramref name="dominante"/> no sorteio. Fora das 14 de humor, lança.</summary>
    public static IReadOnlyList<Expressao> Companheiras(Expressao dominante)
        => EhDeHumor(dominante)
            ? TabelaDeCompanheiras[(int)dominante]
            : throw new ArgumentOutOfRangeException(nameof(dominante), dominante, "A emoção dominante é uma das 14 caras de humor.");
}

/// <summary>
/// Gesto curto (ARCHITECTURE.md 2.6): ação visual de duração limitada na superfície atual,
/// que não muda estado, posição nem superfície (invariante 15). A agenda sorteia de <see cref="Espiar"/> a
/// <see cref="Brincar"/>; os oito do fim são os da onda de um item (DEC-028) e os da paranoia, que só a onda sorteia,
/// também só em IDLE (o <see cref="OlharProTeto"/> também vem, sem sorteio, no começo da paranoia).
/// </summary>
public enum Gesto
{
    Nenhum,
    Espiar,
    OlharAoRedor,
    Cocar,
    Espreguicar,
    Brincar,

    // Gestos da onda (DEC-028): de desenho animado, só em IDLE e só pelos sorteios da onda.
    Soluco,
    Danca,
    Gargalhada,
    Espirro,
    Tosse,
    Tremedeira,

    // Os da paranoia (pedido do usuário de 2026-10-01): de desenho animado, "tem alguém no teto".
    OlharProTeto,
    Agachar,
}

/// <summary>Para onde o personagem está virado. As poses de perfil são desenhadas para a direita.</summary>
public enum Direcao
{
    Direita,
    Esquerda,
}

/// <summary>Acontecimento pontual que a apresentação pode mostrar (ARCHITECTURE.md 2.10).</summary>
public enum Sinal
{
    Nenhum,
    FoiClicado,
    FoiClicadoDuasVezes,
    Pousou,
    Acordou,
}

/// <summary>
/// Sinais que o módulo de movimento emite quando o personagem encontra uma superfície
/// (ARCHITECTURE.md 2.5 e 2.9). Na Fase 2 ainda não há movimento: os testes injetam esses
/// sinais para exercitar a tabela de transições; a partir da Fase 4 eles saem do passo físico.
/// </summary>
public enum SinalDeMovimento
{
    /// <summary>Andando, chegou a uma parede.</summary>
    Parede,

    /// <summary>Andando, chegou a uma passagem para o monitor vizinho.</summary>
    Passagem,

    /// <summary>Andando, o chão acabou sem parede.</summary>
    FimDoChao,

    /// <summary>Escalando, chegou ao topo da área útil.</summary>
    TopoDaParede,

    /// <summary>Escalando para baixo, a parede acabou no chão.</summary>
    FimDaParede,

    /// <summary>Escalando, alcançou uma borda superior onde pode se pendurar.</summary>
    BordaSuperior,

    /// <summary>Pendurado, chegou a uma passagem compatível ou ao fim da borda.</summary>
    FimDaBorda,

    /// <summary>Pulando ou caindo, tocou o chão.</summary>
    ContatoComOChao,
}

/// <summary>Ações que a agenda autônoma pode escolher em <see cref="Estado.Idle"/>.</summary>
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

    /// <summary>
    /// As seis ações de sempre (Fases 2 a 4). A do tamagotchi, <see cref="FumarBaseado"/>, fica de fora: só a configuração do
    /// aplicativo a liga (<see cref="ConfiguracaoDoNucleo.DoAplicativo"/>) e os testes que a pedem, e toda configuração de
    /// antes continua igual.
    /// </summary>
    Todas = Andar | Escalar | Pular | Descansar | Gesto | TrocarExpressao,

    /// <summary>
    /// Fumar um baseado por conta própria, quando ele quer (pedido do usuário de 2026-10-01, 19:10; DEC-028), de desenho
    /// animado: no fim do enum, sem mudar os valores das outras. Só existe com a chave do tamagotchi ligada
    /// (<see cref="ConfiguracaoDoNucleo.Tamagotchi"/>); desligada, o peso é zero e nada muda. Só em IDLE, no chão, sem estar
    /// escondido, e nunca com a onda Chapado ou a paranoia na frente (<see cref="PerfilDeEnergia.PesoFumarBaseado"/>): ele
    /// "tira do chapéu" o baseado, sem item no mundo, e o usa como o baseado que o usuário solta nele.
    /// </summary>
    FumarBaseado = 64,

    /// <summary>
    /// Ir ao outro monitor (Fase 5, passo P13; DEC-032): andar até a porta plana do monitor em que está e atravessar, sem o
    /// sorteio da porta, que é das caminhadas comuns. No fim do enum, fora de <see cref="Todas"/>, como o baseado por conta
    /// própria: só a configuração do aplicativo e os testes que a pedem a ligam. Só existe com a travessia ligada
    /// (<see cref="ConfiguracaoDoNucleo.Travessia"/> e <see cref="Preferencias.AtravessarMonitores"/>) e uma porta plana.
    /// </summary>
    IrAoOutroMonitor = 128,
}

/// <summary>Consultas sobre os estados.</summary>
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

    /// <summary>Se a janela do personagem aparece na tela neste estado.</summary>
    public static bool Visivel(this Estado estado) => estado is not (Estado.Booting or Estado.Hidden or Estado.Exiting);

    /// <summary>Estados do grupo usuário: eventos autônomos que chegam neles são descartados (ARCHITECTURE.md 2.3).</summary>
    public static bool ControladoPeloUsuario(this Estado estado) => estado.Grupo() == GrupoDoEstado.Usuario;

    /// <summary>Estados em que o personagem se move pelo passo físico e o relógio precisa correr.</summary>
    public static bool EmMovimento(this Estado estado)
        => estado is Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Falling or Estado.Landing;

    /// <summary>
    /// Estados que aceitam <c>PRESS</c>: autônomos, físicos, <see cref="Estado.Reacting"/> (DEC-004) e
    /// <see cref="Estado.Using"/> (DEC-028: o usuário prevalece, e o uso acaba na hora).
    /// </summary>
    public static bool AceitaPressionar(this Estado estado)
        => estado.Grupo() is GrupoDoEstado.Autonomo or GrupoDoEstado.Fisico || estado is Estado.Reacting or Estado.Using;
}
