using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>Los estilos de la lámina para regalar: el fondo, el marco, los adornos y las letras del título.</summary>
public enum EstiloLamina { Clasico, Pergamino, Floral, Navidad, Sencillo }

/// <summary>
/// La lámina para regalar (Ramo completo): el árbol entero en una sola hoja, con un título («Familia García López»), una
/// dedicatoria («Para la abuela, Navidad 2026») y el marco y el fondo de un estilo, lista para enmarcar o llevar a una
/// imprenta. Todo se dibuja en puntos de PDF (1/72 de pulgada) sobre una página del tamaño del papel; las medidas del
/// diseño están pensadas para un A3 apaisado (842 puntos de lado corto) y se escalan con el papel.
/// </summary>
public static class Lamina
{
    private const double PtPorMm = 72 / 25.4;
    /// <summary>El lado corto de un A3, en puntos: la medida para la que está hecho el diseño.</summary>
    private const double LadoA3 = 842;

    public static IReadOnlyList<EstiloLamina> Estilos { get; } = Enum.GetValues<EstiloLamina>();

    public static string Nombre(EstiloLamina e) => e switch
    {
        EstiloLamina.Clasico => T("Clásico"), EstiloLamina.Pergamino => T("Pergamino"), EstiloLamina.Floral => T("Floral"),
        EstiloLamina.Navidad => T("Navidad"), _ => T("Sencillo"),
    };

    /// <summary>El tamaño de la página, en puntos.</summary>
    public static Size Pagina(Papel papel, bool vertical) =>
        vertical ? new Size(papel.AnchoMm * PtPorMm, papel.AltoMm * PtPorMm) : new Size(papel.AltoMm * PtPorMm, papel.AnchoMm * PtPorMm);

    /// <summary>Si el árbol sale más grande con la hoja en vertical que apaisada.</summary>
    public static bool Vertical(LienzoArbol lienzo, Papel papel, EstiloLamina estilo, string titulo, string dedicatoria) =>
        EscalaArbol(lienzo, Pagina(papel, true), estilo, titulo, dedicatoria) > EscalaArbol(lienzo, Pagina(papel, false), estilo, titulo, dedicatoria);

    /// <summary>Puntos de la página por píxel del árbol (para saber de qué tamaño sale la letra).</summary>
    public static double EscalaArbol(LienzoArbol lienzo, Size pagina, EstiloLamina estilo, string titulo, string dedicatoria)
    {
        if (lienzo.Layout is not { } lay) return 0;
        var z = Componer(estilo, pagina, titulo, dedicatoria).Arbol;
        return Math.Max(0, Math.Min(z.Width / lay.Ancho, z.Height / lay.Alto));
    }

    // ---------- Composición ----------

    /// <summary>Un texto ya colocado.</summary>
    private sealed record Texto(FormattedText Formato, Point Donde);

    /// <summary>Dónde va cada cosa: los textos, la zona del árbol y lo que separa el título del árbol.</summary>
    private sealed record Composicion(double K, List<Texto> Textos, Rect Arbol, Rect? Separador, double FinTitulo, Rect? Titulo);

    private static FontFamily Fuente(string familia) => new("avares://ArbolGenealogico.Movil/Recursos/Fuentes#" + familia);
    private static readonly FontFamily Cormorant = Fuente("Cormorant Garamond"), Pinyon = Fuente("Pinyon Script"),
        Fell = Fuente("IM FELL English"), Playfair = Fuente("Playfair Display");

    /// <summary>Las letras y los márgenes de un estilo (en puntos de un A3).</summary>
    private sealed record Letras(Typeface Titulo, double TamTitulo, Color ColorTitulo, Typeface Dedicatoria, double TamDedicatoria,
        Color ColorDedicatoria, double Arriba, double Lados, double Abajo, double Hueco, bool AlLado = false);

