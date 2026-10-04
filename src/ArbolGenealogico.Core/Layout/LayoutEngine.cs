using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Layout;

/// <summary>
/// Colocación del árbol: generaciones en filas, clusters rígidos (persona + parejas),
/// orden por recorrido del árbol de familias y coordenadas por relajación con restricciones.
/// </summary>
public static class LayoutEngine
{
    public static LayoutResult Calcular(Arbol arbol, LayoutOptions? opciones = null)
    {
        var motor = new Motor(arbol, opciones ?? new LayoutOptions());
        return motor.Ejecutar();
    }

    internal sealed class Cluster
    {
        public int Id;
        public List<Persona> Cartas = new();
        public List<Union> Uniones = new();
        public int Gen;
        public double Ancho;
        public double X;
        public int Fila;
        public Dictionary<string, int> Indice = new();
    }

    private sealed class Bloque
    {
        public readonly SortedDictionary<int, List<Cluster>> Filas = new();

        public List<Cluster> Fila(int g)
        {
            if (!Filas.TryGetValue(g, out var l)) Filas[g] = l = new();
            return l;
        }

        public void Anadir(Bloque otro)
        {
            foreach (var (g, l) in otro.Filas) Fila(g).AddRange(l);
        }
    }

    private sealed class Motor
    {
        private readonly Arbol _a;
        private readonly LayoutOptions _o;
        private readonly Dictionary<string, Persona> _personas = new();
        private readonly Dictionary<string, Union> _unionHijo = new();
        private readonly Dictionary<string, List<Union>> _unionesPareja = new();
        private readonly Dictionary<string, int> _gen = new();
        private readonly Dictionary<string, Cluster> _clusterDe = new();
        private readonly List<Cluster> _clusters = new();
        private readonly HashSet<Cluster> _visitados = new();
        private List<Union> _uniones = new();

        public Motor(Arbol a, LayoutOptions o) { _a = a; _o = o; }

        public LayoutResult Ejecutar()
        {
            foreach (var p in _a.Personas) _personas[p.Id] = p;
            foreach (var u in _a.Uniones)
            {
                foreach (var h in u.Hijos) _unionHijo.TryAdd(h, u);
                foreach (var p in u.Parejas)
                {
                    if (!_unionesPareja.TryGetValue(p, out var l)) _unionesPareja[p] = l = new();
                    l.Add(u);
                }
            }

            var raiz = _personas.ContainsKey(_a.RaizId) ? _a.RaizId : _a.Personas.FirstOrDefault()?.Id;
            var res = new LayoutResult { Opciones = _o };
            if (raiz == null) return res;

            Generaciones(raiz);
            _uniones = _a.Uniones.Where(u => u.Parejas.Concat(u.Hijos).Any(_gen.ContainsKey)).ToList();
            ConstruirClusters();
            var orden = Ordenar(_clusterDe[raiz]);
            AsignarCoordenadas(orden);
            return Resultado(orden, res);
        }

        // ---------- 1. Generaciones ----------
        private void Generaciones(string raiz)
        {
            var cola = new Queue<string>();
            _gen[raiz] = 0; cola.Enqueue(raiz);
            while (cola.Count > 0)
            {
                var p = cola.Dequeue(); int g = _gen[p];
                void Poner(string id, int gg) { if (_personas.ContainsKey(id) && _gen.TryAdd(id, gg)) cola.Enqueue(id); }
                if (_unionesPareja.TryGetValue(p, out var us))
                    foreach (var u in us)
                    {
                        foreach (var q in u.Parejas) Poner(q, g);
                        foreach (var h in u.Hijos) Poner(h, g + 1);
                    }
                if (_unionHijo.TryGetValue(p, out var up))
                {
                    foreach (var q in up.Parejas) Poner(q, g - 1);
                    foreach (var h in up.Hijos) Poner(h, g);
                }
            }
        }

