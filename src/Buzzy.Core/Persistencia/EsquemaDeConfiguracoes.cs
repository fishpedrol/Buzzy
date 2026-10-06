using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Unicode;
using Buzzy.Core.Personagem;

namespace Buzzy.Core.Persistencia;

/// <summary>
/// Esquema v6 do settings.json (Fase 5; ARCHITECTURE.md 2.12; SECURITY.md 7): converte bytes em
/// <see cref="ConfiguracoesSalvas"/> e de volta, sem E/S. A v2 acrescentou a emoção dominante
/// (<c>preferencias.emocaoDominante</c>, DEC-027); a v3, a postura gravada com a posição (DEC-029, item 11): a borda do
/// esconderijo (<c>posicao.esconderijo</c>, DEC-025) e a marca "preso pelo usuário" (<c>posicao.presoPeloUsuario</c>,
/// DEC-024); a v4, a chave do conteúdo adulto (<c>preferencias.conteudoAdulto</c>, DEC-033); a v5, o sempre no topo e a
/// escala (<c>preferencias.sempreNoTopo</c> e <c>preferencias.escala</c>, DEC-038); a v6, os itens adultos habilitados
/// (<c>preferencias.itensAdultosHabilitados</c>, DEC-041). Campos ausentes usam os padrões atuais: chave geral adulta
/// desligada e vodka, cerveja e cigarro selecionados; o baseado fica desmarcado. Valores gerais já gravados continuam preservados; a raiz só
/// regrava uma versão anterior no primeiro pedido, conforme a agenda existente.
///
/// A leitura é tolerante campo a campo e nunca lança. Só é ilegível o arquivo grande demais, fora de
/// UTF-8, que não é JSON (comentários e vírgula final são aceitos), fundo demais, sem objeto na raiz ou
/// sem um <c>schemaVersion</c> inteiro positivo. No resto, um campo desconhecido é ignorado, um repetido
/// vale na primeira ocorrência, um número fora da faixa é preso ao limite e um tipo errado vale o padrão
/// do campo; a posição é tudo ou nada nos campos obrigatórios. Cada caso gera um aviso.
///
/// A escrita produz sempre o mesmo texto para o mesmo conteúdo: UTF-8 sem BOM, indentação de 2
/// espaços, fim de linha <c>\n</c> (também no fim do arquivo), campos numa ordem fixa e números no
/// formato mais curto que reproduz o valor, sem depender da cultura.
///
/// Sem JsonSerializer e sem reflexão: a leitura extrai campo a campo de um <see cref="JsonDocument"/>, e
/// a escrita usa <see cref="Utf8JsonWriter"/>.
/// </summary>
public static class EsquemaDeConfiguracoes
{
    /// <summary>
    /// Versão escrita no campo <c>schemaVersion</c>. Toda ampliação do esquema a incrementa: a 2 acrescentou a emoção
    /// dominante, a 3, a borda do esconderijo e a marca de preso, a 4, o conteúdo adulto, a 5, o sempre no topo e a escala
    /// (DEC-038), e a 6, os itens adultos habilitados (DEC-041); um build anterior vê o arquivo novo como versão futura e não grava por cima.
    /// </summary>
    public const int VersaoAtual = 6;

    /// <summary>Tamanho máximo do arquivo, contando um BOM; maior, é ilegível sem ser interpretado.</summary>
    public const int TamanhoMaximoEmBytes = 65_536;

    /// <summary>Profundidade máxima de objetos e listas aninhados, contando a raiz; mais funda, o arquivo é ilegível.</summary>
    public const int ProfundidadeMaxima = 8;

    /// <summary>Comprimento máximo da chave do monitor, em caracteres UTF-16.</summary>
    public const int ComprimentoMaximoDaChave = 1_024;

    /// <summary>Faixa das coordenadas gravadas (âncora e tela do monitor), em pixels físicos.</summary>
    public const int CoordenadaMinima = -32_768, CoordenadaMaxima = 32_767;

