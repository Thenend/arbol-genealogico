namespace ArbolGenealogico.Core.Layout;

public sealed class InformeLayout
{
    public int Solapes { get; set; }
    public int CrucesAristas { get; set; }
    public int AristasSobreTarjetas { get; set; }
    public List<string> Detalles { get; } = new();
    public bool Limpio => Solapes == 0 && CrucesAristas == 0 && AristasSobreTarjetas == 0;
    public override string ToString() =>
        $"solapes={Solapes} cruces={CrucesAristas} aristas-sobre-tarjetas={AristasSobreTarjetas}" +
        (Detalles.Count > 0 ? "\n  " + string.Join("\n  ", Detalles.Take(8)) : "");
}

/// <summary>Comprobaciones geométricas del resultado (se usan en los tests).</summary>
public static class Verificacion
{
    private readonly record struct Seg(Pt A, Pt B, string Union);

    public static InformeLayout Comprobar(LayoutResult r, double huecoMin = 4)
    {
        var inf = new InformeLayout();
        var o = r.Opciones;

        // 1. Tarjetas solapadas (o más juntas que huecoMin).
        var cartas = r.TodasLasCartas.ToList();
        for (int i = 0; i < cartas.Count; i++)
            for (int j = i + 1; j < cartas.Count; j++)
            {
                var (a, b) = (cartas[i], cartas[j]);
                if (Math.Abs(a.X - b.X) < o.AnchoCarta + huecoMin && Math.Abs(a.Y - b.Y) < o.AltoCarta + huecoMin)
                { inf.Solapes++; inf.Detalles.Add($"solape {a.Id}/{b.Id}"); }
            }

        // 2. Segmentos de cada conexión (los enlaces de pareja y las bajadas de una misma unión no cuentan entre sí).
        var segs = new List<Seg>();
        foreach (var c in r.Conexiones)
            for (int i = 0; i + 1 < c.Puntos.Count; i++)
                if (Largo(c.Puntos[i], c.Puntos[i + 1]) > 0.01) segs.Add(new Seg(c.Puntos[i], c.Puntos[i + 1], c.UnionId));

        for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
            {
                if (segs[i].Union == segs[j].Union) continue;
                if (Cruzan(segs[i], segs[j]))
                { inf.CrucesAristas++; inf.Detalles.Add($"cruce {segs[i].Union}/{segs[j].Union} en ~({segs[i].A.X:0},{segs[i].A.Y:0})"); }
            }

        // 3. Aristas que atraviesan tarjetas.
        foreach (var s in segs)
            foreach (var c in cartas)
                if (Atraviesa(s, c, o))
                { inf.AristasSobreTarjetas++; inf.Detalles.Add($"arista {s.Union} sobre {c.Id}"); }
        return inf;
    }

    private static double Largo(Pt a, Pt b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static bool Atraviesa(Seg s, CartaPos c, LayoutOptions o)
    {
        const double e = 1.0;
        double x0 = c.X - o.AnchoCarta / 2 + e, x1 = c.X + o.AnchoCarta / 2 - e, y0 = c.Y + e, y1 = c.Y + o.AltoCarta - e;
        double sx0 = Math.Min(s.A.X, s.B.X), sx1 = Math.Max(s.A.X, s.B.X), sy0 = Math.Min(s.A.Y, s.B.Y), sy1 = Math.Max(s.A.Y, s.B.Y);
        return sx0 < x1 && sx1 > x0 && sy0 < y1 && sy1 > y0;
    }

    private static bool Horizontal(Seg s) => Math.Abs(s.A.Y - s.B.Y) < 0.01;

    private static bool Cruzan(Seg a, Seg b)
    {
        const double e = 0.5;
        bool ha = Horizontal(a), hb = Horizontal(b);
        double ax0 = Math.Min(a.A.X, a.B.X), ax1 = Math.Max(a.A.X, a.B.X), ay0 = Math.Min(a.A.Y, a.B.Y), ay1 = Math.Max(a.A.Y, a.B.Y);
        double bx0 = Math.Min(b.A.X, b.B.X), bx1 = Math.Max(b.A.X, b.B.X), by0 = Math.Min(b.A.Y, b.B.Y), by1 = Math.Max(b.A.Y, b.B.Y);
        if (ha && hb) return Math.Abs(a.A.Y - b.A.Y) < e && ax0 < bx1 - e && bx0 < ax1 - e;     // colineales solapadas
        if (!ha && !hb) return Math.Abs(a.A.X - b.A.X) < e && ay0 < by1 - e && by0 < ay1 - e;
        // una horizontal y otra vertical: cruce o contacto (T) entre uniones distintas
        var (h, v) = ha ? (a, b) : (b, a);
        double hx0 = Math.Min(h.A.X, h.B.X), hx1 = Math.Max(h.A.X, h.B.X), hy = h.A.Y;
        double vy0 = Math.Min(v.A.Y, v.B.Y), vy1 = Math.Max(v.A.Y, v.B.Y), vx = v.A.X;
        return vx > hx0 - e && vx < hx1 + e && hy > vy0 - e && hy < vy1 + e;
    }
}
