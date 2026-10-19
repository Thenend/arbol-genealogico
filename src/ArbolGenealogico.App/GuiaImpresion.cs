using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.App;

/// <summary>
/// Guía de impresión: dibuja detrás del árbol la mejor forma de imprimirlo (<see cref="PlanImpresion.Mejor"/>) en el número
/// de hojas elegido de un tamaño de papel (A4 a A1, o los de Estados Unidos), con los cortes entre hojas.
/// </summary>
public sealed class GuiaImpresion : FrameworkElement
{
    /// <summary>Las marcas de la guía, sobre el papel blanco: un azul bien visible.</summary>
    private static readonly Color ColorGuia = Color.FromRgb(0x2F, 0x7E, 0xD8);
    public const double MargenMm = PlanImpresion.MargenMm;
    public const int MaxHojas = PlanImpresion.MaxHojas;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private LayoutResult? _layout;
    private Papel _papel = Papel.A4;
    private int _hojas = 1;

    public GuiaImpresion() { IsHitTestVisible = false; }

    public LayoutResult? Layout { get => _layout; set { _layout = value; InvalidateVisual(); } }
    /// <summary>Tamaño de la letra de los nombres en las tarjetas (px), para decir cómo de grande sale impresa.</summary>
    public double LetraNombre { get; set; } = 14.5;
    public Papel Papel { get => _papel; set { _papel = value; InvalidateVisual(); } }
    /// <summary>De 1 a <see cref="MaxHojas"/>.</summary>
    public int Hojas { get => _hojas; set { _hojas = Math.Clamp(value, 1, MaxHojas); InvalidateVisual(); } }
    /// <summary>Buscar para los cortes entre hojas huecos sin tarjetas; si no, hojas iguales al tamaño máximo.</summary>
    public bool EvitarCortes { get => _evitar; set { _evitar = value; InvalidateVisual(); } }
    private bool _evitar = true;

    /// <summary>Un rectángulo del cálculo, en WPF.</summary>
    public static Rect R(Caja c) => new(c.X, c.Y, c.Ancho, c.Alto);

    protected override void OnRender(DrawingContext dc)
    {
        if (PlanImpresion.Mejor(_layout, _papel, _hojas, _evitar) is not { } c) return;
        var r = R(c.Pegado);
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
        dc.DrawRectangle(null, fino, R(c.Util));

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