    private static Letras De(EstiloLamina e) => e switch
    {
        EstiloLamina.Clasico => new(new(Cormorant, FontStyle.Normal, FontWeight.SemiBold), 54, VerdeClasico,
            new(Cormorant, FontStyle.Italic, FontWeight.Medium), 23, Color.Parse("#5A5040"), 60, 80, 56, 14),
        EstiloLamina.Pergamino => new(new(Pinyon), 72, Tinta, new(Fell, FontStyle.Italic), 22, Color.Parse("#6B4A2A"), 52, 84, 70, 12),
        EstiloLamina.Floral => new(new(Playfair, FontStyle.Italic, FontWeight.Medium), 52, Color.Parse("#3F5A3A"),
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 21, Color.Parse("#A0584E"), 58, 116, 58, 16),
        EstiloLamina.Navidad => new(new(Playfair, FontStyle.Normal, FontWeight.SemiBold), 50, VerdeNavidad,
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 22, RojoNavidad, 100, 110, 64, 14),
        _ => new(new(Estilo.Letra, FontStyle.Normal, FontWeight.SemiBold), 46, Color.Parse("#1F2D22"),
            new(Estilo.Letra, FontStyle.Normal, FontWeight.Medium), 20, Color.Parse("#56685A"), 62, 72, 60, 20, AlLado: true),
    };

    private static readonly Color VerdeClasico = Color.Parse("#2F4A35"), Oro = Color.Parse("#B8913A"), Tinta = Color.Parse("#4A2E17"),
        VerdeNavidad = Color.Parse("#1F4D2E"), RojoNavidad = Color.Parse("#A3262A"), VerdeSencillo = Color.Parse("#2F7A3A");

    /// <summary>Un texto de ese tamaño, que se encoge (hasta la mitad) si no cabe en <paramref name="ancho"/>.</summary>
    private static FormattedText Encajar(string s, Typeface letra, double tam, Color color, double ancho)
    {
        FormattedText Hacer(double t) => new(s, Cultura, FlowDirection.LeftToRight, letra, t, new SolidColorBrush(color));
        var f = Hacer(tam);
        if (f.WidthIncludingTrailingWhitespace > ancho) f = Hacer(Math.Max(tam * 0.5, tam * ancho / f.WidthIncludingTrailingWhitespace * 0.98));
        if (f.WidthIncludingTrailingWhitespace > ancho) { f.MaxTextWidth = ancho; f.Trimming = TextTrimming.CharacterEllipsis; f.MaxLineCount = 1; }
        return f;
    }

    private static Composicion Componer(EstiloLamina e, Size pagina, string titulo, string dedicatoria)
    {
        double w = pagina.Width, h = pagina.Height, k = Math.Min(w, h) / LadoA3;
        var l = De(e);
        var textos = new List<Texto>();
        double lados = l.Lados * k, y = l.Arriba * k, ancho = w - 2 * lados;
        Rect? separador = null, rectTitulo = null;
        titulo = titulo.Trim(); dedicatoria = dedicatoria.Trim();
        if (l.AlLado)
        {
            // Sencillo: el título a la izquierda y la dedicatoria a la derecha, sobre la misma línea, con una raya debajo
            var ded = dedicatoria.Length > 0 ? Encajar(dedicatoria, l.Dedicatoria, l.TamDedicatoria * k, l.ColorDedicatoria, ancho * 0.45) : null;
            double sitio = ancho - (ded != null ? ded.WidthIncludingTrailingWhitespace + 24 * k : 0);
            var tit = titulo.Length > 0 ? Encajar(titulo, l.Titulo, l.TamTitulo * k, l.ColorTitulo, sitio) : null;
            double alto = Math.Max(tit?.Height ?? 0, ded?.Height ?? 0);
            if (tit != null) textos.Add(new(tit, new Point(lados, y + alto - tit.Height)));
            if (ded != null) textos.Add(new(ded, new Point(w - lados - ded.WidthIncludingTrailingWhitespace, y + alto - ded.Height - (tit != null ? tit.Height * 0.12 : 0))));
            if (tit != null || ded != null) { y += alto + 8 * k; separador = new Rect(lados, y, ancho, 2 * k); y += 2 * k; }
        }
        else
        {
            if (titulo.Length > 0)
            {
                var tit = Encajar(titulo, l.Titulo, l.TamTitulo * k, l.ColorTitulo, e == EstiloLamina.Navidad ? ancho - 100 * k : ancho);
                textos.Add(new(tit, new Point((w - tit.WidthIncludingTrailingWhitespace) / 2, y)));
                rectTitulo = new Rect((w - tit.WidthIncludingTrailingWhitespace) / 2, y, tit.WidthIncludingTrailingWhitespace, tit.Height);
                y += tit.Height;
                if (e == EstiloLamina.Clasico) { separador = new Rect((w - 280 * k) / 2, y + 2 * k, 280 * k, 20 * k); y += 26 * k; }
            }
            if (dedicatoria.Length > 0)
            {
                var ded = Encajar(dedicatoria, l.Dedicatoria, l.TamDedicatoria * k, l.ColorDedicatoria, ancho);
                textos.Add(new(ded, new Point((w - ded.WidthIncludingTrailingWhitespace) / 2, y)));
                y += ded.Height;
            }
        }
        double fin = y;
        if (textos.Count > 0) y += l.Hueco * k;
        var arbol = new Rect(lados, y, ancho, Math.Max(1, h - l.Abajo * k - y));
        return new Composicion(k, textos, arbol, separador, fin, rectTitulo);
    }

