using System.Globalization;
using ArbolGenealogico.Core.Layout;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Presentacion;

/// <summary>
/// Un tamaño de papel: los ISO (A4 a A1, los de casi todo el mundo) y los de Estados Unidos y Canadá (Carta, la de las
/// impresoras de casa; Legal, más larga; y Tabloide, el doble de una Carta, como el A3).
/// </summary>
public sealed record Papel(string Codigo, double AnchoMm, double AltoMm, bool Americano)
{
    public static readonly Papel A4 = new("A4", 210, 297, false);
    public static readonly Papel A3 = new("A3", 297, 420, false);
    public static readonly Papel A2 = new("A2", 420, 594, false);
    public static readonly Papel A1 = new("A1", 594, 841, false);
    /// <summary>8½ × 11 pulgadas.</summary>
    public static readonly Papel Carta = new("Letter", 215.9, 279.4, true);
    /// <summary>8½ × 14 pulgadas.</summary>
    public static readonly Papel Legal = new("Legal", 215.9, 355.6, true);
    /// <summary>11 × 17 pulgadas.</summary>
    public static readonly Papel Tabloide = new("Tabloid", 279.4, 431.8, true);

    public static readonly IReadOnlyList<Papel> Todos = new[] { A4, A3, A2, A1, Carta, Legal, Tabloide };

    /// <summary>El nombre que se enseña («A4», «Carta»…).</summary>
    public string Nombre => Codigo switch
    {
        "Letter" => T("Carta"), "Legal" => T("Legal"), "Tabloid" => T("Tabloide"), _ => Codigo,
    };

    /// <summary>Las medidas, en centímetros (y en pulgadas los americanos): «21 × 29,7 cm», «8½ × 11 in · 21,6 × 27,9 cm».</summary>
    public string Medidas
    {
        get
        {
            var c = Cultura;
            string cm = $"{(AnchoMm / 10).ToString("0.#", c)} × {(AltoMm / 10).ToString("0.#", c)} cm";
            if (!Americano) return cm;
            static string Pulgadas(double mm)
            {
                double p = Math.Round(mm / 25.4 * 2) / 2;
                return Math.Floor(p).ToString(CultureInfo.InvariantCulture) + (p % 1 != 0 ? "½" : "");
            }
            return $"{Pulgadas(AnchoMm)} × {Pulgadas(AltoMm)} in  ·  {cm}";
        }
    }

    public override string ToString() => Codigo;

    /// <summary>El papel con ese código («A4», «Letter»…), o A4 si no se conoce.</summary>
    public static Papel De(string? codigo) => Todos.FirstOrDefault(p => p.Codigo == codigo) ?? A4;

    /// <summary>Los países donde el papel de siempre es el Carta (y no el A4).</summary>
    private static readonly HashSet<string> PaisesCarta = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "CA", "MX", "PH", "CL", "CO", "VE", "CR", "GT", "HN", "SV", "NI", "PA", "DO", "PR", "BZ",
    };

    /// <summary>El papel habitual en un país (código de dos letras, como «US» o «ES»): Carta en América del Norte y parte de Hispanoamérica; A4 en el resto.</summary>
    public static Papel Habitual(string? pais) => pais != null && PaisesCarta.Contains(pais) ? Carta : A4;

    /// <summary>El papel habitual en el país de una cultura («en-US», «es-ES»…).</summary>
    public static Papel HabitualDe(CultureInfo cultura)
    {
        try { return cultura.Name.Contains('-') ? Habitual(new RegionInfo(cultura.Name).TwoLetterISORegionName) : A4; }
        catch (ArgumentException) { return A4; }
    }
}

/// <summary>Un rectángulo en coordenadas del árbol.</summary>
public readonly record struct Caja(double X, double Y, double Ancho, double Alto)
{
    public double Derecha => X + Ancho;
    public double Abajo => Y + Alto;
}

