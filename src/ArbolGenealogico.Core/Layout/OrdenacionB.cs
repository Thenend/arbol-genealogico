using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Layout;

/// <summary>
/// Ordenación B, al estilo de Family TreePhoto. El árbol se construye desde la persona principal hacia arriba:
/// cada antepasado es el pie de un bloque formado por sus padres, sus hermanos (con toda su descendencia) y él mismo,
/// que baja en línea recta desde el punto de unión de sus padres hasta quedar por debajo del más profundo de sus sobrinos.
/// En cada pareja de antepasados la mujer va a la izquierda y el hombre a la derecha, cada uno al pie de su propio bloque,
/// y entre los dos queda el hueco de una tarjeta del que cuelgan sus hijos. Los bloques y las familias se empaquetan por
/// contornos (como un árbol ordenado clásico), así que cada rama ocupa solo lo que necesita y las generaciones de ramas
/// distintas no tienen por qué coincidir en altura.
/// La ordenación C es la misma con dos cambios: los hermanos de cada antepasado se reparten a los dos lados de su línea,
/// equilibrando el ancho, y en cada pareja de antepasados la rama con más familia va por fuera del árbol (así los cónyuges
/// de la línea directa quedan más cerca).
/// </summary>
internal static class OrdenacionB
{
    public static LayoutResult Calcular(Arbol arbol, LayoutOptions opciones)
    {
        if (opciones.Ordenacion != Ordenacion.D) return new Motor(arbol, opciones).Ejecutar();
        // Escalonado: cuanto más se escalona, más estrecho y más alto queda el árbol. Se prueban varios grados y se elige el
        // que deja el árbol más grande al imprimirlo en una hoja A4 (apaisada o vertical); en caso de duplicado, el menos escalonado.
        LayoutResult? mejor = null;
        double mejorEscala = 0;
        foreach (var lambda in new[] { 1e6, 8, 4, 2, 1, 0.5, 0.25, 0.1, 0 })
        {
            var r = new Motor(arbol, opciones, lambda).Ejecutar();
            if (r.Ancho <= 0 || r.Alto <= 0) return r;
            double escala = Math.Max(Math.Min(297 / r.Ancho, 210 / r.Alto), Math.Min(210 / r.Ancho, 297 / r.Alto));
            if (mejor == null || escala > mejorEscala * 1.01) { mejor = r; mejorEscala = escala; }
        }
        return mejor!;
    }

    private sealed class CartaB
    {
        public string Id = "";
        public double X;
        public int Fila;
    }

    /// <summary>Trozo de dibujo con coordenadas propias: tarjetas, líneas y contorno por filas.</summary>
    private sealed class Forma
    {
        public readonly List<CartaB> Cartas = new();
        public readonly List<Conexion> Conexiones = new();
        public readonly SortedDictionary<int, (double A, double B)> Contorno = new();
        /// <summary>Posición horizontal de referencia (la persona de la que cuelga la forma o el punto de unión).</summary>
        public double Ancla;

        public int FilaMin => Contorno.Count == 0 ? 0 : Contorno.Keys.First();
        public int FilaMax => Contorno.Count == 0 ? 0 : Contorno.Keys.Last();
        public double MinX => Contorno.Count == 0 ? Ancla : Contorno.Values.Min(c => c.A);
        public double MaxX => Contorno.Count == 0 ? Ancla : Contorno.Values.Max(c => c.B);

        public void Ocupar(int fila, double a, double b) =>
            Contorno[fila] = Contorno.TryGetValue(fila, out var c) ? (Math.Min(c.A, a), Math.Max(c.B, b)) : (a, b);

        public void Mover(double dx, int dfila, double paso)
        {
            if (dx == 0 && dfila == 0) return;
            double dy = dfila * paso;
            foreach (var c in Cartas) { c.X += dx; c.Fila += dfila; }
            foreach (var cx in Conexiones)
            {
                for (int i = 0; i < cx.Puntos.Count; i++) cx.Puntos[i] = new Pt(cx.Puntos[i].X + dx, cx.Puntos[i].Y + dy);
                if (cx.Nudo is { } n) cx.Nudo = new Pt(n.X + dx, n.Y + dy);
            }
            var viejo = Contorno.ToList();
            Contorno.Clear();
            foreach (var (f, (a, b)) in viejo) Contorno[f + dfila] = (a + dx, b + dx);
            Ancla += dx;
        }

        public void Absorber(Forma otra)
        {
            Cartas.AddRange(otra.Cartas);
            Conexiones.AddRange(otra.Conexiones);
            foreach (var (f, (a, b)) in otra.Contorno) Ocupar(f, a, b);
        }

        /// <summary>Desplazamiento horizontal que deja <paramref name="der"/> a la derecha de esta forma, a <paramref name="hueco"/> de ella en todas las filas.</summary>
        public double Separacion(Forma der, double hueco)
        {
            double d = double.NegativeInfinity;
            foreach (var (f, (_, b)) in Contorno)
                if (der.Contorno.TryGetValue(f, out var c)) d = Math.Max(d, b + hueco - c.A);
            return double.IsNegativeInfinity(d) ? MaxX + hueco - der.MinX : d;
        }
    }

    private sealed class Motor
    {
        private readonly Arbol _a;
        private readonly LayoutOptions _o;
        private readonly double _w, _h, _paso;
        private readonly bool _equilibrar;         // ordenaciones C y D
        private readonly bool _escalonar;          // ordenación D
        private readonly double _lambda;           // D: lo que cuesta cada píxel de alto frente a uno de ancho
        private const double Hueco = 30;           // entre familias y hermanos
        private readonly Dictionary<string, Persona> _personas = new();
        private readonly Dictionary<string, Union> _unionHijo = new();
        private readonly Dictionary<string, List<Union>> _unionesPareja = new();
        private HashSet<string> _alcanzables = new(), _directa = new(), _sangre = new();
        private readonly HashSet<string> _colocados = new();
        private readonly HashSet<string> _enlacesHechos = new();
        private readonly HashSet<(string, string)> _hijosHechos = new();

