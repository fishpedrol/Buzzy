using Buzzy.Core;

namespace Buzzy.App.Composicao;

/// <summary>
/// A carência do foco (Fase 7; DEC-037, item 2): o monitor do primeiro plano só vai ao núcleo depois de ficar o mesmo por
/// <see cref="Carencia"/>, e só quando muda. Um candidato novo durante a carência recomeça a contagem com ele; o publicado de
/// novo cancela a carência pendente. Guarda só chaves opacas de monitor, nunca a janela. Nada é periódico: um disparo único
/// por candidato novo. Só na thread da interface.
/// </summary>
internal sealed class CarenciaDoFoco
{
    /// <summary>Quanto o mesmo monitor precisa ficar com o primeiro plano antes de ir ao núcleo.</summary>
    internal static readonly TimeSpan Carencia = TimeSpan.FromSeconds(2);

    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Action<string> _publicar;
    private Action? _cancelar;
    private bool _parada;

    /// <param name="agendarUmaVez">Agenda um disparo único e devolve o que o cancela.</param>
    /// <param name="publicar">Recebe a chave do monitor do foco confirmada, a cada mudança.</param>
    internal CarenciaDoFoco(Func<TimeSpan, Action, Action> agendarUmaVez, Action<string> publicar)
    {
        _agendarUmaVez = agendarUmaVez ?? throw new ArgumentNullException(nameof(agendarUmaVez));
        _publicar = publicar ?? throw new ArgumentNullException(nameof(publicar));
    }

    /// <summary>A chave publicada por último; nula antes da primeira.</summary>
    internal string? Publicado { get; private set; }

    /// <summary>A chave em carência; nula sem carência pendente.</summary>
    internal string? Candidato { get; private set; }

    /// <summary>Um candidato da avaliação (<see cref="AgendaDaTelaCheia.MonitorDoFoco"/>).</summary>
    internal void Candidatar(string chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        if (_parada || chave == Candidato) return;
        Cancelar();
        if (chave == Publicado) return;
        Candidato = chave;
        _cancelar = _agendarUmaVez(Carencia, () =>
        {
            _cancelar = null;
            if (_parada || Candidato != chave) return;
            Candidato = null;
            Publicado = chave;
            _publicar(chave);
        });
    }

    /// <summary>O encerramento: cancela a carência pendente; nada mais é publicado.</summary>
    internal void Parar()
    {
        _parada = true;
        Cancelar();
    }

    private void Cancelar()
    {
        _cancelar?.Invoke();
        _cancelar = null;
        Candidato = null;
    }
}
