namespace Buzzy.PortaoApis;

// Procura os PadroesNaFonte (palavras inteiras ou com ponto, como Process.Start), sem caixa.
// Tira os comentários antes mas lê as strings, então GetProcAddress("SendInput") acusa as duas.
// Não pega nome montado ("Send" + "Input"), alias de using nem escape Unicode; o binário cobre.
internal static class VerificadorDeFonte
{
    private static readonly string[] PastasIgnoradas = ["bin", "obj"];

    // Em "\tSendInput" a palavra lida seria "tSendInput"; tenta de novo sem a letra do escape.
    private const string LetrasDeEscape = "abefnrtv0";

    private static readonly Dictionary<string, Regra> Palavras = IndexarPalavras();
    private static readonly Dictionary<string, List<Sequencia>> Sequencias = IndexarSequencias();

    private sealed record Sequencia(string[] Partes, Regra Regra);

    private enum TipoDeToken
    {
        Palavra,
        Ponto,
        Outro,
    }

    private readonly record struct Token(TipoDeToken Tipo, int Inicio, int Fim);

    // Não desce em bin, obj nem em pontos de junção.
    public static IReadOnlyList<string> ListarArquivos(string pasta)
    {
        ArgumentNullException.ThrowIfNull(pasta);
        var arquivos = new List<string>();
        var pendentes = new Stack<string>();
        pendentes.Push(Path.GetFullPath(pasta));
        while (pendentes.Count > 0)
        {
            string atual = pendentes.Pop();
            arquivos.AddRange(Directory.EnumerateFiles(atual)
                .Where(f => string.Equals(Path.GetExtension(f), ".cs", StringComparison.OrdinalIgnoreCase)));
            foreach (string subpasta in Directory.EnumerateDirectories(atual))
            {
                if (PastasIgnoradas.Contains(Path.GetFileName(subpasta), StringComparer.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(subpasta) & FileAttributes.ReparsePoint) != 0) continue;
                pendentes.Push(subpasta);
            }
        }
        arquivos.Sort(StringComparer.OrdinalIgnoreCase);
        return arquivos;
    }

    public static IReadOnlyList<Violacao> VerificarArquivo(string arquivo)
        => VerificarTexto(arquivo, File.ReadAllText(arquivo));

    public static IReadOnlyList<Violacao> VerificarTexto(string arquivo, string fonte)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(fonte);

        string texto = RemovedorDeComentarios.Remover(fonte);
        List<Token> tokens = Tokenizar(texto);
        int[] inicioDasLinhas = InicioDasLinhas(texto);

        var violacoes = new List<Violacao>();
        var vistas = new HashSet<(int Posicao, Regra Regra)>();
        void Acusar(int posicao, string encontrado, Regra regra)
        {
            // O arquivo de um uso restrito pode citar a função dele; as outras regras valem igual.
            if (UsosRestritos.NaFonte(arquivo, regra) is not null) return;
            if (!vistas.Add((posicao, regra))) return;
            (int linha, int coluna) = Posicao(inicioDasLinhas, posicao);
            violacoes.Add(new Violacao(arquivo, linha, coluna, Codigos.CodigoFonte, regra.Categoria, encontrado,
                $"identificador proibido no código-fonte ({regra.Descricao}): {regra.Motivo}", regra));
        }

        for (int k = 0; k < tokens.Count; k++)
        {
            Token token = tokens[k];
            if (token.Tipo != TipoDeToken.Palavra) continue;

            int inicio = token.Inicio;
            bool depoisDeEscape = inicio > 0 && texto[inicio - 1] == '\\'
                && token.Fim - inicio > 1 && LetrasDeEscape.Contains(texto[inicio], StringComparison.Ordinal);
            int tentativas = depoisDeEscape ? 2 : 1;

            for (int tentativa = 0; tentativa < tentativas; tentativa++, inicio++)
            {
                string palavra = texto[inicio..token.Fim];
                string chave = palavra.ToLowerInvariant();
                if (Palavras.TryGetValue(chave, out Regra? regra)) Acusar(inicio, palavra, regra);
                if (!Sequencias.TryGetValue(chave, out List<Sequencia>? candidatas)) continue;
                foreach (Sequencia sequencia in candidatas)
                {
                    if (CasaSequencia(tokens, texto, k, sequencia.Partes, out string restante))
                        Acusar(inicio, palavra + restante, sequencia.Regra);
                }
            }
        }

