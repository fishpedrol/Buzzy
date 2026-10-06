using System.Globalization;

namespace Buzzy.Core.Personagem;

/// <summary>Resultado de aplicar um evento: o novo estado, os efeitos em ordem e as transições percorridas.</summary>
public sealed record Resultado(EstadoDoNucleo Estado, IReadOnlyList<Efeito> Efeitos, IReadOnlyList<Transicao> Transicoes);

/// <summary>
/// Máquina de estados do personagem (ARCHITECTURE.md 2.6): função pura de (estado, evento) para
/// (estado novo, efeitos). Não acessa relógio, arquivo nem Windows; o tempo só avança por
/// <see cref="Tick"/> e pelos disparos do <see cref="AutonomyTimer"/> e do <see cref="ItemEffectTimer"/> que ela
/// mesma agendou. O tamagotchi (DEC-028) fica em Maquina.Itens.cs (os itens e o uso) e Maquina.Onda.cs (a onda).
/// </summary>
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

    /// <summary>
    /// Monitor em que está a âncora: o que contém o pixel dos pés, logo acima dela
    /// (<see cref="Posicionador.PixelDosPes"/>). A âncora fica na borda inferior exclusiva do sprite,
    /// que numa pilha de monitores já é o primeiro pixel do monitor de baixo.
    /// </summary>
    public static MonitorDoDesktop MonitorDaAncora(Topologia topologia, PontoPx ancora)
    {
        ArgumentNullException.ThrowIfNull(topologia);
        return topologia.MonitorMaisProximo(Posicionador.PixelDosPes(ancora));
    }

    /// <summary>Estados em que a agenda autônoma mantém um temporizador pendente.</summary>
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

        /// <summary>O perfil de energia em vigor: com a onda de um item, o da fase dela (DEC-028, <see cref="PerfilEfetivo"/>).</summary>
        private PerfilDeEnergia Perfil => PerfilEfetivo(_s, _cfg);

        internal void Tratar(Evento evento)
        {
            if (_s.Estado == Estado.Exiting) return;

            // Com o tamagotchi desligado (DEC-028), os eventos dele são descartados antes de tudo, até de encerrar um
            // gesto curto (L15 da crítica): o núcleo fica exatamente como antes.
            if (!_cfg.Tamagotchi && EhDoTamagotchi(evento)) return;
            // Com a curiosidade desligada (DEC-037), os eventos dela também: o núcleo fica como antes.
            if (!_cfg.Curiosidade && EhDaCuriosidade(evento)) return;
            // Sem as configurações (DEC-038, item 2), os comandos delas também, antes de encerrar um gesto curto.
            if (!_cfg.ConfiguracoesDisponiveis && evento is CmdSetEnergy or CmdSetAlwaysOnTop or CmdSetScale) return;

            // Invariante 15: um gesto curto termina ao chegar qualquer evento de prioridade maior
            // que a do relógio (PRESS, CMD_*, painel, sistema).
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
                case CmdSetFullscreenMode e: EscolherModoTelaCheia(e.Ligado); break;
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

            // A posição salva é restaurada pela cascata da partida (ARCHITECTURE.md 2.8): chave, tela do
            // monitor da época, principal. Sem ela, a posição inicial; o texto da regra fica o de sempre.
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
            _s = _s with { Carregado = true, Topologia = e.Topologia, Preferencias = Sanear(e.Preferencias), Lugar = lugar, Posicao = posicao };

            // A postura gravada com a posição (esquema v3, DEC-029, item 11): a acomodação abaixo o devolve escondido na
            // mesma borda (DEC-025), ou agarrado e ainda preso onde o usuário o deixou (DEC-024); longe da parede e do cipó,
            // ela apaga a marca. Sem posição salva, não vale; sem o esconderijo pelo clique duplo na configuração, a borda
            // também não (ele não teria como sair de lá); fora do enum, nenhuma (SECURITY.md 7).
            if (e.PosicaoSalva is not null)
            {
                LadoDoEsconderijo lado = _cfg.EsconderijoNoCliqueDuplo && Enum.IsDefined(e.Esconderijo) ? e.Esconderijo : LadoDoEsconderijo.Nenhum;
                _s = _s with { Esconderijo = lado, PresoPeloUsuario = e.PresoPeloUsuario };
            }
            // Com a emoção dominante gravada (DEC-027), ele já começa com a cara dela.
            if (_s.Preferencias.EmocaoDominante is { } dominante) _s = _s with { Expressao = dominante };

            if (_s.Estado == Estado.Booting)
                Acomodar(lugar.Ancora, "BOOTING: configurações e topologia carregadas" + restaurada, posicao);
            else if (_s.Motivo == MotivoDoOcultamento.Nenhum)
                Acomodar(lugar.Ancora, "HIDDEN: pedido de mostrar anterior à carga" + restaurada, posicao);

            // O modo de tela cheia vale desde a partida: monitores ocupados avisados antes da carga
            // estão em cache.
            if (_s.Estado.Visivel() && _s.Preferencias.ModoTelaCheia && _s.Lugar is { } l && _s.Ocupados.Contem(l.Monitor.Chave))
                SairDoMonitorOcupado("BOOTING: carga sobre um monitor ocupado pela tela cheia");
        }

        /// <summary>
        /// TOPOLOGY_CHANGED (DEC-030). As posições guardadas (a do personagem e o retorno da tela cheia) e os itens fora da mão
        /// acompanham a topologia em qualquer estado, sem mover a janela (<see cref="Posicionador.Rebasear"/>): escondido, ele
        /// continua ligado à chave do monitor dele. Nos estados que revalidam, vale o que aconteceu com o monitor do personagem:
        /// <list type="bullet">
        /// <item>a geometria dele não mudou (só outros monitores mudaram, ele só foi transladado no desktop virtual ou só a chave
        /// mudou): o estado continua (<see cref="ContinuarNoMonitor"/>). Com a toon force (DEC-023), a lateral da área útil
        /// continua sendo parede, então nenhum apoio deixa de existir;</item>
        /// <item>a geometria mudou: SETTLING na mesma posição relativa, com o texto de sempre;</item>
        /// <item>ele sumiu: SETTLING no sobrevivente mais próximo, medido nas coordenadas antigas.</item>
        /// </list>
        /// USING é tratado como REACTING: continua nos dois primeiros casos e acaba numa revalidação (a onda continua). No
        /// gesto, nada é validado: em PRESSED, toda saída parte da posição acompanhada (<see cref="ReancorarOGesto"/>); em
        /// DRAGGING, o lugar do arraste anda com o monitor em que está, como a janela e o cursor.
        /// </summary>
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

            // No arraste, a janela segue o cursor, e o Windows leva os dois com o monitor físico quando a origem muda: o lugar do
            // arraste anda com o monitor em que está, sem validar. Soltar, esconder ou sair antes do próximo DRAG_MOVE fica no
            // mesmo monitor físico (revisão do bloco P6-P9, achado 2).
            if (_s.Estado == Estado.Dragging && _s.Lugar is { } arrastado)
                _s = _s with { Lugar = LugarLivre(nova, Posicionador.AcompanharPonto(antiga, nova, arrastado.Ancora)) };

            // PRESSED, DRAGGING, BOOTING, HIDDEN e EXITING só atualizam o cache e as posições: a validação acontece ao soltar,
            // ao clicar, ao reaparecer ou ao terminar de carregar.
            GrupoDoEstado grupo = _s.Estado.Grupo();
            bool revalida = grupo is GrupoDoEstado.Autonomo or GrupoDoEstado.Fisico || _s.Estado is Estado.Reacting or Estado.Using;
            if (!revalida || _s.Posicao is null || _s.Lugar is not { } lugar) return;

            // A travessia em curso (andando, ou no voo do salto) guarda chaves e coordenadas absolutas: numa mudança de
            // configuração, ela se desfaz e ele se acomoda pela posição relativa (passo P13; C15 da crítica), mesmo se só o outro
            // monitor mudou. A caminhada até a partida de um salto ainda não é travessia: só o plano cai, e ela para no passo
            // seguinte, pela regra de sempre.
            if (_s.Movimento.Travessia is { } travessia)
            {
                if (_s.Estado == Estado.Jumping || (_s.Estado == Estado.Walking && travessia.Tipo == TipoDeTravessia.Andando))
                {
                    // A volta da tela cheia interrompida (DEC-035) termina na posição de antes dela, que já acompanhou a topologia.
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

        /// <summary>
        /// O monitor do personagem continua com a mesma geometria, no máximo transladado em (<paramref name="dx"/>,
        /// <paramref name="dy"/>): o estado continua, com o que estiver em curso (a caminhada, a escalada, o pulo, a reação, o uso,
        /// o esconderijo e o preso), e a âncora, a posição fina e a janela andam juntas. A posição passa a descrever o lugar
        /// novo, para não divergir 1 px da âncora por arredondamento. O relógio e a agenda não mudam; uma transição para o
        /// mesmo estado registra o que aconteceu, sem a chave (que vai ao log).
        /// </summary>
        private void ContinuarNoMonitor(MonitorDoDesktop novo, int dx, int dy)
        {
            Posicionamento antes = _s.Lugar!;
            var ancora = new PontoPx(antes.Ancora.X + dx, antes.Ancora.Y + dy);
            var lugar = new Posicionamento(novo, ancora, antes.Tamanho, antes.Retangulo.Deslocado(dx, dy));
            _s = _s with { Lugar = lugar, Posicao = Posicionador.Descrever(lugar) };
            // A posição fina só vale nos estados de movimento; nos outros, quem entra num deles parte da âncora (IrPara).
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
            // Pegar no ar a volta da tela cheia (DEC-035): a tela cheia já acabou, e o usuário assume daqui; o retorno acaba.
            if (PuloEmVoo is { Pulo: PuloDaTelaCheia.Volta }) _s = _s with { RetornoDaTelaCheia = null };
            _s = _s with { Pegada = new PontoPx(cursor.X - ancora.X, cursor.Y - ancora.Y), PassosRestantes = 0, TelaCheiaMudouNoGesto = false };
            if (_cfg.Personalidade) _s = _s with { ReagiuDe = _s.Estado };
            PegarNoEpisodio();
            IrPara(Estado.Pressed, "PRESS sobre pixel opaco");
        }

        /// <summary>
        /// Fim de um gesto do usuário (linha PRESSED, DRAGGING | FULLSCREEN_TARGETS_CHANGED): um
        /// arraste é sempre escolha de posição; um clique ou um cancelamento só descartam o retorno
        /// temporário se a tela cheia mudou durante o gesto (o ponto do usuário vale). Se o modo foi
        /// desligado durante o gesto, que não escolheu posição, o efeito temporário se desfaz agora:
        /// o personagem volta à posição de antes da tela cheia (linha SETTINGS_CHANGED).
        /// </summary>
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

        /// <summary>
        /// R14 em toda saída de PRESSED (DEC-030; revisão do bloco P6-P9, achados 2 e 3): com o botão pressionado,
        /// TOPOLOGY_CHANGED só acompanha a posição (<see cref="Posicionador.Rebasear"/>), e o lugar de antes do gesto fica nas
        /// coordenadas antigas. Se o monitor dele mudou ou sumiu, o lugar passa a ser o que a posição acompanhada descreve, como
        /// ele estaria parado (<see cref="Posicionador.Reacomodar"/>): a mesma posição relativa no monitor correspondente ou, sem
        /// ele, no mais próximo da âncora acompanhada; validado, sem sair do gesto. As frações continuam descrevendo o lugar
        /// validado. Senão, nada muda. Vale para CLICK, DOUBLE_CLICK, DRAG_CANCEL e DRAG_START.
        /// </summary>
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
            // Se o monitor do personagem mudou ou sumiu no gesto, valida já no CLICK, sem sair da reação, a partir da posição
            // que acompanhou a topologia; senão, a reação começa no mesmo lugar.
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
            // Escondido ou preso, a reação de antes, com a duração de antes.
            int passos = variante == VarianteDaReacao.Padrao && (_s.Esconderijo != LadoDoEsconderijo.Nenhum || _s.PresoPeloUsuario) ? _cfg.PassosDaReacao : Perfil.PassosDaReacao;
            _s = _s with { PassosRestantes = passos, Expressao = CaraDaReacao(variante), Sinal = Sinal.FoiClicado, Reacao = variante };
            IrPara(Estado.Reacting, $"CLICK: {regra}");
        }

        /// <summary>
        /// A variante da reação ao clique (DEC-037, item 8), por regra e sem sorteio, nesta ordem: escondido ou preso, a de
        /// antes; clicado descansando, um susto; olhando a janela, um flagra; o segundo clique seguido desde a última decisão,
        /// empolgada; senão, a da energia (Baixa preguiçosa, Média a de antes, Alta empolgada).
        /// </summary>
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

        /// <summary>A cara do núcleo para cada variante da reação: a do primeiro quadro do clipe dela (ARCHITECTURE.md 2.10).</summary>
        private static Expressao CaraDaReacao(VarianteDaReacao variante) => variante switch
        {
            VarianteDaReacao.Susto or VarianteDaReacao.Flagra => Expressao.Surpreso,
            VarianteDaReacao.Empolgada => Expressao.Empolgado,
            VarianteDaReacao.Preguica => Expressao.Sonolento,
            _ => Expressao.Feliz,
        };

        /// <summary>Se ele está olhando a janela ativa, pela curiosidade (DEC-037, item 5): o clique é um flagra, e ele volta a olhar.</summary>
        private bool OlhandoAJanela => _cfg.Curiosidade && _s.Curiosidade == EstagioDaCuriosidade.Olhando;

        private void CliqueDuplo()
        {
            Estado de = _s.Estado;
            // A partir de PRESSED, o lugar do gesto no referencial atual (R14): é dele que ele se esconde ou fica.
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

        /// <summary>
        /// Clique duplo com o esconderijo ligado (DEC-025): escondido, sai de lá; senão, esconde-se
        /// atrás da borda mais próxima, só com a cabeça e as mãos para fora.
        /// </summary>
        private void AlternarEsconderijo(Estado de)
        {
            if (_s.Topologia is null) return;
            // O fim do gesto vem antes: com o modo de tela cheia desligado no meio dele, ele devolve
            // o personagem à posição de antes da tela cheia (DEC-020), e é de lá que ele se esconde.
            if (de == Estado.Pressed) FimDoGestoDoUsuario(escolheuPosicao: false);
            if (_s.Lugar is not { } lugar) return;
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum)
            {
                // Sai do esconderijo pela mão do usuário: na borda de baixo, fica de pé no chão; numa
                // lateral, fica grudado na parede (DEC-024).
                _s = _s with { Esconderijo = LadoDoEsconderijo.Nenhum, Expressao = Expressao.Feliz, Sinal = Sinal.FoiClicadoDuasVezes };
                Acomodar(lugar.Ancora, "DOUBLE_CLICK: sai do esconderijo", pelaMaoDoUsuario: true);
                return;
            }
            LadoDoEsconderijo lado = LadoMaisProximo(lugar);
            _s = _s with { Esconderijo = lado, Expressao = Expressao.Curioso, Sinal = Sinal.FoiClicadoDuasVezes };
            Acomodar(lugar.Ancora, $"DOUBLE_CLICK: esconde-se atrás da borda ({lado})");
        }

        /// <summary>
        /// Borda do esconderijo (DEC-025): a de cima, se o topo do sprite está ao alcance do cipó (pendurado nele, por exemplo;
        /// pedido do usuário de 2026-10-03); a lateral mais próxima, se o personagem está no alto e junto dela (na parede, por
        /// exemplo); senão, a de baixo.
        /// </summary>
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

        /// <summary>
        /// Onde fica o esconderijo na borda dada (DEC-025), perto do lugar atual: na de baixo, os pés
        /// no chão (o quadro inteiro fica acima da barra e a pose só mostra a cabeça e as mãos); numa
        /// lateral, encostado nela, na mesma altura; na de cima, com o topo do sprite nela, como no cipó.
        /// O sprite continua inteiro na área útil.
        /// </summary>
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

        /// <summary>Caras de quem está escondido, espiando o que acontece (DEC-025).</summary>
        private static readonly Expressao[] ExpressoesDoEscondido = [Expressao.Curioso, Expressao.Travesso, Expressao.Feliz, Expressao.Surpreso, Expressao.Pensativo, Expressao.Rindo];

        /// <summary>
        /// A cara de quem espia escondido, num único sorteio: com a onda de um item, uma das caras da fase (DEC-028); com a
        /// emoção dominante, ela ou uma companheira (DEC-027); na automática, uma das outras caras de quem espia.
        /// </summary>
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
            // O arraste parte do lugar do gesto no referencial atual (R14): um cancelamento antes do primeiro DRAG_MOVE fica lá.
            ReancorarOGesto();
            // Invariante 9: iniciar um arraste fecha o painel de energia.
            FecharPainelSeAberto();
            // Arrastar tira o personagem do esconderijo (DEC-025) e zera os cliques seguidos (DEC-037, item 8).
            _s = _s with { Esconderijo = LadoDoEsconderijo.Nenhum, CliquesSeguidos = 0 };
            IrPara(Estado.Dragging, "DRAG_START");
        }

        private void Arrastar(PontoPx cursor)
        {
            if (_s.Estado != Estado.Dragging || _s.Topologia is null) return;
            // Invariante 2: posição = cursor menos o deslocamento da pegada, sem física nem limite.
            var ancora = new PontoPx(cursor.X - _s.Pegada.X, cursor.Y - _s.Pegada.Y);
            _s = _s with { Lugar = LugarLivre(_s.Topologia, ancora) };
        }

        private void Soltar(PontoPx cursor)
        {
            if (_s.Estado != Estado.Dragging) return;
            var ancora = new PontoPx(cursor.X - _s.Pegada.X, cursor.Y - _s.Pegada.Y);
            // Soltar é escolha manual: descarta o retorno temporário da tela cheia (DEC-013).
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
                // O personagem fica onde estava; não volta ao ponto de origem (ARCHITECTURE.md 2.7).
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
            // SECURITY.md 7: só os três níveis permitidos; um valor fora deles é ignorado.
            if (!_s.PainelAberto || !Enum.IsDefined(nivel)) return;
            if (_s.Preferencias.Energia == nivel) return;
            _s = _s with { Preferencias = _s.Preferencias with { Energia = nivel } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        // ---------------------------------------------------------------- comandos das configurações (DEC-038, item 2)

        /// <summary>
        /// CMD_SET_ENERGY: a energia pelas configurações, sem exigir o painel. Só o campo muda e é gravado; a agenda armada
        /// fica como está, e a próxima decisão usa o perfil novo (invariante 35).
        /// </summary>
        private void EscolherEnergiaPorComando(NivelDeEnergia nivel)
        {
            if (!_s.Carregado || !Enum.IsDefined(nivel) || _s.Preferencias.Energia == nivel) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ENERGY: {nivel}"));
            _s = _s with { Preferencias = _s.Preferencias with { Energia = nivel } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
        }

        /// <summary>CMD_SET_ALWAYS_ON_TOP: muda o campo, grava e pede à raiz que o aplique.</summary>
        private void EscolherSempreNoTopo(bool ligado)
        {
            if (!_s.Carregado || _s.Preferencias.SempreNoTopo == ligado) return;
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ALWAYS_ON_TOP: {(ligado ? "ligado" : "desligado")}"));
            _s = _s with { Preferencias = _s.Preferencias with { SempreNoTopo = ligado } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _depois.Add(new AplicarSempreNoTopo(ligado));
        }

        /// <summary>CMD_SET_SCALE: só o campo muda e é gravado; vale na próxima abertura.</summary>
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
                // Um evento do sistema não troca uma ocultação do usuário; entre os motivos do
                // sistema, a sessão bloqueada prevalece sobre a suspensão e a tela cheia
                // (precedência em ARCHITECTURE.md 2.6).
                if (Precedencia(motivo) > Precedencia(_s.Motivo))
                {
                    _transicoes.Add(new Transicao(Estado.Hidden, Estado.Hidden, $"{regra}: motivo {_s.Motivo} -> {motivo}"));
                    // A ocultação deixa de ser da tela cheia: o retorno temporário volta a ser a
                    // posição do personagem, sem reaparecer, e não sobra para o próximo episódio.
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
            // Os itens (DEC-028): o da mão solta a captura, e os que caem vão ao chão (D18).
            LiberarItemNaMao();
            AssentarItens();
            _s = _s with { Motivo = motivo, PassosRestantes = 0 };
            IrPara(Estado.Hidden, regra);
            // Com o modo desligado, o retorno só sobra de um PRESSED interrompido: a posição de
            // antes da tela cheia volta a valer, como no fim do gesto.
            if (!_s.Preferencias.ModoTelaCheia && _s.RetornoDaTelaCheia is { } retorno)
                _s = _s with { Posicao = retorno, RetornoDaTelaCheia = null };
            GravarPosicaoDoUsuario();
        }

        /// <summary>
        /// Grava a posição escolhida pelo usuário: durante a tela cheia, a de antes da transferência
        /// automática, nunca a temporária (SECURITY.md 5: a posição temporária fica só em memória).
        /// </summary>
        private void GravarPosicaoDoUsuario()
        {
            if ((_s.RetornoDaTelaCheia ?? _s.Posicao) is { } posicao) GravarComAPostura(posicao);
        }

        /// <summary>
        /// O efeito que grava a posição, com a postura do estado como ela está agora (esquema v3, DEC-029, item 11): a borda
        /// do esconderijo (DEC-025) e a marca "preso pelo usuário" (DEC-024). Escondido pela bandeja ou pela sessão, a borda
        /// continua no estado e vai junto; a ocultação, não.
        /// </summary>
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
            // Invariante 10: SESSION_UNLOCKED e RESUMED nunca mostram o que o usuário escondeu.
            if (_s.Estado != Estado.Hidden || _s.Motivo != motivoQueSeDesfaz) return;
            Mostrar(regra);
            // Um evento do sistema também não desfaz o modo de tela cheia: se o personagem
            // reapareceu num monitor ainda ocupado (monitores em cache), o modo age de novo.
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
                    // O usuário pediu: aparece na posição anterior e descarta o retorno temporário.
                    _s = _s with { Posicao = _s.RetornoDaTelaCheia ?? _s.Posicao, RetornoDaTelaCheia = null };
                }
                else
                {
                    // Escondido por outro motivo no meio de um episódio de tela cheia: mostrar é
                    // escolha manual, então reaparece onde estava e o fim da tela cheia não o move
                    // mais (invariante 14).
                    _s = _s with { RetornoDaTelaCheia = null };
                }
                Mostrar("CMD_SHOW");
                return;
            }
            // Visível: mostrar manualmente durante a tela cheia vale como escolha do usuário
            // (invariante 14).
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
            // Fase 4 (DEC-022): pausar para a caminhada na hora; na parede ele desce e pendurado se
            // solta nos passos seguintes (PassoEscalando, PassoPendurado).
            // A travessia em curso é atômica (passo P13; D7): ela termina, e o fim dela o para.
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

        // ---------------------------------------------------------------- emoção dominante (DEC-027)

        /// <summary>
        /// CMD_SET_DOMINANT_EMOTION: grava a emoção nas preferências e, com a cara livre, a mostra na hora. Antes da
        /// carga, fora das 14 caras de humor ou igual à atual, é ignorada. "Automática" (nula) mantém a cara atual
        /// até a próxima troca. Não muda estado, posição nem agenda: a transição para o mesmo estado só registra a
        /// escolha.
        /// </summary>
        private void EscolherEmocao(Expressao? emocao)
        {
            if (!_s.Carregado || (emocao is { } e && !Expressoes.EhDeHumor(e)) || emocao == _s.Preferencias.EmocaoDominante) return;
            _s = _s with { Preferencias = _s.Preferencias with { EmocaoDominante = emocao } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_DOMINANT_EMOTION: {emocao?.ToString() ?? "Automatica"}"));
            // Com a onda de um item, a cara dela tem precedência; a dominante entra quando a onda acabar (DEC-028).
            if (emocao is { } nova && !ComOnda && CaraLivre(_s.Estado)) _s = _s with { Expressao = nova };
        }

        /// <summary>
        /// Estados em que a cara pode mudar na hora, pela emoção dominante ou pela fase da onda de um item: em RESTING
        /// (sonolento), em REACTING (feliz com o clique) e em USING (a cara de quem usa o item, DEC-028), a cara do estado
        /// continua até ele acabar, e então volta à de base.
        /// </summary>
        private static bool CaraLivre(Estado estado) => estado is not (Estado.Resting or Estado.Reacting or Estado.Using);

        /// <summary>
        /// A cara de base: com a onda de um item, a da fase dela (DEC-028); sem onda, a emoção dominante (DEC-027); na
        /// automática, a neutra, como antes.
        /// </summary>
        private Expressao CaraDeBase()
            => ComOnda && _s.Onda is { } onda ? _cfg.TabelaDeOndas(onda.Tipo).Cara(onda.Fase)
               : OlhandoAJanela ? Expressao.Curioso
               : _s.Preferencias.EmocaoDominante ?? Expressao.Neutro;

        /// <summary>
        /// Fim da reação ao clique e do pouso: com a onda de um item (DEC-028) ou com a emoção dominante (DEC-027), a cara
        /// volta à de base. Na automática, sem onda, nada muda: a cara da reação ou do quique fica até a próxima troca,
        /// como antes.
        /// </summary>
        private void VoltarACaraDeBase()
        {
            // Olhando a janela (DEC-037), a cara curiosa volta depois do flagra.
            if (ComOnda || _s.Preferencias.EmocaoDominante is not null || OlhandoAJanela) _s = _s with { Expressao = CaraDeBase() };
        }

        // ---------------------------------------------------------------- tela cheia (DEC-013)

        private void MudarTelaCheia(MonitoresOcupados ocupados)
        {
            bool mudou = !ocupados.Equals(_s.Ocupados);
            _s = _s with { Ocupados = ocupados };
            // Uma vez por mudança; com o modo desligado, só o cache.
            if (!mudou || !_s.Preferencias.ModoTelaCheia || _s.Topologia is null) return;

            // Invariante 14: PRESSED e DRAGGING nunca são interrompidos pelo modo; ao soltar, o ponto
            // do usuário vale e o retorno temporário é descartado (FimDoGestoDoUsuario).
            if (_s.Estado is Estado.Pressed or Estado.Dragging)
            {
                _s = _s with { TelaCheiaMudouNoGesto = true };
                return;
            }
            if (_s.Estado is Estado.Booting or Estado.Exiting) return;
            DesistirDoSaltoParaMonitorFechado();

            // O pulo da tela cheia em voo (DEC-035) segue com o destino livre. A tela cheia acabou: volta dali mesmo. O destino
            // ficou ocupado: sai dali para o monitor livre mais próximo, com o retorno de antes.
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
                    // O episódio terminou com o personagem escondido por outro motivo (usuário,
                    // sessão, suspensão): ele não reaparece, mas a posição de antes da tela cheia
                    // volta a valer para quando reaparecer, e o retorno não sobra.
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

        /// <summary>
        /// Personagem visível num monitor ocupado pela tela cheia (monitores em cache): guarda a
        /// posição anterior só em memória, se ainda não houver uma, e transfere para o monitor livre
        /// mais próximo, sem ativar; se nenhum estiver livre, fecha o painel e esconde.
        /// </summary>
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
            // Perto da lateral que encosta no livre, só um pulinho para o outro lado da borda (item 6); senão, ou sem um arco
            // dele que caiba, o pulo até o cipó.
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

        // ---------------------------------------------------------------- pulo da tela cheia (DEC-035)

        /// <summary>O pulo da tela cheia em voo, ou nulo.</summary>
        private Travessia? PuloEmVoo => _s.Estado == Estado.Jumping && _s.Movimento.Travessia is { Pulo: not PuloDaTelaCheia.Nenhum } pulo ? pulo : null;

        /// <summary>
        /// Visível, com a posição de antes da tela cheia guardada: volta para ela num pulo (DEC-035) ou, sem um arco que caiba,
        /// direto, como antes, com a <paramref name="regraDireta"/>.
        /// </summary>
        private void VoltarDaTelaCheia(string regraDoPulo, string regraDireta)
        {
            if (_s.RetornoDaTelaCheia is { } retorno && _s.Topologia is { } t)
            {
                (Posicionamento r, _) = Posicionador.Reacomodar(t, retorno, _cfg.Tamanho);
                // Das duas pontas perto da mesma borda entre os monitores, a volta também é um pulinho (item 6).
                bool pulinho = _s.Lugar is { } l && t.PorChave(l.Monitor.Chave) is { } aqui && aqui.Chave != r.Monitor.Chave
                    && LadoDoPulinho(aqui, l.Ancora.X, r.Monitor) is var lado && lado != 0 && LadoDoPulinho(r.Monitor, r.Ancora.X, aqui) == -lado;
                if (pulinho && Pular(r.Monitor, r.Ancora, PuloDaTelaCheia.Volta, $"{regraDoPulo} (pulinho)", pulinho: true)) return;
                if (Pular(r.Monitor, r.Ancora, PuloDaTelaCheia.Volta, regraDoPulo)) return;
            }
            RestaurarRetorno(regraDireta);
        }

        /// <summary>
        /// O lado (−1 ou +1) do monitor <paramref name="monitor"/> em que ele está perto da borda com o <paramref name="vizinho"/>
        /// (DEC-035, item 6): a lateral tem uma porta para o vizinho (as áreas úteis se encostam, DEC-032), e a âncora em
        /// <paramref name="x"/> está a até <see cref="ParametrosDeMovimento.DistanciaDoPulinho"/> dela. Senão, 0: no meio, ou perto
        /// da beirada sem monitor do lado.
        /// </summary>
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

        /// <summary>
        /// Onde o pulinho pousa (DEC-035, item 6): do outro lado da borda, a <see cref="ParametrosDeMovimento.EntradaDoPulinho"/>
        /// dela, na mesma superfície: no chão, no chão do livre; no cipó, no cipó; na parede da borda, encostado na parede do
        /// livre, na mesma altura.
        /// </summary>
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

        /// <summary>
        /// Onde ele agarra o cipó do monitor livre (DEC-035): na borda de cima, a <see cref="ParametrosDeMovimento.EntradaNoCipo"/>
        /// da lateral voltada para a partida; com a partida na faixa do monitor (um em cima do outro), na mesma vertical.
        /// </summary>
        private PontoPx ChegadaNoCipo(MonitorDoDesktop livre)
        {
            Superficies sup = Superficies.Do(_s.Topologia!, livre, _cfg.Tamanho.ParaPixels(livre.Dpi));
            int entrada = (int)Math.Round(_cfg.Fisica.EntradaNoCipo * livre.Dpi / 96.0, MidpointRounding.AwayFromZero);
            int partida = _s.Lugar!.Ancora.X;
            int x = partida < livre.Tela.Esquerda ? sup.Esquerda + entrada : partida >= livre.Tela.Direita ? sup.Direita - entrada : partida;
            return new PontoPx(Math.Clamp(x, sup.Esquerda, sup.Direita), sup.Teto);
        }

        /// <summary>
        /// Começa o pulo da tela cheia (DEC-035) da âncora atual até <paramref name="chegada"/>, no monitor
        /// <paramref name="destino"/>: JUMPING, com o arco de <see cref="Passagens.PlanejarPuloDaTelaCheia"/>, sem o gesto em curso e
        /// com a cara empolgada (na automática, sem onda). Com a capacidade ou o movimento desligados, ou sem um arco em que o
        /// sprite fique na união das áreas úteis (por exemplo, escondido atrás da borda), não muda nada e devolve falso: quem
        /// chamou faz a troca direta, como antes.
        /// </summary>
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
            // IrPara só recomeça o movimento quando o estado muda: um pulo que substitui outro em voo parte daqui também.
            _s = _s with { Movimento = new EstadoDoMovimento(partida.X, partida.Y, 0, 0, double.PositiveInfinity, -1, false) { Travessia = plano } };
            return true;
        }

        /// <summary>
        /// O fim do pulo da tela cheia (DEC-035): a acomodação na chegada. Na ida, perto da borda de cima, ele agarra o cipó
        /// (<see cref="OndeAgarrar"/>); no chão (o pulinho, item 6), pousa (LANDING); na volta, a posição de antes da tela cheia vale, com a postura dela, e o retorno acaba.
        /// </summary>
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
            // A ida que acaba no chão (o pulinho, item 6) pousa, como o salto de degrau.
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

        /// <summary>
        /// Esconder ou sair com o pulo da tela cheia em voo (DEC-035): ele vale como terminado. Na ida, a posição passa a ser a
        /// chegada, e o retorno continua guardado; na volta, a posição de antes da tela cheia, e o retorno acaba. Assim, nem a
        /// gravação nem o reaparecer partem de um ponto no ar, por cima do monitor ocupado.
        /// </summary>
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

        /// <summary>
        /// Se o monitor de chave <paramref name="chave"/> está fechado à autonomia (DEC-034): ocupado pela tela cheia, com o modo
        /// ligado. A travessia não entra nele, e a lateral que encosta nele é parede; arrastar até lá continua valendo
        /// (DEC-013: a escolha do usuário prevalece).
        /// </summary>
        private bool Fechado(string chave) => _s.Preferencias.ModoTelaCheia && _s.Ocupados.Contem(chave);

        /// <summary>O filtro das portas (<see cref="Fechado"/>), ou nulo sem nenhum monitor ocupado com o modo ligado.</summary>
        private Func<string, bool>? Fechados => _s.Preferencias.ModoTelaCheia && !_s.Ocupados.Vazio ? Fechado : null;

        /// <summary>
        /// A caminhada até a partida de um salto de degrau planejado para um monitor que ficou fechado (DEC-034): o plano cai,
        /// e ela para no passo seguinte, pela regra de sempre, como numa mudança de topologia. Uma travessia já em curso
        /// termina, e na chegada ele volta pela porta (<see cref="VoltarSeAPortaFechou"/>).
        /// </summary>
        private void DesistirDoSaltoParaMonitorFechado()
        {
            if (_s.Estado == Estado.Walking && _s.Movimento.Travessia is { Tipo: TipoDeTravessia.Salto } planejado && Fechado(planejado.ChaveDestino))
                _s = _s with { Movimento = _s.Movimento with { Travessia = null, Restante = 0 } };
        }

        /// <summary>
        /// O fim de uma travessia num monitor que fechou no meio dela (DEC-034): a porta fechou, e ele volta à origem, inteiro,
        /// encostado na lateral da porta, no chão, sem retorno guardado, porque nunca esteve lá por escolha. Sem a origem, ou
        /// com ela fechada também, sai como numa mudança de tela cheia (<see cref="SairDoMonitorOcupado"/>). Devolve se voltou.
        /// </summary>
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

        /// <summary>
        /// CMD_SET_FULLSCREEN_MODE (DEC-034): o modo de tela cheia (Q-09) ligado ou desligado pelo menu. Registra a escolha numa
        /// transição para o mesmo estado e a grava nas preferências. Desligar desfaz o efeito temporário, como pelas
        /// preferências (<see cref="MudarPreferencias"/>). Ligar com ele à vista num monitor já ocupado o tira de lá, como se a
        /// tela cheia tivesse acabado de começar, e um salto planejado para um monitor ocupado cai; num gesto do usuário,
        /// nada muda (invariante 14). Antes da carga ou igual ao atual, é ignorado.
        /// </summary>
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

        /// <summary>
        /// SECURITY.md 7: nível de energia fora de BAIXA/MEDIA/ALTA vira o padrão seguro, Média; emoção dominante fora
        /// das 14 caras de humor vira "Automática" (DEC-027); escala fora das três vira a Média (DEC-038).
        /// </summary>
        private static Preferencias Sanear(Preferencias preferencias)
        {
            if (!Enum.IsDefined(preferencias.Energia)) preferencias = preferencias with { Energia = Preferencias.Padrao.Energia };
            if (preferencias.EmocaoDominante is { } emocao && !Expressoes.EhDeHumor(emocao)) preferencias = preferencias with { EmocaoDominante = null };
            if (!Enum.IsDefined(preferencias.Escala)) preferencias = preferencias with { Escala = Preferencias.Padrao.Escala };
            preferencias = preferencias with { ItensAdultosHabilitados = Preferencias.NormalizarItensAdultos(preferencias.ItensAdultosHabilitados) };
            return preferencias;
        }

        private void MudarPreferencias(Preferencias novas)
        {
            Preferencias antes = _s.Preferencias;
            novas = Sanear(novas);
            _s = _s with { Preferencias = novas };
            // Uma emoção dominante nova aparece na hora, como pelo menu (DEC-027), a não ser com a onda de um item, que tem
            // precedência (DEC-028); antes da carga, quem decide a cara de partida é a carga.
            if (_s.Carregado && novas.EmocaoDominante is { } emocao && emocao != antes.EmocaoDominante && !ComOnda && CaraLivre(_s.Estado))
                _s = _s with { Expressao = emocao };
            if (_s.Carregado)
                foreach (Item item in TabelaDoTamagotchi.Itens)
                    if (ItemAdulto(item) && antes.ItensAdultosHabilitados.Contem(item) && !novas.ItensAdultosHabilitados.Contem(item))
                        TirarOItemAdulto(item);
            // Desligar o conteúdo adulto pelas preferências faz o mesmo que pelo menu (DEC-033).
            if (_s.Carregado && antes.ConteudoAdulto && !novas.ConteudoAdulto) TirarOConteudoAdulto();
            if (!antes.ModoTelaCheia || novas.ModoTelaCheia) return;

            // Desligar o modo desfaz o efeito temporário dele (Q-09): a posição temporária nunca vira
            // a do usuário (invariante 16).
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
                // Invariante 14: o gesto não é interrompido; o fim dele decide (FimDoGestoDoUsuario).
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
            // Os itens que caem (DEC-028) seguem a própria física, em qualquer estado do personagem.
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
                // Fase 4 (DEC-022): o passo físico move o personagem pelas superfícies.
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
                    // Com a toon force (DEC-023), toda lateral dá para descer, mesmo a passagem.
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
            // Invariante 1 e ARCHITECTURE.md 2.3: nada autônomo com o usuário no controle, com
            // a autonomia pausada, com o painel aberto ou com o usuário segurando um item (DEC-028).
            if (_s.Estado.ControladoPeloUsuario() || _s.AutonomiaPausada || _s.PainelAberto || !_s.Estado.Visivel() || AtentoAoItem) return;

            _s = _s with { DecisaoAgendada = false, CliquesSeguidos = 0 };
            _reagendar = true;
            switch (_s.Estado)
            {
                case Estado.Idle:
                    // A curiosidade decide primeiro (DEC-037); sem ela, ou sem estágio que aja, a agenda de sempre.
                    if (!DecidirCurioso()) DecidirParado();
                    break;
                case Estado.Resting:
                    _s = _s with { Sinal = Sinal.Acordou, Expressao = CaraDeBase() };
                    IrPara(Estado.Idle, "RESTING + AUTONOMY_TIMER: acorda");
                    break;
                case Estado.Peeking:
                    // Escondido (DEC-025): nada o tira de lá; só troca a cara, espiando.
                    Expressao espiando = SortearCaraDoEscondido();
                    _s = _s with { Expressao = espiando };
                    _transicoes.Add(new Transicao(Estado.Peeking, Estado.Peeking, "PEEKING + AUTONOMY_TIMER: espia com outra cara"));
                    break;
                case Estado.Climbing or Estado.Hanging when _cfg.Movimento && _s.PresoPeloUsuario:
                    DecidirPreso();
                    break;
                case Estado.Climbing when _cfg.Movimento && _s.Movimento.Agarrado:
                    // Agarrado sem ter sido posto pelo usuário (numa revalidação): volta a escalar,
                    // para cima ou para baixo, salta ou se solta.
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
                    // Pendurado no meio da borda não há parede para descer; só na quina.
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
            // Com a física, escalar exige saber onde estão as laterais do monitor. Com a toon force
            // (DEC-023), as duas são escaláveis, mesmo a que encosta em outro monitor.
            bool temLateral = !_cfg.Movimento || Mundo(out _, out _, out _);
            Opcao(AcoesAutonomas.Andar, perfil.PesoAndar);
            Opcao(AcoesAutonomas.Escalar, temLateral ? perfil.PesoEscalar : 0);
            Opcao(AcoesAutonomas.Pular, perfil.PesoPular);
            Opcao(AcoesAutonomas.Descansar, perfil.PesoDescansar);
            Opcao(AcoesAutonomas.Gesto, perfil.PesoGesto);
            Opcao(AcoesAutonomas.TrocarExpressao, perfil.PesoTrocarExpressao);
            // O baseado por conta própria (DEC-028; pedido do usuário de 2026-10-01, 19:10), a última opção: sem ela (a chave
            // desligada, fora do chão, com a onda Chapado ou a paranoia na frente), o sorteio é o de sempre.
            Opcao(AcoesAutonomas.FumarBaseado, PodeFumarPorContaPropria ? perfil.PesoFumarBaseado : 0);
            // Ir ao outro monitor (Fase 5, passo P13), a última opção: só com uma porta plana, numa lateral do monitor dele.
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
                    // Dois sorteios, com ou sem onda: o gesto (o da fase, com a onda de um item) e a duração.
                    (Gesto gesto, Aleatorio a3) = SortearGesto();
                    (int passos, Aleatorio a4) = a3.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
                    _s = _s with { Aleatorio = a4, Gesto = gesto, PassosDoGesto = passos };
                    _transicoes.Add(new Transicao(Estado.Idle, Estado.Idle, $"IDLE + AUTONOMY_TIMER: gesto {gesto}"));
                    break;
                case AcoesAutonomas.TrocarExpressao:
                    // O sorteio avança o gerador em _s: a cara vai para uma variável antes do "with".
                    Expressao nova = SortearTrocaDeCara();
                    _s = _s with { Expressao = nova };
                    break;
                case AcoesAutonomas.FumarBaseado:
                    FumarPorContaPropria();
                    break;
                case AcoesAutonomas.IrAoOutroMonitor:
                    PlanejarIdaAoOutroMonitor(perfil, portaEsquerda, portaDireita);
                    break;
            }
        }

        /// <summary>
        /// As laterais do monitor dele por onde a travessia vale agora (<see cref="PlanoDeTravessia"/>): andando ou num salto de
        /// degrau, com a partida entre ele e a lateral.
        /// </summary>
        private (bool Esquerda, bool Direita) PortasDeTravessia()
        {
            if (!_cfg.Movimento || !Mundo(out MonitorDoDesktop m, out Superficies sup, out _)) return (false, false);
            double x = _s.Movimento.X;
            return (PlanoDeTravessia(m, -1, (int)Math.Max(0, x - sup.Esquerda)) is not null || TemTransbordo(m, -1),
                PlanoDeTravessia(m, +1, (int)Math.Max(0, sup.Direita - x)) is not null || TemTransbordo(m, +1));
        }

        /// <summary>
        /// Ir ao outro monitor (<see cref="AcoesAutonomas.IrAoOutroMonitor"/>; Fase 5, passo P13): anda até a porta plana (com
        /// duas, sorteia o lado) e atravessa sem o sorteio da porta; o percurso é a distância até a borda mais uma caminhada do
        /// perfil, que ele segue do outro lado.
        /// </summary>
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

        /// <summary>
        /// Anda até a porta do lado <paramref name="lado"/> e atravessa sem o sorteio da porta, seguindo <paramref name="dip"/>
        /// DIP do outro lado: a porta plana, o salto de degrau ou o transbordo (Fase 5, P13). A ida ao outro monitor e a ida
        /// para ver da curiosidade (DEC-037, item 4) usam a mesma.
        /// </summary>
        private void IrAteAPorta(int lado, int dip, string regra)
        {
            _s = _s with { Direcao = lado > 0 ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, regra);
            if (!Mundo(out MonitorDoDesktop m, out Superficies sup, out double escala)) return;
            double x = _s.Movimento.X;
            double ateABorda = lado > 0 ? sup.Direita - x : x - sup.Esquerda;
            // Um salto de degrau fica planejado, com a partida entre ele e a lateral (P13b): a caminhada vai até lá.
            Travessia? plano = PlanoDeTravessia(m, lado, (int)Math.Max(0, ateABorda));
            if (plano is { Tipo: TipoDeTravessia.Salto })
            {
                _s = _s with { Movimento = _s.Movimento with { Travessia = plano, Restante = double.PositiveInfinity } };
                return;
            }
            // Sem a porta plana e sem o salto, pelo transbordo (P13c): anda até a lateral, escala e transborda.
            if (plano is null)
            {
                _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true, QuerEscalar = true } };
                return;
            }
            _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true, Restante = ateABorda + dip * escala } };
        }

        /// <summary>
        /// Troca de cara da agenda, com exatamente um sorteio (DEC-027 e DEC-028). Com a onda de um item, uma das caras
        /// da fase, que tem precedência (<see cref="SortearCaraDaFase"/>). Com a emoção dominante, ela ou uma das
        /// companheiras (<see cref="SortearComADominante"/>). Na automática, uma das outras 13 caras de humor, pela lista
        /// fixa <see cref="Expressoes.DeHumor"/> (com a cara atual fora das 14, uma das 14): com a tendência da energia
        /// (<see cref="PerfilDeEnergia.PesosDasCaras"/>; DEC-036, item 6), ponderada; sem ela, uniforme, como antes.
        /// </summary>
        private Expressao SortearTrocaDeCara()
        {
            if (FaseEmVigor is { } fase) return SortearCaraDaFase(fase);
            if (_s.Preferencias.EmocaoDominante is { } dominante) return SortearComADominante(dominante);
            IReadOnlyList<Expressao> humor = Expressoes.DeHumor;
            if (Perfil.PesosDasCaras is { } tendencia)
            {
                // A cara atual sai do sorteio (peso 0): a troca sempre muda a cara, como na uniforme.
                int[] pesos = [.. humor.Select((cara, i) => cara == _s.Expressao ? 0 : tendencia[i])];
                (int escolhida, Aleatorio ap) = _s.Aleatorio.Ponderado(pesos);
                _s = _s with { Aleatorio = ap };
                return humor[escolhida];
            }
            // As 14 estão na ordem do enum: a posição de uma cara de humor na lista é o valor dela.
            int atual = Expressoes.EhDeHumor(_s.Expressao) ? (int)_s.Expressao : -1;
            (int e, Aleatorio a) = _s.Aleatorio.Entre(0, humor.Count - (atual < 0 ? 1 : 2));
            _s = _s with { Aleatorio = a };
            // Sorteia entre as outras caras: pula a atual.
            return humor[atual >= 0 && e >= atual ? e + 1 : e];
        }

        /// <summary>Pesos do sorteio com a emoção dominante: ela, 6; cada uma das quatro companheiras, 1.</summary>
        private static readonly int[] PesosDaDominante = [6, 1, 1, 1, 1];

        /// <summary>
        /// Uma cara com a emoção dominante (DEC-027), num único sorteio: ela, em 60% das vezes, ou uma das quatro
        /// companheiras (<see cref="Expressoes.Companheiras"/>). Pode repetir a cara atual: de cada 100 trocas, cerca
        /// de 36 não mudam nada, e é assim que a dominante fica a mais frequente.
        /// </summary>
        private Expressao SortearComADominante(Expressao dominante)
        {
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado(PesosDaDominante);
            _s = _s with { Aleatorio = a };
            return i == 0 ? dominante : Expressoes.Companheiras(dominante)[i - 1];
        }

        private bool Permite(AcoesAutonomas acao) => (_cfg.Acoes & acao) == acao;

        /// <summary>
        /// Escolha ponderada e reproduzível entre destinos permitidos. Com a autonomia pausada ou o
        /// painel aberto, fica com o primeiro destino, o mais calmo.
        /// </summary>
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
            // O fim do gesto acaba também a espiada na borda explorada (DEC-037, item 9).
            _s = _s with { Gesto = Gesto.Nenhum, PassosDoGesto = 0, Movimento = _s.Movimento with { ExplorandoBorda = false } };
            _reagendar = true;
        }

        // ---------------------------------------------------------------- movimento (Fase 4, DEC-022)

        /// <summary>Com a autonomia pausada ou o painel aberto, o movimento em curso termina num lugar estável.</summary>
        private bool Calmo => _s.AutonomiaPausada || _s.PainelAberto;

        /// <summary>
        /// DEC-022 com DEC-024: com a autonomia pausada ou o painel aberto, a agenda não decide, e quem está agarrado à
        /// parede ou ao cipó sem ter sido posto lá pelo usuário (depois da reação a um clique, de uma revalidação, do fim do
        /// uso de um item ou do atento) não fica esperando por ela: deixa de estar agarrado, e os passos seguintes o fazem
        /// descer pela parede ou se soltar do cipó, como a calma faz com quem escala. Preso pelo usuário, continua agarrado
        /// (DEC-024); com um item na mão do usuário, o atento o mantém onde está (DEC-028) até o item sair da mão.
        /// </summary>
        private void SoltarOAgarreComCalma()
        {
            if (!_cfg.Movimento || !Calmo || AtentoAoItem || _s.PresoPeloUsuario) return;
            if (_s.Estado is Estado.Climbing or Estado.Hanging && _s.Movimento.Agarrado)
                _s = _s with { Movimento = _s.Movimento with { Agarrado = false } };
        }

        /// <summary>+1 para a direita, −1 para a esquerda.</summary>
        private int Sentido => _s.Direcao == Direcao.Direita ? 1 : -1;

        /// <summary>Monitor atual (da topologia em cache), superfícies para o sprite e escala; falso sem lugar ou topologia.</summary>
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

        /// <summary>Pixels físicos por passo fixo, a partir de DIPs por segundo.</summary>
        private double PorPasso(double dipPorSegundo, double escala) => dipPorSegundo * escala / _cfg.PassosPorSegundo;

        /// <summary>
        /// Leva a âncora fina a (x, y): a janela usa a posição arredondada, e a posição relativa
        /// acompanha, para sobreviver a uma mudança de topologia no meio do movimento.
        /// </summary>
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
            // A travessia andando em curso é atômica (passo P13; D7): nem a calma a para no meio.
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
            // Com a onda de um item, a velocidade da fase e o cambaleio (DEC-028): o passo oscila numa onda triangular,
            // às vezes para trás; o recuo devolve distância ao percurso e nunca passa da lateral de trás.
            double passo = PorPasso(Fisica.VelocidadeAndando, escala) * Cambaleio(out bool cambaleia);
            double x = mv.X + Sentido * passo;
            int limite = Sentido > 0 ? sup.Direita : sup.Esquerda;
            bool naBorda = Sentido > 0 ? x >= limite : x <= limite;
            if (naBorda) x = limite;
            else if (cambaleia) x = Math.Clamp(x, sup.Esquerda, sup.Direita);
            _s = _s with { Movimento = mv with { Restante = mv.Restante - passo } };
            // A caminhada até a partida de um salto de degrau planejado (passo P13b): ao chegar, ele salta de lá.
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
                // Toon force (DEC-023): a lateral é parede para ele mesmo quando outro monitor encosta nela.
                if (mv.QuerEscalar)
                {
                    IrPara(Estado.Climbing, "WALKING: parede (andava até ela para escalar)");
                    // Indo ao outro monitor pelo transbordo (P13c), a intenção sobe com ele.
                    if (mv.QuerAtravessar) _s = _s with { Movimento = _s.Movimento with { QuerAtravessar = true } };
                    TalvezFoguete();
                    return;
                }
                // Numa porta plana (passo P13; C14 da crítica), a agenda sorteia entre atravessar e o caminho da parede.
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

        /// <summary>
        /// A travessia possível pela lateral <paramref name="lado"/> do monitor, ou nula (passo P13; DEC-032): com a capacidade
        /// e a preferência ligadas, sem calma e sem um item na mão do usuário, andando por uma porta plana
        /// (<see cref="Passagens.PortaPlana"/>) ou, sem ela, num salto de degrau (<see cref="Passagens.SaltoDeDegrau"/>, P13b), com
        /// a partida até <paramref name="recuoMaximo"/> px antes da lateral. Nunca para um monitor fechado (<see cref="Fechado"/>).
        /// </summary>
        private Travessia? PlanoDeTravessia(MonitorDoDesktop m, int lado, int recuoMaximo = 0)
        {
            if (!_cfg.Travessia || !_s.Preferencias.AtravessarMonitores || Calmo || AtentoAoItem || _s.Topologia is not { } t) return null;
            Func<string, bool>? fechados = Fechados;
            if (Passagens.PortaPlana(t, m, lado, _cfg.Tamanho, fechados) is { } porta)
                return new Travessia(TipoDeTravessia.Andando, m.Chave, porta.ChaveVizinho, lado, porta.Borda);
            return Passagens.SaltoDeDegrau(t, m, lado, _cfg.Tamanho, _cfg.Fisica, _cfg.PassosPorSegundo, recuoMaximo, fechados);
        }

        /// <summary>
        /// O transbordo possível na lateral em que ele escala (P13c), se os pés passaram, de <paramref name="yAntes"/> a
        /// <paramref name="yDepois"/>, subindo, pela altura do chão de um vizinho mais alto daquela lateral; ou nulo. Com as
        /// mesmas guardas da travessia (<see cref="PlanoDeTravessia"/>).
        /// </summary>
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

        /// <summary>
        /// Se há um transbordo possível pela lateral <paramref name="lado"/> do monitor (P13c): algum vizinho mais alto daquela
        /// lateral cujo chão ele alcança escalando.
        /// </summary>
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

        /// <summary>
        /// O salto de degrau começa (P13b): JUMPING, com o plano, que o voo segue passo a passo (<see cref="PassoNoSalto"/>). O
        /// plano entra depois de <see cref="IrPara"/>, que recomeça o movimento parado ao entrar num estado de movimento.
        /// </summary>
        private void Saltar(Travessia salto, string regra = "WALKING: degrau alcançável (salto de travessia)")
        {
            IrPara(Estado.Jumping, regra);
            _s = _s with { Movimento = _s.Movimento with { Travessia = salto with { Passo = 0 } } };
        }

        /// <summary>
        /// Um passo do salto de degrau (P13b; D6 e D8): a posição analítica do arco; o monitor da âncora troca na borda, com o
        /// tamanho do novo monitor. No último passo, o pouso exato no chão do vizinho, e o contato com o chão leva a LANDING. No pulo
        /// da tela cheia (DEC-035), o monitor de cada passo é o da âncora, e a chegada é uma acomodação (<see cref="ChegarDoPulo"/>).
        /// </summary>
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
            // No pulo da tela cheia, a velocidade do arco vai ao estado, para a pose esticar quando ele vai rápido (DEC-035).
            if (salto.Pulo != PuloDaTelaCheia.Nenhum)
                _s = _s with { Movimento = _s.Movimento with { VX = salto.VX, VY = salto.VY0 + salto.G * passo / _cfg.PassosPorSegundo } };
        }

        /// <summary>
        /// Na porta, um sorteio entre atravessar, com <see cref="PerfilDeEnergia.PesoAtravessar"/>, e o caminho da parede, com a
        /// soma dos pesos dele (parar, escalar e virar; <see cref="Sinalizar"/>), que sorteia de novo entre os três: a mesma
        /// distribuição de um sorteio só, com o código da parede intacto.
        /// </summary>
        private bool SorteiaAtravessar()
        {
            int atravessar = Perfil.PesoAtravessar;
            if (atravessar <= 0) return false;
            int parede = 3 + (Permite(AcoesAutonomas.Escalar) ? Perfil.PesoEscalar : 0) + 2;
            (int i, Aleatorio a) = _s.Aleatorio.Ponderado([atravessar, parede]);
            _s = _s with { Aleatorio = a };
            return i == 0;
        }

        /// <summary>
        /// Um passo da travessia andando (4.1 e D8): a âncora fina anda reta, sem o cambaleio, na velocidade do monitor da âncora,
        /// e troca de monitor quando, arredondada, passa da borda; os pés ficam no chão (o mesmo dos dois lados). Ela termina com o
        /// sprite inteiro no destino; aí, com calma, com um item na mão do usuário ou sem percurso, ele para.
        /// </summary>
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
            // Pausado ou com o painel aberto, desce até o chão em vez de subir (e o foguete apaga).
            int sentido = Calmo ? 1 : mv.SentidoVertical;
            bool foguete = mv.Foguete && sentido < 0;
            double velocidade = foguete ? _cfg.Fisica.VelocidadeDoFoguete : Fisica.VelocidadeEscalando;
            double y = mv.Y + sentido * PorPasso(velocidade, escala);
            double x = Sentido > 0 ? sup.Direita : sup.Esquerda;
            _s = _s with { Movimento = mv with { SentidoVertical = sentido, Foguete = foguete } };
            // Subindo, os pés cruzaram neste passo a altura do chão de um vizinho mais alto (o topo do trecho de parede abaixo
            // da porta): o transbordo para ele (P13c; C14 da crítica), sem sorteio se ele ia ao outro monitor.
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
                // Pendurado, segue pela borda para dentro, de costas para a parede.
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

        /// <summary>
        /// Preso pelo usuário na parede (DEC-024): percorre o passeio sorteado pela mesma lateral, sem
        /// chegar ao chão nem passar para o cipó, e para agarrado no fim, num limite ou com calma.
        /// </summary>
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

        /// <summary>
        /// Preso pelo usuário no cipó (DEC-024): percorre o passeio sorteado pela borda de cima; numa
        /// quina dá meia-volta em vez de descer; para agarrado no fim ou com calma.
        /// </summary>
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

        /// <summary>Pulo ou queda: gravidade com velocidade máxima, integração semi-implícita, até o chão.</summary>
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
            // O sprite nunca sai da área útil: as laterais e a borda de cima param o voo.
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

        /// <summary>
        /// Toon force (DEC-023): um impacto forte no chão quica como borracha, rindo, em vez de
        /// pousar. A cada quique sobra uma fração da velocidade; depois de
        /// <see cref="ParametrosDeMovimento.QuiquesMaximos"/>, ou com a autonomia pausada ou o
        /// painel aberto, ele pousa. Devolve se quicou.
        /// </summary>
        private bool Quicar(MonitorDoDesktop m, double x, int chao, double vx, double vy, double escala)
        {
            ParametrosDeMovimento f = _cfg.Fisica;
            int quiques = _s.Movimento.Quiques;
            if (Calmo || f.RestituicaoDoQuique <= 0 || quiques >= f.QuiquesMaximos || vy < f.ImpactoMinimoDoQuique * escala) return false;

            string de = _s.Estado.ToString().ToUpperInvariant();
            _s = _s with { Expressao = Expressao.Rindo };
            IrPara(Estado.Jumping, $"{de}: contato com o chão, quique de borracha (toon force)");
            // IrPara recomeça o movimento parado quando o estado muda: a velocidade vem depois.
            _s = _s with { Movimento = _s.Movimento with { VX = vx * f.AtritoDoQuique, VY = -vy * f.RestituicaoDoQuique, Quiques = quiques + 1 } };
            MoverPara(m, x, chao);
            return true;
        }

        /// <summary>
        /// Toon force (DEC-023): ao começar a subir uma parede a partir do chão, às vezes dispara
        /// parede acima num foguete de borracha, até a borda superior. A chance é do perfil de
        /// energia (frequência de uma ação); a velocidade é a mesma em todo nível.
        /// </summary>
        private void TalvezFoguete()
        {
            if (!_cfg.Movimento || _s.Estado != Estado.Climbing || Calmo || _cfg.Fisica.VelocidadeDoFoguete <= 0) return;
            (int sorteio, Aleatorio a) = _s.Aleatorio.Entre(1, 100);
            _s = _s with { Aleatorio = a };
            if (sorteio <= Perfil.ChanceDoFoguete)
                _s = _s with { Movimento = _s.Movimento with { Foguete = true } };
        }

        /// <summary>Explorar a borda (DEC-037, item 9): uma caminhada em cada <see cref="PerfilDeEnergia.ExplorarBordaUmEm"/>, no gerador da personalidade.</summary>
        private bool SorteiaExplorarBorda(PerfilDeEnergia perfil)
        {
            (int n, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(1, Math.Max(1, perfil.ExplorarBordaUmEm));
            _s = _s with { AleatorioDaPersonalidade = a };
            return n == 1;
        }

        /// <summary>
        /// Explorar a borda (DEC-037, item 9): anda até a lateral mais próxima da área útil, sem atravessar nem escalar, e lá
        /// espia para fora (<see cref="ChegarNaBordaExplorada"/>).
        /// </summary>
        private void PlanejarExploracaoDaBorda()
        {
            if (!Mundo(out _, out Superficies sup, out _) || _s.Lugar is null) return;
            double x = _s.Lugar.Ancora.X;
            double ateDireita = sup.Direita - x, ateEsquerda = x - sup.Esquerda;
            _s = _s with { Direcao = ateDireita <= ateEsquerda ? Direcao.Direita : Direcao.Esquerda };
            IrPara(Estado.Walking, "IDLE + AUTONOMY_TIMER: explorar a borda");
            _s = _s with { Movimento = _s.Movimento with { ExplorandoBorda = true, Restante = Math.Min(ateDireita, ateEsquerda) + 1 } };
        }

        /// <summary>Na lateral explorada (DEC-037, item 9): para, virado para fora, e espia, com a duração do perfil no gerador da personalidade.</summary>
        private void ChegarNaBordaExplorada()
        {
            PerfilDeEnergia perfil = Perfil;
            (int passos, Aleatorio a) = _s.AleatorioDaPersonalidade.Entre(perfil.PassosDoGestoMinimo, perfil.PassosDoGestoMaximo);
            IrPara(Estado.Idle, "WALKING: borda explorada (espia para fora)");
            _s = _s with { AleatorioDaPersonalidade = a, Gesto = Gesto.Espiar, PassosDoGesto = passos };
        }

        /// <summary>Caminhada autônoma: distância do perfil de energia; sem espaço à frente, vira.</summary>
        private void PlanejarCaminhada(PerfilDeEnergia perfil)
        {
            if (!Mundo(out _, out Superficies sup, out double escala)) return;
            (int dip, Aleatorio a) = _s.Aleatorio.Entre(perfil.DistanciaAndandoMinima, perfil.DistanciaAndandoMaxima);
            _s = _s with { Aleatorio = a };
            double x = _s.Movimento.X;
            double livre = Sentido > 0 ? sup.Direita - x : x - sup.Esquerda;
            double atras = Sentido > 0 ? x - sup.Esquerda : sup.Direita - x;
            // Com uma porta plana à frente (passo P13), o espaço continua do outro lado: não vira.
            if (Mundo(out MonitorDoDesktop m, out _, out _) && PlanoDeTravessia(m, Sentido) is not null) livre = double.PositiveInfinity;
            if (livre < _cfg.Fisica.EspacoMinimo * escala && atras > livre) Virar();
            _s = _s with { Movimento = _s.Movimento with { Restante = dip * escala } };
        }

        /// <summary>
        /// Escalar a partir de IDLE: já encostado numa lateral, sobe; senão, anda até a lateral
        /// mais próxima e sobe quando chegar. Com a toon force (DEC-023), as duas laterais servem,
        /// mesmo a que encosta em outro monitor.
        /// </summary>
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

        /// <summary>
        /// Pulo autônomo: arco balístico com distância e altura do perfil de energia, calculado
        /// para pousar no chão; sem espaço à frente, pula para o outro lado.
        /// </summary>
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

        /// <summary>Expressões de quem está preso pelo usuário e só olha em volta (DEC-024).</summary>
        private static readonly Expressao[] ExpressoesDoPreso = [Expressao.Feliz, Expressao.Curioso, Expressao.Travesso, Expressao.Rindo, Expressao.Pensativo];

        /// <summary>
        /// A cara de quem está preso e olha em volta, num único sorteio: com a onda de um item, uma das caras da fase
        /// (DEC-028); com a emoção dominante, ela ou uma companheira (DEC-027); na automática, uma das caras de quem está
        /// preso.
        /// </summary>
        private Expressao SortearCaraDoPreso()
        {
            if (FaseEmVigor is { } fase) return SortearCaraDaFase(fase);
            if (_s.Preferencias.EmocaoDominante is { } dominante) return SortearComADominante(dominante);
            (int cara, Aleatorio a) = _s.Aleatorio.Entre(0, ExpressoesDoPreso.Length - 1);
            _s = _s with { Aleatorio = a };
            return ExpressoesDoPreso[cara];
        }

        /// <summary>
        /// Preso pelo usuário (DEC-024): a agenda nunca o tira de lá. Ou ele fica, trocando de cara,
        /// ou passeia um pouco pela mesma superfície: sobe ou desce pela parede, vai para um lado ou
        /// outro pelo cipó. O passeio para agarrado, e o relógio desliga.
        /// </summary>
        private void DecidirPreso()
        {
            bool naParede = _s.Estado == Estado.Climbing;
            (int escolha, Aleatorio a) = _s.Aleatorio.Ponderado([2, 2, 2]);
            (int dip, Aleatorio a2) = a.Entre(_cfg.Fisica.PasseioPresoMinimo, _cfg.Fisica.PasseioPresoMaximo);
            _s = _s with { Aleatorio = a2 };
            // A cara é sorteada sempre, num único sorteio, e só vale quando ele fica e olha em volta.
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

        /// <summary>A agenda decidiu pular da parede: salta para longe dela, de costas para a parede.</summary>
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

        /// <summary>A agenda decidiu pendurado: continua pela borda, desce pela parede da quina, salta ou solta.</summary>
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

        /// <summary>
        /// <c>SETTLING</c> (ARCHITECTURE.md 2.7, passos 4 e 5): escolhe o monitor da âncora (ou o mais
        /// próximo, num vão), prende o sprite na área útil dele e decide pelo apoio. Sem apoio, cai
        /// (<see cref="ConfiguracaoDoNucleo.QuedaFisica"/>) ou, antes da Fase 4, vai direto ao chão.
        /// </summary>
        /// <param name="pelaMaoDoUsuario">
        /// O usuário acabou de soltar o personagem (DRAG_END, DRAG_CANCEL do arraste). Se ele agarrar
        /// uma lateral ou o cipó, fica preso lá até o usuário tirá-lo (DEC-024).
        /// </param>
        /// <param name="apoio">
        /// O apoio em que ele usou um item (DEC-028): no fim do uso, na quina, ao alcance da parede e do cipó, ele agarra o
        /// mesmo de antes. Nulo, o mais próximo, como sempre.
        /// </param>
        private void Acomodar(PontoPx desejada, string regra, PosicaoDoPersonagem? preferida = null, bool pelaMaoDoUsuario = false, ApoioDoUso? apoio = null)
        {
            IrPara(Estado.Settling, regra);
            (Posicionamento lugar, PosicaoDoPersonagem posicao, bool comApoio) = Validar(desejada, preferida);
            _s = _s with { Lugar = lugar, Posicao = posicao };
            _reagendar = true;

            // Escondido (DEC-025): toda acomodação o devolve ao esconderijo, na mesma borda.
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum)
            {
                Posicionamento escondido = EsconderijoPara(lugar, _s.Esconderijo);
                _s = _s with { Lugar = escondido, Posicao = Posicionador.Descrever(escondido) };
                IrPara(Estado.Peeking, $"SETTLING: escondido atrás da borda ({_s.Esconderijo})");
                return;
            }

            // Solto no alto ou junto a uma lateral, agarra ali em vez de cair (DEC-024). Quem já
            // estava preso pelo usuário continua preso: um clique ou uma revalidação não o tiram.
            if (!comApoio && _cfg.Movimento && OndeAgarrar(lugar, apoio) is { } agarre)
            {
                bool preso = pelaMaoDoUsuario || _s.PresoPeloUsuario;
                _s = _s with { Lugar = agarre.Lugar, Posicao = Posicionador.Descrever(agarre.Lugar), Direcao = agarre.Direcao, PresoPeloUsuario = preso };
                IrPara(agarre.Estado, agarre.Estado == Estado.Hanging
                    ? "SETTLING: solto perto da borda de cima, agarra o cipó"
                    : "SETTLING: solto junto a uma lateral, fica grudado na parede");
                // IrPara recomeça o movimento quando o estado muda: parado, agarrado, sem relógio.
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

        /// <summary>
        /// Onde agarrar um personagem sem apoio (DEC-024): o cipó da borda de cima, se o topo do sprite
        /// está perto dela; a lateral, se a âncora está perto dela; a mais próxima das duas, em
        /// proporção ao alcance de cada uma, ou, com as duas ao alcance, a <paramref name="preferido"/>
        /// (o apoio do uso de um item, DEC-028). Perto do chão, nenhuma: ele cai. A exceção é o fim do
        /// uso de um item na parede ou no cipó: ele já estava lá, e volta ao mesmo apoio mesmo perto do
        /// chão (tabela 4.6 e invariante 24), como quem escalava os primeiros passos de uma parede.
        /// </summary>
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

        /// <summary>
        /// A validação de SETTLING sem transição: monitor da âncora (ou o mais próximo), sprite
        /// preso na área útil, apoio no chão e posição relativa resultante.
        /// </summary>
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

        /// <summary>
        /// Esconder ou sair no meio do arraste encerra o gesto onde ele está, como um cancelamento
        /// (ARCHITECTURE.md 2.7): a posição do cursor é validada e passa a ser a escolha do usuário.
        /// </summary>
        private void FixarArrasteInterrompido()
        {
            if (_s.Estado != Estado.Dragging || _s.Lugar is null || _s.Topologia is null) return;
            (Posicionamento lugar, PosicaoDoPersonagem posicao, _) = Validar(_s.Lugar.Ancora, null);
            _s = _s with { Lugar = lugar, Posicao = posicao, RetornoDaTelaCheia = null };
        }

        /// <summary>Posicionamento durante o arraste: segue a âncora sem prender, com o tamanho do DPI do monitor dela.</summary>
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
            // Sair de USING, pelo fim ou por uma interrupção, acaba o uso (DEC-028); a onda continua.
            if (de == Estado.Using && novo != Estado.Using) _s = _s with { Uso = null };
            // Sair de REACTING acaba a variante da reação (DEC-037, item 8).
            if (de == Estado.Reacting && novo != Estado.Reacting) _s = _s with { Reacao = VarianteDaReacao.Padrao };
            // Sair da caminhada desfaz a travessia em curso (passo P13; C15 da crítica).
            if (novo != de && _s.Movimento.Travessia is not null) _s = _s with { Movimento = _s.Movimento with { Travessia = null } };
            if (novo != de && DecideNoEstado(novo)) _reagendar = true;
            // Ao entrar num estado de movimento, a física parte da âncora atual, parada; quem
            // chamou ajusta velocidade e plano depois (DEC-022).
            if (novo != de && novo.EmMovimento() && _s.Lugar is { } lugar)
                _s = _s with { Movimento = new EstadoDoMovimento(lugar.Ancora.X, lugar.Ancora.Y, 0, 0, double.PositiveInfinity, -1, false) };
        }

        // ---------------------------------------------------------------- efeitos

        internal Resultado Concluir()
        {
            var janela = new List<Efeito>();
            var tempo = new List<Efeito>();

            // Com calma, quem está agarrado sem estar preso desce ou se solta (DEC-022), antes de o relógio ser decidido.
            SoltarOAgarreComCalma();

            // Um item que cai e deixou de aparecer vai direto ao chão (L5): o relógio nunca corre por ele.
            AssentarOsInvisiveis();

            bool visivelAntes = _inicio.Estado.Visivel();
            bool visivelDepois = _s.Estado.Visivel();
            if (_s.Estado != Estado.Exiting)
            {
                if (visivelDepois && _s.Lugar is not null && (!visivelAntes || !Equals(_inicio.Lugar, _s.Lugar)))
                    janela.Add(new MoverJanela(_s.Lugar));
                if (!visivelAntes && visivelDepois) janela.Add(new MostrarJanela());
                if (visivelAntes && !visivelDepois) janela.Add(new EsconderJanela());
                // As janelas dos itens (DEC-028), depois da do personagem; saindo, a raiz fecha todas.
                EfeitosDosItens(janela);
            }

            // Relógio: só com movimento, reação, uso de um item, pouso, gesto ou um item à vista caindo (DEC-011;
            // critério 3 da Fase 2; invariante 29). Agarrado à parede ou ao cipó (DEC-024), nada se move: o relógio fica
            // desligado.
            bool agarrado = _s.Estado is Estado.Climbing or Estado.Hanging && _s.Movimento.Agarrado;
            bool relogio = (_s.Estado.EmMovimento() && !agarrado) || _s.Estado is Estado.Reacting or Estado.Using
                || (_s.Estado == Estado.Idle && _s.Gesto != Gesto.Nenhum) || ItemVisivelCaindo;
            if (relogio != _s.RelogioAtivo)
            {
                tempo.Add(relogio ? new LigarRelogio() : new DesligarRelogio());
                _s = _s with { RelogioAtivo = relogio };
            }

            // Agenda autônoma: um temporizador único até a próxima decisão. Com o usuário segurando um item
            // (DEC-028), a agenda pausa; quando o item sai da mão, volta depois do intervalo de acomodação.
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

            // Onda do tamagotchi (DEC-028): o próprio temporizador único, depois do da agenda. Antes, sem onda de substância, a
            // carga da paranoia volta a 0 (pedido do usuário de 2026-10-01).
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
                // Fase 4: tempo na parede e tempo pendurado ("por pouco tempo") do perfil de energia.
                Estado.Climbing when _cfg.Movimento => (perfil.TempoNaParedeMinimo, perfil.TempoNaParedeMaximo),
                Estado.Hanging when _cfg.Movimento => (perfil.TempoPenduradoMinimo, perfil.TempoPenduradoMaximo),
                _ => (perfil.DecisaoMinima, perfil.DecisaoMaxima),
            };
            (TimeSpan atraso, Aleatorio a) = _s.Aleatorio.Duracao(minimo, maximo);
            _s = _s with { Aleatorio = a };
            // Depois de uma interação (soltar, fechar o painel, retomar a autonomia, em qualquer
            // estado que decide), a autonomia só volta após o intervalo de acomodação; é também o
            // menor intervalo entre duas decisões autônomas.
            return atraso < _cfg.IntervaloDeAcomodacao ? _cfg.IntervaloDeAcomodacao : atraso;
        }
    }
}
