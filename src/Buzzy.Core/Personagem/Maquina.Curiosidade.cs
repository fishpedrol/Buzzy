namespace Buzzy.Core.Personagem;

/// <summary>
/// A curiosidade da Fase 7 (DEC-026 e DEC-037), com a capacidade <see cref="ConfiguracaoDoNucleo.Curiosidade"/>. O núcleo
/// recebe só a chave opaca do monitor do primeiro plano (<see cref="ForegroundMonitorChanged"/>, depois da carência do
/// adaptador) e, a pedido, o vão horizontal da janela em dezesseis avos (<see cref="ActiveWindowSpan"/>); o tempo é dele, num
/// disparo único com geração (<see cref="CuriosityTimer"/>). Com o foco há 30 s noutro monitor, vai ver (uma ida por foco);
/// com o foco no monitor dele por bastante tempo, chega perto do trecho da janela e fica olhando. Só age com
/// <see cref="Passo.PodeSerCurioso"/>, na próxima decisão da agenda em IDLE ou no próprio disparo: nada é periódico. Soltar,
/// mostrar pelo comando e redefinir a posição ligam a dispensa, que nada apaga antes do disparo dela.
/// </summary>
public static partial class Maquina
{
    private sealed partial class Passo
    {
        // O disparo da curiosidade pedido neste passo: armar com a espera (substitui o pendente) ou cancelar.
        private TimeSpan? _armarCuriosidade;
        private bool _cancelarCuriosidade;

        private static bool EhDaCuriosidade(Evento evento) => evento is ForegroundMonitorChanged or CuriosityTimer or ActiveWindowSpan;

        /// <summary>Quanto falta da contagem do foco depois da carência do adaptador: 30 s menos 2 s.</summary>
        private TimeSpan RestanteDoLimiar => Maior(_cfg.LimiarDeOutroMonitor - CarenciaDoFocoNoAdaptador, TimeSpan.FromSeconds(1));

        /// <summary>A carência que o adaptador já esperou antes de publicar o foco (DEC-037, item 2).</summary>
        private static readonly TimeSpan CarenciaDoFocoNoAdaptador = TimeSpan.FromSeconds(2);

        private static TimeSpan Maior(TimeSpan a, TimeSpan b) => a > b ? a : b;

        private string? MeuMonitor => _s.Lugar?.Monitor.Chave;

        private bool Ocupado(string chave) => _s.Ocupados.Contem(chave);

        private bool ParanoiaNaFrente => ComOnda && _s.Onda?.Tipo == Onda.Paranoico;

        /// <summary>Um episódio em curso: indo ver, esperando o vão, chegando perto ou olhando. A troca de foco não o interrompe.</summary>
        private bool EpisodioEmCurso => _s.Curiosidade is EstagioDaCuriosidade.IndoVer or EstagioDaCuriosidade.PedindoVao
            or EstagioDaCuriosidade.Aproximando or EstagioDaCuriosidade.Olhando;

        /// <summary>
        /// O predicado único da curiosidade (DEC-037, item 6): ela só age com ele em IDLE no chão, sem gesto, visível, sem o
        /// usuário no controle (IDLE já é visível), sem pausa, sem painel, sem esconderijo, sem estar preso, sem item na mão, sem a paranoia na
        /// frente, sem retorno de tela cheia guardado, sem dispensa e com o monitor dele fora dos ocupados.
        /// </summary>
        private bool PodeSerCurioso
            => _cfg.Curiosidade && _cfg.Movimento && _s.Estado == Estado.Idle && _s.Gesto == Gesto.Nenhum
               && !_s.AutonomiaPausada && !_s.PainelAberto && _s.Esconderijo == LadoDoEsconderijo.Nenhum && !_s.PresoPeloUsuario
               && !AtentoAoItem && !ParanoiaNaFrente && _s.RetornoDaTelaCheia is null && !_s.DispensaAtiva
               && _s.Topologia is not null && MeuMonitor is { } meu && !Ocupado(meu);

