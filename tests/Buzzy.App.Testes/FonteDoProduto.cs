using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Buzzy.App.Testes;

/// <summary>
/// A fonte do produto para as cercas de texto (Fase 9; revisão adversarial da DEC-040): os arquivos .cs de <c>src/</c>, sem
/// obj e bin, e um removedor de comentários que respeita textos (normais, verbatim, interpolados e brutos) e caracteres.
/// Um <c>//</c> dentro de um texto (uma URL, por exemplo) não engole o resto da linha.
/// </summary>
internal static class FonteDoProduto
{
    internal static readonly string Src = Path.Combine(Caminhos.Raiz, "src");

    internal static IEnumerable<string> Arquivos() => Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories)
        .Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]"));

    internal static string Relativo(string arquivo) => Path.GetRelativePath(Src, arquivo);

    /// <summary>O código sem os comentários de linha, de bloco e de documentação; textos e caracteres ficam intactos.</summary>
    internal static string SemComentarios(string codigo)
    {
        var saida = new StringBuilder(codigo.Length);
        int i = 0;
        while (i < codigo.Length)
        {
            char c = codigo[i];
            char proximo = i + 1 < codigo.Length ? codigo[i + 1] : '\0';
            if (c == '/' && proximo == '/')
            {
                while (i < codigo.Length && codigo[i] != '\n') i++;
                continue;
            }
            if (c == '/' && proximo == '*')
            {
                int fim = codigo.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = fim < 0 ? codigo.Length : fim + 2;
                saida.Append(' ');
                continue;
            }
            if (c == '"' || ((c is '@' or '$') && (proximo == '"' || (proximo is '@' or '$' && i + 2 < codigo.Length && codigo[i + 2] == '"'))))
            {
                i = CopiarTexto(codigo, i, saida);
                continue;
            }
            if (c == '\'')
            {
                int j = i + 1;
                while (j < codigo.Length && codigo[j] != '\'' && codigo[j] != '\n') j += codigo[j] == '\\' ? 2 : 1;
                saida.Append(codigo, i, Math.Min(j + 1, codigo.Length) - i);
                i = j + 1;
                continue;
            }
            saida.Append(c);
            i++;
        }
        return saida.ToString();
    }

    /// <summary>Copia um texto a partir de <paramref name="i"/> (com os prefixos @ e $) e devolve onde ele termina.</summary>
    private static int CopiarTexto(string codigo, int i, StringBuilder saida)
    {
        int inicio = i;
        bool verbatim = false;
        while (codigo[i] is '@' or '$')
        {
            if (codigo[i] == '@') verbatim = true;
            i++;
        }
        int aspas = 0;
        while (i < codigo.Length && codigo[i] == '"')
        {
            aspas++;
            i++;
        }
        if (aspas >= 3)
        {
            // Texto bruto: termina na mesma quantidade de aspas.
            string fecho = new('"', aspas);
            int fim = codigo.IndexOf(fecho, i, StringComparison.Ordinal);
            i = fim < 0 ? codigo.Length : fim + aspas;
        }
        else if (aspas == 2)
        {
            // Texto vazio ("").
        }
        else
        {
            while (i < codigo.Length)
            {
                char c = codigo[i];
                if (verbatim && c == '"' && i + 1 < codigo.Length && codigo[i + 1] == '"') { i += 2; continue; }
                if (!verbatim && c == '\\') { i += 2; continue; }
                if (c == '"') { i++; break; }
                if (!verbatim && c == '\n') break;
                i++;
            }
        }
        i = Math.Min(i, codigo.Length);
        saida.Append(codigo, inicio, i - inicio);
        return i;
    }
}
