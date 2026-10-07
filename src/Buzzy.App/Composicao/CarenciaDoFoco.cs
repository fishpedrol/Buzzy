using Buzzy.Core;

namespace Buzzy.App.Composicao;

// O monitor do primeiro plano só vai pro núcleo depois de ficar o mesmo por 2 s, e
// só quando muda. Candidato novo recomeça a contagem; voltar pro já publicado cancela.
// Guarda só a chave do monitor, nunca a janela. Só na thread da interface.
internal sealed class CarenciaDoFoco
{
    internal static readonly TimeSpan Carencia = TimeSpan.FromSeconds(2);

    private readonly Func<TimeSpan, Action, Action> _agendarUmaVez;
    private readonly Action<string> _publicar;
    private Action? _cancelar;
    private bool _parada;

    // agendarUmaVez devolve o cancelamento do disparo.
    internal CarenciaDoFoco(Func<TimeSpan, Action, Action> agendarUmaVez, Action<string> publicar)
    {
        _agendarUmaVez = agendarUmaVez ?? throw new ArgumentNullException(nameof(agendarUmaVez));
        _publicar = publicar ?? throw new ArgumentNullException(nameof(publicar));
    }

    internal string? Publicado { get; private set; }

    // Nulo sem carência pendente.
    internal string? Candidato { get; private set; }

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

    // Depois de parar, nada mais é publicado.
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
