namespace Buzzy.Core.Personagem;

/// <summary>
/// A chave "Conteúdo adulto" (DEC-033; pedido do usuário de 2026-10-02): desligada por padrão e gravada nas preferências.
/// Adulto é todo item que não é de alívio (<see cref="TabelaDoTamagotchi.Adulto"/>), as ondas de substância
/// (<see cref="DadosDaOnda.DeSubstancia"/>), com a paranoia, e o baseado por conta própria. Desligada, um item adulto não
/// nasce (<see cref="Passo.InvocarItem"/>), ele não fuma sozinho (<see cref="Passo.PodeFumarPorContaPropria"/>) e, na hora de
/// desligar, tudo o que é adulto sai (<see cref="Passo.TirarOConteudoAdulto"/>). As escolhas individuais são independentes
/// e configuráveis com a chave geral desligada (DEC-041).
/// </summary>
public static partial class Maquina
{
    private sealed partial class Passo
    {
        /// <summary>Se o item é adulto pela tabela em uso: todo item que não é de alívio.</summary>
        private bool ItemAdulto(Item item) => !_cfg.TabelaDeItens(item).Alivio;

        /// <summary>
        /// CMD_SET_ADULT_CONTENT: grava a escolha nas preferências e a registra numa transição para o mesmo estado; desligar
        /// tira o conteúdo adulto na hora. Antes da carga ou igual à atual, é ignorado.
        /// </summary>
        private void EscolherConteudoAdulto(bool ligado)
        {
            if (!_s.Carregado || ligado == _s.Preferencias.ConteudoAdulto) return;
            _s = _s with { Preferencias = _s.Preferencias with { ConteudoAdulto = ligado } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ADULT_CONTENT: {(ligado ? "ligado" : "desligado")}"));
            if (!ligado) TirarOConteudoAdulto();
        }

        /// <summary>CMD_SET_ADULT_ITEM: salva uma escolha individual e, ao desmarcar, remove só a contribuição daquele item.</summary>
        private void EscolherItemAdulto(Item item, bool ligado)
        {
            if (!_cfg.Tamagotchi || !_s.Carregado || !Enum.IsDefined(item) || !ItemAdulto(item)) return;
            bool estavaLigado = _s.Preferencias.ItensAdultosHabilitados.Contem(item);
            if (estavaLigado == ligado) return;

            ConjuntoDeItens itens = ligado
                ? _s.Preferencias.ItensAdultosHabilitados.Com(item)
                : _s.Preferencias.ItensAdultosHabilitados.Sem(item);
            _s = _s with { Preferencias = _s.Preferencias with { ItensAdultosHabilitados = itens } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, "CMD_SET_ADULT_ITEM: preferência atualizada"));
            if (!ligado) TirarOItemAdulto(item);
        }

        /// <summary>Aplica o desligamento individual a instâncias, uso, onda e carga da mistura do item.</summary>
        private void TirarOItemAdulto(Item item)
        {
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo emTela in _s.Itens.Todos)
            {
                if (emTela.Item != item) continue;
                if (emTela.NaMao) _antes.Add(new LiberarCapturaDoItem(emTela.Id));
                itens = itens.Sem(emTela.Id);
                _removidos[emTela.Id] = MotivoDaRemocao.Recolhido;
            }
            _s = _s with { Itens = itens, Carga = _s.Carga.Sem(item) };

            if (_s.Estado == Estado.Using && _s.Uso is { } uso && uso.Item == item)
                FimDoUso("USING: o item em uso foi desabilitado nas configurações");

            EstadoDaOnda? frenteAnterior = _s.Onda;
            var (frente, fontesDaFrente) = TirarFonteDaOnda(_s.Onda, _s.FontesDaOnda, item);
            var (fundo, fontesDoFundo) = TirarFonteDaOnda(_s.OndaDeFundo, _s.FontesDaOndaDeFundo, item);

