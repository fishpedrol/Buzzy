using System.Text;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Personagem;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Persistencia;

/// <summary>
/// Fase 9, passo F9-P3 (DEC-040, item 7): o leitor de configurações com entradas aleatórias, por mutação de bytes de um
/// corpus válido (as amostras v1 a v5 e saídas do próprio escritor), com semente fixa. Invariantes: <see cref="EsquemaDeConfiguracoes.Ler"/>
/// nunca lança; ilegível devolve os padrões, sem avisos, com um motivo conhecido; legível sai normalizado, cabe no limite
/// e volta igual ao ser regravado; e nenhum aviso leva um valor nem um nome de campo do arquivo (o marcador injetado).
/// </summary>
internal static class EntradasAleatoriasTestes
{
    private const int Semente = 20261005;
    private const int Casos = 20_000;
    private const string Marcador = "ZZMARCA";

    private static readonly string[] Motivos = ["tamanho", "utf8", "json", "raiz", "schemaVersion"];

    private static readonly string[] ValoresTrocados =
    [
        "null", "[]", "{}", "\"x\"", "\"" + Marcador + "\"", "1e400", "-1e400", "-0", "1e-400", "99999999999999999999", "-99999999999999999999",
        "true", "false", "\"\\uD800\"", "\"\\uDC00x\"", "0.5", "-1", "2147483648", "NaN", "\"NaN\"", "[[[[[[[[[[1]]]]]]]]]]",
        "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":1}}}}}}}", "\"" + new string('a', 1025) + "\"", "\"\\u0000\"",
    ];

    private static byte[][] Corpus()
    {
        var corpus = new List<byte[]>();
        foreach (string v in new[] { "settings-v1.json", "settings-v2.json", "settings-v3.json", "settings-v4.json", "settings-v5.json" })
            corpus.Add(File.ReadAllBytes(Path.Combine(ReproducaoTestes.PastaDasFontes(), "Persistencia", "Amostras", v)));
        var r = new Random(Semente);
        for (int i = 0; i < 40; i++)
        {
            var preferencias = new Preferencias((NivelDeEnergia)r.Next(3), r.Next(2) == 0, r.Next(2) == 0)
            {
                SempreNoTopo = r.Next(2) == 0,
                Escala = (EscalaDoPersonagem)r.Next(3),
            };
            PosicaoDoPersonagem? posicao = r.Next(4) == 0 ? null
                : new PosicaoDoPersonagem(r.Next(2) == 0 ? @"\\.\DISPLAY1" : $"mon:{r.NextInt64():x16}", r.NextDouble(), r.NextDouble(), new PontoPx(r.Next(-4000, 4000), r.Next(-2000, 3000)));
            corpus.Add(EsquemaDeConfiguracoes.Escrever(new ConfiguracoesSalvas(posicao, preferencias)));
        }
        return [.. corpus];
    }

    [Teste]
    public static void Ler_MutacoesAleatorias_NuncaLancaENormaliza()
    {
        byte[][] corpus = Corpus();
        var r = new Random(Semente);
        var porSituacao = new Dictionary<SituacaoDaLeitura, int>();
        var porMotivo = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int caso = 0; caso < Casos; caso++)
        {
            byte[] base_ = corpus[r.Next(corpus.Length)];
            var operacoes = new List<string>();
            byte[] entrada = base_;
            int quantas = 1 + r.Next(3);
            for (int k = 0; k < quantas; k++) entrada = Mutar(entrada, r, operacoes);

            LeituraDasConfiguracoes lida;
            try
            {
                lida = EsquemaDeConfiguracoes.Ler(entrada);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"caso {caso} ({string.Join(", ", operacoes)}): Ler lançou {e.GetType().Name}: {e.Message}", e);
            }
            string onde = $"caso {caso} ({string.Join(", ", operacoes)})";
            Afirmar.Verdadeiro(Enum.IsDefined(lida.Situacao), $"{onde}: situação definida");
            porSituacao[lida.Situacao] = porSituacao.GetValueOrDefault(lida.Situacao) + 1;
            Afirmar.Falso(lida.Avisos.Any(a => a.Contains(Marcador, StringComparison.Ordinal)), $"{onde}: um aviso levou o marcador: {string.Join(" / ", lida.Avisos)}");
            if (lida.Situacao == SituacaoDaLeitura.Ilegivel)
            {
                Afirmar.Igual((ConfiguracoesSalvas.Padrao, 0, (int?)null), (lida.Configuracoes, lida.Avisos.Count, lida.Versao), $"{onde}: ilegível vale o padrão");
                Afirmar.Verdadeiro(lida.MotivoIlegivel is { } m && Motivos.Contains(m), $"{onde}: motivo {lida.MotivoIlegivel}");
                porMotivo[lida.MotivoIlegivel!] = porMotivo.GetValueOrDefault(lida.MotivoIlegivel!) + 1;
                continue;
            }
            Afirmar.Igual((string?)null, lida.MotivoIlegivel, $"{onde}: sem motivo de ilegível");
            if (lida.Situacao == SituacaoDaLeitura.VersaoFutura)
                Afirmar.Verdadeiro(lida.Versao > EsquemaDeConfiguracoes.VersaoAtual, $"{onde}: versão futura {lida.Versao}");
            ConfiguracoesSalvas c = lida.Configuracoes;
            Afirmar.Igual(EsquemaDeConfiguracoes.Normalizar(c), c, $"{onde}: o lido já sai normalizado");
            byte[] regravado = EsquemaDeConfiguracoes.Escrever(c);
            Afirmar.Verdadeiro(regravado.Length <= EsquemaDeConfiguracoes.TamanhoMaximoEmBytes, $"{onde}: regravado cabe ({regravado.Length})");
            LeituraDasConfiguracoes relida = EsquemaDeConfiguracoes.Ler(regravado);
            Afirmar.Igual((SituacaoDaLeitura.Valida, c, 0), (relida.Situacao, relida.Configuracoes, relida.Avisos.Count), $"{onde}: regravado e relido, igual e sem aviso");
        }
        // As mutações exercitam os três desfechos e os cinco motivos de ilegível.
        foreach (SituacaoDaLeitura s in Enum.GetValues<SituacaoDaLeitura>())
            Afirmar.Verdadeiro(porSituacao.GetValueOrDefault(s) > 50, $"{s}: {porSituacao.GetValueOrDefault(s)} casos");
        foreach (string m in Motivos)
            Afirmar.Verdadeiro(porMotivo.GetValueOrDefault(m) > 5, $"motivo {m}: {porMotivo.GetValueOrDefault(m)} casos");
        Console.WriteLine($"         {Casos} casos: {string.Join(", ", porSituacao.Select(p => $"{p.Key} {p.Value}"))}; ilegíveis: {string.Join(", ", porMotivo.Select(p => $"{p.Key} {p.Value}"))}");
    }

    /// <summary>Uma mutação sorteada, com o nome dela na lista (para a mensagem de um caso que falhe).</summary>
    private static byte[] Mutar(byte[] entrada, Random r, List<string> operacoes)
    {
        int op = r.Next(16);
        string texto = Encoding.UTF8.GetString(entrada);
        switch (op)
        {
            case 0 when entrada.Length > 0:
            {
                byte[] b = (byte[])entrada.Clone();
                int i = r.Next(b.Length);
                b[i] ^= (byte)(1 << r.Next(8));
                operacoes.Add($"bit em {i}");
                return b;
            }
            case 1:
            {
                int i = r.Next(entrada.Length + 1);
                operacoes.Add($"byte inserido em {i}");
                return [.. entrada[..i], (byte)r.Next(256), .. entrada[i..]];
            }
            case 2 when entrada.Length > 0:
            {
                int i = r.Next(entrada.Length);
                int n = 1 + r.Next(Math.Min(16, entrada.Length - i));
                operacoes.Add($"{n} bytes apagados em {i}");
                return [.. entrada[..i], .. entrada[(i + n)..]];
            }
            case 3 when entrada.Length > 0:
            {
                int i = r.Next(entrada.Length);
                int n = 1 + r.Next(Math.Min(64, entrada.Length - i));
                operacoes.Add($"{n} bytes duplicados em {i}");
                return [.. entrada[..(i + n)], .. entrada[i..]];
            }
            case 4:
            {
                int i = r.Next(entrada.Length + 1);
                operacoes.Add($"cortado em {i}");
                return entrada[..i];
            }
            case 5:
            case 6:
            {
                // Troca um valor (depois de ": ") por outro tipo ou por um valor extremo.
                var dois = IndicesDe(texto, ": ");
                if (dois.Count == 0) goto default;
                int i = dois[r.Next(dois.Count)] + 2;
                int fim = FimDoValor(texto, i);
                string novo = ValoresTrocados[r.Next(ValoresTrocados.Length)];
                operacoes.Add($"valor em {i} trocado por {novo[..Math.Min(20, novo.Length)]}");
                return Encoding.UTF8.GetBytes(texto[..i] + novo + texto[fim..]);
            }
            case 7:
            {
                // Um campo desconhecido (ou repetido) com o marcador no nome e no valor, no começo de um objeto.
                var chaves = IndicesDe(texto, "{");
                if (chaves.Count == 0) goto default;
                int i = chaves[r.Next(chaves.Count)] + 1;
                string campo = r.Next(2) == 0 ? $"\"{Marcador}\": \"{Marcador}\"," : RepetirUmCampo(texto, r);
                operacoes.Add("campo inserido");
                return Encoding.UTF8.GetBytes(texto[..i] + campo + texto[i..]);
            }
            case 8:
            {
                var aspas = IndicesDe(texto, "\"");
                if (aspas.Count == 0) goto default;
                int i = aspas[r.Next(aspas.Count)] + 1;
                string s = r.Next(2) == 0 ? "\\uD800" : "\\uDC00";
                operacoes.Add($"surrogate solto em {i}");
                return Encoding.UTF8.GetBytes(texto[..i] + s + texto[i..]);
            }
            case 9:
                operacoes.Add("BOM duplo");
                return [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. entrada];
            case 10:
            {
                int i = r.Next(entrada.Length + 1);
                byte[] altos = [.. Enumerable.Range(0, 1 + r.Next(4)).Select(_ => (byte)(0x80 + r.Next(128)))];
                operacoes.Add($"bytes altos em {i}");
                return [.. entrada[..i], .. altos, .. entrada[i..]];
            }
            case 11:
            {
                string v = r.Next(2) == 0 ? "-1" : r.Next(2) == 0 ? "0" : r.Next(2) == 0 ? "6" : "2147483648";
                operacoes.Add($"schemaVersion {v}");
                return Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(texto, @"""schemaVersion"":\s*-?\d+", $"\"schemaVersion\": {v}"));
            }
            case 12:
            {
                // Preenche até perto do limite de tamanho: 65 535, 65 536 ou 65 537 bytes.
                int alvo = EsquemaDeConfiguracoes.TamanhoMaximoEmBytes - 1 + r.Next(3);
                int fecha = texto.LastIndexOf('}');
                if (fecha < 0 || entrada.Length >= alvo) goto default;
                operacoes.Add($"preenchido até {alvo}");
                string pre = texto[..fecha], pos = texto[fecha..];
                int faltam = alvo - Encoding.UTF8.GetByteCount(pre) - Encoding.UTF8.GetByteCount(pos);
                return Encoding.UTF8.GetBytes(pre + new string(' ', Math.Max(0, faltam)) + pos);
            }
            case 13:
            {
                int n = 4 + r.Next(10);
                operacoes.Add($"raiz aninhada {n}");
                return Encoding.UTF8.GetBytes(new string('[', n) + texto + new string(']', n));
            }
            case 14:
            {
                byte[] lixo = new byte[r.Next(200)];
                r.NextBytes(lixo);
                operacoes.Add($"lixo de {lixo.Length} bytes");
                return lixo;
            }
            case 15:
            {
                var aspas = IndicesDe(texto, "\": \"");
                if (aspas.Count == 0) goto default;
                int i = aspas[r.Next(aspas.Count)] + 4;
                int n = r.Next(2) == 0 ? 1023 + r.Next(3) : 60_000;
                operacoes.Add($"texto de {n} em {i}");
                return Encoding.UTF8.GetBytes(texto[..i] + new string('m', n) + texto[i..]);
            }
            default:
                operacoes.Add("sem mudança");
                return entrada;
        }
    }

    private static List<int> IndicesDe(string texto, string trecho)
    {
        var lista = new List<int>();
        for (int i = texto.IndexOf(trecho, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(trecho, i + 1, StringComparison.Ordinal)) lista.Add(i);
        return lista;
    }

    /// <summary>O fim de um valor JSON simples a partir de <paramref name="i"/>: a vírgula, a chave ou o colchete que o fecha.</summary>
    private static int FimDoValor(string texto, int i)
    {
        int profundidade = 0;
        bool emTexto = false;
        for (int j = i; j < texto.Length; j++)
        {
            char c = texto[j];
            if (emTexto)
            {
                if (c == '\\') j++;
                else if (c == '"') emTexto = false;
                continue;
            }
            switch (c)
            {
                case '"': emTexto = true; break;
                case '{' or '[': profundidade++; break;
                case '}' or ']' when profundidade == 0: return j;
                case '}' or ']': profundidade--; break;
                case ',' when profundidade == 0: return j;
            }
        }
        return texto.Length;
    }

    private static string RepetirUmCampo(string texto, Random r)
    {
        string[] campos = ["\"energia\": \"alta\",", "\"escala\": \"grande\",", "\"sempreNoTopo\": false,", "\"fracaoX\": 0.9,", "\"chaveMonitor\": \"mon:" + Marcador + "\","];
        return campos[r.Next(campos.Length)];
    }
}