        public Motor(Arbol a, LayoutOptions o, double lambda = 0)
        {
            _a = a; _o = o;
            _w = o.AnchoCarta; _h = o.AltoCarta; _paso = o.AltoCarta + o.HuecoFilas;
            _equilibrar = o.Ordenacion is Ordenacion.C or Ordenacion.D;
            _escalonar = o.Ordenacion == Ordenacion.D;
            _lambda = lambda;
        }

        public LayoutResult Ejecutar()
        {
            var res = new LayoutResult { Opciones = _o };
            foreach (var p in _a.Personas) _personas[p.Id] = p;
            var raiz = _personas.ContainsKey(_a.RaizId) ? _a.RaizId : _a.Personas.FirstOrDefault()?.Id;
            if (raiz == null) return res;
            _alcanzables = _a.Alcanzables();
            if (!_alcanzables.Contains(raiz)) _alcanzables.Add(raiz);
            foreach (var u in _a.Uniones)
            {
                foreach (var h in u.Hijos) _unionHijo.TryAdd(h, u);
                foreach (var p in u.Parejas)
                {
                    if (!_unionesPareja.TryGetValue(p, out var l)) _unionesPareja[p] = l = new();
                    l.Add(u);
                }
            }
            _directa = _a.LineaDirecta();
            _sangre = _a.Sanguineos();

            var total = TienePadres(raiz) ? Bloque(raiz, true) : Descendencia(raiz);
            ColocarSobrantes(total);
            ConexionesPendientes(total);
            return Resultado(total, res);
        }

        // ---------- consultas ----------
        private bool Sirve(string id) => _alcanzables.Contains(id) && _personas.ContainsKey(id) && !_colocados.Contains(id);

        private bool TienePadres(string id) =>
            _unionHijo.TryGetValue(id, out var u) && u.Parejas.Any(Sirve);

        private IEnumerable<Union> UnionesDe(string id) =>
            _unionesPareja.TryGetValue(id, out var l) ? l : Enumerable.Empty<Union>();

        private bool EsMujer(string id) => _personas[id].Sexo == Sexo.Mujer;

        private static double XDe(Forma f, string id) => f.Cartas.First(c => c.Id == id).X;
        private static int FilaDe(Forma f, string id) => f.Cartas.First(c => c.Id == id).Fila;

        private Forma Tarjeta(string id, int fila = 0)
        {
            _colocados.Add(id);
            var f = new Forma();
            f.Cartas.Add(new CartaB { Id = id, X = 0, Fila = fila });
            f.Ocupar(fila, -_w / 2, _w / 2);
            return f;
        }

        private Conexion Enlace(Union u, double x1, double x2, int fila, Pt? nudo = null)
        {
            _enlacesHechos.Add(u.Id);
            double y = fila * _paso + _h / 2;
            var ps = u.Parejas.Where(_alcanzables.Contains).ToList();
            return new Conexion
            {
                UnionId = u.Id, Tipo = TipoConexion.Pareja, Directa = ps.Count == 2 && ps.All(_directa.Contains),
                Puntos = new() { new(x1, y), new(x2, y) }, Nudo = nudo,
            };
        }

        private Conexion HaciaHijo(Union u, string hijo, Pt inicio, double yBus, double xHijo, double yHijo)
        {
            _hijosHechos.Add((u.Id, hijo));
            var pts = new List<Pt> { inicio };
            if (Math.Abs(xHijo - inicio.X) > 0.5) { pts.Add(new(inicio.X, yBus)); pts.Add(new(xHijo, yBus)); }
            pts.Add(new(xHijo, yHijo));
            return new Conexion { UnionId = u.Id, Tipo = TipoConexion.Descendencia, HijoId = hijo, Directa = _directa.Contains(hijo), Puntos = pts };
        }

        /// <summary>Coloca formas de izquierda a derecha, cada una lo más cerca posible de las anteriores.</summary>
        private Forma Empaquetar(List<Forma> formas)
        {
            var acc = new Forma();
            bool primera = true;
            foreach (var f in formas)
            {
                if (!primera) f.Mover(acc.Separacion(f, Hueco), 0, _paso);
                acc.Absorber(f);
                primera = false;
            }
            return acc;
        }

        /// <summary>Copia solo del contorno de una forma, para probar colocaciones sin moverla.</summary>
        private static Forma Contorno(Forma f, int dfila = 0)
        {
            var c = new Forma { Ancla = f.Ancla };
            foreach (var (r, (a, b)) in f.Contorno) c.Contorno[r + dfila] = (a, b);
            return c;
        }

        private double Coste(double ancho, int filas) => ancho + _lambda * filas * _paso;
        private static double Ancho(Forma f) => f.MaxX - f.MinX;

