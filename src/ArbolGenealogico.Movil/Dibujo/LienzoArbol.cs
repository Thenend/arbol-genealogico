using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// El árbol en la pantalla del móvil: lo dibuja entero en un solo control (tarjetas y líneas), y se mueve con un dedo y
/// se amplía con dos. Tocar una tarjeta la selecciona (se ilumina su linaje), tocarla otra vez pide su ficha y
/// mantenerla pulsada pide el menú de añadir familiares.
/// </summary>
public sealed class LienzoArbol : Control
{
    public const double EscalaMin = 0.03, EscalaMax = 3;

    private Arbol? _arbol;
    private LayoutResult? _layout;
    private FormaTarjeta _forma = FormaTarjeta.Ancha, _formaEnUso;
    private Ordenacion _ordenacion = Ordenacion.C;
    private string? _sel;
    private HashSet<string> _coincidencias = new();
    private HashSet<string> _directa = new();
    private Resaltado _res = Resaltado.Ninguno;
    private readonly Dictionary<string, Tarjeta> _tarjetas = new();
    private readonly Dictionary<Conexion, Geometry> _caminos = new();
    private double _e = 1, _tx, _ty;
    private bool _encuadrarPendiente;

    public event Action<string?>? SeleccionCambiada;
    public event Action<string>? FichaPedida;
    public event Action<string>? MenuPedido;
    public event Action<string>? EnlacePedido;

    public LienzoArbol()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public Arbol? Arbol => _arbol;
    public LayoutResult? Layout => _layout;
    public string? Seleccion
    {
        get => _sel;
        set
        {
            if (_sel == value) return;
            _sel = value;
            ActualizarResaltado();
            SeleccionCambiada?.Invoke(value);
        }
    }

    public FormaTarjeta Forma
    {
        get => _forma;
        set { if (_forma != value) Recolocar(() => _forma = value); }
    }

    public Ordenacion Ordenacion
    {
        get => _ordenacion;
        set { if (_ordenacion != value) Recolocar(() => _ordenacion = value); }
    }

    // ---------- Datos ----------

    /// <summary>Muestra un árbol. Con <paramref name="encuadrar"/>, pone la vista inicial (todo, o la persona principal y su familia).</summary>
    public void Mostrar(Arbol a, bool encuadrar)
    {
        _arbol = a;
        if (_sel != null && a.Buscar(_sel) == null) _sel = null;
        Calcular();
        if (encuadrar) { if (Bounds.Width > 0) VistaInicial(); else _encuadrarPendiente = true; }
    }

    /// <summary>Tras editar el árbol: se recoloca y la persona seleccionada (o la del centro) se queda donde estaba.</summary>
    public void Refrescar() => Recolocar(() => { if (_sel != null && _arbol?.Buscar(_sel) == null) _sel = null; });

    private void Recolocar(Action cambio)
    {
        var id = _sel ?? PersonaMasCentrada();
        var antes = id != null ? Centro(id) : null;
        cambio();
        Calcular();
        if (id != null && antes is { } a && Centro(id) is { } d)
        {
            _tx += (a.X - d.X) * _e; _ty += (a.Y - d.Y) * _e;
        }
        InvalidateVisual();
    }

    private void Calcular()
    {
        if (_arbol == null) return;
        _formaEnUso = _forma.Para(_arbol);
        _layout = LayoutEngine.Calcular(_arbol, _formaEnUso.Opciones(_ordenacion));
        _directa = _arbol.LineaDirecta();
        _tarjetas.Clear();
        _caminos.Clear();
        if (_formaEnUso.SinTarjeta) AcercarLineas();
        foreach (var c in _layout.Conexiones) _caminos[c] = Camino(c.Puntos, 12);
        ActualizarResaltado();
    }

    public void ResaltarCoincidencias(IEnumerable<string> ids)
    {
        _coincidencias = ids.ToHashSet();
        InvalidateVisual();
    }

    private void ActualizarResaltado()
    {
        _res = _arbol != null && _layout != null ? Resaltado.De(_arbol, _layout, _sel) : Resaltado.Ninguno;
        InvalidateVisual();
    }

    // ---------- Cámara ----------

    public Rect? RectDe(string id)
    {
        if (_layout == null || !_layout.Cartas.TryGetValue(id, out var c)) return null;
        var o = _layout.Opciones;
        return new Rect(c.X - o.AnchoCarta / 2, c.Y, o.AnchoCarta, o.AltoCarta);
    }

    /// <summary>Centro de la tarjeta en la pantalla.</summary>
    private Point? Centro(string id) => RectDe(id) is { } r ? new Point(r.Center.X * _e + _tx, r.Center.Y * _e + _ty) : null;