/// <summary>
/// Cómo imprimir el árbol en el número de hojas elegido de un tamaño de papel: hojas en vertical u horizontal, en fila, en
/// columna o en cuadrícula, que se pegan recortando sus márgenes interiores. Se elige la combinación con la que el árbol sale
/// más grande y con los cortes entre hojas por sitios sin tarjetas.
/// </summary>
public static class PlanImpresion
{
    /// <summary>
    /// Margen de cada hoja, en el que no se imprime el árbol: las impresoras no llegan al borde, y es la tira por la que se
    /// pegan las hojas (la de al lado se recorta por su línea de corte y se pone encima de este margen).
    /// </summary>
    public const double MargenMm = 12;
    /// <summary>Lo más que se puede repartir el árbol: 8 hojas.</summary>
    public const int MaxHojas = 8;

    /// <summary>Qué tal se lee una letra impresa.</summary>
    public enum Lectura { MuyPequena, Pequena, Bien, Comoda }

    /// <summary>
    /// Una forma de imprimir: hojas en vertical u horizontal, en <paramref name="Filas"/> × <paramref name="Columnas"/>, los
    /// milímetros por píxel del árbol y dónde se corta el árbol entre hojas (<paramref name="CortesX"/>: Columnas + 1 valores,
    /// del borde izquierdo del contenido al derecho; <paramref name="CortesY"/>: igual en vertical). Cada hoja imprime su trozo
    /// desde la esquina de su zona imprimible; si es más pequeño que ella, sobra margen por la derecha o por abajo.
    /// <paramref name="TarjetasCortadas"/>: cuántas tarjetas atraviesa algún corte (normalmente ninguna).
    /// </summary>
    public sealed record Configuracion(Papel Papel, bool Vertical, int Filas, int Columnas, double MmPorPx,
        double[] CortesX, double[] CortesY, int TarjetasCortadas, bool SePidioNoCortar = false)
    {
        public int Hojas => Filas * Columnas;
        /// <summary>Tamaño de cada hoja en mm, ya girada si va en horizontal.</summary>
        public double AnchoHojaMm => Vertical ? Papel.AnchoMm : Papel.AltoMm;
        public double AltoHojaMm => Vertical ? Papel.AltoMm : Papel.AnchoMm;
        /// <summary>Zona imprimible de una hoja, en píxeles del árbol.</summary>
        public double AnchoUtilPx => (AnchoHojaMm - 2 * MargenMm) / MmPorPx;
        public double AltoUtilPx => (AltoHojaMm - 2 * MargenMm) / MmPorPx;

        /// <summary>Lo que se imprime en la hoja de esa fila y columna (en coordenadas del árbol).</summary>
        public Caja Pieza(int fila, int columna) =>
            new(CortesX[columna], CortesY[fila], CortesX[columna + 1] - CortesX[columna], CortesY[fila + 1] - CortesY[fila]);

        /// <summary>Todo lo que se imprime (en coordenadas del árbol).</summary>
        public Caja Util => new(CortesX[0], CortesY[0], CortesX[^1] - CortesX[0], CortesY[^1] - CortesY[0]);

        /// <summary>El papel de todas las hojas ya pegadas (en coordenadas del árbol).</summary>
        public Caja Pegado
        {
            get
            {
                double m = MargenMm / MmPorPx;
                double izquierda = CortesX[0] - m, arriba = CortesY[0] - m;
                double derecha = CortesX[^2] + AnchoUtilPx + m, abajo = CortesY[^2] + AltoUtilPx + m;
                return new Caja(izquierda, arriba, derecha - izquierda, abajo - arriba);
            }
        }

        /// <summary>Tamaño de la letra, en puntos (como en Word), con el que sale impreso un texto de <paramref name="px"/> del árbol.</summary>
        public double Puntos(double px) => px * MmPorPx / (25.4 / 72);

        /// <summary>Qué tal se lee una letra impresa de ese tamaño (un libro usa 10 a 12 pt).</summary>
        public static Lectura Legibilidad(double puntos) =>
            puntos < 4 ? Lectura.MuyPequena : puntos < 6 ? Lectura.Pequena : puntos < 8 ? Lectura.Bien : Lectura.Comoda;

        public static string Texto(Lectura l) => l switch
        {
            Lectura.MuyPequena => T("muy pequeña"), Lectura.Pequena => T("pequeña"), Lectura.Bien => T("se lee bien"), _ => T("cómoda de leer"),
        };

        /// <summary>«3 hojas A4 horizontales, una al lado de otra».</summary>
        public string Reparto
        {
            get
            {
                string hojas = Hojas == 1
                    ? (Vertical ? T("1 hoja {0} vertical", Papel.Nombre) : T("1 hoja {0} horizontal", Papel.Nombre))
                    : (Vertical ? T("{0} hojas {1} verticales", Hojas, Papel.Nombre) : T("{0} hojas {1} horizontales", Hojas, Papel.Nombre));
                string forma = Hojas == 1 ? "" : Filas == 1 ? T(", una al lado de otra") : Columnas == 1 ? T(", una debajo de otra") : T(", en {0} filas de {1}", Filas, Columnas);
                return hojas + forma;
            }
        }

        /// <summary>«nombres en letra de 7,5 pt (se lee bien)».</summary>
        /// <param name="letraNombre">Tamaño de la letra de los nombres en el árbol (px).</param>
        public string Letra(double letraNombre)
        {
            double pt = Puntos(letraNombre);
            return T("nombres en letra de {0} pt ({1})", pt.ToString(pt < 10 ? "0.0" : "0", Cultura), Texto(Legibilidad(pt)));
        }

        /// <summary>Si los cortes atraviesan tarjetas, cuántas (o "" si ninguna).</summary>
        public string Cortes => TarjetasCortadas == 0 ? ""
            : SePidioNoCortar ? N(TarjetasCortadas, "con estas hojas no se puede evitar cortar 1 tarjeta", "con estas hojas no se puede evitar cortar {0} tarjetas")
            : N(TarjetasCortadas, "los cortes atraviesan 1 tarjeta", "los cortes atraviesan {0} tarjetas");

        /// <param name="letraNombre">Tamaño de la letra de los nombres en el árbol (px).</param>
        public string Descripcion(double letraNombre) =>
            $"{Reparto}  ·  {Letra(letraNombre)}{(Cortes.Length > 0 ? "  ·  " + Cortes : "")}";
    }