        /// <summary>
        /// Ordenación D (escalonado): formas que cuelgan de una misma línea (su tarjeta de arriba en la fila 1 y el
        /// <see cref="Forma.Ancla"/> en ella), de izquierda a derecha. La de fuera queda arriba y cada una de las siguientes,
        /// hacia dentro, puede bajar unas filas (con el hueco de su línea reservado encima) para meter su descendencia por
        /// debajo de la anterior. Se elige para cada una la bajada que menos cuesta en ancho más alto (según
        /// <see cref="_lambda"/>), sin subir nunca respecto a la anterior. <paramref name="dir"/>: +1 si la de fuera es la
        /// primera (se empaqueta hacia la derecha), -1 si es la última (hacia la izquierda).
        /// </summary>
        private Forma Escalonar(List<Forma> formas, int dir)
        {
            var orden = dir > 0 ? formas : Enumerable.Reverse(formas).ToList();
            double CosteDe(Forma f) => Coste(f.MaxX - f.MinX, f.FilaMax - f.FilaMin + 1);

            // Contorno de acc con f añadida bajando d filas (y la x a la que va f).
            (Forma Acc, double S) Probar(Forma acc, Forma f, int d)
            {
                var c = Contorno(f, d);
                for (int r = f.FilaMin; r < f.FilaMin + d; r++) c.Ocupar(r, f.Ancla - _w / 2, f.Ancla + _w / 2);
                double s = dir > 0 ? acc.Separacion(c, Hueco) : -c.Separacion(acc, Hueco);
                var nuevo = Contorno(acc);
                c.Mover(s, 0, _paso);
                nuevo.Absorber(c);
                return (nuevo, s);
            }
            IEnumerable<int> Bajadas(Forma acc, int previa) => Enumerable.Range(previa, Math.Max(0, acc.FilaMax - previa) + 1);
            // Lo que queda, cada una con la bajada que menos cuesta en ese momento.
            Forma Voraz(Forma acc, int desde, int previa)
            {
                for (int j = desde; j < orden.Count; j++)
                {
                    var (mejor, mejorD) = (default(Forma)!, previa);
                    foreach (int d in Bajadas(acc, previa))
                    {
                        var (a, _) = Probar(acc, orden[j], d);
                        if (mejor == null || CosteDe(a) < CosteDe(mejor) - 0.5) { mejor = a; mejorD = d; }
                    }
                    acc = mejor; previa = mejorD;
                }
                return acc;
            }

            var actual = Contorno(orden[0]);
            var total = new Forma();
            total.Absorber(orden[0]);
            int bajadaPrevia = 0;
            for (int i = 1; i < orden.Count; i++)
            {
                var f = orden[i];
                // Cada bajada se juzga por cómo queda la escalera entera al terminarla, no solo por este escalón.
                int mejorD = bajadaPrevia; double mejorS = 0, mejorCoste = double.PositiveInfinity;
                Forma? mejorAcc = null;
                foreach (int d in Bajadas(actual, bajadaPrevia))
                {
                    var (a, s) = Probar(actual, f, d);
                    double coste = CosteDe(Voraz(a, i + 1, d));
                    if (coste < mejorCoste - 0.5) { mejorCoste = coste; mejorD = d; mejorS = s; mejorAcc = a; }
                }
                int arriba = f.FilaMin;
                f.Mover(0, mejorD, _paso);
                for (int r = arriba; r < arriba + mejorD; r++) f.Ocupar(r, f.Ancla - _w / 2, f.Ancla + _w / 2);
                f.Mover(mejorS, 0, _paso);
                total.Absorber(f);
                actual = mejorAcc!;
                bajadaPrevia = mejorD;
            }
            return total;
        }

        /// <summary>
        /// Ordenación D: sube todo el bloque de un antepasado (sus padres, hermanos...) <paramref name="k"/> filas por encima de
        /// él, alargando la línea que le baja de sus padres. Lo de su fila (él y sus otras parejas) no se mueve.
        /// </summary>
        private void Elevar(Forma f, string x, int k)
        {
            if (k <= 0) return;
            double dy = -k * _paso;
            foreach (var c in f.Cartas) if (c.Fila < 0) c.Fila -= k;
            foreach (var cx in f.Conexiones)
            {
                if (cx.Tipo == TipoConexion.Descendencia && cx.HijoId == x)
                {
                    for (int i = 0; i < cx.Puntos.Count - 1; i++) cx.Puntos[i] = new Pt(cx.Puntos[i].X, cx.Puntos[i].Y + dy);
                    // el último tramo baja recto hasta él
                    var ult = cx.Puntos[^1];
                    if (Math.Abs(cx.Puntos[^2].X - ult.X) > 0.5) cx.Puntos.Insert(cx.Puntos.Count - 1, new Pt(ult.X, cx.Puntos[^2].Y));
                    continue;
                }
                if (cx.Puntos.All(p => p.Y < 0))
                {
                    for (int i = 0; i < cx.Puntos.Count; i++) cx.Puntos[i] = new Pt(cx.Puntos[i].X, cx.Puntos[i].Y + dy);
                    if (cx.Nudo is { } n) cx.Nudo = new Pt(n.X, n.Y + dy);
                }
            }
            var viejo = f.Contorno.ToList();
            f.Contorno.Clear();
            foreach (var (r, ab) in viejo) f.Contorno[r < 0 ? r - k : r] = ab;
            for (int r = -k; r < 0; r++) f.Ocupar(r, f.Ancla - _w / 2, f.Ancla + _w / 2);
        }

        private Forma ContornoElevado(Forma f, int k)
        {
            var c = new Forma { Ancla = f.Ancla };
            foreach (var (r, ab) in f.Contorno) c.Contorno[r < 0 ? r - k : r] = ab;
            for (int r = -k; r < 0; r++) c.Ocupar(r, f.Ancla - _w / 2, f.Ancla + _w / 2);
            return c;
        }

        /// <summary>Escalonado: la descendencia de cada una (con su tarjeta de arriba en la fila 1), abierta hacia <paramref name="alinear"/>.</summary>
        private List<(string Id, Forma F)> Construir(List<string> ids, int alinear)
        {
            var res = new List<(string, Forma)>();
            foreach (var id in ids)
            {
                if (!Sirve(id)) continue;
                var f = Descendencia(id, alinear);
                f.Mover(0, 1, _paso);
                res.Add((id, f));
            }
            return res;
        }

        /// <summary>Cuántas tarjetas tendrá la descendencia de una persona (ella, sus parejas y sus descendientes): su tamaño aproximado.</summary>
        private int Tamano(string id)
        {
            var vistos = new HashSet<string>();
            var pila = new Stack<string>();
            pila.Push(id);
            while (pila.Count > 0)
            {
                var x = pila.Pop();
                if (!Sirve(x) || !vistos.Add(x)) continue;
                foreach (var u in UnionesDe(x))
                {
                    foreach (var q in u.Parejas) if (q != x && Sirve(q)) vistos.Add(q);
                    foreach (var h in u.Hijos) pila.Push(h);
                }
            }
            return vistos.Count;
        }

