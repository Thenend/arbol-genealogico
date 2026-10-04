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
    private List<(Point a, Point b)> _cables = new();

    public AristasVisual() { IsHitTestVisible = false; }

    public LayoutResult? Layout
    {
        get => _layout;
        set { _layout = value; InvalidateVisual(); }
    }

    /// <summary>Conexiones del linaje de la persona seleccionada (se iluminan; el resto se atenúa).</summary>
    public HashSet<Conexion> Resaltadas
    {
        get => _resaltadas;
        set { _resaltadas = value; InvalidateVisual(); }
    }

    /// <summary>Enlaces punteados entre las dos tarjetas de una persona que aparece repetida (solo las del linaje seleccionado).</summary>
    public List<(Point a, Point b)> Cables
    {
        get => _cables;
        set { _cables = value; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_layout == null) return;
        bool atenuar = _resaltadas.Count > 0;
        byte a = atenuar ? (byte)0x55 : (byte)0xFF;
        var normal = new Pen(new SolidColorBrush(Color.FromArgb(a, 0x5B, 0x65, 0x7D)), 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var pareja = new Pen(new SolidColorBrush(Color.FromArgb(a, 0x8A, 0x94, 0xAD)), 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var oro = new Pen(new SolidColorBrush(Color.FromArgb(atenuar ? (byte)0x80 : (byte)0xFF, Oro.R, Oro.G, Oro.B)), 3.6) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var resplandor = new Pen(new SolidColorBrush(Color.FromArgb(atenuar ? (byte)0x10 : (byte)0x2C, Oro.R, Oro.G, Oro.B)), 9) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var luz = new Pen(new SolidColorBrush(Linaje), 4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var luzResplandor = new Pen(new SolidColorBrush(Color.FromArgb(0x40, Linaje.R, Linaje.G, Linaje.B)), 11) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        normal.Freeze(); pareja.Freeze(); oro.Freeze(); resplandor.Freeze(); luz.Freeze(); luzResplandor.Freeze();

        var resto = _layout.Conexiones.Where(c => !_resaltadas.Contains(c)).ToList();
        foreach (var c in resto.Where(c => !c.Directa))
            dc.DrawGeometry(null, c.Tipo == TipoConexion.Pareja ? pareja : normal, Camino(c.Puntos, 12));
        foreach (var c in resto.Where(c => c.Directa))
            dc.DrawGeometry(null, resplandor, Camino(c.Puntos, 12));
        foreach (var c in resto.Where(c => c.Directa))
            dc.DrawGeometry(null, oro, Camino(c.Puntos, 12));

        // Linaje de la persona seleccionada: encima de todo.
        foreach (var c in _resaltadas) dc.DrawGeometry(null, luzResplandor, Camino(c.Puntos, 12));
        foreach (var c in _resaltadas) dc.DrawGeometry(null, luz, Camino(c.Puntos, 12));

        // Cables entre las copias de una misma persona.
        if (_cables.Count > 0)
        {
            var cable = new Pen(new SolidColorBrush(Linaje), 2.4) { DashStyle = new DashStyle(new double[] { 2.5, 2.5 }, 0), StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            cable.Freeze();
            var punta = new SolidColorBrush(Linaje);
            foreach (var (desde, hasta) in _cables)
            {
                double alto = 70 + Math.Abs(hasta.X - desde.X) * 0.07;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(desde, false, false);
                    ctx.BezierTo(new Point(desde.X, desde.Y - alto), new Point(hasta.X, hasta.Y - alto), hasta, true, true);
                }
                g.Freeze();
                dc.DrawGeometry(null, cable, g);
                dc.DrawEllipse(punta, null, desde, 5, 5);
                dc.DrawEllipse(punta, null, hasta, 5, 5);
            }
        }

        // Marca en el centro de cada pareja.
        var fondo = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16));
        foreach (var c in _layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2))
        {
            var m = new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
            Color borde = _resaltadas.Contains(c) ? Linaje
                : atenuar ? Color.FromArgb(0x70, 0x8A, 0x94, 0xAD)
                : c.Directa ? Oro : Color.FromRgb(0x8A, 0x94, 0xAD);
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
