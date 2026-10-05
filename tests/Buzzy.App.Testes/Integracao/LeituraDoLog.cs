using System.IO;
using System.Text;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Marca e leitura do log de diagnóstico do Buzzy que atravessam a rotação dele (Diagnostico.cs: ao passar de 1 MB, o
/// arquivo vira <c>diagnostico.1.log</c> e recomeça vazio). Usada pelos testes de integração (<see cref="BuzzyEmTeste"/>) e
/// pela verificação de tela (<c>LogDoBuzzy</c>, que compila este arquivo junto): um só jeito de ler, nos dois.
///
/// A marca é um número opaco: o deslocamento no arquivo, nos 32 bits de baixo, e uma assinatura dos últimos bytes antes
/// dele, nos 32 de cima. O log só cresce (linhas inteiras acrescentadas no fim), e só o Buzzy o rotaciona; por isso, na
/// leitura, a assinatura diz em que arquivo está o trecho marcado: no atual (sem rotação: dali em diante), na cópia (uma
/// rotação: o resto da cópia e depois o arquivo novo inteiro, mesmo que ele já tenha passado do deslocamento da marca) ou
/// em nenhum (duas rotações ou mais: a cópia e o atual são inteiros de depois da marca; o resto do arquivo marcado se
/// perdeu). Sem assinatura (marca crua, no começo do arquivo ou com a última linha ainda sendo escrita), vale o critério
/// antigo: o arquivo atual menor que a marca foi rotacionado (revisão de correção do app, achado 6: só esse critério
/// perdia em silêncio as linhas entre a marca e a rotação quando o arquivo novo crescia além da marca).
/// </summary>
internal static class LeituraDoLog
{
    /// <summary>Quantos bytes antes da marca entram na assinatura.</summary>
    private const int BytesDaAssinatura = 64;

    /// <summary>A marca do fim atual de <paramref name="arquivo"/> (0 sem o arquivo), para ler depois só o que vier dali.</summary>
    internal static long Marcar(string arquivo)
    {
        try
        {
            using var fs = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long tamanho = fs.Length;
            if (tamanho > uint.MaxValue) return tamanho; // não acontece com o limite de 1 MB; sem assinatura, por segurança
            return ((long)Assinatura(fs, tamanho) << 32) | tamanho;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return 0;
        }
    }

    /// <summary>O deslocamento da marca no arquivo.</summary>
    internal static long Deslocamento(long marca) => marca & 0xFFFF_FFFFL;

    /// <summary>
    /// As linhas escritas desde a <paramref name="marca"/>, na ordem: do arquivo <paramref name="atual"/> ou, se ele foi
    /// rotacionado depois dela, o resto de <paramref name="rotacionado"/> e depois o atual inteiro.
    /// </summary>
    internal static List<string> LinhasDesde(string atual, string rotacionado, long marca)
    {
        long deslocamento = Deslocamento(marca);
        uint assinatura = (uint)((ulong)marca >> 32);
        var linhas = new List<string>();
        FileStream? fsAtual = Abrir(atual);
        try
        {
            if (assinatura != 0)
            {
                if (fsAtual is not null && Bate(fsAtual, deslocamento, assinatura))
                {
                    LerDesde(fsAtual, deslocamento, linhas);
                    return linhas;
                }
                // O arquivo marcado foi rotacionado: é a cópia (uma rotação) ou já foi sobrescrito por outra (duas ou mais,
                // e aí a cópia inteira é de depois da marca).
                using FileStream? fsCopia = Abrir(rotacionado);
                if (fsCopia is not null) LerDesde(fsCopia, Bate(fsCopia, deslocamento, assinatura) ? deslocamento : 0, linhas);
                if (fsAtual is not null) LerDesde(fsAtual, 0, linhas);
                return linhas;
            }

            // Critério antigo, sem assinatura: o arquivo atual menor que a marca foi rotacionado.
            if (fsAtual is null) return linhas;
            if (deslocamento > fsAtual.Length)
            {
                // Uma cópia menor que a marca (de outra rotação) não tem nada depois dela: a leitura não traz linha nenhuma.
                using FileStream? fsCopia = Abrir(rotacionado);
                if (fsCopia is not null) LerDesde(fsCopia, deslocamento, linhas);
                deslocamento = 0;
            }
            LerDesde(fsAtual, deslocamento, linhas);
            return linhas;
        }
        finally
        {
            fsAtual?.Dispose();
        }
    }

    private static FileStream? Abrir(string arquivo)
    {
        try
        {
            return new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static bool Bate(FileStream fs, long deslocamento, uint assinatura)
        => fs.Length >= deslocamento && Assinatura(fs, deslocamento) == assinatura;

    /// <summary>
    /// A assinatura FNV-1a dos até <see cref="BytesDaAssinatura"/> bytes antes de <paramref name="fim"/>; 0 (sem assinatura)
    /// no começo do arquivo e quando o trecho não termina numa quebra de linha ou tem um byte nulo (uma linha ainda sendo
    /// escrita: o trecho mudaria e a assinatura não bateria mais). Nunca 0 quando há assinatura.
    /// </summary>
    private static uint Assinatura(FileStream fs, long fim)
    {
        int n = (int)Math.Min(BytesDaAssinatura, fim);
        if (n == 0) return 0;
        byte[] bytes = new byte[n];
        fs.Seek(fim - n, SeekOrigin.Begin);
        int lidos = 0;
        while (lidos < n)
        {
            int r = fs.Read(bytes, lidos, n - lidos);
            if (r <= 0) return 0;
            lidos += r;
        }
        if (bytes[^1] != (byte)'\n' || Array.IndexOf(bytes, (byte)0) >= 0) return 0;
        uint h = 2166136261;
        foreach (byte b in bytes) h = unchecked((h ^ b) * 16777619);
        return h == 0 ? 1 : h;
    }

    private static void LerDesde(FileStream fs, long deslocamento, List<string> linhas)
    {
        if (deslocamento > fs.Length) return;
        fs.Seek(deslocamento, SeekOrigin.Begin);
        using var sr = new StreamReader(fs, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        string? linha;
        while ((linha = sr.ReadLine()) is not null) linhas.Add(linha);
    }
}
