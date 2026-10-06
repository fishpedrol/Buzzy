using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Buzzy.PortaoApis.Testes.Apoio;
using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>
/// O portão no executável de arquivo único (F9-P10, DEC-042): pacotes montados como o SDK os monta, a partir do
/// singlefilehost.exe real do SDK instalado (o nome do aplicativo gravado no espaço reservado, o manifesto nos recursos
/// pela mesma API do Windows que o SDK usa, e os arquivos e o índice anexados no formato 6), e uma pasta falsa de pacote
/// de runtime para a procedência. Cada caso adultera uma coisa só e espera a reprovação ou o erro dela.
/// </summary>
public sealed class TestesDoPacote : IDisposable
{
    private readonly PastaTemporaria _pasta = new();

    public void Dispose() => _pasta.Dispose();

    private sealed record Arquivo(string Caminho, byte Tipo, byte[] Conteudo, bool Comprimir = false);

    private sealed record Montagem(string Pacote, string Host, string Runtime, string Fonte, string Manifesto);

    private static readonly byte[] DoRuntimeGerenciado = Encoding.ASCII.GetBytes("MZ falso assembly do runtime");
    private static readonly byte[] DoRuntimeNativo = Encoding.ASCII.GetBytes("MZ falso nativo do runtime, comprimido no pacote " + new string('x', 400));

    private static (int Codigo, string Saida, string Erros) Rodar(params string[] argumentos)
    {
        using var saida = new StringWriter();
        using var erros = new StringWriter();
        int codigo = Portao.Executar(argumentos, saida, erros);
        return (codigo, saida.ToString(), erros.ToString());
    }

    private static (int Codigo, string Saida, string Erros) Rodar(Montagem m)
        => Rodar("--pacote", m.Pacote, "--host-de-arquivo-unico", m.Host, "--runtime", m.Runtime, "--fonte", m.Fonte, "--manifesto", m.Manifesto);

    private static byte[] Produto(string nome, Action<AssemblySintetico>? sujar = null)
    {
        var a = new AssemblySintetico(nome);
        if (nome == "Buzzy") a.ReferenciarAssembly("Buzzy.Core");
        a.ReferenciarMembro("System", "Math", "Round");
        sujar?.Invoke(a);
        return a.Gerar();
    }

    private static List<Arquivo> ArquivosLimpos() =>
    [
        new("Buzzy.dll", 1, Produto("Buzzy")),
        new("Buzzy.Core.dll", 1, Produto("Buzzy.Core")),
        new("System.Falso.dll", 1, DoRuntimeGerenciado),
        new("falso_nativo.dll", 2, DoRuntimeNativo, Comprimir: true),
        new("Buzzy.deps.json", 3, Encoding.UTF8.GetBytes(Deps)),
        new("Buzzy.runtimeconfig.json", 4, Encoding.UTF8.GetBytes(RuntimeConfig())),
    ];

    /// <summary>O runtimeconfig.json que o build do .exe único grava (levantamento de 2026-10-05), com uma propriedade a mais opcional.</summary>
    private static string RuntimeConfig(string extra = "") => $$"""
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "includedFrameworks": [ { "name": "Microsoft.NETCore.App", "version": "10.0.12" }, { "name": "Microsoft.WindowsDesktop.App", "version": "10.0.12" } ],
            "configProperties": {
              {{extra}}"System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
              "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
              "CSWINRT_USE_WINDOWS_UI_XAML_PROJECTIONS": false
            }
          }
        }
        """;

    /// <summary>O deps.json do .exe único, reduzido aos arquivos do pacote de teste.</summary>
    private const string Deps = """
        {
          "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0/win-x64", "signature": "" },
          "compilationOptions": {},
          "targets": { ".NETCoreApp,Version=v10.0/win-x64": {
            "Buzzy/0.1.0": { "dependencies": { "Buzzy.Core": "1.0.0" }, "runtime": { "Buzzy.dll": {} } },
            "Buzzy.Core/1.0.0": { "runtime": { "Buzzy.Core.dll": {} } },
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.12": { "runtime": { "System.Falso.dll": {} }, "native": { "falso_nativo.dll": {} } }
          } },
          "libraries": {
            "Buzzy/0.1.0": { "type": "project", "serviceable": false, "sha512": "" },
            "Buzzy.Core/1.0.0": { "type": "project", "serviceable": false, "sha512": "" },
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.12": { "type": "runtimepack", "serviceable": false, "sha512": "" }
          },
          "runtimes": {}
        }
        """;

