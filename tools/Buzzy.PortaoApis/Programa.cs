using System.Text;

namespace Buzzy.PortaoApis;

internal static class Programa
{
    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return Portao.Executar(args, Console.Out, Console.Error);
    }
}
