namespace Buzzy.Core.Personagem;

// Chave "Conteúdo adulto", desligada por padrão. Adulto = todo item que não é de alívio, as ondas de
// substância (com a paranoia) e o baseado por conta própria. Desligada: item adulto não nasce, ele não
// fuma sozinho e, ao desligar, tudo o que é adulto sai. As escolhas por item valem mesmo com a chave desligada.
public static partial class Maquina
{
    private sealed partial class Passo
    {
        private bool ItemAdulto(Item item) => !_cfg.TabelaDeItens(item).Alivio;

        // Desligar tira o conteúdo adulto na hora. Antes da carga, ou sem mudança, ignora.
        private void EscolherConteudoAdulto(bool ligado)
        {
            if (!_s.Carregado || ligado == _s.Preferencias.ConteudoAdulto) return;
            _s = _s with { Preferencias = _s.Preferencias with { ConteudoAdulto = ligado } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, $"CMD_SET_ADULT_CONTENT: {(ligado ? "ligado" : "desligado")}"));
            if (!ligado) TirarOConteudoAdulto();
        }

        // Só vale pro futuro: um uso em curso continua.
        private void EscolherUsoPorContaPropria(Item item, bool ligado)
        {
            if (!_cfg.Tamagotchi || !_s.Carregado || !Enum.IsDefined(item) || !TabelaDoTamagotchi.Ilicitos.Contem(item) || !_cfg.ItensDaEdicao.Contem(item)) return;
            if (_s.Preferencias.ItensPorContaPropria.Contem(item) == ligado) return;
            ConjuntoDeItens itens = ligado ? _s.Preferencias.ItensPorContaPropria.Com(item) : _s.Preferencias.ItensPorContaPropria.Sem(item);
            _s = _s with { Preferencias = _s.Preferencias with { ItensPorContaPropria = itens } };
            _depois.Add(new GravarPreferencias(_s.Preferencias));
            _transicoes.Add(new Transicao(_s.Estado, _s.Estado, "CMD_SET_SELF_USE_ITEM: preferência atualizada"));
        }

        // Desmarcar remove só a contribuição daquele item.
        private void EscolherItemAdulto(Item item, bool ligado)
        {
            if (!_cfg.Tamagotchi || !_s.Carregado || !Enum.IsDefined(item) || !ItemAdulto(item) || !_cfg.ItensDaEdicao.Contem(item)) return;
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

        // Tira o item do mundo, do uso, da onda e da carga da mistura.
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
                    // Sem isso, a parte do item tirado continuaria sustentando a onda.
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

        // Limita nível/pior ao que os outros itens ainda sustentam.
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

        // Itens adultos saem como recolhidos (o da mão solta a captura antes). A onda de substância do fundo
        // some e a da frente acaba como no fim normal. O uso de item adulto termina na hora, sem olhar pro
        // teto, já que a paranoia saiu. Gesto da onda em curso já terminou, como em todo comando.
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
