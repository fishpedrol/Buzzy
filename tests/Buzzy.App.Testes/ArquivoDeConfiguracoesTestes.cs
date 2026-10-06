using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O settings.json no disco (Fase 5, ARCHITECTURE.md 2.12): a leitura sem efeito colateral, na cascata principal →
/// reserva → padrões, e a gravação atômica, com falha simulada em cada etapa, todos os estados que uma queda pode
/// deixar, arquivos presos por outro FileStream e quedas de verdade (um processo filho encerrado à força no meio das
/// gravações); e as pastas de dados e de perfil de teste. Cada teste usa uma pasta temporária própria, apagada no
/// fim: nenhum lê nem grava em %LOCALAPPDATA%\Buzzy.
/// </summary>
internal sealed class ArquivoDeConfiguracoesTestes : IDisposable
{
    private const string Principal = ArquivoDeConfiguracoes.NomePrincipal;
    private const string Reserva = ArquivoDeConfiguracoes.NomeReserva;
    private const string Temporario = ArquivoDeConfiguracoes.NomeTemporario;
    private const string Copia = ArquivoDeConfiguracoes.NomeIlegivel;

    // A no principal com as preferências padrão; B no secundário à esquerda; N (no ar, no DISPLAY2) e N2 (sem posição) são os novos.
    private static readonly ConfiguracoesSalvas A = new(Posicao(@"\\.\DISPLAY1", 0.85, 1, new RetanguloPx(0, 0, 1920, 1080), new PontoPx(1632, 1032)), Preferencias.Padrao);
    private static readonly ConfiguracoesSalvas B = new(Posicao(@"\\.\DISPLAY2", 0.25, 1, new RetanguloPx(-1920, 0, 0, 1080), new PontoPx(-1440, 1032)), new Preferencias(NivelDeEnergia.Baixa, true, true));
    private static readonly ConfiguracoesSalvas N = new(Posicao("mon:6852e0b1cd2318a2", 0.5, 0.321705, new RetanguloPx(1920, 0, 3840, 1080), new PontoPx(2880, 332)), new Preferencias(NivelDeEnergia.Alta, false, false));
    private static readonly ConfiguracoesSalvas N2 = new(null, new Preferencias(NivelDeEnergia.Media, false, true));

    private static readonly byte[] Lixo = "{ lixo"u8.ToArray();

    /// <summary>Versão futura (a 8, depois da v7 atual) com campos que a v7 não conhece, na raiz, na posição e nas preferências.</summary>
    private static readonly byte[] Futura = """
        {
          "schemaVersion": 8,
          "posicao": { "chaveMonitor": "\\\\.\\DISPLAY2", "fracaoX": 0.25, "fracaoY": 1, "campoNovo": [1, 2] },
          "preferencias": { "energia": "alta", "preferenciaNova": "x" },
          "secaoNova": { "a": 1 }
        }
        """u8.ToArray();

