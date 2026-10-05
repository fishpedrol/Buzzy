using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 7, passo F7-P10 (critérios 1 e 3): com tudo o que o aplicativo liga, a personalidade e a curiosidade inclusive, e o
/// foco fixo no monitor dele, três horas de relógio virtual em cada energia. Baixa, Média e Alta dão frequências e pausas
/// observavelmente distintas (caminhadas, descanso, gestos calmos, aproximações da janela), com a mesma física; e a mesma
/// semente com os mesmos eventos dá as mesmas transições.
/// </summary>
internal static class EnergiaDaFase7Testes
{
    private sealed record Medida(int Caminhadas, double FracaoDescansando, double FracaoCalmos, int Gestos, int Aproximacoes, int Explorações, List<string> Regras);

    private static Medida Medir(NivelDeEnergia energia, ulong semente = 9, int horas = 3)
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128));
        var sim = new SimuladorDeTempo(cfg, semente, TopologiasDeExemplo.UmMonitor, new Preferencias(energia, true)) { ResponderVao = _ => new VaoDaJanela(3, 9) };
        sim.Aplicar(new ForegroundMonitorChanged(TopologiasDeExemplo.Display1));
        double descansando = 0, ultimo = sim.AgoraMs;
        bool emDescanso = false;
        int gestos = 0, calmos = 0;
        sim.AoAplicar = (antes, e, depois) =>
        {
            if (emDescanso) descansando += sim.AgoraMs - ultimo;
            ultimo = sim.AgoraMs;
            emDescanso = depois.Estado == Estado.Resting;
            if (antes.Gesto == Gesto.Nenhum && depois.Gesto is Gesto.Espiar or Gesto.OlharAoRedor or Gesto.Cocar or Gesto.Espreguicar or Gesto.Brincar && antes.Curiosidade != EstagioDaCuriosidade.Olhando && depois.Onda is null && e is AutonomyTimer)
            {
                gestos++;
                if (depois.Gesto is Gesto.Cocar or Gesto.Espreguicar) calmos++;
            }
        };
        sim.Avancar(TimeSpan.FromHours(horas));
        List<string> regras = [.. sim.Transicoes.Select(t => t.Regra)];
        return new Medida(
            regras.Count(r => r is "IDLE + AUTONOMY_TIMER: andar" or "IDLE + AUTONOMY_TIMER: explorar a borda"),
            descansando / sim.AgoraMs,
            gestos == 0 ? 0 : (double)calmos / gestos,
            gestos,
            regras.Count(r => r == "CURIOSIDADE: olha a janela"),
            regras.Count(r => r == "IDLE + AUTONOMY_TIMER: explorar a borda"),
            regras);
    }

    [Teste]
    public static void Energias_ObservavelmenteDistintas()
    {
        Medida baixa = Medir(NivelDeEnergia.Baixa), media = Medir(NivelDeEnergia.Media), alta = Medir(NivelDeEnergia.Alta);
        string Linha(string nome, Medida m) => $"{nome}: {m.Caminhadas} caminhadas ({m.Explorações} na borda), {m.FracaoDescansando:P0} descansando, {m.FracaoCalmos:P0} dos {m.Gestos} gestos da agenda calmos, {m.Aproximacoes} olhares da janela";
        Console.WriteLine($"         {Linha("Baixa", baixa)}");
        Console.WriteLine($"         {Linha("Média", media)}");
        Console.WriteLine($"         {Linha("Alta", alta)}");
        Afirmar.Verdadeiro(baixa.Caminhadas < media.Caminhadas && media.Caminhadas < alta.Caminhadas, "anda mais com mais energia");
        Afirmar.Verdadeiro(baixa.FracaoDescansando > media.FracaoDescansando && media.FracaoDescansando > alta.FracaoDescansando, "descansa menos com mais energia");
        Afirmar.Verdadeiro(baixa.FracaoCalmos > media.FracaoCalmos && media.FracaoCalmos > alta.FracaoCalmos, "gestos mais calmos com menos energia");
        Afirmar.Verdadeiro(baixa.Aproximacoes < media.Aproximacoes && media.Aproximacoes < alta.Aproximacoes, "chega perto da janela mais vezes com mais energia");
        Afirmar.Verdadeiro(baixa.Aproximacoes >= 3, $"mesmo na Baixa, a curiosidade aparece ({baixa.Aproximacoes})");
        // A mesma física nos três níveis (invariante 12): a energia só tem frequências e tempos; o perfil não carrega
        // parâmetros de movimento (PersonalidadeDaFase7Testes.Perfis_SemFisica), e a física é a da configuração.
    }

    // Critério 1: a mesma semente e os mesmos eventos dão as mesmas escolhas, com a curiosidade e a personalidade ligadas.
    [Teste]
    public static void MesmaSemente_MesmasEscolhas()
    {
        foreach (NivelDeEnergia energia in new[] { NivelDeEnergia.Baixa, NivelDeEnergia.Alta })
        {
            Medida a = Medir(energia, 21, horas: 1), b = Medir(energia, 21, horas: 1), c = Medir(energia, 22, horas: 1);
            Afirmar.Sequencia(a.Regras, b.Regras, $"{energia}: as mesmas transições");
            Afirmar.Falso(a.Regras.SequenceEqual(c.Regras), $"{energia}: outra semente, outra sequência");
        }
    }
}
