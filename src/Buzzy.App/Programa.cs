using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;

// P/Invoke só carrega DLLs do System32. A busca padrão começa pela pasta do exe, então
// uma wtsapi32.dll ou shcore.dll plantada ao lado do Buzzy.exe seria carregada.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Buzzy.App;

// Uso: Buzzy.exe [--diagnostico] [--pausado] [--semente N] [--perfil-de-teste NOME] [--sem-tela-cheia]
//   --diagnostico      log em %LOCALAPPDATA%\Buzzy\diagnostico.log. Sem ele, só as
//                      configurações são gravadas (settings.json, .bak, temporário e no
//                      máximo uma cópia de arquivo ilegível).
//   --pausado          começa sem movimento autônomo.
//   --semente N        semente fixa da agenda, pra reproduzir um comportamento.
//   --perfil-de-teste NOME
//                      dados em %LOCALAPPDATA%\Buzzy\testes\NOME. NOME: 1 a 32 de a-z, 0-9
//                      e hífen, sem hífen no começo, e não pode ser nome reservado do
//                      Windows. Sem nome, nome inválido ou opção mal escrita
//                      (--perfil-de-teste=NOME, outra caixa, /perfil-de-teste) desliga a
//                      persistência.
//   --sem-tela-cheia   não liga o observador do primeiro plano.
internal static class Programa
{
    [STAThread]
    internal static int Main(string[] argumentos)
    {
        // Elevado (terminal de admin ou "Executar como administrador"), avisa e sai antes de
        // tudo: sem log, sem ler opções, sem criar pasta, janela ou objeto nomeado. Assim um
        // processo elevado nunca grava através de um link plantado na pasta do Buzzy.
        // Limite do .exe único: antes deste Main o host do .NET já extraiu as DLLs nativas
        // do WPF em %TEMP%\.net\<nome do exe>, mesmo elevado; daqui pra frente, nada roda elevado.
        if (Environment.IsPrivilegedProcess)
        {
            MessageBox.Show(Textos.AvisoElevado, Textos.DicaDaBandeja, MessageBoxButton.OK, MessageBoxImage.Information);
            return CodigosDeSaida.Elevado;
        }

        if (argumentos.Contains("--diagnostico", StringComparer.Ordinal))
            Diagnostico.Ligar();
        OpcoesDaAplicacao opcoes = LerOpcoes(argumentos);

        Diagnostico.Evento("INICIO",
            ("pid", Environment.ProcessId),
            ("versao", Assembly.GetExecutingAssembly().GetName().Version),
            ("edicao", EdicaoDoBuild.NomeNoLog),
            ("runtime", RuntimeInformation.FrameworkDescription),
            ("so", RuntimeInformation.OSDescription));

        InstanciaUnica instancia;
        try
        {
            instancia = InstanciaUnica.Obter();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
        {
            // Só tipo e código: a mensagem traz o nome dos objetos, que inclui o SID da conta.
            Diagnostico.Evento("INSTANCIA", ("erro", $"{e.GetType().Name} 0x{e.HResult:X8}"));
            Diagnostico.Evento("FIM", ("codigo", CodigosDeSaida.InstanciaUnicaIndisponivel), ("pid", Environment.ProcessId));
            return CodigosDeSaida.InstanciaUnicaIndisponivel;
        }

        using (instancia)
        {
            if (!instancia.EhPrimeira)
            {
                bool entregue = instancia.PedirParaAPrimeiraAparecer(out string? erro);
                Diagnostico.Evento("INSTANCIA", ("papel", "segunda"), ("pid", Environment.ProcessId), ("pedidoEntregue", entregue), ("erro", erro ?? ""));
                int codigoSegunda = entregue ? CodigosDeSaida.Normal : CodigosDeSaida.InstanciaUnicaIndisponivel;
                Diagnostico.Evento("FIM", ("codigo", codigoSegunda), ("pid", Environment.ProcessId));
                return codigoSegunda;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var aplicacao = new Aplicacao(app, instancia, opcoes);
            app.Startup += (_, _) => aplicacao.Iniciar();
            int codigo = app.Run();

            Diagnostico.Evento("FIM", ("codigo", codigo), ("pid", Environment.ProcessId));
            return codigo;
        }
    }

    // Valor ilegível é ignorado e vai pro log.
    internal static OpcoesDaAplicacao LerOpcoes(string[] argumentos)
    {
        bool pausado = argumentos.Contains("--pausado", StringComparer.Ordinal);
        ulong? semente = null;
        int i = Array.IndexOf(argumentos, "--semente");
        if (i >= 0)
        {
            if (i + 1 < argumentos.Length && ulong.TryParse(argumentos[i + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong valor))
                semente = valor;
            else
                Diagnostico.Evento("ARGUMENTO", ("ignorado", "--semente"), ("motivo", "falta um número inteiro sem sinal"));
        }

        // Nome inválido nunca cai na pasta real: desliga a persistência. Grafia parecida com a
        // da opção também desliga, porque quem escreveu queria isolar o Buzzy e errou.
        // O argumento recusado não vai pro log: pode ser um caminho.
        string? perfil = null;
        bool persistenciaDesligada = false;
        if (argumentos.Any(ParecidoComAOpcaoDoPerfil))
        {
            persistenciaDesligada = true;
            Diagnostico.Evento("ARGUMENTO", ("ignorado", OpcaoDoPerfil), ("motivo", "grafia diferente da opção; persistência desligada"));
        }
        int p = Array.IndexOf(argumentos, OpcaoDoPerfil);
        if (p >= 0 && !persistenciaDesligada)
        {
            if (p + 1 < argumentos.Length && PastaDeDados.NomeDePerfilValido(argumentos[p + 1]))
            {
                perfil = argumentos[p + 1];
            }
            else
            {
                persistenciaDesligada = true;
                Diagnostico.Evento("ARGUMENTO", ("ignorado", OpcaoDoPerfil),
                    ("motivo", p + 1 < argumentos.Length ? "nome inválido; persistência desligada" : "falta o nome; persistência desligada"));
            }
        }
        bool semTelaCheia = argumentos.Contains("--sem-tela-cheia", StringComparer.Ordinal);
        return new OpcoesDaAplicacao(pausado, semente, perfil, persistenciaDesligada, semTelaCheia);
    }

    internal const string OpcaoDoPerfil = "--perfil-de-teste";

    // Começa com / ou traços (inclusive travessão) e, só pelas letras, com "perfildeteste"
    // em qualquer caixa, sem ser a grafia exata. Pega --perfil-de-teste=NOME,
    // --Perfil-De-Teste, /perfil-de-teste, -perfil-de-teste, --perfil_de_teste. Sem esse
    // prefixo (o próprio nome do perfil, por exemplo), nunca conta.
    internal static bool ParecidoComAOpcaoDoPerfil(string argumento)
    {
        if (string.Equals(argumento, OpcaoDoPerfil, StringComparison.Ordinal)) return false;
        int inicio = 0;
        while (inicio < argumento.Length && (argumento[inicio] == '/' || char.GetUnicodeCategory(argumento[inicio]) == UnicodeCategory.DashPunctuation))
            inicio++;
        if (inicio == 0) return false;
        string letras = string.Concat(argumento.Skip(inicio).Where(char.IsLetter)).ToLowerInvariant();
        return letras.StartsWith("perfildeteste", StringComparison.Ordinal);
    }
}
