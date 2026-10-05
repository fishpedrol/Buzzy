using System.Globalization;
using System.IO;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes.Integracao;

/// <summary>
/// A persistência ligada no Buzzy.exe de verdade (Fase 5, passo P7; DEC-029): soltar, fechar e reabrir no mesmo lugar
/// (S2 e S7 nesta máquina, com o secundário à esquerda, em x negativo, e o principal à direita dele), a postura (preso na
/// parede, escondido na lateral) e a emoção dominante voltando ao reabrir, e a partida com um arquivo ilegível. Os gestos
/// são mensagens POSTADAS às janelas do próprio Buzzy, com o PID conferido antes de cada uma: nada passa pela fila de
/// input do Windows nem por outro aplicativo. Tudo no perfil de teste <c>persistencia</c>
/// (<c>%LOCALAPPDATA%\Buzzy\testes\persistencia</c>), lido e escrito pelo teste só lá; os arquivos reais do usuário, na
/// pasta do Buzzy, são conferidos só por fora (existência, tamanho e datas), antes e depois de cada teste, e nunca lidos.
/// Só rodam com --integracao, depois de avisar o usuário: o Buzzy aparece e some na tela.
/// </summary>
[Integracao]
internal sealed class PersistenciaIntegracaoTestes
{
    private const string Perfil = "persistencia";

    private static readonly TamanhoDip Sprite = new(128, 128);

    // ------------------------------------------------------------------ apoio

    private static RetanguloPx SpriteEm(PontoPx ancora, TamanhoPx tamanho)
    {
        int esquerda = ancora.X - tamanho.Largura / 2;
        return new RetanguloPx(esquerda, ancora.Y - tamanho.Altura, esquerda + tamanho.Largura, ancora.Y);
    }

    private static PontoPx Ancora(RetanguloPx janela) => new(janela.Esquerda + janela.Largura / 2, janela.Base);

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

