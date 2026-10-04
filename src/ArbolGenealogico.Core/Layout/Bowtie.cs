using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Layout;

/// <summary>
/// Ordenación E («Bowtie», pajarita): árbol de antepasados en horizontal. La persona principal en el centro, su padre pegado
/// a su izquierda y su madre a su derecha; los antepasados de cada uno se abren hacia su lado, una generación por columna,
/// cada persona a media altura entre sus dos padres y unida a ellos por un corchete que entra por arriba y por abajo de su
/// tarjeta. Solo salen la persona principal y sus antepasados directos.
/// </summary>
internal static class Bowtie
{
    public static LayoutResult Calcular(Arbol arbol, LayoutOptions o) => new Motor(arbol, o).Ejecutar();

    private sealed class Motor
    {
        private readonly Arbol _a;
        private readonly LayoutOptions _o;
        private readonly double _w, _h, _ranura;
        private const double HuecoCentro = 24;      // entre la persona principal y sus padres
        private const double HuecoColumnas = 36;    // entre columnas cuyas tarjetas quedan a la misma altura
        private const double Entrada = 22;          // a qué distancia del borde de la tarjeta entra el corchete
        private readonly Dictionary<string, Persona> _personas = new();
        private readonly Dictionary<string, Union> _unionHijo = new();
        private readonly HashSet<string> _colocados = new();
        private readonly Dictionary<string, (int Gen, int Lado, double Y)> _pos = new();
        private double _siguiente;                   // siguiente ranura libre del lado que se está colocando

        public Motor(Arbol a, LayoutOptions o)
        {
            _a = a; _o = o;
            _w = o.AnchoCarta; _h = o.AltoCarta; _ranura = o.AltoCarta + 22;
        }

        private List<string> Padres(string id)
        {
            if (!_unionHijo.TryGetValue(id, out var u)) return new();
            var ps = u.Parejas.Where(p => _personas.ContainsKey(p) && !_colocados.Contains(p)).ToList();
            // el hombre arriba y la mujer abajo
            if (ps.Count == 2 && _personas[ps[0]].Sexo == Sexo.Mujer && _personas[ps[1]].Sexo != Sexo.Mujer) ps.Reverse();
            return ps;
        }

        /// <summary>Coloca a la persona y a sus antepasados (en su lado) y devuelve su altura (centro de la tarjeta).</summary>
        private double Colocar(string id, int gen, int lado)
        {
            _colocados.Add(id);
            var padres = Padres(id);
            foreach (var p in padres) _colocados.Add(p);
            double y;
            if (padres.Count == 0) { y = _siguiente; _siguiente += _ranura; }
            else
            {
                var ys = new List<double>();
                foreach (var p in padres) { _colocados.Remove(p); ys.Add(Colocar(p, gen + 1, lado)); }
                y = ys.Count == 1 ? ys[0] : (ys[0] + ys[^1]) / 2;
                // con un solo padre, la tarjeta no puede quedar a su misma altura (el corchete no tendría por dónde entrar)
                if (ys.Count == 1) { y = ys[0] + _ranura / 2; _siguiente = Math.Max(_siguiente, y + _ranura); }
            }
            _pos[id] = (gen, lado, y);
            return y;
        }

