using System.IO;
using System.Security.Cryptography;
using Buzzy.App.Plataforma;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Fase 9, passo F9-P7 (DEC-040, item 10; SECURITY.md 8, item 4, na parte automática): depois de uma execução que grava
/// (abrir e sair, que grava as configurações no perfil de teste), a pasta de dados do Buzzy só tem os nomes que o código grava, a pasta
/// do executável e a pasta Inicializar do usuário ficam iguais, e os arquivos reais, intocados. O Process Monitor, que
/// exige administrador, fica [MANUAL] com <c>tools\verificar-gravacoes.ps1</c>.
/// </summary>
[Integracao]
internal sealed class GravacoesIntegracaoTestes
{
    private static readonly string[] NaRaiz = ["settings.json", "settings.json.bak", "settings.json.tmp", "settings.corrupt.json", "diagnostico.log", "diagnostico.1.log", "testes"];
    private static readonly string[] NoPerfil = ["settings.json", "settings.json.bak", "settings.json.tmp", "settings.corrupt.json"];

    [Teste]
    public void DepoisDeGravar_SoOsNomesDoBuzzy_EAPastaDoExecutavelIgual()
    {
        string dados = Afirmar.NaoNulo(PastaDeDados.DoBuzzy(), "pasta do Buzzy");
        string exe = Path.GetDirectoryName(BuzzyEmTeste.CaminhoDoExecutavel)!;
        string inicializar = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string reais = ArquivosReais.Foto();
        string exeAntes = Impressao(exe), inicializarAntes = Impressao(inicializar);

        using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar())
        {
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded", 5000, "núcleo carregado");
            // Sair grava as preferências na pasta do perfil (o comando de sair descarrega a gravação).
            Afirmar.Igual(0, b.FecharPorWmClose(), "saída limpa");
        }
        Afirmar.Verdadeiro(File.Exists(Path.Combine(dados, "testes", PerfilDeTeste.Integracao, "settings.json")), "houve gravação: o settings.json do perfil de teste existe");

        foreach (string nome in Directory.EnumerateFileSystemEntries(dados).Select(f => Path.GetFileName(f)))
            Afirmar.Verdadeiro(NaRaiz.Contains(nome, StringComparer.OrdinalIgnoreCase), $"{nome} na pasta de dados não é um nome que o Buzzy grava");
        string testes = Path.Combine(dados, "testes");
        if (Directory.Exists(testes))
        {
            foreach (string perfil in Directory.EnumerateDirectories(testes))
                foreach (string nome in Directory.EnumerateFileSystemEntries(perfil).Select(f => Path.GetFileName(f)))
                    Afirmar.Verdadeiro(NoPerfil.Contains(nome, StringComparer.OrdinalIgnoreCase), $"{nome} em testes\\{Path.GetFileName(perfil)} não é um nome que o Buzzy grava");
            Afirmar.Igual(0, Directory.EnumerateFiles(testes).Count(), "nenhum arquivo solto em testes\\");
        }
        Afirmar.Igual(exeAntes, Impressao(exe), "a pasta do executável não muda (o Buzzy não grava ao lado dele)");
        Afirmar.Igual(inicializarAntes, Impressao(inicializar), "a pasta Inicializar do usuário não muda (nenhum atalho)");
        Afirmar.Igual(reais, ArquivosReais.Foto(), "os arquivos e o registro reais do usuário, só por fora");
    }

    /// <summary>Cada arquivo da pasta (sem as subpastas de build), com o tamanho, a data e o hash; vazia sem a pasta.</summary>
    private static string Impressao(string pasta)
    {
        if (!Directory.Exists(pasta)) return "(sem a pasta)";
        return string.Join(";", Directory.EnumerateFiles(pasta).Order(StringComparer.OrdinalIgnoreCase).Select(f =>
        {
            var info = new FileInfo(f);
            using FileStream fluxo = File.Open(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return $"{info.Name}:{info.Length}:{info.LastWriteTimeUtc:o}:{Convert.ToHexString(SHA256.HashData(fluxo))}";
        }));
    }
}
