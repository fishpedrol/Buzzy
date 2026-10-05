using Buzzy.App.Testes.Integracao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

internal static class Programa
{
    /// <summary>
    /// Executor dos testes; com <c>--gravar-sem-parar PASTA</c>, o gravador que o teste de queda inicia e
    /// encerra à força (<see cref="GravadorSemParar"/>). Com <c>--integracao</c>, os arquivos reais de configuração do
    /// usuário são vistos por fora (<see cref="ArquivosReais"/>, só metadados) antes e depois de todos os testes: se
    /// mudaram, a execução falha, mesmo com todos os testes verdes (revisão de segurança do bloco P6-P9, achado 8).
    /// </summary>
    [STAThread]
    internal static int Main(string[] args)
    {
        if (args is [GravadorSemParar.Opcao, var pasta]) return GravadorSemParar.Executar(pasta);
        if (!args.Contains("--integracao")) return Executor.Executar(typeof(Programa).Assembly, args);

        string antes = ArquivosReais.Foto();
        int codigo = Executor.Executar(typeof(Programa).Assembly, args);
        string depois = ArquivosReais.Foto();
        if (depois == antes)
        {
            Console.WriteLine("== arquivos reais do usuário (%LOCALAPPDATA%\\Buzzy), vistos só por fora: intocados pela integração ==");
            return codigo;
        }
        Console.WriteLine("[FALHOU] os arquivos reais do usuário (%LOCALAPPDATA%\\Buzzy) mudaram durante a integração, vistos só por fora:");
        Console.WriteLine($"         antes:  {antes}");
        Console.WriteLine($"         depois: {depois}");
        return codigo == 0 ? 1 : codigo;
    }
}