    private string? PersonaMasCentrada()
    {
        if (_layout == null || _layout.Cartas.Count == 0 || Bounds.Width <= 0) return null;
        double cx = (Bounds.Width / 2 - _tx) / _e, cy = (Bounds.Height / 2 - _ty) / _e;
        var o = _layout.Opciones;
        return _layout.Cartas.MinBy(kv => Math.Pow(kv.Value.X - cx, 2) + Math.Pow(kv.Value.Y + o.AltoCarta / 2 - cy, 2)).Key;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_encuadrarPendiente && e.NewSize.Width > 0) { _encuadrarPendiente = false; VistaInicial(); }
        else if (e.PreviousSize.Width > 0)
        {
            // al girar el teléfono o abrirse el teclado, el centro de la vista sigue en el centro
            _tx += (e.NewSize.Width - e.PreviousSize.Width) / 2;
            _ty += (e.NewSize.Height - e.PreviousSize.Height) / 2;
        }
    }

    /// <summary>Todo el árbol si cabe con una letra legible; si no, la persona principal con sus padres, abuelos e hijos.</summary>
    public void VistaInicial()
    {
        if (_layout == null || _arbol == null || Bounds.Width <= 0) return;
        double pad = 24;
        // la escala mínima legible, mayor si en el sistema se ha elegido letra grande
        double legible = Math.Min(1, 0.42 * Vistas.Ui.Escala);
        double fit = Math.Min((Bounds.Width - 2 * pad) / _layout.Ancho, (Bounds.Height - 2 * pad) / _layout.Alto);
        if (fit >= legible) { Encuadrar(false); return; }
        if (RectDe(_arbol.RaizId) is not { } raiz) { Encuadrar(false); return; }
        var caja = raiz;
        foreach (var id in FamiliaCercana(_arbol.RaizId))
            if (RectDe(id) is { } r) caja = caja.Union(r);
        double s = Math.Clamp(Math.Min((Bounds.Width - 2 * pad) / caja.Width, (Bounds.Height - 2 * pad) / caja.Height), legible, Math.Max(1.0, legible));
        double vw = Bounds.Width / s, vh = Bounds.Height / s;
        double cx = caja.Width <= vw - 2 * pad / s ? caja.Center.X : raiz.Center.X;
        double cy = caja.Height <= vh - 2 * pad / s ? caja.Center.Y : raiz.Bottom + 2.5 * pad / s - vh / 2;
        IrA(s, Bounds.Width / 2 - cx * s, Bounds.Height / 2 - cy * s, false);
    }

    private IEnumerable<string> FamiliaCercana(string id)
    {
        var a = _arbol!;
        foreach (var p in a.UnionComoHijo(id)?.Parejas ?? new List<string>())
        {
            yield return p;
            foreach (var ab in a.UnionComoHijo(p)?.Parejas ?? new List<string>()) yield return ab;
        }
        foreach (var u in a.UnionesComoPareja(id))
        {
            foreach (var q in u.Parejas) yield return q;
            foreach (var h in u.Hijos) yield return h;
        }
    }

    public void Encuadrar(bool animar = true)
    {
        if (_layout == null || Bounds.Width <= 0) return;
        double pad = 24;
        double s = Math.Min((Bounds.Width - 2 * pad) / _layout.Ancho, (Bounds.Height - 2 * pad) / _layout.Alto);
        s = Math.Clamp(Math.Min(s, 1.0), EscalaMin, EscalaMax);
        IrA(s, (Bounds.Width - _layout.Ancho * s) / 2, (Bounds.Height - _layout.Alto * s) / 2, animar);
    }

    public void CentrarEn(string id, double? escala = null, bool animar = true)
    {
        if (RectDe(id) is not { } r) return;
        double s = escala ?? Math.Max(_e, 0.6);
        IrA(s, Bounds.Width / 2 - r.Center.X * s, Bounds.Height / 2 - r.Center.Y * s, animar);
    }

    /// <summary>
    /// Desplaza lo justo para que la tarjeta se vea entera, sin quedar debajo de lo que tapa la parte de abajo del lienzo
    /// (la ficha de la persona seleccionada mide <paramref name="tapadoAbajo"/>).
    /// </summary>
    public void AsegurarVisible(string id, double tapadoAbajo)
    {
        // si ya va de camino a algún sitio (centrar en una búsqueda…), no se le lleva la contraria
        if (_anim != null || RectDe(id) is not { } r || Bounds.Height <= 0) return;
        double margen = 20;
        double arriba = r.Top * _e + _ty, abajo = (r.Bottom + 16) * _e + _ty;       // +16: el botón «+» de debajo
        double izq = r.Left * _e + _tx, der = r.Right * _e + _tx;
        double dy = 0, dx = 0;
        double limite = Bounds.Height - tapadoAbajo - margen;
        if (abajo > limite) dy = limite - abajo;
        if (arriba + dy < margen) dy = margen - arriba;
        if (der > Bounds.Width - margen) dx = Bounds.Width - margen - der;
        if (izq + dx < margen) dx = margen - izq;
        if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5) IrA(_e, _tx + dx, _ty + dy, true);
    }

    private DispatcherTimer? _anim;

    public void IrA(double escala, double tx, double ty, bool animar)
    {
        escala = Math.Clamp(escala, EscalaMin, EscalaMax);
        Detener();
        if (!animar) { _e = escala; _tx = tx; _ty = ty; InvalidateVisual(); return; }
        double e0 = _e, x0 = _tx, y0 = _ty;
        var inicio = DateTime.UtcNow;
        _anim = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) =>
        {
            double t = Math.Min(1, (DateTime.UtcNow - inicio).TotalMilliseconds / 320);
            double k = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            // la escala se interpola en logaritmos (el zoom se siente uniforme) y el desplazamiento de modo que el punto
            // del árbol que acabará en el centro vaya en línea recta
            _e = Math.Exp(Math.Log(e0) + (Math.Log(escala) - Math.Log(e0)) * k);
            double cx0 = (Bounds.Width / 2 - x0) / e0, cy0 = (Bounds.Height / 2 - y0) / e0;
            double cx1 = (Bounds.Width / 2 - tx) / escala, cy1 = (Bounds.Height / 2 - ty) / escala;
            double cx = cx0 + (cx1 - cx0) * k, cy = cy0 + (cy1 - cy0) * k;
            _tx = Bounds.Width / 2 - cx * _e; _ty = Bounds.Height / 2 - cy * _e;
            InvalidateVisual();
            if (t >= 1) Detener();
        });
        _anim.Start();
    }

    private void Detener() { _anim?.Stop(); _anim = null; }

    private void ZoomEn(Point p, double factor)
    {
        double ns = Math.Clamp(_e * factor, EscalaMin, EscalaMax);
        double k = ns / _e;
        _tx = p.X - (p.X - _tx) * k; _ty = p.Y - (p.Y - _ty) * k; _e = ns;
        InvalidateVisual();
    }

    public void Zoom(double factor) { Detener(); ZoomEn(new Point(Bounds.Width / 2, Bounds.Height / 2), factor); }

    // ---------- Gestos ----------

    private readonly Dictionary<long, Point> _dedos = new();
    private Point _inicio;
    private DateTime _inicioT;
    private bool _movido, _mantenido;
    private DispatcherTimer? _mantener;
    private double _pinchDist, _pinchE, _pinchTx, _pinchTy;
    private Point _pinchCentro;
    private DateTime _ultimoToque;
    private Point _ultimoToqueP;
    private readonly List<(DateTime T, Point P)> _rastro = new();
    private DispatcherTimer? _inercia;

    private double Tolerancia(PointerEventArgs e) => e.Pointer.Type == PointerType.Mouse ? 4 : 12;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Detener(); _inercia?.Stop();
        var p = e.GetPosition(this);
        _dedos[e.Pointer.Id] = p;
        e.Pointer.Capture(this);
        if (_dedos.Count == 1)
        {
            _inicio = p; _inicioT = DateTime.UtcNow; _movido = false; _mantenido = false;
            _rastro.Clear(); _rastro.Add((DateTime.UtcNow, p));
            _mantener?.Stop();
            _mantener = new DispatcherTimer(TimeSpan.FromMilliseconds(520), DispatcherPriority.Input, (_, _) =>
            {
                _mantener?.Stop();
                if (_movido || _dedos.Count != 1) return;
                if (TarjetaEn(_inicio) is { } id) { _mantenido = true; Seleccion = id; MenuPedido?.Invoke(id); }
            });
            _mantener.Start();
        }
        else if (_dedos.Count == 2)
        {
            _mantener?.Stop();
            _movido = true;
            EmpezarPinch();
        }
        e.Handled = true;
    }

    private void EmpezarPinch()
    {
        var ps = _dedos.Values.Take(2).ToArray();
        _pinchDist = Math.Max(1, Dist(ps[0], ps[1]));
        _pinchCentro = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
        _pinchE = _e; _pinchTx = _tx; _pinchTy = _ty;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dedos.ContainsKey(e.Pointer.Id)) return;
        var p = e.GetPosition(this);
        var previo = _dedos[e.Pointer.Id];
        _dedos[e.Pointer.Id] = p;
        if (_dedos.Count >= 2)
        {
            var ps = _dedos.Values.Take(2).ToArray();
            double ns = Math.Clamp(_pinchE * Dist(ps[0], ps[1]) / _pinchDist, EscalaMin, EscalaMax);
            var c = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
            // el punto del árbol que estaba bajo el centro de los dos dedos sigue bajo él
            double wx = (_pinchCentro.X - _pinchTx) / _pinchE, wy = (_pinchCentro.Y - _pinchTy) / _pinchE;
            _e = ns; _tx = c.X - wx * ns; _ty = c.Y - wy * ns;
            InvalidateVisual();
            return;
        }
        if (!_movido)
        {
            if (Dist(p, _inicio) < Tolerancia(e)) return;
            _movido = true; _mantener?.Stop();
            previo = _inicio;
        }
        if (_mantenido) return;
        _tx += p.X - previo.X; _ty += p.Y - previo.Y;
        _rastro.Add((DateTime.UtcNow, p));
        if (_rastro.Count > 6) _rastro.RemoveAt(0);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dedos.Remove(e.Pointer.Id)) return;
        _mantener?.Stop();
        var p = e.GetPosition(this);
        if (_dedos.Count == 1) { EmpezarPinch1(); return; }
        if (_dedos.Count > 0) return;
        e.Pointer.Capture(null);
        if (_mantenido) return;
        if (!_movido && (DateTime.UtcNow - _inicioT).TotalMilliseconds < 600) { Toque(p); return; }
        if (_movido) Lanzar();
    }

    /// <summary>Al levantar uno de los dos dedos, el que queda sigue moviendo el árbol desde donde está.</summary>
    private void EmpezarPinch1()
    {
        _movido = true;
        _rastro.Clear();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dedos.Remove(e.Pointer.Id);
        _mantener?.Stop();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Detener();
        ZoomEn(e.GetPosition(this), Math.Pow(1.18, e.Delta.Y));
        e.Handled = true;
    }

    /// <summary>Al soltar el dedo con velocidad, el árbol sigue deslizándose y frena poco a poco.</summary>
    private void Lanzar()
    {
        if (_rastro.Count < 2) return;
        var (t0, p0) = _rastro[0]; var (t1, p1) = _rastro[^1];
        double dt = (t1 - t0).TotalSeconds;
        if (dt <= 0 || (DateTime.UtcNow - t1).TotalMilliseconds > 80) return;
        double vx = (p1.X - p0.X) / dt, vy = (p1.Y - p0.Y) / dt;
        if (Math.Sqrt(vx * vx + vy * vy) < 250) return;
        var ultimo = DateTime.UtcNow;
        _inercia = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) =>
        {
            var ahora = DateTime.UtcNow;
            double s = (ahora - ultimo).TotalSeconds; ultimo = ahora;
            _tx += vx * s; _ty += vy * s;
            double f = Math.Pow(0.06, s);       // pierde el 94 % de la velocidad por segundo
            vx *= f; vy *= f;
            InvalidateVisual();
            if (Math.Sqrt(vx * vx + vy * vy) < 20) _inercia?.Stop();
        });
        _inercia.Start();
    }

    private void Toque(Point p)
    {
        var ahora = DateTime.UtcNow;
        bool doble = (ahora - _ultimoToque).TotalMilliseconds < 380 && Dist(p, _ultimoToqueP) < 40;
        _ultimoToque = doble ? default : ahora; _ultimoToqueP = p;

        // los botones de la tarjeta seleccionada: «+» (añadir familiar) y el de su árbol enlazado
        if (_sel != null && RectDe(_sel) is { } rs)
        {
            var m = AMundo(p);
            // El «+» asoma por el borde de abajo de la tarjeta: cuenta el propio círculo y, para el dedo, un margen por
            // debajo y a los lados, pero no el resto de la tarjeta (tocarla otra vez abre su ficha).
            double aMas = Dist(m, BotonMas(rs));
            bool enTarjeta = rs.Contains(m) && aMas > 15;
            if (!enTarjeta && aMas <= Math.Max(16, 22 / _e)) { MenuPedido?.Invoke(_sel); return; }
            if (TieneEnlace(_sel) && Dist(m, BotonEnlace(rs)) <= Math.Max(13, 16 / _e)) { EnlacePedido?.Invoke(_sel); return; }
        }
        var id = TarjetaEn(p);
        // tocar la tarjeta que ya está seleccionada (o tocar dos veces seguidas) abre su ficha
        if (id != null && (id == _sel || doble)) { Seleccion = id; FichaPedida?.Invoke(id); return; }
        Seleccion = id;
    }

    private Point AMundo(Point p) => new((p.X - _tx) / _e, (p.Y - _ty) / _e);

    private string? TarjetaEn(Point pantalla)
    {
        if (_layout == null) return null;
        var m = AMundo(pantalla);
        var o = _layout.Opciones;
        // con el dedo se toca con poca precisión: un margen alrededor de cada tarjeta, en píxeles de pantalla
        double margen = 6 / _e;
        foreach (var (id, c) in _layout.Cartas)
            if (m.X >= c.X - o.AnchoCarta / 2 - margen && m.X <= c.X + o.AnchoCarta / 2 + margen && m.Y >= c.Y - margen && m.Y <= c.Y + o.AltoCarta + margen)
                return id;
        return null;
    }

    private bool TieneEnlace(string id) => !string.IsNullOrEmpty(_arbol?.Buscar(id)?.ArbolEnlazado);
    /// <summary>Sin tarjeta, el recuadro de la seleccionada se separa un poco del texto (y el «+» baja con él).</summary>
    private double Aire => _formaEnUso.SinTarjeta ? 6 : 0;
    private Point BotonMas(Rect r) => new(r.Center.X, r.Bottom + (_formaEnUso.SinTarjeta ? 14 : 0));
    private static Point BotonEnlace(Rect r) => new(r.Right - 20, r.Bottom - 18);
    private static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ---------- Dibujo ----------

    private static readonly IBrush Fondo = new RadialGradientBrush
    {
        GradientStops = { new GradientStop(Color.FromRgb(0x16, 0x1A, 0x26), 0), new GradientStop(Color.FromRgb(0x0A, 0x0C, 0x11), 1) },
        RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
    };

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Fondo, new Rect(Bounds.Size));
        if (_layout == null || _arbol == null) return;
        var visible = new Rect(-_tx / _e, -_ty / _e, Bounds.Width / _e, Bounds.Height / _e);
        using (ctx.PushTransform(Matrix.CreateScale(_e, _e) * Matrix.CreateTranslation(_tx, _ty)))
            DibujarMundo(ctx, visible, _e < 0.2, conBotones: true);
    }

    /// <summary>
    /// Dibuja el árbol en coordenadas del mundo. <paramref name="sencillo"/>: de muy lejos, las tarjetas sin letras ni
    /// fotos (no se leerían y así el zoom va fluido).
    /// </summary>
    private void DibujarMundo(DrawingContext ctx, Rect visible, bool sencillo, bool conBotones)
    {
        var lay = _layout!;
        DibujarLineas(ctx, visible);
        var o = lay.Opciones;
        foreach (var (id, c) in lay.Cartas)
        {
            var r = new Rect(c.X - o.AnchoCarta / 2, c.Y, o.AnchoCarta, o.AltoCarta);
            if (!r.Inflate(12).Intersects(visible)) continue;
            DibujarTarjeta(ctx, id, r, c.Politica, sencillo);
        }
        if (conBotones && _sel != null && RectDe(_sel) is { } rs && !sencillo)
        {
            var c = BotonMas(rs);
            ctx.DrawEllipse(new SolidColorBrush(Estilo.Oro), new Pen(new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16)), 2), c, 15, 15);
            var lapiz = new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x05)), 2.6) { LineCap = PenLineCap.Round };
            ctx.DrawLine(lapiz, new Point(c.X - 6.5, c.Y), new Point(c.X + 6.5, c.Y));
            ctx.DrawLine(lapiz, new Point(c.X, c.Y - 6.5), new Point(c.X, c.Y + 6.5));
        }
    }

    private static Pen Lapiz(Color c, byte alfa, double grosor) =>
        new(new SolidColorBrush(Estilo.ConAlfa(c, alfa)), grosor) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };

    private static readonly Color Gris = Color.FromRgb(0x5B, 0x65, 0x7D), Claro = Color.FromRgb(0x8A, 0x94, 0xAD);
    private static readonly Pen[] PNormal = { Lapiz(Gris, 0xFF, 2), Lapiz(Gris, 0x55, 2) };
    private static readonly Pen[] PPareja = { Lapiz(Claro, 0xFF, 2.5), Lapiz(Claro, 0x55, 2.5) };
    private static readonly Pen[] POro = { Lapiz(Estilo.Oro, 0xFF, 3.6), Lapiz(Estilo.Oro, 0x80, 3.6) };
    private static readonly Pen[] PResplandor = { Lapiz(Estilo.Oro, 0x2C, 9), Lapiz(Estilo.Oro, 0x10, 9) };
    private static readonly Pen PLuz = Lapiz(Estilo.Linaje, 0xFF, 4), PLuzResplandor = Lapiz(Estilo.Linaje, 0x40, 11);
    private static readonly IBrush FondoNudo = new SolidColorBrush(Estilo.FondoApp);

    private void DibujarLineas(DrawingContext ctx, Rect visible)
    {
        var lay = _layout!;
        bool atenuar = _res.Linaje.Count > 0;
        int Tono(Conexion c) => atenuar && !_res.Cercanas.Contains(c) ? 1 : 0;
        bool Ve(Conexion c) => c.Puntos.Count > 0 && Caja(c).Intersects(visible);
        var resto = lay.Conexiones.Where(c => !_res.Linaje.Contains(c) && Ve(c)).OrderByDescending(Tono).ToList();
        foreach (var c in resto.Where(c => !c.Directa))
            ctx.DrawGeometry(null, (c.Tipo == TipoConexion.Pareja ? PPareja : PNormal)[Tono(c)], _caminos[c]);
        foreach (var c in resto.Where(c => c.Directa)) ctx.DrawGeometry(null, PResplandor[Tono(c)], _caminos[c]);
        foreach (var c in resto.Where(c => c.Directa)) ctx.DrawGeometry(null, POro[Tono(c)], _caminos[c]);
        foreach (var c in _res.Linaje) ctx.DrawGeometry(null, PLuzResplandor, _caminos[c]);
        foreach (var c in _res.Linaje) ctx.DrawGeometry(null, PLuz, _caminos[c]);
        foreach (var c in lay.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2 && Ve(c)))
        {
            var m = c.Nudo is { } n ? new Point(n.X, n.Y) : new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
            Color borde = _res.Linaje.Contains(c) ? Estilo.Linaje : Tono(c) == 1 ? Color.FromArgb(0x70, 0x8A, 0x94, 0xAD) : c.Directa ? Estilo.Oro : Claro;
            ctx.DrawEllipse(FondoNudo, new Pen(new SolidColorBrush(borde), 2), m, 5, 5);
        }
    }

    private static Rect Caja(Conexion c)
    {
        double x0 = c.Puntos.Min(p => p.X), x1 = c.Puntos.Max(p => p.X), y0 = c.Puntos.Min(p => p.Y), y1 = c.Puntos.Max(p => p.Y);
        return new Rect(x0 - 10, y0 - 10, x1 - x0 + 20, y1 - y0 + 20);
    }

    /// <summary>Polilínea ortogonal con esquinas redondeadas de radio máximo r (como en el escritorio).</summary>
    private static Geometry Camino(List<Pt> pts, double r)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        if (pts.Count < 2) return g;
        ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), false);
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
            double l1 = D(a, b), l2 = D(b, c);
            double rr = Math.Min(r, Math.Min(l1, l2) / 2);
            if (rr < 0.5) { ctx.LineTo(new Point(b.X, b.Y)); continue; }
            var p1 = new Point(b.X + (a.X - b.X) / l1 * rr, b.Y + (a.Y - b.Y) / l1 * rr);
            var p2 = new Point(b.X + (c.X - b.X) / l2 * rr, b.Y + (c.Y - b.Y) / l2 * rr);
            ctx.LineTo(p1);
            double cruz = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            ctx.ArcTo(p2, new Size(rr, rr), 0, false, cruz > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        }
        ctx.LineTo(new Point(pts[^1].X, pts[^1].Y));
        ctx.EndFigure(false);
        return g;
        static double D(Pt a, Pt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    // ---------- Tarjetas ----------

    /// <summary>Lo que se prepara una vez por persona para dibujar su tarjeta deprisa (textos medidos, foto, colores).</summary>
    private sealed class Tarjeta
    {
        public required Estilo Estilo;
        public required bool SinNombre;
        public FormattedText? Nombre, Apellidos, Iniciales;
        public Bitmap? Foto;
        public required IBrush FondoTarjeta;
        public bool Historia, Enlace;
    }

    private static readonly Typeface LetraNombre = new(Estilo.Letra, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface LetraNombreCursiva = new(Estilo.Letra, FontStyle.Italic, FontWeight.SemiBold);
    private static readonly Typeface LetraApellidos = new(Estilo.Letra, FontStyle.Normal, FontWeight.Medium);
    private static readonly IBrush Blanco = Brushes.White;
    private static readonly IBrush GrisApellidos = new SolidColorBrush(Color.FromRgb(0xB4, 0xBA, 0xCB));

    /// <summary>Zona del texto dentro de la tarjeta, según su forma.</summary>
    private (Rect Texto, Rect? Foto) Zonas(Rect r, bool enlace)
    {
        var f = _formaEnUso;
        if (!f.Vertical)
        {
            if (f.Foto)
            {
                double fx = r.X + (f.SinTarjeta ? 2 : 22);
                var foto = new Rect(fx, r.Center.Y - 29, 58, 58);
                double tx = foto.Right + (f.SinTarjeta ? 10 : 12);
                // a la derecha, sitio para el botón del árbol enlazado solo si lo lleva
                double der = f.SinTarjeta ? 2 : enlace ? 30 : 12;
                return (new Rect(tx, r.Y + 4, r.Right - der - tx, r.Height - 8), foto);
            }
            double izq = f.SinTarjeta ? 2 : 7 + 17;
            return (new Rect(r.X + izq, r.Y + 4, r.Width - izq - (f.SinTarjeta ? 2 : 10), r.Height - 8), null);
        }
        if (f.Foto)
        {
            double arriba = f.SinTarjeta ? 2 : 22;
            var foto = new Rect(r.Center.X - 33, r.Y + arriba, 66, 66);
            double ty = foto.Bottom + 7;
            double m = f.SinTarjeta ? 2 : 8;
            // abajo, un poco más de margen: el «+» de la tarjeta seleccionada asoma por ahí
            return (new Rect(r.X + m, ty, r.Width - 2 * m, r.Bottom - (f.SinTarjeta ? 8 : 14) - ty), foto);
        }
        double top = f.SinTarjeta ? 2 : 9 + 7 + 6;
        return (new Rect(r.X + (f.SinTarjeta ? 2 : 8), r.Y + top, r.Width - (f.SinTarjeta ? 4 : 16), r.Height - top - (f.SinTarjeta ? 2 : 10)), null);
    }

    /// <summary>Dónde van el nombre y los apellidos dentro de la zona del texto (lo usan el dibujo y las líneas).</summary>
    private (Point Nombre, Point? Apellidos) PosicionTextos(Tarjeta t, Rect zona)
    {
        var f = _formaEnUso;
        var nom = t.Nombre!;
        double alto = nom.Height + (t.Apellidos?.Height ?? 0);
        if (!f.Vertical)
        {
            double y = zona.Center.Y - alto / 2;
            bool centrado = f.SinTarjeta && !f.Foto;
            var pn = new Point(centrado ? zona.Center.X - nom.Width / 2 : zona.X, y);
            Point? pa = t.Apellidos is { } ap ? new Point(centrado ? zona.Center.X - ap.Width / 2 : zona.X, y + nom.Height) : null;
            return (pn, pa);
        }
        double yv = f.Foto ? zona.Y : zona.Center.Y - alto / 2;
        // los textos verticales ocupan todo el ancho y se centran dentro
        return (new Point(zona.X + (zona.Width - nom.MaxTextWidth) / 2, yv),
                t.Apellidos is { } ap2 ? new Point(zona.X + (zona.Width - ap2.MaxTextWidth) / 2, yv + nom.Height) : null);
    }

    /// <summary>Lo que se ve de una tarjeta (la foto, el nombre y los apellidos), en coordenadas del árbol.</summary>
    private List<Rect> Visibles(string id, Rect r)
    {
        var t = Preparar(id, r);
        var f = _formaEnUso;
        var (zona, foto) = Zonas(r, t.Enlace && f.Foto && !f.SinTarjeta);
        var lista = new List<Rect>();
        if (foto is { } fr) lista.Add(fr);
        if (t.Nombre is { } nom)
        {
            var (pn, pa) = PosicionTextos(t, zona);
            // los verticales están centrados: lo que se ve es el ancho de la línea más larga, en medio
            double Izq(FormattedText ft, Point p) => f.Vertical ? zona.Center.X - ft.Width / 2 : p.X;
            lista.Add(new Rect(Izq(nom, pn), pn.Y, nom.Width, nom.Height));
            if (t.Apellidos is { } ap && pa is { } p2) lista.Add(new Rect(Izq(ap, p2), p2.Y, ap.Width, ap.Height));
        }
        return lista;
    }

    /// <summary>
    /// Sin tarjeta, el borde de la tarjeta no se ve: las líneas que llegan a una persona (desde arriba, desde abajo o
    /// desde su pareja, al lado) se alargan hasta su nombre o su foto, para que no se queden cortas (como en el escritorio).
    /// </summary>
    private void AcercarLineas()
    {
        var lay = _layout!;
        double letra = _formaEnUso.LetraNombre;
        // un poco de aire entre la línea y el nombre: a los lados algo más de un tercio de la letra; arriba y abajo, menos
        double hueco = 0.36 * letra, huecoV = 0.12 * letra;
        var o = lay.Opciones;
        var zonas = lay.Cartas.Select(kv =>
        {
            var caja = new Rect(kv.Value.X - o.AnchoCarta / 2, kv.Value.Y, o.AnchoCarta, o.AltoCarta);
            var (_, foto) = Zonas(caja, false);
            return (Caja: caja, Visto: Visibles(kv.Key, caja), Foto: foto);
        }).ToList();
        foreach (var c in lay.Conexiones)
        {
            var ps = c.Puntos;
            if (ps.Count < 2) continue;
            // el círculo de la pareja (del que cuelgan los hijos) se queda donde estaba aunque la línea se alargue
            if (c.Tipo == TipoConexion.Pareja && c.Nudo == null && ps.Count == 2)
                c.Nudo = new Pt((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
            if (!AlNombre(ps, principio: false)) ps[^1] = Acercar(ps[^1], ps[^2]);
            if (!AlNombre(ps, principio: true)) ps[0] = Acercar(ps[0], ps[1]);
        }

        (Rect Caja, List<Rect> Visto, Rect? Foto)? ZonaDe(Pt p)
        {
            foreach (var z in zonas)
                if (p.X >= z.Caja.Left - 0.5 && p.X <= z.Caja.Right + 0.5 && p.Y >= z.Caja.Top - 0.5 && p.Y <= z.Caja.Bottom + 0.5) return z;
            return null;
        }

        // Una línea vertical que llega a una persona con la foto al lado del nombre: el centro de la tarjeta (por donde cae)
        // puede quedar junto a un nombre corto («Yo») en vez de encima. Si la línea viene de una línea horizontal (la de
        // los hermanos), su último tramo se desplaza al centro del nombre (la horizontal se alarga o se acorta lo justo,
        // sin cruces). Si baja recta (del círculo de los padres o de otra persona), no se desplaza. En los dos casos llega
        // hasta justo encima del nombre (o debajo de los apellidos, si sale hacia abajo).
        bool AlNombre(List<Pt> ps, bool principio)
        {
            if (_formaEnUso.Vertical || !_formaEnUso.Foto) return false;
            int e = principio ? 0 : ps.Count - 1, n = principio ? 1 : ps.Count - 2;
            var p = ps[e]; var q = ps[n];
            if (Math.Abs(p.X - q.X) > 0.01 || Math.Abs(p.Y - q.Y) < 0.01) return false;     // no llega en vertical
            if (ZonaDe(p) is not { } z || z.Foto is not { } f) return false;
            var textos = z.Visto.Where(r => r != f).ToList();
            if (textos.Count == 0) return false;
            var nombre = textos[0];
            bool desdeArriba = q.Y < p.Y;
            double borde = desdeArriba ? textos.Min(r => r.Top) - huecoV : textos.Max(r => r.Bottom) + huecoV;
            int r = principio ? n + 1 : n - 1;
            bool esquina = r >= 0 && r < ps.Count && r != e && Math.Abs(ps[r].Y - q.Y) < 0.01;
            double x = p.X;
            if (esquina && (p.X < nombre.Left + 4 || p.X > nombre.Right - 4))
            {
                x = nombre.Center.X;
                ps[n] = new Pt(x, q.Y);
            }
            ps[e] = new Pt(x, borde);
            return true;
        }

        // p: el extremo de la línea; q: el punto anterior (de dónde viene)
        Pt Acercar(Pt p, Pt q)
        {
            double ex = p.X - q.X, ey = p.Y - q.Y;
            if (Math.Abs(ex) < 0.01 && Math.Abs(ey) < 0.01) return p;
            int dx = Math.Abs(ex) > Math.Abs(ey) ? Math.Sign(ex) : 0, dy = dx == 0 ? Math.Sign(ey) : 0;
            if (ZonaDe(p) is not { } z) return p;
            var cruzan = z.Visto.Where(r => dx == 0 ? p.X >= r.Left && p.X <= r.Right : p.Y >= r.Top && p.Y <= r.Bottom).ToList();
            if (cruzan.Count == 0) return p;
            if (dy > 0) return new Pt(p.X, Math.Max(p.Y, cruzan.Min(r => r.Top) - huecoV));       // entra por arriba
            if (dy < 0) return new Pt(p.X, Math.Min(p.Y, cruzan.Max(r => r.Bottom) + huecoV));    // por abajo
            if (dx > 0) return new Pt(Math.Max(p.X, cruzan.Min(r => r.Left) - hueco), p.Y);     // por la izquierda
            return new Pt(Math.Min(p.X, cruzan.Max(r => r.Right) + hueco), p.Y);                 // por la derecha
        }
    }

    private Tarjeta Preparar(string id, Rect r)
    {
        if (_tarjetas.TryGetValue(id, out var t)) return t;
        var p = _arbol!.Obtener(id);
        bool sinNombre = Nombres.SinNombre(p);
        var e = Estilo.De(p.Sexo, sinNombre);
        t = new Tarjeta
        {
            Estilo = e, SinNombre = sinNombre,
            FondoTarjeta = _formaEnUso.SinTarjeta ? Brushes.Transparent : new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(e.Fondo1, 0), new GradientStop(e.Fondo2, 1) },
            },
            Foto = _formaEnUso.Foto ? Fotos.Imagen(p.Foto) : null,
            Historia = !string.IsNullOrWhiteSpace(p.Historia),
            Enlace = !string.IsNullOrEmpty(p.ArbolEnlazado),
        };
        var (zona, _) = Zonas(r, t.Enlace && _formaEnUso.Foto && !_formaEnUso.SinTarjeta);
        string nombre = sinNombre ? "Sin nombre" : string.IsNullOrWhiteSpace(p.Nombre) ? p.Apellidos.Trim() : p.Nombre.Trim();
        bool apellidosAparte = !sinNombre && !_formaEnUso.SinApellidos && !string.IsNullOrWhiteSpace(p.Nombre) && !string.IsNullOrWhiteSpace(p.Apellidos);
        IBrush colorNombre = sinNombre ? new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF))
            : _formaEnUso.SinTarjeta ? new SolidColorBrush(Aclarar(e.Acento, 0.15)) : Blanco;
        // El nombre, grande: en el teléfono el árbol se ve más pequeño que en el ordenador. Si va solo (sin apellidos
        // debajo), aún más. Los que no caben se encogen lo justo.
        double tamNombre = _formaEnUso.Vertical ? (apellidosAparte ? 24 : 26) : (apellidosAparte ? 28 : 31);
        double tamApellidos = _formaEnUso.Vertical ? 17 : 19;
        if (!_formaEnUso.Vertical)
        {
            // en horizontal, una línea para cada uno: si no cabe, se encoge lo justo
            t.Nombre = Encoger(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre, colorNombre, zona.Width);
            if (apellidosAparte) t.Apellidos = Encoger(p.Apellidos.Trim(), LetraApellidos, tamApellidos, GrisApellidos, zona.Width);
            // y si entre los dos no caben a lo alto (tarjetas sin foto, más bajas), se encogen los dos
            double alto = t.Nombre.Height + (t.Apellidos?.Height ?? 0);
            if (alto > zona.Height && zona.Height > 0)
            {
                double k = zona.Height / alto;
                t.Nombre = Encoger(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre * k, colorNombre, zona.Width);
                if (apellidosAparte) t.Apellidos = Encoger(p.Apellidos.Trim(), LetraApellidos, tamApellidos * k, GrisApellidos, zona.Width);
            }
        }
        else
        {
            // en vertical, centrados y en dos líneas si hace falta; si aun así no caben, se encoge todo
            double k = 1;
            for (int i = 0; i < 12; i++)
            {
                t.Nombre = Texto(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre * k, colorNombre, zona.Width);
                t.Apellidos = apellidosAparte ? Texto(p.Apellidos.Trim(), LetraApellidos, tamApellidos * k, GrisApellidos, zona.Width) : null;
                double alto = t.Nombre.Height + (t.Apellidos?.Height ?? 0);
                double ancho = Math.Max(t.Nombre.Width, t.Apellidos?.Width ?? 0);
                if (alto <= zona.Height + 0.5 && ancho <= zona.Width + 0.5) break;
                k *= 0.92;
            }
        }
        if (_formaEnUso.Foto && t.Foto == null)
            t.Iniciales = new FormattedText(Nombres.Iniciales(p), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LetraNombre,
                _formaEnUso.Vertical ? 23 : 21, Blanco);
        _tarjetas[id] = t;
        return t;
    }

    private static FormattedText Encoger(string s, Typeface letra, double tam, IBrush color, double ancho)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, color);
        if (ft.Width <= ancho || ancho <= 0) return ft;
        return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, Math.Max(6, tam * ancho / ft.Width * 0.995), color);
    }

    private static FormattedText Texto(string s, Typeface letra, double tam, IBrush color, double ancho) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, color)
        {
            MaxTextWidth = Math.Max(1, ancho), TextAlignment = TextAlignment.Center, MaxLineCount = 3,
        };

    private static Color Aclarar(Color c, double t) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));

    private void DibujarTarjeta(DrawingContext ctx, string id, Rect r, bool politica, bool sencillo)
    {
        var t = Preparar(id, r);
        var e = t.Estilo;
        var f = _formaEnUso;
        bool sel = id == _sel, coincide = _coincidencias.Contains(id), directa = _directa.Contains(id);
        bool atenuada = _res.Familia != null && !_res.Familia.Contains(id);
        double opacidad = atenuada ? 0.28 : politica ? 0.93 : 1;
        using var _ = opacidad < 1 ? ctx.PushOpacity(opacidad) : (DrawingContext.PushedState?)null;
        var caja = new RoundedRect(r, e.Radio);

        if (sencillo)
        {
            ctx.DrawRectangle(f.SinTarjeta ? new SolidColorBrush(e.Acento) : t.FondoTarjeta,
                new Pen(new SolidColorBrush(sel ? Colors.White : directa ? Estilo.Oro : e.Borde), sel ? 6 : 3), caja);
            return;
        }

        // halo: seleccionada, coincide con la búsqueda o es de la línea directa
        IBrush? halo = sel ? new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))
            : coincide ? new SolidColorBrush(Estilo.ConAlfa(Estilo.Lima, 0x38))
            : directa && !f.SinTarjeta ? new SolidColorBrush(Estilo.ConAlfa(Estilo.Oro, 0x26)) : null;
        var marco = r.Inflate(Aire);
        if (halo != null) ctx.DrawRectangle(halo, null, new RoundedRect(marco.Inflate(7), e.Radio + 7));
        if (!f.SinTarjeta)
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)), null, new RoundedRect(r.Translate(new Vector(0, 5)), e.Radio));
            ctx.DrawRectangle(t.FondoTarjeta, null, caja);
            // la franja del color de su sexo (más gruesa sin foto)
            var franjaColor = new SolidColorBrush(Estilo.ConAlfa(e.Acento, politica ? (byte)0x73 : (byte)0xF2));
            double g = f.Foto ? 4 : 7;
            var franja = f.Vertical
                ? new Rect(r.X + (f.Foto ? 30 : 22), r.Y + 9, r.Width - 2 * (f.Foto ? 30 : 22), g)
                : f.Foto ? new Rect(r.X + 10, r.Y + 20, g, r.Height - 40) : new Rect(r.X + 9, r.Y + 9, g, r.Height - 18);
            ctx.DrawRectangle(franjaColor, null, new RoundedRect(franja, g / 2));
        }

        var (zona, foto) = Zonas(r, t.Enlace && f.Foto && !f.SinTarjeta);
        if (foto is { } fr)
        {
            var circulo = fr.Deflate(2);
            if (t.Foto is { } bmp)
            {
                using (ctx.PushClip(new RoundedRect(circulo, circulo.Width / 2)))
                {
                    double lado = Math.Min(bmp.Size.Width, bmp.Size.Height);
                    var origen = new Rect((bmp.Size.Width - lado) / 2, (bmp.Size.Height - lado) / 2, lado, lado);
                    ctx.DrawImage(bmp, origen, circulo);
                }
            }
            else
            {
                ctx.DrawEllipse(new SolidColorBrush(Estilo.ConAlfa(e.Acento, 0x55)), null, circulo);
                if (t.Iniciales is { } ini)
                    ctx.DrawText(ini, new Point(circulo.Center.X - ini.Width / 2, circulo.Center.Y - ini.Height / 2));
            }
            ctx.DrawEllipse(null, new Pen(new SolidColorBrush(e.Acento), 2), fr.Center, fr.Width / 2 - 1, fr.Height / 2 - 1);
        }

        if (t.Nombre is { } nom)
        {
            var (pn, pa) = PosicionTextos(t, zona);
            ctx.DrawText(nom, pn);
            if (t.Apellidos is { } ap && pa is { } p2) ctx.DrawText(ap, p2);
        }

        // el botón de su árbol enlazado (con foto y con tarjeta: si no, taparía el nombre)
        if (t.Enlace && f.Foto && !f.SinTarjeta)
        {
            var c = BotonEnlace(r);
            ctx.DrawEllipse(new SolidColorBrush(Estilo.ConAlfa(Estilo.Oro, 0x30)), null, c, 12, 12);
            var lp = new Pen(new SolidColorBrush(Estilo.Oro), 1.8) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            ctx.DrawLine(lp, new Point(c.X - 4, c.Y + 4), new Point(c.X + 4.5, c.Y - 4.5));
            ctx.DrawLine(lp, new Point(c.X - 0.5, c.Y - 4.5), new Point(c.X + 4.5, c.Y - 4.5));
            ctx.DrawLine(lp, new Point(c.X + 4.5, c.Y - 4.5), new Point(c.X + 4.5, c.Y + 0.5));
        }

        // contorno: blanco si está seleccionada, lima si coincide con la búsqueda, dorado en la línea directa; discontinuo
        // en la familia política
        Color color; double grosor;
        if (sel) { color = Colors.White; grosor = 2.5; }
        else if (coincide) { color = Estilo.Lima; grosor = 3; }
        else if (directa) { color = Estilo.Oro; grosor = 2.5; }
        else { color = e.Borde; grosor = 1.5; }
        if (!(f.SinTarjeta && !sel && !coincide))
        {
            var pen = new Pen(new SolidColorBrush(color), grosor);
            if (politica && !sel) pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
            ctx.DrawRectangle(null, pen, new RoundedRect(marco.Deflate(0.75), Math.Max(0, e.Radio - 0.75)));
        }
    }

    // ---------- Imagen ----------

    /// <summary>El árbol entero como imagen (para compartirla), con el fondo de la pantalla y sin botones.</summary>
    public RenderTargetBitmap Imagen(double escala)
    {
        var lay = _layout ?? throw new InvalidOperationException("No hay árbol.");
        // que no pase de unos 60 megapíxeles (los teléfonos no podrían con más)
        escala = Math.Min(escala, Math.Sqrt(60e6 / Math.Max(1, lay.Ancho * lay.Alto)));
        var tam = new PixelSize(Math.Max(1, (int)Math.Ceiling(lay.Ancho * escala)), Math.Max(1, (int)Math.Ceiling(lay.Alto * escala)));
        var rtb = new RenderTargetBitmap(tam, new Vector(96, 96));
        var sel = _sel; var res = _res;
        _sel = null; _res = Resaltado.Ninguno;
        try
        {
            using var ctx = rtb.CreateDrawingContext();
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x13, 0x1C)), new Rect(0, 0, tam.Width, tam.Height));
            using (ctx.PushTransform(Matrix.CreateScale(escala, escala)))
                DibujarMundo(ctx, new Rect(0, 0, lay.Ancho, lay.Alto), false, conBotones: false);
        }
        finally { _sel = sel; _res = res; }
        return rtb;
    }
}
