using System.Xml;
using System.Xml.Linq;

namespace Buzzy.PortaoApis;

/// <summary>
/// Confere o manifesto do aplicativo: requestedExecutionLevel com level="asInvoker" e
/// uiAccess="false" (SECURITY.md 8, item 5) e dpiAwareness PerMonitorV2 (ARCHITECTURE.md 2.4 e
/// 2.13.3). Ausência, posição que o Windows ignora ou valor diferente é violação. XML
/// malformado lança <see cref="XmlException"/>, que o portão trata como erro de leitura.
/// </summary>
internal static class VerificadorDeManifesto
{
    public const string NamespaceDpiAwareness = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";

    // Valores que o Windows reconhece em dpiAwareness; ele usa o primeiro reconhecido da lista.
    private static readonly string[] ValoresDeDpi = ["unaware", "system", "permonitor", "permonitorv2"];

    public static IReadOnlyList<Violacao> Verificar(string arquivo) => VerificarTexto(arquivo, File.ReadAllText(arquivo));

    public static IReadOnlyList<Violacao> VerificarTexto(string arquivo, string xml)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(xml);

        var configuracao = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        XDocument documento;
        using (var texto = new StringReader(xml))
        using (var leitor = XmlReader.Create(texto, configuracao))
        {
            documento = XDocument.Load(leitor, LoadOptions.SetLineInfo);
        }

        var violacoes = new List<Violacao>();
        void Acusar(XObject? onde, string api, string detalhe)
        {
            var posicao = onde as IXmlLineInfo;
            bool temLinha = posicao is not null && posicao.HasLineInfo();
            violacoes.Add(new Violacao(arquivo, temLinha ? posicao!.LineNumber : 0, temLinha ? posicao!.LinePosition : 0,
                Codigos.Manifesto, Categoria.Manifesto, api, detalhe, null));
        }

        VerificarNivelDeExecucao(documento, Acusar);
        VerificarDpi(documento, Acusar);
        return violacoes;
    }

    private static void VerificarNivelDeExecucao(XDocument documento, Action<XObject?, string, string> acusar)
    {
        List<XElement> niveis = [.. documento.Descendants().Where(e => e.Name.LocalName == "requestedExecutionLevel")];
        if (niveis.Count == 0)
        {
            acusar(documento.Root, "requestedExecutionLevel",
                "requestedExecutionLevel ausente: o manifesto precisa declarar level=\"asInvoker\" e uiAccess=\"false\" (SECURITY.md 8, item 5)");
            return;
        }
        if (niveis.Count > 1)
            acusar(niveis[1], "requestedExecutionLevel", $"requestedExecutionLevel repetido ({niveis.Count} ocorrências): o manifesto precisa de exatamente um");

        foreach (XElement nivel in niveis)
        {
            if (!NoCaminho(nivel, "assembly", "trustInfo", "security", "requestedPrivileges"))
                acusar(nivel, "requestedExecutionLevel",
                    "requestedExecutionLevel fora de assembly/trustInfo/security/requestedPrivileges: o Windows não o lê");

            XAttribute? level = nivel.Attribute("level");
            if (level is null)
                acusar(nivel, "requestedExecutionLevel level", "atributo level ausente: exigido level=\"asInvoker\", sem elevação");
            else if (!string.Equals(level.Value.Trim(), "asInvoker", StringComparison.OrdinalIgnoreCase))
                acusar(level, "requestedExecutionLevel level", $"level=\"{level.Value}\": exigido level=\"asInvoker\", sem elevação");

            XAttribute? uiAccess = nivel.Attribute("uiAccess");
            if (uiAccess is null)
                acusar(nivel, "requestedExecutionLevel uiAccess", "atributo uiAccess ausente: exigido uiAccess=\"false\"");
            else if (!string.Equals(uiAccess.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase))
                acusar(uiAccess, "requestedExecutionLevel uiAccess",
                    $"uiAccess=\"{uiAccess.Value}\": exigido uiAccess=\"false\" (uiAccess permite enviar input a janelas de outros processos, inclusive elevadas)");
        }
    }

    private static void VerificarDpi(XDocument documento, Action<XObject?, string, string> acusar)
    {
        List<XElement> todos = [.. documento.Descendants().Where(e => e.Name.LocalName == "dpiAwareness")];
        foreach (XElement fora in todos.Where(e => e.Name.NamespaceName != NamespaceDpiAwareness))
        {
            acusar(fora, "dpiAwareness",
                $"dpiAwareness no namespace \"{fora.Name.NamespaceName}\": o Windows só lê o de {NamespaceDpiAwareness}");
        }

        List<XElement> validos = [.. todos.Where(e => e.Name.NamespaceName == NamespaceDpiAwareness)];
        if (validos.Count == 0)
        {
            acusar(documento.Root, "dpiAwareness",
                $"dpiAwareness ausente: exigido PerMonitorV2 em application/windowsSettings, no namespace {NamespaceDpiAwareness} (ARCHITECTURE.md 2.4)");
            return;
        }
        if (validos.Count > 1)
            acusar(validos[1], "dpiAwareness", $"dpiAwareness repetido ({validos.Count} ocorrências): o manifesto precisa de exatamente um");

        foreach (XElement dpi in validos)
        {
            if (!NoCaminho(dpi, "assembly", "application", "windowsSettings"))
                acusar(dpi, "dpiAwareness", "dpiAwareness fora de assembly/application/windowsSettings: o Windows não o lê");

            string? primeiro = dpi.Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(v => ValoresDeDpi.Contains(v, StringComparer.OrdinalIgnoreCase));
            if (!string.Equals(primeiro, "PerMonitorV2", StringComparison.OrdinalIgnoreCase))
                acusar(dpi, "dpiAwareness",
                    $"dpiAwareness=\"{dpi.Value.Trim()}\": o primeiro valor que o Windows reconhece precisa ser PerMonitorV2");
        }
    }

    /// <summary>Se os ancestrais do elemento, do pai até a raiz, têm estes nomes locais.</summary>
    private static bool NoCaminho(XElement elemento, params string[] ancestraisDaRaizAoPai)
    {
        XElement? atual = elemento.Parent;
        for (int i = ancestraisDaRaizAoPai.Length - 1; i >= 0; i--)
        {
            if (atual is null || atual.Name.LocalName != ancestraisDaRaizAoPai[i]) return false;
            atual = atual.Parent;
        }
        return atual is null;
    }
}