    private static (LayoutResult L, Papel P, int H, bool E, Configuracion? C)? _cache;

    /// <summary>
    /// La mejor forma de imprimir el árbol en ese número de hojas de ese papel, o null si no hay árbol: entre todas las
    /// colocaciones de las hojas, la que deja el árbol más grande con los cortes entre hojas por sitios sin tarjetas.
    /// </summary>
    public static Configuracion? Mejor(LayoutResult? layout, Papel papel, int hojas, bool evitarCortes = true)
    {
        if (layout == null || layout.Cartas.Count == 0) return null;
        if (_cache is { } k && ReferenceEquals(k.L, layout) && k.P == papel && k.H == hojas && k.E == evitarCortes) return k.C;
        var o = layout.Opciones;
        const double holgura = 12;      // sombras y brillo alrededor de las tarjetas
        double minX = layout.Cartas.Values.Min(c => c.X) - o.AnchoCarta / 2 - holgura, maxX = layout.Cartas.Values.Max(c => c.X) + o.AnchoCarta / 2 + holgura;
        double minY = layout.Cartas.Values.Min(c => c.Y) - holgura, maxY = layout.Cartas.Values.Max(c => c.Y) + o.AltoCarta + holgura;
        double w = maxX - minX, h = maxY - minY;
        // Por dónde no se puede cortar: las tarjetas (con su holgura).
        var tarjetasX = layout.Cartas.Values.Select(c => (c.X - o.AnchoCarta / 2 - holgura, c.X + o.AnchoCarta / 2 + holgura)).ToList();
        var tarjetasY = layout.Cartas.Values.Select(c => (c.Y - holgura, c.Y + o.AltoCarta + holgura)).ToList();
        var tramos = layout.Conexiones.SelectMany(c => c.Puntos.Zip(c.Puntos.Skip(1))).ToList();
        int CrucesX(double x) => tramos.Count(t => (t.First.X - x) * (t.Second.X - x) < 0);
        int CrucesY(double y) => tramos.Count(t => (t.First.Y - y) * (t.Second.Y - y) < 0);
        // Por dónde se puede cortar (y cuántas líneas cruza cada sitio) no depende del papel ni del tamaño: se calcula una vez
        // por eje, y no en cada una de las pruebas.
        var ejeX = new Eje(minX, maxX, tarjetasX, CrucesX);
        var ejeY = new Eje(minY, maxY, tarjetasY, CrucesY);

        double aw = papel.AnchoMm, ah = papel.AltoMm;
        Configuracion? mejor = null;
        foreach (bool vertical in new[] { true, false })
        {
            double pw = vertical ? aw : ah, ph = vertical ? ah : aw;
            for (int filas = 1; filas <= hojas; filas++)
            {
                if (hojas % filas != 0) continue;
                int cols = hojas / filas;
                // Al pegarlas se recortan los márgenes interiores: lo impreso sigue de una hoja a la otra.
                double mmMax = Math.Min(cols * (pw - 2 * MargenMm) / w, filas * (ph - 2 * MargenMm) / h);
                Configuracion? c = null;
                // Si a ese tamaño los cortes no caen entre tarjetas, se reduce un poco el árbol (hasta un 25 %) para que quepan.
                for (int paso = 0; paso <= 25 && c == null && evitarCortes; paso++)
                {
                    double mm = mmMax * (1 - paso / 100.0);
                    var cx = Cortes(ejeX, cols, (pw - 2 * MargenMm) / mm);
                    var cy = cx == null ? null : Cortes(ejeY, filas, (ph - 2 * MargenMm) / mm);
                    if (cx != null && cy != null) c = new Configuracion(papel, vertical, filas, cols, mm, cx, cy, 0, true);
                }
                if (c == null)
                {
                    // Cortes a partes iguales al tamaño máximo (se pidió así, o no hay manera de no cortar tarjetas).
                    var cx = Enumerable.Range(0, cols + 1).Select(i => minX + w * i / cols).ToArray();
                    var cy = Enumerable.Range(0, filas + 1).Select(i => minY + h * i / filas).ToArray();
                    int cortadas = layout.Cartas.Values.Count(t =>
                        cx.Skip(1).SkipLast(1).Any(x => x > t.X - o.AnchoCarta / 2 && x < t.X + o.AnchoCarta / 2) ||
                        cy.Skip(1).SkipLast(1).Any(y => y > t.Y && y < t.Y + o.AltoCarta));
                    c = new Configuracion(papel, vertical, filas, cols, mmMax, cx, cy, cortadas, evitarCortes);
                }
                // Mejor si no corta tarjetas y, entre las que son iguales en eso, la que deja el árbol más grande.
                bool mejora = mejor == null
                    || (evitarCortes && c.TarjetasCortadas == 0 && mejor.TarjetasCortadas > 0)
                    || ((!evitarCortes || (c.TarjetasCortadas == 0) == (mejor.TarjetasCortadas == 0)) && c.MmPorPx > mejor.MmPorPx * 1.0001);
                if (mejora) mejor = c;
            }
        }
        _cache = (layout, papel, hojas, evitarCortes, mejor);
        return mejor;
    }