        // ---------- 2. Clusters ----------
        private void ConstruirClusters()
        {
            var padre = _gen.Keys.ToDictionary(k => k, k => k);
            string Raiz(string x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; }
            foreach (var u in _uniones)
            {
                var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                for (int i = 1; i < ps.Count; i++) padre[Raiz(ps[i])] = Raiz(ps[0]);
            }
            var grupos = new Dictionary<string, List<Persona>>();
            foreach (var p in _a.Personas.Where(p => _gen.ContainsKey(p.Id)))
            {
                var r = Raiz(p.Id);
                if (!grupos.TryGetValue(r, out var l)) grupos[r] = l = new();
                l.Add(p);
            }
            foreach (var miembros in grupos.Values)
            {
                var c = new Cluster { Id = _clusters.Count };
                c.Cartas = OrdenarCartas(miembros);
                c.Gen = _gen[c.Cartas[0].Id];
                for (int i = 0; i < c.Cartas.Count; i++) { c.Indice[c.Cartas[i].Id] = i; _clusterDe[c.Cartas[i].Id] = c; }
                int n = c.Cartas.Count;
                c.Ancho = n * _o.AnchoCarta + (n - 1) * _o.HuecoPareja;
                c.Uniones = _uniones.Where(u => u.Parejas.Any(c.Indice.ContainsKey))
                    .OrderBy(u => u.Parejas.Where(c.Indice.ContainsKey).Min(p => c.Indice[p]))
                    .ToList();
                _clusters.Add(c);
            }
        }

        private List<Persona> OrdenarCartas(List<Persona> miembros)
        {
            if (miembros.Count == 1) return miembros;
            var ids = miembros.Select(m => m.Id).ToHashSet();
            var ady = miembros.ToDictionary(m => m.Id, _ => new List<string>());
            var uns = _uniones.Where(u => u.Parejas.Count == 2 && ids.Contains(u.Parejas[0])).ToList();
            foreach (var u in uns) { ady[u.Parejas[0]].Add(u.Parejas[1]); ady[u.Parejas[1]].Add(u.Parejas[0]); }

            if (miembros.Count == 2)
            {
                var a = _personas[uns[0].Parejas[0]]; var b = _personas[uns[0].Parejas[1]];
                return a.Sexo == Sexo.Mujer && b.Sexo == Sexo.Hombre ? new() { b, a } : new() { a, b };
            }

            if (miembros.Count <= 7) return MejorOrden(miembros, uns);

            bool camino = ady.Values.All(l => l.Count <= 2) && uns.Count == miembros.Count - 1;
            var res = new List<string>();
            if (camino)
            {
                var ini = miembros.First(m => ady[m.Id].Count == 1).Id;
                string? prev = null, cur = ini;
                while (cur != null)
                {
                    res.Add(cur);
                    var sig = ady[cur].FirstOrDefault(x => x != prev && !res.Contains(x));
                    prev = cur; cur = sig;
                }
            }
            else
            {
                var hub = miembros.OrderByDescending(m => ady[m.Id].Count).First().Id;
                var izq = new List<string>(); var der = new List<string>();
                var vistos = new HashSet<string> { hub };
                var cola = new Queue<string>(); cola.Enqueue(hub);
                bool lado = true;
                while (cola.Count > 0)
                    foreach (var q in ady[cola.Dequeue()])
                        if (vistos.Add(q)) { (lado ? der : izq).Add(q); lado = !lado; cola.Enqueue(q); }
                izq.Reverse();
                res = izq.Concat(new[] { hub }).Concat(der).ToList();
                foreach (var m in miembros) if (!res.Contains(m.Id)) res.Add(m.Id);
            }
            return res.Select(id => _personas[id]).ToList();
        }

        /// <summary>
        /// Orden de un cluster de 3 a 7 cartas por búsqueda exhaustiva: lo importante es que cada pareja quede
        /// adyacente (sin arcos). Las cartas con padres no necesitan ir en los extremos: su bus pasa por encima
        /// de las parejas políticas, que no tienen líneas hacia arriba.
        /// </summary>
        private List<Persona> MejorOrden(List<Persona> miembros, List<Union> uns)
        {
            int n = miembros.Count;
            var exp = miembros.Select(m => _unionHijo.ContainsKey(m.Id)).ToArray();
            var pares = uns.Select(u => (a: miembros.FindIndex(m => m.Id == u.Parejas[0]), b: miembros.FindIndex(m => m.Id == u.Parejas[1]))).ToList();
            var perm = Enumerable.Range(0, n).ToArray();
            var mejor = (int[])perm.Clone();
            double mejorCoste = double.MaxValue;
            var pos = new int[n];

            double Coste()
            {
                for (int i = 0; i < n; i++) pos[perm[i]] = i;
                double c = 0;
                foreach (var (a, b) in pares)
                {
                    int d = Math.Abs(pos[a] - pos[b]);
                    if (d > 1) c += 10 * (d - 1);
                    var (izq, der) = pos[a] < pos[b] ? (a, b) : (b, a);
                    if (miembros[izq].Sexo == Sexo.Mujer && miembros[der].Sexo == Sexo.Hombre) c += 1;
                }
                for (int i = 0; i < n; i++) c += 0.01 * Math.Abs(i - perm[i]);
                return c;
            }

            void Rec(int k)
            {
                if (k == n)
                {
                    double c = Coste();
                    if (c < mejorCoste - 1e-9) { mejorCoste = c; Array.Copy(perm, mejor, n); }
                    return;
                }
                for (int i = k; i < n; i++)
                {
                    (perm[k], perm[i]) = (perm[i], perm[k]);
                    Rec(k + 1);
                    (perm[k], perm[i]) = (perm[i], perm[k]);
                }
            }
            Rec(0);
            return mejor.Select(i => miembros[i]).ToList();
        }

