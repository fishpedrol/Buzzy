using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Automation;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Persistencia;
using Buzzy.Core.Personagem;

namespace Buzzy.Verificacao;

/// <summary>
/// Verificação sintética da Fase 8 (passo F8-P11; DEC-038 e DEC-039), com o receptor desta ferramenta no papel do aplicativo
/// em uso e o Buzzy pausado no perfil de teste:
/// <list type="bullet">
/// <item>V1, o painel pelo menu (N): em primeiro plano, aberto 1 s depois, na área útil, fora do sprite, o personagem parado
/// no lugar, e o foco da UIA no rádio marcado;</item>
/// <item>V2, seta para baixo e Esc: a energia Alta vai para o arquivo do perfil, o painel continua aberto até o Esc;</item>
/// <item>V3, clicar no aplicativo fecha o painel pelo foco, e o foco fica no aplicativo;</item>
/// <item>V4, arrastar o Buzzy com o painel aberto fecha o painel pelo núcleo, sem mudar a energia;</item>
/// <item>V6, as configurações pelo menu (C): em primeiro plano; o Tab percorre controles com nome, na ordem dos grupos;</item>
/// <item>V7, painel e configurações abertos: a energia escolhida no painel aparece marcada nas configurações (UIA);</item>
/// <item>V8, Alt+S desliga e liga o "sempre no topo" do personagem (WS_EX_TOPMOST);</item>
/// <item>V10, Alt+I liga e desliga o início com o Windows simulado (o registro real é conferido pela foto do fim);</item>
/// <item>V9, Alt+G escolhe Grande: o aviso aparece; fechado e reaberto no mesmo perfil, abre com 192 DIP, nos pés do chão;</item>
/// <item>V12, a UIA vista de fora na janela do personagem (prova parcial do peer vazio).</item>
/// </list>
/// Todo input é SINTÉTICO. O que fica de fora (bandeja com o topo desligado, cantos e monitor em x negativo, menu com o Buzzy
/// escondido, custo com as janelas abertas) vai para "não exercitado".
/// </summary>
internal sealed partial class Verificacao
{
    private const ushort VK_DOWN = 0x28, VK_TAB_F8 = 0x09, VK_ALT_F8 = 0x12;

    private nint _hPainel;
    private nint _hConfiguracoes;

