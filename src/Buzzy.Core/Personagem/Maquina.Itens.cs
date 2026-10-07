namespace Buzzy.Core.Personagem;

// Itens do tamagotchi. Com o tamagotchi desligado, os eventos de item são descartados logo de cara.
// O item vive no núcleo: nasce pelo menu ao lado do personagem, cai, quica uma vez, pode ser arrastado
// e, solto sobre ele num estado que aceita, é usado. O app só desenha as janelas a partir dos efeitos.
// A agenda também pode fazê-lo usar uma droga por conta própria, sem item no mundo.
public static partial class Maquina
{
    // Nos outros estados (pulando, caindo, usando, arrastado...) o item cai de onde foi solto.
    public static bool AceitaItem(Estado estado)
        => estado is Estado.Idle or Estado.Walking or Estado.Climbing or Estado.Hanging or Estado.Resting
            or Estado.Reacting or Estado.Landing or Estado.Peeking;

    // O sprite é encolhido margem% de cada lado (20% em 128 px = 26 px, miolo de 76 px), porque o núcleo não
    // conhece a transparência: o miolo cobre o corpo em todas as poses. Retângulos semiabertos, como RECT.
    // Com 50% o miolo fica vazio.
    public static bool SobreOPersonagem(RetanguloPx item, RetanguloPx personagem, int margemPercentual)
    {
        if (margemPercentual is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(margemPercentual), margemPercentual, "A margem do alvo vai de 0 a 50%.");
        int dx = (personagem.Largura * margemPercentual + 50) / 100;
        int dy = (personagem.Altura * margemPercentual + 50) / 100;
        var miolo = new RetanguloPx(personagem.Esquerda + dx, personagem.Topo + dy, personagem.Direita - dx, personagem.Base - dy);
        return miolo.Intersecta(item);
    }

