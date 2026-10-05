using Buzzy.Testes;

namespace Buzzy.Core.Testes;

/// <summary>
/// Ponto de entrada: descobre e executa os testes deste assembly. Na raiz do repositório:
/// <c>dotnet run --project tests/Buzzy.Core.Testes -c Release</c> (aceita as opções de
/// <see cref="Executor"/>, como <c>-- --filtro Posicionador</c>).
/// </summary>
internal static class Programa
{
    [STAThread]
    internal static int Main(string[] args) => Executor.Executar(typeof(Programa).Assembly, args);
}
