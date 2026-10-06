using System.Text;

namespace Buzzy.PortaoApis;

/// <summary>
/// A procedência do host de um executável de arquivo único (F9-P10, DEC-042): o Buzzy.exe empacotado é o
/// singlefilehost.exe do pacote Microsoft.NETCore.App.Host.win-x64 que o SDK usou, com só o que o SDK grava nele.
/// Levantamento de 2026-10-05 (SDK 10.0.401, host 10.0.12), comparando byte a byte:
/// - nos cabeçalhos, mudam só SizeOfInitializedData, Subsystem (de console para GUI: o Buzzy é WinExe), o tamanho do
///   diretório de recursos e, na tabela de seções, o VirtualSize e o SizeOfRawData da .rsrc e o PointerToRawData das
///   seções depois dela (a .rsrc cresce). Se o crescimento passar do alinhamento, mudam também o VirtualAddress dessas
///   seções, os diretórios que apontam para elas e o SizeOfImage, sempre pelo mesmo deslocamento; o CheckSum pode mudar.
///   O resto (ponto de entrada, DllCharacteristics, características e nomes das seções, os outros diretórios, o
///   cabeçalho DOS) é idêntico (revisão adversarial do F9-P10, achado de alta: o ponto de entrada desviado para a .rsrc);
/// - nas seções, todas idênticas, menos a .data (só os 8 bytes da posição do cabeçalho do pacote e o espaço reservado
///   do nome do aplicativo) e a .rsrc, que tem de continuar não executável, só com os tipos de recurso que o SDK grava
///   (o RCDATA do próprio host, igual, a versão, o ícone e um manifesto só), e cujo manifesto o portão confere à parte.
/// </summary>
internal static class ProcedenciaDoHost
{
    /// <summary>O espaço reservado do nome do aplicativo no host: o SHA-256 de "foobar" em hexadecimal, em 1024 bytes.</summary>
    public const string EspacoDoNome = "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2";
    private const int TamanhoDoEspacoDoNome = 1024;
    private const ushort SubsistemaGui = 2;
    private const int RtIcone = 3, RtDados = 10, RtGrupoDeIcones = 14, RtVersao = 16, RtManifesto = 24;
    private const uint SecaoExecutavel = 0x20000000, SecaoDeCodigo = 0x00000020;

    private sealed record Secao(string Nome, long Inicio, long Tamanho, uint EnderecoVirtual, uint TamanhoVirtual, uint Caracteristicas, int Cabecalho);

    private sealed record Pe(byte[] Bytes, int Opcional, int Diretorios, int QuantosDiretorios, List<Secao> Secoes);

    /// <summary>
    /// As diferenças entre o host do pacote e o do SDK que não são as permitidas; lista vazia quando o host é o do pacote
    /// da Microsoft. <paramref name="nomeDoPrincipal"/> é o que o espaço reservado do nome deve conter (Buzzy.dll).
    /// PE malformado lança <see cref="InvalidDataException"/>.
    /// </summary>
    public static IReadOnlyList<string> Comparar(string empacotado, string hostDoSdk, PacoteDeArquivoUnico pacote, string nomeDoPrincipal)
    {
        ArgumentNullException.ThrowIfNull(empacotado);
        ArgumentNullException.ThrowIfNull(hostDoSdk);
        ArgumentNullException.ThrowIfNull(pacote);
        try
        {
            Pe a = Ler(File.ReadAllBytes(empacotado)), b = Ler(File.ReadAllBytes(hostDoSdk));
            var diferencas = new List<string>();
            if (!a.Secoes.Select(s => s.Nome).SequenceEqual(b.Secoes.Select(s => s.Nome)))
                return [$"seções diferentes: {string.Join(" ", a.Secoes.Select(s => s.Nome))} contra {string.Join(" ", b.Secoes.Select(s => s.Nome))}"];
            int recursos = a.Secoes.FindIndex(s => s.Nome == ".rsrc");
            if (recursos < 0) return ["o host não tem a seção .rsrc"];

            CompararCabecalhos(a, b, recursos, diferencas);
            CompararSecoes(a, b, pacote, nomeDoPrincipal, diferencas);
            CompararRecursos(a, b, diferencas);
            return diferencas;
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            throw new InvalidDataException($"PE malformado: {e.Message}", e);
        }
    }

