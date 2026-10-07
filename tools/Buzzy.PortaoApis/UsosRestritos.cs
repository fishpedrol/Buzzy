namespace Buzzy.PortaoApis;

// Funcao é o nome exato no binário (RegSetValueExW); na fonte vale a família toda. Tipo pode ter
// aninhados (Tipo+Nativo). Arquivo é relativo à raiz do repositório.
internal sealed record UsoRestrito(string Modulo, string Funcao, string Assembly, string Tipo, string Arquivo, string Motivo);

// Api como nos metadados (user32.dll!SetWinEventHook); Onde é o método que declara o P/Invoke.
internal sealed record UsoRestritoVisto(string Arquivo, string Api, string Onde, Categoria Categoria, UsoRestrito Uso);

// Funções proibidas que um único tipo do Buzzy pode usar: o observador de tela cheia (assina a
// troca de janela em primeiro plano e a geometria dela, lê só o retângulo) e o "iniciar com o
// Windows" (grava/apaga só o valor Buzzy da chave Run, a pedido do usuário).
//
// No binário vale só pro P/Invoke exato, declarado no tipo (ou aninhado) e assembly indicados.
// Na fonte vale só no arquivo indicado. Fora disso reprova como sempre. Qualquer uso novo
// precisa de entrada aqui com o motivo.
internal static class UsosRestritos
{
    private const string Observador = "Buzzy.App.Plataforma.ObservadorDeTelaCheia";
    private const string ArquivoDoObservador = @"src\Buzzy.App\Plataforma\ObservadorDeTelaCheia.cs";
    private const string Inicio = "Buzzy.App.Plataforma.InicioComOWindows";
    private const string ArquivoDoInicio = @"src\Buzzy.App\Plataforma\InicioComOWindows.cs";

    public static readonly IReadOnlyList<UsoRestrito> Entradas =
    [
        new("user32", "SetWinEventHook", "Buzzy", Observador, ArquivoDoObservador,
            "DEC-013, DEC-034 e DEC-037: o observador de tela cheia e da curiosidade assina, fora do processo e sem o próprio processo, a troca da janela em primeiro plano (o sistema todo) e a mudança de geometria só na thread dela; nenhum evento de input"),
        new("user32", "GetForegroundWindow", "Buzzy", Observador, ArquivoDoObservador,
            "DEC-013, DEC-034 e DEC-037: a janela em primeiro plano, para ler só o retângulo dela e saber quais monitores ela cobre, o monitor do foco e, a pedido, o vão horizontal dela; o identificador não é guardado nem vai ao log"),
        new("user32", "GetWindowThreadProcessId", "Buzzy", Observador, ArquivoDoObservador,
            "DEC-034: só a thread da janela em primeiro plano, para restringir a assinatura de geometria a ela; o processo nunca é pedido (o ponteiro dele vai nulo)"),
        new("advapi32", "RegSetValueExW", "Buzzy", Inicio, ArquivoDoInicio,
            "Q-04 e DEC-038: grava só o valor Buzzy (REG_SZ, o caminho do Buzzy.exe entre aspas) na chave Run do usuário, e só pelo pedido explícito dele nas configurações; nunca na partida, na migração ou num conserto"),
        new("advapi32", "RegDeleteValueW", "Buzzy", Inicio, ArquivoDoInicio,
            "Q-04 e DEC-038: apaga só o valor Buzzy da chave Run do usuário, quando ele aponta para esta cópia, e só pelo pedido explícito dele"),
    ];

    public static UsoRestrito? NoBinario(string assembly, string modulo, string funcao, string tipoQueDeclara)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(modulo);
        ArgumentNullException.ThrowIfNull(funcao);
        ArgumentNullException.ThrowIfNull(tipoQueDeclara);
        string moduloNormalizado = ListaProibida.NormalizarModulo(modulo);
        return Entradas.FirstOrDefault(u =>
            string.Equals(u.Modulo, moduloNormalizado, StringComparison.Ordinal)
            && string.Equals(u.Funcao, funcao, StringComparison.Ordinal)
            && string.Equals(u.Assembly, assembly, StringComparison.Ordinal)
            && (string.Equals(u.Tipo, tipoQueDeclara, StringComparison.Ordinal)
                || tipoQueDeclara.StartsWith(u.Tipo + "+", StringComparison.Ordinal)));
    }

    public static UsoRestrito? NaFonte(string arquivo, Regra regra)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(regra);
        if (regra.Tipo != TipoDeRegra.FuncaoNativa) return null;
        string caminho = arquivo.Replace('/', '\\');
        return Entradas.FirstOrDefault(u =>
            ReferenceEquals(ListaProibida.ProcurarNativa(u.Modulo, u.Funcao), regra)
            && caminho.EndsWith(@"\" + u.Arquivo, StringComparison.OrdinalIgnoreCase));
    }
}
