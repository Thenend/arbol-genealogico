using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Dibujo;
using ArbolGenealogico.Movil.Vistas;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>Un árbol abierto en el ordenador: su archivo (null si aún no se ha guardado), sus cambios y su deshacer.</summary>
internal sealed class Documento(Arbol arbol, string? ruta)
{
    public Arbol Arbol { get; set; } = arbol;
    public string? Ruta { get; set; } = ruta;
    public bool Modificado { get; set; }
    /// <summary>Creado aquí o importado de GEDCOM (cuenta para la versión gratuita al guardarlo).</summary>
    public bool Propio { get; set; }
    public List<Arbol> Deshacer { get; } = new();
    public List<Arbol> Rehacer { get; } = new();
    /// <summary>Quién estaba seleccionado al ir a un árbol enlazado (para volver a él).</summary>
    public string? Seleccion { get; set; }
    public string Titulo => Ruta != null ? Path.GetFileNameWithoutExtension(Ruta)
        : string.IsNullOrWhiteSpace(Arbol.Nombre) ? T("Mi familia") : Arbol.Nombre;
}

/// <summary>
/// Ramo en el ordenador: arriba la barra de herramientas (archivo, deshacer, imprimir, exportar, fusionar, aspecto, buscar
/// y ayuda), en medio el árbol (el mismo lienzo del teléfono, con ratón: clic, doble clic, botón derecho, rueda), abajo a
/// la izquierda la persona seleccionada y abajo a la derecha el zoom y el minimapa. Todo se hace también con el teclado
/// (F1). Los árboles son archivos .ramo donde se quiera, que se guardan al pulsar «Guardar», como en cualquier programa.
/// </summary>
public sealed partial class PantallaPc : Grid, IPantalla
{
    private readonly LienzoArbol _lienzo = new() { ModoRaton = true };
    private readonly List<Documento> _pila = new();
    private Documento? Doc => _pila.Count > 0 ? _pila[^1] : null;

