using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Layout;

/// <summary>
/// Colocación del árbol: generaciones en filas, clusters rígidos (persona + parejas), parejas que bajan por debajo de los
/// sobrinos de sus hermanos (con hilos verticales largos reservados por "fantasmas" en las filas que atraviesan), orden por
/// recorrido del árbol de familias y coordenadas por relajación con restricciones de no cruce.
/// </summary>
public static class LayoutEngine
{
    public static LayoutResult Calcular(Arbol arbol, LayoutOptions? opciones = null)
    {
        opciones ??= new LayoutOptions();
        if (opciones.Ordenacion == Ordenacion.E) return Bowtie.Calcular(arbol, opciones);
        if (opciones.Ordenacion is Ordenacion.B or Ordenacion.C or Ordenacion.D) return OrdenacionB.Calcular(arbol, opciones);
        var motor = new Motor(arbol, opciones);
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
        /// <summary>Hueco reservado en una fila intermedia para el hilo vertical largo hacia un hijo que está más abajo.</summary>
        public bool Fantasma;
        /// <summary>De un fantasma: el cluster del hijo cuyo hilo atraviesa la fila y el desplazamiento de su carta (el fantasma va en su vertical).</summary>
        public Cluster? Columna;
        public double OffColumna;
        /// <summary>Si esta carta era la mitad de una pareja que se ha separado, la otra mitad.</summary>
        public Cluster? ParejaSeparada;
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
        private readonly Dictionary<string, List<Cluster>> _fantasmas = new();   // hijo → fantasmas de arriba abajo

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
            BajarParejas();
            CrearFantasmas();
            var orden = Ordenar(_clusterDe[raiz]);
            SepararParejas();
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

        // ---------- 2b. Filas desplazadas ----------
        /// <summary>
        /// Cuando los dos miembros de una pareja tienen familia de origen, sus ramas se encuentran en la fila de la pareja y
        /// los hijos de los hermanos de cada uno (con los primos de por medio) hacen imposible dibujarlo sin cruces. Igual que
        /// en los árboles impresos, la pareja baja hasta una fila por debajo de todos los descendientes de sus hermanos: el hilo
        /// desde sus padres cae por el lateral de la rama y el espacio que queda debajo es de la pareja y sus hijos. Los
        /// hermanos sin hijos no obligan a bajar.
        /// </summary>
        private void BajarParejas()
        {
            var candidatos = _clusters.Where(k => k.Cartas.Count(c => ClusterPadres(c) != null) >= 2).ToList();
            if (candidatos.Count == 0) return;
            for (int vuelta = 0; vuelta < 40; vuelta++)
            {
                bool cambio = false;
                foreach (var k in candidatos)
                {
                    int necesaria = k.Gen;
                    var propios = Descendientes(k);      // en matrimonios entre parientes la pareja puede descender de su propio hermano
                    foreach (var c in k.Cartas)
                    {
                        if (!_unionHijo.TryGetValue(c.Id, out var u) || ClusterPadres(c) == null) continue;
                        foreach (var h in u.Hijos)
                        {
                            if (!_gen.ContainsKey(h) || h == c.Id) continue;
                            var kh = _clusterDe[h];
                            if (ReferenceEquals(kh, k)) continue;
                            foreach (var d in Descendientes(kh))
                                if (!ReferenceEquals(d, kh) && !propios.Contains(d)) necesaria = Math.Max(necesaria, d.Gen + 1);
                        }
                    }
                    if (necesaria > k.Gen) { k.Gen = necesaria; cambio = true; }
                }
                // Los hijos siempre por debajo de sus padres (los descendientes de una pareja bajada bajan con ella).
                bool sube; int tope = 0;
                do
                {
                    sube = false;
                    foreach (var u in _uniones)
                    {
                        var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                        if (ps.Count == 0) continue;
                        int g = _clusterDe[ps[0]].Gen;
                        foreach (var h in u.Hijos)
                        {
                            if (!_gen.ContainsKey(h)) continue;
                            var kh = _clusterDe[h];
                            if (kh.Gen <= g) { kh.Gen = g + 1; sube = true; cambio = true; }
                        }
                    }
                } while (sube && ++tope < 200);
                if (!cambio) break;
            }
            foreach (var k in _clusters) foreach (var c in k.Cartas) _gen[c.Id] = k.Gen;
        }

        /// <summary>Un fantasma por cada fila que atraviesa el hilo de una unión hacia un hijo que queda más de una fila por debajo.</summary>
        private void CrearFantasmas()
        {
            foreach (var u in _uniones)
            {
                var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                if (ps.Count == 0) continue;
                int gp = _clusterDe[ps[0]].Gen;
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var kh = _clusterDe[h];
                    if (kh.Gen <= gp + 1) continue;
                    var lista = new List<Cluster>();
                    for (int g = gp + 1; g < kh.Gen; g++)
                        lista.Add(new Cluster { Id = -1 - _fantasmas.Count * 1000 - lista.Count, Gen = g, Ancho = FantasmaAncho, Fantasma = true });
                    _fantasmas[h] = lista;
                }
            }
        }

