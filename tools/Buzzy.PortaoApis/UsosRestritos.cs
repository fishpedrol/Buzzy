namespace Buzzy.PortaoApis;

/// <summary>
/// Uso de uma função da lista proibida pelo próprio Buzzy, com decisão aprovada e restrito a um lugar só.
/// </summary>
/// <param name="Modulo">Módulo normalizado (<see cref="ListaProibida.NormalizarModulo"/>).</param>
/// <param name="Funcao">
/// Nome exato da entrada no binário (com o sufixo A ou W, quando a função tem variantes: <c>RegSetValueExW</c>); na fonte,
/// vale a família inteira da função, a regra que a lista proibida acha por esse nome (DEC-038, item 11).
/// </param>
/// <param name="Assembly">O assembly que pode declarar o P/Invoke, sem extensão.</param>
/// <param name="Tipo">O tipo que declara o P/Invoke: ele ou um tipo aninhado nele (<c>Tipo+Nativo</c>).</param>
/// <param name="Arquivo">O único arquivo de fonte que pode citar a função, relativo à raiz do repositório.</param>
/// <param name="Motivo">A decisão e os limites; aparece no relatório.</param>
internal sealed record UsoRestrito(string Modulo, string Funcao, string Assembly, string Tipo, string Arquivo, string Motivo);

/// <summary>Um P/Invoke que coincide com a lista proibida e está na lista de usos restritos.</summary>
/// <param name="Arquivo">O binário.</param>
/// <param name="Api">Como aparece nos metadados: <c>user32.dll!SetWinEventHook</c>.</param>
/// <param name="Onde">O método que declara o P/Invoke.</param>
internal sealed record UsoRestritoVisto(string Arquivo, string Api, string Onde, Categoria Categoria, UsoRestrito Uso);

/// <summary>
/// Os usos restritos (SECURITY.md 3.1 e 8, item 1): funções que continuam na lista proibida, para o resto do produto, e que
/// um único tipo do Buzzy pode usar, com decisão aprovada: o observador de tela cheia (DEC-013, DEC-034 e DEC-037), que
/// assina, fora do processo, a troca da janela em primeiro plano e a mudança de geometria dela, e lê só o retângulo dela; e o
/// adaptador do início com o Windows (Q-04, DEC-038), que grava e apaga só o valor <c>Buzzy</c> da chave Run do usuário, e
/// só pelo pedido explícito dele.
///
/// Regras de uso:
/// - nos binários, vale só para o P/Invoke do módulo e da função exatos, declarado no tipo indicado, ou num tipo aninhado
///   nele, do assembly indicado; declarado em outro tipo ou outro assembly, o P/Invoke reprova;
/// - na fonte, vale só no arquivo indicado (o caminho completo termina nele, sem diferenciar maiúsculas nem o tipo da
///   barra), para a família da função (a mesma regra da lista proibida); citada em outro arquivo, reprova, como sempre;
/// - cada P/Invoke permitido aparece no relatório como "uso restrito";
/// - outra função, outro tipo ou outro arquivo exigem decisão nova e uma entrada aqui, com o motivo.
/// </summary>
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

    /// <summary>O uso restrito deste P/Invoke, ou nulo: módulo e função exatos, no assembly e no tipo indicados.</summary>
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

    /// <summary>O uso restrito que permite citar a função desta regra neste arquivo de fonte, ou nulo.</summary>
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