    // barra de herramientas
    private readonly TextBlock _titulo = new() { FontSize = 15.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _migas = new() { FontSize = 12, Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
    private readonly Button _atras;
    private Button _guardar = null!, _deshacer = null!, _rehacer = null!, _aspecto = null!, _completa = null!;
    private readonly List<Control> _conArbol = new();
    private readonly TextBox _buscar = new()
    {
        FontSize = 14, Padding = new Thickness(34, 8, 12, 8), VerticalAlignment = VerticalAlignment.Center,
        PlaceholderText = T("Buscar persona…"),
    };
    private readonly TextBlock _contador = new()
    {
        FontSize = 12, Foreground = Ui.Suave, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 0, 12, 0), IsHitTestVisible = false,
    };
    private List<string> _coincidencias = new();
    private int _actual = -1;

    // sobre el árbol
    private readonly Border _ficha = new()
    {
        Background = Ui.Panel, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12), Margin = new Thickness(16),
        BorderBrush = Ui.Linea, BorderThickness = new Thickness(1), MaxWidth = 470,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = false,
    };
    private readonly TextBlock _ayuda = new()
    {
        FontSize = 12.5, Foreground = Ui.Suave, Margin = new Thickness(16, 0, 0, 16), TextWrapping = TextWrapping.Wrap, MaxWidth = 640,
        Background = Ui.Panel, Padding = new Thickness(10, 5),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, IsVisible = false,
        Text = T("Doble clic: ficha  ·  Clic derecho: añadir familiar y más  ·  Flechas: moverse  ·  Ctrl+F: buscar  ·  F1: atajos"),
    };
    private readonly Border _panelAspecto = new()
    {
        Background = Ui.Panel, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), Margin = new Thickness(0, 12, 16, 0),
        BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsVisible = false,
    };
    private readonly Border _eleccion = new()
    {
        Background = Ui.Oro, CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 8, 8, 8), Margin = new Thickness(0, 12, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, IsVisible = false,
    };
    private readonly TextBlock _textoEleccion = new() { Foreground = Ui.SobreOro, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
    private readonly Control _controlesVista;
    private readonly Border _bienvenida = new() { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public PantallaPc()
    {
        RowDefinitions = new RowDefinitions("Auto,*");
        Licencia.ContarPropios = ContarPropios;
        _atras = Herramienta(Ui.Iconos.Atras, T("Volver"), T("Volver al árbol anterior") + " (Alt+←)", () => _ = Atras(true));
        Children.Add(CrearBarra());

        var zona = new Grid { ClipToBounds = true };
        Grid.SetRow(zona, 1);
        zona.Children.Add(_lienzo);
        zona.Children.Add(_ayuda);
        zona.Children.Add(_ficha);
        _controlesVista = CrearControlesVista();
        _lienzo.Tapado = new Thickness(0, 0, 250, 0);
        zona.Children.Add(_controlesVista);
        zona.Children.Add(_panelAspecto);
        var cancelar = Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cancelar"), () => TerminarEleccion(null), Ui.SobreOro);
        cancelar.Margin = new Thickness(8, 0, 0, 0);
        _eleccion.Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _textoEleccion, cancelar } };
        zona.Children.Add(_eleccion);
        zona.Children.Add(_bienvenida);
        Children.Add(zona);

        var pref = Preferencias.Actual;
        _lienzo.Forma = FormaTarjeta.Leer(pref.Forma);
        _lienzo.MarcaHistoria = pref.MarcaHistoria;
        _lienzo.Ordenacion = Enum.TryParse<Ordenacion>(pref.Ordenacion, out var o) ? o : Ordenacion.A;
        _lienzo.SeleccionCambiada += id =>
        {
            if (_eligiendo != null)
            {
                if (id != null) Dispatcher.UIThread.Post(() => TerminarEleccion(id));
                return;
            }
            ActualizarFicha();
        };
        _lienzo.FichaPedida += id => { if (_eligiendo == null) _ = Editar(id, false); };
        _lienzo.MenuPedido += id => { if (_eligiendo == null) MostrarMenuPersona(id, null); };
        _lienzo.EnlacePedido += id => { if (_eligiendo == null) _ = AbrirEnlace(id); };
        _lienzo.MenuContextualPedido += (id, p) => { if (_eligiendo == null) MostrarMenuPersona(id, p); };
        ToolTip.SetTip(_buscar, T("Buscar persona…") + " (Ctrl+F)");
        _buscar.TextChanged += (_, _) =>
        {
            // el hueco de la derecha es para el contador; sin nada escrito, para que el «Buscar persona…» quepa entero
            _buscar.Padding = new Thickness(34, 8, string.IsNullOrEmpty(_buscar.Text) ? 12 : 64, 8);
            Buscar();
        };
        _buscar.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { IrACoincidencia(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1); e.Handled = true; }
            else if (e.Key == Key.Escape) { _buscar.Text = ""; _lienzo.Focus(); e.Handled = true; }
        };
        Licencia.Cambiada += ActualizarBarra;
        Tema.Cambiado += () => { if (_panelAspecto.IsVisible) LlenarAspecto(); };

        DragDrop.AddDragOverHandler(this, AlArrastrar);
        DragDrop.AddDropHandler(this, AlSoltar);
        DragDrop.AddDragLeaveHandler(this, (_, _) => _lienzo.Destino = null);

        AttachedToVisualTree += (_, _) =>
        {
            if (Doc == null && _bienvenida.IsVisible == false) Dispatcher.UIThread.Post(Empezar, DispatcherPriority.Background);
        };
        ActualizarBarra();
    }

    public bool Atras()
    {
        if (_eligiendo != null) { TerminarEleccion(null); return true; }
        if (_panelAspecto.IsVisible) { MostrarAspecto(false); return true; }
        if (_lienzo.Seleccion != null) { _lienzo.Seleccion = null; return true; }
        if (!string.IsNullOrEmpty(_buscar.Text)) { _buscar.Text = ""; return true; }
        return false;
    }

    // ---------- Barra de herramientas ----------

    /// <summary>Lo que hace cada botón de la barra cuando no cabe y pasa al menú «Más»: su dibujo, su nombre y su acción.</summary>
    private readonly Dictionary<Control, (string Icono, string Texto, Action Accion)> _enMas = new();
    private readonly BarraAdaptable _fila = new();

    /// <summary>Un botón de la barra: el dibujo y debajo su nombre (se entiende sin tener que pasar el ratón por encima).</summary>
    private Button Herramienta(string icono, string texto, string ayuda, Action accion)
    {
        var pila = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Ui.Icono(icono, 21);
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        pila.Children.Add(ic);
        pila.Children.Add(new TextBlock { Text = texto, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center });
        var b = new Button
        {
            Content = pila, Classes = { "plano" }, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 5),
            MinWidth = 58, VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(b, ayuda);
        Avalonia.Automation.AutomationProperties.SetName(b, texto);
        b.Click += (_, _) => accion();
        _enMas[b] = (icono, texto, accion);
        return b;
    }

    private static Control Separador() => new Border { Width = 1, Height = 34, Background = Ui.Linea, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// La barra de herramientas. Si la ventana es estrecha, se encogen el nombre del árbol y la búsqueda, y después los botones
    /// menos usados pasan al botón «Más» (primero Ayuda, luego Fusionar, Aspecto, Abrir, Nuevo, Exportar, Imprimir y Ramo completo).
    /// </summary>
    private Control CrearBarra()
    {
        var f = _fila;
        void Izq(Control c, int prioridad = 0) { BarraAdaptable.Poner(c, prioridad); f.Children.Add(c); }
        void Der(Control c, int prioridad = 0) { BarraAdaptable.Poner(c, prioridad, derecha: true); f.Children.Add(c); }

        Izq(_atras);
        var nombre = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 14, 0), MaxWidth = 250, Children = { _titulo, _migas } };
        Izq(nombre);
        BarraAdaptable.Poner(nombre, minimo: 120);
        Izq(Herramienta(Ui.Iconos.Nuevo, T("Nuevo"), T("Nuevo árbol") + " (Ctrl+N)", () => _ = Nuevo()), 5);
        Izq(Herramienta(Ui.Iconos.Carpeta, T("Abrir"), T("Abrir un árbol (.ramo) o un GEDCOM de otro programa") + " (Ctrl+O)", () => _ = Abrir()), 4);
        _guardar = Herramienta(Ui.Iconos.Guardar, T("Guardar"), T("Guardar") + " (Ctrl+S)", () => _ = Guardar(Doc, false));
        Izq(_guardar);
        Izq(Separador());
        _deshacer = Herramienta(Ui.Iconos.Deshacer, T("Deshacer"), T("Deshacer") + " (Ctrl+Z)", () => Deshacer(false));
        _rehacer = Herramienta(Ui.Iconos.Rehacer, T("Rehacer"), T("Rehacer") + " (Ctrl+Y)", () => Deshacer(true));
        Izq(_deshacer);
        Izq(_rehacer);
        Izq(Separador(), 7);
        var imprimir = Herramienta(Ui.Iconos.Imprimir, T("Imprimir"), T("En una o varias hojas para recortar y pegar, o una lámina para regalar") + " (Ctrl+P)", () => _ = Imprimir());
        Button exportar = null!;
        exportar = Herramienta(Ui.Iconos.Exportar, T("Exportar"), T("Guardar como PDF, como imagen o en GEDCOM") + " (Ctrl+E)", () => MenuExportar(exportar));
        _enMas[exportar] = (Ui.Iconos.Exportar, T("Exportar") + "…", () => MenuExportar(_mas));
        var fusionar = Herramienta(Ui.Iconos.Fusionar, T("Fusionar"), T("Añadir a este árbol lo que tenga otro árbol"), () => _ = FusionarCon());
        Izq(imprimir, 7); Izq(exportar, 6); Izq(fusionar, 2);
        _mas = Herramienta(Ui.Iconos.Menu, T("Más"), T("Más opciones"), MenuMas);
        _enMas.Remove(_mas);
        Izq(_mas);
        f.Mas = _mas;
        _conArbol.AddRange(new Control[] { _guardar, imprimir, exportar, fusionar, _buscar });

        _completa = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children = { Ui.Icono(Ui.Iconos.Estrella, 18, Ui.BotonTexto), new TextBlock { Text = T("Ramo completo"), Foreground = Ui.BotonTexto, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } },
            },
            Classes = { "principal" }, Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(_completa, T("Sin límites y sin la marca «Hecho con Ramo»"));
        _completa.Click += (_, _) => _ = PantallaCompleta.Ofrecer();
        _enMas[_completa] = (Ui.Iconos.Estrella, T("Ramo completo") + "…", () => _ = PantallaCompleta.Ofrecer());
        Der(_completa, 8);
        _aspecto = Herramienta(Ui.Iconos.Tarjeta, T("Aspecto"), T("Colocación del árbol, forma de las tarjetas y colores"), () => MostrarAspecto(!_panelAspecto.IsVisible));
        Der(_aspecto, 3);
        _conArbol.Add(_aspecto);
        var busqueda = new Grid { Margin = new Thickness(8, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        busqueda.Children.Add(_buscar);
        var lupa = Ui.Icono(Ui.Iconos.Buscar, 17, Ui.Suave);
        lupa.HorizontalAlignment = HorizontalAlignment.Left; lupa.Margin = new Thickness(11, 0, 0, 0); lupa.IsHitTestVisible = false;
        busqueda.Children.Add(lupa);
        busqueda.Children.Add(_contador);
        Der(busqueda);
        BarraAdaptable.Poner(busqueda, derecha: true, minimo: 200, preferido: 242);
        Der(Herramienta(Ui.Iconos.Info, T("Ayuda"), T("Atajos de teclado") + " (F1)", () => _ = MostrarAtajos()), 1);

        f.Margin = new Thickness(8, 4);
        return new Border { Background = Ui.Barra, BorderBrush = Ui.Linea, BorderThickness = new Thickness(0, 0, 0, 1), Child = f };
    }

    private Button _mas = null!;

    /// <summary>El menú del botón «Más»: los botones que no caben ahora en la barra.</summary>
    private void MenuMas()
    {
        var menu = new MenuFlyout();
        foreach (var c in _fila.Escondidos)
        {
            if (!_enMas.TryGetValue(c, out var e)) continue;
            var m = new MenuItem { Header = e.Texto, Icon = Ui.Icono(e.Icono, 18), IsEnabled = c.IsEnabled };
            m.Click += (_, _) => e.Accion();
            menu.Items.Add(m);
        }
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
        menu.ShowAt(_mas);
    }

    private void ActualizarBarra()
    {
        var d = Doc;
        _atras.IsVisible = _pila.Count > 1;
        _titulo.Text = d == null ? "Ramo" : d.Titulo + (d.Modificado ? "  ●" : "");
        _migas.Text = d == null ? "" : (_pila.Count > 1 ? string.Join("  ›  ", _pila.Take(_pila.Count - 1).Select(x => x.Titulo)) + "  ·  " : "")
            + Principal.Personas(d.Arbol.Personas.Count) + (d.Ruta == null ? "  ·  " + T("sin guardar") : "");
        ToolTip.SetTip(_titulo, d?.Ruta);
        foreach (var c in _conArbol) c.IsEnabled = d != null;
        _deshacer.IsEnabled = d?.Deshacer.Count > 0;
        _rehacer.IsEnabled = d?.Rehacer.Count > 0;
        _completa.IsVisible = !Licencia.Completa;
        if (TopLevel.GetTopLevel(this) is Window w) w.Title = d == null ? "Ramo" : $"{d.Titulo}{(d.Modificado ? " *" : "")} — Ramo";
    }

    // ---------- Zoom, minimapa ----------

    private Control CrearControlesVista()
    {
        var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 2 };
        botones.Children.Add(Ui.BotonIcono("M5 12 H19", T("Alejar") + " (Ctrl −)", () => _lienzo.Zoom(1 / 1.3)));
        botones.Children.Add(Ui.BotonIcono(Ui.Iconos.Mas, T("Acercar") + " (Ctrl +)", () => _lienzo.Zoom(1.3)));
        botones.Children.Add(Ui.BotonIcono(Ui.Iconos.Encuadrar, T("Ver todo el árbol") + " (Ctrl+0)", () => _lienzo.Encuadrar()));
        botones.Children.Add(Ui.BotonIcono(Ui.Iconos.Casa, T("Ir a la persona principal") + " (" + T("Inicio") + ")", IrAPrincipal));
        var barra = new Border
        {
            Child = botones, Background = Ui.Panel, CornerRadius = new CornerRadius(12), BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
            Padding = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8),
        };
        var mapa = new MiniMapa(_lienzo) { Width = 230, Height = 150 };
        ToolTip.SetTip(mapa, T("Todo el árbol: haz clic o arrastra para ir a esa parte"));
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 16, 16),
            Children = { barra, mapa }, IsVisible = false,
        };
    }

    private void IrAPrincipal()
    {
        if (Doc is not { } d) return;
        _lienzo.Seleccion = d.Arbol.RaizId;
        _lienzo.CentrarEn(d.Arbol.RaizId, Math.Max(_lienzo.Escala, 0.7));
    }

    // ---------- Ficha de la persona seleccionada ----------

    private void ActualizarFicha()
    {
        var p = _lienzo.Seleccion is { } id ? Doc?.Arbol.Buscar(id) : null;
        _ficha.IsVisible = p != null;
        _ayuda.IsVisible = p == null && Doc != null;
        if (p == null) { _ficha.Child = null; return; }
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        var retrato = Ui.Retrato(p, 58);
        retrato.Margin = new Thickness(0, 0, 14, 0); retrato.VerticalAlignment = VerticalAlignment.Top;
        retrato.Cursor = new Cursor(StandardCursorType.Hand);
        retrato.PointerPressed += (_, e) => { if (VerFoto.Abrir(p)) e.Handled = true; };
        g.Children.Add(retrato);
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock { Text = Nombres.SinNombre(p) ? T("Sin nombre") : p.Nombre, FontSize = 18, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrWhiteSpace(p.Apellidos)) textos.Children.Add(new TextBlock { Text = p.Apellidos, FontSize = 14, Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis });
        if (Core.Idiomas.Fechas.Vida(p) is { Length: > 0 } vida)
            textos.Children.Add(new TextBlock { Text = vida, FontSize = 13, Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        if (!string.IsNullOrWhiteSpace(p.Historia))
            textos.Children.Add(new TextBlock { Text = p.Historia.Trim(), FontSize = 13, Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(textos, 1); g.Children.Add(textos);
        var botones = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        void Poner(Button b) { b.Margin = new Thickness(0, 0, 8, 0); b.Padding = new Thickness(14, 7); botones.Children.Add(b); }
        Poner(Ui.Boton(T("Ficha"), () => _ = Editar(p.Id, false), icono: Ui.Iconos.Editar));
        Button familiar = null!;
        familiar = Ui.Boton(T("Familiar"), () => MenuAnadir(p.Id, familiar), principal: true, icono: Ui.Iconos.Mas);
        Poner(familiar);
        if (!string.IsNullOrEmpty(p.ArbolEnlazado)) Poner(Ui.Boton(T("Su árbol"), () => _ = AbrirEnlace(p.Id), icono: Ui.Iconos.Enlace));
        Grid.SetRow(botones, 1); Grid.SetColumnSpan(botones, 2); g.Children.Add(botones);
        _ficha.Child = g;
    }

    // ---------- Búsqueda ----------

    private void Buscar()
    {
        if (Doc is not { } d) return;
        var texto = _buscar.Text ?? "";
        var encontradas = d.Arbol.BuscarPorNombre(texto).Select(p => p.Id).ToList();
        var lay = _lienzo.Layout;
        _coincidencias = lay == null ? encontradas : encontradas.Where(lay.Cartas.ContainsKey)
            .OrderBy(id => Math.Round(lay.Cartas[id].Y)).ThenBy(id => lay.Cartas[id].X).ToList();
        _actual = -1;
        _lienzo.ResaltarCoincidencias(_coincidencias);
        _contador.Foreground = Ui.Suave;
        if (string.IsNullOrWhiteSpace(texto)) _contador.Text = "";
        else if (_coincidencias.Count == 0) { _contador.Text = T("Ninguna"); _contador.Foreground = Ui.Rojo; }
        else _contador.Text = _coincidencias.Count.ToString(Cultura);
    }

    private void IrACoincidencia(int paso)
    {
        int n = _coincidencias.Count;
        if (n == 0) return;
        _actual = _actual < 0 ? (paso > 0 ? 0 : n - 1) : ((_actual + paso) % n + n) % n;
        var id = _coincidencias[_actual];
        _contador.Text = $"{_actual + 1}/{n}";
        _lienzo.Seleccion = id;
        _lienzo.CentrarEn(id, Math.Max(_lienzo.Escala, 0.7));
    }

    // ---------- Aspecto: colocación, tarjetas y colores ----------

    private void MostrarAspecto(bool ver)
    {
        if (ver) LlenarAspecto();
        _panelAspecto.IsVisible = ver;
        _aspecto.Foreground = ver ? Ui.Oro : Ui.Texto;
    }

    /// <summary>Un grupo de opciones en fila, con la elegida marcada (como en el panel de imprimir).</summary>
    private static Control Segmentos(IReadOnlyList<(string Texto, string? Icono, string? Ayuda)> opciones, int elegida, Action<int> elegir)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < opciones.Count; i++)
        {
            int k = i;
            bool marcada = i == elegida;
            var color = marcada ? Ui.SobreOro : Ui.Texto;
            var dentro = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (opciones[i].Icono is { } ic) dentro.Children.Add(Ui.Icono(ic, 18, color));
            dentro.Children.Add(new TextBlock { Text = opciones[i].Texto, FontSize = 13.5, Foreground = color, VerticalAlignment = VerticalAlignment.Center, FontWeight = marcada ? FontWeight.SemiBold : FontWeight.Normal });
            var b = new Button { Content = dentro, Classes = { "segmento" }, Background = marcada ? Ui.Oro : Brushes.Transparent, Padding = new Thickness(10, 6), MinHeight = 34 };
            if (opciones[i].Ayuda is { } ayuda) ToolTip.SetTip(b, ayuda);
            b.Click += (_, _) => { if (k != elegida) elegir(k); };
            fila.Children.Add(b);
        }
        return new Border
        {
            Child = fila, Background = Ui.Fondo, CornerRadius = new CornerRadius(12), BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
            Padding = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    private void LlenarAspecto()
    {
        var f = _lienzo.Forma;
        var rejilla = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowSpacing = 6, ColumnSpacing = 12 };
        int fila = 0;
        void Fila(string nombre, Control c)
        {
            rejilla.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var t = new TextBlock { Text = nombre, FontSize = 13, Foreground = Ui.Suave, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(t, fila); rejilla.Children.Add(t);
            Grid.SetRow(c, fila); Grid.SetColumn(c, 1); rejilla.Children.Add(c);
            fila++;
        }
        var ordenes = new[] { Ordenacion.A, Ordenacion.B, Ordenacion.C, Ordenacion.D };
        var iconos = new[] { Ui.Iconos.Compacto, Ui.Iconos.Lateral, Ui.Iconos.Balanceado, Ui.Iconos.Escalonado };
        var detalles = new[]
        {
            T("Como los árboles impresos: lo más junto posible"), T("Cada antepasado con su rama, a los lados"),
            T("Los dos lados repartidos"), T("Estrecho, en escalera: para imprimir"),
        };
        Fila(T("Colocación del árbol"), Segmentos(ordenes.Select((o, i) => (Nombres.Ordenacion(o), (string?)iconos[i], (string?)detalles[i])).ToList(),
            Array.IndexOf(ordenes, _lienzo.Ordenacion), i => { CambiarOrdenacion(ordenes[i]); LlenarAspecto(); }));
        var colores = new WrapPanel { MaxWidth = 470 };
        foreach (var t in Tema.Todos)
        {
            bool marcado = t == Tema.Actual;
            var dentro = new StackPanel { Spacing = 4, Children = { Ui.MuestraTema(t), new TextBlock { Text = t.Nombre, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center, Foreground = marcado ? Ui.Oro : Ui.Texto, FontWeight = marcado ? FontWeight.SemiBold : FontWeight.Normal } } };
            var b = new Button { Content = dentro, Classes = { "plano" }, CornerRadius = new CornerRadius(12), Padding = new Thickness(6), BorderBrush = marcado ? Ui.Oro : Brushes.Transparent, BorderThickness = new Thickness(2) };
            ToolTip.SetTip(b, t.Detalle);
            b.Click += (_, _) => CambiarTema(t);
            colores.Children.Add(b);
        }
        Fila(T("Estilo"), colores);

        Fila(T("Tarjetas"), new AjusteTarjeta(() => _lienzo.Forma, () => _lienzo.MarcaHistoria,
                CambiarForma, CambiarMarcaHistoria, CambiarLetra, 470));


        _panelAspecto.Child = rejilla;
    }

    private static void CambiarLetra(Letra l)
    {
        Letra.Poner(l);
        Preferencias.Actual.Letra = l.Id;
        Preferencias.Actual.Guardar();
    }

    /// <summary>Pone un estilo con su letra (la que se hubiera elegido antes se olvida; luego se puede cambiar).</summary>
    private static void CambiarTema(Tema t)
    {
        Tema.Poner(t);
        Letra.Poner(null);
        Preferencias.Actual.Tema = t.Id;
        Preferencias.Actual.Letra = null;
        Preferencias.Actual.Guardar();
    }

    private void CambiarMarcaHistoria(bool marcar)
    {
        _lienzo.MarcaHistoria = marcar;
        Preferencias.Actual.MarcaHistoria = marcar;
        Preferencias.Actual.Guardar();
    }

    private void CambiarOrdenacion(Ordenacion o)
    {
        _lienzo.Ordenacion = o;
        Preferencias.Actual.Ordenacion = o.ToString();
        Preferencias.Actual.Guardar();
    }

    private void CambiarForma(FormaTarjeta f)
    {
        _lienzo.Forma = f;
        Preferencias.Actual.Forma = f.ToString();
        Preferencias.Actual.Guardar();
    }

    // ---------- Atajos de teclado ----------

    internal void Teclear(KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control), mayus = e.KeyModifiers.HasFlag(KeyModifiers.Shift), alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var k = e.Key;
        bool escribiendo = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;
        // mientras se escribe en el buscador, las teclas son suyas (salvo los atajos de archivo y Ctrl+F)
        if (escribiendo && !(ctrl && k is Key.S or Key.O or Key.N or Key.F or Key.P)) return;
        bool hecho = true;
        var d = Doc;
        if (ctrl && k == Key.S) _ = Guardar(d, mayus);
        else if (ctrl && k == Key.O) _ = Abrir();
        else if (ctrl && k == Key.N) _ = Nuevo();
        else if (k == Key.F1) _ = MostrarAtajos();
        else if (d == null) hecho = false;
        else if (ctrl && k == Key.Z) Deshacer(mayus);
        else if (ctrl && k == Key.Y) Deshacer(true);
        else if (ctrl && k == Key.F) { _buscar.Focus(); _buscar.SelectAll(); }
        else if (ctrl && k == Key.P) _ = Imprimir();
        else if (ctrl && k == Key.E) MenuExportar(null);
        else if (ctrl && k == Key.V) _ = PegarFoto();
        else if (ctrl && k is Key.D0 or Key.NumPad0) _lienzo.Encuadrar();
        else if (ctrl && k is Key.Add or Key.OemPlus) _lienzo.Zoom(1.3);
        else if (ctrl && k is Key.Subtract or Key.OemMinus) _lienzo.Zoom(1 / 1.3);
        else if (ctrl && k == Key.L) CambiarOrdenacion((Ordenacion)(((int)_lienzo.Ordenacion + 1) % 4));
        else if (alt && k == Key.Left) _ = Atras(true);
        else if (k == Key.Home) IrAPrincipal();
        else if (k == Key.F3) IrACoincidencia(mayus ? -1 : 1);
        else if (k == Key.Escape) hecho = Principal.Actual.Escape();
        else if (ctrl && k is Key.Left or Key.Right or Key.Up or Key.Down)
            _lienzo.Mover(k == Key.Left ? 140 : k == Key.Right ? -140 : 0, k == Key.Up ? 140 : k == Key.Down ? -140 : 0);
        else if (k is Key.Left or Key.Right or Key.Up or Key.Down) Navegar(k);
        else if (_lienzo.Seleccion is { } id)
        {
            if (ctrl && k == Key.Enter) _ = AbrirEnlace(id);
            else if (k is Key.Enter or Key.F2) _ = Editar(id, false);
            else if (k == Key.Delete) _ = Eliminar(id);
            else if (k is Key.Insert or Key.Add or Key.OemPlus) MostrarMenuPersona(id, null);
            else hecho = false;
        }
        else hecho = false;
        if (hecho) e.Handled = true;
    }

    /// <summary>Con las flechas: ← → en la misma fila, ↑ a un padre, ↓ a un hijo (sin nadie elegido, la persona principal).</summary>
    private void Navegar(Key k)
    {
        if (Doc is not { } d || _lienzo.Layout is not { } lay) return;
        var actual = _lienzo.Seleccion;
        string? destino;
        if (actual == null || !lay.Cartas.TryGetValue(actual, out var c)) destino = d.Arbol.RaizId;
        else
        {
            var a = d.Arbol;
            IEnumerable<string> candidatos = k switch
            {
                Key.Up => a.UnionComoHijo(actual)?.Parejas ?? new List<string>(),
                Key.Down => a.UnionesComoPareja(actual).SelectMany(u => u.Hijos),
                _ => Array.Empty<string>(),
            };
            var preferidos = candidatos.Where(lay.Cartas.ContainsKey).ToList();
            double alto = lay.Opciones.AltoCarta;
            // si no tiene padres o hijos, la tarjeta más cercana en ese sentido
            var lista = preferidos.Count > 0 ? preferidos : lay.Cartas.Keys.Where(id => id != actual && (k switch
            {
                Key.Up => lay.Cartas[id].Y < c.Y - alto / 2,
                Key.Down => lay.Cartas[id].Y > c.Y + alto / 2,
                Key.Left => Math.Abs(lay.Cartas[id].Y - c.Y) < alto / 2 && lay.Cartas[id].X < c.X,
                _ => Math.Abs(lay.Cartas[id].Y - c.Y) < alto / 2 && lay.Cartas[id].X > c.X,
            })).ToList();
            destino = lista.Count == 0 ? null : lista.MinBy(id => Math.Pow(lay.Cartas[id].X - c.X, 2) + Math.Pow(lay.Cartas[id].Y - c.Y, 2) * 0.5);
        }
        if (destino == null) return;
        _lienzo.Seleccion = destino;
        _lienzo.AsegurarVisible(destino, _ficha.IsVisible ? _ficha.Bounds.Height + 16 : 0);
    }

    private Task MostrarAtajos() => Principal.Actual.Avisar(T("Atajos de teclado"), string.Join("\n", new[]
    {
        T("Clic: elegir a una persona (se ilumina su familia)"),
        T("Doble clic, Intro o F2: abrir su ficha"),
        T("Clic derecho, Insert o +: añadir familiares y más"),
        T("Supr: eliminar a la persona elegida"),
        T("Flechas: moverse por el árbol (↑ padres, ↓ hijos)"),
        T("Ctrl+Intro: abrir su árbol enlazado · Alt+←: volver"),
        T("Rueda del ratón o Ctrl + / Ctrl −: zoom · Ctrl+0: ver todo · Inicio: persona principal"),
        T("Arrastrar el fondo o Ctrl+flechas: mover el árbol"),
        T("Ctrl+F: buscar (Intro: la siguiente)"),
        T("Arrastrar una foto sobre una tarjeta, o Ctrl+V: ponerle esa foto"),
        T("Ctrl+L: cambiar la colocación del árbol"),
        T("Ctrl+P: imprimir · Ctrl+E: exportar"),
        T("Ctrl+Z / Ctrl+Y: deshacer / rehacer"),
        T("Ctrl+N / Ctrl+O / Ctrl+S: nuevo / abrir / guardar (Ctrl+Mayús+S: guardar como)"),
        T("Esc: quitar la elección o cerrar lo que esté abierto"),
    }));
}

public sealed partial class PantallaPc
{
    /// <summary>Para las capturas de prueba (el proyecto de escritorio): hace lo que haría un clic o una tecla.</summary>
    internal async Task Prueba(string orden, string valor)
    {
        string Id(string nombre) => Doc!.Arbol.Personas.First(p => p.NombreCompleto.StartsWith(nombre, StringComparison.OrdinalIgnoreCase)).Id;
        switch (orden)
        {
            case "abrir": Cargar(valor); break;
            case "ejemplo": await AbrirEjemplo(); break;
            case "sel": _lienzo.Seleccion = Id(valor); _lienzo.CentrarEn(Id(valor), null, false); break;
            case "ficha": _ = Editar(Id(valor), false); break;
            case "menu": _lienzo.Seleccion = Id(valor); MostrarMenuPersona(Id(valor), null); break;
            case "anadir": _lienzo.Seleccion = Id(valor); MenuAnadir(Id(valor), _ficha); break;
            case "parentesco": _ = CambiarParentesco(Id(valor)); break;
            case "aspecto": MostrarAspecto(true); break;
            case "buscar": _buscar.Text = valor; break;
            case "atajos": _ = MostrarAtajos(); break;
            case "exportar": MenuExportar(null); break;
            case "mas": MenuMas(); break;
            case "imprimir": _ = Imprimir(); break;
            case "fusionar": _ = Fusionar(PantallaInicio.TraducirEjemplo(ArbolJson.Cargar(valor, out _)), Path.GetFileNameWithoutExtension(valor)); break;
            case "todo": _lienzo.Encuadrar(false); break;
            default: throw new ArgumentException("Paso desconocido: " + orden);
        }
    }
}
