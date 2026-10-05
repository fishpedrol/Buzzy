using Buzzy.Core.Personagem;
using Buzzy.Core.Testes.Movimento;
using Buzzy.Testes;

namespace Buzzy.Core.Testes.Personagem;

/// <summary>
/// Fase 8, passo F8-P4 (DEC-038, item 2; invariante 35): os comandos das configurações, um por campo. Sem
/// <c>ConfiguracoesDisponiveis</c>, são descartados antes de tudo, até de encerrar um gesto curto. Com ela, cada um muda só o
/// seu campo e emite exatamente um <c>GravarPreferencias</c> (o do topo, também <c>AplicarSempreNoTopo</c>); são ignorados
/// com o valor igual, inválido ou antes da carga; encerram um gesto curto como todo CMD_* (invariante 15). A energia pelo
/// comando e pelo painel chega à mesma preferência (critério 10), sem mudar a física nem a agenda armada.
/// </summary>
internal static class ComandosDasConfiguracoesTestes
{
    private static readonly ConfiguracaoDoNucleo ComConfiguracoes = new() { ConfiguracoesDisponiveis = true, PainelDeEnergiaDisponivel = true };

    private static Cenario Parado(ConfiguracaoDoNucleo? cfg = null) => Cenario.Parado(cfg ?? ComConfiguracoes);

    /// <summary>O estado sem o que todo evento pode mudar à parte (o sinal pontual), para comparar o resto.</summary>
    private static EstadoDoNucleo SemPreferencias(EstadoDoNucleo s, Preferencias p) => s with { Preferencias = p, Sinal = Sinal.Nenhum };

    [Teste]
    public static void SemACapacidade_DescartadosAntesDeTudo_InclusiveDoGesto()
    {
        Cenario c = Cenario.Em(Estado.Idle, new ConfiguracaoDoNucleo { Acoes = AcoesAutonomas.Gesto });
        c.Decidir();
        Afirmar.Verdadeiro(c.Atual.Gesto != Gesto.Nenhum, "um gesto curto em curso");
        EstadoDoNucleo antes = c.Atual;
        foreach (Evento e in new Evento[] { new CmdSetEnergy(NivelDeEnergia.Alta), new CmdSetAlwaysOnTop(false), new CmdSetScale(EscalaDoPersonagem.Grande) })
        {
            c.Aplicar(e);
            Afirmar.Igual(antes, c.Atual, $"{e}: nada muda, nem o gesto");
            Afirmar.Igual(0, c.Efeitos.Count, $"{e}: nenhum efeito");
        }
    }

    [Teste]
    public static void CadaComando_SoOSeuCampo_UmaGravacao()
    {
        (Evento Comando, Func<Preferencias, Preferencias> Esperado, Type[] Efeitos)[] casos =
        [
            (new CmdSetEnergy(NivelDeEnergia.Alta), p => p with { Energia = NivelDeEnergia.Alta }, [typeof(GravarPreferencias)]),
            (new CmdSetAlwaysOnTop(false), p => p with { SempreNoTopo = false }, [typeof(GravarPreferencias), typeof(AplicarSempreNoTopo)]),
            (new CmdSetScale(EscalaDoPersonagem.Pequena), p => p with { Escala = EscalaDoPersonagem.Pequena }, [typeof(GravarPreferencias)]),
        ];
        foreach ((Evento comando, Func<Preferencias, Preferencias> esperado, Type[] efeitos) in casos)
        {
            Cenario c = Parado();
            EstadoDoNucleo antes = c.Atual;
            c.Aplicar(comando);
            Preferencias p = esperado(antes.Preferencias);
            Afirmar.Igual(p, c.Atual.Preferencias, $"{comando}: o campo");
            Afirmar.Igual(SemPreferencias(antes, p), SemPreferencias(c.Atual, p), $"{comando}: nada além do campo (estado, posição, agenda, geradores)");
            Afirmar.Sequencia(efeitos, c.Efeitos.Select(e => e.GetType()), $"{comando}: os efeitos");
            Afirmar.Igual(p, c.Efeito<GravarPreferencias>().Preferencias, $"{comando}: grava o registro novo");
            Afirmar.Verdadeiro(c.Transicoes.Count == 1 && c.Transicoes[0].De == c.Transicoes[0].Para, $"{comando}: uma transição para o mesmo estado");

            // Igual de novo: ignorado.
            EstadoDoNucleo depois = c.Atual;
            c.Aplicar(comando);
            Afirmar.Igual((depois, 0), (c.Atual, c.Efeitos.Count), $"{comando}: o mesmo valor é ignorado");
        }
        Cenario s = Parado();
        s.Aplicar(new CmdSetAlwaysOnTop(false));
        Afirmar.Igual(new AplicarSempreNoTopo(false), s.Efeito<AplicarSempreNoTopo>(), "aplica o topo desligado");
    }

