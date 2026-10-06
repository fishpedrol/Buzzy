using System.IO;
using Buzzy.App.Composicao;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 9, passo F9-P3 (DEC-040, item 7): as regras do início com o Windows com textos e bytes aleatórios, com semente
/// fixa: o que vem do registro (o valor Run e a marca do StartupApproved) nunca faz as regras lançarem, "desta cópia" só
/// vale para um caminho absoluto, e o estado e a ação ficam sempre no domínio.
/// </summary>
internal sealed class RegrasDoInicioAleatoriasTestes
{
    private const string Aqui = @"C:\Programas\Buzzy\Buzzy.exe";

    [Teste]
    public void TextosEBytesAleatorios_NuncaLancam_EDestaCopiaSoAbsoluto()
    {
        var r = new Random(20261005);
        string[] pedacos = ["\"", "\\", "/", ":", "C:", "\\\\?\\", "\\\\servidor\\", ".", "..", " ", "\0", "\t", "Buzzy.exe", "BUZZY.EXE", "Programas", "%TEMP%", "~1", "\u00e9", "\ud800", "--perfil-de-teste", new string('a', 300),
            @"\Programas\Buzzy\Buzzy.exe", @"C:Programas\Buzzy\Buzzy.exe", @"Buzzy\Buzzy.exe", @".\Buzzy.exe", @"Programas\Buzzy\Buzzy.exe"];
        int destaCopia = 0;
        for (int caso = 0; caso < 10_000; caso++)
        {
            string valor = r.Next(5) == 0 ? "\"" + Aqui + "\"" : string.Concat(Enumerable.Range(0, r.Next(12)).Select(_ => pedacos[r.Next(pedacos.Length)]));
            if (r.Next(3) == 0) valor = valor.ToUpperInvariant();
            byte[]? aprovacao = r.Next(4) == 0 ? null : [.. Enumerable.Range(0, r.Next(17)).Select(_ => (byte)r.Next(256))];
            string? caminho = r.Next(10) == 0 ? valor : Aqui;
            try
            {
                bool desta = RegrasDoInicio.DestaCopia(valor, Aqui);
                if (desta)
                {
                    destaCopia++;
                    string semAspas = valor.Trim().Trim('"');
                    Afirmar.Verdadeiro(Path.IsPathFullyQualified(semAspas), $"caso {caso}: desta cópia só absoluto ({valor})");
                }
                Afirmar.Verdadeiro(Enum.IsDefined(RegrasDoInicio.Aprovacao(aprovacao)), $"caso {caso}: aprovação");
                EstadoDoInicio estado = RegrasDoInicio.Avaliar(r.Next(5) > 0, r.Next(4) == 0 ? null : valor, aprovacao, caminho);
                Afirmar.Verdadeiro(Enum.IsDefined(estado), $"caso {caso}: estado");
                if (!RegrasDoInicio.CaminhoValido(caminho)) Afirmar.Igual(EstadoDoInicio.Indisponivel, estado, $"caso {caso}: caminho inválido, indisponível");
                _ = RegrasDoInicio.AcaoDoPedido(estado, r.Next(2) == 0);
                if (RegrasDoInicio.CaminhoValido(valor)) Afirmar.Verdadeiro(RegrasDoInicio.DadoDoRun(valor).StartsWith('"'), $"caso {caso}: dado entre aspas");
            }
            catch (Exception e) when (e is not FalhaDeAfirmacao)
            {
                throw new InvalidOperationException($"caso {caso} ({valor.Length} caracteres): {e.GetType().Name}: {e.Message}", e);
            }
        }
        Afirmar.Verdadeiro(destaCopia > 1000, $"casos desta cópia: {destaCopia}");
    }
}