    // ---------- Dibujo ----------

    /// <summary>Dibuja la lámina entera en una página de ese tamaño (en puntos).</summary>
    public static void Dibujar(DrawingContext ctx, Size pagina, LienzoArbol lienzo, EstiloLamina estilo, string titulo, string dedicatoria)
    {
        var c = Componer(estilo, pagina, titulo, dedicatoria);
        double w = pagina.Width, h = pagina.Height, k = c.K;
        var todo = new Rect(pagina);
        switch (estilo)
        {
            case EstiloLamina.Clasico:
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FBF7EE")), todo);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(VerdeClasico), 3 * k), todo.Deflate(27.5 * k));
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Oro), 1 * k), todo.Deflate(36.5 * k));
                Esquinas(ctx, "clasico-esquina.svg", todo.Deflate(30 * k), 80 * k, 80 * k, abajo: true);
                if (c.Separador is { } s) Svg.De("clasico-separador.svg").Dibujar(ctx, s);
                break;
            case EstiloLamina.Pergamino:
                Pergamino(ctx, todo, k);
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0xBF, 0x5C, 0x3A, 0x1C)), 2 * k), new RoundedRect(todo.Deflate(31 * k), 6 * k));
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0x5C, 0x3A, 0x1C)), 1 * k) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) },
                    new RoundedRect(todo.Deflate(38.5 * k), 4 * k));
                Svg.De("pergamino-adorno.svg").Dibujar(ctx, new Rect((w - 130 * k) / 2, h - 60 * k, 130 * k, 41 * k));
                break;
            case EstiloLamina.Floral:
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FFFBF6")), todo);
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#C9D8C0")), 1.5 * k), new RoundedRect(todo.Deflate(30.75 * k), 18 * k));
                var flores = Svg.De("floral-esquina.svg");
                double fw = 400 * k, fh = fw * flores.Alto / flores.Ancho;
                flores.Dibujar(ctx, new Rect(8 * k, 8 * k, fw, fh));
                flores.Dibujar(ctx, new Rect(w - 8 * k - fw, h - 8 * k - fh, fw, fh), espejoX: true, espejoY: true);
                break;
            case EstiloLamina.Navidad:
                Navidad(ctx, todo, k, c);
                break;
            default:
                ctx.FillRectangle(Brushes.White, todo);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#C8D2C9")), 1 * k), todo.Deflate(34.5 * k));
                if (c.Separador is { } raya) ctx.FillRectangle(new SolidColorBrush(VerdeSencillo), raya);
                break;
        }

        // el árbol, lo más grande que quepa en su zona, centrado
        if (lienzo.Layout is { } lay)
        {
            var z = c.Arbol;
            double s = Math.Min(z.Width / lay.Ancho, z.Height / lay.Alto);
            double x = z.X + (z.Width - lay.Ancho * s) / 2, y = z.Y + (z.Height - lay.Alto * s) / 2;
            using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x, y)))
                lienzo.DibujarEnPapel(ctx, new Rect(0, 0, lay.Ancho, lay.Alto));
        }

        foreach (var t in c.Textos) ctx.DrawText(t.Formato, t.Donde);
    }

    /// <summary>El adorno de una esquina en las de arriba (y, con <paramref name="abajo"/>, en las cuatro), dentro de <paramref name="caja"/>.</summary>
    private static void Esquinas(DrawingContext ctx, string adorno, Rect caja, double ancho, double alto, bool abajo)
    {
        var svg = Svg.De(adorno);
        svg.Dibujar(ctx, new Rect(caja.X, caja.Y, ancho, alto));
        svg.Dibujar(ctx, new Rect(caja.Right - ancho, caja.Y, ancho, alto), espejoX: true);
        if (!abajo) return;
        svg.Dibujar(ctx, new Rect(caja.X, caja.Bottom - alto, ancho, alto), espejoY: true);
        svg.Dibujar(ctx, new Rect(caja.Right - ancho, caja.Bottom - alto, ancho, alto), espejoX: true, espejoY: true);
    }

    private static RelativePoint Rel(double x, double y) => new(x, y, RelativeUnit.Relative);

    private static RadialGradientBrush Mancha(double cx, double cy, double r, Color color, double desde = 0) => new()
    {
        Center = Rel(cx, cy), GradientOrigin = Rel(cx, cy),
        RadiusX = new RelativeScalar(r, RelativeUnit.Relative), RadiusY = new RelativeScalar(r, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), desde), new GradientStop(color, 1) },
    };

    /// <summary>Papel envejecido: más claro arriba a la izquierda, manchas tostadas y los bordes oscurecidos.</summary>
    private static void Pergamino(DrawingContext ctx, Rect todo, double k)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#EBDCB8")), todo);
        // la luz: del centro (claro) hacia fuera (transparente)
        ctx.FillRectangle(new RadialGradientBrush
        {
            Center = Rel(0.3, 0.2), GradientOrigin = Rel(0.3, 0.2),
            RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0xBF, 0xFF, 0xFA, 0xE8), 0), new GradientStop(Color.FromArgb(0, 0xFF, 0xFA, 0xE8), 1) },
        }, todo);
        ctx.FillRectangle(new RadialGradientBrush
        {
            Center = Rel(0.75, 0.8), GradientOrigin = Rel(0.75, 0.8),
            RadiusX = new RelativeScalar(0.6, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.6, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0x38, 0xB0, 0x84, 0x48), 0), new GradientStop(Color.FromArgb(0, 0xB0, 0x84, 0x48), 1) },
        }, todo);
        // motas y manchas, siempre en el mismo sitio (para que la vista previa no cambie al tocar nada)
        var rnd = new Random(1789);
        for (int i = 0; i < 14; i++)
        {
            double r = rnd.Next(30, 90) * k;
            var p = new Point(rnd.NextDouble() * todo.Width, rnd.NextDouble() * todo.Height);
            ctx.DrawEllipse(new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.FromArgb((byte)rnd.Next(10, 22), 0x9A, 0x6A, 0x30), 0), new GradientStop(Color.FromArgb(0, 0x9A, 0x6A, 0x30), 1) },
            }, null, p, r, r * (0.6 + rnd.NextDouble() * 0.5));
        }
        for (int i = 0; i < 500; i++)
        {
            double r = (0.4 + rnd.NextDouble() * 1.2) * k;
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)rnd.Next(14, 40), 0x6B, 0x45, 0x20)), null,
                new Point(rnd.NextDouble() * todo.Width, rnd.NextDouble() * todo.Height), r, r);
        }
        // los bordes, tostados: un halo que oscurece hacia las esquinas y una sombra por dentro de cada borde
        ctx.FillRectangle(new RadialGradientBrush
        {
            RadiusX = new RelativeScalar(0.71, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.71, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0, 0x78, 0x50, 0x1E), 0.55), new GradientStop(Color.FromArgb(0x61, 0x78, 0x50, 0x1E), 1) },
        }, todo);
        double b = 60 * k;
        var sombra = Color.FromArgb(0x70, 0x6E, 0x46, 0x19);
        var nada = Color.FromArgb(0, 0x6E, 0x46, 0x19);
        LinearGradientBrush Borde(double x1, double y1, double x2, double y2) => new()
        {
            StartPoint = Rel(x1, y1), EndPoint = Rel(x2, y2), GradientStops = { new GradientStop(sombra, 0), new GradientStop(nada, 1) },
        };
        ctx.FillRectangle(Borde(0, 0, 0, 1), new Rect(0, 0, todo.Width, b));
        ctx.FillRectangle(Borde(0, 1, 0, 0), new Rect(0, todo.Height - b, todo.Width, b));
        ctx.FillRectangle(Borde(0, 0, 1, 0), new Rect(0, 0, b, todo.Height));
        ctx.FillRectangle(Borde(1, 0, 0, 0), new Rect(todo.Width - b, 0, b, todo.Height));
    }

    /// <summary>Papel crema con copos muy suaves, marco verde con filete rojo, guirnaldas en las esquinas, el lazo y las estrellas.</summary>
    private static void Navidad(DrawingContext ctx, Rect todo, double k, Composicion c)
    {
        double w = todo.Width, h = todo.Height;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FFF9EF")), todo);
        // copos de nieve muy suaves, repartidos (siempre en el mismo sitio)
        var copo = Svg.De("copo.svg");
        var rnd = new Random(2512);
        for (double y = 60 * k; y < h - 50 * k; y += 70 * k)
            for (double x = 60 * k + (y / (70 * k) % 2) * 35 * k; x < w - 50 * k; x += 70 * k)
            {
                double lado = (12 + rnd.NextDouble() * 10) * k;
                copo.Dibujar(ctx, new Rect(x + rnd.NextDouble() * 20 * k, y + rnd.NextDouble() * 20 * k, lado, lado), opacidad: 0.22);
            }
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(VerdeNavidad), 10 * k), new RoundedRect(todo.Deflate(29 * k), 4 * k));
        ctx.DrawRectangle(new Pen(new SolidColorBrush(RojoNavidad), 1.5 * k), todo.Deflate(40.75 * k));
        var guirnalda = Svg.De("navidad-esquina.svg");
        double gw = 330 * k, gh = gw * guirnalda.Alto / guirnalda.Ancho;
        guirnalda.Dibujar(ctx, new Rect(10 * k, 10 * k, gw, gh));
        guirnalda.Dibujar(ctx, new Rect(w - 10 * k - gw, 10 * k, gw, gh), espejoX: true);
        // abajo, más pequeñas
        double pw = gw * 0.55, ph = gh * 0.55;
        guirnalda.Dibujar(ctx, new Rect(12 * k, h - 12 * k - ph, pw, ph), espejoY: true);
        guirnalda.Dibujar(ctx, new Rect(w - 12 * k - pw, h - 12 * k - ph, pw, ph), espejoX: true, espejoY: true);
        var lazo = Svg.De("navidad-lazo.svg");
        double lw = 130 * k, lh = lw * lazo.Alto / lazo.Ancho;
        lazo.Dibujar(ctx, new Rect((w - lw) / 2, 4 * k, lw, lh));
        if (c.Titulo is { } t)
        {
            var estrella = Svg.De("estrella.svg");
            double e = 26 * k, y = t.Y + (t.Height - e) / 2;
            estrella.Dibujar(ctx, new Rect(t.X - 20 * k - e, y, e, e));
            estrella.Dibujar(ctx, new Rect(t.Right + 20 * k, y, e, e));
        }
    }

    // ---------- PDF e imagen ----------

    /// <summary>Guarda la lámina en un PDF de una página del tamaño del papel, con todo vectorial (también los adornos).</summary>
    public static async Task GuardarPdf(string ruta, LienzoArbol lienzo, Papel papel, bool vertical, EstiloLamina estilo, string titulo, string dedicatoria)
    {
        var pagina = Pagina(papel, vertical);
        var hoja = new Hoja(lienzo, estilo, titulo, dedicatoria) { Width = pagina.Width, Height = pagina.Height };
        var r = new Rect(pagina);
        hoja.Measure(pagina);
        hoja.Arrange(r);
        await using var s = File.Create(ruta);
        using var doc = SkiaSharp.SKDocument.CreatePdf(s, new SkiaSharp.SKDocumentPdfMetadata
        {
            Title = titulo.Trim().Length > 0 ? titulo.Trim() : T("Árbol genealógico"), Creator = "Ramo", Producer = "Ramo",
            Creation = DateTime.Now, Modified = DateTime.Now,
        });
        var canvas = doc.BeginPage((float)pagina.Width, (float)pagina.Height);
        await Avalonia.Skia.Helpers.DrawingContextHelper.RenderAsync(canvas, hoja, r, new Vector(96, 96));
        doc.EndPage();
        doc.Close();
    }

    /// <summary>La lámina como imagen (para las pruebas y las capturas), con <paramref name="escala"/> píxeles por punto.</summary>
    public static Avalonia.Media.Imaging.RenderTargetBitmap Imagen(LienzoArbol lienzo, Papel papel, bool vertical, EstiloLamina estilo, string titulo, string dedicatoria, double escala)
    {
        var pagina = Pagina(papel, vertical);
        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Math.Ceiling(pagina.Width * escala), (int)Math.Ceiling(pagina.Height * escala)), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateScale(escala, escala)))
            Dibujar(ctx, pagina, lienzo, estilo, titulo, dedicatoria);
        return rtb;
    }

    /// <summary>Un control fuera de la pantalla con la lámina, para pasarla al PDF.</summary>
    private sealed class Hoja(LienzoArbol lienzo, EstiloLamina estilo, string titulo, string dedicatoria) : Control
    {
        public override void Render(DrawingContext ctx) => Dibujar(ctx, Bounds.Size, lienzo, estilo, titulo, dedicatoria);
    }
}