    private readonly string _raiz = Directory.CreateTempSubdirectory("buzzy-config-").FullName;
    private int _pastas;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"         limpeza: a pasta temporária {_raiz} ficou para o sistema apagar ({e.GetType().Name})");
        }
    }

    // ------------------------------------------------------------------ pastas

    [Teste]
    public void PastaDoBuzzy_PelaPastaConhecidaESoComCaminhoCompleto()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        Afirmar.Igual(Path.Combine(local, "Buzzy"), PastaDeDados.DoBuzzy());
        Afirmar.Igual(PastaDeDados.DoBuzzy(), Diagnostico.PastaDeDados(), "o log de diagnóstico fica na mesma pasta");

        // Sem a pasta local, ou com um caminho que não é completo, não há pasta: nunca o relativo "Buzzy".
        foreach (string? invalida in new[] { null, "", "Buzzy", @"relativo\pasta", "C:relativo", @"\sem\unidade" })
            Afirmar.Nulo(PastaDeDados.DoBuzzy(invalida), $"pasta local \"{invalida}\"");
        Afirmar.Igual(@"C:\Users\x\AppData\Local\Buzzy", PastaDeDados.DoBuzzy(@"C:\Users\x\AppData\Local"));
        Afirmar.Igual(@"\\servidor\perfis\x\Buzzy", PastaDeDados.DoBuzzy(@"\\servidor\perfis\x"));
    }

    [Teste]
    public void PerfilDeTeste_ValidaNomeENuncaSaiDaPasta()
    {
        string buzzy = Afirmar.NaoNulo(PastaDeDados.DoBuzzy());
        string testes = Path.GetFullPath(Path.Combine(buzzy, "testes"));

        string[] aceitos = ["integracao", "verificacao", "desempenho", "persistencia", "a-1", "a", "0", "a-", "com10", "lpt", "nul0", new string('z', 32)];
        foreach (string nome in aceitos)
        {
            Afirmar.Verdadeiro(PastaDeDados.NomeDePerfilValido(nome), $"\"{nome}\" é aceito");
            string pasta = Afirmar.NaoNulo(PastaDeDados.DoPerfilDeTeste(nome), nome);
            Afirmar.Igual(Path.Combine(buzzy, "testes", nome), pasta);

            // Pelo caminho resolvido: subpasta direta de testes, com o próprio nome.
            string resolvida = Path.GetFullPath(pasta);
            Afirmar.Igual(testes, Path.GetDirectoryName(resolvida), nome);
            Afirmar.Igual(nome, Path.GetFileName(resolvida));
        }

        string?[] recusados =
        [
            null, "", " ", ".", "..", "...", "a/b", @"a\b", @"..\x", "../x", @"C:\x", "c:", "a:b", "A", "Integracao", " a", "a ",
            "-a", "a.b", "a_b", "a\n", "a\r\n", "\ta", "ção", "é", "１", "a\0", new string('z', 33),
            "con", "prn", "aux", "nul", "com0", "com1", "com9", "lpt0", "lpt1", "lpt9",
        ];
        foreach (string? nome in recusados)
        {
            string visivel = nome?.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal) ?? "nulo";
            Afirmar.Falso(PastaDeDados.NomeDePerfilValido(nome), $"\"{visivel}\" é recusado");
            Afirmar.Nulo(PastaDeDados.DoPerfilDeTeste(nome), $"\"{visivel}\" não tem pasta");
        }
    }

    [Teste]
    public void PastaDasConfiguracoes_FalhaFechadaNumaRegraSo()
    {
        // A pasta que o aplicativo usa, pelas opções da linha de comando: com um perfil pedido, nunca a pasta real.
        const string Buzzy = @"C:\Users\x\AppData\Local\Buzzy";
        (string? Perfil, bool Desligada, string? PastaDoBuzzy, string? Esperada)[] casos =
        [
            (null, false, Buzzy, Buzzy),
            (null, true, Buzzy, null),
            (null, false, null, null),
            ("integracao", false, Buzzy, @"C:\Users\x\AppData\Local\Buzzy\testes\integracao"),
            ("a-1", false, Buzzy, @"C:\Users\x\AppData\Local\Buzzy\testes\a-1"),
            ("integracao", true, Buzzy, null),
            ("integracao", false, null, null),
            ("", false, Buzzy, null),
            ("Integracao", false, Buzzy, null),
            (@"..\..\Buzzy", false, Buzzy, null),
            ("..", false, Buzzy, null),
            (@"C:\Users", false, Buzzy, null),
            ("con", false, Buzzy, null),
            (new string('z', 33), false, Buzzy, null),
        ];
        foreach ((string? perfil, bool desligada, string? pastaDoBuzzy, string? esperada) in casos)
        {
            string rotulo = $"perfil {perfil ?? "nenhum"}, persistência {(desligada ? "desligada" : "ligada")}, pasta do Buzzy {pastaDoBuzzy ?? "nenhuma"}";
            string? obtida = PastaDeDados.DasConfiguracoes(perfil, desligada, pastaDoBuzzy);
            Afirmar.Igual(esperada, obtida, rotulo);
            if (perfil is not null)
                Afirmar.Verdadeiro(obtida is null || obtida != pastaDoBuzzy, rotulo + ": com um perfil pedido, nunca a pasta real");
        }

        // A fábrica do arquivo usa a mesma regra, com a pasta do Buzzy do Windows; sem pasta, não há arquivo. Nada é criado.
        string real = Afirmar.NaoNulo(PastaDeDados.DoBuzzy());
        Afirmar.Igual(real, Afirmar.NaoNulo(ArquivoDeConfiguracoes.DaExecucao(null, persistenciaDesligada: false)).Pasta, "sem perfil, a pasta do Buzzy");
        Afirmar.Igual(PastaDeDados.DoPerfilDeTeste("integracao"), Afirmar.NaoNulo(ArquivoDeConfiguracoes.DaExecucao("integracao", false)).Pasta, "com o perfil");
        Afirmar.Nulo(ArquivoDeConfiguracoes.DaExecucao(null, persistenciaDesligada: true), "persistência desligada");
        Afirmar.Nulo(ArquivoDeConfiguracoes.DaExecucao("integracao", persistenciaDesligada: true), "perfil com a persistência desligada");
        Afirmar.Nulo(ArquivoDeConfiguracoes.DaExecucao("Integracao", false), "perfil inválido");
        Afirmar.Nulo(ArquivoDeConfiguracoes.DaExecucao("", false), "perfil vazio");
    }

    // ------------------------------------------------------------------ leitura

    [Teste]
    public void Ler_PastaVazia_PadroesSemCriarNada()
    {
        string pasta = Path.Combine(_raiz, "Buzzy");
        LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Padroes, lida.Origem);
        Afirmar.Igual(ConfiguracoesSalvas.Padrao, lida.Configuracoes);
        Afirmar.Igual(EstadoDoArquivo.Ausente, lida.Principal);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Ausente, lida.Reserva);
        Afirmar.Falso(lida.GravacaoBloqueada);
        Afirmar.Nulo(lida.Versao);
        Afirmar.Igual(0, lida.Avisos);
        Afirmar.Igual(1, lida.TentativasNoPrincipal, "ausente: uma tentativa só");
        Afirmar.Falso(Directory.Exists(pasta), "a leitura não cria a pasta");

        Directory.CreateDirectory(pasta);
        lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Padroes, lida.Origem);
        Afirmar.Sequencia([], Nomes(pasta), "a leitura não cria nenhum arquivo");
    }

    [Teste]
    public void Ler_EstadosDeQuedaExaustivos()
    {
        // Todo estado que uma queda pode deixar, antes, dentro e depois da troca, com o temporário cortado em cada
        // tamanho possível, de vazio a completo: ele nunca é lido.
        byte[] novo = Json(N);
        (string Nome, byte[]? Principal, byte[]? Reserva, byte[]? Copia, OrigemDasConfiguracoes Origem, ConfiguracoesSalvas Esperadas)[] estados =
        [
            ("principal A (queda antes da troca)", Json(A), null, null, OrigemDasConfiguracoes.Principal, Norm(A)),
            ("principal A e reserva B (queda antes da troca)", Json(A), Json(B), null, OrigemDasConfiguracoes.Principal, Norm(A)),
            ("só a reserva A (queda dentro da troca de um principal válido)", null, Json(A), null, OrigemDasConfiguracoes.Reserva, Norm(A)),
            ("reserva B e cópia do ilegível (queda dentro da troca de um principal ilegível)", null, Json(B), Lixo, OrigemDasConfiguracoes.Reserva, Norm(B)),
            ("só a cópia do ilegível (a mesma queda, sem reserva)", null, null, Lixo, OrigemDasConfiguracoes.Padroes, ConfiguracoesSalvas.Padrao),
            ("só o temporário (queda na primeira gravação)", null, null, null, OrigemDasConfiguracoes.Padroes, ConfiguracoesSalvas.Padrao),
            ("principal N e reserva A (queda depois da troca)", Json(N), Json(A), null, OrigemDasConfiguracoes.Principal, Norm(N)),
        ];

        int leituras = 0;
        foreach (var estado in estados)
        {
            string pasta = NovaPasta();
            Directory.CreateDirectory(pasta);
            Escrever(pasta, Principal, estado.Principal);
            Escrever(pasta, Reserva, estado.Reserva);
            Escrever(pasta, Copia, estado.Copia);
            string temporario = Path.Combine(pasta, Temporario);
            for (int k = 0; k <= novo.Length; k++)
            {
                File.WriteAllBytes(temporario, novo[..k]);
                string antes = Retrato(pasta, comConteudo: false);
                LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
                string rotulo = $"{estado.Nome}, temporário com {k} de {novo.Length} bytes";
                Afirmar.Igual(estado.Origem, lida.Origem, rotulo);
                Afirmar.Igual(estado.Esperadas, lida.Configuracoes, rotulo);
                Afirmar.Falso(lida.GravacaoBloqueada, rotulo);
                Afirmar.Igual(antes, Retrato(pasta, comConteudo: false), rotulo + ": a leitura não muda nenhum arquivo");
                leituras++;
            }

            // E o conteúdo dos arquivos que a queda deixou continua o mesmo depois de todas as leituras.
            MesmosBytes(estado.Principal, Ler(pasta, Principal), estado.Nome + ": principal");
            MesmosBytes(estado.Reserva, Ler(pasta, Reserva), estado.Nome + ": reserva");
            MesmosBytes(estado.Copia, Ler(pasta, Copia), estado.Nome + ": cópia de diagnóstico");
            MesmosBytes(novo, Ler(pasta, Temporario), estado.Nome + ": temporário");
        }
        Console.WriteLine($"         {leituras} estados de queda lidos ({estados.Length} arquivos × {novo.Length + 1} cortes do temporário)");
    }

    [Teste]
    public void Ler_PrincipalIlegivel_UsaBak_EPrimeiraGravacaoGuardaUmaCopiaSo()
    {
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Lixo);
        Escrever(pasta, Reserva, Json(B));
        string antes = Retrato(pasta);

        var arquivo = new ArquivoDeConfiguracoes(pasta);
        LeituraDoArquivo lida = arquivo.Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Reserva, lida.Origem);
        Afirmar.Igual(EstadoDoArquivo.Ilegivel, lida.Principal);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Valido, lida.Reserva);
        Afirmar.Igual(Norm(B), lida.Configuracoes);
        Afirmar.Igual<int?>(EsquemaDeConfiguracoes.VersaoAtual, lida.Versao, "a reserva foi gravada pelo esquema atual");
        Afirmar.Falso(lida.GravacaoBloqueada);
        Afirmar.Igual(antes, Retrato(pasta), "a leitura não muda nenhum arquivo");

        // A primeira gravação guarda o ilegível como cópia de diagnóstico, e a reserva continua sendo o último arquivo bom.
        ResultadoDaGravacao r = arquivo.Gravar(N);
        Afirmar.Verdadeiro(r.Gravou, r.Erro);
        Afirmar.Verdadeiro(r.CopiaDeDiagnostico);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Ilegivel, r.PrincipalAntes);
        MesmosBytes(Json(N), Ler(pasta, Principal), "principal");
        MesmosBytes(Json(B), Ler(pasta, Reserva), "reserva");
        MesmosBytes(Lixo, Ler(pasta, Copia), "cópia de diagnóstico");

        // Outro ilegível depois: a cópia é trocada, nunca acumulada.
        byte[] outroLixo = "[1, 2"u8.ToArray();
        Escrever(pasta, Principal, outroLixo);
        var outra = new ArquivoDeConfiguracoes(pasta);
        Afirmar.Igual(OrigemDasConfiguracoes.Reserva, outra.Ler().Origem);
        r = outra.Gravar(N2);
        Afirmar.Verdadeiro(r.Gravou && r.CopiaDeDiagnostico, r.Erro);
        MesmosBytes(Json(N2), Ler(pasta, Principal), "principal");
        MesmosBytes(Json(B), Ler(pasta, Reserva), "reserva");
        MesmosBytes(outroLixo, Ler(pasta, Copia), "cópia de diagnóstico");
        Afirmar.Sequencia(Ordenados(Principal, Reserva, Copia), Nomes(pasta), "uma cópia de diagnóstico só");
    }

    [Teste]
    public void Ler_PrincipalIlegivelSemBak_Padroes()
    {
        // Fora do JSON, fora do UTF-8, vazio, raiz que não é objeto e schemaVersion inválido ou ausente.
        byte[][] ilegiveis =
        [
            Lixo, [], [0xC3, 0x28], "[]"u8.ToArray(), "null"u8.ToArray(), """{"schemaVersion": 0}"""u8.ToArray(),
            """{"posicao": {}}"""u8.ToArray(), """{"schemaVersion": "1"}"""u8.ToArray(),
        ];
        for (int i = 0; i < ilegiveis.Length; i++)
        {
            string pasta = NovaPasta();
            string rotulo = $"ilegível {i}: {Descrever(ilegiveis[i])}";
            Escrever(pasta, Principal, ilegiveis[i]);
            string antes = Retrato(pasta);

            var arquivo = new ArquivoDeConfiguracoes(pasta);
            LeituraDoArquivo lida = arquivo.Ler();
            Afirmar.Igual(OrigemDasConfiguracoes.Padroes, lida.Origem, rotulo);
            Afirmar.Igual(ConfiguracoesSalvas.Padrao, lida.Configuracoes, rotulo);
            Afirmar.Igual(EstadoDoArquivo.Ilegivel, lida.Principal, rotulo);
            Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Ausente, lida.Reserva, rotulo);
            Afirmar.Falso(lida.GravacaoBloqueada, rotulo);
            Afirmar.Igual(antes, Retrato(pasta), rotulo + ": a leitura não muda nenhum arquivo");

            ResultadoDaGravacao r = arquivo.Gravar(N);
            Afirmar.Verdadeiro(r.Gravou && r.CopiaDeDiagnostico, $"{rotulo}: {r.Erro}");
            MesmosBytes(ilegiveis[i], Ler(pasta, Copia), rotulo + ": cópia de diagnóstico");
            Afirmar.Nulo(Ler(pasta, Reserva), rotulo + ": sem reserva, porque não havia arquivo bom");
        }
    }

    [Teste]
    public void Ler_VersaoFutura_BloqueiaGravacao()
    {
        // Principal de versão futura: vale o que se conhece, e nada é gravado por cima.
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Futura);
        string antes = Retrato(pasta);
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        LeituraDoArquivo lida = arquivo.Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Principal, lida.Origem);
        Afirmar.Igual(EstadoDoArquivo.VersaoFutura, lida.Principal);
        Afirmar.Nulo(lida.Reserva, "a reserva nem é consultada");
        Afirmar.Igual<int?>(EsquemaDeConfiguracoes.VersaoAtual + 1, lida.Versao);
        Afirmar.Verdadeiro(lida.GravacaoBloqueada);
        PosicaoDoPersonagem posicao = Afirmar.NaoNulo(lida.Configuracoes.Posicao);
        Afirmar.Igual(@"\\.\DISPLAY2", posicao.ChaveMonitor);
        Afirmar.Igual(0.25, posicao.FracaoX);
        Afirmar.Igual(NivelDeEnergia.Alta, lida.Configuracoes.Preferencias.Energia);

        ResultadoDaGravacao r = arquivo.Gravar(N);
        Afirmar.Falso(r.Gravou);
        Afirmar.Igual("bloqueada", r.Erro);
        Afirmar.Igual(0, r.Tentativas);
        Afirmar.Igual(antes, Retrato(pasta), "bloqueada, a gravação não toca em nada");

        // Reserva de versão futura, com o principal ilegível: o mesmo bloqueio.
        pasta = NovaPasta();
        Escrever(pasta, Principal, Lixo);
        Escrever(pasta, Reserva, Futura);
        antes = Retrato(pasta);
        arquivo = new ArquivoDeConfiguracoes(pasta);
        lida = arquivo.Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Reserva, lida.Origem);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.VersaoFutura, lida.Reserva);
        Afirmar.Verdadeiro(lida.GravacaoBloqueada);
        Afirmar.Igual("bloqueada", arquivo.Gravar(N).Erro);
        Afirmar.Igual(antes, Retrato(pasta), "bloqueada pela reserva, a gravação não toca em nada");

        // Uma versão mais nova gravou depois da leitura: a gravação confere o principal, desiste e bloqueia.
        pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        arquivo = new ArquivoDeConfiguracoes(pasta);
        Afirmar.Falso(arquivo.Ler().GravacaoBloqueada);
        Escrever(pasta, Principal, Futura);
        r = arquivo.Gravar(N);
        Afirmar.Falso(r.Gravou);
        Afirmar.Igual("versaoFutura", r.Erro);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.VersaoFutura, r.PrincipalAntes);
        Afirmar.Verdadeiro(arquivo.GravacaoBloqueada);
        MesmosBytes(Futura, Ler(pasta, Principal), "o arquivo da versão mais nova fica como estava");
        Afirmar.Sequencia([Principal], Nomes(pasta), "sem reserva e sem temporário");
        Afirmar.Igual("bloqueada", arquivo.Gravar(N).Erro, "e continua bloqueada nesta execução");
    }

    [Teste]
    public void Ler_PrincipalInacessivel_UsaBakEBloqueia()
    {
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        Escrever(pasta, Reserva, Json(B));
        var arquivo = new ArquivoDeConfiguracoes(pasta);

        using (new FileStream(Path.Combine(pasta, Principal), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var relogio = Stopwatch.StartNew();
            LeituraDoArquivo lida = arquivo.Ler();
            relogio.Stop();
            Afirmar.Igual(EstadoDoArquivo.Inacessivel, lida.Principal);
            Afirmar.Igual(OrigemDasConfiguracoes.Reserva, lida.Origem);
            Afirmar.Igual(Norm(B), lida.Configuracoes);
            Afirmar.Verdadeiro(lida.GravacaoBloqueada, "não se grava por cima do que não se conseguiu ler");
            Afirmar.Igual(ArquivoDeConfiguracoes.TentativasDeLeitura, lida.TentativasNoPrincipal, "tentou todas as vezes antes de desistir");

            // Com a pausa entre as tentativas. Só um mínimo, que a máquina carregada não derruba; e cada pausa pode
            // acordar até um tique do relógio do Windows (15,6 ms) antes do pedido. Sem pausa, levaria poucos ms.
            TimeSpan minimo = (ArquivoDeConfiguracoes.TentativasDeLeitura - 1) * (ArquivoDeConfiguracoes.PausaEntreLeituras - TimeSpan.FromMilliseconds(16));
            Afirmar.Verdadeiro(relogio.Elapsed >= minimo,
                $"{ArquivoDeConfiguracoes.TentativasDeLeitura} tentativas antes de desistir, com pausa: {relogio.ElapsedMilliseconds} ms");

            Afirmar.Igual("bloqueada", arquivo.Gravar(N).Erro);
        }

        // Liberado, continua bloqueado nesta execução: a leitura da partida não viu o principal.
        Afirmar.Igual("bloqueada", arquivo.Gravar(N).Erro);
        MesmosBytes(Json(A), Ler(pasta, Principal), "principal");
        MesmosBytes(Json(B), Ler(pasta, Reserva), "reserva");
        Afirmar.Sequencia(Ordenados(Principal, Reserva), Nomes(pasta));

        // Sem reserva: os padrões, também com a gravação bloqueada.
        pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        using (new FileStream(Path.Combine(pasta, Principal), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
            Afirmar.Igual(OrigemDasConfiguracoes.Padroes, lida.Origem);
            Afirmar.Verdadeiro(lida.GravacaoBloqueada);
        }
    }

    [Teste]
    public void Ler_PrincipalAbertoParaEscritaPorOutro_LeMesmoAssim()
    {
        // Um editor com o arquivo aberto para escrita (compartilhando leitura, escrita e exclusão) não impede a leitura:
        // ela compartilha tudo, e nunca pede escrita nem exclusão.
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        using var editor = new FileStream(Path.Combine(pasta, Principal), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Principal, lida.Origem);
        Afirmar.Igual(Norm(A), lida.Configuracoes);
        Afirmar.Falso(lida.GravacaoBloqueada);
        Afirmar.Igual(1, lida.TentativasNoPrincipal, "leu de primeira, sem esperar");
    }

    [Teste]
    public void Ler_ArquivoDe70KB_Ilegivel()
    {
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Preenchido(Json(A), 70 * 1024));
        Escrever(pasta, Reserva, Json(B));
        LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(EstadoDoArquivo.Ilegivel, lida.Principal, "70 KB, mesmo sendo JSON válido");
        Afirmar.Igual(OrigemDasConfiguracoes.Reserva, lida.Origem);
        Afirmar.Igual(Norm(B), lida.Configuracoes);

        // O limite: exatamente 64 KiB é lido; um byte a mais, não.
        Escrever(pasta, Principal, Preenchido(Json(A), EsquemaDeConfiguracoes.TamanhoMaximoEmBytes));
        lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(EstadoDoArquivo.Valido, lida.Principal, "exatamente no limite");
        Afirmar.Igual(Norm(A), lida.Configuracoes);
        Escrever(pasta, Principal, Preenchido(Json(A), EsquemaDeConfiguracoes.TamanhoMaximoEmBytes + 1));
        Afirmar.Igual(EstadoDoArquivo.Ilegivel, new ArquivoDeConfiguracoes(pasta).Ler().Principal, "um byte além do limite");

        // Gravar por cima do grande o guarda como a cópia de diagnóstico, inteiro.
        byte[] grande = Preenchido(Json(A), 70 * 1024);
        Escrever(pasta, Principal, grande);
        ResultadoDaGravacao r = new ArquivoDeConfiguracoes(pasta).Gravar(N);
        Afirmar.Verdadeiro(r.Gravou && r.CopiaDeDiagnostico, r.Erro);
        MesmosBytes(grande, Ler(pasta, Copia), "cópia de diagnóstico");
        MesmosBytes(Json(B), Ler(pasta, Reserva), "reserva");
    }

    // ------------------------------------------------------------------ gravação

    [Teste]
    public void Gravar_PrimeiraVez_SemBak()
    {
        string pasta = Path.Combine(_raiz, "Buzzy");
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        Afirmar.Igual(OrigemDasConfiguracoes.Padroes, arquivo.Ler().Origem);

        ResultadoDaGravacao r = arquivo.Gravar(A);
        Afirmar.Verdadeiro(r.Gravou, r.Erro);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Ausente, r.PrincipalAntes);
        Afirmar.Falso(r.CopiaDeDiagnostico);
        Afirmar.Igual(1, r.Tentativas);
        Afirmar.Igual(Json(A).Length, r.Bytes);
        Afirmar.Nulo(r.Erro);
        MesmosBytes(Json(A), Ler(pasta, Principal), "principal, os bytes do esquema");
        Afirmar.Sequencia([Principal], Nomes(pasta), "sem reserva, sem temporário, sem cópia");

        LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Igual(OrigemDasConfiguracoes.Principal, lida.Origem);
        Afirmar.Igual(EstadoDoArquivo.Valido, lida.Principal);
        Afirmar.Nulo(lida.Reserva);
        Afirmar.Igual<int?>(EsquemaDeConfiguracoes.VersaoAtual, lida.Versao, "gravado pelo esquema atual");
        Afirmar.Igual(0, lida.Avisos);
        Afirmar.Igual(Norm(A), lida.Configuracoes);
    }

    [Teste]
    public void Gravar_TresVezes_BakGuardaAAnterior()
    {
        string pasta = NovaPasta();
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        arquivo.Ler();

        ResultadoDaGravacao r = arquivo.Gravar(A);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Ausente, r.PrincipalAntes);
        r = arquivo.Gravar(B);
        Afirmar.Verdadeiro(r.Gravou, r.Erro);
        Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Valido, r.PrincipalAntes);
        MesmosBytes(Json(B), Ler(pasta, Principal), "principal depois da 2ª");
        MesmosBytes(Json(A), Ler(pasta, Reserva), "reserva depois da 2ª");

        // A troca sobrescreve a reserva que já existia.
        r = arquivo.Gravar(N);
        Afirmar.Verdadeiro(r.Gravou, r.Erro);
        MesmosBytes(Json(N), Ler(pasta, Principal), "principal depois da 3ª");
        MesmosBytes(Json(B), Ler(pasta, Reserva), "reserva depois da 3ª");
        Afirmar.Sequencia(Ordenados(Principal, Reserva), Nomes(pasta));
        Afirmar.Igual(Norm(N), new ArquivoDeConfiguracoes(pasta).Ler().Configuracoes);
    }

    [Teste]
    public void Gravar_FalhaSimuladaEmCadaEtapa_NuncaIlegivel()
    {
        EstadoInicial[] iniciais =
        [
            new("sem arquivos", null, null, PrincipalIlegivel: false),
            new("principal A", Json(A), null, PrincipalIlegivel: false),
            new("principal A e reserva B", Json(A), Json(B), PrincipalIlegivel: false),
            new("principal ilegível e reserva B", Lixo, Json(B), PrincipalIlegivel: true),
        ];
        int casos = 0;
        foreach (EstadoInicial inicial in iniciais)
        {
            foreach (EtapaDaGravacao etapa in Enum.GetValues<EtapaDaGravacao>())
            {
                string pasta = NovaPasta();
                string rotulo = $"{inicial.Nome}, queda depois de {etapa}";
                Escrever(pasta, Principal, inicial.Principal);
                Escrever(pasta, Reserva, inicial.Reserva);

                var comQueda = new ArquivoDeConfiguracoes(pasta, e =>
                {
                    if (e == etapa) throw new QuedaSimulada(e);
                });
                comQueda.Ler();
                Afirmar.Lanca<QuedaSimulada>(() => comQueda.Gravar(N, PoliticaDeGravacao.TentativasImediatas), rotulo);

                // O principal é o anterior, intacto, ou o novo; a reserva só recebe um principal válido; a cópia, só o ilegível.
                bool trocou = etapa == EtapaDaGravacao.Substituido;
                bool havia = inicial.Principal is not null;
                MesmosBytes(trocou ? Json(N) : inicial.Principal, Ler(pasta, Principal), rotulo + ": principal");
                MesmosBytes(trocou && havia && !inicial.PrincipalIlegivel ? inicial.Principal : inicial.Reserva, Ler(pasta, Reserva), rotulo + ": reserva");
                MesmosBytes(trocou && inicial.PrincipalIlegivel ? Lixo : null, Ler(pasta, Copia), rotulo + ": cópia de diagnóstico");
                Afirmar.Igual(!trocou, File.Exists(Path.Combine(pasta, Temporario)), rotulo + ": o temporário sobra só antes da troca");

                // Uma execução nova lê o anterior ou o novo; nunca os padrões quando havia um arquivo bom.
                (OrigemDasConfiguracoes origem, ConfiguracoesSalvas esperadas) =
                    trocou ? (OrigemDasConfiguracoes.Principal, Norm(N))
                    : !havia ? (OrigemDasConfiguracoes.Padroes, ConfiguracoesSalvas.Padrao)
                    : inicial.PrincipalIlegivel ? (OrigemDasConfiguracoes.Reserva, Norm(B))
                    : (OrigemDasConfiguracoes.Principal, Norm(A));
                LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
                Afirmar.Igual(origem, lida.Origem, rotulo + ": origem da leitura seguinte");
                Afirmar.Igual(esperadas, lida.Configuracoes, rotulo + ": configurações da leitura seguinte");

                // E a gravação seguinte funciona e leva o temporário.
                var seguinte = new ArquivoDeConfiguracoes(pasta);
                seguinte.Ler();
                ResultadoDaGravacao r = seguinte.Gravar(N2, PoliticaDeGravacao.TentativasImediatas);
                Afirmar.Verdadeiro(r.Gravou, $"{rotulo}: a gravação seguinte falhou ({r.Erro})");
                Afirmar.Igual(Norm(N2), new ArquivoDeConfiguracoes(pasta).Ler().Configuracoes, rotulo + ": depois da gravação seguinte");
                Afirmar.Falso(File.Exists(Path.Combine(pasta, Temporario)), rotulo + ": temporário depois da gravação seguinte");
                casos++;
            }
        }
        Afirmar.Igual(iniciais.Length * Enum.GetValues<EtapaDaGravacao>().Length, casos);
    }

    [Teste]
    public void Gravar_PrincipalAbertoPorOutro_FalhaSemEstragar()
    {
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        Afirmar.Igual(OrigemDasConfiguracoes.Principal, arquivo.Ler().Origem);

        using (new FileStream(Path.Combine(pasta, Principal), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Ler continua possível: a leitura compartilha tudo e não pede exclusão.
            Afirmar.Igual(Norm(A), new ArquivoDeConfiguracoes(pasta).Ler().Configuracoes, "leitura com o principal aberto por outro");

            // Trocar, não: o outro não compartilha a exclusão.
            var relogio = Stopwatch.StartNew();
            ResultadoDaGravacao r = arquivo.Gravar(N, tentativas: 2);
            relogio.Stop();
            Afirmar.Falso(r.Gravou, "gravou com o principal aberto por outro");
            Afirmar.Igual(2, r.Tentativas);
            Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Valido, r.PrincipalAntes);
            string erro = Afirmar.NaoNulo(r.Erro);
            Afirmar.Falso(erro.Contains(_raiz, StringComparison.OrdinalIgnoreCase) || erro.Contains('\\'), $"o erro não leva caminho: {erro}");
            Afirmar.Verdadeiro(relogio.Elapsed >= PoliticaDeGravacao.PausaEntreTentativasImediatas - TimeSpan.FromMilliseconds(10),
                $"pausa entre as tentativas: {relogio.ElapsedMilliseconds} ms");
            MesmosBytes(Json(A), Ler(pasta, Principal), "principal intacto");
            Afirmar.Sequencia([Principal], Nomes(pasta), "nem reserva, nem cópia, e o temporário da gravação desistida foi apagado");
            Afirmar.Falso(arquivo.GravacaoBloqueada, "uma gravação que falhou não bloqueia as próximas");
        }

        ResultadoDaGravacao depois = arquivo.Gravar(N);
        Afirmar.Verdadeiro(depois.Gravou, $"depois de liberado: {depois.Erro}");
        MesmosBytes(Json(N), Ler(pasta, Principal), "principal novo");
        MesmosBytes(Json(A), Ler(pasta, Reserva), "reserva = o anterior");

        // O temporário preso por outro (um antivírus, por exemplo): a exceção dessa falha traz o caminho, e o erro, não.
        using (new FileStream(Path.Combine(pasta, Temporario), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            ResultadoDaGravacao r = arquivo.Gravar(N2);
            Afirmar.Falso(r.Gravou, "gravou com o temporário preso");
            Afirmar.Nulo(r.PrincipalAntes, "falhou antes de conferir o principal");
            string erro = Afirmar.NaoNulo(r.Erro);
            Afirmar.Falso(erro.Contains(_raiz, StringComparison.OrdinalIgnoreCase) || erro.Contains('\\'), $"o erro não leva caminho: {erro}");
        }
        MesmosBytes(Json(N), Ler(pasta, Principal), "principal intacto depois do temporário preso");
        Afirmar.Verdadeiro(arquivo.Gravar(N2).Gravou, "com o temporário liberado");
        MesmosBytes(Json(N2), Ler(pasta, Principal), "principal depois do temporário liberado");
    }

    [Teste]
    public void Gravar_PrincipalInacessivelNaHora_FalhaSemBloquear()
    {
        // Lido na partida, preso sem compartilhar nada depois: a conferência não o lê, a tentativa falha e nada muda;
        // a gravação não fica bloqueada, porque o que está no disco foi lido.
        string pasta = NovaPasta();
        Escrever(pasta, Principal, Json(A));
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        Afirmar.Falso(arquivo.Ler().GravacaoBloqueada);

        using (new FileStream(Path.Combine(pasta, Principal), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            ResultadoDaGravacao r = arquivo.Gravar(N, tentativas: 2);
            Afirmar.Falso(r.Gravou);
            Afirmar.Igual("principalInacessivel", r.Erro);
            Afirmar.Igual<EstadoDoArquivo?>(EstadoDoArquivo.Inacessivel, r.PrincipalAntes);
            Afirmar.Igual(2, r.Tentativas);
            Afirmar.Falso(arquivo.GravacaoBloqueada);
        }
        MesmosBytes(Json(A), Ler(pasta, Principal), "principal intacto");
        Afirmar.Sequencia([Principal], Nomes(pasta), "sem reserva e sem temporário");

        ResultadoDaGravacao depois = arquivo.Gravar(N);
        Afirmar.Verdadeiro(depois.Gravou, $"depois de liberado: {depois.Erro}");
        MesmosBytes(Json(A), Ler(pasta, Reserva), "reserva = o anterior");
    }

    [Teste]
    public void Gravar_TemporarioQueEhLinkParaOutroArquivo_NaoGravaAtravesDele()
    {
        // Um settings.json.tmp que sobrou como link para um arquivo fora da pasta: físico, que qualquer usuário cria, e
        // simbólico, que exige o modo de desenvolvedor. A gravação apaga o link e cria o temporário do zero: o outro
        // arquivo fica intacto, e o principal é um arquivo comum, com o conteúdo novo.
        byte[] original = "arquivo de fora da pasta, que não pode mudar"u8.ToArray();
        foreach (bool simbolico in new[] { false, true })
        {
            string tipo = simbolico ? "link simbólico" : "link físico";
            string pasta = NovaPasta();
            Directory.CreateDirectory(pasta);
            string deFora = Path.Combine(_raiz, $"de-fora-{_pastas}.txt");
            File.WriteAllBytes(deFora, original);
            string temporario = Path.Combine(pasta, Temporario);
            if (simbolico)
            {
                try
                {
                    File.CreateSymbolicLink(temporario, deFora);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"         {tipo}: sem permissão para criar um ({e.GetType().Name}; o modo de desenvolvedor está desligado); só o link físico foi conferido");
                    continue;
                }
            }
            else if (!CriarLinkFisico(temporario, deFora, 0))
            {
                throw new IOException($"CreateHardLinkW falhou (erro {Marshal.GetLastPInvokeError()}).");
            }
            MesmosBytes(original, Ler(pasta, Temporario), $"{tipo}: o temporário aponta para o arquivo de fora");

            var arquivo = new ArquivoDeConfiguracoes(pasta);
            arquivo.Ler();
            ResultadoDaGravacao r = arquivo.Gravar(A);
            Afirmar.Verdadeiro(r.Gravou, $"{tipo}: {r.Erro}");
            MesmosBytes(original, File.ReadAllBytes(deFora), $"{tipo}: o arquivo de fora");
            MesmosBytes(Json(A), Ler(pasta, Principal), $"{tipo}: principal");
            Afirmar.Igual((FileAttributes)0, File.GetAttributes(Path.Combine(pasta, Principal)) & FileAttributes.ReparsePoint, $"{tipo}: o principal não é link");
            Afirmar.Sequencia([Principal], Nomes(pasta), $"{tipo}: sem temporário");
        }
    }

    // Fase 9, F9-P1 (DEC-040, item 4): o principal, a reserva ou a cópia de diagnóstico como link (físico, que qualquer
    // usuário cria, e simbólico, com o modo de desenvolvedor) para um arquivo fora da pasta. A gravação troca entradas da
    // pasta (File.Move e File.Replace renomeiam): o arquivo de fora nunca muda, nem o conteúdo nem a data.
    [Teste]
    public void Gravar_NomesQueSaoLinkParaFora_NuncaGravamAtravesDeles()
    {
        byte[] ilegivel = "{ isto não é json"u8.ToArray();
        // (o nome que vira link, o que o arquivo de fora contém, o que fica no principal antes da gravação)
        var casos = new (string Nome, byte[] DeFora, byte[]? PrincipalAntes)[]
        {
            (Principal, Json(A), null),
            (Principal, ilegivel, null),
            (Reserva, Json(B), Json(A)),
            (Copia, Json(B), ilegivel),
        };
        int conferidos = 0;
        foreach ((string nome, byte[] deForaOriginal, byte[]? principalAntes) in casos)
        {
            foreach (bool simbolico in new[] { false, true })
            {
                string tipo = $"{nome} como {(simbolico ? "link simbólico" : "link físico")}";
                string pasta = NovaPasta();
                Directory.CreateDirectory(pasta);
                string deFora = Path.Combine(_raiz, $"de-fora-{_pastas}.txt");
                File.WriteAllBytes(deFora, deForaOriginal);
                DateTime dataDeFora = File.GetLastWriteTimeUtc(deFora);
                if (principalAntes is not null) File.WriteAllBytes(Path.Combine(pasta, Principal), principalAntes);
                string link = Path.Combine(pasta, nome);
                if (simbolico)
                {
                    try
                    {
                        File.CreateSymbolicLink(link, deFora);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Console.WriteLine($"         {tipo}: sem permissão para criar ({e.GetType().Name}); só o link físico foi conferido");
                        continue;
                    }
                }
                else if (!CriarLinkFisico(link, deFora, 0))
                {
                    throw new IOException($"CreateHardLinkW falhou (erro {Marshal.GetLastPInvokeError()}).");
                }

                var arquivo = new ArquivoDeConfiguracoes(pasta);
                arquivo.Ler();
                ResultadoDaGravacao r = arquivo.Gravar(N);
                MesmosBytes(deForaOriginal, File.ReadAllBytes(deFora), $"{tipo}: o arquivo de fora (gravou={r.Gravou}, {r.Erro})");
                Afirmar.Igual(dataDeFora, File.GetLastWriteTimeUtc(deFora), $"{tipo}: a data do arquivo de fora");
                if (r.Gravou)
                {
                    MesmosBytes(Json(N), Ler(pasta, Principal), $"{tipo}: o principal novo");
                    Afirmar.Igual((FileAttributes)0, File.GetAttributes(Path.Combine(pasta, Principal)) & FileAttributes.ReparsePoint, $"{tipo}: o principal não é link");
                }
                conferidos++;
            }
        }
        Afirmar.Verdadeiro(conferidos >= casos.Length, $"ao menos os links físicos: {conferidos}");
    }

    /// <summary>CreateHardLinkW: um segundo nome para o mesmo arquivo, sem privilégio. Só neste teste, fora do produto.</summary>
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CriarLinkFisico(string novoNome, string existente, nint atributosDeSeguranca);

    [Teste]
    public void Operacoes_SoTocamOsQuatroArquivos()
    {
        string pasta = Path.Combine(_raiz, "Buzzy");
        var arquivo = new ArquivoDeConfiguracoes(pasta);
        arquivo.Ler();
        arquivo.Gravar(A);
        arquivo.Gravar(B);
        File.WriteAllBytes(Path.Combine(pasta, Principal), Lixo);
        arquivo.Gravar(N);
        var comQueda = new ArquivoDeConfiguracoes(pasta, e =>
        {
            if (e == EtapaDaGravacao.TemporarioEscrito) throw new QuedaSimulada(e);
        });
        Afirmar.Lanca<QuedaSimulada>(() => comQueda.Gravar(N2));
        new ArquivoDeConfiguracoes(pasta).Ler();
        Afirmar.Verdadeiro(arquivo.Gravar(N2).Gravou);

        // A pasta-mãe só tem a do Buzzy; esta, só os quatro nomes e nenhuma subpasta.
        Afirmar.Sequencia(["Buzzy"], Nomes(_raiz));
        Afirmar.Sequencia([], Directory.GetDirectories(pasta), "subpastas");
        foreach (string nome in Nomes(pasta))
            Afirmar.Verdadeiro(ArquivoDeConfiguracoes.Nomes.Contains(nome), $"arquivo inesperado: {nome}");
        Afirmar.Sequencia(Ordenados(Principal, Reserva, Copia), Nomes(pasta));
    }

    // ------------------------------------------------------------------ queda de verdade

    /// <summary>
    /// Um processo filho (este executável, com <c>--gravar-sem-parar</c>) grava sem parar numa pasta temporária; o teste
    /// o encerra à força de 0 a 40 ms depois de ele avisar que está pronto, em 25 rodadas com semente fixa. Encerra só o
    /// processo que ele mesmo abriu. Depois de cada queda, o disco tem um principal válido ou, sem ele, uma reserva
    /// válida; nunca a cópia de diagnóstico; e a leitura nunca cai nos padrões.
    /// </summary>
    [Teste]
    public void MatarDuranteGravacoes_NuncaDeixaIlegivel()
    {
        const int Rodadas = 25;
        var sorteio = new Random(20260930);
        ConfiguracoesSalvas[] possiveis = [.. GravadorSemParar.Conteudos.Select(Norm)];
        int comPrincipal = 0, soReserva = 0, comTemporario = 0;

        for (int i = 0; i < Rodadas; i++)
        {
            string pasta = Directory.CreateDirectory(NovaPasta()).FullName;
            int esperaMs = sorteio.Next(0, 41);
            string rotulo = $"queda {i + 1} de {Rodadas}, {esperaMs} ms depois de pronto";

            string? primeiraLinha = null;
            bool viviaNaQueda = false;
            bool terminou;
            using (Process filho = IniciarGravador(pasta))
            {
                try
                {
                    Task<string?> linha = filho.StandardOutput.ReadLineAsync();
                    if (linha.Wait(TimeSpan.FromSeconds(20)))
                    {
                        primeiraLinha = linha.Result;
                        if (primeiraLinha == GravadorSemParar.Pronto)
                        {
                            Thread.Sleep(esperaMs);
                            viviaNaQueda = !filho.HasExited;
                        }
                    }
                }
                finally
                {
                    // Só o processo que este teste abriu, e só ele.
                    try
                    {
                        if (!filho.HasExited) filho.Kill(entireProcessTree: false);
                    }
                    catch (InvalidOperationException)
                    {
                        // Já tinha saído.
                    }
                    terminou = filho.WaitForExit(10_000);
                }
            }
            Afirmar.Igual(GravadorSemParar.Pronto, primeiraLinha, rotulo + ": primeira linha do gravador");
            Afirmar.Verdadeiro(viviaNaQueda, rotulo + ": o gravador saiu sozinho antes da queda");
            Afirmar.Verdadeiro(terminou, rotulo + ": o gravador não terminou depois de encerrado");

            Afirmar.Falso(File.Exists(Path.Combine(pasta, Copia)), rotulo + ": cópia de diagnóstico (o principal ficou ilegível)");
            if (Ler(pasta, Principal) is { } principal)
            {
                Afirmar.Igual(SituacaoDaLeitura.Valida, EsquemaDeConfiguracoes.Ler(principal).Situacao, rotulo + ": principal");
                comPrincipal++;
            }
            else
            {
                byte[] reserva = Afirmar.NaoNulo(Ler(pasta, Reserva), rotulo + ": sem principal, a reserva existe");
                Afirmar.Igual(SituacaoDaLeitura.Valida, EsquemaDeConfiguracoes.Ler(reserva).Situacao, rotulo + ": reserva");
                soReserva++;
            }
            if (File.Exists(Path.Combine(pasta, Temporario))) comTemporario++;

            LeituraDoArquivo lida = new ArquivoDeConfiguracoes(pasta).Ler();
            Afirmar.Diferente(OrigemDasConfiguracoes.Padroes, lida.Origem, rotulo + ": origem da leitura");
            Afirmar.Verdadeiro(possiveis.Contains(lida.Configuracoes), $"{rotulo}: o lido não é nenhum dos conteúdos gravados: {lida.Configuracoes}");
        }
        Console.WriteLine($"         {Rodadas} quedas: {comPrincipal} com o principal, {soReserva} só com a reserva, {comTemporario} com o temporário");
    }

    /// <summary>Este executável de testes como gravador, sem janela, com a entrada e a saída padrão ligadas ao teste.</summary>
    private static Process IniciarGravador(string pasta)
    {
        var psi = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Buzzy.App.Testes.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add(GravadorSemParar.Opcao);
        psi.ArgumentList.Add(pasta);
        return Process.Start(psi) ?? throw new InvalidOperationException("O gravador não iniciou.");
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Queda simulada pelo gancho de etapas: não é de E/S, então a gravação não tenta de novo e a deixa passar.</summary>
    private sealed class QuedaSimulada(EtapaDaGravacao etapa) : Exception($"queda simulada depois de {etapa}");

    private sealed record EstadoInicial(string Nome, byte[]? Principal, byte[]? Reserva, bool PrincipalIlegivel);

    private static PosicaoDoPersonagem Posicao(string chave, double fracaoX, double fracaoY, RetanguloPx tela, PontoPx ancora)
        => new(chave, fracaoX, fracaoY, ancora) { TelaDoMonitor = tela };

    private static byte[] Json(ConfiguracoesSalvas configuracoes) => EsquemaDeConfiguracoes.Escrever(configuracoes);

    private static ConfiguracoesSalvas Norm(ConfiguracoesSalvas configuracoes) => EsquemaDeConfiguracoes.Normalizar(configuracoes);

    /// <summary>O JSON seguido de espaços até o tamanho dado: continua válido, só maior.</summary>
    private static byte[] Preenchido(byte[] json, int tamanho) => [.. json, .. Enumerable.Repeat((byte)' ', tamanho - json.Length)];

    /// <summary>Uma pasta nova, ainda não criada, dentro da pasta temporária do teste.</summary>
    private string NovaPasta() => Path.Combine(_raiz, $"caso-{_pastas++:000}");

    private static void Escrever(string pasta, string nome, byte[]? dados)
    {
        if (dados is null) return;
        Directory.CreateDirectory(pasta);
        File.WriteAllBytes(Path.Combine(pasta, nome), dados);
    }

    private static byte[]? Ler(string pasta, string nome)
    {
        string caminho = Path.Combine(pasta, nome);
        return File.Exists(caminho) ? File.ReadAllBytes(caminho) : null;
    }

    /// <summary>Nomes na pasta, em ordem ordinal; nenhum se ela não existe.</summary>
    private static string[] Nomes(string pasta)
        => Directory.Exists(pasta) ? [.. Directory.GetFileSystemEntries(pasta).Select(e => Path.GetFileName(e)).Order(StringComparer.Ordinal)] : [];

    private static string[] Ordenados(params string[] nomes) => [.. nomes.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Nome, tamanho, hora da última escrita e (com <paramref name="comConteudo"/>) conteúdo de cada arquivo, para provar
    /// que nada mudou.
    /// </summary>
    private static string Retrato(string pasta, bool comConteudo = true)
    {
        if (!Directory.Exists(pasta)) return "(sem pasta)";
        var sb = new StringBuilder();
        foreach (string caminho in Directory.GetFileSystemEntries(pasta).Order(StringComparer.Ordinal))
        {
            sb.Append(Path.GetFileName(caminho));
            var info = new FileInfo(caminho);
            if (info.Exists)
            {
                sb.Append(' ').Append(info.Length).Append(' ').Append(info.LastWriteTimeUtc.Ticks);
                if (comConteudo) sb.Append(' ').Append(Convert.ToBase64String(File.ReadAllBytes(caminho)));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void MesmosBytes(byte[]? esperado, byte[]? obtido, string oQue)
    {
        if (esperado is null && obtido is null) return;
        if (esperado is null || obtido is null || !esperado.AsSpan().SequenceEqual(obtido))
            Afirmar.Falhar($"{oQue}: esperado {Descrever(esperado)}, obtido {Descrever(obtido)}");
    }

    private static string Descrever(byte[]? dados) => dados is null
        ? "arquivo ausente"
        : $"{dados.Length} bytes \"{Encoding.UTF8.GetString(dados, 0, Math.Min(dados.Length, 60)).ReplaceLineEndings(" ")}\"";
}
