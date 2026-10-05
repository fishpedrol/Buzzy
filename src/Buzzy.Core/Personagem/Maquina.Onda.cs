using System.Globalization;

namespace Buzzy.Core.Personagem;

/// <summary>
/// A onda de desenho animado de um item (DEC-028; desenho do núcleo, 4.2 a 4.5 e 4.9), atrás da chave
/// <see cref="ConfiguracaoDoNucleo.Tamagotchi"/>: com ela desligada, uma onda no estado não vale e nenhum efeito novo sai
/// do núcleo. A onda avança só nos disparos únicos do próprio temporizador (<see cref="AgendarOnda"/> e
/// <see cref="ItemEffectTimer"/>), sem relógio de passo fixo, e muda pesos, intervalos, gestos, caras e as três
/// velocidades, com o cambaleio (exceção documentada ao invariante 12). Só a onda da frente vale; a de fundo fica
/// congelada até a da frente acabar (4.5). Comer e beber algo sem álcool acalmam a onda da frente aos poucos, um passo
/// por item (o alívio, pedido do usuário de 2026-10-01). E quem mistura substâncias com droga sintética pode ficar
/// paranoico, de desenho animado, achando que tem alguém no teto (a paranoia, outros pedidos do mesmo dia): o uso que fecha
/// um episódio de mistura com sintética sorteia, uma vez por episódio e com a chance de 1 em 8, a onda
/// <see cref="Onda.Paranoico"/> na frente.
/// </summary>
public static partial class Maquina
{
    /// <summary>Passos de uma volta do cambaleio: 0,8 s a 60 passos por segundo.</summary>
    public const int PassosDoCambaleio = 48;

    /// <summary>
    /// Quanto dura o olhar pro teto do começo da paranoia (<see cref="Gesto.OlharProTeto"/>), em passos do relógio: 1,5 s a
    /// 60 por segundo, fixo, sem sorteio.
    /// </summary>
    public const int PassosDoOlharProTeto = 90;

    /// <summary>
    /// O perfil da fase da onda em curso (tabelas 4.3 e 4.4), ou nulo: sem onda, ou com o tamagotchi desligado, em que
    /// uma onda no estado não vale.
    /// </summary>
    public static PerfilDaOnda? PerfilDaFase(EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(cfg);
        return cfg.Tamagotchi && s.Onda is { } onda ? cfg.TabelaDeOndas(onda.Tipo).Perfil(onda.Fase, onda.Nivel) : null;
    }