    // Fase 9, F9-P3 (DEC-040, item 7): uma escala fora do enum, vinda da carga ou de SETTINGS_CHANGED, vira a padrão no
    // núcleo (Sanear), e o resto das preferências fica como veio.
    [Teste]
    public static void EscalaForaDoEnum_NaCargaENoSettingsChanged_ViraAPadrao()
    {
        foreach (int fora in new[] { -1, 3, 9, int.MaxValue })
        {
            Preferencias lidas = Preferencias.Padrao with { Escala = (EscalaDoPersonagem)fora, Energia = NivelDeEnergia.Alta, SempreNoTopo = false };
            var c = new Cenario(ComConfiguracoes).Aplicar(new Loaded(TopologiasDeExemplo.UmMonitor, null, lidas));
            Afirmar.Igual(lidas with { Escala = Preferencias.Padrao.Escala }, c.Atual.Preferencias, $"carga com a escala {fora}");
            Cenario s = Parado();
            s.Aplicar(new SettingsChanged(lidas));
            Afirmar.Igual(lidas with { Escala = Preferencias.Padrao.Escala }, s.Atual.Preferencias, $"SETTINGS_CHANGED com a escala {fora}");
        }
    }

    [Teste]
    public static void Invalidos_EAntesDaCarga_Ignorados()
    {
        Cenario c = Parado();
        EstadoDoNucleo antes = c.Atual;
        foreach (Evento e in new Evento[] { new CmdSetEnergy((NivelDeEnergia)9), new CmdSetEnergy((NivelDeEnergia)(-1)), new CmdSetScale((EscalaDoPersonagem)7) })
        {
            c.Aplicar(e);
            Afirmar.Igual((antes, 0), (c.Atual, c.Efeitos.Count), $"{e}: inválido, ignorado");
        }
        var antesDaCarga = new Cenario(ComConfiguracoes);
        EstadoDoNucleo booting = antesDaCarga.Atual;
        antesDaCarga.Aplicar(new CmdSetEnergy(NivelDeEnergia.Alta), new CmdSetAlwaysOnTop(false), new CmdSetScale(EscalaDoPersonagem.Grande));
        Afirmar.Igual(booting.Preferencias, antesDaCarga.Atual.Preferencias, "antes da carga, ignorados");
    }

    // Com a capacidade, um comando encerra um gesto curto em curso, como todo CMD_* (invariante 15), e só por isso a agenda
    // é refeita.
    [Teste]
    public static void ComACapacidade_EncerraOGesto()
    {
        Cenario c = Cenario.Em(Estado.Idle, ComConfiguracoes with { Acoes = AcoesAutonomas.Gesto });
        c.Decidir();
        Afirmar.Verdadeiro(c.Atual.Gesto != Gesto.Nenhum, "um gesto curto em curso");
        c.Aplicar(new CmdSetScale(EscalaDoPersonagem.Grande));
        Afirmar.Igual((Gesto.Nenhum, EscalaDoPersonagem.Grande), (c.Atual.Gesto, c.Atual.Preferencias.Escala), "o gesto acabou, e a escala mudou");
    }

