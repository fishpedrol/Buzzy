using System.Globalization;

namespace Buzzy.Core.Personagem;

// Onda de desenho animado de um item. Com o tamagotchi desligado, a onda no estado não vale e nada sai do
// núcleo. Avança só no próprio timer (não no relógio de passo) e mexe em pesos, intervalos, gestos, caras,
// velocidades e cambaleio. Só a da frente vale; a de fundo fica congelada até a da frente acabar.
// Comer/beber sem álcool acalma a da frente um passo por item. Misturar com droga sintética sorteia,
// uma vez por episódio e com chance de 1 em 8, a paranoia (acha que tem alguém no teto).
public static partial class Maquina
{
    // Uma volta do cambaleio: 0,8 s a 60 passos/s.
    public const int PassosDoCambaleio = 48;

    // Olhar pro teto no começo da paranoia: 1,5 s a 60 passos/s, fixo.
    public const int PassosDoOlharProTeto = 90;

    // Nulo sem onda ou com o tamagotchi desligado.
    public static PerfilDaOnda? PerfilDaFase(EstadoDoNucleo s, ConfiguracaoDoNucleo cfg)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(cfg);
        return cfg.Tamagotchi && s.Onda is { } onda ? cfg.TabelaDeOndas(onda.Tipo).Perfil(onda.Fase, onda.Nivel) : null;
    }

    // Sem onda devolve a mesma instância do perfil. Com onda, aplica os percentuais da fase. Se a fase deixa
    // ele mais lento, o tempo na parede/pendurado cresce por 100/Velocidade, senão a agenda corta a subida.
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
            // Atravessar é andar até o outro monitor, então usa o percentual de andar.
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

    // Com onda, só as velocidades de andar, escalar e pendurar mudam (50 a 200%). Gravidade, quique,
    // colisões e o resto ficam iguais. Sem mudança, devolve a mesma instância.
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

    // Onda triangular em volta de 1, amplitude em % (0 = reto). Começo da volta: 1 - amp/100 (a 120%, -0,2,
    // um pequeno recuo); meio: 1 + amp/100; média 1. Conta em inteiros até o fim pra ser determinístico.
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

    // Em ms inteiros, truncado.
    private static TimeSpan Percentual(TimeSpan t, int percentual) => TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * percentual / 100);

    // Peso positivo nunca vira zero, a não ser a 0%.
    private static int Peso(int peso, int percentual) => peso <= 0 || percentual <= 0 ? 0 : Math.Max(1, (peso * percentual + 50) / 100);

    private static int Dip(int dip, int percentual) => Math.Max(1, (dip * percentual + 50) / 100);

    private static TimeSpan MaisLento(TimeSpan t, int velocidade)
        => velocidade >= 100 ? t : TimeSpan.FromMilliseconds((long)t.TotalMilliseconds * 100 / velocidade);

    private sealed partial class Passo
    {
        // Fase nova ou nível novo pelo timer: recomeça o timer. Alívio que só baixa o nível não recomeça.
        private bool _reagendarOnda;

        private ParametrosDeMovimento Fisica => FisicaEfetiva(_s, _cfg);

        private PerfilDaOnda? FaseEmVigor => PerfilDaFase(_s, _cfg);

        private bool ComOnda => _cfg.Tamagotchi && _s.Onda is not null;

        // Subida -> pico -> desce um nível por disparo -> queda (se a onda tem) -> fim. Disparo de geração
        // velha é ignorado. Não mexe na agenda: com a autonomia pausada, só troca a cara.
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

        // Combinação primeiro, paranoia depois. Devolve o texto da paranoia pro log (vazio sem ela).
        private (string Paranoia, bool Comecou) AplicarNaOnda(DadosDoItem dados)
        {
            Combinar(dados);
            return Paranoia(dados);
        }

        // Alívio primeiro, sem começar onda. Sem onda: a do item começa na subida. Mesmo tipo da frente: soma
        // até 3 e a fase recomeça. Mesmo tipo da de fundo: soma nela, que segue congelada. Precedência >= da
        // frente: vai pra frente e a antiga vai pro fundo (só cabem duas). Precedência menor: absorvida.
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
            {
                _s = _s with { FontesDaOnda = ContribuicoesDaOnda.Nenhuma.Com(dados.Item, intensidade) };
                IniciarFase(new EstadoDaOnda(tipo, FaseDaOnda.Subida, intensidade, intensidade));
            }
            else if (frente.Tipo == tipo)
            {
                _s = _s with { FontesDaOnda = _s.FontesDaOnda.Com(dados.Item, intensidade) };
                IniciarFase(Somada(frente, intensidade));
            }
            else if (fundo is not null && fundo.Tipo == tipo)
                _s = _s with
                {
                    OndaDeFundo = Somada(fundo, intensidade),
                    FontesDaOndaDeFundo = _s.FontesDaOndaDeFundo.Com(dados.Item, intensidade),
                };
            else if (_cfg.TabelaDeOndas(tipo).Precedencia >= _cfg.TabelaDeOndas(frente.Tipo).Precedencia)
            {
                _s = _s with
                {
                    OndaDeFundo = frente,
                    FontesDaOndaDeFundo = _s.FontesDaOnda,
                    FontesDaOnda = ContribuicoesDaOnda.Nenhuma.Com(dados.Item, intensidade),
                };
                IniciarFase(new EstadoDaOnda(tipo, FaseDaOnda.Subida, intensidade, intensidade));
            }
        }

        // Água (sem onda própria) alivia qualquer onda. Comida e bebida sem álcool só aliviam onda de
        // substância; com onda leve na frente, combinam normalmente.
        private bool Alivia(DadosDoItem dados)
            => dados.Alivio && _s.Onda is { } frente && (dados.Onda is null || _cfg.TabelaDeOndas(frente.Tipo).DeSubstancia);

        // Se só o nível caiu, fase e timer seguem sem reagendar. Se entrou na queda, ela tem duração cheia.
        // A de fundo nunca é tocada.
        private void Aliviar()
        {
            if (_s.Onda is not { } onda) return;
            if (UmPassoAbaixo(onda) is not { } seguinte) FimDaFrente();
            else if (seguinte.Fase == onda.Fase) _s = _s with { Onda = seguinte };
            else IniciarFase(seguinte);
        }

        // Nível > 1: desce um na mesma fase. Nível 1: queda, se houver. Nulo = fim.
        private EstadoDaOnda? UmPassoAbaixo(EstadoDaOnda onda) => onda switch
        {
            { Fase: FaseDaOnda.Queda } => null,
            { Nivel: > 1 } => onda with { Nivel = onda.Nivel - 1 },
            _ when _cfg.TabelaDeOndas(onda.Tipo).Queda is not null => onda with { Fase = FaseDaOnda.Queda, Nivel = 1 },
            _ => null,
        };

        // Texto pro log, ex.: "; alivia Bebado/Pico/2 -> Bebado/Pico/1". Vazio sem alívio.
        private string DescreverOAlivio(DadosDoItem dados)
        {
            if (!Alivia(dados) || _s.Onda is not { } frente) return "";
            string depois = UmPassoAbaixo(frente) is { } passo ? Descrever(passo)
                : _s.OndaDeFundo is { } fundo ? $"fim; a de fundo volta: {Descrever(fundo)}" : "fim";
            return $"; alivia {Descrever(frente)} -> {depois}";
        }

        // Até 3. A queda volta ao pico; a subida continua subida.
        private static EstadoDaOnda Somada(EstadoDaOnda onda, int intensidade)
        {
            int nivel = Math.Min(3, onda.Nivel + intensidade);
            return onda with { Nivel = nivel, Pior = Math.Max(onda.Pior, nivel), Fase = onda.Fase == FaseDaOnda.Subida ? FaseDaOnda.Subida : FaseDaOnda.Pico };
        }

        // ---------------------------------------------------------------- a paranoia

        // Item de substância entra na carga do episódio. Paranoia já na frente: sobe um nível, sem sorteio.
        // Senão, o uso que fecha uma mistura com sintética sorteia uma única vez por episódio, no gerador
        // próprio da paranoia (nunca no principal). Se sai, entra na frente (maior precedência) e a antiga
        // vai pro fundo. O sorteio só volta quando a carga zerar.
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
            _s = _s with
            {
                OndaDeFundo = _s.Onda,
                FontesDaOndaDeFundo = _s.FontesDaOnda,
                FontesDaOnda = ContribuicoesDaOnda.Nenhuma,
            };
            IniciarFase(comeca);
            return ($"; a paranoia começa: {Descrever(comeca)}", true);
        }

        // A paranoia começa durante o uso; no fim dele, se voltou a IDLE sem gesto, olha pro teto na hora.
        // Em qualquer outro estado, a agenda e as caras da fase fazem o resto.
        private void OlharProTetoNoComecoDaParanoia()
        {
            if (!ComOnda || _s.Onda?.Tipo != Onda.Paranoico || _s.Estado != Estado.Idle || _s.Gesto != Gesto.Nenhum) return;
            _s = _s with { Gesto = Gesto.OlharProTeto, PassosDoGesto = PassosDoOlharProTeto };
            _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE: a paranoia começou, gesto {Gesto.OlharProTeto}"));
        }

        // Roda no fim de todo evento: sem onda de substância na frente nem no fundo, o episódio acabou e a carga
        // zera inteira. A paranoia conta como substância, então a carga dura enquanto ela durar.
        private void ZerarACargaSemSubstancia()
        {
            if (_cfg.Tamagotchi && _s.Carga != CargaDaParanoia.Nenhuma && !DeSubstancia(_s.Onda) && !DeSubstancia(_s.OndaDeFundo))
                _s = _s with { Carga = CargaDaParanoia.Nenhuma };
        }

        private bool DeSubstancia(EstadoDaOnda? onda) => onda is not null && _cfg.TabelaDeOndas(onda.Tipo).DeSubstancia;

        // Recomeça o timer. A cara da fase entra agora se estiver livre; senão, quando o estado acabar.
        private void IniciarFase(EstadoDaOnda onda)
        {
            _s = _s with { Onda = onda };
            _reagendarOnda = true;
            if (CaraLivre(_s.Estado)) _s = _s with { Expressao = _cfg.TabelaDeOndas(onda.Tipo).Cara(onda.Fase) };
        }

        // A de fundo, se houver, volta pra frente com a fase recomeçada e é devolvida. Sem ela, a cara volta
        // à de base e devolve nulo.
        private EstadoDaOnda? FimDaFrente()
        {
            if (_s.OndaDeFundo is { } fundo)
            {
                _s = _s with
                {
                    OndaDeFundo = null,
                    FontesDaOnda = _s.FontesDaOndaDeFundo,
                    FontesDaOndaDeFundo = ContribuicoesDaOnda.Nenhuma,
                };
                IniciarFase(fundo);
                return fundo;
            }
            _s = _s with { Onda = null, FontesDaOnda = ContribuicoesDaOnda.Nenhuma };
            if (CaraLivre(_s.Estado)) _s = _s with { Expressao = CaraDeBase() };
            return null;
        }

        // Disparo único com a duração da fase (mín. 1 s), armado quando a fase começa ou quando falta.
        // Sem onda, ou saindo, cancela o pendente.
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

        // Pode repetir a cara atual.
        private Expressao SortearCaraDaFase(PerfilDaOnda fase)
        {
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado([.. fase.Caras.Select(c => c.Peso)]);
            _s = _s with { Aleatorio = a };
            return fase.Caras[i].Cara;
        }

        // Com onda, um dos gestos da fase (os oito do fim do enum só saem daqui). Sem onda, de Espiar a Brincar.
        private (Gesto Gesto, Aleatorio Proximo) SortearGesto()
        {
            if (FaseEmVigor is { } fase)
            {
                (int i, Aleatorio a) = _s.Aleatorio.Ponderado([.. fase.Gestos.Select(g => g.Peso)]);
                return (fase.Gestos[i].Gesto, a);
            }
            (int g, Aleatorio proximo) = _s.Aleatorio.Entre((int)Gesto.Espiar, (int)Gesto.Brincar);
            // Com personalidade, o tipo vem dos pesos da energia, no gerador dela. O principal avança igual,
            // pra agenda não mudar.
            if (_cfg.Personalidade && Perfil.PesosDosGestos is { } pesos)
            {
                (int i, Aleatorio p) = _s.AleatorioDaPersonalidade.Ponderado([.. pesos]);
                _s = _s with { AleatorioDaPersonalidade = p };
                return ((Gesto)((int)Gesto.Espiar + i), proximo);
            }
            return ((Gesto)g, proximo);
        }

        private double Cambaleio(out bool cambaleia)
        {
            int amplitude = FaseEmVigor?.Cambaleio ?? 0;
            cambaleia = amplitude != 0;
            return FatorDoCambaleio(_s.Passos, amplitude);
        }

        // Tipo/Fase/Nível, igual à linha do retrato.
        private static string Descrever(EstadoDaOnda onda) => string.Create(CultureInfo.InvariantCulture, $"{onda.Tipo}/{onda.Fase}/{onda.Nivel}");
    }
}
