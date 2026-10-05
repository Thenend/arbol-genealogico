using System.Windows;
using System.Windows.Media;
using ArbolGenealogico.Core.Layout;

namespace ArbolGenealogico.App;

/// <summary>Dibuja todas las conexiones (parejas y descendencia) con esquinas redondeadas.</summary>
public sealed class AristasVisual : FrameworkElement
{
    private static readonly Color Oro = Color.FromRgb(0xF5, 0xC4, 0x51);
    private static readonly Color Linaje = Color.FromRgb(0x5E, 0xEA, 0xD4);
    private LayoutResult? _layout;
    private HashSet<Conexion> _resaltadas = new();
    private HashSet<Conexion> _cercanas = new();

    public AristasVisual() { IsHitTestVisible = false; }

    /// <summary>Estilo claro (para imprimir sobre papel blanco): grises más oscuros y un dorado más oscuro.</summary>
    public bool Claro { get; set; }

    /// <summary>Blanco y negro (impresoras de solo tinta negra): líneas en grises y la línea directa en negro, más gruesa.</summary>
    public bool BlancoYNegro { get; set; }

    public LayoutResult? Layout
    {
        get => _layout;
        set { _layout = value; InvalidateVisual(); }
    }

    /// <summary>
    /// Conexiones del linaje de la persona seleccionada (se iluminan) y las que llevan a sus otros familiares cercanos,
    /// como hermanos o cónyuges (se ven con normalidad). El resto se atenúa.
    /// </summary>
    public void Resaltar(HashSet<Conexion> linaje, HashSet<Conexion> cercanas)
    {
        _resaltadas = linaje; _cercanas = cercanas;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_layout == null) return;
        bool atenuar = _resaltadas.Count > 0;
        static Pen Lapiz(Color c, byte alfa, double grosor)
        {
            var p = new Pen(new SolidColorBrush(Color.FromArgb(alfa, c.R, c.G, c.B)), grosor) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            return p;
        }
        var gris = BlancoYNegro ? Color.FromRgb(0x80, 0x80, 0x80) : Claro ? Color.FromRgb(0x7C, 0x84, 0x96) : Color.FromRgb(0x5B, 0x65, 0x7D);
        var claro = Claro ? Color.FromRgb(0x6A, 0x72, 0x86) : Color.FromRgb(0x8A, 0x94, 0xAD);
        var oroLinea = BlancoYNegro ? Colors.Black : Claro ? Color.FromRgb(0xC2, 0x8A, 0x12) : Oro;
        if (BlancoYNegro) claro = Color.FromRgb(0x5C, 0x5C, 0x5C);
        // [0] = con normalidad, [1] = atenuada
        Pen[] normal = { Lapiz(gris, 0xFF, 2), Lapiz(gris, 0x55, 2) };
        Pen[] pareja = { Lapiz(claro, 0xFF, 2.5), Lapiz(claro, 0x55, 2.5) };
        Pen[] oro = { Lapiz(oroLinea, 0xFF, 3.6), Lapiz(oroLinea, 0x80, 3.6) };
        Pen[] resplandor = { Lapiz(Oro, 0x2C, 9), Lapiz(Oro, 0x10, 9) };
        // sobre papel blanco, el turquesa del linaje, más oscuro para que se vea
        var linaje = Claro ? Color.FromRgb(0x0F, 0x9C, 0x8C) : Linaje;
        var luz = Lapiz(linaje, 0xFF, 4);
        var luzResplandor = Lapiz(linaje, 0x40, 11);
        int Tono(Conexion c) => atenuar && !_cercanas.Contains(c) ? 1 : 0;

        // Primero las atenuadas, para que las que se ven con normalidad queden encima.
        var resto = _layout.Conexiones.Where(c => !_resaltadas.Contains(c)).OrderByDescending(Tono).ToList();
        foreach (var c in resto.Where(c => !c.Directa))
            dc.DrawGeometry(null, (c.Tipo == TipoConexion.Pareja ? pareja : normal)[Tono(c)], Camino(c.Puntos, 12));
        if (!BlancoYNegro)          // el brillo dorado, en negro sería una mancha gris
            foreach (var c in resto.Where(c => c.Directa))
                dc.DrawGeometry(null, resplandor[Tono(c)], Camino(c.Puntos, 12));
        foreach (var c in resto.Where(c => c.Directa))
            dc.DrawGeometry(null, oro[Tono(c)], Camino(c.Puntos, 12));

        // Linaje de la persona seleccionada: encima de todo.
        foreach (var c in _resaltadas) dc.DrawGeometry(null, luzResplandor, Camino(c.Puntos, 12));
        foreach (var c in _resaltadas) dc.DrawGeometry(null, luz, Camino(c.Puntos, 12));

        // Marca en el centro de cada pareja.
        var fondo = Claro ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16));
        foreach (var c in _layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2))
        {
            var m = c.Nudo is { } n ? new Point(n.X, n.Y) : new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
            Color borde = _resaltadas.Contains(c) ? linaje
                : Tono(c) == 1 ? Color.FromArgb(0x70, 0x8A, 0x94, 0xAD)
                : c.Directa ? oroLinea : claro;
            dc.DrawEllipse(fondo, new Pen(new SolidColorBrush(borde), 2), m, 5, 5);
        }
    }

    /// <summary>Polilínea ortogonal con esquinas redondeadas de radio máximo r.</summary>
    private static Geometry Camino(List<Pt> pts, double r)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            if (pts.Count < 2) return g;
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), false, false);
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
                double l1 = Dist(a, b), l2 = Dist(b, c);
                double rr = Math.Min(r, Math.Min(l1, l2) / 2);
                if (rr < 0.5) { ctx.LineTo(new Point(b.X, b.Y), true, true); continue; }
                var p1 = new Point(b.X + (a.X - b.X) / l1 * rr, b.Y + (a.Y - b.Y) / l1 * rr);
                var p2 = new Point(b.X + (c.X - b.X) / l2 * rr, b.Y + (c.Y - b.Y) / l2 * rr);
                ctx.LineTo(p1, true, true);
                // sentido del giro según el producto vectorial
                double cruz = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                ctx.ArcTo(p2, new Size(rr, rr), 0, false, cruz > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, true);
            }
            var u = pts[^1];
            ctx.LineTo(new Point(u.X, u.Y), true, true);
        }
        g.Freeze();
        return g;
    }

    private static double Dist(Pt a, Pt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
