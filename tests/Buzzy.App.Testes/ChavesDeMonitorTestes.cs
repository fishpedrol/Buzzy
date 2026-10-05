using System.Text.RegularExpressions;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Chave estável do monitor (Fase 5, passo P6; DEC-030), sem hardware: o resumo do caminho do dispositivo, o clone,
/// o cache da última consulta boa, a reserva pelo nome GDI e a regra de nunca repetir uma chave. Os caminhos dos testes
/// são inventados; nenhum caminho real aparece aqui nem nas mensagens.
/// </summary>
internal sealed class ChavesDeMonitorTestes
{
    private const string CaminhoDell = @"\\?\DISPLAY#DEL40F4#5&2a3b4c5d&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string CaminhoGsm = @"\\?\DISPLAY#GSM5B09#4&1C2D3E4F&0&UID4353#{E6F07B5F-EE97-4A90-B076-33F57BF4EAA7}";
    private const string ChaveDell = "mon:6852e0b1cd2318a2";
    private const string ChaveGsm = "mon:0994facb2eabe85c";

    private static readonly RetanguloPx TelaA = new(0, 0, 1920, 1080);
    private static readonly RetanguloPx TelaB = new(-1920, 0, 0, 1080);
    private static readonly RetanguloPx TelaC = new(1920, -360, 4480, 1080);

    private static readonly IReadOnlyDictionary<string, ChaveConhecida> SemCache = new Dictionary<string, ChaveConhecida>();

    [Teste]
    public void DoCaminho_ValoresFixadosNuncaMudamEntreVersoes()
    {
        // As chaves gravadas no settings.json dependem desta função: mudar o resumo faria toda posição salva cair na
        // restauração pela tela ou no principal (DEC-030). Valores conferidos fora do .NET (SHA-256 do caminho em
        // maiúsculas, em UTF-8; os 8 primeiros bytes em hexadecimal minúsculo).
        Afirmar.Igual(ChaveDell, ChavesDeMonitor.DoCaminho(CaminhoDell));
        Afirmar.Igual(ChaveGsm, ChavesDeMonitor.DoCaminho(CaminhoGsm));
    }

    [Teste]
    public void DoCaminho_NaoDiferenciaMaiusculasMasDiferenciaOsMonitores()
    {
        Afirmar.Igual(ChaveDell, ChavesDeMonitor.DoCaminho(CaminhoDell.ToUpperInvariant()), "maiúsculas");
        Afirmar.Igual(ChaveDell, ChavesDeMonitor.DoCaminho(CaminhoDell.ToLowerInvariant()), "minúsculas");
        Afirmar.Diferente(ChavesDeMonitor.DoCaminho(CaminhoDell), ChavesDeMonitor.DoCaminho(CaminhoDell.Replace("UID4352", "UID4353", StringComparison.Ordinal)),
            "outra porta, outra chave");
    }

    [Teste]
    public void DoCaminho_EhUmResumoOpacoDeTamanhoFixo()
    {
        // Sem o caminho, sem o código do modelo e sem espaço, ';', ',', '|' ou '=', que quebrariam a gravação das
        // reproduções, os monitores ocupados e as linhas do log (DEC-030; SECURITY.md 5 e 6).
        foreach (string caminho in new[] { CaminhoDell, CaminhoGsm, "x", new string('a', 500) })
        {
            string chave = ChavesDeMonitor.DoCaminho(caminho);
            Afirmar.Verdadeiro(Regex.IsMatch(chave, "^mon:[0-9a-f]{16}$", RegexOptions.CultureInvariant), $"formato da chave: {chave}");
            Afirmar.Igual(ChavesDeMonitor.PrefixoEstavel, chave[..4]);
        }
        foreach (string pedaco in new[] { "DEL", "40F4", "DISPLAY", "UID", "4352", "E6F07B5F" })
            Afirmar.Falso(ChavesDeMonitor.DoCaminho(CaminhoDell).Contains(pedaco, StringComparison.OrdinalIgnoreCase), $"a chave não contém \"{pedaco}\"");
    }