    /// <summary>
    /// O manifesto do aplicativo dos recursos do PE: exige um tipo RT_MANIFEST só, com um id só (1) e um idioma só, como
    /// o SDK grava; nulo se não houver. Outra forma lança <see cref="InvalidDataException"/> (o Windows poderia escolher
    /// um manifesto que o portão não leu).
    /// </summary>
    public static string? Manifesto(string arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        try
        {
            Pe pe = Ler(File.ReadAllBytes(arquivo));
            int? raiz = RaizDosRecursos(pe);
            if (raiz is not { } r) return null;
            List<(int Id, int Alvo)> tipos = EntradasPorId(pe.Bytes, r);
            List<(int Id, int Alvo)> manifestos = [.. tipos.Where(t => t.Id == RtManifesto)];
            if (manifestos.Count == 0) return null;
            int diretorioDeIds = Subdiretorio(r, manifestos.Single().Alvo);
            List<(int Id, int Alvo)> ids = EntradasPorId(pe.Bytes, diretorioDeIds);
            if (ids.Count != 1 || ids[0].Id != 1) throw new InvalidDataException($"RT_MANIFEST com {ids.Count} id(s) ({string.Join(",", ids.Select(i => i.Id))}); o SDK grava só o id 1");
            List<(int Id, int Alvo)> idiomas = EntradasPorId(pe.Bytes, Subdiretorio(r, ids[0].Alvo));
            if (idiomas.Count != 1 || (idiomas[0].Alvo & unchecked((int)0x80000000)) != 0)
                throw new InvalidDataException($"RT_MANIFEST id 1 com {idiomas.Count} idioma(s); o SDK grava um só");
            (byte[] dados, _) = Dados(pe, r + idiomas[0].Alvo);
            return Encoding.UTF8.GetString(dados).TrimStart('﻿');
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or ArgumentException or IndexOutOfRangeException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException($"recursos malformados: {e.Message}", e);
        }
    }