    // Critério 10 [AUTO]: a energia pelo painel (ENERGY_SELECTED com ele aberto) e pelo comando chega à mesma preferência,
    // com a mesma gravação; a agenda armada não muda, e a próxima decisão usa o perfil novo; a física não muda.
    [Teste]
    public static void Energia_PeloPainelEPeloComando_AMesmaPreferencia()
    {
        Cenario painel = Parado().Aplicar(new EnergyPanelOpen(), new EnergySelected(NivelDeEnergia.Baixa));
        GravarPreferencias doPainel = painel.Efeito<GravarPreferencias>();
        painel.Aplicar(new EnergyPanelClose());

        Cenario comando = Parado();
        (long geracao, bool agendada) = (comando.Atual.Geracao, comando.Atual.DecisaoAgendada);
        comando.Aplicar(new CmdSetEnergy(NivelDeEnergia.Baixa));
        Afirmar.Igual(doPainel, comando.Efeito<GravarPreferencias>(), "a mesma gravação");
        Afirmar.Igual(painel.Atual.Preferencias, comando.Atual.Preferencias, "a mesma preferência");
        Afirmar.Igual((geracao, agendada), (comando.Atual.Geracao, comando.Atual.DecisaoAgendada), "a agenda armada não muda");
        Afirmar.Falso(comando.Efeitos.Any(e => e is AgendarDecisao or CancelarDecisao), "nenhum reagendamento");
        Afirmar.Igual(PerfilDeEnergia.Baixa, Maquina.PerfilEfetivo(comando.Atual, ComConfiguracoes), "o perfil em vigor é o da Baixa");
        Afirmar.Falso(comando.Atual.PainelAberto, "o painel não abriu");
    }

    // Invariante 35 nas sequências: um comando de preferência, fora de um gesto curto, muda só o seu campo; com o valor igual
    // ou inválido, nada; e nunca mais de um GravarPreferencias.
    [Teste]
    public static void Invariante35_SequenciasAleatorias()
    {
        ConfiguracaoDoNucleo cfg = ConfiguracaoDoNucleo.DoAplicativo(new TamanhoDip(128, 128)) with { ConfiguracoesDisponiveis = true, PainelDeEnergiaDisponivel = true };
        int comandos = 0;
        for (int semente = 1; semente <= 40; semente++)
        {
            var rnd = new Random(semente);
            var sim = new SimuladorDeTempo(cfg, (ulong)semente, TopologiasDeExemplo.LadoALado);
            sim.AoResultado = (antes, e, r) =>
            {
                if (e is not (CmdSetEnergy or CmdSetAlwaysOnTop or CmdSetScale)) return;
                comandos++;
                Afirmar.Verdadeiro(r.Efeitos.OfType<GravarPreferencias>().Count() <= 1, $"{e}: no máximo uma gravação");
                if (antes.Gesto != Gesto.Nenhum) return;
                Afirmar.Igual(SemPreferencias(antes, r.Estado.Preferencias), SemPreferencias(r.Estado, r.Estado.Preferencias), $"semente {semente}, {e}: só a preferência");
                Preferencias esperada = e switch
                {
                    CmdSetEnergy c when Enum.IsDefined(c.Nivel) => antes.Preferencias with { Energia = c.Nivel },
                    CmdSetAlwaysOnTop c => antes.Preferencias with { SempreNoTopo = c.Ligado },
                    CmdSetScale c when Enum.IsDefined(c.Escala) => antes.Preferencias with { Escala = c.Escala },
                    _ => antes.Preferencias,
                };
                Afirmar.Igual(esperada, r.Estado.Preferencias, $"semente {semente}, {e}: só o campo do comando");
                bool mudou = antes.Preferencias != r.Estado.Preferencias;
                Afirmar.Igual(mudou ? 1 : 0, r.Efeitos.OfType<GravarPreferencias>().Count(), $"{e}: gravar só quando muda");
            };
            for (int i = 0; i < 60; i++)
            {
                sim.Avancar(TimeSpan.FromSeconds(rnd.Next(1, 40)));
                sim.Aplicar(rnd.Next(3) switch
                {
                    0 => new CmdSetEnergy((NivelDeEnergia)rnd.Next(-1, 4)),
                    1 => new CmdSetAlwaysOnTop(rnd.Next(2) == 0),
                    _ => new CmdSetScale((EscalaDoPersonagem)rnd.Next(-1, 4)),
                });
            }
        }
        Afirmar.Verdadeiro(comandos >= 2000, $"comandos aplicados: {comandos}");
    }
}
