using System.Text;
using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Persistencia;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Testes de propriedade da máquina de estados (critérios 2 e 3 da Fase 2): milhares de
/// sequências aleatórias de eventos, sobre topologias aleatórias e configurações variadas,
/// conferindo a cada evento aplicado os invariantes de ARCHITECTURE.md 2.6 (inclusive o 18, da Fase 5:
/// toda posição gravada já é gravável, só sai dos eventos que gravam, leva a postura do estado depois do evento e volta
/// igual do settings.json),
/// a regra do relógio, as linhas de FULLSCREEN_TARGETS_CHANGED e as regras do núcleo R1, R6, R8, R9, R11
/// e R12 (Maquina.cs). Parte das sequências entrega os eventos em lotes de 1 a 4 por
/// <see cref="Nucleo.Processar"/>, como a raiz faz com rajadas; parte começa com pedidos
/// anteriores à carga; parte usa perfis de decisão e gestos curtos, para o piso do intervalo de
/// acomodação e o fim do gesto pelo relógio acontecerem. A semente é fixa; toda falha informa a
/// sequência, o lote, o evento e as opções da sequência, para virar teste.
///
/// Cada sequência roda também com a emoção dominante (DEC-027): comandos do menu entre os lotes e emoções na carga e
/// em SETTINGS_CHANGED, de um gerador próprio, sem mudar os sorteios do gerador principal nem as contagens da
/// execução sem emoção. Essa execução passa pelas mesmas conferências (com as contagens à parte), pelas da emoção (R1
/// da emoção e os ganchos da cara de base) e pelo invariante 27: evento a evento, ela só difere da execução sem emoção
/// nas caras e na própria emoção.
///
/// E cada sequência roda com o tamagotchi ligado (DEC-028), com os eventos dele inseridos por um gerador próprio (semente
/// × 37 + 11), também sem mudar os sorteios do gerador principal: invocar, segurar, arrastar até ele ou para longe, soltar,
/// largar, recolher, disparos da onda (o atual ou um velho) e, numa segunda execução, a emoção dominante (inclusive caras
/// de efeito, que são ignoradas). Os eventos da execução principal entram adaptados ao estado dela (a geração da agenda, o
/// sinal coerente e o PRESS no corpo). Essa execução passa pelas conferências de sempre, com o relógio pelo invariante 29, a
/// agenda pausada com o usuário segurando um item e o R11 pelo perfil efetivo da onda, e pelas do tamagotchi: os invariantes
/// 22 a 29 (desenho do núcleo, 4.11), com o alívio e a paranoia, conferidos a cada evento contra regras escritas aqui à
/// parte do núcleo. Em metade dessas sequências, pela semente, o gerador puxa a paranoia (misturas de substâncias com droga
/// sintética no mesmo episódio); em metade, por outro bit da semente, a agenda tem a ação do baseado por conta própria
/// (pedido do usuário de 2026-10-01, 19:10), com um peso maior que o do aplicativo, e o invariante 23 ajustado confere que ele
/// só fuma sozinho em IDLE no chão, sem Chapado nem paranoia na frente, sem item no mundo; nelas, o gerador o leva também a
/// IDLE fora do chão (sem a física, pelo pouso dos sinais) e abre o menu no meio do uso de um baseado. A execução principal
/// repetida com a chave ligada e sem eventos dele, e sem o baseado por conta própria, dá o mesmo registro (invariantes 7 e 22).
///
/// Cada conferência nova conta quantas vezes a situação dela apareceu (<see cref="CasosExigidos"/>):
/// uma conferência que o gerador nunca exercita não protege nada. Duas conferências ficam fora da
/// lista por serem inalcançáveis por construção: um AUTONOMY_TIMER da geração pendente com o painel
/// aberto (abrir o painel já cancela a agenda) e um evento autônomo em PRESSED ou DRAGGING (o
/// <see cref="Nucleo"/> o descarta antes da máquina); para elas, o que se confere é a causa: a agenda
/// sem temporizador com o painel aberto e o descarte pelo núcleo.
/// </summary>
internal static class InvariantesTestes
{
    private const int Semente = 20260929;
    private const int Sequencias = 2000;
    private const int EventosPorSequencia = 200;

    /// <summary>Chave que nenhuma topologia gerada tem (o gerador usa DISPLAY1 a DISPLAY9).</summary>
    private const string ChaveDesconhecida = @"\\.\DISPLAY42";

    /// <summary>Situações que o gerador precisa produzir pelo menos uma vez, para nenhuma conferência ficar vazia.</summary>
    private static readonly string[] CasosExigidos =
    [
        "evento aplicado num lote de 2 a 4", "evento antes da carga", "carga depois de pedidos de esconder", "carga repetida",
        "carga que mantém um pedido de esconder", "carga que mostra o personagem", "carga com posição salva conferida",
        "carga com posição salva em monitor inexistente", "carga restaurada pelo retângulo do monitor", "carga restaurada no monitor principal",
        "carga fora do enum", "SETTINGS_CHANGED fora do enum",
        "ENERGY_SELECTED fora do enum com o painel aberto", "expressão trocada sem transição",
        "DRAG_CANCEL do arraste com retorno", "CMD_SHOW com retorno", "fim da tela cheia sem retorno, visível",
        "agendamento no piso de um sorteio menor", "painel abriu ou fechou sem troca de estado", "DRAG_START com o painel aberto",
        "esconder ou sair com retorno guardado", "esconder ou sair no meio do arraste com retorno",
        "tela cheia com chave desconhecida", "tela cheia repetida", "tela cheia com o modo desligado", "tela cheia durante o gesto",
        "tela cheia transfere com mais de um monitor livre", "tela cheia esconde sem monitor livre",
        "tela cheia vazia restaura a posição anterior", "HIDDEN(POR_TELA_CHEIA) reaparece no livre mais próximo do retorno",
        "fim da tela cheia escondido por outro motivo, com retorno", "tela cheia não vazia escondido por outro motivo, com retorno",
        "fim do clique com retorno e tela cheia mudada", "fim do clique com retorno, sem mudança", "PRESS com a marca de um gesto interrompido",
        "desligar o modo com retorno, visível ou escondido pela tela cheia", "gesto terminou por TICK",
        "desligar o modo com retorno no meio do gesto", "fim do clique com retorno e o modo desligado no gesto",
        "CMD_SHOW escondido pela tela cheia, com retorno", "CMD_SHOW escondido por outro motivo no meio de um episódio",
        "TICK ou sinal de movimento com o usuário no controle", "AUTONOMY_TIMER descartado com o usuário no controle",
        "GRAVAR_POSICAO conferida (invariante 18)", "carga com a travessia desligada", "SETTINGS_CHANGED com a travessia desligada",
        // Cada um dos eventos que gravam a posição grava de fato (invariante 18).
        .. InvarianteDezoito.EventosQueGravam.Select(t => $"GRAVAR_POSICAO de {t.Name}"),
        // TOPOLOGY_CHANGED em execução (Fase 5, passo P8; invariantes 19 e 20): cada classe do monitor do personagem, os
        // estados que só guardam a posição e o CLICK que valida a partir dela.
        "TOPOLOGY_CHANGED sem mudar o monitor do personagem (invariante 19)", "TOPOLOGY_CHANGED que só translada o monitor do personagem (invariante 19)",
        "TOPOLOGY_CHANGED com o monitor do personagem mudado", "TOPOLOGY_CHANGED sem o monitor do personagem",
        "TOPOLOGY_CHANGED no gesto: só o cache e as posições", "TOPOLOGY_CHANGED escondido: só o cache e as posições",
        "TOPOLOGY_CHANGED com a mesma configuração", "retorno da tela cheia acompanhou a topologia", "invariante 20 conferido",
        "CLICK depois de o monitor mudar no gesto",
        // Revisão do bloco P6-P9 (achados 2, 3 e 6): as outras saídas de PRESSED partem do mesmo lugar que o CLICK, e o lugar do
        // arraste anda com o monitor em que está.
        "outra saída de PRESSED depois de o monitor mudar no gesto", "TOPOLOGY_CHANGED no arraste: o lugar anda com o monitor",
    ];

    /// <summary>Mínimo de atrasos distintos acima do piso: um atraso fixo (no mínimo do perfil, por exemplo) passaria na conferência de faixa.</summary>
    private const int AtrasosDistintosMinimos = 100;

    /// <summary>
    /// Situações da execução com a emoção dominante (DEC-027) que o gerador precisa produzir pelo menos uma vez. Elas
    /// são contadas à parte: as contagens da execução principal, sem emoção, continuam as de antes. O fim da reação
    /// (36 passos) e o fim do pouso (12) quase nunca chegam sem um evento que os interrompa, então ficam de fora da
    /// lista; a conferência deles vale quando acontecem, e os testes da emoção (EmocaoDominanteTestes) os exercitam.
    /// </summary>
    private static readonly string[] CasosExigidosDaEmocao =
    [
        "emoção escolhida", "emoção escolhida com a cara ocupada", "emoção automática escolhida", "emoção antes da carga ignorada",
        "emoção fora das 14 ignorada", "emoção igual à atual ignorada", "carga com emoção", "carga com emoção fora das 14",
        "SETTINGS_CHANGED com emoção nova", "SETTINGS_CHANGED com emoção fora das 14", "troca de cara com a dominante", "espiou com a dominante",
        "acordou com a dominante", "acordou na automática",
        "GRAVAR_POSICAO com a emoção escolhida (invariante 18)", "evento igual ao da execução sem emoção (invariante 27)",
    ];

    /// <summary>
    /// Situações da execução com o tamagotchi (DEC-028) que o gerador precisa produzir pelo menos uma vez: as do desenho
    /// do núcleo (5.2) e as de cada regra da crítica de integração (L4 a L6, C16 a C18). Contadas à parte; as da emoção, na
    /// execução com emoção.
    /// </summary>
    private static readonly string[] CasosExigidosDoTamagotchi =
    [
        "item usado", "item solto fora", "USING interrompido por PRESS", "onda avançou", "disparo de onda velho", "onda de fundo voltou",
        "água baixou a onda", "sétimo item", "uso até o fim", "uso interrompido sem PRESS", "soltar sobre ele recusado", "item largado",
        "atento: parou ou acordou", "item invocado ignorado", "relógio ligado só por um item caindo", "item assentado por ficar invisível (L5)",
        "item na mão sobre monitor ocupado (L4)", "recolher com um item na mão (L6)", "pegar outro item com um na mão (L6)", "esconder ou sair com um item na mão",
        "onda absorvida", "onda acumulada", "onda foi para o fundo", "itens reacomodados pela topologia", "ITEM_PRESS ignorado",
        "AUTONOMY_TIMER com o usuário segurando um item", "item caindo com o personagem pressionado ou arrastado",
        // Os itens seguem a regra do personagem numa mudança de topologia (Fase 5, passo P8), e USING é tratado como REACTING.
        "item transladado com o monitor", "item reacomodado pela posição acompanhada", "USING continua com o monitor só transladado ou igual",
        // O item na mão anda com o monitor em que está (revisão do bloco P6-P9, achado 6).
        "item na mão andou com o monitor",
        // O alívio (pedido do usuário de 2026-10-01): a comida e a bebida sem álcool com uma onda de substância na frente, e
        // a água com qualquer onda; cada desfecho de um passo; e o item de alívio com uma onda leve na frente, que combina como
        // sempre.
        "alívio com onda de substância na frente", "alívio baixou o nível", "alívio levou à queda", "alívio acabou a onda",
        "alívio acabou a onda e a de fundo voltou", "item de alívio com onda leve na frente",
        // A paranoia (outros pedidos do mesmo dia): só o uso que fecha um episódio de mistura com droga sintética sorteia, de 1
        // em 8, no gerador próprio dela, que às vezes sai e às vezes não; uma mistura sem sintética e uma sintética sozinha,
        // repetida, não sorteiam, e o episódio que já sorteou não sorteia de novo. Ela sobe com mais uma substância, o alívio a
        // acalma um passo, e a carga volta a 0 quando não sobra onda de substância; e o uso que a começou, até o fim, no chão,
        // termina com o olhar pro teto.
        "sorteio da paranoia saiu", "sorteio da paranoia não saiu", "mistura sem sintética não sorteou", "sintética sozinha repetida não sorteou",
        "episódio já sorteado não sorteou de novo",
        "paranoia começou", "paranoia subiu de nível", "paranoia acalmada por alívio", "carga zerada", "olhou pro teto no começo da paranoia",
        // O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10): a agenda o faz fumar, só em IDLE no chão, nunca
        // com a onda Chapado ou a paranoia na frente; o uso vai até o fim ou o clique o interrompe; num episódio com sintética
        // que ainda não sorteou, ele fecha a mistura e faz o sorteio do episódio; num episódio já sorteado, não sorteia.
        "fumou por conta própria", "não fumou chapado nem paranoico", "o clique interrompeu o baseado por conta própria",
        "baseado por conta própria até o fim", "baseado por conta própria fechou a mistura e sorteou", "baseado por conta própria num episódio já sorteado",
        // E (revisão do baseado, achado 4): em IDLE fora do chão, sem a física, ele não fuma; e o menu de contexto em USING,
        // inclusive no baseado por conta própria, não interrompe o uso.
        "IDLE fora do chão com a ação: não fumou", "menu de contexto em USING", "o menu não interrompeu o baseado por conta própria",
        // A chave do conteúdo adulto (DEC-033): desligada no meio do fluxo, com algo em curso, recusando item adulto e
        // terminando um uso adulto (invariante 30).
        "30: desligado no meio do fluxo", "30: desligado com algo do tamagotchi em curso", "30: item adulto recusado", "30: o uso adulto termina ao desligar",
        // A seleção individual (DEC-041): um item desmarcado com instância, uso ou carga dele em curso (invariante 36).
        "36: desmarcado com algo dele em curso",
    ];

    /// <summary>Situações da execução com o tamagotchi e a emoção dominante (as da emoção, com itens e ondas).</summary>
    private static readonly string[] CasosExigidosDoTamagotchiComEmocao =
    [
        "emoção escolhida", "emoção fora das 14 ignorada", "emoção escolhida com a cara ocupada", "emoção escolhida com a onda",
        "evento igual ao da execução sem emoção (invariante 27)",
    ];

    /// <summary>
    /// Uma sequência gerada: configuração, semente do núcleo e os eventos, em lotes. <paramref name="LotesComEmocao"/> são
    /// os mesmos lotes com a emoção dominante: comandos <see cref="CmdSetDominantEmotion"/> entre eles e uma emoção na
    /// carga e em SETTINGS_CHANGED, tudo de um gerador próprio, para não mudar os sorteios do gerador principal.
    /// <paramref name="AplicadosDoTamagotchi"/> é a execução com o tamagotchi ligado (<paramref name="ConfigDoTamagotchi"/>),
    /// feita pela sombra do gerador, lote a lote: os lotes da principal, adaptados, com os eventos dele entre eles; para cada
    /// evento aplicado, o lote, o estado logo antes e o resultado. <paramref name="LotesDoTamagotchiComEmocao"/> são os mesmos
    /// lotes com a emoção dominante, para uma segunda execução, comparada evento a evento com essa (invariantes 7 e 27).
    /// </summary>
    private sealed record Sequencia(
        ConfiguracaoDoNucleo Config, ulong SementeDoNucleo, List<List<Evento>> Lotes, bool EmLotes, bool AntesDaCarga, bool PerfilCurto,
        List<List<Evento>> LotesComEmocao,
        ConfiguracaoDoNucleo ConfigDoTamagotchi, List<(int Lote, EstadoDoNucleo Antes, Evento Evento, Resultado Resultado)> AplicadosDoTamagotchi,
        long DescartadosDoTamagotchi, List<List<Evento>> LotesDoTamagotchiComEmocao, bool UsoCurto, bool ComTamagotchi, bool PuxaAParanoia)
    {
        public string Opcoes => $"lotes {(EmLotes ? "de 1 a 4" : "de 1")}, {(AntesDaCarga ? "com" : "sem")} eventos antes da carga, perfil {(PerfilCurto ? "curto" : "padrão")}";

        public string OpcoesDoTamagotchi => $"{Opcoes}, uso {(UsoCurto ? "curto" : "da tabela")}{(PuxaAParanoia ? ", puxa a paranoia" : "")}";
    }

    /// <summary>
    /// O que as conferências do tamagotchi acompanham ao longo de uma execução: os Ids já vistos, as janelas dos itens
    /// (pelos efeitos), o uso em curso e os disparos da onda da frente.
    /// </summary>
    private sealed class RastroDoTamagotchi
    {
        public HashSet<int> IdsVistos { get; } = [];

        /// <summary>A janela de cada item, como os efeitos a deixaram: visível ou não, e onde.</summary>
        public Dictionary<int, (bool Visivel, Posicionamento Lugar)> Janelas { get; } = [];

        public int PassosNoUso { get; set; }

        public int PassosEsperados { get; set; }

        public bool PresoNoUso { get; set; }

        public LadoDoEsconderijo EsconderijoNoUso { get; set; }

        public PontoPx AncoraNoUso { get; set; }

        public int DisparosNoEpisodio { get; set; }

        public int LimiteDoEpisodio { get; set; }

        /// <summary>A carga da paranoia esperada, contada aqui à parte do núcleo: as substâncias usadas desde o último zero.</summary>
        public int Carga { get; set; }

        /// <summary>Se alguma substância do episódio era droga sintética, pela transcrição.</summary>
        public bool Sintetica { get; set; }

        /// <summary>Os itens de substância distintos do episódio, na ordem do enum.</summary>
        public SortedSet<Item> Distintas { get; } = [];

        /// <summary>Quantas vezes cada item entrou no episódio, para desfazer um item desmarcado (DEC-041).</summary>
        public Dictionary<Item, int> PorItem { get; } = [];

        /// <summary>
        /// Um item adulto desmarcado (DEC-041, item 3): sai da carga, dos distintos e da sintética; o sorteio feito fica feito.
        /// Sem nenhum item no episódio, a carga volta toda a zero, como no fim do episódio.
        /// </summary>
        public void TirarItem(Item item, Item[] sinteticos)
        {
            if (!PorItem.Remove(item, out int vezes)) return;
            Carga = Math.Max(0, Carga - vezes);
            Distintas.Remove(item);
            Sintetica = Distintas.Any(sinteticos.Contains);
            if (PorItem.Count == 0) ZerarACarga();
        }

        /// <summary>Se o episódio é uma mistura com sintética: uma sintética e pelo menos dois itens distintos.</summary>
        public bool MisturaComSintetica => Sintetica && Distintas.Count >= 2;

        /// <summary>Se o episódio já fez o sorteio da paranoia, o único dele.</summary>
        public bool Sorteada { get; set; }

        /// <summary>Se o uso em curso começou a paranoia, pela conta daqui.</summary>
        public bool UsoComecouAParanoia { get; set; }

        /// <summary>Se o uso em curso é o do baseado por conta própria, que veio da agenda, sem item.</summary>
        public bool UsoPorContaPropria { get; set; }

        /// <summary>O episódio acabou: a carga volta toda a zero, junto, com o sorteio feito.</summary>
        public void ZerarACarga()
        {
            Carga = 0;
            Sintetica = false;
            Distintas.Clear();
            PorItem.Clear();
            Sorteada = false;
        }

        /// <summary>A carga daqui, para as mensagens.</summary>
        public string DescreverACarga()
            => $"carga {Carga}, sintética {(Sintetica ? "sim" : "não")}, distintas [{string.Join(",", Distintas)}], sorteada {(Sorteada ? "sim" : "não")}";
    }

    /// <summary>O que as conferências acumulam entre eventos: quantas vezes cada situação apareceu e os atrasos sorteados.</summary>
    private sealed class Contagens
    {
        public SortedDictionary<string, long> Casos { get; } = new(StringComparer.Ordinal);

        public HashSet<TimeSpan> AtrasosAcimaDoPiso { get; } = [];

        public void Contar(string caso, long vezes = 1) => Casos[caso] = Casos.GetValueOrDefault(caso) + vezes;
    }

    [Teste]
    public static void InvariantesValemEmMilharesDeSequenciasAleatorias()
    {
        var mestre = new Random(Semente);
        var transicoesVistas = new HashSet<(Estado, Estado)>();
        long passos = 0, sequenciasEmLotes = 0, sequenciasAntesDaCarga = 0, sequenciasComPerfilCurto = 0, passosComEmocao = 0;
        long passosDoTamagotchi = 0, passosDoTamagotchiComEmocao = 0, sequenciasComTamagotchi = 0, sequenciasComUsoCurto = 0, sequenciasComEmocaoETamagotchi = 0;
        var contagens = new Contagens();
        var contagensDaEmocao = new Contagens();
        var contagensDoTamagotchi = new Contagens();
        var contagensDoTamagotchiComEmocao = new Contagens();
        var transicoesDoTamagotchi = new HashSet<(Estado, Estado)>();

        for (int n = 0; n < Sequencias; n++)
        {
            int sementeDaSequencia = mestre.Next();
            Sequencia seq = GerarSequencia(sementeDaSequencia, comTamagotchi: n % 2 == 0);
            if (seq.EmLotes) sequenciasEmLotes++;
            if (seq.AntesDaCarga) sequenciasAntesDaCarga++;
            if (seq.PerfilCurto) sequenciasComPerfilCurto++;

            // R-a: o estado antes de cada Aplicar vem do callback, também dentro de um lote.
            var nucleo = new Nucleo(seq.Config, seq.SementeDoNucleo);
            var registro = new List<string>();
            var aplicados = new List<(Evento Evento, Resultado Resultado)>();
            for (int l = 0; l < seq.Lotes.Count; l++)
            {
                int lote = l, aplicado = 0, tamanho = seq.Lotes[l].Count;
                foreach (Evento e in seq.Lotes[l]) nucleo.Enfileirar(e);
                EstadoDoNucleo anterior = nucleo.Estado;
                nucleo.Processar((evento, resultado) =>
                {
                    int i = aplicado++;
                    string Onde() => $"sequência {n} (semente {sementeDaSequencia}; {seq.Opcoes}), lote {lote}, {i + 1}º evento aplicado do lote, evento {evento}";
                    Conferir(seq.Config, anterior, evento, resultado, Onde, contagens);
                    if (tamanho > 1) contagens.Contar("evento aplicado num lote de 2 a 4");
                    foreach (Transicao t in resultado.Transicoes) transicoesVistas.Add((t.De, t.Para));
                    registro.Add(Registrar(evento, resultado));
                    aplicados.Add((evento, resultado));
                    anterior = resultado.Estado;
                    passos++;
                });
            }
            // Invariante 1 e ARCHITECTURE.md 2.3: o núcleo descarta o evento autônomo que chega com
            // o usuário no controle (a conferência de cada evento aplicado confirma que nenhum passou).
            contagens.Contar("AUTONOMY_TIMER descartado com o usuário no controle", nucleo.Descartados);

            // Invariante 7 (R-d): mesma semente e mesma sequência dão, evento a evento, o mesmo
            // retrato, as mesmas transições e os mesmos efeitos. A segunda execução tem o tamagotchi ligado, sem nenhum
            // evento dele: o invariante 22 pede exatamente o mesmo registro (sem itens nem onda, nada muda). A configuração da
            // sequência nunca tem a ação do baseado por conta própria (fora de Todas), que mudaria o sorteio da agenda.
            var outro = new Nucleo(seq.Config with { Tamagotchi = true }, seq.SementeDoNucleo);
            int k = 0;
            for (int l = 0; l < seq.Lotes.Count; l++)
            {
                int lote = l;
                foreach (Evento e in seq.Lotes[l]) outro.Enfileirar(e);
                outro.Processar((evento, resultado) =>
                {
                    string linha = Registrar(evento, resultado);
                    string primeira = k < registro.Count ? registro[k] : "(nada)";
                    if (linha != primeira)
                        Afirmar.Falhar($"invariantes 7 e 22: sequência {n} (semente {sementeDaSequencia}; {seq.Opcoes}), lote {lote}, evento aplicado {k}: primeira execução <{primeira}>, segunda, com o tamagotchi ligado <{linha}>");
                    k++;
                });
            }
            Afirmar.Igual(registro.Count, k, $"invariantes 7 e 22: sequência {n}: mesmo número de eventos aplicados");
            Afirmar.Igual(nucleo.Descartados, outro.Descartados, $"invariantes 7 e 22: sequência {n}: mesmos descartes");

            // Invariante 27 (DEC-027): a mesma sequência com a emoção dominante passa por todas as conferências (com
            // as contagens à parte) e pelas da emoção, e, evento a evento, só difere da execução sem emoção nas caras e
            // na própria emoção: mesmas transições, mesmos efeitos e o mesmo estado, inclusive o gerador
            // pseudoaleatório (cada sorteio de cara continua sendo um sorteio só).
            var comEmocao = new Nucleo(seq.Config, seq.SementeDoNucleo);
            int j = 0;
            for (int l = 0; l < seq.LotesComEmocao.Count; l++)
            {
                int lote = l;
                foreach (Evento e in seq.LotesComEmocao[l]) comEmocao.Enfileirar(e);
                EstadoDoNucleo anterior = comEmocao.Estado;
                comEmocao.Processar((evento, resultado) =>
                {
                    string Onde() => $"com a emoção dominante: sequência {n} (semente {sementeDaSequencia}; {seq.Opcoes}), lote com emoção {lote}, evento {evento}";
                    Conferir(seq.Config, anterior, evento, resultado, Onde, contagensDaEmocao);
                    ConferirEmocao(seq.Config, anterior, evento, resultado, Onde, contagensDaEmocao);
                    if (evento is not CmdSetDominantEmotion)
                    {
                        (Evento semEmocao, Resultado sem) = j < aplicados.Count ? aplicados[j] : (new Tick(), null!);
                        Verificar(j < aplicados.Count, () => $"invariante 27: {Onde()}: evento a mais que a execução sem emoção");
                        Verificar(SemEmocao(evento) == semEmocao, () => $"invariante 27: {Onde()}: na execução sem emoção, o {j}º evento aplicado foi {semEmocao}");
                        Verificar(resultado.Transicoes.SequenceEqual(sem.Transicoes),
                            () => $"invariante 27: {Onde()}: transições {Descrever(resultado.Transicoes)}; sem emoção, {Descrever(sem.Transicoes)}");
                        Verificar(resultado.Efeitos.Select(SemEmocao).SequenceEqual(sem.Efeitos),
                            () => $"invariante 27: {Onde()}: efeitos [{string.Join(", ", resultado.Efeitos.Select(Gravacao.DescreverEfeito))}]; sem emoção, [{string.Join(", ", sem.Efeitos.Select(Gravacao.DescreverEfeito))}]");
                        Verificar(SemAsCaras(resultado.Estado) == SemAsCaras(sem.Estado),
                            () => $"invariante 27: {Onde()}: estado {resultado.Estado.Retrato()} (gerador {resultado.Estado.Aleatorio}); sem emoção, {sem.Estado.Retrato()} (gerador {sem.Estado.Aleatorio})");
                        contagensDaEmocao.Contar("evento igual ao da execução sem emoção (invariante 27)");
                        j++;
                    }
                    anterior = resultado.Estado;
                    passosComEmocao++;
                });
            }
            Afirmar.Igual(aplicados.Count, j, $"invariante 27: sequência {n}: os mesmos eventos aplicados, além dos comandos de emoção");

            // O tamagotchi ligado (DEC-028), numa sequência em duas, para o teste caber no tempo da suíte: a execução da
            // sombra do gerador passa pelas conferências de sempre e pelas dele (invariantes 22 a 29).
            if (!seq.ComTamagotchi) continue;
            sequenciasComTamagotchi++;
            if (seq.UsoCurto) sequenciasComUsoCurto++;
            var rastro = new RastroDoTamagotchi();
            List<(int Lote, EstadoDoNucleo Antes, Evento Evento, Resultado Resultado)> aplicadosDoTamagotchi = seq.AplicadosDoTamagotchi;
            foreach ((int lote, EstadoDoNucleo anterior, Evento evento, Resultado resultado) in aplicadosDoTamagotchi)
            {
                string Onde() => $"com o tamagotchi: sequência {n} (semente {sementeDaSequencia}; {seq.OpcoesDoTamagotchi}), lote {lote}, evento {evento}";
                Conferir(seq.ConfigDoTamagotchi, anterior, evento, resultado, Onde, contagensDoTamagotchi);
                ConferirTamagotchi(seq.ConfigDoTamagotchi, anterior, evento, resultado, Onde, contagensDoTamagotchi, rastro);
                foreach (Transicao t in resultado.Transicoes) transicoesDoTamagotchi.Add((t.De, t.Para));
                passosDoTamagotchi++;
            }
            contagensDoTamagotchi.Contar("AUTONOMY_TIMER descartado com o usuário no controle", seq.DescartadosDoTamagotchi);

            // Invariantes 7 e 27 com o tamagotchi: uma segunda execução, com a emoção dominante, só difere da primeira nas
            // caras e na emoção. Ela roda numa sequência em quatro, para o teste caber no tempo da suíte.
            if (n % 4 != 0) continue;
            sequenciasComEmocaoETamagotchi++;
            var tamagotchiComEmocao = new Nucleo(seq.ConfigDoTamagotchi, seq.SementeDoNucleo);
            var rastroComEmocao = new RastroDoTamagotchi();
            int jt = 0;
            for (int l = 0; l < seq.LotesDoTamagotchiComEmocao.Count; l++)
            {
                int lote = l;
                foreach (Evento e in seq.LotesDoTamagotchiComEmocao[l]) tamagotchiComEmocao.Enfileirar(e);
                EstadoDoNucleo anterior = tamagotchiComEmocao.Estado;
                tamagotchiComEmocao.Processar((evento, resultado) =>
                {
                    string Onde() => $"com o tamagotchi e a emoção dominante: sequência {n} (semente {sementeDaSequencia}; {seq.OpcoesDoTamagotchi}), lote com emoção {lote}, evento {evento}";
                    Conferir(seq.ConfigDoTamagotchi, anterior, evento, resultado, Onde, contagensDoTamagotchiComEmocao);
                    ConferirTamagotchi(seq.ConfigDoTamagotchi, anterior, evento, resultado, Onde, contagensDoTamagotchiComEmocao, rastroComEmocao);
                    ConferirEmocao(seq.ConfigDoTamagotchi, anterior, evento, resultado, Onde, contagensDoTamagotchiComEmocao);
                    if (evento is not CmdSetDominantEmotion)
                    {
                        Verificar(jt < aplicadosDoTamagotchi.Count, () => $"invariante 27: {Onde()}: evento a mais que a execução sem emoção");
                        (_, _, Evento semEmocao, Resultado sem) = aplicadosDoTamagotchi[jt];
                        Verificar(SemEmocao(evento) == semEmocao, () => $"invariante 27: {Onde()}: na execução sem emoção, o {jt}º evento aplicado foi {semEmocao}");
                        Verificar(resultado.Transicoes.SequenceEqual(sem.Transicoes),
                            () => $"invariante 27: {Onde()}: transições {Descrever(resultado.Transicoes)}; sem emoção, {Descrever(sem.Transicoes)}");
                        Verificar(resultado.Efeitos.Select(SemEmocao).SequenceEqual(sem.Efeitos),
                            () => $"invariante 27: {Onde()}: efeitos [{string.Join(", ", resultado.Efeitos.Select(Gravacao.DescreverEfeito))}]; sem emoção, [{string.Join(", ", sem.Efeitos.Select(Gravacao.DescreverEfeito))}]");
                        Verificar(SemAsCaras(resultado.Estado) == SemAsCaras(sem.Estado),
                            () => $"invariante 27: {Onde()}: estado {resultado.Estado.Retrato()} (gerador {resultado.Estado.Aleatorio}); sem emoção, {sem.Estado.Retrato()} (gerador {sem.Estado.Aleatorio})");
                        contagensDoTamagotchiComEmocao.Contar("evento igual ao da execução sem emoção (invariante 27)");
                        jt++;
                    }
                    anterior = resultado.Estado;
                    passosDoTamagotchiComEmocao++;
                });
            }
            Afirmar.Igual(aplicadosDoTamagotchi.Count, jt, $"invariante 27 com o tamagotchi: sequência {n}: os mesmos eventos aplicados, além dos comandos de emoção");
        }

        Console.WriteLine($"         {Sequencias} sequências ({sequenciasEmLotes} em lotes, {sequenciasAntesDaCarga} com eventos antes da carga, {sequenciasComPerfilCurto} com perfil curto), {passos} eventos aplicados, {transicoesVistas.Count} pares de transição distintos, {contagens.AtrasosAcimaDoPiso.Count} atrasos distintos acima do piso");
        Console.WriteLine("         casos: " + string.Join(", ", contagens.Casos.Select(c => $"{c.Key}={c.Value}")));
        string[] daEmocao = [.. CasosExigidosDaEmocao, "fim da reação com a dominante", "fim do pouso com a dominante"];
        Console.WriteLine($"         com a emoção dominante: {passosComEmocao} eventos aplicados; casos: "
            + string.Join(", ", contagensDaEmocao.Casos.Where(c => daEmocao.Contains(c.Key)).Select(c => $"{c.Key}={c.Value}")));
        Console.WriteLine($"         com o tamagotchi: {sequenciasComTamagotchi} sequências ({sequenciasComUsoCurto} com o uso curto), {passosDoTamagotchi} eventos aplicados, {transicoesDoTamagotchi.Count} pares de transição distintos; casos: "
            + string.Join(", ", contagensDoTamagotchi.Casos.Where(c => CasosExigidosDoTamagotchi.Contains(c.Key)).Select(c => $"{c.Key}={c.Value}")));
        string[] daEmocaoComTamagotchi = [.. CasosExigidosDoTamagotchiComEmocao, "acordou com a dominante", "fim do uso com a dominante", "troca de cara com a onda"];
        Console.WriteLine($"         com o tamagotchi e a emoção dominante: {sequenciasComEmocaoETamagotchi} sequências, {passosDoTamagotchiComEmocao} eventos aplicados; casos: "
            + string.Join(", ", contagensDoTamagotchiComEmocao.Casos.Where(c => daEmocaoComTamagotchi.Contains(c.Key)).Select(c => $"{c.Key}={c.Value}")));
        foreach (string caso in CasosExigidos)
            Afirmar.Verdadeiro(contagens.Casos.GetValueOrDefault(caso) > 0, $"o gerador não exercitou \"{caso}\": a conferência correspondente ficou vazia");
        foreach (string caso in CasosExigidosDaEmocao)
            Afirmar.Verdadeiro(contagensDaEmocao.Casos.GetValueOrDefault(caso) > 0, $"o gerador não exercitou \"{caso}\" com a emoção dominante: a conferência correspondente ficou vazia");
        foreach (string caso in CasosExigidosDoTamagotchi)
            Afirmar.Verdadeiro(contagensDoTamagotchi.Casos.GetValueOrDefault(caso) > 0, $"o gerador não exercitou \"{caso}\" com o tamagotchi: a conferência correspondente ficou vazia");
        foreach (string caso in CasosExigidosDoTamagotchiComEmocao)
            Afirmar.Verdadeiro(contagensDoTamagotchiComEmocao.Casos.GetValueOrDefault(caso) > 0, $"o gerador não exercitou \"{caso}\" com o tamagotchi e a emoção dominante: a conferência correspondente ficou vazia");

        // R11: o atraso é sorteado na faixa do perfil, não fixado nela.
        Afirmar.Verdadeiro(contagens.AtrasosAcimaDoPiso.Count >= AtrasosDistintosMinimos,
            $"R11: só {contagens.AtrasosAcimaDoPiso.Count} atrasos distintos acima do piso; esperado ao menos {AtrasosDistintosMinimos}");

        // Invariante 11: todo estado tem entrada e saída; BOOTING só saída, EXITING só entrada. USING (DEC-028) só existe
        // com o tamagotchi: entram as transições das duas execuções.
        HashSet<(Estado, Estado)> todas = [.. transicoesVistas, .. transicoesDoTamagotchi];
        foreach (Estado e in Enum.GetValues<Estado>())
        {
            bool entra = todas.Any(t => t.Item2 == e && t.Item1 != e);
            bool sai = todas.Any(t => t.Item1 == e && t.Item2 != e);
            if (e == Estado.Booting)
            {
                Afirmar.Falso(entra, "BOOTING não tem entrada");
                Afirmar.Verdadeiro(sai, "BOOTING tem saída");
            }
            else if (e == Estado.Exiting)
            {
                Afirmar.Verdadeiro(entra, "EXITING tem entrada");
                Afirmar.Falso(sai, "EXITING não tem saída");
            }
            else
            {
                Afirmar.Verdadeiro(entra && sai, $"{e}: entrada={entra}, saída={sai}");
            }
        }
    }

