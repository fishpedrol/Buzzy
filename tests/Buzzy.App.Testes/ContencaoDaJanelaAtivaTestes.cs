using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 7, passo F7-P2 (DEC-037, itens 2 e 12; SECURITY.md 3.1): a contenção da janela de outro aplicativo, escrita antes da
/// curiosidade. O observador guarda só os ganchos e a thread da geometria, nunca a janela; só ele chama as funções da janela
/// em primeiro plano e lê o retângulo dela, sempre sem pedir o processo e sem ler o instante do evento; só ele e a agenda
/// conhecem a leitura; e nada que cruza para o núcleo (eventos e efeitos) carrega um identificador nativo, nem um retângulo
/// fora dos eventos que já o tinham.
/// </summary>
internal sealed class ContencaoDaJanelaAtivaTestes
{
    private static string Fonte(params string[] partes) => File.ReadAllText(Path.Combine([Caminhos.Raiz, "src", .. partes]));

    private static IEnumerable<string> FontesDoProduto() => Directory.GetFiles(Path.Combine(Caminhos.Raiz, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static FieldInfo[] Campos(Type t) => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static bool Proibido(Type t)
    {
        Type nucleo = Nullable.GetUnderlyingType(t) ?? t;
        return nucleo == typeof(nint) || nucleo == typeof(RetanguloPx) || nucleo == typeof(LeituraDoPrimeiroPlano);
    }

    // C1: os campos do observador, pelo nome; só os dois ganchos são nint, e nenhum guarda texto, retângulo ou leitura.
    [Teste]
    public void Observador_GuardaSoOsGanchosEAThreadDaGeometria()
    {
        FieldInfo[] campos = Campos(typeof(ObservadorDeTelaCheia));
        Afirmar.Sequencia(
            ["<EventosDeGeometria>k__BackingField", "<EventosDePrimeiroPlano>k__BackingField", "Sinal", "_aoEvento", "_ganchoDeGeometria", "_ganchoDePrimeiroPlano", "_threadDaGeometria", "_threadDaInterface"],
            campos.Select(c => c.Name).Order(StringComparer.Ordinal), "os campos do observador");
        Afirmar.Sequencia(["_ganchoDeGeometria", "_ganchoDePrimeiroPlano"], campos.Where(c => c.FieldType == typeof(nint)).Select(c => c.Name).Order(StringComparer.Ordinal), "só os ganchos são nint");
        foreach (FieldInfo c in campos)
            Afirmar.Falso(c.FieldType == typeof(string) || Proibido(c.FieldType) && c.FieldType != typeof(nint), $"{c.Name}: {c.FieldType.Name}");
    }

    // C2: a agenda e a carência do foco nunca guardam a janela, o retângulo, a leitura nem o vão entre avaliações.
    [Teste]
    public void Agenda_NaoGuardaJanelaRetanguloNemLeitura()
    {
        foreach (Type tipo in new[] { typeof(AgendaDaTelaCheia), typeof(CarenciaDoFoco) })
            foreach (FieldInfo c in Campos(tipo))
                Afirmar.Falso(Proibido(c.FieldType) || c.FieldType == typeof(VaoDaJanela) || c.FieldType == typeof(VaoDaJanela?), $"{tipo.Name}.{c.Name}: {c.FieldType.Name}");
        // A carência guarda só chaves opacas de monitor (DEC-037, item 2).
        Afirmar.Sequencia(["<Candidato>k__BackingField", "<Publicado>k__BackingField", "_agendarUmaVez", "_cancelar", "_parada", "_publicar"],
            Campos(typeof(CarenciaDoFoco)).Select(c => c.Name).Order(StringComparer.Ordinal), "os campos da carência");
    }

    // C5: no arquivo do observador, as chamadas nativas são exatamente as da DEC-034; toda GetWindowThreadProcessId leva o
    // processo nulo (literal 0); o instante do evento (tempo) nunca é lido.
    [Teste]
    public void Observador_ChamaSoAsFuncoesDaDecisao_SemOProcessoNemOInstante()
    {
        string fonte = Fonte("Buzzy.App", "Plataforma", "ObservadorDeTelaCheia.cs");
        string codigo = Regex.Replace(fonte, @"//.*|/\*[\s\S]*?\*/", "");
        string[] chamadas = [.. Regex.Matches(codigo, @"\b(?:Nativo|Win32)\.(\w+)\s*\(").Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(["GetCurrentThreadId", "GetForegroundWindow", "GetWindowRect", "GetWindowThreadProcessId", "SHQueryUserNotificationState", "SetWinEventHook", "UnhookWinEvent"], chamadas, "as chamadas nativas do observador");
        MatchCollection tpid = Regex.Matches(codigo, @"Nativo\.GetWindowThreadProcessId\(([^,()]+),\s*([^)]+)\)");
        Afirmar.Verdadeiro(tpid.Count >= 1, "achou a chamada");
        foreach (Match m in tpid) Afirmar.Igual("0", m.Groups[2].Value.Trim(), $"o processo nulo em {m.Value}");
        int assinatura = codigo.IndexOf("private void AoEvento(", StringComparison.Ordinal);
        Afirmar.Verdadeiro(assinatura >= 0, "achou o tratamento do evento");
        string corpo = codigo[codigo.IndexOf('{', assinatura)..];
        corpo = corpo[..corpo.IndexOf("\n    }", StringComparison.Ordinal)];
        Afirmar.Falso(Regex.IsMatch(corpo, @"\btempo\b"), "o instante do evento nunca é lido");
    }

    // C6 e C7: só as janelas do Buzzy e o observador leem um retângulo de janela; só o observador e a agenda conhecem a leitura.
    [Teste]
    public void SoOObservadorLeAJanela_ESoEleEAAgendaConhecemALeitura()
    {
        string Nome(string f) => Path.GetFileName(f);
        Afirmar.Sequencia(["JanelaDeConfiguracoes.cs", "JanelaDoItem.cs", "JanelaPersonagem.cs", "ObservadorDeTelaCheia.cs", "PainelDeEnergia.cs"],
            FontesDoProduto().Where(f => File.ReadAllText(f).Contains("Win32.GetWindowRect(", StringComparison.Ordinal)).Select(Nome).Order(StringComparer.Ordinal), "quem lê um retângulo de janela");
        Afirmar.Sequencia(["AgendaDaTelaCheia.cs", "ObservadorDeTelaCheia.cs"],
            FontesDoProduto().Where(f => File.ReadAllText(f).Contains("LeituraDoPrimeiroPlano", StringComparison.Ordinal)).Select(Nome).Order(StringComparer.Ordinal), "quem conhece a leitura");
    }

    // C3: nada que cruza para o núcleo carrega um identificador nativo, e um retângulo só nos eventos e efeitos que já o
    // tinham (o mundo do desktop e as janelas do próprio Buzzy): uma janela de outro aplicativo nunca entra por um evento.
    [Teste]
    public void EventosEEfeitos_SemIdentificadorNativo_ERetanguloSoOndeJaHavia()
    {
        Type[] tipos = [.. typeof(Evento).Assembly.GetTypes().Where(t => !t.IsAbstract && (typeof(Evento).IsAssignableFrom(t) || typeof(Efeito).IsAssignableFrom(t)))];
        Afirmar.Verdadeiro(tipos.Length > 40, $"achou os eventos e efeitos ({tipos.Length})");
        var comRetangulo = new List<string>();
        foreach (Type t in tipos)
        {
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Type tipo = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                Afirmar.Falso(tipo == typeof(nint) || tipo == typeof(nuint), $"{t.Name}.{p.Name}: identificador nativo");
                Afirmar.Falso(tipo == typeof(LeituraDoPrimeiroPlano), $"{t.Name}.{p.Name}: a leitura da janela");
                if (tipo == typeof(RetanguloPx)) comRetangulo.Add($"{t.Name}.{p.Name}");
            }
        }
        Afirmar.Sequencia([], comRetangulo.Order(StringComparer.Ordinal), "nenhum evento ou efeito com um retângulo solto");
    }
}
