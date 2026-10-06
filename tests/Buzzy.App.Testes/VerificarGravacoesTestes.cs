using System.Diagnostics;
using System.IO;
using System.Text;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 9, passo F9-P7 (DEC-040, item 10): o analisador do registro do Process Monitor (<c>tools\verificar-gravacoes.ps1</c>)
/// separa as gravações do Buzzy (a pasta de dados e o Run\Buzzy), as do Windows em nome do processo e as inesperadas, e
/// só aprova sem inesperadas. A captura real, que exige administrador, fica [MANUAL].
/// </summary>
internal sealed class VerificarGravacoesTestes
{
    private const string PastaLocal = @"C:\Users\Teste\AppData\Local";
    private const string Cabecalho = "\"Time of Day\",\"Process Name\",\"PID\",\"Operation\",\"Path\",\"Result\",\"Detail\"";

    private static string Linha(string operacao, string caminho, string resultado = "SUCCESS", string detalhe = "", string processo = "Buzzy.exe")
        => $"\"10:00\",\"{processo}\",\"1\",\"{operacao}\",\"{caminho}\",\"{resultado}\",\"{detalhe}\"";

    private static (int Codigo, string Saida) Rodar(params string[] linhas)
    {
        string csv = Path.Combine(Path.GetTempPath(), $"buzzy-pm-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, string.Join("\r\n", [Cabecalho, .. linhas]) + "\r\n", new UTF8Encoding(false));
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Caminhos.Raiz, "tools", "verificar-gravacoes.ps1"), "-Csv", csv, "-PastaLocal", PastaLocal })
                psi.ArgumentList.Add(a);
            using Process p = Process.Start(psi)!;
            string saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, saida);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Teste]
    public void SoAsGravacoesEsperadas_Aprova()
    {
        (int codigo, string saida) = Rodar(
            Linha("WriteFile", PastaLocal + @"\Buzzy\settings.json.tmp"),
            Linha("SetRenameInformationFile", PastaLocal + @"\Buzzy\settings.json.tmp"),
            Linha("RegSetValue", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Buzzy", detalhe: "Type: REG_SZ"),
            Linha("RegDeleteValue", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Buzzy"),
            Linha("WriteFile", PastaLocal + @"\D3DSCache\abc\cache.idx"),
            Linha("RegSetValue", @"HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache\x"),
            Linha("WriteFile", @"C:\Users\Teste\Documents\falhou.txt", resultado: "ACCESS DENIED"),
            Linha("ReadFile", @"C:\Windows\win.ini"),
            Linha("CreateFile", @"C:\Windows\Fonts\arial.ttf", detalhe: "Desired Access: Generic Read, Disposition: Open"),
            Linha("WriteFile", @"C:\Users\Teste\Documents\de-outro.txt", processo: "Explorer.EXE"),
            Linha("RegSetValue", @"HKU\S-1-5-21-1111-2222-3333-1001\Software\Microsoft\Windows\CurrentVersion\Run\Buzzy"),
            Linha("RegSetValue", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\Buzzy"),
            Linha("RegSetValue", @"HKU\S-1-5-21-1111-2222-3333-1001_Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache\y"),
            Linha("RegSetInfoKey", @"HKLM\System\CurrentControlSet\Control\Nls", detalhe: "KeySetInformationClass: KeySetHandleTagsInformation, Length: 0"),
            Linha("CreateFile", @"C:\Users\Teste\Documents\so-leitura.txt", detalhe: "Desired Access: Read Attributes, Synchronize, Disposition: Open, OpenResult: Opened"));
        Afirmar.Igual(0, codigo, saida);
        Afirmar.Contem("Do Buzzy (pasta de dados e Run\\Buzzy): 6", saida);
        Afirmar.Contem("Do Windows em nome do processo: 3", saida);
        Afirmar.Contem("INESPERADAS: 0", saida);
    }

    [Teste]
    public void GravacaoForaDaPasta_Reprova()
    {
        foreach (string inesperada in new[]
        {
            Linha("CreateFile", @"C:\Users\Teste\Documents\x.txt", detalhe: "Desired Access: Generic Write, Disposition: Create"),
            Linha("WriteFile", PastaLocal + @"\BuzzyFalso\settings.json"),
            Linha("RegSetValue", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Outro"),
            Linha("RegCreateKey", @"HKCU\Software\Buzzy"),
            Linha("SetRenameInformationFile", @"C:\Users\Teste\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Buzzy.lnk"),
            Linha("RegSetValue", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run\Buzzy"),
            Linha("RegSetValue", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarAl"),
            Linha("WriteFile", @"C:\Users\Teste\Documents\D3DSCache\x.bin"),
            Linha("CreateFile", @"C:\Users\Teste\Documents\novo.txt", detalhe: "Desired Access: Read Attributes, Synchronize, Disposition: OpenIf, OpenResult: Created"),
            Linha("CreateFile", @"C:\Users\Teste\Documents\escrever.txt", detalhe: "Desired Access: Read Attributes, Write Data, Synchronize, Disposition: Open, OpenResult: Opened"),
            Linha("RegSetInfoKey", @"HKCU\Software\Buzzy", detalhe: "KeySetInformationClass: KeyWriteTimeInformation"),
        })
        {
            (int codigo, string saida) = Rodar(Linha("WriteFile", PastaLocal + @"\Buzzy\settings.json.tmp"), inesperada);
            Afirmar.Igual(1, codigo, $"{inesperada}: {saida}");
            Afirmar.Contem("INESPERADAS: 1", saida);
        }
    }

    [Teste]
    public void CsvSemAsColunas_ErroDeUso()
    {
        string csv = Path.Combine(Path.GetTempPath(), $"buzzy-pm-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csv, "a,b\r\n1,2\r\n");
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            foreach (string a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Caminhos.Raiz, "tools", "verificar-gravacoes.ps1"), "-Csv", csv })
                psi.ArgumentList.Add(a);
            using Process p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            Afirmar.Igual(2, p.ExitCode, "sem as colunas do Process Monitor");
        }
        finally
        {
            File.Delete(csv);
        }
    }
}
