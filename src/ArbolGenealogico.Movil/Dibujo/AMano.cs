using Avalonia;
using Avalonia.Media;
using ArbolGenealogico.Core.Layout;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// Trazos «a mano» para el tema Papel y tinta: los contornos de las tarjetas y las líneas del árbol tiemblan un poco, como
/// dibujados con plumilla. El temblor sale de una semilla fija (de la persona o de la línea), así que es siempre el mismo y
/// no baila al mover ni al ampliar el árbol.
/// </summary>
public static class AMano
{
    /// <summary>Una semilla estable para un texto (el hash de .NET cambia en cada arranque).</summary>
    public static int Semilla(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) h = (h ^ c) * 16777619;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    public static int Semilla(IReadOnlyList<Pt> pts)
    {
        unchecked
        {
            int h = 17;
            foreach (var p in pts) h = h * 31 + (int)(p.X * 7) * 13 + (int)(p.Y * 7);
            return h & 0x7FFFFFFF;
        }
    }

    /// <summary>Un temblor suave a lo largo del trazo (suma de ondas con fases de la semilla), entre −1 y 1 más o menos.</summary>
    private static Func<double, double> Ruido(int semilla)
    {
        var r = new Random(semilla);
        var ondas = new (double F, double Fase, double A)[]
        {
            (0.03 + r.NextDouble() * 0.01, r.NextDouble() * 6.3, 0.45),
            (0.08 + r.NextDouble() * 0.02, r.NextDouble() * 6.3, 0.33),
            (0.21 + r.NextDouble() * 0.05, r.NextDouble() * 6.3, 0.22),
        };
        return s => ondas.Sum(o => o.A * Math.Sin(o.F * s + o.Fase));
    }

