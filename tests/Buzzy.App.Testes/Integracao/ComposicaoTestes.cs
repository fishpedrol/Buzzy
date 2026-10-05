using System.Globalization;
using Buzzy.Core;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// Testes de integração da raiz de composição (Aplicacao.cs) pedidos pela auditoria da Fase 2:
/// o menu não prende eventos na fila, o relógio de passo fixo só corre quando o núcleo pede e a
/// janela volta ao lugar do núcleo quando alguém a move por fora. Mensagens só são postadas às
/// janelas do Buzzy aberto pelo próprio teste; nada injeta input. Só rodam com --integracao.
/// </summary>
[Integracao]
internal sealed class ComposicaoTestes
{
    private static EventoDoLog EsperarDesde(BuzzyEmTeste b, long marca, Func<EventoDoLog, bool> condicao, int limiteMs, string oQue)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(limiteMs);
        while (true)
        {
            EventoDoLog? achado = BuzzyEmTeste.EventosDesde(marca).FirstOrDefault(condicao);
            if (achado is not null) return achado;
            if (b.Processo.HasExited) throw new InvalidOperationException($"O Buzzy encerrou antes de registrar: {oQue}.");
            if (DateTime.UtcNow > fim) throw new TimeoutException($"Tempo esgotado esperando no log: {oQue}.");
            Thread.Sleep(50);
        }
    }

    private static PontoPx PontoOpaco(BuzzyEmTeste b)
        => EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);

    [Teste]
    public void EventosDoSistema_BloqueioESuspensaoImediatos_DesbloqueioERetomadaDepoisDaTopologia()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        Thread.Sleep(700); // deixa terminar a releitura inicial do WM_DPICHANGED ao mostrar
        long marca = BuzzyEmTeste.MarcaDoLog();

        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_WTSSESSION_CHANGE, NativoTeste.WTS_SESSION_LOCK, 0, "WTS_SESSION_LOCK") != 0, "bloqueio entregue à janela de serviço deste Buzzy");
        EventoDoLog bloqueio = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "SessionLocked", 3000, "SESSION_LOCKED imediato");
        EventoDoLog bloqueioMensagem = EsperarDesde(b, marca, e => e.Chave == "MENSAGEM" && e["tipo"].EndsWith("WTS_SESSION_LOCK", StringComparison.Ordinal), 1000, "mensagem de bloqueio");
        List<EventoDoLog> eventos = BuzzyEmTeste.EventosDesde(marca);
        Afirmar.Verdadeiro(eventos.FindIndex(e => e.Linha == bloqueioMensagem.Linha) < eventos.FindIndex(e => e.Linha == bloqueio.Linha), "MENSAGEM precede o SESSION_LOCKED imediato");

        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_WTSSESSION_CHANGE, NativoTeste.WTS_SESSION_UNLOCK, 0, "WTS_SESSION_UNLOCK") != 0, "desbloqueio entregue à janela de serviço deste Buzzy");
        EventoDoLog desbloqueioMensagem = EsperarDesde(b, marca, e => e.Chave == "MENSAGEM" && e["tipo"].EndsWith("WTS_SESSION_UNLOCK", StringComparison.Ordinal), 1000, "mensagem de desbloqueio");
        EventoDoLog topologiaDesbloqueio = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["motivo"].Contains("WTS_SESSION_UNLOCK", StringComparison.Ordinal), 3000, "releitura antes do desbloqueio");
        EventoDoLog desbloqueio = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "SessionUnlocked", 3000, "SESSION_UNLOCKED depois da topologia");
        eventos = BuzzyEmTeste.EventosDesde(marca);
        int mensagemDesbloqueio = eventos.FindIndex(e => e.Linha == desbloqueioMensagem.Linha);
        int releituraDesbloqueio = eventos.FindIndex(e => e.Linha == topologiaDesbloqueio.Linha);
        int eventoDesbloqueio = eventos.FindIndex(e => e.Linha == desbloqueio.Linha);
        Afirmar.Verdadeiro(mensagemDesbloqueio < releituraDesbloqueio && releituraDesbloqueio < eventoDesbloqueio, "MENSAGEM, TOPOLOGIA e SESSION_UNLOCKED nessa ordem");

        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_POWERBROADCAST, NativoTeste.PBT_APMSUSPEND, 0, "PBT_APMSUSPEND") != 0, "suspensão entregue à janela de serviço deste Buzzy");
        EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "Suspending", 1000, "SUSPENDING imediato");

        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_POWERBROADCAST, NativoTeste.PBT_APMRESUMEAUTOMATIC, 0, "PBT_APMRESUMEAUTOMATIC") != 0, "retomada entregue à janela de serviço deste Buzzy");
        EventoDoLog retomadaMensagem = EsperarDesde(b, marca, e => e.Chave == "MENSAGEM" && e["tipo"].EndsWith("PBT_APMRESUMEAUTOMATIC", StringComparison.Ordinal), 1000, "mensagem de retomada");
        EventoDoLog topologiaRetomada = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["motivo"].Contains("PBT_APMRESUMEAUTOMATIC", StringComparison.Ordinal), 5000, "releitura depois do atraso mínimo");
        EventoDoLog retomada = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "Resumed", 3000, "RESUMED depois da topologia");
        eventos = BuzzyEmTeste.EventosDesde(marca);
        int mensagemRetomada = eventos.FindIndex(e => e.Linha == retomadaMensagem.Linha);
        int releituraRetomada = eventos.FindIndex(e => e.Linha == topologiaRetomada.Linha);
        int eventoRetomada = eventos.FindIndex(e => e.Linha == retomada.Linha);
        Afirmar.Verdadeiro(mensagemRetomada < releituraRetomada && releituraRetomada < eventoRetomada, "MENSAGEM, TOPOLOGIA e RESUMED nessa ordem");
        double espera = (topologiaRetomada.Instante - retomadaMensagem.Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(espera >= 1400, $"a releitura respeita o mínimo provisório de 1,5 s: {espera:0} ms");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void MenuAberto_WmCloseEncerraNaHoraSemFicarPresoNaFila()
    {
        // Regressão: antes, o laço modal do menu rodava dentro do processamento do núcleo e o
        // CMD_EXIT do WM_CLOSE ficava na fila até o menu fechar.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        PontoPx opaco = PontoOpaco(b);
        long marca = BuzzyEmTeste.MarcaDoLog();

        b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, opaco);
        b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, opaco);
        EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000, "menu exibido");

        int codigo = b.FecharPorWmClose(3000);
        Afirmar.Igual(0, codigo, "encerrou com o menu aberto");
        List<EventoDoLog> ev = BuzzyEmTeste.EventosDesde(marca);
        Afirmar.Verdadeiro(ev.Any(e => e.Chave == "NUCLEO" && e["evento"] == "CmdExit" && e["para"] == "Exiting"), "CMD_EXIT aplicado pelo núcleo");
        Afirmar.Verdadeiro(ev.Any(e => e.Chave == "ENCERRANDO"), "ENCERRANDO registrado");
    }

    [Teste]
    public void Relogio_ParadoEmRepouso_LigaNaReacaoEDesligaNoFim()
    {
        // Com a autonomia ligada: a agenda precisa voltar depois da reação.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(pausado: false);
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        EventoDoLog agenda = b.Esperar(e => e.Chave == "AGENDA" && e.Campos.ContainsKey("atrasoMs"), 3000, "agenda autônoma depois da carga");
        long atraso = long.Parse(agenda["atrasoMs"], CultureInfo.InvariantCulture);
        Afirmar.Verdadeiro(atraso >= 3000, $"a primeira decisão só vem depois do intervalo de acomodação ({atraso} ms)");

        // Critério 3 da Fase 2, no app: em repouso (IDLE sem gesto), o relógio de passo fixo não liga.
        Thread.Sleep(700);
        Afirmar.Falso(b.Eventos().Any(e => e.Chave == "RELOGIO" && e["ligado"] == "sim"), "sem relógio em repouso depois da carga");

        long marca = BuzzyEmTeste.MarcaDoLog();
        PontoPx opaco = PontoOpaco(b);
        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, opaco);
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, opaco);
        EsperarDesde(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "sim" && e["evento"] == "Click", 3000, "relógio ligado pela reação ao clique");
        EsperarDesde(b, marca, e => e.Chave == "RELOGIO" && e["ligado"] == "nao", 3000, "relógio desligado no fim da reação");
        EsperarDesde(b, marca, e => e.Chave == "AGENDA" && e["cancelada"] == "sim", 3000, "agenda cancelada durante o gesto");
        EventoDoLog nova = EsperarDesde(b, marca, e => e.Chave == "AGENDA" && e.Campos.ContainsKey("atrasoMs"), 3000, "agenda de volta depois da reação");
        Afirmar.Verdadeiro(long.Parse(nova["atrasoMs"], CultureInfo.InvariantCulture) >= 3000, "a autonomia volta só depois do intervalo de acomodação");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void JanelaMovidaPorFora_VoltaAoLugarDoNucleoNaReleituraDaTopologia()
    {
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        RetanguloPx lugar = b.RetanguloDaJanela();
        long marca = BuzzyEmTeste.MarcaDoLog();

        // Outro agente (aqui, o teste) move a janela sem passar pelo núcleo.
        Afirmar.Verdadeiro(b.MoverPorFora(b.Janela, 0, lugar.Esquerda - 300, lugar.Topo - 200, NativoTeste.SWP_NOZORDER, "mover a janela"), "janela movida por fora");
        Afirmar.Diferente(lugar, b.RetanguloDaJanela(), "a janela saiu do lugar");

        // Uma releitura de topologia (sem mudança real) reafirma o lugar do núcleo.
        Afirmar.Verdadeiro(b.Postar(b.Servico, NativoTeste.WM_DISPLAYCHANGE, 32, (nint)(1920 | (1080 << 16)), "WM_DISPLAYCHANGE"), "WM_DISPLAYCHANGE postada");
        EsperarDesde(b, marca, e => e.Chave == "POSICAO" && e["reaplicada"] == "sim", 3000, "lugar reaplicado");
        b.EsperarRetangulo(lugar, 3000, "de volta ao lugar do núcleo");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void ReafirmacaoTardia_JanelaMovidaDepoisDaReleitura_VoltaAoLugar_UmaVezSo()
    {
        // Fase 5, passo P9 (D14 do desenho dos monitores): com "Lembrar locais das janelas", o Windows pode devolver a janela
        // ao monitor reconectado DEPOIS da releitura. A conferência tardia, 1,5 s depois de cada releitura publicada,
        // reafirma o lugar do núcleo, uma vez: nada periódico.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        RetanguloPx lugar = b.RetanguloDaJanela();
        long marca = BuzzyEmTeste.MarcaDoLog();

        Afirmar.Verdadeiro(b.Postar(b.Servico, NativoTeste.WM_DISPLAYCHANGE, 32, (nint)(1920 | (1080 << 16)), "WM_DISPLAYCHANGE"), "WM_DISPLAYCHANGE postada");
        EventoDoLog releitura = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["motivo"] == "WM_DISPLAYCHANGE", 3000, "releitura da topologia");

        // Depois da releitura, outro agente (aqui, o teste) move a janela sem passar pelo núcleo.
        long marcaMovida = BuzzyEmTeste.MarcaDoLog();
        RetanguloPx fora = lugar.Deslocado(-300, -200);
        Afirmar.Verdadeiro(b.MoverPorFora(b.Janela, 0, fora.Esquerda, fora.Topo, NativoTeste.SWP_NOZORDER, "mover a janela"), "janela movida por fora depois da releitura");
        Afirmar.Igual(fora, b.RetanguloDaJanela(), "a janela saiu do lugar");

        EventoDoLog reaplicada = EsperarDesde(b, marcaMovida, e => e.Chave == "POSICAO" && e["reaplicada"] == "sim", 4000, "lugar reaplicado pela conferência tardia");
        Afirmar.Igual("reafirmação tardia", reaplicada["motivo"], "o motivo da reaplicação");
        double ms = (reaplicada.Instante - releitura.Instante).TotalMilliseconds;
        Afirmar.Verdadeiro(ms >= 1400 && ms < 3000, $"1,5 s depois da releitura: {ms:0} ms");
        Afirmar.Igual(fora, EventoDoLog.Retangulo(reaplicada["real"]), "o log traz onde a janela estava");
        b.EsperarRetangulo(lugar, 2000, "de volta ao lugar do núcleo");

        // Uma vez por releitura: movida de novo, sem mensagem nova, a janela fica onde foi posta.
        long marcaDeNovo = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Verdadeiro(b.MoverPorFora(b.Janela, 0, fora.Esquerda, fora.Topo, NativoTeste.SWP_NOZORDER, "mover a janela"), "janela movida por fora de novo");
        Thread.Sleep(2500);
        Afirmar.Igual(fora, b.RetanguloDaJanela(), "nenhuma conferência sem releitura: nada periódico");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marcaDeNovo).Any(e => e.Chave is "POSICAO" or "TOPOLOGIA"), "nem reaplicação nem releitura sem mensagem");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void MinimizadoNaTrocaDeMonitores_NaoEsconde_SemTrocaEsconde()
    {
        // Fase 5, passo P12 (DEC-031, adendo): com "Minimizar janelas quando um monitor for desconectado", o Windows minimiza
        // as janelas do monitor que sai. Com uma releitura da topologia pendente, a minimização é do sistema: a janela volta,
        // o personagem não se esconde e a releitura o mantém no lugar do núcleo. Sem nada pendente e com a mesma topologia,
        // minimizar continua escondendo (Q-03). A mensagem de área útil vai por envio síncrono, como o Windows faz, e a
        // minimização é "por fora" (SW_SHOWMINNOACTIVE), as duas só às janelas deste Buzzy.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        Thread.Sleep(2500); // a releitura do WM_DPICHANGED da partida e a conferência tardia dela passam
        RetanguloPx lugar = b.RetanguloDaJanela();

        long marca = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_SETTINGCHANGE, NativoTeste.SPI_SETWORKAREA, 0, "WM_SETTINGCHANGE") != 0, "WM_SETTINGCHANGE(SPI_SETWORKAREA) entregue: a releitura fica pendente");
        b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
        EventoDoLog minimizado = EsperarDesde(b, marca, e => e.Chave == "MINIMIZADO" && e["janela"] == "personagem", 3000, "a minimização registrada");
        Afirmar.Igual(("sistema", "sim"), (minimizado["pelo"], minimizado["releituraPendente"]), "do sistema, com a releitura pendente");
        EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["motivo"].Contains("SPI_SETWORKAREA", StringComparison.Ordinal), 3000, "a releitura");
        Thread.Sleep(500);
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => (e.Chave == "VISIVEL" && e["visivel"] == "nao") || (e.Chave == "NUCLEO" && e["evento"] == "CmdHide")), "não se escondeu");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "a janela continua à vista");
        b.EsperarRetangulo(lugar, 2000, "no lugar do núcleo");

        Thread.Sleep(2000); // a conferência tardia passa: nada fica pendente na agenda
        long marcaDoUsuario = BuzzyEmTeste.MarcaDoLog();
        b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
        EventoDoLog doUsuario = EsperarDesde(b, marcaDoUsuario, e => e.Chave == "MINIMIZADO" && e["janela"] == "personagem", 3000, "a segunda minimização registrada");
        Afirmar.Igual(("usuario", "nao"), (doUsuario["pelo"], doUsuario["releituraPendente"]), "sem troca de monitores, do usuário");
        EsperarDesde(b, marcaDoUsuario, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "minimizar esconde (Q-03)");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void MinimizadoJaEscondidoPelaSessao_NaoViraCmdHide_EODesbloqueioOMostra()
    {
        // Fase 5, passo P12 (D10 do desenho do sistema): escondido por qualquer motivo, uma minimização nunca vira CMD_HIDE.
        // Pela precedência, ela trocaria a ocultação da sessão pela do usuário, e o desbloqueio não o mostraria mais. Aqui o
        // bloqueio é a mensagem WTS enviada ao HWND de serviço deste Buzzy, e a minimização é "por fora".
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        Thread.Sleep(2500); // a releitura da partida e a conferência tardia passam

        long marca = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_WTSSESSION_CHANGE, NativoTeste.WTS_SESSION_LOCK, 0, "WTS_SESSION_LOCK") != 0, "bloqueio entregue");
        EsperarDesde(b, marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "escondido pelo bloqueio");
        b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
        EventoDoLog minimizado = EsperarDesde(b, marca, e => e.Chave == "MINIMIZADO" && e["janela"] == "personagem", 3000, "a minimização registrada");
        Afirmar.Igual("sim", minimizado["escondido"], "já estava escondido");
        Thread.Sleep(300);
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "NUCLEO" && e["evento"] == "CmdHide"), "nenhum CMD_HIDE");
        Afirmar.Falso(NativoTeste.IsWindowVisible(b.Janela), "a janela não fica à vista");

        long marcaDoDesbloqueio = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_WTSSESSION_CHANGE, NativoTeste.WTS_SESSION_UNLOCK, 0, "WTS_SESSION_UNLOCK") != 0, "desbloqueio entregue");
        EsperarDesde(b, marcaDoDesbloqueio, e => e.Chave == "VISIVEL" && e["visivel"] == "sim", 4000, "o desbloqueio o mostra: a ocultação continuou a da sessão");
        Afirmar.Verdadeiro(NativoTeste.IsWindowVisible(b.Janela), "à vista de novo");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }

    [Teste]
    public void Mostrar_ReleATopologiaNaHora_SemArmarAConferenciaTardia()
    {
        // Fase 5, passo P11 (DEC-030, pendência do P9): mostrar por comando relê a topologia na hora, numa função própria,
        // antes do CMD_SHOW, e registra essa leitura (TOPOLOGIA|imediata=sim). Ela não passa pela agenda das mensagens do
        // Windows e não arma a conferência tardia: movida por fora depois do mostrar, a janela fica onde foi posta. Aqui o
        // pedido vem da segunda instância, com o Buzzy à vista, para nenhuma mensagem de topologia entrar no meio.
        using BuzzyEmTeste b = BuzzyEmTeste.Iniciar();
        b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
        Thread.Sleep(2500); // a releitura do WM_DPICHANGED da partida e a conferência tardia dela passam
        RetanguloPx lugar = b.RetanguloDaJanela();
        long marca = BuzzyEmTeste.MarcaDoLog();

        SegundaInstancia segunda = BuzzyEmTeste.AbrirSegundaInstancia();
        Afirmar.Verdadeiro(segunda.SaiuSozinha, $"a segunda instância sai sozinha ({segunda.Encerramento ?? "saiu"})");
        EventoDoLog releitura = EsperarDesde(b, marca, e => e.Chave == "TOPOLOGIA" && e["imediata"] == "sim", 3000, "a releitura imediata do mostrar");
        Afirmar.Igual("revalidar antes de mostrar: segunda instância", releitura["motivo"], "o motivo da releitura imediata");
        Afirmar.Verdadeiro(releitura.Campos.ContainsKey("chaves"), "a releitura imediata leva as chaves, como a agrupada");
        EsperarDesde(b, marca, e => e.Chave == "VISIVEL" && e["visivel"] == "sim" && e["motivo"] == "segunda instância", 3000, "mostrado pela segunda instância");
        b.EsperarRetangulo(lugar, 2000, "no mesmo lugar");

        long marcaMovida = BuzzyEmTeste.MarcaDoLog();
        RetanguloPx fora = lugar.Deslocado(-300, -200);
        Afirmar.Verdadeiro(b.MoverPorFora(b.Janela, 0, fora.Esquerda, fora.Topo, NativoTeste.SWP_NOZORDER, "mover a janela"), "janela movida por fora depois do mostrar");
        Thread.Sleep(2500);
        Afirmar.Igual(fora, b.RetanguloDaJanela(), "sem conferência tardia depois do mostrar");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "TOPOLOGIA" && e["imediata"] != "sim"), "nenhuma releitura agrupada no meio");
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marcaMovida).Any(e => e.Chave == "POSICAO" && e["reaplicada"] == "sim"), "nenhuma reaplicação do lugar");

        Afirmar.Igual(0, b.FecharPorWmClose());
    }
}
