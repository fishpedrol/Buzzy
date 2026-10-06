using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;

// DEC-040, item 3: o P/Invoke só carrega DLLs do System32. Uma wtsapi32.dll ou shcore.dll plantada ao lado do Buzzy.exe
// (a busca padrão começa pela pasta do assembly) não é carregada.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Buzzy.App;

/// <summary>
/// Ponto de entrada do Buzzy.
///
/// Uso: <c>Buzzy.exe [--diagnostico] [--pausado] [--semente N] [--perfil-de-teste NOME] [--sem-tela-cheia]</c>.
/// <list type="bullet">
/// <item><c>--diagnostico</c> liga o log em <c>%LOCALAPPDATA%\Buzzy\diagnostico.log</c>; sem ele, o
/// Buzzy só grava as configurações (<c>settings.json</c>, com a reserva <c>settings.json.bak</c>, o temporário
/// de cada gravação e, no máximo, uma cópia de um arquivo ilegível), na mesma pasta (Fase 5, passo P7).</item>
/// <item><c>--pausado</c> começa com o movimento autônomo pausado (o mesmo que "Pausar movimento" no
/// menu); as verificações de tela usam para ter o personagem parado no lugar inicial.</item>
/// <item><c>--semente N</c> fixa a semente da agenda autônoma, para reproduzir um comportamento.</item>
/// <item><c>--perfil-de-teste NOME</c> isola os dados do Buzzy em <c>%LOCALAPPDATA%\Buzzy\testes\NOME</c>
/// (Fase 5): os testes e as ferramentas que abrem o Buzzy usam, para nunca tocar nas configurações reais
/// do usuário. NOME tem de 1 a 32 caracteres entre a–z, 0–9 e hífen (sem começar por hífen) e não pode
/// ser um nome reservado do Windows; sem nome, com um inválido ou com a opção escrita de outro jeito
/// (<c>--perfil-de-teste=NOME</c>, outra caixa, <c>/perfil-de-teste</c>), a persistência fica desligada
/// nesta execução.</item>
/// <item><c>--sem-tela-cheia</c> não liga o observador da janela em primeiro plano (DEC-034): os testes e a
/// verificação de tela, que conferem o lugar inicial, não dependem do que estiver em tela cheia na máquina.</item>
/// </list>
/// </summary>
internal static class Programa
{
    [STAThread]
    internal static int Main(string[] argumentos)
    {
        // SECURITY.md 8, item 5, e DEC-040, item 2: o Buzzy não usa privilégio de administrador. Iniciado elevado (por um
        // terminal de administrador ou "Executar como administrador"), ele avisa e sai antes de tudo: sem log, sem ler as
        // opções, sem criar pasta, janela ou objeto nomeado. Um processo elevado nunca grava através de um link plantado
        // por um processo comum na pasta do Buzzy. Limite do .exe único (F9-P10; DEC-042, item 9; SECURITY.md 9): antes
        // deste Main, o host do .NET já extraiu (ou reaproveitou) as DLLs nativas do WPF em %TEMP%\.net\<nome do exe>,
        // também quando iniciado elevado; daqui em diante, nada do Buzzy roda elevado.
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
            // Só o tipo e o código: a mensagem traz o nome dos objetos da instância única, com o SID da conta (SECURITY.md 6).
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

    /// <summary>Opções da linha de comando; um valor ilegível é ignorado e registrado no diagnóstico.</summary>
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

        // Perfil de teste: um nome inválido nunca cai na pasta real do usuário, desliga a persistência. Uma
        // grafia parecida com a da opção também desliga: quem a escreveu quis isolar o Buzzy e errou a opção.
        // O argumento recusado não vai para o log: pode ser um caminho.
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

    /// <summary>A opção do perfil de teste, exatamente como tem de ser escrita.</summary>
    internal const string OpcaoDoPerfil = "--perfil-de-teste";

    /// <summary>
    /// Se o argumento é outra grafia da opção do perfil de teste: começa por <c>/</c> ou por traços (um, dois,
    /// travessão) e, contando só as letras, começa por "perfildeteste" em qualquer caixa, mas não é exatamente
    /// <see cref="OpcaoDoPerfil"/>. Pega <c>--perfil-de-teste=NOME</c>, <c>--Perfil-De-Teste</c>,
    /// <c>/perfil-de-teste</c>, <c>-perfil-de-teste</c> e <c>--perfil_de_teste</c>. Um argumento sem esse
    /// prefixo, como o próprio nome do perfil, nunca conta.
    /// </summary>
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
