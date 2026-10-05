using Buzzy.App.Composicao;

namespace Buzzy.App.Plataforma;

/// <summary>
/// A porta do início com o Windows (Q-04; DEC-038, item 12): o adaptador real (<see cref="InicioComOWindows"/>), o simulado
/// (<see cref="InicioSimulado"/>) ou nenhum, pela regra única <see cref="InicioComOWindows.DaExecucao"/>.
/// </summary>
internal interface IInicioComOWindows
{
    ModoDoInicio Modo { get; }

    /// <summary>O estado agora; nunca grava.</summary>
    EstadoDoInicio Ler();

    /// <summary>Grava o valor Run desta cópia; só pelo pedido do usuário.</summary>
    ResultadoDoInicio Ligar();

    /// <summary>Apaga o valor Run, só se for desta cópia; só pelo pedido do usuário.</summary>
    ResultadoDoInicio Desligar();

    /// <summary>O código do Windows da última leitura ou ação que falhou, ou nulo (só o número vai ao log, nunca o caminho).</summary>
    int? UltimoErro { get; }
}

/// <summary>
/// O início com o Windows simulado, só em memória (DEC-038, item 12): o Buzzy com perfil de teste nunca toca o registro. Começa
/// desligado a cada abertura; os testes o constroem em qualquer estado. Segue as mesmas regras do real
/// (<see cref="RegrasDoInicio"/>): ligar grava o caminho desta cópia, desligar só apaga o desta cópia, e a aprovação do
/// Windows só é lida.
/// </summary>
internal sealed class InicioSimulado(string? caminhoAtual, string? valorRun = null, byte[]? aprovacao = null) : IInicioComOWindows
{
    /// <summary>O valor Run simulado.</summary>
    internal string? ValorRun { get; private set; } = valorRun;

    /// <summary>A marca do StartupApproved simulada (só lida pelo Buzzy).</summary>
    internal byte[]? AprovacaoDoWindows { get; set; } = aprovacao;

    /// <summary>Quantas vezes cada ação rodou (para os testes de contenção).</summary>
    internal int Ligacoes { get; private set; }

    internal int Desligamentos { get; private set; }

    /// <summary>Com um código, a próxima ação falha com ele (para os testes da falha mostrada ao usuário).</summary>
    internal int? FalharCom { get; set; }

    public ModoDoInicio Modo => ModoDoInicio.Simulado;

    public int? UltimoErro { get; private set; }

    public EstadoDoInicio Ler() => RegrasDoInicio.Avaliar(true, ValorRun, AprovacaoDoWindows, caminhoAtual);

    public ResultadoDoInicio Ligar()
    {
        Ligacoes++;
        UltimoErro = FalharCom;
        if (FalharCom is not null) return ResultadoDoInicio.Erro;
        if (!RegrasDoInicio.CaminhoValido(caminhoAtual)) return ResultadoDoInicio.Indisponivel;
        ValorRun = RegrasDoInicio.DadoDoRun(caminhoAtual!);
        return ResultadoDoInicio.Ok;
    }

    public ResultadoDoInicio Desligar()
    {
        Desligamentos++;
        UltimoErro = FalharCom;
        if (FalharCom is not null) return ResultadoDoInicio.Erro;
        if (!RegrasDoInicio.CaminhoValido(caminhoAtual)) return ResultadoDoInicio.Indisponivel;
        if (!RegrasDoInicio.DestaCopia(ValorRun, caminhoAtual!)) return ResultadoDoInicio.NaoEDestaCopia;
        ValorRun = null;
        return ResultadoDoInicio.Ok;
    }
}

/// <summary>Sem início com o Windows nesta execução: tudo indisponível, nada tentado.</summary>
internal sealed class InicioIndisponivel : IInicioComOWindows
{
    public ModoDoInicio Modo => ModoDoInicio.Indisponivel;

    public int? UltimoErro => null;

    public EstadoDoInicio Ler() => EstadoDoInicio.Indisponivel;

    public ResultadoDoInicio Ligar() => ResultadoDoInicio.Indisponivel;

    public ResultadoDoInicio Desligar() => ResultadoDoInicio.Indisponivel;
}
