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
    private static readonly Color ColorGuia = Color.FromRgb(0x6F, 0xB7, 0xFF);
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private LayoutResult? _layout;
    private string _papel = "A4";
    private int _hojas = 1;

    public GuiaImpresion() { IsHitTestVisible = false; }

    public LayoutResult? Layout { get => _layout; set { _layout = value; InvalidateVisual(); } }
    /// <summary>"A4", "A3", "A2" o "A1".</summary>
    public string Papel { get => _papel; set { _papel = value; InvalidateVisual(); } }
    /// <summary>De 1 a 4.</summary>
    public int Hojas { get => _hojas; set { _hojas = Math.Clamp(value, 1, 4); InvalidateVisual(); } }

    /// <summary>Tamaño (ancho × alto en vertical, en mm) de cada papel.</summary>
    public static (double W, double H) Medidas(string papel) => papel switch
    {
        "A1" => (594, 841), "A2" => (420, 594), "A3" => (297, 420), _ => (210, 297),
    };

    /// <summary>
    /// Una forma de imprimir: hojas en vertical u horizontal, en <paramref name="Filas"/> × <paramref name="Columnas"/>;
    /// el papel una vez pegado (en coordenadas del árbol), su zona impresa y los milímetros por píxel del árbol.
    /// </summary>
    public readonly record struct Configuracion(string Papel, bool Vertical, int Filas, int Columnas, double MmPorPx, Rect Pegado, Rect Util)
    {
        public int Hojas => Filas * Columnas;
        /// <summary>Tamaño de cada hoja en mm, ya girada si va en horizontal.</summary>
        public double AnchoHojaMm => Vertical ? Medidas(Papel).W : Medidas(Papel).H;
        public double AltoHojaMm => Vertical ? Medidas(Papel).H : Medidas(Papel).W;

        /// <summary>Lo que se imprime en la hoja de esa fila y columna (en coordenadas del árbol).</summary>
        public Rect Pieza(int fila, int columna)
        {
            double w = Util.Width / Columnas, h = Util.Height / Filas;
            return new Rect(Util.X + columna * w, Util.Y + fila * h, w, h);
        }

        public string Descripcion(double anchoCarta)
        {
            string hojas = Hojas == 1 ? $"1 hoja {Papel} {(Vertical ? "vertical" : "horizontal")}"
                : $"{Hojas} hojas {Papel} {(Vertical ? "verticales" : "horizontales")}";
            string forma = Hojas == 1 ? "" : Filas == 1 ? ", una al lado de otra" : Columnas == 1 ? ", una debajo de otra" : $", en {Filas} filas de {Columnas}";
            double cm = anchoCarta * MmPorPx / 10;
            return $"{hojas}{forma}  ·  tarjetas de {cm.ToString("0.0", Es)} cm";
        }
    }

    /// <summary>La mejor forma de imprimir el árbol en ese número de hojas de ese papel, o null si no hay árbol.</summary>
    public static Configuracion? Mejor(LayoutResult? layout, string papel, int hojas)
    {
        if (layout == null || layout.Cartas.Count == 0) return null;
        var o = layout.Opciones;
        double minX = layout.Cartas.Values.Min(c => c.X) - o.AnchoCarta / 2 - 12, maxX = layout.Cartas.Values.Max(c => c.X) + o.AnchoCarta / 2 + 12;
        double minY = layout.Cartas.Values.Min(c => c.Y) - 12, maxY = layout.Cartas.Values.Max(c => c.Y) + o.AltoCarta + 12;
        double w = maxX - minX, h = maxY - minY, cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
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
                double utilW = cols * (pw - 2 * MargenMm), utilH = filas * (ph - 2 * MargenMm);
                double mm = Math.Min(utilW / w, utilH / h);
                if (mejor is { } m && mm <= m.MmPorPx * 1.0001) continue;
                double uw = utilW / mm, uh = utilH / mm, margen = MargenMm / mm;
                var util = new Rect(cx - uw / 2, cy - uh / 2, uw, uh);
                var pegado = new Rect(util.X - margen, util.Y - margen, uw + 2 * margen, uh + 2 * margen);
                mejor = new Configuracion(papel, vertical, filas, cols, mm, pegado, util);
            }
        }
        return mejor;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Mejor(_layout, _papel, _hojas) is not { } c) return;
        var r = c.Pegado;
        // grosor según el tamaño de una hoja (no del conjunto), para que no engorde al juntar varias
        double grosor = Math.Max(r.Width / c.Columnas, r.Height / c.Filas) / 500;
        var color = ColorGuia;
        var borde = new Pen(new SolidColorBrush(Color.FromArgb(0xC0, color.R, color.G, color.B)), grosor)
        {
            DashStyle = new DashStyle(new double[] { 6, 4 }, 0), LineJoin = PenLineJoin.Round,
        };
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x0E, color.R, color.G, color.B)), borde, r);
        var fino = new Pen(new SolidColorBrush(Color.FromArgb(0x48, color.R, color.G, color.B)), grosor * 0.6);
        dc.DrawRectangle(null, fino, c.Util);

        // Cortes entre hojas, de lado a lado de la zona impresa.
        var corte = new Pen(new SolidColorBrush(Color.FromArgb(0xA0, color.R, color.G, color.B)), grosor * 0.8)
        {
            DashStyle = new DashStyle(new double[] { 3, 3 }, 0),
        };
        for (int i = 1; i < c.Columnas; i++)
        {
            double x = c.Util.X + c.Util.Width * i / c.Columnas;
            dc.DrawLine(corte, new Point(x, r.Top), new Point(x, r.Bottom));
        }
        for (int i = 1; i < c.Filas; i++)
        {
            double y = c.Util.Y + c.Util.Height * i / c.Filas;
            dc.DrawLine(corte, new Point(r.Left, y), new Point(r.Right, y));
        }

        // Rótulo en el margen de arriba.
        double margen = MargenMm / c.MmPorPx;
        var ft = new FormattedText(c.Descripcion(_layout!.Opciones.AnchoCarta), Es, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            margen * 0.45, new SolidColorBrush(Color.FromArgb(0xE0, color.R, color.G, color.B)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(r.X + margen, r.Y + (margen - ft.Height) / 2));
    }
}
