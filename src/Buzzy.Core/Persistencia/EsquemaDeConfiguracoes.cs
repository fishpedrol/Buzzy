using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Unicode;
using Buzzy.Core.Personagem;

namespace Buzzy.Core.Persistencia;

// settings.json <-> ConfiguracoesSalvas, sem E/S. Histórico do esquema:
// v2 emoção dominante; v3 esconderijo e preso junto da posição; v4 conteúdo adulto; v5 sempre
// no topo e escala; v6 itens adultos habilitados; v7 uso por conta própria das seis ilícitas.
// Campo ausente vale o padrão atual (adulto desligado; vodka, cerveja e cigarro marcados).
//
// Leitura tolerante, nunca lança. Só é ilegível se for grande demais, não-UTF-8, não-JSON
// (comentário e vírgula final passam), fundo demais, sem objeto na raiz ou sem schemaVersion
// inteiro positivo. Fora isso: desconhecido é ignorado, repetido vale o primeiro, número fora da
// faixa é preso e tipo errado vale o padrão; a posição é tudo ou nada. Tudo gera aviso.
//
// Escrita determinística: UTF-8 sem BOM, 2 espaços, \n (inclusive no fim), ordem fixa e números
// no formato mais curto, sem cultura. Sem JsonSerializer/reflexão de propósito.
public static class EsquemaDeConfiguracoes
{
    // Toda ampliação do esquema incrementa. Build antigo vê o arquivo novo como versão futura e
    // não grava por cima.
    public const int VersaoAtual = 7;

    // Contando o BOM. Maior que isso é ilegível sem nem interpretar.
    public const int TamanhoMaximoEmBytes = 65_536;

    // Contando a raiz.
    public const int ProfundidadeMaxima = 8;

    // Em chars UTF-16.
    public const int ComprimentoMaximoDaChave = 1_024;

    // Âncora e tela, em pixels físicos.
    public const int CoordenadaMinima = -32_768, CoordenadaMaxima = 32_767;

    // Na ordem em que são escritos.
    private static readonly string[] CamposDaRaiz = ["schemaVersion", "posicao", "preferencias"];
    private static readonly string[] CamposDaPosicao = ["chaveMonitor", "telaDoMonitor", "fracaoX", "fracaoY", "ancoraAbsoluta", "esconderijo", "presoPeloUsuario"];
    private static readonly string[] CamposDaTela = ["esquerda", "topo", "direita", "base"];
    private static readonly string[] CamposDaAncora = ["x", "y"];
    private static readonly string[] CamposDasPreferencias = ["energia", "modoTelaCheia", "atravessarMonitores", "emocaoDominante", "conteudoAdulto", "sempreNoTopo", "escala", "itensAdultosHabilitados", "itensPorContaPropria"];

    private static readonly EscalaDoPersonagem[] Escalas = [EscalaDoPersonagem.Pequena, EscalaDoPersonagem.Media, EscalaDoPersonagem.Grande];

    private static readonly NivelDeEnergia[] NiveisDeEnergia = [NivelDeEnergia.Baixa, NivelDeEnergia.Media, NivelDeEnergia.Alta];

    private static readonly LadoDoEsconderijo[] Bordas = [LadoDoEsconderijo.Nenhum, LadoDoEsconderijo.Baixo, LadoDoEsconderijo.Esquerda, LadoDoEsconderijo.Direita, LadoDoEsconderijo.Cima];

    // Emoção nula ("Automática") no arquivo.
    private const string EmocaoAutomatica = "automatica";

    // Na ordem de Expressoes.DeHumor, em minúsculas ASCII.
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

