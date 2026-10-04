using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ArbolGenealogico.Core.Layout;

namespace ArbolGenealogico.App;

/// <summary>Minimapa: tarjetas en miniatura y rectángulo de la zona visible. Clic o arrastre para moverse.</summary>
public sealed class MiniMapa : FrameworkElement
{
    private VistaArbol? _vista;
    private bool _arrastrando;

    public MiniMapa()
    {
        Cursor = Cursors.Hand;
        ClipToBounds = true;
    }

    public VistaArbol? Vista
    {
        get => _vista;
        set
        {
            if (_vista != null) _vista.CamaraCambiada -= InvalidateVisual;
            _vista = value;
            if (_vista != null) _vista.CamaraCambiada += InvalidateVisual;
            InvalidateVisual();
        }
    }

    private (double escala, double ox, double oy)? Transformacion()
    {
        var l = _vista?.Layout;
        if (l == null || l.Ancho <= 0 || ActualWidth <= 0) return null;
        const double m = 10;
        double s = Math.Min((ActualWidth - 2 * m) / l.Ancho, (ActualHeight - 2 * m) / l.Alto);
        return (s, (ActualWidth - l.Ancho * s) / 2, (ActualHeight - l.Alto * s) / 2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var fondo = new SolidColorBrush(Color.FromArgb(0xE6, 0x13, 0x16, 0x1E));
        var borde = new Pen(new SolidColorBrush(Color.FromRgb(0x2C, 0x31, 0x40)), 1);
        dc.DrawRoundedRectangle(fondo, borde, new Rect(0.5, 0.5, ActualWidth - 1, ActualHeight - 1), 12, 12);
        var tr = Transformacion();
        if (tr == null || _vista?.Layout == null) return;
        var (s, ox, oy) = tr.Value;
        var l = _vista.Layout; var o = l.Opciones;

        foreach (var c in l.Cartas.Values)
        {
            var color = c.Directa ? Color.FromRgb(0xF5, 0xC4, 0x51) : c.Politica ? Color.FromRgb(0x5A, 0x60, 0x72) : Color.FromRgb(0x6E, 0x74, 0x88);
            var persona = _vista.Arbol?.Buscar(c.Id);
            bool sinNombre = persona == null || (string.IsNullOrWhiteSpace(persona.Nombre) && string.IsNullOrWhiteSpace(persona.Apellidos));
            if (!c.Directa && !sinNombre)
                color = persona!.Sexo switch
                {
                    Core.Model.Sexo.Hombre => Color.FromRgb(0x4C, 0x75, 0xC8),
                    Core.Model.Sexo.Mujer => Color.FromRgb(0xC0, 0x5C, 0x96),
                    _ => Color.FromRgb(0x86, 0x6C, 0xD2),
                };
            double w = Math.Max(3, o.AnchoCarta * s), h = Math.Max(2.5, o.AltoCarta * s);
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null,
                new Rect(ox + (c.X - o.AnchoCarta / 2) * s, oy + c.Y * s, w, h), 1.5, 1.5);
        }

        var v = _vista.Visible;
        var r = new Rect(ox + v.X * s, oy + v.Y * s, v.Width * s, v.Height * s);
        r.Intersect(new Rect(1, 1, ActualWidth - 2, ActualHeight - 2));
        if (!r.IsEmpty)
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xF5, 0xC4, 0x51)), 1.6), r, 3, 3);
    }

    private void IrA(Point p, bool animar)
    {
        var tr = Transformacion();
        if (tr == null || _vista == null) return;
        var (s, ox, oy) = tr.Value;
        _vista.CentrarEnPunto((p.X - ox) / s, (p.Y - oy) / s, animar);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _arrastrando = true; CaptureMouse();
        IrA(e.GetPosition(this), true);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_arrastrando && e.LeftButton == MouseButtonState.Pressed) IrA(e.GetPosition(this), false);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _arrastrando = false; ReleaseMouseCapture();
    }
}