    private static void CompararCabecalhos(Pe a, Pe b, int recursos, List<string> diferencas)
    {
        if (a.Opcional != b.Opcional || a.QuantosDiretorios != b.QuantosDiretorios || a.Bytes.Length < a.Opcional || b.Bytes.Length < b.Opcional)
        {
            diferencas.Add("cabeçalho PE em outra posição ou com outro número de diretórios");
            return;
        }
        int fimDosCabecalhos = Math.Min(BitConverter.ToInt32(a.Bytes, a.Opcional + 60), BitConverter.ToInt32(b.Bytes, b.Opcional + 60));
        if (BitConverter.ToInt32(a.Bytes, a.Opcional + 60) != BitConverter.ToInt32(b.Bytes, b.Opcional + 60))
            diferencas.Add("SizeOfHeaders diferente");

        // O deslocamento virtual das seções depois da .rsrc (0 quando a .rsrc cresceu dentro do alinhamento).
        long deslocamento = a.Secoes.Count > recursos + 1 ? (long)a.Secoes[recursos + 1].EnderecoVirtual - b.Secoes[recursos + 1].EnderecoVirtual : 0;
        uint inicioDepois = recursos + 1 < b.Secoes.Count ? b.Secoes[recursos + 1].EnderecoVirtual : uint.MaxValue;

        var permitidos = new HashSet<int>();
        void Permitir(int inicio, int tamanho) { for (int i = 0; i < tamanho; i++) permitidos.Add(inicio + i); }
        Permitir(a.Opcional + 8, 4);   // SizeOfInitializedData
        Permitir(a.Opcional + 64, 4);  // CheckSum
        Permitir(a.Opcional + 68, 2);  // Subsystem, conferido abaixo
        Permitir(a.Diretorios + 2 * 8 + 4, 4); // tamanho do diretório de recursos
        if (deslocamento != 0)
        {
            Permitir(a.Opcional + 56, 4); // SizeOfImage
            for (int d = 0; d < a.QuantosDiretorios; d++)
            {
                uint rvaB = BitConverter.ToUInt32(b.Bytes, b.Diretorios + d * 8);
                uint rvaA = BitConverter.ToUInt32(a.Bytes, a.Diretorios + d * 8);
                if (d != 2 && rvaB >= inicioDepois && rvaA - (long)rvaB == deslocamento) Permitir(a.Diretorios + d * 8, 4);
            }
        }
        Secao rsrc = a.Secoes[recursos];
        Permitir(rsrc.Cabecalho + 8, 4);  // VirtualSize da .rsrc
        Permitir(rsrc.Cabecalho + 16, 4); // SizeOfRawData da .rsrc
        for (int i = recursos + 1; i < a.Secoes.Count; i++)
        {
            Permitir(a.Secoes[i].Cabecalho + 20, 4); // PointerToRawData
            if (a.Secoes[i].EnderecoVirtual - (long)b.Secoes[i].EnderecoVirtual == deslocamento) Permitir(a.Secoes[i].Cabecalho + 12, 4);
        }

        for (int i = 0; i < fimDosCabecalhos; i++)
        {
            if (a.Bytes[i] != b.Bytes[i] && !permitidos.Contains(i))
            {
                diferencas.Add($"cabeçalho: byte {i} diferente do host do SDK (fora dos campos que o SDK grava)");
                break;
            }
        }
        if (BitConverter.ToUInt16(a.Bytes, a.Opcional + 68) != SubsistemaGui) diferencas.Add("Subsystem não é o de janelas (GUI)");
        if (rsrc.Caracteristicas != b.Secoes[recursos].Caracteristicas || (rsrc.Caracteristicas & (SecaoExecutavel | SecaoDeCodigo)) != 0)
            diferencas.Add(".rsrc com características diferentes do host do SDK, ou executável");
        // Os tamanhos e posições que podiam mudar têm de ser coerentes com o arquivo.
        long ponteiro = a.Secoes[recursos].Inicio + a.Secoes[recursos].Tamanho;
        for (int i = recursos + 1; i < a.Secoes.Count; i++)
        {
            if (a.Secoes[i].Inicio != ponteiro) diferencas.Add($"{a.Secoes[i].Nome}: posição no arquivo incoerente com a .rsrc");
            ponteiro = a.Secoes[i].Inicio + a.Secoes[i].Tamanho;
        }
    }

    private static void CompararSecoes(Pe a, Pe b, PacoteDeArquivoUnico pacote, string nomeDoPrincipal, List<string> diferencas)
    {
        for (int i = 0; i < a.Secoes.Count; i++)
        {
            Secao x = a.Secoes[i], y = b.Secoes[i];
            if (x.Nome == ".rsrc") continue;
            if (x.Tamanho != y.Tamanho)
            {
                diferencas.Add($"{x.Nome}: tamanho {x.Tamanho} contra {y.Tamanho}");
                continue;
            }
            ReadOnlySpan<byte> da = a.Bytes.AsSpan((int)x.Inicio, (int)x.Tamanho), db = b.Bytes.AsSpan((int)y.Inicio, (int)y.Tamanho);
            if (x.Nome != ".data")
            {
                if (!da.SequenceEqual(db)) diferencas.Add($"{x.Nome}: conteúdo diferente do host do SDK");
                continue;
            }

            // Na .data, só a posição do cabeçalho e o espaço reservado do nome podem mudar.
            long posicao = pacote.PosicaoDaAssinatura - 8 - x.Inicio;
            int espaco = db.IndexOf(Encoding.ASCII.GetBytes(EspacoDoNome));
            if (espaco < 0) { diferencas.Add(".data: o host do SDK não tem o espaço reservado do nome"); continue; }
            if (posicao < 0 || posicao + 8 + LeitorDePacote.Assinatura.Length > x.Tamanho) { diferencas.Add(".data: a assinatura do pacote não está na .data"); continue; }
            for (int j = 0; j < da.Length; j++)
            {
                bool permitido = (j >= posicao && j < posicao + 8) || (j >= espaco && j < espaco + TamanhoDoEspacoDoNome);
                if (!permitido && da[j] != db[j])
                {
                    diferencas.Add($".data: byte {j} diferente fora da posição do pacote e do nome do aplicativo");
                    break;
                }
            }
            byte[] nome = Encoding.UTF8.GetBytes(nomeDoPrincipal);
            ReadOnlySpan<byte> gravado = da.Slice(espaco, Math.Min(TamanhoDoEspacoDoNome, da.Length - espaco));
            if (!gravado.StartsWith(nome) || gravado[nome.Length..].ContainsAnyExcept((byte)0))
                diferencas.Add($".data: o nome gravado no host não é {nomeDoPrincipal}");
        }
    }

