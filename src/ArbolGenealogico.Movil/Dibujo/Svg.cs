using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// Un dibujo SVG sencillo de los recursos (los adornos de las láminas, hechos con tienda/laminas/adornos.py), con líneas y
/// rellenos vectoriales: sale nítido en el PDF a cualquier tamaño. Entiende solo lo que usan esos adornos: g, path, circle,
/// ellipse y rect, con fill, stroke, stroke-width, stroke-linecap, stroke-linejoin, opacity, stroke-opacity y transform, y
/// degradados lineales y radiales (en las coordenadas de cada figura).
/// </summary>
public sealed class Svg
{
    /// <summary>Una figura ya preparada: su forma, cómo se rellena y se perfila, dónde va y con qué opacidad.</summary>
    private sealed record Figura(Geometry Forma, IBrush? Relleno, IPen? Lapiz, Matrix Transformacion, double Opacidad);

    private readonly List<Figura> _figuras = new();
    public double Ancho { get; }
    public double Alto { get; }

    private static readonly Dictionary<string, Svg> Cargados = new();

    /// <summary>Uno de Recursos/Laminas (se lee una vez).</summary>
    public static Svg De(string nombre)
    {
        lock (Cargados)
        {
            if (Cargados.TryGetValue(nombre, out var s)) return s;
            using var flujo = AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/Laminas/" + nombre));
            return Cargados[nombre] = Leer(flujo);
        }
    }

    public static Svg Leer(Stream flujo) => new(XDocument.Load(flujo).Root!);

    private Svg(XElement raiz)
    {
        var vista = Numeros(raiz.Attribute("viewBox")?.Value ?? "");
        Ancho = vista.Length == 4 ? vista[2] : Numero(raiz, "width", 100);
        Alto = vista.Length == 4 ? vista[3] : Numero(raiz, "height", 100);
        var origen = vista.Length == 4 ? Matrix.CreateTranslation(-vista[0], -vista[1]) : Matrix.Identity;
        var degradados = raiz.Descendants().Where(e => e.Name.LocalName is "linearGradient" or "radialGradient")
            .ToDictionary(e => (string)e.Attribute("id")!);
        Recorrer(raiz, origen, new Pintura("#000", null, 1, PenLineCap.Flat, PenLineJoin.Miter, 1, 1), 1, degradados);
    }

    /// <summary>Lo que se hereda de un grupo a sus figuras.</summary>
    private sealed record Pintura(string? Relleno, string? Trazo, double Grosor, PenLineCap Remate, PenLineJoin Union, double OpacidadRelleno, double OpacidadTrazo);

    private void Recorrer(XElement e, Matrix m, Pintura p, double opacidad, Dictionary<string, XElement> degradados)
    {
        foreach (var hijo in e.Elements())
        {
            string tipo = hijo.Name.LocalName;
            if (tipo is "defs" or "linearGradient" or "radialGradient") continue;
            var mh = Transformacion(hijo.Attribute("transform")?.Value) * m;
            var ph = p with
            {
                Relleno = (string?)hijo.Attribute("fill") ?? p.Relleno,
                Trazo = (string?)hijo.Attribute("stroke") ?? p.Trazo,
                Grosor = Numero(hijo, "stroke-width", p.Grosor),
                Remate = (string?)hijo.Attribute("stroke-linecap") switch { "round" => PenLineCap.Round, "square" => PenLineCap.Square, "butt" => PenLineCap.Flat, _ => p.Remate },
                Union = (string?)hijo.Attribute("stroke-linejoin") switch { "round" => PenLineJoin.Round, "bevel" => PenLineJoin.Bevel, "miter" => PenLineJoin.Miter, _ => p.Union },
                OpacidadRelleno = Numero(hijo, "fill-opacity", p.OpacidadRelleno),
                OpacidadTrazo = Numero(hijo, "stroke-opacity", p.OpacidadTrazo),
            };
            double oh = opacidad * Numero(hijo, "opacity", 1);
            if (tipo == "g") { Recorrer(hijo, mh, ph, oh, degradados); continue; }
            Geometry? forma = tipo switch
            {
                "path" => Geometry.Parse((string?)hijo.Attribute("d") ?? ""),
                "circle" => Elipse(Numero(hijo, "cx", 0), Numero(hijo, "cy", 0), Numero(hijo, "r", 0), Numero(hijo, "r", 0)),
                "ellipse" => Elipse(Numero(hijo, "cx", 0), Numero(hijo, "cy", 0), Numero(hijo, "rx", 0), Numero(hijo, "ry", 0)),
                "rect" => new RectangleGeometry(new Rect(Numero(hijo, "x", 0), Numero(hijo, "y", 0), Numero(hijo, "width", 0), Numero(hijo, "height", 0))),
                _ => null,
            };
            if (forma == null) continue;
            var relleno = Pincel(ph.Relleno, ph.OpacidadRelleno, degradados);
            var trazo = Pincel(ph.Trazo, ph.OpacidadTrazo, degradados);
            var lapiz = trazo == null || ph.Grosor <= 0 ? null : new Pen(trazo, ph.Grosor) { LineCap = ph.Remate, LineJoin = ph.Union };
            if (relleno != null || lapiz != null) _figuras.Add(new Figura(forma, relleno, lapiz, mh, oh));
        }
    }

