using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Tamagotchi;

/// <summary>
/// Os invariantes 22 e 27 com a física do aplicativo e a chave Tamagotchi ligada (achado 2 da revisão adversarial): os de
/// InvariantesTestes rodam sem a física, as referências 01 a 05 sem a física e sem o tamagotchi, e
/// EmocaoDominanteTestes.Dominante_SoMudaACara sem itens. Estes dois foram a condição para o aplicativo ligar a chave
/// (passo T9), e a configuração do aplicativo, já com ela ligada, é a que eles conferem. Tudo é de desenho animado.
/// </summary>
internal static class ChaveLigadaTestes
{
    private static readonly TamanhoDip Sprite = new(128, 128);

    /// <summary>Um evento aplicado pela máquina e o resultado dele.</summary>
    private sealed record Aplicado(Evento Evento, Resultado Resultado);

    private static List<Aplicado> Registrar(SimuladorDeTempo sim)
    {
        var aplicados = new List<Aplicado>();
        sim.AoResultado = (_, e, r) => aplicados.Add(new Aplicado(e, r));
        return aplicados;
    }

    // Invariante 22 com a física do aplicativo: sem itens, sem onda, com a emoção automática e sem o baseado por conta
    // própria, ligar a chave não muda nada. A configuração ligada é a do aplicativo (DoAplicativo, com a chave ligada desde o
    // passo T9), com as ações de sempre (Todas, sem UsarPorContaPropria: o baseado por conta própria muda o sorteio da agenda de
    // propósito, e tem os testes dele, BaseadoPorContaPropriaTestes), e a desligada, a mesma com a chave apagada. Com a mesma
    // semente e os mesmos eventos (10 minutos por semente, com cliques, arrastes, clique duplo, bandeja e pausa sorteados, e a
    // agenda livre quase todo o tempo), nos três níveis de energia, com todas as ações de sempre ou só escalar, com ou sem
    // pular (muitas subidas, foguetes e pulos), evento a evento: as mesmas transições, os mesmos efeitos e o mesmo estado,
    // inclusive o gerador.
    [Teste]
    public static void ChaveLigadaSemItens_IgualADesligada()
    {
        Afirmar.Verdadeiro(ConfiguracaoDoNucleo.DoAplicativo(Sprite).Tamagotchi, "o aplicativo liga a chave desde o passo T9");
        var mestre = new Random(2033);
        long eventos = 0, subidas = 0, pulos = 0, andando = 0;
        for (int n = 0; n < 60; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            ConfiguracaoDoNucleo doAplicativo = ConfiguracaoDoNucleo.DoAplicativo(Sprite) with
            {
                Acoes = (n % 3) switch { 0 => AcoesAutonomas.Todas, 1 => AcoesAutonomas.Escalar, _ => AcoesAutonomas.Escalar | AcoesAutonomas.Pular },
            };
            ConfiguracaoDoNucleo desligada = doAplicativo with { Tamagotchi = false };
            var preferencias = new Preferencias((NivelDeEnergia)rnd.Next(3), true).Completa();
            // A mesma instância da topologia nas duas execuções: o estado guarda a topologia por referência.
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            var semChave = new SimuladorDeTempo(desligada, (ulong)semente, topologia, preferencias);
            var comChave = new SimuladorDeTempo(doAplicativo, (ulong)semente, topologia, preferencias);
            Afirmar.Igual(semChave.Estado, comChave.Estado, $"semente {semente}: a carga");
            List<Aplicado> a = Registrar(semChave), b = Registrar(comChave);
            while (semChave.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(5, 40));
                semChave.Avancar(espera);
                comChave.Avancar(espera);
                foreach (Evento e in InteracaoComAAgendaLivre(rnd, semChave.Estado))
                {
                    semChave.Aplicar(e);
                    comChave.Aplicar(e);
                }
            }

            Afirmar.Igual(a.Count, b.Count, $"semente {semente}: o mesmo número de eventos aplicados");
            for (int i = 0; i < a.Count; i++)
            {
                string onde = $"semente {semente} (sequência {n}), {i}º evento ({a[i].Evento})";
                Afirmar.Igual(a[i].Evento, b[i].Evento, $"{onde}: o mesmo evento");
                Afirmar.Sequencia(a[i].Resultado.Transicoes, b[i].Resultado.Transicoes, $"{onde}: as mesmas transições");
                Afirmar.Sequencia(a[i].Resultado.Efeitos, b[i].Resultado.Efeitos, $"{onde}: os mesmos efeitos");
                Afirmar.Igual(a[i].Resultado.Estado, b[i].Resultado.Estado, $"{onde}: o mesmo estado");
                subidas += a[i].Resultado.Transicoes.Count(t => t.Para == Estado.Climbing && t.De != Estado.Settling);
                pulos += a[i].Resultado.Transicoes.Count(t => t.Para == Estado.Jumping);
                if (a[i].Evento is Tick && a[i].Resultado.Estado.Estado == Estado.Walking) andando++;
            }
            eventos += a.Count;
        }
        Console.WriteLine($"         {eventos} eventos em 60 sementes de 10 minutos; {subidas} subidas, {pulos} pulos e quiques, {andando} passos andando");
        Afirmar.Verdadeiro(subidas >= 300 && pulos >= 100 && andando >= 10000, "a física foi exercitada: subidas, pulos e caminhadas");
    }

    // Invariante 27 com a física do aplicativo e a chave ligada, com itens e ondas: as duas execuções, com a emoção
    // automática e com a dominante escolhida pela carga, recebem os mesmos eventos (10 minutos de agenda livre por semente,
    // com itens invocados, soltos sobre ele, o que começa, soma, combina e alivia ondas, ou longe, largados e recolhidos, e
    // com ele posto no chão, no ar, na parede, no cipó e no esconderijo, pausado ou não), e a agenda do aplicativo às vezes o
    // faz fumar um baseado por conta própria. Evento a evento: as mesmas transições, os mesmos efeitos e o mesmo estado,
    // inclusive o gerador, os itens, o uso e as ondas, a não ser a cara e a própria emoção.
    [Teste]
    public static void Dominante_SoMudaACara_ComItensEOndas()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(Sprite) with { Tamagotchi = true };
        var mestre = new Random(2034);
        long eventos = 0, comOnda = 0, comFundo = 0, usos = 0, comOutraCara = 0, fumou = 0;
        for (int n = 0; n < 40; n++)
        {
            int semente = mestre.Next();
            var rnd = new Random(semente);
            Expressao dominante = Expressoes.DeHumor[n % Expressoes.DeHumor.Count];
            Topologia topologia = TopologiasDeExemplo.UmMonitor;
            var preferencias = new Preferencias((NivelDeEnergia)rnd.Next(3), true).Completa();
            var automatica = new SimuladorDeTempo(cfg, (ulong)semente, topologia, preferencias);
            var comDominante = new SimuladorDeTempo(cfg, (ulong)semente, topologia, preferencias with { EmocaoDominante = dominante });
            Afirmar.Igual(SemAsCaras(automatica.Estado), SemAsCaras(comDominante.Estado), $"semente {semente}: a carga");
            List<Aplicado> a = Registrar(automatica), b = Registrar(comDominante);
            bool posto = false;
            while (automatica.AgoraMs < 10 * 60 * 1000)
            {
                TimeSpan espera = TimeSpan.FromSeconds(rnd.Next(2, 30));
                automatica.Avancar(espera);
                comDominante.Avancar(espera);
                foreach (Evento e in OndaTestes.InteracaoComItens(rnd, automatica.Estado, cfg, ref posto))
                {
                    automatica.Aplicar(e);
                    comDominante.Aplicar(e);
                }
            }

            Afirmar.Igual(a.Count, b.Count, $"semente {semente}: o mesmo número de eventos aplicados");
            for (int i = 0; i < a.Count; i++)
            {
                string onde = $"semente {semente} (sequência {n}), dominante {dominante}, {i}º evento ({a[i].Evento})";
                Resultado sem = a[i].Resultado, com = b[i].Resultado;
                Afirmar.Igual(a[i].Evento, b[i].Evento, $"{onde}: o mesmo evento");
                Afirmar.Sequencia(sem.Transicoes, com.Transicoes, $"{onde}: as mesmas transições");
                Afirmar.Sequencia(sem.Efeitos, com.Efeitos, $"{onde}: os mesmos efeitos");
                Afirmar.Igual(SemAsCaras(sem.Estado), SemAsCaras(com.Estado), $"{onde}: o mesmo estado, a não ser pela cara");
                if (sem.Estado.Onda is not null) comOnda++;
                if (sem.Estado.OndaDeFundo is not null) comFundo++;
                usos += sem.Transicoes.Count(t => t.Para == Estado.Using && t.De != Estado.Using);
                fumou += sem.Transicoes.Count(t => t.Para == Estado.Using && a[i].Evento is AutonomyTimer);
                if (sem.Estado.Expressao != com.Estado.Expressao) comOutraCara++;
            }
            eventos += a.Count;
        }
        Console.WriteLine($"         {eventos} eventos em 40 sementes de 10 minutos; {usos} usos ({fumou} do baseado por conta própria), {comOnda} eventos com onda ({comFundo} com onda de fundo), {comOutraCara} com outra cara");
        Afirmar.Verdadeiro(usos >= 100 && comOnda >= eventos / 10 && comFundo > 0, "os itens e as ondas foram exercitados");
        Afirmar.Verdadeiro(fumou > 0, "o baseado por conta própria também");
        Afirmar.Verdadeiro(comOutraCara > eventos / 10, "a dominante muda a cara em boa parte do tempo");
    }

    /// <summary>
    /// Uma interação sorteada, sem itens, que deixa a agenda livre quase todo o tempo (para ele andar, escalar, disparar o
    /// foguete e pular bastante): clique, arraste até um ponto qualquer do monitor, clique duplo (o esconderijo),
    /// esconder (e mostrar na seguinte), pausar (e retomar na seguinte) ou nada.
    /// </summary>
    private static Evento[] InteracaoComAAgendaLivre(Random rnd, EstadoDoNucleo s)
    {
        if (s.Estado == Estado.Hidden) return [new CmdShow()];
        if (s.AutonomiaPausada) return [new CmdResumeAutonomy()];
        if (s.Lugar is not { } lugar) return [];
        var corpo = new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 20);
        var alvo = new PontoPx(rnd.Next(0, 1920), rnd.Next(60, 1032));
        return rnd.Next(10) switch
        {
            0 => [new Press(corpo), new Click()],
            1 => [new Press(corpo), new DragStart(), new DragMove(alvo), new DragEnd(alvo)],
            2 => [new Press(corpo), new Click(), new Press(corpo), new DoubleClick()],
            3 => [new CmdHide()],
            4 => [new CmdPauseAutonomy()],
            _ => [],
        };
    }

    /// <summary>O estado sem a cara e sem a emoção dominante: o que a emoção não pode mudar (invariante 27).</summary>
    private static EstadoDoNucleo SemAsCaras(EstadoDoNucleo s)
        => s with { Expressao = Expressao.Neutro, Preferencias = s.Preferencias with { EmocaoDominante = null } };
}
