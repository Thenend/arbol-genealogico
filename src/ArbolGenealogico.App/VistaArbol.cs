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
    public const double EscalaMin = 0.03, EscalaMax = 2.5;

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
    private readonly GuiaImpresion _guia = new() { Visibility = Visibility.Collapsed };
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

    /// <summary>Algoritmo de colocación (Compacto, Lateral o Balanceado). Al cambiarlo, las tarjetas se deslizan a su nuevo sitio.</summary>
    public Ordenacion Ordenacion
    {
        get => _ordenacion;
        set
        {
            if (_ordenacion == value) return;
            CambiarSinPerderElSitio(() => _ordenacion = value, true);
        }
    }
    private Ordenacion _ordenacion = Ordenacion.C;

    /// <summary>Forma de las tarjetas (horizontal o vertical, con foto o sin ella, letra pequeña o grande).</summary>
    public FormaTarjeta Tarjetas
    {
        get => _tipoTarjeta;
        set
        {
            if (_tipoTarjeta == value) return;
            CambiarSinPerderElSitio(() =>
            {
                _tipoTarjeta = value;
                // Las tarjetas se rehacen con la nueva forma.
                foreach (var t in _tarjetas.Values) _mundo.Children.Remove(t);
                _tarjetas.Clear(); _destinos.Clear();
            }, false);
        }
    }
    private FormaTarjeta _tipoTarjeta = FormaTarjeta.Ancha;

    /// <summary>Muestra detrás del árbol la mejor forma de imprimirlo en <see cref="PapelGuia"/> × <see cref="HojasGuia"/>.</summary>
    public bool GuiaVisible
    {
        get => _guia.Visibility == Visibility.Visible;
        set { _guia.Visibility = value ? Visibility.Visible : Visibility.Collapsed; GuiaCambiada?.Invoke(); }
    }
    /// <summary>"A4", "A3", "A2" o "A1".</summary>
    public string PapelGuia { get => _guia.Papel; set { _guia.Papel = value; GuiaCambiada?.Invoke(); } }
    public int HojasGuia { get => _guia.Hojas; set { _guia.Hojas = value; GuiaCambiada?.Invoke(); } }
    /// <summary>Que los cortes entre hojas busquen huecos sin tarjetas (si no, hojas iguales al tamaño máximo).</summary>
    public bool EvitarCortesGuia { get => _guia.EvitarCortes; set { _guia.EvitarCortes = value; GuiaCambiada?.Invoke(); } }
    /// <summary>La configuración de impresión que se está mostrando (o null).</summary>
    public GuiaImpresion.Configuracion? GuiaActual => GuiaVisible ? GuiaImpresion.Mejor(Layout, _guia.Papel, _guia.Hojas, _guia.EvitarCortes) : null;
    /// <summary>Alto de lo que se superpone a la vista por arriba (el panel de la guía), para dejarle sitio al encuadrar.</summary>
    public double MargenSuperior { get; set; }

    /// <summary>Ha cambiado lo que muestra la guía de impresión (también al recolocar el árbol).</summary>
    public event Action? GuiaCambiada;

    /// <summary>
    /// Aplica un cambio que recoloca el árbol (ordenación, forma de las tarjetas). Viendo el árbol entero y sin nadie
    /// seleccionado, se ve entero también después; si no, la persona seleccionada (o, sin selección, la más cercana al
    /// centro de la pantalla) se queda en el mismo sitio.
    /// </summary>
    private void CambiarSinPerderElSitio(Action cambio, bool animar)
    {
        bool entero = _seleccion == null && ArbolEnteroVisible();
        var id = _seleccion ?? PersonaMasCentrada()?.Id;
        var antes = id != null ? RectDe(id) : null;
        cambio();
        Refrescar(animar);
        if (entero) { Ajustar(animar); return; }
        var despues = id != null ? RectDe(id) : null;
        if (antes is { } a && despues is { } d && ActualWidth > 0)
        {
            // Sobre el destino de la cámara (no sobre el punto por el que va una animación en curso): así dos cambios
            // seguidos se compensan y la vista vuelve exactamente a donde estaba. Se mantiene el centro de su tarjeta.
            double e = (double)GetAnimationBaseValue(EscalaProperty);
            double tx = (double)GetAnimationBaseValue(TxProperty), ty = (double)GetAnimationBaseValue(TyProperty);
            double dx = (a.X + a.Width / 2) - (d.X + d.Width / 2), dy = (a.Y + a.Height / 2) - (d.Y + d.Height / 2);
            IrA(e, tx + dx * e, ty + dy * e, animar);
        }
    }

    /// <summary>El árbol cabe (casi) entero en la pantalla, según el destino de la cámara aunque haya una animación en curso.</summary>
    private bool ArbolEnteroVisible()
    {
        if (Layout == null || ActualWidth <= 0 || Layout.Ancho <= 0 || Layout.Alto <= 0) return false;
        double e = (double)GetAnimationBaseValue(EscalaProperty);
        double tx = (double)GetAnimationBaseValue(TxProperty), ty = (double)GetAnimationBaseValue(TyProperty);
        var visible = new Rect(-tx / e, -ty / e, ActualWidth / e, ActualHeight / e);
        var arbol = new Rect(0, 0, Layout.Ancho, Layout.Alto);
        var comun = Rect.Intersect(visible, arbol);
        return !comun.IsEmpty && comun.Width >= 0.9 * arbol.Width && comun.Height >= 0.9 * arbol.Height;
    }

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
        _mundo.Children.Add(_guia);
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
        Layout = LayoutEngine.Calcular(Arbol, new LayoutOptions
        {
            Ordenacion = _ordenacion,
            AnchoCarta = _tipoTarjeta.Tamano.Width,
            AltoCarta = _tipoTarjeta.Tamano.Height,
            // con las tarjetas sin foto (las más pequeñas), menos hueco entre filas y en las parejas: más árbol en el mismo papel
            HuecoFilas = _tipoTarjeta.Foto ? new LayoutOptions().HuecoFilas : 74,
            HuecoPareja = _tipoTarjeta.Foto ? new LayoutOptions().HuecoPareja : 26,
        });
        var directa = Arbol.LineaDirecta();
        var o = Layout.Opciones;
        bool animarTarjetas = animar && IsLoaded;

        foreach (var id in _tarjetas.Keys.Where(k => !Layout.Cartas.ContainsKey(k)).ToList())
        {
            _mundo.Children.Remove(_tarjetas[id]);
            _tarjetas.Remove(id); _destinos.Remove(id);
        }

        foreach (var (id, pos) in Layout.Cartas)
        {
            var p = Arbol.Obtener(id);
            bool nueva = !_tarjetas.TryGetValue(id, out var t);
            if (nueva)
            {
                t = new TarjetaPersona(id, _tipoTarjeta, _claro);
                t.MasClic += c => MenuSolicitado?.Invoke(c.PersonaId, c);
                t.EnlaceClic += c => AbrirEnlaceSolicitado?.Invoke(c.PersonaId);
                _tarjetas[id] = t;
                _mundo.Children.Add(t);
            }
            t!.Actualizar(p, directa.Contains(id), pos.Politica);
            t.Seleccionada = id == _seleccion;
            t.Coincidencia = _coincidencias.Contains(id);

            double izq = pos.X - o.AnchoCarta / 2, arr = pos.Y;
            Canvas.SetLeft(t, izq); Canvas.SetTop(t, arr);
            if (animarTarjetas && !nueva && _destinos.TryGetValue(id, out var antes) && (Math.Abs(antes.X - izq) > 0.5 || Math.Abs(antes.Y - arr) > 0.5))
            {
                Animar(t, Canvas.LeftProperty, antes.X, izq);
                Animar(t, Canvas.TopProperty, antes.Y, arr);
            }
            else if (animarTarjetas && nueva)
            {
                t.AparecerSuavemente();
            }
            _destinos[id] = new Point(izq, arr);
        }

        _mundo.Width = Layout.Ancho; _mundo.Height = Layout.Alto;
        _aristas.Width = Layout.Ancho; _aristas.Height = Layout.Alto;
        _aristas.Layout = Layout;
        _guia.Layout = Layout;
        GuiaCambiada?.Invoke();
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
            foreach (var (id, t) in _tarjetas) t.Seleccionada = id == value;
            ActualizarLinaje();
            SeleccionCambiada?.Invoke(value);
        }
    }

    /// <summary>Ilumina las conexiones que unen a la persona seleccionada con sus antepasados y descendientes.</summary>
    private void ActualizarLinaje()
    {
        var res = new HashSet<Conexion>();
        var cercanas = new HashSet<Conexion>();
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
            // Las que llevan a otros familiares cercanos que se ven (hermanos, cónyuges) tampoco se oscurecen.
            foreach (var c in Layout.Conexiones)
            {
                if (res.Contains(c) || !uniones.TryGetValue(c.UnionId, out var u) || !u.Parejas.Any(familia.Contains)) continue;
                bool cerca = c.Tipo == TipoConexion.Pareja
                    ? u.Parejas.All(familia.Contains) || u.Hijos.Any(familia.Contains)   // su cónyuge, o los padres de un medio hermano
                    : c.HijoId != null && familia.Contains(c.HijoId);                    // hacia un hermano o medio hermano
                if (cerca) cercanas.Add(c);
            }
        }
        _aristas.Resaltar(res, cercanas);
        // Quien no es de su familia directa se oscurece (sin selección, todo se ve con normalidad).
        foreach (var (id, t) in _tarjetas) t.Atenuada = familia != null && !familia.Contains(id);
    }

    /// <summary>Persona cuya tarjeta está en el punto dado (coordenadas de este control), o null.</summary>
    public string? PersonaEn(Point p)
    {
        var impacto = VisualTreeHelper.HitTest(this, p);
        return TarjetaDe(impacto?.VisualHit)?.PersonaId;
    }

    public void ResaltarDestino(string? id)
    {
        foreach (var (k, t) in _tarjetas) t.DestinoDrop = k == id;
    }

    /// <summary>Marca con un anillo las tarjetas que coinciden con la búsqueda.</summary>
    public void ResaltarCoincidencias(IEnumerable<string> ids)
    {
        _coincidencias = new HashSet<string>(ids);
        foreach (var (id, t) in _tarjetas) t.Coincidencia = _coincidencias.Contains(id);
    }

    /// <summary>Ordena personas en orden de lectura del dibujo: de arriba abajo y de izquierda a derecha.</summary>
    public List<string> OrdenDeLectura(IEnumerable<string> ids)
    {
        if (Layout == null) return ids.ToList();
        return ids.OrderBy(id => Layout.Cartas.TryGetValue(id, out var c) ? Math.Round(c.Y) : double.MaxValue)
                  .ThenBy(id => Layout.Cartas.TryGetValue(id, out var c) ? c.X : double.MaxValue)
                  .ToList();
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

    /// <summary>Para imprimir: fondo blanco y sin la rejilla de puntos; con <paramref name="claro"/>, tarjetas y líneas en estilo claro.</summary>
    public void PrepararParaImprimir(bool claro)
    {
        Background = Brushes.White;
        _puntos.Visibility = Visibility.Collapsed;
        _claro = claro;
        _aristas.Claro = claro;
    }
    private bool _claro;

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
        // Lo que hay que ver: el árbol y, si se muestran, las hojas de la guía de impresión.
        var caja = new Rect(0, 0, Layout.Ancho, Layout.Alto);
        if (GuiaActual is { } g) caja.Union(g.Pegado);
        // Arriba, el sitio que ocupa lo que se superpone a la vista (el panel de la guía de impresión).
        double arriba = pad + MargenSuperior, alto = ActualHeight - arriba - pad;
        double s = Math.Min((ActualWidth - 2 * pad) / caja.Width, alto / caja.Height);
        s = Math.Clamp(Math.Min(s, 1.0), EscalaMin, EscalaMax);
        IrA(s, (ActualWidth - caja.Width * s) / 2 - caja.X * s, arriba + (alto - caja.Height * s) / 2 - caja.Y * s, animar);
    }

    /// <summary>Vista que se pondrá al abrir el árbol (la de la última vez); si su persona ya no está, se usa la de por defecto.</summary>
    public VistaGuardada? VistaPendiente { get; set; }

    /// <summary>
    /// Vista al abrir un árbol: la guardada, si la hay; si no, todo el árbol si cabe con comodidad, y si no, la persona
    /// principal con sus padres, abuelos, hermanos e hijos tan a la vista como permita un zoom aún legible.
    /// </summary>
    public void VistaInicial()
    {
        if (Layout == null || Arbol == null || ActualWidth <= 0) return;
        var guardada = VistaPendiente; VistaPendiente = null;
        if (guardada != null && RectDe(guardada.Persona) is { } ra)
        {
            double s = Math.Clamp(guardada.Escala, EscalaMin, EscalaMax);
            CentrarEnPunto(ra.X + ra.Width / 2 + guardada.Dx, ra.Y + ra.Height / 2 + guardada.Dy, false, s);
            return;
        }
        double pad = 40;
        double fit = Math.Min((ActualWidth - 2 * pad) / Layout.Ancho, (ActualHeight - 2 * pad) / Layout.Alto);
        if (fit >= 0.7) { Ajustar(false); return; }

        var raiz = RectDe(Arbol.RaizId);
        if (raiz == null) { Ajustar(false); return; }
        var caja = raiz.Value;
        foreach (var id in FamiliaCercana(Arbol.RaizId))
            if (RectDe(id) is { } r) caja.Union(r);
        double escala = Math.Clamp(Math.Min((ActualWidth - 2 * pad) / caja.Width, (ActualHeight - 2 * pad) / caja.Height), 0.45, 1.0);
        // Si la familia cercana no cabe, la persona principal queda centrada en horizontal y abajo, con sus antepasados encima.
        double vw = ActualWidth / escala, vh = ActualHeight / escala;
        double cx = caja.Width <= vw - 2 * pad / escala ? caja.X + caja.Width / 2 : raiz.Value.X + raiz.Value.Width / 2;
        double cy = caja.Height <= vh - 2 * pad / escala ? caja.Y + caja.Height / 2 : raiz.Value.Bottom + 2.5 * pad / escala - vh / 2;
        CentrarEnPunto(cx, cy, false, escala);
    }

    /// <summary>Padres, abuelos, hermanos, parejas e hijos de una persona.</summary>
    private IEnumerable<string> FamiliaCercana(string id)
    {
        var a = Arbol!;
        var padres = a.UnionComoHijo(id)?.Parejas ?? new List<string>();
        foreach (var p in padres)
        {
            yield return p;
            foreach (var ab in a.UnionComoHijo(p)?.Parejas ?? new List<string>()) yield return ab;
        }
        foreach (var h in a.UnionComoHijo(id)?.Hijos ?? new List<string>()) yield return h;
        foreach (var u in a.UnionesComoPareja(id))
        {
            foreach (var q in u.Parejas) yield return q;
            foreach (var h in u.Hijos) yield return h;
        }
    }

    /// <summary>
    /// La vista actual, para recuperarla al volver a abrir el árbol: la escala y el centro de la pantalla respecto a la
    /// tarjeta más cercana a él (así sigue valiendo aunque el árbol haya cambiado algo).
    /// </summary>
    public VistaGuardada? VistaActual()
    {
        if (PersonaMasCentrada() is not { } m) return null;
        return new VistaGuardada { Persona = m.Id, Dx = m.Dx, Dy = m.Dy, Escala = m.Escala, Seleccion = _seleccion };
    }

    /// <summary>
    /// La persona cuya tarjeta está más cerca del centro de la pantalla (según el destino de la cámara aunque haya una
    /// animación en curso), con la distancia del centro de la pantalla al de su tarjeta y la escala.
    /// </summary>
    private (string Id, double Dx, double Dy, double Escala)? PersonaMasCentrada()
    {
        if (Layout == null || ActualWidth <= 0 || Layout.Cartas.Count == 0) return null;
        double e = (double)GetAnimationBaseValue(EscalaProperty);
        double tx = (double)GetAnimationBaseValue(TxProperty), ty = (double)GetAnimationBaseValue(TyProperty);
        double cx = (ActualWidth / 2 - tx) / e, cy = (ActualHeight / 2 - ty) / e;
        var o = Layout.Opciones;
        var (id, c) = Layout.Cartas.MinBy(kv => Math.Pow(kv.Value.X - cx, 2) + Math.Pow(kv.Value.Y + o.AltoCarta / 2 - cy, 2));
        return (id, cx - c.X, cy - (c.Y + o.AltoCarta / 2), e);
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
