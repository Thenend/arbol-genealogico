using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Layout;

/// <summary>Genera las aristas ortogonales: enlace de pareja, bajada y bus horizontal hacia los hijos.</summary>
internal static class Enrutador
{
    private sealed class Info
    {
        public Union U = null!;
        public Pt Ancla;
        public double Base;                 // borde inferior de la fila de los padres
        public List<CartaPos> Hijos = new();
        public double A, B;                 // rango horizontal del bus
        public bool TieneBus;
        public int Nivel;
        public List<Pt>? Enlace;
        public bool EnlaceDirecto;
    }

    public static void Rutas(List<Union> uniones, LayoutResult res, HashSet<string> directa)
    {
        var o = res.Opciones;
        var infos = new List<Info>();
        var arcos = new Dictionary<int, int>();

        foreach (var u in uniones)
        {
            var ps = u.Parejas.Where(res.Cartas.ContainsKey).Select(id => res.Cartas[id]).OrderBy(c => c.X).ToList();
            if (ps.Count == 0) continue;
            var info = new Info { U = u, Base = ps[0].Y + o.AltoCarta };
            double midY = ps[0].Y + (o.AlturaEnlace ?? o.AltoCarta / 2);
            if (ps.Count == 2)
            {
                var (p1, p2) = (ps[0], ps[1]);
                bool adyacentes = p1.Cluster == p2.Cluster && Math.Abs(p1.IndiceEnCluster - p2.IndiceEnCluster) == 1;
                info.EnlaceDirecto = directa.Contains(p1.Id) && directa.Contains(p2.Id);
                bool libre = !adyacentes && !res.Cartas.Values.Any(c => c.Id != p1.Id && c.Id != p2.Id &&
                    Math.Abs(c.Y - p1.Y) < 1 && c.X > p1.X && c.X < p2.X);
                if (adyacentes)
                {
                    info.Enlace = new() { new(p1.X + o.AnchoCarta / 2, midY), new(p2.X - o.AnchoCarta / 2, midY) };
                    info.Ancla = new((p1.X + p2.X) / 2, midY);
                }
                else if (libre)
                {
                    // Pareja separada con el espacio entre ellas libre: línea recta, y los hijos cuelgan de ella a la altura de su
                    // conjunto (sin salirse del tramo entre las dos tarjetas).
                    info.Enlace = new() { new(p1.X + o.AnchoCarta / 2, midY), new(p2.X - o.AnchoCarta / 2, midY) };
                    var xs = u.Hijos.Where(res.Cartas.ContainsKey).Select(id => res.Cartas[id].X).ToList();
                    double media = xs.Count > 0 ? (xs.Min() + xs.Max()) / 2 : (p1.X + p2.X) / 2;
                    double lo = p1.X + o.AnchoCarta / 2 + 20, hi = p2.X - o.AnchoCarta / 2 - 20;
                    info.Ancla = new(hi > lo ? Math.Clamp(media, lo, hi) : (p1.X + p2.X) / 2, midY);
                }
                else
                {
                    arcos.TryGetValue(p1.Cluster, out int k); arcos[p1.Cluster] = k + 1;
                    double y = info.Base + 14 + 11 * k;
                    info.Enlace = new() { new(p1.X, info.Base), new(p1.X, y), new(p2.X, y), new(p2.X, info.Base) };
                    info.Ancla = new((p1.X + p2.X) / 2, y);
                }
            }
            else info.Ancla = new(ps[0].X, info.Base);

            info.Hijos = u.Hijos.Where(res.Cartas.ContainsKey).Select(id => res.Cartas[id]).ToList();
            if (info.Hijos.Count > 0)
            {
                var xs = info.Hijos.Select(h => h.X).Append(info.Ancla.X).ToList();
                info.A = xs.Min(); info.B = xs.Max();
                info.TieneBus = info.B - info.A > 0.5;
            }
            infos.Add(info);
        }

        // Niveles de bus por hueco entre filas (escalonados para que los buses solapados no coincidan ni se crucen).
        foreach (var grupo in infos.Where(i => i.Hijos.Count > 0).GroupBy(i => Math.Round(i.Base)))
            AsignarNiveles(grupo.ToList());

        foreach (var i in infos)
        {
            if (i.Enlace != null)
                res.Conexiones.Add(new Conexion { UnionId = i.U.Id, Tipo = TipoConexion.Pareja, Directa = i.EnlaceDirecto, Puntos = i.Enlace });
            if (i.Hijos.Count == 0) continue;

            int niveles = Math.Max(1, infos.Where(x => x.Hijos.Count > 0 && Math.Round(x.Base) == Math.Round(i.Base)).Max(x => x.Nivel) + 1);
            double busY;
            if (niveles == 1) busY = i.Base + o.HuecoFilas * 0.45;
            else
            {
                double paso = Math.Min(20, (o.HuecoFilas - 40) / (niveles - 1));
                busY = i.Base + 20 + i.Nivel * paso;
            }
            busY = Math.Max(busY, i.Ancla.Y + 12);

            foreach (var h in i.Hijos)
            {
                var pts = new List<Pt> { i.Ancla };
                if (Math.Abs(h.X - i.Ancla.X) > 0.5)
                {
                    pts.Add(new(i.Ancla.X, busY));
                    pts.Add(new(h.X, busY));
                }
                pts.Add(new(h.X, h.Y));
                res.Conexiones.Add(new Conexion
                {
                    UnionId = i.U.Id, Tipo = TipoConexion.Descendencia, HijoId = h.Id,
                    Directa = directa.Contains(h.Id), Puntos = pts,
                });
            }
        }
    }

    private static void AsignarNiveles(List<Info> g)
    {
        int n = g.Count;
        var antes = Enumerable.Range(0, n).Select(_ => new List<int>()).ToList(); // antes[j] = los que van por encima de j
        bool Dentro(double x, Info v) => x > v.A + 0.5 && x < v.B - 0.5;

        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var (u, v) = (g[i], g[j]);
                if (!u.TieneBus || !v.TieneBus) continue;
                if (!(u.A < v.B - 0.5 && v.A < u.B - 0.5)) continue;     // no solapan en x
                // La bajada de u atraviesa los niveles superiores a su bus; sus hijos, los inferiores.
                bool uSobreV = Dentro(u.Ancla.X, v) || v.Hijos.Any(h => Dentro(h.X, u));
                bool vSobreU = Dentro(v.Ancla.X, u) || u.Hijos.Any(h => Dentro(h.X, v));
                if (uSobreV && vSobreU)
                {
                    // Conflicto real: se prioriza el que tiene el ancla fuera del otro.
                    (uSobreV, vSobreU) = u.Ancla.X <= v.Ancla.X ? (true, false) : (false, true);
                }
                if (!uSobreV && !vSobreU) { if (u.Ancla.X <= v.Ancla.X) uSobreV = true; else vSobreU = true; }
                if (uSobreV) antes[j].Add(i);
                if (vSobreU) antes[i].Add(j);
            }

        var estado = new int[n]; // 0 sin ver, 1 visitando, 2 hecho
        void Nivel(int j)
        {
            if (estado[j] == 2) return;
            estado[j] = 1;
            int nivel = 0;
            foreach (var i in antes[j])
            {
                if (estado[i] == 1) continue; // ciclo: se ignora la arista
                Nivel(i);
                nivel = Math.Max(nivel, g[i].Nivel + 1);
            }
            g[j].Nivel = nivel;
            estado[j] = 2;
        }
        for (int j = 0; j < n; j++) Nivel(j);
    }
}