    // Nunca lança: o que não dá pra ler vira Ilegivel com os padrões.
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
                // Escape de surrogate solto: o System.Text.Json lança ao transcodificar. Só essa falha
                // conta como ilegível; um bug da extração tem que escapar, senão a próxima gravação
                // trocaria o arquivo pela cópia de diagnóstico e a posição se perderia.
                return Ilegivel("json");
            }
        }
    }

    // Normaliza antes. Nunca passa de TamanhoMaximoEmBytes: o pior caso, chave de 1024 chars
    // todos escapados como \uXXXX, dá uns 6 KiB.
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
            json.WriteStartArray("itensPorContaPropria");
            foreach (Item item in TabelaDoTamagotchi.Itens)
                if (TabelaDoTamagotchi.Ilicitos.Contem(item) && normalizadas.Preferencias.ItensPorContaPropria.Contem(item))
                    json.WriteStringValue(NomeDoItemAdulto(item));
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return [.. saida.WrittenSpan, (byte)'\n'];
    }

    // Como o arquivo guarda, ou seja, o que Ler devolve do que Escrever escreveu: chave inválida
    // apaga a posição; frações saneadas; coordenadas presas; tela vazia vira desconhecida; enums
    // fora da lista voltam ao padrão; esconderijo e preso só existem com posição.
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

    public static string NomeDaEscala(EscalaDoPersonagem escala) => escala switch
    {
        EscalaDoPersonagem.Pequena => "pequena",
        EscalaDoPersonagem.Grande => "grande",
        _ => "media",
    };

    // Lista fechada, sem diferenciar maiúsculas. Nunca Enum.Parse (aceitaria "1" e "a,b").
    // Se falhar, devolve Média.
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

    // Nomes estáveis: não renomear, já estão gravados nos arquivos.
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

    public static string NomeDaEnergia(NivelDeEnergia nivel) => nivel switch
    {
        NivelDeEnergia.Baixa => "baixa",
        NivelDeEnergia.Alta => "alta",
        _ => "media",
    };

    // Lista fechada, sem diferenciar maiúsculas. Nunca Enum.Parse (aceitaria "1" e "Baixa,Alta").
    // Se falhar, devolve Média.
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

    // Nula ou cara que não é de humor vira "automatica".
    public static string NomeDaEmocao(Expressao? emocao)
        => emocao is { } e && Expressoes.EhDeHumor(e) ? NomesDasEmocoes[(int)e] : EmocaoAutomatica;

    // Lista fechada, sem diferenciar maiúsculas. Nunca Enum.Parse: aceitaria números, listas e
    // caras que não são de humor. Se falhar, devolve nula (automática).
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

    // "cima" entrou sem subir a versão: um build antigo lê como nenhum, que é seguro.
    public static string NomeDoEsconderijo(LadoDoEsconderijo lado) => lado switch
    {
        LadoDoEsconderijo.Baixo => "baixo",
        LadoDoEsconderijo.Esquerda => "esquerda",
        LadoDoEsconderijo.Direita => "direita",
        LadoDoEsconderijo.Cima => "cima",
        _ => "nenhum",
    };

    // Lista fechada, sem Enum.Parse (aceitaria números e listas). Se falhar, devolve Nenhum.
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
        Preferencias lidas = LerPreferencias(campos[2], avisos);
        // Até a v6 a travessia era gravada sempre ligada, sem opção na tela: usa o padrão novo.
        if (versao < 7) lidas = lidas with { AtravessarMonitores = Preferencias.Padrao.AtravessarMonitores };
        var configuracoes = new ConfiguracoesSalvas(posicao, lidas) { Esconderijo = esconderijo, PresoPeloUsuario = preso };
        SituacaoDaLeitura situacao = versao > VersaoAtual ? SituacaoDaLeitura.VersaoFutura : SituacaoDaLeitura.Valida;
        return new LeituraDasConfiguracoes(situacao, versao, configuracoes, avisos.AsReadOnly(), null);
    }

    private static LeituraDasConfiguracoes Ilegivel(string motivo)
        => new(SituacaoDaLeitura.Ilegivel, null, ConfiguracoesSalvas.Padrao, [], motivo);

    // Primeira ocorrência de cada campo, na ordem de nomes (nula se ausente). Nome com maiúsculas
    // exatas. O aviso nunca repete nome ou valor vindo do arquivo.
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

    // Chave e frações são tudo ou nada: falta uma, não há posição (nem esconderijo/preso). Tela e
    // âncora são opcionais (a partida recalcula a âncora). Esconderijo/preso ausentes (v1, v2)
    // valem nenhum e solto, sem aviso.
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

    // Ausente ou nula: nenhum, sem aviso. Inválida: nenhum, com aviso (sem repetir o valor).
    private static LadoDoEsconderijo LerEsconderijo(JsonElement? valor, List<string> avisos)
    {
        if (valor is not { ValueKind: not JsonValueKind.Null } borda) return LadoDoEsconderijo.Nenhum;
        if (borda.ValueKind == JsonValueKind.String && TentarLerEsconderijo(borda.GetString()!, out LadoDoEsconderijo lado)) return lado;
        avisos.Add("posicao.esconderijo: não é nenhum, baixo, esquerda, direita nem cima; vale nenhum");
        return LadoDoEsconderijo.Nenhum;
    }

    // Número finito, preso em [0, 1].
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

    // Quatro lados inteiros, presos na faixa e não vazio; qualquer outra coisa vira desconhecida.
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

    // Inválida vira (0, 0).
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

    private static bool LerCoordenada(JsonElement? valor, string nome, List<string> avisos, out int coordenada)
    {
        coordenada = 0;
        if (valor is not { ValueKind: JsonValueKind.Number } numero || !numero.TryGetInt64(out long lida)) return false;
        coordenada = Coordenada(lida);
        if (coordenada != lida) avisos.Add($"{nome}: fora da faixa; presa ao limite");
        return true;
    }

    // Ausente vale o padrão sem aviso; inválido vale o padrão com aviso.
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
            ItensPorContaPropria = LerItensAdultos(campos[8], padrao.ItensPorContaPropria, avisos, "itensPorContaPropria", TabelaDoTamagotchi.Ilicitos),
        };
    }

    // Sem aceitos: os nove adultos. Com aceitos: só esses (as seis ilícitas, pro uso por conta
    // própria). Nome fora da lista ou repetido é ignorado com aviso.
    private static ConjuntoDeItens LerItensAdultos(JsonElement? valor, ConjuntoDeItens padrao, List<string> avisos,
        string campo = "itensAdultosHabilitados", ConjuntoDeItens? aceitos = null)
    {
        if (valor is null) return padrao;
        if (valor.Value.ValueKind != JsonValueKind.Array)
        {
            avisos.Add($"preferencias.{campo}: não é uma lista; valem os padrões");
            return padrao;
        }

        ConjuntoDeItens itens = ConjuntoDeItens.Vazio;
        foreach (JsonElement elemento in valor.Value.EnumerateArray())
        {
            if (elemento.ValueKind != JsonValueKind.String || !TentarLerItemAdulto(elemento.GetString()!, out Item item)
                || (aceitos is { } lista && !lista.Contem(item)))
            {
                avisos.Add($"preferencias.{campo}: identificador desconhecido ou inválido; ignorado");
                continue;
            }
            if (itens.Contem(item))
            {
                avisos.Add($"preferencias.{campo}: {NomeDoItemAdulto(item)} repetido; ignorado");
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
        preferencias = preferencias with
        {
            ItensAdultosHabilitados = Preferencias.NormalizarItensAdultos(preferencias.ItensAdultosHabilitados),
            ItensPorContaPropria = Preferencias.NormalizarPorContaPropria(preferencias.ItensPorContaPropria),
        };
        return preferencias;
    }

    // Sem caractere de controle e UTF-16 válido: surrogate solto não tem representação em JSON.
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

    // Tira o zero negativo, que sairia como -0 no arquivo.
    private static double Fracao(double fracao)
    {
        double saneada = Posicionador.SanearFracao(fracao);
        return saneada == 0 ? 0 : saneada;
    }

    private static int Coordenada(long valor) => (int)Math.Clamp(valor, CoordenadaMinima, CoordenadaMaxima);
}