    private static EventoDoLog Nucleo(BuzzyEmTeste b, long marca, string evento, string de, string para, int limiteMs, string oQue)
        => EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == evento && (de.Length == 0 || e["de"] == de) && e["para"] == para, limiteMs, oQue);

    /// <summary>A pasta do perfil de teste destes testes, em <c>%LOCALAPPDATA%\Buzzy\testes</c>.</summary>
    private static string PastaDoPerfil() => Afirmar.NaoNulo(PastaDeDados.DoPerfilDeTeste(Perfil), "pasta do perfil de teste");

    /// <summary>O settings.json do perfil de teste, lido pelo esquema.</summary>
    private static LeituraDasConfiguracoes LerDoPerfil()
    {
        string principal = Path.Combine(PastaDoPerfil(), ArquivoDeConfiguracoes.NomePrincipal);
        Afirmar.Verdadeiro(File.Exists(principal), "o Buzzy gravou o settings.json do perfil de teste");
        return EsquemaDeConfiguracoes.Ler(File.ReadAllBytes(principal));
    }

    /// <summary>
    /// Roda o cenário e confere que os arquivos reais do usuário ficaram como estavam, por fora (<see cref="ArquivosReais"/>,
    /// só metadados). A integração inteira também confere, antes e depois de todos os testes (Programa).
    /// </summary>
    private static void SemTocarNosArquivosReais(Action cenario)
    {
        Afirmar.NaoNulo(PastaDeDados.DoBuzzy(), "pasta do Buzzy");
        string antes = ArquivosReais.Foto();
        cenario();
        Afirmar.Igual(antes, ArquivosReais.Foto(), "os arquivos reais do usuário, em %LOCALAPPDATA%\\Buzzy, continuam como estavam");
    }

    /// <summary>
    /// Arrasta o personagem por mensagens postadas à janela dele: pressiona no corpo, passa do limiar e leva a âncora a
    /// <paramref name="ancora"/>, conferindo a janela a cada movimento, e solta lá. Devolve a marca do log de antes do
    /// arraste e o DRAG_END que entrou no núcleo.
    /// </summary>
    private static (long Marca, EventoDoLog Soltou) Arrastar(BuzzyEmTeste b, PontoPx ancora, TamanhoPx tamanhoNoDestino)
    {
        RetanguloPx inicio = b.RetanguloDaJanela();
        PontoPx ancoraInicial = Ancora(inicio);
        var pegar = new PontoPx(ancoraInicial.X, ancoraInicial.Y - inicio.Altura / 3);
        var pegada = new PontoPx(pegar.X - ancoraInicial.X, pegar.Y - ancoraInicial.Y);
        long marca = BuzzyEmTeste.MarcaDoLog();

        b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, pegar);
        Nucleo(b, marca, "Press", "", "Pressed", 3000, "PRESS no corpo");
        var meio = new PontoPx(pegar.X - 60, pegar.Y - 60);
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, meio);
        Nucleo(b, marca, "DragStart", "Pressed", "Dragging", 3000, "DRAG_START");
        b.EsperarRetangulo(SpriteEm(new PontoPx(meio.X - pegada.X, meio.Y - pegada.Y), inicio.Tamanho), 3000, "a janela no cursor menos a pegada");

        var cursor = new PontoPx(ancora.X + pegada.X, ancora.Y + pegada.Y);
        b.PostarMouse(NativoTeste.WM_MOUSEMOVE, NativoTeste.MK_LBUTTON, cursor);
        b.EsperarRetangulo(SpriteEm(ancora, tamanhoNoDestino), 3000, "a janela no destino do arraste");
        b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, cursor);
        EventoDoLog soltou = Nucleo(b, marca, "DragEnd", "Dragging", "Settling", 3000, "DRAG_END");
        return (marca, soltou);
    }

    /// <summary>O clique duplo por mensagens postadas no ponto dado (botão pressionado, solto, pressionado, solto).</summary>
    private static void CliqueDuplo(BuzzyEmTeste b, PontoPx ponto)
    {
        foreach (int msg in new[] { NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP, NativoTeste.WM_LBUTTONDOWN, NativoTeste.WM_LBUTTONUP })
            b.PostarMouse(msg, msg == NativoTeste.WM_LBUTTONDOWN ? NativoTeste.MK_LBUTTON : 0, ponto);
    }

    /// <summary>Fecha pelo WM_CLOSE (CMD_EXIT no núcleo) e devolve as linhas CONFIG de gravação da saída.</summary>
    private static List<EventoDoLog> FecharEGravar(BuzzyEmTeste b)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        Afirmar.Igual(0, b.FecharPorWmClose(), "código de saída");
        return [.. BuzzyEmTeste.EventosDesde(marca).Where(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("gravado"))];
    }

    /// <summary>Espera a janela ficar no lugar e não sair dele por <paramref name="duracaoMs"/>, sem o relógio ligar.</summary>
    private static void FicaParado(BuzzyEmTeste b, RetanguloPx lugar, int duracaoMs, string oQue)
    {
        b.EsperarRetangulo(lugar, 3000, oQue);
        long marca = BuzzyEmTeste.MarcaDoLog();
        var fim = DateTime.UtcNow.AddMilliseconds(duracaoMs);
        while (DateTime.UtcNow < fim)
        {
            Afirmar.Igual(lugar, b.RetanguloDaJanela(), $"{oQue}: não sai do lugar");
            Thread.Sleep(50);
        }
        Afirmar.Falso(BuzzyEmTeste.EventosDesde(marca).Any(e => e.Chave == "RELOGIO" && e["ligado"] == "sim"), $"{oQue}: o relógio não ligou");
    }

    // ------------------------------------------------------------------ cenários

    // S2 e S7 (TODO.md, Fase 5): o personagem solto a 25% da área útil do monitor em x negativo (o secundário à esquerda;
    // numa máquina com um monitor só, o principal), no chão. A gravação com atraso cai de 1,9 a 4 s depois do soltar; a
    // saída não grava de novo o que já está no disco; o arquivo do perfil é a v3, com a chave estável do monitor e a tela
    // dele; e o Buzzy reaberto lê o principal e volta ao mesmo retângulo, restaurado pela chave.
    [Teste]
    public void SoltarNoMonitorEmXNegativo_FecharEReabrir_VoltaAoMesmoLugar()
    {
        SemTocarNosArquivosReais(() =>
        {
            LeituraDaTopologia leitura = Afirmar.NaoNulo(LeitorDeTopologia.LerDetalhado(out string? erro), erro);
            Topologia t = leitura.Topologia;
            MonitorDoDesktop alvo = t.Monitores.FirstOrDefault(m => m.Tela.Esquerda < 0) ?? t.Principal;
            Console.WriteLine($"         destino: {(alvo.Principal ? "o principal (nenhum monitor em x negativo)" : $"{leitura.NomeGdi(alvo.Chave)}, em x negativo, com o principal à direita (S2 e S7)")}");
            TamanhoPx tamanho = Sprite.ParaPixels(alvo.Dpi);
            var ancora = new PontoPx(alvo.AreaUtil.Esquerda + alvo.AreaUtil.Largura / 4, alvo.AreaUtil.Base);
            RetanguloPx esperado = SpriteEm(ancora, tamanho);

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                EventoDoLog partida = b.Esperar(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"), 5000, "a leitura das configurações na partida");
                Afirmar.Igual(("padroes", "Ausente", "perfil"), (partida["lido"], partida["principal"], partida["pasta"]), "perfil limpo: os padrões, sem arquivo");
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");

                (long marca, EventoDoLog soltou) = Arrastar(b, ancora, tamanho);
                Nucleo(b, marca, "DragEnd", "Settling", "Idle", 3000, "no chão do destino");
                b.EsperarRetangulo(esperado, 3000, "no chão, a 25% da área útil do destino");
                EventoDoLog gravado = EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["gravado"] == "sim" && e["motivo"] == "atraso", 8000, "a gravação com atraso");
                TimeSpan atraso = gravado.Instante - soltou.Instante;
                Console.WriteLine($"         gravação {atraso.TotalMilliseconds:0} ms depois do soltar, em {gravado["ms"]} ms, {gravado["bytes"]} bytes");
                Afirmar.Verdadeiro(atraso >= TimeSpan.FromSeconds(1.9) && atraso <= TimeSpan.FromSeconds(4), $"a gravação cai de 1,9 a 4 s depois do soltar: {atraso.TotalMilliseconds:0} ms");
                Afirmar.Igual("Ausente", gravado["principalAntes"], "a primeira gravação cria o principal");
                EventoDoLog posicao = BuzzyEmTeste.EventosDesde(marca).Last(e => e.Chave == "POSICAO");
                Afirmar.Igual(alvo.Chave, posicao["monitor"], "solto no monitor de destino, pela chave estável");

                List<EventoDoLog> saida = FecharEGravar(b);
                Afirmar.Sequencia(["sem mudanca/CmdExit"], saida.Select(e => $"{e["gravado"]}/{e["motivo"]}"), "a saída não regrava o que já está no disco");
            }

            LeituraDasConfiguracoes lida = LerDoPerfil();
            Afirmar.Igual((SituacaoDaLeitura.Valida, (int?)EsquemaDeConfiguracoes.VersaoAtual, 0), (lida.Situacao, lida.Versao, lida.Avisos.Count), "o arquivo do perfil é a versão atual, sem aviso");
            PosicaoDoPersonagem salva = Afirmar.NaoNulo(lida.Configuracoes.Posicao, "posição salva");
            Afirmar.Igual((alvo.Chave, (RetanguloPx?)alvo.Tela), (salva.ChaveMonitor, salva.TelaDoMonitor), "a chave estável e a tela do monitor");
            Afirmar.Igual((LadoDoEsconderijo.Nenhum, false), (lida.Configuracoes.Esconderijo, lida.Configuracoes.PresoPeloUsuario), "no chão, sem esconderijo e solto");

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog partida = b.Esperar(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"), 5000, "a leitura das configurações na partida");
                Afirmar.Igual(("principal", "Valido", "atual"), (partida["lido"], partida["principal"], partida["versao"]), "reaberto, lê o principal, da versão atual do esquema");
                EventoDoLog carga = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Booting", 5000, "a carga");
                Afirmar.Igual("BOOTING: configurações e topologia carregadas; posição salva restaurada pela chave", carga["regra"], "restaurada pela chave");
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling" && e["para"] == "Idle", 5000, "de pé no chão");
                b.EsperarRetangulo(esperado, 3000, "no mesmo retângulo em que foi solto");
                Afirmar.Sequencia(["sem mudanca/CmdExit"], FecharEGravar(b).Select(e => $"{e["gravado"]}/{e["motivo"]}"), "nada mudou: nada regravado");
            }
        });
    }

    // A regra do gesto (L5 da crítica) ligada na raiz (revisão de correção do bloco P6-P9, achado 5): soltar o personagem
    // pede a gravação com atraso (2 s); pressionado de novo antes disso e segurado, o disparo chega no meio do gesto. Ele não
    // grava nem rearma a espera (uma linha adiado=gesto, por mais que o botão fique pressionado: nada periódico, nem com o
    // ClickLock), e o fim do gesto (o clique) grava. Os gestos são mensagens postadas às janelas do próprio Buzzy.
    [Teste]
    public void DisparoDaGravacaoNoMeioDoGesto_EsperaOFimSemRearmar_EOFimDoGestoGrava()
    {
        SemTocarNosArquivosReais(() =>
        {
            PontoPx destino;
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                RetanguloPx inicio = b.RetanguloDaJanela();
                PontoPx ancoraInicial = Ancora(inicio);
                destino = new PontoPx(ancoraInicial.X - 300, ancoraInicial.Y);
                RetanguloPx esperado = SpriteEm(destino, inicio.Tamanho);

                (long marca, EventoDoLog soltou) = Arrastar(b, destino, inicio.Tamanho);
                Nucleo(b, marca, "DragEnd", "Settling", "Idle", 3000, "no chão, 300 px à esquerda");
                b.EsperarRetangulo(esperado, 3000, "no lugar do soltar");
                EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["pedido"] == "posicao" && e["evento"] == "DragEnd" && e["imediata"] == "nao", 3000, "o pedido com atraso");

                // De novo no corpo, antes dos 2 s, e segurado.
                long marcaDoGesto = BuzzyEmTeste.MarcaDoLog();
                var corpo = new PontoPx(destino.X, destino.Y - esperado.Altura / 3);
                b.PostarMouse(NativoTeste.WM_LBUTTONDOWN, NativoTeste.MK_LBUTTON, corpo);
                Nucleo(b, marcaDoGesto, "Press", "Idle", "Pressed", 3000, "pressionado de novo");
                EventoDoLog adiado = EsperarDesde(b, marcaDoGesto, e => e.Chave == "CONFIG" && e["adiado"] == "gesto", 5000, "o disparo adiado pelo gesto");
                double ms = (adiado.Instante - soltou.Instante).TotalMilliseconds;
                Afirmar.Verdadeiro(ms >= 1900 && ms < 4000, $"o disparo, 2 s depois do soltar: {ms:0} ms");
                Afirmar.Igual("atraso", adiado["motivo"], "era o disparo do atraso");

                // Segura mais 2,5 s: nada grava e nada rearma.
                Thread.Sleep(2500);
                List<EventoDoLog> noGesto = [.. BuzzyEmTeste.EventosDesde(marcaDoGesto).Where(e => e.Chave == "CONFIG")];
                Afirmar.Igual(1, noGesto.Count(e => e["adiado"] == "gesto"), "adiado uma vez, sem rearmar a espera");
                Afirmar.Falso(noGesto.Any(e => e.Campos.ContainsKey("gravado")), "nada gravado no meio do gesto");

                // Soltar sem mover é um clique: o fim do gesto grava, uma vez.
                long marcaDoFim = BuzzyEmTeste.MarcaDoLog();
                b.PostarMouse(NativoTeste.WM_LBUTTONUP, 0, corpo);
                Nucleo(b, marcaDoFim, "Click", "Pressed", "Reacting", 3000, "o clique");
                EventoDoLog gravado = EsperarDesde(b, marcaDoFim, e => e.Chave == "CONFIG" && e.Campos.ContainsKey("gravado"), 3000, "a gravação no fim do gesto");
                Afirmar.Igual(("sim", "fimDoGesto"), (gravado["gravado"], gravado["motivo"]), "gravado pelo fim do gesto");

                List<EventoDoLog> saida = FecharEGravar(b);
                Afirmar.Sequencia(["sem mudanca/CmdExit"], saida.Select(e => $"{e["gravado"]}/{e["motivo"]}"), "a saída não regrava o que o fim do gesto gravou");
            }

            PosicaoDoPersonagem salva = Afirmar.NaoNulo(LerDoPerfil().Configuracoes.Posicao, "posição salva");
            Afirmar.Igual(destino, salva.AncoraAbsoluta, "o arquivo do perfil tem o lugar do soltar");
        });
    }

    // A descarga na suspensão (Fase 5, passo P11; DEC-031): soltar pede a gravação com atraso, e minimizar esconde o
    // personagem, com o pedido ainda pendente. Escondido pelo usuário, a SUSPENDING não muda o estado nem pede gravação ao
    // núcleo; mesmo assim, o tratador da suspensão descarrega o pendente na hora, sem esperar o atraso de 2 s, que, com a
    // máquina dormindo, só cairia depois de acordar (ou nunca, se a bateria acabar). A mensagem vai ao HWND de serviço deste
    // Buzzy de teste por envio síncrono, como o Windows faz.
    [Teste]
    public void Suspensao_ComOPersonagemEscondido_DescarregaAGravacaoPendenteNaHora()
    {
        SemTocarNosArquivosReais(() =>
        {
            PontoPx destino;
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                RetanguloPx inicio = b.RetanguloDaJanela();
                destino = new PontoPx(Ancora(inicio).X - 300, Ancora(inicio).Y);

                (long marca, _) = Arrastar(b, destino, inicio.Tamanho);
                Nucleo(b, marca, "DragEnd", "Settling", "Idle", 3000, "no chão, 300 px à esquerda");
                EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["pedido"] == "posicao" && e["evento"] == "DragEnd" && e["imediata"] == "nao", 3000, "o pedido com atraso");

                b.MinimizarPorFora(b.Janela, "SW_SHOWMINNOACTIVE");
                EsperarDesde(b, marca, e => e.Chave == "VISIVEL" && e["visivel"] == "nao", 3000, "escondido ao ser minimizado");

                long marcaDaSuspensao = BuzzyEmTeste.MarcaDoLog();
                Afirmar.Verdadeiro(BuzzyEmTeste.EventosDesde(marca).All(e => !(e.Chave == "CONFIG" && e.Campos.ContainsKey("gravado"))), "nada gravado antes da suspensão: o pedido continua pendente");
                Afirmar.Verdadeiro(b.Enviar(b.Servico, NativoTeste.WM_POWERBROADCAST, NativoTeste.PBT_APMSUSPEND, 0, "PBT_APMSUSPEND") != 0, "suspensão entregue à janela de serviço deste Buzzy");
                EventoDoLog mensagem = EsperarDesde(b, marcaDaSuspensao, e => e.Chave == "MENSAGEM" && e["tipo"].EndsWith("PBT_APMSUSPEND", StringComparison.Ordinal), 1000, "a mensagem da suspensão");
                EventoDoLog gravado = EsperarDesde(b, marcaDaSuspensao, e => e.Chave == "CONFIG" && e.Campos.ContainsKey("gravado"), 3000, "a gravação depois da suspensão");
                Afirmar.Igual(("sim", "Suspending"), (gravado["gravado"], gravado["motivo"]), "o pendente gravado pela suspensão, não pelo atraso");
                double ms = (gravado.Instante - mensagem.Instante).TotalMilliseconds;
                Afirmar.Verdadeiro(ms < 500, $"na hora, no tratamento da mensagem: {ms:0} ms");

                // Nada sobra agendado: o atraso foi cancelado pela descarga.
                Thread.Sleep(2500);
                Afirmar.Igual(1, BuzzyEmTeste.EventosDesde(marcaDaSuspensao).Count(e => e.Chave == "CONFIG" && e["gravado"] == "sim"), "uma gravação só: o disparo com atraso foi cancelado");
                Afirmar.Igual(0, b.FecharPorWmClose(), "código de saída");
            }

            PosicaoDoPersonagem salva = Afirmar.NaoNulo(LerDoPerfil().Configuracoes.Posicao, "posição salva");
            Afirmar.Igual(destino, salva.AncoraAbsoluta, "o arquivo do perfil tem o lugar do soltar");
        });
    }

    // A postura gravada com a posição (esquema v3; DEC-029, item 11): solto junto à lateral direita do principal, ele fica
    // preso na parede; fechado e reaberto, volta grudado no mesmo lugar e, pausado, não desce (quem não estivesse preso
    // desceria, pela regra da calma). Escondido atrás dessa lateral pelo clique duplo, fechado e reaberto, volta escondido
    // na mesma borda, no mesmo lugar.
    [Teste]
    public void PresoNaParedeEEscondidoNaLateral_FecharEReabrir_VoltamComoEstavam()
    {
        SemTocarNosArquivosReais(() =>
        {
            Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
            MonitorDoDesktop principal = t.Principal;
            TamanhoPx tamanho = Sprite.ParaPixels(principal.Dpi);
            Superficies sup = Superficies.Do(t, principal, tamanho);
            int meio = principal.AreaUtil.Topo + principal.AreaUtil.Altura / 2;
            RetanguloPx naParede = SpriteEm(new PontoPx(sup.Direita, meio), tamanho);

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                (long marca, _) = Arrastar(b, new PontoPx(sup.Direita - 30, meio), tamanho);
                EventoDoLog agarrou = Nucleo(b, marca, "DragEnd", "Settling", "Climbing", 3000, "grudado na parede");
                Afirmar.Igual("SETTLING: solto junto a uma lateral, fica grudado na parede", agarrou["regra"]);
                b.EsperarRetangulo(naParede, 3000, "encostado na lateral direita");
                // A saída grava, ou encontra no disco o que a gravação com atraso já pôs lá, se ela veio antes de fechar.
                List<EventoDoLog> saida = FecharEGravar(b);
                Afirmar.Verdadeiro(saida.Count == 1 && saida[0]["gravado"] is "sim" or "sem mudanca" && saida[0]["motivo"] == "CmdExit",
                    $"a saída grava na hora ({string.Join("; ", saida.Select(e => e.Linha))})");
            }
            LeituraDasConfiguracoes preso = LerDoPerfil();
            Afirmar.Igual((LadoDoEsconderijo.Nenhum, true), (preso.Configuracoes.Esconderijo, preso.Configuracoes.PresoPeloUsuario), "o arquivo guarda a marca de preso");

            RetanguloPx escondido;
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog voltou = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling", 5000, "a acomodação da carga");
                Afirmar.Igual(("Climbing", "SETTLING: solto junto a uma lateral, fica grudado na parede"), (voltou["para"], voltou["regra"]), "reaberto, grudado na parede");
                FicaParado(b, naParede, 1500, "preso e pausado, fica na parede");

                long marca = BuzzyEmTeste.MarcaDoLog();
                RetanguloPx agora = b.RetanguloDaJanela();
                CliqueDuplo(b, new PontoPx((agora.Esquerda + agora.Direita) / 2, agora.Topo + agora.Altura / 2));
                EventoDoLog escondeu = Nucleo(b, marca, "DoubleClick", "Settling", "Peeking", 3000, "escondido pelo clique duplo");
                Afirmar.Igual("SETTLING: escondido atrás da borda (Direita)", escondeu["regra"], "atrás da lateral direita");
                Thread.Sleep(300);
                escondido = b.RetanguloDaJanela();
                List<EventoDoLog> saida = FecharEGravar(b);
                Afirmar.Verdadeiro(saida.Any(e => e["gravado"] == "sim"), $"a saída grava a borda ({string.Join("; ", saida.Select(e => e.Linha))})");
            }
            LeituraDasConfiguracoes naBorda = LerDoPerfil();
            Afirmar.Igual((LadoDoEsconderijo.Direita, true), (naBorda.Configuracoes.Esconderijo, naBorda.Configuracoes.PresoPeloUsuario),
                "o arquivo guarda a borda, e a marca de preso continua guardada para quando ele sair do esconderijo");

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog voltou = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Settling", 5000, "a acomodação da carga");
                Afirmar.Igual(("Peeking", "SETTLING: escondido atrás da borda (Direita)"), (voltou["para"], voltou["regra"]), "reaberto, escondido na mesma borda");
                FicaParado(b, escondido, 1000, "escondido, no mesmo lugar");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
        });
    }

    // A emoção dominante escolhida pelo menu (DEC-027) vai para o settings.json com atraso e volta ao reabrir: a carga já
    // começa com a cara dela, e o menu a mostra marcada. As teclas são WM_CHAR postadas ao dono do menu, deste Buzzy.
    [Teste]
    public void EmocaoEscolhidaPeloMenu_FecharEReabrir_VoltaComACaraEAMarca()
    {
        SemTocarNosArquivosReais(() =>
        {
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
                long marca = BuzzyEmTeste.MarcaDoLog();
                EventoDoLog fechado = EscolherNoMenu(b, opaco, 'd', 'f');
                Afirmar.Igual(("Emocao", "Feliz"), (fechado["fechado"], fechado["argumento"]), "o menu escolheu Feliz");
                EventoDoLog pedido = EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["pedido"] == "preferencias", 3000, "o pedido de gravação das preferências");
                Afirmar.Igual(("CmdSetDominantEmotion", "nao"), (pedido["evento"], pedido["imediata"]), "com atraso");
                EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["gravado"] == "sim" && e["motivo"] == "atraso", 8000, "a emoção gravada com atraso");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
            Afirmar.Igual<Expressao?>(Expressao.Feliz, LerDoPerfil().Configuracoes.Preferencias.EmocaoDominante, "o arquivo guarda a emoção");

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                b.Esperar(e => e.Chave == "SPRITE" && e["pose"] == "parado" && e["expressao"] == "feliz", 5000, "reaberto, parado com a cara feliz");
                PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
                long marca = BuzzyEmTeste.MarcaDoLog();
                b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, opaco);
                b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, opaco);
                EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000, "menu exibido");
                Afirmar.Igual("Feliz", exibindo["emocaoMarcada"], "o menu mostra a emoção restaurada");
                var dono = (nint)long.Parse(exibindo["dono"], CultureInfo.InvariantCulture);
                Afirmar.Igual((uint)b.Processo.Id, NativoTeste.PidDe(dono), "o dono do menu é deste Buzzy");
                Afirmar.Verdadeiro(b.Postar(dono, NativoTeste.WM_CANCELMODE, 0, 0, "WM_CANCELMODE"), "WM_CANCELMODE ao dono do menu");
                EsperarDesde(b, marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 3000, "menu fechado");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
        });
    }

    // "Desviar da tela cheia" (DEC-034) pelo menu de verdade: T desliga o modo, o núcleo registra o comando e a escolha vai
    // para o settings.json do perfil com atraso; reaberto, o menu mostra a chave desmarcada, e T religa. As teclas são WM_CHAR
    // postadas ao dono do menu, deste Buzzy.
    [Teste]
    public void ModoTelaCheiaPeloMenu_FecharEReabrir_VoltaDesligado()
    {
        SemTocarNosArquivosReais(() =>
        {
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
                long marca = BuzzyEmTeste.MarcaDoLog();
                EventoDoLog fechado = EscolherNoMenu(b, opaco, 't');
                Afirmar.Igual("ModoTelaCheia", fechado["fechado"], "o menu escolheu \"Desviar da tela cheia\"");
                EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 1000, "o menu");
                Afirmar.Igual("sim", exibindo["modoTelaCheia"], "o modo vem ligado");
                EventoDoLog nucleo = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetFullscreenMode", 3000, "CMD_SET_FULLSCREEN_MODE no núcleo");
                Afirmar.Igual(("menu", "CMD_SET_FULLSCREEN_MODE: desligado"), (nucleo["motivo"], nucleo["regra"]), "desligado pelo menu");
                EventoDoLog pedido = EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["pedido"] == "preferencias", 3000, "o pedido de gravação das preferências");
                Afirmar.Igual(("CmdSetFullscreenMode", "nao"), (pedido["evento"], pedido["imediata"]), "com atraso");
                EsperarDesde(b, marca, e => e.Chave == "CONFIG" && e["gravado"] == "sim" && e["motivo"] == "atraso", 8000, "o modo gravado com atraso");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
            Afirmar.Falso(LerDoPerfil().Configuracoes.Preferencias.ModoTelaCheia, "o arquivo guarda o modo desligado");

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
                long marca = BuzzyEmTeste.MarcaDoLog();
                EventoDoLog fechado = EscolherNoMenu(b, opaco, 't');
                EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 1000, "o menu ao reabrir");
                Afirmar.Igual(("nao", "ModoTelaCheia"), (exibindo["modoTelaCheia"], fechado["fechado"]), "reaberto, desligado; T religa");
                EventoDoLog nucleo = EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "CmdSetFullscreenMode", 3000, "CMD_SET_FULLSCREEN_MODE no núcleo");
                Afirmar.Igual("CMD_SET_FULLSCREEN_MODE: ligado", nucleo["regra"], "religado pelo menu");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
        });
    }

    // Um settings.json ilegível no perfil: a partida usa os padrões (a posição inicial), e a primeira gravação, a da saída,
    // guarda o ilegível como a cópia de diagnóstico (settings.corrupt.json) e recria o principal, válido.
    [Teste]
    public void PartidaComArquivoIlegivel_UsaOsPadroes_EASaidaGuardaUmaCopia()
    {
        SemTocarNosArquivosReais(() =>
        {
            Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
            BuzzyEmTeste.ExigirNenhumBuzzyAberto();
            PerfilDeTeste.Limpar(Perfil);
            string pasta = PastaDoPerfil();
            Directory.CreateDirectory(pasta);
            File.WriteAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal), "{ lixo");

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog partida = b.Esperar(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"), 5000, "a leitura das configurações na partida");
                Afirmar.Igual(("padroes", "Ilegivel", "Ausente"), (partida["lido"], partida["principal"], partida["reserva"]), "ilegível e sem reserva: os padrões");
                EventoDoLog carga = b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["de"] == "Booting", 5000, "a carga");
                Afirmar.Igual("BOOTING: configurações e topologia carregadas", carga["regra"], "sem posição salva");
                b.EsperarRetangulo(Posicionador.Inicial(t, Sprite).Retangulo, 3000, "na posição inicial");

                List<EventoDoLog> saida = FecharEGravar(b);
                EventoDoLog gravado = Afirmar.NaoNulo(saida.SingleOrDefault(e => e["gravado"] == "sim"), $"a saída grava ({string.Join("; ", saida.Select(e => e.Linha))})");
                Afirmar.Igual(("CmdExit", "Ilegivel", "sim"), (gravado["motivo"], gravado["principalAntes"], gravado["copiaDeDiagnostico"]), "o ilegível vira a cópia de diagnóstico");
            }
            Afirmar.Igual("{ lixo", File.ReadAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomeIlegivel)), "a cópia de diagnóstico é o ilegível");
            LeituraDasConfiguracoes lida = LerDoPerfil();
            Afirmar.Igual(SituacaoDaLeitura.Valida, lida.Situacao, "o principal recriado é válido");
            Afirmar.Sequencia(new[] { ArquivoDeConfiguracoes.NomePrincipal, ArquivoDeConfiguracoes.NomeIlegivel }.Order(StringComparer.Ordinal),
                Directory.GetFiles(pasta).Select(Path.GetFileName).Order(StringComparer.Ordinal), "só o principal e a cópia de diagnóstico, sem temporário nem reserva");
        });
    }

    // Critério 2 da Fase 8 (DEC-038, item 3): um settings.json v4 no perfil é lido com os padrões nos campos novos e não é
    // regravado na partida; a saída, por um evento do usuário, grava na v5 com os mesmos valores, e o v4 vira a reserva.
    [Teste]
    public void PartidaComArquivoV4_NaoGravaNaPartida_EASaidaMigraParaV5()
    {
        SemTocarNosArquivosReais(() =>
        {
            BuzzyEmTeste.ExigirNenhumBuzzyAberto();
            PerfilDeTeste.Limpar(Perfil);
            string pasta = PastaDoPerfil();
            Directory.CreateDirectory(pasta);
            string principal = Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal);
            var conteudo = new ConfiguracoesSalvas(null, Preferencias.Padrao with { Energia = NivelDeEnergia.Alta, ModoTelaCheia = false });
            string v4 = System.Text.Encoding.UTF8.GetString(EsquemaDeConfiguracoes.Escrever(conteudo))
                .Replace("\"schemaVersion\": 5", "\"schemaVersion\": 4", StringComparison.Ordinal)
                .Replace(",\n    \"sempreNoTopo\": true,\n    \"escala\": \"media\"", "", StringComparison.Ordinal);
            File.WriteAllText(principal, v4);
            DateTime escritoAntes = File.GetLastWriteTimeUtc(principal);

            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog partida = b.Esperar(e => e.Chave == "CONFIG" && e.Campos.ContainsKey("lido"), 5000, "a leitura das configurações na partida");
                Afirmar.Igual(("principal", "anterior"), (partida["lido"], partida["versao"]), "lido do principal, versão anterior");
                Thread.Sleep(3000);
                Afirmar.Igual(escritoAntes, File.GetLastWriteTimeUtc(principal), "a partida não grava");
                FecharEGravar(b);
            }
            LeituraDasConfiguracoes lida = LerDoPerfil();
            Afirmar.Igual(((int?)EsquemaDeConfiguracoes.VersaoAtual, conteudo.Preferencias), (lida.Versao, lida.Configuracoes.Preferencias), "migrado para a v5, com os mesmos valores");
            Afirmar.Igual(v4, File.ReadAllText(Path.Combine(pasta, ArquivoDeConfiguracoes.NomeReserva)), "o v4 virou a reserva");
        });
    }

    // Fase 8, F8-P5 (DEC-038, item 4; critério 11): "Energia…" pelo menu abre o painel compacto, uma janela do próprio Buzzy,
    // inteira na área útil do monitor do personagem e fora do sprite; sem mover o personagem (invariante 8). Sem o primeiro
    // plano na integração, o painel pode fechar por "semFoco", e isso também vale, desde que avise o núcleo (a autonomia não
    // fica pausada). Encerrar fecha tudo.
    [Teste]
    public void PainelDeEnergiaPeloMenu_AbreNaAreaUtil_SemMoverOPersonagem()
    {
        SemTocarNosArquivosReais(() =>
        {
            Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
            using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil);
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
            EventoDoLog posicao = b.Esperar(e => e.Chave == "POSICAO", 5000, "posição");
            PontoPx opaco = EventoDoLog.Ponto(posicao["pontoOpaco"]);
            RetanguloPx personagem = b.RetanguloDaJanela();
            long marca = BuzzyEmTeste.MarcaDoLog();
            EventoDoLog fechado = EscolherNoMenu(b, opaco, 'n');
            Afirmar.Igual("Energia", fechado["fechado"], "o menu escolheu \"Energia…\"");
            EventoDoLog aberto = EsperarDesde(b, marca, e => e.Chave == "PAINEL" && e["aberto"] == "sim", 3000, "o painel aberto");
            RetanguloPx painel = EventoDoLog.Retangulo(aberto["ret"]);
            MonitorDoDesktop monitor = t.Monitores.Single(m => m.AreaUtil.Contem(personagem));
            Afirmar.Verdadeiro(monitor.AreaUtil.Contem(painel) && !painel.Intersecta(personagem), $"painel {painel} inteiro na área útil {monitor.AreaUtil} e fora do sprite {personagem}");
            Afirmar.Igual(personagem, b.RetanguloDaJanela(), "o personagem não se moveu");
            Thread.Sleep(1000);
            if (BuzzyEmTeste.EventosDesde(marca).FirstOrDefault(e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado")) is { } fechou)
            {
                Afirmar.Igual("semFoco", fechou["fechado"], "sem o primeiro plano, fecha por semFoco");
                EsperarDesde(b, marca, e => e.Chave == "NUCLEO" && e["evento"] == "EnergyPanelClose", 2000, "e avisa o núcleo");
            }
            Afirmar.Igual(0, b.FecharPorWmClose(), "encerra com o painel aberto ou fechado");
        });
    }

    // Fase 8, F8-P10 (DEC-038, nota do observador): com as configurações do próprio Buzzy em primeiro plano por mais que a
    // carência do foco (2 s), o observador da tela cheia e a curiosidade não publicam nada: nenhuma linha TELA_CHEIA de
    // mudança, nenhum evento de foco ou de curiosidade no núcleo. As duas defesas (o gancho pula o próprio processo, e a
    // leitura reconhece a thread da interface) têm os testes delas em ObservadorIgnoraOBuzzyTestes e AgendaDaTelaCheiaTestes.
    [Teste]
    public void JanelasProprias_EmPrimeiroPlano_ObservadorNaoPublica()
    {
        SemTocarNosArquivosReais(() =>
        {
            using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, telaCheia: true);
            b.Esperar(e => e.Chave == "TELA_CHEIA" && e["observador"] == "ligado", 5000, "o observador ligado");
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
            PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
            long marca = BuzzyEmTeste.MarcaDoLog();
            EscolherNoMenu(b, opaco, 'c');
            EsperarDesde(b, marca, e => e.Chave == "CONFIGURACOES" && e["aberta"] == "sim", 3000, "as configurações abertas");
            Thread.Sleep(3500);
            EventoDoLog[] depois = [.. BuzzyEmTeste.EventosDesde(marca)];
            Afirmar.Falso(depois.Any(e => e.Chave == "TELA_CHEIA" && e.Campos.ContainsKey("ocupados")), "nenhuma mudança da tela cheia publicada");
            Afirmar.Falso(depois.Any(e => e.Chave == "NUCLEO" && (e["evento"] is "ForegroundMonitorChanged" or "ActiveWindowSpan" or "CuriosityTimer")),
                "nenhum evento de foco ou de curiosidade: " + string.Join(" / ", depois.Where(e => e.Chave == "NUCLEO").Select(e => e["evento"])));
            Afirmar.Igual(0, b.FecharPorWmClose());
        });
    }

    // Fase 8, F8-P6 (DEC-038, item 5): "Configurações…" pelo menu abre uma janela do próprio Buzzy, inteira na área útil e
    // fora do sprite; de novo pelo menu, só ativa a mesma (instância única); encerrar a fecha.
    [Teste]
    public void ConfiguracoesPeloMenu_AbremUmaVez_NaAreaUtil()
    {
        SemTocarNosArquivosReais(() =>
        {
            Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
            using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil);
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
            PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
            RetanguloPx personagem = b.RetanguloDaJanela();
            long marca = BuzzyEmTeste.MarcaDoLog();
            Afirmar.Igual("Configuracoes", EscolherNoMenu(b, opaco, 'c')["fechado"], "o menu escolheu \"Configurações…\"");
            EventoDoLog aberta = EsperarDesde(b, marca, e => e.Chave == "CONFIGURACOES" && e["aberta"] == "sim", 3000, "as configurações abertas");
            RetanguloPx janela = EventoDoLog.Retangulo(aberta["ret"]);
            MonitorDoDesktop monitor = t.Monitores.Single(m => m.AreaUtil.Contem(personagem));
            Afirmar.Verdadeiro(monitor.AreaUtil.Contem(janela) && !janela.Intersecta(personagem), $"janela {janela} na área útil {monitor.AreaUtil} e fora do sprite {personagem}");
            // F8-P9 (DEC-038, item 12): com perfil de teste, o início com o Windows é o simulado, desligado; o registro real
            // fica igual (a foto de SemTocarNosArquivosReais inclui o valor Run).
            EventoDoLog inicio = EsperarDesde(b, marca, e => e.Chave == "INICIO" && e["acao"] == "ler", 3000, "o início lido na abertura");
            Afirmar.Igual(("aberta", "Simulado", "Desligado"), (inicio["motivo"], inicio["modo"], inicio["estado"]), "o simulado, desligado");
            long segunda = BuzzyEmTeste.MarcaDoLog();
            EscolherNoMenu(b, opaco, 'c');
            EsperarDesde(b, segunda, e => e.Chave == "CONFIGURACOES" && e["ativada"] == "sim", 3000, "a mesma janela ativada");
            Afirmar.Falso(BuzzyEmTeste.EventosDesde(segunda).Any(e => e.Chave == "CONFIGURACOES" && e["aberta"] == "sim"), "nenhuma segunda janela");
            Afirmar.Igual(0, b.FecharPorWmClose(), "encerra com as configurações abertas");
        });
    }

    // Fase 8, F8-P7 (DEC-038, item 8; critério 8): o "sempre no topo" desligado no arquivo vale desde a partida: o personagem
    // abre sem WS_EX_TOPMOST, e o log diz TOPO|ligado=nao|motivo=partida; com o padrão, topmost.
    [Teste]
    public void SempreNoTopoDesligadoNoArquivo_AbreSemTopmost()
    {
        SemTocarNosArquivosReais(() =>
        {
            BuzzyEmTeste.ExigirNenhumBuzzyAberto();
            PerfilDeTeste.Limpar(Perfil);
            string pasta = PastaDoPerfil();
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal),
                EsquemaDeConfiguracoes.Escrever(new ConfiguracoesSalvas(null, Preferencias.Padrao with { SempreNoTopo = false })));
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                EventoDoLog topo = b.Esperar(e => e.Chave == "TOPO", 5000, "o topo da partida");
                Afirmar.Igual(("nao", "partida"), (topo["ligado"], topo["motivo"]), "desligado na partida");
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                Afirmar.Igual(0L, NativoTeste.EstiloEstendido(b.Janela) & NativoTeste.WS_EX_TOPMOST, "sem WS_EX_TOPMOST");
                // Abrir o painel sobe o personagem à frente das janelas comuns, sem deixá-lo topmost.
                PontoPx opaco = EventoDoLog.Ponto(b.Esperar(e => e.Chave == "POSICAO", 5000, "posição")["pontoOpaco"]);
                long marca = BuzzyEmTeste.MarcaDoLog();
                EscolherNoMenu(b, opaco, 'n');
                EsperarDesde(b, marca, e => e.Chave == "PAINEL" && e["aberto"] == "sim", 3000, "o painel aberto");
                Afirmar.Igual(0L, NativoTeste.EstiloEstendido(b.Janela) & NativoTeste.WS_EX_TOPMOST, "o painel não religa o topo");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
            PerfilDeTeste.Limpar(Perfil);
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
                Afirmar.Verdadeiro((NativoTeste.EstiloEstendido(b.Janela) & NativoTeste.WS_EX_TOPMOST) != 0, "com o padrão, topmost");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
        });
    }

    // Fase 8, F8-P8 (DEC-038, item 9; critério 9): a escala do arquivo vale desde a partida: a janela tem o tamanho do passo
    // no DPI do monitor, os pés no chão da área útil, e o log diz ESCALA|passo=...|dip=....
    [Teste]
    public void EscalaGrandeNoArquivo_AbreCom192Dip() => EscalaNoArquivo_AbreNoTamanho(EscalaDoPersonagem.Grande, 192);

    // A Pequena revista pelo usuário em 2026-10-04 (96 DIP, 1,5× a arte).
    [Teste]
    public void EscalaPequenaNoArquivo_AbreCom96Dip() => EscalaNoArquivo_AbreNoTamanho(EscalaDoPersonagem.Pequena, 96);

    private void EscalaNoArquivo_AbreNoTamanho(EscalaDoPersonagem passo, int dip)
    {
        SemTocarNosArquivosReais(() =>
        {
            Topologia t = Afirmar.NaoNulo(LeitorDeTopologia.Ler(out string? erro), erro);
            BuzzyEmTeste.ExigirNenhumBuzzyAberto();
            PerfilDeTeste.Limpar(Perfil);
            string pasta = PastaDoPerfil();
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal),
                EsquemaDeConfiguracoes.Escrever(new ConfiguracoesSalvas(null, Preferencias.Padrao with { Escala = passo })));
            using BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false);
            EventoDoLog escala = b.Esperar(e => e.Chave == "ESCALA", 5000, "a escala da partida");
            Afirmar.Igual((passo.ToString(), dip.ToString(System.Globalization.CultureInfo.InvariantCulture)), (escala["passo"], escala["dip"]), $"o passo {passo}");
            b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded" && e["para"] == "Idle", 5000, "núcleo carregado");
            b.Esperar(e => e.Chave == "POSICAO", 5000, "posição");
            RetanguloPx r = b.RetanguloDaJanela();
            MonitorDoDesktop m = t.Monitores.Single(x => x.AreaUtil.Contem(r));
            TamanhoPx esperado = new TamanhoDip(dip, dip).ParaPixels(m.Dpi);
            Afirmar.Igual((esperado.Largura, esperado.Altura, m.AreaUtil.Base), (r.Largura, r.Altura, r.Base), $"{dip} DIP a {m.Dpi} DPI, com os pés no chão ({r})");
            Afirmar.Igual(0, b.FecharPorWmClose());
        });
    }

    // Fase 9, F9-P1 (DEC-040, item 5): o log inteiro de duas execuções sem identificadores do usuário ou da máquina. Uma
    // com um settings.json no perfil cuja chave de monitor leva um marcador; outra com um perfil de teste recusado que
    // contém a pasta e o nome do usuário. Nenhuma linha leva o nome do usuário, o da máquina, a pasta local, o caminho do
    // executável, a pasta do perfil, o marcador ou um SID.
    [Teste]
    public void LogInteiro_SemIdentificadoresDoUsuarioOuDaMaquina()
    {
        const string Marcador = "ZZMARCA";
        SemTocarNosArquivosReais(() =>
        {
            BuzzyEmTeste.ExigirNenhumBuzzyAberto();
            long marca = BuzzyEmTeste.MarcaDoLog();
            PerfilDeTeste.Limpar(Perfil);
            string pasta = PastaDoPerfil();
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, ArquivoDeConfiguracoes.NomePrincipal), EsquemaDeConfiguracoes.Escrever(
                new ConfiguracoesSalvas(new PosicaoDoPersonagem("mon:" + Marcador, 0.5, 1, new PontoPx(500, 1000)), Preferencias.Padrao)));
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: Perfil, limpar: false))
            {
                b.Esperar(e => e.Chave == "NUCLEO" && e["evento"] == "Loaded", 5000, "núcleo carregado");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string recusado = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perfil");
            using (BuzzyEmTeste b = BuzzyEmTeste.Iniciar(perfil: recusado, limpar: false))
            {
                b.Esperar(e => e.Chave == "CONFIG" && e["lido"] == "desligado", 5000, "o perfil recusado desliga a persistência");
                Afirmar.Igual(0, b.FecharPorWmClose());
            }

            List<EventoDoLog> linhas = BuzzyEmTeste.EventosDesde(marca);
            Afirmar.Verdadeiro(linhas.Count > 20, $"as linhas das duas execuções: {linhas.Count}");
            var proibidos = new List<string> { Environment.MachineName, local, BuzzyEmTeste.CaminhoDoExecutavel, Path.GetDirectoryName(BuzzyEmTeste.CaminhoDoExecutavel)!, pasta, recusado, Marcador, "S-1-5-21" };
            if (Environment.UserName.Length >= 3) proibidos.Add(Environment.UserName);
            foreach (EventoDoLog e in linhas)
            {
                string texto = e.Chave + "|" + string.Join("|", e.Campos.Select(c => $"{c.Key}={c.Value}"));
                foreach (string p in proibidos.Where(p => p.Length > 0))
                    Afirmar.Falso(texto.Contains(p, StringComparison.OrdinalIgnoreCase), $"a linha {e.Chave} leva um identificador ({(p == Marcador ? "o marcador" : p.Length > 4 ? p[..4] + "…" : "curto")}): {texto[..Math.Min(160, texto.Length)]}");
            }
        });
    }

    /// <summary>Abre o menu pelo botão direito postado e escolhe pelas teclas, postadas como WM_CHAR ao dono do menu.</summary>
    private static EventoDoLog EscolherNoMenu(BuzzyEmTeste b, PontoPx opaco, params char[] teclas)
    {
        long marca = BuzzyEmTeste.MarcaDoLog();
        b.PostarMouse(NativoTeste.WM_RBUTTONDOWN, 0, opaco);
        b.PostarMouse(NativoTeste.WM_RBUTTONUP, 0, opaco);
        EventoDoLog exibindo = EsperarDesde(b, marca, e => e.Chave == "MENU" && e["exibindo"] == "sim", 3000, "menu exibido");
        var dono = (nint)long.Parse(exibindo["dono"], CultureInfo.InvariantCulture);
        foreach (char tecla in teclas) b.PostarChar(dono, tecla);
        return EsperarDesde(b, marca, e => e.Chave == "MENU" && e.Campos.ContainsKey("fechado"), 3000, $"menu fechado pelas teclas {string.Join(" e ", teclas)}");
    }
}
