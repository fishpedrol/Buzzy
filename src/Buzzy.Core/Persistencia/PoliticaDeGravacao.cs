using Buzzy.Core.Personagem;

namespace Buzzy.Core.Persistencia;

/// <summary>
/// Quando a raiz grava o settings.json (Fase 5; ARCHITECTURE.md 2.12; DEC-010 e DEC-011). Só constantes e a
/// regra: o temporizador é da raiz, e a E/S, do adaptador.
///
/// Um pedido de gravação (efeito <see cref="GravarPosicao"/> ou <see cref="GravarPreferencias"/>) grava com
/// <see cref="Atraso"/>, para a gravação cair com o personagem parado e uma rajada de pedidos virar uma
/// gravação só; grava na hora quando o evento que o gerou é <see cref="Imediata">imediato</see>. Nenhuma
/// gravação é periódica: só eventos do usuário ou do sistema geram pedidos (invariante 18).
/// </summary>
public static class PoliticaDeGravacao
{
    /// <summary>
    /// Espera depois do último pedido, reiniciada a cada pedido: menor que o intervalo de acomodação (3 s),
    /// para a gravação cair antes de o personagem voltar a se mover.
    /// </summary>
    public static readonly TimeSpan Atraso = TimeSpan.FromSeconds(2);

    /// <summary>Esperas das novas tentativas, uma de cada, depois de uma gravação com atraso que falhou; depois da última, desiste até o próximo pedido.</summary>
    public static readonly IReadOnlyList<TimeSpan> EsperasDeNovaTentativa = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60)];

    /// <summary>Tentativas da gravação na hora (e ao descarregar o pendente), com <see cref="PausaEntreTentativasImediatas"/> entre elas.</summary>
    public const int TentativasImediatas = 3;

    /// <summary>Pausa entre as tentativas da gravação na hora.</summary>
    public static readonly TimeSpan PausaEntreTentativasImediatas = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Se o pedido gerado por este evento grava na hora, em vez de esperar o <see cref="Atraso"/>: depois de
    /// suspender, terminar a sessão ou sair, o processo pode não ter outra chance. Bloquear a sessão também
    /// grava na hora: bloqueio seguido de suspensão é o caminho comum antes do sono, e a suspensão, com o
    /// personagem já escondido pela sessão, não gera outro pedido.
    /// </summary>
    public static bool Imediata(Evento evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return evento is Suspending or SessionEnding or CmdExit or SessionLocked;
    }
}