        // ---------- descendencia (árbol ordenado clásico) ----------
        /// <summary>
        /// La persona, sus parejas y toda su descendencia, con la persona en la fila 0 y <see cref="Forma.Ancla"/> en su x.
        /// <paramref name="alinear"/> (solo en el escalonado): 0 = los hijos centrados bajo los padres; +1 = los padres sobre el
        /// hijo de más a la derecha y la familia abierta hacia la izquierda; -1 = al revés.
        /// </summary>
        private Forma Descendencia(string p, int alinear = 0)
        {
            _colocados.Add(p);
            var f = new Forma();
            // Fila de la pareja: la persona y sus parejas (la primera según la regla de la mujer a la izquierda, la segunda al
            // otro lado y las demás alternando) y, por fuera de cada pareja, las otras parejas que tenga esta.
            var fila = new List<string> { p };
            var uniones = new List<(Union U, string A, string? B)>();
            var cola = new Queue<string>(); cola.Enqueue(p);
            int ladoPrimera = 1;
            while (cola.Count > 0)
            {
                var q = cola.Dequeue();
                int nUniones = 0;
                // las uniones con hijos, primero: así sus parejas quedan pegadas y sin arcos que crucen sus líneas
                foreach (var u in UnionesDe(q).OrderBy(x => x.Hijos.Any(Sirve) ? 0 : 1))
                {
                    if (uniones.Any(x => ReferenceEquals(x.U, u))) continue;
                    string? r = u.Parejas.FirstOrDefault(x => x != q && Sirve(x) && !fila.Contains(x));
                    if (r == null)
                    {
                        // sin pareja (o ya en la fila): la unión cuelga de q, o del enlace si la pareja ya está
                        string? otra = u.Parejas.FirstOrDefault(x => x != q && fila.Contains(x));
                        uniones.Add((u, q, otra));
                        continue;
                    }
                    _colocados.Add(r);
                    int iq = fila.IndexOf(q), ip = fila.IndexOf(p);
                    int lado;
                    if (q == p)
                    {
                        if (nUniones == 0) ladoPrimera = EsMujer(p) ? 1 : EsMujer(r) ? -1 : 1;
                        lado = nUniones % 2 == 0 ? ladoPrimera : -ladoPrimera;
                        nUniones++;
                    }
                    else lado = iq < ip ? -1 : 1;
                    if (lado < 0)
                    {
                        fila.Insert(q == p ? 0 : iq, r);
                    }
                    else fila.Insert(q == p ? fila.Count : iq + 1, r);
                    uniones.Add((u, q, r));
                    cola.Enqueue(r);
                }
            }
            double paso = _w + _o.HuecoPareja;
            int ipp = fila.IndexOf(p);
            var xDe = new Dictionary<string, double>();
            for (int i = 0; i < fila.Count; i++) { xDe[fila[i]] = (i - ipp) * paso; f.Cartas.Add(new CartaB { Id = fila[i], X = xDe[fila[i]], Fila = 0 }); }
            f.Ocupar(0, xDe[fila[0]] - _w / 2, xDe[fila[^1]] + _w / 2);

            // Enlaces: recto entre parejas contiguas; en arco entre las que no lo están (por encima de las tarjetas si la unión
            // no tiene hijos, por debajo si los tiene, para que cuelguen de él).
            var inicioDe = new Dictionary<Union, Pt>();
            int arcos = 0, arcosArriba = 0;
            foreach (var (u, qa, qb) in uniones)
            {
                if (qb == null) { inicioDe[u] = new Pt(xDe[qa], _h); continue; }
                double x1 = Math.Min(xDe[qa], xDe[qb]), x2 = Math.Max(xDe[qa], xDe[qb]);
                if (x2 - x1 < paso * 1.5)
                {
                    f.Conexiones.Add(Enlace(u, x1 + _w / 2, x2 - _w / 2, 0));
                    inicioDe[u] = new Pt((x1 + x2) / 2, _h / 2);
                }
                else if (!u.Hijos.Any(Sirve))
                {
                    double y = -12 - 8 * arcosArriba++;
                    _enlacesHechos.Add(u.Id);
                    f.Conexiones.Add(new Conexion
                    {
                        UnionId = u.Id, Tipo = TipoConexion.Pareja, Directa = false,
                        Puntos = new() { new(x1 + _w / 4, 0), new(x1 + _w / 4, y), new(x2 - _w / 4, y), new(x2 - _w / 4, 0) },
                    });
                    inicioDe[u] = new Pt((x1 + x2) / 2, _h);
                }
                else
                {
                    double y = _h + 10 + 8 * arcos++;
                    _enlacesHechos.Add(u.Id);
                    f.Conexiones.Add(new Conexion
                    {
                        UnionId = u.Id, Tipo = TipoConexion.Pareja, Directa = false,
                        Puntos = new() { new(x1, _h), new(x1, y), new(x2, y), new(x2, _h) },
                    });
                    inicioDe[u] = new Pt((x1 + x2) / 2, y);
                }
            }

            // Hijos: cada unión los suyos, centrados bajo su punto de unión mientras no choquen con los de la anterior.
            var grupos = new List<(Union U, Pt Inicio, List<string> Hijos, Forma G)>();
            foreach (var (u, _, _) in uniones.OrderBy(x => inicioDe[x.U].X))
            {
                var hijos = new List<(string Id, Forma F)>();
                Forma g;
                double centro;
                if (!_escalonar)
                {
                    foreach (var h in u.Hijos) if (Sirve(h)) hijos.Add((h, Descendencia(h)));
                    if (hijos.Count == 0) continue;
                    foreach (var (_, hf) in hijos) hf.Mover(0, 1, _paso);
                    g = Empaquetar(hijos.Select(x => x.F).ToList());
                    centro = (hijos[0].F.Ancla + hijos[^1].F.Ancla) / 2;
                }
                else
                {
                    // Escalonado: por fuera (arriba) las familias más pequeñas y por dentro (abajo) las más grandes, que se
                    // extienden por debajo de las otras. Cada familia se abre hacia fuera: los padres sobre su hijo de dentro.
                    var ids = u.Hijos.Where(Sirve).OrderBy(Tamano).ToList();
                    if (ids.Count == 0) continue;
                    List<string> izqIds, derIds;
                    if (alinear > 0) (izqIds, derIds) = (ids, new List<string>());
                    else if (alinear < 0) (izqIds, derIds) = (new List<string>(), Enumerable.Reverse(ids).ToList());
                    else (izqIds, derIds) = (ids.Where((_, i) => i % 2 == 0).ToList(), ids.Where((_, i) => i % 2 == 1).Reverse().ToList());
                    var izqH = Construir(izqIds, 1); var derH = Construir(derIds, -1);
                    hijos = izqH.Concat(derH).ToList();
                    if (hijos.Count == 0) continue;
                    var partes = new List<Forma>();
                    if (izqH.Count > 0) partes.Add(Escalonar(izqH.Select(x => x.F).ToList(), 1));
                    if (derH.Count > 0) partes.Add(Escalonar(derH.Select(x => x.F).ToList(), -1));
                    g = Empaquetar(partes);
                    centro = alinear > 0 ? hijos[^1].F.Ancla : alinear < 0 ? hijos[0].F.Ancla : (hijos[0].F.Ancla + hijos[^1].F.Ancla) / 2;
                }
                g.Ancla = centro;
                grupos.Add((u, inicioDe[u], hijos.Select(x => x.Id).ToList(), g));
            }
            var acumulado = new Forma();
            foreach (var g in grupos)
            {
                g.G.Mover(g.Inicio.X - g.G.Ancla, 0, _paso);
                if (acumulado.Cartas.Count > 0)
                {
                    double d = acumulado.Separacion(g.G, Hueco);
                    if (d > 0) g.G.Mover(d, 0, _paso);
                }
                acumulado.Absorber(g.G);
            }
            f.Absorber(acumulado);

            // Altura de cada bus: si el punto de unión de una familia cae dentro del tramo de otra, su bus va por encima.
            var tramos = grupos.Select(g =>
            {
                var xs = g.Hijos.Select(id => XDe(f, id)).ToList();
                return (A: Math.Min(g.Inicio.X, xs.Min()), B: Math.Max(g.Inicio.X, xs.Max()));
            }).ToList();
            for (int k = 0; k < grupos.Count; k++)
            {
                int debajo = 0;
                for (int j = 0; j < grupos.Count; j++)
                    if (j != k && grupos[j].Inicio.X > tramos[k].A && grupos[j].Inicio.X < tramos[k].B) debajo++;
                double nivel = grupos.Count == 1 ? 0.45 : 0.3 + 0.35 * debajo / (grupos.Count - 1);
                double yBus = Math.Max(_h + _o.HuecoFilas * nivel, grupos[k].Inicio.Y + 10);
                foreach (var id in grupos[k].Hijos)
                    f.Conexiones.Add(HaciaHijo(grupos[k].U, id, grupos[k].Inicio, yBus, XDe(f, id), FilaDe(f, id) * _paso));
            }
            f.Ancla = 0;
            return f;
        }

