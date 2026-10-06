namespace Buzzy.PortaoApis;

/// <summary>
/// Relatório legível do portão. Cada violação sai numa linha no formato de erro que o MSBuild
/// reconhece ("arquivo(linha,coluna): error CODIGO: texto"), para aparecer como erro do build,
/// com arquivo e linha, também no registrador de terminal do dotnet build. As demais linhas
/// evitam esse formato de propósito.
/// </summary>
internal static class Relatorio
{
    public static void Escrever(ResultadoDoPortao resultado, TextWriter saida)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(saida);
        Opcoes opcoes = resultado.Opcoes;

        saida.WriteLine("Portão de APIs proibidas do Buzzy (SECURITY.md 3.2 e 8, item 1)");
        int categorias = ListaProibida.Regras.Select(r => r.Categoria).Distinct().Count();
        saida.WriteLine($"Lista proibida: {ListaProibida.Regras.Count} regras em {categorias} categorias; permissões do apphost: {PermissoesDoApphost.Entradas.Count}; do host de arquivo único: {PermissoesDoHostDeArquivoUnico.Entradas.Count}; usos restritos: {UsosRestritos.Entradas.Count}.");
        saida.WriteLine();

        saida.WriteLine(opcoes.Pacote is null ? $"Binários em {opcoes.Binarios}" : $"Pacote de arquivo único {opcoes.Pacote}");
        int largura = resultado.Binarios.Max(b => Path.GetFileName(b.Caminho).Length);
        foreach (BinarioVerificado b in resultado.Binarios)
            saida.WriteLine($"  {Path.GetFileName(b.Caminho).PadRight(largura)}  {b.Tipo}: {b.Resumo}");
        if (resultado.ResumoDoPacote is not null) saida.WriteLine($"  {resultado.ResumoDoPacote}.");
        if (opcoes.Runtimes.Count > 0)
            saida.WriteLine($"  Do runtime da Microsoft, por procedência (mesmo nome e SHA-256 em {string.Join(", ", opcoes.Runtimes)}): {resultado.DoRuntime}.");
        saida.WriteLine(resultado.NaoVerificados.Count == 0
            ? "  Outros binários na pasta: nenhum."
            : $"  Outros binários na pasta, fora do portão: {string.Join(", ", resultado.NaoVerificados)}.");

        saida.WriteLine($"Código-fonte: {resultado.ArquivosDeFonte} arquivo(s) .cs, sem bin/ e obj/");
        foreach ((string pasta, int arquivos) in resultado.Fontes)
            saida.WriteLine($"  {pasta} ({arquivos})");
        saida.WriteLine(opcoes.Manifesto is null
            ? "Manifesto: não verificado (--manifesto não informado)."
            : $"Manifesto: {opcoes.Manifesto}");
        saida.WriteLine();

        if (resultado.Permitidas.Count > 0)
        {
            saida.WriteLine(opcoes.Pacote is null
                ? "Permitidas no apphost (lançador genérico do SDK, não é código do Buzzy; nunca valem para as DLLs):"
                : "Permitidas no host de arquivo único (o singlefilehost.exe da Microsoft: lançador e runtime; não é código do Buzzy; nunca valem para as DLLs):");
            foreach (Permitida p in resultado.Permitidas)
                saida.WriteLine($"  {Path.GetFileName(p.Arquivo)}: {p.Api} [{p.Categoria.Nome()}] permitida no {Onde(resultado)} - {p.Permissao.Motivo}");
            saida.WriteLine();
        }

        if (resultado.UsosRestritos.Count > 0)
        {
            saida.WriteLine("Usos restritos (decisão aprovada; só no tipo e no arquivo indicados, proibidos no resto do produto):");
            foreach (UsoRestritoVisto u in resultado.UsosRestritos)
                saida.WriteLine($"  {Path.GetFileName(u.Arquivo)}: {u.Api} [{u.Categoria.Nome()}] uso restrito em {u.Onde} ({u.Uso.Arquivo}) - {u.Uso.Motivo}");
            saida.WriteLine();
        }

        if (resultado.Violacoes.Count > 0)
        {
            saida.WriteLine($"Violações ({resultado.Violacoes.Count}):");
            foreach (Violacao v in resultado.Violacoes)
                saida.WriteLine(LinhaDeErro(v));
            saida.WriteLine();
        }

        saida.WriteLine(Resumo(resultado));
    }

    /// <summary>Uma violação no formato de erro do MSBuild.</summary>
    public static string LinhaDeErro(Violacao v)
    {
        ArgumentNullException.ThrowIfNull(v);
        string origem = v.Linha > 0 ? $"{v.Arquivo}({v.Linha},{v.Coluna})" : v.Arquivo;
        return $"{origem}: error {v.Codigo}: [{v.Categoria.Nome()}] {v.Api} - {v.Detalhe}";
    }

    public static string Resumo(ResultadoDoPortao resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        string permitidas = $"{resultado.Permitidas.Count} importação(ões) permitida(s) no {Onde(resultado)}; {resultado.UsosRestritos.Count} uso(s) restrito(s)";
        if (resultado.Violacoes.Count == 0)
            return $"Resumo: APROVADO - nenhuma violação; {permitidas}.";

        int arquivos = resultado.Violacoes.Select(v => v.Arquivo).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        string porCategoria = string.Join(", ", resultado.Violacoes
            .GroupBy(v => v.Categoria)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key.Nome()}: {g.Count()}"));
        return $"Resumo: REPROVADO - {resultado.Violacoes.Count} violação(ões) em {arquivos} arquivo(s) ({porCategoria}); {permitidas}.";
    }

    /// <summary>Onde valem as permissões: o apphost (pasta) ou o host de arquivo único (pacote).</summary>
    private static string Onde(ResultadoDoPortao resultado) => resultado.Opcoes.Pacote is null ? "apphost" : "host de arquivo único";

    /// <summary>Erro de uso ou de leitura, também no formato do MSBuild.</summary>
    public static void EscreverErro(string mensagem, TextWriter erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        erros.WriteLine($"Buzzy.PortaoApis: error {Codigos.ErroDeUso}: {mensagem}");
    }
}
