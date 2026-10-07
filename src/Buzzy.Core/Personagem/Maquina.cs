using System.Globalization;

namespace Buzzy.Core.Personagem;

public sealed record Resultado(EstadoDoNucleo Estado, IReadOnlyList<Efeito> Efeitos, IReadOnlyList<Transicao> Transicoes);

// Máquina de estados do personagem: função pura (estado, evento) -> (estado novo, efeitos). Não toca relógio,
// arquivo nem Windows; o tempo só anda por Tick e pelos timers que ela mesma agendou.
// Tamagotchi em Maquina.Itens.cs e Maquina.Onda.cs.
public static partial class Maquina
{
    public static Resultado Aplicar(EstadoDoNucleo estado, Evento evento, ConfiguracaoDoNucleo config)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(config);
        var passo = new Passo(estado, config);
        passo.Tratar(evento);
        return passo.Concluir();
    }

    // Usa o pixel dos pés, logo acima da âncora: a âncora é a borda de baixo exclusiva do sprite e, com
    // monitores empilhados, já cairia no monitor de baixo.
    public static MonitorDoDesktop MonitorDaAncora(Topologia topologia, PontoPx ancora)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return topologia.MonitorMaisProximo(Posicionador.PixelDosPes(ancora));
    }

    // Estados em que a agenda mantém um timer pendente.
    public static bool DecideNoEstado(Estado estado)
        => estado is Estado.Idle or Estado.Resting or Estado.Climbing or Estado.Hanging or Estado.Peeking;

    private sealed partial class Passo
    {
        private readonly EstadoDoNucleo _inicio;
        private readonly ConfiguracaoDoNucleo _cfg;
        private readonly List<Efeito> _antes = [];
        private readonly List<Efeito> _depois = [];
        private readonly List<Transicao> _transicoes = [];
        private EstadoDoNucleo _s;
        private bool _reagendar;

        internal Passo(EstadoDoNucleo estado, ConfiguracaoDoNucleo config)
        {
            _inicio = estado;
            _cfg = config;
            _s = estado with { Sinal = Sinal.Nenhum };
        }

        // Já ajustado pela onda, se houver.
        private PerfilDeEnergia Perfil => PerfilEfetivo(_s, _cfg);

        internal void Tratar(Evento evento)
        {
            if (_s.Estado == Estado.Exiting) return;

            // Recurso desligado: descarta o evento antes de tudo, até antes de encerrar o gesto curto,
            // pra o núcleo ficar exatamente como antes.
            if (!_cfg.Tamagotchi && EhDoTamagotchi(evento)) return;
            if (!_cfg.Curiosidade && EhDaCuriosidade(evento)) return;
            if (!_cfg.ConfiguracoesDisponiveis && evento is CmdSetEnergy or CmdSetAlwaysOnTop or CmdSetScale) return;

            // Gesto curto termina com qualquer evento de prioridade acima do relógio (PRESS, CMD_*, painel, sistema).
            if (evento.Origem >= Origem.Sistema && _s.Gesto != Gesto.Nenhum)
                EncerrarGesto();

            switch (evento)
            {
                case Loaded e: Carregar(e); break;
                case Press e: Pressionar(e.Cursor); break;
                case Click: Clicar(); break;
                case DoubleClick: CliqueDuplo(); break;
                case DragStart: IniciarArraste(); break;
                case DragMove e: Arrastar(e.Cursor); break;
                case DragEnd e: Soltar(e.Cursor); break;
                case DragCancel: CancelarArraste(); break;
                case ContextMenu e: MenuDeContexto(e.Cursor); break;
                case EnergyPanelOpen: AbrirPainel(); break;
                case EnergySelected e: EscolherEnergia(e.Nivel); break;
                case EnergyPanelClose: FecharPainel(); break;
                case CmdHide: Esconder(MotivoDoOcultamento.PorUsuario, "CMD_HIDE"); break;
                case CmdShow: MostrarPorComando(); break;
                case CmdPauseAutonomy: Pausar(true); break;
                case CmdResumeAutonomy: Pausar(false); break;
                case CmdOpenSettings: AbrirConfiguracoes(); break;
                case CmdResetPosition: RedefinirPosicao(); break;
                case CmdExit: Sair("CMD_EXIT"); break;
                case CmdSetDominantEmotion e: EscolherEmocao(e.Emocao); break;
                case CmdSetAdultContent e: EscolherConteudoAdulto(e.Ligado); break;
                case CmdSetAdultItemEnabled e: EscolherItemAdulto(e.Item, e.Ligado); break;
                case CmdSetSelfUseItem e: EscolherUsoPorContaPropria(e.Item, e.Ligado); break;
                case CmdSetFullscreenMode e: EscolherModoTelaCheia(e.Ligado); break;
                case CmdSetCrossMonitors e: EscolherTravessia(e.Ligado); break;
                case CmdSetEnergy e: EscolherEnergiaPorComando(e.Nivel); break;
                case CmdSetAlwaysOnTop e: EscolherSempreNoTopo(e.Ligado); break;
                case CmdSetScale e: EscolherEscala(e.Escala); break;
                case CmdSummonItem e: InvocarItem(e.Item); break;
                case CmdClearItems: RecolherItens(); break;
                case ItemPress e: PegarItem(e.Id, e.Cursor); break;
                case ItemDragStart e: IniciarArrasteDoItem(e.Id); break;
                case ItemDragMove e: ArrastarItem(e.Id, e.Cursor); break;
                case ItemDragEnd e: SoltarItem(e.Id, e.Cursor); break;
                case ItemRelease e: LargarItem(e.Id); break;
                case TopologyChanged e: MudarTopologia(e.Topologia); break;
                case SessionLocked: Esconder(MotivoDoOcultamento.PorSessao, "SESSION_LOCKED"); break;
                case SessionUnlocked: Reaparecer(MotivoDoOcultamento.PorSessao, "SESSION_UNLOCKED"); break;
                case Suspending: Esconder(MotivoDoOcultamento.PorSuspensao, "SUSPENDING"); break;
                case Resumed: Reaparecer(MotivoDoOcultamento.PorSuspensao, "RESUMED"); break;
                case SessionEnding: Sair("SESSION_ENDING"); break;
                case FullscreenTargetsChanged e: MudarTelaCheia(e.Ocupados); break;
                case SettingsChanged e: MudarPreferencias(e.Preferencias); break;
                case Tick: Passar(); break;
                case MovementSignal e: Sinalizar(e.Sinal); break;
                case AutonomyTimer e: Decidir(e.Geracao); break;
                case ItemEffectTimer e: AvancarOnda(e.Geracao); break;
                case ForegroundMonitorChanged e: MudarFoco(e.Chave); break;
                case CuriosityTimer e: DispararCuriosidade(e.Geracao); break;
                case ActiveWindowSpan e: ReceberVao(e.Geracao, e.Vao); break;
                case ExpressionChange e: _s = _s with { Expressao = e.Expressao }; break;
                default: throw new ArgumentException($"Evento desconhecido: {evento}.", nameof(evento));
            }
        }

        // ---------------------------------------------------------------- carga e topologia

        private void Carregar(Loaded e)
        {
            // Só a primeira carga vale. Antes dela o estado só pode ser BOOTING, HIDDEN (pedido de
            // esconder, bloqueio ou suspensão anteriores à carga) ou EXITING.
            if (_s.Carregado || _s.Estado is not (Estado.Booting or Estado.Hidden)) return;

            // Restaura em cascata: pela chave do monitor, pelo retângulo dele na época, ou no principal.
            Posicionamento lugar;
            PosicaoDoPersonagem posicao;
            string restaurada = "";
            if (e.PosicaoSalva is { } salva)
            {
                (lugar, posicao, OrigemDaRestauracao origem) = Posicionador.Restaurar(e.Topologia, salva, _cfg.Tamanho);
                restaurada = origem switch
                {
                    OrigemDaRestauracao.PelaChave => "; posição salva restaurada pela chave",
                    OrigemDaRestauracao.PeloRetangulo => "; posição salva restaurada pelo retângulo do monitor",
                    _ => "; posição salva restaurada no monitor principal",
                };
            }
            else
            {
                lugar = Posicionador.Inicial(e.Topologia, _cfg.Tamanho);
                posicao = Posicionador.Descrever(lugar);
            }
            _s = _s with { Carregado = true, Topologia = e.Topologia, Preferencias = Sanear(e.Preferencias, _cfg), Lugar = lugar, Posicao = posicao };

            // Postura salva: a acomodação abaixo o devolve escondido na mesma borda ou agarrado e preso (longe de
            // parede/cipó, a marca some). Sem o esconderijo no clique duplo, ignora a borda, senão ele não teria
            // como sair. Valor fora do enum vem do arquivo e é descartado.
            if (e.PosicaoSalva is not null)
            {
                LadoDoEsconderijo lado = _cfg.EsconderijoNoCliqueDuplo && Enum.IsDefined(e.Esconderijo) ? e.Esconderijo : LadoDoEsconderijo.Nenhum;
                _s = _s with { Esconderijo = lado, PresoPeloUsuario = e.PresoPeloUsuario };
            }
            if (_s.Preferencias.EmocaoDominante is { } dominante) _s = _s with { Expressao = dominante };

            if (_s.Estado == Estado.Booting)
                Acomodar(lugar.Ancora, "BOOTING: configurações e topologia carregadas" + restaurada, posicao);
            else if (_s.Motivo == MotivoDoOcultamento.Nenhum)
                Acomodar(lugar.Ancora, "HIDDEN: pedido de mostrar anterior à carga" + restaurada, posicao);

            // Tela cheia vale desde a partida: os ocupados avisados antes da carga estão em cache.
            if (_s.Estado.Visivel() && _s.Preferencias.ModoTelaCheia && _s.Lugar is { } l && _s.Ocupados.Contem(l.Monitor.Chave))
                SairDoMonitorOcupado("BOOTING: carga sobre um monitor ocupado pela tela cheia");
        }

        // Posições guardadas e itens acompanham a topologia em qualquer estado, sem mover a janela. Nos estados
        // que revalidam: monitor dele com a mesma geometria (só transladado ou chave nova) -> continua; geometria
        // mudou -> SETTLING na mesma posição relativa; monitor sumiu -> SETTLING no mais próximo. USING age como
        // REACTING (a onda continua). Em PRESSED/DRAGGING nada é validado aqui.
        private void MudarTopologia(Topologia nova)
        {
            Topologia? antiga = _s.Topologia;
            _s = _s with { Topologia = nova };
            if (antiga is null || antiga.MesmaConfiguracao(nova)) return;

            ReacomodarItens(antiga, nova);
            if (_s.Posicao is { } posicao)
                _s = _s with { Posicao = Posicionador.Rebasear(antiga, nova, posicao, _cfg.Tamanho) };
            if (_s.RetornoDaTelaCheia is { } retorno)
                _s = _s with { RetornoDaTelaCheia = Posicionador.Rebasear(antiga, nova, retorno, _cfg.Tamanho) };

            // No arraste o Windows leva janela e cursor junto com o monitor físico, então o lugar acompanha sem
            // validar. Soltar antes do próximo DRAG_MOVE fica no mesmo monitor físico.
            if (_s.Estado == Estado.Dragging && _s.Lugar is { } arrastado)
                _s = _s with { Lugar = LugarLivre(nova, Posicionador.AcompanharPonto(antiga, nova, arrastado.Ancora)) };

            // Nos outros estados só atualiza o cache; valida depois, ao soltar, clicar, reaparecer ou carregar.
            GrupoDoEstado grupo = _s.Estado.Grupo();
            bool revalida = grupo is GrupoDoEstado.Autonomo or GrupoDoEstado.Fisico || _s.Estado is Estado.Reacting or Estado.Using;
            if (!revalida || _s.Posicao is null || _s.Lugar is not { } lugar) return;

            // Travessia guarda chaves e coordenadas absolutas: qualquer mudança a desfaz e ele se acomoda pela
            // posição relativa, mesmo se só o outro monitor mudou. Andando até a partida do salto ainda não é
            // travessia: só o plano cai.
            if (_s.Movimento.Travessia is { } travessia)
            {
                if (_s.Estado == Estado.Jumping || (_s.Estado == Estado.Walking && travessia.Tipo == TipoDeTravessia.Andando))
                {
                    // Volta da tela cheia interrompida: termina na posição de antes, já rebaseada.
                    if (travessia.Pulo == PuloDaTelaCheia.Volta && _s.RetornoDaTelaCheia is { } retornoDoPulo)
                    {
                        (Posicionamento rv, PosicaoDoPersonagem pv) = Posicionador.Reacomodar(nova, retornoDoPulo, _cfg.Tamanho);
                        _s = _s with { RetornoDaTelaCheia = null };
                        Acomodar(rv.Ancora, "TOPOLOGY_CHANGED: pulo da tela cheia interrompido, de volta à posição anterior", pv);
                        return;
                    }
                    (Posicionamento ra, PosicaoDoPersonagem pa) = Posicionador.Reacomodar(nova, _s.Posicao, _cfg.Tamanho);
                    Acomodar(ra.Ancora, "TOPOLOGY_CHANGED: travessia interrompida", pa);
                    // A ida interrompida por cima do monitor ainda ocupado sai dele de novo.
                    if (travessia.Pulo == PuloDaTelaCheia.Ida && _s.Lugar is { } interrompido && Fechado(interrompido.Monitor.Chave))
                        SairDoMonitorOcupado("TOPOLOGY_CHANGED: pulo da tela cheia interrompido sobre o monitor ocupado");
                    return;
                }
                _s = _s with { Movimento = _s.Movimento with { Travessia = null, Restante = 0 } };
            }

            MonitorDoDesktop? correspondente = Posicionador.MonitorCorrespondente(antiga, nova, lugar.Monitor.Chave, lugar.Monitor.Tela);
            if (correspondente is not null && Posicionador.SoTranslacao(lugar.Monitor, correspondente, out int dx, out int dy))
            {
                ContinuarNoMonitor(correspondente, dx, dy);
                return;
            }

            (Posicionamento r, PosicaoDoPersonagem p) = Posicionador.Reacomodar(nova, _s.Posicao, _cfg.Tamanho);
            Acomodar(r.Ancora, correspondente is null ? "TOPOLOGY_CHANGED: o monitor do personagem foi desconectado" : "TOPOLOGY_CHANGED", p);
        }

        // Monitor só transladado: tudo segue em curso e anda junto. A posição é redescrita do lugar novo pra não
        // divergir 1 px da âncora por arredondamento. Relógio e agenda não mudam.
        private void ContinuarNoMonitor(MonitorDoDesktop novo, int dx, int dy)
        {
            Posicionamento antes = _s.Lugar!;
            var ancora = new PontoPx(antes.Ancora.X + dx, antes.Ancora.Y + dy);
            var lugar = new Posicionamento(novo, ancora, antes.Tamanho, antes.Retangulo.Deslocado(dx, dy));
            _s = _s with { Lugar = lugar, Posicao = Posicionador.Descrever(lugar) };
            // Posição fina só vale em movimento; nos outros, IrPara parte da âncora.
            if (_s.Estado.EmMovimento())
                _s = _s with { Movimento = _s.Movimento with { X = _s.Movimento.X + dx, Y = _s.Movimento.Y + dy } };

            string regra = dx == 0 && dy == 0
                ? (novo.Chave == antes.Monitor.Chave ? "TOPOLOGY_CHANGED: o monitor do personagem não mudou" : "TOPOLOGY_CHANGED: o monitor do personagem mudou de chave")
                : string.Create(CultureInfo.InvariantCulture, $"TOPOLOGY_CHANGED: o monitor do personagem foi transladado ({dx},{dy})");
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, regra));
        }

        // ---------------------------------------------------------------- ação direta

        private void Pressionar(PontoPx cursor)
        {
            if (!_s.Estado.AceitaPressionar() || _s.Lugar is null) return;
            PontoPx ancora = _s.Lugar.Ancora;
            // Pegou no ar a volta da tela cheia: quem arrasta assume, o retorno acaba.
            if (PuloEmVoo is { Pulo: PuloDaTelaCheia.Volta }) _s = _s with { RetornoDaTelaCheia = null };
            _s = _s with { Pegada = new PontoPx(cursor.X - ancora.X, cursor.Y - ancora.Y), PassosRestantes = 0, TelaCheiaMudouNoGesto = false };
            if (_cfg.Personalidade) _s = _s with { ReagiuDe = _s.Estado };
            PegarNoEpisodio();
            IrPara(Estado.Pressed, "PRESS sobre pixel opaco");
        }

        // Arraste sempre escolhe posição e descarta o retorno da tela cheia. Clique/cancelamento só descartam se a
        // tela cheia mudou no gesto. Se o modo foi desligado no meio, volta agora pra posição de antes.
        private void FimDoGestoDoUsuario(bool escolheuPosicao)
        {
            if (escolheuPosicao || _s.TelaCheiaMudouNoGesto)
            {
                _s = _s with { RetornoDaTelaCheia = null };
            }
            else if (!_s.Preferencias.ModoTelaCheia && _s.RetornoDaTelaCheia is { } retorno && _s.Topologia is { } topologia)
            {
                (Posicionamento lugar, PosicaoDoPersonagem posicao) = Posicionador.Reacomodar(topologia, retorno, _cfg.Tamanho);
                _s = _s with { Lugar = lugar, Posicao = posicao, RetornoDaTelaCheia = null };
            }
            _s = _s with { TelaCheiaMudouNoGesto = false };
        }

        // Roda em toda saída de PRESSED. Com o botão apertado a topologia só rebaseia a posição e o lugar fica nas
        // coordenadas antigas. Se o monitor mudou ou sumiu, reacomoda e valida aqui, sem sair do gesto.
        private void ReancorarOGesto()
        {
            if (_s.Estado != Estado.Pressed || _s.Lugar is not { } lugar || _s.Topologia is not { } topologia || _s.Posicao is not { } posicao
                || Equals(topologia.PorChave(lugar.Monitor.Chave), lugar.Monitor)) return;
            (Posicionamento acompanhado, PosicaoDoPersonagem descrita) = Posicionador.Reacomodar(topologia, posicao, _cfg.Tamanho);
            (Posicionamento validado, PosicaoDoPersonagem validada, _) = Validar(acompanhado.Ancora, descrita);
            _s = _s with { Lugar = validado, Posicao = validada };
        }

        private void Clicar()
        {
            if (_s.Estado != Estado.Pressed) return;
            ReancorarOGesto();
            FimDoGestoDoUsuario(escolheuPosicao: false);
            if (!_cfg.Personalidade)
            {
                _s = _s with { PassosRestantes = _cfg.PassosDaReacao, Expressao = Expressao.Feliz, Sinal = Sinal.FoiClicado };
                IrPara(Estado.Reacting, "CLICK");
                return;
            }
            _s = _s with { CliquesSeguidos = _s.CliquesSeguidos + 1 };
            (VarianteDaReacao variante, string regra) = EscolherReacao();
            // Escondido ou preso: reação padrão com a duração fixa.
            int passos = variante == VarianteDaReacao.Padrao && (_s.Esconderijo != LadoDoEsconderijo.Nenhum || _s.PresoPeloUsuario) ? _cfg.PassosDaReacao : Perfil.PassosDaReacao;
            _s = _s with { PassosRestantes = passos, Expressao = CaraDaReacao(variante), Sinal = Sinal.FoiClicado, Reacao = variante };
            IrPara(Estado.Reacting, $"CLICK: {regra}");
        }

        // Sem sorteio; a ordem dos testes importa.
        private (VarianteDaReacao Variante, string Regra) EscolherReacao()
        {
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum) return (VarianteDaReacao.Padrao, "reação de antes (escondido)");
            if (_s.PresoPeloUsuario) return (VarianteDaReacao.Padrao, "reação de antes (preso)");
            if (_s.ReagiuDe == Estado.Resting) return (VarianteDaReacao.Susto, "susto (descansava)");
            if (OlhandoAJanela) return (VarianteDaReacao.Flagra, "flagra (olhava a janela)");
            if (_s.CliquesSeguidos >= 2) return (VarianteDaReacao.Empolgada, "empolgada (cliques seguidos)");
            return _s.Preferencias.Energia switch
            {
                NivelDeEnergia.Baixa => (VarianteDaReacao.Preguica, "preguiçosa (energia Baixa)"),
                NivelDeEnergia.Alta => (VarianteDaReacao.Empolgada, "empolgada (energia Alta)"),
                _ => (VarianteDaReacao.Padrao, "reação de antes (energia Média)"),
            };
        }

        // A cara do primeiro quadro do clipe de cada variante.
        private static Expressao CaraDaReacao(VarianteDaReacao variante) => variante switch
        {
            VarianteDaReacao.Susto or VarianteDaReacao.Flagra => Expressao.Surpreso,
            VarianteDaReacao.Empolgada => Expressao.Empolgado,
            VarianteDaReacao.Preguica => Expressao.Sonolento,
            _ => Expressao.Feliz,
        };

        // Clique aqui é um flagra, e depois ele volta a olhar.
        private bool OlhandoAJanela => _cfg.Curiosidade && _s.Curiosidade == EstagioDaCuriosidade.Olhando;

        private void CliqueDuplo()
        {
            Estado de = _s.Estado;
            // Traz o lugar do gesto pra topologia atual: é dali que ele se esconde ou fica.
            ReancorarOGesto();
            if (_cfg.EsconderijoNoCliqueDuplo)
            {
                if (de is Estado.Pressed or Estado.Idle or Estado.Reacting or Estado.Peeking) AlternarEsconderijo(de);
                return;
            }
            if (de is not (Estado.Pressed or Estado.Idle or Estado.Reacting)) return;

            if (_cfg.PainelDeEnergiaDisponivel)
                AbrirPainelInterno();
            else
                _s = _s with { Sinal = Sinal.FoiClicadoDuasVezes, Expressao = Expressao.Rindo };

            if (de == Estado.Pressed && _s.Lugar is not null)
            {
                FimDoGestoDoUsuario(escolheuPosicao: false);
                Acomodar(_s.Lugar.Ancora, "DOUBLE_CLICK a partir de PRESSED");
            }
        }

        // Escondido, sai; senão, se esconde atrás da borda mais próxima, só cabeça e mãos pra fora.
        private void AlternarEsconderijo(Estado de)
        {
            if (_s.Topologia is null) return;
            // Fim do gesto primeiro: se a tela cheia foi desligada no meio, ele volta pra posição de antes e
            // se esconde de lá.
            if (de == Estado.Pressed) FimDoGestoDoUsuario(escolheuPosicao: false);
            if (_s.Lugar is not { } lugar) return;
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum)
            {
                // Na borda de baixo fica de pé no chão; numa lateral, fica grudado na parede.
                _s = _s with { Esconderijo = LadoDoEsconderijo.Nenhum, Expressao = Expressao.Feliz, Sinal = Sinal.FoiClicadoDuasVezes };
                Acomodar(lugar.Ancora, "DOUBLE_CLICK: sai do esconderijo", pelaMaoDoUsuario: true);
                return;
            }
            LadoDoEsconderijo lado = LadoMaisProximo(lugar);
            _s = _s with { Esconderijo = lado, Expressao = Expressao.Curioso, Sinal = Sinal.FoiClicadoDuasVezes };
            Acomodar(lugar.Ancora, $"DOUBLE_CLICK: esconde-se atrás da borda ({lado})");
        }

        // Cima se está ao alcance do cipó; lateral se está no alto e junto dela; senão, baixo.
        private LadoDoEsconderijo LadoMaisProximo(Posicionamento lugar)
        {
            Superficies sup = Superficies.Do(_s.Topologia!, lugar.Monitor, lugar.Tamanho);
            double escala = lugar.Monitor.Dpi / 96.0;
            PontoPx a = lugar.Ancora;
            bool noAlto = sup.Chao - a.Y >= _cfg.Fisica.AlturaMinimaParaAgarrar * escala;
            if (a.Y - sup.Teto <= _cfg.Fisica.DistanciaParaOCipo * escala) return LadoDoEsconderijo.Cima;
            double aEsquerda = a.X - sup.Esquerda, aDireita = sup.Direita - a.X;
            bool juntoDeUmaLateral = Math.Min(aEsquerda, aDireita) <= _cfg.Fisica.DistanciaParaAParede * escala;
            if (!noAlto || !juntoDeUmaLateral) return LadoDoEsconderijo.Baixo;
            return aDireita <= aEsquerda ? LadoDoEsconderijo.Direita : LadoDoEsconderijo.Esquerda;
        }

        // O sprite fica inteiro na área útil; quem esconde o corpo é a pose, que só mostra cabeça e mãos.
        private Posicionamento EsconderijoPara(Posicionamento lugar, LadoDoEsconderijo lado)
        {
            Superficies sup = Superficies.Do(_s.Topologia!, lugar.Monitor, lugar.Tamanho);
            PontoPx a = lugar.Ancora;
            PontoPx ancora = lado switch
            {
                LadoDoEsconderijo.Direita => new PontoPx(sup.Direita, Math.Clamp(a.Y, sup.Teto, sup.Chao)),
                LadoDoEsconderijo.Esquerda => new PontoPx(sup.Esquerda, Math.Clamp(a.Y, sup.Teto, sup.Chao)),
                LadoDoEsconderijo.Cima => new PontoPx(Math.Clamp(a.X, sup.Esquerda, sup.Direita), sup.Teto),
                _ => new PontoPx(Math.Clamp(a.X, sup.Esquerda, sup.Direita), sup.Chao),
            };
            return NoLugar(lugar.Monitor, ancora);
        }

        private static readonly Expressao[] ExpressoesDoEscondido = [Expressao.Curioso, Expressao.Travesso, Expressao.Feliz, Expressao.Surpreso, Expressao.Pensativo, Expressao.Rindo];

        // Prioridade: cara da onda, depois emoção dominante, depois uma diferente da atual.
        private Expressao SortearCaraDoEscondido()
        {
            if (FaseEmVigor is { } fase) return SortearCaraDaFase(fase);
            if (_s.Preferencias.EmocaoDominante is { } dominante) return SortearComADominante(dominante);
            (int cara, Aleatorio a) = _s.Aleatorio.Entre(0, ExpressoesDoEscondido.Length - 2);
            Expressao atual = _s.Expressao;
            Expressao[] outras = [.. ExpressoesDoEscondido.Where(e => e != atual)];
            _s = _s with { Aleatorio = a };
            return outras[Math.Min(cara, outras.Length - 1)];
        }

        private void IniciarArraste()
        {
            if (_s.Estado != Estado.Pressed) return;
            // Cancelar antes do primeiro DRAG_MOVE deixa ele neste lugar.
            ReancorarOGesto();
            FecharPainelSeAberto();
            // Arrastar tira do esconderijo e zera os cliques seguidos.
            _s = _s with { Esconderijo = LadoDoEsconderijo.Nenhum, CliquesSeguidos = 0 };
            IrPara(Estado.Dragging, "DRAG_START");
        }

        private void Arrastar(PontoPx cursor)
        {
            if (_s.Estado != Estado.Dragging || _s.Topologia is null) return;
            // Cursor menos a pegada, sem física nem limite.
            var ancora = new PontoPx(cursor.X - _s.Pegada.X, cursor.Y - _s.Pegada.Y);
            _s = _s with { Lugar = LugarLivre(_s.Topologia, ancora) };
        }

        private void Soltar(PontoPx cursor)
        {
            if (_s.Estado != Estado.Dragging) return;
            var ancora = new PontoPx(cursor.X - _s.Pegada.X, cursor.Y - _s.Pegada.Y);
            // Soltar é escolha manual: descarta o retorno da tela cheia.
            FimDoGestoDoUsuario(escolheuPosicao: true);
            Acomodar(ancora, "DRAG_END", pelaMaoDoUsuario: true);
            Dispensar("DRAG_END");
            if (_s.Posicao is { } posicao) GravarComAPostura(posicao);
        }

        private void CancelarArraste()
        {
            if (_s.Lugar is null) return;
            if (_s.Estado == Estado.Dragging)
            {
                // Fica onde está; não volta pro ponto de origem.
                FimDoGestoDoUsuario(escolheuPosicao: true);
                Acomodar(_s.Lugar.Ancora, "DRAG_CANCEL", pelaMaoDoUsuario: true);
                if (_s.Posicao is { } posicao) GravarComAPostura(posicao);
            }
            else if (_s.Estado == Estado.Pressed)
            {
                ReancorarOGesto();
                FimDoGestoDoUsuario(escolheuPosicao: false);
                Acomodar(_s.Lugar.Ancora, "DRAG_CANCEL em PRESSED (captura perdida antes do limiar)");
            }
        }

        private void MenuDeContexto(PontoPx cursor)
        {
            if (!_s.Estado.Visivel() || _s.Estado is Estado.Pressed or Estado.Dragging) return;
            _depois.Add(new AbrirMenu(cursor));
        }

        // ---------------------------------------------------------------- painel de energia

        private void AbrirPainel()
        {
            if (!_cfg.PainelDeEnergiaDisponivel || !_s.Estado.Visivel() || _s.Estado is Estado.Pressed or Estado.Dragging) return;
            AbrirPainelInterno();
        }

        private void AbrirPainelInterno()
        {
            if (_s.PainelAberto) return;
            _s = _s with { PainelAberto = true };
            _depois.Add(new AbrirPainelDeEnergia());
        }

        private void EscolherEnergia(NivelDeEnergia nivel)
        {
            // Valor fora do enum é ignorado.
            if (!_s.PainelAberto || !Enum.IsDefined(nivel)) return;
            if (_s.Preferencias.Energia == nivel) return;
            _s = _s with { Preferencias = _s.Preferencias with { Energia = nivel } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        // ---------------------------------------------------------------- comandos das configurações

        // Não precisa do painel aberto. A agenda armada não muda; a próxima decisão já usa o perfil novo.
        private void EscolherEnergiaPorComando(NivelDeEnergia nivel)
        {
            if (!_s.Carregado || !Enum.IsDefined(nivel) || _s.Preferencias.Energia == nivel) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ENERGY: {nivel}"));
            _s = _s with { Preferencias = _s.Preferencias with { Energia = nivel } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        private void EscolherSempreNoTopo(bool ligado)
        {
            if (!_s.Carregado || _s.Preferencias.SempreNoTopo == ligado) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ALWAYS_ON_TOP: {(ligado ? "ligado" : "desligado")}"));
            _s = _s with { Preferencias = _s.Preferencias with { SempreNoTopo = ligado } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _depois.Add(new AplicarSempreNoTopo(ligado));
        }

        // Só vale na próxima abertura.
        private void EscolherEscala(EscalaDoPersonagem escala)
        {
            if (!_s.Carregado || !Enum.IsDefined(escala) || _s.Preferencias.Escala == escala) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_SCALE: {escala}"));
            _s = _s with { Preferencias = _s.Preferencias with { Escala = escala } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        private void FecharPainel()
        {
            if (!_s.PainelAberto) return;
            _s = _s with { PainelAberto = false };
            _reagendar = true;
        }

        private void FecharPainelSeAberto()
        {
            if (!_s.PainelAberto) return;
            _s = _s with { PainelAberto = false };
            _antes.Add(new FecharPainelDeEnergia());
        }

        // ---------------------------------------------------------------- bandeja, menu e sessão

        private void Esconder(MotivoDoOcultamento motivo, string regra)
        {
            if (_s.Estado == Estado.Hidden)
            {
                // Só troca o motivo por um de precedência maior: usuário > sessão > suspensão > tela cheia.
                if (Precedencia(motivo) > Precedencia(_s.Motivo))
                {
                    _transicoes.Add(new Transicao(Estado.Hidden, Estado.Hidden, $"{regra}: motivo {_s.Motivo} -> {motivo}"));
                    // Deixou de ser por tela cheia: o retorno vira a posição, pra não sobrar pro próximo episódio.
                    if (_s.Motivo == MotivoDoOcultamento.PorTelaCheia)
                        _s = _s with { Posicao = _s.RetornoDaTelaCheia ?? _s.Posicao, RetornoDaTelaCheia = null };
                    _s = _s with { Motivo = motivo };
                }
                return;
            }

            LiberarGestoDoUsuario();
            FixarArrasteInterrompido();
            DarOPuloPorTerminado();
            FecharPainelSeAberto();
            // Item da mão solta a captura; os que caem vão pro chão.
            LiberarItemNaMao();
            AssentarItens();
            _s = _s with { Motivo = motivo, PassosRestantes = 0 };
            IrPara(Estado.Hidden, regra);
            // Com o modo desligado, retorno só sobra de um PRESSED interrompido: volta a valer, como no fim do gesto.
            if (!_s.Preferencias.ModoTelaCheia && _s.RetornoDaTelaCheia is { } retorno)
                _s = _s with { Posicao = retorno, RetornoDaTelaCheia = null };
            GravarPosicaoDoUsuario();
        }

        // Durante a tela cheia grava a posição de antes; a temporária fica só em memória.
        private void GravarPosicaoDoUsuario()
        {
            if ((_s.RetornoDaTelaCheia ?? _s.Posicao) is { } posicao) GravarComAPostura(posicao);
        }

        // Grava junto a borda do esconderijo e o "preso". A ocultação (bandeja, sessão) não é gravada.
        private void GravarComAPostura(PosicaoDoPersonagem posicao)
            => _depois.Add(new GravarPosicao(posicao) { Esconderijo = _s.Esconderijo, PresoPeloUsuario = _s.PresoPeloUsuario });

        private static int Precedencia(MotivoDoOcultamento motivo) => motivo switch
        {
            MotivoDoOcultamento.PorUsuario => 4,
            MotivoDoOcultamento.PorSessao => 3,
            MotivoDoOcultamento.PorSuspensao => 2,
            MotivoDoOcultamento.PorTelaCheia => 1,
            _ => 0,
        };

        private void Reaparecer(MotivoDoOcultamento motivoQueSeDesfaz, string regra)
        {
            // Desbloquear ou acordar nunca mostra o que o usuário escondeu.
            if (_s.Estado != Estado.Hidden || _s.Motivo != motivoQueSeDesfaz) return;
            Mostrar(regra);
            // Reapareceu num monitor ainda em tela cheia: o modo age de novo.
            if (_s.Preferencias.ModoTelaCheia && _s.Lugar is { } lugar && _s.Ocupados.Contem(lugar.Monitor.Chave))
                SairDoMonitorOcupado($"{regra}: reapareceu num monitor ocupado pela tela cheia");
        }

        private void MostrarPorComando()
        {
            Dispensar("CMD_SHOW");
            if (_s.Estado == Estado.Hidden)
            {
                if (_s.Motivo == MotivoDoOcultamento.PorTelaCheia)
                {
                    // Aparece na posição de antes da tela cheia.
                    _s = _s with { Posicao = _s.RetornoDaTelaCheia ?? _s.Posicao, RetornoDaTelaCheia = null };
                }
                else
                {
                    // Mostrar é escolha manual: reaparece onde estava e o fim da tela cheia não o move mais.
                    _s = _s with { RetornoDaTelaCheia = null };
                }
                Mostrar("CMD_SHOW");
                return;
            }
            // Já visível: mostrar na mão durante a tela cheia conta como escolha manual.
            if (_s.Estado.Visivel()) _s = _s with { RetornoDaTelaCheia = null };
        }

        private void Mostrar(string regra)
        {
            if (!_s.Carregado || _s.Topologia is null)
            {
                // Ainda não carregou: aparece quando a carga chegar.
                _s = _s with { Motivo = MotivoDoOcultamento.Nenhum };
                return;
            }
            if (_s.Posicao is null)
            {
                Posicionamento inicial = Posicionador.Inicial(_s.Topologia, _cfg.Tamanho);
                Acomodar(inicial.Ancora, regra, Posicionador.Descrever(inicial));
                return;
            }
            (Posicionamento r, PosicaoDoPersonagem p) = Posicionador.Reacomodar(_s.Topologia, _s.Posicao, _cfg.Tamanho);
            Acomodar(r.Ancora, regra, p);
        }

        private void Pausar(bool pausar)
        {
            if (_s.AutonomiaPausada == pausar) return;
            _s = _s with { AutonomiaPausada = pausar };
            if (!pausar) _reagendar = true;
            // Pausar para a caminhada na hora; parede e cipó resolvem nos passos seguintes.
            // Travessia é atômica: termina, e o fim dela o para.
            if (pausar && _cfg.Movimento && _s.Estado == Estado.Walking && _s.Movimento.Travessia is not { Tipo: TipoDeTravessia.Andando }) IrPara(Estado.Idle, "CMD_PAUSE_AUTONOMY: para de andar");
        }

        private void AbrirConfiguracoes()
        {
            if (_cfg.ConfiguracoesDisponiveis) _depois.Add(new AbrirConfiguracoes());
        }

        private void RedefinirPosicao()
        {
            if (!_s.Carregado || _s.Topologia is null || _s.Estado is Estado.Booting or Estado.Pressed or Estado.Dragging) return;
            Dispensar("CMD_RESET_POSITION");
            Posicionamento inicial = Posicionador.Inicial(_s.Topologia, _cfg.Tamanho);
            _s = _s with { RetornoDaTelaCheia = null };
            if (_s.Estado == Estado.Hidden)
                _s = _s with { Lugar = inicial, Posicao = Posicionador.Descrever(inicial) };
            else
                Acomodar(inicial.Ancora, "CMD_RESET_POSITION", Posicionador.Descrever(inicial));
            if (_s.Posicao is { } posicao) GravarComAPostura(posicao);
        }

        private void Sair(string regra)
        {
            LiberarGestoDoUsuario();
            FixarArrasteInterrompido();
            DarOPuloPorTerminado();
            FecharPainelSeAberto();
            LiberarItemNaMao();
            AssentarItens();
            IrPara(Estado.Exiting, regra);
            GravarPosicaoDoUsuario();
            _depois.Add(new Encerrar());
        }

        private void LiberarGestoDoUsuario()
        {
            if (_s.Estado is Estado.Pressed or Estado.Dragging) _antes.Add(new LiberarCaptura());
        }

        // ---------------------------------------------------------------- emoção dominante

        // Só aceita as 14 caras de humor. Nula = automática: mantém a cara atual até a próxima troca.
        private void EscolherEmocao(Expressao? emocao)
        {
            if (!_s.Carregado || (emocao is { } e && !Expressoes.EhDeHumor(e)) || emocao == _s.Preferencias.EmocaoDominante) return;
            _s = _s with { Preferencias = _s.Preferencias with { EmocaoDominante = emocao } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_DOMINANT_EMOTION: {emocao?.ToString() ?? "Automatica"}"));
            // Com onda, a cara dela manda; a dominante entra quando a onda acabar.
            if (emocao is { } nova && !ComOnda && CaraLivre(_s.Estado)) _s = _s with { Expressao = nova };
        }

        // RESTING, REACTING e USING têm cara própria até acabar; depois volta à de base.
        private static bool CaraLivre(Estado estado) => estado is not (Estado.Resting or Estado.Reacting or Estado.Using);

        // Onda > olhando a janela > emoção dominante > neutra.
        private Expressao CaraDeBase()
            => ComOnda && _s.Onda is { } onda ? _cfg.TabelaDeOndas(onda.Tipo).Cara(onda.Fase)
               : OlhandoAJanela ? Expressao.Curioso
               : _s.Preferencias.EmocaoDominante ?? Expressao.Neutro;

        // No automático e sem onda, a cara da reação/pouso fica até a próxima troca.
        private void VoltarACaraDeBase()
        {
            // Olhando a janela, a cara curiosa volta depois do flagra.
            if (ComOnda || _s.Preferencias.EmocaoDominante is not null || OlhandoAJanela) _s = _s with { Expressao = CaraDeBase() };
        }

        // ---------------------------------------------------------------- tela cheia

        private void MudarTelaCheia(MonitoresOcupados ocupados)
        {
            bool mudou = !ocupados.Equals(_s.Ocupados);
            _s = _s with { Ocupados = ocupados };
            // Uma vez por mudança; com o modo desligado, só o cache.
            if (!mudou || !_s.Preferencias.ModoTelaCheia || _s.Topologia is null) return;

            // Nunca interrompe um gesto; ao soltar, vale o ponto escolhido e o retorno é descartado.
            if (_s.Estado is Estado.Pressed or Estado.Dragging)
            {
                _s = _s with { TelaCheiaMudouNoGesto = true };
                return;
            }
            if (_s.Estado is Estado.Booting or Estado.Exiting) return;
            DesistirDoSaltoParaMonitorFechado();

            // Pulo em voo: se a tela cheia acabou, volta dali mesmo; se o destino ficou ocupado, sai pro livre
            // mais próximo mantendo o retorno.
            if (PuloEmVoo is { } emVoo)
            {
                if (ocupados.Vazio)
                {
                    if (_s.RetornoDaTelaCheia is not null) VoltarDaTelaCheia("FULLSCREEN_TARGETS_CHANGED(vazio): pula de volta no meio do pulo", "FULLSCREEN_TARGETS_CHANGED(vazio): restaura a posição anterior");
                }
                else if (ocupados.Contem(emVoo.ChaveDestino))
                {
                    SairDoMonitorOcupado("FULLSCREEN_TARGETS_CHANGED: o destino do pulo ficou ocupado");
                }
                return;
            }

            if (_s.Estado == Estado.Hidden)
            {
                if (_s.Motivo != MotivoDoOcultamento.PorTelaCheia)
                {
                    // Escondido por outro motivo: não reaparece, mas a posição de antes volta a valer.
                    if (ocupados.Vazio && _s.RetornoDaTelaCheia is { } retorno)
                        _s = _s with { Posicao = retorno, RetornoDaTelaCheia = null };
                    return;
                }
                if (ocupados.Vazio)
                {
                    RestaurarRetorno("FULLSCREEN_TARGETS_CHANGED(vazio): restaura a posição anterior");
                    return;
                }
                PosicaoDoPersonagem? referencia = _s.RetornoDaTelaCheia ?? _s.Posicao;
                if (referencia is null) return;
                MonitorDoDesktop? livre = MonitorLivreMaisProximo(_s.Topologia, ocupados, referencia.AncoraAbsoluta);
                if (livre is not null)
                {
                    Posicionamento destino = Posicionador.NoMonitor(livre, referencia.FracaoX, referencia.FracaoY, _cfg.Tamanho);
                    Acomodar(destino.Ancora, "FULLSCREEN_TARGETS_CHANGED: reaparece no monitor livre");
                }
                return;
            }

            if (ocupados.Vazio)
            {
                if (_s.RetornoDaTelaCheia is not null)
                    VoltarDaTelaCheia("FULLSCREEN_TARGETS_CHANGED(vazio): pula de volta à posição anterior", "FULLSCREEN_TARGETS_CHANGED(vazio): restaura a posição anterior");
                return;
            }

            if (_s.Lugar is null || _s.Posicao is null || !ocupados.Contem(_s.Lugar.Monitor.Chave)) return;
            SairDoMonitorOcupado("FULLSCREEN_TARGETS_CHANGED");
        }

        // Guarda a posição de antes (só em memória, se ainda não houver) e vai pro monitor livre mais próximo,
        // sem roubar foco. Sem monitor livre, esconde.
        private void SairDoMonitorOcupado(string regra)
        {
            if (_s.Topologia is null || _s.Lugar is null || _s.Posicao is null) return;
            PosicaoDoPersonagem anterior = _s.RetornoDaTelaCheia ?? _s.Posicao;
            MonitorDoDesktop? monitorLivre = MonitorLivreMaisProximo(_s.Topologia, _s.Ocupados, _s.Lugar.Ancora);
            _s = _s with { RetornoDaTelaCheia = anterior };
            if (monitorLivre is null)
            {
                FecharPainelSeAberto();
                LiberarItemNaMao();
                AssentarItens();
                _s = _s with { Motivo = MotivoDoOcultamento.PorTelaCheia, PassosRestantes = 0 };
                IrPara(Estado.Hidden, $"{regra}: nenhum monitor livre");
                return;
            }
            // Perto da lateral que encosta no livre, um pulinho pro outro lado; senão (ou se não couber), pula
            // até o cipó.
            MonitorDoDesktop daqui = _s.Topologia.PorChave(_s.Lugar.Monitor.Chave) ?? _s.Lugar.Monitor;
            int lado = LadoDoPulinho(daqui, _s.Lugar.Ancora.X, monitorLivre);
            if (lado != 0 && Pular(monitorLivre, ChegadaDoPulinho(daqui, monitorLivre, lado), PuloDaTelaCheia.Ida, $"{regra}: pulinho para o monitor livre", pulinho: true)) return;
            if (Pular(monitorLivre, ChegadaNoCipo(monitorLivre), PuloDaTelaCheia.Ida, $"{regra}: pula para o cipó do monitor livre")) return;
            Posicionamento noLivre = Posicionador.NoMonitor(monitorLivre, _s.Posicao.FracaoX, _s.Posicao.FracaoY, _cfg.Tamanho);
            Acomodar(noLivre.Ancora, $"{regra}: transfere para o monitor livre");
        }

        private void RestaurarRetorno(string regra)
        {
            PosicaoDoPersonagem? retorno = _s.RetornoDaTelaCheia;
            _s = _s with { RetornoDaTelaCheia = null };
            if (retorno is not null) _s = _s with { Posicao = retorno };
            Mostrar(regra);
        }

        // ---------------------------------------------------------------- pulo da tela cheia

        private Travessia? PuloEmVoo => _s.Estado == Estado.Jumping && _s.Movimento.Travessia is { Pulo: not PuloDaTelaCheia.Nenhum } pulo ? pulo : null;

        // Volta num pulo; se nenhum arco couber, volta direto.
        private void VoltarDaTelaCheia(string regraDoPulo, string regraDireta)
        {
            if (_s.RetornoDaTelaCheia is { } retorno && _s.Topologia is { } t)
            {
                (Posicionamento r, _) = Posicionador.Reacomodar(t, retorno, _cfg.Tamanho);
                // As duas pontas perto da mesma borda: a volta também é um pulinho.
                bool pulinho = _s.Lugar is { } l && t.PorChave(l.Monitor.Chave) is { } aqui && aqui.Chave != r.Monitor.Chave
                    && LadoDoPulinho(aqui, l.Ancora.X, r.Monitor) is var lado && lado != 0 && LadoDoPulinho(r.Monitor, r.Ancora.X, aqui) == -lado;
                if (pulinho && Pular(r.Monitor, r.Ancora, PuloDaTelaCheia.Volta, $"{regraDoPulo} (pulinho)", pulinho: true)) return;
                if (Pular(r.Monitor, r.Ancora, PuloDaTelaCheia.Volta, regraDoPulo)) return;
            }
            RestaurarRetorno(regraDireta);
        }

        // -1/+1 se a âncora está perto da lateral que tem porta pro vizinho (áreas úteis encostadas); senão 0.
        private int LadoDoPulinho(MonitorDoDesktop monitor, int x, MonitorDoDesktop vizinho)
        {
            Topologia t = _s.Topologia!;
            Superficies sup = Superficies.Do(t, monitor, _cfg.Tamanho.ParaPixels(monitor.Dpi));
            double alcance = _cfg.Fisica.DistanciaDoPulinho * monitor.Dpi / 96.0;
            foreach (int lado in (ReadOnlySpan<int>)[-1, 1])
            {
                double ateABorda = lado > 0 ? sup.Direita - x : x - sup.Esquerda;
                if (ateABorda <= alcance && Passagens.Portas(t, monitor, lado).Any(p => p.ChaveVizinho == vizinho.Chave)) return lado;
            }
            return 0;
        }

        // Do outro lado da borda, na mesma superfície (chão, cipó ou parede na mesma altura).
        private PontoPx ChegadaDoPulinho(MonitorDoDesktop daqui, MonitorDoDesktop livre, int lado)
        {
            Topologia t = _s.Topologia!;
            Superficies aqui = Superficies.Do(t, daqui, _cfg.Tamanho.ParaPixels(daqui.Dpi)), la = Superficies.Do(t, livre, _cfg.Tamanho.ParaPixels(livre.Dpi));
            int entrada = _s.Estado == Estado.Climbing ? 0 : (int)Math.Round(_cfg.Fisica.EntradaDoPulinho * livre.Dpi / 96.0, MidpointRounding.AwayFromZero);
            int x = lado > 0 ? la.Esquerda + entrada : la.Direita - entrada;
            int y0 = _s.Lugar!.Ancora.Y;
            int y = y0 >= aqui.Chao ? la.Chao : y0 <= aqui.Teto ? la.Teto : Math.Clamp(y0, la.Teto, la.Chao);
            return new PontoPx(Math.Clamp(x, la.Esquerda, la.Direita), y);
        }

        // Perto da lateral virada pra partida; com monitores empilhados, na mesma vertical.
        private PontoPx ChegadaNoCipo(MonitorDoDesktop livre)
        {
            Superficies sup = Superficies.Do(_s.Topologia!, livre, _cfg.Tamanho.ParaPixels(livre.Dpi));
            int entrada = (int)Math.Round(_cfg.Fisica.EntradaNoCipo * livre.Dpi / 96.0, MidpointRounding.AwayFromZero);
            int partida = _s.Lugar!.Ancora.X;
            int x = partida < livre.Tela.Esquerda ? sup.Esquerda + entrada : partida >= livre.Tela.Direita ? sup.Direita - entrada : partida;
            return new PontoPx(Math.Clamp(x, sup.Esquerda, sup.Direita), sup.Teto);
        }

        // Devolve false sem mexer em nada se o recurso está desligado ou nenhum arco cabe nas áreas úteis
        // (ex.: escondido atrás da borda); aí quem chamou faz a troca direta.
        private bool Pular(MonitorDoDesktop destino, PontoPx chegada, PuloDaTelaCheia pulo, string regra, bool pulinho = false)
        {
            if (!_cfg.PuloDaTelaCheia || !_cfg.Movimento || _s.Topologia is not { } t || _s.Lugar is not { } lugar) return false;
            MonitorDoDesktop origem = t.PorChave(lugar.Monitor.Chave) ?? lugar.Monitor;
            PontoPx partida = lugar.Ancora;
            Travessia? plano = Passagens.PlanejarPuloDaTelaCheia(t, origem, partida.X, partida.Y, destino, chegada.X, chegada.Y,
                _cfg.Tamanho, _cfg.Fisica, _cfg.PassosPorSegundo, pulo, pulinho);
            if (plano is null) return false;
            _s = _s with { Gesto = Gesto.Nenhum, PassosDoGesto = 0, Direcao = chegada.X >= partida.X ? Direcao.Direita : Direcao.Esquerda };
            if (!ComOnda && _s.Preferencias.EmocaoDominante is null) _s = _s with { Expressao = Expressao.Empolgado };
            IrPara(Estado.Jumping, regra);
            // IrPara só zera o movimento se o estado muda; um pulo que substitui outro em voo precisa disso aqui.
            _s = _s with { Movimento = new EstadoDoMovimento(partida.X, partida.Y, 0, 0, double.PositiveInfinity, -1, false) { Travessia = plano } };
            return true;
        }

        // Ida: agarra o cipó, ou pousa se for pulinho no chão. Volta: assume a posição de antes e o retorno acaba.
        private void ChegarDoPulo(Travessia pulo)
        {
            if (pulo.Pulo == PuloDaTelaCheia.Volta && _s.RetornoDaTelaCheia is { } retorno && _s.Topologia is { } t)
            {
                (Posicionamento r, PosicaoDoPersonagem p) = Posicionador.Reacomodar(t, retorno, _cfg.Tamanho);
                _s = _s with { RetornoDaTelaCheia = null };
                Acomodar(r.Ancora, "JUMPING: fim do pulo da tela cheia, de volta à posição anterior", p);
                return;
            }
            var chegada = new PontoPx((int)Math.Round(pulo.XDestino, MidpointRounding.AwayFromZero), (int)Math.Round(pulo.YDestino, MidpointRounding.AwayFromZero));
            // Pulinho que acaba no chão pousa, igual ao salto de degrau.
            if (pulo.Pulo == PuloDaTelaCheia.Ida && _s.Topologia?.PorChave(pulo.ChaveDestino) is { } destino && chegada.Y == destino.AreaUtil.Base)
            {
                MoverPara(destino, chegada.X, chegada.Y);
                _transicoes.Add(new Transicao(Estado.Jumping, Estado.Jumping, "JUMPING: fim do pulinho da tela cheia, no chão do monitor livre"));
                Sinalizar(SinalDeMovimento.ContatoComOChao);
                return;
            }
            Acomodar(chegada, pulo.Pulo == PuloDaTelaCheia.Ida
                ? "JUMPING: fim do pulo da tela cheia, agarra o cipó do monitor livre"
                : "JUMPING: fim do pulo da tela cheia");
        }

        // Esconder/sair no meio do pulo: conta como pousado, pra não gravar nem reaparecer num ponto no ar
        // em cima do monitor ocupado.
        private void DarOPuloPorTerminado()
        {
            if (PuloEmVoo is not { } pulo || _s.Topologia is not { } t) return;
            if (pulo.Pulo == PuloDaTelaCheia.Volta)
            {
                if (_s.RetornoDaTelaCheia is { } retorno) _s = _s with { Posicao = retorno, RetornoDaTelaCheia = null };
                return;
            }
            if (t.PorChave(pulo.ChaveDestino) is not { } destino) return;
            Posicionamento chegada = NoLugar(destino, new PontoPx((int)Math.Round(pulo.XDestino, MidpointRounding.AwayFromZero), (int)Math.Round(pulo.YDestino, MidpointRounding.AwayFromZero)));
            _s = _s with { Lugar = chegada, Posicao = Posicionador.Descrever(chegada) };
        }

        private static MonitorDoDesktop? MonitorLivreMaisProximo(Topologia topologia, MonitoresOcupados ocupados, PontoPx referencia)
        {
            MonitorDoDesktop? melhor = null;
            long melhorDistancia = long.MaxValue;
            foreach (MonitorDoDesktop m in topologia.Monitores)
            {
                if (ocupados.Contem(m.Chave)) continue;
                long d = m.Tela.DistanciaAoQuadrado(referencia);
                if (d < melhorDistancia)
                {
                    melhor = m;
                    melhorDistancia = d;
                }
            }
            return melhor;
        }

        // Fechado só pra autonomia: a travessia não entra e a lateral vira parede. Arrastar até lá ainda vale.
        private bool Fechado(string chave) => _s.Preferencias.ModoTelaCheia && _s.Ocupados.Contem(chave);

        // Nulo quando nenhum monitor está fechado.
        private Func<string, bool>? Fechados => _s.Preferencias.ModoTelaCheia && !_s.Ocupados.Vazio ? Fechado : null;

        // Andando até um salto pra monitor que fechou: o plano cai. Travessia já em curso termina e volta na chegada.
        private void DesistirDoSaltoParaMonitorFechado()
        {
            if (_s.Estado == Estado.Walking && _s.Movimento.Travessia is { Tipo: TipoDeTravessia.Salto } planejado && Fechado(planejado.ChaveDestino))
                _s = _s with { Movimento = _s.Movimento with { Travessia = null, Restante = 0 } };
        }

        // O destino fechou no meio da travessia: volta pra origem, encostado na porta, sem guardar retorno (nunca
        // esteve lá por escolha). Se a origem também sumiu ou fechou, sai como na tela cheia.
        private bool VoltarSeAPortaFechou(Travessia travessia, string regra)
        {
            if (!Fechado(travessia.ChaveDestino)) return false;
            if (_s.Topologia is { } t && t.PorChave(travessia.ChaveOrigem) is { } origem && !Fechado(origem.Chave))
            {
                Superficies sup = Superficies.Do(t, origem, _cfg.Tamanho.ParaPixels(origem.Dpi));
                Acomodar(new PontoPx(travessia.Lado > 0 ? sup.Direita : sup.Esquerda, sup.Chao), regra);
            }
            else
            {
                SairDoMonitorOcupado(regra);
            }
            return true;
        }

        // Só vale pras próximas decisões.
        private void EscolherTravessia(bool ligado)
        {
            if (!_s.Carregado || ligado == _s.Preferencias.AtravessarMonitores) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_CROSS_MONITORS: {(ligado ? "ligado" : "desligado")}"));
            MudarPreferencias(_s.Preferencias with { AtravessarMonitores = ligado });
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        // Desligar desfaz o efeito temporário. Ligar com ele à vista num monitor já ocupado o tira de lá, como
        // se a tela cheia tivesse acabado de começar. Durante um gesto, nada muda.
        private void EscolherModoTelaCheia(bool ligado)
        {
            if (!_s.Carregado || ligado == _s.Preferencias.ModoTelaCheia) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_FULLSCREEN_MODE: {(ligado ? "ligado" : "desligado")}"));
            MudarPreferencias(_s.Preferencias with { ModoTelaCheia = ligado });
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            if (!ligado || _s.Estado is Estado.Pressed or Estado.Dragging || !_s.Estado.Visivel()) return;
            DesistirDoSaltoParaMonitorFechado();
            if (_s.Lugar is { } lugar && _s.Ocupados.Contem(lugar.Monitor.Chave))
                SairDoMonitorOcupado("CMD_SET_FULLSCREEN_MODE: ligado com ele num monitor ocupado pela tela cheia");
        }

        // Valores vêm do arquivo e não são confiáveis: energia ou escala inválida vira o padrão, emoção que não é
        // de humor vira automática, e item fora da edição compilada sai da seleção.
        private static Preferencias Sanear(Preferencias preferencias, ConfiguracaoDoNucleo cfg)
        {
            if (!Enum.IsDefined(preferencias.Energia)) preferencias = preferencias with { Energia = Preferencias.Padrao.Energia };
            if (preferencias.EmocaoDominante is { } emocao && !Expressoes.EhDeHumor(emocao)) preferencias = preferencias with { EmocaoDominante = null };
            if (!Enum.IsDefined(preferencias.Escala)) preferencias = preferencias with { Escala = Preferencias.Padrao.Escala };
            ConjuntoDeItens itens = Preferencias.NormalizarItensAdultos(preferencias.ItensAdultosHabilitados);
            ConjuntoDeItens proprios = Preferencias.NormalizarPorContaPropria(preferencias.ItensPorContaPropria);
            foreach (Item item in TabelaDoTamagotchi.Itens)
                if (!cfg.ItensDaEdicao.Contem(item))
                {
                    itens = itens.Sem(item);
                    proprios = proprios.Sem(item);
                }
            return preferencias with { ItensAdultosHabilitados = itens, ItensPorContaPropria = proprios };
        }

        private void MudarPreferencias(Preferencias novas)
        {
            Preferencias antes = _s.Preferencias;
            novas = Sanear(novas, _cfg);
            _s = _s with { Preferencias = novas };
            // Emoção nova aparece na hora, salvo com onda. Antes da carga, quem decide a cara é a carga.
            if (_s.Carregado && novas.EmocaoDominante is { } emocao && emocao != antes.EmocaoDominante && !ComOnda && CaraLivre(_s.Estado))
                _s = _s with { Expressao = emocao };
            if (_s.Carregado)
                foreach (Item item in TabelaDoTamagotchi.Itens)
                    if (ItemAdulto(item) && antes.ItensAdultosHabilitados.Contem(item) && !novas.ItensAdultosHabilitados.Contem(item))
                        TirarOItemAdulto(item);
            // Mesmo efeito de desligar pelo menu.
            if (_s.Carregado && antes.ConteudoAdulto && !novas.ConteudoAdulto) TirarOConteudoAdulto();
            if (!antes.ModoTelaCheia || novas.ModoTelaCheia) return;

            // Desligar o modo desfaz o efeito: a posição temporária nunca vira a escolhida.
            if (_s.Estado == Estado.Hidden && _s.Motivo == MotivoDoOcultamento.PorTelaCheia)
            {
                RestaurarRetorno("SETTINGS_CHANGED: modo de tela cheia desligado");
            }
            else if (_s.Estado == Estado.Hidden)
            {
                // Escondido por outro motivo: não reaparece, mas a posição de antes volta a valer.
                if (_s.RetornoDaTelaCheia is { } retorno) _s = _s with { Posicao = retorno, RetornoDaTelaCheia = null };
            }
            else if (_s.Estado is Estado.Pressed or Estado.Dragging)
            {
                // Não interrompe o gesto; o fim dele decide.
            }
            else if (_s.Estado.Visivel() && _s.RetornoDaTelaCheia is not null)
            {
                VoltarDaTelaCheia("SETTINGS_CHANGED: modo de tela cheia desligado, pula de volta", "SETTINGS_CHANGED: modo de tela cheia desligado");
            }
            else
            {
                _s = _s with { RetornoDaTelaCheia = null };
            }
        }

        // ---------------------------------------------------------------- relógio e movimento

        private void Passar()
        {
            _s = _s with { Passos = _s.Passos + 1 };
            // Itens caem em qualquer estado do personagem.
            PassoDosItens();
            switch (_s.Estado)
            {
                case Estado.Using:
                    _s = _s with { PassosRestantes = _s.PassosRestantes - 1 };
                    if (_s.PassosRestantes <= 0) FimDoUso();
                    break;
                case Estado.Reacting:
                    _s = _s with { PassosRestantes = _s.PassosRestantes - 1 };
                    if (_s.PassosRestantes <= 0 && _s.Lugar is not null)
                    {
                        VoltarACaraDeBase();
                        Acomodar(_s.Lugar.Ancora, "REACTING: fim da reação");
                    }
                    break;
                case Estado.Landing:
                    _s = _s with { PassosRestantes = _s.PassosRestantes - 1 };
                    if (_s.PassosRestantes <= 0)
                    {
                        VoltarACaraDeBase();
                        IrPara(Estado.Idle, "LANDING: fim do pouso");
                    }
                    break;
                case Estado.Idle when _s.Gesto != Gesto.Nenhum:
                    _s = _s with { PassosDoGesto = _s.PassosDoGesto - 1 };
                    if (_s.PassosDoGesto <= 0) EncerrarGesto();
                    break;
                case Estado.Walking when _cfg.Movimento:
                    PassoAndando();
                    break;
                case Estado.Climbing when _cfg.Movimento:
                    PassoEscalando();
                    break;
                case Estado.Hanging when _cfg.Movimento:
                    PassoPendurado();
                    break;
                case Estado.Jumping or Estado.Falling when _cfg.Movimento:
                    PassoNoAr();
                    break;
            }
        }

        private void Sinalizar(SinalDeMovimento sinal)
        {
            bool calmo = _s.AutonomiaPausada || _s.PainelAberto;
            switch (_s.Estado, sinal)
            {
                case (Estado.Walking, SinalDeMovimento.Parede):
                    Escolher("WALKING: parede", calmo,
                        (Estado.Idle, 3),
                        (Estado.Climbing, Permite(AcoesAutonomas.Escalar) ? Perfil.PesoEscalar : 0),
                        (Estado.Walking, 2));
                    if (_s.Estado == Estado.Walking) Virar();
                    else if (_s.Estado == Estado.Climbing) TalvezFoguete();
                    break;
                case (Estado.Walking, SinalDeMovimento.Passagem):
                    IrPara(Estado.Walking, "WALKING: passagem (atravessa)");
                    break;
                case (Estado.Walking, SinalDeMovimento.FimDoChao):
                    IrPara(Estado.Falling, "WALKING: fim do chão");
                    break;
                case (Estado.Climbing, SinalDeMovimento.TopoDaParede):
                    Escolher("CLIMBING: topo da área útil", calmo,
                        (Estado.Idle, 2),
                        (Estado.Walking, Permite(AcoesAutonomas.Andar) ? 2 : 0),
                        (Estado.Jumping, Permite(AcoesAutonomas.Pular) ? Perfil.PesoPular : 0),
                        (Estado.Falling, 1));
                    break;
                case (Estado.Climbing, SinalDeMovimento.FimDaParede):
                    IrPara(Estado.Idle, "CLIMBING: fim da parede");
                    break;
                case (Estado.Climbing, SinalDeMovimento.BordaSuperior):
                    IrPara(Estado.Hanging, "CLIMBING: alcança borda superior apoiável");
                    break;
                case (Estado.Hanging, SinalDeMovimento.FimDaBorda):
                    // Toon force: toda lateral dá pra descer, até a passagem.
                    Escolher("HANGING: passagem compatível ou fim da borda", calmo,
                        (Estado.Climbing, 2),
                        (Estado.Hanging, 1),
                        (Estado.Falling, 1));
                    if (_s.Estado == Estado.Hanging) Virar();
                    // Desce pela parede em que chegou (a direção continua virada para ela).
                    else if (_s.Estado == Estado.Climbing) _s = _s with { Movimento = _s.Movimento with { SentidoVertical = 1 } };
                    break;
                case (Estado.Jumping or Estado.Falling, SinalDeMovimento.ContatoComOChao):
                    _s = _s with { PassosRestantes = _cfg.PassosDoPouso, Sinal = Sinal.Pousou };
                    IrPara(Estado.Landing, $"{_s.Estado.ToString().ToUpperInvariant()}: contato com o chão");
                    break;
            }
        }

        private void Decidir(long geracao)
        {
            // Disparo de um agendamento substituído ou cancelado: ignorado.
            if (!_s.DecisaoAgendada || geracao != _s.Geracao) return;
            // Nada autônomo enquanto o usuário controla, com pausa, painel aberto ou item na mão.
            if (_s.Estado.ControladoPeloUsuario() || _s.AutonomiaPausada || _s.PainelAberto || !_s.Estado.Visivel() || AtentoAoItem) return;

            _s = _s with { DecisaoAgendada = false, CliquesSeguidos = 0 };
            _reagendar = true;
            switch (_s.Estado)
            {
                case Estado.Idle:
                    // A curiosidade tem a vez; se não agir, segue a agenda normal.
                    if (!DecidirCurioso()) DecidirParado();
                    break;
                case Estado.Resting:
                    _s = _s with { Sinal = Sinal.Acordou, Expressao = CaraDeBase() };
                    IrPara(Estado.Idle, "RESTING + AUTONOMY_TIMER: acorda");
                    break;
                case Estado.Peeking:
                    // A agenda nunca o tira do esconderijo; só troca a cara.
                    Expressao espiando = SortearCaraDoEscondido();
                    _s = _s with { Expressao = espiando };
                    _transicoes.Add(new Transicao(Estado.Peeking, Estado.Peeking, "PEEKING + AUTONOMY_TIMER: espia com outra cara"));
                    break;
                case Estado.Climbing or Estado.Hanging when _cfg.Movimento && _s.PresoPeloUsuario:
                    DecidirPreso();
                    break;
                case Estado.Climbing when _cfg.Movimento && _s.Movimento.Agarrado:
                    // Agarrado sem ter sido posto ali (ex.: revalidação): volta a escalar, salta ou se solta.
                    Escolher("CLIMBING agarrado + AUTONOMY_TIMER", calmo: false,
                        (Estado.Climbing, Permite(AcoesAutonomas.Escalar) ? 4 : 0),
                        (Estado.Jumping, Permite(AcoesAutonomas.Pular) ? Perfil.PesoPular : 0),
                        (Estado.Falling, 1));
                    if (_s.Estado == Estado.Climbing)
                    {
                        (int sobe, Aleatorio a) = _s.Aleatorio.Entre(0, 1);
                        _s = _s with { Aleatorio = a, Movimento = _s.Movimento with { Agarrado = false, SentidoVertical = sobe == 0 ? -1 : 1 } };
                        if (sobe == 0) TalvezFoguete();
                    }
                    else if (_s.Estado == Estado.Jumping)
                    {
                        SaltarDaParede();
                    }
                    break;
                case Estado.Hanging when _cfg.Movimento && _s.Movimento.Agarrado:
                    bool naQuinaAgarrado = Mundo(out _, out Superficies supAgarrado, out _) && supAgarrado.NaLateral(_s.Movimento.X, out _);
                    Escolher("HANGING agarrado + AUTONOMY_TIMER", calmo: false,
                        (Estado.Hanging, 3),
                        (Estado.Climbing, naQuinaAgarrado ? 2 : 0),
                        (Estado.Jumping, Permite(AcoesAutonomas.Pular) ? Perfil.PesoPular : 0),
                        (Estado.Falling, 1));
                    if (_s.Estado == Estado.Hanging)
                        _s = _s with { Movimento = _s.Movimento with { Agarrado = false } };
                    else
                        SairDoTeto();
                    break;
                case Estado.Climbing:
                    Escolher("CLIMBING + AUTONOMY_TIMER", calmo: false,
                        (Estado.Jumping, Permite(AcoesAutonomas.Pular) ? Perfil.PesoPular : 0),
                        (Estado.Falling, 1));
                    if (_cfg.Movimento && _s.Estado == Estado.Jumping) SaltarDaParede();
                    break;
                case Estado.Hanging:
                    // No meio da borda não tem parede pra descer; só na quina.
                    bool naQuina = !_cfg.Movimento || (Mundo(out _, out Superficies sup, out _) && sup.NaLateral(_s.Movimento.X, out _));
                    Escolher("HANGING + AUTONOMY_TIMER", calmo: false,
                        (Estado.Hanging, 2),
                        (Estado.Climbing, naQuina ? 2 : 0),
                        (Estado.Jumping, Permite(AcoesAutonomas.Pular) ? Perfil.PesoPular : 0),
                        (Estado.Falling, 1));
                    if (_cfg.Movimento) SairDoTeto();
                    break;
            }
        }

        private void DecidirParado()
        {
            PerfilDeEnergia perfil = Perfil;
            var opcoes = new List<(AcoesAutonomas Acao, int Peso)>();
            void Opcao(AcoesAutonomas acao, int peso)
            {
                if (Permite(acao) && peso > 0) opcoes.Add((acao, peso));
            }
            // Escalar precisa saber onde estão as laterais. Com toon force as duas servem, mesmo a que
            // encosta em outro monitor.
            bool temLateral = !_cfg.Movimento || Mundo(out _, out _, out _);
            Opcao(AcoesAutonomas.Andar, perfil.PesoAndar);
            Opcao(AcoesAutonomas.Escalar, temLateral ? perfil.PesoEscalar : 0);
            Opcao(AcoesAutonomas.Pular, perfil.PesoPular);
            Opcao(AcoesAutonomas.Descansar, perfil.PesoDescansar);
            Opcao(AcoesAutonomas.Gesto, perfil.PesoGesto);
            Opcao(AcoesAutonomas.TrocarExpressao, perfil.PesoTrocarExpressao);
            // Uso por conta própria tem um peso só, não importa quantas drogas estejam marcadas.
            List<Item> proprias = CandidatasPorContaPropria();
            Opcao(AcoesAutonomas.UsarPorContaPropria, proprias.Count > 0 ? perfil.PesoUsarPorContaPropria : 0);
            // Ir ao outro monitor, só com uma porta numa lateral do monitor dele.
            (bool portaEsquerda, bool portaDireita) = PortasDeTravessia();
            Opcao(AcoesAutonomas.IrAoOutroMonitor, portaEsquerda || portaDireita ? perfil.PesoIrAoOutroMonitor : 0);
            if (opcoes.Count == 0) return;

            (int indice, Aleatorio a) = _s.Aleatorio.Ponderado([.. opcoes.Select(o => o.Peso)]);
            _s = _s with { Aleatorio = a };
            switch (opcoes[indice].Acao)
            {
                case AcoesAutonomas.Andar:
                    (int lado, Aleatorio a2) = _s.Aleatorio.Entre(0, 1);
                    _s = _s with { Aleatorio = a2, Direcao = lado == 0 ? Direcao.Direita : Direcao.Esquerda };
                    if (_cfg.Personalidade && _cfg.Movimento && SorteiaExplorarBorda(perfil))
                    {
                        PlanejarExploracaoDaBorda();
                        break;
                    }
                    IrPara(Estado.Walking, "IDLE + AUTONOMY_TIMER: andar");
                    if (_cfg.Movimento) PlanejarCaminhada(perfil);
                    break;
                case AcoesAutonomas.Escalar when _cfg.Movimento:
                    PlanejarEscalada();
                    break;
                case AcoesAutonomas.Escalar:
                    IrPara(Estado.Climbing, "IDLE + AUTONOMY_TIMER: escalar");
                    break;
                case AcoesAutonomas.Pular:
                    IrPara(Estado.Jumping, "IDLE + AUTONOMY_TIMER: pular");
                    if (_cfg.Movimento) PlanejarPulo(perfil);
                    break;
                case AcoesAutonomas.Descansar:
                    _s = _s with { Expressao = Expressao.Sonolento };
                    IrPara(Estado.Resting, "IDLE + AUTONOMY_TIMER: descansar");
                    break;
                case AcoesAutonomas.Gesto:
                    // Sempre dois sorteios (gesto e duração), com ou sem onda.
                    (Gesto gesto, Aleatorio a3) = SortearGesto();
                    (int passos, Aleatorio a4) = a3.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
                    _s = _s with { Aleatorio = a4, Gesto = gesto, PassosDoGesto = passos };
                    _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE + AUTONOMY_TIMER: gesto {gesto}"));
                    break;
                case AcoesAutonomas.TrocarExpressao:
                    // O sorteio mexe em _s, então a cara vai pra uma variável antes do "with".
                    Expressao nova = SortearTrocaDeCara();
                    _s = _s with { Expressao = nova };
                    break;
                case AcoesAutonomas.UsarPorContaPropria:
                    UsarPorContaPropria(proprias);
                    break;
                case AcoesAutonomas.IrAoOutroMonitor:
                    PlanejarIdaAoOutroMonitor(perfil, portaEsquerda, portaDireita);
                    break;
            }
        }

        // Laterais por onde dá pra atravessar agora (andando, salto ou transbordo).
        private (bool Esquerda, bool Direita) PortasDeTravessia()
        {
            if (!_cfg.Movimento || !Mundo(out MonitorDoDesktop m, out Superficies sup, out _)) return (false, false);
            double x = _s.Movimento.X;
            return (PlanoDeTravessia(m, -1, (int)Math.Max(0, x - sup.Esquerda)) is not null || TemTransbordo(m, -1),
                PlanoDeTravessia(m, +1, (int)Math.Max(0, sup.Direita - x)) is not null || TemTransbordo(m, +1));
        }

        // Com duas portas, sorteia o lado. Do outro lado, segue andando uma distância do perfil.
        private void PlanejarIdaAoOutroMonitor(PerfilDeEnergia perfil, bool portaEsquerda, bool portaDireita)
        {
            int lado = portaEsquerda && portaDireita ? 0 : portaEsquerda ? -1 : 1;
            if (lado == 0)
            {
                (int sorteio, Aleatorio a) = _s.Aleatorio.Entre(0, 1);
                _s = _s with { Aleatorio = a };
                lado = sorteio == 0 ? -1 : 1;
            }
            (int dip, Aleatorio a2) = _s.Aleatorio.Entre(perfil.DistanciaAndandoMinima, perfil.DistanciaAndandoMaxima);
            _s = _s with { Aleatorio = a2 };
            IrAteAPorta(lado, dip, "IDLE + AUTONOMY_TIMER: ir ao outro monitor (anda até a porta)");
        }

        // Atravessa sem sortear a porta (plana, salto de degrau ou transbordo) e anda mais dip DIP do outro lado.
        // Usado pela agenda e pela curiosidade.
        private void IrAteAPorta(int lado, int dip, string regra)
        {
            _s = _s with { Direcao = lado > 0 ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, regra);
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            double x = _s.Movimento.X;
            double ateABorda = lado > 0 ? sup.Direita - x : x - sup.Esquerda;
            // Salto de degrau: anda até a partida planejada.
            Travessia? plano = PlanoDeTravessia(m, lado, (int)Math.Max(0, ateABorda));
            if (plano is { Tipo: TipoDeTravessia.Salto })
            {
                _s = _s with { Movimento = _s.Movimento with { Travessia = plano, Restante = double.PositiveInfinity } };
                return;
            }
            // Sem porta plana nem salto: anda até a lateral, escala e transborda.
            if (plano is null)
            {
                _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true, QuerEscalar = true } };
                return;
            }
            _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true, Restante = ateABorda + dip * escala } };
        }

        // Exatamente um sorteio. Prioridade: cara da onda, emoção dominante, ou uma das outras 13 caras de humor
        // (ponderada pela energia, se o perfil tiver pesos; senão uniforme).
        private Expressao SortearTrocaDeCara()
        {
            if (FaseEmVigor is { } fase) return SortearCaraDaFase(fase);
            if (_s.Preferencias.EmocaoDominante is { } dominante) return SortearComADominante(dominante);
            IReadOnlyList<Expressao> humor = Expressoes.DeHumor;
            if (Perfil.PesosDasCaras is { } tendencia)
            {
                // Peso 0 pra cara atual: a troca sempre muda.
                int[] pesos = [.. humor.Select((cara, i) => cara == _s.Expressao ? 0 : tendencia[i])];
                (int escolhida, Aleatorio ap) = _s.Aleatorio.Ponderado(pesos);
                _s = _s with { Aleatorio = ap };
                return humor[escolhida];
            }
            // As 14 estão na ordem do enum: índice na lista = valor do enum.
            int atual = Expressoes.EhDeHumor(_s.Expressao) ? (int)_s.Expressao : -1;
            (int e, Aleatorio a) = _s.Aleatorio.Entre(0, humor.Count - (atual < 0 ? 1 : 2));
            _s = _s with { Aleatorio = a };
            // Sorteia entre as outras caras: pula a atual.
            return humor[atual >= 0 && e >= atual ? e + 1 : e];
        }

        // Dominante 6, cada uma das 4 companheiras 1 (60% a dominante).
        private static readonly int[] PesosDaDominante = [6, 1, 1, 1, 1];

        // Pode repetir a cara atual de propósito (~36 em 100 trocas): é assim que a dominante fica a mais frequente.
        private Expressao SortearComADominante(Expressao dominante)
        {
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado(PesosDaDominante);
            _s = _s with { Aleatorio = a };
            return i == 0 ? dominante : Expressoes.Companheiras(dominante)[i - 1];
        }

        private bool Permite(AcoesAutonomas acao) => (_cfg.Acoes & acao) == acao;

        // Sorteio ponderado. Calmo (pausa ou painel aberto) fica sempre com o primeiro, o mais tranquilo.
        private void Escolher(string regra, bool calmo, params (Estado Destino, int Peso)[] opcoes)
        {
            (Estado Destino, int Peso)[] validas = [.. opcoes.Where(o => o.Peso > 0)];
            if (validas.Length == 0) return;
            Estado destino;
            if (calmo || validas.Length == 1)
            {
                destino = validas[0].Destino;
            }
            else
            {
                (int i, Aleatorio a) = _s.Aleatorio.Ponderado([.. validas.Select(o => o.Peso)]);
                _s = _s with { Aleatorio = a };
                destino = validas[i].Destino;
            }
            IrPara(destino, regra);
        }

        private void Virar() => _s = _s with { Direcao = _s.Direcao == Direcao.Direita ? Direcao.Esquerda : Direcao.Direita };

        private void EncerrarGesto()
        {
            // Também encerra a espiada na borda explorada.
            _s = _s with { Gesto = Gesto.Nenhum, PassosDoGesto = 0, Movimento = _s.Movimento with { ExplorandoBorda = false } };
            _reagendar = true;
        }

        // ---------------------------------------------------------------- movimento

        // Calmo: o movimento em curso termina num lugar estável.
        private bool Calmo => _s.AutonomiaPausada || _s.PainelAberto;

        // Calmo, a agenda não decide; quem está agarrado sem ter sido posto lá ficaria esperando pra sempre.
        // Solta o agarre e os próximos passos o fazem descer/se soltar. Preso ou com item na mão, fica.
        private void SoltarOAgarreComCalma()
        {
            if (!_cfg.Movimento || !Calmo || AtentoAoItem || _s.PresoPeloUsuario) return;
            if (_s.Estado is Estado.Climbing or Estado.Hanging && _s.Movimento.Agarrado)
                _s = _s with { Movimento = _s.Movimento with { Agarrado = false } };
        }

        private int Sentido => _s.Direcao == Direcao.Direita ? 1 : -1;

        // False sem lugar ou topologia.
        private bool Mundo(out MonitorDoDesktop monitor, out Superficies superficies, out double escala)
        {
            monitor = null!;
            superficies = default;
            escala = 1;
            if (_s.Lugar is not { } lugar || _s.Topologia is not { } topologia) return false;
            monitor = topologia.PorChave(lugar.Monitor.Chave) ?? lugar.Monitor;
            superficies = Superficies.Do(topologia, monitor, _cfg.Tamanho.ParaPixels(monitor.Dpi));
            escala = monitor.Dpi / 96.0;
            return true;
        }

        // DIP/s -> px físicos por passo.
        private double PorPasso(double dipPorSegundo, double escala) => dipPorSegundo * escala / _cfg.PassosPorSegundo;

        // A janela usa o arredondado. A posição relativa acompanha pra sobreviver a troca de topologia no meio.
        private void MoverPara(MonitorDoDesktop monitor, double x, double y)
        {
            var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
            TamanhoPx tamanho = _cfg.Tamanho.ParaPixels(monitor.Dpi);
            var lugar = new Posicionamento(monitor, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
            _s = _s with { Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Movimento = _s.Movimento with { X = x, Y = y } };
        }

        private void PassoAndando()
        {
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            // Travessia é atômica: nem a calma a para no meio.
            if (_s.Movimento.Travessia is { Tipo: TipoDeTravessia.Andando } travessia)
            {
                PassoAtravessando(travessia);
                return;
            }
            if (Calmo)
            {
                IrPara(Estado.Idle, "WALKING: autonomia pausada ou painel aberto (para)");
                return;
            }
            EstadoDoMovimento mv = _s.Movimento;
            // Cambaleio: o passo oscila e às vezes vai pra trás; o recuo devolve distância e nunca passa da lateral.
            double passo = PorPasso(Fisica.VelocidadeAndando, escala) * Cambaleio(out bool cambaleia);
            double x = mv.X + Sentido * passo;
            int limite = Sentido > 0 ? sup.Direita : sup.Esquerda;
            bool naBorda = Sentido > 0 ? x >= limite : x <= limite;
            if (naBorda) x = limite;
            else if (cambaleia) x = Math.Clamp(x, sup.Esquerda, sup.Direita);
            _s = _s with { Movimento = mv with { Restante = mv.Restante - passo } };
            // Chegou na partida do salto de degrau planejado: salta.
            if (mv.Travessia is { Tipo: TipoDeTravessia.Salto } pendente && (Sentido > 0 ? x >= pendente.X0 : x <= pendente.X0))
            {
                MoverPara(m, pendente.X0, sup.Chao);
                Saltar(pendente);
                return;
            }
            MoverPara(m, x, sup.Chao);

            if (mv.Aproximando && (naBorda || _s.Movimento.Restante <= 0))
            {
                ChegarPerto("WALKING: chegou perto da janela ativa, olha curioso");
                return;
            }
            if (naBorda)
            {
                if (mv.ExplorandoBorda)
                {
                    ChegarNaBordaExplorada();
                    return;
                }
                // Toon force: a lateral é parede mesmo quando outro monitor encosta nela.
                if (mv.QuerEscalar)
                {
                    IrPara(Estado.Climbing, "WALKING: parede (andava até ela para escalar)");
                    // Indo pro outro monitor pelo transbordo: a intenção sobe junto.
                    if (mv.QuerAtravessar) _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true } };
                    TalvezFoguete();
                    return;
                }
                // Na porta, sorteia entre atravessar e o caminho normal da parede.
                if (PlanoDeTravessia(m, Sentido) is { } plano && (mv.QuerAtravessar || SorteiaAtravessar()))
                {
                    if (plano.Tipo == TipoDeTravessia.Salto)
                    {
                        Saltar(plano);
                        return;
                    }
                    _s = _s with { Movimento = _s.Movimento with { Travessia = plano, QuerAtravessar = false } };
                    _transicoes.Add(new Transicao(Estado.Walking, Estado.Walking, "WALKING: passagem (atravessa)"));
                    return;
                }
                Sinalizar(SinalDeMovimento.Parede);
                return;
            }
            if (!mv.QuerEscalar && _s.Movimento.Restante <= 0) IrPara(Estado.Idle, "WALKING: fim do percurso");
        }

        // Porta plana, ou salto de degrau com a partida até recuoMaximo px antes da lateral. Nunca pra
        // monitor fechado.
        private Travessia? PlanoDeTravessia(MonitorDoDesktop m, int lado, int recuoMaximo = 0)
        {
            if (!_cfg.Travessia || !_s.Preferencias.AtravessarMonitores || Calmo || AtentoAoItem || _s.Topologia is not { } t) return null;
            Func<string, bool>? fechados = Fechados;
            if (Passagens.PortaPlana(t, m, lado, _cfg.Tamanho, fechados) is { } porta)
                return new Travessia(TipoDeTravessia.Andando, m.Chave, porta.ChaveVizinho, lado, porta.Borda);
            return Passagens.SaltoDeDegrau(t, m, lado, _cfg.Tamanho, _cfg.Fisica, _cfg.PassosPorSegundo, recuoMaximo, fechados);
        }

        // Se, subindo de yAntes a yDepois, os pés cruzaram o chão de um vizinho mais alto dessa lateral.
        private Travessia? TransbordoAoPassar(MonitorDoDesktop m, double yAntes, double yDepois)
        {
            if (!_cfg.Travessia || !_s.Preferencias.AtravessarMonitores || Calmo || AtentoAoItem || _s.Topologia is not { } t) return null;
            Func<string, bool>? fechados = Fechados;
            foreach (Porta porta in Passagens.Portas(t, m, Sentido, fechados))
            {
                if (t.PorChave(porta.ChaveVizinho) is not { } vizinho) continue;
                int chao = vizinho.AreaUtil.Base;
                if (chao < yDepois || chao >= yAntes) continue;
                if (Passagens.Transbordo(t, m, Sentido, chao, _cfg.Tamanho, _cfg.Fisica, _cfg.PassosPorSegundo, fechados) is { } transbordo) return transbordo;
            }
            return null;
        }

        // Algum vizinho mais alto nessa lateral cujo chão ele alcança escalando.
        private bool TemTransbordo(MonitorDoDesktop m, int lado)
        {
            if (!_cfg.Travessia || !_s.Preferencias.AtravessarMonitores || Calmo || AtentoAoItem || _s.Topologia is not { } t) return false;
            Func<string, bool>? fechados = Fechados;
            foreach (Porta porta in Passagens.Portas(t, m, lado, fechados))
            {
                if (t.PorChave(porta.ChaveVizinho) is { } vizinho
                    && Passagens.Transbordo(t, m, lado, vizinho.AreaUtil.Base, _cfg.Tamanho, _cfg.Fisica, _cfg.PassosPorSegundo, fechados) is not null) return true;
            }
            return false;
        }

        // O plano entra depois de IrPara, que zera o movimento ao entrar no estado.
        private void Saltar(Travessia salto, string regra = "WALKING: degrau alcançável (salto de travessia)")
        {
            IrPara(Estado.Jumping, regra);
            _s = _s with { Movimento = _s.Movimento with { Travessia = salto with { Passo = 0 } } };
        }

        // Posição analítica do arco (não integrada). O monitor troca na borda. No último passo pousa exato no chão
        // do vizinho; no pulo da tela cheia, a chegada é uma acomodação.
        private void PassoNoSalto(Travessia salto)
        {
            if (_s.Topologia is not { } t || t.PorChave(salto.ChaveOrigem) is not { } origem || t.PorChave(salto.ChaveDestino) is not { } destino)
            {
                _s = _s with { Movimento = _s.Movimento with { Travessia = null } };
                return;
            }
            int passo = salto.Passo + 1;
            if (passo >= salto.PassosTotais && salto.Pulo != PuloDaTelaCheia.Nenhum)
            {
                ChegarDoPulo(salto);
                return;
            }
            if (passo >= salto.PassosTotais)
            {
                MoverPara(destino, salto.XDestino, salto.YDestino);
                _s = _s with { Movimento = _s.Movimento with { VX = 0, VY = 0, Travessia = null } };
                _transicoes.Add(new Transicao(Estado.Jumping, Estado.Jumping, "JUMPING: salto de travessia completo"));
                if (VoltarSeAPortaFechou(salto, "JUMPING: o destino do salto ficou ocupado pela tela cheia (volta pela porta)")) return;
                Sinalizar(SinalDeMovimento.ContatoComOChao);
                return;
            }
            (double x, double y) = Passagens.PosicaoNoSalto(salto, passo, _cfg.PassosPorSegundo);
            var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
            MoverPara(Passagens.MonitorNoSalto(t, salto, origem, destino, ancora), x, y);
            _s = _s with { Movimento = _s.Movimento with { Travessia = salto with { Passo = passo } } };
            // No pulo da tela cheia a velocidade vai pro estado, pra pose esticar quando ele vai rápido.
            if (salto.Pulo != PuloDaTelaCheia.Nenhum)
                _s = _s with { Movimento = _s.Movimento with { VX = salto.VX, VY = salto.VY0 + salto.G * passo / _cfg.PassosPorSegundo } };
        }

        // Atravessar contra a soma dos pesos da parede (parar 3, escalar, virar 2). Se perder, Sinalizar sorteia
        // de novo entre os três: dá a mesma distribuição de um sorteio só sem mexer no código da parede.
        private bool SorteiaAtravessar()
        {
            int atravessar = Perfil.PesoAtravessar;
            if (atravessar <= 0) return false;
            int parede = 3 + (Permite(AcoesAutonomas.Escalar) ? Perfil.PesoEscalar : 0) + 2;
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado([atravessar, parede]);
            _s = _s with { Aleatorio = a };
            return i == 0;
        }

        // Anda reto, sem cambaleio, na velocidade do monitor da âncora; troca de monitor quando a âncora
        // arredondada passa da borda. Termina com o sprite inteiro no destino.
        private void PassoAtravessando(Travessia travessia)
        {
            if (_s.Topologia is not { } t || t.PorChave(travessia.ChaveOrigem) is not { } origem || t.PorChave(travessia.ChaveDestino) is not { } destino)
            {
                _s = _s with { Movimento = _s.Movimento with { Travessia = null } };
                return;
            }
            EstadoDoMovimento mv = _s.Movimento;
            MonitorDoDesktop atual = _s.Lugar is { } lugar && Passagens.PassouDaBorda(travessia, lugar.Ancora) ? destino : origem;
            double passo = PorPasso(Fisica.VelocidadeAndando, atual.Dpi / 96.0);
            double x = mv.X + travessia.Lado * passo;
            var ancora = new PontoPx((int)Math.Round(x, MidpointRounding.AwayFromZero), atual.AreaUtil.Base);
            MonitorDoDesktop daAncora = Passagens.PassouDaBorda(travessia, ancora) ? destino : origem;
            _s = _s with { Movimento = mv with { Restante = mv.Restante - passo } };
            MoverPara(daAncora, x, daAncora.AreaUtil.Base);

            Superficies noDestino = Superficies.Do(t, destino, _cfg.Tamanho.ParaPixels(destino.Dpi));
            int ax = _s.Lugar!.Ancora.X;
            bool completa = travessia.Lado > 0 ? ax >= noDestino.Esquerda : ax <= noDestino.Direita;
            if (!completa) return;
            _s = _s with { Movimento = _s.Movimento with { Travessia = null } };
            _transicoes.Add(new Transicao(Estado.Walking, Estado.Walking, "WALKING: travessia completa"));
            if (VoltarSeAPortaFechou(travessia, "WALKING: o destino da travessia ficou ocupado pela tela cheia (volta pela porta)")) return;
            if (Calmo || AtentoAoItem || _s.Movimento.Restante <= 0) IrPara(Estado.Idle, "WALKING: fim do percurso depois da travessia");
        }

        private void PassoEscalando()
        {
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            EstadoDoMovimento mv = _s.Movimento;
            if (mv.Agarrado) return;
            if (_s.PresoPeloUsuario)
            {
                PassoPresoNaParede(m, sup, escala);
                return;
            }
            // Calmo: desce até o chão em vez de subir, e o foguete apaga.
            int sentido = Calmo ? 1 : mv.SentidoVertical;
            bool foguete = mv.Foguete && sentido < 0;
            double velocidade = foguete ? _cfg.Fisica.VelocidadeDoFoguete : Fisica.VelocidadeEscalando;
            double y = mv.Y + sentido * PorPasso(velocidade, escala);
            double x = Sentido > 0 ? sup.Direita : sup.Esquerda;
            _s = _s with { Movimento = mv with { SentidoVertical = sentido, Foguete = foguete } };
            // Os pés cruzaram o chão de um vizinho mais alto: transborda pra ele (sem sorteio se já ia atravessar).
            if (sentido < 0 && TransbordoAoPassar(m, mv.Y, y) is { } transbordo && (mv.QuerAtravessar || SorteiaAtravessar()))
            {
                MoverPara(m, x, transbordo.Y0);
                Saltar(transbordo, "CLIMBING: transbordo para o chão do vizinho");
                return;
            }
            if (sentido < 0 && y <= sup.Teto)
            {
                MoverPara(m, x, sup.Teto);
                Sinalizar(SinalDeMovimento.BordaSuperior);
                // Pendurado, segue pela borda pra dentro, de costas pra parede.
                if (_s.Estado == Estado.Hanging) Virar();
                return;
            }
            if (sentido > 0 && y >= sup.Chao)
            {
                MoverPara(m, x, sup.Chao);
                Sinalizar(SinalDeMovimento.FimDaParede);
                return;
            }
            MoverPara(m, x, y);
        }

        // Passeia pela mesma lateral sem chegar ao chão nem passar pro cipó; para agarrado no fim, no limite ou calmo.
        private void PassoPresoNaParede(MonitorDoDesktop m, Superficies sup, double escala)
        {
            EstadoDoMovimento mv = _s.Movimento;
            double passo = PorPasso(Fisica.VelocidadeEscalando, escala);
            double baixo = Math.Max(sup.Teto, sup.Chao - _cfg.Fisica.AlturaMinimaParaAgarrar * escala);
            double y = mv.Y + mv.SentidoVertical * passo;
            bool noLimite = y <= sup.Teto || y >= baixo;
            y = Math.Clamp(y, sup.Teto, baixo);
            double restante = mv.Restante - passo;
            _s = _s with { Movimento = mv with { Restante = restante } };
            MoverPara(m, Sentido > 0 ? sup.Direita : sup.Esquerda, y);
            if (Calmo || noLimite || restante <= 0) _s = _s with { Movimento = _s.Movimento with { Agarrado = true } };
        }

        // Na quina dá meia-volta em vez de descer; para agarrado no fim ou calmo.
        private void PassoPresoNoCipo(MonitorDoDesktop m, Superficies sup, double escala)
        {
            EstadoDoMovimento mv = _s.Movimento;
            double passo = PorPasso(Fisica.VelocidadePendurado, escala);
            double x = mv.X + Sentido * passo;
            int limite = Sentido > 0 ? sup.Direita : sup.Esquerda;
            if (Sentido > 0 ? x >= limite : x <= limite)
            {
                x = limite;
                Virar();
            }
            double restante = mv.Restante - passo;
            _s = _s with { Movimento = mv with { Restante = restante } };
            MoverPara(m, x, sup.Teto);
            if (Calmo || restante <= 0) _s = _s with { Movimento = _s.Movimento with { Agarrado = true } };
        }

        private void PassoPendurado()
        {
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            if (_s.Movimento.Agarrado) return;
            if (_s.PresoPeloUsuario)
            {
                PassoPresoNoCipo(m, sup, escala);
                return;
            }
            if (Calmo)
            {
                IrPara(Estado.Falling, "HANGING: autonomia pausada ou painel aberto (solta-se)");
                return;
            }
            EstadoDoMovimento mv = _s.Movimento;
            double x = mv.X + Sentido * PorPasso(Fisica.VelocidadePendurado, escala);
            int limite = Sentido > 0 ? sup.Direita : sup.Esquerda;
            bool naBorda = Sentido > 0 ? x >= limite : x <= limite;
            if (naBorda) x = limite;
            MoverPara(m, x, sup.Teto);
            if (naBorda) Sinalizar(SinalDeMovimento.FimDaBorda);
        }

        // Euler semi-implícito, com velocidade máxima de queda.
        private void PassoNoAr()
        {
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            if (_s.Movimento.Travessia is { Tipo: TipoDeTravessia.Salto } salto)
            {
                PassoNoSalto(salto);
                return;
            }
            EstadoDoMovimento mv = _s.Movimento;
            double dt = 1.0 / _cfg.PassosPorSegundo;
            double vy = Math.Min(mv.VY + _cfg.Fisica.Gravidade * escala * dt, _cfg.Fisica.VelocidadeMaximaDeQueda * escala);
            double vx = mv.VX;
            double x = mv.X + vx * dt;
            double y = mv.Y + vy * dt;
            // Nunca sai da área útil: laterais e topo param o voo.
            if (x < sup.Esquerda) { x = sup.Esquerda; vx = 0; }
            else if (x > sup.Direita) { x = sup.Direita; vx = 0; }
            if (y < sup.Teto) { y = sup.Teto; if (vy < 0) vy = 0; }
            if (y >= sup.Chao)
            {
                if (Quicar(m, x, sup.Chao, vx, vy, escala)) return;
                _s = _s with { Movimento = mv with { VX = 0, VY = 0, Quiques = 0 } };
                MoverPara(m, x, sup.Chao);
                Sinalizar(SinalDeMovimento.ContatoComOChao);
                return;
            }
            _s = _s with { Movimento = mv with { VX = vx, VY = vy } };
            MoverPara(m, x, y);
        }

        // Toon force: impacto forte quica como borracha, rindo. Pousa depois do máximo de quiques ou se calmo.
        private bool Quicar(MonitorDoDesktop m, double x, int chao, double vx, double vy, double escala)
        {
            ParametrosDeMovimento f = _cfg.Fisica;
            int quiques = _s.Movimento.Quiques;
            if (Calmo || f.RestituicaoDoQuique <= 0 || quiques >= f.QuiquesMaximos || vy < f.ImpactoMinimoDoQuique * escala) return false;

            string de = _s.Estado.ToString().ToUpperInvariant();
            _s = _s with { Expressao = Expressao.Rindo };
            IrPara(Estado.Jumping, $"{de}: contato com o chão, quique de borracha (toon force)");
            // IrPara zera o movimento quando o estado muda, então a velocidade vem depois.
            _s = _s with { Movimento = _s.Movimento with { VX = vx * f.AtritoDoQuique, VY = -vy * f.RestituicaoDoQuique, Quiques = quiques + 1 } };
            MoverPara(m, x, chao);
            return true;
        }

        // Toon force: às vezes sobe a parede num foguete de borracha até o topo. Chance vem do perfil; a
        // velocidade é a mesma em todo nível.
        private void TalvezFoguete()
        {
            if (!_cfg.Movimento || _s.Estado != Estado.Climbing || Calmo || _cfg.Fisica.VelocidadeDoFoguete <= 0) return;
            (int sorteio, Aleatorio a) = _s.Aleatorio.Entre(1, 100);
            _s = _s with { Aleatorio = a };
            if (sorteio <= Perfil.ChanceDoFoguete)
                _s = _s with { Movimento = _s.Movimento with { Foguete = true } };
        }

        // Uma caminhada em cada ExplorarBordaUmEm, no gerador da personalidade.
        private bool SorteiaExplorarBorda(PerfilDeEnergia perfil)
        {
            (int n, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(1, Math.Max(1, perfil.ExplorarBordaUmEm));
            _s = _s with { AleatorioDaPersonalidade = a };
            return n == 1;
        }

        // Anda até a lateral mais próxima, sem atravessar nem escalar, e espia pra fora.
        private void PlanejarExploracaoDaBorda()
        {
            if (!Mundo(out _, out Superficies sup, out _) || _s.Lugar is null) return;
            double x = _s.Lugar.Ancora.X;
            double ateDireita = sup.Direita - x, ateEsquerda = x - sup.Esquerda;
            _s = _s with { Direcao = ateDireita <= ateEsquerda ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, "IDLE + AUTONOMY_TIMER: explorar a borda");
            _s = _s with { Movimento = _s.Movimento with { ExplorandoBorda = true, Restante = Math.Min(ateDireita, ateEsquerda) + 1 } };
        }

        private void ChegarNaBordaExplorada()
        {
            PerfilDeEnergia perfil = Perfil;
            (int passos, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
            IrPara(Estado.Idle, "WALKING: borda explorada (espia para fora)");
            _s = _s with { AleatorioDaPersonalidade = a, Gesto = Gesto.Espiar, PassosDoGesto = passos };
        }

        // Sem espaço à frente, vira.
        private void PlanejarCaminhada(PerfilDeEnergia perfil)
        {
            if (!Mundo(out _, out Superficies sup, out double escala)) return;
            (int dip, Aleatorio a) = _s.Aleatorio.Entre(perfil.DistanciaAndandoMinima, perfil.DistanciaAndandoMaxima);
            _s = _s with { Aleatorio = a };
            double x = _s.Movimento.X;
            double livre = Sentido > 0 ? sup.Direita - x : x - sup.Esquerda;
            double atras = Sentido > 0 ? x - sup.Esquerda : sup.Direita - x;
            // Porta à frente: o espaço continua do outro lado, não vira.
            if (Mundo(out MonitorDoDesktop m, out _, out _) && PlanoDeTravessia(m, Sentido) is not null) livre = double.PositiveInfinity;
            if (livre < _cfg.Fisica.EspacoMinimo * escala && atras > livre) Virar();
            _s = _s with { Movimento = _s.Movimento with { Restante = dip * escala } };
        }

        // Encostado numa lateral, sobe; senão anda até a mais próxima (toon force: as duas servem).
        private void PlanejarEscalada()
        {
            if (!Mundo(out _, out Superficies sup, out _) || _s.Lugar is null) return;
            double x = _s.Lugar.Ancora.X;
            if (sup.NaLateral(x, out int lado))
            {
                _s = _s with { Direcao = lado > 0 ? Direcao.Direita : Direcao.Esquerda };
                IrPara(Estado.Climbing, "IDLE + AUTONOMY_TIMER: escalar");
                TalvezFoguete();
                return;
            }
            double ateDireita = sup.Direita - x;
            double ateEsquerda = x - sup.Esquerda;
            _s = _s with { Direcao = ateDireita <= ateEsquerda ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, "IDLE + AUTONOMY_TIMER: escalar (anda até a parede)");
            _s = _s with { Movimento = _s.Movimento with { QuerEscalar = true } };
        }

        // Arco balístico calculado pra pousar no chão; sem espaço à frente, pula pro outro lado.
        private void PlanejarPulo(PerfilDeEnergia perfil)
        {
            if (!Mundo(out _, out Superficies sup, out double escala)) return;
            (int lado, Aleatorio a1) = _s.Aleatorio.Entre(0, 1);
            (int distanciaDip, Aleatorio a2) = a1.Entre(perfil.DistanciaDoPuloMinima, perfil.DistanciaDoPuloMaxima);
            (int alturaDip, Aleatorio a3) = a2.Entre(perfil.AlturaDoPuloMinima, perfil.AlturaDoPuloMaxima);
            _s = _s with { Aleatorio = a3, Direcao = lado == 0 ? Direcao.Direita : Direcao.Esquerda };
            double x = _s.Movimento.X;
            double livre = Sentido > 0 ? sup.Direita - x : x - sup.Esquerda;
            double atras = Sentido > 0 ? x - sup.Esquerda : sup.Direita - x;
            if (livre < distanciaDip * escala && atras > livre)
            {
                Virar();
                livre = atras;
            }
            double distancia = Math.Min(distanciaDip * escala, livre);
            double g = _cfg.Fisica.Gravidade * escala;
            double vy0 = -Math.Sqrt(2 * g * alturaDip * escala);
            double voo = 2 * -vy0 / g;
            double vx = voo > 0 ? Sentido * distancia / voo : 0;
            _s = _s with { Movimento = _s.Movimento with { VX = vx, VY = vy0 } };
        }

        private static readonly Expressao[] ExpressoesDoPreso = [Expressao.Feliz, Expressao.Curioso, Expressao.Travesso, Expressao.Rindo, Expressao.Pensativo];

        // Prioridade: cara da onda, emoção dominante, ou uma das caras do preso.
        private Expressao SortearCaraDoPreso()
        {
            if (FaseEmVigor is { } fase) return SortearCaraDaFase(fase);
            if (_s.Preferencias.EmocaoDominante is { } dominante) return SortearComADominante(dominante);
            (int cara, Aleatorio a) = _s.Aleatorio.Entre(0, ExpressoesDoPreso.Length - 1);
            _s = _s with { Aleatorio = a };
            return ExpressoesDoPreso[cara];
        }

        // A agenda nunca tira o preso de lá: ou fica trocando de cara, ou passeia um pouco pela mesma superfície
        // e para agarrado (aí o relógio desliga).
        private void DecidirPreso()
        {
            bool naParede = _s.Estado == Estado.Climbing;
            (int escolha, Aleatorio a) = _s.Aleatorio.Ponderado([2, 2, 2]);
            (int dip, Aleatorio a2) = a.Entre(_cfg.Fisica.PasseioPresoMinimo, _cfg.Fisica.PasseioPresoMaximo);
            _s = _s with { Aleatorio = a2 };
            // Sorteia a cara sempre (mantém a sequência do gerador), mas só usa se ele ficar.
            Expressao cara = SortearCaraDoPreso();
            string onde = naParede ? "CLIMBING preso pelo usuário" : "HANGING preso pelo usuário no cipó";
            if (escolha == 0)
            {
                _s = _s with { Expressao = cara };
                _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"{onde} + AUTONOMY_TIMER: fica e olha em volta"));
                return;
            }
            double escala = Mundo(out _, out _, out double e) ? e : 1;
            EstadoDoMovimento passeio = _s.Movimento with { Agarrado = false, Restante = dip * escala };
            if (naParede)
                _s = _s with { Movimento = passeio with { SentidoVertical = escolha == 1 ? -1 : 1 } };
            else
                _s = _s with { Movimento = passeio, Direcao = escolha == 1 ? Direcao.Direita : Direcao.Esquerda };
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, naParede
                ? $"{onde} + AUTONOMY_TIMER: passeia pela parede, para {(escolha == 1 ? "cima" : "baixo")}"
                : $"{onde} + AUTONOMY_TIMER: passeia pela borda, para a {(escolha == 1 ? "direita" : "esquerda")}"));
        }

        // Salta pra longe da parede, de costas pra ela.
        private void SaltarDaParede()
        {
            if (!Mundo(out _, out _, out double escala)) return;
            Virar();
            double g = _cfg.Fisica.Gravidade * escala;
            _s = _s with
            {
                Movimento = _s.Movimento with
                {
                    VX = Sentido * _cfg.Fisica.ImpulsoDaParede * escala,
                    VY = -Math.Sqrt(2 * g * _cfg.Fisica.AlturaDoPuloDaParede * escala),
                },
            };
        }

        // Ajusta direção/velocidade depois que a agenda escolheu descer pela quina ou saltar do cipó.
        private void SairDoTeto()
        {
            if (!Mundo(out _, out Superficies sup, out double escala)) return;
            switch (_s.Estado)
            {
                case Estado.Climbing when sup.NaLateral(_s.Movimento.X, out int lado):
                    _s = _s with { Direcao = lado > 0 ? Direcao.Direita : Direcao.Esquerda, Movimento = _s.Movimento with { SentidoVertical = 1 } };
                    break;
                case Estado.Jumping:
                    _s = _s with { Movimento = _s.Movimento with { VX = Sentido * _cfg.Fisica.ImpulsoDaParede * escala, VY = 0 } };
                    break;
            }
        }

        // ---------------------------------------------------------------- validação (SETTLING)

        // SETTLING: monitor da âncora (ou o mais próximo, num vão), prende na área útil e decide pelo apoio.
        // Sem apoio, cai; sem QuedaFisica, vai direto pro chão.
        // pelaMaoDoUsuario: acabou de ser solto; se agarrar parede/cipó, fica preso até tirarem ele.
        // apoio: onde usou o item; na quina, ao alcance dos dois, agarra o mesmo de antes.
        private void Acomodar(PontoPx desejada, string regra, PosicaoDoPersonagem? preferida = null, bool pelaMaoDoUsuario = false, ApoioDoUso? apoio = null)
        {
            IrPara(Estado.Settling, regra);
            (Posicionamento lugar, PosicaoDoPersonagem posicao, bool comApoio) = Validar(desejada, preferida);
            _s = _s with { Lugar = lugar, Posicao = posicao };
            _reagendar = true;

            // Escondido: toda acomodação o devolve ao esconderijo, na mesma borda.
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum)
            {
                Posicionamento escondido = EsconderijoPara(lugar, _s.Esconderijo);
                _s = _s with { Lugar = escondido, Posicao = Posicionador.Descrever(escondido) };
                IrPara(Estado.Peeking, $"SETTLING: escondido atrás da borda ({_s.Esconderijo})");
                return;
            }

            // Solto no alto ou junto a uma lateral, agarra em vez de cair. Quem já estava preso continua
            // preso: clique ou revalidação não o tiram.
            if (!comApoio && _cfg.Movimento && OndeAgarrar(lugar, apoio) is { } agarre)
            {
                bool preso = pelaMaoDoUsuario || _s.PresoPeloUsuario;
                _s = _s with { Lugar = agarre.Lugar, Posicao = Posicionador.Descrever(agarre.Lugar), Direcao = agarre.Direcao, PresoPeloUsuario = preso };
                IrPara(agarre.Estado, agarre.Estado == Estado.Hanging
                    ? "SETTLING: solto perto da borda de cima, agarra o cipó"
                    : "SETTLING: solto junto a uma lateral, fica grudado na parede");
                // Depois do IrPara, que zera o movimento: parado, agarrado, sem relógio.
                _s = _s with { Movimento = _s.Movimento with { Agarrado = true } };
                return;
            }

            _s = _s with { PresoPeloUsuario = false };
            if (comApoio)
                IrPara(Estado.Idle, "SETTLING com apoio");
            else if (_cfg.QuedaFisica)
                IrPara(Estado.Falling, "SETTLING sem apoio");
            else
                IrPara(Estado.Idle, "SETTLING sem apoio: preso no chão (a queda animada é da Fase 4)");
        }

        // Cipó ou lateral, o que estiver mais perto em proporção ao alcance de cada um; com os dois ao alcance,
        // vale o preferido. Perto do chão não agarra e cai, exceto no fim de um uso na parede/cipó: ele já
        // estava lá e volta pro mesmo apoio.
        private (Estado Estado, Posicionamento Lugar, Direcao Direcao)? OndeAgarrar(Posicionamento lugar, ApoioDoUso? preferido = null)
        {
            if (_s.Topologia is not { } topologia) return null;
            MonitorDoDesktop m = lugar.Monitor;
            Superficies sup = Superficies.Do(topologia, m, lugar.Tamanho);
            double escala = m.Dpi / 96.0;
            ParametrosDeMovimento f = _cfg.Fisica;
            PontoPx a = lugar.Ancora;
            bool jaEstavaNoApoio = preferido is ApoioDoUso.Parede or ApoioDoUso.Cipo;
            if (!jaEstavaNoApoio && sup.Chao - a.Y < f.AlturaMinimaParaAgarrar * escala) return null;

            double paraOCipo = (a.Y - sup.Teto) / (f.DistanciaParaOCipo * escala);
            double paraAParede = Math.Min(a.X - sup.Esquerda, sup.Direita - a.X) / (f.DistanciaParaAParede * escala);
            bool cipo = paraOCipo <= 1, parede = paraAParede <= 1;
            if (!cipo && !parede) return null;

            bool peloCipo = preferido switch
            {
                ApoioDoUso.Cipo when cipo => true,
                ApoioDoUso.Parede when parede => false,
                _ => cipo && (!parede || paraOCipo <= paraAParede),
            };
            if (peloCipo)
            {
                var ancora = new PontoPx(Math.Clamp(a.X, sup.Esquerda, sup.Direita), sup.Teto);
                return (Estado.Hanging, NoLugar(m, ancora), _s.Direcao);
            }
            bool direita = sup.Direita - a.X <= a.X - sup.Esquerda;
            var naParede = new PontoPx(direita ? sup.Direita : sup.Esquerda, Math.Clamp(a.Y, sup.Teto, sup.Chao));
            return (Estado.Climbing, NoLugar(m, naParede), direita ? Direcao.Direita : Direcao.Esquerda);
        }

        private Posicionamento NoLugar(MonitorDoDesktop monitor, PontoPx ancora)
        {
            TamanhoPx tamanho = _cfg.Tamanho.ParaPixels(monitor.Dpi);
            return new Posicionamento(monitor, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
        }

        // A validação do SETTLING, sem registrar transição.
        private (Posicionamento Lugar, PosicaoDoPersonagem Posicao, bool ComApoio) Validar(PontoPx desejada, PosicaoDoPersonagem? preferida)
        {
            Topologia topologia = _s.Topologia ?? throw new InvalidOperationException("Validação sem topologia.");
            MonitorDoDesktop monitor = MonitorDaAncora(topologia, desejada);
            TamanhoPx tamanho = _cfg.Tamanho.ParaPixels(monitor.Dpi);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(desejada, tamanho, monitor.AreaUtil);
            bool comApoio = presa.Y == monitor.AreaUtil.Base;
            if (!comApoio && !_cfg.QuedaFisica) presa = presa with { Y = monitor.AreaUtil.Base };

            var lugar = new Posicionamento(monitor, presa, tamanho, Posicionador.RetanguloDoSprite(presa, tamanho));
            PosicaoDoPersonagem posicao = preferida is not null && presa == desejada && preferida.ChaveMonitor == monitor.Chave
                ? preferida with { AncoraAbsoluta = presa, TelaDoMonitor = monitor.Tela }
                : Posicionador.Descrever(lugar);
            return (lugar, posicao, comApoio);
        }

        // Esconder/sair no meio do arraste conta como cancelamento: o ponto atual é validado e vira a escolha.
        private void FixarArrasteInterrompido()
        {
            if (_s.Estado != Estado.Dragging || _s.Lugar is null || _s.Topologia is null) return;
            (Posicionamento lugar, PosicaoDoPersonagem posicao, _) = Validar(_s.Lugar.Ancora, null);
            _s = _s with { Lugar = lugar, Posicao = posicao, RetornoDaTelaCheia = null };
        }

        // Durante o arraste: não prende na área útil; tamanho pelo DPI do monitor da âncora.
        private Posicionamento LugarLivre(Topologia topologia, PontoPx ancora)
        {
            MonitorDoDesktop monitor = MonitorDaAncora(topologia, ancora);
            TamanhoPx tamanho = _cfg.Tamanho.ParaPixels(monitor.Dpi);
            return new Posicionamento(monitor, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
        }

        private void IrPara(Estado novo, string regra)
        {
            Estado de = _s.Estado;
            _transicoes.Add(new Transicao(de, novo, regra));
            _s = _s with { Estado = novo, Motivo = novo == Estado.Hidden ? _s.Motivo : MotivoDoOcultamento.Nenhum };
            // Sair de USING acaba o uso (fim ou interrupção); a onda continua.
            if (de == Estado.Using && novo != Estado.Using) _s = _s with { Uso = null };
            if (de == Estado.Reacting && novo != Estado.Reacting) _s = _s with { Reacao = VarianteDaReacao.Padrao };
            // Trocar de estado desfaz a travessia em curso.
            if (novo != de && _s.Movimento.Travessia is not null) _s = _s with { Movimento = _s.Movimento with { Travessia = null } };
            if (novo != de && DecideNoEstado(novo)) _reagendar = true;
            // Entrando em movimento, parte parado da âncora; quem chamou ajusta velocidade e plano depois.
            if (novo != de && novo.EmMovimento() && _s.Lugar is { } lugar)
                _s = _s with { Movimento = new EstadoDoMovimento(lugar.Ancora.X, lugar.Ancora.Y, 0, 0, double.PositiveInfinity, -1, false) };
        }

        // ---------------------------------------------------------------- efeitos

        internal Resultado Concluir()
        {
            var janela = new List<Efeito>();
            var tempo = new List<Efeito>();

            // Antes de decidir o relógio.
            SoltarOAgarreComCalma();
            AssentarOsInvisiveis();

            bool visivelAntes = _inicio.Estado.Visivel();
            bool visivelDepois = _s.Estado.Visivel();
            if (_s.Estado != Estado.Exiting)
            {
                if (visivelDepois && _s.Lugar is not null && (!visivelAntes || !Equals(_inicio.Lugar, _s.Lugar)))
                    janela.Add(new MoverJanela(_s.Lugar));
                if (!visivelAntes && visivelDepois) janela.Add(new MostrarJanela());
                if (visivelAntes && !visivelDepois) janela.Add(new EsconderJanela());
                // Itens depois do personagem; saindo, a raiz fecha todas.
                EfeitosDosItens(janela);
            }

            // Relógio só roda quando algo se mexe: movimento, reação, uso, gesto ou item visível caindo.
            // Agarrado nada se move, então fica desligado.
            bool agarrado = _s.Estado is Estado.Climbing or Estado.Hanging && _s.Movimento.Agarrado;
            bool relogio = (_s.Estado.EmMovimento() && !agarrado) || _s.Estado is Estado.Reacting or Estado.Using
                || (_s.Estado == Estado.Idle && _s.Gesto != Gesto.Nenhum) || ItemVisivelCaindo;
            if (relogio != _s.RelogioAtivo)
            {
                tempo.Add(relogio ? new LigarRelogio() : new DesligarRelogio());
                _s = _s with { RelogioAtivo = relogio };
            }

            // Agenda: um timer único até a próxima decisão. Com item na mão ela pausa e volta depois do
            // intervalo de acomodação.
            bool querDecisao = DecideNoEstado(_s.Estado) && _s.Gesto == Gesto.Nenhum && !_s.AutonomiaPausada && !_s.PainelAberto && !AtentoAoItem;
            if (querDecisao && (_reagendar || !_s.DecisaoAgendada))
            {
                TimeSpan atraso = SortearAtraso();
                long geracao = _s.Geracao + 1;
                tempo.Add(new AgendarDecisao(atraso, geracao));
                _s = _s with { Geracao = geracao, DecisaoAgendada = true };
            }
            else if (!querDecisao && _s.DecisaoAgendada)
            {
                tempo.Add(new CancelarDecisao());
                _s = _s with { DecisaoAgendada = false };
            }

            // Timer da onda depois do da agenda. Antes zera a carga da paranoia se não sobrou onda de substância.
            ZerarACargaSemSubstancia();
            EfeitosDaOnda(tempo);
            EfeitosDaCuriosidade(tempo);

            var efeitos = new List<Efeito>(_antes.Count + janela.Count + tempo.Count + _depois.Count);
            efeitos.AddRange(_antes);
            efeitos.AddRange(janela);
            efeitos.AddRange(tempo);
            efeitos.AddRange(_depois);
            return new Resultado(_s, efeitos, _transicoes);
        }

        private TimeSpan SortearAtraso()
        {
            PerfilDeEnergia perfil = Perfil;
            (TimeSpan minimo, TimeSpan maximo) = _s.Estado switch
            {
                Estado.Resting => (perfil.DescansoMinimo, perfil.DescansoMaximo),
                // Na parede e pendurado fica pouco tempo.
                Estado.Climbing when _cfg.Movimento => (perfil.TempoNaParedeMinimo, perfil.TempoNaParedeMaximo),
                Estado.Hanging when _cfg.Movimento => (perfil.TempoPenduradoMinimo, perfil.TempoPenduradoMaximo),
                _ => (perfil.DecisaoMinima, perfil.DecisaoMaxima),
            };
            (TimeSpan atraso, Aleatorio a) = _s.Aleatorio.Duracao(minimo, maximo);
            _s = _s with { Aleatorio = a };
            // O intervalo de acomodação é o piso: depois de qualquer interação e entre duas decisões.
            return atraso < _cfg.IntervaloDeAcomodacao ? _cfg.IntervaloDeAcomodacao : atraso;
        }
    }
}
