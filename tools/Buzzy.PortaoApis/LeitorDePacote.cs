using System.IO.Compression;
using System.Text;

namespace Buzzy.PortaoApis;

/// <summary>Tipo de um arquivo dentro do pacote de arquivo único, como o empacotador do .NET o grava.</summary>
internal enum TipoNoPacote : byte
{
    Desconhecido = 0,
    Assembly = 1,
    BinarioNativo = 2,
    DepsJson = 3,
    RuntimeConfigJson = 4,
    Simbolos = 5,
}

/// <summary>Um arquivo dentro do pacote.</summary>
/// <param name="Caminho">Caminho relativo gravado no pacote, com '/' ou '\' como o empacotador gravou.</param>
/// <param name="Deslocamento">Posição do conteúdo no arquivo do pacote.</param>
/// <param name="Tamanho">Tamanho do conteúdo descomprimido.</param>
/// <param name="TamanhoComprimido">Tamanho gravado, comprimido; 0 quando o conteúdo não foi comprimido.</param>
internal sealed record EntradaDoPacote(string Caminho, TipoNoPacote Tipo, long Deslocamento, long Tamanho, long TamanhoComprimido);

/// <summary>O cabeçalho e o índice de um pacote de arquivo único.</summary>
/// <param name="PosicaoDaAssinatura">Onde a assinatura do pacote está no host (dentro da seção .data).</param>
/// <param name="Cabecalho">Posição do cabeçalho do pacote, gravada pelo empacotador nos 8 bytes antes da assinatura.</param>
internal sealed record PacoteDeArquivoUnico(
    long PosicaoDaAssinatura,
    long Cabecalho,
    uint VersaoMaior,
    uint VersaoMenor,
    string Id,
    IReadOnlyList<EntradaDoPacote> Entradas);