    /// <summary>
    /// Un eje del árbol, de <see cref="A"/> a <see cref="B"/>, con los sitios por los que se puede cortar sin atravesar
    /// ninguna tarjeta (unos cuantos en cada hueco libre) y cuántas líneas cruza cada uno. Se calcula la primera vez que se
    /// necesita y sirve para todas las pruebas.
    /// </summary>
    private sealed class Eje
    {
        public readonly double A, B;
        private readonly List<(double A, double B)> _ocupados;
        private readonly Func<double, int> _cruces;
        private List<double>? _sitios;
        private double[]? _coste;

        public Eje(double a, double b, List<(double A, double B)> ocupados, Func<double, int> cruces)
        {
            A = a; B = b; _ocupados = ocupados; _cruces = cruces;
        }

        public List<double> Sitios
        {
            get
            {
                if (_sitios != null) return _sitios;
                // Huecos libres entre lo ocupado; en cada uno, unos cuantos sitios posibles.
                var unidos = new List<(double A, double B)>();
                foreach (var (x, y) in _ocupados.OrderBy(t => t.A))
                    if (unidos.Count > 0 && x <= unidos[^1].B) unidos[^1] = (unidos[^1].A, Math.Max(unidos[^1].B, y));
                    else unidos.Add((x, y));
                var sitios = new List<double>();
                double desde = A;
                foreach (var (x, y) in unidos.Append((B, B)))
                {
                    if (x > desde + 1)
                        for (int i = 1; i <= 5; i++) sitios.Add(desde + (x - desde) * i / 6);
                    desde = Math.Max(desde, y);
                }
                return _sitios = sitios.Where(x => x > A && x < B).Distinct().OrderBy(x => x).ToList();
            }
        }