        private void ArmarCuriosidade(TimeSpan espera)
        {
            _armarCuriosidade = espera;
            _cancelarCuriosidade = false;
        }

        private void DesarmarCuriosidade()
        {
            _armarCuriosidade = null;
            _cancelarCuriosidade = true;
        }

        private void Estagio(EstagioDaCuriosidade estagio, string regra)
        {
            if (_s.Curiosidade == estagio) return;
            _s = _s with { Curiosidade = estagio };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CURIOSIDADE: {regra}"));
        }

        // ---------------------------------------------------------------- eventos

        /// <summary>
        /// FOREGROUND_MONITOR_CHANGED (DEC-037, item 2): um foco novo recomeça a contagem (os 28 s que faltam dos 30) e a ida
        /// daquele foco. Com a dispensa ou um episódio em curso, só o foco muda: a dispensa continua, e o episódio termina e
        /// revê a decisão (<see cref="Satisfazer"/>).
        /// </summary>
        private void MudarFoco(string chave)
        {
            if (chave == _s.FocoDoPrimeiroPlano) return;
            _s = _s with { FocoDoPrimeiroPlano = chave, IdaFeitaNesteFoco = false };
            if (_s.DispensaAtiva || EpisodioEmCurso) return;
            // Sem transição: nenhuma linha de log por troca de foco (DEC-037, item 11).
            _s = _s with { Curiosidade = EstagioDaCuriosidade.Aguardando };
            ArmarCuriosidade(RestanteDoLimiar);
        }

        /// <summary>CURIOSITY_TIMER: o disparo vigente avança a curiosidade; outro, substituído ou cancelado, é ignorado.</summary>
        private void DispararCuriosidade(long geracao)
        {
            if (!_s.CuriosidadeAgendada || geracao != _s.GeracaoDaCuriosidade) return;
            _s = _s with { CuriosidadeAgendada = false };
            if (_s.DispensaAtiva)
            {
                // O fim da dispensa: a contagem recomeça com o foco de agora.
                _s = _s with { DispensaAtiva = false };
                _transicoes.Add(new Transicao(_s.Estado, _s.Estado, "CURIOSIDADE: fim da dispensa"));
                if (_s.FocoDoPrimeiroPlano is null) return;
                Estagio(EstagioDaCuriosidade.Aguardando, "aguarda depois da dispensa");
                ArmarCuriosidade(RestanteDoLimiar);
                return;
            }
            switch (_s.Curiosidade)
            {
                case EstagioDaCuriosidade.PedindoVao:
                    Satisfazer("o vão não chegou");
                    return;
                case EstagioDaCuriosidade.Aguardando or EstagioDaCuriosidade.AguardandoPerto or EstagioDaCuriosidade.Satisfeita:
                    Amadurecer();
                    break;
                default:
                    return;
            }
            if (PodeSerCurioso && _s.Curiosidade is EstagioDaCuriosidade.IrVer or EstagioDaCuriosidade.Aproximar) DecidirCurioso();
        }

        /// <summary>
        /// A contagem venceu: noutro monitor, fora dos ocupados e sem a ida feita, vai ver; no monitor dele, depois do bastante
        /// tempo do perfil (ou de novo, quando o intervalo vence), chega perto; senão, espera o intervalo.
        /// </summary>
        private void Amadurecer()
        {
            if (_s.FocoDoPrimeiroPlano is not { } foco || _s.Topologia?.PorChave(foco) is null)
            {
                Estagio(EstagioDaCuriosidade.Nenhuma, "sem foco");
                return;
            }
            if (foco != MeuMonitor)
            {
                if (!_s.IdaFeitaNesteFoco && !Ocupado(foco))
                {
                    Estagio(EstagioDaCuriosidade.IrVer, "o foco ficou noutro monitor (vai ver)");
                    _s = _s with { FocoDoEpisodio = foco };
                    return;
                }
                // A ida já feita ou o alvo ocupado: espera um foco novo, sem disparo (nada periódico sem efeito).
                Estagio(EstagioDaCuriosidade.Satisfeita, "nada a ver agora (espera um foco novo)");
                return;
            }
            if (_s.Curiosidade == EstagioDaCuriosidade.Aguardando)
            {
                Estagio(EstagioDaCuriosidade.AguardandoPerto, "o foco está no monitor dele (aguarda)");
                ArmarCuriosidade(Maior(Perfil.TempoParaAproximar - _cfg.LimiarDeOutroMonitor, TimeSpan.FromSeconds(1)));
                return;
            }
            Estagio(EstagioDaCuriosidade.Aproximar, "o foco ficou no monitor dele (chega perto)");
            _s = _s with { FocoDoEpisodio = foco };
        }