    /// <summary>
    /// O perfil de energia em vigor (D9; tabela 4.3). Sem onda, ou com o tamagotchi desligado, a mesma instância do perfil
    /// de energia. Com onda, os percentuais da fase aplicados aos intervalos entre decisões e de descanso (em ms inteiros),
    /// aos pesos das ações (arredondados, e nunca zerados se eram positivos, a não ser a 0%), à altura do pulo e ao foguete
    /// (o da fase, ou o do perfil). Com a velocidade reduzida pela fase, o tempo na parede e o pendurado crescem por
    /// 100/Velocidade, para a subida mais lenta não ser cortada pela agenda (L16); mais rápido, ficam os mesmos.
    /// </summary>
    public static PerfilDeEnergia PerfilEfetivo(EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(cfg);
        PerfilDeEnergia perfil = cfg.Perfil(s.Preferencias.Energia);
        if (PerfilDaFase(s, cfg) is not { } p) return perfil;
        return perfil with
        {
            DecisaoMinima = Percentual(perfil.DecisaoMinima, p.Intervalo),
            DecisaoMaxima = Percentual(perfil.DecisaoMaxima, p.Intervalo),
            DescansoMinimo = Percentual(perfil.DescansoMinimo, p.Descanso),
            DescansoMaximo = Percentual(perfil.DescansoMaximo, p.Descanso),
            PesoAndar = Peso(perfil.PesoAndar, p.Andar),
            PesoEscalar = Peso(perfil.PesoEscalar, p.Escalar),
            PesoPular = Peso(perfil.PesoPular, p.Pular),
            PesoDescansar = Peso(perfil.PesoDescansar, p.Descansar),
            PesoGesto = Peso(perfil.PesoGesto, p.Gesticular),
            PesoTrocarExpressao = Peso(perfil.PesoTrocarExpressao, p.TrocarCara),
            // Atravessar é andar até o outro monitor (passo P13): o percentual de andar da fase vale para ele.
            PesoAtravessar = Peso(perfil.PesoAtravessar, p.Andar),
            PesoIrAoOutroMonitor = Peso(perfil.PesoIrAoOutroMonitor, p.Andar),
            AlturaDoPuloMinima = Dip(perfil.AlturaDoPuloMinima, p.AlturaDoPulo),
            AlturaDoPuloMaxima = Dip(perfil.AlturaDoPuloMaxima, p.AlturaDoPulo),
            ChanceDoFoguete = p.ChanceDoFoguete ?? perfil.ChanceDoFoguete,
            TempoNaParedeMinimo = MaisLento(perfil.TempoNaParedeMinimo, p.Velocidade),
            TempoNaParedeMaximo = MaisLento(perfil.TempoNaParedeMaximo, p.Velocidade),
            TempoPenduradoMinimo = MaisLento(perfil.TempoPenduradoMinimo, p.Velocidade),
            TempoPenduradoMaximo = MaisLento(perfil.TempoPenduradoMaximo, p.Velocidade),
        };
    }

    /// <summary>
    /// A física em vigor (D10; exceção documentada ao invariante 12): com onda, só as velocidades de andar, escalar e
    /// pendurar mudam, pelo percentual da fase (de 50 a 200%). A gravidade, a queda máxima, o quique, o foguete, o agarrar,
    /// as colisões e os limites ficam os mesmos. Sem onda, a 100% ou com o tamagotchi desligado, a mesma instância da
    /// configuração.
    /// </summary>
    public static ParametrosDeMovimento FisicaEfetiva(EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(cfg);
        ParametrosDeMovimento f = cfg.Fisica;
        if (PerfilDaFase(s, cfg) is not { } p || p.Velocidade == 100) return f;
        return f with
        {
            VelocidadeAndando = f.VelocidadeAndando * p.Velocidade / 100,
            VelocidadeEscalando = f.VelocidadeEscalando * p.Velocidade / 100,
            VelocidadePendurado = f.VelocidadePendurado * p.Velocidade / 100,
        };
    }

    /// <summary>
    /// O fator do passo da caminhada no cambaleio (4.9): uma onda triangular de <see cref="PassosDoCambaleio"/> passos em
    /// volta de 1, com a amplitude em % (0 anda reto). No começo da volta, 1 − amplitude/100 (a 120%, −0,2: um pequeno
    /// recuo); no meio, 1 + amplitude/100 (2,2); numa volta inteira, a média é 1. Só soma, subtração, multiplicação e
    /// divisão, sobre inteiros até a última conta, para dar o mesmo resultado em qualquer máquina.
    /// </summary>
    public static double FatorDoCambaleio(long passo, int amplitudePercentual)
    {
        if (amplitudePercentual == 0) return 1;
        long naVolta = passo % PassosDoCambaleio;
        if (naVolta < 0) naVolta += PassosDoCambaleio;
        long meio = PassosDoCambaleio / 2;
        long distanciaDoMeio = naVolta >= meio ? naVolta - meio : meio - naVolta;
        long quarto = PassosDoCambaleio / 4;
        return 1 + (double)(amplitudePercentual * (quarto - distanciaDoMeio)) / (100 * quarto);
    }