            // Se a paranoia perdeu a mistura que a sustentava, o efeito termina sem sortear outro.
            if (frente is null && fundo is not null)
            {
                frente = fundo;
                fontesDaFrente = fontesDoFundo;
                fundo = null;
                fontesDoFundo = ContribuicoesDaOnda.Nenhuma;
            }

            _s = _s with { OndaDeFundo = fundo, FontesDaOndaDeFundo = fontesDoFundo };
            if (frente != frenteAnterior)
            {
                if (frente is { } nova)
                {
                    IniciarFase(nova);
                    // As fontes acompanham a onda recalculada: sem isso, a parte do item tirado continuaria sustentando a onda.
                    _s = _s with { FontesDaOnda = fontesDaFrente };
                }
                else
                {
                    _s = _s with { Onda = null, FontesDaOnda = ContribuicoesDaOnda.Nenhuma };
                    if (CaraLivre(_s.Estado)) _s = _s with { Expressao = CaraDeBase() };
                }
            }
            else _s = _s with { Onda = frente, FontesDaOnda = fontesDaFrente };

            ZerarACargaSemSubstancia();
        }

        /// <summary>Retira a fonte e limita nível/pior ao que ainda é sustentado pelos outros itens.</summary>
        private (EstadoDaOnda? Onda, ContribuicoesDaOnda Fontes) TirarFonteDaOnda(
            EstadoDaOnda? onda, ContribuicoesDaOnda fontes, Item item)
        {
            if (onda is null) return (null, ContribuicoesDaOnda.Nenhuma);
            if (onda.Tipo == Onda.Paranoico)
                return _s.Carga.MisturaComSintetica ? (onda, fontes) : (null, ContribuicoesDaOnda.Nenhuma);

            ContribuicoesDaOnda restantes = fontes.Sem(item);
            int total = restantes.Total;
            if (total == 0) return (null, ContribuicoesDaOnda.Nenhuma);
            int nivel = Math.Min(onda.Nivel, total);
            int pior = Math.Max(nivel, Math.Min(onda.Pior, total));
            return (onda with { Nivel = nivel, Pior = pior }, restantes);
        }

        /// <summary>
        /// Desligar tira o que é adulto, com o tamagotchi ligado:
        /// <list type="bullet">
        /// <item>os itens adultos saem do mundo, como recolhidos; o da mão do usuário solta a captura antes;</item>
        /// <item>a onda de substância do fundo some; a da frente acaba como no fim dela (<see cref="FimDaFrente"/>): uma leve
        /// no fundo volta à frente, e sem ela a cara volta à de base. A carga do episódio zera no fim do evento, sem onda de
        /// substância (<see cref="ZerarACargaSemSubstancia"/>);</item>
        /// <item>o uso de um item adulto termina na hora, no mesmo apoio (<see cref="FimDoUso"/>), sem o olhar pro teto: a
        /// paranoia já saiu.</item>
        /// </list>
        /// Um gesto da onda em curso já terminou pelo invariante 15, como em todo comando do usuário.
        /// </summary>
        private void TirarOConteudoAdulto()
        {
            if (!_cfg.Tamagotchi) return;
            ItensNoMundo itens = _s.Itens;
            foreach (ItemNoMundo item in _s.Itens.Todos)
            {
                if (!ItemAdulto(item.Item)) continue;
                if (item.NaMao) _antes.Add(new LiberarCapturaDoItem(item.Id));
                itens = itens.Sem(item.Id);
                _removidos[item.Id] = MotivoDaRemocao.Recolhido;
            }
            _s = _s with { Itens = itens };

            if (DeSubstancia(_s.OndaDeFundo))
                _s = _s with { OndaDeFundo = null, FontesDaOndaDeFundo = ContribuicoesDaOnda.Nenhuma };
            if (DeSubstancia(_s.Onda)) FimDaFrente();

            if (_s.Estado == Estado.Using && _s.Uso is { } uso && ItemAdulto(uso.Item)) FimDoUso($"USING: conteúdo adulto desligado, o uso de {uso.Item} termina");
        }
    }
}
