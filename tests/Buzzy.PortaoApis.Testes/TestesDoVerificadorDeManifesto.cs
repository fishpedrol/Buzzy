using System.Xml;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>Manifesto: asInvoker, uiAccess="false" e dpiAwareness PerMonitorV2, com manifestos de exemplo em texto.</summary>
public sealed class TestesDoVerificadorDeManifesto
{
    private const string Arquivo = @"C:\src\app.manifest";
    private const string Nivel = """<requestedExecutionLevel level="asInvoker" uiAccess="false" />""";
    private const string Dpi = """<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>""";
    // DEC-043: o supportedOS do Windows 10, que o Windows 11 compartilha.
    private const string Compatibilidade = """<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" /></application></compatibility>""";

    internal static string Manifesto(string? nivel = null, string? dpi = null, string? extraNaRaiz = null, string? compatibilidade = null) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
          <assemblyIdentity version="1.0.0.0" name="Buzzy.app" />
          <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
            <security>
              <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
                {nivel ?? Nivel}
              </requestedPrivileges>
            </security>
          </trustInfo>
          {compatibilidade ?? Compatibilidade}
          <application xmlns="urn:schemas-microsoft-com:asm.v3">
            <windowsSettings>
              {dpi ?? Dpi}
              <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
            </windowsSettings>
          </application>
          {extraNaRaiz ?? ""}
        </assembly>
        """;

    private static List<Violacao> Verificar(string xml) => [.. VerificadorDeManifesto.VerificarTexto(Arquivo, xml)];

    private static string DpiCom(string valor) => $"""<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">{valor}</dpiAwareness>""";

    [Teste]
    public void ManifestoCorretoPassa()
    {
        List<Violacao> violacoes = Verificar(Manifesto());
        Afirmar.Igual(0, violacoes.Count, string.Join("; ", violacoes.Select(v => v.Detalhe)));
    }

    [Teste]
    public void ManifestoRealDoPrototipoPassa()
    {
        string caminho = Path.Combine(Repositorio.PastaDoSpike, "app.manifest");
        Afirmar.Verdadeiro(File.Exists(caminho), $"manifesto do protótipo não encontrado: {caminho}");
        Afirmar.Igual(0, VerificadorDeManifesto.Verificar(caminho).Count);
    }

    // DEC-043: sem o GUID do Windows 10 (que o Windows 11 compartilha), o Windows trata o aplicativo como um do Windows 8.
    [Teste]
    public void SupportedOsDoWindows10E11_Exigido()
    {
        Violacao ausente = Verificar(Manifesto(compatibilidade: "")).Single();
        Afirmar.Igual("supportedOS", ausente.Api);
        Afirmar.Contem("supportedOS do Windows 10 e 11 ausente", ausente.Detalhe);
        Afirmar.Igual(Categoria.Manifesto, ausente.Categoria);

        // Só o GUID do Windows 8.1 não basta.
        const string Windows81 = """<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}" /></application></compatibility>""";
        Afirmar.Contem("ausente", Verificar(Manifesto(compatibilidade: Windows81)).Single().Detalhe);

        // Os dois juntos passam, em qualquer caixa: o do Windows 10 e 11 é o que importa.
        const string Ambos = """<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}" /><supportedOS Id="{8E0F7A12-BFB3-4FE8-B9A5-48FD50A15A9A}" /></application></compatibility>""";
        Afirmar.Igual(0, Verificar(Manifesto(compatibilidade: Ambos)).Count);

        // Fora de compatibility/application, ou noutro namespace, o Windows não o lê.
        List<Violacao> fora = Verificar(Manifesto(compatibilidade: "", extraNaRaiz: """<supportedOS xmlns="urn:schemas-microsoft-com:compatibility.v1" Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />"""));
        Afirmar.Verdadeiro(fora.Any(v => v.Detalhe.Contains("fora de assembly/compatibility/application", StringComparison.Ordinal)), string.Join("; ", fora.Select(v => v.Detalhe)));
        Afirmar.Verdadeiro(fora.Any(v => v.Detalhe.Contains("ausente", StringComparison.Ordinal)));
        List<Violacao> namespaceErrado = Verificar(Manifesto(compatibilidade: """<compatibility xmlns="urn:schemas-microsoft-com:asm.v1"><application><supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" /></application></compatibility>"""));
        Afirmar.Igual(2, namespaceErrado.Count, string.Join("; ", namespaceErrado.Select(v => v.Detalhe)));
        Afirmar.Verdadeiro(namespaceErrado.Any(v => v.Detalhe.Contains("namespace", StringComparison.Ordinal)));
    }

