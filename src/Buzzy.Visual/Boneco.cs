using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Buzzy.Visual;

// Formato de assets/identidade/buzzy-poses.json.
public sealed class NoDaPose
{
    [JsonPropertyName("parte")] public string Parte { get; init; } = "";
    [JsonPropertyName("pos")] public double[] Pos { get; init; } = [0, 0];
    [JsonPropertyName("rot")] public double Rot { get; init; }
    [JsonPropertyName("esc")] public double[] Esc { get; init; } = [1, 1];
    [JsonPropertyName("atras")] public List<NoDaPose> Atras { get; init; } = [];
    [JsonPropertyName("frente")] public List<NoDaPose> Frente { get; init; } = [];
}

public sealed class Pose
{
    [JsonPropertyName("vista")] public string Vista { get; init; } = "frente";
    [JsonPropertyName("estado")] public string Estado { get; init; } = "";
    [JsonPropertyName("expressao")] public string Expressao { get; init; } = "neutro";
    [JsonPropertyName("topete")] public string? Topete { get; init; }

    // Só escalada: x da parede em DIP a partir da âncora.
    [JsonPropertyName("parede")] public double? Parede { get; init; }

    [JsonPropertyName("raiz")] public List<NoDaPose> Raiz { get; init; } = [];
}

// Camadas do rosto, de frente e de perfil (*Lado).
public sealed class Expressao
{
    [JsonPropertyName("olhos")] public string Olhos { get; init; } = "";
    [JsonPropertyName("sobrancelhas")] public string Sobrancelhas { get; init; } = "";
    [JsonPropertyName("boca")] public string Boca { get; init; } = "";
    [JsonPropertyName("topete")] public string Topete { get; init; } = "";
    [JsonPropertyName("olhoLado")] public string OlhoLado { get; init; } = "";
    [JsonPropertyName("bocaLado")] public string BocaLado { get; init; } = "";
    [JsonPropertyName("topeteLado")] public string TopeteLado { get; init; } = "";
}

// Em DIP; âncora no centro da borda de baixo.
public sealed class Quadro
{
    [JsonPropertyName("largura")] public double Largura { get; init; } = 128;
    [JsonPropertyName("altura")] public double Altura { get; init; } = 128;
    [JsonPropertyName("ancoraX")] public double AncoraX { get; init; } = 64;
    [JsonPropertyName("ancoraY")] public double AncoraY { get; init; } = 128;
}

public sealed class DefinicaoDoBoneco
{
    [JsonPropertyName("versao")] public int Versao { get; init; }
    [JsonPropertyName("quadro")] public Quadro Quadro { get; init; } = new();
    [JsonPropertyName("expressoes")] public Dictionary<string, Expressao> Expressoes { get; init; } = [];
    [JsonPropertyName("poses")] public Dictionary<string, Pose> Poses { get; init; } = [];

    public static DefinicaoDoBoneco Ler(Stream json)
        => JsonSerializer.Deserialize<DefinicaoDoBoneco>(json, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
           ?? throw new InvalidDataException("Arquivo de poses vazio.");
}

// Boneco de recorte: árvore de partes (translate/rotate/scale) + camadas da expressão, no quadro
// lógico de 0..128 DIP.
public sealed class Boneco
{
    public Boneco(BibliotecaDePartes partes, DefinicaoDoBoneco definicao)
    {
        Partes = partes;
        Definicao = definicao;
    }

    public BibliotecaDePartes Partes { get; }
    public DefinicaoDoBoneco Definicao { get; }

    // espelhar vira em torno da âncora (perfil olhando pra esquerda).
    public Drawing Compor(string pose, string? expressao = null, bool espelhar = false)
    {
        Pose p = Definicao.Poses.TryGetValue(pose, out Pose? achada) ? achada : throw new KeyNotFoundException($"Pose ausente: {pose}");
        string nomeExpressao = expressao ?? p.Expressao;
        Expressao e = Definicao.Expressoes.TryGetValue(nomeExpressao, out Expressao? ex) ? ex : throw new KeyNotFoundException($"Expressão ausente: {nomeExpressao}");

        var raiz = new DrawingGroup();
        var t = new TransformGroup();
        if (espelhar) t.Children.Add(new ScaleTransform(-1, 1));
        t.Children.Add(new TranslateTransform(Definicao.Quadro.AncoraX, Definicao.Quadro.AncoraY));
        raiz.Transform = t;

        foreach (NoDaPose no in p.Raiz) raiz.Children.Add(ComporNo(no, p, e));
        raiz.Freeze();
        return raiz;
    }

