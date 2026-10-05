using Buzzy.Testes;

namespace Buzzy.PortaoApis.Testes;

/// <summary>
/// Remoção de comentários antes da busca no código-fonte: comentários viram espaço, textos
/// ficam intactos, comprimento e quebras de linha são preservados.
/// </summary>
public sealed class TestesDoRemovedorDeComentarios
{
    /// <summary>Espaços do tamanho do trecho: como um comentário fica depois da remoção.</summary>
    private static string B(string trecho) => new(' ', trecho.Length);

    private static string Remover(string fonte)
    {
        string resultado = RemovedorDeComentarios.Remover(fonte);
        Afirmar.Igual(fonte.Length, resultado.Length, "o comprimento mudou");
        for (int i = 0; i < fonte.Length; i++)
        {
            if (fonte[i] is '\n' or '\r') Afirmar.Igual(fonte[i], resultado[i], $"quebra de linha alterada na posição {i}");
        }
        return resultado;
    }

    [Teste]
    public void ComentarioDeLinhaViraEspaco()
    {
        Afirmar.Igual("int a = 1; " + B("// SendInput") + "\r\nint b = 2;", Remover("int a = 1; // SendInput\r\nint b = 2;"));
        Afirmar.Igual(B("/// <see cref=\"SendInput\"/>") + "\nint x;", Remover("/// <see cref=\"SendInput\"/>\nint x;"));
    }

    [Teste]
    public void BarrasDentroDeTextoNaoSaoComentario()
    {
        const string codigo = "var u = \"http://exemplo/SendInput\"; ";
        Afirmar.Igual(codigo + B("// fim"), Remover(codigo + "// fim"));
        Afirmar.Igual("GetProcAddress(m, \"SendInput\");", Remover("GetProcAddress(m, \"SendInput\");"));
    }

    [Teste]
    public void ComentarioDeBlocoPreservaQuebrasDeLinha()
    {
        Afirmar.Igual("a " + B("/* um") + "\r\n" + B("SendInput */") + " b", Remover("a /* um\r\nSendInput */ b"));
        Afirmar.Igual("x" + B("/* a // b */") + "y", Remover("x/* a // b */y"));
    }

    [Teste]
    public void InicioDeBlocoDentroDeTextoNaoAbreComentario()
    {
        const string codigo = "var s = \"/* não é comentário */\"; x ";
        Afirmar.Igual(codigo + B("/* é */"), Remover(codigo + "/* é */"));
    }

    [Teste]
    public void ComentarioDeBlocoSemFimVaiAteOFimDoArquivo()
    {
        Afirmar.Igual("a " + B("/* sem fim") + "\n" + B("SendInput"), Remover("a /* sem fim\nSendInput"));
    }

    [Teste]
    public void TextoVerbatimComAspasDuplicadas()
    {
        const string codigo = """var p = @"C:\pasta""//""SendInput"; """;
        Afirmar.Igual(codigo + B("// c"), Remover(codigo + "// c"));
        const string multilinha = "var q = @\"linha 1 // texto\nlinha 2 /* texto */\"; ";
        Afirmar.Igual(multilinha + B("// c"), Remover(multilinha + "// c"));
    }

    [Teste]
    public void TextoBrutoPreservaBarrasEAspas()
    {
        string codigo = "var r = \"\"\"\n  // SendInput \"\" /* x */\n  \"\"\"; ";
        Afirmar.Igual(codigo + B("// é"), Remover(codigo + "// é"));
        string quatro = "var r = \"\"\"\"com \"\"\" dentro // x\"\"\"\"; ";
        Afirmar.Igual(quatro + B("/* é */"), Remover(quatro + "/* é */"));
    }

    [Teste]
    public void TextoInterpoladoTiraComentarioSoDaExpressao()
    {
        Afirmar.Igual(
            "var s = $\"texto // literal {a " + B("/* comentário */") + " + b} fim\"; " + B("// c"),
            Remover("var s = $\"texto // literal {a /* comentário */ + b} fim\"; // c"));
        Afirmar.Igual(
            "var s = $@\"{(x ? \"a//b\" : \"c\")} // não\"; " + B("/* sim */"),
            Remover("var s = $@\"{(x ? \"a//b\" : \"c\")} // não\"; /* sim */"));
        Afirmar.Igual(
            "var s = $\"{{literal // }} {$\"{x " + B("/* c */") + "}\"}\";",
            Remover("var s = $\"{{literal // }} {$\"{x /* c */}\"}\";"));
    }

    [Teste]
    public void FormatoDeInterpolacaoNaoEhComentario()
    {
        Afirmar.Igual("var s = $\"{data:dd//MM}\"; " + B("// c"), Remover("var s = $\"{data:dd//MM}\"; // c"));
        Afirmar.Igual("var s = $\"{global::System.Math.PI}\";", Remover("var s = $\"{global::System.Math.PI}\";"));
    }

    [Teste]
    public void CaracteresComAspaEBarra()
    {
        const string linha1 = "if (c == '\"' || c == '/') x++; ";
        const string linha2 = "char d = '\\''; var e = '\\\\'; ";
        Afirmar.Igual(linha1 + B("// SendInput") + "\n" + linha2 + B("// y"), Remover(linha1 + "// SendInput\n" + linha2 + "// y"));
    }

    [Teste]
    public void TextoComumSemFimTerminaNaQuebraDeLinha()
    {
        Afirmar.Igual("var s = \"sem fim\n" + B("// comentário"), Remover("var s = \"sem fim\n// comentário"));
    }

    [Teste]
    public void DiretivasTemComentarioEMensagensRemovidos()
    {
        string fonte = string.Join("\n",
            "#region Chamadas SendInput",
            "  #if DEBUG // SendInput",
            "int x = 1;",
            "#endif",
            "#pragma warning disable CA1000 // motivo SendInput",
            "#error SendInput \"sem fim",
            "int y = 2; // fim");
        string esperado = string.Join("\n",
            "#region" + B(" Chamadas SendInput"),
            "  #if DEBUG " + B("// SendInput"),
            "int x = 1;",
            "#endif",
            "#pragma warning disable CA1000 " + B("// motivo SendInput"),
            "#error" + B(" SendInput \"sem fim"),
            "int y = 2; " + B("// fim"));
        Afirmar.Igual(esperado, Remover(fonte));
    }

    [Teste]
    public void CerquilhaForaDoInicioDaLinhaNaoEhDiretiva()
    {
        const string codigo = "var s = \"#region // texto\"; int a = 1; ";
        Afirmar.Igual(codigo + B("// c"), Remover(codigo + "// c"));
    }

    [Teste]
    public void InterpolacaoAninhadaDemaisNaoEstouraAPilha()
    {
        const int niveis = 5000;
        string fonte = "var s = " + string.Concat(Enumerable.Repeat("$\"{", niveis)) + "x" + string.Concat(Enumerable.Repeat("}\"", niveis)) + "; // fim";
        string resultado = Remover(fonte);
        Afirmar.Contem("var s = $\"{$\"{", resultado);
    }

    [Teste]
    public void CodigoSemComentarioFicaIgual()
    {
        const string codigo = "namespace Buzzy;\r\ninternal static class A { static int B(int x) => x / 2 / 3; string C = @\"\\\\servidor\\pasta\"; }";
        Afirmar.Igual(codigo, Remover(codigo));
    }
}
