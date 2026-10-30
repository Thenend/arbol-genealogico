using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>Los estilos de la lámina para regalar: el fondo, el marco, los adornos y las letras del título.</summary>
public enum EstiloLamina { Clasico, Pergamino, Floral, Navidad, Sencillo, Dibujado, Album, Jardin, NocheDorada }

/// <summary>Una foto de fondo para la lámina (de la familia, del pueblo…) y con qué intensidad sale (de 0 a 1).</summary>
public sealed record FondoLamina(Avalonia.Media.Imaging.Bitmap Imagen, double Opacidad);

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
        EstiloLamina.Navidad => T("Navidad"), EstiloLamina.Dibujado => "Dibujado", EstiloLamina.Album => "Álbum",
        EstiloLamina.Jardin => "Jardín", EstiloLamina.NocheDorada => "Noche dorada", _ => T("Sencillo"),
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
        Fell = Fuente("IM FELL English"), Playfair = Fuente("Playfair Display"), Caveat = Fuente("Caveat");

    /// <summary>Las letras y los márgenes de un estilo (en puntos de un A3).</summary>
    private sealed record Letras(Typeface Titulo, double TamTitulo, Color ColorTitulo, Typeface Dedicatoria, double TamDedicatoria,
        Color ColorDedicatoria, double Arriba, double Lados, double Abajo, double Hueco, bool AlLado = false);

    private static Letras De(EstiloLamina e) => e switch
    {
        EstiloLamina.Clasico => new(new(Cormorant, FontStyle.Normal, FontWeight.SemiBold), 54, VerdeClasico,
            new(Cormorant, FontStyle.Italic, FontWeight.Medium), 23, Color.Parse("#5A5040"), 74, 90, 70, 14),
        EstiloLamina.Pergamino => new(new(Pinyon), 72, Tinta, new(Fell, FontStyle.Italic), 22, Color.Parse("#6B4A2A"), 52, 96, 120, 12),
        EstiloLamina.Floral => new(new(Playfair, FontStyle.Italic, FontWeight.Medium), 52, Color.Parse("#3F5A3A"),
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 21, Color.Parse("#A0584E"), 58, 116, 58, 16),
        EstiloLamina.Navidad => new(new(Playfair, FontStyle.Normal, FontWeight.SemiBold), 50, VerdeNavidad,
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 22, RojoNavidad, 100, 110, 64, 14),
        EstiloLamina.Dibujado => new(new(Caveat, FontStyle.Normal, FontWeight.Bold), 66, Color.Parse("#2B2622"),
            new(Caveat, FontStyle.Normal, FontWeight.Bold), 27, Color.Parse("#A8402F"), 76, 100, 72, 14),
        EstiloLamina.Album => new(new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 56, Color.Parse("#3A2A1A"),
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 22, Color.Parse("#76624B"), 92, 108, 84, 14),
        EstiloLamina.Jardin => new(new(Playfair, FontStyle.Normal, FontWeight.SemiBold), 52, Color.Parse("#2F5A36"),
            new(Estilo.Libro, FontStyle.Italic, FontWeight.Medium), 22, Color.Parse("#C2614A"), 96, 92, 96, 14),
        EstiloLamina.NocheDorada => new(new(Cormorant, FontStyle.Normal, FontWeight.SemiBold), 58, Color.Parse("#E8C670"),
            new(Cormorant, FontStyle.Italic, FontWeight.Medium), 24, Color.Parse("#C9C0A6"), 74, 90, 70, 14),
        _ => new(new(Estilo.Letra, FontStyle.Normal, FontWeight.SemiBold), 46, Color.Parse("#1F2D22"),
            new(Estilo.Letra, FontStyle.Normal, FontWeight.Medium), 20, Color.Parse("#56685A"), 62, 72, 84, 20, AlLado: true),
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
            // delante del título, una ramita
            double rama = 46 * k;
            double sitio = ancho - rama - (ded != null ? ded.WidthIncludingTrailingWhitespace + 24 * k : 0);
            var tit = titulo.Length > 0 ? Encajar(titulo, l.Titulo, l.TamTitulo * k, l.ColorTitulo, sitio) : null;
            double alto = Math.Max(tit?.Height ?? 0, ded?.Height ?? 0);
            if (tit != null)
            {
                textos.Add(new(tit, new Point(lados + rama, y + alto - tit.Height)));
                rectTitulo = new Rect(lados + rama, y + alto - tit.Height, tit.WidthIncludingTrailingWhitespace, tit.Height);
            }
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
                if (e == EstiloLamina.Dibujado) { separador = new Rect((w - 300 * k) / 2, y - 4 * k, 300 * k, 18 * k); y += 18 * k; }
                if (e == EstiloLamina.NocheDorada) { separador = new Rect((w - 300 * k) / 2, y + 2 * k, 300 * k, 28 * k); y += 34 * k; }
                if (e == EstiloLamina.Clasico) { separador = new Rect((w - 300 * k) / 2, y + 2 * k, 300 * k, 28 * k); y += 34 * k; }
                // la rúbrica del pergamino, algo metida bajo el título (la letra inglesa deja mucho hueco debajo)
                if (e == EstiloLamina.Pergamino) { separador = new Rect((w - 330 * k) / 2, y - 10 * k, 330 * k, 37 * k); y += 26 * k; }
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
    public static void Dibujar(DrawingContext ctx, Size pagina, LienzoArbol lienzo, EstiloLamina estilo, string titulo, string dedicatoria, FondoLamina? fondo = null)
    {
        var c = Componer(estilo, pagina, titulo, dedicatoria);
        double w = pagina.Width, h = pagina.Height, k = c.K;
        var todo = new Rect(pagina);
        // la foto de fondo va sobre el papel y bajo el marco y los adornos, dentro del marco
        void Foto() { if (fondo != null) DibujarFoto(ctx, fondo, ZonaFoto(estilo, todo, k)); }
        switch (estilo)
        {
            case EstiloLamina.Clasico:
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FBF7EE")), todo);
                // un leve tono dorado hacia los bordes, como un papel bueno
                ctx.FillRectangle(new RadialGradientBrush
                {
                    RadiusX = new RelativeScalar(0.71, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.71, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromArgb(0, 0xC9, 0xA2, 0x4A), 0.5), new GradientStop(Color.FromArgb(0x24, 0xC9, 0xA2, 0x4A), 1) },
                }, todo);
                Foto();
                ctx.DrawRectangle(new Pen(new SolidColorBrush(VerdeClasico), 3 * k), todo.Deflate(27.5 * k));
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Oro), 1 * k), todo.Deflate(36.5 * k));
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(0x80, VerdeClasico.R, VerdeClasico.G, VerdeClasico.B)), 0.6 * k), todo.Deflate(41.3 * k));
                Esquinas(ctx, "clasico-esquina.svg", todo.Deflate(26 * k), 130 * k, 130 * k, abajo: true);
                var centro = Svg.De("clasico-centro.svg");
                centro.Dibujar(ctx, new Rect((w - 150 * k) / 2, 20 * k, 150 * k, 43 * k));
                centro.Dibujar(ctx, new Rect((w - 150 * k) / 2, h - 63 * k, 150 * k, 43 * k), espejoY: true);
                if (c.Separador is { } s) Svg.De("clasico-separador.svg").Dibujar(ctx, s);
                break;
            case EstiloLamina.Pergamino:
                Pergamino(ctx, todo, k);
                Foto();
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0xBF, 0x5C, 0x3A, 0x1C)), 2 * k), new RoundedRect(todo.Deflate(31 * k), 6 * k));
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0x5C, 0x3A, 0x1C)), 1 * k) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) },
                    new RoundedRect(todo.Deflate(38.5 * k), 4 * k));
                Esquinas(ctx, "pergamino-esquina.svg", todo.Deflate(36 * k), 120 * k, 120 * k, abajo: true);
                if (c.Separador is { } rubrica) Svg.De("pergamino-rubrica.svg").Dibujar(ctx, rubrica);
                // el sello de lacre, sobre el marco de abajo
                var sello = Svg.De("pergamino-sello.svg");
                sello.Dibujar(ctx, new Rect((w - 86 * k) / 2, h - 112 * k, 86 * k, 86 * k * sello.Alto / sello.Ancho));
                break;
            case EstiloLamina.Floral:
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FFFBF6")), todo);
                Foto();
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#C9D8C0")), 1.5 * k), new RoundedRect(todo.Deflate(30.75 * k), 18 * k));
                var flores = Svg.De("floral-esquina.svg");
                double fw = 400 * k, fh = fw * flores.Alto / flores.Ancho;
                flores.Dibujar(ctx, new Rect(8 * k, 8 * k, fw, fh));
                flores.Dibujar(ctx, new Rect(w - 8 * k - fw, h - 8 * k - fh, fw, fh), espejoX: true, espejoY: true);
                break;
            case EstiloLamina.Navidad:
                Navidad(ctx, todo, k, c, Foto);
                break;
            case EstiloLamina.Dibujado:
                Dibujado(ctx, todo, k, c, Foto);
                break;
            case EstiloLamina.Album:
                Album(ctx, todo, k, Foto);
                break;
            case EstiloLamina.Jardin:
                Jardin(ctx, todo, k, Foto);
                break;
            case EstiloLamina.NocheDorada:
                ctx.FillRectangle(new SolidColorBrush(Color.Parse("#10162A")), todo);
                ctx.FillRectangle(new RadialGradientBrush
                {
                    RadiusX = new RelativeScalar(0.6, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.6, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromArgb(0x50, 0x2A, 0x3A, 0x6A), 0), new GradientStop(Color.FromArgb(0, 0x2A, 0x3A, 0x6A), 1) },
                }, todo);
                Foto();
                // estrellitas doradas muy suaves, siempre en el mismo sitio
                var rnd = new Random(1225);
                for (int i = 0; i < 160; i++)
                {
                    double r = (0.5 + rnd.NextDouble() * 1.3) * k;
                    ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)rnd.Next(30, 110), 0xE8, 0xC6, 0x70)), null,
                        new Point(rnd.NextDouble() * w, rnd.NextDouble() * h), r, r);
                }
                var oro = new SolidColorBrush(Color.Parse("#C9A85A"));
                ctx.DrawRectangle(new Pen(oro, 2.4 * k), todo.Deflate(27.5 * k));
                ctx.DrawRectangle(new Pen(oro, 0.8 * k), todo.Deflate(36.5 * k));
                Esquinas(ctx, "clasico-esquina.svg", todo.Deflate(26 * k), 130 * k, 130 * k, abajo: true);
                var centroN = Svg.De("clasico-centro.svg");
                centroN.Dibujar(ctx, new Rect((w - 150 * k) / 2, 20 * k, 150 * k, 43 * k));
                centroN.Dibujar(ctx, new Rect((w - 150 * k) / 2, h - 63 * k, 150 * k, 43 * k), espejoY: true);
                if (c.Separador is { } sn) Svg.De("clasico-separador.svg").Dibujar(ctx, sn);
                break;
            default:
                ctx.FillRectangle(Brushes.White, todo);
                Foto();
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#C8D2C9")), 1 * k), todo.Deflate(34.5 * k));
                if (c.Separador is { } raya) ctx.FillRectangle(new SolidColorBrush(VerdeSencillo), raya);
                if (c.Titulo is { } tit) Svg.De("sencillo-rama.svg").Dibujar(ctx, new Rect(tit.X - 46 * k, tit.Y + (tit.Height - 38 * k) / 2 + 3 * k, 38 * k, 38 * k));
                // abajo, en letra pequeña: qué es y cuántas personas tiene
                var pie = new Typeface(Estilo.Letra, FontStyle.Normal, FontWeight.Medium);
                var gris = new SolidColorBrush(Color.Parse("#7F8E82"));
                var que = new FormattedText(T("Árbol genealógico"), Cultura, FlowDirection.LeftToRight, pie, 13 * k, gris);
                ctx.DrawText(que, new Point(72 * k, h - 64 * k));
                if (lienzo.Arbol is { } arbol)
                {
                    var cuantas = new FormattedText(N(arbol.Personas.Count, "1 persona", "{0} personas"), Cultura, FlowDirection.LeftToRight, pie, 13 * k, gris);
                    ctx.DrawText(cuantas, new Point(w - 72 * k - cuantas.WidthIncludingTrailingWhitespace, h - 64 * k));
                }
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

    /// <summary>Dónde va la foto de fondo: todo lo de dentro del marco de cada estilo.</summary>
    private static RoundedRect ZonaFoto(EstiloLamina e, Rect todo, double k) => e switch
    {
        EstiloLamina.Clasico => new(todo.Deflate(41.3 * k)),
        EstiloLamina.Pergamino => new(todo.Deflate(38.5 * k), 4 * k),
        EstiloLamina.Floral => new(todo.Deflate(30.75 * k), 18 * k),
        EstiloLamina.Navidad => new(todo.Deflate(40.75 * k)),
        EstiloLamina.Dibujado => new(todo.Deflate(42 * k)),
        EstiloLamina.Album => new(todo.Deflate(52 * k)),
        EstiloLamina.Jardin => new(todo.Deflate(30 * k), 14 * k),
        EstiloLamina.NocheDorada => new(todo.Deflate(37 * k)),
        _ => new(todo.Deflate(34.5 * k)),
    };

    /// <summary>La foto llenando la zona (centrada, recortando lo que sobre) con su intensidad.</summary>
    private static void DibujarFoto(DrawingContext ctx, FondoLamina fondo, RoundedRect zona)
    {
        var img = fondo.Imagen.Size;
        var z = zona.Rect;
        if (img.Width <= 0 || img.Height <= 0 || z.Width <= 0 || z.Height <= 0) return;
        double s = Math.Max(z.Width / img.Width, z.Height / img.Height);
        double cw = z.Width / s, ch = z.Height / s;
        var origen = new Rect((img.Width - cw) / 2, (img.Height - ch) / 2, cw, ch);
        using (ctx.PushClip(zona))
        using (ctx.PushOpacity(Math.Clamp(fondo.Opacidad, 0, 1)))
            ctx.DrawImage(fondo.Imagen, origen, z);
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
    private static void Navidad(DrawingContext ctx, Rect todo, double k, Composicion c, Action foto)
    {
        double w = todo.Width, h = todo.Height;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FFF9EF")), todo);
        foto();
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

    /// <summary>
    /// Como el tema Papel y tinta: papel crema con grano, un marco doble dibujado a mano (con el mismo temblor que las líneas
    /// del árbol), ramitas de tinta en las esquinas, el lazo rojo arriba y una raya a mano bajo el título.
    /// </summary>
    private static void Dibujado(DrawingContext ctx, Rect todo, double k, Composicion c, Action foto)
    {
        var tinta = Color.Parse("#2B2622");
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#F6EEDF")), todo);
        ctx.FillRectangle(new RadialGradientBrush
        {
            RadiusX = new RelativeScalar(0.71, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.71, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0, 0xD9, 0xC4, 0x9C), 0.55), new GradientStop(Color.FromArgb(0x40, 0xD9, 0xC4, 0x9C), 1) },
        }, todo);
        ctx.FillRectangle(AMano.Grano, todo);
        foto();
        var lapiz = new Pen(new SolidColorBrush(tinta), 2.6 * k) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var fino = new Pen(new SolidColorBrush(tinta), 1.1 * k) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        // el temblor del marco, a la escala de la página (el de las tarjetas está pensado para su tamaño)
        using (ctx.PushTransform(Matrix.CreateScale(k, k)))
        {
            var t = new Rect(todo.X / k, todo.Y / k, todo.Width / k, todo.Height / k);
            ctx.DrawGeometry(null, new Pen(lapiz.Brush, 2.6) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, AMano.Caja(t.Deflate(30), 8, 2.4, 11, true));
            ctx.DrawGeometry(null, new Pen(fino.Brush, 1.1) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, AMano.Caja(t.Deflate(40), 4, 1.6, 12, true));
            // una ramita en cada esquina (la misma, girada), con las hojas dibujadas en contorno
            var rama = new[] { new ArbolGenealogico.Core.Layout.Pt(2, 120), new ArbolGenealogico.Core.Layout.Pt(10, 26), new ArbolGenealogico.Core.Layout.Pt(26, 10), new ArbolGenealogico.Core.Layout.Pt(120, 2) };
            var linea = AMano.Linea(rama, 60, 0.9, 5);
            var hojas = AMano.Hojas(rama, 9, paso: 13, margen: 0, tamHoja: 9);
            var papel = new SolidColorBrush(Color.Parse("#F6EEDF"));
            var hojaPen = new Pen(new SolidColorBrush(tinta), 0.75) { LineJoin = PenLineJoin.Round };
            var verde = new SolidColorBrush(Color.FromArgb(0x55, 0x58, 0x74, 0x3F));
            foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
            {
                double x = sx > 0 ? t.X + 50 : t.Right - 50, y = sy > 0 ? t.Y + 50 : t.Bottom - 50;
                using (ctx.PushTransform(Matrix.CreateScale(1.7 * sx, 1.7 * sy) * Matrix.CreateTranslation(x, y)))
                {
                    ctx.DrawGeometry(null, new Pen(new SolidColorBrush(tinta), 1.2) { LineCap = PenLineCap.Round }, linea);
                    ctx.DrawGeometry(papel, null, hojas);
                    ctx.DrawGeometry(verde, hojaPen, hojas);
                }
            }
            // el lazo rojo, sobre el marco de arriba
            var lazo = AMano.Lazo(new Point(t.Center.X, t.Y + 32), 40);
            ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#C0463A")), new Pen(new SolidColorBrush(tinta), 1.4) { LineJoin = PenLineJoin.Round }, lazo);
            if (c.Separador is { } sep)
            {
                var y = sep.Center.Y / k;
                var raya = new[] { new ArbolGenealogico.Core.Layout.Pt(sep.X / k, y), new ArbolGenealogico.Core.Layout.Pt(sep.Right / k, y) };
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(tinta), 1.5) { LineCap = PenLineCap.Round }, AMano.Linea(raya, 0, 1.6, 21));
                var corazon = new Point(sep.Center.X / k, y);
                AMano.Nudo(ctx, "corazon", corazon, Color.Parse("#C0463A"), tinta);
            }
        }
    }

    /// <summary>Una hoja de álbum antiguo: cartón kraft, la foto (el árbol) en papel crema con borde blanco y cuatro esquineros.</summary>
    private static void Album(DrawingContext ctx, Rect todo, double k, Action foto)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#B89A70")), todo);
        ctx.FillRectangle(AMano.Grano, todo);
        ctx.FillRectangle(new RadialGradientBrush
        {
            RadiusX = new RelativeScalar(0.71, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.71, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0, 0x5A, 0x40, 0x22), 0.5), new GradientStop(Color.FromArgb(0x50, 0x5A, 0x40, 0x22), 1) },
        }, todo);
        var hoja = todo.Deflate(40 * k);
        // la sombra de la foto sobre el cartón, el borde blanco y el papel
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x40, 0x2A, 0x1A, 0x0A)), null, hoja.Translate(new Vector(3 * k, 4 * k)));
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FDFBF5")), hoja);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#F6EEDC")), hoja.Deflate(12 * k));
        foto();
        ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0x5A, 0x40, 0x22)), 0.6 * k), hoja.Deflate(12 * k));
        // los esquineros, negros con un brillo, montados sobre las esquinas de la foto
        double l = 64 * k;
        foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            double x = sx > 0 ? hoja.X - 6 * k : hoja.Right + 6 * k, y = sy > 0 ? hoja.Y - 6 * k : hoja.Bottom + 6 * k;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(x, y), true); c.LineTo(new Point(x + sx * l, y)); c.LineTo(new Point(x, y + sy * l)); c.EndFigure(true);
            }
            ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#2A211A")), new Pen(new SolidColorBrush(Color.Parse("#14100C")), 0.8 * k), g);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xF4, 0xE0)), 1 * k),
                new Point(x + sx * l * 0.82, y + sy * 4 * k), new Point(x + sx * 4 * k, y + sy * l * 0.82));
        }
    }

    /// <summary>Papel claro con una guirnalda de hojas verdes y florecitas arriba y abajo, y un marco verde suave.</summary>
    private static void Jardin(DrawingContext ctx, Rect todo, double k, Action foto)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#F6F9F1")), todo);
        foto();
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#B9CFAE")), 1.4 * k), new RoundedRect(todo.Deflate(30 * k), 14 * k));
        var verde = new SolidColorBrush(Color.Parse("#6E9E5A"));
        var oscuro = new SolidColorBrush(Color.Parse("#4E7A3E"));
        var rama = new SolidColorBrush(Color.Parse("#7A6248"));
        using (ctx.PushTransform(Matrix.CreateScale(k, k)))
        {
            var t = new Rect(todo.X / k, todo.Y / k, todo.Width / k, todo.Height / k);
            foreach (var arriba in new[] { true, false })
            {
                double y = arriba ? t.Y + 30 : t.Bottom - 30;
                // una rama ondulada a lo largo del borde, de lado a lado
                var pts = new List<ArbolGenealogico.Core.Layout.Pt>();
                for (double x = t.X + 60; x <= t.Right - 60; x += 6)
                    pts.Add(new ArbolGenealogico.Core.Layout.Pt(x, y + Math.Sin((x - t.X) / 46) * 7));
                var linea = AMano.Linea(pts, 0, 0.6, arriba ? 3 : 4);
                ctx.DrawGeometry(null, new Pen(rama, 1.8) { LineCap = PenLineCap.Round }, linea);
                ctx.DrawGeometry(verde, new Pen(oscuro, 0.6), AMano.Hojas(pts, arriba ? 31 : 32, paso: 9, margen: 0, tamHoja: 11));
                // florecitas de cinco pétalos, coral y crema
                var rnd = new Random(arriba ? 7 : 8);
                for (double x = t.X + 120; x < t.Right - 100; x += 110 + rnd.NextDouble() * 50)
                {
                    var c0 = new Point(x, y + Math.Sin((x - t.X) / 46) * 7);
                    var color = rnd.Next(3) == 0 ? Color.Parse("#F3E3B8") : Color.Parse("#E8A08C");
                    for (int p = 0; p < 5; p++)
                    {
                        double a = p * Math.PI * 2 / 5 + rnd.NextDouble();
                        ctx.DrawEllipse(new SolidColorBrush(color), new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0xB0, 0x5A, 0x48)), 0.5),
                            new Point(c0.X + Math.Cos(a) * 5.5, c0.Y + Math.Sin(a) * 5.5), 4.6, 4.6);
                    }
                    ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E0B040")), null, c0, 2.6, 2.6);
                }
            }
        }
    }

    // ---------- PDF e imagen ----------

    /// <summary>Guarda la lámina en un PDF de una página del tamaño del papel, con todo vectorial (también los adornos).</summary>
    public static async Task GuardarPdf(string ruta, LienzoArbol lienzo, Papel papel, bool vertical, EstiloLamina estilo, string titulo, string dedicatoria, FondoLamina? fondo = null)
    {
        var pagina = Pagina(papel, vertical);
        var hoja = new Hoja(lienzo, estilo, titulo, dedicatoria, fondo) { Width = pagina.Width, Height = pagina.Height };
        var r = new Rect(pagina);
        hoja.Measure(pagina);
        hoja.Arrange(r);
        await using var s = File.Create(ruta);
        using var doc = SkiaSharp.SKDocument.CreatePdf(s, new SkiaSharp.SKDocumentPdfMetadata
        {
            Title = titulo.Trim().Length > 0 ? titulo.Trim() : T("Árbol genealógico"), Creator = "Ramo", Producer = "Ramo",
            Creation = DateTime.Now, Modified = DateTime.Now,
            // la foto de fondo, en JPEG (sin esto iría sin comprimir y el PDF pesaría muchísimo)
            EncodingQuality = 90,
        });
        var canvas = doc.BeginPage((float)pagina.Width, (float)pagina.Height);
        await Avalonia.Skia.Helpers.DrawingContextHelper.RenderAsync(canvas, hoja, r, new Vector(96, 96));
        doc.EndPage();
        doc.Close();
    }

    /// <summary>La lámina como imagen (para las pruebas y las capturas), con <paramref name="escala"/> píxeles por punto.</summary>
    public static Avalonia.Media.Imaging.RenderTargetBitmap Imagen(LienzoArbol lienzo, Papel papel, bool vertical, EstiloLamina estilo, string titulo, string dedicatoria, double escala, FondoLamina? fondo = null)
    {
        var pagina = Pagina(papel, vertical);
        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Math.Ceiling(pagina.Width * escala), (int)Math.Ceiling(pagina.Height * escala)), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateScale(escala, escala)))
            Dibujar(ctx, pagina, lienzo, estilo, titulo, dedicatoria, fondo);
        return rtb;
    }

    /// <summary>Un control fuera de la pantalla con la lámina, para pasarla al PDF.</summary>
    private sealed class Hoja(LienzoArbol lienzo, EstiloLamina estilo, string titulo, string dedicatoria, FondoLamina? fondo) : Control
    {
        public override void Render(DrawingContext ctx) => Dibujar(ctx, Bounds.Size, lienzo, estilo, titulo, dedicatoria, fondo);
    }
}

