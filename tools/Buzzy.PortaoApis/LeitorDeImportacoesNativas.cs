using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Buzzy.PortaoApis;

// Funcao é "#n" quando a importação é pelo ordinal n.
internal sealed record ImportacaoNativa(string Modulo, string Funcao, bool CargaAtrasada)
{
    public bool PorOrdinal => Funcao.StartsWith('#');

    public override string ToString() => $"{Modulo}!{Funcao}";
}

// Lê as importações normais e as de carga atrasada, em PE32 e PE32+, sem carregar o arquivo.
// PE malformado lança BadImageFormatException.
internal static class LeitorDeImportacoesNativas
{
    private const int MaximoDeEntradas = 1 << 16;
    private const int MaximoDeCaracteres = 1024;

    public static IReadOnlyList<ImportacaoNativa> Ler(string caminho)
    {
        using FileStream arquivo = File.OpenRead(caminho);
        using var pe = new PEReader(arquivo);
        return Ler(pe);
    }

    public static IReadOnlyList<ImportacaoNativa> Ler(PEReader pe)
    {
        ArgumentNullException.ThrowIfNull(pe);
        PEHeader cabecalho = pe.PEHeaders.PEHeader
            ?? throw new BadImageFormatException("O arquivo não tem o cabeçalho opcional do PE.");
        bool pe32Mais = cabecalho.Magic == PEMagic.PE32Plus;

        var importacoes = new List<ImportacaoNativa>();
        LerDiretorio(pe, cabecalho.ImportTableDirectory, pe32Mais, importacoes);
        LerDiretorioAtrasado(pe, cabecalho.DelayImportTableDirectory, pe32Mais, (long)cabecalho.ImageBase, importacoes);
        return importacoes;
    }

    // IMAGE_IMPORT_DESCRIPTOR, 20 bytes: OriginalFirstThunk, TimeDateStamp, ForwarderChain,
    // Name, FirstThunk. A lista termina num descritor zerado.
    private static void LerDiretorio(PEReader pe, DirectoryEntry diretorio, bool pe32Mais, List<ImportacaoNativa> destino)
    {
        if (diretorio.RelativeVirtualAddress == 0) return;
        BlobReader leitor = Bloco(pe, diretorio.RelativeVirtualAddress);
        for (int i = 0; ; i++)
        {
            if (i >= MaximoDeEntradas) throw new BadImageFormatException("Diretório de importação sem fim.");
            int tabelaDeNomes = leitor.ReadInt32();
            leitor.ReadInt32();
            leitor.ReadInt32();
            int nome = leitor.ReadInt32();
            int tabelaDeEnderecos = leitor.ReadInt32();
            if (tabelaDeNomes == 0 && nome == 0 && tabelaDeEnderecos == 0) return;

            string modulo = TextoAscii(pe, nome);
            // Sem a tabela de nomes (vinculadores antigos), a de endereços guarda os nomes no arquivo.
            LerFuncoes(pe, modulo, tabelaDeNomes != 0 ? tabelaDeNomes : tabelaDeEnderecos, pe32Mais, 0, cargaAtrasada: false, destino);
        }
    }

    // IMAGE_DELAYLOAD_DESCRIPTOR, 32 bytes (8 campos de 4). Sem o bit RvaBased em Attributes
    // (formato antigo), os campos são VAs e tem que descontar a base da imagem.
    private static void LerDiretorioAtrasado(PEReader pe, DirectoryEntry diretorio, bool pe32Mais, long baseDaImagem, List<ImportacaoNativa> destino)
    {
        if (diretorio.RelativeVirtualAddress == 0) return;
        BlobReader leitor = Bloco(pe, diretorio.RelativeVirtualAddress);
        for (int i = 0; ; i++)
        {
            if (i >= MaximoDeEntradas) throw new BadImageFormatException("Diretório de importação atrasada sem fim.");
            uint atributos = leitor.ReadUInt32();
            uint nome = leitor.ReadUInt32();
            leitor.ReadUInt32();
            leitor.ReadUInt32();
            uint tabelaDeNomes = leitor.ReadUInt32();
            leitor.ReadUInt32();
            leitor.ReadUInt32();
            leitor.ReadUInt32();
            if (nome == 0) return;

            long desconto = (atributos & 1) != 0 ? 0 : baseDaImagem;
            string modulo = TextoAscii(pe, ParaRva(nome, desconto));
            LerFuncoes(pe, modulo, ParaRva(tabelaDeNomes, desconto), pe32Mais, desconto, cargaAtrasada: true, destino);
        }
    }

    // Entrada de 4 bytes (PE32) ou 8 (PE32+). Bit alto ligado: ordinal nos 16 bits baixos.
    // Desligado: RVA de IMAGE_IMPORT_BY_NAME (2 bytes de dica + nome ASCII). Termina em zero.
    private static void LerFuncoes(PEReader pe, string modulo, int rvaDaTabela, bool pe32Mais, long desconto, bool cargaAtrasada, List<ImportacaoNativa> destino)
    {
        BlobReader leitor = Bloco(pe, rvaDaTabela);
        for (int i = 0; ; i++)
        {
            if (i >= MaximoDeEntradas) throw new BadImageFormatException($"Tabela de importação de {modulo} sem fim.");
            ulong valor = pe32Mais ? leitor.ReadUInt64() : leitor.ReadUInt32();
            if (valor == 0) return;

            ulong bitDeOrdinal = pe32Mais ? 1UL << 63 : 1UL << 31;
            string funcao = (valor & bitDeOrdinal) != 0
                ? $"#{valor & 0xFFFF}"
                : TextoAscii(pe, ParaRva((uint)(valor & 0x7FFFFFFF), desconto) + 2);
            destino.Add(new ImportacaoNativa(modulo, funcao, cargaAtrasada));
        }
    }

    private static int ParaRva(uint valor, long desconto)
    {
        long rva = valor - desconto;
        if (rva <= 0 || rva > int.MaxValue) throw new BadImageFormatException($"Endereço fora da imagem na tabela de importação: 0x{valor:X}.");
        return (int)rva;
    }

    private static BlobReader Bloco(PEReader pe, int rva)
    {
        if (rva <= 0) throw new BadImageFormatException($"RVA inválido na tabela de importação: 0x{rva:X}.");
        PEMemoryBlock bloco = pe.GetSectionData(rva);
        if (bloco.Length == 0) throw new BadImageFormatException($"RVA 0x{rva:X} fora das seções do PE.");
        return bloco.GetReader();
    }

    private static string TextoAscii(PEReader pe, int rva)
    {
        BlobReader leitor = Bloco(pe, rva);
        var texto = new StringBuilder();
        while (leitor.RemainingBytes > 0 && texto.Length < MaximoDeCaracteres)
        {
            byte b = leitor.ReadByte();
            if (b == 0) return texto.ToString();
            texto.Append((char)b);
        }
        throw new BadImageFormatException($"Nome sem terminador na tabela de importação (RVA 0x{rva:X}).");
    }
}
