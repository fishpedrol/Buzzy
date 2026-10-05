using System.Text;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>
/// Leitura da tabela de importação nativa e a lista de permissões do apphost, contra um apphost
/// real gerado pelo SDK (spikes/BuzzySpike/bin/Release/net10.0-windows/BuzzySpike.exe, só leitura).
/// </summary>
public sealed class TestesDeImportacoesNativas : IDisposable
{
    private static readonly string[] ImportacoesDoLancadorNaListaProibida =
        ["KERNEL32.dll!GetProcAddress", "KERNEL32.dll!LoadLibraryA", "KERNEL32.dll!LoadLibraryExW", "SHELL32.dll!ShellExecuteW"];

    private readonly PastaTemporaria _pasta = new();

    public void Dispose() => _pasta.Dispose();

    [Teste]
    public void LeAsImportacoesDeUmApphostReal()
    {
        IReadOnlyList<ImportacaoNativa> importacoes = LeitorDeImportacoesNativas.Ler(Repositorio.ApphostReal());
        var lidas = importacoes.Select(i => i.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] esperadas =
        [
            "KERNEL32.dll!LoadLibraryExW", "KERNEL32.dll!GetProcAddress", "KERNEL32.dll!LoadLibraryA", "KERNEL32.dll!GetModuleFileNameW",
            "SHELL32.dll!ShellExecuteW", "USER32.dll!MessageBoxW", "ADVAPI32.dll!RegOpenKeyExW", "ADVAPI32.dll!RegGetValueW",
        ];
        foreach (string esperada in esperadas)
            Afirmar.Verdadeiro(lidas.Contains(esperada), $"{esperada} não foi lida; {importacoes.Count} importações lidas");
        Afirmar.Verdadeiro(importacoes.Count > 50, $"só {importacoes.Count} importações lidas");
        Afirmar.Falso(importacoes.Any(i => i.PorOrdinal), "o apphost não importa por ordinal");
        Afirmar.Falso(importacoes.Any(i => i.CargaAtrasada), "o apphost não tem importação com carga atrasada");
    }

    [Teste]
    public void ApphostRealPassaSoComAsPermissoesExplicitas()
    {
        string apphost = Repositorio.ApphostReal();
        AnaliseDeImportacoes analise = VerificadorDeImportacoesNativas.Avaliar(apphost, LeitorDeImportacoesNativas.Ler(apphost), ehApphost: true);

        Afirmar.Igual(0, analise.Violacoes.Count, string.Join("; ", analise.Violacoes.Select(v => v.Api)));
        Afirmar.Sequencia(ImportacoesDoLancadorNaListaProibida, analise.Permitidas.Select(p => p.Api).Order(StringComparer.Ordinal));
        Afirmar.Igual(Categoria.Processos, analise.Permitidas.Single(p => p.Api == "SHELL32.dll!ShellExecuteW").Categoria);
    }

    [Teste]
    public void ListaDePermissoesNaoTemEntradaSobrando()
    {
        // Cada permissão existe no apphost real e coincide com a lista proibida: nenhuma permissão morta.
        IReadOnlyList<ImportacaoNativa> importacoes = LeitorDeImportacoesNativas.Ler(Repositorio.ApphostReal());
        foreach (PermissaoDoApphost permissao in PermissoesDoApphost.Entradas)
        {
            string nome = $"{permissao.Modulo}!{permissao.Funcao}";
            Afirmar.Verdadeiro(
                importacoes.Any(i => ListaProibida.NormalizarModulo(i.Modulo) == permissao.Modulo && i.Funcao == permissao.Funcao),
                $"{nome} não está no apphost real");
            Afirmar.NaoNulo(ListaProibida.ProcurarNativa(permissao.Modulo, permissao.Funcao), $"{nome} não precisa de permissão");
            Afirmar.Verdadeiro(permissao.Motivo.Length > 30, $"{nome} sem motivo");
        }
    }

    [Teste]
    public void AsMesmasImportacoesForaDoApphostSaoViolacoes()
    {
        string apphost = Repositorio.ApphostReal();
        AnaliseDeImportacoes analise = VerificadorDeImportacoesNativas.Avaliar(apphost, LeitorDeImportacoesNativas.Ler(apphost), ehApphost: false);

        Afirmar.Igual(0, analise.Permitidas.Count);
        Afirmar.Sequencia(ImportacoesDoLancadorNaListaProibida, analise.Violacoes.Select(v => v.Api).Order(StringComparer.Ordinal));
        Afirmar.Verdadeiro(analise.Violacoes.All(v => v.Codigo == Codigos.ImportacaoNativa));
    }

