using System.Globalization;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Integração da Fase 4: o Buzzy de verdade, com a autonomia ligada e uma semente escolhida por
/// simulação do próprio núcleo (a mesma configuração do app), anda pela tela sozinho, sempre na
/// área útil, e pressionar no meio do movimento o segura na hora. Mensagens só são postadas à
/// janela do Buzzy aberto pelo teste; nada injeta input. Só roda com --integracao.
/// </summary>
[Integracao]
internal sealed class MovimentoIntegracaoTestes
{
    /// <summary>A configuração do núcleo que Aplicacao.Iniciar usa (fonte única no núcleo).</summary>
    private static ConfiguracaoDoNucleo ConfiguracaoDoApp => ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));

    /// <summary>
    /// Semente em que a primeira decisão autônoma é andar ou pular, e cedo (até 12 s), simulada
    /// com o núcleo e a topologia reais desta máquina.
    /// </summary>
    private static (ulong Semente, TimeSpan Atraso, Estado Destino) EscolherSemente(Topologia topologia)
    {
        for (ulong semente = 1; semente < 5000; semente++)
        {
            var nucleo = new Nucleo(ConfiguracaoDoApp, semente);
            nucleo.Enfileirar(new Loaded(topologia, null, Preferencias.Padrao));
            AgendarDecisao? agenda = nucleo.Processar().OfType<AgendarDecisao>().LastOrDefault();
            if (agenda is null || agenda.Atraso > TimeSpan.FromSeconds(12)) continue;
            nucleo.Enfileirar(new AutonomyTimer(agenda.Geracao));
            nucleo.Processar();
            if (nucleo.Estado.Estado is Estado.Walking or Estado.Jumping)
                return (semente, agenda.Atraso, nucleo.Estado.Estado);
        }
        throw new InvalidOperationException("Nenhuma semente com movimento cedo nas primeiras 5000.");
    }

    [Teste]
    public void Autonomia_AndaOuPulaSozinhoDentroDaAreaUtilEPressionarSeguraNaHora()
    {
        Topologia topologia = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
        (ulong semente, TimeSpan atraso, Estado destino) = EscolherSemente(topologia);
        Console.WriteLine($"         semente {semente}: primeira decisão em {atraso.TotalSeconds:0.0} s → {destino}");

        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(pausado: false, semente: semente);
        b.Esperar(e => e.Chave == "NUCLEO" && e["semente"] == semente.ToString(CultureInfo.InvariantCulture), 5000, "semente aplicada");
        RetanguloPx inicio = b.RetanguloDaJanela();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "AutonomyTimer" && e["para"] == destino.ToString(), (int)atraso.TotalMilliseconds + 5000, $"decisão autônoma para {destino}");
        b.Esperar(e => e.Chave == "RELOGIO" && e["ligado"] == "sim", 2000, "relógio ligado pelo movimento");

        // A janela anda, sempre inteira na área útil do monitor em que está.
        var vistos = new HashSet<RetanguloPx>();
        for (int i = 0; i < 20; i++)
        {
            RetanguloPx r = b.RetanguloDaJanela();
            vistos.Add(r);
            MonitorDoDesktop m = topologia.MonitorMaisProximo(new PontoPx(r.Esquerda + r.Largura / 2, r.Base - 1));
            Afirmar.Verdadeiro(m.AreaUtil.Contem(r), $"janela {r} inteira na área útil {m.AreaUtil}");
            Thread.Sleep(50);
        }
        Afirmar.Verdadeiro(vistos.Count >= 3, $"a janela se moveu sozinha ({vistos.Count} posições em 1 s; partiu de {inicio})");

        // Pressionar no meio do movimento: segura na hora (critério 3 da Fase 4).
        bool aindaMovendo = !b.Eventos().Any(e => e.Chave == "RELOGIO" && e["ligado"] == "nao");
        if (aindaMovendo)
        {
            RetanguloPx agora = b.RetanguloDaJanela();
            var centro = new PontoPx(agora.Esquerda + agora.Largura / 2, agora.Topo + agora.Altura / 2);
            b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, centro);
            EventoDoLog press = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Press" && e["para"] == "Pressed", 2000, "PRESS no meio do movimento");
            RetanguloPx segurado = b.RetanguloDaJanela();
            Thread.Sleep(500);
            Afirmar.Igual(segurado, b.RetanguloDaJanela(), $"segurado na hora: a janela não se move mais (estado antes: {press["de"]})");
            b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, centro);
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Click" && e["para"] == "Reacting", 2000, "CLICK depois de segurar");
        }
        else
        {
            Console.WriteLine("         o movimento terminou antes do teste pressionar: parte do critério 3 não exercitada nesta execução");
        }

        Afirmar.Igual(0, b.FecharPorWmClose());
    }
}
