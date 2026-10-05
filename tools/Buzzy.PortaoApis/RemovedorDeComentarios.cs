namespace Buzzy.PortaoApis;

/// <summary>
/// Troca por espaços os comentários de um arquivo C#, sem tocar nos literais de texto.
///
/// Preserva o comprimento e as quebras de linha, para que linha e coluna no resultado sejam as
/// do original. Reconhece comentários de linha e de bloco; textos comuns, verbatim (@"..."),
/// interpolados ($"..." e $@"...", com comentários dentro das expressões), brutos ("""...""");
/// caracteres ('"', '\''); e diretivas de pré-processador, cujas mensagens (#region, #error e
/// afins) também são tratadas como comentário.
///
/// Não valida o código. Num arquivo que não compila, ou nas expressões de um texto bruto
/// interpolado, o pior caso é sobrar comentário no resultado, o que só pode gerar achado a mais,
/// nunca esconder código.
/// </summary>
internal static class RemovedorDeComentarios
{
    public static string Remover(string fonte)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        var varredura = new Varredura(fonte);
        varredura.Codigo(dentroDeInterpolacao: false);
        return varredura.Resultado();
    }

    private static bool EhQuebraDeLinha(char c) => c is '\n' or '\r' or (char)0x85 or (char)0x2028 or (char)0x2029;

    private sealed class Varredura(string fonte)
    {
        // Textos interpolados dentro de expressões de textos interpolados, além deste limite,
        // ficam como texto: evita estourar a pilha com um arquivo patológico.
        private const int AninhamentoMaximo = 64;

        private readonly string _f = fonte;
        private readonly char[] _s = fonte.ToCharArray();
        private int _i;
        private int _aninhamento;

        public string Resultado() => new(_s);

        private char Adiante(int deslocamento)
        {
            int k = _i + deslocamento;
            return k < _f.Length ? _f[k] : '\0';
        }

        /// <summary>
        /// Percorre código. Dentro da expressão de um texto interpolado, para no '}' que fecha a
        /// expressão, sem consumi-lo; o especificador de formato depois de ':' é texto.
        /// </summary>
        public void Codigo(bool dentroDeInterpolacao)
        {
            int profundidade = 0;
            bool inicioDeLinha = !dentroDeInterpolacao;
            while (_i < _f.Length)
            {
                char c = _f[_i];
                if (c == '/' && Adiante(1) == '/') { ComentarioDeLinha(); continue; }
                if (c == '/' && Adiante(1) == '*') { ComentarioDeBloco(); continue; }
                if (inicioDeLinha && c == '#') { Diretiva(); continue; }
                if (EhQuebraDeLinha(c)) { inicioDeLinha = !dentroDeInterpolacao; _i++; continue; }
                if (char.IsWhiteSpace(c)) { _i++; continue; }
                inicioDeLinha = false;

                if (c == '\'') { Caractere(); continue; }
                if ((c is '"' or '@' or '$') && Texto()) continue;

                if (dentroDeInterpolacao)
                {
                    if (c is '(' or '[' or '{')
                    {
                        profundidade++;
                    }
                    else if (c is ')' or ']')
                    {
                        if (profundidade > 0) profundidade--;
                    }
                    else if (c == '}')
                    {
                        if (profundidade == 0) return;
                        profundidade--;
                    }
                    else if (c == ':' && profundidade == 0)
                    {
                        if (Adiante(1) == ':') { _i += 2; continue; }
                        while (_i < _f.Length && _f[_i] != '}') _i++;
                        return;
                    }
                }
                _i++;
            }
        }

        /// <summary>Consome o literal de texto que começa em _i, se houver um.</summary>
        private bool Texto()
        {
            int j = _i;
            bool verbatim = false;
            int cifroes = 0;
            if (_f[j] == '@') { verbatim = true; j++; }
            while (j < _f.Length && _f[j] == '$') { cifroes++; j++; }
            if (!verbatim && j < _f.Length && _f[j] == '@') { verbatim = true; j++; }
            if (j >= _f.Length || _f[j] != '"') return false;

            int aspas = 0;
            while (j + aspas < _f.Length && _f[j + aspas] == '"') aspas++;

            if (!verbatim && aspas >= 3)
            {
                _i = j + aspas;
                TextoBruto(aspas);
                return true;
            }

            _i = j + 1;
            if (verbatim) TextoVerbatim(interpolado: cifroes > 0);
            else TextoComum(interpolado: cifroes > 0);
            return true;
        }

        private void TextoComum(bool interpolado)
        {
            while (_i < _f.Length)
            {
                char c = _f[_i];
                if (c == '\\') { _i += 2; continue; }
                if (c == '"') { _i++; return; }
                if (EhQuebraDeLinha(c)) return;
                if (interpolado && c == '{' && Expressao()) continue;
                _i++;
            }
        }

        private void TextoVerbatim(bool interpolado)
        {
            while (_i < _f.Length)
            {
                char c = _f[_i];
                if (c == '"')
                {
                    if (Adiante(1) == '"') { _i += 2; continue; }
                    _i++;
                    return;
                }
                if (interpolado && c == '{' && Expressao()) continue;
                _i++;
            }
        }

        /// <summary>Em '{' de texto interpolado: "{{" é texto; senão percorre a expressão até o '}'.</summary>
        private bool Expressao()
        {
            if (Adiante(1) == '{') { _i += 2; return true; }
            if (_aninhamento >= AninhamentoMaximo) return false;
            _i++;
            _aninhamento++;
            Codigo(dentroDeInterpolacao: true);
            _aninhamento--;
            if (_i < _f.Length) _i++;
            return true;
        }

        // Texto bruto (C# 11): termina na primeira sequência de pelo menos tantas aspas quantas
        // abriram. As expressões de um texto bruto interpolado ficam como texto.
        private void TextoBruto(int aspas)
        {
            while (_i < _f.Length)
            {
                if (_f[_i] != '"') { _i++; continue; }
                int sequencia = 0;
                while (_i + sequencia < _f.Length && _f[_i + sequencia] == '"') sequencia++;
                _i += sequencia;
                if (sequencia >= aspas) return;
            }
        }

        private void Caractere()
        {
            _i++;
            while (_i < _f.Length)
            {
                char c = _f[_i];
                if (c == '\\') { _i += 2; continue; }
                if (c == '\'') { _i++; return; }
                if (EhQuebraDeLinha(c)) return;
                _i++;
            }
        }

        private void ComentarioDeLinha()
        {
            while (_i < _f.Length && !EhQuebraDeLinha(_f[_i])) _s[_i++] = ' ';
        }

        private void ComentarioDeBloco()
        {
            _s[_i] = ' ';
            _s[_i + 1] = ' ';
            _i += 2;
            while (_i < _f.Length)
            {
                if (_f[_i] == '*' && Adiante(1) == '/')
                {
                    _s[_i] = ' ';
                    _s[_i + 1] = ' ';
                    _i += 2;
                    return;
                }
                if (!EhQuebraDeLinha(_f[_i])) _s[_i] = ' ';
                _i++;
            }
        }

        // Diretiva de pré-processador: vai até o fim da linha. Aspas não abrem texto aqui
        // (#line "arquivo", #pragma checksum). As mensagens de #region, #endregion, #error e
        // #warning são texto livre e viram espaço, como comentário.
        private void Diretiva()
        {
            _i++;
            while (_i < _f.Length && (_f[_i] is ' ' or '\t')) _i++;
            int inicioDaPalavra = _i;
            while (_i < _f.Length && char.IsLetter(_f[_i])) _i++;
            bool mensagem = _f[inicioDaPalavra.._i] is "region" or "endregion" or "error" or "warning";
            while (_i < _f.Length && !EhQuebraDeLinha(_f[_i]))
            {
                if (mensagem) { _s[_i++] = ' '; continue; }
                if (_f[_i] == '/' && Adiante(1) == '/') { ComentarioDeLinha(); return; }
                _i++;
            }
        }
    }
}