        // ---------- antepasados ----------
        /// <summary>
        /// Bloque de un antepasado (o de la persona principal): sus padres arriba, sus hermanos colgando del punto de unión y él
        /// debajo de todos ellos, en la vertical de ese punto. La persona queda en la fila 0 y <see cref="Forma.Ancla"/> en su x.
        /// <paramref name="exterior"/>: lado hacia fuera del árbol (-1 izquierda, +1 derecha, 0 ninguno), el contrario al de su pareja.
        /// </summary>
        private Forma Bloque(string p, bool esRaiz, int exterior = 0)
        {
            _colocados.Add(p);
            var u = _unionHijo[p];
            var (arriba, junta, conPilar, extras) = Pareja(u, exterior);

            var antes = new List<(string Id, Forma F)>(); var despues = new List<(string Id, Forma F)>();
            if (!_escalonar)
            {
                bool pasado = false;
                foreach (var h in u.Hijos)
                {
                    if (h == p) { pasado = true; continue; }
                    if (!Sirve(h)) continue;
                    (pasado ? despues : antes).Add((h, Descendencia(h)));
                }
                if (_equilibrar) (antes, despues) = Repartir(antes.Concat(despues).ToList(), exterior);
            }
            else
            {
                // Escalonado: se reparten por su tamaño aproximado antes de construirlos, porque cada lado se construye abierto
                // hacia fuera. Por fuera (arriba) las familias más pequeñas y junto a su línea (abajo) las más grandes.
                var ids = u.Hijos.Where(h => h != p && Sirve(h)).ToList();
                var (izqIds, derIds) = Repartir(ids, ids.Select(h => Tamano(h) * (_w + Hueco)).ToList(), exterior);
                foreach (var h in izqIds.OrderBy(Tamano)) if (Sirve(h)) antes.Add((h, Descendencia(h, 1)));
                foreach (var h in derIds.OrderByDescending(Tamano)) if (Sirve(h)) despues.Add((h, Descendencia(h, -1)));
            }
            var hermanos = antes.Concat(despues).ToList();

            // Hijos de otras uniones de los padres (medio hermanos), por fuera.
            var gruposIzq = new List<(Union U, Pt Inicio, List<(string Id, Forma F)> Hijos)>();
            var gruposDer = new List<(Union U, Pt Inicio, List<(string Id, Forma F)> Hijos)>();
            foreach (var (ue, ini, lado) in extras)
            {
                var hs = new List<(string, Forma)>();
                foreach (var h in ue.Hijos) if (Sirve(h)) hs.Add((h, Descendencia(h, _escalonar ? (lado < 0 ? 1 : -1) : 0)));
                if (hs.Count > 0) (lado < 0 ? gruposIzq : gruposDer).Add((ue, ini, hs));
            }

            var izquierda = new List<Forma>();
            foreach (var g in gruposIzq.OrderBy(g => g.Inicio.X)) izquierda.AddRange(g.Hijos.Select(x => x.F));
            izquierda.AddRange(antes.Select(x => x.F));
            var derecha = despues.Select(x => x.F).ToList();
            foreach (var g in gruposDer.OrderBy(g => g.Inicio.X)) derecha.AddRange(g.Hijos.Select(x => x.F));
            foreach (var s in izquierda.Concat(derecha)) s.Mover(0, 1, _paso);

            // Escalonado: las familias de fuera arriba y las de dentro, más abajo, metidas bajo ellas.
            Forma? ladoIzq = null, ladoDer = null;
            if (_escalonar)
            {
                if (izquierda.Count > 0) ladoIzq = Escalonar(izquierda, 1);
                if (derecha.Count > 0) ladoDer = Escalonar(derecha, -1);
            }

            // La persona baja por debajo de todo lo que cuelga de sus padres.
            var colgantes = izquierda.Concat(derecha).ToList();
            int filaP = colgantes.Count == 0 ? 1 : 1 + colgantes.Max(x => x.FilaMax);

            // La persona (con su propia descendencia si es la principal) y, encima, el hueco por donde baja su línea.
            var propia = esRaiz ? Descendencia(p) : Tarjeta(p);
            propia.Mover(0, filaP, _paso);
            for (int r = 1; r < filaP; r++) propia.Ocupar(r, propia.Ancla - _w / 2, propia.Ancla + _w / 2);

            var secuencia = new List<Forma>();
            if (_escalonar)
            {
                if (ladoIzq != null) secuencia.Add(ladoIzq);
                secuencia.Add(propia);
                if (ladoDer != null) secuencia.Add(ladoDer);
            }
            else { secuencia.AddRange(izquierda); secuencia.Add(propia); secuencia.AddRange(derecha); }
            var banda = Empaquetar(secuencia);
            banda.Mover(junta - propia.Ancla, 0, _paso);
            if (_equilibrar && conPilar && gruposIzq.Count + gruposDer.Count == 0) junta = Centrar(arriba, banda, u, junta, esRaiz, exterior);
            arriba.Absorber(banda);

            var inicio = conPilar ? new Pt(junta, _h / 2) : new Pt(junta, _h);
            double yBus = _h + _o.HuecoFilas * 0.55;
            foreach (var (id, _) in hermanos) arriba.Conexiones.Add(HaciaHijo(u, id, inicio, yBus, XDe(arriba, id), FilaDe(arriba, id) * _paso));
            arriba.Conexiones.Add(HaciaHijo(u, p, inicio, yBus, junta, filaP * _paso));
            foreach (var g in gruposIzq.Concat(gruposDer))
            {
                double yExtra = _h + _o.HuecoFilas * 0.28;
                foreach (var (id, _) in g.Hijos)
                    arriba.Conexiones.Add(HaciaHijo(g.U, id, g.Inicio, Math.Max(yExtra, g.Inicio.Y + 10), XDe(arriba, id), FilaDe(arriba, id) * _paso));
            }

            arriba.Ancla = junta;
            arriba.Mover(0, -filaP, _paso);
            return arriba;
        }

