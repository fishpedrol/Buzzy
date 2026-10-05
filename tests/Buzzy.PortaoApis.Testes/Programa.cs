using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

internal static class Programa
{
    [STAThread]
    internal static int Main(string[] args) => Executor.Executar(typeof(Programa).Assembly, args);
}
