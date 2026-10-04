using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ArbolGenealogico.Core.Layout;

namespace ArbolGenealogico.App;

/// <summary>
/// Guía de impresión: dibuja detrás del árbol una hoja A4 vertical y otra horizontal, cada una del tamaño con el que el árbol
/// entero cabría dentro de sus márgenes, centradas sobre él, con lo que medirían las tarjetas impresas.
/// </summary>
public sealed class GuiaA4 : FrameworkElement
{
    private const double AnchoA4 = 210, AltoA4 = 297, MargenMm = 10;   // milímetros
    private static readonly Color ColorVertical = Color.FromRgb(0x6F, 0xB7, 0xFF);
    private static readonly Color ColorHorizontal = Color.FromRgb(0xFF, 0x9F, 0x5A);
    private LayoutResult? _layout;

    public GuiaA4() { IsHitTestVisible = false; }

    public LayoutResult? Layout
    {
        get => _layout;
        set { _layout = value; InvalidateVisual(); }
    }

    /// <summary>Una hoja colocada sobre el árbol: su rectángulo (en coordenadas del árbol) y los milímetros por píxel.</summary>
    public readonly record struct Hoja(bool Vertical, Rect Papel, double MmPorPx);

    /// <summary>Las dos hojas (vertical y horizontal) ajustadas al árbol, o ninguna si no hay árbol.</summary>
    public static List<Hoja> Hojas(LayoutResult? layout)
    {
        var res = new List<Hoja>();
        if (layout == null || layout.Cartas.Count == 0) return res;
        var o = layout.Opciones;
        double minX = layout.Cartas.Values.Min(c => c.X) - o.AnchoCarta / 2, maxX = layout.Cartas.Values.Max(c => c.X) + o.AnchoCarta / 2;
        double minY = layout.Cartas.Values.Min(c => c.Y), maxY = layout.Cartas.Values.Max(c => c.Y) + o.AltoCarta;
        // un poco de holgura alrededor de las tarjetas (sombras, brillo de la línea directa)
        minX -= 12; maxX += 12; minY -= 12; maxY += 12;
        double w = maxX - minX, h = maxY - minY, cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        foreach (bool vertical in new[] { true, false })
        {
            double pw = vertical ? AnchoA4 : AltoA4, ph = vertical ? AltoA4 : AnchoA4;
            double mm = Math.Min((pw - 2 * MargenMm) / w, (ph - 2 * MargenMm) / h);
            double anchoPx = pw / mm, altoPx = ph / mm;
            res.Add(new Hoja(vertical, new Rect(cx - anchoPx / 2, cy - altoPx / 2, anchoPx, altoPx), mm));
        }
        return res;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var hojas = Hojas(_layout);
        if (hojas.Count == 0) return;
        double mejor = hojas.Max(x => x.MmPorPx);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        // primero la más grande, para que la otra quede encima
        foreach (var hoja in hojas.OrderByDescending(x => x.Papel.Width * x.Papel.Height))
        {
            var color = hoja.Vertical ? ColorVertical : ColorHorizontal;
            var r = hoja.Papel;
            double grosor = Math.Max(r.Width, r.Height) / 450;
            var borde = new Pen(new SolidColorBrush(Color.FromArgb(0xC0, color.R, color.G, color.B)), grosor)
            {
                DashStyle = new DashStyle(new double[] { 6, 4 }, 0), LineJoin = PenLineJoin.Round,
            };
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x0E, color.R, color.G, color.B)), borde, r);
            // zona imprimible (dentro de los márgenes), apenas insinuada
            double m = MargenMm / hoja.MmPorPx;
            var util = new Rect(r.X + m, r.Y + m, Math.Max(0, r.Width - 2 * m), Math.Max(0, r.Height - 2 * m));
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)), grosor * 0.6), util);

            // Rótulo en el margen: arriba en la vertical y abajo en la horizontal (así no se pisan).
            double cm = (_layout!.Opciones.AnchoCarta * hoja.MmPorPx) / 10;
            string texto = $"A4 {(hoja.Vertical ? "vertical" : "horizontal")}  ·  tarjetas de {cm.ToString("0.0", CultureInfo.GetCultureInfo("es-ES"))} cm"
                         + (Math.Abs(hoja.MmPorPx - mejor) < 1e-9 && hojas.Count > 1 ? "  ·  la que más aprovecha la hoja" : "");
            double tam = m * 0.45;
            var ft = new FormattedText(texto, CultureInfo.GetCultureInfo("es-ES"), FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                tam, new SolidColorBrush(Color.FromArgb(0xE0, color.R, color.G, color.B)), dpi);
            double y = hoja.Vertical ? r.Y + (m - ft.Height) / 2 : r.Bottom - m + (m - ft.Height) / 2;
            dc.DrawText(ft, new Point(r.X + m, y));
        }
    }
}
