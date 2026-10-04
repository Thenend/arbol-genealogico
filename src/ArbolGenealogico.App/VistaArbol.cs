using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

public enum Direccion { Izquierda, Derecha, Arriba, Abajo }

/// <summary>Lienzo del árbol: tarjetas, aristas, selección, zoom y desplazamiento.</summary>
public sealed class VistaArbol : Grid
{
    public const double EscalaMin = 0.06, EscalaMax = 2.5;

    public static readonly DependencyProperty EscalaProperty = DependencyProperty.Register(
        nameof(Escala), typeof(double), typeof(VistaArbol), new PropertyMetadata(1.0, (d, _) => ((VistaArbol)d).AplicarCamara()));
    public static readonly DependencyProperty TxProperty = DependencyProperty.Register(
        nameof(Tx), typeof(double), typeof(VistaArbol), new PropertyMetadata(0.0, (d, _) => ((VistaArbol)d).AplicarCamara()));
    public static readonly DependencyProperty TyProperty = DependencyProperty.Register(
        nameof(Ty), typeof(double), typeof(VistaArbol), new PropertyMetadata(0.0, (d, _) => ((VistaArbol)d).AplicarCamara()));

    public double Escala { get => (double)GetValue(EscalaProperty); set => SetValue(EscalaProperty, value); }
    public double Tx { get => (double)GetValue(TxProperty); set => SetValue(TxProperty, value); }
    public double Ty { get => (double)GetValue(TyProperty); set => SetValue(TyProperty, value); }

    private readonly Canvas _mundo = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly AristasVisual _aristas = new();
    private readonly Rectangle _puntos = new() { IsHitTestVisible = false };
    private readonly DrawingBrush _pincelPuntos;
    private readonly MatrixTransform _transformacion = new();
    private readonly Dictionary<string, TarjetaPersona> _tarjetas = new();
    private readonly Dictionary<string, Point> _destinos = new();

    private bool _pulsado, _arrastrando, _panMedio;
    private Point _inicioRaton;
    private double _inicioTx, _inicioTy;
    private bool _ajustarPendiente;
    private string? _seleccion;
    private HashSet<string> _coincidencias = new();

    public Arbol? Arbol { get; private set; }
    public LayoutResult? Layout { get; private set; }

    public event Action? CamaraCambiada;
    public event Action<string?>? SeleccionCambiada;
    public event Action<string>? EditarSolicitado;
    public event Action<string, FrameworkElement>? MenuSolicitado;
    public event Action<string>? AbrirEnlaceSolicitado;