        /// <summary>
        /// Ordenación C: los hijos de una pareja de antepasados (la banda que cuelga de su línea) se centran bajo esa línea si
        /// hay sitio, en vez de colgar del hueco entre los bloques de los dos. Debajo de la línea de la pareja no hay nada de
        /// sus bloques, así que se puede mover la banda mientras el punto de unión quede sobre la línea. Si la persona no es la
        /// principal, solo se mueve hacia el interior del árbol y sin salirse del ancho de los bloques de sus padres: así no se
        /// aleja de su propia pareja. Devuelve el nuevo punto de unión.
        /// </summary>
        private double Centrar(Forma arriba, Forma banda, Union u, double junta, bool esRaiz, int exterior)
        {
            var enlace = arriba.Conexiones.FirstOrDefault(c => c.UnionId == u.Id && c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2);
            if (enlace == null) return junta;
            double x1 = Math.Min(enlace.Puntos[0].X, enlace.Puntos[1].X), x2 = Math.Max(enlace.Puntos[0].X, enlace.Puntos[1].X);
            const double margen = 24;
            if (x2 - x1 < 2 * margen) return junta;
            double s = (x1 + x2) / 2 - (banda.MinX + banda.MaxX) / 2;
            s = Math.Clamp(s, x1 + margen - junta, x2 - margen - junta);
            if (!esRaiz && exterior < 0) s = Math.Clamp(s, 0, Math.Max(0, arriba.MaxX - banda.MaxX));
            else if (!esRaiz && exterior > 0) s = Math.Clamp(s, Math.Min(0, arriba.MinX - banda.MinX), 0);
            if (Math.Abs(s) < 1) return junta;
            banda.Mover(s, 0, _paso);
            enlace.Nudo = new Pt(junta + s, _h / 2);
            return junta + s;
        }