    /// <summary>El contorno de un rectángulo redondeado, cerrado (para rellenar) o con el final pasado del principio (para el
    /// trazo, como cuando se cierra a mano).</summary>
    public static Geometry Caja(Rect r, double radio, double amp, int semilla, bool trazo)
    {
        radio = Math.Min(radio, Math.Min(r.Width, r.Height) / 2);
        var pts = new List<Point>();
        double paso = 5;
        void Lado(Point a, Point b)
        {
            double l = Dist(a, b); int n = Math.Max(1, (int)(l / paso));
            for (int i = 0; i < n; i++) pts.Add(new Point(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n));
        }
        void Esquina(Point c, double desde)
        {
            int n = Math.Max(2, (int)(radio * Math.PI / 2 / paso));
            for (int i = 0; i < n; i++)
            {
                double ang = desde + Math.PI / 2 * i / n;
                pts.Add(new Point(c.X + radio * Math.Cos(ang), c.Y + radio * Math.Sin(ang)));
            }
        }
        double x0 = r.X, y0 = r.Y, x1 = r.Right, y1 = r.Bottom;
        Lado(new Point(x0 + radio, y0), new Point(x1 - radio, y0)); Esquina(new Point(x1 - radio, y0 + radio), -Math.PI / 2);
        Lado(new Point(x1, y0 + radio), new Point(x1, y1 - radio)); Esquina(new Point(x1 - radio, y1 - radio), 0);
        Lado(new Point(x1 - radio, y1), new Point(x0 + radio, y1)); Esquina(new Point(x0 + radio, y1 - radio), Math.PI / 2);
        Lado(new Point(x0, y1 - radio), new Point(x0, y0 + radio)); Esquina(new Point(x0 + radio, y0 + radio), Math.PI);

        var ruido = Ruido(semilla);
        var c0 = r.Center;
        int total = pts.Count, extra = trazo ? Math.Max(2, total / 30) : 0;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            double s = 0;
            for (int i = 0; i <= total + extra; i++)
            {
                var p = pts[i % total];
                if (i > 0) s += Dist(pts[(i - 1) % total], p);
                // hacia fuera del centro; al pasar del principio, un poco más fuera, para que se note el cierre
                var (nx, ny) = Normal(p.X - c0.X, p.Y - c0.Y);
                double d = amp * ruido(s) + (i >= total ? amp * 0.6 * (i - total + 1) / (extra + 1) : 0);
                var q = new Point(p.X + nx * d, p.Y + ny * d);
                if (i == 0) ctx.BeginFigure(q, !trazo); else ctx.LineTo(q);
            }
            ctx.EndFigure(!trazo);
        }
        return g;
    }

    /// <summary>Una línea del árbol (polilínea ortogonal con esquinas redondeadas de radio r) con temblor; en los extremos no
    /// tiembla, para que siga llegando justo a la tarjeta.</summary>
    public static Geometry Linea(IReadOnlyList<Pt> puntos, double r, double amp, int semilla)
    {
        var g = new StreamGeometry();
        if (puntos.Count < 2) return g;
        var pts = new List<Point> { new(puntos[0].X, puntos[0].Y) };
        void Recta(Point b)
        {
            var a = pts[^1]; double l = Dist(a, b); int n = Math.Max(1, (int)(l / 6));
            for (int i = 1; i <= n; i++) pts.Add(new Point(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n));
        }
        for (int i = 1; i < puntos.Count - 1; i++)
        {
            var a = puntos[i - 1]; var b = puntos[i]; var c = puntos[i + 1];
            double l1 = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            double l2 = Math.Sqrt((c.X - b.X) * (c.X - b.X) + (c.Y - b.Y) * (c.Y - b.Y));
            double rr = Math.Min(r, Math.Min(l1, l2) / 2);
            if (rr < 0.5) { Recta(new Point(b.X, b.Y)); continue; }
            var p1 = new Point(b.X + (a.X - b.X) / l1 * rr, b.Y + (a.Y - b.Y) / l1 * rr);
            var p2 = new Point(b.X + (c.X - b.X) / l2 * rr, b.Y + (c.Y - b.Y) / l2 * rr);
            Recta(p1);
            for (int k = 1; k <= 4; k++)
            {
                double t = k / 4.0, u = 1 - t;
                pts.Add(new Point(u * u * p1.X + 2 * u * t * b.X + t * t * p2.X, u * u * p1.Y + 2 * u * t * b.Y + t * t * p2.Y));
            }
        }
        Recta(new Point(puntos[^1].X, puntos[^1].Y));

        var ruido = Ruido(semilla);
        double largo = 0;
        for (int i = 1; i < pts.Count; i++) largo += Dist(pts[i - 1], pts[i]);
        using var ctx = g.Open();
        double s = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) s += Dist(pts[i - 1], pts[i]);
            var antes = pts[Math.Max(0, i - 1)]; var despues = pts[Math.Min(pts.Count - 1, i + 1)];
            var (nx, ny) = Normal(-(despues.Y - antes.Y), despues.X - antes.X);
            double suave = Math.Min(1, Math.Min(s, largo - s) / 14);
            double d = amp * ruido(s) * suave;
            var q = new Point(pts[i].X + nx * d, pts[i].Y + ny * d);
            if (i == 0) ctx.BeginFigure(q, false); else ctx.LineTo(q);
        }
        ctx.EndFigure(false);
        return g;
    }

    /// <summary>El lazo del icono, en pequeño, para el nudo de las parejas (centrado en c, de unos 2·tam de ancho).</summary>
    public static Geometry Lazo(Point c, double tam)
    {
        double k = tam / 56;
        Point P(double x, double y) => new(c.X + x * k, c.Y + y * k);
        var g = new StreamGeometry();
        using var ctx = g.Open();
        foreach (var s in new[] { -1, 1 })
        {
            ctx.BeginFigure(P(0, 0), true);
            ctx.CubicBezierTo(P(s * -28, -32), P(s * -60, -26), P(s * -56, -2));
            ctx.CubicBezierTo(P(s * -52, 22), P(s * -20, 16), P(0, 0));
            ctx.EndFigure(true);
            ctx.BeginFigure(P(s * -4, 2), true);
            ctx.LineTo(P(s * -28, 52)); ctx.LineTo(P(s * -16, 48)); ctx.LineTo(P(s * -10, 60)); ctx.LineTo(P(s * 2, 4));
            ctx.EndFigure(true);
        }
        return g;
    }

    private static IBrush? _grano;
    /// <summary>El grano del papel: puntitos de tinta muy suaves, en mosaico, por encima del fondo.</summary>
    public static IBrush Grano
    {
        get
        {
            if (_grano != null) return _grano;
            const int lado = 160;
            var bmp = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(lado, lado), new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
            var px = new int[lado * lado];
            var r = new Random(4);
            for (int i = 0; i < px.Length; i++)
            {
                // oscuro (fibras) o claro (brillo del papel), con poca opacidad y premultiplicado
                bool claro = r.NextDouble() < 0.35;
                int a = (int)(Math.Pow(r.NextDouble(), 2.2) * (claro ? 30 : 20));
                int c = claro ? 255 : 0x40;
                int pr = c * a / 255, pg = (claro ? 250 : 0x34) * a / 255, pb = (claro ? 240 : 0x28) * a / 255;
                px[i] = (a << 24) | (pr << 16) | (pg << 8) | pb;
            }
            using (var fb = bmp.Lock()) System.Runtime.InteropServices.Marshal.Copy(px, 0, fb.Address, px.Length);
            return _grano = new ImageBrush(bmp)
            {
                TileMode = TileMode.Tile, Stretch = Stretch.None,
                DestinationRect = new RelativeRect(0, 0, lado, lado, RelativeUnit.Absolute),
            };
        }
    }

    private static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static (double, double) Normal(double x, double y)
    {
        double l = Math.Sqrt(x * x + y * y);
        return l < 1e-9 ? (0, 0) : (x / l, y / l);
    }
}
