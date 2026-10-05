using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Buzzy.App.Apresentacao;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;
using Buzzy.Visual.Animacao;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 6, critério 1 (DEC-036, item 2): o núcleo é o mesmo com dois manifestos diferentes. O núcleo não conhece o
/// manifesto (nenhuma referência ao assembly da arte nem ao manifesto no código dele), e dois núcleos iguais, cada um
/// desenhado por um manifesto — o do app e outro, com os quadros de cada clipe em ordem invertida e os tempos dobrados —,
/// passam pelos mesmos estados, evento a evento, por mil passos de vida autônoma com a configuração do app; as duas
/// apresentações escolhem quadros que a arte desenha, e diferentes, o que mostra que o manifesto mudou de fato.
/// </summary>
internal sealed class DoisManifestosTestes
{
    /// <summary>O manifesto do app com cada clipe de quadros invertidos e tempos dobrados: outro manifesto, também válido.</summary>
    private static ManifestoDeClipes Alternativo()
    {
        string fonte = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Apresentacao", "clipes.json"));
        string dobrado = Regex.Replace(fonte, "\"passos\": (\\d+)", m => $"\"passos\": {Math.Min(ManifestoDeClipes.MaximoDePassos, 2 * int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))}");
        string invertido = Regex.Replace(dobrado, "\"quadros\": \\[(.*)\\] \\}", m =>
        {
            string[] quadros = Regex.Matches(m.Groups[1].Value, "\\{[^{}]*\\}").Select(q => q.Value).Reverse().ToArray();
            return $"\"quadros\": [ {string.Join(", ", quadros)} ] }}";
        });
        Afirmar.Diferente(fonte, invertido, "o alternativo é outro arquivo");
        return ManifestoDeClipes.Ler(invertido);
    }

    [Teste]
    public void ONucleoNaoConheceOManifesto_EDoisNucleosComDoisManifestosPassamPelosMesmosEstados()
    {
        // O núcleo não referencia a arte nem o app, e nada no código dele fala do manifesto.
        AssemblyName[] referencias = typeof(Nucleo).Assembly.GetReferencedAssemblies();
        Afirmar.Falso(referencias.Any(r => r.Name is "Buzzy.Visual" or "Buzzy.App"), $"o núcleo referencia só o runtime: {string.Join(", ", referencias.Select(r => r.Name))}");
        string[] fontesDoNucleo = Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src", "Buzzy.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)).ToArray();
        Afirmar.Verdadeiro(fontesDoNucleo.Length > 10, "achou o código do núcleo");
        foreach (string f in fontesDoNucleo)
            Afirmar.Falso(File.ReadAllText(f).Contains("clipes.json", StringComparison.Ordinal) || File.ReadAllText(f).Contains("ManifestoDeClipes", StringComparison.Ordinal), $"{Path.GetFileName(f)}: fala do manifesto");

        ManifestoDeClipes doApp = PoseDoPersonagem.Manifesto, outro = Alternativo();
        Afirmar.Sequencia([], ValidadorDeClipes.Validar(outro), "o alternativo também passa na validação do build");

        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(SpriteProvisorio.TamanhoLogico);
        var a = new Nucleo(cfg, 7);
        var b = new Nucleo(cfg, 7);
        var carga = new Loaded(new Topologia([new MonitorDoDesktop(@"\\.\DISPLAY1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true)]), null, Preferencias.Padrao);
        int diferentes = 0;
        (bool Relogio, long? Geracao) pa = (false, null), pb = (false, null);
        long passosA = 0, passosB = 0;
        Estado estadoA = Estado.Booting, estadoB = Estado.Booting;
        Evento proximo = carga;
        for (int i = 0; i < 1000; i++)
        {
            pa = Aplicar(a, proximo, pa);
            pb = Aplicar(b, proximo, pb);
            Afirmar.Igual(a.Estado.Retrato().ToString(), b.Estado.Retrato().ToString(), $"evento {i} ({proximo}): o mesmo retrato");
            Afirmar.Igual(a.Estado.Passos, b.Estado.Passos, $"evento {i}: os mesmos passos");
            // Cada apresentação, como a raiz faz, com os passos desde a entrada no estado.
            if (a.Estado.Estado != estadoA) { estadoA = a.Estado.Estado; passosA = a.Estado.Passos; }
            if (b.Estado.Estado != estadoB) { estadoB = b.Estado.Estado; passosB = b.Estado.Passos; }
            if (a.Estado.Estado.Visivel())
            {
                QuadroDoSprite qa = PoseDoPersonagem.Escolher(doApp, a.Retrato, a.Estado.Passos - passosA);
                QuadroDoSprite qb = PoseDoPersonagem.Escolher(outro, b.Retrato, b.Estado.Passos - passosB);
                Afirmar.Verdadeiro(SpriteProvisorio.Compor(qa).Limites() is not null && SpriteProvisorio.Compor(qb).Limites() is not null, $"evento {i}: os dois quadros se desenham");
                if (qa != qb) diferentes++;
            }
            if (pa.Relogio) proximo = new Tick();
            else if (pa.Geracao is { } g) proximo = new AutonomyTimer(g);
            else break;
        }
        Afirmar.Verdadeiro(a.Estado.Passos > 200, $"houve movimento: {a.Estado.Passos} passos");
        Afirmar.Verdadeiro(diferentes > 0, $"os dois manifestos desenharam quadros diferentes ({diferentes} vezes)");
    }

    /// <summary>Aplica um evento e devolve o relógio e a decisão agendada que os efeitos deixam, como a raiz faria.</summary>
    private static (bool Relogio, long? Geracao) Aplicar(Nucleo n, Evento evento, (bool Relogio, long? Geracao) antes)
    {
        (bool relogio, long? geracao) = antes;
        if (evento is AutonomyTimer) geracao = null;
        n.Enfileirar(evento);
        foreach (Efeito e in n.Processar())
        {
            switch (e)
            {
                case LigarRelogio: relogio = true; break;
                case DesligarRelogio: relogio = false; break;
                case AgendarDecisao d: geracao = d.Geracao; break;
                case CancelarDecisao: geracao = null; break;
            }
        }
        return (relogio, geracao);
    }
}