        public double[] Coste => _coste ??= Sitios.Select(x => (double)_cruces(x)).ToArray();
    }

    /// <summary>
    /// Dónde cortar el eje en <paramref name="n"/> trozos de como mucho <paramref name="maximo"/> sin atravesar ninguna
    /// tarjeta. Entre las posibilidades, la que cruza menos líneas y deja los trozos más parecidos. Devuelve n + 1 valores
    /// (de A a B), o null si no se puede.
    /// </summary>
    private static double[]? Cortes(Eje eje, int n, double maximo)
    {
        double a = eje.A, b = eje.B;
        if (b - a > n * maximo + 1e-6) return null;
        if (n == 1) return new[] { a, b };
        var sitios = eje.Sitios;
        if (sitios.Count == 0) return null;
        var coste = eje.Coste;
        // Cada trozo, como mucho lo que cabe en una hoja y como poco un 30 % (ninguna hoja casi vacía).
        bool Cabe(double largo) => largo <= maximo && largo >= 0.3 * maximo;

        // Programación dinámica: el corte k (1..n-1) en uno de los sitios, cada trozo de como mucho «maximo».
        int m = sitios.Count;
        var dp = new double[n, m]; var previo = new int[n, m];
        for (int k = 1; k < n; k++)
            for (int j = 0; j < m; j++)
            {
                dp[k, j] = double.PositiveInfinity; previo[k, j] = -1;
                double ideal = a + (b - a) * k / n;
                // cruzar pocas líneas importa, pero más que las hojas queden repartidas (que ninguna se quede casi vacía)
                double propio = 6 * coste[j] + 150 * Math.Pow((sitios[j] - ideal) / maximo, 2);
                if (k == 1)
                {
                    if (Cabe(sitios[j] - a)) dp[k, j] = propio;
                    continue;
                }
                // Solo pueden ir antes los sitios a los que les cabe el trozo hasta j: como los sitios están ordenados, son
                // los de un tramo seguido (el resto no cumple Cabe y nunca se elegiría). Se recorren en el mismo orden.
                int desde = PrimeroQue(sitios, j, i => sitios[j] - sitios[i] <= maximo);
                for (int i = desde; i < j; i++)
                {
                    if (sitios[j] - sitios[i] < 0.3 * maximo) break;       // a partir de aquí, ninguno cabe
                    if (!double.IsPositiveInfinity(dp[k - 1, i]) && Cabe(sitios[j] - sitios[i]) && dp[k - 1, i] + propio < dp[k, j])
                    { dp[k, j] = dp[k - 1, i] + propio; previo[k, j] = i; }
                }
            }
        int fin = -1;
        for (int j = 0; j < m; j++)
            if (Cabe(b - sitios[j]) && !double.IsPositiveInfinity(dp[n - 1, j]) && (fin < 0 || dp[n - 1, j] < dp[n - 1, fin])) fin = j;
        if (fin < 0) return null;
        var res = new double[n + 1];
        res[0] = a; res[n] = b;
        for (int k = n - 1, j = fin; k >= 1; j = previo[k, j], k--) res[k] = sitios[j];
        return res;
    }

    /// <summary>El primer i en [0, j) que cumple <paramref name="cumple"/> (que va de no cumplirse a cumplirse), o j si ninguno.</summary>
    private static int PrimeroQue(List<double> sitios, int j, Func<int, bool> cumple)
    {
        int lo = 0, hi = j;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (cumple(mid)) hi = mid; else lo = mid + 1;
        }
        return lo;
    }
}
