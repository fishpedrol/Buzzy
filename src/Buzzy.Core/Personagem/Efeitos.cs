namespace Buzzy.Core.Personagem;

// Pedido do núcleo pra raiz. O núcleo nunca move janela, grava arquivo nem
// agenda timer sozinho: devolve efeitos, na ordem em que devem rodar.
public abstract record Efeito;

// Destino em px físicos.
public sealed record MoverJanela(Posicionamento Destino) : Efeito;

// Sem ativar a janela.
public sealed record MostrarJanela : Efeito;

public sealed record EsconderJanela : Efeito;

// Relógio de passo fixo: com ele ligado a raiz entrega Tick.
public sealed record LigarRelogio : Efeito;

public sealed record DesligarRelogio : Efeito;

// Timer único da agenda autônoma: um agendamento novo substitui o anterior.
// Ao disparar, a raiz entrega AutonomyTimer com a mesma geração.
public sealed record AgendarDecisao(TimeSpan Atraso, long Geracao) : Efeito;

public sealed record CancelarDecisao : Efeito;

// Timer único da onda de um item, de 1 s ou mais, até a próxima fase ou nível.
// Substitui o anterior; ao disparar vem ItemEffectTimer com a mesma geração.
// Só sai com o tamagotchi ligado.
public sealed record AgendarOnda(TimeSpan Atraso, long Geracao) : Efeito;

// A onda acabou ou o app está saindo.
public sealed record CancelarOnda : Efeito;

// Um CuriosityTimer com a geração depois do atraso; substitui o pendente.
public sealed record AgendarCuriosidade(TimeSpan Atraso, long Geracao) : Efeito;

public sealed record CancelarCuriosidade : Efeito;

// Uma vez por aproximação: o vão da janela em primeiro plano no monitor Chave.
// A resposta volta em ActiveWindowSpan com a mesma geração.
public sealed record PedirVaoDaJanelaAtiva(string Chave, long Geracao) : Efeito;

// Janelas dos itens, uma por item, sem ativar: o núcleo diz onde cada uma está e quando aparece, some ou sai.
// Num lote, a raiz pode pular um MoverItem que tenha outro do mesmo Id adiante; mostrar, esconder e remover, nunca.

// Lugar em px físicos; cria a janela se ainda não existe.
public sealed record MostrarItem(int Id, Item Item, Posicionamento Lugar) : Efeito;

public sealed record MoverItem(int Id, Posicionamento Lugar) : Efeito;

// O personagem se escondeu, ou o monitor do item está em tela cheia.
public sealed record EsconderItem(int Id) : Efeito;

// O item saiu da tela. Id desconhecido é ignorado.
public sealed record RemoverItem(int Id, MotivoDaRemocao Motivo) : Efeito;

// Pra esconder, sair ou recolher no meio de um gesto sobre o item.
public sealed record LiberarCapturaDoItem(int Id) : Efeito;

// Pra esconder ou sair no meio do arraste.
public sealed record LiberarCaptura : Efeito;

public sealed record AbrirMenu(PontoPx Ponto) : Efeito;

public sealed record AbrirPainelDeEnergia : Efeito;

public sealed record FecharPainelDeEnergia : Efeito;

public sealed record AbrirConfiguracoes : Efeito;

// Grava a posição escolhida no settings.json, junto com a tela do monitor
// (a próxima partida usa pra restaurar). Só sai de evento do usuário ou do
// sistema, nunca do relógio nem da agenda. Gravar já ou com atraso fica a
// cargo de Persistencia.PoliticaDeGravacao.
public sealed record GravarPosicao(PosicaoDoPersonagem Posicao) : Efeito
{
    // Quem sai escondido volta escondido do mesmo lado.
    public LadoDoEsconderijo Esconderijo { get; init; }

    // Preso na parede ou no cipó pelo usuário continua preso no mesmo lugar.
    public bool PresoPeloUsuario { get; init; }
}

// Mesma política de gravação da GravarPosicao.
public sealed record GravarPreferencias(Preferencias Preferencias) : Efeito;

// Aplica uma vez no personagem e nos itens, sem ativar janela. Nada periódico.
public sealed record AplicarSempreNoTopo(bool Ligado) : Efeito;

public sealed record Encerrar : Efeito;

// Troca de estado, com o nome da regra da tabela que a causou.
public sealed record Transicao(Estado De, Estado Para, string Regra)
{
    public override string ToString() => $"{De} -> {Para} ({Regra})";
}
