using System.Globalization;
using System.Text;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Web.Servicios;

public static class Dibujo
{
    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Polilínea ortogonal con esquinas redondeadas de radio máximo r, como trazado SVG.</summary>
    public static string Camino(List<Pt> pts, double r = 12)
    {
        if (pts.Count < 2) return "";
        var sb = new StringBuilder();
        sb.Append('M').Append(N(pts[0].X)).Append(' ').Append(N(pts[0].Y));
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
            double l1 = Dist(a, b), l2 = Dist(b, c);
            double rr = Math.Min(r, Math.Min(l1, l2) / 2);
            if (rr < 0.5) { sb.Append(" L").Append(N(b.X)).Append(' ').Append(N(b.Y)); continue; }
            double p1x = b.X + (a.X - b.X) / l1 * rr, p1y = b.Y + (a.Y - b.Y) / l1 * rr;
            double p2x = b.X + (c.X - b.X) / l2 * rr, p2y = b.Y + (c.Y - b.Y) / l2 * rr;
            double cruz = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            sb.Append(" L").Append(N(p1x)).Append(' ').Append(N(p1y));
            sb.Append(" A").Append(N(rr)).Append(' ').Append(N(rr)).Append(" 0 0 ").Append(cruz > 0 ? '1' : '0').Append(' ')
              .Append(N(p2x)).Append(' ').Append(N(p2y));
        }
        var u = pts[^1];
        sb.Append(" L").Append(N(u.X)).Append(' ').Append(N(u.Y));
        return sb.ToString();
    }

    private static double Dist(Pt a, Pt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static string Num(double v) => N(v);

    /// <summary>
    /// Lo que se resalta al seleccionar a alguien: las líneas de su linaje (hacia sus antepasados y descendientes), las que
    /// llevan a otros familiares cercanos (hermanos, cónyuges; se ven con normalidad) y su familia directa (el resto se atenúa).
    /// </summary>
    public static (HashSet<Conexion> Linaje, HashSet<Conexion> Cercanas, HashSet<string>? Familia) Resaltado(Arbol arbol, LayoutResult layout, string? sel)
    {
        var res = new HashSet<Conexion>();
        var cercanas = new HashSet<Conexion>();
        if (sel == null || arbol.Buscar(sel) == null) return (res, cercanas, null);
        var (antepasados, descendientes) = arbol.Linaje(sel);
        var familia = arbol.FamiliaDirecta(sel);
        antepasados.Add(sel); descendientes.Add(sel);
        var uniones = arbol.Uniones.ToDictionary(u => u.Id);
        var conLinaje = new HashSet<string>();
        foreach (var c in layout.Conexiones.Where(c => c.Tipo == TipoConexion.Descendencia))
        {
            if (!uniones.TryGetValue(c.UnionId, out var u)) continue;
            bool sube = c.HijoId != null && antepasados.Contains(c.HijoId);
            bool baja = u.Parejas.Any(descendientes.Contains);
            if (sube || baja) { res.Add(c); conLinaje.Add(c.UnionId); }
        }
        foreach (var c in layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && conLinaje.Contains(c.UnionId))) res.Add(c);
        foreach (var c in layout.Conexiones)
        {
            if (res.Contains(c) || !uniones.TryGetValue(c.UnionId, out var u) || !u.Parejas.Any(familia.Contains)) continue;
            bool cerca = c.Tipo == TipoConexion.Pareja
                ? u.Parejas.All(familia.Contains) || u.Hijos.Any(familia.Contains)
                : c.HijoId != null && familia.Contains(c.HijoId);
            if (cerca) cercanas.Add(c);
        }
        return (res, cercanas, familia);
    }
}
