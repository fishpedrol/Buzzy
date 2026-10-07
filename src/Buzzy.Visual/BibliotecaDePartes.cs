using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace Buzzy.Visual;

// Uma classe CSS da paleta.
internal sealed record EstiloSvg(Color? Preenchimento, Color? Traco, double Espessura, bool PontaRedonda, bool JuncaoRedonda);

// Subconjunto de SVG: cada <g id> dentro de <defs> é uma parte, com pivô em (0,0). Entende g,
// path, circle, ellipse, class/fill/stroke/stroke-width/transform e regras de classe simples do
// <style>. O resto é ignorado.
public sealed class BibliotecaDePartes
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly Regex ReRegra = new(@"\.([A-Za-z0-9_-]+)\s*\{([^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex ReTransformacao = new(@"(translate|rotate|scale|matrix)\s*\(([^)]*)\)", RegexOptions.Compiled);

    private readonly Dictionary<string, Drawing> _partes;

    private BibliotecaDePartes(Dictionary<string, Drawing> partes, IReadOnlyDictionary<string, EstiloSvg> estilos)
    {
        _partes = partes;
        Estilos = estilos;
    }

    // A paleta, por classe.
    internal IReadOnlyDictionary<string, EstiloSvg> Estilos { get; }

    public IReadOnlyCollection<string> Nomes => _partes.Keys;

    public bool Contem(string nome) => _partes.ContainsKey(nome);

    // Congelado, em coordenadas locais.
    public Drawing Parte(string nome)
        => _partes.TryGetValue(nome, out Drawing? d) ? d : throw new KeyNotFoundException($"Parte ausente na biblioteca: {nome}");

    public static BibliotecaDePartes Ler(Stream svg)
    {
        XDocument doc = XDocument.Load(svg);
        XElement raiz = doc.Root ?? throw new InvalidDataException("SVG vazio.");

        var estilos = new Dictionary<string, EstiloSvg>(StringComparer.Ordinal);
        foreach (XElement style in raiz.Descendants(Svg + "style"))
        {
            foreach (Match m in ReRegra.Matches(style.Value))
                estilos[m.Groups[1].Value] = LerEstilo(m.Groups[2].Value);
        }

        var partes = new Dictionary<string, Drawing>(StringComparer.Ordinal);
        XElement defs = raiz.Element(Svg + "defs") ?? throw new InvalidDataException("SVG sem <defs>.");
        foreach (XElement g in defs.Elements(Svg + "g"))
        {
            string id = (string?)g.Attribute("id") ?? throw new InvalidDataException("Parte sem id.");
            if (partes.ContainsKey(id)) throw new InvalidDataException($"Parte repetida: {id}");
            DrawingGroup desenho = LerGrupo(g, estilos);
            desenho.Freeze();
            partes[id] = desenho;
        }
        return new BibliotecaDePartes(partes, estilos);
    }

    private static DrawingGroup LerGrupo(XElement g, Dictionary<string, EstiloSvg> estilos)
    {
        var grupo = new DrawingGroup();
        if (LerTransformacao((string?)g.Attribute("transform")) is { } t) grupo.Transform = t;

        foreach (XElement e in g.Elements())
        {
            if (e.Name == Svg + "g")
            {
                grupo.Children.Add(LerGrupo(e, estilos));
                continue;
            }

            Geometry? geometria = e.Name.LocalName switch
            {
                "path" => Geometry.Parse("F1 " + ((string?)e.Attribute("d") ?? "")),
                "circle" => new EllipseGeometry(new Point(Num(e, "cx"), Num(e, "cy")), Num(e, "r"), Num(e, "r")),
                "ellipse" => new EllipseGeometry(new Point(Num(e, "cx"), Num(e, "cy")), Num(e, "rx"), Num(e, "ry")),
                _ => null,
            };
            if (geometria is null) continue;
            if (LerTransformacao((string?)e.Attribute("transform")) is { } te) geometria.Transform = te;

            EstiloSvg estilo = Resolver(e, estilos);
            Brush? pincel = estilo.Preenchimento is { } cor ? Congelado(new SolidColorBrush(cor)) : null;
            Pen? caneta = null;
            if (estilo.Traco is { } corTraco && estilo.Espessura > 0)
            {
                caneta = new Pen(Congelado(new SolidColorBrush(corTraco)), estilo.Espessura)
                {
                    StartLineCap = estilo.PontaRedonda ? PenLineCap.Round : PenLineCap.Flat,
                    EndLineCap = estilo.PontaRedonda ? PenLineCap.Round : PenLineCap.Flat,
                    LineJoin = estilo.JuncaoRedonda ? PenLineJoin.Round : PenLineJoin.Miter,
                };
                caneta.Freeze();
            }
            if (pincel is null && caneta is null) continue;
            geometria.Freeze();
            grupo.Children.Add(new GeometryDrawing(pincel, caneta, geometria));
        }
        return grupo;
    }

    private static T Congelado<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    private static double Num(XElement e, string nome)
        => double.Parse((string?)e.Attribute(nome) ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);

    private static EstiloSvg Resolver(XElement e, Dictionary<string, EstiloSvg> estilos)
    {
        EstiloSvg baseEstilo = new(null, null, 0, false, false);
        string? classe = (string?)e.Attribute("class");
        if (classe is not null)
        {
            foreach (string c in classe.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (estilos.TryGetValue(c, out EstiloSvg? s)) baseEstilo = s;
            }
        }

        // Atributo solto ganha da classe (o arquivo não usa, mas o formato aceita).
        Color? fill = baseEstilo.Preenchimento, stroke = baseEstilo.Traco;
        double largura = baseEstilo.Espessura;
        if ((string?)e.Attribute("fill") is { } f) fill = Cor(f);
        if ((string?)e.Attribute("stroke") is { } st) stroke = Cor(st);
        if ((string?)e.Attribute("stroke-width") is { } w) largura = double.Parse(w, CultureInfo.InvariantCulture);
        return baseEstilo with { Preenchimento = fill, Traco = stroke, Espessura = largura };
    }

    private static EstiloSvg LerEstilo(string declaracoes)
    {
        Color? fill = null, stroke = null;
        double largura = 0;
        bool ponta = false, juncao = false;
        foreach (string decl in declaracoes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] kv = decl.Split(':', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;
            switch (kv[0])
            {
                case "fill": fill = Cor(kv[1]); break;
                case "stroke": stroke = Cor(kv[1]); break;
                case "stroke-width": largura = double.Parse(kv[1], CultureInfo.InvariantCulture); break;
                case "stroke-linecap": ponta = kv[1] == "round"; break;
                case "stroke-linejoin": juncao = kv[1] == "round"; break;
            }
        }
        return new EstiloSvg(fill, stroke, largura, ponta, juncao);
    }

    private static Color? Cor(string valor)
    {
        valor = valor.Trim();
        if (valor.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        if (valor.StartsWith('#') && valor.Length == 7)
        {
            return Color.FromRgb(
                byte.Parse(valor.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(valor.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(valor.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        throw new InvalidDataException($"Cor não suportada no subconjunto de SVG: {valor}");
    }

    internal static Transform? LerTransformacao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var grupo = new TransformGroup();
        var lidas = new List<Transform>();
        foreach (Match m in ReTransformacao.Matches(texto))
        {
            double[] a = m.Groups[2].Value
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();
            lidas.Add(m.Groups[1].Value switch
            {
                "translate" => new TranslateTransform(a[0], a.Length > 1 ? a[1] : 0),
                "rotate" => a.Length >= 3 ? new RotateTransform(a[0], a[1], a[2]) : new RotateTransform(a[0]),
                "scale" => new ScaleTransform(a[0], a.Length > 1 ? a[1] : a[0]),
                "matrix" => new MatrixTransform(a[0], a[1], a[2], a[3], a[4], a[5]),
                _ => Transform.Identity,
            });
        }
        // Em SVG, "A B C" aplica C primeiro; o TransformGroup do WPF aplica o primeiro filho primeiro.
        for (int i = lidas.Count - 1; i >= 0; i--) grupo.Children.Add(lidas[i]);
        grupo.Freeze();
        return grupo;
    }
}
