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
using static ArbolGenealogico.Core.Idiomas.Idioma;

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
    private Ordenacion _ordenacion = Ordenacion.A;
    private string? _sel;
    private HashSet<string> _coincidencias = new();
    private HashSet<string> _directa = new();
    private Resaltado _res = Resaltado.Ninguno;
    private readonly Dictionary<string, Tarjeta> _tarjetas = new();
    private readonly Dictionary<Conexion, Geometry> _caminos = new();
    /// <summary>Las mismas, a mano (tema Papel y tinta), hechas al dibujarlas la primera vez.</summary>
    private readonly Dictionary<Conexion, Geometry> _caminosMano = new();
    private double _e = 1, _tx, _ty;
    private bool _encuadrarPendiente;

    public event Action<string?>? SeleccionCambiada;
    public event Action<string>? FichaPedida;
    public event Action<string>? MenuPedido;
    public event Action<string>? EnlacePedido;
    /// <summary>Con <see cref="ModoRaton"/>: el botón derecho sobre una tarjeta (con su id) o sobre el fondo (null), y dónde.</summary>
    public event Action<string?, Point>? MenuContextualPedido;
    /// <summary>Cada vez que se dibuja (la vista se ha movido, ampliado o cambiado): para el minimapa.</summary>
    public event Action? Dibujado;

    /// <summary>
    /// En el ordenador, con ratón: un clic selecciona (otro clic no abre la ficha: hace falta el doble clic), el botón
    /// derecho pide el menú de la tarjeta y mantener pulsado no hace nada (se está arrastrando).
    /// </summary>
    public bool ModoRaton { get; set; }

    /// <summary>
    /// Para la muestra de una tarjeta (al elegir su forma): sin el realce de la línea directa y sin moverse con el dedo; se
    /// encuadra sola al cambiar de tamaño o de forma.
    /// </summary>
    public bool Muestra { get; init; }

    /// <summary>La tarjeta sobre la que se va a soltar algo (una foto que se arrastra), con un anillo como el de buscar.</summary>
    public string? Destino
    {
        get => _destino;
        set { if (_destino != value) { _destino = value; InvalidateVisual(); } }
    }
    private string? _destino;

    /// <summary>La tarjeta que hay en ese punto del control, o null.</summary>
    public string? TarjetaEnPunto(Point p) => TarjetaEn(p);

    /// <summary>Lo que se ve ahora, en coordenadas del árbol.</summary>
    public Rect Visible => _e > 0 ? new Rect(-_tx / _e, -_ty / _e, Bounds.Width / _e, Bounds.Height / _e) : default;

    /// <summary>Los colores: los de la pantalla (los del tema) o, en papel, los del estilo elegido.</summary>
    private Paleta _p;
    private readonly EstiloPapel _papel;
    /// <summary>En papel, el estilo y la letra con los que se dibuja (null: los de la pantalla).</summary>
    private readonly Tema? _tema;
    private readonly Letra? _letra;
    /// <summary>Para imprimir: sin los botones que solo sirven en la pantalla (el del árbol enlazado).</summary>
    private readonly bool _enPapel;

    public LienzoArbol() : this(null) { }

    /// <param name="papel">Para dibujar el árbol en papel, con los colores de ese estilo (null: en la pantalla).</param>
    /// <param name="tema">En papel, el estilo del árbol (null: el de la pantalla).</param>
    /// <param name="letra">En papel, la letra de las tarjetas (null: la de ese estilo).</param>
    public LienzoArbol(EstiloPapel? papel, Tema? tema = null, Letra? letra = null)
    {
        ClipToBounds = true;
        Focusable = true;
        _papel = papel ?? EstiloPapel.Oscuro;
        _tema = tema; _letra = letra;
        _p = PaletaEnUso;
        _enPapel = papel != null;
    }

    private Paleta PaletaEnUso => Paleta.De(_papel, _tema, _letra);

    /// <summary>Al cambiar de tema: los colores nuevos (las tarjetas se vuelven a preparar con ellos).</summary>
    public void CambiarColores()
    {
        _p = PaletaEnUso;
        _tarjetas.Clear();
        _caminosMano.Clear();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Tema.Cambiado += CambiarColores;
        // por si cambió mientras no estaba en la pantalla
        if (_p != PaletaEnUso) CambiarColores();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Tema.Cambiado -= CambiarColores;
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

    /// <summary>Si se marca a las personas que tienen algo escrito en su historia.</summary>
    public bool MarcaHistoria
    {
        get => _marcaHistoria;
        set { if (_marcaHistoria != value) { _marcaHistoria = value; InvalidateVisual(); } }
    }
    private bool _marcaHistoria = true;

    /// <summary>
    /// Si se destaca a la persona principal: su línea directa en dorado y la familia política con el borde discontinuo. Sin
    /// destacar (al imprimir un árbol de toda la familia), todas las tarjetas y las líneas son iguales.
    /// </summary>
    public bool Destacar
    {
        get => _destacar;
        set { if (_destacar != value) { _destacar = value; Calcular(); InvalidateVisual(); } }
    }
    private bool _destacar = true;

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
        // viendo el árbol entero y sin nadie seleccionado, después también se ve entero (con la colocación nueva puede
        // tener otra forma); si no, la persona seleccionada (o la del centro) se queda donde estaba
        bool entero = _sel == null && ArbolEnteroVisible();
        var id = _sel ?? PersonaMasCentrada();
        var antes = id != null ? Centro(id) : null;
        cambio();
        Calcular();
        if (entero || Muestra) { Encuadrar(false); return; }
        if (id != null && antes is { } a && Centro(id) is { } d)
        {
            _tx += (a.X - d.X) * _e; _ty += (a.Y - d.Y) * _e;
        }
        LimitarAhora();
        InvalidateVisual();
    }

    /// <summary>El árbol cabe (casi) entero en la pantalla.</summary>
    private bool ArbolEnteroVisible()
    {
        if (_layout == null || Bounds.Width <= 0 || _layout.Ancho <= 0 || _layout.Alto <= 0) return false;
        var visible = new Rect(-_tx / _e, -_ty / _e, Bounds.Width / _e, Bounds.Height / _e);
        var comun = visible.Intersect(new Rect(0, 0, _layout.Ancho, _layout.Alto));
        return comun.Width >= 0.9 * _layout.Ancho && comun.Height >= 0.9 * _layout.Alto;
    }

    private void Calcular()
    {
        if (_arbol == null) return;
        _formaEnUso = _forma.Para(_arbol);
        _layout = LayoutEngine.Calcular(_arbol, _formaEnUso.Opciones(_ordenacion));
        _directa = Muestra || !_destacar ? new HashSet<string>() : _arbol.LineaDirecta();
        _tarjetas.Clear();
        _caminos.Clear();
        _caminosMano.Clear();
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
    internal Point? Centro(string id) => RectDe(id) is { } r ? new Point(r.Center.X * _e + _tx, r.Center.Y * _e + _ty) : null;

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
        if (Muestra) Encuadrar(false);
        else if (_encuadrarPendiente && e.NewSize.Width > 0) { _encuadrarPendiente = false; VistaInicial(); }
        else if (e.PreviousSize.Width > 0)
        {
            // al girar el teléfono o abrirse el teclado, el centro de la vista sigue en el centro
            _tx += (e.NewSize.Width - e.PreviousSize.Width) / 2;
            _ty += (e.NewSize.Height - e.PreviousSize.Height) / 2;
            LimitarAhora();
        }
    }

    /// <summary>Todo el árbol si cabe con una letra legible; si no, la persona principal con sus padres, abuelos e hijos.</summary>
    public void VistaInicial()
    {
        if (_layout == null || _arbol == null || Bounds.Width <= 0) return;
        double pad = 24;
        double legible = EscalaLectura;
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

    /// <summary>Lo que tapan los controles de encima por cada lado (en el ordenador, el minimapa): «ver todo» no lo usa.</summary>
    public Thickness Tapado { get; set; }

    public void Encuadrar(bool animar = true)
    {
        if (_layout == null || Bounds.Width <= 0) return;
        if (Muestra) { EncuadrarMuestra(); return; }
        double pad = 24, ancho = Bounds.Width - Tapado.Left - Tapado.Right, alto = Bounds.Height - Tapado.Top - Tapado.Bottom;
        double s = Math.Min((ancho - 2 * pad) / _layout.Ancho, (alto - 2 * pad) / _layout.Alto);
        s = Math.Clamp(Math.Min(s, 1.0), EscalaMin, EscalaMax);
        IrA(s, Tapado.Left + (ancho - _layout.Ancho * s) / 2, Tapado.Top + (alto - _layout.Alto * s) / 2, animar);
    }

    /// <summary>De qué tamaño se ve el nombre en la muestra (en píxeles de la pantalla, con la letra de siempre).</summary>
    private const double TamNombreMuestra = 26;

    /// <summary>
    /// La muestra: su tarjeta en el centro, sin contar los márgenes del árbol, ampliada o reducida para que el nombre se vea
    /// siempre del mismo tamaño (o lo que quepa). Así, al quitar y poner cosas, cambia la tarjeta y no parece que todo se
    /// encoja: en las tarjetas sin foto, por ejemplo, la letra es más pequeña para que quepan los apellidos y los años. Si
    /// tiene pareja, se ve también lo que hay entre las dos (la boda) y el principio de la suya (el resto queda cortado).
    /// </summary>
    private void EncuadrarMuestra()
    {
        if (_layout == null || _layout.Cartas.Count == 0) return;
        var id0 = _layout.Cartas.ContainsKey(_arbol!.RaizId) ? _arbol.RaizId : _layout.Cartas.Keys.First();
        var tarjeta = RectDe(id0)!.Value.Inflate(RadioHistoria);
        // la escala, por su tarjeta (para que no cambie por la pareja)
        double nombre = Preparar(id0, RectDe(id0)!.Value).TamNombre;
        double s = nombre > 0 ? TamNombreMuestra * _p.EscalaLetra / nombre : 1;
        s = Math.Max(0.1, Math.Min(s, Math.Min((Bounds.Width - 24) / tarjeta.Width, (Bounds.Height - 24) / tarjeta.Height)));
        var caja = tarjeta;
        foreach (var otro in _layout.Cartas.Keys.Where(id => id != id0))
        {
            var r = RectDe(otro)!.Value;
            caja = caja.Union(new Rect(r.Left < tarjeta.Left ? r.Right - 20 : r.Left, r.Top, 20, r.Height));
        }
        _e = s;
        _ty = Bounds.Height / 2 - tarjeta.Center.Y * _e;
        // todo centrado si cabe; si no, su tarjeta pegada al borde y lo de la pareja hasta donde llegue
        if (caja.Width * _e <= Bounds.Width - 24) _tx = Bounds.Width / 2 - caja.Center.X * _e;
        else _tx = caja.Left < tarjeta.Left ? Bounds.Width - 12 - tarjeta.Right * _e : 12 - tarjeta.Left * _e;
        InvalidateVisual();
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
        escala = Math.Clamp(escala, EscalaMinima, EscalaMax);
        (tx, ty) = Limitar(escala, tx, ty);
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

    // ---------- Límites: el árbol no se puede perder de vista ----------

    /// <summary>
    /// Lo más que se puede alejar: el árbol entero un poco más pequeño que la pantalla (y nunca más lejos de lo que hace
    /// falta para un árbol grande).
    /// </summary>
    private double EscalaMinima
    {
        get
        {
            if (_layout == null || Bounds.Width <= 0 || _layout.Ancho <= 0) return EscalaMin;
            double cabe = Math.Min((Bounds.Width - 48) / _layout.Ancho, (Bounds.Height - 48) / _layout.Alto);
            return Math.Clamp(Math.Min(0.3, 0.8 * cabe), EscalaMin, EscalaMax);
        }
    }

    /// <summary>
    /// El desplazamiento más cercano al pedido con el que el árbol ocupa al menos media pantalla en cada sentido (o
    /// se ve entero, si es más pequeño): no se puede uno ir lejos de las tarjetas.
    /// </summary>
    private (double Tx, double Ty) Limitar(double e, double tx, double ty)
    {
        if (_layout == null || Bounds.Width <= 0) return (tx, ty);
        double w = Bounds.Width, h = Bounds.Height, aw = _layout.Ancho * e, ah = _layout.Alto * e;
        double mx = Math.Min(aw, w / 2), my = Math.Min(ah, h / 2);
        return (Math.Clamp(tx, mx - aw, w - mx), Math.Clamp(ty, my - ah, h - my));
    }

    private void LimitarAhora() => (_tx, _ty) = Limitar(_e, _tx, _ty);

    private void ZoomEn(Point p, double factor)
    {
        double ns = Math.Clamp(_e * factor, Math.Min(EscalaMinima, _e), EscalaMax);
        double k = ns / _e;
        _tx = p.X - (p.X - _tx) * k; _ty = p.Y - (p.Y - _ty) * k; _e = ns;
        LimitarAhora();
        InvalidateVisual();
    }

    /// <summary>La escala de la vista (1: el tamaño de la tarjeta en el árbol).</summary>
    public double Escala => _e;

    public void Zoom(double factor) { Detener(); ZoomEn(new Point(Bounds.Width / 2, Bounds.Height / 2), factor); }

    /// <summary>Desplaza la vista como al arrastrar con el dedo (para las pruebas).</summary>
    public void Mover(double dx, double dy) { Detener(); _tx += dx; _ty += dy; LimitarAhora(); InvalidateVisual(); }

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
        if (Muestra) return;
        Detener(); _inercia?.Stop();
        var p = e.GetPosition(this);
        _dedos[e.Pointer.Id] = p;
        e.Pointer.Capture(this);
        if (ModoRaton && e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _dedos.Remove(e.Pointer.Id);
            e.Pointer.Capture(null);
            var id = TarjetaEn(p);
            if (id != null) Seleccion = id;
            MenuContextualPedido?.Invoke(id, p);
            e.Handled = true;
            return;
        }
        if (_dedos.Count == 1)
        {
            _inicio = p; _inicioT = DateTime.UtcNow; _movido = false; _mantenido = false;
            _rastro.Clear(); _rastro.Add((DateTime.UtcNow, p));
            _mantener?.Stop();
            _mantener = ModoRaton ? null : new DispatcherTimer(TimeSpan.FromMilliseconds(520), DispatcherPriority.Input, (_, _) =>
            {
                _mantener?.Stop();
                if (_movido || _dedos.Count != 1) return;
                if (TarjetaEn(_inicio) is { } id) { _mantenido = true; Seleccion = id; MenuPedido?.Invoke(id); }
            });
            _mantener?.Start();
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
            double ns = Math.Clamp(_pinchE * Dist(ps[0], ps[1]) / _pinchDist, Math.Min(EscalaMinima, _pinchE), EscalaMax);
            var c = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
            // el punto del árbol que estaba bajo el centro de los dos dedos sigue bajo él
            double wx = (_pinchCentro.X - _pinchTx) / _pinchE, wy = (_pinchCentro.Y - _pinchTy) / _pinchE;
            _e = ns; _tx = c.X - wx * ns; _ty = c.Y - wy * ns;
            LimitarAhora();
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
        LimitarAhora();
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
        if (Muestra) return;
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
            double nx = _tx + vx * s, ny = _ty + vy * s;
            (_tx, _ty) = Limitar(_e, nx, ny);
            // al llegar al límite, se para en ese sentido
            if (Math.Abs(_tx - nx) > 0.01) vx = 0;
            if (Math.Abs(_ty - ny) > 0.01) vy = 0;
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
        if (id != null && ((id == _sel && !ModoRaton) || doble)) { Seleccion = id; FichaPedida?.Invoke(id); return; }
        Seleccion = id;
        // dos toques seguidos en el fondo: se alterna entre ver las tarjetas grandes y ver mucho árbol
        if (doble && id == null) AlternarZoom(p);
    }

    /// <summary>Por debajo de esta escala, los textos de las tarjetas se dibujan como barras (no se leerían).</summary>
    private const double EscalaSinNombres = 0.2;

    /// <summary>
    /// La escala para leer las tarjetas cómodamente: la de la vista al abrir un árbol grande, y la del zoom de lectura del
    /// doble toque (mayor si en el sistema se ha elegido letra grande).
    /// </summary>
    private static double EscalaLectura => Math.Min(1, 0.42 * Vistas.Ui.Escala);

    /// <summary>El tamaño del nombre en las tarjetas antes de encogerlo si no cabe (con apellidos debajo o sin ellos).</summary>
    private double TamNombreBase(bool apellidosAparte) =>
        (_formaEnUso.Vertical ? (apellidosAparte ? 24 : 26) : (apellidosAparte ? 28 : 31)) * _p.EscalaLetra;

    /// <summary>
    /// Mucho árbol a la vista para moverse por él: lo más lejos posible con los nombres aún dibujados; si a esa escala el
    /// árbol entero cabe y sobra sitio, el árbol entero.
    /// </summary>
    private double EscalaNavegacion
    {
        get
        {
            double minima = EscalaSinNombres * 1.12;
            if (_layout == null || Bounds.Width <= 0) return minima;
            double cabe = Math.Min((Bounds.Width - 48) / _layout.Ancho, (Bounds.Height - 48) / _layout.Alto);
            return Math.Clamp(cabe, minima, EscalaLectura * 0.7);
        }
    }

    /// <summary>
    /// Doble toque en el fondo: va al zoom (de lectura o de navegación) más lejano del de ahora, con el punto tocado en el
    /// centro de la pantalla. Así, viendo mucho árbol, amplía la zona tocada; y viendo las tarjetas grandes, se aleja.
    /// </summary>
    public void AlternarZoom(Point pantalla)
    {
        double lectura = EscalaLectura, navegacion = EscalaNavegacion;
        double aLectura = Math.Abs(Math.Log(_e) - Math.Log(lectura)), aNavegacion = Math.Abs(Math.Log(_e) - Math.Log(navegacion));
        double destino = aLectura >= aNavegacion ? lectura : navegacion;
        var m = AMundo(pantalla);
        IrA(destino, Bounds.Width / 2 - m.X * destino, Bounds.Height / 2 - m.Y * destino, true);
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

    private static (Tema Tema, IBrush Pincel)? _fondo;
    /// <summary>El fondo del árbol en la pantalla, del tema de ahora: más claro en el centro.</summary>
    private static IBrush Fondo
    {
        get
        {
            var t = Tema.Actual;
            if (_fondo is { } f && f.Tema == t) return f.Pincel;
            IBrush pincel = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(t.Lienzo1, 0), new GradientStop(t.Lienzo2, 1) },
                RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            };
            _fondo = (t, pincel);
            return pincel;
        }
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Fondo, new Rect(Bounds.Size));
        if (Tema.Actual.AMano > 0 || Tema.Actual.Grano) ctx.FillRectangle(AMano.Grano, new Rect(Bounds.Size));
        if (_layout == null || _arbol == null) return;
        var visible = new Rect(-_tx / _e, -_ty / _e, Bounds.Width / _e, Bounds.Height / _e);
        using (ctx.PushTransform(Matrix.CreateScale(_e, _e) * Matrix.CreateTranslation(_tx, _ty)))
            DibujarMundo(ctx, visible, lejos: _e < EscalaSinNombres);
        Dibujado?.Invoke();
    }

    /// <summary>
    /// Dibuja el árbol en coordenadas del mundo. <paramref name="lejos"/>: tan lejos que los textos no se leerían; las tarjetas
    /// se ven igual, pero cada línea de texto es una barra de su color (dibujar cientos de textos diminutos hace que el
    /// zoom dé tirones) y no se dibuja el «+» de la seleccionada (no se podría tocar).
    /// </summary>
    private void DibujarMundo(DrawingContext ctx, Rect visible, bool lejos, bool conBotones = true)
    {
        var lay = _layout!;
        DibujarLineas(ctx, visible);
        if (!lejos && !_formaEnUso.SinBodas) DibujarBodas(ctx, visible);
        var o = lay.Opciones;
        foreach (var (id, c) in lay.Cartas)
        {
            var r = new Rect(c.X - o.AnchoCarta / 2, c.Y, o.AnchoCarta, o.AltoCarta);
            if (!r.Inflate(12).Intersects(visible)) continue;
            DibujarTarjeta(ctx, id, r, c.Politica && _destacar, lejos);
        }
        if (conBotones && !lejos && _sel != null && RectDe(_sel) is { } rs)
        {
            var c = BotonMas(rs);
            var t = Tema.Actual;
            ctx.DrawEllipse(new SolidColorBrush(t.Boton), new Pen(new SolidColorBrush(t.Claro ? Colors.White : t.Fondo), 2), c, 15, 15);
            var lapiz = new Pen(new SolidColorBrush(t.BotonTexto), 2.6) { LineCap = PenLineCap.Round };
            ctx.DrawLine(lapiz, new Point(c.X - 6.5, c.Y), new Point(c.X + 6.5, c.Y));
            ctx.DrawLine(lapiz, new Point(c.X, c.Y - 6.5), new Point(c.X, c.Y + 6.5));
        }
    }

    private void DibujarLineas(DrawingContext ctx, Rect visible)
    {
        var lay = _layout!;
        bool atenuar = _res.Linaje.Count > 0;
        bool Directa(Conexion c) => c.Directa && _destacar;
        int Tono(Conexion c) => atenuar && !_res.Cercanas.Contains(c) ? 1 : 0;
        bool Ve(Conexion c) => c.Puntos.Count > 0 && Caja(c).Intersects(visible);
        var resto = lay.Conexiones.Where(c => !_res.Linaje.Contains(c) && Ve(c)).OrderByDescending(Tono).ToList();
        foreach (var c in resto.Where(c => !Directa(c)))
            ctx.DrawGeometry(null, (c.Tipo == TipoConexion.Pareja ? _p.Pareja : _p.Normal)[Tono(c)], CaminoDe(c));
        if (_p.Resplandor is { } resplandor)
            foreach (var c in resto.Where(Directa)) ctx.DrawGeometry(null, resplandor[Tono(c)], CaminoDe(c));
        foreach (var c in resto.Where(Directa)) ctx.DrawGeometry(null, _p.Oro[Tono(c)], CaminoDe(c));
        if (_p.Hojas is { } hojas)
        {
            var relleno = new SolidColorBrush(hojas);
            foreach (var c in resto.Where(c => c.Tipo != TipoConexion.Pareja))
                ctx.DrawGeometry(relleno, null, AMano.Hojas(c.Puntos, AMano.Semilla(c.Puntos)));
        }
        foreach (var c in _res.Linaje) ctx.DrawGeometry(null, _p.LuzResplandor, CaminoDe(c));
        foreach (var c in _res.Linaje) ctx.DrawGeometry(null, _p.Luz, CaminoDe(c));
        foreach (var c in lay.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2 && Ve(c)))
        {
            var m = c.Nudo is { } n ? new Point(n.X, n.Y) : new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
            Color borde = _res.Linaje.Contains(c) ? _p.ColorLinaje : Tono(c) == 1 ? Estilo.ConAlfa(_p.ColorPareja, 0x70) : Directa(c) ? _p.ColorOro : _p.ColorPareja;
            if (_p.Lazo is { } lazo)
                ctx.DrawGeometry(new SolidColorBrush(Tono(c) == 1 ? Estilo.ConAlfa(lazo, 0x70) : lazo), new Pen(new SolidColorBrush(borde), 1.3) { LineJoin = PenLineJoin.Round }, AMano.Lazo(m, 12));
            else if (_p.Nudo != "circulo") AMano.Nudo(ctx, _p.Nudo, m, _p.ColorNudo ?? borde, borde);
            else ctx.DrawEllipse(_p.FondoNudo, new Pen(new SolidColorBrush(borde), 2), m, 5, 5);
        }
    }

    /// <summary>El camino de la línea: recto o, con el tema Papel y tinta, a mano.</summary>
    private Geometry CaminoDe(Conexion c)
    {
        if (_p.AManoLineas <= 0 && _p.RadioLineas <= 0) return _caminos[c];
        if (!_caminosMano.TryGetValue(c, out var g))
            _caminosMano[c] = g = AMano.Linea(c.Puntos, _p.RadioLineas > 0 ? _p.RadioLineas : 12, _p.AManoLineas, AMano.Semilla(c.Puntos));
        return g;
    }

    /// <summary>
    /// El año de la boda de cada pareja, encima de la línea que los une (debajo cuelgan los hijos), sobre su circulito si
    /// cabe y si no lo más cerca posible, con dos anillos encima: unidos, si siguen juntos; separados, si se separaron o se
    /// divorciaron.
    /// </summary>
    private void DibujarBodas(DrawingContext ctx, Rect visible)
    {
        var uniones = new Dictionary<string, Union>();
        foreach (var u in _arbol!.Uniones) if (u.Boda != null) uniones.TryAdd(u.Id, u);
        if (uniones.Count == 0) return;
        foreach (var c in _layout!.Conexiones)
        {
            if (c.Tipo != TipoConexion.Pareja || c.Puntos.Count != 2 || !uniones.TryGetValue(c.UnionId, out var u)) continue;
            var m = c.Nudo is { } n ? new Point(n.X, n.Y) : new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
            if (!new Rect(m.X - 90, m.Y - 40, 120, 56).Intersects(visible)) continue;
            var f = u.Boda!.Value;
            var texto = (f.Aproximada ? "~" : "") + Core.Idiomas.Fechas.Anio(f.Anio);
            double tam = 15 * _p.EscalaLetra;
            var ft = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LetraApellidos, tam, _p.Fechas);
            if (Math.Abs(c.Puntos[0].X - c.Puntos[1].X) < 0.5)
            {
                // árbol tumbado (la pareja, una encima de la otra): el año a la izquierda del circulito (a la derecha salen
                // los hijos), del alto que quepa entre las dos tarjetas, y los anillos a su izquierda
                double alto = Math.Abs(c.Puntos[1].Y - c.Puntos[0].Y);
                if (ft.Height > alto - 2)
                {
                    tam *= Math.Max(0.6, (alto - 2) / ft.Height);
                    ft = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LetraApellidos, tam, _p.Fechas);
                }
                double xt = m.X - 10 - ft.Width;
                ctx.DrawText(ft, new Point(xt, m.Y - ft.Height / 2));
                double rr = tam * 0.3, sepr = u.Separados ? rr * 1.35 : rr * 0.62, cx = xt - 3 - sepr - rr;
                var lapizr = new Pen(_p.Fechas, Math.Max(1, tam * 0.1));
                ctx.DrawEllipse(null, lapizr, new Point(cx - sepr, m.Y), rr, rr);
                ctx.DrawEllipse(null, lapizr, new Point(cx + sepr, m.Y), rr, rr);
                continue;
            }
            // lo que mide la línea (sin tarjeta, se alarga hasta los nombres; con tarjeta, las parejas se separan más si hay
            // bodas: ver FormaTarjeta.ConBodas); la letra, la que quepa encima
            double x0 = Math.Min(c.Puntos[0].X, c.Puntos[1].X), x1 = Math.Max(c.Puntos[0].X, c.Puntos[1].X), hueco = x1 - x0;
            if (ft.Width > hueco - 6)
            {
                tam *= Math.Max(0.6, (hueco - 6) / ft.Width);
                ft = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LetraApellidos, tam, _p.Fechas);
            }
            double y = m.Y - 6 - ft.Height;
            double x = Math.Clamp(m.X, x0 + ft.Width / 2 + 3, Math.Max(x0 + ft.Width / 2 + 3, x1 - ft.Width / 2 - 3));
            ctx.DrawText(ft, new Point(x - ft.Width / 2, y));
            // los anillos, encima del año
            double r = tam * 0.3, cy = y - r - 1.5, sep = u.Separados ? r * 1.35 : r * 0.62;
            var lapiz = new Pen(_p.Fechas, Math.Max(1, tam * 0.1));
            ctx.DrawEllipse(null, lapiz, new Point(x - sep, cy), r, r);
            ctx.DrawEllipse(null, lapiz, new Point(x + sep, cy), r, r);
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
        /// <summary>Su color en la paleta en uso (en el estilo claro, más oscuro; en blanco y negro, un gris).</summary>
        public required Color Acento;
        public required bool SinNombre;
        public FormattedText? Nombre, Apellidos, Fechas, Iniciales;
        /// <summary>De lejos, en lugar de cada línea de texto, una barra de su color (respecto a la esquina de la tarjeta).</summary>
        public List<(RoundedRect Barra, IBrush Color)> Barras = new();
        public Bitmap? Foto;
        public required IBrush FondoTarjeta;
        public bool Historia, Enlace;
        /// <summary>Lleva la línea de los años (y entonces los textos llegan más abajo).</summary>
        public bool ConFechas;
        /// <summary>El tamaño con el que ha quedado la letra del nombre (después de encogerla si no cabía).</summary>
        public double TamNombre;
    }

    // las letras, las del tema (en Álbum, de libro)
    private Typeface LetraNombre => _p.LetraNombre;
    private Typeface LetraNombreCursiva => _p.LetraNombreCursiva;
    private Typeface LetraApellidos => _p.LetraApellidos;

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

    /// <summary>
    /// Las zonas de una tarjeta ya preparada. Con la línea de los años, los textos llegan hasta abajo: se dejan 14 px libres
    /// para el «+» de la tarjeta seleccionada, que asoma por el borde de abajo (sin tarjeta va más abajo y no hace falta).
    /// </summary>
    private (Rect Texto, Rect? Foto) ZonasDe(Tarjeta t, Rect r)
    {
        var f = _formaEnUso;
        var (texto, foto) = Zonas(r, t.Enlace && f.Foto && !f.SinTarjeta);
        if (t.ConFechas && !f.SinTarjeta && texto.Bottom > r.Bottom - 14)
            texto = new Rect(texto.X, texto.Y, texto.Width, Math.Max(0, r.Bottom - 14 - texto.Y));
        return (texto, foto);
    }

    /// <summary>Dónde van el nombre, los apellidos y los años dentro de la zona del texto (lo usan el dibujo y las líneas).</summary>
    private (Point Nombre, Point? Apellidos, Point? Fechas) PosicionTextos(Tarjeta t, Rect zona)
    {
        var f = _formaEnUso;
        var nom = t.Nombre!;
        double alto = nom.Height + (t.Apellidos?.Height ?? 0) + (t.Fechas?.Height ?? 0);
        if (!f.Vertical)
        {
            double y = zona.Center.Y - alto / 2;
            bool centrado = f.SinTarjeta && !f.Foto;
            Point En(FormattedText ft, double yy) => new(centrado ? zona.Center.X - ft.Width / 2 : zona.X, yy);
            var pn = En(nom, y);
            y += nom.Height;
            Point? pa = t.Apellidos is { } ap ? En(ap, y) : null;
            y += t.Apellidos?.Height ?? 0;
            Point? pf = t.Fechas is { } fe ? En(fe, y) : null;
            return (pn, pa, pf);
        }
        double yv = f.Foto ? zona.Y : zona.Center.Y - alto / 2;
        // los textos verticales ocupan todo el ancho y se centran dentro
        Point Centrado(FormattedText ft, double yy) => new(zona.X + (zona.Width - ft.MaxTextWidth) / 2, yy);
        var pnv = Centrado(nom, yv);
        yv += nom.Height;
        Point? pav = t.Apellidos is { } ap2 ? Centrado(ap2, yv) : null;
        yv += t.Apellidos?.Height ?? 0;
        return (pnv, pav, t.Fechas is { } fe2 ? Centrado(fe2, yv) : null);
    }

    /// <summary>Lo que se ve de una tarjeta (la foto, el nombre y los apellidos), en coordenadas del árbol.</summary>
    private List<Rect> Visibles(string id, Rect r)
    {
        var t = Preparar(id, r);
        var f = _formaEnUso;
        var (zona, foto) = ZonasDe(t, r);
        var lista = new List<Rect>();
        if (foto is { } fr) lista.Add(fr);
        if (t.Nombre is { } nom)
        {
            var (pn, pa, pf) = PosicionTextos(t, zona);
            // los verticales están centrados: lo que se ve es el ancho de la línea más larga, en medio
            double Izq(FormattedText ft, Point p) => f.Vertical ? zona.Center.X - ft.Width / 2 : p.X;
            lista.Add(new Rect(Izq(nom, pn), pn.Y, nom.Width, nom.Height));
            if (t.Apellidos is { } ap && pa is { } p2) lista.Add(new Rect(Izq(ap, p2), p2.Y, ap.Width, ap.Height));
            if (t.Fechas is { } fe && pf is { } p3) lista.Add(new Rect(Izq(fe, p3), p3.Y, fe.Width, fe.Height));
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
            // a los lados, los textos necesitan más aire; la foto, el mismo que arriba y abajo
            double Lado(Rect r) => r == z.Foto ? huecoV : hueco;
            if (dx > 0) return new Pt(Math.Max(p.X, cruzan.Min(r => r.Left - Lado(r))), p.Y);     // por la izquierda
            return new Pt(Math.Min(p.X, cruzan.Max(r => r.Right + Lado(r))), p.Y);                 // por la derecha
        }
    }

    private Tarjeta Preparar(string id, Rect r)
    {
        if (_tarjetas.TryGetValue(id, out var t)) return t;
        var p = _arbol!.Obtener(id);
        bool sinNombre = Nombres.SinNombre(p);
        var e = _p.Estilo(p.Sexo, sinNombre);
        t = new Tarjeta
        {
            Estilo = e, Acento = _p.Acento(e, p.Sexo, sinNombre), SinNombre = sinNombre,
            FondoTarjeta = _formaEnUso.SinTarjeta ? Brushes.Transparent : _p.Fondo(e),
            // en blanco y negro, la foto en grises (como saldrá)
            Foto = !_formaEnUso.Foto ? null : _p.Tipo == EstiloPapel.BlancoYNegro ? Fotos.ImagenGris(p.Foto) : Fotos.Imagen(p.Foto),
            Historia = !string.IsNullOrWhiteSpace(p.Historia),
            Enlace = !string.IsNullOrEmpty(p.ArbolEnlazado),
        };
        // los años, debajo, más pequeños y más tenues: «1920 – 1998», «* 1950», «† 1890»
        string anios = _formaEnUso.SinFechas ? "" : Core.Idiomas.Fechas.Anios(p);
        t.ConFechas = anios.Length > 0;
        var (zona, _) = ZonasDe(t, r);
        string nombre = sinNombre ? T("Sin nombre") : string.IsNullOrWhiteSpace(p.Nombre) ? p.Apellidos.Trim() : p.Nombre.Trim();
        bool apellidosAparte = !sinNombre && !_formaEnUso.SinApellidos && !string.IsNullOrWhiteSpace(p.Nombre) && !string.IsNullOrWhiteSpace(p.Apellidos);
        IBrush colorNombre = _p.Nombre(e, sinNombre, _formaEnUso.SinTarjeta);
        // El nombre, grande: en el teléfono el árbol se ve más pequeño que en el ordenador. Si va solo (sin apellidos
        // debajo), aún más. Los que no caben se encogen lo justo.
        double tamNombre = TamNombreBase(apellidosAparte), kFinal = 1;
        double tamApellidos = (_formaEnUso.Vertical ? 17 : 19) * _p.EscalaApellidos;
        double tamFechas = (_formaEnUso.Vertical ? 15 : 17) * _p.EscalaApellidos;
        if (!_formaEnUso.Vertical)
        {
            // en horizontal, una línea para cada uno: si no cabe, se encoge lo justo
            t.Nombre = Encoger(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre, colorNombre, zona.Width, out t.TamNombre);
            if (apellidosAparte) t.Apellidos = Encoger(p.Apellidos.Trim(), LetraApellidos, tamApellidos, _p.Apellidos, zona.Width);
            if (anios.Length > 0) t.Fechas = Encoger(anios, LetraApellidos, tamFechas, _p.Fechas, zona.Width);
            // y si no caben todos a lo alto (tarjetas sin foto, más bajas), se encogen todos
            double alto = t.Nombre.Height + (t.Apellidos?.Height ?? 0) + (t.Fechas?.Height ?? 0);
            if (alto > zona.Height && zona.Height > 0)
            {
                double k = zona.Height / alto;
                kFinal = k;
                t.Nombre = Encoger(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre * k, colorNombre, zona.Width, out t.TamNombre);
                if (apellidosAparte) t.Apellidos = Encoger(p.Apellidos.Trim(), LetraApellidos, tamApellidos * k, _p.Apellidos, zona.Width);
                if (anios.Length > 0) t.Fechas = Encoger(anios, LetraApellidos, tamFechas * k, _p.Fechas, zona.Width);
            }
        }
        else
        {
            // en vertical, centrados y en dos líneas si hace falta; si aun así no caben, se encoge todo.
            // Las líneas solo se parten entre palabras: si una palabra sola no cabe a lo ancho («Alejandro»),
            // se encoge todo lo justo para que quepa entera (nunca «Alejandr / o»).
            var letraNombre = sinNombre ? LetraNombreCursiva : LetraNombre;
            double palabra = Math.Max(PalabraMasLarga(nombre, letraNombre, tamNombre),
                Math.Max(apellidosAparte ? PalabraMasLarga(p.Apellidos, LetraApellidos, tamApellidos) : 0,
                    PalabraMasLarga(anios, LetraApellidos, tamFechas)));
            double k = palabra > zona.Width && zona.Width > 0 ? zona.Width / palabra * 0.99 : 1;
            for (int i = 0; i < 12; i++)
            {
                kFinal = k;
                t.TamNombre = tamNombre * k;
                t.Nombre = Texto(nombre, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre * k, colorNombre, zona.Width);
                t.Apellidos = apellidosAparte ? Texto(p.Apellidos.Trim(), LetraApellidos, tamApellidos * k, _p.Apellidos, zona.Width) : null;
                t.Fechas = anios.Length > 0 ? Texto(anios, LetraApellidos, tamFechas * k, _p.Fechas, zona.Width) : null;
                double alto = t.Nombre.Height + (t.Apellidos?.Height ?? 0) + (t.Fechas?.Height ?? 0);
                double ancho = Math.Max(t.Nombre.Width, Math.Max(t.Apellidos?.Width ?? 0, t.Fechas?.Width ?? 0));
                if (alto <= zona.Height + 0.5 && ancho <= zona.Width + 0.5) break;
                k *= 0.92;
            }
        }
        Barras(t, r, zona, sinNombre ? LetraNombreCursiva : LetraNombre, tamNombre * kFinal, colorNombre, tamApellidos * kFinal, tamFechas * kFinal);
        if (_formaEnUso.Foto && t.Foto == null)
            t.Iniciales = new FormattedText(Nombres.Iniciales(p), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LetraNombre,
                _formaEnUso.Vertical ? 23 : 21, _p.Iniciales(e));
        _tarjetas[id] = t;
        return t;
    }

    /// <summary>
    /// Las barras que sustituyen a los textos de lejos: una por línea, del ancho del texto (la última de varias, más corta),
    /// de la altura de sus letras y de su color algo transparente, como se ve un texto que no se alcanza a leer.
    /// </summary>
    private void Barras(Tarjeta t, Rect r, Rect zona, Typeface letraNombre, double tamNombre, IBrush colorNombre, double tamApellidos, double tamFechas)
    {
        if (t.Nombre is not { } nom) return;
        var f = _formaEnUso;
        var (pn, pa, pf) = PosicionTextos(t, zona);
        // grosor y opacidad: el nombre, más marcado; los apellidos, más finos y tenues (como se ven sus letras)
        void Una(FormattedText ft, Point p, Typeface letra, double tam, IBrush color, double grosor, double opacidad)
        {
            // sin tarjeta y sin foto, en horizontal, el texto ya va centrado y p es su esquina
            bool centrado = f.Vertical;
            double izq = centrado ? zona.Center.X - ft.Width / 2 : p.X;
            // en horizontal siempre es una línea; en vertical puede partirse en varias
            double linea = f.Vertical
                ? new FormattedText("Ág", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, color).Height
                : ft.Height;
            int n = Math.Max(1, (int)Math.Round(ft.Height / linea));
            var c = (color as ISolidColorBrush)?.Color ?? Colors.White;
            var pincel = new SolidColorBrush(c, c.A / 255.0 * opacidad);
            double alto = linea * grosor;
            for (int i = 0; i < n; i++)
            {
                double ancho = n > 1 && i == n - 1 ? ft.Width * 0.65 : ft.Width;
                double x = centrado ? zona.Center.X - ancho / 2 : izq;
                double y = p.Y + i * linea + (linea - alto) / 2;
                t.Barras.Add((new RoundedRect(new Rect(x - r.X, y - r.Y, ancho, alto), alto / 2), pincel));
            }
        }
        Una(nom, pn, letraNombre, tamNombre, colorNombre, 0.34, 0.75);
        if (t.Apellidos is { } ap && pa is { } p2) Una(ap, p2, LetraApellidos, tamApellidos, _p.Apellidos, 0.26, 0.4);
        if (t.Fechas is { } fe && pf is { } p3) Una(fe, p3, LetraApellidos, tamFechas, _p.Fechas, 0.24, 0.3);
    }

    private static FormattedText Encoger(string s, Typeface letra, double tam, IBrush color, double ancho) =>
        Encoger(s, letra, tam, color, ancho, out _);

    private static FormattedText Encoger(string s, Typeface letra, double tam, IBrush color, double ancho, out double usado)
    {
        usado = tam;
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, color);
        if (ft.Width <= ancho || ancho <= 0) return ft;
        usado = Math.Max(6, tam * ancho / ft.Width * 0.995);
        return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, usado, color);
    }

    /// <summary>Lo que mide a lo ancho la palabra más larga del texto, a ese tamaño de letra.</summary>
    private static double PalabraMasLarga(string s, Typeface letra, double tam)
    {
        double max = 0;
        foreach (var w in s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            max = Math.Max(max, new FormattedText(w, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, Brushes.Black).WidthIncludingTrailingWhitespace);
        return max;
    }

    private static FormattedText Texto(string s, Typeface letra, double tam, IBrush color, double ancho) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tam, color)
        {
            MaxTextWidth = Math.Max(1, ancho), TextAlignment = TextAlignment.Center, MaxLineCount = 3,
        };

    private void DibujarTarjeta(DrawingContext ctx, string id, Rect r, bool politica, bool lejos)
    {
        var t = Preparar(id, r);
        var e = t.Estilo;
        var f = _formaEnUso;
        bool sel = id == _sel, coincide = _coincidencias.Contains(id) || id == _destino, directa = _directa.Contains(id);
        bool atenuada = _res.Familia != null && !_res.Familia.Contains(id);
        double opacidad = atenuada ? 0.28 : politica ? 0.93 : 1;
        using var _ = opacidad < 1 ? ctx.PushOpacity(opacidad) : (DrawingContext.PushedState?)null;
        var caja = new RoundedRect(r, e.Radio);

        // halo: seleccionada, coincide con la búsqueda o es de la línea directa
        IBrush? halo = sel ? new SolidColorBrush(_p.HaloSeleccion)
            : coincide ? new SolidColorBrush(Estilo.ConAlfa(_p.Busqueda, 0x38))
            : directa && !f.SinTarjeta ? _p.HaloDirecta : null;
        var marco = r.Inflate(Aire);
        if (halo != null) ctx.DrawRectangle(halo, null, new RoundedRect(marco.Inflate(7), e.Radio + 7));
        // a mano (tema Papel y tinta): sin sombra, y el relleno y el contorno con temblor, siempre el mismo para cada persona
        double amano = _p.AMano;
        int semilla = amano > 0 ? AMano.Semilla(id) : 0;
        if (!f.SinTarjeta && _p.Manchas)
        {
            // manchas de acuarela: capas transparentes del color de la persona, un poco movidas, con los bordes irregulares y
            // el pigmento acumulado en el borde
            int sm = AMano.Semilla(id);
            var claro = Paleta.Aclarar(e.Acento, 0.3);
            var basica = AMano.Caja(r, e.Radio, 2.2, sm, false);
            ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xFF, 0xFC, 0xF7)), null, basica);
            ctx.DrawGeometry(new SolidColorBrush(Estilo.ConAlfa(claro, 0x3A)), new Pen(new SolidColorBrush(Estilo.ConAlfa(e.Acento, 0x38)), 1.1), basica);
            ctx.DrawGeometry(new SolidColorBrush(Estilo.ConAlfa(claro, 0x22)), null, AMano.Caja(r.Deflate(3).Translate(new Vector(3, 2)), e.Radio, 2.8, sm + 11, false));
            ctx.DrawGeometry(new SolidColorBrush(Estilo.ConAlfa(claro, 0x1A)), null, AMano.Caja(r.Deflate(6).Translate(new Vector(-6, -3)), e.Radio, 3.2, sm + 23, false));
        }
        else if (!f.SinTarjeta && amano > 0)
        {
            // a plumilla: una sombra de rayitas en diagonal, abajo y a la derecha, como en los grabados
            if (_p.Plumilla)
            {
                var rayas = new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0x4A, 0x2E, 0x17)), 0.75) { LineCap = PenLineCap.Round };
                var rnd = new Random(semilla);
                for (double x = r.X + e.Radio * 0.6 + 4; x < r.Right + 2; x += 3.4)
                    ctx.DrawLine(rayas, new Point(x - 2 + rnd.NextDouble(), r.Bottom + 2), new Point(x + 3.5, r.Bottom + 7.5 - rnd.NextDouble()));
                for (double y = r.Y + e.Radio * 0.6 + 4; y < r.Bottom; y += 3.4)
                    ctx.DrawLine(rayas, new Point(r.Right + 2, y - 2 + rnd.NextDouble()), new Point(r.Right + 7.5 - rnd.NextDouble(), y + 3.5));
            }
            ctx.DrawGeometry(t.FondoTarjeta, null, AMano.Caja(r, e.Radio, amano * 0.5, semilla, false));
        }
        else if (!f.SinTarjeta)
        {
            if (_p.SinSombra) { }
            else if (_p.SinFranja)
                for (int k = 3; k >= 1; k--)
                    ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x07, 0x20, 0x30, 0x28)), null, new RoundedRect(r.Translate(new Vector(0, 2 + k)).Inflate(k * 1.6), e.Radio + k * 1.6));
            else ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(_p.AlfaSombra, 0, 0, 0)), null, new RoundedRect(r.Translate(new Vector(0, 5)), e.Radio));
            ctx.DrawRectangle(t.FondoTarjeta, null, caja);
        }
        if (!f.SinTarjeta && !_p.SinFranja)
        {
            // la franja del color de su sexo (más gruesa sin foto)
            var franjaColor = new SolidColorBrush(Estilo.ConAlfa(t.Acento, politica ? (byte)0x73 : (byte)0xF2));
            double g = f.Foto ? 4 : 7;
            var franja = f.Vertical
                ? new Rect(r.X + (f.Foto ? 30 : 22), r.Y + 9, r.Width - 2 * (f.Foto ? 30 : 22), g)
                : f.Foto ? new Rect(r.X + 10, r.Y + 20, g, r.Height - 40) : new Rect(r.X + 9, r.Y + 9, g, r.Height - 18);
            ctx.DrawRectangle(franjaColor, null, new RoundedRect(franja, g / 2));
        }

        var (zona, foto) = ZonasDe(t, r);
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
                ctx.DrawEllipse(_p.SinFoto(e), null, circulo);
                if (t.Iniciales is { } ini && !lejos)
                    ctx.DrawText(ini, new Point(circulo.Center.X - ini.Width / 2, circulo.Center.Y - ini.Height / 2));
            }
            ctx.DrawEllipse(null, new Pen(new SolidColorBrush(t.Acento), 2), fr.Center, fr.Width / 2 - 1, fr.Height / 2 - 1);
        }

        if (lejos)
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(r.X, r.Y)))
                foreach (var (barra, tinta) in t.Barras) ctx.DrawRectangle(tinta, null, barra);
        }
        else if (t.Nombre is { } nom)
        {
            var (pn, pa, pf) = PosicionTextos(t, zona);
            ctx.DrawText(nom, pn);
            if (t.Apellidos is { } ap && pa is { } p2) ctx.DrawText(ap, p2);
            if (t.Fechas is { } fe && pf is { } p3) ctx.DrawText(fe, p3);
        }

        // el botón de su árbol enlazado (con foto y con tarjeta: si no, taparía el nombre; en papel no, que no se puede tocar)
        if (t.Enlace && f.Foto && !f.SinTarjeta && !_enPapel)
        {
            var c = BotonEnlace(r);
            ctx.DrawEllipse(new SolidColorBrush(Estilo.ConAlfa(_p.ColorOro, 0x30)), null, c, 12, 12);
            var lp = new Pen(new SolidColorBrush(_p.ColorOro), 1.8) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            ctx.DrawLine(lp, new Point(c.X - 4, c.Y + 4), new Point(c.X + 4.5, c.Y - 4.5));
            ctx.DrawLine(lp, new Point(c.X - 0.5, c.Y - 4.5), new Point(c.X + 4.5, c.Y - 4.5));
            ctx.DrawLine(lp, new Point(c.X + 4.5, c.Y - 4.5), new Point(c.X + 4.5, c.Y + 0.5));
        }

        // contorno: blanco si está seleccionada, lima si coincide con la búsqueda, dorado en la línea directa; discontinuo
        // en la familia política
        Color color; double grosor;
        if (sel) { color = _p.Seleccion; grosor = 2.5; }
        else if (coincide) { color = _p.Busqueda; grosor = 3; }
        else if (directa) (color, grosor) = _p.BordeDirecta;
        else (color, grosor) = _p.Borde(e);
        bool sinContorno = (_p.Manchas || grosor < 0.1) && !sel && !coincide;
        if (!(f.SinTarjeta && !sel && !coincide) && !sinContorno)
        {
            var pen = new Pen(new SolidColorBrush(color), grosor);
            if (politica && !sel) pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
            if (amano > 0)
            {
                pen.LineCap = PenLineCap.Round; pen.LineJoin = PenLineJoin.Round;
                // a plumilla (sin la familia política, que va discontinua): un trazo que engorda y adelgaza
                if (_p.Plumilla && !politica)
                    ctx.DrawGeometry(new SolidColorBrush(color), null, AMano.CajaPlumilla(marco.Deflate(0.75), Math.Max(0, e.Radio - 0.75), amano, semilla + 1, grosor * 0.45, grosor * 2.4));
                else ctx.DrawGeometry(null, pen, AMano.Caja(marco.Deflate(0.75), Math.Max(0, e.Radio - 0.75), amano, semilla + 1, true));
            }
            else ctx.DrawRectangle(null, pen, new RoundedRect(marco.Deflate(0.75), Math.Max(0, e.Radio - 0.75)));
            // el segundo filete, fino, por dentro
            if (_p.Doble is { } doble && !f.SinTarjeta)
            {
                var pen2 = new Pen(new SolidColorBrush(doble), 0.9);
                if (politica) pen2.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
                ctx.DrawRectangle(null, pen2, new RoundedRect(marco.Deflate(grosor + 3), Math.Max(0, e.Radio - grosor - 3)));
            }
        }

        // lo último: la marca de que tiene historia (por encima del contorno de la seleccionada)
        if (t.Historia && _marcaHistoria) DibujarHistoria(ctx, PosicionMarca(id, r, foto), t.Acento);
    }

    private const double RadioHistoria = 10;

    /// <summary>Qué parte de una tarjeta hay en ese punto del control (para quitarla o ponerla tocándola en la muestra).</summary>
    public ParteTarjeta ParteEn(Point punto)
    {
        if (_layout == null || _arbol == null || _e <= 0) return ParteTarjeta.Ninguna;
        var w = new Point((punto.X - _tx) / _e, (punto.Y - _ty) / _e);
        double holgura = 8 / _e;
        // el año de la boda (y sus anillos), encima del circulito de la pareja
        if (!_formaEnUso.SinBodas)
            foreach (var c in _layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && c.Puntos.Count == 2))
            {
                if (_arbol.Uniones.FirstOrDefault(u => u.Id == c.UnionId)?.Boda == null) continue;
                var m = c.Nudo is { } n ? new Point(n.X, n.Y) : new Point((c.Puntos[0].X + c.Puntos[1].X) / 2, (c.Puntos[0].Y + c.Puntos[1].Y) / 2);
                var zona = Math.Abs(c.Puntos[0].X - c.Puntos[1].X) < 0.5 ? new Rect(m.X - 90, m.Y - 16, 86, 32) : new Rect(m.X - 30, m.Y - 46, 60, 50);
                if (zona.Inflate(holgura).Contains(w)) return ParteTarjeta.Boda;
            }
        foreach (var id in _layout.Cartas.Keys)
        {
            if (RectDe(id) is not { } r || !r.Inflate(holgura + RadioHistoria).Contains(w)) continue;
            var t = Preparar(id, r);
            var (zona, foto) = ZonasDe(t, r);
            if (t.Historia && _marcaHistoria && Dist(PosicionMarca(id, r, foto), w) <= RadioHistoria + holgura) return ParteTarjeta.Historia;
            if (foto is { } fr && fr.Inflate(holgura).Contains(w)) return ParteTarjeta.Foto;
            if (t.Nombre is { } nom)
            {
                var (pn, pa, pf) = PosicionTextos(t, zona);
                // cada línea, a todo el ancho de la zona del texto (es más fácil de acertar con el dedo)
                bool En(FormattedText? ft, Point? p) => ft != null && p is { } q && w.Y >= q.Y - 2 && w.Y <= q.Y + ft.Height + 2
                    && w.X >= zona.X - holgura && w.X <= zona.Right + holgura;
                if (En(t.Fechas, pf)) return ParteTarjeta.Fechas;
                if (En(t.Apellidos, pa)) return ParteTarjeta.Apellidos;
                if (En(nom, pn)) return ParteTarjeta.Nombre;
            }
            return r.Contains(w) ? ParteTarjeta.Tarjeta : ParteTarjeta.Ninguna;
        }
        return ParteTarjeta.Ninguna;
    }

    /// <summary>
    /// Dónde va la marca de «tiene historia»: colgada de la foto, abajo a la derecha, como las notificaciones; sin foto, en
    /// la esquina de arriba a la derecha de la tarjeta o, sin tarjeta, a la altura del nombre y a la derecha del texto más
    /// ancho (lejos de la línea que baja al centro del nombre).
    /// </summary>
    private Point PosicionMarca(string id, Rect r, Rect? foto)
    {
        if (foto is { } fr) return new Point(fr.Center.X + fr.Width / 2 * 0.72, fr.Center.Y + fr.Height / 2 * 0.72);
        if (!_formaEnUso.SinTarjeta) return new Point(r.Right - 4, r.Top + 4);
        var textos = Visibles(id, r);
        if (textos.Count == 0) return new Point(r.Right - 4, r.Top + 4);
        // sin pasar del borde de la tarjeta (al lado puede haber otra persona)
        return new Point(Math.Min(textos.Max(t => t.Right) + RadioHistoria * 0.8, r.Right), textos[0].Top + RadioHistoria * 0.4);
    }

    /// <summary>
    /// La marca de «tiene historia»: un circulito oscuro con el borde del color de la persona (como el de su foto) y tres
    /// renglones apagados del mismo color.
    /// </summary>
    private void DibujarHistoria(DrawingContext ctx, Point c, Color acento)
    {
        double r = RadioHistoria;
        ctx.DrawEllipse(new SolidColorBrush(_p.FondoMarca), new Pen(new SolidColorBrush(Estilo.ConAlfa(acento, 0xB0)), 1.5), c, r, r);
        var renglon = new Pen(new SolidColorBrush(Estilo.ConAlfa(Paleta.Aclarar(acento, 0.25), 0xC8)), 1.6) { LineCap = PenLineCap.Round };
        ctx.DrawLine(renglon, new Point(c.X - r * 0.42, c.Y - r * 0.32), new Point(c.X + r * 0.42, c.Y - r * 0.32));
        ctx.DrawLine(renglon, new Point(c.X - r * 0.42, c.Y), new Point(c.X + r * 0.42, c.Y));
        ctx.DrawLine(renglon, new Point(c.X - r * 0.42, c.Y + r * 0.32), new Point(c.X + r * 0.12, c.Y + r * 0.32));
    }

    // ---------- Imagen ----------

    /// <summary>
    /// El árbol entero como imagen (para compartirla), con el fondo de la pantalla y sin botones. <paramref name="marca"/>:
    /// con «Hecho con Ramo» en una franja debajo (la versión gratuita).
    /// </summary>
    public RenderTargetBitmap Imagen(double escala, bool marca = false)
    {
        var lay = _layout ?? throw new InvalidOperationException("No hay árbol.");
        double alto = lay.Alto + Pie(marca);
        // que no pase de unos 60 megapíxeles (los teléfonos no podrían con más)
        escala = Math.Min(escala, Math.Sqrt(60e6 / Math.Max(1, lay.Ancho * alto)));
        var tam = new PixelSize(Math.Max(1, (int)Math.Ceiling(lay.Ancho * escala)), Math.Max(1, (int)Math.Ceiling(alto * escala)));
        var rtb = new RenderTargetBitmap(tam, new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateScale(escala, escala)))
            DibujarEntero(ctx, marca);
        return rtb;
    }

    /// <summary>La letra de la marca «Hecho con Ramo»: pequeña, pero que crezca algo con el árbol (que se ve de lejos).</summary>
    private double TamMarca => Math.Clamp(Math.Max(_layout!.Ancho, _layout.Alto) / 110, 13, 44);

    /// <summary>La franja de abajo para la marca (en píxeles del árbol), o 0 sin ella.</summary>
    private double Pie(bool marca) => marca ? Math.Ceiling(TamMarca * 2.4) : 0;

    /// <summary>
    /// Guarda el árbol entero en un PDF de una sola página, con líneas y textos vectoriales: se puede ampliar todo lo que se
    /// quiera sin que se pixele, y las aplicaciones de mensajes lo mandan como documento, sin reducirlo como a las fotos.
    /// </summary>
    public async Task GuardarPdf(string ruta, string titulo, bool marca = false)
    {
        var lay = _layout ?? throw new InvalidOperationException("No hay árbol.");
        double alto = lay.Alto + Pie(marca);
        // los visores de PDF no suelen admitir páginas de más de 14 400 puntos (200 pulgadas) de lado
        double k = Math.Min(1, 14400 / Math.Max(lay.Ancho, alto));
        var pagina = new Rect(0, 0, Math.Ceiling(lay.Ancho * k), Math.Ceiling(alto * k));
        var hoja = new Hoja(this, k, marca) { Width = pagina.Width, Height = pagina.Height };
        hoja.Measure(pagina.Size);
        hoja.Arrange(pagina);
        await using var s = File.Create(ruta);
        using var doc = SkiaSharp.SKDocument.CreatePdf(s, new SkiaSharp.SKDocumentPdfMetadata
        {
            Title = titulo, Creator = "Ramo", Producer = "Ramo",
            Creation = DateTime.Now, Modified = DateTime.Now,
        });     // sin RasterDpi: con él, Skia encoge todo el contenido (72 / RasterDpi) y no la página
        var canvas = doc.BeginPage((float)pagina.Width, (float)pagina.Height);
        // Avalonia pone su propia transformación en el lienzo de Skia: la escala va dentro de la hoja
        await Avalonia.Skia.Helpers.DrawingContextHelper.RenderAsync(canvas, hoja, pagina, new Vector(96, 96));
        doc.EndPage();
        doc.Close();
    }

    /// <summary>
    /// Lo que se ve del árbol en esa zona (en coordenadas del árbol), sin fondo, selección ni botones: para las hojas que se
    /// imprimen y su vista previa. <paramref name="lejos"/>: los textos, como barras (ver <see cref="DibujarMundo"/>).
    /// </summary>
    public void DibujarEnPapel(DrawingContext ctx, Rect zona, bool lejos = false)
    {
        if (_layout == null) return;
        var sel = _sel; var res = _res;
        _sel = null; _res = Resaltado.Ninguno;
        try { DibujarMundo(ctx, zona, lejos, conBotones: false); }
        finally { _sel = sel; _res = res; }
    }

    /// <summary>El tamaño de la letra de los nombres en las tarjetas (en píxeles del árbol), para saber cómo saldrá impresa.</summary>
    public double TamLetraNombre => TamNombreBase(!_formaEnUso.SinApellidos);

    /// <summary>
    /// El árbol entero, sin selección ni botones, sobre el fondo de la aplicación (para la imagen y el PDF); con
    /// <paramref name="marca"/>, debajo, «Hecho con Ramo» a la derecha.
    /// </summary>
    private void DibujarEntero(DrawingContext ctx, bool marca)
    {
        var lay = _layout!;
        var sel = _sel; var res = _res;
        _sel = null; _res = Resaltado.Ninguno;
        try
        {
            double pie = Pie(marca);
            ctx.FillRectangle(new SolidColorBrush(_p.FondoImagen), new Rect(0, 0, lay.Ancho, lay.Alto + pie));
            DibujarMundo(ctx, new Rect(0, 0, lay.Ancho, lay.Alto), lejos: false, conBotones: false);
            if (marca)
            {
                var t = new FormattedText(Core.Idiomas.Idioma.T("Hecho con Ramo"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    LetraApellidos, TamMarca, new SolidColorBrush(_p.TextoMarca));
                double x = t.Width + TamMarca * 2 < lay.Ancho ? lay.Ancho - TamMarca - t.Width : (lay.Ancho - t.Width) / 2;
                ctx.DrawText(t, new Point(x, lay.Alto + (pie - t.Height) / 2));
            }
        }
        finally { _sel = sel; _res = res; }
    }

    /// <summary>Un control fuera de la pantalla que dibuja el árbol entero, para pasarlo a un PDF.</summary>
    private sealed class Hoja(LienzoArbol lienzo, double escala, bool marca) : Control
    {
        public override void Render(DrawingContext context)
        {
            using (context.PushTransform(Matrix.CreateScale(escala, escala)))
                lienzo.DibujarEntero(context, marca);
        }
    }
}

/// <summary>Las partes de una tarjeta que se pueden tocar en la muestra.</summary>
public enum ParteTarjeta { Ninguna, Tarjeta, Foto, Nombre, Apellidos, Fechas, Historia, Boda }
