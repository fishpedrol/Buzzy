using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.Core.Testes.Persistencia;

/// <summary>
/// Invariante 18 (Fase 5, ARCHITECTURE.md 2.6), conferido do mesmo jeito pelas sequências aleatórias sem a física
/// (<see cref="Personagem.InvariantesTestes"/>) e com a configuração do aplicativo
/// (<see cref="PropriedadesDaPersistenciaTestes"/>). Todo <see cref="GravarPosicao"/>:
/// <list type="bullet">
/// <item>sai só dos eventos em que o usuário ou o sistema fixam a posição (<see cref="EventosQueGravam"/>), nunca do
/// relógio, do movimento, da agenda autônoma, da troca de expressão, da carga, da topologia, da tela cheia nem das
/// preferências: não há gravação periódica (DEC-011);</item>
/// <item>traz uma posição já gravável: chave não vazia, frações em [0, 1] e a tela do monitor da época conhecida
/// e não vazia, que toda posição descrita ou validada pela máquina tem;</item>
/// <item>traz a postura do estado depois do evento (esquema v3, DEC-029, item 11): a borda do esconderijo e a marca
/// "preso pelo usuário", como ficaram;</item>
/// <item>volta igual do settings.json: escrita e lida pelo esquema, com as preferências em vigor (a emoção dominante
/// inclusive, DEC-027) e a postura, é válida, sem aviso e sem precisar de normalização.</item>
/// </list>
/// Por isso a emoção dominante entrou no núcleo e no esquema juntos, e a postura também: um valor que o arquivo não
/// guardasse faria todo GravarPosicao seguinte falhar aqui.
/// </summary>
internal static class InvarianteDezoito
{
    /// <summary>
    /// Os únicos eventos que gravam a posição: soltar e cancelar o arraste, redefinir a posição, esconder (pela
    /// bandeja, pelo bloqueio da sessão e pela suspensão) e sair (pelo menu e pelo fim da sessão).
    /// </summary>
    internal static readonly IReadOnlyList<Type> EventosQueGravam =
    [
        typeof(DragEnd), typeof(DragCancel), typeof(CmdResetPosition), typeof(CmdHide),
        typeof(SessionLocked), typeof(Suspending), typeof(CmdExit), typeof(SessionEnding),
    ];

    /// <summary>
    /// O que viola o invariante neste <see cref="GravarPosicao"/>, vindo de <paramref name="evento"/>, com o estado
    /// <paramref name="depois"/> dele (as preferências em vigor e a postura); nulo se nada.
    /// </summary>
    internal static string? Violacao(Evento evento, GravarPosicao gravada, EstadoDoNucleo depois)
    {
        PosicaoDoPersonagem posicao = gravada.Posicao;
        if (!EventosQueGravam.Contains(evento.GetType()))
            return $"GravarPosicao saiu de {evento.GetType().Name}, de origem {evento.Origem}; só gravam {string.Join(", ", EventosQueGravam.Select(t => t.Name))}";

        // "is >= 0 and <= 1" também recusa NaN.
        if (string.IsNullOrEmpty(posicao.ChaveMonitor) || posicao.FracaoX is not (>= 0 and <= 1) || posicao.FracaoY is not (>= 0 and <= 1)
            || posicao.TelaDoMonitor is not { Vazio: false })
            return $"posição não gravável {Gravacao.DescreverPosicaoCompleta(posicao)} (tela {posicao.TelaDoMonitor?.ToString() ?? "desconhecida"})";

        if (gravada.Esconderijo != depois.Esconderijo || gravada.PresoPeloUsuario != depois.PresoPeloUsuario)
            return $"postura gravada {gravada.Esconderijo}/preso {gravada.PresoPeloUsuario}; o estado depois do evento tem {depois.Esconderijo}/preso {depois.PresoPeloUsuario}";

        var salvas = new ConfiguracoesSalvas(posicao, depois.Preferencias) { Esconderijo = gravada.Esconderijo, PresoPeloUsuario = gravada.PresoPeloUsuario };
        LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(EsquemaDeConfiguracoes.Escrever(salvas));
        if (lida.Situacao != SituacaoDaLeitura.Valida || lida.Avisos.Count != 0 || lida.Configuracoes != salvas)
        {
            string volta = lida.Configuracoes.Posicao is { } p ? Gravacao.DescreverPosicaoCompleta(p) : "sem posição";
            return $"{Gravacao.DescreverPosicaoCompleta(posicao)} ({gravada.Esconderijo}/preso {gravada.PresoPeloUsuario}) não voltou igual do settings.json ({lida.Situacao}, {volta}, "
                + $"{lida.Configuracoes.Esconderijo}/preso {lida.Configuracoes.PresoPeloUsuario}, {lida.Configuracoes.Preferencias}; avisos: {string.Join(" | ", lida.Avisos)})";
        }
        return null;
    }
}
