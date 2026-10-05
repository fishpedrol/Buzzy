using System.IO;
using System.Text;
using Buzzy.App.Testes.Integracao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// A leitura do log de diagnóstico pelos testes de integração (<see cref="BuzzyEmTeste.EventosDesde(string, string, long)"/>)
/// atravessa a rotação do Buzzy (Diagnostico.cs: passou de 1 MB, o arquivo vira <c>diagnostico.1.log</c> e recomeça
/// vazio): as linhas escritas entre a marca e a rotação vêm da cópia, antes das do arquivo novo. Sem isso, um teste longo
/// (os do tamagotchi escrevem muitas linhas) perderia linhas que esperava. Arquivos numa pasta temporária própria, apagada
/// no fim: nada lê nem grava em %LOCALAPPDATA%\Buzzy.
/// </summary>
internal sealed class LeituraDoLogTestes : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("buzzy-log-").FullName;

    private string Atual => Path.Combine(_pasta, "diagnostico.log");

    private string Rotacionado => Path.Combine(_pasta, "diagnostico.1.log");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"         limpeza: a pasta temporária {_pasta} ficou para o sistema apagar ({e.GetType().Name})");
        }
    }

    [Teste]
    public void SemRotacao_SoAsLinhasDepoisDaMarca()
    {
        Acrescentar(Atual, "A");
        long marca = new FileInfo(Atual).Length;
        Acrescentar(Atual, "B", "C");
        Afirmar.Sequencia(["B", "C"], Chaves(marca), "as linhas depois da marca, na ordem");
        Afirmar.Sequencia(["A", "B", "C"], Chaves(0), "da marca 0, tudo");
    }

    // A rotação se reconhece pelo tamanho: o arquivo novo fica menor que a marca, como na de verdade (a marca perto de 1 MB,
    // o arquivo novo com poucas linhas).
    [Teste]
    public void RotacaoDepoisDaMarca_OTrechoDaCopiaVemAntesDoArquivoNovo()
    {
        Acrescentar(Atual, "A", "A", "A", "A");
        long marca = new FileInfo(Atual).Length;
        Acrescentar(Atual, "B", "C");
        // A rotação do Buzzy: o arquivo vira a cópia, e o próximo evento abre um arquivo novo.
        File.Move(Atual, Rotacionado, overwrite: true);
        Acrescentar(Atual, "D");
        Afirmar.Sequencia(["B", "C", "D"], Chaves(marca), "o trecho da marca em diante, da cópia, e depois o arquivo novo inteiro");
    }

    [Teste]
    public void RotacaoSemACopia_SoOArquivoNovo()
    {
        Acrescentar(Atual, "A", "B", "C");
        long marca = new FileInfo(Atual).Length;
        File.Delete(Atual);
        Acrescentar(Atual, "D");
        Afirmar.Sequencia(["D"], Chaves(marca), "sem a cópia, o arquivo novo inteiro");
        // Uma cópia menor que a marca não é a do trecho dela (outra rotação a sobrescreveu): fica de fora.
        Acrescentar(Rotacionado, "X");
        Afirmar.Sequencia(["D"], Chaves(marca), "a cópia menor que a marca fica de fora");
    }

    [Teste]
    public void SemArquivo_Nada()
        => Afirmar.Igual(0, BuzzyEmTeste.EventosDesde(Atual, Rotacionado, 0).Count, "sem o log, nenhum evento");

    // A marca de LeituraDoLog.Marcar leva a assinatura dos bytes antes dela (revisão de correção, achado 6): a rotação é
    // reconhecida pelo arquivo, não pelo tamanho, e o arquivo novo pode já ter passado do deslocamento da marca.

    [Teste]
    public void MarcaComAssinatura_SemRotacao_SoAsLinhasDepoisDaMarca()
    {
        Acrescentar(Atual, "A");
        long marca = LeituraDoLog.Marcar(Atual);
        Afirmar.Diferente(new FileInfo(Atual).Length, marca, "a marca leva a assinatura além do deslocamento");
        Afirmar.Igual(new FileInfo(Atual).Length, LeituraDoLog.Deslocamento(marca), "o deslocamento é o fim do arquivo");
        Acrescentar(Atual, "B", "C");
        Afirmar.Sequencia(["B", "C"], Chaves(marca));
    }

    [Teste]
    public void MarcaComAssinatura_RotacaoComOArquivoNovoJaMaiorQueAMarca_OTrechoDaCopiaEDepoisOArquivoNovoInteiro()
    {
        Acrescentar(Atual, "A");
        long marca = LeituraDoLog.Marcar(Atual);
        Acrescentar(Atual, "B", "C");
        File.Move(Atual, Rotacionado, overwrite: true);
        Acrescentar(Atual, "D", "E", "F", "G");
        Afirmar.Verdadeiro(new FileInfo(Atual).Length > LeituraDoLog.Deslocamento(marca), "premissa: o arquivo novo já passou do deslocamento da marca");
        Afirmar.Sequencia(["B", "C", "D", "E", "F", "G"], Chaves(marca), "nenhuma linha perdida, nenhuma do arquivo novo pulada");
    }

    [Teste]
    public void MarcaComAssinatura_DuasRotacoes_ACopiaEOAtualInteiros_NadaDeAntesDaMarca()
    {
        Acrescentar(Atual, "A");
        long marca = LeituraDoLog.Marcar(Atual);
        Acrescentar(Atual, "B");
        File.Move(Atual, Rotacionado, overwrite: true); // primeira rotação: a cópia é o arquivo marcado
        Acrescentar(Atual, "C", "D");
        File.Move(Atual, Rotacionado, overwrite: true); // segunda: a cópia do arquivo marcado se perde
        Acrescentar(Atual, "E");
        Afirmar.Sequencia(["C", "D", "E"], Chaves(marca), "o B se perdeu com a segunda rotação; o resto é todo de depois da marca");
    }

    [Teste]
    public void MarcaComALinhaAindaSendoEscrita_SemAssinatura_CriterioAntigo()
    {
        Acrescentar(Atual, "A");
        File.AppendAllText(Atual, "[00:00:01.000] BUZZY|B|n=", new UTF8Encoding(false)); // a linha ainda não terminou
        long marca = LeituraDoLog.Marcar(Atual);
        Afirmar.Igual(new FileInfo(Atual).Length, marca, "sem assinatura: a marca é só o deslocamento");
        File.AppendAllText(Atual, "1" + Environment.NewLine, new UTF8Encoding(false));
        Acrescentar(Atual, "C");
        Afirmar.Sequencia(["C"], Chaves(marca), "o resto da linha B não é evento; depois, o C");
        Afirmar.Igual(0L, LeituraDoLog.Marcar(Path.Combine(_pasta, "nao-existe.log")), "sem o arquivo, a marca é 0");
    }

    private List<string> Chaves(long marca) => [.. BuzzyEmTeste.EventosDesde(Atual, Rotacionado, marca).Select(e => e.Chave)];

    /// <summary>Linhas no formato do Buzzy (<c>[hh:mm:ss.fff] BUZZY|CHAVE|n=1</c>), em UTF-8 sem BOM, como o Diagnostico grava.</summary>
    private static void Acrescentar(string arquivo, params string[] chaves)
    {
        var sb = new StringBuilder();
        foreach (string chave in chaves) sb.Append("[00:00:01.000] BUZZY|").Append(chave).Append("|n=1").Append(Environment.NewLine);
        File.AppendAllText(arquivo, sb.ToString(), new UTF8Encoding(false));
    }
}