    [Teste]
    public void PermissaoCasaModuloEFuncaoExatos()
    {
        ImportacaoNativa[] importacoes =
        [
            new("KERNEL32.dll", "LoadLibraryW", false),
            new("SHELL32.dll", "ShellExecuteA", false),
            new("user32.dll", "GetProcAddress", false),
            new("USER32.dll", "SendInput", false),
            new("WS2_32.dll", "connect", false),
            new("kernel32.DLL", "LoadLibraryExW", false),
            new("USER32.dll", "#123", false),
            new("USER32.dll", "MessageBoxW", false),
        ];
        AnaliseDeImportacoes analise = VerificadorDeImportacoesNativas.Avaliar(@"C:\x\Buzzy.exe", importacoes, ehApphost: true);

        Afirmar.Sequencia<string>(["kernel32.DLL!LoadLibraryExW"], analise.Permitidas.Select(p => p.Api));
        Afirmar.Sequencia<string>(
            ["KERNEL32.dll!LoadLibraryW", "SHELL32.dll!ShellExecuteA", "user32.dll!GetProcAddress", "USER32.dll!SendInput", "WS2_32.dll!connect", "USER32.dll!#123"],
            analise.Violacoes.Select(v => v.Api));
        Afirmar.Igual(Categoria.Rede, analise.Violacoes.Single(v => v.Api == "WS2_32.dll!connect").Categoria);
        Violacao ordinal = analise.Violacoes.Single(v => v.Api == "USER32.dll!#123");
        Afirmar.Igual(Categoria.CodigoDinamico, ordinal.Categoria);
        Afirmar.Contem("ordinal", ordinal.Detalhe);
    }

    [Teste]
    public void ImportacaoComCargaAtrasadaTambemEhConferida()
    {
        AnaliseDeImportacoes analise = VerificadorDeImportacoesNativas.Avaliar(
            "Buzzy.exe", [new ImportacaoNativa("user32.dll", "GetForegroundWindow", true)], ehApphost: true);
        Violacao violacao = analise.Violacoes.Single();
        Afirmar.Igual(Categoria.LerOutrosAplicativos, violacao.Categoria);
        Afirmar.Contem("carga atrasada", violacao.Detalhe);
    }

    [Teste]
    public void LeAImportacaoDeUmPe32()
    {
        // O ManagedPEBuilder grava PE32 com a importação mscoree.dll!_CorDllMain.
        string caminho = Path.Combine(_pasta.Caminho, "Sintetico.Pe32.dll");
        new AssemblySintetico("Sintetico.Pe32").Gravar(caminho);
        Afirmar.Sequencia<string>(["mscoree.dll!_CorDllMain"], LeitorDeImportacoesNativas.Ler(caminho).Select(i => i.ToString()));
    }

    [Teste]
    public void ApphostComImportacaoNovaReprova()
    {
        // Cópia do apphost real com nomes trocados na tabela: USER32!MessageBoxW vira SendInput e
        // SHELL32!ShellExecuteW vira ShellExecuteA, que não está na lista de permissões.
        byte[] bytes = File.ReadAllBytes(Repositorio.ApphostReal());
        TrocarNome(bytes, "MessageBoxW", "SendInput");
        TrocarNome(bytes, "ShellExecuteW", "ShellExecuteA");
        string copia = Path.Combine(_pasta.Caminho, "Buzzy.exe");
        File.WriteAllBytes(copia, bytes);

        AnaliseDeImportacoes analise = VerificadorDeImportacoesNativas.Avaliar(copia, LeitorDeImportacoesNativas.Ler(copia), ehApphost: true);
        Afirmar.Sequencia<string>(["SHELL32.dll!ShellExecuteA", "USER32.dll!SendInput"], analise.Violacoes.Select(v => v.Api).Order(StringComparer.Ordinal));
        Afirmar.Sequencia<string>(
            ["KERNEL32.dll!GetProcAddress", "KERNEL32.dll!LoadLibraryA", "KERNEL32.dll!LoadLibraryExW"],
            analise.Permitidas.Select(p => p.Api).Order(StringComparer.Ordinal));
    }

    [Teste]
    public void PeTruncadoEhErroDeLeitura()
    {
        byte[] bytes = File.ReadAllBytes(Repositorio.ApphostReal());
        string truncado = Path.Combine(_pasta.Caminho, "truncado.exe");
        File.WriteAllBytes(truncado, bytes[..1024]);
        Afirmar.Lanca<BadImageFormatException>(() => LeitorDeImportacoesNativas.Ler(truncado));
    }

    /// <summary>Troca, no próprio arquivo, o único nome ASCII terminado em zero; o que sobra fica zerado.</summary>
    private static void TrocarNome(byte[] bytes, string de, string para)
    {
        Afirmar.Verdadeiro(para.Length <= de.Length);
        byte[] procurado = Encoding.ASCII.GetBytes(de + "\0");
        List<int> posicoes = [];
        for (int i = bytes.AsSpan().IndexOf(procurado); i >= 0;)
        {
            posicoes.Add(i);
            int proxima = bytes.AsSpan(i + 1).IndexOf(procurado);
            i = proxima < 0 ? -1 : i + 1 + proxima;
        }
        Afirmar.Igual(1, posicoes.Count, $"ocorrências de {de} no apphost");
        Array.Clear(bytes, posicoes[0], de.Length);
        Encoding.ASCII.GetBytes(para).CopyTo(bytes, posicoes[0]);
    }
}