        /// <summary>
        /// ACTIVE_WINDOW_SPAN (DEC-037, item 5): o vão pedido vira o destino dele no chão e é descartado; sem vão, ou sem poder
        /// agir, o episódio termina.
        /// </summary>
        private void ReceberVao(long geracao, VaoDaJanela? vao)
        {
            if (_s.Curiosidade != EstagioDaCuriosidade.PedindoVao || geracao != _s.GeracaoDoVao) return;
            DesarmarCuriosidade();
            if (vao is not { Valido: true } v || !PodeSerCurioso || MeuMonitor != _s.FocoDoPrimeiroPlano
                || !Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala) || _s.Lugar is null)
            {
                Satisfazer("sem o vão");
                return;
            }
            (double alvo, Direcao lado) = PontoDeOlhar(m.AreaUtil, sup, v, _s.Lugar.Ancora.X, _s.Lugar.Tamanho.Largura, _cfg.FolgaDoOlhar * escala);
            _s = _s with { LadoDoOlhar = lado };
            double distancia = Math.Abs(alvo - _s.Lugar.Ancora.X);
            if (distancia <= _cfg.FolgaDoOlhar * escala)
            {
                ChegarPerto("IDLE + CURIOSIDADE: já está perto, olha");
                return;
            }
            _s = _s with { Direcao = alvo > _s.Lugar.Ancora.X ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, "IDLE + CURIOSIDADE: chega perto da janela ativa");
            _s = _s with { Movimento = _s.Movimento with { Restante = distancia, Aproximando = true } };
            Estagio(EstagioDaCuriosidade.Aproximando, "chega perto");
        }

        // ---------------------------------------------------------------- decisões

