using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ArbolGenealogico.Core.Layout;

namespace ArbolGenealogico.App;

/// <summary>
/// Guía de impresión: dibuja detrás del árbol la mejor forma de imprimirlo en el número de hojas elegido de un tamaño de
/// papel (A4 a A1): hojas en vertical u horizontal, en fila, en columna o en cuadrícula, que se pegan recortando sus márgenes
/// interiores. Se elige la combinación con la que el árbol sale más grande y se marcan los cortes entre hojas.
/// </summary>
public sealed class GuiaImpresion : FrameworkElement
{
    /// <summary>
    /// Margen de cada hoja, en el que no se imprime el árbol: las impresoras no llegan al borde, y es la tira por la que se
    /// pegan las hojas (la de al lado se recorta por su línea de corte y se pone encima de este margen).
    /// </summary>
    public const double MargenMm = 12;
    /// <summary>Las marcas de la guía, sobre el papel blanco: un azul bien visible.</summary>
    private static readonly Color ColorGuia = Color.FromRgb(0x2F, 0x7E, 0xD8);
    /// <summary>Lo más que se puede repartir el árbol: 8 hojas.</summary>
    public const int MaxHojas = 8;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private LayoutResult? _layout;
    private string _papel = "A4";
    private int _hojas = 1;

    public GuiaImpresion() { IsHitTestVisible = false; }

    public LayoutResult? Layout { get => _layout; set { _layout = value; InvalidateVisual(); } }
    /// <summary>Tamaño de la letra de los nombres en las tarjetas (px), para decir cómo de grande sale impresa.</summary>
    public double LetraNombre { get; set; } = 14.5;
    /// <summary>"A4", "A3", "A2" o "A1".</summary>
    public string Papel { get => _papel; set { _papel = value; InvalidateVisual(); } }
    /// <summary>De 1 a <see cref="MaxHojas"/>.</summary>
    public int Hojas { get => _hojas; set { _hojas = Math.Clamp(value, 1, MaxHojas); InvalidateVisual(); } }
    /// <summary>Buscar para los cortes entre hojas huecos sin tarjetas; si no, hojas iguales al tamaño máximo.</summary>
    public bool EvitarCortes { get => _evitar; set { _evitar = value; InvalidateVisual(); } }
    private bool _evitar = true;

    /// <summary>Tamaño (ancho × alto en vertical, en mm) de cada papel.</summary>
    public static (double W, double H) Medidas(string papel) => papel switch
    {
        "A1" => (594, 841), "A2" => (420, 594), "A3" => (297, 420), _ => (210, 297),
    };

    /// <summary>
    /// Una forma de imprimir: hojas en vertical u horizontal, en <paramref name="Filas"/> × <paramref name="Columnas"/>, los
    /// milímetros por píxel del árbol y dónde se corta el árbol entre hojas (<paramref name="CortesX"/>: Columnas + 1 valores,
    /// del borde izquierdo del contenido al derecho; <paramref name="CortesY"/>: igual en vertical). Cada hoja imprime su trozo
    /// desde la esquina de su zona imprimible; si es más pequeño que ella, sobra margen por la derecha o por abajo.
    /// <paramref name="TarjetasCortadas"/>: cuántas tarjetas atraviesa algún corte (normalmente ninguna).
    /// </summary>
    public sealed record Configuracion(string Papel, bool Vertical, int Filas, int Columnas, double MmPorPx,
        double[] CortesX, double[] CortesY, int TarjetasCortadas, bool SePidioNoCortar = false)
    {
        public int Hojas => Filas * Columnas;
        /// <summary>Tamaño de cada hoja en mm, ya girada si va en horizontal.</summary>
        public double AnchoHojaMm => Vertical ? Medidas(Papel).W : Medidas(Papel).H;
        public double AltoHojaMm => Vertical ? Medidas(Papel).H : Medidas(Papel).W;
        /// <summary>Zona imprimible de una hoja, en píxeles del árbol.</summary>
        public double AnchoUtilPx => (AnchoHojaMm - 2 * MargenMm) / MmPorPx;
        public double AltoUtilPx => (AltoHojaMm - 2 * MargenMm) / MmPorPx;

        /// <summary>Lo que se imprime en la hoja de esa fila y columna (en coordenadas del árbol).</summary>
        public Rect Pieza(int fila, int columna) =>
            new(CortesX[columna], CortesY[fila], CortesX[columna + 1] - CortesX[columna], CortesY[fila + 1] - CortesY[fila]);

        /// <summary>Todo lo que se imprime (en coordenadas del árbol).</summary>
        public Rect Util => new(CortesX[0], CortesY[0], CortesX[^1] - CortesX[0], CortesY[^1] - CortesY[0]);

        /// <summary>El papel de todas las hojas ya pegadas (en coordenadas del árbol).</summary>
        public Rect Pegado
        {
            get
            {
                double m = MargenMm / MmPorPx;
                double izquierda = CortesX[0] - m, arriba = CortesY[0] - m;
                double derecha = CortesX[^2] + AnchoUtilPx + m, abajo = CortesY[^2] + AltoUtilPx + m;
                return new Rect(izquierda, arriba, derecha - izquierda, abajo - arriba);
            }
        }

        /// <summary>Tamaño de la letra, en puntos (como en Word), con el que sale impreso un texto de <paramref name="px"/> del árbol.</summary>
        public double Puntos(double px) => px * MmPorPx / (25.4 / 72);

        /// <summary>Qué tal se lee una letra impresa de ese tamaño (un libro usa 10 a 12 pt).</summary>
        public static string Legibilidad(double puntos) =>
            puntos < 4 ? "muy pequeña" : puntos < 6 ? "pequeña" : puntos < 8 ? "se lee bien" : "cómoda de leer";

        /// <param name="letraNombre">Tamaño de la letra de los nombres en el árbol (px).</param>
        public string Descripcion(double letraNombre)
        {
            string hojas = Hojas == 1 ? $"1 hoja {Papel} {(Vertical ? "vertical" : "horizontal")}"
                : $"{Hojas} hojas {Papel} {(Vertical ? "verticales" : "horizontales")}";
            string forma = Hojas == 1 ? "" : Filas == 1 ? ", una al lado de otra" : Columnas == 1 ? ", una debajo de otra" : $", en {Filas} filas de {Columnas}";
            double pt = Puntos(letraNombre);
            string s = TarjetasCortadas == 1 ? "" : "s";
            string cortes = TarjetasCortadas == 0 ? ""
                : SePidioNoCortar ? $"  ·  con estas hojas no se puede evitar cortar {TarjetasCortadas} tarjeta{s}"
                : $"  ·  los cortes atraviesan {TarjetasCortadas} tarjeta{s}";
            return $"{hojas}{forma}  ·  nombres en letra de {pt.ToString(pt < 10 ? "0.0" : "0", Es)} pt ({Legibilidad(pt)}){cortes}";
        }
    }