    internal Sumario ExecutarConfiguracoes()
    {
        Nativo.POINT cursorOriginal = Nativo.Cursor();
        try
        {
            CalcularPosicoes();
            AbrirReceptor();
            AtivarReceptor("início");
            _prefixo = "Fase 8 — ";
            AbrirBuzzy();
            try
            {
                if (F8PainelPeloMenu())
                {
                    F8SetaEEsc();
                    F8CliqueForaFecha();
                    F8ArrasteFecha();
                }
                if (F8ConfiguracoesPeloMenu())
                {
                    F8TabPelosControles();
                    F8PainelEConfiguracoes();
                    F8SempreNoTopo();
                    F8InicioSimulado();
                    F8PeerDoPersonagem();
                    F8EscalaGrande();
                }
            }
            finally
            {
                FecharBuzzy("Fase 8");
            }
            _prefixo = "";
            _inj.ConferirUltimoInput();
        }
        catch (Interferencia e)
        {
            _houveInterferencia = true;
            _prefixo = "";
            Registrar("EXECUÇÃO", Invalida, "interferência humana detectada: " + e.Message + " Os resultados desta execução não valem.");
        }
        catch (Exception e)
        {
            _prefixo = "";
            Registrar("EXECUÇÃO", Falhou, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _prefixo = "";
            Limpeza(cursorOriginal);
        }

        VerificarReceptorAoFinal();
        _naoExercitados.Add("V-F8-5 cantos e monitor em x negativo (AncoragemDoPainel tem os testes de tabela)");
        _naoExercitados.Add("V-F8-8 bandeja com o topo desligado (critério 8, [MANUAL])");
        _naoExercitados.Add("V-F8-11 menu com o Buzzy escondido (MenuNativoTestes)");
        _naoExercitados.Add("V-F8-13 custo com o painel e as configurações abertos");
        _rel.Linha("   Cobertura da Fase 8: painel (abrir, escolher, fechar por Esc, por foco e pelo arraste), configurações (Tab, painel junto, topo, início simulado, escala com reabertura) e a UIA de fora no personagem; o Narrador e o teclado reais ficam [MANUAL].");
        return Resumir();
    }

    // ------------------------------------------------------------------ painel

    private bool FrenteEh(nint h) => h != 0 && Nativo.GetForegroundWindow() == h;

    private nint JanelaDoLog(EventoBuzzy e, string chave) => JanelaDoBuzzy(e["hwnd"], chave);

    /// <summary>Abre o painel pelo menu do personagem (N) e devolve o evento de abertura, ou nulo.</summary>
    private EventoBuzzy? AbrirPainel(string rotulo, long marca)
    {
        MenuOperado m = MenuDoPersonagem(Vk('N'), "N (Energia…)");
        if (m.Fechado?["fechado"] != "Energia")
        {
            Registrar(rotulo, Inconclusivo, "o menu não escolheu Energia: " + DescreverMenu(m));
            return null;
        }
        EventoBuzzy? aberto = LogDoBuzzy.Esperar(marca, e => e.Chave == "PAINEL" && e["aberto"] == "sim", 3000);
        if (aberto is null) Registrar(rotulo, Falhou, "sem PAINEL|aberto em 3 s");
        else _hPainel = JanelaDoLog(aberto, "PAINEL");
        return aberto;
    }

    private bool F8PainelPeloMenu()
    {
        const string Criterio = "V-F8-1 — o painel pelo menu: em primeiro plano e aberto 1 s depois, na área útil e fora do sprite, o personagem no lugar e o foco no rádio marcado";
        Nativo.RECT antes = Nativo.Retangulo(_hBuzzy);
        long marca = LogDoBuzzy.Marca();
        EventoBuzzy? aberto = AbrirPainel(Criterio, marca);
        if (aberto is null) return false;
        bool naFrente = FrenteEh(_hPainel);
        Thread.Sleep(1000);
        bool fechou = LogDoBuzzy.Desde(marca).Any(e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado"));
        Nativo.RECT painel = Nativo.Retangulo(_hPainel), sprite = Nativo.Retangulo(_hBuzzy);
        Nativo.MonitorLido? m = Nativo.Monitor(new Nativo.POINT((sprite.Left + sprite.Right) / 2, sprite.Bottom - 1));
        bool naArea = m is { } mon && mon.Info.rcWork.Contem(painel);
        bool foraDoSprite = painel.Right <= sprite.Left || painel.Left >= sprite.Right || painel.Bottom <= sprite.Top || painel.Top >= sprite.Bottom;
        bool parado = sprite.Equals(antes);
        string foco = FocoDaUia();
        Registrar(Criterio, naFrente && !fechou && naArea && foraDoSprite && parado && foco.StartsWith("Média", StringComparison.Ordinal),
            $"em primeiro plano={naFrente}; fechou em 1 s={fechou}; painel {painel} na área útil={naArea}, fora do sprite {sprite}={foraDoSprite}; personagem parado={parado}; foco da UIA=\"{foco}\"");
        return naFrente && !fechou;
    }

    private void F8SetaEEsc()
    {
        const string Criterio = "V-F8-2 — seta para baixo escolhe Alta e grava no perfil; o painel continua aberto; Esc fecha";
        long marca = LogDoBuzzy.Marca();
        bool seta = _inj.TeclaVirtual(VK_DOWN, "seta para baixo no painel", () => FrenteEh(_hPainel));
        bool gravou = EsperarAte(() => PreferenciasDoPerfil()?.Energia == NivelDeEnergia.Alta, 6000);
        bool aberto = !LogDoBuzzy.Desde(marca).Any(e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado")) && Nativo.IsWindowVisible(_hPainel);
        bool esc = _inj.TeclaVirtual(VK_ESCAPE, "Esc no painel", () => FrenteEh(_hPainel));
        EventoBuzzy? fechado = LogDoBuzzy.Esperar(marca, e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado"), 3000);
        Registrar(Criterio, seta && gravou && aberto && esc && fechado?["fechado"] == "esc",
            $"seta enviada={seta}; energia Alta no settings.json do perfil={gravou}; aberto depois da seta={aberto}; Esc enviado={esc}; fechado={fechado?["fechado"] ?? "não fechou"}");
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois do V-F8-2");
    }

    private void F8CliqueForaFecha()
    {
        const string Criterio = "V-F8-3 — clicar no aplicativo fecha o painel pelo foco, e o foco fica no aplicativo";
        long marca = LogDoBuzzy.Marca();
        if (AbrirPainel(Criterio, marca) is null) return;
        Thread.Sleep(300);
        _inj.CliqueEsquerdo(_alvoReceptor.X, _alvoReceptor.Y, _hReceptor);
        EventoBuzzy? fechado = LogDoBuzzy.Esperar(marca, e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado"), 3000);
        Thread.Sleep(300);
        bool frente = Nativo.GetForegroundWindow() == _hReceptor;
        Registrar(Criterio, fechado?["fechado"] == "foco" && frente, $"fechado={fechado?["fechado"] ?? "não fechou"}; receptor na frente={frente}");
    }

    private void F8ArrasteFecha()
    {
        const string Criterio = "V-F8-4 — arrastar o Buzzy com o painel aberto fecha o painel pelo núcleo, e a energia não muda";
        long marca = LogDoBuzzy.Marca();
        if (AbrirPainel(Criterio, marca) is null) return;
        Thread.Sleep(300);
        NivelDeEnergia? antes = PreferenciasDoPerfil()?.Energia;
        Nativo.POINT p = PontoOpaco();
        try
        {
            _inj.Pressionar(p.X, p.Y, _hBuzzy);
        }
        catch (CliqueRecusado e)
        {
            Registrar(Criterio, Inconclusivo, $"o pressionar foi recusado: {e.Message}");
            return;
        }
        int dx = p.X > (_areaUtilPrincipal.Left + _areaUtilPrincipal.Right) / 2 ? -1 : 1;
        for (int i = 1; i <= 6; i++)
        {
            _inj.MoverSegurando(p.X + dx * 30 * i, p.Y);
            Thread.Sleep(30);
        }
        _inj.SoltarEsquerdo(p.X + dx * 180, p.Y);
        EventoBuzzy? fechado = LogDoBuzzy.Esperar(marca, e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado"), 3000);
        Thread.Sleep(1500);
        NivelDeEnergia? depois = PreferenciasDoPerfil()?.Energia;
        Registrar(Criterio, fechado?["fechado"] == "nucleo" && antes == depois, $"fechado={fechado?["fechado"] ?? "não fechou"}; energia antes={antes}, depois={depois}");
        if (Nativo.GetForegroundWindow() != _hReceptor) AtivarReceptor("depois do V-F8-4");
    }

    // ------------------------------------------------------------------ configurações

    private bool F8ConfiguracoesPeloMenu()
    {
        const string Criterio = "V-F8-6a — as configurações pelo menu abrem em primeiro plano, na área útil e fora do sprite";
        long marca = LogDoBuzzy.Marca();
        MenuOperado m = MenuDoPersonagem(Vk('C'), "C (Configurações…)");
        EventoBuzzy? aberta = LogDoBuzzy.Esperar(marca, e => e.Chave == "CONFIGURACOES" && e["aberta"] == "sim", 3000);
        if (aberta is null)
        {
            Registrar(Criterio, Falhou, "sem CONFIGURACOES|aberta: " + DescreverMenu(m));
            return false;
        }
        _hConfiguracoes = JanelaDoLog(aberta, "CONFIGURACOES");
        Thread.Sleep(500);
        Nativo.RECT janela = Nativo.Retangulo(_hConfiguracoes), sprite = Nativo.Retangulo(_hBuzzy);
        Nativo.MonitorLido? mon = Nativo.Monitor(new Nativo.POINT((janela.Left + janela.Right) / 2, (janela.Top + janela.Bottom) / 2));
        bool naArea = mon is { } x && x.Info.rcWork.Contem(janela);
        bool fora = janela.Right <= sprite.Left || janela.Left >= sprite.Right || janela.Bottom <= sprite.Top || janela.Top >= sprite.Bottom;
        bool frente = FrenteEh(_hConfiguracoes);
        Registrar(Criterio, frente && naArea && fora, $"em primeiro plano={frente}; janela {janela} na área útil={naArea}; fora do sprite {sprite}={fora}");
        return frente;
    }

    private void F8TabPelosControles()
    {
        const string Criterio = "V-F8-6 — o Tab percorre as configurações: todo controle com nome, uma parada por grupo de opções (na marcada), na ordem (energia, tela cheia, adulto, tamanho, topo, início, Fechar) e de volta ao começo";
        var vistos = new List<string>();
        string primeiro = FocoDaUia(comTipo: true);
        vistos.Add(primeiro);
        for (int i = 0; i < 20; i++)
        {
            if (!_inj.TeclaVirtual(VK_TAB_F8, "Tab nas configurações", () => FrenteEh(_hConfiguracoes))) break;
            Thread.Sleep(150);
            string atual = FocoDaUia(comTipo: true);
            if (atual == primeiro) break;
            vistos.Add(atual);
        }
        bool semNomeVazio = vistos.All(v => !v.StartsWith('[') && !v.StartsWith("(", StringComparison.Ordinal));
        // A energia marcada é a do perfil (Alta, pelo V-F8-2); o tamanho, o padrão (Médio).
        Preferencias? p = PreferenciasDoPerfil();
        string energia = p?.Energia switch { NivelDeEnergia.Baixa => "Baixa", NivelDeEnergia.Alta => "Alta", _ => "Média" };
        string[] esperado = [energia, "Desviar da tela cheia", "Conteúdo adulto", "Médio", "Sempre no topo", "Iniciar com o Windows", "Fechar"];
        bool emOrdem = vistos.Count == esperado.Length && vistos.Zip(esperado).All(par => par.First.StartsWith(par.Second, StringComparison.Ordinal));
        bool voltou = vistos.Count < 21;
        Registrar(Criterio, semNomeVazio && emOrdem && voltou, $"sequência: {string.Join(" → ", vistos)}; esperado: {string.Join(" → ", esperado)}; voltou ao começo={voltou}");
    }

    private void F8PainelEConfiguracoes()
    {
        const string Criterio = "V-F8-7 — com as configurações abertas, a energia escolhida no painel aparece marcada nas configurações";
        long marca = LogDoBuzzy.Marca();
        if (AbrirPainel(Criterio, marca) is null) return;
        Thread.Sleep(300);
        bool seta = _inj.TeclaVirtual(VK_DOWN, "seta para baixo no painel", () => FrenteEh(_hPainel));
        bool marcada = EsperarAte(() => Selecionado(_hConfiguracoes, "Alta (mais ativa)") == true, 3000);
        bool esc = _inj.TeclaVirtual(VK_ESCAPE, "Esc no painel", () => FrenteEh(_hPainel));
        LogDoBuzzy.Esperar(marca, e => e.Chave == "PAINEL" && e.Campos.ContainsKey("fechado"), 3000);
        Registrar(Criterio, seta && marcada && esc, $"seta enviada={seta}; \"Alta\" marcada nas configurações={marcada}; Esc={esc}");
        AtivarConfiguracoes();
    }

    private void F8SempreNoTopo()
    {
        const string Criterio = "V-F8-8 — Alt+S desliga o \"sempre no topo\" do personagem e Alt+S de novo o liga";
        AtivarConfiguracoes();
        long marca = LogDoBuzzy.Marca();
        bool a = AltCom('S');
        EventoBuzzy? desligou = LogDoBuzzy.Esperar(marca, e => e.Chave == "TOPO" && e["ligado"] == "nao", 3000);
        Thread.Sleep(200);
        bool semTopo = !Topmost(_hBuzzy);
        long marca2 = LogDoBuzzy.Marca();
        bool b = AltCom('S');
        EventoBuzzy? ligou = LogDoBuzzy.Esperar(marca2, e => e.Chave == "TOPO" && e["ligado"] == "sim", 3000);
        Thread.Sleep(200);
        bool comTopo = Topmost(_hBuzzy);
        Registrar(Criterio, a && b && desligou is not null && semTopo && ligou is not null && comTopo,
            $"Alt+S={a}; TOPO desligado={desligou is not null}; sem WS_EX_TOPMOST={semTopo}; Alt+S de novo={b}; TOPO ligado={ligou is not null}; com WS_EX_TOPMOST={comTopo}");
    }

    private void F8InicioSimulado()
    {
        const string Criterio = "V-F8-10 — Alt+I liga e desliga o início com o Windows simulado (perfil de teste); o registro real fica igual (foto do fim)";
        AtivarConfiguracoes();
        long marca = LogDoBuzzy.Marca();
        bool a = AltCom('I');
        EventoBuzzy? ligou = LogDoBuzzy.Esperar(marca, e => e.Chave == "INICIO" && e["acao"] == "ligar", 3000);
        long marca2 = LogDoBuzzy.Marca();
        bool b = AltCom('I');
        EventoBuzzy? desligou = LogDoBuzzy.Esperar(marca2, e => e.Chave == "INICIO" && e["acao"] == "desligar", 3000);
        Registrar(Criterio, a && b && ligou?["modo"] == "Simulado" && ligou["resultado"] == "Ok" && desligou?["resultado"] == "Ok",
            $"Alt+I={a}: {Desc(ligou)}; Alt+I de novo={b}: {Desc(desligou)}");
    }

    private void F8PeerDoPersonagem()
    {
        const string Criterio = "V-F8-12 — a UIA vista de fora: a janela do personagem sem filhos na árvore de controle (prova parcial; o proxy do HWND fica com o Narrador)";
        AutomationElement el = AutomationElement.FromHandle(_hBuzzy);
        int filhos = TreeWalker.ControlViewWalker.GetFirstChild(el) is null ? 0 : 1;
        string nome = el.Current.Name;
        Registrar(Criterio, filhos == 0 && string.IsNullOrEmpty(nome),
            $"filhos na árvore de controle={filhos}; nome=\"{nome}\"; controle={el.Current.IsControlElement}; conteúdo={el.Current.IsContentElement}; tipo={el.Current.ControlType.ProgrammaticName}");
    }

    private void F8EscalaGrande()
    {
        const string Criterio = "V-F8-9 — Alt+G escolhe Grande: o aviso de próxima vez aparece; fechado e reaberto no mesmo perfil, abre com 192 DIP, nos pés do chão";
        AtivarConfiguracoes();
        bool g = AltCom('G');
        bool gravou = EsperarAte(() => PreferenciasDoPerfil()?.Escala == EscalaDoPersonagem.Grande, 6000);
        bool aviso = EsperarAte(() => Existe(_hConfiguracoes, "Vale na próxima vez que o Buzzy abrir."), 2000);
        FecharBuzzy("V-F8-9");
        long marca = LogDoBuzzy.Marca();
        ReabrirNoMesmoPerfil();
        EventoBuzzy? escala = LogDoBuzzy.Esperar(marca, e => e.Chave == "ESCALA", 5000);
        Thread.Sleep(700);
        Nativo.RECT r = Nativo.Retangulo(_hBuzzy);
        Nativo.MonitorLido? m = Nativo.Monitor(new Nativo.POINT((r.Left + r.Right) / 2, r.Bottom - 1));
        int esperado = m is { } mon ? (int)Math.Round(192 * mon.Dpi / 96.0) : -1;
        bool tamanho = r.Right - r.Left == esperado && r.Bottom - r.Top == esperado;
        bool noChao = m is { } x && r.Bottom == x.Info.rcWork.Bottom;
        Registrar(Criterio, g && gravou && aviso && escala?["passo"] == "Grande" && tamanho && noChao,
            $"Alt+G={g}; Grande no perfil={gravou}; aviso visível={aviso}; reaberto: {Desc(escala)}; janela {r} (esperado {esperado} px)={tamanho}; no chão={noChao}");
    }

    // ------------------------------------------------------------------ apoio

    private static string Desc(EventoBuzzy? e) => e is null ? "sem a linha" : e.Chave + "|" + string.Join("|", e.Campos.Select(c => $"{c.Key}={c.Value}"));

    private bool AltCom(char letra)
    {
        bool ok = _inj.Combinacao([VK_ALT_F8, Vk(letra)], $"Alt+{letra} nas configurações", () => FrenteEh(_hConfiguracoes));
        Thread.Sleep(200);
        return ok;
    }

    private void AtivarConfiguracoes()
    {
        if (FrenteEh(_hConfiguracoes)) return;
        Nativo.RECT r = Nativo.Retangulo(_hConfiguracoes);
        // Um clique na faixa de cima (enfeite, sem controle) traz a janela à frente, como uma pessoa faria.
        _inj.CliqueEsquerdo(r.Right - 20, r.Top + 60, _hConfiguracoes);
        EsperarAte(() => FrenteEh(_hConfiguracoes), 2000);
    }

    private static bool Topmost(nint h) => ((long)Nativo.GetWindowLongPtr(h, Nativo.GWL_EXSTYLE) & Nativo.WS_EX_TOPMOST) != 0;

    /// <summary>O controle com o foco, pela UIA (cliente, fora do produto), só se for de uma janela do Buzzy.</summary>
    private string FocoDaUia(bool comTipo = false)
    {
        AutomationElement? f = AutomationElement.FocusedElement;
        if (f is null) return "(sem foco)";
        if (_buzzy is null || f.Current.ProcessId != _buzzy.Id) return "(fora do Buzzy)";
        string nome = f.Current.Name;
        string tipo = f.Current.ControlType.ProgrammaticName.Replace("ControlType.", "", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(nome)) return $"[{tipo} sem nome]";
        return comTipo ? $"{nome} ({tipo})" : nome;
    }

    private bool? Selecionado(nint janela, string nome)
    {
        AutomationElement? el = AutomationElement.FromHandle(janela).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, nome));
        return el?.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? p) == true ? ((SelectionItemPattern)p).Current.IsSelected : null;
    }

    private static bool Existe(nint janela, string nome)
        => AutomationElement.FromHandle(janela).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, nome)) is { } el && !el.Current.IsOffscreen;

    /// <summary>As preferências do settings.json do perfil da verificação, pelo esquema do núcleo; nulas sem arquivo válido.</summary>
    private static Preferencias? PreferenciasDoPerfil()
    {
        string? pasta = PastaDeDados.DoPerfilDeTeste(PerfilDaVerificacao.Nome);
        string? arquivo = pasta is null ? null : Path.Combine(pasta, "settings.json");
        if (arquivo is null || !File.Exists(arquivo)) return null;
        try
        {
            LeituraDasConfiguracoes lida = EsquemaDeConfiguracoes.Ler(File.ReadAllBytes(arquivo));
            return lida.Situacao == SituacaoDaLeitura.Valida ? lida.Configuracoes.Preferencias : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Reabre o Buzzy pausado no mesmo perfil, sem apagá-lo (as configurações gravadas valem).</summary>
    private void ReabrirNoMesmoPerfil()
    {
        ExigirNenhumBuzzyAberto();
        _inicioLogBuzzy = LogDoBuzzy.Marca();
        ProcessStartInfo psi = PerfilDaVerificacao.Descrever(_exeBuzzy, "--pausado");
        _buzzy = Process.Start(psi) ?? throw new FalhaDeVerificacao("Buzzy.exe não reabriu.");
        _inicioBuzzy = _buzzy.StartTime;
        _buzzyEncerrado = false;
        _hBuzzy = JanelaDoBuzzy(Esperar(e => e.Chave == "JANELA", 15000, "janela do Buzzy reaberto")["hwnd"], "JANELA");
        _hServico = JanelaDoBuzzy(Esperar(e => e.Chave == "SERVICO", 5000, "janela de serviço")["hwnd"], "SERVICO");
        Esperar(e => e.Chave == "POSICAO", 5000, "posição");
        _rel.Linha($"   Buzzy reaberto no mesmo perfil: pid {_buzzy.Id}");
    }
}