        /// <summary>
        /// A decisão da curiosidade, antes da agenda em IDLE (e no próprio disparo): devolve se ela tomou a decisão. Com um
        /// estágio maduro, age; esperando o vão ou olhando, ocupa a decisão; com a ida ou a aproximação interrompidas, encerra o
        /// episódio. Só com <see cref="PodeSerCurioso"/>.
        /// </summary>
        private bool DecidirCurioso()
        {
            if (!PodeSerCurioso) return false;
            switch (_s.Curiosidade)
            {
                case EstagioDaCuriosidade.IrVer:
                    return IrVer();
                case EstagioDaCuriosidade.IndoVer:
                    if (MeuMonitor == _s.FocoDoPrimeiroPlano)
                    {
                        _s = _s with { IdaFeitaNesteFoco = true };
                        Estagio(EstagioDaCuriosidade.Aproximar, "chegou ao monitor do foco (chega perto)");
                        return PedirVao();
                    }
                    if (MeuMonitor != _s.MonitorDaIda)
                    {
                        // Num monitor do meio do caminho: replaneja daqui.
                        Estagio(EstagioDaCuriosidade.IrVer, "no meio do caminho (segue)");
                        return IrVer();
                    }
                    OlharDeLonge("a ida não saiu");
                    return true;
                case EstagioDaCuriosidade.Aproximar:
                    return PedirVao();
                case EstagioDaCuriosidade.PedindoVao:
                    return true;
                case EstagioDaCuriosidade.Aproximando:
                    Satisfazer("a aproximação parou");
                    return false;
                case EstagioDaCuriosidade.Olhando:
                    DecidirOlhando();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Ir ver (DEC-026, item 3; DEC-037, item 4): anda até a porta do lado que leva ao foco (sem vizinho direto, a que o
        /// aproxima dele) e atravessa pelas passagens da Fase 5, sem sorteio; sem caminho, olha de longe.
        /// </summary>
        private bool IrVer()
        {
            if (_s.FocoDoPrimeiroPlano is not { } foco || foco == MeuMonitor || _s.IdaFeitaNesteFoco || Ocupado(foco)
                || _s.Topologia is not { } t || t.PorChave(foco) is not { } alvo || !Mundo(out MonitorDoDesktop m, out _, out _))
            {
                Satisfazer("nada a ver");
                return false;
            }
            (bool portaEsquerda, bool portaDireita) = PortasDeTravessia();
            int lado = 0;
            foreach (int l in new[] { -1, 1 })
            {
                if ((l < 0 ? portaEsquerda : portaDireita) && Passagens.Portas(t, m, l, Fechados).Any(p => p.ChaveVizinho == foco)) lado = l;
            }
            if (lado == 0)
            {
                int dx = alvo.Tela.Centro.X - m.Tela.Centro.X;
                int l = Math.Sign(dx);
                if (l != 0 && (l < 0 ? portaEsquerda : portaDireita)) lado = l;
            }
            if (lado == 0)
            {
                OlharDeLonge("sem caminho até o foco");
                return true;
            }
            _s = _s with { MonitorDaIda = m.Chave };
            Estagio(EstagioDaCuriosidade.IndoVer, "vai ver");
            IrAteAPorta(lado, Perfil.DistanciaAndandoMinima, "IDLE + CURIOSIDADE: vai ver o outro monitor (anda até a porta)");
            return true;
        }

        /// <summary>Sem caminho até o foco: vira para o lado dele, com a cara curiosa, e a ida daquele foco conta como feita.</summary>
        private void OlharDeLonge(string motivo)
        {
            if (_s.FocoDoPrimeiroPlano is { } foco && _s.Topologia?.PorChave(foco) is { } alvo && _s.Lugar is { } lugar)
            {
                int dx = alvo.Tela.Centro.X - lugar.Ancora.X;
                if (dx != 0) _s = _s with { Direcao = dx > 0 ? Direcao.Direita : Direcao.Esquerda };
            }
            if (!ComOnda) _s = _s with { Expressao = Expressao.Curioso };
            _s = _s with { IdaFeitaNesteFoco = true };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"IDLE + CURIOSIDADE: olha de longe ({motivo})"));
            Satisfazer("olhou de longe");
        }

        /// <summary>Chegar perto (DEC-037, item 5): pede o vão ao adaptador, uma vez, com a espera de guarda.</summary>
        private bool PedirVao()
        {
            if (_s.FocoDoPrimeiroPlano is not { } foco || foco != MeuMonitor)
            {
                Satisfazer("o foco não está aqui");
                return false;
            }
            long geracao = _s.GeracaoDoVao + 1;
            _s = _s with { GeracaoDoVao = geracao };
            _depois.Add(new PedirVaoDaJanelaAtiva(foco, geracao));
            Estagio(EstagioDaCuriosidade.PedindoVao, "pede o vão");
            ArmarCuriosidade(_cfg.EsperaPeloVao);
            return true;
        }

        /// <summary>Chegou perto do trecho da janela: para, virado para ela, com a cara curiosa, e sorteia quantas decisões fica olhando.</summary>
        private void ChegarPerto(string regra)
        {
            PerfilDeEnergia perfil = Perfil;
            (int rodadas, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(perfil.RodadasOlhandoMinimo, perfil.RodadasOlhandoMaximo);
            if (_s.Estado != Estado.Idle) IrPara(Estado.Idle, regra);
            else _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, regra));
            _s = _s with { AleatorioDaPersonalidade = a, RodadasOlhando = rodadas, Direcao = _s.LadoDoOlhar, Curiosidade = EstagioDaCuriosidade.Olhando };
            _s = _s with { Expressao = CaraDeBase() };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, "CURIOSIDADE: olha a janela"));
        }

        /// <summary>
        /// Olhando a janela, a cada decisão da agenda (DEC-037, item 5), no gerador da personalidade: fica olhando, olha ao
        /// redor, coça-se ou perde o interesse; no fim das rodadas, perde o interesse.
        /// </summary>
        private void DecidirOlhando()
        {
            PerfilDeEnergia perfil = Perfil;
            int rodadas = _s.RodadasOlhando - 1;
            _s = _s with { RodadasOlhando = rodadas };
            if (rodadas <= 0)
            {
                PerderOInteresse();
                return;
            }
            (int escolha, Aleatorio a) = _s.AleatorioDaPersonalidade.Ponderado([.. perfil.PesosOlhando]);
            _s = _s with { AleatorioDaPersonalidade = a };
            switch (escolha)
            {
                case 1:
                    GestoOlhando(Gesto.OlharAoRedor);
                    break;
                case 2:
                    GestoOlhando(Gesto.Cocar);
                    break;
                case 3:
                    PerderOInteresse();
                    break;
                default:
                    _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, "IDLE + CURIOSIDADE: continua olhando"));
                    break;
            }
        }

        private void GestoOlhando(Gesto gesto)
        {
            PerfilDeEnergia perfil = Perfil;
            (int passos, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
            _s = _s with { AleatorioDaPersonalidade = a, Gesto = gesto, PassosDoGesto = passos };
            _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE + CURIOSIDADE: gesto {gesto} olhando"));
        }

        /// <summary>Perde o interesse do jeito da energia (DEC-037, item 5): Baixa senta; Média espreguiça; Alta brinca.</summary>
        private void PerderOInteresse()
        {
            PerfilDeEnergia perfil = Perfil;
            Satisfazer("perdeu o interesse");
            switch (_s.Preferencias.Energia)
            {
                case NivelDeEnergia.Baixa:
                    _s = _s with { Expressao = Expressao.Sonolento };
                    IrPara(Estado.Resting, "IDLE + CURIOSIDADE: perdeu o interesse (descansa)");
                    break;
                default:
                    Gesto gesto = _s.Preferencias.Energia == NivelDeEnergia.Alta ? Gesto.Brincar : Gesto.Espreguicar;
                    (int passos, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
                    _s = _s with { AleatorioDaPersonalidade = a, Gesto = gesto, PassosDoGesto = passos };
                    if (gesto == Gesto.Espreguicar && !ComOnda && _s.Preferencias.EmocaoDominante is null) _s = _s with { Expressao = Expressao.Entediado };
                    _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE + CURIOSIDADE: perdeu o interesse ({gesto})"));
                    break;
            }
        }

        /// <summary>
        /// O fim de um episódio: com o foco mudado no meio, a contagem recomeça com o novo; senão, espera o intervalo do perfil.
        /// </summary>
        private void Satisfazer(string motivo)
        {
            bool focoNovo = _s.FocoDoEpisodio is { } doEpisodio && _s.FocoDoPrimeiroPlano is { } foco && doEpisodio != foco;
            _s = _s with { FocoDoEpisodio = null, RodadasOlhando = 0 };
            if (focoNovo)
            {
                Estagio(EstagioDaCuriosidade.Aguardando, $"{motivo} (aguarda de novo)");
                ArmarCuriosidade(RestanteDoLimiar);
                return;
            }
            Estagio(EstagioDaCuriosidade.Satisfeita, motivo);
            ArmarCuriosidade(Perfil.IntervaloEntreCuriosidades);
        }

        // ---------------------------------------------------------------- o usuário

        /// <summary>
        /// A dispensa (DEC-037, item 6): soltar, mostrar pelo comando ou redefinir a posição. A curiosidade não o move até o
        /// disparo dela; a ida daquele foco acaba.
        /// </summary>
        private void Dispensar(string regra)
        {
            if (!_cfg.Curiosidade || !_s.Carregado) return;
            _s = _s with { DispensaAtiva = true, IdaFeitaNesteFoco = true, FocoDoEpisodio = null, RodadasOlhando = 0, Curiosidade = EstagioDaCuriosidade.Nenhuma };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CURIOSIDADE: dispensada ({regra})"));
            ArmarCuriosidade(Perfil.EsperaDepoisDoUsuario);
        }

        /// <summary>Pegar o Buzzy indo ver ou chegando perto encerra o episódio (DEC-037, item 6); olhando, ele volta a olhar.</summary>
        private void PegarNoEpisodio()
        {
            if (!_cfg.Curiosidade) return;
            if (_s.Curiosidade is EstagioDaCuriosidade.IndoVer or EstagioDaCuriosidade.IrVer)
            {
                _s = _s with { IdaFeitaNesteFoco = true };
                Satisfazer("pego na ida");
            }
            else if (_s.Curiosidade == EstagioDaCuriosidade.Aproximando)
            {
                Satisfazer("pego chegando perto");
            }
        }

        /// <summary>O disparo da curiosidade em <see cref="Concluir"/>: armar substitui o pendente; saindo, cancela.</summary>
        private void EfeitosDaCuriosidade(List<Efeito> tempo)
        {
            if (!_cfg.Curiosidade) return;
            if (_s.Estado == Estado.Exiting)
            {
                if (_s.CuriosidadeAgendada) tempo.Add(new CancelarCuriosidade());
                _s = _s with { CuriosidadeAgendada = false };
                return;
            }
            if (_armarCuriosidade is { } espera)
            {
                long geracao = _s.GeracaoDaCuriosidade + 1;
                tempo.Add(new AgendarCuriosidade(espera, geracao));
                _s = _s with { GeracaoDaCuriosidade = geracao, CuriosidadeAgendada = true };
            }
            else if (_cancelarCuriosidade && _s.CuriosidadeAgendada)
            {
                tempo.Add(new CancelarCuriosidade());
                _s = _s with { CuriosidadeAgendada = false };
            }
        }
    }

    /// <summary>
    /// Onde ele fica para olhar o trecho da janela (DEC-037, item 5): ao lado dele, do lado mais perto, com a meia largura
    /// do sprite mais a folga entre o sprite e a borda do trecho, virado para a janela; com o trecho na largura toda
    /// (maximizada), no canto do lado onde está, virado para dentro. Sempre dentro das laterais do chão. Função pura.
    /// </summary>
    public static (double X, Direcao Lado) PontoDeOlhar(RetanguloPx area, Superficies sup, VaoDaJanela vao, double x, int larguraDoSprite, double folga)
    {
        double parte = area.Largura / (double)VaoDaJanela.Partes;
        double inicio = area.Esquerda + (vao.Inicio * parte), fim = area.Esquerda + (vao.Fim * parte);
        if (vao.Inicio == 0 && vao.Fim == VaoDaJanela.Partes)
        {
            bool aEsquerda = x <= (sup.Esquerda + sup.Direita) / 2.0;
            return aEsquerda ? (sup.Esquerda, Direcao.Direita) : (sup.Direita, Direcao.Esquerda);
        }
        double afastamento = (larguraDoSprite / 2.0) + folga;
        double antes = inicio - afastamento, depois = fim + afastamento;
        bool cabeAntes = antes >= sup.Esquerda, cabeDepois = depois <= sup.Direita;
        bool ficaAntes = cabeAntes && cabeDepois ? Math.Abs(x - antes) <= Math.Abs(x - depois) : cabeAntes || (!cabeDepois && Math.Abs(x - antes) <= Math.Abs(x - depois));
        return ficaAntes
            ? (Math.Clamp(antes, sup.Esquerda, sup.Direita), Direcao.Direita)
            : (Math.Clamp(depois, sup.Esquerda, sup.Direita), Direcao.Esquerda);
    }
}