    // Na mão sempre aparece, pro item não sumir no meio do arraste. Fora da mão, só com o personagem à vista
    // e fora de monitor em tela cheia, pra não cobrir o app.
    public static bool ItemVisivel(EstadoDoNucleo s, ItemNoMundo item)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(item);
        return item.NaMao || (s.Estado.Visivel() && !(s.Preferencias.ModoTelaCheia && s.Ocupados.Contem(item.Lugar.Monitor.Chave)));
    }

    // Descartados antes de tudo com o tamagotchi desligado.
    private static bool EhDoTamagotchi(Evento evento)
        => evento is CmdSummonItem or CmdClearItems or ItemPress or ItemDragStart or ItemDragMove or ItemDragEnd or ItemRelease or ItemEffectTimer;

    private sealed partial class Passo
    {
        private bool AtentoAoItem => _cfg.Tamagotchi && _s.Atento;

        // Item visível caindo mantém o relógio rodando.
        private bool ItemVisivelCaindo => _cfg.Tamagotchi && _s.Itens.Todos.Any(i => i.Situacao == SituacaoDoItem.Caindo && ItemVisivel(_s, i));

        // ---------------------------------------------------------------- menu

        // Nasce ao lado dele, primeiro do lado pra onde olha, e cai. No limite de itens, sai antes o de menor
        // Id que não está na mão. Parado e sem onda, ele fica empolgado.
        private void InvocarItem(Item item)
        {
            if (!Enum.IsDefined(item) || !_s.Carregado || !_s.Estado.Visivel() || _s.Topologia is null || _s.Lugar is null || _cfg.MaximoDeItens < 1) return;
            // Item fora da edição compilada não existe.
            if (!_cfg.ItensDaEdicao.Contem(item)) return;
            // Item adulto exige a chave geral e a marcação individual.
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

        // O da mão solta a captura antes.
        private void RecolherItens()
        {
            if (_s.Itens.Quantidade == 0) return;
            if (_s.Itens.NaMao is { } naMao) _antes.Add(new LiberarCapturaDoItem(naMao.Id));
            foreach (ItemNoMundo i in _s.Itens.Todos) _removidos[i.Id] = MotivoDaRemocao.Recolhido;
            _s = _s with { Itens = ItensNoMundo.Nenhum };
        }

        // ---------------------------------------------------------------- gestos sobre um item

        // Pegada = cursor - âncora. Outro item que estivesse na mão é largado antes.
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

        private void IniciarArrasteDoItem(int id)
        {
            if (_s.Itens.NaMao is { Situacao: SituacaoDoItem.Segurado } item && item.Id == id)
                _s = _s with { Itens = _s.Itens.Com(item with { Situacao = SituacaoDoItem.Arrastado }) };
        }

        // Não prende na área útil durante o arraste, igual ao personagem.
        private void ArrastarItem(int id, PontoPx cursor)
        {
            if (_s.Itens.NaMao is not { Situacao: SituacaoDoItem.Arrastado } item || item.Id != id || _s.Topologia is not { } topologia) return;
            var ancora = new PontoPx(cursor.X - item.Pegada.X, cursor.Y - item.Pegada.Y);
            MonitorDoDesktop m = MonitorDaAncora(topologia, ancora);
            _s = _s with { Itens = _s.Itens.Com(item with { Lugar = LugarDoItem(m, ancora), Y = ancora.Y }) };
        }

        // Sobre o personagem num estado que aceita: ele usa. Senão o item cai de onde foi solto.
        // Sem DRAG_START antes (não deveria acontecer), age como RELEASE e nunca usa.
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

        // Clique, clique duplo ou captura perdida: cai de onde está, nunca é usado.
        private void LargarItem(int id)
        {
            if (_s.Itens.NaMao is not { } item || item.Id != id || _s.Topologia is null) return;
            _s = _s with { Itens = _s.Itens.Com(Solto(item, item.Lugar.Ancora)) };
        }

        // Preso na área útil. No ar, cai do zero e pode quicar de novo.
        private ItemNoMundo Solto(ItemNoMundo item, PontoPx desejada)
        {
            MonitorDoDesktop m = MonitorDaAncora(_s.Topologia!, desejada);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(desejada, TamanhoDoItem(m), m.AreaUtil);
            Posicionamento lugar = LugarDoItem(m, presa);
            SituacaoDoItem situacao = presa.Y == m.AreaUtil.Base ? SituacaoDoItem.NoChao : SituacaoDoItem.Caindo;
            return item with { Situacao = situacao, Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = presa.Y, VY = 0, Quiques = 0, Pegada = default };
        }

        // Com um item na mão, a agenda pausa e ele para onde está pra receber o item. Andando para,
        // descansando acorda, na parede/cipó fica agarrado sem foguete; pulo e queda seguem até o chão.
        private void FicarAtento()
        {
            bool acordou = false;
            switch (_s.Estado)
            {
                // Atravessando monitor, espera a travessia acabar; o fim dela o para.
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

        // O uso começa no apoio atual; o que ele fazia é cortado (descansando, acorda antes).
        private void UsarItem(ItemNoMundo item)
        {
            DadosDoItem dados = _cfg.TabelaDeItens(item.Item);
            ApoioDoUso apoio = ApoioAtual();
            _s = _s with { Itens = _s.Itens.Sem(item.Id) };
            _removidos[item.Id] = MotivoDaRemocao.Usado;
            if (_s.Estado == Estado.Resting) _s = _s with { Sinal = Sinal.Acordou };
            ComecarOUso(item.Item, dados, apoio, $"ITEM_DRAG_END sobre o personagem: {dados.Verbo} {item.Item}");
        }

        // Na ordem do menu. Exclui a droga cuja onda já está na frente, pra ele não emendar. Repete condições
        // que a agenda já garante, pra regra valer sozinha.
        private List<Item> CandidatasPorContaPropria()
        {
            var candidatas = new List<Item>();
            Preferencias p = _s.Preferencias;
            if (!_cfg.Tamagotchi || !p.ConteudoAdulto || p.ItensPorContaPropria == ConjuntoDeItens.Vazio
                || _s.Estado != Estado.Idle || _s.Esconderijo != LadoDoEsconderijo.Nenhum
                || _s.AutonomiaPausada || _s.PainelAberto || AtentoAoItem
                || _s.Lugar is not { } lugar || lugar.Ancora.Y != lugar.Monitor.AreaUtil.Base
                || (ComOnda && _s.Onda!.Tipo == Onda.Paranoico))
                return candidatas;
            foreach (Item item in TabelaDoTamagotchi.Itens)
                if (p.ItensPorContaPropria.Contem(item) && p.ItensAdultosHabilitados.Contem(item) && _cfg.ItensDaEdicao.Contem(item)
                    && !(ComOnda && _s.Onda!.Tipo == _cfg.TabelaDeItens(item).Onda))
                    candidatas.Add(item);
            return candidatas;
        }

        // Tira a droga "do chapéu": nenhum item nasce nem gasta Id. Usa o mesmo caminho do item solto nele
        // (combinação, alívio, carga, paranoia). Com várias candidatas, sorteia no gerador principal.
        private void UsarPorContaPropria(List<Item> candidatas)
        {
            Item item = candidatas[0];
            if (candidatas.Count > 1)
            {
                (int indice, Aleatorio a) = _s.Aleatorio.Entre(0, candidatas.Count - 1);
                _s = _s with { Aleatorio = a };
                item = candidatas[indice];
            }
            DadosDoItem dados = _cfg.TabelaDeItens(item);
            ComecarOUso(item, dados, ApoioDoUso.Chao, $"IDLE + AUTONOMY_TIMER: {dados.Verbo} {item} por conta própria");
        }

        // A onda vale desde já: interromper o uso não a desfaz. As ondas mudam antes de entrar em USING, mas a
        // cara de quem usa fica por cima da cara da fase.
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

        // Primeiro pelo estado, depois pela âncora. No ar (toon force) conta como chão e a acomodação decide no
        // fim. Sem física ele nunca agarra parede nem cipó.
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

        // Volta ao mesmo apoio: chão vira IDLE, parede/cipó agarrado (preso se já estava), esconderijo espiando
        // na mesma borda. Se o uso começou a paranoia, olha pro teto.
        private void FimDoUso(string? regra = null)
        {
            Uso? uso = _s.Uso;
            VoltarACaraDeBase();
            if (_s.Lugar is not { } lugar) return;
            Acomodar(lugar.Ancora, regra ?? $"USING: fim do uso de {uso?.Item}", apoio: uso?.Apoio);
            if (uso is { ComecouAParanoia: true }) OlharProTetoNoComecoDaParanoia();
        }

        // ---------------------------------------------------------------- física dos itens

        // Mesma gravidade do personagem, no máximo um quique leve e sem achatar ao pousar.
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

        // Mesma regra do personagem: monitor só transladado, o item anda junto e segue como estava. Senão,
        // rebaseia na topologia nova e reacomoda pela posição relativa. O da mão acompanha o monitor físico
        // sem validar, porque o Windows leva a janela e o cursor junto.
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

        // Ao esconder ou sair: quem está caindo vai direto pro chão, na mesma coluna.
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

        // O relógio nunca roda por item que não se vê: invisível caindo vai direto pro chão.
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

        // Esconder ou sair no meio do arraste solta a captura e deixa o item no chão.
        private void LiberarItemNaMao()
        {
            if (_s.Itens.NaMao is not { } item || _s.Topologia is null) return;
            _antes.Add(new LiberarCapturaDoItem(item.Id));
            _s = _s with { Itens = _s.Itens.Com(NoChao(item)) };
        }

        private ItemNoMundo NoChao(ItemNoMundo item)
        {
            MonitorDoDesktop m = MonitorDoItem(_s.Topologia!, item);
            PontoPx presa = Posicionador.PrenderNaAreaUtil(new PontoPx(item.Lugar.Ancora.X, m.AreaUtil.Base), TamanhoDoItem(m), m.AreaUtil);
            Posicionamento lugar = LugarDoItem(m, presa);
            return item with { Situacao = SituacaoDoItem.NoChao, Lugar = lugar, Posicao = Posicionador.Descrever(lugar), Y = presa.Y, VY = 0, Quiques = 0, Pegada = default };
        }

        // Ao lado dele, primeiro do lado pra onde olha; se cair fora da área ou em cima de outro item, tenta
        // mais longe (até 2 larguras). Se nada servir, o primeiro preso nas laterais. Nasce acima dos pés.
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

        // Só na horizontal, ignorando o item na mão.
        private bool CruzaOutroItem(MonitorDoDesktop m, int x, TamanhoPx tamanho)
        {
            int esquerda = x - tamanho.Largura / 2, direita = esquerda + tamanho.Largura;
            return _s.Itens.Todos.Any(i => !i.NaMao && i.Lugar.Monitor.Chave == m.Chave && i.Lugar.Retangulo.Esquerda < direita && esquerda < i.Lugar.Retangulo.Direita);
        }

        // Se o monitor da chave sumiu, usa o da âncora.
        private static MonitorDoDesktop MonitorDoItem(Topologia topologia, ItemNoMundo item)
            => topologia.PorChave(item.Lugar.Monitor.Chave) ?? MonitorDaAncora(topologia, item.Lugar.Ancora);

        private TamanhoPx TamanhoDoItem(MonitorDoDesktop m) => _cfg.TamanhoDoItem.ParaPixels(m.Dpi);

        private Posicionamento LugarDoItem(MonitorDoDesktop m, PontoPx ancora)
        {
            TamanhoPx tamanho = TamanhoDoItem(m);
            return new Posicionamento(m, ancora, tamanho, Posicionador.RetanguloDoSprite(ancora, tamanho));
        }

        // ---------------------------------------------------------------- efeitos das janelas dos itens

        // Por que cada item saiu neste evento.
        private readonly SortedDictionary<int, MotivoDaRemocao> _removidos = [];

        // Compara começo e fim do evento: primeiro os removidos, depois, por Id, esconder/mostrar/mover.
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