/// <summary>La vista previa de la lámina: la hoja entera, con una sombra, sobre el fondo de la pantalla.</summary>
public sealed class VistaLamina : Control
{
    private LienzoArbol? _lienzo;
    private Size _pagina;
    private EstiloLamina _estilo;
    private string _titulo = "", _dedicatoria = "";
    private const double Margen = 14;

    public void Mostrar(LienzoArbol lienzo, Size pagina, EstiloLamina estilo, string titulo, string dedicatoria)
    {
        _lienzo = lienzo; _pagina = pagina; _estilo = estilo; _titulo = titulo; _dedicatoria = dedicatoria;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        if (_lienzo == null || _pagina.Width <= 0 || Bounds.Width <= 2 * Margen || Bounds.Height <= 2 * Margen) return;
        double s = Math.Min((Bounds.Width - 2 * Margen) / _pagina.Width, (Bounds.Height - 2 * Margen) / _pagina.Height);
        double x = (Bounds.Width - _pagina.Width * s) / 2, y = (Bounds.Height - _pagina.Height * s) / 2;
        var hoja = new Rect(x, y, _pagina.Width * s, _pagina.Height * s);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), null, new RoundedRect(hoja.Translate(new Vector(0, 3)), 2));
        using (ctx.PushClip(hoja))
        using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x, y)))
            Lamina.Dibujar(ctx, _pagina, _lienzo, _estilo, _titulo, _dedicatoria);
    }
}