    [Teste]
    public void DeReserva_FormatoNuncaColideComAChaveEstavel()
    {
        Afirmar.Igual(@"gdi:\\.\DISPLAY2", ChavesDeMonitor.DeReserva(@"\\.\DISPLAY2"));
        Afirmar.Igual(ChavesDeMonitor.PrefixoDeReserva, ChavesDeMonitor.DeReserva(@"\\.\DISPLAY1")[..4]);
        Afirmar.Diferente(ChavesDeMonitor.PrefixoEstavel, ChavesDeMonitor.PrefixoDeReserva);
    }

    [Teste]
    public void Mapear_CloneUsaOMenorCaminhoEmQualquerOrdem()
    {
        // Duplicar (Win+P): uma fonte GDI, dois alvos. A chave vem do menor caminho em maiúsculas (comparação
        // ordinal), para não depender da ordem do Windows; "DEL…" < "GSM…".
        foreach (AlvoAtivo[] alvos in new[]
        {
            new[] { new AlvoAtivo(@"\\.\DISPLAY1", CaminhoDell), new AlvoAtivo(@"\\.\DISPLAY1", CaminhoGsm) },
            new[] { new AlvoAtivo(@"\\.\DISPLAY1", CaminhoGsm), new AlvoAtivo(@"\\.\display1", CaminhoDell) },
        })
        {
            IReadOnlyDictionary<string, string> mapa = ChavesDeMonitor.Mapear(alvos);
            Afirmar.Igual(1, mapa.Count, "uma fonte, uma chave");
            Afirmar.Igual(ChaveDell, mapa[@"\\.\DISPLAY1"], "o menor caminho");
            Afirmar.Igual(ChaveDell, mapa[@"\\.\display1"], "o nome GDI não diferencia maiúsculas");
        }

        IReadOnlyDictionary<string, string> dois = ChavesDeMonitor.Mapear(
            [new AlvoAtivo(@"\\.\DISPLAY1", CaminhoGsm), new AlvoAtivo(@"\\.\DISPLAY2", CaminhoDell)]);
        Afirmar.Igual((ChaveGsm, ChaveDell), (dois[@"\\.\DISPLAY1"], dois[@"\\.\DISPLAY2"]), "estender: uma chave por fonte");
    }

    [Teste]
    public void Mapear_CaminhoOuNomeVazioNaoMapeia()
    {
        IReadOnlyDictionary<string, string> mapa = ChavesDeMonitor.Mapear(
        [
            new AlvoAtivo(@"\\.\DISPLAY1", ""),
            new AlvoAtivo(@"\\.\DISPLAY2", "   "),
            new AlvoAtivo(@"\\.\DISPLAY2", CaminhoGsm),
            new AlvoAtivo("", CaminhoDell),
        ]);
        Afirmar.Falso(mapa.ContainsKey(@"\\.\DISPLAY1"), "sem caminho, sem chave estável");
        Afirmar.Igual(ChaveGsm, mapa[@"\\.\DISPLAY2"], "o alvo com caminho vale, o vazio é ignorado");
        Afirmar.Igual(1, mapa.Count, "nome GDI vazio não entra");
    }

