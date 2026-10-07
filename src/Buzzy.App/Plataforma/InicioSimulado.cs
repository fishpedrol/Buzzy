using Buzzy.App.Composicao;

namespace Buzzy.App.Plataforma;

// Real, simulado ou indisponível; quem escolhe é InicioComOWindows.DaExecucao.
internal interface IInicioComOWindows
{
    ModoDoInicio Modo { get; }

    // Nunca grava.
    EstadoDoInicio Ler();

    ResultadoDoInicio Ligar();

    // Só apaga se o valor for desta cópia.
    ResultadoDoInicio Desligar();

    // Só o número vai pro log, nunca o caminho.
    int? UltimoErro { get; }
}

// Em memória, pra perfil de teste nunca tocar o registro. Começa desligado a cada
// abertura e segue as mesmas RegrasDoInicio do real.
internal sealed class InicioSimulado(string? caminhoAtual, string? valorRun = null, byte[]? aprovacao = null) : IInicioComOWindows
{
    internal string? ValorRun { get; private set; } = valorRun;

    internal byte[]? AprovacaoDoWindows { get; set; } = aprovacao;

    // Contadores pros testes de contenção.
    internal int Ligacoes { get; private set; }

    internal int Desligamentos { get; private set; }

    // Se setado, a próxima ação falha com esse código.
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

internal sealed class InicioIndisponivel : IInicioComOWindows
{
    public ModoDoInicio Modo => ModoDoInicio.Indisponivel;

    public int? UltimoErro => null;

    public EstadoDoInicio Ler() => EstadoDoInicio.Indisponivel;

    public ResultadoDoInicio Ligar() => ResultadoDoInicio.Indisponivel;

    public ResultadoDoInicio Desligar() => ResultadoDoInicio.Indisponivel;
}
