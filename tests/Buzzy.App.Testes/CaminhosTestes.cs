using System.IO;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O Buzzy.exe testado é o da mesma configuração (Release ou Debug) do executável de testes,
/// deduzida da pasta <c>bin\&lt;configuração&gt;\</c> dele. Nenhum teste aqui abre janela.
/// </summary>
internal sealed class CaminhosTestes
{
    [Teste]
    public void ConfiguracaoEhAPastaLogoAbaixoDeBin()
    {
        Afirmar.Igual("Debug", Caminhos.DeduzirConfiguracao(@"C:\repo\tests\Buzzy.App.Testes\bin\Debug\net10.0-windows\"));
        Afirmar.Igual("Release", Caminhos.DeduzirConfiguracao(@"C:\repo\tests\Buzzy.App.Testes\bin\Release\net10.0-windows"));
        Afirmar.Igual("Release", Caminhos.DeduzirConfiguracao(@"C:\repo\tests\Buzzy.App.Testes\bin\Release\net10.0-windows\win-x64\"));
        Afirmar.Lanca<InvalidOperationException>(() => Caminhos.DeduzirConfiguracao(@"C:\repo\publicado\"), "fora de uma pasta bin");
    }

    [Teste]
    public void OBuzzyProcuradoEhDaMesmaConfiguracaoDesteExecutavel()
    {
        string configuracao = Caminhos.Configuracao;
        Afirmar.Contem($@"\bin\{configuracao}\", AppContext.BaseDirectory, "a configuração deduzida é a da pasta deste executável");
        Afirmar.Igual(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "bin", configuracao, "net10.0-windows", "Buzzy.exe"), Caminhos.ExeDoBuzzy());
    }
}