    private static (LayoutResult L, string P, int H, bool E, Configuracion? C)? _cache;

    /// <summary>
    /// La mejor forma de imprimir el árbol en ese número de hojas de ese papel, o null si no hay árbol: entre todas las
    /// colocaciones de las hojas, la que deja el árbol más grande con los cortes entre hojas por sitios sin tarjetas.
    /// </summary>
    public static Configuracion? Mejor(LayoutResult? layout, string papel, int hojas, bool evitarCortes = true)
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

        var (aw, ah) = Medidas(papel);
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

    protected override void OnRender(DrawingContext dc)
    {
        if (Mejor(_layout, _papel, _hojas, _evitar) is not { } c) return;
        var r = c.Pegado;
        // grosor según el tamaño de una hoja (no del conjunto), para que no engorde al juntar varias
        double grosor = Math.Max(r.Width / c.Columnas, r.Height / c.Filas) / 500;
        var color = ColorGuia;
        // El papel (las hojas ya pegadas), blanco y con una sombra suave, sobre la mesa.
        for (int i = 3; i >= 1; i--)
        {
            var sombra = r; sombra.Offset(grosor * i * 1.5, grosor * i * 2.5); sombra.Inflate(grosor * i, grosor * i);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0)), null, sombra);
        }
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD4)), grosor * 0.5), r);
        var fino = new Pen(new SolidColorBrush(Color.FromArgb(0x48, color.R, color.G, color.B)), grosor * 0.6);
        dc.DrawRectangle(null, fino, c.Util);

        // Cortes entre hojas, de lado a lado de la zona impresa.
        // bien visibles: por ahí se juntan las hojas
        var corte = new Pen(new SolidColorBrush(Color.FromArgb(0xD8, color.R, color.G, color.B)), grosor * 1.4)
        {
            DashStyle = new DashStyle(new double[] { 4, 3 }, 0),
        };
        for (int i = 1; i < c.Columnas; i++) dc.DrawLine(corte, new Point(c.CortesX[i], r.Top), new Point(c.CortesX[i], r.Bottom));
        for (int i = 1; i < c.Filas; i++) dc.DrawLine(corte, new Point(r.Left, c.CortesY[i]), new Point(r.Right, c.CortesY[i]));

        // Rótulo en el margen de arriba.
        double margen = MargenMm / c.MmPorPx;
        var ft = new FormattedText(c.Descripcion(LetraNombre), Es, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            margen * 0.45, new SolidColorBrush(Color.FromArgb(0xE0, color.R, color.G, color.B)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(r.X + margen, r.Y + (margen - ft.Height) / 2));
    }
}
