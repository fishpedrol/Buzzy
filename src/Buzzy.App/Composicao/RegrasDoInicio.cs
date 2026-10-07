using System.IO;

namespace Buzzy.App.Composicao;

internal enum EstadoDoInicio
{
    // Sem o valor Buzzy na chave Run.
    Desligado,

    // Aponta pra esta cópia e o Windows não desativou.
    Ligado,

    // Aponta pra esta cópia, mas foi desativado nas Configurações do Windows.
    DesativadoPeloWindows,

    // Aponta pra outra cópia (movida ou em outra pasta).
    OutroCaminho,

    // Erro, caminho inválido, chave ausente, sem pasta ou perfil inválido.
    Indisponivel,
}

internal enum ResultadoDoInicio
{
    Ok,
    Erro,

    // Desligar com o valor de outra cópia: nada é apagado.
    NaoEDestaCopia,

    // Nada foi tentado.
    Indisponivel,
}

internal enum ModoDoInicio
{
    // Registro de verdade: só sem perfil de teste.
    Registro,

    // Só em memória, com perfil de teste válido.
    Simulado,

    // Perfil inválido, persistência desligada ou sem a pasta do Buzzy.
    Indisponivel,
}

// Marca do Windows em StartupApproved\Run. Só lida, nunca gravada.
internal enum AprovacaoDoInicio
{
    // Sem marca conta como aprovado.
    Ausente,
    Ligada,
    Desligada,

    // Formato desconhecido: conta como aprovado (o Windows decide) e vai pro log.
    Desconhecida,
}

// Regras puras do início com o Windows, sem Win32, testadas por tabela.
internal static class RegrasDoInicio
{
    internal const int CaminhoMaximo = 1024;

    // Absoluto, arquivo Buzzy*.exe (o Buzzy.exe da pasta ou o Buzzy-<versão>-win-x64.exe
    // do download), sem aspas nem caractere de controle. Rodando por "dotnet Buzzy.dll"
    // o caminho é o do dotnet, e é recusado.
    internal static bool CaminhoValido(string? caminho)
        => caminho is { Length: > 0 and <= CaminhoMaximo }
           && Path.IsPathFullyQualified(caminho)
           && !caminho.Contains('"') && !caminho.Any(char.IsControl)
           && Path.GetFileName(caminho) is var nome
           && nome.StartsWith("Buzzy", StringComparison.OrdinalIgnoreCase)
           && nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    // Entre aspas e sem argumentos: caminho sem aspas pode ser sequestrado.
    internal static string DadoDoRun(string caminho) => $"\"{caminho}\"";

    // Caminho relativo nunca conta como desta cópia (dependeria da pasta atual do processo).
    // Compara o caminho completo normalizado, sem diferenciar maiúsculas.
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

    // 12 bytes: primeiro byte par = ligada, ímpar = desligada. Na prática aparece 0x02 e
    // 0x03, mas não é documentado, então não dá pra ter certeza.
    internal static AprovacaoDoInicio Aprovacao(byte[]? dados)
        => dados switch
        {
            null => AprovacaoDoInicio.Ausente,
            { Length: 12 } d => d[0] % 2 == 0 ? AprovacaoDoInicio.Ligada : AprovacaoDoInicio.Desligada,
            _ => AprovacaoDoInicio.Desconhecida,
        };

    internal static EstadoDoInicio Avaliar(bool leuComSucesso, string? valorRun, byte[]? aprovacao, string? caminhoAtual)
    {
        if (!leuComSucesso || !CaminhoValido(caminhoAtual)) return EstadoDoInicio.Indisponivel;
        if (valorRun is null) return EstadoDoInicio.Desligado;
        if (!DestaCopia(valorRun, caminhoAtual!)) return EstadoDoInicio.OutroCaminho;
        return Aprovacao(aprovacao) == AprovacaoDoInicio.Desligada ? EstadoDoInicio.DesativadoPeloWindows : EstadoDoInicio.Ligado;
    }

    // true liga, false desliga, null não faz nada.
    internal static bool? AcaoDoPedido(EstadoDoInicio estado, bool marcar)
        => (estado, marcar) switch
        {
            (EstadoDoInicio.Desligado or EstadoDoInicio.OutroCaminho, true) => true,
            (EstadoDoInicio.Ligado or EstadoDoInicio.DesativadoPeloWindows, false) => false,
            _ => null,
        };
}
