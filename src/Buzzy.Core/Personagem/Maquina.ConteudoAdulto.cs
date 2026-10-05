namespace Buzzy.Core.Personagem;

/// <summary>
/// A chave "Conteúdo adulto" (DEC-033; pedido do usuário de 2026-10-02): ligada por padrão e gravada nas preferências.
/// Adulto é todo item que não é de alívio (<see cref="TabelaDoTamagotchi.Adulto"/>), as ondas de substância
/// (<see cref="DadosDaOnda.DeSubstancia"/>), com a paranoia, e o baseado por conta própria. Desligada, um item adulto não
/// nasce (<see cref="Passo.InvocarItem"/>), ele não fuma sozinho (<see cref="Passo.PodeFumarPorContaPropria"/>) e, na hora de
/// desligar, tudo o que é adulto sai (<see cref="Passo.TirarOConteudoAdulto"/>). Ligar só grava a escolha.
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

            if (DeSubstancia(_s.OndaDeFundo)) _s = _s with { OndaDeFundo = null };
            if (DeSubstancia(_s.Onda)) FimDaFrente();

            if (_s.Estado == Estado.Using && _s.Uso is { } uso && ItemAdulto(uso.Item)) FimDoUso($"USING: conteúdo adulto desligado, o uso de {uso.Item} termina");
        }
    }
}
