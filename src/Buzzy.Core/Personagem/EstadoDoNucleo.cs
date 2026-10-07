using System.Globalization;

namespace Buzzy.Core.Personagem;

// Tudo o que o núcleo sabe entre dois eventos, imutável. O gerador mora no
// estado, então mesma sequência de eventos sobre o mesmo estado inicial dá
// sempre o mesmo resultado.
public sealed record EstadoDoNucleo
{
    public Estado Estado { get; init; } = Estado.Booting;

    // Só a primeira carga (Loaded) vale. Mostrar, esconder, sessão e
    // topologia que chegam antes ficam guardados até ela.
    public bool Carregado { get; init; }

    // Nenhum fora de Hidden.
    public MotivoDoOcultamento Motivo { get; init; }

    // Em cache; nula antes de carregar.
    public Topologia? Topologia { get; init; }

    // Onde a janela está (ou vai aparecer). Nulo antes de carregar.
    public Posicionamento? Lugar { get; init; }

    // Posição fina, velocidade e plano do movimento. Ao entrar num estado de
    // movimento, parte da âncora de Lugar.
    public EstadoDoMovimento Movimento { get; init; } = EstadoDoMovimento.Nenhum;

    // A mesma posição relativa à área útil do monitor, pra sobreviver a troca
    // de topologia. Acompanha toda mudança, em qualquer estado, sem mover a
    // janela (Posicionador.Rebasear): escondido, continua preso à chave do monitor.
    public PosicaoDoPersonagem? Posicao { get; init; }

    // Cursor menos âncora no PRESS.
    public PontoPx Pegada { get; init; }

    public Direcao Direcao { get; init; }

    public Expressao Expressao { get; init; }

    public Gesto Gesto { get; init; }

    public int PassosDoGesto { get; init; }

    // Passos que faltam em Reacting, Landing ou Using.
    public int PassosRestantes { get; init; }

    public bool AutonomiaPausada { get; init; }

    public bool PainelAberto { get; init; }

    public Preferencias Preferencias { get; init; } = Preferencias.Padrao;

    public Aleatorio Aleatorio { get; init; }

    // Geração do último agendamento da agenda autônoma.
    public long Geracao { get; init; }

    // Há um AutonomyTimer da geração atual pendente.
    public bool DecisaoAgendada { get; init; }

    public bool RelogioAtivo { get; init; }

    // Monitores cobertos por tela cheia, em cache.
    public MonitoresOcupados Ocupados { get; init; } = MonitoresOcupados.Nenhum;

    // Posição de antes da mudança automática por tela cheia, só em memória.
    // "Não age de novo até a próxima mudança" já sai da regra de uma vez por
    // FullscreenTargetsChanged, sem campo extra. Acompanha a topologia como a posição.
    public PosicaoDoPersonagem? RetornoDaTelaCheia { get; init; }

    // A tela cheia mudou durante PRESSED ou DRAGGING: ao soltar, vale o ponto
    // do usuário e o retorno temporário é descartado.
    public bool TelaCheiaMudouNoGesto { get; init; }

    // Posto pelo usuário numa lateral ou no cipó de cima: fica lá até o
    // usuário tirar. A agenda só passeia pela mesma superfície; nunca salta,
    // solta nem desce sozinho. Clique não tira; arrastar pra outro lugar, sim.
    public bool PresoPeloUsuario { get; init; }

    // Borda onde está escondido, ou Nenhum. Sobrevive a PRESSED e REACTING (o
    // primeiro clique do clique duplo), a HIDDEN e às revalidações: toda
    // acomodação de quem está escondido o devolve ao esconderijo.
    public LadoDoEsconderijo Esconderijo { get; init; }

    // Padrao fora de REACTING e sem Personalidade.
    public VarianteDaReacao Reacao { get; init; }

    // De que estado veio o PRESS, pra reação escolher a variante.
    public Estado ReagiuDe { get; init; }

    // Desde a última decisão autônoma ou o último arraste; só com a personalidade.
    public int CliquesSeguidos { get; init; }

    // Curiosidade: só chaves opacas de monitor e contadores. Nenhum retângulo, vão, instante ou janela.

    // Chave do monitor da janela em primeiro plano; nula antes da primeira.
    public string? FocoDoPrimeiroPlano { get; init; }

    public EstagioDaCuriosidade Curiosidade { get; init; }

    // Uma ida por foco (feita ou encerrada).
    public bool IdaFeitaNesteFoco { get; init; }

    // Foco do começo do episódio, pra ver no fim se mudou no meio; nulo sem episódio.
    public string? FocoDoEpisodio { get; init; }