/// <summary>
/// Lê o pacote de um executável de arquivo único do .NET (PublishSingleFile; F9-P10, DEC-042) sem carregá-lo nem
/// executá-lo. O formato é o do empacotador do SDK (Microsoft.NET.HostModel.Bundle): o host nativo traz, na seção .data,
/// uma assinatura de 32 bytes (o SHA-256 de ".net core bundle") precedida da posição do cabeçalho; o cabeçalho tem a
/// versão, o número de arquivos, o id do pacote, as posições do deps.json e do runtimeconfig.json e as flags; cada
/// entrada, a posição, o tamanho, o tamanho comprimido (versão 6 em diante), o tipo e o caminho. Conteúdo comprimido
/// é Deflate. Só a versão maior 6 foi revisada: outra é erro de leitura, para ninguém aceitar um formato não lido.
/// </summary>
internal static class LeitorDePacote
{
    /// <summary>A assinatura do pacote no host: SHA-256 de ".net core bundle".</summary>
    public static readonly byte[] Assinatura =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    ];

    public const uint VersaoMaiorRevisada = 6;

    /// <summary>Limites do que se lê: o índice e cada arquivo descomprimido, para um pacote adulterado não esgotar a memória.</summary>
    public const int MaximoDeEntradas = 4096;
    public const long MaximoPorArquivo = 256L * 1024 * 1024;

    /// <summary>
    /// O pacote do executável, ou nulo se ele não é um pacote (sem assinatura no host, ou com a posição do cabeçalho 0,
    /// como no host sem nada empacotado). Índice incoerente lança <see cref="InvalidDataException"/>: arquivo fora do
    /// pacote, sobreposto ou repetido; caminho absoluto ou com ".."; versão não revisada; bytes que não são zero entre as
    /// entradas; qualquer byte depois do índice (o pacote termina nele); e as posições do deps.json e do runtimeconfig.json
    /// no cabeçalho, que são as que o host usa, diferentes das entradas desses tipos no índice, que são as que o portão lê
    /// (revisão adversarial do F9-P10, achado de alta).
    /// </summary>
    public static PacoteDeArquivoUnico? Ler(string arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        try
        {
            return LerSemTraduzir(arquivo);
        }
        catch (Exception e) when (e is FormatException or EndOfStreamException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"índice do pacote ilegível: {e.Message}", e);
        }
    }

    private static PacoteDeArquivoUnico? LerSemTraduzir(string arquivo)
    {
        using FileStream fluxo = File.OpenRead(arquivo);
        long fimDaImagem = FimDaImagemPe(fluxo);
        long assinatura = Procurar(fluxo, Assinatura, fimDaImagem);
        if (assinatura < 8) return null;

        using var leitor = new BinaryReader(fluxo, Encoding.UTF8, leaveOpen: true);
        fluxo.Position = assinatura - 8;
        long cabecalho = leitor.ReadInt64();
        if (cabecalho == 0) return null;
        if (cabecalho < fimDaImagem || cabecalho >= fluxo.Length) throw new InvalidDataException($"cabeçalho do pacote fora do arquivo: {cabecalho}");

        fluxo.Position = cabecalho;
        uint maior = leitor.ReadUInt32();
        uint menor = leitor.ReadUInt32();
        if (maior != VersaoMaiorRevisada) throw new InvalidDataException($"versão do pacote {maior}.{menor} não revisada (o portão lê a {VersaoMaiorRevisada}.x)");
        int quantidade = leitor.ReadInt32();
        if (quantidade < 1 || quantidade > MaximoDeEntradas) throw new InvalidDataException($"número de arquivos do pacote fora do limite: {quantidade}");
        string id = leitor.ReadString();
        // Versão 2 em diante: as posições do deps.json e do runtimeconfig.json (o host as usa) e as flags.
        (long Deslocamento, long Tamanho) deps = (leitor.ReadInt64(), leitor.ReadInt64());
        (long Deslocamento, long Tamanho) configuracao = (leitor.ReadInt64(), leitor.ReadInt64());
        leitor.ReadUInt64();

        var entradas = new List<EntradaDoPacote>(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            long deslocamento = leitor.ReadInt64();
            long tamanho = leitor.ReadInt64();
            long comprimido = leitor.ReadInt64();
            byte tipo = leitor.ReadByte();
            string caminho = leitor.ReadString();
            if (!Enum.IsDefined((TipoNoPacote)tipo)) throw new InvalidDataException($"{caminho}: tipo {tipo} desconhecido no pacote");
            if (caminho.Length == 0 || Path.IsPathRooted(caminho) || caminho.Split('/', '\\').Any(p => p is ".." or "." or ""))
                throw new InvalidDataException($"caminho inválido no pacote: \"{caminho}\"");
            long gravado = comprimido == 0 ? tamanho : comprimido;
            if (deslocamento < fimDaImagem || tamanho < 0 || comprimido < 0 || tamanho > MaximoPorArquivo || gravado > fluxo.Length - deslocamento)
                throw new InvalidDataException($"{caminho}: posição ou tamanho fora do arquivo ({deslocamento}, {tamanho}, {comprimido})");
            entradas.Add(new EntradaDoPacote(caminho, (TipoNoPacote)tipo, deslocamento, tamanho, comprimido));
        }

        var porPosicao = entradas.OrderBy(e => e.Deslocamento).ToList();
        for (int i = 1; i < porPosicao.Count; i++)
        {
            EntradaDoPacote anterior = porPosicao[i - 1];
            if (anterior.Deslocamento + (anterior.TamanhoComprimido == 0 ? anterior.Tamanho : anterior.TamanhoComprimido) > porPosicao[i].Deslocamento)
                throw new InvalidDataException($"arquivos sobrepostos no pacote: {anterior.Caminho} e {porPosicao[i].Caminho}");
        }
        if (entradas.Select(e => e.Caminho.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entradas.Count)
            throw new InvalidDataException("caminho repetido no pacote");

        // O índice é o fim do arquivo: nada anexado depois dele.
        if (fluxo.Position != fluxo.Length) throw new InvalidDataException($"{fluxo.Length - fluxo.Position} byte(s) depois do índice do pacote");

        ConferirPosicao("deps.json", deps, TipoNoPacote.DepsJson, entradas);
        ConferirPosicao("runtimeconfig.json", configuracao, TipoNoPacote.RuntimeConfigJson, entradas);

        // Do fim da imagem até o cabeçalho, só as entradas e, entre elas, zeros (o alinhamento do empacotador).
        long atual = fimDaImagem;
        foreach (EntradaDoPacote e in porPosicao.Append(new EntradaDoPacote("(cabeçalho do pacote)", TipoNoPacote.Desconhecido, cabecalho, 0, 0)))
        {
            if (e.Deslocamento < atual) throw new InvalidDataException($"{e.Caminho} começa antes do fim do arquivo anterior");
            if (e.Deslocamento > atual && !SoZeros(fluxo, atual, e.Deslocamento)) throw new InvalidDataException($"bytes que não são zero antes de {e.Caminho}");
            atual = e.Deslocamento + (e.TamanhoComprimido == 0 ? e.Tamanho : e.TamanhoComprimido);
        }
        return new PacoteDeArquivoUnico(assinatura, cabecalho, maior, menor, id, entradas);
    }

    /// <summary>A posição do cabeçalho é exatamente a da única entrada daquele tipo, sem compressão; zero só sem entrada.</summary>
    private static void ConferirPosicao(string nome, (long Deslocamento, long Tamanho) posicao, TipoNoPacote tipo, List<EntradaDoPacote> entradas)
    {
        List<EntradaDoPacote> doTipo = [.. entradas.Where(e => e.Tipo == tipo)];
        if (posicao.Deslocamento == 0 && posicao.Tamanho == 0 && doTipo.Count == 0) return;
        if (doTipo.Count != 1 || doTipo[0].TamanhoComprimido != 0 || doTipo[0].Deslocamento != posicao.Deslocamento || doTipo[0].Tamanho != posicao.Tamanho)
            throw new InvalidDataException($"a posição do {nome} no cabeçalho ({posicao.Deslocamento}, {posicao.Tamanho}) não é a da entrada dele no índice");
    }

    private static bool SoZeros(FileStream fluxo, long inicio, long fim)
    {
        if (fim - inicio > 1 << 20) return false;
        var bytes = new byte[fim - inicio];
        fluxo.Position = inicio;
        fluxo.ReadExactly(bytes);
        return !bytes.AsSpan().ContainsAnyExcept((byte)0);
    }

    /// <summary>O conteúdo descomprimido de uma entrada, conferido contra o tamanho do índice.</summary>
    public static byte[] Conteudo(string arquivo, EntradaDoPacote entrada)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(entrada);
        using FileStream fluxo = File.OpenRead(arquivo);
        fluxo.Position = entrada.Deslocamento;
        var conteudo = new byte[entrada.Tamanho];
        if (entrada.TamanhoComprimido == 0)
        {
            fluxo.ReadExactly(conteudo);
            return conteudo;
        }

        var comprimido = new byte[entrada.TamanhoComprimido];
        fluxo.ReadExactly(comprimido);
        try
        {
            using var deflate = new DeflateStream(new MemoryStream(comprimido), CompressionMode.Decompress);
            deflate.ReadExactly(conteudo);
            if (deflate.Read(new byte[1]) != 0) throw new InvalidDataException($"{entrada.Caminho}: conteúdo maior que o tamanho do índice");
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException($"{entrada.Caminho}: conteúdo menor que o tamanho do índice", e);
        }
        return conteudo;
    }

    /// <summary>Onde termina a imagem PE do host (o fim da última seção no arquivo); a assinatura só é procurada antes dele.</summary>
    private static long FimDaImagemPe(FileStream fluxo)
    {
        using var leitor = new BinaryReader(fluxo, Encoding.ASCII, leaveOpen: true);
        if (fluxo.Length < 0x40) throw new InvalidDataException("arquivo curto demais para um PE");
        fluxo.Position = 0x3c;
        int pe = leitor.ReadInt32();
        if (pe <= 0 || pe > fluxo.Length - 24) throw new InvalidDataException("cabeçalho PE fora do arquivo");
        fluxo.Position = pe;
        if (leitor.ReadUInt32() != 0x00004550) throw new InvalidDataException("sem a assinatura PE");
        fluxo.Position = pe + 6;
        int secoes = leitor.ReadUInt16();
        fluxo.Position = pe + 20;
        int opcional = leitor.ReadUInt16();
        long fim = 0;
        for (int i = 0; i < secoes; i++)
        {
            fluxo.Position = pe + 24 + opcional + i * 40 + 16;
            long tamanho = leitor.ReadUInt32();
            long inicio = leitor.ReadUInt32();
            fim = Math.Max(fim, inicio + tamanho);
        }
        return Math.Min(fim, fluxo.Length);
    }

    /// <summary>A primeira posição da sequência antes de <paramref name="limite"/>, ou -1.</summary>
    private static long Procurar(FileStream fluxo, byte[] sequencia, long limite)
    {
        const int Bloco = 1 << 20;
        var buffer = new byte[Bloco + sequencia.Length];
        long inicio = 0;
        while (inicio < limite)
        {
            fluxo.Position = inicio;
            int lidos = fluxo.Read(buffer, 0, (int)Math.Min(buffer.Length, limite - inicio));
            if (lidos < sequencia.Length) return -1;
            int achado = buffer.AsSpan(0, lidos).IndexOf(sequencia);
            if (achado >= 0) return inicio + achado;
            inicio += lidos - sequencia.Length + 1;
        }
        return -1;
    }
}
