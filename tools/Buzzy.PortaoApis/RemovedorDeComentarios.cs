namespace Buzzy.PortaoApis;

// Troca comentários C# por espaços sem mexer em strings. Mantém comprimento e quebras, então
// linha e coluna batem com o original. Entende strings comuns, verbatim, interpoladas, brutas,
// chars e diretivas (mensagem de #region/#error vira comentário).
// Não valida nada: no pior caso sobra comentário, o que só dá achado a mais, nunca esconde código.
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
        // Interpolação aninhada além disso fica como texto, pra não estourar a pilha.
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

        // Dentro de interpolação, para no '}' que fecha sem consumi-lo; o formato depois de ':' é texto.
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

        // "{{" é texto; senão percorre a expressão até o '}'.
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

        // Termina na primeira sequência com pelo menos tantas aspas quanto abriram. Expressões de
        // string bruta interpolada ficam como texto.
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

        // Vai até o fim da linha; aspas não abrem string aqui (#line "arquivo"). Mensagem de
        // #region, #endregion, #error e #warning vira espaço, como comentário.
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