    private static EllipseGeometry Elipse(double cx, double cy, double rx, double ry) => new(new Rect(cx - rx, cy - ry, 2 * rx, 2 * ry));

    /// <summary>
    /// Lo dibuja en <paramref name="destino"/> (estirado a su tamaño); <paramref name="espejoX"/> y <paramref name="espejoY"/>
    /// le dan la vuelta (para poner el adorno de una esquina en las otras).
    /// </summary>
    public void Dibujar(DrawingContext ctx, Rect destino, bool espejoX = false, bool espejoY = false, double opacidad = 1)
    {
        var m = Matrix.CreateScale(destino.Width / Ancho * (espejoX ? -1 : 1), destino.Height / Alto * (espejoY ? -1 : 1))
                * Matrix.CreateTranslation(espejoX ? destino.Right : destino.X, espejoY ? destino.Bottom : destino.Y);
        using var _ = ctx.PushOpacity(opacidad);
        foreach (var f in _figuras)
        {
            using (ctx.PushTransform(f.Transformacion * m))
            using (ctx.PushOpacity(f.Opacidad))
                ctx.DrawGeometry(f.Relleno, f.Lapiz, f.Forma);
        }
    }

    // ---------- Lectura ----------

    private static IBrush? Pincel(string? valor, double opacidad, Dictionary<string, XElement> degradados)
    {
        if (valor is null or "none") return null;
        if (valor.StartsWith("url(#", StringComparison.Ordinal))
        {
            if (!degradados.TryGetValue(valor[5..^1], out var d)) return null;
            var g = Degradado(d);
            g.Opacity = opacidad;
            return g;
        }
        var c = Color.Parse(valor);
        return new SolidColorBrush(c, opacidad);
    }

    private static GradientBrush Degradado(XElement e)
    {
        var paradas = new GradientStops();
        foreach (var s in e.Elements().Where(s => s.Name.LocalName == "stop"))
        {
            var c = Color.Parse((string?)s.Attribute("stop-color") ?? "#000");
            double a = Numero(s, "stop-opacity", 1);
            paradas.Add(new GradientStop(Color.FromArgb((byte)Math.Round(c.A * a), c.R, c.G, c.B), Numero(s, "offset", 0)));
        }
        RelativePoint Punto(double x, double y) => new(x, y, RelativeUnit.Relative);
        if (e.Name.LocalName == "linearGradient")
            return new LinearGradientBrush
            {
                StartPoint = Punto(Numero(e, "x1", 0), Numero(e, "y1", 0)), EndPoint = Punto(Numero(e, "x2", 1), Numero(e, "y2", 0)),
                GradientStops = paradas,
            };
        double cx = Numero(e, "cx", 0.5), cy = Numero(e, "cy", 0.5), r = Numero(e, "r", 0.5);
        return new RadialGradientBrush
        {
            Center = Punto(cx, cy), GradientOrigin = Punto(Numero(e, "fx", cx), Numero(e, "fy", cy)),
            RadiusX = new RelativeScalar(r, RelativeUnit.Relative), RadiusY = new RelativeScalar(r, RelativeUnit.Relative),
            GradientStops = paradas,
        };
    }

    /// <summary>Una lista de transformaciones de SVG («translate(10 20) rotate(30)»): se aplica primero la de la derecha.</summary>
    internal static Matrix Transformacion(string? texto)
    {
        var m = Matrix.Identity;
        if (string.IsNullOrWhiteSpace(texto)) return m;
        foreach (var parte in texto.Split(')', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int i = parte.IndexOf('(');
            if (i < 0) continue;
            var n = Numeros(parte[(i + 1)..]);
            Matrix t = parte[..i].Trim() switch
            {
                "translate" => Matrix.CreateTranslation(n[0], n.Length > 1 ? n[1] : 0),
                "scale" => Matrix.CreateScale(n[0], n.Length > 1 ? n[1] : n[0]),
                "rotate" when n.Length == 3 => Matrix.CreateTranslation(-n[1], -n[2]) * Matrix.CreateRotation(n[0] * Math.PI / 180) * Matrix.CreateTranslation(n[1], n[2]),
                "rotate" => Matrix.CreateRotation(n[0] * Math.PI / 180),
                "matrix" => new Matrix(n[0], n[1], n[2], n[3], n[4], n[5]),
                _ => Matrix.Identity,
            };
            m = t * m;
        }
        return m;
    }

    private static double[] Numeros(string s) =>
        s.Split(new[] { ' ', ',', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

    private static double Numero(XElement e, string atributo, double defecto) =>
        e.Attribute(atributo)?.Value is { } v ? double.Parse(v.Replace("px", ""), CultureInfo.InvariantCulture) : defecto;
}
