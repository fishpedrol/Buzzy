namespace Buzzy.Core.Personagem;

/// <summary>
/// Os itens do tamagotchi adulto (DEC-028; desenho do núcleo, 4.6 e 4.8, com a crítica de integração), atrás da chave
/// <see cref="ConfiguracaoDoNucleo.Tamagotchi"/>: com ela desligada, os eventos dos itens são descartados antes de tudo e
/// nenhum efeito novo sai do núcleo. O item é uma entidade do núcleo: nasce pelo menu ao lado do personagem, cai com a
/// gravidade dele e quica uma vez, é segurado e arrastado pelo usuário e, solto sobre o personagem num estado que aceita,
/// é usado (<see cref="Estado.Using"/>). O app só desenha as janelas dos itens a partir dos efeitos. O outro uso é o do
/// baseado por conta própria (pedido do usuário de 2026-10-01, 19:10; <see cref="AcoesAutonomas.FumarBaseado"/>): a agenda,
/// em IDLE no chão, às vezes o faz fumar um baseado sozinho, sem item no mundo, pelo mesmo caminho do uso.
/// </summary>
public static partial class Maquina
{
    /// <summary>
    /// Os estados em que o personagem aceita um item solto sobre ele (tabela 4.6): parado, andando, na parede, no cipó,
    /// descansando, reagindo, pousando e escondido na borda. Pulando, caindo, usando outro item, pressionado ou
    /// arrastado, o soltar é recusado e o item cai de onde foi solto; acomodando, escondido, na partida e saindo, também.
    /// </summary>
    public static bool AceitaItem(Estado estado)
        => estado is Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Resting
            or Estado.Reacting or Estado.Landing or Estado.Peeking;

    /// <summary>
    /// Se um item solto está "sobre o personagem" (C14 da crítica): o retângulo do item, já preso na área útil, cruza o
    /// retângulo do sprite do personagem encolhido <paramref name="margemPercentual"/>% de cada lado. O encolhimento de
    /// cada lado é a margem da largura (nas laterais) e da altura (em cima e embaixo), arredondada ao pixel com a metade
    /// para cima: com 20% num sprite de 128 × 128 px, 26 px de cada lado, um miolo de 76 × 76 px. Cruzar é ter ao menos um
    /// pixel em comum, com os retângulos semiabertos, como o RECT do Windows. O núcleo não conhece a transparência dos
    /// quadros: o miolo do sprite cobre o corpo em todas as poses, e um item solto só no canto transparente não conta. A
    /// margem vai de 0 a 50%; com 50%, o miolo fica vazio e nada está sobre ele.
    /// </summary>
    public static bool SobreOPersonagem(RetanguloPx item, RetanguloPx personagem, int margemPercentual)
    {
        if (margemPercentual is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(margemPercentual), margemPercentual, "A margem do alvo vai de 0 a 50%.");
        int dx = (personagem.Largura * margemPercentual + 50) / 100;
        int dy = (personagem.Altura * margemPercentual + 50) / 100;
        var miolo = new RetanguloPx(personagem.Esquerda + dx, personagem.Topo + dy, personagem.Direita - dx, personagem.Base - dy);
        return miolo.Intersecta(item);
    }

