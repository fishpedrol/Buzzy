namespace Buzzy.Core.Personagem;

// Curiosidade. O núcleo só recebe a chave opaca do monitor em foco e, quando pede, o vão horizontal da
// janela em 16 avos. Foco 30 s noutro monitor: vai ver (uma ida por foco). Foco no monitor dele por muito
// tempo: chega perto da janela e fica olhando. Nada é periódico: age na próxima decisão em IDLE ou no
// disparo do timer. Soltar, mostrar pelo comando e redefinir a posição ligam a dispensa.
public static partial class Maquina
{
    private sealed partial class Passo
    {
        // Pedido deste passo: armar (substitui o pendente) ou cancelar.
        private TimeSpan? _armarCuriosidade;
        private bool _cancelarCuriosidade;

        private static bool EhDaCuriosidade(Evento evento) => evento is ForegroundMonitorChanged or CuriosityTimer or ActiveWindowSpan;

        // 30 s menos os 2 s que o adaptador já esperou.
        private TimeSpan RestanteDoLimiar => Maior(_cfg.LimiarDeOutroMonitor - CarenciaDoFocoNoAdaptador, TimeSpan.FromSeconds(1));

        // O adaptador espera isso antes de publicar o foco.
        private static readonly TimeSpan CarenciaDoFocoNoAdaptador = TimeSpan.FromSeconds(2);

        private static TimeSpan Maior(TimeSpan a, TimeSpan b) => a > b ? a : b;

        private string? MeuMonitor => _s.Lugar?.Monitor.Chave;

        private bool Ocupado(string chave) => _s.Ocupados.Contem(chave);

        private bool ParanoiaNaFrente => ComOnda && _s.Onda?.Tipo == Onda.Paranoico;

        // Troca de foco não interrompe um episódio em curso.
        private bool EpisodioEmCurso => _s.Curiosidade is EstagioDaCuriosidade.IndoVer or EstagioDaCuriosidade.PedindoVao
            or EstagioDaCuriosidade.Aproximando or EstagioDaCuriosidade.Olhando;

        // Única condição pra curiosidade agir. IDLE já implica visível e sem ninguém arrastando.
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

        // Foco novo recomeça a contagem (28 s que faltam dos 30). Com dispensa ou episódio em curso, só o
        // foco muda; o episódio revê a decisão quando terminar.
        private void MudarFoco(string chave)
        {
            if (chave == _s.FocoDoPrimeiroPlano) return;
            _s = _s with { FocoDoPrimeiroPlano = chave, IdaFeitaNesteFoco = false };
            if (_s.DispensaAtiva || EpisodioEmCurso) return;
            // Sem transição de propósito: troca de foco não gera linha de log.
            _s = _s with { Curiosidade = EstagioDaCuriosidade.Aguardando };
            ArmarCuriosidade(RestanteDoLimiar);
        }

        // Disparo de geração velha (substituído ou cancelado) é ignorado.
        private void DispararCuriosidade(long geracao)
        {
            if (!_s.CuriosidadeAgendada || geracao != _s.GeracaoDaCuriosidade) return;
            _s = _s with { CuriosidadeAgendada = false };
            if (_s.DispensaAtiva)
            {
                // Fim da dispensa: recomeça a contagem com o foco de agora.
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

        // Contagem venceu. Foco noutro monitor livre e sem ida feita: vai ver. No monitor dele, depois do
        // tempo do perfil: chega perto. Senão espera o intervalo.
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
                // Ida já feita ou alvo ocupado: espera um foco novo, sem armar timer à toa.
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

        // O vão vira o destino no chão e é descartado. Sem vão, ou sem poder agir, o episódio termina.
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

        // Roda antes da agenda em IDLE. Devolve true se a curiosidade ficou com a decisão.
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

        // Anda até a porta que leva ao foco (sem vizinho direto, a que aproxima dele) e atravessa, sem
        // sorteio. Sem caminho, olha de longe.
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

        // Vira pro lado do foco com cara curiosa; conta como ida feita.
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

        // Pede o vão uma vez, com timer de guarda caso não chegue.
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

        // Para virado pra janela e sorteia quantas decisões fica olhando.
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

        // A cada decisão: continua olhando, olha ao redor, se coça ou perde o interesse.
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

        // Energia baixa senta, média espreguiça, alta brinca.
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

        // Fim de episódio. Se o foco mudou no meio, recomeça a contagem; senão espera o intervalo do perfil.
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

        // Depois de soltar, mostrar ou redefinir a posição, a curiosidade não o move até o timer vencer.
        private void Dispensar(string regra)
        {
            if (!_cfg.Curiosidade || !_s.Carregado) return;
            _s = _s with { DispensaAtiva = true, IdaFeitaNesteFoco = true, FocoDoEpisodio = null, RodadasOlhando = 0, Curiosidade = EstagioDaCuriosidade.Nenhuma };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CURIOSIDADE: dispensada ({regra})"));
            ArmarCuriosidade(Perfil.EsperaDepoisDoUsuario);
        }

        // Pegar indo ver ou chegando perto encerra o episódio; olhando, ele volta a olhar.
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

        // Armar substitui o pendente; saindo do app, cancela.
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

    // Fica ao lado do trecho, do lado mais perto, afastado meia largura do sprite + folga, virado pra janela.
    // Janela maximizada: fica no canto onde está, virado pra dentro. Sempre dentro das laterais do chão.
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