/// <summary>
/// La vista previa de la lámina: la hoja entera, con una sombra, sobre el fondo de la pantalla. Se amplía y se mueve como la
/// de las hojas para pegar (ver <see cref="VistaAmpliable"/>), para ver de cerca cómo saldrán las tarjetas y el título.
/// </summary>
public sealed class VistaLamina : VistaAmpliable
{
    private LienzoArbol? _lienzo;
    private Size _pagina;
    private EstiloLamina _estilo;
    private string _titulo = "", _dedicatoria = "";
    private FondoLamina? _fondo;
    /// <summary>Puntos de la página por píxel del árbol: para ampliar hasta lo mismo que en las hojas para pegar.</summary>
    private double _escalaArbol = 1;

    protected override Rect? Papel => _lienzo != null && _pagina.Width > 0 ? new Rect(_pagina) : null;
    /// <summary>Lo más cerca: las tarjetas al doble de su tamaño en el árbol (y la página, al menos a 3 píxeles por punto).</summary>
    protected override double EscalaMax => Math.Max(3, 2 / _escalaArbol);
    /// <summary>El doble toque: las tarjetas a un tamaño en el que se leen bien en el teléfono.</summary>
    protected override double EscalaCerca => Math.Max(1, 0.4 / _escalaArbol);

    public void Mostrar(LienzoArbol lienzo, Size pagina, EstiloLamina estilo, string titulo, string dedicatoria, FondoLamina? fondo)
    {
        _lienzo = lienzo; _pagina = pagina; _estilo = estilo; _titulo = titulo; _dedicatoria = dedicatoria; _fondo = fondo;
        _escalaArbol = Math.Max(1e-3, Lamina.EscalaArbol(lienzo, pagina, estilo, titulo, dedicatoria));
        Rehecho();
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_lienzo == null || _pagina.Width <= 0) return;
        double s = K, x = Ox, y = Oy;
        var hoja = new Rect(x, y, _pagina.Width * s, _pagina.Height * s);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), null, new RoundedRect(hoja.Translate(new Vector(0, 3)), 2));
        using (ctx.PushClip(hoja))
        using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x, y)))
            Lamina.Dibujar(ctx, _pagina, _lienzo, _estilo, _titulo, _dedicatoria, _fondo);
    }
}