    // Campos do esquema v6, na ordem em que são escritos.
    private static readonly string[] CamposDaRaiz = ["schemaVersion", "posicao", "preferencias"];
    private static readonly string[] CamposDaPosicao = ["chaveMonitor", "telaDoMonitor", "fracaoX", "fracaoY", "ancoraAbsoluta", "esconderijo", "presoPeloUsuario"];
    private static readonly string[] CamposDaTela = ["esquerda", "topo", "direita", "base"];
    private static readonly string[] CamposDaAncora = ["x", "y"];
    private static readonly string[] CamposDasPreferencias = ["energia", "modoTelaCheia", "atravessarMonitores", "emocaoDominante", "conteudoAdulto", "sempreNoTopo", "escala", "itensAdultosHabilitados"];

    private static readonly EscalaDoPersonagem[] Escalas = [EscalaDoPersonagem.Pequena, EscalaDoPersonagem.Media, EscalaDoPersonagem.Grande];

    private static readonly NivelDeEnergia[] NiveisDeEnergia = [NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta];

    /// <summary>As bordas do esconderijo e o "nenhum", a lista fechada da leitura (<see cref="TentarLerEsconderijo"/>).</summary>
    private static readonly LadoDoEsconderijo[] Bordas = [LadoDoEsconderijo.Nenhum, LadoDoEsconderijo.Baixo, LadoDoEsconderijo.Esquerda, LadoDoEsconderijo.Direita, LadoDoEsconderijo.Cima];

    /// <summary>A emoção dominante "Automática" (nula) no arquivo.</summary>
    private const string EmocaoAutomatica = "automatica";

    /// <summary>
    /// Os 14 nomes da emoção dominante no arquivo, na ordem de <see cref="Expressoes.DeHumor"/>: o nome da cara em
    /// minúsculas ASCII. É a lista fechada da leitura (<see cref="TentarLerEmocao"/>).
    /// </summary>
    private static readonly string[] NomesDasEmocoes = [.. Expressoes.DeHumor.Select(e => e.ToString().ToLowerInvariant())];

    private static readonly JsonDocumentOptions OpcoesDeLeitura = new()
    {
        MaxDepth = ProfundidadeMaxima,
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonWriterOptions OpcoesDeEscrita = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
    };

    private static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Lê o settings.json. Nunca lança: um arquivo que não se consegue ler é
    /// <see cref="SituacaoDaLeitura.Ilegivel"/>, com as configurações padrão.
    /// </summary>
    public static LeituraDasConfiguracoes Ler(ReadOnlyMemory<byte> conteudo)
    {
        if (conteudo.Length > TamanhoMaximoEmBytes) return Ilegivel("tamanho");
        if (conteudo.Span.StartsWith(Bom)) conteudo = conteudo[Bom.Length..];
        if (!Utf8.IsValid(conteudo.Span)) return Ilegivel("utf8");

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(conteudo, OpcoesDeLeitura);
        }
        catch (JsonException)
        {
            return Ilegivel("json");
        }

