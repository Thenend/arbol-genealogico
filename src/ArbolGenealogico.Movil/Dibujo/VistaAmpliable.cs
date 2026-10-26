using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// Una vista previa de papel que se amplía con dos dedos (o la rueda del ratón) y se mueve con uno, para ver de cerca cómo
/// saldrá; dos toques seguidos alternan entre verlo entero y verlo de cerca. Las hijas dicen qué papel hay
/// (<see cref="Papel"/>, en sus unidades) y lo dibujan con la escala <see cref="K"/> y el origen (<see cref="Ox"/>,
/// <see cref="Oy"/>).
/// </summary>
public abstract class VistaAmpliable : Control, Avalonia.Rendering.ICustomHitTest
{
    protected const double Margen = 14;

    /// <summary>Píxeles de la pantalla por unidad del papel, y dónde cae el origen del papel.</summary>
    private double _k = 1, _ox, _oy;
    /// <summary>Viéndolo entero (al cambiar de papel se vuelve a encajar); si no, ampliado por la persona.</summary>
    private bool _entera = true;

    protected double K => _k;
    protected double Ox => _ox;
    protected double Oy => _oy;

    /// <summary>El papel, en sus unidades; null si aún no hay nada que ver.</summary>
    protected abstract Rect? Papel { get; }
    /// <summary>Lo más cerca que se puede ver (en píxeles por unidad).</summary>
    protected abstract double EscalaMax { get; }
    /// <summary>La escala del doble toque: a un tamaño en el que se lee bien en el teléfono.</summary>
    protected abstract double EscalaCerca { get; }

    /// <summary>Cuando pasa de verlo entero a verlo ampliado, o al revés.</summary>
    public event Action? AmpliacionCambiada;
    public bool Ampliada => !_entera;

    protected VistaAmpliable() { ClipToBounds = true; }

    public bool HitTest(Point p) => new Rect(Bounds.Size).Contains(p);

    /// <summary>Tras cambiar lo que se ve: ampliada, se queda donde estaba (para ver cómo cambia lo mismo con otros
    /// colores o papel); si no, entera.</summary>
    protected void Rehecho()
    {
        if (_entera) Encajar(); else Limitar();
        InvalidateVisual();
    }

    /// <summary>La escala con la que se ve entero.</summary>
    private double EscalaEntera
    {
        get
        {
            if (Papel is not { } p || Bounds.Width <= 0) return 1;
            return Math.Max(1e-4, Math.Min((Bounds.Width - 2 * Margen) / p.Width, (Bounds.Height - 2 * Margen) / p.Height));
        }
    }

    /// <summary>Vuelve a verlo entero.</summary>
    public void Encajar()
    {
        bool antes = _entera;
        _entera = true;
        if (Papel is { } p && Bounds.Width > 0)
        {
            _k = EscalaEntera;
            _ox = (Bounds.Width - p.Width * _k) / 2 - p.X * _k;
            _oy = (Bounds.Height - p.Height * _k) / 2 - p.Y * _k;
        }
        InvalidateVisual();
        if (!antes) AmpliacionCambiada?.Invoke();
    }

    /// <summary>Amplía (o reduce) por un factor alrededor de un punto de la vista.</summary>
    public void Ampliar(double factor, Point centro)
    {
        if (Papel == null) return;
        double minima = EscalaEntera, nueva = Math.Clamp(_k * factor, minima, Math.Max(minima, EscalaMax));
        _ox = centro.X - (centro.X - _ox) * nueva / _k;
        _oy = centro.Y - (centro.Y - _oy) * nueva / _k;
        _k = nueva;
        if (_k <= minima * 1.001) { Encajar(); return; }
        Movida();
    }

    /// <summary>Mueve lo que se ve, como al arrastrar con el dedo.</summary>
    public void Mover(double dx, double dy)
    {
        if (_entera) return;
        _ox += dx; _oy += dy;
        Movida();
    }

    private void Movida()
    {
        bool antes = _entera;
        _entera = false;
        Limitar();
        InvalidateVisual();
        if (antes) AmpliacionCambiada?.Invoke();
    }