    private DrawingGroup ComporNo(NoDaPose no, Pose pose, Expressao e)
    {
        var grupo = new DrawingGroup();
        var t = new TransformGroup();
        double sx = no.Esc.Length > 0 ? no.Esc[0] : 1, sy = no.Esc.Length > 1 ? no.Esc[1] : sx;
        if (sx != 1 || sy != 1) t.Children.Add(new ScaleTransform(sx, sy));
        if (no.Rot != 0) t.Children.Add(new RotateTransform(no.Rot));
        t.Children.Add(new TranslateTransform(no.Pos.Length > 0 ? no.Pos[0] : 0, no.Pos.Length > 1 ? no.Pos[1] : 0));
        grupo.Transform = t;

        foreach (NoDaPose filho in no.Atras) grupo.Children.Add(ComporNo(filho, pose, e));
        string parte = Resolver(no.Parte, pose, e);
        if (parte.Length > 0) grupo.Children.Add(Partes.Parte(parte));
        foreach (NoDaPose filho in no.Frente) grupo.Children.Add(ComporNo(filho, pose, e));
        return grupo;
    }

    private static string Resolver(string parte, Pose pose, Expressao e) => parte switch
    {
        "@olhos" => e.Olhos,
        "@sobrancelhas" => e.Sobrancelhas,
        "@boca" => e.Boca,
        "@topete" => pose.Topete ?? e.Topete,
        "@olhoLado" => e.OlhoLado,
        "@bocaLado" => e.BocaLado,
        "@topeteLado" => e.TopeteLado,
        _ => parte,
    };

    // Com bordaDura, todo pixel sai com alfa 0 ou 255 (sem borda semitransparente).
    public BitmapSource Renderizar(string pose, double dpi, string? expressao = null, bool espelhar = false, bool bordaDura = true)
        => Rasterizar(Compor(pose, expressao, espelhar), Definicao.Quadro.Largura, Definicao.Quadro.Altura, dpi, bordaDura);

    public static BitmapSource Rasterizar(Drawing desenho, double larguraDip, double alturaDip, double dpi, bool bordaDura)
    {
        int w = (int)Math.Round(larguraDip * dpi / 96.0, MidpointRounding.AwayFromZero);
        int h = (int)Math.Round(alturaDip * dpi / 96.0, MidpointRounding.AwayFromZero);
        var visual = new DrawingVisual();
        RenderOptions.SetEdgeMode(visual, bordaDura ? EdgeMode.Aliased : EdgeMode.Unspecified);
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, larguraDip, alturaDip)));
            dc.DrawDrawing(desenho);
            dc.Pop();
        }
        var alvo = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
        alvo.Render(visual);
        int[] pixels = new int[w * h];
        alvo.CopyPixels(pixels, w * 4, 0);
        if (bordaDura) Limiarizar(pixels);
        var bmp = new WriteableBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        bmp.Freeze();
        return bmp;
    }

    // Pbgra32: alfa < 128 zera tudo; senão vira 255 e desfaz a pré-multiplicação da cor.
    public static void Limiarizar(int[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            uint p = (uint)pixels[i];
            uint a = p >> 24;
            if (a == 255) continue;
            if (a < 128)
            {
                pixels[i] = 0;
                continue;
            }
            uint r = Math.Min(255u, ((p >> 16) & 0xFF) * 255 / a);
            uint g = Math.Min(255u, ((p >> 8) & 0xFF) * 255 / a);
            uint b = Math.Min(255u, (p & 0xFF) * 255 / a);
            pixels[i] = unchecked((int)(0xFF000000u | (r << 16) | (g << 8) | b));
        }
    }

    // Pra conferir se a pose cabe no quadro.
    public Rect Limites(string pose, string? expressao = null) => Compor(pose, expressao).Bounds;
}