        /// <summary>
        /// Pareja de antepasados: la mujer a la izquierda y el hombre a la derecha, cada uno al pie de su bloque si tiene
        /// padres, separados por el hueco de una tarjeta (el punto de unión, del que cuelgan los hijos). Fila 0 = la pareja.
        /// Devuelve también las otras uniones de cada uno (su otra pareja queda por fuera, en la misma fila).
        /// </summary>
        private (Forma F, double Junta, bool ConPilar, List<(Union U, Pt Inicio, int Lado)> Extras) Pareja(Union u, int exterior = 0)
        {
            var ps = u.Parejas.Where(Sirve).ToList();
            if (ps.Count == 2 && !EsMujer(ps[0]) && EsMujer(ps[1])) ps.Reverse();
            if (_equilibrar && ps.Count == 2 && exterior != 0)
            {
                // La rama con más familia, por fuera: así el hijo (y su pareja) quedan cerca del borde de la otra.
                int p0 = Peso(ps[0]), p1 = Peso(ps[1]);
                if (p0 != p1 && (p0 > p1) != (exterior < 0)) ps.Reverse();
            }
            var formas = new List<Forma>();
            var extras = new List<(Union, Pt, int)>();
            var hechas = new HashSet<Union> { u };
            double paso = _w + _o.HuecoPareja;
            int arcos = 0, arcosArriba = 0;

            // Otras parejas de x (y, por fuera de cada una, las suyas), en la fila de x hacia su lado exterior.
            void Cadena(Forma fx, string x, double xx, int lado, ref double borde)
            {
                foreach (var otra in UnionesDe(x).OrderBy(o => o.Hijos.Any(Sirve) ? 0 : 1))
                {
                    if (!hechas.Add(otra)) continue;
                    string? sp = otra.Parejas.FirstOrDefault(q => q != x && Sirve(q));
                    Pt inicio = new(xx, _h);
                    if (sp != null)
                    {
                        _colocados.Add(sp);
                        double xs = borde + lado * paso;
                        borde = xs;
                        fx.Cartas.Add(new CartaB { Id = sp, X = xs, Fila = 0 });
                        fx.Ocupar(0, xs - _w / 2, xs + _w / 2);
                        double x1 = Math.Min(xx, xs), x2 = Math.Max(xx, xs);
                        if (x2 - x1 < paso * 1.5)
                        {
                            fx.Conexiones.Add(Enlace(otra, x1 + _w / 2, x2 - _w / 2, 0));
                            inicio = new((x1 + x2) / 2, _h / 2);
                        }
                        else
                        {
                            _enlacesHechos.Add(otra.Id);
                            bool hijos = otra.Hijos.Any(Sirve);
                            double y = hijos ? _h + 10 + 8 * arcos++ : -12 - 8 * arcosArriba++;
                            var pts = hijos
                                ? new List<Pt> { new(x1, _h), new(x1, y), new(x2, y), new(x2, _h) }
                                : new List<Pt> { new(x1 + _w / 4, 0), new(x1 + _w / 4, y), new(x2 - _w / 4, y), new(x2 - _w / 4, 0) };
                            fx.Conexiones.Add(new Conexion { UnionId = otra.Id, Tipo = TipoConexion.Pareja, Puntos = pts });
                            inicio = new((x1 + x2) / 2, hijos ? y : _h);
                        }
                        Cadena(fx, sp, xs, lado, ref borde);
                    }
                    extras.Add((otra, inicio, lado));
                }
            }

            var conBloque = new List<bool>();
            for (int i = 0; i < ps.Count; i++)
            {
                var x = ps[i];
                int lado = ps.Count == 1 ? 1 : (i == 0 ? -1 : 1);
                conBloque.Add(TienePadres(x));
                var fx = conBloque[i] ? Bloque(x, false, ps.Count == 1 ? exterior : lado) : Tarjeta(x);
                double borde = fx.Ancla;
                Cadena(fx, x, fx.Ancla, lado, ref borde);
                formas.Add(fx);
            }

            if (formas.Count < 2)
            {
                var sola = formas.Count == 1 ? formas[0] : new Forma();
                return (sola, sola.Ancla, false, extras);
            }

            // Escalonado: el bloque de uno de los dos puede subir por encima del otro y meterse sobre él.
            if (_escalonar) ElevarUnBloque(formas, ps, conBloque);

            var (izq, der) = (formas[0], formas[1]);
            var pilar = new Forma();
            int filaPilar = _escalonar ? Math.Max(izq.FilaMin, der.FilaMin) : Math.Min(izq.FilaMin, der.FilaMin);
            for (int r = filaPilar; r <= 0; r++) pilar.Ocupar(r, -_w / 2, _w / 2);
            pilar.Mover(izq.Separacion(pilar, Hueco), 0, _paso);
            double xIzq = izq.Ancla;
            izq.Absorber(pilar);
            double dDer = izq.Separacion(der, Hueco);
            der.Mover(dDer, 0, _paso);
            double xDer = der.Ancla;
            izq.Absorber(der);
            double junta = pilar.Ancla;
            // Las otras uniones del de la derecha se anotaron antes de moverlo.
            for (int i = 0; i < extras.Count; i++)
                if (extras[i].Item3 > 0) extras[i] = (extras[i].Item1, new Pt(extras[i].Item2.X + dDer, extras[i].Item2.Y), 1);
            izq.Conexiones.Add(Enlace(u, xIzq + _w / 2, xDer - _w / 2, 0, new Pt(junta, _h / 2)));
            izq.Ancla = junta;
            return (izq, junta, true, extras);
        }

        /// <summary>
        /// Ordenación D: prueba a subir el bloque de la izquierda o el de la derecha unas filas (para que se coloque por encima
        /// del otro en vez de a su lado) y se queda con lo que menos cuesta en ancho más alto.
        /// </summary>
        private void ElevarUnBloque(List<Forma> formas, List<string> ps, List<bool> conBloque)
        {
            double Probar(Forma izq, Forma der)
            {
                var pilar = new Forma();
                for (int r = Math.Max(izq.FilaMin, der.FilaMin); r <= 0; r++) pilar.Ocupar(r, -_w / 2, _w / 2);
                var acc = Contorno(izq);
                pilar.Mover(acc.Separacion(pilar, Hueco), 0, _paso);
                acc.Absorber(pilar);
                var d = Contorno(der);
                d.Mover(acc.Separacion(d, Hueco), 0, _paso);
                acc.Absorber(d);
                return Coste(acc.MaxX - acc.MinX, acc.FilaMax - acc.FilaMin + 1);
            }
            double mejor = Probar(formas[0], formas[1]) - 0.5;
            int lado = -1, filas = 0;
            for (int i = 0; i < 2; i++)
            {
                if (!conBloque[i]) continue;
                int maxK = -formas[1 - i].FilaMin + 1;
                for (int k = 1; k <= maxK; k++)
                {
                    var e = ContornoElevado(formas[i], k);
                    double c = i == 0 ? Probar(e, formas[1]) : Probar(formas[0], e);
                    if (c < mejor) { mejor = c - 0.5; lado = i; filas = k; }
                }
            }
            if (lado >= 0) Elevar(formas[lado], ps[lado], filas);
        }

        /// <summary>
        /// Reparte los hermanos (en su orden) a la izquierda y a la derecha de la línea del antepasado de modo que los dos lados
        /// queden lo más parecidos posible en anchura; si no pueden quedar iguales, el más ancho va hacia el exterior.
        /// </summary>
        private (List<(string Id, Forma F)>, List<(string Id, Forma F)>) Repartir(List<(string Id, Forma F)> hermanos, int exterior) =>
            Repartir(hermanos, hermanos.Select(h => h.F.MaxX - h.F.MinX + Hueco).ToList(), exterior);

        private static (List<T>, List<T>) Repartir<T>(List<T> hermanos, List<double> anchos, int exterior)
        {
            int n = hermanos.Count;
            double total = anchos.Sum();
            int mejor = 0; double mejorCoste = double.MaxValue, izq = 0;
            for (int k = 0; k <= n; k++)
            {
                if (k > 0) izq += anchos[k - 1];
                double der = total - izq;
                bool alReves = exterior < 0 ? der > izq + 0.5 : exterior > 0 && izq > der + 0.5;
                double coste = Math.Abs(izq - der) + (alReves ? 1 : 0);
                if (coste < mejorCoste - 1e-9) { mejorCoste = coste; mejor = k; }
            }
            return (hermanos.Take(mejor).ToList(), hermanos.Skip(mejor).ToList());
        }

