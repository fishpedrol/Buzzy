using System.IO;

namespace Buzzy.App.Composicao;

/// <summary>O estado do início com o Windows, como a janela de configurações o mostra (DEC-038, item 10).</summary>
internal enum EstadoDoInicio
{
    /// <summary>Sem o valor Buzzy na chave Run.</summary>
    Desligado,

    /// <summary>O valor aponta para esta cópia, e o Windows não o desativou.</summary>
    Ligado,

    /// <summary>O valor aponta para esta cópia, mas o usuário o desativou nas Configurações do Windows.</summary>
    DesativadoPeloWindows,

    /// <summary>O valor Buzzy aponta para outra cópia (movida ou em outra pasta).</summary>
    OutroCaminho,

    /// <summary>Sem como ler ou gravar nesta execução (erro, caminho inválido, chave ausente, sem pasta ou perfil inválido).</summary>
    Indisponivel,
}

/// <summary>O resultado de uma ação no início com o Windows.</summary>
internal enum ResultadoDoInicio
{
    Ok,
    Erro,

    /// <summary>Desligar com o valor de outra cópia: nada é apagado.</summary>
    NaoEDestaCopia,

    /// <summary>Indisponível nesta execução: nada foi tentado.</summary>
    Indisponivel,
}

/// <summary>De onde vem o início com o Windows nesta execução (DEC-038, item 12).</summary>
internal enum ModoDoInicio
{
    /// <summary>O registro do usuário: só o Buzzy de verdade, sem perfil de teste.</summary>
    Registro,

    /// <summary>Um simulado só em memória: com perfil de teste válido.</summary>
    Simulado,

    /// <summary>Nenhum: perfil inválido, persistência desligada ou sem a pasta do Buzzy.</summary>
    Indisponivel,
}

/// <summary>Como o Windows marcou o valor em <c>StartupApproved\Run</c>: só lido, nunca gravado.</summary>
internal enum AprovacaoDoInicio
{
    /// <summary>Sem marca: vale aprovado.</summary>
    Ausente,
    Ligada,
    Desligada,

    /// <summary>Um formato que o Buzzy não conhece: vale aprovado (o Windows decide), e o log diz.</summary>
    Desconhecida,
}

/// <summary>
/// As regras puras do início com o Windows (Q-04; DEC-038, itens 10 e 12), sem Win32: o dado gravado no valor Run, a validação
/// do caminho desta cópia, a comparação de cópias e o estado a partir do que foi lido. Testadas por tabela.
/// </summary>
internal static class RegrasDoInicio
{
    /// <summary>O maior caminho aceito para o executável.</summary>
    internal const int CaminhoMaximo = 1024;

    /// <summary>
    /// Se o caminho do executável desta cópia serve para o valor Run: absoluto, terminado em <c>\Buzzy.exe</c> (sem diferenciar
    /// maiúsculas), sem aspas nem caractere de controle e com até <see cref="CaminhoMaximo"/> caracteres. Rodando por
    /// <c>dotnet Buzzy.dll</c>, o caminho é o do dotnet e é recusado.
    /// </summary>
    internal static bool CaminhoValido(string? caminho)
        => caminho is { Length: > 0 and <= CaminhoMaximo }
           && Path.IsPathFullyQualified(caminho)
           && caminho.EndsWith(@"\Buzzy.exe", StringComparison.OrdinalIgnoreCase)
           && !caminho.Contains('"') && !caminho.Any(char.IsControl);

    /// <summary>O dado gravado: o caminho entre aspas, sem argumentos (evita o caminho sem aspas sequestrável).</summary>
    internal static string DadoDoRun(string caminho) => $"\"{caminho}\"";

    /// <summary>
    /// Se o valor Run aponta para esta cópia: sem as aspas, um caminho absoluto (um relativo dependeria da pasta atual do
    /// processo e nunca é desta cópia), pelo caminho completo normalizado, sem diferenciar maiúsculas.
    /// </summary>
    internal static bool DestaCopia(string? valorRun, string caminho)
    {
        if (string.IsNullOrWhiteSpace(valorRun)) return false;
        string semAspas = valorRun.Trim();
        if (semAspas.Length >= 2 && semAspas[0] == '"' && semAspas[^1] == '"') semAspas = semAspas[1..^1];
        if (!Path.IsPathFullyQualified(semAspas)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(semAspas).TrimEnd('\\'), Path.GetFullPath(caminho).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// A marca do <c>StartupApproved</c>: ausente vale aprovada; com 12 bytes, primeiro byte par, ligada, e ímpar, desligada (o
    /// observado comum é 0x02 e 0x03; UNCERTAIN, sem documentação); outro formato, desconhecida.
    /// </summary>
    internal static AprovacaoDoInicio Aprovacao(byte[]? dados)
        => dados switch
        {
            null => AprovacaoDoInicio.Ausente,
            { Length: 12 } d => d[0] % 2 == 0 ? AprovacaoDoInicio.Ligada : AprovacaoDoInicio.Desligada,
            _ => AprovacaoDoInicio.Desconhecida,
        };

    /// <summary>O estado a partir do que foi lido; sem leitura possível ou com o caminho desta cópia inválido, indisponível.</summary>
    internal static EstadoDoInicio Avaliar(bool leuComSucesso, string? valorRun, byte[]? aprovacao, string? caminhoAtual)
    {
        if (!leuComSucesso || !CaminhoValido(caminhoAtual)) return EstadoDoInicio.Indisponivel;
        if (valorRun is null) return EstadoDoInicio.Desligado;
        if (!DestaCopia(valorRun, caminhoAtual!)) return EstadoDoInicio.OutroCaminho;
        return Aprovacao(aprovacao) == AprovacaoDoInicio.Desligada ? EstadoDoInicio.DesativadoPeloWindows : EstadoDoInicio.Ligado;
    }

    /// <summary>A ação que um pedido da caixa causa no estado dado, ou nenhuma: marcar liga; desmarcar desliga; o resto, nada.</summary>
    internal static bool? AcaoDoPedido(EstadoDoInicio estado, bool marcar)
        => (estado, marcar) switch
        {
            (EstadoDoInicio.Desligado or EstadoDoInicio.OutroCaminho, true) => true,
            (EstadoDoInicio.Ligado or EstadoDoInicio.DesativadoPeloWindows, false) => false,
            _ => null,
        };
}
