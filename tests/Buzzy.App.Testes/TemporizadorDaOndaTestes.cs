using System.Diagnostics;
using System.Windows.Threading;
using Buzzy.App.Composicao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O temporizador da onda (DEC-028, passo T8; crítica, C2 e seção 4: "sem timer periódico"), com o DispatcherTimer de
/// verdade, sem janela: o teste roda o laço de mensagens da própria thread por um tempo curto. Um agendamento dispara uma
/// vez só, com a geração dele, e nunca de novo (um DispatcherTimer sem Stop dispararia a cada intervalo); agendar de novo
/// substitui o pendente; cancelar e parar (no encerramento) não deixam disparar.
/// </summary>
internal sealed class TemporizadorDaOndaTestes
{
    /// <summary>Roda o laço de mensagens desta thread por <paramref name="duracao"/>.</summary>
    private static void Bombear(TimeSpan duracao)
    {
        var quadro = new DispatcherFrame();
        var fim = new DispatcherTimer(DispatcherPriority.Normal) { Interval = duracao };
        fim.Tick += (_, _) =>
        {
            fim.Stop();
            quadro.Continue = false;
        };
        fim.Start();
        Dispatcher.PushFrame(quadro);
    }

    /// <summary>Roda o laço até a condição valer, ou até o limite; devolve se valeu.</summary>
    private static bool BombearAte(Func<bool> condicao, TimeSpan limite)
    {
        var relogio = Stopwatch.StartNew();
        while (!condicao())
        {
            if (relogio.Elapsed > limite) return false;
            Bombear(TimeSpan.FromMilliseconds(20));
        }
        return true;
    }

    [Teste]
    public void UmAgendamento_UmDisparoComAGeracao_ENuncaDeNovo()
    {
        var disparos = new List<long>();
        var t = new TemporizadorDaOnda(disparos.Add);
        Afirmar.Falso(t.Ligado, "criado, desligado");
        t.Agendar(TimeSpan.FromMilliseconds(40), 7);
        Afirmar.Igual((true, (long?)7, true), (t.Pendente, t.GeracaoPendente, t.Ligado), "agendado, com a geração, e ligado");
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 0, TimeSpan.FromSeconds(3)), "disparou");
        Afirmar.Igual((false, (long?)null, false), (t.Pendente, t.GeracaoPendente, t.Ligado), "depois do disparo, nada pendente e o DispatcherTimer desligado");

        // Mais de quatro intervalos depois, continua um disparo só: o temporizador para no próprio disparo.
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Sequencia([7L], disparos, "um disparo só, com a geração agendada");

        // Um agendamento novo, um disparo novo.
        t.Agendar(TimeSpan.FromMilliseconds(30), 8);
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 1, TimeSpan.FromSeconds(3)), "disparou de novo");
        Bombear(TimeSpan.FromMilliseconds(200));
        Afirmar.Sequencia([7L, 8L], disparos, "um disparo por agendamento");
    }

    [Teste]
    public void AgendarDeNovo_SubstituiOPendente()
    {
        var disparos = new List<long>();
        var t = new TemporizadorDaOnda(disparos.Add);
        t.Agendar(TimeSpan.FromMilliseconds(30), 1);
        t.Agendar(TimeSpan.FromMilliseconds(60), 2);
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 0, TimeSpan.FromSeconds(3)), "disparou");
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Sequencia([2L], disparos, "só o agendamento mais novo dispara");
    }

    [Teste]
    public void Cancelar_NaoDispara()
    {
        var disparos = new List<long>();
        var t = new TemporizadorDaOnda(disparos.Add);
        t.Agendar(TimeSpan.FromMilliseconds(30), 3);
        Afirmar.Verdadeiro(t.Cancelar(), "havia um disparo pendente");
        Afirmar.Falso(t.Pendente || t.Ligado, "nada pendente e o DispatcherTimer desligado");
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Sequencia([], disparos, "cancelado, não dispara");
        Afirmar.Falso(t.Cancelar(), "cancelar de novo não tem o que cancelar");

        // Cancelar não impede um agendamento seguinte.
        t.Agendar(TimeSpan.FromMilliseconds(30), 4);
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 0, TimeSpan.FromSeconds(3)), "o seguinte dispara");
        Afirmar.Sequencia([4L], disparos);
    }

    [Teste]
    public void Parar_NoEncerramento_NaoDisparaEIgnoraAgendamentosDepois()
    {
        var disparos = new List<long>();
        var t = new TemporizadorDaOnda(disparos.Add);
        t.Agendar(TimeSpan.FromMilliseconds(30), 5);
        t.Parar();
        Afirmar.Falso(t.Pendente || t.Ligado, "parado, nada pendente e o DispatcherTimer desligado");
        t.Agendar(TimeSpan.FromMilliseconds(10), 6);
        Afirmar.Falso(t.Pendente || t.Ligado, "parado, um agendamento depois é ignorado");
        Bombear(TimeSpan.FromMilliseconds(250));
        Afirmar.Sequencia([], disparos, "nada dispara depois de parar");
    }

    [Teste]
    public void AtrasoZeroOuNegativo_DisparaUmaVezLogo()
    {
        var disparos = new List<long>();
        var t = new TemporizadorDaOnda(disparos.Add);
        t.Agendar(TimeSpan.Zero, 9);
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 0, TimeSpan.FromSeconds(3)), "atraso zero dispara");
        t.Agendar(TimeSpan.FromMilliseconds(-5), 10);
        Afirmar.Verdadeiro(BombearAte(() => disparos.Count > 1, TimeSpan.FromSeconds(3)), "atraso negativo também, sem lançar");
        Bombear(TimeSpan.FromMilliseconds(150));
        Afirmar.Sequencia([9L, 10L], disparos, "uma vez cada");
    }
}