    public VistaArbol()
    {
        ClipToBounds = true;
        Focusable = true;
        Background = new RadialGradientBrush(Color.FromRgb(0x16, 0x1A, 0x26), Color.FromRgb(0x0A, 0x0C, 0x11)) { RadiusX = 0.9, RadiusY = 0.9, Center = new Point(0.5, 0.45), GradientOrigin = new Point(0.5, 0.45) };

        // Rejilla de puntos que se mueve con la cámara.
        var punto = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x24, 0x29, 0x37)), null, new EllipseGeometry(new Point(20, 20), 1.6, 1.6));
        var cuadro = new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 40, 40)));
        var dibujo = new DrawingGroup(); dibujo.Children.Add(cuadro); dibujo.Children.Add(punto);
        _pincelPuntos = new DrawingBrush(dibujo) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 40, 40), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 40, 40), ViewboxUnits = BrushMappingMode.Absolute };
        _puntos.Fill = _pincelPuntos;

        _mundo.RenderTransform = _transformacion;
        _mundo.Children.Add(_aristas);
        Children.Add(_puntos);
        Children.Add(_mundo);

        SizeChanged += (_, _) => { if (_ajustarPendiente && ActualWidth > 0) { _ajustarPendiente = false; VistaInicial(); } CamaraCambiada?.Invoke(); };
        PreviewMouseLeftButtonDown += AlPulsar;
        PreviewMouseLeftButtonUp += AlSoltar;
        PreviewMouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { IniciarPan(e, true); e.Handled = true; } };
        PreviewMouseUp += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { FinPan(); e.Handled = true; } };
        PreviewMouseMove += AlMover;
        PreviewMouseRightButtonUp += AlClicDerecho;
        MouseWheel += AlRueda;
        AplicarCamara();
    }

    // ---------- Datos ----------
    public void Cargar(Arbol arbol, bool ajustar)
    {
        Arbol = arbol;
        if (_seleccion != null && arbol.Buscar(_seleccion) == null) _seleccion = null;
        Refrescar(false);
        if (ajustar) { if (ActualWidth > 0) VistaInicial(); else _ajustarPendiente = true; }
    }

    public void Refrescar(bool animar = true)
    {
        if (Arbol == null) return;
        Layout = LayoutEngine.Calcular(Arbol);
        var directa = Arbol.LineaDirecta();
        var o = Layout.Opciones;
        bool animarTarjetas = animar && IsLoaded;

        var claves = Layout.TodasLasCartas.Select(c => c.Clave).ToHashSet();
        foreach (var k in _tarjetas.Keys.Where(k => !claves.Contains(k)).ToList())
        {
            _mundo.Children.Remove(_tarjetas[k]);
            _tarjetas.Remove(k); _destinos.Remove(k);
        }
        var conCopias = Layout.Copias.Select(c => c.Id).ToHashSet();

        // Tarjeta principal de cada persona (clave = su id) y, si hace falta, copias junto a sus parejas (clave "id~unión").
        foreach (var pos in Layout.TodasLasCartas)
        {
            var id = pos.Id; var clave = pos.Clave;
            var p = Arbol.Obtener(id);
            bool nueva = !_tarjetas.TryGetValue(clave, out var t);
            if (nueva)
            {
                t = new TarjetaPersona(id, o.AnchoCarta, o.AltoCarta) { Clave = clave };
                t.MasClic += c => MenuSolicitado?.Invoke(c.PersonaId, c);
                t.EnlaceClic += c => AbrirEnlaceSolicitado?.Invoke(c.PersonaId);
                t.CopiaClic += IrALaOtraTarjeta;
                _tarjetas[clave] = t;
                _mundo.Children.Add(t);
            }
            t!.Actualizar(p, directa.Contains(id), pos.Politica, pos.EsCopia, conCopias.Contains(id));
            t.Seleccionada = id == _seleccion;
            t.Coincidencia = _coincidencias.Contains(id);

            double izq = pos.X - o.AnchoCarta / 2, arr = pos.Y;
            Canvas.SetLeft(t, izq); Canvas.SetTop(t, arr);
            if (animarTarjetas && !nueva && _destinos.TryGetValue(clave, out var antes) && (Math.Abs(antes.X - izq) > 0.5 || Math.Abs(antes.Y - arr) > 0.5))
            {
                Animar(t, Canvas.LeftProperty, antes.X, izq);
                Animar(t, Canvas.TopProperty, antes.Y, arr);
            }
            else if (animarTarjetas && nueva)
            {
                t.AparecerSuavemente();
            }
            _destinos[clave] = new Point(izq, arr);
        }

        _mundo.Width = Layout.Ancho; _mundo.Height = Layout.Alto;
        _aristas.Width = Layout.Ancho; _aristas.Height = Layout.Alto;
        _aristas.Layout = Layout;
        ActualizarLinaje();
        if (animarTarjetas)
            _aristas.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(100) });
        CamaraCambiada?.Invoke();
    }

    private static void Animar(UIElement t, DependencyProperty dp, double desde, double hasta)
    {
        var a = new DoubleAnimation(desde, hasta, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        a.Completed += (_, _) => t.BeginAnimation(dp, null);
        t.BeginAnimation(dp, a);
    }

    // ---------- Selección ----------
    public string? SeleccionId
    {
        get => _seleccion;
        set
        {
            if (_seleccion == value) return;
            _seleccion = value;
            foreach (var t in _tarjetas.Values) t.Seleccionada = t.PersonaId == value;
            ActualizarLinaje();
            SeleccionCambiada?.Invoke(value);
        }
    }

    /// <summary>Ilumina las conexiones que unen a la persona seleccionada con sus antepasados y descendientes.</summary>
    private void ActualizarLinaje()
    {
        var res = new HashSet<Conexion>();
        var cables = new List<(Point a, Point b)>();
        HashSet<string>? familia = null;
        if (Arbol != null && Layout != null && _seleccion != null && Arbol.Buscar(_seleccion) != null)
        {
            var (antepasados, descendientes) = Arbol.Linaje(_seleccion);
            familia = Arbol.FamiliaDirecta(_seleccion);
            antepasados.Add(_seleccion); descendientes.Add(_seleccion);
            var uniones = Arbol.Uniones.ToDictionary(u => u.Id);
            var conLinaje = new HashSet<string>();
            foreach (var c in Layout.Conexiones.Where(c => c.Tipo == TipoConexion.Descendencia))
            {
                if (!uniones.TryGetValue(c.UnionId, out var u)) continue;
                bool sube = c.HijoId != null && antepasados.Contains(c.HijoId);   // de la persona (o un antepasado) hacia sus padres
                bool baja = u.Parejas.Any(descendientes.Contains);                // de la persona (o un descendiente) hacia sus hijos
                if (sube || baja) { res.Add(c); conLinaje.Add(c.UnionId); }
            }
            foreach (var c in Layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && conLinaje.Contains(c.UnionId))) res.Add(c);
            // Si alguien del linaje aparece repetido (junto a su pareja y con su familia de origen), se unen sus dos tarjetas.
            var linaje = new HashSet<string>(antepasados); linaje.UnionWith(descendientes);
            foreach (var copia in Layout.Copias.Where(c => linaje.Contains(c.Id)))
                if (Layout.Cartas.TryGetValue(copia.Id, out var principal))
                    cables.Add((new Point(principal.X, principal.Y), new Point(copia.X, copia.Y)));
        }
        _aristas.Resaltadas = res;
        _aristas.Cables = cables;
        // Quien no es de su familia directa se oscurece (sin selección, todo se ve con normalidad).
        foreach (var t in _tarjetas.Values) t.Atenuada = familia != null && !familia.Contains(t.PersonaId);
    }

    /// <summary>Persona cuya tarjeta está en el punto dado (coordenadas de este control), o null.</summary>
    public string? PersonaEn(Point p)
    {
        var impacto = VisualTreeHelper.HitTest(this, p);
        return TarjetaDe(impacto?.VisualHit)?.PersonaId;
    }

    public void ResaltarDestino(string? id)
    {
        foreach (var t in _tarjetas.Values) t.DestinoDrop = t.PersonaId == id;
    }

    /// <summary>Marca con un anillo las tarjetas que coinciden con la búsqueda.</summary>
    public void ResaltarCoincidencias(IEnumerable<string> ids)
    {
        _coincidencias = new HashSet<string>(ids);
        foreach (var t in _tarjetas.Values) t.Coincidencia = _coincidencias.Contains(t.PersonaId);
    }

    /// <summary>Ordena personas en orden de lectura del dibujo: de arriba abajo y de izquierda a derecha.</summary>
    public List<string> OrdenDeLectura(IEnumerable<string> ids)
    {
        if (Layout == null) return ids.ToList();
        return ids.OrderBy(id => Layout.Cartas.TryGetValue(id, out var c) ? Math.Round(c.Y) : double.MaxValue)
                  .ThenBy(id => Layout.Cartas.TryGetValue(id, out var c) ? c.X : double.MaxValue)
                  .ToList();
    }

    /// <summary>Salta de una tarjeta de la persona a la otra (la principal con su familia de origen y la copia junto a su pareja).</summary>
    private void IrALaOtraTarjeta(TarjetaPersona actual)
    {
        if (Layout == null) return;
        var todas = _tarjetas.Values.Where(t => t.PersonaId == actual.PersonaId)
            .OrderBy(t => t.Clave == t.PersonaId ? 0 : 1).ThenBy(t => t.Clave, StringComparer.Ordinal).ToList();
        if (todas.Count < 2) return;
        var siguiente = todas[(todas.IndexOf(actual) + 1) % todas.Count];
        if (!_destinos.TryGetValue(siguiente.Clave, out var d)) return;
        SeleccionId = actual.PersonaId;
        CentrarEnPunto(d.X + Layout.Opciones.AnchoCarta / 2, d.Y + Layout.Opciones.AltoCarta / 2, true, Math.Max(Escala, 0.7));
    }

    public TarjetaPersona? TarjetaDe(string id) => _tarjetas.TryGetValue(id, out var t) ? t : null;

    /// <summary>
    /// Persona a la que se llega con una flecha: a los lados, la más cercana de la misma fila; hacia arriba, un padre;
    /// hacia abajo, un hijo (el más cercano en horizontal). Si no hay padres o hijos, la más cercana de la fila contigua.
    /// </summary>
    public string? Vecino(string id, Direccion direccion)
    {
        if (Layout == null || Arbol == null || !Layout.Cartas.TryGetValue(id, out var c)) return null;
        var cartas = Layout.Cartas.Values;
        double Dist(CartaPos o) => Math.Abs(o.X - c.X);

        if (direccion is Direccion.Izquierda or Direccion.Derecha)
        {
            bool der = direccion == Direccion.Derecha;
            return cartas.Where(o => Math.Abs(o.Y - c.Y) < 1 && (der ? o.X > c.X + 1 : o.X < c.X - 1))
                         .OrderBy(Dist).FirstOrDefault()?.Id;
        }

        bool arriba = direccion == Direccion.Arriba;
        IEnumerable<string> parientes = arriba
            ? (Arbol.UnionComoHijo(id)?.Parejas ?? new List<string>())
            : Arbol.UnionesComoPareja(id).SelectMany(u => u.Hijos);
        var directo = parientes.Where(Layout.Cartas.ContainsKey).Select(p => Layout.Cartas[p]).OrderBy(Dist).FirstOrDefault();
        if (directo != null) return directo.Id;

        var filas = cartas.Where(o => arriba ? o.Y < c.Y - 1 : o.Y > c.Y + 1).ToList();
        if (filas.Count == 0) return null;
        double fila = arriba ? filas.Max(o => o.Y) : filas.Min(o => o.Y);
        return filas.Where(o => Math.Abs(o.Y - fila) < 1).OrderBy(Dist).First().Id;
    }

    /// <summary>Desplaza lo justo la vista para que la persona quede visible, con un margen.</summary>
    public void MostrarPersona(string id, bool animar = true, double margen = 90)
    {
        var rr = RectDe(id);
        if (rr == null || ActualWidth <= 0) return;
        var r = rr.Value; var v = Visible;
        double m = margen / Escala, dx = 0, dy = 0;
        if (r.Left - m < v.Left) dx = r.Left - m - v.Left; else if (r.Right + m > v.Right) dx = r.Right + m - v.Right;
        if (r.Top - m < v.Top) dy = r.Top - m - v.Top; else if (r.Bottom + m > v.Bottom) dy = r.Bottom + m - v.Bottom;
        if (dx == 0 && dy == 0) return;
        IrA(Escala, Tx - dx * Escala, Ty - dy * Escala, animar);
    }

    public void Desplazar(double dx, double dy) => IrA(Escala, Tx + dx, Ty + dy, false);

    public Rect? RectDe(string id)
    {
        if (Layout == null || !Layout.Cartas.TryGetValue(id, out var c)) return null;
        var o = Layout.Opciones;
        return new Rect(c.X - o.AnchoCarta / 2, c.Y, o.AnchoCarta, o.AltoCarta);
    }

    // ---------- Cámara ----------
    private void AplicarCamara()
    {
        var m = new Matrix(Escala, 0, 0, Escala, Tx, Ty);
        _transformacion.Matrix = m;
        _pincelPuntos.Transform = new MatrixTransform(m);
        CamaraCambiada?.Invoke();
    }

    public void RestablecerCamara() { Escala = 1; Tx = 0; Ty = 0; }

    /// <summary>Rectángulo del mundo visible en pantalla.</summary>
    public Rect Visible => new(-Tx / Escala, -Ty / Escala, ActualWidth / Escala, ActualHeight / Escala);

    public void IrA(double escala, double tx, double ty, bool animar)
    {
        escala = Math.Clamp(escala, EscalaMin, EscalaMax);
        double e0 = Escala, x0 = Tx, y0 = Ty;      // valores actuales (también si hay una animación en curso)
        DetenerAnimaciones();
        // El valor final se guarda siempre como valor real de la cámara: la animación solo pinta el recorrido.
        // Así, si se interrumpe (clic, rueda...) o termina, la vista se queda donde debe y no vuelve atrás.
        Escala = escala; Tx = tx; Ty = ty;
        if (!animar) return;
        var d = TimeSpan.FromMilliseconds(380);
        var f = new CubicEase { EasingMode = EasingMode.EaseInOut };
        BeginAnimation(EscalaProperty, new DoubleAnimation(e0, escala, d) { EasingFunction = f });
        BeginAnimation(TxProperty, new DoubleAnimation(x0, tx, d) { EasingFunction = f });
        BeginAnimation(TyProperty, new DoubleAnimation(y0, ty, d) { EasingFunction = f });
    }

    /// <summary>Para las animaciones de la cámara dejando la vista exactamente donde está en este momento.</summary>
    private void DetenerAnimaciones()
    {
        double e = Escala, x = Tx, y = Ty;
        BeginAnimation(EscalaProperty, null); BeginAnimation(TxProperty, null); BeginAnimation(TyProperty, null);
        Escala = e; Tx = x; Ty = y;
    }

    public void Ajustar(bool animar = true)
    {
        if (Layout == null || ActualWidth <= 0) return;
        double pad = 40;
        double s = Math.Min((ActualWidth - 2 * pad) / Layout.Ancho, (ActualHeight - 2 * pad) / Layout.Alto);
        s = Math.Clamp(Math.Min(s, 1.0), EscalaMin, EscalaMax);
        IrA(s, (ActualWidth - Layout.Ancho * s) / 2, (ActualHeight - Layout.Alto * s) / 2, animar);
    }

    /// <summary>Vista al abrir un árbol: todo si cabe con comodidad; si no, la persona principal con zoom legible.</summary>
    public void VistaInicial()
    {
        if (Layout == null || Arbol == null || ActualWidth <= 0) return;
        double pad = 40;
        double fit = Math.Min((ActualWidth - 2 * pad) / Layout.Ancho, (ActualHeight - 2 * pad) / Layout.Alto);
        if (fit >= 0.7) Ajustar(false);
        else CentrarEn(Arbol.RaizId, 0.8, false);
    }

    public void CentrarEn(string id, double? escala = null, bool animar = true)
    {
        var r = RectDe(id);
        if (r == null) return;
        double s = escala ?? Math.Max(Escala, 0.6);
        CentrarEnPunto(r.Value.X + r.Value.Width / 2, r.Value.Y + r.Value.Height / 2, animar, s);
    }

    public void CentrarEnPunto(double x, double y, bool animar, double? escala = null)
    {
        double s = escala ?? Escala;
        IrA(s, ActualWidth / 2 - x * s, ActualHeight / 2 - y * s, animar);
    }

    public void Zoom(double factor)
    {
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        ZoomEn(c, factor, true);
    }

    private void ZoomEn(Point p, double factor, bool animar)
    {
        double ns = Math.Clamp(Escala * factor, EscalaMin, EscalaMax);
        double k = ns / Escala;
        IrA(ns, p.X - (p.X - Tx) * k, p.Y - (p.Y - Ty) * k, animar);
    }

    // ---------- Ratón ----------
    private void AlRueda(object sender, MouseWheelEventArgs e)
    {
        DetenerAnimaciones();
        ZoomEn(e.GetPosition(this), Math.Pow(1.0014, e.Delta), false);
        e.Handled = true;
    }

    private void IniciarPan(MouseButtonEventArgs e, bool medio)
    {
        DetenerAnimaciones();
        _pulsado = true; _panMedio = medio; _arrastrando = medio;
        _inicioRaton = e.GetPosition(this); _inicioTx = Tx; _inicioTy = Ty;
        if (medio) { CaptureMouse(); Cursor = Cursors.SizeAll; }
    }

    private static TarjetaPersona? TarjetaDe(DependencyObject? o)
    {
        while (o != null)
        {
            if (o is TarjetaPersona t) return t;
            o = o is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(o) : LogicalTreeHelper.GetParent(o);
        }
        return null;
    }

    private void AlPulsar(object sender, MouseButtonEventArgs e)
    {
        Focus();
        IniciarPan(e, false);
        var t = TarjetaDe(e.OriginalSource as DependencyObject);
        if (t != null && e.ClickCount == 2 && !(e.OriginalSource is DependencyObject d && EsBoton(d)))
        {
            _pulsado = false;
            SeleccionId = t.PersonaId;
            EditarSolicitado?.Invoke(t.PersonaId);
            e.Handled = true;
        }
    }

    private static bool EsBoton(DependencyObject o)
    {
        while (o != null) { if (o is Button) return true; if (o is TarjetaPersona) return false; o = VisualTreeHelper.GetParent(o); }
        return false;
    }

    private void AlMover(object sender, MouseEventArgs e)
    {
        if (!_pulsado) return;
        var p = e.GetPosition(this);
        if (!_arrastrando)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { _pulsado = false; return; }
            if (Math.Abs(p.X - _inicioRaton.X) + Math.Abs(p.Y - _inicioRaton.Y) < 5) return;
            _arrastrando = true; CaptureMouse(); Cursor = Cursors.SizeAll;
        }
        Tx = _inicioTx + (p.X - _inicioRaton.X);
        Ty = _inicioTy + (p.Y - _inicioRaton.Y);
    }

    private void AlSoltar(object sender, MouseButtonEventArgs e)
    {
        bool fueArrastre = _arrastrando;
        FinPan();
        if (fueArrastre) { e.Handled = true; return; }
        var t = TarjetaDe(e.OriginalSource as DependencyObject);
        SeleccionId = t?.PersonaId;
    }

    private void FinPan()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        _pulsado = false; _arrastrando = false; _panMedio = false;
        Cursor = null;
    }

    private void AlClicDerecho(object sender, MouseButtonEventArgs e)
    {
        var t = TarjetaDe(e.OriginalSource as DependencyObject);
        if (t == null) return;
        SeleccionId = t.PersonaId;
        MenuSolicitado?.Invoke(t.PersonaId, t);
        e.Handled = true;
    }
}
