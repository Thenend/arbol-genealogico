using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

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
                t = new TarjetaPersona(id, o.AnchoCarta, o.AltoCarta);
                t.MasClic += c => MenuSolicitado?.Invoke(c.PersonaId, c);
                t.EnlaceClic += c => AbrirEnlaceSolicitado?.Invoke(c.PersonaId);
                _tarjetas[id] = t;
                _mundo.Children.Add(t);
            }
            t!.Actualizar(p, directa.Contains(id), pos.Politica);
            t.Seleccionada = id == _seleccion;

            double izq = pos.X - o.AnchoCarta / 2, arr = pos.Y;
            Canvas.SetLeft(t, izq); Canvas.SetTop(t, arr);
            if (animarTarjetas && !nueva && _destinos.TryGetValue(id, out var antes) && (Math.Abs(antes.X - izq) > 0.5 || Math.Abs(antes.Y - arr) > 0.5))
            {
                Animar(t, Canvas.LeftProperty, antes.X, izq);
                Animar(t, Canvas.TopProperty, antes.Y, arr);
            }
            else if (animarTarjetas && nueva)
            {
                t.BeginAnimation(OpacityProperty, new DoubleAnimation(0, pos.Politica ? 0.93 : 1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(120) });
            }
            _destinos[id] = new Point(izq, arr);
        }

        _mundo.Width = Layout.Ancho; _mundo.Height = Layout.Alto;
        _aristas.Width = Layout.Ancho; _aristas.Height = Layout.Alto;
        _aristas.Layout = Layout;
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
            SeleccionCambiada?.Invoke(value);
        }
    }

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
        if (!animar)
        {
            BeginAnimation(EscalaProperty, null); BeginAnimation(TxProperty, null); BeginAnimation(TyProperty, null);
            Escala = escala; Tx = tx; Ty = ty;
            return;
        }
        var d = TimeSpan.FromMilliseconds(380);
        var f = new CubicEase { EasingMode = EasingMode.EaseInOut };
        BeginAnimation(EscalaProperty, new DoubleAnimation(escala, d) { EasingFunction = f });
        BeginAnimation(TxProperty, new DoubleAnimation(tx, d) { EasingFunction = f });
        BeginAnimation(TyProperty, new DoubleAnimation(ty, d) { EasingFunction = f });
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
        BeginAnimation(EscalaProperty, null); BeginAnimation(TxProperty, null); BeginAnimation(TyProperty, null);
        ZoomEn(e.GetPosition(this), Math.Pow(1.0014, e.Delta), false);
        e.Handled = true;
    }

    private void IniciarPan(MouseButtonEventArgs e, bool medio)
    {
        BeginAnimation(EscalaProperty, null); BeginAnimation(TxProperty, null); BeginAnimation(TyProperty, null);
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