        /// <summary>Cuánta familia cuelga por encima de una persona: los que se alcanzan desde sus padres sin pasar por ella.</summary>
        private int Peso(string x)
        {
            if (!_unionHijo.TryGetValue(x, out var up)) return 1;
            var vistos = new HashSet<string> { x };
            var cola = new Queue<string>();
            foreach (var q in up.Parejas.Concat(up.Hijos)) if (Sirve(q) && vistos.Add(q)) cola.Enqueue(q);
            while (cola.Count > 0)
            {
                var y = cola.Dequeue();
                var uniones = UnionesDe(y).ToList();
                if (_unionHijo.TryGetValue(y, out var uh)) uniones.Add(uh);
                foreach (var un in uniones)
                    foreach (var q in un.Parejas.Concat(un.Hijos))
                        if (Sirve(q) && vistos.Add(q)) cola.Enqueue(q);
            }
            return vistos.Count;
        }

        // ---------- lo que no encaja en la estructura ----------
        /// <summary>Personas alcanzables que no se han colocado (p. ej. padres de una pareja política): a la derecha, por familias.</summary>
        private void ColocarSobrantes(Forma total)
        {
            foreach (var id in _a.Personas.Select(p => p.Id).Where(Sirve).ToList())
            {
                if (!Sirve(id)) continue;
                // sube hasta el antepasado más alto aún sin colocar
                var cabeza = id;
                var vistos = new HashSet<string> { cabeza };
                while (_unionHijo.TryGetValue(cabeza, out var up) && up.Parejas.FirstOrDefault(Sirve) is { } q && vistos.Add(q)) cabeza = q;
                var f = Descendencia(cabeza);
                int fila = FilaDeReferencia(f, total);
                f.Mover(total.MaxX + 2 * Hueco - f.MinX, fila, _paso);
                total.Absorber(f);
            }
        }

        private int FilaDeReferencia(Forma f, Forma total)
        {
            var filaDe = total.Cartas.ToDictionary(c => c.Id, c => c.Fila);
            foreach (var c in f.Cartas)
            {
                foreach (var u in UnionesDe(c.Id))
                {
                    foreach (var q in u.Parejas) if (filaDe.TryGetValue(q, out var r)) return r - c.Fila;
                    foreach (var h in u.Hijos) if (filaDe.TryGetValue(h, out var r)) return r - 1 - c.Fila;
                }
                if (_unionHijo.TryGetValue(c.Id, out var up))
                    foreach (var q in up.Parejas) if (filaDe.TryGetValue(q, out var r)) return r + 1 - c.Fila;
            }
            return total.FilaMin - f.FilaMin;
        }

        /// <summary>Uniones cuyas líneas no ha dibujado la estructura (parientes colocados en otro sitio): líneas sencillas.</summary>
        private void ConexionesPendientes(Forma total)
        {
            var pos = total.Cartas.ToDictionary(c => c.Id);
            foreach (var u in _a.Uniones)
            {
                var ps = u.Parejas.Where(pos.ContainsKey).Select(q => pos[q]).OrderBy(c => c.X).ToList();
                if (ps.Count == 0) continue;
                Pt ancla;
                if (ps.Count == 2)
                {
                    var (a, b) = (ps[0], ps[1]);
                    double ya = a.Fila * _paso, yb = b.Fila * _paso;
                    if (a.Fila == b.Fila) ancla = new Pt((a.X + b.X) / 2, ya + _h / 2);
                    else ancla = new Pt((a.X + b.X) / 2, Math.Max(ya, yb) + _h + _o.HuecoFilas * 0.2);
                    if (!_enlacesHechos.Contains(u.Id))
                    {
                        _enlacesHechos.Add(u.Id);
                        var pts = a.Fila == b.Fila
                            ? new List<Pt> { new(a.X + _w / 2, ya + _h / 2), new(b.X - _w / 2, yb + _h / 2) }
                            : new List<Pt> { new(a.X, ya + _h), new(a.X, ancla.Y), new(b.X, ancla.Y), new(b.X, yb + _h) };
                        total.Conexiones.Add(new Conexion { UnionId = u.Id, Tipo = TipoConexion.Pareja, Puntos = pts, Directa = ps.All(c => _directa.Contains(c.Id)) });
                    }
                }
                else ancla = new Pt(ps[0].X, ps[0].Fila * _paso + _h);

                foreach (var h in u.Hijos)
                {
                    if (!pos.TryGetValue(h, out var ch) || _hijosHechos.Contains((u.Id, h))) continue;
                    double yHijo = ch.Fila * _paso;
                    double yBus = Math.Max(ancla.Y + 12, yHijo - _o.HuecoFilas * 0.5);
                    total.Conexiones.Add(HaciaHijo(u, h, ancla, yBus, ch.X, yHijo));
                }
            }
        }

        // ---------- resultado ----------
        private LayoutResult Resultado(Forma total, LayoutResult res)
        {
            if (total.Cartas.Count == 0) return res;
            double minX = total.Cartas.Min(c => c.X) - _w / 2, maxX = total.Cartas.Max(c => c.X) + _w / 2;
            int minFila = total.Cartas.Min(c => c.Fila), maxFila = total.Cartas.Max(c => c.Fila);
            total.Mover(_o.Margen - minX, -minFila, _paso);
            double dy = _o.Margen;
            foreach (var c in total.Cartas)
            {
                res.Cartas[c.Id] = new CartaPos
                {
                    Id = c.Id, X = c.X, Y = dy + c.Fila * _paso, Generacion = c.Fila,
                    Cluster = res.Cartas.Count, IndiceEnCluster = 0,
                    Directa = _directa.Contains(c.Id), Politica = !_sangre.Contains(c.Id),
                };
            }
            foreach (var cx in total.Conexiones)
            {
                for (int i = 0; i < cx.Puntos.Count; i++) cx.Puntos[i] = new Pt(cx.Puntos[i].X, cx.Puntos[i].Y + dy);
                if (cx.Nudo is { } n) cx.Nudo = new Pt(n.X, n.Y + dy);
                res.Conexiones.Add(cx);
            }
            res.Ancho = maxX - minX + 2 * _o.Margen;
            res.Alto = (maxFila - minFila) * _paso + _h + 2 * _o.Margen;
            return res;
        }
    }
}