    // De onde a ida partiu, pra replanejar num monitor do meio do caminho.
    public string? MonitorDaIda { get; init; }

    // Dispensa pelo usuário: até disparar, a curiosidade não o move, e nada a apaga.
    public bool DispensaAtiva { get; init; }

    // Muda a cada agendamento.
    public long GeracaoDaCuriosidade { get; init; }

    public bool CuriosidadeAgendada { get; init; }

    // Do último pedido do vão.
    public long GeracaoDoVao { get; init; }

    // Guardado na chegada; o vão em si é descartado.
    public Direcao LadoDoOlhar { get; init; }

    // Em decisões da agenda.
    public int RodadasOlhando { get; init; }

    // Onda do último item usado, ou a paranoia, na frente. Nula sem onda. Só
    // em memória, nunca gravada; com o tamagotchi desligado, não vale.
    public EstadoDaOnda? Onda { get; init; }

    // Contribuições por item da onda da frente, só em memória.
    public ContribuicoesDaOnda FontesDaOnda { get; init; }

    // A que estava na frente quando chegou outra de precedência maior ou
    // igual. Fica congelada (sem timer nem efeito) e volta à frente, com a
    // fase recomeçada, quando a da frente acaba. Só cabem duas: uma terceira
    // descarta a de fundo.
    public EstadoDaOnda? OndaDeFundo { get; init; }

    public ContribuicoesDaOnda FontesDaOndaDeFundo { get; init; }

    // Carga do episódio: quantos itens de substância, se algum era sintético,
    // quais itens distintos e se já sorteou. Cada item entra depois da
    // combinação; o uso que fecha uma mistura com sintética, sem paranoia na
    // frente, sorteia a onda Paranoico, uma vez por episódio, com
    // AleatorioDaParanoia. Zera no fim de todo evento em que nem a onda da
    // frente nem a de fundo é de substância (paranoia conta). Só em memória.
    public CargaDaParanoia Carga { get; init; } = CargaDaParanoia.Nenhuma;

    // Gerador separado só pros sorteios da paranoia (no máximo um por
    // episódio), pra agenda, caras e reproduções gravadas não mudarem por
    // causa deles. Não reinicia com o episódio. Só em memória.
    public Aleatorio AleatorioDaParanoia { get; init; }

    // Mesma ideia, pros sorteios da personalidade e da curiosidade (tipo de
    // gesto, explorar borda, rodadas e escolhas do olhar). Só é consumido com
    // Personalidade ou Curiosidade ligadas. Só em memória.
    public Aleatorio AleatorioDaPersonalidade { get; init; }

    // Do último agendamento do timer da onda.
    public long GeracaoDaOnda { get; init; }

    // Há um ItemEffectTimer da geração atual pendente.
    public bool OndaAgendada { get; init; }

    // Só em Using.
    public Uso? Uso { get; init; }

    // No máximo MaximoDeItens. Só em memória, nunca gravados; somem ao sair.
    public ItensNoMundo Itens { get; init; } = ItensNoMundo.Nenhum;

    // Cada Id é usado uma vez só.
    public int ProximoIdDeItem { get; init; } = 1;

    // Usuário segurando um item: fica atento, parado, sem decisão autônoma,
    // até o item sair da mão.
    public bool Atento => Itens.NaMao is not null;

    // Relógio lógico: passos fixos já aplicados.
    public long Passos { get; init; }

    // Acontecimento pontual do último evento, pra apresentação.
    public Sinal Sinal { get; init; }

    public static EstadoDoNucleo Inicial(ulong semente) => new()
    {
        Aleatorio = new Aleatorio(semente),
        AleatorioDaParanoia = new Aleatorio(SementeDaParanoia(semente)),
        AleatorioDaPersonalidade = new Aleatorio(SementeDaPersonalidade(semente)),
    };

    // semente × 41 + 13, 64 bits sem sinal.
    public static ulong SementeDaParanoia(ulong semente) => unchecked((semente * 41) + 13);

    // semente × 47 + 19, 64 bits sem sinal.
    public static ulong SementeDaPersonalidade(ulong semente) => unchecked((semente * 47) + 19);