    [Teste]
    public void Atribuir_PeloCaminhoPeloCacheOuPelaReservaNaOrdemDoWindows()
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY2"] = ChaveGsm };
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA) };

        IReadOnlyList<ChaveAtribuida> chaves = ChavesDeMonitor.Atribuir(
            [(@"\\.\DISPLAY1", TelaA), (@"\\.\DISPLAY2", TelaB), (@"\\.\DISPLAY3", TelaC)], mapa, cache);

        Afirmar.Sequencia(
            [
                new ChaveAtribuida(@"\\.\DISPLAY1", ChaveDell, OrigemDaChave.Cache),
                new ChaveAtribuida(@"\\.\DISPLAY2", ChaveGsm, OrigemDaChave.Caminho),
                new ChaveAtribuida(@"\\.\DISPLAY3", @"gdi:\\.\DISPLAY3", OrigemDaChave.Reserva),
            ],
            chaves);
    }

    [Teste]
    public void Atribuir_ConsultaQueFalhou_SoCacheOuReserva()
    {
        // Mapa nulo: a consulta da configuração de vídeo falhou (sessão remota, por exemplo). O cache da última
        // consulta boa segura a chave enquanto o monitor tiver o mesmo nome GDI e a mesma tela.
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA),
            [@"\\.\DISPLAY2"] = new(ChaveGsm, TelaB),
        };
        IReadOnlyList<ChaveAtribuida> chaves = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY2", TelaB), (@"\\.\DISPLAY1", TelaA)], mapa: null, cache);
        Afirmar.Sequencia(
            [new ChaveAtribuida(@"\\.\DISPLAY2", ChaveGsm, OrigemDaChave.Cache), new ChaveAtribuida(@"\\.\DISPLAY1", ChaveDell, OrigemDaChave.Cache)],
            chaves);

        IReadOnlyList<ChaveAtribuida> semNada = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY1", TelaA)], mapa: null, SemCache);
        Afirmar.Igual(new ChaveAtribuida(@"\\.\DISPLAY1", @"gdi:\\.\DISPLAY1", OrigemDaChave.Reserva), semNada.Single());
    }

    [Teste]
    public void Atribuir_CacheValeComOMesmoNomeEOMesmoTamanho_InclusiveTransladado()
    {
        // Uma troca de principal ou um rearranjo translada as telas sem mudar o monitor nem o nome GDI dele: com a consulta
        // negada (sessão bloqueada ou remota), o cache segura a chave. Exigir a mesma tela punha todos os monitores na
        // reserva, e o apelido por retângulo do núcleo levava o personagem ao monitor que passou a ocupar a tela antiga
        // (revisão de correção do bloco P6-P9, achado 1). Com outro tamanho de tela, o nome GDI pode ter ido para outro
        // monitor: a chave antiga não é reaproveitada.
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA) };
        foreach (RetanguloPx mesmoTamanho in new[] { TelaA, TelaB, TelaA.Deslocado(1, 0), TelaA.Deslocado(1920, -360) })
        {
            ChaveAtribuida c = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY1", mesmoTamanho)], mapa: null, cache).Single();
            Afirmar.Igual(new ChaveAtribuida(@"\\.\DISPLAY1", ChaveDell, OrigemDaChave.Cache), c, $"tela {mesmoTamanho}");
        }
        foreach (RetanguloPx outra in new[] { new RetanguloPx(0, 0, 2560, 1440), new RetanguloPx(0, 0, 1080, 1920), new RetanguloPx(0, 0, 1920, 1200) })
        {
            ChaveAtribuida c = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY1", outra)], mapa: null, cache).Single();
            Afirmar.Igual(new ChaveAtribuida(@"\\.\DISPLAY1", @"gdi:\\.\DISPLAY1", OrigemDaChave.Reserva), c, $"tela {outra}");
        }
        Afirmar.Igual(OrigemDaChave.Reserva, ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY2", TelaA)], mapa: null, cache).Single().Origem, "outro nome GDI");
    }

    [Teste]
    public void Atribuir_TrocaDePrincipalComAConsultaNegada_CadaMonitorFicaComAChaveDele()
    {
        // S2 com a consulta negada: o DISPLAY2, à esquerda, vira o principal; a origem vai para ele e as duas telas andam
        // 1920 px para a direita. Cada nome GDI continua com a chave do próprio caminho, lida na última consulta boa.
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA),
            [@"\\.\DISPLAY2"] = new(ChaveGsm, TelaB),
        };
        IReadOnlyList<ChaveAtribuida> chaves = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY1", TelaA.Deslocado(1920, 0)), (@"\\.\DISPLAY2", TelaA)], mapa: null, cache);
        Afirmar.Sequencia(
            [new ChaveAtribuida(@"\\.\DISPLAY1", ChaveDell, OrigemDaChave.Cache), new ChaveAtribuida(@"\\.\DISPLAY2", ChaveGsm, OrigemDaChave.Cache)],
            chaves);
    }

    [Teste]
    public void Atribuir_NuncaRepeteChave()
    {
        // Dois nomes no cache com a mesma chave (cache fabricado): o segundo, na ordem do Windows, vai para a reserva.
        var repetido = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA),
            [@"\\.\DISPLAY2"] = new(ChaveDell, TelaB),
        };
        IReadOnlyList<ChaveAtribuida> chaves = ChavesDeMonitor.Atribuir([(@"\\.\DISPLAY1", TelaA), (@"\\.\DISPLAY2", TelaB)], mapa: null, repetido);
        Afirmar.Sequencia([ChaveDell, @"gdi:\\.\DISPLAY2"], chaves.Select(c => c.Chave));
        Afirmar.Igual(OrigemDaChave.Reserva, chaves[1].Origem);

        // A chave lida do caminho é a da consulta atual e vale mais que uma do cache, em qualquer ordem: o monitor
        // do cache que a repetiria vai para a reserva.
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY2"] = ChaveDell };
        var cache = new Dictionary<string, ChaveConhecida>(StringComparer.OrdinalIgnoreCase) { [@"\\.\DISPLAY1"] = new(ChaveDell, TelaA) };
        foreach ((string, RetanguloPx)[] ordem in new[]
        {
            new[] { (@"\\.\DISPLAY1", TelaA), (@"\\.\DISPLAY2", TelaB) },
            new[] { (@"\\.\DISPLAY2", TelaB), (@"\\.\DISPLAY1", TelaA) },
        })
        {
            Dictionary<string, ChaveAtribuida> porNome = ChavesDeMonitor.Atribuir(ordem, mapa, cache).ToDictionary(c => c.NomeGdi);
            Afirmar.Igual(new ChaveAtribuida(@"\\.\DISPLAY2", ChaveDell, OrigemDaChave.Caminho), porNome[@"\\.\DISPLAY2"]);
            Afirmar.Igual(new ChaveAtribuida(@"\\.\DISPLAY1", @"gdi:\\.\DISPLAY1", OrigemDaChave.Reserva), porNome[@"\\.\DISPLAY1"]);
        }
    }

    [Teste]
    public void NovoCache_GuardaAsChavesDoCaminhoEAsQueContinuaramPeloCache()
    {
        // Uma consulta boa que não traz o caminho de um monitor ainda enumerado (o alvo marcado como indisponível, ou o
        // nome que não pôde ser lido) não apaga a chave dele: ela continuou pelo cache e continua lá, com a tela de agora.
        // A reserva nunca entra.
        IReadOnlyList<(string NomeGdi, RetanguloPx Tela)> enumerados = [(@"\\.\DISPLAY1", TelaA), (@"\\.\DISPLAY2", TelaB), (@"\\.\DISPLAY3", TelaC)];
        IReadOnlyList<ChaveAtribuida> chaves =
        [
            new(@"\\.\DISPLAY1", ChaveDell, OrigemDaChave.Caminho),
            new(@"\\.\DISPLAY2", ChaveGsm, OrigemDaChave.Cache),
            new(@"\\.\DISPLAY3", @"gdi:\\.\DISPLAY3", OrigemDaChave.Reserva),
        ];
        IReadOnlyDictionary<string, ChaveConhecida> cache = ChavesDeMonitor.NovoCache(enumerados, chaves);
        Afirmar.Igual(2, cache.Count, "a do caminho e a que continuou pelo cache; a reserva, não");
        Afirmar.Igual(new ChaveConhecida(ChaveDell, TelaA), cache[@"\\.\display1"], "com a tela da época; o nome não diferencia maiúsculas");
        Afirmar.Igual(new ChaveConhecida(ChaveGsm, TelaB), cache[@"\\.\DISPLAY2"], "a do cache, com a tela de agora");
        Afirmar.Falso(cache.ContainsKey(@"\\.\DISPLAY3"), "a reserva não entra");
    }

    [Teste]
    public void AlvoAtivo_TextoNuncaMostraOCaminhoDoDispositivo()
    {
        // O caminho identifica o hardware (SECURITY.md 6): se um alvo for parar num texto por engano, só o nome GDI aparece.
        string texto = new AlvoAtivo(@"\\.\DISPLAY1", CaminhoDell).ToString();
        Afirmar.Contem(@"\\.\DISPLAY1", texto);
        foreach (string pedaco in new[] { "DEL40F4", "UID4352", "DISPLAY#" })
            Afirmar.Falso(texto.Contains(pedaco, StringComparison.OrdinalIgnoreCase), $"o texto do alvo não mostra \"{pedaco}\"");
    }
}
