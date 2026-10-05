using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Buzzy.App.Apresentacao;
using Buzzy.App.Composicao;
using Buzzy.App.Plataforma;
using Buzzy.Core;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Os achados confirmados da revisão adversarial da Fase 8 (F8-P12): a falha do início com o Windows dita ao usuário e com o
/// código no log; as janelas do Buzzy no monitor da topologia atual; a janela de configurações limitada à área útil, com
/// rolagem; e o painel subindo os itens junto com o personagem. A chave Run ausente e o valor grande demais (adaptador real)
/// ficam na contenção na fonte e na revisão: nenhum teste toca o registro.
/// </summary>
internal sealed class RevisaoDaFase8Testes
{
    private const string Aqui = @"C:\Programas\Buzzy\Buzzy.exe";

    private static void Toggle(CaixaDeComando c) => ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(c).GetPattern(PatternInterface.Toggle)).Toggle();

    [Teste]
    public void Inicio_FalhaDitaAoUsuario_ComOCodigoNoLog()
    {
        var s = new InicioSimulado(Aqui) { FalharCom = 5 };
        var caixa = new CaixaDeComando { Content = "_Iniciar com o Windows" };
        var estado = new TextBlock();
        var log = new List<string>();
        var controle = new ControleDoInicio(caixa, estado, s, campos => log.Add(string.Join("|", campos.Select(c => $"{c.Campo}={c.Valor}"))));
        controle.Atualizar("aberta");
        Toggle(caixa);
        Afirmar.Igual((true, EstadoDoInicio.Desligado, false), (controle.Falhou, controle.Estado, caixa.IsChecked == true), "a ação falhou: a caixa continua desmarcada");
        Afirmar.Contem(Textos.ConfigInicioFalhou, estado.Text);
        Afirmar.Igual(Visibility.Visible, estado.Visibility, "a linha de estado aparece");
        Afirmar.Verdadeiro(log.Any(l => l.Contains("acao=ligar", StringComparison.Ordinal) && l.Contains("resultado=Erro", StringComparison.Ordinal) && l.Contains("codigo=5", StringComparison.Ordinal)), string.Join(" / ", log));
        Afirmar.Falso(log.Any(l => l.Contains(Aqui, StringComparison.OrdinalIgnoreCase)), "o caminho nunca vai ao log");
        s.FalharCom = null;
        controle.Atualizar("ativada");
        Afirmar.Falso(controle.Falhou, "a próxima leitura sem ação limpa a falha");
        Afirmar.Falso(estado.Text.Contains(Textos.ConfigInicioFalhou, StringComparison.Ordinal), estado.Text);
        Toggle(caixa);
        Afirmar.Igual((false, EstadoDoInicio.Ligado), (controle.Falhou, controle.Estado), "de novo, sem falha");
        Afirmar.Falso(log.Last().Contains("codigo=", StringComparison.Ordinal), "sem erro, sem código");
    }

    private static readonly MonitorDoDesktop Principal = new(@"\.\DISPLAY1", new RetanguloPx(0, 0, 1920, 1080), new RetanguloPx(0, 0, 1920, 1032), 96, true);
    private static readonly MonitorDoDesktop Segundo = new(@"\.\DISPLAY2", new RetanguloPx(1920, 0, 3840, 1080), new RetanguloPx(1920, 0, 3840, 1080), 144, false);

    [Teste]
    public void Janelas_NoMonitorDaTopologiaAtual()
    {
        var sprite = new RetanguloPx(3000, 900, 3192, 1080);
        // O mesmo monitor com a barra de tarefas nova: a área útil de agora, não a guardada.
        MonitorDoDesktop comBarra = Segundo with { AreaUtil = new RetanguloPx(1920, 0, 3840, 1032) };
        Afirmar.Igual(comBarra, LugarDasConfiguracoes.Monitor(new Topologia([Principal, comBarra]), Segundo, sprite), "pela chave, com a área útil atual");
        // O monitor do personagem saiu (escondido e desconectado): o mais próximo dos pés guardados, nunca o que não existe.
        Afirmar.Igual(Principal, LugarDasConfiguracoes.Monitor(new Topologia([Principal]), Segundo, sprite), "sem o monitor, o mais próximo");
        Afirmar.Igual(1032.0, LugarDasConfiguracoes.AlturaMaximaDip(Principal), "a 100%, a área útil em DIP");
        Afirmar.Igual(720.0, LugarDasConfiguracoes.AlturaMaximaDip(Segundo), "a 150%, 1080 px viram 720 DIP");
    }

    [Teste]
    public void Configuracoes_LimitadasAAreaUtil_ComRolagem()
    {
        var janela = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        try
        {
            Afirmar.Igual((ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled, false, false),
                (janela.Rolagem.VerticalScrollBarVisibility, janela.Rolagem.HorizontalScrollBarVisibility, janela.Rolagem.Focusable, janela.Rolagem.IsTabStop), "a coluna rola, sem parar o Tab");
            Afirmar.Verdadeiro(janela.Rolagem.Content is StackPanel { MaxWidth: JanelaDeConfiguracoes.LarguraMaxima }, "a rolagem leva a coluna");
            janela.LimitarAltura(400);
            Afirmar.Igual(400.0, janela.MaxHeight, "a altura limitada");
            janela.LimitarAltura(double.NaN);
            janela.LimitarAltura(0);
            Afirmar.Igual(400.0, janela.MaxHeight, "valores inválidos não mudam o limite");
            // Medida com o limite: o conteúdo, mais alto, cabe pela rolagem.
            var conteudo = (FrameworkElement)janela.Content;
            janela.Content = null;
            conteudo.Measure(new Size(double.PositiveInfinity, 300));
            conteudo.Arrange(new Rect(0, 0, conteudo.DesiredSize.Width, 300));
            conteudo.UpdateLayout();
            Afirmar.Verdadeiro(janela.Rolagem.ExtentHeight > janela.Rolagem.ViewportHeight && janela.Rolagem.ViewportHeight > 0,
                $"rola: conteúdo {janela.Rolagem.ExtentHeight:0} DIP em {janela.Rolagem.ViewportHeight:0}");
        }
        finally
        {
            janela.Close();
        }
    }

    // L17: toda subida do personagem acima das janelas comuns leva os itens junto, logo abaixo dele.
    [Teste]
    public void SubirOPersonagem_SobeOsItensJunto()
    {
        string pasta = Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Composicao");
        int subidas = 0;
        foreach (string arquivo in Directory.GetFiles(pasta, "Aplicacao*.cs"))
        {
            string[] linhas = File.ReadAllLines(arquivo);
            for (int i = 0; i < linhas.Length; i++)
            {
                if (!Regex.IsMatch(linhas[i], @"_personagem\??\.AoTopoDaFaixa\(\)") || linhas[i].TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                subidas++;
                bool junto = linhas.Skip(i + 1).Take(2).Any(l => l.Contains("_itens?.ReordenarAbaixoDoPersonagem()", StringComparison.Ordinal));
                Afirmar.Verdadeiro(junto, $"{Path.GetFileName(arquivo)}:{i + 1}: o personagem sobe sem os itens");
            }
        }
        Afirmar.Verdadeiro(subidas >= 3, $"as subidas (mostrar, fim da tela cheia, painel): {subidas}");
    }
}