    /// <summary>Um intervalo a um percentual, em milissegundos inteiros (truncados).</summary>
    private static TimeSpan Percentual(TimeSpan t, int percentual) => TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * percentual / 100);

    /// <summary>Um peso a um percentual, arredondado; um peso positivo nunca vira zero, a não ser a 0%.</summary>
    private static int Peso(int peso, int percentual) => peso <= 0 || percentual <= 0 ? 0 : Math.Max(1, (peso * percentual + 50) / 100);

    /// <summary>Uma distância em DIP a um percentual, arredondada, de pelo menos 1.</summary>
    private static int Dip(int dip, int percentual) => Math.Max(1, (dip * percentual + 50) / 100);

    /// <summary>Um tempo alongado por 100/velocidade, com a velocidade abaixo de 100% (L16); senão, o mesmo.</summary>
    private static TimeSpan MaisLento(TimeSpan t, int velocidade)
        => velocidade >= 100 ? t : TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * 100 / velocidade);

    private sealed partial class Passo
    {
        /// <summary>
        /// Uma fase começou ou recomeçou neste evento, ou o temporizador levou o pico a outro nível (<see cref="IniciarFase"/>):
        /// o temporizador da onda recomeça. O alívio que só baixa o nível, na mesma fase, não o recomeça.
        /// </summary>
        private bool _reagendarOnda;

        /// <summary>A física em vigor: com onda, as três velocidades da fase (D10).</summary>
        private ParametrosDeMovimento Fisica => FisicaEfetiva(_s, _cfg);

        /// <summary>O perfil da fase da onda em vigor; nulo sem onda ou com o tamagotchi desligado.</summary>
        private PerfilDaOnda? FaseEmVigor => PerfilDaFase(_s, _cfg);

        /// <summary>Se há uma onda que vale: no estado e com o tamagotchi ligado.</summary>
        private bool ComOnda => _cfg.Tamagotchi && _s.Onda is not null;

        /// <summary>
        /// ITEM_EFFECT_TIMER (4.5): a onda avança uma fase ou um nível. Subida → pico; pico acima do nível 1 → um nível
        /// abaixo; pico no nível 1 → queda (nível 1), ou o fim, sem queda; queda → fim. Um disparo de outra geração, ou já
        /// atendido, é ignorado, como o da agenda. Não reagenda a decisão autônoma: com a autonomia pausada, o disparo só
        /// troca a cara. Com o tamagotchi desligado, é ignorado.
        /// </summary>
        private void AvancarOnda(long geracao)
        {
            if (!_cfg.Tamagotchi || !_s.OndaAgendada || geracao != _s.GeracaoDaOnda || _s.Onda is not { } onda) return;
            _s = _s with { OndaAgendada = false };
            EstadoDaOnda? seguinte = onda.Fase switch
            {
                FaseDaOnda.Subida => onda with { Fase = FaseDaOnda.Pico },
                FaseDaOnda.Pico when onda.Nivel > 1 => onda with { Nivel = onda.Nivel - 1 },
                FaseDaOnda.Pico when _cfg.TabelaDeOndas(onda.Tipo).Queda is not null => onda with { Fase = FaseDaOnda.Queda, Nivel = 1 },
                _ => null,
            };
            string fim = "fim";
            if (seguinte is not null) IniciarFase(seguinte);
            else if (FimDaFrente() is { } voltou) fim = $"fim; a de fundo volta: {Descrever(voltou)}";
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"ITEM_EFFECT_TIMER: onda {Descrever(onda)} -> {(seguinte is null ? fim : Descrever(seguinte))}"));
        }

        /// <summary>
        /// O que o item usado faz nas ondas: primeiro a combinação (4.5), com o alívio (<see cref="Combinar"/>); depois, a
        /// paranoia (<see cref="Paranoia"/>). Devolve o texto da paranoia para a regra do soltar (vazio sem ela) e se ela
        /// começou neste uso.
        /// </summary>
        private (string Paranoia, bool Comecou) AplicarNaOnda(DadosDoItem dados)
        {
            Combinar(dados);
            return Paranoia(dados);
        }

        /// <summary>
        /// A combinação (4.5), quando ele usa um item. Primeiro, o alívio (<see cref="Alivia"/>): a água, com qualquer onda
        /// na frente, e a comida e a bebida sem álcool, com uma onda de substância na frente, a aliviam um passo
        /// (<see cref="Aliviar"/>), sem começar onda nenhuma. Sem onda própria e sem alívio (a água sem onda), nada. Sem
        /// onda, a do item começa na subida, no nível da intensidade. Do mesmo tipo da da frente, os níveis somam até 3 e a
        /// fase recomeça (a queda volta ao pico). Do mesmo tipo da de fundo, os níveis dela somam, e ela continua congelada.
        /// De precedência maior ou igual à da frente, vai para a frente e a da frente fica atrás, congelada (a de fundo
        /// anterior é descartada: só cabem duas). De precedência menor, é absorvida: nem a onda nem o temporizador mudam.
        /// </summary>
        private void Combinar(DadosDoItem dados)
        {
            if (Alivia(dados))
            {
                Aliviar();
                return;
            }
            if (dados.Onda is not { } tipo) return;
            int intensidade = Math.Clamp(dados.Intensidade, 1, 3);
            EstadoDaOnda? frente = _s.Onda, fundo = _s.OndaDeFundo;
            if (frente is null)
                IniciarFase(new EstadoDaOnda(tipo, FaseDaOnda.Subida, intensidade, intensidade));
            else if (frente.Tipo == tipo)
                IniciarFase(Somada(frente, intensidade));
            else if (fundo is not null && fundo.Tipo == tipo)
                _s = _s with { OndaDeFundo = Somada(fundo, intensidade) };
            else if (_cfg.TabelaDeOndas(tipo).Precedencia >= _cfg.TabelaDeOndas(frente.Tipo).Precedencia)
            {
                _s = _s with { OndaDeFundo = frente };
                IniciarFase(new EstadoDaOnda(tipo, FaseDaOnda.Subida, intensidade, intensidade));
            }
        }

        /// <summary>
        /// Se o item alivia a onda da frente (o alívio, pedido do usuário de 2026-10-01): só um item de alívio, e só com
        /// onda na frente. Sem onda própria, a água alivia qualquer onda, de substância ou leve; com onda própria, a comida
        /// e a bebida sem álcool só aliviam uma onda de substância, e com uma onda leve na frente combinam como sempre.
        /// </summary>
        private bool Alivia(DadosDoItem dados)
            => dados.Alivio && _s.Onda is { } frente && (dados.Onda is null || _cfg.TabelaDeOndas(frente.Tipo).DeSubstancia);

        /// <summary>
        /// O alívio: comer ou beber algo sem álcool acalma a onda da frente um passo (<see cref="UmPassoAbaixo"/>). Só o
        /// nível caiu: a fase e o temporizador em curso continuam, e nada é reagendado. Na queda que começa: a duração cheia
        /// dela, pelo pior nível, e a cara dela. No fim da onda: a de fundo volta, como no fim pelo temporizador
        /// (<see cref="FimDaFrente"/>). A de fundo nunca é tocada.
        /// </summary>
        private void Aliviar()
        {
            if (_s.Onda is not { } onda) return;
            if (UmPassoAbaixo(onda) is not { } seguinte) FimDaFrente();
            else if (seguinte.Fase == onda.Fase) _s = _s with { Onda = seguinte };
            else IniciarFase(seguinte);
        }

        /// <summary>
        /// Um passo do alívio: na subida ou no pico acima do nível 1, um nível abaixo, na mesma fase; no nível 1, a queda
        /// (nível 1, com o mesmo pior), ou nulo, o fim, se a onda não tem queda; na queda, nulo, o fim.
        /// </summary>
        private EstadoDaOnda? UmPassoAbaixo(EstadoDaOnda onda) => onda switch
        {
            { Fase: FaseDaOnda.Queda } => null,
            { Nivel: > 1 } => onda with { Nivel = onda.Nivel - 1 },
            _ when _cfg.TabelaDeOndas(onda.Tipo).Queda is not null => onda with { Fase = FaseDaOnda.Queda, Nivel = 1 },
            _ => null,
        };

        /// <summary>
        /// O que o alívio do item fará na onda da frente, para a regra da transição do uso, sem dado pessoal:
        /// "; alivia Bebado/Pico/2 -> Bebado/Pico/1", "-> fim" ou "-> fim; a de fundo volta: …". Vazio sem alívio.
        /// </summary>
        private string DescreverOAlivio(DadosDoItem dados)
        {
            if (!Alivia(dados) || _s.Onda is not { } frente) return "";
            string depois = UmPassoAbaixo(frente) is { } passo ? Descrever(passo)
                : _s.OndaDeFundo is { } fundo ? $"fim; a de fundo volta: {Descrever(fundo)}" : "fim";
            return $"; alivia {Descrever(frente)} -> {depois}";
        }

        /// <summary>A mesma onda com mais níveis, até 3: o pior nível acompanha, e a queda volta ao pico; a subida continua subida.</summary>
        private static EstadoDaOnda Somada(EstadoDaOnda onda, int intensidade)
        {
            int nivel = Math.Min(3, onda.Nivel + intensidade);
            return onda with { Nivel = nivel, Pior = Math.Max(onda.Pior, nivel), Fase = onda.Fase == FaseDaOnda.Subida ? FaseDaOnda.Subida : FaseDaOnda.Pico };
        }

        // ---------------------------------------------------------------- a paranoia (pedidos do usuário de 2026-10-01)

        /// <summary>
        /// A paranoia, depois da combinação. Um item de substância (todo item que não é de alívio) entra na carga do episódio
        /// (<see cref="CargaDaParanoia.Com"/>); a comida e a bebida sem álcool não fazem nada aqui. Com a paranoia na frente,
        /// ela sobe um nível (até 3), o pior acompanha e a fase recomeça, como no mesmo tipo (<see cref="Somada"/>: a queda
        /// volta ao pico; a subida continua subida), sem sorteio. Sem ela na frente, o uso que fecha um episódio de mistura
        /// com droga sintética (<see cref="CargaDaParanoia.MisturaComSintetica"/>, contando o item atual) faz o sorteio do
        /// episódio, o único (<see cref="CargaDaParanoia.Sorteada"/>), com a chance da configuração
        /// (<see cref="ConfiguracaoDoNucleo.ChanceDaParanoia"/>, 1 em 8), num passo do gerador próprio da paranoia
        /// (<see cref="EstadoDoNucleo.AleatorioDaParanoia"/>); o gerador principal, nunca. Saindo ou não, o episódio não
        /// sorteia mais, até a carga voltar a zero. Se sai, ela começa na frente, na subida do nível 1, e a frente vai para o
        /// fundo, congelada (a de fundo anterior é descartada), como manda a precedência dela, a maior de todas. Devolve o
        /// texto da regra do soltar ("; a paranoia começa: Paranoico/Subida/1" ou "; a paranoia sobe: Paranoico/Pico/1 ->
        /// Paranoico/Pico/2"; vazio sem paranoia, inclusive quando o sorteio não sai) e se ela começou.
        /// </summary>
        private (string Texto, bool Comecou) Paranoia(DadosDoItem dados)
        {
            if (dados.Alivio) return ("", false);
            _s = _s with { Carga = _s.Carga.Com(dados) };
            if (_s.Onda is { Tipo: Onda.Paranoico } paranoia)
            {
                EstadoDaOnda subiu = Somada(paranoia, 1);
                IniciarFase(subiu);
                return ($"; a paranoia sobe: {Descrever(paranoia)} -> {Descrever(subiu)}", false);
            }
            if (!_s.Carga.MisturaComSintetica || _s.Carga.Sorteada) return ("", false);
            (bool saiu, Aleatorio proximo) = _s.AleatorioDaParanoia.Sortear(_cfg.ChanceDaParanoia);
            _s = _s with { AleatorioDaParanoia = proximo, Carga = _s.Carga with { Sorteada = true } };
            if (!saiu) return ("", false);
            var comeca = new EstadoDaOnda(Onda.Paranoico, FaseDaOnda.Subida, 1, 1);
            _s = _s with { OndaDeFundo = _s.Onda };
            IniciarFase(comeca);
            return ($"; a paranoia começa: {Descrever(comeca)}", true);
        }

        /// <summary>
        /// O começo da paranoia, com ele livre. Ela começa no soltar do item, com ele já usando (<see cref="Uso.ComecouAParanoia"/>):
        /// no fim desse uso, se a acomodação o devolve a IDLE sem gesto e a paranoia continua na frente, ele olha pro teto na
        /// hora (<see cref="Gesto.OlharProTeto"/>, por <see cref="PassosDoOlharProTeto"/> passos), sem sorteio. Em qualquer
        /// outro estado (na parede ou no cipó, preso ou não; escondido; no ar), nada especial: a agenda e as caras da fase
        /// fazem o resto.
        /// </summary>
        private void OlharProTetoNoComecoDaParanoia()
        {
            if (!ComOnda || _s.Onda?.Tipo != Onda.Paranoico || _s.Estado != Estado.Idle || _s.Gesto != Gesto.Nenhum) return;
            _s = _s with { Gesto = Gesto.OlharProTeto, PassosDoGesto = PassosDoOlharProTeto };
            _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE: a paranoia começou, gesto {Gesto.OlharProTeto}"));
        }

        /// <summary>
        /// No fim de todo evento, a carga da paranoia volta toda a <see cref="CargaDaParanoia.Nenhuma"/> (as substâncias, a
        /// sintética, os itens distintos e o sorteio feito, juntos) se nem a onda da frente nem a de fundo é de substância: o
        /// episódio acabou, e o seguinte sorteia de novo. A paranoia é de substância, então a carga dura enquanto ela durar. O
        /// gerador da paranoia segue, sem voltar ao começo. Com o tamagotchi desligado, nada muda.
        /// </summary>
        private void ZerarACargaSemSubstancia()
        {
            if (_cfg.Tamagotchi && _s.Carga != CargaDaParanoia.Nenhuma && !DeSubstancia(_s.Onda) && !DeSubstancia(_s.OndaDeFundo))
                _s = _s with { Carga = CargaDaParanoia.Nenhuma };
        }

        /// <summary>Se a onda existe e é de substância, pela tabela (<see cref="DadosDaOnda.DeSubstancia"/>).</summary>
        private bool DeSubstancia(EstadoDaOnda? onda) => onda is not null && _cfg.TabelaDeOndas(onda.Tipo).DeSubstancia;

        /// <summary>
        /// A onda entra numa fase, ou noutro nível: o temporizador recomeça com a duração dela, e a cara da fase entra na
        /// hora se a cara está livre; senão, no fim do estado (<see cref="VoltarACaraDeBase"/>, acordar).
        /// </summary>
        private void IniciarFase(EstadoDaOnda onda)
        {
            _s = _s with { Onda = onda };
            _reagendarOnda = true;
            if (CaraLivre(_s.Estado)) _s = _s with { Expressao = _cfg.TabelaDeOndas(onda.Tipo).Cara(onda.Fase) };
        }

        /// <summary>
        /// A onda da frente acabou. Com uma de fundo, ela volta à frente, com a fase em que estava recomeçada na duração
        /// cheia e a cara dessa fase (4.5); devolve essa onda. Sem ela, fica sem onda, e a cara volta à de base, a emoção
        /// dominante ou a neutra, se está livre; devolve nulo.
        /// </summary>
        private EstadoDaOnda? FimDaFrente()
        {
            if (_s.OndaDeFundo is { } fundo)
            {
                _s = _s with { OndaDeFundo = null };
                IniciarFase(fundo);
                return fundo;
            }
            _s = _s with { Onda = null };
            if (CaraLivre(_s.Estado)) _s = _s with { Expressao = CaraDeBase() };
            return null;
        }

        /// <summary>
        /// O temporizador da onda (4.5), depois do da agenda: com onda e fora de EXITING, um disparo único com a duração da
        /// fase (nunca menos de 1 s), agendado quando a fase começa ou quando falta; sem onda, ou saindo, o pendente é
        /// cancelado. Com o tamagotchi desligado, nenhum efeito novo sai do núcleo.
        /// </summary>
        private void EfeitosDaOnda(List<Efeito> tempo)
        {
            if (!_cfg.Tamagotchi) return;
            if (_s.Onda is { } onda && _s.Estado != Estado.Exiting)
            {
                if (_s.OndaAgendada && !_reagendarOnda) return;
                TimeSpan atraso = _cfg.TabelaDeOndas(onda.Tipo).Duracao(onda.Fase, onda.Pior);
                long geracao = _s.GeracaoDaOnda + 1;
                tempo.Add(new AgendarOnda(atraso, geracao));
                _s = _s with { GeracaoDaOnda = geracao, OndaAgendada = true };
            }
            else if (_s.OndaAgendada)
            {
                tempo.Add(new CancelarOnda());
                _s = _s with { OndaAgendada = false };
            }
        }

        /// <summary>Uma cara da fase da onda (tabela 4.4; na subida, só a da subida), num único sorteio ponderado; pode repetir a atual.</summary>
        private Expressao SortearCaraDaFase(PerfilDaOnda fase)
        {
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado([.. fase.Caras.Select(c => c.Peso)]);
            _s = _s with { Aleatorio = a };
            return fase.Caras[i].Cara;
        }

        /// <summary>
        /// O gesto da agenda, num único sorteio (D12): com onda, um dos gestos da fase, pelos pesos (os oito do fim do
        /// enum só saem daqui, além do olhar pro teto do começo da paranoia, sem sorteio); sem onda, de
        /// <see cref="Gesto.Espiar"/> a <see cref="Gesto.Brincar"/>, como antes.
        /// </summary>
        private (Gesto Gesto, Aleatorio Proximo) SortearGesto()
        {
            if (FaseEmVigor is { } fase)
            {
                (int i, Aleatorio a) = _s.Aleatorio.Ponderado([.. fase.Gestos.Select(g => g.Peso)]);
                return (fase.Gestos[i].Gesto, a);
            }
            (int g, Aleatorio proximo) = _s.Aleatorio.Entre((int)Gesto.Espiar, (int)Gesto.Brincar);
            // Com a personalidade (DEC-037, itens 7 e 9), o tipo vem dos pesos da energia, no gerador dela; o principal anda
            // o mesmo passo de antes, e a agenda não muda por causa disso.
            if (_cfg.Personalidade && Perfil.PesosDosGestos is { } pesos)
            {
                (int i, Aleatorio p) = _s.AleatorioDaPersonalidade.Ponderado([.. pesos]);
                _s = _s with { AleatorioDaPersonalidade = p };
                return ((Gesto)((int)Gesto.Espiar + i), proximo);
            }
            return ((Gesto)g, proximo);
        }

        /// <summary>O fator do cambaleio no passo atual do relógio (4.9); 1 sem onda ou sem cambaleio na fase.</summary>
        private double Cambaleio(out bool cambaleia)
        {
            int amplitude = FaseEmVigor?.Cambaleio ?? 0;
            cambaleia = amplitude != 0;
            return FatorDoCambaleio(_s.Passos, amplitude);
        }

        /// <summary>A onda como na linha do retrato, Tipo/Fase/Nível, na cultura invariante.</summary>
        private static string Descrever(EstadoDaOnda onda) => string.Create(CultureInfo.InvariantCulture, $"{onda.Tipo}/{onda.Fase}/{onda.Nivel}");
    }
}