        // ---------- 3. Orden por filas ----------
        private Cluster? ClusterPadres(Persona p)
        {
            if (!_unionHijo.TryGetValue(p.Id, out var u)) return null;
            var q = u.Parejas.FirstOrDefault(_gen.ContainsKey);
            return q == null ? null : _clusterDe[q];
        }

        private List<Persona> Expandibles(Cluster k) =>
            k.Cartas.Where(c => ClusterPadres(c) is { } kp && !_visitados.Contains(kp)).ToList();

        private List<Cluster> Ordenar(Cluster raiz)
        {
            var bloque = Completo(raiz);
            foreach (var c in _clusters.Where(c => !_visitados.Contains(c)).ToList())
                if (!_visitados.Contains(c)) bloque.Anadir(Completo(c));

            var filas = bloque.Filas.ToList();
            int f = 0;
            foreach (var (_, lista) in filas) { foreach (var c in lista) c.Fila = f; f++; }
            _filas = filas.Select(x => x.Value).ToList();
            return _clusters;
        }

        private List<List<Cluster>> _filas = new();

        /// <summary>Cluster + descendientes + familias de origen de sus cartas.</summary>
        private Bloque Completo(Cluster k)
        {
            _visitados.Add(k);
            var b = new Bloque();
            b.Fila(k.Gen).Add(k);
            foreach (var u in k.Uniones)
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var ch = _clusterDe[h];
                    if (!_visitados.Contains(ch)) b.Anadir(Completo(ch));
                }
            return EnvolverArriba(k, b, 0);
        }

        /// <summary>
        /// Despliega la familia de origen de las cartas del cluster. <paramref name="lado"/> indica el lado hacia el que
        /// deben ir los hermanos de toda esta rama: -1 izquierda, +1 derecha, 0 libre (aún no hay rama enfrentada).
        /// </summary>
        private Bloque EnvolverArriba(Cluster k, Bloque b, int lado)
        {
            var exp = Expandibles(k);
            foreach (var c in exp)
            {
                // Con dos familias de origen en el mismo cluster, cada una despliega sus hermanos hacia su lado exterior.
                int l = exp.Count >= 2 ? (ReferenceEquals(c, exp[0]) ? -1 : 1) : lado;
                b = Arriba(k, b, c, l);
            }
            return b;
        }

        /// <summary>
        /// Añade a <paramref name="xb"/> (cluster x ya colocado) los padres de la carta c, sus hermanos
        /// con sus descendientes y, recursivamente, los abuelos.
        /// </summary>
        private Bloque Arriba(Cluster x, Bloque xb, Persona c, int lado)
        {
            var kp = ClusterPadres(c);
            if (kp == null || _visitados.Contains(kp)) return xb;
            _visitados.Add(kp);

            var segs = new List<(Bloque b, bool slot)>();
            bool usado = false;
            foreach (var u in kp.Uniones)
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var ch = _clusterDe[h];
                    if (ReferenceEquals(ch, x)) { if (!usado) { segs.Add((xb, true)); usado = true; } }
                    else if (!_visitados.Contains(ch)) segs.Add((Completo(ch), false));
                }
            if (!usado) segs.Add((xb, true));

            int slot = segs.FindIndex(s => s.slot);
            if (lado != 0)
            {
                // Rama enfrentada a otra: el cluster va pegado al lado de la otra familia y los hermanos hacia fuera.
                var s = segs[slot]; segs.RemoveAt(slot);
                if (lado < 0) { segs.Add(s); slot = segs.Count - 1; } else { segs.Insert(0, s); slot = 0; }
            }

            var antes = segs.Take(slot).ToList();       // hermanos a la izquierda del cluster
            var despues = segs.Skip(slot + 1).ToList(); // hermanos a la derecha
            var desc = Descendientes(x);