        private const double FantasmaAncho = 8, FantasmaHueco = 22;

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
            var parte = new HashSet<Cluster>();      // lo que va con k "hacia arriba": sus hilos y las familias que se le añadan
            foreach (var c in k.Cartas)
                if (_fantasmas.TryGetValue(c.Id, out var fs))
                    foreach (var f in fs) { b.Fila(f.Gen).Add(f); parte.Add(f); }
            foreach (var u in k.Uniones)
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var ch = _clusterDe[h];
                    if (!_visitados.Contains(ch)) b.Anadir(Completo(ch));
                }
            return EnvolverArriba(k, b, 0, 0, parte, new HashSet<Cluster>());
        }

        /// <summary>
        /// Despliega la familia de origen de las cartas del cluster. <paramref name="lado"/> indica el lado hacia el que
        /// deben ir los hermanos de toda esta rama: -1 izquierda, +1 derecha, 0 libre (aún no hay rama enfrentada).
        /// <paramref name="rama"/> es el lado de la rama entera respecto a la otra con la que se enfrenta (lo que queda al otro
        /// lado es ajeno) y <paramref name="parte"/> lo que ya pertenece a esta rama.
        /// </summary>
        private Bloque EnvolverArriba(Cluster k, Bloque b, int lado, int rama, HashSet<Cluster> parte, HashSet<Cluster> deLaRama)
        {
            var exp = Expandibles(k);
            foreach (var c in exp)
            {
                // Con dos familias de origen en el mismo cluster, cada una despliega sus hermanos hacia su lado exterior.
                int l = exp.Count >= 2 ? (ReferenceEquals(c, exp[0]) ? -1 : 1) : lado;
                int r = rama != 0 ? rama : (exp.Count >= 2 ? l : lado);
                // cada rama de primer nivel lleva su propio conjunto de lo ya colocado
                b = Arriba(k, b, c, l, r, parte, rama != 0 ? deLaRama : new HashSet<Cluster>());
            }
            return b;
        }

        /// <summary>
        /// Añade a <paramref name="xb"/> (cluster x ya colocado) los padres de la carta c, sus hermanos
        /// con sus descendientes y, recursivamente, los abuelos.
        /// </summary>
        private Bloque Arriba(Cluster x, Bloque xb, Persona c, int lado, int rama, HashSet<Cluster> parte, HashSet<Cluster> deLaRama)
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
                if (g > kp.Gen)
                {
                    // Los hermanos (y sus descendientes) se pegan a la parte del bloque que pertenece al propio cluster, a sus
                    // descendientes y a su rama, no al borde de todo lo construido hasta ahora. Así el hermano de un antepasado queda
                    // junto a él aunque haya ramas de otras familias a ese lado.
                    var baseFila = xb.Filas.TryGetValue(g, out var bf) ? bf : new List<Cluster>();
                    Func<Cluster, bool> marca = g >= x.Gen ? e => desc.Contains(e) || parte.Contains(e) : parte.Contains;
                    int ini = baseFila.FindIndex(e => marca(e)), fin = baseFila.FindLastIndex(e => marca(e)) + 1;
                    if (ini < 0) { ini = 0; fin = baseFila.Count; }
                    l.AddRange(baseFila.Take(ini));
                    foreach (var s in antes) if (s.b.Filas.TryGetValue(g, out var p1)) l.AddRange(p1);
                    l.AddRange(baseFila.Skip(ini).Take(fin - ini));
                    foreach (var s in despues) if (s.b.Filas.TryGetValue(g, out var p2)) l.AddRange(p2);
                    l.AddRange(baseFila.Skip(fin));
                    continue;
                }
                for (int i = 0; i < segs.Count; i++)
                    if (segs[i].b.Filas.TryGetValue(g, out var parte2)) l.AddRange(parte2);
            }
            // Los padres y los fantasmas de sus propios hilos: junto a lo propio de la rama, hacia el lado de esta familia.
            Colocar(res.Fila(kp.Gen), new List<Cluster> { kp }, parte, deLaRama, lado, rama);
            for (int g = kp.Gen - 1; ; g--)
            {
                var grupo = new List<Cluster>();
                foreach (var c2 in kp.Cartas)
                    if (_fantasmas.TryGetValue(c2.Id, out var fs)) foreach (var f in fs) if (f.Gen == g) grupo.Add(f);
                if (grupo.Count == 0) break;
                Colocar(res.Fila(g), grupo, parte, deLaRama, lado, rama);
            }
            void Propio(Cluster e) { parte.Add(e); deLaRama.Add(e); }
            Propio(kp);
            foreach (var c2 in kp.Cartas)
                if (_fantasmas.TryGetValue(c2.Id, out var fs2)) foreach (var f in fs2) Propio(f);
            foreach (var sg in segs)
                if (!sg.slot) foreach (var lista in sg.b.Filas.Values) foreach (var e in lista) Propio(e);
            var parteKp = new HashSet<Cluster>();
            foreach (var c2 in kp.Cartas)
                if (_fantasmas.TryGetValue(c2.Id, out var fs3)) foreach (var f in fs3) parteKp.Add(f);
            var resultado = EnvolverArriba(kp, res, lado, rama, parteKp, deLaRama);
            parte.UnionWith(parteKp);       // todo lo que cuelga de esta rama es también de la rama de x
            return resultado;
        }

        /// <summary>
        /// Inserta <paramref name="nuevos"/> en una fila junto a lo propio de la rama (<paramref name="parte"/>): antes de ello si
        /// su familia va a la izquierda, después si va a la derecha. Si no hay nada propio en la fila se usa todo lo ya colocado de
        /// la rama de primer nivel y, si tampoco hay, el extremo contrario al de la rama enfrentada.
        /// </summary>
        private static void Colocar(List<Cluster> fila, List<Cluster> nuevos, HashSet<Cluster> parte, HashSet<Cluster> deLaRama, int lado, int rama)
        {
            int ini = fila.FindIndex(parte.Contains), fin = fila.FindLastIndex(parte.Contains) + 1;
            if (ini < 0) { ini = fila.FindIndex(deLaRama.Contains); fin = fila.FindLastIndex(deLaRama.Contains) + 1; }
            int en = ini >= 0 ? (lado < 0 ? ini : fin) : (rama < 0 ? 0 : fila.Count);
            fila.InsertRange(en, nuevos);
        }

        /// <summary>El cluster y todos los clusters de sus descendientes.</summary>
        private HashSet<Cluster> Descendientes(Cluster x)
        {
            var res = new HashSet<Cluster> { x };
            var pila = new Stack<Cluster>();
            pila.Push(x);
            while (pila.Count > 0)
            {
                var k = pila.Pop();
                // los hilos que entran en k desde una familia ajena a esta descendencia no son suyos
                foreach (var c in k.Cartas)
                    if (_fantasmas.TryGetValue(c.Id, out var fs) && ClusterPadres(c) is { } kpc && res.Contains(kpc))
                        foreach (var f in fs) res.Add(f);
                foreach (var u in k.Uniones)
                    foreach (var h in u.Hijos)
                    {
                        if (!_gen.ContainsKey(h)) continue;
                        var ch = _clusterDe[h];
                        if (res.Add(ch)) pila.Push(ch);
                    }
            }
            return res;
        }

        // ---------- 3b. Parejas separadas ----------
        /// <summary>
        /// El elemento de la carta en una fila entre sus padres y ella: el fantasma de su hilo o, en la fila de los padres, el
        /// cluster de los padres.
        /// </summary>
        private Cluster? ElementoDelHilo(Persona c, int fila)
        {
            if (_fantasmas.TryGetValue(c.Id, out var fs)) { var f = fs.FirstOrDefault(x => x.Fila == fila); if (f != null) return f; }
            var kp = ClusterPadres(c);
            return kp != null && kp.Fila == fila ? kp : null;
        }

        /// <summary>
        /// Una pareja cuyas dos cartas bajan desde familias distintas necesita estar bajo las dos columnas a la vez. Si en alguna
        /// fila hay algo entre esas dos columnas (primos, tíos…), las dos cartas no pueden ir pegadas: se separan
        /// horizontalmente y la línea de la pareja las une por el espacio libre de su fila.
        /// </summary>
        private void SepararParejas()
        {
            int siguienteId = _clusters.Count == 0 ? 0 : _clusters.Max(c => c.Id) + 1;
            foreach (var k in _clusters.ToList())
            {
                if (k.Cartas.Count != 2) continue;
                var (c1, c2) = (k.Cartas[0], k.Cartas[1]);
                var kp1 = ClusterPadres(c1); var kp2 = ClusterPadres(c2);
                if (kp1 == null || kp2 == null) continue;
                int desde = Math.Min(kp1.Fila, kp2.Fila), hasta = k.Fila - 1;
                bool estorba = false;
                for (int g = desde; g <= hasta && !estorba; g++)
                {
                    var e1 = ElementoDelHilo(c1, g); var e2 = ElementoDelHilo(c2, g);
                    if (e1 == null || e2 == null) continue;
                    var fila = _filas[g];
                    if (Math.Abs(fila.IndexOf(e1) - fila.IndexOf(e2)) > 1) estorba = true;
                }
                if (!estorba) continue;

                Cluster Nueva(Persona c) => new Cluster
                {
                    Id = siguienteId++, Gen = k.Gen, Fila = k.Fila, Ancho = _o.AnchoCarta, Cartas = { c },
                    Indice = { [c.Id] = 0 }, Uniones = k.Uniones.Where(u => u.Parejas.Contains(c.Id)).ToList(),
                };
                var n1 = Nueva(c1); var n2 = Nueva(c2);
                n1.ParejaSeparada = n2; n2.ParejaSeparada = n1;
                _clusterDe[c1.Id] = n1; _clusterDe[c2.Id] = n2;
                _clusters.Remove(k); _clusters.Add(n1); _clusters.Add(n2);
                var fk = _filas[k.Fila]; int ik = fk.IndexOf(k);
                fk.RemoveAt(ik); fk.Insert(ik, n1); fk.Insert(ik + 1, n2);
            }
        }

        // ---------- 4. Coordenadas ----------
        private sealed class Termino
        {
            public Cluster Hijo = null!, Padre = null!;
            public double OffHijo, OffPadre, Peso;
            /// <summary>Pareja separada: los hijos se sitúan entre los dos padres, pero los padres no se mueven por ellos.</summary>
            public bool SoloHijo;
        }

        private sealed class Rest
        {
            public Cluster VarR = null!, VarL = null!;
            public double DR, DL, M;
            public bool OrdenFila, Bus;
        }

        private const double MargenBus = 12;

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

        /// <summary>Posición horizontal de un elemento de fila: un fantasma está siempre en la vertical de la carta de su hijo.</summary>
        private static double V(Cluster e) => e.Fantasma ? e.Columna!.X + e.OffColumna : e.X;

        private void AsignarCoordenadas(List<Cluster> _)
        {
            var directa = _a.LineaDirecta();
            var terminos = new List<Termino>();
            foreach (var u in _uniones)
            {
                var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                if (ps.Count == 0) continue;
                var kp = _clusterDe[ps[0]];
                bool separada = ps.Count == 2 && !ReferenceEquals(_clusterDe[ps[0]], _clusterDe[ps[1]]);
                double offU = separada ? 0 : ps.Average(p => Off(kp, p));
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    var kh = _clusterDe[h];
                    double peso = directa.Contains(h) ? _o.PesoLineaDirecta : 1;
                    if (separada)
                        foreach (var p in ps)
                            terminos.Add(new Termino { Hijo = kh, Padre = _clusterDe[p], OffHijo = Off(kh, h), OffPadre = 0, Peso = peso / 2, SoloHijo = true });
                    else
                        terminos.Add(new Termino { Hijo = kh, Padre = kp, OffHijo = Off(kh, h), OffPadre = offU, Peso = peso });
                    if (_fantasmas.TryGetValue(h, out var cadena))
                        foreach (var f in cadena) { f.Columna = kh; f.OffColumna = Off(kh, h); }
                }
            }
            var comoHijo = _clusters.ToDictionary(c => c, _ => new List<Termino>());
            var comoPadre = _clusters.ToDictionary(c => c, _ => new List<Termino>());
            foreach (var t in terminos) { comoHijo[t.Hijo].Add(t); if (!t.SoloHijo) comoPadre[t.Padre].Add(t); }

            // Separaciones mínimas entre contiguos de cada fila (acumuladas).
            var seps = new List<double[]>();
            var pos = new Dictionary<Cluster, int>();
            foreach (var fila in _filas)
            {
                var sp = new double[fila.Count];
                for (int i = 1; i < fila.Count; i++)
                    sp[i] = sp[i - 1] + (fila[i - 1].Ancho + fila[i].Ancho) / 2 +
                            (fila[i - 1].Fantasma || fila[i].Fantasma ? FantasmaHueco
                             : ReferenceEquals(fila[i - 1].ParejaSeparada, fila[i]) ? _o.HuecoPareja
                             : MismaFamilia(fila[i - 1], fila[i]) ? _o.HuecoHermanos : _o.HuecoFamilias);
                seps.Add(sp);
                for (int i = 0; i < fila.Count; i++) pos[fila[i]] = i;
            }

            // Restricciones "derecha ≥ izquierda + margen" entre variables (cada fantasma es una posición de la columna de su carta).
            var restr = new List<Rest>();
            (Cluster v, double d) Var(Cluster e, double off) => e.Fantasma ? (e.Columna!, e.OffColumna + off) : (e, off);
            void Anadir(Cluster der, double offDer, Cluster izq, double offIzq, double margen, bool ordenFila, bool bus = false)
            {
                var (vr, dr) = Var(der, offDer); var (vl, dl) = Var(izq, offIzq);
                if (ReferenceEquals(vr, vl)) return;
                restr.Add(new Rest { VarR = vr, DR = dr, VarL = vl, DL = dl, M = margen, OrdenFila = ordenFila, Bus = bus });
            }
            for (int r = 0; r < _filas.Count; r++)
                for (int i = 1; i < _filas[r].Count; i++)
                    Anadir(_filas[r][i], 0, _filas[r][i - 1], 0, seps[r][i] - seps[r][i - 1],
                        !_filas[r][i].Fantasma && !_filas[r][i - 1].Fantasma);

            // Un hilo (vertical) en el hueco entre dos filas: cartas con padres e hilos de fantasma.
            bool TieneHilo(Cluster e) => e.Fantasma || e.Cartas.Any(c => ClusterPadres(c) != null);
            foreach (var u in _uniones)
            {
                var ps = u.Parejas.Where(_gen.ContainsKey).ToList();
                if (ps.Count == 0) continue;
                var kp = _clusterDe[ps[0]];
                bool separada = ps.Count == 2 && !ReferenceEquals(_clusterDe[ps[0]], _clusterDe[ps[1]]);
                double offU = separada ? 0 : ps.Average(p => Off(kp, p));
                int fila = pos.ContainsKey(kp) ? kp.Fila : -1;
                var hijos = new List<Cluster>();
                foreach (var h in u.Hijos)
                {
                    if (!_gen.ContainsKey(h)) continue;
                    hijos.Add(_fantasmas.TryGetValue(h, out var ch) ? ch[0] : _clusterDe[h]);
                }
                if (hijos.Count == 0 || fila < 0) continue;
                var lf = _filas[fila + 1];
                int i0 = hijos.Min(e => lf.IndexOf(e)), i1 = hijos.Max(e => lf.IndexOf(e));
                if (i0 < 0) continue;
                // sin elementos ajenos dentro del tramo (si los hubiera, el orden ya no es plano y no se fuerza nada)
                bool contiguo = true;
                for (int i = i0; i <= i1; i++) if (!hijos.Contains(lf[i])) { contiguo = false; break; }
                if (!contiguo) continue;
                if (separada)
                {
                    // los hijos quedan entre los dos padres, y el enlace baja desde dentro de su tramo
                    var (pa, pb) = pos[_clusterDe[ps[0]]] < pos[_clusterDe[ps[1]]] ? (_clusterDe[ps[0]], _clusterDe[ps[1]]) : (_clusterDe[ps[1]], _clusterDe[ps[0]]);
                    Anadir(lf[i0], 0, pa, 0, MargenBus, false, true);
                    Anadir(pb, 0, lf[i1], 0, MargenBus, false, true);
                    continue;
                }
                int izq = i0 - 1; while (izq >= 0 && !TieneHilo(lf[izq])) izq--;
                int der = i1 + 1; while (der < lf.Count && !TieneHilo(lf[der])) der++;
                if (izq >= 0) Anadir(kp, offU, lf[izq], 0, MargenBus, false, true);
                if (der < lf.Count) Anadir(lf[der], 0, kp, offU, MargenBus, false, true);
            }

            // Colocación inicial factible: todo lo a la izquierda que permiten las restricciones. Si el orden de las filas no es
            // plano, las restricciones de los buses pueden ser contradictorias (el valor crecería sin fin): entonces se prescinde de ellas.
            bool Compactar()
            {
                for (int r = 0; r < _filas.Count; r++)
                    foreach (var e in _filas[r]) if (!e.Fantasma) e.X = seps[r][pos[e]];
                for (int vuelta = 0; vuelta < 400 + restr.Count; vuelta++)
                {
                    bool cambio = false;
                    foreach (var rs in restr)
                    {
                        double falta = rs.VarL.X + rs.DL + rs.M - (rs.VarR.X + rs.DR);
                        if (falta > 1e-6) { rs.VarR.X += falta; cambio = true; }
                    }
                    if (!cambio) return true;
                }
                return false;
            }
            if (!Compactar())
            {
                restr.RemoveAll(rs => rs.Bus);
                // Último recurso: sin hilos tampoco; solo el orden de cada fila, que siempre se puede cumplir.
                if (!Compactar()) { restr.RemoveAll(rs => !rs.OrdenFila); Compactar(); }
            }
            var porVar = _clusters.ToDictionary(c => c, _ => new List<Rest>());
            foreach (var rs in restr)
            {
                if (rs.OrdenFila) continue;     // entre dos cartas de una fila lo resuelve la regresión isotónica
                porVar[rs.VarR].Add(rs); porVar[rs.VarL].Add(rs);
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
                var fila = _filas[r];
                var idx = new List<int>();
                for (int i = 0; i < fila.Count; i++) if (!fila[i].Fantasma) idx.Add(i);
                int n = idx.Count;
                if (n == 0) return 0;
                var z = new double[n]; var w = new double[n];
                var lb = new double[n]; var ub = new double[n];          // cotas sobre z = x − separación acumulada
                for (int j = 0; j < n; j++)
                {
                    int i = idx[j]; var k = fila[i];
                    z[j] = Deseado(k, out w[j]) - seps[r][i];
                    double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
                    foreach (var rs in porVar[k])
                    {
                        if (ReferenceEquals(rs.VarR, k)) lo = Math.Max(lo, rs.VarL.X + rs.DL + rs.M - rs.DR);
                        else hi = Math.Min(hi, rs.VarR.X + rs.DR - rs.M - rs.DL);
                    }
                    lb[j] = lo - seps[r][i]; ub[j] = hi - seps[r][i];
                }
                Pav(z, w);
                double mx = double.NegativeInfinity, mn = double.PositiveInfinity;
                var lbm = new double[n]; var ubm = new double[n];
                for (int j = 0; j < n; j++) { mx = Math.Max(mx, lb[j]); lbm[j] = mx; }
                for (int j = n - 1; j >= 0; j--) { mn = Math.Min(mn, ub[j]); ubm[j] = mn; }
                double delta = 0;
                for (int j = 0; j < n; j++)
                {
                    double zz = Math.Min(Math.Max(z[j], lbm[j]), Math.Max(ubm[j], lbm[j]));
                    double nx = zz + seps[r][idx[j]];
                    delta = Math.Max(delta, Math.Abs(nx - fila[idx[j]].X));
                    fila[idx[j]].X = nx;
                }
                return delta;
            }

            void Relajar()
            {
                for (int it = 0; it < _o.MaxBarridos; it++)
                {
                    double delta = 0;
                    for (int r = 0; r < _filas.Count; r++) delta = Math.Max(delta, Barrer(r));
                    for (int r = _filas.Count - 1; r >= 0; r--) delta = Math.Max(delta, Barrer(r));
                    if (delta < 0.01) break;
                }
            }

            // Una familia entera (una persona con toda su descendencia) desplazada en bloque hacia donde la llaman sus padres,
            // hasta donde dejen sus vecinas. Los barridos fila a fila no lo consiguen: cada fila sola no se mueve porque la
            // retienen las de arriba y abajo, y quedaban huecos vacíos entre ramas.
            var restrDe = _clusters.ToDictionary(c => c, _ => new List<Rest>());
            foreach (var rs in restr) { restrDe[rs.VarR].Add(rs); restrDe[rs.VarL].Add(rs); }
            bool MoverBloque(Cluster x)
            {
                var bloque = Descendientes(x);
                bloque.RemoveWhere(e => e.Fantasma);
                double num = 0, den = 0, lo = double.NegativeInfinity, hi = double.PositiveInfinity;
                foreach (var k in bloque)
                {
                    foreach (var t in comoHijo[k])
                        if (!bloque.Contains(t.Padre)) { num += t.Peso * (t.Padre.X + t.OffPadre - t.OffHijo - k.X); den += t.Peso; }
                    foreach (var t in comoPadre[k])
                        if (!bloque.Contains(t.Hijo))
                        {
                            double w = t.Peso * _o.FactorPadreSobreHijos;
                            num += w * (t.Hijo.X + t.OffHijo - t.OffPadre - k.X); den += w;
                        }
                    foreach (var rs in restrDe[k])
                    {
                        bool r = bloque.Contains(rs.VarR), l = bloque.Contains(rs.VarL);
                        if (r && !l) lo = Math.Max(lo, rs.VarL.X + rs.DL + rs.M - (rs.VarR.X + rs.DR));
                        else if (l && !r) hi = Math.Min(hi, rs.VarR.X + rs.DR - rs.M - (rs.VarL.X + rs.DL));
                    }
                }
                if (den <= 0 || lo > hi + 1e-6) return false;
                double s = Math.Clamp(num / den, Math.Min(lo, 0), Math.Max(hi, 0));
                if (Math.Abs(s) < 0.5) return false;
                foreach (var k in bloque) k.X += s;
                return true;
            }

            Relajar();
            for (int ronda = 0; ronda < 12; ronda++)
            {
                bool movido = false;
                foreach (var k in _clusters) if (!k.Fantasma && MoverBloque(k)) movido = true;
                if (!movido) break;
                Relajar();
            }
            foreach (var l in _fantasmas.Values) foreach (var f in l) f.X = V(f);
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