    /// <summary>Na .rsrc: só os tipos que o SDK grava, e o RCDATA do host igual ao do host do SDK.</summary>
    private static void CompararRecursos(Pe a, Pe b, List<string> diferencas)
    {
        int? raizA = RaizDosRecursos(a), raizB = RaizDosRecursos(b);
        if (raizA is not { } ra) { diferencas.Add(".rsrc sem o diretório de recursos"); return; }
        if (BitConverter.ToUInt16(a.Bytes, ra + 12) != 0) diferencas.Add(".rsrc: tipo de recurso com nome (o SDK só grava tipos por número)");
        int[] permitidos = [RtIcone, RtDados, RtGrupoDeIcones, RtVersao, RtManifesto];
        foreach ((int id, _) in EntradasPorId(a.Bytes, ra))
            if (!permitidos.Contains(id)) diferencas.Add($".rsrc: tipo de recurso {id} que o SDK não grava");
        if (raizB is { } rb && Folhas(b, rb, RtDados) is var doSdk && Folhas(a, ra, RtDados) is var empacotado)
        {
            if (doSdk.Count != empacotado.Count || !doSdk.Zip(empacotado).All(p => p.First.AsSpan().SequenceEqual(p.Second)))
                diferencas.Add(".rsrc: o RCDATA difere do host do SDK");
        }
    }

    /// <summary>Os dados de todas as folhas de um tipo, na ordem do diretório.</summary>
    private static List<byte[]> Folhas(Pe pe, int raiz, int tipo)
    {
        var folhas = new List<byte[]>();
        foreach ((_, int alvo) in EntradasPorId(pe.Bytes, raiz).Where(t => t.Id == tipo))
        {
            int nomes = Subdiretorio(raiz, alvo);
            foreach ((_, int alvoDoNome) in Todas(pe.Bytes, nomes))
            {
                int idiomas = Subdiretorio(raiz, alvoDoNome);
                foreach ((_, int folha) in Todas(pe.Bytes, idiomas))
                {
                    if ((folha & unchecked((int)0x80000000)) != 0) throw new InvalidDataException("recurso com diretório no nível da folha");
                    folhas.Add(Dados(pe, raiz + folha).Dados);
                }
            }
        }
        return folhas;
    }

    private static int Subdiretorio(int raiz, int alvo)
    {
        if ((alvo & unchecked((int)0x80000000)) == 0) throw new InvalidDataException("entrada de recurso sem subdiretório onde o SDK grava um");
        return raiz + (alvo & 0x7fffffff);
    }

    private static List<(int Id, int Alvo)> EntradasPorId(byte[] pe, int diretorio)
        => [.. Todas(pe, diretorio).Where(e => (e.Nome & unchecked((int)0x80000000)) == 0)];