            var res = new Bloque();
            var filas = new SortedSet<int> { kp.Gen };
            foreach (var s in segs) foreach (var g in s.b.Filas.Keys) filas.Add(g);
            foreach (var g in filas)
            {
                var l = res.Fila(g);
                if (g >= x.Gen && g != kp.Gen)
                {
                    // Fila del cluster y las de debajo: los hermanos (y sus descendientes) se pegan a la parte del bloque que
                    // pertenece al propio cluster y a sus descendientes, no al borde de todo lo construido hasta ahora. Así el
                    // hermano de un antepasado queda junto a él aunque haya ramas de otras familias a ese lado.
                    var baseFila = xb.Filas.TryGetValue(g, out var bf) ? bf : new List<Cluster>();
                    int ini = baseFila.FindIndex(desc.Contains), fin = baseFila.FindLastIndex(desc.Contains) + 1;
                    if (ini < 0) { ini = 0; fin = baseFila.Count; }
                    l.AddRange(baseFila.Take(ini));
                    foreach (var s in antes) if (s.b.Filas.TryGetValue(g, out var p1)) l.AddRange(p1);
                    l.AddRange(baseFila.Skip(ini).Take(fin - ini));
                    foreach (var s in despues) if (s.b.Filas.TryGetValue(g, out var p2)) l.AddRange(p2);
                    l.AddRange(baseFila.Skip(fin));
                    continue;
                }
                for (int i = 0; i < segs.Count; i++)
                {
                    if (g == kp.Gen && i == slot + 1) l.Add(kp);
                    if (segs[i].b.Filas.TryGetValue(g, out var parte)) l.AddRange(parte);
                }
                if (g == kp.Gen && slot == segs.Count - 1) l.Add(kp);
            }
            return EnvolverArriba(kp, res, lado);
        }

        /// <summary>El cluster y todos los clusters de sus descendientes.</summary>
        private HashSet<Cluster> Descendientes(Cluster x)
        {
            var res = new HashSet<Cluster> { x };
            var pila = new Stack<Cluster>();
            pila.Push(x);
            while (pila.Count > 0)
                foreach (var u in pila.Pop().Uniones)
                    foreach (var h in u.Hijos)
                    {
                        if (!_gen.ContainsKey(h)) continue;
                        var ch = _clusterDe[h];
                        if (res.Add(ch)) pila.Push(ch);
                    }
            return res;
        }

        // ---------- 4. Coordenadas ----------
        private sealed class Termino
        {
            public Cluster Hijo = null!, Padre = null!;
            public double OffHijo, OffPadre, Peso;
        }

        private double Off(Cluster k, string id) =>
            -k.Ancho / 2 + _o.AnchoCarta / 2 + k.Indice[id] * (_o.AnchoCarta + _o.HuecoPareja);

        private bool MismaFamilia(Cluster a, Cluster b)
        {
            foreach (var c in a.Cartas)
            {
                if (!_unionHijo.TryGetValue(c.Id, out var u)) continue;
                foreach (var d in b.Cartas) if (_unionHijo.TryGetValue(d.Id, out var v) && ReferenceEquals(u, v)) return true;
            }
            return false;
        }

        private void AsignarCoordenadas(List<Cluster> _)
        {
            var directa = _a.LineaDirecta();
            var terminos = new List<Termino>();
            foreach (var u in _uniones)
            {
                var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                if (ps.Count == 0) continue;
                var kp = _clusterDe[ps[0]];
                double offU = ps.Average(p => Off(kp, p));
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var kh = _clusterDe[h];
                    terminos.Add(new Termino
                    {
                        Hijo = kh, Padre = kp, OffHijo = Off(kh, h), OffPadre = offU,
                        Peso = directa.Contains(h) ? _o.PesoLineaDirecta : 1,
                    });
                }
            }
            var comoHijo = _clusters.ToDictionary(c => c, _ => new List<Termino>());
            var comoPadre = _clusters.ToDictionary(c => c, _ => new List<Termino>());
            foreach (var t in terminos) { comoHijo[t.Hijo].Add(t); comoPadre[t.Padre].Add(t); }

            // Separaciones mínimas entre contiguos de cada fila.
            var seps = new List<double[]>();
            foreach (var fila in _filas)
            {
                var s = new double[fila.Count];
                for (int i = 1; i < fila.Count; i++)
                    s[i] = s[i - 1] + (fila[i - 1].Ancho + fila[i].Ancho) / 2 +
                           (MismaFamilia(fila[i - 1], fila[i]) ? _o.HuecoHermanos : _o.HuecoFamilias);
                seps.Add(s);
            }
            for (int r = 0; r < _filas.Count; r++)
            {
                double centro = seps[r][^1] / 2;
                for (int i = 0; i < _filas[r].Count; i++) _filas[r][i].X = seps[r][i] - centro;
            }

            double Deseado(Cluster k, out double peso)
            {
                double num = 0, den = 0;
                foreach (var t in comoHijo[k]) { num += t.Peso * (t.Padre.X + t.OffPadre - t.OffHijo); den += t.Peso; }
                // Los padres se colocan sobre sus hijos con prioridad: es el hijo quien "tira" menos de ellos que ellos de él.
                foreach (var t in comoPadre[k]) { double w = t.Peso * _o.FactorPadreSobreHijos; num += w * (t.Hijo.X + t.OffHijo - t.OffPadre); den += w; }
                peso = den > 0 ? den : 1e-3;
                return den > 0 ? num / den : k.X;
            }

            double Barrer(int r)
            {
                var fila = _filas[r]; int n = fila.Count;
                var z = new double[n]; var w = new double[n];
                for (int i = 0; i < n; i++) { z[i] = Deseado(fila[i], out w[i]) - seps[r][i]; }
                Pav(z, w);
                double delta = 0;
                for (int i = 0; i < n; i++)
                {
                    double nx = z[i] + seps[r][i];
                    delta = Math.Max(delta, Math.Abs(nx - fila[i].X));
                    fila[i].X = nx;
                }
                return delta;
            }

            for (int it = 0; it < _o.MaxBarridos; it++)
            {
                double delta = 0;
                for (int r = 0; r < _filas.Count; r++) delta = Math.Max(delta, Barrer(r));
                for (int r = _filas.Count - 1; r >= 0; r--) delta = Math.Max(delta, Barrer(r));
                if (delta < 0.01) break;
            }
        }

        /// <summary>Regresión isotónica ponderada (pool adjacent violators): z queda no decreciente.</summary>
        private static void Pav(double[] z, double[] w)
        {
            int n = z.Length;
            var media = new double[n]; var peso = new double[n]; var cuenta = new int[n];
            int top = -1;
            for (int i = 0; i < n; i++)
            {
                top++; media[top] = z[i]; peso[top] = w[i]; cuenta[top] = 1;
                while (top > 0 && media[top - 1] > media[top])
                {
                    double p = peso[top - 1] + peso[top];
                    media[top - 1] = (media[top - 1] * peso[top - 1] + media[top] * peso[top]) / p;
                    peso[top - 1] = p; cuenta[top - 1] += cuenta[top];
                    top--;
                }
            }
            int k = 0;
            for (int b = 0; b <= top; b++) for (int j = 0; j < cuenta[b]; j++) z[k++] = media[b];
        }

        // ---------- 5. Resultado ----------
        private LayoutResult Resultado(List<Cluster> _, LayoutResult res)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            foreach (var c in _clusters) { minX = Math.Min(minX, c.X - c.Ancho / 2); maxX = Math.Max(maxX, c.X + c.Ancho / 2); }
            double dx = _o.Margen - minX;
            int minFila = _filas.Count == 0 ? 0 : 0;
            double pitch = _o.AltoCarta + _o.HuecoFilas;
            var directa = _a.LineaDirecta();
            var sang = _a.Sanguineos();

            foreach (var c in _clusters)
            {
                foreach (var p in c.Cartas)
                {
                    res.Cartas[p.Id] = new CartaPos
                    {
                        Id = p.Id,
                        X = c.X + dx + Off(c, p.Id),
                        Y = _o.Margen + c.Fila * pitch,
                        Generacion = c.Gen, Cluster = c.Id, IndiceEnCluster = c.Indice[p.Id],
                        Directa = directa.Contains(p.Id), Politica = !sang.Contains(p.Id),
                    };
                }
            }
            foreach (var fila in _filas) res.Filas.Add(fila.Select(c => c.Cartas.Select(p => p.Id).ToList()).ToList());
            res.Ancho = maxX - minX + 2 * _o.Margen;
            res.Alto = Math.Max(0, _filas.Count - 1) * pitch + _o.AltoCarta + 2 * _o.Margen;

            Enrutador.Rutas(_uniones.Where(u => u.Parejas.Concat(u.Hijos).Any(_gen.ContainsKey)).ToList(), res, directa);
            return res;
        }
    }
}