        return violacoes;
    }

    // Depois de tokens[k] tem que vir ".", parte 1, ".", parte 2... restante é o que casou depois da primeira.
    private static bool CasaSequencia(List<Token> tokens, string texto, int k, string[] partes, out string restante)
    {
        restante = "";
        int t = k;
        for (int p = 1; p < partes.Length; p++)
        {
            if (t + 2 >= tokens.Count) return false;
            if (tokens[t + 1].Tipo != TipoDeToken.Ponto) return false;
            Token palavra = tokens[t + 2];
            if (palavra.Tipo != TipoDeToken.Palavra) return false;
            string lida = texto[palavra.Inicio..palavra.Fim];
            if (!string.Equals(lida, partes[p], StringComparison.OrdinalIgnoreCase)) return false;
            restante += "." + lida;
            t += 2;
        }
        return true;
    }

    private static List<Token> Tokenizar(string texto)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < texto.Length)
        {
            char c = texto[i];
            if (EhCaractereDePalavra(c))
            {
                int inicio = i;
                while (i < texto.Length && EhCaractereDePalavra(texto[i])) i++;
                tokens.Add(new Token(TipoDeToken.Palavra, inicio, i));
            }
            else if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else
            {
                tokens.Add(new Token(c == '.' ? TipoDeToken.Ponto : TipoDeToken.Outro, i, i + 1));
                i++;
            }
        }
        return tokens;
    }

    private static bool EhCaractereDePalavra(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int[] InicioDasLinhas(string texto)
    {
        var inicios = new List<int> { 0 };
        for (int i = 0; i < texto.Length; i++)
        {
            if (texto[i] == '\n') inicios.Add(i + 1);
        }
        return [.. inicios];
    }

    private static (int Linha, int Coluna) Posicao(int[] inicioDasLinhas, int deslocamento)
    {
        int indice = Array.BinarySearch(inicioDasLinhas, deslocamento);
        if (indice < 0) indice = ~indice - 1;
        return (indice + 1, deslocamento - inicioDasLinhas[indice] + 1);
    }

    private static Dictionary<string, Regra> IndexarPalavras()
    {
        // Palavra repetida (Clipboard do WPF e do WinForms) fica com a primeira; a categoria é a mesma.
        var palavras = new Dictionary<string, Regra>(StringComparer.Ordinal);
        foreach (Regra r in ListaProibida.Regras)
        {
            foreach (string padrao in r.PadroesNaFonte.Where(p => !p.Contains('.', StringComparison.Ordinal)))
                palavras.TryAdd(padrao.ToLowerInvariant(), r);
        }
        return palavras;
    }

    private static Dictionary<string, List<Sequencia>> IndexarSequencias()
    {
        var sequencias = new Dictionary<string, List<Sequencia>>(StringComparer.Ordinal);
        foreach (Regra r in ListaProibida.Regras)
        {
            foreach (string padrao in r.PadroesNaFonte.Where(p => p.Contains('.', StringComparison.Ordinal)))
            {
                string[] partes = padrao.ToLowerInvariant().Split('.');
                if (!sequencias.TryGetValue(partes[0], out List<Sequencia>? lista))
                {
                    lista = [];
                    sequencias[partes[0]] = lista;
                }
                lista.Add(new Sequencia(partes, r));
            }
        }
        return sequencias;
    }
}