    [Teste]
    public void ElevacaoEhViolacao()
    {
        string[] elevados = ["requireAdministrator", "highestAvailable"];
        foreach (string nivel in elevados)
        {
            Violacao violacao = Verificar(Manifesto(nivel: $"""<requestedExecutionLevel level="{nivel}" uiAccess="false" />""")).Single();
            Afirmar.Igual("requestedExecutionLevel level", violacao.Api);
            Afirmar.Contem(nivel, violacao.Detalhe);
            Afirmar.Igual(Categoria.Manifesto, violacao.Categoria);
            Afirmar.Igual(Codigos.Manifesto, violacao.Codigo);
            Afirmar.Igual(7, violacao.Linha);
        }
    }

    [Teste]
    public void UiAccessVerdadeiroOuAusenteEhViolacao()
    {
        Violacao verdadeiro = Verificar(Manifesto(nivel: """<requestedExecutionLevel level="asInvoker" uiAccess="true" />""")).Single();
        Afirmar.Igual("requestedExecutionLevel uiAccess", verdadeiro.Api);
        Afirmar.Contem("uiAccess=\"true\"", verdadeiro.Detalhe);

        Violacao ausente = Verificar(Manifesto(nivel: """<requestedExecutionLevel level="asInvoker" />""")).Single();
        Afirmar.Contem("uiAccess ausente", ausente.Detalhe);

        Violacao semLevel = Verificar(Manifesto(nivel: """<requestedExecutionLevel uiAccess="false" />""")).Single();
        Afirmar.Contem("level ausente", semLevel.Detalhe);
    }

    [Teste]
    public void NivelAusenteRepetidoOuForaDoLugarEhViolacao()
    {
        Violacao ausente = Verificar(Manifesto(nivel: "")).Single();
        Afirmar.Contem("requestedExecutionLevel ausente", ausente.Detalhe);

        Violacao repetido = Verificar(Manifesto(nivel: Nivel + Nivel)).Single();
        Afirmar.Contem("repetido", repetido.Detalhe);

        // Direto na raiz o Windows não lê; o nível correto dentro de trustInfo também falta.
        List<Violacao> fora = Verificar(Manifesto(nivel: "", extraNaRaiz: Nivel));
        Afirmar.Verdadeiro(fora.Any(v => v.Detalhe.Contains("fora de assembly/trustInfo", StringComparison.Ordinal)),
            string.Join("; ", fora.Select(v => v.Detalhe)));
    }

    [Teste]
    public void DpiDiferenteDePerMonitorV2EhViolacao()
    {
        string[] invalidos = ["PerMonitor", "System", "Unaware", "PerMonitor, PerMonitorV2", "true/pm", "Invalido"];
        foreach (string valor in invalidos)
        {
            Violacao violacao = Verificar(Manifesto(dpi: DpiCom(valor))).Single();
            Afirmar.Igual("dpiAwareness", violacao.Api, valor);
            Afirmar.Contem("PerMonitorV2", violacao.Detalhe);
        }
    }

    [Teste]
    public void DpiAceitaListaComPerMonitorV2Primeiro()
    {
        string[] validos = ["PerMonitorV2", "PerMonitorV2, PerMonitor", "permonitorv2", " PerMonitorV2 ", "Invalido, PerMonitorV2"];
        foreach (string valor in validos)
            Afirmar.Igual(0, Verificar(Manifesto(dpi: DpiCom(valor))).Count, valor);
    }

    [Teste]
    public void DpiAusenteOuNoNamespaceErradoEhViolacao()
    {
        // Só o dpiAware antigo não basta.
        Violacao ausente = Verificar(Manifesto(dpi: "")).Single();
        Afirmar.Contem("dpiAwareness ausente", ausente.Detalhe);

        List<Violacao> errado = Verificar(Manifesto(dpi: """<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">PerMonitorV2</dpiAwareness>"""));
        Afirmar.Igual(2, errado.Count, string.Join("; ", errado.Select(v => v.Detalhe)));
        Afirmar.Verdadeiro(errado.Any(v => v.Detalhe.Contains("namespace", StringComparison.Ordinal)));
        Afirmar.Verdadeiro(errado.Any(v => v.Detalhe.Contains("ausente", StringComparison.Ordinal)));

        Violacao fora = Verificar(Manifesto(dpi: "", extraNaRaiz: Dpi)).Single(v => v.Detalhe.Contains("fora de", StringComparison.Ordinal));
        Afirmar.Igual("dpiAwareness", fora.Api);
    }

    [Teste]
    public void XmlMalformadoOuComDtdEhErroDeLeitura()
    {
        Afirmar.Lanca<XmlException>(() => Verificar("<assembly><trustInfo></assembly>"));
        Afirmar.Lanca<XmlException>(() => Verificar("""<?xml version="1.0"?><!DOCTYPE assembly [<!ENTITY x "y">]><assembly>&x;</assembly>"""));
    }
}
