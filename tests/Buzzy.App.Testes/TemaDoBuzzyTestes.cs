using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.App.Apresentacao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// O tema "chapéu de palha" (DEC-039): o dicionário do tema estiliza só a aparência dos controles por intenção, e o do alto
/// contraste não tem estilo nenhum, só as cores do sistema; a faixa de cima é um enfeite fora da árvore do leitor de tela; e
/// as duas janelas, renderizadas fora da tela, saem com as cores da paleta. Com <c>BUZZY_PREVIA</c> apontando para uma pasta,
/// grava as prévias em PNG (a 100% e 150%), sem abrir janela.
/// </summary>
internal sealed class TemaDoBuzzyTestes
{
    [Teste]
    public void Dicionarios_TemaEAltoContraste()
    {
        ResourceDictionary tema = TemaDoBuzzy.Dicionario(true), sistema = TemaDoBuzzy.Dicionario(false);
        foreach (Type t in new[] { typeof(CaixaDeComando), typeof(RadioDeComando), typeof(Button), typeof(GroupBox), typeof(Seletor<NivelDeEnergia>), typeof(Seletor<EscalaDoPersonagem>) })
            Afirmar.Verdadeiro(tema[t] is Style, $"o tema estiliza {t.Name}");
        Afirmar.Verdadeiro(tema[TemaDoBuzzy.ChaveSeletorSemTitulo] is Style, "o seletor sem título");
        Afirmar.Igual(0, sistema.Values.OfType<Style>().Count(), "no alto contraste, nenhum estilo: os controles nativos");
        foreach (string chave in new[] { TemaDoBuzzy.ChaveFundo, TemaDoBuzzy.ChaveTexto, TemaDoBuzzy.ChaveTextoSuave, TemaDoBuzzy.ChaveCabecalho, TemaDoBuzzy.ChaveTextoDoCabecalho, TemaDoBuzzy.ChaveAba, TemaDoBuzzy.ChaveFaixa, TemaDoBuzzy.ChaveBorda })
        {
            Afirmar.Verdadeiro(tema[chave] is SolidColorBrush, $"{chave} no tema");
            Afirmar.Verdadeiro(sistema[chave] is Brush b && TemPincelDoSistema(b), $"{chave} no alto contraste é uma cor do sistema");
        }
        Afirmar.Igual((TemaDoBuzzy.Fundo, TemaDoBuzzy.Contorno, TemaDoBuzzy.Pelo), (Cor(tema, TemaDoBuzzy.ChaveFundo), Cor(tema, TemaDoBuzzy.ChaveTexto), Cor(tema, TemaDoBuzzy.ChaveCabecalho)), "a paleta");
    }

    [Teste]
    public void Cabecalho_ForaDaArvoreDoLeitor()
    {
        FrameworkElement cabecalho = TemaDoBuzzy.Cabecalho("Título", 18);
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(cabecalho);
        Afirmar.Igual((false, false, 0), (peer.IsControlElement(), peer.IsContentElement(), peer.GetChildren()?.Count ?? 0), "o enfeite não aparece para o leitor de tela");
    }

    [Teste]
    public void Janelas_RenderizadasComAPaleta()
    {
        string? pasta = Environment.GetEnvironmentVariable("BUZZY_PREVIA");
        var cfg = new JanelaDeConfiguracoes(Preferencias.Padrao, EscalaDoPersonagem.Media, comConteudoAdulto: true);
        BitmapSource c = Renderizar(cfg, pasta, "previa-configuracoes");
        var painel = new PainelDeEnergia(NivelDeEnergia.Media);
        BitmapSource p = Renderizar(painel, pasta, "previa-painel");
        foreach ((string nome, BitmapSource bmp) in new[] { ("configurações", c), ("painel", p) })
        {
            Afirmar.Igual(TemaDoBuzzy.Pelo, Pixel(bmp, bmp.PixelWidth - 6, 6), $"{nome}: a faixa azul no topo");
            Afirmar.Igual(TemaDoBuzzy.Fundo, Pixel(bmp, bmp.PixelWidth - 8, bmp.PixelHeight - 6), $"{nome}: o fundo creme embaixo");
        }
    }

    private static bool TemPincelDoSistema(Brush b)
        => typeof(SystemColors).GetProperties().Where(p => p.PropertyType == typeof(SolidColorBrush)).Any(p => ReferenceEquals(p.GetValue(null), b));

    private static Color Cor(ResourceDictionary d, string chave) => ((SolidColorBrush)d[chave]).Color;

    private static Color Pixel(BitmapSource bmp, int x, int y)
    {
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return Color.FromRgb(px[2], px[1], px[0]);
    }

    /// <summary>O conteúdo da janela, fora dela e fora da tela, com o dicionário dela, a 100% (e a 150% na prévia).</summary>
    private static BitmapSource Renderizar(Window janela, string? pasta, string nome)
    {
        var conteudo = (UIElement)janela.Content;
        janela.Content = null;
        var host = new Border { Child = conteudo };
        foreach (ResourceDictionary d in janela.Resources.MergedDictionaries) host.Resources.MergedDictionaries.Add(d);
        host.Background = (Brush)host.FindResource(TemaDoBuzzy.ChaveFundo);
        TextElement.SetForeground(host, (Brush)host.FindResource(TemaDoBuzzy.ChaveTexto));
        TextElement.SetFontSize(host, 13);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
        BitmapSource? primeiro = null;
        foreach (double escala in pasta is null ? [1.0] : new[] { 1.0, 1.5 })
        {
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth * escala), (int)Math.Ceiling(host.ActualHeight * escala), 96 * escala, 96 * escala, PixelFormats.Pbgra32);
            rtb.Render(host);
            primeiro ??= rtb;
            if (pasta is null) continue;
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using FileStream fs = File.Create(Path.Combine(pasta, $"{nome}-{escala * 100:0}.png"));
            enc.Save(fs);
        }
        janela.Close();
        return primeiro!;
    }
}