    private static List<(int Nome, int Alvo)> Todas(byte[] pe, int diretorio)
    {
        int nomeados = BitConverter.ToUInt16(pe, diretorio + 12), porId = BitConverter.ToUInt16(pe, diretorio + 14);
        var entradas = new List<(int, int)>(nomeados + porId);
        for (int k = 0; k < nomeados + porId; k++)
        {
            int e = diretorio + 16 + k * 8;
            entradas.Add((BitConverter.ToInt32(pe, e), BitConverter.ToInt32(pe, e + 4)));
        }
        return entradas;
    }

    private static (byte[] Dados, int Rva) Dados(Pe pe, int entrada)
    {
        int rva = BitConverter.ToInt32(pe.Bytes, entrada), tamanho = BitConverter.ToInt32(pe.Bytes, entrada + 4);
        long inicio = Deslocamento(pe, rva);
        if (tamanho < 0 || inicio < 0 || inicio + tamanho > pe.Bytes.Length) throw new InvalidDataException("dados de recurso fora do arquivo");
        return (pe.Bytes.AsSpan((int)inicio, tamanho).ToArray(), rva);
    }

    private static int? RaizDosRecursos(Pe pe)
    {
        if (pe.QuantosDiretorios <= 2) return null;
        int rva = BitConverter.ToInt32(pe.Bytes, pe.Diretorios + 2 * 8);
        if (rva == 0) return null;
        long raiz = Deslocamento(pe, rva);
        if (raiz < 0 || raiz + 16 > pe.Bytes.Length) throw new InvalidDataException("diretório de recursos fora do arquivo");
        return (int)raiz;
    }

    private static long Deslocamento(Pe pe, long rva)
    {
        foreach (Secao s in pe.Secoes)
            if (rva >= s.EnderecoVirtual && rva < s.EnderecoVirtual + Math.Max(s.TamanhoVirtual, (uint)s.Tamanho)) return rva - s.EnderecoVirtual + s.Inicio;
        return -1;
    }

    private static Pe Ler(byte[] pe)
    {
        if (pe.Length < 0x40) throw new InvalidDataException("não é um PE");
        int cabecalho = BitConverter.ToInt32(pe, 0x3c);
        if (cabecalho <= 0 || cabecalho > pe.Length - 24 || BitConverter.ToUInt32(pe, cabecalho) != 0x00004550)
            throw new InvalidDataException("não é um PE");
        int quantas = BitConverter.ToUInt16(pe, cabecalho + 6), tamanhoOpcional = BitConverter.ToUInt16(pe, cabecalho + 20);
        int opcional = cabecalho + 24;
        if (opcional + tamanhoOpcional + quantas * 40 > pe.Length || tamanhoOpcional < 112) throw new InvalidDataException("cabeçalho PE fora do arquivo");
        bool pe32Mais = BitConverter.ToUInt16(pe, opcional) == 0x20b;
        int diretorios = opcional + (pe32Mais ? 112 : 96);
        int quantosDiretorios = (int)Math.Min(BitConverter.ToUInt32(pe, diretorios - 4), (uint)((opcional + tamanhoOpcional - diretorios) / 8));
        var secoes = new List<Secao>(quantas);
        for (int i = 0; i < quantas; i++)
        {
            int s = opcional + tamanhoOpcional + i * 40;
            string nome = Encoding.ASCII.GetString(pe, s, 8).TrimEnd('\0');
            long tamanho = BitConverter.ToUInt32(pe, s + 16), inicio = BitConverter.ToUInt32(pe, s + 20);
            if (inicio + tamanho > pe.Length) throw new InvalidDataException($"{nome}: seção fora do arquivo");
            secoes.Add(new Secao(nome, inicio, tamanho, BitConverter.ToUInt32(pe, s + 12), BitConverter.ToUInt32(pe, s + 8), BitConverter.ToUInt32(pe, s + 36), s));
        }
        return new Pe(pe, opcional, diretorios, quantosDiretorios, secoes);
    }
}
