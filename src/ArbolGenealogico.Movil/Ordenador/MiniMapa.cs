using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ArbolGenealogico.Movil.Dibujo;
using ArbolGenealogico.Movil.Vistas;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>
/// El árbol entero en pequeño, con un recuadro donde está la vista. Pulsar o arrastrar en él lleva la vista a ese sitio.
/// </summary>
public sealed class MiniMapa : Control
{
    private readonly LienzoArbol _lienzo;

    public MiniMapa(LienzoArbol lienzo)
    {
        _lienzo = lienzo;
        // el lienzo avisa mientras dibuja: el minimapa se vuelve a dibujar después, no en medio
        _lienzo.Dibujado += () => Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual, Avalonia.Threading.DispatcherPriority.Render);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>La escala y el desplazamiento con los que el árbol entero cabe en el minimapa.</summary>
    private (double K, double X, double Y)? Encaje()
    {
        if (_lienzo.Layout is not { Ancho: > 0, Alto: > 0 } lay || Bounds.Width <= 0) return null;
        const double margen = 8;
        double k = Math.Min((Bounds.Width - 2 * margen) / lay.Ancho, (Bounds.Height - 2 * margen) / lay.Alto);
        return (k, (Bounds.Width - lay.Ancho * k) / 2, (Bounds.Height - lay.Alto * k) / 2);
    }

    public override void Render(DrawingContext ctx)
    {
        var fondo = new RoundedRect(new Rect(Bounds.Size), 12);
        ctx.DrawRectangle(Ui.Panel, new Pen(Ui.Linea, 1), fondo);
        if (Encaje() is not var (k, x0, y0) || _lienzo.Layout is not { } lay) return;
        var o = lay.Opciones;
        using (ctx.PushClip(fondo))
        {
            var tarjeta = new SolidColorBrush(Tema.Actual.Suave, 0.55);
            var directa = new SolidColorBrush(Tema.Actual.Linaje, 0.9);
            var linea = _lienzo.Arbol?.LineaDirecta() ?? new HashSet<string>();
            foreach (var (id, c) in lay.Cartas)
                ctx.FillRectangle(linea.Contains(id) ? directa : tarjeta,
                    new Rect(x0 + (c.X - o.AnchoCarta / 2) * k, y0 + c.Y * k, Math.Max(1.5, o.AnchoCarta * k), Math.Max(1.5, o.AltoCarta * k)));
            if (_lienzo.Seleccion is { } sel && _lienzo.RectDe(sel) is { } rs)
                ctx.FillRectangle(Ui.Oro, new Rect(x0 + rs.X * k - 1, y0 + rs.Y * k - 1, Math.Max(3, rs.Width * k) + 2, Math.Max(3, rs.Height * k) + 2));
            var v = _lienzo.Visible;
            // recortado al minimapa, para que se vean sus cuatro lados aunque la vista se salga del árbol
            var marco = new Rect(x0 + v.X * k, y0 + v.Y * k, v.Width * k, v.Height * k).Intersect(new Rect(Bounds.Size).Deflate(2));
            ctx.DrawRectangle(new SolidColorBrush(Tema.Actual.Destacado, 0.12), new Pen(Ui.Oro, 1.6), marco);
        }
    }

    private bool _arrastrando;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _arrastrando = true;
        e.Pointer.Capture(this);
        Ir(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_arrastrando) Ir(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _arrastrando = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _lienzo.Zoom(Math.Pow(1.18, e.Delta.Y));
        e.Handled = true;
    }

    /// <summary>Pone en el centro de la vista el punto del árbol que hay bajo ese punto del minimapa.</summary>
    private void Ir(Point p)
    {
        if (Encaje() is not var (k, x0, y0)) return;
        double mx = (p.X - x0) / k, my = (p.Y - y0) / k, e = _lienzo.Escala;
        _lienzo.IrA(e, _lienzo.Bounds.Width / 2 - mx * e, _lienzo.Bounds.Height / 2 - my * e, false);
    }
}