    /// <summary>Que el papel no se pierda de vista: si no cabe, ocupa toda la vista; si cabe, se queda dentro.</summary>
    private void Limitar()
    {
        if (Papel is not { } p || Bounds.Width <= 0) return;
        _k = Math.Max(_k, EscalaEntera);
        (_ox, _oy) = (Eje(_ox, p.X, p.Width, Bounds.Width), Eje(_oy, p.Y, p.Height, Bounds.Height));

        double Eje(double o, double inicio, double largo, double vista)
        {
            double a = inicio * _k + o, l = largo * _k;          // dónde empieza el papel en la vista, y lo que mide
            if (l + 2 * Margen <= vista) return (vista - l) / 2 - inicio * _k;
            return o + Math.Clamp(a, vista - Margen - l, Margen) - a;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_entera) Encajar();
        else
        {
            // al cambiar de tamaño (al girar el teléfono), el centro sigue en el centro
            _ox += (e.NewSize.Width - e.PreviousSize.Width) / 2;
            _oy += (e.NewSize.Height - e.PreviousSize.Height) / 2;
            Limitar();
        }
    }

    // ---------- Gestos ----------

    private readonly Dictionary<long, Point> _dedos = new();
    private Point _inicio, _ultimoToqueP;
    private DateTime _inicioT, _ultimoToque;
    private bool _movido;
    private double _pinchDist, _pinchK, _pinchOx, _pinchOy;
    private Point _pinchCentro;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        _dedos[e.Pointer.Id] = p;
        e.Pointer.Capture(this);
        if (_dedos.Count == 1) { _inicio = p; _inicioT = DateTime.UtcNow; _movido = false; }
        else if (_dedos.Count == 2) { _movido = true; EmpezarPinch(); }
        e.Handled = true;
    }

    private void EmpezarPinch()
    {
        var ps = _dedos.Values.Take(2).ToArray();
        _pinchDist = Math.Max(1, Dist(ps[0], ps[1]));
        _pinchCentro = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
        _pinchK = _k; _pinchOx = _ox; _pinchOy = _oy;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dedos.TryGetValue(e.Pointer.Id, out var previo)) return;
        var p = e.GetPosition(this);
        _dedos[e.Pointer.Id] = p;
        if (_dedos.Count >= 2)
        {
            // el punto que estaba bajo el centro de los dos dedos sigue bajo él
            var ps = _dedos.Values.Take(2).ToArray();
            var c = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
            double wx = (_pinchCentro.X - _pinchOx) / _pinchK, wy = (_pinchCentro.Y - _pinchOy) / _pinchK;
            double minima = EscalaEntera;
            _k = Math.Clamp(_pinchK * Dist(ps[0], ps[1]) / _pinchDist, minima, Math.Max(minima, EscalaMax));
            _ox = c.X - wx * _k; _oy = c.Y - wy * _k;
            if (_k <= minima * 1.001 && _entera) { Limitar(); InvalidateVisual(); return; }
            Movida();
            return;
        }
        if (!_movido)
        {
            if (Dist(p, _inicio) < (e.Pointer.Type == PointerType.Mouse ? 4 : 12)) return;
            _movido = true;
            previo = _inicio;
        }
        Mover(p.X - previo.X, p.Y - previo.Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dedos.Remove(e.Pointer.Id)) return;
        if (_dedos.Count > 0) return;
        e.Pointer.Capture(null);
        // al soltar del todo un pellizco que lo dejó casi entero, se encaja
        if (!_entera && _k <= EscalaEntera * 1.02) Encajar();
        if (_movido || (DateTime.UtcNow - _inicioT).TotalMilliseconds > 500) return;
        var p = e.GetPosition(this);
        var ahora = DateTime.UtcNow;
        bool doble = (ahora - _ultimoToque).TotalMilliseconds < 380 && Dist(p, _ultimoToqueP) < 40;
        _ultimoToque = doble ? default : ahora; _ultimoToqueP = p;
        if (doble) AlternarAmpliacion(p);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dedos.Remove(e.Pointer.Id);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Ampliar(Math.Pow(1.18, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>Dos toques: de verlo entero, a verlo de cerca en el punto tocado; y de cerca, otra vez entero.</summary>
    public void AlternarAmpliacion(Point p)
    {
        if (!_entera) { Encajar(); return; }
        Ampliar(Math.Max(EscalaCerca, EscalaEntera * 2) / _k, p);
        // el punto tocado, al centro
        Mover(Bounds.Width / 2 - p.X, Bounds.Height / 2 - p.Y);
    }

    private static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
