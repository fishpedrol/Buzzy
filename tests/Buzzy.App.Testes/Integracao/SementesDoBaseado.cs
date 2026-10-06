using Buzzy.Core;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// A semente do baseado por conta própria (pedido do usuário de 2026-10-01, 19:10; DEC-028) para o que abre o Buzzy de
/// verdade com a agenda ligada: a primeira em que a primeira decisão da agenda depois da carga (sem posição salva, com as
/// preferências padrão e a topologia dada, a desta máquina) chega cedo e é fumar um baseado por conta própria, sem item no
/// mundo. É a diretiva de teste que faz a ação sair cedo na tela: com a configuração do aplicativo e nada no meio, a raiz
/// recebe o mesmo AUTONOMY_TIMER que a simulação, e a semente vai pela linha de comando (<c>--semente</c>). A escolha usa só o
/// núcleo, sem janela. O teste de integração e a verificação de tela (Buzzy.Verificacao, que compila este arquivo, como o
/// <see cref="PerfilDeTeste"/>) escolhem a semente do mesmo jeito.
/// </summary>
internal static class SementesDoBaseado
{
    /// <summary>
    /// A primeira semente, de 1 a <paramref name="ultima"/>, em que a primeira decisão vem em até <paramref name="ate"/> e é o
    /// baseado por conta própria, com a configuração dada (a do aplicativo), e o atraso dessa decisão; nula se nenhuma.
    /// </summary>
    internal static (ulong Semente, TimeSpan PrimeiraDecisao)? PrimeiraEmQueFumaCedo(ConfiguracaoDoNucleo cfg, Topologia topologia, TimeSpan ate, ulong ultima)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(topologia);
        for (ulong semente = 1; semente <= ultima; semente++)
        {
            var nucleo = new Nucleo(cfg, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao with { ConteudoAdulto = true, ItensAdultosHabilitados = Preferencias.TodosOsItensAdultos }));
            AgendarDecisao? agenda = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (agenda is null || agenda.Atraso > ate) continue;
            nucleo.Enfileirar(new AutonomyTimer(agenda.Geracao));
            nucleo.Processar();
            if (nucleo.Estado is { Estado: Estado.Using, Uso: { Item: Item.Baseado } } && nucleo.Estado.Itens.Quantidade == 0)
                return (semente, agenda.Atraso);
        }
        return null;
    }
}