    /// <summary>
    /// Se a janela do item aparece (L4 da crítica e D18): o item na mão do usuário sempre aparece, para o gesto nunca
    /// sumir no meio; fora da mão, só com o personagem à vista e fora de um monitor ocupado pela tela cheia (com o modo
    /// ligado), para não cobrir um aplicativo em tela cheia (Q-09).
    /// </summary>
    public static bool ItemVisivel(EstadoDoNucleo s, ItemNoMundo item)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(item);
        return item.NaMao || (s.Estado.Visivel() && !(s.Preferencias.ModoTelaCheia && s.Ocupados.Contem(item.Lugar.Monitor.Chave)));
    }

    /// <summary>Os eventos do tamagotchi: com a chave desligada, a máquina os descarta antes de tudo (L15).</summary>
    private static bool EhDoTamagotchi(Evento evento)
        => evento is CmdSummonItem or CmdClearItems or ItemPress or ItemDragStart or ItemDragMove or ItemDragEnd or ItemRelease or ItemEffectTimer;

    private sealed partial class Passo
    {
        /// <summary>Se o usuário segura um item e isso vale: com o tamagotchi desligado, não há itens.</summary>
        private bool AtentoAoItem => _cfg.Tamagotchi && _s.Atento;

        /// <summary>Se algum item visível está caindo: o relógio corre por ele (invariante 29).</summary>
        private bool ItemVisivelCaindo => _cfg.Tamagotchi && _s.Itens.Todos.Any(i => i.Situacao == SituacaoDoItem.Caindo && ItemVisivel(_s, i));

        // ---------------------------------------------------------------- menu

        /// <summary>
        /// CMD_SUMMON_ITEM (4.8): o item nasce ao lado do personagem, primeiro do lado para onde ele olha, acima do chão, e
        /// cai. Com <see cref="ConfiguracaoDoNucleo.MaximoDeItens"/> itens, antes sai o de menor Id que não está na mão
        /// (D17). Parado e sem onda, ele fica empolgado. Antes da carga, escondido ou com um item fora do enum, nada.
        /// </summary>
        private void InvocarItem(Item item)
        {
            if (!Enum.IsDefined(item) || !_s.Carregado || !_s.Estado.Visivel() || _s.Topologia is null || _s.Lugar is null || _cfg.MaximoDeItens < 1) return;
            // Um item fora da edição (DEC-044, item 2) não existe aqui.
            if (!_cfg.ItensDaEdicao.Contem(item)) return;
            // A chave geral e a seleção individual (DEC-033/041) são ambas exigidas para invocar um item adulto.
            if (ItemAdulto(item) && (!_s.Preferencias.ConteudoAdulto || !_s.Preferencias.ItensAdultosHabilitados.Contem(item))) return;
            while (_s.Itens.Quantidade >= _cfg.MaximoDeItens)
            {
                if (_s.Itens.Todos.FirstOrDefault(i => !i.NaMao) is not { } maisAntigo) return;
                _s = _s with { Itens = _s.Itens.Sem(maisAntigo.Id) };
                _removidos[maisAntigo.Id] = MotivoDaRemocao.Substituido;
            }
            (Posicionamento lugar, double y) = LugarDeNascimento();
            int id = _s.ProximoIdDeItem;
            var novo = new ItemNoMundo(id, item, SituacaoDoItem.Caindo, lugar, Posicionador.Descrever(lugar)) { Y = y };
            _s = _s with { Itens = _s.Itens.Com(novo), ProximoIdDeItem = id + 1 };
            if (_s.Estado == Estado.Idle && !ComOnda) _s = _s with { Expressao = Expressao.Empolgado };
        }

        /// <summary>CMD_CLEAR_ITEMS: todos os itens saem; o da mão do usuário solta a captura antes (L6).</summary>
        private void RecolherItens()
        {
            if (_s.Itens.Quantidade == 0) return;
            if (_s.Itens.NaMao is { } naMao) _antes.Add(new LiberarCapturaDoItem(naMao.Id));
            foreach (ItemNoMundo i in _s.Itens.Todos) _removidos[i.Id] = MotivoDaRemocao.Recolhido;
            _s = _s with { Itens = ItensNoMundo.Nenhum };
        }

        // ---------------------------------------------------------------- gestos sobre um item

        /// <summary>
        /// ITEM_PRESS: o item visível fica na mão do usuário, com a pegada (cursor menos âncora), e o personagem fica atento
        /// (<see cref="FicarAtento"/>). Outro item que estivesse na mão é largado antes, de onde estava.
        /// </summary>
        private void PegarItem(int id, PontoPx cursor)
        {
            if (_s.Topologia is null || _s.Itens.PorId(id) is not { } item || !ItemVisivel(_s, item)) return;
            if (_s.Itens.NaMao is { } outro && outro.Id != id)
            {
                _antes.Add(new LiberarCapturaDoItem(outro.Id));
                _s = _s with { Itens = _s.Itens.Com(Solto(outro, outro.Lugar.Ancora)) };
            }
            var pegada = new PontoPx(cursor.X - item.Lugar.Ancora.X, cursor.Y - item.Lugar.Ancora.Y);
            _s = _s with { Itens = _s.Itens.Com(item with { Situacao = SituacaoDoItem.Segurado, Pegada = pegada, VY = 0 }) };
            FicarAtento();
        }

        /// <summary>ITEM_DRAG_START: o item segurado passa a ser arrastado.</summary>
        private void IniciarArrasteDoItem(int id)
        {
            if (_s.Itens.NaMao is { Situacao: SituacaoDoItem.Segurado } item && item.Id == id)
                _s = _s with { Itens = _s.Itens.Com(item with { Situacao = SituacaoDoItem.Arrastado }) };
        }

        /// <summary>ITEM_DRAG_MOVE: a âncora do item é o cursor menos a pegada, sem prender, como o personagem (invariante 2).</summary>
        private void ArrastarItem(int id, PontoPx cursor)
        {
            if (_s.Itens.NaMao is not { Situacao: SituacaoDoItem.Arrastado } item || item.Id != id || _s.Topologia is not { } topologia) return;
            var ancora = new PontoPx(cursor.X - item.Pegada.X, cursor.Y - item.Pegada.Y);
            MonitorDoDesktop m = MonitorDaAncora(topologia, ancora);
            _s = _s with { Itens = _s.Itens.Com(item with { Lugar = LugarDoItem(m, ancora), Y = ancora.Y }) };
        }

        /// <summary>
        /// ITEM_DRAG_END: a âncora é presa na área útil. Sobre o personagem (<see cref="SobreOPersonagem"/>), num estado que
        /// aceita (<see cref="AceitaItem"/>), ele usa o item; senão, o item cai de onde foi solto, ou fica, se já está no chão.
        /// Sem o ITEM_DRAG_START antes (o árbitro sempre o manda; é robustez), o fim do gesto larga o item segurado de onde
        /// ele está, como o ITEM_RELEASE: ele sai da mão, o personagem deixa de estar atento e o item nunca é usado.
        /// </summary>
        private void SoltarItem(int id, PontoPx cursor)
        {
            if (_s.Itens.NaMao is { Situacao: SituacaoDoItem.Segurado } segurado && segurado.Id == id)
            {
                LargarItem(id);
                return;
            }
            if (_s.Itens.NaMao is not { Situacao: SituacaoDoItem.Arrastado } item || item.Id != id || _s.Topologia is null) return;
            ItemNoMundo solto = Solto(item, new PontoPx(cursor.X - item.Pegada.X, cursor.Y - item.Pegada.Y));
            if (_s.Lugar is { } lugar && AceitaItem(_s.Estado) && SobreOPersonagem(solto.Lugar.Retangulo, lugar.Retangulo, _cfg.MargemDoAlvo))
            {
                UsarItem(solto);
                return;
            }
            _s = _s with { Itens = _s.Itens.Com(solto) };
        }

        /// <summary>ITEM_RELEASE (clique, clique duplo ou captura perdida): o item cai de onde está e nunca é usado.</summary>
        private void LargarItem(int id)
        {
            if (_s.Itens.NaMao is not { } item || item.Id != id || _s.Topologia is null) return;
            _s = _s with { Itens = _s.Itens.Com(Solto(item, item.Lugar.Ancora)) };
        }

        /// <summary>
        /// O item sai da mão em <paramref name="desejada"/>, presa na área útil do monitor dela: no chão, fica; no ar, cai
        /// do zero, podendo quicar de novo.
        /// </summary>
        private ItemNoMundo Solto(ItemNoMundo item, PontoPx desejada)
        {
            MonitorDoDesktop m = MonitorDaAncora(_s.Topologia!, desejada);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(desejada, TamanhoDoItem(m), m.AreaUtil);
            Posicionamento lugar = LugarDoItem(m, presa);
            SituacaoDoItem situacao = presa.Y == m.AreaUtil.Base ? SituacaoDoItem.NoChao : SituacaoDoItem.Caindo;
            return item with { Situacao = situacao, Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = presa.Y, VY = 0, Quiques = 0, Pegada = default };
        }

        /// <summary>
        /// Atento (C18 da crítica): enquanto o usuário segura um item, a agenda pausa (<see cref="Concluir"/>) e ele para
        /// onde está, para o item poder ser solto nele. Andando, para; descansando, acorda; na parede e no cipó, fica
        /// agarrado, e o foguete apaga (L7); pulo e queda seguem até o chão. Sem onda, olha curioso.
        /// </summary>
        private void FicarAtento()
        {
            bool acordou = false;
            switch (_s.Estado)
            {
                // Atravessando (passo P13), espera a travessia acabar, como a pausa: o fim dela o para.
                case Estado.Walking when _s.Movimento.Travessia is not { Tipo: TipoDeTravessia.Andando }:
                    IrPara(Estado.Idle, "ITEM_PRESS: para e olha o item");
                    break;
                case Estado.Resting:
                    _s = _s with { Sinal = Sinal.Acordou };
                    IrPara(Estado.Idle, "ITEM_PRESS: acorda e olha o item");
                    acordou = true;
                    break;
                case Estado.Climbing or Estado.Hanging when _cfg.Movimento:
                    _s = _s with { Movimento = _s.Movimento with { Agarrado = true, Foguete = false } };
                    break;
            }
            if (!ComOnda && _s.Estado is Estado.Idle or Estado.Climbing or Estado.Hanging or Estado.Peeking)
                _s = _s with { Expressao = Expressao.Curioso };
            else if (acordou)
                _s = _s with { Expressao = CaraDeBase() };
        }

        // ---------------------------------------------------------------- uso (USING)

        /// <summary>
        /// Ele usa o item solto sobre ele (4.6): o item sai (<see cref="MotivoDaRemocao.Usado"/>) e o uso começa no apoio em
        /// que ele está (<see cref="ComecarOUso"/>). Descansando, acorda antes; andando, reagindo ou pousando, o que fazia é
        /// cortado.
        /// </summary>
        private void UsarItem(ItemNoMundo item)
        {
            DadosDoItem dados = _cfg.TabelaDeItens(item.Item);
            ApoioDoUso apoio = ApoioAtual();
            _s = _s with { Itens = _s.Itens.Sem(item.Id) };
            _removidos[item.Id] = MotivoDaRemocao.Usado;
            if (_s.Estado == Estado.Resting) _s = _s with { Sinal = Sinal.Acordou };
            ComecarOUso(item.Item, dados, apoio, $"ITEM_DRAG_END sobre o personagem: {dados.Verbo} {item.Item}");
        }

        /// <summary>
        /// Se ele pode fumar um baseado por conta própria agora (<see cref="AcoesAutonomas.FumarBaseado"/>; pedido do usuário
        /// de 2026-10-01, 19:10): só com a chave do tamagotchi ligada, em IDLE, no chão (a âncora na borda de baixo da área
        /// útil do monitor dele), com o conteúdo adulto ligado e o baseado selecionado (DEC-033/041), sem estar escondido, com a autonomia livre, sem item na mão do usuário e sem a onda Chapado
        /// ou a paranoia na frente, para ele não emendar. A agenda só decide visível, com a autonomia livre e sem item na mão;
        /// a regra repete as três condições para valer sozinha.
        /// </summary>
        private bool PodeFumarPorContaPropria
            => _cfg.Tamagotchi && _s.Preferencias.ConteudoAdulto && _s.Preferencias.ItensAdultosHabilitados.Contem(Item.Baseado)
                && _s.Estado == Estado.Idle && _s.Esconderijo == LadoDoEsconderijo.Nenhum
                && !_s.AutonomiaPausada && !_s.PainelAberto && !AtentoAoItem
                && _s.Lugar is { } lugar && lugar.Ancora.Y == lugar.Monitor.AreaUtil.Base
                && !(ComOnda && _s.Onda!.Tipo is Onda.Chapado or Onda.Paranoico);

        /// <summary>
        /// A agenda escolheu o baseado por conta própria (<see cref="PodeFumarPorContaPropria"/>): ele "tira do chapéu" um
        /// baseado, sem item no mundo (nada nasce, nada sai e nenhum Id é gasto), e o usa no chão, com o uso do baseado da
        /// tabela (<see cref="ConfiguracaoDoNucleo.TabelaDeItens"/>), pelo mesmo caminho do baseado que o usuário solta nele
        /// (<see cref="ComecarOUso"/>): a combinação, o alívio, a carga e o sorteio da paranoia.
        /// </summary>
        private void FumarPorContaPropria()
        {
            DadosDoItem dados = _cfg.TabelaDeItens(Item.Baseado);
            ComecarOUso(Item.Baseado, dados, ApoioDoUso.Chao, $"IDLE + AUTONOMY_TIMER: {dados.Verbo} {Item.Baseado} por conta própria");
        }

        /// <summary>
        /// O uso começa (4.6), no ponto do soltar do item arrastado, venha o item do usuário ou da agenda (o baseado por conta
        /// própria): com a cara de quem usa, e a onda vale desde já (C16): interromper o uso não a desfaz. A regra da transição
        /// diz também o que ele fez na onda da frente, com o alívio (<see cref="DescreverOAlivio"/>), e na paranoia
        /// (<see cref="Paranoia"/>). As ondas mudam antes de ele entrar em USING, mas a cara de quem usa vale por cima de
        /// qualquer cara de fase.
        /// </summary>
        private void ComecarOUso(Item item, DadosDoItem dados, ApoioDoUso apoio, string regra)
        {
            string alivio = DescreverOAlivio(dados);
            (string paranoia, bool comecou) = AplicarNaOnda(dados);
            _s = _s with
            {
                Uso = new Uso(item, dados.Verbo, dados.PassosDoUso, apoio) { ComecouAParanoia = comecou },
                PassosRestantes = dados.PassosDoUso,
                Expressao = dados.CaraDurante,
            };
            IrPara(Estado.Using, $"{regra}{alivio}{paranoia}");
        }

        /// <summary>
        /// O apoio do uso (4.6), primeiro pelo estado e depois pela geometria: escondido na borda, o esconderijo; na
        /// parede, fora do chão, a parede, mesmo na quina; no cipó, o cipó; senão, pela âncora: no chão, o chão; na borda de
        /// cima, o cipó; numa lateral, a parede; no ar (toon force), o chão, e a acomodação decide no fim. Sem a física, ele
        /// nunca agarra a parede nem o cipó: o apoio é o chão (ou o esconderijo).
        /// </summary>
        private ApoioDoUso ApoioAtual()
        {
            if (_s.Esconderijo != LadoDoEsconderijo.Nenhum) return ApoioDoUso.Esconderijo;
            if (!_cfg.Movimento || !Mundo(out _, out Superficies sup, out _) || _s.Lugar is not { } lugar) return ApoioDoUso.Chao;
            PontoPx a = lugar.Ancora;
            if (_s.Estado == Estado.Climbing && a.Y != sup.Chao && sup.NaLateral(a.X, out _)) return ApoioDoUso.Parede;
            if (_s.Estado == Estado.Hanging && a.Y == sup.Teto) return ApoioDoUso.Cipo;
            if (a.Y == sup.Chao) return ApoioDoUso.Chao;
            if (a.Y == sup.Teto) return ApoioDoUso.Cipo;
            return sup.NaLateral(a.X, out _) ? ApoioDoUso.Parede : ApoioDoUso.Chao;
        }

        /// <summary>
        /// Fim do uso: a cara volta à de base e a acomodação o devolve ao mesmo apoio (4.6): no chão, IDLE; na parede e no
        /// cipó, agarrado, preso se já estava (DEC-024); no esconderijo, espiando na mesma borda (DEC-025). Se o uso começou
        /// a paranoia, ele olha pro teto, se ficou livre para isso (<see cref="OlharProTetoNoComecoDaParanoia"/>).
        /// </summary>
        private void FimDoUso(string? regra = null)
        {
            Uso? uso = _s.Uso;
            VoltarACaraDeBase();
            if (_s.Lugar is not { } lugar) return;
            Acomodar(lugar.Ancora, regra ?? $"USING: fim do uso de {uso?.Item}", apoio: uso?.Apoio);
            if (uso is { ComecouAParanoia: true }) OlharProTetoNoComecoDaParanoia();
        }

        // ---------------------------------------------------------------- física dos itens

        /// <summary>
        /// Um passo do relógio para cada item caindo (4.8): a mesma gravidade e a mesma queda máxima do personagem, com um
        /// quique leve no máximo (<see cref="ParametrosDeMovimento.QuiquesDoItem"/>), sem achatar ao pousar (C27).
        /// </summary>
        private void PassoDosItens()
        {
            if (!_cfg.Tamagotchi || !_s.Itens.AlgumCaindo || _s.Topologia is not { } topologia) return;
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo item in _s.Itens.Todos)
            {
                if (item.Situacao == SituacaoDoItem.Caindo) itens = itens.Com(Cair(topologia, item));
            }
            _s = _s with { Itens = itens };
        }

        private ItemNoMundo Cair(Topologia topologia, ItemNoMundo item)
        {
            MonitorDoDesktop m = MonitorDoItem(topologia, item);
            Superficies sup = Superficies.Do(topologia, m, TamanhoDoItem(m));
            ParametrosDeMovimento f = _cfg.Fisica;
            double escala = m.Dpi / 96.0, dt = 1.0 / _cfg.PassosPorSegundo;
            double vy = Math.Min(item.VY + f.Gravidade * escala * dt, f.VelocidadeMaximaDeQueda * escala);
            double y = item.Y + vy * dt;
            int quiques = item.Quiques;
            SituacaoDoItem situacao = SituacaoDoItem.Caindo;
            if (y < sup.Teto)
            {
                y = sup.Teto;
                if (vy < 0) vy = 0;
            }
            if (y >= sup.Chao)
            {
                y = sup.Chao;
                if (quiques < f.QuiquesDoItem && f.RestituicaoDoItem > 0 && vy >= f.ImpactoMinimoDoItem * escala)
                {
                    vy = -vy * f.RestituicaoDoItem;
                    quiques++;
                }
                else
                {
                    vy = 0;
                    situacao = SituacaoDoItem.NoChao;
                }
            }
            var ancora = new PontoPx(Math.Clamp(item.Lugar.Ancora.X, sup.Esquerda, sup.Direita), (int)Math.Round(y, MidpointRounding.AwayFromZero));
            Posicionamento lugar = LugarDoItem(m, ancora);
            return item with { Situacao = situacao, Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = y, VY = vy, Quiques = quiques };
        }

        /// <summary>
        /// Depois de uma mudança de topologia (4.8), cada item fora da mão segue a mesma regra do personagem (DEC-030): no
        /// monitor que não mudou de geometria, no máximo transladado, ele continua como estava, caindo ou no chão, e anda junto;
        /// senão, a posição dele acompanha a topologia (<see cref="Posicionador.Rebasear"/>, com o sobrevivente medido nas
        /// coordenadas antigas) e é reacomodada pela posição relativa; fora do chão, volta a cair. O da mão segue o cursor, e o
        /// Windows leva a janela e o cursor com o monitor físico: o lugar dele anda com o monitor em que está, sem validar, como
        /// o arraste do personagem (<see cref="Posicionador.AcompanharPonto"/>; revisão do bloco P6-P9, achado 6). Largado antes
        /// do próximo movimento, ele fica no mesmo monitor físico.
        /// </summary>
        private void ReacomodarItens(Topologia antiga, Topologia nova)
        {
            if (_s.Itens.Quantidade == 0) return;
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo item in _s.Itens.Todos)
            {
                if (item.NaMao)
                {
                    PontoPx ancora = Posicionador.AcompanharPonto(antiga, nova, item.Lugar.Ancora);
                    itens = itens.Com(item with { Lugar = LugarDoItem(MonitorDaAncora(nova, ancora), ancora), Y = item.Y + ancora.Y - item.Lugar.Ancora.Y });
                    continue;
                }
                Posicionamento l = item.Lugar;
                if (Posicionador.MonitorCorrespondente(antiga, nova, l.Monitor.Chave, l.Monitor.Tela) is { } mesmo
                    && Posicionador.SoTranslacao(l.Monitor, mesmo, out int dx, out int dy))
                {
                    var lugar = new Posicionamento(mesmo, new PontoPx(l.Ancora.X + dx, l.Ancora.Y + dy), l.Tamanho, l.Retangulo.Deslocado(dx, dy));
                    itens = itens.Com(item with { Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = item.Y + dy });
                    continue;
                }
                PosicaoDoPersonagem acompanhada = Posicionador.Rebasear(antiga, nova, item.Posicao, _cfg.TamanhoDoItem);
                (Posicionamento r, PosicaoDoPersonagem p) = Posicionador.Reacomodar(nova, acompanhada, _cfg.TamanhoDoItem);
                SituacaoDoItem situacao = r.Ancora.Y == r.Monitor.AreaUtil.Base ? SituacaoDoItem.NoChao : SituacaoDoItem.Caindo;
                itens = itens.Com(item with { Situacao = situacao, Lugar = r, Posicao = p, Y = r.Ancora.Y, VY = 0, Quiques = 0 });
            }
            _s = _s with { Itens = itens };
        }

        /// <summary>Os itens que caem vão direto ao chão, cada um na coluna em que estava (D18: ao esconder ou sair).</summary>
        private void AssentarItens()
        {
            if (!_s.Itens.AlgumCaindo || _s.Topologia is null) return;
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo item in _s.Itens.Todos)
            {
                if (item.Situacao == SituacaoDoItem.Caindo) itens = itens.Com(NoChao(item));
            }
            _s = _s with { Itens = itens };
        }

        /// <summary>
        /// Um item que cai e deixa de aparecer (o personagem se escondeu, ou o monitor ficou ocupado pela tela cheia) vai
        /// direto ao chão (L5): o relógio nunca corre por um item que não se vê.
        /// </summary>
        private void AssentarOsInvisiveis()
        {
            if (!_cfg.Tamagotchi || !_s.Itens.AlgumCaindo || _s.Topologia is null) return;
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo item in _s.Itens.Todos)
            {
                if (item.Situacao == SituacaoDoItem.Caindo && !ItemVisivel(_s, item)) itens = itens.Com(NoChao(item));
            }
            _s = _s with { Itens = itens };
        }

        /// <summary>
        /// Esconder ou sair no meio do gesto sobre um item solta a captura dele, e o item fica no chão, na coluna em que
        /// estava (4.8).
        /// </summary>
        private void LiberarItemNaMao()
        {
            if (_s.Itens.NaMao is not { } item || _s.Topologia is null) return;
            _antes.Add(new LiberarCapturaDoItem(item.Id));
            _s = _s with { Itens = _s.Itens.Com(NoChao(item)) };
        }

        /// <summary>O item no chão do monitor dele, na mesma coluna (presa entre as laterais).</summary>
        private ItemNoMundo NoChao(ItemNoMundo item)
        {
            MonitorDoDesktop m = MonitorDoItem(_s.Topologia!, item);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(new PontoPx(item.Lugar.Ancora.X, m.AreaUtil.Base), TamanhoDoItem(m), m.AreaUtil);
            Posicionamento lugar = LugarDoItem(m, presa);
            return item with { Situacao = SituacaoDoItem.NoChao, Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = presa.Y, VY = 0, Quiques = 0, Pegada = default };
        }

        /// <summary>
        /// Onde o item invocado nasce (4.8), no monitor do personagem: ao lado dele, com uma folga, primeiro do lado para
        /// onde ele olha e depois do outro, e mais longe (até duas larguras de item) se o lugar está fora da área útil ou
        /// cruza outro item fora da mão; se nenhum servir, o primeiro, preso entre as laterais. A altura é
        /// <see cref="ParametrosDeMovimento.AlturaDaQuedaDoItem"/> acima dos pés dele (no chão, acima do chão), sem passar
        /// da borda de cima. Devolve também a âncora fina vertical.
        /// </summary>
        private (Posicionamento Lugar, double Y) LugarDeNascimento()
        {
            Topologia topologia = _s.Topologia!;
            Posicionamento personagem = _s.Lugar!;
            MonitorDoDesktop m = topologia.PorChave(personagem.Monitor.Chave) ?? MonitorDaAncora(topologia, personagem.Ancora);
            TamanhoPx tamanho = TamanhoDoItem(m);
            Superficies sup = Superficies.Do(topologia, m, tamanho);
            double escala = m.Dpi / 96.0;
            int folga = (int)Math.Round(_cfg.Fisica.FolgaDoItem * escala, MidpointRounding.AwayFromZero);
            int afastamento = _cfg.Tamanho.ParaPixels(m.Dpi).Largura / 2 + folga + tamanho.Largura / 2;
            int olhando = _s.Direcao == Direcao.Direita ? 1 : -1;
            int primeiro = personagem.Ancora.X + olhando * afastamento;
            int? x = null;
            for (int k = 0; k <= 2 && x is null; k++)
            {
                foreach (int lado in new[] { olhando, -olhando })
                {
                    int candidato = personagem.Ancora.X + lado * (afastamento + k * (tamanho.Largura + folga));
                    if (candidato < sup.Esquerda || candidato > sup.Direita || CruzaOutroItem(m, candidato, tamanho)) continue;
                    x = candidato;
                    break;
                }
            }
            double y = Math.Max(sup.Teto, Math.Min(personagem.Ancora.Y, sup.Chao) - _cfg.Fisica.AlturaDaQuedaDoItem * escala);
            var ancora = new PontoPx(x ?? Math.Clamp(primeiro, sup.Esquerda, sup.Direita), (int)Math.Round(y, MidpointRounding.AwayFromZero));
            return (LugarDoItem(m, ancora), y);
        }

        /// <summary>Se um item com a âncora na coluna <paramref name="x"/> cruzaria, na horizontal, outro item fora da mão no mesmo monitor.</summary>
        private bool CruzaOutroItem(MonitorDoDesktop m, int x, TamanhoPx tamanho)
        {
            int esquerda = x - tamanho.Largura / 2, direita = esquerda + tamanho.Largura;
            return _s.Itens.Todos.Any(i => !i.NaMao && i.Lugar.Monitor.Chave == m.Chave && i.Lugar.Retangulo.Esquerda < direita && esquerda < i.Lugar.Retangulo.Direita);
        }

        /// <summary>O monitor do item na topologia em cache: o da chave dele, ou o da âncora, se ele sumiu.</summary>
        private static MonitorDoDesktop MonitorDoItem(Topologia topologia, ItemNoMundo item)
            => topologia.PorChave(item.Lugar.Monitor.Chave) ?? MonitorDaAncora(topologia, item.Lugar.Ancora);

        private TamanhoPx TamanhoDoItem(MonitorDoDesktop m) => _cfg.TamanhoDoItem.ParaPixels(m.Dpi);

        private Posicionamento LugarDoItem(MonitorDoDesktop m, PontoPx ancora)
        {
            TamanhoPx tamanho = TamanhoDoItem(m);
            return new Posicionamento(m, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
        }

        // ---------------------------------------------------------------- efeitos das janelas dos itens

        /// <summary>Por que cada item saiu neste evento.</summary>
        private readonly SortedDictionary<int, MotivoDaRemocao> _removidos = [];

        /// <summary>
        /// As janelas dos itens (4.8), comparando o começo e o fim do evento: primeiro os que saíram (<see cref="RemoverItem"/>),
        /// depois, por Id, os que deixaram de aparecer (<see cref="EsconderItem"/>), os que passaram a aparecer ou nasceram
        /// à vista (<see cref="MostrarItem"/>) e os que mudaram de lugar à vista (<see cref="MoverItem"/>).
        /// </summary>
        private void EfeitosDosItens(List<Efeito> janela)
        {
            if (!_cfg.Tamagotchi) return;
            foreach (ItemNoMundo antes in _inicio.Itens.Todos)
            {
                if (_s.Itens.PorId(antes.Id) is null)
                    janela.Add(new RemoverItem(antes.Id, _removidos.GetValueOrDefault(antes.Id, MotivoDaRemocao.Recolhido)));
            }
            foreach (ItemNoMundo depois in _s.Itens.Todos)
            {
                ItemNoMundo? antes = _inicio.Itens.PorId(depois.Id);
                bool via = antes is not null && ItemVisivel(_inicio, antes);
                bool ve = ItemVisivel(_s, depois);
                if (via && !ve) janela.Add(new EsconderItem(depois.Id));
                else if (!via && ve) janela.Add(new MostrarItem(depois.Id, depois.Item, depois.Lugar));
                else if (ve && !Equals(antes!.Lugar, depois.Lugar)) janela.Add(new MoverItem(depois.Id, depois.Lugar));
            }
        }
    }
}