        using (documento)
        {
            try
            {
                return Extrair(documento.RootElement);
            }
            catch (InvalidOperationException)
            {
                // Um texto do arquivo que não vira UTF-16 válido (um escape de surrogate solto, num nome de campo ou num
                // valor): o System.Text.Json lança ao transcodificar, e o arquivo conta como JSON ilegível. A captura é só
                // dessa falha: um defeito da extração não pode passar por arquivo ilegível, que a gravação seguinte
                // trocaria pela cópia de diagnóstico, perdendo a posição; ele escapa, e a partida desliga a persistência.
                return Ilegivel("json");
            }
        }
    }

    /// <summary>
    /// Bytes do settings.json com as configurações normalizadas (<see cref="Normalizar"/>). Nunca lança por
    /// causa do conteúdo e nunca passa de <see cref="TamanhoMaximoEmBytes"/>: o pior caso, uma chave de
    /// 1024 caracteres todos escapados como <c>\uXXXX</c>, fica perto de 6 KiB.
    /// </summary>
    public static byte[] Escrever(ConfiguracoesSalvas configuracoes)
    {
        ConfiguracoesSalvas normalizadas = Normalizar(configuracoes);
        var saida = new ArrayBufferWriter<byte>(512);
        using (var json = new Utf8JsonWriter(saida, OpcoesDeEscrita))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", VersaoAtual);
            if (normalizadas.Posicao is { } p)
            {
                json.WriteStartObject("posicao");
                json.WriteString("chaveMonitor", p.ChaveMonitor);
                if (p.TelaDoMonitor is { } tela)
                {
                    json.WriteStartObject("telaDoMonitor");
                    json.WriteNumber("esquerda", tela.Esquerda);
                    json.WriteNumber("topo", tela.Topo);
                    json.WriteNumber("direita", tela.Direita);
                    json.WriteNumber("base", tela.Base);
                    json.WriteEndObject();
                }
                json.WriteNumber("fracaoX", p.FracaoX);
                json.WriteNumber("fracaoY", p.FracaoY);
                json.WriteStartObject("ancoraAbsoluta");
                json.WriteNumber("x", p.AncoraAbsoluta.X);
                json.WriteNumber("y", p.AncoraAbsoluta.Y);
                json.WriteEndObject();
                json.WriteString("esconderijo", NomeDoEsconderijo(normalizadas.Esconderijo));
                json.WriteBoolean("presoPeloUsuario", normalizadas.PresoPeloUsuario);
                json.WriteEndObject();
            }
            json.WriteStartObject("preferencias");
            json.WriteString("energia", NomeDaEnergia(normalizadas.Preferencias.Energia));
            json.WriteBoolean("modoTelaCheia", normalizadas.Preferencias.ModoTelaCheia);
            json.WriteBoolean("atravessarMonitores", normalizadas.Preferencias.AtravessarMonitores);
            json.WriteString("emocaoDominante", NomeDaEmocao(normalizadas.Preferencias.EmocaoDominante));
            json.WriteBoolean("conteudoAdulto", normalizadas.Preferencias.ConteudoAdulto);
            json.WriteBoolean("sempreNoTopo", normalizadas.Preferencias.SempreNoTopo);
            json.WriteString("escala", NomeDaEscala(normalizadas.Preferencias.Escala));
            json.WriteStartArray("itensAdultosHabilitados");
            foreach (Item item in TabelaDoTamagotchi.Itens)
                if (TabelaDoTamagotchi.Adulto(item) && normalizadas.Preferencias.ItensAdultosHabilitados.Contem(item))
                    json.WriteStringValue(NomeDoItemAdulto(item));
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return [.. saida.WrittenSpan, (byte)'\n'];
    }

    /// <summary>
    /// As configurações como o arquivo as guarda: posição sem chave válida (vazia, longa demais, com
    /// caractere de controle ou surrogate solto) vira nenhuma; frações saneadas (NaN vira 0,5, o resto é
    /// preso em [0, 1]); coordenadas presas na faixa; tela que fica vazia vira desconhecida; energia fora dos
    /// três níveis vira Média; emoção dominante fora das 14 caras de humor vira automática; preferências nulas, as
    /// padrão; a postura só existe com a posição (sem ela, nenhuma borda e solto), e uma borda fora do enum vira
    /// nenhuma. É o que <see cref="Ler"/> devolve do que <see cref="Escrever"/> escreveu.
    /// </summary>
    public static ConfiguracoesSalvas Normalizar(ConfiguracoesSalvas configuracoes)
    {
        ArgumentNullException.ThrowIfNull(configuracoes);
        PosicaoDoPersonagem? posicao = NormalizarPosicao(configuracoes.Posicao);
        return new ConfiguracoesSalvas(posicao, NormalizarPreferencias(configuracoes.Preferencias))
        {
            Esconderijo = posicao is not null && Enum.IsDefined(configuracoes.Esconderijo) ? configuracoes.Esconderijo : LadoDoEsconderijo.Nenhum,
            PresoPeloUsuario = posicao is not null && configuracoes.PresoPeloUsuario,
        };
    }

    /// <summary>Nome da escala no arquivo (DEC-038, item 9): <c>"pequena"</c>, <c>"media"</c> ou <c>"grande"</c>; fora das três, <c>"media"</c>.</summary>
    public static string NomeDaEscala(EscalaDoPersonagem escala) => escala switch
    {
        EscalaDoPersonagem.Pequena => "pequena",
        EscalaDoPersonagem.Grande => "grande",
        _ => "media",
    };

    /// <summary>
    /// Escala pelos três nomes do arquivo (<see cref="NomeDaEscala"/>), sem diferenciar maiúsculas, e nunca pelo Enum.Parse
    /// (SECURITY.md 7). Falso, <paramref name="escala"/> é a Média, o padrão.
    /// </summary>
    public static bool TentarLerEscala(string texto, out EscalaDoPersonagem escala)
    {
        ArgumentNullException.ThrowIfNull(texto);
        foreach (EscalaDoPersonagem candidata in Escalas)
        {
            if (string.Equals(texto, NomeDaEscala(candidata), StringComparison.OrdinalIgnoreCase))
            {
                escala = candidata;
                return true;
            }
        }
        escala = Preferencias.Padrao.Escala;
        return false;
    }

    /// <summary>Nome ASCII estável de um dos nove itens adultos no settings.json.</summary>
    private static string NomeDoItemAdulto(Item item) => item switch
    {
        Item.Vodka => "vodka",
        Item.Cerveja => "cerveja",
        Item.Baseado => "baseado",
        Item.Cigarro => "cigarro",
        Item.Cocaina => "cocaina",
        Item.Md => "md",
        Item.LancaPerfume => "lancaperfume",
        Item.Cogumelo => "cogumelo",
        Item.Bala => "bala",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Item não é adulto."),
    };

    /// <summary>Leitor por lista fechada para os identificadores persistidos (DEC-041).</summary>
    private static bool TentarLerItemAdulto(string nome, out Item item)
    {
        item = nome.ToLowerInvariant() switch
        {
            "vodka" => Item.Vodka,
            "cerveja" => Item.Cerveja,
            "baseado" => Item.Baseado,
            "cigarro" => Item.Cigarro,
            "cocaina" => Item.Cocaina,
            "md" => Item.Md,
            "lancaperfume" => Item.LancaPerfume,
            "cogumelo" => Item.Cogumelo,
            "bala" => Item.Bala,
            _ => default,
        };
        return TabelaDoTamagotchi.Adulto(item) && string.Equals(nome, NomeDoItemAdulto(item), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nome do nível no arquivo: <c>"baixa"</c>, <c>"media"</c> ou <c>"alta"</c>; fora dos três, <c>"media"</c>.</summary>
    public static string NomeDaEnergia(NivelDeEnergia nivel) => nivel switch
    {
        NivelDeEnergia.Baixa => "baixa",
        NivelDeEnergia.Alta => "alta",
        _ => "media",
    };

    /// <summary>
    /// Nível pelos três nomes do arquivo (<see cref="NomeDaEnergia"/>), sem diferenciar maiúsculas, e nunca
    /// pelo Enum.Parse, que aceitaria <c>"1"</c> e <c>"Baixa,Alta"</c>. Falso, <paramref name="nivel"/> é
    /// Média, o padrão seguro (SECURITY.md 7).
    /// </summary>
    public static bool TentarLerEnergia(string texto, out NivelDeEnergia nivel)
    {
        ArgumentNullException.ThrowIfNull(texto);
        foreach (NivelDeEnergia candidato in NiveisDeEnergia)
        {
            if (string.Equals(texto, NomeDaEnergia(candidato), StringComparison.OrdinalIgnoreCase))
            {
                nivel = candidato;
                return true;
            }
        }
        nivel = Preferencias.Padrao.Energia;
        return false;
    }

    /// <summary>
    /// Nome da emoção dominante no arquivo (DEC-027): <c>"automatica"</c> para nula, ou o nome da cara de humor em
    /// minúsculas ASCII, como <c>"feliz"</c>; fora das 14 caras de humor, <c>"automatica"</c>.
    /// </summary>
    public static string NomeDaEmocao(Expressao? emocao)
        => emocao is { } e && Expressoes.EhDeHumor(e) ? NomesDasEmocoes[(int)e] : EmocaoAutomatica;

    /// <summary>
    /// Emoção dominante por um dos nomes do arquivo (<see cref="NomeDaEmocao"/>: <c>"automatica"</c> ou uma das 14 caras
    /// de humor), sem diferenciar maiúsculas, e nunca pelo Enum.Parse, que aceitaria números, listas e as caras que
    /// não são de humor (SECURITY.md 7). Falso, <paramref name="emocao"/> é nula: a automática, o padrão seguro.
    /// </summary>
    public static bool TentarLerEmocao(string texto, out Expressao? emocao)
    {
        ArgumentNullException.ThrowIfNull(texto);
        emocao = null;
        if (string.Equals(texto, EmocaoAutomatica, StringComparison.OrdinalIgnoreCase)) return true;
        for (int i = 0; i < NomesDasEmocoes.Length; i++)
        {
            if (string.Equals(texto, NomesDasEmocoes[i], StringComparison.OrdinalIgnoreCase))
            {
                emocao = Expressoes.DeHumor[i];
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Nome da borda do esconderijo no arquivo (DEC-025): <c>"nenhum"</c>, <c>"baixo"</c>, <c>"esquerda"</c>,
    /// <c>"direita"</c> ou <c>"cima"</c> (2026-10-03, no mesmo esquema: uma versão anterior lê <c>"cima"</c> como nenhum, o
    /// padrão seguro); fora do enum, <c>"nenhum"</c>.
    /// </summary>
    public static string NomeDoEsconderijo(LadoDoEsconderijo lado) => lado switch
    {
        LadoDoEsconderijo.Baixo => "baixo",
        LadoDoEsconderijo.Esquerda => "esquerda",
        LadoDoEsconderijo.Direita => "direita",
        LadoDoEsconderijo.Cima => "cima",
        _ => "nenhum",
    };

    /// <summary>
    /// Borda do esconderijo pelos nomes do arquivo (<see cref="NomeDoEsconderijo"/>), sem diferenciar maiúsculas,
    /// e nunca pelo Enum.Parse, que aceitaria números e listas (SECURITY.md 7). Falso, <paramref name="lado"/> é nenhum,
    /// o padrão seguro.
    /// </summary>
    public static bool TentarLerEsconderijo(string texto, out LadoDoEsconderijo lado)
    {
        ArgumentNullException.ThrowIfNull(texto);
        foreach (LadoDoEsconderijo candidato in Bordas)
        {
            if (string.Equals(texto, NomeDoEsconderijo(candidato), StringComparison.OrdinalIgnoreCase))
            {
                lado = candidato;
                return true;
            }
        }
        lado = LadoDoEsconderijo.Nenhum;
        return false;
    }

    // ---------------------------------------------------------------- leitura campo a campo

    private static LeituraDasConfiguracoes Extrair(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object) return Ilegivel("raiz");

        var avisos = new List<string>();
        JsonElement?[] campos = Campos(raiz, CamposDaRaiz, "", avisos);
        if (campos[0] is not { ValueKind: JsonValueKind.Number } versaoLida || !versaoLida.TryGetInt32(out int versao) || versao < 1)
            return Ilegivel("schemaVersion");

        (PosicaoDoPersonagem? posicao, LadoDoEsconderijo esconderijo, bool preso) = LerPosicao(campos[1], avisos);
        var configuracoes = new ConfiguracoesSalvas(posicao, LerPreferencias(campos[2], avisos)) { Esconderijo = esconderijo, PresoPeloUsuario = preso };
        SituacaoDaLeitura situacao = versao > VersaoAtual ? SituacaoDaLeitura.VersaoFutura : SituacaoDaLeitura.Valida;
        return new LeituraDasConfiguracoes(situacao, versao, configuracoes, avisos.AsReadOnly(), null);
    }

    private static LeituraDasConfiguracoes Ilegivel(string motivo)
        => new(SituacaoDaLeitura.Ilegivel, null, ConfiguracoesSalvas.Padrao, [], motivo);

    /// <summary>
    /// A primeira ocorrência de cada campo do esquema no objeto, na ordem de <paramref name="nomes"/>; nula
    /// quando ausente. O nome precisa ser igual, diferenciando maiúsculas. Uma repetição e um campo
    /// desconhecido só geram aviso, com o nome do esquema e nunca um nome ou valor do arquivo.
    /// </summary>
    private static JsonElement?[] Campos(JsonElement objeto, string[] nomes, string onde, List<string> avisos)
    {
        var achados = new JsonElement?[nomes.Length];
        foreach (JsonProperty campo in objeto.EnumerateObject())
        {
            int i = 0;
            while (i < nomes.Length && !campo.NameEquals(nomes[i])) i++;
            if (i == nomes.Length)
                avisos.Add(onde.Length == 0 ? "campo desconhecido na raiz: ignorado" : $"campo desconhecido em {onde}: ignorado");
            else if (achados[i] is not null)
                avisos.Add($"campo repetido: {(onde.Length == 0 ? nomes[i] : $"{onde}.{nomes[i]}")}; vale o primeiro");
            else
                achados[i] = campo.Value;
        }
        return achados;
    }

    /// <summary>
    /// A posição, tudo ou nada nos campos obrigatórios: sem a chave ou uma das frações, ou com uma delas
    /// inválida, não há posição, nem a postura que vem com ela. A tela do monitor e a âncora são opcionais: inválidas,
    /// valem desconhecida e (0, 0), porque a partida recalcula a âncora de qualquer forma (Posicionador.Restaurar). A
    /// postura (v3) também é opcional: ausente, como num arquivo v1 ou v2, vale nenhuma borda e solto, sem aviso.
    /// </summary>
    private static (PosicaoDoPersonagem? Posicao, LadoDoEsconderijo Esconderijo, bool Preso) LerPosicao(JsonElement? valor, List<string> avisos)
    {
        if (valor is not { } posicao || posicao.ValueKind == JsonValueKind.Null) return default;
        if (posicao.ValueKind != JsonValueKind.Object)
        {
            avisos.Add("posicao: não é um objeto; sem posição");
            return default;
        }

        JsonElement?[] campos = Campos(posicao, CamposDaPosicao, "posicao", avisos);
        string? chave = campos[0] is { ValueKind: JsonValueKind.String } texto ? texto.GetString() : null;
        if (!ChaveValida(chave))
        {
            avisos.Add("posicao.chaveMonitor: ausente ou inválida; sem posição");
            return default;
        }
        if (!LerFracao(campos[2], "posicao.fracaoX", avisos, out double fracaoX) || !LerFracao(campos[3], "posicao.fracaoY", avisos, out double fracaoY))
            return default;
        var lida = new PosicaoDoPersonagem(chave, fracaoX, fracaoY, LerAncora(campos[4], avisos)) { TelaDoMonitor = LerTela(campos[1], avisos) };
        return (lida, LerEsconderijo(campos[5], avisos), LerBooleano(campos[6], "posicao.presoPeloUsuario", false, avisos));
    }

    /// <summary>
    /// Borda do esconderijo, opcional: um dos nomes (<see cref="TentarLerEsconderijo"/>). Ausente ou nula vale
    /// nenhuma, sem aviso, como a emoção; outro valor vale nenhuma, com um aviso que não repete o valor do arquivo.
    /// </summary>
    private static LadoDoEsconderijo LerEsconderijo(JsonElement? valor, List<string> avisos)
    {
        if (valor is not { ValueKind: not JsonValueKind.Null } borda) return LadoDoEsconderijo.Nenhum;
        if (borda.ValueKind == JsonValueKind.String && TentarLerEsconderijo(borda.GetString()!, out LadoDoEsconderijo lado)) return lado;
        avisos.Add("posicao.esconderijo: não é nenhum, baixo, esquerda, direita nem cima; vale nenhum");
        return LadoDoEsconderijo.Nenhum;
    }

    /// <summary>Fração obrigatória: um número finito, preso em [0, 1]. Falso se ausente ou de outro tipo.</summary>
    private static bool LerFracao(JsonElement? valor, string nome, List<string> avisos, out double fracao)
    {
        if (valor is { ValueKind: JsonValueKind.Number } numero && numero.TryGetDouble(out double lida) && double.IsFinite(lida))
        {
            fracao = Fracao(lida);
            if (fracao != lida) avisos.Add($"{nome}: fora de [0, 1]; presa ao limite");
            return true;
        }
        avisos.Add($"{nome}: ausente ou não é um número finito; sem posição");
        fracao = 0;
        return false;
    }

    /// <summary>
    /// Tela do monitor da época, opcional: os quatro lados inteiros, presos na faixa das coordenadas, com
    /// esquerda menor que direita e topo menor que base. Qualquer outra coisa vale desconhecida.
    /// </summary>
    private static RetanguloPx? LerTela(JsonElement? valor, List<string> avisos)
    {
        if (valor is not { } tela || tela.ValueKind == JsonValueKind.Null) return null;
        if (tela.ValueKind == JsonValueKind.Object)
        {
            JsonElement?[] lados = Campos(tela, CamposDaTela, "posicao.telaDoMonitor", avisos);
            if (LerCoordenada(lados[0], "posicao.telaDoMonitor.esquerda", avisos, out int esquerda)
                && LerCoordenada(lados[1], "posicao.telaDoMonitor.topo", avisos, out int topo)
                && LerCoordenada(lados[2], "posicao.telaDoMonitor.direita", avisos, out int direita)
                && LerCoordenada(lados[3], "posicao.telaDoMonitor.base", avisos, out int baseY)
                && new RetanguloPx(esquerda, topo, direita, baseY) is { Vazio: false } lida)
                return lida;
        }
        avisos.Add("posicao.telaDoMonitor: inválida; tela desconhecida");
        return null;
    }

    /// <summary>Âncora absoluta, opcional: x e y inteiros, presos na faixa das coordenadas; senão, (0, 0).</summary>
    private static PontoPx LerAncora(JsonElement? valor, List<string> avisos)
    {
        if (valor is not { } ancora || ancora.ValueKind == JsonValueKind.Null) return default;
        if (ancora.ValueKind == JsonValueKind.Object)
        {
            JsonElement?[] eixos = Campos(ancora, CamposDaAncora, "posicao.ancoraAbsoluta", avisos);
            if (LerCoordenada(eixos[0], "posicao.ancoraAbsoluta.x", avisos, out int x) && LerCoordenada(eixos[1], "posicao.ancoraAbsoluta.y", avisos, out int y))
                return new PontoPx(x, y);
        }
        avisos.Add("posicao.ancoraAbsoluta: inválida; vale (0, 0)");
        return default;
    }

    /// <summary>Coordenada: um número inteiro, preso na faixa das coordenadas. Falso se ausente ou de outro tipo.</summary>
    private static bool LerCoordenada(JsonElement? valor, string nome, List<string> avisos, out int coordenada)
    {
        coordenada = 0;
        if (valor is not { ValueKind: JsonValueKind.Number } numero || !numero.TryGetInt64(out long lida)) return false;
        coordenada = Coordenada(lida);
        if (coordenada != lida) avisos.Add($"{nome}: fora da faixa; presa ao limite");
        return true;
    }

    /// <summary>
    /// Preferências campo a campo: ausente vale o padrão, sem aviso; inválido vale o padrão, com aviso. A emoção dominante
    /// nula ou ausente é automática; conteúdo adulto ausente é desligado; topo ausente é ligado; escala ausente é Média;
    /// seleção de itens ausente habilita vodka, cerveja e cigarro; o baseado fica desmarcado.
    /// </summary>
    private static Preferencias LerPreferencias(JsonElement? valor, List<string> avisos)
    {
        Preferencias padrao = Preferencias.Padrao;
        if (valor is not { } preferencias || preferencias.ValueKind == JsonValueKind.Null) return padrao;
        if (preferencias.ValueKind != JsonValueKind.Object)
        {
            avisos.Add("preferencias: não é um objeto; valem os padrões");
            return padrao;
        }

        JsonElement?[] campos = Campos(preferencias, CamposDasPreferencias, "preferencias", avisos);
        NivelDeEnergia energia = padrao.Energia;
        if (campos[0] is { } nivel && (nivel.ValueKind != JsonValueKind.String || !TentarLerEnergia(nivel.GetString()!, out energia)))
            avisos.Add("preferencias.energia: não é baixa, media nem alta; vale media");
        Expressao? emocao = null;
        if (campos[3] is { ValueKind: not JsonValueKind.Null } nomeDaEmocao
            && (nomeDaEmocao.ValueKind != JsonValueKind.String || !TentarLerEmocao(nomeDaEmocao.GetString()!, out emocao)))
            avisos.Add("preferencias.emocaoDominante: não é uma das expressões; vale automatica");
        EscalaDoPersonagem escala = padrao.Escala;
        if (campos[6] is { ValueKind: not JsonValueKind.Null } nomeDaEscala
            && (nomeDaEscala.ValueKind != JsonValueKind.String || !TentarLerEscala(nomeDaEscala.GetString()!, out escala)))
            avisos.Add("preferencias.escala: não é pequena, media nem grande; vale media");
        return new Preferencias(
            energia,
            LerBooleano(campos[1], "preferencias.modoTelaCheia", padrao.ModoTelaCheia, avisos),
            LerBooleano(campos[2], "preferencias.atravessarMonitores", padrao.AtravessarMonitores, avisos))
        {
            EmocaoDominante = emocao,
            ConteudoAdulto = LerBooleano(campos[4], "preferencias.conteudoAdulto", padrao.ConteudoAdulto, avisos),
            SempreNoTopo = LerBooleano(campos[5], "preferencias.sempreNoTopo", padrao.SempreNoTopo, avisos),
            Escala = escala,
            ItensAdultosHabilitados = LerItensAdultos(campos[7], padrao.ItensAdultosHabilitados, avisos),
        };
    }

    private static ConjuntoDeItens LerItensAdultos(JsonElement? valor, ConjuntoDeItens padrao, List<string> avisos)
    {
        if (valor is null) return padrao;
        if (valor.Value.ValueKind != JsonValueKind.Array)
        {
            avisos.Add("preferencias.itensAdultosHabilitados: não é uma lista; valem os padrões");
            return padrao;
        }

        ConjuntoDeItens itens = ConjuntoDeItens.Vazio;
        foreach (JsonElement elemento in valor.Value.EnumerateArray())
        {
            if (elemento.ValueKind != JsonValueKind.String || !TentarLerItemAdulto(elemento.GetString()!, out Item item))
            {
                avisos.Add("preferencias.itensAdultosHabilitados: identificador desconhecido ou inválido; ignorado");
                continue;
            }
            if (itens.Contem(item))
            {
                avisos.Add($"preferencias.itensAdultosHabilitados: {NomeDoItemAdulto(item)} repetido; ignorado");
                continue;
            }
            itens = itens.Com(item);
        }
        return itens;
    }

    private static bool LerBooleano(JsonElement? valor, string nome, bool padrao, List<string> avisos)
    {
        switch (valor?.ValueKind)
        {
            case null:
                return padrao;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                avisos.Add($"{nome}: não é true nem false; vale o padrão");
                return padrao;
        }
    }

    // ---------------------------------------------------------------- regras de valor

    private static PosicaoDoPersonagem? NormalizarPosicao(PosicaoDoPersonagem? posicao)
    {
        if (posicao is null || !ChaveValida(posicao.ChaveMonitor)) return null;
        var ancora = new PontoPx(Coordenada(posicao.AncoraAbsoluta.X), Coordenada(posicao.AncoraAbsoluta.Y));
        RetanguloPx? tela = posicao.TelaDoMonitor is { } t
            && new RetanguloPx(Coordenada(t.Esquerda), Coordenada(t.Topo), Coordenada(t.Direita), Coordenada(t.Base)) is { Vazio: false } presa
            ? presa
            : null;
        return new PosicaoDoPersonagem(posicao.ChaveMonitor, Fracao(posicao.FracaoX), Fracao(posicao.FracaoY), ancora) { TelaDoMonitor = tela };
    }

    private static Preferencias NormalizarPreferencias(Preferencias? preferencias)
    {
        if (preferencias is null) return Preferencias.Padrao;
        if (!Enum.IsDefined(preferencias.Energia)) preferencias = preferencias with { Energia = Preferencias.Padrao.Energia };
        if (preferencias.EmocaoDominante is { } emocao && !Expressoes.EhDeHumor(emocao)) preferencias = preferencias with { EmocaoDominante = null };
        if (!Enum.IsDefined(preferencias.Escala)) preferencias = preferencias with { Escala = Preferencias.Padrao.Escala };
        preferencias = preferencias with { ItensAdultosHabilitados = Preferencias.NormalizarItensAdultos(preferencias.ItensAdultosHabilitados) };
        return preferencias;
    }

    /// <summary>
    /// Chave gravável: de 1 a <see cref="ComprimentoMaximoDaChave"/> caracteres, sem caractere de controle e
    /// em UTF-16 válido (um surrogate solto não tem representação em JSON).
    /// </summary>
    private static bool ChaveValida([NotNullWhen(true)] string? chave)
    {
        if (string.IsNullOrEmpty(chave) || chave.Length > ComprimentoMaximoDaChave) return false;
        for (int i = 0; i < chave.Length; i++)
        {
            if (char.IsControl(chave[i])) return false;
            if (char.IsHighSurrogate(chave[i]) && i + 1 < chave.Length && char.IsLowSurrogate(chave[i + 1])) i++;
            else if (char.IsSurrogate(chave[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Fração saneada como na restauração (<see cref="Posicionador.SanearFracao"/>), sem o zero negativo, que
    /// o arquivo escreveria como <c>-0</c>.
    /// </summary>
    private static double Fracao(double fracao)
    {
        double saneada = Posicionador.SanearFracao(fracao);
        return saneada == 0 ? 0 : saneada;
    }

    private static int Coordenada(long valor) => (int)Math.Clamp(valor, CoordenadaMinima, CoordenadaMaxima);
}