    // O que a apresentação e os testes enxergam.
    public Retrato Retrato() => new(
        Estado,
        Motivo,
        Lugar?.Ancora ?? default,
        Lugar?.Monitor.Chave ?? "",
        Lugar?.Tamanho ?? default,
        Direcao,
        Expressao,
        Gesto,
        AutonomiaPausada,
        PainelAberto,
        Preferencias.Energia,
        RelogioAtivo,
        Sinal)
    {
        EmocaoDominante = Preferencias.EmocaoDominante,
        ConteudoAdulto = Preferencias.ConteudoAdulto,
        Onda = Onda,
        OndaDeFundo = OndaDeFundo,
        Carga = Carga.Substancias,
        Uso = Uso,
        PassoDoUso = Uso is { } uso ? uso.Passos - PassosRestantes : 0,
        Itens = Itens,
        Reacao = Reacao,
        NaBorda = Estado == Estado.Idle && Gesto == Gesto.Espiar && Movimento.ExplorandoBorda,
        Olhando = Curiosidade == EstagioDaCuriosidade.Olhando && Estado == Estado.Idle && Gesto == Gesto.Nenhum,
    };
}

// O que a apresentação desenha e o que as reproduções gravadas comparam.
public sealed record Retrato(
    Estado Estado,
    MotivoDoOcultamento Motivo,
    PontoPx Ancora,
    string ChaveMonitor,
    TamanhoPx Tamanho,
    Direcao Direcao,
    Expressao Expressao,
    Gesto Gesto,
    bool AutonomiaPausada,
    bool PainelAberto,
    NivelDeEnergia Energia,
    bool RelogioAtivo,
    Sinal Sinal)
{
    // Pra marca no menu; nula = "Automática".
    public Expressao? EmocaoDominante { get; init; }

    // Pra marca no menu; desligado por padrão.
    public bool ConteudoAdulto { get; init; }

    // Pras sobreposições da apresentação; nula sem onda.
    public EstadoDaOnda? Onda { get; init; }

    public EstadoDaOnda? OndaDeFundo { get; init; }

    // Itens de substância usados no episódio; 0 sem episódio.
    public int Carga { get; init; }

    // Item, verbo, duração e apoio, pra pose de uso; nulo fora de USING.
    public Uso? Uso { get; init; }

    // De 0 a duração - 1, pro quadro da animação; 0 fora de USING.
    public int PassoDoUso { get; init; }

    // Inclui o que está na mão do usuário.
    public ItensNoMundo Itens { get; init; } = ItensNoMundo.Nenhum;

    public VarianteDaReacao Reacao { get; init; }

    // Espiando pra fora da lateral que foi explorar: muda a pose.
    public bool NaBorda { get; init; }

    // Parado olhando a janela em primeiro plano: muda a pose.
    public bool Olhando { get; init; }

    // Linha canônica (cultura invariante) das reproduções gravadas. Os campos
    // opcionais (onda=, fundo=, carga=, uso=, emocao=, adulto=nao, itens=,
    // reacao=, borda=, olhando=) só aparecem quando têm valor, pra linha sem
    // eles continuar igual à das referências gravadas antigas.
    public string Descrever()
    {
        string estado = Estado == Estado.Hidden ? $"Hidden({Motivo})" : Estado.ToString();
        string onda = Onda is { } o ? $" onda={o.Tipo}/{o.Fase}/{o.Nivel}" : "";
        string fundo = OndaDeFundo is { } f ? $" fundo={f.Tipo}/{f.Fase}/{f.Nivel}" : "";
        string carga = Carga > 0 ? string.Create(CultureInfo.InvariantCulture, $" carga={Carga}") : "";
        string uso = Uso is { } u ? string.Create(CultureInfo.InvariantCulture, $" uso={u.Item}/{u.Verbo}/{PassoDoUso}de{u.Passos}/{u.Apoio}") : "";
        string emocao = EmocaoDominante is { } e ? $" emocao={e}" : "";
        string adulto = ConteudoAdulto ? "" : " adulto=nao";
        string itens = Itens.Quantidade > 0 ? $" itens=[{Itens}]" : "";
        string reacao = Reacao != VarianteDaReacao.Padrao ? $" reacao={Reacao.ToString().ToLowerInvariant()}" : "";
        string borda = NaBorda ? " borda=sim" : "";
        string olhando = Olhando ? " olhando=sim" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"{estado} ancora=({Ancora.X},{Ancora.Y}) monitor={ChaveMonitor} tamanho={Tamanho.Largura}x{Tamanho.Altura} direcao={Direcao} expressao={Expressao} gesto={Gesto} pausada={SimNao(AutonomiaPausada)} painel={SimNao(PainelAberto)} energia={Energia} relogio={SimNao(RelogioAtivo)} sinal={Sinal}{onda}{fundo}{carga}{uso}{emocao}{adulto}{itens}{reacao}{borda}{olhando}");
    }

    private static string SimNao(bool valor) => valor ? "sim" : "nao";

    public override string ToString() => Descrever();
}
