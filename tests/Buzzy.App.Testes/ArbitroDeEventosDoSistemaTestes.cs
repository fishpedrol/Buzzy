using Buzzy.App.Composicao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

internal sealed class ArbitroDeEventosDoSistemaTestes
{
    private sealed class Cenario
    {
        internal List<Evento> Enviados { get; } = [];

        internal ArbitroDeEventosDoSistema Arbitro { get; }

        internal Cenario() => Arbitro = new(Enviados.Add);
    }

    [Teste]
    public void Desbloqueio_EsperaUmaTopologiaPublicadaAntesDeChegarAoNucleo()
    {
        var c = new Cenario();

        TimeSpan espera = c.Arbitro.Sinalizar(new SessionUnlocked());
        Afirmar.Igual(TimeSpan.Zero, espera, "desbloquear não acrescenta atraso mínimo");
        Afirmar.Igual(0, c.Enviados.Count, "SESSION_UNLOCKED ainda não foi enviado");

        c.Arbitro.TopologiaRelida(publicada: false);
        Afirmar.Igual(0, c.Enviados.Count, "uma leitura incoerente não libera o evento");

        c.Arbitro.TopologiaRelida(publicada: true);
        Afirmar.Igual(1, c.Enviados.Count, "a leitura publicada libera um evento");
        Afirmar.Verdadeiro(c.Enviados[0] is SessionUnlocked, "o evento liberado é SESSION_UNLOCKED");
    }

    [Teste]
    public void Retomada_EsperaNoMinimo1500msEConservaAOrdemAteUmaTopologiaPublicada()
    {
        var c = new Cenario();

        c.Arbitro.Sinalizar(new SessionUnlocked());
        TimeSpan espera = c.Arbitro.Sinalizar(new Resumed());

        Afirmar.Igual(TimeSpan.FromMilliseconds(1500), espera, "RESUMED pede o atraso mínimo provisório");
        Afirmar.Igual(0, c.Enviados.Count, "nenhum evento é enviado antes da topologia");
        c.Arbitro.TopologiaRelida(publicada: false);
        Afirmar.Igual(0, c.Enviados.Count, "a tentativa incoerente não libera a fila");

        c.Arbitro.TopologiaRelida(publicada: true);
        Afirmar.Igual(2, c.Enviados.Count, "a leitura publicada libera os dois eventos");
        Afirmar.Verdadeiro(c.Enviados[0] is SessionUnlocked, "SESSION_UNLOCKED permanece primeiro");
        Afirmar.Verdadeiro(c.Enviados[1] is Resumed, "RESUMED permanece depois");
    }

    [Teste]
    public void Bloqueio_CancelaDesbloqueioPendenteEChegaImediatamenteAoNucleo()
    {
        var c = new Cenario();
        c.Arbitro.Sinalizar(new SessionUnlocked());

        c.Arbitro.Sinalizar(new SessionLocked());

        Afirmar.Igual(1, c.Enviados.Count, "SESSION_LOCKED é imediato");
        Afirmar.Verdadeiro(c.Enviados[0] is SessionLocked, "o bloqueio foi enviado");
        c.Arbitro.TopologiaRelida(publicada: true);
        Afirmar.Igual(1, c.Enviados.Count, "um SESSION_UNLOCKED antigo não desfaz o novo bloqueio");
    }

    [Teste]
    public void Suspensao_CancelaRetomadaPendenteEChegaImediatamenteAoNucleo()
    {
        var c = new Cenario();
        c.Arbitro.Sinalizar(new Resumed());

        c.Arbitro.Sinalizar(new Suspending());

        Afirmar.Igual(1, c.Enviados.Count, "SUSPENDING é imediato");
        Afirmar.Verdadeiro(c.Enviados[0] is Suspending, "a suspensão foi enviada");
        c.Arbitro.TopologiaRelida(publicada: true);
        Afirmar.Igual(1, c.Enviados.Count, "um RESUMED antigo não desfaz a nova suspensão");
    }
}
