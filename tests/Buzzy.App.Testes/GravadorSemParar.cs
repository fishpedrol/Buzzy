using System.Diagnostics;
using System.IO;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.App.Testes;

/// <summary>
/// Modo <c>--gravar-sem-parar PASTA</c> do executável de testes, usado por
/// <see cref="ArquivoDeConfiguracoesTestes.MatarDuranteGravacoes_NuncaDeixaIlegivel"/>: grava o settings.json da
/// pasta sem parar, alternando <see cref="Conteudos"/> de tamanhos diferentes, até ser encerrado. O teste que o
/// inicia o encerra à força no meio das gravações e confere o que a queda deixou no disco.
///
/// Só aceita uma pasta que já existe dentro da pasta temporária do usuário. Antes de avisar <see cref="Pronto"/>,
/// grava uma vez por inteiro: o principal já existe quando a queda pode acontecer. Sai sozinho quando a entrada
/// padrão fecha (quem o iniciou terminou ou caiu) ou depois de <see cref="TempoMaximo"/>, para nunca ficar
/// gravando sem dono.
/// </summary>
internal static class GravadorSemParar
{
    internal const string Opcao = "--gravar-sem-parar";

    /// <summary>Linha escrita na saída padrão depois da primeira gravação completa.</summary>
    internal const string Pronto = "pronto";

    private static readonly TimeSpan TempoMaximo = TimeSpan.FromSeconds(30);

    /// <summary>O que o gravador alterna: com posição, sem posição e com uma chave longa (arquivo maior).</summary>
    internal static IReadOnlyList<ConfiguracoesSalvas> Conteudos { get; } =
    [
        new(new PosicaoDoPersonagem(@"\\.\DISPLAY1", 0.85, 1, new PontoPx(1632, 1032)) { TelaDoMonitor = new RetanguloPx(0, 0, 1920, 1080) }, Preferencias.Padrao),
        new(null, new Preferencias(NivelDeEnergia.Alta, ModoTelaCheia: false, AtravessarMonitores: false)),
        new(new PosicaoDoPersonagem("mon:" + new string('7', 900), 0.25, 0.5, new PontoPx(-1440, 540)) { TelaDoMonitor = new RetanguloPx(-1920, 0, 0, 1080) },
            new Preferencias(NivelDeEnergia.Baixa, ModoTelaCheia: true, AtravessarMonitores: true)),
    ];

    internal static int Executar(string pasta)
    {
        string completa = Path.GetFullPath(pasta);
        string temporaria = Path.GetFullPath(Path.GetTempPath());
        if (completa.Length <= temporaria.Length || !completa.StartsWith(temporaria, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(completa))
        {
            Console.Error.WriteLine($"{Opcao}: a pasta precisa existir dentro de {temporaria}.");
            return 2;
        }

        var arquivo = new ArquivoDeConfiguracoes(completa);
        ResultadoDaGravacao primeira = arquivo.Gravar(Conteudos[0], PoliticaDeGravacao.TentativasImediatas);
        if (!primeira.Gravou)
        {
            Console.Error.WriteLine($"{Opcao}: a primeira gravação falhou ({primeira.Erro}).");
            return 1;
        }

        var vigia = new Thread(() =>
        {
            try
            {
                Console.In.ReadToEnd();
            }
            catch (IOException)
            {
            }
            Environment.Exit(0);
        })
        { IsBackground = true, Name = "vigia da entrada padrão" };
        vigia.Start();

        Console.Out.WriteLine(Pronto);
        Console.Out.Flush();

        var relogio = Stopwatch.StartNew();
        for (int i = 1; relogio.Elapsed < TempoMaximo; i++)
            arquivo.Gravar(Conteudos[i % Conteudos.Count]);
        return 0;
    }
}