        public LayoutResult Ejecutar()
        {
            var res = new LayoutResult { Opciones = _o };
            foreach (var p in _a.Personas) _personas[p.Id] = p;
            foreach (var u in _a.Uniones) foreach (var h in u.Hijos) _unionHijo.TryAdd(h, u);
            var raiz = _personas.ContainsKey(_a.RaizId) ? _a.RaizId : _a.Personas.FirstOrDefault()?.Id;
            if (raiz == null) return res;

            _colocados.Add(raiz);
            _pos[raiz] = (0, 0, 0);
            var padres = Padres(raiz);
            foreach (var p in padres) _colocados.Add(p);
            // padre a la izquierda y madre a la derecha (con un solo progenitor, a la izquierda)
            if (padres.Count == 2 && _personas[padres[0]].Sexo == Sexo.Mujer && _personas[padres[1]].Sexo != Sexo.Mujer) padres.Reverse();
            for (int i = 0; i < padres.Count; i++)
            {
                int lado = i == 0 ? -1 : 1;
                _siguiente = 0;
                _colocados.Remove(padres[i]);
                double y = Colocar(padres[i], 1, lado);
                // todo su lado se desplaza para que quede a la altura de la persona principal
                foreach (var id in _pos.Keys.ToList())
                    if (_pos[id].Lado == lado) _pos[id] = (_pos[id].Gen, lado, _pos[id].Y - y);
            }

            // Columnas: cada generación tan cerca de la anterior como permitan sus tarjetas (si alguna queda a la misma
            // altura que otra de la columna de al lado, no pueden solaparse en horizontal; si no, basta con que quepa el corchete).
            var xs = new Dictionary<(int Gen, int Lado), double> { [(0, 0)] = 0 };
            foreach (int lado in new[] { -1, 1 })
            {
                int maxGen = _pos.Values.Where(v => v.Lado == lado).Select(v => v.Gen).DefaultIfEmpty(0).Max();
                if (maxGen == 0) continue;
                double x = lado * (_w + HuecoCentro);
                xs[(1, lado)] = x;
                for (int g = 2; g <= maxGen; g++)
                {
                    var interior = _pos.Values.Where(v => v.Lado == lado && v.Gen == g - 1).Select(v => v.Y).ToList();
                    var exterior = _pos.Values.Where(v => v.Lado == lado && v.Gen == g).Select(v => v.Y).ToList();
                    bool alturaComun = interior.Any(a => exterior.Any(b => Math.Abs(a - b) < _h + 12));
                    double paso = alturaComun ? _w + HuecoColumnas : _w - Entrada + 12;
                    x += lado * paso;
                    xs[(g, lado)] = x;
                }
            }

            double X(string id) => xs[(_pos[id].Gen, _pos[id].Lado)];
            var directa = _a.LineaDirecta();
            var cartas = _pos.Keys.ToList();

            // Conexiones: de cada progenitor, un tramo horizontal desde su borde interior hasta la vertical que entra en la
            // tarjeta del hijo por arriba (el de arriba) o por abajo (el de abajo). Los padres de la principal, de lado a lado.
            var conexiones = new List<Conexion>();
            foreach (var hijo in cartas)
            {
                if (!_unionHijo.TryGetValue(hijo, out var u)) continue;
                var ps = u.Parejas.Where(p => _pos.ContainsKey(p) && _pos[p].Gen == _pos[hijo].Gen + 1 &&
                                              (_pos[hijo].Gen == 0 || _pos[p].Lado == _pos[hijo].Lado)).ToList();
                double xh = X(hijo), yh = _pos[hijo].Y;
                foreach (var p in ps)
                {
                    double xp = X(p), yp = _pos[p].Y;
                    int lado = _pos[p].Lado;                       // hacia dónde queda el progenitor
                    var pts = new List<Pt>();
                    if (_pos[hijo].Gen == 0)
                    {
                        pts.Add(new(xp - lado * _w / 2, yp));
                        pts.Add(new(xh + lado * _w / 2, yh));
                    }
                    else
                    {
                        double borde = xp - lado * _w / 2;          // borde del progenitor que mira al hijo
                        double xv = xh + lado * (_w / 2 - Entrada); // vertical dentro del ancho del hijo, cerca de su borde exterior
                        double yEntrada = yp < yh ? yh - _h / 2 : yh + _h / 2;
                        pts.Add(new(borde, yp));
                        pts.Add(new(xv, yp));
                        pts.Add(new(xv, yEntrada));
                    }
                    conexiones.Add(new Conexion
                    {
                        UnionId = u.Id, Tipo = TipoConexion.Descendencia, HijoId = hijo,
                        Directa = directa.Contains(hijo) && directa.Contains(p), Puntos = pts,
                    });
                }
            }

            // A coordenadas positivas, con margen.
            double minX = cartas.Min(id => X(id)) - _w / 2, maxX = cartas.Max(id => X(id)) + _w / 2;
            double minY = cartas.Min(id => _pos[id].Y) - _h / 2, maxY = cartas.Max(id => _pos[id].Y) + _h / 2;
            double dx = _o.Margen - minX, dy = _o.Margen - minY;
            var sangre = _a.Sanguineos();
            foreach (var id in cartas)
                res.Cartas[id] = new CartaPos
                {
                    Id = id, X = X(id) + dx, Y = _pos[id].Y - _h / 2 + dy, Generacion = -_pos[id].Gen,
                    Cluster = res.Cartas.Count, Directa = directa.Contains(id), Politica = !sangre.Contains(id),
                };
            foreach (var c in conexiones)
            {
                for (int i = 0; i < c.Puntos.Count; i++) c.Puntos[i] = new Pt(c.Puntos[i].X + dx, c.Puntos[i].Y + dy);
                res.Conexiones.Add(c);
            }
            res.Ancho = maxX - minX + 2 * _o.Margen;
            res.Alto = maxY - minY + 2 * _o.Margen;
            return res;
        }
    }
}
