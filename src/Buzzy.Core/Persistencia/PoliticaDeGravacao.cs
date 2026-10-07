using Buzzy.Core.Personagem;

namespace Buzzy.Core.Persistencia;

// Quando gravar o settings.json. Só a regra: o timer é da raiz e a E/S, do adaptador.
// Pedidos esperam Atraso, pra gravar com o personagem parado e juntar rajadas numa gravação só;
// eventos Imediata gravam na hora. Nada é periódico: só eventos geram pedidos.
public static class PoliticaDeGravacao
{
    // Reinicia a cada pedido. Menor que a acomodação (3 s), pra gravar antes de ele voltar a andar.
    public static readonly TimeSpan Atraso = TimeSpan.FromSeconds(2);

    // Uma de cada após falha da gravação com atraso; depois da última, desiste até o próximo pedido.
    public static readonly IReadOnlyList<TimeSpan> EsperasDeNovaTentativa = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60)];

    // Gravação na hora (e ao descarregar o pendente).
    public const int TentativasImediatas = 3;

    public static readonly TimeSpan PausaEntreTentativasImediatas = TimeSpan.FromMilliseconds(50);

    // Depois de suspender, encerrar a sessão ou sair pode não haver outra chance. Bloquear também:
    // bloqueio + suspensão é o caminho comum antes do sono, e a suspensão, com o personagem já
    // escondido, não gera outro pedido.
    public static bool Imediata(Evento evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return evento is Suspending or SessionEnding or CmdExit or SessionLocked;
    }
}
