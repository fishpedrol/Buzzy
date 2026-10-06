using System.Xml;
using System.Xml.Linq;

namespace Buzzy.PortaoApis;

/// <summary>
/// Confere o manifesto do aplicativo: requestedExecutionLevel com level="asInvoker" e
/// uiAccess="false" (SECURITY.md 8, item 5), dpiAwareness PerMonitorV2 (ARCHITECTURE.md 2.4 e
/// 2.13.3) e o supportedOS do Windows 10 e 11 em compatibility/application (DEC-043). Ausência,
/// posição que o Windows ignora ou valor diferente é violação. XML malformado lança
/// <see cref="XmlException"/>, que o portão trata como erro de leitura.
/// </summary>
internal static class VerificadorDeManifesto
{
    public const string NamespaceDpiAwareness = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
    public const string NamespaceCompatibilidade = "urn:schemas-microsoft-com:compatibility.v1";

    /// <summary>O GUID de supportedOS do Windows 10, que o Windows 11 compartilha (DEC-043).</summary>
    public const string SupportedOsWindows10E11 = "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}";

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
        VerificarCompatibilidade(documento, Acusar);
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

    /// <summary>
    /// DEC-043: o manifesto declara o Windows 10 e 11 (um só GUID para os dois). Sem a declaração, ou com ela onde o Windows
    /// não a lê, ele informa ao aplicativo a versão do Windows 8 e aplica o comportamento de compatibilidade dela. Outros
    /// GUIDs ao lado (Windows 7, 8, 8.1) não reprovam: o que importa é o do Windows 10 e 11 estar lá.
    /// </summary>
    private static void VerificarCompatibilidade(XDocument documento, Action<XObject?, string, string> acusar)
    {
        List<XElement> todos = [.. documento.Descendants().Where(e => e.Name.LocalName == "supportedOS")];
        foreach (XElement fora in todos.Where(e => e.Name.NamespaceName != NamespaceCompatibilidade))
            acusar(fora, "supportedOS", $"supportedOS no namespace \"{fora.Name.NamespaceName}\": o Windows só lê o de {NamespaceCompatibilidade}");

        List<XElement> validos = [.. todos.Where(e => e.Name.NamespaceName == NamespaceCompatibilidade)];
        foreach (XElement fora in validos.Where(e => !NoCaminho(e, "assembly", "compatibility", "application")))
            acusar(fora, "supportedOS", "supportedOS fora de assembly/compatibility/application: o Windows não o lê");

        bool windows10E11 = validos.Any(e => NoCaminho(e, "assembly", "compatibility", "application")
            && string.Equals(e.Attribute("Id")?.Value.Trim(), SupportedOsWindows10E11, StringComparison.OrdinalIgnoreCase));
        if (!windows10E11)
            acusar(validos.Count > 0 ? validos[0] : documento.Root, "supportedOS",
                $"supportedOS do Windows 10 e 11 ausente: o manifesto precisa declarar <supportedOS Id=\"{SupportedOsWindows10E11}\" /> em assembly/compatibility/application, no namespace {NamespaceCompatibilidade}; sem ele, o Windows informa ao aplicativo a versão do Windows 8 (DEC-043)");
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
