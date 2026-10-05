namespace Buzzy.Core.Personagem;

/// <summary>
/// Pedido do núcleo à raiz de composição (ARCHITECTURE.md 2.3, passo 4). O núcleo nunca move
/// janela, grava arquivo nem agenda timer diretamente; ele devolve efeitos, na ordem em que
/// devem ser executados.
/// </summary>
public abstract record Efeito;

/// <summary>Leva a janela ao posicionamento dado (pixels físicos).</summary>
public sealed record MoverJanela(Posicionamento Destino) : Efeito;

/// <summary>Mostra a janela do personagem sem ativá-la.</summary>
public sealed record MostrarJanela : Efeito;

/// <summary>Esconde a janela do personagem.</summary>
public sealed record EsconderJanela : Efeito;

/// <summary>Liga o relógio de passo fixo: a raiz passa a entregar <see cref="Tick"/>.</summary>
public sealed record LigarRelogio : Efeito;

/// <summary>Desliga o relógio de passo fixo.</summary>
public sealed record DesligarRelogio : Efeito;

/// <summary>
/// Agenda o temporizador único da agenda autônoma. Um agendamento novo substitui o anterior;
/// quando disparar, a raiz entrega <see cref="AutonomyTimer"/> com a mesma geração.
/// </summary>
public sealed record AgendarDecisao(TimeSpan Atraso, long Geracao) : Efeito;

/// <summary>Cancela o temporizador da agenda autônoma.</summary>
public sealed record CancelarDecisao : Efeito;

/// <summary>
/// Agenda o temporizador único da onda de um item (DEC-028), um disparo só, de 1 s ou mais, até a próxima fase ou
/// nível. Um agendamento novo substitui o anterior; quando disparar, a raiz entrega <see cref="ItemEffectTimer"/> com a
/// mesma geração. Só sai do núcleo com o tamagotchi ligado.
/// </summary>
public sealed record AgendarOnda(TimeSpan Atraso, long Geracao) : Efeito;

/// <summary>Cancela o temporizador da onda: a onda acabou, ou o aplicativo está saindo.</summary>
public sealed record CancelarOnda : Efeito;

/// <summary>
/// Agenda o disparo único da curiosidade (Fase 7; DEC-037, item 3): um <see cref="CuriosityTimer"/> com a geração, depois do
/// atraso; substitui o pendente.
/// </summary>
public sealed record AgendarCuriosidade(TimeSpan Atraso, long Geracao) : Efeito;

/// <summary>Cancela o disparo da curiosidade pendente.</summary>
public sealed record CancelarCuriosidade : Efeito;

/// <summary>
/// Pede ao adaptador, uma vez por aproximação, o vão da janela em primeiro plano no monitor <paramref name="Chave"/> (DEC-037,
/// item 2); a resposta vem em <see cref="ActiveWindowSpan"/> com a mesma geração.
/// </summary>
public sealed record PedirVaoDaJanelaAtiva(string Chave, long Geracao) : Efeito;

// Janelas dos itens (DEC-028), uma por item, sem ativar: o núcleo diz onde cada uma está e quando aparece, some ou sai.
// Num lote, a raiz pode pular um MoverItem que tenha outro do mesmo Id adiante; mostrar, esconder e remover, nunca.

/// <summary>Mostra a janela do item no lugar dado (pixels físicos); cria a janela, se ainda não existe.</summary>
public sealed record MostrarItem(int Id, Item Item, Posicionamento Lugar) : Efeito;

/// <summary>Leva a janela do item ao lugar dado.</summary>
public sealed record MoverItem(int Id, Posicionamento Lugar) : Efeito;

/// <summary>Esconde a janela do item: o personagem se escondeu, ou o monitor do item está ocupado pela tela cheia.</summary>
public sealed record EsconderItem(int Id) : Efeito;

/// <summary>Fecha a janela do item, que saiu da tela (um Id que a raiz não conhece é ignorado).</summary>
public sealed record RemoverItem(int Id, MotivoDaRemocao Motivo) : Efeito;

/// <summary>Solta a captura do mouse do gesto em curso sobre o item (esconder, sair ou recolher no meio dele).</summary>
public sealed record LiberarCapturaDoItem(int Id) : Efeito;

/// <summary>Solta a captura do mouse de um gesto em curso (esconder ou sair no meio do arraste).</summary>
public sealed record LiberarCaptura : Efeito;

/// <summary>Abre o menu do Buzzy no ponto dado.</summary>
public sealed record AbrirMenu(PontoPx Ponto) : Efeito;

/// <summary>Abre o painel compacto de energia (Fase 8).</summary>
public sealed record AbrirPainelDeEnergia : Efeito;

/// <summary>Fecha o painel de energia.</summary>
public sealed record FecharPainelDeEnergia : Efeito;

/// <summary>Abre a janela de configurações (Fase 8).</summary>
public sealed record AbrirConfiguracoes : Efeito;

/// <summary>
/// Grava a posição escolhida pelo usuário no settings.json (Fase 5), com a tela do monitor da época
/// (<see cref="PosicaoDoPersonagem.TelaDoMonitor"/>), que a partida seguinte usa na restauração. Só sai de
/// evento do usuário ou do sistema, nunca do relógio nem da agenda (invariante 18). Se a raiz grava com
/// atraso ou na hora, diz <see cref="Persistencia.PoliticaDeGravacao"/>.
/// </summary>
public sealed record GravarPosicao(PosicaoDoPersonagem Posicao) : Efeito
{
    /// <summary>
    /// A borda do esconderijo no momento da gravação (DEC-025; <see cref="EstadoDoNucleo.Esconderijo"/>): quem sai
    /// escondido volta escondido no mesmo lado (esquema v3, DEC-029, item 11). Fica fora do construtor posicional.
    /// </summary>
    public LadoDoEsconderijo Esconderijo { get; init; }

    /// <summary>
    /// A marca "preso pelo usuário" no momento da gravação (DEC-024; <see cref="EstadoDoNucleo.PresoPeloUsuario"/>):
    /// quem estava preso na parede ou no cipó continua preso onde o usuário o deixou (esquema v3, DEC-029, item 11).
    /// </summary>
    public bool PresoPeloUsuario { get; init; }
}

/// <summary>
/// Grava as preferências no settings.json (Fase 5; quem as muda é o menu da emoção dominante, DEC-027, e, na Fase 8, o
/// painel), pela mesma política de <see cref="GravarPosicao"/>.
/// </summary>
public sealed record GravarPreferencias(Preferencias Preferencias) : Efeito;

/// <summary>
/// Aplica o "Sempre no topo" escolhido (Fase 8; DEC-038, item 8) ao personagem e aos itens, uma vez, sem ativar nenhuma
/// janela; nada periódico.
/// </summary>
public sealed record AplicarSempreNoTopo(bool Ligado) : Efeito;

/// <summary>Encerra o aplicativo.</summary>
public sealed record Encerrar : Efeito;

/// <summary>Uma troca de estado de comportamento, com a regra da tabela que a produziu.</summary>
public sealed record Transicao(Estado De, Estado Para, string Regra)
{
    public override string ToString() => $"{De} -> {Para} ({Regra})";
}