    /// <summary>Confere um evento aplicado: <paramref name="antes"/> é o estado logo antes do Aplicar.</summary>
    private static void Conferir(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, Evento evento, Resultado r, Func<string> onde, Contagens contagens)
    {
        void Contar(string caso) => contagens.Contar(caso);
        EstadoDoNucleo depois = r.Estado;
        IReadOnlyList<Efeito> efeitos = r.Efeitos;
        IReadOnlyList<Transicao> transicoes = r.Transicoes;
        PontoPx? ancoraAntes = antes.Lugar?.Ancora;
        PontoPx? ancoraDepois = depois.Lugar?.Ancora;
        bool mesmoEstadoEPosicao = antes.Estado == depois.Estado && ancoraAntes == ancoraDepois;

        // EXITING é final: nenhum evento muda o estado nem produz efeito.
        if (antes.Estado == Estado.Exiting)
            Verificar(transicoes.Count == 0 && efeitos.Count == 0 && depois == antes, () => $"EXITING: {onde()}: o evento teve efeito ({Descrever(transicoes)}; {efeitos.Count} efeitos)");

        // Invariante 1: em PRESSED e DRAGGING (SETTLING nunca sobra entre dois eventos), o relógio
        // e o movimento não mudam estado nem posição; um evento autônomo nem chega à máquina.
        if (antes.Estado is Estado.Pressed or Estado.Dragging && evento.Origem == Origem.Relogio)
        {
            Contar("TICK ou sinal de movimento com o usuário no controle");
            Verificar(mesmoEstadoEPosicao, () => $"invariante 1: {onde()}: {antes.Estado}→{depois.Estado}, {ancoraAntes}→{ancoraDepois}");
        }
        if (evento.Origem == Origem.Autonomo)
            Verificar(!antes.Estado.ControladoPeloUsuario(), () => $"invariante 1: {onde()}: evento autônomo chegou à máquina em {antes.Estado}; o núcleo devia descartá-lo");

        // Invariante 2: em DRAGGING, posição = cursor − pegada.
        if (depois.Estado == Estado.Dragging && evento is DragMove m)
            Verificar(ancoraDepois == new PontoPx(m.Cursor.X - depois.Pegada.X, m.Cursor.Y - depois.Pegada.Y), () => $"invariante 2: {onde()}: âncora {ancoraDepois}");

        // Invariante 5: depois de SETTLING, a âncora está na área útil de um monitor presente.
        if (transicoes.Any(t => t.De == Estado.Settling) && depois.Topologia is { } topologia && ancoraDepois is { } a)
            Verificar(NaAreaUtil(topologia, a), () => $"invariante 5: {onde()}: âncora {a} fora de toda área útil de {topologia}");

        // Invariante 21 (Fase 5, passo P13; DEC-032): com a física, à vista e fora de PRESSED, DRAGGING, REACTING, do esconderijo
        // (DEC-025, de propósito atrás da borda) e de uma travessia em curso, o sprite fica inteiro na área útil do monitor da
        // âncora, quando cabe nela; no meio de uma travessia, inteiro na união das áreas úteis.
        if (cfg.Movimento && depois.Estado.Visivel() && depois.Lugar is { } lugar21 && depois.Topologia is { } t21
            && depois.Estado is not (Estado.Pressed or Estado.Dragging or Estado.Reacting or Estado.Peeking) && depois.Esconderijo == LadoDoEsconderijo.Nenhum)
        {
            RetanguloPx area21 = lugar21.Monitor.AreaUtil;
            if (depois.Movimento.Travessia is not null)
            {
                Contar("21: no meio de uma travessia");
                Verificar(Passagens.NaUniaoDasAreasUteis(t21, lugar21.Retangulo), () => $"invariante 21: {onde()}: atravessando com o sprite {lugar21.Retangulo} fora da união das áreas úteis");
            }
            else if (lugar21.Tamanho.Largura <= area21.Largura && lugar21.Tamanho.Altura <= area21.Altura)
            {
                Verificar(area21.Contem(lugar21.Retangulo), () => $"invariante 21: {onde()}: {depois.Estado} com o sprite {lugar21.Retangulo} fora da área útil {area21} do monitor da âncora");
            }
        }

        // Invariante 6: trocar expressão não muda estado nem posição.
        if (evento is ExpressionChange x)
        {
            Verificar(mesmoEstadoEPosicao, () => $"invariante 6: {onde()}: {antes.Estado}→{depois.Estado}, {ancoraAntes}→{ancoraDepois}");
            Verificar(antes.Estado == Estado.Exiting || depois.Expressao == x.Expressao, () => $"invariante 6: {onde()}: expressão {depois.Expressao}");
        }

        // Invariante 6 generalizado (R-c): toda troca de expressão sem transição, venha do evento que
        // vier (clique duplo, decisão autônoma, troca pedida, emoção dominante), mantém estado e âncora. A
        // carga fica de fora: ela define a posição de quem ainda não tinha uma e, com a emoção dominante
        // gravada, a cara de partida (DEC-027), sem transição quando um pedido de esconder anterior a ela
        // continua valendo (R6, conferida em ConferirCarga).
        if (antes.Expressao != depois.Expressao && transicoes.Count == 0 && evento is not Loaded)
        {
            Contar("expressão trocada sem transição");
            Verificar(mesmoEstadoEPosicao, () => $"invariante 6: {onde()}: expressão {antes.Expressao}→{depois.Expressao} com {antes.Estado}→{depois.Estado}, {ancoraAntes}→{ancoraDepois}");
        }

        // Invariante 8: abrir ou fechar o painel nunca muda a posição.
        if (evento is EnergyPanelOpen or EnergyPanelClose)
            Verificar(mesmoEstadoEPosicao, () => $"invariante 8: {onde()}: {antes.Estado}→{depois.Estado}, {ancoraAntes}→{ancoraDepois}");

        // Invariante 9: iniciar o arraste fecha o painel; soltar não muda a energia.
        if (evento is DragStart && depois.Estado == Estado.Dragging)
            Verificar(!depois.PainelAberto, () => $"invariante 9: {onde()}: painel aberto no arraste");
        if (evento is DragStart or DragMove or DragEnd or DragCancel)
            Verificar(antes.Preferencias.Energia == depois.Preferencias.Energia, () => $"invariante 9: {onde()}: energia mudou");

        // Invariante 10: desbloquear ou retomar nunca mostra o que o usuário escondeu.
        if (evento is SessionUnlocked or Resumed && antes.Estado == Estado.Hidden && antes.Motivo == MotivoDoOcultamento.PorUsuario)
            Verificar(depois.Estado == Estado.Hidden && depois.Motivo == MotivoDoOcultamento.PorUsuario, () => $"invariante 10: {onde()}: {depois.Estado}/{depois.Motivo}");

        // Linhas de FULLSCREEN_TARGETS_CHANGED, com R12 e a marca de R9 (invariante 14 incluído).
        if (evento is FullscreenTargetsChanged f)
            ConferirTelaCheia(antes, f, r, onde, contagens);

        // TOPOLOGY_CHANGED em execução (Fase 5, passo P8): as posições acompanham a topologia, e os invariantes 19 e 20.
        if (evento is TopologyChanged mudanca)
            ConferirTopologia(antes, mudanca, r, onde, contagens, cfg.Tamanho);

        // Toda saída de PRESSED (CLICK, DOUBLE_CLICK, DRAG_CANCEL e DRAG_START) depois de o monitor mudar com o botão
        // pressionado (P8, R14; revisão do bloco P6-P9, achados 2 e 3) parte de onde ele estaria parado: a posição que
        // acompanhou a topologia, na mesma posição relativa, no monitor da chave dela ou, sem ele, no mais próximo do pixel dos
        // pés da âncora acompanhada. Toda saída fica nesse monitor; o CLICK fica exatamente nesse lugar, e as frações continuam
        // descrevendo o lugar validado: a partida seguinte o restaura ali. Com um retorno da tela cheia guardado, o fim do
        // gesto pode levá-lo de volta à posição anterior (regra R9 acima); fica de fora.
        if (evento is Click or DoubleClick or DragCancel or DragStart && antes.Estado == Estado.Pressed && antes.Lugar is { } noGesto
            && antes.Topologia is { } topologiaDaSaida && !Equals(topologiaDaSaida.PorChave(noGesto.Monitor.Chave), noGesto.Monitor)
            && antes.RetornoDaTelaCheia is null && antes.Posicao is { } acompanhou)
        {
            PontoPx acompanhada = acompanhou.AncoraAbsoluta;
            MonitorDoDesktop alvo = topologiaDaSaida.PorChave(acompanhou.ChaveMonitor) ?? topologiaDaSaida.MonitorMaisProximo(new PontoPx(acompanhada.X, acompanhada.Y - 1));
            Verificar(depois.Lugar?.Monitor.Chave == alvo.Chave,
                () => $"R14: {onde()}: a saída de PRESSED devia partir de {Descrever(acompanhou)} em {alvo.Chave}; ficou em {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora}");
            if (evento is Click)
            {
                Contar("CLICK depois de o monitor mudar no gesto");
                PontoPx validada = Posicionador.NoMonitor(alvo, acompanhou.FracaoX, acompanhou.FracaoY, cfg.Tamanho).Ancora;
                if (!cfg.QuedaFisica) validada = validada with { Y = alvo.AreaUtil.Base };
                Verificar(depois.Lugar is { } validado && validado.Monitor == alvo && validado.Ancora == validada,
                    () => $"R14: {onde()}: o CLICK devia validar {Descrever(acompanhou)} em {alvo.Chave} {validada}; ficou em {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora}");
                PosicaoDoPersonagem? descrita = depois.Posicao;
                MonitorDoDesktop? daPosicao = descrita is null ? null : topologiaDaSaida.PorChave(descrita.ChaveMonitor);
                Verificar(descrita is not null && daPosicao is not null && descrita.TelaDoMonitor == daPosicao.Tela && depois.Lugar is { } lugarDoClique
                        && Posicionador.NoMonitor(daPosicao, descrita.FracaoX, descrita.FracaoY, cfg.Tamanho).Ancora == lugarDoClique.Ancora,
                    () => $"R14: {onde()}: as frações de {Descrever(descrita)} (tela {descrita?.TelaDoMonitor}) não descrevem o lugar validado {depois.Lugar?.Ancora}");
            }
            else
            {
                Contar("outra saída de PRESSED depois de o monitor mudar no gesto");
            }
        }
        if (evento is DragEnd && antes.Estado == Estado.Dragging)
            Verificar(depois.RetornoDaTelaCheia is null, () => $"invariante 14: {onde()}: retorno temporário sobreviveu ao arraste");

        // Invariante 14 estendido (R-g, regras R7 e R9): cancelar o arraste e mostrar manualmente
        // também são escolha do usuário e descartam o retorno.
        if (evento is DragCancel && antes.Estado == Estado.Dragging)
        {
            if (antes.RetornoDaTelaCheia is not null) Contar("DRAG_CANCEL do arraste com retorno");
            Verificar(depois.RetornoDaTelaCheia is null, () => $"invariante 14: {onde()}: retorno temporário sobreviveu ao DRAG_CANCEL do arraste");
        }
        if (evento is CmdShow && antes.Estado is not (Estado.Booting or Estado.Exiting))
        {
            if (antes.RetornoDaTelaCheia is not null) Contar("CMD_SHOW com retorno");
            Verificar(depois.RetornoDaTelaCheia is null, () => $"invariante 14: {onde()}: retorno temporário sobreviveu ao CMD_SHOW em {antes.Estado}");
        }
        // Linhas HIDDEN(...) | CMD_SHOW: depois da carga, aparece na posição anterior validada — a de
        // antes da tela cheia, se foi ela que o escondeu; onde estava, se foi outro motivo — sem
        // reaplicar o modo (é escolha do usuário, DEC-020).
        if (evento is CmdShow && antes.Estado == Estado.Hidden && antes.Carregado && antes.Topologia is { } topologiaAoMostrar)
        {
            PosicaoDoPersonagem? anterior = antes.Motivo == MotivoDoOcultamento.PorTelaCheia ? antes.RetornoDaTelaCheia ?? antes.Posicao : antes.Posicao;
            if (antes.Motivo == MotivoDoOcultamento.PorTelaCheia && antes.RetornoDaTelaCheia is not null) Contar("CMD_SHOW escondido pela tela cheia, com retorno");
            if (antes.Motivo != MotivoDoOcultamento.PorTelaCheia && antes.RetornoDaTelaCheia is not null) Contar("CMD_SHOW escondido por outro motivo no meio de um episódio");
            Posicionamento esperado = anterior is null ? Posicionador.Inicial(topologiaAoMostrar, cfg.Tamanho) : Posicionador.Reacomodar(topologiaAoMostrar, anterior, cfg.Tamanho).Resultado;
            if (antes.Esconderijo != LadoDoEsconderijo.Nenhum)
            {
                // Escondido na borda (DEC-025): reaparece no esconderijo, na mesma borda do mesmo monitor.
                Contar("CMD_SHOW de quem estava escondido na borda");
                Verificar(depois.Estado == Estado.Peeking && depois.Esconderijo == antes.Esconderijo && depois.Lugar is { } escondido && escondido.Monitor.Chave == esperado.Monitor.Chave,
                    () => $"CMD_SHOW: {onde()}: escondido na borda {antes.Esconderijo} devia voltar ao esconderijo em {esperado.Monitor.Chave}; obtido {depois.Estado} ({depois.Esconderijo}) em {depois.Lugar?.Monitor.Chave}");
            }
            else
            {
                Verificar(depois.Estado.Visivel() && depois.Lugar is { } mostrado && mostrado.Monitor.Chave == esperado.Monitor.Chave && mostrado.Ancora.X == esperado.Ancora.X,
                    () => $"CMD_SHOW: {onde()}: de HIDDEN({antes.Motivo}) devia aparecer em {esperado.Monitor.Chave} {esperado.Ancora}; obtido {depois.Estado} em {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora}");
            }
        }

        // R9: um clique, clique duplo ou cancelamento em PRESSED só descarta o retorno se a tela cheia
        // mudou durante o gesto (o ponto do usuário vale); sem mudança, o retorno fica, ou, se o modo
        // foi desligado no meio do gesto, o personagem volta à posição anterior (linha SETTINGS_CHANGED
        // que desliga o modo). E um gesto novo começa sem a marca do anterior.
        if (antes.Estado == Estado.Pressed && evento is Click or DoubleClick or DragCancel && antes.RetornoDaTelaCheia is { } retornoNoGesto)
        {
            if (antes.TelaCheiaMudouNoGesto)
            {
                Contar("fim do clique com retorno e tela cheia mudada");
                Verificar(depois.RetornoDaTelaCheia is null, () => $"R9: {onde()}: a tela cheia mudou no gesto e o retorno sobreviveu");
            }
            else if (antes.Preferencias.ModoTelaCheia)
            {
                Contar("fim do clique com retorno, sem mudança");
                Verificar(Equals(depois.RetornoDaTelaCheia, retornoNoGesto), () => $"R9: {onde()}: sem mudança de tela cheia no gesto, o retorno sumiu");
            }
            else if (antes.Topologia is { } topologiaDoGesto)
            {
                Contar("fim do clique com retorno e o modo desligado no gesto");
                (Posicionamento anterior, _) = Posicionador.Reacomodar(topologiaDoGesto, retornoNoGesto, cfg.Tamanho);
                Verificar(depois.RetornoDaTelaCheia is null && depois.Lugar is { } lugar && lugar.Monitor.Chave == anterior.Monitor.Chave && lugar.Ancora.X == anterior.Ancora.X,
                    () => $"SETTINGS_CHANGED desligou o modo no gesto: {onde()}: o fim do clique devia levar à posição anterior {anterior.Monitor.Chave} {anterior.Ancora} sem retorno; obtido {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
            }
        }
        if (evento is Press && transicoes.Any(t => t.Para == Estado.Pressed))
        {
            if (antes.TelaCheiaMudouNoGesto) Contar("PRESS com a marca de um gesto interrompido");
            Verificar(!depois.TelaCheiaMudouNoGesto, () => $"R9: {onde()}: o gesto novo começou com a marca de mudança de tela cheia de outro gesto");
        }

        // Invariante 15: gesto curto não muda estado nem posição e termina com prioridade maior.
        if (antes.Gesto != Gesto.Nenhum && depois.Gesto == Gesto.Nenhum && evento is Tick)
        {
            Contar("gesto terminou por TICK");
            Verificar(mesmoEstadoEPosicao, () => $"invariante 15: {onde()}: o fim do gesto mudou {antes.Estado}→{depois.Estado}");
        }
        if (antes.Gesto != Gesto.Nenhum && evento.Origem >= Origem.Sistema)
            Verificar(depois.Gesto == Gesto.Nenhum, () => $"invariante 15: {onde()}: gesto {depois.Gesto} sobreviveu");
        if (depois.Gesto != Gesto.Nenhum)
            Verificar(depois.Estado == Estado.Idle, () => $"invariante 15: {onde()}: gesto fora de IDLE ({depois.Estado})");

        // Critério 3 e invariante 29: o relógio corre se e somente se o personagem se move (sem estar agarrado), reage,
        // usa um item (DEC-028), faz um gesto em IDLE ou há um item à vista caindo.
        bool agarrado = depois.Estado is Estado.Climbing or Estado.Hanging && depois.Movimento.Agarrado;
        bool itemCaindo = cfg.Tamagotchi && depois.Itens.Todos.Any(i => i.Situacao == SituacaoDoItem.Caindo && VeOItem(depois, i));
        bool pelaAnimacao = (depois.Estado.EmMovimento() && !agarrado) || depois.Estado is Estado.Reacting or Estado.Using || (depois.Estado == Estado.Idle && depois.Gesto != Gesto.Nenhum);
        bool precisa = pelaAnimacao || itemCaindo;
        if (itemCaindo && !pelaAnimacao) Contar("relógio ligado só por um item caindo");
        Verificar(depois.RelogioAtivo == precisa, () => $"critério 3 e invariante 29: {onde()}: relógio={depois.RelogioAtivo} em {depois.Estado} com gesto {depois.Gesto}, item caindo à vista {itemCaindo}");
        Efeito? ultimoDoRelogio = efeitos.LastOrDefault(e => e is LigarRelogio or DesligarRelogio);
        if (antes.RelogioAtivo != depois.RelogioAtivo)
            Verificar(ultimoDoRelogio is not null && ultimoDoRelogio is LigarRelogio == depois.RelogioAtivo, () => $"critério 3: {onde()}: o relógio mudou sem o efeito certo");
        else
            Verificar(ultimoDoRelogio is null, () => $"critério 3: {onde()}: efeito do relógio sem mudança");

        // Critério 3 afirmado diretamente (R-b): sem relógio em IDLE sem gesto, RESTING, HIDDEN,
        // PRESSED, DRAGGING, BOOTING e EXITING, a não ser por um item à vista caindo (L8 da crítica).
        if ((depois.Estado == Estado.Idle && depois.Gesto == Gesto.Nenhum)
            || depois.Estado is Estado.Resting or Estado.Hidden or Estado.Pressed or Estado.Dragging or Estado.Booting or Estado.Exiting)
        {
            if (itemCaindo && depois.Estado is Estado.Pressed or Estado.Dragging) Contar("item caindo com o personagem pressionado ou arrastado");
            Verificar(!depois.RelogioAtivo || itemCaindo, () => $"critério 3: {onde()}: relógio ligado em {depois.Estado} (gesto {depois.Gesto}) sem item caindo");
        }

        // Agenda: um temporizador só nos estados que decidem, com a autonomia livre e sem o usuário segurando um item
        // (DEC-028); e, nessas condições, sempre um (a agenda não para sozinha, nem depois de um gesto curto).
        bool segurandoItem = cfg.Tamagotchi && depois.Atento;
        bool autonomiaLivre = Maquina.DecideNoEstado(depois.Estado) && !depois.AutonomiaPausada && !depois.PainelAberto && depois.Gesto == Gesto.Nenhum && !segurandoItem;
        Verificar(depois.DecisaoAgendada == autonomiaLivre,
            () => $"agenda: {onde()}: temporizador pendente={depois.DecisaoAgendada} em {depois.Estado} (pausada {depois.AutonomiaPausada}, painel {depois.PainelAberto}, gesto {depois.Gesto}, segurando um item {segurandoItem})");

        // R11 (R-h): todo agendamento respeita o piso do intervalo de acomodação e a faixa do perfil
        // em vigor: o do nível de energia, com a onda de um item aplicada (DEC-028, perfil efetivo); descanso em RESTING,
        // decisão nos demais.
        AgendarDecisao[] agendas = [.. efeitos.OfType<AgendarDecisao>()];
        Verificar(agendas.Length <= 1, () => $"agenda: {onde()}: {agendas.Length} agendamentos num só evento");
        foreach (AgendarDecisao agenda in agendas)
        {
            TimeSpan piso = cfg.IntervaloDeAcomodacao;
            PerfilDeEnergia perfil = Maquina.PerfilEfetivo(depois, cfg);
            (TimeSpan minimo, TimeSpan maximo) = depois.Estado == Estado.Resting
                ? (perfil.DescansoMinimo, perfil.DescansoMaximo)
                : (perfil.DecisaoMinima, perfil.DecisaoMaxima);
            if (agenda.Atraso == piso && minimo < piso) Contar("agendamento no piso de um sorteio menor");
            if (agenda.Atraso > piso) contagens.AtrasosAcimaDoPiso.Add(agenda.Atraso);
            Verificar(agenda.Atraso >= piso, () => $"R11: {onde()}: atraso {agenda.Atraso} abaixo do intervalo de acomodação {piso}");
            Verificar(agenda.Atraso >= Maior(minimo, piso) && agenda.Atraso <= Maior(maximo, piso),
                () => $"R11: {onde()}: atraso {agenda.Atraso} fora da faixa {minimo}–{maximo} (piso {piso}) do perfil {perfil.Nivel} em {depois.Estado}");
            Verificar(depois.DecisaoAgendada && agenda.Geracao == depois.Geracao, () => $"agenda: {onde()}: geração {agenda.Geracao} agendada, estado com {depois.Geracao} (pendente {depois.DecisaoAgendada})");
        }

        // Janela: mostrar e esconder acompanham a visibilidade.
        if (depois.Estado != Estado.Exiting)
        {
            bool mostrou = efeitos.Any(e => e is MostrarJanela), escondeu = efeitos.Any(e => e is EsconderJanela);
            Verificar(mostrou == (!antes.Estado.Visivel() && depois.Estado.Visivel()), () => $"janela: {onde()}: mostrar={mostrou} de {antes.Estado} para {depois.Estado}");
            Verificar(escondeu == (antes.Estado.Visivel() && !depois.Estado.Visivel()), () => $"janela: {onde()}: esconder={escondeu} de {antes.Estado} para {depois.Estado}");
        }

        // R6 (R-e): antes da carga o personagem nunca aparece; a primeira carga vale, com a topologia,
        // as preferências e a posição dela; as seguintes são ignoradas.
        if (!depois.Carregado)
        {
            Contar("evento antes da carga");
            Verificar(!depois.Estado.Visivel(), () => $"R6: {onde()}: visível ({depois.Estado}) antes da carga");
        }
        if (evento is Loaded carga)
            ConferirCarga(cfg, antes, carga, r, onde, contagens);

        // R1 (R-e): a energia é sempre um dos três níveis; ENERGY_SELECTED fora deles é ignorado, sem
        // gravar; SETTINGS_CHANGED fora deles vira Média.
        Verificar(Enum.IsDefined(depois.Preferencias.Energia), () => $"R1: {onde()}: energia fora do enum: {(int)depois.Preferencias.Energia}");
        if (evento is EnergySelected { Nivel: var nivel } && !Enum.IsDefined(nivel))
        {
            if (antes.PainelAberto) Contar("ENERGY_SELECTED fora do enum com o painel aberto");
            Verificar(antes.Preferencias == depois.Preferencias && !efeitos.Any(e => e is GravarPreferencias), () => $"R1: {onde()}: ENERGY_SELECTED({(int)nivel}) não foi ignorado");
        }
        if (evento is SettingsChanged configuracao && antes.Estado != Estado.Exiting)
            ConferirConfiguracoes(cfg, antes, configuracao, r, onde, contagens);
        // DEC-020: com o modo desligado, o retorno temporário não sobra fora de um gesto.
        if (!depois.Preferencias.ModoTelaCheia && depois.Estado is not (Estado.Pressed or Estado.Dragging))
            Verificar(depois.RetornoDaTelaCheia is null, () => $"modo de tela cheia desligado: {onde()}: retorno {Gravacao.DescreverPosicao(depois.RetornoDaTelaCheia!)} sobrou em {depois.Estado}");

        // Painel (R-f). Invariante 4: com o painel aberto, AUTONOMY_TIMER não produz transição,
        // gesto nem expressão, e nenhuma transição entra num estado autônomo em movimento ou em
        // RESTING, exceto pelo próprio movimento (MovementSignal: a física em curso termina; o
        // comportamento calmo de CLIMBING/HANGING com o painel aberto é da Fase 4). O primeiro caso
        // nunca acha uma decisão pendente (abrir o painel cancela a agenda, conferido acima em
        // "agenda"): fica como rede de segurança, sem contar como cobertura.
        if (evento is AutonomyTimer && antes.PainelAberto && depois.PainelAberto)
        {
            Verificar(transicoes.Count == 0 && depois.Gesto == antes.Gesto && depois.Expressao == antes.Expressao,
                () => $"invariante 4: {onde()}: decisão autônoma com o painel aberto ({Descrever(transicoes)}, gesto {depois.Gesto}, expressão {depois.Expressao})");
        }
        // Começar é entrar no estado: a transição de um estado para ele mesmo, como a que registra a escolha da
        // emoção dominante (DEC-027), não começa comportamento.
        if (antes.PainelAberto && depois.PainelAberto && evento is not MovementSignal)
            Verificar(!transicoes.Any(t => t.De != t.Para && t.Para is Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Resting),
                () => $"invariante 4: {onde()}: comportamento autônomo começou com o painel aberto ({Descrever(transicoes)})");
        // Invariante 8 em todo passo em que o painel abre ou fecha sem troca de estado.
        if (antes.PainelAberto != depois.PainelAberto && antes.Estado == depois.Estado)
        {
            Contar("painel abriu ou fechou sem troca de estado");
            Verificar(ancoraAntes == ancoraDepois, () => $"invariante 8: {onde()}: o painel {(depois.PainelAberto ? "abriu" : "fechou")} e a âncora foi de {ancoraAntes} a {ancoraDepois}");
        }
        // Invariante 9: iniciar o arraste com o painel aberto pede para fechá-lo.
        if (evento is DragStart && antes.PainelAberto && depois.Estado == Estado.Dragging)
        {
            Contar("DRAG_START com o painel aberto");
            Verificar(efeitos.Any(e => e is FecharPainelDeEnergia), () => $"invariante 9: {onde()}: arraste começou sem FecharPainelDeEnergia");
        }
        // Painel só existe com o personagem visível.
        if (!depois.Estado.Visivel())
            Verificar(!depois.PainelAberto, () => $"painel: {onde()}: aberto em {depois.Estado}");

        ConferirGravacaoAoEsconderOuSair(antes, evento, r, onde, contagens);
        ConferirGravacaoDaPosicao(evento, r, onde, contagens);

        // Com o tamagotchi desligado, não há carga da paranoia nem sorteio dela (invariante 22).
        if (!cfg.Tamagotchi)
        {
            Verificar(depois.Carga == CargaDaParanoia.Nenhuma, () => $"invariante 22: {onde()}: carga {depois.Carga} com o tamagotchi desligado");
            Verificar(depois.AleatorioDaParanoia == antes.AleatorioDaParanoia, () => $"invariante 22: {onde()}: o gerador da paranoia mudou com o tamagotchi desligado");
        }
    }

    /// <summary>
    /// Invariante 18 (Fase 5; <see cref="InvarianteDezoito"/>): todo GravarPosicao sai só de soltar, cancelar o
    /// arraste, redefinir, esconder, bloquear, suspender ou sair, nunca do relógio, do movimento, da agenda, da
    /// carga, da topologia, da tela cheia nem das preferências (não há gravação periódica, DEC-011); traz uma
    /// posição já gravável (chave, frações em [0, 1] e a tela do monitor da época) e a postura do estado depois do
    /// evento (a borda do esconderijo e a marca de preso, esquema v3); e volta igual do settings.json. No máximo um por
    /// evento.
    /// </summary>
    private static void ConferirGravacaoDaPosicao(Evento evento, Resultado r, Func<string> onde, Contagens contagens)
    {
        GravarPosicao[] gravadas = [.. r.Efeitos.OfType<GravarPosicao>()];
        Verificar(gravadas.Length <= 1, () => $"invariante 18: {onde()}: {gravadas.Length} GravarPosicao num só evento");
        foreach (GravarPosicao g in gravadas)
        {
            contagens.Contar("GRAVAR_POSICAO conferida (invariante 18)");
            contagens.Contar($"GRAVAR_POSICAO de {evento.GetType().Name}");
            string? violacao = InvarianteDezoito.Violacao(evento, g, r.Estado);
            Verificar(violacao is null, () => $"invariante 18: {onde()}: {violacao}");
        }
    }

    /// <summary>
    /// R1 e invariante 27 para a emoção dominante (DEC-027). Ela é sempre nula ou uma das 14 caras de humor.
    /// CMD_SET_DOMINANT_EMOTION antes da carga, fora das 14 ou igual à atual é ignorado: nada muda e nada é gravado.
    /// Senão, ele só troca a preferência (e, com a cara livre, a cara), grava com um GravarPreferencias e nenhum outro
    /// efeito e registra uma transição para o mesmo estado. A carga e SETTINGS_CHANGED com uma emoção de humor nova a
    /// mostram na hora (fora das 14, valem a automática: conferido com as preferências saneadas). Com a dominante, o fim
    /// da reação, o fim do pouso e o acordar voltam a ela, e as trocas de cara da agenda, no IDLE e escondido, só
    /// sorteiam a dominante e as companheiras; na automática, ele acorda neutro, como antes. Com o tamagotchi (DEC-028),
    /// a cara também espera o fim do uso de um item, e a da onda tem precedência: com onda, a cara de base é a da fase, e
    /// as trocas sorteiam as caras da fase.
    /// </summary>
    private static void ConferirEmocao(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, Evento evento, Resultado r, Func<string> onde, Contagens contagens)
    {
        EstadoDoNucleo depois = r.Estado;
        Expressao? dominante = depois.Preferencias.EmocaoDominante;
        bool ondaAntes = cfg.Tamagotchi && antes.Onda is not null;
        // A cara de base depois do evento: a da fase da onda; sem onda, a dominante; na automática, a neutra.
        Expressao? DaOnda(EstadoDoNucleo s) => cfg.Tamagotchi && s.Onda is { } o ? cfg.TabelaDeOndas(o.Tipo).Cara(o.Fase) : null;
        Expressao cdeBase = DaOnda(depois) ?? dominante ?? Expressao.Neutro;
        Verificar(dominante is not { } fora || DeHumor(fora), () => $"R1: {onde()}: emoção dominante fora das 14 caras de humor: {(int)dominante!.Value}");
        if (dominante is not null && r.Efeitos.Any(e => e is GravarPosicao)) contagens.Contar("GRAVAR_POSICAO com a emoção escolhida (invariante 18)");

        if (evento is CmdSetDominantEmotion { Emocao: var emocao } && antes.Estado != Estado.Exiting)
        {
            string? ignorado = !antes.Carregado ? "emoção antes da carga ignorada"
                : emocao is { } invalida && !DeHumor(invalida) ? "emoção fora das 14 ignorada"
                : emocao == antes.Preferencias.EmocaoDominante ? "emoção igual à atual ignorada"
                : null;
            if (ignorado is not null)
            {
                contagens.Contar(ignorado);
                Verificar(r.Transicoes.Count == 0 && r.Efeitos.Count == 0 && depois == antes with { Sinal = Sinal.Nenhum },
                    () => $"R1: {onde()}: {ignorado}, mas houve [{Descrever(r.Transicoes)}], {r.Efeitos.Count} efeito(s), {antes.Retrato()} → {depois.Retrato()}");
                return;
            }
            bool caraLivre = antes.Estado is not (Estado.Resting or Estado.Reacting or Estado.Using);
            contagens.Contar(emocao is null ? "emoção automática escolhida" : !caraLivre ? "emoção escolhida com a cara ocupada" : ondaAntes ? "emoção escolhida com a onda" : "emoção escolhida");
            Expressao cara = emocao is { } nova && caraLivre && !ondaAntes ? nova : antes.Expressao;
            Verificar(depois == antes with { Sinal = Sinal.Nenhum, Expressao = cara, Preferencias = antes.Preferencias with { EmocaoDominante = emocao } },
                () => $"CMD_SET_DOMINANT_EMOTION: {onde()}: devia mudar só a preferência ({emocao}) e a cara ({cara}): {antes.Retrato()} → {depois.Retrato()}");
            Verificar(r.Efeitos.Count == 1 && r.Efeitos[0] is GravarPreferencias g && g.Preferencias == depois.Preferencias,
                () => $"CMD_SET_DOMINANT_EMOTION: {onde()}: efeitos [{string.Join(", ", r.Efeitos.Select(Gravacao.DescreverEfeito))}], esperado só GravarPreferencias");
            Verificar(r.Transicoes.Count == 1 && r.Transicoes[0].De == antes.Estado && r.Transicoes[0].Para == antes.Estado,
                () => $"CMD_SET_DOMINANT_EMOTION: {onde()}: transições [{Descrever(r.Transicoes)}], esperada uma de {antes.Estado} para ele mesmo");
            return;
        }

        if (evento is Loaded carga && !antes.Carregado && antes.Estado != Estado.Exiting && carga.Preferencias.EmocaoDominante is { } naCarga)
        {
            contagens.Contar(DeHumor(naCarga) ? "carga com emoção" : "carga com emoção fora das 14");
            if (DeHumor(naCarga))
                Verificar(depois.Expressao == naCarga, () => $"carga: {onde()}: começou com a cara {depois.Expressao}, não com a emoção {naCarga}");
        }
        if (evento is SettingsChanged configuracoes && antes.Estado != Estado.Exiting && configuracoes.Preferencias.EmocaoDominante is { } emocaoNova)
        {
            if (!DeHumor(emocaoNova))
            {
                contagens.Contar("SETTINGS_CHANGED com emoção fora das 14");
            }
            else if (antes.Carregado && emocaoNova != antes.Preferencias.EmocaoDominante && antes.Estado is not (Estado.Resting or Estado.Reacting or Estado.Using) && !ondaAntes)
            {
                contagens.Contar("SETTINGS_CHANGED com emoção nova");
                Verificar(depois.Expressao == emocaoNova, () => $"SETTINGS_CHANGED: {onde()}: a cara ficou {depois.Expressao}, não a emoção nova {emocaoNova}");
            }
        }

        bool acordou = evento is AutonomyTimer && antes.Estado == Estado.Resting && depois.Sinal == Sinal.Acordou;
        bool fimDoUso = evento is Tick && r.Transicoes.FirstOrDefault()?.Regra.StartsWith("USING: fim do uso", StringComparison.Ordinal) == true;
        if (dominante is not { } d)
        {
            if (acordou)
            {
                contagens.Contar("acordou na automática");
                Verificar(depois.Expressao == cdeBase, () => $"acordar: {onde()}: na automática, acordou {depois.Expressao}, e não {cdeBase} (neutro, ou a cara da onda)");
            }
            if (fimDoUso && DaOnda(depois) is { } daOnda)
                Verificar(depois.Expressao == daOnda, () => $"fim do uso: {onde()}: com a onda, a cara ficou {depois.Expressao}, e não a da fase, {daOnda}");
            return;
        }
        string? gancho = acordou ? "acordou com a dominante"
            : evento is Tick && r.Transicoes.FirstOrDefault()?.Regra == "REACTING: fim da reação" ? "fim da reação com a dominante"
            : evento is Tick && r.Transicoes.FirstOrDefault()?.Regra == "LANDING: fim do pouso" ? "fim do pouso com a dominante"
            : fimDoUso ? "fim do uso com a dominante"
            : null;
        if (gancho is not null)
        {
            contagens.Contar(gancho);
            Verificar(depois.Expressao == cdeBase, () => $"{gancho}: {onde()}: a cara ficou {depois.Expressao}, e não a de base {cdeBase} (a dominante {d}, ou a cara da onda)");
        }
        bool trocouNoIdle = evento is AutonomyTimer && antes.Estado == Estado.Idle && depois.Estado == Estado.Idle && r.Transicoes.Count == 0
            && depois.Gesto == Gesto.Nenhum && depois.Expressao != antes.Expressao;
        bool espiou = evento is AutonomyTimer && r.Transicoes.Any(t => t.Regra == "PEEKING + AUTONOMY_TIMER: espia com outra cara");
        if (trocouNoIdle || espiou)
        {
            if (Maquina.PerfilDaFase(depois, cfg) is { } fase)
            {
                // A onda tem precedência (DEC-028): as trocas sorteiam as caras da fase.
                contagens.Contar("troca de cara com a onda");
                Verificar(fase.Caras.Any(c => c.Cara == depois.Expressao), () => $"troca de cara: {onde()}: com a onda, sorteou {depois.Expressao}, que não é uma cara da fase");
                return;
            }
            contagens.Contar(trocouNoIdle ? "troca de cara com a dominante" : "espiou com a dominante");
            Verificar(depois.Expressao == d || Expressoes.Companheiras(d).Contains(depois.Expressao),
                () => $"{(trocouNoIdle ? "troca de cara" : "esconderijo")}: {onde()}: sorteou {depois.Expressao}, que não é a dominante {d} nem uma companheira dela");
        }
    }

    /// <summary>
    /// Os invariantes do tamagotchi (DEC-028; desenho do núcleo, 4.11, com a crítica de integração), conferidos a cada
    /// evento aplicado com a chave ligada, contra regras escritas aqui, à parte do núcleo:
    /// <list type="bullet">
    /// <item>23: um item só nasce por CMD_SUMMON_ITEM, com o próximo Id, nunca repetido; só sai usado (ITEM_DRAG_END sobre
    /// ele, num estado que aceita), recolhido ou substituído (o sétimo); só se entra em USING assim ou pela ação autônoma do
    /// baseado por conta própria, só o baseado, só em IDLE no chão, sem Chapado nem paranoia na frente, sem item no mundo
    /// (<see cref="ConferirOBaseadoPorContaPropria"/>);</item>
    /// <item>24: em USING, PRESS leva a PRESSED no mesmo evento; o uso dura exatamente os passos do item e sai por SETTLING
    /// para o mesmo apoio (o chão ou o esconderijo, sem a física), mantendo o preso e o esconderijo; interrompido, só o uso
    /// acaba, e a onda continua;</item>
    /// <item>25: com onda (fora de EXITING), exatamente um disparo pendente, de 1 s ou mais; sem onda, nenhum; níveis de 1 a
    /// 3, a queda no nível 1, no máximo uma onda de fundo, de outro tipo; a combinação (4.5), com o alívio, e o avanço seguem
    /// a tabela; e a onda da frente acaba em no máximo 2 + nível disparos sem item novo;</item>
    /// <item>o alívio (pedido do usuário de 2026-10-01): a água, e a comida e a bebida sem álcool com uma onda de substância
    /// na frente, nunca sobem o nível, nunca alongam a fase em curso (só o nível caiu: o mesmo disparo pendente; a queda que
    /// começa: a duração cheia dela), nunca tocam a onda de fundo (que só volta, se a da frente acabou), nunca começam a
    /// onda do item e nunca sorteiam (o gerador fica o mesmo);</item>
    /// <item>a paranoia (outros pedidos do mesmo dia): a carga, contada aqui (as substâncias desde o último zero, se alguma
    /// era sintética, os itens distintos, pela transcrição, e se o episódio já sorteou; tudo volta a 0 junto no fim de todo
    /// evento sem onda de substância), é a do núcleo; depois da combinação, com a paranoia na frente, mais uma substância a
    /// sobe um nível, sem sorteio; sem ela, só o uso que fecha um episódio de mistura com sintética sorteia, uma vez por
    /// episódio, com um passo do gerador da paranoia, calculado aqui sobre o valor cru dele, e, saindo, ela começa na frente
    /// (a frente vai para o fundo); fora desse sorteio, o gerador da paranoia nunca muda, e o soltar nunca muda o gerador
    /// principal; ela só começa assim, só existe num episódio de mistura com sintética que já sorteou e nunca fica no fundo;
    /// e o uso que a começou, até o fim, com ele de volta a IDLE e ela na frente, termina com o olhar pro teto de 90 passos,
    /// sem sorteio (nenhum outro fim de uso traz gesto);</item>
    /// <item>26: a física em vigor só muda as três velocidades, entre 50% e 200%;</item>
    /// <item>28: no máximo <see cref="ConfiguracaoDoNucleo.MaximoDeItens"/> itens; fora da mão, o sprite inteiro na área útil
    /// do monitor dele, presente, e os pés no chão quando parado; a janela de cada item (pelos efeitos) aparece se e
    /// somente se ele é visível (na mão, sempre: L4), no lugar dele; nenhum item invisível caindo (L5);</item>
    /// <item>a captura de um item que sai da mão sem o gesto dele acabar é solta (L6); segurar um item deixa o personagem
    /// atento: andando, para; descansando, acorda; sem sair do lugar; e nenhuma decisão autônoma acontece enquanto isso.</item>
    /// </list>
    /// O invariante 29 (o relógio) fica em <see cref="Conferir"/>, para todas as execuções; o 22, na execução principal
    /// repetida com a chave ligada; o 27, na comparação com a execução com a emoção.
    /// </summary>
    private static void ConferirTamagotchi(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, Evento evento, Resultado r, Func<string> onde, Contagens contagens, RastroDoTamagotchi rastro)
    {
        void Contar(string caso) => contagens.Contar(caso);
        if (antes.Estado == Estado.Exiting) return;
        EstadoDoNucleo depois = r.Estado;
        IReadOnlyList<Transicao> transicoes = r.Transicoes;

        // ------------------------------------------------ 30: com o conteúdo adulto desligado, nada adulto (DEC-033)
        if (!depois.Preferencias.ConteudoAdulto)
        {
            Verificar(depois.Itens.Todos.All(i => cfg.TabelaDeItens(i.Item).Alivio), () => $"invariante 30: {onde()}: item adulto no mundo com a chave desligada ({depois.Itens})");
            Verificar(!(depois.Onda is { } frente && cfg.TabelaDeOndas(frente.Tipo).DeSubstancia) && !(depois.OndaDeFundo is { } fundo && cfg.TabelaDeOndas(fundo.Tipo).DeSubstancia),
                () => $"invariante 30: {onde()}: onda de substância com a chave desligada ({depois.Onda}, fundo {depois.OndaDeFundo})");
            Verificar(depois.Uso is not { } uso || cfg.TabelaDeItens(uso.Item).Alivio, () => $"invariante 30: {onde()}: usando {depois.Uso?.Item} com a chave desligada");
            if (evento is CmdSetAdultContent { Ligado: false } && antes.Preferencias.ConteudoAdulto) Contar("30: desligado no meio do fluxo");
            if (evento is CmdSetAdultContent { Ligado: false } && (antes.Itens.Todos.Any(i => !cfg.TabelaDeItens(i.Item).Alivio) || antes.Onda is not null || antes.Uso is not null))
                Contar("30: desligado com algo do tamagotchi em curso");
        }

        // ------------------------------------------------ 36: um item adulto desmarcado não está no mundo nem em uso (DEC-041)
        if (depois.Carregado)
        {
            Verificar(depois.Itens.Todos.All(i => cfg.TabelaDeItens(i.Item).Alivio || depois.Preferencias.ItensAdultosHabilitados.Contem(i.Item)),
                () => $"invariante 36: {onde()}: item desmarcado no mundo ({depois.Itens}; marcados {depois.Preferencias.ItensAdultosHabilitados})");
            Verificar(depois.Uso is not { } emUso || cfg.TabelaDeItens(emUso.Item).Alivio || depois.Preferencias.ItensAdultosHabilitados.Contem(emUso.Item),
                () => $"invariante 36: {onde()}: usando {depois.Uso?.Item}, desmarcado");
            Verificar(depois.Carga.PorItem.Total == depois.Carga.Substancias
                && Preferencias.TodosOsItensAdultos.Itens.All(i => (depois.Carga.PorItem.Contagem(i) > 0) == depois.Carga.Distintas.Contem(i)),
                () => $"invariante 36: {onde()}: a proveniência da carga não bate ({depois.Carga})");
            if (evento is CmdSetAdultItemEnabled { Ligado: false } d && antes.Preferencias.ItensAdultosHabilitados.Contem(d.Item)
                && (antes.Itens.Todos.Any(i => i.Item == d.Item) || antes.Uso?.Item == d.Item || antes.Carga.Distintas.Contem(d.Item)))
                Contar("36: desmarcado com algo dele em curso");
        }

        // ------------------------------------------------ 28: os itens no mundo e as janelas deles
        Verificar(depois.Itens.Quantidade <= cfg.MaximoDeItens, () => $"invariante 28: {onde()}: {depois.Itens.Quantidade} itens");
        foreach (ItemNoMundo item in depois.Itens.Todos)
        {
            TamanhoPx tamanho = cfg.TamanhoDoItem.ParaPixels(item.Lugar.Monitor.Dpi);
            Verificar(item.Lugar.Tamanho == tamanho && item.Lugar.Retangulo == Posicionador.RetanguloDoSprite(item.Lugar.Ancora, tamanho),
                () => $"invariante 28: {onde()}: o item {item.Id} com o tamanho {item.Lugar.Tamanho} e o retângulo {item.Lugar.Retangulo}");
            if (item.Situacao == SituacaoDoItem.Caindo)
                Verificar(VeOItem(depois, item), () => $"L5: {onde()}: o item {item.Id} cai sem aparecer (em {item.Lugar.Monitor.Chave}, com o personagem em {depois.Estado})");
            if (item.NaMao) continue;
            MonitorDoDesktop? monitor = depois.Topologia?.PorChave(item.Lugar.Monitor.Chave);
            Verificar(monitor is not null && monitor == item.Lugar.Monitor, () => $"invariante 28: {onde()}: o item {item.Id} num monitor que não é o da topologia ({item.Lugar.Monitor})");
            RetanguloPx area = item.Lugar.Monitor.AreaUtil;
            if (tamanho.Largura <= area.Largura && tamanho.Altura <= area.Altura)
                Verificar(area.Contem(item.Lugar.Retangulo), () => $"invariante 28: {onde()}: o item {item.Id} ({item.Lugar.Retangulo}) fora da área útil {area}");
            if (item.Situacao == SituacaoDoItem.NoChao)
                Verificar(item.Lugar.Ancora.Y == area.Base, () => $"invariante 28: {onde()}: o item {item.Id} parado fora do chão ({item.Lugar.Ancora}, chão {area.Base})");
        }
        if (depois.Estado != Estado.Exiting)
        {
            foreach (Efeito efeito in r.Efeitos)
            {
                switch (efeito)
                {
                    case MostrarItem m:
                        Verificar(!rastro.Janelas.TryGetValue(m.Id, out var antesDeMostrar) || !antesDeMostrar.Visivel, () => $"janelas: {onde()}: mostrou a janela já visível do item {m.Id}");
                        rastro.Janelas[m.Id] = (true, m.Lugar);
                        break;
                    case MoverItem m:
                        Verificar(rastro.Janelas.TryGetValue(m.Id, out var antesDeMover) && antesDeMover.Visivel, () => $"janelas: {onde()}: moveu a janela escondida do item {m.Id}");
                        rastro.Janelas[m.Id] = (true, m.Lugar);
                        break;
                    case EsconderItem e:
                        Verificar(rastro.Janelas.TryGetValue(e.Id, out var antesDeEsconder) && antesDeEsconder.Visivel, () => $"janelas: {onde()}: escondeu a janela não visível do item {e.Id}");
                        rastro.Janelas[e.Id] = (false, antesDeEsconder.Lugar);
                        break;
                    case RemoverItem e:
                        Verificar(antes.Itens.PorId(e.Id) is not null && depois.Itens.PorId(e.Id) is null, () => $"janelas: {onde()}: removeu o item {e.Id}, que não saiu");
                        rastro.Janelas.Remove(e.Id);
                        break;
                }
            }
            foreach (ItemNoMundo item in depois.Itens.Todos)
            {
                bool visivel = VeOItem(depois, item);
                bool janela = rastro.Janelas.TryGetValue(item.Id, out var j) && j.Visivel;
                Verificar(janela == visivel, () => $"invariante 28 e L4: {onde()}: a janela do item {item.Id} {(janela ? "aparece" : "não aparece")}, e ele {(visivel ? "é" : "não é")} visível ({item.Situacao}, em {item.Lugar.Monitor.Chave}, personagem em {depois.Estado})");
                if (visivel) Verificar(j.Lugar == item.Lugar, () => $"janelas: {onde()}: a janela do item {item.Id} em {j.Lugar?.Ancora}, e ele em {item.Lugar.Ancora}");
                if (item.NaMao && visivel && depois.Preferencias.ModoTelaCheia && depois.Ocupados.Contem(item.Lugar.Monitor.Chave)) Contar("item na mão sobre monitor ocupado (L4)");
            }
            Verificar(rastro.Janelas.Keys.All(id => depois.Itens.PorId(id) is not null), () => $"janelas: {onde()}: sobrou a janela de um item que saiu");
        }
        else
        {
            // Saindo, a raiz fecha todas as janelas: nenhum efeito de janela de item.
            Verificar(!r.Efeitos.Any(e => e is MostrarItem or MoverItem or EsconderItem or RemoverItem), () => $"janelas: {onde()}: efeito de janela de item ao sair");
        }

        // ------------------------------------------------ 23: nascer e sair
        int[] novos = [.. depois.Itens.Todos.Select(i => i.Id).Where(id => antes.Itens.PorId(id) is null)];
        int[] sairam = [.. antes.Itens.Todos.Select(i => i.Id).Where(id => depois.Itens.PorId(id) is null)];
        if (novos.Length > 0)
        {
            Verificar(evento is CmdSummonItem && novos.Length == 1 && novos[0] == antes.ProximoIdDeItem && depois.ProximoIdDeItem == antes.ProximoIdDeItem + 1,
                () => $"invariante 23: {onde()}: os itens [{string.Join(",", novos)}] nasceram (o próximo Id era {antes.ProximoIdDeItem})");
            Verificar(rastro.IdsVistos.Add(novos[0]), () => $"invariante 23: {onde()}: o Id {novos[0]} se repetiu");
        }
        else
        {
            Verificar(depois.ProximoIdDeItem == antes.ProximoIdDeItem, () => $"invariante 23: {onde()}: o próximo Id mudou sem item novo");
        }
        RemoverItem[] remocoes = [.. r.Efeitos.OfType<RemoverItem>()];
        if (depois.Estado != Estado.Exiting)
            Verificar(remocoes.Select(e => e.Id).Order().SequenceEqual(sairam.Order()), () => $"invariante 23: {onde()}: saíram [{string.Join(",", sairam)}], removidos [{string.Join(",", remocoes.Select(e => e.Id))}]");
        foreach (RemoverItem remocao in remocoes)
        {
            MotivoDaRemocao esperado = evento switch
            {
                ItemDragEnd => MotivoDaRemocao.Usado,
                CmdClearItems => MotivoDaRemocao.Recolhido,
                // Desligar o conteúdo adulto (DEC-033) recolhe só os itens adultos.
                CmdSetAdultContent { Ligado: false } or SettingsChanged { Preferencias.ConteudoAdulto: false } when !cfg.TabelaDeItens(antes.Itens.PorId(remocao.Id)!.Item).Alivio => MotivoDaRemocao.Recolhido,
                // Desmarcar um item adulto (DEC-041) recolhe só as instâncias dele.
                CmdSetAdultItemEnabled { Ligado: false } desmarcado when antes.Itens.PorId(remocao.Id)!.Item == desmarcado.Item => MotivoDaRemocao.Recolhido,
                SettingsChanged mudancaDosItens when !cfg.TabelaDeItens(antes.Itens.PorId(remocao.Id)!.Item).Alivio
                    && !mudancaDosItens.Preferencias.ItensAdultosHabilitados.Contem(antes.Itens.PorId(remocao.Id)!.Item) => MotivoDaRemocao.Recolhido,
                CmdSummonItem => MotivoDaRemocao.Substituido,
                _ => (MotivoDaRemocao)(-1),
            };
            Verificar(remocao.Motivo == esperado, () => $"invariante 23: {onde()}: o item {remocao.Id} saiu como {remocao.Motivo} com {evento.GetType().Name}");
        }
        if (evento is CmdClearItems) Verificar(depois.Itens.Quantidade == 0, () => $"recolher: {onde()}: sobraram {depois.Itens.Quantidade} itens");

        // A invocação.
        if (evento is CmdSummonItem invocacao)
        {
            bool aceita = antes.Carregado && antes.Estado.Visivel() && Enum.IsDefined(invocacao.Item) && antes.Lugar is not null;
            // Com o conteúdo adulto desligado (DEC-033), um item adulto também é ignorado; e, com ele ligado, um item adulto
            // desmarcado individualmente (DEC-041).
            if (aceita && !antes.Preferencias.ConteudoAdulto && !cfg.TabelaDeItens(invocacao.Item).Alivio)
            {
                aceita = false;
                Contar("30: item adulto recusado");
            }
            if (aceita && !cfg.TabelaDeItens(invocacao.Item).Alivio && !antes.Preferencias.ItensAdultosHabilitados.Contem(invocacao.Item))
            {
                aceita = false;
                Contar("DEC-041: item adulto desmarcado recusado");
            }
            if (!aceita)
            {
                Contar("item invocado ignorado");
                Verificar(novos.Length == 0 && sairam.Length == 0, () => $"invariante 23: {onde()}: a invocação devia ser ignorada em {antes.Estado} (carregado {antes.Carregado}, item {(int)invocacao.Item})");
            }
            else
            {
                Verificar(novos.Length == 1, () => $"invariante 23: {onde()}: a invocação não fez o item nascer");
                ItemNoMundo novo = depois.Itens.PorId(novos[0])!;
                Verificar(novo.Item == invocacao.Item, () => $"invariante 23: {onde()}: nasceu {novo.Item}");
                ConferirNascimento(cfg, antes, novo, onde);
                if (antes.Itens.Quantidade >= cfg.MaximoDeItens)
                {
                    Contar("sétimo item");
                    int maisAntigo = antes.Itens.Todos.First(i => !i.NaMao).Id;
                    Verificar(sairam.SequenceEqual([maisAntigo]), () => $"D17: {onde()}: saíram [{string.Join(",", sairam)}], esperado o mais antigo fora da mão, {maisAntigo}");
                }
                else
                {
                    Verificar(sairam.Length == 0, () => $"D17: {onde()}: saiu um item sem precisar");
                }
            }
        }

        // ------------------------------------------------ segurar, arrastar, soltar e largar
        ItemNoMundo? naMaoAntes = antes.Itens.NaMao;
        if (evento is ItemPress press)
        {
            ItemNoMundo? alvo = antes.Itens.PorId(press.Id);
            if (alvo is null || !VeOItem(antes, alvo) || antes.Topologia is null)
            {
                Contar("ITEM_PRESS ignorado");
                Verificar(depois.Itens == antes.Itens, () => $"ITEM_PRESS: {onde()}: um item que não existe ou não se vê foi pego");
            }
            else
            {
                ItemNoMundo pego = depois.Itens.PorId(press.Id)!;
                Verificar(pego.Situacao == SituacaoDoItem.Segurado && pego.Lugar == alvo.Lugar && pego.Pegada == new PontoPx(press.Cursor.X - alvo.Lugar.Ancora.X, press.Cursor.Y - alvo.Lugar.Ancora.Y),
                    () => $"ITEM_PRESS: {onde()}: o item ficou {pego.Situacao} em {pego.Lugar.Ancora}, pegada {pego.Pegada}");
                Estado esperado = antes.Estado is Estado.Walking or Estado.Resting ? Estado.Idle : antes.Estado;
                if (antes.Estado is Estado.Walking or Estado.Resting) Contar("atento: parou ou acordou");
                Verificar(depois.Estado == esperado && Equals(depois.Lugar, antes.Lugar),
                    () => $"atento: {onde()}: de {antes.Estado} foi a {depois.Estado} (esperado {esperado}), âncora {antes.Lugar?.Ancora} → {depois.Lugar?.Ancora}");
            }
        }
        if (evento is AutonomyTimer && antes.Atento)
        {
            Contar("AUTONOMY_TIMER com o usuário segurando um item");
            Verificar(transicoes.Count == 0 && depois == antes with { Sinal = Sinal.Nenhum }, () => $"atento: {onde()}: decisão autônoma com o usuário segurando um item ({Descrever(transicoes)})");
        }
        if (evento is ItemDragMove movimento && naMaoAntes is { Situacao: SituacaoDoItem.Arrastado } arrastado && arrastado.Id == movimento.Id)
        {
            var ancora = new PontoPx(movimento.Cursor.X - arrastado.Pegada.X, movimento.Cursor.Y - arrastado.Pegada.Y);
            Verificar(depois.Itens.PorId(arrastado.Id)?.Lugar.Ancora == ancora, () => $"invariante 2 do item: {onde()}: âncora {depois.Itens.PorId(arrastado.Id)?.Lugar.Ancora}, esperado {ancora}");
        }
        bool entrouNoUso = transicoes.Any(t => t.Para == Estado.Using && t.De != Estado.Using);
        // O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10): o uso que vem da agenda, sem item.
        bool fumouSozinho = entrouNoUso && evento is AutonomyTimer;
        if (evento is ItemDragEnd fim && naMaoAntes is { Situacao: SituacaoDoItem.Arrastado } solto && solto.Id == fim.Id && antes.Topologia is { } topologia)
        {
            var desejada = new PontoPx(fim.Cursor.X - solto.Pegada.X, fim.Cursor.Y - solto.Pegada.Y);
            MonitorDoDesktop m = Maquina.MonitorDaAncora(topologia, desejada);
            TamanhoPx tamanho = cfg.TamanhoDoItem.ParaPixels(m.Dpi);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(desejada, tamanho, m.AreaUtil);
            bool sobre = antes.Lugar is { } personagem && SobreEle(Posicionador.RetanguloDoSprite(presa, tamanho), personagem.Retangulo, cfg.MargemDoAlvo);
            bool aceita = AceitaOItem(antes.Estado);
            if (sobre && aceita)
            {
                Contar("item usado");
                Verificar(entrouNoUso && depois.Estado == Estado.Using && depois.Uso?.Item == solto.Item && sairam.SequenceEqual([solto.Id]),
                    () => $"invariante 23: {onde()}: solto sobre ele em {antes.Estado}, devia usar o {solto.Item}; ficou {depois.Estado}, uso {depois.Uso}");
            }
            else
            {
                Contar(sobre ? "soltar sobre ele recusado" : "item solto fora");
                ConferirSolto(antes, depois, solto, presa, m, onde);
            }
        }
        if (evento is ItemRelease largar && naMaoAntes is { } largado && largado.Id == largar.Id && antes.Topologia is { } topologiaAoLargar)
        {
            Contar("item largado");
            MonitorDoDesktop m = Maquina.MonitorDaAncora(topologiaAoLargar, largado.Lugar.Ancora);
            ConferirSolto(antes, depois, largado, Posicionador.PrenderNaAreaUtil(largado.Lugar.Ancora, cfg.TamanhoDoItem.ParaPixels(m.Dpi), m.AreaUtil), m, onde);
        }
        // Invariante 23 ajustado: um uso vem do ITEM_DRAG_END do usuário ou da ação autônoma UsarPorContaPropria, só o baseado, só em
        // IDLE no chão, e nunca cria nem tira item do mundo.
        if (entrouNoUso)
            Verificar(evento is ItemDragEnd || fumouSozinho, () => $"invariante 23: {onde()}: entrou em USING por {evento.GetType().Name}");
        ConferirOBaseadoPorContaPropria(cfg, antes, evento, r, onde, contagens, fumouSozinho, novos.Length + sairam.Length);

        // L6: um item que sai da mão sem o gesto dele acabar (esconder, sair, recolher, pegar outro) solta a captura.
        if (naMaoAntes is { } eraDaMao && depois.Itens.NaMao?.Id != eraDaMao.Id)
        {
            bool peloGesto = evento is ItemDragEnd d && d.Id == eraDaMao.Id || evento is ItemRelease rl && rl.Id == eraDaMao.Id;
            bool liberou = r.Efeitos.Contains(new LiberarCapturaDoItem(eraDaMao.Id));
            Verificar(peloGesto != liberou, () => $"L6: {onde()}: o item {eraDaMao.Id} saiu da mão {(peloGesto ? "pelo gesto, e a captura foi solta" : "sem o gesto acabar, e a captura não foi solta")}");
            if (evento is CmdClearItems) Contar("recolher com um item na mão (L6)");
            else if (!peloGesto && evento is ItemPress) Contar("pegar outro item com um na mão (L6)");
            else if (!peloGesto) Contar("esconder ou sair com um item na mão");
            if (!peloGesto && depois.Itens.PorId(eraDaMao.Id) is { } noChao && evento is not ItemPress)
                Verificar(noChao.Situacao == SituacaoDoItem.NoChao, () => $"4.8: {onde()}: o item da mão devia ficar no chão, ficou {noChao.Situacao}");
        }
        foreach (LiberarCapturaDoItem liberar in r.Efeitos.OfType<LiberarCapturaDoItem>())
            Verificar(naMaoAntes?.Id == liberar.Id, () => $"L6: {onde()}: soltou a captura do item {liberar.Id}, que não estava na mão");
        // Escondido ou saindo, nenhum item fica na mão: o gesto sobre ele acabou com a captura solta (4.8).
        if (depois.Estado is Estado.Hidden or Estado.Exiting)
            Verificar(depois.Itens.NaMao is null, () => $"4.8: {onde()}: {depois.Estado} com o item {depois.Itens.NaMao?.Id} na mão");

        // L5: um item que caía e deixou de aparecer foi direto ao chão.
        foreach (ItemNoMundo caia in antes.Itens.Todos.Where(i => i.Situacao == SituacaoDoItem.Caindo))
        {
            if (depois.Itens.PorId(caia.Id) is { Situacao: SituacaoDoItem.NoChao } assentado && !VeOItem(depois, assentado) && evento is not Tick)
                Contar("item assentado por ficar invisível (L5)");
        }
        if (evento is TopologyChanged mudanca && antes.Itens.Todos.Any(i => !i.NaMao) && antes.Topologia is { } velha && !velha.MesmaConfiguracao(mudanca.Topologia))
        {
            Contar("itens reacomodados pela topologia");
            // A mesma regra do personagem (Fase 5, passo P8): no monitor que não mudou de geometria, no máximo transladado, o
            // item anda junto e continua como estava, caindo ou no chão; senão, a posição dele acompanha a topologia e é
            // reacomodada pela posição relativa, parada. Um item que deixou de aparecer pode ter ido ao chão (L5): fica de fora.
            Topologia topologiaNova = mudanca.Topologia;
            foreach (ItemNoMundo item in antes.Itens.Todos.Where(i => !i.NaMao))
            {
                if (depois.Itens.PorId(item.Id) is not { } movido || !VeOItem(depois, movido)) continue;
                MonitorDoDesktop m = item.Lugar.Monitor;
                MonitorDoDesktop? n = topologiaNova.PorChave(m.Chave) ?? topologiaNova.Monitores.FirstOrDefault(x => x.Tela == m.Tela && velha.PorChave(x.Chave) is null);
                int dx = n is null ? 0 : n.Tela.Esquerda - m.Tela.Esquerda, dy = n is null ? 0 : n.Tela.Topo - m.Tela.Topo;
                if (n is not null && n.Tela == m.Tela.Deslocado(dx, dy) && n.AreaUtil == m.AreaUtil.Deslocado(dx, dy) && n.Dpi == m.Dpi)
                {
                    Contar("item transladado com o monitor");
                    var lugar = new Posicionamento(n, new PontoPx(item.Lugar.Ancora.X + dx, item.Lugar.Ancora.Y + dy), item.Lugar.Tamanho, item.Lugar.Retangulo.Deslocado(dx, dy));
                    Verificar(movido.Lugar == lugar && movido.Situacao == item.Situacao && movido.VY == item.VY && movido.Y == item.Y + dy && movido.Quiques == item.Quiques,
                        () => $"P8, itens: {onde()}: o item {item.Id} devia andar ({dx},{dy}) e continuar {item.Situacao}; ficou {movido.Situacao} em {movido.Lugar.Ancora}, VY {item.VY}→{movido.VY}");
                }
                else
                {
                    Contar("item reacomodado pela posição acompanhada");
                    (Posicionamento esperado, _) = Posicionador.Reacomodar(topologiaNova, Acompanhada(velha, topologiaNova, item.Posicao, cfg.TamanhoDoItem)!, cfg.TamanhoDoItem);
                    Verificar(movido.Lugar == esperado && movido.VY == 0 && movido.Quiques == 0,
                        () => $"P8, itens: {onde()}: o item {item.Id} devia ir a {esperado.Monitor.Chave} {esperado.Ancora}, parado; ficou em {movido.Lugar.Monitor.Chave} {movido.Lugar.Ancora}, VY {movido.VY}");
                }
            }
        }
        // O item na mão (revisão do bloco P6-P9, achado 6) anda com o monitor em que está, sem validar, como o arraste do
        // personagem: o Windows leva a janela e o cursor com o monitor físico.
        if (evento is TopologyChanged mudancaNaMao && antes.Itens.NaMao is { } naMao && antes.Topologia is { } antiga && !antiga.MesmaConfiguracao(mudancaNaMao.Topologia))
        {
            Contar("item na mão andou com o monitor");
            PontoPx ancora = PontoAcompanhado(antiga, mudancaNaMao.Topologia, naMao.Lugar.Ancora);
            MonitorDoDesktop m = mudancaNaMao.Topologia.MonitorMaisProximo(new PontoPx(ancora.X, ancora.Y - 1));
            TamanhoPx t = cfg.TamanhoDoItem.ParaPixels(m.Dpi);
            var esperado = new Posicionamento(m, ancora, t, new RetanguloPx(ancora.X - t.Largura / 2, ancora.Y - t.Altura, ancora.X - t.Largura / 2 + t.Largura, ancora.Y));
            ItemNoMundo? continua = depois.Itens.PorId(naMao.Id);
            Verificar(continua is { NaMao: true } && continua.Lugar == esperado && continua.Situacao == naMao.Situacao && continua.Y == naMao.Y + ancora.Y - naMao.Lugar.Ancora.Y,
                () => $"P6-P9, item na mão: {onde()}: o item {naMao.Id} devia ir de {naMao.Lugar.Ancora} a {esperado.Monitor.Chave} {ancora}; ficou {continua?.Situacao} em {continua?.Lugar.Monitor.Chave} {continua?.Lugar.Ancora}");
        }

        // ------------------------------------------------ 24: o uso
        Verificar((depois.Uso is not null) == (depois.Estado == Estado.Using), () => $"invariante 24: {onde()}: uso {depois.Uso} em {depois.Estado}");
        if (antes.Estado == Estado.Using && evento is Press)
        {
            Contar("USING interrompido por PRESS");
            if (rastro.UsoPorContaPropria) Contar("o clique interrompeu o baseado por conta própria");
            Verificar(depois.Estado == Estado.Pressed && transicoes.Count == 1, () => $"invariante 24: {onde()}: PRESS em USING levou a {depois.Estado} ({Descrever(transicoes)})");
        }
        // O menu de contexto em USING abre o menu e não interrompe o uso, nem o do baseado por conta própria (revisão do baseado,
        // achado 4): nenhuma transição, o mesmo uso e os mesmos passos.
        if (antes.Estado == Estado.Using && evento is ContextMenu)
        {
            Contar("menu de contexto em USING");
            if (rastro.UsoPorContaPropria) Contar("o menu não interrompeu o baseado por conta própria");
            Verificar(depois.Estado == Estado.Using && transicoes.Count == 0 && depois.Uso == antes.Uso && depois.PassosRestantes == antes.PassosRestantes
                    && r.Efeitos.OfType<AbrirMenu>().Count() == 1,
                () => $"invariante 24: {onde()}: o menu em USING levou a {depois.Estado} ({Descrever(transicoes)}), uso {antes.Uso} → {depois.Uso}, efeitos [{string.Join(", ", r.Efeitos.Select(Gravacao.DescreverEfeito))}]");
        }
        if (entrouNoUso)
        {
            DadosDoItem dados = cfg.TabelaDeItens(depois.Uso!.Item);
            rastro.UsoPorContaPropria = fumouSozinho;
            rastro.PassosNoUso = 0;
            rastro.PassosEsperados = dados.PassosDoUso;
            rastro.PresoNoUso = depois.PresoPeloUsuario;
            rastro.EsconderijoNoUso = depois.Esconderijo;
            rastro.AncoraNoUso = depois.Lugar!.Ancora;
            Verificar(depois.Uso.Passos == dados.PassosDoUso && depois.PassosRestantes == dados.PassosDoUso && depois.Uso.Verbo == dados.Verbo && depois.Expressao == dados.CaraDurante,
                () => $"invariante 24: {onde()}: o uso começou como {depois.Uso} ({depois.PassosRestantes} passos, cara {depois.Expressao})");
            Verificar(depois.Uso.Apoio == (depois.Esconderijo != LadoDoEsconderijo.Nenhum ? ApoioDoUso.Esconderijo : ApoioDoUso.Chao) || cfg.Movimento,
                () => $"invariante 24: {onde()}: sem a física, o apoio é o chão ou o esconderijo, e não {depois.Uso.Apoio}");
        }
        if (antes.Estado == Estado.Using && evento is Tick) rastro.PassosNoUso++;
        if (antes.Estado == Estado.Using && depois.Estado == Estado.Using)
            Verificar(depois.Uso == antes.Uso && depois.PassosRestantes == rastro.PassosEsperados - rastro.PassosNoUso,
                () => $"invariante 24: {onde()}: o uso mudou para {depois.Uso}, {depois.PassosRestantes} passos restantes depois de {rastro.PassosNoUso}");
        // Uma mudança de topologia que mantém o uso (o monitor dele só foi transladado, ou não mudou) leva a âncora junto
        // (invariante 19, conferido em ConferirTopologia): o "mesmo lugar" do fim do uso passa a ser o transladado. Sem isso,
        // a conferência do fim do uso no chão reprovava uma translação certa (lacuna achada na correção do alívio).
        if (evento is TopologyChanged && antes.Estado == Estado.Using && depois.Estado == Estado.Using && depois.Lugar is { } transladado)
            rastro.AncoraNoUso = transladado.Ancora;
        if (antes.Estado == Estado.Using && depois.Estado != Estado.Using)
        {
            // Desligar o conteúdo adulto (DEC-033) termina o uso adulto e tira as ondas de substância no mesmo evento.
            bool desligouOAdulto = antes.Preferencias.ConteudoAdulto && !depois.Preferencias.ConteudoAdulto;
            if (desligouOAdulto) Contar("30: o uso adulto termina ao desligar");
            // Desmarcar um item adulto (DEC-041, item 3) também tira a parte dele das ondas, no mesmo evento.
            bool desmarcouUmItem = (evento is CmdSetAdultItemEnabled { Ligado: false } d && antes.Preferencias.ItensAdultosHabilitados.Contem(d.Item))
                || (evento is SettingsChanged s && antes.Preferencias.ItensAdultosHabilitados.Itens.Any(i => !s.Preferencias.ItensAdultosHabilitados.Contem(i)));
            if (desmarcouUmItem) Contar("DEC-041: o uso termina ao desmarcar o item");
            Verificar(desligouOAdulto || desmarcouUmItem || (depois.Onda == antes.Onda && depois.OndaDeFundo == antes.OndaDeFundo), () => $"C16: {onde()}: o uso acabou e a onda mudou ({antes.Onda} → {depois.Onda})");
            bool peloFim = transicoes.Count > 0 && transicoes[0].Regra.StartsWith("USING: fim do uso", StringComparison.Ordinal);
            if (peloFim)
            {
                Contar("uso até o fim");
                if (rastro.UsoPorContaPropria) Contar("baseado por conta própria até o fim");
                Verificar(evento is Tick && rastro.PassosNoUso == rastro.PassosEsperados && transicoes[0].Para == Estado.Settling,
                    () => $"invariante 24: {onde()}: o uso acabou depois de {rastro.PassosNoUso} passos, esperado {rastro.PassosEsperados}");
                if (rastro.EsconderijoNoUso != LadoDoEsconderijo.Nenhum)
                    Verificar(depois.Estado == Estado.Peeking && depois.Esconderijo == rastro.EsconderijoNoUso, () => $"invariante 24: {onde()}: escondido, voltou a {depois.Estado} ({depois.Esconderijo})");
                else if (depois.Topologia is { } t && rastro.AncoraNoUso.Y == Maquina.MonitorDaAncora(t, rastro.AncoraNoUso).AreaUtil.Base)
                    Verificar(depois.Estado == Estado.Idle && depois.Lugar!.Ancora == rastro.AncoraNoUso, () => $"invariante 24: {onde()}: no chão, voltou a {depois.Estado} em {depois.Lugar?.Ancora}");
                if (depois.Estado is Estado.Climbing or Estado.Hanging)
                    Verificar(depois.PresoPeloUsuario == rastro.PresoNoUso, () => $"invariante 24: {onde()}: preso antes {rastro.PresoNoUso}, depois {depois.PresoPeloUsuario}");
                // A paranoia (pedido do usuário de 2026-10-01): o uso que a começou, até o fim, com ele de volta a IDLE e ela
                // ainda na frente, termina com o olhar pro teto, na hora, por 90 passos e sem sorteio (o gerador não muda: nem
                // o gesto nem a agenda sorteiam); em qualquer outro caso, o fim do uso não traz gesto nenhum.
                bool olha = rastro.UsoComecouAParanoia && depois.Estado == Estado.Idle && depois.Onda?.Tipo == Onda.Paranoico;
                if (olha) Contar("olhou pro teto no começo da paranoia");
                Verificar(olha ? depois.Gesto == Gesto.OlharProTeto && depois.PassosDoGesto == 90 && depois.Aleatorio == antes.Aleatorio : depois.Gesto == Gesto.Nenhum,
                    () => $"paranoia: {onde()}: no fim do uso (começou a paranoia: {rastro.UsoComecouAParanoia}), em {depois.Estado} com a onda {depois.Onda}, ficou o gesto {depois.Gesto} ({depois.PassosDoGesto} passos; gerador {(depois.Aleatorio == antes.Aleatorio ? "igual" : "mudou")})");
            }
            else
            {
                if (evento is not Press) Contar("uso interrompido sem PRESS");
                Verificar(evento is not Tick, () => $"invariante 24: {onde()}: um TICK interrompeu o uso antes do fim");
                Verificar(depois.Gesto == Gesto.Nenhum, () => $"paranoia: {onde()}: o uso interrompido trouxe o gesto {depois.Gesto}");
            }
        }

        // ------------------------------------------------ 25: a onda
        if (depois.Estado != Estado.Exiting)
            Verificar(depois.OndaAgendada == (depois.Onda is not null), () => $"invariante 25: {onde()}: onda {depois.Onda}, disparo pendente {depois.OndaAgendada}");
        else
            Verificar(!depois.OndaAgendada, () => $"invariante 25: {onde()}: saindo com um disparo da onda pendente");
        AgendarOnda[] agendas = [.. r.Efeitos.OfType<AgendarOnda>()];
        Verificar(agendas.Length + r.Efeitos.OfType<CancelarOnda>().Count() <= 1, () => $"invariante 25: {onde()}: mais de um efeito do temporizador da onda");
        foreach (AgendarOnda agenda in agendas)
            Verificar(agenda.Atraso >= TimeSpan.FromSeconds(1) && agenda.Geracao == depois.GeracaoDaOnda && depois.OndaAgendada,
                () => $"invariante 25: {onde()}: agendou {agenda.Atraso} na geração {agenda.Geracao} (estado com {depois.GeracaoDaOnda})");
        foreach (EstadoDaOnda? onda in new[] { depois.Onda, depois.OndaDeFundo })
        {
            if (onda is null) continue;
            Verificar(onda.Nivel is >= 1 and <= 3 && onda.Pior >= onda.Nivel && onda.Pior <= 3 && (onda.Fase != FaseDaOnda.Queda || onda.Nivel == 1),
                () => $"invariante 25: {onde()}: onda {onda}");
        }
        Verificar(depois.OndaDeFundo is null || (depois.Onda is not null && depois.Onda.Tipo != depois.OndaDeFundo.Tipo),
            () => $"invariante 25: {onde()}: onda de fundo {depois.OndaDeFundo} com a da frente {depois.Onda}");
        if (entrouNoUso)
        {
            DadosDoItem dados = cfg.TabelaDeItens(depois.Uso!.Item);
            (EstadoDaOnda? frente, EstadoDaOnda? fundo, string caso) = OndaDepoisDoUso(antes.Onda, antes.OndaDeFundo, dados, cfg);
            Contar(caso);
            // O soltar nunca sorteia no gerador principal: nem o alívio, nem a combinação, nem a paranoia. O uso por conta
            // própria gasta o sorteio da agenda, um passo, e, com mais de uma droga possível, o da droga (DEC-045), outro passo;
            // o resto do caminho é o mesmo.
            int possiveis = TabelaDoTamagotchi.Ilicitos.Itens.Count(i => antes.Preferencias.ItensPorContaPropria.Contem(i)
                && antes.Preferencias.ItensAdultosHabilitados.Contem(i) && cfg.ItensDaEdicao.Contem(i)
                && !(antes.Onda is { } frente && frente.Tipo == cfg.TabelaDeItens(i).Onda));
            Aleatorio principalEsperado = !fumouSozinho ? antes.Aleatorio
                : possiveis > 1 ? antes.Aleatorio.Sortear().Proximo.Sortear().Proximo : antes.Aleatorio.Sortear().Proximo;
            Verificar(depois.Aleatorio == principalEsperado,
                () => $"paranoia: {onde()}: o {(fumouSozinho ? "baseado por conta própria" : $"soltar do {dados.Item}")} mudou o gerador principal além do sorteio da agenda");
            // A paranoia, depois da combinação: a carga do episódio e, com ela na frente, a subida; sem ela, o sorteio, só no uso
            // que fecha a mistura com sintética, uma vez por episódio, no gerador próprio dela.
            (frente, fundo, Aleatorio geradorEsperado, string? paranoia) = ComAParanoia(frente, fundo, dados.Item, antes.AleatorioDaParanoia, cfg.ChanceDaParanoia, rastro);
            if (paranoia is not null) Contar(paranoia);
            if (fumouSozinho && paranoia is "sorteio da paranoia saiu" or "sorteio da paranoia não saiu") Contar("baseado por conta própria fechou a mistura e sorteou");
            if (fumouSozinho && paranoia is "episódio já sorteado não sorteou de novo") Contar("baseado por conta própria num episódio já sorteado");
            rastro.UsoComecouAParanoia = paranoia == "sorteio da paranoia saiu";
            if (rastro.UsoComecouAParanoia) Contar("paranoia começou");
            Verificar(depois.AleatorioDaParanoia == geradorEsperado,
                () => $"paranoia: {onde()}: {dados.Item} com a onda {antes.Onda} ({rastro.DescreverACarga()}) devia {(geradorEsperado == antes.AleatorioDaParanoia ? "não sortear" : "sortear um passo")} no gerador da paranoia");
            Verificar(depois.Onda == frente && depois.OndaDeFundo == fundo,
                () => $"4.5: {onde()}: {dados.Item} com a onda {antes.Onda} (fundo {antes.OndaDeFundo}; {rastro.DescreverACarga()}) deu {depois.Onda} (fundo {depois.OndaDeFundo}); esperado {frente} (fundo {fundo})");
            Verificar(depois.Uso.ComecouAParanoia == rastro.UsoComecouAParanoia, () => $"paranoia: {onde()}: o uso devia {(rastro.UsoComecouAParanoia ? "" : "não ")}ter começado a paranoia");
            if (paranoia is "sorteio da paranoia saiu" or "paranoia subiu de nível")
            {
                // A fase recomeçada: um disparo novo, com a duração cheia da fase da paranoia (a subida, 1 s; o pico, um nível
                // inteiro).
                AgendarOnda[] daParanoia = [.. r.Efeitos.OfType<AgendarOnda>()];
                EstadoDaOnda novaParanoia = frente!;
                Verificar(daParanoia.Length == 1 && daParanoia[0].Atraso == DuracaoCheia(cfg, novaParanoia) && daParanoia[0].Geracao == antes.GeracaoDaOnda + 1,
                    () => $"paranoia: {onde()}: {paranoia} ({novaParanoia}), e o temporizador não recomeçou com {DuracaoCheia(cfg, novaParanoia)}: [{string.Join(", ", r.Efeitos.Where(e => e is AgendarOnda or CancelarOnda).Select(Gravacao.DescreverEfeito))}]");
            }
            ConferirAlivio(cfg, antes, dados, r, onde, contagens);
            if (Alivia(dados, antes.Onda) && antes.Onda!.Tipo == Onda.Paranoico) Contar("paranoia acalmada por alívio");
        }
        else
        {
            // Fora de um uso, o gerador da paranoia nunca muda: só o sorteio dela, no soltar, o usa.
            Verificar(depois.AleatorioDaParanoia == antes.AleatorioDaParanoia, () => $"paranoia: {onde()}: o gerador da paranoia mudou fora de um uso");
        }
        if (evento is ItemEffectTimer disparo)
        {
            bool vale = antes.OndaAgendada && disparo.Geracao == antes.GeracaoDaOnda && antes.Onda is not null;
            if (!vale)
            {
                if (antes.Onda is not null && disparo.Geracao != antes.GeracaoDaOnda) Contar("disparo de onda velho");
                Verificar(transicoes.Count == 0 && r.Efeitos.Count == 0 && depois == antes with { Sinal = Sinal.Nenhum },
                    () => $"invariante 25: {onde()}: o disparo {disparo.Geracao} (agendado {antes.GeracaoDaOnda}, pendente {antes.OndaAgendada}) mudou alguma coisa");
            }
            else
            {
                Contar("onda avançou");
                (EstadoDaOnda? frente, EstadoDaOnda? fundo) = OndaDepoisDoDisparo(antes.Onda!, antes.OndaDeFundo, cfg);
                Verificar(depois.Onda == frente && depois.OndaDeFundo == fundo, () => $"4.5: {onde()}: o disparo levou {antes.Onda} (fundo {antes.OndaDeFundo}) a {depois.Onda} (fundo {depois.OndaDeFundo}); esperado {frente} (fundo {fundo})");
                rastro.DisparosNoEpisodio++;
                Verificar(rastro.DisparosNoEpisodio <= rastro.LimiteDoEpisodio, () => $"invariante 25: {onde()}: {rastro.DisparosNoEpisodio} disparos na mesma onda da frente, mais que {rastro.LimiteDoEpisodio}");
            }
        }
        if (antes.OndaDeFundo is { } deFundo && depois.OndaDeFundo is null && depois.Onda == deFundo) Contar("onda de fundo voltou");
        if (depois.Onda is { } nova && (entrouNoUso || antes.Onda is null || antes.Onda.Tipo != nova.Tipo))
        {
            rastro.DisparosNoEpisodio = 0;
            rastro.LimiteDoEpisodio = 2 + nova.Nivel;
        }

        // A carga da paranoia (pedidos do usuário de 2026-10-01), escrita aqui à parte: volta toda a 0, junto (as substâncias, a
        // sintética, os itens distintos e o sorteio feito), no fim de todo evento em que nem a onda da frente nem a de fundo é
        // de substância (pela transcrição; a paranoia é de substância). A paranoia só começa num uso de substância de um
        // episódio de mistura com sintética, e só pelo sorteio; na frente, o episódio é uma mistura com sintética que já
        // sorteou; e ela nunca fica no fundo: a precedência dela é a maior.
        // Desmarcar um item adulto (DEC-041), pelo comando ou por SETTINGS_CHANGED com a chave geral ligada, tira a parte dele.
        if (evento is CmdSetAdultItemEnabled { Ligado: false } itemDesmarcado && antes.Preferencias.ItensAdultosHabilitados.Contem(itemDesmarcado.Item))
            rastro.TirarItem(itemDesmarcado.Item, ItensSinteticos);
        if (evento is SettingsChanged { } novasPreferencias)
            foreach (Item saiu in antes.Preferencias.ItensAdultosHabilitados.Itens.Where(i => !novasPreferencias.Preferencias.ItensAdultosHabilitados.Contem(i)).ToArray())
                rastro.TirarItem(saiu, ItensSinteticos);
        bool comSubstancia = (depois.Onda is { } daFrente && OndasDeSubstancia.Contains(daFrente.Tipo))
            || (depois.OndaDeFundo is { } doFundo && OndasDeSubstancia.Contains(doFundo.Tipo));
        if (!comSubstancia && rastro.Carga > 0)
        {
            Contar("carga zerada");
            rastro.ZerarACarga();
        }
        Verificar(depois.Carga.Substancias == rastro.Carga && depois.Carga.Sintetica == rastro.Sintetica && depois.Carga.Distintas.Itens.SequenceEqual(rastro.Distintas)
                && depois.Carga.Sorteada == rastro.Sorteada,
            () => $"paranoia: {onde()}: a carga do núcleo é {depois.Carga.Substancias}, sintética {depois.Carga.Sintetica}, distintas [{depois.Carga.Distintas}], sorteada {depois.Carga.Sorteada}; a esperada, {rastro.DescreverACarga()} (onda {depois.Onda}, fundo {depois.OndaDeFundo})");
        if (depois.Onda?.Tipo == Onda.Paranoico && antes.Onda?.Tipo != Onda.Paranoico)
            Verificar(entrouNoUso && rastro.UsoComecouAParanoia, () => $"paranoia: {onde()}: a paranoia começou fora de um sorteio que saiu ({evento.GetType().Name})");
        Verificar(depois.Onda?.Tipo != Onda.Paranoico || (rastro.MisturaComSintetica && rastro.Sorteada),
            () => $"paranoia: {onde()}: a paranoia na frente sem um episódio de mistura com sintética que já sorteou ({rastro.DescreverACarga()})");
        Verificar(depois.OndaDeFundo?.Tipo != Onda.Paranoico, () => $"paranoia: {onde()}: a paranoia no fundo, atrás de {depois.Onda}");

        // ------------------------------------------------ 26: a física em vigor
        ParametrosDeMovimento f = Maquina.FisicaEfetiva(depois, cfg);
        Verificar(f with { VelocidadeAndando = cfg.Fisica.VelocidadeAndando, VelocidadeEscalando = cfg.Fisica.VelocidadeEscalando, VelocidadePendurado = cfg.Fisica.VelocidadePendurado } == cfg.Fisica,
            () => $"invariante 26: {onde()}: a onda {depois.Onda} mudou mais que as três velocidades");
        foreach ((double efetiva, double base_) in new[] { (f.VelocidadeAndando, cfg.Fisica.VelocidadeAndando), (f.VelocidadeEscalando, cfg.Fisica.VelocidadeEscalando), (f.VelocidadePendurado, cfg.Fisica.VelocidadePendurado) })
            Verificar(efetiva >= base_ * 0.5 - 1e-9 && efetiva <= base_ * 2 + 1e-9, () => $"invariante 26: {onde()}: velocidade {efetiva} com a base {base_}");
    }

    /// <summary>
    /// O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10; adendo da DEC-028), escrito aqui à parte do núcleo.
    /// Só um AUTONOMY_TIMER que vale (a geração agendada, visível, com a autonomia livre e sem item na mão do usuário) o faz
    /// fumar, e só com a chave ligada e a ação UsarPorContaPropria na configuração, em IDLE, no chão (a âncora na borda de baixo da
    /// área útil), sem estar escondido e sem a onda Chapado ou a paranoia na frente. O uso é o do baseado, no chão, por uma
    /// transição só, de IDLE para USING, com a regra "IDLE + AUTONOMY_TIMER: Fumar Baseado por conta própria" (mais o texto da
    /// paranoia); nenhum item nasce nem sai, nenhum Id é gasto e nenhuma janela de item muda. O resto do uso (a combinação, a
    /// carga, o sorteio da paranoia, a duração, o fim e o clique) é conferido como o de qualquer item, em
    /// <see cref="ConferirTamagotchi"/>. Conta também as decisões em IDLE no chão com Chapado ou a paranoia na frente, em que
    /// ele, com a ação ligada, não fumou.
    /// </summary>
    private static void ConferirOBaseadoPorContaPropria(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, Evento evento, Resultado r, Func<string> onde, Contagens contagens, bool fumou, int itensQueMudaram)
    {
        if (evento is not AutonomyTimer timer) return;
        EstadoDoNucleo depois = r.Estado;
        bool vale = antes.DecisaoAgendada && timer.Geracao == antes.Geracao && antes.Estado.Visivel() && !antes.Estado.ControladoPeloUsuario()
            && !antes.AutonomiaPausada && !antes.PainelAberto && antes.Itens.NaMao is null;
        bool ligada = cfg.Tamagotchi && (cfg.Acoes & AcoesAutonomas.UsarPorContaPropria) == AcoesAutonomas.UsarPorContaPropria;
        bool noChao = antes.Estado == Estado.Idle && antes.Esconderijo == LadoDoEsconderijo.Nenhum && antes.Lugar is { } l && l.Ancora.Y == l.Monitor.AreaUtil.Base;
        bool chapadoOuParanoico = antes.Onda?.Tipo is Onda.Chapado or Onda.Paranoico;
        // DEC-045: as drogas que podem, escritas aqui à parte do núcleo: marcadas por conta própria e nos itens adultos, da
        // edição, com a chave adulta ligada, sem a paranoia na frente e sem a onda delas na frente.
        Item[] podem = antes.Preferencias.ConteudoAdulto && antes.Onda?.Tipo != Onda.Paranoico
            ? [.. TabelaDoTamagotchi.Ilicitos.Itens.Where(i => antes.Preferencias.ItensPorContaPropria.Contem(i) && antes.Preferencias.ItensAdultosHabilitados.Contem(i)
                && cfg.ItensDaEdicao.Contem(i) && antes.Onda?.Tipo != cfg.TabelaDeItens(i).Onda)]
            : [];
        if (fumou)
        {
            contagens.Contar("fumou por conta própria");
            Item usado = depois.Uso?.Item ?? Item.Baseado;
            Verificar(vale && ligada && noChao && podem.Contains(usado),
                () => $"invariante 23: {onde()}: usou {usado} por conta própria sem poder (decisão que vale {vale}, ação ligada {ligada}, em IDLE no chão {noChao}, onda {antes.Onda}, podiam [{string.Join(",", podem)}])");
            if (usado != Item.Baseado) contagens.Contar("outra droga por conta própria");
            string regra = $"IDLE + AUTONOMY_TIMER: {cfg.TabelaDeItens(usado).Verbo} {usado} por conta própria";
            Verificar(r.Transicoes.Count == 1 && r.Transicoes[0].De == Estado.Idle && r.Transicoes[0].Para == Estado.Using && r.Transicoes[0].Regra.StartsWith(regra, StringComparison.Ordinal),
                () => $"baseado por conta própria: {onde()}: transições [{string.Join("; ", r.Transicoes)}]");
            Verificar(depois.Uso is { Apoio: ApoioDoUso.Chao }, () => $"uso por conta própria: {onde()}: o uso foi {depois.Uso}, e não no chão");
            Verificar(itensQueMudaram == 0 && depois.Itens == antes.Itens && depois.ProximoIdDeItem == antes.ProximoIdDeItem,
                () => $"invariante 23: {onde()}: o baseado por conta própria mexeu nos itens ([{antes.Itens}] → [{depois.Itens}], próximo Id {antes.ProximoIdDeItem} → {depois.ProximoIdDeItem})");
            Verificar(!r.Efeitos.Any(e => e is MostrarItem or MoverItem or EsconderItem or RemoverItem or LiberarCapturaDoItem),
                () => $"invariante 23: {onde()}: o baseado por conta própria mexeu numa janela de item");
            return;
        }
        if (vale && ligada && noChao && chapadoOuParanoico) contagens.Contar("não fumou chapado nem paranoico");
        Verificar(!(vale && ligada && noChao && podem.Length == 0 && depois.Estado == Estado.Using),
            () => $"invariante 23: {onde()}: entrou em uso sem nenhuma droga que podia");
        // Em IDLE fora do chão (sem a física, depois do pouso pelos sinais), sem estar escondido e sem Chapado nem paranoia na
        // frente, só o chão o impede (revisão do baseado, achado 4): com a ação ligada, a decisão que vale não o fez fumar.
        bool foraDoChao = antes.Estado == Estado.Idle && antes.Esconderijo == LadoDoEsconderijo.Nenhum && antes.Lugar is { } noAr && noAr.Ancora.Y != noAr.Monitor.AreaUtil.Base;
        if (vale && ligada && foraDoChao && !chapadoOuParanoico) contagens.Contar("IDLE fora do chão com a ação: não fumou");
    }

    /// <summary>O item invocado nasce no monitor dele, entre as laterais, numa das colunas candidatas (4.8), acima do chão.</summary>
    private static void ConferirNascimento(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, ItemNoMundo novo, Func<string> onde)
    {
        Posicionamento personagem = antes.Lugar!;
        Topologia topologia = antes.Topologia!;
        MonitorDoDesktop m = topologia.PorChave(personagem.Monitor.Chave) ?? Maquina.MonitorDaAncora(topologia, personagem.Ancora);
        TamanhoPx tamanho = cfg.TamanhoDoItem.ParaPixels(m.Dpi);
        Superficies sup = Superficies.Do(topologia, m, tamanho);
        double escala = m.Dpi / 96.0;
        int folga = (int)Math.Round(cfg.Fisica.FolgaDoItem * escala, MidpointRounding.AwayFromZero);
        int afastamento = cfg.Tamanho.ParaPixels(m.Dpi).Largura / 2 + folga + tamanho.Largura / 2;
        int olhando = antes.Direcao == Direcao.Direita ? 1 : -1;
        int[] candidatos = [.. Enumerable.Range(0, 3).SelectMany(k => new[] { olhando, -olhando }.Select(lado => personagem.Ancora.X + lado * (afastamento + k * (tamanho.Largura + folga))))];
        int x = novo.Lugar.Ancora.X;
        Verificar(novo.Lugar.Monitor.Chave == m.Chave && x >= sup.Esquerda && x <= sup.Direita && (candidatos.Contains(x) || x == Math.Clamp(candidatos[0], sup.Esquerda, sup.Direita)),
            () => $"4.8: {onde()}: o item nasceu em {novo.Lugar.Monitor.Chave} x={x}; candidatos [{string.Join(",", candidatos)}] entre {sup.Esquerda} e {sup.Direita}");
        if (novo.Situacao == SituacaoDoItem.Caindo)
        {
            double y = Math.Max(sup.Teto, Math.Min(personagem.Ancora.Y, sup.Chao) - cfg.Fisica.AlturaDaQuedaDoItem * escala);
            Verificar(novo.Lugar.Ancora.Y == (int)Math.Round(y, MidpointRounding.AwayFromZero), () => $"4.8: {onde()}: o item nasceu em y={novo.Lugar.Ancora.Y}, esperado {y}");
        }
    }

    /// <summary>Um item solto fora (ou recusado) ou largado: fora da mão, no lugar preso na área útil; no chão, parado; no ar, caindo (ou no chão, se não se vê).</summary>
    private static void ConferirSolto(EstadoDoNucleo antes, EstadoDoNucleo depois, ItemNoMundo item, PontoPx presa, MonitorDoDesktop m, Func<string> onde)
    {
        ItemNoMundo? solto = depois.Itens.PorId(item.Id);
        Verificar(solto is not null && !solto.NaMao, () => $"soltar: {onde()}: o item {item.Id} continua na mão ou sumiu");
        bool noChao = presa.Y == m.AreaUtil.Base;
        if (noChao || !VeOItem(depois, solto!))
            Verificar(solto!.Situacao == SituacaoDoItem.NoChao && solto.Lugar.Ancora == new PontoPx(presa.X, m.AreaUtil.Base) && solto.Lugar.Monitor.Chave == m.Chave,
                () => $"soltar: {onde()}: no chão (ou sem aparecer), o item ficou {solto.Situacao} em {solto.Lugar.Ancora}, esperado {presa.X} no chão de {m.Chave}");
        else
            Verificar(solto!.Situacao == SituacaoDoItem.Caindo && solto.Lugar.Ancora == presa && solto.VY == 0,
                () => $"soltar: {onde()}: o item ficou {solto.Situacao} em {solto.Lugar.Ancora}, esperado caindo de {presa}");
        Verificar(depois.Estado == antes.Estado, () => $"soltar: {onde()}: soltar ou largar um item fora mudou o estado de {antes.Estado} para {depois.Estado}");
    }

    /// <summary>
    /// A classe do alívio (pedido do usuário de 2026-10-01), lida da transcrição das tabelas do desenho
    /// (<see cref="Tamagotchi.TabelasDoDesenho"/>), à parte do núcleo: os itens de alívio, a comida e a bebida sem álcool (a
    /// banana, a água, o café e o energético). Os outros, inclusive o cogumelo, que também se come, e a bala, droga
    /// sintética, são de substância.
    /// </summary>
    private static readonly Item[] ItensDeAlivio = [.. Tamagotchi.TabelasDoDesenho.ItensEsperados().Where(i => i.Alivio).Select(i => i.Item)];

    /// <summary>
    /// Os itens de substância, pela mesma transcrição: os outros nove, que entram na carga da paranoia (pedidos do usuário de
    /// 2026-10-01).
    /// </summary>
    private static readonly Item[] ItensDeSubstancia = [.. Tamagotchi.TabelasDoDesenho.ItensEsperados().Where(i => !i.Alivio).Select(i => i.Item)];

    /// <summary>
    /// As drogas sintéticas, pela mesma transcrição (a bala, o MD, a cocaína e o lança-perfume): só um episódio que mistura
    /// substâncias com uma delas sorteia a paranoia.
    /// </summary>
    private static readonly Item[] ItensSinteticos = [.. Tamagotchi.TabelasDoDesenho.ItensEsperados().Where(i => i.Sintetica).Select(i => i.Item)];

    /// <summary>As outras substâncias, pela mesma transcrição (álcool, maconha, cigarro e cogumelo): misturadas só entre si, nunca sorteiam.</summary>
    private static readonly Item[] OutrasSubstancias = [.. Tamagotchi.TabelasDoDesenho.ItensEsperados().Where(i => !i.Alivio && !i.Sintetica).Select(i => i.Item)];

    /// <summary>
    /// As ondas de substância, pela mesma transcrição: as que a comida e a bebida sem álcool acalmam (as leves, só a água),
    /// inclusive a paranoia.
    /// </summary>
    private static readonly Onda[] OndasDeSubstancia = [.. Tamagotchi.TabelasDoDesenho.OndasEsperadas().Where(o => o.DeSubstancia).Select(o => o.Onda)];

    /// <summary>
    /// Se o item alivia a onda da frente, pela classe da transcrição: só um item de alívio e só com onda na frente; sem onda
    /// própria (a água), qualquer onda; com onda própria, só uma de substância.
    /// </summary>
    private static bool Alivia(DadosDoItem dados, EstadoDaOnda? frente)
        => ItensDeAlivio.Contains(dados.Item) && frente is not null && (dados.Onda is null || OndasDeSubstancia.Contains(frente.Tipo));

    /// <summary>
    /// A combinação (4.5), escrita aqui à parte: a onda da frente e a de fundo depois de usar o item, e o caso. Primeiro, o
    /// alívio (pedido do usuário de 2026-10-01; <see cref="Alivia"/>): a água, com qualquer onda na frente, e a comida e a
    /// bebida sem álcool, com uma onda de substância na frente, a aliviam um passo (<see cref="Aliviada"/>); a água sem onda
    /// não faz nada. Senão: sem onda, a do item começa na subida; do mesmo tipo da da frente ou da de fundo, soma níveis até 3
    /// (a queda volta ao pico); de precedência maior ou igual, vai para a frente e a da frente vai para o fundo; de
    /// precedência menor, é absorvida.
    /// </summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo, string Caso) OndaDepoisDoUso(EstadoDaOnda? frente, EstadoDaOnda? fundo, DadosDoItem dados, ConfiguracaoDoNucleo cfg)
    {
        if (Alivia(dados, frente))
        {
            (EstadoDaOnda? f, EstadoDaOnda? b) = Aliviada(frente!, fundo, cfg);
            return (f, b, dados.Onda is null ? "água baixou a onda" : "alívio com onda de substância na frente");
        }
        if (dados.Onda is not { } tipo) return (frente, fundo, "água sem onda");
        int n = Math.Clamp(dados.Intensidade, 1, 3);
        EstadoDaOnda Somada(EstadoDaOnda o)
        {
            int nivel = Math.Min(3, o.Nivel + n);
            return new EstadoDaOnda(o.Tipo, o.Fase == FaseDaOnda.Subida ? FaseDaOnda.Subida : FaseDaOnda.Pico, nivel, Math.Max(o.Pior, nivel));
        }
        if (frente is null) return (new EstadoDaOnda(tipo, FaseDaOnda.Subida, n, n), fundo, "onda começou");
        if (frente.Tipo == tipo) return (Somada(frente), fundo, "onda acumulada");
        if (fundo is not null && fundo.Tipo == tipo) return (frente, Somada(fundo), "onda acumulada");
        if (cfg.TabelaDeOndas(tipo).Precedencia >= cfg.TabelaDeOndas(frente.Tipo).Precedencia) return (new EstadoDaOnda(tipo, FaseDaOnda.Subida, n, n), frente, "onda foi para o fundo");
        return (frente, fundo, "onda absorvida");
    }

    /// <summary>
    /// A paranoia (pedidos do usuário de 2026-10-01), escrita aqui à parte, depois da combinação (<paramref name="frente"/> e
    /// <paramref name="fundo"/> são as ondas dela). Um item de substância, pela transcrição, entra na carga do rastro: mais
    /// um, a sintética se ele for, e ele entre os distintos; um de alívio, não. Com a paranoia na frente, ela sobe um nível
    /// (até 3), o pior acompanha, e a queda volta ao pico (a subida continua subida), sem sorteio. Sem ela, só o uso que fecha
    /// um episódio de mistura com sintética (uma sintética e pelo menos dois itens distintos) sorteia, uma vez por episódio
    /// (o rastro guarda que o episódio sorteou), num passo do gerador da paranoia: o valor cru do passo, num inteiro de 1 a Em
    /// pelo resto, sai se for até Vezes (escrito aqui à parte de <see cref="Aleatorio.Sortear(Chance)"/>); saindo, ela começa
    /// na frente, na subida do nível 1, e a frente vai para o fundo (a de fundo anterior sai). Devolve as ondas, o gerador da
    /// paranoia esperado depois do uso e o caso (nulo sem nada a contar: um item de alívio ou a primeira substância do
    /// episódio).
    /// </summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo, Aleatorio Gerador, string? Caso) ComAParanoia(
        EstadoDaOnda? frente, EstadoDaOnda? fundo, Item item, Aleatorio gerador, Chance chance, RastroDoTamagotchi rastro)
    {
        if (ItensDeAlivio.Contains(item)) return (frente, fundo, gerador, null);
        rastro.Carga++;
        rastro.Sintetica |= ItensSinteticos.Contains(item);
        rastro.Distintas.Add(item);
        rastro.PorItem[item] = rastro.PorItem.GetValueOrDefault(item) + 1;
        if (frente is { Tipo: Onda.Paranoico })
        {
            int nivel = Math.Min(3, frente.Nivel + 1);
            var subiu = new EstadoDaOnda(Onda.Paranoico, frente.Fase == FaseDaOnda.Subida ? FaseDaOnda.Subida : FaseDaOnda.Pico, nivel, Math.Max(frente.Pior, nivel));
            return (subiu, fundo, gerador, "paranoia subiu de nível");
        }
        if (!rastro.MisturaComSintetica)
        {
            string? semSorteio = rastro.Distintas.Count >= 2 ? "mistura sem sintética não sorteou"
                : rastro.Sintetica && rastro.Carga >= 2 ? "sintética sozinha repetida não sorteou"
                : null;
            return (frente, fundo, gerador, semSorteio);
        }
        if (rastro.Sorteada) return (frente, fundo, gerador, "episódio já sorteado não sorteou de novo");
        rastro.Sorteada = true;
        (ulong valor, Aleatorio proximo) = gerador.Sortear();
        bool saiu = 1 + (long)(valor % (ulong)chance.Em) <= chance.Vezes;
        return saiu
            ? (new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Subida, 1, 1), frente, proximo, "sorteio da paranoia saiu")
            : (frente, fundo, proximo, "sorteio da paranoia não saiu");
    }

    /// <summary>
    /// Um passo do alívio, pela decisão: na queda, a da frente acaba (e a de fundo volta); na subida ou no pico acima do
    /// nível 1, um nível abaixo, na mesma fase; no nível 1, da subida ou do pico, a queda (sem queda, acaba).
    /// </summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo) Aliviada(EstadoDaOnda frente, EstadoDaOnda? fundo, ConfiguracaoDoNucleo cfg)
    {
        if (frente.Fase == FaseDaOnda.Queda) return (fundo, null);
        if (frente.Nivel > 1) return (frente with { Nivel = frente.Nivel - 1 }, fundo);
        return cfg.TabelaDeOndas(frente.Tipo).Queda is null ? (fundo, null) : (frente with { Fase = FaseDaOnda.Queda, Nivel = 1 }, fundo);
    }

    /// <summary>
    /// O alívio (pedido do usuário de 2026-10-01), conferido por propriedades, além do resultado exato de
    /// <see cref="OndaDepoisDoUso"/>. Com um item de alívio e uma onda que ele alivia na frente (a água, qualquer onda; a
    /// comida e a bebida sem álcool, uma de substância): o nível nunca sobe, e o pior não muda; a fase em curso nunca se
    /// alonga (só o nível caiu: nenhum efeito do temporizador e o mesmo disparo pendente; a queda que começa, ou a de fundo
    /// que volta: um disparo novo com a duração cheia da fase); a onda de fundo nunca é tocada (só volta à frente, se a da
    /// frente acabou); e a onda do item nunca começa nem soma. Com uma onda leve na frente, a comida e a bebida combinam como
    /// sempre (conferido por <see cref="OndaDepoisDoUso"/>), e aqui só se conta o caso. Nos dois casos, nenhum sorteio: o
    /// gerador logo depois do soltar é o de logo antes.
    /// </summary>
    private static void ConferirAlivio(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, DadosDoItem dados, Resultado r, Func<string> onde, Contagens contagens)
    {
        if (!ItensDeAlivio.Contains(dados.Item) || antes.Onda is not { } frente) return;
        EstadoDoNucleo depois = r.Estado;
        // Nenhum sorteio novo: o item de alívio, aliviando ou combinando como sempre, não mexe no gerador.
        Verificar(depois.Aleatorio == antes.Aleatorio, () => $"alívio: {onde()}: {dados.Item} com {frente} na frente sorteou: o gerador mudou");
        if (!Alivia(dados, frente))
        {
            contagens.Contar("item de alívio com onda leve na frente");
            return;
        }
        AgendarOnda[] agendas = [.. r.Efeitos.OfType<AgendarOnda>()];
        bool cancelou = r.Efeitos.OfType<CancelarOnda>().Any();
        string Efeitos() => string.Join(", ", r.Efeitos.Where(e => e is AgendarOnda or CancelarOnda).Select(Gravacao.DescreverEfeito));
        if (dados.Onda is { } propria)
        {
            Verificar(!(depois.Onda?.Tipo == propria && depois.Onda != antes.OndaDeFundo) && !(depois.OndaDeFundo?.Tipo == propria && depois.OndaDeFundo != antes.OndaDeFundo),
                () => $"alívio: {onde()}: {dados.Item} com {frente} na frente começou ou somou a onda dele ({propria}): ficou {depois.Onda} (fundo {depois.OndaDeFundo})");
        }

        if (depois.Onda is not { } nova || nova.Tipo != frente.Tipo)
        {
            contagens.Contar(antes.OndaDeFundo is null ? "alívio acabou a onda" : "alívio acabou a onda e a de fundo voltou");
            Verificar(depois.Onda == antes.OndaDeFundo && depois.OndaDeFundo is null,
                () => $"alívio: {onde()}: a frente {frente} acabou, e a de fundo {antes.OndaDeFundo} devia voltar intacta; ficou {depois.Onda} (fundo {depois.OndaDeFundo})");
            if (depois.Onda is { } voltou)
                Verificar(agendas.Length == 1 && agendas[0].Atraso == DuracaoCheia(cfg, voltou) && agendas[0].Geracao == antes.GeracaoDaOnda + 1 && !cancelou,
                    () => $"alívio: {onde()}: a de fundo {voltou} voltou sem um disparo novo com a duração cheia ({DuracaoCheia(cfg, voltou)}): [{Efeitos()}]");
            else
                Verificar(agendas.Length == 0 && cancelou && !depois.OndaAgendada, () => $"alívio: {onde()}: sem onda, o temporizador devia ser cancelado: [{Efeitos()}]");
            return;
        }

        Verificar(nova.Nivel <= frente.Nivel && nova.Pior == frente.Pior, () => $"alívio: {onde()}: {frente} foi a {nova}: o nível subiu ou o pior mudou");
        Verificar(depois.OndaDeFundo == antes.OndaDeFundo, () => $"alívio: {onde()}: a onda de fundo mudou de {antes.OndaDeFundo} para {depois.OndaDeFundo}");
        if (nova.Fase == frente.Fase)
        {
            contagens.Contar("alívio baixou o nível");
            Verificar(agendas.Length == 0 && !cancelou && depois.OndaAgendada && depois.GeracaoDaOnda == antes.GeracaoDaOnda,
                () => $"alívio: {onde()}: só o nível caiu ({frente} → {nova}), e o temporizador em curso mudou: [{Efeitos()}], geração {antes.GeracaoDaOnda} → {depois.GeracaoDaOnda}");
        }
        else
        {
            contagens.Contar("alívio levou à queda");
            Verificar(nova.Fase == FaseDaOnda.Queda && agendas.Length == 1 && agendas[0].Atraso == DuracaoCheia(cfg, nova) && agendas[0].Geracao == antes.GeracaoDaOnda + 1 && !cancelou,
                () => $"alívio: {onde()}: {frente} foi a {nova}, que devia ser a queda com um disparo novo de {DuracaoCheia(cfg, nova)}: [{Efeitos()}]");
        }
    }

    /// <summary>
    /// A duração cheia de uma fase, pelos dados da tabela (4.2), escrita aqui: a subida, um nível do pico, ou a queda pelo
    /// pior nível (a base × 100, 125 ou 150%); nunca menos de 1 s.
    /// </summary>
    private static TimeSpan DuracaoCheia(ConfiguracaoDoNucleo cfg, EstadoDaOnda onda)
    {
        DadosDaOnda d = cfg.TabelaDeOndas(onda.Tipo);
        TimeSpan t = onda.Fase switch
        {
            FaseDaOnda.Subida => d.Subida,
            FaseDaOnda.Pico => d.NivelDoPico,
            _ => TimeSpan.FromTicks(d.QuedaBase.Ticks * (100 + 25 * (onda.Pior - 1)) / 100),
        };
        return t < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : t;
    }

    /// <summary>O disparo da onda (4.5): subida → pico; pico acima do 1 → um nível abaixo; pico no 1 → queda (sem queda, o fim); queda → o fim; no fim, a de fundo volta.</summary>
    private static (EstadoDaOnda? Frente, EstadoDaOnda? Fundo) OndaDepoisDoDisparo(EstadoDaOnda frente, EstadoDaOnda? fundo, ConfiguracaoDoNucleo cfg)
        => frente.Fase switch
        {
            FaseDaOnda.Subida => (frente with { Fase = FaseDaOnda.Pico }, fundo),
            FaseDaOnda.Pico when frente.Nivel > 1 => (frente with { Nivel = frente.Nivel - 1 }, fundo),
            FaseDaOnda.Pico when cfg.TabelaDeOndas(frente.Tipo).Queda is not null => (frente with { Fase = FaseDaOnda.Queda, Nivel = 1 }, fundo),
            _ => (fundo, null),
        };

    /// <summary>Se a janela do item aparece (L4 e D18), escrito aqui à parte: na mão, sempre; fora, com o personagem à vista e fora de um monitor ocupado pela tela cheia (com o modo ligado).</summary>
    private static bool VeOItem(EstadoDoNucleo s, ItemNoMundo item)
        => item.Situacao is SituacaoDoItem.Segurado or SituacaoDoItem.Arrastado
            || (s.Estado is not (Estado.Booting or Estado.Hidden or Estado.Exiting) && !(s.Preferencias.ModoTelaCheia && s.Ocupados.Contem(item.Lugar.Monitor.Chave)));

    /// <summary>
    /// "Sobre ele" (C14), escrito aqui à parte: o retângulo do item tem ao menos um pixel em comum com o do personagem
    /// encolhido a margem de cada lado (arredondada, metade para cima).
    /// </summary>
    private static bool SobreEle(RetanguloPx item, RetanguloPx personagem, int margem)
    {
        int dx = (personagem.Largura * margem + 50) / 100, dy = (personagem.Altura * margem + 50) / 100;
        int esquerda = personagem.Esquerda + dx, direita = personagem.Direita - dx, topo = personagem.Topo + dy, baixo = personagem.Base - dy;
        return esquerda < direita && topo < baixo && item.Esquerda < direita && esquerda < item.Direita && item.Topo < baixo && topo < item.Base;
    }

    /// <summary>Os estados que aceitam um item solto sobre ele (tabela 4.6), escritos aqui à parte.</summary>
    private static bool AceitaOItem(Estado e)
        => e is Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Resting or Estado.Reacting or Estado.Landing or Estado.Peeking;

    /// <summary>
    /// Linhas de FULLSCREEN_TARGETS_CHANGED (ARCHITECTURE.md 2.6, DEC-013 e DEC-020), com o modo e o
    /// cache de antes do evento: "uma vez por mudança" (R12) e, com o modo desligado, só o cache;
    /// em PRESSED e DRAGGING, só o cache e a marca de R9 (invariante 14); visível, age só se a âncora
    /// estiver num monitor ocupado e transfere para o livre mais próximo dela, ou esconde se não houver
    /// livre, guardando a posição anterior se ainda não houver uma; HIDDEN(POR_TELA_CHEIA) reaparece no
    /// livre mais próximo do retorno; escondido por outro motivo, só o conjunto vazio age (R4). Uma
    /// chave desconhecida conta no conjunto, mas nunca como monitor.
    /// </summary>
    private static void ConferirTelaCheia(EstadoDoNucleo antes, FullscreenTargetsChanged f, Resultado r, Func<string> onde, Contagens contagens)
    {
        if (antes.Estado == Estado.Exiting) return;
        EstadoDoNucleo depois = r.Estado;
        MonitoresOcupados ocupados = f.Ocupados;
        Topologia? topologia = antes.Topologia;
        PosicaoDoPersonagem? retorno = antes.RetornoDaTelaCheia;
        Verificar(depois.Ocupados.Equals(ocupados), () => $"tela cheia: {onde()}: cache {depois.Ocupados}, esperado {ocupados}");
        if (topologia is not null && ocupados.Chaves.Any(ch => topologia.PorChave(ch) is null)) contagens.Contar("tela cheia com chave desconhecida");

        // Nada além do cache (e do fim de um gesto curto, invariante 15) muda. Com o tamagotchi (DEC-028), as janelas dos
        // itens acompanham os monitores ocupados, e um item que caía e sumiu vai ao chão, desligando o relógio (L5): isso
        // é conferido em ConferirTamagotchi.
        void SoOCache(string regra, bool marca)
        {
            bool itemCaindoAntes = antes.Itens.Todos.Any(i => i.Situacao == SituacaoDoItem.Caindo && VeOItem(antes, i));
            Efeito[] doPersonagem = [.. r.Efeitos.Where(e => e is not (MostrarItem or MoverItem or EsconderItem) && !(e is DesligarRelogio && itemCaindoAntes))];
            bool efeitosDoGesto = antes.Gesto != Gesto.Nenhum
                ? doPersonagem.All(e => e is DesligarRelogio or AgendarDecisao)
                : doPersonagem.Length == 0;
            Verificar(r.Transicoes.Count == 0 && depois.Estado == antes.Estado && depois.Motivo == antes.Motivo
                    && Equals(depois.Lugar, antes.Lugar) && Equals(depois.Posicao, antes.Posicao) && Equals(depois.RetornoDaTelaCheia, retorno)
                    && depois.PainelAberto == antes.PainelAberto && depois.TelaCheiaMudouNoGesto == marca && efeitosDoGesto,
                () => $"tela cheia ({regra}): {onde()}: devia só atualizar o cache, mas {antes.Estado}/{antes.Motivo}→{depois.Estado}/{depois.Motivo}, "
                    + $"{antes.Lugar?.Ancora}→{depois.Lugar?.Ancora}, retorno {Descrever(retorno)}→{Descrever(depois.RetornoDaTelaCheia)}, "
                    + $"marca {antes.TelaCheiaMudouNoGesto}→{depois.TelaCheiaMudouNoGesto}, {Descrever(r.Transicoes)}, efeitos [{string.Join(", ", r.Efeitos.Select(e => e.GetType().Name))}]");
        }

        if (ocupados.Equals(antes.Ocupados))
        {
            contagens.Contar("tela cheia repetida");
            SoOCache("R12: o mesmo conjunto não age", antes.TelaCheiaMudouNoGesto);
            return;
        }
        if (!antes.Preferencias.ModoTelaCheia)
        {
            contagens.Contar("tela cheia com o modo desligado");
            SoOCache("modo desligado", antes.TelaCheiaMudouNoGesto);
            return;
        }
        if (topologia is null || antes.Estado == Estado.Booting)
        {
            SoOCache("antes da carga", antes.TelaCheiaMudouNoGesto);
            return;
        }
        if (antes.Estado is Estado.Pressed or Estado.Dragging)
        {
            contagens.Contar("tela cheia durante o gesto");
            SoOCache("PRESSED, DRAGGING: o gesto não é interrompido", marca: true);
            return;
        }

        MonitorDoDesktop[] livres = [.. topologia.Monitores.Where(mon => !ocupados.Contem(mon.Chave))];
        // O livre escolhido é o mais próximo da referência (empates valem qualquer um deles).
        void NoLivreMaisProximo(string regra, PontoPx referencia)
        {
            long Distancia(MonitorDoDesktop mon) => mon.Tela.DistanciaAoQuadrado(referencia);
            MonitorDoDesktop? destino = depois.Lugar is { } lugar ? livres.FirstOrDefault(mon => mon.Chave == lugar.Monitor.Chave) : null;
            Verificar(depois.Estado.Visivel() && destino is not null && Distancia(destino) == livres.Min(Distancia),
                () => $"tela cheia ({regra}): {onde()}: foi para {depois.Estado} em {depois.Lugar?.Monitor.Chave}; livres {string.Join(",", livres.Select(l => $"{l.Chave}@{Distancia(l)}"))} a partir de {referencia}");
        }

        if (antes.Estado == Estado.Hidden)
        {
            if (antes.Motivo != MotivoDoOcultamento.PorTelaCheia)
            {
                if (ocupados.Vazio && retorno is not null)
                {
                    // R4: o episódio acabou com o personagem escondido por outro motivo.
                    contagens.Contar("fim da tela cheia escondido por outro motivo, com retorno");
                    Verificar(r.Transicoes.Count == 0 && r.Efeitos.Count == 0 && depois.Estado == Estado.Hidden && depois.Motivo == antes.Motivo
                            && Equals(depois.Posicao, retorno) && depois.RetornoDaTelaCheia is null,
                        () => $"R4: {onde()}: esperado continuar {antes.Motivo} com a posição de antes e sem retorno; obtido {depois.Estado}/{depois.Motivo}, posição {Descrever(depois.Posicao)}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
                    return;
                }
                if (retorno is not null) contagens.Contar("tela cheia não vazia escondido por outro motivo, com retorno");
                SoOCache("escondido por outro motivo", antes.TelaCheiaMudouNoGesto);
                return;
            }
            if (ocupados.Vazio)
            {
                contagens.Contar("tela cheia vazia restaura a posição anterior");
                Verificar(depois.Estado.Visivel() && depois.RetornoDaTelaCheia is null, () => $"tela cheia vazia: {onde()}: HIDDEN(POR_TELA_CHEIA) devia reaparecer sem retorno; obtido {depois.Estado}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
                return;
            }
            if (livres.Length == 0)
            {
                SoOCache("HIDDEN(POR_TELA_CHEIA) sem monitor livre", antes.TelaCheiaMudouNoGesto);
                return;
            }
            PosicaoDoPersonagem referencia = retorno ?? antes.Posicao!;
            if (retorno is not null && livres.Length > 1) contagens.Contar("HIDDEN(POR_TELA_CHEIA) reaparece no livre mais próximo do retorno");
            NoLivreMaisProximo("HIDDEN(POR_TELA_CHEIA) com monitor livre: reaparece nele", referencia.AncoraAbsoluta);
            Verificar(Equals(depois.RetornoDaTelaCheia, retorno), () => $"tela cheia: {onde()}: HIDDEN(POR_TELA_CHEIA) reapareceu sem manter o retorno");
            return;
        }

        // Visível, fora de um gesto.
        if (ocupados.Vazio)
        {
            if (retorno is null)
            {
                contagens.Contar("fim da tela cheia sem retorno, visível");
                SoOCache("invariante 14: fim da tela cheia sem retorno", antes.TelaCheiaMudouNoGesto);
                return;
            }
            contagens.Contar("tela cheia vazia restaura a posição anterior");
            Verificar(depois.Estado.Visivel() && depois.RetornoDaTelaCheia is null && r.Transicoes.Any(t => t.Para == Estado.Settling),
                () => $"tela cheia vazia: {onde()}: devia restaurar a posição anterior e limpar o retorno; obtido {depois.Estado}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
            return;
        }
        PontoPx ancora = antes.Lugar!.Ancora;
        if (!ocupados.Contem(antes.Lugar.Monitor.Chave))
        {
            SoOCache("a âncora está num monitor livre", antes.TelaCheiaMudouNoGesto);
            return;
        }
        PosicaoDoPersonagem guardada = retorno ?? antes.Posicao!;
        if (livres.Length == 0)
        {
            contagens.Contar("tela cheia esconde sem monitor livre");
            Verificar(depois.Estado == Estado.Hidden && depois.Motivo == MotivoDoOcultamento.PorTelaCheia && !depois.PainelAberto && Equals(depois.RetornoDaTelaCheia, guardada),
                () => $"tela cheia sem monitor livre: {onde()}: esperado HIDDEN(POR_TELA_CHEIA), painel fechado e retorno {Descrever(guardada)}; obtido {depois.Estado}/{depois.Motivo}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
            return;
        }
        if (livres.Length > 1) contagens.Contar("tela cheia transfere com mais de um monitor livre");
        NoLivreMaisProximo("transfere para o monitor livre", ancora);
        Verificar(Equals(depois.RetornoDaTelaCheia, guardada), () => $"tela cheia: {onde()}: transferido com retorno {Descrever(depois.RetornoDaTelaCheia)}, esperado {Descrever(guardada)} (\"se ainda não houver uma guardada\")");
    }

    /// <summary>
    /// TOPOLOGY_CHANGED com o app aberto (Fase 5, passo P8; DEC-030), contra regras escritas aqui, à parte do núcleo:
    /// <list type="bullet">
    /// <item>com a mesma configuração, nada além do cache;</item>
    /// <item>o retorno da tela cheia, em qualquer estado, e a posição, nos estados que não revalidam (PRESSED, DRAGGING, BOOTING
    /// e HIDDEN), acompanham a topologia (<see cref="Acompanhada"/>), sem transição e sem mover a janela;</item>
    /// <item>invariante 19: nos estados que revalidam, se a geometria do monitor do personagem não mudou (no máximo transladado,
    /// ou com a chave nova e a mesma tela), o estado continua, com uma transição para ele mesmo; a âncora, o retângulo e a
    /// posição fina andam exatamente pela translação; a posição descreve o lugar novo; o que estava em curso continua; e a
    /// agenda e o relógio não mudam (salvo o fim de um gesto curto, invariante 15, e os itens);</item>
    /// <item>senão, SETTLING: no monitor da mesma chave, ou, sem ele, no mais próximo do pixel dos pés da posição acompanhada,
    /// com a regra que diz que ele foi desconectado;</item>
    /// <item>invariante 20: depois, visível e fora de PRESSED e DRAGGING, o monitor do personagem é da topologia nova e, quando
    /// o sprite cabe nele, a âncora está na área útil dele.</item>
    /// </list>
    /// </summary>
    private static void ConferirTopologia(EstadoDoNucleo antes, TopologyChanged mudanca, Resultado r, Func<string> onde, Contagens contagens, TamanhoDip tamanho)
    {
        if (antes.Estado == Estado.Exiting) return;
        EstadoDoNucleo depois = r.Estado;
        Topologia nova = mudanca.Topologia;
        Verificar(ReferenceEquals(depois.Topologia, nova), () => $"topologia: {onde()}: o cache não é a topologia nova");
        if (antes.Topologia is not { } velha) return;
        if (velha.MesmaConfiguracao(nova))
        {
            contagens.Contar("TOPOLOGY_CHANGED com a mesma configuração");
            Verificar(r.Transicoes.Count == 0 && depois.Estado == antes.Estado && Equals(depois.Lugar, antes.Lugar) && Equals(depois.Posicao, antes.Posicao)
                    && Equals(depois.RetornoDaTelaCheia, antes.RetornoDaTelaCheia) && !r.Efeitos.Any(e => e is MoverJanela or MoverItem),
                () => $"topologia: {onde()}: com a mesma configuração, devia só atualizar o cache ({Descrever(r.Transicoes)})");
            return;
        }

        PosicaoDoPersonagem? retorno = Acompanhada(velha, nova, antes.RetornoDaTelaCheia, tamanho);
        if (retorno is not null) contagens.Contar("retorno da tela cheia acompanhou a topologia");
        Verificar(Equals(depois.RetornoDaTelaCheia, retorno),
            () => $"topologia: {onde()}: retorno {Descrever(depois.RetornoDaTelaCheia)} (tela {depois.RetornoDaTelaCheia?.TelaDoMonitor}), esperado {Descrever(retorno)} (tela {retorno?.TelaDoMonitor})");

        bool revalida = antes.Estado is Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Jumping or Estado.Falling
            or Estado.Landing or Estado.Resting or Estado.Peeking or Estado.Reacting or Estado.Using;
        if (!revalida || antes.Lugar is null || antes.Posicao is null)
        {
            contagens.Contar(antes.Estado is Estado.Pressed or Estado.Dragging ? "TOPOLOGY_CHANGED no gesto: só o cache e as posições" : "TOPOLOGY_CHANGED escondido: só o cache e as posições");
            PosicaoDoPersonagem? posicao = Acompanhada(velha, nova, antes.Posicao, tamanho);
            // No arraste (revisão do bloco P6-P9, achado 2), o lugar anda com o monitor em que está, sem validar, e a janela vai
            // junto; nos outros, o lugar fica.
            Posicionamento? lugarEsperado = antes.Lugar;
            if (antes.Estado == Estado.Dragging && antes.Lugar is { } arrastado)
            {
                contagens.Contar("TOPOLOGY_CHANGED no arraste: o lugar anda com o monitor");
                PontoPx ancora = PontoAcompanhado(velha, nova, arrastado.Ancora);
                MonitorDoDesktop doArraste = nova.MonitorMaisProximo(new PontoPx(ancora.X, ancora.Y - 1));
                TamanhoPx t = tamanho.ParaPixels(doArraste.Dpi);
                lugarEsperado = new Posicionamento(doArraste, ancora, t, new RetanguloPx(ancora.X - t.Largura / 2, ancora.Y - t.Altura, ancora.X - t.Largura / 2 + t.Largura, ancora.Y));
            }
            Verificar(r.Transicoes.Count == 0 && depois.Estado == antes.Estado && Equals(depois.Lugar, lugarEsperado) && Equals(depois.Posicao, posicao)
                    && r.Efeitos.Count(e => e is MoverJanela) == (Equals(lugarEsperado, antes.Lugar) ? 0 : 1),
                () => $"topologia: {onde()}: em {antes.Estado}, devia só acompanhar as posições; {Descrever(r.Transicoes)}, lugar {antes.Lugar?.Ancora}→{depois.Lugar?.Ancora} (esperado {lugarEsperado?.Ancora}), posição {Descrever(depois.Posicao)}, esperada {Descrever(posicao)}");
            return;
        }

        // O monitor correspondente: o da mesma chave; com chave nova, o de mesma tela cuja chave não existia antes.
        Posicionamento lugar = antes.Lugar;
        MonitorDoDesktop m = lugar.Monitor;
        MonitorDoDesktop? n = nova.PorChave(m.Chave) ?? nova.Monitores.FirstOrDefault(x => x.Tela == m.Tela && velha.PorChave(x.Chave) is null);
        int dx = n is null ? 0 : n.Tela.Esquerda - m.Tela.Esquerda, dy = n is null ? 0 : n.Tela.Topo - m.Tela.Topo;
        bool mesmaGeometria = n is not null && n.Tela == m.Tela.Deslocado(dx, dy) && n.AreaUtil == m.AreaUtil.Deslocado(dx, dy) && n.Dpi == m.Dpi;
        if (mesmaGeometria)
        {
            contagens.Contar(dx != 0 || dy != 0 ? "TOPOLOGY_CHANGED que só translada o monitor do personagem (invariante 19)"
                : n!.Chave == m.Chave ? "TOPOLOGY_CHANGED sem mudar o monitor do personagem (invariante 19)"
                : "TOPOLOGY_CHANGED que só troca a chave do monitor do personagem (invariante 19)");
            if (antes.Estado == Estado.Using) contagens.Contar("USING continua com o monitor só transladado ou igual");
            var ancora = new PontoPx(lugar.Ancora.X + dx, lugar.Ancora.Y + dy);
            Verificar(depois.Estado == antes.Estado && r.Transicoes.Count == 1 && r.Transicoes[0].De == antes.Estado && r.Transicoes[0].Para == antes.Estado,
                () => $"invariante 19: {onde()}: o monitor do personagem só andou ({dx},{dy}), e o estado foi de {antes.Estado} a {depois.Estado} ({Descrever(r.Transicoes)})");
            Verificar(depois.Lugar is { } l && l.Monitor == n && l.Ancora == ancora && l.Tamanho == lugar.Tamanho && l.Retangulo == lugar.Retangulo.Deslocado(dx, dy),
                () => $"invariante 19: {onde()}: lugar {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora} {depois.Lugar?.Retangulo}; esperado {n!.Chave} {ancora} {lugar.Retangulo.Deslocado(dx, dy)}");
            RetanguloPx area = n!.AreaUtil;
            Verificar(depois.Posicao is { } p && p.ChaveMonitor == n.Chave && p.TelaDoMonitor == n.Tela && p.AncoraAbsoluta == ancora
                    && p.FracaoX == (ancora.X - area.Esquerda) / (double)area.Largura && p.FracaoY == (ancora.Y - area.Topo) / (double)area.Altura,
                () => $"invariante 19: {onde()}: a posição {Descrever(depois.Posicao)} (tela {depois.Posicao?.TelaDoMonitor}) não descreve o lugar novo {n.Chave} {ancora}");
            Verificar(depois.Esconderijo == antes.Esconderijo && depois.PresoPeloUsuario == antes.PresoPeloUsuario && depois.Uso == antes.Uso
                    && depois.PassosRestantes == antes.PassosRestantes && depois.Direcao == antes.Direcao && depois.Expressao == antes.Expressao
                    && depois.Movimento == (antes.Estado.EmMovimento() ? antes.Movimento with { X = antes.Movimento.X + dx, Y = antes.Movimento.Y + dy } : antes.Movimento),
                () => $"invariante 19: {onde()}: o que estava em curso não continuou igual (esconderijo {antes.Esconderijo}→{depois.Esconderijo}, preso {antes.PresoPeloUsuario}→{depois.PresoPeloUsuario}, uso {antes.Uso}→{depois.Uso}, movimento {antes.Movimento}→{depois.Movimento})");
            Verificar(r.Efeitos.Count(e => e is MoverJanela) == (Equals(depois.Lugar, antes.Lugar) ? 0 : 1),
                () => $"invariante 19: {onde()}: a janela {(Equals(depois.Lugar, antes.Lugar) ? "não devia se mover" : "devia acompanhar")} ({r.Efeitos.Count(e => e is MoverJanela)} MoverJanela)");
            // Um gesto curto acaba com qualquer evento do sistema (invariante 15), e com ele o relógio e a agenda mudam; um item
            // pode passar a cair ou a aparecer.
            if (antes.Gesto == Gesto.Nenhum)
                Verificar(depois.Geracao == antes.Geracao && depois.DecisaoAgendada == antes.DecisaoAgendada && !r.Efeitos.Any(e => e is AgendarDecisao or CancelarDecisao),
                    () => $"invariante 19: {onde()}: a agenda mudou (geração {antes.Geracao}→{depois.Geracao})");
            if (antes.Gesto == Gesto.Nenhum && antes.Itens.Quantidade == 0)
                Verificar(depois.RelogioAtivo == antes.RelogioAtivo && !r.Efeitos.Any(e => e is LigarRelogio or DesligarRelogio), () => $"invariante 19: {onde()}: o relógio mudou");
        }
        else
        {
            contagens.Contar(n is null ? "TOPOLOGY_CHANGED sem o monitor do personagem" : "TOPOLOGY_CHANGED com o monitor do personagem mudado");
            string regra = n is null ? "TOPOLOGY_CHANGED: o monitor do personagem foi desconectado" : "TOPOLOGY_CHANGED";
            Verificar(r.Transicoes.Count > 0 && r.Transicoes[0].De == antes.Estado && r.Transicoes[0].Para == Estado.Settling && r.Transicoes[0].Regra == regra,
                () => $"topologia: {onde()}: devia revalidar com \"{regra}\" ({Descrever(r.Transicoes)}, {r.Transicoes.FirstOrDefault()?.Regra})");
            // Na mesma chave, ou no mais próximo do pixel dos pés da posição que acompanhou a topologia.
            PosicaoDoPersonagem acompanhada = Acompanhada(velha, nova, antes.Posicao, tamanho)!;
            MonitorDoDesktop esperado = n ?? nova.MonitorMaisProximo(new PontoPx(acompanhada.AncoraAbsoluta.X, acompanhada.AncoraAbsoluta.Y - 1));
            Verificar(depois.Lugar?.Monitor.Chave == esperado.Chave,
                () => $"topologia: {onde()}: revalidado em {depois.Lugar?.Monitor.Chave}, esperado {esperado.Chave} (posição acompanhada {Descrever(acompanhada)})");
        }

        // Invariante 20.
        if (depois.Estado.Visivel() && depois.Estado is not (Estado.Pressed or Estado.Dragging) && depois.Lugar is { } final)
        {
            contagens.Contar("invariante 20 conferido");
            RetanguloPx areaFinal = final.Monitor.AreaUtil;
            bool cabe = final.Tamanho.Largura <= areaFinal.Largura && final.Tamanho.Altura <= areaFinal.Altura;
            Verificar(nova.Monitores.Contains(final.Monitor) && (!cabe || (final.Ancora.X >= areaFinal.Esquerda && final.Ancora.X < areaFinal.Direita
                    && final.Ancora.Y > areaFinal.Topo && final.Ancora.Y <= areaFinal.Base)),
                () => $"invariante 20: {onde()}: {depois.Estado} em {final.Monitor} com a âncora {final.Ancora}, fora da topologia nova ou da área útil");
        }
    }

    /// <summary>
    /// A posição guardada depois de uma mudança de topologia (DEC-030), escrita aqui à parte do núcleo (Posicionador.Rebasear): no
    /// monitor correspondente (a mesma chave; com chave nova, a mesma tela), a fração na área útil atual dele, com a chave e a tela
    /// dele; sem ele, a chave, as frações e a tela ficam, e a âncora anda com o sobrevivente mais próximo do pixel dos pés, medido
    /// nas coordenadas antigas (no empate, o principal, depois a ordem da topologia nova).
    /// </summary>
    private static PosicaoDoPersonagem? Acompanhada(Topologia velha, Topologia nova, PosicaoDoPersonagem? p, TamanhoDip tamanho)
    {
        if (p is null) return null;
        MonitorDoDesktop? n = nova.PorChave(p.ChaveMonitor)
            ?? (p.TelaDoMonitor is { } tela ? nova.Monitores.FirstOrDefault(x => x.Tela == tela && velha.PorChave(x.Chave) is null) : null);
        if (n is not null)
            return p with { ChaveMonitor = n.Chave, AncoraAbsoluta = Posicionador.NoMonitor(n, p.FracaoX, p.FracaoY, tamanho).Ancora, TelaDoMonitor = n.Tela };

        var pes = new PontoPx(p.AncoraAbsoluta.X, p.AncoraAbsoluta.Y - 1);
        (MonitorDoDesktop Novo, MonitorDoDesktop Velho)? melhor = null;
        long menor = long.MaxValue;
        foreach (MonitorDoDesktop x in nova.Monitores)
        {
            if (velha.PorChave(x.Chave) is not { } v) continue;
            long d = v.Tela.DistanciaAoQuadrado(pes);
            if (d < menor || (d == menor && x.Principal && !melhor!.Value.Novo.Principal))
            {
                melhor = (x, v);
                menor = d;
            }
        }
        return melhor is { } s
            ? p with { AncoraAbsoluta = new PontoPx(p.AncoraAbsoluta.X + s.Novo.Tela.Esquerda - s.Velho.Tela.Esquerda, p.AncoraAbsoluta.Y + s.Novo.Tela.Topo - s.Velho.Tela.Topo) }
            : p;
    }

    /// <summary>
    /// Um ponto livre (a âncora do arraste ou do item na mão) depois de uma mudança de topologia (revisão do bloco P6-P9,
    /// achados 2 e 6), escrito aqui à parte do núcleo (Posicionador.AcompanharPonto): anda com a tela do monitor do pixel dos
    /// pés na topologia antiga, se ele continua (a mesma chave; com chave nova, a mesma tela); senão, com a do sobrevivente
    /// mais próximo do pixel dos pés, medido nas coordenadas antigas; sem nenhum, fica.
    /// </summary>
    private static PontoPx PontoAcompanhado(Topologia velha, Topologia nova, PontoPx ponto)
    {
        var pes = new PontoPx(ponto.X, ponto.Y - 1);
        MonitorDoDesktop daqui = velha.MonitorMaisProximo(pes);
        MonitorDoDesktop? n = nova.PorChave(daqui.Chave) ?? nova.Monitores.FirstOrDefault(x => x.Tela == daqui.Tela && velha.PorChave(x.Chave) is null);
        if (n is not null) return new PontoPx(ponto.X + n.Tela.Esquerda - daqui.Tela.Esquerda, ponto.Y + n.Tela.Topo - daqui.Tela.Topo);
        (MonitorDoDesktop Novo, MonitorDoDesktop Velho)? melhor = null;
        long menor = long.MaxValue;
        foreach (MonitorDoDesktop x in nova.Monitores)
        {
            if (velha.PorChave(x.Chave) is not { } v) continue;
            long d = v.Tela.DistanciaAoQuadrado(pes);
            if (d < menor || (d == menor && x.Principal && !melhor!.Value.Novo.Principal))
            {
                melhor = (x, v);
                menor = d;
            }
        }
        return melhor is { } s ? new PontoPx(ponto.X + s.Novo.Tela.Esquerda - s.Velho.Tela.Esquerda, ponto.Y + s.Novo.Tela.Topo - s.Velho.Tela.Topo) : ponto;
    }

    /// <summary>
    /// Linha BOOTING | configurações e topologia carregadas (R6): a primeira carga vale, com a topologia
    /// e as preferências dela (energia saneada, R1); um pedido de esconder anterior continua valendo,
    /// sem mostrar nem agendar; sem ele, o personagem aparece (ou a tela cheia em cache o esconde). A
    /// posição salva é restaurada pela cascata da partida (ARCHITECTURE.md 2.8, Posicionador.Restaurar):
    /// o monitor da chave; sem ele, o primeiro com a tela salva; sem nenhum dos dois, o principal. A
    /// posição relativa salva é aplicada à área útil desse monitor, e a posição do núcleo passa a ser
    /// dele, com a tela dele e frações válidas. Sem posição salva, começa no principal. As cargas
    /// seguintes são ignoradas.
    /// </summary>
    private static void ConferirCarga(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, Loaded carga, Resultado r, Func<string> onde, Contagens contagens)
    {
        EstadoDoNucleo depois = r.Estado;
        if (antes.Carregado || antes.Estado == Estado.Exiting)
        {
            contagens.Contar("carga repetida");
            Verificar(r.Transicoes.Count == 0 && antes.Estado == depois.Estado && antes.Motivo == depois.Motivo
                    && Equals(antes.Lugar, depois.Lugar) && Equals(antes.Posicao, depois.Posicao)
                    && antes.Preferencias == depois.Preferencias && ReferenceEquals(antes.Topologia, depois.Topologia),
                () => $"R6: {onde()}: carga repetida não foi ignorada ({antes.Estado}→{depois.Estado}, {Descrever(r.Transicoes)})");
            return;
        }

        if (antes.Estado == Estado.Hidden) contagens.Contar("carga depois de pedidos de esconder");
        if (!carga.Preferencias.AtravessarMonitores) contagens.Contar("carga com a travessia desligada");
        if (carga.PosicaoSalva is { } salvaDesconhecida && carga.Topologia.PorChave(salvaDesconhecida.ChaveMonitor) is null) contagens.Contar("carga com posição salva em monitor inexistente");
        if (!Enum.IsDefined(carga.Preferencias.Energia)) contagens.Contar("carga fora do enum");
        Verificar(depois.Carregado && ReferenceEquals(depois.Topologia, carga.Topologia) && depois.Preferencias == Saneadas(carga.Preferencias),
            () => $"R6: {onde()}: a primeira carga não valeu (carregado {depois.Carregado}, preferências {depois.Preferencias})");

        if (antes.Estado == Estado.Hidden && antes.Motivo != MotivoDoOcultamento.Nenhum)
        {
            contagens.Contar("carga que mantém um pedido de esconder");
            Verificar(r.Transicoes.Count == 0 && r.Efeitos.Count == 0 && depois.Estado == Estado.Hidden && depois.Motivo == antes.Motivo,
                () => $"R6: {onde()}: o pedido de esconder anterior à carga ({antes.Motivo}) não continuou valendo: {depois.Estado}/{depois.Motivo}, {Descrever(r.Transicoes)}, {r.Efeitos.Count} efeitos");
        }
        else
        {
            contagens.Contar("carga que mostra o personagem");
            Verificar(r.Transicoes.Count > 0 && r.Transicoes[0].Para == Estado.Settling
                    && (depois.Estado.Visivel() || (depois.Estado == Estado.Hidden && depois.Motivo == MotivoDoOcultamento.PorTelaCheia)),
                () => $"R6: {onde()}: a carga de {antes.Estado}/{antes.Motivo} não mostrou o personagem: {depois.Estado}/{depois.Motivo}, {Descrever(r.Transicoes)}");
        }

        // A tela cheia em cache não agiu (ela sempre guarda um retorno quando age): a posição é a da carga.
        if (depois.RetornoDaTelaCheia is not null) return;
        Posicionamento lugar = Afirmar.NaoNulo(depois.Lugar, $"R6: {onde()}: lugar depois da carga");
        if (carga.PosicaoSalva is { } salva)
        {
            contagens.Contar("carga com posição salva conferida");
            MonitorDoDesktop? daChave = carga.Topologia.PorChave(salva.ChaveMonitor);
            MonitorDoDesktop? daTela = daChave is null ? carga.Topologia.Monitores.FirstOrDefault(m => m.Tela == salva.TelaDoMonitor) : null;
            if (daTela is not null) contagens.Contar("carga restaurada pelo retângulo do monitor");
            else if (daChave is null) contagens.Contar("carga restaurada no monitor principal");
            MonitorDoDesktop esperado = daChave ?? daTela ?? carga.Topologia.Principal;
            Verificar(lugar.Monitor.Chave == esperado.Chave,
                () => $"R6: {onde()}: posição salva em {salva.ChaveMonitor} (tela {salva.TelaDoMonitor?.ToString() ?? "desconhecida"}) restaurada em {lugar.Monitor.Chave}, esperado {esperado.Chave}");
            int x = Posicionador.NoMonitor(esperado, salva.FracaoX, salva.FracaoY, cfg.Tamanho).Ancora.X;
            Verificar(lugar.Ancora.X == x, () => $"R6: {onde()}: âncora {lugar.Ancora} em {lugar.Monitor.Chave} não é a fração salva {salva.FracaoX} (x {x})");
            // A posição do núcleo passa a ser do monitor escolhido: a chave e a tela dele, e frações
            // válidas mesmo quando as salvas eram NaN, infinitas ou fora de [0, 1].
            Verificar(depois.Posicao is { } p && p.ChaveMonitor == esperado.Chave && p.TelaDoMonitor == esperado.Tela
                    && p.FracaoX is >= 0 and <= 1 && p.FracaoY is >= 0 and <= 1,
                () => $"R6: {onde()}: posição do núcleo {Descrever(depois.Posicao)} (tela {depois.Posicao?.TelaDoMonitor}) depois de restaurar em {esperado.Chave} (tela {esperado.Tela})");
        }
        else
        {
            Posicionamento inicial = Posicionador.Inicial(carga.Topologia, cfg.Tamanho);
            Verificar(lugar.Monitor.Chave == inicial.Monitor.Chave && lugar.Ancora.X == inicial.Ancora.X, () => $"R6: {onde()}: sem posição salva, começou em {lugar.Monitor.Chave} {lugar.Ancora}, não na posição inicial {inicial.Ancora}");
        }
    }

    /// <summary>
    /// SETTINGS_CHANGED: as preferências passam a ser as recebidas, com o nível fora do enum trocado
    /// por Média (R1). Desligar o modo com o retorno guardado desfaz o efeito temporário (linha
    /// "qualquer, com retorno temporário guardado | SETTINGS_CHANGED que desliga o modo"): visível ou
    /// escondido pela tela cheia, volta à posição anterior validada; escondido por outro motivo, a
    /// posição anterior volta a valer sem reaparecer; em PRESSED e DRAGGING o gesto não é
    /// interrompido e o retorno fica para o fim dele (conferido na regra R9 acima).
    /// </summary>
    private static void ConferirConfiguracoes(ConfiguracaoDoNucleo cfg, EstadoDoNucleo antes, SettingsChanged configuracao, Resultado r, Func<string> onde, Contagens contagens)
    {
        EstadoDoNucleo depois = r.Estado;
        if (!Enum.IsDefined(configuracao.Preferencias.Energia)) contagens.Contar("SETTINGS_CHANGED fora do enum");
        if (!configuracao.Preferencias.AtravessarMonitores) contagens.Contar("SETTINGS_CHANGED com a travessia desligada");
        Verificar(depois.Preferencias == Saneadas(configuracao.Preferencias),
            () => $"R1: {onde()}: preferências {depois.Preferencias}, esperado {Saneadas(configuracao.Preferencias)}");

        bool desliga = antes.Preferencias.ModoTelaCheia && !configuracao.Preferencias.ModoTelaCheia;
        if (!desliga || antes.RetornoDaTelaCheia is not { } retorno || antes.Topologia is null) return;
        if (antes.Estado is Estado.Pressed or Estado.Dragging)
        {
            contagens.Contar("desligar o modo com retorno no meio do gesto");
            Verificar(depois.Estado == antes.Estado && Equals(depois.RetornoDaTelaCheia, retorno) && Equals(depois.Lugar, antes.Lugar),
                () => $"SETTINGS_CHANGED desliga o modo no gesto: {onde()}: o gesto devia seguir intacto com o retorno {Descrever(retorno)}; obtido {depois.Estado}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
            return;
        }
        if (antes.Estado == Estado.Hidden && antes.Motivo != MotivoDoOcultamento.PorTelaCheia)
        {
            contagens.Contar("desligar o modo com retorno, escondido por outro motivo");
            Verificar(depois.Estado == Estado.Hidden && depois.Motivo == antes.Motivo && depois.RetornoDaTelaCheia is null && Equals(depois.Posicao, retorno),
                () => $"SETTINGS_CHANGED desliga o modo escondido por {antes.Motivo}: {onde()}: esperado continuar escondido com a posição {Descrever(retorno)} e sem retorno; obtido {depois.Estado}({depois.Motivo}), posição {Descrever(depois.Posicao)}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
            return;
        }
        bool voltaAPosicaoAnterior = antes.Estado.Visivel() || (antes.Estado == Estado.Hidden && antes.Motivo == MotivoDoOcultamento.PorTelaCheia);
        if (!voltaAPosicaoAnterior) return;

        contagens.Contar("desligar o modo com retorno, visível ou escondido pela tela cheia");
        (Posicionamento anterior, _) = Posicionador.Reacomodar(antes.Topologia, retorno, cfg.Tamanho);
        Verificar(depois.Estado.Visivel() && depois.RetornoDaTelaCheia is null && depois.Lugar is { } lugar
                && lugar.Monitor.Chave == anterior.Monitor.Chave && lugar.Ancora.X == anterior.Ancora.X,
            () => $"SETTINGS_CHANGED desliga o modo: {onde()}: esperado voltar a {anterior.Monitor.Chave} {anterior.Ancora} sem retorno; obtido {depois.Estado} em {depois.Lugar?.Monitor.Chave} {depois.Lugar?.Ancora}, retorno {Descrever(depois.RetornoDaTelaCheia)}");
    }

    /// <summary>
    /// R8 (R-i) e linhas de CMD_HIDE, SESSION_LOCKED, SUSPENDING, CMD_EXIT e SESSION_ENDING: esconder
    /// um personagem ainda não escondido, ou sair, grava exatamente uma vez a posição escolhida pelo
    /// usuário (o retorno, se houver; nenhuma, se ainda não há posição). No meio do arraste, o gesto
    /// termina onde está, como um DRAG_CANCEL (ARCHITECTURE.md 2.7), e "um arraste sempre descarta o
    /// retorno" (R9, DEC-020): grava o ponto validado. Esconder fora do arraste guarda o retorno para o
    /// fim da tela cheia (R4); já escondido, nada é gravado.
    /// </summary>
    private static void ConferirGravacaoAoEsconderOuSair(EstadoDoNucleo antes, Evento evento, Resultado r, Func<string> onde, Contagens contagens)
    {
        bool esconde = evento is CmdHide or SessionLocked or Suspending;
        bool sai = evento is CmdExit or SessionEnding;
        if (antes.Estado == Estado.Exiting || !(esconde || sai)) return;
        EstadoDoNucleo depois = r.Estado;
        GravarPosicao[] gravadas = [.. r.Efeitos.OfType<GravarPosicao>()];
        if (esconde && antes.Estado == Estado.Hidden)
        {
            Verificar(gravadas.Length == 0, () => $"R8: {onde()}: já escondido, gravou {gravadas.Length} posição(ões)");
            return;
        }

        bool doArraste = antes.Estado == Estado.Dragging;
        PosicaoDoPersonagem? esperada = doArraste ? depois.Posicao : antes.RetornoDaTelaCheia ?? antes.Posicao;
        if (antes.RetornoDaTelaCheia is not null)
            contagens.Contar(doArraste ? "esconder ou sair no meio do arraste com retorno" : "esconder ou sair com retorno guardado");
        Verificar(gravadas.Length == (esperada is null ? 0 : 1) && (esperada is null || Equals(gravadas[0].Posicao, esperada)),
            () => $"R8: {onde()}: esperado gravar {Descrever(esperada)} uma vez; gravou [{string.Join("; ", gravadas.Select(g => Gravacao.DescreverPosicao(g.Posicao)))}] (retorno antes {Descrever(antes.RetornoDaTelaCheia)}, de {antes.Estado})");
        if (doArraste)
        {
            Verificar(depois.RetornoDaTelaCheia is null, () => $"R9: {onde()}: o arraste interrompido não descartou o retorno {Descrever(depois.RetornoDaTelaCheia)}");
            Verificar(depois.Topologia is { } topologia && depois.Lugar is { } lugar && NaAreaUtil(topologia, lugar.Ancora),
                () => $"R8: {onde()}: o arraste interrompido não foi validado: âncora {depois.Lugar?.Ancora}");
        }
        else if (esconde && antes.Preferencias.ModoTelaCheia)
        {
            // Com o modo desligado, o retorno não sobra fora do gesto (conferido em todo passo).
            Verificar(Equals(depois.RetornoDaTelaCheia, antes.RetornoDaTelaCheia), () => $"R4: {onde()}: esconder de {antes.Estado} trocou o retorno {Descrever(antes.RetornoDaTelaCheia)} por {Descrever(depois.RetornoDaTelaCheia)}");
        }
    }

    /// <summary>
    /// Uma sequência: às vezes com pedidos anteriores à carga, depois a carga e eventos de todas as
    /// origens, em lotes de 1 (ou de 1 a 4) eventos. Com <paramref name="comTamagotchi"/>, também a execução com o
    /// tamagotchi ligado (sem ele, nenhum sorteio do gerador dele acontece, e o resto da sequência é o mesmo).
    /// </summary>
    private static Sequencia GerarSequencia(int semente, bool comTamagotchi)
    {
        var rnd = new Random(semente);
        var gerador = new GeradorDeTopologias(rnd);
        var cfg = new ConfiguracaoDoNucleo
        {
            QuedaFisica = rnd.Next(2) == 0,
            PainelDeEnergiaDisponivel = rnd.Next(2) == 0,
            ConfiguracoesDisponiveis = rnd.Next(2) == 0,
            Acoes = (AcoesAutonomas)rnd.Next((int)AcoesAutonomas.Todas + 1),
            // Esconderijo pelo clique duplo (DEC-025) em um terço das sequências, escolhido pela
            // semente da sequência para não mudar os outros sorteios do gerador.
            EsconderijoNoCliqueDuplo = (uint)semente % 3 == 0,
        };
        // R-h: perfil com decisões, descansos e gestos curtos e piso variável, para o piso do
        // intervalo de acomodação importar (com o perfil padrão, todo sorteio já passa de 3 s) e o
        // gesto terminar pelo relógio antes de outro evento o interromper.
        bool perfilCurto = rnd.Next(3) == 0;
        if (perfilCurto)
            cfg = cfg with { Perfil = PerfilCurto, IntervaloDeAcomodacao = TimeSpan.FromMilliseconds(rnd.Next(300, 5001)) };
        bool emLotes = rnd.Next(3) == 0;
        bool antesDaCarga = rnd.Next(4) == 0;
        Topologia topologia = gerador.NovaTopologia();
        // A travessia das preferências (Preferencias.AtravessarMonitores) sai de um gerador próprio, com
        // semente derivada da semente da sequência: variar o campo não consome sorteios do gerador principal,
        // e os outros sorteios (e as contagens dos casos) não mudam.
        var travessia = new Random(unchecked(semente * 31 + 7));
        // A emoção dominante (DEC-027) também: os comandos e as emoções da execução com emoção saem deste gerador.
        var emocao = new Random(unchecked(semente * 37 + 11));
        // O tamagotchi (DEC-028) tem o gerador dele, outra instância com a mesma semente da da emoção: os eventos dele, a
        // adaptação dos eventos da execução principal e a emoção da execução com o tamagotchi saem só daqui. Três
        // sequências em quatro usam itens de uso curto (1 a 6 passos), para o fim do uso acontecer muitas vezes; e a
        // gravidade é quatro vezes a do aplicativo, para os itens chegarem ao chão em menos passos (sem a física, ela só
        // move os itens; a queda com os parâmetros do aplicativo fica em ItensTestes).
        var doTamagotchi = new Random(unchecked(semente * 37 + 11));
        bool usoCurto = doTamagotchi.Next(4) != 0;
        // A paranoia (pedidos do usuário de 2026-10-01) pede uma mistura de substâncias com droga sintética no mesmo episódio e
        // um sorteio de 1 em 8 que saia, o que o gerador faz pouco sozinho. Em metade das sequências, escolhida pela semente
        // (sem sorteio a mais, para a outra metade continuar como é), ele a puxa: veja ItemAInvocar e PegarUmItem.
        bool puxaAParanoia = (uint)semente % 2 == 1;
        ConfiguracaoDoNucleo cfgDoTamagotchi = cfg with { Tamagotchi = true, Fisica = cfg.Fisica with { Gravidade = cfg.Fisica.Gravidade * 4 } };
        if (usoCurto) cfgDoTamagotchi = cfgDoTamagotchi with { TabelaDeItens = ItemDeUsoCurto };
        // O baseado por conta própria (pedido do usuário de 2026-10-01, 19:10): em metade das sequências, por outro bit da
        // semente (sem sorteio a mais), a execução com o tamagotchi tem a ação UsarPorContaPropria, como o aplicativo, com o peso 6
        // em vez de 1, para ele fumar sozinho muitas vezes (e o clique interromper, e o baseado fechar a mistura). A execução
        // principal e a repetida com a chave ligada continuam sem ela (o invariante 22 é sem o baseado por conta própria).
        bool fumaSozinho = ((uint)semente >> 1) % 2 == 0;
        if (fumaSozinho)
        {
            Func<NivelDeEnergia, PerfilDeEnergia> perfil = cfgDoTamagotchi.Perfil;
            cfgDoTamagotchi = cfgDoTamagotchi with { Acoes = cfgDoTamagotchi.Acoes | AcoesAutonomas.UsarPorContaPropria, Perfil = n => perfil(n) with { PesoUsarPorContaPropria = 6 } };
        }
        var sombraDoTamagotchi = new Nucleo(cfgDoTamagotchi, (ulong)semente);
        var aplicadosDoTamagotchi = new List<(int Lote, EstadoDoNucleo Antes, Evento Evento, Resultado Resultado)>();
        var lotesDoTamagotchiComEmocao = new List<List<Evento>>();
        int lotesDoTamagotchi = 0;

        // Um núcleo-sombra acompanha a sequência, lote a lote como a execução conferida, para os
        // eventos fazerem sentido (PRESS no personagem, AUTONOMY_TIMER da geração agendada).
        var sombra = new Nucleo(cfg, (ulong)semente);
        var lotes = new List<List<Evento>>();
        var lotesComEmocao = new List<List<Evento>>();
        int total = 0;
        void EntregarAoTamagotchi(List<Evento> lote)
        {
            int indice = lotesDoTamagotchi++;
            lotesDoTamagotchiComEmocao.Add([.. lote.Select(e => ComEmocao(e, doTamagotchi))]);
            foreach (Evento e in lote) sombraDoTamagotchi.Enfileirar(e);
            EstadoDoNucleo anterior = sombraDoTamagotchi.Estado;
            sombraDoTamagotchi.Processar((evento, resultado) =>
            {
                aplicadosDoTamagotchi.Add((indice, anterior, evento, resultado));
                anterior = resultado.Estado;
            });
        }
        void Entregar(List<Evento> lote)
        {
            // Na execução com emoção, às vezes um comando de emoção antes do lote, num lote só dele. Nunca no meio
            // de um gesto curto: o comando o encerraria (invariante 15), e a sequência deixaria de ser a mesma.
            if (sombra.Estado.Gesto == Gesto.Nenhum && emocao.Next(12) == 0)
                lotesComEmocao.Add([new CmdSetDominantEmotion(EsquemaDeConfiguracoesTestes.EmocaoAleatoria(emocao))]);
            lotesComEmocao.Add([.. lote.Select(e => ComEmocao(e, emocao))]);

            // Na execução com o tamagotchi: antes do lote, às vezes eventos dele, cada grupo num lote; às vezes (só na
            // execução com emoção) um comando de emoção, fora de um gesto curto; e o lote, adaptado ao estado dela.
            if (comTamagotchi)
            {
                for (int i = 0; i < 4 && SortearDoTamagotchi(doTamagotchi, sombraDoTamagotchi.Estado, cfgDoTamagotchi, puxaAParanoia) is { } doItem; i++)
                    EntregarAoTamagotchi(doItem);
                if (sombraDoTamagotchi.Estado.Gesto == Gesto.Nenhum && doTamagotchi.Next(15) == 0)
                    lotesDoTamagotchiComEmocao.Add([new CmdSetDominantEmotion(EsquemaDeConfiguracoesTestes.EmocaoAleatoria(doTamagotchi))]);
                // A chave do conteúdo adulto (DEC-033), num lote só dela e fora de um gesto curto: desligada de vez em quando, e
                // religada logo, para o resto do fluxo continuar com os itens adultos.
                bool adulto = sombraDoTamagotchi.Estado.Preferencias.ConteudoAdulto;
                if (sombraDoTamagotchi.Estado.Gesto == Gesto.Nenhum && doTamagotchi.Next(adulto ? 40 : 6) == 0)
                    EntregarAoTamagotchi([new CmdSetAdultContent(!adulto)]);
                // A seleção individual (DEC-041), também num lote só dela: um item desmarcado de vez em quando, e religado
                // logo, para o resto do fluxo continuar com ele.
                if (sombraDoTamagotchi.Estado.Gesto == Gesto.Nenhum && doTamagotchi.Next(30) == 0)
                {
                    Item[] adultos = [.. Preferencias.TodosOsItensAdultos.Itens];
                    Item escolhido = adultos[doTamagotchi.Next(adultos.Length)];
                    bool marcado = sombraDoTamagotchi.Estado.Preferencias.ItensAdultosHabilitados.Contem(escolhido);
                    EntregarAoTamagotchi([new CmdSetAdultItemEnabled(escolhido, !marcado)]);
                }
                foreach (Item desmarcado in Preferencias.TodosOsItensAdultos.Itens.Where(i => !sombraDoTamagotchi.Estado.Preferencias.ItensAdultosHabilitados.Contem(i)))
                    if (doTamagotchi.Next(4) == 0) EntregarAoTamagotchi([new CmdSetAdultItemEnabled(desmarcado, true)]);
                EntregarAoTamagotchi([.. lote.Select(e => Adaptar(e, sombra.Estado, sombraDoTamagotchi.Estado, doTamagotchi))]);
            }

            lotes.Add(lote);
            foreach (Evento e in lote) sombra.Enfileirar(e);
            sombra.Processar();
            total += lote.Count;
        }

        if (antesDaCarga)
        {
            for (int i = rnd.Next(1, 7); i > 0; i--)
                Entregar([SortearAntesDaCarga(rnd, travessia, gerador, sombra.Estado, ref topologia)]);
        }
        Entregar([NovaCarga(rnd, travessia, gerador, topologia)]);

        while (total < EventosPorSequencia)
        {
            int tamanho = emLotes ? rnd.Next(1, 5) : 1;
            var lote = new List<Evento>(tamanho);
            for (int j = 0; j < tamanho; j++) lote.Add(Sortear(rnd, travessia, gerador, sombra.Estado, ref topologia));
            Entregar(lote);
        }
        return new Sequencia(cfg, (ulong)semente, lotes, emLotes, antesDaCarga, perfilCurto, lotesComEmocao,
            cfgDoTamagotchi, aplicadosDoTamagotchi, sombraDoTamagotchi.Descartados, lotesDoTamagotchiComEmocao, usoCurto, comTamagotchi, puxaAParanoia);
    }

    /// <summary>Os itens com o uso de 1 a 6 passos: o fim do uso acontece sem rajadas longas de TICK.</summary>
    private static DadosDoItem ItemDeUsoCurto(Item item) => TabelaDoTamagotchi.DoItem(item) with { PassosDoUso = 1 + (int)item % 6 };

    /// <summary>
    /// O evento da execução principal na execução com o tamagotchi, adaptado ao estado dela: o AUTONOMY_TIMER é o da geração
    /// agendada nela (ou o velho, se era velho), o sinal de movimento coerente com o estado dela (se era coerente) e o PRESS
    /// e o menu de contexto, no corpo dela. O resto é o mesmo evento.
    /// </summary>
    private static Evento Adaptar(Evento evento, EstadoDoNucleo principal, EstadoDoNucleo tamagotchi, Random rnd)
    {
        PontoPx NoCorpo(PontoPx p) => principal.Lugar is { } a && tamagotchi.Lugar is { } t ? new PontoPx(p.X - a.Ancora.X + t.Ancora.X, p.Y - a.Ancora.Y + t.Ancora.Y) : p;
        return evento switch
        {
            AutonomyTimer t => new AutonomyTimer(t.Geracao == principal.Geracao ? tamagotchi.Geracao : tamagotchi.Geracao - 1),
            MovementSignal m when Coerentes(principal.Estado).Contains(m.Sinal) && Coerentes(tamagotchi.Estado) is { Length: > 0 } coerentes
                => new MovementSignal(coerentes[rnd.Next(coerentes.Length)]),
            Press p => new Press(NoCorpo(p.Cursor)),
            ContextMenu c => new ContextMenu(NoCorpo(c.Cursor)),
            _ => evento,
        };
    }

    /// <summary>
    /// Um lote de eventos do tamagotchi, ou nulo para parar: rajadas de TICK com um item caindo ou um uso em curso (às vezes
    /// até o fim exato do uso), o gesto sobre o item da mão (arrastar até ele ou para longe, soltar, largar), invocar (às
    /// vezes fora do enum), pegar um item (às vezes um Id que não existe), recolher e disparos da onda (o atual, um velho
    /// ou sem onda). Escondido ou antes da carga, eventos que devem ser ignorados. Com <paramref name="puxaAParanoia"/>, no
    /// meio de um episódio, às vezes um item vai até ele (<see cref="PuxarAParanoia"/>).
    /// </summary>
    private static List<Evento>? SortearDoTamagotchi(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, bool puxaAParanoia)
    {
        if (rnd.Next(5) < 2 || s.Estado == Estado.Exiting) return null;
        if (!s.Carregado || !s.Estado.Visivel())
        {
            return rnd.Next(8) switch
            {
                0 => [new CmdSummonItem(ItemAleatorio(rnd))],
                1 => [new ItemEffectTimer(rnd.Next(2) == 0 ? s.GeracaoDaOnda : s.GeracaoDaOnda - 1)],
                2 when s.Itens.Quantidade > 0 => [new ItemPress(s.Itens.Todos[rnd.Next(s.Itens.Quantidade)].Id, new PontoPx(rnd.Next(0, 2000), rnd.Next(0, 1100)))],
                3 => [new CmdClearItems()],
                _ => null,
            };
        }
        if (puxaAParanoia && PuxarAParanoia(rnd, s, cfg) is { } puxada) return puxada;
        if (PuxarOBaseadoPorContaPropria(rnd, s, cfg) is { } doBaseado) return doBaseado;
        if (s.Estado == Estado.Using && rnd.Next(2) == 0)
            return Ticks(rnd.Next(3) == 0 ? Math.Max(1, s.PassosRestantes) : rnd.Next(1, Math.Max(2, s.PassosRestantes + 2)));
        if (s.Itens.AlgumCaindo && rnd.Next(2) == 0) return Ticks(rnd.Next(1, 40));
        if (s.Itens.NaMao is { } naMao) return ContinuarOGesto(rnd, s, cfg, naMao);
        int sorteio = rnd.Next(100);
        return sorteio switch
        {
            < 22 => [new CmdSummonItem(ItemAInvocar(rnd, s))],
            < 50 when s.Itens.Quantidade > 0 => PegarUmItem(rnd, s, cfg),
            < 52 => [new CmdClearItems()],
            < 72 when s.Onda is not null => [new ItemEffectTimer(rnd.Next(6) == 0 ? s.GeracaoDaOnda - 1 - rnd.Next(3) : s.GeracaoDaOnda)],
            < 74 => [new ItemEffectTimer(s.GeracaoDaOnda)],
            _ => null,
        };
    }

    /// <summary>
    /// Nas sequências com o baseado por conta própria (a ação UsarPorContaPropria na configuração; revisão do baseado, achado 4), às
    /// vezes o gerador leva a duas situações que ele quase nunca alcançava sozinho. Fora do chão: sem a física (os invariantes
    /// rodam sem ela), com a queda pelos sinais, ele pousa e fica em IDLE onde o pouso acabou, no ar, e ali não pode fumar; mas
    /// o pouso (12 TICKs) quase nunca terminava sem outro evento no meio, e a decisão que vale em IDLE fora do chão, com a ação
    /// e sem o Chapado na frente, saía uma vez em mil sequências. No ar, então, o contato com o chão; pousando no ar, o pouso
    /// até o fim; e, em IDLE fora do chão com a decisão pendente, o disparo dela. E, usando um baseado (o dele ou o solto pelo
    /// usuário), o menu de contexto, que abre sem interromper o uso. As outras sequências não passam por aqui e seguem como
    /// eram; nestas, o gerador segue como sempre quando devolve nulo.
    /// </summary>
    private static List<Evento>? PuxarOBaseadoPorContaPropria(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        if ((cfg.Acoes & AcoesAutonomas.UsarPorContaPropria) != AcoesAutonomas.UsarPorContaPropria || s.Lugar is not { } lugar) return null;
        if (s.Estado == Estado.Using)
            return s.Uso?.Item == Item.Baseado && rnd.Next(8) == 0 ? [new ContextMenu(new PontoPx(lugar.Ancora.X, lugar.Ancora.Y - 20))] : null;
        if (lugar.Ancora.Y == lugar.Monitor.AreaUtil.Base) return null;
        return s.Estado switch
        {
            Estado.Falling or Estado.Jumping when rnd.Next(3) == 0 => [new MovementSignal(SinalDeMovimento.ContatoComOChao)],
            Estado.Landing when rnd.Next(2) == 0 => Ticks(Math.Max(1, s.PassosRestantes)),
            Estado.Idle when s.DecisaoAgendada && rnd.Next(4) == 0 => [new AutonomyTimer(s.Geracao)],
            _ => null,
        };
    }

    private static List<Evento> Ticks(int quantos) => [.. Enumerable.Repeat<Evento>(new Tick(), quantos)];

    /// <summary>Um item do enum; uma vez em quinze, um valor fora dele, que a invocação ignora.</summary>
    private static Item ItemAleatorio(Random rnd) => rnd.Next(15) == 0 ? (Item)rnd.Next(13, 40) : (Item)rnd.Next(13);

    /// <summary>
    /// O item a invocar: com uma onda de fundo, metade das vezes um item de alívio (a comida e a bebida sem álcool), para o
    /// alívio que acaba a onda da frente e traz a de fundo de volta acontecer muitas vezes, e não por sorte; senão,
    /// qualquer um (<see cref="ItemAleatorio"/>).
    /// </summary>
    private static Item ItemAInvocar(Random rnd, EstadoDoNucleo s)
        => s.OndaDeFundo is not null && rnd.Next(2) == 0 ? ItensDeAlivio[rnd.Next(ItensDeAlivio.Length)] : ItemAleatorio(rnd);

    /// <summary>
    /// Puxando a paranoia (pedidos do usuário de 2026-10-01), no meio de um episódio (com carga) e com ele fora do uso e sem
    /// item na mão, metade das vezes um item vai até ele. Com a paranoia na frente, metade das vezes um de alívio, para ela
    /// ser acalmada, e metade uma substância, para ela subir de nível antes de os disparos a acabarem. Sem ela e sem droga
    /// sintética no episódio, dois terços das vezes uma sintética, para a mistura acontecer, e um terço outra substância (a
    /// mistura sem sintética, que não sorteia); com sintética, uma vez em quatro, um item de substância já usado no episódio
    /// (a sintética sozinha, repetida), e nas outras, qualquer substância: a que fecha a mistura faz o sorteio do episódio, e,
    /// depois dele, as outras não sorteiam mais (um sorteio por episódio, a escolha do usuário às 23:03). O item é um dessa
    /// classe já no mundo, com o gesto inteiro até ele, ou um novo, invocado, que vai até ele num lote seguinte. Sem isso, a
    /// mistura com sintética quase nunca se completava num episódio, e a subida de nível não acontecia. Nulo na outra metade:
    /// o gerador segue como sempre.
    /// </summary>
    private static List<Evento>? PuxarAParanoia(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        if (s.Carga.Substancias == 0 || s.Estado == Estado.Using || s.Itens.NaMao is not null || rnd.Next(2) == 0) return null;
        Item[] classe;
        if (s.Onda?.Tipo == Onda.Paranoico) classe = rnd.Next(2) == 0 ? ItensDeAlivio : ItensDeSubstancia;
        else if (!s.Carga.Sintetica) classe = rnd.Next(3) == 0 ? OutrasSubstancias : ItensSinteticos;
        else classe = rnd.Next(4) == 0 ? [.. s.Carga.Distintas.Itens] : ItensDeSubstancia;
        ItemNoMundo[] noMundo = [.. s.Itens.Todos.Where(i => classe.Contains(i.Item))];
        return noMundo.Length > 0 ? GestoAteEle(rnd, s, cfg, noMundo[rnd.Next(noMundo.Length)]) : [new CmdSummonItem(classe[rnd.Next(classe.Length)])];
    }

    /// <summary>
    /// Pega um item (às vezes um Id que não existe): só pega, e o gesto segue nos lotes seguintes, com outros eventos no
    /// meio; ou clica nele (pega e larga); ou o gesto inteiro, até ele ou para longe. Com uma onda de fundo e um item de
    /// alívio no mundo, o gesto inteiro de um deles até ele: um passo do alívio por vez, até a onda da frente acabar e a de
    /// fundo voltar (o alívio, pedido do usuário de 2026-10-01; sem isso, o caso saía por sorte, de nenhuma a três vezes).
    /// </summary>
    private static List<Evento> PegarUmItem(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        if (s.OndaDeFundo is not null && s.Itens.Todos.Where(i => ItensDeAlivio.Contains(i.Item)).ToArray() is { Length: > 0 } deAlivio)
            return GestoAteEle(rnd, s, cfg, deAlivio[rnd.Next(deAlivio.Length)]);
        ItemNoMundo item = s.Itens.Todos[rnd.Next(s.Itens.Quantidade)];
        int id = rnd.Next(15) == 0 ? s.ProximoIdDeItem + rnd.Next(3) : item.Id;
        var press = new ItemPress(id, new PontoPx(item.Lugar.Ancora.X + rnd.Next(-20, 21), item.Lugar.Ancora.Y - rnd.Next(2, 44)));
        var pegada = new PontoPx(press.Cursor.X - item.Lugar.Ancora.X, press.Cursor.Y - item.Lugar.Ancora.Y);
        PontoPx cursor = Mais(Alvo(rnd, s, cfg, sobreEle: rnd.Next(5) < 3), pegada);
        return rnd.Next(3) switch
        {
            0 => [press],
            1 => [press, new ItemRelease(id)],
            _ => [press, new ItemDragStart(id), new ItemDragMove(id, cursor), new ItemDragEnd(id, cursor)],
        };
    }

    /// <summary>O gesto inteiro do item até ele: pega 10 px acima da âncora, arrasta e solta perto do meio dele.</summary>
    private static List<Evento> GestoAteEle(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, ItemNoMundo item)
    {
        var pegou = new ItemPress(item.Id, new PontoPx(item.Lugar.Ancora.X, item.Lugar.Ancora.Y - 10));
        PontoPx sobre = Mais(Alvo(rnd, s, cfg, sobreEle: true), new PontoPx(0, -10));
        return [pegou, new ItemDragStart(item.Id), new ItemDragMove(item.Id, sobre), new ItemDragEnd(item.Id, sobre)];
    }

    /// <summary>O gesto sobre o item da mão continua (ou espera, para outros eventos passarem com o item seguro).</summary>
    private static List<Evento>? ContinuarOGesto(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, ItemNoMundo naMao)
    {
        if (rnd.Next(3) == 0) return null;
        if (rnd.Next(30) == 0) return [new CmdClearItems()];
        // Às vezes um PRESS noutro item com este ainda na mão (dois ponteiros): o da mão é largado, com a captura solta.
        if (rnd.Next(20) == 0 && s.Itens.Todos.FirstOrDefault(i => i.Id != naMao.Id) is { } outro)
            return [new ItemPress(outro.Id, new PontoPx(outro.Lugar.Ancora.X, outro.Lugar.Ancora.Y - 10))];
        if (naMao.Situacao == SituacaoDoItem.Segurado)
            return rnd.Next(4) == 0 ? [new ItemRelease(naMao.Id)] : [new ItemDragStart(naMao.Id)];
        PontoPx Cursor(bool sobreEle) => Mais(Alvo(rnd, s, cfg, sobreEle), naMao.Pegada);
        int sorteio = rnd.Next(10);
        if (sorteio < 3) return [new ItemDragMove(naMao.Id, Cursor(rnd.Next(2) == 0))];
        if (sorteio < 7)
        {
            PontoPx sobre = Cursor(sobreEle: true);
            return [new ItemDragMove(naMao.Id, sobre), new ItemDragEnd(naMao.Id, sobre)];
        }
        return sorteio < 9 ? [new ItemDragEnd(naMao.Id, Cursor(sobreEle: false))] : [new ItemRelease(naMao.Id)];
    }

    /// <summary>
    /// Onde pôr a âncora do item: perto do meio do personagem (o item centrado no sprite dele, com um desvio de até um
    /// terço do sprite), ou num ponto qualquer de um monitor, às vezes um pouco fora dele.
    /// </summary>
    private static PontoPx Alvo(Random rnd, EstadoDoNucleo s, ConfiguracaoDoNucleo cfg, bool sobreEle)
    {
        if (sobreEle && s.Lugar is { } l)
        {
            int alturaDoItem = cfg.TamanhoDoItem.ParaPixels(l.Monitor.Dpi).Altura;
            int dx = l.Tamanho.Largura / 3, dy = l.Tamanho.Altura / 3;
            return new PontoPx(l.Ancora.X + rnd.Next(-dx, dx + 1), l.Retangulo.Topo + l.Tamanho.Altura / 2 + alturaDoItem / 2 + rnd.Next(-dy, dy + 1));
        }
        IReadOnlyList<MonitorDoDesktop> monitores = s.Topologia!.Monitores;
        RetanguloPx tela = monitores[rnd.Next(monitores.Count)].Tela;
        return new PontoPx(rnd.Next(tela.Esquerda - 100, tela.Direita + 100), rnd.Next(tela.Topo - 100, tela.Base + 100));
    }

    private static PontoPx Mais(PontoPx a, PontoPx b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Os sinais de movimento que o estado trata (como o passo físico da Fase 4 os emite).</summary>
    private static SinalDeMovimento[] Coerentes(Estado estado) => estado switch
    {
        Estado.Walking => [SinalDeMovimento.Parede, SinalDeMovimento.Passagem, SinalDeMovimento.FimDoChao],
        Estado.Climbing => [SinalDeMovimento.TopoDaParede, SinalDeMovimento.FimDaParede, SinalDeMovimento.BordaSuperior],
        Estado.Hanging => [SinalDeMovimento.FimDaBorda],
        Estado.Jumping or Estado.Falling => [SinalDeMovimento.ContatoComOChao],
        _ => [],
    };

    /// <summary>O evento da execução com emoção: a carga e SETTINGS_CHANGED ganham uma emoção dominante (às vezes nula ou fora das 14).</summary>
    private static Evento ComEmocao(Evento evento, Random emocao) => evento switch
    {
        Loaded carga => carga with { Preferencias = carga.Preferencias with { EmocaoDominante = EsquemaDeConfiguracoesTestes.EmocaoAleatoria(emocao) } },
        SettingsChanged configuracoes => configuracoes with { Preferencias = configuracoes.Preferencias with { EmocaoDominante = EsquemaDeConfiguracoesTestes.EmocaoAleatoria(emocao) } },
        _ => evento,
    };

    /// <summary>O evento sem a emoção dominante: o da execução principal.</summary>
    private static Evento SemEmocao(Evento evento) => evento switch
    {
        Loaded carga => carga with { Preferencias = carga.Preferencias with { EmocaoDominante = null } },
        SettingsChanged configuracoes => configuracoes with { Preferencias = configuracoes.Preferencias with { EmocaoDominante = null } },
        _ => evento,
    };

    /// <summary>O efeito sem a emoção dominante nas preferências gravadas.</summary>
    private static Efeito SemEmocao(Efeito efeito)
        => efeito is GravarPreferencias g ? g with { Preferencias = g.Preferencias with { EmocaoDominante = null } } : efeito;

    /// <summary>O estado sem a cara e sem a emoção dominante: o que a emoção não pode mudar (invariante 27).</summary>
    private static EstadoDoNucleo SemAsCaras(EstadoDoNucleo s)
        => s with { Expressao = Expressao.Neutro, Preferencias = s.Preferencias with { EmocaoDominante = null } };

    /// <summary>
    /// Pedidos que podem chegar antes da carga (R6): esconder, mostrar, sessão, suspensão,
    /// topologia, tela cheia, configurações, e às vezes qualquer outro evento.
    /// </summary>
    private static Evento SortearAntesDaCarga(Random rnd, Random travessia, GeradorDeTopologias gerador, EstadoDoNucleo s, ref Topologia topologia) => rnd.Next(12) switch
    {
        0 => new CmdHide(),
        1 => new CmdShow(),
        2 => new SessionLocked(),
        3 => new SessionUnlocked(),
        4 => new Suspending(),
        5 => new Resumed(),
        6 => NovaTopologia(rnd, gerador, ref topologia),
        7 => TelaCheia(rnd, topologia),
        8 => new SettingsChanged(new Preferencias(Nivel(rnd), rnd.Next(3) != 0, Atravessar(travessia)).ComAdultosAleatorios(rnd)),
        9 => new CmdResetPosition(),
        10 => rnd.Next(2) == 0 ? new CmdPauseAutonomy() : new CmdResumeAutonomy(),
        _ => Sortear(rnd, travessia, gerador, s, ref topologia),
    };

    /// <summary>
    /// Carga com preferências às vezes inválidas e, metade das vezes, uma posição salva: num monitor
    /// da topologia ou numa chave que ela não tem, com frações às vezes fora de [0, 1] ou NaN. A tela
    /// salva é a do monitor em que a âncora salva está, se ela está em algum; senão, desconhecida. Ela
    /// sai da âncora já sorteada, sem sorteio novo, para não mudar os outros sorteios do gerador: com a
    /// chave desconhecida, leva a restauração pelo retângulo ou, sem ela, ao principal.
    /// </summary>
    private static Loaded NovaCarga(Random rnd, Random travessia, GeradorDeTopologias gerador, Topologia topologia)
    {
        PosicaoDoPersonagem? salva = null;
        if (rnd.Next(2) == 0)
        {
            string chave = rnd.Next(4) == 0 ? ChaveDesconhecida : topologia.Monitores[rnd.Next(topologia.Monitores.Count)].Chave;
            double Fracao() => rnd.Next(5) == 0 ? gerador.Fracao() : rnd.NextDouble();
            double fx = Fracao(), fy = Fracao();
            PontoPx ancora = gerador.Ponto(topologia);
            salva = new PosicaoDoPersonagem(chave, fx, fy, ancora) { TelaDoMonitor = topologia.MonitorQueContem(Posicionador.PixelDosPes(ancora))?.Tela };
        }
        return new Loaded(topologia, salva, new Preferencias(Nivel(rnd), rnd.Next(4) != 0, Atravessar(travessia)).ComAdultosAleatorios(rnd));
    }

    private static Evento Sortear(Random rnd, Random travessia, GeradorDeTopologias gerador, EstadoDoNucleo s, ref Topologia topologia)
    {
        PontoPx ancora = s.Lugar?.Ancora ?? new PontoPx(0, 0);
        Topologia atual = topologia;
        PontoPx NoCorpo() => new(ancora.X + rnd.Next(-20, 21), ancora.Y - rnd.Next(5, 60));
        PontoPx Qualquer() => gerador.Ponto(atual);

        // Durante um gesto curto o relógio corre a 60 Hz: metade das vezes, o próximo evento é um
        // TICK, para o gesto também terminar pelo relógio, e não só interrompido.
        if (s.Gesto != Gesto.Nenhum && rnd.Next(2) == 0) return new Tick();

        int sorteio = rnd.Next(100);
        return sorteio switch
        {
            < 18 => new Tick(),
            < 24 => new Press(NoCorpo()),
            < 27 => new Click(),
            < 29 => new DoubleClick(),
            < 32 => new DragStart(),
            < 40 => new DragMove(Qualquer()),
            < 43 => new DragEnd(Qualquer()),
            < 44 => new DragCancel(),
            < 45 => new ContextMenu(NoCorpo()),
            < 47 => new EnergyPanelOpen(),
            < 48 => new EnergySelected(Nivel(rnd)),
            < 49 => new EnergyPanelClose(),
            < 51 => new CmdHide(),
            < 53 => new CmdShow(),
            < 54 => new CmdPauseAutonomy(),
            < 55 => new CmdResumeAutonomy(),
            < 56 => new CmdOpenSettings(),
            < 57 => new CmdResetPosition(),
            < 58 => rnd.Next(20) == 0 ? new CmdExit() : new Tick(),
            < 61 => NovaTopologia(rnd, gerador, ref topologia),
            < 62 => new SessionLocked(),
            < 63 => new SessionUnlocked(),
            < 64 => new Suspending(),
            < 65 => new Resumed(),
            < 66 => rnd.Next(20) == 0 ? new SessionEnding() : new Tick(),
            < 70 => TelaCheia(rnd, atual),
            < 71 => new SettingsChanged(new Preferencias(Nivel(rnd), rnd.Next(3) != 0, Atravessar(travessia)).ComAdultosAleatorios(rnd)),
            < 78 => new MovementSignal(SinalCoerente(rnd, s.Estado)),
            < 90 => new AutonomyTimer(rnd.Next(10) == 0 ? s.Geracao - 1 : s.Geracao),
            // R-e: carga repetida no meio da sequência, com outra topologia; deve ser ignorada.
            < 91 => NovaCarga(rnd, travessia, gerador, rnd.Next(2) == 0 ? atual : gerador.NovaTopologia()),
            // As 14 caras de humor: um valor novo no fim do enum não muda os sorteios do gerador (F1).
            _ => new ExpressionChange((Expressao)rnd.Next(14)),
        };
    }

    /// <summary>
    /// Sinal do movimento: dois terços das vezes um que o estado atual trata (como o passo físico da
    /// Fase 4 emitirá), para a sequência chegar a HANGING, pousar e sair da parede; no resto, qualquer um.
    /// </summary>
    private static SinalDeMovimento SinalCoerente(Random rnd, Estado estado)
    {
        SinalDeMovimento[] coerentes = Coerentes(estado);
        return coerentes.Length > 0 && rnd.Next(3) != 0
            ? coerentes[rnd.Next(coerentes.Length)]
            : (SinalDeMovimento)rnd.Next(Enum.GetValues<SinalDeMovimento>().Length);
    }

    /// <summary>Monitores ocupados: qualquer subconjunto dos presentes e, às vezes, uma chave que a topologia não tem.</summary>
    private static FullscreenTargetsChanged TelaCheia(Random rnd, Topologia topologia)
    {
        List<string> chaves = [.. topologia.Monitores.Select(m => m.Chave).Where(_ => rnd.Next(2) == 0)];
        if (rnd.Next(5) == 0) chaves.Add(rnd.Next(2) == 0 ? ChaveDesconhecida : GeradorDeTopologias.Chave(rnd.Next(1, 10)));
        return new FullscreenTargetsChanged(new MonitoresOcupados(chaves));
    }

    /// <summary>Travessia entre monitores nas preferências: desligada uma vez em quatro, pelo gerador próprio dela.</summary>
    private static bool Atravessar(Random travessia) => travessia.Next(4) != 0;

    /// <summary>Nível de energia; uma vez em dez, fora do enum (3 a 99), como um arquivo adulterado (SECURITY.md 7).</summary>
    private static NivelDeEnergia Nivel(Random rnd) => rnd.Next(10) == 0 ? (NivelDeEnergia)rnd.Next(3, 100) : (NivelDeEnergia)rnd.Next(3);

    private static TopologyChanged NovaTopologia(Random rnd, GeradorDeTopologias gerador, ref Topologia topologia)
    {
        topologia = rnd.Next(3) == 0 ? gerador.NovaTopologia() : gerador.Mudar(topologia);
        return new TopologyChanged(topologia);
    }

    /// <summary>Perfis com decisões de 100 a 900 ms, descansos de 200 a 1500 ms e gestos de 1 a 5 passos.</summary>
    private static PerfilDeEnergia PerfilCurto(NivelDeEnergia nivel) => PerfilDeEnergia.Padrao(nivel) with
    {
        DecisaoMinima = TimeSpan.FromMilliseconds(100),
        DecisaoMaxima = TimeSpan.FromMilliseconds(900),
        DescansoMinimo = TimeSpan.FromMilliseconds(200),
        DescansoMaximo = TimeSpan.FromMilliseconds(1500),
        PassosDoGestoMinimo = 1,
        PassosDoGestoMaximo = 5,
    };

    /// <summary>As preferências como a carga deve guardá-las: nível fora do enum vira Média, e emoção fora das 14 caras de humor, a automática (R1).</summary>
    private static Preferencias Saneadas(Preferencias p)
    {
        if (!Enum.IsDefined(p.Energia)) p = p with { Energia = NivelDeEnergia.Media };
        if (p.EmocaoDominante is { } e && !DeHumor(e)) p = p with { EmocaoDominante = null };
        return p;
    }

    /// <summary>As 14 caras de humor são os valores de 0 (Neutro) a 13 (Determinado), escritos aqui à parte do núcleo.</summary>
    private static bool DeHumor(Expressao e) => (int)e is >= 0 and <= 13;

    private static TimeSpan Maior(TimeSpan a, TimeSpan b) => a > b ? a : b;

    /// <summary>Se a âncora está na área útil de algum monitor: dentro na horizontal, com os pés entre o topo (exclusivo) e o chão.</summary>
    private static bool NaAreaUtil(Topologia topologia, PontoPx a)
        => topologia.Monitores.Any(mon => a.X >= mon.AreaUtil.Esquerda && a.X < mon.AreaUtil.Direita && a.Y > mon.AreaUtil.Topo && a.Y <= mon.AreaUtil.Base);

    /// <summary>Linha de um evento aplicado: o evento, as transições, os efeitos e o retrato, no formato das reproduções.</summary>
    private static string Registrar(Evento evento, Resultado r)
    {
        var sb = new StringBuilder(Gravacao.Escrever(evento, t => t.ImpressaoDigital));
        foreach (Transicao t in r.Transicoes) sb.Append(" ~ ").Append(t);
        foreach (Efeito e in r.Efeitos) sb.Append(" ! ").Append(Gravacao.DescreverEfeito(e));
        sb.Append(" = ").Append(r.Estado.Retrato().Descrever());
        return sb.ToString();
    }

    private static string Descrever(IEnumerable<Transicao> transicoes) => string.Join(", ", transicoes.Select(t => $"{t.De}->{t.Para}"));

    private static string Descrever(PosicaoDoPersonagem? p) => p is null ? "nenhuma" : Gravacao.DescreverPosicao(p);

    private static void Verificar(bool condicao, Func<string> mensagem)
    {
        if (!condicao) Afirmar.Falhar(mensagem());
    }
}