    /// <summary>
    /// Monta o pacote. <paramref name="adulterarHost"/> mexe no host antes de anexar (depois do nome e do manifesto);
    /// <paramref name="versao"/> e <paramref name="caminhoExtra"/> estragam o índice.
    /// </summary>
    private Montagem Montar(
        List<Arquivo>? arquivos = null,
        string nomeNoHost = "Buzzy.dll",
        string? manifesto = null,
        Action<byte[]>? adulterarHost = null,
        uint versao = 6,
        bool sobrepor = false,
        bool posicoesDoCabecalho = true,
        byte[]? depoisDoIndice = null,
        byte[]? lacuna = null,
        byte[]? runtimeConfigPeloCabecalho = null)
    {
        string host = Repositorio.HostDeArquivoUnicoReal();
        string runtime = _pasta.Subpasta(@"runtime\pacote");
        Directory.CreateDirectory(Path.Combine(runtime, @"runtimes\win-x64\lib\net10.0"));
        Directory.CreateDirectory(Path.Combine(runtime, @"runtimes\win-x64\native"));
        File.WriteAllBytes(Path.Combine(runtime, @"runtimes\win-x64\lib\net10.0\System.Falso.dll"), DoRuntimeGerenciado);
        File.WriteAllBytes(Path.Combine(runtime, @"runtimes\win-x64\native\falso_nativo.dll"), DoRuntimeNativo);

        string pacote = Path.Combine(_pasta.Subpasta("publicado"), "Buzzy.exe");
        byte[] imagem = File.ReadAllBytes(host);
        byte[] espaco = Encoding.ASCII.GetBytes(ProcedenciaDoHost.EspacoDoNome);
        int posicao = imagem.AsSpan().IndexOf(espaco);
        Afirmar.Verdadeiro(posicao > 0, "o host do SDK tem o espaço reservado do nome");
        byte[] nome = Encoding.UTF8.GetBytes(nomeNoHost);
        Array.Clear(imagem, posicao, espaco.Length);
        nome.CopyTo(imagem, posicao);
        // O subsistema de janelas, como o SDK grava para um WinExe.
        BitConverter.GetBytes((ushort)2).CopyTo(imagem, BitConverter.ToInt32(imagem, 0x3c) + 24 + 68);
        File.WriteAllBytes(pacote, imagem);

        string textoDoManifesto = manifesto ?? TestesDoVerificadorDeManifesto.Manifesto();
        GravarManifesto(pacote, Encoding.UTF8.GetBytes(textoDoManifesto));

        byte[] comRecursos = File.ReadAllBytes(pacote);
        adulterarHost?.Invoke(comRecursos);
        using (var fluxo = new FileStream(pacote, FileMode.Create))
        using (var escritor = new BinaryWriter(fluxo, Encoding.UTF8))
        {
            escritor.Write(comRecursos);
            var indice = new List<(long Deslocamento, long Tamanho, long Comprimido, byte Tipo, string Caminho)>();
            foreach (Arquivo a in arquivos ?? ArquivosLimpos())
            {
                if (lacuna is not null && indice.Count == 1) escritor.Write(lacuna);
                long deslocamento = fluxo.Position;
                if (a.Comprimir)
                {
                    var comprimido = new MemoryStream();
                    using (var deflate = new DeflateStream(comprimido, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(a.Conteudo);
                    escritor.Write(comprimido.ToArray());
                    indice.Add((deslocamento, a.Conteudo.Length, comprimido.Length, a.Tipo, a.Caminho));
                }
                else
                {
                    escritor.Write(a.Conteudo);
                    indice.Add((deslocamento, a.Conteudo.Length, 0, a.Tipo, a.Caminho));
                }
            }
            if (sobrepor && indice.Count > 1) indice[1] = indice[1] with { Deslocamento = indice[0].Deslocamento };
            // Um runtimeconfig.json que só a posição do cabeçalho aponta, fora do índice (o host o leria; o portão, não).
            (long, long)? soNoCabecalho = null;
            if (runtimeConfigPeloCabecalho is not null)
            {
                soNoCabecalho = (fluxo.Position, runtimeConfigPeloCabecalho.Length);
                escritor.Write(runtimeConfigPeloCabecalho);
            }

            long cabecalho = fluxo.Position;
            escritor.Write(versao);
            escritor.Write(0u);
            escritor.Write(indice.Count);
            escritor.Write("id-de-teste");
            foreach (byte tipo in new byte[] { 3, 4 })
            {
                var doTipo = indice.Where(x => x.Tipo == tipo).ToList();
                (long posicaoNoCabecalho, long tamanho) = !posicoesDoCabecalho || doTipo.Count == 0 ? (0L, 0L) : (doTipo[0].Deslocamento, doTipo[0].Tamanho);
                if (tipo == 4 && soNoCabecalho is { } fora) (posicaoNoCabecalho, tamanho) = fora;
                escritor.Write(posicaoNoCabecalho);
                escritor.Write(tamanho);
            }
            escritor.Write(0UL);
            foreach (var e in indice)
            {
                escritor.Write(e.Deslocamento);
                escritor.Write(e.Tamanho);
                escritor.Write(e.Comprimido);
                escritor.Write(e.Tipo);
                escritor.Write(e.Caminho);
            }
            if (depoisDoIndice is not null) escritor.Write(depoisDoIndice);

            int assinatura = comRecursos.AsSpan().IndexOf(LeitorDePacote.Assinatura);
            Afirmar.Verdadeiro(assinatura > 8, "o host tem a assinatura do pacote");
            fluxo.Position = assinatura - 8;
            escritor.Write(cabecalho);
        }

        _pasta.Escrever(@"src\Limpo.cs", "namespace Buzzy;\ninternal static class Limpo { }\n");
        string app = _pasta.Escrever("app.manifest", TestesDoVerificadorDeManifesto.Manifesto());
        return new Montagem(pacote, host, runtime, Path.Combine(_pasta.Caminho, "src"), app);
    }

    [Teste]
    public void PacoteLimpo_Aprova_ComOHostDaMicrosoftEOProdutoLidoPorDentro()
    {
        (int codigo, string saida, string erros) = Rodar(Montar());
        Afirmar.Igual(0, codigo, saida + erros);
        Afirmar.Contem("Resumo: APROVADO", saida);
        Afirmar.Contem("pacote 6.0 com 6 arquivo(s): 2 do produto verificados por dentro, 2 do runtime da Microsoft por procedência (SHA-256), 2 de configuração", saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.Core.dll", saida);
        Afirmar.Contem("host de arquivo único:", saida);
        // As 33 importações revisadas do host (as 4 do lançador, LoadLibraryExA, CreateProcessW e as 27 do OLEAUT32).
        Afirmar.Igual(PermissoesDoHostDeArquivoUnico.Entradas.Count, Regex.Matches(saida, "permitida no host de arquivo único - ").Count, saida);
        Afirmar.Contem("OLEAUT32.dll!#2 [Código dinâmico] permitida no host de arquivo único - SysAllocString:", saida);
        Afirmar.Falso(saida.Contains(": error ", StringComparison.Ordinal), saida);
    }

    [Teste]
    public void HostComCodigoAlterado_Reprova()
    {
        (int codigo, string saida, _) = Rodar(Montar(adulterarHost: imagem =>
        {
            // Um byte no começo da .text (a primeira seção do host).
            int pe = BitConverter.ToInt32(imagem, 0x3c);
            int secao = pe + 24 + BitConverter.ToUInt16(imagem, pe + 20);
            int inicio = BitConverter.ToInt32(imagem, secao + 20);
            imagem[inicio + 100] ^= 0xFF;
        }));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("error BZP007", saida);
        Afirmar.Contem(".text: conteúdo diferente do host do SDK", saida);
    }

    [Teste]
    public void HostComOutroNomeDeAplicativo_Reprova()
    {
        (int codigo, string saida, _) = Rodar(Montar(nomeNoHost: "Outro.dll"));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("error BZP007", saida);
        Afirmar.Contem("o nome gravado no host não é Buzzy.dll", saida);
    }

    [Teste]
    public void ProdutoComPInvokeProibido_DentroDoPacote_Reprova()
    {
        List<Arquivo> arquivos = ArquivosLimpos();
        arquivos[1] = new Arquivo("Buzzy.Core.dll", 1, Produto("Buzzy.Core", a => a.DeclararPInvoke("user32.dll", "SendInput")));
        (int codigo, string saida, _) = Rodar(Montar(arquivos));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.Core.dll: error BZP001: [Injetar input] user32.dll!SendInput", saida);
    }

    [Teste]
    public void ArquivoDoRuntimeAlterado_OuDeTerceiro_Reprova()
    {
        List<Arquivo> alterado = ArquivosLimpos();
        alterado[2] = alterado[2] with { Conteudo = [.. DoRuntimeGerenciado, 0x00] };
        (int codigo, string saida, _) = Rodar(Montar(alterado));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!System.Falso.dll: error BZP006", saida);

        List<Arquivo> terceiro = [.. ArquivosLimpos(), new Arquivo("Terceiro.dll", 1, Produto("Terceiro"))];
        (codigo, saida, _) = Rodar(Montar(terceiro));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!Terceiro.dll: error BZP006", saida);

        // Um arquivo de símbolos não passa; um segundo deps.json torna o pacote ilegível (o host leria um só).
        (codigo, saida, _) = Rodar(Montar([.. ArquivosLimpos(), new Arquivo("Buzzy.pdb", 5, [1, 2, 3])]));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.pdb: error BZP006", saida);
        (codigo, _, string erros) = Rodar(Montar([.. ArquivosLimpos(), new Arquivo("Outro.deps.json", 3, "{}"u8.ToArray())]));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("a posição do deps.json no cabeçalho", erros);
    }

    [Teste]
    public void ManifestoEmbutidoComElevacao_Reprova()
    {
        string elevado = TestesDoVerificadorDeManifesto.Manifesto(nivel: "<requestedExecutionLevel level=\"requireAdministrator\" uiAccess=\"false\" />");
        (int codigo, string saida, _) = Rodar(Montar(manifesto: elevado));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!RT_MANIFEST", saida);
        Afirmar.Contem("error BZP005", saida);
    }

    [Teste]
    public void PacoteIlegivel_OuUsoErrado_SaiComDois()
    {
        (int codigo, _, string erros) = Rodar(Montar(versao: 7));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("versão do pacote 7.0 não revisada", erros);

        List<Arquivo> fora = ArquivosLimpos();
        fora[0] = fora[0] with { Caminho = @"..\Buzzy.dll" };
        (codigo, _, erros) = Rodar(Montar(fora));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("caminho inválido no pacote", erros);

        (codigo, _, erros) = Rodar(Montar(sobrepor: true));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("sobrepostos", erros);

        // O host sem pacote nenhum não é um pacote.
        string host = Repositorio.HostDeArquivoUnicoReal();
        string src = _pasta.Escrever(@"src2\Limpo.cs", "namespace Buzzy;\ninternal static class Limpo { }\n");
        string runtime = Montar().Runtime;
        (codigo, _, erros) = Rodar("--pacote", host, "--host-de-arquivo-unico", host, "--runtime", runtime, "--fonte", Path.GetDirectoryName(src)!);
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("não é um pacote de arquivo único", erros);

        foreach ((string[] argumentos, string mensagem) in new (string[], string)[]
        {
            (["--pacote", host, "--runtime", runtime, "--fonte", "x"], "--pacote precisa de --host-de-arquivo-unico"),
            (["--pacote", host, "--host-de-arquivo-unico", host, "--fonte", "x"], "--pacote precisa de pelo menos um --runtime"),
            (["--pacote", host, "--binarios", "x", "--fonte", "x"], "não vão juntos"),
            (["--binarios", "x", "--host-de-arquivo-unico", host, "--fonte", "x"], "só vale com --pacote"),
        })
        {
            (codigo, _, erros) = Rodar(argumentos);
            Afirmar.Igual(2, codigo, string.Join(" ", argumentos));
            Afirmar.Contem(mensagem, erros);
        }
    }

    // Revisão adversarial do F9-P10, achados de alta: o runtimeconfig.json e o deps.json por lista fechada; as posições
    // do cabeçalho (as que o host usa) iguais às do índice; nada depois do índice nem entre as entradas.
    [Teste]
    public void ConfiguracaoDoRuntimeForaDaLista_Reprova()
    {
        List<Arquivo> gancho = ArquivosLimpos();
        gancho[5] = gancho[5] with { Conteudo = Encoding.UTF8.GetBytes(RuntimeConfig("\"STARTUP_HOOKS\": \"C:/x/gancho.dll\", ")) };
        (int codigo, string saida, _) = Rodar(Montar(gancho));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.runtimeconfig.json: error BZP008: [Código dinâmico] STARTUP_HOOKS", saida);

        List<Arquivo> pacoteDeFora = ArquivosLimpos();
        string deps = Deps.Replace("\"runtimes\": {}", "\"runtimes\": {}, \"x\": 1", StringComparison.Ordinal)
            .Replace("\"Buzzy.Core/1.0.0\": { \"type\": \"project\"", "\"Gancho/1.0.0\": { \"type\": \"package\"", StringComparison.Ordinal);
        pacoteDeFora[4] = pacoteDeFora[4] with { Conteudo = Encoding.UTF8.GetBytes(deps) };
        (codigo, saida, _) = Rodar(Montar(pacoteDeFora));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.deps.json: error BZP008: [Código dinâmico] Gancho/1.0.0 (package)", saida);
        Afirmar.Contem("Buzzy.exe!Buzzy.deps.json: error BZP008: [Código dinâmico] x - chave do deps.json", saida);

        // Sem o runtimeconfig.json no pacote, também reprova.
        (codigo, saida, _) = Rodar(Montar([.. ArquivosLimpos().Where(a => a.Tipo != 4)]));
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("o pacote não tem o Buzzy.runtimeconfig.json e o Buzzy.deps.json", saida);
    }

    [Teste]
    public void IndiceDesencontradoDoCabecalho_OuComSobras_EhIlegivel()
    {
        (int codigo, _, string erros) = Rodar(Montar(runtimeConfigPeloCabecalho: Encoding.UTF8.GetBytes(RuntimeConfig("\"STARTUP_HOOKS\": \"x.dll\", "))));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("a posição do runtimeconfig.json no cabeçalho", erros);

        (codigo, _, erros) = Rodar(Montar(posicoesDoCabecalho: false));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("a posição do deps.json no cabeçalho (0, 0)", erros);

        (codigo, _, erros) = Rodar(Montar(depoisDoIndice: [1, 2, 3]));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("3 byte(s) depois do índice", erros);

        (codigo, _, erros) = Rodar(Montar(lacuna: [0x90, 0x90]));
        Afirmar.Igual(2, codigo, erros);
        Afirmar.Contem("bytes que não são zero antes de", erros);

        // Zeros entre as entradas (o alinhamento do empacotador) passam.
        (codigo, string saida, erros) = Rodar(Montar(lacuna: new byte[16]));
        Afirmar.Igual(0, codigo, saida + erros);
    }

    [Teste]
    public void CabecalhoDoHostAlterado_Reprova()
    {
        static int Opcional(byte[] i) => BitConverter.ToInt32(i, 0x3c) + 24;
        static int SecaoDosRecursos(byte[] i)
        {
            int pe = BitConverter.ToInt32(i, 0x3c), n = BitConverter.ToUInt16(i, pe + 6), tamanho = BitConverter.ToUInt16(i, pe + 20);
            for (int k = 0; k < n; k++)
            {
                int s = pe + 24 + tamanho + k * 40;
                if (Encoding.ASCII.GetString(i, s, 5) == ".rsrc") return s;
            }
            throw new InvalidOperationException("sem .rsrc");
        }

        foreach ((string caso, Action<byte[]> adulterar, string esperado) in new (string, Action<byte[]>, string)[]
        {
            ("ponto de entrada", i => i[Opcional(i) + 16] ^= 0x10, "cabeçalho: byte"),
            ("sem ASLR nem DEP", i => i[Opcional(i) + 70] = 0, "cabeçalho: byte"),
            (".rsrc executável", i => i[SecaoDosRecursos(i) + 39] |= 0x20, ".rsrc com características diferentes"),
            ("console", i => i[Opcional(i) + 68] = 3, "Subsystem não é o de janelas"),
        })
        {
            (int codigo, string saida, _) = Rodar(Montar(adulterarHost: adulterar));
            Afirmar.Igual(1, codigo, $"{caso}: {saida}");
            Afirmar.Contem("error BZP007", saida, caso);
            Afirmar.Contem(esperado, saida, caso);
        }
    }

    // A pasta de saída autocontida: com --runtime, o binário do runtime da Microsoft passa por procedência; alterado, reprova.
    [Teste]
    public void PastaAutocontida_ORuntimePassaPorProcedencia_EAlteradoReprova()
    {
        Montagem m = Montar();
        string binarios = _pasta.Subpasta("autocontido");
        File.WriteAllBytes(Path.Combine(binarios, "Buzzy.dll"), Produto("Buzzy"));
        File.WriteAllBytes(Path.Combine(binarios, "Buzzy.Core.dll"), Produto("Buzzy.Core"));
        File.Copy(Repositorio.ApphostReal(), Path.Combine(binarios, "Buzzy.exe"));
        File.WriteAllBytes(Path.Combine(binarios, "System.Falso.dll"), DoRuntimeGerenciado);
        File.WriteAllBytes(Path.Combine(binarios, "falso_nativo.dll"), DoRuntimeNativo);
        File.WriteAllText(Path.Combine(binarios, "Buzzy.runtimeconfig.json"), RuntimeConfig());
        File.WriteAllText(Path.Combine(binarios, "Buzzy.deps.json"), Deps);

        (int codigo, string saida, _) = Rodar("--binarios", binarios, "--runtime", m.Runtime, "--fonte", m.Fonte, "--manifesto", m.Manifesto);
        Afirmar.Igual(0, codigo, saida);
        Afirmar.Contem("por procedência", saida);
        Afirmar.Contem("): 2.", saida);

        File.WriteAllBytes(Path.Combine(binarios, "falso_nativo.dll"), [.. DoRuntimeNativo, 0x01]);
        (codigo, saida, _) = Rodar("--binarios", binarios, "--runtime", m.Runtime, "--fonte", m.Fonte, "--manifesto", m.Manifesto);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("falso_nativo.dll: error BZP006", saida);
        Afirmar.Contem("nem, com o mesmo SHA-256, dos pacotes de runtime da Microsoft", saida);

        // Na pasta, o runtimeconfig.json também passa pela lista fechada.
        File.WriteAllBytes(Path.Combine(binarios, "falso_nativo.dll"), DoRuntimeNativo);
        File.WriteAllText(Path.Combine(binarios, "Buzzy.runtimeconfig.json"), RuntimeConfig("\"STARTUP_HOOKS\": \"x.dll\", "));
        (codigo, saida, _) = Rodar("--binarios", binarios, "--runtime", m.Runtime, "--fonte", m.Fonte, "--manifesto", m.Manifesto);
        Afirmar.Igual(1, codigo, saida);
        Afirmar.Contem("Buzzy.runtimeconfig.json: error BZP008", saida);
    }

    /// <summary>O manifesto nos recursos (RT_MANIFEST, id 1), pela mesma API do Windows que o SDK usa no host.</summary>
    private static void GravarManifesto(string arquivo, byte[] manifesto)
    {
        nint atualizacao = BeginUpdateResourceW(arquivo, false);
        Afirmar.Verdadeiro(atualizacao != 0, $"BeginUpdateResource: {Marshal.GetLastPInvokeError()}");
        bool gravou = UpdateResourceW(atualizacao, 24, 1, 0, manifesto, (uint)manifesto.Length);
        Afirmar.Verdadeiro(EndUpdateResourceW(atualizacao, !gravou) && gravou, $"UpdateResource: {Marshal.GetLastPInvokeError()}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint BeginUpdateResourceW(string arquivo, bool apagarExistentes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResourceW(nint atualizacao, nint tipo, nint nome, ushort idioma, byte[] dados, uint tamanho);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResourceW(nint atualizacao, bool descartar);
}
